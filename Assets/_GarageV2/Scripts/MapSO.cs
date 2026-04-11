using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum RaceType
{
    Racing, Elimination, NoBrakeChallenge, DriftScore, TargetDrift, ComboMaster, FreeDrift
}
//Se adauga in create menu ca sa poti crea mai usor SO.
[CreateAssetMenu(fileName = "MapSO_", menuName = "SO/Map")]


public class MapSO : ScriptableObject
{
    [Header("General")]
    [SerializeField] private int _id;
    [SerializeField] private Sprite _mapsprite;
    [SerializeField] private Sprite _countyimg;
    [SerializeField] private string _MapName;
    [SerializeField] private int _price;
    [SerializeField] private int _target;
    [SerializeField] private int _time;
    [SerializeField] private int _lap;
    [SerializeField] private string _date;
    [SerializeField] private string _info;
    [SerializeField] private string _countyName;
    [SerializeField] private RaceType _racetype;

    [Header("Race Mission")]
    [SerializeField] private int _opponentCount = 3;
    [SerializeField] private int _raceLaps = 3;

    [Header("Elimination Mission")]
    [SerializeField] private float _eliminationInterval = 25f;

    [Header("No Brake Mission")]
    [SerializeField] private float _brakeEffectiveness = 0f;
    [SerializeField] private float _handbrakeEffectiveness = 0f;

    [Header("Drift Score Mission")]
    [SerializeField] private int _driftBronzeTarget = 5000;
    [SerializeField] private float _driftSilverMultiplier = 1.5f;
    [SerializeField] private float _driftGoldMultiplier = 2f;

    [Header("Target Drift Mission")]
    [SerializeField] private int _targetDriftScore = 50000;
    [SerializeField] private int _targetDriftTimeLimit = 60;

    [Header("Combo Master Mission")]
    [SerializeField] private float _comboBronzeTarget = 2f;
    [SerializeField] private float _comboSilverTarget = 3.5f;
    [SerializeField] private float _comboGoldTarget = 5f;
    
    public int id => _id;
    public Sprite mapsprite => _mapsprite;
    public Sprite county => _countyimg;
    public string mapName => _MapName;
    public int price => _price;
    public int target => _target;
    public int time => _time;
    public int lap => _lap;
    public string date => _date;
    public string info => _info;
    public string countyname => _countyName;
    public RaceType raceType => _racetype;
    public int opponentCount => _opponentCount;
    public int raceLaps => _raceLaps;
    public float eliminationInterval => _eliminationInterval;
    public float brakeEffectiveness => _brakeEffectiveness;
    public float handbrakeEffectiveness => _handbrakeEffectiveness;
    public int driftBronzeTarget => _driftBronzeTarget;
    public float driftSilverMultiplier => _driftSilverMultiplier;
    public float driftGoldMultiplier => _driftGoldMultiplier;
    public int targetDriftScore => _targetDriftScore;
    public int targetDriftTimeLimit => _targetDriftTimeLimit;
    public float comboBronzeTarget => _comboBronzeTarget;
    public float comboSilverTarget => _comboSilverTarget;
    public float comboGoldTarget => _comboGoldTarget;

}
