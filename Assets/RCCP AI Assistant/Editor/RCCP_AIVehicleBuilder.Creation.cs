//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Partial class containing vehicle creation and wheel detection methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Vehicle Creation

    /// <summary>
    /// Removes existing Rigidbody and WheelCollider components from the vehicle model.
    /// This matches RCCP Setup Wizard behavior to prevent physics conflicts.
    /// </summary>
    /// <param name="vehicle">The vehicle model to clean up</param>
    /// <returns>Count of removed components (Rigidbodies, WheelColliders)</returns>
    private static (int rigidbodies, int wheelColliders) RemoveExistingPhysicsComponents(GameObject vehicle) {
        int removedRigidbodies = 0;
        int removedWheelColliders = 0;

        // Remove existing Rigidbodies (matches RCCP_CreateNewVehicle.cs:60-65)
        Rigidbody[] rigidbodies = vehicle.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies) {
            Undo.DestroyObjectImmediate(rb);
            removedRigidbodies++;
        }

        // Remove existing WheelColliders (matches RCCP_CreateNewVehicle.cs:78-81)
        WheelCollider[] wheelColliders = vehicle.GetComponentsInChildren<WheelCollider>(true);
        foreach (WheelCollider wc in wheelColliders) {
            Undo.DestroyObjectImmediate(wc);
            removedWheelColliders++;
        }

        if (VerboseLogging && (removedRigidbodies > 0 || removedWheelColliders > 0)) {
            Debug.Log($"[RCCP AI] Removed {removedRigidbodies} Rigidbody(s) and {removedWheelColliders} WheelCollider(s) from model");
        }

        return (removedRigidbodies, removedWheelColliders);
    }

    private static RCCP_CarController CreateNewVehicle(GameObject vehicle, RCCP_AIConfig.VehicleSetupConfig config) {
        // Remove existing physics components to prevent conflicts (matches RCCP Setup Wizard behavior)
        RemoveExistingPhysicsComponents(vehicle);

        // Fix pivot position - create parent object at bounds center (same as RCCP Setup Wizard)
        // NOTE: We use the original vehicle's name, NOT the config name - users prefer to keep their model names intact
        GameObject pivot = new GameObject(vehicle.name);
        Undo.RegisterCreatedObjectUndo(pivot, "RCCP AI Create Vehicle");

        pivot.transform.position = RCCP_GetBounds.GetBoundsCenter(vehicle.transform);
        pivot.transform.rotation = vehicle.transform.rotation;

        // Parent original vehicle model to pivot (with undo support)
        Undo.SetTransformParent(vehicle.transform, pivot.transform, "RCCP AI Parent Vehicle");

        // Add RCCP_CarController to pivot
        RCCP_CarController carController = Undo.AddComponent<RCCP_CarController>(pivot);
        if (carController == null) {
            Debug.LogError("[RCCP AI] Failed to add RCCP_CarController component!");
            Undo.DestroyObjectImmediate(pivot);
            return null;
        }

        // Select the pivot object
        Selection.activeGameObject = pivot;

        // Add Rigidbody to pivot
        Rigidbody rb = pivot.GetComponent<Rigidbody>();
        if (rb == null) {
            rb = Undo.AddComponent<Rigidbody>(pivot);
        }

        if (rb == null) {
            Debug.LogError("[RCCP AI] Failed to add Rigidbody component!");
            return null;
        }

        // Setup Rigidbody defaults (from extracted RCCP defaults)
        var rbDefaults = RCCP_AIComponentDefaults.Instance?.rigidbody;
        Undo.RecordObject(rb, "RCCP AI Rigidbody Setup");
        float aiMass = config.vehicleConfig?.mass ?? 0f;
        rb.mass = aiMass > 0 ? aiMass : (rbDefaults?.mass ?? 1350f);
        rb.linearDamping = rbDefaults?.linearDamping ?? 0.0025f;
        rb.angularDamping = rbDefaults?.angularDamping ?? 0.35f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // Create drivetrain components using RCCP's methods (same order as RCCP_CreateNewVehicle.AddAllComponents)
        RCCP_CreateNewVehicle.AddEngine(carController);
        RCCP_CreateNewVehicle.AddClutch(carController);
        RCCP_CreateNewVehicle.AddGearbox(carController);
        RCCP_CreateNewVehicle.AddAxles(carController);
        RCCP_CreateNewVehicle.AddDifferential(carController);
        RCCP_CreateNewVehicle.AddDifferentialToAxle(carController);  // Connect differential to rear axle

        // Wire up drivetrain events using RCCP's methods
        RCCP_CreateNewVehicle.AddEngineToClutchListener(carController);
        RCCP_CreateNewVehicle.AddClutchToGearboxListener(carController);
        RCCP_CreateNewVehicle.AddGearboxToDifferentialListener(carController);

        // Create addon components using RCCP's methods (same order as RCCP_CreateNewVehicle.AddAddonComponents)
        RCCP_CreateNewVehicle.AddInputs(carController);
        RCCP_CreateNewVehicle.AddAero(carController);
        RCCP_CreateNewVehicle.AddStability(carController);
        RCCP_CreateNewVehicle.AddAudio(carController);
        RCCP_CreateNewVehicle.AddCustomizer(carController);
        RCCP_CreateNewVehicle.AddLights(carController);
        RCCP_CreateNewVehicle.AddDamage(carController);
        RCCP_CreateNewVehicle.AddParticles(carController);
        RCCP_CreateNewVehicle.AddLOD(carController);
        RCCP_CreateNewVehicle.AddOtherAddons(carController);

        // Create body colliders from the largest meshes
        CreateBodyColliders(vehicle.transform, carController);

        return carController;
    }

    /// <summary>
    /// Creates convex MeshColliders on the largest mesh parts of the vehicle body.
    /// Excludes wheel meshes and very small parts.
    /// </summary>
    /// <param name="vehicleModel">The model transform containing mesh parts</param>
    /// <param name="carController">The car controller (to get wheel transforms to exclude)</param>
    /// <param name="maxColliders">Maximum number of colliders to create (default 5)</param>
    private static void CreateBodyColliders(Transform vehicleModel, RCCP_CarController carController, int maxColliders = 5) {
        if (vehicleModel == null) return;

        // Collect transforms to exclude (wheel models)
        HashSet<Transform> excludedTransforms = new HashSet<Transform>();

        // Get wheel transforms from axles (if already assigned)
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        foreach (var axle in axles) {
            if (axle.leftWheelModel != null) {
                excludedTransforms.Add(axle.leftWheelModel);
                // Also exclude children of wheel models
                foreach (Transform child in axle.leftWheelModel.GetComponentsInChildren<Transform>(true)) {
                    excludedTransforms.Add(child);
                }
            }
            if (axle.rightWheelModel != null) {
                excludedTransforms.Add(axle.rightWheelModel);
                foreach (Transform child in axle.rightWheelModel.GetComponentsInChildren<Transform>(true)) {
                    excludedTransforms.Add(child);
                }
            }
        }

        // Also exclude by name pattern (fallback for new vehicles where wheels aren't assigned yet)
        string[] wheelNamePatterns = { "wheel", "tire", "tyre", "rim", "whl", "fl_", "fr_", "rl_", "rr_", "lf_", "rf_", "lr_", "rr_" };

        // Collect all mesh filters, excluding wheels
        List<MeshFilter> meshFilters = new List<MeshFilter>();
        foreach (MeshFilter mf in vehicleModel.GetComponentsInChildren<MeshFilter>(true)) {
            if (mf == null || mf.sharedMesh == null) continue;
            if (excludedTransforms.Contains(mf.transform)) continue;

            // Skip meshes with wheel-related names
            string meshName = mf.gameObject.name.ToLower();
            bool isWheelByName = false;
            foreach (string pattern in wheelNamePatterns) {
                if (meshName.Contains(pattern)) {
                    isWheelByName = true;
                    break;
                }
            }
            if (isWheelByName) continue;

            // Skip very small meshes (likely small details)
            float volume = GetMeshVolume(mf.sharedMesh);
            if (volume < 0.001f) continue;

            meshFilters.Add(mf);
        }

        // Sort by mesh volume (largest first)
        meshFilters.Sort((a, b) => GetMeshVolume(b.sharedMesh).CompareTo(GetMeshVolume(a.sharedMesh)));

        // Add MeshColliders to the top N largest meshes
        int collidersAdded = 0;
        foreach (MeshFilter mf in meshFilters) {
            if (collidersAdded >= maxColliders) break;

            // Skip if already has a collider
            if (mf.GetComponent<Collider>() != null) continue;

            // Add convex MeshCollider
            MeshCollider mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.convex = true;
            mc.sharedMesh = mf.sharedMesh;
            collidersAdded++;

            if (VerboseLogging) Debug.Log($"[RCCP AI] Added body collider to: {mf.gameObject.name}");
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Created {collidersAdded} body colliders");
    }

    /// <summary>
    /// Calculates the approximate volume of a mesh based on its bounds.
    /// Used for sorting meshes by size.
    /// </summary>
    private static float GetMeshVolume(Mesh mesh) {
        if (mesh == null) return 0f;
        Vector3 size = mesh.bounds.size;
        return size.x * size.y * size.z;
    }

    #endregion

    #region Wheel Detection

    private static void AssignDetectedWheels(RCCP_CarController carController, Transform root, RCCP_AIConfig.DetectedWheels detectedWheels) {
        // Get front/rear axles by comparing wheel Z positions
        FindFrontRearAxles(carController, out RCCP_Axle frontAxle, out RCCP_Axle rearAxle);

        // Find wheel transforms
        Transform frontLeft = FindTransformByPath(root, detectedWheels.frontLeft);
        Transform frontRight = FindTransformByPath(root, detectedWheels.frontRight);
        Transform rearLeft = FindTransformByPath(root, detectedWheels.rearLeft);
        Transform rearRight = FindTransformByPath(root, detectedWheels.rearRight);

        // Assign to front axle
        if (frontAxle != null) {
            Undo.RecordObject(frontAxle, "RCCP AI Assign Wheels");

            if (frontLeft != null) {
                frontAxle.leftWheelModel = frontLeft;
                if (frontAxle.leftWheelCollider != null) {
                    Undo.RecordObject(frontAxle.leftWheelCollider, "RCCP AI Assign Wheel");
                    frontAxle.leftWheelCollider.wheelModel = frontLeft;
                    AlignWheelCollider(frontAxle.leftWheelCollider, frontLeft);
                }
            }

            if (frontRight != null) {
                frontAxle.rightWheelModel = frontRight;
                if (frontAxle.rightWheelCollider != null) {
                    Undo.RecordObject(frontAxle.rightWheelCollider, "RCCP AI Assign Wheel");
                    frontAxle.rightWheelCollider.wheelModel = frontRight;
                    AlignWheelCollider(frontAxle.rightWheelCollider, frontRight);
                }
            }
        }

        // Assign to rear axle
        if (rearAxle != null) {
            Undo.RecordObject(rearAxle, "RCCP AI Assign Wheels");

            if (rearLeft != null) {
                rearAxle.leftWheelModel = rearLeft;
                if (rearAxle.leftWheelCollider != null) {
                    Undo.RecordObject(rearAxle.leftWheelCollider, "RCCP AI Assign Wheel");
                    rearAxle.leftWheelCollider.wheelModel = rearLeft;
                    AlignWheelCollider(rearAxle.leftWheelCollider, rearLeft);
                }
            }

            if (rearRight != null) {
                rearAxle.rightWheelModel = rearRight;
                if (rearAxle.rightWheelCollider != null) {
                    Undo.RecordObject(rearAxle.rightWheelCollider, "RCCP AI Assign Wheel");
                    rearAxle.rightWheelCollider.wheelModel = rearRight;
                    AlignWheelCollider(rearAxle.rightWheelCollider, rearRight);
                }
            }
        }
    }

    private static Transform FindTransformByPath(Transform root, string path) {
        if (string.IsNullOrEmpty(path)) return null;

        // Try direct find
        Transform result = root.Find(path);
        if (result != null) return result;

        // Try recursive search by name
        string name = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;
        return FindTransformByName(root, name);
    }

    private static Transform FindTransformByName(Transform parent, string name) {
        if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
            return parent;
        }

        foreach (Transform child in parent) {
            Transform found = FindTransformByName(child, name);
            if (found != null) return found;
        }

        return null;
    }

    private static void AlignWheelCollider(RCCP_WheelCollider wheelCollider, Transform wheelModel) {
        if (wheelCollider == null || wheelModel == null) return;

        // Use RCCP's built-in AlignWheel method which handles positioning and radius
        wheelCollider.AlignWheel();
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
