using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    internal sealed class ApHud : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Text label, sourceList;
        private RectTransform meter;
        internal event System.Action Inspected;
        private float pulseStart = -1;
        private int current = -1;


        internal void Initialize(Text text, Text sources, RectTransform fill)
        {
            label = text;
            sourceList = sources;
            meter = fill;
            sourceList.transform.parent.gameObject.SetActive(false);
        }
        internal void Set(int points, int max, string sources)
        {
            if (current >= 0 && current < points)
                pulseStart = Time.unscaledTime;
            current = points;
            meter.anchorMax = new Vector2(Mathf.Clamp01(points / (float)Mathf.Max(1, max)), 1);
            label.text = $"行動力  {points}／{max}";
            sourceList.text = "每月行動力來源\n" + sources;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - pulseStart;
            label.transform.localScale = elapsed < .42f ? Vector3.one * (1 + .10f * Mathf.Sin(elapsed * 7.48f)) : Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            sourceList.transform.parent.gameObject.SetActive(true);
            Inspected?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData) => sourceList.transform.parent.gameObject.SetActive(false);
    }
}
