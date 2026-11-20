using System;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// Absolute-position helpers for the world map and intel screens. Panels and buttons use the shared
    /// ink UI kit (paper / wood / teal 9-slice, vermilion buttons) and the role fonts from UiKit so the
    /// map reads as part of the same game as the courtyard HUD.
    /// </summary>
    internal static class MapUi
    {
        internal static readonly Color Ink = new(.19f,.17f,.14f);
        internal static readonly Color Paper = new(.93f,.86f,.73f);
        internal static readonly Color Wood = new(.21f,.25f,.23f);
        internal static readonly Color Red = new(.61f,.24f,.17f);
        internal static readonly Color Gold = new(.78f,.65f,.44f);
        internal static readonly Color Jade = new(.20f,.39f,.28f);
        private static Font font;

        internal static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            rect.anchorMin = rect.anchorMax = new Vector2(0,1);
            rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y);
            rect.sizeDelta = new Vector2(w,h);
            return rect;
        }

        internal static RectTransform Panel(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var rect = Rect(parent,name,x,y,w,h);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        /// <summary>A 9-slice ink-kit panel ("Paper", "Wood", "Teal", "Plaque"...).</summary>
        internal static RectTransform Kit(Transform parent,string name,float x,float y,float w,float h,string sprite,
            bool blocksClicks = true)
        {
            var rect = Panel(parent,name,x,y,w,h,sprite == "Paper" ? Paper : Wood);
            var image = rect.GetComponent<Image>();
            UiKit.Skin(image,sprite);
            image.raycastTarget = blocksClicks;
            return rect;
        }

        internal static Text Text(Transform parent,string label,float x,float y,float w,float h,int size,
            Color color,TextAnchor alignment = TextAnchor.MiddleLeft,string role = null)
        {
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei","Noto Sans CJK TC","Arial" },22)
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = Rect(parent,label,x,y,w,h).gameObject.AddComponent<Text>();
            text.font = UiKit.Font(role ?? (size >= 28 ? "Titles" : "Interface"),font,label);
            text.fontSize = size;
            text.text = label;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            // Paragraphs (upper-left) keep their size and wrap; single-line labels shrink to fit their slot.
            text.resizeTextForBestFit = alignment != TextAnchor.UpperLeft;
            text.resizeTextMinSize = Mathf.Min(13,size);
            text.resizeTextMaxSize = size;
            return text;
        }

        internal static Button Button(Transform parent,string label,float x,float y,float w,float h,Action action,
            Color? background = null,int size = 20)
        {
            Color tint = background ?? Wood;
            var rect = Panel(parent,label,x,y,w,h,tint);
            var image = rect.GetComponent<Image>();
            if (tint == Red) UiKit.Skin(image,"Vermilion");
            else if (tint == Wood) UiKit.Skin(image,"Wood");
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1,.88f,.76f);
            colors.pressedColor = new Color(.74f,.65f,.57f);
            colors.disabledColor = new Color(.62f,.60f,.58f,.75f);
            button.colors = colors;
            button.onClick.AddListener(() => { UiSfx.Play("click"); action(); });
            rect.gameObject.AddComponent<UiHoverSound>();
            if (!string.IsNullOrEmpty(label))
                Text(rect,label,6,0,w-12,h,size,Paper,TextAnchor.MiddleCenter);
            return button;
        }

        /// <summary>Toggle-tab look: vermilion when selected, wood otherwise.</summary>
        internal static void Select(Button button,bool selected)
        {
            UiKit.Skin((Image)button.targetGraphic,selected ? "Vermilion" : "Wood");
            if (button.targetGraphic.GetComponent<Image>().sprite == null)
                button.targetGraphic.color = selected ? Red : Wood;
        }

        internal static void Clear(Transform root)
        {
            foreach (Transform child in root)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
