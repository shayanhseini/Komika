// VisionFeed — camera + MediaPipe pose model, nothing else.
//
// Emits the raw pose landmarks of every camera frame. It contains NO game
// logic: the wings gesture, the lock and the keys live in the C# core inside
// Unity (compiled to WebAssembly).
//
// Performance on weak devices:
//   · the model runs in a Web Worker: the page / Unity main thread never waits
//   · Lite model by default; drops to Lite automatically if a bigger one is slow
//   · at most `maxFps` inferences per second; setPaused(true) stops inference
//     entirely (menus, results screen) while keeping the camera open
//
// Accuracy in crowds and bad light (see image-cues.js):
//   · setFocus(playerLandmarks): once the game has chosen the player, the model
//     only sees a crop around them (others cut away, player bigger in the input)
//   · player-priority auto-exposure + the camera's own exposure point on the player
//   · a clothing-colour signature per person (`sigs`) for identity tracking

import { openCamera, listCameras, watchCameras, stopStream, cameraSupport, CameraError, CAMERA_ERROR } from './camera.js';
import { createPoseDetector, TASKS_VISION_VERSION } from './pose.js';

export { TASKS_VISION_VERSION, CAMERA_ERROR };

export const FEED_STATUS = Object.freeze({
  IDLE: 'idle',
  LOADING: 'loading',
  RUNNING: 'running',
  MODEL_FAILED: 'model-failed',   // model / WebAssembly files could not be loaded
  ...CAMERA_ERROR,
});

const STORE_KEY = 'dronevision.camera';
const store = {
  get() { try { return localStorage.getItem(STORE_KEY); } catch { return null; } },
  set(v) { try { localStorage.setItem(STORE_KEY, v); } catch { /* private mode */ } },
};

export class VisionFeed {
  /**
   * @param o.model       'lite' (default, light on any GPU) | 'full' | 'heavy' | 'auto'
   * @param o.delegate    'auto' (default: the worker measures GPU vs CPU on this device and keeps
   *                    the faster — on the dev PC GPU-in-worker gave 9 FPS, CPU 20.7) | 'GPU' | 'CPU'
   * @param o.numPoses    1 (default, like the Python app): MediaPipe follows the one person it
   *                    found and never looks for a second, so a neighbour is not even seen.
   *                    2–3 make it search the whole frame every frame (measured 50–75% slower
   *                    with one person in view) and hand the neighbour to the tracker.
   * @param o.adaptive    drop to the lite model when FPS is low (default true)
   * @param o.worker      'auto' (default): run the model in a Web Worker when supported
   * @param o.maxFps      inference rate cap (default 30)
   * @param o.focus       crop around the player once setFocus() is called (default FALSE: on a
   *                    crop the Tasks Lite model jumps off the hands; owner's video 16-35 big wrist
   *                    jumps vs 2 without, even with a crop that never moves). The callers set it
   *                    only AFTER the lock: a crop that moved during the "hands still" moment kept
   *                    the player from locking.
   * @param o.exposure    adaptive shadow lift in software (default true)
   * @param o.cameraControl  also drive the camera's own exposure / metering point
   *                    (default false: on some webcams this breaks auto-exposure)
   * @param o.width/height/fps  requested camera format (default 640×480@30)
   * @param o.assetsBase  vendor folder URL (default: ../vendor/ next to src/)
   * @param o.video       existing <video> to use (default: a hidden one is created)
   */
  constructor(o = {}) {
    this.opts = {
      model: 'lite', delegate: 'auto', numPoses: 1, adaptive: true, worker: 'auto', maxFps: 24,
      focus: false, exposure: true, cameraControl: false,
      width: 640, height: 360, fps: 30,
      assetsBase: new URL('../vendor/', import.meta.url).href,
      ...o,
    };
    this.video = o.video || createHiddenVideo();
    this.detector = null;
    this.stream = null;
    this.source = 'none';      // 'camera' | 'video' | 'simulator'
    this.cameras = [];
    this.cameraId = null;
    this.cameraLabel = '';
    this.poses = [];           // raw poses of the last processed frame (full-frame coords)
    this.sigs = [];            // clothing-colour signature [r,g,b] per pose, or null
    this.roi = null;           // crop the model looks at ({x,y,w,h} 0..1), null = whole frame
    this._focusSeen = 0;
    this.aspect = 4 / 3;
    this.frameSeq = 0;
    this.frameT = 0;
    this.paused = false;
    this.runStatus = FEED_STATUS.RUNNING;
    this._handlers = {};
    this._running = false;
    this._loopToken = 0;
    this._lastFrameT = 0;
    this._lastSent = 0;
    this._slowSince = 0;
    this._startedAt = 0;
    this._busy = false;
    this._dirty = false;
    this._wakeLock = null;
    this._objectUrl = null;
    this._recovering = false;
    this._unwatch = watchCameras(() => this.refreshCameras());
    this._onVisibility = () => this._visibilityChanged();
    document.addEventListener('visibilitychange', this._onVisibility);
    this.status = FEED_STATUS.IDLE;
    this.stats = { fps: 0, inferMs: 0, latencyMs: 0, model: '', delegate: '', runner: '', resolution: '', gain: 1, lum: 0, focus: false };
  }

