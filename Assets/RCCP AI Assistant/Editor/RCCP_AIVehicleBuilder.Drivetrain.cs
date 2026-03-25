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
/// Partial class containing engine, gearbox, and clutch methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Engine Settings

    private static void ApplyEngineSettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config) {
        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        if (engine == null) return;

        Undo.RecordObject(engine, "RCCP AI Engine");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.engine;

        if (config.engine != null) {
            engine.maximumTorqueAsNM = config.engine.maximumTorqueAsNM > 0 ? config.engine.maximumTorqueAsNM : defaults?.maximumTorqueAsNM ?? 200f;
            engine.minEngineRPM = config.engine.minEngineRPM > 0 ? config.engine.minEngineRPM : defaults?.minEngineRPM ?? 750f;
            engine.maxEngineRPM = config.engine.maxEngineRPM > 0 ? config.engine.maxEngineRPM : defaults?.maxEngineRPM ?? 7000f;
            engine.maximumSpeed = config.engine.maximumSpeed > 0 ? config.engine.maximumSpeed : defaults?.maximumSpeed ?? 200f;
            engine.engineInertia = config.engine.engineInertia > 0 ? config.engine.engineInertia : defaults?.engineInertia ?? 0.15f;
            engine.engineFriction = config.engine.engineFriction > 0 ? config.engine.engineFriction : defaults?.engineFriction ?? 0.2f;
            engine.turboCharged = config.engine.turboCharged;
            engine.maxTurboChargePsi = config.engine.maxTurboChargePsi > 0 ? config.engine.maxTurboChargePsi : defaults?.maxTurboChargePsi ?? 6f;
            engine.turboChargerCoEfficient = config.engine.turboChargerCoEfficient > 0 ? config.engine.turboChargerCoEfficient : defaults?.turboChargerCoEfficient ?? 1.5f;
        } else {
            // No config provided, apply all defaults
            engine.maximumTorqueAsNM = defaults?.maximumTorqueAsNM ?? 200f;
            engine.minEngineRPM = defaults?.minEngineRPM ?? 750f;
            engine.maxEngineRPM = defaults?.maxEngineRPM ?? 7000f;
            engine.maximumSpeed = defaults?.maximumSpeed ?? 200f;
            engine.engineInertia = defaults?.engineInertia ?? 0.15f;
            engine.engineFriction = defaults?.engineFriction ?? 0.2f;
            engine.turboCharged = defaults?.turboCharged ?? false;
            engine.maxTurboChargePsi = defaults?.maxTurboChargePsi ?? 6f;
            engine.turboChargerCoEfficient = defaults?.turboChargerCoEfficient ?? 1.5f;
        }

        // Update maximum speed calculations (recalculates final drive ratios)
        engine.UpdateMaximumSpeed();

        EditorUtility.SetDirty(engine);
    }

    private static void ApplyEngineSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.EngineConfig config, RCCP_AIConfig.EngineConfig configAllTrue = null) {
        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);

        // Create Engine component if it doesn't exist (should always exist, but just in case)
        if (engine == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Engine component...");
            RCCP_CreateNewVehicle.AddEngine(carController);
            engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        }

        if (engine == null) {
            Debug.LogWarning("[RCCP AI] Failed to create Engine component");
            return;
        }

        Undo.RecordObject(engine, "RCCP AI Engine");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.engine;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally
            engine.maximumTorqueAsNM = config.maximumTorqueAsNM;
            engine.minEngineRPM = config.minEngineRPM;
            engine.maxEngineRPM = config.maxEngineRPM;
            engine.maximumSpeed = config.maximumSpeed;
            engine.engineInertia = config.engineInertia;
            engine.engineFriction = config.engineFriction;
            engine.turboCharged = config.turboCharged;
            engine.maxTurboChargePsi = config.maxTurboChargePsi;
            engine.turboChargerCoEfficient = config.turboChargerCoEfficient;
        } else {
            // Partial update mode - ONLY change values that are explicitly specified
            // Do NOT force defaults - leave current values untouched if not specified
            if (config.maximumTorqueAsNM > 0) engine.maximumTorqueAsNM = config.maximumTorqueAsNM;
            if (config.minEngineRPM > 0) engine.minEngineRPM = config.minEngineRPM;
            if (config.maxEngineRPM > 0) engine.maxEngineRPM = config.maxEngineRPM;
            if (config.maximumSpeed > 0) engine.maximumSpeed = config.maximumSpeed;
            if (config.engineInertia > 0) engine.engineInertia = config.engineInertia;
            if (config.engineFriction > 0) engine.engineFriction = config.engineFriction;

            // Turbo settings - apply boolean with explicit detection
            // If value is true, apply true.
            // If value is false AND allTrueValue is false, apply false (user explicitly set false).
            // Else (value false, allTrueValue true) -> Missing in JSON, ignore.
            if (config.turboCharged) {
                engine.turboCharged = true;
            } else if (configAllTrue != null && !configAllTrue.turboCharged) {
                engine.turboCharged = false;
            }

            // Only apply turbo params if turbo is enabled (or just enabled)
            if (engine.turboCharged) {
                if (config.maxTurboChargePsi > 0) engine.maxTurboChargePsi = config.maxTurboChargePsi;
                if (config.turboChargerCoEfficient > 0) engine.turboChargerCoEfficient = config.turboChargerCoEfficient;
            }
        }

        // Update maximum speed calculations (recalculates final drive ratios)
        engine.UpdateMaximumSpeed();

        EditorUtility.SetDirty(engine);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Engine updated: torque={engine.maximumTorqueAsNM}Nm, maxRPM={engine.maxEngineRPM}, turbo={engine.turboCharged}");
    }

    #endregion

    #region Gearbox Settings

    private static void ApplyGearboxSettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config) {
        RCCP_Gearbox gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        if (gearbox == null) return;

        Undo.RecordObject(gearbox, "RCCP AI Gearbox");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.gearbox;

        if (config.gearbox != null) {
            // Apply transmission type from config (default to Automatic if not specified)
            if (!string.IsNullOrEmpty(config.gearbox.transmissionType)) {
                switch (config.gearbox.transmissionType.ToLower()) {
                    case "manual":
                        gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Manual;
                        break;
                    case "semiautomatic":
                    case "semi-automatic":
                        gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Automatic_DNRP;
                        break;
                    default:
                        gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Automatic;
                        break;
                }
            } else {
                // Use default transmission type
                gearbox.transmissionType = ParseTransmissionType(defaults?.transmissionType ?? "Automatic");
            }

            // Gear ratios
            if (config.gearbox.gearRatios != null && config.gearbox.gearRatios.Length > 0) {
                gearbox.gearRatios = config.gearbox.gearRatios;
            } else if (defaults?.gearRatios != null && defaults.gearRatios.Length > 0) {
                gearbox.gearRatios = defaults.gearRatios;
            }

            gearbox.shiftingTime = config.gearbox.shiftingTime > 0 ? config.gearbox.shiftingTime : defaults?.shiftingTime ?? 0.2f;
            gearbox.shiftThreshold = config.gearbox.shiftThreshold > 0 ? config.gearbox.shiftThreshold : defaults?.shiftThreshold ?? 0.85f;

            // Apply shift RPM values if provided, otherwise use defaults
            gearbox.shiftUpRPM = config.gearbox.shiftUpRPM > 0 ? config.gearbox.shiftUpRPM : defaults?.shiftUpRPM ?? 6500f;
            gearbox.shiftDownRPM = config.gearbox.shiftDownRPM > 0 ? config.gearbox.shiftDownRPM : defaults?.shiftDownRPM ?? 3500f;
        } else {
            // No config provided, apply all defaults
            gearbox.transmissionType = ParseTransmissionType(defaults?.transmissionType ?? "Automatic");
            if (defaults?.gearRatios != null && defaults.gearRatios.Length > 0) {
                gearbox.gearRatios = defaults.gearRatios;
            }
            gearbox.shiftingTime = defaults?.shiftingTime ?? 0.2f;
            gearbox.shiftThreshold = defaults?.shiftThreshold ?? 0.85f;
            gearbox.shiftUpRPM = defaults?.shiftUpRPM ?? 6500f;
            gearbox.shiftDownRPM = defaults?.shiftDownRPM ?? 3500f;
        }

        // Validate and clamp shift RPM values to engine RPM range
        ValidateGearboxShiftRPM(carController, gearbox);

        EditorUtility.SetDirty(gearbox);
    }

    private static RCCP_Gearbox.TransmissionType ParseTransmissionType(string type) {
        if (string.IsNullOrEmpty(type)) return RCCP_Gearbox.TransmissionType.Automatic;
        switch (type.ToLower()) {
            case "manual": return RCCP_Gearbox.TransmissionType.Manual;
            case "semiautomatic":
            case "semi-automatic":
            case "automatic_dnrp": return RCCP_Gearbox.TransmissionType.Automatic_DNRP;
            default: return RCCP_Gearbox.TransmissionType.Automatic;
        }
    }

    /// <summary>
    /// Validates and clamps gearbox shift RPM values to be within the engine's RPM range.
    /// Ensures shiftUpRPM doesn't exceed maxEngineRPM and shiftDownRPM isn't below minEngineRPM.
    /// Also ensures shiftDownRPM is less than shiftUpRPM.
    /// </summary>
    private static void ValidateGearboxShiftRPM(RCCP_CarController carController, RCCP_Gearbox gearbox) {
        if (carController == null || gearbox == null) return;

        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        if (engine == null) return;

        float minRPM = engine.minEngineRPM;
        float maxRPM = engine.maxEngineRPM;

        // Store original values for logging
        float originalShiftUp = gearbox.shiftUpRPM;
        float originalShiftDown = gearbox.shiftDownRPM;

        const float maxUpMargin = 200f;
        const float minDownMargin = 500f;
        const float minGap = 1000f;

        float minShiftDown = minRPM + minDownMargin;
        float maxShiftUp = maxRPM - maxUpMargin;

        // Ensure maxShiftUp is not below minRPM (protect against extremely low max RPM)
        if (maxShiftUp < minRPM) {
            maxShiftUp = minRPM;
        }

        // If there isn't enough RPM range to honor margins/gap, clamp to bounds safely
        if (maxShiftUp < minShiftDown) {
            gearbox.shiftUpRPM = maxShiftUp;
            gearbox.shiftDownRPM = Mathf.Min(minShiftDown, gearbox.shiftUpRPM);
        } else {
            // Clamp within engine range + margins
            gearbox.shiftUpRPM = Mathf.Clamp(gearbox.shiftUpRPM, minShiftDown, maxShiftUp);
            gearbox.shiftDownRPM = Mathf.Clamp(gearbox.shiftDownRPM, minShiftDown, maxShiftUp);

            // Ensure shiftDownRPM is less than shiftUpRPM with a minimum gap when possible
            if (gearbox.shiftUpRPM - gearbox.shiftDownRPM < minGap) {
                float desiredDown = gearbox.shiftUpRPM - minGap;
                if (desiredDown >= minShiftDown) {
                    gearbox.shiftDownRPM = desiredDown;
                } else {
                    float desiredUp = gearbox.shiftDownRPM + minGap;
                    if (desiredUp <= maxShiftUp) {
                        gearbox.shiftUpRPM = desiredUp;
                    } else {
                        // Not enough room for full gap; snap to bounds
                        gearbox.shiftDownRPM = minShiftDown;
                        gearbox.shiftUpRPM = maxShiftUp;
                    }
                }
            }
        }

        // Log if values were changed
        if (VerboseLogging && (originalShiftUp != gearbox.shiftUpRPM || originalShiftDown != gearbox.shiftDownRPM)) {
            Debug.Log($"[RCCP AI] Gearbox shift RPM adjusted to fit engine range ({minRPM}-{maxRPM}): " +
                      $"shiftUp {originalShiftUp} -> {gearbox.shiftUpRPM}, shiftDown {originalShiftDown} -> {gearbox.shiftDownRPM}");
        }
    }

    private static void ApplyGearboxSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.GearboxConfig config) {
        RCCP_Gearbox gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);

        // Create Gearbox component if it doesn't exist (should always exist, but just in case)
        if (gearbox == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Gearbox component...");
            RCCP_CreateNewVehicle.AddGearbox(carController);
            gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        }

        if (gearbox == null) {
            Debug.LogWarning("[RCCP AI] Failed to create Gearbox component");
            return;
        }

        Undo.RecordObject(gearbox, "RCCP AI Gearbox");

        // Transmission type
        if (!string.IsNullOrEmpty(config.transmissionType)) {
            switch (config.transmissionType.ToLower()) {
                case "manual":
                    gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Manual;
                    break;
                case "semiautomatic":
                case "semi-automatic":
                    gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Automatic_DNRP;
                    break;
                default:
                    gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Automatic;
                    break;
            }
        }

        // Gear ratios
        if (config.gearRatios != null && config.gearRatios.Length > 0) {
            gearbox.gearRatios = config.gearRatios;
        }

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally
            gearbox.shiftingTime = config.shiftingTime;
            gearbox.shiftThreshold = config.shiftThreshold;
            gearbox.shiftUpRPM = config.shiftUpRPM;
            gearbox.shiftDownRPM = config.shiftDownRPM;
        } else {
            // Partial update mode - ONLY change values that are explicitly specified
            // Do NOT force defaults - leave current values untouched if not specified
            if (config.shiftingTime > 0) gearbox.shiftingTime = config.shiftingTime;
            if (config.shiftThreshold > 0) gearbox.shiftThreshold = config.shiftThreshold;
            if (config.shiftUpRPM > 0) gearbox.shiftUpRPM = config.shiftUpRPM;
            if (config.shiftDownRPM > 0) gearbox.shiftDownRPM = config.shiftDownRPM;
        }

        // Validate and clamp shift RPM values to engine RPM range
        ValidateGearboxShiftRPM(carController, gearbox);

        EditorUtility.SetDirty(gearbox);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Gearbox updated: type={gearbox.transmissionType}, gears={gearbox.gearRatios?.Length}, shiftUp={gearbox.shiftUpRPM}, shiftDown={gearbox.shiftDownRPM}");
    }

    #endregion

    #region Clutch Settings

    private static void ApplyClutchSettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config) {
        RCCP_Clutch clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);
        if (clutch == null) return;

        Undo.RecordObject(clutch, "RCCP AI Clutch");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.clutch;

        if (config.clutch != null) {
            clutch.clutchInertia = config.clutch.clutchInertia > 0 ? config.clutch.clutchInertia : defaults?.clutchInertia ?? 0.1f;
            clutch.engageRPM = config.clutch.engageRPM > 0 ? config.clutch.engageRPM : defaults?.engageRPM ?? 2000f;

            // IMPORTANT: Default to automatic clutch = true for vehicle CREATION
            // Only honor automaticClutch: false if gearbox.transmissionType is explicitly "Manual"
            // This prevents accidentally setting manual clutch when AI forgets to include automaticClutch: true
            if (config.clutch.automaticClutch) {
                clutch.automaticClutch = true;
            } else {
                // automaticClutch is false in config - check if manual transmission was explicitly requested
                bool isManualTransmission = config.gearbox != null &&
                    !string.IsNullOrEmpty(config.gearbox.transmissionType) &&
                    config.gearbox.transmissionType.Equals("Manual", System.StringComparison.OrdinalIgnoreCase);

                if (isManualTransmission) {
                    // User explicitly requested manual transmission, so honor manual clutch
                    clutch.automaticClutch = false;
                } else {
                    // Default to automatic clutch for automatic/semi-automatic transmissions
                    clutch.automaticClutch = true;
                }
            }

            // For these boolean fields, use value from config if true, otherwise use default
            clutch.pressClutchWhileShiftingGears = config.clutch.pressClutchWhileShiftingGears || (defaults?.pressClutchWhileShiftingGears ?? true);
            clutch.pressClutchWhileHandbraking = config.clutch.pressClutchWhileHandbraking || (defaults?.pressClutchWhileHandbraking ?? true);
        } else {
            // No config provided, apply all defaults (automatic clutch = true)
            clutch.clutchInertia = defaults?.clutchInertia ?? 0.1f;
            clutch.engageRPM = defaults?.engageRPM ?? 2000f;
            clutch.automaticClutch = defaults?.automaticClutch ?? true;
            clutch.pressClutchWhileShiftingGears = defaults?.pressClutchWhileShiftingGears ?? true;
            clutch.pressClutchWhileHandbraking = defaults?.pressClutchWhileHandbraking ?? true;
        }

        EditorUtility.SetDirty(clutch);
    }

    private static void ApplyClutchSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.ClutchConfig config, RCCP_AIConfig.ClutchConfig configAllTrue = null) {
        RCCP_Clutch clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);

        // Create Clutch component if it doesn't exist (should always exist, but just in case)
        if (clutch == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Clutch component...");
            RCCP_CreateNewVehicle.AddClutch(carController);
            clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);
        }

        if (clutch == null) {
            Debug.LogWarning("[RCCP AI] Failed to create Clutch component");
            return;
        }

        Undo.RecordObject(clutch, "RCCP AI Clutch");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.clutch;

        if (IsRestoreMode) {
            // In restore mode, apply ALL values unconditionally (including false booleans)
            clutch.clutchInertia = config.clutchInertia;
            clutch.engageRPM = config.engageRPM;
            clutch.automaticClutch = config.automaticClutch;
            clutch.pressClutchWhileShiftingGears = config.pressClutchWhileShiftingGears;
            clutch.pressClutchWhileHandbraking = config.pressClutchWhileHandbraking;
        } else {
            // Partial update mode - ONLY change values that are explicitly specified
            // Do NOT force defaults - leave current values untouched if not specified
            if (config.clutchInertia > 0) clutch.clutchInertia = config.clutchInertia;
            if (config.engageRPM > 0) clutch.engageRPM = config.engageRPM;
            
            // Helper for booleans
            void ApplyBool(ref bool field, bool value, bool allTrueValue) {
                if (value) field = true;
                else if (configAllTrue != null && !allTrueValue) field = false;
            }

            ApplyBool(ref clutch.automaticClutch, config.automaticClutch, configAllTrue?.automaticClutch ?? true);
            ApplyBool(ref clutch.pressClutchWhileShiftingGears, config.pressClutchWhileShiftingGears, configAllTrue?.pressClutchWhileShiftingGears ?? true);
            ApplyBool(ref clutch.pressClutchWhileHandbraking, config.pressClutchWhileHandbraking, configAllTrue?.pressClutchWhileHandbraking ?? true);
        }

        EditorUtility.SetDirty(clutch);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Clutch updated: engageRPM={clutch.engageRPM}, automatic={clutch.automaticClutch}");
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
