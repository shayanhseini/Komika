// Bridge between the camera/model (VisionFeed) and a Unity WebGL build — drone version.
//
// This page-side code contains NO game logic. It only:
//   1. runs the camera + MediaPipe model and writes the raw landmarks of up to
//      3 people into one float buffer that C# polls every frame
//      (DroneVisionInput.cs → Plugins/WebGL/DroneVision.jslib);
//   2. draws what C# tells it to: the player frame (white → gold → green on
//      lock), the wings, the camera preview, short Persian hints, and the
//      connection box (status + the four keys lit while pressed).
// The wings gesture, the lock, the keys and crowd handling all run in the C# core
// inside Unity (compiled to WebAssembly) — the same logic as the PC app.
//
// Camera choice: the browser remembers the last camera. Ctrl+Shift+C switches to
// the next camera (shown for 3 s); ?camera=2 in the URL picks camera #2 at start.
// All overlay elements use pointer-events:none: they never block Unity's UI.

import { VisionFeed } from './vision-feed.js';
import { drawOverlay, bodyBox, drawBodyFrame } from './overlay.js';
import { BUFFER_LENGTH, packFeed } from './feed-buffer.js';

const PHASE = ['searching', 'detected', 'locked'];
// codes = DroneVision.Core.DroneHint
const HINT_TEXT = {
  1: 'کمی عقب‌تر بایستید تا شانه‌هایتان کامل دیده شود',
  2: 'روبه‌روی دوربین صاف بایستید',
  3: 'کمی نزدیک‌تر بیایید',
  4: 'در وسط تصویر بایستید',
  5: 'روبه‌روی دوربین صاف بایستید',
  6: 'با خم شدن ملایم به چپ یا راست مسیر را عوض کنید',
};
const DARK_TEXT = 'نور محیط کم است؛ جای روشن‌تری بایستید یا چراغ را روشن کنید';
const PHASE_TEXT = {
  searching: 'روبه‌روی دوربین صاف بایستید',
  detected: 'کمی صبر کنید، صاف بایستید…',
  locked: 'آماده! برای حرکت درجا بزنید و با خم شدن چپ یا راست مسیر را عوض کنید',
};
const ERROR_TEXT = {
  loading: 'در حال آماده‌سازی دوربین…',
  'permission-denied': 'برای کنترل با دوربین، دسترسی دوربین را مجاز کنید (یا با صفحه‌کلید بازی کنید)',
  'no-camera': 'دوربینی پیدا نشد؛ بازی با صفحه‌کلید فعال است',
  'camera-busy': 'دوربین در اختیار برنامه‌ی دیگری است',
  insecure: 'دوربین فقط روی HTTPS کار می‌کند',
  unsupported: 'این مرورگر از دوربین پشتیبانی نمی‌کند',
  error: 'خطا در راه‌اندازی دوربین',
  'model-failed': 'مدل تشخیص بارگذاری نشد؛ اینترنت یا سرور را بررسی کنید و صفحه را دوباره باز کنید',
};
const TERMINAL_ERRORS = new Set(['permission-denied', 'no-camera', 'camera-busy', 'insecure', 'unsupported', 'error', 'model-failed']);

const buf = new Float32Array(BUFFER_LENGTH);
let feed = null;
let view = null;
// what C# decided on the last frame (drives the drawing only)
const ui = { op: -1, phase: 'searching', progress: 0, hint: 0, side: 0, vert: 0, held: false, keys: 0, runPct: 0, lockedAt: 0 };

