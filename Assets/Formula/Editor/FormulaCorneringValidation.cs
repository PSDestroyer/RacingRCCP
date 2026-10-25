using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runtime-only experiment. Every case gets a fresh car; production assets and saves are untouched.
[InitializeOnLoad]
public static class FormulaCorneringValidation
{
    private const string Key = "Formula.CorneringBalance.Active";
    private const string Output = "Docs/AI/FormulaHandling/CorneringBalance.json";
    private static FormulaCorneringValidationDriver driver;
    private static double started;
    private static Report report;

    [Serializable]
    public sealed class Sample
    {
        public string tuning, maneuver;
        public int developmentLevel, fixedSamples, renderFrames, minGrounded = 4;
        public bool assists;
        public float initialSpeed, finalSpeed, maxSlipAngle, finalSlipAngle, maxYawRate, finalYawRate;
        public float maxWheelAngle, signedHeading, finalLateralDisplacement, peakLateralDisplacement;
        public float firstRequestedSteerTime = -1, firstProcessedSteerTime = -1, duration;
    }

    [Serializable]
    public sealed class Report
    {
        public string status = "running", unityVersion;
        public string method = "Fresh vehicles on a flat road; fixed-step input requests and sampling, through the actual RCCP OverrideInputs/Update path. Velocity is seeded once before measurement, never during it. Four independent profiles run in separated lanes. Grounding is reported; transient unloading alone does not fail a case.";
        public float fixedDeltaTime;
        public bool isolatedSave = true;
        public List<Sample> measurements = new List<Sample>();
        public List<string> errors = new List<string>();
    }

    static FormulaCorneringValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Tools/Formula Career/Validate Cornering Balance")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".PreviousScene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        string previousSave = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSaveOverride", previousSave != null);
        SessionState.SetString(Key + ".SaveOverride", previousSave ?? "");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaCornering-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report { unityVersion = Application.unityVersion, fixedDeltaTime = Time.fixedDeltaTime };
            started = EditorApplication.timeSinceStartup;
            driver = null;
            Application.runInBackground = true;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        else if (value == PlayModeStateChange.EnteredEditMode)
        {
            if (report != null && report.status == "running")
            {
                report.status = "interrupted";
                Write();
            }
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSaveOverride", false) ? SessionState.GetString(Key + ".SaveOverride", "") : null);
            string previous = SessionState.GetString(Key + ".PreviousScene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            SessionState.SetBool(Key, false);
            driver = null;
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 360)
                throw new TimeoutException("Cornering balance experiment exceeded 360 seconds.");
            if (driver == null)
            {
                foreach (var car in UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None))
                    car.gameObject.SetActive(false);
                foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    collider.enabled = false;
                var road = new GameObject("Cornering validation flat road");
                road.transform.position = new Vector3(0, -.05f, 0);
                road.AddComponent<BoxCollider>().size = new Vector3(5000, .1f, 5000);
                driver = new GameObject("Cornering validation fixed-step driver").AddComponent<FormulaCorneringValidationDriver>();
                driver.Initialize(report, Write);
            }
            if (driver.Done)
            {
                report.status = report.errors.Count == 0 ? "passed" : "failed";
                Write();
                EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error)
        {
            report.errors.Add(error.ToString());
            report.status = "failed";
            Write();
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Log(string message, string trace, LogType type)
    {
        if (report != null && SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception))
            report.errors.Add(message);
    }

    private static void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
    }
}

// Runs after production steering/axle FixedUpdates, samples the prior simulation step,
// and requests the next input through RCCP's normal render-frame input propagation.
[DefaultExecutionOrder(30000)]
public sealed class FormulaCorneringValidationDriver : MonoBehaviour
{
    private static readonly float[] Speeds = { 60, 100, 140, 200, 260 };
    private static readonly string[] Maneuvers = { "key_tap", "key_hold", "direction_reversal", "lift_in_turn" };
    private FormulaCorneringValidation.Report report;
    private Action write;
    private RCCP_CarController[] cars;
    private FormulaCorneringValidation.Sample[] samples;
    private float[] lastYaw;
    private Vector3[] origins;
    private float elapsed;
    private int batch, stage, firstRenderFrame;
    public bool Done { get; private set; }

    public void Initialize(FormulaCorneringValidation.Report destination, Action save)
    {
        report = destination;
        write = save;
        SpawnBatch();
    }

