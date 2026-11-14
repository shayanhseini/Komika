using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class BuildWebGL
{
    private const string TriggerPath = "Temp/trigger_webgl_build.txt";
    private const string StatusPath = "Temp/webgl_build_status.txt";
    private const string OutputFolder = "Builds/WebGL";

    static BuildWebGL()
    {
        EditorApplication.update += CheckTrigger;
    }

    [MenuItem("Kamica/Build WebGL for GitHub Pages")]
    public static void TriggerFromMenu()
    {
        File.WriteAllText(TriggerPath, "manual");
        Debug.Log("[BuildWebGL] Build triggered from Menu.");
    }

    private static void CheckTrigger()
    {
        if (!File.Exists(TriggerPath))
            return;

        try
        {
            File.Delete(TriggerPath);
        }
        catch
        {
            return;
        }

        PerformBuild();
    }

    public static void PerformBuild()
    {
        try
        {
            File.WriteAllText(StatusPath, "BUILDING");
            Debug.Log("[BuildWebGL] Configuring WebGL settings for GitHub Pages...");

            // 1. Configure settings for 100% GitHub Pages compatibility (No Gzip/Brotli header mismatch)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            // 2. Gather active scenes
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // Fallback to active scene or GameScene.unity
                string fallback = "Assets/Scenes/GameScene.unity";
                if (File.Exists(fallback))
                    scenes = new[] { fallback };
                else
                    throw new Exception("No valid scenes found to build!");
            }

            string fullOutputPath = Path.GetFullPath(OutputFolder);
            if (!Directory.Exists(fullOutputPath))
            {
                Directory.CreateDirectory(fullOutputPath);
            }

            Debug.Log($"[BuildWebGL] Starting BuildPlayer to: {fullOutputPath}");
            Debug.Log($"[BuildWebGL] Scenes ({scenes.Length}): {string.Join(", ", scenes)}");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullOutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string info = $"SUCCESS:{summary.totalSize}:{summary.totalTime.TotalSeconds:F1}";
                File.WriteAllText(StatusPath, info);
                Debug.Log($"[BuildWebGL] BUILD SUCCEEDED in {summary.totalTime.TotalSeconds:F1}s! Output size: {summary.totalSize / (1024 * 1024)}MB");
            }
            else
            {
                string err = $"FAILED:{summary.totalErrors}";
                File.WriteAllText(StatusPath, err);
                Debug.LogError($"[BuildWebGL] BUILD FAILED with {summary.totalErrors} errors!");
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(StatusPath, $"ERROR:{ex.Message}");
            Debug.LogException(ex);
        }
    }
}
