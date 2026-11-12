using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class UpgradeKomikaUIFlow
{
    private static readonly Color Navy = new(0.018f, 0.04f, 0.065f, 0.98f);
    private static readonly Color Panel = new(0.045f, 0.095f, 0.14f, 0.98f);
    private static readonly Color Cyan = new(0.12f, 0.9f, 1f, 1f);
    private static readonly Color Orange = new(1f, 0.55f, 0.12f, 1f);
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

        font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();

        GameObject menu = root.Find("Menu Extensions").gameObject;
        GameObject character = root.Find("Character Selection Panel").gameObject;
        GameObject help = root.Find("How To Play Panel").gameObject;
        GameObject hud = root.Find("Subway HUD").gameObject;
        CanvasGroup menuGroup = Group(menu); CanvasGroup characterGroup = Group(character);
        CanvasGroup helpGroup = Group(help); CanvasGroup hudGroup = Group(hud);

        ClearChildren(hud.transform);
        TMP_Text currentRecord = BuildHud(hud.transform, controller, out TMP_Text hudBest);

        GameObject pause = ReplacePanel(root, "Pause Menu Panel", new Color(0.01f,0.025f,0.045f,.97f));
        CanvasGroup pauseGroup = Group(pause);
        BuildPausePanel(pause.transform, controller);

        GameObject gameOver = ReplacePanel(root, "Komika Game Over Panel", Navy);
        CanvasGroup gameOverGroup = Group(gameOver);
        BuildGameOverPanel(gameOver.transform, controller, out TMP_Text finalRecord, out TMP_Text finalBest);

        Transform oldMainBest = menu.transform.Find("Main Best Record");
        if (oldMainBest != null) Object.DestroyImmediate(oldMainBest.gameObject);
        GameObject mainBestCard = Image("Main Best Record", menu.transform, new Color(.02f,.065f,.095f,.9f));
        Rect(mainBestCard.GetComponent<RectTransform>(),.5f,1f,new Vector2(0,-205),new Vector2(330,82));
        Text("Caption",mainBestCard.transform,"BEST RECORD",17,Cyan,TextAlignmentOptions.Center,new Vector2(0,18),new Vector2(300,28));
        TMP_Text mainBest = Text("Value",mainBestCard.transform,"0000 M",28,Color.white,TextAlignmentOptions.Center,new Vector2(0,-14),new Vector2(300,38));

        CanvasGroup[] groups = { menuGroup, characterGroup, helpGroup, hudGroup, pauseGroup, gameOverGroup };
        Set(controller,"menuExtensions",menu); Set(controller,"characterPanel",character); Set(controller,"howToPlayPanel",help);
        Set(controller,"gameplayHud",hud); Set(controller,"pausePanel",pause); Set(controller,"gameOverPanel",gameOver);
        Set(controller,"menuGroup",menuGroup); Set(controller,"characterGroup",characterGroup); Set(controller,"howToPlayGroup",helpGroup);
        Set(controller,"hudGroup",hudGroup); Set(controller,"pauseGroup",pauseGroup); Set(controller,"gameOverGroup",gameOverGroup);
        Set(controller,"allPanelGroups",groups.Cast<Object>().ToArray());
        Set(controller,"currentRecordText",currentRecord); Set(controller,"hudBestRecordText",hudBest); Set(controller,"menuBestRecordText",mainBest);
        Set(controller,"gameOverRecordText",finalRecord); Set(controller,"gameOverBestRecordText",finalBest);
        Set(controller,"gameManager",gameManager); Set(controller,"saveManager",saveManager);
        if (pauseController != null) Set(pauseController,"uiController",controller);

        foreach (CanvasGroup group in groups) SetImmediate(group, group == menuGroup);
        DisableLegacyGameOver(canvas.transform, controller);
        EditorUtility.SetDirty(root.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        return "UI flow upgraded: Pause + Records + GameOver + 6 CanvasGroup transitions";
    }

    private static TMP_Text BuildHud(Transform parent, KomikaUIController controller, out TMP_Text best)
    {
        GameObject top = Image("Record Bar",parent,new Color(.015f,.04f,.06f,.94f));
        RectTransform tr=top.GetComponent<RectTransform>(); tr.anchorMin=new Vector2(0,1);tr.anchorMax=new Vector2(1,1);tr.pivot=new Vector2(.5f,1);tr.anchoredPosition=Vector2.zero;tr.sizeDelta=new Vector2(0,125);
        Button pause=Button("Pause Button",top.transform,"II",Cyan); Rect(pause.GetComponent<RectTransform>(),0f,.5f,new Vector2(58,0),new Vector2(78,78)); UnityEventTools.AddPersistentListener(pause.onClick,controller.PauseGame);
        Text("Current Caption",top.transform,"RECORD",16,Color.white,TextAlignmentOptions.Center,new Vector2(-105,22),new Vector2(210,28));
        TMP_Text current=Text("Current Record",top.transform,"0000 M",30,Orange,TextAlignmentOptions.Center,new Vector2(-105,-17),new Vector2(210,44));
        Text("Best Caption",top.transform,"BEST",16,Color.white,TextAlignmentOptions.Center,new Vector2(145,22),new Vector2(190,28));
        best=Text("Best Record",top.transform,"0000 M",30,Cyan,TextAlignmentOptions.Center,new Vector2(145,-17),new Vector2(190,44));
        return current;
    }

    private static void BuildPausePanel(Transform parent, KomikaUIController controller)
    {
        GameObject window=Image("Pause Window",parent,Panel); Rect(window.GetComponent<RectTransform>(),.5f,.5f,Vector2.zero,new Vector2(590,700));
        Text("Title",window.transform,"PAUSED",52,Cyan,TextAlignmentOptions.Center,new Vector2(0,245),new Vector2(520,80));
        Text("Hint",window.transform,"Take a breath, runner",20,Color.white,TextAlignmentOptions.Center,new Vector2(0,185),new Vector2(500,45));
        Button resume=Button("Resume Button",window.transform,"RESUME",Cyan); Rect(resume.GetComponent<RectTransform>(),.5f,.5f,new Vector2(0,60),new Vector2(390,82)); UnityEventTools.AddPersistentListener(resume.onClick,controller.ResumeGame);
        Button restart=Button("Restart Button",window.transform,"RESTART",Orange); Rect(restart.GetComponent<RectTransform>(),.5f,.5f,new Vector2(0,-50),new Vector2(390,76)); UnityEventTools.AddPersistentListener(restart.onClick,controller.RestartGame);
        Button menu=Button("Main Menu Button",window.transform,"MAIN MENU",Color.white); Rect(menu.GetComponent<RectTransform>(),.5f,.5f,new Vector2(0,-155),new Vector2(390,76)); UnityEventTools.AddPersistentListener(menu.onClick,controller.ReturnToMainMenu);
    }

    private static void BuildGameOverPanel(Transform parent, KomikaUIController controller, out TMP_Text current, out TMP_Text best)
    {
        Text("Title",parent,"RUN OVER",54,Orange,TextAlignmentOptions.Center,new Vector2(0,385),new Vector2(640,90));
        Text("Subtitle",parent,"YOUR TEHRAN RUN",20,Color.white,TextAlignmentOptions.Center,new Vector2(0,325),new Vector2(500,40));
        GameObject currentCard=Image("Current Record Card",parent,Panel); Rect(currentCard.GetComponent<RectTransform>(),.5f,.5f,new Vector2(0,125),new Vector2(560,175));
        Text("Caption",currentCard.transform,"RECORD",20,Color.white,TextAlignmentOptions.Center,new Vector2(0,42),new Vector2(500,35));
        current=Text("Value",currentCard.transform,"0000 M",48,Orange,TextAlignmentOptions.Center,new Vector2(0,-18),new Vector2(500,70));
        GameObject bestCard=Image("Best Record Card",parent,new Color(.035f,.13f,.16f,1)); Rect(bestCard.GetComponent<RectTransform>(),.5f,.5f,new Vector2(0,-70),new Vector2(560,150));
        Text("Caption",bestCard.transform,"BEST RECORD",18,Cyan,TextAlignmentOptions.Center,new Vector2(0,32),new Vector2(500,32));
        best=Text("Value",bestCard.transform,"0000 M",40,Cyan,TextAlignmentOptions.Center,new Vector2(0,-20),new Vector2(500,60));
        Button restart=Button("Restart Button",parent,"RUN AGAIN",Cyan); Rect(restart.GetComponent<RectTransform>(),.5f,0f,new Vector2(0,190),new Vector2(390,82)); UnityEventTools.AddPersistentListener(restart.onClick,controller.RestartGame);
        Button menu=Button("Main Menu Button",parent,"MAIN MENU",Orange); Rect(menu.GetComponent<RectTransform>(),.5f,0f,new Vector2(0,90),new Vector2(390,72)); UnityEventTools.AddPersistentListener(menu.onClick,controller.ReturnToMainMenu);
    }

    private static void DisableLegacyGameOver(Transform canvas, KomikaUIController controller)
    {
        Transform legacy=canvas.Find("GameOver Panel"); if(legacy!=null) legacy.gameObject.SetActive(false);
        UIManager ui=Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if(ui==null)return; var so=new SerializedObject(ui); so.FindProperty("gameOverPanel").objectReferenceValue=null; so.FindProperty("gameOverCanvasGroup").objectReferenceValue=null; so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject ReplacePanel(Transform parent,string name,Color color){Transform old=parent.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);GameObject go=Image(name,parent,color);Stretch(go.GetComponent<RectTransform>());return go;}
    private static CanvasGroup Group(GameObject go){CanvasGroup g=go.GetComponent<CanvasGroup>();return g!=null?g:go.AddComponent<CanvasGroup>();}
    private static void SetImmediate(CanvasGroup g,bool on){g.alpha=on?1:0;g.interactable=on;g.blocksRaycasts=on;g.gameObject.SetActive(on);}
    private static void ClearChildren(Transform parent){for(int i=parent.childCount-1;i>=0;i--)Object.DestroyImmediate(parent.GetChild(i).gameObject);}
    private static Button Button(string name,Transform parent,string label,Color accent){GameObject go=Image(name,parent,new Color(.02f,.055f,.08f,1));Button b=go.AddComponent<Button>();ColorBlock cb=b.colors;cb.normalColor=Color.white;cb.highlightedColor=new Color(.8f,1f,1f);cb.pressedColor=accent;b.colors=cb;TMP_Text t=Text("Label",go.transform,label,26,accent,TextAlignmentOptions.Center,Vector2.zero,new Vector2(10,10));Stretch(t.rectTransform);return b;}
    private static GameObject Image(string name,Transform parent,Color color){GameObject go=new(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);go.GetComponent<UnityEngine.UI.Image>().color=color;return go;}
    private static TMP_Text Text(string name,Transform parent,string value,float size,Color color,TextAlignmentOptions alignment,Vector2 pos,Vector2 sizeDelta){GameObject go=new(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();t.text=value;t.fontSize=size;t.color=color;t.alignment=alignment;t.font=font;t.raycastTarget=false;Rect(t.rectTransform,.5f,.5f,pos,sizeDelta);return t;}
    private static void Rect(RectTransform r,float ax,float ay,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=new Vector2(ax,ay);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;}
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    private static void Set(Object target,string property,Object value){var so=new SerializedObject(target);var p=so.FindProperty(property);if(p!=null){p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}}
    private static void Set(Object target,string property,Object[] values){var so=new SerializedObject(target);var p=so.FindProperty(property);if(p==null)return;p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
}