function createView(cfg) {
  const style = document.createElement('style');
  style.textContent = `
  #dv-root{position:fixed;inset:0;z-index:10;pointer-events:none;font-family:ISans,Vazirmatn,Tahoma,sans-serif;direction:rtl}
  #dv-root *{pointer-events:none;box-sizing:border-box}
  #dv-cam{position:absolute;overflow:hidden;background:#000;transition:all .35s ease}
  #dv-cam video,#dv-cam canvas{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transform:scaleX(-1)}
  #dv-root.lobby{background:rgba(5,10,20,.55)}
  #dv-root.lobby #dv-cam{left:50%;top:46%;transform:translate(-50%,-50%);
    width:min(92vw,calc(72vh*var(--dv-ar,1.778)));aspect-ratio:var(--dv-ar,1.778);max-height:72vh;
    border-radius:18px;box-shadow:0 10px 40px rgba(0,0,0,.55);border:3px solid rgba(255,255,255,.25)}
  #dv-root.corner #dv-cam{width:min(17vw,178px);aspect-ratio:var(--dv-ar,1.778);border-radius:12px;border:2px solid rgba(255,255,255,.5)}
  #dv-root.corner.dv-bottom-left #dv-cam{left:12px;bottom:52px}
  #dv-root.corner.dv-bottom-right #dv-cam{right:12px;bottom:52px}
  #dv-root.corner.dv-top-left #dv-cam{left:12px;top:12px}
  #dv-root.corner.dv-top-right #dv-cam{right:12px;top:12px}
  #dv-root.locked #dv-cam{border-color:#34d399;box-shadow:0 0 28px rgba(52,211,153,.55)}
  #dv-root.hidden{display:none}
  #dv-msg{position:absolute;left:50%;bottom:max(4vh,16px);transform:translateX(-50%);max-width:92vw;
    padding:10px 22px;border-radius:999px;background:rgba(10,18,36,.85);color:#fff;font-size:clamp(15px,2.6vw,22px);
    font-weight:700;text-align:center;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;transition:background .25s}
  #dv-root.locked #dv-msg{background:#10b981}
  #dv-root.corner #dv-msg,#dv-root.nolabels #dv-msg{display:none}
  #dv-box{position:absolute;display:none;align-items:center;gap:8px;padding:6px 10px;border-radius:999px;
    background:rgba(10,18,36,.86);color:#fff;font-size:13px;font-weight:700;white-space:nowrap;direction:rtl}
  #dv-root.corner.box #dv-box{display:flex}
  #dv-root.dv-bottom-left #dv-box{left:12px;bottom:12px}
  #dv-root.dv-bottom-right #dv-box{right:12px;bottom:12px}
  #dv-root.dv-top-left #dv-box{left:12px;top:calc(12px + min(17vw,178px) / var(--dv-ar,1.778) + 8px)}
  #dv-root.dv-top-right #dv-box{right:12px;top:calc(12px + min(17vw,178px) / var(--dv-ar,1.778) + 8px)}
  #dv-dot{width:12px;height:12px;border-radius:50%;background:#8a94a6;flex:none}
  #dv-box.on #dv-dot{background:#34d399;box-shadow:0 0 8px #34d399}
  #dv-box.wait #dv-dot{background:#f2c14e}
  #dv-keys{display:flex;gap:3px;direction:ltr}
  #dv-keys span{min-width:22px;height:22px;padding:0 4px;border-radius:6px;background:rgba(255,255,255,.12);
    color:rgba(255,255,255,.55);font:700 12px/22px Tahoma,sans-serif;text-align:center}
  #dv-keys span.on{background:#34d399;color:#06281c}
  #dv-meter{width:44px;height:7px;border-radius:999px;background:rgba(255,255,255,.16);overflow:hidden;direction:ltr}
  #dv-meter > i{display:block;height:100%;width:0%;background:#34d399;transition:width .08s linear,background .15s}
  #dv-meter.fast > i{background:#23e6ff}
  #dv-toast{position:absolute;left:50%;top:16px;transform:translateX(-50%);padding:8px 18px;border-radius:999px;
    background:rgba(10,18,36,.9);color:#fff;font-size:16px;font-weight:700;display:none}`;
  document.head.appendChild(style);
  const root = document.createElement('div');
  root.id = 'dv-root';
  root.innerHTML = '<div id="dv-cam"><video muted playsinline></video><canvas></canvas></div><div id="dv-msg"></div>'
    + '<div id="dv-box"><span id="dv-dot"></span><span id="dv-state"></span><span id="dv-meter"><i></i></span><span id="dv-keys"></span></div><div id="dv-toast"></div>';
  document.body.appendChild(root);
  const labels = String(cfg.keyLabels || 'D,A,W,S').split(',');
  const v = { root, cam: root.querySelector('#dv-cam'), video: root.querySelector('video'),
    canvas: root.querySelector('canvas'), msg: root.querySelector('#dv-msg'), box: null,
    statusBox: root.querySelector('#dv-box'), state: root.querySelector('#dv-state'),
    meterEl: root.querySelector('#dv-meter'), meterFill: root.querySelector('#dv-meter > i'), keysEl: root.querySelector('#dv-keys'),
    toast: root.querySelector('#dv-toast'), toastTimer: 0,
    mode: 'lobby', corner: cfg.previewCorner || 'bottom-left', labels: cfg.previewLabels !== false,
    showBox: cfg.statusBox !== false, lastText: '', lastBox: '', lastPoseSeq: -1, lastRenderKey: '', errorAt: 0 };
  // left-to-right on screen: ← left · → right (bits 2, 1; jump/slide remain keyboard/swipe)
  v.keySpans = [[2, labels[1]], [1, labels[0]]].map(([bit, text]) => {
    const s = document.createElement('span');
    s.textContent = text || '?';
    s.dataset.bit = bit;
    v.keysEl.appendChild(s);
    return s;
  });
  v.ctx = v.canvas.getContext('2d');
  setMode(v, cfg.previewMode || 'lobby');
  requestAnimationFrame(t => draw(v, t));
  return v;
}

