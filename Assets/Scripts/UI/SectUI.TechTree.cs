using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>One node card on the tech-tree window.</summary>
    internal struct TechCard
    {
        public string name, cost, effect, status;
        public bool researched, unlocked;
        public Action research;
    }

    public sealed partial class SectUI
    {
        private static readonly Color TechDone = new(.62f, .93f, .58f);
        private static readonly Color TechOpen = new(1f, .86f, .48f);
        private static readonly Color TechLocked = new(.93f, .80f, .64f);

        /// <summary>
        /// 藏經閣・門派科技樹: four branch rows, each a plaque followed by four tiered node cards joined by arrows.
        /// A node card is clickable only while it is 「可研習」 and affordable.
        /// </summary>
        internal void ShowTechTree(IEnumerable<string> header,
            IEnumerable<(string name, string motto, TechCard[] nodes)> branches, Action close)
        {
            ShowCreation("藏經閣・門派科技樹", header, new[] { ("關閉", close) }, null);
            const float plaqueShare = .104f, gap = .02f, rowHeight = 162;
            foreach (var branch in branches)
            {
                var row = CreationRow(branch.name, rowHeight);
                var plaque = Panel(branch.name, row, Vector2.zero, new(plaqueShare, 1), new(0, 4), new(-8, -4), false);
                UiKit.Skin(plaque.GetComponent<Image>(), "Plaque");
                var title = Label(plaque, branch.name, 26, TextAnchor.MiddleCenter);
                title.color = new Color(.98f, .89f, .72f);
                Anchor(title.rectTransform, new(0, .45f), Vector2.one, new(8, 0), new(-8, -10));
                var motto = Label(plaque, branch.motto, 18, TextAnchor.UpperCenter);
                motto.color = new Color(.92f, .80f, .62f);
                Anchor(motto.rectTransform, Vector2.zero, new(1, .45f), new(8, 10), new(-8, -4));

                float width = (1 - plaqueShare - gap * 3) / 4;
                for (int index = 0; index < branch.nodes.Length; index++)
                {
                    var node = branch.nodes[index];
                    float x = plaqueShare + index * (width + gap);
                    float right = x + width;
                    bool clickable = !node.researched && node.unlocked && node.research != null;
                    var card = Button(row, node.name, clickable ? node.research : null);
                    var selectable = card.GetComponent<Button>();
                    selectable.interactable = clickable;
                    if (!clickable) selectable.transition = Selectable.Transition.None;
                    Anchor(card, new(x, 0), new(right, 1), new(0, 4), new(0, -4));
                    var image = card.GetComponent<Image>();
                    UiKit.Skin(image, node.researched ? "Selected" : node.unlocked ? "Vermilion" : "Wood");
                    if (!node.researched && !node.unlocked)
                        image.color = new Color(.30f, .28f, .25f);
                    if (node.researched)
                    {
                        var glow = card.gameObject.AddComponent<Outline>();
                        glow.effectColor = new Color(.55f, .95f, .55f, .8f);
                        glow.effectDistance = new Vector2(2, 2);
                    }
                    var caption = card.GetComponentInChildren<Text>();
                    caption.alignment = TextAnchor.MiddleLeft;
                    caption.resizeTextMaxSize = 22;
                    AddCardIcon(card, UiKit.TechIcon(branch.name, index), 10, 38);
                    Anchor(caption.rectTransform, Vector2.up, Vector2.one, new(54, -34), new(-8, -4));
                    var cost = Label(card, $"第{"一二三四"[Mathf.Clamp(index, 0, 3)]}重　{node.cost}", 18, TextAnchor.MiddleLeft);
                    cost.color = new Color(.95f, .82f, .62f);
                    Anchor(cost.rectTransform, Vector2.up, Vector2.one, new(54, -58), new(-8, -34));
                    var effect = Label(card, node.effect, 18);
                    effect.color = new Color(1f, .93f, .80f);
                    effect.horizontalOverflow = HorizontalWrapMode.Wrap;
                    effect.resizeTextForBestFit = true;
                    effect.resizeTextMinSize = 16;
                    effect.resizeTextMaxSize = 18;
                    Anchor(effect.rectTransform, Vector2.zero, Vector2.one, new(12, 34), new(-8, -60));
                    var status = Label(card, node.status, 18, TextAnchor.MiddleLeft);
                    status.color = node.researched ? TechDone : node.unlocked ? TechOpen : TechLocked;
                    status.resizeTextMinSize = 15;
                    status.resizeTextMaxSize = 18;
                    Anchor(status.rectTransform, Vector2.zero, Vector2.right, new(12, 6), new(-8, 32));
                    if (index == branch.nodes.Length - 1)
                        continue;
                    // Connector: a short ink rule ending in an arrow head, lit once the left node is learned.
                    var link = Rect("連線", row);
                    Anchor(link, new(right, .5f), new(right + gap, .5f), new(3, -2), new(-10, 2));
                    var rule = link.gameObject.AddComponent<Image>();
                    rule.raycastTarget = false;
                    rule.color = node.researched ? TechDone : new Color(.55f, .42f, .28f);
                    var head = Label(row, "▶", 22, TextAnchor.MiddleCenter);
                    head.color = rule.color;
                    Anchor(head.rectTransform, new(right + gap, .5f), new(right + gap, .5f), new(-16, -12), new(0, 12));
                }
            }
        }
    }
}
