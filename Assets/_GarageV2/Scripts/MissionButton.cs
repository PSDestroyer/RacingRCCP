using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class MissionButton : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button button;

    private MissionSO mission;
    private CareerUIController controller;

    public void Setup(MissionSO mission, CareerUIController controller)
    {
        this.mission = mission;
        this.controller = controller;
        
        nameText.text = mission.missionName;
        typeText.text = mission.raceType.ToString();
        rewardText.text = mission.rewardMoney + " coins";
        
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        controller.StartMission(mission);
    }
}
