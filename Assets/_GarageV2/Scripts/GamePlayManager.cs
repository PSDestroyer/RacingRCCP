using System;
using HalvaStudio.Save;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using ALIyerEdon;
using System.Linq;

public class GamePlayManager : MonoBehaviour
{
    [Serializable]
    public class RaceRacer
    {
        public string displayName = "Racer";
        public Transform racerTransform;
        public RCCP_AI aiDriver;

        [NonSerialized] public int currentWaypointIndex;
        [NonSerialized] public int completedLaps;
        [NonSerialized] public bool finished;
        [NonSerialized] public bool eliminated;
        [NonSerialized] public float distanceToNextWaypoint;
        [NonSerialized] public float raceProgress;
        [NonSerialized] public int finishPosition;
        [NonSerialized] public float finishTime;
        [NonSerialized] public float currentCircuitDistance;
        [NonSerialized] public bool progressInitialized;
        [NonSerialized] public float startCircuitDistance;
        [NonSerialized] public bool lapCountingArmed;
        [NonSerialized] public int currentSegmentIndex;
        [NonSerialized] public int currentProgressPathIndex;
        [NonSerialized] public float sharedRankingProgress;
        [NonSerialized] public int currentCheckpointIndex;
        [NonSerialized] public int nextCheckpointIndex;
        [NonSerialized] public bool checkpointProgressInitialized;
    }

    private struct PathProgressInfo
    {
        public int pathIndex;
        public float normalizedProgress;
        public float sqrDistance;
        public float distanceToNextWaypoint;
    }

    [NonSerialized]public GameObject player;
    public RCCP_CarController CarController;
    public Transform SpawnPoint;
    public RaceType RaceType;

    [Header("Racing Settings")]
    public bool useCurrentMapModeSettings = true;
    public Waypoint_System externalWaypointSystem;
    public Transform externalWaypointRoot;
    public Checkpoint_Manager externalCheckpointManager;
    public RaceRacer[] aiRacers;
    public int totalRaceLaps = 3;
    public float waypointReachDistance = 26f;
    public TMP_Text currentLapText;
    public TMP_Text racePositionText;
    public TMP_Text raceStateText;
    public TMP_Text eliminationTimerText;

    [Header("Elimination Settings")]
    public float eliminationInterval = 25f;
    public float eliminationWarningThreshold = 10f;
    public float eliminationCriticalThreshold = 3f;
    public Color eliminationTimerNormalColor = Color.white;
    public Color eliminationTimerWarningColor = Color.red;
    public float eliminationPulseSpeed = 6f;
    public float eliminationPulseScale = 0.15f;
    public float eliminationCriticalPulseSpeed = 10f;
    public float eliminationCriticalPulseScale = 0.3f;

    [Header("Race Position UI")]
    public bool showLiveLeaderboard = false;
    public float positionChangeFadeDuration = 0.2f;
    [Range(0f, 1f)] public float positionChangeMinAlpha = 0.25f;
    public Color playerLeaderboardColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Finish Summary UI")]
    public GameObject finishSummaryScreen;
    public TMP_Text finishTitleText;
    public Color finishCompleteTitleColor = Color.green;
    public Color finishFailedTitleColor = Color.red;
    public TMP_Text finishModeText;
    public TMP_Text finishPositionText;
    public TMP_Text finishRewardText;
    public TMP_Text finishTimeText;
    public TMP_Text finishLeaderboardText;
    public GameObject finishSummaryContinueButton;
    public bool showFinishLeaderboard = true;
    public float finishSummaryValueDuration = 0.45f;
    public float finishSummaryStepDelay = 0.12f;

    [Header("Finish EXP UI")]
    public GameObject finishExpScreen;
    public TMP_Text expLevelText;
    public TMP_Text expTotalText;
    public TMP_Text expGainText;
    public TMP_Text expLevelRewardText;
    public Slider expProgressSlider;
    public GameObject finishExpContinueButton;
    public float expAnimationDuration = 1.5f;
    public float expSliderPulseDuration = 0.2f;
    public float expSliderPulseScale = 0.12f;
    public float expRewardPulseDuration = 0.22f;
    public float expRewardPulseScale = 0.22f;
    public int expPerLevel = 2500;
    public int missionCompletionExp = 2500;
    public int firstPlaceExpBonus = 2500;
    public int secondPlaceExpBonus = 1500;
    public int thirdPlaceExpBonus = 750;
    public int participationExp = 500;
    public int levelUpMoneyReward = 400;

    [Header("No Brake Challenge")]
    [Range(0f, 1f)] public float brakeEffectiveness = 0f;
    [Range(0f, 1f)] public float handbrakeEffectiveness = 0f;

    [Header("Race Start")]
    public bool useCountdown = true;
    public bool useCinematicRaceIntro = true;
    public float cinematicRaceIntroDuration = 7f;
    public float cinematicRaceIntroDistance = 16f;
    public float cinematicRaceIntroLeftOffset = 3f;
    public float countdownStepDuration = 1f;
    public float goTextDuration = 1f;

    [Header("Route Warnings")]
    public float wrongDirectionMinimumSpeedKmh = 12f;
    public float wrongDirectionDetectionDelay = 1.2f;
    [Range(-1f, 0f)] public float wrongDirectionDotThreshold = -0.25f;
    public float routeWarningDisplayDuration = 1.6f;
    [Min(1)] public int missedCheckpointRespawnCountdown = 3;

    [Header("Opponent Spawning")]
    public bool autoSpawnOpponents = true;
    public int opponentCount = 3;
    public bool usePlayerCarForOpponents = true;
    public RCCP_AIArcadePreset[] opponentAIPresets;
    public RCCP_AIArcadePreset.Difficulty[] opponentDifficulties = {
        RCCP_AIArcadePreset.Difficulty.Medium,
        RCCP_AIArcadePreset.Difficulty.Hard,
        RCCP_AIArcadePreset.Difficulty.Medium
    };
    public Transform[] opponentSpawnPoints;
    public float spawnRowSpacing = 8f;
    public float spawnColumnSpacing = 4f;
    public int spawnCarsPerRow = 2;
    public string[] opponentDisplayNames = {
        "Rex",
        "Vega",
        "Blaze",
        "Axel",
        "Rook",
        "Nova",
        "Diesel",
        "Storm"
    };
    
    [Header("Checkpoint Visuals")]
    public GameObject checkpointPrefab;
    public Vector3 checkpointVisualOffset = new Vector3(0f, 2f, 0f);
    public bool showOnlyNextCheckpoint = true;

    [Header("Player Recovery")]
    public float playerRouteRespawnHeight = 3f;
    public float playerRouteRespawnForwardOffset = 2f;
    public float playerRouteRespawnCooldown = 0.6f;

    [Header("MiniMap")]
    public MiniMap gameplayMiniMap;
    public bool autoFindMiniMap = true;

    private RaceRacer playerRacer = new RaceRacer { displayName = "Player" };
    private RaceRacer[] allRacers = Array.Empty<RaceRacer>();
    private GameObject[] checkpointVisuals = Array.Empty<GameObject>();
    private readonly List<GameObject> spawnedOpponentObjects = new List<GameObject>();
    private bool raceStarted = false;
    private float eliminationTimer = 0f;
    private string lastRacePositionText = string.Empty;
    private Coroutine racePositionAnimationCoroutine;
    private float raceElapsedTime = 0f;
    private float driftElapsedTime = 0f;
    private bool missionResultsShown = false;
    private bool missionRewardsApplied = false;
    private bool missionSucceeded = false;
    private int missionRewardEarned = 0;
    private int missionExpEarned = 0;
    private int missionLevelRewardEarned = 0;
    private int missionStartingExpTotal = 0;
    private int missionFinalExpTotal = 0;
    private int missionStartingLevel = 1;
    private int missionFinalLevel = 1;
    private int nextRaceFinishPosition = 1;
    private Coroutine expAnimationCoroutine;
    private Coroutine expRewardAnimationCoroutine;
    private Coroutine expSliderAnimationCoroutine;
    private Coroutine finishSummaryAnimationCoroutine;
    private Coroutine finishContinueSelectionCoroutine;
    private Coroutine raceStateClearCoroutine;
    private Coroutine raceStartFlowCoroutine;
    private ArcadeVP.WaypointCircuit runtimeRaceWaypointCircuit;
    private RCCP_AIWaypointsContainer runtimeRaceWaypoints;
    private readonly List<Waypoint_System> resolvedWaypointSystems = new List<Waypoint_System>();
    private InputAction playerRouteRespawnAction;
    private float lastPlayerRouteRespawnTime = -999f;
    private float routeWarningsEnabledTime;
    private float wrongDirectionTimer;
    private float activeRouteWarningUntil;
    private string activeRouteWarningText = string.Empty;
    private int lastUnexpectedCheckpointIndex = -1;
    private Coroutine missedCheckpointRespawnCoroutine;
    private bool nitroHapticsActive;
    private int lastHapticGear = int.MinValue;

    [Header("Drifting Settings")]
    public bool driftingNow = false;
    public float totalDriftPoints = 0f;      //  Total drift points.
    public float currentDriftPoints = 0f;  
    [Space()]
    public float currentDriftCoins = 0f;        //  Current drift coins.
    public float totalDriftCoins = 0f;        //  Total drift coins.
    [Space()]
    public float currentMP = 1f;       //  Current drift multiplier.
    [Space()]
    public float totalDriftTime = 0f;     //  Total drifting time.
    public float currentDriftComboTime = 0f;     //  Continuous drift time used for combo progression.
    public float totalDriftDistance = 0f;     //  Total drifting time.
    public bool canScore = true;        //  Can score now?
    private Vector3 lastPosition;
    public int driftPointsMP = 200;       //	Drift points multiplier.
    public int driftCoinsMP = 10;       //	Drift coins multiplier.
    public float maxDriftComboMultiplier = 5f;
    public float comboStepDuration = 1.5f;
    public float comboStepValue = 0.5f;
    public float comboStartDelay = 2.5f;
    public float driftTime = 1f;        //	Timer for resetting the drift.
    public float driftSpeed = 25f;        //	Speed limit for drift score.
    public bool resetDriftPointsAfterCollision = true;      //	Resets current drift score on collisions.
    public float minimumCollision = 5f;     //	Minimum collision limit for resetting the drift score.
    private bool driftInterruptedByCollision = false;
    private float driftInterruptTimer = 0f;
    [SerializeField] private float driftInterruptDuration = 0.5f;

    [Header("Drifting UI")] 
    public Slider DriftTimeSlider;
    public Slider DriftProgressSlider;
    public TMP_Text scoreText;
    public TMP_Text TotalScoreText;
    public TMP_Text DriftMedalText;
    public TMP_Text DriftTargetText;
    public TMP_Text DriftComboText;
    public TMP_Text DriftModeText;
    public TMP_Text DriftTimerText;
    public Image BronzeMedalImage;
    public Image SilverMedalImage;
    public Image GoldMedalImage;
    [Range(0f, 1f)] public float lockedMedalAlpha = 0.35f;
    [Range(0f, 1f)] public float unlockedMedalAlpha = 1f;
    public float comboPulseDuration = 0.18f;
    public float comboPulseScale = 0.2f;

    [Header("Drift Targets")]
    public bool useCurrentMapDriftTarget = true;
    public float bronzeTargetScore = 5000f;
    public float silverTargetMultiplier = 1.5f;
    public float goldTargetMultiplier = 2f;
    public float bronzeComboTarget = 2f;
    public float silverComboTarget = 3.5f;
    public float goldComboTarget = 5f;
    public bool useCurrentMapTargetDriftSettings = true;
    public float targetDriftScore = 50000f;
    public float targetDriftTimeLimit = 60f;
    public float targetDriftWarningThreshold = 10f;
    public Color targetDriftTimerNormalColor = Color.white;
    public Color targetDriftTimerWarningColor = Color.red;
    public float targetDriftPulseSpeed = 7f;
    public float targetDriftPulseScale = 0.18f;

    private bool driftModeFinished = false;
    private float currentDriftDisplayedScore = 0f;
    private float lastDisplayedComboMultiplier = 0f;
    private Coroutine driftComboAnimationCoroutine;
    private float targetDriftTimeRemaining = 0f;
    //  When player achieved a score.
    // public delegate void onDriftScoreAchieved(BD_PlayerManager Player);
    // public static event onDriftScoreAchieved OnDriftScoreAchieved;
    private void Start()
    {
        Time.timeScale = 1f;
        ResetRCCPInputForGameplay();

        if (SelectedCareerMission.Mission != null)
        {
            ApplyMission(SelectedCareerMission.Mission);
        }
        else
        {
            RaceType = SelectedGameMode.RaceType;
            ApplyCurrentMapSettings();
        }
        Debug.Log("GAMEPLAY selected: " + RaceType);
        SetUpRaceStyle(GetDrivingStyleIndex());
        
        InstancePlayer();
        if (CarController != null)
        {
            ResetPlayerVehicleForMission();
            CarController.useCustomBehavior = true;
            CarController.customBehaviorIndex = GetDrivingStyleIndex();
        }

        SetUpRaceStyle(GetDrivingStyleIndex());
        

        if (finishSummaryScreen != null)
            finishSummaryScreen.SetActive(false);

        if (finishExpScreen != null)
            finishExpScreen.SetActive(false);

        if (finishSummaryContinueButton != null)
            finishSummaryContinueButton.SetActive(false);

        if (finishExpContinueButton != null)
            finishExpContinueButton.SetActive(false);

        if (scoreText != null)
            scoreText.gameObject.SetActive(false);

        if (DriftTimeSlider != null)
            DriftTimeSlider.value = totalDriftTime;

        InitializeDriftMode();

        if (IsRaceMode() && autoSpawnOpponents)
            SpawnAutomaticOpponents();

        InitializeRaceMode();

        if (IsRaceMode())
            BeginRaceStartFlow();
        else
            StartCoroutine(EnablePlayerControlNextFrame());

        InitializeMiniMap();
        StartCoroutine(RemoveDuplicateEventSystemsAfterSceneSetup());
        
    }

    private IEnumerator EnablePlayerControlNextFrame()
    {
        yield return null;

        if (CarController == null)
            yield break;

        ResetRCCPInputForGameplay();
        ResetPlayerVehicleForMission();
    }

    private void ResetPlayerVehicleForMission()
    {
        if (CarController == null)
            return;

        CarController.externalControl = false;
        CarController.SetCanControl(true);
        CarController.SetEngine(true);

        if (CarController.Inputs != null)
            CarController.Inputs.DisableOverrideInputs();

        if (CarController.Rigid != null)
        {
            CarController.Rigid.isKinematic = false;
            CarController.Rigid.WakeUp();
        }

        if (CarController.Gearbox == null)
            return;

        CarController.Gearbox.forceToNGear = false;
        CarController.Gearbox.forceToRGear = false;
        CarController.Gearbox.automaticGearSelector = RCCP_Gearbox.SemiAutomaticDNRPGear.D;
        CarController.Gearbox.currentGear = 0;
        if (CarController.Gearbox.currentGearState == null)
            CarController.Gearbox.currentGearState = new RCCP_Gearbox.CurrentGearState();

        CarController.Gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
        CarController.Gearbox.gearInput = 1f;
    }

    private void ResetRCCPInputForGameplay()
    {
        RCCP_InputManager inputManager = RCCP_InputManager.Instance;

        if (inputManager == null)
            return;

        inputManager.overrideInputs = false;

        if (inputManager.inputActionsInstance == null && RCCP_InputActions.Instance != null)
            inputManager.inputActionsInstance = RCCP_InputActions.Instance.inputActions;

        InputActionAsset inputActions = inputManager.inputActionsInstance;

        if (inputActions == null)
            return;

        inputActions.Enable();

        InputActionMap vehicleMap = inputActions.FindActionMap("Vehicle");
        if (vehicleMap != null)
            vehicleMap.Enable();

        InputActionMap cameraMap = inputActions.FindActionMap("Camera");
        if (cameraMap != null)
            cameraMap.Enable();

        InputActionMap optionalMap = inputActions.FindActionMap("Optional");
        if (optionalMap != null)
            optionalMap.Enable();
    }

    private void EnablePlayerRouteRespawnInput()
    {
        if (playerRouteRespawnAction != null)
            return;

        playerRouteRespawnAction = new InputAction("Player Route Respawn", InputActionType.Button, "<Gamepad>/rightStickPress");
        playerRouteRespawnAction.performed += RespawnPlayerOnRouteCtx;
        playerRouteRespawnAction.Enable();
    }

    private void DisablePlayerRouteRespawnInput()
    {
        if (playerRouteRespawnAction == null)
            return;

        playerRouteRespawnAction.performed -= RespawnPlayerOnRouteCtx;
        playerRouteRespawnAction.Disable();
        playerRouteRespawnAction.Dispose();
        playerRouteRespawnAction = null;
    }

    private void RespawnPlayerOnRouteCtx(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;

        RespawnPlayerOnRoute();
    }

    public void RespawnPlayerOnRoute()
    {
        if (CarController == null || missionResultsShown)
            return;

        if (Time.unscaledTime - lastPlayerRouteRespawnTime < playerRouteRespawnCooldown)
            return;

        lastPlayerRouteRespawnTime = Time.unscaledTime;

        ArcadeVP.WaypointCircuit raceCircuit = GetRaceWaypointCircuit();
        bool preserveCheckpointProgress;
        Pose respawnPose = GetPlayerRouteRespawnPose(raceCircuit, out preserveCheckpointProgress);
        Transform carTransform = CarController.transform;

        if (CarController.Rigid != null)
        {
            CarController.Rigid.linearVelocity = Vector3.zero;
            CarController.Rigid.angularVelocity = Vector3.zero;
            CarController.Rigid.isKinematic = false;
            CarController.Rigid.position = respawnPose.position;
            CarController.Rigid.rotation = respawnPose.rotation;
        }

        carTransform.SetPositionAndRotation(respawnPose.position, respawnPose.rotation);

        if (CarController.Rigid != null)
            CarController.Rigid.WakeUp();

        ResetPlayerVehicleForMission();
        SyncPlayerRaceProgressAfterRespawn(raceCircuit, preserveCheckpointProgress);
    }

