//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;

/// <summary>
/// Receives animation curves from Timeline / AnimationClips and applies them back to the vehicle.
/// This lets a recorded RCCP clip drive the same gameplay-facing values through Unity Timeline.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Other Addons/RCCP Timeline Playback")]
public class RCCP_TimelinePlayback : RCCP_Component {

    [Header("Recorded Inputs")]
    [Range(0f, 1f)] public float throttleInput = 0f;
    [Range(0f, 1f)] public float brakeInput = 0f;
    [Range(-1f, 1f)] public float steerInput = 0f;
    [Range(0f, 1f)] public float handbrakeInput = 0f;
    [Range(0f, 1f)] public float clutchInput = 0f;
    [Range(0f, 1f)] public float nosInput = 0f;

    [Header("Recorded Vehicle State")]
    public float direction = 1f;
    public float currentGear = 0f;
    public float gearInput = 1f;
    public float gearState = 0f;
    public float neutralGear = 0f;

    [Header("Recorded Lights")]
    public float lowBeamHeadLightsOn = 0f;
    public float highBeamHeadLightsOn = 0f;
    public float indicatorsLeft = 0f;
    public float indicatorsRight = 0f;
    public float indicatorsAll = 0f;

    [Header("Recorded Rigidbody State")]
    public Vector3 linearVelocity = Vector3.zero;
    public Vector3 angularVelocity = Vector3.zero;

    private readonly RCCP_Inputs animatedInputs = new RCCP_Inputs();
    private int pendingPhysicsFrames = 0;

    private void OnDidApplyAnimationProperties() {

        ApplyAnimatedState();

        if (Application.isPlaying)
            pendingPhysicsFrames = 2;

    }

    private void FixedUpdate() {

        if (!Application.isPlaying || pendingPhysicsFrames <= 0)
            return;

        ApplyPhysicsState();
        pendingPhysicsFrames--;

    }

    private void OnDisable() {

        if (!Application.isPlaying || !CarController)
            return;

        if (CarController.Inputs)
            CarController.Inputs.DisableOverrideInputs();

        if (CarController.Inputs)
            CarController.Inputs.overrideExternalInputs = false;

        if (CarController.Gearbox)
            CarController.Gearbox.DisableOverride();

    }

    public void ApplyAnimatedState() {

        if (!CarController)
            return;

        SyncControllerMirrors();

        if (Application.isPlaying) {

            ApplyInputs();
            ApplyGearbox();
            ApplyPhysicsState();

        }

        ApplyLights();

    }

    private void ApplyInputs() {

        if (!CarController.Inputs)
            return;

        animatedInputs.throttleInput = throttleInput;
        animatedInputs.brakeInput = brakeInput;
        animatedInputs.steerInput = steerInput;
        animatedInputs.handbrakeInput = handbrakeInput;
        animatedInputs.clutchInput = clutchInput;
        animatedInputs.nosInput = nosInput;
        animatedInputs.mouseInput = Vector2.zero;

        CarController.Inputs.OverrideInputs(animatedInputs);
        CarController.Inputs.overrideExternalInputs = true;

    }

    private void ApplyGearbox() {

        if (!CarController.Gearbox)
            return;

        int targetGear = Mathf.RoundToInt(currentGear);
        int targetState = Mathf.Clamp(
            Mathf.RoundToInt(gearState),
            0,
            System.Enum.GetValues(typeof(RCCP_Gearbox.CurrentGearState.GearState)).Length - 1
        );

        CarController.Gearbox.OverrideGear(
            targetGear,
            gearInput,
            (RCCP_Gearbox.CurrentGearState.GearState)targetState
        );

    }

    private void ApplyLights() {

        if (!CarController.Lights)
            return;

        CarController.Lights.lowBeamHeadlights = lowBeamHeadLightsOn > .5f;
        CarController.Lights.highBeamHeadlights = highBeamHeadLightsOn > .5f;
        CarController.Lights.indicatorsLeft = indicatorsLeft > .5f;
        CarController.Lights.indicatorsRight = indicatorsRight > .5f;
        CarController.Lights.indicatorsAll = indicatorsAll > .5f;

    }

    private void ApplyPhysicsState() {

        if (!CarController || !CarController.Rigid)
            return;

        CarController.Rigid.linearVelocity = linearVelocity;
        CarController.Rigid.angularVelocity = angularVelocity;

    }

    private void SyncControllerMirrors() {

        CarController.throttleInput_V = throttleInput;
        CarController.brakeInput_V = brakeInput;
        CarController.steerInput_V = steerInput;
        CarController.handbrakeInput_V = handbrakeInput;
        CarController.clutchInput_V = clutchInput;
        CarController.nosInput_V = nosInput;
        CarController.direction = Mathf.RoundToInt(direction);
        CarController.currentGear = Mathf.RoundToInt(currentGear);
        CarController.NGearNow = neutralGear > .5f;
        CarController.lowBeamLights = lowBeamHeadLightsOn > .5f;
        CarController.highBeamLights = highBeamHeadLightsOn > .5f;
        CarController.indicatorsLeftLights = indicatorsLeft > .5f;
        CarController.indicatorsRightLights = indicatorsRight > .5f;
        CarController.indicatorsAllLights = indicatorsAll > .5f;

    }

}
