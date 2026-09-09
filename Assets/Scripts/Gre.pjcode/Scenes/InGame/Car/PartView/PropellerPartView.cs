using UnityEngine;

namespace Gre.pjcode.Scenes.InGame {
	public class PropellerPartView : PartView {
		#region Constants

		private const float RotateSpeed = 1200f;

		#endregion

		#region Serialize Fields

		[SerializeField] private Transform _root;

		#endregion

		#region Methods

		protected override void UpdateInternal(float deltaTime) {
			_root.Rotate(Vector3.forward, RotateSpeed * deltaTime);
		}

		#endregion
	}
}