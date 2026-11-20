using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    internal sealed class ApHud : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Text label, sourceList;
        internal event System.Action Inspected;
        private float pulseStart = -1;
        private int current = -1;


        internal void Initialize(Text text, Text sources)
        {
            label = text;
            sourceList = sources;
            sourceList.transform.parent.gameObject.SetActive(false);
        }
        internal void Set(int points, int max, string sources)
        {
            if (current >= 0 && current < points)
                pulseStart = Time.unscaledTime;
            current = points;
            int filled = Mathf.Min(points, 6);
            label.text = $"行動力 {points}／{max}　" + new string('◆', filled) + new string('◇', Mathf.Min(max, 6) - filled);
            sourceList.text = sources;
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
