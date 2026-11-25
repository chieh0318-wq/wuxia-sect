using System;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        /// <summary>
        /// v1126e #5: 「結束本月」 with action points left asks first — 「尚餘 X 點行動力，確定結束？」 — since unspent points
        /// do not carry over. With none left it settles the month at once.
        /// </summary>
        private void RequestEndMonth()
        {
            if (state == null) return;
            if (state.ActionPoints <= 0)
            {
                EndMonth();
                return;
            }
            ui.ShowModal("結束本月", new[]
                {
                    $"尚餘 {state.ActionPoints} 點行動力，確定結束？",
                    "用剩的行動力不會留到下月；月底結算後補回 " + state.ApMax + " 點。"
                },
                new (string, Action)[]
                {
                    ("繼續辦事", () => ui.CloseModal()),
                    ("結束本月", () => { ui.CloseModal(); EndMonth(); })
                });
        }
    }
}