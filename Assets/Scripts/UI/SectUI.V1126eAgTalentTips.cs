using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126e-ag (Item 3): hover + click tooltips for talent chips across character cards, dossier,
    /// recruit cards, recruit picker, and creation picked-talent dropdown. Reads all numbers from
    /// <see cref="TalentCatalog"/> and <see cref="EffectText"/> without duplicating balance values.
    /// </summary>
    public sealed partial class SectUI
    {
        private static CreationOption[] cachedTalentCatalog;
        private RectTransform talentTip;
        private Text talentTipText;
        private RectTransform talentTipPinnedTarget;

        private static CreationOption[] TalentCatalogItems =>
            cachedTalentCatalog ??= M2Data.Load<TalentCatalog>("talents")?.talents ?? Array.Empty<CreationOption>();

        internal static CreationOption FindTalent(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName)) return null;
            return TalentCatalogItems.FirstOrDefault(o => o != null && o.id == idOrName)
                ?? TalentCatalogItems.FirstOrDefault(o => o != null && o.name == idOrName);
        }

        /// <summary>Formats a single talent's name, rarity, exact numeric effects, and description from live catalog data.</summary>
        internal static string TalentTipText(CreationOption option, string fallbackId = null)
        {
            if (option == null)
                option = FindTalent(fallbackId);
            if (option == null)
                return string.IsNullOrWhiteSpace(fallbackId) ? "" : $"【{fallbackId}】";

            string tag = option.negative ? "（負面）" : option.rarity switch
            {
                "legendary" => "（傳奇）",
                "rare" => "（稀有）",
                _ => ""
            };
            var lines = new List<string> { $"【{option.name}】{tag}" };

            string effectLine = "";
            if (option.effects != null && option.effects.Length > 0)
                effectLine = EffectText.Describe(option.effects);
            else if (!string.IsNullOrWhiteSpace(option.summary))
                effectLine = option.summary.Trim();
            else if (!string.IsNullOrWhiteSpace(option.desc))
                effectLine = option.desc.Trim();

            if (!string.IsNullOrWhiteSpace(effectLine))
                lines.Add("效果：" + effectLine);

            if (!string.IsNullOrWhiteSpace(option.desc) && !string.Equals(option.desc.Trim(), effectLine, StringComparison.Ordinal))
                lines.Add(option.desc.Trim());

            return string.Join("\n", lines);
        }

        internal static string TalentTipText(string talentId) => TalentTipText(FindTalent(talentId), talentId);

        internal static string TalentListTipText(IEnumerable<string> talentIds)
        {
            var ids = (talentIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
            if (ids.Length == 0) return "尚無天賦";
            return string.Join("\n\n", ids.Select(TalentTipText).Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>Attaches both hover and click tooltip handlers to a talent chip or cell.</summary>
        internal void AttachTalentTip(RectTransform target, Func<string> getText)
        {
            if (target == null || getText == null) return;
            var graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                var hit = target.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                graphic = hit;
            }
            graphic.raycastTarget = true;

            var trigger = target.gameObject.GetComponent<EventTrigger>() ?? target.gameObject.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                if (talentTipPinnedTarget == null || talentTipPinnedTarget == target)
                    ShowTalentTip(target, getText(), pinned: talentTipPinnedTarget == target);
            });

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                if (talentTipPinnedTarget != target)
                    HideTalentTip();
            });

            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(data =>
            {
                if (data is PointerEventData ped && ped.button != PointerEventData.InputButton.Left) return;
                if (talentTipPinnedTarget == target && talentTip != null && talentTip.gameObject.activeSelf)
                    HideTalentTip();
                else
                    ShowTalentTip(target, getText(), pinned: true);
            });

            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
            trigger.triggers.Add(click);
        }

        /// <summary>Hook for CreationPicksDropdown rows: looks up talent by name, falling back to the row's detail string.</summary>
        internal void AttachTalentTipByName(RectTransform target, string name, string detail)
        {
            if (target == null || string.IsNullOrWhiteSpace(name)) return;
            AttachTalentTip(target, () =>
            {
                var found = FindTalent(name);
                if (found != null) return TalentTipText(found);
                return string.IsNullOrWhiteSpace(detail) ? $"【{name}】" : $"【{name}】\n效果：{detail}";
            });
        }

        internal void HideTalentTip()
        {
            talentTipPinnedTarget = null;
            if (talentTip != null) talentTip.gameObject.SetActive(false);
        }

        private void ShowTalentTip(RectTransform target, string text, bool pinned)
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            if (canvas == null || string.IsNullOrWhiteSpace(text)) return;
            var root = modal != null && target.IsChildOf(modal) ? modal : (RectTransform)canvas.rootCanvas.transform;

            if (talentTip == null || talentTip.parent != root)
            {
                if (talentTip != null) UnityEngine.Object.Destroy(talentTip.gameObject);
                talentTip = Panel("天賦提示", root, new(.5f, .5f), new(.5f, .5f), Vector2.zero, new(440, 140), false);
                UiKit.Skin(talentTip.GetComponent<Image>(), "Lacquer");
                talentTip.GetComponent<Image>().raycastTarget = false;
                talentTipText = Label(talentTip, "", 18, TextAnchor.UpperLeft);
                talentTipText.color = CardText;
                talentTipText.horizontalOverflow = HorizontalWrapMode.Wrap;
                talentTipText.verticalOverflow = VerticalWrapMode.Overflow;
                talentTipText.resizeTextForBestFit = false;
                talentTipText.lineSpacing = 1.15f;
                talentTipText.raycastTarget = false;
                Fill(talentTipText.rectTransform, 12);
            }

            talentTipPinnedTarget = pinned ? target : null;
            talentTipText.text = text;
            float width = text.Length > 120 ? 520f : 420f;
            talentTip.sizeDelta = new Vector2(width, 200f);
            Canvas.ForceUpdateCanvases();
            float height = Mathf.Max(text.Split('\n').Length * 25f, talentTipText.preferredHeight) + 26f;
            var size = new Vector2(width, height);

            talentTip.pivot = new Vector2(0f, 1f);
            talentTip.anchorMin = talentTip.anchorMax = new Vector2(.5f, .5f);
            talentTip.sizeDelta = size;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 below = root.InverseTransformPoint(corners[0]);
            Vector2 above = root.InverseTransformPoint(corners[1]);
            var bounds = root.rect;
            float x = Mathf.Clamp(below.x, bounds.xMin + 8f, bounds.xMax - size.x - 8f);
            float y = below.y - 6f;
            if (y - size.y < bounds.yMin + 8f) y = above.y + size.y + 6f;
            y = Mathf.Clamp(y, bounds.yMin + size.y + 8f, bounds.yMax - 8f);
            talentTip.anchoredPosition = new Vector2(x, y) - bounds.center;
            talentTip.SetAsLastSibling();
            talentTip.gameObject.SetActive(true);

            var watch = talentTip.GetComponent<TalentTipWatch>() ?? talentTip.gameObject.AddComponent<TalentTipWatch>();
            watch.Bind(this, target, pinned);
        }

        /// <summary>Renders interactive clickable/hoverable talent chips inside a VerticalLayoutGroup (e.g. dossier or character sheet).</summary>
        internal void AddInteractiveTalentChips(RectTransform parent, IEnumerable<string> talentIds, float chipHeight = 32f)
        {
            var ids = (talentIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
            if (ids.Length == 0) return;

            const int perRow = 4;
            for (int start = 0; start < ids.Length; start += perRow)
            {
                var slice = ids.Skip(start).Take(perRow).ToArray();
                var row = Rect("天賦標籤列", parent);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = chipHeight + 4f;
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.padding = new RectOffset(0, 0, 2, 2);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;

                foreach (string id in slice)
                {
                    var talent = FindTalent(id);
                    string caption = talent?.name ?? id;
                    var chip = Panel("天賦標籤_" + id, row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
                    UiKit.Skin(chip.GetComponent<Image>(), "Wood");
                    var le = chip.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = Mathf.Clamp(caption.Length * 22f + 26f, 96f, 210f);
                    le.minHeight = chipHeight;

                    var label = Label(chip, caption, 18, TextAnchor.MiddleCenter);
                    label.color = talent?.rarity switch
                    {
                        "legendary" => new Color(1f, .71f, .37f),
                        "rare" => new Color(.64f, .88f, .79f),
                        _ => new Color(.97f, .91f, .78f)
                    };
                    label.resizeTextMinSize = 14;
                    label.resizeTextMaxSize = 18;
                    Fill(label.rectTransform, 4);

                    string capturedId = id;
                    AttachTalentTip(chip, () => TalentTipText(talent, capturedId));
                }
            }
        }

        /// <summary>Hook for ShowCharacter (SectUI.Ranks.cs): adds a compact interactive talent chip section.</summary>
        private void AddCharacterTalentRow(string[] talentIds)
        {
            var ids = (talentIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
            if (ids.Length == 0) return;
            var heading = CreationRow("天賦標題", 30);
            var title = Label(heading, "天賦（點擊或移過查看數值）", 19, TextAnchor.MiddleLeft);
            title.color = CardGold;
            Fill(title.rectTransform);
            AddInteractiveTalentChips(modalList, ids, 32f);
        }

        /// <summary>Dismisses a pinned talent tip if its target disappears or the user clicks outside / presses Esc.</summary>
        private sealed class TalentTipWatch : MonoBehaviour
        {
            private SectUI owner;
            private RectTransform target;
            private bool pinned;
            private int pinnedFrame;

            internal void Bind(SectUI ui, RectTransform rt, bool isPinned)
            {
                owner = ui;
                target = rt;
                pinned = isPinned;
                pinnedFrame = Time.frameCount;
            }

            private void LateUpdate()
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    owner?.HideTalentTip();
                    return;
                }
                if (!pinned || Time.frameCount == pinnedFrame) return;
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                {
                    owner?.HideTalentTip();
                    return;
                }
                if (Input.GetMouseButtonDown(0))
                {
                    var cam = target.GetComponentInParent<Canvas>()?.worldCamera;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(target, Input.mousePosition, cam) &&
                        !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, Input.mousePosition, cam))
                    {
                        owner?.HideTalentTip();
                    }
                }
            }
        }
    }
}
