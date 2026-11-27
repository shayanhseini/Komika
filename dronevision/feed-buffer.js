// Layout of the float buffer Unity polls every frame (page → C#).
// Pure (no DOM) so it can be tested in Node. The C# twin is
// Runtime/Core/FeedBuffer.cs; tests/make-golden.js writes sample buffers that
// the C# test suite decodes.

export const SLOT = Object.freeze({
  VERSION: 0, STATUS: 1, SEQ: 2, T_HI: 3, T_LO: 4, ASPECT: 5, POSES: 6,
  FPS: 7, INFER_MS: 8, LATENCY: 9, PAUSED: 10, GAIN: 11, LUM: 12, FOCUS: 13,
  POSE_DATA: 16, MAX_POSES: 3, STRIDE: 33 * 4,
  SIG_DATA: 16 + 3 * 33 * 4, SIG_STRIDE: 4,   // r, g, b, valid per pose
});
export const BUFFER_LENGTH = SLOT.SIG_DATA + SLOT.MAX_POSES * SLOT.SIG_STRIDE; // 424
export const BUFFER_VERSION = 3;

export const STATUS_CODE = Object.freeze({
  idle: 0, loading: 1, running: 2,
  'permission-denied': 5, 'no-camera': 6, 'camera-busy': 7, insecure: 8, unsupported: 9, error: 10,
  'model-failed': 11,
});

/**
 * @param buf   Float32Array(BUFFER_LENGTH)
 * @param f     { status, seq, t (ms), aspect, poses: [[{x,y,z,visibility}×33]], sigs: [[r,g,b]|null],
 *                fps, inferMs, latencyMs, paused, gain, lum, focus }
 */
export function packFeed(buf, f) {
  buf[SLOT.VERSION] = BUFFER_VERSION;
  buf[SLOT.STATUS] = STATUS_CODE[f.status] ?? STATUS_CODE.error;
  buf[SLOT.SEQ] = f.seq;
  // split the timestamp so float32 keeps sub-millisecond precision for hours
  const hi = Math.floor(f.t / 65536);
  buf[SLOT.T_HI] = hi;
  buf[SLOT.T_LO] = f.t - hi * 65536;
  buf[SLOT.ASPECT] = f.aspect;
  const n = Math.min(f.poses.length, SLOT.MAX_POSES);
  buf[SLOT.POSES] = n;
  buf[SLOT.FPS] = f.fps || 0;
  buf[SLOT.INFER_MS] = f.inferMs || 0;
  buf[SLOT.LATENCY] = f.latencyMs || 0;
  buf[SLOT.PAUSED] = f.paused ? 1 : 0;
  buf[SLOT.GAIN] = f.gain || 1;
  buf[SLOT.LUM] = f.lum || 0;
  buf[SLOT.FOCUS] = f.focus ? 1 : 0;
  for (let p = 0; p < n; p++) {
    const lm = f.poses[p];
    const base = SLOT.POSE_DATA + p * SLOT.STRIDE;
    for (let i = 0; i < 33; i++) {
      const q = lm[i];
      const o = base + i * 4;
      buf[o] = q.x; buf[o + 1] = q.y; buf[o + 2] = q.z || 0; buf[o + 3] = q.visibility ?? 1;
    }
    const sig = f.sigs && f.sigs[p];
    const so = SLOT.SIG_DATA + p * SLOT.SIG_STRIDE;
    buf[so] = sig ? sig[0] : 0; buf[so + 1] = sig ? sig[1] : 0; buf[so + 2] = sig ? sig[2] : 0; buf[so + 3] = sig ? 1 : 0;
  }
  return buf;
}
