using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class RearWingPartView : PartView
    {
        const float BoundScale = 0.105f;
        const float BoundInterval = 0.12f;

        [SerializeField] private ParticleSystem _windEffect;
        [SerializeField] private Transform _root;

        Vector3 _defaultRootScale;
        float _countTime;

        protected override void InitializeInternal()
        {
            if (_root != null) _defaultRootScale = _root.localScale;
        }

        protected override void ActivateInternal(float time)
        {
            if (_windEffect != null) _windEffect.Play();
            _countTime = 0f;
        }

        protected override void UpdateInternal(float deltaTime)
        {
            if (_root == null) return;
            _countTime += deltaTime;
            float scale = 1f + BoundScale * Mathf.Sin(2f * Mathf.PI / BoundInterval * _countTime);
            _root.localScale = _defaultRootScale * scale;
        }

        protected override void InactivateInternal()
        {
            if (_windEffect != null) _windEffect.Stop();
            if (_root != null) _root.localScale = _defaultRootScale;
        }
    }
}
