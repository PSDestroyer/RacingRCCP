using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TournamentSO", menuName = "SO/Tournament")]
public class TournamentSO : ScriptableObject
{
    public string tournamentName;
    public Sprite icon;
    public Sprite backgroundImage;
    public string description;

    public List<MissionSO> missions;
}
