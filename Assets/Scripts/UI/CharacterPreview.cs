using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Wuxia
{
    internal sealed class CharacterPreview : MonoBehaviour
    {
        private GameObject stage;
        private GameObject figure;
        private Camera cameraView;
        private RenderTexture target;
        private float started;

        internal void Initialize(CharacterLook look, RawImage output)
        {
            started = Time.unscaledTime;
            stage = new GameObject("Appearance preview");
            stage.transform.position = new Vector3(0, 1000, 0);
            figure = CharacterFactory.Build(look);
            figure.transform.SetParent(stage.transform, false);
            CharacterFactory.Part(stage.transform, PrimitiveType.Cylinder, new(0, -.08f, 0), new(.9f, .10f, .9f),
                new Color(.45f, .33f, .24f));
            var viewer = new GameObject("Preview camera");
            viewer.transform.SetParent(stage.transform, false);
            viewer.transform.localPosition = new Vector3(.10f, 1.25f, 3);
            viewer.transform.LookAt(stage.transform.position + Vector3.up * .86f);
            cameraView = viewer.AddComponent<Camera>();
            cameraView.enabled = false;
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.backgroundColor = new Color(.11f, .17f, .17f);
            cameraView.orthographic = true;
            cameraView.orthographicSize = 1;
            cameraView.farClipPlane = 10;
            target = new RenderTexture(310, 328, 24);
            cameraView.targetTexture = target;
            output.texture = target;
            Render();
        }

        private void LateUpdate()
        {
            if (figure == null) return;
            // The refined figure faces +Z (towards the preview camera); start three-quarter front and turn slowly.
            figure.transform.localEulerAngles = new Vector3(0, -24 + (Time.unscaledTime - started) * 27, 0);
            Render();
        }

        private void Render() => RenderPipeline.SubmitRenderRequest(cameraView,
            new UniversalRenderPipeline.SingleCameraRequest { destination = target });

        private void OnDestroy()
        {
            if (stage != null) Destroy(stage);
            if (target != null) Destroy(target);
        }
    }
}
