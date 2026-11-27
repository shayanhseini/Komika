// MediaPipe Tasks Vision PoseLandmarker (WebAssembly + GPU).
//
// Two runners with the same shape:
//   WorkerPoseDetector — inference in a Web Worker (default): the page/game
//                        main thread never waits for the model.
//   PoseDetector       — inference on the main thread (fallback for browsers
//                        without module workers / OffscreenCanvas).
//
// Assets: ./vendor first (tools/fetch-assets.ps1 — no third-party servers at
// run time), then the public CDNs. No HEAD probing: each candidate is simply
// tried in order, so hosts that reject HEAD requests still work.

export const TASKS_VISION_VERSION = '1.0.1';
const CDN = `https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@${TASKS_VISION_VERSION}`;
const MODEL_CDN = name =>
  `https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_${name}/float16/latest/pose_landmarker_${name}.task`;

import { FrameCues, sigLum } from './image-cues.js';

export const MODELS = ['lite', 'full', 'heavy'];
const STRIDE = 33 * 4;

export function isMobile() {
  const uad = navigator.userAgentData;
  if (uad && typeof uad.mobile === 'boolean' && uad.mobile) return true;
  return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1); // iPadOS
}

/** Phones and weak PCs start on the lite model; others on full (auto-drops if slow). */
export function autoModel() {
  const cores = navigator.hardwareConcurrency || 4;
  return isMobile() || cores <= 4 ? 'lite' : 'full';
}

function resolveModel(model) {
  return model && model !== 'auto' ? model : autoModel();
}

function libCandidates(assetsBase) {
  const list = [];
  if (assetsBase) list.push({ lib: `${assetsBase}mediapipe/vision_bundle.js`, wasm: `${assetsBase}mediapipe/wasm`, source: 'local' });
  list.push({ lib: `${CDN}/vision_bundle.mjs`, wasm: `${CDN}/wasm`, source: 'cdn' });
  return list;
}

function modelCandidates(assetsBase, model) {
  const list = [];
  if (assetsBase) list.push({ url: `${assetsBase}models/pose_landmarker_${model}.task`, source: 'local', model });
  list.push({ url: MODEL_CDN(model), source: 'cdn', model });
  return list;
}

/** True when this browser can run the model in a module worker with WebGL. */
export function workerSupported() {
  return typeof Worker !== 'undefined' && typeof OffscreenCanvas !== 'undefined'
    && typeof createImageBitmap === 'function';
}

// ---- main-thread runner ------------------------------------------------------

let _lib = null;

async function loadLibrary(assetsBase) {
  if (_lib) return _lib;
  let lastErr;
  for (const c of libCandidates(assetsBase)) {
    try {
      const mp = await import(/* @vite-ignore */ c.lib);
      const fileset = await mp.FilesetResolver.forVisionTasks(c.wasm);
      _lib = { mp, fileset, source: c.source };
      return _lib;
    } catch (err) {
      lastErr = err;
    }
  }
  throw lastErr;
}

export class PoseDetector {
  /**
   * @param o.model     'auto' | 'lite' | 'full' | 'heavy'
   * @param o.delegate  'auto' (worker: measures GPU vs CPU, keeps the faster) | 'GPU' | 'CPU'
   *                   (GPU falls back to CPU automatically)
   * @param o.numPoses  1 = fastest; 2–3 = crowd mode (operator picked by the tracker)
   * @param o.assetsBase URL of the vendor folder, ending in '/'
   */
  static async create(o = {}) {
    const d = new PoseDetector();
    d.async = false;
    d.mode = 'main';
    d.assetsBase = o.assetsBase || null;
    d.numPoses = o.numPoses || 1;
    d.lib = await loadLibrary(d.assetsBase);
    // main thread: 'auto' = GPU (with the CPU fallback below)
    await d._build(resolveModel(o.model), o.delegate === 'CPU' ? 'CPU' : 'GPU');
    return d;
  }

