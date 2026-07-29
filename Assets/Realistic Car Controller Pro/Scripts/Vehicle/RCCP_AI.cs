//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright (c) 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// AI driver for RCCP vehicles that relies on Unity NavMesh.
/// Provides four behavior modes for different driving scenarios.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/AI/RCCP AI")]
public class RCCP_AI : RCCP_Component {

    /// <summary>
    /// Available AI behavior modes.
    /// </summary>
    public enum BehaviourType {
        /// <summary>Loops through waypoints at normal speed.</summary>
        FollowWaypoints,
        /// <summary>Races through waypoints aggressively.</summary>
        RaceWaypoints,
        /// <summary>Follows a target at fixed distance.</summary>
        FollowTarget,
        /// <summary>Chases and intercepts a target.</summary>
        ChaseTarget
    }

    #region References

    private RCCP_AIDynamicObstacleAvoidance obstacleAvoidance;

    private NavMeshAgent _agent;
    /// <summary>
    /// NavMesh agent for pathfinding. Auto-created if missing.
    /// </summary>
    private NavMeshAgent Agent {
        get {
            if (_agent == null)
                _agent = GetComponentInChildren<NavMeshAgent>(true);

            if (_agent != null) {
                _agent.gameObject.SetActive(true);
            } else {
                _agent = new GameObject("Agent").AddComponent<NavMeshAgent>();
                _agent.transform.SetParent(transform);
                _agent.transform.localPosition = Vector3.zero;
                _agent.transform.localRotation = Quaternion.identity;
                ConfigureAgent(_agent);
            }
            return _agent;
        }
    }

    #endregion

    #region Settings

    [Header("Behavior")]
    [Tooltip("Select the AI behavior mode: FollowWaypoints, RaceWaypoints, FollowTarget, or ChaseTarget.")]
    public BehaviourType behaviour = BehaviourType.RaceWaypoints;

    [Tooltip("Container holding the list of waypoints for waypoint-based behaviors.")]
    public RCCP_AIWaypointsContainer waypointsContainer;

    [Tooltip("Target Transform for FollowTarget or ChaseTarget behaviors.")]
    public Transform target;

    [Header("Waypoint Settings")]
    [Tooltip("Distance in meters at which a waypoint is considered reached.")]
    public float waypointReachThreshold = 32.5f;

    [Tooltip("Additional look-ahead distance in meters when racing.")]
    public float raceLookAhead = 36f;

    [Header("Driving Settings")]
    [Tooltip("Friction coefficient for safe cornering speed calculation.")]
    public float roadGrip = 1.1f;

    [Tooltip("Maximum throttle input (0 to 1).")]
    [Range(0f, 1f)] public float maxThrottle = 1f;

    [Tooltip("Maximum brake input (0 to 1).")]
    [Range(0f, 1f)] public float maxBrake = 1f;

    [Tooltip("Driving aggressiveness factor.")]
    [Range(0f, 3f)] public float agressiveness = 2f;

    [Tooltip("Steering sensitivity multiplier.")]
    [Range(0f, 5f)] public float steerSensitivity = 3f;

    [Header("Steering Look-ahead")]
    [Tooltip("Minimum look-ahead distance in meters when stationary.")]
    public float minLookAhead = 5f;

    [Tooltip("Additional look-ahead per km/h of speed.")]
    public float lookAheadPerKph = .25f;

    [Header("Corner Awareness")]
    [Tooltip("Corner angle above this value starts reducing look-ahead.")]
    public float mediumCornerAngleThreshold = 25f;

    [Tooltip("Corner angle above this value is treated as a sharp corner.")]
    public float sharpCornerAngleThreshold = 55f;

    [Tooltip("Maximum steering look-ahead used for medium corners.")]
    public float mediumCornerLookAhead = 14f;

    [Tooltip("Maximum steering look-ahead used for sharp corners.")]
    public float sharpCornerLookAhead = 7f;

    [Tooltip("Distance ahead used to inspect upcoming waypoint direction changes.")]
    public float cornerDetectionDistance = 30f;

    [Tooltip("Target speed cap used for sharp corners.")]
    public float sharpCornerTargetSpeed = 55f;

    [Tooltip("Target speed cap used for medium corners.")]
    public float mediumCornerTargetSpeed = 85f;

    [Tooltip("How quickly look-ahead adapts between straight and corner states.")]
    public float lookAheadSmoothSpeed = 6f;

    [Header("Path Recovery")]
    [Tooltip("Lateral path error in meters before the AI starts recovery steering behavior.")]
    public float pathRecoveryDistance = 5f;

    [Tooltip("Short look-ahead used while recovering back to the racing line.")]
    public float recoveryLookAhead = 6f;

    [Tooltip("Extra steering multiplier while the AI is recovering back to the path.")]
    public float recoverySteerBoost = 1.35f;

    [Tooltip("Reduces aggressive angle-braking while the AI is correcting back to the path.")]
    [Range(0f, 1f)] public float recoveryAngleBrakeReduction = .5f;

    [Tooltip("Minimum throttle kept alive during path recovery so the AI doesn't stall too hard.")]
    [Range(0f, 1f)] public float recoveryMinimumThrottle = .12f;

    [Tooltip("Maximum brake allowed during path recovery.")]
    [Range(0f, 1f)] public float recoveryMaxBrake = .4f;

    [Tooltip("Extra throttle added when the AI is exiting a corner cleanly.")]
    [Range(0f, 1f)] public float cornerExitThrottleBoost = .18f;

    [Header("Rubber Band")]
    [Tooltip("Player vehicle used to calculate catch-up assistance.")]
    public Transform rubberBandTarget;

    [Tooltip("Allows the AI to catch up when behind and slows it slightly when far ahead.")]
    public bool useRubberBanding = true;

    [Tooltip("Maximum target-speed multiplier while the AI is behind the player.")]
    [Range(1f, 3f)] public float behindPlayerSpeedMultiplier = 3f;

    [Tooltip("Minimum target-speed multiplier while the AI is ahead of the player.")]
    [Range(.5f, 1f)] public float aheadPlayerSpeedMultiplier = .94f;

