using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        /// <summary>
        /// v1124 C17 山門演武 picker on the scene page: mode tabs (兩人對練 / 多人混戰), a grid of fighter cards to toggle,
        /// the cost and odds line, then 推演切磋 / 親自下場 (turn battle, only when the protagonist takes part) / 返回.
        /// </summary>
        internal void ShowSparPicker(GameState state, IList<string> picks, PickerSort sort, Action redraw,
            bool melee, string status, string block,
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

            const float width = 1760;
            var table = MapUi.Kit(page, "門人名冊", 60, 174, 1800, 670, "Wood", false);
            PickerHeader(MapUi.Rect(table, "欄目", 12, 12, width, 42), width, sort, redraw, true);
            var list = ScrollList(table, new(12, 14), new(-28, -60));
            var rows = new[] { GameState.HeroId }.Concat(state.Disciples.Where(member => member != null).Select(member => member.id))
                .Select(id =>
                {
                    int order = picks.IndexOf(id);
                    string badge = order < 0 ? "" : melee ? $"出場 {order + 1}" : order == 0 ? "甲方" : "乙方";
                    float? chance = picks.Count == 1 && order < 0 ? state.SparOdds(id, picks[0]) : null;
                    return PickerMember(state, id, "sparring", badge, AssignmentSystem.Block(state, id), chance, () => toggle(id));
                });
            foreach (var entry in SortRows(rows, sort))
            {
                var row = Rect("演武人選", list);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
                PickerBody(row, width, 70, entry);
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
