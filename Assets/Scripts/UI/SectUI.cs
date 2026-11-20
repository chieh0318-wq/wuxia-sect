using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private static readonly Color PanelColor = new Color(0.87f, 0.80f, 0.66f, 1f);
        private static readonly Color ButtonColor = new Color(0.43f, 0.20f, 0.15f, 1f);
        private static readonly Color Ink = new Color(0.20f, 0.15f, 0.10f);
        private readonly RectTransform canvas;
        private readonly RectTransform labels;
        private readonly RectTransform commandList;
        private readonly RectTransform details;
        private readonly RectTransform detailsList;
        private readonly RectTransform modal;
        private readonly RectTransform modalList;
        private readonly RectTransform dialogue;
        private readonly Text dialogueText;
        private readonly Text date;
        private readonly Text resources;
        private readonly Text toast;
        private readonly Font font;
        private readonly List<(Transform target, Vector3 offset, Text text)> worldLabels =
            new List<(Transform, Vector3, Text)>();

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
            date = Label(top, "", 28, TextAnchor.MiddleLeft);
            Anchor(date.rectTransform, Vector2.zero, Vector2.up, new Vector2(22, 0), new Vector2(360, 0));
            resources = Label(top, "", 24, TextAnchor.MiddleCenter);
            Anchor(resources.rectTransform, new Vector2(0.36f, 0), new Vector2(0.64f, 1),
                new Vector2(-120, 0), new Vector2(120, 0));
            var end = Button(top, "結束本月", endMonth);
            targets["end_month"] = end;
            Anchor(end, Vector2.right, Vector2.one, new Vector2(-188, 12), new Vector2(-12, -12));

            var commands = Panel("Commands", canvas, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -590), new Vector2(310, -110));
            commandList = ScrollList(commands, new Vector2(14, 14), new Vector2(-14, -14));

            details = Panel("Building details", canvas, Vector2.one, Vector2.one,
                new Vector2(-390, -665), new Vector2(-18, -110));
            detailsList = ScrollList(details, new Vector2(14, 14), new Vector2(-14, -72));
            var closeDetails = Button(details, "關閉", CloseBuilding);
            Anchor(closeDetails, Vector2.one, Vector2.one, new Vector2(-118, -60), new Vector2(-14, -12));
            CloseBuilding();

            var help = Panel("Controls", canvas, Vector2.zero, Vector2.zero,
                new Vector2(18, 18), new Vector2(520, 98), false);
            Fill(Label(help, "WASD／方向鍵移動　右鍵／中鍵拖曳\n滾輪縮放　左鍵選取　Esc／右鍵取消建造",
                22, TextAnchor.MiddleLeft).rectTransform, 12);

            var toastPanel = Panel("Messages", canvas, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-380, 258), new Vector2(380, 310), false);
            toast = Label(toastPanel, "", 23, TextAnchor.MiddleCenter);
            Fill(toast.rectTransform, 8);
            toastPanel.gameObject.SetActive(false);

            dialogue = Panel("Dialogue", canvas, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-440, 102), new Vector2(440, 246));
            dialogueText = Label(dialogue, "", 25);
            Anchor(dialogueText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(20, 14), new Vector2(-120, -14));
            var closeDialogue = Button(dialogue, "關閉", CloseDialogue);
            Anchor(closeDialogue, Vector2.one, Vector2.one, new Vector2(-112, -65), new Vector2(-14, -14));
            CloseDialogue();

            modal = Panel("Modal blocker", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modal.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var card = Panel("Report", modal, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(-350, -235), new Vector2(350, 235));
            modalList = ScrollList(card, new Vector2(22, 82), new Vector2(-22, -22));
            modalButtons = Rect("Report actions", card);
            Anchor(modalButtons, Vector2.zero, Vector2.right, new Vector2(22, 18), new Vector2(-22, 68));
            var actions = modalButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            actions.spacing = 16;
            actions.childControlWidth = actions.childControlHeight = true;
            actions.childForceExpandWidth = actions.childForceExpandHeight = true;
            CloseModal();
            CreateObjective();
        }

        public void Refresh(GameState state)
        {
            date.text = state.Date;
            resources.text = $"銀兩 {state.Silver}　糧食 {state.Food}　聲望 {state.Reputation}　弟子 {state.Disciples.Count}";
        }

        public void ShowCommands(string title, IEnumerable<(string label, Action run)> commands)
        {
            Clear(commandList);
            AddLine(commandList, title, 28);
            targets.Remove("cmd_build");
            targets.Remove("cmd_recruit");
            int index = 0;
            foreach (var command in commands)
            {
                var rect = Button(commandList, command.label, command.run);
                if (title == "指令" && index < 2)
                    targets[index == 0 ? "cmd_build" : "cmd_recruit"] = rect;
                index++;
            }
        }

        public void ShowBuilding(BuildingData building, IEnumerable<(string label, Action run)> actions)
        {
            Clear(detailsList);
            AddLine(detailsList, building.name, 28);
            AddLine(detailsList, "等級 1");
            AddLine(detailsList, building.description);
            foreach (var action in actions)
                Button(detailsList, action.label, action.run);
            details.gameObject.SetActive(true);
        }

        public void ShowModal(string title, IEnumerable<string> lines,
            IEnumerable<(string label, Action run)> buttons = null, bool dismissible = true, Action closed = null)
        {
            Clear(modalList);
            foreach (Transform child in modalButtons)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            AddLine(modalList, title, 30);
            foreach (string line in lines)
                AddLine(modalList, line);
            modalDismissible = dismissible;
            modalClosed = closed;
            foreach (var action in buttons ?? new[] { ("確定", (Action)(() => CloseModal())) })
                Button(modalButtons, action.Item1, action.Item2);
            modal.transform.SetAsLastSibling();
            modal.gameObject.SetActive(true);
        }

        public void ShowDialogue(DiscipleData disciple, string line)
        {
            dialogueText.text = $"{disciple.name}　{disciple.title}／{disciple.trait}\n{line}";
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
        public void Destroy() => UnityEngine.Object.Destroy(canvas.gameObject);

        public void Message(string message)
        {
            toast.text = message;
            toast.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public void AddWorldLabel(Transform target, Vector3 offset, string name)
        {
            var label = Label(labels, name, 18, TextAnchor.MiddleCenter);
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(64, name.Length * 18 + 12), 26);
            var backing = label.gameObject.AddComponent<Outline>();
            backing.effectColor = new Color(.91f, .84f, .69f);
            backing.effectDistance = new Vector2(1.5f, 1.5f);
            worldLabels.Add((target, offset, label));
        }

        public void PositionLabels(Camera camera)
        {
            Camera projection = Canvas.renderMode == RenderMode.ScreenSpaceCamera ? camera : null;
            foreach (var entry in worldLabels)
            {
                Vector3 screen = camera.WorldToScreenPoint(entry.target.position + entry.offset);
                bool visible = screen.z > 0 && screen.z < 56 && camera.pixelRect.Contains(screen);
                entry.text.gameObject.SetActive(visible);
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(labels, screen, projection, out var point))
                    entry.text.rectTransform.anchoredPosition = point;
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
            line.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private RectTransform Button(RectTransform parent, string text, Action action)
        {
            var rect = Panel(text, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rect.GetComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 53;
            var label = Label(rect, text, 22, TextAnchor.MiddleCenter);
            label.color = new Color(.98f, .92f, .81f);
            Fill(label.rectTransform, 4);
            return rect;
        }

        private Text Label(RectTransform parent, string content, int size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var text = Rect("Text", parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Ink;
            text.raycastTarget = false;
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

        private static RectTransform Panel(string name, RectTransform parent, Vector2 a, Vector2 b,
            Vector2 min, Vector2 max, bool blocksClicks = true)
        {
            var rect = Rect(name, parent);
            Anchor(rect, a, b, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = blocksClicks;
            var frame = rect.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(.24f, .14f, .09f, .95f);
            frame.effectDistance = new Vector2(3, -3);
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
