using UnityEngine;

[CreateAssetMenu(fileName = "MissionSO", menuName = "SO/Missions")]
public class MissionSO : ScriptableObject
{
    [Header("UI")]
    [Min(1)] public int missionNumber = 1;

    public string missionName;
    public Sprite preview;
    public RaceType raceType;
    
    public string sceneName = "GameplayTestScene";

    public int rewardMoney;
    public int rewardExp;

    [Header("Race Settings")]
    public int laps = 3;
    public int opponentCount = 3;

    [Header("Elimination Settings")]
    public float eliminationInterval = 25f;

    [Header("No Brake Settings")]
    [Range(0f, 1f)] public float brakeEffectiveness = 0f;
    [Range(0f, 1f)] public float handbrakeEffectiveness = 0f;

    [Header("Drift / Timed Settings")]
    public int targetScore;
    public int timeLimit;

    [Header("AI Difficulty")]
    public bool useMissionAISettings = true;
    public RCCP_AIArcadePreset.Difficulty opponentDifficulty = RCCP_AIArcadePreset.Difficulty.Medium;
    public RCCP_AIArcadePreset[] opponentAIPresets;
    [Range(0f, .5f)] public float rubberBandStrength = .24f;

    public bool isLockedByDefault;

}
