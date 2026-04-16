using HalvaStudio.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionButtonSelect : MonoBehaviour
{
    [SerializeField] private Image missionImage;
    [SerializeField] private TMP_Text missionLabel;
    [SerializeField] private Button button;
    [SerializeField] private GameObject lockStateObject;
    [SerializeField] private float lockedAlpha = .4f;
    [SerializeField] private float unlockedAlpha = 1f;

    private MapSelect mapSelect;
    private int missionIndex;
    private MapSO mapSo;
    private bool isUnlocked;

    public void Configure(MapSelect owner, int index, MapSelect.MissionData missionData, bool unlocked, bool completed)
    {
        mapSelect = owner;
        missionIndex = index;
        mapSo = missionData.mapSo;
        isUnlocked = unlocked;

        if (missionImage != null)
            missionImage.sprite = missionData.missionImage;

        if (missionLabel != null)
        {
            string missionName = string.IsNullOrWhiteSpace(missionData.missionName)
                ? (missionData.mapSo != null ? missionData.mapSo.raceType.ToString() : "Mission")
                : missionData.missionName;
            missionLabel.text = unlocked ? missionName : $"{missionName}\nLOCKED";
            missionLabel.alpha = unlocked ? unlockedAlpha : lockedAlpha;
        }

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = unlocked;
            button.onClick.AddListener(SelectMission);
        }

        if (missionImage != null)
        {
            Color color = missionImage.color;
            color.a = unlocked ? unlockedAlpha : lockedAlpha;
            missionImage.color = color;
        }

        if (lockStateObject != null)
            lockStateObject.SetActive(!unlocked);
    }

    public void SelectMission()
    {
        if (!isUnlocked)
            return;

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
