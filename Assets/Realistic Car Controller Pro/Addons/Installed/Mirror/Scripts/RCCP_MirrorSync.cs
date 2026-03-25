//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if RCCP_MIRROR && MIRROR
using UnityEngine;
using Mirror;

[System.Serializable]
public struct RCCP_MirrorTransformFallbackMessage : NetworkMessage {

    public uint netId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
    public float timestamp;

}

[DefaultExecutionOrder(10)]
[RequireComponent(typeof(NetworkIdentity))]
public class RCCP_MirrorSync : NetworkBehaviour {

    [Tooltip("Distance threshold beyond which remote vehicles teleport instead of interpolating.")]
    public float teleportDistanceThreshold = 4f;

    [Header("Advanced Interpolation Settings")]
    [Tooltip("Lag compensation time in seconds. Higher values = smoother but more latency.")]
    public float lagCompensationTime = 0.05f;

    [Tooltip("Position interpolation speed multiplier.")]
    public float positionInterpolationSpeed = 10f;

    [Tooltip("Rotation interpolation speed multiplier.")]
    public float rotationInterpolationSpeed = 10f;

    [Tooltip("Enable extrapolation for smoother movement prediction.")]
    public bool useExtrapolation = true;

    [Tooltip("Maximum total prediction time (lag compensation + extrapolation) in seconds.")]
    public float maxExtrapolationTime = .1f;

    [Header("Velocity Synchronization")]
    [Tooltip("Enable velocity-based smoothing for more accurate physics.")]
    public bool useVelocitySmoothing = true;

    [Tooltip("Velocity interpolation damping factor.")]
    public float velocityDampening = .8f;

    [Header("Network Settings")]
    [Tooltip("How many times per second to send vehicle state over the network.")]
    public int sendRate = 30;

    /// <summary>
    /// Reference to the parent Realistic Car Controller component.
    /// </summary>
    private RCCP_CarController carController;

    /// <summary>
    /// Cached inputs module of the car controller.
    /// </summary>
    private RCCP_Input inputsModule;

    /// <summary>
    /// Cached engine module of the car controller.
    /// </summary>
    private RCCP_Engine engineModule;

    /// <summary>
    /// Cached clutch module of the car controller.
    /// </summary>
    private RCCP_Clutch clutchModule;

    /// <summary>
    /// Cached gearbox module of the car controller.
    /// </summary>
    private RCCP_Gearbox gearboxModule;

    /// <summary>
    /// Cached differential module of the car controller.
    /// </summary>
    private RCCP_Differential[] differentialModule;

    // Enhanced networking state variables

    /// <summary>
    /// Timestamp of last network update, used for interpolation.
    /// </summary>
    private float lastUpdateTime = 0f;

    /// <summary>
    /// Network lag between updates.
    /// </summary>
    private float networkLag = 0f;

    /// <summary>
    /// Target position received from network with lag compensation.
    /// </summary>
    private Vector3 targetPosition = Vector3.zero;

    /// <summary>
    /// Target rotation received from network with lag compensation.
    /// </summary>
    private Quaternion targetRotation = Quaternion.identity;

    /// <summary>
    /// Last received network velocity.
    /// </summary>
    private Vector3 networkVelocity = Vector3.zero;

    /// <summary>
    /// Last received network angular velocity.
    /// </summary>
    private Vector3 networkAngularVelocity = Vector3.zero;

    /// <summary>
    /// Prediction time (lag + smoothing) used when the last network update arrived.
    /// </summary>
    private float basePredictionTime = 0f;

    /// <summary>
    /// Wheel RPMs for networked vehicles.
    /// </summary>
    private float[] wheelRPMs;

    // Inputs and state overrides

    private float gasInput = 0f;
    private float brakeInput = 0f;
    private float steerInput = 0f;
    private float handbrakeInput = 0f;
    private float boostInput = 0f;
    private float clutchInput = 0f;
    private float engineRPM = 0f;
    private int currentGear = 0;
    private RCCP_Gearbox.CurrentGearState.GearState currentGearState;
    private float gearInput = 1f;
    private bool changingGear = false;
    private bool engineStarting = false;
    private bool engineRunning = false;
    private int direction = 0;

    // Drivetrain outputs

