//----------------------------------------------
//        Realistic Traffic Controller
//
// Copyright � 2014 - 2024 BoneCracker Games
// https://www.bonecrackergames.com
//
//----------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class.
/// </summary>
public class RTC_Core : MonoBehaviour {

    /// <summary>
    /// Car controller component.
    /// </summary>
    public RTC_CarController carController;

    private RTC_SceneManager sceneManager;

    /// <summary>
    /// RTC Scene Manager.
    /// </summary>
    public RTC_SceneManager RTCSceneManager {

        get {

            if (sceneManager == null)
                sceneManager = RTC_SceneManager.Instance;

            return sceneManager;

        }

    }

    /// <summary>
    /// Reset vehicle on enable / disable.
    /// </summary>
    public void ResetVehicle() {

        carController.throttleInput = 0f;
        carController.brakeInput = 0f;
        carController.steerInput = 0f;
        carController.clutchInput = 0f;
        carController.fuelInput = 0f;
        carController.idleInput = 0f;
        carController.throttleInputRaw = 0f;
        carController.brakeInputRaw = 0f;
        carController.steerInputRaw = 0f;
        carController.clutchInputRaw = 0f;

        carController.tractionWheelRPM2EngineRPM = 0f;
        carController.wheelRPM2Speed = 0f;
        carController.targetWheelSpeedForCurrentGear = 0f;
        carController.currentGear = 0;
        carController.gearShiftingNow = false;
        carController.lastTimeShifted = 0f;
        carController.desiredSpeed = 0f;
        carController.direction = 1;
        carController.currentSpeed = 0f;
        carController.waitingAtWaypoint = 0f;
        carController.stopNow = false;
        carController.interconnecting = false;
        carController.willTurnLeft = false;
        carController.willTurnRight = false;
        carController.crashed = false;
        carController.stoppedForReason = false;
        carController.stoppedTime = 0f;
        carController.passingObstacle = false;
        carController.overtakingTimer = 0f;
        carController.waitForHorn = 0f;
        carController.raycastHitDistance = 0f;
        carController.raycastedVehicle = null;
        carController.indicatorTimer = 0f;
        carController.m_disableAfterCrash = 0f;
        carController.m_checkUpsideDown = 0f;

        carController.Rigid.linearVelocity = Vector3.zero;
        carController.Rigid.angularVelocity = Vector3.zero;

        for (int i = 0; i < carController.wheels.Length; i++) {

            if (carController.wheels[i] != null && carController.wheels[i].wheelCollider != null) {

                carController.wheels[i].wheelCollider.motorTorque = 0f;
                carController.wheels[i].wheelCollider.steerAngle = 0f;
                carController.wheels[i].wheelCollider.brakeTorque = 0f;

            }

        }

        if (carController.engineRunning) {

            carController.wantedEngineRPMRaw = carController.minEngineRPM;
            carController.currentEngineRPM = carController.minEngineRPM;

        } else {

            carController.wantedEngineRPMRaw = 0f;
            carController.currentEngineRPM = 0f;

        }

        //  Resetting audio.
        if (carController.engineSoundOnSource) {

            carController.engineSoundOnSource.volume = 0f;
            carController.engineSoundOnSource.pitch = 0f;

        }

        if (carController.engineSoundOffSource) {

            carController.engineSoundOffSource.volume = 0f;
            carController.engineSoundOffSource.pitch = 0f;

        }

    }

    /// <summary>
    /// Resets the vehicle on enable.
    /// </summary>
    public void ResetVehicleOnEnable() {

        ResetVehicle();

    }

    /// <summary>
    /// Resets the vehicle on disable.
    /// </summary>
    public void ResetVehicleOnDisable() {

        ResetVehicle();

    }

    /// <summary>
    /// Finds eligible gear depends on the speed.
    /// </summary>
    /// <returns></returns>
    public float[] FindTargetSpeed() {

        float[] targetSpeeds = new float[carController.gearRatios.Length];

        if (targetSpeeds.Length == 0)
            return targetSpeeds;

        float partition = carController.maximumSpeed / (float)carController.gearRatios.Length;

        //  Assigning target speeds.
        for (int i = targetSpeeds.Length - 1; i >= 0; i--)
            targetSpeeds[i] = partition * (i + 1) * carController.gearShiftThreshold;

        return targetSpeeds;

    }

    /// <summary>
    /// Finds eligible gear depends on the speed.
    /// </summary>
    /// <returns></returns>
    public int FindEligibleGear() {

        float[] targetSpeeds = FindTargetSpeed();
        int eligibleGear = 0;

        if (targetSpeeds.Length == 0)
            return eligibleGear;

        for (int i = 0; i < targetSpeeds.Length; i++) {

            if (carController.currentSpeed < targetSpeeds[i]) {

                eligibleGear = i;
                break;

            }

        }

        return eligibleGear;

    }

    /// <summary>
    /// Returns -1 when to the left, 1 to the right, and 0 for forward/backward
    /// </summary>
    /// <param name="fwd"></param>
    /// <param name="targetDir"></param>
    /// <param name="up"></param>
    /// <returns></returns>
    public float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {

        Vector3 perp = Vector3.Cross(fwd, targetDir);
        float dir = Vector3.Dot(perp, up);

        return dir;

    }

    [Obsolete("Use 'DestroyAllAudioSources' instead of ''DisableAllAudioSources")]
    public void DisableAllAudioSources() {

        DestroyAllAudioSources();

    }

    /// <summary>
    /// Destroys the engine audio sources.
    /// </summary>
    public void DestroyAllAudioSources() {

        if (carController.engineSoundOnSource)
            Destroy(carController.engineSoundOnSource.gameObject);

        if (carController.engineSoundOffSource)
            Destroy(carController.engineSoundOffSource.gameObject);

        carController.engineSoundOnSource = null;
        carController.engineSoundOffSource = null;

    }

}
