using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class MinoListItem : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;

        public RectTransform Rect => _rect == null ? transform as RectTransform : _rect;
    }
}
