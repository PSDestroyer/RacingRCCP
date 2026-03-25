//----------------------------------------------
//        RCCP AI Setup Assistant
//        Review & Apply Panel
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Data structure representing a single property change.
/// </summary>
[Serializable]
public class RCCP_AIPropertyChange {
    public string propertyName;
    public string oldValue;
    public string newValue;
    public string componentType;  // For "Go to Field" / "Select" functionality
    
    public RCCP_AIPropertyChange(string name, string oldVal, string newVal, string compType = null) {
        propertyName = name;
        oldValue = oldVal ?? "—";
        newValue = newVal;
        componentType = compType;
    }
    
    public bool HasChanged => oldValue != newValue && newValue != "—" && !string.IsNullOrEmpty(newValue);
}

/// <summary>
/// Data structure representing a group of changes (e.g., Drivetrain, Chassis).
/// </summary>
[Serializable]
public class RCCP_AIChangeGroup {
    public string groupName;
    public string icon;
    public List<RCCP_AIPropertyChange> changes = new List<RCCP_AIPropertyChange>();
    public bool isEnabled = true;
    public bool isExpanded = false;
    
    public int ChangeCount => changes.Count(c => c.HasChanged);
    public bool HasChanges => ChangeCount > 0;
    
    public RCCP_AIChangeGroup(string name, string iconEmoji = "📦") {
        groupName = name;
        icon = iconEmoji;
    }
    
    public void AddChange(string property, string oldVal, string newVal, string compType = null) {
        changes.Add(new RCCP_AIPropertyChange(property, oldVal, newVal, compType));
    }
}

/// <summary>
/// Complete review data for a vehicle configuration.
/// </summary>
[Serializable]
public class RCCP_AIReviewData {
    public string vehicleName;
    public string vehicleDimensions;
    public string modelUsed;
    public float estimatedCost;
    public int tokenCount;
    public List<RCCP_AIChangeGroup> changeGroups = new List<RCCP_AIChangeGroup>();
    public string rawJson;
    public string explanation;
    
    // Reference to the parsed config for selective application
    public RCCP_AIConfig.VehicleSetupConfig parsedConfig;
    
    public int TotalEnabledChanges => changeGroups
        .Where(g => g.isEnabled)
        .Sum(g => g.ChangeCount);
    
    public int TotalChanges => changeGroups.Sum(g => g.ChangeCount);
}

/// <summary>
/// Review & Apply Panel for RCCP AI Assistant.
/// Shows structured change preview before applying AI-generated configurations.
/// </summary>
public class RCCP_AIReviewPanel {
    
    #region Callbacks
    
    public Action<List<string>> OnApplySelected;  // Called with list of enabled group names
    public Action OnApplyAll;
    public Action OnBack;
    public Action OnRegenerate;
    public Action<string> OnCopyJson;
    public Action<RCCP_AIReviewData> OnSavePreset;
    public Func<string, UnityEngine.Object> OnSelectComponent;  // Returns component by type name
    
    #endregion
    
    #region State
    
    private RCCP_AIReviewData reviewData;
    private Vector2 scrollPosition;
    private bool showJsonPreview;
    
    // Design system shortcut
    private static class DS {
        public static Color Accent => RCCP_AIDesignSystem.Colors.AccentPrimary;
        public static Color AccentMuted => RCCP_AIDesignSystem.Colors.AccentMuted;
        public static Color BgBase => RCCP_AIDesignSystem.Colors.GetBgBase();
        public static Color BgElevated => RCCP_AIDesignSystem.Colors.BgElevated;
        public static Color BgRecessed => RCCP_AIDesignSystem.Colors.BgRecessed;
        public static Color BgHover => RCCP_AIDesignSystem.Colors.BgHover;
        public static Color TextPrimary => RCCP_AIDesignSystem.Colors.GetTextPrimary();
        public static Color TextSecondary => RCCP_AIDesignSystem.Colors.GetTextSecondary();
        public static Color TextDisabled => RCCP_AIDesignSystem.Colors.TextDisabled;
        public static Color Success => RCCP_AIDesignSystem.Colors.Success;
        public static Color Warning => RCCP_AIDesignSystem.Colors.Warning;
        public static Color Error => RCCP_AIDesignSystem.Colors.Error;
        public static Color Border => RCCP_AIDesignSystem.Colors.BorderDefault;
        public static Color BorderLight => RCCP_AIDesignSystem.Colors.BorderLight;
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Set the review data to display.
    /// </summary>
    public void SetReviewData(RCCP_AIReviewData data) {
        reviewData = data;
        scrollPosition = Vector2.zero;
        showJsonPreview = false;
        
        // Initialize all groups - expand those with changes, enable those with changes
        if (data != null) {
            foreach (var group in data.changeGroups) {
                group.isExpanded = group.HasChanges;
                group.isEnabled = group.HasChanges;
            }
        }
    }
    
    /// <summary>
    /// Create review data from AI configuration response.
    /// Call this after parsing the AI JSON response.
    /// </summary>
    public static RCCP_AIReviewData CreateFromResponse(
        RCCP_CarController vehicle,
        string rawJson,
        string modelUsed,
        float cost,
        int tokens,
        RCCP_AIPromptAsset.PanelType panelType = RCCP_AIPromptAsset.PanelType.Generic) {

        if (string.IsNullOrEmpty(rawJson)) return null;

        // Extract JSON if wrapped in markdown
        string extracted = ExtractJson(rawJson);

        // Wheels panel returns WheelConfig directly, not VehicleSetupConfig
        if (panelType == RCCP_AIPromptAsset.PanelType.Wheels) {
            return CreateFromWheelsResponse(vehicle, extracted, modelUsed, cost, tokens);
        }

        // Audio panel returns AudioConfig directly, not VehicleSetupConfig
        if (panelType == RCCP_AIPromptAsset.PanelType.Audio) {
            return CreateFromAudioResponse(vehicle, extracted, modelUsed, cost, tokens);
        }

        // Lights panel returns LightsConfig directly, not VehicleSetupConfig
        if (panelType == RCCP_AIPromptAsset.PanelType.Lights) {
            return CreateFromLightsResponse(vehicle, extracted, modelUsed, cost, tokens);
        }

        // Damage panel returns DamageConfig directly, not VehicleSetupConfig
        if (panelType == RCCP_AIPromptAsset.PanelType.Damage) {
            return CreateFromDamageResponse(vehicle, extracted, modelUsed, cost, tokens);
        }

        RCCP_AIConfig.VehicleSetupConfig config;
        RCCP_AIConfig.VehicleSetupConfig configAllTrue;
        try {
            config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(extracted);
            // Parse again with booleans initialized to true - for detecting explicitly set false values
            configAllTrue = ParseWithAllBoolsTrue(extracted);
        } catch {
            return null;
        }

        if (config == null) return null;

        var data = new RCCP_AIReviewData {
            vehicleName = vehicle != null ? vehicle.name : "New Vehicle",
            vehicleDimensions = GetVehicleDimensions(vehicle),
            modelUsed = modelUsed,
            estimatedCost = cost,
            tokenCount = tokens,
            rawJson = extracted,  // Store extracted JSON, not original response with potential markdown
            explanation = config.explanation,
            parsedConfig = config
        };

        // Build change groups (passing configAllTrue for explicit boolean detection)
        data.changeGroups = BuildChangeGroups(vehicle, config, configAllTrue);

        return data;
    }

    /// <summary>
    /// Create review data specifically for Wheels panel responses (WheelConfig format).
    /// </summary>
    private static RCCP_AIReviewData CreateFromWheelsResponse(
        RCCP_CarController vehicle,
        string extracted,
        string modelUsed,
        float cost,
        int tokens) {

        RCCP_AIConfig.WheelConfig wheelConfig;
        try {
            wheelConfig = JsonUtility.FromJson<RCCP_AIConfig.WheelConfig>(extracted);
        } catch {
            return null;
        }

        if (wheelConfig == null || !RCCP_AIVehicleBuilder.HasMeaningfulValues(wheelConfig)) {
            return null;
        }

        var data = new RCCP_AIReviewData {
            vehicleName = vehicle != null ? vehicle.name : "Vehicle",
            vehicleDimensions = GetVehicleDimensions(vehicle),
            modelUsed = modelUsed,
            estimatedCost = cost,
            tokenCount = tokens,
            rawJson = extracted,
            explanation = wheelConfig.explanation
        };

        // Build wheel-specific change groups
        data.changeGroups = BuildWheelChangeGroups(vehicle, wheelConfig);

        return data;
    }

