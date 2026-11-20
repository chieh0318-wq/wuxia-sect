using UnityEngine;
using UnityEngine.EventSystems;

namespace Wuxia
{
    public sealed class CameraController : MonoBehaviour
    {
        private const float Pitch = 53f;
        internal event System.Action<string> Changed;
        private Vector2 focus = new(1.1f, -1.5f);
        private float height = 43f;
        private Vector3 lastMouse;
        private bool dragging;

        private void Update()
        {
            Vector2 previousFocus = focus;
            float previousHeight = height;
            Vector2 keys = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keys.x--;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keys.x++;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keys.y--;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keys.y++;
            focus += keys.normalized * (height * 0.52f * Time.unscaledDeltaTime);

            Vector3 mouse = Input.mousePosition;
            bool held = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (held && dragging)
            {
                Vector3 delta = mouse - lastMouse;
                focus -= new Vector2(delta.x, delta.y) * (height / Mathf.Max(1, Screen.height));
            }
            dragging = held;
            lastMouse = mouse;
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                height = Mathf.Clamp(height - Input.mouseScrollDelta.y * 2.6f, 12f, 46f);
            focus.x = Mathf.Clamp(focus.x, -20f, 20f);
            focus.y = Mathf.Clamp(focus.y, -20f, 20f);
            PositionCamera();
            if (focus != previousFocus)
                Changed?.Invoke("camera_move");
            if (height != previousHeight)
                Changed?.Invoke("camera_zoom");
        }

        private void Awake() => PositionCamera();

        internal void Frame(Vector2 point, float altitude)
        {
            focus = point;
            height = altitude;
            PositionCamera();
        }

        private void PositionCamera()
        {
            float setback = height / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            transform.SetPositionAndRotation(new Vector3(focus.x, height, focus.y - setback),
                Quaternion.Euler(Pitch, 0, 0));
        }
    }
}
