using HalvaStudio.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionButtonSelect : MonoBehaviour
{
    [SerializeField] private Image missionImage;
    [SerializeField] private TMP_Text missionLabel;
    [SerializeField] private Button button;

    private MapSelect mapSelect;
    private int missionIndex;
    private MapSO mapSo;

    public void Configure(MapSelect owner, int index, MapSelect.MissionData missionData)
    {
        mapSelect = owner;
        missionIndex = index;
        mapSo = missionData.mapSo;

        if (missionImage != null)
            missionImage.sprite = missionData.missionImage;

        if (missionLabel != null)
            missionLabel.text = string.IsNullOrWhiteSpace(missionData.missionName)
                ? (missionData.mapSo != null ? missionData.mapSo.raceType.ToString() : "Mission")
                : missionData.missionName;

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(SelectMission);
        }
    }

    public void SelectMission()
    {
        if (mapSelect != null)
        {
            mapSelect.SelectMission(missionIndex);
            return;
        }

        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null || mapSo == null)
            return;

        SaveManager.Instance.saveData.currentMap = mapSo.id;
        SaveManager.Instance.saveData.currentMissionMapId = mapSo.id;
        SaveManager.Instance.saveData.currentMissionRaceType = (int)mapSo.raceType;
        SaveManager.Instance.Save();
    }
}
