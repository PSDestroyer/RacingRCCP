using System;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// A short, isolated scene acceptance run. It never changes the user's save profile.
[InitializeOnLoad]
public static class FormulaGaragePreview
{
    private const string Active = "FormulaGarage.Preview.Active", Stage = "FormulaGarage.Preview.Stage", Previous = "FormulaGarage.Preview.Previous", ReportKey = "FormulaGarage.Preview.Report";
    [Serializable] private class Report { public string phase; public string[] passed = Array.Empty<string>(); public string[] errors = Array.Empty<string>(); public Vector3 carViewportCenter; }
    private static Report report;
    private static double started;
    private static Canvas menuCanvas;
    static FormulaGaragePreview()
    {
        EditorApplication.playModeStateChanged += OnMode;
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }
    [MenuItem("Tools/Formula Career/Preview and Validate Formula Garage")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!File.Exists(FormulaGarageBuilder.GarageScene)) throw new InvalidOperationException("Create Formula Garage first.");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaGarage-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active, true); SessionState.SetInt(Stage, 0);
        SessionState.SetString(ReportKey, JsonUtility.ToJson(new Report()));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FormulaGarageBuilder.GarageScene);
        EditorApplication.EnterPlaymode();
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, "")); started = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            var view = Type.GetType("UnityEditor.GameView,UnityEditor"); if (view != null) EditorWindow.GetWindow(view).Focus();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Active, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
            string previous = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pass(string value) { report.passed = report.passed.Concat(new[] { value }).ToArray(); }
    private static void Next(int stage, string phase)
    {
        SessionState.SetInt(Stage, stage); started = EditorApplication.timeSinceStartup; report.phase = phase;
        string json = JsonUtility.ToJson(report, true); SessionState.SetString(ReportKey, json);
        Directory.CreateDirectory("Docs/AI/FormulaGarage"); File.WriteAllText("Docs/AI/FormulaGarage/RuntimeValidation.json", json);
    }
    private static void Click(string id) => UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b => b.name == id && b.gameObject.activeInHierarchy).onClick.Invoke();
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot("Docs/AI/FormulaGarage/" + name + ".png");
    private static void Update()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - started; int stage = SessionState.GetInt(Stage, 0);
            Require(age < 60, "Garage acceptance phase timed out: " + report.phase);
            if (age < 1) return;
            var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>();
            if (stage == 0 && age > 3)
            {
                Require(menu != null && menu.CurrentPage == "Garaj", "Dedicated garage did not open the garage page.");
                Require(GameObject.Find("Sci-Fi_Neon_City_fbx") == null, "Previous city is still loaded.");
                Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e => e.isActiveAndEnabled) == 1, "Expected exactly one active EventSystem.");
                var cars = UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None);
                Require(cars.Length == 1 && cars[0].GetComponent<FormulaVehicle>() != null, "Expected exactly one Generic preview car.");
                Require(cars[0].Rigid.isKinematic && !cars[0].canControl, "Garage preview is not parked.");
                var displayWheels = cars[0].GetComponentsInChildren<RCCP_WheelCollider>();
                Require(displayWheels.Length == 4 && displayWheels.All(w => w.wheelModel != null && w.wheelModel.gameObject.activeInHierarchy && w.wheelModel.GetComponentsInChildren<Renderer>().Any(r => r.enabled)), "Parked Generic must keep all four wheel models visible.");
                report.carViewportCenter = Camera.main.WorldToViewportPoint(cars[0].transform.position + Vector3.up * .6f);
                Require(report.carViewportCenter.z > 0 && report.carViewportCenter.x > .55f && report.carViewportCenter.x < .95f, "Car framing overlaps the left menu or leaves the camera.");
                Pass("Dedicated Formula garage opens with one parked Generic, one EventSystem and no city scenery");
                Capture("GarageLiveries"); Next(1, "livery-equip");
            }
            else if (stage == 1)
            {
                Click("generic_blue"); Click("livery-apply"); Require(FormulaProgression.State.equippedLivery == "generic_blue", "Free blue livery could not be equipped.");
                Require(FormulaProgression.Data.money == 0, "Free livery changed the wallet.");
                Pass("Livery selection changes the preview and persists without charging credits");
                Capture("GarageBlue"); Next(2, "setup-equip");
            }
            else if (stage == 2)
            {
                Click("garage-1"); Click("setup-speed"); Require(FormulaProgression.State.setup == "speed", "Setup did not equip.");
                Pass("Free setup works in the new garage"); Capture("GarageSetup"); Next(3, "development");
            }
            else if (stage == 3)
            {
                Click("garage-0"); Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Count(b => b.name.StartsWith("buy-")) == 4, "Development branches missing.");
                Capture("GarageDevelopment"); Pass("All four development branches remain available");
                Next(4, "world-capture");
            }
            else if (stage == 4)
            {
                menuCanvas = menu.View.Canvas; menuCanvas.enabled = false;
                Capture("GarageWorld"); Next(5, "return-menu");
            }
            else if (stage == 5)
            {
                menuCanvas.enabled = true; FormulaSession.ReturnToMenu(); Next(6, "menu-load");
            }
            else if (stage == 6 && age > 3)
            {
                Require(menu != null && menu.CurrentPage == "Acasă", "Menu did not reload.");
                Require(FormulaProgression.State.setup == "speed" && FormulaProgression.State.equippedLivery == "generic_blue", "Garage choices did not survive scene reload.");
                Require(UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None).Length == 1, "Menu spawned a second preview car.");
                Pass("Menu reuses the Formula garage and preserves setup/livery after scene reload");
                Capture("MenuHome"); Next(7, "audio-settings");
            }
            else if (stage == 7)
            {
                menu.Show("Setări"); Click("existing-settings"); Require(menu.View.Settings.DetailsOpen, "New audio/remapping settings did not open.");
                menu.Back();
                Require(menu.CurrentPage == "Setări" && UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None).Length == 1, "Settings spawned a legacy preview or failed to return.");
                Pass("Audio/remapping opens and returns without spawning a legacy car");
                Next(8, "complete"); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error)
        {
            report.errors = report.errors.Concat(new[] { error.Message + "\n" + error.StackTrace }).ToArray();
            Next(99, "failed"); EditorApplication.ExitPlaymode();
        }
    }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (report == null || !SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors = report.errors.Concat(new[] { message }).ToArray();
    }
}
