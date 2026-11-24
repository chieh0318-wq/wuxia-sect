using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1125 #20: the month report as a memorial scroll — lacquer rails, xuan paper, one icon per entry, income in
    /// green, spending and losses in red, and the month's net change written at the foot.
    /// </summary>
    public sealed partial class SectUI
    {
        internal static readonly Color Income = new(.20f, .50f, .26f), Expense = new(.66f, .18f, .14f);

        internal void ShowMonthScroll(string title, IList<string> lines, (int silver, int food, int reputation)? net, Action closed)
        {
            ShowModal("", Array.Empty<string>(), new[] { ("收卷", (Action)(() => CloseModal())) }, closed: closed);
            Anchor(modalCard, Vector2.one * .5f, Vector2.one * .5f, new(-560, -470), new(560, 470));
            characterDecoration = MapUi.Docket(modalCard, "門派月報", 0, 0, 1120, 856);
            var page = characterDecoration;
            var seal = MapUi.Kit(page, "月報印", 40, 30, 74, 74, "Seal", false);
            MapUi.Text(seal, "報", 0, 0, 74, 74, 40, new Color(1f, .92f, .78f), TextAnchor.MiddleCenter, "Names");
            string heading = lines.Count > 0 && lines[0].EndsWith("結算", StringComparison.Ordinal) ? lines[0] : "本月結算";
            MapUi.Text(page, title, 132, 26, 600, 52, 36, MapUi.Red, role: "Titles");
            MapUi.Text(page, heading, 132, 76, 600, 34, 22, MapUi.Ink);
            MapUi.Panel(page, "卷首線", 40, 120, 1040, 2, new Color(.55f, .38f, .22f, .7f));

            var viewport = MapUi.Panel(page, "奏摺內文", 40, 132, 1040, 560, new Color(.27f, .19f, .13f, .05f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var rows = MapUi.Rect(viewport, "條目", 0, 0, 1040, 0);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            float y = 0;
            foreach (string raw in lines.Skip(heading == (lines.Count > 0 ? lines[0] : null) ? 1 : 0))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var (icon, tone) = ReportStyle(raw);
                int height = raw.Length > 40 ? 76 : 48;
                var row = MapUi.Rect(rows, "條", 0, y, 1040, height);
                var mark = MapUi.Rect(row, icon, 8, (height - 36) / 2f, 36, 36).gameObject.AddComponent<RawImage>();
                mark.texture = UiKit.Icon(icon);
                mark.enabled = mark.texture != null;
                mark.raycastTarget = false;
                var text = MapUi.Text(row, raw.Replace(' ', ' '), 58, 0, 970, height, 22, tone, TextAnchor.MiddleLeft);
                text.resizeTextForBestFit = false;
                MapUi.Panel(row, "隔線", 58, height - 1, 970, 1, new Color(.55f, .38f, .22f, .22f));
                y += height + 4;
            }
            rows.sizeDelta = new Vector2(1040, y);

            // The foot of the memorial: this month's net change.
            var foot = MapUi.Kit(page, "本月淨額", 40, 704, 1040, 96, "Wood", false);
            MapUi.Text(foot, "本月淨額", 22, 0, 180, 96, 28, UiKit.HeaderGold, role: "Names");
            if (net.HasValue)
            {
                Net(foot, "hud_silver", "銀兩", net.Value.silver, 220);
                Net(foot, "hud_food", "糧食", net.Value.food, 490);
                Net(foot, "hud_rep", "聲望", net.Value.reputation, 760);
            }
            else MapUi.Text(foot, "各月收支已分條列於上方。", 220, 0, 780, 96, 22, MapUi.Paper);
            FitModalButtons();

            void Net(Transform parent, string icon, string label, int value, float x)
            {
                var image = MapUi.Rect(parent, icon, x, 30, 36, 36).gameObject.AddComponent<RawImage>();
                image.texture = UiKit.Icon(icon);
                image.enabled = image.texture != null;
                image.raycastTarget = false;
                Color tone = value > 0 ? new Color(.55f, .92f, .55f) : value < 0 ? new Color(1f, .52f, .45f) : MapUi.Paper;
                MapUi.Text(parent, $"{label} {value:+0;−0;0}", x + 44, 0, 200, 96, 26, tone, TextAnchor.MiddleLeft, "Numbers");
            }
        }

        /// <summary>Icon and ink colour for one report entry: gains green, costs / losses red, totals dark ink.</summary>
        internal static (string icon, Color tone) ReportStyle(string line)
        {
            string icon = line.Contains("行動力") ? "hud_ap"
                : line.Contains("糧") && !line.Contains("銀") ? "hud_food"
                : line.Contains("聲望") && !line.Contains("銀") ? "hud_rep"
                : line.Contains("突破") || line.Contains("武學") || line.Contains("修練") ? "skill_sword"
                : line.Contains("弟子") || line.Contains("忠誠") ? "hud_disciple"
                : line.Contains("外交") || line.Contains("勢力") || line.Contains("朝貢") || line.Contains("盟") ? "faction_court"
                : line.Contains("堂") || line.Contains("建築") ? "skill_construction"
                : line.Contains("傷") || line.Contains("藥") ? "skill_medicine"
                : line.Contains("銀") ? "hud_silver"
                : "act_skill";
            bool loss = line.Contains("−") || System.Text.RegularExpressions.Regex.IsMatch(line, @"-\d") ||
                line.Contains("下降") || line.Contains("短缺") || line.Contains("不敷") || line.Contains("拖欠") ||
                line.Contains("失敗") || line.Contains("維護") || line.Contains("口糧");
            bool gain = !loss && (line.Contains("+") || line.Contains("收入") || line.Contains("上升") || line.Contains("增加") ||
                line.Contains("恢復") || line.Contains("突破") || line.Contains("獲得"));
            Color tone = loss ? Expense : gain ? Income : MapUi.Ink;
            if (line.StartsWith("本月結餘", StringComparison.Ordinal)) tone = new Color(.42f, .30f, .12f);
            return (icon, tone);
        }
    }
}
