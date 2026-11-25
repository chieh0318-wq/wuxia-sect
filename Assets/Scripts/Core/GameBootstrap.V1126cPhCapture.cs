using System;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] Ph3CaptureSteps() => new (string, Action)[]
        {
            ("PH3_16_courtyard_cells.png", () =>
            {
                CleanupV1125PaCapture();
                EnsureCaptureCourtyard();
                var garden = System.Array.Find(state.Data.Buildings, building => building.id == "vegetable");
                int plot = System.Array.FindIndex(state.Buildings, building => building == null);
                state.Buildings[plot] = garden;
                world.PlaceBuilding(plot, garden, state.FootprintCells(plot, garden));
                world.HighlightPlots(true);
                var next = System.Array.FindIndex(state.Buildings, plot + 1, building => building == null);
                world.PreviewCells(state.FootprintCells(next, garden));
            }),
            ("PH3_20_raid_alert.png", () =>
            {
                CleanupV1125PaCapture();
                EnsureCaptureCourtyard();
                world.HighlightPlots(false);
                state.Story.sectRaid = new SectRaid
                {
                    faction = "tubo", target = state.StartNode, due = state.MonthStamp + 2, troops = 810
                };
                ui.Refresh(state);
            }),
            ("PH3_20_defender_assignment.png", OpenRaidAlert),
            ("PH3_21_silver_ledger.png", () =>
            {
                ui.CloseModal(force: true);
                state.Story.sectRaid = null;
                state.SeedDiplomacyDemo();
                ui.Refresh(state);
                ui.ShowResourceBreakdown(0);
            }),
            ("PH3_26_vertical_commands.png", () =>
            {
                ui.CloseModal(force: true);
                state.LearnArt("kaibei_shou");
                ui.StartTurnBattle(state, new BattleRequest { expeditionParty = new[] { GameState.HeroId } },
                    this, captureRound: _ => { }, captureInspect: inspect => inspect("skill"));
            }),
            ("PH3_27_inventory_manual.png", () =>
            {
                ui.CloseBattle();
                state.StoreRealmManual("tiedan_dao");
                ShowSectInventory(2);
            }),
        };
    }
}
