using System;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal void ShowRankLadder(GameState state, Action back)
        {
            void Close()
            {
                AssignmentBack = null;
                CloseModal(force: true);
                back?.Invoke();
            }
            ShowCreation("門中位階", new[] { "派遣、生產與戰鬥可累積貢獻。升階由玩家決定，貢獻保留。" },
                new[] { ("返回", (Action)Close) }, null);
            foreach (var rank in AssignmentSystem.Ladder)
                CreationChoice(rank.Name, $"累積貢獻 {rank.Contribution}　每月 {rank.Monthly} 次　能力 +{rank.Ability}　領兵 {rank.Troops}", null, null);
            CreationChoice(AssignmentSystem.Head.Name, $"每月 {AssignmentSystem.Head.Monthly} 次　領兵 {AssignmentSystem.Head.Troops}", null, null);
            foreach (var member in state.Disciples)
            {
                string id = member.id;
                var rank = AssignmentSystem.RankOf(state, id);
                int grade = AssignmentSystem.Grade(state, id);
                string next = grade + 1 < AssignmentSystem.Ladder.Length
                    ? $"　升{AssignmentSystem.Ladder[grade + 1].Name}須 {AssignmentSystem.Ladder[grade + 1].Contribution}" : "";
                CreationChoice($"{member.name}　{rank.Name}", $"貢獻 {AssignmentSystem.Contribution(state, id)}{next}", null,
                    AssignmentSystem.CanPromote(state, id) ? (Action)(() =>
                    {
                        AssignmentSystem.Promote(state, id);
                        ShowRankLadder(state, back);
                    }) : null);
            }
            AssignmentBack = Close;
        }
    }
}
