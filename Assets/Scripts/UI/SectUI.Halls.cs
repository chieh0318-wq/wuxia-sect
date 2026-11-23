using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>One hall card on the 堂口 page.</summary>
    internal sealed class HallCard
    {
        public string id, name, icon, art, level, desc, summary;
        public bool founded;
        public List<(string text, bool met)> requirements = new();
        public (string label, string tip, Action run)? build;
        public List<(string label, string tip, Action run)> functions = new();
    }

    public sealed partial class SectUI
    {
        /// <summary>
        /// v1124 C18 山門堂口: one tall card per hall — art, level, purpose, then either its unlock conditions (✓／✗) and a
        /// 「興建」 button, or its commands with their costs and an 「擴建」 button.
        /// </summary>
        internal void ShowHalls(IList<HallCard> halls, string header, string message, Action leave)
        {
            var page = Scene.NewPage(null, figures: false);
            var strip = MapUi.Kit(page, "堂口說明", 40, 100, 1840, 52, "Wood", false);
            MapUi.Text(strip, header, 20, 0, 1800, 52, 22, MapUi.Paper);
            int count = Mathf.Max(1, halls.Count);
            float gap = 16, cardW = Mathf.Min(420, (1840 - gap * (count - 1)) / count), cardH = 790, top = 162;
            for (int i = 0; i < halls.Count; i++)
            {
                var hall = halls[i];
                float x = 40 + i * (cardW + gap);
                var card = MapUi.Docket(page, hall.name, x, top, cardW, cardH);
                var art = Resources.Load<Texture2D>(hall.art ?? "");
                if (art != null)
                    MapUi.Portrait(card, art, 16, 18, cardW - 32, 170);
                else
                {
                    var plate = MapUi.Panel(card, "堂口圖", 16, 18, cardW - 32, 170, hall.founded ? new Color(.30f, .24f, .18f) : new Color(.22f, .22f, .21f));
                    plate.GetComponent<Image>().raycastTarget = false;
                    var icon = UiKit.Icon(hall.icon);
                    if (icon != null)
                    {
                        var image = MapUi.Rect(plate, "icon", (cardW - 32) / 2 - 60, 25, 120, 120).gameObject.AddComponent<RawImage>();
                        image.texture = icon;
                        image.color = hall.founded ? Color.white : new Color(.55f, .55f, .55f);
                        image.raycastTarget = false;
                    }
                }
                var badge = MapUi.Rect(card, "堂口圖示", 20, 201, 34, 34).gameObject.AddComponent<RawImage>();
                badge.texture = UiKit.Icon(hall.icon);
                badge.raycastTarget = false;
                MapUi.Text(card, hall.name, 62, 196, cardW - 192, 48, 34, MapUi.Red, TextAnchor.MiddleLeft, "Titles");
                MapUi.Text(card, hall.level, cardW - 140, 200, 120, 40, 22, hall.founded ? MapUi.Jade : MapUi.Ink, TextAnchor.MiddleRight);
                var desc = MapUi.Text(card, hall.desc, 20, 244, cardW - 40, 84, 18, MapUi.Ink, TextAnchor.UpperLeft, "Narration");
                desc.resizeTextForBestFit = true;
                desc.resizeTextMinSize = 15;
                desc.resizeTextMaxSize = 18;
                desc.verticalOverflow = VerticalWrapMode.Truncate;
                var summary = MapUi.Text(card, hall.summary, 20, 332, cardW - 40, 100, 18, MapUi.Jade, TextAnchor.UpperLeft);
                summary.resizeTextForBestFit = true;
                summary.resizeTextMinSize = 14;
                summary.resizeTextMaxSize = 18;
                summary.verticalOverflow = VerticalWrapMode.Truncate;
                float y = 440;
                if (!hall.founded)
                {
                    MapUi.Text(card, "解鎖條件", 20, y, cardW - 40, 30, 20, MapUi.Red);
                    y += 32;
                    foreach (var (text, met) in hall.requirements)
                    {
                        MapUi.Text(card, (met ? "✓ " : "✗ ") + text, 28, y, cardW - 56, 30, 19,
                            met ? MapUi.Jade : new Color(.55f, .22f, .16f));
                        y += 32;
                    }
                }
                foreach (var (label, tip, run) in hall.functions)
                {
                    var button = MapUi.Button(card, "", 20, y, cardW - 40, 100, () => run?.Invoke(), null);
                    button.name = "堂口指令:" + label;
                    button.interactable = run != null;
                    MapUi.Text(button.transform, label, 14, 4, cardW - 68, 34, 23, MapUi.Paper, TextAnchor.MiddleLeft, "Names");
                    var tipText = MapUi.Text(button.transform, tip, 14, 38, cardW - 68, 58, 16, MapUi.Gold, TextAnchor.UpperLeft);
                    tipText.resizeTextForBestFit = true;
                    tipText.resizeTextMinSize = 13;
                    tipText.resizeTextMaxSize = 16;
                    tipText.verticalOverflow = VerticalWrapMode.Truncate;
                    y += 108;
                }
                if (hall.build.HasValue)
                {
                    var (buildLabel, buildTip, buildRun) = hall.build.Value;
                    var build = MapUi.Button(card, "", 20, cardH - 104, cardW - 40, 84, () => buildRun?.Invoke(), MapUi.Red);
                    build.name = "堂口興建:" + hall.id;
                    build.interactable = buildRun != null;
                    MapUi.Text(build.transform, buildLabel, 14, 4, cardW - 68, 38, 25, MapUi.Paper, TextAnchor.MiddleLeft, "Names");
                    MapUi.Text(build.transform, buildTip, 14, 42, cardW - 68, 38, 17, MapUi.Gold, TextAnchor.UpperLeft);
                }
            }
            if (!string.IsNullOrEmpty(message))
            {
                var note = MapUi.Kit(page, "堂口消息", 40, 970, 1340, 76, "Wood", false);
                MapUi.Text(note, message, 20, 0, 1300, 76, 22, MapUi.Gold);
            }
            MapUi.Button(page, "返回山門", 1640, 976, 240, 64, leave, MapUi.Red, 26).name = "堂口返回";
        }

        /// <summary>Target grid for a hall command (a fighter for 閉關, a slot for 打造).</summary>
        internal void ShowHallTargets(string title, string hint, IList<(string id, string label, string sub, Texture2D image)> targets,
            Action<string> pick, Action back)
        {
            var page = Scene.NewPage(null, figures: false);
            var strip = MapUi.Kit(page, "說明", 40, 100, 1840, 52, "Wood", false);
            MapUi.Text(strip, $"{title}　{hint}", 20, 0, 1800, 52, 22, MapUi.Paper);
            const int perRow = 4;
            float w = 440, h = 150, gap = 20;
            for (int i = 0; i < targets.Count && i < 20; i++)
            {
                var target = targets[i];
                var button = MapUi.Button(page, "", 40 + i % perRow * (w + gap), 172 + i / perRow * (h + 14), w, h, () => pick(target.id), null);
                button.name = "堂口對象:" + target.id;
                if (target.image != null) MapUi.Portrait(button.transform, target.image, 12, 12, 100, 126);
                float x = target.image != null ? 126 : 20;
                MapUi.Text(button.transform, target.label, x, 12, w - x - 12, 44, 28, MapUi.Paper, TextAnchor.MiddleLeft, "Names");
                MapUi.Text(button.transform, target.sub, x, 58, w - x - 12, 80, 19, MapUi.Gold, TextAnchor.UpperLeft);
            }
            MapUi.Button(page, "返回", 1640, 976, 240, 64, back, null, 26).name = "堂口對象返回";
        }
    }
}
