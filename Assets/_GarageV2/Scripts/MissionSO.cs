using UnityEngine;

[CreateAssetMenu(fileName = "MissionSO", menuName = "SO/Missions")]
public class MissionSO : ScriptableObject
{
    public string missionName;
    public Sprite preview;
    public RaceType raceType;
    
    public string sceneName = "GameplayTestScene";

    public int rewardMoney;
    public int rewardExp;

    public int laps = 3;
    public int opponentCount = 3;
    public int targetScore;
    public int timeLimit;

    [Header("AI Difficulty")]
    public bool useMissionAISettings = true;
    public RCCP_AIArcadePreset.Difficulty opponentDifficulty = RCCP_AIArcadePreset.Difficulty.Medium;
    public RCCP_AIArcadePreset[] opponentAIPresets;
    [Range(0f, .5f)] public float rubberBandStrength = .24f;

    public bool isLockedByDefault;

}
