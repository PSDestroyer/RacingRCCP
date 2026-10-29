using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Real four-car physics/AI on the corrected track; isolated save, no transport,
// scripted checkpoint completion, or forced velocity. Device driving is tested separately.
[InitializeOnLoad]
public static class FormulaPolishPlayValidation
{
    private const string Key = "Formula.Polish.Play";
    private static readonly List<string> errors = new List<string>();
    private static readonly List<object> telemetry = new List<object>();
    private static GamePlayManager game;
    private static double phaseAt, nextWrite;
    private static int phase;
    private static string status;
    private static float minPace, maxPace, maxSpeed;
    private static int frames;
    static FormulaPolishPlayValidation()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Polish/Play Validate Track Competition and Music")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".PreviousScene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaPolish-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            errors.Clear(); telemetry.Clear(); game = null; phase = frames = 0; minPace = maxPace = 1; maxSpeed = 0;
            status = "running"; phaseAt = nextWrite = EditorApplication.timeSinceStartup;
            Application.runInBackground = true; Time.timeScale = 1; AudioListener.pause = false;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            Write();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1; AudioListener.pause = false;
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            string save = SessionState.GetString(Key + ".Save", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(save) ? null : save);
            string previous = SessionState.GetString(Key + ".PreviousScene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Key, false);
        }
    }
    private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate(); frames++;
        try
        {
            double age = EditorApplication.timeSinceStartup - phaseAt;
            Require(errors.Count == 0, "Runtime errors during polish validation");
            Require(age < (phase == 2 ? 300 : 70), "Polish playtest timed out in phase " + phase);
            if (phase == 0)
            {
                if (Object.FindFirstObjectByType<FormulaMainMenu>() == null || age < 1) return;
                var sound = SoundManager.Instance; Require(sound != null && sound.MusicSource.isPlaying, "Menu music not playing");
                Require(sound.MusicSource.clip.length >= 119, "Menu retained the old music");
                sound.SetMusicVolume(.5f);
                string error = FormulaSession.Launch("race:track_08", "quick"); Require(error == null, error); Next(1);
            }
            else if (phase == 1)
            {
                game = Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.CarController == null || !game.FormulaRaceStarted || !game.CarController.GetComponent<FormulaVehicle>().IsReady) return;
                Require(game.enableDynamicRacePacing && game.aiRacers.Length == 3, "Formula competition pacing did not activate");
                Require(game.FormulaCompletedLaps == 0 && game.aiRacers.All(r => r.completedLaps == 0), "A grid car received an early lap");
                FormulaSession.PrepareAgent(game.CarController.gameObject);
                var driver = game.CarController.gameObject.AddComponent<RCCP_AI>();
                driver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; driver.waypointsContainer = game.raceWaypoints;
                game.GetComponent<FormulaCircuit>().ConfigureDriver(driver);
                game.totalRaceLaps = 999;
                Time.timeScale = 2; Next(2);
            }
            else if (phase == 2)
            {
                var sound = SoundManager.Instance;
                Require(sound.MusicSource.isPlaying && sound.MusicSource.volume > .015f && sound.MusicSource.volume < .15f, "Race music was muted or dominates the engine");
                maxSpeed = Mathf.Max(maxSpeed, game.CarController.absoluteSpeed);
                foreach (var rival in game.aiRacers)
                {
                    Require(rival.pacingMultiplier >= .949f && rival.pacingMultiplier <= 1.251f, "Rival pace left its limits");
                    minPace = Mathf.Min(minPace, rival.pacingMultiplier); maxPace = Mathf.Max(maxPace, rival.pacingMultiplier);
                    Require(rival.aiDriver.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, "Opponent lost its navigation binding");
                }
                if (EditorApplication.timeSinceStartup >= nextWrite)
                {
                    nextWrite = EditorApplication.timeSinceStartup + 5;
                    telemetry.Add(new { elapsed = game.FormulaElapsed, playerLaps = game.FormulaCompletedLaps, playerValidLaps = FormulaSession.ValidLaps, speed = game.CarController.absoluteSpeed,
                        opponents = game.aiRacers.Select(r => new { r.completedLaps, r.currentWaypointIndex, r.pacingMultiplier, speed = r.aiDriver.CarController.absoluteSpeed, recoveries = r.aiDriver.GetComponent<FormulaAIRecovery>().RecoveryCount }).ToArray(), musicGain = sound.MusicSource.volume });
                    Write();
                }
                if (game.FormulaCompletedLaps < 1 || game.aiRacers.Any(r => r.completedLaps < 1)) return;
                Require(FormulaSession.ValidLaps >= 1, "Player did not drive a valid full lap");
                Require(maxPace > 1.005f || minPace < .995f, "Race did not exercise dynamic pace correction");
                Time.timeScale = 0; AudioListener.pause = true; Next(3);
            }
            else if (phase == 3 && age > .7)
            {
                Require(SoundManager.Instance.MusicSource.isPlaying && SoundManager.Instance.MusicSource.ignoreListenerPause, "Pause killed music playback");
                Time.timeScale = 1; AudioListener.pause = false; FormulaSession.ReturnToMenu(); Next(4);
            }
            else if (phase == 4 && age > 1 && Object.FindFirstObjectByType<FormulaMainMenu>() != null)
            {
                Require(!FormulaSession.InRace && SoundManager.Instance.MusicSource.isPlaying, "Music did not survive returning to Menu");
                status = "passed"; Write(); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception ex) { errors.Add(ex.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    private static void Write() => File.WriteAllText(FormulaPolishTrackAudit.Folder + "/CompetitionPlay.json", JsonConvert.SerializeObject(new { status, phase, frames, minPace, maxPace, maxSpeed, telemetry, isolatedSave = true, switchHardwareMeasured = false, errors }, Formatting.Indented));
    private static void Log(string message, string stack, LogType type) { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack); }
}
