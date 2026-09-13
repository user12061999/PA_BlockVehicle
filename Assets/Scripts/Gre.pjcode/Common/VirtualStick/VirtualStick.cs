using UnityEngine;
using UnityEngine.EventSystems;

namespace Gre.pjcode.Common.VirtualStick
{
    public sealed class VirtualStick : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private UnityEngine.UI.Image _stickParent;
        [SerializeField] private RectTransform _stick;
        private int? _pointerId;
        private bool _running;
        public Vector2 Direction { get; private set; }

        void Awake() { SetRunning(false); }

        public void SetRunning(bool running)
        {
            _running = running;
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = running;
            _canvasGroup.interactable = running;
            ResetStick();
        }

        public void BeginPointer(PointerEventData data)
        {
            if (!_running || _pointerId.HasValue || data.button != PointerEventData.InputButton.Left) return;
            _pointerId = data.pointerId;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)_stickParent.transform.parent, data.position, data.pressEventCamera, out Vector3 position);
            _stickParent.rectTransform.position = position;
            _stick.position = position;
        }

        public void DragPointer(PointerEventData data)
        {
            if (_pointerId != data.pointerId) return;
            RectTransform rect = _stickParent.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out Vector2 offset)) return;
            float radius = Mathf.Max(1f, Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f);
            Direction = Vector2.ClampMagnitude(offset / radius, 1f);
            _stick.position = rect.TransformPoint(Direction * radius);
            _canvasGroup.alpha = 1f;
        }

        public void EndPointer(PointerEventData data)
        {
            if (_pointerId == data.pointerId) ResetStick();
        }

        void OnDisable() { ResetStick(); }
        void OnApplicationFocus(bool focused) { if (!focused) ResetStick(); }

        void ResetStick()
        {
            _pointerId = null;
            Direction = Vector2.zero;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_stick != null && _stickParent != null) _stick.position = _stickParent.transform.position;
        }
    }
}
