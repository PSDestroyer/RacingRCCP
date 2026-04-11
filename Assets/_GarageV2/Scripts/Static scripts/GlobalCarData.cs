using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public static class GlobalCarData
{
    public static List<CarSO> _carlists;
    public static CarSO _currentCar;
    public static List<Button> _buttonList = new List<Button>();
    public static List<MapSO> _maplists;
    public static MapSO thismap;
    public static List<Button> _mapbuttonList = new List<Button>();
    
    [RuntimeInitializeOnLoadMethod]
    public static void Initialize()
    {
        //Incarca toate SO in lista
        _carlists  = Resources.LoadAll<CarSO>(path: PathLocation.CarsDataLocation).OrderBy(so =>so.id ).ToList();
        _maplists = Resources.LoadAll<MapSO>(PathLocation.MapsDataLocarion).OrderBy(so=>so.id).ToList();
        
        //Aici Current car cauta Prin lista de SO ID si il compara cu cel din PlayerPrefs
        _currentCar = _carlists.Find(e => e.id == PlayerPrefs.GetInt(SaveKeys.CurrentCar,0));
        
    }

    public static MapSO GetMapById(int id)
    {
        if (_maplists == null || _maplists.Count == 0)
            return null;

        return _maplists.Find(map => map != null && map.id == id);
    }
}
