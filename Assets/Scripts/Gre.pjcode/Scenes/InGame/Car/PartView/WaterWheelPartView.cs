using UnityEngine;

namespace Gre.pjcode.Scenes.InGame {
	public class WaterWheelPartView : PartView {
		#region Constants

		private const float RotateSpeed = 400f;

		#endregion

		#region Serialize Fields

		[SerializeField] private ParticleSystem _splashEffect;
		[SerializeField] private Transform _root;

		#endregion

		#region Variables

		private Vector3 _rotateAxis;

		#endregion

		#region Methods

		protected override void InitializeInternal() {
			_rotateAxis = _sideType == PartAttachSideType.Right ? Vector3.right : Vector3.left;
		}

		protected override void ActivateInternal(float time) {
			_splashEffect.Play();
		}

		protected override void UpdateInternal(float deltaTime) {
			_root.Rotate(_rotateAxis, RotateSpeed * deltaTime);
		}

		protected override void InactivateInternal() {
			_splashEffect.Stop();
		}

		#endregion
	}
}