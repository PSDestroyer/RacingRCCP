using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FormulaTransmissionValidation
{
    private const string Key = "Formula.Transmission.Validation", Folder = "Docs/AI/FormulaTransmission";
    private static RCCP_CarController[] cars;
    private static FormulaTransmissionProbe[] probes;
    private static FormulaTransmissionProbe.Metrics[] metrics;
    private static readonly List<string> errors = new List<string>(), checks = new List<string>();
    private static int phase, frameRate;
    private static float began;
    private static double started, nextWrite;
    private static string status;
    private static int airborneGear, airborneRequests;
    static FormulaTransmissionValidation()
    { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode; Application.logMessageReceived += Log; }
    [MenuItem("Tools/Formula Career/Drive Validate Formula Transmission")]
    public static void Run() => Begin(60);
    [MenuItem("Tools/Formula Career/Drive Validate Formula Transmission at 30 FPS")]
    public static void Run30() => Begin(30);
    [MenuItem("Tools/Formula Career/Drive Validate Formula Transmission at 120 FPS")]
    public static void Run120() => Begin(120);
    private static void Begin(int fps)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetInt(Key + ".FPS", Application.targetFrameRate); SessionState.SetInt(Key + ".VSync", QualitySettings.vSyncCount);
        SessionState.SetInt(Key + ".RequestedFPS", fps);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaTransmission-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = 0; errors.Clear(); checks.Clear(); cars = null; probes = null; metrics = null;
            status = "running"; started = EditorApplication.timeSinceStartup; began = Time.time; nextWrite = 0;
            frameRate = SessionState.GetInt(Key + ".RequestedFPS", 60);
            Time.timeScale = 1; AudioListener.pause = false; Application.runInBackground = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = frameRate;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus(); Write();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1; AudioListener.pause = false;
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            Application.targetFrameRate = SessionState.GetInt(Key + ".FPS", -1); QualitySettings.vSyncCount = SessionState.GetInt(Key + ".VSync", 0);
            string save = SessionState.GetString(Key + ".Save", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(save) ? null : save);
            string previous = SessionState.GetString(Key + ".Previous", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Key, false);
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 180, "Transmission validation timed out");
            Require(errors.Count == 0, "Runtime error during transmission validation");
            if (EditorApplication.timeSinceStartup >= nextWrite) { nextWrite = EditorApplication.timeSinceStartup + 2; Write(); }
            float age = Time.time - began;
            if (phase == 0)
            {
                foreach (var car in UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None)) car.gameObject.SetActive(false);
                foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) collider.enabled = false;
                var floor = new GameObject("Play-only transmission test straight"); floor.transform.position = new Vector3(0, -.05f, 0);
                floor.AddComponent<BoxCollider>().size = new Vector3(600, .1f, 16000);
                RCCP_SceneManager.Instance.registerLastVehicleAsPlayer = false;
                cars = new RCCP_CarController[2]; probes = new FormulaTransmissionProbe[2];
                for (int i = 0; i < cars.Length; i++)
                {
                    cars[i] = UnityEngine.Object.Instantiate(FormulaAssets.Current.vehiclePrefab, new Vector3(i == 0 ? -50 : 50, .4f, -2000), Quaternion.identity).GetComponent<RCCP_CarController>();
                    if (i == 0) cars[i].GetComponent<FormulaTransmission>().enabled = false;
                    cars[i].SetCanControl(true); cars[i].SetEngine(true); cars[i].Inputs.autoReverse = false;
                    cars[i].Inputs.OverrideInputs(new RCCP_Inputs { brakeInput = 1 });
                }
                Next(1);
            }
            else if (phase == 1)
            {
                if (age < 2.5f || cars.Any(c => !c.GetComponent<FormulaVehicle>().IsReady)) return;
                Require(cars[1].GetComponent<FormulaTransmission>().ControlActive, "New controller not active");
                for (int i = 0; i < cars.Length; i++)
                {
                    cars[i].GetComponent<FormulaVehicle>().ApplyDevelopment(0, 0, 0, 0, "balanced");
                    probes[i] = cars[i].gameObject.AddComponent<FormulaTransmissionProbe>();
                    probes[i].Begin(i == 0 ? "original_RCCP_control" : "Formula_production"); probes[i].Phase = "accelerate";
                    cars[i].Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = 1 });
                }
                checks.Add("Two stock Generic cars, unchanged wheel/rigidbody/gear ratios and power, same flat road, full throttle; original RCCP control vs production Formula controller."); Next(2);
            }
            else if (phase == 2)
            {
                if (cars[1].Gearbox.currentGear < 7 || age < 1 || cars[1].Gearbox.shiftingNow ||
                    probes[1].Result.samples.Count(s => s.phase == "accelerate" && s.gear == 8 && !s.shifting) < 20)
                { Require(age < 65, "Production car did not naturally accelerate through all eight gears"); return; }
                foreach (var probe in probes) probe.Phase = "brake";
                foreach (var car in cars) car.Inputs.OverrideInputs(new RCCP_Inputs { brakeInput = 1 });
                checks.Add("Production car reached eighth gear using its automatic decisions; no speed, RPM or gear seeding during acceleration."); Next(3);
            }
            else if (phase == 3)
            {
                if (cars[1].absoluteSpeed > .5f || cars[1].Gearbox.currentGear != 0 || cars[1].Gearbox.shiftingNow)
                { Require(age < 20, "Production car did not brake and return through the gears to first"); return; }
                metrics = probes.Select(p => p.Finish()).ToArray();
                var result = metrics[1];
                Require(result.accelerationGears.SequenceEqual(Enumerable.Range(1, 8)), "An acceleration gear was skipped");
                Require(result.brakingGears.SequenceEqual(Enumerable.Range(1, 8)), "A braking gear was skipped");
                Require(result.shifts.Count(s => s.to > s.from) == 7 && result.shifts.Count(s => s.to < s.from) == 7, "Unexpected shifts or gear hunting during the straight run");
                Require(result.longestLimiterSeconds.Take(7).Max() <= .15f, "Production lingers on the limiter before top gear");
                Require(result.longestNearRedlineEpisode.Take(7).Max() <= .15f, "Repeated limiter pulses form a long high-RPM plateau");
                Require(result.maxShiftSeconds <= .15f, "Shift completion exceeds150ms");
                Require(result.maxClutchEngagementSeconds <= .12f, "Clutch engagement exceeds120ms after shifting");
                Require(result.shifts.All(s => Mathf.Abs(s.to - s.from) == 1), "Transmission skips gears");
                checks.Add("Natural 1–8 acceleration and 8–1 braking: seven upshifts and seven downshifts, no skipped gear or hunting, bounded limiter dwell and prompt clutch engagement.");
                // Subsequent checks are explicitly controlled Play-only fixtures.
                cars[0].gameObject.SetActive(false); cars[1].Inputs.OverrideInputs(new RCCP_Inputs());
                cars[1].Gearbox.ShiftToN(); Next(4);
            }
            else if (phase == 4)
            {
                if (age < .2f) return;
                Require(cars[1].Gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.Neutral && cars[1].Clutch.clutchInput > .98f, "Neutral does not disengage clutch");
                cars[1].Gearbox.ShiftReverse(); Next(5);
            }
            else if (phase == 5)
            {
                if (age < .3f) return;
                Require(cars[1].Gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.InReverseGear, "Reverse state failed");
                cars[1].Inputs.OverrideInputs(new RCCP_Inputs { brakeInput = .5f }); Next(6);
            }
            else if (phase == 6)
            {
                if (age < 1.5f) return;
                Require(Vector3.Dot(cars[1].Rigid.linearVelocity, cars[1].transform.forward) < -.5f, "Reverse did not transmit torque");
                checks.Add("Neutral disengages the clutch; a public reverse request followed by normal reversed pedal input drives the car backwards.");
                var car = cars[1]; car.Rigid.isKinematic = true;
                car.Gearbox.OverrideGear(2, 0, RCCP_Gearbox.CurrentGearState.GearState.Neutral); Next(7);
            }
            else if (phase == 7)
            {
                if (age < .2f) return;
                var car = cars[1];
                Require(!car.GetComponent<FormulaTransmission>().ControlActive && !car.Clutch.overrideClutch && Mathf.Approximately(car.Engine.engineCouplingToWheelsRate, 1.5f), "External gear override did not restore RCCP ownership");
                car.Gearbox.DisableOverride(); car.Gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
                car.Rigid.position += Vector3.up * 20; car.Rigid.isKinematic = false; car.Rigid.linearVelocity = car.transform.forward * 28;
                car.Engine.OverrideRPM(12000); car.Engine.DisableOverride();
                car.Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = 1 });
                airborneGear = car.Gearbox.currentGear; airborneRequests = car.GetComponent<FormulaTransmission>().AutomaticUpshifts; Next(8);
            }
            else if (phase == 8)
            {
                if (age < .5f) return;
                var car = cars[1]; var transmission = car.GetComponent<FormulaTransmission>();
                Require(transmission.ControlActive && transmission.GroundedWheels == 0 && car.Gearbox.currentGear == airborneGear && transmission.AutomaticUpshifts == airborneRequests, "Airborne fixture changed gear automatically");
                transmission.enabled = false;
                Require(!car.Clutch.overrideClutch && Mathf.Approximately(car.Engine.engineCouplingToWheelsRate, 1.5f), "Disable did not restore the original clutch/coupling");
                checks.Add("Controlled airborne high-RPM fixture does not upshift. External override and component disable restore original RCCP ownership and tuning.");
                status = "passed"; Write(); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception failure)
        {
            errors.Add(failure.ToString()); status = "failed";
            if (metrics == null && probes != null) metrics = probes.Where(p => p != null).Select(p => p.Finish()).ToArray();
            Write(); EditorApplication.ExitPlaymode();
        }
    }
    private static void Next(int target) { phase = target; began = Time.time; Write(); }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private static void Log(string message, string trace, LogType type)
    { if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + trace); }
    private static void Write()
    {
        Directory.CreateDirectory(Folder);
        var progress = cars == null ? null : cars.Where(c => c != null).Select(c => new { speed = c.absoluteSpeed, gear = c.Gearbox.currentGear + 1,
            rpm = c.Engine.engineRPM, clutch = c.Clutch.clutchInput, active = c.GetComponent<FormulaTransmission>().ControlActive }).ToArray();
        File.WriteAllText(Folder + "/Validation-" + frameRate + ".json", JsonConvert.SerializeObject(new
        { status, unity = Application.unityVersion, requestedFrameRate = frameRate, phase, isolatedSave = true,
            method = "Production stock prefab vs original RCCP control; actual full-throttle straight acceleration and braking with no gear/RPM/speed seeding. Separate labelled neutral/reverse, external ownership and airborne fixtures.",
            progress, checks, errors, metrics }, Formatting.Indented));
    }
}
