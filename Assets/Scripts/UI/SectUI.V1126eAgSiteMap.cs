using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126e-ag (Item 2): keeps every faction flag and site pin strictly on its true map coordinate while offsetting
    /// site nameplates with leader lines so nothing overlaps at 1920×1080, 1366×768, or 1280×720.
    /// </summary>
    internal sealed class SiteMapLayoutGuard : MonoBehaviour
    {
        private struct SiteEntry
        {
            public string id;
            public RectTransform marker;
            public RectTransform pin;
            public Vector2 uv;
            public Vector2 preferredPx;
            public bool focused;
        }

        private readonly List<SiteEntry> entries = new();
        private Vector2 lastSize = new(-1, -1);

        internal void Register(string id, RectTransform marker, RectTransform pin, Vector2 uv, Vector2 preferredPx, bool focused)
        {
            entries.Add(new SiteEntry
            {
                id = id, marker = marker, pin = pin, uv = uv, preferredPx = preferredPx, focused = focused
            });
            lastSize = new(-1, -1);
        }

        private void OnRectTransformDimensionsChange() => lastSize = new(-1, -1);

        private void LateUpdate()
        {
            var map = (RectTransform)transform;
            var size = map.rect.size;
            if (size.x <= 1f || size.y <= 1f) return;
            if (Mathf.Abs(size.x - lastSize.x) < 0.5f && Mathf.Abs(size.y - lastSize.y) < 0.5f) return;
            lastSize = size;
            Layout(map);
        }

        private void Layout(RectTransform map)
        {
            // Remove old leader lines before re-drawing for the new resolution.
            for (int i = map.childCount - 1; i >= 0; i--)
            {
                var child = map.GetChild(i);
                if (child.name.StartsWith("引線_", StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }

            Rect bounds = map.rect;
            const float margin = 8f;
            Rect safe = Rect.MinMaxRect(bounds.xMin + margin, bounds.yMin + margin, bounds.xMax - margin, bounds.yMax - margin);

            // Fixed obstacles: faction flags, site pins, and legend (never moved off their true coordinates).
            var fixedBoxes = new List<Rect>();
            var flags = new List<RectTransform>();
            var pins = new List<RectTransform>();
            RectTransform legend = null;
            RectTransform tip = null;

            foreach (Transform child in map)
            {
                var rt = child as RectTransform;
                if (rt == null || !rt.gameObject.activeSelf) continue;
                if (child.name.StartsWith("附近勢力_", StringComparison.Ordinal))
                {
                    flags.Add(rt);
                    fixedBoxes.Add(LocalBox(map, rt, 4f));
                }
                else if (child.name.StartsWith("選址點_", StringComparison.Ordinal))
                {
                    pins.Add(rt);
                    fixedBoxes.Add(LocalBox(map, rt, 3f));
                }
                else if (child.name == "關係圖例")
                {
                    legend = rt;
                    fixedBoxes.Add(LocalBox(map, rt, 6f));
                }
                else if (child.name == "勢力提示")
                {
                    tip = rt;
                }
            }

            var placed = new List<Rect>();
            // Place focused site first, then sites in denser areas (more nearby obstacles) so tight clusters get first pick.
            var ordered = entries
                .Where(e => e.marker != null)
                .OrderByDescending(e => e.focused)
                .ThenByDescending(e => CountNearby(UvToLocal(bounds, e.uv), fixedBoxes, 95f))
                .ToList();

            var lines = new List<RectTransform>();
            foreach (var entry in ordered)
            {
                Vector2 pinPos = UvToLocal(bounds, entry.uv);
                Vector2 labelSize = entry.marker.rect.size;
                if (labelSize.x < 10f || labelSize.y < 10f) labelSize = new Vector2(114f, 34f);

                Vector2 bestOffset = FindBestOffset(pinPos, labelSize, entry.preferredPx, safe, fixedBoxes, placed);
                entry.marker.anchoredPosition = bestOffset;

                Vector2 center = pinPos + bestOffset;
                Rect box = Rect.MinMaxRect(center.x - labelSize.x * .5f - 4f, center.y - labelSize.y * .5f - 4f,
                    center.x + labelSize.x * .5f + 4f, center.y + labelSize.y * .5f + 4f);
                placed.Add(box);

                if (bestOffset.magnitude > 14f)
                    lines.Add(DrawLeader(map, entry.id, pinPos, center, entry.focused));
            }

            // Z-order: map painting (0) -> leader lines -> pins -> faction flags -> site nameplates -> legend -> hover tip.
            int idx = 1;
            foreach (var line in lines) line.SetSiblingIndex(idx++);
            foreach (var pin in pins) pin.SetSiblingIndex(idx++);
            foreach (var flag in flags) flag.SetSiblingIndex(idx++);
            foreach (var entry in entries)
                if (entry.marker != null) entry.marker.SetSiblingIndex(idx++);
            if (legend != null) legend.SetAsLastSibling();
            if (tip != null) tip.SetAsLastSibling();
        }

        private static Vector2 FindBestOffset(Vector2 pinPos, Vector2 size, Vector2 preferred, Rect safe,
            List<Rect> fixedBoxes, List<Rect> placed)
        {
            var candidates = new List<Vector2>();
            if (preferred.sqrMagnitude > 1f)
                candidates.Add(preferred);

            // Radial rings around the pin: prefer clean cardinal & diagonal offsets close to the pin.
            float hw = size.x * .5f, hh = size.y * .5f;
            float[] radii = { 1f, 1.35f, 1.75f, 2.2f, 2.75f, 3.4f };
            Vector2[] dirs =
            {
                new(0, 1), new(0, -1), new(-1, 0), new(1, 0),
                new(-1, 1), new(1, 1), new(-1, -1), new(1, -1),
                new(-0.5f, 1), new(0.5f, 1), new(-0.5f, -1), new(0.5f, -1),
                new(-1, 0.5f), new(1, 0.5f), new(-1, -0.5f), new(1, -0.5f)
            };

            foreach (float r in radii)
            {
                foreach (var d in dirs)
                {
                    var dir = d.normalized;
                    float dx = dir.x * (hw + 22f * r);
                    float dy = dir.y * (hh + 20f * r);
                    candidates.Add(new Vector2(dx, dy));
                }
            }

            Vector2 best = candidates[0];
            float bestScore = float.PositiveInfinity;

            foreach (var offset in candidates)
            {
                Vector2 center = pinPos + offset;
                // Clamp gently inside safe bounds if close to edge.
                center.x = Mathf.Clamp(center.x, safe.xMin + hw, safe.xMax - hw);
                center.y = Mathf.Clamp(center.y, safe.yMin + hh, safe.yMax - hh);
                Vector2 clampedOffset = center - pinPos;

                Rect rect = Rect.MinMaxRect(center.x - hw - 2f, center.y - hh - 2f, center.x + hw + 2f, center.y + hh + 2f);
                float overlapArea = 0f;
                for (int i = 0; i < fixedBoxes.Count; i++) overlapArea += OverlapArea(rect, fixedBoxes[i]);
                for (int i = 0; i < placed.Count; i++) overlapArea += OverlapArea(rect, placed[i]) * 1.5f;

                float dist = clampedOffset.magnitude;
                float prefPenalty = preferred.sqrMagnitude > 1f ? Vector2.Distance(clampedOffset, preferred) * 0.15f : 0f;
                float score = overlapArea * 1000f + dist + prefPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = clampedOffset;
                    if (overlapArea <= 0.01f && dist < 68f) break;
                }
            }
            return best;
        }

        private static RectTransform DrawLeader(RectTransform map, string id, Vector2 from, Vector2 to, bool focused)
        {
            var go = new GameObject("引線_" + id, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(map, false);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(0f, .5f);

            Vector2 delta = to - from;
            float len = delta.magnitude;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(len, focused ? 2.6f : 2f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var img = go.GetComponent<Image>();
            img.color = focused ? new Color(.98f, .86f, .44f, .95f) : new Color(.92f, .86f, .70f, .82f);
            img.raycastTarget = false;
            return rt;
        }

        private static int CountNearby(Vector2 p, List<Rect> boxes, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            for (int i = 0; i < boxes.Count; i++)
                if ((boxes[i].center - p).sqrMagnitude <= r2) n++;
            return n;
        }

        private static Vector2 UvToLocal(Rect bounds, Vector2 uv) =>
            new(Mathf.Lerp(bounds.xMin, bounds.xMax, uv.x), Mathf.Lerp(bounds.yMin, bounds.yMax, uv.y));

        private static float OverlapArea(Rect a, Rect b)
        {
            float x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float y = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return x > 0f && y > 0f ? x * y : 0f;
        }

        private static Rect LocalBox(RectTransform map, RectTransform rect, float pad)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = map.InverseTransformPoint(corners[0]);
            Vector2 b = map.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad,
                Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);
        }
    }

    public sealed partial class SectUI
    {
        /// <summary>v1126e-ag (Item 2): fixed coordinate pin for a candidate settlement site.</summary>
        private RectTransform CreateSitePin(RectTransform map, string siteId, Vector2 uv, bool focused)
        {
            float half = focused ? 7f : 5.5f;
            var pin = Rect("選址點_" + siteId, map);
            Anchor(pin, uv, uv, new(-half, -half), new(half, half));
            pin.localRotation = Quaternion.Euler(0, 0, 45f);
            var fill = pin.gameObject.AddComponent<Image>();
            fill.color = focused ? new Color(.86f, .24f, .18f, 1f) : new Color(.94f, .80f, .40f, 1f);
            fill.raycastTarget = false;
            var rim = pin.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(.12f, .09f, .06f, .95f);
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            return pin;
        }
    }
}
