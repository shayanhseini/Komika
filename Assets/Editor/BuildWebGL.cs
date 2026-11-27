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
            Debug.Log("[BuildWebGL] Applying optimizations and configuring WebGL settings for GitHub Pages...");

            // 1. Apply WebGL Optimizations & ensure DroneVision CV setup in scene
            WebGLOptimizer.ApplyOptimizations();
            DroneVisionSceneSetup.SetupSceneQuiet();

            // 2. Gather active scenes
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
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
                // Ensure .nojekyll for GitHub Pages
                string nojekyllPath = Path.Combine(fullOutputPath, ".nojekyll");
                if (!File.Exists(nojekyllPath))
                {
                    File.WriteAllText(nojekyllPath, "");
                }

                // Update index.html title
                string indexPath = Path.Combine(fullOutputPath, "index.html");
                if (File.Exists(indexPath))
                {
                    string html = File.ReadAllText(indexPath);
                    html = html.Replace("<title>Unity Web Player | EndlessFlyingProject</title>", "<title>Komika | کمیکا</title>");
                    html = html.Replace("<title>Unity Web Player | Endless Flying Project</title>", "<title>Komika | کمیکا</title>");
                    html = html.Replace("<title>Unity Web Player | Komika</title>", "<title>Komika | کمیکا</title>");
                    html = html.Replace("<div id=\"unity-build-title\">EndlessFlyingProject</div>", "<div id=\"unity-build-title\">Komika | کمیکا</div>");
                    html = html.Replace("<div id=\"unity-build-title\">Endless Flying Project</div>", "<div id=\"unity-build-title\">Komika | کمیکا</div>");
                    html = html.Replace("<div id=\"unity-build-title\">Komika</div>", "<div id=\"unity-build-title\">Komika | کمیکا</div>");

                    if (!html.Contains("unity-progress-text") && html.Contains("<div id=\"unity-progress-bar-full\"></div>"))
                    {
                        html = html.Replace("<div id=\"unity-progress-bar-full\"></div>\n        </div>",
                            "<div id=\"unity-progress-bar-full\"></div>\n        </div>\n        <div id=\"unity-progress-text\" style=\"color: #00e5ff; font-family: sans-serif; font-size: 15px; font-weight: bold; margin-top: 12px; text-shadow: 0 0 10px rgba(0,229,255,0.7); text-align: center;\">0%</div>");
                        html = html.Replace("document.querySelector(\"#unity-progress-bar-full\").style.width = 100 * progress + \"%\";",
                            "document.querySelector(\"#unity-progress-bar-full\").style.width = 100 * progress + \"%\";\n          var pt = document.querySelector(\"#unity-progress-text\");\n          if (pt) pt.textContent = Math.round(100 * progress) + \"%\";");
                    }
                    File.WriteAllText(indexPath, html);
                }

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
