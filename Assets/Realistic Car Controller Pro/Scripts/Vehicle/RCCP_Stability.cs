//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright (c) 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages various stability systems for the vehicle:
/// - ABS (Anti-skid Braking System),
/// - ESP (Electronic Stability Program),
/// - TCS (Traction Control System),
/// plus steering, traction, and angular-drag helpers.
///
/// IMPORTANT: Steering Angle Integration with RCCP_Input
/// This component works in conjunction with RCCP_Input, which modifies the steering angle through:
/// - Steering Curve: Reduces max steering angle at high speeds
/// - Counter Steering: Adds automatic correction based on wheel slip
/// - Steering Limiter: Reduces steering when the vehicle is sliding
///
/// The CarController.steerAngle value used here is the ACTUAL steering angle (in degrees)
/// after all RCCP_Input modifications. To properly normalize steering calculations,
/// we derive the effective maximum steering angle by dividing the actual angle by the
/// processed steering input (CarController.steerInput_P).
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Addons/RCCP Stability")]
public class RCCP_Stability : RCCP_Component {

    /// <summary>
    /// Reference to the axle manager component.
    /// </summary>
    [Tooltip("Reference to the axle manager component.")]
    public RCCP_Axles AxleManager;

    /// <summary>
    /// Reference to the front axle.
    /// </summary>
    [Tooltip("Reference to the front axle for ESP calculations.")]
    public RCCP_Axle frontAxle;

    /// <summary>
    /// Reference to the rear axle.
    /// </summary>
    [Tooltip("Reference to the rear axle for ESP calculations.")]
    public RCCP_Axle rearAxle;

    /// <summary>
    /// Collection of axles that provide power to wheels.
    /// </summary>
    [Tooltip("Collection of powered axles for TCS calculations.")]
    public List<RCCP_Axle> poweredAxles = new List<RCCP_Axle>();

    /// <summary>
    /// Collection of axles used for steering.
    /// </summary>
    [Tooltip("Collection of steering axles.")]
    public List<RCCP_Axle> steeringAxles = new List<RCCP_Axle>();

    /// <summary>
    /// Collection of axles used for braking.
    /// </summary>
    [Tooltip("Collection of braked axles for ABS calculations.")]
    public List<RCCP_Axle> brakedAxles = new List<RCCP_Axle>();

    /// <summary>
    /// Enable / disable ABS.
    /// </summary>
    [Tooltip("Enable or disable the Anti-lock Braking System (ABS). When enabled, prevents wheel lockup during heavy braking by modulating brake pressure.")]
    public bool ABS = true;

    /// <summary>
    /// Enable / disable ESP.
    /// </summary>
    [Tooltip("Enable or disable the Electronic Stability Program (ESP). When enabled, applies selective braking to individual wheels to correct oversteer and understeer.")]
    public bool ESP = true;

    /// <summary>
    /// Enable / disable TCS.
    /// </summary>
    [Tooltip("Enable or disable the Traction Control System (TCS). When enabled, reduces engine torque when driven wheels lose traction to prevent wheelspin.")]
    public bool TCS = true;

    /// <summary>
    /// ABS threshold. If slip * brakeInput exceeds this, ABS will engage to reduce brake torque.
    /// </summary>
    [Tooltip("ABS engagement threshold. If wheel slip multiplied by brake input exceeds this value, ABS activates to reduce brake torque. Lower values make ABS more sensitive.")]
    [Range(.01f, .5f)] public float engageABSThreshold = .35f;

    /// <summary>
    /// ESP threshold. If sideways slip exceeds this, ESP will engage by applying brakes to stabilize the vehicle.
    /// </summary>
    [Tooltip("ESP engagement threshold. If sideways wheel slip exceeds this value, ESP activates to apply corrective braking. Lower values make ESP more aggressive.")]
    [Range(.01f, .5f)] public float engageESPThreshold = .35f;

    /// <summary>
    /// TCS threshold. If forward slip on powered wheels exceeds this, TCS will engage to reduce motor torque.
    /// </summary>
    [Tooltip("TCS engagement threshold. If forward wheel slip on powered wheels exceeds this value, TCS activates to reduce engine torque. Lower values make TCS more sensitive.")]
    [Range(.01f, .5f)] public float engageTCSThreshold = .35f;

    /// <summary>
    /// How strongly ABS reduces brake torque.
    /// </summary>
    [Tooltip("ABS intensity multiplier. Higher values result in more aggressive brake torque reduction when ABS is engaged.")]
    [Range(0f, 1f)] public float ABSIntensity = 1f;

    /// <summary>
    /// How strongly ESP brakes wheels to correct over/under steering.
    /// </summary>
    [Tooltip("ESP intensity multiplier. Higher values result in more aggressive corrective braking to stabilize the vehicle.")]
    [Range(0f, 1f)] public float ESPIntensity = 1f;

    /// <summary>
    /// How strongly TCS reduces torque to wheels if slipping.
    /// </summary>
    [Tooltip("TCS intensity multiplier. Higher values result in more aggressive torque reduction when wheels are spinning.")]
    [Range(0f, 1f)] public float TCSIntensity = 1f;

    /// <summary>
    /// True if ABS is currently engaged on at least one wheel.
    /// </summary>
    [Tooltip("True if ABS is currently active on at least one wheel.")]
    public bool ABSEngaged = false;

    /// <summary>
    /// True if ESP is currently engaged to help stabilize the vehicle.
    /// </summary>
    [Tooltip("True if ESP is currently correcting oversteer or understeer.")]
    public bool ESPEngaged = false;

    /// <summary>
    /// True if TCS is currently engaged to reduce excessive wheel slip under power.
    /// </summary>
    [Tooltip("True if TCS is currently reducing torque due to wheel slip.")]
    public bool TCSEngaged = false;

    /// <summary>
    /// If true, adds a small force to reduce oversteer or understeer (steering helper).
    /// </summary>
    [Tooltip("Enable steering helper. Applies corrective forces at wheel contact points to reduce oversteer and understeer based on wheel load distribution.")]
    public bool steeringHelper = true;

    /// <summary>
    /// If true, reduces front tire stiffness if the vehicle is skidding significantly (traction helper).
    /// </summary>
    [Tooltip("Enable traction helper. Reduces front axle lateral stiffness when the vehicle is sliding to prevent spins and improve control recovery.")]
    public bool tractionHelper = true;

    /// <summary>
    /// If true, increases angular drag as speed increases (angular drag helper).
    /// </summary>
    [Tooltip("Enable angular drag helper. Increases rotational damping at higher speeds for improved high-speed stability.")]
    public bool angularDragHelper = false;

    /// <summary>
    /// If true, limits maximum drift angle. On extreme angles, angular velocity is damped.
    /// </summary>
    [Tooltip("Enable drift angle limiter. Dampens angular velocity when the drift angle exceeds the maximum allowed value to prevent uncontrollable spins.")]
    public bool driftAngleLimiter = false;

    /// <summary>
    /// Max allowed drift angle in degrees before it's partially corrected.
    /// </summary>
    [Tooltip("Maximum allowed drift angle in degrees. Beyond this angle, the drift angle limiter will dampen angular velocity.")]
    [Range(0f, 90f)] public float maxDriftAngle = 35f;

    /// <summary>
    /// How quickly the angular velocity is damped if the drift angle exceeds maxDriftAngle.
    /// </summary>
    [Tooltip("Drift angle correction factor. Higher values result in faster damping of angular velocity when drift angle exceeds the maximum.")]
    [Range(0f, 10f)] public float driftAngleCorrectionFactor = 5f;

