using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    public sealed class DiscipleWalker : MonoBehaviour
    {
        private readonly Queue<Vector3> route = new Queue<Vector3>();
        private List<Vector3> entrances;
        private List<Vector3> preferredSpots;
        private List<Vector3> wanderSpots;
        private List<Rect> buildings;
        private Rect walkBounds = new(-18.8f, -17.5f, 37.6f, 35f);
        private System.Func<Vector3, Vector3> grounded;
        private float pause;
        private Vector3 destination;

        private static readonly HashSet<DiscipleWalker> ActiveWalkers = new();
        private Transform body;
        private SpriteRenderer figureRenderer;
        private Sprite walkingSprite;

        private void OnEnable() => ActiveWalkers.Add(this);
        private void OnDisable() => ActiveWalkers.Remove(this);
        private Vector3 restingPosition;
        private const float WalkSpeed = 2.15f;
        private const float Separation = 2.25f;
        private const float BuildingPad = 0.72f;

        public void Initialize(List<Vector3> waypoints, List<Rect> footprints,
            System.Func<Vector3, Vector3> ground, Transform figure)
        {
            entrances = waypoints;
            buildings = footprints;
            grounded = ground;
            transform.position = grounded(transform.position);
            body = figure;
            figureRenderer = body.GetComponentInChildren<SpriteRenderer>();
            walkingSprite = CharacterFactory.WalkingStandee(figureRenderer.sprite);
            restingPosition = body.localPosition;
            destination = new Vector3(transform.position.x, 0, transform.position.z);
            pause = UnityEngine.Random.Range(0.4f, 2.2f);
        }

        /// <summary>v1126e-ag (Item 4): supplies preferred building/field spots, wander points, and courtyard bounds.</summary>
        internal void ConfigureSpread(List<Vector3> preferred, List<Vector3> wander, Rect bounds)
        {
            preferredSpots = preferred;
            wanderSpots = wander;
            walkBounds = bounds;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0 || grounded == null || body == null) return;

            Vector3 flat = transform.position;
            flat.y = 0;

            // Always apply soft separation so even pausing disciples never overlap each other.
            Vector3 sep = SeparationForce(flat);
            if (pause > 0)
            {
                pause -= Time.deltaTime;
                if (sep.sqrMagnitude > 0.04f)
                {
                    Vector3 nudged = ClampToWalkable(flat + sep.normalized * (WalkSpeed * 0.65f * Time.deltaTime), flat);
                    transform.position = grounded(nudged);
                    destination = nudged;
                }
                body.localPosition = restingPosition;
                return;
            }

            Vector3 difference = destination - flat;
            if (difference.sqrMagnitude < 0.04f)
            {
                if (route.Count == 0)
                {
                    ChooseRoute();
                    pause = UnityEngine.Random.Range(1.0f, 3.2f);
                }
                body.localPosition = restingPosition;
                if (route.Count > 0) destination = route.Dequeue();
                return;
            }

            Vector3 desired = difference.normalized;
            Vector3 steer = desired + sep * 1.45f;
            if (steer.sqrMagnitude < 0.001f)
            {
                ReplanAfterPause();
                return;
            }

            Vector3 step = steer.normalized * Mathf.Min(WalkSpeed * Time.deltaTime, difference.magnitude + 0.25f);
            Vector3 next = flat + step;

            if (HitsBuilding(next))
            {
                // Try sliding along X or Z before giving up and replanning.
                Vector3 slideX = new(flat.x + step.x, 0, flat.z);
                Vector3 slideZ = new(flat.x, 0, flat.z + step.z);
                if (!HitsBuilding(slideX) && walkBounds.Contains(new Vector2(slideX.x, slideX.z)))
                    next = slideX;
                else if (!HitsBuilding(slideZ) && walkBounds.Contains(new Vector2(slideZ.x, slideZ.z)))
                    next = slideZ;
                else
                {
                    ReplanAfterPause();
                    return;
                }
            }

            next = ClampToWalkable(next, flat);
            if ((next - flat).sqrMagnitude < 0.000001f)
            {
                ReplanAfterPause();
                return;
            }

            float screenX = Vector3.Dot(next - flat, body.right);
            if (Mathf.Abs(screenX) > .00001f && figureRenderer != null && walkingSprite != null)
            {
                figureRenderer.sprite = walkingSprite;
                figureRenderer.flipX = CharacterFactory.FlipStandee(walkingSprite.texture, screenX > 0);
            }
            body.localPosition = restingPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 13)) * .022f);
            transform.position = grounded(next);
        }

        private Vector3 SeparationForce(Vector3 flat)
        {
            Vector3 push = Vector3.zero;
            float range = Separation * 1.15f;
            float rangeSqr = range * range;
            foreach (var walker in ActiveWalkers)
            {
                if (walker == this || walker.entrances != entrances) continue;
                Vector3 other = walker.transform.position;
                other.y = 0;
                Vector3 away = flat - other;
                float sqr = away.sqrMagnitude;
                if (sqr >= rangeSqr) continue;
                float dist = Mathf.Sqrt(Mathf.Max(sqr, 0.0001f));
                Vector3 dir = sqr > 0.0001f ? away / dist : new Vector3((GetHashCode() & 1) == 0 ? 1f : -1f, 0, 0.3f).normalized;
                float weight = (range - dist) / range;
                push += dir * (weight * 2.2f);
            }
            return push;
        }

        private bool HitsBuilding(Vector3 pos)
        {
            if (buildings == null) return false;
            var pt = new Vector2(pos.x, pos.z);
            for (int i = 0; i < buildings.Count; i++)
            {
                var r = buildings[i];
                if (pt.x >= r.xMin - BuildingPad && pt.x <= r.xMax + BuildingPad &&
                    pt.y >= r.yMin - BuildingPad && pt.y <= r.yMax + BuildingPad)
                    return true;
            }
            return false;
        }

        private Vector3 ClampToWalkable(Vector3 candidate, Vector3 fallback)
        {
            candidate.x = Mathf.Clamp(candidate.x, walkBounds.xMin, walkBounds.xMax);
            candidate.z = Mathf.Clamp(candidate.z, walkBounds.yMin, walkBounds.yMax);
            return HitsBuilding(candidate) ? fallback : candidate;
        }

        private void ReplanAfterPause()
        {
            route.Clear();
            destination = new Vector3(transform.position.x, 0, transform.position.z);
            body.localPosition = restingPosition;
            pause = Random.Range(.35f, 1.25f);
        }

        internal void WalkForCapture(Vector3 position, Vector3 target)
        {
            transform.position = grounded(position);
            destination = target;
            pause = 0;
            route.Clear();
        }

        private void ChooseRoute()
        {
            Vector3 target = PickTarget();
            Vector3 current = transform.position;
            current.y = 0;

            // Use spread north-south corridor lanes instead of funneling everyone into one single line.
            if (Mathf.Abs(current.z - target.z) > 1.5f)
            {
                float[] lanes = { -15.5f, -5.0f, 5.0f, 15.5f };
                float bestLane = lanes[0];
                float bestCost = float.PositiveInfinity;
                foreach (float lx in lanes)
                {
                    if (lx < walkBounds.xMin + 0.5f || lx > walkBounds.xMax - 0.5f) continue;
                    float cost = Mathf.Abs(current.x - lx) + Mathf.Abs(target.x - lx);
                    if (cost < bestCost) { bestCost = cost; bestLane = lx; }
                }
                float jitter = UnityEngine.Random.Range(-0.9f, 0.9f);
                float laneX = Mathf.Clamp(bestLane + jitter, walkBounds.xMin + 0.5f, walkBounds.xMax - 0.5f);
                var turn1 = new Vector3(laneX, 0, current.z);
                var turn2 = new Vector3(laneX, 0, target.z);
                if (!HitsBuilding(turn1)) route.Enqueue(turn1);
                if (!HitsBuilding(turn2)) route.Enqueue(turn2);
            }
            route.Enqueue(target);
        }

        private Vector3 PickTarget()
        {
            bool usePreferred = preferredSpots != null && preferredSpots.Count > 0 && UnityEngine.Random.value < 0.65f;
            List<Vector3> pool = usePreferred ? preferredSpots
                : (wanderSpots != null && wanderSpots.Count > 0 ? wanderSpots : entrances);

            if (pool == null || pool.Count == 0)
                return new Vector3(UnityEngine.Random.Range(walkBounds.xMin, walkBounds.xMax), 0,
                    UnityEngine.Random.Range(walkBounds.yMin, walkBounds.yMax));

            // Pick a spot that isn't crowded by another disciple.
            Vector3 best = pool[UnityEngine.Random.Range(0, pool.Count)];
            float bestClearance = -1f;
            int tries = Mathf.Min(8, pool.Count);
            for (int i = 0; i < tries; i++)
            {
                Vector3 candidate = pool[UnityEngine.Random.Range(0, pool.Count)];
                candidate.x += UnityEngine.Random.Range(-0.9f, 0.9f);
                candidate.z += UnityEngine.Random.Range(-0.6f, 0.6f);
                candidate.x = Mathf.Clamp(candidate.x, walkBounds.xMin, walkBounds.xMax);
                candidate.z = Mathf.Clamp(candidate.z, walkBounds.yMin, walkBounds.yMax);
                if (HitsBuilding(candidate)) continue;

                float nearest = 999f;
                foreach (var walker in ActiveWalkers)
                {
                    if (walker == this || walker.entrances != entrances) continue;
                    Vector3 other = walker.transform.position;
                    float d = (candidate.x - other.x) * (candidate.x - other.x) + (candidate.z - other.z) * (candidate.z - other.z);
                    if (d < nearest) nearest = d;
                }
                if (nearest > bestClearance)
                {
                    bestClearance = nearest;
                    best = candidate;
                    if (nearest >= Separation * Separation * 1.4f) break;
                }
            }
            return best;
        }
    }
}
