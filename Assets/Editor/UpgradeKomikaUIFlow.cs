using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class UpgradeKomikaUIFlow
{
    private static readonly Color Panel = new(0.05f, 0.105f, 0.155f, 0.98f);
    private static readonly Color Card = new(0.08f, 0.155f, 0.22f, 0.98f);
    private static readonly Color Cyan = new(0.14f, 0.90f, 1f, 1f);
    private static readonly Color Orange = new(1f, 0.56f, 0.14f, 1f);
    private static readonly Color Gold = new(1f, 0.82f, 0.25f, 1f);
    private static TMP_FontAsset font;

    public static string Main()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Transform root = canvas != null ? canvas.transform.Find("Komika UI") : null;
        if (root == null) return "ERROR: Komika UI not found";
        KomikaUIController controller = root.GetComponent<KomikaUIController>();
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        SaveManager saveManager = Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include);
        PauseController pauseController = Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);
        if (controller == null || gameManager == null || saveManager == null) return "ERROR: required runtime references not found";

        font = BuildKomikaUI.EnsureIranSansFont();

        GameObject menu = root.Find("Menu Extensions").gameObject;
        GameObject character = root.Find("Character Selection Panel").gameObject;
        GameObject help = root.Find("How To Play Panel").gameObject;
        GameObject hud = root.Find("Subway HUD").gameObject;
        CanvasGroup menuGroup = Group(menu); CanvasGroup characterGroup = Group(character);
        CanvasGroup helpGroup = Group(help); CanvasGroup hudGroup = Group(hud);

        ClearChildren(hud.transform);
        TMP_Text currentRecord = BuildLandscapeHud(hud.transform, controller, out TMP_Text hudBest, out TMP_Text hudCoin);

        GameObject pause = ReplacePanel(root, "Pause Menu Panel", new Color(0.01f, 0.025f, 0.045f, 0.88f));
        CanvasGroup pauseGroup = Group(pause);
        BuildLandscapePausePanel(pause.transform, controller);

        GameObject gameOver = ReplacePanel(root, "Komika Game Over Panel", new Color(0.012f, 0.028f, 0.05f, 0.92f));
        CanvasGroup gameOverGroup = Group(gameOver);
        BuildLandscapeGameOverPanel(gameOver.transform, controller, out TMP_Text finalRecord, out TMP_Text finalBest, out TMP_Text finalCoin);

        // Top-Left Persian Brand + Best Record Card (aligned cleanly left-to-right on left margin without overlapping hero!)
        Transform oldMainBest = menu.transform.Find("Main Best Record");
        if (oldMainBest != null) Object.DestroyImmediate(oldMainBest.gameObject);
        GameObject mainBestCard = Image("Main Best Record", menu.transform, new Color(.025f, .065f, .105f, .92f));
        Rect(mainBestCard.GetComponent<RectTransform>(), 0f, 1f, new Vector2(180f, -54f), new Vector2(310f, 80f));
        GameObject accent = Image("Left Accent", mainBestCard.transform, Cyan);
        var ar = accent.GetComponent<RectTransform>();
        ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, .5f); ar.sizeDelta = new Vector2(5f, 0f);
        Text("Brand", mainBestCard.transform, "کمیکا - تهران", 16, Orange, TextAlignmentOptions.Right, new Vector2(6f, 23f), new Vector2(276f, 24f));
        Text("Caption", mainBestCard.transform, "بهترین رکورد", 15, Cyan, TextAlignmentOptions.Right, new Vector2(6f, 0f), new Vector2(276f, 24f));
        TMP_Text mainBest = Text("Value", mainBestCard.transform, "۰ متر", 22, Color.white, TextAlignmentOptions.Right, new Vector2(6f, -23f), new Vector2(276f, 30f));

        CanvasGroup[] groups = { menuGroup, characterGroup, helpGroup, hudGroup, pauseGroup, gameOverGroup };
        Set(controller, "menuExtensions", menu); Set(controller, "characterPanel", character); Set(controller, "howToPlayPanel", help);
        Set(controller, "gameplayHud", hud); Set(controller, "pausePanel", pause); Set(controller, "gameOverPanel", gameOver);
        Set(controller, "menuGroup", menuGroup); Set(controller, "characterGroup", characterGroup); Set(controller, "howToPlayGroup", helpGroup);
        Set(controller, "hudGroup", hudGroup); Set(controller, "pauseGroup", pauseGroup); Set(controller, "gameOverGroup", gameOverGroup);
        Set(controller, "allPanelGroups", groups.Cast<Object>().ToArray());
        Set(controller, "currentRecordText", currentRecord); Set(controller, "hudBestRecordText", hudBest); Set(controller, "hudCoinText", hudCoin); Set(controller, "menuBestRecordText", mainBest);
        Set(controller, "gameOverRecordText", finalRecord); Set(controller, "gameOverBestRecordText", finalBest); Set(controller, "gameOverCoinText", finalCoin);
        Set(controller, "gameManager", gameManager); Set(controller, "saveManager", saveManager);
        if (pauseController != null) Set(pauseController, "uiController", controller);

        foreach (CanvasGroup group in groups) SetImmediate(group, group == menuGroup);
        DisableLegacyGameOver(canvas.transform);
        EditorUtility.SetDirty(root.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        return "Persian Landscape 1280x720 UI Flow ready: IRANSans + RTLTextMeshPro";
    }

    private static TMP_Text BuildLandscapeHud(Transform parent, KomikaUIController controller, out TMP_Text best, out TMP_Text coin)
    {
        Button pause = Button("Pause Button", parent, "II", Cyan);
        Rect(pause.GetComponent<RectTransform>(), 0f, 1f, new Vector2(46f, -44f), new Vector2(56f, 56f));
        UnityEventTools.AddPersistentListener(pause.onClick, controller.PauseGame);

        GameObject bar = Image("Record Bar", parent, new Color(.02f, .05f, .085f, .88f));
        Rect(bar.GetComponent<RectTransform>(), .5f, 1f, new Vector2(0f, -44f), new Vector2(560f, 68f));
        GameObject barAccent = Image("Top Accent", bar.transform, Cyan);
        var br = barAccent.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 1); br.anchorMax = new Vector2(1, 1);
        br.pivot = new Vector2(.5f, 1); br.sizeDelta = new Vector2(0f, 3f);

        Text("Best Caption", bar.transform, "بهترین رکورد", 14, Cyan, TextAlignmentOptions.Center, new Vector2(-180f, 16f), new Vector2(160f, 22f));
        best = Text("Best Record", bar.transform, "۰ متر", 22, Cyan, TextAlignmentOptions.Center, new Vector2(-180f, -12f), new Vector2(160f, 32f));

        Text("Coin Caption", bar.transform, "سکه", 14, Gold, TextAlignmentOptions.Center, new Vector2(0f, 16f), new Vector2(140f, 22f));
        coin = Text("Coin Counter", bar.transform, "۰", 22, Gold, TextAlignmentOptions.Center, new Vector2(0f, -12f), new Vector2(140f, 32f));

        Text("Current Caption", bar.transform, "مسافت", 14, new Color(0.88f, 0.94f, 1f), TextAlignmentOptions.Center, new Vector2(180f, 16f), new Vector2(160f, 22f));
        TMP_Text current = Text("Current Record", bar.transform, "۰ متر", 22, Orange, TextAlignmentOptions.Center, new Vector2(180f, -12f), new Vector2(160f, 32f));
        return current;
    }

    private static void BuildLandscapePausePanel(Transform parent, KomikaUIController controller)
    {
        GameObject window = Image("Pause Window", parent, Panel);
        Rect(window.GetComponent<RectTransform>(), .5f, .5f, Vector2.zero, new Vector2(470f, 430f));
        GameObject topBar = Image("Accent Top", window.transform, Cyan);
        var tr = topBar.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(.5f, 1); tr.sizeDelta = new Vector2(0, 5f);

        Text("Title", window.transform, "توقف بازی", 34, Cyan, TextAlignmentOptions.Center, new Vector2(0, 155f), new Vector2(400f, 54f));
        Text("Hint", window.transform, "کمی نفس تازه کن، دونده!", 15, Color.white, TextAlignmentOptions.Center, new Vector2(0, 114f), new Vector2(400f, 28f));

        Button resume = Button("Resume Button", window.transform, "ادامه بازی", Cyan);
        Rect(resume.GetComponent<RectTransform>(), .5f, .5f, new Vector2(0, 38f), new Vector2(320f, 62f));
        UnityEventTools.AddPersistentListener(resume.onClick, controller.ResumeGame);

        Button restart = Button("Restart Button", window.transform, "شروع مجدد", Orange);
        Rect(restart.GetComponent<RectTransform>(), .5f, .5f, new Vector2(0, -42f), new Vector2(320f, 58f));
        UnityEventTools.AddPersistentListener(restart.onClick, controller.RestartGame);

        Button menu = Button("Main Menu Button", window.transform, "منوی اصلی", Color.white);
        Rect(menu.GetComponent<RectTransform>(), .5f, .5f, new Vector2(0, -118f), new Vector2(320f, 58f));
        UnityEventTools.AddPersistentListener(menu.onClick, controller.ReturnToMainMenu);
    }

    private static void BuildLandscapeGameOverPanel(Transform parent, KomikaUIController controller, out TMP_Text current, out TMP_Text best, out TMP_Text coins)
    {
        GameObject window = Image("GameOver Window", parent, Panel);
        Rect(window.GetComponent<RectTransform>(), .5f, .5f, Vector2.zero, new Vector2(720f, 440f));
        GameObject topBar = Image("Accent Top", window.transform, Orange);
        var tr = topBar.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(.5f, 1); tr.sizeDelta = new Vector2(0, 5f);

        Text("Title", window.transform, "پایان مسیر", 38, Orange, TextAlignmentOptions.Center, new Vector2(0, 158f), new Vector2(500f, 58f));
        Text("Subtitle", window.transform, "خلاصه دویدن شما در خیابان‌های تهران", 16, Color.white, TextAlignmentOptions.Center, new Vector2(0, 118f), new Vector2(500f, 28f));

        // 3 horizontal Persian telemetry cards (RTL visual order: Coins left, Best middle, Distance right)
        GameObject coinCard = Image("Coin Card", window.transform, new Color(.14f, .12f, .05f, .98f));
        Rect(coinCard.GetComponent<RectTransform>(), .5f, .5f, new Vector2(-215f, 14f), new Vector2(200f, 130f));
        Text("Caption", coinCard.transform, "سکه‌ها", 16, Gold, TextAlignmentOptions.Center, new Vector2(0, 32f), new Vector2(180f, 28f));
        coins = Text("Value", coinCard.transform, "۰ سکه", 24, Gold, TextAlignmentOptions.Center, new Vector2(0, -14f), new Vector2(180f, 48f));

        GameObject bestCard = Image("Best Record Card", window.transform, Card);
        Rect(bestCard.GetComponent<RectTransform>(), .5f, .5f, new Vector2(0f, 14f), new Vector2(200f, 130f));
        Text("Caption", bestCard.transform, "بهترین رکورد", 16, Cyan, TextAlignmentOptions.Center, new Vector2(0, 32f), new Vector2(180f, 28f));
        best = Text("Value", bestCard.transform, "۰ متر", 28, Cyan, TextAlignmentOptions.Center, new Vector2(0, -14f), new Vector2(180f, 48f));

        GameObject currentCard = Image("Current Record Card", window.transform, Card);
        Rect(currentCard.GetComponent<RectTransform>(), .5f, .5f, new Vector2(215f, 14f), new Vector2(200f, 130f));
        Text("Caption", currentCard.transform, "مسافت طی‌شده", 16, Color.white, TextAlignmentOptions.Center, new Vector2(0, 32f), new Vector2(180f, 28f));
        current = Text("Value", currentCard.transform, "۰ متر", 28, Orange, TextAlignmentOptions.Center, new Vector2(0, -14f), new Vector2(180f, 48f));

        Button restart = Button("Restart Button", window.transform, "دویدن دوباره", Cyan);
        Rect(restart.GetComponent<RectTransform>(), .5f, 0f, new Vector2(145f, 58f), new Vector2(260f, 64f));
        UnityEventTools.AddPersistentListener(restart.onClick, controller.RestartGame);

        Button menu = Button("Main Menu Button", window.transform, "منوی اصلی", Orange);
        Rect(menu.GetComponent<RectTransform>(), .5f, 0f, new Vector2(-145f, 58f), new Vector2(260f, 64f));
        UnityEventTools.AddPersistentListener(menu.onClick, controller.ReturnToMainMenu);
    }

    private static void DisableLegacyGameOver(Transform canvas)
    {
        Transform legacy = canvas.Find("GameOver Panel");
        if (legacy != null) legacy.gameObject.SetActive(false);
        UIManager ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null) return;
        var so = new SerializedObject(ui);
        var p1 = so.FindProperty("gameOverPanel"); if (p1 != null) p1.objectReferenceValue = null;
        var p2 = so.FindProperty("gameOverCanvasGroup"); if (p2 != null) p2.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject ReplacePanel(Transform parent, string name, Color color) { Transform old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); GameObject go = Image(name, parent, color); Stretch(go.GetComponent<RectTransform>()); return go; }
    private static CanvasGroup Group(GameObject go) { CanvasGroup g = go.GetComponent<CanvasGroup>(); return g != null ? g : go.AddComponent<CanvasGroup>(); }
    private static void SetImmediate(CanvasGroup g, bool on) { g.alpha = on ? 1 : 0; g.interactable = on; g.blocksRaycasts = on; g.gameObject.SetActive(on); }
    private static void ClearChildren(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject); }
    private static Button Button(string name, Transform parent, string label, Color accent) { GameObject go = Image(name, parent, new Color(.025f, .06f, .095f, 1)); Button b = go.AddComponent<Button>(); ColorBlock cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(.84f, 1f, 1f); cb.pressedColor = accent; b.colors = cb; GameObject trim = Image("Accent Trim", go.transform, accent); RectTransform tr = trim.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(.5f, 0f); tr.sizeDelta = new Vector2(0f, 3f); TMP_Text t = Text("Label", go.transform, label, 20, accent, TextAlignmentOptions.Center, Vector2.zero, new Vector2(10, 10)); Stretch(t.rectTransform); return b; }
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
    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    private static void Set(Object target, string property, Object value) { var so = new SerializedObject(target); var p = so.FindProperty(property); if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); } }
    private static void Set(Object target, string property, Object[] values) { var so = new SerializedObject(target); var p = so.FindProperty(property); if (p == null) return; p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
}