  async _build(model, delegate) {
    let lastErr;
    for (const m of modelCandidates(this.assetsBase, model)) {
      for (const del of delegate === 'GPU' ? ['GPU', 'CPU'] : ['CPU']) {
        try {
          const lm = await this.lib.mp.PoseLandmarker.createFromOptions(this.lib.fileset, {
            baseOptions: { modelAssetPath: m.url, delegate: del },
            runningMode: 'VIDEO',
            numPoses: this.numPoses,
            minPoseDetectionConfidence: 0.5,
            minPosePresenceConfidence: 0.5,
            minTrackingConfidence: 0.5,
            outputSegmentationMasks: false,
          });
          if (this.landmarker) this.landmarker.close();
          this.landmarker = lm;
          this.model = model;
          this.delegate = del;
          this.modelSource = m.source;
          this.lastTs = -1;
          this.errors = 0;
          this._warmUp();
          return;
        } catch (err) {
          lastErr = err;
        }
      }
    }
    throw lastErr;
  }

  // First inference compiles the GPU shaders: do it while loading, not in play.
  _warmUp() {
    try {
      const c = document.createElement('canvas');
      c.width = c.height = 256;
      c.getContext('2d').fillRect(0, 0, 256, 256);
      this.landmarker.detectForVideo(c, ++this.lastTs);
    } catch { /* best effort */ }
  }

  async setModel(model) {
    model = resolveModel(model);
    if (model !== this.model) await this._build(model, this.delegate);
  }

  async setNumPoses(n) {
    this.numPoses = n;
    await this.landmarker.setOptions({ numPoses: n });
  }

  /**
   * @param source  <video> (full frame)
   * @param roi     region of the frame to analyse ({x,y,w,h} 0..1) or null for all of it
   * @returns { poses, sigs, gain, lum } — landmarks in FULL-frame coords, one list per person
   */
  detect(source, tMs, roi = null, exposure = true) {
    const ts = Math.max(Math.round(tMs), this.lastTs + 1); // MediaPipe needs strictly increasing timestamps
    this.lastTs = ts;
    if (!this.cues) {
      this.cues = new FrameCues((w, h) => { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; });
    }
    const vw = source.videoWidth || source.width, vh = source.videoHeight || source.height;
    const r = roi || { x: 0, y: 0, w: 1, h: 1 };
    try {
      const input = this.cues.prepare(source, r.x * vw, r.y * vh, r.w * vw, r.h * vh, exposure);
      const result = this.landmarker.detectForVideo(input, ts);
      this.errors = 0;
      const found = result.landmarks || [];
      this.cues.feedback(found.length, found.length ? sigLum(this.cues.signature(found[0])) : -1);
      const poses = found.map(lm => {
        const sig = this.cues.signature(lm);
        const mapped = lm.map(q => ({ x: r.x + q.x * r.w, y: r.y + q.y * r.h, z: q.z, visibility: q.visibility }));
        return { lm: mapped, sig };
      });
      return { poses: poses.map(p => p.lm), sigs: poses.map(p => p.sig), gain: this.cues.gain, lum: this.cues.lum };
    } catch (err) {
      // some mobile GPUs fail at run time rather than at creation
      if (this.delegate === 'GPU' && ++this.errors >= 3 && !this._rebuilding) {
        this._rebuilding = true;
        this._build(this.model, 'CPU').finally(() => { this._rebuilding = false; });
      }
      throw err;
    }
  }

  close() {
    if (this.landmarker) this.landmarker.close();
    this.landmarker = null;
  }
}

// ---- worker runner -------------------------------------------------------------

export class WorkerPoseDetector {
  static async create(o = {}) {
    const d = new WorkerPoseDetector();
    d.async = true;
    d.mode = 'worker';
    d.assetsBase = o.assetsBase || null;
    d.numPoses = o.numPoses || 1;
    d._pending = new Map();
    d._seq = 0;
    d.worker = new Worker(new URL('./pose-worker.js', import.meta.url), { type: 'module' });
    const ready = new Promise((resolve, reject) => {
      d._onReady = resolve;
      d._onFatal = reject;
    });
    d.worker.onmessage = e => d._message(e.data);
    // old browsers without module workers fail here (parse error) → caller falls back
    d.worker.onerror = e => { e.preventDefault?.(); d._onFatal(new Error(e.message || 'worker failed')); };
    const model = resolveModel(o.model);
    d.worker.postMessage({
      type: 'init', libs: libCandidates(d.assetsBase), models: modelCandidates(d.assetsBase, model),
      delegate: o.delegate || 'auto', numPoses: d.numPoses,
    });
    const timeout = new Promise((_, reject) => setTimeout(() => reject(new Error('worker init timeout')), 60000));
    try {
      await Promise.race([ready, timeout]);
    } catch (err) {
      d.close();
      throw err;
    }
    return d;
  }

