using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private static readonly Color ButtonColor = new Color(0.43f, 0.20f, 0.15f, 1f);
        private static readonly Color Ink = new Color(.15f, .105f, .075f);
        private readonly RectTransform canvas;
        private readonly RectTransform labels;
        private readonly RectTransform commandList;
        private readonly RectTransform details;
        private readonly RectTransform detailsList;
        private readonly RectTransform modal;
        private readonly RectTransform modalList;
        private readonly RectTransform modalCard;
        private readonly RectTransform dialogue;
        private readonly Text dialogueText;
        private readonly Text speakerName;
        private readonly Text date;
        private readonly Text[] resourceLabels = new Text[4];
        private RectTransform titleScreen;
        private readonly Text apDisplay;
        private readonly ApHud apHud;
        private readonly RawImage dialoguePortrait;
        private Action inspectDisciple;
        private readonly Text toast;
        private readonly Font font;
        private readonly List<(Transform target, Vector3 offset, RectTransform tag)> worldLabels = new();
        private readonly List<Rect> visibleTags = new();

        public bool ModalOpen => modal.gameObject.activeSelf;
        public bool DialogueOpen => dialogue.gameObject.activeSelf;

        public SectUI(Action endMonth)
        {
            var screen = new GameObject("Sect UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            screen.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            screen.GetComponent<Canvas>().sortingOrder = 40;
            var scaler = screen.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvas = (RectTransform)screen.transform;
            font = Font.CreateDynamicFontFromOSFont(new[]
            {
                "Microsoft JhengHei", "PMingLiU", "Microsoft YaHei"
            }, 24);
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            labels = Rect("World labels", canvas);
            Fill(labels);
            var top = Panel("Top bar", canvas, new Vector2(0, 1), Vector2.one,
                new Vector2(18, -94), new Vector2(-18, -18));
            UiKit.Skin(top.GetComponent<Image>(), "Teal");
            date = Label(top, "", 28, TextAnchor.MiddleLeft);
            var lightInk = new Color(.92f, .86f, .73f);
            date.color = lightInk;
            Anchor(date.rectTransform, Vector2.zero, Vector2.up, new(56, 0), new(360, 0));
            HudIcon(top, "Month", 16);
            var counters = Rect("門派資源", top);
            Anchor(counters, new(.31f, 0), new(.68f, 1), Vector2.zero, Vector2.zero);
            string[] icons = { "Silver", "Food", "Reputation", "Disciples" };
            for (int index = 0; index < icons.Length; index++)
            {
                var cell = Rect(icons[index], counters);
                Anchor(cell, new(index * .25f, 0), new((index + 1) * .25f, 1), Vector2.zero, Vector2.zero);
                HudIcon(cell, icons[index], 0);
                var value = Label(cell, "", 24, TextAnchor.MiddleLeft);
                value.color = lightInk;
                value.font = UiKit.Font("Numbers", font);
                Anchor(value.rectTransform, Vector2.zero, Vector2.one, new(36, 0), new(-6, 0));
                resourceLabels[index] = value;
            }
            var ap = Panel("行動力", top, new(.70f, 0), new(.70f, 1), new(16, 8), new(288, -8));
            UiKit.Skin(ap.GetComponent<Image>(), "Wood");
            apDisplay = Label(ap, "", 24, TextAnchor.MiddleCenter);
            apDisplay.color = lightInk;
            Fill(apDisplay.rectTransform, 8);
            apHud = ap.gameObject.AddComponent<ApHud>();
            var apSources = Panel("行動力來源", ap, Vector2.zero, Vector2.right, new(-38, -134), new(38, -4), false);
            UiKit.Skin(apSources.GetComponent<Image>(), "Wood");
            var sourceList = Label(apSources, "", 20, TextAnchor.MiddleLeft);
            sourceList.color = lightInk;
            Fill(sourceList.rectTransform, 14);
            apHud.Initialize(apDisplay, sourceList);
            targets["ap"] = ap;
            var end = Button(top, "結束本月", endMonth);
            targets["end_month"] = end;
            Anchor(end, Vector2.right, Vector2.one, new Vector2(-188, 12), new Vector2(-12, -12));

            var commands = Panel("Commands", canvas, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -365), new Vector2(310, -110));
            this.commands = commands;
            commandList = ScrollList(commands, new Vector2(14, 14), new Vector2(-14, -14));

            details = Panel("Building details", canvas, Vector2.one, Vector2.one,
                new Vector2(-390, -665), new Vector2(-18, -110));
            detailsList = ScrollList(details, new Vector2(14, 14), new Vector2(-14, -72));
            var closeDetails = Button(details, "關閉", CloseBuilding);
            Anchor(closeDetails, Vector2.one, Vector2.one, new Vector2(-118, -60), new Vector2(-14, -12));
            CloseBuilding();

            var help = Panel("Controls", canvas, Vector2.zero, Vector2.zero,
                new Vector2(18, 18), new Vector2(570, 106), false);
            Fill(Label(help, "WASD／方向鍵移動　右鍵／中鍵拖曳　滾輪縮放\n左鍵選取　M 江湖地圖　Esc 取消建造／設定",
                22, TextAnchor.MiddleLeft).rectTransform, 12);

            var toastPanel = Panel("Messages", canvas, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-380, 258), new Vector2(380, 310), false);
            toast = Label(toastPanel, "", 23, TextAnchor.MiddleCenter);
            Fill(toast.rectTransform, 8);
            toastPanel.gameObject.SetActive(false);

            dialogue = Panel("Dialogue", canvas, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-440, 102), new Vector2(440, 246));
            var portrait = Rect("Portrait", dialogue);
            Anchor(portrait, Vector2.zero, Vector2.up, new(16, 12), new(110, -12));
            dialoguePortrait = portrait.gameObject.AddComponent<RawImage>();
            dialoguePortrait.raycastTarget = false;
            speakerName = Label(dialogue, "", 25);
            speakerName.font = UiKit.Font("Names", font);
            Anchor(speakerName.rectTransform, Vector2.zero, Vector2.one, new(126, 80), new(-120, -15));
            dialogueText = Label(dialogue, "", 24);
            dialogueText.font = UiKit.Font("Dialogue", font);
            Anchor(dialogueText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(126, 14), new Vector2(-120, -62));
            var inspect = Button(dialogue, "弟子", () => inspectDisciple?.Invoke());
            Anchor(inspect, Vector2.one, Vector2.one, new(-112, -124), new(-14, -73));
            var closeDialogue = Button(dialogue, "關閉", CloseDialogue);
            Anchor(closeDialogue, Vector2.one, Vector2.one, new Vector2(-112, -65), new Vector2(-14, -14));
            CloseDialogue();

            modal = Panel("Modal blocker", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backdrop = modal.GetComponent<Image>();
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            backdrop.color = new Color(.08f, .055f, .034f, .40f);
            modalCard = Panel("Report", modal, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modalList = ScrollList(modalCard, new Vector2(26, 82), new Vector2(-26, -22));
            modalButtons = Rect("Report actions", modalCard);
            Anchor(modalButtons, Vector2.zero, Vector2.right, new Vector2(22, 18), new Vector2(-22, 68));
            var actions = modalButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            actions.spacing = 16;
            actions.childControlWidth = actions.childControlHeight = true;
            actions.childForceExpandWidth = actions.childForceExpandHeight = true;
            CloseModal(force: true);
            CreateObjective();
        }

        internal event Action ApInspected
        {
            add => apHud.Inspected += value;
            remove => apHud.Inspected -= value;
        }

        public bool BuildingOpen => details.gameObject.activeSelf;

        private static void HudIcon(RectTransform parent, string icon, int offset)
        {
            var rect = Rect(icon + " icon", parent);
            Anchor(rect, new(0, .5f), new(0, .5f), new(offset, -18), new(offset + 36, 18));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiKit.Sprite(icon);
            image.enabled = image.sprite != null;
            image.raycastTarget = false;
        }

        public void Refresh(GameState state)
        {
            date.text = state.DateWithDay;
            apHud.Set(state.ActionPoints, state.ApMax, state.ApDescription);
            resourceLabels[0].text = $"銀兩 {state.Silver}";
            resourceLabels[1].text = $"糧食 {state.Food}";
            resourceLabels[2].text = $"聲望 {state.Reputation}";
            resourceLabels[3].text = $"弟子 {state.Disciples.Count}";
        }

        public void ShowCommands(string title, IEnumerable<(string label, Action run)> commands)
        {
            Clear(commandList);
            SetCommandsPanelHeight(title == "建造" ? 480 : 381);
            AddLine(commandList, title, 28);
            targets.Remove("cmd_build");
            targets.Remove("cmd_recruit");
            int index = 0;
            foreach (var command in commands)
            {
                var rect = Button(commandList, command.label, command.run);
                rect.GetComponent<Button>().interactable = command.run != null;
                if (title == "指令" && index < 2)
                    targets[index == 0 ? "cmd_build" : "cmd_recruit"] = rect;
                index++;
            }
        }

        private RectTransform mapButtons;

        /// <summary>Bottom-right wood plaque with the world map (M) and intel overview shortcuts.</summary>
        internal void ShowMapButtons(Action openMap, Action openIntel, Action openCharacters)
        {
            if (mapButtons != null)
                return;
            mapButtons = Panel("江湖", canvas, Vector2.right, Vector2.right, new(-640, 18), new(-18, 92));
            UiKit.Skin(mapButtons.GetComponent<Image>(), "Wood");
            var row = mapButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 10, 10);
            row.spacing = 12;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            var mapButton = Button(mapButtons, "江湖地圖　M", openMap);
            var intelButton = Button(mapButtons, "情報總覽", openIntel);
            Button(mapButtons, "江湖人物　C", openCharacters);
            targets["map"] = mapButton;
            targets["intel"] = intelButton;
        }

        private void SetCommandsPanelHeight(float height) => commands.offsetMin = new Vector2(18, -110 - height);

        public void ShowBuilding(BuildingData building, IEnumerable<(string label, Action run)> actions, int level = 1)
        {
            Clear(detailsList);
            AddLine(detailsList, building.name, 28);
            AddLine(detailsList, $"等級 {level}");
            AddLine(detailsList, building.description);
            foreach (var action in actions)
                Button(detailsList, action.label, action.run).GetComponent<Button>().interactable = action.run != null;
            details.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailsList);
            float height = Mathf.Clamp(LayoutUtility.GetPreferredHeight(detailsList) + 86, 155, 555);
            Anchor(details, Vector2.one, Vector2.one, new Vector2(-390, -110 - height), new Vector2(-18, -110));
            detailsList.anchoredPosition = Vector2.zero;
        }

        public void ShowModal(string title, IEnumerable<string> lines,
            IEnumerable<(string label, Action run)> buttons = null, bool dismissible = true, Action closed = null)
        {
            ResetCreationLayout();
            Clear(modalList);
            foreach (Transform child in modalButtons)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            if (!string.IsNullOrEmpty(title))
                AddLine(modalList, title, 30);
            foreach (string line in lines)
                AddLine(modalList, line);
            Vector2 center = dismissible ? Vector2.one * .5f : new Vector2(.5f, .24f);
            Vector2 extent = dismissible ? new Vector2(352, 185) : new Vector2(420, 103);
            Anchor(modalCard, center, center, -extent, extent);
            modal.GetComponent<Image>().color = new Color(.08f, .055f, .034f, dismissible ? .38f : .21f);
            modalDismissible = dismissible;
            modalClosed = closed;
            foreach (var action in buttons ?? new[] { ("確定", (Action)(() => CloseModal())) })
                Button(modalButtons, action.Item1, action.Item2);
            if (modalCard.GetComponent<InkPanelMotion>() == null)
                modalCard.gameObject.AddComponent<InkPanelMotion>();
            modal.transform.SetAsLastSibling();
            modal.gameObject.SetActive(true);
        }

        public void ShowTitle(string title, Action start, Action resume)
        {
            CloseTitle();
            titleScreen = Panel("Title", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var background = titleScreen.GetComponent<Image>();
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = new Color(.08f, .15f, .16f);
            var image = Rect("Title art", titleScreen);
            Fill(image);
            AddImage(image, PortraitService.Story("title_bg") ?? UiKit.Backdrop());
            var plaque = Panel("Title plaque", titleScreen, new(.30f, .5f), new(.30f, .5f),
                new(-256, -208), new(256, 240));
            UiKit.Skin(plaque.GetComponent<Image>(), "Plaque");
            var heading = Label(plaque, title, 62, TextAnchor.MiddleCenter);
            heading.color = new Color(.94f, .88f, .76f);
            Anchor(heading.rectTransform, Vector2.up, Vector2.one, new(24, -128), new(-24, -16));
            var divider = Rect("Divider", plaque);
            Anchor(divider, Vector2.up, Vector2.one, new(48, -131), new(-48, -123));
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.sprite = UiKit.Sprite("Divider");
            dividerImage.raycastTarget = false;
            AddTitleButton("新遊戲", start, 192);
            AddTitleButton("繼續", resume, 264);
            AddTitleButton("設定", ShowAudioOptions, 336);
            AddTitleButton("離開", Application.Quit, 408);

            void AddTitleButton(string text, Action run, float bottom)
            {
                var item = Button(plaque, text, run);
                Anchor(item, Vector2.up, Vector2.one, new(72, -bottom), new(-72, -bottom + 56));
                item.GetComponent<Button>().interactable = run != null;
            }
        }

        public void CloseTitle()
        {
            if (titleScreen == null)
                return;
            titleScreen.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(titleScreen.gameObject);
            titleScreen = null;
        }

        public void ShowAudioOptions()
        {
            ShowModal("設定", Array.Empty<string>());
            Anchor(modalCard, Vector2.one * .5f, Vector2.one * .5f, new(-352, -224), new(352, 224));
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one, new(26, 82), new(-26, -22));
            AddVolume("主音量", GameAudioPrefs.MasterVolume, value => GameAudioPrefs.Master = value);
            AddVolume("音樂", GameAudioPrefs.Music, value => GameAudioPrefs.Music = value);
            AddVolume("音效", GameAudioPrefs.Sfx, value => GameAudioPrefs.Sfx = value);
            ToggleMute();

            void ToggleMute()
            {
                Button(modalList, GameAudioPrefs.Muted ? "取消靜音" : "靜音", () =>
                {
                    GameAudioPrefs.Muted = !GameAudioPrefs.Muted;
                    ShowAudioOptions();
                });
            }

            void AddVolume(string caption, float value, Action<float> changed)
            {
                var row = Rect(caption, modalList);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
                var label = Label(row, "", 22, TextAnchor.MiddleLeft);
                Anchor(label.rectTransform, Vector2.zero, Vector2.up, Vector2.zero, new(170, 0));
                var sliderRect = Rect("音量", row);
                Anchor(sliderRect, Vector2.zero, Vector2.one, new(180, 0), Vector2.zero);
                var track = Rect("Track", sliderRect);
                Anchor(track, new(0, .5f), new(1, .5f), new(0, -3), new(0, 3));
                track.gameObject.AddComponent<Image>().color = new Color(.34f, .25f, .17f);
                var range = Rect("Handle range", sliderRect);
                Fill(range, 15);
                var handle = Rect("Handle", range);
                handle.sizeDelta = new Vector2(22, 24);
                var ink = handle.gameObject.AddComponent<Image>();
                ink.color = ButtonColor;
                var slider = sliderRect.gameObject.AddComponent<Slider>();
                slider.handleRect = handle;
                slider.targetGraphic = ink;
                slider.value = value;
                SetCaption(value);
                slider.onValueChanged.AddListener(next =>
                {
                    SetCaption(next);
                    changed(next);
                });
                void SetCaption(float current) => label.text = $"{caption} {current:P0}";
            }
        }

        public void ShowDialogue(DiscipleData disciple, string line, Action inspect = null)
        {
            inspectDisciple = inspect;
            dialoguePortrait.texture = PortraitService.Resolve(string.IsNullOrEmpty(disciple.portrait) ? disciple.id : disciple.portrait, disciple.gender);
            speakerName.text = $"{disciple.name}　{disciple.title}／{disciple.trait}";
            dialogueText.text = line;
            dialogue.gameObject.SetActive(true);
        }

        public bool IsInDialogue(Vector2 screenPoint) =>
            RectTransformUtility.RectangleContainsScreenPoint(dialogue, screenPoint);

        public void CloseBuilding() => details.gameObject.SetActive(false);
        public void CloseModal(bool force = false)
        {
            if (ModalOpen && !modalDismissible && !force)
                return;
            modal.gameObject.SetActive(false);
            var callback = modalClosed;
            modalClosed = null;
            callback?.Invoke();
        }
        public void CloseDialogue() => dialogue.gameObject.SetActive(false);
        public void Destroy()
        {
            if (canvas)
                UnityEngine.Object.Destroy(canvas.gameObject);
            UnityEngine.Object.Destroy(paper);
            UnityEngine.Object.Destroy(paperTexture);
        }

        public void Message(string message)
        {
            toast.text = message;
            toast.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public void AddWorldLabel(Transform target, Vector3 offset, string name, bool building = true)
        {
            var tag = Panel("Name tag", labels, Vector2.one * .5f, Vector2.one * .5f,
                Vector2.zero, Vector2.zero, false);
            int size = building ? 22 : 16;
            tag.sizeDelta = new Vector2(Mathf.Max(66, name.Length * (size + 1) + 16), size + 13);
            var ink = Label(tag, name, size, TextAnchor.MiddleCenter);
            ink.color = new Color(.17f, .105f, .065f);
            Fill(ink.rectTransform, 3);
            worldLabels.Add((target, offset, tag));
        }

        internal void RemoveWorldLabel(Transform target)
        {
            int index = worldLabels.FindIndex(entry => entry.target == target);
            if (index < 0) return;
            UnityEngine.Object.Destroy(worldLabels[index].tag.gameObject);
            worldLabels.RemoveAt(index);
        }

        public void PositionLabels(Camera camera)
        {
            Camera projection = Canvas.renderMode == RenderMode.ScreenSpaceCamera ? camera : null;
            visibleTags.Clear();
            foreach (var entry in worldLabels)
            {
                Vector3 screen = camera.WorldToScreenPoint(entry.target.position + entry.offset);
                bool visible = screen.z > 0 && screen.z < (entry.offset.y < 2.7f ? 39 : 57) &&
                    camera.pixelRect.Contains(screen);
                if (visible)
                {
                    float scale = Canvas.scaleFactor;
                    var rect = new Rect((Vector2)screen - entry.tag.sizeDelta * scale * .5f, entry.tag.sizeDelta * scale);
                    foreach (var other in visibleTags)
                        if (rect.Overlaps(other))
                        {
                            visible = false;
                            break;
                        }
                    if (visible)
                    {
                        visibleTags.Add(rect);
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(labels, screen, projection, out var local);
                        entry.tag.anchoredPosition = local;
                    }
                }
                entry.tag.gameObject.SetActive(visible);
            }
        }

        private RectTransform ScrollList(RectTransform parent, Vector2 min, Vector2 max)
        {
            var view = Rect("Scroll viewport", parent);
            Anchor(view, Vector2.zero, Vector2.one, min, max);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;

            var content = Rect("Content", view);
            Anchor(content, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1);
            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(0, 0, 0, 8);
            stack.spacing = 9;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        private void AddLine(RectTransform parent, string text, int size = 23)
        {
            var line = Label(parent, text, size);
            line.gameObject.AddComponent<LayoutElement>().minHeight = size + 8;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.resizeTextForBestFit = false;
            line.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private RectTransform Button(RectTransform parent, string text, Action action)
        {
            var rect = Panel(text, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rect.GetComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = ButtonColor;
            UiKit.Skin(image, "Vermilion");
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1, .86f, .75f);
            colors.pressedColor = new Color(.72f, .63f, .56f);
            button.colors = colors;
            rect.gameObject.AddComponent<UiHoverSound>();
            button.onClick.AddListener(() =>
            {
                UiSfx.Play("click");
                action?.Invoke();
            });
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 53;
            var label = Label(rect, text, 22, TextAnchor.MiddleCenter);
            label.color = new Color(.98f, .92f, .81f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 15;
            label.resizeTextMaxSize = 22;
            Fill(label.rectTransform, 4);
            return rect;
        }

        private Text Label(RectTransform parent, string content, int size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var text = Rect("Text", parent).gameObject.AddComponent<Text>();
            text.font = UiKit.Font(size >= 28 ? "Titles" : "Interface", font, content);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Ink;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(15, size);
            text.resizeTextMaxSize = size;
            text.text = content;
            return text;
        }

        private static void Clear(RectTransform parent)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            parent.anchoredPosition = Vector2.zero;
        }

        private RectTransform Panel(string name, RectTransform parent, Vector2 a, Vector2 b,
            Vector2 min, Vector2 max, bool blocksClicks = true)
        {
            var rect = Rect(name, parent);
            Anchor(rect, a, b, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiKit.Sprite("Paper") ?? Paper();
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = blocksClicks;
            return rect;
        }

        private static RectTransform Rect(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Fill(RectTransform rect, float padding = 0) =>
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.one * padding, Vector2.one * -padding);

        private static void Anchor(RectTransform rect, Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            rect.anchorMin = a;
            rect.anchorMax = b;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
