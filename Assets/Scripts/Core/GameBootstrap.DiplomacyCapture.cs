using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        /// <summary>
        /// "-wuxiaDiplomacyCapture &lt;dir&gt;": muted, offscreen shots of the diplomacy conversation and 外交急報 on a seeded campaign; quits after verification.
        /// </summary>
        private IEnumerator CaptureDiplomacy(string directory)
        {
            capturing = true;
            yield return new WaitForSecondsRealtime(2);
            Directory.CreateDirectory(directory);
            int width = 1920, height = 1080;
            string[] args = System.Environment.GetCommandLineArgs();
            int option = System.Array.IndexOf(args, "-wuxiaCaptureRes");
            if (option >= 0 && option + 1 < args.Length)
            {
                var size = args[option + 1].Split('x');
                if (size.Length == 2 && int.TryParse(size[0], out int w) && int.TryParse(size[1], out int h) && w > 0 && h > 0)
                {
                    width = w;
                    height = h;
                }
            }
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            var cameraData = view.GetUniversalAdditionalCameraData();
            bool post = cameraData.renderPostProcessing;
            bool ok = true;
            try
            {
                var next = new GameState(data);
                var site = M2Data.Load<PrologueData>("prologue").locations?.FirstOrDefault();
                if (site != null)
                {
                    next.Story.locationId = site.id;
                    next.Story.mapNode = site.node;
                }
                EnterCourtyard(next, false);
                controls.enabled = false;
                ok &= VerifyDiplomacyRules();
                state.SeedDiplomacyDemo();
                ui.Canvas.enabled = false;
                cameraData.renderPostProcessing = false;
                view.targetTexture = target;
                view.aspect = width / (float)height;
                OpenWorldMap();
                var overlay = map.CaptureCanvas;
                overlay.renderMode = RenderMode.ScreenSpaceCamera;
                overlay.worldCamera = view;
                overlay.planeDistance = view.nearClipPlane + .11f;
                yield return null;

                // 01: the faction card (click a banner) with its new 外交 button and attitude value.
                var faction = GameState.FindFaction("wulong") ?? WorldMapData.Load().Factions.First();
                map.FocusFaction(faction);
                yield return null;
                var cardButton = overlay.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "門派外交");
                if (cardButton == null || !cardButton.interactable)
                {
                    Debug.LogError("[Diplomacy] Faction card has no usable 外交 button.");
                    ok = false;
                }
                Shot("00_faction_card.png");

                cardButton?.onClick.Invoke();
                yield return null;
                var panel = map.CaptureDiplomacy;
                var randomState = Random.state;
                Random.InitState(1123);
                int ap = state.ActionPoints, silver = state.Silver, before = state.Attitude(faction);
                Click("外交:gift");
                yield return null;
                Check(state.ActionPoints == ap && state.Silver == silver, "council preview is free");
                Click("再議");
                yield return null;
                Check(!panel.EventOpen && state.ActionPoints == ap && state.Silver == silver, "cancelling council is free");
                Click("外交:gift");
                yield return null;
                Shot("01_council.png");
                yield return AdvanceTo("使者抵達");
                Check(state.ActionPoints == ap - 1 && state.Silver == silver - 25 && state.Attitude(faction) == before,
                    "dispatch pays base costs without settling");
                Click("對話繼續");
                yield return null;
                Shot("02_envoy_arrival.png");
                yield return AdvanceTo("答覆");
                Shot("03_reply_choices.png");
                Click("答覆:lavish");
                yield return null;
                Check(state.Silver == silver - 40 && state.Attitude(faction) == before, "choice pays its extra silver before settlement");
                Click("對話繼續");
                yield return null;
                Shot("04_leader_reaction.png");
                yield return AdvanceTo("結局");
                Shot("05_narrated_outcome.png");
                Check(state.Attitude(faction) == before, "narrated outcome precedes settlement");
                yield return AdvanceTo(null);
                Check(!panel.EventOpen && (state.Attitude(faction) == before + 14 || state.Attitude(faction) == before - 2),
                    "gift settles with its selected modifier");
                Random.state = randomState;

                Check(state.RollDiploEvent("birthday"), "birthday event can be generated");
                panel.ShowEvent();
                yield return null;
                Shot("06_event_arrival.png");
                var host = state.PendingDiploTarget;
                int eventBefore = host == null ? 0 : state.Attitude(host);
                silver = state.Silver;
                yield return AdvanceTo("答覆");
                Click("答覆:lavish");
                yield return null;
                yield return AdvanceTo(null);
                Check(host != null && state.PendingDiploEvent == null && state.Silver == silver - 30 &&
                    state.Attitude(host) == Mathf.Min(100, eventBefore + 12), "birthday event settles through conversation");

                state.RefillAp();
                Check(state.RollDiploEvent("vassal_plea"), "vassal plea can be generated");
                panel.ShowEvent();
                yield return null;
                yield return AdvanceTo("答覆");
                ap = state.ActionPoints;
                var vassal = state.PendingDiploTarget;
                int vassalBefore = state.Attitude(vassal);
                Click("答覆:lead");
                yield return null;
                yield return AdvanceTo(null);
                var relief = Diplomacy.Data.events.First(incident => incident.id == "vassal_plea").options[0];
                int reliefGain = Mathf.RoundToInt(relief.effects.First(effect => effect.key == "attitude").value);
                Check(state.ActionPoints == ap - relief.ap - 1 && state.Attitude(vassal) == Mathf.Min(100, vassalBefore + reliefGain + 5),
                    "leading relief reuses the original option and adds its surcharge and effects");

                void Click(string buttonName)
                {
                    var button = overlay.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == buttonName);
                    Check(button != null && button.interactable, "usable button: " + buttonName);
                    if (button != null && button.interactable) button.onClick.Invoke();
                }

                IEnumerator AdvanceTo(string stage)
                {
                    bool Arrived() => stage == null ? !panel.EventOpen :
                        overlay.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == stage && rect.parent.name == "外交急報");
                    for (int i = 0; i < 20 && !Arrived(); i++)
                    {
                        Click("對話繼續");
                        yield return null;
                    }
                    Check(Arrived(), "arrived at " + (stage ?? "settlement"));
                }

                void Check(bool condition, string message)
                {
                    if (condition) return;
                    Debug.LogError("[Diplomacy] Capture check failed: " + message);
                    ok = false;
                }
                map.Close();
                overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            finally
            {
                RenderTexture.active = previous;
                view.targetTexture = null;
                view.ResetAspect();
                cameraData.renderPostProcessing = post;
                Destroy(pixels);
                Destroy(target);
            }
            Debug.Log(ok ? "[Diplomacy] Capture and checks passed." : "[Diplomacy] Capture finished with errors.");
            Application.Quit(ok ? 0 : 1);

            void Shot(string file)
            {
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(view, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                string path = Path.Combine(directory, file);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log($"[Diplomacy] wrote {path}");
            }
        }

        /// <summary>Rule checks on throwaway states: gating, vassal → merge, month-end tribute, drift and victory counting.</summary>
        private bool VerifyDiplomacyRules()
        {
            bool ok = true;
            void Check(bool condition, string message)
            {
                if (condition) return;
                ok = false;
                Debug.LogError("[Diplomacy] Check failed: " + message);
            }
            var rules = Diplomacy.Data;
            Check(rules.actions.Length == 9 && rules.events.Length >= 5, $"diplomacy.json loaded ({rules.actions.Length} actions, {rules.events.Length} events)");
            var dialogue = DiplomacyDialogue.Data;
            Check(rules.actions.All(action => dialogue.actions.Any(scene => scene.id == action.id && scene.options.Length >= 2)),
                "all nine actions have conversation choices");
            Check(rules.events.All(incident => dialogue.events.Any(scene => scene.id == incident.id && scene.Covers(incident))),
                "all six events have matching conversation choices");
            Check(CastDialogue.Find("lin")?.address?.mid == "老三" && CastDialogue.Find("han")?.daily?.low?.Length > 0,
                "affinity-sensitive addresses and daily lines deserialize");
            var gift = dialogue.actions.First(scene => scene.id == "gift").options.First(choice => choice.id == "lavish");
            Check(Mathf.Approximately(GameState.ModifiedDiploChance(.72f, gift.mods), .78f) &&
                Mathf.Approximately(GameState.ModifiedDiploChance(.94f, gift.mods), .95f),
                "option odds modify the base and respect the upper cap");
            var fresh = new GameState(data);
            var shaolin = GameState.FindFaction("shaolin");
            var court = GameState.FindFaction("jinwuwei");
            var bandit = GameState.FindFaction("heisong");
            Check(fresh.Attitude(shaolin) == 50, "shaolin starts friendly at +50");
            Check(fresh.DiploBlock(GameState.DiploActionById("alliance"), shaolin) != null, "alliance needs trade or pact first");
            Check(fresh.DiploBlock(GameState.DiploActionById("vassal"), court) != null, "court cannot become a vassal");
            Check(fresh.DiploBlock(GameState.DiploActionById("trade"), bandit) != null, "bandits do not trade");
            Check(fresh.DiploBlock(GameState.DiploActionById("gift"), shaolin) == null, "gift to shaolin allowed at start");

            var demo = new GameState(data);
            demo.SeedDiplomacyDemo();
            var pressure = dialogue.actions.First(scene => scene.id == "vassal").options.First(choice => choice.id == "press");
            var awed = GameState.FindFaction("wulong");
            var terms = pressure.mods.Effective(demo, awed, "recluse", "neutral");
            Check(terms.chance == 6 && terms.terms.tributePct == 30, "conditional and permanent negotiation modifiers deserialize");
            var v = demo.Victory;
            Check(v.Loyal == 3 && v.Subjects == 1 && v.Held == 3 && !v.Diplomatic && !v.Conquest,
                $"demo victory counts loyal {v.Loyal} subjects {v.Subjects} held {v.Held}");
            var funiu = GameState.FindFaction("funiu");
            demo.RefillAp();
            var randomState = Random.state;
            Random.InitState(7);
            int held = demo.Story.heldNodes.Length;
            string merge = "";
            for (int i = 0; i < 6 && !demo.HasTie("funiu", Diplomacy.Merged); i++)
            {
                demo.RefillAp();
                // Keep the vassal willing and the coffers full so a failed roll can simply be retried.
                if (demo.Attitude(funiu) < 70)
                    demo.ApplyEffects(new[] { new GameEffect { key = "relation", @ref = "funiu", value = 70 - demo.Attitude(funiu) } });
                demo.ApplyEffects(new[] { new GameEffect { key = "silver", value = 100 } });
                merge = demo.DiploAct(GameState.DiploActionById("merge"), funiu);
            }
            Random.state = randomState;
            Check(demo.HasTie("funiu", Diplomacy.Merged) && demo.Story.heldNodes.Length > held && demo.HoldsNode("funiu"),
                "merging the funiu vassal plants the sect banner on its seat: " + merge);
            int silver = demo.Silver;
            var report = demo.EndMonth();
            Check(report.Lines.Any(line => line.StartsWith("外交收入")) && demo.Silver > 0, "month end pays trade and tribute");
            Debug.Log($"[Diplomacy] Rules verified: {(ok ? "all passed" : "FAILED")}. Month report: " +
                string.Join(" / ", report.Lines.Where(line => line.Contains("外交"))) + $" (silver {silver}->{demo.Silver})");
            return ok;
        }
    }
}
