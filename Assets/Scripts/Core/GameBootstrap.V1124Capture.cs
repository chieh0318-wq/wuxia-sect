using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1124 P1B-2 capture shots 80–89: 山道巡訪 scene (dialogue, choices, rewards), a 秘境探尋 floor, a 寶物秘境 guardian
    /// and its claim page, the world map with treasure seals, the 山門演武 picker and result, and the 堂口 page.
    /// Each step also checks the rule it shows. Run alone with "-wuxiaCaptureOnly 8".
    /// </summary>
    public sealed partial class GameBootstrap
    {
        /// <summary>Clicks a named button on the sect canvas (scene pages name their buttons).</summary>
        private bool CaptureClick(string name)
        {
            var button = ui.Canvas.GetComponentsInChildren<Button>(true)
                .LastOrDefault(entry => entry.name == name && entry.interactable && entry.gameObject.activeInHierarchy);
            if (button == null)
            {
                Debug.LogError($"[Capture] v1124 button not found: {name}");
                return false;
            }
            button.onClick.Invoke();
            return true;
        }

        private void CaptureClickThrough(int times)
        {
            for (int i = 0; i < times; i++)
                if (ui.Canvas.GetComponentsInChildren<Button>().All(entry => entry.name != "場景繼續") || !CaptureClick("場景繼續"))
                    return;
        }

        /// <summary>A courtyard state for the v1124 shots when the capture runs them alone.</summary>
        private void EnsureCaptureCourtyard()
        {
            ui.CloseStoryBeat();
            ui.CloseModal(force: true);
            if (state != null && courtyardReady) return;
            var next = new GameState(data);
            var site = M2Data.Load<PrologueData>("prologue").locations?.FirstOrDefault();
            if (site != null)
            {
                next.Story.locationId = site.id;
                next.Story.mapNode = site.node;
            }
            next.Protagonist.name = "盧凝雪";
            next.Protagonist.gender = "女";
            next.Protagonist.title = "代掌門";
            next.Protagonist.lookId = next.Protagonist.portrait = "hero_f1";
            EnterCourtyard(next, false);
            controls.enabled = false;
        }

        private (string filename, Action show)[] V1124CaptureSteps() => new (string, Action)[]
        {
            ("80_c15_road_dialogue.png", () =>
            {
                EnsureCaptureCourtyard();
                if (string.IsNullOrEmpty(state.Protagonist.name)) state.Protagonist.name = "盧凝雪";
                var road = RealmContent.Data.roadEvents.FirstOrDefault(entry => entry.id == "road_robbery") ?? RealmContent.PickRoadEvent();
                Debug.Log($"[Capture] v1124 C15: {RealmContent.Data.roadEvents.Length} road events, " +
                    $"{RealmContent.Data.treasures.Length} treasure realms, {HallCatalog.Data.halls.Length} halls.");
                PlayRoadEvent(road, "", false);
            }),
            ("81_c15_road_choices.png", () => CaptureClickThrough(1)),
            ("82_c15_road_result.png", () =>
            {
                int silver = state.Silver;
                CaptureClick("場景選項:報出門派名號喝退");
                CaptureClickThrough(2);
                Debug.Log($"[Capture] v1124 C15 road result: silver {silver} → {state.Silver}, injury {state.Protagonist.injury}.");
            }),
            ("83_c15_realm_floor.png", () =>
            {
                CaptureClick("結果確定");
                EnterSecretRealm("循著江湖傳聞");
                realmRun.realm = SecretRealms.All.First(realm => realm.id == "sword_tomb");
                ui.Scene.Begin(realmRun.realm.name, "江湖秘境", realmRun.realm.scene, HeroFigure());
                realmRun.stage = 2;
                ShowRealmStage();
                if (!ui.Canvas.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("場景選項:拔劍一戰", StringComparison.Ordinal)))
                    Debug.LogError("[Capture] v1124 C15: boss floor has no battle option.");
            }),
            ("84_c16_treasure_guardian.png", () =>
            {
                LeaveRealm();
                var site = RealmContent.Treasure("tr_zhongnan_guan");
                state.PartyNode = site.node;
                state.RefillAp();
                string block = state.TreasureBlock(site);
                if (block != null) Debug.LogError("[Capture] v1124 C16 treasure blocked: " + block);
                EnterTreasure(site);
                CaptureClickThrough(1);
                CaptureClick("場景選項:" + site.stages[0].choices[0].label);
                CaptureClickThrough(2);
            }),
            ("85_c16_treasure_claim.png", () =>
            {
                int arts = state.Protagonist.arts?.Length ?? 0;
                ClaimTreasure();
                var site = RealmContent.Treasure("tr_zhongnan_guan");
                if (!state.TreasureClaimed(site.id) || state.TreasureBlock(site) == null)
                    Debug.LogError("[Capture] v1124 C16: treasure not marked as taken.");
                else
                    Debug.Log($"[Capture] v1124 C16 claimed {site.name}: arts {arts} → {state.Protagonist.arts?.Length ?? 0}, " +
                        $"taken {state.TreasuresClaimed}/{RealmContent.Data.treasures.Length}.");
            }),
            ("86_c16_map_treasures.png", () =>
            {
                CaptureClick("結果確定");
                ui.Canvas.enabled = false;
                OpenWorldMap();
                var overlay = map.CaptureCanvas;
                overlay.renderMode = RenderMode.ScreenSpaceCamera;
                overlay.worldCamera = view;
                overlay.planeDistance = view.nearClipPlane + .11f;
                map.CaptureTreasure("tr_taibai_elixir");
            }),
            ("87_c17_spar_picker.png", () =>
            {
                map.CaptureCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                map.Close();
                ui.Canvas.enabled = true;
                state.RefillAp();
                state.Apply(new Yield { food = 20 });
                StartSparring();
                ToggleSpar(GameState.HeroId);
                ToggleSpar("lin");
                if (state.SparBlock(sparPicks, false) != null) Debug.LogError("[Capture] v1124 C17 picker blocked: " + state.SparBlock(sparPicks, false));
            }),
            ("88_c17_spar_result.png", () =>
            {
                int ap = state.ActionPoints, relation = state.CharacterRelation("lin");
                var line = RankSystem.BestCombat(state.Disciples.First(d => d.id == "lin").lines);
                float progress = line?.progress ?? 0;
                string rank = line?.rank;
                RunSparSimulation();
                CaptureClickThrough(12);
                Debug.Log($"[Capture] v1124 C17 spar: AP {ap} → {state.ActionPoints}, 林照好感 {relation} → {state.CharacterRelation("lin")}, " +
                    $"林照 {rank}/{progress:0.0} → {line?.rank}/{line?.progress:0.0}.");
                if (state.ActionPoints != ap - state.SparApCost || state.CharacterRelation("lin") <= relation)
                    Debug.LogError("[Capture] v1124 C17: sparring did not cost AP or move favour.");
            }),
            ("89_c18_halls.png", () =>
            {
                if (ui.SceneOpen) CloseScene();
                state.Apply(new Yield { silver = 400, food = 40, reputation = 30 });
                foreach (string id in new[] { "training_hall", "pharmacy_hall" })
                {
                    state.RefillAp();
                    Debug.Log("[Capture] v1124 C18: " + state.BuildHall(GameState.Hall(id)));
                }
                state.RefillAp();
                var report = new MonthlyReport();
                state.HallsMonth(report);
                Debug.Log("[Capture] v1124 C18 month: " + string.Join(" / ", report.Lines));
                if (state.HallLevel("training_hall") != 1 || state.HallLevel("guest_hall") != 0 || report.Lines.Count == 0)
                    Debug.LogError("[Capture] v1124 C18: hall levels or monthly effects are wrong.");
                OpenHalls();
            }),
            ("89b_c18_hall_targets.png", () =>
            {
                UseHall(GameState.Hall("training_hall"), GameState.Hall("training_hall").functions
                    .First(function => function.id == "seclusion"));
            })
        };
    }
}
