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
/// Partial class containing LOD, wheel blur, exhaust, body tilt, and particles methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Visual Effects Apply Methods

    /// <summary>
    /// Applies LOD (Level of Detail) settings to a vehicle.
    /// Controls which components are enabled based on camera distance.
    /// </summary>
    public static void ApplyLodSettings(RCCP_CarController carController, RCCP_AIConfig.LodConfig config) {
        if (carController == null || config == null) return;

        RCCP_Lod lod = carController.GetComponentInChildren<RCCP_Lod>(true);

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(lod, "LOD");
            return;
        }

        // Create LOD component if it doesn't exist
        if (lod == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create LOD: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating LOD component...");
            RCCP_CreateNewVehicle.AddLOD(carController);
            lod = carController.GetComponentInChildren<RCCP_Lod>(true);
        }

        if (lod == null) {
            Debug.LogWarning("[RCCP AI] Failed to create LOD component");
            return;
        }

        Undo.RecordObject(lod, "RCCP AI LOD Settings");

        // If user is configuring this component, they want it enabled
        if (!lod.enabled) {
            lod.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] LOD component was disabled, enabling it for configuration");
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.lod;

        // Apply LOD factor if specified
        if (config.lodFactor > 0) {
            lod.lodFactor = Mathf.Clamp(config.lodFactor, 0.1f, 1f);
        }

        // Apply force to first level (highest detail)
        if (config.ShouldModifyForceFirst) {
            lod.forceToFirstLevel = config.forceToFirstLevel == 1;
        }

        // Apply force to latest level (lowest detail)
        if (config.ShouldModifyForceLast) {
            lod.forceToLatestLevel = config.forceToLatestLevel == 1;
        }

        EditorUtility.SetDirty(lod);
        if (VerboseLogging) Debug.Log($"[RCCP AI] LOD settings applied: factor={lod.lodFactor}, forceFirst={lod.forceToFirstLevel}, forceLast={lod.forceToLatestLevel}");
    }

    /// <summary>
    /// Applies wheel blur visual effect settings.
    /// Creates a motion blur effect on wheels at high speeds.
    /// </summary>
    public static void ApplyWheelBlurSettings(RCCP_CarController carController, RCCP_AIConfig.WheelBlurConfig config) {
        if (carController == null || config == null) return;

        // WheelBlur is accessed through OtherAddonsManager
        RCCP_OtherAddons otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Use GetComponentInChildren instead of runtime property (otherAddons.WheelBlur)
        RCCP_WheelBlur wheelBlur = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_WheelBlur>(true) : null;

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(wheelBlur, "WheelBlur");
            return;
        }

        // From here on, we're enabling or configuring WheelBlur

        if (otherAddons == null) {
            // Create OtherAddons if it doesn't exist and user wants wheel blur
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create OtherAddons for WheelBlur: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating OtherAddons component for WheelBlur...");
            RCCP_CreateNewVehicle.AddOtherAddons(carController);
            otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);
        }

        if (otherAddons == null) {
            Debug.LogWarning("[RCCP AI] Failed to create OtherAddons component");
            return;
        }

        // Re-check for WheelBlur after potentially creating OtherAddons
        wheelBlur = otherAddons.GetComponentInChildren<RCCP_WheelBlur>(true);

        // Create WheelBlur component if it doesn't exist (config was sent, so user wants it)
        if (wheelBlur == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create WheelBlur: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating WheelBlur component...");

            // Create child GameObject like RCCP_OtherAddonsEditor does
            GameObject wheelBlurObject = new GameObject("RCCP_WheelBlur");
            Undo.RegisterCreatedObjectUndo(wheelBlurObject, "RCCP AI Create WheelBlur");
            wheelBlurObject.transform.SetParent(otherAddons.transform, false);
            wheelBlurObject.transform.SetSiblingIndex(0);
            wheelBlur = wheelBlurObject.AddComponent<RCCP_WheelBlur>();

            if (wheelBlur == null) {
                Debug.LogWarning("[RCCP AI] Failed to create WheelBlur component");
                return;
            }
        }

        Undo.RecordObject(wheelBlur, "RCCP AI Wheel Blur Settings");

        // If user is configuring this component, they want it enabled
        if (!wheelBlur.enabled) {
            wheelBlur.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] WheelBlur component was disabled, enabling it for configuration");
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.wheelBlur;

        // Apply offset if specified
        if (config.offset != null && !config.offset.IsZero) {
            wheelBlur.offset = config.offset.ToVector3();
        }

        // Apply scale if specified (0.0-0.2 range)
        if (config.scale > 0) {
            wheelBlur.scale = Mathf.Clamp(config.scale, 0f, 0.2f);
        }

        // Apply rotation speed if specified (0.0-5.0 range)
        if (config.rotationSpeed > 0) {
            wheelBlur.rotationSpeed = Mathf.Clamp(config.rotationSpeed, 0f, 5f);
        }

        // Apply smoothness if specified
        if (config.smoothness > 0) {
            wheelBlur.smoothness = config.smoothness;
        }

        EditorUtility.SetDirty(wheelBlur);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Wheel blur settings applied: scale={wheelBlur.scale}, rotationSpeed={wheelBlur.rotationSpeed}, smoothness={wheelBlur.smoothness}");
    }

    /// <summary>
    /// Applies exhaust effect settings.
    /// Configures smoke and flame effects on exhaust pipes.
    /// </summary>
    public static void ApplyExhaustSettings(RCCP_CarController carController, RCCP_AIConfig.ExhaustsConfig config) {
        if (carController == null || config == null) return;

        // Exhausts manager is accessed through OtherAddonsManager
        RCCP_OtherAddons otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Use GetComponentInChildren instead of runtime property (otherAddons.Exhausts)
        RCCP_Exhausts exhaustsManager = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_Exhausts>(true) : null;

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(exhaustsManager, "Exhausts");
            return;
        }

        // From here on, we're enabling or configuring Exhausts

        if (otherAddons == null) {
            // Create OtherAddons if it doesn't exist and user wants exhausts
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create OtherAddons for Exhausts: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating OtherAddons component for Exhausts...");
            RCCP_CreateNewVehicle.AddOtherAddons(carController);
            otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);
        }

        if (otherAddons == null) {
            Debug.LogWarning("[RCCP AI] Failed to create OtherAddons component");
            return;
        }

        // Re-check for Exhausts after potentially creating OtherAddons
        exhaustsManager = otherAddons.GetComponentInChildren<RCCP_Exhausts>(true);

        // Create Exhausts component if it doesn't exist (config was sent, so user wants it)
        if (exhaustsManager == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create Exhausts: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Exhausts component...");

            // Create child GameObject like RCCP_OtherAddonsEditor does
            GameObject exhaustsObject = new GameObject("RCCP_Exhausts");
            Undo.RegisterCreatedObjectUndo(exhaustsObject, "RCCP AI Create Exhausts");
            exhaustsObject.transform.SetParent(otherAddons.transform, false);
            exhaustsObject.transform.SetSiblingIndex(0);
            exhaustsManager = exhaustsObject.AddComponent<RCCP_Exhausts>();

            if (exhaustsManager == null) {
                Debug.LogWarning("[RCCP AI] Failed to create Exhausts component");
                return;
            }
        }

        // If user is configuring this component, they want it enabled
        if (!exhaustsManager.enabled) {
            exhaustsManager.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] Exhausts manager was disabled, enabling it for configuration");
        }

        // Refresh the exhaust list
        exhaustsManager.GetAllExhausts();
        RCCP_Exhaust[] exhausts = exhaustsManager.Exhaust;

        if (exhausts == null || exhausts.Length == 0) {
            if (VerboseLogging) Debug.LogWarning("[RCCP AI] No RCCP_Exhaust components found on vehicle.");
            return;
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.exhausts;

        if (config.exhausts != null) {
            foreach (var exhaustConfig in config.exhausts) {
                // Apply to specific exhaust or all
                if (exhaustConfig.exhaustIndex < 0) {
                    // Apply to all exhausts
                    foreach (var exhaust in exhausts) {
                        ApplySingleExhaustConfig(exhaust, exhaustConfig, defaults);
                    }
                } else if (exhaustConfig.exhaustIndex < exhausts.Length) {
                    // Apply to specific exhaust
                    ApplySingleExhaustConfig(exhausts[exhaustConfig.exhaustIndex], exhaustConfig, defaults);
                }
            }
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Exhaust settings applied to {exhausts.Length} exhaust(s)");
    }

    private static void ApplySingleExhaustConfig(RCCP_Exhaust exhaust, RCCP_AIConfig.ExhaustConfig config, RCCP_AIComponentDefaults.ExhaustsDefaults defaults) {
        if (exhaust == null || config == null) return;

        Undo.RecordObject(exhaust, "RCCP AI Exhaust Settings");

        // If user is configuring this exhaust, they want it enabled
        if (!exhaust.enabled) {
            exhaust.enabled = true;
            if (VerboseLogging) Debug.Log($"[RCCP AI] Exhaust '{exhaust.name}' was disabled, enabling it for configuration");
        }

        // Apply flame on cut-off
        if (config.ShouldModifyFlameOnCutOff) {
            exhaust.flameOnCutOff = config.flameOnCutOff == 1;
        }

        // Apply flare brightness
        if (config.flareBrightness > 0) {
            exhaust.flareBrightness = config.flareBrightness;
        }

        // Apply flame colors
        if (config.flameColor != null) {
            exhaust.flameColor = config.flameColor.ToColor();
        }
        if (config.boostFlameColor != null) {
            exhaust.boostFlameColor = config.boostFlameColor.ToColor();
        }

        // Apply emission settings
        if (config.minEmission > 0) exhaust.minEmission = config.minEmission;
        if (config.maxEmission > 0) exhaust.maxEmission = config.maxEmission;

        // Apply size settings
        if (config.minSize > 0) exhaust.minSize = config.minSize;
        if (config.maxSize > 0) exhaust.maxSize = config.maxSize;

        // Apply speed settings
        if (config.minSpeed > 0) exhaust.minSpeed = config.minSpeed;
        if (config.maxSpeed > 0) exhaust.maxSpeed = config.maxSpeed;

        EditorUtility.SetDirty(exhaust);
    }

    /// <summary>
    /// Applies body tilt visual effect settings.
    /// Tilts the vehicle body based on acceleration and turning.
    /// </summary>
    public static void ApplyBodyTiltSettings(RCCP_CarController carController, RCCP_AIConfig.BodyTiltConfig config) {
        if (carController == null || config == null) return;

        // BodyTilt is on OtherAddonsManager or as separate component
        RCCP_BodyTilt bodyTilt = carController.GetComponentInChildren<RCCP_BodyTilt>(true);

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(bodyTilt, "BodyTilt");
            return;
        }

        // If not found and user wants to enable it, we can try to add it
        if (bodyTilt == null) {
            if (config.ShouldModifyEnabled && config.enabled == 1) {
                // Cannot add BodyTilt automatically - it requires tiltTargets to be set
                if (VerboseLogging) Debug.LogWarning("[RCCP AI] Cannot add RCCP_BodyTilt automatically. It requires tiltTargets to be configured manually.");
            } else {
                if (VerboseLogging) Debug.LogWarning("[RCCP AI] No RCCP_BodyTilt component found on vehicle.");
            }
            return;
        }

        Undo.RecordObject(bodyTilt, "RCCP AI Body Tilt Settings");

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.bodyTilt;

        // If user is configuring this component, they want it enabled
        // Only disable if AI explicitly sends enabled=0
        if (config.ShouldModifyEnabled && config.enabled == 0) {
            bodyTilt.enabled = false;
        } else if (!bodyTilt.enabled) {
            bodyTilt.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] BodyTilt component was disabled, enabling it for configuration");
        }

        // Apply max tilt angle
        if (config.maxTiltAngle > 0) {
            bodyTilt.maxTiltAngle = config.maxTiltAngle;
        }

        // Apply forward tilt multiplier
        if (config.forwardTiltMultiplier > 0) {
            bodyTilt.forwardTiltMultiplier = config.forwardTiltMultiplier;
        }

        // Apply sideways tilt multiplier
        if (config.sidewaysTiltMultiplier > 0) {
            bodyTilt.sidewaysTiltMultiplier = config.sidewaysTiltMultiplier;
        }

        // Apply smooth speed
        if (config.tiltSmoothSpeed >= 0) {
            bodyTilt.tiltSmoothSpeed = config.tiltSmoothSpeed;
        }

        EditorUtility.SetDirty(bodyTilt);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Body tilt settings applied: enabled={bodyTilt.enabled}, maxAngle={bodyTilt.maxTiltAngle}, forward={bodyTilt.forwardTiltMultiplier}, sideways={bodyTilt.sidewaysTiltMultiplier}");
    }

    /// <summary>
    /// Applies particles system settings.
    /// Configures collision spark/scratch particle effects, tire smoke, and skidmarks.
    /// </summary>
    public static void ApplyParticlesSettings(RCCP_CarController carController, RCCP_AIConfig.ParticlesConfig config) {
        if (carController == null || config == null) return;

        RCCP_Particles particles = carController.GetComponentInChildren<RCCP_Particles>(true);

        // Handle REMOVE request - destroy the GameObject completely (or disable if prefab)
        if (config.remove) {
            TryDestroyComponentGameObject(particles, "Particles");
            return;
        }

        // Create Particles component if it doesn't exist
        if (particles == null) {
            if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                Debug.LogWarning("[RCCP AI] Cannot create Particles: Prefab modification cancelled.");
                return;
            }
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Particles component...");
            RCCP_CreateNewVehicle.AddParticles(carController);
            particles = carController.GetComponentInChildren<RCCP_Particles>(true);
        }

        if (particles == null) {
            Debug.LogWarning("[RCCP AI] Failed to create Particles component");
            return;
        }

        Undo.RecordObject(particles, "RCCP AI Particles Settings");

        // If user is configuring this component, they want it enabled
        if (!particles.enabled) {
            particles.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] Particles component was disabled, enabling it for configuration");
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.particles;

        // Apply collision filter mask if specified
        // -1 means all layers (default), otherwise use the specified mask
        if (config.collisionFilterMask != 0) {
            if (config.collisionFilterMask == -1) {
                particles.collisionFilter = -1; // All layers
            } else {
                particles.collisionFilter = config.collisionFilterMask;
            }
        }

        // Apply tire smoke settings (if fields exist on RCCP_Particles)
        // Note: These settings may be managed through RCCP_Settings or per-ground-material
        // The defaults are extracted but actual application depends on RCCP version

        // Apply skidmark settings if available
        // Note: Skidmark settings may be on RCCP_Skidmarks component or RCCP_Settings

        EditorUtility.SetDirty(particles);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Particles settings applied: collisionFilter={particles.collisionFilter}");
    }

    /// <summary>
    /// Applies all visual effects settings from a combined config.
    /// </summary>
    public static void ApplyVisualEffectsSettings(RCCP_CarController carController, RCCP_AIConfig.VisualEffectsConfig config) {
        if (carController == null || config == null) return;

        // Begin undo group for all visual effects changes
        Undo.SetCurrentGroupName("RCCP AI Visual Effects");
        int undoGroup = Undo.GetCurrentGroup();

        if (config.lod != null) {
            ApplyLodSettings(carController, config.lod);
        }

        if (config.wheelBlur != null) {
            ApplyWheelBlurSettings(carController, config.wheelBlur);
        }

        if (config.exhausts != null) {
            ApplyExhaustSettings(carController, config.exhausts);
        }

        if (config.bodyTilt != null) {
            ApplyBodyTiltSettings(carController, config.bodyTilt);
        }

        if (config.particles != null) {
            ApplyParticlesSettings(carController, config.particles);
        }

        // Collapse all changes into a single undo operation
        Undo.CollapseUndoOperations(undoGroup);

        if (VerboseLogging) Debug.Log($"[RCCP AI] Visual effects settings applied. {config.explanation}");
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
