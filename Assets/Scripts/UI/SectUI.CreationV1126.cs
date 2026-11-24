using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126 #4 (T-3 #4): what one more point of each attribute is worth, read from the live formulas so the
    /// creation screen follows any future rebalance. Combat figures come from <see cref="AttributeSystem.Derive"/>
    /// (one point above the base, everything else at base); the management effects mirror the GameState formulas
    /// they name.
    /// </summary>
    internal static class AttributePerPoint
    {
        internal static string Describe(int slot)
        {
            var baseAttrs = Enumerable.Repeat(AttributeSystem.Base, AttributeSystem.Count).ToArray();
            var raised = (int[])baseAttrs.Clone();
            raised[slot]++;
            var lines = RankSystem.Seed(null, RankSystem.PrimaryRank, RankSystem.PrimaryCap, RankSystem.BaseCap, RankSystem.BaseRank);
            var before = AttributeSystem.Derive(baseAttrs, lines);
            var after = AttributeSystem.Derive(raised, lines);
            var parts = new List<string>();
            Add("攻擊", after.Attack - before.Attack);
            Add("先手", after.Speed - before.Speed);
            Add("氣血", after.MaxHp - before.MaxHp);
            Add("內力", after.MaxQi - before.MaxQi);
            Add("內功威力", after.QiPower - before.QiPower);
            Percent("閃避", after.Dodge - before.Dodge);
            Percent("奇遇", after.Fortune - before.Fortune);
            Percent("修習", after.Study - before.Study);
            // Outside Derive: GameState.Effects (聲望 ×(1+3%/點)), GameState.Diplomacy (交涉分 = 魅力 + 悟性/2),
            // GameState.Ranks (療傷 +1 每 3 點根骨).
            switch (AttributeSystem.Keys[slot])
            {
                case "cha": parts.Add("聲望收穫 +3%"); parts.Add("交涉 +1"); break;
                case "wit": parts.Add("交涉 +0.5"); break;
                case "con": parts.Add("療傷 每3點 +1"); break;
            }
            return parts.Count == 0 ? "" : "每點　" + string.Join("・", parts);

            void Add(string name, int delta)
            {
                if (delta != 0) parts.Add($"{name} {delta:+0;-0}");
            }
            void Percent(string name, float delta)
            {
                if (Mathf.Abs(delta) >= .0005f) parts.Add($"{name} {delta * 100:+0.#;-0.#}%");
            }
        }
    }

    public sealed partial class SectUI
    {
        /// <summary>
        /// v1126 #2 (T-3 #2): 主修 is picked from a row of the combat lines instead of a button that cycles, with the
        /// consequences spelled out right underneath.
        /// </summary>
        internal void CreationMajor(string current, Action<string> choose, string explanation)
        {
            var combat = RankSystem.Categories.Where(category => category.family == "combat").ToArray();
            var row = CreationRow("主修武學", 92);
            var card = Panel("主修", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
            UiKit.Skin(card.GetComponent<Image>(), "Lacquer");
            var heading = Label(card, "主修武學", 22, TextAnchor.MiddleLeft);
            heading.color = CardGold;
            heading.fontStyle = FontStyle.Bold;
            Anchor(heading.rectTransform, Vector2.up, Vector2.up, new(16, -44), new(130, -6));
            var chips = Rect("主修選項", card);
            Anchor(chips, Vector2.up, Vector2.one, new(136, -44), new(-12, -6));
            var layout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            foreach (var category in combat)
            {
                string id = category.id;
                bool picked = id == current;
                var chip = Button(chips, category.name, picked ? null : () => choose(id));
                chip.name = "主修_" + id;
                chip.GetComponent<Button>().interactable = true;
                UiKit.Skin(chip.GetComponent<Image>(), picked ? "Selected" : "Wood");
                var caption = chip.GetComponentInChildren<Text>();
                caption.fontSize = caption.resizeTextMaxSize = 20;
                caption.color = picked ? UiKit.HeaderGold : CardText;
                var icon = UiKit.Icon(UiKit.SkillIcon(id));
                if (icon != null)
                {
                    var image = Rect("Icon", chip);
                    Anchor(image, new(0, .5f), new(0, .5f), new(6, -12), new(30, 12));
                    AddImage(image, icon);
                }
            }
            var note = Label(card, explanation, 17, TextAnchor.UpperLeft);
            note.color = CardText;
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.resizeTextForBestFit = true;
            note.resizeTextMinSize = 15;
            note.resizeTextMaxSize = 17;
            Anchor(note.rectTransform, Vector2.zero, Vector2.one, new(16, 4), new(-12, -48));
        }
    }
}
