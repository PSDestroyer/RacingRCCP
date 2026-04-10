using System;
using HalvaStudio.Save;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayManager : MonoBehaviour
{
    [NonSerialized]public GameObject player;
    public RCCP_CarController CarController;
    public Transform SpawnPoint;
    public RaceType RaceType;
    
    
    
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
        SetUpRaceStyle(1);
        scoreText.gameObject.SetActive(false);
        DriftTimeSlider.value = totalDriftTime;
        
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
        RCCP_SceneManager.Instance.activePlayerVehicle = CarController;
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

   private void Update() {

       if (RaceType == RaceType.DriftScore)
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
                if (!scoreText.gameObject.activeSelf&&currentDriftCoins>1)
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
            if (scoreText.gameObject.activeSelf)
            {
                scoreText.gameObject.SetActive(false);
            }
            // if (OnDriftScoreAchieved != null)
                // OnDriftScoreAchieved(this);
                TotalScoreText.text = totalDriftPoints.ToString("N1");
            currentDriftPoints = 0;
            currentDriftCoins = 0;

        }
        lastPosition = transform.position;
 
       }
    
    }
   
   private void OnCarCollision(RCCP_CarController car, Collision collision)
   {
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
   private void CheckGroundGap() {

       WheelCollider wheel = GetComponentInChildren<WheelCollider>();
       float distancePivotBetweenWheel = Vector3.Distance(new Vector3(0f, transform.position.y, 0f), new Vector3(0f, wheel.transform.position.y, 0f));

       RaycastHit hit;

       if (Physics.Raycast(wheel.transform.position, -Vector3.up, out hit, 10f))
           transform.position = new Vector3(transform.position.x, hit.point.y + distancePivotBetweenWheel + (wheel.radius) + (wheel.suspensionDistance / 2f), transform.position.z);

   }
}
