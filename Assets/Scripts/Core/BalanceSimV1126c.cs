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
    /// v1126c PA3 (editor only): balance simulation. Plays the REAL <see cref="GameState"/> month by month — month-end
    /// tick (<see cref="GameState.EndMonth"/>: building income, upkeep, trade, held nodes, faction AI, raids), the real
    /// action data from actions.json (cost, AP, reputation × assignment quality), <see cref="GameState.TrainMartial"/>,
    /// <see cref="GameState.Research"/>, diplomacy actions, campaigns, master duels — under scripted play styles.
    /// Entry point: Wuxia.EditorTools.BalanceSimV1126c.Run (Assets/Editor). Output: v1126c2_balance_sim.md/.csv.
    /// Env: WUXIA_SIM_TAG (file suffix, e.g. "old"), WUXIA_SIM_YEARS (default 15), WUXIA_SIM_SEEDS (default 20).
    /// </summary>
    public static class BalanceSimCore
    {
        internal static readonly string[] Styles = { "balanced", "martial", "merchant", "diplomat", "conqueror" };
        private static readonly string[] StyleNames = { "均衡經營", "武學速成", "經商致富", "合縱外交", "攻城略地" };
        private static readonly string[] Sources = { "train", "action", "develop", "trade", "diplomacy", "conquest", "champion", "month" };

        private sealed class Sample
        {
            public int silver, food, reputation, members, topTier, heroTier, heroCap, techs, maxTechTier, routes, settled,
                tradeYear, loyal, subjects, held, masters, grand, ap, income;
            public bool[] met;
        }

        private sealed class Run
        {
            public Sample[] months;
            public readonly Dictionary<string, int> repBySource = Sources.ToDictionary(s => s, _ => 0);
            public int firstTier4 = -1, firstSss = -1;
        }

        private static string style;
        private static Run current;

        public static void RunAll()
        {
            string tag = Environment.GetEnvironmentVariable("WUXIA_SIM_TAG") ?? "";
            int years = int.TryParse(Environment.GetEnvironmentVariable("WUXIA_SIM_YEARS"), out int y) ? y : 15;
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("WUXIA_SIM_SEEDS"), out int n) ? n : 20;
            string dir = @"C:\Dev\wuxia-ops";
            string stem = Path.Combine(dir, "v1126c2_balance_sim" + (tag.Length > 0 ? "_" + tag : ""));
            BalanceV1126c.Reload();
            var data = GameData.Load();
            var csv = new StringBuilder("style,seed,month,silver,food,reputation,members,topTier,heroTier,heroCap,techs,maxTechTier,routes,settled,tradeYear,loyal,subjects,held,masters,grand,apMax,netSilver," +
                string.Join(",", GameState.RouteIds.Select(id => "met_" + id)) + "\n");
            var md = new StringBuilder();
            md.AppendLine($"# v1126c 平衡模擬（{(tag.Length > 0 ? tag : "new")}）");
            md.AppendLine();
            md.AppendLine($"產生時間 {DateTime.Now:yyyy-MM-dd HH:mm}。{years} 年 × {seeds} 個種子 × {Styles.Length} 種玩法；每月走真正的 GameState.EndMonth。");
            md.AppendLine($"種子 = 1126 + 7919×seed + 104729×玩法序號。聲望倍率 reputation.gainScalePct = {BalanceV1126c.Reputation.gainScalePct}%。");
            md.AppendLine();
            var routeTable = new List<string>();
            for (int si = 0; si < Styles.Length; si++)
            {
                style = Styles[si];
                var runs = new List<Run>();
                for (int seed = 0; seed < seeds; seed++)
                {
                    UnityEngine.Random.InitState(1126 + 7919 * seed + 104729 * si);
                    current = new Run();
                    try { current.months = Play(data, years); }
                    catch (Exception error) { Debug.LogError($"[BalanceSim] {style} seed {seed}: {error}"); continue; }
                    runs.Add(current);
                    for (int m = 0; m < current.months.Length; m++)
                    {
                        var s = current.months[m];
                        csv.Append($"{style},{seed},{m + 1},{s.silver},{s.food},{s.reputation},{s.members},{RankSystem.At(s.topTier)},{RankSystem.At(s.heroTier)},{RankSystem.At(s.heroCap)},{s.techs},{s.maxTechTier},{s.routes},{s.settled},{s.tradeYear},{s.loyal},{s.subjects},{s.held},{s.masters},{s.grand},{s.ap},{s.income}," +
                            string.Join(",", s.met.Select(b => b ? 1 : 0)) + "\n");
                    }
                }
                md.AppendLine($"## {StyleNames[si]}（{style}，{runs.Count} 局，各年末中位數）");
                md.AppendLine();
                md.AppendLine("| 年 | 銀兩 | 糧食 | 聲望 | 門人 | 最高階 | 主角主修/上限 | 科技數 | 最高科技重 | 商路(滿期) | 年商路收入 | 盟/附 | 據點 | 勝高手(宗師) | 行動力 | 月淨銀 |");
                md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                for (int year = 1; year <= years; year++)
                {
                    int index = year * 12 - 1;
                    int Med(Func<Sample, int> pick)
                    {
                        var values = runs.Where(r => r.months.Length > index).Select(r => pick(r.months[index])).OrderBy(v => v).ToArray();
                        return values.Length == 0 ? -1 : values[values.Length / 2];
                    }
                    md.AppendLine($"| {year} | {Med(s => s.silver)} | {Med(s => s.food)} | {Med(s => s.reputation)} | {Med(s => s.members)} | {RankSystem.At(Med(s => s.topTier))} | " +
                        $"{RankSystem.At(Med(s => s.heroTier))}/{RankSystem.At(Med(s => s.heroCap))} | {Med(s => s.techs)} | {Med(s => s.maxTechTier)} | {Med(s => s.routes)}({Med(s => s.settled)}) | {Med(s => s.tradeYear)} | " +
                        $"{Med(s => s.loyal)}/{Med(s => s.subjects)} | {Med(s => s.held)} | {Med(s => s.masters)}({Med(s => s.grand)}) | {Med(s => s.ap)} | {Med(s => s.income)} |");
                }
                md.AppendLine();
                md.AppendLine("破關之路達成年（首次達標月 ÷ 12）：");
                md.AppendLine();
                for (int r = 0; r < GameState.RouteIds.Length; r++)
                {
                    var firsts = runs.Select(run => Array.FindIndex(run.months, s => s.met[r])).ToArray();
                    string line = Percentiles(firsts, runs.Count);
                    md.AppendLine($"- {GameState.RouteName(GameState.RouteIds[r])}：{line}");
                    routeTable.Add($"| {StyleNames[si]} | {GameState.RouteName(GameState.RouteIds[r])} | {line} |");
                }
                md.AppendLine($"- 第四重科技首次研成：{Percentiles(runs.Select(run => run.firstTier4).ToArray(), runs.Count)}");
                md.AppendLine($"- 主角主修首達 SSS：{Percentiles(runs.Select(run => run.firstSss).ToArray(), runs.Count)}");
                md.AppendLine();
                md.AppendLine("聲望來源（每局平均，全期）：" + string.Join("、", Sources.Select(src =>
                    $"{SourceName(src)} {(runs.Count == 0 ? 0 : runs.Average(run => run.repBySource[src])):0}")));
                md.AppendLine();
            }
            md.AppendLine("## 總表：各玩法破關年");
            md.AppendLine();
            md.AppendLine("| 玩法 | 破關之路 | 達成局數／中位年（P10–P90） |");
            md.AppendLine("|---|---|---|");
            foreach (string row in routeTable) md.AppendLine(row);
            File.WriteAllText(stem + ".csv", csv.ToString(), new UTF8Encoding(false));
            File.WriteAllText(stem + ".md", md.ToString(), new UTF8Encoding(false));
            Debug.Log("[BalanceSim] done → " + stem + ".md");
        }

        private static string SourceName(string src) => src switch
        {
            "train" => "督促操練", "action" => "其他門內指令", "develop" => "營建科技招收", "trade" => "通商",
            "diplomacy" => "外交", "conquest" => "攻討", "champion" => "挑戰高手", _ => "月結（建築・事件・防守）"
        };

        private static string Percentiles(int[] firstMonths, int total)
        {
            var reached = firstMonths.Where(m => m >= 0).OrderBy(m => m).ToArray();
            if (reached.Length == 0) return $"0/{total} 局達成";
            float Year(int i) => (reached[Mathf.Clamp(i, 0, reached.Length - 1)] + 1) / 12f;
            // Unreached runs count as "later than the horizon" for the percentiles.
            var all = firstMonths.Select(m => m < 0 ? int.MaxValue : m).OrderBy(m => m).ToArray();
            string P(float q)
            {
                int v = all[Mathf.Clamp(Mathf.FloorToInt(q * (all.Length - 1) + .5f), 0, all.Length - 1)];
                return v == int.MaxValue ? "未達" : $"{(v + 1) / 12f:0.0}";
            }
            return $"{reached.Length}/{total} 局達成，中位 {P(.5f)} 年（P10 {P(.1f)}–P90 {P(.9f)}；最快 {Year(0):0.0}）";
        }

        private static void Track(GameState s, string source, Action step)
        {
            int before = s.Reputation;
            step();
            current.repBySource[source] += s.Reputation - before;
        }

        private static bool Track(GameState s, string source, Func<bool> step)
        {
            int before = s.Reputation;
            bool done = step();
            current.repBySource[source] += s.Reputation - before;
            return done;
        }

        private static Sample[] Play(GameData data, int years)
        {
            var s = new GameState(data);
            var hero = s.Protagonist;
            hero.skills = new[] { "qingping" };
            hero.major = "sword";
            // 20 creation points tilted toward the style (order: 武力 身法 根骨 內力 悟性 魅力 幸運).
            hero.attrs = style switch
            {
                "merchant" => new[] { 10, 11, 11, 10, 14, 15, 13 },
                "diplomat" => new[] { 10, 11, 10, 10, 14, 17, 12 },
                "conqueror" => new[] { 15, 13, 13, 11, 12, 13, 11 },
                "martial" => new[] { 15, 14, 12, 12, 15, 10, 12 },
                _ => new[] { 12, 12, 12, 11, 14, 14, 13 }
            };
            var map = WorldMapData.Load();
            start = s.StartNode;
            var samples = new List<Sample>();
            int recruits = 0;
            for (int month = 0; month < years * 12; month++)
            {
                int silverBefore = s.Silver;
                Spend(s, map, ref recruits);
                Track(s, "month", () =>
                {
                    if (s.RaidReady) DefendRaid(s);
                    if (s.PendingDiploEvent != null) s.ResolveDiploEvent(0);
                    s.EndMonth();
                });
                var victory = s.Victory;
                int maxTier = GameState.TechTree.Where(node => s.HasTech(node.id)).Select(node => node.tier).DefaultIfEmpty(0).Max();
                if (maxTier >= 4 && current.firstTier4 < 0) current.firstTier4 = month;
                if (s.HeroMajorTier >= RankSystem.Index("SSS") && current.firstSss < 0) current.firstSss = month;
                samples.Add(new Sample
                {
                    silver = s.Silver, food = s.Food, reputation = s.Reputation, members = s.Disciples.Count + 1,
                    topTier = Math.Max(s.HeroMajorTier, s.Disciples.Select(d => RankSystem.Index(RankSystem.Martial(d.lines ?? Array.Empty<SkillLine>()))).DefaultIfEmpty(0).Max()),
                    heroTier = s.HeroMajorTier, heroCap = RankSystem.Index(s.HeroMajorLine?.cap),
                    techs = s.TechCount, maxTechTier = maxTier,
                    routes = s.TradeRoutes, settled = s.SettledTradeRoutes, tradeYear = s.TradeYear,
                    loyal = victory.Loyal, subjects = victory.Subjects, held = victory.Held,
                    masters = s.MastersDefeated, grand = s.GrandmastersDefeated, ap = s.ApMax, income = s.Silver - silverBefore,
                    met = GameState.RouteIds.Select(id => s.Route(id).Met).ToArray()
                });
            }
            return samples.ToArray();
        }

        private static string start = "xiaoshan";

        private static void Spend(GameState s, WorldMapData map, ref int recruits)
        {
            int guard = 0;
            int drills = style == "martial" ? 2 : 1;
            for (int i = 0; i < drills; i++) Track(s, "train", () => Train(s));
            while (s.ActionPoints > 0 && guard++ < 20)
            {
                int r = recruits;
                bool developed = Track(s, "develop", () => Develop(s, ref r));
                recruits = r;
                if (developed) continue;
                if (style == "merchant" && Track(s, "trade", () => TradeStep(s, map, includeCourt: true))) continue;
                if (style == "diplomat" && Track(s, "diplomacy", () => DiplomacyStep(s, map))) continue;
                if (style == "conqueror" && Track(s, "conquest", () => ConquestStep(s, map))) continue;
                if (style == "martial" && Track(s, "champion", () => ChampionStep(s, map))) continue;
                if (style == "balanced" && (Track(s, "trade", () => TradeStep(s, map, false)) ||
                    Track(s, "diplomacy", () => DiplomacyStep(s, map)) || Track(s, "champion", () => ChampionStep(s, map)))) continue;
                // Martial rush drills the extra AP only once the next 劍宗 node is paid for; otherwise it earns silver for it.
                if (style == "martial" && !SavingForSword(s) && Track(s, "train", () => Train(s))) continue;
                Track(s, "action", () => Commerce(s));
            }
        }

        private static bool SavingForSword(GameState s)
        {
            var next = GameState.TechTree.FirstOrDefault(node => node.branch == "sword" && !s.HasTech(node.id));
            return next != null && s.Silver < next.silver + 30;
        }

        private static ActionData Action(GameState s, string id) => s.Data.Actions.FirstOrDefault(a => a.id == id);

        /// <summary>督促操練 exactly as GameBootstrap runs it: pay the action cost and AP, TrainMartial, reputation × quality.</summary>
        private static bool Train(GameState s)
        {
            var action = Action(s, "train");
            if (action == null || !s.CanPay(action.cost) || !s.TrySpendAp(action.apCost, "sim:train")) return false;
            s.Pay(action.cost);
            float quality = AssignmentSystem.Predict(s, "train", GameState.HeroId).Quality;
            var line = s.CurrentStudyLine(GameState.HeroId);
            float gain = s.DrillGain(GameState.HeroId);
            s.TrainMartial(GameState.HeroId, fixedGain: gain * s.StudyMultiplier(line?.id ?? ""));
            foreach (var id in s.SparCandidates.Where(id => id != GameState.HeroId && !s.IsDispatched(id) && id != s.Story.delegateId))
                s.PracticeMartial(id, gain);
            s.Apply(new Yield { reputation = Mathf.RoundToInt(action.effect.reputation * quality) });
            return true;
        }

        private static void Commerce(GameState s)
        {
            var action = Action(s, "commerce");
            int ap = action?.apCost ?? 1;
            if (!s.TrySpendAp(ap, "sim:commerce")) return;
            float quality = AssignmentSystem.Predict(s, "commerce", GameState.HeroId).Quality;
            s.Apply(new Yield { silver = Mathf.RoundToInt((action?.effect.silver ?? 5) * quality),
                reputation = Mathf.RoundToInt((action?.effect.reputation ?? 0) * quality) });
        }

        private static string[] TechOrder => style switch
        {
            "martial" => new[] { "sword1", "sword2", "inner1", "sword3", "sword4", "craft1", "craft2", "craft3", "craft4", "inner2" },
            "conqueror" => new[] { "craft1", "sword1", "sword2", "sword3", "outer1", "outer2", "outer3", "outer4", "craft2", "craft3", "craft4" },
            _ => new[] { "craft1", "craft2", "craft3", "craft4", "sword1", "sword2", "sword3", "sword4" }
        };

        private static bool Develop(GameState s, ref int recruits)
        {
            if (s.Food < 12 + s.Disciples.Count && Build(s, "kitchen", 0)) return true;
            if (s.Food < 8 + s.Disciples.Count && Build(s, "field", 0)) return true;
            if (s.BuildingLevel("council") is > 0 and < 3 && s.Silver >= 60)
            {
                int before = s.BuildingLevel("council");
                s.UpgradeCouncil();
                if (s.BuildingLevel("council") > before) return true;
            }
            foreach (string id in TechOrder)
            {
                var node = GameState.Tech(id);
                if (node != null && s.CanResearch(node) && s.Silver >= node.silver + 30)
                {
                    s.Research(node);
                    if (s.HasTech(id)) return true;
                }
            }
            if (s.ExpansionBlock() == null && s.Silver >= 80 && s.ExpandSect(GameState.HeroId)) return true;
            foreach (string id in new[] { "library", "council", "teahouse", "smithy", "steward_office", "hall", "workshop", "infirmary", "quarters", "master_pavilion" })
                if (s.Silver >= 70 && Build(s, id, 40)) return true;
            if (s.Disciples.Count < s.Beds && s.Silver >= 40 && s.Food >= 10 && s.TrySpendAp(1, "sim:recruit"))
            {
                s.Pay(new Cost { silver = s.Data.Start.recruitCost, food = 2 });
                var template = s.Disciples.FirstOrDefault() ?? new DiscipleData();
                s.Disciples.Add(new DiscipleData
                {
                    id = "sim_" + recruits++, name = "新弟子", gender = "男", age = 18, loyalty = 60,
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
            int plot = Enumerable.Range(0, s.Buildings.Length).Where(i => s.CanPlaceInCells(i, building) && s.PlotUnlocked(i)).DefaultIfEmpty(-1).First();
            if (plot < 0) return false;
            int silver = s.BuildSilver(building.cost.silver);
            var action = Action(s, "build");
            if (s.Silver < silver + reserve || !s.TrySpendAp(action?.apCost ?? 1, "sim:build")) return false;
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
            map.Factions.Where(filter).OrderBy(f => map.Route(start, f.seat)?.Days ?? 999);

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
                var node = map.Holdings(f).FirstOrDefault(n => n.id != start && !s.HoldsNode(n.id));
                if (node == null) continue;
                if (!f.Outlaw && s.Attitude(f) > -25)
                {
                    if (s.Reputation >= 6 && Act(s, "intimidate", f)) return true;
                    continue;
                }
                if (s.HasTie(f.id, Diplomacy.Alliance | Diplomacy.Pact)) continue;
                var party = new[] { GameState.HeroId }.Concat(s.Disciples.Where(d => d.injury < 3)
                    .OrderByDescending(d => RankSystem.Index(RankSystem.Martial(d.lines ?? Array.Empty<SkillLine>()))).Take(4).Select(d => d.id)).ToArray();
                if (s.Protagonist.injury >= 3 || !s.TrySpendAp(3, "sim:campaign")) return false;
                int troops = Mathf.Clamp(f.disciples / 2, 200, 3200);
                bool won = Fight(new SectCampaign(s, party, f, troops, support: s.CampaignSupport));
                // Same outcome literals as GameBootstrap.Campaign (victory / defeat).
                if (won)
                {
                    s.Story.heldNodes = (s.Story.heldNodes ?? Array.Empty<string>()).Append(node.id).ToArray();
                    s.ApplyEffects(new[] { new GameEffect { key = "relation", @ref = f.id, value = -25 } });
                    s.Apply(new Yield { reputation = GameState.CampaignWinReputation, silver = 30, food = 20 });
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
                // Duel odds by difficulty (boss pacing assumption): 1.0 → 75%, 1.2 → 65%, 1.4 → 55%, 1.6 → 45%.
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
