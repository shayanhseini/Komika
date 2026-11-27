using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DroneVision.Core;
using UnityEngine;

namespace DroneVision
{
    /// <summary>Engine status. 5..11 match the page feed's error codes.</summary>
    public enum VisionStatus
    {
        Idle = 0, Loading = 1, Searching = 2, Calibrating = 3, Ready = 4,
        PermissionDenied = 5, NoCamera = 6, CameraBusy = 7, Insecure = 8, Unsupported = 9, Error = 10,
        /// <summary>The pose model / WebAssembly files could not be loaded (hosting or network problem).</summary>
        ModelFailed = 11,
    }

    /// <summary>Camera preview drawn over the game (WebGL). Never blocks clicks.</summary>
    public enum PreviewMode { Lobby = 0, Corner = 1, Hidden = 2 }

    public enum PreviewCorner { BottomLeft, BottomRight, TopLeft, TopRight }

    [Serializable]
    public class CameraInfo
    {
        public string id;
        public string label;
        public string facing;
    }

    /// <summary>
    /// The one component the game talks to. Put it on a GameObject in the first scene.
    /// It replaces the PC drone-operator app (which pressed D / A / W / S): the same
    /// gesture, the same logic, but the game reads it directly.
    ///
    ///   // easiest: keep your keyboard code, swap Input for DroneVisionInput
    ///   if (DroneVisionInput.GetKey(KeyCode.D)) TurnRight();      // vision OR the real key
    ///   float h = DroneVisionInput.GetAxisRaw("Horizontal");        // -1 / 0 / +1
    ///   // or read the state
    ///   var v = DroneVisionInput.Instance;
    ///   v.OnLockChanged += locked => { if (locked) StartFlight(); else PauseFlight(); };
    ///   drone.bank = v.Roll;  drone.climb = v.Pitch;                // analog -1..1
    ///
    /// The gesture ("wings", from the wrists only):
    ///   right / left = one wrist higher than the other (wings tilted)
    ///   up / down    = both wrists above / below the shoulder line
    ///   lock         = wings up for lockMs (green frame); keys only while locked and wings up.
    /// All decision logic runs HERE, in the C# core (WebAssembly in WebGL builds).
    /// The page only supplies camera landmarks and draws what this component asks for.
    /// Native builds: assign <see cref="landmarkSource"/>. Editor: arrow keys fly a virtual player.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DroneVisionInput : MonoBehaviour
    {
        public static DroneVisionInput Instance { get; private set; }

        [Header("Camera & model")]
        [Tooltip("auto (default): full model on PCs (closest to the PC app), lite on phones/weak PCs, and drops to lite if the PC is too slow · lite · full · heavy")]
        public string model = "auto";
        [Tooltip("People the model looks for. 1 (default, as the PC app): it follows the player and never looks for anyone else")]
        [Range(1, 3)] public int numPoses = 1;
        [Tooltip("Max model runs per second")]
        [Range(10, 30)] public int maxFps = 30;
        [Tooltip("Camera format asked of the browser. 960×540 (16:9) as the PC app: open wings need a wide picture")]
        public int cameraWidth = 960;
        public int cameraHeight = 540;
        [Tooltip("After the lock the model only sees a crop around the player. OFF: the web model jumps off the hands on a crop (measured)")]
        public bool focusCrop = false;

        [Header("Preview & connection box (WebGL)")]
        [Tooltip("Lobby (big mirror + frame) until the player is locked, then the small corner box. Turned off by SetPreviewMode().")]
        public bool autoPreview = true;
        public PreviewMode previewMode = PreviewMode.Lobby;
        public PreviewCorner previewCorner = PreviewCorner.BottomLeft;
        [Tooltip("Persian guidance line under the lobby preview")]
        public bool previewLabels = true;
        [Tooltip("Status pill under the corner preview: connected / locking / waiting, and the four keys lit while pressed")]
        public bool statusBox = true;
        [Tooltip("How long the green frame stays big before moving to the corner (auto preview)")]
        [Range(0, 3)] public float lockedShowSeconds = 0.7f;

        [Header("Gesture tuning (upright lean mode or legacy wings mode)")]
        [Tooltip("ON: player stands upright naturally and leans slightly left/right to steer (no arms-up required, no up/down jump).")]
        public bool uprightLeanMode = true;
        [Tooltip("Left / right sensitivity in shoulder widths. Lower = easier slight lean (0.20 = gentle ~10 deg lean).")]
        [Range(0.08f, 0.8f)] public float rollEnter = 0.20f;
        [Tooltip("Up: both wrists this far above the shoulder line (unused in uprightLeanMode)")]
        [Range(0.1f, 0.6f)] public float upMargin = 0.25f;
        [Tooltip("Down: both wrists this far below the shoulder line (unused in uprightLeanMode)")]
        [Range(0.1f, 0.6f)] public float downMargin = 0.3f;
        [Tooltip("Standing upright this long locks the player (green frame)")]
        [Range(150, 2000)] public float lockMs = 350;
        [Tooltip("A locked player may be hidden this long without losing the lock")]
        [Range(250, 3000)] public float lockGraceMs = 1200;
        [Tooltip("Wrists count as wings above this line in legacy mode")]
        [Range(0.5f, 1.05f)] public float holdLine = 0.9f;
        [Tooltip("Swap left and right")]
        public bool invert = false;

        [Header("Crowd")]
        [Tooltip("A new player is only accepted inside this central band of the image (fraction of its width)")]
        [Range(0.3f, 1f)] public float stageZone = 0.8f;
        [Range(0.02f, 0.2f)] public float minPlayerSize = 0.04f;

        [Header("Keys (as the PC app: D / A / W / S, held while in the zone)")]
        public KeyCode keyRight = KeyCode.D;
        public KeyCode keyLeft = KeyCode.A;
        public KeyCode keyUp = KeyCode.W;
        public KeyCode keyDown = KeyCode.S;
        [Tooltip("GetKey / GetAxisRaw also read the real keyboard (legacy Input Manager), so the game stays playable with keys")]
        public bool mergeKeyboard = true;

        [Header("Native builds / Editor")]
        [Tooltip("A MonoBehaviour implementing ILandmarkSource (native builds only)")]
        public MonoBehaviour landmarkSource;
        [Tooltip("Editor/testing without a camera: the arrow keys fly a virtual player through the same core")]
        public bool keyboardFallback = true;

        // ---- state (read every frame) ----------------------------------------
        public VisionStatus Status { get; private set; }
        /// <summary>The player is confirmed (green frame). Start / resume on this; pause when it drops.</summary>
        public bool IsLocked { get; private set; }
        /// <summary>0..1 towards the lock.</summary>
        public float LockProgress { get; private set; }
        /// <summary>Wings up: both wrists raised (above the hold line).</summary>
        public bool WingsOpen { get; private set; }
        /// <summary>A body is currently detected.</summary>
        public bool Tracking { get; private set; }
        public DroneHint Hint { get; private set; }
        /// <summary>Analog bank -1..1 (+ = the player's right). 0 when the wings are down.</summary>
        public float Roll { get; private set; }
        /// <summary>Analog climb -1..1 (+ = up). 0 when the wings are down.</summary>
        public float Pitch { get; private set; }
        /// <summary>-1 left / 0 / +1 right — the A / D key, only while locked and wings up.</summary>
        public int Horizontal { get; private set; }
        /// <summary>-1 down / 0 / +1 up — the S / W key, only while locked and wings up.</summary>
        public int Vertical { get; private set; }
        public bool Right { get { return Horizontal > 0; } }
        public bool Left { get { return Horizontal < 0; } }
        public bool Up { get { return Vertical > 0; } }
        public bool Down { get { return Vertical < 0; } }
        public float Fps { get; private set; }
        public float LatencyMs { get; private set; }
        public float InferMs { get; private set; }
        /// <summary>Scene brightness where the player stands, 0..1 (WebGL). Below ~0.12 is too dark.</summary>
        public float Brightness { get; private set; }
        public float ExposureGain { get; private set; }
        public bool Focused { get; private set; }
        /// <summary>Seconds since the lock (0 when not locked).</summary>
        public float SecondsSinceLock { get { return IsLocked ? Time.unscaledTime - _lockedAt : 0f; } }
        /// <summary>The full core state (counters, raw values) for debugging.</summary>
        public DroneState CoreState { get { return _tracker.State; } }

        // ---- events ------------------------------------------------------------
        /// <summary>true = player locked (green) → start / resume; false = lost (hidden longer than lockGraceMs) → pause.</summary>
        public event Action<bool> OnLockChanged;
        /// <summary>true = wings up; false = hands dropped.</summary>
        public event Action<bool> OnWingsChanged;
        /// <summary>Horizontal changed: -1 / 0 / +1 (key A / none / D).</summary>
        public event Action<int> OnHorizontalChanged;
        /// <summary>Vertical changed: -1 / 0 / +1 (key S / none / W).</summary>
        public event Action<int> OnVerticalChanged;
        public event Action<VisionStatus> OnStatusChanged;

        private PlayerTracker _tracker;   // crowd identity + wings + lock
        private ILandmarkSource _source = null;
        private readonly List<PoseFrame> _poses = new List<PoseFrame>(3);
        private readonly List<ColorSig> _sigs = new List<ColorSig>(3);
        private readonly PoseFrame[] _pool = { new PoseFrame(), new PoseFrame(), new PoseFrame() };
        private bool _started;
        private bool _statusSet;
        private bool _paused;
        private float _lockedAt = -1;
        private int _keys, _prevKeys;     // vision keys this frame / last frame (bits: 1 right, 2 left, 4 up, 8 down)
        private int _keysFrame = -1;
        private float _simRoll, _simPitch;
        private Vector2? _debug;
        private readonly PoseFrame _sim = new PoseFrame();

#if UNITY_WEBGL && !UNITY_EDITOR
        private readonly float[] _buf = new float[FeedBuffer.Length];
        private int _lastSeq = -1;
        private double _lastFrameT;
        private int _sentPreview = -1;

        [DllImport("__Internal")] private static extern void DroneVision_Start(string configJson);
        [DllImport("__Internal")] private static extern int DroneVision_Read(float[] buffer, int length);
        [DllImport("__Internal")] private static extern void DroneVision_SetUi(int op, int phase, float progress, int hint, int side, int vert, int held, int keys);
        [DllImport("__Internal")] private static extern void DroneVision_SetPreviewMode(int mode);
        [DllImport("__Internal")] private static extern void DroneVision_SetPaused(int paused);
        [DllImport("__Internal")] private static extern void DroneVision_SetModel(string model);
        [DllImport("__Internal")] private static extern void DroneVision_UseCamera(string deviceId);
        [DllImport("__Internal")] private static extern string DroneVision_GetCameras();
        [DllImport("__Internal")] private static extern void DroneVision_Stop();
        [DllImport("__Internal")] private static extern void DroneVision_Retry();
#endif

        // JSON DTOs: filled by JsonUtility, which the compiler cannot see
#pragma warning disable 0649
        [Serializable] private class StartJson { public string model; public int numPoses; public int maxFps; public int width; public int height; public bool focusCrop; public string previewMode; public string previewCorner; public bool previewLabels; public bool statusBox; public string keyLabels; }
        [Serializable] private class CamerasJson { public string current; public CameraInfo[] cameras; }
#pragma warning restore 0649

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // DontDestroyOnLoad only works on root objects: keep the whole root alive
            DontDestroyOnLoad(transform.root.gameObject);
            _tracker = new PlayerTracker(CoreConfig());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            StartVision();
        }

