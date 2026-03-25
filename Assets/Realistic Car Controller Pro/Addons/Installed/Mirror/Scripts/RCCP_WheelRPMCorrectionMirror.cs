//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if RCCP_MIRROR && MIRROR
using UnityEngine;
using Mirror;

/// <summary>
/// Corrects wheel RPM on remote vehicles by comparing the synced (owner's) RPM with the local RPM
/// and applying corrective motor/brake torque to match them.
/// This script is automatically added to each wheel by RCCP_MirrorSync on remote vehicles only.
/// </summary>
[DefaultExecutionOrder(5)] // Runs AFTER RCCP_WheelCollider (order 0) applies its torque
public class RCCP_WheelRPMCorrectionMirror : MonoBehaviour {

    [Header("Correction Settings")]
    [Tooltip("Torque applied per unit of RPM error. Higher = more aggressive correction.")]
    public float correctionGain = 50f;

    [Tooltip("RPM errors below this threshold are ignored to prevent jitter.")]
    public float rpmTolerance = 5f;

    [Tooltip("Maximum correction torque that can be applied.")]
    public float maxCorrectionTorque = 500f;

    [Header("References (Set by RCCP_MirrorSync)")]
    [Tooltip("Index of this wheel in the vehicle's wheel array.")]
    public int wheelIndex;

    [Tooltip("Reference to the MirrorSync component on this vehicle.")]
    public RCCP_MirrorSync mirrorSync;

    /// <summary>
    /// Reference to the RCCP_WheelCollider on this GameObject.
    /// </summary>
    private RCCP_WheelCollider wheelCollider;

    /// <summary>
    /// Reference to the Unity WheelCollider component.
    /// </summary>
    private WheelCollider unityWheelCollider;

    private void Awake() {

        wheelCollider = GetComponent<RCCP_WheelCollider>();

        if (wheelCollider != null)
            unityWheelCollider = wheelCollider.WheelCollider;

    }

    private void FixedUpdate() {

        if (!NetworkClient.active)
            return;

        if (!NetworkClient.isConnected)
            return;

        // Safety check: Only run on remote vehicles
        if (mirrorSync == null || (mirrorSync && mirrorSync.IsMine))
            return;

        // Ensure we have valid references
        if (wheelCollider == null || unityWheelCollider == null)
            return;

        // Don't correct if wheel collider is disabled
        if (!unityWheelCollider.enabled)
            return;

        // Get target RPM from network sync
        float targetRPM = mirrorSync.GetTargetWheelRPM(wheelIndex);

        // Get current local RPM
        float currentRPM = unityWheelCollider.rpm;

        // Calculate error
        float error = targetRPM - currentRPM;

        // Skip if error is within tolerance
        if (Mathf.Abs(error) < rpmTolerance)
            return;

        // Apply correction
        ApplyCorrection(error);

    }

    /// <summary>
    /// Applies corrective torque to bring wheel RPM closer to target.
    /// Motor torque can be positive (forward) or negative (reverse).
    /// The sign of error naturally gives us the correct direction.
    /// </summary>
    /// <param name="error">Difference between target RPM and current RPM</param>
    private void ApplyCorrection(float error) {

        // Calculate correction torque (clamped to max)
        // Positive correction = forward spin, Negative correction = reverse spin
        float correction = Mathf.Clamp(error * correctionGain, -maxCorrectionTorque, maxCorrectionTorque);

        // Apply motor torque directly - works for both forward and reverse
        unityWheelCollider.motorTorque += correction;

    }

}
#endif
