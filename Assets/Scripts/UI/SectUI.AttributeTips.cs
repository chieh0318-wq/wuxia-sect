using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126b 決定 1: hovering an attribute (creation rows, the hero page, a disciple's sheet) lists every effect one
    /// more point has, read from <see cref="AttributeSystem.PerPoint"/>.
    /// </summary>
    public sealed partial class SectUI
    {
        private RectTransform attributeTip;
        private Text attributeTipText;

        internal void AttributeHover(RectTransform target, int slot, Func<int> value)
        {
            if (target == null) return;
            if (target.TryGetComponent<Graphic>(out var surface)) surface.raycastTarget = true;
            var trigger = target.gameObject.GetComponent<EventTrigger>() ?? target.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowAttributeTip(target, AttributePerPoint.Tip(slot, value())));
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideAttributeTip());
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        private void HideAttributeTip()
        {
            if (attributeTip != null) attributeTip.gameObject.SetActive(false);
        }

        private void ShowAttributeTip(RectTransform target, string text)
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            if (canvas == null || string.IsNullOrEmpty(text)) return;
            var root = target.IsChildOf(modal) ? modal : (RectTransform)canvas.rootCanvas.transform;
            if (attributeTip == null || attributeTip.parent != root)
            {
                if (attributeTip != null) UnityEngine.Object.Destroy(attributeTip.gameObject);
                attributeTip = Panel("屬性提示", root, new(.5f, .5f), new(.5f, .5f), Vector2.zero, new(440, 200), false);
                UiKit.Skin(attributeTip.GetComponent<Image>(), "Lacquer");
                attributeTip.GetComponent<Image>().raycastTarget = false;
                attributeTipText = Label(attributeTip, "", 18, TextAnchor.UpperLeft);
                attributeTipText.color = CardText;
                attributeTipText.horizontalOverflow = HorizontalWrapMode.Wrap;
                attributeTipText.verticalOverflow = VerticalWrapMode.Overflow;
                attributeTipText.raycastTarget = false;
                Fill(attributeTipText.rectTransform, 12);
            }
            attributeTipText.text = text;
            int lines = text.Split('\n').Length;
            // v1126c PC3 #3: long formula lines wrap, so size the box from the wrapped text height.
            float width = text.Length > 150 ? 600 : 440;
            attributeTip.sizeDelta = new Vector2(width, 200);
            var size = new Vector2(width, Mathf.Max(lines * 25, attributeTipText.preferredHeight) + 26);
            attributeTip.pivot = new Vector2(0, 1);
            attributeTip.anchorMin = attributeTip.anchorMax = new Vector2(.5f, .5f);
            attributeTip.sizeDelta = size;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 below = root.InverseTransformPoint(corners[0]);
            Vector2 above = root.InverseTransformPoint(corners[1]);
            var bounds = root.rect;
            float x = Mathf.Clamp(below.x, bounds.xMin + 8, bounds.xMax - size.x - 8);
            float y = below.y - 6;
            if (y - size.y < bounds.yMin + 8) y = above.y + size.y + 6;
            y = Mathf.Clamp(y, bounds.yMin + size.y + 8, bounds.yMax - 8);
            attributeTip.anchoredPosition = new Vector2(x, y) - bounds.center;
            attributeTip.SetAsLastSibling();
            attributeTip.gameObject.SetActive(true);
            // v1126c PC3: the tip follows its row; it closes when the page that owns the row is rebuilt or left.
            var watch = attributeTip.GetComponent<TipOwnerWatch>();
            if (!watch) watch = attributeTip.gameObject.AddComponent<TipOwnerWatch>();
            watch.Target = target;
        }
    }

    /// <summary>v1126c PC3: hides a hover tip whose owning control was destroyed or hidden.</summary>
    internal sealed class TipOwnerWatch : MonoBehaviour
    {
        internal RectTransform Target;
        private void LateUpdate()
        {
            if (Target == null || !Target.gameObject.activeInHierarchy) gameObject.SetActive(false);
        }
    }
}
