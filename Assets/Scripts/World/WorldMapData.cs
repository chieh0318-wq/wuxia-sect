using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public interface IWorldMapHost
    {
        int ActionPoints { get; }
        string DateText { get; }
        bool TrySpendAp(int cost, string reason);
        void AdvanceDays(int days);
        string PartyNode { get; set; }
        string StartNode { get; }
        bool IsFactionKnown(string id);
        void SetFactionKnown(string id);
        /// <summary>Saved relation delta (prologue/story `relation` effects, -100..100) for a faction.</summary>
        int RelationShift(string id);
        /// <summary>Rolls a roadside qi-yu (adventure) on arrival, driven by 幸運; returns a message or null.</summary>
        string TravelFortune();
        /// <summary>M6: road AP after tech discounts, sect-held strongholds and 率眾攻討.</summary>
        int TravelCost(int ap);
        bool HoldsNode(string id);
        string SectLabel { get; }
        int SectPower { get; }
        float AssaultChance(int garrison, float guard);
        string Assault(string nodeId, string factionId, int garrison, float guard);
    }

    // GameState (Core/GameState.World.cs) is the save-backed IWorldMapHost.

    [Serializable] internal sealed class MapBounds
    {
        public float lonMin = 70, lonMax = 136, latMin = 15, latMax = 55;
        public Vector2 Uv(MapNode node) => node.x >= 0 && node.y >= 0 ? new Vector2(node.x, 1 - node.y) : new Vector2(
            Mathf.InverseLerp(lonMin,lonMax,node.lon), Mathf.InverseLerp(latMin,latMax,node.lat));
    }
    [Serializable] internal sealed class MapNode
    {
        public string id, name, type, region, desc;
        public float lon, lat;
        /// <summary>M6 spread layout on the painting: normalized 0..1, y measured from the top (-1 = use lon/lat).</summary>
        public float x = -1, y = -1;
        public string Label => string.IsNullOrWhiteSpace(name) ? id : name;
    }
    [Serializable] internal sealed class MapRoad
    {
        public string a, b;
        public int days = 1, ap = 1;
        public string Other(string id) => a == id ? b : a;
    }
    [Serializable] internal sealed class FactionNotable
    {
        public string name, title, rank, cap, desc;
    }
    [Serializable] internal sealed class MapSkillLine
    {
        public string id, rank, cap;
        internal static readonly IReadOnlyDictionary<string,string> Combat = new Dictionary<string,string>
        {
            ["fist"]="拳掌",["blade"]="刀",["sword"]="劍",["hidden"]="暗器",
            ["qinggong"]="輕功",["neigong"]="內功",["polearm"]="槍棍"
        };
        internal static readonly IReadOnlyDictionary<string,string> Production = new Dictionary<string,string>
        {
            ["medicine"]="醫術",["alchemy"]="煉丹製藥",["forging"]="鍛造",["commerce"]="經商",
            ["farming"]="耕作",["construction"]="建築",["teaching"]="教導"
        };
    }
    [Serializable] internal sealed class MapFaction
    {
        public string id, name, type, color, seat, stance, leader, leaderId, desc, leaderRank, leaderCap, leaderWeapon;
        public string[] martialArts, territory;
        public FactionNotable[] notables;
        public MapSkillLine[] leaderLines;
        public int reputation, disciples, money;
        public bool special, known = true;
        public string Label => string.IsNullOrWhiteSpace(name) ? id : name;
        public string Relation => RelationLabel(stance);
        /// <summary>Authored stance shifted by the saved relation delta: base friendly +50 / neutral 0 / hostile -50, then ±25 thresholds.</summary>
        public string StanceWith(int shift)
        {
            int score = (stance == "friendly" ? 50 : stance == "hostile" ? -50 : 0) + shift;
            return score >= 25 ? "friendly" : score <= -25 ? "hostile" : "neutral";
        }
        public string RelationWith(int shift) => RelationLabel(StanceWith(shift));
        private static string RelationLabel(string value) => value == "friendly" ? "友好" : value == "hostile" ? "敵對" : "中立";
        public bool Court => special || type is "court" or "military" or "foreign";
        /// <summary>草莽勢力: bandit / outlaw holds, weakly garrisoned and open to 率眾攻討.</summary>
        public bool Outlaw => type == "bandit" || type == "outlaw";
        public string TypeName => type switch
        {
            "sect" => "門派", "court" => "朝廷", "military" => "軍鎮", "clan" => "世家",
            "merchant" => "商幫", "bandit" => "山寨", "foreign" => "外邦", _ => "其他"
        };
        public Color Tint => ColorUtility.TryParseHtmlString(color, out var tint) ? tint : new Color(.47f,.26f,.16f);
        public string PortraitId => string.IsNullOrWhiteSpace(leaderId)
            ? id switch
            {
                "huayue" => "elder_villain", "funiu" => "rival_head", "heishan" => "warlord",
                _ => "leader_" + id
            }
            : leaderId;

        public bool Controls(string node) => seat == node || (territory?.Contains(node) ?? false);
        public string WeaponLabel
        {
            get
            {
                var weapon = RankSystem.Catalog.breakthroughs?.FirstOrDefault(entry => entry.id == leaderWeapon);
                if (weapon != null && !string.IsNullOrWhiteSpace(weapon.name))
                    return weapon.effectiveRankDelta > 0 ? $"{weapon.name}（武學 +{weapon.effectiveRankDelta}）" : weapon.name;
                return string.IsNullOrEmpty(leaderWeapon) ? "" : "神兵";
            }
        }
        public string Arts => string.Join("、", martialArts ?? Array.Empty<string>());
    }
    internal sealed class MapRoute
    {
        public readonly List<string> Nodes = new();
        public int Days, Ap;
    }

    internal sealed class WorldMapData
    {
        [Serializable] private sealed class MapFile
        {
            public MapBounds bounds;
            public MapNode[] nodes;
            public MapRoad[] edges;
        }
        [Serializable] private sealed class FactionFile { public MapFaction[] factions; }
        public readonly Dictionary<string,MapNode> Nodes = new();
        public readonly List<MapFaction> Factions = new();
        public readonly List<MapRoad> Roads = new();
        public readonly MapBounds Bounds;
        private readonly Dictionary<string,List<MapRoad>> connected = new();
        private static WorldMapData loaded;
        public string DefaultNode => Nodes.Values.FirstOrDefault(n => n.type == "sect")?.id ?? Nodes.Keys.FirstOrDefault();
        public static WorldMapData Load() => loaded ??= new WorldMapData();

        private WorldMapData()
        {
            var map = Parse<MapFile>("worldmap.json");
            Bounds = map?.bounds ?? new MapBounds();
            foreach (var node in map?.nodes ?? Array.Empty<MapNode>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id) || !Nodes.TryAdd(node.id,node)) continue;
                connected.Add(node.id,new List<MapRoad>());
            }
            foreach (var road in map?.edges ?? Array.Empty<MapRoad>())
            {
                if (road == null || string.IsNullOrEmpty(road.a) || string.IsNullOrEmpty(road.b) ||
                    !connected.TryGetValue(road.a,out var a) || !connected.TryGetValue(road.b,out var b)) continue;
                road.days = Mathf.Max(1,road.days);
                road.ap = Mathf.Max(0,road.ap);
                Roads.Add(road);
                a.Add(road);
                b.Add(road);
            }
            var ids = new HashSet<string>();
            foreach (var faction in Parse<FactionFile>("factions.json")?.factions ?? Array.Empty<MapFaction>())
                if (faction != null && !string.IsNullOrWhiteSpace(faction.id) && ids.Add(faction.id)) Factions.Add(faction);
        }

        private static T Parse<T>(string name) where T : class
        {
            string path = Path.Combine(Application.streamingAssetsPath,"Data",name);
            try { return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null; }
            catch (Exception error) { Debug.LogWarning($"[WorldMap] {name}: {error.Message}"); return null; }
        }

        public IEnumerable<MapNode> Holdings(MapFaction faction) =>
            Nodes.Values.Where(node => faction.Controls(node.id));

        public IEnumerable<MapFaction> Controllers(MapNode node) => Factions.Where(faction => faction.Controls(node.id))
            .OrderByDescending(faction => faction.seat == node.id).ThenByDescending(faction => faction.disciples);

        public IReadOnlyList<MapRoad> Connections(MapNode node) => connected[node.id];

        public string NodeLabel(string id) => Nodes.TryGetValue(id ?? "",out var node) ? node.Label : "—";

        // Dijkstra orders by elapsed days, breaking ties by AP.
        public MapRoute Route(string start, string destination)
        {
            if (!Nodes.ContainsKey(start ?? "") || !Nodes.ContainsKey(destination ?? "") || start == destination)
                return null;
            var visited = new HashSet<string>();
            var costs = new Dictionary<string,(int days,int ap)> { [start] = (0,0) };
            var previous = new Dictionary<string,string>();
            while (true)
            {
                var next = costs.Where(p => !visited.Contains(p.Key)).OrderBy(p => p.Value).FirstOrDefault();
                if (next.Key == null) return null;
                if (next.Key == destination)
                {
                    var route = new MapRoute { Days = next.Value.days, Ap = next.Value.ap };
                    for (string id = destination; id != start; id = previous[id]) route.Nodes.Add(id);
                    route.Nodes.Add(start);
                    route.Nodes.Reverse();
                    return route;
                }
                visited.Add(next.Key);
                foreach (var road in connected[next.Key])
                {
                    string neighbour = road.Other(next.Key);
                    if (visited.Contains(neighbour)) continue;
                    var cost = (next.Value.days + road.days, next.Value.ap + road.ap);
                    if (costs.TryGetValue(neighbour,out var old) && cost.CompareTo(old) >= 0) continue;
                    costs[neighbour] = cost;
                    previous[neighbour] = next.Key;
                }
            }
        }
    }
}
