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
    [SerializeField] private string _displayName;
    [SerializeField] private int _price;
    [SerializeField] private int _power;
    [SerializeField] private int _color;
    [SerializeField] private int _topSpeed;
    [SerializeField] private int _steerAngle;
    [SerializeField] private int _traction;
    [Min(2000f),Tooltip("BrakePower")]
    [SerializeField] private int _brake;
    [SerializeField] private bool _turbo;
    [Header("Gameplay Camera")]
    [SerializeField] private bool _overrideGameplayCamera;
    [SerializeField] private float _gameplayCameraDistance = 6.5f;
    [SerializeField] private float _gameplayCameraHeight = 1.5f;
    [SerializeField] private float _gameplayCameraPitch = 7.5f;
    [SerializeField] private Vector3 _gameplayCameraOffset = new Vector3(0f, 0f, 0.2f);
    [SerializeField] private bool _gameplayCameraAutoFocus = true;
    
    public int id => _id;
    public string carPrefabLocation => _carPrefabLocation;
    public Sprite carsprite => _carsprite;
    public Sprite CarClass => _CarClass;
    public string carName => _carName;
    public string displayName => string.IsNullOrWhiteSpace(_displayName) ? _carName : _displayName;
    public int price => _price;
    public int power => _power;
    public int speed => _topSpeed;
    public int color => _color;
    public int steerAngle => _steerAngle;
    public int traction => _traction;
    public int brake => _brake;
    public bool turbo => _turbo;
    public bool overrideGameplayCamera => _overrideGameplayCamera;
    public float gameplayCameraDistance => _gameplayCameraDistance;
    public float gameplayCameraHeight => _gameplayCameraHeight;
    public float gameplayCameraPitch => _gameplayCameraPitch;
    public Vector3 gameplayCameraOffset => _gameplayCameraOffset;
    public bool gameplayCameraAutoFocus => _gameplayCameraAutoFocus;

}
