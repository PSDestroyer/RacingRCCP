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
    [TextArea(2, 6)]
    [SerializeField] private string _carInfo;
    [SerializeField] private int _price;
    [SerializeField] private int _color;
    
    public int id => _id;
    public string carPrefabLocation => _carPrefabLocation;
    public Sprite carsprite => _carsprite;
    public Sprite CarClass => _CarClass;
    public string carName => _carName;
    public string carInfo => _carInfo;
    public int price => _price;
    public int color => _color;

}