    private Pose GetPlayerRouteRespawnPose(ArcadeVP.WaypointCircuit raceCircuit, out bool preserveCheckpointProgress)
    {
        preserveCheckpointProgress = false;

        if (TryGetLastPassedCheckpointRespawnPose(out Pose checkpointPose))
        {
            preserveCheckpointProgress = true;
            return checkpointPose;
        }

        if (raceCircuit == null || raceCircuit.Length <= 0f || CarController == null)
        {
            if (SpawnPoint != null)
                return new Pose(SpawnPoint.position + Vector3.up * playerRouteRespawnHeight, SpawnPoint.rotation);

            Transform carTransform = CarController != null ? CarController.transform : transform;
            return new Pose(carTransform.position + Vector3.up * playerRouteRespawnHeight, carTransform.rotation);
        }

        float hintDistance = playerRacer != null && playerRacer.progressInitialized
            ? playerRacer.currentCircuitDistance
            : FindClosestDistanceAlongRaceRoute(CarController.transform.position);

        float routeDistance = FindClosestDistanceAlongRaceRoute(CarController.transform.position, hintDistance);
        ArcadeVP.WaypointCircuit.RoutePoint routePoint = raceCircuit.GetRoutePoint(routeDistance + playerRouteRespawnForwardOffset);
        Vector3 forward = routePoint.direction;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = CarController.transform.forward;

        forward.Normalize();
        Vector3 position = routePoint.position + Vector3.up * playerRouteRespawnHeight;
        position = GetGroundedPlayerRespawnPosition(position);

        return new Pose(position, Quaternion.LookRotation(forward, Vector3.up));
    }

    private bool TryGetLastPassedCheckpointRespawnPose(out Pose respawnPose)
    {
        respawnPose = default;

        Checkpoint_Manager checkpointManager = ResolveCheckpointManager();
        if (checkpointManager == null || checkpointManager.checkpoints == null || checkpointManager.checkpoints.Count == 0)
            return false;

        if (playerRacer == null || !playerRacer.checkpointProgressInitialized || playerRacer.currentCheckpointIndex < 0)
            return false;

        int checkpointIndex = Mathf.Clamp(playerRacer.currentCheckpointIndex, 0, checkpointManager.checkpoints.Count - 1);
        Transform checkpoint = checkpointManager.checkpoints[checkpointIndex];
        if (checkpoint == null)
            return false;

        Vector3 forward = checkpoint.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = CarController != null ? CarController.transform.forward : transform.forward;

        forward.Normalize();

        Vector3 position = checkpoint.position + Vector3.up * playerRouteRespawnHeight;
        position = GetGroundedPlayerRespawnPosition(position);
        respawnPose = new Pose(position, Quaternion.LookRotation(forward, Vector3.up));
        return true;
    }

    private Vector3 GetGroundedPlayerRespawnPosition(Vector3 routePosition)
    {
        Vector3 rayOrigin = routePosition + Vector3.up * 25f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 80f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * playerRouteRespawnHeight;

        return routePosition;
    }

    private void SyncPlayerRaceProgressAfterRespawn(ArcadeVP.WaypointCircuit raceCircuit, bool preserveCheckpointProgress)
    {
        if (playerRacer == null || CarController == null || raceCircuit == null)
            return;

        float routeDistance = FindClosestDistanceAlongRaceRoute(CarController.transform.position, playerRacer.currentCircuitDistance);
        playerRacer.racerTransform = CarController.transform;
        playerRacer.currentCircuitDistance = routeDistance;
        playerRacer.startCircuitDistance = routeDistance;
        playerRacer.currentWaypointIndex = GetNextWaypointIndexFromCircuitDistance(routeDistance);
        playerRacer.progressInitialized = false;

        if (!preserveCheckpointProgress)
            playerRacer.checkpointProgressInitialized = false;
        else if (HasCheckpointProgressSource())
            playerRacer.currentWaypointIndex = playerRacer.nextCheckpointIndex;
    }

