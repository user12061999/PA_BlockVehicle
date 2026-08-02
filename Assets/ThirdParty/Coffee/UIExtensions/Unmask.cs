using UnityEngine;

namespace Coffee.UIExtensions
{
    public sealed class Unmask : MonoBehaviour
    {
        [SerializeField] private RectTransform m_FitTarget;
        [SerializeField] private bool m_FitOnLateUpdate;
        [SerializeField] private bool m_OnlyForChildren;
        [SerializeField] private bool m_ShowUnmaskGraphic;
        [SerializeField] private float m_EdgeSmoothing = 0.01f;
    }
}
