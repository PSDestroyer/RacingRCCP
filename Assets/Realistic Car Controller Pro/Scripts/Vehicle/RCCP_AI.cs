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
    public float waypointReachThreshold = 25f;

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
        float steeringLookAhead = Mathf.Max(minLookAhead, lookAheadPerKph * speedKph);

        // Get steering target
        Vector3 lookPt = GetSteeringLookAheadPoint(steeringLookAhead);
        Vector3 localLook = Quaternion.Inverse(predRot) * (lookPt - predPos);
        float rawSteer = Mathf.Atan2(localLook.x, localLook.z);
        float steer = Mathf.Clamp(rawSteer * steerSensitivity, -1f, 1f);

        // Calculate safe cornering speed
        float speedLookAhead = (behaviour == BehaviourType.RaceWaypoints || behaviour == BehaviourType.ChaseTarget)
            ? raceLookAhead : steeringLookAhead;
        float minRadius = Mathf.Max(1f, GetTightestRadiusAhead(speedLookAhead));
        float aLat = roadGrip * 9.81f;
        float safeSpeedKph = Mathf.Sqrt(aLat * minRadius) * 3.6f;

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
        float brakePID = Mathf.Clamp01(-control / controlDivisor) * maxBrake;

        // Feed-forward brake for overspeed
        float ffBrake = 0f;
        if (speedKph > safeSpeedKph)
            ffBrake = Mathf.Clamp01((speedKph - safeSpeedKph) / safeSpeedKph) * brakeFeedForwardFactor;

        // Angle-based brake
        Vector3 dirLook = lookPt - predPos;
        float angleToLook = Vector3.Angle(predRot * Vector3.forward, dirLook);
        float angleBrake = Mathf.Clamp01(angleToLook / Mathf.Lerp(20f, 75f, agressiveness / 3f)) * maxBrake;

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

        float cutThrottle = (speedKph >= 25f) ? finalBrake : 0f;

        // Set final inputs
        inputs.steerInput = Mathf.Clamp(steer, -1f, 1f);
        inputs.throttleInput = Mathf.Clamp01(throttle - cutThrottle);
        inputs.brakeInput = Mathf.Clamp01(finalBrake);
        inputs.handbrakeInput = 0f;

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
