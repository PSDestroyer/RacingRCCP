using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum RaceType
{
    Racing, Elimination, NoBrakeChallenge, TimeAttack, ChaseRace, DriftScore, PerfectDrift, TargetDrift, ComboMaster, FreeDrift
}
//Se adauga in create menu ca sa poti crea mai usor SO.
[CreateAssetMenu(fileName = "MapSO_", menuName = "SO/Map")]


public class MapSO : ScriptableObject
{
    [Header("General Info")]
    [SerializeField] private int _id;
    [SerializeField] private Sprite _mapsprite;
    [SerializeField] private Sprite _countyimg;
    [SerializeField] private string _MapName;
    [SerializeField] private int _price;
    [SerializeField] private int _target;
    [SerializeField] private int _time;
    [SerializeField] private int _lap;

    [Header("Mission Mode")]
    [Tooltip("Choose the gameplay mode for this mission. Only the matching settings block below is relevant.")]
    [SerializeField] private RaceType _racetype;

    [Header("Race Core")]
    [Tooltip("How many AI opponents spawn in race-style modes that use opponents.")]
    [SerializeField] private int _opponentCount = 3;
    [Tooltip("Lap count used by race-style modes.")]
    [SerializeField] private int _raceLaps = 3;

    [Header("Mode: Elimination")]
    [SerializeField] private float _eliminationInterval = 25f;

    [Header("Mode: Limited Brake Challenge")]
    [SerializeField] private float _brakeEffectiveness = 0f;
    [SerializeField] private float _handbrakeEffectiveness = 0f;
    [SerializeField] private float _limitedBrakeGoldTime = 1f;
    [SerializeField] private float _limitedBrakeSilverTime = 3f;
    [SerializeField] private float _limitedBrakeBronzeTime = 5f;

    [Header("Mode: Time Attack")]
    [SerializeField] private int _timeAttackGoldTime = 75;
    [SerializeField] private int _timeAttackSilverTime = 85;
    [SerializeField] private int _timeAttackBronzeTime = 95;

    [Header("Mode: Chase Race")]
    [SerializeField] private float _chaseHeadStartSeconds = 5f;
    [SerializeField] private int _chaseLapLimit = 2;

    [Header("Mode: Drift Score")]
    [SerializeField] private int _driftBronzeTarget = 5000;
    [SerializeField] private float _driftSilverMultiplier = 1.5f;
    [SerializeField] private float _driftGoldMultiplier = 2f;

    [Header("Mode: Perfect Drift")]
    [SerializeField] private float _perfectDriftBronzeTime = 5f;
    [SerializeField] private float _perfectDriftSilverTime = 10f;
    [SerializeField] private float _perfectDriftGoldTime = 15f;
    [SerializeField] private float _perfectDriftRunTimeLimit = 60f;

    [Header("Mode: Target Drift")]
    [SerializeField] private int _targetDriftScore = 50000;
    [SerializeField] private int _targetDriftTimeLimit = 60;

    [Header("Mode: Combo Master")]
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
    public RaceType raceType => _racetype;
    public int opponentCount => _opponentCount;
    public int raceLaps => _raceLaps;
    public float eliminationInterval => _eliminationInterval;
    public float brakeEffectiveness => _brakeEffectiveness;
    public float handbrakeEffectiveness => _handbrakeEffectiveness;
    public float limitedBrakeGoldTime => _limitedBrakeGoldTime;
    public float limitedBrakeSilverTime => _limitedBrakeSilverTime;
    public float limitedBrakeBronzeTime => _limitedBrakeBronzeTime;
    public int timeAttackGoldTime => _timeAttackGoldTime;
    public int timeAttackSilverTime => _timeAttackSilverTime;
    public int timeAttackBronzeTime => _timeAttackBronzeTime;
    public float chaseHeadStartSeconds => _chaseHeadStartSeconds;
    public int chaseLapLimit => _chaseLapLimit;
    public int driftBronzeTarget => _driftBronzeTarget;
    public float driftSilverMultiplier => _driftSilverMultiplier;
    public float driftGoldMultiplier => _driftGoldMultiplier;
    public float perfectDriftBronzeTime => _perfectDriftBronzeTime;
    public float perfectDriftSilverTime => _perfectDriftSilverTime;
    public float perfectDriftGoldTime => _perfectDriftGoldTime;
    public float perfectDriftRunTimeLimit => _perfectDriftRunTimeLimit;
    public int targetDriftScore => _targetDriftScore;
    public int targetDriftTimeLimit => _targetDriftTimeLimit;
    public float comboBronzeTarget => _comboBronzeTarget;
    public float comboSilverTarget => _comboSilverTarget;
    public float comboGoldTarget => _comboGoldTarget;

}
