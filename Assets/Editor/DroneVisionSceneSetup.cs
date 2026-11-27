using System;
using System.IO;
using DroneVision;
using DroneVision.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DroneVisionSceneSetup
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Kamica/CV/Setup DroneVision & Verify Lean Core")]
    public static void SetupAndVerify()
    {
        SetupKamicaPlayerCharacter.ExecuteSetup(silent: true);
        SetupSceneQuiet();
        VerifyAnimatorAndAudioWiring();
        VerifyTrackerLeanCore();
        RenderFastRunCheckShot();
        Debug.Log("[DroneVisionSceneSetup] Setup & lean + jog + FastRun + audio verification SUCCEEDED.");
    }

    public static void SetupSceneQuiet()
    {
        WebGLOptimizer.ApplyOptimizations();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        bool openedTarget = false;
        if (SceneManager.GetActiveScene().path != GameScenePath && File.Exists(GameScenePath))
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            openedTarget = true;
        }

        Scene scene = SceneManager.GetActiveScene();
        DroneVisionInput[] existing = UnityEngine.Object.FindObjectsByType<DroneVisionInput>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        DroneVisionInput activeInput = null;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] == null) continue;
            if (activeInput == null && existing[i].transform.parent == null)
            {
                activeInput = existing[i];
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
            }
        }

        if (activeInput == null)
        {
            GameObject go = new GameObject("DroneVision");
            SceneManager.MoveGameObjectToScene(go, scene);
            activeInput = go.AddComponent<DroneVisionInput>();
        }

        activeInput.gameObject.name = "DroneVision";
        activeInput.model = "auto";
        activeInput.numPoses = 1;
        activeInput.maxFps = 30;
        activeInput.cameraWidth = 960;
        activeInput.cameraHeight = 540;
        activeInput.focusCrop = false;

        activeInput.autoPreview = true;
        activeInput.previewMode = PreviewMode.Lobby;
        activeInput.previewCorner = PreviewCorner.BottomLeft;
        activeInput.previewLabels = true;
        activeInput.statusBox = true;
        activeInput.lockedShowSeconds = 0.7f;

        activeInput.uprightLeanMode = true;
        activeInput.rollEnter = 0.24f;
        activeInput.leanCooldownMs = 340f;
        activeInput.leanNeutralRearmMs = 110f;
        activeInput.enableJogDetection = true;
        activeInput.jogSprintHz = 2.45f;
        activeInput.lockMs = 350f;
        activeInput.lockGraceMs = 1200f;
        activeInput.invert = false;
        activeInput.stageZone = 0.8f;
        activeInput.minPlayerSize = 0.04f;
        activeInput.keyboardFallback = true;

        var worldMove = UnityEngine.Object.FindFirstObjectByType<WorldMovementManager>(FindObjectsInactive.Include);
        var gameMgr = UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        var playerCtrl = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        var diffMgr = UnityEngine.Object.FindFirstObjectByType<DifficultyManager>(FindObjectsInactive.Include);
        if (playerCtrl != null && worldMove != null)
        {
            var pcSo = new SerializedObject(playerCtrl);
            var wmProp = pcSo.FindProperty("worldMovement");
            if (wmProp != null) wmProp.objectReferenceValue = worldMove;
            pcSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerCtrl);
        }
        if (diffMgr != null && worldMove != null)
        {
            var dmSo = new SerializedObject(diffMgr);
            var wmProp = dmSo.FindProperty("worldMovement");
            if (wmProp != null) wmProp.objectReferenceValue = worldMove;
            dmSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(diffMgr);
        }

        // Wire Footsteps and LaneShift SFX onto GameAudioController
        var audioCtrl = UnityEngine.Object.FindFirstObjectByType<GameAudioController>(FindObjectsInactive.Include);
        if (audioCtrl != null)
        {
            AudioClip footstepsClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/Player/Running_Footsteps.mp3");
            AudioClip laneShiftClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/Player/LaneShift.wav");

            AudioSource EnsureAudioChild(string childName, AudioClip clip, bool loop)
            {
                Transform t = audioCtrl.transform.Find(childName);
                GameObject child = t != null ? t.gameObject : new GameObject(childName);
                if (t == null) child.transform.SetParent(audioCtrl.transform, false);
                AudioSource src = child.GetComponent<AudioSource>();
                if (src == null) src = child.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = loop;
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                EditorUtility.SetDirty(src);
                return src;
            }

            AudioSource footstepsSrc = EnsureAudioChild("FootstepsAudio", footstepsClip, true);
            AudioSource laneShiftSrc = EnsureAudioChild("LaneShiftAudio", laneShiftClip, false);

            var audioSo = new SerializedObject(audioCtrl);
            audioSo.FindProperty("footstepsSource").objectReferenceValue = footstepsSrc;
            audioSo.FindProperty("footstepsClip").objectReferenceValue = footstepsClip;
            audioSo.FindProperty("laneShiftSource").objectReferenceValue = laneShiftSrc;
            audioSo.FindProperty("laneShiftClip").objectReferenceValue = laneShiftClip;
            audioSo.FindProperty("gameManager").objectReferenceValue = gameMgr;
            audioSo.FindProperty("worldMovement").objectReferenceValue = worldMove;
            audioSo.FindProperty("playerController").objectReferenceValue = playerCtrl;
            audioSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioCtrl);
        }

        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas != null)
        {
            Transform key3 = canvas.transform.Find("Komika UI/How To Play Panel/Window/Control 3/Keys");
            if (key3 != null)
            {
                var rtl = key3.GetComponent<RTLTextMeshPro>();
                if (rtl != null)
                {
                    rtl.SetText("کلیدهای A / D یا چپ و راست\nیا خم شدن به چپ و راست در دوربین", persianDigits: true);
                    EditorUtility.SetDirty(rtl);
                }
            }
        }

        EditorUtility.SetDirty(activeInput);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        if (openedTarget)
            Debug.Log("[DroneVisionSceneSetup] Opened & saved GameScene with root DroneVision, SFX & FastRun BlendTree.");
    }

    private static PoseFrame MakePose(double leanRight, bool includeHips, double wristUp = -1.2, double jogBob = 0.0, double kneeSwing = 0.0, double armSwing = 0.0)
    {
        var f = new PoseFrame { Aspect = 16.0 / 9.0 };
        double cx = 0.5, sy = 0.4 + jogBob, sw = 0.1, swh = sw * f.Aspect;
        Landmark[] lm = f.Landmarks;
        for (int i = 0; i < PoseFrame.Count; i++) lm[i] = new Landmark(cx, 0.5, 0);

        double sx = cx - leanRight * sw * 0.55;
        lm[PoseFrame.Nose] = new Landmark(cx - leanRight * sw * 0.85, sy - swh * 0.8, 0.99);
        lm[PoseFrame.LShoulder] = new Landmark(sx + sw / 2, sy - leanRight * swh * 0.12, 0.99);
        lm[PoseFrame.RShoulder] = new Landmark(sx - sw / 2, sy + leanRight * swh * 0.12, 0.99);
        lm[PoseFrame.LElbow] = new Landmark(sx + sw * 0.60, sy + swh * 0.65 - armSwing, 0.95);
        lm[PoseFrame.RElbow] = new Landmark(sx - sw * 0.60, sy + swh * 0.65 + armSwing, 0.95);
        if (includeHips)
        {
            lm[PoseFrame.LHip] = new Landmark(cx + sw * 0.4, sy + swh * 1.45, 0.97);
            lm[PoseFrame.RHip] = new Landmark(cx - sw * 0.4, sy + swh * 1.45, 0.97);
            lm[PoseFrame.LKnee] = new Landmark(cx + sw * 0.42, sy + swh * 2.45 - Math.Max(0.0, kneeSwing), 0.95);
            lm[PoseFrame.RKnee] = new Landmark(cx - sw * 0.42, sy + swh * 2.45 - Math.Max(0.0, -kneeSwing), 0.95);
        }
        lm[PoseFrame.LWrist] = new Landmark(sx + sw * 0.65, sy - wristUp * swh, 0.9);
        lm[PoseFrame.RWrist] = new Landmark(sx - sw * 0.65, sy - wristUp * swh, 0.9);
        return f;
    }

    private static void VerifyTrackerLeanCore()
    {
        var cfg = new DroneConfig
        {
            uprightLeanMode = true,
            rollEnter = 0.24,
            releaseRatio = 0.48,
            zoneFrames = 3,
            leanCooldownMs = 340,
            leanNeutralRearmMs = 110,
            enableJogDetection = true,
            jogSprintHz = 2.45,
            lockMs = 350
        };
        var tracker = new DroneTracker(cfg);
        double t = 0;
        double sw = 0.1 * (16.0 / 9.0);

        // 1. Stand still with realistic MediaPipe sensor jitter (~18 FPS) -> locks, RunIntensity == 0 (Idle)
        DroneState st = null;
        for (int i = 0; i < 20; i++)
        {
            t += 55.0;
            double noise = ((i % 3) - 1) * sw * 0.0025;
            st = tracker.Update(MakePose(0.0, includeHips: true, wristUp: -1.3, jogBob: noise), t);
        }
        if (!st.Locked || st.Side != 0 || st.Vert != 0 || st.RunIntensity > 0.05)
            throw new Exception($"Stationary noise check failed: locked={st.Locked}, side={st.Side}, run={st.RunIntensity}");

        // 2a. Soft realistic indoor jog @ 15 FPS with natural foot weight-shift sway and NO knees visible
        for (int i = 0; i < 24; i++)
        {
            t += 66.7;
            double phase = (t / 1000.0) * 1.75 * Math.PI * 2.0;
            double bob = Math.Sin(phase) * sw * 0.018;
            double sway = Math.Sin(phase * 0.5) * 0.08;
            st = tracker.Update(MakePose(sway, includeHips: false, wristUp: -1.3, jogBob: bob), t);
        }
        if (st.RunIntensity < 0.30)
            throw new Exception($"Soft 15FPS jog with sway failed: run={st.RunIntensity}, hz={st.StepCadence}");

        // 2b. Chest-up view where arms pump and head barely moves (no hips/knees)
        for (int i = 0; i < 15; i++) { t += 55.0; tracker.Update(MakePose(0.0, includeHips: false), t); }
        for (int i = 0; i < 24; i++)
        {
            t += 50.0;
            double phase = (t / 1000.0) * 1.9 * Math.PI * 2.0;
            double bob = Math.Sin(phase) * sw * 0.007;
            double arm = Math.Sin(phase * 0.5) * sw * 0.095;
            st = tracker.Update(MakePose(0.0, includeHips: false, wristUp: -1.1, jogBob: bob, armSwing: arm), t);
        }
        if (st.RunIntensity < 0.30)
            throw new Exception($"Chest-up arm pump jog failed: run={st.RunIntensity}, hz={st.StepCadence}");

        // 3. Fast sprint in place (~2.75 Hz) -> RunIntensity pushes into sprint band (>= 0.72)
        for (int i = 0; i < 40; i++)
        {
            t += 33.3;
            double phase = (t / 1000.0) * 2.75 * Math.PI * 2.0;
            double bob = Math.Sin(phase) * sw * 0.040;
            double knee = Math.Sin(phase * 0.5) * sw * 0.20;
            st = tracker.Update(MakePose(0.0, includeHips: true, wristUp: -1.3, jogBob: bob, kneeSwing: knee), t);
        }
        if (st.RunIntensity < 0.72)
            throw new Exception($"Fast sprint failed to reach FastRun band: run={st.RunIntensity}, hz={st.StepCadence}");

        // 4. Stop moving (1000ms) -> drops cleanly back to Idle (< 0.08)
        for (int i = 0; i < 30; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.0, includeHips: true), t);
        }
        if (st.RunIntensity > 0.08)
            throw new Exception($"Stop to Idle failed: run={st.RunIntensity}");

        // 5. Anti-double-trigger lean check: lean right -> triggers ONCE, brief wobble must NOT trigger twice!
        int rightTriggers = 0;
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.75, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        if (rightTriggers != 1)
            throw new Exception($"Expected exact 1 right trigger on initial lean, got {rightTriggers}");

        // Brief partial return (60ms < 110ms neutral rearm) then lean right again -> MUST NOT double trigger!
        for (int i = 0; i < 2; i++)
        {
            t += 30.0;
            st = tracker.Update(MakePose(0.15, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.75, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        if (rightTriggers != 1)
            throw new Exception($"Double-trigger guard failed! triggers={rightTriggers}");

        // Full upright return (350ms) then clean second lean right -> MUST trigger cleanly!
        for (int i = 0; i < 12; i++)
        {
            t += 33.3;
            tracker.Update(MakePose(0.0, includeHips: true), t);
        }
        for (int i = 0; i < 10; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.75, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        if (rightTriggers != 2)
            throw new Exception($"Rearmed second lean right failed! triggers={rightTriggers}");

        // Opposite lean left works cleanly without waiting long neutral settle
        int leftTriggers = 0;
        for (int i = 0; i < 12; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(-0.75, includeHips: true), t);
            if (st.LeanTrigger < 0) leftTriggers++;
        }
        if (leftTriggers != 1 || st.Vert != 0)
            throw new Exception($"Opposite left lean failed: leftTriggers={leftTriggers}, vert={st.Vert}");
    }

    private static void VerifyAnimatorAndAudioWiring()
    {
        var runClips = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Player/Character/Character_Model.fbx");
        AnimationClip runClip = null;
        foreach (var obj in runClips)
        {
            if (obj is AnimationClip ac && ac.name.Equals("Run_Cycle", StringComparison.OrdinalIgnoreCase))
            {
                runClip = ac;
                break;
            }
        }
        AnimationClip fastClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Player/Character/Fast_Run.anim");
        if (runClip == null || fastClip == null)
            throw new Exception($"Missing Run or FastRun clip (run={runClip != null}, fast={fastClip != null})");

        var runPaths = new System.Collections.Generic.HashSet<string>();
        string sampleRun = "";
        var runBindings = AnimationUtility.GetCurveBindings(runClip);
        for (int i = 0; i < Math.Min(6, runBindings.Length); i++)
            sampleRun += $"[{runBindings[i].type.Name}|{runBindings[i].path}|{runBindings[i].propertyName}] ";
        foreach (var b in runBindings) runPaths.Add(b.path);

        var fastBindings = AnimationUtility.GetCurveBindings(fastClip);
        int matched = 0;
        int nonZeroCurveDiff = 0;
        string sampleFast = "";
        for (int i = 0; i < fastBindings.Length; i++)
        {
            var b = fastBindings[i];
            if (i < 6) sampleFast += $"[{b.type.Name}|{b.path}|{b.propertyName}] ";
            if (runPaths.Contains(b.path)) matched++;
            var c = AnimationUtility.GetEditorCurve(fastClip, b);
            if (c != null && c.keys.Length >= 2 && Math.Abs(c.keys[0].value - c.keys[c.keys.Length / 2].value) > 0.01f)
                nonZeroCurveDiff++;
        }
        Debug.Log($"[DroneVisionSceneSetup] fastLegacy={fastClip.legacy} len={fastClip.length} runBindings={runBindings.Length} fastBindings={fastBindings.Length} nonZero={nonZeroCurveDiff}\n  RUN : {sampleRun}\n  FAST: {sampleFast}");
        if (matched < 20 || nonZeroCurveDiff < 10)
            throw new Exception($"Fast_Run check failed (matched={matched}, nonZero={nonZeroCurveDiff})");

        var footsteps = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/Player/Running_Footsteps.mp3");
        var laneShift = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/Player/LaneShift.wav");
        if (footsteps == null || laneShift == null)
            throw new Exception($"Missing player audio clips (footsteps={footsteps != null}, laneShift={laneShift != null})");
        Debug.Log($"[DroneVisionSceneSetup] Verified Fast_Run ({fastClip.length:F2}s, matchedBindings={matched}) and audio clips (footsteps={footsteps.length:F1}s, laneShift={laneShift.length:F2}s).");
    }

    public static void RenderFastRunCheckShot()
    {
        if (SceneManager.GetActiveScene().path != GameScenePath && File.Exists(GameScenePath))
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
        Transform visual = player != null ? player.transform.Find("Character_Visual") : null;
        if (visual == null) return;
        Animator animator = visual.GetComponent<Animator>();
        if (animator == null) return;

        string outDir = @"C:\Users\Nima\.gemini\antigravity\brain\7b9ba8fe-181b-4459-b89e-1cb7eb5be3d3\scratch";
        Directory.CreateDirectory(outDir);

        GameObject backdrop = GameObject.Find("Menu Street Backdrop");
        if (backdrop != null) backdrop.SetActive(false);

        GameObject camGo = new GameObject("TempVerifyFastRunCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.backgroundColor = new Color(0.14f, 0.20f, 0.28f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        var rt = new RenderTexture(960, 720, 24);
        cam.targetTexture = rt;
        Vector3 center = visual.position + Vector3.up * 1.15f;

        Transform spineT = visual.Find("Character_Rig/Root/Hips/Spine");
        Transform legL = visual.Find("Character_Rig/Root/Hips/UpperLeg.L");
        Transform armL = visual.Find("Character_Rig/Root/Hips/Spine/Chest/UpperChest/Shoulder.L/UpperArm.L");
        var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        void CaptureAnimatorState(string tag, bool running, float runBlend, float normTime, Vector3 offset, string file)
        {
            animator.Rebind();
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsSliding", false);
            animator.SetBool("IsRunning", running);
            animator.SetFloat("RunBlend", runBlend);
            if (running)
                animator.Play("Run", 0, normTime);
            else
                animator.Play("Idle", 0, normTime);
            animator.Update(0.02f);
            animator.Update(0.02f);
            foreach (var smr in renderers)
            {
                smr.forceMatrixRecalculationPerRender = true;
                smr.updateWhenOffscreen = true;
                smr.enabled = false;
                smr.enabled = true;
            }

            Debug.Log($"[FastRunRenderCheck] {tag}: spine={(spineT ? spineT.localEulerAngles.ToString("F1") : "null")}, legL={(legL ? legL.localEulerAngles.ToString("F1") : "null")}, armL={(armL ? armL.localEulerAngles.ToString("F1") : "null")}");

            camGo.transform.position = center + offset;
            camGo.transform.LookAt(center);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        Vector3 side34 = new Vector3(2.5f, 0.15f, 0.9f);
        CaptureAnimatorState("IDLE", false, 0f, 0.15f, side34, "verify_idle_pose.png");
        CaptureAnimatorState("RUN_NORMAL", true, 0f, 0.30f, side34, "verify_run_normal.png");
        CaptureAnimatorState("FAST_RUN", true, 1f, 0.30f, side34, "verify_fastrun_side34.png");

        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(camGo);
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
    }
}
