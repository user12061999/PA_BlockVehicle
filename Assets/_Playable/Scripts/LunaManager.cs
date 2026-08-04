using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LunaManager : MonoBehaviour
{
    public static LunaManager ins;

    public int countActionCurrent = 0;
    
    [LunaPlaygroundField("Time Show EndGame")] public int timeEndCreative = 60;
    [LunaPlaygroundField("Action to EndGame")] public int CountAction = 10;
    public bool isCretiveEnd;
    public Button[] lstBtnInstall;
    public GameObject EndCard;
    public GameObject retryCard;
    public GameObject WinCard;
    public bool isCretivePause;
    [LunaPlaygroundField("Light Intensity")]public float lightIntensity = 1.5f;
    [LunaPlaygroundField("Light Color")]public Color lightColor=Color.white;
    public List<Light>  lights = new List<Light>();


    private void Awake()
    {
        ins = this;
    }

    private void Start()
    {
        Luna.Unity.LifeCycle.OnPause += PauseGameplay;
        Luna.Unity.LifeCycle.OnResume += ResumeGameplay;

        foreach (Button button in lstBtnInstall)
        {
            if (button != null)
                button.onClick.AddListener(OnClickEndCard);
        }

        HideAllCards();
        foreach (var VARIABLE in lights)
        {
            VARIABLE.color=lightColor;
            VARIABLE.intensity=lightIntensity;
        }
    }

    public void CheckClickShowEndCard(float time = 0)
    {
        if (isCretiveEnd)
            return;

        countActionCurrent++;
        if (CountAction <= 0 || countActionCurrent >= CountAction)
            ShowEndCard(time);
    }

    public void ShowLoseCard(float time = 0f)
    {
        ShowRetryCard(time);
    }

    public void PauseGameplay()
    {
        Time.timeScale = 0;
    }

    public void ResumeGameplay()
    {
        Time.timeScale = 1;
    }

    public void OnTimeExpired()
    {
        ShowRetryCard(0f);
    }

    public void ShowRetryCard(float time = 0f)
    {
        if (isCretiveEnd)
            return;

        isCretiveEnd = true;
        isCretivePause = true;
        Luna.Unity.LifeCycle.GameEnded();
        Invoke(nameof(ShowRetryCardPanel), time);
    }

    public void ShowEndCard(float time = 0f)
    {
        if (isCretiveEnd)
            return;

        isCretiveEnd = true;
        isCretivePause = true;
        Luna.Unity.LifeCycle.GameEnded();
        Invoke(nameof(ShowEndCardPanel), time);
    }
    public void ShowEndCardNodelay()
    {
        ShowEndCard(0f);
    }
    public void ShowWinCard(float time = 0f)
    {
        if (isCretiveEnd)
            return;

        isCretiveEnd = true;
        isCretivePause = true;

        Invoke(nameof(ShowWinCardPanel), time);
        Luna.Unity.LifeCycle.GameEnded();
    }

    public void OnClickEndCard()
    {
        Luna.Unity.Playable.InstallFullGame();
    }

    

    public void OnDestroy()
    {
        Luna.Unity.LifeCycle.OnPause -= PauseGameplay;
        Luna.Unity.LifeCycle.OnResume -= ResumeGameplay;
    }

    public void ShowEndCardPanel()
    {
        if (EndCard != null)
            EndCard.SetActive(true);
    }
    public void ShowRetryCardPanel()
    {
        if (retryCard != null)
            retryCard.SetActive(true);
    }
    public void ShowWinCardPanel()
    {
        if (WinCard != null)
            WinCard.SetActive(true);
    }
    

    private void HideAllCards()
    {
        if (EndCard != null)
            EndCard.SetActive(false);

        if (retryCard != null)
            retryCard.SetActive(false);

        if (WinCard != null)
            WinCard.SetActive(false);
    }
}
