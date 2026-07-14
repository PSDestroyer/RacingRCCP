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

        return PlayerPrefs.GetInt(GetMissionKey(tournament, mission), 0) == 1;
    }

    public static void MarkMissionCompleted(TournamentSO tournament, MissionSO mission)
    {
        if (tournament == null || mission == null)
            return;

        PlayerPrefs.SetInt(GetMissionKey(tournament, mission), 1);
        PlayerPrefs.Save();
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
