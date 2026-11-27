using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class WebGLOptimizer
{
    [MenuItem("Kamica/Apply WebGL Optimizations")]
    public static void ApplyOptimizations()
    {
        Debug.Log("[WebGLOptimizer] Setting WebGL PlayerSettings...");

        PlayerSettings.productName = "Komika";
        PlayerSettings.companyName = "Kamica";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;

        // 1. IL2CPP & Code Stripping
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);

        // 2. WebGL Player Settings
        PlayerSettings.WebGL.template = "PROJECT:DroneVision";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.memorySize = 128;

        Debug.Log("[WebGLOptimizer] WebGL PlayerSettings configured successfully!");
    }
}
