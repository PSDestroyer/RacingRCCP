using System;
using HalvaStudio.Save;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("Drifting Settings")]
    public bool driftingNow = false;
    public float totalDriftPoints = 0f;      //  Total drift points.
    public float currentDriftPoints = 0f;  
    [Space()]
    public float currentDriftCoins = 0f;        //  Current drift coins.
    public float totalDriftCoins = 0f;        //  Total drift coins.
    [Space()]
    public int currentMP = 1;       //  Current drift multiplier.
    [Space()]
    public float totalDriftTime = 0f;     //  Total drifting time.
    public float currentDriftComboTime = 0f;     //  Continuous drift time used for combo progression.
    public float totalDriftDistance = 0f;     //  Total drifting time.
    public bool canScore = true;        //  Can score now?
    private Vector3 lastPosition;
    public int driftPointsMP = 200;       //	Drift points multiplier.
    public int driftCoinsMP = 10;       //	Drift coins multiplier.
    public int maxDriftComboMultiplier = 5;
    public float comboStepDuration = 1.5f;
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
    public Image BronzeMedalImage;
    public Image SilverMedalImage;
    public Image GoldMedalImage;
    [Range(0f, 1f)] public float lockedMedalAlpha = 0.35f;
    [Range(0f, 1f)] public float unlockedMedalAlpha = 1f;

    [Header("Drift Targets")]
    public bool useCurrentMapDriftTarget = true;
    public float bronzeTargetScore = 5000f;
    public float silverTargetMultiplier = 1.5f;
    public float goldTargetMultiplier = 2f;

    private bool driftModeFinished = false;
    private float currentDriftDisplayedScore = 0f;
    //  When player achieved a score.
    // public delegate void onDriftScoreAchieved(BD_PlayerManager Player);
    // public static event onDriftScoreAchieved OnDriftScoreAchieved;
    private void Start()
    {
        InstancePlayer();
        SetUpRaceStyle(GetDrivingStyleIndex());

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
        if (RaceType != RaceType.DriftScore)
            return;

        driftModeFinished = false;
        currentMP = 1;
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
        RCCP_Settings.Instance.behaviorSelectedIndex = type;
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
       UpdateDriftUI();

       // If can control of the vehicle is disabled, return.
       if (!CarController.canControl) {

           driftingNow = false;
           totalDriftTime = 0f;
           currentDriftComboTime = 0f;
           currentDriftPoints = 0;
           currentDriftCoins = 0;
           currentMP = 1;
           driftInterruptedByCollision = false;
           driftInterruptTimer = 0f;

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

           totalDriftPoints += currentDriftPoints;
           totalDriftCoins += currentDriftCoins;
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
           currentMP = 1;
           currentDriftComboTime = 0f;

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

               if (raceStateText != null)
                   raceStateText.text = "Finish";
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
       currentDriftDisplayedScore = totalDriftPoints + currentDriftPoints;

       if (TotalScoreText != null)
           TotalScoreText.text = currentDriftDisplayedScore.ToString("N0");

       if (DriftComboText != null)
           DriftComboText.text = $"x{Mathf.Max(1, currentMP)}";

       if (DriftTargetText != null)
           DriftTargetText.text =
               $"Bronze  {GetBronzeTarget():N0}\n" +
               $"Silver  {GetSilverTarget():N0}\n" +
               $"Gold    {GetGoldTarget():N0}";

       if (DriftProgressSlider != null)
           DriftProgressSlider.value = Mathf.Clamp01(currentDriftDisplayedScore / Mathf.Max(1f, GetGoldTarget()));

       if (DriftMedalText != null)
           DriftMedalText.text = GetCurrentDriftMedalText();

       UpdateDriftMedalImages();
   }

   private void UpdateDriftMedalImages()
   {
       SetMedalImageState(BronzeMedalImage, currentDriftDisplayedScore >= GetBronzeTarget());
       SetMedalImageState(SilverMedalImage, currentDriftDisplayedScore >= GetSilverTarget());
       SetMedalImageState(GoldMedalImage, currentDriftDisplayedScore >= GetGoldTarget());
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
       if (RaceType != RaceType.DriftScore || driftModeFinished)
           return;

       if (currentDriftDisplayedScore >= GetGoldTarget())
       {
           driftModeFinished = true;
           canScore = false;

           if (raceStateText != null)
               raceStateText.text = "Finish";
       }
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

   private int GetCurrentDriftComboMultiplier()
   {
       if (comboStepDuration <= 0f)
           return 1;

       int combo = 1 + Mathf.FloorToInt(Mathf.Max(0f, currentDriftComboTime - driftTime) / comboStepDuration);
       return Mathf.Clamp(combo, 1, Mathf.Max(1, maxDriftComboMultiplier));
   }

   private float GetBronzeTarget()
   {
       if (useCurrentMapDriftTarget && GlobalCarData.thismap != null && GlobalCarData.thismap.target > 0)
           return GlobalCarData.thismap.target;

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
       if (currentDriftDisplayedScore >= GetGoldTarget())
           return "Gold";

       if (currentDriftDisplayedScore >= GetSilverTarget())
           return "Silver";

       if (currentDriftDisplayedScore >= GetBronzeTarget())
           return "Bronze";

       return "No Medal";
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
       if (allRacers == null || allRacers.Length == 0)
           return "1/1";

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

       return $"{position}/{GetActiveRacerCount()}";
   }

   private List<RaceRacer> GetRankedActiveRacers()
   {
       List<RaceRacer> rankedRacers = new List<RaceRacer>();

       for (int i = 0; i < allRacers.Length; i++)
       {
           RaceRacer racer = allRacers[i];

           if (racer == null || racer.eliminated)
               continue;

           rankedRacers.Add(racer);
       }

       rankedRacers.Sort((a, b) =>
       {
           if (ReferenceEquals(a, b))
               return 0;

           if (IsRacerAhead(a, b))
               return -1;

           if (IsRacerAhead(b, a))
               return 1;

           return 0;
       });

       return rankedRacers;
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
           SetRaceParticipantsControl(false);

           if (raceStateText != null)
               raceStateText.text = "Eliminated";
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

       playerRacer.finished = true;
       SetRaceParticipantsControl(false);

       if (!playerRacer.eliminated && GetActiveRacerCount() == 1)
       {
           if (raceStateText != null)
               raceStateText.text = "Winner";
       }
       else
       {
           if (raceStateText != null)
               raceStateText.text = "Eliminated";
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
           currentMP = 1;
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
