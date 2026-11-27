using DroneVision;
using UnityEngine;

namespace DroneVision.Samples
{
    /// <summary>
    /// On-screen debug readout (IMGUI, no UI package needed): status, lock, wings, zones,
    /// keys, FPS and latency. Remove it (or untick <see cref="show"/>) for release. F1 toggles it.
    /// </summary>
    public class DroneVisionDebugHUD : MonoBehaviour
    {
        public bool show = true;
        [Range(10, 40)] public int fontSize = 18;

        private GUIStyle _style;

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) show = !show;
#endif
        }

        private void OnGUI()
        {
            if (!show) return;
            DroneVisionInput v = DroneVisionInput.Instance;
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft };
            _style.fontSize = fontSize;
            string text = v == null
                ? "DroneVisionInput: missing from the scene"
                : "Status: " + v.Status + "   Hint: " + v.Hint
                  + "\nLocked: " + v.IsLocked + "  (" + Mathf.RoundToInt(v.LockProgress * 100) + "%)   Wings: " + v.WingsOpen
                  + "\nRoll: " + v.Roll.ToString("0.00") + "   Pitch: " + v.Pitch.ToString("0.00")
                  + "\nKeys: " + (v.Left ? "A " : "") + (v.Right ? "D " : "") + (v.Up ? "W " : "") + (v.Down ? "S " : "")
                  + "   H " + v.Horizontal + "  V " + v.Vertical
                  + "\nFPS: " + v.Fps.ToString("0") + "   Model: " + v.InferMs.ToString("0") + " ms   Latency: " + v.LatencyMs.ToString("0") + " ms";
            GUI.Box(new Rect(10, 10, fontSize * 26, fontSize * 7), text, _style);
        }
    }
}
