using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class CaterpillarPartView : PartView
    {
        [SerializeField] private ParticleSystem _smokeEffect;
        [SerializeField] private Transform[] _wheels;
        [SerializeField] private Transform _root;

        private Vector3 _rotateAxis;
        private float _countTime;

        protected override void InitializeInternal()
        {
            _rotateAxis = _sideType == PartAttachSideType.Right ? Vector3.back : Vector3.forward;
        }

        protected override void ActivateInternal(float time)
        {
            if (_smokeEffect != null) _smokeEffect.Play();
            _countTime = 0f;
        }

        protected override void UpdateInternal(float deltaTime)
        {
            if (_wheels != null)
            {
                foreach (Transform wheel in _wheels)
                {
                    if (wheel != null) wheel.Rotate(_rotateAxis, 1200f * deltaTime);
                }
            }

            if (_root == null) return;
            _countTime += deltaTime;
            Vector3 position = _root.localPosition;
            position.y = 0.035f * Mathf.Sin(2f * Mathf.PI / 0.15f * _countTime);
            _root.localPosition = position;
        }

        protected override void InactivateInternal()
        {
            if (_smokeEffect != null) _smokeEffect.Stop();
            if (_root == null) return;
            Vector3 position = _root.localPosition;
            position.y = 0f;
            _root.localPosition = position;
        }
    }
}