    /// <summary>
    /// Build change groups from WheelConfig (for Wheels panel).
    /// </summary>
    private static List<RCCP_AIChangeGroup> BuildWheelChangeGroups(
        RCCP_CarController vehicle,
        RCCP_AIConfig.WheelConfig config) {

        var groups = new List<RCCP_AIChangeGroup>();

        // Get current wheel values for comparison
        var currentAxles = vehicle?.GetComponentsInChildren<RCCP_Axle>();
        RCCP_WheelCollider firstWheel = null;
        if (currentAxles != null && currentAxles.Length > 0) {
            foreach (var axle in currentAxles) {
                if (axle.leftWheelCollider != null) {
                    firstWheel = axle.leftWheelCollider;
                    break;
                }
            }
        }

        // Base wheel settings
        bool hasBaseChanges = config.camber != 0 || config.caster != 0 || config.wheelWidth > 0 ||
                              (config.grip > 0 && config.grip != 1f) ||
                              RCCP_AIVehicleBuilder.HasMeaningfulValues(config.forwardFriction) ||
                              RCCP_AIVehicleBuilder.HasMeaningfulValues(config.sidewaysFriction);

        if (hasBaseChanges) {
            var wheels = new RCCP_AIChangeGroup("Wheels", "🛞");
            if (config.camber != 0) {
                string oldVal = firstWheel != null ? $"{firstWheel.camber:F1}°" : "0°";
                wheels.AddChange("Camber", oldVal, $"{config.camber:F1}°", "RCCP_WheelCollider");
            }
            if (config.caster != 0) {
                string oldVal = firstWheel != null ? $"{firstWheel.caster:F1}°" : "0°";
                wheels.AddChange("Caster", oldVal, $"{config.caster:F1}°", "RCCP_WheelCollider");
            }
            if (config.wheelWidth > 0) {
                string oldVal = firstWheel != null ? $"{firstWheel.width:F3}m" : "—";
                wheels.AddChange("Width", oldVal, $"{config.wheelWidth:F3}m", "RCCP_WheelCollider");
            }
            if (config.grip > 0 && config.grip != 1f) {
#if RCCP_V2_2_OR_NEWER
                string oldVal = firstWheel != null ? $"{firstWheel.grip:F2}" : "1.0";
#else
                string oldVal = "1.0"; // grip property not available in V2.0
#endif
                wheels.AddChange("Grip", oldVal, $"{config.grip:F2}", "RCCP_WheelCollider");
            }
            if (config.forwardFriction != null && config.forwardFriction.HasValues) {
                string frictionDesc = FormatFrictionCurve(config.forwardFriction);
                wheels.AddChange("Forward Friction", "Default", frictionDesc, "RCCP_WheelCollider");
            }
            if (config.sidewaysFriction != null && config.sidewaysFriction.HasValues) {
                string frictionDesc = FormatFrictionCurve(config.sidewaysFriction);
                wheels.AddChange("Sideways Friction", "Default", frictionDesc, "RCCP_WheelCollider");
            }
            if (wheels.HasChanges) groups.Add(wheels);
        }

        // Front axle overrides
        if (config.front != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.front)) {
            var front = new RCCP_AIChangeGroup("Front Axle", "🔹");
            if (config.front.camber != 0)
                front.AddChange("Camber", "0°", $"{config.front.camber:F1}°", "RCCP_WheelCollider_Front");
            if (config.front.caster != 0)
                front.AddChange("Caster", "0°", $"{config.front.caster:F1}°", "RCCP_WheelCollider_Front");
            if (config.front.wheelWidth > 0)
                front.AddChange("Width", "—", $"{config.front.wheelWidth:F3}m", "RCCP_WheelCollider_Front");
            if (config.front.grip > 0 && config.front.grip != 1f)
                front.AddChange("Grip", "1.0", $"{config.front.grip:F2}", "RCCP_WheelCollider_Front");
            if (config.front.forwardFriction != null && config.front.forwardFriction.HasValues)
                front.AddChange("Forward Friction", "Default", FormatFrictionCurve(config.front.forwardFriction), "RCCP_WheelCollider_Front");
            if (config.front.sidewaysFriction != null && config.front.sidewaysFriction.HasValues)
                front.AddChange("Sideways Friction", "Default", FormatFrictionCurve(config.front.sidewaysFriction), "RCCP_WheelCollider_Front");
            if (front.HasChanges) groups.Add(front);
        }

        // Rear axle overrides
        if (config.rear != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.rear)) {
            var rear = new RCCP_AIChangeGroup("Rear Axle", "🔸");
            if (config.rear.camber != 0)
                rear.AddChange("Camber", "0°", $"{config.rear.camber:F1}°", "RCCP_WheelCollider_Rear");
            if (config.rear.caster != 0)
                rear.AddChange("Caster", "0°", $"{config.rear.caster:F1}°", "RCCP_WheelCollider_Rear");
            if (config.rear.wheelWidth > 0)
                rear.AddChange("Width", "—", $"{config.rear.wheelWidth:F3}m", "RCCP_WheelCollider_Rear");
            if (config.rear.grip > 0 && config.rear.grip != 1f)
                rear.AddChange("Grip", "1.0", $"{config.rear.grip:F2}", "RCCP_WheelCollider_Rear");
            if (config.rear.forwardFriction != null && config.rear.forwardFriction.HasValues)
                rear.AddChange("Forward Friction", "Default", FormatFrictionCurve(config.rear.forwardFriction), "RCCP_WheelCollider_Rear");
            if (config.rear.sidewaysFriction != null && config.rear.sidewaysFriction.HasValues)
                rear.AddChange("Sideways Friction", "Default", FormatFrictionCurve(config.rear.sidewaysFriction), "RCCP_WheelCollider_Rear");
            if (rear.HasChanges) groups.Add(rear);
        }

