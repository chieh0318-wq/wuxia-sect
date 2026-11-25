using System;
using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private IEnumerable<(string filename, Action show)> Pb3CaptureSteps() => new (string, Action)[]
        {
            ("PB3_01_build_picker.png", () => ui.ChooseAssignment(state, "興建菜圃", "build", 1, _ => { })),
            ("PB3_02_patrol_odds.png", () => ui.ChooseAssignment(state, "山道巡訪", "explore", 3, _ => { })),
            ("PB3_03_study_order.png", () => OpenStudyOrders(GameState.HeroId, () => ui.CloseModal(force: true))),
            ("PB3_04_character_progress.png", ShowProtagonist),
            ("PB3_05_drill_result.png", () =>
            {
                ui.CloseModal(force: true);
                ExecuteAssignedAction(data.Actions.First(action => action.id == "train"), new[] { GameState.HeroId });
            }),
            ("PB3_06_spar_picker.png", StartSparring)
        };
    }
}
