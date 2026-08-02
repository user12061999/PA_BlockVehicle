using UnityEngine;
using UnityEngine.UI;

namespace Gre.UI
{
    public enum ButtonState
    {
        Disable = 0,
        Enable = 1,
    }

    public sealed class CustomButton : Button
    {
        public void SetText(string value)
        {
            Text text = GetComponentInChildren<Text>(true);
            if (text != null) text.text = value;
        }

        public void SetState(ButtonState state)
        {
            interactable = state == ButtonState.Enable;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}
