using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        // Title, a two-line objective at 24px and a three-line hint at 20px above the skip button.
        private const float ObjectiveHeight = 255;
        private readonly Dictionary<string, RectTransform> targets = new();
        private readonly Vector3[] highlightCorners = new Vector3[4];
        private RectTransform modalButtons;
        private bool modalDismissible;
        private Action modalClosed;
        private RectTransform objective, pulse, commands;
        private Sprite paper;
        private Texture2D paperTexture;
        private Text objectiveTitle, objectiveBody, objectiveHint, landmarkMarker;
        private Action onSkip;

        internal Canvas Canvas => canvas.GetComponent<Canvas>();

        private void CreateObjective()
        {
            objective = Panel("Tutorial objective", canvas, Vector2.one, Vector2.one,
                new Vector2(-442, -110 - ObjectiveHeight), new Vector2(-18, -110));
            objectiveTitle = Label(objective, "", 28);
            Anchor(objectiveTitle.rectTransform, Vector2.zero, Vector2.one, new(20, ObjectiveHeight - 52), new(-20, -16));
            objectiveBody = Label(objective, "", 24);
            Anchor(objectiveBody.rectTransform, Vector2.zero, Vector2.one, new(20, ObjectiveHeight - 126), new(-20, -58));
            objectiveHint = Label(objective, "", 20);
            Anchor(objectiveHint.rectTransform, Vector2.zero, Vector2.one, new(20, 49), new(-20, -128));
            var skip = Button(objective, "略過引導", () => onSkip?.Invoke());
            Anchor(skip, Vector2.zero, Vector2.zero, new(20, 11), new(157, 45));

            pulse = Panel("Tutorial control highlight", canvas, Vector2.one * .5f, Vector2.one * .5f,
                Vector2.zero, Vector2.zero, false);
            pulse.GetComponent<Image>().color = Color.clear;

            for (int i = 0; i < 4; i++)
            {
                var line = Rect("Seal border", pulse);
                var image = line.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                image.color = new Color(.82f, .36f, .18f);
                if (i < 2)
                    Anchor(line, new(0, i), new(1, i), new(0, -2), new(0, 2));
                else
                    Anchor(line, new(i - 2, 0), new(i - 2, 1), new(-2, 0), new(2, 0));
            }
            pulse.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            landmarkMarker = Label(labels, "◆", 38, TextAnchor.MiddleCenter);
            landmarkMarker.color = new Color(.83f, .29f, .13f);
            landmarkMarker.rectTransform.sizeDelta = new Vector2(72, 68);
            HideObjective();
        }

        private Sprite Paper()
        {
            if (paper)
                return paper;
            const int size = 64;
            paperTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Parchment and timber frame" };
            paperTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                float fleck = Mathf.PerlinNoise(x * .33f, y * .57f);
                float grain = Mathf.PerlinNoise(x * .13f, y * .09f);
                Color color = edge < 5
                    ? Color.Lerp(new Color(.19f, .11f, .07f), new Color(.39f, .25f, .16f), grain)
                    : Color.Lerp(new Color(.80f, .71f, .53f), new Color(.94f, .87f, .71f), .55f * grain + .45f * fleck);
                if (edge == 5 || edge == 8)
                    color = Color.Lerp(color, new Color(.64f, .45f, .27f), .42f);
                pixels[y * size + x] = color;
            }
            paperTexture.SetPixels32(pixels);
            paperTexture.Apply(false, true);
            paper = Sprite.Create(paperTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, Vector4.one * 10);
            return paper;
        }

        internal void ShowObjective(string title, string body, string hint, Action skip)
        {
            objectiveTitle.text = title;
            objectiveBody.text = body;
            objectiveHint.text = hint;
            onSkip = skip;
            objective.gameObject.SetActive(true);
        }

        internal void HideObjective()
        {
            objective.gameObject.SetActive(false);
            pulse.gameObject.SetActive(false);
            landmarkMarker.gameObject.SetActive(false);
        }

        internal void PositionHighlight(string key, Camera camera, Courtyard world)
        {
            pulse.gameObject.SetActive(false);
            landmarkMarker.gameObject.SetActive(false);
            float top = details.gameObject.activeSelf ? -686 : -110;
            Anchor(objective, Vector2.one, Vector2.one, new Vector2(-442, top - ObjectiveHeight), new Vector2(-18, top));
            if (ModalOpen)
                modal.SetAsLastSibling();
            if (ModalOpen || string.IsNullOrEmpty(key))
                return;

            float phase = .7f + Mathf.Sin(Time.unscaledTime * 5) * .25f;
            if (key.StartsWith("ui:", StringComparison.Ordinal) && targets.TryGetValue(key.Substring(3), out var target))
            {
                target.GetWorldCorners(highlightCorners);
                Vector2 min = canvas.InverseTransformPoint(highlightCorners[0]);
                Vector2 max = canvas.InverseTransformPoint(highlightCorners[2]);
                pulse.anchoredPosition = (min + max) / 2;
                pulse.sizeDelta = max - min + Vector2.one * 9;
                pulse.GetComponent<CanvasGroup>().alpha = phase;
                pulse.gameObject.SetActive(true);
            }
            if (!key.StartsWith("building:", StringComparison.Ordinal) ||
                !world.TryGetLandmark(key.Substring(9), out var landmark))
                return;
            Vector3 screen = camera.WorldToScreenPoint(landmark.root.position + Vector3.up * (landmark.height + 1.45f));
            Camera projection = Canvas.renderMode == RenderMode.ScreenSpaceCamera ? camera : null;
            if (screen.z > 0 && camera.pixelRect.Contains(screen) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labels, screen, projection, out var local))
            {
                landmarkMarker.rectTransform.anchoredPosition = local + Vector2.up * (phase * 12);
                landmarkMarker.transform.SetAsLastSibling();
                landmarkMarker.gameObject.SetActive(true);
            }
        }
    }
}
