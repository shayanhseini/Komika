using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class UpgradeKomikaCarousel
{
    private static readonly Color Navy = new(0.022f, 0.05f, 0.085f, 0.96f);
    private static readonly Color Panel = new(0.05f, 0.11f, 0.165f, 0.98f);
    private static readonly Color Cyan = new(0.14f, 0.90f, 1f, 1f);
    private static readonly Color Orange = new(1f, 0.56f, 0.14f, 1f);
    private static TMP_FontAsset font;

    public static string Main()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Transform root = canvas != null ? canvas.transform.Find("Komika UI") : null;
        if (root == null) return "ERROR: Komika UI not found";

        KomikaUIController controller = root.GetComponent<KomikaUIController>();
        UIManager uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (controller == null || uiManager == null) return "ERROR: controller or UIManager not found";
        font = BuildKomikaUI.EnsureIranSansFont();

        AddLandscapeStartControls(root.Find("Menu Extensions"), uiManager);

        Transform oldPanel = root.Find("Character Selection Panel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);
        GameObject panel = BuildLandscapeCarousel(root, controller, out RectTransform[] cards, out CanvasGroup[] groups, out GameObject[] dots);

        Set(controller, "characterPanel", panel);
        Set(controller, "characterCards", cards.Cast<Object>().ToArray());
        Set(controller, "characterCardGroups", groups.Cast<Object>().ToArray());
        Set(controller, "selectedIndicators", dots.Cast<Object>().ToArray());

        var so = new SerializedObject(controller);
        var spacingProp = so.FindProperty("carouselSpacing");
        if (spacingProp != null) spacingProp.floatValue = 320f;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);

        EditorUtility.SetDirty(root.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        return $"Upgraded Persian Landscape carousel: cards={cards.Length}";
    }

    private static void AddLandscapeStartControls(Transform menu, UIManager uiManager)
    {
        if (menu == null) return;
        Transform oldTap = menu.Find("Center Start Tap Area");
        if (oldTap != null) Object.DestroyImmediate(oldTap.gameObject);
        Transform oldPlay = menu.Find("Start Run Button");
        if (oldPlay != null) Object.DestroyImmediate(oldPlay.gameObject);

        GameObject bgTap = Image("Center Start Tap Area", menu, new Color(0f, 0f, 0f, 0.005f));
        Stretch(bgTap.GetComponent<RectTransform>());
        Button tapButton = bgTap.AddComponent<Button>();
        var cb = tapButton.colors;
        cb.normalColor = cb.highlightedColor = cb.pressedColor = cb.selectedColor = Color.white;
        tapButton.colors = cb;
        UnityEventTools.AddPersistentListener(tapButton.onClick, uiManager.StartGame);
        bgTap.transform.SetAsFirstSibling();

        Button playBtn = Button("Start Run Button", menu, "شروع دویدن", new Color(0.95f, 0.45f, 0.08f, 1f), Color.white);
        Rect(playBtn.GetComponent<RectTransform>(), .5f, 0f, new Vector2(0f, 54f), new Vector2(240f, 64f));
        TMP_Text label = playBtn.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null) label.fontSize = 22f;
        UnityEventTools.AddPersistentListener(playBtn.onClick, uiManager.StartGame);
    }

    private static GameObject BuildLandscapeCarousel(Transform parent, KomikaUIController controller, out RectTransform[] cards, out CanvasGroup[] groups, out GameObject[] dots)
    {
        GameObject overlay = Image("Character Selection Panel", parent, Navy);
        Stretch(overlay.GetComponent<RectTransform>());
        CharacterCarouselSwipe swipe = overlay.AddComponent<CharacterCarouselSwipe>();
        Set(swipe, "controller", controller);

        GameObject top = Image("Top Fill", overlay.transform, new Color(.03f, .10f, .15f, 0.9f));
        RectStretch(top.GetComponent<RectTransform>(), new Vector2(0, .82f), Vector2.one);
        GameObject bottom = Image("Bottom Fill", overlay.transform, new Color(.025f, .07f, .105f, 0.9f));
        RectStretch(bottom.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, .16f));

        TMP_Text title = Text("Title", overlay.transform, "انتخاب دونده", 32, Cyan, TextAlignmentOptions.Center, Vector2.zero, new Vector2(560, 50));
        Rect(title.rectTransform, .5f, 1f, new Vector2(0, -45), new Vector2(560, 50));
        TMP_Text hint = Text("Swipe Hint", overlay.transform, "برای تغییر به چپ و راست بکشید یا فلش بزنید", 14, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(560, 28));
        Rect(hint.rectTransform, .5f, 1f, new Vector2(0, -82), new Vector2(560, 28));

        GameObject carousel = new("Horizontal Character Carousel", typeof(RectTransform));
        carousel.transform.SetParent(overlay.transform, false);
        Rect(carousel.GetComponent<RectTransform>(), .5f, .5f, new Vector2(0, 4f), new Vector2(960, 390));

        Color[] colors = { new(.1f,.85f,1f), new(1f,.42f,.12f), new(.66f,.25f,1f), new(.35f,1f,.3f), new(1f,.18f,.3f) };
        string[] persianNames = { "فیروزه‌ای", "نارنجی", "بنفش", "سبز نئون", "قرمز" };
        string[] rawNames = { "CYAN", "ORANGE", "PURPLE", "LIME", "RED" };
        cards = new RectTransform[5]; groups = new CanvasGroup[5]; dots = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            int captured = i;
            Button card = Button("Character " + rawNames[i], carousel.transform, "", Panel, colors[i]);
            cards[i] = card.GetComponent<RectTransform>();
            Rect(cards[i], .5f, .5f, new Vector2(i * 320f, 0), new Vector2(265, 340));
            groups[i] = card.gameObject.AddComponent<CanvasGroup>();
            UnityEventTools.AddIntPersistentListener(card.onClick, controller.SelectCharacter, captured);

            GameObject glow = Image("Card Glow", card.transform, new Color(colors[i].r, colors[i].g, colors[i].b, .14f));
            Stretch(glow.GetComponent<RectTransform>());
            glow.GetComponent<Image>().raycastTarget = false;

            GameObject cube = Image("Cube Preview", card.transform, colors[i]);
            Rect(cube.GetComponent<RectTransform>(), .5f, .5f, new Vector2(0, 36), new Vector2(145, 145));
            cube.GetComponent<Image>().raycastTarget = false;

            GameObject shine = Image("Highlight", cube.transform, Color.Lerp(colors[i], Color.white, .45f));
            Rect(shine.GetComponent<RectTransform>(), 0f, 1f, new Vector2(28, -22), new Vector2(58, 18));
            shine.GetComponent<Image>().raycastTarget = false;

            Text("Character Name", card.transform, persianNames[i], 24, colors[i], TextAlignmentOptions.Center, new Vector2(0, -86), new Vector2(240, 44));
            Text("Character Type", card.transform, "دونده تهرانی", 14, Color.white, TextAlignmentOptions.Center, new Vector2(0, -120), new Vector2(220, 28));
        }

        Button previous = Button("Previous Character", overlay.transform, "<", Panel, Cyan);
        Rect(previous.GetComponent<RectTransform>(), .5f, .5f, new Vector2(-485, 4f), new Vector2(68, 110));
        UnityEventTools.AddPersistentListener(previous.onClick, controller.ShowPreviousCharacter);

        Button next = Button("Next Character", overlay.transform, ">", Panel, Cyan);
        Rect(next.GetComponent<RectTransform>(), .5f, .5f, new Vector2(485, 4f), new Vector2(68, 110));
        UnityEventTools.AddPersistentListener(next.onClick, controller.ShowNextCharacter);

        GameObject dotRow = new("Selection Dots", typeof(RectTransform));
        dotRow.transform.SetParent(overlay.transform, false);
        Rect(dotRow.GetComponent<RectTransform>(), .5f, 0f, new Vector2(0, 102), new Vector2(240, 20));
        for (int i = 0; i < 5; i++)
        {
            dots[i] = Image("Dot " + (i + 1), dotRow.transform, Cyan);
            Rect(dots[i].GetComponent<RectTransform>(), .5f, .5f, new Vector2((i - 2) * 42, 0), new Vector2(26, 7));
        }

        Button back = Button("Back Button", overlay.transform, "بازگشت", Panel, Orange);
        Rect(back.GetComponent<RectTransform>(), .5f, 0f, new Vector2(0, 44), new Vector2(230, 54));
        UnityEventTools.AddPersistentListener(back.onClick, controller.CloseOverlayPanels);
        return overlay;
    }

    private static Button Button(string name, Transform parent, string label, Color bg, Color accent)
    {
        GameObject go = Image(name, parent, bg); Button b = go.AddComponent<Button>();
        ColorBlock cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(.86f, 1f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); b.colors = cb;
        if (!string.IsNullOrEmpty(label)) { TMP_Text t = Text("Label", go.transform, label, 26, accent, TextAlignmentOptions.Center, Vector2.zero, new Vector2(20, 20)); Stretch(t.rectTransform); }
        return b;
    }
    private static GameObject Image(string name, Transform parent, Color color) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false); go.GetComponent<UnityEngine.UI.Image>().color = color; return go; }
    private static TMP_Text Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, Vector2 pos, Vector2 sizeDelta)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.color = color; t.alignment = alignment; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        Rect(t.rectTransform, .5f, .5f, pos, sizeDelta);
        var rtl = go.AddComponent<RTLTextMeshPro>();
        rtl.SetText(value, persianDigits: true);
        return t;
    }
    private static void Rect(RectTransform r, float ax, float ay, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = new Vector2(ax, ay); r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; }
    private static void RectStretch(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }
    private static void Stretch(RectTransform r) { RectStretch(r, Vector2.zero, Vector2.one); }
    private static void Set(Object target, string property, Object value) { var so = new SerializedObject(target); var p = so.FindProperty(property); if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); } }
    private static void Set(Object target, string property, Object[] values) { var so = new SerializedObject(target); var p = so.FindProperty(property); if (p == null) return; p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
}