    [Tooltip("Distance where rubber-banding reaches its full effect.")]
    public float rubberBandFullEffectDistance = 120f;

    [Tooltip("Maximum engine-power multiplier while catching up.")]
    [Range(1f, 3f)] public float behindPlayerPowerMultiplier = 3f;

    [Tooltip("Maximum wheel-grip multiplier while catching up.")]
    [Range(1f, 3f)] public float behindPlayerHandlingMultiplier = 1.8f;

    [Tooltip("Maximum braking-response multiplier while catching up.")]
    [Range(1f, 3f)] public float behindPlayerBrakeMultiplier = 1.2f;

    [Tooltip("How much farther ahead the AI scans corners at full catch-up boost.")]
    [Range(1f, 4f)] public float behindPlayerCornerPreviewMultiplier = 2.5f;

    [Tooltip("Minimum corner target speed while catch-up assistance is fully active.")]
    public float rubberBandMinimumCornerSpeed = 40f;

    [Tooltip("How quickly power and handling blend toward the requested boost.")]
    public float rubberBandStatsResponse = 2.5f;

    [Header("PID Control")]
    [Tooltip("Proportional gain for speed control.")]
    public float kp = .2f;

    [Tooltip("Integral gain for speed control.")]
    public float ki = .01f;

    [Tooltip("Derivative gain for speed control.")]
    public float kd = .02f;

    [Header("Target Following")]
    [Tooltip("Distance to maintain behind target in FollowTarget mode.")]
    public float followTargetDistance = 5f;

    [Tooltip("Prediction time for intercepting targets in ChaseTarget mode.")]
    public float chasePredictionTime = 1f;

    [Header("State")]
    [Tooltip("Force the AI to stop.")]
    public bool stopNow = false;

    [Tooltip("Force the AI to reverse.")]
    public bool reverseNow = false;

    [Tooltip("Enable stuck detection and recovery.")]
    public bool checkStuck = true;

    [Header("Rollover Recovery")]
    [Tooltip("Enable automatic recovery when the AI remains on its side or roof.")]
    public bool recoverWhenRolledOver = true;

    [Tooltip("Up-vector dot threshold below which the vehicle is considered rolled over.")]
    [Range(-1f, 1f)] public float rolledOverUpDotThreshold = .45f;

    [Tooltip("Maximum speed at which rollover recovery is allowed.")]
    public float rolledOverMaxSpeedKph = 8f;

    [Tooltip("Time the vehicle must remain rolled over before a recovery attempt.")]
    public float rolledOverRecoveryDelay = 2f;

    [Tooltip("Height above the detected ground used for local upright recovery.")]
    public float uprightRecoveryHeight = 1.5f;

    [Tooltip("Height above the detected ground used when moving the AI back to its route.")]
    public float routeRespawnHeight = 2.25f;

    #endregion

    #region Runtime State

    /// <summary>
    /// Current waypoint index the AI is navigating to.
    /// </summary>
    public int currentWaypointIndex;

    /// <summary>
    /// Current AI inputs to be applied to the vehicle.
    /// </summary>
    public RCCP_Inputs inputs = new RCCP_Inputs();

    private BehaviourType previousBehaviour;
    private float stuckTimer;
    private float pidIntegral;
    private float lastSpeedError;
    private float brakeFeedForwardFactor = .25f;
    private float smoothedSteeringLookAhead;
    private float currentRubberBandSpeedMultiplier = 1f;
    private float currentRubberBandStatsMultiplier = 1f;
    private float currentRubberBandCatchUpEffect;
    private float defaultEngineTorque;
    private RCCP_WheelCollider[] rubberBandWheels;
    private float[] defaultWheelGrip;
    private bool rubberBandStatsCached;
    private float rolledOverTimer;
    private float uprightStableTimer;
    private int rolloverRecoveryAttempts;

    private float[] defaultSteerSpeedOfAxle;
    private bool[] defaultInputStates;

    private RCCP_AIBrakeZone currentBrakeZone;

    #endregion

    #region Unity Lifecycle

    public override void Start() {

        base.Start();
        ConfigureAgent(Agent);

    }

    public override void OnEnable() {

        base.OnEnable();

        previousBehaviour = behaviour;
        OnBehaviorChanged();

        if (CarController != null)
            CarController.externalControl = true;

        // Find waypoints container if not assigned
        if (waypointsContainer == null)
            waypointsContainer = FindFirstObjectByType<RCCP_AIWaypointsContainer>(FindObjectsInactive.Include);

        SaveAndApplyInputSettings();
        CacheRubberBandStats();
        smoothedSteeringLookAhead = minLookAhead;

    }

    public override void OnDisable() {

        base.OnDisable();

        if (CarController != null)
            CarController.externalControl = false;

        RestoreInputSettings();
        RestoreRubberBandStats();

    }

    private void FixedUpdate() {

        if (Agent == null || CarController == null)
            return;

        // Check for behavior change
        if (previousBehaviour != behaviour)
            OnBehaviorChanged();
        previousBehaviour = behaviour;

        // Main AI loop
        UpdateDestination();
        UpdateRubberBandStats();
        ComputeControls();

        if (checkStuck)
            HandleStuckVehicle();

        if (recoverWhenRolledOver)
            HandleRolloverRecovery();

        ApplyObstacleAvoidance();

        // Apply inputs to vehicle
        if (CarController.Inputs != null)
            CarController.Inputs.OverrideInputs(inputs);

    }

    #endregion

    #region Initialization

    /// <summary>
    /// Configures the NavMesh agent with optimal settings.
    /// </summary>
    private void ConfigureAgent(NavMeshAgent agent) {

        if (agent == null)
            return;

        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.radius = 1.2f;
        agent.height = 3f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.speed = 60f;
        agent.acceleration = 40f;
        agent.angularSpeed = 720f;

    }

