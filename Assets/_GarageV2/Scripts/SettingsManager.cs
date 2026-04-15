using System;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public Slider sfx;
    public Slider vehicle;
    public Slider music;
    public Toggle vibrationToggle;
    public Toggle easyDriftModeToggle;
    private void Start()
    {
        sfx.value = SaveManager.Instance.saveData.soundLevel;
        vehicle.value = SaveManager.Instance.saveData.VehicleLevel;
        music.value = SaveManager.Instance.saveData.musicLevel;

        if (vibrationToggle != null)
            vibrationToggle.isOn = SaveManager.Instance.saveData.vibrationsState;

        if (easyDriftModeToggle != null)
            easyDriftModeToggle.isOn = SaveManager.Instance.saveData.easyDriftMode;
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

    public void OnSetVibration(bool value)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        SaveManager.Instance.saveData.vibrationsState = value;
    }

    public void OnSetEasyDriftMode(bool value)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        SaveManager.Instance.saveData.easyDriftMode = value;
    }
}
