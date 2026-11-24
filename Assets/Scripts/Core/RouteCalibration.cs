#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126b 決定 3 (editor only): calibrates the four route thresholds by playing the real <see cref="GameState"/>
    /// month by month under scripted "skilled player" policies (one per route) and logging yearly medians.
    /// Run hidden: Unity -batchmode -nographics -nosound -noaudio -executeMethod Wuxia.RouteCalibration.Run -quit
    /// Output: C:\Dev\wuxia-ops\v1126b_calib\calibration.csv and summary.txt.
    /// </summary>
    public static class RouteCalibration
    {
        private const int Years = 10, Trials = 16;
        private static readonly string[] Policies = { "wealth", "diplomacy", "conquest", "champion", "balanced" };

        private sealed class Sample
        {
            public int silver, reputation, routes, settled, tradeYear, loyal, subjects, held, tier, masters, grand, disciples, ap, income;
            public bool[] met;
        }

        public static void Run()
        {
            string dir = Environment.GetEnvironmentVariable("WUXIA_CALIB_DIR");
            if (string.IsNullOrEmpty(dir)) dir = @"C:\Dev\wuxia-ops\v1126b_calib";
            Directory.CreateDirectory(dir);
            var csv = new StringBuilder("policy,trial,month,silver,reputation,routes,settled,tradeYear,loyal,subjects,held,tier,masters,grand,disciples,ap,income\n");
            var summary = new StringBuilder();
            var data = GameData.Load();
            foreach (string policy in Policies)
            {
                var runs = new List<Sample[]>();
                for (int trial = 0; trial < Trials; trial++)
                {
                    UnityEngine.Random.InitState(1126 + trial * 31 + policy.GetHashCode() % 997);
                    Sample[] months;
                    try { months = Play(data, policy); }
                    catch (Exception error)
                    {
                        Debug.LogError($"[Calib] {policy} trial {trial}: {error}");
                        continue;
                    }
                    runs.Add(months);
                    for (int m = 0; m < months.Length; m++)
                    {
                        var s = months[m];
                        csv.Append($"{policy},{trial},{m + 1},{s.silver},{s.reputation},{s.routes},{s.settled},{s.tradeYear},{s.loyal},{s.subjects},{s.held},{s.tier},{s.masters},{s.grand},{s.disciples},{s.ap},{s.income}\n");
                    }
                }
                summary.AppendLine($"== {policy} ({runs.Count} trials, medians at each year end) ==");
                summary.AppendLine("year silver rep routes settled12 tradeYear loyal subjects held tier masters grand disciples apMax income/mo");
                for (int year = 1; year <= Years; year++)
                {
                    int index = year * 12 - 1;
                    int Med(Func<Sample, int> pick)
                    {
                        var values = runs.Where(run => run.Length > index).Select(run => pick(run[index])).OrderBy(v => v).ToArray();
                        return values.Length == 0 ? -1 : values[values.Length / 2];
                    }
                    summary.AppendLine($"{year,2} {Med(s => s.silver),6} {Med(s => s.reputation),4} {Med(s => s.routes),3} {Med(s => s.settled),3} {Med(s => s.tradeYear),5} " +
                        $"{Med(s => s.loyal),3} {Med(s => s.subjects),3} {Med(s => s.held),3} {RankSystem.At(Med(s => s.tier)),3} {Med(s => s.masters),3} {Med(s => s.grand),3} " +
                        $"{Med(s => s.disciples),3} {Med(s => s.ap),2} {Med(s => s.income),4}");
                }
                for (int r = 0; r < GameState.RouteIds.Length; r++)
                {
                    var firsts = runs.Select(run => Array.FindIndex(run, sample => sample.met[r])).ToArray();
                    var reached = firsts.Where(m => m >= 0).OrderBy(m => m).ToArray();
                    string median = reached.Length == 0 ? "未達成" : $"{(reached[reached.Length / 2] + 1) / 12f:0.0} 年";
                    summary.AppendLine($"  {GameState.RouteName(GameState.RouteIds[r])}：{reached.Length}/{firsts.Length} 局於 {Years} 年內達成，中位 {median}" +
                        (reached.Length > 0 ? $"（最快 {(reached[0] + 1) / 12f:0.0}、最慢 {(reached[reached.Length - 1] + 1) / 12f:0.0}）" : ""));
                }
                summary.AppendLine();
            }
            File.WriteAllText(Path.Combine(dir, "calibration.csv"), csv.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "summary.txt"), summary.ToString(), new UTF8Encoding(false));
            Debug.Log("[Calib] done\n" + summary);
        }

        private static Sample[] Play(GameData data, string policy)
        {
            var s = new GameState(data);
            var hero = s.Protagonist;
            hero.skills = new[] { "qingping" };
            hero.major = "sword";
            // A skilled player's 20 points, tilted toward the route.
            hero.attrs = policy switch
            {
                "wealth" => new[] { 10, 11, 11, 10, 14, 15, 13 },
                "diplomacy" => new[] { 10, 11, 10, 10, 14, 17, 12 },
                "conquest" => new[] { 15, 13, 13, 11, 12, 13, 11 },
                "champion" => new[] { 15, 14, 12, 12, 15, 10, 12 },
                _ => new[] { 12, 12, 12, 11, 14, 14, 13 }
            };
            var map = WorldMapData.Load();
            s_start = s.StartNode;
            var samples = new List<Sample>();
            int recruits = 0;
            for (int month = 0; month < Years * 12; month++)
            {
                int start = s.Silver;
                Spend(s, map, policy, ref recruits);
                if (s.RaidReady) DefendRaid(s);
                if (s.PendingDiploEvent != null) s.ResolveDiploEvent(0);
                s.EndMonth();
                var victory = s.Victory;
                samples.Add(new Sample
                {
                    silver = s.Silver, reputation = s.Reputation, routes = s.TradeRoutes, settled = s.SettledTradeRoutes,
                    tradeYear = s.TradeYear, loyal = victory.Loyal, subjects = victory.Subjects, held = victory.Held,
                    tier = s.HeroMajorTier, masters = s.MastersDefeated, grand = s.GrandmastersDefeated,
                    disciples = s.Disciples.Count, ap = s.ApMax, income = s.Silver - start,
                    met = GameState.RouteIds.Select(id => s.Route(id).Met).ToArray()
                });
            }
            return samples.ToArray();
        }

        private static void Spend(GameState s, WorldMapData map, string policy, ref int recruits)
        {
            int guard = 0;
            // 1) the hero trains (champion twice).
            for (int i = 0; i < (policy == "champion" ? 2 : 1); i++) Train(s);
            while (s.ActionPoints > 0 && guard++ < 20)
            {
                if (Develop(s, policy, ref recruits)) continue;
                if (policy == "wealth" && TradeStep(s, map, includeCourt: true)) continue;
                if (policy == "diplomacy" && DiplomacyStep(s, map)) continue;
                if (policy == "conquest" && ConquestStep(s, map)) continue;
                if (policy == "champion" && ChampionStep(s, map)) continue;
                if (policy == "balanced" && (TradeStep(s, map, false) || DiplomacyStep(s, map) || ChampionStep(s, map))) continue;
                if (policy == "champion" && Train(s)) continue;
                Commerce(s);
            }
        }

        private static bool Train(GameState s)
        {
            if (s.Food < 3 || !s.TrySpendAp(1, "calib:train")) return false;
            s.Pay(new Cost { food = 3 });
            float quality = AssignmentSystem.Predict(s, "train", GameState.HeroId).Quality;
            s.TrainMartial(GameState.HeroId);
            s.Apply(new Yield { reputation = Mathf.RoundToInt(2 * quality) });
            return true;
        }

        private static void Commerce(GameState s)
        {
            if (!s.TrySpendAp(1, "calib:commerce")) return;
            float quality = AssignmentSystem.Predict(s, "commerce", GameState.HeroId).Quality;
            s.Apply(new Yield { silver = Mathf.RoundToInt(5 * quality) });
        }

        /// <summary>Sect growth every policy shares: income buildings, council, techs, expansion, recruits, food.</summary>
        private static bool Develop(GameState s, string policy, ref int recruits)
        {
            if (s.Food < 12 + s.Disciples.Count && Build(s, "kitchen", 0)) return true;
            if (s.Food < 8 + s.Disciples.Count && Build(s, "field", 0)) return true;
            if (s.BuildingLevel("council") is > 0 and < 3 && s.Silver >= 60)
            {
                int before = s.BuildingLevel("council");
                s.UpgradeCouncil();
                if (s.BuildingLevel("council") > before) return true;
            }
            string[] order = policy switch
            {
                "champion" => new[] { "sword1", "sword2", "inner1", "sword3", "sword4", "craft1", "craft2", "craft3", "craft4", "inner2" },
                "conquest" => new[] { "craft1", "sword1", "sword2", "sword3", "outer1", "outer2", "outer3", "outer4", "craft2", "craft3", "craft4" },
                _ => new[] { "craft1", "craft2", "craft3", "craft4", "sword1", "sword2", "sword3", "sword4" }
            };
            foreach (string id in order)
            {
                var node = GameState.Tech(id);
                if (node != null && s.CanResearch(node) && s.Silver >= node.silver + 30)
                {
                    s.Research(node);
                    if (s.HasTech(id)) return true;
                }
            }
            if (s.ExpansionBlock() == null && s.Silver >= 80 && s.ExpandSect(GameState.HeroId)) return true;
            foreach (string id in new[] { "council", "teahouse", "smithy", "steward_office", "hall", "workshop", "infirmary", "quarters", "master_pavilion" })
                if (s.Silver >= 70 && Build(s, id, 40)) return true;
            if (s.Disciples.Count < s.Beds && s.Silver >= 40 && s.Food >= 10 && s.TrySpendAp(1, "calib:recruit"))
            {
                s.Pay(new Cost { silver = s.Data.Start.recruitCost, food = 2 });
                var template = s.Disciples.FirstOrDefault() ?? new DiscipleData();
                s.Disciples.Add(new DiscipleData
                {
                    id = "calib_" + recruits++, name = "新弟子", gender = "男", age = 18, loyalty = 60,
                    portrait = template.portrait, weapon = "sword",
                    attrs = Enumerable.Range(0, AttributeSystem.Count).Select(_ => UnityEngine.Random.Range(7, 14)).ToArray(),
                    lines = RankSystem.Seed(null, "D", "C", RankSystem.BaseCap, RankSystem.BaseRank)
                });
                return true;
            }
            return false;
        }

        private static bool Build(GameState s, string id, int reserve)
        {
            var building = s.Data.Buildings.FirstOrDefault(b => b.id == id);
            if (building == null || !s.IsAvailable(building)) return false;
            int plot = Enumerable.Range(0, s.Buildings.Length).FirstOrDefault(i => s.Buildings[i] == null && s.PlotUnlocked(i));
            if (plot < 0 || s.Buildings[plot] != null || !s.PlotUnlocked(plot)) return false;
            int silver = s.BuildSilver(building.cost.silver);
            if (s.Silver < silver + reserve || !s.TrySpendAp(1, "calib:build")) return false;
            s.Pay(new Cost { silver = silver, food = building.cost.food });
            s.Buildings[plot] = building;
            return true;
        }

        private static bool Act(GameState s, string action, MapFaction faction)
        {
            var data = GameState.DiploActionById(action);
            if (data == null || s.DiploBlock(data, faction) != null || s.Silver < data.silver + 15) return false;
            s.DiploAct(data, faction);
            return true;
        }

        private static IEnumerable<MapFaction> Near(WorldMapData map, Func<MapFaction, bool> filter) =>
            map.Factions.Where(filter).OrderBy(f => map.Route(s_start, f.seat)?.Days ?? 999);

        private static string s_start = "xiaoshan";

        private static bool TradeStep(GameState s, WorldMapData map, bool includeCourt)
        {
            foreach (var f in Near(map, f => !f.Outlaw && (includeCourt || Diplomacy.Jianghu(f))))
            {
                if (!s.IsFactionKnown(f.id)) { if (Act(s, "envoy", f)) return true; continue; }
                if (s.HasTie(f.id, Diplomacy.Trade)) continue;
                if (s.Attitude(f) < 10) { if (Act(s, "gift", f)) return true; continue; }
                if (Act(s, "trade", f)) return true;
            }
            return false;
        }

        private static bool DiplomacyStep(GameState s, WorldMapData map)
        {
            foreach (var f in Near(map, Diplomacy.Jianghu))
            {
                int ties = s.Ties(f.id), attitude = s.Attitude(f);
                if (!s.IsFactionKnown(f.id)) { if (Act(s, "envoy", f)) return true; continue; }
                if ((ties & (Diplomacy.Vassal | Diplomacy.Merged)) != 0) continue;
                if ((ties & Diplomacy.Alliance) != 0)
                {
                    if (s.Victory.Subjects < RouteRules.Data.diplomacy.subjects && Act(s, "vassal", f)) return true;
                    continue;
                }
                if (attitude < 0 && Act(s, "mediate", f)) return true;
                if ((ties & Diplomacy.Trade) == 0 && attitude >= 10 && Act(s, "trade", f)) return true;
                if ((ties & Diplomacy.Pact) == 0 && attitude >= 30 && Act(s, "pact", f)) return true;
                if (attitude >= 50 && Act(s, "alliance", f)) return true;
                if (attitude < 74 && Act(s, "gift", f)) return true;
            }
            return false;
        }

        private static bool ConquestStep(GameState s, WorldMapData map)
        {
            if (s.ActionPoints < 3) return false;
            foreach (var f in Near(map, f => !f.special && !f.Court))
            {
                if (!s.IsFactionKnown(f.id)) { if (Act(s, "envoy", f)) return true; continue; }
                var node = map.Holdings(f).FirstOrDefault(n => n.id != s_start && !s.HoldsNode(n.id));
                if (node == null) continue;
                if (!f.Outlaw && s.Attitude(f) > -25)
                {
                    if (s.Reputation >= 6 && Act(s, "intimidate", f)) return true;
                    continue;
                }
                if (s.HasTie(f.id, Diplomacy.Alliance | Diplomacy.Pact)) continue;
                var party = new[] { GameState.HeroId }.Concat(s.Disciples.Where(d => d.injury < 3)
                    .OrderByDescending(d => RankSystem.Index(RankSystem.Martial(d.lines ?? Array.Empty<SkillLine>()))).Take(4).Select(d => d.id)).ToArray();
                if (s.Protagonist.injury >= 3 || !s.TrySpendAp(3, "calib:campaign")) return false;
                int troops = Mathf.Clamp(f.disciples / 2, 200, 3200);
                bool won = Fight(new SectCampaign(s, party, f, troops, support: s.CampaignSupport));
                if (won)
                {
                    s.Story.heldNodes = (s.Story.heldNodes ?? Array.Empty<string>()).Append(node.id).ToArray();
                    s.ApplyEffects(new[] { new GameEffect { key = "relation", @ref = f.id, value = -25 } });
                    s.Apply(new Yield { reputation = 6, silver = 30, food = 20 });
                }
                else s.Apply(new Yield { reputation = -2 });
                return true;
            }
            return false;
        }

        private static bool Fight(SectCampaign battle)
        {
            battle.Formation = WarFormation.Spear;
            battle.Deploy();
            for (int round = 0; round < 60 && !battle.Finished; round++) battle.Advance(false);
            return battle.Won;
        }

        private static bool ChampionStep(GameState s, WorldMapData map)
        {
            foreach (var f in GameState.Masters.OrderBy(f => f.reputation))
            {
                if (!s.IsFactionKnown(f.id)) { if (Act(s, "envoy", f)) return true; continue; }
                if (s.MasterBlock(f) != null) continue;
                float difficulty = s.MasterDifficulty(f);
                if (!s.BeginMasterChallenge(f)) continue;
                // Duel odds by difficulty (assumed from boss pacing): 1.0 → 75%, 1.2 → 65%, 1.4 → 55%, 1.6 → 45%.
                bool won = UnityEngine.Random.value < Mathf.Clamp(1.25f - .5f * difficulty, .2f, .85f);
                s.ResolveMasterChallenge(f, won);
                return true;
            }
            return false;
        }

        private static void DefendRaid(GameState s)
        {
            var raid = s.Story.sectRaid;
            var foe = GameState.FindFaction(raid.faction);
            var guards = new[] { GameState.HeroId }.Concat(s.Disciples.Where(d => d.injury < 3).Take(4).Select(d => d.id)).ToArray();
            bool won = foe != null && Fight(new SectCampaign(s, guards, foe, raid.troops, raid));
            s.FinishRaid(won);
        }
    }
}
#endif
