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

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// ScriptableObject that stores default values extracted from RCCP components.
/// These defaults are used by RCCP_AIVehicleBuilder when creating new vehicles.
/// Run "Tools > BoneCracker Games > RCCP AI Assistant > Extract Component Defaults" to regenerate.
/// </summary>
[CreateAssetMenu(fileName = "RCCP_AIComponentDefaults", menuName = "RCCP AI/Component Defaults")]
public class RCCP_AIComponentDefaults : ScriptableObject {

    #region Singleton
    private static RCCP_AIComponentDefaults _instance;
    public static RCCP_AIComponentDefaults Instance {
        get {
            if (_instance == null) {
                _instance = Resources.Load<RCCP_AIComponentDefaults>("RCCP_AIComponentDefaults");
                if (_instance == null) {
                    Debug.LogWarning("[RCCP AI] RCCP_AIComponentDefaults asset not found. Run 'Tools > BoneCracker Games > RCCP AI Assistant > Extract Component Defaults' to create it.");
                }
            }
            return _instance;
        }
    }

    public static void ResetInstance() {
        _instance = null;
    }
    #endregion

    #region Prompt Section Generation
    /// <summary>
    /// Generates a formatted text section containing all component defaults for injection into AI prompts.
    /// This ensures the AI has accurate default values extracted from actual RCCP components.
    /// </summary>
    public string GetDefaultsAsPromptSection() {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== COMPONENT DEFAULTS (extracted from RCCP) ===");
        sb.AppendLine("Use these as reference when user doesn't specify values.");
        sb.AppendLine();

        // Rigidbody
        sb.AppendLine("RIGIDBODY:");
        sb.AppendLine($"  mass: {rigidbody.mass}, linearDamping: {rigidbody.linearDamping}, angularDamping: {rigidbody.angularDamping}");
        sb.AppendLine();

        // Engine
        sb.AppendLine("ENGINE:");
        sb.AppendLine($"  minEngineRPM: {engine.minEngineRPM}, maxEngineRPM: {engine.maxEngineRPM}");
        sb.AppendLine($"  maximumTorqueAsNM: {engine.maximumTorqueAsNM}, maximumSpeed: {engine.maximumSpeed}");
        sb.AppendLine($"  engineInertia: {engine.engineInertia}, engineFriction: {engine.engineFriction}");
        sb.AppendLine($"  turboCharged: {engine.turboCharged.ToString().ToLower()}, maxTurboChargePsi: {engine.maxTurboChargePsi}, turboChargerCoEfficient: {engine.turboChargerCoEfficient}");
        sb.AppendLine();

        // Gearbox
        sb.AppendLine("GEARBOX:");
        sb.AppendLine($"  transmissionType: {gearbox.transmissionType}");
        sb.AppendLine($"  gearRatios: [{string.Join(", ", gearbox.gearRatios)}]");
        sb.AppendLine($"  shiftingTime: {gearbox.shiftingTime}, shiftThreshold: {gearbox.shiftThreshold}");
        sb.AppendLine($"  shiftUpRPM: {gearbox.shiftUpRPM}, shiftDownRPM: {gearbox.shiftDownRPM}");
        sb.AppendLine();

        // Clutch
        sb.AppendLine("CLUTCH:");
        sb.AppendLine($"  clutchInertia: {clutch.clutchInertia}, engageRPM: {clutch.engageRPM}");
        sb.AppendLine($"  automaticClutch: {clutch.automaticClutch.ToString().ToLower()}");
        sb.AppendLine();

        // Differential
        sb.AppendLine("DIFFERENTIAL:");
        sb.AppendLine($"  differentialType: {differential.differentialType}");
        sb.AppendLine($"  limitedSlipRatio: {differential.limitedSlipRatio}, finalDriveRatio: {differential.finalDriveRatio}");
        sb.AppendLine();

        // Axle
        sb.AppendLine("AXLE:");
        sb.AppendLine($"  antirollForce: {axle.antirollForce}, maxSteerAngle: {axle.maxSteerAngle}");
        sb.AppendLine($"  maxBrakeTorque: {axle.maxBrakeTorque}, maxHandbrakeTorque: {axle.maxHandbrakeTorque}");
        sb.AppendLine($"  steerSpeed: {axle.steerSpeed}, powerMultiplier: {axle.powerMultiplier}, steerMultiplier: {axle.steerMultiplier}, brakeMultiplier: {axle.brakeMultiplier}, handbrakeMultiplier: {axle.handbrakeMultiplier}");
        sb.AppendLine();

        // WheelCollider / Suspension
        sb.AppendLine("WHEEL/SUSPENSION:");
        sb.AppendLine($"  wheelWidth: {wheelCollider.wheelWidth}, suspensionDistance: {wheelCollider.suspensionDistance}");
        sb.AppendLine($"  suspensionSpring: {wheelCollider.suspensionSpring}, suspensionDamper: {wheelCollider.suspensionDamper}");
        sb.AppendLine($"  camber: {wheelCollider.camber}, caster: {wheelCollider.caster}");
        sb.AppendLine();

        // Friction Curves
        sb.AppendLine("FRICTION CURVES:");
        sb.AppendLine($"  forward: extremumSlip={wheelCollider.forwardExtremumSlip}, extremumValue={wheelCollider.forwardExtremumValue}, asymptoteSlip={wheelCollider.forwardAsymptoteSlip}, asymptoteValue={wheelCollider.forwardAsymptoteValue}, stiffness={wheelCollider.forwardStiffness}");
        sb.AppendLine($"  sideways: extremumSlip={wheelCollider.sidewaysExtremumSlip}, extremumValue={wheelCollider.sidewaysExtremumValue}, asymptoteSlip={wheelCollider.sidewaysAsymptoteSlip}, asymptoteValue={wheelCollider.sidewaysAsymptoteValue}, stiffness={wheelCollider.sidewaysStiffness}");
        sb.AppendLine();

        // Stability
        sb.AppendLine("STABILITY:");
        sb.AppendLine($"  ABS: {stability.ABS.ToString().ToLower()}, ESP: {stability.ESP.ToString().ToLower()}, TCS: {stability.TCS.ToString().ToLower()}");
        sb.AppendLine($"  steeringHelper: {stability.steeringHelper.ToString().ToLower()}, tractionHelper: {stability.tractionHelper.ToString().ToLower()}, angularDragHelper: {stability.angularDragHelper.ToString().ToLower()}");
        sb.AppendLine($"  steerHelperStrength: {stability.steerHelperStrength}, tractionHelperStrength: {stability.tractionHelperStrength}, angularDragHelperStrength: {stability.angularDragHelperStrength}");
        sb.AppendLine();

        // AeroDynamics
        sb.AppendLine("AERODYNAMICS:");
        sb.AppendLine($"  downForce: {aeroDynamics.downForce}, airResistance: {aeroDynamics.airResistance}, wheelResistance: {aeroDynamics.wheelResistance}");
        sb.AppendLine();

        // NOS
        sb.AppendLine("NOS:");
        sb.AppendLine($"  torqueMultiplier: {nos.torqueMultiplier}, durationTime: {nos.durationTime}");
        sb.AppendLine($"  regenerateTime: {nos.regenerateTime}, regenerateRate: {nos.regenerateRate}");
        sb.AppendLine();

        // FuelTank
        sb.AppendLine("FUEL TANK:");
        sb.AppendLine($"  fuelTankCapacity: {fuelTank.fuelTankCapacity}, fuelTankFillAmount: {fuelTank.fuelTankFillAmount}, baseLitersPerHour: {fuelTank.baseLitersPerHour}, maxLitersPerHour: {fuelTank.maxLitersPerHour}");
        sb.AppendLine();

        // Input
        sb.AppendLine("INPUT:");
        sb.AppendLine($"  counterSteerFactor: {input.counterSteerFactor}, counterSteering: {input.counterSteering.ToString().ToLower()}");
        sb.AppendLine($"  steeringLimiter: {input.steeringLimiter.ToString().ToLower()}, autoReverse: {input.autoReverse.ToString().ToLower()}, steeringDeadzone: {input.steeringDeadzone}");
        sb.AppendLine();

        // Lights
        sb.AppendLine("LIGHTS:");
        sb.AppendLine($"  intensity: {lights.intensity}, smoothness: {lights.smoothness}");
        sb.AppendLine($"  useLensFlares: {lights.useLensFlares.ToString().ToLower()}, flareBrightness: {lights.flareBrightness}");
        sb.AppendLine($"  isBreakable: {lights.isBreakable.ToString().ToLower()}, breakStrength: {lights.breakStrength}");
        sb.AppendLine();

        // Damage
        sb.AppendLine("DAMAGE:");
        sb.AppendLine($"  meshDeformation: {damage.meshDeformation.ToString().ToLower()}, maximumDamage: {damage.maximumDamage}");
        sb.AppendLine($"  deformationRadius: {damage.deformationRadius}, deformationMultiplier: {damage.deformationMultiplier}");

        return sb.ToString();
    }
    #endregion

