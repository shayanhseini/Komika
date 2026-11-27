# Drone Vision — Unity integration guide

For Unity developers **and their AI assistants**. Persian version: `README.fa.md`.

## What this package does

Body control for the drone-operator exhibition game, running inside a Unity **WebGL**
build. The visitor stands in front of a webcam and flies with their arms, like a
fixed-wing aircraft:

| Body | Result | Key it replaces |
|---|---|---|
| Both hands raised like wings, held ~0.45 s | Player locked (green frame) | — |
| Wings tilted: left wrist higher than the right | Turn **right** | `D` |
| Wings tilted: right wrist higher than the left | Turn **left** | `A` |
| Both wrists above the shoulder line | **Up** | `W` |
| Both wrists below the shoulder line (still raised to the side) | **Down** | `S` |
| Hands hanging down | Nothing pressed | — |

Left/right and up/down work at the same time. Keys are "held" while the body stays
in the zone, exactly like the PC drone-operator app (Mashk drone) this package
replaces. The logic is the **same code**: the C# core is a 1:1 port of the PC app's
Python core, checked frame by frame on 2784 recorded frames (`golden/drone_vectors.json`).

Architecture:
- The **WebGL template** (`Assets/WebGLTemplates/DroneVision/`) opens the camera,
  runs the MediaPipe pose model in a Web Worker, and draws the preview, the green
  frame and the connection box. It contains **no game logic**.
