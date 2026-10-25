using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// A narrow runtime experiment: fresh independent cars, one changed factor per candidate.
[InitializeOnLoad]
public static class FormulaPowerCornerValidation
{
    private const string Key = "Formula.PowerCorner.Active";
    private static string Output => "Docs/AI/FormulaHandling/" + (SessionState.GetBool(Key + ".Traction", false) ? "PowerCornerTractionDiagnosis.json" : "PowerCornerDiagnosis.json");
    private static FormulaPowerCornerValidationDriver driver;
    private static Report report;
    private static double started;

    [Serializable]
    public sealed class WheelTrace
    {
        public bool grounded;
        public float forwardSlip, sidewaysSlip, motorTorque, brakeTorque, rpm, contactForce, driftSidewaysStiffnessMultiplier;
    }
    [Serializable]
    public sealed class Trace
    {
        public float time, speed, requestedSteer, requestedThrottle, rawSteer, processedSteer, processedThrottle;
        public float actualSteer, slipAngle, yawRate, signedHeading, driftIntensity, engineRPM, tractionScale, rearSlipScale, bodySlipScale;
        public int gear, grounded, renderFrame;
        public bool espEngaged, tcsEngaged;
        public WheelTrace[] wheels;
    }
    [Serializable]
    public sealed class Profile
    {
        public string name;
        public float steer = 1, throttle = 1, steeringCurveMultiplier = 1;
        public bool esp = true, tcs = true, adaptiveThrottle, bodySlipGuard, limitedSlipRatioZero;
        public float rearGripMultiplier = 1, turnThrottleCap = 1, tcsThresholdOverride = -1;
        public float tcsIntensity, tcsThreshold, differentialLimitedSlipRatio, rearGrip;
        public float initialSpeed = 100, finalSpeed, maxSlipAngle, maxYawRate, peakHeading, signedHeading, maxWheelAngle;
        public float maxDriftIntensity, minDriftSidewaysStiffnessMultiplier = 1, duration, launchDuration;
        public float minTractionScale = 1, finalTractionScale = 1, minRequestedThrottle = 1;
        public int initialGear, minGrounded = 4, fixedSamples, renderFrames, espEngagedSamples, tcsEngagedSamples;
        public List<Trace> trace = new List<Trace>();
    }
    [Serializable]
    public sealed class Report
    {
        public string status = "running", unityVersion, settingsName, selectedBehaviorName;
        public string method = "Nine fresh stock L0 cars on a flat road in separated lanes. Seed 100 km/h once after settling; eight primary profiles start in gear index 0 and a ninth production discriminator starts in gear index 2. Automatic gearbox remains active. Steer from 0.5 to 4.5 seconds, observe for 6 seconds, requested throttle held throughout. Real RCCP OverrideInputs/Update propagation and FormulaSteering remain active. Fixed-step summaries and 0.1-second traces. Wheel order FL, FR, RL, RR; slips are raw WheelHit values, avoiding RCCP slip getters that mutate filters. Diagnostic completion does not imply handling passed.";
        public string tcsCandidate = "Production TCSIntensity is already the documented maximum 1 ([Range(0,1)]); the seventh case changes ESP to off instead.";
        public float fixedDeltaTime;
        public bool isolatedSave = true, overrideBehavior, driftMode, tractionExperiment;
        public int selectedBehaviorIndex, targetFrameRate, vSyncCount;
        public string adaptiveLoop;
        public List<Profile> profiles = new List<Profile>();
        public List<string> errors = new List<string>();
    }

