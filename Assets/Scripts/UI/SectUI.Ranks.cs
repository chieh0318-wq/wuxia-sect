using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal void ShowCharacter(string name, string title, string gender, string portrait, int[] attrs,
            SkillLine[] lines, string weapon, int weaponDelta, (string, Action)? extra = null,
            string[] gear = null, DerivedStats? stats = null)
        {
            var close = ("關閉", (Action)(() => CloseModal()));
            ShowModal(name, new[] { title }, extra.HasValue ? new[] { extra.Value, close } : new[] { close });
            Anchor(modalCard, new(.5f, .5f), new(.5f, .5f), new(-500, -390), new(500, 390));
            CharacterEquipment(portrait, gender, weapon, gear);
            var profile = CreationRow("人物", 168);
            var rank = RankSystem.Martial(lines, weaponDelta: weaponDelta);
            var rankLabel = Label(profile, "武學  " + RankSystem.Format(rank, RankSystem.Martial(lines, true)), 28);
            rankLabel.color = Color.Lerp(RankSystem.Color(rank), Ink, .35f);
            Anchor(rankLabel.rectTransform, Vector2.up, Vector2.one, new(12, -39), new(-12, 0));
            var values = AttributeSystem.Normalize(attrs);
            for (int i = 0; i < AttributeSystem.Count; i++)
            {
                var stat = Panel(AttributeSystem.Names[i], profile, new(i / 7f, 1), new((i + 1) / 7f, 1),
                    new(3, -114), new(-3, -44), false);
                UiKit.Skin(stat.GetComponent<Image>(), "Wood");
                var caption = Label(stat, AttributeSystem.Names[i], 18, TextAnchor.MiddleLeft);
                caption.color = CardMuted;
                Anchor(caption.rectTransform, Vector2.up, Vector2.one, new(10, -32), new(-4, -4));
                var value = Label(stat, values[i].ToString(), 26, TextAnchor.MiddleRight);
                value.color = values[i] > AttributeSystem.Base ? CardGold : values[i] < AttributeSystem.Base ? CardLow : CardText;
                value.font = UiKit.Font("Numbers", font, value.text);
                Anchor(value.rectTransform, Vector2.up, Vector2.one, new(10, -34), new(-10, -2));
                CardBar(stat, 10, -44, -10, 6, values[i] / (float)AttributeSystem.Max,
                    Color.Lerp(AttributeSystem.Tints[i], Color.white, .25f));
            }
            var derived = stats ?? AttributeSystem.Derive(values, lines, weaponDelta);
            var summary = Label(profile, $"攻擊 {derived.Attack}　先手 {derived.Speed}　氣血 {derived.MaxHp}　內力 {derived.MaxQi}" +
                $"　內功威力 {derived.QiPower}　閃避 {derived.Dodge:P0}　奇遇 {derived.Fortune:P0}", 20, TextAnchor.MiddleCenter);
            Anchor(summary.rectTransform, Vector2.zero, Vector2.right, new(8, 4), new(-8, 46));
            SkillColumns(lines);
        }

        internal void SkillColumns(SkillLine[] lines, bool knownOnly = false)
        {
            var families = new[] { (key: "combat", label: "戰鬥技能"), (key: "production", label: "生產技能") };
            var groups = families.Select(family => RankSystem.Categories.Where(category => category.family == family.key)
                .Select(category => (category, line: Array.Find(lines, entry => entry?.id == category.id)))
                .Where(pair => !knownOnly || pair.line != null).ToArray()).ToArray();
            const int rowHeight = 39;
            var row = CreationRow("技能", 44 + rowHeight * groups.Max(group => group.Length));
            for (int column = 0; column < families.Length; column++)
            {
                if (knownOnly && groups[column].Length == 0) continue;
                var panel = Rect(families[column].label, row);
                Anchor(panel, new(column * .5f, 0), new((column + 1) * .5f, 1), new(10, 0), new(-10, 0));
                var heading = Label(panel, families[column].label, 27);
                Anchor(heading.rectTransform, Vector2.up, Vector2.one, new(0, -38), Vector2.zero);
                int y = -44;
                foreach (var pair in groups[column])
                {
                    var line = pair.line;
                    var label = Label(panel, $"{pair.category.name}　{RankSystem.Format(line?.rank, line?.cap)}", 20);
                    if (knownOnly)
                        label.color = Color.Lerp(RankSystem.Color(line.rank), Ink, .35f);
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
            if (attrs == null || attrs.Length == 0)
                return "";
            var values = AttributeSystem.Normalize(attrs);
            return string.Join("　", values.Select((value, index) => $"{AttributeSystem.Names[index]} {value}"));
        }
    }
}
