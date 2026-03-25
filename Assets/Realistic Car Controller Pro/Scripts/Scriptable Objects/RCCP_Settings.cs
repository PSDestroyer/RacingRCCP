//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

/// <summary>
/// Stored all general shared RCCP settings here.
/// </summary>
[System.Serializable]
public class RCCP_Settings : ScriptableObject {

    #region singleton
    private static RCCP_Settings instance;
    public static RCCP_Settings Instance { get { if (instance == null) instance = Resources.Load("RCCP_Settings") as RCCP_Settings; return instance; } }
    #endregion

    /// <summary>
    /// Current behavior.
    /// </summary>
    public BehaviorType SelectedBehaviorType {

        get {

            if (overrideBehavior)
                return behaviorTypes[behaviorSelectedIndex];
            else
                return null;

        }

    }

    /// <summary>
    /// Use multithreading if current platform is supported. Fallback to false if platform is not supported.
    /// </summary>
    public bool multithreading = true;

    /// <summary>
    /// Current selected behavior index.
    /// </summary>
    public int behaviorSelectedIndex = 0;

    /// <summary>
    /// Override FPS?
    /// </summary>
    public bool overrideFPS = true;

    /// <summary>
    /// Override fixed timestep?
    /// </summary>
    public bool overrideFixedTimeStep = true;

    /// <summary>
    /// Overriden fixed timestep value.
    /// </summary>
    [Range(.005f, .06f)] public float fixedTimeStep = .02f;

    /// <summary>
    /// Maximum angular velocity.
    /// </summary>
    [Range(.5f, 20f)] public float maxAngularVelocity = 6;

    /// <summary>
    /// Maximum FPS.
    /// </summary>
    public int maxFPS = 120;

    /// <summary>
    /// Override the behavior?
    /// </summary>
    public bool overrideBehavior = true;

    /// <summary>
    /// Behavior Types
    /// </summary>
    [System.Serializable]
    public class BehaviorType {

        /// <summary>
        /// Behavior name.
        /// </summary>
        public string behaviorName = "New Behavior";

        //  Driving helpers.
        [Header("Stability")]
        public bool ABS = true;
        public bool ESP = true;
        public bool TCS = true;
        public bool steeringHelper = true;
        public bool tractionHelper = true;
        public bool angularDragHelper = false;

        [Tooltip("Enables drift mode. Applies force-based drift assistance and reduces rear tire grip for controlled sliding.")]
        public bool driftMode = false;
        [Tooltip("Limits the maximum drift angle. Dampens angular velocity when drift angle exceeds the limit to prevent uncontrollable spins.")]
        public bool driftAngleLimiter = false;
        [Tooltip("Maximum allowed drift angle in degrees before correction forces are applied.")]
        [Range(0f, 90f)] public float driftAngleLimit = 30f;
        [Tooltip("How aggressively the drift angle is corrected when exceeding the limit. Higher values mean faster correction.")]
        [Range(0f, 10f)] public float driftAngleCorrectionFactor = 3f;

        //  Drift forces.
        [Header("Drift Forces")]
        [Tooltip("Multiplier for yaw torque applied during drift. Higher values make the car rotate faster when drifting.")]
        [Range(0f, 3f)] public float driftYawTorqueMultiplier = 0.7f;
        [Tooltip("Forward push force during drift to maintain speed. Higher values reduce speed loss while sliding.")]
        [Range(0f, 5000f)] public float driftForwardForceMultiplier = 2000f;
        [Tooltip("Lateral push force during drift. Higher values push the car further sideways for wider drifts.")]
        [Range(0f, 4000f)] public float driftSidewaysForceMultiplier = 1500f;
        [Tooltip("Minimum speed (km/h) required for drift forces to activate. Below this speed, no drift assistance is applied.")]
        [Range(0f, 60f)] public float driftMinSpeed = 20f;
        [Tooltip("Speed (km/h) at which drift forces reach full strength. Forces scale linearly between min speed and this value.")]
        [Range(20f, 150f)] public float driftFullForceSpeed = 80f;
        [Tooltip("How much throttle input alone contributes to yaw rotation. Higher values allow initiating drift with throttle without steering.")]
        [Range(0f, 1f)] public float driftThrottleYawFactor = 0.3f;

        //  Drift friction.
        [Header("Drift Friction")]
        [Tooltip("Minimum rear tire sideways grip during full drift. Lower values allow more lateral sliding. 1.0 = no grip reduction.")]
        [Range(0.1f, 1f)] public float driftRearSidewaysStiffnessMin = 0.45f;
        [Tooltip("Minimum rear tire forward grip during full drift. Lower values cause more speed loss. 1.0 = no grip reduction.")]
        [Range(0.5f, 1f)] public float driftRearForwardStiffnessMin = 0.8f;
        [Tooltip("Minimum front tire sideways grip during drift. Higher values keep front-end responsive for steering control.")]
        [Range(0.5f, 1.2f)] public float driftFrontSidewaysStiffnessMin = 0.9f;
        [Tooltip("How quickly tire grip reduces when entering a drift. Higher values make grip loss more immediate.")]
        [Range(1f, 20f)] public float driftFrictionResponseSpeed = 8f;
        [Tooltip("How quickly tire grip recovers when exiting a drift. Higher values make grip recovery faster.")]
        [Range(1f, 20f)] public float driftFrictionRecoverySpeed = 4f;

