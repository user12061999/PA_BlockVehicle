using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class CarSphereTracer : MonoBehaviour
    {
        [SerializeField] private float _addForceWeight = 0.5f;
        [SerializeField] private CarView _carView;
        [SerializeField] private ParticleSystem Booster;

        public float AddForceWeight => _addForceWeight;
        public CarView CarView => _carView;

        public void Setup(Rigidbody rb) { }
        public void SetCustomGravityWeight(float weight) { }
        public void ForceUpdate() { }
        public void PlayRunningEffect(TerrainType terrainType) { }
        public void StopRunningEffect(bool immediate = false) { }
        public void GetPart(int partId) { }
        public void HidePartView() { }
        public void GetCoin(int amount) { }
        public void HideCoinView() { }
        public void PlayDashEffect() { }
        public void UnlockBooster() { }
        public void GetAttachment() { }
        public void SetBooster(bool isSet) { }
        public void AppearBooster() { }

        public void SetActivateBooster(float time)
        {
            Booster.Play();
        }
        public void InactivateBooster() { }
        public void PlayBoosterAttachEffect() { }
        public void SustainDash(float forceWeight, float duration) { }
    }

    public enum TerrainType
    {
        Default,
        Dirt,
        Water,
        Air,
        Max
    }
}
