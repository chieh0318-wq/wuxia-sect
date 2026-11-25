using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126c PC3 #5 / #25: one dialogue-box style for every talk in the game (prologue beats, portrait talks, scene pages,
    /// diplomacy talks), 旁白 included: a compact teal plate with a gold rim (UiKit "Plaque") sized to the name, the name in
    /// gold 30 px, and the body in the upright 32 px dialogue face. Raw speaker tags (「[narrator]」, 「[kang]」) and italic
    /// markup never reach the screen.
    /// </summary>
    internal static class DialogueStyle
    {
        internal const int NameSize = 30, BodySize = 32, PlateHeight = 60;
        /// <summary>How far the plate rises above the top edge of its box.</summary>
        internal const float PlateLift = 28;
        internal const string BodyFont = "Dialogue";

        internal static string Speaker(string speaker)
        {
            string cleaned = Clean(speaker).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ||
                   string.Equals(cleaned, "narrator", System.StringComparison.OrdinalIgnoreCase)
                ? "旁白" : cleaned;
        }

        /// <summary>Plate width that fits the name (and an optional subtitle line), 168–400 px.</summary>
        internal static float PlateWidth(string name, string subtitle = null) =>
            Mathf.Clamp(Mathf.Max((name ?? "").Length * 34 + 64, (subtitle ?? "").Length * 19 + 40), 168, 400);

        private static readonly Regex Tag = new(@"(^|\n)[ \t]*\[[A-Za-z_]+\][ \t]*", RegexOptions.Compiled);

        /// <summary>Strips leaked speaker tags and italic / bold markup from a line before it is shown.</summary>
        internal static string Clean(string text) =>
            Tag.Replace(text ?? "", "$1").Replace("<i>", "").Replace("</i>", "").Replace("<b>", "").Replace("</b>", "");

        /// <summary>The shared plate for MapUi-built pages (scene stage, diplomacy). x / y are the plate's top-left corner.</summary>
        internal static RectTransform MapPlate(Transform parent, string speaker, float x, float y, string subtitle = null)
        {
            string name = Speaker(speaker);
            bool twoLines = !string.IsNullOrEmpty(subtitle);
            float width = PlateWidth(name, subtitle), height = twoLines ? PlateHeight + 22 : PlateHeight;
            var plate = MapUi.Kit(parent, "名牌", x, y, width, height, "Plaque");
            var label = MapUi.Text(plate, name, 12, twoLines ? 4 : 2, width - 24, PlateHeight - 6, NameSize, MapUi.Gold,
                TextAnchor.MiddleCenter, "Names");
            label.fontStyle = FontStyle.Bold;
            if (twoLines)
                MapUi.Text(plate, subtitle, 12, PlateHeight - 6, width - 24, 26, 18, MapUi.Paper, TextAnchor.MiddleCenter);
            return plate;
        }
    }

    public sealed partial class SectUI
    {
        /// <summary>The shared plate on a sect-canvas dialogue box, its left edge <paramref name="left"/> px from the box's left.</summary>
        private RectTransform DialoguePlate(RectTransform box, string speaker, float left)
        {
            string name = DialogueStyle.Speaker(speaker);
            float width = DialogueStyle.PlateWidth(name);
            var plate = Panel("Dialogue nameplate", box, Vector2.up, Vector2.up,
                new(left, DialogueStyle.PlateLift - DialogueStyle.PlateHeight), new(left + width, DialogueStyle.PlateLift), false);
            UiKit.Skin(plate.GetComponent<Image>(), "Plaque");
            var label = Label(plate, name, DialogueStyle.NameSize, TextAnchor.MiddleCenter);
            label.font = UiKit.Font("Names", font, name);
            label.color = CardGold;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = false;
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new(12, 0), new(-12, 0));
            return plate;
        }

        /// <summary>The shared body text settings (size, upright dialogue face, no best-fit shrinking).</summary>
        private void DialogueBody(Text body, string text)
        {
            body.font = UiKit.Font(DialogueStyle.BodyFont, font, text);
            body.fontSize = DialogueStyle.BodySize;
            body.fontStyle = FontStyle.Normal;
            body.resizeTextForBestFit = false;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.1f;
        }
    }
}