    static FormulaPowerCornerValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Diagnose Power Corner")]
    public static void Run() => Start(false);
    [MenuItem("Tools/Formula Career/Diagnose Power Corner Traction")]
    public static void Traction() => Start(true);
    private static void Start(bool traction)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".PreviousScene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetBool(Key + ".Traction", traction);
        SessionState.SetInt(Key + ".FrameRate", Application.targetFrameRate);
        SessionState.SetInt(Key + ".VSync", QualitySettings.vSyncCount);
        string previousSave = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSaveOverride", previousSave != null);
        SessionState.SetString(Key + ".SaveOverride", previousSave ?? "");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaPowerCorner-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report { unityVersion = Application.unityVersion, fixedDeltaTime = Time.fixedDeltaTime, tractionExperiment = SessionState.GetBool(Key + ".Traction", false) };
            driver = null; started = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            if (report.tractionExperiment) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; }
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        else if (value == PlayModeStateChange.EnteredEditMode)
        {
            if (report != null && report.status == "running") { report.status = "interrupted"; Write(); }
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSaveOverride", false) ? SessionState.GetString(Key + ".SaveOverride", "") : null);
            string previous = SessionState.GetString(Key + ".PreviousScene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            if (SessionState.GetBool(Key + ".Traction", false))
            {
                QualitySettings.vSyncCount = SessionState.GetInt(Key + ".VSync", 0);
                Application.targetFrameRate = SessionState.GetInt(Key + ".FrameRate", -1);
            }
            SessionState.SetBool(Key, false); driver = null;
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new TimeoutException("Power-corner experiment exceeded 120 seconds.");
            if (driver == null)
            {
                foreach (var car in UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None)) car.gameObject.SetActive(false);
                foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) collider.enabled = false;
                var road = new GameObject("Power-corner diagnosis flat road");
                road.transform.position = new Vector3(0, -.05f, 0);
                road.AddComponent<BoxCollider>().size = new Vector3(5000, .1f, 5000);
                driver = new GameObject("Power-corner fixed-step driver").AddComponent<FormulaPowerCornerValidationDriver>();
                driver.Initialize(report, Write);
            }
            if (driver.Done)
            {
                report.status = report.errors.Count == 0 ? "completed" : "errors";
                Write(); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error)
        {
            report.errors.Add(error.ToString()); report.status = "errors";
            Write(); EditorApplication.ExitPlaymode();
        }
    }
    private static void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (report != null && SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) report.errors.Add(message);
    }
}

// Observe the last completed physics step after the normal vehicle FixedUpdates;
// request the next raw input without bypassing RCCP's Update processing.
[DefaultExecutionOrder(30000)]
public sealed class FormulaPowerCornerValidationDriver : MonoBehaviour
{
    private FormulaPowerCornerValidation.Report report;
    private Action write;
    private RCCP_CarController[] cars;
    private RCCP_WheelCollider[][] wheels;
    private float[] lastYaw, ages, nextTrace, tractionScale, bodySlipScale, requestedThrottle;
    private float elapsed;
    private int[] firstRenderFrame;
    private bool measuring;
    public bool Done { get; private set; }

