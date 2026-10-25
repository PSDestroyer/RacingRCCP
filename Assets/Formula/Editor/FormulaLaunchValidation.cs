using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only launch diagnosis. Each treatment changes one factor on a runtime
// instance. No scene, prefab, player save or production tuning is modified.
[InitializeOnLoad]
public static class FormulaLaunchValidation
{
    private const string Key = "Formula.LaunchValidation";
    private const string Output = "Docs/AI/FormulaHandling/LaunchDiagnosis.json";
    private static readonly string[] Profiles = { "keyboard_stock", "raw_zero_steer", "counter_steer_off", "esp_off", "direct_differential", "front_x_centered", "front_z_aligned", "symmetric_front_geometry", "mirrored_front_offset" };
    private static readonly string[] ValidationProfiles = { "keyboard_stock", "raw_zero_steer", "opposite_heading", "orthogonal_heading", "legacy_front_offset", "mirrored_front_offset" };
    private static readonly string[] ExtendedProfiles = { "flat_keyboard_stock", "flat_raw_stock", "flat_tcs_off", "flat_direct_differential", "banked_stock", "banked_tcs_off", "banked_direct_differential", "banked_half_tcs" };
    private static readonly string[] ClearanceProfiles = { "flat_stock", "flat_body_colliders_off", "flat_authored_com", "flat_lower_wheels_08_raise_body", "banked_stock", "banked_body_colliders_off", "banked_authored_com", "banked_lower_wheels_08_raise_body" };
    private static readonly string[] StabilityProfiles = { "flat_stock", "flat_force_point_02", "flat_damper_4500", "flat_antiroll_off", "banked_stock", "banked_force_point_02", "banked_damper_4500", "banked_antiroll_off" };
    private static readonly string[] SuspensionBalanceProfiles = { "flat_stock", "flat_damper_3500", "flat_damper_4500_force_point_02", "flat_damper_4500_authored_com", "banked_stock", "banked_damper_3500", "banked_damper_4500_force_point_02", "banked_damper_4500_authored_com" };
    private static readonly string[] CalibrationProfiles = { "flat_stock", "flat_calibrated_4500_level0", "flat_calibrated_3500_level0", "flat_calibrated_4500_level3", "banked_stock", "banked_calibrated_4500_level0", "banked_calibrated_3500_level0", "banked_calibrated_4500_level3" };
    private static string[] profiles;
    private static readonly List<object> samples = new List<object>();
    private static readonly List<string> errors = new List<string>();
    private static RCCP_CarController[] cars;
    private static Vector3[] origins;
    private static Quaternion[] rotations;
    private static Keyboard keyboard;
    private static int phase;
    private static float began, nextSample;
    private static double started;
    private static string status;
    private static float[] maxLateral, maxRawSteer;
    private static float[] maxProcessedSteer, maxRearTorqueDifference, maxHeading, maxSpeed;
    private static int[] minimumGrounded;
    private static Vector3[] roadSurfacePoints, roadNormals;
    private static float[] settledRootRoadHeight, settledBodyClearance, settledRootY, settledBodyWorldLowestY;

    static FormulaLaunchValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Tools/Formula Career/Diagnose Straight Launch")]
    public static void Run() => Start(false);

    [MenuItem("Tools/Formula Career/Validate Straight Launch")]
    public static void Validate() => Start(true);

    [MenuItem("Tools/Formula Career/Validate Straight Grid Launch")]
    public static void Grid() => Start(true, true);

    [MenuItem("Tools/Formula Career/Diagnose Extended Straight Launch")]
    public static void Extended() => Start(false, false, true);

    [MenuItem("Tools/Formula Career/Diagnose Formula Ground Clearance")]
    public static void GroundClearance() => Start(false, false, true, true);

    [MenuItem("Tools/Formula Career/Diagnose Formula Launch Stability")]
    public static void Stability() => Start(false, false, true, false, true);

