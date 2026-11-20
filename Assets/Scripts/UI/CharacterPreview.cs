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
            AddLight("Soft key", new Vector3(-1.1f, 2.6f, 1.4f), new Color(1, .89f, .73f), 2.3f);
            AddLight("Cool rim", new Vector3(.8f, 1.8f, -.8f), new Color(.66f, .83f, 1), 2.0f);
            target = new RenderTexture(540, 480, 24);
            cameraView.targetTexture = target;
            output.texture = target;
            Render();
        }

        private void AddLight(string name, Vector3 position, Color color, float intensity)
        {
            var lamp = new GameObject(name).AddComponent<Light>();
            lamp.transform.SetParent(stage.transform, false);
            lamp.transform.localPosition = position;
            lamp.type = LightType.Point;
            lamp.color = color;
            lamp.intensity = intensity;
            lamp.range = 4;
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
