using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Physical three-lap race; no teleports, invented checkpoints or scripted finish.
[InitializeOnLoad]
public static class FormulaAIThreeLapValidation
{
    const string Key = "Formula.AIThreeLap";
    static string Folder => Environment.GetEnvironmentVariable("FORMULA_AI_REPORT_PATH") ?? "Docs/AI/FormulaAIThreeLap";
    static string Track => Environment.GetEnvironmentVariable("FORMULA_AI_TEST_TRACK") ?? "track_01";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Progress = typeof(GamePlayManager).GetMethod("GetRacerProgressMeters", Private);
    static readonly MethodInfo Corner = typeof(RCCP_AI).GetMethod("GetUpcomingCornerAngle", Private);
    static readonly MethodInfo PathError = typeof(RCCP_AI).GetMethod("GetCurrentPathError", Private);
    static readonly List<string> errors = new List<string>();
    static readonly List<object> laps = new List<object>();
    static GamePlayManager game;
    static GamePlayManager.RaceRacer[] racers;
    static RCCP_AI[] drivers;
    static int phase, lastFrame;
    static double began, nextReport;
    static float nextSample;
    static string status;
    static StreamWriter csv;
    static Stats[] stats;
    static object result;
    class Stats
    {
        public int lastLap, samples, straightSamples, reversals, wobbleWindows;
        public float maxSpeed, maxLateralError, straightSeconds, lastSign, lastStraightAt = -10, lastLapAt;
        public float maxGapBehind;
        public Queue<float> flips = new Queue<float>();
    }
    static FormulaAIThreeLapValidation()
    {
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += (message, trace, type) =>
        { if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + trace); };
    }
    [MenuItem("Tools/Formula Career/Drive Validate Three Lap AI")]
    public static void Run() => Start(false);
    [MenuItem("Tools/Formula Career/Drive Validate Three Lap AI Catch Up")]
    public static void RunCatchUp() => Start(true);
    static void Start(bool fasterReference)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Key + ".FasterReference", fasterReference);
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaAIThreeLap-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            errors.Clear(); laps.Clear(); result = null; phase = 0; lastFrame = -1; status = "starting";
            began = EditorApplication.timeSinceStartup; nextReport = 0; nextSample = 0; Application.runInBackground = true;
            csv = new StreamWriter(Folder + "/Telemetry.csv"); csv.WriteLine("time,racer,lap,checkpoint,progress,speed,steer,yawRate,cornerAngle,lateralError,gapToPlayer,recoveries,x,z,pace");
            Write();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            csv?.Dispose(); csv = null; Time.timeScale = 1; AudioListener.pause = false;
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            string save = SessionState.GetString(Key + ".Save", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(save) ? null : save);
            string scene = SessionState.GetString(Key + ".Scene", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            SessionState.SetBool(Key, false);
            if (Application.isBatchMode) EditorApplication.Exit(status == "passed" ? 0 : 1);
        }
    }
    static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate(); if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
        try
        {
            Require(errors.Count == 0, "Runtime errors; see report");
            Require(EditorApplication.timeSinceStartup - began < 900, "Three-lap race timed out");
            if (phase == 0)
            {
                var menu = Object.FindFirstObjectByType<FormulaMainMenu>();
                if (menu == null || menu.CurrentPage != "Acasă") return;
                Require(FormulaSession.Launch("race:" + Track, "quick") == null, "Race launch failed"); phase = 1; status = "loading"; Write();
            }
            else if (phase == 1)
            {
                game = Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || !game.FormulaRaceStarted || game.CarController == null || !game.CarController.GetComponent<FormulaVehicle>().IsReady) return;
                Require(game.totalRaceLaps == 3 && game.aiRacers.Length == 3, "Expected a normal three-lap race with three opponents");
                FormulaSession.PrepareAgent(game.CarController.gameObject);
                var driver = game.CarController.gameObject.AddComponent<RCCP_AI>();
                driver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; driver.waypointsContainer = game.raceWaypoints;
                game.GetComponent<FormulaCircuit>().ConfigureDriver(driver);
                if (SessionState.GetBool(Key + ".FasterReference", false)) driver.GetComponent<FormulaAIPace>().PacingMultiplier = 1.25f;
                var player = (GamePlayManager.RaceRacer)typeof(GamePlayManager).GetField("playerRacer", Private).GetValue(game);
                racers = new[] { player }.Concat(game.aiRacers).ToArray(); drivers = new[] { driver }.Concat(game.aiRacers.Select(r => r.aiDriver)).ToArray();
                stats = racers.Select(r => new Stats()).ToArray();
                File.WriteAllText(Folder + "/RuntimeWaypoints.json", JsonConvert.SerializeObject(
                    game.raceWaypoints.waypoints.Select(w => new[] { w.transform.position.x, w.transform.position.y, w.transform.position.z }).ToArray()));
                // Fixed physics step remains unchanged; only wall-clock execution is accelerated.
                Time.timeScale = 2; phase = 2; status = "driving"; Write();
            }
            else if (phase == 2)
            {
                if (Time.time >= nextSample) { nextSample = Time.time + .1f; Sample(); }
                if (EditorApplication.timeSinceStartup >= nextReport) { nextReport = EditorApplication.timeSinceStartup + 5; Write(); }
                if (game.FormulaCompletedLaps < 3 || Object.FindFirstObjectByType<FormulaResultsView>() == null) return;
                Sample();
                float length = (float)typeof(GamePlayManager).GetField("raceTrackLength", Private).GetValue(game);
                float playerProgress = Distance(0);
                var gaps = Enumerable.Range(1, 3).Select(i => playerProgress - Distance(i)).ToArray();
                result = new { elapsed = game.FormulaElapsed, playerLaps = game.FormulaCompletedLaps, validLaps = FormulaSession.ValidLaps,
                    opponentLaps = game.aiRacers.Select(r => r.completedLaps).ToArray(), gapsMeters = gaps, gapsInLaps = gaps.Select(g => g / length).ToArray(),
                    recoveries = drivers.Skip(1).Select(d => d.GetComponent<FormulaAIRecovery>()?.RecoveryCount ?? 0).ToArray(),
                    finishPosition = FormulaProgression.State.lastReceipt?.finishPosition, trackLength = length };
                Require(FormulaSession.ValidLaps == 3, "Reference driver did not complete three valid laps");
                Require(gaps.All(g => g < length * .25f), "An opponent finished more than a quarter lap behind the reference driver");
                Require(stats.Skip(1).All(s => s.straightSamples > 100 && s.wobbleWindows == 0), "Sustained straight-line steering oscillations detected, or insufficient straight samples");
                Require(drivers.Skip(1).All(d => (d.GetComponent<FormulaAIRecovery>()?.RecoveryCount ?? 0) == 0), "An opponent required recovery transport");
                status = "passed"; Write(); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception ex) { errors.Add(ex.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    static float Distance(int i) => (float)Progress.Invoke(game, new object[] { racers[i] });
    static void Sample()
    {
        float time = game.FormulaElapsed, playerProgress = Distance(0);
        for (int i = 0; i < racers.Length; i++)
        {
            var r = racers[i]; var d = drivers[i]; var car = d.CarController; var s = stats[i];
            float angle = (float)Corner.Invoke(d, new object[] { 30f });
            float lateral = (float)PathError.Invoke(d, null), gap = playerProgress - Distance(i);
            float steer = d.inputs.steerInput, yaw = car.Rigid.angularVelocity.y * Mathf.Rad2Deg;
            s.samples++; s.maxSpeed = Mathf.Max(s.maxSpeed, car.absoluteSpeed); s.maxLateralError = Mathf.Max(s.maxLateralError, lateral); s.maxGapBehind = Mathf.Max(s.maxGapBehind, gap);
            bool straight = car.absoluteSpeed >= 60 && angle < 8 && !r.finished;
            if (straight)
            {
                s.straightSamples++; s.straightSeconds += .1f;
                if (time - s.lastStraightAt > .3f) { s.flips.Clear(); s.lastSign = 0; }
                if (Mathf.Abs(steer) > .12f)
                {
                    float sign = Mathf.Sign(steer);
                    if (s.lastSign != 0 && sign != s.lastSign) { s.reversals++; s.flips.Enqueue(time); }
                    s.lastSign = sign;
                }
                while (s.flips.Count > 0 && time - s.flips.Peek() > 2) s.flips.Dequeue();
                if (s.flips.Count >= 4) { s.wobbleWindows++; s.flips.Clear(); }
                s.lastStraightAt = time;
            }
            if (r.completedLaps > s.lastLap)
            { laps.Add(new { racer = i, lap = r.completedLaps, lapSeconds = time - s.lastLapAt, raceSeconds = time, gapMeters = gap }); s.lastLap = r.completedLaps; s.lastLapAt = time; }
            float pace = i == 0 ? d.GetComponent<FormulaAIPace>().PacingMultiplier : r.pacingMultiplier;
            csv.WriteLine(FormattableString.Invariant($"{time:F3},{i},{r.completedLaps},{r.currentWaypointIndex},{Distance(i):F3},{car.absoluteSpeed:F3},{steer:F4},{yaw:F3},{angle:F3},{lateral:F3},{gap:F3},{d.GetComponent<FormulaAIRecovery>()?.RecoveryCount ?? 0},{car.transform.position.x:F3},{car.transform.position.z:F3},{pace:F4}"));
        }
    }
    static void Write()
    {
        csv?.Flush();
        File.WriteAllText(Folder + "/Validation.json", JsonConvert.SerializeObject(new { status, phase, elapsed = game == null ? 0 : game.FormulaElapsed,
            playerLaps = game == null ? 0 : game.FormulaCompletedLaps, opponentLaps = game == null ? null : game.aiRacers?.Select(r => r.completedLaps).ToArray(),
            stats, laps, result, errors, reference = "stock player car, automated RCCP driver",
            referencePace = SessionState.GetBool(Key + ".FasterReference", false) ? 1.25f : 1f, track = Track, timeScale = 2,
            fixedStepUnchanged = true, scriptedFinish = false, isolatedSave = true, switchHardwareTested = false }, Formatting.Indented));
    }
}