    /// <summary>
    /// Strength factor for steeringHelper.
    /// </summary>
    [Tooltip("Steering helper strength. Controls the intensity of corrective forces applied to reduce lateral slip and improve handling.")]
    [Range(0f, 1f)] public float steerHelperStrength = .025f;

    /// <summary>
    /// Strength factor for tractionHelper.
    /// </summary>
    [Tooltip("Traction helper strength. Controls how much front axle stiffness is reduced when the vehicle is sliding.")]
    [Range(0f, 1f)] public float tractionHelperStrength = .05f;

    /// <summary>
    /// Strength factor for angularDragHelper.
    /// </summary>
    [Tooltip("Angular drag helper strength. Controls how much additional rotational damping is applied at higher speeds.")]
    [Range(0f, 1f)] public float angularDragHelperStrength = .075f;

    /// <summary>
    /// Minimum helper strength at low speeds (0 km/h). Lower values = more nimble at low speed.
    /// </summary>
    [Tooltip("Minimum helper strength at low speeds. Lower values make the vehicle more nimble and responsive at low speeds.")]
    [Range(0f, 1f)] public float minSpeedHelperStrength = .2f;

    /// <summary>
    /// Maximum helper strength at high speeds. Higher values = more stable at high speed.
    /// </summary>
    [Tooltip("Maximum helper strength at high speeds. Higher values provide more stability assistance at highway speeds.")]
    [Range(0f, 1f)] public float maxSpeedHelperStrength = 1f;

    /// <summary>
    /// Speed (km/h) at which helper reaches maximum strength. Lower = earlier stabilization.
    /// </summary>
    [Tooltip("Speed at which stability helpers reach maximum strength. Lower values mean full stabilization kicks in at lower speeds.")]
    [Range(0f, 200f)] public float fullHelperSpeed = 80f;

    /// <summary>
    /// Minimum steering angle to trigger steering assist (degrees). Prevents unnecessary calculations at near-zero steering.
    /// </summary>
    [Tooltip("Minimum steering angle to activate steering assist. Prevents unnecessary force calculations when steering is nearly centered.")]
    [Range(0.1f, 5f)] public float minSteerAngleForAssist = 1f;

    /// <summary>
    /// Minimum speed for steering assist to activate (km/h). More responsive at low speeds.
    /// </summary>
    [Tooltip("Minimum speed for steering assist activation. Below this speed, steering assist is reduced.")]
    [Range(5f, 30f)] public float steerAssistMinSpeed = 10f;

    /// <summary>
    /// Maximum speed for full steering assist (km/h). Assist scales between min and max speed.
    /// </summary>
    [Tooltip("Maximum speed for full steering assist effect. Assist strength scales between min and max speed values.")]
    [Range(30f, 120f)] public float steerAssistMaxSpeed = 60f;

    /// <summary>
    /// High speed threshold (km/h) for safety adjustments. Above this speed, stability is prioritized.
    /// </summary>
    [Tooltip("High speed threshold for safety adjustments. Above this speed, stability helpers are reduced when wheels lose contact to prevent flips.")]
    [Range(40f, 100f)] public float highSpeedThreshold = 60f;

    /// <summary>
    /// Speed range for high-speed safety scaling (km/h). Used to calculate safety reduction at very high speeds.
    /// </summary>
    [Tooltip("Speed range over which high-speed safety scaling is applied. Larger values create a more gradual safety reduction.")]
    [Range(50f, 150f)] public float highSpeedSafetyRange = 80f;

    /// <summary>
    /// Smoothing speed for drift forces and torques. Higher values = faster response, lower values = smoother transitions.
    /// </summary>
    [Tooltip("Smoothing speed for drift mode forces. Higher values mean faster response, lower values mean smoother transitions.")]
    [Range(1f, 20f)] public float driftForceSmoothing = 8f;

    /// <summary>
    /// Smoothing speed for ESP brake torques. Higher values = faster ESP response, lower values = gentler corrections.
    /// </summary>
    [Tooltip("Smoothing speed for ESP brake corrections. Higher values mean faster ESP response, lower values mean gentler corrections.")]
    [Range(1f, 20f)] public float espBrakeSmoothing = 10f;

    /// <summary>
    /// Smoothing speed for steering helper forces. Higher values = more responsive, lower values = smoother handling.
    /// </summary>
    [Tooltip("Smoothing speed for steering helper forces. Higher values mean more responsive handling, lower values mean smoother steering.")]
    [Range(1f, 20f)] public float steerHelperForceSmoothing = 15f;

    /// <summary>
    /// Current grounded factor based on wheel contact time.
    /// </summary>
    [Tooltip("Grounded factor (0-1) based on wheel contact time. Used to scale stability forces.")]
    public float groundedFactor = 0f;

    /// <summary>
    /// Current stability factor based on handbrake input.
    /// 1.0 = full stability assistance, 0.0 = no stability assistance.
    /// Scales down as handbrake is applied.
    /// </summary>
    private float currentStabilityFactor = 1f;

    private float previousGroundedScaling = 1f;
    private int previousGroundedWheelCount = 4;

    // Drift state
    [HideInInspector, Tooltip("Current drift intensity (0-1). Computed from rear wheel slip magnitude.")]
    public float driftIntensity = 0f;
    private float smoothedDriftIntensity = 0f;

    // Drift force parameters (set via CheckBehaviorDelayed from BehaviorType)
    [HideInInspector, Tooltip("Multiplier for yaw torque applied during drift. Higher values make the car rotate faster when drifting.")]
    public float driftYawTorqueMultiplier = 0.7f;
    [HideInInspector, Tooltip("Forward push force during drift to maintain speed. Higher values reduce speed loss while sliding.")]
    public float driftForwardForceMultiplier = 2000f;
    [HideInInspector, Tooltip("Lateral push force during drift. Higher values push the car further sideways for wider drifts.")]
    public float driftSidewaysForceMultiplier = 1500f;
    [HideInInspector, Tooltip("Minimum speed (km/h) required for drift forces to activate. Below this speed, no drift assistance is applied.")]
    public float driftMinSpeed = 20f;
    [HideInInspector, Tooltip("Speed (km/h) at which drift forces reach full strength. Forces scale linearly between min speed and this value.")]
    public float driftFullForceSpeed = 80f;
    [HideInInspector, Tooltip("How much throttle input alone contributes to yaw rotation. Higher values allow initiating drift with throttle without steering.")]
    public float driftThrottleYawFactor = 0.3f;

    // Drift friction parameters
    [HideInInspector, Tooltip("Minimum rear tire sideways grip during full drift. Lower values allow more lateral sliding. 1.0 = no grip reduction.")]
    public float driftRearSidewaysStiffnessMin = 0.45f;
    [HideInInspector, Tooltip("Minimum rear tire forward grip during full drift. Lower values cause more speed loss. 1.0 = no grip reduction.")]
    public float driftRearForwardStiffnessMin = 0.8f;
    [HideInInspector, Tooltip("Minimum front tire sideways grip during drift. Higher values keep front-end responsive for steering control.")]
    public float driftFrontSidewaysStiffnessMin = 0.9f;
    [HideInInspector, Tooltip("How quickly tire grip reduces when entering a drift. Higher values make grip loss more immediate.")]
    public float driftFrictionResponseSpeed = 8f;
    [HideInInspector, Tooltip("How quickly tire grip recovers when exiting a drift. Higher values make grip recovery faster.")]
    public float driftFrictionRecoverySpeed = 4f;