        //  Drift recovery.
        [Header("Drift Recovery")]
        [Tooltip("Maximum angular velocity (deg/s) allowed during drift. Prevents uncontrollable spins. 0 = no limit.")]
        [Range(0f, 360f)] public float driftMaxAngularVelocity = 120f;
        [Tooltip("Multiplier for recovery force when counter-steering during drift. Higher values make recovery from drifts easier.")]
        [Range(1f, 5f)] public float driftCounterSteerRecoveryBoost = 2f;
        [Tooltip("Constant forward force applied during drift to maintain momentum. Higher values prevent speed loss while drifting.")]
        [Range(0f, 3000f)] public float driftMomentumMaintenanceForce = 800f;
        [Tooltip("Smoothing speed for drift force transitions. Higher values mean faster response, lower values mean smoother transitions.")]
        [Range(1f, 20f)] public float driftForceSmoothing = 8f;

        //  Steering.
        [Header("Steering")]
        public AnimationCurve steeringCurve = new AnimationCurve(new Keyframe(0f, 40f), new Keyframe(50f, 20f), new Keyframe(100f, 11f), new Keyframe(150f, 6f), new Keyframe(200f, 5f));
        public float steeringSensitivity = 1f;
        public bool counterSteering = true;
        public bool limitSteering = true;

        [Header("Differential")]
        public RCCP_Differential.DifferentialType differentialType = RCCP_Differential.DifferentialType.Open;

        //  Counter steering limitations.
        [Space()]
        public float counterSteeringMinimum = .5f;
        public float counterSteeringMaximum = 1f;

        //  Steering sensitivity limitations.
        [Space()]
        public float steeringSpeedMinimum = .5f;
        public float steeringSpeedMaximum = 1f;

        //  Steering helper linear velocity limitations.
        [Range(0f, 1f)] public float steeringHelperStrengthMinimum = .1f;
        [Range(0f, 1f)] public float steeringHelperStrengthMaximum = 1f;

        //  Traction helper strength limitations.
        [Range(0f, 1f)] public float tractionHelperStrengthMinimum = .1f;
        [Range(0f, 1f)] public float tractionHelperStrengthMaximum = 1f;

        //  Angular drag limitations.
        [Range(0f, 10f)] public float angularDrag = .1f;
        [Range(0f, 1f)] public float angularDragHelperMinimum = .1f;
        [Range(0f, 1f)] public float angularDragHelperMaximum = 1f;

        //  Anti roll limitations.
        [Space()]
        public float antiRollMinimum = 500f;

        //  Gear shifting delay limitation.
        [Space()]
        [Range(.1f, .9f)] public float gearShiftingThreshold = .8f;
        [Range(0f, 1f)] public float gearShiftingDelayMinimum = .15f;
        [Range(0f, 1f)] public float gearShiftingDelayMaximum = .5f;

        //  Wheel frictions.
        [Header("Wheel Frictions Forward Front Side")]
        public float forwardExtremumSlip_F = .4f;
        public float forwardExtremumValue_F = 1f;
        public float forwardAsymptoteSlip_F = .8f;
        public float forwardAsymptoteValue_F = .5f;

        [Header("Wheel Frictions Forward Rear Side")]
        public float forwardExtremumSlip_R = .4f;
        public float forwardExtremumValue_R = .95f;
        public float forwardAsymptoteSlip_R = .75f;
        public float forwardAsymptoteValue_R = .5f;

        [Header("Wheel Frictions Sideways Front Side")]
        public float sidewaysExtremumSlip_F = .4f;
        public float sidewaysExtremumValue_F = 1f;
        public float sidewaysAsymptoteSlip_F = .5f;
        public float sidewaysAsymptoteValue_F = .75f;

        [Header("Wheel Frictions Sideways Rear Side")]
        public float sidewaysExtremumSlip_R = .4f;
        public float sidewaysExtremumValue_R = 1.05f;
        public float sidewaysAsymptoteSlip_R = .5f;
        public float sidewaysAsymptoteValue_R = .8f;

    }

    /// <summary>
    /// Behavior Types
    /// </summary>
    public BehaviorType[] behaviorTypes;

    /// <summary>
    /// Fixed wheelcolliders with higher mass will be used.
    /// </summary>
    public bool useFixedWheelColliders = true;      //  

    /// <summary>
    /// All vehicles can be resetted if upside down.
    /// </summary>
    public bool autoReset = true;       //  

    /// <summary>
    /// Information telemetry about current vehicle
    /// </summary>
    public bool useTelemetry = false;
    public bool useInputDebugger = false;
    public bool useMPH = false;

