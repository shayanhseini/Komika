using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SetupKamicaPlayerCharacter
{
    private const string CharacterFolder = "Assets/Models/Player/Character";
    private const string FbxPath = CharacterFolder + "/Character_Model.fbx";
    private const string TexturePath = CharacterFolder + "/texture_pbr_20250901.png";
    private const string NormalPath = CharacterFolder + "/texture_pbr_20250901_normal.png";
    private const string MaterialPath = CharacterFolder + "/Character_Material.mat";
    private const string ControllerPath = CharacterFolder + "/Character_Animator.controller";
    private const string ScenePath = "Assets/Scenes/GameScene.unity";

    // Setup can be run via MenuItem("Komika/Setup Player Character")

    [MenuItem("Komika/Setup Player Character")]
    public static void ManualRun()
    {
        ExecuteSetup(silent: false);
    }

    public static string ExecuteSetup(bool silent = false)
    {
        if (!File.Exists(FbxPath))
        {
            if (!silent) Debug.LogWarning($"[SetupKamicaPlayer] FBX not found at {FbxPath}");
            return "FBX not found";
        }

        // 1. Setup Material
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(litShader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else
        {
            mat.shader = litShader;
        }

        Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
        mat.SetColor("_BaseColor", Color.white);
        if (normalMap != null)
        {
            mat.SetTexture("_BumpMap", normalMap);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.35f);
        mat.SetFloat("_Metallic", 0.0f);
        EditorUtility.SetDirty(mat);

        // 2. Configure Model Importer for FBX animations with exact integer frame ranges and looping
        ModelImporter fbxImporter = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (fbxImporter != null)
        {
            fbxImporter.animationType = ModelImporterAnimationType.Generic;

            // Idle: 60 frames (0 to 59 at 30fps)
            var idleClip = new ModelImporterClipAnimation
            {
                name = "Idle_Cycle",
                takeName = "Character_Rig|Idle_Cycle",
                firstFrame = 0f,
                lastFrame = 59f,
                loopTime = true,
                wrapMode = WrapMode.Loop
            };

            // Run: 17 frames (0 to 16 at 30fps)
            var runClip = new ModelImporterClipAnimation
            {
                name = "Run_Cycle",
                takeName = "Character_Rig|Run_Cycle",
                firstFrame = 0f,
                lastFrame = 16f,
                loopTime = true,
                wrapMode = WrapMode.Loop
            };

            fbxImporter.clipAnimations = new[] { idleClip, runClip };
            fbxImporter.SaveAndReimport();
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[SetupKamicaPlayer] Configured FBX clips: Idle_Cycle (0-59 frames, loop=true) and Run_Cycle (0-16 frames, loop=true).");
        }

        // 3. Load the resulting AnimationClip assets from FBX
        var allClips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();

        AnimationClip idleAsset = allClips.FirstOrDefault(c => c.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0);
        AnimationClip runAsset = allClips.FirstOrDefault(c => c.name.IndexOf("Run", System.StringComparison.OrdinalIgnoreCase) >= 0);

        if (idleAsset == null && allClips.Count > 0) idleAsset = allClips[0];
        if (runAsset == null && allClips.Count > 1) runAsset = allClips[1];

        // 4. Create / Configure Animator Controller
        AnimatorController animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (animController == null)
        {
            animController = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        // Ensure "IsRunning" parameter exists
        if (!animController.parameters.Any(p => p.name == "IsRunning"))
        {
            animController.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        }

        if (animController.layers != null && animController.layers.Length > 0)
        {
            var sm = animController.layers[0].stateMachine;

            // Find or create Idle state
            AnimatorState idleState = sm.states.FirstOrDefault(s => s.state != null && s.state.name == "Idle").state;
            if (idleState == null)
            {
                idleState = sm.AddState("Idle", new Vector3(250, 0, 0));
            }
            idleState.motion = idleAsset;
            idleState.speed = 1f;

            // Find or create Run state
            AnimatorState runState = sm.states.FirstOrDefault(s => s.state != null && s.state.name == "Run").state;
            if (runState == null)
            {
                runState = sm.AddState("Run", new Vector3(250, 100, 0));
            }
            runState.motion = runAsset;
            runState.speed = 1f;

            // Make Idle the default state!
            sm.defaultState = idleState;

            // Reconstruct transitions cleanly
            idleState.transitions = new AnimatorStateTransition[0];
            runState.transitions = new AnimatorStateTransition[0];

            // Idle -> Run transition (when IsRunning is true)
            var idleToRun = idleState.AddTransition(runState);
            idleToRun.hasExitTime = false;
            idleToRun.hasFixedDuration = true;
            idleToRun.duration = 0.15f;
            idleToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

            // Run -> Idle transition (when IsRunning is false)
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.hasFixedDuration = true;
            runToIdle.duration = 0.2f;
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            EditorUtility.SetDirty(animController);
            Debug.Log($"[SetupKamicaPlayer] Configured Animator Controller: Idle -> {idleAsset?.name}, Run -> {runAsset?.name}");
        }

        // 5. Setup Scene
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            player = GameObject.Find("Player");

        if (player == null)
        {
            Debug.LogError("[SetupKamicaPlayer] Player GameObject not found in scene!");
            return "Player not found";
        }

        // Hide old Cube if present
        Transform oldCube = player.transform.Find("Cube");
        if (oldCube != null)
        {
            oldCube.gameObject.SetActive(false);
        }

        // Re-instantiate Character_Visual to bind cleanly to newly imported FBX
        Transform existingVisual = player.transform.Find("Character_Visual");
        if (existingVisual != null)
        {
            Object.DestroyImmediate(existingVisual.gameObject);
        }

        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxPrefab == null)
        {
            Debug.LogError($"[SetupKamicaPlayer] Failed to load prefab at {FbxPath}");
            return "Prefab load failed";
        }

        GameObject characterInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, player.transform);
        if (characterInstance == null)
        {
            characterInstance = Object.Instantiate(fbxPrefab, player.transform);
        }

        characterInstance.name = "Character_Visual";
        characterInstance.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        characterInstance.transform.localRotation = Quaternion.identity;
        characterInstance.transform.localScale = Vector3.one;

        // Apply material to all renderers
        Renderer primaryRenderer = null;
        foreach (Renderer r in characterInstance.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = mat;
            if (primaryRenderer == null && r is SkinnedMeshRenderer)
                primaryRenderer = r;
        }
        if (primaryRenderer == null)
            primaryRenderer = characterInstance.GetComponentInChildren<Renderer>(true);

        // Configure Animator
        Animator animator = characterInstance.GetComponent<Animator>();
        if (animator == null)
            animator = characterInstance.AddComponent<Animator>();
        animator.runtimeAnimatorController = animController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Setup PlayerAnimationController on Player
        PlayerAnimationController animCtrl = player.GetComponent<PlayerAnimationController>();
        if (animCtrl == null)
            animCtrl = player.AddComponent<PlayerAnimationController>();

        SerializedObject animSo = new SerializedObject(animCtrl);
        animSo.FindProperty("animator").objectReferenceValue = animator;
        animSo.FindProperty("gameManager").objectReferenceValue = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        animSo.FindProperty("worldMovement").objectReferenceValue = Object.FindFirstObjectByType<WorldMovementManager>(FindObjectsInactive.Include);
        animSo.ApplyModifiedPropertiesWithoutUndo();

        // Update PlayerController slideVisual
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            SerializedObject pcSo = new SerializedObject(playerController);
            var slideProp = pcSo.FindProperty("slideVisual");
            if (slideProp != null)
                slideProp.objectReferenceValue = characterInstance.transform;
            pcSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Update KomikaUIController playerRenderer
        KomikaUIController uiController = Object.FindFirstObjectByType<KomikaUIController>(FindObjectsInactive.Include);
        if (uiController != null && primaryRenderer != null)
        {
            SerializedObject uiSo = new SerializedObject(uiController);
            var rendProp = uiSo.FindProperty("playerRenderer");
            if (rendProp != null)
                rendProp.objectReferenceValue = primaryRenderer;
            var matsProp = uiSo.FindProperty("characterMaterials");
            if (matsProp != null && matsProp.arraySize > 0)
                matsProp.GetArrayElementAtIndex(0).objectReferenceValue = mat;
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // 6. Setup Front Idle Camera and Smooth Arc Transition to Behind
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraTransitionController camTrans = mainCam.GetComponent<CameraTransitionController>();
            if (camTrans == null)
                camTrans = mainCam.gameObject.AddComponent<CameraTransitionController>();

            GameObject idlePointObj = GameObject.Find("Camera Idle Transform");
            if (idlePointObj == null)
            {
                idlePointObj = new GameObject("Camera Idle Transform");
            }
            Vector3 frontPos = new Vector3(0.0f, 0.45f, -90.50f);
            Quaternion frontRot = Quaternion.Euler(4.0f, 180f, 0f);
            idlePointObj.transform.position = frontPos;
            idlePointObj.transform.rotation = frontRot;

            GameObject gameplayPointObj = GameObject.Find("Camera Game Transform");

            SerializedObject camTransSo = new SerializedObject(camTrans);
            camTransSo.FindProperty("idlePoint").objectReferenceValue = idlePointObj.transform;
            if (gameplayPointObj != null)
                camTransSo.FindProperty("gameplayPoint").objectReferenceValue = gameplayPointObj.transform;
            camTransSo.FindProperty("idlePosition").vector3Value = frontPos;
            camTransSo.FindProperty("idleEulerAngles").vector3Value = new Vector3(4.0f, 180f, 0f);
            camTransSo.FindProperty("moveDuration").floatValue = 1.6f;
            camTransSo.FindProperty("arcSideAmount").floatValue = 2.6f;
            camTransSo.FindProperty("gameStartProgress").floatValue = 0.70f;
            camTransSo.ApplyModifiedPropertiesWithoutUndo();

            // Place Main Camera in front Idle position
            mainCam.transform.position = frontPos;
            mainCam.transform.rotation = frontRot;

            PlayerCameraController pcCam = mainCam.GetComponent<PlayerCameraController>();
            if (pcCam != null)
                pcCam.enabled = false;

            EditorUtility.SetDirty(mainCam.gameObject);
            EditorUtility.SetDirty(idlePointObj);
            Debug.Log("[SetupKamicaPlayer] Configured Front Idle Camera at (0, 0.45, -90.50) with arc swing to behind!");
        }

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        string msg = "[SetupKamicaPlayer] SUCCESS! Character, Animations (Idle & Run), and Front-to-Behind Camera Transition fully configured.";
        Debug.Log(msg);
        return msg;
    }
}
