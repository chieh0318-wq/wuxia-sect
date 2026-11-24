using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] Pb2CaptureSteps() => new (string, Action)[]
        {
            ("PB2_00_creation_talents.png", () =>
            {
                if (map != null && map.IsOpen) map.Close();
                CleanupV1125PaCapture();
                EnsureCaptureCourtyard();
                CaptureFlow().CaptureTalents();
            }),
            ("PB2_01_hero_talents.png", () =>
            {
                VerifyFactionSimulation();
                VerifyTalentNumbers();
                CleanupV1125PaCapture();
                EnsureCaptureCourtyard();
                state.Protagonist.talents = new[] { "keen_eye", "brute_force" };
                directoryFocus = HeroCharacterId;
                ShowSectRoster();
            }),
            ("PB2_02_guest_talents.png", () => ShowCharacterDirectory(0, state.FriendId)),
            ("PB2_03_master_talents.png", () => ShowCharacterDirectory(0, "master")),
            ("PB2_04_disciple_talents.png", () => ShowCharacterDirectory(0, state.Disciples.First().id)),
            ("PB2_05_ambush.png", () =>
            {
                ui.CloseModal(force: true);
                state.Story.fieldDispatches = Array.Empty<FieldDispatch>();
                state.RefillAp();
                var site = RealmContent.Data.treasures.First(entry =>
                    WorldMapData.Load().Route(state.StartNode, entry.node)?.Nodes.Count > 2);
                state.SendTreasureDispatch(site, new[] { GameState.HeroId });
                RequirePb2(state.IsDispatched(GameState.HeroId), "hero dispatch started");
                var before = (state.Silver, state.Food, state.Reputation);
                state.ForceDispatchAmbush = true;
                var report = state.EndMonth();
                RequirePb2(state.Protagonist.injury == 1 && report.Lines.Any(line => line.Contains("遇伏")),
                    "ambush wound persists after month end");
                ShowMonthReport("門派月報・外派遇伏", report.Lines.OrderByDescending(line => line.Contains("遇伏")).ToList(), before, null);
            }),
            ("PB2_06_envoy.png", () =>
            {
                ui.CloseModal(force: true);
                state.FactionWorld.envoy = new FactionEnvoy
                {
                    faction = "huayue", kind = "ultimatum", due = state.MonthStamp + 1, silver = 35, food = 25
                };
                ShowFactionEnvoy();
            }),
            ("PB2_07_guard_campaign.png", () =>
            {
                ui.CloseModal(force: true);
                RequirePb2(state.IsDispatched(GameState.HeroId), "hero is still away");
                state.AnswerFactionEnvoy("reject");
                var raid = state.Story.sectRaid;
                RequirePb2(raid != null && raid.target == state.StartNode, "ultimatum created a mountain-gate attack");
                raid.due = state.MonthStamp;
                StartDefensiveCampaign();
                RequirePb2(campaignOpen, "remaining disciples can defend while hero is away");
            }),
            ("PB2_08_guard_resolution.png", () =>
            {
                int silver = state.Silver, food = state.Food, reputation = state.Reputation;
                ClickCaptureCaption("開戰");
                ClickCaptureCaption("撤兵");
                RequirePb2(state.Story.sectRaid == null && state.Silver == Math.Max(0, silver - 35) &&
                    state.Food == Math.Max(0, food - 25) && state.Reputation == reputation - 5,
                    "retreat resolves raid and charges defeat losses");
            }),
            ("PB2_09_jianghu_news.png", () =>
            {
                var before = (state.Silver, state.Food, state.Reputation);
                var report = state.EndMonth();
                int headline = report.Lines.IndexOf("江湖大事");
                RequirePb2(headline >= 0, "month report includes faction news");
                ShowMonthReport("門派月報", report.Lines.Skip(headline).ToList(), before, null);
            })
        };

        private static void RequirePb2(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("[PB2] " + description);
            Debug.Log("[PB2] PASS " + description);
        }

        internal static void VerifyFactionSimulation(GameData gameData)
        {
            var oldRandom = UnityEngine.Random.state;
            try
            {
                foreach (int seed in new[] { 1126, 1127, 1128 })
                {
                    UnityEngine.Random.InitState(seed);
                    var simulation = new GameState(gameData);
                    var counts = new System.Collections.Generic.List<int>();
                    int announcements = 0;
                    for (int month = 1; month <= 60; month++)
                    {
                        var report = simulation.EndMonth();
                        announcements += report.Lines.Count(line => line.Contains("宣戰"));
                        if (month == 1 || month % 12 == 0)
                            counts.Add(simulation.FactionWorld.realms.Count(realm => realm.holdings.Length > 0));
                        var offer = simulation.FactionWorld.envoy;
                        if (offer != null) simulation.AnswerFactionEnvoy("reject");
                    }
                    var world = simulation.FactionWorld;
                    Debug.Log($"[PB2] seed={seed} active={string.Join("/", counts)} annexations={world.annexations} declarations={announcements} raids={world.raids}");
                    RequirePb2(counts[2] >= counts[0] / 2 && announcements > 0, "five-year AI simulation has activity and preserves most factions through year two");
                }
            }
            finally { UnityEngine.Random.state = oldRandom; }
        }

        private void VerifyFactionSimulation() => VerifyFactionSimulation(data);

        private void VerifyTalentNumbers()
        {
            var sample = new GameState(data);
            sample.Protagonist.talents = new[] { "brute_force", "heavy_force", "wind_speed", "swift_feet",
                "keen_eye", "side_step", "inspect_iron", "pure_forge" };
            var combatant = sample.ApplyBattleTalents(GameState.HeroId,
                new BattleCombatant { attack = 40, speed = 27, maxHp = 80, maxQi = 50 });
            RequirePb2(combatant.attack == 51 && combatant.speed == 33 &&
                Mathf.Approximately(combatant.talentHit, .06f) && Mathf.Approximately(combatant.dodge, .05f),
                "fixed attack and speed apply before multipliers, and hit/dodge use percentage points");
            RequirePb2(Mathf.Approximately(sample.TalentValue(GameState.HeroId, "forge", 20), 20),
                "over-cap effects in a restored talent list clamp to twenty percent");

            string save = System.IO.Path.Combine(Application.temporaryCachePath, "pb2_" + Guid.NewGuid() + ".json");
            try
            {
                const string olderSave = "{\"version\":1,\"year\":1,\"month\":2,\"buildings\":[],\"disciples\":[],\"story\":{}}";
                System.IO.Directory.CreateDirectory(Application.temporaryCachePath);
                System.IO.File.WriteAllText(save, olderSave);
                var migrated = GameState.LoadSave(data, save);
                RequirePb2(migrated != null && migrated.FactionWorld.difficulty == 1 &&
                    migrated.FactionWorld.realms == null, "legacy save receives default faction settings");
                migrated.EndMonth();
                RequirePb2(migrated.FactionWorld.realms.Length > 0, "legacy save seeds faction state on month end");
                sample.FactionWorld.difficulty = 2;
                sample.EndMonth();
                RequirePb2(sample.Save(save), "temporary PB2 save written");
                var restored = GameState.LoadSave(data, save);
                RequirePb2(restored?.FactionWorld.difficulty == 2 &&
                    restored.FactionWorld.realms.Length == sample.FactionWorld.realms.Length &&
                    restored.Protagonist.talents.SequenceEqual(sample.Protagonist.talents),
                    "faction state and selected talents survive save and reload");
            }
            finally { if (System.IO.File.Exists(save)) System.IO.File.Delete(save); }
        }
    }
}
