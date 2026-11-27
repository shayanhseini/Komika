// MediaPipe Pose landmark indices (same 33-point topology in the legacy
// solutions API, the Tasks API and the Unity plugin).
export const NOSE = 0;
export const L_SHOULDER = 11;
export const R_SHOULDER = 12;
export const L_ELBOW = 13;
export const R_ELBOW = 14;
export const L_WRIST = 15;
export const R_WRIST = 16;
export const L_HIP = 23;
export const R_HIP = 24;
export const L_KNEE = 25;
export const R_KNEE = 26;
export const L_ANKLE = 27;
export const R_ANKLE = 28;

export const COUNT = 33;

// The only points the drone logic reads (wrists, shoulders, hips; the nose only
// for drawing). Golden test vectors store just these.
export const USED = [NOSE, L_SHOULDER, R_SHOULDER, L_WRIST, R_WRIST, L_HIP, R_HIP];

export const SKELETON = [
  [L_SHOULDER, R_SHOULDER], [L_SHOULDER, L_HIP], [R_SHOULDER, R_HIP], [L_HIP, R_HIP],
  [L_SHOULDER, L_ELBOW], [L_ELBOW, L_WRIST], [R_SHOULDER, R_ELBOW], [R_ELBOW, R_WRIST],
  [L_HIP, L_KNEE], [L_KNEE, L_ANKLE], [R_HIP, R_KNEE], [R_KNEE, R_ANKLE],
];
