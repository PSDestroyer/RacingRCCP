using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FormulaRouteFlowPlayValidation
{
    private const string Key = "Formula.RouteFlow.Play", Folder = "Docs/AI/FormulaRouteFlow";
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static readonly List<object> laps = new List<object>();
    private static readonly List<object> invalidations = new List<object>();
    private static bool sawInvalid;
    private static int phase, track, previousGameId;
    private static double began, lastWrite;
    private static GamePlayManager game;
    private static RCCP_Inputs inputs;
    private static string status;
    private static float reverseSpeed, reverseDistance;
    private static Vector3 reverseStart;
    private static readonly string[] Tracks = { "track_01", "rtg_01" };
    static FormulaRouteFlowPlayValidation()
    {
        EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick; Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Drive Validate Player Route Flow")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetInt(Key + ".Fps", Application.targetFrameRate); SessionState.SetInt(Key + ".VSync", QualitySettings.vSyncCount);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaRouteFlow-" + Guid.NewGuid().ToString("N"), "save.json"));
        Directory.CreateDirectory(Folder); SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); laps.Clear(); invalidations.Clear(); sawInvalid = false; phase = track = previousGameId = 0; game = null; status = "running"; reverseSpeed = reverseDistance = 0;
            Application.runInBackground = true; Time.timeScale = 1; began = EditorApplication.timeSinceStartup;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus(); Write();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1; Application.targetFrameRate = SessionState.GetInt(Key + ".Fps", -1); QualitySettings.vSyncCount = SessionState.GetInt(Key + ".VSync", 0);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
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
            double age = EditorApplication.timeSinceStartup - began;
            Require(age < (phase == 7 ? 300 : 90), "Route flow timed out in phase " + phase);
            Require(errors.Count == 0, "Runtime error during route flow validation");
            if (EditorApplication.timeSinceStartup - lastWrite > 5) { lastWrite = EditorApplication.timeSinceStartup; Write(); }
            if (phase == 0)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>(); if (menu == null || menu.CurrentPage != "Acasă" || age < 1) return;
                Launch("track_01"); Next(1);
            }
            else if (phase == 1)
            {
                if (!Ready("track_01")) return;
                inputs = new RCCP_Inputs { brakeInput = 1 }; game.CarController.Inputs.OverrideInputs(inputs);
                reverseStart = game.CarController.transform.position; Next(2);
            }
            else if (phase == 2)
            {
                inputs.brakeInput = 1; inputs.throttleInput = 0; game.CarController.Inputs.OverrideInputs(inputs);
                reverseSpeed = Mathf.Max(reverseSpeed, game.CarController.absoluteSpeed); reverseDistance = Vector3.Distance(reverseStart, game.CarController.transform.position);
                Require(game.FormulaCompletedLaps == 0, "Backing away from the grid awarded a lap");
                if (!game.FormulaWrongWay) return;
                Require(game.CarController.reversingNow && game.CarController.absoluteSpeed >= 10, "Wrong-way warning did not come from real reverse motion");
                Next(3);
            }
            else if (phase == 3)
            {
                inputs.brakeInput = 1; game.CarController.Inputs.OverrideInputs(inputs);
                if (age < .6) return;
                var hud = UnityEngine.Object.FindFirstObjectByType<FormulaRaceView>();
                Require(hud.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "WRONG WAY"), "Wrong-way warning is absent from the actual HUD");
                Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Folder + "/Wrong-Way.png");
                checks.Add("Real RCCP brake/reverse input backs the car away from the grid; sustained opposite travel shows WRONG WAY and awards no laps"); Next(4);
            }
            else if (phase == 4)
            {
                // Formula axles deliberately have no handbrake. Apply the RCCP
                // service brake directly, without automatic reverse swapping.
                game.CarController.Inputs.overrideExternalInputs = true;
                inputs.brakeInput = 1; inputs.throttleInput = inputs.handbrakeInput = 0; game.CarController.Inputs.OverrideInputs(inputs);
                if (game.FormulaWrongWay || game.CarController.absoluteSpeed > 1 || age < 1) return;
                checks.Add("Stopping actual reverse motion clears WRONG WAY, independently of the selected gear"); Launch(Tracks[track]); Next(5);
            }
            else if (phase == 5)
            {
                if (!Ready(Tracks[track])) return;
                FormulaSession.PrepareAgent(game.CarController.gameObject);
                var driver = game.CarController.gameObject.AddComponent<RCCP_AI>();
                driver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; driver.waypointsContainer = game.raceWaypoints;
                game.GetComponent<FormulaCircuit>().ConfigureDriver(driver);
                sawInvalid = false;
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; Time.timeScale = 3; Next(7);
            }
            else if (phase == 7)
            {
                if (FormulaSession.LapInvalid && !sawInvalid)
                {
                    const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var progress = (FormulaRouteProgress)typeof(GamePlayManager).GetField("formulaRouteProgress", fields).GetValue(game);
                    invalidations.Add(new { track = Tracks[track], elapsed = game.FormulaElapsed, checkpoint = game.FormulaNextCheckpoint, routeReason = progress.EditorLastInvalidation, outsideSeconds = (float)typeof(FormulaRaceUI).GetField("outsideSeconds", fields).GetValue(game.GetComponent<FormulaRaceUI>()) });
                    sawInvalid = true; Write();
                }
                if (game.FormulaCompletedLaps < 1) return;
                Require(FormulaSession.ValidLaps >= 1 && FormulaSession.LastLapValid, "Naturally driven lap did not remain valid on " + Tracks[track]);
                Require(!game.FormulaWrongWay, "Normal forward driving triggered WRONG WAY");
                laps.Add(new { track = Tracks[track], completedLaps = game.FormulaCompletedLaps, validLaps = FormulaSession.ValidLaps, bestLap = FormulaSession.BestLap, elapsed = game.FormulaElapsed, nextCheckpoint = game.FormulaNextCheckpoint });
                Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Folder + "/Lap-" + Tracks[track] + ".png"); Next(8);
            }
            else if (phase == 8)
            {
                track++;
                if (track < Tracks.Length) { Time.timeScale = 1; Launch(Tracks[track]); Next(5); }
                else { checks.Add("The real RCCP player completes a valid lap on Racing Visuals and RTG using production lap progression, without seeded poses, velocities or lap completion"); status = "passed"; Write(); EditorApplication.ExitPlaymode(); }
            }
        }
        catch (Exception failure) { errors.Add(failure.Message); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    private static bool Ready(string id)
    {
        game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
        return game != null && game.GetInstanceID() != previousGameId && game.GetComponent<FormulaCircuit>()?.trackId == id && game.FormulaRaceStarted && game.CarController != null && game.CarController.GetComponent<FormulaVehicle>().IsReady;
    }
    private static void Launch(string id)
    {
        var old = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>(); previousGameId = old != null ? old.GetInstanceID() : 0;
        string error = FormulaSession.Launch("trial:" + id, "time_trial"); Require(error == null, error);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Next(int next) { phase = next; began = EditorApplication.timeSinceStartup; Write(); }
    private static void Log(string message, string trace, LogType type) { if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception)) errors.Add(message); }
    private static void Write() => File.WriteAllText(Folder + "/PlayValidation.json", JsonConvert.SerializeObject(new { status, unity = Application.unityVersion, phase, isolatedSave = true, method = "Real RCCP reverse input, braking to a stop and natural AI-driven player laps through the production GamePlayManager. No player transports, seeded velocities or scripted lap completion.", reverseSpeedKmh = reverseSpeed, reverseDistanceMetres = reverseDistance, telemetry = game == null ? null : new { elapsed = game.FormulaElapsed, laps = game.FormulaCompletedLaps, checkpoint = game.FormulaNextCheckpoint, wrongWay = game.FormulaWrongWay, signedSpeed = game.CarController == null ? 0 : game.CarController.speed, reverse = game.CarController != null && game.CarController.reversingNow, throttle = game.CarController == null ? 0 : game.CarController.Inputs.throttleInput, brake = game.CarController == null ? 0 : game.CarController.Inputs.brakeInput }, checks, laps, invalidations, errors }, Formatting.Indented));
}
