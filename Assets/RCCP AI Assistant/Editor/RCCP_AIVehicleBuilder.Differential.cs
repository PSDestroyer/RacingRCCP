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
/// Partial class containing differential-related methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Differential Helpers

    /// <summary>
    /// Parses a differential type string into the enum value.
    /// </summary>
    private static RCCP_Differential.DifferentialType ParseDifferentialType(string typeString, RCCP_Differential.DifferentialType defaultType = RCCP_Differential.DifferentialType.Limited) {
        if (string.IsNullOrEmpty(typeString)) return defaultType;

        switch (typeString.ToLower()) {
            case "open":
                return RCCP_Differential.DifferentialType.Open;
            case "limited":
                return RCCP_Differential.DifferentialType.Limited;
            case "fulllocked":
            case "locked":
                return RCCP_Differential.DifferentialType.FullLocked;
            case "direct":
                return RCCP_Differential.DifferentialType.Direct;
            default:
                return defaultType;
        }
    }

    /// <summary>
    /// Finds the front and rear axles using multiple detection strategies.
    /// 1. First tries axle names ("Front"/"Rear") which is RCCP's naming convention
    /// 2. Falls back to wheel collider Z positions if names don't help
    /// 3. Falls back to hierarchy order if Z positions are too close (wheels not positioned yet)
    /// </summary>
    private static void FindFrontRearAxles(RCCP_CarController carController, out RCCP_Axle frontAxle, out RCCP_Axle rearAxle) {
        frontAxle = null;
        rearAxle = null;

        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        if (axles == null || axles.Length < 2) return;

        // Strategy 1: Use RCCP's naming convention (most reliable)
        foreach (var axle in axles) {
            string axleName = axle.gameObject.name.ToLower();
            if (axleName.Contains("front") && frontAxle == null) {
                frontAxle = axle;
            } else if (axleName.Contains("rear") && rearAxle == null) {
                rearAxle = axle;
            }
        }

        // If we found both via naming, we're done
        if (frontAxle != null && rearAxle != null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] FindFrontRearAxles: Identified by axle names");
            return;
        }

        // Strategy 2: Use wheel collider Z positions
        float highestZ = float.MinValue;
        float lowestZ = float.MaxValue;
        RCCP_Axle highestZAxle = null;
        RCCP_Axle lowestZAxle = null;

        foreach (var axle in axles) {
            if (axle.leftWheelCollider == null) continue;

            // Get wheel collider position relative to the car controller (vehicle root)
            float wheelZ = carController.transform.InverseTransformPoint(
                axle.leftWheelCollider.transform.position).z;

            if (wheelZ > highestZ) {
                highestZ = wheelZ;
                highestZAxle = axle;
            }

            if (wheelZ < lowestZ) {
                lowestZ = wheelZ;
                lowestZAxle = axle;
            }
        }

        // Check if Z positions are meaningful (more than 0.1m apart)
        // If wheel colliders haven't been positioned yet, they'll all be at similar positions
        const float MIN_AXLE_SEPARATION = 0.1f;
        bool hasValidZPositions = (highestZ - lowestZ) >= MIN_AXLE_SEPARATION;

        if (hasValidZPositions && highestZAxle != null && lowestZAxle != null && highestZAxle != lowestZAxle) {
            // Use Z positions
            if (frontAxle == null) frontAxle = highestZAxle;
            if (rearAxle == null) rearAxle = lowestZAxle;
            if (VerboseLogging) Debug.Log($"[RCCP AI] FindFrontRearAxles: Identified by Z positions (separation: {highestZ - lowestZ:F2}m)");
            return;
        }

        // Strategy 3: Fall back to hierarchy order (first axle = front, last = rear)
        // This matches RCCP's default creation order in RCCP_Axles.Reset()
        if (frontAxle == null) frontAxle = axles[0];
        if (rearAxle == null) rearAxle = axles[axles.Length - 1];

        if (VerboseLogging) Debug.Log("[RCCP AI] FindFrontRearAxles: Using hierarchy order (wheel colliders not positioned yet)");
    }

    /// <summary>
    /// Ensures the correct number of differentials exist for the drive type.
    /// Creates or removes differentials as needed.
    /// Returns the updated differentials array.
    /// </summary>
    private static RCCP_Differential[] EnsureDifferentialsForDriveType(
        RCCP_CarController carController,
        RCCP_Differential[] differentials,
        string driveType,
        RCCP_Axle frontAxle,
        RCCP_Axle rearAxle) {

        if (differentials.Length == 0) {
            Debug.LogWarning("[RCCP AI] No differentials found on vehicle.");
            return differentials;
        }

        if (driveType == "AWD") {
            // AWD needs 2 differentials
            if (differentials.Length < 2) {
                // Unpack prefab if needed before creating GameObjects
                if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                    Debug.LogWarning("[RCCP AI] Cannot create differential: Failed to unpack prefab instance.");
                    return differentials;
                }

                // Store original settings
                RCCP_Differential originalDiff = differentials[0];
                var originalType = originalDiff.differentialType;
                float originalSlipRatio = originalDiff.limitedSlipRatio;
                float originalFinalDrive = originalDiff.finalDriveRatio;

                // Create second differential
                GameObject refDiff = differentials[0].gameObject;
                GameObject newDiffObj = UnityEngine.Object.Instantiate(refDiff, refDiff.transform.parent);
                if (newDiffObj == null) {
                    Debug.LogWarning("[RCCP AI] Failed to create second differential for AWD");
                    return differentials;
                }

                // Register the new object for undo
                Undo.RegisterCreatedObjectUndo(newDiffObj, "RCCP AI Create Differential");

                newDiffObj.transform.SetSiblingIndex(refDiff.transform.GetSiblingIndex() + 1);

                // Copy settings to new differential
                RCCP_Differential newDiff = newDiffObj.GetComponent<RCCP_Differential>();
                if (newDiff != null) {
                    Undo.RecordObject(newDiff, "RCCP AI Differential Settings");
                    newDiff.differentialType = originalType;
                    newDiff.limitedSlipRatio = originalSlipRatio;
                    newDiff.finalDriveRatio = originalFinalDrive;
                    EditorUtility.SetDirty(newDiff);
                }

                // Name them appropriately (with undo support)
                Undo.RecordObject(refDiff, "RCCP AI Rename Differential");
                refDiff.name = "RCCP_Differential_Front";
                newDiffObj.name = "RCCP_Differential_Rear";

                // Refresh array
                differentials = carController.GetComponentsInChildren<RCCP_Differential>(true);

                if (VerboseLogging) Debug.Log($"[RCCP AI] Created second differential for AWD. Now have {differentials.Length} differentials.");
            }

            // Connect differentials to axles
            ConnectDifferentialsToAxles(differentials, frontAxle, rearAxle, "AWD");

        } else {
            // FWD or RWD needs only 1 differential
            if (differentials.Length > 1) {
                // Unpack prefab if needed before destroying GameObjects
                if (!RCCP_AIUtility.UnpackPrefabIfNeeded(carController.gameObject)) {
                    Debug.LogWarning("[RCCP AI] Cannot remove differential: Failed to unpack prefab instance.");
                    return differentials;
                }

                // Remove extra differentials
                for (int i = differentials.Length - 1; i > 0; i--) {
                    if (VerboseLogging) Debug.Log($"[RCCP AI] Removing extra differential: {differentials[i].gameObject.name}");
                    Undo.DestroyObjectImmediate(differentials[i].gameObject);
                }

                // Refresh array
                differentials = carController.GetComponentsInChildren<RCCP_Differential>(true);
            }

            // Connect to appropriate axle
            if (differentials.Length > 0) {
                Undo.RecordObject(differentials[0], "RCCP AI Differential Connection");
                Undo.RecordObject(differentials[0].gameObject, "RCCP AI Rename Differential");

                RCCP_Axle targetAxle = driveType == "FWD" ? frontAxle : rearAxle;
                differentials[0].connectedAxle = targetAxle;
                differentials[0].gameObject.name = "RCCP_Differential";
                EditorUtility.SetDirty(differentials[0]);
            }
        }

        // Rewire gearbox to all differentials
        RCCP_CreateNewVehicle.AddGearboxToDifferentialListener(carController);

        return differentials;
    }

    /// <summary>
    /// Connects differentials to front and rear axles based on drive type.
    /// </summary>
    private static void ConnectDifferentialsToAxles(RCCP_Differential[] differentials, RCCP_Axle frontAxle, RCCP_Axle rearAxle, string driveType) {
        if (differentials.Length == 0) return;

        if (driveType == "AWD" && differentials.Length >= 2) {
            // Try to match by name first
            bool frontConnected = false;
            bool rearConnected = false;

            foreach (var diff in differentials) {
                Undo.RecordObject(diff, "RCCP AI Differential Connection");

                if (diff.gameObject.name.Contains("Front") && !frontConnected) {
                    diff.connectedAxle = frontAxle;
                    frontConnected = true;
                    EditorUtility.SetDirty(diff);
                } else if (!rearConnected) {
                    diff.connectedAxle = rearAxle;
                    rearConnected = true;
                    EditorUtility.SetDirty(diff);
                }
            }

            // Fallback: assign in order
            if (!frontConnected || !rearConnected) {
                Undo.RecordObject(differentials[0], "RCCP AI Differential Connection");
                differentials[0].connectedAxle = frontAxle;
                EditorUtility.SetDirty(differentials[0]);
                if (differentials.Length > 1) {
                    Undo.RecordObject(differentials[1], "RCCP AI Differential Connection");
                    differentials[1].connectedAxle = rearAxle;
                    EditorUtility.SetDirty(differentials[1]);
                }
            }
        } else if (driveType == "FWD") {
            Undo.RecordObject(differentials[0], "RCCP AI Differential Connection");
            differentials[0].connectedAxle = frontAxle;
            EditorUtility.SetDirty(differentials[0]);
        } else {
            // RWD (default)
            Undo.RecordObject(differentials[0], "RCCP AI Differential Connection");
            differentials[0].connectedAxle = rearAxle;
            EditorUtility.SetDirty(differentials[0]);
        }
    }

    /// <summary>
    /// Applies differential settings (type, slip ratio, final drive) to all differentials.
    /// </summary>
    private static void ApplyDifferentialProperties(RCCP_Differential[] differentials, RCCP_AIConfig.DifferentialConfig config, bool partialUpdate = false) {
        if (differentials.Length == 0) return;

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.differential;

        // For partial updates, only change type if explicitly specified
        bool hasTypeSpecified = config != null && !string.IsNullOrEmpty(config.type);
        var diffType = hasTypeSpecified
            ? ParseDifferentialType(config.type)
            : (config == null ? ParseDifferentialType(defaults?.differentialType ?? "Limited") : differentials[0].differentialType);

        foreach (var diff in differentials) {
            Undo.RecordObject(diff, "RCCP AI Differential");

            // Only update type if specified (or in full update mode)
            if (hasTypeSpecified || !partialUpdate) {
                diff.differentialType = diffType;
            }

            if (partialUpdate && !IsRestoreMode) {
                // Partial update - only apply non-zero values
                if (config != null) {
                    if (config.limitedSlipRatio > 0) diff.limitedSlipRatio = config.limitedSlipRatio;
                    if (config.finalDriveRatio > 0) diff.finalDriveRatio = config.finalDriveRatio;
                }
            } else {
                // Full update - use config values with defaults as fallback
                float slipRatio = config?.limitedSlipRatio ?? 0;
                float finalDrive = config?.finalDriveRatio ?? 0;
                diff.limitedSlipRatio = slipRatio > 0 ? slipRatio : defaults?.limitedSlipRatio ?? 80f;
                diff.finalDriveRatio = finalDrive > 0 ? finalDrive : defaults?.finalDriveRatio ?? 3.73f;
            }

            EditorUtility.SetDirty(diff);
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Differential updated: type={differentials[0].differentialType}, slip={differentials[0].limitedSlipRatio}, finalDrive={differentials[0].finalDriveRatio}");
    }

    #endregion

    #region Differential Apply Methods

    private static void ApplyDifferentialSettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config) {
        RCCP_Differential[] differentials = carController.GetComponentsInChildren<RCCP_Differential>(true);
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);

        if (differentials.Length == 0 || config.differential == null) return;

        string driveType = config.driveType?.ToUpper() ?? "RWD";

        // Find front and rear axles using RCCP's built-in detection
        FindFrontRearAxles(carController, out RCCP_Axle frontAxle, out RCCP_Axle rearAxle);

        // Ensure correct number of differentials and connections for drive type
        differentials = EnsureDifferentialsForDriveType(carController, differentials, driveType, frontAxle, rearAxle);

        // Apply differential properties (type, slip ratio, final drive)
        ApplyDifferentialProperties(differentials, config.differential, partialUpdate: false);
    }

    private static void ApplyDifferentialSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.DifferentialConfig config) {
        RCCP_Differential[] differentials = carController.GetComponentsInChildren<RCCP_Differential>(true);
        if (differentials.Length == 0) return;

        // Apply differential properties (type, slip ratio, final drive) - partial update mode
        ApplyDifferentialProperties(differentials, config, partialUpdate: true);
    }

    /// <summary>
    /// Changes the drive type of an existing vehicle (FWD, RWD, AWD).
    /// Manages differentials and axle connections accordingly.
    /// </summary>
    private static void ApplyDriveTypeChange(RCCP_CarController carController, string newDriveType) {
        if (string.IsNullOrEmpty(newDriveType)) return;

        string driveType = newDriveType.ToUpper();
        if (driveType != "FWD" && driveType != "RWD" && driveType != "AWD") {
            Debug.LogWarning($"[RCCP AI] Invalid drive type: {newDriveType}. Must be FWD, RWD, or AWD.");
            return;
        }

        // Get current differentials and axles
        RCCP_Differential[] differentials = carController.GetComponentsInChildren<RCCP_Differential>(true);
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);

        if (differentials.Length == 0) {
            Debug.LogWarning("[RCCP AI] No differentials found on vehicle. Cannot change drive type.");
            return;
        }

        // Find front and rear axles using RCCP's built-in detection
        FindFrontRearAxles(carController, out RCCP_Axle frontAxle, out RCCP_Axle rearAxle);

        if (frontAxle == null || rearAxle == null) {
            Debug.LogWarning("[RCCP AI] Could not find front and rear axles. Cannot change drive type.");
            return;
        }

        // Detect current drive type
        string currentDriveType = DetectCurrentDriveType(differentials, frontAxle, rearAxle);

        if (currentDriveType == driveType) {
            if (VerboseLogging) Debug.Log($"[RCCP AI] Vehicle is already {driveType}. No changes needed.");
            return;
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Changing drive type from {currentDriveType} to {driveType}...");

        // Use helper to ensure correct differential count and connections
        // Note: isPower is set automatically by RCCP_Differential at runtime
        EnsureDifferentialsForDriveType(carController, differentials, driveType, frontAxle, rearAxle);

        if (VerboseLogging) Debug.Log($"[RCCP AI] Drive type changed to {driveType} successfully.");
    }

    /// <summary>
    /// Detects the current drive type based on differential configuration.
    /// </summary>
    private static string DetectCurrentDriveType(RCCP_Differential[] differentials, RCCP_Axle frontAxle, RCCP_Axle rearAxle) {
        if (differentials.Length >= 2) {
            // Check if both axles have differentials connected
            bool hasFrontDiff = false;
            bool hasRearDiff = false;

            foreach (var diff in differentials) {
                if (diff.connectedAxle == frontAxle) hasFrontDiff = true;
                if (diff.connectedAxle == rearAxle) hasRearDiff = true;
            }

            if (hasFrontDiff && hasRearDiff) {
                return "AWD";
            }
        }

        // Single differential - check which axle it's connected to
        if (differentials.Length > 0 && differentials[0].connectedAxle != null) {
            if (differentials[0].connectedAxle == frontAxle) {
                return "FWD";
            } else if (differentials[0].connectedAxle == rearAxle) {
                return "RWD";
            }
        }

        // Default to RWD if we can't determine
        return "RWD";
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