    /// <summary>
    /// Timestamp when defaults were last extracted
    /// </summary>
    public string extractedDate;

    /// <summary>
    /// RCCP version when defaults were extracted
    /// </summary>
    public string rccpVersion;

    // Core Component defaults
    public RigidbodyDefaults rigidbody = new RigidbodyDefaults();
    public EngineDefaults engine = new EngineDefaults();
    public GearboxDefaults gearbox = new GearboxDefaults();
    public ClutchDefaults clutch = new ClutchDefaults();
    public DifferentialDefaults differential = new DifferentialDefaults();
    public AxleDefaults axle = new AxleDefaults();
    public StabilityDefaults stability = new StabilityDefaults();
    public WheelColliderDefaults wheelCollider = new WheelColliderDefaults();
    public AeroDynamicsDefaults aeroDynamics = new AeroDynamicsDefaults();
    public NosDefaults nos = new NosDefaults();
    public FuelTankDefaults fuelTank = new FuelTankDefaults();
    public LimiterDefaults limiter = new LimiterDefaults();
    public DamageDefaults damage = new DamageDefaults();
    public InputDefaults input = new InputDefaults();

    // Additional Core Components
    public AudioDefaults audio = new AudioDefaults();
    public LightsDefaults lights = new LightsDefaults();
    public ParticlesDefaults particles = new ParticlesDefaults();
    public LodDefaults lod = new LodDefaults();

