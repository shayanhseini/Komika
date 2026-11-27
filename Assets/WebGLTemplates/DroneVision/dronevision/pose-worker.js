// Module worker: runs MediaPipe PoseLandmarker off the main thread, so the
// game's frame loop (Unity WebGL runs on the main thread) is never blocked by
// inference. Frames arrive as transferred ImageBitmaps; landmarks go back as
// one transferred Float32Array per frame.
//
// Messages in:  init {libs, models, delegate: 'auto'|'GPU'|'CPU', numPoses}
//               frame {id, bitmap, ts, roi, exposure} · model {models} · numPoses {n}
//                 roi: the region of the full frame the bitmap shows ({x,y,w,h} 0..1), or null
// Messages out: ready/model {model, delegate, source}
//               result {id, data, sigs, count, inferMs, gain, lum}  (landmarks in FULL-frame coords)
//               error {id?, message, fatal}

import { FrameCues, sigLum } from './image-cues.js';

const STRIDE = 33 * 4; // x, y, z, visibility per landmark
let cues = null;

let mp = null;
let fileset = null;
let landmarker = null;
let delegate = 'GPU';
let numPoses = 1;
let lastTs = -1;
let gpuErrors = 0;
let lastBuild = null;

// delegate 'auto': which is faster is a property of the DEVICE, so measure it.
// Measured on the dev PC with the page rendering (like a game): GPU in this worker
// 9 FPS (112 ms/frame, waits in steps of 33 ms), CPU in this worker 20.7 FPS (46 ms).
// Phones may be the other way round. GPU runs first; if it is not clearly fast,
// CPU is measured too and the faster one is kept (once, in the lobby, ~2 s).
// GPU in a worker starts fast and slows down after ~16 frames (measured), so the
// window is long enough to see the sustained speed.
const PROBE_SKIP = 10, PROBE_FRAMES = 30, GPU_FAST_MS = 25;
let probe = null;   // { stage: 'GPU' | 'CPU', times: [], gpuMs }

function median(v) {
  const s = v.slice().sort((a, b) => a - b);
  return s[s.length >> 1];
}

let swapping = false;

/** Called after each timed inference while probing; may swap the delegate. */
async function probeStep(ms) {
  if (swapping) return;   // frames still running on the old delegate don't count
  probe.times.push(ms);
  if (probe.times.length < PROBE_SKIP + PROBE_FRAMES) return;
  const m = median(probe.times.slice(PROBE_SKIP));
  if (probe.stage === 'GPU') {
    if (m <= GPU_FAST_MS || !lastBuild) { probe = null; return; }   // GPU is fine: keep it
    swapping = true;
    let info;
    try { info = await build(lastBuild, 'CPU'); } finally { swapping = false; }
    probe = { stage: 'CPU', times: [], gpuMs: m };
    self.postMessage({ type: 'model', ...info, probe: `GPU ${m.toFixed(0)} ms → trying CPU` });
  } else {
    const gpuMs = probe.gpuMs;
    probe = null;
    if (m < gpuMs * 0.85) {
      self.postMessage({ type: 'model', model: lastModel, delegate: 'CPU', source: lastSource, probe: `kept CPU ${m.toFixed(0)} ms (GPU ${gpuMs.toFixed(0)} ms)` });
    } else {
      swapping = true;
      let info;
      try { info = await build(lastBuild, 'GPU'); } finally { swapping = false; }
      self.postMessage({ type: 'model', ...info, probe: `kept GPU ${gpuMs.toFixed(0)} ms (CPU ${m.toFixed(0)} ms)` });
    }
  }
}
let lastModel = '', lastSource = '';

/** libs: [{lib, wasm, source}] in preference order (local first). */
async function loadLibrary(libs) {
  let lastErr;
  for (const l of libs) {
    try {
      mp = await import(l.lib);
      // `true`: the ES-module flavour of the wasm loader (module workers have no importScripts)
      fileset = await mp.FilesetResolver.forVisionTasks(l.wasm, true);
      return l.source;
    } catch (err) {
      lastErr = err;
      mp = null;
    }
  }
  throw lastErr;
}

function options(url, del) {
  const o = {
    baseOptions: { modelAssetPath: url, delegate: del },
    runningMode: 'VIDEO',
    numPoses,
    minPoseDetectionConfidence: 0.5,
    minPosePresenceConfidence: 0.5,
    minTrackingConfidence: 0.5,
    outputSegmentationMasks: false,
  };
  // GPU inside a worker needs an OffscreenCanvas for its WebGL2 context
  if (del === 'GPU') o.canvas = new OffscreenCanvas(1, 1);
  return o;
}

/** models: [{url, source, model}] in preference order (local first). */
let builds = 0;

