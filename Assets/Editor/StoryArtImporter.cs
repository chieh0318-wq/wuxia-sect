using System;
using UnityEditor;

namespace Wuxia.EditorTools
{
    internal sealed class StoryArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Portraits/", StringComparison.Ordinal) &&
                !assetPath.StartsWith("Assets/Resources/Story/", StringComparison.Ordinal))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