        /// <summary>Starts the camera + model. Called automatically in Start().</summary>
        public void StartVision()
        {
            if (_started) return;
            _started = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            var cfg = new StartJson
            {
                model = model, numPoses = numPoses, maxFps = maxFps, width = cameraWidth, height = cameraHeight, focusCrop = focusCrop,
                previewMode = ModeName(previewMode), previewCorner = CornerName(previewCorner), previewLabels = previewLabels,
                statusBox = statusBox, keyLabels = KeyLabel(keyRight) + "," + KeyLabel(keyLeft) + "," + KeyLabel(keyUp) + "," + KeyLabel(keyDown),
            };
            DroneVision_Start(JsonUtility.ToJson(cfg));
            _sentPreview = (int)previewMode;
            SetStatus(VisionStatus.Loading);
#else
            _source = landmarkSource as ILandmarkSource;
            if (landmarkSource != null && _source == null)
                Debug.LogError("[DroneVision] landmarkSource does not implement ILandmarkSource", landmarkSource);
            SetStatus(_source != null || keyboardFallback ? VisionStatus.Searching : VisionStatus.Unsupported);
#endif
        }

        private void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ReadWebGL();
            UpdateAutoPreview();
#else
            if (_source != null) ReadNative();
            else if (keyboardFallback) ReadKeyboard();
#endif
            LatchKeys();
        }

        // ---- WebGL -------------------------------------------------------------
