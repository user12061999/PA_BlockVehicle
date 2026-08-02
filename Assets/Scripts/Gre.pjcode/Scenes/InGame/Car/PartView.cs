using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public class PartView : MonoBehaviour
    {
#if !RELEASE
        [SerializeField] private bool _isDrawGizmos = true;
#endif

        [SerializeField] private PartAttachType _attachType;
        [SerializeField] private Color _viewColor = Color.white;

        private float _activateTime;
        private MaterialPropertyBlock _propertyBlock;
        protected PartAttachSideType _sideType;

        public Transform T => transform;
        public PartAttachType AttachType => _attachType;
        public string UniqueId { get; private set; }
        public int PartId { get; private set; }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            _activateTime -= deltaTime;
            UpdateInternal(deltaTime);
            if (_activateTime <= 0f) Inactivate();
        }

#if !RELEASE
        void OnDrawGizmos()
        {
            if (!_isDrawGizmos) return;
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.25f);
        }
#endif

        public void Initialize(int partId, string uniqueId, PartAttachSideType sideType)
        {
            PartId = partId;
            UniqueId = uniqueId;
            _sideType = sideType;

            if (sideType == PartAttachSideType.Left) transform.localScale = new Vector3(-1f, 1f, 1f);
            InitializeInternal();

            foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.receiveShadows = false;
            }

            ApplyViewColor();
            enabled = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ApplyViewColor();
        }
#endif

        public void Activate(float time)
        {
            if (time > 0f)
            {
                _activateTime = time;
                ActivateInternal(time);
                enabled = true;
            }
        }

        public void Inactivate()
        {
            _activateTime = 0f;
            InactivateInternal();
            enabled = false;
        }

        protected virtual void InitializeInternal() { }
        protected virtual void ActivateInternal(float time) { }
        protected virtual void UpdateInternal(float deltaTime) { }
        protected virtual void InactivateInternal() { }

        void ApplyViewColor()
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", _viewColor);
                _propertyBlock.SetColor("_Color", _viewColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
