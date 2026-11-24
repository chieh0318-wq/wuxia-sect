using System;
using System.Collections.Generic;
using System.Linq;
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
        private string lastMessage;
        private readonly Font font;
        private readonly List<(Transform target, Vector3 offset, RectTransform tag, bool building)> worldLabels = new();
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
                new Vector2(18, -98), new Vector2(-18, -18));
            UiKit.Skin(top.GetComponent<Image>(), "Teal");
            var lightInk = new Color(.95f, .89f, .76f);
            var datePlaque = HudPlaque(top, "日期", new(0, 0), new(0, 1), new(14, 10), new(400, -10));
            date = Label(datePlaque, "", 24, TextAnchor.MiddleLeft);
            date.color = UiKit.HeaderGold;
            date.font = UiKit.Font("Names", font);
            Anchor(date.rectTransform, Vector2.zero, Vector2.one, new(58, 0), new(-10, 0));
            HudIcon(datePlaque, "Month", 14);
            var counters = Rect("門派資源", top);
            Anchor(counters, new(.225f, 0), new(.655f, 1), Vector2.zero, Vector2.zero);
            string[] icons = { "Silver", "Food", "Reputation", "Disciples" };
            for (int index = 0; index < icons.Length; index++)
            {
                var cell = HudPlaque(counters, icons[index], new(index * .25f, 0), new((index + 1) * .25f, 1),
                    new(5, 10), new(-5, -10));
                HudIcon(cell, icons[index], 12);
                var value = Label(cell, "", 22, TextAnchor.MiddleLeft);
                value.color = lightInk;
                value.font = UiKit.Font("Numbers", font);
                Anchor(value.rectTransform, Vector2.zero, Vector2.one, new(52, 0), new(-8, 0));
                resourceLabels[index] = value;
            }
            var ap = HudPlaque(top, "行動力", new(.665f, 0), new(.665f, 1), new(0, 8), new(290, -8));
            AddCardIcon(ap, "hud_ap", 12, 28);
            apDisplay = Label(ap, "", 22, TextAnchor.MiddleLeft);
            apDisplay.color = lightInk;
            Anchor(apDisplay.rectTransform, Vector2.zero, Vector2.one, new(48, 22), new(-12, -4));
            apHud = ap.gameObject.AddComponent<ApHud>();
            var apSources = Panel("行動力來源", ap, Vector2.zero, Vector2.right, new(-38, -150), new(38, -4), false);
            UiKit.Skin(apSources.GetComponent<Image>(), "Wood");
            var sourceList = Label(apSources, "", 20, TextAnchor.MiddleLeft);
            sourceList.color = lightInk;
            Fill(sourceList.rectTransform, 14);
            var apTrack = Rect("本月行動力", ap);
            Anchor(apTrack, Vector2.zero, Vector2.right, new(18, 9), new(-18, 23));
            var pipRow = apTrack.gameObject.AddComponent<HorizontalLayoutGroup>();
            pipRow.spacing = 5;
            pipRow.childControlWidth = pipRow.childControlHeight = true;
            pipRow.childForceExpandWidth = pipRow.childForceExpandHeight = true;
            apHud.Initialize(apDisplay, sourceList, apTrack);
            targets["ap"] = ap;
            var end = Button(top, "結束本月", endMonth);
            targets["end_month"] = end;
            UiKit.Skin(end.GetComponent<Image>(), "Selected");
            var endLabel = end.GetComponentInChildren<Text>();
            endLabel.fontSize = endLabel.resizeTextMaxSize = 24;
            endLabel.resizeTextMinSize = UiKit.MinTitle;
            endLabel.color = UiKit.HeaderGold;
            endLabel.fontStyle = FontStyle.Bold;
            Anchor(end, Vector2.right, Vector2.one, new Vector2(-220, 10), new Vector2(-14, -10));

            var commands = Panel("Commands", canvas, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -365), new Vector2(458, -112));
            UiKit.Skin(commands.GetComponent<Image>(), "Xuan");
            this.commands = commands;
            commandList = ScrollList(commands, new Vector2(22, 22), new Vector2(-22, -22));

            details = Panel("Building details", canvas, Vector2.one, Vector2.one,
                new Vector2(-420, -665), new Vector2(-18, -112));
            UiKit.Skin(details.GetComponent<Image>(), "Xuan");
            detailsList = ScrollList(details, new Vector2(22, 22), new Vector2(-22, -76));
            var closeDetails = Button(details, "關閉", CloseBuilding);
            Anchor(closeDetails, Vector2.one, Vector2.one, new Vector2(-118, -60), new Vector2(-14, -12));
            CloseBuilding();

            var help = Panel("Controls", canvas, Vector2.zero, Vector2.zero,
                new Vector2(18, 18), new Vector2(550, 104), false);
            Fill(Label(help, "左鍵查看院中建築與弟子\nWASD 移動　滾輪縮放",
                22, TextAnchor.MiddleLeft).rectTransform, 12);

            // v1125 #21: notices are stacked, self-sizing slips that fade (Toast.cs).
            Toast.Initialize(canvas, font);

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
            UiKit.Skin(modalCard.GetComponent<Image>(), "Xuan");
            modalList = ScrollList(modalCard, new Vector2(32, 84), new Vector2(-32, -28));
            modalButtons = Rect("Report actions", modalCard);
            Anchor(modalButtons, Vector2.zero, Vector2.right, new Vector2(30, 22), new Vector2(-30, 72));
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

        /// <summary>Dark lacquer badge on the top bar (date, each resource, action points).</summary>
        private static RectTransform HudPlaque(RectTransform parent, string name, Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent);
            Anchor(rect, a, b, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            UiKit.Skin(image, "Wood");
            return rect;
        }

        private static void HudIcon(RectTransform parent, string icon, int offset)
        {
            var rect = Rect(icon + " icon", parent);
            Anchor(rect, new(0, .5f), new(0, .5f), new(offset, -19), new(offset + 38, 19));
            string mapped = icon switch
            {
                "Silver" => "hud_silver",
                "Food" => "hud_food",
                "Reputation" => "hud_rep",
                "Disciples" => "hud_disciple",
                "Month" => "attr_luk",
                _ => null
            };
            var painted = mapped != null ? UiKit.IconSprite(mapped) : null;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = painted ?? UiKit.Sprite(icon);
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
            resourceLabels[3].text = $"門人 {state.Disciples.Count}";
        }

        public void ShowCommands(string title, IEnumerable<(string label, Action run)> commands) =>
            ShowCommands(title, commands.Select(command => (command.label, (string)null, command.run, false)));

        /// <summary>
        /// Command / build menu of rich cards: a title-and-cost line plus a 1–2 line purpose / yield subtitle.
        /// A selected card (the building chosen for placement) is drawn with the highlighted frame.
        /// </summary>
        public void ShowCommands(string title, IEnumerable<(string label, string detail, Action run, bool selected)> commands)
        {
            Clear(commandList);
            commandsBack = null;
            SetCommandsPanelHeight(title == "建造" ? 800 : 780);
            AddLine(commandList, title == "指令" ? "門派事務" : title, 28);
            targets.Remove("cmd_build");
            targets.Remove("cmd_recruit");
            int index = 0;
            foreach (var command in commands)
            {
                var rect = CommandCard(commandList, command.label, command.detail, command.run, command.selected);
                if (title == "指令" && index < 2)
                    targets[index == 0 ? "cmd_build" : "cmd_recruit"] = rect;
                else if (title == "指令" && !command.selected)
                    UiKit.Skin(rect.GetComponent<Image>(), "Wood");
                index++;
            }
        }

        internal void ShowCommandCategories(
            (string name, string detail, (string label, string detail, Action run, bool selected)[] entries)[] categories)
        {
            DrawCategories();

            void DrawCategories()
            {
                Clear(commandList);
                SetCommandsPanelHeight(780);
                AddLine(commandList, "門派事務", 28);
                foreach (var category in categories)
                {
                    var entry = CommandCard(commandList, category.name, category.detail, () => Expand(category), false);
                    entry.GetComponent<LayoutElement>().preferredHeight = 96;
                    if (category.name == "建造") targets["cmd_build"] = entry;
                    if (category.name == "人事") targets["cmd_recruit"] = entry;
                }
            }

            void Expand((string name, string detail, (string label, string detail, Action run, bool selected)[] entries) category)
            {
                ShowCommands("門派事務・" + category.name, new[] { ("‹ 返回分類", "查看其他門派事務（右鍵亦可返回）", (Action)DrawCategories, false) }
                    .Concat(category.entries));
                commandsBack = DrawCategories; // v1125 #16: right-click / Esc returns to the categories
                if (commandList.childCount > category.entries.Length)
                    targets["cmd_back"] = (RectTransform)commandList.GetChild(commandList.childCount - category.entries.Length - 1);
                if (category.name == "建造" && commandList.childCount > 2)
                    targets["cmd_build"] = (RectTransform)commandList.GetChild(commandList.childCount - category.entries.Length);
                if (category.name == "人事" && commandList.childCount > 2)
                    targets["cmd_recruit"] = (RectTransform)commandList.GetChild(commandList.childCount - category.entries.Length);
            }
        }

        internal void OpenCommandCategoryForCapture(string category)
        {
            var button = commandList.GetComponentsInChildren<Button>()
                .FirstOrDefault(entry => entry.name == category);
            button?.onClick.Invoke();
        }

        private RectTransform CommandCard(RectTransform parent, string label, string detail, Action run, bool selected)
        {
            var rect = Button(parent, label, run);
            rect.GetComponent<Button>().interactable = run != null;
            if (selected)
            {
                UiKit.Skin(rect.GetComponent<Image>(), "Selected");
                var glow = rect.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(.55f, .95f, .55f, .9f);
                glow.effectDistance = new Vector2(3, 3);
            }
            if (string.IsNullOrEmpty(detail))
                return rect;
            string cmdIcon = CommandIconFor(label);
            var iconTex = UiKit.Icon(cmdIcon);
            int textLeft = iconTex != null ? 74 : 22;
            // v1125 #1: caption and body heights follow their wrapped text so nothing overlaps or is cut off.
            float cardWidth = Mathf.Max(200, parent.rect.width > 1 ? parent.rect.width : 396);
            if (iconTex != null)
            {
                var ic = Rect("CmdIcon", rect);
                Anchor(ic, Vector2.up, Vector2.up, new(16, -68), new(64, -20));
                AddImage(ic, iconTex);
            }
            var caption = rect.GetComponentInChildren<Text>();
            caption.alignment = TextAnchor.MiddleLeft;
            caption.fontSize = caption.resizeTextMaxSize = 22;
            caption.resizeTextMinSize = UiKit.MinBody;
            caption.resizeTextForBestFit = false;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.text = label.Replace(' ', ' ');
            float captionHeight = Mathf.Max(38, PreferredHeight(caption, cardWidth - textLeft - 10) + 10);
            Anchor(caption.rectTransform, Vector2.up, Vector2.one, new(textLeft, -captionHeight), new(-10, -6));
            var divider = Rect("Card rule", rect);
            Anchor(divider, Vector2.up, Vector2.one, new(textLeft, -captionHeight - 3), new(-14, -captionHeight - 2));
            var rule = divider.gameObject.AddComponent<Image>();
            rule.color = new Color(.90f, .72f, .40f, .45f);
            rule.raycastTarget = false;
            // No-break spaces: legacy Text only wraps at spaces, which strands "+10，解鎖…" runs on one line.
            var body = Label(rect, detail.Replace(' ', ' '), 18);
            body.color = new Color(1f, .86f, .62f);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.resizeTextForBestFit = false;
            body.lineSpacing = 1.05f;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            float bodyHeight = PreferredHeight(body, cardWidth - textLeft - 10);
            rect.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(84 + Mathf.Min(captionHeight, 58),
                captionHeight + bodyHeight + 20);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new(textLeft, 6), new(-10, -captionHeight - 6));
            UiKit.AccentStripe(rect, selected ? UiKit.GoldAccent : run == null ? new Color(.45f, .40f, .34f) : UiKit.Crimson);
            return rect;
        }

        private static string CommandIconFor(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            if (label == "人事") return "hud_disciple";
            if (label.Contains("探險")) return "realm_explore";
            if (label.Contains("外交")) return "faction_court";
            if (label.Contains("征伐") || label.Contains("地圖")) return "faction_sect";
            if (label.Contains("主角")) return "attr_cha";
            if (label.Contains("建造") || label.Contains("興建") || label.Contains("議事堂") || label.Contains("廂房")) return "skill_construction";
            if (label.Contains("招") || label.Contains("弟子")) return "hud_disciple";
            if (label.Contains("演武") || label.Contains("比武") || label.Contains("實戰") || label.Contains("戰鬥")) return "act_attack";
            if (label.Contains("練") || label.Contains("修習") || label.Contains("武學")) return "skill_sword";
            if (label.Contains("藏經") || label.Contains("研習") || label.Contains("功法")) return "item_manual_sword";
            if (label.Contains("藥") || label.Contains("療傷") || label.Contains("丹")) return "skill_medicine";
            if (label.Contains("鍛") || label.Contains("兵器")) return "skill_forging";
            if (label.Contains("伙房") || label.Contains("糧")) return "hud_food";
            if (label.Contains("茶寮") || label.Contains("打聽") || label.Contains("情報")) return "attr_wit";
            if (label.Contains("秘境")) return "realm_treasure";
            if (label.Contains("山道") || label.Contains("奇遇") || label.Contains("探索")) return "realm_explore";
            if (label.Contains("商") || label.Contains("銀")) return "hud_silver";
            return "act_skill";
        }

        private RectTransform mapButtons;
        private Action commandsBack;

        /// <summary>v1125 #16: steps the 門派事務 menu back to its categories. False when already at the top.</summary>
        internal bool CommandsBack()
        {
            if (commandsBack == null) return false;
            var back = commandsBack;
            commandsBack = null;
            back();
            return true;
        }

        internal bool CommandsInSubmenu => commandsBack != null;

        private static readonly string[] BackLabels = { "取消", "關閉", "收卷", "繼續遊戲", "繼續引導", "確定", "知道了" };

        /// <summary>
        /// v1125 #16: right-click / Esc on a modal page. Presses the page's own 「返回…」 button when it has one
        /// (one level up), otherwise its cancel / close / 收卷 button, otherwise closes a dismissible modal.
        /// Returns false when the page offers no way back (story choices, creation steps without 返回).
        /// </summary>
        internal bool ModalBack()
        {
            if (!ModalOpen) return false;
            var buttons = modalButtons.GetComponentsInChildren<Button>(false)
                .Where(button => button.interactable && button.gameObject.activeInHierarchy).ToArray();
            var pick = buttons.FirstOrDefault(button => button.name.TrimStart('‹', ' ').StartsWith("返回", StringComparison.Ordinal));
            foreach (string label in BackLabels)
                pick ??= buttons.FirstOrDefault(button => button.name == label);
            // Full-page panels (堂口, 演武, 政務) draw their own 返回 / 關閉 buttons on the card itself.
            pick ??= modalCard.GetComponentsInChildren<Button>(false).Where(button => button.interactable)
                .FirstOrDefault(button => Caption(button).StartsWith("返回", StringComparison.Ordinal) ||
                    button.name.EndsWith("返回", StringComparison.Ordinal));
            pick ??= modalCard.GetComponentsInChildren<Button>(false).Where(button => button.interactable)
                .FirstOrDefault(button => Caption(button) is "關閉" or "收卷" or "離開");
            if (pick != null)
            {
                pick.onClick.Invoke();
                return true;
            }
            if (!modalDismissible) return false;
            CloseModal();
            return true;

            static string Caption(Button button) =>
                (button.GetComponentInChildren<Text>()?.text ?? button.name).TrimStart('‹', ' ', '「');
        }

        /// <summary>Bottom-right wood plaque with the world map (M) and intel overview shortcuts.</summary>
        internal void ShowMapButtons(Action openMap, Action openIntel, Action openCharacters, Action openSystem, Action openSect = null)
        {
            if (mapButtons != null)
                return;
            mapButtons = Panel("江湖", canvas, Vector2.right, Vector2.right, new(openSect != null ? -1010 : -826, 18), new(-18, 92));
            UiKit.Skin(mapButtons.GetComponent<Image>(), "Wood");
            var row = mapButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 10, 10);
            row.spacing = 12;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            // v1125 #18: 「本門」 lists only our own people (hero, elders, seniors, disciples, the friend).
            if (openSect != null)
                targets["sect_roster"] = Button(mapButtons, "本門　B", openSect);
            var mapButton = Button(mapButtons, "江湖地圖　M", openMap);
            var intelButton = Button(mapButtons, "情報", openIntel);
            Button(mapButtons, "江湖人物　C", openCharacters);
            UiKit.Skin(mapButton.GetComponent<Image>(), "Wood");
            UiKit.Skin(Button(mapButtons, "系統　Esc", openSystem).GetComponent<Image>(), "Wood");
            targets["map"] = mapButton;
            targets["intel"] = intelButton;
        }

        private void SetCommandsPanelHeight(float height) => commands.offsetMin = new Vector2(18, -112 - height);

        public void ShowBuilding(BuildingData building, IEnumerable<(string label, Action run)> actions, int level = 1) =>
            ShowBuilding(building, actions.Select(action => (action.label, (string)null, action.run)), level);

        public void ShowBuilding(BuildingData building, IEnumerable<(string label, string detail, Action run)> actions, int level = 1)
        {
            Clear(detailsList);
            AddLine(detailsList, building.name, 28);
            AddLine(detailsList, $"等級 {level}");
            AddLine(detailsList, building.description);
            if (!string.IsNullOrEmpty(building.summary))
                AddLine(detailsList, "【用途】" + building.summary, 20);
            foreach (var action in actions)
                CommandCard(detailsList, action.label, action.detail, action.run, false);
            details.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailsList);
            float height = Mathf.Clamp(LayoutUtility.GetPreferredHeight(detailsList) + 86, 155, 780);
            Anchor(details, Vector2.one, Vector2.one, new Vector2(-420, -112 - height), new Vector2(-18, -112));
            detailsList.anchoredPosition = Vector2.zero;
        }

        /// <summary>What the 門派政務 page needs from the bootstrap to make its slips act (v1125 #9).</summary>
        internal sealed class LedgerHooks
        {
            public string incomeLine, apLine, netLine;
            public Action<BuildingData> openBuilding;
            public Action<DiscipleData> openDisciple;
            public Func<DiscipleData, string> status, life;
            public Action openHalls, openRoster;
        }

        /// <summary>
        /// v1125 #9: 門派政務 you can act on. Top: this month's income / upkeep and where the action points come
        /// from. Left: every building as a button that opens its card (upgrade / functions). Right: the disciple
        /// register — click a disciple to open their page (talk, 委任總管, assign to a hall, arrange training).
        /// </summary>
        internal void ShowSectLedger(GameState state, LedgerHooks hooks = null)
        {
            hooks ??= new LedgerHooks();
            var buttons = new List<(string, Action)>();
            if (hooks.openHalls != null) buttons.Add(("山門堂口　›", hooks.openHalls));
            if (hooks.openRoster != null) buttons.Add(("本門名冊　›", hooks.openRoster));
            buttons.Add(("收卷", () => CloseModal()));
            ShowModal("", Array.Empty<string>(), buttons);
            Anchor(modalCard, Vector2.one * .5f, Vector2.one * .5f, new(-700, -440), new(700, 440));
            characterDecoration = MapUi.Docket(modalCard, "門派政務", 0, 0, 1400, 794, false);
            var page = characterDecoration;
            MapUi.Text(page, $"{state.SectLabel}　門派政務", 38, 18, 760, 60, 40, MapUi.Gold);
            MapUi.Text(page, state.Date, 960, 24, 400, 50, 23, MapUi.Paper, TextAnchor.MiddleRight);

            // Monthly budget and action-point strip.
            var budget = MapUi.Kit(page, "每月收支", 30, 86, 1340, 96, "Wood", false);
            AddIconAt(budget, "hud_silver", 16, 12, 34);
            MapUi.Text(budget, hooks.incomeLine ?? $"銀兩 {state.Silver}　糧食 {state.Food}", 60, 6, 860, 42, 21, MapUi.Paper);
            MapUi.Text(budget, hooks.netLine ?? "", 60, 48, 860, 42, 21, MapUi.Gold);
            AddIconAt(budget, "hud_ap", 930, 12, 34);
            MapUi.Text(budget, hooks.apLine ?? $"行動力 {state.ActionPoints}／{state.ApMax}", 972, 6, 352, 84, 20, MapUi.Paper,
                TextAnchor.UpperLeft);

            var affairs = MapUi.Docket(page, "門務", 30, 194, 470, 572);
            MapUi.Text(affairs, "門務簿", 24, 14, 250, 56, 30, MapUi.Red);
            MapUi.Text(affairs, "點建築開啟功能", 230, 22, 216, 44, 18, MapUi.Ink, TextAnchor.MiddleRight);
            MapUi.Text(affairs, $"床位 {state.Beds}　弟子 {state.Disciples.Count}　聲望 {state.Reputation}",
                24, 72, 422, 40, 21, MapUi.Ink);
            var works = LedgerScroll(affairs, 22, 120, 426, 432);
            int workY = 0;
            foreach (var building in state.Buildings.Where(b => b != null).GroupBy(b => b.id))
            {
                var entry = building.First();
                var slip = MapUi.Button(works, "", 0, workY, 420, 58, () => hooks.openBuilding?.Invoke(entry));
                slip.name = entry.name;
                slip.interactable = hooks.openBuilding != null;
                MapUi.Text(slip.transform, entry.name, 15, 0, 240, 58, 22, MapUi.Paper);
                MapUi.Text(slip.transform, $"等級 {state.BuildingLevel(entry.id)}・{building.Count()} 處　›", 230, 0, 176, 58, 19,
                    MapUi.Gold, TextAnchor.MiddleRight);
                workY += 64;
            }
            works.sizeDelta = new Vector2(426, workY);

            var registry = MapUi.Docket(page, "弟子", 520, 194, 850, 572);
            MapUi.Text(registry, "弟子名冊", 24, 14, 300, 56, 30, MapUi.Red);
            MapUi.Text(registry, "點弟子：談話・委任總管・指派堂口・安排修練", 300, 22, 520, 44, 18, MapUi.Ink, TextAnchor.MiddleRight);
            var roster = LedgerScroll(registry, 20, 78, 810, 474);
            int memberY = 0;
            foreach (var member in state.Disciples)
            {
                var person = member;
                var slip = MapUi.Button(roster, "", 0, memberY, 804, 110, () => hooks.openDisciple?.Invoke(person));
                slip.name = member.name;
                slip.interactable = hooks.openDisciple != null;
                var rect = (RectTransform)slip.transform;
                MapUi.Portrait(rect, PortraitService.Resolve(PortraitService.IdOrDefault(member.portrait, member.id), member.gender),
                    12, 8, 72, 94);
                MapUi.Text(rect, member.name + "　" + member.title, 100, 6, 420, 36, 24, MapUi.Paper, role: "Names");
                MapUi.Text(rect, hooks.life?.Invoke(member) ?? member.trait, 100, 42, 450, 30, 18, new Color(.78f, .88f, .74f));
                string status = hooks.status?.Invoke(member);
                MapUi.Text(rect, string.IsNullOrEmpty(status) ? member.trait : status, 100, 72, 450, 30, 18,
                    string.IsNullOrEmpty(status) ? MapUi.Gold : new Color(.95f, .62f, .48f));
                var affinity = MapUi.Rect(rect, "好感", 580, 40, 26, 26).gameObject.AddComponent<RawImage>();
                affinity.texture = UiKit.Icon("hud_affinity");
                affinity.raycastTarget = false;
                MapUi.Text(rect, $"好感 {CastDialogue.Score(state, member)}", 612, 24, 120, 60, 21, MapUi.Paper);
                MapUi.Text(rect, "›", 748, 24, 40, 60, 30, MapUi.Gold, TextAnchor.MiddleCenter);
                memberY += 118;
            }
            roster.sizeDelta = new Vector2(810, memberY);
            FitModalButtons();

            RectTransform LedgerScroll(Transform parent, float x, float y, float width, float height)
            {
                var viewport = MapUi.Panel(parent, "卷內", x, y, width, height, new Color(.27f, .19f, .13f, .10f));
                viewport.gameObject.AddComponent<RectMask2D>();
                var contents = MapUi.Rect(viewport, "卷頁", 0, 0, width, 0);
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = contents;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 45;
                return contents;
            }

            void AddIconAt(Transform parent, string icon, float x, float y, float size)
            {
                var image = MapUi.Rect(parent, icon, x, y, size, size).gameObject.AddComponent<RawImage>();
                image.texture = UiKit.Icon(icon);
                image.enabled = image.texture != null;
                image.raycastTarget = false;
            }
        }

        public void ShowModal(string title, IEnumerable<string> lines,
            IEnumerable<(string label, Action run)> buttons = null, bool dismissible = true, Action closed = null)
        {
            CloseTalk(false); // v1125 #4: a page never opens under a lingering portrait talk
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
            var shade = Rect("Ink vignette", titleScreen);
            Fill(shade);
            AddImage(shade, UiKit.Vignette()).color = new Color(1, 1, 1, .85f);
            // Left-side ink wash so the banner reads against any illustration.
            var wash = Rect("Ink wash", titleScreen);
            Anchor(wash, Vector2.zero, new(.48f, 1), Vector2.zero, Vector2.zero);
            AddImage(wash, InkWash()).color = new Color(1, 1, 1, .9f);

            // Zongmen-Origin style vertical banner: dark ink lacquer, double antique-gold trim, seal, calligraphy.
            var banner = Panel("Title banner", titleScreen, new(.24f, .5f), new(.24f, .5f),
                new(-300, -430), new(300, 430));
            var bannerImage = banner.GetComponent<Image>();
            UiKit.Skin(bannerImage, "Lacquer");
            bannerImage.color = new Color(1, 1, 1, .93f);
            GoldFrame(banner, 16, 2, UiKit.TitleGold);
            GoldFrame(banner, 22, 1, new Color(.78f, .66f, .42f, .6f));

            var seal = Panel("Title seal", banner, new(.5f, 1), new(.5f, 1), new(-130, -104), new(130, -54), false);
            UiKit.Skin(seal.GetComponent<Image>(), "Seal");
            var sealText = Label(seal, "【開元武林錄】", 24, TextAnchor.MiddleCenter);
            sealText.color = new Color(1f, .91f, .74f);
            sealText.font = UiKit.Font("Names", font, sealText.text);
            Fill(sealText.rectTransform, 4);

            var heading = Label(banner, string.Join(" ", title.ToCharArray()), 48, TextAnchor.MiddleCenter);
            heading.fontStyle = FontStyle.Bold;
            heading.font = UiKit.Font("Titles", font, title);
            heading.color = new Color(.97f, .90f, .72f);
            heading.resizeTextForBestFit = false;
            var headingShadow = heading.gameObject.AddComponent<Shadow>();
            headingShadow.effectColor = new Color(0, 0, 0, .85f);
            headingShadow.effectDistance = new Vector2(2, -3);
            var headingGlow = heading.gameObject.AddComponent<Outline>();
            headingGlow.effectColor = new Color(.78f, .60f, .30f, .45f);
            Anchor(heading.rectTransform, new(0, 1), Vector2.one, new(30, -196), new(-30, -118));

            var subtitle = Label(banner, "「崤函風雲起・一劍開宗門」", 20, TextAnchor.MiddleCenter);
            subtitle.color = UiKit.TitleGold;
            subtitle.font = UiKit.Font("Narration", font, subtitle.text);
            Anchor(subtitle.rectTransform, new(0, 1), Vector2.one, new(30, -238), new(-30, -204));

            var divider = Rect("Divider", banner);
            Anchor(divider, new(0, 1), Vector2.one, new(70, -262), new(-70, -250));
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.sprite = UiKit.Sprite("Divider");
            dividerImage.color = UiKit.TitleGold;
            dividerImage.raycastTarget = false;

            AddTitleButton("踏入江湖", "新遊戲", start, 0);
            AddTitleButton("再續前緣", "繼續遊玩", resume, 1);
            AddTitleButton("百器天工", "系統設定", ShowAudioOptions, 2);
            AddTitleButton("歸隱山林", "退出遊戲", () => ConfirmExit(CloseModalFromMenu), 3);

            var footer = Label(banner, "開元元年・崤函之間", 18, TextAnchor.MiddleCenter);
            footer.color = new Color(.70f, .60f, .44f);
            Anchor(footer.rectTransform, Vector2.zero, Vector2.right, new(30, 34), new(-30, 64));

            void AddTitleButton(string text, string note, Action run, int index)
            {
                float top = -310 - index * 84;
                var item = Button(banner, text, run);
                item.name = text;
                Anchor(item, new(0, 1), Vector2.one, new(64, top - 56), new(-64, top));
                var plate = item.GetComponent<Image>();
                UiKit.Skin(plate, "TitleMenu");
                var button = item.GetComponent<Button>();
                button.interactable = run != null;
                var colors = button.colors;
                colors.highlightedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(.85f, .78f, .70f);
                colors.disabledColor = new Color(.55f, .52f, .50f, .85f);
                button.colors = colors;
                var caption = item.GetComponentInChildren<Text>();
                caption.supportRichText = true;
                caption.text = $"{text}<color=#B99D6B>　（{note}）</color>";
                caption.fontSize = caption.resizeTextMaxSize = 26;
                caption.resizeTextMinSize = UiKit.MinTitle;
                caption.color = new Color(.98f, .91f, .76f);
                caption.alignment = TextAnchor.MiddleLeft;
                caption.font = UiKit.Font("Names", font, text + note + "（）　");
                Anchor(caption.rectTransform, Vector2.zero, Vector2.one, new(46, 2), new(-16, -2));
                var glow = Rect("Hover glow", item);
                glow.SetAsFirstSibling();
                Fill(glow, -6);
                var glowImage = glow.gameObject.AddComponent<Image>();
                glowImage.sprite = null;
                glowImage.color = new Color(.95f, .76f, .38f, 0);
                glowImage.raycastTarget = false;
                item.gameObject.AddComponent<HoverGlow>().Initialize(glowImage, caption);
            }
        }

        /// <summary>Rectangular gold rule inset inside <paramref name="parent"/> (four thin bars).</summary>
        private static void GoldFrame(RectTransform parent, float inset, float thickness, Color color)
        {
            for (int side = 0; side < 4; side++)
            {
                var bar = Rect("Gold rule", parent);
                if (side < 2)
                    Anchor(bar, new(0, side), new(1, side), new(inset, side == 0 ? inset : -inset - thickness),
                        new(-inset, side == 0 ? inset + thickness : -inset));
                else
                    Anchor(bar, new(side - 2, 0), new(side - 2, 1), new(side == 2 ? inset : -inset - thickness, inset),
                        new(side == 2 ? inset + thickness : -inset, -inset));
                var image = bar.gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
        }

        private static Texture2D inkWash;

        /// <summary>Horizontal ink gradient: deep ink on the left fading to clear, behind the title banner.</summary>
        private static Texture2D InkWash()
        {
            if (inkWash != null) return inkWash;
            inkWash = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++)
                inkWash.SetPixel(x, 0, new Color(.04f, .035f, .03f, Mathf.SmoothStep(.78f, 0, x / 63f)));
            inkWash.Apply(false, true);
            return inkWash;
        }

        public void CloseTitle()
        {
            if (titleScreen == null)
                return;
            titleScreen.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(titleScreen.gameObject);
            titleScreen = null;
        }

        private void CloseModalFromMenu() => CloseModal();

        internal void ShowSystemMenu() => ShowAudioOptions(true);

        public void ShowAudioOptions() => ShowAudioOptions(false);

        private void ShowAudioOptions(bool system)
        {
            ShowModal(system ? "系統選單" : "音量設定", Array.Empty<string>(), system
                ? new (string, Action)[] { ("繼續遊戲", CloseModalFromMenu), ("退出遊戲", () => ConfirmExit(ShowSystemMenu)) }
                : null);
            Anchor(modalCard, Vector2.one * .5f, Vector2.one * .5f, new(-352, -234), new(352, 234));
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one, new(30, 82), new(-30, -26));
            if (system)
            {
                AddLine(modalList, "音量", 24);
                foreach (Transform action in modalButtons)
                    if (action.gameObject.activeSelf && action.name == "繼續遊戲") UiKit.Skin(action.GetComponent<Image>(), "Wood");
            }
            AddVolume("主音量", GameAudioPrefs.MasterVolume, value => GameAudioPrefs.Master = value);
            AddVolume("音樂", GameAudioPrefs.Music, value => GameAudioPrefs.Music = value);
            AddVolume("音效", GameAudioPrefs.Sfx, value => GameAudioPrefs.Sfx = value);
            ToggleMute();

            void ToggleMute()
            {
                Button(modalList, GameAudioPrefs.Muted ? "取消靜音" : "靜音", () =>
                {
                    GameAudioPrefs.Muted = !GameAudioPrefs.Muted;
                    ShowAudioOptions(system);
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

        internal void ConfirmExit(Action cancel)
        {
            ShowModal("退出遊戲", new[] { "確定要離開江湖？尚未儲存的進度將會遺失。" },
                new (string, Action)[] { ("取消", CloseModalFromMenu), ("確認退出", QuitGame) }, closed: cancel);
            Anchor(modalCard, Vector2.one * .5f, Vector2.one * .5f, new(-338, -146), new(338, 146));
            foreach (Transform action in modalButtons)
                if (action.gameObject.activeSelf && action.name == "取消") UiKit.Skin(action.GetComponent<Image>(), "Wood");
        }

        internal bool ActivateMenuAction(string label)
        {
            foreach (Transform action in modalButtons)
            {
                if (!action.gameObject.activeInHierarchy || action.name != label) continue;
                action.GetComponent<Button>().onClick.Invoke();
                return true;
            }
            return false;
        }

        private static void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
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

        /// <summary>v1125 #21: shows a fading notice slip; an empty message is ignored (slips expire on their own).</summary>
        public void Message(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || message == lastMessage && Toast.Count > 0) return;
            lastMessage = message;
            Toast.Show(message);
        }

        /// <summary>Notice with an explicit icon and quality colour (0 凡品 … 3 神兵).</summary>
        internal void Message(string message, string icon, int quality = 0)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            lastMessage = message;
            Toast.Show(message, icon, Toast.QualityColor(quality));
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
            worldLabels.Add((target, offset, tag, building));
            tag.gameObject.SetActive(building);
        }

        internal void RemoveWorldLabel(Transform target)
        {
            int index = worldLabels.FindIndex(entry => entry.target == target);
            if (index < 0) return;
            UnityEngine.Object.Destroy(worldLabels[index].tag.gameObject);
            worldLabels.RemoveAt(index);
        }

        public void PositionLabels(Camera camera, Transform hovered = null, Transform selected = null)
        {
            Camera projection = Canvas.renderMode == RenderMode.ScreenSpaceCamera ? camera : null;
            visibleTags.Clear();
            foreach (var entry in worldLabels.OrderBy(entry => entry.target == selected ? 0 : entry.target == hovered ? 1 : 2))
            {
                Vector3 screen = camera.WorldToScreenPoint(entry.target.position + entry.offset);
                bool showTag = entry.building ? screen.z < (entry.offset.y < 2.7f ? 39 : 57) :
                    entry.target == hovered || entry.target == selected;
                bool visible = entry.target.gameObject.activeInHierarchy && showTag && screen.z > 0 &&
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
            label.resizeTextMinSize = UiKit.MinBody;
            label.resizeTextMaxSize = 22;
            Fill(label.rectTransform, 4);
            return rect;
        }

        private Text Label(RectTransform parent, string content, int size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            size = UiKit.Readable(size);
            var text = Rect("Text", parent).gameObject.AddComponent<Text>();
            text.font = UiKit.Font(size >= 28 ? "Titles" : "Interface", font, content);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Ink;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = UiKit.MinBody;
            text.resizeTextMaxSize = size;
            // With the 18px floor a label may not shrink to fit; let it draw rather than silently truncate.
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
