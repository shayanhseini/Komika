using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class BuildKomikaUI
{
    private const string IranSansTtfPath = "Assets/Font/IRANSans/IRANSansMobile-Bold.ttf";
    private const string IranSansAssetPath = "Assets/Font/IRANSans/IRANSans_SDF.asset";

    private static readonly Color Navy = new(0.03f, 0.065f, 0.105f, 0.94f);
    private static readonly Color Panel = new(0.055f, 0.115f, 0.17f, 0.97f);
    private static readonly Color Card = new(0.08f, 0.165f, 0.235f, 0.98f);
    private static readonly Color Cyan = new(0.14f, 0.90f, 1f, 1f);
    private static readonly Color Orange = new(1f, 0.56f, 0.14f, 1f);
    private static TMP_FontAsset font;

    public static TMP_FontAsset EnsureIranSansFont()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(IranSansAssetPath);
        if (existing != null
            && existing.atlasTexture != null
            && existing.material != null
            && existing.atlasPopulationMode == AtlasPopulationMode.Static
            && existing.atlasWidth >= 2048
            && !existing.getFontFeatures
            && existing.characterTable != null
            && existing.characterTable.Count > 200)
            return existing;

        Font ttf = AssetDatabase.LoadAssetAtPath<Font>(IranSansTtfPath);
        if (ttf == null)
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        if (existing != null)
            AssetDatabase.DeleteAsset(IranSansAssetPath);

        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(ttf, 54, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
        if (created == null)
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        created.name = "IRANSans_SDF";
        created.isMultiAtlasTexturesEnabled = false;
        created.getFontFeatures = false;
        if (created.atlasTexture != null) created.atlasTexture.name = "IRANSans_SDF Atlas";
        if (created.material != null) created.material.name = "IRANSans_SDF Material";

        ClearFontFeatures(created);

        AssetDatabase.CreateAsset(created, IranSansAssetPath);
        if (created.atlasTexture != null) AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
        if (created.material != null) AssetDatabase.AddObjectToAsset(created.material, created);

        // Bake ASCII, Persian alphabet, Persian digits & Presentation Forms into ONE static 2048x2048 SDF atlas
        var chars = new StringBuilder();
        for (int c = 0x0020; c <= 0x007E; c++) chars.Append((char)c);
        chars.Append('\u060C').Append('\u061B').Append('\u061F').Append('\u0621');
        for (int c = 0x0622; c <= 0x064A; c++) chars.Append((char)c);
        chars.Append('\u067E').Append('\u0686').Append('\u0698').Append('\u06A9').Append('\u06AF').Append('\u06CC');
        for (int c = 0x0660; c <= 0x0669; c++) chars.Append((char)c);
        for (int c = 0x06F0; c <= 0x06F9; c++) chars.Append((char)c);
        for (int c = 0xFB56; c <= 0xFB95; c++) chars.Append((char)c);
        for (int c = 0xFBFC; c <= 0xFBFF; c++) chars.Append((char)c);
        for (int c = 0xFE80; c <= 0xFEFC; c++) chars.Append((char)c);

        created.TryAddCharacters(chars.ToString(), out string missing);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning($"[BuildKomikaUI] Missing glyphs in IRANSans: {missing.Length}");

        // Strip logical OpenType GPOS/GSUB rules so visual-order Presentation Forms never jump or overlap!
        created.atlasPopulationMode = AtlasPopulationMode.Static;
        created.getFontFeatures = false;
        ClearFontFeatures(created);

        if (created.atlasTexture != null) EditorUtility.SetDirty(created.atlasTexture);
        if (created.material != null) EditorUtility.SetDirty(created.material);
        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static void ClearFontFeatures(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;
        if (fontAsset.fontFeatureTable != null)
            fontAsset.fontFeatureTable.glyphPairAdjustmentRecords?.Clear();
        var so = new SerializedObject(fontAsset);
        var clearDyn = so.FindProperty("m_ClearDynamicDataOnBuild");
        if (clearDyn != null) clearDyn.boolValue = false;
        var getFeat = so.FindProperty("m_GetFontFeatures");
        if (getFeat != null) getFeat.boolValue = false;
        var ft = so.FindProperty("m_FontFeatureTable");
        if (ft != null)
        {
            ft.FindPropertyRelative("m_GlyphPairAdjustmentRecords")?.ClearArray();
            ft.FindPropertyRelative("m_LigatureSubstitutionRecords")?.ClearArray();
            ft.FindPropertyRelative("m_MultipleSubstitutionRecords")?.ClearArray();
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        fontAsset.ReadFontAssetDefinition();
    }

    public static string Main()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return "ERROR: Canvas not found";

        font = EnsureIranSansFont();

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        foreach (string legacyName in new[] { "Gameplay UI", "Start Panel", "GameOver Panel" })
        {
            Transform legacy = canvas.transform.Find(legacyName);
            if (legacy != null) legacy.gameObject.SetActive(false);
        }

        Transform old = canvas.transform.Find("Komika UI");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        GameObject root = UIObject("Komika UI", canvas.transform);
        Stretch(root.GetComponent<RectTransform>());
        KomikaUIController controller = root.AddComponent<KomikaUIController>();

        GameObject menu = UIObject("Menu Extensions", root.transform);
        Stretch(menu.GetComponent<RectTransform>());
        GameObject characterPanel = BuildCharacterPanel(root.transform, out Material[] materials, out GameObject[] indicators);
        GameObject helpPanel = BuildHelpPanel(root.transform, controller);
        GameObject hud = UIObject("Subway HUD", root.transform);
        Stretch(hud.GetComponent<RectTransform>());

        Button characterButton = Button("Character Select Button", menu.transform, "انتخاب دونده", Cyan);
        Rect(characterButton.GetComponent<RectTransform>(), .5f, 0f, new Vector2(-245f, 52f), new Vector2(205f, 56f));
        UnityEventTools.AddPersistentListener(characterButton.onClick, controller.OpenCharacterPanel);

        Button helpButton = Button("How To Play Button", menu.transform, "راهنمای بازی", Cyan);
        Rect(helpButton.GetComponent<RectTransform>(), .5f, 0f, new Vector2(245f, 52f), new Vector2(205f, 56f));
        UnityEventTools.AddPersistentListener(helpButton.onClick, controller.OpenHowToPlayPanel);

        Renderer playerRenderer = GameObject.FindGameObjectsWithTag("Player").SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).FirstOrDefault();
        Set(controller, "characterPanel", characterPanel);
        Set(controller, "menuExtensions", menu);
        Set(controller, "howToPlayPanel", helpPanel);
        Set(controller, "gameplayHud", hud);
        Set(controller, "playerRenderer", playerRenderer);
        Set(controller, "characterMaterials", materials);
        Set(controller, "selectedIndicators", indicators);
        Set(controller, "gameManager", UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include));
        Set(controller, "saveManager", UnityEngine.Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include));

        characterPanel.SetActive(false); helpPanel.SetActive(false); hud.SetActive(false);
        EditorUtility.SetDirty(canvas.gameObject); EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        string carouselResult = UpgradeKomikaCarousel.Main();
        string flowResult = UpgradeKomikaUIFlow.Main();
        return $"Built Persian IRANSans 1280x720 UI: font={(font != null ? font.name : "null")}; {carouselResult}; {flowResult}";
    }

    private static GameObject BuildCharacterPanel(Transform parent, out Material[] materials, out GameObject[] indicators)
    {
        GameObject overlay = Image("Character Selection Panel", parent, new Color(0.01f, 0.025f, 0.045f, 0.85f));
        Stretch(overlay.GetComponent<RectTransform>());
        Color[] colors = { new(.1f,.85f,1f), new(1f,.42f,.12f), new(.66f,.25f,1f), new(.35f,1f,.3f), new(1f,.18f,.3f) };
        string[] names = { "CYAN", "ORANGE", "PURPLE", "LIME", "RED" };
        materials = colors.Select((c, i) => CharacterMaterial(c, names[i])).ToArray();
        indicators = new GameObject[5];
        return overlay;
    }

    private static GameObject BuildHelpPanel(Transform parent, KomikaUIController controller)
    {
        GameObject overlay = Image("How To Play Panel", parent, new Color(0.01f, 0.025f, 0.045f, 0.86f));
        Stretch(overlay.GetComponent<RectTransform>());
        GameObject window = Image("Window", overlay.transform, Panel);
        Rect(window.GetComponent<RectTransform>(), .5f, .5f, Vector2.zero, new Vector2(880f, 445f));

        GameObject accentBar = Image("Accent Top", window.transform, Cyan);
        var abr = accentBar.GetComponent<RectTransform>();
        abr.anchorMin = new Vector2(0, 1); abr.anchorMax = new Vector2(1, 1);
        abr.pivot = new Vector2(.5f, 1); abr.sizeDelta = new Vector2(0, 5f);

        Text("Title", window.transform, "راهنمای کنترل بازی", 30, Cyan, TextAlignmentOptions.Center, new Vector2(0f, 175f), new Vector2(560f, 52f));
        Text("Subtitle", window.transform, "کنترل‌های حرکت در خیابان‌های تهران", 16, new Color(0.82f, 0.9f, 1f, 0.95f), TextAlignmentOptions.Center, new Vector2(0f, 138f), new Vector2(560f, 30f));

        string[] headers = { "اسلاید زیر مانع", "پرش از مانع", "تغییر خط" };
        string[] keys = {
            "کلید S یا جهت پایین\nیا کشیدن انگشت به پایین",
            "کلید W یا بالا یا Space\nیا کشیدن انگشت به بالا",
            "کلیدهای A / D یا چپ و راست\nیا خم شدن به چپ و راست در دوربین"
        };
        Color[] cardColors = { Cyan, Orange, Cyan };
        for (int i = 0; i < 3; i++)
        {
            float cx = (i - 1) * 270f;
            GameObject row = Image("Control " + (i + 1), window.transform, Card);
            Rect(row.GetComponent<RectTransform>(), .5f, .5f, new Vector2(cx, 8f), new Vector2(246f, 190f));
            GameObject badge = Image("Badge", row.transform, cardColors[i]);
            Rect(badge.GetComponent<RectTransform>(), .5f, 1f, new Vector2(0f, -4f), new Vector2(246f, 5f));
            Text("Header", row.transform, headers[i], 21, cardColors[i], TextAlignmentOptions.Center, new Vector2(0f, 42f), new Vector2(226f, 38f));
            Text("Keys", row.transform, keys[i], 15, Color.white, TextAlignmentOptions.Center, new Vector2(0f, -24f), new Vector2(230f, 92f));
        }

        Text("Pause Hint", window.transform, "توقف بازی در هر لحظه با کلید P یا ESC", 15, new Color(0.75f, 0.85f, 0.95f), TextAlignmentOptions.Center, new Vector2(0f, -112f), new Vector2(540f, 30f));
        Button close = Button("Back Button", window.transform, "بازگشت", Orange);
        Rect(close.GetComponent<RectTransform>(), .5f, 0f, new Vector2(0f, 44f), new Vector2(230f, 54f));
        UnityEventTools.AddPersistentListener(close.onClick, controller.CloseOverlayPanels);
        return overlay;
    }

    private static Material CharacterMaterial(Color color, string name)
    {
        const string folder = "Assets/Art/KomikaGameplay/Characters";
        if (!AssetDatabase.IsValidFolder("Assets/Art/KomikaGameplay")) AssetDatabase.CreateFolder("Assets/Art", "KomikaGameplay");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/KomikaGameplay", "Characters");
        string path = $"{folder}/Character_{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color; m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Button Button(string name, Transform parent, string label, Color accent)
    {
        GameObject go = Image(name, parent, Navy);
        Button b = go.AddComponent<Button>();
        ColorBlock cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(.84f, 1f, 1f); cb.pressedColor = accent; b.colors = cb;
        GameObject trim = Image("Bottom Trim", go.transform, new Color(accent.r, accent.g, accent.b, 0.85f));
        RectTransform tr = trim.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f);
        tr.pivot = new Vector2(.5f, 0f); tr.sizeDelta = new Vector2(0f, 3f);
        TMP_Text t = Text("Label", go.transform, label, 19, accent, TextAlignmentOptions.Center, Vector2.zero, new Vector2(-18, -10));
        Stretch(t.rectTransform);
        return b;
    }

    private static GameObject Image(string name, Transform parent, Color color) { GameObject go = UIObject(name, parent); var image = go.AddComponent<Image>(); image.color = color; return go; }

    private static TMP_Text Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, Vector2 pos, Vector2 sizeDelta)
    {
        GameObject go = UIObject(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Normal;
        t.color = color;
        t.alignment = alignment;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        Rect(t.rectTransform, .5f, .5f, pos, sizeDelta);
        var rtl = go.AddComponent<RTLTextMeshPro>();
        rtl.SetText(value, persianDigits: true);
        return t;
    }

    private static GameObject UIObject(string name, Transform parent) { GameObject go = new(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
    private static void Rect(RectTransform r, float ax, float ay, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = new Vector2(ax, ay); r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; }
    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    private static void EnsureEventSystem() { var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None); if (systems.Length == 0) { new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); } for (int i = 1; i < systems.Length; i++) UnityEngine.Object.DestroyImmediate(systems[i].gameObject); }
    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value) { var so = new SerializedObject(target); var p = so.FindProperty(property); if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); } }
    private static void Set<T>(UnityEngine.Object target, string property, T[] values) where T : UnityEngine.Object { var so = new SerializedObject(target); var p = so.FindProperty(property); if (p != null && values != null) { p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); } }
}