    // Drift recovery parameters
    [HideInInspector, Tooltip("Maximum angular velocity (deg/s) allowed during drift. Prevents uncontrollable spins. 0 = no limit.")]
    public float driftMaxAngularVelocity = 120f;
    [HideInInspector, Tooltip("Multiplier for recovery force when counter-steering during drift. Higher values make recovery from drifts easier.")]
    public float driftCounterSteerRecoveryBoost = 2f;
    [HideInInspector, Tooltip("Constant forward force applied during drift to maintain momentum. Higher values prevent speed loss while drifting.")]
    public float driftMomentumMaintenanceForce = 800f;

    // Smoothed drift force/torque tracking
    private Vector3 smoothedDriftTorque = Vector3.zero;
    private Vector3 smoothedDriftForwardForce = Vector3.zero;
    private Vector3 smoothedDriftSidewaysForce = Vector3.zero;

    // Smoothed ESP brake torque tracking
    private float smoothedESPFrontLeftBrake = 0f;
    private float smoothedESPFrontRightBrake = 0f;
    private float smoothedESPRearLeftBrake = 0f;
    private float smoothedESPRearRightBrake = 0f;

    // Smoothed steer helper force tracking (per wheel)
    private Dictionary<RCCP_WheelCollider, Vector3> smoothedLateralForces = new Dictionary<RCCP_WheelCollider, Vector3>();
    private Dictionary<RCCP_WheelCollider, Vector3> smoothedSteerForces = new Dictionary<RCCP_WheelCollider, Vector3>();

    private float wheelForceFactor;
    private float smoothedTractionStiffness = 1f;

    public override void Start() {

        base.Start();

        AxleManager = CarController.AxleManager;
        frontAxle = CarController.FrontAxle;
        rearAxle = CarController.RearAxle;
        poweredAxles = CarController.PoweredAxles;
        steeringAxles = CarController.SteeredAxles;
        brakedAxles = CarController.BrakedAxles;

    }

    private void FixedUpdate() {

        if (!CarController)
            return;

        if (CarController.IsGrounded)
            groundedFactor += Time.deltaTime * .5f;
        else
            groundedFactor = 0f;

        if (CarController.handbrakeInput_V > .5f)
            groundedFactor = .35f;

        groundedFactor = Mathf.Clamp01(groundedFactor);

        // Calculate stability factor based on handbrake input
        // 1.0 = no handbrake (full stability), 0.0 = full handbrake (no stability)
        // This allows gradual reduction of stability systems as handbrake is applied
        currentStabilityFactor = Mathf.Clamp01(1f - CarController.handbrakeInput_V);

        if (ESP)
            UpdateESP();

        if (TCS)
            UpdateTCS();

        if (ABS)
            UpdateABS();

        if (steeringHelper)
            SteerHelper();

        if (tractionHelper)
            TractionHelper();

        if (angularDragHelper)
            AngularDragHelper();

        if (driftAngleLimiter)
            LimitDriftAngle();

        if (RCCPSettings.SelectedBehaviorType != null && RCCPSettings.SelectedBehaviorType.driftMode)
            Drift();
        else
            ResetDriftFriction();

    }

    /// <summary>
    /// Two-layer cooperative drift system. Computes drift intensity from rear wheel slip,
    /// applies force-based drift (yaw torque, forward/sideways forces) with speed scaling
    /// and spinout prevention, then communicates friction multipliers to wheel colliders.
    /// </summary>
    private void Drift() {

        // Early exit if not properly grounded
        if (groundedFactor < 0.1f)
            return;

        float rearWheelSlipAmountForward = 0f;
        float rearWheelSlipAmountSideways = 0f;

        // Check if rear wheels are actually grounded before calculating slip
        bool rearLeftGrounded = false;
        bool rearRightGrounded = false;

        if (rearAxle != null) {

            WheelHit hit;

            if (rearAxle.leftWheelCollider != null && rearAxle.leftWheelCollider.WheelCollider != null)
                rearLeftGrounded = rearAxle.leftWheelCollider.WheelCollider.GetGroundHit(out hit) && hit.force > 10f;

            if (rearAxle.rightWheelCollider != null && rearAxle.rightWheelCollider.WheelCollider != null)
                rearRightGrounded = rearAxle.rightWheelCollider.WheelCollider.GetGroundHit(out hit) && hit.force > 10f;

        }

        // Only calculate slip if at least one rear wheel is properly grounded
        if (!rearLeftGrounded && !rearRightGrounded) {

            ResetDriftFriction();
            return;

        }

        // 1. Get average slip on rear wheels (forward & sideways).
        if (rearAxle != null) {

            float leftForwardSlip = rearLeftGrounded ? rearAxle.leftWheelCollider.ForwardSlip : 0f;
            float rightForwardSlip = rearRightGrounded ? rearAxle.rightWheelCollider.ForwardSlip : 0f;
            float leftSidewaysSlip = rearLeftGrounded ? rearAxle.leftWheelCollider.SidewaysSlip : 0f;
            float rightSidewaysSlip = rearRightGrounded ? rearAxle.rightWheelCollider.SidewaysSlip : 0f;

            rearWheelSlipAmountForward = (leftForwardSlip + rightForwardSlip) * 0.5f;
            rearWheelSlipAmountSideways = (leftSidewaysSlip + rightSidewaysSlip) * 0.5f;

        }

        // 2. Compute drift intensity using sqrt scaling — makes small slips significant for easier initiation
        float sidewaysSlipAbs = Mathf.Abs(rearWheelSlipAmountSideways);
        float rawDriftIntensity = Mathf.Clamp01(Mathf.Sqrt(sidewaysSlipAbs) * 1.5f);
        smoothedDriftIntensity = Mathf.Lerp(smoothedDriftIntensity, rawDriftIntensity, Time.fixedDeltaTime * driftForceSmoothing);
        driftIntensity = smoothedDriftIntensity;

        // 3. Speed-dependent scaling — no drift forces at low speeds
        float speed = CarController.absoluteSpeed;
        float speedScale = Mathf.Clamp01((speed - driftMinSpeed) / Mathf.Max(driftFullForceSpeed - driftMinSpeed, 1f));

        // 4. Use linear slip with sign preservation instead of squared
        float linearForwardSlip = rearWheelSlipAmountForward;
        float linearSidewaysSlip = rearWheelSlipAmountSideways;

        // 5. Determine force application point (COM if available)
        Transform comTransform = transform;
        RCCP_AeroDynamics aeroDynamics = CarController.AeroDynamics;

        if (aeroDynamics != null && aeroDynamics.COM != null)
            comTransform = aeroDynamics.COM;

        // 6. Normalized steering input
        float normalizedSteerInput = Mathf.Clamp(CarController.steerInput_P, -1f, 1f);

        // 7. Counter-steer detection: player steers against drift direction
        float driftDirection = Mathf.Sign(rearWheelSlipAmountSideways);
        bool isCounterSteering = (normalizedSteerInput != 0f) && (Mathf.Sign(normalizedSteerInput) != driftDirection) && (sidewaysSlipAbs > 0.1f);
        float counterSteerFactor = isCounterSteering ? driftCounterSteerRecoveryBoost : 1f;

        // 8. Yaw torque — steering always contributes; throttle adds extra yaw via driftThrottleYawFactor
        float steeringYaw = normalizedSteerInput * CarController.direction;
        float throttleYaw = driftThrottleYawFactor * driftIntensity * Mathf.Abs(CarController.throttleInput_P) * CarController.direction * driftDirection;

        // Counter-steer boosts recovery yaw
        float yawInput = steeringYaw * counterSteerFactor + throttleYaw;

        Vector3 targetDriftTorque = Vector3.up
            * yawInput
            * driftYawTorqueMultiplier
            * speedScale
            * groundedFactor;

        smoothedDriftTorque = Vector3.Lerp(
            smoothedDriftTorque,
            targetDriftTorque,
            Time.fixedDeltaTime * driftForceSmoothing
        );

        CarController.Rigid.AddRelativeTorque(smoothedDriftTorque, ForceMode.Acceleration);

        // 9. Angular velocity clamping — prevents spinouts
        if (driftMaxAngularVelocity > 0f) {

            Vector3 angVel = CarController.Rigid.angularVelocity;
            float maxAngVelRad = driftMaxAngularVelocity * Mathf.Deg2Rad;

            if (Mathf.Abs(angVel.y) > maxAngVelRad) {

                angVel.y = Mathf.Lerp(angVel.y, Mathf.Sign(angVel.y) * maxAngVelRad, Time.fixedDeltaTime * 5f);
                CarController.Rigid.angularVelocity = angVel;

            }

        }

        // Grounded ratio for force scaling
        float groundedRatio = 0f;
        if (rearLeftGrounded && rearRightGrounded)
            groundedRatio = 1f;
        else if (rearLeftGrounded || rearRightGrounded)
            groundedRatio = 0.5f;

        // 10. Forward force — two parts: reactive (slip-based) + proactive (momentum maintenance)
        float reactiveForward = driftForwardForceMultiplier
            * Mathf.Abs(linearSidewaysSlip)
            * Mathf.Clamp01(Mathf.Abs(linearForwardSlip) * 4f);

        float proactiveForward = driftMomentumMaintenanceForce
            * driftIntensity
            * Mathf.Abs(CarController.throttleInput_P);

        Vector3 targetForwardForce = transform.forward
            * (reactiveForward + proactiveForward)
            * speedScale
            * CarController.direction
            * groundedFactor
            * groundedRatio;

        smoothedDriftForwardForce = Vector3.Lerp(
            smoothedDriftForwardForce,
            targetForwardForce,
            Time.fixedDeltaTime * driftForceSmoothing
        );

        CarController.Rigid.AddForceAtPosition(
            smoothedDriftForwardForce,
            comTransform.position,
            ForceMode.Force
        );

        // 11. Sideways force — reduced during counter-steer for easier recovery
        float sidewaysReduction = isCounterSteering ? 0.4f : 1f;

        Vector3 targetSidewaysForce = transform.right
            * driftSidewaysForceMultiplier
            * linearSidewaysSlip
            * Mathf.Clamp01(Mathf.Abs(linearForwardSlip) * 4f)
            * sidewaysReduction
            * speedScale
            * CarController.direction
            * groundedFactor
            * groundedRatio;

        smoothedDriftSidewaysForce = Vector3.Lerp(
            smoothedDriftSidewaysForce,
            targetSidewaysForce,
            Time.fixedDeltaTime * driftForceSmoothing
        );

        CarController.Rigid.AddForceAtPosition(
            smoothedDriftSidewaysForce,
            comTransform.position,
            ForceMode.Force
        );

        // 12. Bridge to friction layer
        ApplyDriftFriction();

    }

