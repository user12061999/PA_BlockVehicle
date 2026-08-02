using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class WingPartView : PartView
    {
        [SerializeField] private TrailRenderer _windTrail;

        private float _defaultTrailTime;

        protected override void InitializeInternal()
        {
            if (_windTrail != null) _defaultTrailTime = _windTrail.time;
        }

        protected override void ActivateInternal(float time)
        {
            if (_windTrail == null) return;
            _windTrail.Clear();
            _windTrail.time = _defaultTrailTime;
            _windTrail.enabled = true;
        }

        protected override void InactivateInternal()
        {
            if (_windTrail == null) return;
            _windTrail.time = 0f;
            _windTrail.enabled = false;
        }
    }
}
