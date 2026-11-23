#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wuxia.EditorTools
{
    public static class WuxiaBuild
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        // Makes sure an (empty) Main scene exists and is in Build Settings.
        // The game builds itself at runtime, so the scene can stay empty.
        public static void EnsureMainScene()
        {
            if (!File.Exists(MainScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, MainScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
        }

        [MenuItem("Wuxia/Build Windows (Builds/Win)")]
        public static void BuildWindows()
        {
            EnsureMainScene();
            var outputDirectory = Environment.GetEnvironmentVariable("WUXIA_BUILD_DIR");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = "Builds/Win";
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = Path.Combine(outputDirectory, "WuxiaSect.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var originalWidth = PlayerSettings.defaultScreenWidth;
            var originalHeight = PlayerSettings.defaultScreenHeight;
            var originalMode = PlayerSettings.fullScreenMode;
            var originalNativeResolution = PlayerSettings.defaultIsNativeResolution;
            var originalResizable = PlayerSettings.resizableWindow;
            BuildReport report;
            try
            {
                PlayerSettings.defaultScreenWidth = 1920;
                PlayerSettings.defaultScreenHeight = 1080;
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                PlayerSettings.defaultIsNativeResolution = false;
                PlayerSettings.resizableWindow = true;
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.defaultScreenWidth = originalWidth;
                PlayerSettings.defaultScreenHeight = originalHeight;
                PlayerSettings.fullScreenMode = originalMode;
                PlayerSettings.defaultIsNativeResolution = originalNativeResolution;
                PlayerSettings.resizableWindow = originalResizable;
            }
            Debug.Log("[WuxiaBuild] result=" + report.summary.result + " errors=" + report.summary.totalErrors);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
