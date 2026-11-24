using System;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private void ShowFactionEnvoy()
        {
            var offer = state.FactionWorld.envoy;
            if (offer == null) return;
            string terms = offer.silver == 0 && offer.food == 0 ? "無須交付銀糧。" :
                $"對價　銀兩 {offer.silver}、糧食 {offer.food}。";
            string consequence = offer.kind == "ultimatum" ? "拒絕或逾期，對方將發兵。" : "議約不成，兩月後方可再議。";
            if (offer.kind == "submission" && GameState.Patronage(GameState.FindFaction(offer.faction)) is PatronageTerms duties)
                terms = duties.Benefit + duties.Cost;
            ui.ShowCreation($"{GameState.FindFaction(offer.faction).Label}來使・{GameState.OfferLabel(offer.kind)}",
                new[] { $"限 {(offer.due - 1) % 12 + 1} 月答覆。", terms, consequence },
                new (string, Action)[]
                {
                    ("答應", () => Answer("accept")),
                    ("還價", () => Answer("counter")),
                    ("拒絕", () => Answer("reject")),
                    ("稍候", () => ui.CloseModal(force: true))
                }, null);

            void Answer(string answer)
            {
                string result = state.AnswerFactionEnvoy(answer);
                if (state.FactionWorld.envoy == null) ui.CloseModal(force: true);
                ui.Refresh(state);
                ui.Message(result);
                if (!capturing) state.Save();
            }
        }
    }
}
