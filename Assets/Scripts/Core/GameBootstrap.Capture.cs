using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private bool capturing;

        private IEnumerator Capture(string directory)
        {
            capturing = true;
            yield return new WaitForSecondsRealtime(2);
            Directory.CreateDirectory(directory);
            int width = 1920, height = 1080;
            string[] args = System.Environment.GetCommandLineArgs();
            int option = System.Array.IndexOf(args, "-wuxiaCaptureRes");
            if (option >= 0 && option + 1 < args.Length)
            {
                string[] dimensions = args[option + 1].Split('x');
                if (dimensions.Length == 2 && int.TryParse(dimensions[0], out int w) &&
                    int.TryParse(dimensions[1], out int h) && w > 0 && h > 0 &&
                    w <= SystemInfo.maxTextureSize && h <= SystemInfo.maxTextureSize)
                {
                    width = w;
                    height = h;
                }
            }
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

                var creation = new NewGameFlow(ui, new GameState(data), _ => { });
                var steps = new (string filename, System.Action show)[]
                {
                    ("01_title.png", () => { }),
                    ("18_gender.png", () => { ui.CloseTitle(); creation.Begin(); }),
                    ("02_appearance.png", () => { ui.CloseTitle(); creation.CaptureAppearance(); }),
                    ("24_creation_preview.png", () =>
                    {
                        var preview = new GameState(data);
                        preview.Protagonist.gender = "女";
                        preview.Protagonist.title = "大師姐";
                        preview.Protagonist.name = "顧清霜";
                        new NewGameFlow(ui, preview, _ => { }).CaptureAppearance();
                    }),
                    ("03_talents.png", creation.CaptureTalents),
                    ("13_skills.png", creation.CaptureSkills),
                    ("14_attributes.png", creation.CaptureAttributes),
                    ("15_background.png", creation.CaptureBackground),
                    ("04_prologue_beat.png", creation.CapturePrologue),
                    ("05_disaster_choice.png", creation.CaptureDisaster),
                    ("16_response_choice.png", creation.CaptureResponse),
                    ("06_location_choice.png", creation.CaptureLocation),
                    ("07_courtyard_hud_ap.png", () =>
                    {
                        ui.CloseStoryBeat();
                        ui.CloseModal(force: true);
                        // Same defaults as skipping the prologue: first starting site, chosen look.
                        var next = new GameState(data);
                        var site = M2Data.Load<PrologueData>("prologue").locations?.FirstOrDefault();
                        if (site != null)
                        {
                            next.Story.locationId = site.id;
                            next.Story.mapNode = site.node;
                        }
                        EnterCourtyard(next, true);
                        controls.enabled = false;
                        tutorial?.ChooseForCapture();
                        tutorial?.Record("camera_move");
                        tutorial?.Record("camera_zoom");
                    }),
                    ("08_building_panel.png", () =>
                    {
                        var hall = System.Array.Find(state.Buildings, building => building?.id == "hall");
                        if (hall != null)
                        {
                            tutorial?.Record("click_building", hall.id);
                            ShowBuilding(hall);
                        }
                    }),
                    ("09_dialogue_portrait.png", () =>
                    {
                        if (state.Disciples.Count > 0)
                            Speak(state.Disciples[0]);
                    }),
                    ("17_recruit_methods.png", () =>
                    {
                        ui.CloseDialogue();
                        OpenRecruitment(data.Actions.First(action => action.type == "recruit"));
                    }),
                    ("10_recruit.png", CaptureRecruitment),
                    ("19_special_recruit.png", () =>
                    {
                        var special = Recruitment.specials?.FirstOrDefault(candidate => candidate.story != null);
                        if (special != null) ReviewCandidate(special);
                    }),
                    ("11_options_audio.png", ui.ShowAudioOptions),
                    ("12_protagonist_panel.png", ShowProtagonist)
                };

                foreach (var step in steps)
                {
                    step.show();
                    yield return new WaitForSecondsRealtime(step.filename == "04_prologue_beat.png" ? 2.8f : .15f);
                    if (!SaveShot(step.filename))
                        yield break;
                }
                bool shotsOk = true;
                ui.CloseModal(force: true);
                yield return CaptureM2(file => shotsOk &= SaveShot(file));
                if (!shotsOk)
                    yield break;
                Application.Quit(VerifyCapturedGame(directory) ? 0 : 1);
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
                ui.PositionHighlight(tutorial?.Highlight, view, world);
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
        private bool VerifyCapturedGame(string directory)
        {
            string path = Path.Combine(directory, "capture-check-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var build = System.Array.Find(data.Actions, action => action.type == "build");
                var plot = System.Array.IndexOf(state.Buildings, null);
                if (build == null || plot < 0)
                    throw new System.InvalidOperationException("The tutorial requires a build action and an empty plot.");
                ui.CloseModal(force: true);
                BeginBuild(build);
                buildChoice = System.Array.Find(data.Buildings, building => building.id == "council");
                if (buildChoice == null)
                    throw new System.InvalidOperationException("Council configuration is unavailable.");
                PlaceBuilding(plot);
                if (state.Buildings[plot]?.id != "council" || state.TrySpendAp(state.ApMax + 1, "capture-check"))
                    throw new System.InvalidOperationException("AP spending or building placement failed.");

                state.TrySpendAp(state.ActionPoints, "capture-check");
                var method = activeMethod;
                candidates = null;
                Visit(method);
                int before = state.Disciples.Count;
                if (candidates == null || candidates.Length == 0)
                    throw new System.InvalidOperationException("The tutorial could not open recruitment with zero AP.");
                var acceptedCandidate = candidates[0];
                AcceptCandidate(acceptedCandidate);
                AcceptCandidate(acceptedCandidate);
                if (state.Disciples.Count != before + 1 || !candidates.Contains(acceptedCandidate) ||
                    RecruitRestriction(acceptedCandidate) != Recruited)
                    throw new System.InvalidOperationException("The tutorial could not accept a recruit.");
                ui.CloseModal(force: true);
                state.EndMonth();
                tutorial?.Record("end_month");
                tutorial?.Record("read_report");
                var recruit = state.Disciples[before];
                if (tutorial?.Active == true || state.ActionPoints != state.ApMax || !state.Save(path))
                    throw new System.InvalidOperationException("The tutorial, AP refill, or save failed.");
                var restored = GameState.LoadSave(data, path);
                if (restored == null || restored.ActionPoints != state.ActionPoints || restored.Month != state.Month ||
                    !restored.Disciples.Exists(member => member.id == recruit.id && member.aptitude?.bone == recruit.aptitude?.bone))
                    throw new System.InvalidOperationException("The save did not restore the generated recruit, date, and AP.");
                Debug.Log("[Capture] AP, tutorial completion and generated recruit save/load smoke passed.");
                return true;
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Capture] Smoke failed: {error}");
                return false;
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".tmp");
            }
        }
    }
}
