using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    internal static class UiKit
    {
        private static Texture2D backdrop;

        internal static Texture2D Backdrop()
        {
            if (backdrop != null)
                return backdrop;
            backdrop = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < backdrop.height; y++)
                backdrop.SetPixel(0, y, Color.Lerp(new Color(.04f, .08f, .09f), new Color(.16f, .27f, .27f), y / 63f));
            backdrop.Apply(false, true);
            return backdrop;
        }

        internal static Sprite Sprite(string name) => Resources.Load<Sprite>("InkUi/" + name);

        internal static Font Font(string role, Font fallback, string text = "")
        {
            var font = Resources.Load<Font>("Fonts/" + role);
            if (font == null)
                return fallback;
            return System.Linq.Enumerable.Any(text, ch => !char.IsWhiteSpace(ch) && !font.HasCharacter(ch)) ? fallback : font;
        }

        internal static void Skin(Image image, string name)
        {
            var sprite = Sprite(name);
            if (sprite == null)
                return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
    }

    internal sealed class InkPanelMotion : MonoBehaviour
    {
        private CanvasGroup group;
        private float start;
        private RectTransform panel;
        private Vector2 restingPosition;

        private void OnEnable()
        {
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            panel = (RectTransform)transform;
            restingPosition = panel.anchoredPosition;
            start = Time.unscaledTime;
            group.alpha = .65f;
            panel.anchoredPosition = restingPosition + Vector2.down * 8;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - start) / .12f);
            if (progress >= 1 && group.alpha >= 1)
                return;
            group.alpha = Mathf.Lerp(.65f, 1, progress);
            panel.anchoredPosition = restingPosition + Vector2.down * (8 * (1 - progress));
        }
        private void OnDisable()
        {
            if (panel != null && group.alpha < 1)
                panel.anchoredPosition = restingPosition;
        }
    }
}
