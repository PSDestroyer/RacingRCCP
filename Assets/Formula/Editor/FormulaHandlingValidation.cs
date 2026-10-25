using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated physics experiment: raw full-key inputs, identical stock vehicles and road.
[InitializeOnLoad]
public static class FormulaHandlingValidation
{
    private const string Active = "Formula.Handling.Active";
    private const string Folder = "Docs/AI/FormulaHandling";
    [Serializable] private sealed class Sample
    {
        public string tuning, maneuver;
        public float initialSpeed, maxSlipAngle, maxWheelAngle, finalSlipAngle, finalYawRate, finalSpeed, maxYawRate;
        public int minGrounded = 4;
    }
    [Serializable] private sealed class Report
    {
        public string status = "running", unityVersion;
        public List<Sample> measurements = new List<Sample>();
        public List<string> errors = new List<string>();
    }
    private static Report report;
    private static RCCP_CarController[] cars;
    private static Vector3[] positions;
    private static Sample[] samples;
    private static double entered;
    private static float began;
    private static int stage, maneuver;
    static FormulaHandlingValidation() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode; Application.logMessageReceived += Log; }

    private static void ConfigureControl(RCCP_CarController car, int profile)
    {
        if (profile == 0)
        {
            var steering = car.GetComponent<FormulaSteering>();
            if (steering != null) steering.enabled = false;
            car.Inputs.steeringCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(80, .55f), new Keyframe(180, .27f), new Keyframe(340, .14f));
            return;
        }
        if (!Mathf.Approximately(car.Inputs.steeringCurve.Evaluate(200), FormulaHandlingAuthoring.SteeringCurve().Evaluate(200))) throw new Exception("Corrected production prefab was not loaded.");
    }
    [MenuItem("Tools/Formula Career/Compare Formula Handling")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Active + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaHandling-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Active, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report { unityVersion = Application.unityVersion }; stage = 0; maneuver = 0; entered = EditorApplication.timeSinceStartup; Application.runInBackground = true;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        if (value == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Active, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
            string previous = SessionState.GetString(Active + ".Previous", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            Application.runInBackground = SessionState.GetBool(Active + ".Background", false);
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup - entered > 150) throw new Exception("Handling experiment timed out.");
            if (stage == 0)
            {
                foreach (var car in UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None)) car.gameObject.SetActive(false);
                foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) collider.enabled = false;
                var road = new GameObject("Handling test road"); var box = road.AddComponent<BoxCollider>(); box.size = new Vector3(5000, .1f, 5000); road.transform.position = new Vector3(0, -.05f, 0);
                cars = new RCCP_CarController[6]; positions = new Vector3[6];
                for (int i = 0; i < cars.Length; i++)
                {
                    positions[i] = new Vector3(-600 + i * 150, .5f, -500);
                    cars[i] = UnityEngine.Object.Instantiate(FormulaAssets.Current.vehiclePrefab, positions[i], Quaternion.identity).GetComponent<RCCP_CarController>();
                    cars[i].canControl = true;
                }
                began = Time.time; stage = 1;
            }
            else if (stage == 1 && Time.time - began > 1.5f && cars.All(c => c.GetComponent<FormulaVehicle>().IsReady))
            {
                samples = new Sample[cars.Length];
                for (int i = 0; i < cars.Length; i++)
                {
                    var car = cars[i]; car.GetComponent<FormulaVehicle>().ApplyDevelopment(0, 0, 0, 0, "balanced");
                    car.Stability.ABS = car.Stability.TCS = car.Stability.ESP = true;
                    Vector3 settled = positions[i]; settled.y = car.Rigid.position.y;
                    car.Rigid.position = settled; car.Rigid.rotation = Quaternion.identity;
                    car.Rigid.linearVelocity = Vector3.forward * (new[] { 140f, 200f, 260f }[i % 3] / 3.6f); car.Rigid.angularVelocity = Vector3.zero;
                    car.Gearbox.overrideGear = true; car.Gearbox.currentGear = i % 3 + 4;
                    car.Engine.engineRunning = true; car.Inputs.autoReverse = false;
                    ConfigureControl(car, i / 3);
                    samples[i] = new Sample { tuning = i / 3 == 0 ? "legacy_steering_control" : "corrected_prefab", maneuver = new[] { "key_tap", "key_hold", "lift_in_turn" }[maneuver], initialSpeed = new[] { 140f, 200f, 260f }[i % 3] };
                    report.measurements.Add(samples[i]);
                    car.Inputs.OverrideInputs(new RCCP_Inputs());
                }
                began = Time.time; stage = 2;
            }
            else if (stage == 2)
            {
                float age = Time.time - began;
                for (int i = 0; i < cars.Length; i++)
                {
                    var car = cars[i];
                    float hold = maneuver == 0 ? .18f : .65f;
                    car.Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = maneuver == 2 && age > .4f ? 0 : .25f, steerInput = age > .25f && age < .25f + hold ? 1 : 0 });
                    Vector3 local = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
                    var sample = samples[i];
                    sample.finalSlipAngle = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
                    sample.finalYawRate = Mathf.Abs(car.Rigid.angularVelocity.y) * Mathf.Rad2Deg; sample.finalSpeed = car.absoluteSpeed;
                    if (age > .2f)
                    {
                        sample.maxSlipAngle = Mathf.Max(sample.maxSlipAngle, sample.finalSlipAngle);
                        sample.maxYawRate = Mathf.Max(sample.maxYawRate, sample.finalYawRate);
                        sample.maxWheelAngle = Mathf.Max(sample.maxWheelAngle, Mathf.Abs(car.FrontAxle.steerAngle));
                        sample.minGrounded = Mathf.Min(sample.minGrounded, car.AllWheelColliders.Count(w => w.WheelCollider.isGrounded));
                    }
                }
                if (age > 3.5f)
                {
                    Write(); maneuver++;
                    if (maneuver < 3) { began = Time.time; stage = 1; }
                    else
                    {
                        foreach (var corrected in report.measurements.Where(s => s.tuning == "corrected_prefab"))
                        {
                            var legacy = report.measurements.Single(s => s.tuning == "legacy_steering_control" && s.maneuver == corrected.maneuver && s.initialSpeed == corrected.initialSpeed);
                            if (corrected.maxSlipAngle > 12 || corrected.maxSlipAngle > legacy.maxSlipAngle * .8f || corrected.minGrounded != 4)
                                throw new Exception("Keyboard handling regression at " + corrected.initialSpeed + " km/h / " + corrected.maneuver);
                        }
                        report.status = report.errors.Count == 0 ? "passed" : "failed"; Write(); EditorApplication.ExitPlaymode();
                    }
                }
            }
        }
        catch (Exception e) { report.errors.Add(e.Message); report.status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    private static void Log(string message, string trace, LogType type)
    { if (report != null && SessionState.GetBool(Active, false) && (type == LogType.Error || type == LogType.Exception)) report.errors.Add(message); }
    private static void Write() { Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/Comparison.json", JsonUtility.ToJson(report, true)); }
}
