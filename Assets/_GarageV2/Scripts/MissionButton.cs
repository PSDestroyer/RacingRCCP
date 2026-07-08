using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    public void Setup(MissionSO mission, CareerUIController controller, bool isLocked, bool isCompleted)
    {
        this.mission = mission;
        this.controller = controller;
        this.isLocked = isLocked;
        this.isCompleted = isCompleted;

        if (missionNumberText != null)
            missionNumberText.text = mission.missionNumber.ToString("00");

        if (nameText != null)
            nameText.text = mission.missionName;

        if (typeText != null)
            typeText.text = mission.raceType.ToString();

        if (rewardText != null)
            rewardText.text = mission.rewardMoney + " coins";

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