    /// <summary>
    /// Communicates drift friction multipliers to each wheel collider based on drift intensity.
    /// Rear wheels get full intensity effect, front wheels get 50% reduced effect.
    /// </summary>
    private void ApplyDriftFriction() {

        if (CarController.AxleManager == null || CarController.AxleManager.Axles == null)
            return;

        float lerpSpeed = driftIntensity > 0.1f ? driftFrictionResponseSpeed : driftFrictionRecoverySpeed;

        for (int i = 0; i < CarController.AxleManager.Axles.Count; i++) {

            RCCP_Axle axle = CarController.AxleManager.Axles[i];

            if (axle == null)
                continue;

            // Determine if this is a rear axle (z < 0) or front axle
            bool isRearAxle = false;

            if (axle.leftWheelCollider != null)
                isRearAxle = axle.leftWheelCollider.transform.localPosition.z < 0f;
            else if (axle.rightWheelCollider != null)
                isRearAxle = axle.rightWheelCollider.transform.localPosition.z < 0f;

            // Front wheels get 50% reduced drift intensity effect
            float effectiveIntensity = isRearAxle ? driftIntensity : driftIntensity * 0.5f;

            float targetForwardMul;
            float targetSidewaysMul;

            if (isRearAxle) {

                targetForwardMul = Mathf.Lerp(1f, driftRearForwardStiffnessMin, effectiveIntensity);
                targetSidewaysMul = Mathf.Lerp(1f, driftRearSidewaysStiffnessMin, effectiveIntensity);

            } else {

                targetForwardMul = 1f; // Front forward grip stays at 100%
                targetSidewaysMul = Mathf.Lerp(1f, driftFrontSidewaysStiffnessMin, effectiveIntensity);

            }

            if (axle.leftWheelCollider != null) {

                axle.leftWheelCollider.driftForwardStiffnessMultiplier = Mathf.Lerp(
                    axle.leftWheelCollider.driftForwardStiffnessMultiplier,
                    targetForwardMul,
                    Time.fixedDeltaTime * lerpSpeed
                );

                axle.leftWheelCollider.driftSidewaysStiffnessMultiplier = Mathf.Lerp(
                    axle.leftWheelCollider.driftSidewaysStiffnessMultiplier,
                    targetSidewaysMul,
                    Time.fixedDeltaTime * lerpSpeed
                );

            }

            if (axle.rightWheelCollider != null) {

                axle.rightWheelCollider.driftForwardStiffnessMultiplier = Mathf.Lerp(
                    axle.rightWheelCollider.driftForwardStiffnessMultiplier,
                    targetForwardMul,
                    Time.fixedDeltaTime * lerpSpeed
                );

                axle.rightWheelCollider.driftSidewaysStiffnessMultiplier = Mathf.Lerp(
                    axle.rightWheelCollider.driftSidewaysStiffnessMultiplier,
                    targetSidewaysMul,
                    Time.fixedDeltaTime * lerpSpeed
                );

            }

        }

    }

    /// <summary>
    /// Resets all drift friction multipliers to 1.0 when drift mode is off.
    /// </summary>
    private void ResetDriftFriction() {

        driftIntensity = 0f;
        smoothedDriftIntensity = 0f;

        if (CarController.AxleManager == null || CarController.AxleManager.Axles == null)
            return;

        for (int i = 0; i < CarController.AxleManager.Axles.Count; i++) {

            RCCP_Axle axle = CarController.AxleManager.Axles[i];

            if (axle == null)
                continue;

            if (axle.leftWheelCollider != null) {

                axle.leftWheelCollider.driftForwardStiffnessMultiplier = 1f;
                axle.leftWheelCollider.driftSidewaysStiffnessMultiplier = 1f;

            }

            if (axle.rightWheelCollider != null) {

                axle.rightWheelCollider.driftForwardStiffnessMultiplier = 1f;
                axle.rightWheelCollider.driftSidewaysStiffnessMultiplier = 1f;

            }

        }

    }

