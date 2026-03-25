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
/// Partial class containing audio component configuration methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Audio Settings

    /// <summary>
    /// Applies audio settings to a vehicle. Creates RCCP_Audio component if it doesn't exist.
    /// Used by both the Audio panel and history restore.
    /// </summary>
    /// <param name="carController">The vehicle to configure</param>
    /// <param name="config">Audio configuration from AI response</param>
    /// <param name="engineMinRPM">Optional override for engine min RPM (for validation)</param>
    /// <param name="engineMaxRPM">Optional override for engine max RPM (for validation)</param>
    /// <returns>Number of engine sound layers modified</returns>
    public static int ApplyAudioSettings(
        RCCP_CarController carController,
        RCCP_AIConfig.AudioConfig config,
        float? engineMinRPM = null,
        float? engineMaxRPM = null) {

        if (carController == null) {
            Debug.LogError("[RCCP AI] Cannot apply audio: CarController is null");
            return 0;
        }

        if (config == null || config.engineSounds == null || config.engineSounds.Length == 0) {
            if (VerboseLogging) Debug.Log("[RCCP AI] No audio configuration to apply");
            return 0;
        }

        // Get or create audio component
        var audioComponent = carController.GetComponentInChildren<RCCP_Audio>(true);

        if (audioComponent == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] Creating Audio component...");
            RCCP_CreateNewVehicle.AddAudio(carController);
            audioComponent = carController.GetComponentInChildren<RCCP_Audio>(true);

            if (audioComponent == null) {
                Debug.LogError("[RCCP AI] Failed to create RCCP_Audio component");
                return 0;
            }
        }

        Undo.RecordObject(audioComponent, "RCCP AI Audio");

        // If user is configuring this component, they want it enabled
        if (!audioComponent.enabled) {
            audioComponent.enabled = true;
            if (VerboseLogging) Debug.Log("[RCCP AI] Audio component was disabled, enabling it for configuration");
        }

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.audio;

        // Determine engine RPM range for validation
        float minRPM = engineMinRPM ?? defaults?.engineMinRPM ?? 750f;
        float maxRPM = engineMaxRPM ?? defaults?.engineMaxRPM ?? 7000f;

        // If no overrides provided, try to read from engine component
        if (!engineMinRPM.HasValue || !engineMaxRPM.HasValue) {
            var engineComponent = carController.GetComponentInChildren<RCCP_Engine>(true);
            if (engineComponent != null) {
                minRPM = engineComponent.minEngineRPM;
                maxRPM = engineComponent.maxEngineRPM;
                if (VerboseLogging)
                    Debug.Log($"[RCCP AI] Engine RPM range: {minRPM}-{maxRPM}");
            } else {
                if (VerboseLogging)
                    Debug.LogWarning("[RCCP AI] No RCCP_Engine found, using default RPM range for audio validation");
            }
        }

        int layersModified = 0;

        // Ensure the audio component has an engine sounds array
        if (audioComponent.engineSounds == null || audioComponent.engineSounds.Length == 0) {
            // Initialize with default array size (typically 4 layers)
            int layerCount = Mathf.Min(config.engineSounds.Length, 4);
            audioComponent.engineSounds = new RCCP_Audio.EngineSound[layerCount];
            for (int i = 0; i < layerCount; i++) {
                audioComponent.engineSounds[i] = new RCCP_Audio.EngineSound();
            }
        }

        // Apply each layer configuration
        for (int i = 0; i < config.engineSounds.Length; i++) {
            var srcLayer = config.engineSounds[i];
            if (srcLayer == null) continue;

            // Determine target layer index (use layerIndex if specified, otherwise sequential)
            int targetIndex = srcLayer.layerIndex > 0 ? srcLayer.layerIndex : i;
            if (targetIndex >= audioComponent.engineSounds.Length) continue;

            var dstLayer = audioComponent.engineSounds[targetIndex];
            if (dstLayer == null) continue;

            // Handle enable/disable (set maxVolume to 0 to effectively mute)
            if (srcLayer.ShouldDisable) {
                dstLayer.maxVolume = 0f;
                layersModified++;
                continue;  // Skip other settings if disabling
            }

            // Apply RPM range - clamp to engine's actual RPM range
            if (srcLayer.minRPM > 0 || srcLayer.maxRPM > 0) {
                float clampedMinRPM = Mathf.Clamp(srcLayer.minRPM, 0f, maxRPM);
                float clampedMaxRPM = Mathf.Clamp(srcLayer.maxRPM, clampedMinRPM + 100f, maxRPM);

                // Log if values were clamped
                if (VerboseLogging && (clampedMaxRPM != srcLayer.maxRPM || clampedMinRPM != srcLayer.minRPM)) {
                    Debug.Log($"[RCCP AI] Layer {targetIndex} RPM clamped to engine range: {srcLayer.minRPM}-{srcLayer.maxRPM} -> {clampedMinRPM}-{clampedMaxRPM}");
                }

                dstLayer.minRPM = clampedMinRPM;
                dstLayer.maxRPM = clampedMaxRPM;
            }

            // Apply pitch range
            if (srcLayer.minPitch > 0)
                dstLayer.minPitch = Mathf.Clamp(srcLayer.minPitch, 0.1f, 3f);
            else
                dstLayer.minPitch = defaults?.engineMinPitch ?? 0.1f;
            if (srcLayer.maxPitch > 0)
                dstLayer.maxPitch = Mathf.Clamp(srcLayer.maxPitch, dstLayer.minPitch, 3f);
            else
                dstLayer.maxPitch = defaults?.engineMaxPitch ?? 1f;

            // Apply volume (if enabling, ensure non-zero; if just updating, use the provided value)
            if (srcLayer.ShouldEnable && srcLayer.maxVolume <= 0) {
                // Enabling but no volume specified - use default
                dstLayer.maxVolume = defaults?.engineMaxVolume ?? 1f;
            } else if (srcLayer.maxVolume > 0) {
                dstLayer.maxVolume = Mathf.Clamp(srcLayer.maxVolume, 0.1f, 2f);
            }

            // Apply 3D audio distance
            if (srcLayer.minDistance > 0)
                dstLayer.minDistance = Mathf.Clamp(srcLayer.minDistance, 1f, 50f);
            else
                dstLayer.minDistance = defaults?.engineMinDistance ?? 10f;
            if (srcLayer.maxDistance > 0)
                dstLayer.maxDistance = Mathf.Clamp(srcLayer.maxDistance, dstLayer.minDistance + 10f, 500f);
            else
                dstLayer.maxDistance = defaults?.engineMaxDistance ?? 200f;

            layersModified++;
        }

        EditorUtility.SetDirty(audioComponent);

        if (VerboseLogging && layersModified > 0)
            Debug.Log($"[RCCP AI] Audio settings applied ({layersModified} layers modified)");

        return layersModified;
    }

    /// <summary>
    /// Apply audio settings during restore operation.
    /// Restores engine sound layer settings to previously captured values.
    /// </summary>
    private static void ApplyAudioSettingsForRestore(RCCP_CarController carController, RCCP_AIConfig.AudioConfig config) {
        if (config == null || config.engineSounds == null) return;

        var audio = carController.GetComponentInChildren<RCCP_Audio>(true);
        if (audio == null || audio.engineSounds == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI Restore] No audio component found, skipping audio restore");
            return;
        }

        Undo.RecordObject(audio, "RCCP AI Restore Audio");

        foreach (var soundConfig in config.engineSounds) {
            if (soundConfig.layerIndex >= 0 && soundConfig.layerIndex < audio.engineSounds.Length) {
                var layer = audio.engineSounds[soundConfig.layerIndex];
                if (layer != null) {
                    layer.minRPM = soundConfig.minRPM;
                    layer.maxRPM = soundConfig.maxRPM;
                    layer.minPitch = soundConfig.minPitch;
                    layer.maxPitch = soundConfig.maxPitch;
                    layer.maxVolume = soundConfig.maxVolume;
                    layer.minDistance = soundConfig.minDistance;
                    layer.maxDistance = soundConfig.maxDistance;

                    // EngineSound has audioSourceOn and audioSourceOff
                    if (soundConfig.ShouldModifyEnabled) {
                        if (layer.audioSourceOn != null) {
                            layer.audioSourceOn.enabled = soundConfig.ShouldEnable;
                        }
                        if (layer.audioSourceOff != null) {
                            layer.audioSourceOff.enabled = soundConfig.ShouldEnable;
                        }
                    }
                }
            }
        }

        EditorUtility.SetDirty(audio);
        if (VerboseLogging) Debug.Log($"[RCCP AI Restore] Audio settings restored ({config.engineSounds.Length} layers)");
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
