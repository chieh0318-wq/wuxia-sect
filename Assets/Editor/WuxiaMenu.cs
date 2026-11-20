#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Wuxia
{
    internal static class WuxiaMenu
    {
        [MenuItem("Wuxia/Create Main Scene")]
        private static void CreateMainScene()
        {
            const string path = "Assets/Scenes/Main.unity";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(path))
                EditorSceneManager.OpenScene(path);
            else
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, path))
                    return;
            }

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != path)
                .Append(new EditorBuildSettingsScene(path, true)).ToArray();
        }
    }
}
#endif
