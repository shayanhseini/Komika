// Camera-preview drawing: upright body posture, shoulder axis, torso lean indicator,
// and the player frame that turns green on lock. Draws in RAW image coordinates;
// mirror the canvas with CSS (transform: scaleX(-1)) together with the <video>.

import { SKELETON, NOSE, L_SHOULDER, R_SHOULDER, L_HIP, R_HIP, L_WRIST, R_WRIST, COUNT } from './core/landmarks.js';

const SIDE_COLORS = { '-1': '#3aa8ff', '0': '#34d399', '1': '#ff6b6b' };
export const FRAME_COLORS = { searching: 'rgba(255,255,255,0.85)', detected: '#f2c14e', locked: '#34d399' };

/**
 * @param state { held, side, vert } — from the C# vision core: active lock, lean zone -1/0/+1
 */
export function drawOverlay(ctx, landmarks, state, { minVisibility = 0.5, handVisibility = 0.35, clear = true, ghost = false, light = false } = {}) {
  const { width: w, height: h } = ctx.canvas;
  if (clear) ctx.clearRect(0, 0, w, h);
  if (!landmarks) return;
  if (ghost) {
    ctx.save();
    ctx.lineWidth = Math.max(2, Math.min(w, h) * 0.006);
    ctx.strokeStyle = 'rgba(200,200,200,0.6)';
    ctx.setLineDash([6, 6]);
    for (const [a, b] of SKELETON) {
      const pa = landmarks[a], pb = landmarks[b];
      if (!pa || !pb || (pa.visibility ?? 1) < minVisibility || (pb.visibility ?? 1) < minVisibility) continue;
      ctx.beginPath(); ctx.moveTo(pa.x * w, pa.y * h); ctx.lineTo(pb.x * w, pb.y * h); ctx.stroke();
    }
    ctx.restore();
    return;
  }
  const P = (i, min = minVisibility) => landmarks[i] && (landmarks[i].visibility ?? 1) >= min ? landmarks[i] : null;
  const scale = Math.min(w, h);

  ctx.lineCap = 'round';
  ctx.lineWidth = Math.max(2, scale * 0.008);
  ctx.strokeStyle = 'rgba(255,255,255,0.45)';
  for (const [a, b] of light ? [] : SKELETON) {
    const pa = P(a, a === L_WRIST || a === R_WRIST ? handVisibility : minVisibility);
    const pb = P(b, b === L_WRIST || b === R_WRIST ? handVisibility : minVisibility);
    if (!pa || !pb) continue;
    ctx.beginPath();
    ctx.moveTo(pa.x * w, pa.y * h);
    ctx.lineTo(pb.x * w, pb.y * h);
    ctx.stroke();
  }

  // What Kamica reads: upright shoulder axis + torso/head lean vector
  const activeColor = state.held ? (SIDE_COLORS[String(state.side)] || '#34d399') : '#f2c14e';
  const ls = P(L_SHOULDER), rs = P(R_SHOULDER);
  const lh = P(L_HIP, 0.45), rh = P(R_HIP, 0.45);
  const nose = P(NOSE, 0.35);

  if (ls && rs) {
    const sx = (ls.x + rs.x) * 0.5, sy = (ls.y + rs.y) * 0.5;
    if (!light && lh && rh) {
      const hx = (lh.x + rh.x) * 0.5, hy = (lh.y + rh.y) * 0.5;
      // Vertical upright reference plumb line from hip midpoint
      ctx.save();
      ctx.lineWidth = Math.max(1.5, scale * 0.004);
      ctx.setLineDash([6, 6]);
      ctx.strokeStyle = 'rgba(255,255,255,0.4)';
      ctx.beginPath();
      ctx.moveTo(hx * w, hy * h);
      ctx.lineTo(hx * w, Math.max(0.05, sy - (hy - sy) * 0.6) * h);
      ctx.stroke();
      ctx.restore();

      // Torso lean spine
      ctx.lineWidth = Math.max(3, scale * 0.012);
      ctx.strokeStyle = activeColor;
      ctx.beginPath();
      ctx.moveTo(hx * w, hy * h);
      ctx.lineTo(sx * w, sy * h);
      if (nose) ctx.lineTo(nose.x * w, nose.y * h);
      ctx.stroke();
    } else if (nose) {
      ctx.lineWidth = Math.max(3, scale * 0.011);
      ctx.strokeStyle = activeColor;
      ctx.beginPath();
      ctx.moveTo(sx * w, sy * h);
      ctx.lineTo(nose.x * w, nose.y * h);
      ctx.stroke();
    }

    // Shoulder line colored by left / upright / right lean zone
    ctx.lineWidth = Math.max(4, scale * 0.013);
    ctx.strokeStyle = activeColor;
    ctx.beginPath();
    ctx.moveTo(rs.x * w, rs.y * h);
    ctx.lineTo(ls.x * w, ls.y * h);
    ctx.stroke();
  }

  ctx.fillStyle = activeColor;
  for (const p of [ls, rs, nose]) {
    if (!p) continue;
    ctx.beginPath();
    ctx.arc(p.x * w, p.y * h, Math.max(4, scale * 0.014), 0, Math.PI * 2);
    ctx.fill();
  }
}