    // Addon Components
    public AIDefaults ai = new AIDefaults();
    public AIDynamicObstacleAvoidanceDefaults aiObstacleAvoidance = new AIDynamicObstacleAvoidanceDefaults();
    public BodyTiltDefaults bodyTilt = new BodyTiltDefaults();
    public ExhaustsDefaults exhausts = new ExhaustsDefaults();
    public TrailerAttacherDefaults trailerAttacher = new TrailerAttacherDefaults();
    public WheelBlurDefaults wheelBlur = new WheelBlurDefaults();
    public RecorderDefaults recorder = new RecorderDefaults();
    public DetachablePartDefaults detachablePart = new DetachablePartDefaults();
    public VisualDashboardDefaults visualDashboard = new VisualDashboardDefaults();
    public ExteriorCamerasDefaults exteriorCameras = new ExteriorCamerasDefaults();

    // Upgrade Components
    public UpgradeEngineDefaults upgradeEngine = new UpgradeEngineDefaults();
    public UpgradeBrakeDefaults upgradeBrake = new UpgradeBrakeDefaults();
    public UpgradeHandlingDefaults upgradeHandling = new UpgradeHandlingDefaults();
    public UpgradeSpeedDefaults upgradeSpeed = new UpgradeSpeedDefaults();
    public UpgradeSpoilerDefaults upgradeSpoiler = new UpgradeSpoilerDefaults();
    public UpgradePaintDefaults upgradePaint = new UpgradePaintDefaults();
    public UpgradeNeonDefaults upgradeNeon = new UpgradeNeonDefaults();
    public UpgradeDecalDefaults upgradeDecal = new UpgradeDecalDefaults();
    public UpgradeSirenDefaults upgradeSiren = new UpgradeSirenDefaults();

    #region Default Classes

    [Serializable]
    public class RigidbodyDefaults {
        public float mass = 1350f;
        public float linearDamping = 0.0025f;
        public float angularDamping = 0.35f;
        public float maxAngularVelocity = 6f;
    }

    [Serializable]
    public class EngineDefaults {
        public float minEngineRPM = 750f;
        public float maxEngineRPM = 7000f;
        public float maximumTorqueAsNM = 200f;
        public float maximumSpeed = 240f;
        public float engineAccelerationRate = 0.75f;
        public float engineCouplingToWheelsRate = 1.5f;
        public float engineDecelerationRate = 0.35f;
        public float engineInertia = 0.2f;       // Range: 0.01-1
        public float engineFriction = 0.2f;      // Range: 0-1
        public bool turboCharged = false;
        public float maxTurboChargePsi = 12f;    // Updated from component
        public float turboChargerCoEfficient = 1.25f;  // Range: 1-2
        public bool engineRevLimiter = true;
        public float revLimiterCutFrequency = 15f; // Range: 5-30
        public float peakRPM = 4000f;
    }