#if UNITY_WEBGL && !UNITY_EDITOR
        private void ReadWebGL()
        {
            int n = DroneVision_Read(_buf, _buf.Length);
            if (n < FeedBuffer.PoseData) return;
            Fps = _buf[FeedBuffer.Fps];
            InferMs = _buf[FeedBuffer.InferMs];
            LatencyMs = _buf[FeedBuffer.LatencyMs];
            ExposureGain = _buf[FeedBuffer.Gain];
            Brightness = _buf[FeedBuffer.Lum];
            Focused = _buf[FeedBuffer.Focus] > 0.5f;

            int feed = (int)_buf[FeedBuffer.Status];
            if (feed != FeedBuffer.StatusRunning)
            {
                if (feed >= 5)
                {
                    // camera gone: forget the player and release every key
                    if (_tracker.State.Tracking || _tracker.State.Locked)
                    {
                        _poses.Clear();
                        _sigs.Clear();
                        Consume(_tracker.Update(_poses, _sigs, 16.0 / 9.0, _lastFrameT + 100000));
                    }
                    SetStatus((VisionStatus)feed);
                }
                else SetStatus(feed == 0 ? VisionStatus.Idle : VisionStatus.Loading);
                return;
            }

            int seq = (int)_buf[FeedBuffer.Seq];
            if (seq == _lastSeq) return; // no new camera frame since the last Unity frame
            _lastSeq = seq;
            _lastFrameT = FeedBuffer.Timestamp(_buf);
            FeedBuffer.ReadPoses(_buf, _pool, _poses, _sigs);
            Consume(_tracker.Update(_poses, _sigs, _buf[FeedBuffer.Aspect], _lastFrameT));

            // tell the page who the player is: it draws their frame, wings and the lit keys
            DroneState s = _tracker.State;
            int phase = !s.Tracking ? 0 : (s.Locked ? 2 : 1);
            DroneVision_SetUi(s.Tracking ? s.Operator : -1, phase, (float)s.LockProgress, (int)s.Hint, s.Side, s.Vert,
                s.HandsHeld ? 1 : 0, KeyBits());
        }

        private void UpdateAutoPreview()
        {
            if (!autoPreview) return;
            PreviewMode want = IsLocked && Time.unscaledTime - _lockedAt >= lockedShowSeconds ? PreviewMode.Corner : PreviewMode.Lobby;
            if ((int)want == _sentPreview) return;
            _sentPreview = (int)want;
            previewMode = want;
            DroneVision_SetPreviewMode((int)want);
        }
