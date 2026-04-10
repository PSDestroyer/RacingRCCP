using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum RaceType
{
    Racing, DriftScore, FreeDrift
}
//Se adauga in create menu ca sa poti crea mai usor SO.
[CreateAssetMenu(fileName = "MapSO_", menuName = "SO/Map")]


public class MapSO : ScriptableObject
{
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

}
