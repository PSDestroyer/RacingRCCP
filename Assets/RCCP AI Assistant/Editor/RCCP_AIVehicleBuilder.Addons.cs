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
/// Partial class containing NOS, fuel tank, and limiter methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region NOS Settings

    private static void ApplyNosSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.NosConfig config, RCCP_AIConfig.NosConfig configAllTrue = null) {
        RCCP_OtherAddons otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Use GetComponentInChildren instead of runtime property (otherAddons.Nos)
        RCCP_Nos nos = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_Nos>(true) : null;

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(nos, "NOS");
            return;
        }

        // Handle DISABLE request - explicitly disabled in config
        // Check using configAllTrue: if enabled is false AND allTrue.enabled is false (user set false)
        bool explicitDisable = !config.enabled && (configAllTrue != null && !configAllTrue.enabled);

        // Also check if enabled is false AND no meaningful values set (implicit disable request)
        // Only if configAllTrue is missing (legacy path)
        bool implicitDisable = configAllTrue == null && !config.enabled && !IsRestoreMode &&
                               config.torqueMultiplier <= 0 && config.durationTime <= 0;

        // In restore mode with enabled=false: disable if exists, skip creation if absent
        bool restoreDisable = IsRestoreMode && !config.enabled;

        if ((explicitDisable || implicitDisable || restoreDisable) && nos != null) {
            Undo.RecordObject(nos, "RCCP AI Disable NOS");
            nos.enabled = false;
            EditorUtility.SetDirty(nos);
            if (VerboseLogging) Debug.Log("[RCCP AI] NOS component disabled");
            return;
        }

        // If restore wants disabled but component doesn't exist, nothing to do
        if (restoreDisable && nos == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI Restore] NOS was absent at capture time, skipping");
            return;
        }

        // From here on, we're enabling or configuring NOS

        // Create OtherAddons if it doesn't exist
        if (otherAddons == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create OtherAddons for NOS: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating OtherAddons component for NOS...");
            RCCP_CreateNewVehicle.AddOtherAddons(carController);
            otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);
        }

        if (otherAddons == null) {
            Debug.LogWarning("[RCCP AI] Failed to create OtherAddons component");
            return;
        }

        // Re-check for NOS after potentially creating OtherAddons
        nos = otherAddons.GetComponentInChildren<RCCP_Nos>(true);

        // Create NOS component if it doesn't exist (config was sent, so user wants it)
        // RCCP expects addon components on child GameObjects, not on OtherAddons directly
        if (nos == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create NOS: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating NOS component...");

            // Create child GameObject like RCCP_OtherAddonsEditor does
            GameObject nosObject = new GameObject("RCCP_NOS");
            Undo.RegisterCreatedObjectUndo(nosObject, "RCCP AI Create NOS");
            nosObject.transform.SetParent(otherAddons.transform, false);
            nosObject.transform.SetSiblingIndex(0);
            nos = nosObject.AddComponent<RCCP_Nos>();

            if (nos == null) {
                Debug.LogWarning("[RCCP AI] Failed to create NOS component");
                return;
            }
        }

        Undo.RecordObject(nos, "RCCP AI NOS");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.nos;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally
            nos.enabled = config.enabled;
            nos.torqueMultiplier = config.torqueMultiplier;
            nos.durationTime = config.durationTime;
            nos.regenerateTime = config.regenerateTime;
            nos.regenerateRate = config.regenerateRate;
        } else {
            // Enable the component since user is configuring it
            // Only if explicit disable wasn't requested (handled above)
            if (!nos.enabled) {
                nos.enabled = true;
                if (VerboseLogging) Debug.Log("[RCCP AI] NOS component was disabled, enabling it for configuration");
            }

            // Helper to detect if a float field was actually present in the JSON
            bool HasExplicitFloat(float value, float? allTrueValue) {
                if (configAllTrue != null)
                    return allTrueValue.HasValue && !float.IsNaN(allTrueValue.Value);
                return !Mathf.Approximately(value, 0f);
            }

            // ONLY change values that are explicitly specified in JSON
            if (HasExplicitFloat(config.torqueMultiplier, configAllTrue?.torqueMultiplier))
                nos.torqueMultiplier = config.torqueMultiplier;
            if (HasExplicitFloat(config.durationTime, configAllTrue?.durationTime))
                nos.durationTime = config.durationTime;
            if (HasExplicitFloat(config.regenerateTime, configAllTrue?.regenerateTime))
                nos.regenerateTime = config.regenerateTime;
            if (HasExplicitFloat(config.regenerateRate, configAllTrue?.regenerateRate))
                nos.regenerateRate = config.regenerateRate;
        }

        EditorUtility.SetDirty(nos);
        if (VerboseLogging) Debug.Log($"[RCCP AI] NOS updated: enabled={nos.enabled}, multiplier={nos.torqueMultiplier}");
    }

    #endregion

    #region FuelTank Settings

    private static void ApplyFuelTankSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.FuelTankConfig config, RCCP_AIConfig.FuelTankConfig configAllTrue = null) {
        RCCP_OtherAddons otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Use GetComponentInChildren instead of runtime property (otherAddons.FuelTank)
        RCCP_FuelTank fuelTank = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_FuelTank>(true) : null;

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(fuelTank, "FuelTank");
            return;
        }

        // Handle DISABLE request
        bool explicitDisable = !config.enabled && (configAllTrue != null && !configAllTrue.enabled);
        bool implicitDisable = configAllTrue == null && !config.enabled && !IsRestoreMode && config.fuelTankCapacity <= 0;

        // In restore mode with enabled=false: disable if exists, skip creation if absent
        bool restoreDisable = IsRestoreMode && !config.enabled;

        if ((explicitDisable || implicitDisable || restoreDisable) && fuelTank != null) {
            Undo.RecordObject(fuelTank, "RCCP AI Disable FuelTank");
            fuelTank.enabled = false;
            EditorUtility.SetDirty(fuelTank);
            if (VerboseLogging) Debug.Log("[RCCP AI] FuelTank component disabled");
            return;
        }

        // If restore wants disabled but component doesn't exist, nothing to do
        if (restoreDisable && fuelTank == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI Restore] FuelTank was absent at capture time, skipping");
            return;
        }

        // From here on, we're enabling or configuring FuelTank

        // Create OtherAddons if it doesn't exist
        if (otherAddons == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create OtherAddons for FuelTank: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating OtherAddons component for FuelTank...");
            RCCP_CreateNewVehicle.AddOtherAddons(carController);
            otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);
        }

        if (otherAddons == null) {
            Debug.LogWarning("[RCCP AI] Failed to create OtherAddons component");
            return;
        }

        // Re-check for FuelTank after potentially creating OtherAddons
        fuelTank = otherAddons.GetComponentInChildren<RCCP_FuelTank>(true);

        // Create FuelTank component if it doesn't exist (config was sent, so user wants it)
        // RCCP expects addon components on child GameObjects, not on OtherAddons directly
        if (fuelTank == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create FuelTank: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating FuelTank component...");

            // Create child GameObject like RCCP_OtherAddonsEditor does
            GameObject fuelTankObject = new GameObject("RCCP_FuelTank");
            Undo.RegisterCreatedObjectUndo(fuelTankObject, "RCCP AI Create FuelTank");
            fuelTankObject.transform.SetParent(otherAddons.transform, false);
            fuelTankObject.transform.SetSiblingIndex(0);
            fuelTank = fuelTankObject.AddComponent<RCCP_FuelTank>();

            if (fuelTank == null) {
                Debug.LogWarning("[RCCP AI] Failed to create FuelTank component");
                return;
            }
        }

        Undo.RecordObject(fuelTank, "RCCP AI FuelTank");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.fuelTank;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally
            fuelTank.enabled = config.enabled;
            fuelTank.fuelTankCapacity = config.fuelTankCapacity;
            fuelTank.fuelTankFillAmount = Mathf.Clamp01(config.fuelTankFillAmount);
            fuelTank.stopEngine = config.stopEngineWhenEmpty;
            fuelTank.baseLitersPerHour = config.baseLitersPerHour;
            fuelTank.maxLitersPerHour = config.maxLitersPerHour;
        } else {
            // Enable the component since user is configuring it
            if (!fuelTank.enabled) {
                fuelTank.enabled = true;
                if (VerboseLogging) Debug.Log("[RCCP AI] FuelTank component was disabled, enabling it for configuration");
            }

            // Helper to detect if a float field was actually present in the JSON
            bool HasExplicitFloat(float value, float? allTrueValue) {
                if (configAllTrue != null)
                    return allTrueValue.HasValue && !float.IsNaN(allTrueValue.Value);
                return !Mathf.Approximately(value, 0f);
            }

            // ONLY change values that are explicitly specified in JSON
            if (HasExplicitFloat(config.fuelTankCapacity, configAllTrue?.fuelTankCapacity))
                fuelTank.fuelTankCapacity = config.fuelTankCapacity;
            if (HasExplicitFloat(config.fuelTankFillAmount, configAllTrue?.fuelTankFillAmount))
                fuelTank.fuelTankFillAmount = Mathf.Clamp01(config.fuelTankFillAmount);

            // Boolean handling for stopEngine
            if (config.stopEngineWhenEmpty) fuelTank.stopEngine = true;
            else if (configAllTrue != null && !configAllTrue.stopEngineWhenEmpty) fuelTank.stopEngine = false;

            if (HasExplicitFloat(config.baseLitersPerHour, configAllTrue?.baseLitersPerHour))
                fuelTank.baseLitersPerHour = config.baseLitersPerHour;
            if (HasExplicitFloat(config.maxLitersPerHour, configAllTrue?.maxLitersPerHour))
                fuelTank.maxLitersPerHour = config.maxLitersPerHour;
        }

        EditorUtility.SetDirty(fuelTank);
        if (VerboseLogging) Debug.Log($"[RCCP AI] FuelTank updated: enabled={fuelTank.enabled}, capacity={fuelTank.fuelTankCapacity}L");
    }

    #endregion

    #region Input Settings

    private static void ApplyInputSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.InputConfig config, RCCP_AIConfig.InputConfig configAllTrue = null) {
        RCCP_Input input = carController.GetComponentInChildren<RCCP_Input>(true);

        // Create Input component if it doesn't exist (should always exist, but just in case)
        if (input == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Input component...");
            RCCP_CreateNewVehicle.AddInputs(carController);
            input = carController.GetComponentInChildren<RCCP_Input>(true);
        }

        if (input == null) {
            Debug.LogWarning("[RCCP AI] Failed to create Input component");
            return;
        }

        Undo.RecordObject(input, "RCCP AI Input");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.input;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally
            input.counterSteerFactor = Mathf.Clamp01(config.counterSteerFactor);
            input.counterSteering = config.counterSteering;
            input.steeringLimiter = config.steeringLimiter;
            input.autoReverse = config.autoReverse;
            input.steeringDeadzone = Mathf.Clamp(config.steeringDeadzone, 0f, 0.2f);
        } else {
            // Helper to detect if a float field was actually present in the JSON
            bool HasExplicitFloat(float value, float? allTrueValue) {
                if (configAllTrue != null)
                    return allTrueValue.HasValue && !float.IsNaN(allTrueValue.Value);
                return !Mathf.Approximately(value, 0f);
            }

            // Partial update mode - ONLY change values that are explicitly specified in JSON
            if (HasExplicitFloat(config.counterSteerFactor, configAllTrue?.counterSteerFactor))
                input.counterSteerFactor = Mathf.Clamp01(config.counterSteerFactor);
            if (HasExplicitFloat(config.steeringDeadzone, configAllTrue?.steeringDeadzone))
                input.steeringDeadzone = Mathf.Clamp(config.steeringDeadzone, 0f, 0.2f);

            // Boolean handling
            void ApplyBool(ref bool field, bool value, bool allTrueValue) {
                if (value) field = true;
                else if (configAllTrue != null && !allTrueValue) field = false;
            }

            ApplyBool(ref input.counterSteering, config.counterSteering, configAllTrue?.counterSteering ?? true);
            ApplyBool(ref input.steeringLimiter, config.steeringLimiter, configAllTrue?.steeringLimiter ?? true);
            ApplyBool(ref input.autoReverse, config.autoReverse, configAllTrue?.autoReverse ?? true);
        }

        EditorUtility.SetDirty(input);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Input updated: counterSteerFactor={input.counterSteerFactor}, counterSteering={input.counterSteering}, steeringLimiter={input.steeringLimiter}");
    }

    #endregion

    #region Limiter Settings

    private static void ApplyLimiterSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.LimiterConfig config, RCCP_AIConfig.LimiterConfig configAllTrue = null) {
        RCCP_OtherAddons otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Use GetComponentInChildren instead of runtime property (otherAddons.Limiter)
        RCCP_Limiter limiter = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_Limiter>(true) : null;

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(limiter, "Limiter");
            return;
        }

        // Handle DISABLE request
        bool explicitDisable = !config.enabled && (configAllTrue != null && !configAllTrue.enabled);
        bool implicitDisable = configAllTrue == null && !config.enabled && !IsRestoreMode &&
                                (config.limitSpeedAtGear == null || config.limitSpeedAtGear.Length == 0);

        // In restore mode with enabled=false: disable if exists, skip creation if absent
        bool restoreDisable = IsRestoreMode && !config.enabled;

        if ((explicitDisable || implicitDisable || restoreDisable) && limiter != null) {
            Undo.RecordObject(limiter, "RCCP AI Disable Limiter");
            limiter.enabled = false;
            EditorUtility.SetDirty(limiter);
            if (VerboseLogging) Debug.Log("[RCCP AI] Limiter component disabled");
            return;
        }

        // If restore wants disabled but component doesn't exist, nothing to do
        if (restoreDisable && limiter == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI Restore] Limiter was absent at capture time, skipping");
            return;
        }

        // From here on, we're enabling or configuring Limiter

        // Create OtherAddons if it doesn't exist
        if (otherAddons == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create OtherAddons for Limiter: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating OtherAddons component for Limiter...");
            RCCP_CreateNewVehicle.AddOtherAddons(carController);
            otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);
        }

        if (otherAddons == null) {
            Debug.LogWarning("[RCCP AI] Failed to create OtherAddons component");
            return;
        }

        // Re-check for Limiter after potentially creating OtherAddons
        limiter = otherAddons.GetComponentInChildren<RCCP_Limiter>(true);

        // Create Limiter component if it doesn't exist (config was sent, so user wants it)
        // RCCP expects addon components on child GameObjects, not on OtherAddons directly
        if (limiter == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create Limiter: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Limiter component...");

            // Create child GameObject like RCCP_OtherAddonsEditor does
            GameObject limiterObject = new GameObject("RCCP_Limiter");
            Undo.RegisterCreatedObjectUndo(limiterObject, "RCCP AI Create Limiter");
            limiterObject.transform.SetParent(otherAddons.transform, false);
            limiterObject.transform.SetSiblingIndex(0);
            limiter = limiterObject.AddComponent<RCCP_Limiter>();

            if (limiter == null) {
                Debug.LogWarning("[RCCP AI] Failed to create Limiter component");
                return;
            }
        }

        Undo.RecordObject(limiter, "RCCP AI Limiter");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.limiter;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally
            limiter.enabled = config.enabled;
            if (config.limitSpeedAtGear != null) {
                limiter.limitSpeedAtGear = config.limitSpeedAtGear;
            }
            limiter.applyDownhillForce = config.applyDownhillForce;
            limiter.downhillForceStrength = config.downhillForceStrength;
        } else {
            // Enable the component since user is configuring it
            if (!limiter.enabled) {
                limiter.enabled = true;
                if (VerboseLogging) Debug.Log("[RCCP AI] Limiter component was disabled, enabling it for configuration");
            }

            // ONLY change values that are explicitly specified
            if (config.limitSpeedAtGear != null && config.limitSpeedAtGear.Length > 0) {
                limiter.limitSpeedAtGear = config.limitSpeedAtGear;
            }
            
            // Boolean handling for applyDownhillForce
            if (config.applyDownhillForce) limiter.applyDownhillForce = true;
            else if (configAllTrue != null && !configAllTrue.applyDownhillForce) limiter.applyDownhillForce = false;

            if (config.downhillForceStrength > 0) limiter.downhillForceStrength = config.downhillForceStrength;
        }

        EditorUtility.SetDirty(limiter);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Limiter updated: enabled={limiter.enabled}");
    }

    #endregion

    #region Recorder Settings

    private static void ApplyRecorderSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.RecorderConfig config) {
        RCCP_OtherAddons otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);
        RCCP_Recorder recorder = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_Recorder>(true) : null;

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(recorder, "Recorder");
            return;
        }

        // Recorder has no configurable properties via AI - it's runtime-only (record/play)
        // If we reach here, the config was sent but remove wasn't true, so nothing to do
        if (VerboseLogging) Debug.Log("[RCCP AI] Recorder config received but no changes to apply (use remove: true to remove)");
    }

    #endregion

    #region TrailerAttacher Settings

    private static void ApplyTrailerAttacherSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.TrailerAttacherConfig config) {
        RCCP_OtherAddons otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);
        RCCP_TrailerAttacher trailerAttacher = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_TrailerAttacher>(true) : null;

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(trailerAttacher, "TrailerAttacher");
            return;
        }

        // TrailerAttacher has no configurable properties via AI - it's just a trigger for trailer attachment
        // If we reach here, the config was sent but remove wasn't true, so nothing to do
        if (VerboseLogging) Debug.Log("[RCCP AI] TrailerAttacher config received but no changes to apply (use remove: true to remove)");
    }

    #endregion

    #region Customizer Settings

    private static void ApplyCustomizerSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.CustomizerConfig config, RCCP_AIConfig.CustomizerConfig configAllTrue = null) {
        RCCP_Customizer customizer = carController.GetComponentInChildren<RCCP_Customizer>(true);

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(customizer, "Customizer");
            return;
        }

        // If customizer doesn't exist and we're not removing, nothing to configure
        if (customizer == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Customizer component not found on vehicle");
            return;
        }

        Undo.RecordObject(customizer, "RCCP AI Customizer");

        // Apply settings - these are all optional
        if (!string.IsNullOrEmpty(config.saveFileName)) {
            customizer.saveFileName = config.saveFileName;
        }

        // Boolean handling using configAllTrue pattern
        void ApplyBool(ref bool field, bool value, bool allTrueValue) {
            if (value) field = true;
            else if (configAllTrue != null && !allTrueValue) field = false;
        }

        ApplyBool(ref customizer.autoInitialize, config.autoInitialize, configAllTrue?.autoInitialize ?? true);
        ApplyBool(ref customizer.autoLoadLoadout, config.autoLoadLoadout, configAllTrue?.autoLoadLoadout ?? true);
        ApplyBool(ref customizer.autoSave, config.autoSave, configAllTrue?.autoSave ?? true);

        // Initialize method enum
        if (!string.IsNullOrEmpty(config.initializeMethod)) {
            switch (config.initializeMethod.ToLower()) {
                case "awake":
                    customizer.initializeMethod = RCCP_Customizer.InitializeMethod.Awake;
                    break;
                case "onenable":
                    customizer.initializeMethod = RCCP_Customizer.InitializeMethod.OnEnable;
                    break;
                case "start":
                    customizer.initializeMethod = RCCP_Customizer.InitializeMethod.Start;
                    break;
                case "delayedwithfixedupdate":
                case "delayed":
                    customizer.initializeMethod = RCCP_Customizer.InitializeMethod.DelayedWithFixedUpdate;
                    break;
            }
        }

        EditorUtility.SetDirty(customizer);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Customizer updated: saveFileName={customizer.saveFileName}, autoInitialize={customizer.autoInitialize}");
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
