using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    public sealed class DiscipleWalker : MonoBehaviour
    {
        private readonly Queue<Vector3> route = new Queue<Vector3>();
        private List<Vector3> entrances;
        private List<Rect> buildings;
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
        private const float Separation = .9f;
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
            pause = UnityEngine.Random.Range(0.5f, 2.5f);
        }

        private void Update()
        {
            if (pause > 0 || Time.deltaTime <= 0)
            {
                pause -= Time.deltaTime;
                body.localPosition = restingPosition;
                return;
            }

            Vector3 flat = transform.position;
            flat.y = 0;
            Vector3 difference = destination - flat;
            if (difference.sqrMagnitude < 0.0025f)
            {
                if (route.Count == 0)
                {
                    ChooseRoute();
                    pause = UnityEngine.Random.Range(0.8f, 3f);
                }
                body.localPosition = restingPosition;
                destination = route.Dequeue();
                return;
            }

            var next = Vector3.MoveTowards(flat, destination, WalkSpeed * Time.deltaTime);
            if (buildings.Exists(footprint => footprint.Contains(new Vector2(next.x, next.z))))
            {
                ReplanAfterPause();
                return;
            }
            foreach (var walker in ActiveWalkers)
            {
                if (walker == this || walker.entrances != entrances) continue;
                var other = walker.transform.position;
                other.y = 0;
                float distance = (next - other).sqrMagnitude;
                if (distance >= Separation * Separation || distance >= (flat - other).sqrMagnitude) continue;
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

        private void ReplanAfterPause()
        {
            route.Clear();
            destination = new Vector3(transform.position.x, 0, transform.position.z);
            body.localPosition = restingPosition;
            pause = Random.Range(.3f, 1.3f);
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
            Vector3 target = entrances.Count == 0 ? new Vector3(UnityEngine.Random.Range(-8f, 8f), 0, -16) :
                entrances[UnityEngine.Random.Range(0, entrances.Count)];
            target.x += UnityEngine.Random.Range(-0.65f, 0.65f);
            Vector3 current = transform.position;
            if (entrances.Count > 0 && Mathf.Abs(current.z - target.z) > .15f)
            {
                float lane = target.z > current.z ? 4.4f : 5.6f;
                route.Enqueue(new Vector3(lane, 0, current.z));
                route.Enqueue(new Vector3(lane, 0, target.z));
            }
            route.Enqueue(target);
        }
    }
}