    [Serializable]
    public class GearboxDefaults {
        public string transmissionType = "Automatic";
        public float[] gearRatios = new float[] { 4.35f, 2.5f, 1.66f, 1.23f, 1.0f, 0.85f };
        public float shiftingTime = 0.2f;
        public float shiftThreshold = 0.8f;      // Range: 0.1-0.9
        public float shiftUpRPM = 5500f;         // Updated from component
        public float shiftDownRPM = 2750f;       // Updated from component
    }

    [Serializable]
    public class ClutchDefaults {
        public float clutchInertia = 0.5f;
        public float engageRPM = 1600f;
        public bool automaticClutch = true;
        public bool pressClutchWhileShiftingGears = true;
        public bool pressClutchWhileHandbraking = true;
    }

    [Serializable]
    public class DifferentialDefaults {
        public string differentialType = "Limited";
        public float limitedSlipRatio = 80f;
        public float finalDriveRatio = 3.73f;
    }

    [Serializable]
    public class AxleDefaults {
        public float antirollForce = 500f;
        public float maxSteerAngle = 40f;
        public float maxBrakeTorque = 3000f;
        public float maxHandbrakeTorque = 5000f;
        public float steerSpeed = 5f;
        public float powerMultiplier = 1f;
        public float steerMultiplier = 1f;
        public float brakeMultiplier = 1f;
        public float handbrakeMultiplier = 1f;

        // Front axle specific
        public bool frontIsSteer = true;
        public bool frontIsBrake = true;
        public bool frontIsHandbrake = false;

        // Rear axle specific
        public bool rearIsSteer = false;
        public bool rearIsBrake = true;
        public bool rearIsHandbrake = true;
    }

    [Serializable]
    public class StabilityDefaults {
        public bool ABS = true;
        public bool ESP = true;
        public bool TCS = true;
        public bool steeringHelper = true;
        public bool tractionHelper = true;
        public bool angularDragHelper = false;
        public float engageABSThreshold = 0.35f;
        public float engageESPThreshold = 0.35f;
        public float engageTCSThreshold = 0.35f;
        public float steerHelperStrength = 0.5f;
        public float tractionHelperStrength = 0.5f;
        public float angularDragHelperStrength = 0.5f;
    }

    [Serializable]
    public class WheelColliderDefaults {
        public float wheelRadius = 0.34f;  // Reference only - RCCP calculates from wheel mesh, not applied by AI
        public float wheelWidth = 0.25f;   // Min: 0.1
        public float suspensionDistance = 0.2f;  // WHEEL_SUSPENSION_DISTANCE constant
        public float suspensionSpring = 50000f;  // WHEEL_SPRING_VALUE constant
        public float suspensionDamper = 3500f;   // WHEEL_DAMPER_VALUE constant
        public float camber = 0f;
        public float caster = 0f;
        public float grip = 1f;            // Range: 0-2

        // Friction curve defaults
        public float forwardExtremumSlip = 0.4f;
        public float forwardExtremumValue = 1f;
        public float forwardAsymptoteSlip = 0.8f;
        public float forwardAsymptoteValue = 0.75f;
        public float forwardStiffness = 1f;

        public float sidewaysExtremumSlip = 0.25f;
        public float sidewaysExtremumValue = 1f;
        public float sidewaysAsymptoteSlip = 0.5f;
        public float sidewaysAsymptoteValue = 0.75f;
        public float sidewaysStiffness = 1f;
    }

    [Serializable]
    public class AeroDynamicsDefaults {
        public float downForce = 10f;              // Min: 0
        public float airResistance = 10f;          // Range: 0-100
        public float wheelResistance = 10f;        // Range: 0-100
        public bool dynamicCOM = false;
        public bool autoReset = true;
        public float autoResetTime = 3f;           // Min: 0
    }

    [Serializable]
    public class NosDefaults {
        public bool enabled = false;
        public float torqueMultiplier = 2.5f;    // Min: 0
        public float durationTime = 3f;          // Min: 0
        public float regenerateTime = 2f;        // Min: 0
        public float regenerateRate = 1f;        // Updated from component, Min: 0
    }

