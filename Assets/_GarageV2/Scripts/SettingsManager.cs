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
    public bool controlVehicleWhileSettingsOpen = false;

    private void Start()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        if (sfx != null)
            sfx.value = SaveManager.Instance.saveData.soundLevel;

        if (vehicle != null)
            vehicle.value = SaveManager.Instance.saveData.VehicleLevel;

        if (music != null)
            music.value = SaveManager.Instance.saveData.musicLevel;

        if (vibrationToggle != null)
            vibrationToggle.isOn = SaveManager.Instance.saveData.vibrationsState;

        if (easyDriftModeToggle != null)
            easyDriftModeToggle.isOn = SaveManager.Instance.saveData.easyDriftMode;
    }

    private void OnEnable()
    {
        if (controlVehicleWhileSettingsOpen)
            SetVehicleSettingsState(false);
    }

    private void OnDisable()
    {
        if (controlVehicleWhileSettingsOpen)
            SetVehicleSettingsState(true);

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }

    public void OnSetSfxVolume(float value)
    {
        if (SoundManager.Instance == null)
            return;

        SoundManager.Instance.SetSfxVolume(value);
    }

    public void OnSetVehicleVolume(float value)
    {
        if (SoundManager.Instance == null)
            return;

        SoundManager.Instance.SetVehicleVolume(value);
    }

    public void OnSetMusicVolume(float value)
    {
        if (SoundManager.Instance == null)
            return;

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

    private void SetVehicleSettingsState(bool state)
    {
        if (RCCP_SceneManager.Instance == null || RCCP_SceneManager.Instance.activePlayerVehicle == null)
            return;

        RCCP_CarController activeVehicle = RCCP_SceneManager.Instance.activePlayerVehicle;
        activeVehicle.SetCanControl(state);

        Rigidbody vehicleRigidbody = activeVehicle.GetComponent<Rigidbody>();

        if (vehicleRigidbody != null)
            vehicleRigidbody.isKinematic = state;
    }
}
