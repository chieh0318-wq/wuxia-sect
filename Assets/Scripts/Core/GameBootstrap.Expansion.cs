using System;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private void ShowSectExpansion()
        {
            string block = state.ExpansionBlock();
            int scale = state.SectScale;
            void Back()
            {
                ui.TrackPanelBack(null);
                ui.CloseModal(force: true);
                if (choosingBuilding) ShowBuildMenu();
                else ShowCommands();
            }
            ui.ShowCreation("擴建山門",
                new[] { $"當前規模　{GameState.Scales[scale].name}　用地 {state.LandUsed}／{state.LandCapacity}", block ?? "可以擴建" },
                new[] { ("返回", (Action)Back) }, null);
            for (int i = 0; i < GameState.Scales.Length; i++)
            {
                var tier = GameState.Scales[i];
                ui.CreationChoice(tier.name, $"聲望 {tier.reputation}・弟子 {tier.disciples}・建築 {tier.buildings}・用地 {state.LandAtScale(i)}" +
                    (i > 0 ? $"・銀 {tier.silver}・行動力 1" : ""), null,
                    i == scale + 1 && block == null ? (Action)(() => ui.ChooseAssignment(state, "擴建山門", "build", 1, party =>
                    {
                        if (!state.ExpandSect(party[0])) { ShowSectExpansion(); return; }
                        CancelBuild();
                        world.Destroy();
                        BuildCourtyard();
                        ui.Refresh(state);
                        ShowCommands();
                        ui.Message($"山門已擴為【{GameState.Scales[state.SectScale].name}】。");
                    }, ShowSectExpansion)) : null, picked: i == scale);
            }
            ui.TrackPanelBack(Back);
        }
    }
}