  // ---- events --------------------------------------------------------------
  on(event, fn) { (this._handlers[event] ||= new Set()).add(fn); return () => this.off(event, fn); }
  off(event, fn) { this._handlers[event]?.delete(fn); }
  _emit(event, arg) { this._handlers[event]?.forEach(fn => { try { fn(arg); } catch (e) { console.error(e); } }); }

  _setStatus(status) {
    if (status === this.status) return;
    this.status = status;
    this._emit('status', status);
  }

  // ---- cameras ---------------------------------------------------------------
  async refreshCameras() {
    try {
      this.cameras = await listCameras();
    } catch {
      this.cameras = [];
    }
    this._emit('cameras', this.cameras);
    return this.cameras;
  }

  /**
   * Start with the camera. Uses the remembered camera if it is still plugged in,
   * otherwise the front camera. Loads the pose model in parallel with the camera.
   */
  async start({ deviceId } = {}) {
    this._setStatus(FEED_STATUS.LOADING);
    const unsupported = cameraSupport();
    if (unsupported) {
      this._setStatus(unsupported);
      this._emit('error', new CameraError(unsupported));
      return false;
    }
    const model = this._ensureDetector();
    try {
      await this.refreshCameras();
      const remembered = deviceId || store.get();
      const known = remembered && this.cameras.some(c => c.id === remembered) ? remembered : null;
      await this._openCamera(known);
      await model;
    } catch (err) {
      this._fail(err);
      return false;
    }
    this._run();
    return true;
  }

  async useCamera(deviceId) {
    this._setStatus(FEED_STATUS.LOADING);
    try {
      await this._openCamera(deviceId);
      await this._ensureDetector();
    } catch (err) {
      this._fail(err);
      return false;
    }
    this._run();
    return true;
  }

  // A USB camera that drops out (loose cable, hub reset) is reopened
  // automatically: the same one if it comes back, otherwise any camera.
  async _recoverCamera() {
    if (this._recovering) return;
    this._recovering = true;
    this._setStatus(FEED_STATUS.LOADING);
    const id = this.cameraId;
    try {
      for (let attempt = 0; attempt < 5 && this.source === 'camera'; attempt++) {
        await new Promise(r => setTimeout(r, 600 + attempt * 400));
        await this.refreshCameras();
        const same = id && this.cameras.some(c => c.id === id);
        try {
          await this._openCamera(same ? id : null);
          this._run();
          return;
        } catch { /* try again */ }
      }
      this._fail(new CameraError(CAMERA_ERROR.NOT_FOUND));
    } finally {
      this._recovering = false;
    }
  }

