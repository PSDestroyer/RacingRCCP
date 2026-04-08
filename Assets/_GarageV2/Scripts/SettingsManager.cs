using System;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public Slider sfx;
    public Slider vehicle;
    public Slider music;
    private void Start()
    {
        sfx.value = SaveManager.Instance.saveData.soundLevel;
        vehicle.value = SaveManager.Instance.saveData.VehicleLevel;
        music.value = SaveManager.Instance.saveData.musicLevel;
    }

    private void OnDisable()
    {
        SaveManager.Instance.Save();
    }

    public void OnSetSfxVolume(float value)
    {
        SoundManager.Instance.SetSfxVolume(value);
    }
    public void OnSetVehicleVolume(float value)
    {
        SoundManager.Instance.SetVehicleVolume(value);
    }
    public void OnSetMusicVolume(float value)
    {
        SoundManager.Instance.SetMusicVolume(value);
    }
}