#endif

        // ---- native ------------------------------------------------------------
        private void ReadNative()
        {
            if (_paused) return;
            double t;
            _poses.Clear();
            _sigs.Clear(); // native sources give no clothing signature: position + size tracking only
            if (!_source.TryGetLatest(_poses, out t)) return;
            double aspect = _poses.Count > 0 ? _poses[0].Aspect : 16.0 / 9.0;
            Consume(_tracker.Update(_poses, _sigs, aspect, t));
        }

        // ---- editor / fallback: a virtual player whose wings follow the arrow keys ------
        private void ReadKeyboard()
        {
            if (_paused) return;
            float roll = 0, pitch = 0;
            if (_debug.HasValue)
            {
                roll = _debug.Value.x;
                pitch = _debug.Value.y;
            }
            else
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                roll = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
                pitch = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
#endif
            }
            // wrists move at most ~6 shoulder widths per second, like a person
            float step = 6f * Time.unscaledDeltaTime;
            _simRoll = Mathf.MoveTowards(_simRoll, roll * 0.5f, step);
            _simPitch = Mathf.MoveTowards(_simPitch, pitch * 0.6f, step);
            FillSimPose(_sim, _simRoll, _simPitch, uprightLeanMode);
            _poses.Clear();
            _sigs.Clear();
            _poses.Add(_sim);
            // the same core as the camera: the lock, zones and keys behave exactly as in the game
            Consume(_tracker.Update(_poses, _sigs, _sim.Aspect, Time.unscaledTime * 1000.0));
        }

        /// <summary>Synthetic front-facing pose for Editor arrow-key testing.</summary>
        private static void FillSimPose(PoseFrame f, float roll, float pitch, bool leanMode)
        {
            const double cx = 0.5, sy = 0.4, sw = 0.1; // shoulder width (frame-width units)
            double aspect = 16.0 / 9.0, swh = sw * aspect;
            f.Aspect = aspect;
            Landmark[] lm = f.Landmarks;
            for (int i = 0; i < PoseFrame.Count; i++) lm[i] = new Landmark(cx, 0.5, 0);
            if (leanMode)
            {
                double sx = cx - roll * sw * 0.55;
                lm[PoseFrame.Nose] = new Landmark(cx - roll * sw * 0.85, sy - swh * 0.8, 0.99);
                lm[PoseFrame.LShoulder] = new Landmark(sx + sw / 2, sy - roll * swh * 0.12, 0.99);
                lm[PoseFrame.RShoulder] = new Landmark(sx - sw / 2, sy + roll * swh * 0.12, 0.99);
                lm[PoseFrame.LHip] = new Landmark(cx + sw * 0.4, sy + swh * 1.45, 0.97);
                lm[PoseFrame.RHip] = new Landmark(cx - sw * 0.4, sy + swh * 1.45, 0.97);
                lm[PoseFrame.LWrist] = new Landmark(sx + sw * 0.65, sy + swh * 1.35, 0.9);
                lm[PoseFrame.RWrist] = new Landmark(sx - sw * 0.65, sy + swh * 1.35, 0.9);
            }
            else
            {
                lm[PoseFrame.Nose] = new Landmark(cx, sy - swh * 0.8, 0.99);
                lm[PoseFrame.LShoulder] = new Landmark(cx + sw / 2, sy, 0.99);   // raw image: the person's left is +x
                lm[PoseFrame.RShoulder] = new Landmark(cx - sw / 2, sy, 0.99);
                lm[PoseFrame.LHip] = new Landmark(cx + sw * 0.4, sy + swh * 1.45, 0.97);
                lm[PoseFrame.RHip] = new Landmark(cx - sw * 0.4, sy + swh * 1.45, 0.97);
                double lUp = pitch + roll, rUp = pitch - roll; // left wrist higher = bank right
                lm[PoseFrame.LWrist] = new Landmark(cx + sw * 2.1, sy - lUp * swh, 0.9);
                lm[PoseFrame.RWrist] = new Landmark(cx - sw * 2.1, sy - rUp * swh, 0.9);
            }
        }

        /// <summary>Drive the virtual player from your own debug input (x = roll, y = pitch, -1..1). Null releases it.</summary>
        public void DebugSetWings(Vector2? rollPitch)
        {
            _debug = rollPitch;
        }

        // ---- state + events ----------------------------------------------------
        private void Consume(DroneState s)
        {
            SetStatus(s.Status == DroneStatus.Ready ? VisionStatus.Ready
                : s.Status == DroneStatus.Calibrating ? VisionStatus.Calibrating : VisionStatus.Searching);
            Roll = (float)s.Roll;
            Pitch = (float)s.Pitch;
            Hint = s.Hint;
            Tracking = s.Tracking;
            LockProgress = (float)s.LockProgress;

            if (s.Locked != IsLocked)
            {
                IsLocked = s.Locked;
                if (s.Locked) _lockedAt = Time.unscaledTime;
                RaiseBool(OnLockChanged, s.Locked);
            }
            if (s.HandsHeld != WingsOpen)
            {
                WingsOpen = s.HandsHeld;
                RaiseBool(OnWingsChanged, s.HandsHeld);
            }
            // keys only while locked AND wings up (the PC app's rule)
            bool live = s.Locked && s.HandsHeld;
            int h = live ? s.Side : 0, v = live ? s.Vert : 0;
            if (h != Horizontal)
            {
                Horizontal = h;
                RaiseInt(OnHorizontalChanged, h);
            }
            if (v != Vertical)
            {
                Vertical = v;
                RaiseInt(OnVerticalChanged, v);
            }
        }

        private int KeyBits()
        {
            return (Horizontal > 0 ? 1 : 0) | (Horizontal < 0 ? 2 : 0) | (Vertical > 0 ? 4 : 0) | (Vertical < 0 ? 8 : 0);
        }

        private void LatchKeys()
        {
            if (_keysFrame == Time.frameCount) return;
            _keysFrame = Time.frameCount;
            _prevKeys = _keys;
            _keys = KeyBits();
        }

        private int Bit(KeyCode key)
        {
            int b = 0;
            if (key == keyRight) b |= 1;
            if (key == keyLeft) b |= 2;
            if (key == keyUp) b |= 4;
            if (key == keyDown) b |= 8;
            return b;
        }

        private void SetStatus(VisionStatus status)
        {
            if (status == Status && _statusSet) return;
            _statusSet = true;
            Status = status;
            if (OnStatusChanged != null)
            {
                try { OnStatusChanged(status); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private static void RaiseBool(Action<bool> evt, bool value)
        {
            if (evt == null) return;
            try { evt(value); } catch (Exception e) { Debug.LogException(e); }
        }

        private static void RaiseInt(Action<int> evt, int value)
        {
            if (evt == null) return;
            try { evt(value); } catch (Exception e) { Debug.LogException(e); }
        }

        // ---- drop-in replacements for Input.GetKey / GetAxisRaw ------------------
        /// <summary>
        /// Like Input.GetKey: true while the vision holds this key (keyRight / keyLeft / keyUp /
        /// keyDown) or, with mergeKeyboard, while the real key is down.
        /// </summary>
        public static bool GetKey(KeyCode key)
        {
            DroneVisionInput d = Instance;
            if (d != null)
            {
                d.LatchKeys();
                if ((d._keys & d.Bit(key)) != 0) return true;
                if (!d.mergeKeyboard) return false;
            }
            return RealKey(key);
        }

        /// <summary>Like Input.GetKeyDown (call it from Update).</summary>
        public static bool GetKeyDown(KeyCode key)
        {
            DroneVisionInput d = Instance;
            if (d != null)
            {
                d.LatchKeys();
                int b = d.Bit(key);
                if ((d._keys & b) != 0 && (d._prevKeys & b) == 0) return true;
                if (!d.mergeKeyboard) return false;
            }
            return RealKeyDown(key);
        }

        /// <summary>Like Input.GetKeyUp (call it from Update).</summary>
        public static bool GetKeyUp(KeyCode key)
        {
            DroneVisionInput d = Instance;
            if (d != null)
            {
                d.LatchKeys();
                int b = d.Bit(key);
                if ((d._keys & b) == 0 && (d._prevKeys & b) != 0) return true;
                if (!d.mergeKeyboard) return false;
            }
            return RealKeyUp(key);
        }

        /// <summary>
        /// Like Input.GetAxisRaw for "Horizontal" (A/D) and "Vertical" (S/W): -1 / 0 / +1 from the
        /// vision, or from the real keys (mergeKeyboard) when the vision gives 0.
        /// </summary>
        public static float GetAxisRaw(string axis)
        {
            DroneVisionInput d = Instance;
            bool horizontal = axis == "Horizontal";
            if (!horizontal && axis != "Vertical") return 0f;
            if (d != null)
            {
                int v = horizontal ? d.Horizontal : d.Vertical;
                if (v != 0 || !d.mergeKeyboard) return v;
                KeyCode pos = horizontal ? d.keyRight : d.keyUp, neg = horizontal ? d.keyLeft : d.keyDown;
                return (RealKey(pos) ? 1f : 0f) - (RealKey(neg) ? 1f : 0f);
            }
            return (RealKey(horizontal ? KeyCode.D : KeyCode.W) ? 1f : 0f) - (RealKey(horizontal ? KeyCode.A : KeyCode.S) ? 1f : 0f);
        }

        private static bool RealKey(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        private static bool RealKeyDown(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        private static bool RealKeyUp(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(key);
#else
            return false;
#endif
        }

        // ---- commands ----------------------------------------------------------
        /// <summary>New player: forget the current one; they must raise their wings again to lock.</summary>
        public void Recalibrate()
        {
            _tracker.Recalibrate();
            Consume(_tracker.State);
        }

        /// <summary>Take manual control of the preview (turns <see cref="autoPreview"/> off).</summary>
        public void SetPreviewMode(PreviewMode mode)
        {
            autoPreview = false;
            previewMode = mode;
#if UNITY_WEBGL && !UNITY_EDITOR
            _sentPreview = (int)mode;
            DroneVision_SetPreviewMode((int)mode);
#endif
        }

        /// <summary>Back to the automatic preview (lobby until the lock, then the corner box).</summary>
        public void ResumeAutoPreview()
        {
            autoPreview = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            _sentPreview = -1;
#endif
        }

        /// <summary>Pause the model (menus, results): saves GPU. The camera stays open, keys are released.</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                _poses.Clear();
                _sigs.Clear();
                Horizontal = 0;
                Vertical = 0;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            DroneVision_SetPaused(paused ? 1 : 0);
#endif
        }

        /// <summary>"auto" | "lite" | "full" | "heavy".</summary>
        public void SetModel(string modelName)
        {
            model = modelName;
#if UNITY_WEBGL && !UNITY_EDITOR
            DroneVision_SetModel(modelName);
#endif
        }

        /// <summary>Push the inspector tuning values to the core at runtime.</summary>
        public void ApplyTuning()
        {
            CopyTuning(_tracker.Config);
            _tracker.ApplyConfig();
        }

        /// <summary>Cameras the browser reports (labels appear after permission). Native: WebCamTexture devices.</summary>
        public CameraInfo[] GetCameras(out string currentId)
        {
            currentId = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            CamerasJson j = JsonUtility.FromJson<CamerasJson>(DroneVision_GetCameras());
            currentId = j.current;
            return j.cameras ?? new CameraInfo[0];
#else
            WebCamDevice[] devices = WebCamTexture.devices;
            var list = new CameraInfo[devices.Length];
            for (int i = 0; i < devices.Length; i++)
                list[i] = new CameraInfo { id = devices[i].name, label = devices[i].name, facing = devices[i].isFrontFacing ? "user" : "environment" };
            return list;
#endif
        }

        /// <summary>Switch camera (remembered by the browser for the next visit).</summary>
        public void UseCamera(string deviceId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DroneVision_UseCamera(deviceId ?? "");
#endif
        }

        /// <summary>After PermissionDenied / NoCamera: ask again (e.g. from a "try again" button).</summary>
        public void Retry()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DroneVision_Retry();
#endif
        }

        public void StopVision()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DroneVision_Stop();
#endif
            _started = false;
        }

        private DroneConfig CoreConfig()
        {
            var c = new DroneConfig();
            CopyTuning(c);
            return c;
        }

        private void CopyTuning(DroneConfig c)
        {
            c.uprightLeanMode = uprightLeanMode;
            c.rollEnter = rollEnter; c.upMargin = upMargin; c.downMargin = downMargin;
            c.lockMs = lockMs; c.lockGraceMs = lockGraceMs; c.holdLine = holdLine; c.invert = invert;
            c.stageZone = stageZone; c.minOperatorScale = minPlayerSize;
        }

        private static string KeyLabel(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.RightArrow: return "→";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                default: return k.ToString();
            }
        }

        private static string ModeName(PreviewMode m)
        {
            return m == PreviewMode.Corner ? "corner" : m == PreviewMode.Hidden ? "hidden" : "lobby";
        }

        private static string CornerName(PreviewCorner c)
        {
            switch (c)
            {
                case PreviewCorner.BottomRight: return "bottom-right";
                case PreviewCorner.TopLeft: return "top-left";
                case PreviewCorner.TopRight: return "top-right";
                default: return "bottom-left";
            }
        }
    }
}
