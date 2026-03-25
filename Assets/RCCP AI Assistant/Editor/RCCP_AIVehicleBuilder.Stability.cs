//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Partial class containing stability and aerodynamics methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Stability Settings

    private static void ApplyStabilitySettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config) {
        RCCP_Stability stability = carController.GetComponentInChildren<RCCP_Stability>(true);
        if (stability == null) return;

        Undo.RecordObject(stability, "RCCP AI Stability");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.stability;

        if (config.stability != null) {
            stability.ABS = config.stability.ABS;
            stability.ESP = config.stability.ESP;
            stability.TCS = config.stability.TCS;
            stability.steeringHelper = config.stability.steeringHelper;
            stability.tractionHelper = config.stability.tractionHelper;
            stability.angularDragHelper = config.stability.angularDragHelper;
            stability.steerHelperStrength = config.stability.steerHelperStrength > 0 ? config.stability.steerHelperStrength : defaults?.steerHelperStrength ?? 0.5f;
            stability.tractionHelperStrength = config.stability.tractionHelperStrength > 0 ? config.stability.tractionHelperStrength : defaults?.tractionHelperStrength ?? 0.5f;
            stability.angularDragHelperStrength = config.stability.angularDragHelperStrength > 0 ? config.stability.angularDragHelperStrength : defaults?.angularDragHelperStrength ?? 0.5f;
        } else {
            // No config provided, apply all defaults
            stability.ABS = defaults?.ABS ?? true;
            stability.ESP = defaults?.ESP ?? true;
            stability.TCS = defaults?.TCS ?? true;
            stability.steeringHelper = defaults?.steeringHelper ?? true;
            stability.tractionHelper = defaults?.tractionHelper ?? true;
            stability.angularDragHelper = defaults?.angularDragHelper ?? false;
            stability.steerHelperStrength = defaults?.steerHelperStrength ?? 0.5f;
            stability.tractionHelperStrength = defaults?.tractionHelperStrength ?? 0.5f;
            stability.angularDragHelperStrength = defaults?.angularDragHelperStrength ?? 0.5f;
        }

        EditorUtility.SetDirty(stability);
    }

    private static void ApplyStabilitySettingsPartial(RCCP_CarController carController, RCCP_AIConfig.StabilityConfig config, RCCP_AIConfig.StabilityConfig configAllTrue = null) {
        RCCP_Stability stability = carController.GetComponentInChildren<RCCP_Stability>(true);

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        // Check for explicit true or default false (missing) vs user false
        // For 'remove', default is false. We only act if it's true.
        if (config.remove) {
            TryDestroyComponentGameObject(stability, "Stability");
            return;
        }

        // Create Stability component if it doesn't exist
        if (stability == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Stability component...");
            RCCP_CreateNewVehicle.AddStability(carController);
            stability = carController.GetComponentInChildren<RCCP_Stability>(true);
        }

        if (stability == null) {
            Debug.LogWarning("[RCCP AI] Failed to create Stability component");
            return;
        }

        Undo.RecordObject(stability, "RCCP AI Stability");

        // If user is configuring this component, they want it enabled
        if (!stability.enabled) {
            stability.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] Stability component was disabled, enabling it for configuration");
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.stability;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally (including false and zero)
            stability.ABS = config.ABS;
            stability.ESP = config.ESP;
            stability.TCS = config.TCS;
            stability.steeringHelper = config.steeringHelper;
            stability.tractionHelper = config.tractionHelper;
            stability.angularDragHelper = config.angularDragHelper;
            stability.steerHelperStrength = config.steerHelperStrength;
            stability.tractionHelperStrength = config.tractionHelperStrength;
            stability.angularDragHelperStrength = config.angularDragHelperStrength;
        } else {
            // Partial update mode - detect explicit values using configAllTrue
            
            // Helper to apply boolean if explicit
            void ApplyBool(ref bool field, bool value, bool allTrueValue) {
                // If value is true, user explicitly set true (JsonUtility default is false)
                if (value) {
                    field = true;
                }
                // If value is false, but allTrueValue is false, user explicitly set false
                // (JsonUtility overwrote our 'true' initialization with 'false' from JSON)
                else if (configAllTrue != null && !allTrueValue) {
                    field = false;
                }
                // Else: value is false AND allTrueValue is true -> Field was MISSING in JSON (not touched)
            }

            ApplyBool(ref stability.ABS, config.ABS, configAllTrue?.ABS ?? true);
            ApplyBool(ref stability.ESP, config.ESP, configAllTrue?.ESP ?? true);
            ApplyBool(ref stability.TCS, config.TCS, configAllTrue?.TCS ?? true);
            ApplyBool(ref stability.steeringHelper, config.steeringHelper, configAllTrue?.steeringHelper ?? true);
            ApplyBool(ref stability.tractionHelper, config.tractionHelper, configAllTrue?.tractionHelper ?? true);
            ApplyBool(ref stability.angularDragHelper, config.angularDragHelper, configAllTrue?.angularDragHelper ?? true);

            // Helper strengths - ONLY change when explicitly specified
            if (config.steerHelperStrength > 0) stability.steerHelperStrength = config.steerHelperStrength;
            if (config.tractionHelperStrength > 0) stability.tractionHelperStrength = config.tractionHelperStrength;
            if (config.angularDragHelperStrength > 0) stability.angularDragHelperStrength = config.angularDragHelperStrength;
        }

        EditorUtility.SetDirty(stability);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Stability updated: ABS={stability.ABS}, ESP={stability.ESP}, TCS={stability.TCS}");
    }

    #endregion

    #region Aerodynamics Settings

    private static void ApplyAeroDynamicsPartial(RCCP_CarController carController, RCCP_AIConfig.AeroDynamicsConfig config) {
        RCCP_AeroDynamics aero = carController.GetComponentInChildren<RCCP_AeroDynamics>(true);

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(aero, "AeroDynamics");
            return;
        }

        // Create AeroDynamics component if it doesn't exist
        if (aero == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating AeroDynamics component...");
            RCCP_CreateNewVehicle.AddAero(carController);
            aero = carController.GetComponentInChildren<RCCP_AeroDynamics>(true);
        }

        if (aero == null) {
            Debug.LogWarning("[RCCP AI] Failed to create AeroDynamics component");
            return;
        }

        Undo.RecordObject(aero, "RCCP AI Aerodynamics");

        // If user is configuring this component, they want it enabled
        if (!aero.enabled) {
            aero.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] AeroDynamics component was disabled, enabling it for configuration");
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.aeroDynamics;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally
            aero.downForce = config.downForce;
            aero.airResistance = Mathf.Clamp(config.airResistance, 0f, 100f);
            aero.wheelResistance = Mathf.Clamp(config.wheelResistance, 0f, 100f);
        } else {
            // Partial update mode - ONLY change when explicitly specified
            if (config.downForce > 0) aero.downForce = config.downForce;
            if (config.airResistance > 0) aero.airResistance = Mathf.Clamp(config.airResistance, 0f, 100f);
            if (config.wheelResistance > 0) aero.wheelResistance = Mathf.Clamp(config.wheelResistance, 0f, 100f);
        }

        EditorUtility.SetDirty(aero);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Aerodynamics updated: downForce={aero.downForce}, airResistance={aero.airResistance}, wheelResistance={aero.wheelResistance}");
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
