using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1124 C16: fixed one-time 寶物秘境 marked on the world map. Gold seals are open explorations, vermilion seals
    /// are guarded by a master or a beast, grey seals are already taken. Clicking one opens its card; entering needs
    /// the party on that stronghold and 1 AP (the bootstrap plays the scene).
    /// </summary>
    public sealed partial class WorldMapScreen
    {
        private readonly List<(TreasureSite site, RectTransform marker, Vector2 point)> treasureMarkers = new();
        internal event Action<TreasureSite> TreasureRequested;

        private bool Claimed(TreasureSite site) => host is GameState game && game.TreasureClaimed(site.id);

        private void BuildTreasures()
        {
            treasureMarkers.Clear();
            foreach (var site in RealmContent.Data.treasures)
            {
                if (!data.Nodes.TryGetValue(site.node ?? "", out var node)) continue;
                var button = MapUi.Button(map, "", 0, 0, 30, 30, () => ShowTreasure(site), Color.clear);
                button.name = "寶物秘境:" + site.id;
                var rect = (RectTransform)button.transform;
                rect.pivot = new Vector2(.5f, .5f);
                treasureMarkers.Add((site, rect, Position(node)));
            }
            RefreshTreasures();
        }

        /// <summary>Repaints every seal for its current state (taken / guarded / open).</summary>
        private void RefreshTreasures()
        {
            foreach (var (site, marker, _) in treasureMarkers)
            {
                foreach (Transform child in marker) Destroy(child.gameObject);
                bool taken = Claimed(site);
                // Gold-rimmed seals so they read apart from the faction flags: cream = explore, vermilion = guarded, grey = taken.
                Color seal = taken ? new Color(.42f, .40f, .37f) : site.Guarded ? MapUi.Red : MapUi.Paper;
                MapUi.Panel(marker, "印框", 0, 0, 30, 30, taken ? MapUi.Ink : new Color(.93f, .76f, .30f)).GetComponent<Image>().raycastTarget = false;
                MapUi.Panel(marker, "印面", 3, 3, 24, 24, seal).GetComponent<Image>().raycastTarget = false;
                var emblem = MapUi.Rect(marker, "寶物秘境圖示", 4, 4, 22, 22).gameObject.AddComponent<RawImage>();
                emblem.texture = UiKit.Icon(site.Guarded ? "realm_guardian" : "realm_treasure");
                emblem.color = taken ? new Color(.58f, .58f, .58f) : Color.white;
                emblem.raycastTarget = false;
                marker.SetAsLastSibling();
            }
        }

        /// <summary>Keeps seals a constant on-screen size beside their stronghold (called from LayoutLabels).</summary>
        private void LayoutTreasures()
        {
            foreach (var (site, marker, point) in treasureMarkers)
            {
                marker.localScale = Vector3.one / zoom;
                Vector2 local = point + new Vector2(site.dx, site.dy) * IconScale / zoom;
                marker.anchoredPosition = new Vector2(local.x, -local.y);
            }
        }

        private void ShowTreasure(TreasureSite site)
        {
            if (traveling || !data.Nodes.TryGetValue(site.node ?? "", out var node)) return;
            bool taken = Claimed(site);
            var panel = Card(site.name, $"寶物秘境・{node.Label}　{site.KindLabel}　{(taken ? "已取" : "未取")}");
            FocusMarker(node);
            var icon = UiKit.Icon(site.TypeIcon);
            if (icon != null)
            {
                var image = MapUi.Rect(panel, "寶物", 20, 116, 72, 72).gameObject.AddComponent<RawImage>();
                image.texture = icon;
                image.raycastTarget = false;
            }
            MapUi.Text(panel, site.TypeLabel, 104, 112, 300, 28, 18, MapUi.Red);
            MapUi.Text(panel, site.treasure, 104, 140, 302, 48, 24, MapUi.Ink, TextAnchor.MiddleLeft, "Names");
            int y = 200;
            if (site.Guarded)
            {
                var keeper = site.guardian;
                var face = keeper.portrait != null && !keeper.portrait.StartsWith("beast_", StringComparison.Ordinal)
                    ? PortraitService.Resolve(keeper.portrait, keeper.gender) : null;
                MapUi.Portrait(panel, face, 20, y, 84, 104);
                if (face == null) MapUi.Text(panel, "獸", 20, y, 84, 104, 40, MapUi.Paper, TextAnchor.MiddleCenter, "Names");
                MapUi.Text(panel, "鎮守", 116, y, 290, 26, 18, MapUi.Red);
                MapUi.Text(panel, keeper.name, 116, y + 26, 290, 36, 26, MapUi.Ink, TextAnchor.MiddleLeft, "Names");
                MapUi.Text(panel, $"{keeper.subtitle}　須擊敗方能取寶", 116, y + 64, 290, 30, 17, MapUi.Ink);
                y += 116;
            }
            else
            {
                MapUi.Text(panel, "無人鎮守，闖過機關險地即可取寶。", 20, y, 386, 30, 19, MapUi.Jade);
                y += 38;
            }
            var intro = MapUi.Text(panel, site.intro, 20, y, 386, 150, 18, MapUi.Ink, TextAnchor.UpperLeft, "Narration");
            intro.verticalOverflow = VerticalWrapMode.Truncate;
            y += 156;
            MapUi.Text(panel, "取得", 20, y, 60, 26, 18, MapUi.Red);
            MapUi.Text(panel, EffectText.Describe(site.rewards, host as GameState), 80, y, 326, 56, 17, MapUi.Jade, TextAnchor.UpperLeft);
            y += 62;

            var route = data.Route(host.PartyNode, node.id);
            bool here = node.id == host.PartyNode;
            string message = taken ? "秘寶已經取走。" : here ? "隊伍就在此地，可入境探寶（行動力 1）。" :
                route == null ? "沒有可通的道路" : $"須先抵達：行軍 {route.Days} 日" + (host.TravelCost(route.Ap) > 0 ? $"　行動力 {host.TravelCost(route.Ap)}" : "");
            MapUi.Text(panel, message, 20, y, 386, 40, 18, taken ? MapUi.Ink : MapUi.Jade, TextAnchor.UpperLeft);

            var enter = MapUi.Button(panel, "入境探寶\n行動力 1", 20, 610, 124, 48, () => TreasureRequested?.Invoke(site), MapUi.Red, 16);
            enter.name = "入境探寶";
            enter.interactable = !taken && here && host.ActionPoints >= 1;
            cardActions.Add(enter);
            var stronghold = MapUi.Button(panel, "查看據點", 151, 610, 124, 48, () => ShowNode(node), null, 18);
            cardActions.Add(stronghold);
            var go = MapUi.Button(panel, "確認啟程", 282, 610, 124, 48, () => StartTravel(route, node.id), MapUi.Red, 18);
            go.interactable = !here && route != null && host.ActionPoints >= host.TravelCost(route.Ap);
            cardActions.Add(go);
        }

        /// <summary>Capture hook: opens a treasure card by id (or the first one).</summary>
        internal void CaptureTreasure(string id = null)
        {
            var site = RealmContent.Treasure(id) ?? RealmContent.Data.treasures.FirstOrDefault();
            if (site != null)
            {
                if (data.Nodes.TryGetValue(site.node, out var node)) CenterOn(node.id);
                ShowTreasure(site);
            }
        }
    }
}
