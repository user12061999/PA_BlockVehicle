using UnityEngine;
using UnityEngine.UI;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class InGameGetItemUiView : MonoBehaviour
    {
        Text _title;
        Text _details;
        Image _icon;
        RectTransform _card;

        public void Show(Sprite icon, string itemName, string description)
        {
            if (_card == null) BuildView();
            _title.text = "REWARD RECEIVED!";
            _details.text = itemName + "\n" + description;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Rect area = ((RectTransform)transform).rect;
            _card.sizeDelta = new Vector2(Mathf.Min(760f, area.width * .88f), Mathf.Min(800f, area.height * .85f));
        }

        void BuildView()
        {
            Transform fade = transform.Find("FadeRoot");
            CanvasGroup group = fade.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = group.interactable = true;
            Image background = fade.Find("Bg").GetComponent<Image>();
            background.color = new Color(.02f, .04f, .1f, .88f);
            background.raycastTarget = true;
            _card = (RectTransform)fade.Find("ItemView");
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(.5f, .5f);
            _card.anchoredPosition = Vector2.zero;
            _card.localScale = Vector3.one;
            CanvasGroup cardGroup = _card.GetComponent<CanvasGroup>();
            cardGroup.alpha = 1f;
            cardGroup.blocksRaycasts = cardGroup.interactable = true;
            foreach (Transform child in _card) child.gameObject.SetActive(false);
            transform.Find("MoveIcon").gameObject.SetActive(false);
            transform.Find("FinishEffect").gameObject.SetActive(false);
            Image panel = _card.gameObject.AddComponent<Image>();
            panel.color = new Color(.07f, .13f, .23f, 1f);
            _title = _card.Find("TitleText").GetComponent<Text>();
            _title.gameObject.SetActive(true);
            _title.alignment = TextAnchor.MiddleCenter;
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 18;
            _title.resizeTextMaxSize = 54;
            _title.color = new Color(1f, .85f, .35f);
            _title.raycastTarget = false;
            Fit(_title.rectTransform, new Vector2(.05f, .78f), new Vector2(.95f, .95f));
            _icon = _card.Find("Icon").GetComponent<Image>();
            _icon.gameObject.SetActive(true);
            _icon.color = Color.white;
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            Fit(_icon.rectTransform, new Vector2(.32f, .44f), new Vector2(.68f, .75f));
            _details = Instantiate(_title, _card);
            _details.name = "RewardDetails";
            _details.color = Color.white;
            _details.resizeTextMaxSize = 40;
            Fit(_details.rectTransform, new Vector2(.06f, .25f), new Vector2(.94f, .44f));
            var buttonRect = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            buttonRect.SetParent(_card, false);
            Fit(buttonRect, new Vector2(.17f, .07f), new Vector2(.83f, .2f));
            Image buttonImage = buttonRect.GetComponent<Image>();
            buttonImage.color = new Color(.12f, .65f, .43f);
            Button button = buttonRect.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => gameObject.SetActive(false));
            Text label = Instantiate(_title, buttonRect);
            label.name = "Label";
            label.text = "CONTINUE";
            label.color = Color.white;
            label.resizeTextMaxSize = 42;
            Fit(label.rectTransform, new Vector2(.04f, .08f), new Vector2(.96f, .92f));
        }

        static void Fit(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
