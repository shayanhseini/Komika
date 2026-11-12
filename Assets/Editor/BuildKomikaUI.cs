using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BuildKomikaUI
{
    private static readonly Color Navy = new(0.025f, 0.055f, 0.09f, 0.96f);
    private static readonly Color Panel = new(0.055f, 0.11f, 0.16f, 0.98f);
    private static readonly Color Cyan = new(0.12f, 0.9f, 1f, 1f);
    private static readonly Color Orange = new(1f, 0.55f, 0.12f, 1f);
    private static TMP_FontAsset font;

    public static string Main()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return "ERROR: Canvas not found";

        font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.matchWidthOrHeight = 0.5f;
        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        Transform legacyGameplay = canvas.transform.Find("Gameplay UI");
        if (legacyGameplay != null)
            foreach (string childName in new[] { "Coin_panel", "Speed_Panel", "Distance_Panel" })
            {
                Transform child = legacyGameplay.Find(childName);
                if (child != null) child.gameObject.SetActive(false);
            }

        Transform old = canvas.transform.Find("Komika UI");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        GameObject root = UIObject("Komika UI", canvas.transform);
        Stretch(root.GetComponent<RectTransform>());
        KomikaUIController controller = root.AddComponent<KomikaUIController>();

        GameObject menu = UIObject("Menu Extensions", root.transform);
        Stretch(menu.GetComponent<RectTransform>());
        GameObject characterPanel = BuildCharacterPanel(root.transform, controller, out Material[] materials, out GameObject[] indicators);
        GameObject helpPanel = BuildHelpPanel(root.transform, controller);
        GameObject hud = BuildHud(root.transform, out TMP_Text coins, out TMP_Text distance, out TMP_Text speed);

        Button characterButton = Button("Character Select Button", menu.transform, "CHARACTERS", Cyan);
        Rect(characterButton.GetComponent<RectTransform>(), .5f, 0f, new Vector2(0, 165), new Vector2(330, 72));
        UnityEventTools.AddPersistentListener(characterButton.onClick, controller.OpenCharacterPanel);
        Button helpButton = Button("How To Play Button", menu.transform, "HOW TO PLAY", Orange);
        Rect(helpButton.GetComponent<RectTransform>(), .5f, 0f, new Vector2(0, 80), new Vector2(330, 62));
        UnityEventTools.AddPersistentListener(helpButton.onClick, controller.OpenHowToPlayPanel);

        Renderer playerRenderer = GameObject.FindGameObjectsWithTag("Player").SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).FirstOrDefault();
        Set(controller, "characterPanel", characterPanel);
        Set(controller, "menuExtensions", menu);
        Set(controller, "howToPlayPanel", helpPanel);
        Set(controller, "gameplayHud", hud);
        Set(controller, "playerRenderer", playerRenderer);
        Set(controller, "characterMaterials", materials);
        Set(controller, "selectedIndicators", indicators);
        Set(controller, "coinText", coins);
        Set(controller, "distanceText", distance);
        Set(controller, "speedText", speed);
        Set(controller, "gameManager", UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include));
        Set(controller, "worldMovement", UnityEngine.Object.FindFirstObjectByType<WorldMovementManager>(FindObjectsInactive.Include));

        characterPanel.SetActive(false); helpPanel.SetActive(false); hud.SetActive(false);
        EditorUtility.SetDirty(canvas.gameObject); EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        string carouselResult = UpgradeKomikaCarousel.Main();
        return $"Built Komika UI: characterMaterials={materials.Length}, EventSystems={UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}; {carouselResult}";
    }

    private static GameObject BuildCharacterPanel(Transform parent, KomikaUIController controller, out Material[] materials, out GameObject[] indicators)
    {
        GameObject overlay = Image("Character Selection Panel", parent, new Color(0,0,0,.78f)); Stretch(overlay.GetComponent<RectTransform>());
        GameObject window = Image("Window", overlay.transform, Panel); Rect(window.GetComponent<RectTransform>(), .5f, .5f, Vector2.zero, new Vector2(650, 780));
        Text("Title", window.transform, "SELECT RUNNER", 42, Cyan, TextAlignmentOptions.Center, new Vector2(0, 325), new Vector2(580, 70));
        Text("Subtitle", window.transform, "Choose your color", 21, Color.white, TextAlignmentOptions.Center, new Vector2(0, 275), new Vector2(580, 40));

        Color[] colors = { new(.1f,.85f,1f), new(1f,.42f,.12f), new(.66f,.25f,1f), new(.35f,1f,.3f), new(1f,.18f,.3f) };
        string[] names = { "CYAN", "ORANGE", "PURPLE", "LIME", "RED" };
        materials = colors.Select((c,i) => CharacterMaterial(c, names[i])).ToArray();
        indicators = new GameObject[5];
        for (int i=0;i<5;i++)
        {
            int captured = i;
            float x = (i % 2 == 0 ? -155 : 155); float y = 155 - (i / 2) * 190;
            if (i == 4) x = 0;
            Button card = Button("Character " + names[i], window.transform, names[i], colors[i]);
            Rect(card.GetComponent<RectTransform>(), .5f, .5f, new Vector2(x,y), new Vector2(240,155));
            RectTransform cardLabel = card.transform.Find("Label").GetComponent<RectTransform>();
            Rect(cardLabel, .5f, 0f, new Vector2(0, 20), new Vector2(210, 30));
            UnityEventTools.AddIntPersistentListener(card.onClick, controller.SelectCharacter, captured);
            GameObject cube = Image("Cube Preview", card.transform, colors[i]); Rect(cube.GetComponent<RectTransform>(), .5f,.5f,new Vector2(0,24),new Vector2(72,72));
            GameObject shine = Image("Highlight", cube.transform, Color.Lerp(colors[i], Color.white,.45f));
            Rect(shine.GetComponent<RectTransform>(), 0f,1f,new Vector2(10,-10),new Vector2(32,14));
            indicators[i] = Image("Selected", card.transform, Cyan);
            Rect(indicators[i].GetComponent<RectTransform>(), .5f,0f,new Vector2(0,7),new Vector2(120,6));
        }
        Button close = Button("Back Button", window.transform, "BACK", Orange); Rect(close.GetComponent<RectTransform>(),.5f,0f,new Vector2(0,45),new Vector2(250,66));
        UnityEventTools.AddPersistentListener(close.onClick, controller.CloseOverlayPanels);
        return overlay;
    }

    private static GameObject BuildHelpPanel(Transform parent, KomikaUIController controller)
    {
        GameObject overlay = Image("How To Play Panel", parent, new Color(0,0,0,.78f)); Stretch(overlay.GetComponent<RectTransform>());
        GameObject window = Image("Window", overlay.transform, Panel); Rect(window.GetComponent<RectTransform>(),.5f,.5f,Vector2.zero,new Vector2(620,760));
        Text("Title", window.transform,"HOW TO PLAY",42,Cyan,TextAlignmentOptions.Center,new Vector2(0,300),new Vector2(560,70));
        string[] rows={"SWIPE LEFT / RIGHT\nCHANGE LANE","SWIPE UP\nJUMP","SWIPE DOWN\nSLIDE"};
        for(int i=0;i<3;i++) { GameObject row=Image("Control "+(i+1),window.transform,i==2?new Color(.12f,.23f,.29f,1):new Color(.08f,.17f,.23f,1)); Rect(row.GetComponent<RectTransform>(),.5f,.5f,new Vector2(0,155-i*155),new Vector2(520,125)); Text("Label",row.transform,rows[i],27,Color.white,TextAlignmentOptions.Center,Vector2.zero,new Vector2(480,105)); }
        Button close=Button("Back Button",window.transform,"BACK",Orange); Rect(close.GetComponent<RectTransform>(),.5f,0f,new Vector2(0,48),new Vector2(250,66)); UnityEventTools.AddPersistentListener(close.onClick,controller.CloseOverlayPanels);
        return overlay;
    }

    private static GameObject BuildHud(Transform parent, out TMP_Text coins, out TMP_Text distance, out TMP_Text speed)
    {
        GameObject hud=UIObject("Subway HUD",parent); Stretch(hud.GetComponent<RectTransform>());
        GameObject top=Image("Top Bar",hud.transform,new Color(.02f,.05f,.08f,.88f)); var tr=top.GetComponent<RectTransform>(); tr.anchorMin=new Vector2(0,1);tr.anchorMax=new Vector2(1,1);tr.pivot=new Vector2(.5f,1);tr.anchoredPosition=Vector2.zero;tr.sizeDelta=new Vector2(0,120);
        PauseController pause=UnityEngine.Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);
        Button pauseButton=Button("Pause Button",top.transform,"II",Cyan); Rect(pauseButton.GetComponent<RectTransform>(),0f,.5f,new Vector2(55,0),new Vector2(78,78)); if(pause!=null) UnityEventTools.AddPersistentListener(pauseButton.onClick,pause.TogglePause);
        coins=Text("Coins",top.transform,"0000 COINS",25,Orange,TextAlignmentOptions.Center,new Vector2(0,18),new Vector2(260,46));
        distance=Text("Distance",top.transform,"000 M",22,Color.white,TextAlignmentOptions.Center,new Vector2(0,-24),new Vector2(260,38));
        speed=Text("Speed",top.transform,"000 KM/H",21,Cyan,TextAlignmentOptions.Right,new Vector2(-28,0),new Vector2(220,60)); var sr=speed.rectTransform; sr.anchorMin=sr.anchorMax=new Vector2(1,.5f);sr.pivot=new Vector2(1,.5f);
        Text("Mission Badge",hud.transform,"RUN  •  SURVIVE  •  COLLECT",19,Color.white,TextAlignmentOptions.Center,new Vector2(0,-145),new Vector2(380,42)).rectTransform.anchorMin=new Vector2(.5f,1);
        return hud;
    }

    private static Material CharacterMaterial(Color color,string name)
    {
        const string folder="Assets/Art/KomikaGameplay/Characters";
        if(!AssetDatabase.IsValidFolder("Assets/Art/KomikaGameplay")) AssetDatabase.CreateFolder("Assets/Art","KomikaGameplay");
        if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/KomikaGameplay","Characters");
        string path=$"{folder}/Character_{name}.mat"; Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){ Shader shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); m=new Material(shader); AssetDatabase.CreateAsset(m,path); }
        m.color=color; m.SetColor("_BaseColor",color); EditorUtility.SetDirty(m); return m;
    }

    private static Button Button(string name,Transform parent,string label,Color accent)
    {
        GameObject go=Image(name,parent,Navy); Button b=go.AddComponent<Button>(); ColorBlock cb=b.colors; cb.normalColor=Color.white;cb.highlightedColor=new Color(.8f,1f,1f);cb.pressedColor=accent; b.colors=cb;
        Text("Label",go.transform,label,23,accent,TextAlignmentOptions.Center,Vector2.zero,new Vector2(-18,-10)); Stretch(go.transform.GetChild(0).GetComponent<RectTransform>()); return b;
    }
    private static GameObject Image(string name,Transform parent,Color color){GameObject go=UIObject(name,parent); var image=go.AddComponent<Image>();image.color=color;return go;}
    private static TMP_Text Text(string name,Transform parent,string value,float size,Color color,TextAlignmentOptions alignment,Vector2 pos,Vector2 sizeDelta){GameObject go=UIObject(name,parent);var t=go.AddComponent<TextMeshProUGUI>();t.text=value;t.fontSize=size;t.color=color;t.alignment=alignment;t.font=font;t.enableWordWrapping=false;Rect(t.rectTransform,.5f,.5f,pos,sizeDelta);return t;}
    private static GameObject UIObject(string name,Transform parent){GameObject go=new(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go;}
    private static void Rect(RectTransform r,float ax,float ay,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=new Vector2(ax,ay);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;}
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    private static void EnsureEventSystem(){var systems=UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None);if(systems.Length==0){GameObject go=new("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));} for(int i=1;i<systems.Length;i++) UnityEngine.Object.DestroyImmediate(systems[i].gameObject);}
    private static void Set(UnityEngine.Object target,string property,UnityEngine.Object value){var so=new SerializedObject(target);so.FindProperty(property).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Set(UnityEngine.Object target,string property,UnityEngine.Object[] values){var so=new SerializedObject(target);var p=so.FindProperty(property);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
}
