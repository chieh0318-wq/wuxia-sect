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
    /// v1126e #1 (editor only): before/after battle simulation. Builds every fight with the REAL
    /// <see cref="BattleSetup.Create"/> and plays it with the REAL <see cref="BattleModel"/> (both sides on
    /// <see cref="BattleModel.AutoCommand"/> / enemy AI). 「改前」 flips <see cref="AttributeSystem.LegacyV1126d"/> (內功威力
    /// base 25 × 38/25, single 防禦 = 攻擊 ÷ 3, enemies scaled from 攻擊), 「改後」 is the shipped code.
    /// Entry point: Wuxia.EditorTools.BattleSimV1126e.Run. Output: C:\Dev\wuxia-ops\v1126e_battle_sim.md / .csv.
    /// Env: WUXIA_SIM_FIGHTS (default 200 per cell).
    /// </summary>
    public static class BattleSimV1126eCore
    {
        private static readonly string[] Builds = { "outer", "inner", "balanced" };
        private static readonly string[] BuildNames = { "外功型", "內功型", "均衡型" };
        private static readonly string[] Scenarios = { "normal", "boss", "sparring", "assault" };
        private static readonly string[] ScenarioNames = { "普通", "首領", "切磋", "攻城" };
        private static readonly string[] Stages = { "early", "late" };
        private static readonly string[] StageNames = { "前期（第 6 月）", "後期（第 8 年）" };

        internal struct Cell
        {
            public int fights, wins, rounds;
            public double lost;
            public float WinRate => fights == 0 ? 0 : wins / (float)fights;
            public float Rounds => wins == 0 ? 0 : rounds / (float)wins;
            public float Lost => fights == 0 ? 0 : (float)(lost / fights);
        }

        public static void RunAll()
        {
            int fights = int.TryParse(Environment.GetEnvironmentVariable("WUXIA_SIM_FIGHTS"), out int n) ? n : 200;
            string stem = @"C:\Dev\wuxia-ops\v1126e_battle_sim";
            var data = GameData.Load();
            var catalog = new BattleCatalog();
            var csv = new StringBuilder("stage,scenario,build,version,fights,winRate,avgRoundsToWin,partyHpLostPct,heroAttack,heroInner,heroOuterDef,heroInnerDef\n");
            var results = new Dictionary<(int, int, int, bool), Cell>();
            var heroLine = new Dictionary<(int, int, bool), string>();
            bool saved = AttributeSystem.LegacyV1126d;
            try
            {
                foreach (bool legacy in new[] { true, false })
                {
                    AttributeSystem.LegacyV1126d = legacy;
                    for (int st = 0; st < Stages.Length; st++)
                    for (int b = 0; b < Builds.Length; b++)
                    {
                        var probe = MakeState(data, Builds[b], Stages[st]);
                        var hs = probe.HeroStats;
                        heroLine[(st, b, legacy)] = $"外功 {hs.Attack}・內功 {(legacy ? Mathf.RoundToInt(hs.QiPower * 38f / 25f) : hs.QiPower)}・外防 {hs.OuterDefense}・內防 {hs.InnerDefense}・氣血 {hs.MaxHp}";
                        for (int sc = 0; sc < Scenarios.Length; sc++)
                        {
                            var cell = new Cell();
                            for (int seed = 0; seed < fights; seed++)
                            {
                                var state = MakeState(data, Builds[b], Stages[st]);
                                var request = Request(state, Scenarios[sc], Stages[st]);
                                UnityEngine.Random.InitState(9001 + seed);
                                var (party, enemies) = BattleSetup.Create(state, request, catalog);
                                var (won, rounds, lost) = Fight(party, enemies, catalog, 1126 + seed * 7919 + sc * 104729 + b * 31337);
                                cell.fights++;
                                if (won) { cell.wins++; cell.rounds += rounds; }
                                cell.lost += lost;
                            }
                            results[(st, sc, b, legacy)] = cell;
                            csv.Append($"{Stages[st]},{Scenarios[sc]},{Builds[b]},{(legacy ? "before" : "after")},{cell.fights}," +
                                $"{cell.WinRate:0.000},{cell.Rounds:0.00},{cell.Lost * 100:0.0},{hs.Attack},{hs.QiPower},{hs.OuterDefense},{hs.InnerDefense}\n");
                        }
                    }
                }
            }
            finally { AttributeSystem.LegacyV1126d = saved; }

            var md = new StringBuilder();
            md.AppendLine("# v1126e 戰鬥模擬：外功／內功同底、外防／內防拆分（改前 vs 改後）");
            md.AppendLine();
            md.AppendLine($"產生時間 {DateTime.Now:yyyy-MM-dd HH:mm}。每格 {fights} 場；戰鬥由真正的 BattleSetup.Create 建立、BattleModel 逐回合結算，" +
                "我方以 AutoCommand 出招（與遊戲內「自動」相同），敵方用原本 AI。無丹藥。");
            md.AppendLine("改前 = 47f76b4 公式（內功威力底 25 再 ×38/25、單一防禦 = 攻擊 ÷ 3、敵方依外功攻擊縮放）；改後 = 本版公式。");
            md.AppendLine();
            md.AppendLine("配置（主角＋兩名同門，屬性傾向相同）：外功型 武力 16／內力 8、內功型 內力 16／武力 8、均衡型 各 12（根骨 12）；" +
                "後期主屬性再 +4、根骨 14，武學升至 S／A。外功型只會劍法、內功型以內功招式為主、均衡型兩者各一。");
            md.AppendLine("切磋 = 主角＋一名同門 對 兩名標準同門（全屬性 10、預設武學）；攻城 = BattleRequest.MapAssault（前期守備 .6、後期 .95）。");
            md.AppendLine();
            for (int st = 0; st < Stages.Length; st++)
            {
                md.AppendLine($"## {StageNames[st]}");
                md.AppendLine();
                for (int b = 0; b < Builds.Length; b++)
                    md.AppendLine($"- {BuildNames[b]}主角　改前：{heroLine[(st, b, true)]}　改後：{heroLine[(st, b, false)]}");
                md.AppendLine();
                md.AppendLine("| 場景 | 配置 | 勝率 改前 → 改後 | 平均勝利回合 改前 → 改後 | 隊伍損失氣血 改前 → 改後 | 判定 |");
                md.AppendLine("|---|---|---|---|---|---|");
                for (int sc = 0; sc < Scenarios.Length; sc++)
                for (int b = 0; b < Builds.Length; b++)
                {
                    var before = results[(st, sc, b, true)];
                    var after = results[(st, sc, b, false)];
                    bool winOk = Mathf.Abs(after.WinRate - before.WinRate) <= .10f + 1e-4f;
                    bool roundOk = before.wins == 0 || after.wins == 0 || Mathf.Abs(after.Rounds - before.Rounds) <= 1.0f + 1e-4f;
                    md.AppendLine($"| {ScenarioNames[sc]} | {BuildNames[b]} | {before.WinRate:P0} → {after.WinRate:P0} | " +
                        $"{before.Rounds:0.0} → {after.Rounds:0.0} | {before.Lost:P0} → {after.Lost:P0} | {(winOk && roundOk ? "✓" : "✗ 超出 ±10%／±1 回合")} |");
                }
                md.AppendLine();
            }
            md.AppendLine("## 單一配置是否獨強（改後，同場景三種配置勝率差）");
            md.AppendLine();
            md.AppendLine("| 階段 | 場景 | 外功型 | 內功型 | 均衡型 | 最大差 |");
            md.AppendLine("|---|---|---|---|---|---|");
            for (int st = 0; st < Stages.Length; st++)
            for (int sc = 0; sc < Scenarios.Length; sc++)
            {
                var rates = Enumerable.Range(0, Builds.Length).Select(b => results[(st, sc, b, false)].WinRate).ToArray();
                md.AppendLine($"| {StageNames[st]} | {ScenarioNames[sc]} | {rates[0]:P0} | {rates[1]:P0} | {rates[2]:P0} | {(rates.Max() - rates.Min()) * 100:0} 點 |");
            }
            md.AppendLine();
            File.WriteAllText(stem + ".csv", csv.ToString(), new UTF8Encoding(false));
            File.WriteAllText(stem + ".md", md.ToString(), new UTF8Encoding(false));
            Debug.Log("[BattleSimV1126e] done → " + stem + ".md");
        }

        /// <summary>One fight to the end (or 60 rounds): (won, rounds, party HP lost as a share of party max HP).</summary>
        internal static (bool won, int rounds, double lost) Fight(List<BattleCombatant> party, List<BattleCombatant> enemies,
            BattleCatalog catalog, int seed)
        {
            var model = new BattleModel(party, enemies, catalog, seed, () => false, () => ("", 0, 0));
            int guard = 0;
            while (!model.Finished && model.Round <= 60 && guard++ < 5000)
            {
                if (model.NeedsCommand)
                {
                    var target = enemies.Where(unit => unit.Alive).OrderBy(unit => unit.hp).First();
                    var (command, aim) = model.AutoCommand(target);
                    if (model.Step(command, aim).Count == 0 && model.Reason(command, aim) != "")
                        model.Step("attack", target);
                }
                else model.Step(null, null);
            }
            double max = party.Sum(unit => unit.maxHp);
            double lost = max <= 0 ? 0 : party.Sum(unit => unit.maxHp - unit.hp) / max;
            return (model.Won, model.Round, lost);
        }

        private static readonly Dictionary<string, (int str, int qi, string[] early, string[] late)> BuildSpecs = new()
        {
            ["outer"] = (16, 8, new[] { "qingping", "xiaoshan13" }, new[] { "jianqi_huntuo", "art_765deea9" }),
            ["inner"] = (8, 16, new[] { "xiaoshan_tuna", "guixi" }, new[] { "zifu_xuanyuan", "zhoutian_banyun" }),
            ["balanced"] = (12, 12, new[] { "qingping", "xiaoshan_tuna" }, new[] { "hangu_ziqi", "zifu_xuanyuan" })
        };

        internal static GameState MakeState(GameData data, string build, string stage)
        {
            var state = new GameState(data);
            var spec = BuildSpecs[build];
            bool late = stage == "late";
            int lift = late ? 4 : 0;
            int con = late ? 14 : 12;
            int[] Attrs() => new[] { spec.str + (spec.str > 10 ? lift : 0), 12, con, spec.qi + (spec.qi > 10 ? lift : 0), 11, 10, 10 };
            string[] arts = late ? spec.early.Concat(spec.late).ToArray() : spec.early;
            string Top(string line) => build switch
            {
                "outer" => line == "sword" ? (late ? "S" : "B") : line == "neigong" ? "D" : null,
                "inner" => line == "neigong" ? (late ? "S" : "B") : line == "sword" ? "D" : null,
                _ => line is "sword" or "neigong" ? (late ? "A" : "C") : null
            };
            var hero = state.Protagonist;
            hero.name = "模擬主角";
            hero.gender = "男";
            hero.lookId = hero.portrait = "hero_m1";
            hero.attrs = Attrs();
            hero.skills = arts;
            hero.major = build == "inner" ? "neigong" : "sword";
            foreach (var line in hero.lines)
                if (Top(line.id) is string rank) { line.rank = rank; line.cap = rank; }
            foreach (var member in state.Disciples.Take(2))
            {
                member.attrs = Attrs();
                member.learnedArts = arts;
                member.talents = Array.Empty<string>();
                foreach (var line in member.lines ?? Array.Empty<SkillLine>())
                    if (Top(line.id) is string rank) { line.rank = rank; line.cap = rank; }
            }
            // Sparring opponents: two reference disciples (all attributes 10, their own arts and ranks).
            foreach (var member in state.Disciples.Skip(2).Take(2))
                member.attrs = Enumerable.Repeat(AttributeSystem.Base, AttributeSystem.Count).ToArray();
            return state;
        }

        private static BattleRequest Request(GameState state, string scenario, string stage)
        {
            bool late = stage == "late";
            var ids = new[] { GameState.HeroId }.Concat(state.Disciples.Take(2).Select(d => d.id)).ToArray();
            switch (scenario)
            {
                case "boss":
                    return new BattleRequest { encounter = BattleEncounter.Boss, enemyLeadName = "山寨寨主", enemyCount = 3,
                        difficulty = late ? 1.2f : 1.0f, expeditionParty = ids };
                case "sparring":
                    return new BattleRequest { sparParty = ids.Take(2).ToArray(),
                        sparOpponents = state.Disciples.Skip(2).Take(2).Select(d => d.id).ToArray() };
                case "assault":
                    var request = BattleRequest.MapAssault("山寨", "寨主", "npc_am01", late ? 60 : 30, late ? .95f : .6f);
                    request.expeditionParty = ids;
                    return request;
                default:
                    return new BattleRequest { enemyCount = 3, difficulty = late ? 1.1f : 1.0f, expeditionParty = ids };
            }
        }
    }
}
#endif
