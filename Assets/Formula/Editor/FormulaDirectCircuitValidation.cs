using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Real three-lap race from a directly opened scene, with no scripted finish.
[InitializeOnLoad]
public static class FormulaDirectCircuitValidation
{
    private const string Key = "Formula.DirectCircuitValidation";
    private static double started, nextReport;
    private static int phase, initialMoney, initialXp, completedLaps, validLaps;
    private static float elapsed;
    private static FormulaReceipt receipt;
    private static readonly List<string> errors = new List<string>();
    static FormulaDirectCircuitValidation() { EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    [MenuItem("Tools/Formula Career/Validate Direct Circuit Play")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaDirect-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/Scenes/Formula_RTG_10.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            errors.Clear(); phase = completedLaps = validLaps = 0; elapsed = 0; receipt = null;
            started = EditorApplication.timeSinceStartup; nextReport = started; Application.runInBackground = true;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null); Time.timeScale = 1;
            string previous = SessionState.GetString(Key + ".Previous", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        }
    }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            Require(EditorApplication.timeSinceStartup - started < (phase == 1 ? 1200 : 120), "Direct scene validation timed out in phase " + phase);
            var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
            if (phase == 0)
            {
                if (game == null || game.CarController == null || !game.FormulaRaceStarted || !game.CarController.GetComponent<FormulaVehicle>().IsReady) return;
                Require(FormulaSession.CurrentEvent?.id == "race:rtg_10" && FormulaProgression.State.activeMode == "quick" && game.aiRacers.Length == 3 && game.totalRaceLaps == 3, "Direct circuit did not start the authored free race.");
                Require(game.FormulaCompletedLaps == 0 && game.aiRacers.All(r => r.aiDriver.GetComponentInChildren<NavMeshAgent>().isOnNavMesh), "Direct scene has an unsafe start.");
                initialMoney = FormulaProgression.Data.money; initialXp = FormulaProgression.Data.exp;
                FormulaSession.PrepareAgent(game.CarController.gameObject);
                var driver = game.CarController.gameObject.AddComponent<RCCP_AI>();
                driver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; driver.waypointsContainer = game.raceWaypoints;
                game.GetComponent<FormulaCircuit>().ConfigureDriver(driver);
                Time.timeScale = 3; phase = 1; started = EditorApplication.timeSinceStartup; Write("driving", null);
            }
            else if (phase == 1)
            {
                completedLaps = game.FormulaCompletedLaps; validLaps = FormulaSession.ValidLaps; elapsed = game.FormulaElapsed;
                if (EditorApplication.timeSinceStartup >= nextReport) { nextReport = EditorApplication.timeSinceStartup + 5; Write("driving", null); }
                var result = UnityEngine.Object.FindFirstObjectByType<FormulaResultsView>();
                if (result == null) return;
                receipt = FormulaProgression.State.lastReceipt;
                Require(completedLaps == 3 && validLaps > 0 && receipt != null && receipt.eventId == "race:rtg_10", "Three-lap race did not produce a valid natural result.");
                Require(receipt.credits == 0 && receipt.xp == 0 && FormulaProgression.Data.money == initialMoney && FormulaProgression.Data.exp == initialXp, "Direct free race changed career economy.");
                Require(result.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "P" + receipt.finishPosition), "Quick race result did not show the finishing position.");
                Time.timeScale = 1; phase = 2; started = EditorApplication.timeSinceStartup; Write("showing_results", null);
            }
            else if (phase == 2)
            {
                if (EditorApplication.timeSinceStartup - started < 1 || ShaderUtil.anythingCompiling) return;
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCircuits/NaturalRaceResults.png");
                phase = 3; started = EditorApplication.timeSinceStartup;
            }
            else if (phase == 3)
            {
                if (EditorApplication.timeSinceStartup - started < 1) return;
                var result = UnityEngine.Object.FindFirstObjectByType<FormulaResultsView>();
                result.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "menu").onClick.Invoke();
                phase = 4; started = EditorApplication.timeSinceStartup; Write("returning_to_menu", null);
            }
            else if (phase == 4)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>(); if (menu == null) return;
                Require(menu.CurrentPage == "Acasă" && errors.Count == 0, "Result did not return to Home cleanly.");
                Write("passed", null); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error) { Time.timeScale = 1; Write("failed", error.ToString()); EditorApplication.ExitPlaymode(); }
    }
    private static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(message + "\n" + trace);
    }
    private static void Write(string status, string error) => File.WriteAllText("Docs/AI/FormulaCircuits/DirectPlayValidation.json", JsonConvert.SerializeObject(new { status, error, errors, phase, scene = "Formula_RTG_10", mode = "quick", laps = completedLaps, validLaps, elapsed, opponents = 3, receipt, isolatedSave = true, scriptedFinish = false, playerTransport = false }, Formatting.Indented));
}
