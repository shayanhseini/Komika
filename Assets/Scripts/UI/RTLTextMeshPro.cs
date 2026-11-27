using TMPro;
using UnityEngine;

/// <summary>
/// Drop-in RTL TMPro bridge component for dynamic and static Persian UI and 3D world-space signs using IRANSans + PersianTextShaper.
/// Ensures Unity's TMP right-to-left flag stays off so pre-shaped visual glyphs render cleanly in WebGL & Editor.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class RTLTextMeshPro : MonoBehaviour
{
    [TextArea(1, 4)]
    [SerializeField] private string rawText = "";
    [SerializeField] private bool convertDigitsToPersian = true;

    private TMP_Text targetText;
    private string lastShapedRaw;
    private bool lastShapedDigits;
    private bool hasApplied;

    public string RawText
    {
        get => rawText;
        set
        {
            if (rawText == value && targetText != null) return;
            rawText = value;
            Apply();
        }
    }

    private void Awake() => Apply();
    private void OnEnable() => Apply();
    private void OnValidate() { hasApplied = false; Apply(); }

    public void SetText(string value, bool? persianDigits = null)
    {
        if (persianDigits.HasValue) convertDigitsToPersian = persianDigits.Value;
        rawText = value ?? "";
        Apply();
    }

    public void Apply()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
        if (targetText == null) return;

        if (Application.isPlaying && hasApplied
            && string.Equals(lastShapedRaw, rawText, System.StringComparison.Ordinal)
            && lastShapedDigits == convertDigitsToPersian)
            return;

        string shaped = PersianTextShaper.Shape(rawText, convertDigitsToPersian);
        lastShapedRaw = rawText;
        lastShapedDigits = convertDigitsToPersian;
        hasApplied = true;

        if (targetText.isRightToLeftText)
            targetText.isRightToLeftText = false;
        if (!string.Equals(targetText.text, shaped, System.StringComparison.Ordinal))
            targetText.text = shaped;
    }

    public static void ApplyTo(TMP_Text label, string text, bool persianDigits = true)
    {
        if (label == null) return;
        if (label.TryGetComponent(out RTLTextMeshPro rtl))
        {
            rtl.SetText(text, persianDigits);
        }
        else
        {
            label.isRightToLeftText = false;
            label.text = PersianTextShaper.Shape(text, persianDigits);
        }
    }
}