    [MenuItem("Tools/Formula Career/Diagnose Formula Suspension Balance")]
    public static void SuspensionBalance() => Start(false, false, true, false, false, true);

    [MenuItem("Tools/Formula Career/Compare Formula Launch Calibration")]
    public static void Calibration() => Start(false, false, true, false, false, false, true);

    private static void Start(bool validation, bool grid = false, bool extended = false, bool clearance = false, bool stability = false, bool suspensionBalance = false, bool calibration = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetBool(Key + ".Validation", validation);
        SessionState.SetBool(Key + ".Grid", grid);
        SessionState.SetBool(Key + ".Extended", extended);
        SessionState.SetBool(Key + ".Clearance", clearance);
        SessionState.SetBool(Key + ".Stability", stability);
        SessionState.SetBool(Key + ".SuspensionBalance", suspensionBalance);
        SessionState.SetBool(Key + ".Calibration", calibration);
        SessionState.SetString(Key + ".PreviousSavePath", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaLaunch-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(grid ? "Assets/Scenes/Menu.unity" : "Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = 0; samples.Clear(); errors.Clear(); cars = null; status = "running";
            profiles = SessionState.GetBool(Key + ".Calibration", false) ? CalibrationProfiles : SessionState.GetBool(Key + ".SuspensionBalance", false) ? SuspensionBalanceProfiles : SessionState.GetBool(Key + ".Stability", false) ? StabilityProfiles : SessionState.GetBool(Key + ".Clearance", false) ? ClearanceProfiles : SessionState.GetBool(Key + ".Extended", false) ? ExtendedProfiles : SessionState.GetBool(Key + ".Grid", false) ? new[] { "grid_keyboard_stock" } : SessionState.GetBool(Key + ".Validation", false) ? ValidationProfiles : Profiles;
            started = EditorApplication.timeSinceStartup; Application.runInBackground = true; Time.timeScale = 1;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            string previousSave = SessionState.GetString(Key + ".PreviousSavePath", "");
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(previousSave) ? null : previousSave);
            string previous = SessionState.GetString(Key + ".Previous", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            SessionState.SetBool(Key, false);
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Launch diagnosis timed out.");
            if (phase == 0)
            {
                if (SessionState.GetBool(Key + ".Grid", false))
                {
                    if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null) return;
                    string error = FormulaSession.Launch("g01", "career");
                    if (error != null) throw new Exception(error);
                    phase = 10; return;
                }
                foreach (var c in UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
                foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) c.enabled = false;
                bool extended = SessionState.GetBool(Key + ".Extended", false);
                if (!extended)
                {
                    var road = new GameObject("Launch diagnosis flat road");
                    road.transform.position = new Vector3(0, -.05f, 0);
                    road.AddComponent<BoxCollider>().size = new Vector3(3000, .1f, 3000);
                }
                cars = new RCCP_CarController[profiles.Length]; origins = new Vector3[cars.Length]; rotations = new Quaternion[cars.Length];
                maxLateral = new float[cars.Length]; maxRawSteer = new float[cars.Length];
                if (extended)
                {
                    maxProcessedSteer = new float[cars.Length]; maxRearTorqueDifference = new float[cars.Length];
                    maxHeading = new float[cars.Length]; maxSpeed = new float[cars.Length];
                    minimumGrounded = Enumerable.Repeat(4, cars.Length).ToArray();
                    roadSurfacePoints = new Vector3[cars.Length]; roadNormals = new Vector3[cars.Length];
                    settledRootRoadHeight = new float[cars.Length]; settledBodyClearance = new float[cars.Length];
                    settledRootY = new float[cars.Length]; settledBodyWorldLowestY = new float[cars.Length];
                }
                for (int i = 0; i < cars.Length; i++)
                {
                    float yaw = SessionState.GetBool(Key + ".Validation", false) ? i == 2 ? 180 : i == 3 ? 90 : 0 : 0;
                    Vector3 position = new Vector3(i * 30, .2f, 0);
                    Quaternion rotation = Quaternion.Euler(0, yaw, 0);
                    if (extended)
                    {
                        // Separate lanes keep the level control surface out from under
                        // the banked cars. Positive roll matches G01's observed road normal.
                        var road = new GameObject("Extended launch road " + profiles[i]);
                        bool wideLane = SessionState.GetBool(Key + ".Clearance", false) || SessionState.GetBool(Key + ".Stability", false) || SessionState.GetBool(Key + ".SuspensionBalance", false) || SessionState.GetBool(Key + ".Calibration", false);
                        road.transform.SetPositionAndRotation(new Vector3((i - (cars.Length - 1) * .5f) * (wideLane ? 400 : 120), -.05f, 0), Quaternion.Euler(0, 0, i >= 4 ? 1.6f : 0));
                        road.AddComponent<BoxCollider>().size = new Vector3(wideLane ? 300 : 90, .1f, 3000);
                        position = road.transform.TransformPoint(new Vector3(0, .25f, 0));
                        rotation = road.transform.rotation;
                        roadSurfacePoints[i] = road.transform.TransformPoint(Vector3.up * .05f);
                        roadNormals[i] = road.transform.up;
                    }
                    cars[i] = UnityEngine.Object.Instantiate(FormulaAssets.Current.vehiclePrefab, position, rotation).GetComponent<RCCP_CarController>();
                    cars[i].canControl = true;
                    cars[i].Inputs.OverrideInputs(new RCCP_Inputs());
                }
                keyboard = InputSystem.AddDevice<Keyboard>("Formula straight launch diagnosis");
                began = Time.time; phase = 1;
            }
            else if (phase == 10)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.CarController == null || !game.FormulaRaceStarted || !game.CarController.GetComponent<FormulaVehicle>().IsReady) return;
                cars = new[] { game.CarController }; origins = new[] { cars[0].transform.position }; rotations = new[] { cars[0].transform.rotation };
                maxLateral = new float[1]; maxRawSteer = new float[1];
                keyboard = InputSystem.AddDevice<Keyboard>("Formula grid launch acceptance");
                cars[0].Inputs.DisableOverrideInputs();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
                began = Time.time; nextSample = 0; phase = 3;
            }
            else if (phase == 1 && Time.time - began > 2 && cars.All(c => c.GetComponent<FormulaVehicle>().IsReady))
            {
                for (int i = 0; i < cars.Length; i++)
                {
                    var car = cars[i]; car.GetComponent<FormulaVehicle>().ApplyDevelopment(0, 0, 0, 0, "balanced");
                    car.Stability.ABS = car.Stability.TCS = car.Stability.ESP = true;
                    if (SessionState.GetBool(Key + ".Extended", false))
                    {
                        if (SessionState.GetBool(Key + ".Calibration", false))
                        {
                            if (i % 4 == 0)
                            {
                                // Retain the original failure as a control even after the
                                // production prefab has adopted the selected calibration.
                                foreach (var wheel in car.AllWheelColliders)
                                {
                                    var spring = wheel.WheelCollider.suspensionSpring;
                                    spring.damper = 8500;
                                    wheel.WheelCollider.suspensionSpring = spring;
                                    wheel.WheelCollider.forceAppPointDistance = .05f;
                                }
                                Vector3 com = new Vector3(0, .3150252f, 0);
                                car.Rigid.centerOfMass = com;
                                car.AeroDynamics.COM.position = car.transform.TransformPoint(com);
                            }
                            else
                            {
                                int level = i % 4 == 3 ? 3 : 0;
                                car.GetComponent<FormulaVehicle>().ApplyDevelopment(level, level, level, level, "balanced");
                                // Acceptance cars use saved production values and normal
                                // progression only. The 3500 alternative remains diagnostic.
                                foreach (var wheel in car.AllWheelColliders)
                                {
                                    var spring = wheel.WheelCollider.suspensionSpring;
                                    if (!Mathf.Approximately(spring.damper, 4500f * (1f + .035f * level)) || !Mathf.Approximately(wheel.WheelCollider.forceAppPointDistance, .2f))
                                        errors.Add("Production suspension calibration missing on " + profiles[i] + "/" + wheel.name);
                                    if (i % 4 == 2)
                                    {
                                        spring.damper = 3500;
                                        wheel.WheelCollider.suspensionSpring = spring;
                                    }
                                }
                                if (Vector3.Distance(car.Rigid.centerOfMass, new Vector3(0, .25f, -.15f)) > .001f ||
                                    Vector3.Distance(car.transform.InverseTransformPoint(car.AeroDynamics.COM.position), new Vector3(0, .25f, -.15f)) > .001f)
                                    errors.Add("Production COM calibration missing on " + profiles[i]);
                            }
                        }
                        else if (SessionState.GetBool(Key + ".SuspensionBalance", false))
                        {
                            if (i % 4 != 0)
                                foreach (var wheel in car.AllWheelColliders)
                                {
                                    var spring = wheel.WheelCollider.suspensionSpring;
                                    spring.damper = i % 4 == 1 ? 3500 : 4500;
                                    wheel.WheelCollider.suspensionSpring = spring;
                                    if (i % 4 == 2) wheel.WheelCollider.forceAppPointDistance = .2f;
                                }
                            if (i % 4 == 3)
                            {
                                Vector3 com = new Vector3(0, .25f, -.15f);
                                car.Rigid.centerOfMass = com;
                                car.AeroDynamics.COM.position = car.transform.TransformPoint(com);
                            }
                        }
                        else if (SessionState.GetBool(Key + ".Stability", false))
                        {
                            switch (i % 4)
                            {
                                case 1:
                                    foreach (var wheel in car.AllWheelColliders) wheel.WheelCollider.forceAppPointDistance = .2f;
                                    break;
                                case 2:
                                    foreach (var wheel in car.AllWheelColliders)
                                    {
                                        var spring = wheel.WheelCollider.suspensionSpring;
                                        spring.damper = 4500;
                                        wheel.WheelCollider.suspensionSpring = spring;
                                    }
                                    break;
                                case 3:
                                    car.FrontAxle.antirollForce = car.RearAxle.antirollForce = 0;
                                    break;
                            }
                        }
                        else if (SessionState.GetBool(Key + ".Clearance", false))
                        {
                            switch (i % 4)
                            {
                                case 1:
                                    foreach (var bodyCollider in car.GetComponentsInChildren<BoxCollider>()) bodyCollider.enabled = false;
                                    break;
                                case 2:
                                    Vector3 com = new Vector3(0, .25f, -.15f);
                                    car.Rigid.centerOfMass = com;
                                    car.AeroDynamics.COM.position = car.transform.TransformPoint(com);
                                    break;
                                case 3:
                                    // Lower the wheels relative to the body, so its settled
                                    // height rises. Raising wheel centres would lower the body.
                                    foreach (var wheel in car.AllWheelColliders) wheel.transform.localPosition += Vector3.down * .08f;
                                    break;
                            }
                        }
                        else
                        {
                            if (i == 2 || i == 5) car.Stability.TCS = false;
                            if (i == 3 || i == 6) car.GetComponentInChildren<RCCP_Differential>().differentialType = RCCP_Differential.DifferentialType.Direct;
                            if (i == 7) car.Stability.TCSIntensity *= .5f;
                        }
                        continue;
                    }
                    bool validation = SessionState.GetBool(Key + ".Validation", false);
                    if (!validation && i == 2) car.Inputs.counterSteering = false;
                    if (!validation && i == 3) car.Stability.ESP = false;
                    if (!validation && i == 4) car.GetComponentInChildren<RCCP_Differential>().differentialType = RCCP_Differential.DifferentialType.Direct;
                    if (!validation && i >= 5)
                    {
                        var left = car.FrontAxle.leftWheelCollider.transform; var right = car.FrontAxle.rightWheelCollider.transform;
                        Vector3 l = left.localPosition, r = right.localPosition;
                        float half = (r.x - l.x) * .5f, z = (l.z + r.z) * .5f;
                        if (i == 5 || i == 7) { l.x = -half; r.x = half; }
                        if (i == 6 || i == 7) { l.z = z; r.z = z; }
                        if (i == 8) { float shift = -(l.x + r.x); l.x += shift; r.x += shift; }
                        left.localPosition = l; right.localPosition = r;
                    }
                    if (validation && i >= 4)
                    {
                        // Reintroduce only the measured source-model lateral bias.
                        // Its mirror must reverse the pull; this is the control fixture.
                        float offset = i == 4 ? -.00172418f : .00172418f;
                        car.FrontAxle.leftWheelCollider.transform.localPosition += Vector3.right * offset;
                        car.FrontAxle.rightWheelCollider.transform.localPosition += Vector3.right * offset;
                    }
                }
                began = Time.time; phase = 2;
            }
            else if (phase == 2 && Time.time - began > 1)
            {
                for (int i = 0; i < cars.Length; i++)
                {
                    origins[i] = cars[i].transform.position; rotations[i] = cars[i].transform.rotation;
                    if (SessionState.GetBool(Key + ".Extended", false))
                    {
                        settledRootRoadHeight[i] = Vector3.Dot(origins[i] - roadSurfacePoints[i], roadNormals[i]);
                        settledBodyClearance[i] = BodyClearance(cars[i], roadSurfacePoints[i], roadNormals[i]);
                        settledRootY[i] = origins[i].y;
                        settledBodyWorldLowestY[i] = BodyClearance(cars[i], Vector3.zero, Vector3.up);
                    }
                    if (i == 0) cars[i].Inputs.DisableOverrideInputs();
                    else cars[i].Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = 1, steerInput = 0 });
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
                began = Time.time; nextSample = 0; phase = 3;
            }
            else if (phase == 3)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
                float age = Time.time - began;
                if (age >= nextSample)
                {
                    bool extended = SessionState.GetBool(Key + ".Extended", false);
                    nextSample = age + (extended ? .2f : .1f);
                    for (int i = 0; i < cars.Length; i++)
                    {
                        var c = cars[i]; Vector3 delta = Quaternion.Inverse(rotations[i]) * (c.transform.position - origins[i]);
                        var diff = c.GetComponentInChildren<RCCP_Differential>();
                        var raw = c.Inputs.inputs;
                        maxLateral[i] = Mathf.Max(maxLateral[i], Mathf.Abs(delta.x));
                        maxRawSteer[i] = Mathf.Max(maxRawSteer[i], Mathf.Abs(raw.steerInput));
                        if (extended)
                        {
                            maxProcessedSteer[i] = Mathf.Max(maxProcessedSteer[i], Mathf.Abs(c.steerInput_P));
                            maxRearTorqueDifference[i] = Mathf.Max(maxRearTorqueDifference[i], Mathf.Abs(c.RearAxle.leftWheelCollider.WheelCollider.motorTorque - c.RearAxle.rightWheelCollider.WheelCollider.motorTorque));
                            maxHeading[i] = Mathf.Max(maxHeading[i], Mathf.Abs(Mathf.DeltaAngle(rotations[i].eulerAngles.y, c.transform.eulerAngles.y)));
                            maxSpeed[i] = Mathf.Max(maxSpeed[i], c.absoluteSpeed);
                            minimumGrounded[i] = Mathf.Min(minimumGrounded[i], c.AllWheelColliders.Count(w => w.WheelCollider.isGrounded));
                        }
                        samples.Add(new
                        {
                            profile = profiles[i], time = age, speed = c.absoluteSpeed, lateral = delta.x, distance = delta.z,
                            heading = Mathf.DeltaAngle(rotations[i].eulerAngles.y, c.transform.eulerAngles.y), yaw = c.Rigid.angularVelocity.y * Mathf.Rad2Deg,
                            rawSteer = raw.steerInput, processedSteer = c.steerInput_P, throttle = raw.throttleInput, brake = raw.brakeInput,
                            frontAngle = c.FrontAxle.steerAngle, com = c.Rigid.centerOfMass.ToString("F6"),
                            groundClearance = SessionState.GetBool(Key + ".Clearance", false) || SessionState.GetBool(Key + ".Stability", false) || SessionState.GetBool(Key + ".SuspensionBalance", false) || SessionState.GetBool(Key + ".Calibration", false) ? new
                            {
                                rootY = c.transform.position.y, rootRoadHeight = Vector3.Dot(c.transform.position - roadSurfacePoints[i], roadNormals[i]),
                                bodyClearance = BodyClearance(c, roadSurfacePoints[i], roadNormals[i]), rigidMass = c.Rigid.mass,
                                bodyWorldLowestY = BodyClearance(c, Vector3.zero, Vector3.up),
                                wheelForceSum = c.AllWheelColliders.Sum(w => { w.WheelCollider.GetGroundHit(out WheelHit hit); return hit.force; }),
                                sprungMasses = c.AllWheelColliders.Select(w => w.WheelCollider.sprungMass).ToArray(),
                                sprungMassSum = c.AllWheelColliders.Sum(w => w.WheelCollider.sprungMass)
                            } : null,
                            differential = new { left = diff.outputLeft, right = diff.outputRight },
                            wheels = new[] { c.FrontAxle.leftWheelCollider, c.FrontAxle.rightWheelCollider, c.RearAxle.leftWheelCollider, c.RearAxle.rightWheelCollider }.Select(w =>
                            {
                                w.WheelCollider.GetGroundHit(out WheelHit hit);
                                return new { grounded = w.WheelCollider.isGrounded, steer = w.WheelCollider.steerAngle, rpm = w.WheelCollider.rpm, motor = w.WheelCollider.motorTorque, brake = w.WheelCollider.brakeTorque, forwardSlip = hit.forwardSlip, sidewaysSlip = hit.sidewaysSlip, force = hit.force, normal = hit.normal.ToString("F5"), ground = hit.collider == null ? "none" : hit.collider.name };
                            }).ToArray()
                        });
                    }
                }
                bool grid = SessionState.GetBool(Key + ".Grid", false);
                if (age > (SessionState.GetBool(Key + ".Extended", false) ? 12 : grid ? 5 : 6))
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (SessionState.GetBool(Key + ".Validation", false))
                    {
                        for (int i = 0; i < (grid ? 1 : 4); i++)
                            if (maxLateral[i] > (grid ? .3f : .2f) || maxRawSteer[i] > .0001f || cars[i].absoluteSpeed < 60)
                                errors.Add("Straight launch regression on " + profiles[i] + ": lateral=" + maxLateral[i] + ", rawSteer=" + maxRawSteer[i]);
                        if (!grid && (maxLateral[4] < .5f || maxLateral[5] < .5f)) errors.Add("Legacy offset fixtures did not reproduce the launch defect.");
                    }
                    bool calibration = SessionState.GetBool(Key + ".Calibration", false);
                    if (calibration)
                        foreach (int i in new[] { 1, 3, 5, 7 })
                        {
                            var car = cars[i];
                            Vector3 delta = Quaternion.Inverse(rotations[i]) * (car.transform.position - origins[i]);
                            float lateral = Mathf.Max(maxLateral[i], Mathf.Abs(delta.x));
                            float heading = Mathf.Max(maxHeading[i], Mathf.Abs(Mathf.DeltaAngle(rotations[i].eulerAngles.y, car.transform.eulerAngles.y)));
                            bool banked = i >= 4;
                            if (lateral >= (banked ? 5 : 1) || heading >= (banked ? 2 : 1) || minimumGrounded[i] < 4 ||
                                car.AllWheelColliders.Any(w => !w.WheelCollider.isGrounded) || car.absoluteSpeed <= 170 || maxRawSteer[i] > .0001f || maxProcessedSteer[i] > .0001f)
                                errors.Add("Production launch acceptance failed on " + profiles[i] + ": lateral=" + lateral + ", heading=" + heading + ", grounded=" + minimumGrounded[i] + ", speed=" + car.absoluteSpeed + ", rawSteer=" + maxRawSteer[i] + ", processedSteer=" + maxProcessedSteer[i]);
                        }
                    status = errors.Count == 0 ? SessionState.GetBool(Key + ".Validation", false) || calibration ? "passed" : "completed" : "failed";
                    Write(); EditorApplication.ExitPlaymode();
                }
            }
        }
        catch (Exception e) { errors.Add(e.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }

    private static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception)) errors.Add(message);
    }

    // Signed distance from every authored body-box corner to the lane surface;
    // works even when that treatment has disabled the body colliders.
    private static float BodyClearance(RCCP_CarController car, Vector3 roadPoint, Vector3 normal)
    {
        float minimum = float.PositiveInfinity;
        foreach (var box in car.GetComponentsInChildren<BoxCollider>())
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z)));
                        minimum = Mathf.Min(minimum, Vector3.Dot(corner - roadPoint, normal));
                    }
        return minimum;
    }

    private static void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        if (SessionState.GetBool(Key + ".Extended", false))
        {
            var summary = new List<object>();
            if (cars != null)
                for (int i = 0; i < cars.Length; i++)
                {
                    var c = cars[i]; if (c == null) continue;
                    Vector3 delta = Quaternion.Inverse(rotations[i]) * (c.transform.position - origins[i]);
                    summary.Add(new
                    {
                        profile = profiles[i], seconds = Time.time - began, bankDegrees = i >= 4 ? 1.6f : 0,
                        finalSpeedKmh = c.absoluteSpeed, peakSpeedKmh = Mathf.Max(maxSpeed[i], c.absoluteSpeed), forwardMetres = delta.z,
                        finalLateralMetres = delta.x, maxLateralMetres = Mathf.Max(maxLateral[i], Mathf.Abs(delta.x)),
                        finalHeadingDegrees = Mathf.DeltaAngle(rotations[i].eulerAngles.y, c.transform.eulerAngles.y), maxHeadingDegrees = Mathf.Max(maxHeading[i], Mathf.Abs(Mathf.DeltaAngle(rotations[i].eulerAngles.y, c.transform.eulerAngles.y))),
                        maxRawSteer = maxRawSteer[i], maxProcessedSteer = maxProcessedSteer[i],
                        maxSampledRearTorqueDifferenceNm = maxRearTorqueDifference[i], minimumGroundedWheels = minimumGrounded[i],
                        tcs = c.Stability.TCS, tcsIntensity = c.Stability.TCSIntensity,
                        differential = c.GetComponentInChildren<RCCP_Differential>().differentialType.ToString(),
                        settledRootRoadHeight = settledRootRoadHeight[i], settledBodyClearance = settledBodyClearance[i],
                        settledRootY = settledRootY[i], settledBodyWorldLowestY = settledBodyWorldLowestY[i],
                        finalRootY = c.transform.position.y, finalBodyClearance = BodyClearance(c, roadSurfacePoints[i], roadNormals[i]),
                        finalBodyWorldLowestY = BodyClearance(c, Vector3.zero, Vector3.up),
                        rigidMass = c.Rigid.mass, com = c.Rigid.centerOfMass.ToString("F6"),
                        sprungMasses = c.AllWheelColliders.Select(w => w.WheelCollider.sprungMass).ToArray(),
                        sprungMassSum = c.AllWheelColliders.Sum(w => w.WheelCollider.sprungMass),
                        forceAppPointDistances = c.AllWheelColliders.Select(w => w.WheelCollider.forceAppPointDistance).ToArray(),
                        springDampers = c.AllWheelColliders.Select(w => w.WheelCollider.suspensionSpring.damper).ToArray(),
                        springRates = c.AllWheelColliders.Select(w => w.WheelCollider.suspensionSpring.spring).ToArray(),
                        engineTorque = c.Engine.maximumTorqueAsNM, brakeTorque = c.FrontAxle.maxBrakeTorque,
                        downforce = c.AeroDynamics.downForce, drag = c.AeroDynamics.airResistance,
                        frontAntiroll = c.FrontAxle.antirollForce, rearAntiroll = c.RearAxle.antirollForce,
                        bodyCollidersEnabled = c.GetComponentsInChildren<BoxCollider>().Count(b => b.enabled)
                    });
                }
            bool clearance = SessionState.GetBool(Key + ".Clearance", false);
            bool stability = SessionState.GetBool(Key + ".Stability", false);
            bool suspensionBalance = SessionState.GetBool(Key + ".SuspensionBalance", false);
            bool calibration = SessionState.GetBool(Key + ".Calibration", false);
            File.WriteAllText("Docs/AI/FormulaHandling/" + (calibration ? "LaunchCalibrationComparison.json" : suspensionBalance ? "SuspensionBalanceDiagnosis.json" : stability ? "StabilityDiagnosis.json" : clearance ? "GroundClearanceDiagnosis.json" : "ExtendedLaunchDiagnosis.json"), JsonConvert.SerializeObject(new
            {
                status, unity = Application.unityVersion,
                method = "Twelve-second full-throttle launches from settled rest on separate level or 1.6-degree banked BoxCollider lanes. Current centered production prefab. First car uses W through Input System; all others request raw zero steering. " + (calibration ? "Acceptance uses actual saved production physics plus normal level0/level3 development, requiring flat drift<1m/heading<1deg, banked drift<5m/heading<2deg, four wheels grounded, speed>170km/h and zero steering. Diagnostic controls recreate original 8500 damping/0.05 force point/old COM and compare a 3500 damping alternative" : suspensionBalance ? "Runtime-only 3500 damping, 4500 damping with 0.2 force application height, and 4500 damping with authored COM treatments" : stability ? "Runtime-only single-factor wheel force application point, suspension damper, and antiroll treatments" : clearance ? "Runtime-only single-factor body collider, authored COM, and lowered-wheel/raised-body treatments" : "Runtime-only single-factor TCS/differential treatments") + "; sampled every 0.2 seconds.",
                devices = InputSystem.devices.Select(d => d.layout).ToArray(), isolatedSave = true, errors, summary, samples
            }, Formatting.Indented));
            return;
        }
        bool grid = SessionState.GetBool(Key + ".Grid", false);
        string output = grid ? "Docs/AI/FormulaHandling/GridLaunchValidation.json" : SessionState.GetBool(Key + ".Validation", false) ? "Docs/AI/FormulaHandling/LaunchValidation.json" : Output;
        string method = grid ? "Natural g01 career grid start; five seconds of W through real keyboard Input System, no A/D, no AI, no position/velocity writes" : "Six-second full-throttle launches from settled rest on one level BoxCollider, zero requested steering, runtime-only hypothesis treatments";
        File.WriteAllText(output, JsonConvert.SerializeObject(new { status, unity = Application.unityVersion, method, devices = InputSystem.devices.Select(d => d.layout).ToArray(), isolatedSave = true, errors, samples }, Formatting.Indented));
    }
}
