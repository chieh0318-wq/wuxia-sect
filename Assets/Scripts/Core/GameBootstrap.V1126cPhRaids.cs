namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private void OpenRaidAlert()
        {
            if (state.Story.sectRaid == null) return;
            if (state.RaidReady || state.RaidBlock(RaidOrder.Garrison) != null)
            {
                ShowRaidDesk();
                return;
            }
            ui.ChooseAssignment(state, "指派守將", "campaign", 10, party =>
            {
                string result = state.RunRaidOrder(RaidOrder.Garrison, party);
                if (!capturing) state.Save();
                ui.Refresh(state);
                ShowRaidDesk();
                ui.Message(result);
            }, ShowRaidDesk, id => state.RaidChance(RaidOrder.Garrison, id));
        }
    }
}