    private float[] differentialOutputLeft;
    private float[] differentialOutputRight;

    // Lights state

    private bool lowBeamHeadLightsOn = false;
    private bool highBeamHeadLightsOn = false;
    private bool indicatorsLeft = false;
    private bool indicatorsRight = false;
    private bool indicatorsAll = false;

    // Send rate limiter

    private float sendTimer = 0f;
    private float sendInterval => 1f / sendRate;

    /// <summary>
    /// Whether wheel RPM correction components have been added to remote wheels.
    /// Deferred from OnStartClient to FixedUpdate so IsMine is stable.
    /// </summary>
    private bool wheelCorrectionsAdded = false;

    private static bool transformFallbackHandlerRegistered = false;
    private static int transformFallbackHandlerRefCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {

        transformFallbackHandlerRegistered = false;
        transformFallbackHandlerRefCount = 0;

    }

    /// <summary>
    /// Returns true if this vehicle is owned by the local client.
    /// With playerPrefab auto-spawn and ReplacePlayerForConnection, isLocalPlayer
    /// is set immediately and reliably — no timing workarounds needed.
    /// </summary>
    public bool IsMine {

        get {

            if (netId == 0)
                return false;

            if (isLocalPlayer)
                return true;

            if (isOwned)
                return true;

            // Host mode fallback: server knows authority is ours before the client processes it
            if (isServer && NetworkServer.localConnection != null && connectionToClient == NetworkServer.localConnection)
                return true;

            return false;

        }

    }

    /// <summary>
    /// Ensures vehicle prefabs are registered with the NetworkManager so
    /// late-joining clients can instantiate vehicles they haven't seen yet.
    /// Safe to call multiple times; registration is idempotent.
    /// </summary>
    public static void EnsureSpawnInfrastructure() {

        RegisterVehiclePrefabs();

    }

    /// <summary>
    /// Returns the correct vehicle list based on whether Demo Content addon is installed.
    /// </summary>
    public static RCCP_CarController[] GetVehicleList() {

#if RCCP_DEMO
        return RCCP_DemoVehicles_Mirror.Instance.vehicles;
#else
        return RCCP_Prototype_Mirror.Instance.vehicles;
#endif

    }

    /// <summary>
    /// Registers all demo vehicle prefabs with the NetworkManager so late-joining
    /// clients can instantiate vehicles they haven't seen yet.
    /// </summary>
    private static void RegisterVehiclePrefabs() {

        NetworkManager nm = NetworkManager.singleton;

        if (nm == null)
            return;

        RCCP_CarController[] vehicles = GetVehicleList();

        if (vehicles == null)
            return;

        for (int i = 0; i < vehicles.Length; i++) {

            if (vehicles[i] == null)
                continue;

            GameObject prefab = vehicles[i].gameObject;

            // Skip the playerPrefab — Mirror registers it automatically
            if (prefab == nm.playerPrefab)
                continue;

            if (!nm.spawnPrefabs.Contains(prefab))
                nm.spawnPrefabs.Add(prefab);

        }

    }

    private void Awake() {

        carController = GetComponentInParent<RCCP_CarController>(true);

        inputsModule = carController.Inputs;
        engineModule = carController.Engine;
        clutchModule = carController.Clutch;
        gearboxModule = carController.Gearbox;
        differentialModule = carController.Differentials;

        // Initialize wheel RPMs
        wheelRPMs = new float[carController.AllWheelColliders.Length];

    }

    public void Start() {
    }

    public override void OnStartServer() {

        EnsureSpawnInfrastructure();

        transformFallbackHandlerRefCount++;

        if (!transformFallbackHandlerRegistered) {

            NetworkServer.RegisterHandler<RCCP_MirrorTransformFallbackMessage>(OnServerTransformFallbackMessage);
            transformFallbackHandlerRegistered = true;

        }

    }

    public override void OnStopServer() {

        transformFallbackHandlerRefCount = Mathf.Max(0, transformFallbackHandlerRefCount - 1);

        if (transformFallbackHandlerRegistered && transformFallbackHandlerRefCount == 0) {

            NetworkServer.UnregisterHandler<RCCP_MirrorTransformFallbackMessage>();
            transformFallbackHandlerRegistered = false;

        }

    }

