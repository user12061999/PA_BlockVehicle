using UnityEngine;
using UnityEngine.UI;

namespace Gre.pjcode.Ui {
	public class NotificationBadge : MonoBehaviour {
		#region Serialize Fields

		[SerializeField] private Image _flash;

		[SerializeField] [Tooltip("フラッシュ演出1回分の周期（秒）")]
		private float _cycleDuration = 1.5f;

		[SerializeField] [Tooltip("フラッシュ時の最大スケール")]
		private float _maxScale = 1.6f;

		[SerializeField] [Tooltip("進行度(0-1)に対するスケール/フェードのイージング")]
		private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		#endregion

		#region Variables

		private RectTransform _flashRect;
		private Color _baseColor;

		#endregion

		#region Unity Event Functions

		private void Awake() {
			_flashRect = _flash.rectTransform;
			_baseColor = _flash.color;
		}

		private void Update() {
			// アプリ起動からの経過時間(Time.time)を周期で割った余りを位相として使うため、
			// このスクリプトを使う全バッジが同じタイミングでフラッシュする
			var phase = Time.time % _cycleDuration / _cycleDuration;
			var eased = _easeCurve.Evaluate(phase);

			var scale = Mathf.Lerp(1f, _maxScale, eased);
			_flashRect.localScale = new Vector3(scale, scale, 1f);

			var alpha = Mathf.Lerp(_baseColor.a, 0f, eased);
			_flash.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
		}

		#endregion
	}
}