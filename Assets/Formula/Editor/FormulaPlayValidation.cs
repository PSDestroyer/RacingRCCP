using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using HalvaStudio.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class FormulaPlayValidation
{
    private const string Active = "FormulaCareer.Validation.Active", Stage = "FormulaCareer.Validation.Stage", Previous = "FormulaCareer.Validation.PreviousStart", ReportKey = "FormulaCareer.Validation.Report", SaveKey = "FormulaCareer.Validation.Save";
    [Serializable] private class Report { public List<string> passed = new List<string>(); public List<string> errors = new List<string>(); public string phase; public float circuit1MaxSpeed, circuit2MaxSpeed; public int circuit1Laps, circuit2Laps; public float liveSpeed, liveThrottle, liveBrake; public bool reverse, navOnMesh, navHasPath; public int aiWaypoint; }
    private static Report report;
    private static double phaseStarted, lastFlush;
    private static FormulaMainMenu menu;
    private static GamePlayManager game;
    private static RCCP_Inputs inputs;
    private static bool braking, stopped, aiStarted;
    private static Vector3 drivingStart;
    private static Gamepad validationPad;
    private static GameObject selectionBeforeMove;
    static FormulaPlayValidation()
    {
        if (SessionState.GetBool(Active, false)) report = JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, ""));
        EditorApplication.playModeStateChanged += OnMode;
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }
    [MenuItem("Tools/Formula Career/Run Menu and Race Validation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validation.");
        FormulaProgressionValidation.Run(); FormulaCircuitBuilder.RegisterScenes();
        string path = Path.Combine(Path.GetTempPath(), "FormulaPlayValidation-" + Guid.NewGuid().ToString("N"), "save.json");
        // No file: exercise the real first-install path and the legacy scene template.
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        SessionState.SetString(SaveKey, path); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", path);
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active, true); SessionState.SetInt(Stage, 0);
        SessionState.SetString(ReportKey, JsonUtility.ToJson(new Report()));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, ""));
            phaseStarted = EditorApplication.timeSinceStartup; Application.runInBackground = true;
            Type view = Type.GetType("UnityEditor.GameView,UnityEditor"); if (view != null) EditorWindow.GetWindow(view).Focus();
            Time.timeScale = 1f; menu = null; game = null; braking = stopped = aiStarted = false;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetInt(Stage, 0) == 6)
            {
                SessionState.SetInt(Stage, 7); EditorApplication.delayCall += EditorApplication.EnterPlaymode;
            }
            else Cleanup();
        }
    }
    private static void Cleanup()
    {
        if (validationPad != null && validationPad.added) InputSystem.RemoveDevice(validationPad);
        validationPad = null;
        SessionState.SetBool(Active, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
        string previous = SessionState.GetString(Previous, ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        Debug.Log("FORMULA_CAREER_PLAY_VALIDATION_COMPLETE");
    }
    private static void Pass(string text) { if (!report.passed.Contains(text)) report.passed.Add(text); Save(); }
    private static void Save()
    {
        string json = JsonUtility.ToJson(report, true); SessionState.SetString(ReportKey, json);
        Directory.CreateDirectory("Docs/AI/FormulaCareer"); File.WriteAllText("Docs/AI/FormulaCareer/RuntimeValidation.json", json);
    }
    private static void Next(int stage, string phase)
    {
        SessionState.SetInt(Stage, stage); phaseStarted = EditorApplication.timeSinceStartup; report.phase = phase; Save();
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Click(string name)
    {
        var button = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b => b.name == name && b.gameObject.activeInHierarchy);
        button.onClick.Invoke();
    }
    private static void Update()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - phaseStarted > 180)
        { report.errors.Add("Phase timed out: " + report.phase); Finish(); return; }
        try
        {
            int stage = SessionState.GetInt(Stage, 0); double age = EditorApplication.timeSinceStartup - phaseStarted;
            if (game != null && game.CarController != null && EditorApplication.timeSinceStartup - lastFlush > 5)
            {
                lastFlush = EditorApplication.timeSinceStartup;
                var car = game.CarController;
                report.liveSpeed = car.absoluteSpeed; report.reverse = car.reversingNow;
                report.liveThrottle = car.Inputs.throttleInput; report.liveBrake = car.Inputs.brakeInput;
                var ai = car.GetComponent<RCCP_AI>(); report.aiWaypoint = ai != null ? ai.currentWaypointIndex : -1;
                var agent = car.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
                report.navOnMesh = agent != null && agent.isOnNavMesh; report.navHasPath = report.navOnMesh && agent.hasPath;
                Save();
            }
            if (stage == 0 && age > 3)
            {
                menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>(); Require(menu != null && menu.CurrentPage == "Acasă", "Main menu did not initialize.");
                Require(GlobalCarData._carlists.Count == 1 && GlobalCarData._carlists[0].carName == "Formula G27", "Legacy catalog still exposed.");
                Require(FormulaProgression.State.ownedLiveries.Count == 2 && FormulaProgression.Data.money == 0, "Starter profile incorrect.");
                Pass("Menu initializes with one Generic chassis, two free liveries and zero balance");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCareer/Home.png"); Next(1, "garage-layout");
            }
            else if (stage == 1 && age > 1)
            {
                menu.Show("Garaj"); Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Count(b => b.name.StartsWith("buy-")) == 4, "Four upgrade branches missing.");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCareer/Development.png"); Next(2, "livery-layout");
            }
            else if (stage == 2 && age > 1)
            {
                Click("garage-2"); Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Count(b => b.name.StartsWith("generic_")) == 8, "Livery grid incomplete.");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCareer/Liveries.png");
                Pass("Garage exposes four development branches and eight liveries"); Next(22, "controller-navigation");

            }
            else if (stage == 22 && age > 1)
            {
                menu.Show("Setări"); selectionBeforeMove = EventSystem.current.currentSelectedGameObject;
                validationPad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(validationPad, new GamepadState().WithButton(GamepadButton.DpadDown));
                Next(23, "controller-move");
            }
            else if (stage == 23 && age > .3)
            {
                Require(EventSystem.current.currentSelectedGameObject != selectionBeforeMove, "Gamepad D-pad did not move UI selection.");
                InputSystem.QueueStateEvent(validationPad, new GamepadState());
                menu.Show("Setări"); Next(24, "controller-submit");
            }
            else if (stage == 24 && age > .2)
            {
                InputSystem.QueueStateEvent(validationPad, new GamepadState().WithButton(GamepadButton.South)); Next(25, "controller-submit-result");
            }
            else if (stage == 25 && age > .3)
            {
                Require(!FormulaProgression.State.abs, "Gamepad Submit did not activate the selected assistance button.");
                InputSystem.QueueStateEvent(validationPad, new GamepadState()); Next(26, "controller-cancel");
            }
            else if (stage == 26 && age > .2)
            {
                InputSystem.QueueStateEvent(validationPad, new GamepadState().WithButton(GamepadButton.East)); Next(27, "controller-cancel-result");
            }
            else if (stage == 27 && age > .3)
            {
                Require(menu.CurrentPage == "Acasă", "Gamepad Cancel did not return to Home.");
                InputSystem.RemoveDevice(validationPad); validationPad = null;
                Pass("Virtual Input System gamepad navigates, submits and cancels the actual UI");
                menu.Show("Setări"); Click("assist-abs"); Click("existing-settings");
                Require(menu.View.Settings.DetailsOpen, "New audio/remap panel did not open.");
                menu.Back(); Require(menu.CurrentPage == "Setări", "Settings Back failed.");
                Pass("New audio/remap settings open and return to Formula settings");
                Next(21, "start-first-event");
            }
            else if (stage == 21 && age > 1)
            {
                Next(3, "circuit-1-braking"); Require(FormulaSession.Launch("g01", "career") == null, "G01 failed to launch.");
                string run = FormulaProgression.State.activeRunId;
                Require(FormulaSession.Launch("g01", "career") != null && FormulaProgression.State.activeRunId == run, "A repeated launch replaced the active run.");
                Pass("Repeated event launch cannot replace the run being loaded");
            }
            else if (stage == 3)
            {
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || !game.FormulaRaceStarted || game.CarController == null) return;
                var car = game.CarController;
                if (inputs == null) { inputs = new RCCP_Inputs(); drivingStart = car.transform.position; }
                report.circuit1MaxSpeed = Mathf.Max(report.circuit1MaxSpeed, car.absoluteSpeed);
                if (car.absoluteSpeed >= 35) braking = true;
                inputs.throttleInput = braking ? 0 : .7f; inputs.brakeInput = braking ? 1 : 0;
                car.Inputs.OverrideInputs(inputs);
                FormulaSession.ObserveBraking(car.absoluteSpeed, inputs.brakeInput);
                if (braking && car.absoluteSpeed < 3)
                {
                    Require(Vector3.Distance(drivingStart, car.transform.position) > 5 && FormulaSession.BrakeStopCompleted, "Physical braking objective failed.");
                    Pass("G01 accelerates and brakes physically on circuit 1");
                    inputs.throttleInput = inputs.brakeInput = 0;
                    car.Inputs.OverrideInputs(inputs); Next(31, "forward-gear-handover");
                }
            }
            else if (stage == 31 && age > .3)
            {
                if (game.CarController.shiftingNow) return;
                if (game.CarController.reversingNow) { game.CarController.Gearbox.ShiftToGear(0); return; }
                inputs = null; AttachAI(game.CarController); Next(4, "circuit-1-full-lap"); Time.timeScale = 5;
            }
            else if (stage == 4)
            {
                report.circuit1MaxSpeed = Mathf.Max(report.circuit1MaxSpeed, game.CarController.absoluteSpeed);
                report.circuit1Laps = game.FormulaCompletedLaps;
                if (game.FormulaCompletedLaps >= 1 && FormulaProgression.State.lastReceipt?.eventId == "g01")
                {
                    Require(FormulaProgression.State.Best("g01") >= Medal.Bronze, "Full lap failed validity.");
                    Pass("Circuit 1 completed a full physics-driven lap and committed G01 rewards");
                    Time.timeScale = 1; Next(45, "results-screen");
                }
            }
            else if (stage == 45 && age > 1)
            {
                Require(GameObject.Find("Formula Results") != null, "Formula results panel did not render.");
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCareer/Results.png"); Next(46, "capture-results");
            }
            else if (stage == 46 && age > .5)
            {
                Next(5, "buy-and-equip"); FormulaSession.ReturnToMenu();
            }
            else if (stage == 5 && age > 3)
            {
                menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>(); if (menu == null) return;
                menu.Show("Garaj"); Click("buy-engine"); Click("confirm");
                Require(FormulaProgression.State.Owned("engine") == 1, "Upgrade purchase failed through UI.");
                Click("garage-2"); Click("generic_blue"); Click("livery-apply");
                Require(FormulaProgression.State.equippedLivery == "generic_blue" && FormulaProgression.State.Owned("engine") == 1, "Livery reset development.");
                Pass("UI purchase and livery equip preserve development and save successfully");
                Next(6, "reload-profile"); EditorApplication.ExitPlaymode();
            }
            else if (stage == 7 && age > 3)
            {
                Require(FormulaProgression.State.Owned("engine") == 1 && FormulaProgression.State.equippedLivery == "generic_blue", "Reload lost progression.");
                Pass("Progression survives a complete Play Mode exit and new load");
                inputs = null; game = null; Next(8, "circuit-2-race"); Require(FormulaSession.Launch("g04", "quick") == null, "Circuit 2 launch failed.");
            }
            else if (stage == 8)
            {
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || !game.FormulaRaceStarted || game.CarController == null) return;
                if (!aiStarted)
                {
                    Require(game.aiRacers.Length == 3 && game.raceWaypoints.waypoints.Count > 100, "Circuit 2 opponents or route missing.");
                    Pass("Circuit 2 starts with three Generic opponents and road-derived checkpoints");
                    AttachAI(game.CarController); aiStarted = true; Time.timeScale = 5;
                }
                report.circuit2MaxSpeed = Mathf.Max(report.circuit2MaxSpeed, game.CarController.absoluteSpeed);
                report.circuit2Laps = game.FormulaCompletedLaps;
                if (game.FormulaCompletedLaps >= 1)
                {
                    Pass("Circuit 2 completed a full physics-driven lap with opponents");
                    ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCareer/Circuit2.png");
                    Finish();
                }
            }
        }
        catch (Exception e) { report.errors.Add(e.Message + "\n" + e.StackTrace); Finish(); }
    }
    private static void AttachAI(RCCP_CarController car)
    {
        car.Inputs.DisableOverrideInputs();
        FormulaSession.PrepareAgent(car.gameObject);
        var ai = car.gameObject.AddComponent<RCCP_AI>();
        ai.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; ai.waypointsContainer = game.raceWaypoints;
        ai.waypointReachThreshold = 12; ai.raceLookAhead = 20; ai.agressiveness = 1; ai.maxThrottle = .7f;
    }
    private static void Finish() { Next(99, "complete"); Time.timeScale = 1; EditorApplication.ExitPlaymode(); }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || report == null) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        { if (!report.errors.Contains(message)) { report.errors.Add(message); Save(); } }
    }
}
