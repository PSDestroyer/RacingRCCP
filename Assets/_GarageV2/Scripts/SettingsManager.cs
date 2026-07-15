using System;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public Slider sfx;
    public Slider vehicle;
    public Slider music;
    [SerializeField] private bool managePreviewVehicleState = true;
    [SerializeField] private bool handleSelectedButtonSubmit = true;

    private RCCP_CarController lockedVehicle;
    private bool vehicleStateLocked;
    private bool previousCanControl;
    private bool previousExternalControl;
    private bool previousRigidbodyKinematic;
    private int lastHandledSubmitFrame = -1;

    private void Start()
    {
        RefreshUIFromSave();
    }

    private void Update()
    {
        if (handleSelectedButtonSubmit && WasSubmitPressed())
            PressSelectedControl();
    }

    private void OnEnable()
    {
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

    private void PressSelectedControl()
    {
        if (EventSystem.current == null || Time.frameCount == lastHandledSubmitFrame)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(transform))
            return;

        Selectable selectable = selected.GetComponent<Selectable>() ?? selected.GetComponentInParent<Selectable>();
        if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable())
            return;

        lastHandledSubmitFrame = Time.frameCount;

        RCCP_UI_DashboardButton dashboardButton = selectable.GetComponent<RCCP_UI_DashboardButton>();
        if (dashboardButton != null && dashboardButton.isActiveAndEnabled)
        {
            dashboardButton.OnSubmit(new BaseEventData(EventSystem.current));
            return;
        }

        ExecuteEvents.Execute(selectable.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            pointerPress = selectable.gameObject,
            rawPointerPress = selectable.gameObject
        };

        ExecuteEvents.Execute(selectable.gameObject, pointerEventData, ExecuteEvents.pointerClickHandler);
    }

    private static bool WasSubmitPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;
        }

        return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }
}
