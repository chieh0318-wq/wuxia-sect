using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126e #2: the settlement map keeps every icon on its true map coordinate — faction flags at their seat, and a
    /// small dot at each site — and only the site name plates move. Once the map has its size (so it works at any
    /// resolution), each plate takes the nearest free spot around its site (centered first, then rings of offsets),
    /// clear of every flag, every other plate and dot, the colour key and the map edge; a plate that had to move gets a
    /// leader line back to its dot. Replaces the v1126c guard, which pushed the flags off their seats instead.
    /// </summary>
    internal sealed class SiteLabelLeaders : MonoBehaviour
    {
        private bool done;
        private int sizedFrames;
        internal int Moved { get; private set; }
        internal int Overlaps { get; private set; }

        private void LateUpdate()
        {
            if (done) return;
            var map = (RectTransform)transform;
            if (map.rect.width <= 1) return;
            // Wait one more frame once the map has a size, so the layout above it has settled.
            if (++sizedFrames < 2) return;
            done = true;
            Place(map);
        }

        private void Place(RectTransform map)
        {
            var bounds = map.rect;
            var obstacles = new List<Rect>();
            var markers = new List<RectTransform>();
            foreach (Transform child in map)
            {
                var rect = (RectTransform)child;
                if (child.name.StartsWith("選址_", System.StringComparison.Ordinal)) markers.Add(rect);
                else if (child.name.StartsWith("附近勢力_", System.StringComparison.Ordinal)) obstacles.Add(Box(map, rect, 3));
                else if (child.name == "關係圖例") obstacles.Add(Box(map, rect, 4));
            }
            // Every site's true point, taken before anything moves (the plates are centered on it).
            var points = markers.Select(marker => Box(map, marker, 0).center).ToList();
            foreach (var point in points) obstacles.Add(new Rect(point - new Vector2(7, 7), new Vector2(14, 14)));

            Moved = Overlaps = 0;
            for (int index = 0; index < markers.Count; index++)
            {
                var marker = markers[index];
                var point = points[index];
                var size = Box(map, marker, 0).size;
                var own = new Rect(point - new Vector2(7, 7), new Vector2(14, 14));
                Vector2 best = Vector2.zero;
                float bestCost = float.MaxValue;
                foreach (var offset in Offsets(size))
                {
                    var plate = new Rect(point + offset - size / 2, size);
                    var padded = new Rect(plate.xMin - 3, plate.yMin - 3, plate.width + 6, plate.height + 6);
                    float cost = 0;
                    foreach (var other in obstacles)
                        if (other != own && padded.Overlaps(other)) cost += Area(padded, other) + 400;
                    if (plate.xMin < bounds.xMin + 2 || plate.xMax > bounds.xMax - 2 || plate.yMin < bounds.yMin + 2 || plate.yMax > bounds.yMax - 2)
                        cost += 1e6f;
                    cost += offset.magnitude * .01f; // nearer wins among the free spots
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = offset;
                    }
                    if (cost < 1) break;
                }
                if (bestCost >= 1) Overlaps++;
                marker.anchoredPosition += best;
                var placed = new Rect(point + best - size / 2, size);
                obstacles.Add(placed);
                Dot(map, marker.name, point);
                if (best != Vector2.zero)
                {
                    Moved++;
                    Leader(map, marker.name, point, Nearest(placed, point));
                }
            }
            // Plates (and their lines and dots) above the flags, so a name is never hidden.
            foreach (Transform child in map.Cast<Transform>().ToArray())
                if (child.name.StartsWith("選址引線_", System.StringComparison.Ordinal)) child.SetAsLastSibling();
            foreach (Transform child in map.Cast<Transform>().ToArray())
                if (child.name.StartsWith("選址點_", System.StringComparison.Ordinal)) child.SetAsLastSibling();
            foreach (var marker in markers) marker.SetAsLastSibling();
            Debug.Log($"[SiteMap] {markers.Count} plates, {Moved} moved with leader lines, {Overlaps} still touching");
        }

        /// <summary>Centered first, then rings of spots around the site, nearest first.</summary>
        private static IEnumerable<Vector2> Offsets(Vector2 size)
        {
            yield return Vector2.zero;
            float dx = size.x / 2 + 14, dy = size.y / 2 + 10;
            for (int ring = 1; ring <= 5; ring++)
            {
                var spots = new List<Vector2>();
                foreach (int sy in new[] { 1, -1 })
                    spots.Add(new Vector2(0, sy * dy * ring));
                foreach (int sx in new[] { 1, -1 })
                {
                    spots.Add(new Vector2(sx * dx * ring, 0));
                    foreach (int sy in new[] { 1, -1 })
                    {
                        spots.Add(new Vector2(sx * dx * ring, sy * dy * ring));
                        spots.Add(new Vector2(sx * dx * ring * .5f, sy * dy * ring));
                    }
                }
                foreach (var spot in spots.OrderBy(spot => spot.magnitude)) yield return spot;
            }
        }

        private static float Area(Rect a, Rect b) =>
            Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

        private static Vector2 Nearest(Rect rect, Vector2 point) =>
            new(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));

        private static void Dot(RectTransform map, string name, Vector2 point)
        {
            var dot = new GameObject("選址點_" + name.Substring(3), typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            dot.SetParent(map, false);
            Center(map, dot, point, new Vector2(12, 12));
            var image = dot.GetComponent<Image>();
            image.color = new Color(.98f, .86f, .42f);
            image.raycastTarget = false;
            var rim = dot.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(.12f, .08f, .05f, .9f);
            rim.effectDistance = new Vector2(2, -2);
        }

        private static void Leader(RectTransform map, string name, Vector2 from, Vector2 to)
        {
            var line = new GameObject("選址引線_" + name.Substring(3), typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            line.SetParent(map, false);
            var delta = to - from;
            Center(map, line, (from + to) / 2, new Vector2(delta.magnitude, 3));
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = line.GetComponent<Image>();
            image.color = new Color(.15f, .10f, .06f, .92f);
            image.raycastTarget = false;
        }

        /// <summary>Places a child centered on a point given in the map's local space.</summary>
        private static void Center(RectTransform map, RectTransform child, Vector2 local, Vector2 size)
        {
            var rect = map.rect;
            var anchor = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            child.anchorMin = child.anchorMax = anchor;
            child.pivot = new Vector2(.5f, .5f);
            child.sizeDelta = size;
            child.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// The child's box in the map's local space. Every child here is anchored to a single point, so the box comes
        /// straight from the anchors (exact even before the canvas has moved the child — a redrawn map reuses the panel
        /// and its children's world positions are stale on the first frame, which threw the CL2 first try off).
        /// </summary>
        private static Rect Box(RectTransform map, RectTransform rect, float pad)
        {
            if (rect.anchorMin == rect.anchorMax)
            {
                var area = map.rect;
                var anchor = new Vector2(area.xMin + rect.anchorMin.x * area.width, area.yMin + rect.anchorMin.y * area.height);
                var size = rect.sizeDelta;
                var min = anchor + rect.anchoredPosition - Vector2.Scale(rect.pivot, size);
                return Rect.MinMaxRect(min.x - pad, min.y - pad, min.x + size.x + pad, min.y + size.y + pad);
            }
            return WorldBox(map, rect, pad);
        }

        private static Rect WorldBox(RectTransform map, RectTransform rect, float pad)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = map.InverseTransformPoint(corners[0]), b = map.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);
        }
    }
}
