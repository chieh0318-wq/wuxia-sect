using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>One fighter card on the 山門演武 picker.</summary>
    internal sealed class SparCard
    {
        public string id, name, title, line, note, badge;
        public Texture2D portrait;
        public bool selected;
    }

    public sealed partial class SectUI
    {
        /// <summary>
        /// v1124 C17 山門演武 picker on the scene page: mode tabs (兩人對練 / 多人混戰), a grid of fighter cards to toggle,
        /// the cost and odds line, then 推演切磋 / 親自下場 (turn battle, only when the protagonist takes part) / 返回.
        /// </summary>
        internal void ShowSparPicker(IList<SparCard> cards, bool melee, string status, string block,
            Action<string> toggle, Action<bool> setMode, Action simulate, Action fight, Action leave)
        {
            var page = Scene.NewPage(null, figures: false);
            var help = MapUi.Kit(page, "說明", 60, 104, 1800, 56, "Wood", false);
            MapUi.Text(help, melee ? "多人混戰：選 3–4 人循環對打，勝場最多者奪魁。" : "兩人對練：選 2 人。主角下場時可改用回合實戰親自比劃。",
                20, 0, 1180, 56, 23, MapUi.Paper);
            var duel = MapUi.Button(help, "兩人對練", 1240, 6, 260, 44, () => setMode(false), null, 22);
            var group = MapUi.Button(help, "多人混戰", 1520, 6, 260, 44, () => setMode(true), null, 22);
            MapUi.Select(duel, !melee);
            MapUi.Select(group, melee);

            // Up to ten fighters as large cards; larger sects switch to three compact rows of seven.
            bool compact = cards.Count > 10;
            int perRow = compact ? 7 : 5;
            float cardW = compact ? 238 : 340, cardH = compact ? 214 : 316, gapX = compact ? 22 : 25, gapY = compact ? 12 : 18;
            float left = 60, top = 178, face = compact ? 0.62f : 1f;
            for (int i = 0; i < cards.Count && i < perRow * (compact ? 3 : 2); i++)
            {
                var card = cards[i];
                float x = left + i % perRow * (cardW + gapX), y = top + i / perRow * (cardH + gapY);
                var button = MapUi.Button(page, "", x, y, cardW, cardH, () => toggle(card.id), card.selected ? MapUi.Red : null);
                button.name = "演武人選:" + card.id;
                MapUi.Portrait(button.transform, card.portrait, 12, 12, 130 * face, 168 * face);
                float tx = 12 + 130 * face + 10, tw = cardW - tx - 10;
                MapUi.Text(button.transform, card.name, tx, 12, tw, 40, compact ? 24 : 30, MapUi.Paper, TextAnchor.MiddleLeft, "Names");
                MapUi.Text(button.transform, card.title, tx, 52, tw, 28, compact ? 18 : 20, MapUi.Gold);
                MapUi.Text(button.transform, card.line, tx, 82, tw, 28, compact ? 18 : 21, MapUi.Paper);
                MapUi.Text(button.transform, card.note, 12, 24 + 168 * face, cardW - 24, cardH - 76 - 168 * face, compact ? 16 : 19,
                    new Color(.86f, .90f, .82f), TextAnchor.UpperLeft);
                if (!string.IsNullOrEmpty(card.badge))
                {
                    var badge = MapUi.Kit(button.transform, "出場", cardW - 120, cardH - 50, 110, 40, "Wood", false);
                    MapUi.Text(badge, card.badge, 0, 0, 110, 40, 22, MapUi.Gold, TextAnchor.MiddleCenter, "Names");
                }
            }
            var bar = MapUi.Kit(page, "演武底欄", 60, 860, 1800, 110, "Wood", false);
            MapUi.Text(bar, status, 24, 6, 1000, 50, 23, MapUi.Paper);
            MapUi.Text(bar, block ?? "人選已定，可以開始。", 24, 56, 1000, 44, 20, block == null ? MapUi.Gold : new Color(1f, .70f, .62f));
            var run = MapUi.Button(bar, "推演切磋", 1050, 22, 230, 66, simulate, MapUi.Red, 26);
            run.name = "演武開始";
            run.interactable = block == null;
            var real = MapUi.Button(bar, "親自下場（實戰）", 1300, 22, 260, 66, () => fight?.Invoke(), null, 22);
            real.name = "演武實戰";
            real.interactable = block == null && fight != null;
            MapUi.Button(bar, "返回", 1580, 22, 196, 66, leave, null, 24).name = "演武返回";
        }
    }
}