    /// <summary>
    /// Manages the ABS logic, reducing brake torque if a braked wheel is slipping above engageABSThreshold.
    /// </summary>
    private void UpdateABS() {

        ABSEngaged = false;

        if (AxleManager == null)
            return;

        if (brakedAxles == null || brakedAxles.Count < 1)
            return;

        // Scale ABS intensity by stability factor (reduced when handbrake is applied)
        float scaledABSIntensity = ABSIntensity * currentStabilityFactor;

        for (int i = 0; i < brakedAxles.Count; i++) {

            if (brakedAxles[i] == null)
                continue;

            if (brakedAxles[i].leftWheelCollider != null) {

                if ((Mathf.Abs(brakedAxles[i].leftWheelCollider.ForwardSlip) * brakedAxles[i].brakeInput) >= engageABSThreshold) {

                    brakedAxles[i].leftWheelCollider.CutBrakeABS(scaledABSIntensity);
                    ABSEngaged = true;

                }

            }

            if (brakedAxles[i].rightWheelCollider != null) {

                if ((Mathf.Abs(brakedAxles[i].rightWheelCollider.ForwardSlip) * brakedAxles[i].brakeInput) >= engageABSThreshold) {

                    brakedAxles[i].rightWheelCollider.CutBrakeABS(scaledABSIntensity);
                    ABSEngaged = true;

                }

            }

        }

    }

    /// <summary>
    /// Manages ESP logic, detecting oversteer and understeer by comparing
    /// front/rear sideways slip. Applies brake torque on specific wheels
    /// to stabilize the vehicle.
    /// </summary>
    private void UpdateESP() {

        ESPEngaged = false;

        // Early out if front or rear axle is missing
        if (frontAxle == null || rearAxle == null)
            return;

        if (frontAxle.leftWheelCollider == null || frontAxle.rightWheelCollider == null)
            return;

        if (rearAxle.leftWheelCollider == null || rearAxle.rightWheelCollider == null)
            return;

        // Scale ESP intensity by stability factor (reduced when handbrake is applied)
        float scaledESPIntensity = ESPIntensity * currentStabilityFactor;

        // Sum the sideways slip for each axle
        float frontSlip = frontAxle.leftWheelCollider.SidewaysSlip
                        + frontAxle.rightWheelCollider.SidewaysSlip;
        float rearSlip = rearAxle.leftWheelCollider.SidewaysSlip
                       + rearAxle.rightWheelCollider.SidewaysSlip;

        // Check if slips exceed your threshold
        bool underSteering = Mathf.Abs(frontSlip) >= engageESPThreshold;
        bool overSteering = Mathf.Abs(rearSlip) >= engageESPThreshold;

        // If either condition is met, ESP is engaged
        if (underSteering || overSteering)
            ESPEngaged = true;

        // -----------------------------------------------------------
        // 1. Understeer Correction
        // If front wheels are skidding (underSteering),
        // brake front wheels proportionally to frontSlip sign.
        // -----------------------------------------------------------
        if (underSteering && frontAxle.isBrake) {

            // Calculate target brake torques
            float targetFrontLeftBrake = frontAxle.maxBrakeTorque * (scaledESPIntensity * 0.1f)
                * Mathf.Clamp(-frontSlip, 0f, Mathf.Infinity);

            float targetFrontRightBrake = frontAxle.maxBrakeTorque * (scaledESPIntensity * 0.1f)
                * Mathf.Clamp(frontSlip, 0f, Mathf.Infinity);

            // Smooth the brake torque transitions
            smoothedESPFrontLeftBrake = Mathf.Lerp(
                smoothedESPFrontLeftBrake,
                targetFrontLeftBrake,
                Time.fixedDeltaTime * espBrakeSmoothing
            );

            smoothedESPFrontRightBrake = Mathf.Lerp(
                smoothedESPFrontRightBrake,
                targetFrontRightBrake,
                Time.fixedDeltaTime * espBrakeSmoothing
            );

            // Apply smoothed brake torques
            frontAxle.leftWheelCollider.AddBrakeTorque(smoothedESPFrontLeftBrake);
            frontAxle.rightWheelCollider.AddBrakeTorque(smoothedESPFrontRightBrake);

        } else {

            // Reset smoothed values when not understeering
            smoothedESPFrontLeftBrake = Mathf.Lerp(smoothedESPFrontLeftBrake, 0f, Time.fixedDeltaTime * espBrakeSmoothing * 2f);
            smoothedESPFrontRightBrake = Mathf.Lerp(smoothedESPFrontRightBrake, 0f, Time.fixedDeltaTime * espBrakeSmoothing * 2f);

        }

        // -----------------------------------------------------------
        // 2. Oversteer Correction
        // If rear wheels are skidding (overSteering),
        // brake rear wheels proportionally to rearSlip sign.
        // -----------------------------------------------------------
        if (overSteering && rearAxle.isBrake) {

            // Calculate target brake torques
            float targetRearLeftBrake = rearAxle.maxBrakeTorque * (scaledESPIntensity * 0.2f)
                * Mathf.Clamp(-rearSlip, 0f, Mathf.Infinity);

            float targetRearRightBrake = rearAxle.maxBrakeTorque * (scaledESPIntensity * 0.2f)
                * Mathf.Clamp(rearSlip, 0f, Mathf.Infinity);

            // Smooth the brake torque transitions
            smoothedESPRearLeftBrake = Mathf.Lerp(
                smoothedESPRearLeftBrake,
                targetRearLeftBrake,
                Time.fixedDeltaTime * espBrakeSmoothing
            );

            smoothedESPRearRightBrake = Mathf.Lerp(
                smoothedESPRearRightBrake,
                targetRearRightBrake,
                Time.fixedDeltaTime * espBrakeSmoothing
            );

            // Apply smoothed brake torques
            rearAxle.leftWheelCollider.AddBrakeTorque(smoothedESPRearLeftBrake);
            rearAxle.rightWheelCollider.AddBrakeTorque(smoothedESPRearRightBrake);

        } else {

            // Reset smoothed values when not oversteering
            smoothedESPRearLeftBrake = Mathf.Lerp(smoothedESPRearLeftBrake, 0f, Time.fixedDeltaTime * espBrakeSmoothing * 2f);
            smoothedESPRearRightBrake = Mathf.Lerp(smoothedESPRearRightBrake, 0f, Time.fixedDeltaTime * espBrakeSmoothing * 2f);

        }

    }

