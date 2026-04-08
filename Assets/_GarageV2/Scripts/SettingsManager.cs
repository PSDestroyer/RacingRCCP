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

    private void OnEnable()
    {
        RCCP_SceneManager.Instance.activePlayerVehicle.SetCanControl(true);
        RCCP_SceneManager.Instance.activePlayerVehicle.GetComponent<Rigidbody>().isKinematic = true;

    }

    private void OnDisable()
    {
        RCCP_SceneManager.Instance.activePlayerVehicle.SetCanControl(false);
        RCCP_SceneManager.Instance.activePlayerVehicle.GetComponent<Rigidbody>().isKinematic = false;
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
