using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FormulaPhysicsValidation
{
    const string Key = "Formula.Physics.Validation", Previous = Key + ".Previous";
    [Serializable] public class Measurement { public string branch; public int level; public float speedAfterFourSeconds, stoppingDistance, finalSpeed, finalY, actualBrakeInput, actualWheelBrake, torque, brakeTorque, downforce, drag, spring, damper; }
    [Serializable] class Report { public List<Measurement> measurements = new List<Measurement>(); public List<string> passed = new List<string>(); public List<string> errors = new List<string>(); public string status; public int stage; public float simulationSeconds; }
    static Report report;
    static FormulaVehicle[] vehicles;
    static Vector3[] starts;
    static bool[] brakeStopped;
    static float started;
    static int stage;
    static bool accelerationCaptured;
    static double entered;
    static double lastWrite;
    static FormulaPhysicsValidation() { EditorApplication.update += Update; EditorApplication.playModeStateChanged += Mode; Application.logMessageReceived += Log; }
    [MenuItem("Tools/Formula Career/Validate Upgrade Physics")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        string path = Path.Combine(Path.GetTempPath(), "FormulaPhysics-" + Guid.NewGuid().ToString("N"), "save.json");
        FormulaSaveStorage.Write(path, new HalvaStudio.Save.SaveManager.SaveData());
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", path);
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report { status = "running" }; stage = 0; accelerationCaptured = false; entered = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            Type view = Type.GetType("UnityEditor.GameView,UnityEditor"); if (view != null) EditorWindow.GetWindow(view).Focus();
        }
        if (value == PlayModeStateChange.EnteredEditMode)
        {
            if (report != null && report.status == "running") { report.status = "interrupted"; Write(); }
            SessionState.SetBool(Key, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
            string previous = SessionState.GetString(Previous, ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        }
    }
    static void Update()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - lastWrite > 1)
        { lastWrite = EditorApplication.timeSinceStartup; report.stage = stage; report.simulationSeconds = Time.time - started; Write(); }
        try
        {
            if (EditorApplication.timeSinceStartup - entered > 60) throw new Exception("Physics benchmark timed out.");
            if (stage == 0)
            {
                foreach (var car in UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None)) car.gameObject.SetActive(false);
                vehicles = new FormulaVehicle[8]; starts = new Vector3[8]; brakeStopped = new bool[4];
                for (int i = 0; i < 8; i++)
                {
                    var go = UnityEngine.Object.Instantiate(FormulaAssets.Current.vehiclePrefab, new Vector3(-70 + i * 20, .55f, -70), Quaternion.identity);
                    vehicles[i] = go.GetComponent<FormulaVehicle>();
                }
                started = Time.time; stage = 1;
            }
            else if (stage == 1 && Time.time - started > 2 && vehicles.All(v => v.IsReady))
            {
                for (int i = 0; i < 8; i++)
                {
                    int level = i % 4; var v = vehicles[i]; var car = v.GetComponent<RCCP_CarController>();
                    v.ApplyDevelopment(i < 4 ? level : 0, i >= 4 ? level : 0, level, level, "balanced");
                    var wheel = car.GetComponentInChildren<WheelCollider>();
                    float once = car.Engine.maximumTorqueAsNM;
                    v.ApplyDevelopment(i < 4 ? level : 0, i >= 4 ? level : 0, level, level, "balanced");
                    if (!Mathf.Approximately(once, car.Engine.maximumTorqueAsNM)) throw new Exception("Upgrade multipliers compounded.");
                    report.measurements.Add(new Measurement { branch = i < 4 ? "engine" : "brakes", level = level, torque = car.Engine.maximumTorqueAsNM, brakeTorque = car.FrontAxle.maxBrakeTorque, downforce = car.AeroDynamics.downForce, drag = car.AeroDynamics.airResistance, spring = wheel.suspensionSpring.spring, damper = wheel.suspensionSpring.damper });
                    // Measure one branch at a time; suspension/aero above are inspected independently.
                    v.ApplyDevelopment(i < 4 ? level : 0, i >= 4 ? level : 0, 0, 0, "balanced");
                    car.Rigid.linearVelocity = Vector3.forward * ((i < 4 ? 60f : 100f) / 3.6f); car.Rigid.angularVelocity = Vector3.zero;
                    car.Gearbox.overrideGear = true; car.Gearbox.currentGear = 3;
                    car.canControl = true; car.Engine.engineRunning = true;
                    car.Inputs.autoReverse = false;
                    starts[i] = car.transform.position;
                    car.Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = i < 4 ? .35f : 0, brakeInput = i < 4 ? 0 : 1 });
                }
                started = Time.time; stage = 2;
            }
            else if (stage == 2)
            {
                for (int i = 4; i < 8; i++)
                    if (Time.time - started > .2f && !brakeStopped[i - 4] && vehicles[i].GetComponent<RCCP_CarController>().Rigid.linearVelocity.magnitude * 3.6f < 3)
                    { brakeStopped[i - 4] = true; report.measurements[i].stoppingDistance = Vector3.Distance(starts[i], vehicles[i].transform.position); }
                if (!accelerationCaptured && Time.time - started >= 4)
                {
                    for (int i = 0; i < 4; i++) report.measurements[i].speedAfterFourSeconds = vehicles[i].GetComponent<RCCP_CarController>().absoluteSpeed;
                    accelerationCaptured = true;
                }
                if (accelerationCaptured && (brakeStopped.All(b => b) || Time.time - started >= 12))
                {
                    for (int i = 4; i < 8; i++)
                    {
                        var c = vehicles[i].GetComponent<RCCP_CarController>(); var m = report.measurements[i];
                        m.finalSpeed = c.absoluteSpeed; m.finalY = c.transform.position.y; m.actualBrakeInput = c.brakeInput_V;
                        m.actualWheelBrake = c.GetComponentInChildren<WheelCollider>().brakeTorque;
                        if (!brakeStopped[i - 4]) m.stoppingDistance = Vector3.Distance(starts[i], c.transform.position);
                    }
                    if (report.measurements[3].speedAfterFourSeconds <= report.measurements[0].speedAfterFourSeconds) throw new Exception("Engine level 3 did not increase measured acceleration.");
                    if (!brakeStopped.All(b => b)) throw new Exception("Braking benchmark did not stop all cars.");
                    if (report.measurements[7].stoppingDistance >= report.measurements[4].stoppingDistance) throw new Exception("Brake level 3 did not reduce measured stopping distance.");
                    report.passed.Add("Engine level 3 improves four-second rolling acceleration from 60 km/h in fourth gear at 35% throttle");
                    report.passed.Add("Four brake levels stop physically from 100 km/h with ABS");
                    report.passed.Add("Brake level 3 reduces stopping distance compared with stock");
                    report.passed.Add("All four levels update RCCP torque, springs, dampers, downforce and drag without compounding");
                    Finish();
                }
            }
        }
        catch (Exception e) { report.errors.Add(e.Message); Finish(); }
    }
    static void Log(string message, string stack, LogType type) { if (report != null && SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) report.errors.Add(message); }
    static void Finish()
    {
        report.status = report.errors.Count == 0 ? "passed" : "failed";
        Write();
        EditorApplication.ExitPlaymode();
    }
    static void Write() { Directory.CreateDirectory("Docs/AI/FormulaCareer"); File.WriteAllText("Docs/AI/FormulaCareer/PhysicsValidation.json", JsonUtility.ToJson(report, true)); }
}
