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
    private static UnityEngine.InputSystem.Keyboard keyboard;
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
            if (keyboard != null) { UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); keyboard = null; }
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
                Require(FormulaSession.Launch("g01", "career") == null, "English event failed to launch.");
                Require(UnityEngine.Object.FindFirstObjectByType<FormulaLoadingView>() != null, "Themed asynchronous loading view did not appear.");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/English/Loading.png");
                Pass("Themed loading is connected to the existing asynchronous scene loader"); Next(1, "race-load");
            }
            else if (stage == 1 && FormulaSession.InRace && age > 4)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>(); if (game == null || game.CarController == null || !game.FormulaRaceStarted || GameObject.Find("Formula Race") == null) return;
                var texts = Texts(); Require(texts.Any(t => t.text.StartsWith("BEST  ")) && texts.Any(t => t.text == "PAUSE") && texts.Any(t => t.text == "KM/H"), "Authored race HUD is missing timing, pause or vehicle data.");
                Require(FormulaAssets.Current.FindTrack("track_01").displayName == "Grand Prix Circuit", "Track name is not English.");
                Require(game.timerUIRoot == null || !game.timerUIRoot.GetComponentInParent<Canvas>().enabled, "Legacy race Canvas remains visible behind the authored HUD.");
                var surface = game.CarController.GetComponent<FormulaVehicleSurface>();
                var body = game.CarController.GetComponent<FormulaVehicle>().body;
                Require(surface != null && surface.SurfaceCount == 11 && body.sharedMaterial.shader.name == "Universal Render Pipeline/Complex Lit", "Race Generic did not inherit its PBR materials and wetness bindings.");
                Pass("Shakedown HUD shows the valid-lap objective in English"); ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/English/HUD.png");
                Directory.CreateDirectory("Docs/AI/FormulaMaterials"); ScreenCapture.CaptureScreenshot("Docs/AI/FormulaMaterials/RaceDry.png"); Next(5000, "race-surface-wetness");
            }
            else if (stage == 5000)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                game.CarController.GetComponent<FormulaVehicleSurface>().SetWetness(1); Next(5001, "race-wet-capture");
            }
            else if (stage == 5001)
            {
                // Switching from SRP-batched dry rendering to wet property blocks
                // may compile an Editor-only variant. Never capture its cyan placeholder.
                if (ShaderUtil.anythingCompiling || age < 2) return;
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                var body = game.CarController.GetComponent<FormulaVehicle>().body;
                var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block, 0);
                Require(block.GetFloat("_Smoothness") >= .89f && block.GetFloat("_ClearCoatMask") > .99f, "Wet surface response did not reach the real race car.");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaMaterials/RaceWet.png"); Next(5002, "race-surface-dry-restore");
            }
            else if (stage == 5002)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                var body = game.CarController.GetComponent<FormulaVehicle>().body;
                game.CarController.GetComponent<FormulaVehicleSurface>().SetWetness(0);
                var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block, 0);
                Require(block.isEmpty, "Dry race car retained wetness property blocks.");
                Pass("Actual circuit car renders native PBR paint and restores dry surfaces after a wet capture without residual overrides"); Next(2, "scripted-finish");
            }
            else if (stage == 2)
            {
                FormulaSession.InvalidateLap(); Require(FormulaSession.LapInvalid, "Track limits did not invalidate the lap."); Next(20, "invalid-lap-feedback");
            }
            else if (stage == 20)
            {
                Require(Texts().Any(t => t.text.Contains("LAP INVALID · TRACK LIMITS")), "The HUD does not explain an invalid lap.");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/English/InvalidLap.png"); Next(21, "pause-session");
            }
            else if (stage == 21)
            {
                GameObject.Find("race-pause").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(Time.timeScale == 0 && AudioListener.pause, "Pause did not freeze simulation and audio."); Next(22, "pause-confirmation");
            }
            else if (stage == 22)
            {
                GameObject.Find("leave").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(Texts().Any(t => t.text == "LEAVE SESSION?") && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "cancel", "Leave confirmation is missing or selects the destructive action by default.");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/English/PauseConfirmation.png"); Next(23, "pause-resume");
            }
            else if (stage == 23)
            {
                var system = UnityEngine.EventSystems.EventSystem.current;
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy<UnityEngine.EventSystems.ICancelHandler>(system.currentSelectedGameObject, new UnityEngine.EventSystems.BaseEventData(system), (handler, data) => handler.OnCancel(data));
                GameObject.Find("resume").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(Time.timeScale == 1 && !AudioListener.pause, "Resume did not restore simulation and audio.");
                keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Formula race keyboard validation");
                Next(24, "keyboard-pause");
            }
            else if (stage == 24)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape)); Next(25, "keyboard-pause-state");
            }
            else if (stage == 25)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                Require(Time.timeScale == 0, "Escape toggled Pause twice through RCCP and the UI input module."); Next(26, "keyboard-resume");
            }
            else if (stage == 26)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape)); Next(27, "keyboard-resume-state");
            }
            else if (stage == 27)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                Require(Time.timeScale == 1 && !AudioListener.pause, "Escape did not resume the paused race.");
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); keyboard = null;
                Pass("Invalid laps explain track limits; Pause and leave confirmation work; Escape pauses/resumes exactly once through RCCP and UI input");
                FormulaSession.Lap(1); Require(FormulaSession.ValidLaps == 0, "Invalid lap counted toward Shakedown.");
                FormulaSession.Lap(250);
                Require(!FormulaSession.BrakeStopCompleted, "Test must complete Shakedown without a braking stop.");
                Require(FormulaSession.Complete(true, 1, 250) != null, "English result receipt failed to save."); Next(3, "result-render");
            }
            else if (stage == 3)
            {
                var texts = Texts(); Require(texts.Any(t => t.text == "BRONZE") && texts.Any(t => t.text == "RETRY") && UnityEngine.Object.FindFirstObjectByType<FormulaResultsView>() != null, "Result panel is not English.");
                Require(texts.Any(t => t.text.StartsWith("LEVEL 1  /")) && texts.Any(t => t.text.StartsWith("NEXT MEDAL  /")) && texts.Any(t => t.text.StartsWith("UP NEXT  /")), "Results lack XP, next medal or next event context.");
                Require(!texts.Any(t => t.text == "LEVEL UP BONUS  +0 CR"), "Zero level bonus is displayed.");
                Require(texts.All(t => !System.Text.RegularExpressions.Regex.IsMatch(t.text, "[ăâîșțĂÂÎȘȚşţŞŢ]")), "Romanian text remains in the HUD or results.");
                var receipt = FormulaProgression.State.lastReceipt;
                Require(texts.Any(t => t.text == "+" + receipt.credits.ToString("N0") + " CR") && texts.Any(t => t.text == "+" + receipt.xp.ToString("N0") + " XP"), "Result reward labels differ from the saved receipt.");
                Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "next", "The next career event must be the primary result action.");
                Pass("Shakedown awards Bronze for a valid lap without a braking stop; authored result values match the saved receipt and focus Continue career"); ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/English/Results.png"); Next(4, "result-capture");
            }
            else if (stage == 4)
            {
                GameObject.Find("next").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Next(5, "next-event-review");
            }
            else if (stage == 5 && age > 3)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>(); if (menu == null) return;
                Require(menu.CurrentPage == "Carieră" && menu.View.Dialogs.IsOpen && menu.View.Canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "FIRST FLYING LAP"), "Next event launched a race or lost its briefing.");
                Require(!FormulaSession.InRace && FormulaProgression.State.Best("g01") == Medal.Bronze, "Review route lost career progress.");
                Pass("Results show XP, next medal and next event; Next event returns to its Career briefing without launching or losing progress");
                menu.Back(); Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "g02", "Next event briefing did not restore its Career card.");
                menu.Back(); Next(6, "menu-return");
            }
            else if (stage == 6)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>();
                Require(menu.View.Canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "MAIN MENU"), "English menu title did not survive returning from a race.");
                Pass("English menu and progression survive returning from the circuit"); Next(7, "complete"); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error) { report.errors = report.errors.Concat(new[] { error.Message + "\n" + error.StackTrace }).ToArray(); Next(99, "failed"); EditorApplication.ExitPlaymode(); }
    }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (report != null && SessionState.GetBool(Active, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors = report.errors.Concat(new[] { message }).ToArray();
    }
}
