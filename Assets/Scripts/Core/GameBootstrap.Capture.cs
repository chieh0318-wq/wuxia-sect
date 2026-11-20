using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private IEnumerator Capture(string directory)
        {
            yield return new WaitForSecondsRealtime(2);
            if (tutorial == null)
            {
                Debug.LogError("[Capture] Tutorial failed to initialize.");
                Application.Quit(1);
                yield break;
            }
            Directory.CreateDirectory(directory);
            var resolution = CaptureResolution(); // M2-B capture hooks
            int width = resolution.x, height = resolution.y;
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var canvas = ui.Canvas;
            RenderTexture previous = RenderTexture.active;
            try
            {
                controls.enabled = false;
                view.targetTexture = target;
                view.aspect = width / (float)height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = view;
                canvas.planeDistance = view.nearClipPlane + .1f;

                yield return null;
                if (!SaveShot("01_courtyard_overview.png"))
                    yield break;
                tutorial.ChooseForCapture();
                tutorial.Record("camera_move");
                tutorial.Record("camera_zoom");
                yield return null;
                if (!SaveShot("02_tutorial_hud.png"))
                    yield break;

                controls.Frame(new Vector2(0, 7.1f), 16.8f);
                yield return null;
                if (!SaveShot("03_closeup.png"))
                    yield break;

                var hall = System.Array.Find(state.Buildings, building => building?.id == "hall");
                tutorial.Record("click_building", hall.id);
                Speak(state.Disciples[0]);
                ShowBuilding(hall);
                yield return null;
                if (!SaveShot("04_building_panel.png")) yield break;
                yield return CaptureM2(file => SaveShot(file));
                Application.Quit();
            }
            finally
            {
                RenderTexture.active = previous;
                view.targetTexture = null;
                view.ResetAspect();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Destroy(pixels);
                Destroy(target);
            }

            bool SaveShot(string file)
            {
                try
                {
                    WritePng(file);
                    return true;
                }
                catch (System.Exception error)
                {
                    Debug.LogError($"[Capture] {error}");
                    Application.Quit(1);
                    return false;
                }
            }

            void WritePng(string file)
            {
                Canvas.ForceUpdateCanvases();
                ui.PositionLabels(view);
                ui.PositionHighlight(tutorial.Highlight, view, world);
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(view, new UniversalRenderPipeline.SingleCameraRequest
                {
                    destination = target
                });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                string path = Path.Combine(directory, file);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log($"[Capture] wrote {path}");
            }
        }
    }
}