    private IEnumerator RemoveDuplicateEventSystemsAfterSceneSetup()
    {
        yield return null;
        yield return null;

        EventSystem[] eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (eventSystems.Length <= 1)
            yield break;

        EventSystem sceneEventSystem = null;
        Scene activeScene = SceneManager.GetActiveScene();

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem.gameObject.scene == activeScene)
            {
                sceneEventSystem = eventSystem;
                break;
            }
        }

        if (sceneEventSystem == null)
            sceneEventSystem = EventSystem.current;

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem == sceneEventSystem)
                continue;

                Destroy(eventSystem.gameObject);
        }
    }

    private void InitializeMiniMap()
    {
        if (gameplayMiniMap == null && autoFindMiniMap)
            gameplayMiniMap = FindFirstObjectByType<MiniMap>(FindObjectsInactive.Include);

        if (gameplayMiniMap == null)
            return;

        StartCoroutine(InitializeMiniMapNextFrame());
    }

    private IEnumerator InitializeMiniMapNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (gameplayMiniMap == null)
            yield break;

        gameplayMiniMap.player1 = CarController != null ? CarController.transform : null;
        gameplayMiniMap.player2 = null;
        gameplayMiniMap.opponents = GetMiniMapOpponentTargets();
        gameplayMiniMap.Init();
    }

    private Transform[] GetMiniMapOpponentTargets()
    {
        if (aiRacers == null || aiRacers.Length == 0)
            return Array.Empty<Transform>();

        List<Transform> targets = new List<Transform>();

        for (int i = 0; i < aiRacers.Length; i++)
        {
            RaceRacer aiRacer = aiRacers[i];

            if (aiRacer == null || aiRacer.racerTransform == null)
                continue;

            targets.Add(aiRacer.racerTransform);
        }

        return targets.ToArray();
    }

    private void InitializeDriftMode()
    {
        if (!IsDriftScoringMode())
            return;

        driftModeFinished = false;
        currentMP = 1f;
        totalDriftPoints = 0f;
        currentDriftPoints = 0f;
        currentDriftCoins = 0f;
        currentDriftComboTime = 0f;
        totalDriftTime = 0f;
        targetDriftTimeRemaining = GetTargetDriftTimeLimit();
        driftElapsedTime = 0f;
        missionResultsShown = false;
        missionRewardsApplied = false;
        missionSucceeded = false;
        missionRewardEarned = 0;
        missionExpEarned = 0;
        missionLevelRewardEarned = 0;
        missionStartingExpTotal = 0;
        missionFinalExpTotal = 0;
        missionStartingLevel = Mathf.Max(1, SaveManager.Instance != null && SaveManager.Instance.saveData != null ? SaveManager.Instance.saveData.currentLevel : 1);
        missionFinalLevel = missionStartingLevel;
        lastDisplayedComboMultiplier = 0f;
        canScore = true;
        UpdateDriftUI();
    }
    
    private void OnEnable()
    {
        RCCP_Events.OnRCCPCollision += OnCarCollision;
        RCCP_InputManager.OnGearShiftedUp += OnGearShiftHaptic;
        RCCP_InputManager.OnGearShiftedDown += OnGearShiftHaptic;
        EnablePlayerRouteRespawnInput();
    }

    private void OnDisable()
    {
        RCCP_Events.OnRCCPCollision -= OnCarCollision;
        RCCP_InputManager.OnGearShiftedUp -= OnGearShiftHaptic;
        RCCP_InputManager.OnGearShiftedDown -= OnGearShiftHaptic;
        DisablePlayerRouteRespawnInput();
    }

    private void OnGearShiftHaptic() => GameHaptics.GearShift();

    private void ApplyCurrentMapSettings()
    {
        if (SelectedCareerMission.Mission != null)
            return;

        if (!useCurrentMapModeSettings || GlobalCarData.thismap == null)
            return;

        MapSO currentMap = GlobalCarData.thismap;

        if (currentMap.raceLaps > 0)
            totalRaceLaps = currentMap.raceLaps;
        else if (currentMap.lap > 0)
            totalRaceLaps = currentMap.lap;

        if (currentMap.opponentCount > 0)
            opponentCount = currentMap.opponentCount;

        if (currentMap.eliminationInterval > 0f)
            eliminationInterval = currentMap.eliminationInterval;

        brakeEffectiveness = Mathf.Clamp01(currentMap.brakeEffectiveness);
        handbrakeEffectiveness = Mathf.Clamp01(currentMap.handbrakeEffectiveness);

        if (currentMap.driftBronzeTarget > 0)
            bronzeTargetScore = currentMap.driftBronzeTarget;
        else if (currentMap.target > 0)
            bronzeTargetScore = currentMap.target;

        if (currentMap.driftSilverMultiplier > 0f)
            silverTargetMultiplier = currentMap.driftSilverMultiplier;

        if (currentMap.driftGoldMultiplier > 0f)
            goldTargetMultiplier = currentMap.driftGoldMultiplier;

        if (currentMap.comboBronzeTarget > 0f)
            bronzeComboTarget = currentMap.comboBronzeTarget;

        if (currentMap.comboSilverTarget > 0f)
            silverComboTarget = currentMap.comboSilverTarget;

        if (currentMap.comboGoldTarget > 0f)
            goldComboTarget = currentMap.comboGoldTarget;

        if (currentMap.targetDriftScore > 0)
            targetDriftScore = currentMap.targetDriftScore;
        else if (currentMap.target > 0)
            targetDriftScore = currentMap.target;

        if (currentMap.targetDriftTimeLimit > 0)
            targetDriftTimeLimit = currentMap.targetDriftTimeLimit;
        else if (currentMap.time > 0)
            targetDriftTimeLimit = currentMap.time;
    }


    public void InstancePlayer()
    {
        CarSO selectedCar = GetCurrentCarSO();
        if (selectedCar == null)
            return;

        string playerPrefabLocation = selectedCar.carPrefabLocation;
        GameObject playerPrefab = Resources.Load<GameObject>(playerPrefabLocation);

        if (playerPrefab == null)
        {
            Debug.LogError($"Player car prefab not found in Resources: {playerPrefabLocation}");
            return;
        }

        player = Instantiate(playerPrefab, SpawnPoint);
        CarController = player.GetComponent<RCCP_CarController>();
        if (CarController != null)
        {
            PrepareGameplayCameraProfileBeforeRegister(selectedCar);
            RCCP.RegisterPlayerVehicle(CarController, true, true);
            ApplyGameplayCameraProfile(selectedCar);
            StartCoroutine(ApplyGameplayCameraProfileNextFrame(selectedCar));
        }
    }

    private CarSO GetCurrentCarSO()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return null;

        if (GlobalCarData._carlists == null || GlobalCarData._carlists.Count == 0)
            return null;

        int carIndex = Mathf.Clamp(SaveManager.Instance.saveData.currentCar, 0, GlobalCarData._carlists.Count - 1);
        return GlobalCarData._carlists[carIndex];
    }

    private IEnumerator ApplyGameplayCameraProfileNextFrame(CarSO selectedCar)
    {
        yield return null;
        ApplyGameplayCameraProfile(selectedCar);
    }

    private void ApplyGameplayCameraProfile(CarSO selectedCar)
    {
        if (selectedCar == null || !selectedCar.overrideGameplayCamera)
            return;

        RCCP_Camera gameplayCamera = RCCP_SceneManager.Instance != null ? RCCP_SceneManager.Instance.activePlayerCamera : null;
        if (gameplayCamera == null)
            return;

        gameplayCamera.cameraMode = RCCP_Camera.CameraMode.TPS;
        gameplayCamera.TPSAutoFocus = selectedCar.gameplayCameraAutoFocus;
        gameplayCamera.TPSDistance = selectedCar.gameplayCameraDistance;
        gameplayCamera.TPSHeight = selectedCar.gameplayCameraHeight;
        gameplayCamera.TPSPitch = selectedCar.gameplayCameraPitch;
        gameplayCamera.TPSOffset = selectedCar.gameplayCameraOffset;
    }

    private void PrepareGameplayCameraProfileBeforeRegister(CarSO selectedCar)
    {
        if (selectedCar == null || !selectedCar.overrideGameplayCamera)
            return;

        RCCP_Camera gameplayCamera = RCCP_SceneManager.Instance != null ? RCCP_SceneManager.Instance.activePlayerCamera : null;
        if (gameplayCamera == null)
            return;

        gameplayCamera.TPSAutoFocus = selectedCar.gameplayCameraAutoFocus;
    }

    public void SetUpRaceStyle(int type)
    {
        // 0  = Balanced
        // 1  = Drift
        // 2  = Race
        // 3  = Arcade
        RCCP_Settings.Instance.overrideBehavior = true;
        RCCP_Settings.Instance.behaviorSelectedIndex = type;
        RCCP_Events.Event_OnBehaviorChanged();
        
        Debug.Log("RCCP behavior index: " + type);
        // Debug.Log(RCCP_Settings.Instance.behaviorTypes[type].behaviorName.ToString()); // Test Debug style
    }

    private int GetDrivingStyleIndex()
    {
        switch (RaceType)
        {
            case RaceType.Racing:
                return 2;
            case RaceType.Elimination:
            case RaceType.NoBrakeChallenge:
                return 2;
            case RaceType.FreeDrift:
            case RaceType.DriftScore:
            case RaceType.TargetDrift:
            case RaceType.ComboMaster:
                return 1;
            default:
                return 0;
        }
    }

    private void InitializeRaceMode()
    {
        if (!IsRaceMode())
            return;

        EnsureRaceWaypointSources();
        ArcadeVP.WaypointCircuit raceCircuit = GetRaceWaypointCircuit();

        playerRacer = new RaceRacer
        {
            displayName = "Player",
            racerTransform = CarController != null ? CarController.transform : null,
            aiDriver = null,
            currentWaypointIndex = GetClosestWaypointIndex(CarController != null ? CarController.transform : null),
            currentCircuitDistance = raceCircuit != null && CarController != null ? FindClosestDistanceAlongRaceRoute(CarController.transform.position) : 0f,
            completedLaps = 0,
            finished = false,
            eliminated = false,
            finishPosition = 0,
            finishTime = 0f,
            progressInitialized = false,
            startCircuitDistance = raceCircuit != null && CarController != null ? FindClosestDistanceAlongRaceRoute(CarController.transform.position) : 0f,
            lapCountingArmed = false,
            currentSegmentIndex = 0,
            currentProgressPathIndex = -1,
            sharedRankingProgress = 0f,
            currentCheckpointIndex = -1,
            nextCheckpointIndex = 0,
            checkpointProgressInitialized = false
        };

        int aiCount = aiRacers != null ? aiRacers.Length : 0;
        allRacers = new RaceRacer[aiCount + 1];
        allRacers[0] = playerRacer;

        for (int i = 0; i < aiCount; i++)
        {
            RaceRacer aiRacer = aiRacers[i];

            if (aiRacer == null)
                continue;

            if (string.IsNullOrWhiteSpace(aiRacer.displayName) || IsGeneratedOpponentName(aiRacer.displayName))
                aiRacer.displayName = GetOpponentDisplayName(i);

            if (aiRacer.aiDriver != null)
            {
                aiRacer.aiDriver.enabled = false;
                aiRacer.racerTransform = aiRacer.aiDriver.transform;
            }

            aiRacer.currentWaypointIndex = GetClosestWaypointIndex(aiRacer.racerTransform);
            aiRacer.currentCircuitDistance = raceCircuit != null && aiRacer.racerTransform != null ? FindClosestDistanceAlongRaceRoute(aiRacer.racerTransform.position) : 0f;

            aiRacer.completedLaps = 0;
            aiRacer.finished = false;
            aiRacer.eliminated = false;
            aiRacer.finishPosition = 0;
            aiRacer.finishTime = 0f;
            aiRacer.progressInitialized = false;
            aiRacer.startCircuitDistance = aiRacer.currentCircuitDistance;
            aiRacer.lapCountingArmed = false;
            aiRacer.currentSegmentIndex = 0;
            aiRacer.currentProgressPathIndex = -1;
            aiRacer.sharedRankingProgress = FindClosestWaypointProgress(aiRacer);
            aiRacer.currentCheckpointIndex = -1;
            aiRacer.nextCheckpointIndex = 0;
            aiRacer.checkpointProgressInitialized = false;
            allRacers[i + 1] = aiRacer;
        }

        eliminationTimer = eliminationInterval;
        raceElapsedTime = 0f;
        nextRaceFinishPosition = 1;
        missionResultsShown = false;
        missionRewardsApplied = false;
        missionSucceeded = false;
        missionRewardEarned = 0;
        missionExpEarned = 0;
        missionLevelRewardEarned = 0;
        missionStartingExpTotal = 0;
        missionFinalExpTotal = 0;
        missionStartingLevel = Mathf.Max(1, SaveManager.Instance != null && SaveManager.Instance.saveData != null ? SaveManager.Instance.saveData.currentLevel : 1);
        missionFinalLevel = missionStartingLevel;
        SpawnCheckpointVisuals();
        UpdateRaceUI();
    }

    private void BeginRaceStartFlow()
    {
        SetRaceParticipantsControl(false);
        raceStarted = false;

        if (raceStartFlowCoroutine != null)
            StopCoroutine(raceStartFlowCoroutine);

        raceStartFlowCoroutine = StartCoroutine(RaceStartFlowCoroutine());
    }

    private IEnumerator RaceStartFlowCoroutine()
    {
        if (useCinematicRaceIntro && cinematicRaceIntroDuration > 0f)
            yield return StartCoroutine(RaceIntroCinematicCoroutine());

        if (useCountdown)
            yield return StartCoroutine(RaceCountdownCoroutine());
        else
            StartRaceNow();

        raceStartFlowCoroutine = null;
    }

    private IEnumerator RaceIntroCinematicCoroutine()
    {
        if (raceStateText != null)
            raceStateText.text = string.Empty;

        yield return null;

        ApplyRaceIntroCinematicSettings();
        SetGameplayCameraMode(RCCP_Camera.CameraMode.CINEMATIC);
        yield return new WaitForSeconds(cinematicRaceIntroDuration);
        RestoreStandardGameplayCamera();
    }

    private IEnumerator RaceCountdownCoroutine()
    {
        string[] countdownTexts = { "3", "2", "1" };

        for (int i = 0; i < countdownTexts.Length; i++)
        {
            if (raceStateText != null)
                raceStateText.text = countdownTexts[i];

            GameHaptics.StartTick();
            yield return new WaitForSeconds(countdownStepDuration);
        }

        if (raceStateText != null)
            raceStateText.text = UILocalization.Get("ui.go", "GO!");

        GameHaptics.StartGo();
        StartRaceNow();
        yield return new WaitForSeconds(goTextDuration);

        if (raceStateText != null && !playerRacer.finished)
            raceStateText.text = string.Empty;
    }

    private void SetGameplayCameraMode(RCCP_Camera.CameraMode cameraMode)
    {
        RCCP_Camera gameplayCamera = RCCP_SceneManager.Instance != null ? RCCP_SceneManager.Instance.activePlayerCamera : null;
        if (gameplayCamera == null)
            return;

        gameplayCamera.ChangeCamera(cameraMode);
    }

    private void ApplyRaceIntroCinematicSettings()
    {
        RCCP_CinematicCamera cinematicCamera = RCCP_CinematicCamera.Instance;
        if (cinematicCamera == null)
            return;

        cinematicCamera.followDistance = cinematicRaceIntroDistance;
        cinematicCamera.leftOffset = cinematicRaceIntroLeftOffset;
    }

    private void RestoreStandardGameplayCamera()
    {
        RCCP_Camera gameplayCamera = RCCP_SceneManager.Instance != null ? RCCP_SceneManager.Instance.activePlayerCamera : null;
        if (gameplayCamera != null)
            gameplayCamera.ChangeCamera(RCCP_Camera.CameraMode.TPS);

        ApplyGameplayCameraProfile(GetCurrentCarSO());
    }

    private void StartRaceNow()
    {
        raceStarted = true;
        routeWarningsEnabledTime = Time.unscaledTime + goTextDuration;
        wrongDirectionTimer = 0f;
        activeRouteWarningUntil = 0f;
        activeRouteWarningText = string.Empty;
        lastUnexpectedCheckpointIndex = -1;
        eliminationTimer = eliminationInterval;
        SetRaceParticipantsControl(true);
        RefreshOpponentDrivers(true);
    }

    private void UpdatePlayerRouteWarnings()
    {
        if (!raceStarted || missionResultsShown || playerRacer == null || playerRacer.finished ||
            CarController == null || Time.unscaledTime < routeWarningsEnabledTime)
            return;

        if (missedCheckpointRespawnCoroutine != null)
            return;

        CheckMissedCheckpoint();
        CheckWrongDirection();

        if (!string.IsNullOrEmpty(activeRouteWarningText) &&
            Time.unscaledTime >= activeRouteWarningUntil)
        {
            if (raceStateText != null && raceStateText.text == activeRouteWarningText)
                raceStateText.text = string.Empty;

            activeRouteWarningText = string.Empty;
        }
    }

    private void CheckMissedCheckpoint()
    {
        Checkpoint_Manager checkpointManager = ResolveCheckpointManager();
        if (checkpointManager == null || checkpointManager.checkpoints == null ||
            checkpointManager.checkpoints.Count < 2 || !playerRacer.checkpointProgressInitialized)
            return;

        int unexpectedIndex = -1;
        for (int index = 0; index < checkpointManager.checkpoints.Count; index++)
        {
            if (index == playerRacer.nextCheckpointIndex || index == playerRacer.currentCheckpointIndex)
                continue;

            Transform checkpoint = checkpointManager.checkpoints[index];
            if (checkpoint != null && IsInsideCheckpoint(checkpoint, CarController.transform.position))
            {
                unexpectedIndex = index;
                break;
            }
        }

        if (unexpectedIndex < 0)
        {
            lastUnexpectedCheckpointIndex = -1;
            return;
        }

        if (unexpectedIndex == lastUnexpectedCheckpointIndex)
            return;

        lastUnexpectedCheckpointIndex = unexpectedIndex;
        GameHaptics.CheckpointMissed();
        missedCheckpointRespawnCoroutine = StartCoroutine(MissedCheckpointRespawnCoroutine());
    }

    private IEnumerator MissedCheckpointRespawnCoroutine()
    {
        if (CarController != null)
        {
            CarController.SetCanControl(false);

            if (CarController.Inputs != null)
                CarController.Inputs.OverrideInputs(new RCCP_Inputs());
        }

        int countdown = Mathf.Max(1, missedCheckpointRespawnCountdown);
        while (countdown > 0 && raceStarted && !missionResultsShown && !playerRacer.finished)
        {
            if (raceStateText != null)
            {
                raceStateText.text = UILocalization.Get(
                    "race.you_missed_checkpoint",
                    "YOU MISSED THE CHECKPOINT");
                raceStateText.text += "\n" + string.Format(
                    UILocalization.Get("race.respawn_in_format", "RESPAWN IN {0}"),
                    countdown);
            }

            GameHaptics.RespawnTick();
            yield return new WaitForSecondsRealtime(1f);
            countdown--;
        }

        if (raceStarted && !missionResultsShown && !playerRacer.finished)
        {
            RespawnPlayerOnRoute();
            GameHaptics.Respawn();
        }

        if (CarController != null)
        {
            if (CarController.Inputs != null)
                CarController.Inputs.DisableOverrideInputs();

            CarController.SetCanControl(raceStarted && !missionResultsShown);
        }

        if (raceStateText != null)
            raceStateText.text = string.Empty;

        activeRouteWarningText = string.Empty;
        wrongDirectionTimer = 0f;
        lastUnexpectedCheckpointIndex = -1;
        routeWarningsEnabledTime = Time.unscaledTime + 0.75f;
        missedCheckpointRespawnCoroutine = null;
    }

    private void CheckWrongDirection()
    {
        Rigidbody rigidbody = CarController.Rigid;
        ArcadeVP.WaypointCircuit circuit = GetRaceWaypointCircuit();
        if (rigidbody == null || circuit == null || circuit.Length <= 0f)
        {
            wrongDirectionTimer = 0f;
            return;
        }

        Vector3 velocity = rigidbody.linearVelocity;
        velocity.y = 0f;
        float speedKmh = velocity.magnitude * 3.6f;
        float routeDistance = FindClosestDistanceAlongRaceRoute(
            CarController.transform.position,
            playerRacer.progressInitialized ? playerRacer.currentCircuitDistance : (float?)null);
        Vector3 routeDirection = circuit.GetRoutePoint(routeDistance + 2f).direction;
        routeDirection.y = 0f;

        bool movingWrongWay = speedKmh >= wrongDirectionMinimumSpeedKmh &&
                              routeDirection.sqrMagnitude > 0.001f &&
                              Vector3.Dot(velocity.normalized, routeDirection.normalized) <= wrongDirectionDotThreshold;

        if (!movingWrongWay)
        {
            wrongDirectionTimer = 0f;
            return;
        }

        wrongDirectionTimer += Time.unscaledDeltaTime;
        if (wrongDirectionTimer >= wrongDirectionDetectionDelay)
            ShowRouteWarning(UILocalization.Get("race.wrong_direction", "WRONG DIRECTION"));
    }

    private void ShowRouteWarning(string warning)
    {
        if (raceStateText == null || string.IsNullOrWhiteSpace(warning))
            return;

        // Countdown, finish, and elimination messages have priority.
        if (!string.IsNullOrEmpty(raceStateText.text) &&
            raceStateText.text != activeRouteWarningText)
            return;

        activeRouteWarningText = warning;
        activeRouteWarningUntil = Time.unscaledTime + routeWarningDisplayDuration;
        raceStateText.text = warning;
    }

    private void SetRaceParticipantsControl(bool state)
    {
        if (CarController != null)
            CarController.canControl = state;

        if (aiRacers == null)
            return;

        for (int i = 0; i < aiRacers.Length; i++)
        {
            RaceRacer aiRacer = aiRacers[i];

            if (aiRacer == null)
                continue;

            RCCP_CarController aiCarController = null;

            if (aiRacer.aiDriver != null)
                aiCarController = aiRacer.aiDriver.CarController;

            if (aiCarController == null && aiRacer.racerTransform != null)
                aiCarController = aiRacer.racerTransform.GetComponent<RCCP_CarController>();

            if (aiCarController != null)
                aiCarController.canControl = state;
        }
    }

    private void SpawnAutomaticOpponents()
    {
        ClearSpawnedOpponents();

        if (opponentCount <= 0)
        {
            aiRacers = Array.Empty<RaceRacer>();
            return;
        }

        bool previousRegisterLastVehicleAsPlayer = false;
        bool hasSceneManager = RCCP_SceneManager.Instance != null;

        if (hasSceneManager)
        {
            previousRegisterLastVehicleAsPlayer = RCCP_SceneManager.Instance.registerLastVehicleAsPlayer;
            RCCP_SceneManager.Instance.registerLastVehicleAsPlayer = false;
        }

        aiRacers = new RaceRacer[opponentCount];

        for (int i = 0; i < opponentCount; i++)
        {
            Transform spawnTransform = GetOpponentSpawnTransform(i);
            GameObject opponentObject = SpawnOpponentVehicle(i, spawnTransform);

            if (opponentObject == null)
                continue;

            spawnedOpponentObjects.Add(opponentObject);
            RCCP_RacingOpponentAI oldRacingAI = opponentObject.GetComponent<RCCP_RacingOpponentAI>();
            if (oldRacingAI != null)
            {
                oldRacingAI.enabled = false;
                Destroy(oldRacingAI);
            }

            EnsureOpponentNavMeshAgent(opponentObject);
            RCCP_AI aiDriver = opponentObject.GetComponent<RCCP_AI>();

            if (aiDriver == null)
                aiDriver = opponentObject.AddComponent<RCCP_AI>();

            ConfigureOpponentMainAI(aiDriver);

            aiRacers[i] = new RaceRacer
            {
                displayName = GetOpponentDisplayName(i),
                racerTransform = opponentObject.transform,
                aiDriver = aiDriver,
                currentWaypointIndex = 0,
                completedLaps = 0,
                finished = false,
                finishPosition = 0,
                finishTime = 0f,
                currentProgressPathIndex = -1,
                sharedRankingProgress = 0f,
                currentCheckpointIndex = -1,
                nextCheckpointIndex = 0,
                checkpointProgressInitialized = false
            };
        }

        if (hasSceneManager)
            RCCP_SceneManager.Instance.registerLastVehicleAsPlayer = previousRegisterLastVehicleAsPlayer;

        if (CarController != null)
        {
            CarSO selectedCar = GetCurrentCarSO();
            PrepareGameplayCameraProfileBeforeRegister(selectedCar);
            RCCP.RegisterPlayerVehicle(CarController, true, true);
            ApplyGameplayCameraProfile(selectedCar);
            StartCoroutine(ApplyGameplayCameraProfileNextFrame(selectedCar));
        }

        StartCoroutine(RefreshOpponentsAfterSpawn());
    }

    private IEnumerator RefreshOpponentsAfterSpawn()
    {
        yield return null;
        yield return new WaitForFixedUpdate();

        RefreshOpponentDrivers(raceStarted);
    }

    private float GetOpponentRacingLineOffset(int opponentIndex)
    {
        float[] offsets = { -.3f, .3f, 0f, -.15f, .15f };
        return offsets[Mathf.Abs(opponentIndex) % offsets.Length];
    }

    private string GetOpponentDisplayName(int opponentIndex)
    {
        if (opponentDisplayNames == null || opponentDisplayNames.Length == 0)
            return $"Racer {opponentIndex + 1}";

        int safeIndex = Mathf.Abs(opponentIndex) % opponentDisplayNames.Length;
        string displayName = opponentDisplayNames[safeIndex];

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = $"Racer {opponentIndex + 1}";

        if (opponentIndex >= opponentDisplayNames.Length)
            displayName = $"{displayName} {opponentIndex + 1}";

        return displayName;
    }

    private bool IsGeneratedOpponentName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return true;

        string trimmedName = displayName.Trim();
        return trimmedName.StartsWith("AI ", StringComparison.OrdinalIgnoreCase)
            || trimmedName.StartsWith("Racer ", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshOpponentDrivers(bool canControl)
    {
        if (aiRacers == null)
            return;

        for (int i = 0; i < aiRacers.Length; i++)
        {
            RaceRacer aiRacer = aiRacers[i];

            if (aiRacer == null || aiRacer.racerTransform == null)
                continue;

            RCCP_AI aiDriver = aiRacer.aiDriver;
            EnsureOpponentNavMeshAgent(aiRacer.racerTransform.gameObject);

            if (aiDriver == null)
                aiDriver = aiRacer.racerTransform.GetComponent<RCCP_AI>();

            if (aiDriver == null)
                aiDriver = aiRacer.racerTransform.gameObject.AddComponent<RCCP_AI>();

            aiRacer.aiDriver = aiDriver;
            ConfigureOpponentMainAI(aiDriver);
            aiDriver.enabled = canControl;
            aiDriver.Reload();

            RCCP_CarController aiCarController = aiDriver.CarController;

            if (aiCarController == null)
                continue;

            aiCarController.externalControl = true;
            aiCarController.SetCanControl(canControl);
            aiCarController.SetEngine(true);
            ConfigureOpponentDamage(aiCarController);

            if (aiCarController.Rigid != null)
                aiCarController.Rigid.WakeUp();
        }
    }

    private void ConfigureOpponentMainAI(RCCP_AI aiDriver)
    {
        if (aiDriver == null)
            return;

        aiDriver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints;
        aiDriver.waypointsContainer = runtimeRaceWaypoints;
        aiDriver.target = null;
        aiDriver.rubberBandTarget = CarController != null ? CarController.transform : null;
        aiDriver.useRubberBanding = true;
        aiDriver.behindPlayerBrakeMultiplier = 1.2f;
        aiDriver.rubberBandMinimumCornerSpeed = 40f;
    }

    private NavMeshAgent EnsureOpponentNavMeshAgent(GameObject opponentObject)
    {
        if (opponentObject == null)
            return null;

        NavMeshAgent agent = opponentObject.GetComponentInChildren<NavMeshAgent>(true);

        if (agent == null)
        {
            GameObject agentObject = new GameObject("NavMeshAgent");
            agentObject.transform.SetParent(opponentObject.transform, false);
            agent = agentObject.AddComponent<NavMeshAgent>();
        }

        agent.transform.localPosition = Vector3.zero;
        agent.transform.localRotation = Quaternion.identity;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.radius = 1.2f;
        agent.height = 3f;
        agent.speed = 60f;
        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.enabled = true;

        return agent;
    }

    private void ConfigureOpponentDamage(RCCP_CarController aiCarController)
    {
        if (aiCarController == null || aiCarController.Damage == null)
            return;

        aiCarController.Damage.wheelDetachment = false;
        aiCarController.Damage.partDamage = false;
        aiCarController.Damage.deformationMultiplier = Mathf.Min(aiCarController.Damage.deformationMultiplier, 0.35f);
        aiCarController.Damage.wheelDamageMultiplier = Mathf.Min(aiCarController.Damage.wheelDamageMultiplier, 0.25f);
    }

    private ArcadeVP.WaypointCircuit GetRaceWaypointCircuit()
    {
        EnsureRaceWaypointSources();

        if (runtimeRaceWaypointCircuit != null)
        {
            runtimeRaceWaypointCircuit.RebuildRoute();
            return runtimeRaceWaypointCircuit;
        }

        if (runtimeRaceWaypoints == null || runtimeRaceWaypoints.waypoints == null || runtimeRaceWaypoints.waypoints.Count < 2)
            return null;

        GameObject circuitObject = new GameObject("Runtime_RaceWaypointCircuit");
        circuitObject.transform.SetParent(transform);
        runtimeRaceWaypointCircuit = circuitObject.AddComponent<ArcadeVP.WaypointCircuit>();
        runtimeRaceWaypointCircuit.waypointList.items = new Transform[runtimeRaceWaypoints.waypoints.Count];

        for (int i = 0; i < runtimeRaceWaypoints.waypoints.Count; i++)
        {
            RCCP_Waypoint waypoint = runtimeRaceWaypoints.waypoints[i];

            if (waypoint == null)
                continue;

            GameObject waypointObject = new GameObject($"CircuitWaypoint_{i:000}");
            waypointObject.transform.SetParent(circuitObject.transform);
            waypointObject.transform.SetPositionAndRotation(waypoint.transform.position, waypoint.transform.rotation);
            runtimeRaceWaypointCircuit.waypointList.items[i] = waypointObject.transform;
        }

        runtimeRaceWaypointCircuit.RebuildRoute();
        return runtimeRaceWaypointCircuit;
    }

    private ArcadeVP.WaypointCircuit GetOpponentWaypointCircuit(int opponentIndex)
    {
        // Every opponent must follow the same route used by race progress and
        // checkpoints. Selecting a circuit by opponent index made steering and
        // progress correction fight each other when a scene contained two paths.
        return GetRaceWaypointCircuit();
    }

    private void EnsureRaceWaypointSources()
    {
        if (runtimeRaceWaypoints != null)
            return;

        resolvedWaypointSystems.Clear();
        resolvedWaypointSystems.AddRange(GetResolvedWaypointSystems());

        if (resolvedWaypointSystems.Count == 0)
            return;

        List<Pose> progressRoute = BuildProgressRoutePoses();

        if (progressRoute.Count < 2)
            return;

        GameObject runtimeContainerObject = new GameObject("Runtime_RaceWaypoints");
        runtimeContainerObject.transform.SetParent(transform);
        runtimeRaceWaypoints = runtimeContainerObject.AddComponent<RCCP_AIWaypointsContainer>();

        for (int i = 0; i < progressRoute.Count; i++)
        {
            Pose sourceWaypoint = progressRoute[i];

            GameObject waypointObject = new GameObject($"RCCP_Waypoint_{i:000}");
            waypointObject.transform.SetParent(runtimeContainerObject.transform);
            waypointObject.transform.SetPositionAndRotation(sourceWaypoint.position, sourceWaypoint.rotation);
            waypointObject.AddComponent<RCCP_Waypoint>();
        }

        runtimeRaceWaypoints.GetAllWaypoints();

    }

    private List<Pose> BuildProgressRoutePoses()
    {
        List<Pose> checkpointRoute = BuildCheckpointRoutePoses();
        if (checkpointRoute.Count >= 2)
            return checkpointRoute;

        List<Pose> result = new List<Pose>();

        if (resolvedWaypointSystems.Count == 0)
            return result;

        int sampleCount = resolvedWaypointSystems
            .Where(system => system != null && system.waypoints != null)
            .Select(system => Mathf.Max(0, system.waypoints.Count))
            .DefaultIfEmpty(0)
            .Max();

        sampleCount = Mathf.Max(sampleCount * 2, 64);

        if (sampleCount < 2)
            return result;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            Vector3 positionSum = Vector3.zero;
            Vector3 forwardSum = Vector3.zero;
            int validCount = 0;
            float normalizedDistance = sampleIndex / (float)sampleCount;

            for (int systemIndex = 0; systemIndex < resolvedWaypointSystems.Count; systemIndex++)
            {
                Waypoint_System system = resolvedWaypointSystems[systemIndex];

                if (system == null || system.waypoints == null || system.waypoints.Count < 2)
                    continue;

                Pose sampledPose = SampleWaypointSystemPose(system.waypoints, normalizedDistance);
                positionSum += sampledPose.position;
                forwardSum += sampledPose.forward;
                validCount++;
            }

            if (validCount == 0)
                continue;

            Vector3 position = positionSum / validCount;
            Vector3 forward = forwardSum.sqrMagnitude > 0.001f ? forwardSum.normalized : Vector3.forward;
            result.Add(new Pose(position, Quaternion.LookRotation(forward, Vector3.up)));
        }

        return result;
    }

    private List<Pose> BuildCheckpointRoutePoses()
    {
        List<Pose> result = new List<Pose>();
        Checkpoint_Manager checkpointManager = ResolveCheckpointManager();

        if (checkpointManager == null || checkpointManager.checkpoints == null || checkpointManager.checkpoints.Count < 2)
            return result;

        for (int i = 0; i < checkpointManager.checkpoints.Count; i++)
        {
            Transform checkpoint = checkpointManager.checkpoints[i];

            if (checkpoint == null)
                continue;

            Vector3 forward = checkpoint.forward.sqrMagnitude > 0.001f ? checkpoint.forward : Vector3.forward;
            result.Add(new Pose(checkpoint.position, Quaternion.LookRotation(forward, Vector3.up)));
        }

        return result;
    }

    private Checkpoint_Manager ResolveCheckpointManager()
    {
        if (externalCheckpointManager != null)
            return externalCheckpointManager;

        externalCheckpointManager = FindFirstObjectByType<Checkpoint_Manager>(FindObjectsInactive.Include);
        return externalCheckpointManager;
    }

    private Pose SampleWaypointSystemPose(List<Transform> waypoints, float normalizedDistance)
    {
        if (waypoints == null || waypoints.Count == 0)
            return new Pose(transform.position, transform.rotation);

        if (waypoints.Count == 1 || waypoints[0] == null)
            return new Pose(waypoints[0] != null ? waypoints[0].position : transform.position, waypoints[0] != null ? waypoints[0].rotation : transform.rotation);

        float totalLength = 0f;
        int count = waypoints.Count;

        for (int i = 0; i < count; i++)
        {
            Transform from = waypoints[i];
            Transform to = waypoints[(i + 1) % count];

            if (from == null || to == null)
                continue;

            totalLength += Vector3.Distance(from.position, to.position);
        }

        if (totalLength <= 0.01f)
            return new Pose(waypoints[0].position, waypoints[0].rotation);

        float targetDistance = Mathf.Repeat(normalizedDistance, 1f) * totalLength;
        float accumulated = 0f;

        for (int i = 0; i < count; i++)
        {
            Transform from = waypoints[i];
            Transform to = waypoints[(i + 1) % count];

            if (from == null || to == null)
                continue;

            float segmentLength = Vector3.Distance(from.position, to.position);

            if (segmentLength <= 0.001f)
                continue;

            if (accumulated + segmentLength >= targetDistance)
            {
                float t = Mathf.InverseLerp(accumulated, accumulated + segmentLength, targetDistance);
                Vector3 position = Vector3.Lerp(from.position, to.position, t);
                Vector3 forward = (to.position - from.position).normalized;
                return new Pose(position, Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : from.forward, Vector3.up));
            }

            accumulated += segmentLength;
        }

        Transform last = waypoints[count - 1];
        Transform first = waypoints[0];
        Vector3 fallbackForward = first != null && last != null ? (first.position - last.position).normalized : Vector3.forward;
        return new Pose(last != null ? last.position : transform.position, Quaternion.LookRotation(fallbackForward.sqrMagnitude > 0.001f ? fallbackForward : Vector3.forward, Vector3.up));
    }

    private List<Waypoint_System> GetResolvedWaypointSystems()
    {
        List<Waypoint_System> result = new List<Waypoint_System>();
        Waypoint_System selectedSystem = externalWaypointSystem;

        if (!IsValidRaceWaypointSystem(selectedSystem) && externalWaypointRoot != null)
            selectedSystem = externalWaypointRoot
                .GetComponentsInChildren<Waypoint_System>(true)
                .Where(IsValidRaceWaypointSystem)
                .OrderBy(system => GetHierarchyOrderKey(system.transform))
                .FirstOrDefault();

        if (!IsValidRaceWaypointSystem(selectedSystem))
            selectedSystem = FindObjectsByType<Waypoint_System>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(IsValidRaceWaypointSystem)
                .OrderBy(system => GetHierarchyOrderKey(system.transform))
                .FirstOrDefault();

        if (selectedSystem != null)
            result.Add(selectedSystem);

        return result;
    }

    private bool IsValidRaceWaypointSystem(Waypoint_System system)
    {
        return system != null
            && system.waypoints != null
            && system.waypoints.Count(waypoint => waypoint != null) >= 2;
    }

    private string GetHierarchyOrderKey(Transform target)
    {
        if (target == null)
            return string.Empty;

        Stack<int> indices = new Stack<int>();
        Transform current = target;

        while (current != null)
        {
            indices.Push(current.GetSiblingIndex());
            current = current.parent;
        }

        return string.Join(".", indices.Select(index => index.ToString("D4")));
    }

    private GameObject SpawnOpponentVehicle(int opponentIndex, Transform spawnTransform)
    {
        if (spawnTransform == null)
            return null;

        GameObject prefabToSpawn = GetOpponentPrefab(opponentIndex);

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"Opponent {opponentIndex + 1} was not spawned because its prefab could not be loaded.");
            return null;
        }

        return Instantiate(prefabToSpawn, spawnTransform.position, spawnTransform.rotation);
    }

    private GameObject GetOpponentPrefab(int opponentIndex)
    {
        if (GlobalCarData._carlists == null || GlobalCarData._carlists.Count == 0)
            return null;

        int currentCarIndex = SaveManager.Instance.saveData.currentCar;
        int chosenCarIndex = currentCarIndex;

        if (!usePlayerCarForOpponents)
            chosenCarIndex = (currentCarIndex + opponentIndex + 1) % GlobalCarData._carlists.Count;

        string prefabLocation = GlobalCarData._carlists[chosenCarIndex].carPrefabLocation;
        GameObject prefab = Resources.Load<GameObject>(prefabLocation);

        if (prefab == null)
            Debug.LogWarning($"Opponent car prefab not found in Resources. Car index: {chosenCarIndex}, path: {prefabLocation}");

        return prefab;
    }

    private Transform GetOpponentSpawnTransform(int opponentIndex)
    {
        if (opponentSpawnPoints != null && opponentIndex < opponentSpawnPoints.Length && opponentSpawnPoints[opponentIndex] != null)
            return opponentSpawnPoints[opponentIndex];

        if (SpawnPoint == null)
            return null;

        int row = spawnCarsPerRow > 0 ? (opponentIndex / spawnCarsPerRow) + 1 : opponentIndex + 1;
        int column = spawnCarsPerRow > 0 ? opponentIndex % spawnCarsPerRow : 0;

        float horizontalOffset = 0f;
        if (spawnCarsPerRow > 1)
            horizontalOffset = (column - (spawnCarsPerRow - 1) * .5f) * spawnColumnSpacing;

        Vector3 spawnPosition =
            SpawnPoint.position
            - SpawnPoint.forward * (row * spawnRowSpacing)
            + SpawnPoint.right * horizontalOffset;

        GameObject runtimeSpawnPoint = new GameObject($"OpponentSpawn_{opponentIndex + 1}");
        runtimeSpawnPoint.transform.SetPositionAndRotation(spawnPosition, SpawnPoint.rotation);
        runtimeSpawnPoint.transform.SetParent(transform);

        return runtimeSpawnPoint.transform;
    }

    private void ClearSpawnedOpponents()
    {
        for (int i = 0; i < spawnedOpponentObjects.Count; i++)
        {
            if (spawnedOpponentObjects[i] != null)
                Destroy(spawnedOpponentObjects[i]);
        }

        spawnedOpponentObjects.Clear();

        if (runtimeRaceWaypointCircuit != null)
        {
            Destroy(runtimeRaceWaypointCircuit.gameObject);
            runtimeRaceWaypointCircuit = null;
        }

        if (runtimeRaceWaypoints != null)
        {
            Destroy(runtimeRaceWaypoints.gameObject);
            runtimeRaceWaypoints = null;
        }

        resolvedWaypointSystems.Clear();
    }

   private void Update() {
        MaintainPlayerControlForNonRaceMode();

        switch (RaceType)
        {
           case RaceType.DriftScore:
           case RaceType.TargetDrift:
           case RaceType.ComboMaster:
               UpdateDriftMode();
               break;

           case RaceType.FreeDrift:
               UpdateDriftMode();
               break;

           case RaceType.Racing:
           case RaceType.Elimination:
           case RaceType.NoBrakeChallenge:
               UpdateRaceMode();
               break;
       }

    }

    private void MaintainPlayerControlForNonRaceMode()
    {
        if (IsRaceMode() || missionResultsShown || CarController == null)
            return;

        ResetRCCPInputForGameplay();

        if (CarController.externalControl)
            CarController.externalControl = false;

        if (!CarController.canControl)
            CarController.SetCanControl(true);

        if (CarController.Inputs != null)
            CarController.Inputs.DisableOverrideInputs();

        if (CarController.Rigid != null)
        {
            if (CarController.Rigid.isKinematic)
                CarController.Rigid.isKinematic = false;

            if (CarController.Rigid.IsSleeping())
                CarController.Rigid.WakeUp();
        }

        if (CarController.Gearbox == null)
            return;

        if (CarController.Gearbox.forceToNGear)
            CarController.Gearbox.forceToNGear = false;

        if (CarController.Gearbox.forceToRGear)
            CarController.Gearbox.forceToRGear = false;

        if (CarController.Gearbox.automaticGearSelector != RCCP_Gearbox.SemiAutomaticDNRPGear.D)
            CarController.Gearbox.automaticGearSelector = RCCP_Gearbox.SemiAutomaticDNRPGear.D;

        if (CarController.Gearbox.currentGearState == null)
            CarController.Gearbox.currentGearState = new RCCP_Gearbox.CurrentGearState();

        if (CarController.Gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.Neutral ||
            CarController.Gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.Park)
        {
            CarController.Gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
        }
    }

   private void UpdateDriftMode()
   {
       if (!missionResultsShown)
           driftElapsedTime += Time.deltaTime;

       UpdateDriftUI();
       UpdateDrivingHaptics();

       if (!IsDriftScoringMode())
           return;

       UpdateTargetDriftTimer();

       // If can control of the vehicle is disabled, return.
       if (!CarController.canControl) {

           driftingNow = false;
           totalDriftTime = 0f;
           currentDriftComboTime = 0f;
           currentDriftPoints = 0;
           currentDriftCoins = 0;
           currentMP = 1f;
           driftInterruptedByCollision = false;
           driftInterruptTimer = 0f;
           lastDisplayedComboMultiplier = 0f;

           return;

       }
       if (driftInterruptedByCollision)
       {
           driftInterruptTimer -= Time.deltaTime;

           if (driftInterruptTimer <= 0f)
           {
               driftInterruptTimer = 0f;
               driftInterruptedByCollision = false;
           }
       }
       bool slipDrift = Mathf.Abs(CarController.RearAxle.leftWheelCollider.SidewaysSlip) >= .35f;

       if (driftInterruptedByCollision)
           driftingNow = false;
       else
           driftingNow = slipDrift;

       if (Mathf.Abs(CarController.RearAxle.leftWheelCollider.SidewaysSlip) >= .35f)
           driftingNow = true;
       else
           driftingNow = false;

       float distance = Vector3.Distance(lastPosition, transform.position);

       //  If canScore is enabled and drifting with above speed limit, calculate the score.
       if (canScore && (CarController.Rigid.linearVelocity.magnitude * 3.6f) >= driftSpeed && driftingNow) {

           //  Increasing total drifting time.
           totalDriftTime += Time.deltaTime;
           currentDriftComboTime += Time.deltaTime;

           //  If drifting time is high enough, increase the score.
           if (totalDriftTime >= driftTime) {
               currentMP = GetCurrentDriftComboMultiplier();
               if (scoreText != null)
               {
                    scoreText.text = currentDriftPoints.ToString("N1");
               }
               currentDriftPoints += (driftPointsMP * currentMP) * Time.deltaTime;
               currentDriftCoins += (driftCoinsMP / currentMP) * Time.deltaTime;
               if (scoreText != null && !scoreText.gameObject.activeSelf && currentDriftCoins > 1)
               {
                   scoreText.gameObject.SetActive(true);
               }
           }

           totalDriftDistance += distance;

       } else {

           totalDriftTime -= Time.deltaTime;
           currentDriftComboTime = 0f;

       }
       if (DriftTimeSlider != null)
       {
           DriftTimeSlider.value = totalDriftTime;
       }
       //  Clamping the drifting time.
       totalDriftTime = Mathf.Clamp(totalDriftTime, 0f, driftTime + 1.5f);

       //  If current drifting is over, add current score to the total score.
       if (currentDriftPoints > 0 && totalDriftTime < driftTime) {

           if (RaceType == RaceType.DriftScore)
           {
               totalDriftPoints += currentDriftPoints;
               totalDriftCoins += currentDriftCoins;
           }
           else if (RaceType == RaceType.ComboMaster)
           {
               if (raceStateText != null && !driftModeFinished)
                   raceStateText.text = "Combo Lost";
           }

           if (scoreText != null && scoreText.gameObject.activeSelf)
           {
               scoreText.gameObject.SetActive(false);
           }
           // if (OnDriftScoreAchieved != null)
               // OnDriftScoreAchieved(this);
           if (TotalScoreText != null)
               TotalScoreText.text = totalDriftPoints.ToString("N1");
           currentDriftPoints = 0;
           currentDriftCoins = 0;
           currentMP = 1f;
           currentDriftComboTime = 0f;
           lastDisplayedComboMultiplier = 0f;

       }
       lastPosition = transform.position;

       CheckDriftTargets();
   }

   private void UpdateRaceMode()
   {
       if (CarController == null || runtimeRaceWaypoints == null || runtimeRaceWaypoints.waypoints == null || runtimeRaceWaypoints.waypoints.Count == 0 || GetRaceWaypointCircuit() == null)
           return;

       if (!raceStarted)
       {
           UpdateCheckpointVisuals();
           return;
       }

       if (!missionResultsShown)
           raceElapsedTime += Time.deltaTime;

       ApplyPlayerBrakeRestrictions();
       UpdateDrivingHaptics();
       UpdatePlayerRaceProgress();
       UpdatePlayerRouteWarnings();
       UpdateAIRaceProgress();
       UpdateEliminationMode();
       UpdateCheckpointVisuals();
       UpdateRaceUI();
   }

   private void UpdateDrivingHaptics()
   {
       if (CarController == null || CarController.Rigid == null)
           return;

       float low = 0f;
       float high = 0f;
       float speedKmh = CarController.Rigid.linearVelocity.magnitude * 3.6f;

       if (CarController.currentGear != lastHapticGear)
       {
           if (lastHapticGear != int.MinValue)
               GameHaptics.GearShift();
           lastHapticGear = CarController.currentGear;
       }

       if (driftingNow) { low = .18f; high = .3f; }
       bool nitroActive = CarController.nosInput_P > .1f;
       if (nitroActive && !nitroHapticsActive)
           GameHaptics.Pulse(.45f, .75f, .22f);
       nitroHapticsActive = nitroActive;
       if (nitroActive) { low = Mathf.Max(low, .3f); high = Mathf.Max(high, .48f); }
       if ((CarController.brakeInput_P > .75f || CarController.handbrakeInput_P > .5f) && speedKmh > 25f)
       { low = Mathf.Max(low, .24f); high = Mathf.Max(high, .36f); }

       if (Physics.Raycast(CarController.transform.position + Vector3.up, Vector3.down,
               out RaycastHit hit, 3f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
       {
           string surface = (hit.collider.name + " " +
                             (hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.name : string.Empty)).ToLowerInvariant();
           if (hit.collider is TerrainCollider || surface.Contains("gravel") || surface.Contains("dirt") ||
               surface.Contains("grass") || surface.Contains("offroad"))
           {
               float strength = Mathf.InverseLerp(10f, 100f, speedKmh);
               low = Mathf.Max(low, .12f * strength);
               high = Mathf.Max(high, .25f * strength);
           }
       }

       if (low > 0f || high > 0f)
           GameHaptics.Continuous(low, high);
   }

    private void UpdatePlayerRaceProgress()
    {
        if (playerRacer.eliminated)
            return;

       playerRacer.racerTransform = CarController.transform;

        if (playerRacer.finished)
            return;

       UpdateRacerRaceProgress(playerRacer);

       if ((RaceType == RaceType.Racing || RaceType == RaceType.NoBrakeChallenge) && playerRacer.completedLaps >= totalRaceLaps)
       {
           MarkRacerFinished(playerRacer);
           int finalPosition = GetPlayerRacePosition();
           CompleteRaceMission(finalPosition == 1, finalPosition == 1 ? "Winner" : $"Finished {finalPosition}/{GetTotalRaceParticipantCount()}");
       }
    }

    private void UpdateAIRaceProgress()
    {
        if (aiRacers == null)
            return;

       for (int i = 0; i < aiRacers.Length; i++)
       {
           RaceRacer aiRacer = aiRacers[i];

           if (aiRacer == null || aiRacer.racerTransform == null || aiRacer.finished || aiRacer.eliminated)
               continue;

           UpdateRacerRaceProgress(aiRacer);

           if ((RaceType == RaceType.Racing || RaceType == RaceType.NoBrakeChallenge) && aiRacer.completedLaps >= totalRaceLaps)
               MarkRacerFinished(aiRacer);
       }
   }

   private void UpdateRacerRaceProgress(RaceRacer racer)
   {
       if (racer == null || racer.racerTransform == null)
           return;

       if (resolvedWaypointSystems.Count == 0)
           resolvedWaypointSystems.AddRange(GetResolvedWaypointSystems());

       bool useCheckpointProgress = HasCheckpointProgressSource();

       if (useCheckpointProgress)
       {
           UpdateRacerCheckpointProgress(racer);
           return;
       }

       if (TryFindBestResolvedPathProgress(racer, out PathProgressInfo pathProgress))
       {
           UpdateRacerRaceProgressOnResolvedPaths(racer, pathProgress);
           return;
       }

       if (runtimeRaceWaypoints == null || runtimeRaceWaypoints.waypoints == null || runtimeRaceWaypoints.waypoints.Count == 0)
           return;

       int waypointCount = runtimeRaceWaypoints.waypoints.Count;
       if (waypointCount < 2)
           return;

       float previousProgress = Mathf.Repeat(racer.currentCircuitDistance, waypointCount);
       float currentProgress = FindClosestWaypointProgress(racer);
       float lapWrapThreshold = Mathf.Clamp(waypointCount * .08f, 0.75f, 3f);

       if (!racer.progressInitialized)
       {
           racer.progressInitialized = true;
           racer.currentCircuitDistance = currentProgress;
           racer.startCircuitDistance = currentProgress;
           racer.lapCountingArmed = false;
           racer.currentSegmentIndex = Mathf.FloorToInt(currentProgress) % waypointCount;
           racer.currentWaypointIndex = (racer.currentSegmentIndex + 1) % waypointCount;
           racer.raceProgress = (racer.completedLaps * waypointCount) + currentProgress;

           if (racer.currentWaypointIndex >= 0 && racer.currentWaypointIndex < runtimeRaceWaypoints.waypoints.Count)
           {
               RCCP_Waypoint initialNextWaypoint = runtimeRaceWaypoints.waypoints[racer.currentWaypointIndex];

               if (initialNextWaypoint != null)
                   racer.distanceToNextWaypoint = Vector3.Distance(racer.racerTransform.position, initialNextWaypoint.transform.position);
           }

           return;
       }

       float armDistance = Mathf.Clamp(waypointCount * .25f, 3f, waypointCount * .5f);
       float distanceFromStart = GetForwardLoopDistance(racer.startCircuitDistance, currentProgress, waypointCount);

       if (!racer.lapCountingArmed && distanceFromStart >= armDistance)
           racer.lapCountingArmed = true;

       if (racer.lapCountingArmed && previousProgress > waypointCount - lapWrapThreshold && currentProgress < lapWrapThreshold)
           racer.completedLaps++;
       else if (racer.completedLaps > 0 && previousProgress < lapWrapThreshold && currentProgress > waypointCount - lapWrapThreshold)
           racer.completedLaps--;

       racer.currentCircuitDistance = currentProgress;
       racer.currentSegmentIndex = Mathf.FloorToInt(currentProgress) % waypointCount;
       racer.currentWaypointIndex = (racer.currentSegmentIndex + 1) % waypointCount;

       if (racer.currentWaypointIndex >= 0 && racer.currentWaypointIndex < runtimeRaceWaypoints.waypoints.Count)
       {
           RCCP_Waypoint nextWaypoint = runtimeRaceWaypoints.waypoints[racer.currentWaypointIndex];

           if (nextWaypoint != null)
               racer.distanceToNextWaypoint = Vector3.Distance(racer.racerTransform.position, nextWaypoint.transform.position);
       }

       racer.raceProgress = (racer.completedLaps * waypointCount) + currentProgress;
   }

   private void UpdateRacerCheckpointProgress(RaceRacer racer)
   {
       Checkpoint_Manager checkpointManager = ResolveCheckpointManager();

       if (checkpointManager == null || checkpointManager.checkpoints == null || checkpointManager.checkpoints.Count < 2)
           return;

       List<Transform> checkpoints = checkpointManager.checkpoints;
       int checkpointCount = checkpoints.Count;

       if (!racer.checkpointProgressInitialized)
       {
           racer.checkpointProgressInitialized = true;
           racer.currentCheckpointIndex = -1;
           racer.nextCheckpointIndex = 0;
           racer.completedLaps = 0;
           racer.lapCountingArmed = false;
       }

       for (int guard = 0; guard < checkpointCount; guard++)
       {
           Transform nextCheckpoint = checkpoints[racer.nextCheckpointIndex];

           if (nextCheckpoint == null || !IsInsideCheckpoint(nextCheckpoint, racer.racerTransform.position))
               break;

           racer.currentCheckpointIndex = racer.nextCheckpointIndex;
           racer.nextCheckpointIndex = (racer.currentCheckpointIndex + 1) % checkpointCount;

           if (racer.currentCheckpointIndex == checkpointCount / 2)
               racer.lapCountingArmed = true;

           if (racer.currentCheckpointIndex == 0)
           {
               if (racer.lapCountingArmed)
                   racer.completedLaps++;

               racer.lapCountingArmed = false;
           }
       }

       Transform targetCheckpoint = checkpoints[racer.nextCheckpointIndex];
       racer.distanceToNextWaypoint = targetCheckpoint != null
           ? Vector3.Distance(racer.racerTransform.position, targetCheckpoint.position)
           : 0f;
       racer.currentWaypointIndex = racer.nextCheckpointIndex;
       racer.sharedRankingProgress = racer.currentCheckpointIndex >= 0
           ? racer.currentCheckpointIndex + (100000f - Mathf.Min(racer.distanceToNextWaypoint, 100000f)) / 100000f
           : -1f;
       racer.raceProgress = racer.completedLaps + Mathf.Max(0f, racer.sharedRankingProgress / checkpointCount);
   }

   private bool IsInsideCheckpoint(Transform checkpoint, Vector3 worldPosition)
   {
       if (checkpoint == null)
           return false;

       Collider checkpointCollider = checkpoint.GetComponent<Collider>();

       if (checkpointCollider == null)
           return Vector3.Distance(checkpoint.position, worldPosition) <= waypointReachDistance;

       if (checkpointCollider is BoxCollider boxCollider)
       {
           Vector3 localPoint = checkpoint.InverseTransformPoint(worldPosition) - boxCollider.center;
           Vector3 halfSize = boxCollider.size * .5f;
           return Mathf.Abs(localPoint.x) <= halfSize.x
               && Mathf.Abs(localPoint.y) <= halfSize.y
               && Mathf.Abs(localPoint.z) <= halfSize.z;
       }

       Vector3 closestPoint = checkpointCollider.ClosestPoint(worldPosition);
       return (closestPoint - worldPosition).sqrMagnitude <= 0.01f;
   }

   private bool HasCheckpointProgressSource()
   {
       Checkpoint_Manager checkpointManager = ResolveCheckpointManager();

       return checkpointManager != null
           && checkpointManager.checkpoints != null
           && checkpointManager.checkpoints.Count >= 2;
   }

   private void UpdateRacerRaceProgressOnResolvedPaths(RaceRacer racer, PathProgressInfo pathProgress)
   {
       float previousProgress = Mathf.Repeat(racer.currentCircuitDistance, 1f);
       float currentProgress = Mathf.Repeat(pathProgress.normalizedProgress, 1f);
       const float lapWrapThreshold = 0.08f;

       int runtimeWaypointCount = runtimeRaceWaypoints != null && runtimeRaceWaypoints.waypoints != null
           ? runtimeRaceWaypoints.waypoints.Count
           : 0;

       if (!racer.progressInitialized)
       {
           racer.progressInitialized = true;
           racer.currentCircuitDistance = currentProgress;
           racer.startCircuitDistance = currentProgress;
           racer.lapCountingArmed = false;
           racer.currentProgressPathIndex = pathProgress.pathIndex;
           racer.currentSegmentIndex = runtimeWaypointCount > 0 ? Mathf.FloorToInt(currentProgress * runtimeWaypointCount) % runtimeWaypointCount : 0;
           racer.sharedRankingProgress = FindClosestWaypointProgress(racer);
           if (runtimeWaypointCount > 0)
           {
               int sharedSegmentIndex = Mathf.FloorToInt(racer.sharedRankingProgress) % runtimeWaypointCount;
               racer.currentWaypointIndex = (sharedSegmentIndex + 1) % runtimeWaypointCount;
           }
           else
           {
               racer.currentWaypointIndex = 0;
           }

           if (racer.currentWaypointIndex >= 0 && racer.currentWaypointIndex < runtimeRaceWaypoints.waypoints.Count)
           {
               RCCP_Waypoint nextWaypoint = runtimeRaceWaypoints.waypoints[racer.currentWaypointIndex];

               if (nextWaypoint != null)
                   racer.distanceToNextWaypoint = Vector3.Distance(racer.racerTransform.position, nextWaypoint.transform.position);
           }
           else
           {
               racer.distanceToNextWaypoint = pathProgress.distanceToNextWaypoint;
           }

           racer.raceProgress = racer.completedLaps + currentProgress;
           return;
       }

       float distanceFromStart = GetForwardLoopDistance(racer.startCircuitDistance, currentProgress, 1f);

       if (!racer.lapCountingArmed && distanceFromStart >= 0.25f)
           racer.lapCountingArmed = true;

       if (racer.lapCountingArmed && previousProgress > 1f - lapWrapThreshold && currentProgress < lapWrapThreshold)
           racer.completedLaps++;
       else if (racer.completedLaps > 0 && previousProgress < lapWrapThreshold && currentProgress > 1f - lapWrapThreshold)
           racer.completedLaps--;

       racer.currentCircuitDistance = currentProgress;
       racer.currentProgressPathIndex = pathProgress.pathIndex;
       racer.currentSegmentIndex = runtimeWaypointCount > 0 ? Mathf.FloorToInt(currentProgress * runtimeWaypointCount) % runtimeWaypointCount : 0;
       racer.sharedRankingProgress = FindClosestWaypointProgress(racer);

       if (runtimeWaypointCount > 0)
       {
           int sharedSegmentIndex = Mathf.FloorToInt(racer.sharedRankingProgress) % runtimeWaypointCount;
           racer.currentWaypointIndex = (sharedSegmentIndex + 1) % runtimeWaypointCount;
       }
       else
       {
           racer.currentWaypointIndex = 0;
       }

       if (racer.currentWaypointIndex >= 0 && racer.currentWaypointIndex < runtimeRaceWaypoints.waypoints.Count)
       {
           RCCP_Waypoint nextWaypoint = runtimeRaceWaypoints.waypoints[racer.currentWaypointIndex];

           if (nextWaypoint != null)
               racer.distanceToNextWaypoint = Vector3.Distance(racer.racerTransform.position, nextWaypoint.transform.position);
       }
       else
       {
           racer.distanceToNextWaypoint = pathProgress.distanceToNextWaypoint;
       }

       racer.raceProgress = racer.completedLaps + currentProgress;
   }

   private bool TryFindBestResolvedPathProgress(RaceRacer racer, out PathProgressInfo bestProgress)
   {
       bestProgress = default;
       bestProgress.pathIndex = -1;
       bestProgress.sqrDistance = float.MaxValue;

       if (racer == null || racer.racerTransform == null || resolvedWaypointSystems.Count == 0)
           return false;

       float bestScore = float.MaxValue;

       for (int pathIndex = 0; pathIndex < resolvedWaypointSystems.Count; pathIndex++)
       {
           Waypoint_System system = resolvedWaypointSystems[pathIndex];

           if (system == null || system.waypoints == null || system.waypoints.Count < 2)
               continue;

           EvaluateResolvedPathProgress(
               racer.racerTransform.position,
               system.waypoints,
               pathIndex,
               racer.currentProgressPathIndex,
               ref bestProgress,
               ref bestScore);
       }

       return bestProgress.pathIndex >= 0;
   }

   private void EvaluateResolvedPathProgress(
       Vector3 worldPosition,
       List<Transform> waypoints,
       int pathIndex,
       int preferredPathIndex,
       ref PathProgressInfo bestProgress,
       ref float bestScore)
   {
       if (waypoints == null || waypoints.Count < 2)
           return;

       float totalLength = 0f;
       int waypointCount = waypoints.Count;

       for (int i = 0; i < waypointCount; i++)
       {
           Transform from = waypoints[i];
           Transform to = waypoints[(i + 1) % waypointCount];

           if (from == null || to == null)
               continue;

           totalLength += Vector3.Distance(from.position, to.position);
       }

       if (totalLength <= 0.001f)
           return;

       float accumulated = 0f;

       for (int segmentIndex = 0; segmentIndex < waypointCount; segmentIndex++)
       {
           Transform from = waypoints[segmentIndex];
           Transform to = waypoints[(segmentIndex + 1) % waypointCount];

           if (from == null || to == null)
               continue;

           Vector3 segment = to.position - from.position;
           float segmentLengthSqr = segment.sqrMagnitude;

           if (segmentLengthSqr <= 0.0001f)
           {
               accumulated += Mathf.Sqrt(segmentLengthSqr);
               continue;
           }

           float segmentLength = Mathf.Sqrt(segmentLengthSqr);
           float t = Mathf.Clamp01(Vector3.Dot(worldPosition - from.position, segment) / segmentLengthSqr);
           Vector3 closestPoint = from.position + segment * t;
           float sqrDistance = (worldPosition - closestPoint).sqrMagnitude;
           float score = sqrDistance;

           if (pathIndex == preferredPathIndex)
               score *= 0.92f;

           if (score < bestScore)
           {
               bestScore = score;
               bestProgress.pathIndex = pathIndex;
               bestProgress.normalizedProgress = Mathf.Repeat((accumulated + (segmentLength * t)) / totalLength, 1f);
               bestProgress.sqrDistance = sqrDistance;
               bestProgress.distanceToNextWaypoint = Vector3.Distance(worldPosition, to.position);
           }

           accumulated += segmentLength;
       }
   }

   private float FindClosestWaypointProgress(RaceRacer racer)
   {
       if (racer == null || racer.racerTransform == null || runtimeRaceWaypoints == null || runtimeRaceWaypoints.waypoints == null)
           return 0f;

       int waypointCount = runtimeRaceWaypoints.waypoints.Count;
       if (waypointCount < 2)
           return 0f;

       bool useLocalSearch = racer.progressInitialized;
       int localBackRange = 2;
       int localForwardRange = 6;
       float bestProgress = 0f;
       float bestSqrDistance = float.MaxValue;

       if (useLocalSearch)
       {
           for (int offset = -localBackRange; offset <= localForwardRange; offset++)
           {
               int segmentIndex = Mathf.FloorToInt(Mathf.Repeat(racer.currentSegmentIndex + offset, waypointCount));
               EvaluateWaypointSegmentProgress(racer.racerTransform.position, segmentIndex, ref bestProgress, ref bestSqrDistance);
           }

           if (bestSqrDistance < float.MaxValue)
               return bestProgress;
       }

       for (int segmentIndex = 0; segmentIndex < waypointCount; segmentIndex++)
           EvaluateWaypointSegmentProgress(racer.racerTransform.position, segmentIndex, ref bestProgress, ref bestSqrDistance);

       return bestProgress;
   }

   private void EvaluateWaypointSegmentProgress(Vector3 worldPosition, int segmentIndex, ref float bestProgress, ref float bestSqrDistance)
   {
       int waypointCount = runtimeRaceWaypoints.waypoints.Count;
       int nextIndex = (segmentIndex + 1) % waypointCount;
       RCCP_Waypoint fromWaypoint = runtimeRaceWaypoints.waypoints[segmentIndex];
       RCCP_Waypoint toWaypoint = runtimeRaceWaypoints.waypoints[nextIndex];

       if (fromWaypoint == null || toWaypoint == null)
           return;

       Vector3 from = fromWaypoint.transform.position;
       Vector3 to = toWaypoint.transform.position;
       Vector3 segment = to - from;
       float segmentLengthSqr = segment.sqrMagnitude;

       if (segmentLengthSqr <= 0.0001f)
           return;

       float t = Mathf.Clamp01(Vector3.Dot(worldPosition - from, segment) / segmentLengthSqr);
       Vector3 closestPoint = from + segment * t;
       float sqrDistance = (worldPosition - closestPoint).sqrMagnitude;

       if (sqrDistance >= bestSqrDistance)
           return;

       bestSqrDistance = sqrDistance;
       bestProgress = segmentIndex + t;
   }

   private float FindClosestDistanceAlongRaceRoute(Vector3 worldPosition, float? distanceHint = null)
   {
       ArcadeVP.WaypointCircuit raceCircuit = GetRaceWaypointCircuit();

       if (raceCircuit == null || raceCircuit.Length <= 0f)
           return 0f;

       float bestDistance = 0f;
       float bestSqrDistance = float.MaxValue;
       int samples = 180;

       if (distanceHint.HasValue)
       {
           float searchRange = Mathf.Clamp(waypointReachDistance * 3f, 30f, 90f);
           samples = 72;

           for (int i = 0; i <= samples; i++)
           {
               float t = i / (float)samples;
               float sampleDistance = Mathf.Repeat(distanceHint.Value - searchRange + (searchRange * 2f * t), raceCircuit.Length);
               Vector3 samplePosition = raceCircuit.GetRoutePosition(sampleDistance);
               float sqrDistance = (worldPosition - samplePosition).sqrMagnitude;

               if (sqrDistance < bestSqrDistance)
               {
                   bestSqrDistance = sqrDistance;
                   bestDistance = sampleDistance;
               }
           }

           return bestDistance;
       }

        for (int i = 0; i <= samples; i++)
        {
            float sampleDistance = raceCircuit.Length * (i / (float)samples);
            Vector3 samplePosition = raceCircuit.GetRoutePosition(sampleDistance);
            float sqrDistance = (worldPosition - samplePosition).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestDistance = sampleDistance;
            }
        }

       return bestDistance;
   }

   private int GetNextWaypointIndexFromCircuitDistance(float circuitDistance)
   {
       if (runtimeRaceWaypoints == null || runtimeRaceWaypoints.waypoints == null || runtimeRaceWaypoints.waypoints.Count == 0)
           return 0;

       if (runtimeRaceWaypointCircuit == null || runtimeRaceWaypointCircuit.Waypoints == null || runtimeRaceWaypointCircuit.Waypoints.Length == 0)
           return 0;

       Vector3 routePosition = runtimeRaceWaypointCircuit.GetRoutePosition(circuitDistance);
       int closestIndex = 0;
       float closestSqrDistance = float.MaxValue;

       for (int i = 0; i < runtimeRaceWaypoints.waypoints.Count; i++)
       {
           RCCP_Waypoint waypoint = runtimeRaceWaypoints.waypoints[i];

           if (waypoint == null)
               continue;

           float sqrDistance = (waypoint.transform.position - routePosition).sqrMagnitude;

           if (sqrDistance < closestSqrDistance)
           {
               closestSqrDistance = sqrDistance;
               closestIndex = i;
           }
       }

       return closestIndex;
   }

   private float GetForwardLoopDistance(float fromDistance, float toDistance, float loopLength)
   {
       if (loopLength <= 0f)
           return 0f;

       float delta = toDistance - fromDistance;

       if (delta < 0f)
           delta += loopLength;

       return delta;
   }

   private void UpdateRaceUI()
   {
       if (currentLapText != null)
       {
           if (RaceType == RaceType.Elimination)
               currentLapText.text = string.Format(
                   UILocalization.Get("ui.survivors_format", "SURVIVORS {0}/{1}"),
                   GetActiveRacerCount(), allRacers.Length);
           else if (RaceType == RaceType.NoBrakeChallenge)
           {
               int shownLap = Mathf.Min(playerRacer.completedLaps + 1, totalRaceLaps);
               currentLapText.text = string.Format(
                   UILocalization.Get("ui.no_brake_lap_format", "NO BRAKE  LAP {0}/{1}"),
                   shownLap, totalRaceLaps);
           }
           else
           {
               int shownLap = Mathf.Min(playerRacer.completedLaps + 1, totalRaceLaps);
               currentLapText.text = string.Format(
                   UILocalization.Get("ui.lap_format", "LAP {0}/{1}"),
                   shownLap, totalRaceLaps);
           }
       }

       if (racePositionText != null)
           UpdateRacePositionText();

       if (eliminationTimerText != null)
       {
           eliminationTimerText.gameObject.SetActive(RaceType == RaceType.Elimination && raceStarted && !playerRacer.finished);

           if (RaceType == RaceType.Elimination && raceStarted && !playerRacer.finished)
               UpdateEliminationTimerUI();
       }
   }

   private void UpdateDriftUI()
   {
       currentDriftDisplayedScore = RaceType == RaceType.ComboMaster ? currentMP : totalDriftPoints + currentDriftPoints;

       if (TotalScoreText != null)
           TotalScoreText.text = RaceType == RaceType.ComboMaster ? $"x{currentDriftDisplayedScore:0.0}" : currentDriftDisplayedScore.ToString("N0");

       if (DriftComboText != null)
           UpdateDriftComboText();

       if (DriftTargetText != null)
           DriftTargetText.text = GetDriftTargetText();

       if (DriftProgressSlider != null)
       {
           float progressTarget = RaceType == RaceType.TargetDrift ? GetTargetDriftScore() : GetGoldTarget();
           DriftProgressSlider.value = Mathf.Clamp01(currentDriftDisplayedScore / Mathf.Max(1f, progressTarget));
       }

       if (DriftMedalText != null)
           DriftMedalText.text = GetCurrentDriftMedalText();

       if (DriftModeText != null)
       {
           if (RaceType == RaceType.ComboMaster)
               DriftModeText.text = "Combo Master";
           else if (RaceType == RaceType.TargetDrift)
               DriftModeText.text = "Target Drift";
           else if (RaceType == RaceType.DriftScore)
               DriftModeText.text = "Drift Score";
       }

       if (DriftTimerText != null)
       {
           bool showTargetTimer = RaceType == RaceType.TargetDrift && !driftModeFinished;
           DriftTimerText.gameObject.SetActive(showTargetTimer);

           if (showTargetTimer)
           {
               int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, targetDriftTimeRemaining));
               int minutes = totalSeconds / 60;
               int seconds = totalSeconds % 60;
               DriftTimerText.text = $"{minutes:00}:{seconds:00}";
               UpdateTargetDriftTimerUI();
           }
           else
           {
               DriftTimerText.color = targetDriftTimerNormalColor;
               DriftTimerText.rectTransform.localScale = Vector3.one;
           }
       }

       UpdateDriftMedalImages();
   }

   private void UpdateDriftMedalImages()
   {
       bool targetDriftUnlocked = RaceType == RaceType.TargetDrift && currentDriftDisplayedScore >= GetTargetDriftScore();

       SetMedalImageState(BronzeMedalImage, RaceType == RaceType.TargetDrift ? targetDriftUnlocked : currentDriftDisplayedScore >= GetBronzeTarget());
       SetMedalImageState(SilverMedalImage, RaceType == RaceType.TargetDrift ? targetDriftUnlocked : currentDriftDisplayedScore >= GetSilverTarget());
       SetMedalImageState(GoldMedalImage, RaceType == RaceType.TargetDrift ? targetDriftUnlocked : currentDriftDisplayedScore >= GetGoldTarget());
   }

   private void SetMedalImageState(Image medalImage, bool unlocked)
   {
       if (medalImage == null)
           return;

       Color color = medalImage.color;
       color.a = unlocked ? unlockedMedalAlpha : lockedMedalAlpha;
       medalImage.color = color;
   }

   private void CheckDriftTargets()
   {
       if (!IsDriftScoringMode() || driftModeFinished)
           return;

       if (RaceType == RaceType.TargetDrift)
       {
           if (currentDriftDisplayedScore >= GetTargetDriftScore())
               CompleteDriftMission(true, "Finish");

           return;
       }

       if (currentDriftDisplayedScore >= GetGoldTarget())
           CompleteDriftMission(true, "Finish");
       else if (currentDriftDisplayedScore >= GetSilverTarget())
       {
           if (raceStateText != null)
               raceStateText.text = "Silver";
       }
       else if (currentDriftDisplayedScore >= GetBronzeTarget())
       {
           if (raceStateText != null)
               raceStateText.text = "Bronze";
       }
   }

   private float GetCurrentDriftComboMultiplier()
   {
       if (comboStepDuration <= 0f)
           return 1f;

       if (currentDriftComboTime < comboStartDelay)
           return 1f;

       float comboSteps = 1f + Mathf.Floor(Mathf.Max(0f, currentDriftComboTime - comboStartDelay) / comboStepDuration);
       float combo = 1f + (comboSteps * Mathf.Max(0.5f, comboStepValue));
       return Mathf.Clamp(combo, 1f, Mathf.Max(1f, maxDriftComboMultiplier));
   }

   private void UpdateDriftComboText()
   {
       if (DriftComboText == null)
           return;

       bool comboVisible = currentDriftComboTime >= comboStartDelay && currentMP > 1f;
       DriftComboText.gameObject.SetActive(comboVisible);

       if (!comboVisible)
       {
           DriftComboText.rectTransform.localScale = Vector3.one;
           lastDisplayedComboMultiplier = 0f;
           return;
       }

       DriftComboText.text = $"x{currentMP:0.0}";

       if (Mathf.Abs(lastDisplayedComboMultiplier - currentMP) > 0.01f)
       {
           lastDisplayedComboMultiplier = currentMP;

           if (driftComboAnimationCoroutine != null)
               StopCoroutine(driftComboAnimationCoroutine);

           driftComboAnimationCoroutine = StartCoroutine(AnimateDriftComboChange());
       }
   }

   private IEnumerator AnimateDriftComboChange()
   {
       if (DriftComboText == null)
           yield break;

       Vector3 baseScale = Vector3.one;
       Vector3 targetScale = Vector3.one * (1f + comboPulseScale);
       float duration = Mathf.Max(0.01f, comboPulseDuration);

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = time / duration;
           DriftComboText.rectTransform.localScale = Vector3.Lerp(baseScale, targetScale, t);
           yield return null;
       }

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = time / duration;
           DriftComboText.rectTransform.localScale = Vector3.Lerp(targetScale, baseScale, t);
           yield return null;
       }

       DriftComboText.rectTransform.localScale = baseScale;
       driftComboAnimationCoroutine = null;
   }

   private float GetBronzeTarget()
   {
       if (RaceType == RaceType.ComboMaster)
           return bronzeComboTarget;

       MissionSO mission = SelectedCareerMission.Mission;

       if (mission != null && mission.targetScore > 0)
           return mission.targetScore;

       if (useCurrentMapDriftTarget && GlobalCarData.thismap != null)
       {
           if (GlobalCarData.thismap.driftBronzeTarget > 0)
               return GlobalCarData.thismap.driftBronzeTarget;

           if (GlobalCarData.thismap.target > 0)
               return GlobalCarData.thismap.target;
       }

       return bronzeTargetScore;
   }

   private float GetSilverTarget()
   {
       return GetBronzeTarget() * Mathf.Max(1f, silverTargetMultiplier);
   }

   private float GetGoldTarget()
   {
       return GetBronzeTarget() * Mathf.Max(1f, goldTargetMultiplier);
   }

   private string GetCurrentDriftMedalText()
   {
       if (RaceType == RaceType.TargetDrift)
           return currentDriftDisplayedScore >= GetTargetDriftScore() ? "Target Reached" : "Reach Target";

       if (currentDriftDisplayedScore >= GetGoldTarget())
           return "Gold";

       if (currentDriftDisplayedScore >= GetSilverTarget())
           return "Silver";

       if (currentDriftDisplayedScore >= GetBronzeTarget())
           return "Bronze";

       return RaceType == RaceType.ComboMaster ? "No Combo Medal" : "No Medal";
   }

   private string GetDriftTargetText()
   {
       if (RaceType == RaceType.ComboMaster)
       {
           return $"Bronze  x{GetBronzeTarget():0.0}\n" +
                  $"Silver  x{GetSilverTarget():0.0}\n" +
                  $"Gold    x{GetGoldTarget():0.0}";
       }

       if (RaceType == RaceType.TargetDrift)
       {
           return $"Target  {GetTargetDriftScore():N0}\n" +
                  $"Time    {GetTargetDriftTimeLimit():0}s";
       }

       return $"Bronze  {GetBronzeTarget():N0}\n" +
              $"Silver  {GetSilverTarget():N0}\n" +
              $"Gold    {GetGoldTarget():N0}";
   }

   private void UpdateTargetDriftTimer()
   {
       if (RaceType != RaceType.TargetDrift || driftModeFinished)
           return;

       targetDriftTimeRemaining -= Time.deltaTime;

       if (targetDriftTimeRemaining > 0f)
           return;

       targetDriftTimeRemaining = 0f;
       CompleteDriftMission(currentDriftDisplayedScore >= GetTargetDriftScore(), currentDriftDisplayedScore >= GetTargetDriftScore() ? "Finish" : "Failed");
   }

   private void UpdateTargetDriftTimerUI()
   {
       if (DriftTimerText == null)
           return;

       bool isWarning = targetDriftTimeRemaining <= targetDriftWarningThreshold;
       DriftTimerText.color = isWarning ? targetDriftTimerWarningColor : targetDriftTimerNormalColor;

       if (isWarning)
       {
           float pulse = 1f + Mathf.Sin(Time.unscaledTime * targetDriftPulseSpeed) * targetDriftPulseScale;
           DriftTimerText.rectTransform.localScale = Vector3.one * pulse;
       }
       else
       {
           DriftTimerText.rectTransform.localScale = Vector3.one;
       }
   }

   private float GetTargetDriftScore()
   {
       MissionSO mission = SelectedCareerMission.Mission;

       if (mission != null && mission.targetScore > 0)
           return mission.targetScore;

       if (useCurrentMapTargetDriftSettings && GlobalCarData.thismap != null)
       {
           if (GlobalCarData.thismap.targetDriftScore > 0)
               return GlobalCarData.thismap.targetDriftScore;

           if (GlobalCarData.thismap.target > 0)
               return GlobalCarData.thismap.target;
       }

       return targetDriftScore;
   }

   private float GetTargetDriftTimeLimit()
   {
       MissionSO mission = SelectedCareerMission.Mission;

       if (mission != null && mission.timeLimit > 0)
           return mission.timeLimit;

       if (useCurrentMapTargetDriftSettings && GlobalCarData.thismap != null)
       {
           if (GlobalCarData.thismap.targetDriftTimeLimit > 0)
               return GlobalCarData.thismap.targetDriftTimeLimit;

           if (GlobalCarData.thismap.time > 0)
               return GlobalCarData.thismap.time;
       }

       return targetDriftTimeLimit;
   }

   private void UpdateEliminationTimerUI()
   {
       if (eliminationTimerText == null)
           return;

       float clampedTimer = Mathf.Max(0f, eliminationTimer);
       int totalSeconds = Mathf.CeilToInt(clampedTimer);
       int minutes = totalSeconds / 60;
       int seconds = totalSeconds % 60;

       eliminationTimerText.text = $"{minutes:00}:{seconds:00}";

       bool isWarning = clampedTimer <= eliminationWarningThreshold;
       bool isCritical = clampedTimer <= eliminationCriticalThreshold;
       eliminationTimerText.color = isWarning ? eliminationTimerWarningColor : eliminationTimerNormalColor;

       if (isWarning)
       {
           float pulseSpeed = isCritical ? eliminationCriticalPulseSpeed : eliminationPulseSpeed;
           float pulseScale = isCritical ? eliminationCriticalPulseScale : eliminationPulseScale;
           float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseScale;
           eliminationTimerText.rectTransform.localScale = Vector3.one * pulse;
       }
       else
       {
           eliminationTimerText.rectTransform.localScale = Vector3.one;
       }
   }

   private void CompleteRaceMission(bool success, string stateText)
   {
       if (missionResultsShown)
           return;

       missionSucceeded = success;
       missionResultsShown = true;
       raceStarted = false;
       if (stateText == "Eliminated")
           GameHaptics.Eliminated();
       else if (success)
           GameHaptics.Victory();
       else
           GameHaptics.Defeat();
       SetRaceParticipantsControl(false);

       if (raceStateText != null)
       {
           raceStateText.text = GetLocalizedRaceState(stateText);

           if (success && stateText == "Winner")
               ScheduleRaceStateClear(3f);
       }

       if (success)
           CareerMissionProgress.MarkMissionCompleted(SelectedCareerMission.Tournament, SelectedCareerMission.Mission);

       ApplyMissionRewards();
       ShowFinishSummaryScreen();
   }

   private void CompleteDriftMission(bool success, string stateText)
   {
       if (missionResultsShown)
           return;

       missionSucceeded = success;
       if (success)
           GameHaptics.Victory();
       else
           GameHaptics.Defeat();
       missionResultsShown = true;
       driftModeFinished = true;
       canScore = false;

       if (CarController != null)
           CarController.canControl = false;

       if (scoreText != null && scoreText.gameObject.activeSelf)
           scoreText.gameObject.SetActive(false);

       if (raceStateText != null)
           raceStateText.text = GetLocalizedRaceState(stateText);

       if (success)
           CareerMissionProgress.MarkMissionCompleted(SelectedCareerMission.Tournament, SelectedCareerMission.Mission);

       ApplyMissionRewards();
       ShowFinishSummaryScreen();
   }

   private void ApplyMissionRewards()
   {
       if (missionRewardsApplied || SaveManager.Instance == null || SaveManager.Instance.saveData == null)
           return;

       missionRewardsApplied = true;

       int playerPosition = IsRaceMode() ? GetPlayerRacePosition() : 1;
       missionRewardEarned = missionSucceeded ? GetMissionRewardAmount() : 0;
       missionExpEarned = GetMissionExpReward(playerPosition, missionSucceeded);
       missionStartingExpTotal = Mathf.Max(0, SaveManager.Instance.saveData.exp);
       missionStartingLevel = Mathf.Max(1, SaveManager.Instance.saveData.currentLevel);

       int expRequirement = Mathf.Max(1, expPerLevel);
       int startLevelProgress = missionStartingExpTotal % expRequirement;
       int levelUps = (startLevelProgress + missionExpEarned) / expRequirement;

       missionLevelRewardEarned = Mathf.Max(0, levelUps * levelUpMoneyReward);
       missionFinalExpTotal = missionStartingExpTotal + missionExpEarned;
       missionFinalLevel = missionStartingLevel + levelUps;

       SaveManager.Instance.saveData.exp = missionFinalExpTotal;
       SaveManager.Instance.saveData.currentLevel = missionFinalLevel;
       AddMoneyToPlayer(missionRewardEarned + missionLevelRewardEarned);
       SaveManager.Instance.Save();
   }

   private void ShowFinishSummaryScreen()
   {
       SetRaceUIVisible(false);

       if (finishSummaryScreen != null)
           finishSummaryScreen.SetActive(true);

       if (finishExpScreen != null)
           finishExpScreen.SetActive(false);

       if (finishTitleText != null)
       {
           finishTitleText.text = missionSucceeded
               ? UILocalization.Get("ui.complete", "COMPLETE")
               : UILocalization.Get("ui.failed", "FAILED");
           finishTitleText.color = missionSucceeded ? finishCompleteTitleColor : finishFailedTitleColor;
       }

       if (finishModeText != null)
           finishModeText.text = GetRaceModeDisplayName();

       if (finishSummaryContinueButton != null)
           finishSummaryContinueButton.SetActive(false);

       if (finishSummaryAnimationCoroutine != null)
           StopCoroutine(finishSummaryAnimationCoroutine);

       finishSummaryAnimationCoroutine = StartCoroutine(AnimateFinishSummaryValues());

       if (finishLeaderboardText != null)
       {
           bool shouldShowLeaderboard = showFinishLeaderboard && IsRaceMode();
           finishLeaderboardText.gameObject.SetActive(shouldShowLeaderboard);

           if (shouldShowLeaderboard)
               finishLeaderboardText.text = GetFinishLeaderboardText();
       }
   }

   public void OpenFinishExpScreen()
   {
       SetRaceUIVisible(false);

       if (finishSummaryScreen != null)
           finishSummaryScreen.SetActive(false);

       if (finishExpScreen != null)
           finishExpScreen.SetActive(true);

       if (finishExpContinueButton != null)
           finishExpContinueButton.SetActive(false);

       if (expAnimationCoroutine != null)
           StopCoroutine(expAnimationCoroutine);

       if (expRewardAnimationCoroutine != null)
           StopCoroutine(expRewardAnimationCoroutine);

       if (expSliderAnimationCoroutine != null)
           StopCoroutine(expSliderAnimationCoroutine);

        if (expLevelRewardText != null)
        {
            expLevelRewardText.gameObject.SetActive(false);
            expLevelRewardText.rectTransform.localScale = Vector3.one;
        }

        if (expProgressSlider != null)
            expProgressSlider.transform.localScale = Vector3.one;

       expAnimationCoroutine = StartCoroutine(AnimateExpRewardSequence());
   }

   public void CloseFinishScreens()
   {
       if (finishSummaryScreen != null)
           finishSummaryScreen.SetActive(false);

       if (finishExpScreen != null)
           finishExpScreen.SetActive(false);

       if (finishSummaryContinueButton != null)
           finishSummaryContinueButton.SetActive(false);

        if (finishExpContinueButton != null)
            finishExpContinueButton.SetActive(false);

       if (expProgressSlider != null)
           expProgressSlider.transform.localScale = Vector3.one;

       if (expLevelRewardText != null)
           expLevelRewardText.rectTransform.localScale = Vector3.one;

       if (expSliderAnimationCoroutine != null)
           expSliderAnimationCoroutine = null;

       if (finishSummaryAnimationCoroutine != null)
           finishSummaryAnimationCoroutine = null;

       if (finishContinueSelectionCoroutine != null)
       {
           StopCoroutine(finishContinueSelectionCoroutine);
           finishContinueSelectionCoroutine = null;
       }

       if (raceStateClearCoroutine != null)
       {
           StopCoroutine(raceStateClearCoroutine);
           raceStateClearCoroutine = null;
       }

       SetRaceUIVisible(true);
   }

   public void ReturnToMenuFromFinish()
   {
       if (LoadingManager.Instance != null)
       {
           LoadingManager.Instance.LoadScene("Menu");
           return;
       }

       SceneManager.LoadScene("Menu");
   }

   public bool IsRaceStartedForPause()
   {
       if (!IsRaceMode())
           return true;

       return raceStarted;
   }

   public bool IsAnyResultScreenVisible()
   {
       if (finishSummaryScreen != null && finishSummaryScreen.activeInHierarchy)
           return true;

       if (finishExpScreen != null && finishExpScreen.activeInHierarchy)
           return true;

       return false;
   }

   private IEnumerator AnimateExpRewardSequence()
   {
       float duration = Mathf.Max(0.1f, expAnimationDuration);
       int expRequirement = Mathf.Max(1, expPerLevel);
       int previousDisplayedLevelUps = 0;

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = Mathf.Clamp01(time / duration);
           int displayedGainedExp = Mathf.RoundToInt(Mathf.Lerp(0f, missionExpEarned, t));
           int displayedTotalExp = missionStartingExpTotal + displayedGainedExp;
           int displayedLevelUps = ((missionStartingExpTotal % expRequirement) + displayedGainedExp) / expRequirement;
           int displayedLevel = missionStartingLevel + displayedLevelUps;
           int displayedLevelProgress = displayedTotalExp % expRequirement;

           if (displayedLevelUps > previousDisplayedLevelUps)
           {
               GameHaptics.Success();
               previousDisplayedLevelUps = displayedLevelUps;

               if (expSliderAnimationCoroutine != null)
                   StopCoroutine(expSliderAnimationCoroutine);

               expSliderAnimationCoroutine = StartCoroutine(AnimateExpSliderPulse());

               if (expRewardAnimationCoroutine != null)
                   StopCoroutine(expRewardAnimationCoroutine);

               expRewardAnimationCoroutine = StartCoroutine(AnimateExpRewardReveal());
           }

           UpdateExpScreenUI(displayedLevel, displayedTotalExp, displayedGainedExp, displayedLevelUps, displayedLevelProgress, expRequirement);
           yield return null;
       }

       UpdateExpScreenUI(
           missionFinalLevel,
           missionFinalExpTotal,
           missionExpEarned,
           Mathf.Max(0, missionFinalLevel - missionStartingLevel),
           missionFinalExpTotal % expRequirement,
           expRequirement);

       if (expProgressSlider != null)
           expProgressSlider.transform.localScale = Vector3.one;

       if (finishExpContinueButton != null)
       {
           finishExpContinueButton.SetActive(true);
           SelectFinishContinueButton(finishExpContinueButton);
       }

       expAnimationCoroutine = null;
   }

   private IEnumerator AnimateFinishSummaryValues()
   {
       float stepDuration = Mathf.Max(0.05f, finishSummaryValueDuration);
       float stepDelay = Mathf.Max(0f, finishSummaryStepDelay);

       if (finishRewardText != null)
       {
           yield return AnimateSummaryMoney(stepDuration);

           if (stepDelay > 0f)
               yield return new WaitForSecondsRealtime(stepDelay);
       }

       if (finishPositionText != null)
       {
           yield return AnimateSummaryPrimaryStat(stepDuration);

           if (stepDelay > 0f)
               yield return new WaitForSecondsRealtime(stepDelay);
       }

       if (finishTimeText != null)
           yield return AnimateSummaryTime(stepDuration);

       if (finishSummaryContinueButton != null)
       {
           finishSummaryContinueButton.SetActive(true);
           SelectFinishContinueButton(finishSummaryContinueButton);
       }

       finishSummaryAnimationCoroutine = null;
   }

   private void SelectFinishContinueButton(GameObject target)
   {
       if (target == null || EventSystem.current == null)
           return;

       if (finishContinueSelectionCoroutine != null)
           StopCoroutine(finishContinueSelectionCoroutine);

       finishContinueSelectionCoroutine = StartCoroutine(SelectFinishContinueButtonNextFrame(target));
   }

   private IEnumerator SelectFinishContinueButtonNextFrame(GameObject target)
   {
       for (int i = 0; i < 4; i++)
       {
           yield return null;
           yield return new WaitForEndOfFrame();

           if (target == null || !target.activeInHierarchy || EventSystem.current == null)
           {
               finishContinueSelectionCoroutine = null;
               yield break;
           }

           Button button = target.GetComponent<Button>();
           GameObject selectedTarget = button != null ? button.gameObject : target;

           Canvas.ForceUpdateCanvases();
           EventSystem.current.SetSelectedGameObject(null);
           EventSystem.current.SetSelectedGameObject(selectedTarget);

            if (button != null)
                button.Select();
       }

       finishContinueSelectionCoroutine = null;
   }

   private void ScheduleRaceStateClear(float delay)
   {
       if (raceStateText == null)
           return;

       if (raceStateClearCoroutine != null)
           StopCoroutine(raceStateClearCoroutine);

       raceStateClearCoroutine = StartCoroutine(ClearRaceStateTextAfterDelay(delay));
   }

   private IEnumerator ClearRaceStateTextAfterDelay(float delay)
   {
       yield return new WaitForSeconds(delay);

       if (raceStateText != null)
           raceStateText.text = string.Empty;

       raceStateClearCoroutine = null;
   }

   private void SetRaceUIVisible(bool visible)
   {
       SetUIElementVisible(currentLapText, visible);
       SetUIElementVisible(racePositionText, visible);
       SetUIElementVisible(raceStateText, visible);
       SetUIElementVisible(eliminationTimerText, visible);

       if (RCCP_SceneManager.Instance != null &&
           RCCP_SceneManager.Instance.activePlayerCanvas != null)
       {
           RCCP_UIManager playerCanvas = RCCP_SceneManager.Instance.activePlayerCanvas;
           playerCanvas.enabled = visible;

           if (playerCanvas.dashboard != null)
               playerCanvas.dashboard.SetActive(visible);
       }
   }

   private void SetUIElementVisible(Component component, bool visible)
   {
       if (component == null || component.gameObject == null)
           return;

       component.gameObject.SetActive(visible);
   }

   private void UpdateExpScreenUI(int displayedLevel, int displayedTotalExp, int displayedGainedExp, int displayedLevelUps, int displayedLevelProgress, int expRequirement)
   {
       if (expLevelText != null)
           expLevelText.text = string.Format(
               UILocalization.Get("ui.level", "LEVEL {0}"),
               displayedLevel);

       if (expTotalText != null)
           expTotalText.text = string.Format(
               UILocalization.Get("ui.exp_total_format", "{0:N0} EXP"),
               displayedTotalExp);

       if (expGainText != null)
           expGainText.text = string.Format(
               UILocalization.Get("ui.exp_gain_format", "+{0:N0} EXP"),
               displayedGainedExp);

       if (expLevelRewardText != null)
       {
           int displayedLevelReward = displayedLevelUps * levelUpMoneyReward;
           expLevelRewardText.richText = true;
           expLevelRewardText.gameObject.SetActive(displayedLevelReward > 0);

           if (displayedLevelReward > 0)
               expLevelRewardText.text = string.Format(
                   UILocalization.Get("ui.level_up_reward_format", "LEVEL UP REWARD  +{0:N0}  <color=#FFD21F>CR</color>"),
                   displayedLevelReward);
       }

       if (expProgressSlider != null)
           expProgressSlider.value = (float)displayedLevelProgress / expRequirement;
       
   }

   private IEnumerator AnimateSummaryMoney(float duration)
   {
       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = Mathf.Clamp01(time / duration);
           int displayedValue = Mathf.RoundToInt(Mathf.Lerp(0f, missionRewardEarned, t));
           finishRewardText.text = FormatFinishRewardText(displayedValue);
           yield return null;
       }

       finishRewardText.text = FormatFinishRewardText(missionRewardEarned);
   }

   private string FormatFinishRewardText(int reward)
   {
       return string.Format(
           UILocalization.Get("ui.reward_format", "REWARD: {0:N0}  <color=#FFD21F>CR</color>"),
           reward);
   }

   private IEnumerator AnimateSummaryPrimaryStat(float duration)
   {
       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = Mathf.Clamp01(time / duration);
           finishPositionText.text = GetAnimatedFinishPrimaryStatText(t);
           yield return null;
       }

       finishPositionText.text = GetFinishPrimaryStatText();
   }

   private IEnumerator AnimateSummaryTime(float duration)
   {
       float missionTime = GetMissionElapsedTime();

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = Mathf.Clamp01(time / duration);
           float displayedTime = Mathf.Lerp(0f, missionTime, t);
           finishTimeText.text = string.Format(
               UILocalization.Get("ui.time_format", "TIME: {0}"),
               FormatRaceTime(displayedTime));
           yield return null;
       }

       finishTimeText.text = string.Format(
           UILocalization.Get("ui.time_format", "TIME: {0}"),
           FormatRaceTime(missionTime));
   }

   private string GetAnimatedFinishPrimaryStatText(float normalizedTime)
   {
       float clampedTime = Mathf.Clamp01(normalizedTime);

       if (IsRaceMode())
       {
           int displayedPosition = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, GetPlayerRacePosition(), clampedTime)));
           return string.Format(
               UILocalization.Get("ui.position_format", "POSITION  {0}/{1}"),
               displayedPosition, GetTotalRaceParticipantCount());
       }

       if (RaceType == RaceType.ComboMaster)
       {
           float displayedCombo = Mathf.Lerp(0f, currentDriftDisplayedScore, clampedTime);
           return $"Best Combo  x{displayedCombo:0.0}";
       }

       float displayedScore = Mathf.Lerp(0f, currentDriftDisplayedScore, clampedTime);
       return $"Score  {displayedScore:N0}";
   }

   private IEnumerator AnimateExpSliderPulse()
   {
       if (expProgressSlider == null)
           yield break;

       Vector3 baseScale = Vector3.one;
       Vector3 targetScale = Vector3.one * (1f + expSliderPulseScale);
       float duration = Mathf.Max(0.01f, expSliderPulseDuration);

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = time / duration;
           expProgressSlider.transform.localScale = Vector3.Lerp(baseScale, targetScale, t);
           yield return null;
       }

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = time / duration;
           expProgressSlider.transform.localScale = Vector3.Lerp(targetScale, baseScale, t);
           yield return null;
       }

       expProgressSlider.transform.localScale = baseScale;
       expSliderAnimationCoroutine = null;
   }

   private IEnumerator AnimateExpRewardReveal()
   {
       if (expLevelRewardText == null)
           yield break;

       expLevelRewardText.gameObject.SetActive(true);

       Vector3 baseScale = Vector3.one;
       Vector3 targetScale = Vector3.one * (1f + expRewardPulseScale);
       float duration = Mathf.Max(0.01f, expRewardPulseDuration);

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = time / duration;
           expLevelRewardText.rectTransform.localScale = Vector3.Lerp(baseScale, targetScale, t);
           yield return null;
       }

       for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
       {
           float t = time / duration;
           expLevelRewardText.rectTransform.localScale = Vector3.Lerp(targetScale, baseScale, t);
           yield return null;
       }

       expLevelRewardText.rectTransform.localScale = baseScale;
       expRewardAnimationCoroutine = null;
   }

   private int GetMissionRewardAmount()
   {
       MissionSO mission = SelectedCareerMission.Mission;

       if (mission != null)
           return Mathf.Max(0, mission.rewardMoney);

       if (GlobalCarData.thismap == null)
           return 0;

       return Mathf.Max(0, GlobalCarData.thismap.price);
   }

   private int GetMissionExpReward(int playerPosition, bool success)
   {
       MissionSO mission = SelectedCareerMission.Mission;

       if (mission != null)
           return success ? Mathf.Max(0, mission.rewardExp) : 0;

       int expReward = success ? missionCompletionExp : participationExp;

       switch (playerPosition)
       {
           case 1:
               expReward += firstPlaceExpBonus;
               break;
           case 2:
               expReward += secondPlaceExpBonus;
               break;
           case 3:
               expReward += thirdPlaceExpBonus;
               break;
       }

       return Mathf.Max(0, expReward);
   }

   private float GetMissionElapsedTime()
   {
       return IsRaceMode() ? raceElapsedTime : driftElapsedTime;
   }

   private string GetFinishPrimaryStatText()
   {
       if (IsRaceMode())
           return string.Format(
               UILocalization.Get("ui.position_format", "POSITION  {0}/{1}"),
               GetPlayerRacePosition(), GetTotalRaceParticipantCount());

       if (RaceType == RaceType.ComboMaster)
           return $"Best Combo  x{currentDriftDisplayedScore:0.0}";

       return $"Score  {currentDriftDisplayedScore:N0}";
   }

   private void AddMoneyToPlayer(int amount)
   {
       if (amount <= 0 || SaveManager.Instance == null || SaveManager.Instance.saveData == null)
           return;

       MoneyManager moneyManager = FindFirstObjectByType<MoneyManager>(FindObjectsInactive.Include);

       if (moneyManager != null)
       {
           moneyManager.MoneyToAdd(amount);
           return;
       }

       SaveManager.Instance.saveData.money += amount;
   }

   private string GetRaceModeDisplayName()
   {
       switch (RaceType)
       {
           case RaceType.Racing:
               return UILocalization.Get("race.classic", "CLASSIC RACE");
           case RaceType.Elimination:
               return UILocalization.Get("race.elimination", "ELIMINATION");
           case RaceType.NoBrakeChallenge:
               return UILocalization.Get("race.no_brake", "NO BRAKE CHALLENGE");
           case RaceType.DriftScore:
               return UILocalization.Get("race.drift_score", "DRIFT SCORE");
           case RaceType.TargetDrift:
               return UILocalization.Get("race.target_drift", "TARGET DRIFT");
           case RaceType.ComboMaster:
               return UILocalization.Get("race.combo_master", "COMBO MASTER");
           case RaceType.FreeDrift:
               return UILocalization.Get("race.free_drift", "FREE DRIFT");
           default:
               return RaceType.ToString();
       }
   }

   private string FormatRaceTime(float timeValue)
   {
       int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(timeValue));
       int minutes = totalSeconds / 60;
       int seconds = totalSeconds % 60;
       return $"{minutes:00}:{seconds:00}";
   }

   private void UpdateRacePositionText()
   {
       if (racePositionText == null)
           return;

       string newPositionText = showLiveLeaderboard ? GetLiveLeaderboardText() : GetPlayerRacePositionText();
       racePositionText.text = newPositionText;

       if (string.IsNullOrEmpty(lastRacePositionText))
       {
           lastRacePositionText = newPositionText;
           return;
       }

       if (lastRacePositionText == newPositionText)
           return;

       lastRacePositionText = newPositionText;

       if (racePositionAnimationCoroutine != null)
           StopCoroutine(racePositionAnimationCoroutine);

       racePositionAnimationCoroutine = StartCoroutine(AnimateRacePositionChange());
   }

   private IEnumerator AnimateRacePositionChange()
   {
       if (racePositionText == null)
           yield break;

       Color originalColor = racePositionText.color;
       float fadeOutDuration = Mathf.Max(0.01f, positionChangeFadeDuration);
       float fadeInDuration = fadeOutDuration;

       for (float time = 0f; time < fadeOutDuration; time += Time.unscaledDeltaTime)
       {
           float t = time / fadeOutDuration;
           Color color = originalColor;
           color.a = Mathf.Lerp(1f, positionChangeMinAlpha, t);
           racePositionText.color = color;
           yield return null;
       }

       for (float time = 0f; time < fadeInDuration; time += Time.unscaledDeltaTime)
       {
           float t = time / fadeInDuration;
           Color color = originalColor;
           color.a = Mathf.Lerp(positionChangeMinAlpha, 1f, t);
           racePositionText.color = color;
           yield return null;
       }

       racePositionText.color = originalColor;
       racePositionAnimationCoroutine = null;
   }

   private string GetLiveLeaderboardText()
   {
       List<RaceRacer> rankedRacers = GetRankedActiveRacers();

       if (rankedRacers.Count == 0)
           return string.Empty;

       List<string> leaderboardLines = new List<string>(rankedRacers.Count + 1)
       {
           UILocalization.Get("ui.leaderboard", "LEADERBOARD") + ":"
       };

       for (int i = 0; i < rankedRacers.Count; i++)
       {
           RaceRacer racer = rankedRacers[i];
           string racerName = ReferenceEquals(racer, playerRacer) ? GetPlayerLeaderboardName() : racer.displayName;

           if (ReferenceEquals(racer, playerRacer))
               racerName = $"<color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(playerLeaderboardColor)}>{racerName}</color>";

           leaderboardLines.Add($"{i + 1}. {racerName}");
       }

       return string.Join("\n", leaderboardLines);
   }

   private string GetFinishLeaderboardText()
   {
       List<RaceRacer> rankedRacers = GetRankedRacers(includeEliminated: true);

       if (rankedRacers.Count == 0)
           return string.Empty;

       List<string> leaderboardLines = new List<string>(rankedRacers.Count);

       for (int i = 0; i < rankedRacers.Count; i++)
       {
           RaceRacer racer = rankedRacers[i];
           string racerName = ReferenceEquals(racer, playerRacer) ? GetPlayerLeaderboardName() : racer.displayName;

           if (ReferenceEquals(racer, playerRacer))
               racerName = $"<color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(playerLeaderboardColor)}>{racerName}</color>";

           if (racer.eliminated && RaceType == RaceType.Elimination)
               racerName += "  OUT";

           leaderboardLines.Add($"{i + 1}. {racerName}");
       }

       return string.Join("\n", leaderboardLines);
   }

   private string GetPlayerLeaderboardName()
   {
       if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
           return "YOU";

       string playerName = SaveManager.Instance.saveData.PlayerName;
       return string.IsNullOrWhiteSpace(playerName) ? "YOU" : playerName;
   }

   private void SpawnCheckpointVisuals()
   {
       ClearCheckpointVisuals();

       if (checkpointPrefab == null || runtimeRaceWaypoints == null || runtimeRaceWaypoints.waypoints == null)
           return;

       checkpointVisuals = new GameObject[runtimeRaceWaypoints.waypoints.Count];

       for (int i = 0; i < runtimeRaceWaypoints.waypoints.Count; i++)
       {
           RCCP_Waypoint waypoint = runtimeRaceWaypoints.waypoints[i];

           if (waypoint == null)
               continue;

           Vector3 spawnPosition = waypoint.transform.position + checkpointVisualOffset;
           GameObject visual = Instantiate(checkpointPrefab, spawnPosition, waypoint.transform.rotation, waypoint.transform);
           visual.name = $"{checkpointPrefab.name}_{i}";
           checkpointVisuals[i] = visual;
       }

       UpdateCheckpointVisuals();
   }

   private void UpdateCheckpointVisuals()
   {
       if (checkpointVisuals == null || checkpointVisuals.Length == 0)
           return;

       for (int i = 0; i < checkpointVisuals.Length; i++)
       {
           GameObject visual = checkpointVisuals[i];

           if (visual == null)
               continue;

           bool shouldBeVisible = !showOnlyNextCheckpoint || i == playerRacer.currentWaypointIndex;
           visual.SetActive(shouldBeVisible);
       }
   }

   private void ClearCheckpointVisuals()
   {
       if (checkpointVisuals == null || checkpointVisuals.Length == 0)
           return;

       for (int i = 0; i < checkpointVisuals.Length; i++)
       {
           if (checkpointVisuals[i] != null)
               Destroy(checkpointVisuals[i]);
       }

       checkpointVisuals = Array.Empty<GameObject>();
   }

   private string GetPlayerRacePositionText()
   {
       return $"{GetPlayerRacePosition()}/{GetActiveRacerCount()}";
   }

   private List<RaceRacer> GetRankedActiveRacers()
   {
       return GetRankedRacers(includeEliminated: false);
   }

   private List<RaceRacer> GetRankedRacers(bool includeEliminated)
   {
       List<RaceRacer> rankedRacers = new List<RaceRacer>();

       for (int i = 0; i < allRacers.Length; i++)
       {
           RaceRacer racer = allRacers[i];

           if (racer == null)
               continue;

           if (!includeEliminated && racer.eliminated)
               continue;

           rankedRacers.Add(racer);
       }

       rankedRacers.Sort((a, b) =>
       {
           if (ReferenceEquals(a, b))
               return 0;

           if (a.eliminated != b.eliminated)
               return a.eliminated ? 1 : -1;

           if (IsRacerAhead(a, b))
               return -1;

           if (IsRacerAhead(b, a))
               return 1;

           return 0;
       });

       return rankedRacers;
   }

   private int GetPlayerRacePosition()
   {
       if (playerRacer != null && playerRacer.finished && playerRacer.finishPosition > 0)
           return playerRacer.finishPosition;

       if (allRacers == null || allRacers.Length == 0)
           return 1;

       int position = 1;

       for (int i = 0; i < allRacers.Length; i++)
       {
           RaceRacer racer = allRacers[i];

           if (racer == null || racer == playerRacer)
               continue;

           if (racer.eliminated)
               continue;

           if (IsRacerAheadOfPlayer(racer))
               position++;
       }

       return position;
   }

   private void MarkRacerFinished(RaceRacer racer)
   {
       if (racer == null || racer.finished)
           return;

       racer.finished = true;

       if (racer.finishPosition <= 0)
           racer.finishPosition = nextRaceFinishPosition++;

       racer.finishTime = raceElapsedTime;
   }

   private int GetTotalRaceParticipantCount()
   {
       if (allRacers == null || allRacers.Length == 0)
           return 1;

       int total = 0;

       for (int i = 0; i < allRacers.Length; i++)
       {
           if (allRacers[i] != null)
               total++;
       }

       return Mathf.Max(1, total);
   }

   private bool IsRacerAheadOfPlayer(RaceRacer otherRacer)
   {
       if (otherRacer == null || otherRacer.eliminated)
           return false;

       if (playerRacer.finished && playerRacer.finishPosition > 0)
           return otherRacer.finished && otherRacer.finishPosition > 0 && otherRacer.finishPosition < playerRacer.finishPosition;

       if (playerRacer.eliminated)
           return true;

       if (ShouldUseGridRanking(playerRacer, otherRacer))
           return GetGridRankingValue(otherRacer) > GetGridRankingValue(playerRacer);

       if (otherRacer.completedLaps != playerRacer.completedLaps)
           return otherRacer.completedLaps > playerRacer.completedLaps;

       if (HasCheckpointProgressSource())
       {
           if (otherRacer.currentCheckpointIndex != playerRacer.currentCheckpointIndex)
               return otherRacer.currentCheckpointIndex > playerRacer.currentCheckpointIndex;

           return otherRacer.distanceToNextWaypoint < playerRacer.distanceToNextWaypoint;
       }

       return otherRacer.sharedRankingProgress > playerRacer.sharedRankingProgress;
   }

   private void UpdateEliminationMode()
   {
       if (RaceType != RaceType.Elimination || playerRacer.finished)
           return;

       if (GetActiveRacerCount() <= 1)
       {
           ResolveEliminationWinner();
           return;
       }

       eliminationTimer -= Time.deltaTime;

       if (eliminationTimer > 0f)
           return;

       eliminationTimer = eliminationInterval;

       RaceRacer lastRacer = GetLastActiveRacer();

       if (lastRacer != null)
           EliminateRacer(lastRacer);

       if (GetActiveRacerCount() <= 1)
           ResolveEliminationWinner();
   }

   private RaceRacer GetLastActiveRacer()
   {
       RaceRacer lastRacer = null;

       for (int i = 0; i < allRacers.Length; i++)
       {
           RaceRacer candidate = allRacers[i];

           if (candidate == null || candidate.eliminated || candidate.finished)
               continue;

           if (lastRacer == null || IsRacerAhead(lastRacer, candidate))
               lastRacer = candidate;
       }

       return lastRacer;
   }

   private bool IsRacerAhead(RaceRacer racerA, RaceRacer racerB)
   {
       if (racerA == null || racerA.eliminated)
           return false;

       if (racerB == null || racerB.eliminated)
           return true;

       if (racerA.finished || racerB.finished)
       {
           if (racerA.finished && racerB.finished)
               return racerA.finishPosition > 0 && (racerB.finishPosition <= 0 || racerA.finishPosition < racerB.finishPosition);

           return racerA.finished;
       }

       if (ShouldUseGridRanking(racerA, racerB))
           return GetGridRankingValue(racerA) > GetGridRankingValue(racerB);

       if (racerA.completedLaps != racerB.completedLaps)
           return racerA.completedLaps > racerB.completedLaps;

       if (HasCheckpointProgressSource())
       {
           if (racerA.currentCheckpointIndex != racerB.currentCheckpointIndex)
               return racerA.currentCheckpointIndex > racerB.currentCheckpointIndex;

           return racerA.distanceToNextWaypoint < racerB.distanceToNextWaypoint;
       }

       return racerA.sharedRankingProgress > racerB.sharedRankingProgress;
   }

   private bool ShouldUseGridRanking(RaceRacer racerA, RaceRacer racerB)
   {
       if (SpawnPoint == null)
           return false;

       if (racerA == null || racerB == null)
           return false;

       if (racerA.completedLaps > 0 || racerB.completedLaps > 0)
           return false;

       if (HasCheckpointProgressSource())
           return racerA.currentCheckpointIndex < 0 && racerB.currentCheckpointIndex < 0;

       return !racerA.lapCountingArmed && !racerB.lapCountingArmed;
   }

   private float GetGridRankingValue(RaceRacer racer)
   {
       if (racer == null || racer.racerTransform == null || SpawnPoint == null)
           return float.MinValue;

       Vector3 localPosition = SpawnPoint.InverseTransformPoint(racer.racerTransform.position);

       return localPosition.z;
   }

   private void EliminateRacer(RaceRacer racer)
   {
       if (racer == null || racer.eliminated)
           return;

       racer.eliminated = true;

       RCCP_CarController racerCar = GetRacerCarController(racer);

       if (racerCar != null)
       {
           racerCar.canControl = false;
           racerCar.gameObject.SetActive(false);
       }

       if (ReferenceEquals(racer, playerRacer))
       {
           playerRacer.finished = true;
           CompleteRaceMission(false, "Eliminated");
       }
       else if (raceStateText != null)
       {
           raceStateText.text = string.Format(
               UILocalization.Get("ui.racer_out_format", "{0} OUT"),
               racer.displayName);
           ScheduleRaceStateClear(3f);
       }
   }

   private string GetLocalizedRaceState(string stateText)
   {
       switch (stateText)
       {
           case "Finish":
               return UILocalization.Get("ui.finish", "FINISH");
           case "Winner":
               return UILocalization.Get("ui.winner", "WINNER");
           case "Eliminated":
               return UILocalization.Get("ui.eliminated", "ELIMINATED");
           case "Failed":
               return UILocalization.Get("ui.failed", "FAILED");
           default:
               return stateText;
       }
   }

   private void ResolveEliminationWinner()
   {
       if (playerRacer.finished)
           return;

       if (!playerRacer.eliminated && GetActiveRacerCount() == 1)
       {
           playerRacer.finished = true;
           CompleteRaceMission(true, "Winner");
       }
       else
       {
           playerRacer.finished = true;
           CompleteRaceMission(false, "Eliminated");
       }
   }

   private RCCP_CarController GetRacerCarController(RaceRacer racer)
   {
       if (racer == null)
           return null;

       if (ReferenceEquals(racer, playerRacer))
           return CarController;

       if (racer.aiDriver != null && racer.aiDriver.CarController != null)
           return racer.aiDriver.CarController;

       if (racer.racerTransform != null)
           return racer.racerTransform.GetComponent<RCCP_CarController>();

       return null;
   }

   private int GetActiveRacerCount()
   {
       int activeCount = 0;

       for (int i = 0; i < allRacers.Length; i++)
       {
           RaceRacer racer = allRacers[i];

           if (racer == null || racer.eliminated)
               continue;

           activeCount++;
       }

       return activeCount;
   }

   private bool IsRaceMode()
   {
       return RaceType == RaceType.Racing || RaceType == RaceType.Elimination || RaceType == RaceType.NoBrakeChallenge;
   }

   private bool IsDriftScoringMode()
   {
       return RaceType == RaceType.DriftScore || RaceType == RaceType.TargetDrift || RaceType == RaceType.ComboMaster;
   }

   private void ApplyPlayerBrakeRestrictions()
   {
       if (RaceType != RaceType.NoBrakeChallenge || CarController == null || !raceStarted || playerRacer.finished)
           return;

       if (CarController.Inputs != null)
       {
           CarController.Inputs.brakeInput *= brakeEffectiveness;
           CarController.Inputs.handbrakeInput *= handbrakeEffectiveness;
       }

       CarController.brakeInput_P *= brakeEffectiveness;
       CarController.handbrakeInput_P *= handbrakeEffectiveness;
       CarController.brakeInput_V *= brakeEffectiveness;
       CarController.handbrakeInput_V *= handbrakeEffectiveness;
   }

   private int GetClosestWaypointIndex(Transform targetTransform)
   {
       if (targetTransform == null || runtimeRaceWaypoints == null || runtimeRaceWaypoints.waypoints == null || runtimeRaceWaypoints.waypoints.Count == 0)
           return 0;

       int closestIndex = 0;
       float closestDistance = float.MaxValue;

       for (int i = 0; i < runtimeRaceWaypoints.waypoints.Count; i++)
       {
           RCCP_Waypoint waypoint = runtimeRaceWaypoints.waypoints[i];

           if (waypoint == null)
               continue;

           float distance = Vector3.Distance(targetTransform.position, waypoint.transform.position);

           if (distance < closestDistance)
           {
               closestDistance = distance;
               closestIndex = i;
           }
       }

       return closestIndex;
   }
   
   private void OnCarCollision(RCCP_CarController car, Collision collision)
   {
       if (car != CarController)
           return;

       if (collision.relativeVelocity.magnitude < 2f)
           return;

       float impact = Mathf.InverseLerp(2f, 25f, collision.relativeVelocity.magnitude);
       GameHaptics.Pulse(Mathf.Lerp(.18f, .85f, impact), Mathf.Lerp(.3f, 1f, impact), Mathf.Lerp(.1f, .4f, impact));

       if (IsRaceMode())
           return;

       driftingNow = false;
       driftInterruptedByCollision = true;
       driftInterruptTimer = driftInterruptDuration;

       totalDriftTime = 0f;
       currentDriftComboTime = 0f;

       if (currentDriftPoints > 0)
       {
           currentDriftPoints = 0;
           currentDriftCoins = 0;
           currentMP = 1f;
       }
   }

   private void FixedUpdate() {

       if (IsRaceMode())
           return;

       float rearWheelSlipAmountForward = 0f;
       float rearWheelSlipAmountSideways = 0f;

       // Calculate wheel slip amounts for the rear wheels.
       if (CarController && CarController.RearAxle) {

           rearWheelSlipAmountForward = (CarController.RearAxle.leftWheelCollider.wheelSlipAmountForward + CarController.RearAxle.rightWheelCollider.wheelSlipAmountForward) * .5f;
           rearWheelSlipAmountSideways = (CarController.RearAxle.leftWheelCollider.wheelSlipAmountSideways + CarController.RearAxle.rightWheelCollider.wheelSlipAmountSideways) * .5f;

       }

       float pRearWheelSlipAmountForward = (rearWheelSlipAmountForward * rearWheelSlipAmountForward) * Mathf.Sign(rearWheelSlipAmountForward);
       float pRearWheelSlipAmountSideways = (rearWheelSlipAmountSideways * rearWheelSlipAmountSideways) * Mathf.Sign(rearWheelSlipAmountSideways);

       // Apply forces to simulate drifting behavior.
       CarController.Rigid.AddRelativeTorque(Vector3.up * CarController.steerInput_P * CarController.direction * .75f, ForceMode.Acceleration);

       // CarController.Rigid.AddForceAtPosition(transform.forward * 1500f * Mathf.Abs(pRearWheelSlipAmountSideways) * Mathf.Clamp01(Mathf.Abs(pRearWheelSlipAmountForward * 10f)) * CarController.direction, transform.position, ForceMode.Force);
       // CarController.Rigid.AddForceAtPosition(transform.right * 1250f * pRearWheelSlipAmountSideways * Mathf.Clamp01(Mathf.Abs(Mathf.Clamp(pRearWheelSlipAmountForward, .1f, 1f) * 10f)) * CarController.direction, transform.position, ForceMode.Force);

   }

   private void OnDestroy()
   {
       DisablePlayerRouteRespawnInput();
       ClearCheckpointVisuals();
       ClearSpawnedOpponents();
   }
   private void CheckGroundGap() {

       WheelCollider wheel = GetComponentInChildren<WheelCollider>();
       float distancePivotBetweenWheel = Vector3.Distance(new Vector3(0f, transform.position.y, 0f), new Vector3(0f, wheel.transform.position.y, 0f));

       RaycastHit hit;

       if (Physics.Raycast(wheel.transform.position, -Vector3.up, out hit, 10f))
           transform.position = new Vector3(transform.position.x, hit.point.y + distancePivotBetweenWheel + (wheel.radius) + (wheel.suspensionDistance / 2f), transform.position.z);

   }

   private void ApplyMission(MissionSO mission)
   {
       if (mission == null)
           return;

       RaceType = mission.raceType;

       if (mission.laps > 0)
           totalRaceLaps = mission.laps;

       if (mission.opponentCount >= 0)
           opponentCount = mission.opponentCount;

        if (mission.eliminationInterval > 0f)
            eliminationInterval = mission.eliminationInterval;

        brakeEffectiveness = Mathf.Clamp01(mission.brakeEffectiveness);
        handbrakeEffectiveness = Mathf.Clamp01(mission.handbrakeEffectiveness);

       if (mission.targetScore > 0)
           targetDriftScore = mission.targetScore;

       if (mission.timeLimit > 0)
           targetDriftTimeLimit = mission.timeLimit;

   }
}