    /// <summary>
    /// Manages TCS logic, reducing motor torque if the powered wheels
    /// are slipping beyond engageTCSThreshold (in forward or reverse).
    /// </summary>
    private void UpdateTCS() {

        TCSEngaged = false;

        if (poweredAxles == null || poweredAxles.Count < 1)
            return;

        // If the vehicle isn't moving forward or backward (direction == 0),
        // we can skip TCS. Or handle differently if desired.
        if (CarController.direction == 0)
            return;

        // Scale TCS intensity by stability factor (reduced when handbrake is applied)
        float scaledTCSIntensity = TCSIntensity * currentStabilityFactor;

        // For each powered axle, check forward slip. If it exceeds threshold,
        // and the sign of slip matches the car's direction, reduce torque.
        for (int i = 0; i < poweredAxles.Count; i++) {

            if (poweredAxles[i] == null)
                continue;

            // Left wheel
            if (poweredAxles[i].leftWheelCollider != null) {

                float leftSlip = poweredAxles[i].leftWheelCollider.ForwardSlip;

                if (Mathf.Abs(leftSlip) >= engageTCSThreshold && Mathf.Sign(leftSlip) == CarController.direction) {

                    poweredAxles[i].leftWheelCollider.CutTractionTCS(scaledTCSIntensity);
                    TCSEngaged = true;

                }

            }

            // Right wheel
            if (poweredAxles[i].rightWheelCollider != null) {

                float rightSlip = poweredAxles[i].rightWheelCollider.ForwardSlip;

                if (Mathf.Abs(rightSlip) >= engageTCSThreshold && Mathf.Sign(rightSlip) == CarController.direction) {

                    poweredAxles[i].rightWheelCollider.CutTractionTCS(scaledTCSIntensity);
                    TCSEngaged = true;

                }

            }

        }

    }


    /// <summary>
    /// Returns a normalized [0-1] factor based on how much suspension force
    /// the wheels are applying right now. 0 = all wheels off-ground, 1 = ~static weight.
    /// </summary>
    private float GetWheelForceFactor() {

        if (CarController == null || CarController.Rigid == null)
            return 0f;

        if (CarController.AxleManager == null || CarController.AxleManager.Axles == null)
            return 0f;

        var rb = CarController.Rigid;
        float g = Physics.gravity.magnitude;
        int wheelCount = 0;
        float totalForce = 0f;

        // assume your AxleManager has a list of all axles
        foreach (var axle in CarController.AxleManager.Axles) {

            if (axle == null)
                continue;

            foreach (var wc in new[] { axle.leftWheelCollider, axle.rightWheelCollider }) {

                if (wc == null || wc.WheelCollider == null)
                    continue;

                WheelHit hit;
                if (wc.WheelCollider.GetGroundHit(out hit)) {
                    totalForce += hit.force;  // suspension force this wheel applies
                    wheelCount++;
                }

            }

        }

        if (wheelCount == 0)
            return 0f;

        // static weight per wheel = mass * g / wheelCount
        float referenceForcePerWheel = (rb.mass * g) / wheelCount;
        // average actual force per wheel
        float avgForcePerWheel = totalForce / wheelCount;

        // normalize and clamp
        return Mathf.Clamp01(avgForcePerWheel / referenceForcePerWheel);

    }

    /// <summary>
    /// SteerHelper uses wheel contact positions for geometry-aware physics.
    /// Applies corrective forces at individual wheel contact points based on load.
    /// </summary>
    private void SteerHelper() {

        if (CarController == null || CarController.Rigid == null)
            return;

        if (CarController.Rigid.isKinematic)
            return;

        if (CarController.AxleManager == null || CarController.AxleManager.Axles == null)
            return;

        // get our new physical grounding factor - increased response speed from 3.5f to 5.5f
        wheelForceFactor = Mathf.Lerp(wheelForceFactor, GetWheelForceFactor(), Time.fixedDeltaTime * 5.5f);

        if (!CarController.IsGrounded)
            wheelForceFactor -= Time.fixedDeltaTime * 12f;

        if (wheelForceFactor < 0)
            wheelForceFactor = 0f;

        if (wheelForceFactor < 0.1f)
            return;

        // Get total wheel load and count grounded wheels
        float totalWheelForce = 0f;
        int groundedWheelCount = 0;
        int totalWheelCount = 0;

        foreach (var axle in CarController.AxleManager.Axles) {

            if (axle == null)
                continue;

            WheelHit hit;
            totalWheelCount += 2;

            if (axle.leftWheelCollider != null && axle.leftWheelCollider.WheelCollider != null) {

                if (axle.leftWheelCollider.WheelCollider.GetGroundHit(out hit)) {

                    totalWheelForce += hit.force;
                    groundedWheelCount++;

                }

            }

            if (axle.rightWheelCollider != null && axle.rightWheelCollider.WheelCollider != null) {

                if (axle.rightWheelCollider.WheelCollider.GetGroundHit(out hit)) {

                    totalWheelForce += hit.force;
                    groundedWheelCount++;

                }

            }

        }

        if (totalWheelForce < 0.1f || groundedWheelCount == 0)
            return;

        // Calculate groundedness ratio (1.0 = all wheels grounded, 0.5 = half grounded, etc.)
        float groundednessRatio = (float)groundedWheelCount / (float)totalWheelCount;

        // Scale helper strength based on how many wheels are grounded
        // Full strength at 4 wheels, reduced strength at 2-3 wheels, minimal at 1 wheel
        float groundedScaling = Mathf.Pow(groundednessRatio, 1.5f);

        // Calculate speed-based scaling for steering helper
        // Low speeds: minSpeedHelperStrength (default 20% - nimble, responsive)
        // High speeds: maxSpeedHelperStrength (default 100% - stable)
        // Transition point: fullHelperSpeed (default 80 km/h)
        float speedFactor = Mathf.InverseLerp(0f, fullHelperSpeed, CarController.absoluteSpeed);
        speedFactor = Mathf.Lerp(minSpeedHelperStrength, maxSpeedHelperStrength, speedFactor);

        // High-speed safety: reduce helper strength at high speeds with partial ground contact
        // Prevents flips during bumps/jumps at highway speeds
        if (CarController.absoluteSpeed > highSpeedThreshold && groundedWheelCount < totalWheelCount) {

            float highSpeedSafety = Mathf.Lerp(1f, 0.4f, (CarController.absoluteSpeed - highSpeedThreshold) / highSpeedSafetyRange);
            speedFactor *= highSpeedSafety;

        }

        // Apply speed factor to grounded scaling
        groundedScaling *= speedFactor;

        // Detect sudden wheel lift-off (bump/jump detection)
        int wheelCountChange = Mathf.Abs(groundedWheelCount - previousGroundedWheelCount);
        bool suddenGroundChange = wheelCountChange >= 1;

        // Smooth the grounded scaling to prevent sudden force spikes
        // Slower smoothing during transitions (bumps/jumps), faster during stable driving
        // Increased stable driving speed from 8f to 12f for better responsiveness
        float smoothingSpeed = suddenGroundChange ? 4f : 12f;
        groundedScaling = Mathf.Lerp(previousGroundedScaling, groundedScaling, Time.fixedDeltaTime * smoothingSpeed);

        // Limit maximum change per frame to prevent force spikes
        // Max 15% change per frame = safe, gradual transitions
        float maxChangePerFrame = 0.15f;
        float scalingDelta = groundedScaling - previousGroundedScaling;
        scalingDelta = Mathf.Clamp(scalingDelta, -maxChangePerFrame, maxChangePerFrame);
        groundedScaling = previousGroundedScaling + scalingDelta;

        // Store for next frame
        previousGroundedScaling = groundedScaling;
        previousGroundedWheelCount = groundedWheelCount;

        // Transform current velocity into local space for lateral calculations
        Vector3 localVelocity = transform.InverseTransformDirection(CarController.Rigid.linearVelocity);

        // Apply corrective forces at each wheel contact point
        foreach (var axle in CarController.AxleManager.Axles) {

            if (axle == null)
                continue;

            if (axle.leftWheelCollider != null)
                ApplyWheelSteerHelper(axle.leftWheelCollider, localVelocity, totalWheelForce, groundedScaling);

            if (axle.rightWheelCollider != null)
                ApplyWheelSteerHelper(axle.rightWheelCollider, localVelocity, totalWheelForce, groundedScaling);

        }

        // Global angular velocity damping for stability
        // Scale damping based on groundedness to avoid abnormal behavior when wheels are off ground
        // Also scale by stability factor to reduce damping when handbrake is applied
        float correctedSteerHelper = steerHelperStrength * 1f * wheelForceFactor * groundedScaling * currentStabilityFactor;

        Vector3 angVel = CarController.Rigid.angularVelocity;
        angVel.y *= (1f - (correctedSteerHelper * 0.1f));

        CarController.Rigid.angularVelocity = Vector3.Lerp(
            CarController.Rigid.angularVelocity,
            angVel,
            wheelForceFactor * 0.5f * groundedScaling * currentStabilityFactor
        );

    }

