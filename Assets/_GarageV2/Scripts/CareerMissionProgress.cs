using System.Collections.Generic;
using HalvaStudio.Save;
using UnityEngine;

public static class CareerMissionProgress
{
    private static string GetMissionKey(TournamentSO tournament, MissionSO mission)
    {
        string tournamentId = tournament != null ? tournament.name : "UnknownTournament";
        string missionId = mission != null ? mission.name : "UnknownMission";
        return $"{SaveKeys.CareerMissionProgress}_{tournamentId}_{missionId}";
    }

    public static bool IsMissionCompleted(TournamentSO tournament, MissionSO mission)
    {
        if (tournament == null || mission == null)
            return false;

        string missionKey = GetMissionKey(tournament, mission);
        List<string> completedMissions = GetCompletedMissions();

        if (completedMissions == null)
            return false;

        if (completedMissions.Contains(missionKey))
            return true;

#if UNITY_EDITOR
        // One-time migration for progress created by the previous PlayerPrefs system.
        if (PlayerPrefs.GetInt(missionKey, 0) == 1)
        {
            completedMissions.Add(missionKey);
            SaveManager.Instance.Save();
            PlayerPrefs.DeleteKey(missionKey);
            PlayerPrefs.Save();
            return true;
        }
#endif

        return false;
    }

    public static void MarkMissionCompleted(TournamentSO tournament, MissionSO mission, bool saveImmediately = true)
    {
        if (tournament == null || mission == null)
            return;

        List<string> completedMissions = GetCompletedMissions();

        if (completedMissions == null)
            return;

        string missionKey = GetMissionKey(tournament, mission);
        if (completedMissions.Contains(missionKey))
            return;

        completedMissions.Add(missionKey);

        if (saveImmediately)
            SaveManager.Instance.Save(true);
    }

    private static List<string> GetCompletedMissions()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return null;

        if (SaveManager.Instance.saveData.completedCareerMissions == null)
            SaveManager.Instance.saveData.completedCareerMissions = new List<string>();

        return SaveManager.Instance.saveData.completedCareerMissions;
    }

    public static bool IsMissionUnlocked(TournamentSO tournament, int missionIndex)
    {
        if (!IsTournamentUnlocked(tournament))
            return false;

        if (tournament == null || tournament.missions == null || missionIndex < 0 || missionIndex >= tournament.missions.Count)
            return false;

        MissionSO mission = tournament.missions[missionIndex];
        if (mission == null)
            return false;

        if (IsMissionCompleted(tournament, mission))
            return true;

        if (missionIndex == 0)
            return !mission.isLockedByDefault;

        MissionSO previousMission = tournament.missions[missionIndex - 1];
        return previousMission != null && IsMissionCompleted(tournament, previousMission);
    }

    public static bool IsTournamentUnlocked(TournamentSO tournament)
    {
        if (tournament == null)
            return false;

        if (tournament.prerequisiteTournament == null)
            return true;

        return IsTournamentCompleted(tournament.prerequisiteTournament);
    }

    public static bool IsTournamentCompleted(TournamentSO tournament)
    {
        if (tournament == null || tournament.missions == null || tournament.missions.Count == 0)
            return false;

        for (int i = 0; i < tournament.missions.Count; i++)
        {
            MissionSO mission = tournament.missions[i];
            if (mission == null || !IsMissionCompleted(tournament, mission))
                return false;
        }

        return true;
    }
}
