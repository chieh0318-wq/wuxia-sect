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
        /// "-wuxiaDiplomacyCapture &lt;dir&gt;": muted, offscreen shots of the diplomacy UI on a seeded mid-game state
        /// (faction card button, diplomacy panel, victory routes, a 外交急報), plus logic checks; quits when done.
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
                Shot("01_faction_card_diplomacy_button.png");

                // 02: the panel opened from that button, after one real gift so the result line shows.
                cardButton?.onClick.Invoke();
                yield return null;
                var panel = map.CaptureDiplomacy;
                if (!panel.IsOpen)
                {
                    Debug.LogError("[Diplomacy] The card button did not open the diplomacy panel.");
                    ok = false;
                }
                var randomState = Random.state;
                Random.InitState(1123);
                int ap = state.ActionPoints, silver = state.Silver, before = state.Attitude(faction);
                string gift = panel.Act("gift");
                Random.state = randomState;
                bool paid = state.ActionPoints == ap - 1 && state.Silver == silver - 25 && state.Attitude(faction) != before;
                Debug.Log($"[Diplomacy] Gift on {faction.Label}: {gift} AP {ap}->{state.ActionPoints}, silver {silver}->{state.Silver}, " +
                    $"attitude {before}->{state.Attitude(faction)}.");
                if (!paid)
                {
                    Debug.LogError("[Diplomacy] Gift did not spend 1 AP / 25 silver or move attitude.");
                    ok = false;
                }
                yield return null;
                Shot("02_diplomacy_panel.png");

                // 03: the 勝利之路 tab — conquest and diplomatic victory side by side.
                panel.ShowVictoryTab();
                yield return null;
                Shot("03_victory_progress.png");

                // 04: a 外交急報 (forced 掌門壽宴 on a friendly sect), then its resolution.
                panel.Select(faction);
                if (!state.RollDiploEvent("birthday"))
                {
                    Debug.LogError("[Diplomacy] No faction qualified for the 掌門壽宴 event.");
                    ok = false;
                }
                panel.ShowEvent();
                yield return null;
                Shot("04_diplomacy_event.png");
                var host = state.PendingDiploTarget;
                int eventBefore = host == null ? 0 : state.Attitude(host);
                silver = state.Silver;
                string outcome = panel.Resolve(0);
                Debug.Log($"[Diplomacy] Event resolved: {outcome}");
                if (host == null || state.PendingDiploEvent != null || state.Silver != silver - 30 || state.Attitude(host) != Mathf.Min(100, eventBefore + 12))
                {
                    Debug.LogError("[Diplomacy] Event option did not apply its cost and attitude change.");
                    ok = false;
                }
                yield return null;
                Shot("05_diplomacy_event_result.png");
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