    public void Initialize(FormulaPowerCornerValidation.Report destination, Action save)
    {
        report = destination; write = save;
        if (report.tractionExperiment)
        {
            report.method = "Eight fresh stock L0 cars naturally accelerate from rest with full throttle. Each starts an independent six-second measurement on reaching 100 km/h; no seeded pose/velocity. Automatic gears, production steering curve, steer from 0.5 to 4.5 seconds. One runtime candidate per car, plus adaptive throttle with a body-sideslip guard. Real RCCP OverrideInputs/Update path. Target 60 FPS, vSync off, restored afterward. Fixed-step summaries and 0.1-second traces; wheel order FL, FR, RL, RR and raw WheelHit slips. Diagnostic completion is not an acceptance pass.";
            report.tcsCandidate = "Compare earlier TCS threshold 0.12 and TCS off against stock; intensity remains the valid maximum 1.";
            report.adaptiveLoop = "Measurement only: s=max(0,leftRear.forwardSlip,rightRear.forwardSlip) from grounded WheelHits. If s>0.15, rearScale=Clamp01(rearScale-4*fixedDeltaTime*Clamp01((s-0.15)/0.15)); if s<0.10, rearScale=Clamp01(rearScale+0.4*fixedDeltaTime); otherwise hold. In the body-guard candidate at speed>20, target=Clamp01((7-abs(bodySlipDegrees))/4), otherwise target=1; bodyScale moves toward target at 10/s when reducing, 2/s when recovering. Combined scale=min(rearScale,bodyScale); requested throttle=demand*scale. Scales start at 1. Launch to 100 km/h uses full demand and no adaptive cut. Turn caps apply only during the 0.5..4.5s steering interval.";
            report.profiles.AddRange(new[]
            {
                new FormulaPowerCornerValidation.Profile { name = "production_full_throttle" },
                new FormulaPowerCornerValidation.Profile { name = "tcs_threshold_012", tcsThresholdOverride = .12f },
                new FormulaPowerCornerValidation.Profile { name = "rear_grip_plus_25_percent", rearGripMultiplier = 1.25f },
                new FormulaPowerCornerValidation.Profile { name = "turn_throttle_cap_055", turnThrottleCap = .55f },
                new FormulaPowerCornerValidation.Profile { name = "turn_throttle_cap_035", turnThrottleCap = .35f },
                new FormulaPowerCornerValidation.Profile { name = "adaptive_rear_slip_throttle", adaptiveThrottle = true },
                new FormulaPowerCornerValidation.Profile { name = "tcs_off", tcs = false },
                new FormulaPowerCornerValidation.Profile { name = "adaptive_rear_slip_throttle_body_slip_guard", adaptiveThrottle = true, bodySlipGuard = true }
            });
        }
        else report.profiles.AddRange(new[]
        {
            new FormulaPowerCornerValidation.Profile { name = "production_full_steer_full_throttle" },
            new FormulaPowerCornerValidation.Profile { name = "production_half_steer_full_throttle", steer = .5f },
            new FormulaPowerCornerValidation.Profile { name = "production_full_steer_quarter_throttle", throttle = .25f },
            new FormulaPowerCornerValidation.Profile { name = "production_full_steer_zero_throttle", throttle = 0 },
            new FormulaPowerCornerValidation.Profile { name = "steering_curve_75_percent_full_throttle", steeringCurveMultiplier = .75f },
            new FormulaPowerCornerValidation.Profile { name = "limited_differential_ratio_zero_full_throttle", limitedSlipRatioZero = true },
            new FormulaPowerCornerValidation.Profile { name = "esp_off_full_throttle", esp = false },
            new FormulaPowerCornerValidation.Profile { name = "rear_grip_plus_15_percent_full_throttle", rearGripMultiplier = 1.15f },
            new FormulaPowerCornerValidation.Profile { name = "production_full_steer_full_throttle_initial_gear_2", initialGear = 2 }
        });
        cars = new RCCP_CarController[report.profiles.Count];
        wheels = new RCCP_WheelCollider[cars.Length][]; lastYaw = new float[cars.Length];
        ages = new float[cars.Length]; nextTrace = new float[cars.Length];
        tractionScale = new float[cars.Length]; bodySlipScale = new float[cars.Length]; requestedThrottle = new float[cars.Length]; firstRenderFrame = new int[cars.Length];
        for (int i = 0; i < cars.Length; i++)
        {
            var car = Instantiate(FormulaAssets.Current.vehiclePrefab, new Vector3((i - (cars.Length - 1) * .5f) * 500, .5f, -500), Quaternion.identity).GetComponent<RCCP_CarController>();
            cars[i] = car; car.GetAllComponents();
            if (car.GetComponent<FormulaSteering>() == null) throw new InvalidOperationException("Production car has no FormulaSteering component.");
            car.GetComponent<FormulaSteering>().enabled = true;
            car.canControl = true; car.externalControl = false;
            car.Inputs.overrideExternalInputs = false;
            car.Inputs.OverrideInputs(new RCCP_Inputs());
        }
    }
    private void FixedUpdate()
    {
        if (report == null || Done) return;
        try
        {
            elapsed += Time.fixedDeltaTime;
            if (!measuring)
            {
                if (elapsed < 1.5f || cars.Any(c => !c.GetComponent<FormulaVehicle>().IsReady)) return;
                BeginMeasurement(); return;
            }
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i]; var profile = report.profiles[i];
                if (ages[i] >= 6f - .0001f) continue;
                if (ages[i] < 0)
                {
                    if (car.absoluteSpeed < 100)
                    {
                        if (elapsed > 15) throw new TimeoutException(profile.name + " did not reach 100 km/h within 15 seconds of full-throttle launch.");
                        continue;
                    }
                    ages[i] = 0; nextTrace[i] = .1f; firstRenderFrame[i] = Time.frameCount;
                    lastYaw[i] = car.Rigid.rotation.eulerAngles.y;
                    profile.launchDuration = elapsed; profile.initialSpeed = car.absoluteSpeed; profile.initialGear = car.Gearbox.currentGear;
                    Vector3 launchVelocity = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
                    profile.trace.Add(Capture(i, Mathf.Atan2(launchVelocity.x, launchVelocity.z) * Mathf.Rad2Deg));
                    continue;
                }
                ages[i] += Time.fixedDeltaTime;
                Vector3 local = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
                float speed = car.Rigid.linearVelocity.magnitude * 3.6f;
                float slip = speed > 5 ? Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg : 0;
                float yaw = car.Rigid.rotation.eulerAngles.y;
                profile.signedHeading += Mathf.DeltaAngle(lastYaw[i], yaw); lastYaw[i] = yaw;
                profile.peakHeading = Mathf.Max(profile.peakHeading, Mathf.Abs(profile.signedHeading));
                profile.maxSlipAngle = Mathf.Max(profile.maxSlipAngle, Mathf.Abs(slip));
                profile.maxYawRate = Mathf.Max(profile.maxYawRate, Mathf.Abs(car.Rigid.angularVelocity.y * Mathf.Rad2Deg));
                profile.maxWheelAngle = Mathf.Max(profile.maxWheelAngle, Mathf.Abs(car.FrontAxle.steerAngle));
                profile.minGrounded = Mathf.Min(profile.minGrounded, wheels[i].Count(w => w.WheelCollider.isGrounded));
                profile.maxDriftIntensity = Mathf.Max(profile.maxDriftIntensity, car.Stability.driftIntensity);
                profile.minDriftSidewaysStiffnessMultiplier = Mathf.Min(profile.minDriftSidewaysStiffnessMultiplier, wheels[i].Min(w => w.driftSidewaysStiffnessMultiplier));
                if (car.Stability.ESPEngaged) profile.espEngagedSamples++;
                if (car.Stability.TCSEngaged) profile.tcsEngagedSamples++;
                profile.finalSpeed = speed; profile.duration = ages[i]; profile.fixedSamples++;
                profile.renderFrames = Time.frameCount - firstRenderFrame[i];
                profile.finalTractionScale = Mathf.Min(tractionScale[i], bodySlipScale[i]);
                profile.minTractionScale = Mathf.Min(profile.minTractionScale, profile.finalTractionScale);
                profile.minRequestedThrottle = Mathf.Min(profile.minRequestedThrottle, requestedThrottle[i]);
                if (ages[i] + .0001f >= nextTrace[i]) { profile.trace.Add(Capture(i, slip)); nextTrace[i] += .1f; }
            }
            if (ages.Any(age => age < 6f - .0001f)) { RequestInputs(); return; }
            foreach (var car in cars) car.Inputs.OverrideInputs(new RCCP_Inputs());
            Done = true; write();
        }
        catch (Exception error) { report.errors.Add(error.ToString()); Done = true; }
    }
    private void BeginMeasurement()
    {
        var settings = RCCP_Settings.Instance;
        var behavior = settings.SelectedBehaviorType;
        report.settingsName = settings.name; report.overrideBehavior = settings.overrideBehavior;
        report.selectedBehaviorIndex = settings.behaviorSelectedIndex;
        report.selectedBehaviorName = behavior != null ? behavior.behaviorName : "none";
        report.driftMode = behavior != null && behavior.driftMode;
        if (report.tractionExperiment) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; }
        report.targetFrameRate = Application.targetFrameRate; report.vSyncCount = QualitySettings.vSyncCount;
        for (int i = 0; i < cars.Length; i++)
        {
            var car = cars[i]; var profile = report.profiles[i];
            car.GetComponent<FormulaVehicle>().ApplyDevelopment(0, 0, 0, 0, "balanced");
            car.Stability.ABS = true; car.Stability.TCS = profile.tcs; car.Stability.ESP = profile.esp;
            if (profile.tcsThresholdOverride >= 0) car.Stability.engageTCSThreshold = profile.tcsThresholdOverride;
            car.Inputs.autoReverse = false;
            if (profile.steeringCurveMultiplier != 1)
            {
                var curve = car.Inputs.steeringCurve;
                var keys = curve.keys;
                for (int k = 0; k < keys.Length; k++)
                {
                    keys[k].value *= profile.steeringCurveMultiplier;
                    keys[k].inTangent *= profile.steeringCurveMultiplier;
                    keys[k].outTangent *= profile.steeringCurveMultiplier;
                }
                car.Inputs.steeringCurve = new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
            }
            var differential = car.GetComponentInChildren<RCCP_Differential>();
            if (differential == null) throw new InvalidOperationException("Production car has no differential.");
            if (profile.limitedSlipRatioZero) differential.limitedSlipRatio = 0;
            car.RearAxle.leftWheelCollider.grip *= profile.rearGripMultiplier;
            car.RearAxle.rightWheelCollider.grip *= profile.rearGripMultiplier;
            profile.tcsIntensity = car.Stability.TCSIntensity;
            profile.tcsThreshold = car.Stability.engageTCSThreshold;
            profile.differentialLimitedSlipRatio = differential.limitedSlipRatio;
            profile.rearGrip = car.RearAxle.leftWheelCollider.grip;
            wheels[i] = new[] { car.FrontAxle.leftWheelCollider, car.FrontAxle.rightWheelCollider, car.RearAxle.leftWheelCollider, car.RearAxle.rightWheelCollider };
            car.Engine.engineRunning = true;
            car.Gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Automatic;
            car.Gearbox.overrideGear = false; car.Gearbox.currentGear = profile.initialGear;
            // Original seeded experiment only; traction mode launches naturally from rest.
            if (!report.tractionExperiment)
            {
                car.Rigid.rotation = Quaternion.identity;
                car.Rigid.linearVelocity = Vector3.forward * (100f / 3.6f);
                car.Rigid.angularVelocity = Vector3.zero;
            }
            lastYaw[i] = 0;
            ages[i] = report.tractionExperiment ? -1 : 0; nextTrace[i] = .1f;
            tractionScale[i] = bodySlipScale[i] = 1;
            firstRenderFrame[i] = Time.frameCount;
        }
        elapsed = 0; measuring = true;
        RequestInputs();
        if (!report.tractionExperiment)
            for (int i = 0; i < cars.Length; i++) report.profiles[i].trace.Add(Capture(i, 0));
        write();
    }
    private void RequestInputs()
    {
        for (int i = 0; i < cars.Length; i++)
        {
            var profile = report.profiles[i];
            var car = cars[i]; float time = ages[i];
            if (time >= 6f - .0001f) { car.Inputs.OverrideInputs(new RCCP_Inputs()); continue; }
            bool turning = time >= .5f && time < 4.5f;
            float demand = turning ? Mathf.Min(profile.throttle, profile.turnThrottleCap) : profile.throttle;
            if (time < 0) demand = 1;
            else if (profile.adaptiveThrottle)
            {
                float slip = 0;
                for (int w = 2; w < 4; w++)
                    if (wheels[i][w].WheelCollider.GetGroundHit(out WheelHit hit)) slip = Mathf.Max(slip, hit.forwardSlip);
                if (slip > .15f) tractionScale[i] = Mathf.Clamp01(tractionScale[i] - 4f * Time.fixedDeltaTime * Mathf.Clamp01((slip - .15f) / .15f));
                else if (slip < .10f) tractionScale[i] = Mathf.Clamp01(tractionScale[i] + .4f * Time.fixedDeltaTime);
                if (profile.bodySlipGuard)
                {
                    Vector3 velocity = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
                    float bodySlip = Mathf.Abs(Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg);
                    float target = car.absoluteSpeed > 20 ? Mathf.Clamp01((7f - bodySlip) / 4f) : 1;
                    bodySlipScale[i] = Mathf.MoveTowards(bodySlipScale[i], target, Time.fixedDeltaTime * (target < bodySlipScale[i] ? 10f : 2f));
                }
            }
            requestedThrottle[i] = demand * Mathf.Min(tractionScale[i], bodySlipScale[i]);
            car.Inputs.OverrideInputs(new RCCP_Inputs { steerInput = turning ? profile.steer : 0, throttleInput = requestedThrottle[i] });
        }
    }
    private FormulaPowerCornerValidation.Trace Capture(int index, float slip)
    {
        var car = cars[index]; var profile = report.profiles[index];
        var sample = new FormulaPowerCornerValidation.Trace
        {
            time = ages[index], speed = car.Rigid.linearVelocity.magnitude * 3.6f,
            requestedSteer = ages[index] >= .5f && ages[index] < 4.5f ? profile.steer : 0, requestedThrottle = requestedThrottle[index],
            rawSteer = car.Inputs.inputs.steerInput, processedSteer = car.steerInput_P, processedThrottle = car.throttleInput_P,
            actualSteer = car.FrontAxle.steerAngle, slipAngle = slip,
            yawRate = car.Rigid.angularVelocity.y * Mathf.Rad2Deg, signedHeading = profile.signedHeading,
            driftIntensity = car.Stability.driftIntensity, gear = car.Gearbox.currentGear, engineRPM = car.Engine.engineRPM,
            tractionScale = Mathf.Min(tractionScale[index], bodySlipScale[index]), rearSlipScale = tractionScale[index], bodySlipScale = bodySlipScale[index],
            espEngaged = car.Stability.ESPEngaged, tcsEngaged = car.Stability.TCSEngaged,
            renderFrame = Time.frameCount, wheels = new FormulaPowerCornerValidation.WheelTrace[4]
        };
        for (int w = 0; w < wheels[index].Length; w++)
        {
            var wrapper = wheels[index][w]; var wheel = wrapper.WheelCollider;
            bool grounded = wheel.GetGroundHit(out WheelHit hit);
            if (grounded) sample.grounded++;
            sample.wheels[w] = new FormulaPowerCornerValidation.WheelTrace
            {
                grounded = grounded, forwardSlip = grounded ? hit.forwardSlip : 0, sidewaysSlip = grounded ? hit.sidewaysSlip : 0,
                motorTorque = wheel.motorTorque, brakeTorque = wheel.brakeTorque, rpm = wheel.rpm,
                contactForce = grounded ? hit.force : 0, driftSidewaysStiffnessMultiplier = wrapper.driftSidewaysStiffnessMultiplier
            };
        }
        return sample;
    }
}
