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
            CloseStoryBeat();
            recruitGrid = creationGrid = null;
            var buttons = choices?.ToList() ?? new List<(string, Action)>();
            if (next != null) buttons.Add(("繼續", next));
            if (skip != null) buttons.Add(("略過", skip));
            ShowModal(heading, lines.Where(line => !string.IsNullOrWhiteSpace(line)), buttons.ToArray(), dismissible: false);
            Anchor(modalCard, new(.5f, .5f), new(.5f, .5f), new(-540, -400), new(540, 400));
            modal.GetComponent<Image>().color = new Color(.08f, .15f, .16f);
            modalList.anchoredPosition = Vector2.zero;
        }

        internal void ModalPortrait(string id, string gender)
        {
            var row = CreationRow("肖像", 128);
            var image = Rect("Speaker", row);
            Anchor(image, new(.5f, 0), new(.5f, 1), new(-45, 4), new(45, -4));
            AddImage(image, PortraitService.Resolve(id, gender));
            Anchor(modalCard, new(.5f, .25f), new(.5f, .25f), new(-448, -180), new(448, 180));
        }

        internal void CreationGrid(int columns, int rows, float cellHeight)
        {
            const float gap = 12;
            creationGrid = CreationRow("選項", rows * cellHeight + (rows - 1) * gap);
            var grid = creationGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2((1028 - (columns - 1) * gap) / columns, cellHeight);
            grid.spacing = Vector2.one * gap;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }

        internal void CreationChoice(string label, string description, string image, Action run, string rarity = null)
        {
            var item = Button(creationGrid == null ? modalList : creationGrid, label, run);
            item.GetComponent<Button>().interactable = run != null;
            var layout = item.GetComponent<LayoutElement>();
            var caption = item.GetComponentInChildren<Text>();
            var art = string.IsNullOrEmpty(image) ? null : PortraitService.Story(image);
            layout.preferredHeight = art == null ? 108 : 140;
            var frame = item.GetComponent<Image>();
            UiKit.Skin(frame, rarity == "legendary" ? "Selected" : "Wood");
            frame.color = rarity switch
            {
                "legendary" => Color.white,
                "rare" => new Color(1, .87f, .64f),
                _ => new Color(.93f, .92f, .87f)
            };
            int left = art == null ? 16 : creationGrid == null ? 200 : 166;
            caption.alignment = TextAnchor.MiddleLeft;
            Anchor(caption.rectTransform, Vector2.up, Vector2.one, new(left, -40), new(-16, -6));
            var body = Label(item, description ?? "", 20);
            body.color = new Color(.96f, .89f, .75f);
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 16;
            body.resizeTextMaxSize = 20;
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new(left, 6), new(-16, -42));
            if (art != null)
            {
                var thumbnail = Rect("Choice illustration", item);
                Anchor(thumbnail, Vector2.zero, Vector2.up, new(8, 10), new(left - 12, -10));
                var raw = AddImage(thumbnail, art);
                float boxAspect = creationGrid == null ? 1.55f : .85f;
                float crop = Mathf.Min(1, boxAspect / (art.width / (float)art.height));
                raw.uvRect = new Rect((1 - crop) / 2, 0, crop, 1);
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

        internal void CreationPortraits(CharacterLook[] looks, CharacterLook selected, Action<CharacterLook> choose)
        {
            var row = CreationRow("形貌", 344);
            for (int index = 0; index < looks.Length; index++)
            {
                var look = looks[index];
                var card = Button(row, (look == selected ? "◆ " : "") + look.label, () => choose(look));
                Anchor(card, new(index * .22f, 0), new((index + 1) * .22f, 1), new(8, 8), new(-8, -8));
                var caption = card.GetComponentInChildren<Text>();
                Anchor(caption.rectTransform, Vector2.zero, Vector2.right, new(8, 4), new(-8, 44));
                var art = Rect("Portrait", card);
                Anchor(art, new(.5f, 0), new(.5f, 1), new(-82, 48), new(82, -12));
                AddImage(art, PortraitService.Resolve(string.IsNullOrEmpty(look.portrait) ? look.id : look.portrait, look.gender));
            }
            var preview = Rect("形貌預覽", row);
            Anchor(preview, new(.66f, 0), Vector2.one, new(12, 8), new(-8, -8));
            var output = AddImage(preview, null);
            preview.gameObject.AddComponent<CharacterPreview>().Initialize(selected, output);
            if (!string.IsNullOrEmpty(selected.desc))
                AddLine(modalList, selected.desc, 23);
        }

        internal void CreationAttribute(string name, int current, Action less, Action more)
        {
            var row = CreationRow(name, 54);
            var caption = Label(row, $"{name}　{current}", 26, TextAnchor.MiddleLeft);
            Fill(caption.rectTransform, 8);
            var minus = Button(row, "−", less);
            var plus = Button(row, "＋", more);
            Anchor(minus, Vector2.right, Vector2.one, new(-156, 2), new(-88, -2));
            Anchor(plus, Vector2.right, Vector2.one, new(-76, 2), new(-8, -2));
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
