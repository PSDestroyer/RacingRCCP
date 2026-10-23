using System;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Focused English HUD/result acceptance using a scripted finish and an isolated save.
[InitializeOnLoad]
public static class FormulaEnglishRacePreview
{
    private const string Active = "Formula.EnglishRace.Active", Stage = "Formula.EnglishRace.Stage", Previous = "Formula.EnglishRace.Previous";
    [Serializable] private sealed class Report { public string phase; public string[] passed = Array.Empty<string>(), errors = Array.Empty<string>(); public string method = "Temporary save, real circuit scene, scripted finish; no driving validation"; }
    private static Report report;
    private static double started;
    static FormulaEnglishRacePreview() { EditorApplication.playModeStateChanged += OnMode; EditorApplication.update += Update; Application.logMessageReceived += OnLog; }
    [MenuItem("Tools/Formula Career/Preview English Race Text")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaEnglish-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active, true); SessionState.SetInt(Stage, 0);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report(); started = EditorApplication.timeSinceStartup; Application.runInBackground = true;
            var view = Type.GetType("UnityEditor.GameView,UnityEditor"); if (view != null) EditorWindow.GetWindow(view).Focus();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Active, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
            string old = SessionState.GetString(Previous, ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(old) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(old);
        }
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Next(int stage, string phase)
    {
        SessionState.SetInt(Stage, stage); started = EditorApplication.timeSinceStartup; report.phase = phase;
        Directory.CreateDirectory("Docs/AI/FormulaUI/English"); File.WriteAllText("Docs/AI/FormulaUI/English/RaceValidation.json", JsonUtility.ToJson(report, true));
    }
    private static void Pass(string text) => report.passed = report.passed.Concat(new[] { text }).ToArray();
    private static TMP_Text[] Texts() => GameObject.Find("Formula Race").GetComponentsInChildren<TMP_Text>();
    private static void Update()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - started; Require(age < 90, "English race phase timed out: " + report.phase); if (age < 1) return;
            int stage = SessionState.GetInt(Stage, 0);
            if (stage == 0 && age > 3)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null) return;
                Require(FormulaSession.Launch("g01", "career") == null, "English event failed to launch."); Next(1, "race-load");
            }
            else if (stage == 1 && FormulaSession.InRace && age > 4)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>(); if (game == null || game.CarController == null || GameObject.Find("Formula Race") == null) return;
                var texts = Texts(); Require(texts.Any(t => t.text.Contains("Best lap:")) && texts.Any(t => t.text == "Menu"), "Race HUD is not English.");
                Require(FormulaAssets.Current.FindTrack("track_01").displayName == "Grand Prix Circuit", "Track name is not English.");
                Pass("Shakedown HUD shows the valid-lap objective in English"); ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/English/HUD.png"); Next(2, "scripted-finish");
            }
            else if (stage == 2)
            {
                FormulaSession.InvalidateLap(); FormulaSession.Lap(1);
                Require(FormulaSession.ValidLaps == 0, "Invalid lap counted toward Shakedown.");
                FormulaSession.Lap(250);
                Require(!FormulaSession.BrakeStopCompleted, "Test must complete Shakedown without a braking stop.");
                Require(FormulaSession.Complete(true, 1, 250) != null, "English result receipt failed to save."); Next(3, "result-render");
            }
            else if (stage == 3)
            {
                var texts = Texts(); Require(texts.Any(t => t.text == "BRONZE") && texts.Any(t => t.text == "Retry event") && UnityEngine.Object.FindFirstObjectByType<FormulaResultsView>() != null, "Result panel is not English.");
                Require(texts.All(t => !System.Text.RegularExpressions.Regex.IsMatch(t.text, "[ăâîșțĂÂÎȘȚşţŞŢ]")), "Romanian text remains in the HUD or results.");
                var receipt = FormulaProgression.State.lastReceipt;
                Require(texts.Any(t => t.text == "+" + receipt.credits.ToString("N0") + " CR") && texts.Any(t => t.text == "+" + receipt.xp.ToString("N0") + " XP"), "Result reward labels differ from the saved receipt.");
                Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "next", "The next career event must be the primary result action.");
                Pass("Shakedown awards Bronze for a valid lap without a braking stop; authored result values match the saved receipt and focus Continue career"); ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/English/Results.png"); Next(4, "result-capture");
            }
            else if (stage == 4)
            {
                var events = UnityEngine.EventSystems.EventSystem.current;
                Require(UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy<UnityEngine.EventSystems.ICancelHandler>(events.currentSelectedGameObject, new UnityEngine.EventSystems.BaseEventData(events), (handler, data) => handler.OnCancel(data)) != null, "Results do not handle the UI Back action.");
                Next(5, "menu-return");
            }
            else if (stage == 5 && age > 3)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>(); if (menu == null) return;
                Require(menu.View.Canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "MAIN MENU"), "English menu title did not survive returning from a race.");
                Pass("English menu and progression survive returning from the circuit"); Next(6, "complete"); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error) { report.errors = report.errors.Concat(new[] { error.Message + "\n" + error.StackTrace }).ToArray(); Next(99, "failed"); EditorApplication.ExitPlaymode(); }
    }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (report != null && SessionState.GetBool(Active, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors = report.errors.Concat(new[] { message }).ToArray();
    }
}