    /// <summary>
    /// Saves current input settings and applies AI-specific settings.
    /// </summary>
    private void SaveAndApplyInputSettings() {

        if (CarController == null || CarController.AxleManager == null)
            return;

        // Save steer speeds
        defaultSteerSpeedOfAxle = new float[CarController.AxleManager.Axles.Count];
        for (int i = 0; i < CarController.AxleManager.Axles.Count; i++) {
            if (CarController.AxleManager.Axles[i] != null) {
                defaultSteerSpeedOfAxle[i] = CarController.AxleManager.Axles[i].steerSpeed;
                CarController.AxleManager.Axles[i].steerSpeed = 10f;
            }
        }

        // Save input settings
        if (CarController.Inputs != null) {
            defaultInputStates = new bool[4];
            defaultInputStates[0] = CarController.Inputs.autoReverse;
            defaultInputStates[1] = CarController.Inputs.inverseThrottleBrakeOnReverse;
            defaultInputStates[2] = CarController.Inputs.counterSteering;
            defaultInputStates[3] = CarController.Inputs.steeringLimiter;

            CarController.Inputs.autoReverse = false;
            CarController.Inputs.inverseThrottleBrakeOnReverse = true;
            CarController.Inputs.counterSteering = false;
            CarController.Inputs.steeringLimiter = false;
        }

    }

    /// <summary>
    /// Restores original input settings when AI is disabled.
    /// </summary>
    private void RestoreInputSettings() {

        if (CarController == null || CarController.AxleManager == null)
            return;

        // Restore steer speeds
        if (defaultSteerSpeedOfAxle != null) {
            for (int i = 0; i < defaultSteerSpeedOfAxle.Length && i < CarController.AxleManager.Axles.Count; i++) {
                if (CarController.AxleManager.Axles[i] != null)
                    CarController.AxleManager.Axles[i].steerSpeed = defaultSteerSpeedOfAxle[i];
            }
        }

        // Restore input settings
        if (CarController.Inputs != null && defaultInputStates != null && defaultInputStates.Length >= 4) {
            CarController.Inputs.autoReverse = defaultInputStates[0];
            CarController.Inputs.inverseThrottleBrakeOnReverse = defaultInputStates[1];
            CarController.Inputs.counterSteering = defaultInputStates[2];
            CarController.Inputs.steeringLimiter = defaultInputStates[3];
        }

    }

    /// <summary>
    /// Called when behavior mode changes.
    /// </summary>
    private void OnBehaviorChanged() {

        stopNow = false;
        reverseNow = false;
        smoothedSteeringLookAhead = minLookAhead;

        if (behaviour == BehaviourType.FollowWaypoints || behaviour == BehaviourType.RaceWaypoints)
            currentWaypointIndex = GetClosestWaypoint();

    }

    #endregion

    #region Destination Management

    /// <summary>
    /// Updates the NavMesh destination based on current behavior.
    /// </summary>
    private void UpdateDestination() {

        switch (behaviour) {

            case BehaviourType.FollowWaypoints:
                UpdateWaypointDestination(false);
                break;

            case BehaviourType.RaceWaypoints:
                UpdateWaypointDestination(true);
                break;

            case BehaviourType.FollowTarget:
                UpdateFollowTargetDestination();
                break;

            case BehaviourType.ChaseTarget:
                UpdateChaseTargetDestination();
                break;

        }

        // Sync agent position with vehicle
        Agent.nextPosition = transform.position;

    }

    /// <summary>
    /// Updates destination for waypoint-based behaviors.
    /// </summary>
    private void UpdateWaypointDestination(bool useRaceLookAhead) {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return;

        int count = waypointsContainer.waypoints.Count;
        float threshSqr = waypointReachThreshold * waypointReachThreshold;

        // Skip waypoints within reach threshold
        while ((CarController.transform.position - waypointsContainer.waypoints[currentWaypointIndex].transform.position).sqrMagnitude < threshSqr) {
            currentWaypointIndex = (currentWaypointIndex + 1) % count;
        }

        if (useRaceLookAhead) {
            // Compute look-ahead point along waypoint path
            Vector3 lookPoint = GetWaypointLookAheadPoint(raceLookAhead);
            Agent.SetDestination(lookPoint);
        } else {
            Agent.SetDestination(waypointsContainer.waypoints[currentWaypointIndex].transform.position);
        }

    }

    /// <summary>
    /// Updates destination for FollowTarget behavior.
    /// </summary>
    private void UpdateFollowTargetDestination() {

        if (target == null)
            return;

        Vector3 desiredPos = target.position - target.forward * followTargetDistance;
        stopNow = Vector3.Distance(desiredPos, CarController.transform.position) < followTargetDistance;
        Agent.SetDestination(desiredPos);

    }

    /// <summary>
    /// Updates destination for ChaseTarget behavior with prediction.
    /// </summary>
    private void UpdateChaseTargetDestination() {

        if (target == null)
            return;

        // Get target velocity
        Vector3 targetVel = Vector3.zero;
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
            targetVel = rb.linearVelocity;

        // Calculate intercept point
        float distance = Vector3.Distance(transform.position, target.position);
        float timeToReach = Agent.speed > 0f ? distance / Agent.speed : 0f;
        float predictT = Mathf.Clamp(timeToReach, 0f, chasePredictionTime);
        Vector3 interceptPoint = target.position + targetVel * predictT;

        Agent.SetDestination(interceptPoint);

    }

    /// <summary>
    /// Finds the closest waypoint to the vehicle, preferring forward-facing ones.
    /// </summary>
    private int GetClosestWaypoint() {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 1)
            return 0;

        int closestAll = 0;
        float closestAllDistance = float.MaxValue;
        int closestFront = -1;
        float closestFrontDistance = float.MaxValue;

        Vector3 carPos = CarController.transform.position;
        Vector3 carFwd = CarController.transform.forward;

        for (int i = 0; i < waypointsContainer.waypoints.Count; i++) {

            var wp = waypointsContainer.waypoints[i];
            if (wp == null)
                continue;

            Vector3 wpPos = wp.transform.position;
            float dist = Vector3.Distance(wpPos, carPos);

            if (dist < closestAllDistance) {
                closestAllDistance = dist;
                closestAll = i;
            }

            Vector3 toWp = wpPos - carPos;
            if (Vector3.Dot(carFwd, toWp) > 0f && dist < closestFrontDistance) {
                closestFrontDistance = dist;
                closestFront = i;
            }

        }

