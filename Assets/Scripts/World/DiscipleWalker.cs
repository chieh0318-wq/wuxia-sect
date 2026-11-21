using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    public sealed class DiscipleWalker : MonoBehaviour
    {
        private readonly Queue<Vector3> route = new Queue<Vector3>();
        private List<Vector3> entrances;
        private float pause;
        private Vector3 destination;

        private Transform body;
        private Vector3 restingPosition;
        private const float WalkSpeed = 2.15f;
        public void Initialize(List<Vector3> waypoints, Transform figure)
        {
            entrances = waypoints;
            body = figure;
            restingPosition = body.localPosition;
            destination = transform.position;
            pause = UnityEngine.Random.Range(0.5f, 2.5f);
        }

        private void Update()
        {
            if (pause > 0)
            {
                pause -= Time.deltaTime;
                body.localPosition = restingPosition;
                return;
            }

            Vector3 difference = destination - transform.position;
            if (difference.sqrMagnitude < 0.0025f)
            {
                if (route.Count == 0)
                {
                    ChooseRoute();
                    pause = UnityEngine.Random.Range(0.8f, 3f);
                }
                destination = route.Dequeue();
                return;
            }

            body.localPosition = restingPosition + Vector3.up * (Mathf.Sin(Time.time * 13) * .022f);
            transform.position = Vector3.MoveTowards(transform.position, destination, WalkSpeed * Time.deltaTime);
        }

        internal void WalkForCapture(Vector3 position, Vector3 target)
        {
            transform.position = position;
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
            if (Mathf.Abs(current.z - target.z) > 2f)
            {
                route.Enqueue(new Vector3(5, 0, current.z));
                route.Enqueue(new Vector3(5, 0, target.z));
            }
            route.Enqueue(target);
        }
    }
}