  async _openCamera(deviceId) {
    this._stopSource();
    stopStream(this.stream);
    this._revokeObjectUrl();
    this.video.removeAttribute('src');
    const cam = await openCamera(this.video, {
      deviceId, width: this.opts.width, height: this.opts.height, fps: this.opts.fps,
    });
    this.stream = cam.stream;
    this.source = 'camera';
    this.roi = null;
    this._poiOK = undefined;
    this._expoOK = undefined;
    this._expoV = undefined;
    this.cameraId = cam.settings.deviceId || deviceId || null;
    this.cameraLabel = cam.label;
    if (this.cameraId) store.set(this.cameraId);
    this.stats.resolution = `${this.video.videoWidth}×${this.video.videoHeight}`;
    await this.refreshCameras(); // labels become available after permission
    cam.track?.addEventListener('ended', () => {
      if (this.source === 'camera' && this.stream === cam.stream) this._recoverCamera();
    });
  }

  /** Test with a recorded video (e.g. booth footage) instead of the camera. */
  async useVideoFile(file) {
    this._setStatus(FEED_STATUS.LOADING);
    this._stopSource();
    stopStream(this.stream);
    this.stream = null;
    this.video.srcObject = null;
    this._revokeObjectUrl();
    this._objectUrl = URL.createObjectURL(file);
    this.video.src = this._objectUrl;
    this.video.loop = true;
    this.video.muted = true;
    await this.video.play().catch(() => {});
    if (!this.video.videoWidth) await new Promise(r => this.video.addEventListener('loadedmetadata', r, { once: true }));
    this.source = 'video';
    this.cameraLabel = file.name;
    this.stats.resolution = `${this.video.videoWidth}×${this.video.videoHeight}`;
    try {
      await this._ensureDetector();
    } catch (err) {
      this._fail(err);
      return false;
    }
    this._run();
    return true;
  }

  /** Hook for subclasses with extra sources (the test page's simulator). */
  _stopSource() {}

  _revokeObjectUrl() {
    if (this._objectUrl) URL.revokeObjectURL(this._objectUrl);
    this._objectUrl = null;
  }

  // Phones: the player never touches the screen, so keep it awake while playing,
  // and resume the camera when the page comes back to the foreground.
  async _keepAwake(on) {
    try {
      if (on && !this._wakeLock && document.visibilityState === 'visible' && navigator.wakeLock) {
        this._wakeLock = await navigator.wakeLock.request('screen');
        this._wakeLock.addEventListener('release', () => { this._wakeLock = null; });
      } else if (!on && this._wakeLock) {
        await this._wakeLock.release();
        this._wakeLock = null;
      }
    } catch { /* not supported / denied: harmless */ }
  }

  _visibilityChanged() {
    if (document.visibilityState !== 'visible' || !this._running) return;
    if (this.source === 'camera' || this.source === 'video') {
      this.video.play().catch(() => {});
      this._keepAwake(true);
    }
  }

  /** Stop/resume inference (camera stays open). Use it in menus to save battery and GPU. */
  setPaused(paused) {
    this.paused = !!paused;
  }

  async _ensureDetector() {
    if (this.detector) return this.detector;
    if (!this._detectorPromise) {
      this._detectorPromise = createPoseDetector({
        model: this.opts.model, delegate: this.opts.delegate, worker: this.opts.worker,
        numPoses: this.opts.numPoses, assetsBase: this.opts.assetsBase,
      }).then(d => {
        this.detector = d;
        // the worker may switch GPU -> CPU by itself if the GPU fails at run time
        d.onModelChange = () => this._modelChanged();
        this._modelChanged();
        return d;
      }).catch(err => {
        this._detectorPromise = null;
        const e = err instanceof Error ? err : new Error(String(err));
        e.modelFailed = true;
        throw e;
      });
    }
    return this._detectorPromise;
  }

  _modelChanged() {
    const d = this.detector;
    if (!d) return;
    Object.assign(this.stats, { model: d.model, delegate: d.delegate, runner: d.mode });
    this._emit('model', { model: d.model, delegate: d.delegate, source: d.modelSource, runner: d.mode });
  }

  async setModel(model) {
    this.opts.model = model;
    if (!this.detector) return;
    await this.detector.setModel(model);
    this._slowSince = 0;
    this._modelChanged();
  }

  async setNumPoses(n) {
    this.opts.numPoses = n;
    if (this.detector) await this.detector.setNumPoses(n);
  }

