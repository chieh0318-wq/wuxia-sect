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
                Shot("10_map_faction_card.png");

                cardButton?.onClick.Invoke();
                yield return null;
                var panel = map.CaptureDiplomacy;
                var randomState = Random.state;
                Random.InitState(1123);
                panel.Select(GameState.FindFaction("shaolin"));
                yield return null;
                Shot("11_diplomacy_roster.png");
                panel.Select(faction);
                Click("外交:gift");
                yield return null;
                Shot("12_action_grid_advisors.png");
                var kang = state.Disciples.FirstOrDefault(d => d.id == "kang");
                int priorLoyalty = kang?.loyalty ?? 0;
                Check(kang != null, "Kang is available for affinity comparisons");
                if (kang != null)
                {
                    SetKangAffinity(10);
                    Click("外交:gift");
                    yield return null;
                    Shot("22_affinity_low_advisors.png");
                    SetKangAffinity(85);
                    Click("外交:gift");
                    yield return null;
                    Shot("23_affinity_high_advisors.png");
                    kang.loyalty = priorLoyalty;
                }
                panel.ShowVictoryTab();
                yield return null;
                Shot("13_victory_road.png");
                panel.Select(faction);
                yield return null;
                int ap = state.ActionPoints, silver = state.Silver, before = state.Attitude(faction);
                Click("外交:gift");
                Click("入席議事");
                yield return null;
                Check(state.ActionPoints == ap && state.Silver == silver, "council preview is free");
                Click("再議");
                yield return null;
                Check(!panel.EventOpen && state.ActionPoints == ap && state.Silver == silver, "cancelling council is free");
                Click("外交:gift");
                Click("入席議事");
                yield return null;
                Shot("17_council_home_hall.png");
                yield return AdvanceTo("使者抵達");
                Check(state.ActionPoints == ap - 1 && state.Silver == silver - 25 && state.Attitude(faction) == before,
                    "dispatch pays base costs without settling");
                Click("對話繼續");
                yield return null;
                Shot("18_visit_daoist_wulong.png");
                yield return AdvanceTo("答覆");
                Shot("03_reply_choices.png");
                Click("答覆:lavish");
                yield return null;
                Check(state.Silver == silver - 40 && state.Attitude(faction) == before, "choice pays its extra silver before settlement");
                Click("對話繼續");
                yield return null;
                var nextLabel = overlay.GetComponentsInChildren<Button>().First(b => b.name == "對話繼續")
                    .GetComponentInChildren<Text>().text;
                Check(nextLabel == "繼續", "leader reaction advances with 繼續");
                Shot("19_reaction_continue_label.png");
                yield return AdvanceTo("結局");
                Shot("05_narrated_outcome.png");
                Check(state.Attitude(faction) == before, "narrated outcome precedes settlement");
                yield return AdvanceTo(null);
                Check(!panel.EventOpen && (state.Attitude(faction) == before + 14 || state.Attitude(faction) == before - 2),
                    "gift settles with its selected modifier");
                Random.state = randomState;

                Check(state.RollDiploEvent("birthday"), "birthday event can be generated");
                // Pin this capture to the Emei incident to verify its nun envoy and abbess separately.
                state.Story.diploEventTarget = "emei";
                panel.ShowEvent();
                yield return null;
                Check(ShowsPortrait("使者來報", "npc_nun01") && !ShowsPortrait("使者來報", "leader_emei") &&
                    TalkPage("使者來報").GetComponentsInChildren<Text>().Any(label => label.text == "峨眉劍宗使者"),
                    "Emei birthday announcement pairs the envoy name with the envoy portrait");
                Shot("20_event_envoy_portrait.png");
                var host = state.PendingDiploTarget;
                int eventBefore = host == null ? 0 : state.Attitude(host);
                silver = state.Silver;
                yield return AdvanceTo("答覆");
                Click("答覆:lavish");
                yield return null;
                Click("對話繼續");
                yield return null;
                Check(ShowsPortrait("對方回應", "leader_emei") && !ShowsPortrait("對方回應", "npc_nun01"),
                    "Emei birthday reaction uses the speaking abbess's portrait");
                Shot("24_event_abbess_portrait.png");
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

                RectTransform TalkPage(string stage) => overlay.GetComponentsInChildren<RectTransform>()
                    .FirstOrDefault(rect => rect.name == stage && rect.parent.name == "外交急報");

                bool ShowsPortrait(string stage, string id) => TalkPage(stage)?.GetComponentsInChildren<RawImage>()
                    .Any(img => img.texture != null && img.texture.name == id) == true;

                void Click(string buttonName)
                {
                    var button = overlay.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == buttonName);
                    Check(button != null && button.interactable, "usable button: " + buttonName);
                    if (button != null && button.interactable) button.onClick.Invoke();
                }

                IEnumerator AdvanceTo(string stage)
                {
                    bool Arrived() => stage == null ? !panel.EventOpen :
                        TalkPage(stage) != null;
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

                var sectCanvas = ui.Canvas;
                sectCanvas.enabled = true;
                sectCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                sectCanvas.worldCamera = view;
                sectCanvas.planeDistance = view.nearClipPlane + .11f;
                ShowSect();
                yield return new WaitForSecondsRealtime(.5f);
                Shot("14_sect_management.png");
                ui.CloseModal(force: true);
                if (kang != null)
                {
                    state.RefillAp();
                    SetKangAffinity(10);
                    Random.InitState(1123);
                    Speak(kang);
                    yield return null;
                    Shot("15_affinity_low_courtyard.png");
                    state.RefillAp();
                    SetKangAffinity(85);
                    Random.InitState(1123);
                    Speak(kang);
                    yield return null;
                    Shot("16_affinity_high_courtyard.png");
                    kang.loyalty = priorLoyalty;
                    ui.CloseDialogue();
                }

                var board = MapUi.Docket(sectCanvas.transform, "戰鬥立牌", 313, 100, 1294, 880, false);
                MapUi.Text(board, "盟軍戰鬥立牌　／　朝右", 50, 22, 1194, 64, 40, MapUi.Gold, TextAnchor.MiddleCenter);
                Standee("wei", "魏老七", 124);
                Standee("cui", "崔鈺", 700);
                yield return null;
                Shot("21_standees_right.png");
                Destroy(board.gameObject);
                sectCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                void SetKangAffinity(int score)
                {
                    kang.loyalty = score - state.CharacterRelation(kang.id);
                    Check(CastDialogue.Score(state, kang) == score, "capture affinity: " + score);
                }

                void Standee(string id, string name, float x)
                {
                    var texture = Resources.Load<Texture2D>("Standees/" + id + "_battle_right");
                    Check(texture != null, "right-facing standee exists for " + name);
                    if (texture == null) return;
                    const float height = 686;
                    float width = height * texture.width / texture.height;
                    var image = MapUi.Rect(board, name, x + (470 - width) / 2, 96, width, height)
                        .gameObject.AddComponent<RawImage>();
                    image.texture = texture;
                    image.raycastTarget = false;
                    MapUi.Text(board, name, x, 797, 470, 55, 32, MapUi.Paper, TextAnchor.MiddleCenter);
                }
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
