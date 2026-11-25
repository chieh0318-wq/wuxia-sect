using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>One line of a portrait talk: who speaks, which side they stand on, and what they say.</summary>
    internal struct PortraitLine
    {
        public string speaker, title, portrait, gender, text;
        public bool right;

        internal PortraitLine(string speaker, string title, string portrait, string gender, string text, bool right = false)
        {
            this.speaker = speaker;
            this.title = title;
            this.portrait = portrait;
            this.gender = gender;
            this.text = text;
            this.right = right;
        }
    }

    /// <summary>
    /// v1125 #4: the Nobunaga-style talk used everywhere outside full story scenes (courtyard chat, the 軍師's
    /// month summary, the new-month opener, sect-ledger talks). A large half-length portrait stands on the left or
    /// right over the live scene, beside a wide lacquer text box with a clear nameplate. Click / Space reveals the
    /// line, then advances; right-click / Esc closes (UiBack in GameBootstrap.HandleBack).
    /// </summary>
    public sealed partial class SectUI
    {
        private RectTransform talkScreen;
        private TalkTypewriter talkWriter;
        private Action talkDone;

        internal bool TalkOpen => talkScreen != null;

        /// <summary>Single line with optional buttons (e.g. 「人物詳情」).</summary>
        internal void ShowTalk(PortraitLine line, IEnumerable<(string label, Action run)> actions = null, Action closed = null) =>
            ShowTalk(new[] { line }, closed, actions);

        /// <summary>Plays <paramref name="lines"/> in order; <paramref name="done"/> runs after the last one closes.</summary>
        internal void ShowTalk(IList<PortraitLine> lines, Action done, IEnumerable<(string label, Action run)> actions = null)
        {
            CloseTalk(false);
            CloseDialogue();
            if (lines == null || lines.Count == 0)
            {
                done?.Invoke();
                return;
            }
            talkDone = done;
            var buttons = actions?.ToArray() ?? Array.Empty<(string, Action)>();
            int index = 0;
            Draw();

            void Draw()
            {
                var line = lines[index];
                bool last = index == lines.Count - 1;
                BuildTalk(line, last ? buttons : Array.Empty<(string, Action)>(), () =>
                {
                    if (index + 1 < lines.Count)
                    {
                        index++;
                        Draw();
                    }
                    else CloseTalk();
                }, last ? lines.Count : index + 1, lines.Count);
            }
        }

        /// <summary>Closes the talk; <paramref name="finish"/> runs the sequence's completion callback.</summary>
        internal void CloseTalk(bool finish = true)
        {
            if (talkScreen != null)
            {
                talkScreen.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(talkScreen.gameObject);
                talkScreen = null;
                talkWriter = null;
            }
            var done = talkDone;
            talkDone = null;
            if (finish) done?.Invoke();
        }

        internal void RevealTalkForCapture() => talkWriter?.Complete();

        private void BuildTalk(PortraitLine line, (string label, Action run)[] actions, Action advance, int page, int pages)
        {
            if (talkScreen != null)
            {
                talkScreen.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(talkScreen.gameObject);
            }
            talkScreen = Rect("Portrait talk", canvas);
            Fill(talkScreen);
            talkScreen.SetAsLastSibling();
            // Soft ink wash over the lower half keeps the live scene visible while the text stays readable.
            var wash = Rect("Talk wash", talkScreen);
            Anchor(wash, Vector2.zero, new Vector2(1, .62f), Vector2.zero, Vector2.zero);
            var washImage = wash.gameObject.AddComponent<RawImage>();
            washImage.texture = TalkWash();
            washImage.raycastTarget = true; // the talk is modal: clicks do not fall through to the courtyard
            var blocker = wash.gameObject.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.onClick.AddListener(() => Press());

            bool right = line.right;
            bool hasPortrait = !string.IsNullOrEmpty(line.portrait);
            const float portraitW = 470, portraitH = 700;
            if (hasPortrait)
            {
                var side = new Vector2(right ? 1 : 0, 0);
                float x = right ? -portraitW - 40 : 40;
                var figure = Panel("Talk portrait", talkScreen, side, side, new(x, 30), new(x + portraitW, 30 + portraitH), false);
                UiKit.Skin(figure.GetComponent<Image>(), "Lacquer");
                var art = Rect("Portrait art", figure);
                Fill(art, 10);
                var texture = PortraitService.Resolve(line.portrait, line.gender);
                var raw = AddImage(art, texture);
                FitCover(raw, texture, (portraitW - 20) / (portraitH - 20));
            }

            float left = hasPortrait && !right ? portraitW + 70 : 60;
            float rightPad = hasPortrait && right ? portraitW + 70 : 60;
            // v1126d CF2: the lacquer skin is partly see-through, so the bottom bar, the pinned objective and
            // toasts showed through the spoken line. A solid ink backing keeps the text clean.
            var backing = Rect("Talk backing", talkScreen);
            Anchor(backing, Vector2.zero, Vector2.right, new(left + 6, 42), new(-rightPad - 6, 330));
            var backingImage = backing.gameObject.AddComponent<Image>();
            backingImage.color = new Color(.11f, .08f, .06f, 1f);
            backingImage.raycastTarget = false;
            var box = Panel("Talk box", talkScreen, Vector2.zero, Vector2.right, new(left, 36), new(-rightPad, 336));
            UiKit.Skin(box.GetComponent<Image>(), "Lacquer");
            box.GetComponent<Image>().color = new Color(1, 1, 1, .96f);
            var press = box.gameObject.AddComponent<Button>();
            press.transition = Selectable.Transition.None;
            press.onClick.AddListener(() => Press());

            // v1126c PC3 #5: the shared compact plate and larger upright body text (旁白 included).
            var plate = DialoguePlate(box, line.speaker, 34);
            if (!string.IsNullOrWhiteSpace(line.title))
            {
                float titleLeft = 34 + plate.rect.width + 16;
                var title = Label(box, line.title, 20, TextAnchor.MiddleLeft);
                title.color = CardMuted;
                Anchor(title.rectTransform, Vector2.up, Vector2.one, new(titleLeft, -30), new(-30, 4));
            }

            var body = Label(box, "", DialogueStyle.BodySize, TextAnchor.UpperLeft);
            body.color = CardText;
            DialogueBody(body, line.text);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new(44, 66), new(-36, -50));

            var hint = Label(box, pages > 1 ? $"{page}／{pages}　點擊繼續　右鍵關閉" : "點擊繼續　右鍵關閉", 18, TextAnchor.MiddleLeft);
            hint.color = CardMuted;
            Anchor(hint.rectTransform, Vector2.zero, Vector2.zero, new(44, 18), new(520, 58));
            if (actions.Length > 0)
            {
                var row = Rect("Talk actions", box);
                Anchor(row, Vector2.right, Vector2.right, new(-30 - actions.Length * 214, 16), new(-30, 70));
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 14;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = true;
                foreach (var (label, run) in actions)
                {
                    var button = Button(row, label, () =>
                    {
                        CloseTalk(false);
                        run?.Invoke();
                    });
                    button.GetComponent<Button>().interactable = run != null;
                    UiKit.Skin(button.GetComponent<Image>(), "Wood");
                }
            }
            // Legacy Text wraps only at spaces; no-break spaces keep the full line in one flowing paragraph.
            talkWriter = talkScreen.gameObject.AddComponent<TalkTypewriter>();
            talkWriter.Begin(body, DialogueStyle.Clean(line.text).Replace(' ', ' '), CharactersPerSecond);

            void Press()
            {
                if (talkWriter != null && !talkWriter.Done)
                {
                    talkWriter.Complete();
                    return;
                }
                advance();
            }
            talkWriter.Advance = Press;
        }

        /// <summary>v1125 #9/#10: adds an action button to the open modal page (before 關閉 when <paramref name="first"/>).</summary>
        internal void AddModalAction(string label, Action run, bool first = false)
        {
            if (!ModalOpen) return;
            var button = Button(modalButtons, label, run);
            button.name = label.Split('\n')[0];
            button.GetComponent<Button>().interactable = run != null;
            UiKit.Skin(button.GetComponent<Image>(), "Wood");
            if (first) button.SetSiblingIndex(0);
            FitModalButtons();
        }

        /// <summary>
        /// v1125 #1: the modal action row shares its width by caption length so long labels (委任總管…) get room
        /// instead of shrinking below the readable size or spilling over their neighbours. Pages with many actions
        /// widen the card up to 1400 px.
        /// </summary>
        internal void FitModalButtons(float minCardWidth = 0)
        {
            var active = modalButtons.GetComponentsInChildren<Button>(false).Where(b => b.gameObject.activeSelf).ToArray();
            float need = 0;
            bool twoLines = false;
            foreach (var button in active)
            {
                var caption = button.GetComponentInChildren<Text>();
                if (caption == null) continue;
                var parts = caption.text.Split('\n');
                int longest = parts.Max(line => line.Length);
                twoLines |= parts.Length > 1;
                var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
                layout.flexibleWidth = Mathf.Max(.7f, longest / 5f);
                layout.minWidth = Mathf.Min(320, longest * 19 + 24);
                need += layout.minWidth + 16;
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
                caption.lineSpacing = .92f;
            }
            float width = Mathf.Max(minCardWidth, need + 60);
            if (width > modalCard.rect.width + 1)
            {
                float half = Mathf.Min(1400, width) / 2;
                var size = modalCard.rect.height / 2;
                Anchor(modalCard, Vector2.one * .5f, Vector2.one * .5f, new(-half, -size), new(half, size));
            }
            // Two-line captions (label + cost / effect) need a taller action row; lift the page body above it.
            if (twoLines)
            {
                Anchor(modalButtons, Vector2.zero, Vector2.right, new(30, 16), new(-30, 90));
                var viewport = (RectTransform)modalList.parent;
                if (viewport.offsetMin.y < 100) viewport.offsetMin = new Vector2(viewport.offsetMin.x, 100);
                foreach (var button in active)
                {
                    var caption = button.GetComponentInChildren<Text>();
                    if (caption == null) continue;
                    caption.resizeTextMaxSize = Mathf.Min(caption.resizeTextMaxSize, 21);
                    caption.fontSize = Mathf.Min(caption.fontSize, 21);
                }
            }
        }

        /// <summary>Resizes the open modal card (centred), e.g. for list pages with many entries.</summary>
        internal void SizeModal(float width, float height)
        {
            Anchor(modalCard, Vector2.one * .5f, Vector2.one * .5f, new(-width / 2, -height / 2), new(width / 2, height / 2));
        }

        private static Texture2D talkWash;

        private static Texture2D TalkWash()
        {
            if (talkWash != null) return talkWash;
            talkWash = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                talkWash.SetPixel(0, y, new Color(.03f, .025f, .02f, Mathf.SmoothStep(.72f, 0, y / 63f)));
            talkWash.Apply(false, true);
            return talkWash;
        }
    }

    /// <summary>Reveals a talk line character by character; Space advances like a click.</summary>
    internal sealed class TalkTypewriter : MonoBehaviour
    {
        private Text target;
        private string full;
        private float start, speed;
        internal Action Advance;
        internal bool Done => target == null || target.text.Length >= full.Length;

        internal void Begin(Text text, string content, float charactersPerSecond)
        {
            target = text;
            full = content ?? "";
            speed = Mathf.Max(1, charactersPerSecond);
            start = Time.unscaledTime;
            target.text = "";
        }

        internal void Complete()
        {
            if (target == null) return;
            start = Time.unscaledTime - full.Length / speed - .1f;
            target.text = full;
        }

        private void Update()
        {
            if (target == null) return;
            int count = Mathf.Min(full.Length, (int)((Time.unscaledTime - start) * speed));
            if (count != target.text.Length) target.text = full.Substring(0, count);
            if (Input.GetKeyDown(KeyCode.Space)) Advance?.Invoke();
        }
    }
}
