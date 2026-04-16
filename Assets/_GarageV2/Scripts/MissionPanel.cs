using System.Collections.Generic;
using UnityEngine;

public class MissionPanel : MonoBehaviour
{
    private List<MapSelect.MissionData> missions = new List<MapSelect.MissionData>();
    private MapSelect mapSelect;
    public Transform MissionContent;
    public MissionButtonSelect MissionButton;
    public List<MissionButtonSelect> MissionButtons = new List<MissionButtonSelect>();

    public void ShowMissions(MapSelect owner, List<MapSelect.MissionData> missionList)
    {
        mapSelect = owner;
        missions = missionList ?? new List<MapSelect.MissionData>();
        Initialize();
    }

    public void Initialize()
    {
        ClearList();

        for (int i = 0; i < missions.Count; i++)
        {
            var button = Instantiate(MissionButton, MissionContent);
            bool isUnlocked = mapSelect == null || mapSelect.IsMissionUnlocked(GetSelectedMapIndex(), GetSelectedTrackIndex(), i);
            bool isCompleted = mapSelect != null && mapSelect.IsMissionCompleted(GetSelectedMapIndex(), GetSelectedTrackIndex(), i);
            button.Configure(mapSelect, i, missions[i], isUnlocked, isCompleted);
            MissionButtons.Add(button);
        }
    }

    public void ClearList()
    {
        for (int i = 0; i < MissionButtons.Count; i++)
        {
            if (MissionButtons[i] != null)
                Destroy(MissionButtons[i].gameObject);
        }

        MissionButtons.Clear();
    }

    private int GetSelectedMapIndex()
    {
        if (HalvaStudio.Save.SaveManager.Instance == null || HalvaStudio.Save.SaveManager.Instance.saveData == null)
            return 0;

        return Mathf.Max(0, HalvaStudio.Save.SaveManager.Instance.saveData.selectedMapIndex);
    }

    private int GetSelectedTrackIndex()
    {
        if (HalvaStudio.Save.SaveManager.Instance == null || HalvaStudio.Save.SaveManager.Instance.saveData == null)
            return 0;

        return Mathf.Max(0, HalvaStudio.Save.SaveManager.Instance.saveData.selectedTrackIndex);
    }
}
