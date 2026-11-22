using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    internal sealed class ApHud : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Text label, sourceList;
        private RectTransform pips;
        internal event System.Action Inspected;
        private float pulseStart = -1;
        private int current = -1;


        internal void Initialize(Text text, Text sources, RectTransform fill)
        {
            label = text;
            sourceList = sources;
            pips = fill;
            sourceList.transform.parent.gameObject.SetActive(false);
        }
        internal void Set(int points, int max, string sources)
        {
            if (current >= 0 && current < points)
                pulseStart = Time.unscaledTime;
            current = points;
            SetPips(points, max);
            label.text = $"行動力　{points}／{max}";
            sourceList.text = "每月行動力來源\n" + sources;
        }

        /// <summary>One jade pip per point of monthly action: lit jade while unspent, dim ink once used.</summary>
        private void SetPips(int points, int max)
        {
            max = Mathf.Clamp(max, 1, 12);
            while (pips.childCount < max)
            {
                var pip = new GameObject("行動點", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                pip.SetParent(pips, false);
                pip.GetComponent<Image>().raycastTarget = false;
            }
            for (int index = 0; index < pips.childCount; index++)
            {
                var pip = pips.GetChild(index);
                pip.gameObject.SetActive(index < max);
                pip.GetComponent<Image>().color = index < points ? new Color(.42f, .86f, .70f) : new Color(.18f, .26f, .24f, .9f);
            }
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
