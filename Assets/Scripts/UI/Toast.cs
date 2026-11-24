using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1125 #21: stacked notice slips. Each slip grows with its text (never overflows), shows an item / resource
    /// icon on the left with a quality-coloured rim, fades out after ~3.5 s, and closes at once on a left or right
    /// click. New slips appear at the bottom and push older ones upward; at most four are shown.
    /// </summary>
    internal static class Toast
    {
        private const float Life = 3.6f, Fade = .55f, Width = 760;
        private const int MaxShown = 4;
        private static RectTransform column;
        private static Font font;
        private static readonly List<ToastSlip> live = new();

        internal static bool Ready => column != null;

        internal static void Initialize(RectTransform canvas, Font fallback)
        {
            font = fallback;
            column = new GameObject("Toast stack", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(canvas, false);
            column.anchorMin = column.anchorMax = new Vector2(.5f, 0);
            column.pivot = new Vector2(.5f, 0);
            column.anchoredPosition = new Vector2(0, 250);
            column.sizeDelta = new Vector2(Width, 0);
            var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.LowerCenter;
            stack.spacing = 8;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Shows a slip. <paramref name="icon"/> is a Resources/Icons name; null guesses one from the text.</summary>
        internal static void Show(string text, string icon = null, Color? quality = null)
        {
            if (column == null || string.IsNullOrWhiteSpace(text)) return;
            column.SetAsLastSibling();
            live.RemoveAll(slip => slip == null);
            while (live.Count >= MaxShown)
            {
                live[0].Dismiss(true);
                live.RemoveAt(0);
            }
            var slip = Build(text.Trim(), icon ?? GuessIcon(text), quality ?? GuessQuality(text));
            live.Add(slip);
        }

        /// <summary>Removes every slip immediately (e.g. when leaving the courtyard).</summary>
        internal static void ClearAll()
        {
            foreach (var slip in live)
                if (slip != null) slip.Dismiss(true);
            live.Clear();
        }

        internal static int Count
        {
            get
            {
                live.RemoveAll(slip => slip == null);
                return live.Count;
            }
        }

        private static ToastSlip Build(string text, string icon, Color quality)
        {
            var rect = new GameObject("Toast", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(column, false);
            var frame = rect.gameObject.AddComponent<Image>();
            UiKit.Skin(frame, "Lacquer");
            frame.color = new Color(1, 1, 1, .97f);
            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(18, 22, 14, 14);
            row.spacing = 16;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            var rim = new GameObject("Quality rim", typeof(RectTransform)).GetComponent<RectTransform>();
            rim.SetParent(rect, false);
            var rimImage = rim.gameObject.AddComponent<Image>();
            rimImage.color = quality;
            rimImage.raycastTarget = false;
            var rimLayout = rim.gameObject.AddComponent<LayoutElement>();
            rimLayout.preferredWidth = rimLayout.minWidth = 58;
            rimLayout.preferredHeight = rimLayout.minHeight = 58;
            var art = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
            art.SetParent(rim, false);
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = Vector2.one * 3;
            art.offsetMax = Vector2.one * -3;
            var back = art.gameObject.AddComponent<Image>();
            back.color = new Color(.10f, .08f, .06f);
            back.raycastTarget = false;
            var picture = new GameObject("Picture", typeof(RectTransform)).GetComponent<RectTransform>();
            picture.SetParent(art, false);
            picture.anchorMin = Vector2.zero;
            picture.anchorMax = Vector2.one;
            picture.offsetMin = Vector2.one * 3;
            picture.offsetMax = Vector2.one * -3;
            var raw = picture.gameObject.AddComponent<RawImage>();
            raw.texture = UiKit.Icon(icon);
            raw.enabled = raw.texture != null;
            raw.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            label.rectTransform.SetParent(rect, false);
            label.font = UiKit.Font("Interface", font, text);
            label.fontSize = 22;
            label.supportRichText = true;
            label.color = new Color(.97f, .91f, .78f);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            label.lineSpacing = 1.08f;
            label.raycastTarget = false;
            // Legacy Text only breaks at spaces; no-break spaces keep "銀兩 +10" runs together.
            label.text = text.Replace(' ', ' ');
            var textLayout = label.gameObject.AddComponent<LayoutElement>();
            textLayout.preferredWidth = Width - 18 - 22 - 58 - 16;
            textLayout.flexibleWidth = 1;

            var slip = rect.gameObject.AddComponent<ToastSlip>();
            slip.Begin(Life, Fade);
            LayoutRebuilder.ForceRebuildLayoutImmediate(column);
            return slip;
        }

        internal static string GuessIcon(string text)
        {
            if (string.IsNullOrEmpty(text)) return "act_skill";
            foreach (string slot in GameState.GearSlots)
                if (text.Contains("（" + slot)) return UiKit.SlotIcon(slot);
            if (text.Contains("秘笈") || text.Contains("劍訣") || text.Contains("心法")) return "item_manual_sword";
            if (text.Contains("丹") || text.Contains("藥") || text.Contains("傷")) return "skill_medicine";
            if (text.Contains("落成") || text.Contains("興建") || text.Contains("空地") || text.Contains("動工")) return "skill_construction";
            if (text.Contains("弟子") || text.Contains("入門") || text.Contains("收徒")) return "hud_disciple";
            if (text.Contains("行動力")) return "hud_ap";
            if (text.Contains("奇遇") || text.Contains("秘境")) return "realm_treasure";
            if (text.Contains("銀")) return "hud_silver";
            if (text.Contains("糧")) return "hud_food";
            if (text.Contains("聲望")) return "hud_rep";
            if (text.Contains("忠誠") || text.Contains("好感")) return "hud_affinity";
            return "act_skill";
        }

        internal static Color GuessQuality(string text)
        {
            if (string.IsNullOrEmpty(text)) return QualityColor(0);
            if (text.Contains("神兵") || text.Contains("傳說")) return QualityColor(3);
            if (text.Contains("精品") || text.Contains("精良")) return QualityColor(2);
            if (text.Contains("良品")) return QualityColor(1);
            if (text.Contains("不足") || text.Contains("失敗") || text.Contains("無法")) return new Color(.72f, .25f, .20f);
            return QualityColor(0);
        }

        /// <summary>Grade colours: 凡品 bronze, 良品 jade, 精品 azure, 神兵 violet-gold.</summary>
        internal static Color QualityColor(int grade) => grade switch
        {
            1 => new Color(.36f, .72f, .48f),
            2 => new Color(.33f, .58f, .90f),
            3 => new Color(.80f, .52f, .95f),
            _ => new Color(.78f, .62f, .36f)
        };
    }

    internal sealed class ToastSlip : MonoBehaviour, IPointerClickHandler
    {
        private CanvasGroup group;
        private float born, life, fade;
        private bool leaving;

        internal void Begin(float life, float fade)
        {
            this.life = life;
            this.fade = fade;
            born = Time.unscaledTime;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) UiBack.Consume();
            Dismiss(true);
        }

        internal void Dismiss(bool now)
        {
            if (this == null) return;
            if (now)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            leaving = true;
        }

        private void Update()
        {
            float age = Time.unscaledTime - born;
            if (age >= life) leaving = true;
            if (!leaving)
            {
                group.alpha = Mathf.Clamp01(age / .18f);
                return;
            }
            group.alpha -= Time.unscaledDeltaTime / fade;
            if (group.alpha <= 0) Dismiss(true);
        }
    }
}