  _fail(err) {
    const code = err instanceof CameraError ? err.code : err && err.modelFailed ? FEED_STATUS.MODEL_FAILED : FEED_STATUS.FAILED;
    console.error('[dronevision]', err);
    this._running = false;
    this._setStatus(code);
    this._keepAwake(false);
    this._emit('error', err);
  }

  // ---- frame loop --------------------------------------------------------------
  _run() {
    const token = ++this._loopToken;
    this._running = true;
    this._startedAt = performance.now();
    this._lastFrameT = 0;
    this._busy = false;
    this._dirty = false;
    if (this.detector) Object.assign(this.stats, { model: this.detector.model, delegate: this.detector.delegate, runner: this.detector.mode });
    if (this.source === 'camera') this._keepAwake(true);
    this._setStatus(this.runStatus);
    const v = this.video;
    if ('requestVideoFrameCallback' in HTMLVideoElement.prototype) {
      // exactly one inference per new camera frame: no duplicates, no waiting
      const tick = (now, meta) => {
        if (token !== this._loopToken) return;
        this._frame(now, meta.captureTime || meta.presentationTime || now);
        v.requestVideoFrameCallback(tick);
      };
      v.requestVideoFrameCallback(tick);
    } else {
      let lastTime = -1;
      const tick = now => {
        if (token !== this._loopToken) return;
        if (v.currentTime !== lastTime) {
          lastTime = v.currentTime;
          this._frame(now, now);
        }
        requestAnimationFrame(tick);
      };
      requestAnimationFrame(tick);
    }
  }

  _frame(now, captureT) {
    const v = this.video;
    if (this.paused || !this.detector || !v.videoWidth || v.readyState < 2) return;
    const t = performance.now();
    if (t - this._lastSent < 1000 / this.opts.maxFps - 4) return;
    if (this.detector.async) {
      // one frame in flight; while the worker is busy only the NEWEST frame waits
      if (this._busy) this._dirty = true;
      else this._sendFrame(captureT);
      return;
    }
    this._lastSent = t;
    let r;
    try {
      r = this.detector.detect(v, t, this.roi, this.opts.exposure);
    } catch (err) {
      console.error('[dronevision] detect failed', err);
      return;
    }
    this.stats.inferMs = ema(this.stats.inferMs, performance.now() - t, 0.1);
    this.stats.gain = r.gain;
    this.stats.lum = r.lum;
    this._deliver(r.poses, r.sigs, v.videoWidth / v.videoHeight, t, captureT);
    this._adapt(t);
  }

  async _sendFrame(captureT) {
    const token = this._loopToken;
    const v = this.video;
    this._busy = true;
    try {
      const t0 = performance.now();
      this._lastSent = t0;
      const w = v.videoWidth, h = v.videoHeight;
      const roi = this.roi;
      // ship only the region the model should look at, never more than 320 px on its long side
      const sx = roi ? roi.x * w : 0, sy = roi ? roi.y * h : 0;
      const sw = roi ? roi.w * w : w, sh = roi ? roi.h * h : h;
      const k = Math.min(1, 320 / Math.max(sw, sh));
      const bitmap = await createImageBitmap(v, Math.round(sx), Math.round(sy), Math.round(sw), Math.round(sh),
        k < 1 ? { resizeWidth: Math.round(sw * k), resizeHeight: Math.round(sh * k), resizeQuality: 'low' } : undefined);
      if (token !== this._loopToken || !this.detector) { bitmap.close(); return; }
      const r = await this.detector.detect(bitmap, t0, roi, this.opts.exposure);
      if (token !== this._loopToken) return;
      this.stats.inferMs = ema(this.stats.inferMs, r.inferMs, 0.1);
      this.stats.gain = r.gain;
      this.stats.lum = r.lum;
      this._deliver(r.poses, r.sigs, w / h, t0, captureT);
      this._adapt(t0);
    } catch (err) {
      if (token === this._loopToken) console.warn('[dronevision] frame skipped:', err.message || err);
    } finally {
      this._busy = false;
      if (this._dirty && token === this._loopToken && !this.paused) {
        this._dirty = false;
        this._sendFrame(performance.now());
      }
    }
  }

