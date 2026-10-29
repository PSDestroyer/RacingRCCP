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

// Short real-physics loading/rendering check in sun and rain, with an isolated save.
[InitializeOnLoad]
public static class FormulaAsphaltPlayValidation
{
    private const string Key = "Formula.Asphalt.Play";
    private static readonly string[] Tracks = { "track_01", "track_06" };
    private static readonly List<string> errors = new List<string>();
    private static readonly List<object> results = new List<object>();
    private static int phase, track;
    private static double phaseAt;
    private static float simulationAt, distance;
    private static Vector3 previous;
    private static string status;
    private static GamePlayManager game;

    static FormulaAsphaltPlayValidation()
    {
        EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack);
        };
    }

    [MenuItem("Tools/Formula Career/Surfaces/Play Validate Damp Asphalt")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaAsphalt-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            errors.Clear(); results.Clear(); phase = track = 0; status = "running"; phaseAt = EditorApplication.timeSinceStartup;
            Application.runInBackground = true; Time.timeScale = 1; game = null;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus(); Write();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            string save = SessionState.GetString(Key + ".Save", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(save) ? null : save);
            string scene = SessionState.GetString(Key + ".Scene", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            SessionState.SetBool(Key, false);
        }
    }

    private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; Write(); }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            Require(errors.Count == 0, "Runtime error during asphalt validation");
            double age = EditorApplication.timeSinceStartup - phaseAt; Require(age < 90, "Asphalt playtest timed out in phase " + phase);
            if (phase == 0)
            {
                if (Object.FindFirstObjectByType<FormulaMainMenu>() == null || age < 1) return;
                string error = FormulaSession.Launch("trial:" + Tracks[track], "time_trial"); Require(error == null, error); Next(1);
            }
            else if (phase == 1)
            {
                game = Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.CarController == null || !game.FormulaRaceStarted || !game.CarController.GetComponent<FormulaVehicle>().IsReady) return;
                var root = GameObject.Find("Formula Authored Tyre Wear"); Require(root != null, "Authored wear missing in race");
                var renderer = root.GetComponent<MeshRenderer>(); var mesh = root.GetComponent<MeshFilter>().sharedMesh;
                Require(renderer.sharedMaterial.shader.isSupported && renderer.sharedMaterial.passCount == 1 && !mesh.isReadable, "Invalid runtime wear material/mesh");
                FormulaSession.PrepareAgent(game.CarController.gameObject);
                var driver = game.CarController.gameObject.AddComponent<RCCP_AI>();
                driver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; driver.waypointsContainer = game.raceWaypoints;
                game.GetComponent<FormulaCircuit>().ConfigureDriver(driver);
                previous = game.CarController.transform.position; simulationAt = Time.time; distance = 0; Time.timeScale = 2; Next(2);
            }
            else if (phase == 2)
            {
                distance += Vector3.Distance(previous, game.CarController.transform.position); previous = game.CarController.transform.position;
                if (Time.time - simulationAt < 12) return;
                Require(distance > 20, "Race physics did not advance");
                Require(Vector3.Dot(game.CarController.transform.up, Vector3.up) > .7f, "Smoke driver left its upright driving state");
                var renderer = GameObject.Find("Formula Authored Tyre Wear").GetComponent<MeshRenderer>(); Require(renderer.isVisible, "Wear renderer was never in view");
                var roads = game.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>())
                    .SelectMany(r => r.sharedMaterials).Where(m => m != null && m.HasProperty("_Smoothness") && m.name.EndsWith("Circuit", StringComparison.Ordinal)).Distinct().ToArray();
                Require(roads.Any(m => m.IsKeywordEnabled("_METALLICSPECGLOSSMAP") && m.GetFloat("_Smoothness") >= .75f), "Damp road material did not load");
                results.Add(new { track = Tracks[track], distanceDriven = distance, simulatedSeconds = Time.time - simulationAt, wearVisible = renderer.isVisible,
                    shaderSupported = renderer.sharedMaterial.shader.isSupported, wearPasses = renderer.sharedMaterial.passCount, vertices = renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount,
                    isolatedTimeTrial = true, upright = true });
                ScreenCapture.CaptureScreenshot(FormulaAsphaltFinishAuthoring.Report + "/Play-" + Tracks[track] + ".png"); Next(3);
            }
            else if (phase == 3 && age > 1) { Time.timeScale = 1; FormulaSession.ReturnToMenu(); Next(4); }
            else if (phase == 4 && age > 1 && Object.FindFirstObjectByType<FormulaMainMenu>() != null)
            {
                track++;
                if (track < Tracks.Length) Next(0);
                else { status = "passed"; Write(); EditorApplication.ExitPlaymode(); }
            }
        }
        catch (Exception ex) { errors.Add(ex.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }

    private static void Write()
    {
        Directory.CreateDirectory(FormulaAsphaltFinishAuthoring.Report);
        File.WriteAllText(FormulaAsphaltFinishAuthoring.Report + "/PlayValidation.json", JsonConvert.SerializeObject(new { status, phase, results,
            isolatedSave = true, switchHardwareTested = false, errors }, Formatting.Indented));
    }
}
