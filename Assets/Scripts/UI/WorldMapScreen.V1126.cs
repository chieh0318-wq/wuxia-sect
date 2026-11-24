using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126 #12 / #13 / #15 (T-3 #12): faction flags drawn by kind (綠林 black torn flag with crossed blades, 朝廷・方鎮
    /// vermilion with a gold seal, 武林 teal / beige with a round crest, 外族 swallow-tailed ochre with fangs), a legend
    /// box in the map corner with one aligned row per sign, hover tips on strongholds and flags, a one-line footer,
    /// and an attack button on every stronghold card — grey with the reason and the way forward when it is not possible.
    /// </summary>
    public sealed partial class WorldMapScreen
    {
        private RectTransform hoverTip;
        private Text hoverText;

        internal enum FlagKind { Outlaw, Court, Sect, Foreign }

        internal static FlagKind KindOf(MapFaction faction) => faction.Outlaw ? FlagKind.Outlaw :
            faction.type == "foreign" ? FlagKind.Foreign : faction.Court ? FlagKind.Court : FlagKind.Sect;

        internal static string KindName(MapFaction faction) => KindOf(faction) switch
        {
            FlagKind.Outlaw => "綠林山匪",
            FlagKind.Court => faction.type == "military" ? "方鎮" : "朝廷",
            FlagKind.Foreign => "外族",
            _ => faction.type is "clan" or "merchant" ? "江湖幫會" : "武林門派"
        };

        /// <summary>The cloth of a faction flag, drawn inside <paramref name="rect"/> at <paramref name="size"/> px.</summary>
        internal static void DrawFlag(RectTransform rect, FlagKind kind, string glyph, float x, float y, float size, bool beige = false)
        {
            Color cloth = kind switch
            {
                FlagKind.Outlaw => new Color(.10f, .09f, .08f),
                FlagKind.Court => new Color(.74f, .15f, .10f),
                FlagKind.Foreign => new Color(.56f, .38f, .16f),
                _ => beige ? new Color(.86f, .79f, .62f) : new Color(.20f, .47f, .45f)
            };
            Color ink = kind == FlagKind.Sect && beige ? MapUi.Ink : MapUi.Paper;
            var body = Piece("Banner", x, y, size, size * .82f, cloth);
            var edge = body.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(.10f, .07f, .05f, .85f);
            edge.effectDistance = new Vector2(1, -1);
            switch (kind)
            {
                case FlagKind.Outlaw:
                    // Ragged hem: three tatters of different lengths, and two crossed blades.
                    for (int i = 0; i < 3; i++)
                        Piece("Tatter", x + i * size / 3f + 1, y + size * .82f, size / 3f - 2, size * (i == 1 ? .10f : .20f), cloth);
                    Blade(45);
                    Blade(-45);
                    break;
                case FlagKind.Court:
                    // Gold border and a square official seal.
                    Piece("Gold hem", x, y + size * .82f - 2, size, 2, new Color(.93f, .74f, .30f));
                    var seal = Piece("Seal", x + size * .22f, y + size * .14f, size * .56f, size * .56f, new Color(.93f, .76f, .32f));
                    Glyph(seal, glyph, new Color(.55f, .10f, .06f), size * .56f);
                    break;
                case FlagKind.Foreign:
                    // Swallow tails and a row of wolf fangs along the top.
                    Piece("Tail", x, y + size * .82f, size * .38f, size * .18f, cloth);
                    Piece("Tail", x + size * .62f, y + size * .82f, size * .38f, size * .18f, cloth);
                    for (int i = 0; i < 3; i++)
                    {
                        var fang = Piece("Fang", x + size * (.18f + i * .26f), y + size * .04f, size * .14f, size * .14f, MapUi.Paper);
                        fang.localRotation = Quaternion.Euler(0, 0, 45);
                    }
                    Glyph(Piece("Field", x + size * .2f, y + size * .3f, size * .6f, size * .46f, Color.clear), glyph, ink, size * .5f);
                    break;
                default:
                    // Round crest (a diamond-cut disc) with the sect's glyph.
                    var crest = Piece("Crest", x + size * .2f, y + size * .1f, size * .6f, size * .6f, beige ? new Color(.20f, .47f, .45f) : MapUi.Paper);
                    crest.localRotation = Quaternion.Euler(0, 0, 45);
                    var face = Piece("Crest face", x + size * .25f, y + size * .15f, size * .5f, size * .5f, beige ? new Color(.20f, .47f, .45f) : MapUi.Paper);
                    Glyph(face, glyph, beige ? MapUi.Paper : new Color(.12f, .30f, .28f), size * .5f);
                    break;
            }

            RectTransform Piece(string name, float px, float py, float w, float h, Color color)
            {
                var piece = MapUi.Panel(rect, name, px, py, w, h, color);
                piece.GetComponent<Image>().raycastTarget = false;
                return piece;
            }
            void Blade(float angle)
            {
                var blade = Piece("Blade", x + size * .1f, y + size * .38f, size * .8f, Mathf.Max(2, size * .08f), new Color(.86f, .86f, .82f));
                blade.pivot = new Vector2(.5f, .5f);
                blade.anchoredPosition = new Vector2(x + size * .5f, -(y + size * .41f));
                blade.localRotation = Quaternion.Euler(0, 0, angle);
            }
            static void Glyph(RectTransform parent, string text, Color color, float box)
            {
                if (string.IsNullOrEmpty(text)) return;
                var label = MapUi.Text(parent, text, 0, 0, box, box, Mathf.RoundToInt(box * .72f), color, TextAnchor.MiddleCenter, "Names");
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            }
        }

        private static bool Beige(MapFaction faction) => faction.type is "clan" or "merchant";

        /// <summary>Map-corner legend: one aligned row per sign (node types, then the four flag kinds, the party).</summary>
        private void BuildLegend()
        {
            const float row = 26, width = 196;
            var entries = new (string label, Action<RectTransform> icon)[]
            {
                ("大城　雄城重鎮", r => NodeIcon(r, "city")),
                ("小城　縣鎮驛站", r => NodeIcon(r, "town")),
                ("山門　門派名山", r => NodeIcon(r, "sect")),
                ("關隘　險要雄關", r => NodeIcon(r, "pass")),
                ("綠林山匪", r => DrawFlag(r, FlagKind.Outlaw, "", 4, 2, 20)),
                ("朝廷・方鎮", r => DrawFlag(r, FlagKind.Court, "", 4, 2, 20)),
                ("武林門派・幫會", r => DrawFlag(r, FlagKind.Sect, "", 4, 2, 20)),
                ("外族", r => DrawFlag(r, FlagKind.Foreign, "", 4, 2, 20)),
                ("行　我方隊伍", r =>
                {
                    var token = MapUi.Panel(r, "Party", 5, 2, 20, 20, MapUi.Red);
                    token.GetComponent<Image>().raycastTarget = false;
                    MapUi.Text(token, "行", 0, 0, 20, 20, 18, MapUi.Paper, TextAnchor.MiddleCenter);
                })
            };
            float height = 34 + entries.Length * row + 8;
            var box = MapUi.Kit(viewport, "Map legend", 8, 8, width, height, "Paper", false);
            box.SetAsLastSibling();
            MapUi.Text(box, "圖例", 12, 4, width - 24, 28, 19, MapUi.Red, TextAnchor.MiddleLeft, "Names");
            for (int i = 0; i < entries.Length; i++)
            {
                var line = MapUi.Rect(box, entries[i].label, 10, 34 + i * row, width - 20, row);
                entries[i].icon(line);
                var text = MapUi.Text(line, entries[i].label, 34, 0, width - 54, row, 18, MapUi.Ink);
                text.resizeTextMinSize = 15;
            }

            void NodeIcon(RectTransform parent, string type)
            {
                var texture = Icon(type);
                var image = MapUi.Rect(parent, "Icon", 2, 1, 26, 24).gameObject.AddComponent<RawImage>();
                image.texture = texture;
                image.color = texture == null ? MapUi.Ink : Color.white;
                image.raycastTarget = false;
            }
        }

        /// <summary>v1126 #15: the footer keeps one short line; the controls show only while the pointer rests on 「操作」.</summary>
        private void BuildFooterHelp(RectTransform footer)
        {
            var chip = MapUi.Button(footer, "？操作說明", 1400, 22, 150, 40, () => { }, null, 18);
            chip.name = "操作說明";
            Hover(chip.gameObject, () => "滾輪：縮放地圖\n左鍵、右鍵或中鍵拖曳：平移\nWASD：平移\n右鍵單擊／Esc：返回上一頁\n點據點或旗幟：查看詳情");
        }

        private void EnsureHoverTip()
        {
            if (hoverTip != null) return;
            hoverTip = MapUi.Kit(frame, "Hover tip", 0, 0, 300, 60, "Wood", false);
            hoverText = MapUi.Text(hoverTip, "", 12, 8, 276, 44, 18, MapUi.Paper, TextAnchor.UpperLeft);
            hoverTip.gameObject.SetActive(false);
        }

        /// <summary>Shows a small tip beside the pointer while it rests on <paramref name="target"/>.</summary>
        private void Hover(GameObject target, Func<string> text)
        {
            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowHover(text()));
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideHover());
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
            // A click still has to reach the button / map underneath.
            var pass = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pass.callback.AddListener(_ => HideHover());
            trigger.triggers.Add(pass);
        }

        private void ShowHover(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EnsureHoverTip();
            hoverText.text = text;
            int lines = text.Split('\n').Length;
            float width = Mathf.Clamp(text.Split('\n').Max(line => line.Length) * 19 + 28, 160, 420);
            float height = lines * 25 + 18;
            hoverText.rectTransform.sizeDelta = new Vector2(width - 24, height - 14);
            hoverTip.sizeDelta = new Vector2(width, height);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, Input.mousePosition, canvas.worldCamera, out var local);
            // frame pivot is centred; MapUi rects anchor top-left.
            float x = local.x + frame.rect.width / 2 + 18, y = frame.rect.height / 2 - local.y + 18;
            x = Mathf.Min(x, frame.rect.width - width - 12);
            y = Mathf.Min(y, frame.rect.height - height - 12);
            hoverTip.anchoredPosition = new Vector2(x, -y);
            hoverTip.SetAsLastSibling();
            hoverTip.gameObject.SetActive(true);
        }

        private void HideHover()
        {
            if (hoverTip != null) hoverTip.gameObject.SetActive(false);
        }

        /// <summary>v1126 #13: what a stronghold is and who holds it (「洛陽　州城｜朝廷・大唐朝廷・中立」).</summary>
        private string NodeTip(MapNode node)
        {
            var faction = data.Controllers(node).FirstOrDefault();
            string head = $"{node.Label}　{TypeName(node.type)}";
            if (host != null && host.HoldsNode(node.id)) return head + "\n本派據點";
            if (faction == null) return head + "\n無人據守";
            bool known = host != null && host.IsFactionKnown(faction.id);
            return head + $"\n{KindName(faction)}・{faction.Label}" +
                (known && host != null ? $"・{faction.RelationWith(host.RelationShift(faction.id))}" : "・虛實未明");
        }

        private string FlagTip(MapFaction faction)
        {
            bool known = host != null && host.IsFactionKnown(faction.id);
            return $"{faction.Label}\n{KindName(faction)}" +
                (known && host != null ? $"・{faction.RelationWith(host.RelationShift(faction.id))}" : "・虛實未明");
        }

        /// <summary>
        /// v1126 #12: what the attack button on a stronghold card does. Blocked cases return no action and a reason
        /// with the way forward. (Rules live with the host; this mirrors the existing ones — outlaw holds are
        /// assaulted directly, other powers go through 門派戰役 once hostile (朝廷 / 方鎮 / 外族 at any time, with a
        /// warning) and the party stands at the stronghold.)
        /// </summary>
        private (string label, string note, bool blocked, Action run) AttackOption(MapNode node, MapFaction faction, bool held,
            bool known, Action assault, Action campaign)
        {
            const string attack = "攻打";
            if (held) return (attack, "本派據點，無須攻打。", true, null);
            if (faction == null) return (attack, "此地無人據守，行軍前往即可。", true, null);
            if (faction.Outlaw)
                return ($"率眾攻討\n勝算 {host.AssaultChance(Garrison(node), OutlawGuard(node) == "丙" ? .9f : .7f):P0}",
                    host.ActionPoints >= 2 ? "草莽據點：可直接率眾攻討（行動力 2）。" : "率眾攻討需行動力 2，下月再戰。",
                    host.ActionPoints < 2, assault);
            if (!known) return (attack, "虛實未明：先派人打探，再議攻守。", true, null);
            if (host is GameState game && game.Patron?.id == faction.id)
                return (attack, $"{faction.Label}是本門靠山，須先解除依附才能攻打。", true, null);
            if (node.id == host.StartNode) return (attack, "此為本門開局立足之地，不可攻打。", true, null);
            string stance = faction.StanceWith(host.RelationShift(faction.id));
            string relation = faction.RelationWith(host.RelationShift(faction.id));
            string warning = KindOf(faction) switch
            {
                FlagKind.Court when faction.type == "military" => $"方鎮據點：攻打將與{faction.Label}為敵。",
                FlagKind.Court => $"朝廷據點：攻打將與{faction.Label}為敵。",
                FlagKind.Foreign => $"外族據點：攻打將與{faction.Label}開戰。",
                _ => null
            };
            if (warning == null && stance != "hostile")
                return (attack, $"與{faction.Label}仍是{relation}，需先在外交中交惡、宣戰。", true, null);
            if (node.id != host.PartyNode) return ("門派戰役", (warning ?? "") + "隊伍須先行軍至此，方可發動門派戰役。", true, null);
            if (campaign == null) return ("門派戰役", "此處暫不能開戰。", true, null);
            if (host.ActionPoints < 2) return ("門派戰役", "門派戰役需行動力 2，下月再戰。", true, null);
            return ("門派戰役 ›", warning ?? $"與{faction.Label}已成敵對，可發動門派戰役（行動力 2）。", false, campaign);
        }
    }
}
