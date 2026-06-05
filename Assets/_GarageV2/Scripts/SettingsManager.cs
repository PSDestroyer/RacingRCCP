using System;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public Slider sfx;
    public Slider vehicle;
    public Slider music;
    [SerializeField] private bool managePreviewVehicleState = true;

    private void Start()
    {
        RefreshUIFromSave();
    }

    private void OnEnable()
    {
        if (!managePreviewVehicleState)
            return;

        if (RCCP_SceneManager.Instance == null || RCCP_SceneManager.Instance.activePlayerVehicle == null)
            return;

        RCCP_SceneManager.Instance.activePlayerVehicle.SetCanControl(true);

        Rigidbody rigidbody = RCCP_SceneManager.Instance.activePlayerVehicle.GetComponent<Rigidbody>();
        if (rigidbody != null)
            rigidbody.isKinematic = true;

    }

    private void OnDisable()
    {
        if (managePreviewVehicleState && RCCP_SceneManager.Instance != null && RCCP_SceneManager.Instance.activePlayerVehicle != null)
        {
            RCCP_SceneManager.Instance.activePlayerVehicle.SetCanControl(false);

            Rigidbody rigidbody = RCCP_SceneManager.Instance.activePlayerVehicle.GetComponent<Rigidbody>();
            if (rigidbody != null)
                rigidbody.isKinematic = false;
        }

        SaveSettings();
    }

    public void RefreshUIFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        if (sfx != null)
            sfx.SetValueWithoutNotify(SaveManager.Instance.saveData.soundLevel);

        if (vehicle != null)
            vehicle.SetValueWithoutNotify(SaveManager.Instance.saveData.VehicleLevel);

        if (music != null)
            music.SetValueWithoutNotify(SaveManager.Instance.saveData.musicLevel);
    }

    public void SaveSettings()
    {
        if (SaveManager.Instance != null)
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
