using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class UpgradeKomikaCarousel
{
    private static readonly Color Navy = new(0.018f, 0.04f, 0.065f, 1f);
    private static readonly Color Panel = new(0.04f, 0.09f, 0.13f, 1f);
    private static readonly Color Cyan = new(0.12f, 0.9f, 1f, 1f);
    private static readonly Color Orange = new(1f, 0.55f, 0.12f, 1f);
    private static TMP_FontAsset font;

    public static string Main()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Transform root = canvas != null ? canvas.transform.Find("Komika UI") : null;
        if (root == null) return "ERROR: Komika UI not found";

        KomikaUIController controller = root.GetComponent<KomikaUIController>();
        UIManager uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (controller == null || uiManager == null) return "ERROR: controller or UIManager not found";
        font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();

        AddCenterStartArea(root.Find("Menu Extensions"), uiManager);

        Transform oldPanel = root.Find("Character Selection Panel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);
        GameObject panel = BuildCarousel(root, controller, out RectTransform[] cards, out CanvasGroup[] groups, out GameObject[] dots);

        Set(controller, "characterPanel", panel);
        Set(controller, "characterCards", cards.Cast<Object>().ToArray());
        Set(controller, "characterCardGroups", groups.Cast<Object>().ToArray());
        Set(controller, "selectedIndicators", dots.Cast<Object>().ToArray());
        panel.SetActive(false);

        EditorUtility.SetDirty(root.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        return $"Upgraded menu: centerStart=True, carouselCards={cards.Length}, fullScreen=True";
    }

    private static void AddCenterStartArea(Transform menu, UIManager uiManager)
    {
        if (menu == null) return;
        Transform existing = menu.Find("Center Start Tap Area");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject go = Image("Center Start Tap Area", menu, new Color(0.02f, 0.06f, 0.09f, 0.12f));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.12f, 0.25f); rect.anchorMax = new Vector2(0.88f, 0.78f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Button button = go.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, uiManager.StartGame);
        Text("Prompt", go.transform, "TAP TO START", 30, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(420, 70));
        go.transform.SetAsFirstSibling();
    }

    private static GameObject BuildCarousel(Transform parent, KomikaUIController controller, out RectTransform[] cards, out CanvasGroup[] groups, out GameObject[] dots)
    {
        GameObject overlay = Image("Character Selection Panel", parent, Navy); Stretch(overlay.GetComponent<RectTransform>());
        CharacterCarouselSwipe swipe = overlay.AddComponent<CharacterCarouselSwipe>();
        Set(swipe, "controller", controller);

        GameObject top = Image("Top Fill", overlay.transform, new Color(.03f,.12f,.17f,1));
        RectStretch(top.GetComponent<RectTransform>(), new Vector2(0,.78f), Vector2.one);
        GameObject bottom = Image("Bottom Fill", overlay.transform, new Color(.025f,.075f,.11f,1));
        RectStretch(bottom.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1,.22f));

        TMP_Text title = Text("Title", overlay.transform, "SELECT RUNNER", 44, Cyan, TextAlignmentOptions.Center, Vector2.zero, new Vector2(620,75));
        Rect(title.rectTransform,.5f,1f,new Vector2(0,-95),new Vector2(620,75));
        TMP_Text hint = Text("Swipe Hint", overlay.transform, "SWIPE LEFT OR RIGHT", 20, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(520,42));
        Rect(hint.rectTransform,.5f,1f,new Vector2(0,-155),new Vector2(520,42));

        GameObject carousel = new("Horizontal Character Carousel", typeof(RectTransform));
        carousel.transform.SetParent(overlay.transform, false);
        Rect(carousel.GetComponent<RectTransform>(), .5f, .5f, new Vector2(0,15), new Vector2(720,680));

        Color[] colors = { new(.1f,.85f,1f), new(1f,.42f,.12f), new(.66f,.25f,1f), new(.35f,1f,.3f), new(1f,.18f,.3f) };
        string[] names = { "CYAN", "ORANGE", "PURPLE", "LIME", "RED" };
        cards = new RectTransform[5]; groups = new CanvasGroup[5]; dots = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            int captured = i;
            Button card = Button("Character " + names[i], carousel.transform, "", colors[i]);
            cards[i] = card.GetComponent<RectTransform>();
            Rect(cards[i], .5f, .5f, new Vector2(i * 430f, 0), new Vector2(390, 530));
            groups[i] = card.gameObject.AddComponent<CanvasGroup>();
            UnityEventTools.AddIntPersistentListener(card.onClick, controller.SelectCharacter, captured);
            GameObject glow = Image("Card Glow", card.transform, new Color(colors[i].r, colors[i].g, colors[i].b, .16f)); Stretch(glow.GetComponent<RectTransform>());
            GameObject cube = Image("Cube Preview", card.transform, colors[i]); Rect(cube.GetComponent<RectTransform>(),.5f,.5f,new Vector2(0,45),new Vector2(250,250));
            GameObject shine = Image("Highlight", cube.transform, Color.Lerp(colors[i],Color.white,.45f)); Rect(shine.GetComponent<RectTransform>(),0f,1f,new Vector2(45,-35),new Vector2(105,32));
            Text("Character Name", card.transform, names[i], 38, colors[i], TextAlignmentOptions.Center, new Vector2(0,-145), new Vector2(340,65));
            Text("Character Type", card.transform, "KOMIKA RUNNER", 18, Color.white, TextAlignmentOptions.Center, new Vector2(0,-195), new Vector2(300,38));
        }

        Button previous = Button("Previous Character", overlay.transform, "‹", Cyan); Rect(previous.GetComponent<RectTransform>(),0f,.5f,new Vector2(70,0),new Vector2(86,140)); UnityEventTools.AddPersistentListener(previous.onClick,controller.ShowPreviousCharacter);
        Button next = Button("Next Character", overlay.transform, "›", Cyan); Rect(next.GetComponent<RectTransform>(),1f,.5f,new Vector2(-70,0),new Vector2(86,140)); UnityEventTools.AddPersistentListener(next.onClick,controller.ShowNextCharacter);

        GameObject dotRow = new("Selection Dots",typeof(RectTransform)); dotRow.transform.SetParent(overlay.transform,false); Rect(dotRow.GetComponent<RectTransform>(),.5f,0f,new Vector2(0,155),new Vector2(260,30));
        for(int i=0;i<5;i++){ dots[i]=Image("Dot "+(i+1),dotRow.transform,Cyan); Rect(dots[i].GetComponent<RectTransform>(),.5f,.5f,new Vector2((i-2)*48,0),new Vector2(28,8)); }

        Button back = Button("Back Button", overlay.transform, "BACK", Orange); Rect(back.GetComponent<RectTransform>(),.5f,0f,new Vector2(0,70),new Vector2(270,72)); UnityEventTools.AddPersistentListener(back.onClick,controller.CloseOverlayPanels);
        return overlay;
    }

    private static Button Button(string name, Transform parent, string label, Color accent)
    {
        GameObject go = Image(name,parent,Panel); Button b=go.AddComponent<Button>();
        ColorBlock cb=b.colors; cb.normalColor=Color.white; cb.highlightedColor=new Color(.82f,1f,1f); cb.pressedColor=accent; b.colors=cb;
        if(!string.IsNullOrEmpty(label)){ TMP_Text t=Text("Label",go.transform,label,34,accent,TextAlignmentOptions.Center,Vector2.zero,new Vector2(20,20)); Stretch(t.rectTransform); }
        return b;
    }
    private static GameObject Image(string name,Transform parent,Color color){GameObject go=new(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);go.GetComponent<UnityEngine.UI.Image>().color=color;return go;}
    private static TMP_Text Text(string name,Transform parent,string value,float size,Color color,TextAlignmentOptions alignment,Vector2 pos,Vector2 sizeDelta){GameObject go=new(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();t.text=value;t.fontSize=size;t.color=color;t.alignment=alignment;t.font=font;t.raycastTarget=false;Rect(t.rectTransform,.5f,.5f,pos,sizeDelta);return t;}
    private static void Rect(RectTransform r,float ax,float ay,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=new Vector2(ax,ay);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;}
    private static void RectStretch(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    private static void Stretch(RectTransform r){RectStretch(r,Vector2.zero,Vector2.one);}
    private static void Set(Object target,string property,Object value){var so=new SerializedObject(target);so.FindProperty(property).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Set(Object target,string property,Object[] values){var so=new SerializedObject(target);var p=so.FindProperty(property);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
}