- **`DroneVisionInput`** (C#) reads the raw landmarks every frame and decides
  everything: who the player is (crowds), the lock, wings, zones, keys. It is compiled
  to WebAssembly with the game.
- Everything is self-hosted (model, wasm). No CDN is needed at run time.

## Installation (5 steps)

1. Copy `Packages/com.flightvision.dronevision/` into the project's `Packages/` folder
   (or Package Manager → *Add package from disk* → its `package.json`).
2. Copy `Assets/WebGLTemplates/DroneVision/` into the project's `Assets/WebGLTemplates/`.
3. **Edit → Project Settings → Player → WebGL → Resolution and Presentation →
   WebGL Template = DroneVision.**
4. In the **first scene**, create an empty GameObject named `DroneVision` and add the
   `DroneVisionInput` component. It survives scene loads (`DontDestroyOnLoad`).
5. Make the game read the body (next section). Build for WebGL and host over
   **HTTPS** (or open from `localhost`): browsers only allow the camera there.

Unity 2021.3 or newer. Works with the legacy Input Manager, the new Input System, or
both (only `GetKey`'s keyboard merge needs the legacy one).

## Connecting the game (pick ONE way)

### A. Keep the keyboard code (fastest, same as the PC app)

Replace `Input.` with `DroneVisionInput.` for the four drone keys:

```csharp
using DroneVision;

void Update()
{
    if (DroneVisionInput.GetKey(KeyCode.D)) TurnRight();
    if (DroneVisionInput.GetKey(KeyCode.A)) TurnLeft();
    if (DroneVisionInput.GetKey(KeyCode.W)) Climb();
    if (DroneVisionInput.GetKey(KeyCode.S)) Dive();

    float h = DroneVisionInput.GetAxisRaw("Horizontal"); // -1 / 0 / +1
    float v = DroneVisionInput.GetAxisRaw("Vertical");
}
```

`GetKey` / `GetKeyDown` / `GetKeyUp` / `GetAxisRaw` return the vision **or** the real
keyboard (`mergeKeyboard`, on by default), so the game still works with keys. The key
codes are set in the inspector (`keyRight` D, `keyLeft` A, `keyUp` W, `keyDown` S).

If the game uses the **new Input System**, read the state instead:

```csharp
var dv = DroneVisionInput.Instance;
float h = dv.Horizontal;   // -1 left (A) / 0 / +1 right (D)
float v = dv.Vertical;     // -1 down (S) / 0 / +1 up (W)
bool right = dv.Right, left = dv.Left, up = dv.Up, down = dv.Down;
// combine with your own actions: Mathf.Clamp(h + myAction.ReadValue<float>(), -1, 1)
```

### B. Analog control (smoother)

```csharp
var dv = DroneVisionInput.Instance;
if (dv.IsLocked && dv.WingsOpen)
{
    bank  = dv.Roll;    // -1..1, + = the player's right
    climb = dv.Pitch;   // -1..1, + = up
}
```

### Lock / pause (recommended for both)

```csharp
void Start()
{
    DroneVisionInput.Instance.OnLockChanged += OnLock;    // subscribe in Start, not Awake
}
void OnDestroy()
{
    if (DroneVisionInput.Instance != null) DroneVisionInput.Instance.OnLockChanged -= OnLock;
}
void OnLock(bool locked)
{
    if (locked) StartOrResumeFlight();   // green frame: a player is in control
    else PauseFlight();                  // player gone longer than lockGraceMs (1 s)
}
```

A player who leaves and comes back within 2.5 s is recognised and locked again at once.

## What the visitor sees (built in, nothing to build)

- **Lobby** (until the lock): the mirrored camera, big, over a dimmed game, with a
  frame around the player (white → gold with a progress bar → green) and a Persian
  hint line (“دست‌ها را مثل بال هواپیما بالا بیاورید”, “کمی عقب‌تر بایستید…”).
- **Corner** (1 s after the lock): a small preview with a green border and the
  **connection box** under it: a dot (green = connected, gold = connecting, grey =
  waiting), a Persian status (“متصل ✓”) and the four key letters, lit while pressed.
- `previewMode`, `previewCorner`, `statusBox`, `previewLabels` change this;
  `SetPreviewMode(PreviewMode.Hidden)` hides it all (e.g. on a results screen).
- All of it is `pointer-events: none`: it never blocks the game's UI.

## Camera selection

- The browser asks for the camera once; the chosen camera is **remembered** for the
  next visit (localStorage).
- **Ctrl + Shift + C** on the game page switches to the next camera (its name shows
  for 3 s) and remembers it.
- `?camera=2` in the page URL picks camera #2 at start (e.g. an exhibition shortcut).
- From C#: `GetCameras(out currentId)` and `UseCamera(id)` (for an in-game menu).

## API reference: `DroneVision.DroneVisionInput`

| Member | Meaning |
|---|---|
| `static Instance` | The component (null before its `Awake`). |
| `static GetKey / GetKeyDown / GetKeyUp(KeyCode)` | Like `Input.*`: vision OR real keyboard. Call from `Update`. |
| `static GetAxisRaw("Horizontal" / "Vertical")` | -1 / 0 / +1. |
| `IsLocked`, `LockProgress` | Player confirmed (green frame), 0..1 towards it. |
| `WingsOpen` | Both wrists raised. |
| `Horizontal`, `Vertical`, `Right/Left/Up/Down` | The keys: only while locked AND wings open. |
| `Roll`, `Pitch` | Analog -1..1 (0 when the wings are down). |
| `Hint` (`DroneHint`) | What the visitor should do: `RaiseHands`, `BothHands`, `StepBack`, `FaceCamera`, `ComeCloser`, `MoveToCenter`. |
| `Status` (`VisionStatus`) | `Loading`, `Searching`, `Calibrating`, `Ready`, or an error: `PermissionDenied`, `NoCamera`, `CameraBusy`, `Insecure`, `Unsupported`, `ModelFailed`. |
| `Fps`, `InferMs`, `LatencyMs`, `Brightness` | Diagnostics. |
| Events | `OnLockChanged(bool)`, `OnWingsChanged(bool)`, `OnHorizontalChanged(int)`, `OnVerticalChanged(int)`, `OnStatusChanged(VisionStatus)`. |
| `Recalibrate()` | Forget the player (new visitor must raise their wings again). |
| `SetPaused(bool)` | Stop the model in menus (camera stays open, keys released). |
| `SetPreviewMode(PreviewMode)` | Manual preview control (turns `autoPreview` off). |
| `ResumeAutoPreview()` | Back to automatic: lobby until the lock, then the corner box. |
| `SetModel("auto"/"lite"/"full"/"heavy")`, `GetCameras`, `UseCamera`, `Retry`, `StopVision`, `ApplyTuning` | Runtime controls. |

Inspector tuning (defaults = the PC app's): `rollEnter` 0.5, `upMargin` 0.25,
`downMargin` 0.3 (all in the player's own shoulder widths, so near/far does not
matter), `lockMs` 450, `lockGraceMs` 1000, `holdLine` 0.9, `invert`. Model `auto` =
full model on PCs (closest to the PC app), lite on phones, and it drops to lite if
the PC is too slow (FPS < 18 and the model > 40 ms for 5 s, the PC app's rule).
Camera 960×540 like the PC app (open wings need a wide picture).

## Rules for AI assistants

- Use only `DroneVision.DroneVisionInput` (and the samples). Subscribe to events in
  `Start` and unsubscribe in `OnDestroy`. Never access `Instance` from another `Awake`.
- Exactly one `DroneVisionInput`, in the first scene.
- Keep the WebGL template = **DroneVision**. If the project already has a custom
  template, merge: keep its look, but it must load
  `<script type="module" src="dronevision/unity-bridge.js"></script>` **before** the
  Unity loader and ship the `dronevision/` and `vendor/` folders and `.htaccess`.
- **Don't edit** `Runtime/Core/*.cs` (golden-tested twin of the PC app),
  `Runtime/Plugins/WebGL/DroneVision.jslib`, or the template's `dronevision/` and
  `vendor/` files. To change sensitivity use the inspector fields.
- Don't add a second camera/pose pipeline (WebCamTexture, MediaPipe Unity plugin) in
  WebGL: the page already owns the camera.
- Gate gameplay on `IsLocked`. Do not invert axes: positive is already the
  player's own right / up (the preview is mirrored for them).
- The game can stay keyboard-playable: keep `mergeKeyboard` on.

## Testing

- **Editor (no camera):** press Play. A virtual player raises its wings and locks in
  ~0.5 s; the **arrow keys** tilt / raise its wings through the real core, so
  `GetKey(KeyCode.D)` etc. turn on exactly as with a person. Add `DroneVisionDebugHUD`
  (sample) to see the values (F1 toggles).
- **Real camera:** *File → Build And Run* (WebGL) serves on `localhost`, where the
  camera is allowed. On a server use HTTPS.
- Samples: Package Manager → Drone Vision → Samples → *Quick Start*
  (`DroneVisionExample` flies a transform, `DroneVisionDebugHUD`).

## Hosting

- HTTPS (camera rule). The template's `.htaccess` sets the MIME types for `.wasm`,
  `.task` and Unity's `.br`/`.gz` files on Apache/cPanel; on other servers set the
  same. Or build with *Compression Format = Disabled* / *Decompression Fallback* on.
- ~50 MB of vision files (two models + wasm) are cached by the browser after the
  first visit.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `Status` stays `Loading` | Build not using the **DroneVision** template, or `Assets/WebGLTemplates/DroneVision/dronevision/` missing. |
| `ModelFailed` | `vendor/` not uploaded, or the server serves `.wasm`/`.task` with a wrong MIME type. |
| `PermissionDenied` / `Insecure` | Camera blocked, or the page is not HTTPS/localhost. Show a button calling `Retry()`. |
| Wrong camera | Ctrl+Shift+C on the page, or `?camera=N`, or `UseCamera(id)`. |
| Never locks | Both hands must be raised (above the waist) for ~0.5 s. `Hint` says what is wrong. Light the player from the front. |
| Hands out of frame (`StepBack`) | Camera too close: open wings need ~2 m from a wide camera. |
| Turns too easily / too hard | `rollEnter` (0.5; higher = harder). Up/down: `upMargin`, `downMargin`. |
| Left/right swapped | The game maps `D` to its left: swap `keyRight`/`keyLeft`, or set `invert`. |

## File map

| Path | Role | Edit? |
|---|---|---|
| `Runtime/DroneVisionInput.cs` | The component the game uses | Only to extend the API |
| `Runtime/Core/*.cs` | Wings / lock / crowd logic (port of the PC app), buffer layout | **No** |
| `Runtime/Plugins/WebGL/DroneVision.jslib` | C# ↔ page bridge | **No** |
| `Runtime/ILandmarkSource.cs` | Hook for native (non-WebGL) pose providers | — |
| `Samples~/QuickStart/*.cs` | Example drone controller + debug HUD | Copy and adapt freely |
| `Assets/WebGLTemplates/DroneVision/index.html` | Page that loads Unity + the engine | Look only |
| `Assets/WebGLTemplates/DroneVision/dronevision/*.js` | Camera, model worker, overlay, bridge | **No** |
| `Assets/WebGLTemplates/DroneVision/vendor/` | MediaPipe wasm + pose models (self-hosted) | **No** |
