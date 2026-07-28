using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

public class MissionButton : MonoBehaviour, ISelectHandler
{
    [SerializeField] private TMP_Text missionNumberText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Image lockIcon;
    [SerializeField] private Image completedIcon;
    [SerializeField] private Button button;

    private MissionSO mission;
    private bool isLocked;
    private bool isCompleted;
    private CareerUIController controller;
    public Button Button => button;
    public RectTransform RectTransform => transform as RectTransform;
    public MissionSO Mission => mission;
    public bool IsLocked => isLocked;
    public bool IsCompleted => isCompleted;

    private void Awake()
    {
        DisableStaticLocalizer(nameText);
        DisableStaticLocalizer(typeText);
        DisableStaticLocalizer(rewardText);
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        RefreshLocalizedText();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    public void Setup(MissionSO mission, CareerUIController controller, bool isLocked, bool isCompleted)
    {
        this.mission = mission;
        this.controller = controller;
        this.isLocked = isLocked;
        this.isCompleted = isCompleted;

        if (missionNumberText != null)
            missionNumberText.text = mission.missionNumber.ToString("00");

        RefreshLocalizedText();

        if (lockIcon != null)
            lockIcon.gameObject.SetActive(isLocked && !isCompleted);

        if (completedIcon != null)
            completedIcon.gameObject.SetActive(isCompleted);
        
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnLocaleChanged(Locale _)
    {
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        if (mission == null)
            return;

        if (nameText != null)
            nameText.text = UILocalization.GetKnownText(mission.missionName);

        if (typeText != null)
            typeText.text = UILocalization.GetKnownText(mission.raceType.ToString());

        if (rewardText != null)
            rewardText.text = mission.rewardMoney + " CR";
    }

    private static void DisableStaticLocalizer(TMP_Text text)
    {
        if (text == null)
            return;

        LocalizeStringEvent localizer = text.GetComponent<LocalizeStringEvent>();
        if (localizer != null)
            localizer.enabled = false;
    }

    private void OnClick()
    {
        if (isLocked)
            return;

        controller.StartMission(mission);
    }

    public void OnSelect(BaseEventData eventData)
    {
        controller?.OnMissionButtonSelected(this);
    }
}
