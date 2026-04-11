using System;
using HalvaStudio.Save;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

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
    }

    [NonSerialized]public GameObject player;
    public RCCP_CarController CarController;
    public Transform SpawnPoint;
    public RaceType RaceType;

    [Header("Racing Settings")]
    public bool useCurrentMapModeSettings = true;
    public RCCP_AIWaypointsContainer raceWaypoints;
    public RaceRacer[] aiRacers;
    public int totalRaceLaps = 3;
    public float waypointReachDistance = 20f;
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
    public int expPerLevel = 10000;
    public int missionCompletionExp = 2500;
    public int firstPlaceExpBonus = 2500;
    public int secondPlaceExpBonus = 1500;
    public int thirdPlaceExpBonus = 750;
    public int participationExp = 500;
    public int levelUpMoneyReward = 1000;

    [Header("No Brake Challenge")]
    [Range(0f, 1f)] public float brakeEffectiveness = 0f;
    [Range(0f, 1f)] public float handbrakeEffectiveness = 0f;

    [Header("Race Start")]
    public bool useCountdown = true;
    public float countdownStepDuration = 1f;
    public float goTextDuration = 1f;

    [Header("Opponent Spawning")]
    public bool autoSpawnOpponents = true;
    public int opponentCount = 3;
    public bool usePlayerCarForOpponents = true;
    public Transform[] opponentSpawnPoints;
    public float spawnRowSpacing = 8f;
    public float spawnColumnSpacing = 4f;
    public int spawnCarsPerRow = 2;
    
    [Header("Checkpoint Visuals")]
    public GameObject checkpointPrefab;
    public Vector3 checkpointVisualOffset = new Vector3(0f, 2f, 0f);
    public bool showOnlyNextCheckpoint = true;

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
    private Coroutine expAnimationCoroutine;
    private Coroutine expRewardAnimationCoroutine;
    private Coroutine expSliderAnimationCoroutine;
    private Coroutine finishSummaryAnimationCoroutine;

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
        InstancePlayer();
        ApplyCurrentMapSettings();
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
    }

    private void OnDisable()
    {
        RCCP_Events.OnRCCPCollision -= OnCarCollision;
    }

    private void ApplyCurrentMapSettings()
    {
        if (GlobalCarData.thismap == null && SaveManager.Instance != null && SaveManager.Instance.saveData != null)
        {
            int missionMapId = SaveManager.Instance.saveData.currentMissionMapId >= 0
                ? SaveManager.Instance.saveData.currentMissionMapId
                : SaveManager.Instance.saveData.currentMap;

            GlobalCarData.thismap = GlobalCarData.GetMapById(missionMapId);
        }

        if (!useCurrentMapModeSettings || GlobalCarData.thismap == null)
            return;

        MapSO currentMap = GlobalCarData.thismap;

        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null && SaveManager.Instance.saveData.currentMissionRaceType >= 0)
            RaceType = (RaceType)SaveManager.Instance.saveData.currentMissionRaceType;
        else
            RaceType = currentMap.raceType;

        if (currentMap.raceLaps > 0)
            totalRaceLaps = currentMap.raceLaps;

        if (currentMap.opponentCount > 0)
            opponentCount = currentMap.opponentCount;

        if (currentMap.eliminationInterval > 0f)
            eliminationInterval = currentMap.eliminationInterval;

        brakeEffectiveness = Mathf.Clamp01(currentMap.brakeEffectiveness);
        handbrakeEffectiveness = Mathf.Clamp01(currentMap.handbrakeEffectiveness);

        if (currentMap.driftBronzeTarget > 0)
            bronzeTargetScore = currentMap.driftBronzeTarget;

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

        if (currentMap.targetDriftTimeLimit > 0)
            targetDriftTimeLimit = currentMap.targetDriftTimeLimit;
    }


    public void InstancePlayer()
    {
        player = Instantiate(Resources.Load<GameObject>(GlobalCarData._carlists[SaveManager.Instance.saveData.currentCar].carPrefabLocation), SpawnPoint);
        CarController = player.GetComponent<RCCP_CarController>();
        if (CarController != null)
            RCCP.RegisterPlayerVehicle(CarController);
    }

    public void SetUpRaceStyle(int type)
    {
        // 0  = Balanced
        // 1  = Drift
        // 2  = Race
        // 3  = Arcade
        // RCCP_Settings.Instance.behaviorSelectedIndex = type;
        RCCP_SceneManager.Instance.SetBehavior(type);

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

        if (raceWaypoints == null)
            raceWaypoints = FindFirstObjectByType<RCCP_AIWaypointsContainer>(FindObjectsInactive.Include);

        playerRacer = new RaceRacer
        {
            displayName = "Player",
            racerTransform = CarController != null ? CarController.transform : null,
            aiDriver = null,
            currentWaypointIndex = GetClosestWaypointIndex(CarController != null ? CarController.transform : null),
            completedLaps = 0,
            finished = false,
            eliminated = false
        };

        int aiCount = aiRacers != null ? aiRacers.Length : 0;
        allRacers = new RaceRacer[aiCount + 1];
        allRacers[0] = playerRacer;

        for (int i = 0; i < aiCount; i++)
        {
            RaceRacer aiRacer = aiRacers[i];

            if (aiRacer == null)
                continue;

            if (string.IsNullOrWhiteSpace(aiRacer.displayName))
                aiRacer.displayName = $"AI {i + 1}";

            if (aiRacer.aiDriver != null)
            {
                aiRacer.racerTransform = aiRacer.aiDriver.transform;
                aiRacer.aiDriver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints;

                if (raceWaypoints != null)
                    aiRacer.aiDriver.waypointsContainer = raceWaypoints;

                aiRacer.currentWaypointIndex = aiRacer.aiDriver.currentWaypointIndex;
            }
            else
            {
                aiRacer.currentWaypointIndex = GetClosestWaypointIndex(aiRacer.racerTransform);
            }

            aiRacer.completedLaps = 0;
            aiRacer.finished = false;
            aiRacer.eliminated = false;
            allRacers[i + 1] = aiRacer;
        }

        eliminationTimer = eliminationInterval;
        raceElapsedTime = 0f;
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

        if (useCountdown)
            StartCoroutine(RaceCountdownCoroutine());
        else
            StartRaceNow();
    }

    private IEnumerator RaceCountdownCoroutine()
    {
        string[] countdownTexts = { "3", "2", "1" };

        for (int i = 0; i < countdownTexts.Length; i++)
        {
            if (raceStateText != null)
                raceStateText.text = countdownTexts[i];

            yield return new WaitForSeconds(countdownStepDuration);
        }

        if (raceStateText != null)
            raceStateText.text = "GO";

        StartRaceNow();
        yield return new WaitForSeconds(goTextDuration);

        if (raceStateText != null && !playerRacer.finished)
            raceStateText.text = string.Empty;
    }

    private void StartRaceNow()
    {
        raceStarted = true;
        eliminationTimer = eliminationInterval;
        SetRaceParticipantsControl(true);
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

            RCCP_AI aiDriver = opponentObject.GetComponent<RCCP_AI>();

            if (aiDriver == null)
                aiDriver = opponentObject.AddComponent<RCCP_AI>();

            aiDriver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints;

            if (raceWaypoints != null)
                aiDriver.waypointsContainer = raceWaypoints;

            aiRacers[i] = new RaceRacer
            {
                displayName = $"AI {i + 1}",
                racerTransform = opponentObject.transform,
                aiDriver = aiDriver,
                currentWaypointIndex = 0,
                completedLaps = 0,
                finished = false
            };
        }

        if (hasSceneManager)
            RCCP_SceneManager.Instance.registerLastVehicleAsPlayer = previousRegisterLastVehicleAsPlayer;

        if (CarController != null)
            RCCP.RegisterPlayerVehicle(CarController);
    }

    private GameObject SpawnOpponentVehicle(int opponentIndex, Transform spawnTransform)
    {
        if (spawnTransform == null)
            return null;

        GameObject prefabToSpawn = GetOpponentPrefab(opponentIndex);

        if (prefabToSpawn == null)
            return null;

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
        return Resources.Load<GameObject>(prefabLocation);
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
    }

   private void Update() {

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

   private void UpdateDriftMode()
   {
       if (!missionResultsShown)
           driftElapsedTime += Time.deltaTime;

       UpdateDriftUI();

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
       if (CarController == null || raceWaypoints == null || raceWaypoints.waypoints == null || raceWaypoints.waypoints.Count == 0)
           return;

       if (!raceStarted)
       {
           UpdateCheckpointVisuals();
           return;
       }

       if (!missionResultsShown)
           raceElapsedTime += Time.deltaTime;

       ApplyPlayerBrakeRestrictions();
       UpdatePlayerRaceProgress();
       UpdateAIRaceProgress();
       UpdateEliminationMode();
       UpdateCheckpointVisuals();
       UpdateRaceUI();
   }

   private void UpdatePlayerRaceProgress()
   {
       if (playerRacer.eliminated)
           return;

       playerRacer.racerTransform = CarController.transform;

       if (playerRacer.finished)
           return;

       int waypointCount = raceWaypoints.waypoints.Count;
       Transform targetWaypoint = raceWaypoints.waypoints[playerRacer.currentWaypointIndex].transform;
       playerRacer.distanceToNextWaypoint = Vector3.Distance(playerRacer.racerTransform.position, targetWaypoint.position);

       if (playerRacer.distanceToNextWaypoint > waypointReachDistance)
           return;

       playerRacer.currentWaypointIndex++;

       if (playerRacer.currentWaypointIndex >= waypointCount)
       {
           playerRacer.currentWaypointIndex = 0;
           playerRacer.completedLaps++;

           if ((RaceType == RaceType.Racing || RaceType == RaceType.NoBrakeChallenge) && playerRacer.completedLaps >= totalRaceLaps)
           {
               playerRacer.finished = true;
               CompleteRaceMission(true, "Finish");
           }
       }
   }

   private void UpdateAIRaceProgress()
   {
       if (aiRacers == null)
           return;

       int waypointCount = raceWaypoints.waypoints.Count;

       for (int i = 0; i < aiRacers.Length; i++)
       {
           RaceRacer aiRacer = aiRacers[i];

           if (aiRacer == null || aiRacer.racerTransform == null || aiRacer.finished || aiRacer.eliminated)
               continue;

           if (aiRacer.aiDriver != null)
           {
               int previousIndex = aiRacer.currentWaypointIndex;
               aiRacer.currentWaypointIndex = aiRacer.aiDriver.currentWaypointIndex;

               if (previousIndex > aiRacer.currentWaypointIndex)
               {
                   aiRacer.completedLaps++;

                   if ((RaceType == RaceType.Racing || RaceType == RaceType.NoBrakeChallenge) && aiRacer.completedLaps >= totalRaceLaps)
                       aiRacer.finished = true;
               }
           }

           int safeWaypointIndex = Mathf.Clamp(aiRacer.currentWaypointIndex, 0, waypointCount - 1);
           aiRacer.distanceToNextWaypoint = Vector3.Distance(
               aiRacer.racerTransform.position,
               raceWaypoints.waypoints[safeWaypointIndex].transform.position);
       }
   }

   private void UpdateRaceUI()
   {
       if (currentLapText != null)
       {
           if (RaceType == RaceType.Elimination)
               currentLapText.text = $"Survivors {GetActiveRacerCount()}/{allRacers.Length}";
           else if (RaceType == RaceType.NoBrakeChallenge)
           {
               int shownLap = Mathf.Min(playerRacer.completedLaps + 1, totalRaceLaps);
               currentLapText.text = $"No Brake  Lap {shownLap}/{totalRaceLaps}";
           }
           else
           {
               int shownLap = Mathf.Min(playerRacer.completedLaps + 1, totalRaceLaps);
               currentLapText.text = $"Lap {shownLap}/{totalRaceLaps}";
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

       if (useCurrentMapDriftTarget && GlobalCarData.thismap != null)
       {
           if (GlobalCarData.thismap.driftBronzeTarget > 0)
               return GlobalCarData.thismap.driftBronzeTarget;
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
       if (useCurrentMapTargetDriftSettings && GlobalCarData.thismap != null)
       {
           if (GlobalCarData.thismap.targetDriftScore > 0)
               return GlobalCarData.thismap.targetDriftScore;
       }

       return targetDriftScore;
   }

   private float GetTargetDriftTimeLimit()
   {
       if (useCurrentMapTargetDriftSettings && GlobalCarData.thismap != null)
       {
           if (GlobalCarData.thismap.targetDriftTimeLimit > 0)
               return GlobalCarData.thismap.targetDriftTimeLimit;
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
       SetRaceParticipantsControl(false);

       if (raceStateText != null)
           raceStateText.text = stateText;

       ApplyMissionRewards();
       ShowFinishSummaryScreen();
   }

   private void CompleteDriftMission(bool success, string stateText)
   {
       if (missionResultsShown)
           return;

        CommitPendingDriftScore();

       missionSucceeded = success;
       missionResultsShown = true;
       driftModeFinished = true;
       canScore = false;

       if (CarController != null)
           CarController.canControl = false;

       if (scoreText != null && scoreText.gameObject.activeSelf)
           scoreText.gameObject.SetActive(false);

       if (raceStateText != null)
           raceStateText.text = stateText;

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

       SaveMissionResultSnapshot();
       SaveManager.Instance.saveData.exp = missionFinalExpTotal;
       SaveManager.Instance.saveData.currentLevel = missionFinalLevel;
       AddMoneyToPlayer(missionRewardEarned + missionLevelRewardEarned);
       SaveManager.Instance.Save();
   }

   private void CommitPendingDriftScore()
   {
       if (!IsDriftScoringMode())
           return;

       if (RaceType == RaceType.DriftScore || RaceType == RaceType.TargetDrift)
       {
           if (currentDriftPoints > 0f)
               totalDriftPoints += currentDriftPoints;

           if (currentDriftCoins > 0f)
               totalDriftCoins += currentDriftCoins;
       }

       currentDriftPoints = 0f;
       currentDriftCoins = 0f;
       totalDriftTime = 0f;
       currentDriftComboTime = 0f;
       currentMP = RaceType == RaceType.ComboMaster ? currentMP : 1f;
       lastDisplayedComboMultiplier = RaceType == RaceType.ComboMaster ? currentMP : 0f;
       UpdateDriftUI();
   }

   private void SaveMissionResultSnapshot()
   {
       if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
           return;

       SaveManager.SaveData saveData = SaveManager.Instance.saveData;

       saveData.currentRaceTime = Mathf.Max(0, Mathf.RoundToInt(GetMissionElapsedTime()));
       saveData.currentRaceLap = IsRaceMode() ? Mathf.Max(0, playerRacer.completedLaps) : 0;
       saveData.currentRaceTarget = IsRaceMode()
           ? Mathf.Max(1, GetPlayerRacePosition())
           : Mathf.Max(0, Mathf.RoundToInt(currentDriftDisplayedScore));
       saveData.currentRacePay = Mathf.Max(0, missionRewardEarned + missionLevelRewardEarned);
   }

   private void ShowFinishSummaryScreen()
   {
       if (finishSummaryScreen != null)
           finishSummaryScreen.SetActive(true);

       if (finishExpScreen != null)
           finishExpScreen.SetActive(false);

       if (finishTitleText != null)
           finishTitleText.text = missionSucceeded ? "Mission Complete" : "Mission Failed";

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
           finishExpContinueButton.SetActive(true);

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
           finishSummaryContinueButton.SetActive(true);

       finishSummaryAnimationCoroutine = null;
   }

   private void UpdateExpScreenUI(int displayedLevel, int displayedTotalExp, int displayedGainedExp, int displayedLevelUps, int displayedLevelProgress, int expRequirement)
   {
       if (expLevelText != null)
           expLevelText.text = $"Level {displayedLevel}";

       if (expTotalText != null)
           expTotalText.text = $"{displayedTotalExp:N0} EXP";

       if (expGainText != null)
           expGainText.text = $"+{displayedGainedExp:N0} EXP";

       if (expLevelRewardText != null)
       {
           int displayedLevelReward = displayedLevelUps * levelUpMoneyReward;
           expLevelRewardText.gameObject.SetActive(displayedLevelReward > 0);

           if (displayedLevelReward > 0)
               expLevelRewardText.text = $"Level Up Reward  +{displayedLevelReward:N0}<sprite index=0>";
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
           finishRewardText.text = $"{displayedValue:N0}<sprite index=0>";
           yield return null;
       }

       finishRewardText.text = $"{missionRewardEarned:N0}<sprite index=0>";
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
           finishTimeText.text = FormatRaceTime(displayedTime);
           yield return null;
       }

       finishTimeText.text = FormatRaceTime(missionTime);
   }

   private string GetAnimatedFinishPrimaryStatText(float normalizedTime)
   {
       float clampedTime = Mathf.Clamp01(normalizedTime);

       if (IsRaceMode())
       {
           int displayedPosition = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, GetPlayerRacePosition(), clampedTime)));
           return $"Position  {displayedPosition}/{GetTotalRaceParticipantCount()}";
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
       if (GlobalCarData.thismap == null)
           return 0;

       return Mathf.Max(0, GlobalCarData.thismap.price);
   }

   private int GetMissionExpReward(int playerPosition, bool success)
   {
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
           return $"Position  {GetPlayerRacePosition()}/{GetTotalRaceParticipantCount()}";

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
               return "Classic Race";
           case RaceType.Elimination:
               return "Elimination";
           case RaceType.NoBrakeChallenge:
               return "No Brake Challenge";
           case RaceType.DriftScore:
               return "Drift Score";
           case RaceType.TargetDrift:
               return "Target Drift";
           case RaceType.ComboMaster:
               return "Combo Master";
           case RaceType.FreeDrift:
               return "Free Drift";
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

       List<string> leaderboardLines = new List<string>(rankedRacers.Count);

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

       if (checkpointPrefab == null || raceWaypoints == null || raceWaypoints.waypoints == null)
           return;

       checkpointVisuals = new GameObject[raceWaypoints.waypoints.Count];

       for (int i = 0; i < raceWaypoints.waypoints.Count; i++)
       {
           RCCP_Waypoint waypoint = raceWaypoints.waypoints[i];

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

       if (playerRacer.eliminated)
           return true;

       if (otherRacer.completedLaps != playerRacer.completedLaps)
           return otherRacer.completedLaps > playerRacer.completedLaps;

       if (otherRacer.currentWaypointIndex != playerRacer.currentWaypointIndex)
           return otherRacer.currentWaypointIndex > playerRacer.currentWaypointIndex;

       return otherRacer.distanceToNextWaypoint < playerRacer.distanceToNextWaypoint;
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

       if (racerA.completedLaps != racerB.completedLaps)
           return racerA.completedLaps > racerB.completedLaps;

       if (racerA.currentWaypointIndex != racerB.currentWaypointIndex)
           return racerA.currentWaypointIndex > racerB.currentWaypointIndex;

       return racerA.distanceToNextWaypoint < racerB.distanceToNextWaypoint;
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
           raceStateText.text = $"{racer.displayName} OUT";
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
       if (targetTransform == null || raceWaypoints == null || raceWaypoints.waypoints == null || raceWaypoints.waypoints.Count == 0)
           return 0;

       int closestIndex = 0;
       float closestDistance = float.MaxValue;

       for (int i = 0; i < raceWaypoints.waypoints.Count; i++)
       {
           RCCP_Waypoint waypoint = raceWaypoints.waypoints[i];

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
       if (IsRaceMode())
           return;

       if (car != CarController)
           return;

       if (collision.relativeVelocity.magnitude < 2f)
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
}
