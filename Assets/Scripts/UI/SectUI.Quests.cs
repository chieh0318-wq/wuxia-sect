using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// Pins the current objective under the resource bar. During the guide it sits above the bottom bar,
    /// leaving room for the guide card. The bottom-bar quest button opens the full board.
    /// </summary>
    public sealed partial class SectUI
    {
        private RectTransform questPanel;
        private bool questCollapsed;
        private GameState questState;

        /// <summary>Set by the bootstrap: the running guide step (title, objective), or null when the guide is over.</summary>
        internal Func<(string title, string objective)?> TutorialQuest;

        internal void ShowQuestPanel(GameState state)
        {
            questState = state;
            if (questPanel != null)
            {
                RefreshQuestPanel();
                return;
            }
            questPanel = Panel("任務", canvas, Vector2.right, Vector2.right, new(-442, 104), new(-18, 144));
            UiKit.Skin(questPanel.GetComponent<Image>(), "Lacquer");
            questPanel.gameObject.AddComponent<QuestPanelWatcher>().Hidden = () =>
                ModalOpen || StoryOpen || questBook != null || (details != null && details.gameObject.activeSelf);
            RefreshQuestPanel();
        }

        internal void RefreshQuestPanel()
        {
            if (questBook != null) RefreshQuestBook();
            if (questPanel == null || questState == null) return;
            foreach (Transform child in questPanel)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            const float width = 424;

            var header = Button(questPanel, "", () => { questCollapsed = !questCollapsed; RefreshQuestPanel(); });
            header.name = "任務標題";
            UiKit.Skin(header.GetComponent<Image>(), "Plaque");
            header.GetComponentInChildren<Text>().text = "";
            // v1126c PC3 #10: the pinned box is only 「眼下要務」; the whole board lives behind the 「任務　J」 button.
            var caption = Label(header, questCollapsed ? "眼下要務　▸" : "眼下要務　▾", 22, TextAnchor.MiddleLeft);
            caption.color = UiKit.HeaderGold;
            caption.fontStyle = FontStyle.Bold;
            Anchor(caption.rectTransform, Vector2.zero, Vector2.one, new(14, 0), new(-10, 0));
            var stage = QuestBoard.Current(questState);
            var guide = TutorialQuest?.Invoke();
            var peek = Label(header, guide.HasValue ? "引導：" + guide.Value.title : stage != null ? stage.title : "諸事已備",
                18, TextAnchor.MiddleRight);
            peek.color = MapUi.Paper;
            Anchor(peek.rectTransform, Vector2.zero, Vector2.one, new(150, 0), new(-14, 0));
            Anchor(header, Vector2.up, Vector2.one, new(0, -44), Vector2.zero);
            if (questCollapsed)
            {
                Resize(44);
                return;
            }
            float y = WriteQuestBody(questPanel, width, 44, false, 0);
            Resize(y + 12);

            void Resize(float height)
            {
                if (guide.HasValue)
                    Anchor(questPanel, Vector2.right, Vector2.right, new(-width - 18, 104), new(-18, 104 + height));
                else
                    Anchor(questPanel, Vector2.one, Vector2.one, new(-width - 18, -112 - height), new(-18, -112));
            }
        }

        // ── v1126c PC3 #10: the full 任務 board (bottom-bar 「任務　J」) ──
        private RectTransform questBook;
        private object questBookBack;
        internal bool QuestBookOpen => questBook != null;

        internal void ToggleQuestBook()
        {
            if (questBook != null)
            {
                CloseQuestBook();
                return;
            }
            if (questState == null) return;
            questBook = Rect("任務總覽", canvas);
            Anchor(questBook, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dim = questBook.gameObject.AddComponent<Image>();
            dim.color = new Color(.08f, .055f, .034f, .45f); // blocks clicks on the courtyard behind
            var watch = questBook.gameObject.AddComponent<QuestBookWatcher>();
            watch.Close = () => ModalOpen || StoryOpen || SceneOpen;
            watch.Run = CloseQuestBook;
            questBookBack = UiBack.Push(CloseQuestBook, () => questBook != null, questBook);
            RefreshQuestBook();
        }

        internal void CloseQuestBook()
        {
            if (questBook == null) return;
            UiBack.Pop(questBookBack);
            UnityEngine.Object.Destroy(questBook.gameObject);
            questBook = null;
        }

        private void RefreshQuestBook()
        {
            if (questBook == null || questState == null) return;
            foreach (Transform child in questBook)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            const float width = 860;
            var card = Rect("任務卷", questBook);
            UiKit.Skin(card.gameObject.AddComponent<Image>(), "Lacquer");
            var title = Label(card, "任務", 30, TextAnchor.MiddleLeft);
            title.color = UiKit.HeaderGold;
            title.fontStyle = FontStyle.Bold;
            Anchor(title.rectTransform, Vector2.up, Vector2.one, new(24, -56), new(-24, -10));
            var hint = Label(card, "按 J 或 Esc／右鍵收起", 17, TextAnchor.MiddleRight);
            hint.color = CardMuted;
            Anchor(hint.rectTransform, Vector2.up, Vector2.one, new(24, -56), new(-150, -10));
            var close = Button(card, "收卷", CloseQuestBook);
            close.name = "任務收卷";
            Anchor(close, Vector2.one, Vector2.one, new(-128, -52), new(-20, -14));
            float y = WriteQuestBody(card, width, 64, true, 2);
            float height = Mathf.Min(y + 18, 1000);
            Anchor(card, new(.5f, .5f), new(.5f, .5f), new(-width / 2, -height / 2 + 20), new(width / 2, height / 2 + 20));
            var fit = card.gameObject.AddComponent<FitToScreenV1126cPc>();
            fit.Height = height;
        }

        /// <summary>
        /// Writes 眼下要務 (always) and, when <paramref name="full"/>, the four 破關之路 with their live counters and
        /// 「宣告結局」 buttons into <paramref name="host"/> from <paramref name="y"/> down; returns the bottom edge.
        /// </summary>
        private float WriteQuestBody(RectTransform host, float width, float y, bool full, int bump)
        {
            var data = QuestBoardData.Data;
            const float pad = 16;
            var stage = QuestBoard.Current(questState);
            var guide = TutorialQuest?.Invoke();
            var met = new Color(.64f, .92f, .72f);

            if (full) Section("眼下要務");
            else y += 6; // the pinned box's header already says 眼下要務
            if (guide.HasValue)
            {
                Line(guide.Value.title, 21 + bump, UiKit.HeaderGold, 30 + bump);
                Line(guide.Value.objective, 18 + bump, MapUi.Paper, Wrapped(guide.Value.objective, 18 + bump));
                Line("新手引導進行中，完成後接續門派要務。", 17 + bump, CardMuted, 26 + bump);
            }
            else if (stage != null)
            {
                Line(stage.title, 21 + bump, UiKit.HeaderGold, 30 + bump);
                if (!string.IsNullOrEmpty(stage.brief)) Line(stage.brief, 18 + bump, MapUi.Paper, Wrapped(stage.brief, 18 + bump));
                foreach (var (text, current, target) in QuestBoard.Steps(questState, stage))
                {
                    bool done = current >= target;
                    string step = $"{(done ? "✔" : "・")} {text}　{current}／{target}";
                    Line(step, 18 + bump, done ? met : MapUi.Paper, Wrapped(step, 18 + bump));
                }
            }
            else Line("短期要務都已辦妥，往長遠的路走吧。", 18 + bump, MapUi.Paper, 28 + bump);

            if (!full) return y;

            y += 6;
            Section("常駐任務・破關之路");
            foreach (var route in data.routes ?? Array.Empty<QuestRoute>())
            {
                if (route == null) continue;
                string progress = QuestBoard.RouteProgress(questState, route);
                // v1126b 決定 3: the live counters wrap under the route name; a met route offers 「宣告結局」.
                string routeId = QuestBoard.RouteId(route);
                var live = routeId == null ? (RouteState?)null : questState.Route(routeId);
                float nameTop = y;
                Line(route.name, 19 + bump, UiKit.HeaderGold, 26 + bump);
                if (live is { Met: true, Declared: false })
                {
                    var declare = Button(host, "宣告結局", () => DeclareRoute(routeId));
                    declare.name = "宣告結局";
                    declare.GetComponentInChildren<Text>().fontSize = 16;
                    Anchor(declare, Vector2.one, Vector2.one, new(-pad - 104, -nameTop - 26), new(-pad, -nameTop - 1));
                }
                Line(progress, 16 + bump, live is { Met: true } ? met : progress.StartsWith("達成條件：待定", StringComparison.Ordinal) ? CardMuted : MapUi.Paper,
                    Wrapped(progress, 16 + bump), pad + 14);
                if (!string.IsNullOrEmpty(route.direction))
                    Line(route.direction, 15 + bump, CardMuted, Wrapped(route.direction, 15 + bump), pad + 14);
            }
            return y;

            void Section(string text)
            {
                var label = Label(host, text, 18 + bump, TextAnchor.MiddleLeft);
                label.color = UiKit.GoldAccent;
                Anchor(label.rectTransform, Vector2.up, Vector2.one, new(pad, -y - 26 - bump), new(-pad, -y));
                var rule = Rect("Rule", host);
                Anchor(rule, Vector2.up, Vector2.one, new(pad, -y - 28 - bump), new(-pad, -y - 27 - bump));
                var ink = rule.gameObject.AddComponent<Image>();
                ink.color = new Color(.93f, .74f, .42f, .35f);
                ink.raycastTarget = false;
                y += 32 + bump;
            }
            void Line(string text, int size, Color color, float height, float left = pad)
            {
                var label = Label(host, text ?? "", size, TextAnchor.UpperLeft);
                label.color = color;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.resizeTextForBestFit = false;
                Anchor(label.rectTransform, Vector2.up, Vector2.one, new(left, -y - height), new(-pad, -y));
                y += height;
            }
            float Wrapped(string text, int size)
            {
                int perLine = Mathf.Max(1, Mathf.FloorToInt((width - pad * 2 - 14) / size));
                return Mathf.Max(1, Mathf.CeilToInt((text ?? "").Length / (float)perLine)) * (size + 6) + 4;
            }
        }
        /// <summary>v1126b 決定 3: proclaims a route's ending; play carries on afterwards.</summary>
        private void DeclareRoute(string id)
        {
            if (questState == null || !questState.DeclareRoute(id)) return;
            AudioManager.Jingle("victory");
            string title = id switch { "diplomacy" => "江湖盟主", "conquest" => "一統江湖", "wealth" => "富甲天下", _ => "天下第一" };
            string sect = questState.SectLabel;
            string body = id switch
            {
                "diplomacy" => $"{sect}不動干戈，令群雄歸心、各派奉為上宗。眾人齊聚山門，共推掌門為江湖盟主。",
                "conquest" => $"{sect}兵鋒所指，各處據點盡入門下。四方豪強俯首，江湖從此以{sect}號令為準。",
                "wealth" => $"{sect}商路通達四海，庫中金銀如山。天下行商皆仰{sect}鼻息，連朝廷也要禮讓三分。",
                _ => $"掌門遍訪天下名家，一一折服。自此江湖論劍，再無人敢與{sect}掌門爭這「天下第一」四字。"
            };
            ShowModal(title, new[] { body, "", questState.Route(id).Progress, "", "（遊戲可繼續進行，其餘破關之路仍可再走。）" },
                new[] { ("受此重任", (Action)(() => { CloseModal(); RefreshQuestPanel(); })) });
        }

        internal void HideQuestPanel()
        {
            if (questPanel == null) return;
            UnityEngine.Object.Destroy(questPanel.gameObject);
            questPanel = null;
        }
    }

    /// <summary>Fades the 任務 panel out (and lets clicks through) while something else needs that corner.</summary>
    internal sealed class QuestPanelWatcher : MonoBehaviour
    {
        internal Func<bool> Hidden;
        private CanvasGroup group;

        private void LateUpdate()
        {
            group ??= gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            bool hide = Hidden?.Invoke() == true;
            group.alpha = hide ? 0 : 1;
            group.blocksRaycasts = !hide;
        }
    }

    /// <summary>v1126c PC3 #10: closes the 任務 board when a page, story beat or scene takes over.</summary>
    internal sealed class QuestBookWatcher : MonoBehaviour
    {
        internal Func<bool> Close;
        internal Action Run;
        private void LateUpdate()
        {
            if (Close?.Invoke() == true) Run?.Invoke();
        }
    }

    /// <summary>v1126c PC3 #10: shrinks a fixed-height card so it never runs off a short screen.</summary>
    internal sealed class FitToScreenV1126cPc : MonoBehaviour
    {
        internal float Height;
        private void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null || Height <= 0) return;
            float room = parent.rect.height - 40;
            float scale = Mathf.Min(1f, room / Height);
            transform.localScale = new Vector3(scale, scale, 1);
        }
    }
}