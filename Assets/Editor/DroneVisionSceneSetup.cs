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
        SetupSceneQuiet();
        VerifyTrackerLeanCore();
        Debug.Log("[DroneVisionSceneSetup] Setup & lean verification SUCCEEDED.");
    }

    public static void SetupSceneQuiet()
    {
        WebGLOptimizer.ApplyOptimizations();

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
        activeInput.rollEnter = 0.20f;
        activeInput.lockMs = 350f;
        activeInput.lockGraceMs = 1200f;
        activeInput.invert = false;
        activeInput.stageZone = 0.8f;
        activeInput.minPlayerSize = 0.04f;
        activeInput.keyboardFallback = true;

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
            Debug.Log("[DroneVisionSceneSetup] Opened & saved GameScene with root DroneVision.");
    }

    private static PoseFrame MakePose(double leanRight, bool includeHips, double wristUp = -1.2)
    {
        var f = new PoseFrame { Aspect = 16.0 / 9.0 };
        double cx = 0.5, sy = 0.4, sw = 0.1, swh = sw * f.Aspect;
        Landmark[] lm = f.Landmarks;
        for (int i = 0; i < PoseFrame.Count; i++) lm[i] = new Landmark(cx, 0.5, 0);

        double sx = cx - leanRight * sw * 0.55;
        lm[PoseFrame.Nose] = new Landmark(cx - leanRight * sw * 0.85, sy - swh * 0.8, 0.99);
        lm[PoseFrame.LShoulder] = new Landmark(sx + sw / 2, sy - leanRight * swh * 0.12, 0.99);
        lm[PoseFrame.RShoulder] = new Landmark(sx - sw / 2, sy + leanRight * swh * 0.12, 0.99);
        if (includeHips)
        {
            lm[PoseFrame.LHip] = new Landmark(cx + sw * 0.4, sy + swh * 1.45, 0.97);
            lm[PoseFrame.RHip] = new Landmark(cx - sw * 0.4, sy + swh * 1.45, 0.97);
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
            rollEnter = 0.20,
            lockMs = 350,
            zoneFrames = 2
        };
        var tracker = new DroneTracker(cfg);
        double t = 0;

        // 1. Stand upright with hands relaxed down (450ms) -> must lock!
        DroneState st = null;
        for (int i = 0; i < 15; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.0, includeHips: true, wristUp: -1.3), t);
        }
        if (!st.Locked || st.Side != 0 || st.Vert != 0)
            throw new Exception($"Upright lock failed: locked={st.Locked}, side={st.Side}, vert={st.Vert}");

        // 2. Gentle lean right -> must output Side == +1 and Vert == 0 (even with arms high!)
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.65, includeHips: true, wristUp: +0.9), t);
        }
        if (st.Side != 1 || st.Vert != 0 || Math.Abs(st.Pitch) > 1e-6)
            throw new Exception($"Right lean failed: side={st.Side}, vert={st.Vert}, pitch={st.Pitch}");

        // 3. Return upright -> Side == 0
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(0.0, includeHips: true, wristUp: -1.3), t);
        }
        if (st.Side != 0)
            throw new Exception($"Return to neutral failed: side={st.Side}");

        // 4. Gentle lean left -> Side == -1, Vert == 0
        for (int i = 0; i < 8; i++)
        {
            t += 33.3;
            st = tracker.Update(MakePose(-0.65, includeHips: true, wristUp: -1.3), t);
        }
        if (st.Side != -1 || st.Vert != 0)
            throw new Exception($"Left lean failed: side={st.Side}, vert={st.Vert}");

        // 5. Seated / waist-cut view (no hips) leaning right -> still detects Side == +1
        var chestTracker = new DroneTracker(cfg);
        t = 0;
        for (int i = 0; i < 15; i++)
        {
            t += 33.3;
            chestTracker.Update(MakePose(0.0, includeHips: false), t);
        }
        for (int i = 0; i < 10; i++)
        {
            t += 33.3;
            st = chestTracker.Update(MakePose(0.95, includeHips: false), t);
        }
        if (!st.Locked || st.Side != 1 || st.Vert != 0)
            throw new Exception($"Chest-only right lean failed: locked={st.Locked}, side={st.Side}, vert={st.Vert}");
    }
}