        return groups;
    }

    /// <summary>
    /// Create review data specifically for Audio panel responses (AudioConfig format).
    /// </summary>
    private static RCCP_AIReviewData CreateFromAudioResponse(
        RCCP_CarController vehicle,
        string extracted,
        string modelUsed,
        float cost,
        int tokens) {

        RCCP_AIConfig.AudioConfig audioConfig;
        try {
            audioConfig = JsonUtility.FromJson<RCCP_AIConfig.AudioConfig>(extracted);
        } catch {
            return null;
        }

        if (audioConfig == null || audioConfig.engineSounds == null || audioConfig.engineSounds.Length == 0) {
            return null;
        }

        var data = new RCCP_AIReviewData {
            vehicleName = vehicle != null ? vehicle.name : "Vehicle",
            vehicleDimensions = GetVehicleDimensions(vehicle),
            modelUsed = modelUsed,
            estimatedCost = cost,
            tokenCount = tokens,
            rawJson = extracted,
            explanation = audioConfig.explanation
        };

        // Build audio-specific change groups
        data.changeGroups = BuildAudioChangeGroups(vehicle, audioConfig);

        return data;
    }

    /// <summary>
    /// Build change groups from AudioConfig (for Audio panel).
    /// </summary>
    private static List<RCCP_AIChangeGroup> BuildAudioChangeGroups(
        RCCP_CarController vehicle,
        RCCP_AIConfig.AudioConfig config) {

        var groups = new List<RCCP_AIChangeGroup>();

        if (config.engineSounds == null || config.engineSounds.Length == 0) {
            return groups;
        }

        // Get current audio component for comparison
        var currentAudio = vehicle?.GetComponentInChildren<RCCP_Audio>(true);

        foreach (var layer in config.engineSounds) {
            string layerName = layer.layerIndex switch {
                0 => "Layer 0 (Idle)",
                1 => "Layer 1 (Low)",
                2 => "Layer 2 (Mid)",
                3 => "Layer 3 (High)",
                _ => $"Layer {layer.layerIndex}"
            };

            var audioGroup = new RCCP_AIChangeGroup(layerName, "🔊");

            // Get current layer values if available
            RCCP_Audio.EngineSound currentLayer = null;
            if (currentAudio?.engineSounds != null && layer.layerIndex < currentAudio.engineSounds.Length) {
                currentLayer = currentAudio.engineSounds[layer.layerIndex];
            }

            if (layer.minRPM > 0 || layer.maxRPM > 0) {
                string oldRPM = currentLayer != null ? $"{currentLayer.minRPM:F0}-{currentLayer.maxRPM:F0}" : "—";
                audioGroup.AddChange("RPM Range", oldRPM, $"{layer.minRPM:F0}-{layer.maxRPM:F0}", "RCCP_Audio");
            }
            if (layer.minPitch > 0 || layer.maxPitch > 0) {
                string oldPitch = currentLayer != null ? $"{currentLayer.minPitch:F2}-{currentLayer.maxPitch:F2}" : "—";
                audioGroup.AddChange("Pitch", oldPitch, $"{layer.minPitch:F2}-{layer.maxPitch:F2}", "RCCP_Audio");
            }
            if (layer.maxVolume > 0) {
                string oldVol = currentLayer != null ? $"{currentLayer.maxVolume:F2}" : "—";
                audioGroup.AddChange("Volume", oldVol, $"{layer.maxVolume:F2}", "RCCP_Audio");
            }
            if (layer.minDistance > 0 || layer.maxDistance > 0) {
                string oldDist = currentLayer != null ? $"{currentLayer.minDistance:F0}-{currentLayer.maxDistance:F0}m" : "—";
                audioGroup.AddChange("3D Distance", oldDist, $"{layer.minDistance:F0}-{layer.maxDistance:F0}m", "RCCP_Audio");
            }
            if (layer.enabled == 0 || layer.enabled == 1) {
                string oldEnabled = currentLayer != null ? (currentLayer.maxVolume > 0 ? "On" : "Off") : "—";
                audioGroup.AddChange("Enabled", oldEnabled, layer.enabled == 1 ? "On" : "Off", "RCCP_Audio");
            }

            if (audioGroup.HasChanges) groups.Add(audioGroup);
        }

        return groups;
    }

    /// <summary>
    /// Create review data specifically for Lights panel responses (LightsConfig format).
    /// </summary>
    private static RCCP_AIReviewData CreateFromLightsResponse(
        RCCP_CarController vehicle,
        string extracted,
        string modelUsed,
        float cost,
        int tokens) {

        RCCP_AIConfig.LightsConfig lightsConfig;
        try {
            lightsConfig = JsonUtility.FromJson<RCCP_AIConfig.LightsConfig>(extracted);
        } catch {
            return null;
        }

        if (lightsConfig == null || lightsConfig.lights == null || lightsConfig.lights.Length == 0) {
            return null;
        }

        var data = new RCCP_AIReviewData {
            vehicleName = vehicle != null ? vehicle.name : "Vehicle",
            vehicleDimensions = GetVehicleDimensions(vehicle),
            modelUsed = modelUsed,
            estimatedCost = cost,
            tokenCount = tokens,
            rawJson = extracted,
            explanation = lightsConfig.explanation,
            parsedConfig = null  // Not using VehicleSetupConfig
        };

        data.changeGroups = BuildLightsChangeGroups(vehicle, lightsConfig);
        return data;
    }

    /// <summary>
    /// Build change groups from LightsConfig (for Lights panel).
    /// </summary>
    private static List<RCCP_AIChangeGroup> BuildLightsChangeGroups(
        RCCP_CarController vehicle,
        RCCP_AIConfig.LightsConfig config) {

        var groups = new List<RCCP_AIChangeGroup>();

        if (config.lights == null || config.lights.Length == 0) {
            return groups;
        }

        // Get current lights manager for comparison
        var currentLights = vehicle?.GetComponentInChildren<RCCP_Lights>(true);

        foreach (var light in config.lights) {
            if (light == null || string.IsNullOrEmpty(light.lightType)) continue;

            string lightName = light.lightType.Replace("_", " ");
            var lightGroup = new RCCP_AIChangeGroup(lightName, "💡");

            // Try to find current light of this type for comparison
            RCCP_Light currentLight = null;
            if (currentLights?.lights != null) {
                foreach (var cl in currentLights.lights) {
                    if (cl != null && cl.lightType.ToString() == light.lightType) {
                        currentLight = cl;
                        break;
                    }
                }
            }

            if (light.intensity > 0) {
                string oldIntensity = currentLight != null ? $"{currentLight.intensity:F1}" : "—";
                lightGroup.AddChange("Intensity", oldIntensity, $"{light.intensity:F1}", "RCCP_Light");
            }
            if (light.smoothness > 0) {
                string oldSmoothness = currentLight != null ? $"{currentLight.smoothness:F2}" : "—";
                lightGroup.AddChange("Smoothness", oldSmoothness, $"{light.smoothness:F2}", "RCCP_Light");
            }
            if (light.range > 0) {
                var unityLight = currentLight?.GetComponent<Light>();
                string oldRange = unityLight != null ? $"{unityLight.range:F0}m" : "—";
                lightGroup.AddChange("Range", oldRange, $"{light.range:F0}m", "Light");
            }
            if (light.spotAngle > 0) {
                var unityLight = currentLight?.GetComponent<Light>();
                string oldAngle = unityLight != null ? $"{unityLight.spotAngle:F0}°" : "—";
                lightGroup.AddChange("Spot Angle", oldAngle, $"{light.spotAngle:F0}°", "Light");
            }
            // Only show color if actually specified (not just default zeros from JsonUtility)
            if (light.lightColor != null && light.lightColor.IsSpecified) {
                var unityLight = currentLight?.GetComponent<Light>();
                string oldColor = unityLight != null ? $"{unityLight.color.r:F1}, {unityLight.color.g:F1}, {unityLight.color.b:F1}" : "—";
                string newColor = $"{light.lightColor.r:F1}, {light.lightColor.g:F1}, {light.lightColor.b:F1}";
                // Only add if actually different
                if (oldColor != newColor) {
                    lightGroup.AddChange("Color", oldColor, newColor, "Light");
                }
            }
            // Only show lens flares if explicitly specified (1 = disable, 2 = enable)
            // 0 = don't change (JsonUtility default), so ShouldModifyLensFlares won't be true
            if (light.ShouldModifyLensFlares) {
                bool newValue = light.useLensFlares == 2;  // 2 = enable
                bool currentValue = currentLight?.useLensFlares ?? false;
                // Only show if actually changing OR if no current light exists
                if (currentLight == null || newValue != currentValue) {
                    string oldFlares = currentLight != null ? (currentValue ? "On" : "Off") : "—";
                    lightGroup.AddChange("Lens Flares", oldFlares, newValue ? "On" : "Off", "RCCP_Light");
                }
            }
            // Only show breakable if explicitly specified (1 = not breakable, 2 = breakable)
            // 0 = don't change (JsonUtility default), so ShouldModifyBreakable won't be true
            if (light.ShouldModifyBreakable) {
                bool newValue = light.isBreakable == 2;  // 2 = breakable
                bool currentValue = currentLight?.isBreakable ?? false;
                // Only show if actually changing OR if no current light exists
                if (currentLight == null || newValue != currentValue) {
                    string oldBreakable = currentLight != null ? (currentValue ? "Yes" : "No") : "—";
                    lightGroup.AddChange("Breakable", oldBreakable, newValue ? "Yes" : "No", "RCCP_Light");
                }
            }
            if (light.flareBrightness > 0) {
                string oldFlare = currentLight != null ? $"{currentLight.flareBrightness:F1}" : "—";
                lightGroup.AddChange("Flare Brightness", oldFlare, $"{light.flareBrightness:F1}", "RCCP_Light");
            }

            if (lightGroup.HasChanges) groups.Add(lightGroup);
        }

        return groups;
    }

    /// <summary>
    /// Create review data specifically for Damage panel responses (DamageConfig format).
    /// </summary>
    private static RCCP_AIReviewData CreateFromDamageResponse(
        RCCP_CarController vehicle,
        string extracted,
        string modelUsed,
        float cost,
        int tokens) {

        RCCP_AIConfig.DamageConfig damageConfig;
        try {
            damageConfig = JsonUtility.FromJson<RCCP_AIConfig.DamageConfig>(extracted);
        } catch {
            return null;
        }

        if (damageConfig == null) {
            return null;
        }

        var data = new RCCP_AIReviewData {
            vehicleName = vehicle != null ? vehicle.name : "Vehicle",
            vehicleDimensions = GetVehicleDimensions(vehicle),
            modelUsed = modelUsed,
            estimatedCost = cost,
            tokenCount = tokens,
            rawJson = extracted,
            explanation = damageConfig.explanation,
            parsedConfig = null  // Not using VehicleSetupConfig
        };

        data.changeGroups = BuildDamageChangeGroups(vehicle, damageConfig);
        return data;
    }

    /// <summary>
    /// Build change groups from DamageConfig (for Damage panel).
    /// </summary>
    private static List<RCCP_AIChangeGroup> BuildDamageChangeGroups(
        RCCP_CarController vehicle,
        RCCP_AIConfig.DamageConfig config) {

        var groups = new List<RCCP_AIChangeGroup>();

        // Get current damage component for comparison
        var currentDamage = vehicle?.GetComponentInChildren<RCCP_Damage>(true);

        // Mesh Deformation group
        var meshGroup = new RCCP_AIChangeGroup("Mesh Deformation", "🔨");

        // meshDeformation is a bool - check if explicitly set by presence of other mesh settings
        bool hasMeshSettings = config.maximumDamage > 0 || config.deformationRadius > 0 ||
                               config.deformationMultiplier > 0 || config.automaticInstallation;
        if (hasMeshSettings || config.meshDeformation) {
            string oldEnabled = currentDamage != null ? (currentDamage.meshDeformation ? "On" : "Off") : "—";
            meshGroup.AddChange("Enabled", oldEnabled, config.meshDeformation ? "On" : "Off", "RCCP_Damage");
        }

        if (config.maximumDamage > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.maximumDamage:F2}" : "—";
            meshGroup.AddChange("Maximum Damage", oldVal, $"{config.maximumDamage:F2}", "RCCP_Damage");
        }

        if (config.deformationRadius > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.deformationRadius:F2}m" : "—";
            meshGroup.AddChange("Deformation Radius", oldVal, $"{config.deformationRadius:F2}m", "RCCP_Damage");
        }

        if (config.deformationMultiplier > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.deformationMultiplier:F2}x" : "—";
            meshGroup.AddChange("Deformation Multiplier", oldVal, $"{config.deformationMultiplier:F2}x", "RCCP_Damage");
        }

        if (meshGroup.HasChanges) groups.Add(meshGroup);

        // Wheel Damage group
        var wheelGroup = new RCCP_AIChangeGroup("Wheel Damage", "🛞");

        bool hasWheelSettings = config.wheelDamageRadius > 0 || config.wheelDamageMultiplier > 0;
        if (hasWheelSettings || config.wheelDamage) {
            string oldEnabled = currentDamage != null ? (currentDamage.wheelDamage ? "On" : "Off") : "—";
            wheelGroup.AddChange("Enabled", oldEnabled, config.wheelDamage ? "On" : "Off", "RCCP_Damage");
        }

        if (config.wheelDamageRadius > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.wheelDamageRadius:F2}m" : "—";
            wheelGroup.AddChange("Damage Radius", oldVal, $"{config.wheelDamageRadius:F2}m", "RCCP_Damage");
        }

        if (config.wheelDamageMultiplier > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.wheelDamageMultiplier:F2}x" : "—";
            wheelGroup.AddChange("Damage Multiplier", oldVal, $"{config.wheelDamageMultiplier:F2}x", "RCCP_Damage");
        }

        // wheelDetachment - check if there are wheel damage settings to know if it was specified
        if (hasWheelSettings) {
            string oldDetach = currentDamage != null ? (currentDamage.wheelDetachment ? "On" : "Off") : "—";
            wheelGroup.AddChange("Wheel Detachment", oldDetach, config.wheelDetachment ? "On" : "Off", "RCCP_Damage");
        }

        if (wheelGroup.HasChanges) groups.Add(wheelGroup);

        // Light Damage group
        var lightGroup = new RCCP_AIChangeGroup("Light Damage", "💡");

        bool hasLightSettings = config.lightDamageRadius > 0 || config.lightDamageMultiplier > 0;
        if (hasLightSettings || config.lightDamage) {
            string oldEnabled = currentDamage != null ? (currentDamage.lightDamage ? "On" : "Off") : "—";
            lightGroup.AddChange("Enabled", oldEnabled, config.lightDamage ? "On" : "Off", "RCCP_Damage");
        }

        if (config.lightDamageRadius > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.lightDamageRadius:F2}m" : "—";
            lightGroup.AddChange("Damage Radius", oldVal, $"{config.lightDamageRadius:F2}m", "RCCP_Damage");
        }

        if (config.lightDamageMultiplier > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.lightDamageMultiplier:F2}x" : "—";
            lightGroup.AddChange("Damage Multiplier", oldVal, $"{config.lightDamageMultiplier:F2}x", "RCCP_Damage");
        }

        if (lightGroup.HasChanges) groups.Add(lightGroup);

        // Part Damage group
        var partGroup = new RCCP_AIChangeGroup("Part Damage", "🔧");

        bool hasPartSettings = config.partDamageRadius > 0 || config.partDamageMultiplier > 0;
        if (hasPartSettings || config.partDamage) {
            string oldEnabled = currentDamage != null ? (currentDamage.partDamage ? "On" : "Off") : "—";
            partGroup.AddChange("Enabled", oldEnabled, config.partDamage ? "On" : "Off", "RCCP_Damage");
        }

        if (config.partDamageRadius > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.partDamageRadius:F2}m" : "—";
            partGroup.AddChange("Damage Radius", oldVal, $"{config.partDamageRadius:F2}m", "RCCP_Damage");
        }

        if (config.partDamageMultiplier > 0) {
            string oldVal = currentDamage != null ? $"{currentDamage.partDamageMultiplier:F2}x" : "—";
            partGroup.AddChange("Damage Multiplier", oldVal, $"{config.partDamageMultiplier:F2}x", "RCCP_Damage");
        }

        if (partGroup.HasChanges) groups.Add(partGroup);

        // Detachable Parts group (if any specified)
        if (config.detachableParts != null && config.detachableParts.Length > 0) {
            var detachGroup = new RCCP_AIChangeGroup("Detachable Parts", "📦");

            foreach (var part in config.detachableParts) {
                if (string.IsNullOrEmpty(part.meshName)) continue;

                string partName = !string.IsNullOrEmpty(part.partType)
                    ? $"{part.partType} ({part.meshName})"
                    : part.meshName;

                string details = "";
                if (part.strength > 0) details += $"str={part.strength:F0}";
                if (part.mass > 0) details += (details.Length > 0 ? ", " : "") + $"mass={part.mass:F0}";
                if (string.IsNullOrEmpty(details)) details = "Default";

                detachGroup.AddChange(partName, "—", details, "RCCP_DetachablePart");
            }

            if (detachGroup.HasChanges) groups.Add(detachGroup);
        }

        return groups;
    }

    /// <summary>
    /// Draw the Review & Apply panel. Returns true if Apply was clicked.
    /// </summary>
    public bool Draw() {
        if (reviewData == null) {
            EditorGUILayout.HelpBox("No review data available.", MessageType.Info);
            return false;
        }
        
        bool applyClicked = false;
        
        // Summary Bar
        DrawSummaryBar();
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        
        // AI Explanation (if any)
        if (!string.IsNullOrEmpty(reviewData.explanation)) {
            DrawExplanationBox();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        }
        
        // Change Groups
        DrawChangeGroups();
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        
        // JSON Preview (collapsible)
        DrawJsonPreview();
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);
        
        // Actions Footer
        applyClicked = DrawActionsFooter();
        
        return applyClicked;
    }
    
    /// <summary>
    /// Get list of enabled group names for selective application.
    /// </summary>
    public List<string> GetEnabledGroupNames() {
        if (reviewData == null) return new List<string>();
        
        return reviewData.changeGroups
            .Where(g => g.isEnabled && g.HasChanges)
            .Select(g => g.groupName)
            .ToList();
    }
    
    /// <summary>
    /// Check if a specific group is enabled.
    /// </summary>
    public bool IsGroupEnabled(string groupName) {
        var group = reviewData?.changeGroups.FirstOrDefault(g => g.groupName == groupName);
        return group?.isEnabled ?? false;
    }
    
    #endregion
    
    #region Drawing Methods
    
    private void DrawSummaryBar() {
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);
        
        // Top row: Vehicle info
        EditorGUILayout.BeginHorizontal();
        
        // Vehicle icon and name
        GUILayout.Label("🚗", GUILayout.Width(18));
        GUILayout.Label(reviewData.vehicleName, new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontStyle = FontStyle.Bold
        });
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);
        
        // Dimensions
        GUILayout.Label(reviewData.vehicleDimensions, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.TextSecondary }
        });
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space3);
        
        // Bottom row: Model, cost, changes count
        EditorGUILayout.BeginHorizontal();
        
        // Model
        DrawInfoChip("🤖", reviewData.modelUsed);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);

        // Cost - only show when using own API key (not server proxy)
        bool showCost = RCCP_AISettings.Instance != null && !RCCP_AISettings.Instance.useServerProxy;
        if (showCost) {
            DrawInfoChip("💰", $"~${reviewData.estimatedCost:F4}", DS.Warning);
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);
        }
        
        // Changes count
        var enabledCount = reviewData.TotalEnabledChanges;
        var totalCount = reviewData.TotalChanges;
        DrawInfoChip("📝", $"{enabledCount}/{totalCount} changes", enabledCount > 0 ? DS.Success : DS.TextSecondary);
        
        GUILayout.FlexibleSpace();
        
        // Undo available indicator
        DrawBadge("↩ Undo available", DS.Success);
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawExplanationBox() {
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);
        
        GUIStyle explanationStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            wordWrap = true,
            richText = true,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary },
            padding = new RectOffset(4, 4, 4, 4)
        };
        
        GUILayout.Label(reviewData.explanation, explanationStyle);
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawChangeGroups() {
        foreach (var group in reviewData.changeGroups) {
            if (!group.HasChanges) continue;  // Skip empty groups
            DrawChangeGroup(group);
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
        }
    }
    
    private void DrawChangeGroup(RCCP_AIChangeGroup group) {
        // Group container with left border
        Rect groupRect = EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelRecessed);
        
        // Draw colored left border
        Color borderColor = group.isEnabled ? DS.Accent : DS.BorderLight;
        EditorGUI.DrawRect(new Rect(groupRect.x, groupRect.y, 3, groupRect.height), borderColor);
        
        // Group Header
        EditorGUILayout.BeginHorizontal();
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        
        // Expand/Collapse arrow
        string arrowContent = group.isExpanded ? "▼" : "▶";
        GUIStyle arrowStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.TextSecondary },
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM
        };
        
        if (GUILayout.Button(arrowContent, arrowStyle, GUILayout.Width(14), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall))) {
            group.isExpanded = !group.isExpanded;
        }
        
        // Icon
        GUILayout.Label(group.icon, GUILayout.Width(18));
        
        // Group name
        GUIStyle nameStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontStyle = FontStyle.Bold,
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD
        };
        GUILayout.Label(group.groupName, nameStyle);
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        
        // Change count badge
        string changeText = group.ChangeCount == 1 ? "1 change" : $"{group.ChangeCount} changes";
        DrawBadge(changeText, DS.Accent);
        
        GUILayout.FlexibleSpace();
        
        // Enable/Disable checkbox
        bool newEnabled = EditorGUILayout.Toggle(group.isEnabled, GUILayout.Width(16));
        if (newEnabled != group.isEnabled) {
            group.isEnabled = newEnabled;
        }
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
        
        EditorGUILayout.EndHorizontal();
        
        // Group Content (when expanded)
        if (group.isExpanded && group.HasChanges) {
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
            
            EditorGUILayout.BeginVertical();
            
            // Draw each change
            var meaningfulChanges = group.changes.Where(c => c.HasChanged).ToList();
            for (int i = 0; i < meaningfulChanges.Count; i++) {
                DrawChangeRow(meaningfulChanges[i], i < meaningfulChanges.Count - 1);
            }
            
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawChangeRow(RCCP_AIPropertyChange change, bool showSeparator) {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space8);  // Indent
        
        // Property name
        GUIStyle propStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.TextSecondary },
            alignment = TextAnchor.MiddleLeft
        };
        GUILayout.Label(change.propertyName, propStyle, GUILayout.Width(130));
        
        GUILayout.FlexibleSpace();
        
        // Old value container
        Rect oldRect = GUILayoutUtility.GetRect(115, 18, GUILayout.Width(115));
        RCCP_AIDesignSystem.DrawRoundedRect(oldRect, RCCP_AIDesignSystem.Colors.DiffRemovedBg, 4);

        GUIStyle oldStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.DiffRemovedText },
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        GUI.Label(oldRect, change.oldValue, oldStyle);

        // Arrow
        GUIStyle arrowStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.Accent },
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("→", arrowStyle, GUILayout.Width(20));

        // New value container
        Rect newRect = GUILayoutUtility.GetRect(115, 18, GUILayout.Width(115));
        RCCP_AIDesignSystem.DrawRoundedRect(newRect, RCCP_AIDesignSystem.Colors.DiffAddedBg, 4);

        GUIStyle newStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.DiffAddedText },
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        GUI.Label(newRect, change.newValue, newStyle);
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        
        // Select button (if component type is available)
        if (!string.IsNullOrEmpty(change.componentType) && OnSelectComponent != null) {
            if (GUILayout.Button("↗", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(RCCP_AIDesignSystem.Heights.Pill))) {
                var component = OnSelectComponent(change.componentType);
                if (component != null) {
                    Selection.activeObject = component;
                    EditorGUIUtility.PingObject(component);
                }
            }
        } else {
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space7);
        }
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        EditorGUILayout.EndHorizontal();
        
        if (showSeparator) {
            Rect separatorRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            separatorRect.x += 28;
            separatorRect.width -= 40;
            EditorGUI.DrawRect(separatorRect, DS.Border);
        }
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
    }
    
    private void DrawJsonPreview() {
        EditorGUILayout.BeginHorizontal();
        showJsonPreview = EditorGUILayout.Foldout(showJsonPreview, "Raw JSON Response", true);
        EditorGUILayout.EndHorizontal();
        
        if (showJsonPreview && !string.IsNullOrEmpty(reviewData.rawJson)) {
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelRecessed);
            
            GUIStyle jsonStyle = new GUIStyle(EditorStyles.textArea) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
                wordWrap = true,
                richText = false,
                normal = { textColor = DS.TextSecondary }
            };
            
            // Limit display height
            string displayJson = reviewData.rawJson;
            if (displayJson.Length > 2000) {
                displayJson = displayJson.Substring(0, 2000) + "\n... (truncated)";
            }
            
            float height = Mathf.Min(200, displayJson.Split('\n').Length * 14 + 20);
            EditorGUILayout.SelectableLabel(displayJson, jsonStyle, GUILayout.Height(height));
            
            EditorGUILayout.EndVertical();
        }
    }
    
    private bool DrawActionsFooter() {
        bool applyClicked = false;
        
        // Separator
        Rect sepRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(sepRect, DS.BorderLight);
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        
        EditorGUILayout.BeginHorizontal();
        
        // Secondary actions (left side)
        if (GUILayout.Button("📋 Copy JSON", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            if (!string.IsNullOrEmpty(reviewData.rawJson)) {
                GUIUtility.systemCopyBuffer = reviewData.rawJson;
                OnCopyJson?.Invoke(reviewData.rawJson);
            }
        }
        
        if (GUILayout.Button("💾 Save Preset", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            OnSavePreset?.Invoke(reviewData);
        }
        
        if (GUILayout.Button("🔄 Regenerate", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            OnRegenerate?.Invoke();
        }
        
        if (GUILayout.Button("← Back", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            OnBack?.Invoke();
        }
        
        GUILayout.FlexibleSpace();
        
        // Primary action (right side)
        var enabledChanges = reviewData.TotalEnabledChanges;
        
        GUI.enabled = enabledChanges > 0;
        
        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = enabledChanges > 0 ? DS.Success : Color.gray;
        
        string buttonText = enabledChanges > 0 
            ? $"✓  Apply {enabledChanges} Changes" 
            : "No Changes Selected";
        
        if (GUILayout.Button(buttonText, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge), GUILayout.MinWidth(180))) {
            var enabledGroups = GetEnabledGroupNames();
            OnApplySelected?.Invoke(enabledGroups);
            applyClicked = true;
        }
        
        GUI.backgroundColor = originalBg;
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
        
        // Warning text
        GUIStyle warningStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.5f) }
        };
        GUILayout.Label("AI can make mistakes. Review changes carefully before applying.", warningStyle);
        
        return applyClicked;
    }
    
    #endregion
    
    #region Helper Methods
    
    private void DrawBadge(string text, Color color) {
        GUIStyle badgeStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            fontSize = RCCP_AIDesignSystem.Typography.SizeXS,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(6, 6, 2, 2),
            normal = { textColor = color }
        };
        
        GUIContent content = new GUIContent(text);
        Vector2 size = badgeStyle.CalcSize(content);
        
        Rect rect = GUILayoutUtility.GetRect(size.x + 8, 16);
        
        // Background
        EditorGUI.DrawRect(rect, RCCP_AIDesignSystem.Colors.WithAlpha(color, 0.15f));
        
        // Border
        float t = 1;
        Color borderColor = RCCP_AIDesignSystem.Colors.WithAlpha(color, 0.3f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), borderColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), borderColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), borderColor);
        EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), borderColor);
        
        // Text
        GUI.Label(rect, text, badgeStyle);
    }
    
    private void DrawInfoChip(string icon, string text, Color? textColor = null) {
        EditorGUILayout.BeginHorizontal();
        
        GUIStyle iconStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontSize = RCCP_AIDesignSystem.Typography.SizeSM };
        GUILayout.Label(icon, iconStyle, GUILayout.Width(14));
        
        GUIStyle textStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = textColor ?? DS.TextSecondary },
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM
        };
        GUILayout.Label(text, textStyle);
        
        EditorGUILayout.EndHorizontal();
    }
    
    private static string GetVehicleDimensions(RCCP_CarController vehicle) {
        if (vehicle == null) return "Unknown";

        Bounds localBounds = new Bounds();
        bool initialized = false;
        Matrix4x4 worldToLocal = vehicle.transform.worldToLocalMatrix;

        foreach (var renderer in vehicle.GetComponentsInChildren<Renderer>()) {
            Bounds meshBounds;
            Matrix4x4 meshToWorld;

            // Get mesh bounds - handle MeshFilter and SkinnedMeshRenderer
            MeshFilter mf = renderer.GetComponent<MeshFilter>();
            SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;

            if (mf != null && mf.sharedMesh != null) {
                meshBounds = mf.sharedMesh.bounds;
                meshToWorld = renderer.transform.localToWorldMatrix;
            } else if (smr != null && smr.sharedMesh != null) {
                meshBounds = smr.sharedMesh.bounds;
                meshToWorld = renderer.transform.localToWorldMatrix;
            } else {
                // Fallback: use renderer bounds corners for other renderer types
                meshBounds = renderer.bounds;
                meshToWorld = Matrix4x4.identity;
            }

            // Transform the 8 corners of the bounds to vehicle's local space
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            Vector3[] corners = new Vector3[8] {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners) {
                Vector3 worldPos = meshToWorld.MultiplyPoint3x4(corner);
                Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);

                if (!initialized) {
                    localBounds = new Bounds(localPos, Vector3.zero);
                    initialized = true;
                } else {
                    localBounds.Encapsulate(localPos);
                }
            }
        }

        return $"{localBounds.size.x:F2}m × {localBounds.size.y:F2}m × {localBounds.size.z:F2}m";
    }
    
    private static string ExtractJson(string response) {
        return RCCP_AIUtility.ExtractJson(response);
    }

    /// <summary>
    /// Format a friction curve config for display in the review panel.
    /// Shows key values in a very compact format to fit in narrow columns.
    /// Format: slip/val (extremum only, most important for grip feel)
    /// </summary>
    private static string FormatFrictionCurve(RCCP_AIConfig.FrictionCurveConfig config) {
        if (config == null) return "—";

        // Show extremum slip/value - the most important grip characteristic
        // Keep very short to fit in 95px column
        if (config.extremumSlip > 0 || config.extremumValue > 0) {
            return $"{config.extremumSlip:F2}/{config.extremumValue:F1}";
        }

        // Fallback to stiffness if only that's set
        if (config.stiffness > 0 && config.stiffness != 1f) {
            return $"×{config.stiffness:F2}";
        }

        return "Custom";
    }

    /// <summary>
    /// Parse JSON with all booleans initialized to true.
    /// Used to detect which booleans were explicitly set in JSON vs defaulted to false.
    /// </summary>
    private static RCCP_AIConfig.VehicleSetupConfig ParseWithAllBoolsTrue(string json) {
        var config = new RCCP_AIConfig.VehicleSetupConfig();
        // Pre-initialize all boolean-containing configs with booleans set to true
        config.stability = new RCCP_AIConfig.StabilityConfig {
            ABS = true, ESP = true, TCS = true,
            steeringHelper = true, tractionHelper = true, angularDragHelper = true
        };
        config.engine = new RCCP_AIConfig.EngineConfig {
            turboCharged = true
        };
        config.nos = new RCCP_AIConfig.NosConfig {
            enabled = true
        };
        config.input = new RCCP_AIConfig.InputConfig {
            counterSteering = true, steeringLimiter = true, autoReverse = true
        };
        // JsonUtility.FromJsonOverwrite will only overwrite fields that exist in the JSON
        JsonUtility.FromJsonOverwrite(json, config);
        return config;
    }

    /// <summary>
    /// Check if a boolean was explicitly set in JSON (not just defaulted to false).
    /// Logic: If value is true, it was explicitly set. If value is false AND allTrueValue is also false,
    /// it means the JSON contained an explicit "false" that overwrote our "true" initialization.
    /// </summary>
    private static bool WasExplicitlySet(bool value, bool allTrueValue) {
        return value || !allTrueValue;
    }

    /// <summary>
    /// Build change groups from the parsed config.
    /// </summary>
    private static List<RCCP_AIChangeGroup> BuildChangeGroups(
        RCCP_CarController vehicle,
        RCCP_AIConfig.VehicleSetupConfig config,
        RCCP_AIConfig.VehicleSetupConfig configAllTrue = null) {

        var groups = new List<RCCP_AIChangeGroup>();

        // Get current values for comparison (if vehicle exists)
        var currentEngine = vehicle?.GetComponentInChildren<RCCP_Engine>();
        var currentGearbox = vehicle?.GetComponentInChildren<RCCP_Gearbox>();
        var currentClutch = vehicle?.GetComponentInChildren<RCCP_Clutch>();
        var currentDiff = vehicle?.GetComponentInChildren<RCCP_Differential>();
        var currentStability = vehicle?.GetComponentInChildren<RCCP_Stability>();
        var currentRb = vehicle?.GetComponent<Rigidbody>();
        var currentAero = vehicle?.GetComponentInChildren<RCCP_AeroDynamics>();
        var currentAxles = vehicle?.GetComponentsInChildren<RCCP_Axle>();
        var currentInput = vehicle?.GetComponentInChildren<RCCP_Input>();
        var currentFuelTank = vehicle?.GetComponentInChildren<RCCP_FuelTank>(true);

        // Get front/rear axles by comparing wheel collider Z positions
        // (higher Z = front, lower Z = rear in local space)
        RCCP_Axle frontAxle = null;
        RCCP_Axle rearAxle = null;
        WheelCollider firstWheelCollider = null;
        if (currentAxles != null && currentAxles.Length > 0 && vehicle != null) {
            float highestZ = float.MinValue;
            float lowestZ = float.MaxValue;

            foreach (var axle in currentAxles) {
                if (axle == null || axle.leftWheelCollider == null) continue;
                float wheelZ = vehicle.transform.InverseTransformPoint(
                    axle.leftWheelCollider.transform.position).z;
                if (wheelZ > highestZ) {
                    highestZ = wheelZ;
                    frontAxle = axle;
                }
                if (wheelZ < lowestZ) {
                    lowestZ = wheelZ;
                    rearAxle = axle;
                }
            }
            // Get first wheel collider for suspension spring/damper
            if (frontAxle?.leftWheelCollider != null) {
                firstWheelCollider = frontAxle.leftWheelCollider.GetComponent<WheelCollider>();
            }
        }

        // === VEHICLE / CHASSIS ===
        var chassis = new RCCP_AIChangeGroup("Chassis", "🚗");
        if (config.vehicleConfig != null) {
            if (config.vehicleConfig.mass > 0) {
                string oldMass = currentRb != null ? $"{currentRb.mass:F0} kg" : "—";
                chassis.AddChange("Mass", oldMass, $"{config.vehicleConfig.mass:F0} kg", "Rigidbody");
            }
            if (config.vehicleConfig.centerOfMassOffset != null && !config.vehicleConfig.centerOfMassOffset.IsZero) {
                string oldCOM = "—";
                if (currentAero?.COM != null) {
                    var comPos = currentAero.COM.localPosition;
                    oldCOM = $"{comPos.x:F2}, {comPos.y:F2}, {comPos.z:F2}";
                }
                chassis.AddChange("Center of Mass", oldCOM,
                    $"{config.vehicleConfig.centerOfMassOffset.x:F2}, {config.vehicleConfig.centerOfMassOffset.y:F2}, {config.vehicleConfig.centerOfMassOffset.z:F2}",
                    "RCCP_CarController");
            }
        }
        if (!string.IsNullOrEmpty(config.driveType)) {
            string oldDrive = currentDiff != null ? currentDiff.differentialType.ToString() : "—";
            chassis.AddChange("Drive Type", oldDrive, config.driveType, "RCCP_Differential");
        }
        if (chassis.HasChanges) groups.Add(chassis);
        
        // === ENGINE ===
        var engine = new RCCP_AIChangeGroup("Engine", "⚙️");
        if (config.engine != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.engine)) {
            if (config.engine.maximumTorqueAsNM > 0) {
                string oldTorque = currentEngine != null ? $"{currentEngine.maximumTorqueAsNM:F0} Nm" : "—";
                engine.AddChange("Max Torque", oldTorque, $"{config.engine.maximumTorqueAsNM:F0} Nm", "RCCP_Engine");
            }
            if (config.engine.maxEngineRPM > 0) {
                string oldRpm = currentEngine != null ? $"{currentEngine.maxEngineRPM:F0}" : "—";
                engine.AddChange("Max RPM", oldRpm, $"{config.engine.maxEngineRPM:F0}", "RCCP_Engine");
            }
            if (config.engine.maximumSpeed > 0) {
                string oldSpeed = currentEngine != null ? $"{currentEngine.maximumSpeed:F0} km/h" : "—";
                engine.AddChange("Max Speed", oldSpeed, $"{config.engine.maximumSpeed:F0} km/h", "RCCP_Engine");
            }
            var engineAllTrue = configAllTrue?.engine;
            if (engineAllTrue != null && WasExplicitlySet(config.engine.turboCharged, engineAllTrue.turboCharged)) {
                string oldTurbo = currentEngine != null && currentEngine.turboCharged ? "On" : "Off";
                engine.AddChange("Turbo", oldTurbo, config.engine.turboCharged ? $"On ({config.engine.maxTurboChargePsi:F1} psi)" : "Off", "RCCP_Engine");
            }
            if (config.engine.engineInertia > 0) {
                string oldInertia = currentEngine != null ? $"{currentEngine.engineInertia:F3}" : "—";
                engine.AddChange("Engine Inertia", oldInertia, $"{config.engine.engineInertia:F3}", "RCCP_Engine");
            }
        }
        if (engine.HasChanges) groups.Add(engine);
        
        // === GEARBOX ===
        var gearbox = new RCCP_AIChangeGroup("Gearbox", "🔧");
        if (config.gearbox != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.gearbox)) {
            if (!string.IsNullOrEmpty(config.gearbox.transmissionType)) {
                string oldTrans = currentGearbox != null ? currentGearbox.transmissionType.ToString() : "—";
                gearbox.AddChange("Transmission", oldTrans, config.gearbox.transmissionType, "RCCP_Gearbox");
            }
            if (config.gearbox.gearRatios != null && config.gearbox.gearRatios.Length > 0) {
                string oldGears = currentGearbox?.gearRatios != null ? $"{currentGearbox.gearRatios.Length}" : "—";
                gearbox.AddChange("Gear Count", oldGears, $"{config.gearbox.gearRatios.Length}", "RCCP_Gearbox");
            }
            if (config.gearbox.shiftingTime > 0) {
                string oldShift = currentGearbox != null ? $"{currentGearbox.shiftingTime:F2}s" : "—";
                gearbox.AddChange("Shift Time", oldShift, $"{config.gearbox.shiftingTime:F2}s", "RCCP_Gearbox");
            }
        }
        if (gearbox.HasChanges) groups.Add(gearbox);

        // === CLUTCH ===
        var clutch = new RCCP_AIChangeGroup("Clutch", "⚡");
        if (config.clutch != null) {
            if (config.clutch.clutchInertia > 0) {
                string oldInertia = currentClutch != null ? $"{currentClutch.clutchInertia:F3}" : "—";
                clutch.AddChange("Clutch Inertia", oldInertia, $"{config.clutch.clutchInertia:F3}", "RCCP_Clutch");
            }
            if (config.clutch.engageRPM > 0) {
                string oldEngageRPM = currentClutch != null ? $"{currentClutch.engageRPM:F0}" : "—";
                clutch.AddChange("Engage RPM", oldEngageRPM, $"{config.clutch.engageRPM:F0}", "RCCP_Clutch");
            }
        }
        if (clutch.HasChanges) groups.Add(clutch);

        // === DIFFERENTIAL ===
        var diff = new RCCP_AIChangeGroup("Differential", "🔩");
        if (config.differential != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.differential)) {
            if (config.differential.finalDriveRatio > 0) {
                string oldFinal = currentDiff != null ? $"{currentDiff.finalDriveRatio:F2}" : "—";
                diff.AddChange("Final Drive", oldFinal, $"{config.differential.finalDriveRatio:F2}", "RCCP_Differential");
            }
            if (config.differential.limitedSlipRatio > 0) {
                string oldLSD = currentDiff != null ? $"{currentDiff.limitedSlipRatio:F0}%" : "—";
                diff.AddChange("Limited Slip", oldLSD, $"{config.differential.limitedSlipRatio:F0}%", "RCCP_Differential");
            }
        }
        if (diff.HasChanges) groups.Add(diff);

        // === SUSPENSION ===
        var suspension = new RCCP_AIChangeGroup("Suspension", "🛞");
        if (config.suspension != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.suspension)) {
            if (config.suspension.spring > 0) {
                string oldSpring = firstWheelCollider != null ? $"{firstWheelCollider.suspensionSpring.spring:F0}" : "—";
                suspension.AddChange("Spring Force", oldSpring, $"{config.suspension.spring:F0}", "RCCP_Axle");
            }
            if (config.suspension.damper > 0) {
                string oldDamper = firstWheelCollider != null ? $"{firstWheelCollider.suspensionSpring.damper:F0}" : "—";
                suspension.AddChange("Damper Force", oldDamper, $"{config.suspension.damper:F0}", "RCCP_Axle");
            }
            if (config.suspension.distance > 0) {
                string oldDistance = firstWheelCollider != null ? $"{firstWheelCollider.suspensionDistance:F3}m" : "—";
                suspension.AddChange("Travel Distance", oldDistance, $"{config.suspension.distance:F3}m", "RCCP_Axle");
            }
        }
        if (config.axles != null) {
            if (config.axles.front != null) {
                if (config.axles.front.maxSteerAngle > 0) {
                    string oldSteer = frontAxle != null ? $"{frontAxle.maxSteerAngle:F0}°" : "—";
                    suspension.AddChange("Front Steer Angle", oldSteer, $"{config.axles.front.maxSteerAngle:F0}°", "RCCP_Axle_Front");
                }
                if (config.axles.front.antirollForce > 0) {
                    string oldAntiroll = frontAxle != null ? $"{frontAxle.antirollForce:F0}" : "—";
                    suspension.AddChange("Front Anti-Roll", oldAntiroll, $"{config.axles.front.antirollForce:F0}", "RCCP_Axle_Front");
                }
            }
            if (config.axles.rear != null) {
                if (config.axles.rear.maxBrakeTorque > 0) {
                    string oldBrake = rearAxle != null ? $"{rearAxle.maxBrakeTorque:F0}" : "—";
                    suspension.AddChange("Rear Brake Torque", oldBrake, $"{config.axles.rear.maxBrakeTorque:F0}", "RCCP_Axle_Rear");
                }
            }
        }
        if (suspension.HasChanges) groups.Add(suspension);
        
        // === STABILITY ===
        var stability = new RCCP_AIChangeGroup("Stability", "🎯");
        if (config.stability != null) {
            // Use configAllTrue to detect which booleans were EXPLICITLY set in JSON
            var stabilityAllTrue = configAllTrue?.stability;

            // Only add ABS change if it was explicitly set in the JSON
            if (stabilityAllTrue != null && WasExplicitlySet(config.stability.ABS, stabilityAllTrue.ABS)) {
                string oldABS = currentStability != null ? (currentStability.ABS ? "On" : "Off") : "—";
                stability.AddChange("ABS", oldABS, config.stability.ABS ? "On" : "Off", "RCCP_Stability");
            }

            // Only add ESP change if it was explicitly set in the JSON
            if (stabilityAllTrue != null && WasExplicitlySet(config.stability.ESP, stabilityAllTrue.ESP)) {
                string oldESP = currentStability != null ? (currentStability.ESP ? "On" : "Off") : "—";
                stability.AddChange("ESP", oldESP, config.stability.ESP ? "On" : "Off", "RCCP_Stability");
            }

            // Only add TCS change if it was explicitly set in the JSON
            if (stabilityAllTrue != null && WasExplicitlySet(config.stability.TCS, stabilityAllTrue.TCS)) {
                string oldTCS = currentStability != null ? (currentStability.TCS ? "On" : "Off") : "—";
                stability.AddChange("TCS", oldTCS, config.stability.TCS ? "On" : "Off", "RCCP_Stability");
            }

            // Only add helper changes if they were explicitly set in the JSON
            if (stabilityAllTrue != null && WasExplicitlySet(config.stability.steeringHelper, stabilityAllTrue.steeringHelper)) {
                string oldHelper = currentStability != null ? (currentStability.steeringHelper ? "On" : "Off") : "—";
                stability.AddChange("Steering Helper", oldHelper, config.stability.steeringHelper ? "On" : "Off", "RCCP_Stability");
            }

            if (stabilityAllTrue != null && WasExplicitlySet(config.stability.tractionHelper, stabilityAllTrue.tractionHelper)) {
                string oldHelper = currentStability != null ? (currentStability.tractionHelper ? "On" : "Off") : "—";
                stability.AddChange("Traction Helper", oldHelper, config.stability.tractionHelper ? "On" : "Off", "RCCP_Stability");
            }

            if (stabilityAllTrue != null && WasExplicitlySet(config.stability.angularDragHelper, stabilityAllTrue.angularDragHelper)) {
                string oldHelper = currentStability != null ? (currentStability.angularDragHelper ? "On" : "Off") : "—";
                stability.AddChange("Angular Drag Helper", oldHelper, config.stability.angularDragHelper ? "On" : "Off", "RCCP_Stability");
            }

            // Strength values - these still use > 0 check since 0 is the default/unset value
            if (config.stability.steerHelperStrength > 0) {
                string oldSteerStr = currentStability != null ? $"{currentStability.steerHelperStrength:F2}" : "—";
                stability.AddChange("Steer Helper Strength", oldSteerStr, $"{config.stability.steerHelperStrength:F2}", "RCCP_Stability");
            }
            if (config.stability.tractionHelperStrength > 0) {
                string oldTractStr = currentStability != null ? $"{currentStability.tractionHelperStrength:F2}" : "—";
                stability.AddChange("Traction Helper Strength", oldTractStr, $"{config.stability.tractionHelperStrength:F2}", "RCCP_Stability");
            }
            if (config.stability.angularDragHelperStrength > 0) {
                string oldAngStr = currentStability != null ? $"{currentStability.angularDragHelperStrength:F2}" : "—";
                stability.AddChange("Angular Drag Helper Strength", oldAngStr, $"{config.stability.angularDragHelperStrength:F2}", "RCCP_Stability");
            }
        }
        if (stability.HasChanges) groups.Add(stability);

        // === AERODYNAMICS ===
        var aero = new RCCP_AIChangeGroup("Aerodynamics", "💨");
        if (config.aeroDynamics != null) {
            if (config.aeroDynamics.downForce > 0) {
                string oldDownforce = currentAero != null ? $"{currentAero.downForce:F0}" : "—";
                aero.AddChange("Downforce", oldDownforce, $"{config.aeroDynamics.downForce:F0}", "RCCP_AeroDynamics");
            }
            if (config.aeroDynamics.airResistance > 0) {
                string oldAirRes = currentAero != null ? $"{currentAero.airResistance:F2}" : "—";
                aero.AddChange("Air Resistance", oldAirRes, $"{config.aeroDynamics.airResistance:F2}", "RCCP_AeroDynamics");
            }
        }
        if (aero.HasChanges) groups.Add(aero);

        // === WHEEL FRICTION ===
        var friction = new RCCP_AIChangeGroup("Tires", "🏎️");
        if (config.wheelFriction != null) {
            if (!string.IsNullOrEmpty(config.wheelFriction.type)) {
                friction.AddChange("Tire Type", "—", config.wheelFriction.type, "RCCP_WheelCollider");
            }
            if (config.wheelFriction.forward != null && config.wheelFriction.forward.HasValues) {
                string oldFwd = firstWheelCollider != null ? $"Ext:{firstWheelCollider.forwardFriction.extremumSlip:F2}" : "—";
                friction.AddChange("Forward Grip", oldFwd, $"Ext:{config.wheelFriction.forward.extremumSlip:F2}", "RCCP_WheelCollider");
            }
            if (config.wheelFriction.sideways != null && config.wheelFriction.sideways.HasValues) {
                string oldSide = firstWheelCollider != null ? $"Ext:{firstWheelCollider.sidewaysFriction.extremumSlip:F2}" : "—";
                friction.AddChange("Sideways Grip", oldSide, $"Ext:{config.wheelFriction.sideways.extremumSlip:F2}", "RCCP_WheelCollider");
            }
        }
        if (friction.HasChanges) groups.Add(friction);

        // === ADD-ONS ===
        var addons = new RCCP_AIChangeGroup("Add-ons", "🔧");
        if (config.nos != null) {
            var nosAllTrue = configAllTrue?.nos;
            if (nosAllTrue != null && WasExplicitlySet(config.nos.enabled, nosAllTrue.enabled)) {
                var currentNos = vehicle?.GetComponentInChildren<RCCP_Nos>(true);
                string oldNos = currentNos != null && currentNos.enabled ? "On" : "Off";
                addons.AddChange("NOS", oldNos, config.nos.enabled ? $"On (×{config.nos.torqueMultiplier:F1})" : "Off", "RCCP_Nos");
            }
        }
        if (config.fuelTank != null && config.fuelTank.enabled && config.fuelTank.fuelTankCapacity > 0) {
            string oldFuel = currentFuelTank != null ? $"{currentFuelTank.fuelTankCapacity:F0}L" : "—";
            addons.AddChange("Fuel Tank", oldFuel, $"{config.fuelTank.fuelTankCapacity:F0}L", "RCCP_FuelTank");
        }
        if (config.limiter != null && config.limiter.enabled) {
            var currentLimiter = vehicle?.GetComponentInChildren<RCCP_Limiter>(true);
            string oldLimiter = currentLimiter != null && currentLimiter.enabled ? "On" : "Off";
            addons.AddChange("Speed Limiter", oldLimiter, "On", "RCCP_Limiter");
        }
        if (addons.HasChanges) groups.Add(addons);

        // === INPUT ===
        var input = new RCCP_AIChangeGroup("Input", "🎮");
        if (config.input != null) {
            if (config.input.counterSteerFactor > 0) {
                string oldCounterFactor = currentInput != null ? $"{currentInput.counterSteerFactor:F2}" : "—";
                input.AddChange("Counter Steer", oldCounterFactor, $"{config.input.counterSteerFactor:F2}", "RCCP_Input");
            }

            var inputAllTrue = configAllTrue?.input;
            if (inputAllTrue != null) {
                if (WasExplicitlySet(config.input.counterSteering, inputAllTrue.counterSteering)) {
                    string oldVal = currentInput != null && currentInput.counterSteering ? "On" : "Off";
                    input.AddChange("Counter Steering", oldVal, config.input.counterSteering ? "On" : "Off", "RCCP_Input");
                }
                if (WasExplicitlySet(config.input.steeringLimiter, inputAllTrue.steeringLimiter)) {
                    string oldVal = currentInput != null && currentInput.steeringLimiter ? "On" : "Off";
                    input.AddChange("Steering Limiter", oldVal, config.input.steeringLimiter ? "On" : "Off", "RCCP_Input");
                }
                if (WasExplicitlySet(config.input.autoReverse, inputAllTrue.autoReverse)) {
                    string oldVal = currentInput != null && currentInput.autoReverse ? "On" : "Off";
                    input.AddChange("Auto Reverse", oldVal, config.input.autoReverse ? "On" : "Off", "RCCP_Input");
                }
            }
        }
        if (input.HasChanges) groups.Add(input);
        
        return groups;
    }
    
    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
