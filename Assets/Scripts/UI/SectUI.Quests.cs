using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126 #8 (T-3 #8): the 任務 panel, docked bottom-right above the 本門 / 地圖 buttons. The header toggles it.
    /// Top: one short-term goal at a time (the new-player guide while it runs, then the stages in quests.json);
    /// bottom, always pinned: the four long-term routes. It steps aside while a building card or a page is open.
    /// </summary>
    public sealed partial class SectUI
    {
        private RectTransform questPanel, questBody;
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
                ModalOpen || StoryOpen || (details != null && details.gameObject.activeSelf);
            RefreshQuestPanel();
        }

        internal void RefreshQuestPanel()
        {
            if (questPanel == null || questState == null) return;
            foreach (Transform child in questPanel)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            var data = QuestBoardData.Data;
            const float width = 424, pad = 16;
            float y = 0;

            var header = Button(questPanel, "", () => { questCollapsed = !questCollapsed; RefreshQuestPanel(); });
            header.name = "任務標題";
            UiKit.Skin(header.GetComponent<Image>(), "Plaque");
            header.GetComponentInChildren<Text>().text = "";
            var caption = Label(header, questCollapsed ? "任務　▸" : "任務　▾", 22, TextAnchor.MiddleLeft);
            caption.color = UiKit.HeaderGold;
            caption.fontStyle = FontStyle.Bold;
            Anchor(caption.rectTransform, Vector2.zero, Vector2.one, new(14, 0), new(-10, 0));
            var stage = QuestBoard.Current(questState);
            var guide = TutorialQuest?.Invoke();
            var peek = Label(header, guide.HasValue ? "引導：" + guide.Value.title : stage != null ? stage.title : "諸事已備",
                18, TextAnchor.MiddleRight);
            peek.color = MapUi.Paper;
            Anchor(peek.rectTransform, Vector2.zero, Vector2.one, new(110, 0), new(-14, 0));
            y = 44;
            if (questCollapsed)
            {
                Anchor(header, Vector2.up, Vector2.one, new(0, -44), Vector2.zero);
                Resize(44);
                return;
            }
            float top = 0;
            Place(header, 44);

            // ── short-term: one goal at a time ──
            Section("眼下要務");
            if (guide.HasValue)
            {
                Line(guide.Value.title, 21, UiKit.HeaderGold, 30);
                Line(guide.Value.objective, 18, MapUi.Paper, Wrapped(guide.Value.objective, 18));
                Line("新手引導進行中，完成後接續門派要務。", 17, CardMuted, 26);
            }
            else if (stage != null)
            {
                Line(stage.title, 21, UiKit.HeaderGold, 30);
                if (!string.IsNullOrEmpty(stage.brief)) Line(stage.brief, 18, MapUi.Paper, Wrapped(stage.brief, 18));
                foreach (var (text, current, target) in QuestBoard.Steps(questState, stage))
                {
                    bool done = current >= target;
                    Line($"{(done ? "✔" : "・")} {text}　{current}／{target}", 18, done ? new Color(.64f, .92f, .72f) : MapUi.Paper, 26);
                }
            }
            else Line("短期要務都已辦妥，往長遠的路走吧。", 18, MapUi.Paper, 28);

            // ── long-term: the four routes, pinned at the bottom ──
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
                Line(route.name, 19, UiKit.HeaderGold, 26);
                if (live is { Met: true, Declared: false })
                {
                    var declare = Button(questPanel, "宣告結局", () => DeclareRoute(routeId));
                    declare.name = "宣告結局";
                    declare.GetComponentInChildren<Text>().fontSize = 16;
                    Anchor(declare, Vector2.one, Vector2.one, new(-pad - 104, -nameTop - 26), new(-pad, -nameTop - 1));
                }
                var met = new Color(.64f, .92f, .72f);
                Line(progress, 16, live is { Met: true } ? met : progress.StartsWith("達成條件：待定", StringComparison.Ordinal) ? CardMuted : MapUi.Paper,
                    Wrapped(progress, 16), pad + 14);
                if (!string.IsNullOrEmpty(route.direction))
                    Line(route.direction, 15, CardMuted, Wrapped(route.direction, 15), pad + 14);
            }
            Resize(y + 16);

            void Place(RectTransform rect, float height)
            {
                Anchor(rect, Vector2.up, Vector2.one, new(0, -top - height), new(0, -top));
                top += height;
            }
            void Section(string text)
            {
                var label = Label(questPanel, text, 18, TextAnchor.MiddleLeft);
                label.color = UiKit.GoldAccent;
                Anchor(label.rectTransform, Vector2.up, Vector2.one, new(pad, -y - 26), new(-pad, -y));
                var rule = Rect("Rule", questPanel);
                Anchor(rule, Vector2.up, Vector2.one, new(pad, -y - 28), new(-pad, -y - 27));
                var ink = rule.gameObject.AddComponent<Image>();
                ink.color = new Color(.93f, .74f, .42f, .35f);
                ink.raycastTarget = false;
                y += 32;
            }
            void Line(string text, int size, Color color, float height, float left = pad)
            {
                var label = Label(questPanel, text ?? "", size, TextAnchor.UpperLeft);
                label.color = color;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.resizeTextForBestFit = false;
                Anchor(label.rectTransform, Vector2.up, Vector2.one, new(left, -y - height), new(-pad, -y));
                y += height;
            }
            float Wrapped(string text, int size)
            {
                int perLine = Mathf.Max(1, Mathf.FloorToInt((width - pad * 2) / size));
                return Mathf.Max(1, Mathf.CeilToInt((text ?? "").Length / (float)perLine)) * (size + 6) + 4;
            }
            void Resize(float height)
            {
                Anchor(questPanel, Vector2.right, Vector2.right, new(-width - 18, 104), new(-18, 104 + height));
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
}
