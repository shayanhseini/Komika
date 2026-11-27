// Cheap per-frame image cues, computed next to the pose model (worker or main
// thread):
//
//   · crop    — the model sees only the region around the locked player
//               (others cut away, player bigger in the model input)
//   · light   — adaptive SHADOW LIFT (gamma curve), chosen by measurement:
//               a plain brightness boost destroyed detection in normal light,
//               while gamma 0.5 left normal scenes intact and turned a fully
//               back-lit player (window behind them) from 0/6 to 6/6 detections.
//               When nobody is found, both curves are tried on alternate frames
//               and the one that finds the person is kept ("probing").
//   · colour  — a clothing-colour signature per detected person (mean torso
//               colour, from the UNCORRECTED frame so it stays stable), used by
//               the C# core to keep the same player in a crowd
//
// Dark AND noisy scenes cannot be fixed in software (the curve amplifies the
// noise too): the page shows "not enough light" and asks the camera itself for
// more exposure where the browser allows it (vision-feed.js).

const TW = 64, TH = 48;          // tiny analysis image
const MAX_SIDE = 512;            // model input never needs more
const LIFT_GAMMA = 0.5;
const PROBE_AFTER = 6;           // frames without anyone before trying the other curve
const RELAX_FRAMES = 45;         // well-lit player this long -> drop the lift again
// Reading pixels back from the GPU (getImageData) stalls the pipeline: measured on
// this PC, 17 ms for the 64×48 thumbnail and ~20 ms for the lift, against 12 ms for
// the Lite model itself; in the Worker it added up to 133 ms/frame (8 FPS, hands lost
// when churning fast). Light and clothing colour change slowly, so the thumbnail is
// read only every ANALYSE_EVERY frames, and the model input is drawn on a GPU canvas
// (no readback) unless the shadow lift is actually on.
// Exception: while there is a crowd (2+ people seen in the last CROWD_HOLD frames) it is
// read EVERY frame — the clothing colour is what stops a person passing behind the
// player from taking over (crowd scenario 'crossing' failed in both games with colour
// on every 6th frame only, and passes again with this rule). A lone player keeps the speed.
const ANALYSE_EVERY = 6;
const CROWD_HOLD = 30;

export class FrameCues {
  /** @param makeCanvas (w, h) => OffscreenCanvas | HTMLCanvasElement */
  constructor(makeCanvas) {
    this.tiny = makeCanvas(TW, TH);
    this.tctx = this.tiny.getContext('2d', { willReadFrequently: true });
    this.work = makeCanvas(2, 2);    // CPU-backed: only for the lift (pixel access)
    this.wctx = this.work.getContext('2d', { willReadFrequently: true });
    this.gpu = makeCanvas(2, 2);     // GPU-backed: the normal model input, no readback
    this.gctx = this.gpu.getContext('2d');
    this.px = null;
    this.frame = 0;
    this.crowdUntil = -1;  // read the thumbnail every frame up to this frame (crowd)
    this.fresh = false;    // thumbnail read on THIS frame (signatures are valid)
    this.subjLum = -1;
    this.lum = 0;          // brightness of the central band, 0..1
    this.lift = false;     // gamma lift applied to the frame being analysed
    this.keepLift = false; // current choice
    this.miss = 0;
    this.bright = 0;
    this.lut = new Uint8ClampedArray(256);
    for (let i = 0; i < 256; i++) this.lut[i] = Math.round(Math.pow(i / 255, LIFT_GAMMA) * 255);
  }

  /** 1 = no correction, 2 = shadows lifted (reported to the game as "gain"). */
  get gain() { return this.lift ? 1 / LIFT_GAMMA : 1; }

