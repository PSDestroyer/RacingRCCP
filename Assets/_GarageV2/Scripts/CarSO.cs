using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Se adauga in create menu ca sa poti crea mai usor SO.
[CreateAssetMenu(fileName = "CarSO_", menuName = "SO/Car")]
public class CarSO : ScriptableObject
{
    [SerializeField] private int _id;
    [SerializeField] private string _carPrefabLocation;
    [SerializeField] private Sprite _carsprite;
    [SerializeField] private Sprite _CarClass;
    [SerializeField] private string _carName;
    [SerializeField] private int _price;
    [SerializeField] private int _power;
    [SerializeField] private int _color;
    [SerializeField] private int _topSpeed;
    [SerializeField] private int _steerAngle;
    [SerializeField] private int _traction;
    [Min(2000f),Tooltip("BrakePower")]
    [SerializeField] private int _brake;
    [SerializeField] private bool _turbo;
    
    public int id => _id;
    public string carPrefabLocation => _carPrefabLocation;
    public Sprite carsprite => _carsprite;
    public Sprite CarClass => _CarClass;
    public string carName => _carName;
    public int price => _price;
    public int power => _power;
    public int speed => _topSpeed;
    public int color => _color;
    public int steerAngle => _steerAngle;
    public int traction => _traction;
    public int brake => _brake;
    public bool turbo => _turbo;

}
