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
            Directory.CreateDirectory(directory);
            const int width = 1920, height = 1080;
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

                SaveShot("01_courtyard_overview.png");
                tutorial.ChooseForCapture();
                tutorial.Record("camera_move");
                tutorial.Record("camera_zoom");
                SaveShot("02_tutorial_hud.png");

                controls.Frame(new Vector2(0, 8.6f), 15.0f);
                SaveShot("03_closeup.png");

                ShowBuilding(System.Array.Find(state.Buildings, building => building?.id == "hall"));
                Speak(state.Disciples[0]);
                ShowBuilding(System.Array.Find(state.Buildings, building => building?.id == "hall"));
                SaveShot("04_building_panel.png");
                Application.Quit();
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Capture] {error}");
                Application.Quit(1);
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

            void SaveShot(string file)
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
