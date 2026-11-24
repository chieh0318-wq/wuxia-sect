using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1125 #6: our own mountain gate is easy to find. A large crimson 「本」 banner, a 「本門」 plaque and a
    /// pulsing gold ring stand on the home node; the map opens centred there, and a 「回到本門」 button in the
    /// viewport corner flies back to it at any time.
    /// </summary>
    public sealed partial class WorldMapScreen
    {
        private RectTransform homeMarker, homeHalo;
        private static Texture2D ringTexture;

        private string HomeNode => host != null && data.Nodes.ContainsKey(host.StartNode ?? "") ? host.StartNode : null;

        private void BuildHome()
        {
            homeMarker = MapUi.Rect(map, "本門標記", 0, 0, 120, 150);
            homeMarker.pivot = new Vector2(.5f, .30f);
            homeHalo = MapUi.Rect(homeMarker, "本門光圈", 0, 72, 120, 64);
            var halo = homeHalo.gameObject.AddComponent<RawImage>();
            halo.texture = Ring();
            halo.color = new Color(1f, .82f, .36f, .9f);
            halo.raycastTarget = false;
            // Banner pole and a large crimson cloth with 「本」.
            MapUi.Panel(homeMarker, "旗杆", 58, 0, 4, 100, MapUi.Ink).GetComponent<Image>().raycastTarget = false;
            var cloth = MapUi.Panel(homeMarker, "本門旗", 62, 4, 44, 44, new Color(.70f, .16f, .12f));
            cloth.GetComponent<Image>().raycastTarget = false;
            var rim = cloth.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(1f, .84f, .45f, .95f);
            rim.effectDistance = new Vector2(2, -2);
            var glyph = MapUi.Text(cloth, "本", 0, 0, 44, 44, 30, new Color(1f, .93f, .78f), TextAnchor.MiddleCenter, "Names");
            glyph.resizeTextForBestFit = false;
            MapUi.Panel(homeMarker, "旗穗", 62, 48, 44, 3, MapUi.Gold).GetComponent<Image>().raycastTarget = false;
            var plate = MapUi.Kit(homeMarker, "本門牌", 20, 112, 80, 34, "Plaque", false);
            var label = MapUi.Text(plate, "本門", 0, 0, 80, 34, 20, UiKit.HeaderGold, TextAnchor.MiddleCenter, "Names");
            label.resizeTextForBestFit = false;
            label.fontStyle = FontStyle.Bold;
            homeMarker.gameObject.SetActive(false);

            var back = MapUi.Button(frame, "◎ 回到本門", 40, 736, 190, 46, FocusHome, MapUi.Red, 21);
            back.name = "回到本門";
        }

        /// <summary>Flies the camera to the home node at a readable zoom.</summary>
        internal void FocusHome()
        {
            if (traveling || HomeNode == null) return;
            var node = data.Nodes[HomeNode];
            targetZoom = Mathf.Clamp(Mathf.Max(zoom, 1.7f), MinZoom, MaxZoom);
            Vector2 p = Position(node);
            targetPan = new Vector2((MapWidth / 2 - p.x) * targetZoom, (p.y - MapHeight / 2) * targetZoom);
            changingScreen = true;
        }

        /// <summary>Opening the map: jump (no easing) to the home node so the player sees where they are.</summary>
        private void CenterOnHome()
        {
            if (HomeNode == null) return;
            var node = data.Nodes[HomeNode];
            zoom = targetZoom = Mathf.Clamp(1.7f, MinZoom, MaxZoom);
            Vector2 p = Position(node);
            pan = targetPan = new Vector2((MapWidth / 2 - p.x) * zoom, (p.y - MapHeight / 2) * zoom);
        }

        private void LayoutHome()
        {
            if (homeMarker == null) return;
            string home = HomeNode;
            homeMarker.gameObject.SetActive(home != null);
            if (home == null) return;
            Vector2 p = Position(data.Nodes[home]);
            homeMarker.anchoredPosition = new Vector2(p.x, -p.y);
            homeMarker.localScale = Vector3.one * IconScale / zoom;
            homeMarker.SetAsLastSibling();
        }

        private void PulseHome()
        {
            if (homeHalo == null || !homeHalo.gameObject.activeInHierarchy) return;
            float wave = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            homeHalo.localScale = new Vector3(1 + wave * .35f, 1 + wave * .35f, 1);
            homeHalo.GetComponent<RawImage>().color = new Color(1f, .82f, .36f, .95f - wave * .55f);
        }

        private static Texture2D Ring()
        {
            if (ringTexture != null) return ringTexture;
            const int size = 128;
            ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var center = Vector2.one * (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
                float ring = Mathf.Clamp01(1 - Mathf.Abs(d - .82f) / .12f);
                float glow = Mathf.Clamp01(1 - d) * .25f;
                ringTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(ring, glow)));
            }
            ringTexture.Apply(false, true);
            return ringTexture;
        }
    }
}