    [Serializable]
    public class FuelTankDefaults {
        public bool enabled = false;
        public float fuelTankCapacity = 60f;      // Range: 0-300
        public float fuelTankFillAmount = 1f;     // Range: 0-1
        public bool stopEngineWhenEmpty = true;
        public float baseLitersPerHour = 1f;      // Range: 0-20, Updated from component
        public float maxLitersPerHour = 25f;      // Range: 0-200, Updated from component
        public float minimalIdleThrottle = 0.05f; // Range: 0-1
    }

    [Serializable]
    public class LimiterDefaults {
        public bool enabled = false;
        public bool applyDownhillForce = true;
        public float downhillForceStrength = 100f;
    }

    [Serializable]
    public class DamageDefaults {
        public bool meshDeformation = true;
        public float maximumDamage = 0.75f;
        public float deformationRadius = 0.75f;
        public float deformationMultiplier = 1f;
    }

    [Serializable]
    public class InputDefaults {
        public float counterSteerFactor = 0.5f;    // Range: 0-1
        public bool counterSteering = true;
        public bool steeringLimiter = true;
        public bool autoReverse = true;
        public bool inverseThrottleBrakeOnReverse = true;
        public bool cutThrottleWhenShifting = true;
        public bool applyBrakeOnDisable = false;
        public bool applyHandBrakeOnDisable = true;
        public float steeringDeadzone = 0.05f;     // Range: 0-0.2
        public float throttleDeadzone = 0.05f;     // Range: 0-0.2
        public float brakeDeadzone = 0.05f;        // Range: 0-0.2
        public float handbrakeDeadzone = 0.05f;    // Range: 0-0.2
        public float nosDeadzone = 0.05f;          // Range: 0-0.2
        public float clutchDeadzone = 0.05f;       // Range: 0-0.2
    }

    #endregion

    #region Additional Core Component Classes

    [Serializable]
    public class AudioDefaults {
        // NOTE: RCCP_Audio uses nested classes (EngineSound, GearboxSound, etc.)
        // These are typical defaults for engine sound layers

        // Engine sound layer defaults (from EngineSound class)
        public float engineMinPitch = 0.1f;
        public float engineMaxPitch = 1f;
        public float engineMinRPM = 600f;
        public float engineMaxRPM = 8000f;
        public float engineMinDistance = 10f;
        public float engineMaxDistance = 200f;
        public float engineMaxVolume = 1f;

        // These values cannot be directly extracted - RCCP_Audio has no top-level fields
        // Audio is configured through nested class arrays (engineSounds[], etc.)
    }

    [Serializable]
    public class LightsDefaults {
        // RCCP_Light component defaults (individual light settings)
        public float intensity = 1f;           // Range 0.1-10
        public float smoothness = 0.5f;        // Range 0.1-1
        public bool useLensFlares = true;
        public float flareBrightness = 1.5f;   // Range 0-10
        public bool isBreakable = true;
        public float breakStrength = 100f;
    }

    [Serializable]
    public class ParticlesDefaults {
        // NOTE: RCCP_Particles only has prefab references and LayerMask
        // No numeric configuration values to extract
        // Particle behavior is controlled by the prefabs themselves (RCCP_WheelSlipParticles, etc.)

        // Collision filter default (all layers)
        public int collisionFilterValue = -1;

        // These are documentation values - not extractable from RCCP_Particles
        // Actual particle settings are on child components like RCCP_Exhausts
    }

    [Serializable]
    public class LodDefaults {
        public bool forceToFirstLevel = false;
        public bool forceToLatestLevel = false;
        public float lodFactor = 0.8f;  // Range 0.1-1.0
    }

    #endregion

    #region Addon Component Classes

    [Serializable]
    public class AIDefaults {
        // Navigation
        public float waypointRadius = 5f;
        public float nextWaypointDistance = 20f;

        // Speed control
        public float maxSpeed = 100f;
        public float smoothedSteer = 5f;

        // Obstacle detection
        public float raycastDistance = 30f;
        public float raycastAngle = 30f;
        public int raycastCount = 5;

        // Behavior
        public float aggressiveness = 0.5f;
        public float brakeDistance = 10f;
        public float steeringSensitivity = 1f;

        // PID Controller values
        public float steerPID_P = 1f;
        public float steerPID_I = 0f;
        public float steerPID_D = 0.1f;
        public float throttlePID_P = 1f;
        public float throttlePID_I = 0f;
        public float throttlePID_D = 0.1f;
    }

