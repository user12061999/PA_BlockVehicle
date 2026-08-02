using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class JetBoosterPartView : PartView
    {
        [SerializeField] private ParticleSystem _fireEffect;
        [SerializeField] private Transform _root;

        private Vector3 _defaultRootScale;
        private float _countTime;

        protected override void InitializeInternal()
        {
            if (_root != null) _defaultRootScale = _root.localScale;
        }

        protected override void ActivateInternal(float time)
        {
            if (_fireEffect != null) _fireEffect.Play();
            _countTime = 0f;
        }

        protected override void UpdateInternal(float deltaTime)
        {
            if (_root == null) return;
            _countTime += deltaTime;
            float length = 0.11f * Mathf.Sin(2f * Mathf.PI / 0.125f * _countTime);
            _root.localScale = new Vector3(
                _defaultRootScale.x * (1f - length),
                _defaultRootScale.y * (1f + length),
                _defaultRootScale.z * (1f - length)
            );
        }

        protected override void InactivateInternal()
        {
            if (_fireEffect != null) _fireEffect.Stop();
            if (_root != null) _root.localScale = _defaultRootScale;
        }
    }
}
