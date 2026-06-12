using System;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapSelect : MonoBehaviour
{
    [Serializable]
    public class MissionData
    {
        public string missionName;
        public Sprite missionImage;
        public MapSO mapSo;
    }

    [Serializable]
    public class TrackData
    {
        public string trackName;
        public Sprite trackImage;
        public int trackMapId = -1;
        public List<MissionData> missions = new List<MissionData>(4);
    }

    [Serializable]
    public class MapData
    {
        public string mapName;
        public List<TrackData> tracks = new List<TrackData>(4);
    }

    public List<MapData> maps = new List<MapData>(4);
    [Header("Debug")]
    [SerializeField] private bool unlockAllLevelsForTesting = false;
    public GameObject MapPanel;
    public GameObject TrackPanel;
    public GameObject MissionPanel;
    public GameObject PlayPanel;

    public TrackPanel trackPanelController;
    public MissionPanel missionPanelController;

    private int selectedMapIndex = -1;
    private int selectedTrackIndex = -1;
    private int selectedMissionIndex = -1;

    private void Start()
    {
        EnsureMissionProgressInitialized();
        ResetPanels();
    }

    private void OnEnable()
    {
        EnsureMissionProgressInitialized();
        ResetPanels();
    }

    public void SelectMap(int mapIndex)
    {
        if (mapIndex < 0 || mapIndex >= maps.Count)
            return;

        selectedMapIndex = mapIndex;
        selectedTrackIndex = -1;
        selectedMissionIndex = -1;

        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
        {
            SaveManager.Instance.saveData.selectedMapName = maps[mapIndex].mapName;
            SaveManager.Instance.saveData.selectedTrackName = string.Empty;
            SaveManager.Instance.saveData.selectedMapIndex = mapIndex;
            SaveManager.Instance.saveData.selectedTrackIndex = -1;
            SaveManager.Instance.saveData.selectedMissionIndex = -1;
            SaveManager.Instance.saveData.currentMapTrackCount = maps[mapIndex].tracks != null ? maps[mapIndex].tracks.Count : 0;
            SaveManager.Instance.saveData.currentTrackMissionCount = 0;
            SaveManager.Instance.saveData.currentMissionMapId = -1;
            SaveManager.Instance.saveData.currentMissionRaceType = -1;
            SaveManager.Instance.Save();
        }

        ShowTrackStep();

        if (trackPanelController != null)
            trackPanelController.ShowTracks(this, maps[mapIndex].tracks);

        RequestSelection(trackPanelController != null ? trackPanelController.GetFirstSelectableTrackButton() : null, TrackPanel);

        PlayClick();
    }

    public void SelectTrack(int trackIndex)
    {
        if (selectedMapIndex < 0 || selectedMapIndex >= maps.Count)
            return;

        List<TrackData> tracks = maps[selectedMapIndex].tracks;

        if (trackIndex < 0 || trackIndex >= tracks.Count)
            return;

        if (!IsTrackUnlocked(selectedMapIndex, trackIndex))
            return;

        selectedTrackIndex = trackIndex;
        selectedMissionIndex = -1;

        TrackData selectedTrack = tracks[trackIndex];
        GlobalCarData.thismap = GlobalCarData.GetMapById(selectedTrack.trackMapId);

        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
        {
            SaveManager.Instance.saveData.selectedTrackName = selectedTrack.trackName;
            SaveManager.Instance.saveData.selectedTrackIndex = trackIndex;
            SaveManager.Instance.saveData.selectedMissionIndex = -1;
            SaveManager.Instance.saveData.currentMapTrackCount = tracks.Count;
            SaveManager.Instance.saveData.currentTrackMissionCount = selectedTrack.missions != null ? selectedTrack.missions.Count : 0;
            SaveManager.Instance.saveData.currentMap = selectedTrack.trackMapId;
            SaveManager.Instance.saveData.currentMissionMapId = selectedTrack.trackMapId;
            SaveManager.Instance.saveData.currentMissionRaceType = -1;
            SaveManager.Instance.Save();
        }

        ShowMissionStep();

        if (missionPanelController != null)
            missionPanelController.ShowMissions(this, selectedTrack.missions);

        RequestSelection(missionPanelController != null ? missionPanelController.GetFirstSelectableMissionButton() : null, MissionPanel);

        PlayClick();
    }

    public void SelectMission(int missionIndex)
    {
        if (selectedMapIndex < 0 || selectedMapIndex >= maps.Count)
            return;

        if (selectedTrackIndex < 0 || selectedTrackIndex >= maps[selectedMapIndex].tracks.Count)
            return;

        List<MissionData> missions = maps[selectedMapIndex].tracks[selectedTrackIndex].missions;

        if (missionIndex < 0 || missionIndex >= missions.Count)
            return;

        if (!IsMissionUnlocked(selectedMapIndex, selectedTrackIndex, missionIndex))
            return;

        MissionData mission = missions[missionIndex];

        if (mission == null || mission.mapSo == null)
            return;
        SetUpRaceStyle(mission.mapSo.raceType);
        
        selectedMissionIndex = missionIndex;
        GlobalCarData.thismap = mission.mapSo;

        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
        {
            SaveManager.Instance.saveData.selectedMissionIndex = missionIndex;
            SaveManager.Instance.saveData.currentMapTrackCount = maps[selectedMapIndex].tracks.Count;
            SaveManager.Instance.saveData.currentTrackMissionCount = missions.Count;
            SaveManager.Instance.saveData.currentMap = mission.mapSo.id;
            SaveManager.Instance.saveData.currentMissionMapId = mission.mapSo.id;
            SaveManager.Instance.saveData.currentMissionRaceType = (int)mission.mapSo.raceType;
            SaveManager.Instance.Save();

            
            
            if (!string.IsNullOrWhiteSpace(SaveManager.Instance.saveData.selectedTrackName) && LoadingManager.Instance != null)
            {
                LoadingManager.Instance.LoadScene(SaveManager.Instance.saveData.selectedTrackName);
                PlayClick();
                return;
            }

            if (!string.IsNullOrWhiteSpace(SaveManager.Instance.saveData.selectedTrackName))
            {
                SceneManager.LoadScene(SaveManager.Instance.saveData.selectedTrackName);
                PlayClick();
                return;
            }
        }

    }
    public void SetUpRaceStyle(RaceType raceType)
    {
        SetUpRaceStyle(GetDrivingStyleIndex(raceType));
    }

    public void SetUpRaceStyle(int type)
    {
        // 0  = Balanced
        // 1  = Drift
        // 2  = Race
        // 3  = Arcade
        if (RCCP_Settings.Instance == null || RCCP_Settings.Instance.behaviorTypes == null || RCCP_Settings.Instance.behaviorTypes.Length == 0)
            return;

        int safeBehaviorIndex = Mathf.Clamp(type, 0, RCCP_Settings.Instance.behaviorTypes.Length - 1);
        RCCP_Settings.Instance.behaviorSelectedIndex = safeBehaviorIndex;

        if (RCCP_SceneManager.Instance != null)
            RCCP_SceneManager.Instance.SetBehavior(safeBehaviorIndex);

        // Debug.Log(RCCP_Settings.Instance.behaviorTypes[type].behaviorName.ToString()); // Test Debug style
    }

    private int GetDrivingStyleIndex(RaceType raceType)
    {
        switch (raceType)
        {
            case RaceType.Racing:
            case RaceType.Elimination:
            case RaceType.NoBrakeChallenge:
            case RaceType.TimeAttack:
            case RaceType.ChaseRace:
                return 2;

            case RaceType.FreeDrift:
            case RaceType.DriftScore:
            case RaceType.PerfectDrift:
            case RaceType.TargetDrift:
            case RaceType.ComboMaster:
                return 1;

            default:
                return 0;
        }
    }

    public bool IsTrackUnlocked(int mapIndex, int trackIndex)
    {
        if (unlockAllLevelsForTesting)
            return true;

        return SaveManager.Instance == null || SaveManager.Instance.IsTrackUnlocked(mapIndex, trackIndex);
    }

    public bool IsMissionUnlocked(int mapIndex, int trackIndex, int missionIndex)
    {
        if (unlockAllLevelsForTesting)
            return true;

        return SaveManager.Instance == null || SaveManager.Instance.IsMissionUnlocked(mapIndex, trackIndex, missionIndex);
    }

    public bool IsMissionCompleted(int mapIndex, int trackIndex, int missionIndex)
    {
        return SaveManager.Instance != null && SaveManager.Instance.IsMissionCompleted(mapIndex, trackIndex, missionIndex);
    }

    public string GetMissionMedalText(int mapIndex, int trackIndex, int missionIndex)
    {
        if (SaveManager.Instance == null)
            return string.Empty;

        return SaveManager.Instance.GetMissionMedal(mapIndex, trackIndex, missionIndex);
    }

    public bool HandleBack()
    {
        if (MissionPanel != null && MissionPanel.activeSelf)
        {
            selectedMissionIndex = -1;

            if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
            {
                SaveManager.Instance.saveData.selectedMissionIndex = -1;
                SaveManager.Instance.saveData.currentMissionRaceType = -1;
                SaveManager.Instance.Save();
            }

            ShowTrackStep();
            RequestSelection(trackPanelController != null ? trackPanelController.GetFirstSelectableTrackButton() : null, TrackPanel);
            PlayClick();
            return true;
        }

        if (TrackPanel != null && TrackPanel.activeSelf)
        {
            selectedTrackIndex = -1;
            selectedMissionIndex = -1;

            if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
            {
                SaveManager.Instance.saveData.selectedTrackName = string.Empty;
                SaveManager.Instance.saveData.selectedTrackIndex = -1;
                SaveManager.Instance.saveData.selectedMissionIndex = -1;
                SaveManager.Instance.saveData.currentMissionMapId = -1;
                SaveManager.Instance.saveData.currentMissionRaceType = -1;
                SaveManager.Instance.Save();
            }

            ResetPanels();
            RequestSelection(GetFirstSelectableInPanel(MapPanel), MapPanel);
            PlayClick();
            return true;
        }

        return false;
    }

    private void ResetPanels()
    {
        if (MapPanel != null)
            MapPanel.SetActive(true);

        if (TrackPanel != null)
            TrackPanel.SetActive(false);

        if (MissionPanel != null)
            MissionPanel.SetActive(false);

        if (PlayPanel != null)
            PlayPanel.SetActive(false);

        RequestSelection(GetFirstSelectableInPanel(MapPanel), MapPanel);
    }

    private void ShowTrackStep()
    {
        if (MapPanel != null)
            MapPanel.SetActive(false);

        if (TrackPanel != null)
            TrackPanel.SetActive(true);

        if (MissionPanel != null)
            MissionPanel.SetActive(false);

        if (PlayPanel != null)
            PlayPanel.SetActive(false);
    }

    private void ShowMissionStep()
    {
        if (MapPanel != null)
            MapPanel.SetActive(false);

        if (TrackPanel != null)
            TrackPanel.SetActive(false);

        if (MissionPanel != null)
            MissionPanel.SetActive(true);

        if (PlayPanel != null)
            PlayPanel.SetActive(false);
    }

    private void RequestSelection(GameObject preferredTarget, GameObject panelRoot)
    {
        StartCoroutine(SelectPanelButtonNextFrame(preferredTarget, panelRoot));
    }

    private IEnumerator SelectPanelButtonNextFrame(GameObject preferredTarget, GameObject panelRoot)
    {
        yield return null;

        if (EventSystem.current == null)
            yield break;

        GameObject selectedObject = preferredTarget != null && preferredTarget.activeInHierarchy
            ? preferredTarget
            : GetFirstSelectableInPanel(panelRoot);

        if (selectedObject == null)
            yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectedObject);
    }

    private GameObject GetFirstSelectableInPanel(GameObject panelRoot)
    {
        if (panelRoot == null)
            return null;

        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                continue;

            return button.gameObject;
        }

        return null;
    }

    private void PlayClick()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();
    }

    private void EnsureMissionProgressInitialized()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.EnsureMissionProgressInitialized();
    }
}
