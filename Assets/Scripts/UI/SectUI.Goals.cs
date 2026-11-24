using System;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal Action GoalsFromSystem;

        internal void ShowSectGoals(GameState state, Action next)
        {
            void Continue()
            {
                TrackPanelBack(null);
                CloseModal(force: true);
                next?.Invoke();
            }

            ShowCreation("門派志向", new[] { "重整崤山劍門，繼而以外交或征伐在江湖立足。" },
                new[] { ("收起卷軸", (Action)Continue) }, null);
            foreach (string line in state.GoalLines())
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('｜', 2);
                CreationChoice(parts[0], parts.Length == 2 ? parts[1] : "", null, null);
            }
            TrackPanelBack(Continue);
        }
    }
}
