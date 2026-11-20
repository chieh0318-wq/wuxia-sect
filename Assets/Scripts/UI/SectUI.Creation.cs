using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private RectTransform storyScreen, creationGrid;
        private RectTransform creationBackdrop, creationPlaque, creationSummary;
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
            if (skip != null) buttons.Add(("略過", skip));
            ShowModal("", lines.Where(line => !string.IsNullOrWhiteSpace(line)), buttons.ToArray(), dismissible: false);
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
            var title = Label(creationPlaque, heading, 35, TextAnchor.MiddleCenter);
            title.color = new Color(.98f, .89f, .72f);
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
            foreach (var decoration in new[] { creationBackdrop, creationPlaque, creationSummary })
            {
                if (decoration == null) continue;
                decoration.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(decoration.gameObject);
            }
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one, new(26, 82), new(-26, -22));
        }

        internal void CreationSummary(IEnumerable<string> lines)
        {
            const int width = 344;
            creationListWidth -= width + 20;
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one,
                new(36, 88), new(-36 - width - 20, -112));
            creationSummary = Panel("天賦總覽", modalCard, Vector2.right, Vector2.one,
                new(-36 - width, 88), new(-36, -112));
            var ledger = ScrollList(creationSummary, new(22, 16), new(-22, -16));
            AddLine(ledger, "天賦總覽", 28);
            foreach (string line in lines)
                AddLine(ledger, line, 21);
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
            var row = CreationRow("可習技藝", 285);
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
            string rarity = null, bool picked = false, int? cost = null, bool banner = false)
        {
            var item = Button(creationGrid == null ? modalList : creationGrid, label, run);
            item.GetComponent<Button>().interactable = run != null;
            var layout = item.GetComponent<LayoutElement>();
            var caption = item.GetComponentInChildren<Text>();
            var art = string.IsNullOrEmpty(image) ? null : PortraitService.Story(image);
            layout.preferredHeight = art == null ? 124 : 164;
            var frame = item.GetComponent<Image>();
            UiKit.Skin(frame, picked ? "Selected" : "Wood");
            frame.color = rarity switch
            {
                "legendary" => new Color(1, .78f, .59f),
                "rare" => new Color(1, .87f, .64f),
                _ => new Color(.93f, .92f, .87f)
            };
            bool hasBanner = art != null && banner;
            int left = art == null || hasBanner ? 16 : creationGrid == null ? 200 : 196;
            int top = hasBanner ? 198 : 0;
            caption.alignment = TextAnchor.MiddleLeft;
            caption.resizeTextMinSize = 20;
            Anchor(caption.rectTransform, Vector2.up, Vector2.one, new(left, -44 - top),
                new(cost.HasValue ? -120 : -16, -6 - top));
            if (cost.HasValue)
            {
                var badge = Label(item, $"{cost} 點", 20, TextAnchor.MiddleCenter);
                badge.color = new Color(1, .84f, .55f);
                Anchor(badge.rectTransform, Vector2.one, Vector2.one, new(-112, -43), new(-52, -7));
            }
            if (picked)
            {
                Stamp(item, "選", 36);
                var glow = item.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(.89f, .66f, .31f, .75f);
                glow.effectDistance = new Vector2(2, 2);
            }
            var body = Label(item, description ?? "", 20);
            body.color = new Color(.96f, .89f, .75f);
            body.resizeTextForBestFit = false;
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new(left, 8), new(-16, -46 - top));
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

        internal void CreationPortraits(CharacterLook[] looks, CharacterLook selected, int page,
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
            var indicator = Label(grid, $"第 {page + 1}／{pages} 頁", 22, TextAnchor.MiddleCenter);
            Anchor(indicator.rectTransform, new(.33f, 1), new(.67f, 1), new(0, -39), Vector2.zero);
            PageButton("‹", -1, .06f);
            PageButton("›", 1, .76f);

            var sheet = Panel("形貌預覽", row, new(.55f, 0), Vector2.one,
                new(8, 22), new(-5, 0));
            var portrait = Rect("選定肖像", sheet);
            Anchor(portrait, Vector2.zero, Vector2.up, new(18, 88), new(273, -50));
            AddImage(portrait, LookPortrait(selected));
            var preview = Rect("立體預覽", sheet);
            Anchor(preview, new(.48f, 0), Vector2.one, new(12, 74), new(-8, -38));
            preview.gameObject.AddComponent<CharacterPreview>().Initialize(selected, AddImage(preview, null));
            var title = Label(sheet, selected.label, 27, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, Vector2.up, new(.48f, 1), new(12, -48), new(-8, -5));
            var detail = Label(sheet, selected.desc ?? "", 21, TextAnchor.MiddleLeft);
            Anchor(detail.rectTransform, Vector2.zero, Vector2.right, new(18, 8), new(-18, 77));

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

        internal void CreationAttribute(string name, int current, int maximum, Action less, Action more)
        {
            var row = CreationRow(name, 87);
            var meter = Panel(name, row, new(.15f, 0), new(.85f, 1), Vector2.zero, Vector2.zero);
            var caption = Label(meter, $"{name}　{current}", 26, TextAnchor.MiddleLeft);
            Anchor(caption.rectTransform, Vector2.zero, Vector2.up, new(23, 8), new(178, -8));
            var track = Rect("Attribute track", meter);
            Anchor(track, new(.18f, .5f), new(.80f, .5f), new(0, -6), new(-10, 6));
            track.gameObject.AddComponent<Image>().color = new Color(.36f, .27f, .18f, .24f);
            var fill = Rect("Attribute value", track);
            Anchor(fill, Vector2.zero, new(Mathf.Clamp01(current / (float)Mathf.Max(1, maximum)), 1),
                Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = new Color(.26f, .44f, .36f);
            var minus = Button(meter, "−", less);
            var plus = Button(meter, "＋", more);
            Anchor(minus, Vector2.right, Vector2.one, new(-170, 14), new(-102, -14));
            Anchor(plus, Vector2.right, Vector2.one, new(-90, 14), new(-22, -14));
            minus.GetComponent<Button>().interactable = less != null;
            plus.GetComponent<Button>().interactable = more != null;
        }

        private RectTransform CreationRow(string name, float height)
        {
            var row = Rect(name, modalList);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        internal void ShowStoryBeat(string speaker, string text, string image, string portrait, bool required,
            Action next, Action skip)
        {
            CloseModal(force: true);
            var previousImage = storyScreen != null ? storyArt.texture as Texture2D : null;
            CloseStoryBeat();
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
            var dock = Panel("Dialogue", storyScreen, new(.5f, 0), new(.5f, 0), new(-680, 60), new(680, 314));
            var press = dock.gameObject.AddComponent<Button>();
            press.transition = Selectable.Transition.None;
            press.onClick.AddListener(AdvanceStory);

            bool hasPortrait = !string.IsNullOrEmpty(portrait);
            if (hasPortrait)
            {
                var head = Rect("Speaker portrait", dock);
                Anchor(head, Vector2.zero, Vector2.up, new(18, 20), new(186, -20));
                var raw = AddImage(head, PortraitService.Resolve(portrait, required: required));
                raw.enabled = raw.texture != null;
            }
            int left = hasPortrait ? 206 : 32;
            var name = Label(dock, speaker, 30, TextAnchor.MiddleLeft);
            Anchor(name.rectTransform, Vector2.up, Vector2.one, new(left, -66), new(-32, -14));
            storyText = Label(dock, "", 30);
            storyText.font = UiKit.Font(string.IsNullOrEmpty(speaker) ? "Narration" : "Dialogue", font, text);
            name.font = UiKit.Font("Names", font, speaker);
            Anchor(storyText.rectTransform, Vector2.zero, Vector2.one, new(left, 52), new(-32, -74));
            var hint = Label(dock, "點擊或按空白鍵繼續", 20, TextAnchor.MiddleRight);
            Anchor(hint.rectTransform, Vector2.zero, Vector2.right, new(left, 12), new(-32, 42));
            var skipButton = Button(storyScreen, "略過", skip);
            Anchor(skipButton, Vector2.one, Vector2.one, new(-174, -74), new(-24, -24));
            storyPages = Paginate(text, 33, 3);
            storyNext = next;
            storyPage = 0;
            storyStart = artStart = Time.unscaledTime;
            UpdateStoryBeat();
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
            storyScreen.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(storyScreen.gameObject);
            storyScreen = null;
        }
    }
}
