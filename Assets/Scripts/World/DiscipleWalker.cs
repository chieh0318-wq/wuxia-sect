using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// A disciple walking the courtyard. v1126e #4: each one heads for a spot of their own — at the building their
    /// strongest art belongs to (fields for 耕作, the 藥廬 for 醫術, the 演武場 for martial arts …) or anywhere open in
    /// the yard — chosen by <see cref="Courtyard.NextSpot"/> so no two people aim at the same place. Neighbours inside a
    /// personal-space ellipse (wider front-to-back, since the camera looks along +z) push each other apart every frame,
    /// walking or resting, so figures never stand on top of one another; routes step around buildings instead of
    /// funnelling everyone down the central lane.
    /// </summary>
    public sealed class DiscipleWalker : MonoBehaviour
    {
        private readonly Queue<Vector3> route = new Queue<Vector3>();
        private List<Vector3> entrances;
        private List<Rect> buildings;
        private System.Func<Vector3, Vector3> grounded;
        private System.Func<DiscipleWalker, Vector3> nextSpot;
        private float pause;
        private Vector3 destination;
        private Rect? workGround;

        private static readonly HashSet<DiscipleWalker> ActiveWalkers = new();
        private Transform body;
        private SpriteRenderer figureRenderer;
        private Sprite walkingSprite;

        private void OnEnable() => ActiveWalkers.Add(this);
        private void OnDisable() => ActiveWalkers.Remove(this);
        private Vector3 restingPosition;
        private const float WalkSpeed = 2.15f;
        /// <summary>Personal space: half-width across the screen (x) and depth (z) of the ellipse two figures keep.</summary>
        internal const float SpaceX = 1.25f, SpaceZ = 2.1f;

        internal string[] WorkBuildings = System.Array.Empty<string>();
        internal System.Random Rng = new System.Random(0);
        internal Vector3 Destination => route.Count > 0 ? LastOf(route) : destination;
        internal Vector3 Flat => new(transform.position.x, 0, transform.position.z);

        public void Initialize(List<Vector3> waypoints, List<Rect> footprints,
            System.Func<Vector3, Vector3> ground, Transform figure, System.Func<DiscipleWalker, Vector3> spot = null)
        {
            entrances = waypoints;
            buildings = footprints;
            grounded = ground;
            nextSpot = spot;
            transform.position = grounded(transform.position);
            body = figure;
            figureRenderer = body.GetComponentInChildren<SpriteRenderer>();
            walkingSprite = CharacterFactory.WalkingStandee(figureRenderer.sprite);
            restingPosition = body.localPosition;
            destination = Flat;
            pause = UnityEngine.Random.Range(0.5f, 2.5f);
        }

        /// <summary>Ellipse distance between two ground points: below 1 means the figures overlap.</summary>
        internal static float Gap(Vector3 a, Vector3 b)
        {
            float dx = (a.x - b.x) / SpaceX, dz = (a.z - b.z) / SpaceZ;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>True when <paramref name="point"/> is inside another walker's space, now or where they are going.</summary>
        internal static bool Crowded(Vector3 point, DiscipleWalker self, float margin = 1.05f)
        {
            foreach (var walker in ActiveWalkers)
            {
                if (walker == self || walker.entrances != self.entrances) continue;
                if (Gap(point, walker.Flat) < margin || Gap(point, walker.Destination) < margin) return true;
            }
            return false;
        }

        /// <summary>The smallest ellipse gap between any two active walkers (captures log it).</summary>
        internal static float SmallestGap()
        {
            float smallest = float.MaxValue;
            var list = new List<DiscipleWalker>(ActiveWalkers);
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    smallest = Mathf.Min(smallest, Gap(list[i].Flat, list[j].Flat));
            return smallest;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0) return;
            Vector3 flat = Flat;
            // Separation: overlapping neighbours push apart whether walking or resting.
            Vector3 push = Vector3.zero;
            foreach (var walker in ActiveWalkers)
            {
                if (walker == this || walker.entrances != entrances) continue;
                var other = walker.Flat;
                float gap = Gap(flat, other);
                if (gap >= 1) continue;
                var away = flat - other;
                if (away.sqrMagnitude < .0001f) away = new Vector3(GetInstanceID() > walker.GetInstanceID() ? 1 : -1, 0, 0);
                push += new Vector3(away.x / (SpaceX * SpaceX), 0, away.z / (SpaceZ * SpaceZ)).normalized * (1 - gap);
            }
            if (push.sqrMagnitude > .0001f)
            {
                var nudged = flat + push.normalized * Mathf.Min(push.magnitude * 3f, 1.4f) * WalkSpeed * Time.deltaTime;
                if (Walkable(nudged))
                {
                    transform.position = grounded(nudged);
                    flat = nudged;
                }
            }

            if (pause > 0)
            {
                pause -= Time.deltaTime;
                body.localPosition = restingPosition;
                return;
            }

            Vector3 difference = destination - flat;
            if (difference.sqrMagnitude < 0.0025f)
            {
                if (route.Count == 0)
                {
                    ChooseRoute();
                    pause = UnityEngine.Random.Range(1.5f, 4.5f);
                    return;
                }
                body.localPosition = restingPosition;
                destination = route.Dequeue();
                return;
            }

            var next = Vector3.MoveTowards(flat, destination, WalkSpeed * Time.deltaTime);
            if (!Walkable(next))
            {
                ReplanAfterPause();
                return;
            }
            // Walking straight into someone: step aside and pick another spot.
            foreach (var walker in ActiveWalkers)
            {
                if (walker == this || walker.entrances != entrances) continue;
                var other = walker.Flat;
                if (Gap(next, other) >= .8f || Gap(next, other) >= Gap(flat, other)) continue;
                ReplanAfterPause();
                return;
            }
            float screenX = Vector3.Dot(next - flat, body.right);
            if (Mathf.Abs(screenX) > .00001f)
            {
                figureRenderer.sprite = walkingSprite;
                figureRenderer.flipX = CharacterFactory.FlipStandee(walkingSprite.texture, screenX > 0);
            }
            body.localPosition = restingPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 13)) * .022f);
            transform.position = grounded(next);
        }

        /// <summary>Outside every building, except the open ground (field, training yard) this walker works on.</summary>
        private bool Walkable(Vector3 point)
        {
            var p = new Vector2(point.x, point.z);
            var here = new Vector2(transform.position.x, transform.position.z);
            foreach (var footprint in buildings)
                if (footprint.Contains(p) && !(workGround.HasValue && footprint == workGround.Value) && !footprint.Contains(here))
                    return false;
            return true;
        }

        private void ReplanAfterPause()
        {
            route.Clear();
            destination = Flat;
            body.localPosition = restingPosition;
            pause = Random.Range(.4f, 1.4f);
        }

        internal void WalkForCapture(Vector3 position, Vector3 target)
        {
            transform.position = grounded(position);
            destination = target;
            pause = 0;
            route.Clear();
        }

        /// <summary>v1126e capture: settle into the spot chosen for this walker straight away.</summary>
        internal void SettleForCapture()
        {
            ChooseRoute();
            var spot = Destination;
            route.Clear();
            transform.position = grounded(spot);
            destination = Flat;
            pause = 30;
        }

        internal void UseWorkGround(Rect? ground) => workGround = ground;

        private void ChooseRoute()
        {
            Vector3 target;
            if (nextSpot != null) target = nextSpot(this);
            else
            {
                target = entrances.Count == 0 ? new Vector3(UnityEngine.Random.Range(-8f, 8f), 0, -16) :
                    entrances[UnityEngine.Random.Range(0, entrances.Count)];
                target.x += UnityEngine.Random.Range(-0.65f, 0.65f);
            }
            target.y = 0;
            route.Clear();
            Vector3 from = Flat;
            // Step around a building in the way: go via the nearest clear corner of the first footprint crossed.
            var blocker = FirstBlocker(from, target);
            if (blocker.HasValue)
            {
                var box = blocker.Value;
                var corners = new[]
                {
                    new Vector3(box.xMin - .9f, 0, box.yMin - .9f), new Vector3(box.xMax + .9f, 0, box.yMin - .9f),
                    new Vector3(box.xMin - .9f, 0, box.yMax + .9f), new Vector3(box.xMax + .9f, 0, box.yMax + .9f)
                };
                Vector3? via = null;
                float best = float.MaxValue;
                foreach (var corner in corners)
                {
                    if (!Walkable(corner) || FirstBlocker(from, corner).HasValue || FirstBlocker(corner, target).HasValue) continue;
                    float length = Vector3.Distance(from, corner) + Vector3.Distance(corner, target);
                    if (length < best) { best = length; via = corner; }
                }
                if (via.HasValue) route.Enqueue(via.Value);
            }
            route.Enqueue(target);
        }

        private Rect? FirstBlocker(Vector3 from, Vector3 to)
        {
            float length = Vector3.Distance(from, to);
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / .35f));
            for (int step = 1; step <= steps; step++)
            {
                var point = Vector3.Lerp(from, to, step / (float)steps);
                var p = new Vector2(point.x, point.z);
                foreach (var footprint in buildings)
                    if (footprint.Contains(p) && !(workGround.HasValue && footprint == workGround.Value)) return footprint;
            }
            return null;
        }

        private static Vector3 LastOf(Queue<Vector3> queue)
        {
            Vector3 last = default;
            foreach (var entry in queue) last = entry;
            return last;
        }
    }
}