    /// <summary>
    /// Auto saves and loads the rebind map.
    /// </summary>
    public bool autoSaveLoadInputRebind = true;

    /// <summary>
    /// For mobile inputs
    /// </summary>
    public enum MobileController { TouchScreen, Gyro, SteeringWheel, Joystick }

    /// <summary>
    /// For mobile inputs
    /// </summary>
    public MobileController mobileController;

    /// <summary>
    /// Enable / disable the mobile controllers.
    /// </summary>
    public bool mobileControllerEnabled = false;

    /// <summary>
    /// Accelerometer sensitivity
    /// </summary>
    public float gyroSensitivity = 2.5f;

    /// <summary>
    /// Setting layers.
    /// </summary>
    public bool setLayers = true;

    /// <summary>
    /// Layer of the vehicle.
    /// </summary>
    public string RCCPLayer = "RCCP_Vehicle";

    /// <summary>
    /// Wheelcollider layer.
    /// </summary>
    public string RCCPWheelColliderLayer = "RCCP_WheelCollider";

    /// <summary>
    /// Detachable part's layer.
    /// </summary>
    public string RCCPDetachablePartLayer = "RCCP_DetachablePart";

    /// <summary>
    /// Props layer.
    /// </summary>
    public string RCCPPropLayer = "RCCP_Prop";

    /// <summary>
    /// Props layer.
    /// </summary>
    public string RCCPObstacleLayer = "RCCP_Obstacle";

    /// <summary>
    /// Used for using the lights more efficent and realistic. Vertex is not important, pixel is important.
    /// </summary>
    public bool useHeadLightsAsVertexLights = false;

    /// <summary>
    /// Used for using the lights more efficent and realistic. Vertex is not important, pixel is important.
    /// </summary>
    public bool useBrakeLightsAsVertexLights = true;

    /// <summary>
    /// Used for using the lights more efficent and realistic. Vertex is not important, pixel is important.
    /// </summary>
    public bool useReverseLightsAsVertexLights = true;

    /// <summary>
    /// Used for using the lights more efficent and realistic. Vertex is not important, pixel is important.
    /// </summary>
    public bool useIndicatorLightsAsVertexLights = true;

    /// <summary>
    /// Used for using the lights more efficent and realistic. Vertex is not important, pixel is important.
    /// </summary>
    public bool useOtherLightsAsVertexLights = true;

    #region Setup Prefabs

    // Light prefabs.
    public RCCP_LightSetupData lightsSetupData = new RCCP_LightSetupData();
    public GameObject lightBox;

    //  Camera prefabs.
    public RCCP_Camera RCCPMainCamera;
    public GameObject RCCPHoodCamera;
    public GameObject RCCPWheelCamera;
    public GameObject RCCPCinematicCamera;
    public GameObject RCCPFixedCamera;

    //  UI prefabs.
    public GameObject RCCPCanvas;
    public GameObject RCCPTelemetry;

    // Sound FX.
    public AudioMixerGroup audioMixer;
    public AudioClip engineLowClipOn;
    public AudioClip engineLowClipOff;
    public AudioClip engineMedClipOn;
    public AudioClip engineMedClipOff;
    public AudioClip engineHighClipOn;
    public AudioClip engineHighClipOff;
    public AudioClip engineIdleClipOn;
    public AudioClip engineIdleClipOff;
    public AudioClip engineStartClip;
    public AudioClip reversingClip;
    public AudioClip windClip;
    public AudioClip brakeClip;
    public AudioClip wheelDeflateClip;
    public AudioClip wheelInflateClip;
    public AudioClip wheelFlatClip;
    public AudioClip indicatorClip;
    public AudioClip bumpClip;
    public AudioClip NOSClip;
    public AudioClip turboClip;
    public AudioClip[] gearClips;
    public AudioClip[] crashClips;
    public AudioClip[] blowoutClip;
    public AudioClip[] exhaustFlameClips;

    //  Particles
    public GameObject contactParticles;
    public GameObject scratchParticles;
    public GameObject wheelSparkleParticles;

    //  Other prefabs.
    public GameObject exhaustGas;
    public RCCP_SkidmarksManager skidmarksManager;
    public GameObject wheelBlur;

    public Object lensFlareData;
    public Flare flare;
    public GameObject flarePrefab;

    public GameObject hdrpVolumeProfilePrefab;
    public Material defaultDecalMaterial;
    public Material defaultNeonMaterial;

    public Object vehicleColliderMaterial;

    #endregion

    // Used for folding sections of RCCP Settings.
    public bool foldGeneralSettings = false;
    public bool foldBehaviorSettings = false;
    public bool foldControllerSettings = false;
    public bool foldUISettings = false;
    public bool foldWheelPhysics = false;
    public bool foldOptimization = false;
    public bool foldTagsAndLayers = false;
    public bool foldExtensions = false;
    public bool resourcesSettings = false;

}