  /**
   * Draw the source region into the model input, lifting shadows when needed.
   * @param src  ImageBitmap | HTMLVideoElement | canvas
   * @param sx,sy,sw,sh  source rectangle in pixels (the crop, or the whole frame)
   * @param adaptive  true = adaptive shadow lift on
   */
  prepare(src, sx, sy, sw, sh, adaptive) {
    this.fresh = this.frame % ANALYSE_EVERY === 0 || this.frame <= this.crowdUntil || !this.px;
    this.frame++;
    if (this.fresh) {
      this.tctx.drawImage(src, sx, sy, sw, sh, 0, 0, TW, TH);
      this.px = this.tctx.getImageData(0, 0, TW, TH).data;
      let sum = 0, n = 0;
      for (let y = 4; y < TH - 4; y++) {
        for (let x = Math.floor(TW * 0.2); x < Math.ceil(TW * 0.8); x++) {
          const o = (y * TW + x) * 4;
          sum += 0.299 * this.px[o] + 0.587 * this.px[o + 1] + 0.114 * this.px[o + 2];
          n++;
        }
      }
      this.lum = sum / n / 255;
    }

    // nobody found for a while: alternate the curve until someone shows up
    this.lift = adaptive && (this.miss >= PROBE_AFTER ? (this.miss % 2 === 0) !== this.keepLift : this.keepLift);

    const scale = Math.min(1, MAX_SIDE / Math.max(sw, sh));
    const w = Math.max(2, Math.round(sw * scale)), h = Math.max(2, Math.round(sh * scale));
    if (!this.lift) {
      if (this.gpu.width !== w || this.gpu.height !== h) { this.gpu.width = w; this.gpu.height = h; }
      this.gctx.drawImage(src, sx, sy, sw, sh, 0, 0, w, h);
      return this.gpu;
    }
    if (this.work.width !== w || this.work.height !== h) { this.work.width = w; this.work.height = h; }
    this.wctx.drawImage(src, sx, sy, sw, sh, 0, 0, w, h);
    const img = this.wctx.getImageData(0, 0, w, h);
    const d = img.data, lut = this.lut;
    for (let i = 0; i < d.length; i += 4) {
      d[i] = lut[d[i]]; d[i + 1] = lut[d[i + 1]]; d[i + 2] = lut[d[i + 2]];
    }
    this.wctx.putImageData(img, 0, 0);
    return this.work;
  }

  /**
   * Report what the model found on the prepared frame, so the curve that works is kept.
   * @param found  number of poses detected
   * @param subjectLum  brightness of the main person's torso 0..1 (or -1 if unknown)
   */
  feedback(found, subjectLum) {
    if (found >= 2) this.crowdUntil = this.frame + CROWD_HOLD;
    if (found > 0) {
      this.miss = 0;
      // evidence only: keep the curve that just found the person. (Dark CLOTHES are not
      // dark LIGHT, so the player's colour never switches the lift on by itself.)
      this.keepLift = this.lift;
      // (the torso brightness is only measured on thumbnail frames: keep the last one)
      if (subjectLum >= 0) this.subjLum = subjectLum;
      // lifted but the player is well lit for a while: try without it again
      if (this.keepLift && this.subjLum > 0.4 && ++this.bright > RELAX_FRAMES) { this.keepLift = false; this.bright = 0; }
    } else {
      this.miss++;
    }
  }

  /** Mean torso colour [r,g,b] 0..1 of one pose (landmarks relative to the prepared region), or null. */
  signature(lm) {
    if (!this.fresh) return null; // thumbnail is from an earlier frame: the person may have moved
    const px = this.px;
    const P = i => lm[i] && (lm[i].visibility ?? 1) >= 0.5 ? lm[i] : null;
    const ls = P(11), rs = P(12);
    if (!px || !ls || !rs) return null;
    let lh = P(23), rh = P(24);
    if (!lh || !rh) {
      // hips out of frame: a box one shoulder-width tall below the shoulders
      const w = Math.sqrt((ls.x - rs.x) ** 2 + (ls.y - rs.y) ** 2);
      lh = { x: ls.x, y: ls.y + w }; rh = { x: rs.x, y: rs.y + w };
    }
    let r = 0, g = 0, b = 0, n = 0;
    for (const u of [0.3, 0.5, 0.7]) {
      const tx = ls.x + (rs.x - ls.x) * u, ty = ls.y + (rs.y - ls.y) * u;
      const bx = lh.x + (rh.x - lh.x) * u, by = lh.y + (rh.y - lh.y) * u;
      for (const v of [0.25, 0.5, 0.75]) {
        const x = Math.round((tx + (bx - tx) * v) * (TW - 1)), y = Math.round((ty + (by - ty) * v) * (TH - 1));
        if (x < 0 || y < 0 || x >= TW || y >= TH) continue;
        const o = (y * TW + x) * 4;
        r += px[o]; g += px[o + 1]; b += px[o + 2]; n++;
      }
    }
    return n >= 5 ? [r / n / 255, g / n / 255, b / n / 255] : null;
  }
}

/** Brightness 0..1 of a colour signature. */
export function sigLum(sig) {
  return sig ? 0.299 * sig[0] + 0.587 * sig[1] + 0.114 * sig[2] : -1;
}
