using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private string ActionPreview(ActionData action)
        {
            string detail = action.id switch
            {
                "train" => $"全門各修習 1 路，基礎進度 +{BalanceV1126c.Martial.drillProgress}；授業 B 以上進度再 +{BalanceV1126c.Martial.teacherBonusPct}%",
                "study" => $"所選門人研讀 1 路，基礎進度 +{BalanceV1126c.Martial.practiceProgress}",
                "explore" => "武學奇遇與秘境入口分開判定。選人時可查成敗機率。",
                "lodge" => $"每月 1 次，弟子忠誠 +{GameState.LodgeLoyalty}、傷勢 −{GameState.LodgeHealing}",
                "medicine" => "主角與弟子傷勢下降，減幅依選派門人的醫術而定",
                "feast" => $"弟子忠誠 +{GameState.FeastLoyalty}；主角傷勢 −{state.RecoveryAmount(null).Hero + GameState.FeastExtraHealing}、弟子傷勢 −{state.RecoveryAmount(null).Members}",
                "decree" => $"弟子忠誠 +{GameState.DecreeLoyalty}",
                "forge_gear" => "鍛成裝備 1 件，選人時可查看各品級的機率",
                "gather_rumors" => $"下次奇遇的傳聞加成 {state.Story.rumorBonus:P0} → {state.NextRumorBonus:P0}，另有機會探得秘境",
                "talk" => "門中問敘，銀兩、糧食、聲望皆 +0",
                _ => action.summary
            };
            string numbers = Changes(action.effect);
            if (!string.IsNullOrEmpty(numbers))
                detail += "　基礎：" + numbers;
            string skill = AssignmentSystem.Relevance(action.id).Skill;
            if (skill != null && !RankSystem.IsCombat(skill))
                detail += $"　{EffectText.LineName(skill)}經驗 +{BalanceV1126c.LifeSkill.xpPerTask}";
            return detail;
        }

        private string AssignedPreview(ActionData action, string worker)
        {
            var output = AssignedYield(action, AssignmentSystem.Predict(state, action.id, worker).Quality);
            var parts = new List<string> { Changes(output) };
            if (action.id == "train")
            {
                var line = state.CurrentStudyLine(GameState.HeroId);
                float baseGain = state.DrillGain(worker);
                float heroGain = GameState.PracticeProgressFor(line, baseGain * state.StudyMultiplier(line?.id ?? ""));
                var gains = state.SparCandidates.Where(id => id != GameState.HeroId && !state.IsDispatched(id) && id != state.Story.delegateId)
                    .Select(id => GameState.PracticeProgressFor(state.CurrentStudyLine(id), baseGain)).DefaultIfEmpty(0).ToArray();
                parts.Add($"各自修習 1 路　主角進度 +{heroGain:0.#}／弟子 +{gains.Min():0.#}～{gains.Max():0.#}");
            }
            if (action.id == "study")
                parts.Add($"{state.StudyLabel(worker)}　進度 +{GameState.PracticeProgressFor(state.CurrentStudyLine(worker), state.TrainingGain(worker, state.CurrentStudyLine(worker), state.CurrentStudy(worker, out _))):0.#}");
            if (action.id == "medicine")
            {
                var heal = state.RecoveryAmount(worker);
                int visits = state.HasTech("craft1") ? 2 : 1;
                parts.Add($"主角傷勢 −{heal.Hero * visits}、弟子傷勢 −{heal.Members * visits}");
            }
            return string.Join("　", parts.Where(text => !string.IsNullOrEmpty(text)));
        }

        private Yield AssignedYield(ActionData action, float quality) => new()
        {
            silver = Mathf.RoundToInt(action.effect.silver * quality),
            food = Mathf.RoundToInt(action.effect.food * quality),
            materials = Mathf.RoundToInt(action.effect.materials * quality),
            reputation = Mathf.RoundToInt(action.effect.reputation * quality)
        };

        private IEnumerable<string> DrillSect(string leader)
        {
            float gain = state.DrillGain(leader);
            foreach (string id in state.SparCandidates.Where(id => !state.IsDispatched(id) && id != state.Story.delegateId))
            {
                if (id == GameState.HeroId)
                {
                    var line = state.CurrentStudyLine(id);
                    string result = state.TrainMartial(id, fixedGain: gain * state.StudyMultiplier(line?.id ?? ""));
                    int breakthrough = result.IndexOf("突破：", StringComparison.Ordinal);
                    if (breakthrough >= 0) yield return result.Substring(breakthrough);
                }
                else state.PracticeMartial(id, gain);
            }
        }

        private Dictionary<(string Owner, string Stat), string> ActionSnapshot()
        {
            var readings = new Dictionary<(string Owner, string Stat), string>
            {
                [(null, "行動力")] = state.ActionPoints.ToString(), [(null, "銀兩")] = state.Silver.ToString(),
                [(null, "糧食")] = state.Food.ToString(), [(null, "聲望")] = state.Reputation.ToString(),
                [(null, "材料")] = state.Story.materials.ToString(), [(null, "傳聞加成")] = $"{state.Story.rumorBonus:P0}",
                [(null, "門人")] = state.Disciples.Count.ToString(), [(GameState.HeroId, "傷勢")] = state.Protagonist.injury.ToString()
            };
            foreach (var member in state.Disciples)
            {
                readings[(member.id, "忠誠")] = member.loyalty.ToString();
                readings[(member.id, "傷勢")] = member.injury.ToString();
            }
            foreach (var id in new[] { GameState.HeroId }.Concat(state.Disciples.Select(member => member.id)))
            {
                readings[(id, "本月餘次")] = AssignmentSystem.Remaining(state, id).ToString();
                foreach (var line in state.MemberLines(id).Where(line => line != null))
                {
                    bool martial = RankSystem.IsCombat(line.id);
                    float progress = martial ? line.progress : state.WorkPractice(id, line.id)?.xp ?? 0;
                    int max = martial ? GameState.MartialProgressMax : GameState.LifeProgressMax;
                    readings[(id, EffectText.LineName(line.id))] = $"{line.rank}／{line.cap}　{progress:0.#}／{max}";
                }
            }
            return readings;
        }

        private List<string> ActionChanges(Dictionary<(string Owner, string Stat), string> before) =>
            ActionSnapshot().Where(entry => before.TryGetValue(entry.Key, out var old) && old != entry.Value)
                .Select(entry =>
                {
                    var (owner, stat) = entry.Key;
                    string label = owner == null ? stat : $"{state.SparName(owner)}・{stat}";
                    return $"{label}　{before[entry.Key]} → {entry.Value}";
                }).ToList();

        private void ShowActionResult(string name, Dictionary<(string Owner, string Stat), string> before, string note,
            Action after = null)
        {
            var lines = ActionChanges(before);
            if (!string.IsNullOrWhiteSpace(note)) lines.Add(note.Trim());
            if (lines.Count == 0) lines.Add("此番事畢，門中數值無變。");
            ui.ShowCreation(name + "・結果", lines, new[] { ("收卷", (Action)(() =>
            {
                ui.CloseModal(force: true);
                after?.Invoke();
            })) }, null);
        }

        private void OpenStudyOrders(string member, Action back)
        {
            var arts = state.TrainingManuals(member).ToArray();
            ui.ShowCreation("指派修習", new[] { state.StudyLabel(member),
                arts.Length == 0 ? "尚無已習得的功法，可先修習基礎武學。" : "修滿目前上限後，門人將自選下一門武學。" },
                new[] { ("恢復自選", (Action)(() => Select(null))), ("返回", back) }, null);
            foreach (var art in arts)
            {
                var line = state.MemberLines(member).FirstOrDefault(entry => entry.id == art.category);
                Action run = GameState.CanAdvance(line) ? () => Select(art.id) : null;
                ui.CreationChoice(art.name, GameState.MartialLineProgress(line), null, run);
            }
            void Select(string id)
            {
                if (!state.AssignStudy(member, id)) return;
                if (!capturing) state.Save();
                back();
            }
        }
    }
}