function setMode(v, mode) {
  v.mode = mode === 'corner' || mode === 'hidden' ? mode : 'lobby';
  paintClasses(v);
}

function paintClasses(v) {
  const c = ['dv-' + v.corner, v.mode];
  if (ui.phase === 'locked') c.push('locked');
  if (!v.labels) c.push('nolabels');
  if (v.showBox) c.push('box');
  v.root.className = c.join(' ');
}

function paintBox(v) {
  const state = ui.phase === 'locked' ? 'on' : ui.phase === 'detected' ? 'wait' : 'off';
  const motionText = ui.runPct >= 68 ? 'تند دویدن ✓' : ui.runPct >= 10 ? 'در حال دویدن ✓' : 'متصل · درجا بزنید';
  const text = ui.phase === 'locked' ? motionText
    : ui.phase === 'detected' ? 'در حال شناسایی…' : (ERROR_TEXT[feed && feed.status] ? 'دوربین قطع' : 'منتظر بازیکن');
  const pct = ui.phase === 'locked' ? Math.max(0, Math.min(100, ui.runPct | 0)) : 0;
  const key = state + text + ui.keys + ':' + (pct >> 2);
  if (key === v.lastBox) return;
  v.lastBox = key;
  v.statusBox.className = state;
  v.state.textContent = text;
  if (v.meterFill) {
    v.meterFill.style.width = pct + '%';
    v.meterEl.className = pct >= 68 ? 'fast' : '';
  }
  for (const s of v.keySpans) s.className = ui.keys & Number(s.dataset.bit) ? 'on' : '';
}

function toast(text) {
  if (!view) return;
  view.toast.textContent = text;
  view.toast.style.display = 'block';
  clearTimeout(view.toastTimer);
  view.toastTimer = setTimeout(() => { view.toast.style.display = 'none'; }, 3000);
}

function draw(v, now) {
  requestAnimationFrame(t => draw(v, t));
  if (v.mode === 'hidden' || !feed) return;
  const vid = feed.video;
  if (vid.videoWidth) {
    const scale = v.mode === 'corner' ? Math.min(1, 320 / vid.videoWidth) : 1;
    const cw = Math.max(2, Math.round(vid.videoWidth * scale));
    const ch = Math.max(2, Math.round(vid.videoHeight * scale));
    if (v.canvas.width !== cw || v.canvas.height !== ch) {
      v.canvas.width = cw;
      v.canvas.height = ch;
      v.root.style.setProperty('--dv-ar', String(vid.videoWidth / vid.videoHeight));
      v.lastRenderKey = '';
    }
  }
  const player = ui.op >= 0 ? feed.poses[ui.op] : null;
  if (feed.frameSeq !== v.lastPoseSeq) {
    v.lastPoseSeq = feed.frameSeq;
    v.box = bodyBox(player, v.box);
    if (!player) v.box = null;
  }
  const pulse = v.mode === 'lobby' && ui.lockedAt ? Math.max(0, 1 - (now - ui.lockedAt) / 900) : 0;
  const renderKey = `${feed.frameSeq}:${ui.phase}:${ui.side}:${ui.held ? 1 : 0}:${(ui.progress * 20) | 0}:${v.mode}:${ui.op}`;
  if (renderKey !== v.lastRenderKey || pulse > 0) {
    v.lastRenderKey = renderKey;
    const ctx = v.ctx;
    ctx.clearRect(0, 0, v.canvas.width, v.canvas.height);
    if (v.mode === 'lobby') {
      drawOverlay(ctx, player, { side: ui.side, vert: ui.vert, held: ui.held }, { clear: false });
      drawBodyFrame(ctx, v.box, ui.phase, ui.progress, pulse);
    } else {
      drawOverlay(ctx, player, { side: ui.side, vert: ui.vert, held: ui.held }, { clear: false, light: true });
      if (v.box) drawBodyFrame(ctx, v.box, ui.phase, ui.progress, 0);
    }
  }
  if (v.showBox) paintBox(v);
  // If camera cannot start (denied or missing), don't leave the center menu dim forever
  if (TERMINAL_ERRORS.has(feed.status)) {
    if (!v.errorAt) v.errorAt = now;
    else if (v.mode === 'lobby' && now - v.errorAt > 3000) setMode(v, 'corner');
  } else {
    v.errorAt = 0;
  }
  // message line (lobby only)
  let text = ERROR_TEXT[feed.status] || '';
  if (!text && ui.phase === 'searching' && feed.stats.lum > 0 && feed.stats.lum < 0.12) text = DARK_TEXT;
  if (!text) text = (ui.phase !== 'locked' && HINT_TEXT[ui.hint]) || PHASE_TEXT[ui.phase];
  if (text !== v.lastText) { v.msg.textContent = text; v.lastText = text; }
}

