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

    [Tooltip("Optional arcade AI preset. If assigned, its values are copied to this AI on enable/reload.")]
    public RCCP_AIArcadePreset arcadePreset;

    [Tooltip("Used when no preset asset is assigned.")]
    public RCCP_AIArcadePreset.Difficulty arcadeDifficulty = RCCP_AIArcadePreset.Difficulty.Medium;

    [Tooltip("Apply built-in Easy / Medium / Hard / Expert values when no preset asset is assigned.")]
    public bool useBuiltInDifficulty = true;

    [Tooltip("Container holding the list of waypoints for waypoint-based behaviors.")]
    public RCCP_AIWaypointsContainer waypointsContainer;

    [Tooltip("Target Transform for FollowTarget or ChaseTarget behaviors.")]
    public Transform target;

    [Header("Waypoint Settings")]
    [Tooltip("Distance in meters at which a waypoint is considered reached.")]
    public float waypointReachThreshold = 25f;

    [Tooltip("Additional look-ahead distance in meters when racing.")]
    public float raceLookAhead = 36f;

    [Tooltip("Minimum waypoint steps to look ahead while racing.")]
    public int minRaceLookAheadSteps = 3;

    [Tooltip("Maximum waypoint steps to look ahead while racing.")]
    public int maxRaceLookAheadSteps = 6;

    [Tooltip("How many km/h add one extra waypoint look-ahead step.")]
    public float speedPerLookAheadStep = 35f;

    [Tooltip("How many waypoint steps ahead are scanned for corner severity.")]
    public int cornerScanSteps = 6;

    [Tooltip("Shortest racing target distance in meters.")]
    public float minRaceTargetDistance = 7f;

    [Tooltip("Longest racing target distance in meters. Keep this short enough so AI doesn't cut across corners.")]
    public float maxRaceTargetDistance = 28f;

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

    [Tooltip("Arcade AI maximum target speed.")]
    public float arcadeMaxSpeedKph = 135f;

    [Tooltip("How hard the AI applies throttle.")]
    [Range(0f, 1f)] public float arcadeAcceleration = .85f;

    [Tooltip("How hard the AI applies brake when over target speed.")]
    [Range(.2f, 3f)] public float brakeSensitivity = 1f;

    [Header("Steering Look-ahead")]
    [Tooltip("Minimum look-ahead distance in meters when stationary.")]
    public float minLookAhead = 5f;

    [Tooltip("Additional look-ahead per km/h of speed.")]
    public float lookAheadPerKph = .25f;

    [Header("Corner Awareness")]
    [Tooltip("Corners below this angle keep the normal speed-based look-ahead.")]
    public float mediumCornerAngleThreshold = 25f;

    [Tooltip("Corners above this angle are treated as sharp corners.")]
    public float sharpCornerAngleThreshold = 55f;

    [Tooltip("Maximum steering look-ahead while approaching a medium corner.")]
    public float mediumCornerLookAhead = 14f;

    [Tooltip("Maximum steering look-ahead while approaching a sharp corner.")]
    public float sharpCornerLookAhead = 7f;

    [Tooltip("Distance ahead of the AI used to scan waypoint direction changes.")]
    public float cornerDetectionDistance = 70f;

    [Tooltip("Target speed while approaching a sharp corner.")]
    public float sharpCornerTargetSpeed = 55f;

    [Tooltip("How quickly steering look-ahead transitions between straight and corner values.")]
    public float lookAheadSmoothSpeed = 8f;

    [Header("Race Intelligence")]
    [Tooltip("Optional extra inside-corner offset. Keep this off when your waypoints already follow the racing line / apex.")]
    public bool useApexSteering = false;

    [Tooltip("Maximum inside offset used for apex steering.")]
    public float apexMaxOffset = 4f;

    [Tooltip("How strongly the AI cuts toward the apex in corners.")]
    [Range(0f, 1f)] public float apexStrength = .12f;

    [Tooltip("How many waypoint segments behind the current progress can be considered when sampling the racing line.")]
    public int pathSearchBackSegments = 2;

    [Tooltip("How many waypoint segments ahead of the current progress can be considered when sampling the racing line.")]
    public int pathSearchForwardSegments = 14;

    [Tooltip("How quickly apex offset blends in and out.")]
    public float apexOffsetSmoothSpeed = 5f;

    [Tooltip("How quickly steering input follows the desired steering value.")]
    public float steeringSmoothSpeed = 5f;

    [Tooltip("Maximum steering input change per second. Prevents front wheels from snapping left/right.")]
    public float steeringMaxChangePerSecond = 2f;

    [Tooltip("Reduces steering when the car already has lateral velocity, preventing left-right weaving.")]
    [Range(0f, 1f)] public float lateralSteeringDamping = .35f;

    [Tooltip("Maximum steering input allowed at high speed.")]
    [Range(.2f, 1f)] public float highSpeedSteerLimit = .65f;

    [Tooltip("Distance from a detected corner where AI starts reducing speed.")]
    public float cornerBrakingDistance = 35f;

    [Tooltip("Player transform used for dynamic race pace. If empty, rubber banding is disabled.")]
    public Transform playerTarget;

    [Tooltip("Allows AI to subtly speed up behind the player and slow down ahead of the player.")]
    public bool useRubberBanding = true;

    [Tooltip("Speed multiplier when AI is far behind the player.")]
    public float behindPlayerSpeedMultiplier = 1.12f;

    [Tooltip("Speed multiplier when AI is far ahead of the player.")]
    public float aheadPlayerSpeedMultiplier = .9f;

    [Tooltip("Distance difference where rubber banding reaches full strength.")]
    public float rubberBandFullEffectDistance = 120f;

    [Tooltip("Extra catch-up multiplier applied only when this AI is behind the player.")]
    public float catchUpAggression = 1.15f;

    [Tooltip("Maximum random pace variation per AI, keeping races less predictable.")]
    [Range(0f, .2f)] public float paceVariation = .06f;

    [Tooltip("Chance for small temporary driving imperfections.")]
    [Range(0f, .2f)] public float mistakeChance = .04f;

    [Header("Arcade Avoidance")]
    public LayerMask avoidanceLayers = ~0;
    public float avoidanceDistance = 22f;
    public float avoidanceSideOffset = 2.2f;
    [Range(0f, 1f)] public float avoidanceBrake = .75f;

    [Tooltip("How many waypoint entries ahead can be used to recover forward progress after collisions.")]
    public int waypointForwardRecoveryScan = 8;

    [Header("Arcade Racing Line")]
    [Tooltip("Stable side offset from the waypoint racing line. Use different values per opponent to reduce collisions.")]
    public float racingLineOffset = 0f;

    [Tooltip("Distance ahead used to calculate the racing line side direction.")]
    public float racingLineDirectionSample = 12f;

    [Header("Arcade Drift Assist")]
    [Tooltip("Helps AI hold controllable arcade slides through sharp corners.")]
    public bool useArcadeDriftAssist = true;

    [Tooltip("Minimum speed before AI is allowed to use drift assist.")]
    public float driftAssistMinSpeed = 35f;

    [Tooltip("Target side slip angle AI tries to hold while sliding.")]
    public float targetDriftAngle = 18f;

    [Tooltip("Slip angle where AI starts preventing spin.")]
    public float spinPreventionAngle = 32f;

    [Tooltip("Minimum throttle while AI is in a controlled slide.")]
    [Range(0f, 1f)] public float driftThrottle = .55f;

    [Tooltip("Short handbrake input used only to start a slide.")]
    [Range(0f, 1f)] public float driftEntryHandbrake = .18f;

    [Tooltip("How strongly AI counter-steers while sliding.")]
    [Range(0f, 1f)] public float driftCounterSteer = .45f;

    [Tooltip("How strongly excessive yaw is damped to prevent spin-outs.")]
    [Range(0f, 1f)] public float driftYawDamping = .45f;

    [Header("Arcade Recovery")]
    [Tooltip("If false, recovery will never teleport the AI to waypoints. Recommended for normal racing.")]
    public bool allowTeleportRecovery = false;

    public float stuckRecoverySeconds = 2.5f;
    public float flippedRecoverySeconds = 1.5f;
    public float offTrackRecoveryDistance = 55f;
    public float offTrackRecoverySeconds = 8f;

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
    private float currentCornerAngle;
    private float currentCornerDistance = float.MaxValue;
    private float currentCornerSign;
    private Vector3 currentCornerPoint;
    private int currentRaceLookAheadSteps = 3;
    private float smoothedSteerInput;
    private float aiPaceOffset;
    private float aiAccelerationMultiplier = 1f;
    private float aiSteeringMultiplier = 1f;
    private float aiBrakeMultiplier = 1f;
    private Vector3 smoothedApexOffset;
    private float flippedTimer;
    private float offTrackTimer;
    private float mistakeTimer;
    private float mistakeSteerOffset;
    private float avoidanceSideMemory;
    private float avoidanceMemoryTimer;
    private float contactAvoidanceTimer;
    private float contactAvoidanceSteer;

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

        ApplyArcadePreset();
        RollArcadeVariation();

        // Find waypoints container if not assigned
        if (waypointsContainer == null)
            waypointsContainer = FindFirstObjectByType<RCCP_AIWaypointsContainer>(FindObjectsInactive.Include);

        SaveAndApplyInputSettings();

    }

    public override void OnDisable() {

        base.OnDisable();

        if (CarController != null)
            CarController.externalControl = false;

        RestoreInputSettings();

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
        ComputeControls();

        if (checkStuck)
            HandleStuckVehicle();

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
                if (!IsAgentReady())
                    return;

                UpdateWaypointDestination(false);
                break;

            case BehaviourType.RaceWaypoints:
                UpdateRaceWaypointProgress();
                break;

            case BehaviourType.FollowTarget:
                if (!IsAgentReady())
                    return;

                UpdateFollowTargetDestination();
                break;

            case BehaviourType.ChaseTarget:
                if (!IsAgentReady())
                    return;

                UpdateChaseTargetDestination();
                break;

        }

        // Sync agent position with vehicle
        if (IsAgentReady())
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
        int safety = 0;

        SynchronizeWaypointProgress();

        // Skip waypoints within reach threshold
        while (safety < count && waypointsContainer.waypoints[currentWaypointIndex] != null && (CarController.transform.position - waypointsContainer.waypoints[currentWaypointIndex].transform.position).sqrMagnitude < threshSqr) {
            currentWaypointIndex = (currentWaypointIndex + 1) % count;
            safety++;
        }

        if (waypointsContainer.waypoints[currentWaypointIndex] == null)
            return;

        if (useRaceLookAhead) {
            // Compute look-ahead point along waypoint path
            Vector3 lookPoint = GetWaypointLookAheadPoint(raceLookAhead);
            TrySetAgentDestination(ApplyRacingLineOffset(lookPoint, raceLookAhead));
        } else {
            TrySetAgentDestination(waypointsContainer.waypoints[currentWaypointIndex].transform.position);
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
        TrySetAgentDestination(desiredPos);

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

        TrySetAgentDestination(interceptPoint);

    }

    private bool IsAgentReady() {

        return Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh;

    }

    private bool TrySetAgentDestination(Vector3 destination) {

        if (!IsAgentReady())
            return false;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            return Agent.SetDestination(hit.position);

        return false;

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
        if (stopNow || !HasDrivingPath()) {
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
        float normalSteeringLookAhead = GetArcadeBaseLookAhead(speedKph);
        float steeringLookAhead = GetCornerAwareSteeringLookAhead(normalSteeringLookAhead);

        // Get steering target
        Vector3 lookPt = GetSteeringLookAheadPoint(steeringLookAhead);
        Vector3 localLook = Quaternion.Inverse(predRot) * (lookPt - predPos);
        float rawSteer = Mathf.Atan2(localLook.x, localLook.z);
        float steer = Mathf.Clamp(rawSteer * steerSensitivity * aiSteeringMultiplier, -1f, 1f);
        steer += GetMistakeSteerOffset();

        float avoidanceSteer = 0f;
        float avoidanceBrakeInput = 0f;
        GetSimpleAvoidanceInputs(out avoidanceSteer, out avoidanceBrakeInput);
        GetContactAvoidanceInputs(out float contactSteer, out float contactBrake);
        avoidanceSteer += contactSteer;
        avoidanceBrakeInput = Mathf.Max(avoidanceBrakeInput, contactBrake);
        steer = Mathf.Clamp(steer + avoidanceSteer, -1f, 1f);
        steer = ApplySteeringStability(steer, speedKph);

        float targetSpeedKph = GetArcadeTargetSpeed(speedKph, Mathf.Abs(steer), GetRacePaceMultiplier());

        // Cap speed to brake zone target if inside one
        if (currentBrakeZone != null)
            targetSpeedKph = Mathf.Min(targetSpeedKph, currentBrakeZone.targetSpeed);

        float speedError = targetSpeedKph - speedKph;
        float throttle = speedError > 2f ? maxThrottle * arcadeAcceleration * aiAccelerationMultiplier : 0f;
        float finalBrake = speedError < -2f ? Mathf.Clamp01((-speedError / 24f) * brakeSensitivity * aiBrakeMultiplier) * maxBrake : 0f;
        finalBrake = Mathf.Max(finalBrake, avoidanceBrakeInput);

        // Override brake dead zone for brake zones
        if (currentBrakeZone != null && speedKph > currentBrakeZone.targetSpeed) {
            float overSpeed = (speedKph - currentBrakeZone.targetSpeed) / currentBrakeZone.targetSpeed;
            finalBrake = Mathf.Max(finalBrake, Mathf.Clamp01(overSpeed) * maxBrake);
            throttle = 0f;
        }

        if (finalBrake > .15f)
            throttle = 0f;

        float handbrake = 0f;
        ApplyArcadeDriftAssist(speedKph, ref steer, ref throttle, ref finalBrake, ref handbrake);

        float finalSteer = GetSmoothedSteerInput(steer);

        // Set final inputs
        inputs.steerInput = Mathf.Clamp(finalSteer, -1f, 1f);
        inputs.throttleInput = Mathf.Clamp01(throttle);
        inputs.brakeInput = Mathf.Clamp01(finalBrake);
        inputs.handbrakeInput = Mathf.Clamp01(handbrake);

    }

    private bool HasDrivingPath() {

        if (behaviour == BehaviourType.RaceWaypoints || behaviour == BehaviourType.FollowWaypoints)
            return waypointsContainer != null && waypointsContainer.waypoints != null && waypointsContainer.waypoints.Count > 0;

        return IsAgentReady() && Agent.hasPath;

    }

    private float GetArcadeBaseLookAhead(float speedKph) {

        float speedLookAhead = Mathf.Lerp(minLookAhead, raceLookAhead, Mathf.InverseLerp(0f, arcadeMaxSpeedKph, speedKph));
        return Mathf.Max(minLookAhead, speedLookAhead);

    }

    private float GetArcadeTargetSpeed(float speedKph, float absSteer, float racePaceMultiplier) {

        float targetSpeed = Mathf.Max(10f, arcadeMaxSpeedKph) * Mathf.Max(.65f, racePaceMultiplier);
        targetSpeed = GetCornerAwareTargetSpeed(targetSpeed, speedKph, racePaceMultiplier);
        float steeringCut = Mathf.Lerp(1f, .82f, Mathf.Clamp01(absSteer));
        return targetSpeed * steeringCut;

    }

    private void ApplyArcadeDriftAssist(float speedKph, ref float steer, ref float throttle, ref float brake, ref float handbrake) {

        if (!useArcadeDriftAssist || CarController == null || CarController.Rigid == null)
            return;

        float cornerFactor = GetCornerProximityFactor(cornerBrakingDistance);
        float steerDemand = Mathf.Abs(steer);

        if (speedKph < driftAssistMinSpeed || (cornerFactor < .15f && steerDemand < .45f))
            return;

        Vector3 localVelocity = CarController.transform.InverseTransformDirection(CarController.Rigid.linearVelocity);

        if (Mathf.Abs(localVelocity.z) < 1f)
            return;

        float slipAngle = Mathf.Atan2(localVelocity.x, Mathf.Abs(localVelocity.z)) * Mathf.Rad2Deg;
        float absSlip = Mathf.Abs(slipAngle);
        float desiredSlip = Mathf.Lerp(targetDriftAngle * .65f, targetDriftAngle, Mathf.Max(cornerFactor, steerDemand));

        throttle = Mathf.Max(throttle, driftThrottle);

        if (absSlip < desiredSlip * .55f && steerDemand > .35f) {
            handbrake = Mathf.Max(handbrake, driftEntryHandbrake * Mathf.Max(cornerFactor, steerDemand));
            brake = Mathf.Min(brake, .15f);
        }

        if (absSlip > desiredSlip) {
            float counterAmount = Mathf.InverseLerp(desiredSlip, spinPreventionAngle, absSlip);
            float counterSteer = Mathf.Sign(slipAngle) * driftCounterSteer * counterAmount;
            steer = Mathf.Clamp(Mathf.Lerp(steer, counterSteer, counterAmount), -1f, 1f);
            handbrake = 0f;
        }

        if (absSlip > spinPreventionAngle) {
            float damping = driftYawDamping * Time.fixedDeltaTime * 8f;
            Vector3 angularVelocity = CarController.Rigid.angularVelocity;
            angularVelocity.y = Mathf.Lerp(angularVelocity.y, angularVelocity.y * .45f, damping);
            CarController.Rigid.angularVelocity = angularVelocity;
            brake = Mathf.Min(brake, .1f);
            throttle = Mathf.Max(throttle, driftThrottle * .85f);
        }

    }

    private float GetMistakeSteerOffset() {

        if (mistakeChance <= 0f)
            return 0f;

        if (mistakeTimer > 0f) {
            mistakeTimer -= Time.fixedDeltaTime;
            return mistakeSteerOffset;
        }

        mistakeSteerOffset = 0f;

        if (Random.value < mistakeChance * Time.fixedDeltaTime) {
            mistakeTimer = Random.Range(.35f, .9f);
            mistakeSteerOffset = Random.Range(-.12f, .12f);
        }

        return mistakeSteerOffset;

    }

    private void GetSimpleAvoidanceInputs(out float steerOffset, out float brakeInput) {

        steerOffset = 0f;
        brakeInput = 0f;

        if (avoidanceDistance <= 0f || CarController == null)
            return;

        Vector3 origin = CarController.transform.position + Vector3.up * .8f + CarController.transform.forward * 2f;
        RaycastHit[] hits = Physics.SphereCastAll(origin, 1.15f, CarController.transform.forward, avoidanceDistance, avoidanceLayers, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return;

        RaycastHit bestHit = new RaycastHit();
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++) {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(CarController.transform))
                continue;

            RCCP_CarController otherVehicle = hit.collider.GetComponentInParent<RCCP_CarController>();

            if (otherVehicle == null)
                continue;

            if (hit.distance < bestDistance) {
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        if (bestDistance == float.MaxValue)
            return;

        Vector3 localHit = CarController.transform.InverseTransformPoint(bestHit.point);
        float side = Mathf.Abs(localHit.x) < .35f ? GetStableAvoidanceSide() : -Mathf.Sign(localHit.x);
        float urgency = 1f - Mathf.Clamp01(bestDistance / Mathf.Max(1f, avoidanceDistance));
        steerOffset = side * avoidanceSideOffset * .035f * urgency;
        brakeInput = avoidanceBrake * Mathf.Lerp(.55f, 1f, urgency);

    }

    private void GetContactAvoidanceInputs(out float steerOffset, out float brakeInput) {

        steerOffset = 0f;
        brakeInput = 0f;

        if (contactAvoidanceTimer <= 0f)
            return;

        contactAvoidanceTimer -= Time.fixedDeltaTime;
        float strength = Mathf.Clamp01(contactAvoidanceTimer / .45f);
        steerOffset = contactAvoidanceSteer * .12f * strength;
        brakeInput = Mathf.Lerp(.25f, .85f, strength);

    }

    private float GetStableAvoidanceSide() {

        if (avoidanceMemoryTimer > 0f) {
            avoidanceMemoryTimer -= Time.fixedDeltaTime;

            if (!Mathf.Approximately(avoidanceSideMemory, 0f))
                return avoidanceSideMemory;
        }

        avoidanceSideMemory = ChooseOpenSide();
        avoidanceMemoryTimer = .75f;
        return avoidanceSideMemory;

    }

    private Vector3 ApplyRacingLineOffset(Vector3 point, float distance) {

        if (Mathf.Abs(racingLineOffset) < .01f || waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 2)
            return point;

        Vector3 aheadPoint = GetWaypointLookAheadPoint(distance + Mathf.Max(3f, racingLineDirectionSample));
        Vector3 forward = FlattenDirection(aheadPoint - point);

        if (forward.sqrMagnitude < .01f)
            forward = FlattenDirection(point - CarController.transform.position);

        if (forward.sqrMagnitude < .01f)
            return point;

        Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
        return point + right * racingLineOffset;

    }

    private Vector3 GetRaceWaypointTargetPoint() {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return CarController.transform.position + CarController.transform.forward * Mathf.Max(5f, minLookAhead);

        float speedKph = CarController != null ? Mathf.Max(0f, CarController.speed) : 0f;
        float speedFactor = Mathf.InverseLerp(0f, Mathf.Max(40f, arcadeMaxSpeedKph), speedKph);
        float targetDistance = Mathf.Lerp(Mathf.Max(3f, minRaceTargetDistance), Mathf.Max(minRaceTargetDistance, maxRaceTargetDistance), speedFactor);

        if (currentCornerAngle >= sharpCornerAngleThreshold)
            targetDistance = Mathf.Min(targetDistance, sharpCornerLookAhead);
        else if (currentCornerAngle >= mediumCornerAngleThreshold)
            targetDistance = Mathf.Min(targetDistance, mediumCornerLookAhead);

        Vector3 targetPoint = GetWaypointLookAheadPoint(targetDistance);
        return ApplyRacingLineOffset(targetPoint, targetDistance);

    }

    private float ChooseOpenSide() {

        Vector3 origin = CarController.transform.position + Vector3.up * .8f + CarController.transform.forward * 2f;
        Vector3 leftOrigin = origin - CarController.transform.right * 1.2f;
        Vector3 rightOrigin = origin + CarController.transform.right * 1.2f;
        bool leftBlocked = Physics.SphereCast(leftOrigin, .8f, CarController.transform.forward, out _, avoidanceDistance * .7f, avoidanceLayers, QueryTriggerInteraction.Ignore);
        bool rightBlocked = Physics.SphereCast(rightOrigin, .8f, CarController.transform.forward, out _, avoidanceDistance * .7f, avoidanceLayers, QueryTriggerInteraction.Ignore);

        if (leftBlocked && !rightBlocked)
            return 1f;

        if (rightBlocked && !leftBlocked)
            return -1f;

        return Random.value > .5f ? 1f : -1f;

    }

    /// <summary>
    /// Gets the steering look-ahead point based on behavior type.
    /// </summary>
    private Vector3 GetSteeringLookAheadPoint(float distance) {

        if (behaviour == BehaviourType.RaceWaypoints)
            return GetRaceWaypointTargetPoint();

        if (behaviour == BehaviourType.FollowWaypoints || behaviour == BehaviourType.RaceWaypoints) {
            Vector3 lookPoint = GetWaypointLookAheadPoint(distance);
            lookPoint = ApplyRacingLineOffset(lookPoint, distance);
            return ApplyApexOffset(lookPoint);
        }
        else
            return GetPathLookAheadPoint(distance);

    }

    private void ApplyArcadePreset() {

        if (arcadePreset == null) {
            if (useBuiltInDifficulty)
                ApplyBuiltInDifficulty();

            return;
        }

        arcadeMaxSpeedKph = arcadePreset.maxSpeedKph;
        arcadeAcceleration = arcadePreset.acceleration;
        steerSensitivity = arcadePreset.steeringSensitivity;
        brakeSensitivity = arcadePreset.brakeSensitivity;
        roadGrip = arcadePreset.grip;
        raceLookAhead = arcadePreset.straightLookAhead;
        mediumCornerLookAhead = arcadePreset.cornerLookAhead;
        sharpCornerLookAhead = arcadePreset.sharpCornerLookAhead;
        cornerDetectionDistance = arcadePreset.cornerDetectionDistance;
        cornerBrakingDistance = arcadePreset.cornerBrakingDistance;
        sharpCornerTargetSpeed = arcadePreset.sharpCornerSpeed;
        paceVariation = arcadePreset.paceVariation;
        mistakeChance = arcadePreset.mistakeChance;
        avoidanceDistance = arcadePreset.avoidanceDistance;
        avoidanceSideOffset = Mathf.Min(avoidanceSideOffset, arcadePreset.avoidanceSideOffset);
        avoidanceBrake = Mathf.Max(avoidanceBrake, arcadePreset.avoidanceBrake);
        stuckRecoverySeconds = arcadePreset.stuckSeconds;
        flippedRecoverySeconds = arcadePreset.flippedSeconds;
        offTrackRecoveryDistance = arcadePreset.offTrackDistance;
        behindPlayerSpeedMultiplier = 1f + arcadePreset.rubberBandStrength;
        aheadPlayerSpeedMultiplier = 1f - (arcadePreset.rubberBandStrength * .35f);
        catchUpAggression = 1f + (arcadePreset.rubberBandStrength * .65f);
        useApexSteering = false;
        ApplyArcadePathDefaults();
        ApplyPresetDifficultyModifier();

    }

    private void ApplyBuiltInDifficulty() {

        switch (arcadeDifficulty) {
            case RCCP_AIArcadePreset.Difficulty.Easy:
                ApplyArcadeValues(120f, .72f, 1f, .9f, 1f, .18f, .11f);
                ApplyCornerPaceValues(48f, 58f, 15f, 8f, 1.05f);
                break;

            case RCCP_AIArcadePreset.Difficulty.Hard:
                ApplyArcadeValues(172f, .98f, 1.35f, 1.18f, 1.24f, .34f, .025f);
                ApplyCornerPaceValues(64f, 50f, 13f, 7f, 1.3f);
                break;

            case RCCP_AIArcadePreset.Difficulty.Expert:
                ApplyArcadeValues(205f, 1f, 1.5f, 1.32f, 1.38f, .44f, .01f);
                ApplyCornerPaceValues(72f, 48f, 12f, 6f, 1.55f);
                break;

            default:
                ApplyArcadeValues(142f, .86f, 1.16f, 1f, 1.1f, .25f, .055f);
                ApplyCornerPaceValues(56f, 54f, 14f, 8f, 1.15f);
                break;
        }

    }

    private void ApplyArcadeValues(float speed, float accelerationValue, float steeringValue, float brakeValue, float gripValue, float rubberBandStrength, float mistakes) {

        arcadeMaxSpeedKph = speed;
        arcadeAcceleration = accelerationValue;
        steerSensitivity = steeringValue;
        brakeSensitivity = brakeValue;
        roadGrip = gripValue;
        mistakeChance = mistakes;
        behindPlayerSpeedMultiplier = 1f + rubberBandStrength;
        aheadPlayerSpeedMultiplier = 1f - (rubberBandStrength * .35f);
        catchUpAggression = 1f + (rubberBandStrength * .65f);
        useApexSteering = false;
        ApplyArcadePathDefaults();

    }

    private void ApplyArcadePathDefaults() {

        if (waypointReachThreshold > 10f)
            waypointReachThreshold = 10f;

        maxRaceLookAheadSteps = Mathf.Min(maxRaceLookAheadSteps, 6);
        speedPerLookAheadStep = Mathf.Max(speedPerLookAheadStep, 35f);
        cornerScanSteps = Mathf.Clamp(cornerScanSteps, 3, 6);
        maxRaceTargetDistance = Mathf.Min(maxRaceTargetDistance, 28f);
        minRaceTargetDistance = Mathf.Min(minRaceTargetDistance, maxRaceTargetDistance);

        avoidanceDistance = Mathf.Max(avoidanceDistance, 22f);
        avoidanceSideOffset = Mathf.Min(avoidanceSideOffset, 2.2f);
        avoidanceBrake = Mathf.Max(avoidanceBrake, .75f);

    }

    private void ApplyCornerPaceValues(float sharpCornerSpeed, float brakingDistance, float mediumLookAhead, float sharpLookAhead, float catchUpValue) {

        sharpCornerTargetSpeed = sharpCornerSpeed;
        cornerBrakingDistance = brakingDistance;
        mediumCornerLookAhead = mediumLookAhead;
        sharpCornerLookAhead = sharpLookAhead;
        catchUpAggression = Mathf.Max(catchUpAggression, catchUpValue);

    }

    private void ApplyPresetDifficultyModifier() {

        switch (arcadeDifficulty) {
            case RCCP_AIArcadePreset.Difficulty.Easy:
                arcadeMaxSpeedKph *= .9f;
                arcadeAcceleration *= .9f;
                sharpCornerTargetSpeed *= .85f;
                cornerBrakingDistance *= 1.15f;
                break;

            case RCCP_AIArcadePreset.Difficulty.Hard:
                arcadeMaxSpeedKph *= 1.14f;
                arcadeAcceleration = Mathf.Min(1f, arcadeAcceleration * 1.08f);
                sharpCornerTargetSpeed *= 1.05f;
                cornerBrakingDistance *= 1.05f;
                behindPlayerSpeedMultiplier = Mathf.Max(behindPlayerSpeedMultiplier, 1.3f);
                catchUpAggression = Mathf.Max(catchUpAggression, 1.32f);
                break;

            case RCCP_AIArcadePreset.Difficulty.Expert:
                arcadeMaxSpeedKph *= 1.32f;
                arcadeAcceleration = Mathf.Min(1f, arcadeAcceleration * 1.16f);
                sharpCornerTargetSpeed *= 1.12f;
                cornerBrakingDistance *= 1.15f;
                mediumCornerLookAhead = Mathf.Min(mediumCornerLookAhead, 12f);
                sharpCornerLookAhead = Mathf.Min(sharpCornerLookAhead, 6f);
                behindPlayerSpeedMultiplier = Mathf.Max(behindPlayerSpeedMultiplier, 1.44f);
                aheadPlayerSpeedMultiplier = Mathf.Min(aheadPlayerSpeedMultiplier, .86f);
                catchUpAggression = Mathf.Max(catchUpAggression, 1.58f);
                break;
        }

    }

    private void RollArcadeVariation() {

        aiPaceOffset = Random.Range(-paceVariation, paceVariation);
        aiAccelerationMultiplier = Random.Range(1f - paceVariation, 1f + paceVariation);
        aiSteeringMultiplier = Random.Range(1f - paceVariation, 1f + paceVariation);
        aiBrakeMultiplier = Random.Range(1f - paceVariation, 1f + paceVariation);

    }

    private void SynchronizeWaypointProgress() {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return;

        int count = waypointsContainer.waypoints.Count;
        int currentIndex = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
        float bestDistance = float.MaxValue;
        int bestOffset = 0;
        int maxScan = Mathf.Clamp(waypointForwardRecoveryScan, 1, count);

        for (int offset = 0; offset <= maxScan; offset++) {
            int index = (currentIndex + offset) % count;
            RCCP_Waypoint waypoint = waypointsContainer.waypoints[index];

            if (waypoint == null)
                continue;

            float distance = Vector3.SqrMagnitude(FlattenPoint(CarController.transform.position) - FlattenPoint(waypoint.transform.position));

            if (distance < bestDistance) {
                bestDistance = distance;
                bestOffset = offset;
            }
        }

        if (bestOffset > 0)
            currentWaypointIndex = (currentIndex + bestOffset) % count;

    }

    private void UpdateRaceWaypointProgress() {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return;

        int count = waypointsContainer.waypoints.Count;
        float threshold = Mathf.Max(2f, waypointReachThreshold);
        float threshSqr = threshold * threshold;
        int safety = 0;

        while (safety < count) {
            int index = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
            RCCP_Waypoint waypoint = waypointsContainer.waypoints[index];
            RCCP_Waypoint nextWaypoint = waypointsContainer.waypoints[(index + 1) % count];

            if (waypoint == null)
                break;

            Vector3 flatCar = FlattenPoint(CarController.transform.position);
            Vector3 flatWaypoint = FlattenPoint(waypoint.transform.position);
            float distanceSqr = Vector3.SqrMagnitude(flatCar - flatWaypoint);

            bool crossedWaypointPlane = false;

            if (nextWaypoint != null) {
                Vector3 segmentDirection = FlattenDirection(nextWaypoint.transform.position - waypoint.transform.position);

                if (segmentDirection.sqrMagnitude > .01f)
                    crossedWaypointPlane = Vector3.Dot(flatCar - flatWaypoint, segmentDirection) > 0f;
            }

            if (distanceSqr > threshSqr && !crossedWaypointPlane)
                break;

            currentWaypointIndex = (currentWaypointIndex + 1) % count;
            safety++;
        }

    }

    /// <summary>
    /// Smoothes steering to prevent front wheels from snapping when the target point changes quickly.
    /// </summary>
    private float GetSmoothedSteerInput(float targetSteer) {

        if (steeringSmoothSpeed <= 0f && steeringMaxChangePerSecond <= 0f) {
            smoothedSteerInput = targetSteer;
            return targetSteer;
        }

        float smoothedTarget = targetSteer;

        if (steeringSmoothSpeed > 0f) {
            float t = 1f - Mathf.Exp(-steeringSmoothSpeed * Time.fixedDeltaTime);
            smoothedTarget = Mathf.Lerp(smoothedSteerInput, targetSteer, t);
        }

        if (steeringMaxChangePerSecond > 0f) {
            float maxDelta = steeringMaxChangePerSecond * Time.fixedDeltaTime;
            smoothedSteerInput = Mathf.MoveTowards(smoothedSteerInput, smoothedTarget, maxDelta);
        } else {
            smoothedSteerInput = smoothedTarget;
        }

        return smoothedSteerInput;

    }

    /// <summary>
    /// Reduces over-correction when the vehicle is already moving sideways or travelling very fast.
    /// </summary>
    private float ApplySteeringStability(float targetSteer, float speedKph) {

        float stableSteer = targetSteer;

        if (CarController.Rigid != null && lateralSteeringDamping > 0f) {
            Vector3 localVelocity = CarController.transform.InverseTransformDirection(CarController.Rigid.linearVelocity);
            float lateralCorrection = Mathf.Clamp(localVelocity.x / 18f, -1f, 1f) * lateralSteeringDamping;
            stableSteer -= lateralCorrection;
        }

        float speedLimit = Mathf.Lerp(1f, highSpeedSteerLimit, Mathf.InverseLerp(50f, 160f, speedKph));
        float cornerLimit = Mathf.Lerp(1f, .75f, Mathf.InverseLerp(mediumCornerAngleThreshold, sharpCornerAngleThreshold, currentCornerAngle));
        float finalLimit = Mathf.Clamp01(speedLimit * cornerLimit);

        return Mathf.Clamp(stableSteer, -finalLimit, finalLimit);

    }

    /// <summary>
    /// Reduces steering look-ahead before sharp waypoint direction changes.
    /// </summary>
    private float GetCornerAwareSteeringLookAhead(float normalLookAhead) {

        currentCornerAngle = behaviour == BehaviourType.RaceWaypoints ? GetRaceUpcomingCornerAngle() : GetUpcomingWaypointCornerAngle();
        currentRaceLookAheadSteps = GetRaceLookAheadSteps(CarController != null ? Mathf.Max(0f, CarController.speed) : 0f);

        float targetLookAhead = normalLookAhead;
        float cornerFactor = GetCornerProximityFactor(cornerDetectionDistance);

        if (currentCornerAngle >= sharpCornerAngleThreshold) {
            targetLookAhead = Mathf.Lerp(normalLookAhead, Mathf.Min(normalLookAhead, sharpCornerLookAhead), cornerFactor);
        } else if (currentCornerAngle >= mediumCornerAngleThreshold) {
            float t = Mathf.InverseLerp(mediumCornerAngleThreshold, sharpCornerAngleThreshold, currentCornerAngle);
            float cornerLookAhead = Mathf.Lerp(mediumCornerLookAhead, sharpCornerLookAhead, t);
            targetLookAhead = Mathf.Lerp(normalLookAhead, Mathf.Min(normalLookAhead, cornerLookAhead), cornerFactor);
        }

        targetLookAhead = Mathf.Max(minLookAhead, targetLookAhead);

        if (smoothedSteeringLookAhead <= 0f)
            smoothedSteeringLookAhead = targetLookAhead;

        if (lookAheadSmoothSpeed <= 0f) {
            smoothedSteeringLookAhead = targetLookAhead;
        } else {
            float t = 1f - Mathf.Exp(-lookAheadSmoothSpeed * Time.fixedDeltaTime);
            smoothedSteeringLookAhead = Mathf.Lerp(smoothedSteeringLookAhead, targetLookAhead, t);
        }

        return smoothedSteeringLookAhead;

    }

    /// <summary>
    /// Limits target speed before medium and sharp waypoint corners.
    /// </summary>
    private float GetCornerAwareTargetSpeed(float normalTargetSpeed, float currentSpeedKph, float racePaceMultiplier) {

        if (currentCornerAngle < mediumCornerAngleThreshold)
            return normalTargetSpeed;

        float safeSharpSpeed = Mathf.Max(5f, sharpCornerTargetSpeed);
        float speedExtraBrakeDistance = Mathf.Max(0f, currentSpeedKph - safeSharpSpeed) * .55f;
        float activeBrakeDistance = Mathf.Max(cornerBrakingDistance, cornerBrakingDistance + speedExtraBrakeDistance);
        float cornerFactor = GetCornerProximityFactor(activeBrakeDistance);

        if (cornerFactor <= 0f)
            return normalTargetSpeed;

        float cornerPaceMultiplier = Mathf.Lerp(1f, Mathf.Max(.8f, racePaceMultiplier), .25f);

        if (currentCornerAngle >= sharpCornerAngleThreshold)
            return Mathf.Lerp(normalTargetSpeed, Mathf.Min(normalTargetSpeed, safeSharpSpeed * cornerPaceMultiplier), cornerFactor);

        float t = Mathf.InverseLerp(mediumCornerAngleThreshold, sharpCornerAngleThreshold, currentCornerAngle);
        float mediumTargetSpeed = Mathf.Lerp(normalTargetSpeed, safeSharpSpeed, t);
        mediumTargetSpeed *= cornerPaceMultiplier;
        return Mathf.Lerp(normalTargetSpeed, Mathf.Min(normalTargetSpeed, mediumTargetSpeed), cornerFactor);

    }

    private int GetRaceLookAheadSteps(float speedKph) {

        int minSteps = Mathf.Max(1, minRaceLookAheadSteps);
        int maxSteps = Mathf.Max(minSteps, maxRaceLookAheadSteps);
        int speedSteps = minSteps + Mathf.FloorToInt(speedKph / Mathf.Max(1f, speedPerLookAheadStep));
        int steps = Mathf.Clamp(speedSteps, minSteps, maxSteps);

        if (currentCornerAngle >= sharpCornerAngleThreshold)
            steps = minSteps;
        else if (currentCornerAngle >= mediumCornerAngleThreshold)
            steps = Mathf.Min(steps, minSteps + 1);

        return steps;

    }

    private float GetCornerProximityFactor(float activeDistance) {

        if (currentCornerAngle < mediumCornerAngleThreshold || currentCornerDistance == float.MaxValue)
            return 0f;

        return 1f - Mathf.InverseLerp(0f, Mathf.Max(1f, activeDistance), currentCornerDistance);

    }

    /// <summary>
    /// Applies a small inside-corner offset so the AI aims closer to the apex instead of the waypoint centerline.
    /// </summary>
    private Vector3 ApplyApexOffset(Vector3 lookPoint) {

        if (!useApexSteering || currentCornerAngle < mediumCornerAngleThreshold)
            return lookPoint + SmoothApexOffset(Vector3.zero);

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 3)
            return lookPoint + SmoothApexOffset(Vector3.zero);

        if (Mathf.Approximately(currentCornerSign, 0f) || currentCornerDistance == float.MaxValue)
            return lookPoint + SmoothApexOffset(Vector3.zero);

        int count = waypointsContainer.waypoints.Count;
        int cornerIndex = GetClosestWaypointIndexToPosition(currentCornerPoint);

        if (cornerIndex < 0)
            return lookPoint + SmoothApexOffset(Vector3.zero);

        RCCP_Waypoint previousWaypoint = waypointsContainer.waypoints[(cornerIndex - 1 + count) % count];
        RCCP_Waypoint cornerWaypoint = waypointsContainer.waypoints[cornerIndex];

        if (previousWaypoint == null || cornerWaypoint == null)
            return lookPoint + SmoothApexOffset(Vector3.zero);

        Vector3 incoming = FlattenDirection(cornerWaypoint.transform.position - previousWaypoint.transform.position);

        if (incoming.sqrMagnitude < 0.01f)
            return lookPoint + SmoothApexOffset(Vector3.zero);

        Vector3 rightOfIncoming = new Vector3(incoming.z, 0f, -incoming.x).normalized;
        Vector3 inside = rightOfIncoming * currentCornerSign;
        float cornerFactor = Mathf.InverseLerp(mediumCornerAngleThreshold, sharpCornerAngleThreshold, currentCornerAngle);
        float distanceToCorner = Vector3.Distance(FlattenPoint(CarController.transform.position), FlattenPoint(currentCornerPoint));
        float proximityFactor = 1f - Mathf.InverseLerp(0f, Mathf.Max(1f, cornerDetectionDistance), distanceToCorner);
        float offset = apexMaxOffset * apexStrength * cornerFactor * Mathf.Clamp01(proximityFactor);

        return lookPoint + SmoothApexOffset(inside * offset);

    }

    private Vector3 SmoothApexOffset(Vector3 targetOffset) {

        if (apexOffsetSmoothSpeed <= 0f) {
            smoothedApexOffset = targetOffset;
        } else {
            float t = 1f - Mathf.Exp(-apexOffsetSmoothSpeed * Time.fixedDeltaTime);
            smoothedApexOffset = Vector3.Lerp(smoothedApexOffset, targetOffset, t);
        }

        return smoothedApexOffset;

    }

    /// <summary>
    /// Adjusts AI target speed relative to the player without making the race fully scripted.
    /// </summary>
    private float GetRacePaceMultiplier() {

        float multiplier = 1f + aiPaceOffset;

        if (!useRubberBanding || playerTarget == null || waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 2)
            return Mathf.Max(.1f, multiplier);

        float progressDelta = GetTrackProgressDistance(playerTarget.position) - GetTrackProgressDistance(CarController.transform.position);
        float t = Mathf.Clamp01(Mathf.Abs(progressDelta) / Mathf.Max(1f, rubberBandFullEffectDistance));

        if (progressDelta > 0f) {
            float behindBoost = Mathf.Max(1f, behindPlayerSpeedMultiplier) * Mathf.Lerp(1f, Mathf.Max(1f, catchUpAggression), t);
            multiplier *= Mathf.Lerp(1f, behindBoost, t);
        } else if (progressDelta < 0f) {
            multiplier *= Mathf.Lerp(1f, Mathf.Min(1f, aheadPlayerSpeedMultiplier), t);
        }

        return Mathf.Clamp(multiplier, .65f, 1.65f);

    }

    /// <summary>
    /// Estimates progress along the waypoint loop in meters.
    /// </summary>
    private float GetTrackProgressDistance(Vector3 position) {

        int closestIndex = GetClosestWaypointIndexToPosition(position);

        if (closestIndex < 0)
            return 0f;

        float progress = 0f;

        for (int i = 0; i < closestIndex; i++) {
            RCCP_Waypoint a = waypointsContainer.waypoints[i];
            RCCP_Waypoint b = waypointsContainer.waypoints[(i + 1) % waypointsContainer.waypoints.Count];

            if (a == null || b == null)
                continue;

            progress += Vector3.Distance(FlattenPoint(a.transform.position), FlattenPoint(b.transform.position));
        }

        RCCP_Waypoint closestWaypoint = waypointsContainer.waypoints[closestIndex];

        if (closestWaypoint != null)
            progress -= Vector3.Distance(FlattenPoint(position), FlattenPoint(closestWaypoint.transform.position));

        return progress;

    }

    private int GetClosestWaypointIndexToPosition(Vector3 position) {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return -1;

        int closestIndex = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < waypointsContainer.waypoints.Count; i++) {
            RCCP_Waypoint waypoint = waypointsContainer.waypoints[i];

            if (waypoint == null)
                continue;

            float distance = Vector3.SqrMagnitude(FlattenPoint(position) - FlattenPoint(waypoint.transform.position));

            if (distance < closestDistance) {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;

    }

    private float GetRaceUpcomingCornerAngle() {

        currentCornerDistance = float.MaxValue;
        currentCornerSign = 0f;
        currentCornerPoint = Vector3.zero;

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 3)
            return 0f;

        int count = waypointsContainer.waypoints.Count;
        int startIndex = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
        int scanSteps = Mathf.Clamp(cornerScanSteps, 2, count - 1);
        float sharpestAngle = 0f;
        float travelled = 0f;

        for (int offset = 0; offset < scanSteps; offset++) {
            RCCP_Waypoint a = waypointsContainer.waypoints[(startIndex + offset) % count];
            RCCP_Waypoint b = waypointsContainer.waypoints[(startIndex + offset + 1) % count];
            RCCP_Waypoint c = waypointsContainer.waypoints[(startIndex + offset + 2) % count];

            if (a == null || b == null || c == null)
                continue;

            Vector3 dirA = FlattenDirection(b.transform.position - a.transform.position);
            Vector3 dirB = FlattenDirection(c.transform.position - b.transform.position);

            if (dirA.sqrMagnitude < .01f || dirB.sqrMagnitude < .01f)
                continue;

            if (offset > 0)
                travelled += Vector3.Distance(FlattenPoint(a.transform.position), FlattenPoint(b.transform.position));
            else
                travelled = Vector3.Distance(FlattenPoint(CarController.transform.position), FlattenPoint(b.transform.position));

            float angle = Vector3.Angle(dirA, dirB);

            if (angle > sharpestAngle) {
                sharpestAngle = angle;
                currentCornerDistance = travelled;
                currentCornerPoint = b.transform.position;
                currentCornerSign = Mathf.Sign(Vector3.Cross(dirA, dirB).y);
            }
        }

        return sharpestAngle;

    }

    /// <summary>
    /// Scans upcoming waypoint direction changes and returns the sharpest corner angle ahead.
    /// </summary>
    private float GetUpcomingWaypointCornerAngle() {

        currentCornerDistance = float.MaxValue;
        currentCornerSign = 0f;
        currentCornerPoint = Vector3.zero;

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 3)
            return 0f;

        int count = waypointsContainer.waypoints.Count;
        int index = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
        RCCP_Waypoint currentWaypoint = waypointsContainer.waypoints[index];

        if (currentWaypoint == null)
            return 0f;

        Vector3 previousPoint = CarController.transform.position;
        Vector3 previousDirection = FlattenDirection(currentWaypoint.transform.position - previousPoint);

        if (previousDirection.sqrMagnitude < 0.01f)
            return 0f;

        float sharpestAngle = 0f;
        float travelled = 0f;
        float scanDistance = Mathf.Max(0f, cornerDetectionDistance);

        for (int scanned = 0; scanned < count && travelled <= scanDistance; scanned++) {
            RCCP_Waypoint cornerWaypoint = waypointsContainer.waypoints[index];
            RCCP_Waypoint nextWaypoint = waypointsContainer.waypoints[(index + 1) % count];

            if (cornerWaypoint == null || nextWaypoint == null)
                break;

            Vector3 cornerPoint = cornerWaypoint.transform.position;
            Vector3 nextPoint = nextWaypoint.transform.position;
            travelled += Vector3.Distance(FlattenPoint(previousPoint), FlattenPoint(cornerPoint));

            Vector3 nextDirection = FlattenDirection(nextPoint - cornerPoint);

            if (nextDirection.sqrMagnitude >= 0.01f) {
                float angle = Vector3.Angle(previousDirection, nextDirection);

                if (angle > sharpestAngle) {
                    sharpestAngle = angle;
                    currentCornerDistance = travelled;
                    currentCornerPoint = cornerPoint;
                    currentCornerSign = Mathf.Sign(Vector3.Cross(previousDirection, nextDirection).y);
                }

                previousDirection = nextDirection;
            }

            previousPoint = cornerPoint;
            index = (index + 1) % count;
        }

        return sharpestAngle;

    }

    private static Vector3 FlattenPoint(Vector3 point) {

        point.y = 0f;
        return point;

    }

    private static Vector3 FlattenDirection(Vector3 direction) {

        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;

    }

    /// <summary>
    /// Gets a point along the waypoint path at the specified distance.
    /// </summary>
    private Vector3 GetWaypointLookAheadPoint(float distance) {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return CarController.transform.position + CarController.transform.forward * distance;

        int count = waypointsContainer.waypoints.Count;

        if (count == 1) {
            RCCP_Waypoint onlyWaypoint = waypointsContainer.waypoints[0];
            return onlyWaypoint != null ? onlyWaypoint.transform.position : CarController.transform.position + CarController.transform.forward * distance;
        }

        int i = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
        Vector3 last = CarController.transform.position;
        float travelled = 0f;
        int safety = 0;

        while (travelled < distance && safety < count + 2) {
            safety++;

            RCCP_Waypoint waypoint = waypointsContainer.waypoints[i];

            if (waypoint == null)
                break;

            Vector3 nextPt = waypoint.transform.position;
            float segmentLength = Vector3.Distance(FlattenPoint(last), FlattenPoint(nextPt));

            if (segmentLength <= 0.01f) {
                i = (i + 1) % count;
                continue;
            }

            if (travelled + segmentLength >= distance)
                return Vector3.Lerp(last, nextPt, (distance - travelled) / segmentLength);

            travelled += segmentLength;
            last = nextPt;
            i = (i + 1) % count;
        }

        return last;

    }

    private bool TryGetClosestPathSample(Vector3 position, out int segmentIndex, out float segmentT, out Vector3 closestPoint) {

        segmentIndex = -1;
        segmentT = 0f;
        closestPoint = position;

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count < 2)
            return false;

        int count = waypointsContainer.waypoints.Count;
        int currentSegment = (Mathf.Clamp(currentWaypointIndex, 0, count - 1) - 1 + count) % count;
        int backSegments = Mathf.Max(0, pathSearchBackSegments);
        int forwardSegments = Mathf.Max(1, pathSearchForwardSegments);
        bool searchWholeLoop = count <= backSegments + forwardSegments + 1;

        if (searchWholeLoop) {
            SearchClosestPathSegments(position, 0, count - 1, true, ref segmentIndex, ref segmentT, ref closestPoint, out _);
        } else {
            SearchClosestPathSegments(position, currentSegment - backSegments, currentSegment + forwardSegments, false, ref segmentIndex, ref segmentT, ref closestPoint, out float windowDistance);

            if (segmentIndex < 0 || windowDistance > offTrackRecoveryDistance * offTrackRecoveryDistance)
                SearchClosestPathSegments(position, 0, count - 1, true, ref segmentIndex, ref segmentT, ref closestPoint, out _);
        }

        return segmentIndex >= 0;

    }

    private void SearchClosestPathSegments(Vector3 position, int startIndex, int endIndex, bool absoluteIndices, ref int segmentIndex, ref float segmentT, ref Vector3 closestPoint, out float closestDistanceSqr) {

        Vector3 flatPosition = FlattenPoint(position);
        closestDistanceSqr = float.MaxValue;
        int count = waypointsContainer.waypoints.Count;

        for (int rawIndex = startIndex; rawIndex <= endIndex; rawIndex++) {
            int i = absoluteIndices ? rawIndex : WrapIndex(rawIndex, count);
            RCCP_Waypoint a = waypointsContainer.waypoints[i];
            RCCP_Waypoint b = waypointsContainer.waypoints[(i + 1) % count];

            if (a == null || b == null)
                continue;

            Vector3 pointA = FlattenPoint(a.transform.position);
            Vector3 pointB = FlattenPoint(b.transform.position);
            Vector3 segment = pointB - pointA;
            float lengthSqr = segment.sqrMagnitude;

            if (lengthSqr <= 0.0001f)
                continue;

            float t = Mathf.Clamp01(Vector3.Dot(flatPosition - pointA, segment) / lengthSqr);
            Vector3 projected = pointA + segment * t;
            float distanceSqr = Vector3.SqrMagnitude(flatPosition - projected);

            if (distanceSqr < closestDistanceSqr) {
                closestDistanceSqr = distanceSqr;
                segmentIndex = i;
                segmentT = t;
                closestPoint = projected;
            }
        }

    }

    private static int WrapIndex(int index, int count) {

        if (count <= 0)
            return 0;

        index %= count;
        return index < 0 ? index + count : index;

    }

    private Vector3 GetPointAheadOnWaypointPath(int segmentIndex, Vector3 startPoint, float distance) {

        int count = waypointsContainer.waypoints.Count;
        int i = Mathf.Clamp(segmentIndex, 0, count - 1);
        Vector3 last = FlattenPoint(startPoint);
        float travelled = 0f;
        int safety = 0;

        while (travelled < distance && safety < count + 2) {
            safety++;

            RCCP_Waypoint nextWaypoint = waypointsContainer.waypoints[(i + 1) % count];

            if (nextWaypoint == null)
                break;

            Vector3 nextPt = FlattenPoint(nextWaypoint.transform.position);
            float seg = Vector3.Distance(last, nextPt);

            if (seg <= 0.01f) {
                i = (i + 1) % count;
                continue;
            }

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

            if (seg <= 0.01f)
                continue;

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

        if (!CarController.canControl) {
            stuckTimer = 0f;
            flippedTimer = 0f;
            offTrackTimer = 0f;
            return;
        }

        float speedKph = CarController.absoluteSpeed;

        // Detect stuck: throttle applied but not moving
        if (CarController.direction == 1 && speedKph < 2f && inputs.throttleInput >= 0.3f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        if (Vector3.Dot(CarController.transform.up, Vector3.up) < .25f)
            flippedTimer += Time.fixedDeltaTime;
        else
            flippedTimer = 0f;

        if (IsTooFarFromRacingLine())
            offTrackTimer += Time.fixedDeltaTime;
        else
            offTrackTimer = 0f;

        if (flippedTimer > flippedRecoverySeconds) {
            stuckTimer = 0f;
            flippedTimer = 0f;
            offTrackTimer = 0f;
            RecoverUprightInPlace();
            return;
        }

        if (stuckTimer > stuckRecoverySeconds) {
            stuckTimer = 0f;
            StartCoroutine(RecoverFromStuck());
            return;
        }

        if (allowTeleportRecovery && offTrackTimer > offTrackRecoverySeconds) {
            stuckTimer = 0f;
            flippedTimer = 0f;
            offTrackTimer = 0f;
            TeleportToRacingLine();
        }

    }

    /// <summary>
    /// Reverses briefly to recover from stuck position.
    /// </summary>
    private IEnumerator RecoverFromStuck() {

        if (reverseNow)
            yield break;

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

    private bool IsTooFarFromRacingLine() {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return false;

        float distance = GetDistanceToWaypointPath(CarController.transform.position);
        return distance > offTrackRecoveryDistance;

    }

    private float GetDistanceToWaypointPath(Vector3 position) {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0)
            return 0f;

        int count = waypointsContainer.waypoints.Count;

        if (count == 1) {
            RCCP_Waypoint onlyWaypoint = waypointsContainer.waypoints[0];
            return onlyWaypoint != null ? Vector3.Distance(FlattenPoint(position), FlattenPoint(onlyWaypoint.transform.position)) : 0f;
        }

        Vector3 flatPosition = FlattenPoint(position);
        float closestDistance = float.MaxValue;

        for (int i = 0; i < count; i++) {
            RCCP_Waypoint a = waypointsContainer.waypoints[i];
            RCCP_Waypoint b = waypointsContainer.waypoints[(i + 1) % count];

            if (a == null || b == null)
                continue;

            Vector3 pointA = FlattenPoint(a.transform.position);
            Vector3 pointB = FlattenPoint(b.transform.position);
            Vector3 closestPoint = ClosestPointOnSegment(flatPosition, pointA, pointB);
            closestDistance = Mathf.Min(closestDistance, Vector3.Distance(flatPosition, closestPoint));
        }

        return closestDistance == float.MaxValue ? 0f : closestDistance;

    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b) {

        Vector3 segment = b - a;
        float lengthSqr = segment.sqrMagnitude;

        if (lengthSqr <= 0.0001f)
            return a;

        float t = Vector3.Dot(point - a, segment) / lengthSqr;
        return a + segment * Mathf.Clamp01(t);

    }

    private void RecoverUprightInPlace() {

        if (CarController == null)
            return;

        Vector3 forward = FlattenDirection(CarController.transform.forward);

        if (forward.sqrMagnitude < .01f)
            forward = Vector3.forward;

        CarController.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (CarController.Rigid != null) {
            CarController.Rigid.angularVelocity = Vector3.zero;
            CarController.Rigid.linearVelocity *= .25f;
            CarController.Rigid.WakeUp();
        }

    }

    private void TeleportToRacingLine() {

        if (waypointsContainer == null || waypointsContainer.waypoints == null || waypointsContainer.waypoints.Count == 0 || CarController == null)
            return;

        int count = waypointsContainer.waypoints.Count;
        int targetIndex = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
        RCCP_Waypoint targetWaypoint = waypointsContainer.waypoints[targetIndex];
        RCCP_Waypoint nextWaypoint = waypointsContainer.waypoints[(targetIndex + 1) % count];

        if (targetWaypoint == null)
            return;

        Vector3 position = targetWaypoint.transform.position + Vector3.up * 1.25f;
        Quaternion rotation = CarController.transform.rotation;

        if (nextWaypoint != null) {
            Vector3 direction = FlattenDirection(nextWaypoint.transform.position - targetWaypoint.transform.position);

            if (direction.sqrMagnitude > .01f)
                rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 20f, NavMesh.AllAreas))
            position = navHit.position + Vector3.up * 1.25f;

        CarController.transform.SetPositionAndRotation(position, rotation);

        if (CarController.Rigid != null) {
            CarController.Rigid.linearVelocity = Vector3.zero;
            CarController.Rigid.angularVelocity = Vector3.zero;
            CarController.Rigid.WakeUp();
        }

        NavMeshAgent agent = GetComponentInChildren<NavMeshAgent>(true);

        if (agent != null && agent.isActiveAndEnabled && NavMesh.SamplePosition(position, out NavMeshHit agentHit, 20f, NavMesh.AllAreas))
            agent.Warp(agentHit.position);

        Reload();

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

        inputs.steerInput += obstacleAvoidance.steerInput * .5f;
        inputs.steerInput = Mathf.Clamp(inputs.steerInput, -1f, 1f);
        inputs.brakeInput = Mathf.Max(inputs.brakeInput, obstacleAvoidance.brakeInput * .5f);

    }

    private void OnCollisionStay(Collision collision) {

        if (collision == null || CarController == null)
            return;

        RCCP_CarController otherVehicle = collision.collider != null ? collision.collider.GetComponentInParent<RCCP_CarController>() : null;

        if (otherVehicle == null || otherVehicle == CarController)
            return;

        Vector3 otherLocalPosition = CarController.transform.InverseTransformPoint(otherVehicle.transform.position);

        if (otherLocalPosition.z < -2f)
            return;

        contactAvoidanceTimer = .45f;
        contactAvoidanceSteer = Mathf.Abs(otherLocalPosition.x) < .35f ? GetStableAvoidanceSide() : -Mathf.Sign(otherLocalPosition.x);

    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the AI state.
    /// </summary>
    public void Reload() {

        ApplyArcadePreset();
        RollArcadeVariation();
        stuckTimer = 0f;
        pidIntegral = 0f;
        lastSpeedError = 0f;
        smoothedSteeringLookAhead = 0f;
        smoothedSteerInput = 0f;
        smoothedApexOffset = Vector3.zero;
        stopNow = false;
        reverseNow = false;
        currentBrakeZone = null;

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
