using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal void ShowCharacter(string name, string title, string gender, string portrait, int[] attrs,
            SkillLine[] lines, int weaponDelta, (string, Action)? extra = null)
        {
            var close = ("關閉", (Action)(() => CloseModal()));
            ShowModal(name, new[] { title }, extra.HasValue ? new[] { extra.Value, close } : new[] { close });
            Anchor(modalCard, new(.5f, .5f), new(.5f, .5f), new(-480, -344), new(480, 344));
            var profile = CreationRow("人物", 144);
            var art = Rect("Portrait", profile);
            Anchor(art, Vector2.zero, Vector2.up, new(8, 8), new(110, -8));
            AddImage(art, PortraitService.Resolve(portrait, gender));
            var rank = RankSystem.Martial(lines, weaponDelta: weaponDelta);
            float multiplier = RankSystem.Multiplier(rank);
            int[] effective = attrs?.Select(value => Mathf.RoundToInt(value * multiplier)).ToArray();
            var body = Label(profile, $"武學　{RankSystem.Format(rank, RankSystem.Martial(lines, true))}　×{multiplier:0.##}\n{Attributes(attrs)}\n實戰　{Attributes(effective)}", 25);
            body.color = Color.Lerp(RankSystem.Color(rank), Ink, .35f);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new(132, 16), new(-16, -16));
            SkillColumns(lines);
        }

        internal void SkillColumns(SkillLine[] lines)
        {
            var families = new[] { (key: "combat", label: "戰鬥技能"), (key: "production", label: "生產技能") };
            var groups = families.Select(family => RankSystem.Categories.Where(category => category.family == family.key)
                .Select(category => (category, line: Array.Find(lines, entry => entry.id == category.id))).ToArray()).ToArray();
            const int rowHeight = 39;
            var row = CreationRow("技能", 44 + rowHeight * groups.Max(group => group.Length));
            for (int column = 0; column < families.Length; column++)
            {
                var panel = Rect(families[column].label, row);
                Anchor(panel, new(column * .5f, 0), new((column + 1) * .5f, 1), new(10, 0), new(-10, 0));
                var heading = Label(panel, families[column].label, 27);
                Anchor(heading.rectTransform, Vector2.up, Vector2.one, new(0, -38), Vector2.zero);
                int y = -44;
                foreach (var pair in groups[column])
                {
                    var line = pair.line;
                    var label = Label(panel, $"{pair.category.name}　{RankSystem.Format(line?.rank, line?.cap)}", 20);
                    Anchor(label.rectTransform, Vector2.up, Vector2.one, new(0, y - 30), new(0, y));
                    for (int tier = 0; tier <= RankSystem.Index(line?.cap); tier++)
                    {
                        var pip = Rect("Rank bar", panel);
                        Anchor(pip, Vector2.up, Vector2.up, new(tier * 28, y - 34), new(tier * 28 + 24, y - 27));
                        var image = pip.gameObject.AddComponent<Image>();
                        image.raycastTarget = false;
                        image.color = tier <= RankSystem.Index(line?.rank) ? RankSystem.Color(RankSystem.At(tier))
                            : new Color(.35f, .32f, .25f, .45f);
                    }
                    y -= rowHeight;
                }
            }
        }

        internal static string Attributes(int[] attrs)
        {
            if (attrs?.Length < 5 || attrs == null)
                return "";
            return $"武力 {attrs[0]}　身法 {attrs[1]}　根骨 {attrs[2]}　悟性 {attrs[3]}　魅力 {attrs[4]}";
        }
    }
}
