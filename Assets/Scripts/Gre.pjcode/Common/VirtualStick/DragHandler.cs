using UnityEngine;
using UnityEngine.EventSystems;

namespace Gre.pjcode.Common.VirtualStick
{
    public sealed class DragHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData) { }
        public void OnPointerUp(PointerEventData eventData) { }
    }
}
