//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Data classes for parsing AI-generated vehicle configurations.
/// These classes mirror the JSON structure returned by the AI.
/// </summary>
public static class RCCP_AIConfig {

    /// <summary>
    /// Prompt mode determines how the AI should interpret and respond to user input.
    /// </summary>
    public enum PromptMode {
        /// <summary>User wants to configure/change vehicle settings. AI returns JSON.</summary>
        Request,
        /// <summary>User is asking a question or seeking explanation. AI returns text.</summary>
        Ask
    }

    /// <summary>
    /// Response returned when the AI rejects an invalid/nonsense request.
    /// The AI returns this instead of a vehicle config when the input is gibberish.
    /// </summary>
    [Serializable]
    public class RejectionResponse {
        public bool rejected;
        public string reason;
        public string[] suggestions;
    }

    [Serializable]
    public class VehicleSetupConfig {
        public VehicleConfig vehicleConfig;
        public EngineConfig engine;
        public ClutchConfig clutch;
        public GearboxConfig gearbox;
        public DifferentialConfig differential;
        public string driveType;                      // FWD, RWD, AWD
        public AxlesConfig axles;
        public SuspensionConfig suspension;
        public SuspensionConfig frontSuspension;      // Per-axle suspension (for restore)
        public SuspensionConfig rearSuspension;       // Per-axle suspension (for restore)
        public WheelFrictionConfig wheelFriction;
        public WheelFrictionConfig frontWheelFriction; // Per-axle friction (for restore)
        public WheelFrictionConfig rearWheelFriction;  // Per-axle friction (for restore)
        public WheelConfig wheels;                    // Wheel dimensions and alignment
        public StabilityConfig stability;
        public AeroDynamicsConfig aeroDynamics;
        public NosConfig nos;
        public FuelTankConfig fuelTank;
        public LimiterConfig limiter;
        public InputConfig input;
        public RecorderConfig recorder;               // Recorder component (remove only)
        public TrailerAttacherConfig trailerAttacher; // Trailer attacher component (remove only)
        public CustomizerConfig customizer;           // Customizer component (save/load system)
        public AudioConfig audio;                     // Audio settings (for restore)
        public LightsConfig lights;                   // Lights settings (for restore)
        public DamageConfig damage;                   // Damage settings (for restore)
        public VisualEffectsConfig visualEffects;     // Visual effects (for restore)
        public string vehicleBehavior;                // Per-vehicle behavior preset NAME (e.g., "Drift", "Racing", "global")
        public DetectedWheels detectedWheels;
        public string explanation;
    }

    [Serializable]
    public class VehicleConfig {
        public string name;
        public float mass;
        public Vector3Config centerOfMassOffset;
    }

    [Serializable]
    public class Vector3Config {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() {
            return new Vector3(x, y, z);
        }

