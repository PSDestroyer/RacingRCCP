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
    public float totalDriftDistance = 0f;     //  Total drifting time.
    public bool canScore = true;        //  Can score now?
    private Vector3 lastPosition;
    public int driftPointsMP = 200;       //	Drift points multiplier.
    public int driftCoinsMP = 10;       //	Drift coins multiplier.
    public float driftTime = 1f;        //	Timer for resetting the drift.
    public float driftSpeed = 25f;        //	Speed limit for drift score.
    public bool resetDriftPointsAfterCollision = true;      //	Resets current drift score on collisions.
    public float minimumCollision = 5f;     //	Minimum collision limit for resetting the drift score.
    private bool driftInterruptedByCollision = false;
    private float driftInterruptTimer = 0f;
    [SerializeField] private float driftInterruptDuration = 0.5f;

    [Header("Drifting UI")] 
    public Slider DriftTimeSlider;
    public TMP_Text scoreText;
    public TMP_Text TotalScoreText;
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

        if (RaceType == RaceType.Racing && autoSpawnOpponents)
            SpawnAutomaticOpponents();

        InitializeRaceMode();

        if (RaceType == RaceType.Racing)
            BeginRaceStartFlow();
        
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
            case RaceType.FreeDrift:
            case RaceType.DriftScore:
                return 1;
            default:
                return 0;
        }
    }

    private void InitializeRaceMode()
    {
        if (RaceType != RaceType.Racing)
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
            finished = false
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
            allRacers[i + 1] = aiRacer;
        }

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
           case RaceType.FreeDrift:
               UpdateDriftMode();
               break;

           case RaceType.Racing:
               UpdateRaceMode();
               break;
       }

    }

   private void UpdateDriftMode()
   {
       // If can control of the vehicle is disabled, return.
       if (!CarController.canControl) {

           driftingNow = false;
           totalDriftTime = 0f;
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

           //  If drifting time is high enough, increase the score.
           if (totalDriftTime >= driftTime) {
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

       }
       lastPosition = transform.position;
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

       UpdatePlayerRaceProgress();
       UpdateAIRaceProgress();
       UpdateCheckpointVisuals();
       UpdateRaceUI();
   }

   private void UpdatePlayerRaceProgress()
   {
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

           if (playerRacer.completedLaps >= totalRaceLaps)
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

           if (aiRacer == null || aiRacer.racerTransform == null || aiRacer.finished)
               continue;

           if (aiRacer.aiDriver != null)
           {
               int previousIndex = aiRacer.currentWaypointIndex;
               aiRacer.currentWaypointIndex = aiRacer.aiDriver.currentWaypointIndex;

               if (previousIndex > aiRacer.currentWaypointIndex)
               {
                   aiRacer.completedLaps++;

                   if (aiRacer.completedLaps >= totalRaceLaps)
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
           int shownLap = Mathf.Min(playerRacer.completedLaps + 1, totalRaceLaps);
           currentLapText.text = $"Lap {shownLap}/{totalRaceLaps}";
       }

       if (racePositionText != null)
           racePositionText.text = GetPlayerRacePositionText();
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

           if (IsRacerAheadOfPlayer(racer))
               position++;
       }

       return $"{position}/{allRacers.Length}";
   }

   private bool IsRacerAheadOfPlayer(RaceRacer otherRacer)
   {
       if (otherRacer.completedLaps != playerRacer.completedLaps)
           return otherRacer.completedLaps > playerRacer.completedLaps;

       if (otherRacer.currentWaypointIndex != playerRacer.currentWaypointIndex)
           return otherRacer.currentWaypointIndex > playerRacer.currentWaypointIndex;

       return otherRacer.distanceToNextWaypoint < playerRacer.distanceToNextWaypoint;
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
       if (RaceType == RaceType.Racing)
           return;

       if (car != CarController)
           return;

       if (collision.relativeVelocity.magnitude < 2f)
           return;

       driftingNow = false;
       driftInterruptedByCollision = true;
       driftInterruptTimer = driftInterruptDuration;

       totalDriftTime = 0f;

       if (currentDriftPoints > 0)
       {
           currentDriftPoints = 0;
           currentDriftCoins = 0;
           currentMP = 1;
       }
   }

   private void FixedUpdate() {

       if (RaceType == RaceType.Racing)
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