    public override void OnStartClient() {

        if (IsMine)
            RegisterAsPlayerVehicle();

    }

    private void OnEnable() {

        GetInitialValues();

    }

    /// <summary>
    /// Captures the initial network state and transform for a newly enabled object.
    /// </summary>
    private void GetInitialValues() {

        Rigidbody rigid = carController ? carController.Rigid : null;

        if (rigid) {
            targetPosition = rigid.position;
            targetRotation = rigid.rotation;
            networkVelocity = rigid.linearVelocity;
            networkAngularVelocity = rigid.angularVelocity;
        } else {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        basePredictionTime = 0f;
        lastUpdateTime = Time.time;

        gasInput = carController.throttleInput_V;
        brakeInput = carController.brakeInput_V;
        steerInput = carController.steerInput_V;
        handbrakeInput = carController.handbrakeInput_V;
        boostInput = carController.nosInput_V;
        clutchInput = carController.clutchInput_V;

        engineRPM = carController.engineRPM;
        direction = carController.direction;
        engineStarting = carController.engineStarting;
        engineRunning = carController.engineRunning;

        currentGear = carController.currentGear;
        currentGearState = carController.Gearbox.currentGearState.gearState;
        gearInput = carController.Gearbox.gearInput;

        differentialOutputLeft = new float[differentialModule.Length];
        differentialOutputRight = new float[differentialModule.Length];

        lowBeamHeadLightsOn = carController.lowBeamLights;
        highBeamHeadLightsOn = carController.highBeamLights;
        indicatorsLeft = carController.indicatorsLeftLights;
        indicatorsRight = carController.indicatorsRightLights;
        indicatorsAll = carController.indicatorsAllLights;

    }

    /// <summary>
    /// Called every physics step to apply local or remote control.
    /// </summary>
    private void FixedUpdate() {

        if (!NetworkClient.active)
            return;

        if (!NetworkClient.isConnected)
            return;

        if (!carController)
            return;

        if (!IsMine) {

            // Deferred from OnStartClient: add wheel RPM correction only once IsMine is confirmed false.
            if (!wheelCorrectionsAdded) {

                wheelCorrectionsAdded = true;

                for (int i = 0; i < carController.AllWheelColliders.Length; i++) {

                    RCCP_WheelRPMCorrectionMirror correction = carController.AllWheelColliders[i].gameObject.AddComponent<RCCP_WheelRPMCorrectionMirror>();
                    correction.wheelIndex = i;
                    correction.mirrorSync = this;

                }

            }

            ApplyRemoteMovement();
            ApplyRemoteInputsAndState();

        } else {

            if (inputsModule.overridePlayerInputs)
                inputsModule.DisableOverrideInputs();

            if (inputsModule.overrideExternalInputs)
                inputsModule.overrideExternalInputs = false;

            // Clear module overrides that may have been set during initial frames
            if (engineModule.overrideEngineRPM)
                engineModule.DisableOverride();

            if (clutchModule.overrideClutch)
                clutchModule.DisableOverride();

            if (gearboxModule.overrideGear)
                gearboxModule.DisableOverride();

            if (differentialModule != null) {

                for (int i = 0; i < differentialModule.Length; i++) {

                    if (differentialModule[i] != null && differentialModule[i].overrideDifferential)
                        differentialModule[i].DisableOverride();

                }

            }

            // Send state at configured rate
            sendTimer += Time.fixedDeltaTime;

            if (sendTimer >= sendInterval) {

                sendTimer = 0f;
                SendVehicleState();

            }

        }

    }

    /// <summary>
    /// Packs and sends the vehicle state from the owner to the server.
    /// </summary>
    private void SendVehicleState() {

        if (netId == 0)
            return;

        // Collect differential outputs
        float[] diffLeft = new float[differentialModule.Length];
        float[] diffRight = new float[differentialModule.Length];

        for (int i = 0; i < differentialModule.Length; i++) {

            if (differentialModule[i] == null)
                continue;

            diffLeft[i] = differentialModule[i].outputLeft;
            diffRight[i] = differentialModule[i].outputRight;

        }

        // Collect wheel RPMs
        float[] wRPMs = new float[carController.AllWheelColliders.Length];

        for (int i = 0; i < wRPMs.Length; i++)
            wRPMs[i] = carController.AllWheelColliders[i].WheelCollider.rpm;

        CmdSendVehicleState(
            // Inputs
            carController.throttleInput_V,
            carController.brakeInput_V,
            carController.steerInput_V,
            carController.handbrakeInput_V,
            carController.nosInput_V,
            carController.clutchInput_V,
            // Engine state
            carController.engineRPM,
            carController.shiftingNow,
            carController.engineStarting,
            carController.engineRunning,
            carController.direction,
            // Gear state
            carController.currentGear,
            carController.gearInput_V,
            MapGearState(),
            // Differential outputs
            diffLeft,
            diffRight,
            // Lights
            carController.lowBeamLights,
            carController.highBeamLights,
            carController.indicatorsLeftLights,
            carController.indicatorsRightLights,
            carController.indicatorsAllLights,
            // Wheel RPMs
            wRPMs,
            // Transform
            transform.position,
            transform.rotation,
            carController.Rigid.linearVelocity,
            carController.Rigid.angularVelocity,
            (float)NetworkTime.time
        );

        // Fallback transform channel for client-owned vehicles.
        // This bypasses Command authority edge cases and keeps host/non-owners in sync.
        if (isClient && !isServer) {

            NetworkClient.Send(new RCCP_MirrorTransformFallbackMessage {
                netId = netId,
                position = transform.position,
                rotation = transform.rotation,
                velocity = carController.Rigid.linearVelocity,
                angularVelocity = carController.Rigid.angularVelocity,
                timestamp = (float)NetworkTime.time
            });

        }

    }

    /// <summary>
    /// Command: owner sends vehicle state to server.
    /// </summary>
    [Command(channel = Channels.Unreliable, requiresAuthority = false)]
    private void CmdSendVehicleState(
        float _gas, float _brake, float _steer, float _handbrake, float _boost, float _clutch,
        float _engineRPM, bool _changingGear, bool _engineStarting, bool _engineRunning, int _direction,
        int _currentGear, float _gearInput, int _gearStateIndex,
        float[] _diffLeft, float[] _diffRight,
        bool _lowBeam, bool _highBeam, bool _indLeft, bool _indRight, bool _indAll,
        float[] _wheelRPMs,
        Vector3 _position, Quaternion _rotation, Vector3 _velocity, Vector3 _angularVelocity,
        float _timestamp,
        NetworkConnectionToClient sender = null
    ) {

        if (sender == null)
            return;

        // If authority wasn't set correctly during spawn, recover it here for this sender.
        if (connectionToClient == null)
            netIdentity.AssignClientAuthority(sender);

        // Ignore updates from non-owning connections.
        if (connectionToClient != sender)
            return;

        // Host fallback:
        // In some host-mode setups, remote-client RPC updates may not be applied locally
        // for client-owned objects. Applying the received state here keeps host visuals in sync.
        if (NetworkClient.active &&
            connectionToClient != null &&
            connectionToClient.connectionId != NetworkConnection.LocalConnectionId) {

            ApplyReceivedVehicleState(
                _gas, _brake, _steer, _handbrake, _boost, _clutch,
                _engineRPM, _changingGear, _engineStarting, _engineRunning, _direction,
                _currentGear, _gearInput, _gearStateIndex,
                _diffLeft, _diffRight,
                _lowBeam, _highBeam, _indLeft, _indRight, _indAll,
                _wheelRPMs,
                _position, _rotation, _velocity, _angularVelocity,
                _timestamp
            );

        }

        // Server relays to all clients except the owner
        RpcReceiveVehicleState(
            _gas, _brake, _steer, _handbrake, _boost, _clutch,
            _engineRPM, _changingGear, _engineStarting, _engineRunning, _direction,
            _currentGear, _gearInput, _gearStateIndex,
            _diffLeft, _diffRight,
            _lowBeam, _highBeam, _indLeft, _indRight, _indAll,
            _wheelRPMs,
            _position, _rotation, _velocity, _angularVelocity,
            _timestamp
        );

    }

    /// <summary>
    /// ClientRpc: server relays vehicle state to all clients except owner (unreliable channel).
    /// </summary>
    [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
    private void RpcReceiveVehicleState(
        float _gas, float _brake, float _steer, float _handbrake, float _boost, float _clutch,
        float _engineRPM, bool _changingGear, bool _engineStarting, bool _engineRunning, int _direction,
        int _currentGear, float _gearInput, int _gearStateIndex,
        float[] _diffLeft, float[] _diffRight,
        bool _lowBeam, bool _highBeam, bool _indLeft, bool _indRight, bool _indAll,
        float[] _wheelRPMs,
        Vector3 _position, Quaternion _rotation, Vector3 _velocity, Vector3 _angularVelocity,
        float _timestamp
    ) {

        ApplyReceivedVehicleState(
            _gas, _brake, _steer, _handbrake, _boost, _clutch,
            _engineRPM, _changingGear, _engineStarting, _engineRunning, _direction,
            _currentGear, _gearInput, _gearStateIndex,
            _diffLeft, _diffRight,
            _lowBeam, _highBeam, _indLeft, _indRight, _indAll,
            _wheelRPMs,
            _position, _rotation, _velocity, _angularVelocity,
            _timestamp
        );

    }

    /// <summary>
    /// Applies one received network snapshot to this vehicle.
    /// Used by both Rpc and host-side command fallback paths.
    /// </summary>
    private void ApplyReceivedVehicleState(
        float _gas, float _brake, float _steer, float _handbrake, float _boost, float _clutch,
        float _engineRPM, bool _changingGear, bool _engineStarting, bool _engineRunning, int _direction,
        int _currentGear, float _gearInput, int _gearStateIndex,
        float[] _diffLeft, float[] _diffRight,
        bool _lowBeam, bool _highBeam, bool _indLeft, bool _indRight, bool _indAll,
        float[] _wheelRPMs,
        Vector3 _position, Quaternion _rotation, Vector3 _velocity, Vector3 _angularVelocity,
        float _timestamp
    ) {

        // Apply inputs
        gasInput = _gas;
        brakeInput = _brake;
        steerInput = _steer;
        handbrakeInput = _handbrake;
        boostInput = _boost;
        clutchInput = _clutch;

        // Apply engine state
        engineRPM = _engineRPM;
        changingGear = _changingGear;
        engineStarting = _engineStarting;
        engineRunning = _engineRunning;
        direction = _direction;

        // Apply gear state
        currentGear = _currentGear;
        gearInput = _gearInput;
        currentGearState = MapGearState(_gearStateIndex);

        // Apply differential outputs
        if (differentialModule != null && differentialModule.Length > 0) {

            for (int i = 0; i < differentialModule.Length && i < _diffLeft.Length; i++) {

                differentialOutputLeft[i] = _diffLeft[i];
                differentialOutputRight[i] = _diffRight[i];

            }

        }

        // Apply lights
        lowBeamHeadLightsOn = _lowBeam;
        highBeamHeadLightsOn = _highBeam;
        indicatorsLeft = _indLeft;
        indicatorsRight = _indRight;
        indicatorsAll = _indAll;

        // Apply wheel RPMs
        if (wheelRPMs != null && _wheelRPMs != null) {

            for (int i = 0; i < wheelRPMs.Length && i < _wheelRPMs.Length; i++)
                wheelRPMs[i] = _wheelRPMs[i];

        }

        ApplyReceivedTransformState(_position, _rotation, _velocity, _angularVelocity, _timestamp);

    }

    private static void OnServerTransformFallbackMessage(NetworkConnectionToClient conn, RCCP_MirrorTransformFallbackMessage msg) {

        if (conn == null)
            return;

        if (!NetworkServer.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            return;

        RCCP_MirrorSync sync = identity.GetComponent<RCCP_MirrorSync>();

        if (sync == null)
            return;

        if (identity.connectionToClient != conn)
            return;

        sync.ServerReceiveTransformFallback(msg.position, msg.rotation, msg.velocity, msg.angularVelocity, msg.timestamp);

    }

    private void ServerReceiveTransformFallback(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity, float timestamp) {

        // Host mode: apply immediately for local view.
        if (NetworkClient.active &&
            connectionToClient != null &&
            connectionToClient.connectionId != NetworkConnection.LocalConnectionId)
            ApplyReceivedTransformState(position, rotation, velocity, angularVelocity, timestamp);

        RpcReceiveTransformFallback(position, rotation, velocity, angularVelocity, timestamp);

    }

    [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
    private void RpcReceiveTransformFallback(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity, float timestamp) {

        ApplyReceivedTransformState(position, rotation, velocity, angularVelocity, timestamp);

    }

    private void ApplyReceivedTransformState(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity, float timestamp) {

        // Calculate network lag using Mirror's NetworkTime
        networkLag = (float)(NetworkTime.time - timestamp);

        // Apply lag compensation
        ApplyLagCompensation(position, rotation, velocity, angularVelocity);

        lastUpdateTime = Time.time;

    }

    /// <summary>
    /// Applies enhanced movement interpolation for remote vehicles.
    /// </summary>
    private void ApplyRemoteMovement() {

        Rigidbody rigid = carController.Rigid;

        if (!rigid)
            return;

        // Calculate time difference since last update
        float timeSinceUpdate = Mathf.Max(0f, Time.time - lastUpdateTime);
        float remainingPredictionBudget = Mathf.Max(0f, maxExtrapolationTime - basePredictionTime);
        float extraPrediction = useExtrapolation ? Mathf.Min(timeSinceUpdate, remainingPredictionBudget) : 0f;

        // Predict position and rotation with capped extrapolation
        Vector3 predictedPosition = targetPosition + (networkVelocity * extraPrediction);
        Quaternion predictedRotation = targetRotation;

        if (networkAngularVelocity.sqrMagnitude > 0.0001f) {

            Vector3 angularStep = networkAngularVelocity * extraPrediction * Mathf.Rad2Deg;
            predictedRotation *= Quaternion.Euler(angularStep);

        }

        // Check for teleportation threshold
        float distance = Vector3.Distance(rigid.position, predictedPosition);

        if (distance > teleportDistanceThreshold) {

            // Teleport if too far
            rigid.position = predictedPosition;
            rigid.rotation = predictedRotation;
            rigid.linearVelocity = networkVelocity;
            rigid.angularVelocity = networkAngularVelocity;

        } else {

            // Smooth position and rotation using Rigidbody for better collision stability
            float positionLerp = Mathf.Clamp01(positionInterpolationSpeed * Time.fixedDeltaTime);
            float rotationLerp = Mathf.Clamp01(rotationInterpolationSpeed * Time.fixedDeltaTime);

            Vector3 newPosition = Vector3.Lerp(rigid.position, predictedPosition, positionLerp);
            Quaternion newRotation = Quaternion.Slerp(rigid.rotation, predictedRotation, rotationLerp);

            rigid.MovePosition(newPosition);
            rigid.MoveRotation(newRotation);

            // Apply velocity smoothing to rigidbody
            if (useVelocitySmoothing) {

                float velocityLerp = Mathf.Clamp01(positionInterpolationSpeed * Time.fixedDeltaTime);
                rigid.linearVelocity = Vector3.Lerp(rigid.linearVelocity, networkVelocity, velocityLerp);
                rigid.angularVelocity = Vector3.Lerp(rigid.angularVelocity, networkAngularVelocity, velocityLerp);

            } else {

                rigid.linearVelocity = networkVelocity;
                rigid.angularVelocity = networkAngularVelocity;

            }

        }

    }

    /// <summary>
    /// Applies remote inputs and vehicle state.
    /// </summary>
    private void ApplyRemoteInputsAndState() {

        carController.Inputs.overrideExternalInputs = true;

        // Override inputs and modules
        inputsModule.OverrideInputs(new RCCP_Inputs {
            throttleInput = gasInput,
            brakeInput = brakeInput,
            steerInput = steerInput,
            handbrakeInput = handbrakeInput,
            nosInput = boostInput,
            clutchInput = clutchInput
        });

        engineModule.OverrideRPM(engineRPM);
        engineModule.engineStarting = engineStarting;
        engineModule.engineRunning = engineRunning;

        clutchModule.OverrideInput(clutchInput);

        var targetGear = currentGearState;
        gearboxModule.OverrideGear(currentGear, gearInput, targetGear);
        gearboxModule.shiftingNow = changingGear;

        if (differentialModule != null && differentialModule.Length > 0) {

            for (int i = 0; i < differentialModule.Length; i++) {

                if (differentialModule[i] == null)
                    continue;

                differentialModule[i].OverrideDifferential(differentialOutputLeft[i], differentialOutputRight[i]);

            }

        }

        // Apply lights
        carController.Lights.lowBeamHeadlights = lowBeamHeadLightsOn;
        carController.Lights.highBeamHeadlights = highBeamHeadLightsOn;
        carController.Lights.indicatorsLeft = indicatorsLeft;
        carController.Lights.indicatorsRight = indicatorsRight;
        carController.Lights.indicatorsAll = indicatorsAll;

    }

    /// <summary>
    /// Called by Mirror when this client gains authority over the object.
    /// Registers the vehicle as the local player's vehicle and sets up camera tracking.
    /// Works for both host-spawned and client-requested vehicles.
    /// </summary>
    public override void OnStartAuthority() {

        RegisterAsPlayerVehicle();

    }

    /// <summary>
    /// Registers the vehicle with RCCP as the local player's vehicle
    /// and sets up camera tracking.
    /// </summary>
    private void RegisterAsPlayerVehicle() {

        if (carController == null)
            return;

        RCCP.RegisterPlayerVehicle(carController, true, true);
        RCCP.SetControl(carController, true);

        RCCP_Camera cam = RCCP_SceneManager.Instance ? RCCP_SceneManager.Instance.activePlayerCamera : null;

        if (cam)
            cam.SetTarget(carController);

    }

    private int MapGearState() {

        switch (carController.Gearbox.currentGearState.gearState) {

            case RCCP_Gearbox.CurrentGearState.GearState.InForwardGear:
            return 1;

            case RCCP_Gearbox.CurrentGearState.GearState.InReverseGear:
            return -2;

            case RCCP_Gearbox.CurrentGearState.GearState.Neutral:
            return 0;

            case RCCP_Gearbox.CurrentGearState.GearState.Park:
            return -1;

        }

        return 1;

    }

    private RCCP_Gearbox.CurrentGearState.GearState MapGearState(int index) {

        switch (index) {

            case 1:
            return RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;

            case -2:
            return RCCP_Gearbox.CurrentGearState.GearState.InReverseGear;

            case 0:
            return RCCP_Gearbox.CurrentGearState.GearState.Neutral;

            case -1:
            return RCCP_Gearbox.CurrentGearState.GearState.Park;

        }

        return RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;

    }

    /// <summary>
    /// Applies lag compensation to received network data.
    /// </summary>
    private void ApplyLagCompensation(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity) {

        // Store the raw network data
        networkVelocity = velocity;
        networkAngularVelocity = angularVelocity;

        // Apply lag compensation by predicting future position (capped to avoid overshoot)
        float predictionTime = networkLag + (lagCompensationTime * 0.5f);
        basePredictionTime = Mathf.Clamp(predictionTime, 0f, maxExtrapolationTime);
        targetPosition = position + velocity * basePredictionTime;
        targetRotation = rotation;

        // Apply angular compensation if significant rotation
        if (angularVelocity.magnitude > 0.1f) {

            Vector3 angularDisplacement = angularVelocity * basePredictionTime * Mathf.Rad2Deg;
            targetRotation *= Quaternion.Euler(angularDisplacement);

        }

    }

    /// <summary>
    /// Returns the target RPM for the specified wheel index.
    /// </summary>
    public float GetTargetWheelRPM(int index) {

        if (wheelRPMs == null || index < 0 || index >= wheelRPMs.Length)
            return 0f;

        return wheelRPMs[index];

    }

    /// <summary>
    /// Ensures a NetworkIdentity is attached when this component is reset.
    /// </summary>
    private void Reset() {

        NetworkIdentity ni = GetComponent<NetworkIdentity>();

        if (!ni)
            ni = gameObject.AddComponent<NetworkIdentity>();

    }

    /// <summary>
    /// Visualizes network synchronization data in the scene view.
    /// </summary>
    private void OnDrawGizmos() {

        if (Application.isPlaying && !IsMine) {

            // Draw target position in red
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, 0.5f);

            // Draw velocity vector in blue
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, networkVelocity.normalized * 3f);

            // Draw interpolation line in yellow
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetPosition);

        }

    }

}
#endif