        public bool IsZero => Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f) && Mathf.Approximately(z, 0f);
    }

    // NOTE: All default values are 0/null/empty so JsonUtility's auto-created objects
    // won't be mistaken for meaningful configs. The AI will provide actual values.

    [Serializable]
    public class EngineConfig {
        public float maximumTorqueAsNM;
        public float minEngineRPM;
        public float maxEngineRPM;
        public float maximumSpeed;
        public float engineInertia;
        public float engineFriction;
        public bool turboCharged;
        public float maxTurboChargePsi;
        public float turboChargerCoEfficient;
    }

    [Serializable]
    public class GearboxConfig {
        public string transmissionType;
        public float[] gearRatios;
        public float shiftingTime;
        public float shiftThreshold;
        public float shiftUpRPM;
        public float shiftDownRPM;
    }

    [Serializable]
    public class DifferentialConfig {
        public string type;
        public float limitedSlipRatio;
        public float finalDriveRatio;
    }

    [Serializable]
    public class AxlesConfig {
        public AxleConfig front;
        public AxleConfig rear;
    }

    [Serializable]
    public class AxleConfig {
        public bool isSteer;
        public bool isBrake;
        // Note: isPower is NOT included here because it's controlled by RCCP_Differential.
        // The differential automatically sets isPower = true on its connected axle.
        // Use driveType (FWD/RWD/AWD) to configure which axles receive power.
        public bool isHandbrake;
        public float maxSteerAngle;
        public float maxBrakeTorque;
        public float maxHandbrakeTorque;
        public float antirollForce;
        public float steerSpeed;
        public float powerMultiplier;
        public float steerMultiplier;
        public float brakeMultiplier;
        public float handbrakeMultiplier;
    }

    /// <summary>
    /// Configuration for clutch system.
    /// Maps to RCCP_Clutch.
    /// </summary>
    [Serializable]
    public class ClutchConfig {
        public float clutchInertia;
        public float engageRPM;
        public bool automaticClutch;
        public bool pressClutchWhileShiftingGears;
        public bool pressClutchWhileHandbraking;
    }

    /// <summary>
    /// Configuration for fuel tank.
    /// Maps to RCCP_FuelTank (via OtherAddonsManager).
    /// </summary>
    [Serializable]
    public class FuelTankConfig {
        public bool enabled;
        public bool remove;                 // When true, destroys the FuelTank GameObject completely
        public float fuelTankCapacity;
        public float fuelTankFillAmount;
        public bool stopEngineWhenEmpty;
        public float baseLitersPerHour;
        public float maxLitersPerHour;
    }

    /// <summary>
    /// Configuration for speed limiter.
    /// Maps to RCCP_Limiter (via OtherAddonsManager).
    /// </summary>
    [Serializable]
    public class LimiterConfig {
        public bool enabled;
        public bool remove;                 // When true, destroys the Limiter GameObject completely
        public float[] limitSpeedAtGear;
        public bool applyDownhillForce;
        public float downhillForceStrength;
    }

    [Serializable]
    public class SuspensionConfig {
        public float distance;
        public float spring;
        public float damper;
    }

    [Serializable]
    public class WheelFrictionConfig {
        public string type;
        public FrictionCurveConfig forward;
        public FrictionCurveConfig sideways;
    }

    [Serializable]
    public class FrictionCurveConfig {
        public float extremumSlip;
        public float extremumValue;
        public float asymptoteSlip;
        public float asymptoteValue;
        public float stiffness;

        public WheelFrictionCurve ToWheelFrictionCurve() {
            // Default values if not set (0 would make friction ineffective)
            // These defaults are based on Unity's WheelCollider defaults
            float effectiveExtremumSlip = extremumSlip > 0 ? extremumSlip : 0.4f;
            float effectiveExtremumValue = extremumValue > 0 ? extremumValue : 1.0f;
            float effectiveAsymptoteSlip = asymptoteSlip > 0 ? asymptoteSlip : 0.8f;
            float effectiveAsymptoteValue = asymptoteValue > 0 ? asymptoteValue : 0.5f;
            float effectiveStiffness = stiffness > 0 ? stiffness : 1.0f;

            return new WheelFrictionCurve {
                extremumSlip = effectiveExtremumSlip,
                extremumValue = effectiveExtremumValue,
                asymptoteSlip = effectiveAsymptoteSlip,
                asymptoteValue = effectiveAsymptoteValue,
                stiffness = effectiveStiffness
            };
        }

        /// <summary>
        /// Check if this config has any meaningful values set
        /// </summary>
        public bool HasValues => extremumSlip > 0 || extremumValue > 0 || asymptoteSlip > 0 || asymptoteValue > 0 || stiffness > 0;
    }

    [Serializable]
    public class StabilityConfig {
        public bool remove;              // When true, destroys the Stability GameObject completely
        public bool ABS;
        public bool ESP;
        public bool TCS;
        public bool steeringHelper;
        public bool tractionHelper;
        public bool angularDragHelper;
        public float steerHelperStrength;
        public float tractionHelperStrength;
        public float angularDragHelperStrength;
    }

    [Serializable]
    public class DetectedWheels {
        public string frontLeft;
        public string frontRight;
        public string rearLeft;
        public string rearRight;
    }

    /// <summary>
    /// Configuration for aerodynamics.
    /// Maps to RCCP_AeroDynamics.
    /// </summary>
    [Serializable]
    public class AeroDynamicsConfig {
        public bool remove;              // When true, destroys the AeroDynamics GameObject completely
        public float downForce;
        public float airResistance;
        public float wheelResistance;
    }

    /// <summary>
    /// Configuration for NOS/boost system.
    /// Maps to RCCP_Nos.
    /// </summary>
    [Serializable]
    public class NosConfig {
        public bool enabled;
        public bool remove;                 // When true, destroys the NOS GameObject completely
        public float torqueMultiplier;
        public float durationTime;
        public float regenerateTime;
        public float regenerateRate;
    }

    /// <summary>
    /// Configuration for Recorder component.
    /// Maps to RCCP_Recorder (via OtherAddonsManager).
    /// </summary>
    [Serializable]
    public class RecorderConfig {
        public bool remove;                 // When true, destroys the Recorder GameObject completely
    }

    /// <summary>
    /// Configuration for TrailerAttacher component.
    /// Maps to RCCP_TrailerAttacher (via OtherAddonsManager).
    /// </summary>
    [Serializable]
    public class TrailerAttacherConfig {
        public bool remove;                 // When true, destroys the TrailerAttacher GameObject completely
    }

    /// <summary>
    /// Configuration for Customizer component.
    /// Maps to RCCP_Customizer (main addon for vehicle customization system).
    /// </summary>
    [Serializable]
    public class CustomizerConfig {
        public bool remove;                 // When true, destroys the Customizer GameObject completely
        public string saveFileName;         // Save file name for the vehicle
        public bool autoInitialize;         // Auto initializes all managers (default true)
        public bool autoLoadLoadout;        // Loads the latest loadout on start (default true)
        public bool autoSave;               // Auto save changes (default true)
        public string initializeMethod;     // Awake, OnEnable, Start, or DelayedWithFixedUpdate
    }

    #region Behavior Config

    /// <summary>
    /// A single keyframe for steering curve.
    /// Speed is in km/h, multiplier is 0-1 (applied to steer input).
    /// </summary>
    [Serializable]
    public class SteeringCurveKeyframe {
        public float speed;      // X axis: speed in km/h
        public float multiplier; // Y axis: steer input multiplier (0-1)
    }

    /// <summary>
    /// Steering curve configuration for behavior presets.
    /// Defines how steering input is reduced at higher speeds.
    /// Format: speed (km/h) -> multiplier (0-1)
    /// Example: At 0 km/h multiplier=1.0 (full steering), at 200 km/h multiplier=0.15 (reduced steering)
    /// </summary>
    [Serializable]
    public class SteeringCurveConfig {
        public SteeringCurveKeyframe[] keyframes;

        /// <summary>
        /// Converts to Unity AnimationCurve.
        /// </summary>
        public AnimationCurve ToAnimationCurve() {
            if (keyframes == null || keyframes.Length == 0)
                return null;

            Keyframe[] unityKeyframes = new Keyframe[keyframes.Length];
            for (int i = 0; i < keyframes.Length; i++) {
                // time = speed, value = multiplier (clamped 0-1)
                unityKeyframes[i] = new Keyframe(
                    keyframes[i].speed,
                    Mathf.Clamp01(keyframes[i].multiplier)
                );
            }

            AnimationCurve curve = new AnimationCurve(unityKeyframes);

            // Smooth the curve tangents for natural transitions
            for (int i = 0; i < curve.keys.Length; i++) {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }

            return curve;
        }

        public bool HasValues => keyframes != null && keyframes.Length > 0;
    }

    /// <summary>
    /// Configuration for RCCP behavior presets (driving styles).
    /// Maps to RCCP_Settings.BehaviorType.
    /// </summary>
    [Serializable]
    public class BehaviorConfig {
        // Action type: "switch", "modify", or "create"
        public string action;
        public string behaviorName;

        // Stability systems
        public bool ABS;
        public bool ESP;
        public bool TCS;
        public bool steeringHelper;
        public bool tractionHelper;
        public bool angularDragHelper;

        // Drift settings
        public bool driftMode;
        public bool driftAngleLimiter;
        public float driftAngleLimit;
        public float driftAngleCorrectionFactor;

        // Steering
        public float steeringSensitivity;
        public bool counterSteering;
        public bool limitSteering;
        public SteeringCurveConfig steeringCurve;  // Speed-sensitive steering curve

        // Differential
        public string differentialType;

        // Helper strengths
        public float steeringHelperStrengthMin;
        public float steeringHelperStrengthMax;
        public float tractionHelperStrengthMin;
        public float tractionHelperStrengthMax;

        // Gear shifting
        public float gearShiftingThreshold;

        // Wheel friction - Front
        public float forwardExtremumSlip_F;
        public float forwardExtremumValue_F;
        public float forwardAsymptoteSlip_F;
        public float forwardAsymptoteValue_F;
        public float sidewaysExtremumSlip_F;
        public float sidewaysExtremumValue_F;
        public float sidewaysAsymptoteSlip_F;
        public float sidewaysAsymptoteValue_F;

        // Wheel friction - Rear
        public float forwardExtremumSlip_R;
        public float forwardExtremumValue_R;
        public float forwardAsymptoteSlip_R;
        public float forwardAsymptoteValue_R;
        public float sidewaysExtremumSlip_R;
        public float sidewaysExtremumValue_R;
        public float sidewaysAsymptoteSlip_R;
        public float sidewaysAsymptoteValue_R;

        public string explanation;
    }

    #endregion

    #region Wheel Config

    /// <summary>
    /// Configuration for wheel/tire settings.
    /// Maps to RCCP_WheelCollider and Unity WheelCollider.
    /// Note: Suspension settings (spring, damper, distance) are handled by SuspensionConfig in the Customization panel.
    /// </summary>
    [Serializable]
    public class WheelConfig {
        // Wheel dimensions (meters)
        public float wheelRadius;   // IGNORED - RCCP calculates radius automatically from wheel mesh
        public float wheelWidth;    // Width of the tire (affects contact patch and grip)

        // Friction
        public FrictionCurveConfig forwardFriction;
        public FrictionCurveConfig sidewaysFriction;

        // Alignment (degrees) - Note: RCCP doesn't support toe
        public float camber;
        public float caster;

        // Grip multiplier (0-2, default 1.0)
        public float grip;  // Multiplies ground material stiffness. 0.5 = low grip, 1.0 = normal, 2.0 = max grip

        // Per-axle overrides (optional)
        public AxleWheelConfig front;
        public AxleWheelConfig rear;

        public string explanation;
    }

    [Serializable]
    public class AxleWheelConfig {
        // Wheel dimensions (optional per-axle override)
        public float wheelRadius;   // IGNORED - RCCP calculates radius automatically from wheel mesh
        public float wheelWidth;

        // Note: Suspension settings are handled by SuspensionConfig in the Customization panel
        public FrictionCurveConfig forwardFriction;
        public FrictionCurveConfig sidewaysFriction;
        public float camber;    // Per-axle camber override
        public float caster;    // Per-axle caster override
        public float grip;      // Per-axle grip override (0-2, default 1.0)
    }

    #endregion

    #region Audio Config

    /// <summary>
    /// Configuration for vehicle audio settings.
    /// Maps to RCCP_Audio engine sound layers.
    /// Note: RCCP_Audio doesn't have global volume multipliers - each sound type has its own maxVolume.
    /// </summary>
    [Serializable]
    public class AudioConfig {
        public bool remove;              // When true, destroys the Audio GameObject completely

        // Engine sound layers (typically 4: idle, low, mid, high RPM)
        public EngineSoundConfig[] engineSounds;

        public string explanation;
    }

    [Serializable]
    public class EngineSoundConfig {
        // Layer index (0-3 typically, for idle/low/mid/high RPM)
        public int layerIndex;

        // Enabled state (-1 = don't change, 0 = disable, 1 = enable)
        public int enabled;

        public float minRPM;
        public float maxRPM;
        public float minPitch;
        public float maxPitch;
        public float maxVolume;
        public float minDistance;
        public float maxDistance;

        // Check if enabled field should modify the layer
        public bool ShouldEnable => enabled == 1;
        public bool ShouldDisable => enabled == 0;
        public bool ShouldModifyEnabled => enabled == 0 || enabled == 1;
    }

    #endregion

    #region Lights Config

    /// <summary>
    /// Configuration for vehicle lights.
    /// Maps to RCCP_Lights and RCCP_Light.
    /// </summary>
    [Serializable]
    public class LightsConfig {
        public bool remove;              // When true, destroys the Lights GameObject completely
        public LightConfig[] lights;
        public string explanation;
    }

    [Serializable]
    public class LightConfig {
        public string lightType;
        public float intensity;
        public float smoothness;
        public ColorConfig emissiveColor;

        // Custom position (optional - if not set, auto-position from bounds)
        public Vector3Config position;

        // Custom rotation (optional - in euler angles)
        public Vector3Config rotation;

        // Unity Light component properties
        public float range;           // How far the light reaches (default ~10 for headlights)
        public float spotAngle;       // Cone angle for spot lights (default 80-120)
        public ColorConfig lightColor; // Unity Light color (null = don't change)

        // RCCP_Light properties
        public float flareBrightness; // Lens flare intensity (0-10, default 1.5)
        public int useLensFlares;     // 0 = don't change (default), 1 = disable, 2 = enable

        // Damage/breakable properties
        public int isBreakable;       // 0 = don't change (default), 1 = not breakable, 2 = breakable
        public float strength;        // Durability before breaking (default 100)
        public int breakPoint;        // Threshold for breaking (default 35)

        // Whether to use custom position/rotation
        public bool HasCustomPosition => position != null && !position.IsZero;
        public bool HasCustomRotation => rotation != null && !rotation.IsZero;

        // Helper to check if lens flare setting should be modified (1 = disable, 2 = enable)
        public bool ShouldModifyLensFlares => useLensFlares == 1 || useLensFlares == 2;
        public bool ShouldModifyBreakable => isBreakable == 1 || isBreakable == 2;
    }

    [Serializable]
    public class ColorConfig {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color ToColor() {
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Returns true if this color was actually specified in JSON.
        /// A color with r=0, g=0, b=0, a=0 is treated as "not specified" since
        /// fully transparent black is not a meaningful light color.
        /// </summary>
        public bool IsSpecified => r != 0f || g != 0f || b != 0f || a != 0f;
    }

    #endregion

    #region Damage Config

    /// <summary>
    /// Configuration for vehicle damage system.
    /// Maps to RCCP_Damage.
    /// </summary>
    [Serializable]
    public class DamageConfig {
        public bool remove;              // When true, destroys the Damage GameObject completely

        // Mesh deformation settings
        public bool meshDeformation;
        public float maximumDamage;
        public float deformationRadius;
        public float deformationMultiplier;
        public bool automaticInstallation;

        // Wheel damage settings
        public bool wheelDamage;
        public float wheelDamageRadius;
        public float wheelDamageMultiplier;
        public bool wheelDetachment;

        // Light damage settings
        public bool lightDamage;
        public float lightDamageRadius;
        public float lightDamageMultiplier;

        // Part (detachable) damage settings
        public bool partDamage;
        public float partDamageRadius;
        public float partDamageMultiplier;

        // Detachable parts configuration
        public DetachablePartConfig[] detachableParts;

        public string explanation;
    }

    /// <summary>
    /// Configuration for individual detachable parts.
    /// Maps to RCCP_DetachablePart.
    /// </summary>
    [Serializable]
    public class DetachablePartConfig {
        // Part type: Hood, Trunk, Door, Bumper_F, Bumper_R, Other
        public string partType;

        // Name of the mesh transform to make detachable
        public string meshName;

        // Strength before part detaches (lower = easier to detach)
        public float strength;

        // Mass of the detached part
        public float mass;
    }

    #endregion

    #region Friction Presets

    public static class FrictionPresets {

        public static (FrictionCurveConfig forward, FrictionCurveConfig sideways) GetPreset(string type) {
            switch (type?.ToLower()) {
                case "realistic":
                    return (
                        new FrictionCurveConfig { extremumSlip = 0.4f, extremumValue = 1.0f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.0f },
                        new FrictionCurveConfig { extremumSlip = 0.25f, extremumValue = 0.95f, asymptoteSlip = 0.5f, asymptoteValue = 0.7f, stiffness = 1.0f }
                    );

                case "stable":
                    return (
                        new FrictionCurveConfig { extremumSlip = 0.3f, extremumValue = 1.0f, asymptoteSlip = 0.8f, asymptoteValue = 0.65f, stiffness = 1.0f },
                        new FrictionCurveConfig { extremumSlip = 0.2f, extremumValue = 1.0f, asymptoteSlip = 0.5f, asymptoteValue = 0.65f, stiffness = 1.0f }
                    );

                case "slippy":
                case "drift":
                    return (
                        new FrictionCurveConfig { extremumSlip = 0.4f, extremumValue = 1.0f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.0f },
                        new FrictionCurveConfig { extremumSlip = 0.375f, extremumValue = 0.95f, asymptoteSlip = 0.5f, asymptoteValue = 0.6f, stiffness = 1.0f }
                    );

                case "balanced":
                default:
                    return (
                        new FrictionCurveConfig { extremumSlip = 0.35f, extremumValue = 1.0f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.0f },
                        new FrictionCurveConfig { extremumSlip = 0.2f, extremumValue = 1.0f, asymptoteSlip = 0.5f, asymptoteValue = 0.75f, stiffness = 1.0f }
                    );
            }
        }
    }

    #endregion

    #region Visual Effects Config

    /// <summary>
    /// Configuration for LOD (Level of Detail) system.
    /// Maps to RCCP_Lod.
    /// Controls which components are enabled based on camera distance.
    /// </summary>
    [Serializable]
    public class LodConfig {
        public bool remove;              // When true, destroys the LOD GameObject completely

        // LOD factor (0.1-1.0) - Higher means components stay enabled at greater distances
        public float lodFactor;

        // Force specific LOD levels (overrides distance-based calculation)
        public int forceToFirstLevel;    // -1 = don't change, 0 = disable, 1 = enable (highest detail)
        public int forceToLatestLevel;   // -1 = don't change, 0 = disable, 1 = enable (lowest detail)

        public string explanation;

        public bool ShouldModifyForceFirst => forceToFirstLevel == 0 || forceToFirstLevel == 1;
        public bool ShouldModifyForceLast => forceToLatestLevel == 0 || forceToLatestLevel == 1;
    }

    /// <summary>
    /// Configuration for wheel blur visual effect.
    /// Maps to RCCP_WheelBlur (via OtherAddonsManager).
    /// Creates a motion blur effect on wheels at high speeds.
    /// </summary>
    [Serializable]
    public class WheelBlurConfig {
        public bool remove;              // When true, destroys the WheelBlur GameObject completely

        // Offset applied to blur mesh position (X is flipped for right-side wheels)
        public Vector3Config offset;

        // Scale of the blur mesh (0.0-0.2, typical: 0.06)
        public float scale;

        // Rotation speed multiplier (0.0-5.0, typical: 0.25)
        public float rotationSpeed;

        // Smoothness of blur intensity transitions (higher = faster response)
        public float smoothness;

        public string explanation;
    }

    /// <summary>
    /// Configuration for exhaust effects.
    /// Maps to RCCP_Exhaust components.
    /// </summary>
    [Serializable]
    public class ExhaustsConfig {
        public bool remove;              // When true, destroys the Exhausts GameObject completely

        // Apply to all exhausts, or specific ones by index
        public ExhaustConfig[] exhausts;

        public string explanation;
    }

    [Serializable]
    public class ExhaustConfig {
        // Index of exhaust to modify (-1 = apply to all)
        public int exhaustIndex;

        // Flame on cut-off (backfire effect)
        public int flameOnCutOff;        // -1 = don't change, 0 = disable, 1 = enable

        // Lens flare brightness for flame (0-10, typical: 1)
        public float flareBrightness;

        // Flame colors
        public ColorConfig flameColor;
        public ColorConfig boostFlameColor;

        // Smoke emission settings
        public float minEmission;        // Min smoke rate (typical: 5)
        public float maxEmission;        // Max smoke rate (typical: 20)

        // Smoke particle size
        public float minSize;            // Min particle size (typical: 1)
        public float maxSize;            // Max particle size (typical: 4)

        // Smoke particle speed
        public float minSpeed;           // Min particle speed (typical: 0.1)
        public float maxSpeed;           // Max particle speed (typical: 1)

        public bool ShouldModifyFlameOnCutOff => flameOnCutOff == 0 || flameOnCutOff == 1;
    }

    /// <summary>
    /// Configuration for body tilt visual effect.
    /// Maps to RCCP_BodyTilt.
    /// Tilts the vehicle body based on acceleration and turning.
    /// </summary>
    [Serializable]
    public class BodyTiltConfig {
        public bool remove;              // When true, destroys the BodyTilt GameObject completely

        // Enable/disable body tilt (-1 = don't change, 0 = disable, 1 = enable)
        public int enabled;

        // Maximum tilt angle in degrees (typical: 5-10)
        public float maxTiltAngle;

        // Forward/backward tilt sensitivity (typical: 5)
        public float forwardTiltMultiplier;

        // Sideways tilt sensitivity (typical: 5)
        public float sidewaysTiltMultiplier;

        // Smoothing speed (higher = faster response, 0 = instant)
        public float tiltSmoothSpeed;

        public string explanation;

        public bool ShouldModifyEnabled => enabled == 0 || enabled == 1;
    }

    /// <summary>
    /// Configuration for input settings.
    /// Maps to RCCP_Input component.
    /// </summary>
    [Serializable]
    public class InputConfig {
        public float counterSteerFactor;        // 0-1, default 0.5 - counter-steer strength
        public bool counterSteering;            // Enable counter-steering assistance
        public bool steeringLimiter;            // Limit steering at high speeds
        public bool autoReverse;                // Enable automatic reverse gear
        public float steeringDeadzone;          // 0-0.2, default 0.05

        public string explanation;
    }

    /// <summary>
    /// Configuration for particles system.
    /// Maps to RCCP_Particles.
    /// </summary>
    [Serializable]
    public class ParticlesConfig {
        public bool remove;              // When true, destroys the Particles GameObject completely

        // Collision layer filter for scratch/contact effects
        // -1 = all layers, specific value = that layer mask
        public int collisionFilterMask;

        // Tire smoke settings
        public float tireSmokeEmissionRate;     // 0-100, default 50
        public float tireSmokeLifetime;         // 0-10, default 3
        public float tireSmokeSlipThreshold;    // 0-1, default 0.25

        // Skidmark settings
        public float skidmarkWidth;             // 0.1-0.5, default 0.25
        public float skidmarkIntensity;         // 0-2, default 1

        public string explanation;
    }

    /// <summary>
    /// Combined visual effects configuration.
    /// Used when AI configures multiple visual effects at once.
    /// </summary>
    [Serializable]
    public class VisualEffectsConfig {
        public LodConfig lod;
        public WheelBlurConfig wheelBlur;
        public ExhaustsConfig exhausts;
        public BodyTiltConfig bodyTilt;
        public ParticlesConfig particles;

        public string explanation;
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
