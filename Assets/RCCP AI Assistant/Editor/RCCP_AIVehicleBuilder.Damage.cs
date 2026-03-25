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
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Partial class containing damage component configuration methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Damage Settings

    /// <summary>
    /// Applies damage settings to a vehicle. Creates RCCP_Damage component if it doesn't exist.
    /// Used by both the Damage panel and history restore.
    /// </summary>
    /// <param name="carController">The vehicle to configure</param>
    /// <param name="config">Damage configuration from AI response</param>
    /// <param name="configAllTrue">Optional allTrue config for detecting explicit boolean values in JSON</param>
    /// <returns>Number of detachable parts created/configured</returns>
    public static int ApplyDamageSettings(
        RCCP_CarController carController,
        RCCP_AIConfig.DamageConfig config,
        RCCP_AIConfig.DamageConfig configAllTrue = null) {

        if (carController == null) {
            Debug.LogError("[RCCP AI] Cannot apply damage: CarController is null");
            return 0;
        }

        if (config == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] No damage configuration to apply");
            return 0;
        }

        // Get or create damage component
        var damage = carController.GetComponentInChildren<RCCP_Damage>(true);

        if (damage == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Damage component...");
            RCCP_CreateNewVehicle.AddDamage(carController);
            damage = carController.GetComponentInChildren<RCCP_Damage>(true);

            if (damage == null) {
                Debug.LogError("[RCCP AI] Failed to create RCCP_Damage component");
                return 0;
            }
        }

        Undo.RecordObject(damage, "RCCP AI Damage");

        // If user is configuring this component, they want it enabled
        if (!damage.enabled) {
            damage.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] Damage component was disabled, enabling it for configuration");
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.damage;

        // Helper to safely apply booleans only when explicitly set in JSON
        void ApplyBool(ref bool field, bool value, bool allTrueValue) {
            if (configAllTrue == null) {
                // No allTrue config - apply directly (for restore or when JSON not available)
                field = value;
            } else if (value) {
                // Value is true - user explicitly set true
                field = true;
            } else if (!allTrueValue) {
                // Value is false AND allTrue is false - user explicitly set false
                field = false;
            }
            // Else: value is false AND allTrue is true - field was missing, don't change
        }

        // Apply mesh deformation settings
        ApplyBool(ref damage.meshDeformation, config.meshDeformation, configAllTrue?.meshDeformation ?? true);

        if (config.maximumDamage > 0)
            damage.maximumDamage = Mathf.Clamp(config.maximumDamage, 0.1f, 1f);
        else if (configAllTrue == null)
            damage.maximumDamage = defaults?.maximumDamage ?? 0.75f;

        if (config.deformationRadius > 0)
            damage.deformationRadius = Mathf.Clamp(config.deformationRadius, 0.1f, 2f);
        else if (configAllTrue == null)
            damage.deformationRadius = defaults?.deformationRadius ?? 0.75f;

        if (config.deformationMultiplier > 0)
            damage.deformationMultiplier = Mathf.Clamp(config.deformationMultiplier, 0.1f, 3f);
        else if (configAllTrue == null)
            damage.deformationMultiplier = defaults?.deformationMultiplier ?? 1f;

        ApplyBool(ref damage.automaticInstallation, config.automaticInstallation, configAllTrue?.automaticInstallation ?? true);

        // Apply wheel damage settings
        ApplyBool(ref damage.wheelDamage, config.wheelDamage, configAllTrue?.wheelDamage ?? true);
        if (config.wheelDamageRadius > 0)
            damage.wheelDamageRadius = Mathf.Clamp(config.wheelDamageRadius, 0.1f, 5f);
        if (config.wheelDamageMultiplier > 0)
            damage.wheelDamageMultiplier = Mathf.Clamp(config.wheelDamageMultiplier, 0.1f, 3f);
        ApplyBool(ref damage.wheelDetachment, config.wheelDetachment, configAllTrue?.wheelDetachment ?? true);

        // Apply light damage settings
        ApplyBool(ref damage.lightDamage, config.lightDamage, configAllTrue?.lightDamage ?? true);
        if (config.lightDamageRadius > 0)
            damage.lightDamageRadius = Mathf.Clamp(config.lightDamageRadius, 0.1f, 3f);
        if (config.lightDamageMultiplier > 0)
            damage.lightDamageMultiplier = Mathf.Clamp(config.lightDamageMultiplier, 0.1f, 3f);

        // Apply part damage settings
        ApplyBool(ref damage.partDamage, config.partDamage, configAllTrue?.partDamage ?? true);
        if (config.partDamageRadius > 0)
            damage.partDamageRadius = Mathf.Clamp(config.partDamageRadius, 0.1f, 5f);
        if (config.partDamageMultiplier > 0)
            damage.partDamageMultiplier = Mathf.Clamp(config.partDamageMultiplier, 0.1f, 3f);

        EditorUtility.SetDirty(damage);

        // Configure detachable parts if specified
        int partsCreated = 0;
        if (config.detachableParts != null && config.detachableParts.Length > 0) {
            partsCreated = ConfigureDetachableParts(carController.gameObject, config.detachableParts);
        }

        if (VerboseLogging) {
            string status = config.meshDeformation
                ? $"[RCCP AI] Damage configured: max={config.maximumDamage:F2}, radius={config.deformationRadius:F2}"
                : "[RCCP AI] Damage settings applied";
            if (partsCreated > 0)
                status += $", {partsCreated} detachable parts configured";
            Debug.Log(status);
        }

        return partsCreated;
    }

    /// <summary>
    /// Apply damage settings during restore operation.
    /// Restores damage system properties to previously captured values.
    /// </summary>
    private static void ApplyDamageSettingsForRestore(RCCP_CarController carController, RCCP_AIConfig.DamageConfig config) {
        if (config == null) return;

        var damage = carController.GetComponentInChildren<RCCP_Damage>(true);
        if (damage == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI Restore] No damage component found, skipping damage restore");
            return;
        }

        Undo.RecordObject(damage, "RCCP AI Restore Damage");

        damage.meshDeformation = config.meshDeformation;
        damage.maximumDamage = config.maximumDamage;
        damage.deformationRadius = config.deformationRadius;
        damage.deformationMultiplier = config.deformationMultiplier;
        damage.automaticInstallation = config.automaticInstallation;

        damage.wheelDamage = config.wheelDamage;
        damage.wheelDamageRadius = config.wheelDamageRadius;
        damage.wheelDamageMultiplier = config.wheelDamageMultiplier;
        damage.wheelDetachment = config.wheelDetachment;

        damage.lightDamage = config.lightDamage;
        damage.lightDamageRadius = config.lightDamageRadius;
        damage.lightDamageMultiplier = config.lightDamageMultiplier;

        damage.partDamage = config.partDamage;
        damage.partDamageRadius = config.partDamageRadius;
        damage.partDamageMultiplier = config.partDamageMultiplier;

        EditorUtility.SetDirty(damage);
        if (VerboseLogging) Debug.Log("[RCCP AI Restore] Damage settings restored");
    }

    #endregion

    #region Detachable Parts

    /// <summary>
    /// Configures detachable parts on a vehicle based on AI configuration.
    /// </summary>
    /// <param name="vehicle">The vehicle GameObject</param>
    /// <param name="partConfigs">Array of detachable part configurations</param>
    /// <returns>Number of parts created/configured</returns>
    public static int ConfigureDetachableParts(GameObject vehicle, RCCP_AIConfig.DetachablePartConfig[] partConfigs) {
        if (vehicle == null || partConfigs == null) return 0;

        int partsCreated = 0;

        foreach (var partConfig in partConfigs) {
            if (string.IsNullOrEmpty(partConfig.meshName)) continue;

            // Find the mesh transform by name
            Transform meshTransform = FindTransformByName(vehicle.transform, partConfig.meshName);
            if (meshTransform == null) {
                Debug.LogWarning($"[RCCP AI] Could not find mesh: {partConfig.meshName} for detachable part");
                continue;
            }

            // Check if already has RCCP_DetachablePart
            RCCP_DetachablePart existingPart = meshTransform.GetComponent<RCCP_DetachablePart>();
            if (existingPart != null) {
                // Update existing part settings
                Undo.RecordObject(existingPart, "RCCP AI Update Detachable Part");

                if (!string.IsNullOrEmpty(partConfig.partType)) {
                    if (Enum.TryParse<RCCP_DetachablePart.DetachablePartType>(partConfig.partType, true, out var pType)) {
                        existingPart.partType = pType;
                    }
                }
                if (partConfig.strength > 0) existingPart.strength = partConfig.strength;

                EditorUtility.SetDirty(existingPart);
                partsCreated++;
                continue;
            }

            // Add new RCCP_DetachablePart component
            RCCP_DetachablePart newPart = Undo.AddComponent<RCCP_DetachablePart>(meshTransform.gameObject);

            // Set part type
            if (!string.IsNullOrEmpty(partConfig.partType)) {
                if (Enum.TryParse<RCCP_DetachablePart.DetachablePartType>(partConfig.partType, true, out var pType)) {
                    newPart.partType = pType;
                }
            }

            // Set strength (how hard to detach)
            if (partConfig.strength > 0)
                newPart.strength = partConfig.strength;
            else
                newPart.strength = 100f;  // Default

            partsCreated++;

            if (VerboseLogging)
                Debug.Log($"[RCCP AI] Created detachable part: {partConfig.meshName} ({newPart.partType})");
        }

        return partsCreated;
    }

    // Note: FindTransformByName is defined in RCCP_AIVehicleBuilder.Creation.cs

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
