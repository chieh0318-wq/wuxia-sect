using System;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    internal static class MapUi
    {
        internal static readonly Color Ink = new(.16f,.14f,.11f);
        internal static readonly Color Paper = new(.93f,.86f,.73f);
        internal static readonly Color Wood = new(.21f,.25f,.23f);
        internal static readonly Color Red = new(.54f,.22f,.15f);
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

        internal static Text Text(Transform parent,string label,float x,float y,float w,float h,int size,
            Color color,TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei","Noto Sans CJK TC","Arial" },22)
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = Rect(parent,label,x,y,w,h).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = label;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        internal static Button Button(Transform parent,string label,float x,float y,float w,float h,Action action,
            Color? background = null,int size = 20)
        {
            var rect = Panel(parent,label,x,y,w,h,background ?? Wood);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => { AudioManager.Play("click"); action(); });
            rect.gameObject.AddComponent<M2ButtonAudio>();
            Text(rect,label,4,0,w-8,h,size,Paper,TextAnchor.MiddleCenter);
            return button;
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
