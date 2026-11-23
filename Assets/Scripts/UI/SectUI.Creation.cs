using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>Snapshot of the hero-in-progress shown on the creation summary card.</summary>
    internal sealed class CreationSheet
    {
        public string name, title, portrait, gender, background, picksHeading;
        public int[] attrs;
        public SkillLine[] lines;
        public readonly List<(string label, int left, int total)> budgets = new();
        public readonly List<string> picks = new();
    }

    public sealed partial class SectUI
    {
        private RectTransform storyScreen, creationGrid;
        private RectTransform creationBackdrop, creationPlaque, creationSummary, characterDecoration;
        private readonly ScrollRect[] artScrolls = new ScrollRect[2];
        private float creationListWidth;
        private Text storyText;
        private RawImage storyArt, previousStoryArt;
        private Action storyNext;
        private string[] storyPages;
        private int storyPage;
        private float storyStart;
        private float artStart;
        private const float CharactersPerSecond = 40f;

        internal void ShowCreation(string heading, IEnumerable<string> lines,
            IEnumerable<(string, Action)> choices, Action skip, Action next = null)
        {
            var backdrop = storyScreen != null ? storyArt.texture as Texture2D : null;
            CloseStoryBeat();
            recruitGrid = creationGrid = null;
            var buttons = choices?.ToList() ?? new List<(string, Action)>();
            if (next != null) buttons.Add(("繼續", next));
            if (skip != null && skip != next) buttons.Add(("略過", skip));
            ShowModal("", lines.Where(line => !string.IsNullOrWhiteSpace(line)), buttons.ToArray(), dismissible: false);
            foreach (Transform action in modalButtons)
                if (action.name != "繼續") UiKit.Skin(action.GetComponent<Image>(), "Wood");
            Anchor(modalCard, new(.07f, .07f), new(.93f, .93f), Vector2.zero, Vector2.zero);
            creationListWidth = modalCard.rect.width - 72;
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one,
                new(36, 88), new(-36, -112));
            creationBackdrop = Rect("Menu illustration", modal);
            creationBackdrop.SetAsFirstSibling();
            Fill(creationBackdrop);
            AddImage(creationBackdrop, backdrop ?? PortraitService.Story("title_bg") ?? UiKit.Backdrop()).color =
                new Color(.56f, .54f, .51f);
            var vignette = Rect("Ink vignette", creationBackdrop);
            Fill(vignette);
            AddImage(vignette, UiKit.Vignette());
            creationPlaque = Panel("Scroll title", modalCard, new(.5f, 1), new(.5f, 1),
                new(-275, -89), new(275, -17));
            UiKit.Skin(creationPlaque.GetComponent<Image>(), "Plaque");
            var title = Label(creationPlaque, heading, 34, TextAnchor.MiddleCenter);
            title.color = UiKit.HeaderGold;
            title.fontStyle = FontStyle.Bold;
            Fill(title.rectTransform, 10);
            Stamp(creationPlaque, heading[0].ToString(), 48);
            var divider = Rect("Ink rule", creationPlaque);
            Anchor(divider, Vector2.zero, Vector2.right, new(-190, -13), new(190, -6));
            var rule = divider.gameObject.AddComponent<Image>();
            rule.sprite = UiKit.Sprite("Divider");
            rule.raycastTarget = false;
            modalList.anchoredPosition = Vector2.zero;
        }

        private void ResetCreationLayout()
        {
            appearanceInput = null;
            var modalScroll = modalList.parent.GetComponent<ScrollRect>();
            modalScroll.verticalScrollbar = null;
            modalScroll.StopMovement();
            modalList.anchoredPosition = Vector2.zero;
            foreach (var decoration in new[] { creationBackdrop, creationPlaque, creationSummary, characterDecoration })
            {
                if (decoration == null) continue;
                decoration.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(decoration.gameObject);
            }
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one, new(32, 84), new(-32, -28));
        }

        private static readonly Color CardText = new(.96f, .89f, .75f);
        private static readonly Color CardMuted = new(.80f, .70f, .55f);
        private static readonly Color CardGold = new(.93f, .74f, .42f);
        private static readonly Color CardLow = new(.95f, .58f, .48f);

        /// <summary>Live character summary card docked on the right of every creation step.</summary>
        internal void CreationSummary(CreationSheet sheet, bool showPicks = true)
        {
            const int width = 372;
            creationListWidth -= width + 20;
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one,
                new(36, 88), new(-36 - width - 20, -112));
            creationSummary = Panel("角色總覽", modalCard, Vector2.right, Vector2.one,
                new(-36 - width, 88), new(-36, -112));
            UiKit.Skin(creationSummary.GetComponent<Image>(), "Wood");
            var card = creationSummary;

            var frame = Rect("肖像", card);
            Anchor(frame, Vector2.up, Vector2.up, new(18, -152), new(126, -16));
            frame.gameObject.AddComponent<Image>().color = new Color(.12f, .09f, .07f);
            var face = Rect("Face", frame);
            Fill(face, 3);
            var texture = PortraitService.Resolve(sheet.portrait, sheet.gender);
            var raw = AddImage(face, texture);
            if (texture != null)
            {
                float crop = 102f * texture.height / (130f * texture.width);
                raw.uvRect = crop <= 1 ? new Rect((1 - crop) / 2, 0, crop, 1) : new Rect(0, (1 - 1 / crop) / 2, 1, 1 / crop);
            }
            CardLabel(sheet.name ?? "", 30, CardText, 140, -16, -14, -54).font = UiKit.Font("Names", font, sheet.name ?? "");
            CardLabel(sheet.title ?? "", 21, CardGold, 140, -58, -14, -86);
            CardLabel(string.IsNullOrEmpty(sheet.background) ? "出身未定" : "出身　" + sheet.background, 19, CardMuted,
                140, -90, -14, -150, TextAnchor.UpperLeft);

            float y = -166;
            foreach (var (label, left, total) in sheet.budgets)
            {
                CardLabel(label, 19, CardMuted, 18, y, -150, y - 24);
                CardLabel($"剩 {left}／{total}", 20, left > 0 ? CardGold : CardText, 160, y, -18, y - 24, TextAnchor.MiddleRight);
                CardBar(card, 18, y - 28, -18, 7, total <= 0 ? 0 : left / (float)total, CardGold);
                y -= 42;
            }

            y -= 4;
            CardHeading("屬性", y);
            y -= 32;
            var attrs = AttributeSystem.Normalize(sheet.attrs);
            for (int index = 0; index < AttributeSystem.Count; index++)
            {
                var aIcon = UiKit.Icon(UiKit.AttrIcon(index));
                if (aIcon != null)
                {
                    var ic = Rect("AttrIcon", card);
                    Anchor(ic, Vector2.up, Vector2.up, new(16, y - 22), new(36, y - 2));
                    AddImage(ic, aIcon);
                }
                CardLabel(AttributeSystem.Names[index], 18, CardText, aIcon != null ? 40 : 18, y, -270, y - 24);
                CardBar(card, aIcon != null ? 92 : 80, y - 9, -62, 8, attrs[index] / (float)AttributeSystem.Max,
                    Color.Lerp(AttributeSystem.Tints[index], Color.white, .25f));
                CardLabel(attrs[index].ToString(), 19, attrs[index] > AttributeSystem.Base ? CardGold :
                    attrs[index] < AttributeSystem.Base ? CardLow : CardText, 310, y, -18, y - 24, TextAnchor.MiddleRight);
                y -= 26;
            }

            y -= 8;
            CardHeading("武學與體魄", y);
            y -= 32;
            var stats = AttributeSystem.Derive(attrs, sheet.lines);
            var cells = new[]
            {
                ("攻擊", stats.Attack.ToString()), ("先手", stats.Speed.ToString()),
                ("氣血", stats.MaxHp.ToString()), ("內力", stats.MaxQi.ToString()),
                ("閃避", $"{stats.Dodge:P0}"), ("奇遇", $"{stats.Fortune:P0}")
            };
            for (int index = 0; index < cells.Length; index++)
            {
                int column = index % 2;
                float rowY = y - index / 2 * 28;
                var cell = Rect(cells[index].Item1, card);
                Anchor(cell, new(column * .5f, 1), new((column + 1) * .5f, 1),
                    new(column == 0 ? 18 : 8, rowY - 24), new(column == 0 ? -8 : -18, rowY));
                var name = Label(cell, cells[index].Item1, 18, TextAnchor.MiddleLeft);
                name.color = CardMuted;
                Fill(name.rectTransform);
                var value = Label(cell, cells[index].Item2, 20, TextAnchor.MiddleRight);
                value.color = CardText;
                value.font = UiKit.Font("Numbers", font, value.text);
                Fill(value.rectTransform);
            }
            y -= 3 * 28 + 8;

            if (!showPicks) return;
            CardHeading(sheet.picksHeading ?? "已選", y);
            y -= 32;
            var picks = CardLabel(sheet.picks.Count == 0 ? "尚未選擇" : string.Join("　", sheet.picks), 18,
                sheet.picks.Count == 0 ? CardMuted : CardText, 18, y, -18, 0, TextAnchor.UpperLeft);
            Anchor(picks.rectTransform, Vector2.zero, Vector2.one, new(18, 14), new(-18, y));
            picks.horizontalOverflow = HorizontalWrapMode.Wrap;
            picks.resizeTextMinSize = 18;

            void CardHeading(string text, float top)
            {
                CardLabel(text, 21, CardGold, 18, top, -18, top - 26);
                var rule = Rect("Rule", card);
                Anchor(rule, Vector2.up, Vector2.one, new(18, top - 29), new(-18, top - 28));
                var ink = rule.gameObject.AddComponent<Image>();
                ink.color = new Color(.93f, .74f, .42f, .35f);
                ink.raycastTarget = false;
            }

            // left/right are offsets from the card's left/right edges; top/bottom are offsets from its top edge.
            Text CardLabel(string text, int size, Color color, float left, float top, float right, float bottom,
                TextAnchor alignment = TextAnchor.MiddleLeft)
            {
                var label = Label(card, text, size, alignment);
                label.color = color;
                Anchor(label.rectTransform, Vector2.up, Vector2.one, new(left, bottom), new(right, top));
                return label;
            }
        }

        /// <summary>Horizontal meter anchored to the top of <paramref name="parent"/> (right offset is from the right edge).</summary>
        private static void CardBar(RectTransform parent, float left, float top, float right, float height, float fill, Color color)
        {
            var track = Rect("Meter", parent);
            Anchor(track, Vector2.up, Vector2.one, new(left, top - height), new(right, top));
            var back = track.gameObject.AddComponent<Image>();
            back.color = new Color(0, 0, 0, .35f);
            back.raycastTarget = false;
            var bar = Rect("Meter fill", track);
            Anchor(bar, Vector2.zero, new(Mathf.Clamp01(fill), 1), Vector2.zero, Vector2.zero);
            var ink = bar.gameObject.AddComponent<Image>();
            ink.color = color;
            ink.raycastTarget = false;
        }

        /// <summary>Compact 7×2 grid of skill-line tiles: name, rank / potential cap and a tier meter.</summary>
        internal void SkillTiles(SkillLine[] lines)
        {
            var families = new[] { "combat", "production" };
            var row = CreationRow("技能一覽", 2 * 66 + 8);
            for (int family = 0; family < families.Length; family++)
            {
                var categories = RankSystem.Categories.Where(category => category.family == families[family]).Take(7).ToArray();
                for (int index = 0; index < categories.Length; index++)
                {
                    var category = categories[index];
                    var line = Array.Find(lines ?? Array.Empty<SkillLine>(), entry => entry?.id == category.id);
                    var tile = Panel(category.name, row, new(index / 7f, 1), new((index + 1) / 7f, 1),
                        new(3, -66 - family * 74), new(-3, -family * 74), false);
                    UiKit.Skin(tile.GetComponent<Image>(), "Wood");
                    var sIcon = UiKit.Icon(UiKit.SkillIcon(category.id));
                    if (sIcon != null)
                    {
                        var ic = Rect("SkillIcon", tile);
                        Anchor(ic, Vector2.up, Vector2.up, new(6, -32), new(30, -8));
                        AddImage(ic, sIcon);
                    }
                    var name = Label(tile, category.name, 18, TextAnchor.MiddleLeft);
                    name.color = family == 0 ? new Color(.98f, .80f, .66f) : new Color(.78f, .90f, .78f);
                    Anchor(name.rectTransform, Vector2.up, Vector2.one, new(sIcon != null ? 34 : 10, -34), new(-50, -6));
                    int rank = RankSystem.Index(line?.rank), cap = RankSystem.Index(line?.cap);
                    var tier = Label(tile, $"{RankSystem.At(rank)}/{RankSystem.At(cap)}", 19, TextAnchor.MiddleRight);
                    tier.color = Color.Lerp(RankSystem.Color(RankSystem.At(rank)), Color.white, .45f);
                    tier.font = UiKit.Font("Numbers", font, tier.text);
                    Anchor(tier.rectTransform, Vector2.up, Vector2.one, new(10, -34), new(-8, -6));
                    CardBar(tile, 10, -42, -10, 6, (cap + 1) / (float)RankSystem.Count, new Color(1, 1, 1, .22f));
                    CardBar(tile, 10, -42, -10, 6, (rank + 1) / (float)RankSystem.Count, RankSystem.Color(RankSystem.At(rank)));
                    if (cap >= RankSystem.Index("S"))
                    {
                        var star = Label(tile, "潛力 " + RankSystem.At(cap), 18, TextAnchor.MiddleLeft);
                        star.color = CardGold;
                        Anchor(star.rectTransform, Vector2.zero, Vector2.right, new(10, 3), new(-6, 20));
                    }
                }
            }
        }

        internal void CreationTabs(IEnumerable<(string label, bool active, Action run)> filters)
        {
            var row = CreationRow("稀有度", 48);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            foreach (var (label, active, run) in filters)
                UiKit.Skin(Button(row, label, run).GetComponent<Image>(), active ? "Selected" : "Wood");
        }

        internal Vector2 ArtsScrollPosition => new(
            artScrolls[0] == null ? 1 : artScrolls[0].verticalNormalizedPosition,
            artScrolls[1] == null ? 1 : artScrolls[1].verticalNormalizedPosition);

        private void Stamp(RectTransform parent, string glyph, int size)
        {
            var seal = Panel("Seal", parent, Vector2.one, Vector2.one,
                new(-size - 12, -size - 12), new(-12, -12));
            UiKit.Skin(seal.GetComponent<Image>(), "Seal");
            seal.GetComponent<Image>().raycastTarget = false;
            var ink = Label(seal, glyph, size / 2, TextAnchor.MiddleCenter);
            ink.color = new Color(1, .88f, .71f);
            Fill(ink.rectTransform);
        }

        internal void ModalPortrait(string id, string gender, bool fullScreen = false)
        {
            var row = CreationRow("肖像", fullScreen ? 382 : 128);
            var image = Rect("Speaker", row);
            float halfWidth = fullScreen ? 144 : 45;
            Anchor(image, new(.5f, 0), new(.5f, 1), new(-halfWidth, 4), new(halfWidth, -4));
            AddImage(image, PortraitService.Resolve(id, gender));
            if (!fullScreen)
                Anchor(modalCard, new(.5f, .25f), new(.5f, .25f), new(-448, -180), new(448, 180));
        }

        internal void CreationIdentity(string title, string portraitId, string gender, Action choose)
        {
            var card = Button(creationGrid, title, choose);
            UiKit.Skin(card.GetComponent<Image>(), "Wood");
            var portrait = Rect("Portrait", card);
            Anchor(portrait, new(.5f, 0), new(.5f, 1), new(-177, 78), new(177, -22));
            AddImage(portrait, PortraitService.Resolve(portraitId, gender));
            var caption = card.GetComponentInChildren<Text>();
            caption.fontSize = 30;
            caption.resizeTextMaxSize = 30;
            Anchor(caption.rectTransform, Vector2.zero, Vector2.right, new(16, 15), new(-16, 71));
        }

        internal void CreationGrid(int columns, int rows, float cellHeight)
        {
            const float gap = 12;
            creationGrid = CreationRow("選項", rows * cellHeight + (rows - 1) * gap);
            var grid = creationGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2((creationListWidth - (columns - 1) * gap) / columns, cellHeight);
            grid.spacing = Vector2.one * gap;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }

        internal void CreationArts(CreationOption[] combat, CreationOption[] production,
            Vector2 scrollPosition, Action<CreationOption> draw)
        {
            var row = CreationRow("可習技藝", 488);
            AddFamily(combat, 0);
            AddFamily(production, 1);
            creationGrid = null;
            Canvas.ForceUpdateCanvases();
            for (int column = 0; column < artScrolls.Length; column++)
                artScrolls[column].verticalNormalizedPosition = Mathf.Clamp01(scrollPosition[column]);

            void AddFamily(CreationOption[] arts, int column)
            {
                var pane = Panel("技藝", row, new(column * .5f, 0), new((column + 1) * .5f, 1),
                    new(5, 0), new(-5, 0));
                creationGrid = ScrollList(pane, new(13, 14), new(-31, -14));
                var scroll = creationGrid.parent.GetComponent<ScrollRect>();
                artScrolls[column] = scroll;
                foreach (var art in arts) draw(art);
                var track = Rect("Scroll track", pane);
                Anchor(track, Vector2.right, Vector2.one, new(-21, 14), new(-10, -14));
                track.gameObject.AddComponent<Image>().color = new Color(.36f, .27f, .18f, .24f);
                var slide = Rect("Scroll travel area", track);
                Fill(slide);
                var thumb = Rect("Scroll grip", slide);
                Fill(thumb);
                var grip = thumb.gameObject.AddComponent<Image>();
                grip.color = new Color(.56f, .26f, .19f);
                var bar = track.gameObject.AddComponent<Scrollbar>();
                bar.direction = Scrollbar.Direction.BottomToTop;
                bar.targetGraphic = grip;
                bar.handleRect = thumb;
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
        }

        internal void CreationChoice(string label, string description, string image, Action run,
            string rarity = null, bool picked = false, int? cost = null, bool banner = false, string tags = null, bool wideDescription = false, string icon = null,
            bool reserveEffect = false)
        {
            var item = Button(creationGrid == null ? modalList : creationGrid, label, run);
            item.GetComponent<Button>().interactable = run != null;
            var layout = item.GetComponent<LayoutElement>();
            var caption = item.GetComponentInChildren<Text>();
            var art = string.IsNullOrEmpty(image) ? null : PortraitService.Story(image);
            layout.preferredHeight = art == null ? (string.IsNullOrEmpty(tags) ? 124 : 146) : 164;
            var frame = item.GetComponent<Image>();
            UiKit.Skin(frame, picked ? "Selected" : "Wood");
            frame.color = rarity switch
            {
                "legendary" => new Color(1, .78f, .59f),
                "rare" => new Color(1, .87f, .64f),
                _ => new Color(.93f, .92f, .87f)
            };
            bool hasBanner = art != null && banner;
            int left = art == null || hasBanner ? 16 : wideDescription ? 90 : creationGrid == null ? 200 : 196;
            if (icon != null && art == null)
            {
                left = 58;
                AddCardIcon(item, icon, 16, 36);
            }
            int top = hasBanner ? 198 : 0;
            bool storyChoice = reserveEffect || (art != null && rarity == null && !cost.HasValue);
            caption.alignment = TextAnchor.MiddleLeft;
            caption.resizeTextMinSize = 20;
            Anchor(caption.rectTransform, Vector2.up, Vector2.one, new(left, -44 - top),
                new(cost.HasValue ? -120 : -16, -6 - top));
            if (!string.IsNullOrEmpty(rarity))
            {
                var ribbon = Rect("Rarity ribbon", item);
                Anchor(ribbon, Vector2.zero, Vector2.up, new(5, 8), new(10, -8));
                var stripe = ribbon.gameObject.AddComponent<Image>();
                stripe.raycastTarget = false;
                stripe.color = rarity switch
                {
                    "legendary" => new Color(.95f, .66f, .22f),
                    "rare" => new Color(.40f, .62f, .86f),
                    _ => new Color(.62f, .58f, .50f)
                };
            }
            if (cost.HasValue)
            {
                var coin = Rect("Cost seal", item);
                Anchor(coin, Vector2.one, Vector2.one, new(picked ? -112 : -60, -46), new(picked ? -68 : -16, -8));
                var disc = coin.gameObject.AddComponent<Image>();
                disc.raycastTarget = false;
                disc.color = cost < 0 ? new Color(.30f, .45f, .36f) : new Color(.62f, .20f, .14f);
                var badge = Label(coin, cost < 0 ? $"+{-cost}" : cost.ToString(), 22, TextAnchor.MiddleCenter);
                badge.color = new Color(1, .90f, .70f);
                badge.font = UiKit.Font("Numbers", font, badge.text);
                Fill(badge.rectTransform);
            }
            if (picked)
            {
                Stamp(item, "選", 36);
                var glow = item.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(.89f, .66f, .31f, .75f);
                glow.effectDistance = new Vector2(2, 2);
            }
            var body = Label(item, description ?? "", wideDescription ? 18 : 20);
            if (wideDescription)
            {
                body.alignment = TextAnchor.UpperLeft;
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.verticalOverflow = VerticalWrapMode.Truncate;
                body.lineSpacing = .94f;
            }
            body.color = CardText;
            body.resizeTextForBestFit = false;
            if (rarity != null && icon != null)
                body.verticalOverflow = VerticalWrapMode.Truncate;
            int tagTop = wideDescription ? 80 : 38;
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new(left, string.IsNullOrEmpty(tags) ? (storyChoice ? 34 : 8) : tagTop + 4),
                new(-16, -46 - top));
            if (storyChoice && string.IsNullOrEmpty(tags))
            {
                var effect = Label(item, "", 18, TextAnchor.MiddleLeft);
                effect.name = "影響";
                effect.color = CardGold;
                Anchor(effect.rectTransform, Vector2.zero, Vector2.right, new(left, 6), new(-16, 30));
            }
            if (!string.IsNullOrEmpty(tags))
            {
                var strip = Rect("Effect tags", item);
                Anchor(strip, Vector2.zero, Vector2.right, new(left - 4, 8), new(-12, tagTop));
                var wash = strip.gameObject.AddComponent<Image>();
                wash.color = new Color(.05f, .04f, .03f, .38f);
                wash.raycastTarget = false;
                var chips = Label(strip, tags, 18, wideDescription ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
                chips.color = new Color(.99f, .80f, .45f);
                chips.verticalOverflow = VerticalWrapMode.Overflow;
                chips.horizontalOverflow = wideDescription ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                chips.resizeTextForBestFit = false;
                Anchor(chips.rectTransform, Vector2.zero, Vector2.one, new(8, 0), new(-6, 0));
                var clip = strip.gameObject.AddComponent<RectMask2D>();
                clip.padding = Vector4.zero;
            }
            if (art != null)
            {
                var thumbnail = Rect("Choice illustration", item);
                var raw = AddImage(thumbnail, art);
                var grid = creationGrid == null ? null : creationGrid.GetComponent<GridLayoutGroup>();
                float artAspect = art.width / (float)art.height;
                if (hasBanner)
                {
                    Anchor(thumbnail, Vector2.up, Vector2.one, new(10, -194), new(-10, -10));
                    float width = grid == null ? creationListWidth : grid.cellSize.x;
                    float crop = Mathf.Min(1, artAspect * 184 / (width - 20));
                    raw.uvRect = new Rect(0, (1 - crop) / 2, 1, crop);
                }
                else
                {
                    Anchor(thumbnail, Vector2.zero, Vector2.up, new(8, 10), new(left - 12, -10));
                    float height = (grid == null ? layout.preferredHeight : grid.cellSize.y) - 20;
                    float crop = Mathf.Min(1, (left - 20) / (height * artAspect));
                    raw.uvRect = new Rect((1 - crop) / 2, 0, crop, 1);
                }
            }
        }

        internal void CreationName(string value, Action<string> changed, Func<string> suggest)
        {
            var row = CreationRow("姓名", 64);
            var entry = Panel("姓名輸入", row, Vector2.zero, Vector2.one, new(16, 4), new(-176, -4));
            var text = Label(entry, "", 26, TextAnchor.MiddleLeft);
            Fill(text.rectTransform, 8);
            var input = entry.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.characterLimit = 12;
            input.text = value;
            input.onEndEdit.AddListener(name => changed(name));
            var random = Button(row, "隨機姓名", () => { input.text = suggest(); changed(input.text); });
            Anchor(random, Vector2.right, Vector2.one, new(-160, 4), new(-8, -4));
        }

        private Action appearanceInput;

        internal void UpdateAppearance() => appearanceInput?.Invoke();

        internal void CreationPortraits(CharacterLook[] looks, CharacterLook selected, string characterTitle, int page,
            Action<int> changePage, Action<CharacterLook> choose)
        {
            const int pageSize = 10;
            int pages = (looks.Length + pageSize - 1) / pageSize;
            page = Mathf.Clamp(page, 0, pages - 1);
            var row = CreationRow("形貌", 500);
            var grid = Rect("肖像選擇", row);
            Anchor(grid, Vector2.zero, new(.55f, 1), Vector2.zero, new(-10, 0));
            var visible = looks.Skip(page * pageSize).Take(pageSize).ToArray();
            for (int index = 0; index < visible.Length; index++)
            {
                var look = visible[index];
                int column = index % 5, line = index / 5;
                var card = Button(grid, look.label, () => choose(look));
                Anchor(card, new(column / 5f, 0), new((column + 1) / 5f, 1),
                    new(4, 241 - line * 218), new(-4, -45 - line * 218));
                UiKit.Skin(card.GetComponent<Image>(), look == selected ? "Selected" : "Wood");
                var caption = card.GetComponentInChildren<Text>();
                Anchor(caption.rectTransform, Vector2.zero, Vector2.right, new(3, 3), new(-3, 38));
                var art = Rect("Portrait", card);
                Anchor(art, new(.5f, 0), new(.5f, 1), new(-59, 43), new(59, -9));
                AddImage(art, LookPortrait(look));
                if (look == selected) Stamp(card, "選", 32);
            }
            if (pages > 1)
            {
                var indicator = Label(grid, $"第 {page + 1}／{pages} 頁", 22, TextAnchor.MiddleCenter);
                Anchor(indicator.rectTransform, new(.33f, 1), new(.67f, 1), new(0, -39), Vector2.zero);
                PageButton("‹", -1, .06f);
                PageButton("›", 1, .76f);
            }
            else
            {
                var heading = Label(grid, "選擇肖像", 25, TextAnchor.MiddleLeft);
                Anchor(heading.rectTransform, Vector2.up, Vector2.one, new(4, -39), Vector2.zero);
            }

            var sheet = Panel("水墨肖像", row, new(.55f, 0), Vector2.one,
                new(8, 14), new(-5, 0));
            var portraitFrame = Panel("裱框", sheet, new(0, .5f), new(0, .5f),
                new(16, -190), new(302, 190));
            UiKit.Skin(portraitFrame.GetComponent<Image>(), "Wood");
            var portrait = Rect("選定肖像", portraitFrame);
            Fill(portrait, 5);
            AddImage(portrait, LookPortrait(selected));
            var title = Label(sheet, characterTitle, 32, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, Vector2.up, Vector2.one, new(322, -127), new(-20, -70));
            var style = Panel("形貌風格", sheet, Vector2.up, Vector2.one,
                new(320, -176), new(-24, -135));
            UiKit.Skin(style.GetComponent<Image>(), "Wood");
            var styleLabel = Label(style, selected.label, 22, TextAnchor.MiddleCenter);
            styleLabel.color = MapUi.Paper;
            Fill(styleLabel.rectTransform, 5);
            var detail = Label(sheet, selected.desc ?? "", 22, TextAnchor.UpperLeft);
            Anchor(detail.rectTransform, Vector2.zero, Vector2.one, new(322, 34), new(-22, -199));

            appearanceInput = () =>
            {
                var focus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                if (focus != null && focus.GetComponent<InputField>()?.isFocused == true) return;
                float wheel = RectTransformUtility.RectangleContainsScreenPoint(grid, Input.mousePosition,
                    Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera)
                    ? Input.mouseScrollDelta.y : 0;
                int pageDelta = Input.GetKeyDown(KeyCode.PageUp) || wheel > 0 ? -1 :
                    Input.GetKeyDown(KeyCode.PageDown) || wheel < 0 ? 1 : 0;
                if (pageDelta != 0 && page + pageDelta >= 0 && page + pageDelta < pages)
                {
                    changePage(page + pageDelta);
                    return;
                }
                int move = Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : Input.GetKeyDown(KeyCode.RightArrow) ? 1 :
                    Input.GetKeyDown(KeyCode.UpArrow) ? -5 : Input.GetKeyDown(KeyCode.DownArrow) ? 5 : 0;
                if (move != 0 && visible.Length > 0)
                    choose(visible[Mathf.Clamp(Array.IndexOf(visible, selected) + move, 0, visible.Length - 1)]);
            };

            void PageButton(string label, int delta, float left)
            {
                bool enabled = page + delta >= 0 && page + delta < pages;
                var button = Button(grid, label, enabled ? () => changePage(page + delta) : null);
                Anchor(button, new(left, 1), new(left + .18f, 1), new(0, -39), Vector2.zero);
                button.GetComponent<Button>().interactable = enabled;
            }
        }

        private static Texture2D LookPortrait(CharacterLook look) =>
            PortraitService.Resolve(string.IsNullOrEmpty(look.portrait) ? look.id : look.portrait, look.gender);

        /// <summary>One attribute row: seal glyph, name, pip track (min / base markers), value, delta and − / ＋.</summary>
        internal void CreationAttribute(int index, int current, int baseValue, int minimum, int maximum, int bonus,
            string description, Action less, Action more)
        {
            string name = AttributeSystem.Names[index];
            Color tint = AttributeSystem.Tints[index];
            var row = CreationRow(name, 84);
            var card = Panel(name, row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
            UiKit.Skin(card.GetComponent<Image>(), "Wood");
            var seal = Rect("Seal", card);
            Anchor(seal, new(0, .5f), new(0, .5f), new(16, -29), new(74, 29));
            var sealInk = seal.gameObject.AddComponent<Image>();
            sealInk.color = tint;
            sealInk.raycastTarget = false;
            var attrIcon = UiKit.Icon(UiKit.AttrIcon(index));
            if (attrIcon != null)
            {
                var ic = Rect("AttrIcon", seal);
                Fill(ic, 3);
                AddImage(ic, attrIcon);
            }
            else
            {
                var glyph = Label(seal, AttributeSystem.Glyphs[index], 32, TextAnchor.MiddleCenter);
                glyph.color = new Color(1, .93f, .80f);
                Fill(glyph.rectTransform);
            }
            var caption = Label(card, name, 27, TextAnchor.MiddleLeft);
            caption.color = CardText;
            Anchor(caption.rectTransform, Vector2.up, Vector2.up, new(90, -42), new(196, -6));
            var benefit = Label(card, description, 18, TextAnchor.MiddleLeft);
            benefit.color = CardMuted;
            Anchor(benefit.rectTransform, Vector2.zero, Vector2.right, new(90, 6), new(-300, 36));

            var track = Rect("Attribute pips", card);
            Anchor(track, Vector2.up, Vector2.one, new(206, -33), new(-300, -15));
            for (int pip = 1; pip <= maximum; pip++)
            {
                var cell = Rect("Pip", track);
                Anchor(cell, new((pip - 1) / (float)maximum, 0), new(pip / (float)maximum, 1), new(1.5f, 0), new(-1.5f, 0));
                var ink = cell.gameObject.AddComponent<Image>();
                ink.raycastTarget = false;
                ink.color = pip <= current
                    ? (pip > baseValue ? Color.Lerp(tint, new Color(1, .86f, .55f), .55f) : Color.Lerp(tint, Color.white, .12f))
                    : pip <= minimum ? new Color(.05f, .04f, .03f, .55f) : new Color(1, .95f, .85f, .14f);
                if (pip == baseValue)
                {
                    var tick = Rect("Base mark", cell);
                    Anchor(tick, new(1, 0), Vector2.one, new(0, -6), new(3, 6));
                    var mark = tick.gameObject.AddComponent<Image>();
                    mark.color = CardGold;
                    mark.raycastTarget = false;
                }
            }
            var value = Label(card, current.ToString(), 36, TextAnchor.MiddleCenter);
            value.color = current > baseValue ? CardGold : current < baseValue ? CardLow : CardText;
            value.font = UiKit.Font("Numbers", font, value.text);
            Anchor(value.rectTransform, Vector2.right, Vector2.one, new(-290, 28), new(-196, -4));
            int delta = current - baseValue;
            string note = (delta == 0 ? "基礎" : $"{delta:+0;-0}") + (bonus != 0 ? $"　加成{bonus:+0;-0}" : "");
            var deltaLabel = Label(card, note, 18, TextAnchor.MiddleCenter);
            deltaLabel.color = CardMuted;
            Anchor(deltaLabel.rectTransform, Vector2.right, Vector2.right, new(-300, 4), new(-186, 28));
            var minus = Button(card, "−", less);
            var plus = Button(card, "＋", more);
            Anchor(minus, Vector2.right, Vector2.one, new(-178, 16), new(-104, -16));
            Anchor(plus, Vector2.right, Vector2.one, new(-90, 16), new(-16, -16));
            minus.GetComponent<Button>().interactable = less != null;
            plus.GetComponent<Button>().interactable = more != null;
        }

        private RectTransform CreationRow(string name, float height)
        {
            var row = Rect(name, modalList);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        internal bool StoryOpen => storyScreen != null;

        internal void ShowStoryBeat(string speaker, string text, string image, string portrait, bool required,
            Action next, Action skip, string skipLabel = "略過", string voiceText = null, bool femaleHero = false, bool speakerOnRight = false)
        {
            CloseModal(force: true);
            var previousImage = storyScreen != null ? storyArt.texture as Texture2D : null;
            CloseStoryBeat();
            // AI voice-over keyed by the md5 of the raw (unformatted) beat text; silently absent when unvoiced.
            AudioManager.VoiceForText(voiceText ?? text, femaleHero);
            storyScreen = Panel("Prologue", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backing = storyScreen.GetComponent<Image>();
            backing.sprite = null;
            backing.type = Image.Type.Simple;
            backing.color = new Color(.08f, .15f, .16f);
            var nextImage = PortraitService.Story(image) ?? UiKit.Backdrop();
            previousStoryArt = AddImage(Rect("Previous illustration", storyScreen), previousImage ?? nextImage);
            Fill(previousStoryArt.rectTransform);
            storyArt = AddImage(Rect("Illustration", storyScreen), nextImage);
            storyArt.enabled = storyArt.texture != null;
            Fill(storyArt.rectTransform);
            bool hasPortrait = !string.IsNullOrEmpty(portrait);
            var staging = Rect("Speaker staging", storyScreen);
            Anchor(staging, Vector2.zero, Vector2.right,
                new(hasPortrait && !speakerOnRight ? 330 : 68, 66),
                new(hasPortrait && speakerOnRight ? -330 : -68, 414));
            if (hasPortrait)
            {
                var side = new Vector2(speakerOnRight ? 1 : 0, 0);
                float portraitX = speakerOnRight ? -444 : 44;
                var figure = Panel("Speaker portrait", storyScreen, side, side,
                    new(portraitX, 70), new(portraitX + 400, 650), false);
                UiKit.Skin(figure.GetComponent<Image>(), "Lacquer");
                var head = Rect("Portrait art", figure);
                Fill(head, 9);
                var texture = PortraitService.Resolve(portrait, required: required);
                var raw = AddImage(head, texture);
                FitCover(raw, texture, 382f / 562f);
            }
            staging.SetAsLastSibling();
            var dock = Panel("Dialogue", staging, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Skin(dock.GetComponent<Image>(), "Lacquer");
            var press = dock.gameObject.AddComponent<Button>();
            press.transition = Selectable.Transition.None;
            press.onClick.AddListener(AdvanceStory);

            const int left = 42;
            var nameplate = Panel("Speaker nameplate", dock, Vector2.up, Vector2.up, new(30, -54), new(402, 12), false);
            UiKit.Skin(nameplate.GetComponent<Image>(), "Plaque");
            var name = Label(nameplate, string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker, 30, TextAnchor.MiddleLeft);
            name.color = CardGold;
            Fill(name.rectTransform, 14);
            storyText = Label(dock, "", 30, TextAnchor.UpperLeft);
            storyText.color = CardText;
            storyText.font = UiKit.Font(string.IsNullOrEmpty(speaker) ? "Narration" : "Dialogue", font, text);
            name.font = UiKit.Font("Names", font, speaker);
            Anchor(storyText.rectTransform, Vector2.zero, Vector2.one, new(left, 52), new(-32, -74));
            var hint = Label(dock, "點擊或按空白鍵繼續", 20, TextAnchor.MiddleRight);
            hint.color = CardMuted;
            Anchor(hint.rectTransform, Vector2.zero, Vector2.right, new(left, 12), new(-32, 42));
            var skipButton = Button(storyScreen, skipLabel, skip);
            Anchor(skipButton, Vector2.one, Vector2.one, new(-174, -74), new(-24, -24));
            storyPages = Paginate(text, 43, 5);
            storyNext = next;
            storyPage = 0;
            storyStart = artStart = Time.unscaledTime;
            UpdateStoryBeat();
        }

        private static void AddCardIcon(RectTransform parent, string id, float x, float size)
        {
            var texture = UiKit.Icon(id);
            if (texture == null) return;
            var image = Rect("Icon", parent);
            Anchor(image, Vector2.up, Vector2.up, new(x, -8 - size), new(x + size, -8));
            AddImage(image, texture);
        }

        private static RawImage AddImage(RectTransform parent, Texture2D texture)
        {
            var art = parent.gameObject.AddComponent<RawImage>();
            art.texture = texture;
            art.raycastTarget = false;
            return art;
        }

        private static string[] Paginate(string text, int columns, int rows)
        {
            const string cannotStart = "，。！？；：、）」』》】…";
            const string cannotEnd = "（「『《【";
            var lines = new List<string>();
            foreach (var paragraph in text.Split('\n'))
            {
                int start = 0;
                while (start < paragraph.Length)
                {
                    int end = Math.Min(start + columns, paragraph.Length);
                    if (end < paragraph.Length && cannotStart.Contains(paragraph[end])) end++;
                    if (end < paragraph.Length && cannotEnd.Contains(paragraph[end - 1])) end--;
                    lines.Add(paragraph.Substring(start, end - start));
                    start = end;
                }
            }
            var pages = new List<string>();
            for (int index = 0; index < lines.Count; index += rows)
                pages.Add(string.Join("\n", lines.Skip(index).Take(rows)));
            return pages.Count == 0 ? new[] { "" } : pages.ToArray();
        }

        internal void UpdateStoryBeat()
        {
            if (storyScreen == null)
                return;
            var current = storyPages[storyPage];
            int count = Mathf.Min(current.Length, (int)((Time.unscaledTime - storyStart) * CharactersPerSecond));
            if (count != storyText.text.Length)
                storyText.text = current.Substring(0, count);
            float elapsed = Time.unscaledTime - artStart;
            storyArt.color = new Color(1, 1, 1, Mathf.Clamp01(elapsed / .28f));
            float travel = Mathf.Min(elapsed, 16);
            storyArt.rectTransform.localScale = Vector3.one * (1.015f + travel * .004f);
            storyArt.rectTransform.anchoredPosition = new Vector2(travel * 1.2f, travel * .5f);
            previousStoryArt.enabled = elapsed < .28f;
            if (Input.GetKeyDown(KeyCode.Space))
                AdvanceStory();
        }

        private void AdvanceStory()
        {
            if (storyText.text.Length < storyPages[storyPage].Length)
            {
                storyStart = Time.unscaledTime - storyPages[storyPage].Length / CharactersPerSecond - .1f;
                return;
            }
            if (storyPage + 1 < storyPages.Length)
            {
                storyPage++;
                storyStart = Time.unscaledTime;
                return;
            }
            storyNext?.Invoke();
        }

        internal void CloseStoryBeat()
        {
            if (storyScreen == null)
                return;
            AudioManager.StopVoice();
            storyScreen.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(storyScreen.gameObject);
            storyScreen = null;
        }
    }
}
