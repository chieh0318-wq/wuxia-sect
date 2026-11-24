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
            var row = CreationRow("可選天賦捲軸", 316); // v1126 #2: the 主修 row sits above the skill tiles
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

        internal void CreationSelectedTalents(string heading, (CreationOption option, Action remove)[] picks)
        {
            // v1126 #5 (T-3 #5): the picked talents flow as chips sized to their names and wrap onto as many rows as
            // they need; the bar grows upwards and the scrolling list above shrinks, so nothing ever reaches the
            // 上一步 / 繼續 row (which tops out at y = 72; the bar starts at 84).
            const float chipHeight = 40, gap = 8, pad = 8, titleWidth = 216;
            float rowWidth = modalCard.rect.width - 72 - titleWidth - 12 - pad * 2;
            var widths = picks.Select(pick => Mathf.Min(rowWidth, 54 + (pick.option.name.Length + (pick.remove == null ? 0 : 2)) * 21f)).ToArray();
            var placed = new List<(float x, int line)>();
            float x = 0;
            int lines = 1;
            foreach (float width in widths)
            {
                if (x > 0 && x + width > rowWidth) { x = 0; lines++; }
                placed.Add((x, lines - 1));
                x += width + gap;
            }
            float barHeight = Mathf.Max(64, lines * chipHeight + (lines - 1) * gap + pad * 2);
            var scroll = (RectTransform)modalList.parent;
            scroll.offsetMin = new Vector2(scroll.offsetMin.x, 84 + barHeight + 12);
            characterDecoration = Panel("已選天賦", modalCard, Vector2.zero, Vector2.right,
                new(36, 84), new(-36, 84 + barHeight), false);
            UiKit.Skin(characterDecoration.GetComponent<Image>(), "Lacquer");
            var title = Label(characterDecoration, heading, 22, TextAnchor.MiddleLeft);
            title.color = CardGold;
            title.resizeTextMinSize = 18;
            Anchor(title.rectTransform, Vector2.up, Vector2.up, new(20, -pad - chipHeight), new(titleWidth, -pad));
            if (picks.Length == 0)
            {
                var hint = Label(characterDecoration, "點選上方天賦加入", 18, TextAnchor.MiddleLeft);
                hint.color = CardMuted;
                Anchor(hint.rectTransform, Vector2.zero, Vector2.one, new(titleWidth + 4, 0), new(-10, 0));
                return;
            }
            var row = Rect("Selected talent icons", characterDecoration);
            Anchor(row, Vector2.zero, Vector2.one, new(titleWidth, pad), new(-12, -pad));
            for (int index = 0; index < picks.Length; index++)
            {
                var (option, remove) = picks[index];
                var button = Button(row, option.name, remove);
                Anchor(button, Vector2.up, Vector2.up, new(placed[index].x, -(placed[index].line + 1) * chipHeight - placed[index].line * gap),
                    new(placed[index].x + widths[index], -placed[index].line * (chipHeight + gap)));
                var size = button.GetComponent<LayoutElement>();
                if (size != null) size.ignoreLayout = true;
                button.GetComponent<Button>().interactable = remove != null;
                UiKit.Skin(button.GetComponent<Image>(), "Wood");
                AddCardIcon(button, UiKit.TalentIcon(option), 8, 26);
                var caption = button.GetComponentInChildren<Text>();
                caption.text = remove == null ? option.name : option.name + "　×";
                caption.resizeTextMinSize = 18;
                caption.resizeTextMaxSize = 20;
                caption.alignment = TextAnchor.MiddleLeft;
                caption.horizontalOverflow = HorizontalWrapMode.Overflow;
                Anchor(caption.rectTransform, Vector2.zero, Vector2.one, new(40, 2), new(-6, -2));
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
                Write("附近勢力・目前關係", 22, 378, 415, CardGold);
                if (data.Nodes.TryGetValue(focused.node ?? "", out var selectedNode))
                {
                    var point = data.Bounds.Uv(selectedNode);
                    var neighbors = data.Factions.Where(f => data.Nodes.ContainsKey(f.seat ?? ""))
                        .OrderBy(f => (data.Bounds.Uv(data.Nodes[f.seat]) - point).sqrMagnitude).Take(4);
                    int top = 428;
                    foreach (var faction in neighbors)
                    {
                        int score = state.Attitude(faction);
                        string relation = score >= 25 ? "友好" : score <= -25 ? "敵對" : "中立";
                        Write($"{faction.Label}　{relation} {score:+0;-0;0}", 20, top, top + 37, CardText);
                        top += 39;
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