/** Smoothed box around the visible body (0..1 image coords), or null. */
export function bodyBox(landmarks, prev, { minVisibility = 0.5, pad = 0.14, smooth = 0.35 } = {}) {
  if (!landmarks) return null;
  let x0 = 1, y0 = 1, x1 = 0, y1 = 0, n = 0;
  for (let i = 0; i < COUNT; i++) {
    const p = landmarks[i];
    if (!p || (p.visibility ?? 1) < minVisibility) continue;
    if (p.x < -0.1 || p.x > 1.1 || p.y < -0.1 || p.y > 1.1) continue;
    x0 = Math.min(x0, p.x); x1 = Math.max(x1, p.x);
    y0 = Math.min(y0, p.y); y1 = Math.max(y1, p.y);
    n++;
  }
  if (n < 4) return null;
  const bw = x1 - x0, bh = y1 - y0;
  const box = {
    x0: Math.max(0.01, x0 - bw * pad), x1: Math.min(0.99, x1 + bw * pad),
    y0: Math.max(0.01, y0 - bh * pad * 0.8), y1: Math.min(0.99, y1 + bh * pad * 0.5),
  };
  if (!prev) return box;
  for (const k of ['x0', 'x1', 'y0', 'y1']) box[k] = prev[k] + (box[k] - prev[k]) * smooth;
  return box;
}

/**
 * Scanner-style corner frame around the player.
 * @param phase 'searching' | 'detected' | 'locked'
 * @param progress 0..1 lock progress (drawn along the frame while 'detected')
 * @param pulse 0..1 extra glow right after locking
 */
export function drawBodyFrame(ctx, box, phase, progress = 0, pulse = 0) {
  if (!box) return;
  const { width: w, height: h } = ctx.canvas;
  const x = box.x0 * w, y = box.y0 * h, bw = (box.x1 - box.x0) * w, bh = (box.y1 - box.y0) * h;
  const s = Math.min(w, h);
  const color = FRAME_COLORS[phase] || FRAME_COLORS.searching;
  const len = Math.min(bw, bh) * 0.22;
  ctx.save();
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  ctx.strokeStyle = color;
  ctx.lineWidth = Math.max(3, s * (phase === 'locked' ? 0.014 : 0.009));
  if (phase === 'locked') {
    ctx.shadowColor = color;
    ctx.shadowBlur = s * (0.03 + 0.05 * pulse);
    ctx.fillStyle = `rgba(52, 211, 153, ${0.10 + 0.12 * pulse})`;
    ctx.fillRect(x, y, bw, bh);
  }
  // four corner brackets
  const corners = [[x, y, 1, 1], [x + bw, y, -1, 1], [x, y + bh, 1, -1], [x + bw, y + bh, -1, -1]];
  ctx.beginPath();
  for (const [cx, cy, dx, dy] of corners) {
    ctx.moveTo(cx + dx * len, cy);
    ctx.lineTo(cx, cy);
    ctx.lineTo(cx, cy + dy * len);
  }
  ctx.stroke();
  // lock progress along the bottom edge
  if (phase === 'detected' && progress > 0) {
    ctx.shadowBlur = 0;
    ctx.strokeStyle = FRAME_COLORS.locked;
    ctx.lineWidth = Math.max(4, s * 0.012);
    ctx.beginPath();
    ctx.moveTo(x + len, y + bh);
    ctx.lineTo(x + len + (bw - 2 * len) * Math.min(1, progress), y + bh);
    ctx.stroke();
  }
  ctx.restore();
}
