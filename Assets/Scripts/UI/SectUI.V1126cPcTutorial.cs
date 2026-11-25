using System;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126c PC3 #11 / #15: the guide's pointer. A bobbing arrow with 「點這裡」 sits beside whatever control the
    /// current step lights, and the 擴建院落 step marks the recommended blueprint card 「★推薦」 and lights it.
    /// </summary>
    public sealed partial class SectUI
    {
        private RectTransform guideArrow;
        private Text guideArrowGlyph, guideArrowWord;

        internal bool TargetActive(string key) =>
            targets.TryGetValue(key, out var rect) && rect != null && rect.gameObject.activeInHierarchy;

        /// <summary>Finds the build-menu card for <paramref name="building"/>, tags it 「★推薦」 and lets the guide light it.</summary>
        internal void MarkBlueprint(string building)
        {
            targets.Remove("blueprint");
            if (string.IsNullOrEmpty(building) || commandList == null) return;
            foreach (Transform child in commandList)
            {
                var card = child as RectTransform;
                if (card == null) continue;
                foreach (var text in card.GetComponentsInChildren<Text>(true))
                {
                    string line = (text.text ?? "").Replace("【已選】", "");
                    int cut = line.IndexOf('\n');
                    if ((cut >= 0 ? line.Substring(0, cut) : line).Trim() != building) continue;
                    text.text = text.text.Insert(cut >= 0 ? text.text.IndexOf('\n') : text.text.Length, "　★推薦");
                    targets["blueprint"] = card;
                    return;
                }
            }
        }

        private void EnsureGuideArrow()
        {
            if (guideArrow != null) return;
            guideArrow = Rect("Tutorial arrow", canvas);
            guideArrow.sizeDelta = new Vector2(120, 86);
            guideArrow.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            guideArrowGlyph = Label(guideArrow, "▼", 44, TextAnchor.MiddleCenter);
            guideArrowGlyph.color = new Color(.95f, .42f, .16f);
            guideArrowGlyph.raycastTarget = false;
            guideArrowWord = Label(guideArrow, "點這裡", 20, TextAnchor.MiddleCenter);
            guideArrowWord.color = UiKit.HeaderGold;
            guideArrowWord.fontStyle = FontStyle.Bold;
            guideArrowWord.raycastTarget = false;
            var shadow = guideArrowWord.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0, 0, 0, .85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>Places the arrow beside the lit control (min/max in canvas space), pointing at it.</summary>
        private void PointGuideArrow(Vector2 min, Vector2 max, float phase)
        {
            EnsureGuideArrow();
            var size = canvas.rect.size;
            Vector2 center = (min + max) / 2;
            float bob = (phase - .7f) * 40;
            string glyph;
            Vector2 at;
            if (center.x < -size.x * .28f) // left command panel: arrow to its right, pointing left
            {
                glyph = "◀";
                at = new Vector2(max.x + 70 + bob, center.y);
            }
            else if (center.y > size.y * .25f) // top bar: arrow below, pointing up
            {
                glyph = "▲";
                at = new Vector2(center.x, min.y - 56 - bob);
            }
            else // bottom bar and elsewhere: arrow above, pointing down
            {
                glyph = "▼";
                at = new Vector2(center.x, max.y + 56 + bob);
            }
            at.x = Mathf.Clamp(at.x, -size.x / 2 + 64, size.x / 2 - 64);
            at.y = Mathf.Clamp(at.y, -size.y / 2 + 48, size.y / 2 - 48);
            guideArrowGlyph.text = glyph;
            bool vertical = glyph != "◀";
            Anchor(guideArrowGlyph.rectTransform, new(.5f, .5f), new(.5f, .5f),
                vertical ? new(-30, glyph == "▲" ? 4 : -40) : new(-60, -24), vertical ? new(30, glyph == "▲" ? 48 : 4) : new(-10, 24));
            Anchor(guideArrowWord.rectTransform, new(.5f, .5f), new(.5f, .5f),
                vertical ? new(-60, glyph == "▲" ? -40 : 6) : new(-8, -16), vertical ? new(60, glyph == "▲" ? -6 : 40) : new(80, 16));
            guideArrow.anchoredPosition = at;
            guideArrow.SetAsLastSibling();
            guideArrow.gameObject.SetActive(true);
        }

        private void HideGuideArrow()
        {
            if (guideArrow != null) guideArrow.gameObject.SetActive(false);
        }
    }
}
