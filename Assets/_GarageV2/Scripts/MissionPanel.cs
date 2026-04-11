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
            button.Configure(mapSelect, i, missions[i]);
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
}
