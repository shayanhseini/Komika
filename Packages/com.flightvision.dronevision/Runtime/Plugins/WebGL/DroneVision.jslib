// Unity WebGL <-> camera/model feed (the DroneVision WebGL template's
// dronevision/unity-bridge.js, loaded as window.DroneVisionUnity). The page side
// has no game logic: it hands over raw landmarks and draws what C# tells it to.
// Keep in sync with DroneVisionInput.cs. Prefix DroneVision_ so this package can
// live in the same Unity project as BodyLean (BodyLean_) and Mashk (Mashk_).
mergeInto(LibraryManager.library, {

  DroneVision_Start: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    if (window.DroneVisionUnity) window.DroneVisionUnity.start(json);
    else window.__droneVisionPending = json; // bridge module still loading
  },

  // Copies the feed buffer (status + raw landmarks) into the managed float[].
  DroneVision_Read: function (bufPtr, len) {
    var b = window.DroneVisionUnity;
    if (!b) return 0;
    var src = b.read();
    var n = Math.min(len, src.length);
    HEAPF32.set(src.subarray(0, n), bufPtr >> 2);
    return n;
  },

  // Drawing only: operator index, phase (0 searching, 1 detected, 2 locked),
  // lock progress 0..1, hint code (DroneHint), roll zone -1/0/+1, pitch zone
  // -1/0/+1, wings up 0/1, pressed keys (bits: 1 right, 2 left, 4 up, 8 down).
  DroneVision_SetUi: function (op, phase, progress, hint, side, vert, held, keys, runPct) {
    if (window.DroneVisionUnity) window.DroneVisionUnity.setUi(op, phase, progress, hint, side, vert, held, keys, runPct);
  },

  // 0 lobby (big preview + frame), 1 corner, 2 hidden
  DroneVision_SetPreviewMode: function (mode) {
    if (window.DroneVisionUnity) window.DroneVisionUnity.setPreviewMode(['lobby', 'corner', 'hidden'][mode] || 'lobby');
  },

  DroneVision_SetPaused: function (paused) {
    if (window.DroneVisionUnity) window.DroneVisionUnity.setPaused(!!paused);
  },

  DroneVision_SetModel: function (modelPtr) {
    if (window.DroneVisionUnity) window.DroneVisionUnity.setModel(UTF8ToString(modelPtr));
  },

  DroneVision_UseCamera: function (idPtr) {
    if (window.DroneVisionUnity) window.DroneVisionUnity.useCamera(UTF8ToString(idPtr));
  },

  DroneVision_GetCameras: function () {
    var s = window.DroneVisionUnity ? window.DroneVisionUnity.camerasJson() : '{"current":null,"cameras":[]}';
    var size = lengthBytesUTF8(s) + 1;
    var ptr = _malloc(size);
    stringToUTF8(s, ptr, size);
    return ptr; // freed by the managed marshaller
  },

  DroneVision_Stop: function () {
    if (window.DroneVisionUnity) window.DroneVisionUnity.stop();
  },

  DroneVision_Retry: function () {
    if (window.DroneVisionUnity) window.DroneVisionUnity.retry();
  },
});