    /// <summary>
    /// Applies steering helper forces at individual wheel contact points.
    /// Uses wheel load to scale force intensity, creating geometry-aware physics.
    /// Scales forces based on how many wheels are grounded to prevent abnormal behavior.
    /// </summary>
    private void ApplyWheelSteerHelper(RCCP_WheelCollider wc, Vector3 localVel, float totalForce, float groundedScaling) {

        if (wc == null || wc.WheelCollider == null)
            return;

        WheelHit hit;

        if (!wc.WheelCollider.GetGroundHit(out hit))
            return;

        // Check if wheel is actually making meaningful contact
        // Adaptive threshold based on speed - lower at low speeds for better response
        float minForceThreshold = Mathf.Lerp(5f, 15f, CarController.absoluteSpeed / 100f);
        if (hit.force < minForceThreshold)
            return;

        // Calculate per-wheel strength based on load distribution
        float wheelLoadFactor = hit.force / totalForce;

        // Add damping when wheel force is low (transitioning state)
        // This prevents sudden force spikes when wheels are lifting off
        float contactStability = Mathf.Clamp01(hit.force / (CarController.Rigid.mass * Physics.gravity.magnitude * 0.25f));

        // Scale by stability factor to reduce steering helper when handbrake is applied
        float helperStrength = steerHelperStrength * 1f * wheelLoadFactor * groundedScaling * contactStability * currentStabilityFactor;

        // Adaptive lateral force scaling based on speed
        // More aggressive at low speeds for better maneuverability, gentler at high speeds for stability
        float lateralForceScale = Mathf.Lerp(2.5f, 1.8f, CarController.absoluteSpeed / fullHelperSpeed);

        // Lateral correction force at contact point
        // Reduces sideways slip by applying force opposite to lateral velocity
        Vector3 targetLateralForce = -transform.right * localVel.x * helperStrength * lateralForceScale;

        // Initialize smoothed force tracking for this wheel if not exists
        if (!smoothedLateralForces.ContainsKey(wc))
            smoothedLateralForces[wc] = Vector3.zero;

        // Smooth the lateral force transition
        smoothedLateralForces[wc] = Vector3.Lerp(
            smoothedLateralForces[wc],
            targetLateralForce,
            Time.fixedDeltaTime * steerHelperForceSmoothing
        );

        // Use Force instead of VelocityChange to prevent instant velocity changes
        // This makes transitions smoother when wheels lose contact
        CarController.Rigid.AddForceAtPosition(smoothedLateralForces[wc], hit.point, ForceMode.Force);

        // Steering assist force at contact point
        // Creates natural yaw torque based on wheelbase geometry
        if (Mathf.Abs(CarController.steerAngle) > minSteerAngleForAssist) {

            // Get the theoretical max steering angle from front axle
            float theoreticalMaxSteerAngle = frontAxle != null ? frontAxle.maxSteerAngle : 40f;

            // Get the effective max steering angle at current speed
            // This accounts for RCCP_Input's steering curve reduction
            // steerInput_P is already processed by the curve, so we can derive the effective max
            float effectiveMaxSteerAngle = theoreticalMaxSteerAngle;

            // If we have a valid steer input, calculate the effective max based on actual angle vs input
            // This gives us the real maximum considering steering curve and limiters
            if (Mathf.Abs(CarController.steerInput_P) > 0.01f) {
                // The actual angle divided by the input gives us the effective max at this speed
                effectiveMaxSteerAngle = Mathf.Abs(CarController.steerAngle / CarController.steerInput_P);
            }

            // Calculate normalized steering factor based on effective maximum
            // This properly accounts for RCCP_Input's modifications
            float normalizedSteer = Mathf.Clamp01(Mathf.Abs(CarController.steerAngle) / effectiveMaxSteerAngle);
            normalizedSteer *= Mathf.Sign(CarController.steerAngle);

            // Scale assist based on speed (more assist at lower speeds, less at high speeds)
            float speedAssistFactor = Mathf.InverseLerp(steerAssistMinSpeed, steerAssistMaxSpeed, CarController.absoluteSpeed);

            // Apply steering assist force
            float steerFactor = normalizedSteer * speedAssistFactor;
            Vector3 targetSteerForce = transform.right * helperStrength * steerFactor * 1.5f;

            // Use Force mode and scale by mass for consistent behavior
            // This prevents sudden torque when wheels lose contact
            targetSteerForce *= CarController.Rigid.mass;

            // Initialize smoothed force tracking for this wheel if not exists
            if (!smoothedSteerForces.ContainsKey(wc))
                smoothedSteerForces[wc] = Vector3.zero;

            // Smooth the steering force transition
            smoothedSteerForces[wc] = Vector3.Lerp(
                smoothedSteerForces[wc],
                targetSteerForce,
                Time.fixedDeltaTime * steerHelperForceSmoothing
            );

            // Apply smoothed steering force
            CarController.Rigid.AddForceAtPosition(smoothedSteerForces[wc], hit.point, ForceMode.Force);

        } else {

            // Reset smoothed steering force when not steering
            if (smoothedSteerForces.ContainsKey(wc))
                smoothedSteerForces[wc] = Vector3.Lerp(smoothedSteerForces[wc], Vector3.zero, Time.fixedDeltaTime * steerHelperForceSmoothing * 2f);

        }

    }