// Ctrl+Shift+C: next camera (the browser remembers it for the next visit)
async function nextCamera() {
  if (!feed) return;
  const cams = await feed.refreshCameras();
  if (!cams.length) { toast('دوربینی پیدا نشد'); return; }
  const i = cams.findIndex(c => c.id === feed.cameraId);
  const next = cams[(i + 1) % cams.length];
  toast('دوربین ' + ((i + 1) % cams.length + 1) + ' از ' + cams.length + ': ' + next.label);
  await feed.useCamera(next.id);
}

window.addEventListener('keydown', e => {
  if (e.ctrlKey && e.shiftKey && (e.code === 'KeyC')) { e.preventDefault(); nextCamera(); }
}, true);

async function cameraFromUrl() {
  const n = parseInt(new URLSearchParams(location.search).get('camera') || '', 10);
  if (!(n >= 1)) return null;
  // labels and ids need the permission first: open any camera, then pick #n
  return n;
}

const api = {
  /** config JSON: { model, numPoses, maxFps, width, height, focusCrop, previewMode, previewCorner, previewLabels, statusBox, keyLabels } */
  async start(json) {
    let cfg = {};
    try { cfg = json ? JSON.parse(json) : {}; } catch (e) { console.warn('[dronevision] bad config json', e); }
    if (feed) { feed.start(); return; }
    view = createView(cfg);
    feed = new VisionFeed({
      model: cfg.model || 'lite', numPoses: cfg.numPoses || 1, maxFps: cfg.maxFps || 24, video: view.video,
      width: cfg.width || 640, height: cfg.height || 360,
      focus: cfg.focusCrop === true,   // off by default: the web model jumps off the hands on a crop (measured)
    });
    window.DroneVisionFeed = feed; // for debugging from the console
    const ok = await feed.start();
    const n = await cameraFromUrl();
    if (ok && n) {
      const cams = await feed.refreshCameras();
      const cam = cams[n - 1];
      if (cam && cam.id !== feed.cameraId) await feed.useCamera(cam.id);
    }
  },

  /** Fills and returns the shared buffer (raw landmarks of up to 3 people). */
  read() {
    if (!feed) return packFeed(buf, { status: 'idle', seq: 0, t: 0, aspect: 16 / 9, poses: [] });
    const st = feed.stats;
    return packFeed(buf, {
      status: feed.status, seq: feed.frameSeq, t: feed.frameT, aspect: feed.aspect, poses: feed.poses, sigs: feed.sigs,
      fps: st.fps, inferMs: st.inferMs, latencyMs: st.latencyMs, paused: feed.paused,
      gain: st.gain, lum: st.lum, focus: st.focus,
    });
  },

  /** From C#, once per processed frame: who the player is and what the core decided. */
  setUi(op, phase, progress, hint, side, vert, held, keys, runPct = 0) {
    const ph = PHASE[phase] || 'searching';
    if (ph === 'locked' && ui.phase !== 'locked') ui.lockedAt = performance.now();
    const changed = ph !== ui.phase;
    ui.op = op; ui.phase = ph; ui.progress = progress; ui.hint = hint;
    ui.side = side; ui.vert = vert; ui.held = !!held; ui.keys = keys | 0; ui.runPct = runPct | 0;
    if (changed && view) paintClasses(view);
    // crop around the player only once LOCKED (only if focusCrop is on)
    if (feed) feed.setFocus(op >= 0 && phase === 2 ? feed.poses[op] : null);
  },

  setPreviewMode(mode) { if (view) setMode(view, mode); },
  setPaused(p) { if (feed) feed.setPaused(p); },
  camerasJson() {
    return JSON.stringify({ current: feed ? feed.cameraId : null, cameras: feed ? feed.cameras : [] });
  },
  useCamera(id) { if (feed) feed.useCamera(id || null); },
  nextCamera,
  setModel(model) { if (feed) feed.setModel(model); },
  stop() { if (feed) feed.stop(); },
  retry() { if (feed) feed.start(); },
};

window.DroneVisionUnity = api;
// Unity may have called Start before this module finished loading
if (window.__droneVisionPending !== undefined) {
  api.start(window.__droneVisionPending);
  delete window.__droneVisionPending;
}
