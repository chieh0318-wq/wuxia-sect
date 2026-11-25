using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private ScrollRect talentScroll;
        internal float TalentScrollPosition => talentScroll == null ? 1f : talentScroll.verticalNormalizedPosition;

        /// <summary>Compact top bar for merged 形貌與出身: Name input + Random button + 男/女 gender buttons.</summary>
        internal void CreationNameAndGender(string nameValue, Action<string> changed, Func<string> suggest,
            string currentGender, Action<string> chooseGender)
        {
            var row = CreationRow("姓名與性別", 56);
            var entry = Panel("姓名輸入", row, Vector2.zero, Vector2.one, new(4, 4), new(-390, -4));
            var text = Label(entry, "", 24, TextAnchor.MiddleLeft);
            Fill(text.rectTransform, 8);
            var input = entry.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.characterLimit = 12;
            input.text = nameValue;
            input.onEndEdit.AddListener(v => changed(v));

            var rand = Button(row, "隨機姓名", () => { input.text = suggest(); changed(input.text); });
            Anchor(rand, Vector2.right, Vector2.one, new(-380, 4), new(-236, -4));

            var male = Button(row, "男", () => chooseGender("男"));
            UiKit.Skin(male.GetComponent<Image>(), currentGender == "男" ? "Selected" : "Wood");
            Anchor(male, Vector2.right, Vector2.one, new(-226, 4), new(-116, -4));

            var female = Button(row, "女", () => chooseGender("女"));
            UiKit.Skin(female.GetComponent<Image>(), currentGender == "女" ? "Selected" : "Wood");
            Anchor(female, Vector2.right, Vector2.one, new(-110, 4), new(0, -4));
        }

        /// <summary>
        /// Compact 6×2 portrait grid (all 12 portraits visible at once, exact 3:4 aspect ratio, never squished)
        /// decoupled from background selection so both fit on one unified screen.
        /// </summary>
        internal void CreationPortraitStrip(CharacterLook[] looks, CharacterLook selected, string backgroundId, string title,
            Action<CharacterLook> choose)
        {
            characterDecoration = Rect("形貌與出身捲軸", modalCard);
            Fill(characterDecoration);
            AttachScrollbar((RectTransform)modalList.parent, characterDecoration, Vector2.right,
                new(-422, 88), new(-410, -112));
            const int cols = 6;
            int rows = Mathf.Max(1, (looks.Length + cols - 1) / cols);
            const float cellH = 166f;
            var row = CreationRow("肖像一覽", rows * cellH + 34);
            var hdr = Label(row, $"選擇肖像（僅影響外觀，共 {looks.Length} 款）　—　當前：{selected?.label ?? ""}", 20, TextAnchor.MiddleLeft);
            hdr.color = Ink;
            hdr.fontStyle = FontStyle.Bold;
            Anchor(hdr.rectTransform, Vector2.up, Vector2.one, new(4, -30), new(-4, 0));

            var gridObj = Rect("肖像網格", row);
            Anchor(gridObj, Vector2.zero, Vector2.one, new(0, 0), new(0, -34));
            var grid = gridObj.gameObject.AddComponent<GridLayoutGroup>();
            float gap = 8f;
            grid.cellSize = new Vector2((creationListWidth - (cols - 1) * gap) / cols, cellH - gap);
            grid.spacing = new Vector2(gap, gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;

            foreach (var look in looks)
            {
                var current = look;
                bool picked = current == selected;
                var card = Button(gridObj, current.label, () => choose(current));
                UiKit.Skin(card.GetComponent<Image>(), picked ? "Selected" : "Wood");
                var caption = card.GetComponentInChildren<Text>();
                caption.fontSize = caption.resizeTextMaxSize = 18;
                caption.color = picked ? UiKit.HeaderGold : CardText;
                Anchor(caption.rectTransform, Vector2.zero, Vector2.right, new(2, 2), new(-2, 30));

                var art = Rect("Standee preview", card);
                Anchor(art, new(.5f, 1), new(.5f, 1), new(-52, -123), new(52, -6));
                var tex = CharacterFactory.HeroStandee(current.gender, current.id, backgroundId, title);
                var figure = Rect("Full-body standee", art);
                AddImage(figure, tex);
                var fit = figure.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = tex.width / (float)tex.height;
                if (picked) Stamp(card, "選", 26);
            }
        }

        /// <summary>Scrollable 3-column talent pane docked below pinned SkillTiles (matches CreationArts height/UX).</summary>
        internal void CreationTalentScroll(CreationOption[] options, float scrollPos, Action<CreationOption> draw)
        {
            // v1126 #2: the 主修 row sits above the skill tiles. v1126c PF: with the picks moved into the card's
            // drop-down, the list takes whatever height the page has left below the rows above it (at least 316).
            float used = 8;
            foreach (Transform child in modalList)
                if (child.gameObject.activeSelf && child.GetComponent<LayoutElement>() is { } above)
                    used += above.preferredHeight + 9;
            float room = ((RectTransform)modalList.parent).rect.height - used - 2;
            var row = CreationRow("可選天賦捲軸", Mathf.Max(316, room));
            var pane = Panel("天賦列表框", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var content = ScrollList(pane, new(12, 12), new(-28, -12));
            var scroll = content.parent.GetComponent<ScrollRect>();
            talentScroll = scroll;

            if (options.Length == 0)
            {
                AddLine(content, "此分類暫無天賦。", 22);
            }
            else
            {
                const int cols = 3;
                const float gap = 10f;
                const float cellH = 152f;
                int rows = (options.Length + cols - 1) / cols;
                var gridObj = Rect("天賦網格", content);
                gridObj.gameObject.AddComponent<LayoutElement>().preferredHeight = rows * cellH + Math.Max(0, rows - 1) * gap;
                var grid = gridObj.gameObject.AddComponent<GridLayoutGroup>();
                float availW = creationListWidth - 44f;
                grid.cellSize = new Vector2((availW - (cols - 1) * gap) / cols, cellH);
                grid.spacing = new Vector2(gap, gap);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = cols;
                // v1125 #1: columns follow the real scroll width (the docked summary narrows it), so the third
                // column is never clipped by the viewport mask.
                gridObj.gameObject.AddComponent<FitGridColumns>().Estimate = grid.cellSize.x;
                creationGrid = gridObj;
                foreach (var option in options)
                    draw(option);
                creationGrid = null;
            }

            AttachScrollbar((RectTransform)content.parent, pane, Vector2.right, new(-20, 12), new(-8, -12));
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scrollPos);
        }

        /// <summary>
        /// M9 Legacy Points (先輩遺澤 / 選擇資源) screen: categorized icon tiles on the left,
        /// live point balance + item details + carried list on the right.
        /// </summary>
        internal void CreationLegacyShop(int totalPoints, int leftPoints, Dictionary<string, int> counts,
            LegacyItemDef focused, Action<LegacyItemDef, int> adjust, Action<LegacyItemDef> focus)
        {
            const int sideW = 372;
            creationListWidth = modalCard.rect.width - 72 - sideW - 20;
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one,
                new(36, 88), new(-36 - sideW - 20, -112));

            // Right sidebar: Legacy Points & Selected Item Details
            creationSummary = Panel("積分總覽", modalCard, Vector2.right, Vector2.one,
                new(-36 - sideW, 88), new(-36, -112));
            UiKit.Skin(creationSummary.GetComponent<Image>(), "Lacquer");
            var side = creationSummary;

            var ptsBox = Panel("積分框", side, Vector2.up, Vector2.one, new(16, -82), new(-16, -16), false);
            UiKit.Skin(ptsBox.GetComponent<Image>(), "Jade");
            var coinTex = UiKit.Icon("hud_silver");
            if (coinTex != null)
            {
                var c = Rect("CoinIcon", ptsBox);
                Anchor(c, new(0, .5f), new(0, .5f), new(14, -20), new(54, 20));
                AddImage(c, coinTex);
            }
            var ptsLbl = Label(ptsBox, $"剩餘 {leftPoints}／共 {totalPoints}", 24, TextAnchor.MiddleLeft);
            ptsLbl.color = UiKit.HeaderGold;
            ptsLbl.fontStyle = FontStyle.Bold;
            Anchor(ptsLbl.rectTransform, Vector2.zero, Vector2.one, new(64, 0), new(-12, 0));

            var sub = Label(side, "來自先輩的遺澤，挑選對應裝備、丹藥或秘笈後即可開局帶入遊戲。通關秘境與攻討據點可累積更多永久積分。", 18, TextAnchor.UpperLeft);
            sub.color = CardMuted;
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            sub.resizeTextForBestFit = false;
            Anchor(sub.rectTransform, Vector2.up, Vector2.one, new(18, -168), new(-18, -92));

            // Focused item detail box
            var detailBox = Panel("道具詳情", side, Vector2.up, Vector2.one, new(16, -420), new(-16, -176), false);
            UiKit.Skin(detailBox.GetComponent<Image>(), "Wood");
            if (focused != null)
            {
                var icFrame = Rect("FocusIcon", detailBox);
                Anchor(icFrame, Vector2.up, Vector2.up, new(16, -98), new(98, -16));
                var icTex = UiKit.Icon(focused.icon);
                if (icTex != null) AddImage(icFrame, icTex);
                var fn = Label(detailBox, focused.name, 25, TextAnchor.MiddleLeft);
                fn.color = UiKit.HeaderGold;
                fn.fontStyle = FontStyle.Bold;
                Anchor(fn.rectTransform, Vector2.up, Vector2.one, new(110, -52), new(-12, -14));
                var fc = Label(detailBox, $"消耗 {focused.cost} 積分　上限 {focused.maxQty}", 18, TextAnchor.MiddleLeft);
                fc.color = CardGold;
                Anchor(fc.rectTransform, Vector2.up, Vector2.one, new(110, -82), new(-12, -52));
                var fs = Label(detailBox, focused.statsText, 19, TextAnchor.MiddleLeft);
                fs.color = new Color(.64f, .92f, .72f);
                Anchor(fs.rectTransform, Vector2.up, Vector2.one, new(16, -138), new(-16, -104));
                var fd = Label(detailBox, focused.desc, 18, TextAnchor.UpperLeft);
                fd.color = CardText;
                fd.horizontalOverflow = HorizontalWrapMode.Wrap;
                fd.resizeTextForBestFit = false;
                Anchor(fd.rectTransform, Vector2.zero, Vector2.one, new(16, 12), new(-16, -142));
            }

            // Carried summary
            var carriedHdr = Label(side, "開局攜帶清單", 21, TextAnchor.MiddleLeft);
            carriedHdr.color = CardGold;
            Anchor(carriedHdr.rectTransform, Vector2.up, Vector2.one, new(18, -460), new(-18, -430));
            var carriedLines = counts.Where(kv => kv.Value > 0)
                .Select(kv =>
                {
                    var d = LegacySystem.Catalog.FirstOrDefault(x => x.id == kv.Key);
                    return d == null ? "" : $"• {d.name} ×{kv.Value}（{d.cost * kv.Value} 積分）";
                }).Where(s => s.Length > 0).ToArray();
            var carriedTxt = Label(side, carriedLines.Length == 0 ? "尚未挑選任何遺澤物資。" : string.Join("\n", carriedLines), 18, TextAnchor.UpperLeft);
            carriedTxt.color = carriedLines.Length == 0 ? CardMuted : CardText;
            carriedTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            carriedTxt.resizeTextForBestFit = false;
            Anchor(carriedTxt.rectTransform, Vector2.zero, Vector2.one, new(18, 16), new(-18, -466));

            // Left area: Categorized icon grid
            string[] categories = { "武器裝備", "丹藥糧食", "武學秘笈" };
            foreach (var cat in categories)
            {
                var items = LegacySystem.Catalog.Where(x => x.category == cat).ToArray();
                var header = CreationRow("分類_" + cat, 36);
                var hl = Label(header, $"◆ {cat}", 23, TextAnchor.MiddleLeft);
                hl.color = Ink;
                hl.fontStyle = FontStyle.Bold;
                Anchor(hl.rectTransform, Vector2.zero, Vector2.one, new(6, 0), new(-6, 0));

                const int cols = 6;
                float gap = 10f;
                int rows = (items.Length + cols - 1) / cols;
                float cellH = 162f;
                var gridRow = CreationRow("物品_" + cat, rows * cellH + Math.Max(0, rows - 1) * gap);
                var grid = gridRow.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2((creationListWidth - (cols - 1) * gap) / cols, cellH);
                grid.spacing = new Vector2(gap, gap);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = cols;

                foreach (var item in items)
                {
                    var def = item;
                    int qty = counts.TryGetValue(def.id, out var q) ? q : 0;
                    bool canAdd = qty < def.maxQty && leftPoints >= def.cost;
                    var tile = Button(gridRow, "", () =>
                    {
                        focus(def);
                        if (canAdd) adjust(def, 1);
                        else if (qty > 0) adjust(def, -qty);
                    });
                    tile.GetComponentInChildren<Text>().text = "";
                    UiKit.Skin(tile.GetComponent<Image>(), qty > 0 ? "Selected" : "Wood");
                    if (def == focused)
                    {
                        var outline = tile.gameObject.AddComponent<Outline>();
                        outline.effectColor = UiKit.HeaderGold;
                        outline.effectDistance = new Vector2(2, 2);
                    }

                    var icRect = Rect("Icon", tile);
                    Anchor(icRect, new(.5f, 1), new(.5f, 1), new(-42, -92), new(42, -8));
                    var icTex = UiKit.Icon(def.icon);
                    if (icTex != null) AddImage(icRect, icTex);

                    var nm = Label(tile, def.name, 18, TextAnchor.MiddleCenter);
                    nm.color = qty > 0 ? UiKit.HeaderGold : CardText;
                    Anchor(nm.rectTransform, Vector2.zero, Vector2.right, new(4, 34), new(-4, 64));

                    var costBar = Panel("價格", tile, Vector2.zero, Vector2.right, new(8, 6), new(-8, 32), false);
                    costBar.GetComponent<Image>().color = new Color(.08f, .06f, .05f, .78f);
                    var costLbl = Label(costBar, $"{def.cost} 積分", 18, TextAnchor.MiddleCenter);
                    costLbl.color = canAdd || qty > 0 ? CardGold : CardLow;
                    Fill(costLbl.rectTransform);

                    if (qty > 0)
                    {
                        var qtyBadge = Panel("數量", tile, Vector2.one, Vector2.one, new(-44, -34), new(-6, -6), false);
                        UiKit.Skin(qtyBadge.GetComponent<Image>(), "Seal");
                        var ql = Label(qtyBadge, $"×{qty}", 18, TextAnchor.MiddleCenter);
                        ql.color = UiKit.HeaderGold;
                        Fill(ql.rectTransform);
                    }
                }
            }
        }

        private const int CreationCardWidth = 372;
        private const float CardContentHeight = 640, PicksHeaderHeight = 44, PicksFooter = 12 + PicksHeaderHeight + 12;
        private Action<bool> creationPicksSet;

        /// <summary>
        /// v1126c PF: 繼續 leaves the shared action row and sits on a fixed anchor directly under the right-hand card
        /// (same x-range as the 372-wide card, y 22–72 of the page), so its place and size never depend on any text;
        /// the other actions share the row to its left.
        /// </summary>
        private void PinContinueButton()
        {
            var row = modalButtons.GetComponent<HorizontalLayoutGroup>();
            foreach (Transform action in modalButtons)
            {
                if (!action.gameObject.activeSelf || action.name != "繼續") continue;
                var size = action.GetComponent<LayoutElement>() ?? action.gameObject.AddComponent<LayoutElement>();
                size.ignoreLayout = true;
                // modalButtons spans x 30…W-30 of the page; the card spans W-408…W-36.
                Anchor((RectTransform)action, Vector2.right, Vector2.one, new(-(CreationCardWidth + 6), 0), new(-6, 0));
                if (row != null) row.padding = new RectOffset(0, CreationCardWidth + 6 + 16, 0, 0);
            }
        }

        /// <summary>Capture / test hook: opens or closes the creation card's 已選 drop-down. False when there is none.</summary>
        internal bool SetCreationPicksOpen(bool open)
        {
            if (creationPicksSet == null || creationSummary == null) return false;
            creationPicksSet(open);
            return true;
        }

        /// <summary>
        /// v1126c PF: the picks on the creation card are a drop-down instead of wrapping text. Collapsed it is one
        /// fixed-height header pinned to the card's bottom (「已選天賦（N）▼」); clicking it opens a fixed-height,
        /// scrolling list above the header, inside the card, one pick per row (name + short effect, ellipsized to the
        /// row). Clicking the header again, clicking anywhere outside, right-click or Esc (UiBack) closes it. Nothing
        /// in here can reach the 繼續 button, which sits below the card.
        /// </summary>
        private void CreationPicksDropdown(RectTransform card, CreationSheet sheet)
        {
            creationPicksSet = null;
            string heading = string.IsNullOrEmpty(sheet.picksHeading) ? "已選" : sheet.picksHeading;
            var rows = new List<(string name, string detail, string icon, Action remove)>(sheet.pickRows);
            if (rows.Count == 0)
                foreach (string pick in sheet.picks) rows.Add((pick, "", null, null));
            int count = rows.Count;
            float cardWidth = card.rect.width > 1 ? card.rect.width : CreationCardWidth;
            const float side = 10, bottom = 12;

            // Invisible catcher for clicks outside the open list (drawn under the list and the header).
            var blocker = Rect("已選收合遮罩", card);
            Anchor(blocker, Vector2.zero, Vector2.one, new(-8000, -8000), new(8000, 8000));
            blocker.gameObject.AddComponent<Image>().color = Color.clear;

            float panelBottom = bottom + PicksHeaderHeight + 4;
            float panelHeight = Mathf.Min(340, Mathf.Max(120, card.rect.height - panelBottom - 16));
            var panel = Panel("已選清單", card, Vector2.zero, Vector2.right,
                new(side, panelBottom), new(-side, panelBottom + panelHeight));
            UiKit.Skin(panel.GetComponent<Image>(), "Lacquer");
            var list = ScrollList(panel, new(10, 10), new(-26, -10));
            AttachScrollbar((RectTransform)list.parent, panel, Vector2.right, new(-20, 10), new(-10, -10));
            float rowWidth = cardWidth - side * 2 - 10 - 26;
            const float rowHeight = 58;
            if (count == 0)
            {
                var none = Label(list, "尚未選擇", 18, TextAnchor.MiddleLeft);
                none.color = CardMuted;
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            }
            foreach (var (name, detail, icon, remove) in rows)
            {
                var item = Rect("已選_" + name, list);
                item.gameObject.AddComponent<LayoutElement>().preferredHeight = rowHeight;
                var shade = item.gameObject.AddComponent<Image>();
                shade.color = new Color(0, 0, 0, .22f);
                shade.raycastTarget = false;
                float left = 8;
                var iconTexture = string.IsNullOrEmpty(icon) ? null : UiKit.Icon(icon);
                if (iconTexture != null)
                {
                    var art = Rect("Icon", item);
                    Anchor(art, new(0, .5f), new(0, .5f), new(8, -17), new(42, 17));
                    AddImage(art, iconTexture);
                    left = 50;
                }
                float right = remove != null ? 46 : 8;
                bool hasDetail = !string.IsNullOrWhiteSpace(detail);
                var title = Label(item, name, 20, TextAnchor.MiddleLeft);
                title.color = CardGold;
                title.fontStyle = FontStyle.Normal;
                Anchor(title.rectTransform, hasDetail ? Vector2.up : Vector2.zero, Vector2.one,
                    new(left, hasDetail ? -31 : 0), new(-right, hasDetail ? -2 : 0));
                OneLine(title, name, rowWidth - left - right, 20);
                if (hasDetail)
                {
                    var effect = Label(item, detail, 18, TextAnchor.MiddleLeft);
                    effect.color = CardMuted;
                    Anchor(effect.rectTransform, Vector2.zero, Vector2.right, new(left, 3), new(-right, 29));
                    OneLine(effect, detail, rowWidth - left - right, 18);
                }
                if (remove != null)
                {
                    var drop = Button(item, "×", remove);
                    drop.name = "移除_" + name;
                    Anchor(drop, new(1, .5f), new(1, .5f), new(-42, -18), new(-6, 18));
                    UiKit.Skin(drop.GetComponent<Image>(), "Wood");
                }
            }

            var header = Button(card, "", null);
            header.name = "已選收合";
            Anchor(header, Vector2.zero, Vector2.right, new(side, bottom), new(-side, bottom + PicksHeaderHeight));
            UiKit.Skin(header.GetComponent<Image>(), "Lacquer");
            var caption = header.GetComponentInChildren<Text>();
            caption.alignment = TextAnchor.MiddleLeft;
            caption.color = CardGold;
            caption.fontStyle = FontStyle.Normal;
            Anchor(caption.rectTransform, Vector2.zero, Vector2.one, new(14, 0), new(-40, 0));
            OneLine(caption, $"{heading}（{count}）", cardWidth - side * 2 - 14 - 40, 21);
            var arrow = Label(header, "▼▲", 20, TextAnchor.MiddleCenter);
            arrow.color = CardGold;
            arrow.resizeTextForBestFit = false;
            Anchor(arrow.rectTransform, Vector2.right, Vector2.one, new(-38, 0), new(-8, 0));
            var toggle = header.GetComponent<Button>();
            toggle.interactable = count > 0;

            bool open = false;
            void Set(bool value)
            {
                if (panel == null) return;
                open = value && count > 0;
                panel.gameObject.SetActive(open);
                blocker.gameObject.SetActive(open);
                arrow.text = count == 0 ? "" : open ? "▲" : "▼";
                if (open) UiBack.Push(() => Set(false), () => panel != null && panel.gameObject.activeInHierarchy, panel);
                else UiBack.Pop(panel);
            }
            toggle.onClick.AddListener(() => Set(!open));
            var outside = blocker.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(() => Set(false));
            blocker.SetAsLastSibling();
            panel.SetAsLastSibling();
            header.SetAsLastSibling();
            Set(false);
            creationPicksSet = Set;
        }

        /// <summary>
        /// v1126c PF capture check, in page units: the 已選 header is one line of fixed height, the open list, the
        /// card and its content stay clear of 繼續, the other actions do not touch it, and its caption fits.
        /// </summary>
        internal bool LogCreationLayout(string tag)
        {
            Canvas.ForceUpdateCanvases();
            var problems = new List<string>();
            Transform go = null;
            foreach (Transform action in modalButtons)
                if (action.gameObject.activeSelf && action.name == "繼續") go = action;
            UnityEngine.Rect Box(Transform t)
            {
                var corners = new Vector3[4];
                ((RectTransform)t).GetWorldCorners(corners);
                Vector2 a = modalCard.InverseTransformPoint(corners[0]), b = modalCard.InverseTransformPoint(corners[2]);
                return UnityEngine.Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            string Fmt(UnityEngine.Rect r) => $"[{r.xMin:0},{r.yMin:0}–{r.xMax:0},{r.yMax:0}]";
            if (go == null) problems.Add("no 繼續");
            var goBox = go == null ? default : Box(go);
            if (go != null)
            {
                var caption = go.GetComponentInChildren<Text>();
                if (caption.preferredWidth > caption.rectTransform.rect.width + 1 || caption.cachedTextGenerator.lineCount > 1)
                    problems.Add("繼續 caption does not fit");
                foreach (Transform action in modalButtons)
                    if (action != go && action.gameObject.activeSelf && Box(action).Overlaps(goBox))
                        problems.Add($"{action.name} overlaps 繼續");
            }
            string detail = "";
            if (creationSummary != null)
            {
                var card = Box(creationSummary);
                if (go != null && card.Overlaps(goBox)) problems.Add("card overlaps 繼續");
                var header = creationSummary.Find("已選收合");
                var content = creationSummary.Find("人物卡內容");
                var panel = creationSummary.Find("已選清單");
                if (header != null)
                {
                    var h = Box(header);
                    var text = header.GetComponentInChildren<Text>();
                    if (h.height > PicksHeaderHeight + 1) problems.Add("header taller than one line");
                    if (text.cachedTextGenerator.lineCount > 1 || text.preferredWidth > text.rectTransform.rect.width + 1)
                        problems.Add("header caption overflows");
                    if (content != null && Box(content).yMin < h.yMax - .5f) problems.Add("card content runs into the header");
                    if (go != null && h.Overlaps(goBox)) problems.Add("header overlaps 繼續");
                    detail += $" header={Fmt(h)} '{text.text}'";
                }
                if (panel != null && panel.gameObject.activeSelf)
                {
                    var p = Box(panel);
                    if (go != null && p.Overlaps(goBox)) problems.Add("open list overlaps 繼續");
                    if (p.xMin < card.xMin - .5f || p.xMax > card.xMax + .5f || p.yMax > card.yMax + .5f) problems.Add("open list leaves the card");
                    foreach (var label in panel.GetComponentsInChildren<Text>())
                        if (label.horizontalOverflow == HorizontalWrapMode.Overflow &&
                            label.preferredWidth > label.rectTransform.rect.width + 1)
                            problems.Add($"row text overflows: {label.text}");
                    foreach (var label in panel.GetComponentsInChildren<Text>())
                        if (label.text.Length > 0 && (label.cachedTextGenerator.characterCountVisible == 0 ||
                            label.cachedTextGenerator.lineCount > 1))
                            problems.Add($"row text hidden or wrapped: {label.text}");
                    detail += $" list={Fmt(p)}";
                }
                if (content != null) detail += $" content={Fmt(Box(content))} scale={content.localScale.x:0.00}";
                detail += $" card={Fmt(card)}";
            }
            bool ok = problems.Count == 0;
            string line = $"[Capture] PF layout {tag}: ok={ok} 繼續={(go == null ? "-" : Fmt(goBox))}{detail}" +
                (ok ? "" : " problems=" + string.Join("; ", problems));
            if (ok) Debug.Log(line);
            else Debug.LogError(line);
            return ok;
        }

        /// <summary>Single-line text that never leaves its box: shrinks to the 18px floor, then ends in 「…」.</summary>
        private void OneLine(Text label, string value, float width, int size)
        {
            value = (value ?? "").Replace("\n", " ").Replace("\r", "");
            label.font = UiKit.Font("Interface", font, value);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            // No wrapping, so it is always one line; Truncate would hide that line whenever the font's line height
            // is a hair taller than the box.
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontSize = UiKit.Readable(size);
            if (Measure(value) > width && label.fontSize > UiKit.MinBody)
                label.fontSize = UiKit.MinBody;
            if (Measure(value) > width)
            {
                int keep = value.Length;
                while (keep > 0 && Measure(value.Substring(0, keep) + "…") > width) keep--;
                value = value.Substring(0, keep).TrimEnd('・', '，', ' ') + "…";
            }
            label.text = value;

            float Measure(string text)
            {
                var settings = label.GetGenerationSettings(new Vector2(width, label.fontSize * 2f));
                settings.horizontalOverflow = HorizontalWrapMode.Overflow;
                settings.resizeTextForBestFit = false;
                return label.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / label.pixelsPerUnit;
            }
        }

        internal void ShowSettlementMap(GameState state, StoryLocation[] sites, string prompt,
            Action<StoryLocation> confirm, Action skip, bool previewFirst = false)
        {
            var data = WorldMapData.Load();
            StoryLocation focused = previewFirst ? sites.FirstOrDefault() : null;
            Draw();

            void Draw()
            {
                ShowCreation("立足之地", new[] { prompt },
                    focused == null ? null : new[] { ("確認立足", (Action)(() => confirm(focused))) }, skip);
                var row = CreationRow("山門選址", 656);
                var map = Rect("候選地圖", row);
                Anchor(map, Vector2.zero, new(.69f, 1), Vector2.zero, new(-12, 0));
                var paint = AddImage(map, Resources.Load<Texture2D>("WorldMap/world_map_san10") ?? M2RuntimeAssets.Load()?.map);
                paint.color = paint.texture == null ? MapUi.Paper : Color.white;
                foreach (var site in sites)
                {
                    if (!data.Nodes.TryGetValue(site.node ?? "", out var node)) continue;
                    var point = data.Bounds.Uv(node);
                    var marker = Button(map, site.name, () => { focused = site; Draw(); });
                    var label = marker.GetComponentInChildren<Text>();
                    label.resizeTextMaxSize = 20;
                    Anchor(marker, point, point, new(-60, -20), new(60, 20));
                    UiKit.Skin(marker.GetComponent<Image>(), site == focused ? "Selected" : "Plaque");
                    marker.SetAsLastSibling();
                }
                SettlementPowers(map, data, state, sites, focused); // v1126 #10

                var detail = Panel("選址詳情", row, new(.69f, 0), Vector2.one, Vector2.zero, Vector2.zero, false);
                UiKit.Skin(detail.GetComponent<Image>(), "Wood");
                if (focused == null)
                {
                    Write("點選地圖上的地點，查看地勢、資源與附近勢力。", 24, 22, 150, CardText);
                    return;
                }
                Write($"{focused.name}　{focused.region}", 28, 16, 60, CardGold);
                Write("物產　" + focused.resources, 20, 62, 102, CardText);
                Write(focused.bonusText, 21, 110, 245, CardGold);
                Write(focused.desc, 20, 246, 374, CardMuted);
                Write("附近勢力・關係・距離", 22, 378, 415, CardGold);
                {
                    // v1126 #10: relation colour, kind, strength and road distance for the four nearest powers.
                    int top = 424;
                    foreach (var (faction, hops, days) in NearbyPowers(data, focused.node, 4))
                    {
                        string stance = faction.StanceWith(state.RelationShift(faction.id));
                        string distance = hops == 0 ? "同地" : hops >= 99 ? "無路" : $"{hops} 格・{days} 日";
                        Write($"{faction.Label}　{faction.RelationWith(state.RelationShift(faction.id))}　{distance}", 20, top, top + 28,
                            RelationColor(stance));
                        Write($"{WorldMapScreen.KindName(faction)}・人馬約 {faction.disciples:N0}・聲望 {faction.reputation}", 18, top + 27, top + 52, CardMuted);
                        top += 56;
                    }
                }

                void Write(string value, int size, int top, int bottom, Color color)
                {
                    var label = Label(detail, value ?? "", size, TextAnchor.UpperLeft);
                    label.color = color;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    Anchor(label.rectTransform, Vector2.up, Vector2.one, new(18, -bottom), new(-18, -top));
                }
            }
        }

        private static void FitCover(RawImage raw, Texture2D tex, float targetAspect)
        {
            if (raw == null || tex == null || tex.height <= 0) return;
            float srcAspect = tex.width / (float)tex.height;
            if (srcAspect > targetAspect)
            {
                float w = targetAspect / srcAspect;
                raw.uvRect = new Rect((1f - w) * .5f, 0f, w, 1f);
            }
            else
            {
                float h = srcAspect / targetAspect;
                raw.uvRect = new Rect(0f, (1f - h) * .72f, 1f, h);
            }
        }
    }

    /// <summary>v1125 #1: keeps a fixed-column GridLayoutGroup's cells exactly as wide as the grid's own rect.</summary>
    internal sealed class FitGridColumns : MonoBehaviour
    {
        private GridLayoutGroup grid;
        private float width = -1;

        /// <summary>Cell width the grid was laid out with before the layout pass (cards size their text to it).</summary>
        internal float Estimate;

        private void OnRectTransformDimensionsChange() => Fit();
        private void LateUpdate() => Fit();

        private void Fit()
        {
            grid ??= GetComponent<GridLayoutGroup>();
            var rect = (RectTransform)transform;
            // Ignore the default 100 px rect a fresh object has before its layout group sizes it.
            if (grid == null || rect.rect.width < 300 || Mathf.Abs(rect.rect.width - width) < .5f) return;
            width = rect.rect.width;
            int columns = Mathf.Max(1, grid.constraintCount);
            float cell = (width - grid.padding.horizontal - (columns - 1) * grid.spacing.x) / columns;
            if (Mathf.Abs(cell - grid.cellSize.x) > .5f) grid.cellSize = new Vector2(cell, grid.cellSize.y);
        }
    }
}
