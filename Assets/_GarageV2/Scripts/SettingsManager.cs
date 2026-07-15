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

    private RCCP_CarController lockedVehicle;
    private bool vehicleStateLocked;
    private bool previousCanControl;
    private bool previousExternalControl;
    private bool previousRigidbodyKinematic;
    private RCCP_InputManager lockedInputManager;
    private RCCP_Inputs previousInputManagerInputs;
    private bool inputManagerStateLocked;
    private bool previousInputManagerOverride;
    private bool previousSuppressGameplayActionEvents;

    private void Start()
    {
        RefreshUIFromSave();
    }

    private void OnEnable()
    {
        LockRCCPInputManager();
        LockVehicleInput();
    }

    private void OnDisable()
    {
        RestoreVehicleInput();
        SaveSettings();
    }

    private void LockVehicleInput()
    {
        if (!managePreviewVehicleState || vehicleStateLocked)
            return;

        if (RCCP_SceneManager.Instance == null || RCCP_SceneManager.Instance.activePlayerVehicle == null)
            return;

        lockedVehicle = RCCP_SceneManager.Instance.activePlayerVehicle;
        previousCanControl = lockedVehicle.canControl;
        previousExternalControl = lockedVehicle.externalControl;

        Rigidbody rigidbody = lockedVehicle.GetComponent<Rigidbody>();
        previousRigidbodyKinematic = rigidbody != null && rigidbody.isKinematic;

        lockedVehicle.externalControl = false;
        lockedVehicle.SetCanControl(false);

        if (lockedVehicle.Inputs != null)
            lockedVehicle.Inputs.OverrideInputs(new RCCP_Inputs());

        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
        }

        vehicleStateLocked = true;
    }

    private void RestoreVehicleInput()
    {
        RestoreRCCPInputManager();

        if (!vehicleStateLocked)
            return;

        if (lockedVehicle != null)
        {
            if (lockedVehicle.Inputs != null)
                lockedVehicle.Inputs.DisableOverrideInputs();

            lockedVehicle.externalControl = previousExternalControl;
            lockedVehicle.SetCanControl(previousCanControl);

            Rigidbody rigidbody = lockedVehicle.GetComponent<Rigidbody>();
            if (rigidbody != null)
                rigidbody.isKinematic = previousRigidbodyKinematic;
        }

        lockedVehicle = null;
        vehicleStateLocked = false;
    }

    private void LockRCCPInputManager()
    {
        if (inputManagerStateLocked)
            return;

        lockedInputManager = RCCP_InputManager.Instance;
        if (lockedInputManager == null)
            return;

        previousInputManagerInputs = lockedInputManager.inputs;
        previousInputManagerOverride = lockedInputManager.overrideInputs;
        previousSuppressGameplayActionEvents = lockedInputManager.suppressGameplayActionEvents;

        lockedInputManager.suppressGameplayActionEvents = true;
        lockedInputManager.OverrideInputs(new RCCP_Inputs());
        inputManagerStateLocked = true;
    }

    private void RestoreRCCPInputManager()
    {
        if (!inputManagerStateLocked)
            return;

        if (lockedInputManager != null)
        {
            lockedInputManager.suppressGameplayActionEvents = previousSuppressGameplayActionEvents;

            if (previousInputManagerOverride && previousInputManagerInputs != null)
                lockedInputManager.OverrideInputs(previousInputManagerInputs);
            else
                lockedInputManager.DisableOverrideInputs();
        }

        lockedInputManager = null;
        previousInputManagerInputs = null;
        inputManagerStateLocked = false;
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