  _deliver(poses, sigs, aspect, t, captureT) {
    if (this._lastFrameT) {
      const dt = t - this._lastFrameT;
      if (dt > 0) this.stats.fps = ema(this.stats.fps, 1000 / dt, 0.1);
    }
    this._lastFrameT = t;
    this.stats.latencyMs = ema(this.stats.latencyMs, Math.max(0, performance.now() - captureT), 0.1);
    this.poses = poses;
    this.sigs = sigs || [];
    this.aspect = aspect;
    this.frameT = t;
    this.frameSeq = (this.frameSeq + 1) % 16777216;
    this.stats.focus = !!this.roi;
    this._cameraExposure();
    this._onPoses(poses, this.sigs, aspect, t);
  }

  /** Override to consume frames. */
  _onPoses(poses, sigs, aspect, t) {
    this._emit('poses', { poses, sigs, aspect, t });
  }

  /**
   * Tell the feed who the player is (their landmarks, full-frame coords), or null
   * when there is no player. Call it once per processed frame. The model then
   * looks only at a stable crop around the player; when the player is lost the
   * crop widens quickly and returns to the whole frame.
   */
  setFocus(landmarks) {
    const now = performance.now();
    if (!this.opts.focus) { this.roi = null; return; }
    const box = landmarks ? bodyExtent(landmarks) : null;
    if (box) {
      this._focusSeen = now;
      const bw = box.x1 - box.x0, bh = box.y1 - box.y0;
      // generous margins: room to swing the arms, lean, step a little. Extra HEADROOM:
      // the landmarks stop at the eyes, the head does not, and the model needs the
      // whole face (measured: a close player with the old crop was found in 34/53
      // frames, with headroom 30/30)
      const w = Math.min(1, Math.max(bw * 2.0, 0.45)), h = Math.min(1, Math.max(bh * 1.6 + 0.1, 0.6));
      const cx = (box.x0 + box.x1) / 2, cy = (box.y0 + box.y1) / 2 - bh * 0.15;
      const cand = clampRect({ x: cx - w / 2, y: cy - h / 2, w, h });
      const cur = this.roi;
      // hysteresis: keep the crop still while the player stays well inside it
      const inside = cur && box.x0 > cur.x + cur.w * 0.12 && box.x1 < cur.x + cur.w * 0.88
        && (box.y0 > cur.y + cur.h * 0.15 || cur.y <= 0) && box.y1 < cur.y + cur.h * 0.98;
      const sameSize = cur && cand.w / cur.w > 0.8 && cand.w / cur.w < 1.25 && cand.h / cur.h > 0.8 && cand.h / cur.h < 1.25;
      if (!(inside && sameSize)) this.roi = cand.w >= 0.95 && cand.h >= 0.95 ? null : cand;
      this._pointOfInterest(cx, (box.y0 + box.y1) / 2);
    } else if (this.roi) {
      const gone = now - this._focusSeen;
      if (gone > 700) this.roi = null;
      else if (gone > 120) {
        const r = this.roi;
        const w = Math.min(1, r.w * 1.25), h = Math.min(1, r.h * 1.25);
        const grown = clampRect({ x: r.x + r.w / 2 - w / 2, y: r.y + r.h / 2 - h / 2, w, h });
        this.roi = grown.w >= 0.95 && grown.h >= 0.95 ? null : grown;
      }
    }
  }

  // Dark or back-lit scene: ask the camera sensor itself for more exposure where
  // the browser exposes it (Chrome on Android and many desktop webcams). Real
  // exposure raises the signal, which a software curve cannot do in the dark.
  _cameraExposure() {
    if (!this.opts.cameraControl) return;
    const tr = this.stream && this.stream.getVideoTracks()[0];
    if (!tr || !tr.getCapabilities || this._expoOK === false) return;
    const now = performance.now();
    if (now - (this._expoT || 0) < 2000) return;
    this._expoT = now;
    let cap;
    try { cap = tr.getCapabilities().exposureCompensation; } catch { cap = null; }
    if (!cap || typeof cap.max !== 'number') { this._expoOK = false; return; }
    const want = this.stats.gain > 1 || this.stats.lum < 0.15 ? cap.max * 0.6 : 0;
    const v = Math.max(cap.min, Math.min(cap.max, want));
    if (v === this._expoV) return;
    this._expoV = v;
    tr.applyConstraints({ advanced: [{ exposureCompensation: v }] }).catch(() => { this._expoOK = false; });
  }