        return closestFront != -1 ? closestFront : closestAll;

    }

    #endregion

    #region Control Computation

    /// <summary>
    /// Computes throttle, brake, and steering inputs.
    /// </summary>
    private void ComputeControls() {

        // Predict future state for smoother control
        PredictFutureState(0.5f, out Vector3 predPos, out Quaternion predRot, out _, out _);

        // Early exit conditions
        if (!Agent.hasPath || stopNow) {
            inputs.steerInput = 0f;
            inputs.throttleInput = 0f;
            inputs.brakeInput = maxBrake;
            inputs.handbrakeInput = 0f;
            return;
        }

        if (reverseNow) {
            inputs.steerInput = 0f;
            inputs.throttleInput = 0f;
            inputs.brakeInput = 1f;
            inputs.handbrakeInput = 0f;
            return;
        }

        // Calculate speed and look-ahead distance
        float speedKph = Mathf.Max(0f, CarController.speed);
        float normalLookAhead = Mathf.Max(minLookAhead, lookAheadPerKph * speedKph);
        float cornerPreviewMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, behindPlayerCornerPreviewMultiplier), currentRubberBandCatchUpEffect);
        float cornerAngle = GetUpcomingCornerAngle(cornerDetectionDistance * cornerPreviewMultiplier);
        float targetSteeringLookAhead = GetCornerAwareLookAhead(normalLookAhead, cornerAngle);
        float pathError = GetCurrentPathError();
        float recoveryStrength = Mathf.InverseLerp(pathRecoveryDistance, pathRecoveryDistance * 2.5f, pathError);
        float cornerSeverity = GetCornerSeverity(cornerAngle);
        targetSteeringLookAhead = Mathf.Lerp(targetSteeringLookAhead, Mathf.Max(minLookAhead, recoveryLookAhead), recoveryStrength);
        float lookAheadBlend = 1f - Mathf.Exp(-Mathf.Max(.01f, lookAheadSmoothSpeed) * Time.fixedDeltaTime);
        smoothedSteeringLookAhead = Mathf.Lerp(
            Mathf.Max(minLookAhead, smoothedSteeringLookAhead),
            Mathf.Max(minLookAhead, targetSteeringLookAhead),
            lookAheadBlend);
        float steeringLookAhead = Mathf.Max(minLookAhead, smoothedSteeringLookAhead);

        // Get steering target
        Vector3 lookPt = GetSteeringLookAheadPoint(steeringLookAhead);
        lookPt = GetRecoveryAwareLookPoint(lookPt, recoveryStrength);
        Vector3 localLook = Quaternion.Inverse(predRot) * (lookPt - predPos);
        float rawSteer = Mathf.Atan2(localLook.x, localLook.z);
        float steerSensitivityScale = Mathf.Lerp(1f, recoverySteerBoost, recoveryStrength);
        float steer = Mathf.Clamp(rawSteer * steerSensitivity * steerSensitivityScale, -1f, 1f);

        // Calculate safe cornering speed
        float speedLookAhead = (behaviour == BehaviourType.RaceWaypoints || behaviour == BehaviourType.ChaseTarget)
            ? raceLookAhead : steeringLookAhead;
        speedLookAhead *= cornerPreviewMultiplier;
        float minRadius = Mathf.Max(1f, GetTightestRadiusAhead(speedLookAhead));
        float aLat = roadGrip * 9.81f;
        float safeSpeedKph = Mathf.Sqrt(aLat * minRadius) * 3.6f;
        safeSpeedKph = Mathf.Min(safeSpeedKph, GetCornerAwareSpeedCap(cornerAngle, safeSpeedKph));
        float cornerSafeSpeedMultiplier = Mathf.Lerp(currentRubberBandSpeedMultiplier, 1f, cornerSeverity);
        safeSpeedKph *= cornerSafeSpeedMultiplier;
        float catchUpMinimumSpeed = Mathf.Lerp(0f, Mathf.Max(0f, rubberBandMinimumCornerSpeed), currentRubberBandCatchUpEffect);
        safeSpeedKph = Mathf.Max(safeSpeedKph, catchUpMinimumSpeed);

        // Cap speed to brake zone target if inside one
        if (currentBrakeZone != null)
            safeSpeedKph = Mathf.Min(safeSpeedKph, currentBrakeZone.targetSpeed);

        // PID speed control
        float error = safeSpeedKph - speedKph;
        pidIntegral += error * Time.fixedDeltaTime;
        float derivative = (error - lastSpeedError) / Time.fixedDeltaTime;
        lastSpeedError = error;

        float controlDivisor = Mathf.Lerp(30f, 10f, agressiveness / 3f);
        float control = kp * error + ki * pidIntegral + kd * derivative;
        float throttle = Mathf.Clamp01(control / controlDivisor) * maxThrottle;
        float brakeResponseMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, behindPlayerBrakeMultiplier), currentRubberBandCatchUpEffect);
        float brakePID = Mathf.Clamp01((-control / controlDivisor) * brakeResponseMultiplier) * maxBrake;

        // Feed-forward brake for overspeed
        float ffBrake = 0f;
        if (speedKph > safeSpeedKph)
            ffBrake = Mathf.Clamp01(((speedKph - safeSpeedKph) / Mathf.Max(1f, safeSpeedKph)) * brakeResponseMultiplier) * brakeFeedForwardFactor;

        // Angle-based brake
        Vector3 dirLook = lookPt - predPos;
        float angleToLook = Vector3.Angle(predRot * Vector3.forward, dirLook);
        float angleBrake = Mathf.Clamp01((angleToLook / Mathf.Lerp(20f, 75f, agressiveness / 3f)) * brakeResponseMultiplier) * maxBrake;
        angleBrake *= Mathf.Lerp(1f, Mathf.Clamp01(1f - recoveryAngleBrakeReduction), recoveryStrength);

        // Combine brakes
        float finalBrake = Mathf.Max(brakePID, ffBrake, angleBrake);

        // Apply brake/throttle logic
        if (finalBrake < 0.3f || speedKph < 25f)
            finalBrake = 0f;
        if (finalBrake >= 0.3f && speedKph >= 25f)
            throttle = 0f;

        // Override brake dead zone for brake zones
        if (currentBrakeZone != null && speedKph > currentBrakeZone.targetSpeed) {
            float overSpeed = (speedKph - currentBrakeZone.targetSpeed) / currentBrakeZone.targetSpeed;
            finalBrake = Mathf.Max(finalBrake, Mathf.Clamp01(overSpeed) * maxBrake);
            throttle = 0f;
        }

        if (recoveryStrength > 0f) {
            finalBrake = Mathf.Min(finalBrake, Mathf.Lerp(finalBrake, recoveryMaxBrake, recoveryStrength));
            throttle = Mathf.Max(throttle, recoveryMinimumThrottle * recoveryStrength);
        }

        float cornerExitReadiness = (1f - recoveryStrength) * (1f - cornerSeverity) * Mathf.InverseLerp(10f, 45f, speedKph);
        if (cornerExitReadiness > 0f && finalBrake < 0.2f)
            throttle = Mathf.Clamp01(throttle + (cornerExitThrottleBoost * cornerExitReadiness));

        float cutThrottle = (speedKph >= 25f) ? finalBrake : 0f;

        // Set final inputs
        inputs.steerInput = Mathf.Clamp(steer, -1f, 1f);
        inputs.throttleInput = Mathf.Clamp01(throttle - cutThrottle);
        inputs.brakeInput = Mathf.Clamp01(finalBrake);
        inputs.handbrakeInput = 0f;

    }

    private float GetRubberBandSpeedMultiplier() {

        if (!useRubberBanding || rubberBandTarget == null || CarController == null)
            return 1f;

        Vector3 toPlayer = rubberBandTarget.position - CarController.transform.position;
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;
        if (distance < 1f)
            return 1f;

        float effect = Mathf.Clamp01(distance / Mathf.Max(1f, rubberBandFullEffectDistance));
        Vector3 carForward = CarController.transform.forward;
        carForward.y = 0f;

        bool playerIsAhead = Vector3.Dot(carForward.normalized, toPlayer / distance) > 0f;
        return playerIsAhead
            ? Mathf.Lerp(1f, Mathf.Max(1f, behindPlayerSpeedMultiplier), effect)
            : Mathf.Lerp(1f, Mathf.Clamp01(aheadPlayerSpeedMultiplier), effect);

    }

    private void CacheRubberBandStats() {

        if (CarController == null)
            return;

        if (CarController.Engine != null)
            defaultEngineTorque = CarController.Engine.maximumTorqueAsNM;

        rubberBandWheels = CarController.AllWheelColliders;
        if (rubberBandWheels != null) {
            defaultWheelGrip = new float[rubberBandWheels.Length];

            for (int i = 0; i < rubberBandWheels.Length; i++)
                defaultWheelGrip[i] = rubberBandWheels[i] != null ? rubberBandWheels[i].grip : 1f;
        }

        rubberBandStatsCached = true;

    }

    private void UpdateRubberBandStats() {

        if (!rubberBandStatsCached)
            CacheRubberBandStats();

        float requestedSpeedMultiplier = GetRubberBandSpeedMultiplier();
        float response = Mathf.Max(.01f, rubberBandStatsResponse) * Time.fixedDeltaTime;
        currentRubberBandSpeedMultiplier = Mathf.MoveTowards(currentRubberBandSpeedMultiplier, requestedSpeedMultiplier, response);

        float catchUpEffect = Mathf.InverseLerp(1f, Mathf.Max(1.01f, behindPlayerSpeedMultiplier), Mathf.Max(1f, requestedSpeedMultiplier));
        currentRubberBandCatchUpEffect = Mathf.MoveTowards(currentRubberBandCatchUpEffect, catchUpEffect, response);
        float targetPowerMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, behindPlayerPowerMultiplier), catchUpEffect);
        currentRubberBandStatsMultiplier = Mathf.MoveTowards(currentRubberBandStatsMultiplier, targetPowerMultiplier, response);

        if (CarController != null && CarController.Engine != null)
            CarController.Engine.maximumTorqueAsNM = defaultEngineTorque * currentRubberBandStatsMultiplier;

        if (rubberBandWheels == null || defaultWheelGrip == null)
            return;

        float handlingBlend = Mathf.InverseLerp(1f, Mathf.Max(1f, behindPlayerPowerMultiplier), currentRubberBandStatsMultiplier);
        float handlingMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, behindPlayerHandlingMultiplier), handlingBlend);

        for (int i = 0; i < rubberBandWheels.Length && i < defaultWheelGrip.Length; i++) {
            if (rubberBandWheels[i] != null)
                rubberBandWheels[i].grip = defaultWheelGrip[i] * handlingMultiplier;
        }

    }

    private void RestoreRubberBandStats() {

        if (!rubberBandStatsCached)
            return;

        if (CarController != null && CarController.Engine != null)
            CarController.Engine.maximumTorqueAsNM = defaultEngineTorque;

        if (rubberBandWheels != null && defaultWheelGrip != null) {
            for (int i = 0; i < rubberBandWheels.Length && i < defaultWheelGrip.Length; i++) {
                if (rubberBandWheels[i] != null)
                    rubberBandWheels[i].grip = defaultWheelGrip[i];
            }
        }

        currentRubberBandSpeedMultiplier = 1f;
        currentRubberBandStatsMultiplier = 1f;
        currentRubberBandCatchUpEffect = 0f;

    }

    /// <summary>
    /// Gets the steering look-ahead point based on behavior type.
    /// </summary>
    private Vector3 GetSteeringLookAheadPoint(float distance) {

        if (behaviour == BehaviourType.FollowWaypoints || behaviour == BehaviourType.RaceWaypoints)
            return GetWaypointLookAheadPoint(distance);
        else
            return GetPathLookAheadPoint(distance);

    }

    /// <summary>
    /// Returns a recovery-adjusted look point when the AI has drifted away from the racing line.
    /// </summary>
    private Vector3 GetRecoveryAwareLookPoint(Vector3 defaultLookPoint, float recoveryStrength) {

        if (recoveryStrength <= 0f || waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return defaultLookPoint;

        int count = waypointsContainer.waypoints.Count;
        int previousIndex = (currentWaypointIndex - 1 + count) % count;
        Vector3 segmentStart = waypointsContainer.waypoints[previousIndex].transform.position;
        Vector3 segmentEnd = waypointsContainer.waypoints[currentWaypointIndex].transform.position;
        Vector3 closestPoint = GetClosestPointOnSegment(segmentStart, segmentEnd, CarController.transform.position);
        Vector3 segmentDirection = (segmentEnd - segmentStart).normalized;
        Vector3 recoveryPoint = closestPoint + segmentDirection * Mathf.Max(2f, recoveryLookAhead);

        return Vector3.Lerp(defaultLookPoint, recoveryPoint, Mathf.Clamp01(recoveryStrength));

    }

    /// <summary>
    /// Measures lateral distance from the AI to the current waypoint segment.
    /// </summary>
    private float GetCurrentPathError() {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0 || CarController == null)
            return 0f;

        int count = waypointsContainer.waypoints.Count;
        int previousIndex = (currentWaypointIndex - 1 + count) % count;
        Vector3 segmentStart = waypointsContainer.waypoints[previousIndex].transform.position;
        Vector3 segmentEnd = waypointsContainer.waypoints[currentWaypointIndex].transform.position;
        Vector3 closestPoint = GetClosestPointOnSegment(segmentStart, segmentEnd, CarController.transform.position);

        return Vector3.Distance(CarController.transform.position, closestPoint);

    }

    /// <summary>
    /// Finds the closest point on a segment.
    /// </summary>
    private Vector3 GetClosestPointOnSegment(Vector3 segmentStart, Vector3 segmentEnd, Vector3 point) {

        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSqr = segment.sqrMagnitude;

        if (segmentLengthSqr <= 0.0001f)
            return segmentStart;

        float t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / segmentLengthSqr);
        return segmentStart + segment * t;

    }

    /// <summary>
    /// Calculates the strongest upcoming waypoint direction change within the scan distance.
    /// </summary>
    private float GetUpcomingCornerAngle(float scanDistance) {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 3)
            return 0f;

        float remainingDistance = Mathf.Max(1f, scanDistance);
        int count = waypointsContainer.waypoints.Count;
        int prevIndex = (currentWaypointIndex - 1 + count) % count;
        Vector3 previousPoint = CarController != null ? CarController.transform.position : transform.position;
        Vector3 currentPoint = waypointsContainer.waypoints[currentWaypointIndex].transform.position;
        Vector3 previousDirection = (currentPoint - previousPoint).normalized;
        float sharpestAngle = 0f;
        int scanIndex = currentWaypointIndex;

        while (remainingDistance > 0f) {

            int nextIndex = (scanIndex + 1) % count;
            Vector3 segmentStart = waypointsContainer.waypoints[scanIndex].transform.position;
            Vector3 segmentEnd = waypointsContainer.waypoints[nextIndex].transform.position;
            Vector3 nextDirection = (segmentEnd - segmentStart).normalized;

            if (previousDirection.sqrMagnitude > 0.001f && nextDirection.sqrMagnitude > 0.001f)
                sharpestAngle = Mathf.Max(sharpestAngle, Vector3.Angle(previousDirection, nextDirection));

            float segmentLength = Vector3.Distance(segmentStart, segmentEnd);
            remainingDistance -= segmentLength;
            previousDirection = nextDirection;
            scanIndex = nextIndex;

            if (scanIndex == prevIndex)
                break;

        }

        return sharpestAngle;

    }

    /// <summary>
    /// Reduces steering look-ahead for medium and sharp corners.
    /// </summary>
    private float GetCornerAwareLookAhead(float normalLookAhead, float cornerAngle) {

        if (cornerAngle < mediumCornerAngleThreshold)
            return normalLookAhead;

        if (cornerAngle < sharpCornerAngleThreshold) {
            float t = Mathf.InverseLerp(mediumCornerAngleThreshold, sharpCornerAngleThreshold, cornerAngle);
            float mediumCap = Mathf.Lerp(normalLookAhead, mediumCornerLookAhead, t);
            return Mathf.Min(normalLookAhead, Mathf.Max(minLookAhead, mediumCap));
        }

        float sharpT = Mathf.InverseLerp(sharpCornerAngleThreshold, 120f, cornerAngle);
        float sharpCap = Mathf.Lerp(mediumCornerLookAhead, sharpCornerLookAhead, sharpT);
        return Mathf.Min(normalLookAhead, Mathf.Max(minLookAhead, sharpCap));

    }

    /// <summary>
    /// Applies an earlier target speed cap for medium and sharp corners.
    /// </summary>
    private float GetCornerAwareSpeedCap(float cornerAngle, float fallbackSafeSpeed) {

        if (cornerAngle < mediumCornerAngleThreshold)
            return fallbackSafeSpeed;

        if (cornerAngle < sharpCornerAngleThreshold) {
            float t = Mathf.InverseLerp(mediumCornerAngleThreshold, sharpCornerAngleThreshold, cornerAngle);
            float mediumTargetSpeed = Mathf.Lerp(fallbackSafeSpeed, mediumCornerTargetSpeed, t);
            return Mathf.Min(fallbackSafeSpeed, mediumTargetSpeed);
        }

        return Mathf.Min(fallbackSafeSpeed, sharpCornerTargetSpeed);

    }

    /// <summary>
    /// Returns a 0-1 severity value for the detected upcoming corner.
    /// </summary>
    private float GetCornerSeverity(float cornerAngle) {

        if (cornerAngle <= mediumCornerAngleThreshold)
            return 0f;

        return Mathf.InverseLerp(mediumCornerAngleThreshold, 100f, cornerAngle);

    }

    /// <summary>
    /// Gets a point along the waypoint path at the specified distance.
    /// </summary>
    private Vector3 GetWaypointLookAheadPoint(float distance) {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return CarController.transform.position + CarController.transform.forward * distance;

        float travelled = 0f;
        int i = currentWaypointIndex;
        int count = waypointsContainer.waypoints.Count;
        Vector3 last = CarController.transform.position;

        while (travelled < distance) {
            Vector3 nextPt = waypointsContainer.waypoints[i].transform.position;
            float seg = Vector3.Distance(last, nextPt);
            if (travelled + seg >= distance)
                return Vector3.Lerp(last, nextPt, (distance - travelled) / seg);
            travelled += seg;
            last = nextPt;
            i = (i + 1) % count;
        }

        return last;

    }

    /// <summary>
    /// Gets a point along the NavMesh path at the specified distance.
    /// </summary>
    private Vector3 GetPathLookAheadPoint(float distance) {

        if (!Agent.hasPath || Agent.path.corners.Length < 2)
            return CarController.transform.position + CarController.transform.forward * distance;

        float travelled = 0f;
        for (int i = 0; i < Agent.path.corners.Length - 1; i++) {

            Vector3 a = Agent.path.corners[i];
            Vector3 b = Agent.path.corners[i + 1];
            float seg = Vector3.Distance(a, b);

            if (travelled + seg > distance) {
                float t = (distance - travelled) / seg;
                return Vector3.Lerp(a, b, t);
            }
            travelled += seg;

        }

        return Agent.path.corners[Agent.path.corners.Length - 1];

    }

    /// <summary>
    /// Calculates the tightest turn radius within the scan distance.
    /// </summary>
    private float GetTightestRadiusAhead(float scanDist) {

        if (!Agent.hasPath || Agent.path.corners.Length < 3)
            return 1000f;

        float minRadius = float.MaxValue;
        float travelled = 0f;

        for (int i = 1; i < Agent.path.corners.Length - 1; i++) {

            Vector3 p0 = Agent.path.corners[i - 1];
            Vector3 p1 = Agent.path.corners[i];
            Vector3 p2 = Agent.path.corners[i + 1];

            travelled += Vector3.Distance(p0, p1);
            if (travelled > scanDist)
                break;

            float a = Vector3.Distance(p0, p1);
            float b = Vector3.Distance(p1, p2);
            float c = Vector3.Distance(p0, p2);

            if (a > 0.1f && b > 0.1f && c > 0.1f) {
                float angle = Mathf.Acos(Mathf.Clamp((a * a + b * b - c * c) / (2f * a * b), -1f, 1f));
                if (angle > 0.01f) {
                    float radius = a / (2f * Mathf.Sin(angle * 0.5f));
                    minRadius = Mathf.Min(minRadius, radius);
                }
            }

        }

        return minRadius == float.MaxValue ? 1000f : Mathf.Max(minRadius, 5f);

    }

    /// <summary>
    /// Predicts future vehicle state using simple integration.
    /// </summary>
    private void PredictFutureState(float dt, out Vector3 predictedPosition, out Quaternion predictedRotation, out Vector3 predictedVelocity, out Vector3 predictedAngularVelocity) {

        predictedVelocity = CarController.Rigid.linearVelocity;
        predictedAngularVelocity = CarController.Rigid.angularVelocity;
        predictedPosition = CarController.transform.position + predictedVelocity * dt;
        predictedRotation = CarController.transform.rotation * Quaternion.Euler(predictedAngularVelocity * Mathf.Rad2Deg * dt);

    }

    #endregion

    #region Stuck Handling

    /// <summary>
    /// Detects and recovers from stuck situations.
    /// </summary>
    private void HandleStuckVehicle() {

        if (!CarController.canControl || reverseNow) {
            stuckTimer = 0f;
            return;
        }

        float speedKph = CarController.absoluteSpeed;

        // Detect stuck: throttle applied but not moving
        if (CarController.direction == 1 && speedKph < 2f && inputs.throttleInput >= 0.3f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        if (stuckTimer > 2f) {
            stuckTimer = 0f;
            StartCoroutine(RecoverFromStuck());
        }

    }

    /// <summary>
    /// Reverses briefly to recover from stuck position.
    /// </summary>
    private IEnumerator RecoverFromStuck() {

        if (CarController.Inputs != null)
            CarController.Inputs.autoReverse = true;

        reverseNow = true;
        yield return new WaitForSeconds(1.5f);
        reverseNow = false;

        if (CarController.Inputs != null)
            CarController.Inputs.autoReverse = false;

        if (CarController.Gearbox != null)
            CarController.Gearbox.ShiftToGear(0);

    }

    private void HandleRolloverRecovery() {

        if (!CarController.canControl || CarController.Rigid == null) {
            rolledOverTimer = 0f;
            uprightStableTimer = 0f;
            return;
        }

        float uprightDot = Vector3.Dot(CarController.transform.up, Vector3.up);
        bool isRolledOver = uprightDot < rolledOverUpDotThreshold &&
                            CarController.absoluteSpeed <= rolledOverMaxSpeedKph;

        if (!isRolledOver) {
            rolledOverTimer = 0f;

            if (uprightDot > .75f) {
                uprightStableTimer += Time.fixedDeltaTime;

                if (uprightStableTimer >= 3f) {
                    rolloverRecoveryAttempts = 0;
                    uprightStableTimer = 3f;
                }
            } else {
                uprightStableTimer = 0f;
            }

            return;
        }

        uprightStableTimer = 0f;
        rolledOverTimer += Time.fixedDeltaTime;

        if (rolledOverTimer < Mathf.Max(.25f, rolledOverRecoveryDelay))
            return;

        bool moveBackToRoute = rolloverRecoveryAttempts > 0;
        RecoverRolledOverVehicle(moveBackToRoute);
        rolloverRecoveryAttempts++;
        rolledOverTimer = 0f;

    }

    private void RecoverRolledOverVehicle(bool moveBackToRoute) {

        Transform vehicleTransform = CarController.transform;
        Vector3 recoveryPosition = vehicleTransform.position;
        Vector3 routeForward = GetRecoveryRouteForward();

        if (moveBackToRoute)
            recoveryPosition = GetRouteRespawnPosition(routeForward);

        float recoveryHeight = moveBackToRoute ? routeRespawnHeight : uprightRecoveryHeight;
        recoveryPosition = GetGroundedRecoveryPosition(recoveryPosition, recoveryHeight);
        Quaternion recoveryRotation = Quaternion.LookRotation(routeForward, Vector3.up);

        vehicleTransform.SetPositionAndRotation(recoveryPosition, recoveryRotation);
        CarController.Rigid.linearVelocity = Vector3.zero;
        CarController.Rigid.angularVelocity = Vector3.zero;
        CarController.Rigid.WakeUp();

        reverseNow = false;
        stuckTimer = 0f;
        pidIntegral = 0f;
        lastSpeedError = 0f;
        inputs.Clear();

        if (CarController.Inputs != null) {
            CarController.Inputs.autoReverse = false;
            CarController.Inputs.OverrideInputs(inputs);
        }

        if (_agent != null && _agent.isActiveAndEnabled)
            _agent.nextPosition = recoveryPosition;

    }

    private Vector3 GetRecoveryRouteForward() {

        Vector3 forward = Vector3.ProjectOnPlane(CarController.transform.forward, Vector3.up).normalized;

        if (waypointsContainer != null &&
            waypointsContainer.waypoints != null &&
            waypointsContainer.waypoints.Count > 1) {
            int count = waypointsContainer.waypoints.Count;
            int nextIndex = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
            int previousIndex = (nextIndex - 1 + count) % count;
            RCCP_Waypoint previous = waypointsContainer.waypoints[previousIndex];
            RCCP_Waypoint next = waypointsContainer.waypoints[nextIndex];

            if (previous != null && next != null) {
                Vector3 segmentForward = Vector3.ProjectOnPlane(
                    next.transform.position - previous.transform.position,
                    Vector3.up).normalized;

                if (segmentForward.sqrMagnitude > .001f)
                    forward = segmentForward;
            }
        }

        return forward.sqrMagnitude > .001f ? forward : Vector3.forward;

    }

    private Vector3 GetRouteRespawnPosition(Vector3 routeForward) {

        if (waypointsContainer == null ||
            waypointsContainer.waypoints == null ||
            waypointsContainer.waypoints.Count == 0)
            return CarController.transform.position;

        int count = waypointsContainer.waypoints.Count;
        int nextIndex = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
        int previousIndex = (nextIndex - 1 + count) % count;
        RCCP_Waypoint previous = waypointsContainer.waypoints[previousIndex];

        return previous != null
            ? previous.transform.position + routeForward * 3f
            : CarController.transform.position;

    }

    private Vector3 GetGroundedRecoveryPosition(Vector3 position, float height) {

        Vector3 rayOrigin = position + Vector3.up * 12f;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            40f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++) {
            Collider hitCollider = hits[i].collider;

            if (hitCollider == null || hitCollider.transform.IsChildOf(CarController.transform))
                continue;

            return hits[i].point + Vector3.up * Mathf.Max(.5f, height);
        }

        return position + Vector3.up * Mathf.Max(.5f, height);

    }

    #endregion

    #region Obstacle Avoidance

    /// <summary>
    /// Applies steering adjustments from obstacle avoidance component.
    /// </summary>
    private void ApplyObstacleAvoidance() {

        if (obstacleAvoidance == null)
            obstacleAvoidance = GetComponent<RCCP_AIDynamicObstacleAvoidance>();

        if (obstacleAvoidance == null || Mathf.Abs(obstacleAvoidance.steerInput) < 0.1f)
            return;

        if (stuckTimer >= 2f)
            return;

        inputs.steerInput += obstacleAvoidance.steerInput * 2f;
        inputs.steerInput = Mathf.Clamp(inputs.steerInput, -1f, 1f);

    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the AI state.
    /// </summary>
    public void Reload() {

        stuckTimer = 0f;
        pidIntegral = 0f;
        lastSpeedError = 0f;
        stopNow = false;
        reverseNow = false;
        currentBrakeZone = null;
        rolledOverTimer = 0f;
        uprightStableTimer = 0f;
        rolloverRecoveryAttempts = 0;

    }

    /// <summary>
    /// Called by RCCP_AIBrakeZone when the vehicle enters a brake zone.
    /// </summary>
    public void EnteredBrakeZone(RCCP_AIBrakeZone zone) {

        currentBrakeZone = zone;

    }

    /// <summary>
    /// Called by RCCP_AIBrakeZone when the vehicle exits a brake zone.
    /// </summary>
    public void ExitedBrakeZone() {

        currentBrakeZone = null;

    }

    #endregion

    #region Editor Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos() {

        if (!Application.isPlaying || Agent == null || !Agent.isActiveAndEnabled || CarController == null)
            return;

        Vector3 carPos = CarController.transform.position + Vector3.up * 0.25f;
        float speedKph = CarController.speed;

        // Behavior & speed label
        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
        style.normal.textColor = Color.white;
        UnityEditor.Handles.Label(carPos + Vector3.up * 1f, $"{behaviour}  |  {speedKph:0} km/h", style);

        // Destination line
        Gizmos.color = Color.green;
        Gizmos.DrawLine(carPos, Agent.destination + Vector3.up * 0.25f);
        Gizmos.DrawWireSphere(Agent.destination + Vector3.up * 0.25f, 0.5f);

        // Current waypoint
        if (waypointsContainer != null && waypointsContainer.waypoints != null && waypointsContainer.waypoints.Count > 0) {
            var nextWp = waypointsContainer.waypoints[currentWaypointIndex].transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(nextWp + Vector3.up * 0.3f, 0.4f);
        }

        // Path corners
        if (Agent.hasPath) {
            Gizmos.color = Color.cyan;
            var pts = Agent.path.corners;
            for (int i = 0; i < pts.Length - 1; i++) {
                Gizmos.DrawLine(pts[i] + Vector3.up * 0.1f, pts[i + 1] + Vector3.up * 0.1f);
                Gizmos.DrawSphere(pts[i] + Vector3.up * 0.1f, 0.2f);
            }
            if (pts.Length > 0)
                Gizmos.DrawSphere(pts[pts.Length - 1] + Vector3.up * 0.1f, 0.2f);
        }

        // Look-ahead point
        float lookDist = Mathf.Max(minLookAhead, lookAheadPerKph * speedKph);
        Vector3 lookPt = GetSteeringLookAheadPoint(lookDist);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(lookPt + Vector3.up * 0.15f, 0.3f);
        Gizmos.DrawLine(carPos, lookPt + Vector3.up * 0.15f);

    }
#endif

    #endregion

    private void Reset() {
        // Ensure NavMesh agent is created when component is added
        NavMeshAgent agentRef = Agent;
    }

}
