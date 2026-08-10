using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AavegotchiBowl.EditorTools
{
    /// <summary>
    /// Batch WebGL build for Aarcade upload (bowl.* prefix, Brotli).
    /// Usage:
    ///   Unity -batchmode -quit -projectPath . \
    ///     -executeMethod AavegotchiBowl.EditorTools.WebGLBuild.BuildForAarcade
    /// </summary>
    public static class WebGLBuild
    {
        const string ProductName = "bowl";
        const string OutputRoot = "Builds/WebGL";

        [MenuItem("Aavegotchi Bowl/Build WebGL (Aarcade)")]
        public static void BuildFromMenu()
        {
            var ok = BuildCore(exitOnComplete: false);
            EditorUtility.DisplayDialog(
                "Aavegotchi Bowl WebGL",
                ok ? $"Build succeeded.\n\n{Path.GetFullPath(Path.Combine(OutputRoot, "Build"))}" : "Build failed — check the Console.",
                "OK");
        }

        public static void BuildForAarcade()
        {
            BuildCore(exitOnComplete: true);
        }

        static bool BuildCore(bool exitOnComplete)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuild] No enabled scenes in Build Settings.");
                if (exitOnComplete) EditorApplication.Exit(1);
                return false;
            }

            var previousName = PlayerSettings.productName;
            var previousCompression = PlayerSettings.WebGL.compressionFormat;
            var previousFallback = PlayerSettings.WebGL.decompressionFallback;

            try
            {
                PlayerSettings.productName = ProductName;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = true;

                var outputDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), OutputRoot));
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
                Directory.CreateDirectory(outputDir);

                Debug.Log($"[WebGLBuild] Building {scenes.Length} scene(s) → {outputDir}");
                foreach (var scene in scenes)
                    Debug.Log($"[WebGLBuild]   {scene}");

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputDir,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;

                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"[WebGLBuild] Failed: {summary.result} ({summary.totalErrors} errors)");
                    if (exitOnComplete) EditorApplication.Exit(1);
                    return false;
                }

                var buildFolder = Path.Combine(outputDir, "Build");
                if (!Directory.Exists(buildFolder))
                    buildFolder = outputDir;

                Debug.Log($"[WebGLBuild] Succeeded in {summary.totalTime}. Output: {buildFolder}");
                foreach (var file in Directory.GetFiles(buildFolder))
                    Debug.Log($"[WebGLBuild]   {Path.GetFileName(file)} ({new FileInfo(file).Length} bytes)");

                if (exitOnComplete) EditorApplication.Exit(0);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBuild] Exception: {e}");
                if (exitOnComplete) EditorApplication.Exit(1);
                return false;
            }
            finally
            {
                PlayerSettings.productName = previousName;
                PlayerSettings.WebGL.compressionFormat = previousCompression;
                PlayerSettings.WebGL.decompressionFallback = previousFallback;
            }
        }
    }
}
