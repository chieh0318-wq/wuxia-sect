using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126e-ag (Item 4): spreads courtyard disciples across buildings, fields, and open courtyard lanes so they
    /// never crowd into a single line or overlap, and assigns each disciple preferred building/field waypoints.
    /// </summary>
    public sealed partial class Courtyard
    {
        private readonly List<(string id, Vector3 pos)> buildingSpots = new();
        private const float SpawnMinSeparation = 2.45f;
        private const float FootprintPad = 0.85f;

        private Rect WalkBounds => new(-yardWidth * .5f + 2.2f, -17.5f, yardWidth - 4.4f, 35f);

        private void RecordBuildingSpot(string buildingId, Vector3 center, float width, float depth)
        {
            if (string.IsNullOrEmpty(buildingId)) return;
            // Front entrance + side spots so multiple disciples visiting the same building or field don't stack.
            var candidates = new[]
            {
                center + new Vector3(0f, 0f, -(depth * .5f + 1.45f)),
                center + new Vector3(-Mathf.Min(2.4f, width * .45f), 0f, -(depth * .5f + 1.55f)),
                center + new Vector3(Mathf.Min(2.4f, width * .45f), 0f, -(depth * .5f + 1.55f)),
                center + new Vector3(width * .5f + 1.45f, 0f, 0f),
                center + new Vector3(-(width * .5f + 1.45f), 0f, 0f)
            };
            foreach (var spot in candidates)
                if (IsWalkableSpot(spot))
                    buildingSpots.Add((buildingId, spot));
        }

        private bool IsWalkableSpot(Vector3 pos)
        {
            var bounds = WalkBounds;
            if (!bounds.Contains(new Vector2(pos.x, pos.z))) return false;
            var pt = new Vector2(pos.x, pos.z);
            for (int i = 0; i < buildingFootprints.Count; i++)
            {
                var r = buildingFootprints[i];
                var padded = Rect.MinMaxRect(r.xMin - FootprintPad, r.yMin - FootprintPad,
                    r.xMax + FootprintPad, r.yMax + FootprintPad);
                if (padded.Contains(pt)) return false;
            }
            return true;
        }

        /// <summary>Preferred building/field ids for a disciple based on their production skills and role.</summary>
        private static string[] PreferredBuildingsFor(DiscipleData data)
        {
            if (data == null) return Array.Empty<string>();
            var prefs = new List<string>();
            string bestProd = data.lines?
                .Where(l => l != null && !RankSystem.IsCombat(l.id))
                .OrderByDescending(l => RankSystem.Index(l.rank))
                .Select(l => l.id)
                .FirstOrDefault();

            switch (bestProd)
            {
                case "farming":
                    prefs.AddRange(new[] { "field", "vegetable", "kitchen" });
                    break;
                case "medicine":
                case "alchemy":
                    prefs.AddRange(new[] { "infirmary", "hall" });
                    break;
                case "forging":
                    prefs.AddRange(new[] { "smithy", "workshop", "yard" });
                    break;
                case "commerce":
                    prefs.AddRange(new[] { "steward_office", "teahouse", "workshop" });
                    break;
                case "teaching":
                    prefs.AddRange(new[] { "library", "master_pavilion", "yard" });
                    break;
                case "construction":
                    prefs.AddRange(new[] { "council", "logging", "mine", "quarters" });
                    break;
            }

            if (prefs.Count == 0)
                prefs.AddRange(new[] { "yard", "hall", "library", "quarters" });
            return prefs.ToArray();
        }

        private List<Vector3> PreferredSpotsFor(DiscipleData data)
        {
            var prefs = PreferredBuildingsFor(data);
            var spots = new List<Vector3>();
            foreach (string id in prefs)
            {
                foreach (var entry in buildingSpots)
                    if (entry.id == id) spots.Add(entry.pos);
            }
            return spots;
        }

        private List<Vector3> WanderSpots()
        {
            var spots = new List<Vector3>();
            foreach (var e in entrances)
                if (IsWalkableSpot(e)) spots.Add(e);

            // Spread wander points along courtyard lanes and open spaces.
            float halfW = yardWidth * .5f - 3.2f;
            float[] lanesZ = { -14.2f, -4.0f, 5.8f, 13.8f };
            foreach (float z in lanesZ)
            {
                for (float x = -halfW; x <= halfW; x += 4.5f)
                {
                    var p = new Vector3(x, 0f, z);
                    if (IsWalkableSpot(p)) spots.Add(p);
                }
            }
            foreach (var extra in new[] { new Vector3(5f, 0f, -9f), new Vector3(5f, 0f, 1f), new Vector3(5f, 0f, 9.5f), new Vector3(0f, 0f, 9.5f) })
                if (IsWalkableSpot(extra)) spots.Add(extra);
            return spots;
        }

        /// <summary>Finds a non-overlapping spawn position distributed across preferred buildings and courtyard lanes.</summary>
        private Vector3 FindSpreadSpawn(DiscipleData data)
        {
            var occupied = discipleTargets.Keys
                .Where(k => k != null && k.transform.parent != null)
                .Select(k => k.transform.parent.position)
                .ToList();

            var pool = new List<Vector3>();
            pool.AddRange(PreferredSpotsFor(data));
            pool.AddRange(WanderSpots());

            // Deterministic per-disciple offset so repeated runs look natural and well-distributed.
            int hash = 17;
            foreach (char c in data?.id ?? data?.name ?? "") hash = unchecked(hash * 31 + c);
            int shift = pool.Count > 0 ? Math.Abs(hash) % pool.Count : 0;

            foreach (float minDist in new[] { SpawnMinSeparation, 1.95f, 1.45f })
            {
                float minSqr = minDist * minDist;
                for (int i = 0; i < pool.Count; i++)
                {
                    Vector3 candidate = pool[(i + shift) % pool.Count];
                    if (occupied.All(o => FlatSqrDist(o, candidate) >= minSqr))
                        return candidate;
                }
            }

            // Fallback: pick the point in the pool farthest from any occupied disciple.
            Vector3 best = new Vector3(0f, 0f, -4f);
            float bestDist = -1f;
            foreach (var candidate in pool)
            {
                float nearest = occupied.Count == 0 ? 999f : occupied.Min(o => FlatSqrDist(o, candidate));
                if (nearest > bestDist)
                {
                    bestDist = nearest;
                    best = candidate;
                }
            }
            return best;
        }

        private static float FlatSqrDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
