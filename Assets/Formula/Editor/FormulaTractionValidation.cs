using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Exercises the production component. This driver never modulates throttle or writes velocity.
[InitializeOnLoad]
public static class FormulaTractionValidation
{
    private const string Key = "Formula.TractionValidation.Active";
    private static string Output => "Docs/AI/FormulaHandling/TractionValidation" + (SessionState.GetString(Key + ".Mode", "matrix") == "matrix" ? "" : "-" + SessionState.GetString(Key + ".Mode", "matrix")) + (SessionState.GetInt(Key + ".TargetFPS", 60) == 60 ? "" : "-" + SessionState.GetInt(Key + ".TargetFPS", 60) + "fps") + ".json";
    private static FormulaTractionValidationDriver driver;
    private static Report report;
    private static double started;

    [Serializable]
    public sealed class WheelSample
    {
        public bool grounded;
        public float forwardSlip, sidewaysSlip, rpm, motorTorque, brakeTorque, force;
    }
    [Serializable]
    public sealed class Sample
    {
        public float time, speed, slip, yawRate, heading, rawThrottle, processedThrottle, rawSteer, processedSteer, wheelAngle;
        public float torqueScale, engineTorque, maximumTorque, engineRPM;
        public int grounded, gear, renderFrame;
        public bool tcsEngaged, espEngaged, tcsEnabled, canControl, externalControl, overrideExternalInputs, engineRunning, tractionEnabled;
        public WheelSample[] wheels;
    }
    [Serializable]
    public sealed class Case
    {
        public string name, ownershipCheck;
        public int level, direction, fixedSamples, renderFrames, minGrounded = 4, initialGear;
        public bool diagnosticBaseline, recoveredFullPower, liftReapply, resetTriggered, resetPassed, nonFiniteObserved;
        public float steerFraction = 1;
        public float targetSpeed, actualEntrySpeed, launchSeconds, launchHeading, launchLateralDisplacement;
        public float authoredMaximumTorque, minLaunchTorqueScale = 1, maxSlip, maxYawRate, signedHeading, turnHeading;
        public float distanceTravelled, forwardDistance, finalSpeed, finalSlip, finalYawRate, finalTorqueScale, finalMaximumTorque;
        public float finalAlignedFullPowerSeconds, maximumTorqueDeviation, maxRawThrottleError;
        public float scaleBeforeReset, maxResetScaleError, maxResetMaximumTorqueError;
        public int resetFixedSamples;
        public float minTorqueScale = 1, minRawThrottle = 1, recoveryAlignedSeconds, fullPowerRecoveredTime = -1, duration;
        public List<Sample> trace = new List<Sample>();
    }
    [Serializable]
    public sealed class Report
    {
        public string status = "running", unityVersion, selectedBehavior, mode;
        public string method = "Fresh production cars naturally launch from rest with raw throttle held at 1 through RCCP OverrideInputs/Update. Target entry speeds 80/100/120 km/h, left/right full steering, stock/L3; each independent measurement holds steering from 0.5 to 4.5 seconds, then observes straight recovery to 9 seconds. FormulaTractionControl alone owns traction intervention. No velocity/pose writes after spawn or experimental throttle cuts. One stock 100 km/h right-turn case has only FormulaTractionControl disabled and is excluded from handling acceptance. Fixed-step summaries, 0.1-second raw WheelHit traces (FL,FR,RL,RR), chosen frame-rate target reported separately. Isolated save and restored Editor settings.";
        public string acceptance = "Production: body slip <20 degrees, all four wheels grounded, requested-direction heading at steering release >20 degrees, >60m travel and forward progress, final speed >30 km/h, raw throttle follows the requested input. At the end, torque scale >=95%, body slip <3 degrees, yaw <5 deg/s, and these conditions hold for the final continuous 0.3 seconds. Engine maximum torque must remain within 0.01 Nm of its authored value. Stock matrix 0-100 launch must remain within baseline*1.10+0.15s. Baseline spin is diagnostic only.";
        public bool isolatedSave = true, driftMode;
        public float fixedDeltaTime;
        public int targetFrameRate, vSyncCount;
        public List<Case> cases = new List<Case>();
        public List<string> errors = new List<string>();
    }
    static FormulaTractionValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Validate Formula Traction")]
    public static void Run() => Start(60);
    [MenuItem("Tools/Formula Career/Validate Formula Traction at 30 FPS")]
    public static void Run30() => Start(30);
    [MenuItem("Tools/Formula Career/Validate Formula Traction at 120 FPS")]
    public static void Run120() => Start(120);
    [MenuItem("Tools/Formula Career/Validate Formula Traction Targeted Inputs")]
    public static void Targeted() => Start(60, "targeted");
    [MenuItem("Tools/Formula Career/Validate Formula Traction Ownership")]
    public static void Ownership() => Start(60, "ownership");
    private static void Start(int targetFPS, string mode = "matrix")
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetInt(Key + ".FrameRate", Application.targetFrameRate);
        SessionState.SetInt(Key + ".VSync", QualitySettings.vSyncCount);
        SessionState.SetInt(Key + ".TargetFPS", targetFPS);
        SessionState.SetString(Key + ".Mode", mode);
        string oldSave = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSave", oldSave != null); SessionState.SetString(Key + ".Save", oldSave ?? "");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaTraction-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report { unityVersion = Application.unityVersion, fixedDeltaTime = Time.fixedDeltaTime, targetFrameRate = SessionState.GetInt(Key + ".TargetFPS", 60), mode = SessionState.GetString(Key + ".Mode", "matrix") };
            driver = null; started = EditorApplication.timeSinceStartup;
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = report.targetFrameRate;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        else if (value == PlayModeStateChange.EnteredEditMode)
        {
            if (report != null && report.status == "running") { report.status = "interrupted"; Write(); }
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSave", false) ? SessionState.GetString(Key + ".Save", "") : null);
            string scene = SessionState.GetString(Key + ".Scene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            QualitySettings.vSyncCount = SessionState.GetInt(Key + ".VSync", 0);
            Application.targetFrameRate = SessionState.GetInt(Key + ".FrameRate", -1);
            SessionState.SetBool(Key, false); driver = null;
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new TimeoutException("Traction acceptance exceeded 180 seconds.");
            if (driver == null)
            {
                foreach (var car in UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None)) car.gameObject.SetActive(false);
                foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) collider.enabled = false;
                var road = new GameObject("Traction acceptance flat road");
                road.transform.position = new Vector3(0, -.05f, 0);
                road.AddComponent<BoxCollider>().size = new Vector3(10000, .1f, 10000);
                driver = new GameObject("Production traction acceptance driver").AddComponent<FormulaTractionValidationDriver>();
                driver.Initialize(report, Write);
            }
            if (driver.Done)
            {
                report.status = report.errors.Count == 0 ? "passed" : "failed";
                Write(); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error)
        {
            report.errors.Add(error.ToString()); report.status = "failed";
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

[DefaultExecutionOrder(30000)]
public sealed class FormulaTractionValidationDriver : MonoBehaviour
{
    private sealed class RuntimeCase
    {
        public FormulaTractionValidation.Case result;
        public RCCP_CarController car;
        public FormulaTractionControl traction;
        public RCCP_WheelCollider[] wheels;
        public float age = -1, lastYaw, nextTrace, fullPowerAlignedSeconds, requestedThrottle, resetElapsed = -1;
        public Vector3 launchOrigin, previousPosition;
        public int firstRenderFrame;
        public bool turnHeadingRecorded;
    }
    private readonly List<RuntimeCase> runs = new List<RuntimeCase>();
    private FormulaTractionValidation.Report report;
    private Action write;
    private float elapsed, nextWrite;
    private bool launched;
    public bool Done { get; private set; }

    public void Initialize(FormulaTractionValidation.Report destination, Action save)
    {
        report = destination; write = save;
        if (report.mode == "targeted")
        {
            report.method = "Four production cases naturally launch to 100 km/h: half steering/full throttle and full steering/lift at 2.0s/reapply at 2.7s, at L0 and L3. Same 0.5..4.5s steering interval and 9s recovery measurement as the main matrix. Fixed-step real RCCP raw input path; no velocity/pose writes. Production traction owns torque intervention.";
            foreach (int level in new[] { 0, 3 })
            {
                report.cases.Add(new FormulaTractionValidation.Case { name = "half_steer_L" + level, level = level, targetSpeed = 100, direction = 1, steerFraction = .5f });
                report.cases.Add(new FormulaTractionValidation.Case { name = "lift_reapply_L" + level, level = level, targetSpeed = 100, direction = 1, liftReapply = true });
            }
        }
        else if (report.mode == "ownership")
        {
            report.method = "Six fresh cars naturally launch to 100 km/h and enter a full-throttle turn. Only after production TorqueScale drops to <=0.8, change one ownership/assist condition for 0.2s: TCS off, RCCP_AI ownership signal externalControl, external input override, engine off, component disabled, canControl off. The actual AI navigation component is not attached to this flat-road probe. Fixed-step observations; no teleport or velocity writes.";
            report.acceptance = "Every case must first demonstrate active protection (TorqueScale <=0.8), then restore TorqueScale to 1 within the next physics step and hold it for 0.2s; engine maximum torque remains within 0.01 Nm. Flags are restored after observation. Handling bounds do not apply while the assist is intentionally bypassed.";
            foreach (string check in new[] { "tcs_off", "ai_external_control", "external_input_override", "engine_off", "component_disabled", "can_control_off" })
                report.cases.Add(new FormulaTractionValidation.Case { name = check, ownershipCheck = check, targetSpeed = 100, direction = 1 });
        }
        else
        {
            foreach (int level in new[] { 0, 3 })
            foreach (int speed in new[] { 80, 100, 120 })
                foreach (int direction in new[] { -1, 1 })
                    report.cases.Add(new FormulaTractionValidation.Case { name = "production_L" + level + "_" + speed + "_" + (direction < 0 ? "left" : "right"), level = level, targetSpeed = speed, direction = direction });
            report.cases.Add(new FormulaTractionValidation.Case { name = "diagnostic_component_disabled_100_right", targetSpeed = 100, direction = 1, diagnosticBaseline = true });
        }
        for (int i = 0; i < report.cases.Count; i++)
        {
            var car = Instantiate(FormulaAssets.Current.vehiclePrefab, new Vector3((i - (report.cases.Count - 1) * .5f) * 500, .5f, -1000), Quaternion.identity).GetComponent<RCCP_CarController>();
            car.GetAllComponents();
            var traction = car.GetComponent<FormulaTractionControl>();
            if (traction == null) throw new InvalidOperationException("Production prefab has no FormulaGame.FormulaTractionControl.");
            traction.enabled = !report.cases[i].diagnosticBaseline;
            if (car.GetComponent<FormulaSteering>() == null || !car.GetComponent<FormulaSteering>().enabled) throw new InvalidOperationException("Production FormulaSteering is missing or disabled.");
            car.canControl = true; car.externalControl = false; car.Inputs.overrideExternalInputs = false;
            car.Inputs.OverrideInputs(new RCCP_Inputs());
            runs.Add(new RuntimeCase { result = report.cases[i], car = car, traction = traction });
        }
    }
    private void FixedUpdate()
    {
        if (report == null || Done) return;
        try
        {
            elapsed += Time.fixedDeltaTime;
            if (!launched)
            {
                if (elapsed < 1.5f || runs.Any(r => !r.car.GetComponent<FormulaVehicle>().IsReady)) return;
                BeginLaunch(); return;
            }
            foreach (var run in runs)
            {
                if (run.age >= 9f - .0001f) continue;
                var car = run.car; var result = run.result;
                if (run.age < 0)
                {
                    result.minLaunchTorqueScale = Mathf.Min(result.minLaunchTorqueScale, Scale(run));
                    if (car.absoluteSpeed < result.targetSpeed)
                    {
                        if (elapsed > 15) throw new TimeoutException(result.name + " did not reach entry speed within 15 seconds of full-throttle launch.");
                        continue;
                    }
                    result.launchSeconds = elapsed; result.actualEntrySpeed = car.absoluteSpeed; result.initialGear = car.Gearbox.currentGear;
                    result.launchHeading = Mathf.DeltaAngle(0, car.Rigid.rotation.eulerAngles.y);
                    result.launchLateralDisplacement = car.Rigid.position.x - run.launchOrigin.x;
                    run.age = 0; run.nextTrace = .1f; run.firstRenderFrame = Time.frameCount;
                    run.previousPosition = car.Rigid.position; run.lastYaw = car.Rigid.rotation.eulerAngles.y;
                    result.trace.Add(Capture(run)); continue;
                }
                run.age += Time.fixedDeltaTime;
                Observe(run);
                if (report.mode == "ownership") ObserveOwnership(run);
            }
            if (elapsed >= nextWrite) { nextWrite = elapsed + 1; write(); }
            if (runs.Any(r => r.age < 9f - .0001f)) { RequestInputs(); return; }
            foreach (var run in runs) run.car.Inputs.OverrideInputs(new RCCP_Inputs());
            CheckResults(); Done = true; write();
        }
        catch (Exception error) { report.errors.Add(error.ToString()); Done = true; }
    }
    private void BeginLaunch()
    {
        var behavior = RCCP_Settings.Instance.SelectedBehaviorType;
        report.selectedBehavior = behavior != null ? behavior.behaviorName : "none";
        report.driftMode = behavior != null && behavior.driftMode;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = report.targetFrameRate;
        report.targetFrameRate = Application.targetFrameRate; report.vSyncCount = QualitySettings.vSyncCount;
        foreach (var run in runs)
        {
            var car = run.car; int level = run.result.level;
            car.GetComponent<FormulaVehicle>().ApplyDevelopment(level, level, level, level, "balanced");
            car.Stability.ABS = car.Stability.TCS = car.Stability.ESP = true;
            car.Inputs.autoReverse = false;
            car.Engine.engineRunning = true;
            car.Gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Automatic;
            car.Gearbox.overrideGear = false; car.Gearbox.currentGear = 0;
            run.result.authoredMaximumTorque = car.Engine.maximumTorqueAsNM;
            run.wheels = new[] { car.FrontAxle.leftWheelCollider, car.FrontAxle.rightWheelCollider, car.RearAxle.leftWheelCollider, car.RearAxle.rightWheelCollider };
            run.launchOrigin = car.Rigid.position;
        }
        elapsed = 0; nextWrite = 1; launched = true; RequestInputs(); write();
    }
    private static float Scale(RuntimeCase run) => run.traction.TorqueScale;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float BodySlip(RCCP_CarController car)
    {
        if (car.Rigid.linearVelocity.magnitude * 3.6f < 5) return 0;
        Vector3 local = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
        return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
    }
    private void Observe(RuntimeCase run)
    {
        var car = run.car; var result = run.result;
        float slip = BodySlip(car), yawRate = car.Rigid.angularVelocity.y * Mathf.Rad2Deg;
        float yaw = car.Rigid.rotation.eulerAngles.y, scale = Scale(run);
        result.signedHeading += Mathf.DeltaAngle(run.lastYaw, yaw); run.lastYaw = yaw;
        result.maxSlip = Mathf.Max(result.maxSlip, Mathf.Abs(slip));
        result.maxYawRate = Mathf.Max(result.maxYawRate, Mathf.Abs(yawRate));
        result.minGrounded = Mathf.Min(result.minGrounded, run.wheels.Count(w => w.WheelCollider.isGrounded));
        result.minTorqueScale = Mathf.Min(result.minTorqueScale, scale);
        result.minRawThrottle = Mathf.Min(result.minRawThrottle, car.Inputs.inputs.throttleInput);
        result.maxRawThrottleError = Mathf.Max(result.maxRawThrottleError, Mathf.Abs(car.Inputs.inputs.throttleInput - run.requestedThrottle));
        result.maximumTorqueDeviation = Mathf.Max(result.maximumTorqueDeviation, Mathf.Abs(car.Engine.maximumTorqueAsNM - result.authoredMaximumTorque));
        result.distanceTravelled += Vector3.Distance(run.previousPosition, car.Rigid.position); run.previousPosition = car.Rigid.position;
        result.forwardDistance += car.transform.InverseTransformDirection(car.Rigid.linearVelocity).z * Time.fixedDeltaTime;
        result.finalSpeed = car.Rigid.linearVelocity.magnitude * 3.6f; result.finalSlip = slip; result.finalYawRate = yawRate;
        result.finalTorqueScale = scale; result.finalMaximumTorque = car.Engine.maximumTorqueAsNM;
        result.nonFiniteObserved |= !Finite(slip) || !Finite(yawRate) || !Finite(scale) || !Finite(result.finalSpeed) ||
            !Finite(result.signedHeading) || !Finite(result.authoredMaximumTorque) || !Finite(result.finalMaximumTorque) ||
            !Finite(car.Engine.producedTorqueAsNM) || !Finite(car.Inputs.inputs.throttleInput);
        result.duration = run.age; result.fixedSamples++; result.renderFrames = Time.frameCount - run.firstRenderFrame;
        if (run.age >= 4.5f)
        {
            if (!run.turnHeadingRecorded) { result.turnHeading = result.signedHeading; run.turnHeadingRecorded = true; }
            bool aligned = Mathf.Abs(slip) < 3 && Mathf.Abs(yawRate) < 5 && result.finalSpeed > 20;
            if (aligned) result.recoveryAlignedSeconds += Time.fixedDeltaTime;
            bool fullPower = scale >= .95f && Mathf.Abs(car.Engine.maximumTorqueAsNM - result.authoredMaximumTorque) <= .01f;
            run.fullPowerAlignedSeconds = aligned && fullPower ? run.fullPowerAlignedSeconds + Time.fixedDeltaTime : 0;
            result.finalAlignedFullPowerSeconds = run.fullPowerAlignedSeconds;
            if (run.fullPowerAlignedSeconds >= .3f && !result.recoveredFullPower)
            {
                result.recoveredFullPower = true; result.fullPowerRecoveredTime = run.age;
            }
        }
        if (run.age + .0001f >= run.nextTrace) { result.trace.Add(Capture(run)); run.nextTrace += .1f; }
    }
    private void ObserveOwnership(RuntimeCase run)
    {
        var result = run.result;
        if (run.resetElapsed < 0)
        {
            if (run.age < .5f || Scale(run) > .8f) return;
            result.scaleBeforeReset = Scale(run); result.resetTriggered = true;
            SetOwnershipFlag(run, true); run.resetElapsed = 0;
            result.trace.Add(Capture(run)); return;
        }
        run.resetElapsed += Time.fixedDeltaTime; result.resetFixedSamples++;
        result.maxResetScaleError = Mathf.Max(result.maxResetScaleError, Mathf.Abs(Scale(run) - 1));
        result.maxResetMaximumTorqueError = Mathf.Max(result.maxResetMaximumTorqueError, Mathf.Abs(run.car.Engine.maximumTorqueAsNM - result.authoredMaximumTorque));
        if (run.resetElapsed + .0001f < .2f) return;
        result.resetPassed = !result.nonFiniteObserved && result.maxResetScaleError <= .001f && result.maxResetMaximumTorqueError <= .01f;
        result.trace.Add(Capture(run));
        SetOwnershipFlag(run, false); run.age = 9;
    }
    private static void SetOwnershipFlag(RuntimeCase run, bool bypass)
    {
        switch (run.result.ownershipCheck)
        {
            case "tcs_off": run.car.Stability.TCS = !bypass; break;
            case "ai_external_control": run.car.externalControl = bypass; break;
            case "external_input_override": run.car.Inputs.overrideExternalInputs = bypass; break;
            case "engine_off": run.car.Engine.engineRunning = !bypass; break;
            case "component_disabled": run.traction.enabled = !bypass; break;
            case "can_control_off": run.car.canControl = !bypass; break;
        }
    }
    private void RequestInputs()
    {
        foreach (var run in runs)
        {
            if (run.age >= 9f - .0001f) { run.car.Inputs.OverrideInputs(new RCCP_Inputs()); continue; }
            run.requestedThrottle = run.result.liftReapply && run.age >= 2f && run.age < 2.7f ? 0 : 1;
            run.car.Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = run.requestedThrottle, steerInput = run.age >= .5f && run.age < 4.5f ? run.result.direction * run.result.steerFraction : 0 });
        }
    }
    private FormulaTractionValidation.Sample Capture(RuntimeCase run)
    {
        var car = run.car;
        var sample = new FormulaTractionValidation.Sample
        {
            time = run.age, speed = car.Rigid.linearVelocity.magnitude * 3.6f, slip = BodySlip(car),
            yawRate = car.Rigid.angularVelocity.y * Mathf.Rad2Deg, heading = run.result.signedHeading,
            rawThrottle = car.Inputs.inputs.throttleInput, processedThrottle = car.throttleInput_P,
            rawSteer = car.Inputs.inputs.steerInput, processedSteer = car.steerInput_P, wheelAngle = car.FrontAxle.steerAngle,
            torqueScale = Scale(run), engineTorque = car.Engine.producedTorqueAsNM, maximumTorque = car.Engine.maximumTorqueAsNM,
            engineRPM = car.Engine.engineRPM, gear = car.Gearbox.currentGear, renderFrame = Time.frameCount,
            tcsEngaged = car.Stability.TCSEngaged, espEngaged = car.Stability.ESPEngaged,
            tcsEnabled = car.Stability.TCS, canControl = car.canControl, externalControl = car.externalControl,
            overrideExternalInputs = car.Inputs.overrideExternalInputs, engineRunning = car.Engine.engineRunning, tractionEnabled = run.traction.enabled,
            wheels = new FormulaTractionValidation.WheelSample[4]
        };
        for (int i = 0; i < run.wheels.Length; i++)
        {
            var wheel = run.wheels[i].WheelCollider; bool grounded = wheel.GetGroundHit(out WheelHit hit);
            if (grounded) sample.grounded++;
            sample.wheels[i] = new FormulaTractionValidation.WheelSample
            {
                grounded = grounded, forwardSlip = grounded ? hit.forwardSlip : 0, sidewaysSlip = grounded ? hit.sidewaysSlip : 0,
                rpm = wheel.rpm, motorTorque = wheel.motorTorque, brakeTorque = wheel.brakeTorque, force = grounded ? hit.force : 0
            };
        }
        return sample;
    }
    private void CheckResults()
    {
        foreach (var result in report.cases.Where(c => !c.diagnosticBaseline))
            if (!Finite(result.minLaunchTorqueScale) || result.minLaunchTorqueScale < .999f)
                report.errors.Add(result.name + ": traction protection reduced power during the flat straight launch.");
        if (report.mode == "ownership")
        {
            foreach (var result in report.cases)
                if (result.nonFiniteObserved || !result.resetTriggered || !result.resetPassed || result.resetFixedSamples < 1)
                    report.errors.Add(result.name + ": active protection was not demonstrated or the bypass did not reset it for 0.2 seconds.");
            return;
        }
        var baseline = report.cases.SingleOrDefault(c => c.diagnosticBaseline);
        foreach (var result in report.cases.Where(c => !c.diagnosticBaseline))
        {
            string label = result.name + ": ";
            if (result.nonFiniteObserved) report.errors.Add(label + "non-finite physics/input/torque telemetry was observed.");
            if (float.IsNaN(result.maxSlip) || float.IsInfinity(result.maxSlip) || result.maxSlip >= 20) report.errors.Add(label + "body slip reached 20 degrees or became invalid.");
            if (result.minGrounded != 4) report.errors.Add(label + "lost wheel contact during measurement.");
            if (result.direction * result.turnHeading <= 20) report.errors.Add(label + "insufficient heading response in the requested direction.");
            if (result.distanceTravelled <= 60 || result.forwardDistance <= 60 || result.finalSpeed <= 30) report.errors.Add(label + "insufficient forward progress or final speed.");
            if (result.maxRawThrottleError > .001f || !result.liftReapply && result.minRawThrottle < .999f) report.errors.Add(label + "raw throttle demand was altered.");
            if (!result.recoveredFullPower || result.finalTorqueScale < .95f || float.IsNaN(result.finalTorqueScale) || Mathf.Abs(result.finalSlip) >= 3 || Mathf.Abs(result.finalYawRate) >= 5 || result.finalAlignedFullPowerSeconds < .3f) report.errors.Add(label + "did not finish aligned at 95% power for the final continuous 0.3 seconds.");
            if (result.maximumTorqueDeviation > .01f || Mathf.Abs(result.finalMaximumTorque - result.authoredMaximumTorque) > .01f) report.errors.Add(label + "authored engine maximum torque changed by more than 0.01 Nm.");
            if (baseline != null && result.level == 0 && result.targetSpeed == 100 && result.launchSeconds > baseline.launchSeconds * 1.10f + .15f) report.errors.Add(label + "straight 0-100 launch regressed by more than 10% plus 0.15 seconds.");
        }
    }
}
