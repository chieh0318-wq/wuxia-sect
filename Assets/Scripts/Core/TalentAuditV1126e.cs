#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126e #6 (editor only): for every talent in talents.json, compares what the player is told (the description for
    /// modifier talents, the EffectText tag line for effect talents) with what the code actually does, by applying the
    /// talent to a fresh <see cref="GameState"/> and measuring the change. Entry point: Wuxia.EditorTools.TalentAuditV1126e.Run.
    /// Output: C:\Dev\wuxia-ops\v1126e_talent_audit.md. Logs 「[TalentAudit] mismatches=N」.
    /// </summary>
    public static class TalentAuditV1126eCore
    {
        private const string AuditId = "recruit_v1126e_audit";

        public static int RunAll()
        {
            var data = GameData.Load();
            var talents = (M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>())
                .Where(option => option != null).ToArray();
            string sources = string.Join("\n", Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs",
                SearchOption.AllDirectories).Where(path => !path.Contains("TalentAuditV1126e"))
                .Select(File.ReadAllText));
            var rows = new List<(string id, string name, string told, string measured, bool ok)>();
            foreach (var talent in talents)
            {
                try
                {
                    if (talent.modifiers is { Length: > 0 }) rows.Add(Modifier(data, talent, sources));
                    if (talent.effects is { Length: > 0 }) rows.Add(Effects(data, talent, sources));
                    if (talent.modifiers is not { Length: > 0 } && talent.effects is not { Length: > 0 })
                        rows.Add((talent.id, talent.name, talent.desc, "沒有任何效果資料", false));
                }
                catch (Exception error)
                {
                    rows.Add((talent.id, talent.name, talent.desc, "例外：" + error.Message, false));
                }
            }
            // Signature talents of the named disciples must exist.
            var known = new HashSet<string>(talents.Select(t => t.id));
            foreach (string id in GameState.NamedDisciples)
                foreach (string talent in TalentRules.Fixed(id).Where(t => !known.Contains(t)))
                    rows.Add((talent, "?", $"{id} 的固定天賦", "talents.json 沒有這個天賦", false));
            // The full new-game path for 王佐之才 (StartStory → Finish → first month end).
            rows.Add(NewGamePath(data, talents.First(t => t.id == "wangzuo")));

            int bad = rows.Count(row => !row.ok);
            var md = new StringBuilder();
            md.AppendLine("# v1126e 天賦稽核：描述與實際效果");
            md.AppendLine();
            md.AppendLine($"產生時間 {DateTime.Now:yyyy-MM-dd HH:mm}。共 {talents.Length} 個天賦、{rows.Count} 項檢查，不一致 {bad} 項。");
            md.AppendLine("modifiers 類（戰鬥／生產加成，弟子也會有）：比對描述裡的數字與資料數值，再把天賦掛到一名測試弟子身上，量 ApplyBattleTalents／TalentValue 的實際變化，並確認程式有讀這個鍵。");
            md.AppendLine("effects 類（創角天賦）：玩家看到的是 EffectText 產生的效果標籤；把效果套到全新 GameState（與 StartStory 相同的 ApplyEffects，再 RefillAp），量實際變化。");
            md.AppendLine();
            md.AppendLine("| 天賦 | 玩家看到 | 實測 | 結果 |");
            md.AppendLine("|---|---|---|---|");
            foreach (var row in rows)
                md.AppendLine($"| {row.name}（{row.id}） | {Cell(row.told)} | {Cell(row.measured)} | {(row.ok ? "✓" : "✗")} |");
            File.WriteAllText(@"C:\Dev\wuxia-ops\v1126e_talent_audit.md", md.ToString(), new UTF8Encoding(false));
            Debug.Log($"[TalentAudit] talents={talents.Length} checks={rows.Count} mismatches={bad}");
            foreach (var row in rows.Where(row => !row.ok))
                Debug.LogWarning($"[TalentAudit] MISMATCH {row.id}: told「{row.told}」 measured「{row.measured}」");
            return bad;

            static string Cell(string text) => (text ?? "").Replace("|", "／").Replace("\n", " ");
        }

        // ── modifier talents ──
        private static readonly string[] BattleKeys =
            { "attack", "attackPct", "speed", "speedPct", "hp", "qiPct", "defense", "dodge", "hit", "crit", "reduction", "basicAttack" };

        private static (string, string, string, string, bool) Modifier(GameData data, CreationOption talent, string sources)
        {
            var state = new GameState(data);
            var seed = state.Disciples.First();
            var probe = JsonUtility.FromJson<DiscipleData>(JsonUtility.ToJson(seed));
            probe.id = AuditId;
            probe.talents = Array.Empty<string>();
            state.Disciples.Add(probe);
            var parts = new List<string>();
            bool ok = true;
            BattleCombatant Unit() => new()
            {
                attack = 100, internalAttack = 100, speed = 50, hp = 200, maxHp = 200, qi = 100, maxQi = 100,
                outerDefense = 20, innerDefense = 20, dodge = .10f
            };
            var before = state.ApplyBattleTalents(AuditId, Unit());
            probe.talents = new[] { talent.id };
            var after = state.ApplyBattleTalents(AuditId, Unit());
            foreach (var modifier in talent.modifiers)
            {
                float v = modifier.value;
                string key = modifier.key;
                float measured;
                string what;
                switch (key)
                {
                    case "attack": measured = after.attack - before.attack; what = "攻擊"; break;
                    case "attackPct": measured = (after.attack / (float)before.attack - 1) * 100; what = "攻擊%"; break;
                    case "speed": measured = after.speed - before.speed; what = "先手"; break;
                    case "speedPct": measured = (after.speed / (float)before.speed - 1) * 100; what = "先手%"; break;
                    case "hp": measured = after.maxHp - before.maxHp; what = "氣血上限"; break;
                    case "qiPct": measured = (after.maxQi / (float)before.maxQi - 1) * 100; what = "內力上限%"; break;
                    case "defense":
                        measured = after.outerDefense - before.outerDefense;
                        what = $"外防 {after.outerDefense - before.outerDefense:+0}・內防 {after.innerDefense - before.innerDefense:+0}；";
                        ok &= after.innerDefense - before.innerDefense == Mathf.RoundToInt(v);
                        what += "外防";
                        break;
                    case "dodge": measured = (after.dodge - before.dodge) * 100; what = "閃避點"; break;
                    case "hit": measured = (after.talentHit - before.talentHit) * 100; what = "命中點"; break;
                    case "crit": measured = (after.talentCrit - before.talentCrit) * 100; what = "暴擊點"; break;
                    case "reduction": measured = (after.talentReduction - before.talentReduction) * 100; what = "減傷%"; break;
                    case "basicAttack": measured = after.talentBasicAttack - before.talentBasicAttack; what = "普攻攻擊"; break;
                    default:
                        // Production / travel keys: the summed talent value, and a code path that reads the key.
                        measured = state.TalentValue(AuditId, key, 999);
                        what = $"TalentValue(\"{key}\")";
                        bool read = Regex.IsMatch(sources, $"TalentValue\\([^;]*\"{Regex.Escape(key)}\"") ||
                                    (key is "farming" or "commerce" or "teaching" or "construction" &&
                                     sources.Contains("TalentValue(worker, id, 20)")) ||
                                    (key == "forge" && sources.Contains("skill == \"forging\" ? \"forge\""));
                        if (!read) { ok = false; parts.Add($"程式沒有讀取「{key}」"); }
                        break;
                }
                bool match = Mathf.Abs(measured - v) <= Mathf.Max(.051f, Mathf.Abs(v) * .02f);
                ok &= match;
                parts.Add($"{what} {measured:+0.##;-0.##;0}（資料 {v:0.##}）");
                // The description must quote the value (growth is a switch, described in words).
                if (key == "growth")
                {
                    bool coded = sources.Contains("Year <= 3 ? .9f : 1.3f");
                    bool said = talent.desc.Contains("前三年修練 −10%") && talent.desc.Contains("第四年起＋30%");
                    ok &= coded && said;
                    parts.Add(coded ? "程式：前三年 ×0.9、第四年起 ×1.3" : "程式沒有 ×0.9／×1.3");
                    continue;
                }
                var numbers = Regex.Matches(talent.desc ?? "", @"\d+(\.\d+)?").Select(m => float.Parse(m.Value)).ToArray();
                bool quoted = numbers.Any(number => Mathf.Abs(number - Mathf.Abs(v)) <= .5f + Mathf.Abs(v) * .01f);
                if (!quoted) { ok = false; parts.Add("描述沒有寫出這個數值"); }
            }
            return (talent.id, talent.name, talent.desc, string.Join("；", parts), ok);
        }

        // ── effect talents (creation) ──
        private static (string, string, string, string, bool) Effects(GameData data, CreationOption talent, string sources)
        {
            var state = new GameState(data);
            state.Protagonist.talents = new[] { talent.id };
            var before = Snapshot(state);
            var fresh = new GameState(data);
            state.ApplyEffects(talent.effects);
            state.RefillAp(); // NewGameFlow.Finish
            var after = Snapshot(state);
            var parts = new List<string>();
            bool ok = true;
            var told = string.Join("、", talent.effects.Select(e => EffectText.Describe(e, new GameState(data))).Where(s => s.Length > 0));
            // Several effects can share a key (e.g. two studyPct); compare the summed expectation per key + ref.
            foreach (var group in talent.effects.GroupBy(e => (e.key, e.@ref ?? "")))
            {
                var (key, @ref) = group.Key;
                float v = group.Sum(e => e.value);
                int iv = Mathf.RoundToInt(v);
                float measured;
                string what;
                int slot = AttributeSystem.Index(key);
                if (slot >= 0) { measured = after.attrs[slot] - before.attrs[slot]; what = AttributeSystem.Names[slot]; }
                else switch (key)
                {
                    case "apMax":
                        measured = after.apMax - before.apMax;
                        what = $"行動力上限（{before.apMax}→{after.apMax}，本月 {after.ap}）";
                        if (after.ap != after.apMax) { ok = false; parts.Add($"本月行動力 {after.ap} ≠ 上限 {after.apMax}"); }
                        break;
                    case "silver": measured = after.silver - before.silver; what = "銀兩"; break;
                    case "food": measured = after.food - before.food; what = "糧食"; break;
                    case "reputation":
                        measured = after.reputation - before.reputation;
                        what = "聲望";
                        // EffectText shows ReputationEffect(value) (gain scale and 魅力), the same figure Apply adds.
                        v = fresh.ReputationEffect(iv);
                        break;
                    case "recruitPoolSize": measured = state.Story.recruitPoolSize - fresh.Story.recruitPoolSize; what = "招徒人選"; break;
                    case "recruitCostPct": measured = state.Story.recruitCostPct - fresh.Story.recruitCostPct; what = "招徒費用%"; break;
                    case "buildCostPct": measured = state.Story.buildCostPct - fresh.Story.buildCostPct; what = "建造費用%"; break;
                    case "trainBonusPct": measured = state.Story.trainBonusPct - fresh.Story.trainBonusPct; what = "修習進度%"; break;
                    case "insightChance": measured = state.Story.insightChance - fresh.Story.insightChance; what = "感悟機率"; break;
                    case "loyaltyAll": measured = state.Story.loyaltyBonus - fresh.Story.loyaltyBonus; what = "全門忠誠（新弟子）"; break;
                    case "noGraveInjury": measured = state.Protagonist.noGraveInjury ? 1 : 0; what = "不會重傷"; break;
                    case "relation":
                        measured = (state.Story.relations.FirstOrDefault(r => r.id == @ref)?.value ?? 0) -
                                   (fresh.Story.relations.FirstOrDefault(r => r.id == @ref)?.value ?? 0);
                        what = $"{@ref} 關係";
                        break;
                    case "lineRank":
                    case "rank":
                    case "cap":
                    {
                        var b = before.lines.FirstOrDefault(l => l.id == Line(state, @ref));
                        var a = state.Protagonist.lines.FirstOrDefault(l => l.id == Line(state, @ref));
                        measured = key == "cap" ? RankSystem.Index(a?.cap) - RankSystem.Index(b?.cap)
                            : RankSystem.Index(a?.rank) - RankSystem.Index(b?.rank);
                        what = $"{EffectText.LineName(Line(state, @ref))}{(key == "cap" ? "上限" : "階位")}（{b?.rank}/{b?.cap}→{a?.rank}/{a?.cap}）";
                        break;
                    }
                    case "studyPct":
                        what = $"修習速度%（{(string.IsNullOrEmpty(@ref) ? "全部" : @ref)}）";
                        measured = StudyFor(state, @ref) - StudyFor(fresh, @ref);
                        break;
                    default: measured = float.NaN; what = $"未知鍵 {key}"; break;
                }
                bool match = !float.IsNaN(measured) && Mathf.Abs(measured - v) <= Mathf.Max(.001f, Mathf.Abs(v) * .02f);
                ok &= match;
                parts.Add($"{what} {measured:+0.###;-0.###;0}（資料 {v:0.###}）");
                // Something must read the value afterwards.
                string field = key switch
                {
                    "recruitPoolSize" => "Story.recruitPoolSize", "recruitCostPct" => "Story.recruitCostPct",
                    "buildCostPct" => "Story.buildCostPct", "trainBonusPct" => "Story.trainBonusPct",
                    "insightChance" => "Story.insightChance", "loyaltyAll" => "Story.loyaltyBonus",
                    "apMax" => "Story.apBonus", _ => null
                };
                if (field != null && Regex.Matches(sources, Regex.Escape(field)).Count < 2)
                {
                    ok = false;
                    parts.Add($"程式沒有讀取 {field}");
                }
            }
            return (talent.id, talent.name, told, string.Join("；", parts), ok);
        }

        private static string Line(GameState state, string @ref) => @ref == "martial"
            ? RankSystem.Major(state.Protagonist.lines, state.Protagonist.major)?.id : @ref;

        private static float StudyFor(GameState state, string @ref)
        {
            string key = state.StudyKeyForAudit(@ref);
            return (state.Protagonist.study ?? Array.Empty<StoryValue>()).FirstOrDefault(s => s?.id == key)?.value ?? 0;
        }

        private sealed class Snap
        {
            public int[] attrs;
            public int apMax, ap, silver, food, reputation;
            public SkillLine[] lines;
        }

        private static Snap Snapshot(GameState state) => new()
        {
            attrs = state.HeroAttributes.ToArray(), apMax = state.ApMax, ap = state.ActionPoints, silver = state.Silver,
            food = state.Food, reputation = state.Reputation,
            lines = state.Protagonist.lines.Select(l => new SkillLine { id = l.id, rank = l.rank, cap = l.cap }).ToArray()
        };

        /// <summary>王佐之才 through the real new-game order: talents applied, the story's Finish refill, the first month end.</summary>
        private static (string, string, string, string, bool) NewGamePath(GameData data, CreationOption talent)
        {
            var plain = new GameState(data);
            plain.RefillAp();
            int baseline = plain.ApMax;
            var state = new GameState(data);
            state.Protagonist.talents = new[] { talent.id };
            state.ApplyEffects(talent.effects);
            int afterEffects = state.ActionPoints;
            state.RefillAp();
            int start = state.ActionPoints, max = state.ApMax;
            state.EndMonth();
            int refill = state.ActionPoints;
            bool ok = max == baseline + 1 && start == max && refill == state.ApMax && afterEffects == max;
            return (talent.id + "（開局流程）", talent.name, $"行動力上限 +1（無天賦時上限 {baseline}）",
                $"套用天賦當下本月 {afterEffects}／{max}；開局 {start}／{max}；首月結算後補回 {refill}／{state.ApMax}；明細：{state.ApDescription.Replace("\n", "、")}", ok);
        }
    }
}
#endif
