using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class InGameRunUiView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private CanvasGroup _runningGroup;
        [SerializeField] private TMP_Text _forwardDistanceText;
        [SerializeField] private CanvasGroup _speedGaugeGroup;
        [SerializeField] private Image _speedGaugeBar;
        [SerializeField] private TMP_Text _speedGaugeText;
        [SerializeField] private RectTransform _speedNeedle;
        [SerializeField] private RectTransform _progressGaugeRoot;
        [SerializeField] private Image _progressGaugeBar;
        [SerializeField] private RectTransform _progressGaugePin;
        [SerializeField] private TMP_Text _progressGaugePinText;
        [SerializeField] private RectTransform _progressGaugeBestPin;
        [SerializeField] private float _roadLength = 120f;
        [SerializeField] private float _speedNeedleMaxAngle = 120f;
        [SerializeField] private float _speedNeedleMinAngle = -120f;
        [SerializeField] private float _maxDisplaySpeedKmh = 160f;

        float _bestDistance;
        float _targetDistance = 1f;

        void Awake()
        {
            CacheViews();
            SetRunning(false);
        }

        public void BeginRun()
        {
            CacheViews();
            _targetDistance = Mathf.Max(1f, _roadLength);
            SetRunning(true);
            UpdateRun(0f, 0f);
        }

        public void UpdateRun(float distance, float speed)
        {
            CacheViews();
            float safeDistance = Mathf.Max(0f, distance);
            float safeSpeed = Mathf.Max(0f, speed);
            _targetDistance = Mathf.Max(1f, _roadLength);

            float progress = Mathf.Clamp01(safeDistance / _targetDistance);
            float bestProgress = Mathf.Clamp01(_bestDistance / _targetDistance);
            int meters = Mathf.RoundToInt(safeDistance);
            int kmh = Mathf.RoundToInt(safeSpeed * 3.6f);

            if (_forwardDistanceText != null) _forwardDistanceText.text = meters + "m";
            if (_progressGaugePinText != null) _progressGaugePinText.text = Mathf.RoundToInt(progress * 100f) + "%";
            if (_progressGaugeBar != null) _progressGaugeBar.fillAmount = progress;
            SetPinProgress(_progressGaugePin, progress);
            SetPinProgress(_progressGaugeBestPin, bestProgress);
            if (_progressGaugeBestPin != null) _progressGaugeBestPin.gameObject.SetActive(_bestDistance > 0.01f);

            float speedProgress = _maxDisplaySpeedKmh > 0f ? Mathf.Clamp01(kmh / _maxDisplaySpeedKmh) : 0f;
            if (_speedGaugeBar != null) _speedGaugeBar.fillAmount = speedProgress;
            if (_speedGaugeText != null) _speedGaugeText.text = kmh.ToString();
            if (_speedNeedle != null)
            {
                float angle = Mathf.Lerp(_speedNeedleMaxAngle, _speedNeedleMinAngle, speedProgress);
                _speedNeedle.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        public void FinishRun(float distance)
        {
            _bestDistance = Mathf.Max(_bestDistance, distance);
            SetRunning(false);
        }

        public void ResetRun()
        {
            _targetDistance = Mathf.Max(1f, _roadLength);
            SetRunning(false);
            UpdateRun(0f, 0f);
        }

        void SetRunning(bool isRunning)
        {
            SetCanvasGroup(_runningGroup, isRunning);
            SetCanvasGroup(_speedGaugeGroup, isRunning);
            if (_runningGroup == null && _speedGaugeGroup == null) SetCanvasGroup(_canvasGroup, isRunning);
        }

        void SetPinProgress(RectTransform pin, float progress)
        {
            if (pin == null || _progressGaugeRoot == null) return;
            Rect rect = _progressGaugeRoot.rect;
            Vector2 position = pin.anchoredPosition;
            float targetX = Mathf.Lerp(rect.xMin, rect.xMax, progress);
            float anchorX = Mathf.Lerp(rect.xMin, rect.xMax, pin.anchorMin.x);
            position.x = targetX - anchorX;
            pin.anchoredPosition = position;
        }

        void CacheViews()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_runningGroup == null) _runningGroup = FindComponent<CanvasGroup>("RunningGroup");
            if (_forwardDistanceText == null) _forwardDistanceText = FindComponentInChildren<TMP_Text>("RunningGroup", "CustomText");
            if (_speedGaugeGroup == null) _speedGaugeGroup = FindComponent<CanvasGroup>("SpeedGaugeGroup");
            if (_speedGaugeBar == null) _speedGaugeBar = FindComponentInChildren<Image>("SpeedGauge", "GaugeBar");
            if (_speedGaugeText == null) _speedGaugeText = FindComponent<TMP_Text>("ValueText");
            if (_speedNeedle == null) _speedNeedle = FindRect("Needle");
            if (_progressGaugeRoot == null) _progressGaugeRoot = FindRect("ProgressGauge");
            if (_progressGaugeBar == null) _progressGaugeBar = FindComponentInChildren<Image>("ProgressGauge", "Bar");
            if (_progressGaugePin == null) _progressGaugePin = FindRect("Pin");
            if (_progressGaugePinText == null) _progressGaugePinText = FindComponent<TMP_Text>("ProgressText");
            if (_progressGaugeBestPin == null) _progressGaugeBestPin = FindRect("BestPin");
        }

        T FindComponent<T>(string objectName) where T : Component
        {
            Transform child = FindChild(transform, objectName);
            return child == null ? null : child.GetComponent<T>();
        }

        T FindComponentInChildren<T>(string rootName, string objectName) where T : Component
        {
            Transform root = FindChild(transform, rootName);
            Transform child = root == null ? null : FindChild(root, objectName);
            return child == null ? null : child.GetComponent<T>();
        }

        RectTransform FindRect(string objectName)
        {
            Transform child = FindChild(transform, objectName);
            return child as RectTransform;
        }

        static Transform FindChild(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }

        static void SetCanvasGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