  // Phones that support it (Chrome on Android): meter exposure/focus on the player.
  _pointOfInterest(x, y) {
    if (!this.opts.cameraControl) return;
    const tr = this.stream && this.stream.getVideoTracks()[0];
    if (!tr || !tr.getCapabilities) return;
    if (this._poiOK === undefined) {
      try { this._poiOK = 'pointsOfInterest' in (tr.getCapabilities() || {}); } catch { this._poiOK = false; }
    }
    const now = performance.now();
    if (!this._poiOK || now - (this._poiT || 0) < 1500) return;
    this._poiT = now;
    tr.applyConstraints({ advanced: [{ pointsOfInterest: [{ x, y }] }] }).catch(() => { this._poiOK = false; });
  }

  // Lower the model when the device cannot keep up (like the Python app's fallback).
  _adapt(now) {
    if (!this.opts.adaptive || !this.detector || this.detector.model === 'lite') return;
    if (now - this._startedAt < 3000) return;
    // as the PC app: slow AND the model itself is slow (a slow camera alone is not the model's fault)
    if (this.stats.fps > 0 && this.stats.fps < 18 && this.stats.inferMs > 40) {
      if (!this._slowSince) this._slowSince = now;
      if (now - this._slowSince > 5000 && !this._swapping) {
        this._swapping = true;
        const next = this.detector.model === 'heavy' ? 'full' : 'lite';
        console.info(`[dronevision] ${this.stats.fps.toFixed(1)} fps: switching to ${next} model`);
        this.setModel(next).finally(() => { this._swapping = false; this._startedAt = performance.now(); });
      }
    } else {
      this._slowSince = 0;
    }
  }

  stop() {
    this._loopToken++;
    this._running = false;
    this._stopSource();
    stopStream(this.stream);
    this.stream = null;
    this._revokeObjectUrl();
    this._keepAwake(false);
    this.source = 'none';
    this._setStatus(FEED_STATUS.IDLE);
  }

  dispose() {
    this.stop();
    this._unwatch();
    document.removeEventListener('visibilitychange', this._onVisibility);
    this.detector?.close();
    this.detector = null;
  }
}

function bodyExtent(lm) {
  let x0 = 1, y0 = 1, x1 = 0, y1 = 0, n = 0;
  for (let i = 0; i < lm.length; i++) {
    const p = lm[i];
    if (!p || (p.visibility ?? 1) < 0.5 || p.x < -0.05 || p.x > 1.05 || p.y < -0.05 || p.y > 1.05) continue;
    x0 = Math.min(x0, p.x); x1 = Math.max(x1, p.x); y0 = Math.min(y0, p.y); y1 = Math.max(y1, p.y);
    n++;
  }
  return n >= 4 ? { x0: Math.max(0, x0), y0: Math.max(0, y0), x1: Math.min(1, x1), y1: Math.min(1, y1) } : null;
}

function clampRect(r) {
  const w = Math.min(1, r.w), h = Math.min(1, r.h);
  return { x: Math.min(Math.max(0, r.x), 1 - w), y: Math.min(Math.max(0, r.y), 1 - h), w, h };
}

function ema(prev, value, a) {
  return prev ? prev + (value - prev) * a : value;
}

function createHiddenVideo() {
  const v = document.createElement('video');
  v.muted = true;
  v.playsInline = true;
  v.setAttribute('playsinline', '');
  v.style.cssText = 'position:fixed;width:1px;height:1px;opacity:0;pointer-events:none;left:-10px;top:-10px';
  document.body.appendChild(v);
  return v;
}