  _message(m) {
    if (m.type === 'result') {
      const p = this._pending.get(m.id);
      if (!p) return;
      this._pending.delete(m.id);
      this.timing = { prepMs: m.prepMs, detMs: m.detMs }; // diagnostics: where a slow frame spends its time
      p.resolve({ poses: unpack(m.data, m.count), sigs: unpackSigs(m.sigs, m.count), inferMs: m.inferMs, gain: m.gain, lum: m.lum });
    } else if (m.type === 'ready' || m.type === 'model') {
      this.model = m.model;
      this.delegate = m.delegate;
      this.modelSource = m.source;
      if (m.probe) console.info('[dronevision] delegate probe:', m.probe);
      if (m.type === 'ready') this._onReady();
      if (this._onModel) { const f = this._onModel; this._onModel = null; f(); }
      if (this.onModelChange) this.onModelChange(m);
    } else if (m.type === 'ack') {
      const p = this._pending.get(m.id);
      if (p) { this._pending.delete(m.id); p.resolve(); }
    } else if (m.type === 'error') {
      if (m.fatal) { this._onFatal(new Error(m.message)); return; }
      const p = this._pending.get(m.id);
      if (p) { this._pending.delete(m.id); p.reject(new Error(m.message)); }
      else if (this._onModelErr) { const f = this._onModelErr; this._onModelErr = null; this._onModel = null; f(new Error(m.message)); }
    }
  }

  _call(msg, transfer) {
    const id = ++this._seq;
    return new Promise((resolve, reject) => {
      this._pending.set(id, { resolve, reject });
      this.worker.postMessage({ ...msg, id }, transfer || []);
    });
  }

  /**
   * bitmap is transferred (closed by the worker); roi = the frame region it shows, or null.
   * Resolves to { poses, sigs, inferMs, gain, lum } with landmarks in FULL-frame coords.
   */
  detect(bitmap, tMs, roi = null, exposure = true) {
    return this._call({ type: 'frame', bitmap, ts: tMs, roi, exposure }, [bitmap]);
  }

  setModel(model) {
    model = resolveModel(model);
    if (model === this.model) return Promise.resolve();
    return new Promise((resolve, reject) => {
      this._onModel = resolve;
      this._onModelErr = reject;
      this.worker.postMessage({ type: 'model', models: modelCandidates(this.assetsBase, model) });
    });
  }

  setNumPoses(n) {
    this.numPoses = n;
    return this._call({ type: 'numPoses', n });
  }

  close() {
    if (this.worker) this.worker.terminate();
    this.worker = null;
    for (const p of this._pending.values()) p.reject(new Error('closed'));
    this._pending.clear();
  }
}

function unpackSigs(sigs, count) {
  const out = new Array(count);
  for (let p = 0; p < count; p++) out[p] = sigs && sigs[p * 4 + 3] > 0 ? [sigs[p * 4], sigs[p * 4 + 1], sigs[p * 4 + 2]] : null;
  return out;
}

function unpack(data, count) {
  const poses = new Array(count);
  for (let p = 0; p < count; p++) {
    const lm = new Array(33);
    for (let i = 0; i < 33; i++) {
      const o = p * STRIDE + i * 4;
      lm[i] = { x: data[o], y: data[o + 1], z: data[o + 2], visibility: data[o + 3] };
    }
    poses[p] = lm;
  }
  return poses;
}

/**
 * Worker when the browser supports it, else main thread.
 * @param o.worker  'auto' (default) | true | false
 */
export async function createPoseDetector(o = {}) {
  const want = o.worker ?? 'auto';
  if (want !== false && workerSupported()) {
    try {
      return await WorkerPoseDetector.create(o);
    } catch (err) {
      if (want === true) throw err;
      console.warn('[dronevision] worker unavailable, running the model on the main thread', err);
    }
  }
  return PoseDetector.create(o);
}
