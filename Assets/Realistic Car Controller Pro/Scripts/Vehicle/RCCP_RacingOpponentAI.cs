using UnityEngine;

[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/AI/RCCP Racing Opponent AI")]
public class RCCP_RacingOpponentAI : MonoBehaviour {

    [Header("References")]
    public RCCP_CarController car;
    public ArcadeVP.WaypointCircuit waypointCircuit;
    public RCCP_AIWaypointsContainer waypointsContainer;
    public Transform playerTarget;

    [Header("Route Progress")]
    public int currentWaypointIndex;
    public float progressDistance;
    public float progressResyncInterval = .4f;
    public int routeSearchSamples = 160;
    public float localProgressSearchDistance = 18f;
    public int localProgressSearchSamples = 48;

    [Header("Look Ahead")]
    public float lookAheadForTargetOffset = 5f;
    public float lookAheadForTargetFactor = .11f;
    public float lookAheadForSpeedOffset = 28f;
    public float lookAheadForSpeedFactor = .18f;
    public float minLookAheadDistance = 4f;
    public float maxLookAheadDistance = 16f;
    public float brakePreviewDistance = 18f;
    public float brakePreviewFactor = .12f;

    [Header("Speed")]
    public float maxSpeedKph = 145f;
    [Range(0f, 1f)] public float acceleration = .9f;
    [Range(.2f, 3f)] public float brakeSensitivity = 1.1f;
    public float brakeAngle = 28f;
    public float mediumCornerAngle = 25f;
    public float sharpCornerAngle = 55f;
    public float mediumCornerSpeedKph = 90f;
    public float sharpCornerSpeedKph = 48f;
    public float minimumMoveSpeedKph = 18f;
    public float cornerBrakeAggression = 1.25f;

    [Header("Steering")]
    public float steeringSensitivity = 1.55f;
    public float steeringSmoothTime = .12f;
    public float maxSteerChangePerSecond = 6f;
    [Range(.2f, 1f)] public float highSpeedSteerLimit = .82f;
    public float straightSteerDeadZone = .045f;
    public float straightSteerAssist = .35f;

    [Header("Racing Line")]
    public float laneOffset = 0f;
    public float maxLaneOffsetInCorners = .35f;
    public float insideCornerOffset = .8f;
    public float laneVariationRange = .35f;

    [Header("Rubber Band")]
    public bool useRubberBanding = true;
    public float behindPlayerSpeedMultiplier = 1.16f;
    public float aheadPlayerSpeedMultiplier = .94f;
    public float rubberBandFullEffectDistance = 120f;

    [Header("Consistency")]
    [Range(0f, .2f)] public float paceVariation = .04f;
    [Range(0f, .2f)] public float mistakeChance = .03f;
    [Range(0f, .2f)] public float mistakeStrength = .06f;
    public float mistakeDuration = 1.25f;

    [Header("Avoidance")]
    public LayerMask obstacleLayers = ~0;
    public LayerMask vehicleLayers = ~0;
    public float frontSensorDistance = 16f;
    public float sideSensorDistance = 5f;
    public float sensorRadius = .55f;
    public float wallSlowSpeedKph = 42f;
    [Range(0f, 1f)] public float obstacleBrake = .3f;
    [Range(0f, 1f)] public float vehicleBrake = .06f;
    public float obstacleSteerStrength = .18f;
    public float vehicleSteerStrength = .28f;
    public float minimumTrafficSpeedKph = 24f;
    public float trafficLaneOffset = 1.35f;
    public float trafficBiasDuration = 2.2f;
    public float overtakeSpeedBonusKph = 12f;
    public float overtakeBrakeReduction = .45f;
    [Range(0f, 1f)] public float playerBrake = .16f;
    public float playerRespectSpeedKph = 34f;

    [Header("Recovery")]
    public float stuckSpeedKph = 4f;
    public float stuckSeconds = 3f;
    public float reverseStuckSeconds = 1.1f;
    public float uprightAssistSeconds = 2.5f;
    public float wallStuckSeconds = 1.4f;
    public float hardResetStuckSeconds = 5f;

    public RCCP_Inputs inputs = new RCCP_Inputs();

    private float currentSteer;
    private float steerVelocity;
    private float resyncTimer;
    private float stuckTimer;
    private float reverseTimer;
    private float upsideDownTimer;
    private float wallStuckTimer;
    private float sensorSteer;
    private float sensorBrake;
    private float sensorSpeedLimit;
    private bool trafficBlocked;
    private bool frontStaticBlocked;
    private float frontStaticUrgency;
    private float frontStaticEscapeSide;
    private float paceMultiplier = 1f;
    private float mistakeTimer;
    private float mistakeSpeedMultiplier = 1f;
    private float mistakeSteerOffset;
    private float nextMistakeCheckTime;
    private float trafficSideBias;
    private float trafficBiasTimer;
    private bool leftVehicleBlocked;
    private bool rightVehicleBlocked;
    private float frontVehicleUrgency;
    private float personalityLaneOffset;
    private float personalityBrakeBias = 1f;
    private float personalityCornerSpeedBias = 1f;
    private float personalitySteerBias = 1f;
    private float personalityOvertakeBias = 1f;

    private void Awake() {

        if (car == null)
            car = GetComponent<RCCP_CarController>();

    }

    private void OnEnable() {

        if (car == null)
            car = GetComponent<RCCP_CarController>();

        Reload();

    }

    private void OnDisable() {

        if (car != null && car.Inputs != null)
            car.Inputs.DisableOverrideInputs();

    }

    private void FixedUpdate() {

        if (!IsReady()) {
            inputs.Clear();
            return;
        }

        if (!car.canControl) {
            inputs.Clear();
            car.Inputs.OverrideInputs(inputs);
            return;
        }

        EnsureVehicleReady();
        UpdateRouteProgress();
        ReadSensors();

        RouteFrame route = GetRouteFrame();
        UpdateDriverConsistency();
        float targetSteer = CalculateSteer(route.targetPoint, route.direction, route.cornerAngle) + sensorSteer;
        float targetSpeed = CalculateTargetSpeed(route.cornerAngle, route.previewCornerAngle, Mathf.Abs(targetSteer));
        targetSpeed = Mathf.Min(targetSpeed, sensorSpeedLimit);

        ApplyInputs(targetSteer, targetSpeed);
        HandleRecovery();

    }

    public void Reload() {

        EnsureVehicleReady();

        if (waypointCircuit != null)
            waypointCircuit.RebuildRoute();

        progressDistance = FindClosestDistanceOnCircuit(transform.position);
        currentWaypointIndex = FindClosestCircuitWaypoint(transform.position);
        currentSteer = 0f;
        steerVelocity = 0f;
        resyncTimer = 0f;
        stuckTimer = 0f;
        reverseTimer = 0f;
        upsideDownTimer = 0f;
        wallStuckTimer = 0f;
        trafficSideBias = 0f;
        trafficBiasTimer = 0f;
        frontStaticBlocked = false;
        frontStaticUrgency = 0f;
        frontStaticEscapeSide = 0f;
        ResetDriverConsistency();
        inputs.Clear();

    }

    private bool IsReady() {

        return car != null
            && car.Inputs != null
            && waypointCircuit != null
            && waypointCircuit.Waypoints != null
            && waypointCircuit.Waypoints.Length > 1;

    }

    private void UpdateRouteProgress() {

        ArcadeVP.WaypointCircuit.RoutePoint progressPoint = waypointCircuit.GetRoutePoint(progressDistance);
        Vector3 flatDirection = Flatten(progressPoint.direction).normalized;
        Vector3 flatDelta = Flatten(transform.position - progressPoint.position);

        if (flatDirection.sqrMagnitude > .001f)
            progressDistance += Mathf.Max(0f, Vector3.Dot(flatDelta, flatDirection));

        resyncTimer -= Time.fixedDeltaTime;

        if (resyncTimer <= 0f) {
            float searchForwardDistance = Mathf.Clamp(car.speed * .02f, 1.5f, 5f);
            float searchCenter = progressDistance + searchForwardDistance;
            progressDistance = FindClosestDistanceNear(transform.position, searchCenter, localProgressSearchDistance, localProgressSearchSamples);
            currentWaypointIndex = FindClosestCircuitWaypoint(waypointCircuit.GetRoutePosition(progressDistance));
            resyncTimer = progressResyncInterval;
        }

    }

    private RouteFrame GetRouteFrame() {

        float speed = Mathf.Max(0f, car.speed);
        float lookAhead = Mathf.Clamp(
            lookAheadForTargetOffset + lookAheadForTargetFactor * speed,
            minLookAheadDistance,
            maxLookAheadDistance);

        ArcadeVP.WaypointCircuit.RoutePoint currentPoint = waypointCircuit.GetRoutePoint(progressDistance);
        float midLookAhead = Mathf.Lerp(minLookAheadDistance, lookAhead, .5f);
        ArcadeVP.WaypointCircuit.RoutePoint midPoint = waypointCircuit.GetRoutePoint(progressDistance + midLookAhead);
        ArcadeVP.WaypointCircuit.RoutePoint targetPoint = waypointCircuit.GetRoutePoint(progressDistance + lookAhead);
        float speedPreviewDistance = lookAheadForSpeedOffset + lookAheadForSpeedFactor * speed;
        float brakePreviewLookAhead = speedPreviewDistance + (brakePreviewDistance * personalityBrakeBias) + brakePreviewFactor * speed;
        ArcadeVP.WaypointCircuit.RoutePoint speedPoint = waypointCircuit.GetRoutePoint(progressDistance + speedPreviewDistance);
        ArcadeVP.WaypointCircuit.RoutePoint previewPoint = waypointCircuit.GetRoutePoint(progressDistance + brakePreviewLookAhead);
        float cornerAngle = Vector3.Angle(currentPoint.direction, speedPoint.direction);
        float previewCornerAngle = Vector3.Angle(currentPoint.direction, previewPoint.direction);

        if (cornerAngle >= sharpCornerAngle)
            targetPoint = waypointCircuit.GetRoutePoint(progressDistance + minLookAheadDistance);
        else if (cornerAngle >= mediumCornerAngle)
            targetPoint = waypointCircuit.GetRoutePoint(progressDistance + Mathf.Lerp(minLookAheadDistance, lookAhead, .35f));

        Vector3 target = targetPoint.position;
        Vector3 routeRight = new Vector3(targetPoint.direction.z, 0f, -targetPoint.direction.x).normalized;

        if (routeRight.sqrMagnitude > .01f) {
            float cornerT = Mathf.InverseLerp(mediumCornerAngle, sharpCornerAngle, cornerAngle);
            float laneLimit = Mathf.Lerp(1.1f, maxLaneOffsetInCorners, cornerT);
            float offset = Mathf.Clamp(laneOffset + personalityLaneOffset, -laneLimit, laneLimit);
            offset += GetCornerLineOffset(currentPoint.direction, midPoint.direction, speedPoint.direction, routeRight, cornerT);
            offset += GetTrafficOffset(cornerT);
            target += routeRight * offset;
        }

        return new RouteFrame(target, targetPoint.direction, cornerAngle, previewCornerAngle);

    }

    private float GetCornerLineOffset(Vector3 currentDirection, Vector3 midDirection, Vector3 futureDirection, Vector3 routeRight, float cornerT) {

        if (cornerT <= 0f || midDirection.sqrMagnitude < .01f || futureDirection.sqrMagnitude < .01f)
            return 0f;

        Vector3 currentFlat = Flatten(currentDirection).normalized;
        Vector3 midFlat = Flatten(midDirection).normalized;
        Vector3 futureFlat = Flatten(futureDirection).normalized;

        float totalAngle = Vector3.Angle(currentFlat, futureFlat);

        if (totalAngle < .1f)
            return 0f;

        Vector3 directionDelta = (futureFlat - currentFlat).normalized;
        float turnSide = Mathf.Clamp(Vector3.Dot(directionDelta, routeRight), -1f, 1f);

        if (Mathf.Abs(turnSide) < .05f)
            return 0f;

        float midAngle = Vector3.Angle(currentFlat, midFlat);
        float apexBlend = Mathf.Clamp01(midAngle / totalAngle);

        float outsideOffset = -Mathf.Sign(turnSide) * insideCornerOffset * 1.1f * cornerT;
        float insideOffset = Mathf.Sign(turnSide) * insideCornerOffset * cornerT;

        return Mathf.Lerp(outsideOffset, insideOffset, apexBlend);

    }

    private float CalculateSteer(Vector3 targetPoint, Vector3 targetDirection, float cornerAngle) {

        Vector3 localTarget = transform.InverseTransformPoint(targetPoint);
        localTarget.y = 0f;

        if (localTarget.sqrMagnitude < .01f)
            return 0f;

        Vector3 flatForward = Flatten(transform.forward).normalized;
        Vector3 flatTargetDirection = Flatten(targetDirection).normalized;
        float lateralComponent = localTarget.x / Mathf.Max(1f, Mathf.Abs(localTarget.z));
        float headingComponent = flatTargetDirection.sqrMagnitude > .001f
            ? Mathf.Clamp(Vector3.SignedAngle(flatForward, flatTargetDirection, Vector3.up) / 45f, -1f, 1f)
            : 0f;
        float cornerT = Mathf.InverseLerp(mediumCornerAngle, sharpCornerAngle, cornerAngle);
        float lateralWeight = Mathf.Lerp(1.15f, 1.35f, cornerT);
        float headingWeight = Mathf.Lerp(.8f, 1.2f, cornerT);

        float rawSteer = (lateralComponent * lateralWeight + headingComponent * headingWeight) * steeringSensitivity * personalitySteerBias;
        float speedLimit = Mathf.Lerp(1f, highSpeedSteerLimit, Mathf.InverseLerp(80f, 180f, Mathf.Max(0f, car.speed)));
        return Mathf.Clamp(rawSteer, -speedLimit, speedLimit);

    }

    private float CalculateTargetSpeed(float cornerAngle, float previewCornerAngle, float absSteer) {

        float target = maxSpeedKph;
        float angleT = Mathf.InverseLerp(mediumCornerAngle, sharpCornerAngle, cornerAngle);
        float previewAngleT = Mathf.InverseLerp(mediumCornerAngle, sharpCornerAngle, previewCornerAngle);

        if (cornerAngle >= mediumCornerAngle)
            target = Mathf.Lerp(maxSpeedKph, Mathf.Lerp(mediumCornerSpeedKph, sharpCornerSpeedKph, angleT) * personalityCornerSpeedBias, angleT);

        if (cornerAngle >= brakeAngle)
            target = Mathf.Min(target, Mathf.Lerp(mediumCornerSpeedKph, sharpCornerSpeedKph, Mathf.InverseLerp(brakeAngle, sharpCornerAngle, cornerAngle)) * personalityCornerSpeedBias);

        if (previewCornerAngle >= mediumCornerAngle) {
            float previewTarget = Mathf.Lerp(maxSpeedKph, Mathf.Lerp(mediumCornerSpeedKph, sharpCornerSpeedKph, previewAngleT) * personalityCornerSpeedBias, previewAngleT);
            target = Mathf.Min(target, Mathf.Lerp(maxSpeedKph, previewTarget, Mathf.Clamp01(previewAngleT * cornerBrakeAggression)));
        }

        if (frontVehicleUrgency > .05f && trafficBiasTimer > 0f && (!leftVehicleBlocked || !rightVehicleBlocked))
            target += overtakeSpeedBonusKph * personalityOvertakeBias * frontVehicleUrgency;

        target *= Mathf.Lerp(1f, .86f, Mathf.Clamp01(absSteer));
        target *= paceMultiplier;
        target *= GetRubberBandMultiplier();
        target *= mistakeSpeedMultiplier;
        return Mathf.Max(minimumMoveSpeedKph, target);

    }

    private void ReadSensors() {

        sensorSteer = 0f;
        sensorBrake = 0f;
        sensorSpeedLimit = float.MaxValue;
        trafficBlocked = false;
        frontStaticBlocked = false;
        frontStaticUrgency = 0f;
        frontStaticEscapeSide = 0f;
        leftVehicleBlocked = false;
        rightVehicleBlocked = false;
        frontVehicleUrgency = 0f;
        trafficBiasTimer = Mathf.Max(0f, trafficBiasTimer - Time.fixedDeltaTime);

        Vector3 origin = transform.position + Vector3.up * 1.05f + transform.forward * 2f;

        if (TrySphereCastIgnoringSelf(origin, sensorRadius, transform.forward, out RaycastHit frontHit, frontSensorDistance, obstacleLayers))
            ApplyFrontSensor(frontHit);

        Vector3 sideOrigin = transform.position + Vector3.up * 1f;

        if (TrySphereCastIgnoringSelf(sideOrigin, sensorRadius * .75f, transform.right, out RaycastHit rightHit, sideSensorDistance, obstacleLayers))
            ApplySideSensor(rightHit, 1f);

        if (TrySphereCastIgnoringSelf(sideOrigin, sensorRadius * .75f, -transform.right, out RaycastHit leftHit, sideSensorDistance, obstacleLayers))
            ApplySideSensor(leftHit, -1f);

    }

    private void ApplyFrontSensor(RaycastHit hit) {

        if (ShouldIgnoreHit(hit))
            return;

        float urgency = 1f - Mathf.Clamp01(hit.distance / Mathf.Max(.01f, frontSensorDistance));
        RCCP_CarController otherCar = hit.collider.GetComponentInParent<RCCP_CarController>();
        float side = GetAvoidanceSide(hit);

        if (otherCar != null) {
            trafficBlocked = true;
            frontVehicleUrgency = Mathf.Max(frontVehicleUrgency, urgency);
            UpdateTrafficBias(GetPreferredTrafficSide(side), urgency);
            sensorSteer += side * vehicleSteerStrength * Mathf.Lerp(.65f, 1.15f, urgency);
            bool isPlayer = IsPlayerVehicle(otherCar);
            float trafficBrake = isPlayer
                ? playerBrake
                : (leftVehicleBlocked && rightVehicleBlocked) ? vehicleBrake * 1.6f : vehicleBrake * overtakeBrakeReduction;
            sensorBrake = Mathf.Max(sensorBrake, trafficBrake * urgency);
            float clearSideTargetSpeed = minimumTrafficSpeedKph + 42f;
            float blockedTargetSpeed = minimumTrafficSpeedKph + 12f;
            float trafficTargetSpeed = isPlayer
                ? Mathf.Min(playerRespectSpeedKph, blockedTargetSpeed + 10f)
                : (leftVehicleBlocked && rightVehicleBlocked) ? blockedTargetSpeed : clearSideTargetSpeed;
            sensorSpeedLimit = Mathf.Min(sensorSpeedLimit, Mathf.Lerp(maxSpeedKph, trafficTargetSpeed, urgency * .65f));
            return;
        }

        sensorBrake = Mathf.Max(sensorBrake, obstacleBrake * urgency);
        sensorSpeedLimit = Mathf.Min(sensorSpeedLimit, Mathf.Lerp(maxSpeedKph, wallSlowSpeedKph, urgency));
        frontStaticBlocked = true;
        frontStaticUrgency = Mathf.Max(frontStaticUrgency, urgency);
        frontStaticEscapeSide = side != 0f ? side : GetAvoidanceSide(hit);
        sensorSteer += frontStaticEscapeSide * obstacleSteerStrength * Mathf.Lerp(.7f, 1.25f, urgency);

    }

    private void ApplySideSensor(RaycastHit hit, float sensorSide) {

        if (ShouldIgnoreHit(hit))
            return;

        float urgency = 1f - Mathf.Clamp01(hit.distance / Mathf.Max(.01f, sideSensorDistance));
        RCCP_CarController otherCar = hit.collider.GetComponentInParent<RCCP_CarController>();

        if (otherCar != null) {
            trafficBlocked = true;
            if (sensorSide > 0f)
                rightVehicleBlocked = true;
            else
                leftVehicleBlocked = true;

            UpdateTrafficBias(-sensorSide, urgency * .8f);
            float sideSteerStrength = IsPlayerVehicle(otherCar) ? 1.1f : .8f;
            sensorSteer -= sensorSide * vehicleSteerStrength * sideSteerStrength * urgency;
            return;
        }

        sensorSteer -= sensorSide * obstacleSteerStrength * .18f * urgency;
        sensorSpeedLimit = Mathf.Min(sensorSpeedLimit, Mathf.Lerp(maxSpeedKph, wallSlowSpeedKph + 12f, urgency));

    }

    private void ApplyInputs(float targetSteer, float targetSpeed) {

        targetSteer = Mathf.Clamp(targetSteer + mistakeSteerOffset, -1f, 1f);

        if (!frontStaticBlocked && frontVehicleUrgency < .05f && Mathf.Abs(targetSteer) < .18f) {
            targetSteer = Mathf.Abs(targetSteer) < straightSteerDeadZone
                ? 0f
                : Mathf.Lerp(targetSteer, 0f, straightSteerAssist);
        }

        if (reverseTimer > 0f) {
            reverseTimer -= Time.fixedDeltaTime;
            float reverseSide = frontStaticEscapeSide != 0f ? frontStaticEscapeSide : -Mathf.Sign(currentSteer == 0f ? 1f : currentSteer);
            targetSteer = reverseSide * .85f;
        }

        float smoothedSteer = Mathf.SmoothDamp(currentSteer, targetSteer, ref steerVelocity, steeringSmoothTime);
        currentSteer = Mathf.MoveTowards(currentSteer, smoothedSteer, maxSteerChangePerSecond * Time.fixedDeltaTime);

        float speedError = targetSpeed - Mathf.Max(0f, car.speed);
        float throttle = speedError > 2f ? acceleration : 0f;
        float brake = speedError < -3f ? Mathf.Clamp01((-speedError / 28f) * brakeSensitivity) : 0f;
        brake = Mathf.Max(brake, sensorBrake);

        if (trafficBlocked && car.speed < minimumTrafficSpeedKph) {
            brake = Mathf.Min(brake, .08f);
            throttle = Mathf.Max(throttle, acceleration * .45f);
        }

        if (reverseTimer > 0f) {
            throttle = 0f;
            brake = 1f;
        }

        if (brake > .12f)
            throttle = 0f;

        inputs.steerInput = Mathf.Clamp(currentSteer, -1f, 1f);
        inputs.throttleInput = Mathf.Clamp01(throttle);
        inputs.brakeInput = Mathf.Clamp01(brake);
        inputs.handbrakeInput = 0f;
        inputs.clutchInput = 0f;
        inputs.nosInput = 0f;

        car.Inputs.OverrideInputs(inputs);

    }

    private void HandleRecovery() {

        bool wantsToMove = inputs.throttleInput > .2f;

        if (wantsToMove && car.speed < stuckSpeedKph)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        if (wantsToMove && frontStaticBlocked && car.speed < stuckSpeedKph + 1f)
            wallStuckTimer += Time.fixedDeltaTime;
        else
            wallStuckTimer = 0f;

        if (stuckTimer >= stuckSeconds) {
            reverseTimer = Mathf.Max(reverseTimer, reverseStuckSeconds);
            stuckTimer = 0f;
            progressDistance = FindClosestDistanceOnCircuit(transform.position);
        }

        if (wallStuckTimer >= wallStuckSeconds) {
            reverseTimer = Mathf.Max(reverseTimer, reverseStuckSeconds * 1.75f);
            currentSteer = Mathf.Clamp(frontStaticEscapeSide == 0f ? currentSteer : frontStaticEscapeSide, -.95f, .95f);
            wallStuckTimer = 0f;
        }

        if (frontStaticBlocked && stuckTimer + wallStuckTimer >= hardResetStuckSeconds)
            RecoverToRoute();

        if (Vector3.Dot(transform.up, Vector3.up) < .25f)
            upsideDownTimer += Time.fixedDeltaTime;
        else
            upsideDownTimer = 0f;

        if (upsideDownTimer >= uprightAssistSeconds) {
            ArcadeVP.WaypointCircuit.RoutePoint routePoint = waypointCircuit.GetRoutePoint(progressDistance);
            transform.rotation = Quaternion.LookRotation(Flatten(routePoint.direction).normalized, Vector3.up);
            transform.position += Vector3.up * .65f;

            if (car.Rigid != null) {
                car.Rigid.linearVelocity *= .25f;
                car.Rigid.angularVelocity = Vector3.zero;
                car.Rigid.WakeUp();
            }

            upsideDownTimer = 0f;
        }

    }

    private void RecoverToRoute() {

        if (waypointCircuit == null || car == null)
            return;

        ArcadeVP.WaypointCircuit.RoutePoint routePoint = waypointCircuit.GetRoutePoint(progressDistance);
        Vector3 forward = Flatten(routePoint.direction).normalized;

        if (forward.sqrMagnitude < .001f)
            forward = Flatten(transform.forward).normalized;

        Vector3 offset = Vector3.zero;

        if (frontStaticEscapeSide != 0f) {
            Vector3 routeRight = new Vector3(forward.z, 0f, -forward.x).normalized;
            offset = routeRight * frontStaticEscapeSide * 1.25f;
        }

        transform.SetPositionAndRotation(routePoint.position + offset + Vector3.up * .55f, Quaternion.LookRotation(forward, Vector3.up));

        if (car.Rigid != null) {
            car.Rigid.linearVelocity = Vector3.zero;
            car.Rigid.angularVelocity = Vector3.zero;
            car.Rigid.WakeUp();
        }

        stuckTimer = 0f;
        wallStuckTimer = 0f;
        reverseTimer = reverseStuckSeconds;
        progressDistance = FindClosestDistanceOnCircuit(transform.position);
        currentWaypointIndex = FindClosestCircuitWaypoint(transform.position);

    }

    private float FindClosestDistanceOnCircuit(Vector3 position) {

        if (waypointCircuit == null || waypointCircuit.Length <= 0f)
            return 0f;

        int samples = Mathf.Max(16, routeSearchSamples);
        float bestDistance = 0f;
        float bestSqrDistance = float.MaxValue;
        Vector3 flatPosition = Flatten(position);

        for (int i = 0; i < samples; i++) {
            float distance = waypointCircuit.Length * i / samples;
            Vector3 routePosition = Flatten(waypointCircuit.GetRoutePosition(distance));
            float sqrDistance = Vector3.SqrMagnitude(flatPosition - routePosition);

            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestDistance = distance;
        }

        return bestDistance;

    }

    private float FindClosestDistanceNear(Vector3 position, float centerDistance, float searchDistance, int samples) {

        if (waypointCircuit == null || waypointCircuit.Length <= 0f)
            return 0f;

        float length = waypointCircuit.Length;
        float bestDistance = Mathf.Repeat(centerDistance, length);
        float bestSqrDistance = float.MaxValue;
        Vector3 flatPosition = Flatten(position);
        int safeSamples = Mathf.Max(8, samples);
        float halfRange = Mathf.Max(minLookAheadDistance, searchDistance) * .5f;

        for (int i = 0; i <= safeSamples; i++) {
            float t = safeSamples == 0 ? 0f : i / (float)safeSamples;
            float candidateDistance = centerDistance - halfRange + searchDistance * t;
            float wrappedDistance = Mathf.Repeat(candidateDistance, length);
            Vector3 routePosition = Flatten(waypointCircuit.GetRoutePosition(wrappedDistance));
            float sqrDistance = Vector3.SqrMagnitude(flatPosition - routePosition);

            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestDistance = wrappedDistance;
        }

        return bestDistance;

    }

    private int FindClosestCircuitWaypoint(Vector3 position) {

        if (waypointCircuit == null || waypointCircuit.Waypoints == null || waypointCircuit.Waypoints.Length == 0)
            return 0;

        int bestIndex = 0;
        float bestSqrDistance = float.MaxValue;
        Vector3 flatPosition = Flatten(position);

        for (int i = 0; i < waypointCircuit.Waypoints.Length; i++) {
            Transform waypoint = waypointCircuit.Waypoints[i];

            if (waypoint == null)
                continue;

            float sqrDistance = Vector3.SqrMagnitude(flatPosition - Flatten(waypoint.position));

            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestIndex = i;
        }

        return bestIndex;

    }

    private bool ShouldIgnoreHit(RaycastHit hit) {

        return hit.collider == null
            || hit.collider.transform.IsChildOf(transform)
            || Vector3.Dot(hit.normal, Vector3.up) > .65f;

    }

    private float GetAvoidanceSide(RaycastHit hit) {

        Vector3 localHit = transform.InverseTransformPoint(hit.point);

        if (Mathf.Abs(localHit.x) > .25f)
            return -Mathf.Sign(localHit.x);

        float sideFromNormal = Vector3.Dot(hit.normal, transform.right);

        if (Mathf.Abs(sideFromNormal) > .15f)
            return Mathf.Sign(sideFromNormal);

        if (Mathf.Abs(laneOffset) > .1f)
            return Mathf.Sign(laneOffset);

        return Random.value > .5f ? 1f : -1f;

    }

    private bool TrySphereCastIgnoringSelf(Vector3 origin, float radius, Vector3 direction, out RaycastHit bestHit, float maxDistance, LayerMask layerMask) {

        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
        bestHit = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++) {
            RaycastHit hit = hits[i];

            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestHit = hit;
            bestDistance = hit.distance;
        }

        return bestDistance < float.MaxValue;

    }

    private float GetRubberBandMultiplier() {

        if (!useRubberBanding || playerTarget == null)
            return 1f;

        float distance = Vector3.Distance(Flatten(playerTarget.position), Flatten(transform.position));
        float t = Mathf.Clamp01(distance / Mathf.Max(1f, rubberBandFullEffectDistance));
        float forwardDot = Vector3.Dot(transform.forward, Flatten(playerTarget.position - transform.position).normalized);

        if (forwardDot > 0f)
            return Mathf.Lerp(1f, behindPlayerSpeedMultiplier, t);

        return Mathf.Lerp(1f, aheadPlayerSpeedMultiplier, t);

    }

    private float GetTrafficOffset(float cornerT) {

        if (Mathf.Abs(trafficSideBias) < .01f || trafficBiasTimer <= 0f)
            return 0f;

        float cornerReduction = Mathf.Lerp(1f, .7f, cornerT);
        return trafficSideBias * trafficLaneOffset * cornerReduction;

    }

    private float GetPreferredTrafficSide(float fallbackSide) {

        if (!leftVehicleBlocked && rightVehicleBlocked)
            return -1f;

        if (!rightVehicleBlocked && leftVehicleBlocked)
            return 1f;

        if (!leftVehicleBlocked && !rightVehicleBlocked) {
            if (Mathf.Abs(trafficSideBias) > .01f)
                return trafficSideBias;

            return fallbackSide != 0f ? fallbackSide : (((GetInstanceID() & 1) == 0) ? 1f : -1f);
        }

        return fallbackSide;

    }

    private bool IsPlayerVehicle(RCCP_CarController otherCar) {

        if (otherCar == null || playerTarget == null)
            return false;

        return otherCar.transform == playerTarget || otherCar.transform.IsChildOf(playerTarget) || playerTarget.IsChildOf(otherCar.transform);

    }

    private void UpdateTrafficBias(float desiredSide, float urgency) {

        if (Mathf.Abs(desiredSide) < .01f)
            desiredSide = ((GetInstanceID() & 1) == 0) ? 1f : -1f;

        if (trafficBiasTimer <= 0f || Mathf.Sign(trafficSideBias) != Mathf.Sign(desiredSide))
            trafficSideBias = Mathf.Sign(desiredSide);

        trafficBiasTimer = Mathf.Max(trafficBiasTimer, Mathf.Lerp(.45f, trafficBiasDuration, Mathf.Clamp01(urgency)));

    }

    private void ResetDriverConsistency() {

        paceMultiplier = 1f + Random.Range(-paceVariation, paceVariation);
        personalityLaneOffset = Random.Range(-laneVariationRange, laneVariationRange);
        personalityBrakeBias = Random.Range(.92f, 1.12f);
        personalityCornerSpeedBias = Random.Range(.95f, 1.08f);
        personalitySteerBias = Random.Range(.96f, 1.08f);
        personalityOvertakeBias = Random.Range(.9f, 1.15f);
        mistakeTimer = 0f;
        mistakeSpeedMultiplier = 1f;
        mistakeSteerOffset = 0f;
        nextMistakeCheckTime = Time.time + Random.Range(1.5f, 3f);

    }

    private void UpdateDriverConsistency() {

        if (mistakeTimer > 0f) {
            mistakeTimer -= Time.fixedDeltaTime;

            if (mistakeTimer <= 0f) {
                mistakeSpeedMultiplier = 1f;
                mistakeSteerOffset = 0f;
            }
        }

        if (mistakeChance <= 0f || Time.time < nextMistakeCheckTime)
            return;

        nextMistakeCheckTime = Time.time + Random.Range(1.75f, 3.5f);

        if (Random.value > mistakeChance)
            return;

        mistakeTimer = mistakeDuration * Random.Range(.75f, 1.2f);
        mistakeSpeedMultiplier = 1f - Random.Range(mistakeStrength * .35f, mistakeStrength);
        mistakeSteerOffset = Random.Range(-mistakeStrength, mistakeStrength) * .2f;

    }

    private void EnsureVehicleReady() {

        if (car == null)
            return;

        car.externalControl = true;
        car.SetEngine(true);

        if (car.Rigid != null && car.Rigid.IsSleeping())
            car.Rigid.WakeUp();

        if (car.Gearbox == null)
            return;

        car.Gearbox.forceToNGear = false;
        car.Gearbox.forceToRGear = false;
        car.Gearbox.automaticGearSelector = RCCP_Gearbox.SemiAutomaticDNRPGear.D;
        car.Gearbox.currentGear = Mathf.Max(0, car.Gearbox.currentGear);

        if (car.Gearbox.currentGearState == null)
            car.Gearbox.currentGearState = new RCCP_Gearbox.CurrentGearState();

        car.Gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
        car.Gearbox.gearInput = 1f;

    }

    private static Vector3 Flatten(Vector3 value) {

        value.y = 0f;
        return value;

    }

    private readonly struct RouteFrame {

        public readonly Vector3 targetPoint;
        public readonly Vector3 direction;
        public readonly float cornerAngle;
        public readonly float previewCornerAngle;

        public RouteFrame(Vector3 targetPoint, Vector3 direction, float cornerAngle, float previewCornerAngle) {
            this.targetPoint = targetPoint;
            this.direction = direction;
            this.cornerAngle = cornerAngle;
            this.previewCornerAngle = previewCornerAngle;
        }

    }

}
