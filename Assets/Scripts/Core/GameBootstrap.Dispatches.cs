using System;
using System.Linq;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private void ShowFieldDispatches()
        {
            ui.ShowCreation("外派名單", state.Story.fieldDispatches.Length == 0 ? new[] { "目前無人在外。" } : Array.Empty<string>(),
                new[] { ("返回", (Action)(() => ui.CloseModal(force: true))) }, null);
            foreach (var mission in state.Story.fieldDispatches)
            {
                var site = RealmContent.Treasure(mission.site);
                if (site == null) continue;
                string party = string.Join("、", mission.party.Select(worker => worker == GameState.HeroId
                    ? state.Protagonist.name : state.Disciples.Find(member => member.id == worker)?.name ?? worker));
                bool arrived = state.DispatchArrived(mission);
                int due = state.DispatchReturnMonth(mission);
                string detail = $"{party}　{(mission.returning ? "回程" : arrived ? "已抵達" : "前往秘境")}　預計開元{(due - 1) / 12}年{(due - 1) % 12 + 1}月返山";
                if (arrived && !state.TreasureClaimed(site.id))
                    ui.CreationChoice($"{site.name}・入境探寶", detail, null, () =>
                    {
                        ui.CloseModal(force: true);
                        state.RecallDispatch(mission);
                        BeginTreasureScene(site, mission.party);
                    });
                if (!mission.returning)
                    ui.CreationChoice($"{site.name}・飛鴿傳書", detail, null, () =>
                    {
                        state.RecallDispatch(mission);
                        ShowFieldDispatches();
                        if (!capturing) state.Save();
                    });
                else ui.CreationChoice(site.name, detail, null, null);
            }
        }
    }
}
