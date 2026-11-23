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

            var male = Button(row, "男・大師兄", () => chooseGender("男"));
            UiKit.Skin(male.GetComponent<Image>(), currentGender == "男" ? "Selected" : "Wood");
            Anchor(male, Vector2.right, Vector2.one, new(-226, 4), new(-116, -4));

            var female = Button(row, "女・大師姐", () => chooseGender("女"));
            UiKit.Skin(female.GetComponent<Image>(), currentGender == "女" ? "Selected" : "Wood");
            Anchor(female, Vector2.right, Vector2.one, new(-110, 4), new(0, -4));
        }

        /// <summary>
        /// Compact 6×2 portrait grid (all 12 portraits visible at once, exact 3:4 aspect ratio, never squished)
        /// decoupled from background selection so both fit on one unified screen.
        /// </summary>
        internal void CreationPortraitStrip(CharacterLook[] looks, CharacterLook selected, Action<CharacterLook> choose)
        {
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

                // Exact 3:4 portrait box (88 x 117) centered cleanly above caption
                var art = Rect("Portrait", card);
                Anchor(art, new(.5f, 1), new(.5f, 1), new(-44, -123), new(44, -6));
                var tex = LookPortrait(current);
                var raw = AddImage(art, tex);
                FitCover(raw, tex, 88f / 117f);
                if (picked) Stamp(card, "選", 26);
            }
        }

        /// <summary>Scrollable 3-column talent pane docked below pinned SkillTiles (matches CreationArts height/UX).</summary>
        internal void CreationTalentScroll(CreationOption[] options, float scrollPos, Action<CreationOption> draw)
        {
            var row = CreationRow("可選天賦捲軸", 442);
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
            var ptsLbl = Label(ptsBox, $"先輩積分：{leftPoints} ／ {totalPoints}", 24, TextAnchor.MiddleLeft);
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
}