    /// <summary>
    /// Traction helper. Reduces front axle lateral stiffness if the vehicle's
    /// lateral slip or angular velocity is high, preventing spins.
    /// Enhanced with adaptive response based on speed and improved smoothing.
    /// </summary>
    private void TractionHelper() {

        // 1. Basic checks
        if (!CarController.IsGrounded)
            return;                 // Don't apply traction help if car is airborne.

        if (CarController.Rigid == null || CarController.Rigid.isKinematic)
            return;                 // Skip if the Rigidbody is not being simulated normally.

        if (frontAxle == null)
            return;

        // 2. Grab the car's velocity and remove any vertical component
        Vector3 velocity = CarController.Rigid.linearVelocity;
        velocity -= transform.up * Vector3.Dot(velocity, transform.up);

        // Optional: Early out if velocity is nearly zero to avoid undefined directions
        if (velocity.sqrMagnitude < 0.0001f) {

            // Smooth return to full grip
            smoothedTractionStiffness = Mathf.Lerp(smoothedTractionStiffness, 1f, Time.fixedDeltaTime * 8f);
            frontAxle.tractionHelpedSidewaysStiffness = smoothedTractionStiffness;
            return;

        }

        // Normalize to keep only direction
        velocity.Normalize();

        // 3. Calculate the angle between the car's forward vector and the velocity direction
        float crossDot = Vector3.Dot(Vector3.Cross(transform.forward, velocity), transform.up);
        // Clamp to avoid domain errors in Asin if crossDot slightly exceeds [-1..1]
        crossDot = Mathf.Clamp(crossDot, -1f, 1f);

        float angle = -Mathf.Asin(crossDot);

        // 4. Get the yaw (angular velocity around Y axis)
        float angularVelo = CarController.Rigid.angularVelocity.y;

        // 5. Decide whether to reduce front-axle grip
        //    Check if the angle sign is "opposite" the steerAngle sign (angle * steerAngle < 0).
        //    This indicates counter-steering or loss of control
        if (angle * frontAxle.steerAngle < 0) {

            // Adaptive minimum grip based on speed
            // Higher minimum at low speeds (better control), lower at high speeds (easier drift initiation)
            float minGrip = Mathf.Lerp(0.3f, 0.15f, CarController.absoluteSpeed / fullHelperSpeed);

            // Get the theoretical max steering angle
            float theoreticalMaxSteerAngle = frontAxle != null ? frontAxle.maxSteerAngle : 40f;

            // Calculate effective max based on current steering input
            // This accounts for RCCP_Input's steering curve and limiters
            float effectiveMaxSteerAngle = theoreticalMaxSteerAngle;

            if (Mathf.Abs(CarController.steerInput_P) > 0.01f) {
                // Derive effective max from actual angle vs processed input
                effectiveMaxSteerAngle = Mathf.Abs(CarController.steerAngle / CarController.steerInput_P);
            }

            // Calculate normalized steering using the effective maximum
            float normalizedSteerInput = Mathf.Clamp01(Mathf.Abs(frontAxle.steerAngle) / effectiveMaxSteerAngle);

            // Speed-based response scaling
            // More aggressive at high speeds, gentler at low speeds
            float speedResponseScale = Mathf.Lerp(0.7f, 1.2f, CarController.absoluteSpeed / fullHelperSpeed);

            // The higher the angular velocity and steering input, the more we reduce stiffness
            // Enhanced with speed-based scaling for better response across speed ranges
            // Scale by stability factor to reduce traction helper when handbrake is applied
            float clampFactor = Mathf.Clamp01(
                tractionHelperStrength
                * Mathf.Abs(angularVelo)
                * (0.4f + normalizedSteerInput * 0.6f)
                * speedResponseScale
                * currentStabilityFactor
            );

            // Calculate target stiffness
            float targetStiffness = Mathf.Lerp(1f, minGrip, clampFactor);

            // Smooth the stiffness transition for better feel
            // Faster response when reducing grip (entering slide), slower when restoring (exiting slide)
            float smoothSpeed = targetStiffness < smoothedTractionStiffness ? 10f : 6f;
            smoothedTractionStiffness = Mathf.Lerp(smoothedTractionStiffness, targetStiffness, Time.fixedDeltaTime * smoothSpeed);

            frontAxle.tractionHelpedSidewaysStiffness = smoothedTractionStiffness;

        } else {

            // If angles aren't conflicting, restore full grip smoothly
            smoothedTractionStiffness = Mathf.Lerp(smoothedTractionStiffness, 1f, Time.fixedDeltaTime * 8f);
            frontAxle.tractionHelpedSidewaysStiffness = smoothedTractionStiffness;

        }

    }

    /// <summary>
    /// Angular drag helper. Gradually increases Rigidbody's angularDrag
    /// at higher speeds for more stability, but scales it down while
    /// the player is actually steering in the same direction of the
    /// car's turn. Uses a calculated steering scale instead of a fixed factor.
    /// </summary>
    private void AngularDragHelper() {

        if (CarController.Rigid == null || CarController.Rigid.isKinematic)
            return;

        float baseDrag = 0f;
        float maxDrag = 10f;

        float speedFactor = (CarController.absoluteSpeed * angularDragHelperStrength) / 1000f;

        if (!CarController.IsGrounded)
            speedFactor *= 4f;

        speedFactor = Mathf.Clamp01(speedFactor);

        float targetAngularDrag = Mathf.Lerp(baseDrag, maxDrag, speedFactor);

        float steerDifference = Mathf.Abs(CarController.steerInput_V) - Mathf.Abs(CarController.steerInput_P);
        steerDifference *= 100f;
        steerDifference = Mathf.Clamp01(steerDifference);

        if (steerDifference > 0.05f) {

            float steerAmount = Mathf.Clamp01((steerDifference - 0.1f) / (1f - 0.1f));
            float maxSteerDragReduction = 0.6f;
            float steeringDragScale = 1f - (maxSteerDragReduction * steerAmount * 1f);

            targetAngularDrag *= steeringDragScale;

        }

        // Scale by stability factor to reduce angular drag helper when handbrake is applied
        targetAngularDrag *= currentStabilityFactor;

        // Finally apply the computed drag
        CarController.Rigid.angularDamping = Mathf.Lerp(CarController.Rigid.angularDamping, targetAngularDrag, Time.fixedDeltaTime * 2f);

    }


    /// <summary>
    /// Limits the maximum drift angle if it exceeds maxDriftAngle
    /// by damping angular velocity.
    /// </summary>
    private void LimitDriftAngle() {

        if (CarController.Rigid == null)
            return;

        // 1. Acquire current velocity and forward direction
        Vector3 velocity = CarController.Rigid.linearVelocity;
        Vector3 forward = transform.forward;

        // 2. Compute the signed angle (in degrees) between 'forward' and 'velocity',
        //    using Vector3.up as the axis. This effectively measures the yaw angle
        //    relative to the car's forward direction.
        float angle = Vector3.SignedAngle(forward, velocity, Vector3.up);

        // 3. If the absolute drift angle is beyond the desired maxDriftAngle,
        //    we damp the car's angular velocity, pulling it back toward zero rotation.
        //    Scale by stability factor to reduce correction when handbrake is applied
        if (Mathf.Abs(angle) > maxDriftAngle) {

            CarController.Rigid.angularVelocity = Vector3.Lerp(
                CarController.Rigid.angularVelocity,
                Vector3.zero,
                Time.fixedDeltaTime * driftAngleCorrectionFactor * groundedFactor * currentStabilityFactor
            );

        }

    }

    /// <summary>
    /// Resets all stability system states and runtime variables to defaults.
    /// </summary>
    public void Reload() {

        // Main stability system engaged states
        ABSEngaged = false;
        ESPEngaged = false;
        TCSEngaged = false;

        groundedFactor = 0f;
        currentStabilityFactor = 1f;

        // Reset force smoothing values
        previousGroundedScaling = 1f;
        previousGroundedWheelCount = 4;

        // Reset drift state
        driftIntensity = 0f;
        smoothedDriftIntensity = 0f;

        // Reset drift force/torque smoothing
        smoothedDriftTorque = Vector3.zero;
        smoothedDriftForwardForce = Vector3.zero;
        smoothedDriftSidewaysForce = Vector3.zero;

        // Reset ESP brake torque smoothing
        smoothedESPFrontLeftBrake = 0f;
        smoothedESPFrontRightBrake = 0f;
        smoothedESPRearLeftBrake = 0f;
        smoothedESPRearRightBrake = 0f;

        // Reset steer helper force smoothing
        smoothedLateralForces.Clear();
        smoothedSteerForces.Clear();

        // Reset traction helper smoothing
        smoothedTractionStiffness = 1f;
        wheelForceFactor = 0f;

    }

}
