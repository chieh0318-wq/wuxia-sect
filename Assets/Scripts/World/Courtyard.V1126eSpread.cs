using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126e #4: where courtyard people go. Everyone starts at an open spot of their own; afterwards each walk ends either
    /// at the building their strongest art belongs to (inside a field or the 演武場, in front of a hall) or at an open
    /// spot nearby. A spot inside someone's personal space — where they stand or where they are heading — is never picked.
    /// </summary>
    public sealed partial class Courtyard
    {
        private readonly List<(string id, Vector3 center, Rect footprint)> workSites = new();

        private static readonly string[] OpenGround = { "field", "vegetable", "yard" };

        /// <summary>The walkable inside of the walls.</summary>
        private Rect YardArea => Rect.MinMaxRect(-yardWidth / 2 + 1.8f, -19.2f, yardWidth / 2 - 1.8f, 18.6f);

        internal static int StableSeed(string text)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in text ?? "") hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>Buildings a disciple works at, from their highest-ranked art (combat arts train at the 演武場).</summary>
        internal static string[] WorkBuildingsOf(DiscipleData data)
        {
            var top = (data?.lines ?? System.Array.Empty<SkillLine>()).Where(line => line != null && !string.IsNullOrEmpty(line.id))
                .OrderByDescending(line => RankSystem.Index(line.rank)).ThenByDescending(line => RankSystem.Index(line.cap)).FirstOrDefault();
            if (top == null) return System.Array.Empty<string>();
            return top.id switch
            {
                "farming" => new[] { "field", "vegetable" },
                "medicine" or "alchemy" => new[] { "infirmary" },
                "forging" => new[] { "smithy", "workshop" },
                "construction" => new[] { "workshop", "logging", "mine" },
                "commerce" => new[] { "teahouse", "steward_office", "kitchen" },
                "teaching" => new[] { "library", "hall" },
                _ => RankSystem.IsCombat(top.id) ? new[] { "yard" } : System.Array.Empty<string>()
            };
        }

        private bool OpenSpot(Vector2 point, float pad, Rect? allowed = null) =>
            YardArea.Contains(point) && !buildingFootprints.Any(footprint => footprint != allowed &&
                Rect.MinMaxRect(footprint.xMin - pad, footprint.yMin - pad, footprint.xMax + pad, footprint.yMax + pad).Contains(point));

        private Vector3 SpawnSpot(System.Random rng)
        {
            var others = discipleTargets.Keys.Where(hit => hit != null).Select(hit => hit.transform.parent.position).ToList();
            var area = YardArea;
            Vector3 best = new(0, 0, -12);
            float bestGap = -1;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                // Mostly the southern half the camera opens on, some further north.
                float z = attempt % 3 == 2 ? Mathf.Lerp(area.yMin, area.yMax, (float)rng.NextDouble())
                    : Mathf.Lerp(area.yMin, 6f, (float)rng.NextDouble());
                var point = new Vector3(Mathf.Lerp(area.xMin, area.xMax, (float)rng.NextDouble()), 0, z);
                if (!OpenSpot(new Vector2(point.x, point.z), .8f)) continue;
                float gap = others.Count == 0 ? 99 : others.Min(other => DiscipleWalker.Gap(point, other));
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = point;
                }
                if (gap >= 1.25f) break;
            }
            return best;
        }

        /// <summary>The next place <paramref name="walker"/> walks to (see the class summary).</summary>
        internal Vector3 NextSpot(DiscipleWalker walker)
        {
            var rng = walker.Rng;
            var here = walker.Flat;
            var sites = workSites.Where(site => walker.WorkBuildings.Contains(site.id)).ToList();
            if (sites.Count > 0 && rng.NextDouble() < .6)
            {
                var site = sites[rng.Next(sites.Count)];
                bool ground = OpenGround.Contains(site.id);
                var box = site.footprint;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    Vector3 point = ground
                        ? new Vector3(Mathf.Lerp(box.xMin + .4f, box.xMax - .4f, (float)rng.NextDouble()), 0,
                            Mathf.Lerp(box.yMin + .4f, box.yMax - .4f, (float)rng.NextDouble()))
                        : new Vector3(Mathf.Lerp(box.xMin - .4f, box.xMax + .4f, (float)rng.NextDouble()), 0,
                            Mathf.Lerp(box.yMin - 2.4f, box.yMin - .8f, (float)rng.NextDouble()));
                    if (!OpenSpot(new Vector2(point.x, point.z), ground ? 0 : .5f, ground ? box : null)) continue;
                    if (DiscipleWalker.Crowded(point, walker)) continue;
                    walker.UseWorkGround(ground ? box : null);
                    return point;
                }
            }
            walker.UseWorkGround(null);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2, far = 2.5f + (float)rng.NextDouble() * 7f;
                var point = here + new Vector3(Mathf.Cos(angle) * far, 0, Mathf.Sin(angle) * far);
                if (!OpenSpot(new Vector2(point.x, point.z), .6f) || DiscipleWalker.Crowded(point, walker)) continue;
                return point;
            }
            return here;
        }

        /// <summary>v1126e capture: every walker takes its own spot at once; returns (walkers, smallest gap, at work).</summary>
        internal (int count, float smallestGap, int atWork) SettleWalkersForCapture()
        {
            var walkers = root.GetComponentsInChildren<DiscipleWalker>();
            foreach (var walker in walkers) walker.SettleForCapture();
            int atWork = walkers.Count(walker => workSites.Any(site => walker.WorkBuildings.Contains(site.id) &&
                Vector2.Distance(new Vector2(walker.Flat.x, walker.Flat.z), site.footprint.center) < 5f));
            return (walkers.Length, DiscipleWalker.SmallestGap(), atWork);
        }
    }
}
