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
/// Partial class containing lights component configuration methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Lights Settings

    /// <summary>
    /// Applies lights settings to a vehicle. Creates RCCP_Lights component if it doesn't exist.
    /// Used by both the Lights panel and history restore.
    /// </summary>
    /// <param name="carController">The vehicle to configure</param>
    /// <param name="config">Lights configuration from AI response</param>
    /// <returns>Tuple of (lightsCreated, lightsModified)</returns>
    public static (int created, int modified) ApplyLightsSettings(
        RCCP_CarController carController,
        RCCP_AIConfig.LightsConfig config) {

        if (carController == null) {
            Debug.LogError("[RCCP AI] Cannot apply lights: CarController is null");
            return (0, 0);
        }

        if (config == null || config.lights == null || config.lights.Length == 0) {
            if (VerboseLogging) Debug.Log("[RCCP AI] No lights configuration to apply");
            return (0, 0);
        }

        // Validate RCCP_Settings.Instance early to fail fast with clear message
        if (RCCP_Settings.Instance == null) {
            Debug.LogError("[RCCP AI] RCCP_Settings.Instance is null. Please ensure RCCP is properly configured.");
            return (0, 0);
        }
        if (RCCP_Settings.Instance.lightsSetupData == null) {
            Debug.LogError("[RCCP AI] RCCP_Settings.Instance.lightsSetupData is null. Please configure lights setup data in RCCP Settings.");
            return (0, 0);
        }

        // Get or create RCCP_Lights manager
        var lightsManager = carController.GetComponentInChildren<RCCP_Lights>(true);
        if (lightsManager == null) {
            if (VerboseLogging) Debug.Log($"[RCCP AI] Creating Lights component on {carController.gameObject.name}...");
            RCCP_CreateNewVehicle.AddLights(carController);
            lightsManager = carController.GetComponentInChildren<RCCP_Lights>(true);

            if (lightsManager == null) {
                Debug.LogError("[RCCP AI] Failed to create RCCP_Lights component");
                return (0, 0);
            }
        }

        Undo.RecordObject(lightsManager, "RCCP AI Lights");

        // If user is configuring this component, they want it enabled
        if (!lightsManager.enabled) {
            lightsManager.enabled = true;
            if (VerboseLogging) Debug.Log($"[RCCP AI] Lights component was disabled on {carController.gameObject.name}, enabling it for configuration");
        }

        // Get vehicle bounds for auto-positioning new lights
        Bounds vehicleBounds = GetVehicleBounds(carController.gameObject);
        Vector3 ext = vehicleBounds.extents;

        int lightsModified = 0;
        int lightsCreated = 0;

        foreach (var lightConfig in config.lights) {
            if (string.IsNullOrEmpty(lightConfig.lightType)) continue;

            // Parse light type from string
            if (!TryParseLightType(lightConfig.lightType, out RCCP_Light.LightType targetType)) {
                Debug.LogWarning($"[RCCP AI] Unknown light type: {lightConfig.lightType}");
                continue;
            }

            // Find existing lights of this type
            List<RCCP_Light> existingLights = new List<RCCP_Light>();
            if (lightsManager.lights != null) {
                foreach (var light in lightsManager.lights) {
                    if (light != null && light.lightType == targetType)
                        existingLights.Add(light);
                }
            }

            // If no lights of this type exist, create them
            if (existingLights.Count == 0) {
                var newLights = CreateLightsOfType(lightsManager, targetType, lightConfig, ext);
                lightsCreated += newLights.Count;
                existingLights.AddRange(newLights);
            }

            // Apply settings to all lights of this type
            foreach (var light in existingLights) {
                ApplyLightSettings(light, lightConfig);
                lightsModified++;
            }
        }

        // Refresh the lights list
        lightsManager.GetAllLights();

        if (VerboseLogging && (lightsCreated > 0 || lightsModified > 0)) {
            string status = lightsCreated > 0
                ? $"[RCCP AI] {carController.gameObject.name}: Created {lightsCreated} and modified {lightsModified} light(s)"
                : $"[RCCP AI] {carController.gameObject.name}: Modified {lightsModified} light(s)";
            Debug.Log(status);
        }

        return (lightsCreated, lightsModified);
    }

    /// <summary>
    /// Applies settings from a LightConfig to an RCCP_Light component.
    /// </summary>
    private static void ApplyLightSettings(RCCP_Light light, RCCP_AIConfig.LightConfig lightConfig) {
        if (light == null) return;

        Undo.RecordObject(light, "RCCP AI Light Settings");

        // If user is configuring this light, they want it enabled
        if (!light.enabled) {
            light.enabled = true;
            if (VerboseLogging) Debug.Log($"[RCCP AI] Light '{light.name}' was disabled, enabling it for configuration");
        }

        // Only change intensity if explicitly specified (preserve existing value otherwise)
        if (lightConfig.intensity > 0)
            light.intensity = Mathf.Clamp(lightConfig.intensity, 0.1f, 10f);

        if (lightConfig.smoothness > 0)
            light.smoothness = Mathf.Clamp(lightConfig.smoothness, 0.1f, 1f);

        // Flare brightness
        if (lightConfig.flareBrightness > 0)
            light.flareBrightness = Mathf.Clamp(lightConfig.flareBrightness, 0f, 10f);

        // Use lens flares toggle (1 = disable, 2 = enable)
        if (lightConfig.ShouldModifyLensFlares)
            light.useLensFlares = lightConfig.useLensFlares == 2;

        // Breakable properties (1 = not breakable, 2 = breakable)
        if (lightConfig.ShouldModifyBreakable)
            light.isBreakable = lightConfig.isBreakable == 2;

        if (lightConfig.strength > 0)
            light.strength = Mathf.Clamp(lightConfig.strength, 1f, 500f);

        if (lightConfig.breakPoint > 0)
            light.breakPoint = Mathf.Clamp(lightConfig.breakPoint, 1, 100);

        // Get or update Unity Light component
        Light unityLight = light.GetComponent<Light>();
        if (unityLight != null) {
            Undo.RecordObject(unityLight, "RCCP AI Light Settings");

            // Range - crucial for visibility (Unity Light property, not RCCP_Light)
            if (lightConfig.range > 0)
                unityLight.range = Mathf.Clamp(lightConfig.range, 1f, 200f);

            // Spot angle (for spot lights) - Unity Light property
            if (lightConfig.spotAngle > 0 && unityLight.type == LightType.Spot)
                unityLight.spotAngle = Mathf.Clamp(lightConfig.spotAngle, 1f, 179f);

            // Light color - Unity Light property
            if (lightConfig.lightColor != null && lightConfig.lightColor.IsSpecified)
                unityLight.color = lightConfig.lightColor.ToColor();

            EditorUtility.SetDirty(unityLight);
        }

        // Handle lens flare component (pipeline-aware)
        if (light.useLensFlares)
            EnsureCorrectLensFlareComponent(light.gameObject);

        EditorUtility.SetDirty(light);
    }

    /// <summary>
    /// Apply lights settings during restore operation.
    /// Removes lights added by AI modification, restores properties to captured values.
    /// </summary>
    private static void ApplyLightsSettingsForRestore(RCCP_CarController carController, RCCP_AIConfig.LightsConfig config) {
        if (config == null || config.lights == null) return;

        var lightsManager = carController.GetComponentInChildren<RCCP_Lights>(true);
        if (lightsManager == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI Restore] No lights component found, skipping lights restore");
            return;
        }

        Undo.RecordObject(lightsManager, "RCCP AI Restore Lights");

        // Step 1: Build expected count-per-type and grouped configs from the before-state
        var configsByType = new Dictionary<RCCP_Light.LightType, List<RCCP_AIConfig.LightConfig>>();
        foreach (var lc in config.lights) {
            if (string.IsNullOrEmpty(lc.lightType)) continue;
            if (!TryParseLightType(lc.lightType, out RCCP_Light.LightType lt)) continue;
            if (!configsByType.ContainsKey(lt))
                configsByType[lt] = new List<RCCP_AIConfig.LightConfig>();
            configsByType[lt].Add(lc);
        }

        // Step 2: Group current vehicle lights by type
        var currentByType = new Dictionary<RCCP_Light.LightType, List<RCCP_Light>>();
        if (lightsManager.lights != null) {
            foreach (var light in lightsManager.lights) {
                if (light == null) continue;
                if (!currentByType.ContainsKey(light.lightType))
                    currentByType[light.lightType] = new List<RCCP_Light>();
                currentByType[light.lightType].Add(light);
            }
        }

        // Step 3: Remove lights that shouldn't exist
        // - Types not in the before-state config (added by AI modification)
        // - Excess lights if current count exceeds the before-state count for that type
        var lightsToRemove = new List<RCCP_Light>();
        foreach (var kvp in currentByType) {
            if (!configsByType.ContainsKey(kvp.Key)) {
                // This entire type was added by AI - remove all lights of this type
                lightsToRemove.AddRange(kvp.Value);
                if (VerboseLogging) Debug.Log($"[RCCP AI Restore] Removing {kvp.Value.Count} light(s) of type {kvp.Key} (not in before-state)");
            } else {
                int expected = configsByType[kvp.Key].Count;
                if (kvp.Value.Count > expected) {
                    // More lights of this type than before - remove excess from the end
                    for (int i = expected; i < kvp.Value.Count; i++)
                        lightsToRemove.Add(kvp.Value[i]);
                    if (VerboseLogging) Debug.Log($"[RCCP AI Restore] Removing {kvp.Value.Count - expected} excess light(s) of type {kvp.Key}");
                }
            }
        }

        int removedCount = 0;
        foreach (var light in lightsToRemove) {
            if (light != null) {
                var result = TryDestroyComponentGameObject(light, $"Light ({light.lightType})");
                if (result == DestroyResult.Success || result == DestroyResult.DisabledInstead)
                    removedCount++;
            }
        }

        // Step 4: Refresh the lights list after removals
        if (removedCount > 0)
            lightsManager.GetAllLights();

        // Step 5: Restore settings on remaining lights, matched by type then index
        int restoredCount = 0;
        foreach (var kvp in configsByType) {
            var type = kvp.Key;
            var configs = kvp.Value;

            // Get remaining lights of this type on the vehicle
            var remaining = new List<RCCP_Light>();
            if (lightsManager.lights != null) {
                foreach (var l in lightsManager.lights) {
                    if (l != null && l.lightType == type)
                        remaining.Add(l);
                }
            }

            // Apply settings by index
            int applyCount = Mathf.Min(configs.Count, remaining.Count);
            for (int i = 0; i < applyCount; i++) {
                RestoreSingleLightSettings(remaining[i], configs[i]);
                restoredCount++;
            }

            if (configs.Count > remaining.Count && VerboseLogging)
                Debug.Log($"[RCCP AI Restore] {configs.Count - remaining.Count} light(s) of type {type} missing on vehicle, cannot recreate");
        }

        lightsManager.GetAllLights();
        EditorUtility.SetDirty(lightsManager);
        if (VerboseLogging) Debug.Log($"[RCCP AI Restore] Lights restored: {restoredCount} modified, {removedCount} removed");
    }

    /// <summary>
    /// Restores all properties of a single RCCP_Light from a captured config.
    /// </summary>
    private static void RestoreSingleLightSettings(RCCP_Light light, RCCP_AIConfig.LightConfig lightConfig) {
        if (light == null || lightConfig == null) return;

        Undo.RecordObject(light, "RCCP AI Restore Light");

        light.intensity = lightConfig.intensity;
        light.smoothness = lightConfig.smoothness;
        light.flareBrightness = lightConfig.flareBrightness;
        light.useLensFlares = lightConfig.useLensFlares == 2;
        light.isBreakable = lightConfig.isBreakable == 2;
        light.strength = lightConfig.strength;
        light.breakPoint = lightConfig.breakPoint;

        // Restore Unity Light component settings
        var unityLight = light.GetComponent<Light>();
        if (unityLight != null) {
            Undo.RecordObject(unityLight, "RCCP AI Restore Unity Light");
            unityLight.range = lightConfig.range;
            unityLight.spotAngle = lightConfig.spotAngle;

            if (lightConfig.lightColor != null && lightConfig.lightColor.IsSpecified)
                unityLight.color = lightConfig.lightColor.ToColor();

            EditorUtility.SetDirty(unityLight);
        }

        EditorUtility.SetDirty(light);
    }

    #endregion

    #region Light Creation Helpers

    /// <summary>
    /// Creates lights of a specific type at auto-calculated positions based on vehicle bounds.
    /// </summary>
    private static List<RCCP_Light> CreateLightsOfType(RCCP_Lights manager, RCCP_Light.LightType lightType,
        RCCP_AIConfig.LightConfig config, Vector3 ext) {

        List<RCCP_Light> created = new List<RCCP_Light>();

        // Use specified color if provided, otherwise use default for light type
        Color color = (config.emissiveColor != null && config.emissiveColor.IsSpecified)
            ? config.emissiveColor.ToColor()
            : GetDefaultColorForType(lightType);
        float intensity = config.intensity > 0 ? config.intensity : GetDefaultIntensityForType(lightType);

        // Check if custom position is provided
        if (config.HasCustomPosition) {
            // Use custom position - create single light at specified position
            Vector3 customPos = config.position.ToVector3();
            Quaternion customRot = config.HasCustomRotation
                ? Quaternion.Euler(config.rotation.ToVector3())
                : (customPos.z < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);

            string lightName = $"RCCP_{lightType}_Custom";
            created.Add(SpawnLightWithRotation(manager, lightName, customPos, customRot, lightType, color, intensity));
            return created;
        }

        // Positioning offsets (same as RCCP_LightSetupWizard defaults)
        float fx = 0.05f;   // forward offset
        float sx = -0.45f;  // side offset
        float hx = -0.25f;  // height offset

        switch (lightType) {
            case RCCP_Light.LightType.Headlight_LowBeam:
                created.Add(SpawnLight(manager, "RCCP_LowBeam_L",
                    new Vector3(-(ext.x + sx), hx + ext.y * 0.5f, ext.z + fx), lightType, color, intensity));
                created.Add(SpawnLight(manager, "RCCP_LowBeam_R",
                    new Vector3((ext.x + sx), hx + ext.y * 0.5f, ext.z + fx), lightType, color, intensity));
                break;

            case RCCP_Light.LightType.Headlight_HighBeam:
                created.Add(SpawnLight(manager, "RCCP_HighBeam_L",
                    new Vector3(-(ext.x * 0.6f + sx), hx + ext.y * 0.6f, ext.z + fx), lightType, color, intensity));
                created.Add(SpawnLight(manager, "RCCP_HighBeam_R",
                    new Vector3((ext.x * 0.6f + sx), hx + ext.y * 0.6f, ext.z + fx), lightType, color, intensity));
                break;

            case RCCP_Light.LightType.Brakelight:
                created.Add(SpawnLight(manager, "RCCP_Brake_L",
                    new Vector3(-(ext.x + sx), hx + ext.y * 0.5f, -ext.z - fx), lightType, color, intensity));
                created.Add(SpawnLight(manager, "RCCP_Brake_R",
                    new Vector3((ext.x + sx), hx + ext.y * 0.5f, -ext.z - fx), lightType, color, intensity));
                break;

            case RCCP_Light.LightType.Taillight:
                created.Add(SpawnLight(manager, "RCCP_Tail_L",
                    new Vector3(-(ext.x * 0.8f + sx), hx + ext.y * 0.45f, -ext.z - fx), lightType, color, intensity));
                created.Add(SpawnLight(manager, "RCCP_Tail_R",
                    new Vector3((ext.x * 0.8f + sx), hx + ext.y * 0.45f, -ext.z - fx), lightType, color, intensity));
                break;

            case RCCP_Light.LightType.Reverselight:
                created.Add(SpawnLight(manager, "RCCP_Reverse_L",
                    new Vector3(-(ext.x * 0.6f + sx), hx + ext.y * 0.3f, -ext.z - fx), lightType, color, intensity));
                created.Add(SpawnLight(manager, "RCCP_Reverse_R",
                    new Vector3((ext.x * 0.6f + sx), hx + ext.y * 0.3f, -ext.z - fx), lightType, color, intensity));
                break;

            case RCCP_Light.LightType.IndicatorLeftLight:
                float yInd = hx + ext.y * 0.4f;
                created.Add(SpawnLight(manager, "RCCP_Indicator_FL",
                    new Vector3(-(ext.x + sx), yInd, ext.z + fx), lightType, color, intensity));
                created.Add(SpawnLight(manager, "RCCP_Indicator_RL",
                    new Vector3(-(ext.x + sx), yInd, -ext.z - fx), lightType, color, intensity));
                break;

            case RCCP_Light.LightType.IndicatorRightLight:
                float yIndR = hx + ext.y * 0.4f;
                created.Add(SpawnLight(manager, "RCCP_Indicator_FR",
                    new Vector3((ext.x + sx), yIndR, ext.z + fx), lightType, color, intensity));
                created.Add(SpawnLight(manager, "RCCP_Indicator_RR",
                    new Vector3((ext.x + sx), yIndR, -ext.z - fx), lightType, color, intensity));
                break;
        }

        return created;
    }

    /// <summary>
    /// Spawns a single light at a position, auto-rotating rear lights to face backward.
    /// </summary>
    private static RCCP_Light SpawnLight(RCCP_Lights manager, string name, Vector3 localPos,
        RCCP_Light.LightType lightType, Color color, float intensity) {
        // Rotate rear lights to face backward
        Quaternion rotation = localPos.z < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        return SpawnLightWithRotation(manager, name, localPos, rotation, lightType, color, intensity);
    }

    /// <summary>
    /// Spawns a single light with explicit position and rotation.
    /// </summary>
    internal static RCCP_Light SpawnLightWithRotation(RCCP_Lights manager, string name, Vector3 localPos,
        Quaternion localRotation, RCCP_Light.LightType lightType, Color color, float intensity) {

        // Defensive null check (main validation is in ApplyLightsSettings, but be safe)
        if (RCCP_Settings.Instance == null || RCCP_Settings.Instance.lightsSetupData == null) {
            Debug.LogError("RCCP_Settings or lightsSetupData is null - cannot create light");
            return null;
        }

        // Get setup data from RCCP_Settings (same as RCCP_LightsEditor uses)
        RCCP_LightSetupData setupData = RCCP_Settings.Instance.lightsSetupData;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create RCCP Light");
        go.transform.SetParent(manager.transform);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRotation;

        // Add Unity Light component (same configuration as RCCP_LightsEditor.CreateLight)
        Light l = go.AddComponent<Light>();
        l.type = UnityEngine.LightType.Spot;
        l.color = color;
        l.intensity = intensity;
        l.range = 30f;
        l.spotAngle = 90f;
        l.renderMode = LightRenderMode.ForcePixel;

        // Add RCCP_Light component
        RCCP_Light rcLight = go.AddComponent<RCCP_Light>();
        rcLight.lightType = lightType;
        rcLight.intensity = intensity;
        rcLight.emissiveColor = color;

        // Add lens flares if enabled in RCCP settings (same as RCCP_LightsEditor.CreateLight)
        if (setupData.useLensFlares) {

#if !BCG_URP && !BCG_HDRP
            LensFlare flareComp = go.AddComponent<LensFlare>();
            flareComp.flare = setupData.flare;
            flareComp.brightness = 0f;

            // Set ignore layers via SerializedObject (property not exposed in public API)
            SerializedObject serializedFlare = new SerializedObject(flareComp);
            SerializedProperty ignoreLayers = serializedFlare.FindProperty("m_IgnoreLayers");
            if (ignoreLayers != null) {
                ignoreLayers.intValue = LayerMask.GetMask(
                    RCCP_Settings.Instance.RCCPLayer,
                    RCCP_Settings.Instance.RCCPWheelColliderLayer,
                    RCCP_Settings.Instance.RCCPDetachablePartLayer
                );
                serializedFlare.ApplyModifiedPropertiesWithoutUndo();
            }
#else
            UnityEngine.Rendering.LensFlareComponentSRP flareComp = go.AddComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
            flareComp.lensFlareData = setupData.lensFlareSRP as UnityEngine.Rendering.LensFlareDataSRP;
            flareComp.attenuationByLightShape = false;
            flareComp.intensity = 0f;
#endif

        }

        // Register with manager
        manager.RegisterLight(rcLight);

        return rcLight;
    }

    #endregion

    #region Light Utility Methods

    /// <summary>
    /// Gets the vehicle bounds in local space for light positioning.
    /// </summary>
    public static Bounds GetVehicleBounds(GameObject vehicle) {
        Bounds localBounds = new Bounds();
        bool boundsInitialized = false;

        MeshFilter[] meshFilters = vehicle.GetComponentsInChildren<MeshFilter>(false);

        foreach (MeshFilter mf in meshFilters) {
            if (mf.sharedMesh == null) continue;

            Bounds meshBounds = mf.sharedMesh.bounds;
            Vector3 centerLS = meshBounds.center;
            Vector3 extentsLS = meshBounds.extents;

            // Iterate each corner of the mesh bounds
            for (int xi = -1; xi <= 1; xi += 2) {
                for (int yi = -1; yi <= 1; yi += 2) {
                    for (int zi = -1; zi <= 1; zi += 2) {
                        Vector3 cornerMeshLocal = centerLS + Vector3.Scale(extentsLS, new Vector3(xi, yi, zi));
                        Vector3 cornerWorld = mf.transform.TransformPoint(cornerMeshLocal);
                        Vector3 cornerVehicleLocal = vehicle.transform.InverseTransformPoint(cornerWorld);

                        if (!boundsInitialized) {
                            localBounds = new Bounds(cornerVehicleLocal, Vector3.zero);
                            boundsInitialized = true;
                        } else {
                            localBounds.Encapsulate(cornerVehicleLocal);
                        }
                    }
                }
            }
        }

        if (!boundsInitialized)
            localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);

        return localBounds;
    }

    /// <summary>
    /// Gets the default color for a light type from RCCP settings.
    /// </summary>
    internal static Color GetDefaultColorForType(RCCP_Light.LightType lightType) {
        // Defensive null check (main validation is in ApplyLightsSettings, but be safe)
        if (RCCP_Settings.Instance == null || RCCP_Settings.Instance.lightsSetupData == null) {
            return Color.white;  // Safe fallback
        }

        // Use RCCP_Settings.Instance.lightsSetupData (same as RCCP_LightsEditor)
        RCCP_LightSetupData setupData = RCCP_Settings.Instance.lightsSetupData;

        switch (lightType) {
            case RCCP_Light.LightType.Headlight_LowBeam:
            case RCCP_Light.LightType.Headlight_HighBeam:
                return setupData.headlightColor;
            case RCCP_Light.LightType.Brakelight:
                return setupData.brakelightColor;
            case RCCP_Light.LightType.Taillight:
                return setupData.taillightColor;
            case RCCP_Light.LightType.Reverselight:
                return setupData.reverselightColor;
            case RCCP_Light.LightType.IndicatorLeftLight:
            case RCCP_Light.LightType.IndicatorRightLight:
                return setupData.indicatorColor;
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Gets the default intensity for a light type from RCCP settings.
    /// </summary>
    internal static float GetDefaultIntensityForType(RCCP_Light.LightType lightType) {
        var defaults = RCCP_AIComponentDefaults.Instance?.lights;
        float defaultIntensity = defaults?.intensity ?? 1f;

        // Defensive null check (main validation is in ApplyLightsSettings, but be safe)
        if (RCCP_Settings.Instance == null || RCCP_Settings.Instance.lightsSetupData == null) {
            return defaultIntensity;  // Safe fallback
        }

        // Use RCCP_Settings.Instance.lightsSetupData (same as RCCP_LightsEditor)
        RCCP_LightSetupData setupData = RCCP_Settings.Instance.lightsSetupData;

        switch (lightType) {
            case RCCP_Light.LightType.Headlight_LowBeam:
            case RCCP_Light.LightType.Headlight_HighBeam:
                return setupData.defaultIntensityForHeadlights > 0
                    ? setupData.defaultIntensityForHeadlights
                    : defaultIntensity;
            case RCCP_Light.LightType.Brakelight:
            case RCCP_Light.LightType.Taillight:
                return setupData.defaultIntensityForBrakeLights > 0
                    ? setupData.defaultIntensityForBrakeLights
                    : defaultIntensity;
            case RCCP_Light.LightType.Reverselight:
                return setupData.defaultIntensityForReverseLights > 0
                    ? setupData.defaultIntensityForReverseLights
                    : defaultIntensity;
            case RCCP_Light.LightType.IndicatorLeftLight:
            case RCCP_Light.LightType.IndicatorRightLight:
                return setupData.defaultIntensityForIndicatorLights > 0
                    ? setupData.defaultIntensityForIndicatorLights
                    : defaultIntensity;
            default:
                return defaultIntensity;
        }
    }

    /// <summary>
    /// Parses a light type string to RCCP_Light.LightType enum, with fuzzy matching support.
    /// </summary>
    public static bool TryParseLightType(string typeString, out RCCP_Light.LightType lightType) {
        lightType = RCCP_Light.LightType.Headlight_LowBeam;

        if (string.IsNullOrEmpty(typeString)) return false;

        // Try exact enum parse first (handles "Headlight_LowBeam", "Brakelight", etc.)
        try {
            lightType = (RCCP_Light.LightType)Enum.Parse(typeof(RCCP_Light.LightType), typeString, true);
            return true;
        } catch (ArgumentException) {
            // Expected when string doesn't match enum name - fall through to fuzzy matching
        }

        // Normalize the string for fuzzy matching
        string normalized = typeString.Replace(" ", "").Replace("-", "_").ToLower();

        // Match against known patterns
        if (normalized.Contains("lowbeam") || normalized == "headlight" || normalized == "headlights")
            lightType = RCCP_Light.LightType.Headlight_LowBeam;
        else if (normalized.Contains("highbeam"))
            lightType = RCCP_Light.LightType.Headlight_HighBeam;
        else if (normalized.Contains("brake"))
            lightType = RCCP_Light.LightType.Brakelight;
        else if (normalized.Contains("tail"))
            lightType = RCCP_Light.LightType.Taillight;
        else if (normalized.Contains("reverse"))
            lightType = RCCP_Light.LightType.Reverselight;
        else if (normalized.Contains("indicatorleft") || normalized.Contains("leftindicator") ||
                 normalized.Contains("indicator_left") || normalized.Contains("left_indicator"))
            lightType = RCCP_Light.LightType.IndicatorLeftLight;
        else if (normalized.Contains("indicatorright") || normalized.Contains("rightindicator") ||
                 normalized.Contains("indicator_right") || normalized.Contains("right_indicator"))
            lightType = RCCP_Light.LightType.IndicatorRightLight;
        else
            return false;

        return true;
    }

    /// <summary>
    /// Ensures the light GameObject has the correct lens flare component for the current render pipeline.
    /// Removes incompatible components and adds the correct one.
    /// </summary>
    public static void EnsureCorrectLensFlareComponent(GameObject lightObj) {
        if (lightObj == null) return;

#if BCG_URP || BCG_HDRP
        // URP/HDRP: Use LensFlareComponentSRP

        // Remove old built-in LensFlare if present
        LensFlare oldFlare = lightObj.GetComponent<LensFlare>();
        if (oldFlare != null) {
            Undo.DestroyObjectImmediate(oldFlare);
            if (VerboseLogging)
                Debug.Log($"[RCCP AI] Removed legacy LensFlare from {lightObj.name} (using URP/HDRP)");
        }

        // Check if SRP lens flare exists
        UnityEngine.Rendering.LensFlareComponentSRP srpFlare = lightObj.GetComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
        if (srpFlare == null) {
            // Add SRP lens flare
            srpFlare = Undo.AddComponent<UnityEngine.Rendering.LensFlareComponentSRP>(lightObj);
            srpFlare.attenuationByLightShape = false;
            srpFlare.intensity = 0f; // RCCP_Light controls this dynamically

            // Get lens flare data from RCCP_Settings
            if (RCCP_Settings.Instance.lensFlareData != null) {
                srpFlare.lensFlareData = RCCP_Settings.Instance.lensFlareData as UnityEngine.Rendering.LensFlareDataSRP;
            }

            if (VerboseLogging)
                Debug.Log($"[RCCP AI] Added LensFlareComponentSRP to {lightObj.name}");
        } else {
            // Ensure it has the correct data
            if (srpFlare.lensFlareData == null && RCCP_Settings.Instance.lensFlareData != null) {
                Undo.RecordObject(srpFlare, "RCCP AI LensFlare Data");
                srpFlare.lensFlareData = RCCP_Settings.Instance.lensFlareData as UnityEngine.Rendering.LensFlareDataSRP;
            }
        }

        EditorUtility.SetDirty(srpFlare);
#else
        // Built-in pipeline: Use legacy LensFlare

        // Remove SRP lens flare if present (shouldn't happen, but safety check)
        // Note: LensFlareComponentSRP won't exist in built-in, so we can't check for it directly
        // The preprocessor ensures this code path only runs in built-in

        LensFlare legacyFlare = lightObj.GetComponent<LensFlare>();
        if (legacyFlare == null) {
            // Add legacy lens flare
            legacyFlare = Undo.AddComponent<LensFlare>(lightObj);
            legacyFlare.brightness = 0f; // RCCP_Light controls this dynamically

            // Get flare asset from RCCP_Settings
            if (RCCP_Settings.Instance.flare != null) {
                legacyFlare.flare = RCCP_Settings.Instance.flare;
            }

            // Set ignore layers for vehicle (prevents self-occlusion)
            SetLensFlareIgnoreLayers(legacyFlare);

            if (VerboseLogging)
                Debug.Log($"[RCCP AI] Added LensFlare to {lightObj.name}");
        } else {
            // Ensure it has the correct flare asset
            if (legacyFlare.flare == null && RCCP_Settings.Instance.flare != null) {
                Undo.RecordObject(legacyFlare, "RCCP AI LensFlare Asset");
                legacyFlare.flare = RCCP_Settings.Instance.flare;
            }
        }

        EditorUtility.SetDirty(legacyFlare);
#endif
    }

#if !BCG_URP && !BCG_HDRP
    /// <summary>
    /// Sets the ignore layers on a LensFlare to prevent vehicle self-occlusion.
    /// </summary>
    private static void SetLensFlareIgnoreLayers(LensFlare flare) {
        if (flare == null || RCCP_Settings.Instance == null) return;

        int ignoreMask = 0;

        if (!string.IsNullOrEmpty(RCCP_Settings.Instance.RCCPLayer))
            ignoreMask |= LayerMask.GetMask(RCCP_Settings.Instance.RCCPLayer);

        if (!string.IsNullOrEmpty(RCCP_Settings.Instance.RCCPWheelColliderLayer))
            ignoreMask |= LayerMask.GetMask(RCCP_Settings.Instance.RCCPWheelColliderLayer);

        if (!string.IsNullOrEmpty(RCCP_Settings.Instance.RCCPDetachablePartLayer))
            ignoreMask |= LayerMask.GetMask(RCCP_Settings.Instance.RCCPDetachablePartLayer);

        if (ignoreMask == 0) return;

        // Use SerializedObject to access the hidden m_IgnoreLayers property
        var serializedFlare = new SerializedObject(flare);
        var ignoreLayers = serializedFlare.FindProperty("m_IgnoreLayers");

        if (ignoreLayers != null) {
            ignoreLayers.intValue |= ignoreMask;
            serializedFlare.ApplyModifiedPropertiesWithoutUndo();
        }
    }
#endif

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