async function build(models, wantDelegate) {
  let lastErr;
  for (const m of models) {
    for (const del of wantDelegate === 'GPU' ? ['GPU', 'CPU'] : ['CPU']) {
      try {
        // A second landmarker in the same module worker fails with "ModuleFactory not set":
        // the wasm loader is an ES module, cached after the first import, so it never
        // re-registers its factory. A fresh URL makes it evaluate again (measured: the
        // GPU→CPU switch, the runtime GPU fallback and model changes all failed without it).
        const fs = builds++ === 0 ? fileset : { ...fileset, wasmLoaderPath: `${fileset.wasmLoaderPath}${fileset.wasmLoaderPath.includes('?') ? '&' : '?'}b=${builds}` };
        const next = await mp.PoseLandmarker.createFromOptions(fs, options(m.url, del));
        if (landmarker) landmarker.close();
        landmarker = next;
        delegate = del;
        lastTs = -1;
        gpuErrors = 0;
        lastBuild = models;
        lastModel = m.model;
        lastSource = m.source;
        warmUp();
        return { model: m.model, delegate: del, source: m.source };
      } catch (err) {
        lastErr = err;
      }
    }
  }
  throw lastErr;
}

// The first inference compiles the GPU shaders (~100-300 ms). Do it now, while
// loading, instead of on the player's first frames.
function warmUp() {
  try {
    const c = new OffscreenCanvas(256, 256);
    c.getContext('2d').fillRect(0, 0, 256, 256);
    landmarker.detectForVideo(c, ++lastTs);
  } catch { /* warm-up is best effort */ }
}

self.onmessage = async e => {
  const msg = e.data;
  try {
    if (msg.type === 'frame') {
      const t0 = performance.now();
      const ts = Math.max(Math.round(msg.ts), lastTs + 1); // strictly increasing, as MediaPipe requires
      lastTs = ts;
      if (!cues) cues = new FrameCues((w, h) => new OffscreenCanvas(w, h));
      let result;
      try {
        const bm = msg.bitmap;
        const input = cues.prepare(bm, 0, 0, bm.width, bm.height, msg.exposure !== false);
        bm.close();
        const d0 = performance.now();
        result = landmarker.detectForVideo(input, ts);
        msg.prepMs = d0 - t0;
        msg.detMs = performance.now() - d0;
        if (probe) msg.probeMs = msg.detMs;
        gpuErrors = 0;
      } finally {
        try { msg.bitmap.close(); } catch { /* already closed */ }
      }
      const poses = result.landmarks || [];
      const roi = msg.roi;
      const firstSig = poses.length ? cues.signature(poses[0]) : null;
      cues.feedback(poses.length, sigLum(firstSig));
      const data = new Float32Array(Math.max(1, poses.length * STRIDE));
      const sigs = new Float32Array(Math.max(1, poses.length * 4));
      for (let p = 0; p < poses.length; p++) {
        const lm = poses[p];
        const sig = cues.signature(lm); // crop coordinates, before mapping
        if (sig) { sigs[p * 4] = sig[0]; sigs[p * 4 + 1] = sig[1]; sigs[p * 4 + 2] = sig[2]; sigs[p * 4 + 3] = 1; }
        for (let i = 0; i < lm.length && i < 33; i++) {
          const o = p * STRIDE + i * 4;
          data[o] = roi ? roi.x + lm[i].x * roi.w : lm[i].x;
          data[o + 1] = roi ? roi.y + lm[i].y * roi.h : lm[i].y;
          data[o + 2] = lm[i].z;
          data[o + 3] = lm[i].visibility ?? 1;
        }
      }
      self.postMessage({
        type: 'result', id: msg.id, data, sigs, count: poses.length,
        inferMs: performance.now() - t0, prepMs: msg.prepMs, detMs: msg.detMs, gain: cues.gain, lum: cues.lum,
      }, [data.buffer, sigs.buffer]);
      // the next frame waits for a delegate swap (once, during the probe)
      if (probe && poses.length && msg.probeMs !== undefined) await probeStep(msg.probeMs);
    } else if (msg.type === 'init') {
      numPoses = msg.numPoses || 1;
      const libSource = await loadLibrary(msg.libs);
      const want = msg.delegate || 'auto';
      const info = await build(msg.models, want === 'CPU' ? 'CPU' : 'GPU');
      if (want === 'auto' && info.delegate === 'GPU') probe = { stage: 'GPU', times: [] };
      self.postMessage({ type: 'ready', ...info, libSource });
    } else if (msg.type === 'model') {
      const info = await build(msg.models, delegate === 'CPU' ? 'CPU' : 'GPU');
      self.postMessage({ type: 'model', ...info });
    } else if (msg.type === 'numPoses') {
      numPoses = msg.n;
      await landmarker.setOptions({ numPoses });
      self.postMessage({ type: 'ack', id: msg.id });
    }
  } catch (err) {
    // some mobile GPUs fail at run time rather than at creation: fall back to CPU
    if (msg.type === 'frame' && delegate === 'GPU' && ++gpuErrors >= 3 && lastBuild) {
      try {
        const info = await build(lastBuild, 'CPU');
        self.postMessage({ type: 'model', ...info });
      } catch { /* reported below */ }
    }
    self.postMessage({ type: 'error', id: msg.id, message: String(err && err.message || err), fatal: msg.type === 'init' });
  }
};
