#if UNITY_EDITOR
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
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = "Builds/Win/WuxiaSect.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[WuxiaBuild] result=" + report.summary.result + " errors=" + report.summary.totalErrors);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
