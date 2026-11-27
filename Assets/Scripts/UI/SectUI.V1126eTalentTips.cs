using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126e #3: talent chips answer a hover and a click with the talent's name, rarity and exact effect. The numbers are
    /// the talent's own data — its description (checked against the applied modifiers by TalentAuditV1126e) and, for
    /// one-off effects, <see cref="EffectText.Describe(IEnumerable{GameEffect}, GameState, string)"/> of the same effect
    /// list ApplyEffects uses — so the tip never carries a second copy of the numbers.
    /// </summary>
    internal static class TalentTips
    {
        private static CreationOption[] catalog;

        internal static CreationOption[] Catalog =>
            catalog ??= M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>();

        internal static CreationOption Find(string id) => Catalog.FirstOrDefault(option => option?.id == id);

        internal static string Rarity(CreationOption option) => option.cost < 0 ? "負面" : option.rarity switch
        {
            "legendary" => "傳說",
            "rare" => "稀有",
            _ => "常見"
        };

        /// <summary>「【氣海如淵】常見天賦」, then the effect lines.</summary>
        internal static string Text(CreationOption option)
        {
            if (option == null) return null;
            var lines = new List<string> { $"【{option.name}】{Rarity(option)}天賦" };
            string desc = (option.desc ?? option.summary ?? "").Trim();
            if (desc.Length > 0) lines.Add("效果：" + desc);
            string applied = option.effects?.Length > 0 ? EffectText.Describe(option.effects) : "";
            if (!string.IsNullOrWhiteSpace(applied) && applied != desc) lines.Add("數值：" + applied);
            if (lines.Count == 1) lines.Add("無數值效果。");
            return string.Join("\n", lines);
        }

        /// <summary>Several talents, one block each (the recruit table's 天賦 cell).</summary>
        internal static string Text(IEnumerable<string> ids)
        {
            var blocks = (ids ?? Array.Empty<string>()).Distinct().Select(Find).Where(option => option != null).Select(Text).ToArray();
            return blocks.Length == 0 ? "沒有天賦。" : string.Join("\n", blocks);
        }

        /// <summary>The table cell text: the names (at most two, then 「等 N 項」).</summary>
        internal static string Names(string[] ids)
        {
            var names = (ids ?? Array.Empty<string>()).Select(id => Find(id)?.name ?? id).ToArray();
            if (names.Length == 0) return "—";
            return names.Length <= 2 ? string.Join("\n", names) : $"{names[0]}\n{names[1]} 等 {names.Length} 項";
        }
    }

    public sealed partial class SectUI
    {
        /// <summary>v1126e #3: hover shows the talent tip; a click pins or closes it (touch and click users).</summary>
        internal void TalentChip(RectTransform chip, Func<string> text, bool clickable = true)
        {
            if (chip == null) return;
            if (chip.TryGetComponent<Graphic>(out var surface)) surface.raycastTarget = true;
            var hover = clickable ? chip.gameObject.AddComponent<TalentTipChip>() : chip.gameObject.AddComponent<TalentTipHover>();
            hover.Ui = this;
            hover.Text = text;
        }

        /// <summary>
        /// v1126e #3 (row idea from the Antigravity branch): a member's character card lists their talents as chips,
        /// four per row, each answering hover and click with the exact effect.
        /// </summary>
        internal void CharacterTalentRow(string[] talentIds)
        {
            var ids = (talentIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
            if (ids.Length == 0) return;
            var heading = CreationRow("天賦標題", 30);
            var title = Label(heading, "天賦（滑過或點擊看數值）", 19, TextAnchor.MiddleLeft);
            title.color = CardGold;
            Fill(title.rectTransform);
            for (int start = 0; start < ids.Length; start += 4)
            {
                var row = CreationRow("天賦標籤列", 36);
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8;
                layout.padding = new RectOffset(0, 0, 2, 2);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                foreach (string id in ids.Skip(start).Take(4))
                {
                    var talent = TalentTips.Find(id);
                    string caption = talent?.name ?? id;
                    var chip = Panel("天賦_" + caption, row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
                    UiKit.Skin(chip.GetComponent<Image>(), "Wood");
                    var element = chip.gameObject.AddComponent<LayoutElement>();
                    element.preferredWidth = Mathf.Clamp(caption.Length * 22f + 26f, 96f, 210f);
                    element.minHeight = 32;
                    var label = Label(chip, caption, 18, TextAnchor.MiddleCenter);
                    label.color = talent?.rarity switch
                    {
                        "legendary" => new Color(1f, .71f, .37f),
                        "rare" => new Color(.64f, .88f, .79f),
                        _ => new Color(.97f, .91f, .78f)
                    };
                    label.raycastTarget = false;
                    Fill(label.rectTransform, 4);
                    var shown = talent;
                    string fallback = id;
                    TalentChip(chip, () => shown != null ? TalentTips.Text(shown) : TalentTips.Text(new[] { fallback }));
                }
            }
        }

        internal void ShowTalentTip(RectTransform target, string text) => ShowAttributeTip(target, text);
        internal void HideTalentTip() => HideAttributeTip();

        /// <summary>v1126e capture: hovers (or clicks) the first talent chip named <paramref name="name"/> on screen.</summary>
        internal string RevealTalentTipForCapture(string name = null, bool click = false)
        {
            var chips = UnityEngine.Object.FindObjectsByType<TalentTipHover>(FindObjectsSortMode.None)
                .Where(chip => chip.isActiveAndEnabled).ToArray();
            var chip = chips.FirstOrDefault(entry => name == null || (entry.Text?.Invoke() ?? "").Contains("【" + name + "】"))
                ?? chips.FirstOrDefault();
            if (chip == null) return "no chip";
            if (click && chip is TalentTipChip clicker) clicker.OnPointerClick(new PointerEventData(EventSystem.current));
            else chip.OnPointerEnter(new PointerEventData(EventSystem.current));
            return $"{chips.Length} chips; shown: {chip.Text?.Invoke()?.Replace("\n", " / ")}";
        }
    }

    /// <summary>Hover-only talent tip (lets a click fall through to the row button underneath).</summary>
    internal class TalentTipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal SectUI Ui;
        internal Func<string> Text;
        protected bool Pinned;

        public void OnPointerEnter(PointerEventData _)
        {
            if (Ui != null) Ui.ShowTalentTip((RectTransform)transform, Text?.Invoke());
        }

        public void OnPointerExit(PointerEventData _)
        {
            if (!Pinned && Ui != null) Ui.HideTalentTip();
        }
    }

    /// <summary>Talent chip: hover shows the tip, a click pins it open (a second click closes it).</summary>
    internal sealed class TalentTipChip : TalentTipHover, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData _)
        {
            Pinned = !Pinned;
            if (Ui == null) return;
            if (Pinned) Ui.ShowTalentTip((RectTransform)transform, Text?.Invoke());
            else Ui.HideTalentTip();
        }

        private void OnDisable() => Pinned = false;
    }
}
