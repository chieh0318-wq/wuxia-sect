using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wuxia.EditorTools
{
    internal sealed class M2TextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var texture = (TextureImporter)assetImporter;
            if (assetPath == "Assets/Art/Map/world_map.jpg")
            {
                texture.maxTextureSize = 4096;
                texture.mipmapEnabled = false;
                texture.npotScale = TextureImporterNPOTScale.None;
                texture.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            // Portraits and story CG are configured by StoryArtImporter (2D texture, no mipmaps) only,
            // so the two postprocessors cannot fight over the texture type.
        }
    }

    public static class M2BuildAssets
    {
        public static void InstallRuntimeAssets()
        {
            const string path = "Assets/Resources/M2RuntimeAssets.asset";
            var assets = AssetDatabase.LoadAssetAtPath<M2RuntimeAssets>(path);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<M2RuntimeAssets>();
                AssetDatabase.CreateAsset(assets,path);
            }
            assets.map = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Map/world_map.jpg");
            assets.music = System.Array.Empty<AudioClip>();
            assets.sfx = Clips("Assets/Audio/Sfx");
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }

        private static AudioClip[] Clips(string path) => AssetDatabase.FindAssets("t:AudioClip",new[] { path })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(name => name)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
    }
}
