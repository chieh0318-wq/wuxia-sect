using System;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] Ph3CaptureSteps() => new (string, Action)[]
        {
            ("PH3_20_raid_alert.png", () =>
            {
                CleanupV1125PaCapture();
                EnsureCaptureCourtyard();
                state.Story.sectRaid = new SectRaid
                {
                    faction = "tubo", target = state.StartNode, due = state.MonthStamp + 2, troops = 810
                };
                ui.Refresh(state);
            }),
            ("PH3_20_defender_assignment.png", OpenRaidAlert),
        };
    }
}
