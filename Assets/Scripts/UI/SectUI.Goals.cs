using System;
using System.Linq;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal Action GoalsFromSystem;

        /// <summary>
        /// v1126 #8: the 門派志向 page (system menu) reads the same data as the 任務 panel — the current short-term
        /// goal first, then the four long-term routes. No longer shown during the opening (the 軍師 speaks instead).
        /// </summary>
        internal void ShowSectGoals(GameState state, Action next)
        {
            void Continue()
            {
                TrackPanelBack(null);
                CloseModal(force: true);
                next?.Invoke();
            }

            ShowCreation("門派志向", new[] { "眼下先辦短期要務；長遠要走哪條路，由你決定，四條路也可兼而行之。" },
                new[] { ("收起卷軸", (Action)Continue) }, null);
            var stage = QuestBoard.Current(state);
            if (stage != null)
                CreationChoice("眼下要務｜" + stage.title, string.Join("　", new[] { stage.brief }
                    .Concat(QuestBoard.Steps(state, stage).Select(step => $"{step.text} {step.current}／{step.target}"))
                    .Where(text => !string.IsNullOrEmpty(text))), null, null);
            foreach (var route in QuestBoardData.Data.routes ?? Array.Empty<QuestRoute>())
                if (route != null)
                    CreationChoice("常駐任務｜" + route.name, $"{route.direction}　{QuestBoard.RouteProgress(state, route)}", null, null);
            TrackPanelBack(Continue);
        }
    }
}
