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
    private const string FastRunAnimPath = CharacterFolder + "/Fast_Run.anim";
    private const string TexturePath = CharacterFolder + "/texture_pbr_20250901.png";
    private const string NormalPath = CharacterFolder + "/texture_pbr_20250901_normal.png";
    private const string MaterialPath = CharacterFolder + "/Character_Material.mat";
    private const string ControllerPath = CharacterFolder + "/Character_Animator.controller";
    private const string ScenePath = "Assets/Scenes/GameScene.unity";

    static SetupKamicaPlayerCharacter()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool("Komika_SetupChar_v2", false))
                return;
            SessionState.SetBool("Komika_SetupChar_v2", true);
            ExecuteSetup(silent: true);
        };
    }

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

        // 2. Configure Model Importer for FBX animations (4 Idles, Run, and 42-frame Jump)
        ModelImporter fbxImporter = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (fbxImporter != null)
        {
            fbxImporter.animationType = ModelImporterAnimationType.Generic;

            // Determine take names from imported takes
            string idleCycleTake = "Character_Rig|Idle_Cycle";
            string idleBounceTake = "Character_Rig|Idle_Bounce";
            string idleLookAroundTake = "Character_Rig|Idle_LookAround";
            string idleCheckWatchTake = "Character_Rig|Idle_CheckWatch";
            string runTake = "Character_Rig|Run_Cycle";
            string jumpTake = "Character_Rig|Jump";
            string moveLeftTake = "Character_Rig|Move_Left";
            string moveRightTake = "Character_Rig|Move_Right";
            string slideTake = "Character_Rig|Slide";

            var defaultClips = fbxImporter.defaultClipAnimations;
            if (defaultClips != null)
            {
                foreach (var dc in defaultClips)
                {
                    string tName = dc.takeName ?? dc.name;
                    if (tName.Equals("Character_Rig|Idle_Cycle", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("Idle_Cycle", System.StringComparison.OrdinalIgnoreCase))
                        idleCycleTake = tName;
                    else if (tName.Equals("Character_Rig|Idle_Bounce", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("Idle_Bounce", System.StringComparison.OrdinalIgnoreCase))
                        idleBounceTake = tName;
                    else if (tName.Equals("Character_Rig|Idle_LookAround", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("Idle_LookAround", System.StringComparison.OrdinalIgnoreCase))
                        idleLookAroundTake = tName;
                    else if (tName.Equals("Character_Rig|Idle_CheckWatch", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("Idle_CheckWatch", System.StringComparison.OrdinalIgnoreCase))
                        idleCheckWatchTake = tName;
                    else if (tName.Equals("Character_Rig|Run_Cycle", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("Run_Cycle", System.StringComparison.OrdinalIgnoreCase))
                        runTake = tName;
                    else if (tName.Equals("Character_Rig|Jump", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("|Jump", System.StringComparison.OrdinalIgnoreCase) || tName == "Jump")
                        jumpTake = tName;
                    else if (tName.Equals("Character_Rig|Move_Left", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("Move_Left", System.StringComparison.OrdinalIgnoreCase))
                        moveLeftTake = tName;
                    else if (tName.Equals("Character_Rig|Move_Right", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("Move_Right", System.StringComparison.OrdinalIgnoreCase))
                        moveRightTake = tName;
                    else if (tName.Equals("Character_Rig|Slide", System.StringComparison.OrdinalIgnoreCase) || tName.EndsWith("|Slide", System.StringComparison.OrdinalIgnoreCase) || tName == "Slide")
                        slideTake = tName;
                }
            }

            // 1. Idle_Cycle: 61 frames (0 to 60 at 30fps = 2.0s)
            var idleClip = new ModelImporterClipAnimation
            {
                name = "Idle_Cycle",
                takeName = idleCycleTake,
                firstFrame = 0f,
                lastFrame = 60f,
                loopTime = true,
                wrapMode = WrapMode.Loop
            };

            // 2. Idle_Bounce: 61 frames (0 to 60 at 30fps = 2.0s)
            var idleBounceClip = new ModelImporterClipAnimation
            {
                name = "Idle_Bounce",
                takeName = idleBounceTake,
                firstFrame = 0f,
                lastFrame = 60f,
                loopTime = true,
                wrapMode = WrapMode.Loop
            };

            // 3. Idle_LookAround: 121 frames (0 to 120 at 30fps = 4.0s)
            var idleLookAroundClip = new ModelImporterClipAnimation
            {
                name = "Idle_LookAround",
                takeName = idleLookAroundTake,
                firstFrame = 0f,
                lastFrame = 120f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            // 4. Idle_CheckWatch: 121 frames (0 to 120 at 30fps = 4.0s)
            var idleCheckWatchClip = new ModelImporterClipAnimation
            {
                name = "Idle_CheckWatch",
                takeName = idleCheckWatchTake,
                firstFrame = 0f,
                lastFrame = 120f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            // Run: 17 frames (0 to 16 at 30fps)
            var runClip = new ModelImporterClipAnimation
            {
                name = "Run_Cycle",
                takeName = runTake,
                firstFrame = 0f,
                lastFrame = 16f,
                loopTime = true,
                wrapMode = WrapMode.Loop
            };

            // Jump: 42 frames (0 to 41 at 30fps)
            // Frames 0 to 20 (21 frames): Ascent to apex
            // Frames 21 to 41 (21 frames): Descent to landing
            var jumpClip = new ModelImporterClipAnimation
            {
                name = "Jump",
                takeName = jumpTake,
                firstFrame = 0f,
                lastFrame = 41f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            var jumpUpClip = new ModelImporterClipAnimation
            {
                name = "Jump_Up",
                takeName = jumpTake,
                firstFrame = 0f,
                lastFrame = 20f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            var jumpDownClip = new ModelImporterClipAnimation
            {
                name = "Jump_Down",
                takeName = jumpTake,
                firstFrame = 21f,
                lastFrame = 41f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            // Move Left: 17 frames (0 to 16 at 30fps)
            var moveLeftClip = new ModelImporterClipAnimation
            {
                name = "Move_Left",
                takeName = moveLeftTake,
                firstFrame = 0f,
                lastFrame = 16f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            // Move Right: 17 frames (0 to 16 at 30fps)
            var moveRightClip = new ModelImporterClipAnimation
            {
                name = "Move_Right",
                takeName = moveRightTake,
                firstFrame = 0f,
                lastFrame = 16f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            // Slide: 49 frames (0 to 48 at 30fps)
            var slideClip = new ModelImporterClipAnimation
            {
                name = "Slide",
                takeName = slideTake,
                firstFrame = 0f,
                lastFrame = 48f,
                loopTime = false,
                wrapMode = WrapMode.ClampForever
            };

            fbxImporter.clipAnimations = new[] {
                idleClip,
                idleBounceClip,
                idleLookAroundClip,
                idleCheckWatchClip,
                runClip,
                jumpClip,
                jumpUpClip,
                jumpDownClip,
                moveLeftClip,
                moveRightClip,
                slideClip
            };
            fbxImporter.SaveAndReimport();
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[SetupKamicaPlayer] Configured FBX clips: 4 Idles, Run_Cycle, Jump (42 frames), Move_Left, Move_Right, Slide (49 frames).");
        }

        // 3. Load AnimationClip assets from FBX
        var allClips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();

        AnimationClip idleAsset = allClips.FirstOrDefault(c => c.name.Equals("Idle_Cycle", System.StringComparison.OrdinalIgnoreCase) || c.name.Equals("Idle", System.StringComparison.OrdinalIgnoreCase));
        AnimationClip idleBounceAsset = allClips.FirstOrDefault(c => c.name.Equals("Idle_Bounce", System.StringComparison.OrdinalIgnoreCase));
        AnimationClip idleLookAroundAsset = allClips.FirstOrDefault(c => c.name.Equals("Idle_LookAround", System.StringComparison.OrdinalIgnoreCase));
        AnimationClip idleCheckWatchAsset = allClips.FirstOrDefault(c => c.name.Equals("Idle_CheckWatch", System.StringComparison.OrdinalIgnoreCase));
        AnimationClip runAsset = allClips.FirstOrDefault(c => c.name.Equals("Run_Cycle", System.StringComparison.OrdinalIgnoreCase));
        if (runAsset == null)
            runAsset = allClips.FirstOrDefault(c => c.name.Equals("Run", System.StringComparison.OrdinalIgnoreCase));

        AnimationClip fastRunAsset = BuildFastRunFromRunClip(runAsset, idleAsset);
        AnimationClip jumpAsset = allClips.FirstOrDefault(c => c.name.Equals("Jump", System.StringComparison.OrdinalIgnoreCase));
        if (jumpAsset == null)
            jumpAsset = allClips.FirstOrDefault(c => c.name.IndexOf("Jump", System.StringComparison.OrdinalIgnoreCase) >= 0);
        AnimationClip moveLeftAsset = allClips.FirstOrDefault(c => c.name.Equals("Move_Left", System.StringComparison.OrdinalIgnoreCase));
        AnimationClip moveRightAsset = allClips.FirstOrDefault(c => c.name.Equals("Move_Right", System.StringComparison.OrdinalIgnoreCase));
        AnimationClip slideAsset = allClips.FirstOrDefault(c => c.name.Equals("Slide", System.StringComparison.OrdinalIgnoreCase));

        // 4. Create / Configure Animator Controller
        AnimatorController animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (animController == null)
        {
            animController = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        // Ensure Parameters exist
        EnsureParameter(animController, "IsRunning", AnimatorControllerParameterType.Bool);
        EnsureParameter(animController, "RunBlend", AnimatorControllerParameterType.Float, 0.0f);
        EnsureParameter(animController, "IsJumping", AnimatorControllerParameterType.Bool);
        EnsureParameter(animController, "IsSliding", AnimatorControllerParameterType.Bool);
        EnsureParameter(animController, "JumpSpeed", AnimatorControllerParameterType.Float, 1.0f);
        EnsureParameter(animController, "SlideSpeed", AnimatorControllerParameterType.Float, 1.0f);
        EnsureParameter(animController, "JumpProgress", AnimatorControllerParameterType.Float, 0.0f);

        if (animController.layers != null && animController.layers.Length > 0)
        {
            var sm = animController.layers[0].stateMachine;

            // Helper to get or create state
            AnimatorState GetOrCreateState(string name, Vector3 pos)
            {
                var existing = sm.states.FirstOrDefault(s => s.state != null && s.state.name == name).state;
                if (existing == null)
                {
                    existing = sm.AddState(name, pos);
                }
                return existing;
            }

            // 1. Idle States
            AnimatorState idleState = GetOrCreateState("Idle", new Vector3(200, 0, 0));
            idleState.motion = idleAsset;
            idleState.speed = 1f;

            AnimatorState idleBounceState = GetOrCreateState("Idle_Bounce", new Vector3(450, 0, 0));
            idleBounceState.motion = idleBounceAsset;
            idleBounceState.speed = 1f;

            AnimatorState idleLookAroundState = GetOrCreateState("Idle_LookAround", new Vector3(700, 0, 0));
            idleLookAroundState.motion = idleLookAroundAsset;
            idleLookAroundState.speed = 1f;

            AnimatorState idleCheckWatchState = GetOrCreateState("Idle_CheckWatch", new Vector3(950, 0, 0));
            idleCheckWatchState.motion = idleCheckWatchAsset;
            idleCheckWatchState.speed = 1f;

            // 2. Action States
            // Clean any prior embedded BlendTree subassets before adding updated RunLocomotion BlendTree
            foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (subAsset is BlendTree bt)
                    Object.DestroyImmediate(bt, true);
            }

            AnimatorState runState = GetOrCreateState("Run", new Vector3(550, 200, 0));
            if (fastRunAsset != null)
            {
                var runTree = new BlendTree
                {
                    name = "RunLocomotion",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "RunBlend",
                    useAutomaticThresholds = false
                };
                AssetDatabase.AddObjectToAsset(runTree, animController);
                runTree.AddChild(runAsset, 0f);
                runTree.AddChild(fastRunAsset, 1f);
                runState.motion = runTree;
            }
            else
            {
                runState.motion = runAsset;
            }
            runState.speed = 1f;

            AnimatorState jumpState = GetOrCreateState("Jump", new Vector3(550, 350, 0));
            jumpState.motion = jumpAsset;
            jumpState.speed = 1f;
            jumpState.speedParameterActive = true;
            jumpState.speedParameter = "JumpSpeed";

            AnimatorState slideState = GetOrCreateState("Slide", new Vector3(550, 80, 0));
            slideState.motion = slideAsset;
            slideState.speed = 1f;
            slideState.speedParameterActive = true;
            slideState.speedParameter = "SlideSpeed";

            AnimatorState moveLeftState = GetOrCreateState("Move_Left", new Vector3(300, 350, 0));
            moveLeftState.motion = moveLeftAsset;
            moveLeftState.speed = 1.35f;

            AnimatorState moveRightState = GetOrCreateState("Move_Right", new Vector3(800, 350, 0));
            moveRightState.motion = moveRightAsset;
            moveRightState.speed = 1.35f;

            // Default state is Idle
            sm.defaultState = idleState;

            // Clear existing transitions to rebuild cleanly
            idleState.transitions = new AnimatorStateTransition[0];
            idleBounceState.transitions = new AnimatorStateTransition[0];
            idleLookAroundState.transitions = new AnimatorStateTransition[0];
            idleCheckWatchState.transitions = new AnimatorStateTransition[0];
            runState.transitions = new AnimatorStateTransition[0];
            jumpState.transitions = new AnimatorStateTransition[0];
            slideState.transitions = new AnimatorStateTransition[0];
            moveLeftState.transitions = new AnimatorStateTransition[0];
            moveRightState.transitions = new AnimatorStateTransition[0];

            // --- A. Connected Idle Sequence (Home screen / Waiting loop) ---
            // Idle -> Idle_Bounce
            var t1 = idleState.AddTransition(idleBounceState);
            t1.hasExitTime = true;
            t1.exitTime = 0.95f;
            t1.hasFixedDuration = true;
            t1.duration = 0.25f;
            t1.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // Idle_Bounce -> Idle_LookAround
            var t2 = idleBounceState.AddTransition(idleLookAroundState);
            t2.hasExitTime = true;
            t2.exitTime = 0.95f;
            t2.hasFixedDuration = true;
            t2.duration = 0.25f;
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // Idle_LookAround -> Idle_CheckWatch
            var t3 = idleLookAroundState.AddTransition(idleCheckWatchState);
            t3.hasExitTime = true;
            t3.exitTime = 0.95f;
            t3.hasFixedDuration = true;
            t3.duration = 0.25f;
            t3.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // Idle_CheckWatch -> Idle (loop back)
            var t4 = idleCheckWatchState.AddTransition(idleState);
            t4.hasExitTime = true;
            t4.exitTime = 0.95f;
            t4.hasFixedDuration = true;
            t4.duration = 0.25f;
            t4.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // --- B. Immediate transitions to Run and Jump from ALL 4 Idle States ---
            AnimatorState[] allIdleStates = new[] { idleState, idleBounceState, idleLookAroundState, idleCheckWatchState };
            foreach (var idleS in allIdleStates)
            {
                // To Run when game starts
                var idleToRun = idleS.AddTransition(runState);
                idleToRun.hasExitTime = false;
                idleToRun.hasFixedDuration = true;
                idleToRun.duration = 0.15f;
                idleToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

                // To Jump if jumping from idle
                var idleToJump = idleS.AddTransition(jumpState);
                idleToJump.hasExitTime = false;
                idleToJump.hasFixedDuration = true;
                idleToJump.duration = 0.05f;
                idleToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            }

            // --- C. Run State transitions ---
            // Run -> Idle (when game ends / stops)
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.hasFixedDuration = true;
            runToIdle.duration = 0.20f;
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // Run -> Jump (when player jumps while running)
            var runToJump = runState.AddTransition(jumpState);
            runToJump.hasExitTime = false;
            runToJump.hasFixedDuration = true;
            runToJump.duration = 0.05f;
            runToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");

            // --- D. Jump State transitions ---
            // Jump -> Run (landing back into run)
            var jumpToRun = jumpState.AddTransition(runState);
            jumpToRun.hasExitTime = false;
            jumpToRun.hasFixedDuration = true;
            jumpToRun.duration = 0.12f;
            jumpToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

            // Jump -> Idle (landing if not running)
            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.hasExitTime = false;
            jumpToIdle.hasFixedDuration = true;
            jumpToIdle.duration = 0.15f;
            jumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            jumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // --- E. Lane Change transitions ---
            // Move_Left -> Run or Idle (smooth exit depending on locomotion state)
            var leftToRun = moveLeftState.AddTransition(runState);
            leftToRun.hasExitTime = true;
            leftToRun.exitTime = 0.85f;
            leftToRun.hasFixedDuration = true;
            leftToRun.duration = 0.12f;
            leftToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

            var leftToIdle = moveLeftState.AddTransition(idleState);
            leftToIdle.hasExitTime = true;
            leftToIdle.exitTime = 0.85f;
            leftToIdle.hasFixedDuration = true;
            leftToIdle.duration = 0.14f;
            leftToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            var leftToJump = moveLeftState.AddTransition(jumpState);
            leftToJump.hasExitTime = false;
            leftToJump.hasFixedDuration = true;
            leftToJump.duration = 0.05f;
            leftToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");

            // Move_Right -> Run or Idle (smooth exit depending on locomotion state)
            var rightToRun = moveRightState.AddTransition(runState);
            rightToRun.hasExitTime = true;
            rightToRun.exitTime = 0.85f;
            rightToRun.hasFixedDuration = true;
            rightToRun.duration = 0.12f;
            rightToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

            var rightToIdle = moveRightState.AddTransition(idleState);
            rightToIdle.hasExitTime = true;
            rightToIdle.exitTime = 0.85f;
            rightToIdle.hasFixedDuration = true;
            rightToIdle.duration = 0.14f;
            rightToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            var rightToJump = moveRightState.AddTransition(jumpState);
            rightToJump.hasExitTime = false;
            rightToJump.hasFixedDuration = true;
            rightToJump.duration = 0.05f;
            rightToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");

            // --- F. Slide transitions ---
            // Run -> Slide
            var runToSlide = runState.AddTransition(slideState);
            runToSlide.hasExitTime = false;
            runToSlide.hasFixedDuration = true;
            runToSlide.duration = 0.08f;
            runToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");

            // Slide -> Run
            var slideToRun = slideState.AddTransition(runState);
            slideToRun.hasExitTime = false;
            slideToRun.hasFixedDuration = true;
            slideToRun.duration = 0.15f;
            slideToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");

            // Slide -> Jump (Jump out of slide)
            var slideToJump = slideState.AddTransition(jumpState);
            slideToJump.hasExitTime = false;
            slideToJump.hasFixedDuration = true;
            slideToJump.duration = 0.05f;
            slideToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");

            // Jump -> Slide (Dive down)
            var jumpToSlide = jumpState.AddTransition(slideState);
            jumpToSlide.hasExitTime = false;
            jumpToSlide.hasFixedDuration = true;
            jumpToSlide.duration = 0.08f;
            jumpToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");

            // Slide -> Idle (in case player stops running while sliding)
            var slideToIdle = slideState.AddTransition(idleState);
            slideToIdle.hasExitTime = false;
            slideToIdle.hasFixedDuration = true;
            slideToIdle.duration = 0.20f;
            slideToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
            slideToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");

            EditorUtility.SetDirty(animController);
            Debug.Log($"[SetupKamicaPlayer] Configured Animator Controller: 4 Connected Idles -> Run -> Jump -> Lane Shift -> Slide fully wired.");
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
        GameObject characterInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, player.transform);
        characterInstance.name = "Character_Visual";
        characterInstance.transform.localPosition = new Vector3(0f, -0.47f, 0f);
        characterInstance.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        characterInstance.transform.localScale = Vector3.one * 1.6f;

        // Apply material
        var renderers = characterInstance.GetComponentsInChildren<Renderer>(true);
        Renderer primaryRenderer = null;
        foreach (var r in renderers)
        {
            if (r.gameObject.name.IndexOf("Mesh", System.StringComparison.OrdinalIgnoreCase) >= 0 || primaryRenderer == null)
                primaryRenderer = r;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        // Setup Animator
        Animator animator = characterInstance.GetComponent<Animator>();
        if (animator == null)
            animator = characterInstance.AddComponent<Animator>();
        animator.runtimeAnimatorController = animController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Setup PlayerController
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            SerializedObject pcSo = new SerializedObject(playerController);
            var slideProp = pcSo.FindProperty("slideVisual");
            if (slideProp != null)
                slideProp.objectReferenceValue = characterInstance.transform;
            pcSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Setup PlayerAnimationController on Player
        PlayerAnimationController animCtrl = player.GetComponent<PlayerAnimationController>();
        if (animCtrl == null)
            animCtrl = player.AddComponent<PlayerAnimationController>();

        SerializedObject animSo = new SerializedObject(animCtrl);
        animSo.FindProperty("animator").objectReferenceValue = animator;
        animSo.FindProperty("gameManager").objectReferenceValue = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        animSo.FindProperty("worldMovement").objectReferenceValue = Object.FindFirstObjectByType<WorldMovementManager>(FindObjectsInactive.Include);
        var pcProp = animSo.FindProperty("playerController");
        if (pcProp != null)
            pcProp.objectReferenceValue = playerController;
        animSo.ApplyModifiedPropertiesWithoutUndo();

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
        }

        EditorUtility.SetDirty(player);
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        string msg = "[SetupKamicaPlayer] SUCCESS! Character, Animations (Idle, Run, Jump synced), and Scene fully configured.";
        Debug.Log(msg);
        return msg;
    }

    private static AnimationClip BuildFastRunFromRunClip(AnimationClip runClip, AnimationClip idleClip)
    {
        if (runClip == null) return null;

        float srcLen = Mathf.Max(0.1f, runClip.length);
        float dstLen = 0.38f; // Athletic high-tempo sprint stride (~30% faster cadence than normal jog)
        int sampleCount = 17; // 16 intervals, exact closed loop frame[0] == frame[16]

        var bindings = AnimationUtility.GetCurveBindings(runClip);
        var byPath = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        foreach (var b in bindings)
        {
            if (b.type != typeof(Transform)) continue;
            if (!byPath.TryGetValue(b.path, out var map))
            {
                map = new Dictionary<string, AnimationCurve>();
                byPath[b.path] = map;
            }
            map[b.propertyName] = AnimationUtility.GetEditorCurve(runClip, b);
        }

        var idleByPath = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        if (idleClip != null)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(idleClip))
            {
                if (b.type != typeof(Transform)) continue;
                if (!idleByPath.TryGetValue(b.path, out var map))
                {
                    map = new Dictionary<string, AnimationCurve>();
                    idleByPath[b.path] = map;
                }
                map[b.propertyName] = AnimationUtility.GetEditorCurve(idleClip, b);
            }
        }

        static Quaternion SampleQuat(Dictionary<string, AnimationCurve> map, float time)
        {
            if (map == null
                || !map.TryGetValue("m_LocalRotation.x", out var cx)
                || !map.TryGetValue("m_LocalRotation.y", out var cy)
                || !map.TryGetValue("m_LocalRotation.z", out var cz)
                || !map.TryGetValue("m_LocalRotation.w", out var cw))
                return Quaternion.identity;
            var q = new Quaternion(cx.Evaluate(time), cy.Evaluate(time), cz.Evaluate(time), cw.Evaluate(time));
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return mag > 1e-6f ? new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag) : Quaternion.identity;
        }

        static Quaternion MeanQuat(Dictionary<string, AnimationCurve> map, float len)
        {
            if (map == null || !map.ContainsKey("m_LocalRotation.w")) return Quaternion.identity;
            Quaternion refQ = SampleQuat(map, 0f);
            Vector4 sum = Vector4.zero;
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                Quaternion q = SampleQuat(map, (i / (float)n) * len);
                if (Quaternion.Dot(q, refQ) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                sum += new Vector4(q.x, q.y, q.z, q.w);
            }
            return sum.sqrMagnitude > 1e-8f ? new Quaternion(sum.x, sum.y, sum.z, sum.w).normalized : refQ;
        }

        float idleLen = idleClip != null ? Mathf.Max(0.1f, idleClip.length) : 1f;
        Quaternion ComputeLeanDelta(string boneSuffix, float boostWeight)
        {
            string runKey = byPath.Keys.FirstOrDefault(p => p.EndsWith("/" + boneSuffix) || p == boneSuffix);
            string idleKey = idleByPath.Keys.FirstOrDefault(p => p.EndsWith("/" + boneSuffix) || p == boneSuffix);
            if (runKey == null || idleKey == null) return Quaternion.identity;
            Quaternion qRun = MeanQuat(byPath[runKey], srcLen);
            Quaternion qIdle = MeanQuat(idleByPath[idleKey], idleLen);
            Quaternion deltaFromIdle = qRun * Quaternion.Inverse(qIdle);
            return Quaternion.SlerpUnclamped(Quaternion.identity, deltaFromIdle, boostWeight);
        }

        Quaternion spineLean = ComputeLeanDelta("Spine", 1.15f);
        Quaternion chestLean = ComputeLeanDelta("Chest", 1.10f);
        Quaternion headCounter = Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, chestLean * spineLean, 0.72f));

        AnimationClip clip = new AnimationClip { name = "Fast_Run", frameRate = 30f };

        foreach (var kv in byPath)
        {
            string path = kv.Key;
            var map = kv.Value;
            string leaf = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;

            // Scale positions slightly on Hips for athletic sprint bounce
            if (map.TryGetValue("m_LocalPosition.x", out var px)
                && map.TryGetValue("m_LocalPosition.y", out var py)
                && map.TryGetValue("m_LocalPosition.z", out var pz))
            {
                bool isHips = leaf == "Hips";
                var nPx = new AnimationCurve();
                var nPy = new AnimationCurve();
                var nPz = new AnimationCurve();
                float meanY = 0f;
                for (int i = 0; i < sampleCount - 1; i++) meanY += py.Evaluate((i / 16f) * srcLen);
                meanY /= 16f;
                for (int i = 0; i < sampleCount; i++)
                {
                    float u = i / 16f;
                    float st = u * srcLen;
                    float dt = u * dstLen;
                    float y = py.Evaluate(st);
                    if (isHips) y = meanY + (y - meanY) * 1.45f - 0.015f;
                    nPx.AddKey(new Keyframe(dt, px.Evaluate(st)));
                    nPy.AddKey(new Keyframe(dt, y));
                    nPz.AddKey(new Keyframe(dt, pz.Evaluate(st)));
                }
                clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", nPx);
                clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", nPy);
                clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", nPz);
            }

            // Amplify rotational stride/swing cleanly around each bone's own average pose
            if (map.ContainsKey("m_LocalRotation.x") && map.ContainsKey("m_LocalRotation.w"))
            {
                float amp = 1.0f;
                Quaternion extra = Quaternion.identity;

                if (leaf.StartsWith("UpperLeg.") || leaf.StartsWith("UpperArm.")) amp = 1.42f;
                else if (leaf.StartsWith("LowerLeg.") || leaf.StartsWith("LowerArm.")) amp = 1.35f;
                else if (leaf.StartsWith("Foot.") || leaf.StartsWith("Toes.")) amp = 1.25f;
                else if (leaf.StartsWith("Cape_") || leaf.StartsWith("HairTail") || leaf.StartsWith("Coat_Front")) amp = 1.48f;
                else if (leaf == "Spine") { amp = 1.30f; extra = spineLean; }
                else if (leaf == "Chest" || leaf == "UpperChest") { amp = 1.30f; extra = chestLean; }
                else if (leaf == "Head") { amp = 1.15f; extra = headCounter; }

                Quaternion meanQ = MeanQuat(map, srcLen);
                var qx = new AnimationCurve();
                var qy = new AnimationCurve();
                var qz = new AnimationCurve();
                var qw = new AnimationCurve();
                Quaternion prevQ = Quaternion.identity;

                for (int i = 0; i < sampleCount; i++)
                {
                    float u = i / 16f;
                    float st = (i == sampleCount - 1) ? 0f : u * srcLen;
                    float dt = u * dstLen;
                    Quaternion qOrig = SampleQuat(map, st);
                    if (Quaternion.Dot(qOrig, meanQ) < 0f)
                        qOrig = new Quaternion(-qOrig.x, -qOrig.y, -qOrig.z, -qOrig.w);

                    Quaternion q = amp == 1.0f ? qOrig : Quaternion.SlerpUnclamped(meanQ, qOrig, amp);
                    q = (extra * q).normalized;
                    if (i > 0 && Quaternion.Dot(q, prevQ) < 0f)
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    prevQ = q;

                    qx.AddKey(new Keyframe(dt, q.x));
                    qy.AddKey(new Keyframe(dt, q.y));
                    qz.AddKey(new Keyframe(dt, q.z));
                    qw.AddKey(new Keyframe(dt, q.w));
                }

                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", qx);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", qy);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", qz);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", qw);
            }
        }

        clip.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(runClip);
        settings.loopTime = true;
        settings.loopBlend = true;
        settings.startTime = 0f;
        settings.stopTime = dstLen;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(FastRunAnimPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(clip);
            return existing;
        }
        AssetDatabase.CreateAsset(clip, FastRunAnimPath);
        return clip;
    }

    private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type, float defaultValue = 0f)
    {
        var existing = controller.parameters.FirstOrDefault(p => p.name == paramName);
        if (existing == null)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = paramName,
                type = type,
                defaultFloat = defaultValue,
                defaultBool = false,
                defaultInt = 0
            });
        }
    }
}
