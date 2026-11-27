using System;
using System.IO;
using DroneVision;
using DroneVision.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        RenderScenePerfVerification();
        Debug.Log("[DroneVisionSceneSetup] Setup & lean + jog + FastRun + audio + WebGL perf verification SUCCEEDED.");
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
        activeInput.model = "lite";
        activeInput.numPoses = 1;
        activeInput.maxFps = 24;
        activeInput.cameraWidth = 640;
        activeInput.cameraHeight = 360;
        activeInput.focusCrop = false;

        activeInput.autoPreview = true;
        activeInput.previewMode = PreviewMode.Lobby;
        activeInput.previewCorner = PreviewCorner.BottomLeft;
        activeInput.previewLabels = true;
        activeInput.statusBox = true;
        activeInput.lockedShowSeconds = 0.7f;

        activeInput.uprightLeanMode = true;
        activeInput.rollEnter = 0.19f;
        activeInput.leanCooldownMs = 230f;
        activeInput.leanNeutralRearmMs = 60f;
        activeInput.enableJogDetection = true;
        activeInput.jogSprintHz = 3.00f;
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
        if (playerCtrl != null)
        {
            var pcSo = new SerializedObject(playerCtrl);
            if (worldMove != null)
            {
                var wmProp = pcSo.FindProperty("worldMovement");
                if (wmProp != null) wmProp.objectReferenceValue = worldMove;
            }
            var cdProp = pcSo.FindProperty("visionStepMinCooldown");
            if (cdProp != null) cdProp.floatValue = 0.18f;
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
        foreach (var rtl in UnityEngine.Object.FindObjectsByType<RTLTextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rtl == null || string.IsNullOrEmpty(rtl.RawText)) continue;
            if (rtl.RawText.Contains("کامیکا"))
            {
                rtl.SetText(rtl.RawText.Replace("کامیکا", "کمیکا"), persianDigits: true);
                EditorUtility.SetDirty(rtl);
                var tmp = rtl.GetComponent<TMP_Text>();
                if (tmp != null) EditorUtility.SetDirty(tmp);
            }
        }

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

        ApplyWebGLScenePerformanceOptimizations(scene);

        EditorUtility.SetDirty(activeInput);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        if (openedTarget)
            Debug.Log("[DroneVisionSceneSetup] Opened & saved GameScene with root DroneVision, SFX & FastRun BlendTree.");
    }

    private static void ApplyWebGLScenePerformanceOptimizations(Scene scene)
    {
        // 1. Use crisp SMAA Low (clean edges, zero FXAA texture blur, no TAA MotionVectors overhead)
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c != null && (c.CompareTag("MainCamera") || c.name.Contains("Main Camera")))
                {
                    mainCam = c;
                    break;
                }
            }
        }
        if (mainCam != null)
        {
            mainCam.farClipPlane = 450f;
            var uac = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (uac != null)
            {
                uac.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                uac.antialiasingQuality = AntialiasingQuality.Low;
                uac.stopNaN = false;
                EditorUtility.SetDirty(uac);
            }
            var culling = mainCam.GetComponent<CameraDistanceCulling>();
            if (culling != null)
            {
                var cso = new SerializedObject(culling);
                var rProp = cso.FindProperty("renderDistance");
                if (rProp != null) rProp.floatValue = 450f;
                var cityProp = cso.FindProperty("cityCullDistance");
                if (cityProp != null) cityProp.floatValue = 262f;
                cso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(culling);
            }
        }

        // 2. Assign distant Horizon landmarks to Horizon layer so Default city geometry past fog (262m) culls cleanly
        int horizonLayer = LayerMask.NameToLayer("Horizon");
        if (horizonLayer >= 0)
        {
            foreach (string hName in new[] { "Tehran_Horizon_Ground", "Tehran_Horizon_Alborz", "Tehran_Horizon_MiladTower" })
            {
                GameObject hGo = GameObject.Find(hName);
                if (hGo == null) continue;
                foreach (Transform tr in hGo.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.gameObject.layer != horizonLayer)
                    {
                        tr.gameObject.layer = horizonLayer;
                        EditorUtility.SetDirty(tr.gameObject);
                    }
                }
            }
        }

        // 3. Enable GPU Instancing across city/props materials (zero visual change, fewer draw calls)
        foreach (string folder in new[] { "Assets/Models/City/Materials", "Assets/Materials" })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && !mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                }
            }
        }

        // 4. Keep character albedo crisp at 2048 and cap heavy normal map at 1024 compressed on WebGL
        foreach (var entry in new[]
        {
            ("Assets/Models/Player/Character/texture_pbr_20250901.png", 2048),
            ("Assets/Models/Player/Character/texture_pbr_20250901_normal.png", 1024)
        })
        {
            var importer = AssetImporter.GetAtPath(entry.Item1) as TextureImporter;
            if (importer == null) continue;
            var ps = importer.GetPlatformTextureSettings("WebGL");
            if (!ps.overridden || ps.maxTextureSize != entry.Item2 || ps.textureCompression != TextureImporterCompression.Compressed)
            {
                ps.name = "WebGL";
                ps.overridden = true;
                ps.maxTextureSize = entry.Item2;
                ps.textureCompression = TextureImporterCompression.Compressed;
                ps.compressionQuality = 50;
                importer.SetPlatformTextureSettings(ps);
                importer.SaveAndReimport();
            }
        }

        // 5. Disable shadow casting on small poles / decorative parked cars while keeping shadows on buildings, trees, obstacles & player
        foreach (string prefabPath in new[]
        {
            "Assets/Prefabs/City/Tehran_StreetLamp.prefab",
            "Assets/Prefabs/City/Tehran_StreetSign_Pole.prefab",
            "Assets/Prefabs/City/Tehran_Peugeot206.prefab"
        })
        {
            GameObject pRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pRoot == null) continue;
            bool dirty = false;
            foreach (Renderer r in pRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r.shadowCastingMode != ShadowCastingMode.Off)
                {
                    dirty = true;
                    break;
                }
            }
            if (dirty)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                foreach (Renderer r in contents.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        foreach (string prefix in new[] { "Menu_Lamp_", "Menu_Peugeot206_" })
        {
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                foreach (Transform tr in rootObj.GetComponentsInChildren<Transform>(true))
                {
                    if (!tr.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    foreach (Renderer r in tr.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.shadowCastingMode != ShadowCastingMode.Off)
                        {
                            r.shadowCastingMode = ShadowCastingMode.Off;
                            EditorUtility.SetDirty(r);
                        }
                    }
                }
            }
        }
    }

    private static PoseFrame MakePose(double leanRight, bool includeHips, double wristUp = -1.2, double jogBob = 0.0, double kneeSwing = 0.0, double armSwing = 0.0, double lateralX = 0.0)
    {
        var f = new PoseFrame { Aspect = 16.0 / 9.0 };
        double cx = 0.5 - lateralX, sy = 0.4 + jogBob, sw = 0.1, swh = sw * f.Aspect;
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
            lm[PoseFrame.LHip] = new Landmark(0.5 + sw * 0.4, sy + swh * 1.45, 0.97);
            lm[PoseFrame.RHip] = new Landmark(0.5 - sw * 0.4, sy + swh * 1.45, 0.97);
            lm[PoseFrame.LKnee] = new Landmark(0.5 + sw * 0.42, sy + swh * 2.45 - Math.Max(0.0, kneeSwing), 0.95);
            lm[PoseFrame.RKnee] = new Landmark(0.5 - sw * 0.42, sy + swh * 2.45 - Math.Max(0.0, -kneeSwing), 0.95);
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
            rollEnter = 0.19,
            releaseRatio = 0.45,
            zoneFrames = 2,
            leanCooldownMs = 230,
            leanNeutralRearmMs = 60,
            enableJogDetection = true,
            jogSprintHz = 3.00,
            lockMs = 350
        };
        var tracker = new DroneTracker(cfg);
        double t = 0;
        double sw = 0.1 * (16.0 / 9.0);

        // 1. Stand still + tiny camera/body fidget (~0.008) -> locks, stays Idle (NEVER triggers Run or FastRun!)
        DroneState st = null;
        for (int i = 0; i < 24; i++)
        {
            t += 50.0;
            double fidgetBob = Math.Sin(i * 0.8) * sw * 0.007;
            double fidgetArm = Math.Sin(i * 0.5) * sw * 0.025;
            st = tracker.Update(MakePose(0.03 * Math.Sin(i * 0.4), includeHips: true, wristUp: -1.3, jogBob: fidgetBob, armSwing: fidgetArm), t);
        }
        if (!st.Locked || st.Side != 0 || st.Vert != 0 || st.RunIntensity > 0.08)
            throw new Exception($"Tiny fidget/stationary check failed: locked={st.Locked}, side={st.Side}, run={st.RunIntensity}");

        // 2a. Normal jogging @ ~1.65 Hz with weight-shift sway (NO hips) -> Run band (0.25..0.60), NOT FastRun, ZERO lean triggers!
        int spuriousLean = 0;
        for (int i = 0; i < 28; i++)
        {
            t += 55.0;
            double phase = (t / 1000.0) * 1.65 * Math.PI * 2.0;
            double bob = Math.Sin(phase) * sw * 0.024;
            double arm = Math.Sin(phase * 0.5) * sw * 0.08;
            double sway = Math.Sin(phase * 0.5) * 0.08;
            st = tracker.Update(MakePose(sway, includeHips: false, wristUp: -1.3, jogBob: bob, armSwing: arm), t);
            if (st.LeanTrigger != 0) spuriousLean++;
        }
        if (st.RunIntensity < 0.25 || st.RunIntensity >= 0.65 || spuriousLean > 0)
            throw new Exception($"Normal jog band/sway check failed: run={st.RunIntensity}, hz={st.StepCadence}, spuriousLean={spuriousLean}");

        // 2b. Settle then test chest-up arm-pump jog (~1.85 Hz) -> Normal Run (NOT FastRun!)
        for (int i = 0; i < 20; i++) { t += 50.0; tracker.Update(MakePose(0.0, includeHips: false), t); }
        for (int i = 0; i < 28; i++)
        {
            t += 45.0;
            double phase = (t / 1000.0) * 1.85 * Math.PI * 2.0;
            double bob = Math.Sin(phase) * sw * 0.020;
            double arm = Math.Sin(phase * 0.5) * sw * 0.095;
            st = tracker.Update(MakePose(0.0, includeHips: false, wristUp: -1.1, jogBob: bob, armSwing: arm), t);
        }
        if (st.RunIntensity < 0.25 || st.RunIntensity >= 0.66)
            throw new Exception($"Chest-up arm pump jog failed: run={st.RunIntensity}, hz={st.StepCadence}");

        // 3. Genuine fast sprint in place (~2.95 Hz) -> RunIntensity pushes into FastRun band (>= 0.72)
        for (int i = 0; i < 42; i++)
        {
            t += 33.3;
            double phase = (t / 1000.0) * 2.95 * Math.PI * 2.0;
            double bob = Math.Sin(phase) * sw * 0.044;
            double knee = Math.Sin(phase * 0.5) * sw * 0.22;
            double arm = Math.Sin(phase * 0.5) * sw * 0.16;
            st = tracker.Update(MakePose(0.0, includeHips: true, wristUp: -1.3, jogBob: bob, kneeSwing: knee, armSwing: arm), t);
        }
        if (st.RunIntensity < 0.72)
            throw new Exception($"Fast sprint failed to reach FastRun band: run={st.RunIntensity}, hz={st.StepCadence}");

        // 4. Stop moving with realistic webcam jitter & noisy low-visibility knees -> must stay strictly Idle (< 0.08 in 750ms, <= 0.02 over 2s)
        for (int i = 0; i < 19; i++)
        {
            t += 40.0;
            double jBob = Math.Sin(i * 1.1) * sw * 0.006;
            double jArm = Math.Sin(i * 0.7) * sw * 0.025;
            PoseFrame p = MakePose(0.02 * Math.Sin(i * 0.5), includeHips: true, wristUp: -1.3, jogBob: jBob, armSwing: jArm);
            // Low-visibility bottom-edge hallucinated knees oscillating wildly while standing still must be rejected!
            p.Landmarks[PoseFrame.LKnee] = new Landmark(0.54, 0.98 - Math.Sin(i * 0.6) * sw * 0.14, 0.45);
            p.Landmarks[PoseFrame.RKnee] = new Landmark(0.46, 0.98 + Math.Sin(i * 0.6) * sw * 0.14, 0.45);
            st = tracker.Update(p, t);
        }
        if (st.RunIntensity > 0.08)
            throw new Exception($"Stop to Idle failed within 760ms: run={st.RunIntensity}");
        for (int i = 0; i < 35; i++)
        {
            t += 40.0;
            double jBob = Math.Sin(i * 1.3) * sw * 0.007;
            double jArm = Math.Sin(i * 0.9) * sw * 0.030;
            PoseFrame p = MakePose(0.02 * Math.Sin(i * 0.4), includeHips: false, wristUp: -1.3, jogBob: jBob, armSwing: jArm);
            st = tracker.Update(p, t);
            if (st.RunIntensity > 0.05)
                throw new Exception($"Noisy stationary drift triggered keep-walking: i={i}, run={st.RunIntensity}, hz={st.StepCadence}");
        }

        // 5. Chest-up lateral lean (level shoulders, no hips) MUST trigger right & left reliably!
        int cuRight = 0, cuLeft = 0;
        for (int i = 0; i < 8; i++)
        {
            t += 40.0;
            st = tracker.Update(MakePose(0.06, includeHips: false, lateralX: 0.032), t);
            if (st.LeanTrigger > 0) cuRight++;
        }
        for (int i = 0; i < 8; i++)
        {
            t += 40.0;
            st = tracker.Update(MakePose(-0.06, includeHips: false, lateralX: -0.032), t);
            if (st.LeanTrigger < 0) cuLeft++;
        }
        if (cuRight != 1 || cuLeft != 1)
            throw new Exception($"Chest-up lateral lean check failed: right={cuRight}, left={cuLeft}");
        for (int i = 0; i < 12; i++) { t += 40.0; tracker.Update(MakePose(0.0, includeHips: true), t); }

        // 6. Full-body anti-double-trigger lean check: lean right -> triggers ONCE, brief dip inside zone must NOT trigger twice!
        int rightTriggers = 0;
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.55, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        if (rightTriggers != 1)
            throw new Exception($"Expected exact 1 right trigger on initial lean, got {rightTriggers}");

        // Brief partial dip keeping inside hysteresis (> 0.19*0.45=0.085) then lean right again -> MUST NOT double trigger!
        for (int i = 0; i < 2; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.20, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.55, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        if (rightTriggers != 1)
            throw new Exception($"Double-trigger guard failed! triggers={rightTriggers}");

        // Quick upright settle (~140ms) then clean second lean right -> MUST re-arm and trigger cleanly!
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            tracker.Update(MakePose(0.0, includeHips: true), t);
        }
        for (int i = 0; i < 10; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.55, includeHips: true), t);
            if (st.LeanTrigger > 0) rightTriggers++;
        }
        if (rightTriggers != 2)
            throw new Exception($"Rearmed second lean right failed! triggers={rightTriggers}");

        // Opposite lean left works cleanly without stalling
        int leftTriggers = 0;
        for (int i = 0; i < 10; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(-0.55, includeHips: true), t);
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

    private static void RenderScenePerfVerification()
    {
        if (SceneManager.GetActiveScene().path != GameScenePath && File.Exists(GameScenePath))
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        Camera cam = Camera.main;
        if (cam == null) return;

        var uac = cam.GetComponent<UniversalAdditionalCameraData>();
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        var chunks = UnityEngine.Object.FindObjectsByType<WorldChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var dv = UnityEngine.Object.FindFirstObjectByType<DroneVisionInput>(FindObjectsInactive.Include);

        int activeRenderers = 0, castShadowRenderers = 0;
        long totalTriangles = 0;
        foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            activeRenderers++;
            if (mr.shadowCastingMode != ShadowCastingMode.Off) castShadowRenderers++;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) totalTriangles += mf.sharedMesh.triangles.Length / 3;
        }
        int horizonCount = 0;
        int horizonLayer = LayerMask.NameToLayer("Horizon");
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (horizonLayer >= 0 && r.gameObject.layer == horizonLayer) horizonCount++;

        Debug.Log($"[PerfVerify] AA={(uac != null ? uac.antialiasing.ToString() : "null")} MobileRP=(resScale={(rp ? rp.renderScale : -1f)}, shadowRes={(rp ? rp.mainLightShadowmapResolution : -1)}, shadowDist={(rp ? rp.shadowDistance : -1f)}, cascades={(rp ? rp.shadowCascadeCount : -1)}, addLights={(rp ? rp.additionalLightsRenderingMode.ToString() : "?")}) Chunks={chunks.Length} ActiveMR={activeRenderers} CastShadows={castShadowRenderers} TotalTris={totalTriangles} HorizonRenderers={horizonCount} CV=(model={dv?.model}, {dv?.cameraWidth}x{dv?.cameraHeight}@{dv?.maxFps}fps)");

        if (uac == null || uac.antialiasing != AntialiasingMode.SubpixelMorphologicalAntiAliasing)
            throw new Exception("Expected Main Camera SMAA antialiasing!");
        if (rp == null || rp.renderScale < 0.99f || rp.mainLightShadowmapResolution > 1024 || rp.shadowCascadeCount != 1 || rp.shadowDistance > 65f)
            throw new Exception("Mobile_RPAsset crisp resolution + shadow optimizations not applied!");
        if (dv == null || dv.model != "lite" || dv.cameraWidth != 640 || dv.maxFps != 24)
            throw new Exception("DroneVision CV perf settings not applied!");

        foreach (var rtl in UnityEngine.Object.FindObjectsByType<RTLTextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rtl != null && rtl.RawText != null && rtl.RawText.Contains("کامیکا"))
                throw new Exception($"Legacy spelling found on RTLTextMeshPro '{rtl.name}': {rtl.RawText}");
        }
        foreach (var tmp in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp != null && !string.IsNullOrEmpty(tmp.text) && (tmp.text.Contains("کامیکا") || tmp.text.Contains("ﺎﮑﯿﻣﺎﮐ")))
                throw new Exception($"Legacy spelling found on TMP_Text '{tmp.name}': {tmp.text}");
        }

        string outDir = @"C:\Users\Nima\.gemini\antigravity\brain\7b9ba8fe-181b-4459-b89e-1cb7eb5be3d3\scratch";
        Directory.CreateDirectory(outDir);
        Vector3 origPos = cam.transform.position;
        Quaternion origRot = cam.transform.rotation;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;

        void Shoot(Vector3 pos, Quaternion rot, string fileName)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            Shader.SetGlobalVector("_KomikaCurveOrigin", new Vector4(0f, 0f, -92.84f, 1f));
            Shader.SetGlobalVector("_KomikaCurveParams", new Vector4(0.0012f, 0f, 160f, 0f));
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, fileName), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        Shoot(new Vector3(0f, 2.55f, -98.40f), Quaternion.Euler(10.0f, 0f, 0f), "verify_perf_gameplay.png");
        Shoot(new Vector3(0f, 0.45f, -90.50f), Quaternion.Euler(4.0f, 180f, 0f), "verify_perf_menu.png");

        RenderTexture.active = null;
        cam.targetTexture = prev;
        UnityEngine.Object.DestroyImmediate(rt);
        cam.transform.SetPositionAndRotation(origPos, origRot);
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
    }
}
