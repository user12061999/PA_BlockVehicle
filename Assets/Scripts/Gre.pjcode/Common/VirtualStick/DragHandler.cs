using UnityEngine;
using UnityEngine.EventSystems;

namespace Gre.pjcode.Common.VirtualStick
{
    public sealed class DragHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private VirtualStick _stick;
        void Awake() { _stick = GetComponentInParent<VirtualStick>(); }
        public void OnPointerDown(PointerEventData eventData) { _stick.BeginPointer(eventData); }
        public void OnBeginDrag(PointerEventData eventData) { _stick.DragPointer(eventData); }
        public void OnDrag(PointerEventData eventData) { _stick.DragPointer(eventData); }
        public void OnEndDrag(PointerEventData eventData) { _stick.EndPointer(eventData); }
        public void OnPointerUp(PointerEventData eventData) { _stick.EndPointer(eventData); }
    }
}
