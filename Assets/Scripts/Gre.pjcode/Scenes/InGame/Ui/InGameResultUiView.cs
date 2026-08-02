using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class InGameResultUiView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _earnedGoldTmpText;
        [SerializeField] private Text _earnedGoldText;
        [SerializeField] private Button _claimButton;

        void Awake()
        {
            CacheViews();
            SetOpen(false);
        }

        public void SetClaimAction(UnityAction action)
        {
            CacheViews();
            if (_claimButton == null) return;
            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(() =>
            {
                SetOpen(false);
                action?.Invoke();
            });
        }

        public void Open(int earnedGold)
        {
            CacheViews();
            string value = earnedGold.ToString();
            if (_earnedGoldTmpText != null) _earnedGoldTmpText.text = value;
            if (_earnedGoldText != null) _earnedGoldText.text = value;
            SetOpen(true);
        }

        void SetOpen(bool isOpen)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = isOpen ? 1f : 0f;
                _canvasGroup.interactable = isOpen;
                _canvasGroup.blocksRaycasts = isOpen;
            }

            gameObject.SetActive(isOpen);
        }

        void CacheViews()
        {
            _canvasGroup ??= GetComponent<CanvasGroup>();
            _claimButton ??= GetComponentInChildren<Button>(true);
            if (_earnedGoldTmpText != null || _earnedGoldText != null) return;

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "EarnedGoldText" && child.name != "GoldText") continue;
                _earnedGoldTmpText = child.GetComponent<TMP_Text>();
                _earnedGoldText = child.GetComponent<Text>();
                if (_earnedGoldTmpText != null || _earnedGoldText != null) return;
            }

            _earnedGoldTmpText = GetComponentInChildren<TMP_Text>(true);
            _earnedGoldText = GetComponentInChildren<Text>(true);
        }
    }
}
