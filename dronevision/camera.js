// Camera discovery and opening. Works the same on Windows browsers, Android
// and iOS. Labels are only exposed by browsers after camera permission, so
// listCameras() is called again once a stream is open.

export const CAMERA_ERROR = Object.freeze({
  INSECURE: 'insecure',              // page not served over HTTPS / localhost
  UNSUPPORTED: 'unsupported',        // no getUserMedia (very old browser / webview)
  PERMISSION: 'permission-denied',
  NOT_FOUND: 'no-camera',
  BUSY: 'camera-busy',               // another app holds the camera
  FAILED: 'error',
});

export class CameraError extends Error {
  constructor(code, cause) {
    super(code);
    this.code = code;
    this.cause = cause;
  }
}

function guessFacing(label) {
  const l = (label || '').toLowerCase();
  if (/front|user|facetime|selfie|جلو/.test(l)) return 'user';
  if (/back|rear|environment|عقب/.test(l)) return 'environment';
  return 'unknown';
}

export function cameraSupport() {
  if (typeof window !== 'undefined' && window.isSecureContext === false) return CAMERA_ERROR.INSECURE;
  if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return CAMERA_ERROR.UNSUPPORTED;
  return null;
}

/** @returns {Promise<Array<{id:string,label:string,facing:string}>>} */
export async function listCameras() {
  if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) return [];
  const devices = await navigator.mediaDevices.enumerateDevices();
  return devices
    .filter(d => d.kind === 'videoinput')
    .map((d, i) => ({ id: d.deviceId, label: d.label || `دوربین ${i + 1}`, facing: guessFacing(d.label) }));
}

/** Calls back whenever a camera is plugged in or removed. Returns an unsubscribe fn. */
export function watchCameras(callback) {
  const md = navigator.mediaDevices;
  if (!md || !md.addEventListener) return () => {};
  const handler = () => callback();
  md.addEventListener('devicechange', handler);
  return () => md.removeEventListener('devicechange', handler);
}

/**
 * Opens a camera into a <video>. Low resolution on purpose: the pose model
 * works at 256 px anyway, and a smaller frame means less upload + lower latency.
 */
export async function openCamera(video, { deviceId = null, facingMode = 'user', width = 640, height = 480, fps = 30 } = {}) {
  const unsupported = cameraSupport();
  if (unsupported) throw new CameraError(unsupported);

  const base = { width: { ideal: width }, height: { ideal: height }, frameRate: { ideal: fps, max: 60 } };
  const video_ = deviceId ? { ...base, deviceId: { exact: deviceId } } : { ...base, facingMode };
  let stream;
  try {
    stream = await navigator.mediaDevices.getUserMedia({ video: video_, audio: false });
  } catch (err) {
    const name = err && err.name;
    if (name === 'NotAllowedError' || name === 'SecurityError') throw new CameraError(CAMERA_ERROR.PERMISSION, err);
    if (name === 'NotFoundError' || name === 'OverconstrainedError') throw new CameraError(CAMERA_ERROR.NOT_FOUND, err);
    if (name === 'NotReadableError' || name === 'AbortError') throw new CameraError(CAMERA_ERROR.BUSY, err);
    throw new CameraError(CAMERA_ERROR.FAILED, err);
  }
  video.muted = true;
  video.playsInline = true;
  video.setAttribute('playsinline', '');
  video.srcObject = stream;
  await video.play().catch(() => {});
  if (!video.videoWidth) {
    await new Promise(resolve => video.addEventListener('loadedmetadata', resolve, { once: true }));
  }
  const track = stream.getVideoTracks()[0];
  const settings = track && track.getSettings ? track.getSettings() : {};
  return { stream, track, settings, label: track ? track.label : '' };
}

export function stopStream(stream) {
  if (stream) for (const t of stream.getTracks()) t.stop();
}
