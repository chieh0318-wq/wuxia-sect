using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126 #4 / v1126b 決定 1: what one more point of each attribute is worth, read from the live formulas
    /// (<see cref="AttributeSystem.PerPoint"/>) so the creation screen and the character page follow any rebalance.
    /// </summary>
    internal static class AttributePerPoint
    {
        internal static string Describe(int slot)
        {
            var parts = AttributeSystem.PerPoint(slot);
            return parts.Count == 0 ? "" : "每點　" + string.Join("・", parts);
        }

        /// <summary>Multi-line hover text: the attribute, its uses, and every per-point effect on its own line.</summary>
        internal static string Tip(int slot, int value)
        {
            var parts = AttributeSystem.PerPoint(slot);
            int delta = value - AttributeSystem.Base;
            string head = $"{AttributeSystem.Names[slot]} {value}" +
                (delta == 0 ? "（基準）" : $"（較基準 {delta:+0;-0}）");
            return head + "\n" + AttributeSystem.Benefits[slot] + "\n每多一點：\n・" + string.Join("\n・", parts);
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

        /// <summary>
        /// v1126 #10 (T-3 #10): the powers around the candidate sites, drawn on the settlement map with their flag and
        /// a ring in the relation colour (敵對 red, 中立 gold, 友好 jade); hovering shows strength and distance.
        /// </summary>
        internal static (MapFaction faction, int hops, int days)[] NearbyPowers(WorldMapData data, string node, int count)
        {
            if (!data.Nodes.TryGetValue(node ?? "", out var origin)) return Array.Empty<(MapFaction, int, int)>();
            var point = data.Bounds.Uv(origin);
            return data.Factions.Where(f => data.Nodes.ContainsKey(f.seat ?? ""))
                .Select(f =>
                {
                    var route = f.seat == node ? null : data.Route(node, f.seat);
                    int hops = f.seat == node ? 0 : route == null ? 99 : route.Nodes.Count - 1;
                    return (faction: f, hops, days: route?.Days ?? 0, far: (data.Bounds.Uv(data.Nodes[f.seat]) - point).sqrMagnitude);
                })
                .OrderBy(entry => entry.hops).ThenBy(entry => entry.far).Take(count)
                .Select(entry => (entry.faction, entry.hops, entry.days)).ToArray();
        }

        internal static Color RelationColor(string stance) => stance switch
        {
            "hostile" => new Color(.86f, .22f, .16f),
            "friendly" => new Color(.30f, .72f, .52f),
            _ => new Color(.92f, .74f, .30f)
        };

        private Text settlementTip;

        internal void SettlementPowers(RectTransform map, WorldMapData data, GameState state, StoryLocation[] sites, StoryLocation focused)
        {
            var shown = sites.SelectMany(site => NearbyPowers(data, site.node, 4).Select(entry => entry.faction))
                .Distinct().ToArray();
            foreach (var faction in shown)
            {
                var uv = data.Bounds.Uv(data.Nodes[faction.seat]);
                string stance = faction.StanceWith(state.RelationShift(faction.id));
                var holder = Rect("附近勢力_" + faction.id, map);
                Anchor(holder, uv, uv, new(-17, 6), new(17, 48));
                var ring = holder.gameObject.AddComponent<Image>();
                ring.color = new Color(0, 0, 0, .25f);
                var rim = holder.gameObject.AddComponent<Outline>();
                rim.effectColor = RelationColor(stance);
                rim.effectDistance = new Vector2(3, -3);
                var flag = MapUi.Rect(holder, "旗", 0, 0, 34, 42);
                WorldMapScreen.DrawFlag(flag, WorldMapScreen.KindOf(faction), faction.Label.Substring(0, 1), 4, 4, 26,
                    faction.type is "clan" or "merchant");
                var trigger = holder.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowSettlementTip(map, faction, uv));
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => { if (settlementTip != null) settlementTip.transform.parent.gameObject.SetActive(false); });
                trigger.triggers.Add(enter);
                trigger.triggers.Add(exit);
            }

            // Colour key in the map corner.
            var key = Panel("關係圖例", map, Vector2.zero, Vector2.zero, new(10, 10), new(330, 52), false);
            UiKit.Skin(key.GetComponent<Image>(), "Lacquer");
            var row = Rect("圖例", key);
            Anchor(row, Vector2.zero, Vector2.one, new(10, 4), new(-10, -4));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            foreach (var (stance, label) in new[] { ("hostile", "敵對"), ("", "中立"), ("friendly", "友好") })
            {
                var chip = Label(row, "● " + label, 19, TextAnchor.MiddleCenter);
                chip.color = RelationColor(stance);
            }
            settlementTip = null;

            void ShowSettlementTip(RectTransform parent, MapFaction faction, Vector2 uv)
            {
                if (settlementTip == null)
                {
                    var box = Panel("勢力提示", parent, Vector2.zero, Vector2.zero, Vector2.zero, new(340, 112), false);
                    UiKit.Skin(box.GetComponent<Image>(), "Lacquer");
                    box.GetComponent<Image>().raycastTarget = false;
                    settlementTip = Label(box, "", 18, TextAnchor.UpperLeft);
                    settlementTip.color = CardText;
                    settlementTip.horizontalOverflow = HorizontalWrapMode.Wrap;
                    Fill(settlementTip.rectTransform, 10);
                }
                var site = focused ?? sites.FirstOrDefault();
                var near = site == null ? default : NearbyPowers(data, site.node, 99).FirstOrDefault(entry => entry.faction == faction);
                string distance = site == null || near.faction == null ? "" :
                    near.hops == 0 ? $"就在{site.name}" : near.hops >= 99 ? $"與{site.name}無路可通" : $"距{site.name} {near.hops} 格（約 {near.days} 日）";
                string stance = faction.RelationWith(state.RelationShift(faction.id));
                settlementTip.text = $"{faction.Label}　{WorldMapScreen.KindName(faction)}・{stance}\n" +
                    $"實力　人馬約 {faction.disciples:N0}・聲望 {faction.reputation}\n{distance}";
                var tipBox = (RectTransform)settlementTip.transform.parent;
                tipBox.anchorMin = tipBox.anchorMax = uv;
                tipBox.pivot = new Vector2(uv.x > .6f ? 1 : 0, uv.y > .5f ? 1 : 0);
                tipBox.anchoredPosition = new Vector2(uv.x > .6f ? -24 : 24, uv.y > .5f ? -8 : 52);
                tipBox.SetAsLastSibling();
                tipBox.gameObject.SetActive(true);
            }
        }
    }
}