    [Serializable]
    public class BodyTiltDefaults {
        public bool enabled = false;
        public float tiltAngle = 5f;
        public float tiltSpeed = 2f;
        public float verticalTiltAngle = 2f;
    }

    [Serializable]
    public class ExhaustsDefaults {
        public float emissionMultiplier = 1f;
        public float smokeIntensity = 1f;
        public bool flameEnabled = true;
        public float flameThreshold = 0.8f;
    }

    [Serializable]
    public class TrailerAttacherDefaults {
        public float connectionDistance = 2f;
        public float breakForce = 10000f;
        public float breakTorque = 10000f;
        public bool autoConnect = false;
    }

    [Serializable]
    public class WheelBlurDefaults {
        public bool enabled = true;
        public float blurStartSpeed = 50f;
        public float blurFullSpeed = 100f;
        public float blurIntensity = 1f;
    }

    [Serializable]
    public class RecorderDefaults {
        public bool enabled = false;
        public float recordInterval = 0.1f;
        public int maxRecordedFrames = 1000;
        public bool recordPosition = true;
        public bool recordRotation = true;
        public bool recordInputs = true;
    }

    [Serializable]
    public class AIDynamicObstacleAvoidanceDefaults {
        public bool enabled = true;
        public float detectionDistance = 20f;
        public float detectionAngle = 45f;
        public float avoidanceStrength = 1f;
        public float reactionTime = 0.5f;
        public int rayCount = 5;
        public LayerMask obstacleLayers;
    }

    [Serializable]
    public class DetachablePartDefaults {
        public float detachmentForce = 5000f;
        public float detachmentTorque = 5000f;
        public bool canDetach = true;
        public float randomRotationForce = 10f;
    }

    [Serializable]
    public class VisualDashboardDefaults {
        public float needleSmoothness = 5f;
        public float rpmMultiplier = 1f;
        public float speedMultiplier = 1f;
        public bool digitalDisplay = true;
    }

    [Serializable]
    public class ExteriorCamerasDefaults {
        public float hoodCameraFOV = 60f;
        public float wheelCameraFOV = 70f;
        public float cameraSmoothing = 5f;
        public bool autoSwitch = false;
    }

    #endregion

    #region Upgrade Component Classes

    [Serializable]
    public class UpgradeEngineDefaults {
        public int maxLevel = 5;
        public float torqueIncreasePerLevel = 25f;
        public float rpmIncreasePerLevel = 500f;
        public float efficiencyPerLevel = 0.05f;
    }

    [Serializable]
    public class UpgradeBrakeDefaults {
        public int maxLevel = 5;
        public float brakeForceIncreasePerLevel = 500f;
        public float absEfficiencyPerLevel = 0.05f;
    }

    [Serializable]
    public class UpgradeHandlingDefaults {
        public int maxLevel = 5;
        public float steerAngleIncreasePerLevel = 2f;
        public float stabilityIncreasePerLevel = 0.1f;
        public float tractionIncreasePerLevel = 0.05f;
    }

    [Serializable]
    public class UpgradeSpeedDefaults {
        public int maxLevel = 5;
        public float topSpeedIncreasePerLevel = 10f;
        public float accelerationIncreasePerLevel = 0.05f;
    }

    [Serializable]
    public class UpgradeSpoilerDefaults {
        public int maxLevel = 3;
        public float downforceIncreasePerLevel = 50f;
        public float dragIncreasePerLevel = 5f;
    }

    [Serializable]
    public class UpgradePaintDefaults {
        public bool useMetallic = true;
        public float defaultSmoothness = 0.8f;
        public float defaultMetallic = 0.5f;
    }

    [Serializable]
    public class UpgradeNeonDefaults {
        public bool enabled = false;
        public float intensity = 1f;
        public float range = 5f;
        public bool underglow = true;
    }

    [Serializable]
    public class UpgradeDecalDefaults {
        public float defaultScale = 1f;
        public float defaultOpacity = 1f;
        public bool allowMultiple = true;
    }

    [Serializable]
    public class UpgradeSirenDefaults {
        public bool enabled = false;
        public float lightIntensity = 2f;
        public float flashInterval = 0.25f;
        public bool soundEnabled = true;
        public float soundVolume = 1f;
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
