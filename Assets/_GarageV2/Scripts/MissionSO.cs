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

    public bool isLockedByDefault;

}