    private void SpawnBatch()
    {
        bool launch = batch >= Speeds.Length * Maneuvers.Length;
        bool assists = batch != Speeds.Length * Maneuvers.Length + 1;
        int count = assists ? 4 : 2;
        cars = new RCCP_CarController[count];
        samples = new FormulaCorneringValidation.Sample[count];
        lastYaw = new float[count]; origins = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            bool production = assists ? i >= 2 : i == 1;
            int level = assists ? (i % 2) * 3 : 0;
            origins[i] = new Vector3(-750 + i * 500, .5f, -500);
            var car = Instantiate(FormulaAssets.Current.vehiclePrefab, origins[i], Quaternion.identity).GetComponent<RCCP_CarController>();
            cars[i] = car;
            car.GetAllComponents();
            car.canControl = true;
            car.Inputs.OverrideInputs(new RCCP_Inputs());
            var steering = car.GetComponentsInChildren<MonoBehaviour>(true).SingleOrDefault(c => c != null && c.GetType().FullName == "FormulaGame.FormulaSteering");
            if (steering == null) throw new InvalidOperationException("Production vehicle has no FormulaSteering component.");
            steering.enabled = production;
            if (!production) ApplyBaseline(car);
            samples[i] = new FormulaCorneringValidation.Sample
            {
                tuning = production ? "production" : "previous_restrictive_curve",
                developmentLevel = level, assists = assists,
                maneuver = launch ? "launch_full_throttle" : Maneuvers[batch / Speeds.Length],
                initialSpeed = launch ? 0 : Speeds[batch % Speeds.Length]
            };
        }
        elapsed = 0; stage = 0;
    }

    private static void ApplyBaseline(RCCP_CarController car)
    {
        car.Inputs.steeringCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(40, .8f), new Keyframe(80, .3f), new Keyframe(100, .15f), new Keyframe(140, .095f), new Keyframe(200, .045f), new Keyframe(260, .032f), new Keyframe(340, .024f));
        car.FrontAxle.steerSpeed = 3;
    }

    private void FixedUpdate()
    {
        if (report == null || Done) return;
        try
        {
            elapsed += Time.fixedDeltaTime;
            if (stage == 0)
            {
                if (elapsed < 1.5f || cars.Any(c => !c.GetComponent<FormulaVehicle>().IsReady)) return;
                for (int i = 0; i < cars.Length; i++)
                {
                    var car = cars[i]; var sample = samples[i];
                    int level = sample.developmentLevel;
                    car.GetComponent<FormulaVehicle>().ApplyDevelopment(level, level, level, level, "balanced");
                    // Development/Start may refresh the tuning even while its component is disabled.
                    if (sample.tuning == "previous_restrictive_curve") ApplyBaseline(car);
                    car.Stability.ABS = car.Stability.TCS = car.Stability.ESP = sample.assists;
                    car.Stability.steeringHelper = car.Stability.tractionHelper = sample.assists;
                    if (!sample.assists) car.Stability.angularDragHelper = false;
                    car.Inputs.counterSteering = car.Inputs.steeringLimiter = sample.assists;
                    car.Inputs.autoReverse = false;
                    car.Engine.engineRunning = true;
                    car.Gearbox.overrideGear = sample.maneuver != "launch_full_throttle";
                    car.Gearbox.currentGear = sample.initialSpeed == 0 ? 0 : sample.initialSpeed <= 100 ? 2 : sample.initialSpeed <= 140 ? 4 : sample.initialSpeed <= 200 ? 5 : 6;
                    // Controlled initial condition only; no pose/velocity writes after measurement begins.
                    origins[i] = new Vector3(origins[i].x, car.Rigid.position.y, origins[i].z);
                    car.Rigid.position = origins[i]; car.Rigid.rotation = Quaternion.identity;
                    car.Rigid.linearVelocity = Vector3.forward * (sample.initialSpeed / 3.6f);
                    car.Rigid.angularVelocity = Vector3.zero;
                    lastYaw[i] = 0;
                    report.measurements.Add(sample);
                }
                firstRenderFrame = Time.frameCount;
                elapsed = 0; stage = 1;
                RequestInputs(0);
                return;
            }
            SamplePhysics();
            if (elapsed < 3f)
            {
                RequestInputs(elapsed);
                return;
            }
            foreach (var car in cars) { car.gameObject.SetActive(false); Destroy(car.gameObject); }
            write();
            batch++;
            if (batch < Speeds.Length * Maneuvers.Length + 2) SpawnBatch();
            else { CheckResults(); Done = true; }
        }
        catch (Exception error)
        {
            report.errors.Add(error.ToString());
            Done = true;
        }
    }

    private void RequestInputs(float time)
    {
        for (int i = 0; i < cars.Length; i++)
        {
            var sample = samples[i];
            bool launch = sample.maneuver == "launch_full_throttle";
            float stop = sample.maneuver == "key_tap" ? .43f : 1.15f;
            float steer = !launch && time >= .25f && time < stop ? 1 : 0;
            if (sample.maneuver == "direction_reversal" && time >= .7f) steer *= -1;
            float throttle = launch ? 1 : sample.maneuver == "lift_in_turn" && time >= .6f ? 0 : .25f;
            cars[i].Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = throttle, steerInput = steer });
            if (steer != 0 && sample.firstRequestedSteerTime < 0) sample.firstRequestedSteerTime = time;
        }
    }

    private void SamplePhysics()
    {
        for (int i = 0; i < cars.Length; i++)
        {
            var car = cars[i]; var sample = samples[i];
            Vector3 local = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
            float speed = car.Rigid.linearVelocity.magnitude * 3.6f;
            sample.finalSlipAngle = speed > 5 ? Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) : 0;
            sample.finalYawRate = car.Rigid.angularVelocity.y * Mathf.Rad2Deg;
            sample.maxSlipAngle = Mathf.Max(sample.maxSlipAngle, sample.finalSlipAngle);
            sample.maxYawRate = Mathf.Max(sample.maxYawRate, Mathf.Abs(sample.finalYawRate));
            sample.maxWheelAngle = Mathf.Max(sample.maxWheelAngle, Mathf.Abs(car.FrontAxle.steerAngle));
            sample.minGrounded = Mathf.Min(sample.minGrounded, car.AllWheelColliders.Count(w => w.WheelCollider.isGrounded));
            float yaw = car.Rigid.rotation.eulerAngles.y;
            sample.signedHeading += Mathf.DeltaAngle(lastYaw[i], yaw); lastYaw[i] = yaw;
            sample.finalLateralDisplacement = car.Rigid.position.x - origins[i].x;
            sample.peakLateralDisplacement = Mathf.Max(sample.peakLateralDisplacement, Mathf.Abs(sample.finalLateralDisplacement));
            sample.finalSpeed = speed; sample.duration = elapsed;
            sample.fixedSamples++; sample.renderFrames = Time.frameCount - firstRenderFrame;
            if (sample.firstRequestedSteerTime >= 0 && sample.firstProcessedSteerTime < 0 && Mathf.Abs(car.steerInput_P) > .001f)
                sample.firstProcessedSteerTime = elapsed;
        }
    }

    private void CheckResults()
    {
        foreach (var sample in report.measurements.Where(s => s.tuning == "production"))
        {
            string label = sample.maneuver + " / " + sample.initialSpeed + " km/h / L" + sample.developmentLevel + " / assists " + sample.assists;
            if (float.IsNaN(sample.maxSlipAngle) || float.IsNaN(sample.signedHeading) || sample.maxSlipAngle >= 15)
                report.errors.Add("Spin/stability limit exceeded: " + label);
            if (sample.maneuver != "launch_full_throttle" && sample.firstProcessedSteerTime < 0)
                report.errors.Add("Steering input was not processed: " + label);
            if (sample.maneuver == "key_hold")
            {
                float minimumHeading = sample.initialSpeed <= 140 ? 3 : 1.5f;
                if (sample.signedHeading < minimumHeading)
                    report.errors.Add("Insufficient sustained turning response: " + label);
                if (sample.initialSpeed == 100 || sample.initialSpeed == 140 || sample.initialSpeed == 200)
                {
                    var baseline = report.measurements.Single(s => s.tuning == "previous_restrictive_curve" && s.maneuver == sample.maneuver && s.initialSpeed == sample.initialSpeed && s.developmentLevel == sample.developmentLevel);
                    if (sample.signedHeading < baseline.signedHeading * 1.02f)
                        report.errors.Add("Sustained turning did not improve by 2% over restrictive baseline: " + label);
                }
            }
            if (sample.maneuver == "launch_full_throttle" && (Mathf.Abs(sample.signedHeading) > 5 || sample.peakLateralDisplacement > 1 || sample.finalSpeed < 30))
                report.errors.Add("Straight launch exceeded drift limits or failed to reach 30 km/h: " + label);
        }
    }
}
