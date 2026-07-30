using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LoadingScreenTextAnimator : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private string localizationKey = "ui.preparing_track";
    [SerializeField] private string baseText = "Preparing Track";
    [SerializeField] private float stepTime = 0.35f;

    private float timer;
    private int dots;
    private string localizedText;

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        RefreshLocalizedText();
        timer = 0f;
        dots = 0;
        UpdateText();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;

        if (timer < stepTime)
            return;

        timer = 0f;
        dots = (dots + 1) % 4;
        UpdateText();
    }

    private void UpdateText()
    {
        if (targetText == null)
            return;

        targetText.text = localizedText + new string('.', dots);
    }

    private void OnLocaleChanged(Locale _)
    {
        RefreshLocalizedText();
        UpdateText();
    }

    private void RefreshLocalizedText()
    {
        localizedText = UILocalization.Get(localizationKey, baseText);
    }
}
