using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    internal sealed class UiHoverSound : UnityEngine.MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && GetComponent<Button>().IsInteractable())
                transform.localScale = UnityEngine.Vector3.one * .97f;
        }

        public void OnPointerUp(PointerEventData eventData) => ResetScale();
        public void OnPointerExit(PointerEventData eventData) => ResetScale();
        private void OnDisable() => ResetScale();
        private void ResetScale() => transform.localScale = UnityEngine.Vector3.one;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (GetComponent<Button>().IsInteractable())
                UiSfx.Play("hover");
        }
    }
}
