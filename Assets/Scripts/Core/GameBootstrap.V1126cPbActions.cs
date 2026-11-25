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
                    if (!string.IsNullOrWhiteSpace(result)) yield return result.Trim();
                }
                else state.PracticeMartial(id, gain);
            }
        }

        private Dictionary<string, string> ActionSnapshot()
        {
            var readings = new Dictionary<string, string>
            {
                ["行動力"] = state.ActionPoints.ToString(), ["銀兩"] = state.Silver.ToString(), ["糧食"] = state.Food.ToString(),
                ["聲望"] = state.Reputation.ToString(), ["材料"] = state.Story.materials.ToString(),
                ["傳聞加成"] = $"{state.Story.rumorBonus:P0}", ["門人"] = state.Disciples.Count.ToString(),
                [$"{state.Protagonist.name}・傷勢"] = state.Protagonist.injury.ToString()
            };
            foreach (var member in state.Disciples)
            {
                readings[$"{member.name}・忠誠"] = member.loyalty.ToString();
                readings[$"{member.name}・傷勢"] = member.injury.ToString();
            }
            foreach (var id in new[] { GameState.HeroId }.Concat(state.Disciples.Select(member => member.id)))
            {
                string name = state.SparName(id);
                readings[$"{name}・本月餘次"] = AssignmentSystem.Remaining(state, id).ToString();
                foreach (var line in state.MemberLines(id).Where(line => line != null))
                {
                    bool martial = RankSystem.IsCombat(line.id);
                    float progress = martial ? line.progress : state.WorkPractice(id, line.id)?.xp ?? 0;
                    int max = martial ? GameState.MartialProgressMax : GameState.LifeProgressMax;
                    readings[$"{name}・{EffectText.LineName(line.id)}"] = $"{line.rank}／{line.cap}　{progress:0.#}／{max}";
                }
            }
            return readings;
        }

        private void ShowActionResult(string name, Dictionary<string, string> before, string note)
        {
            var lines = ActionSnapshot().Where(entry => before.TryGetValue(entry.Key, out var old) && old != entry.Value)
                .Select(entry => $"{entry.Key}　{before[entry.Key]} → {entry.Value}").ToList();
            if (!string.IsNullOrWhiteSpace(note)) lines.Add(note.Trim());
            if (lines.Count == 0) lines.Add("此番事畢，門中數值無變。");
            ui.ShowModal(name + "・結果", lines, new[] { ("收卷", (Action)(() => ui.CloseModal(force: true))) });
        }

        private void OpenStudyOrders(string member, Action back)
        {
            ui.ShowCreation("指派修習", new[] { state.StudyLabel(member), "修滿目前上限後，門人將自選下一門武學。" },
                new[] { ("恢復自選", (Action)(() => Select(null))), ("返回", back) }, null);
            foreach (var art in state.TrainingManuals(member))
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
