using System.Linq;
using UnityEditor;

namespace Wuxia.EditorTools
{
    internal sealed class QuaterniusImport : AssetPostprocessor
    {
        private bool IsPack => assetPath.StartsWith("Assets/ThirdParty/Quaternius/");

        private void OnPreprocessModel()
        {
            if (!IsPack) return;
            var model = (ModelImporter)assetImporter;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.isReadable = true;
            model.importCameras = false;
            model.importLights = false;
            bool animation = assetPath.EndsWith("/UAL1_Standard.fbx");
            bool body = assetPath.EndsWith("_FullBody.fbx");
            model.animationType = body || animation ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            model.importAnimation = animation;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsPack || !assetPath.EndsWith("/UAL1_Standard.fbx")) return;
            var model = (ModelImporter)assetImporter;
            var clips = model.defaultClipAnimations.Where(clip => clip.name.EndsWith("|Idle_Loop") ||
                clip.name.EndsWith("|Walk_Loop") || clip.name.EndsWith("|Walk_Formal_Loop")).ToArray();
            foreach (var clip in clips)
            {
                clip.loopTime = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootRotation = true;
            }
            model.clipAnimations = clips;
        }
    }
}
