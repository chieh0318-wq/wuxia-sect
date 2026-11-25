using System;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] Pd3CaptureSteps() => new (string, Action)[]
        {
            ("PD3_22_tech_tree.png", ShowTechTree),
            ("PD3_29_victory_routes.png", () => ui.ShowSectGoals(state, CloseTechTree)),
        };
    }
}
