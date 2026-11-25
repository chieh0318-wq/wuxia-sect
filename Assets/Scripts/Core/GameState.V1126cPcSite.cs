using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        /// <summary>v1126c PC3 #8: the chosen site's lasting yield (prologue.json siteSilver / siteFood keys), every month.</summary>
        internal (int silver, int food) SiteMonthly => (Story.siteSilver, Story.siteFood);

        private void SiteMonth(MonthlyReport report)
        {
            CountPlayedMonth(); // v1126c PC3 #15
            if (Story.siteSilver == 0 && Story.siteFood == 0) return;
            Silver = Mathf.Max(0, Silver + Story.siteSilver);
            Food = Mathf.Max(0, Food + Story.siteFood);
            report.Lines.Add($"山門地利　銀兩 {Story.siteSilver:+0;-0;0}、糧食 {Story.siteFood:+0;-0;0}");
        }
    }

    /// <summary>
    /// v1126c PC3 #8: every opening-site effect as a number read from the same effect list <see cref="GameState.ApplyEffects"/>
    /// applies; one-off amounts say 開局, lasting ones say 每月 or 永久.
    /// </summary>
    internal static class SiteEffects
    {
        internal static List<string> Lines(GameEffect[] effects)
        {
            var lines = new List<string>();
            foreach (var effect in effects ?? Array.Empty<GameEffect>())
            {
                if (effect == null) continue;
                int value = Mathf.RoundToInt(effect.value);
                string signed = value.ToString("+0;−0;0");
                switch (effect.key)
                {
                    case "siteSilver": lines.Add($"每月銀兩 {signed}"); break;
                    case "siteFood": lines.Add($"每月糧食 {signed}"); break;
                    case "silver": lines.Add($"開局銀兩 {signed}"); break;
                    case "food": lines.Add($"開局糧食 {signed}"); break;
                    case "reputation": lines.Add($"開局聲望 {signed}"); break;
                    case "buildCostPct": lines.Add($"興建費用 {signed}%（永久）"); break;
                    case "trainBonusPct": lines.Add($"修習進度 {signed}%（永久）"); break;
                    case "insightChance": lines.Add($"感悟機率 {effect.value * 100:+0;−0;0}%（奇遇頓悟、秘境判定）"); break;
                    case "recruitPoolSize": lines.Add($"招徒候選 {signed} 人"); break;
                    case "recruitCostPct": lines.Add($"招徒花費 {signed}%"); break;
                    case "apMax": lines.Add($"行動力上限 {signed}"); break;
                    case "loyaltyAll": lines.Add($"門人忠誠 {signed}"); break;
                    case "lineRank": lines.Add($"主角{EffectText.LineName(effect.@ref)} {signed} 階"); break;
                    case "cap": lines.Add($"主角{EffectText.LineName(effect.@ref)}上限 {signed} 階"); break;
                    case "relation":
                        lines.Add($"與{GameState.FindFaction(effect.@ref)?.Label ?? effect.@ref}關係 {signed}"); break;
                    case "flag": break;
                    default: lines.Add(EffectText.Describe(effect, null)); break;
                }
            }
            return lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        }
    }

    /// <summary>
    /// v1126c PC3 #7: nearby powers for an opening site, measured on the painted map with the same normalized coordinates the
    /// settlement map draws its markers and flags at (<see cref="MapBounds.Uv"/>), so the list always matches what is on
    /// screen. Powers sharing a seat are one entry (the largest first). Travel days are the straight-line distance times the
    /// median days-per-distance of all roads on the same map.
    /// </summary>
    internal static class SiteGeography
    {
        /// <summary>Width / height of the painted map (world_map_san10.png is 1664 × 936).</summary>
        internal const float MapAspect = 1664f / 936f;
        private static float daysPerUnit = -1;

        internal readonly struct Neighbour
        {
            public readonly MapFaction Faction;
            public readonly MapFaction[] Seat;
            public readonly float Distance;
            public readonly int Days;
            public Neighbour(MapFaction faction, MapFaction[] seat, float distance, int days)
            {
                Faction = faction; Seat = seat; Distance = distance; Days = days;
            }
            public string SeatNames => Seat.Length <= 1 ? Faction.Label : $"{Faction.Label}等 {Seat.Length} 家";
        }

        internal static float Distance(Vector2 a, Vector2 b) => Vector2.Distance(new(a.x * MapAspect, a.y), new(b.x * MapAspect, b.y));

        internal static float DaysPerUnit(WorldMapData data)
        {
            if (daysPerUnit > 0) return daysPerUnit;
            var rates = data.Roads.Where(road => data.Nodes.ContainsKey(road.a) && data.Nodes.ContainsKey(road.b))
                .Select(road => (road.days, far: Distance(data.Bounds.Uv(data.Nodes[road.a]), data.Bounds.Uv(data.Nodes[road.b]))))
                .Where(entry => entry.far > .001f).Select(entry => entry.days / entry.far).OrderBy(rate => rate).ToArray();
            daysPerUnit = rates.Length == 0 ? 40 : rates[rates.Length / 2];
            return daysPerUnit;
        }

        internal static int Days(WorldMapData data, float distance) => Mathf.Max(1, Mathf.RoundToInt(distance * DaysPerUnit(data)));

        /// <summary>The <paramref name="count"/> nearest seats to <paramref name="node"/> (by drawn position).</summary>
        internal static Neighbour[] Nearby(WorldMapData data, string node, int count)
        {
            if (!data.Nodes.TryGetValue(node ?? "", out var origin)) return Array.Empty<Neighbour>();
            var point = data.Bounds.Uv(origin);
            return Seats(data).Select(seat =>
                {
                    float far = Distance(point, data.Bounds.Uv(data.Nodes[seat.Key]));
                    return new Neighbour(seat[0], seat.ToArray(), far, seat.Key == node ? 0 : Days(data, far));
                })
                .OrderBy(entry => entry.Distance).Take(count).ToArray();
        }

        /// <summary>Factions grouped by seat node, largest (人馬) first within a seat.</summary>
        internal static IEnumerable<IGrouping<string, MapFaction>> SeatGroups(WorldMapData data) =>
            data.Factions.Where(f => data.Nodes.ContainsKey(f.seat ?? "")).GroupBy(f => f.seat);

        private static IEnumerable<SeatList> Seats(WorldMapData data) =>
            SeatGroups(data).Select(group => new SeatList(group.Key, group.OrderByDescending(f => f.disciples).ToArray()));

        private sealed class SeatList : List<MapFaction>
        {
            public readonly string Key;
            public SeatList(string key, IEnumerable<MapFaction> factions) : base(factions) => Key = key;
        }
    }
}
