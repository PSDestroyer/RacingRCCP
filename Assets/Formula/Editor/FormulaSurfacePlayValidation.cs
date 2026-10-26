using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Presentation acceptance through the normal menu/race load path. An isolated
// save is used; no race finishes, vehicle transports or handling changes occur.
[InitializeOnLoad]
public static class FormulaSurfacePlayValidation
{
    private const string Key = "Formula.SurfacePlayValidation", Folder = "Docs/AI/FormulaSurfaces/Gameplay";
    private static readonly string[] Tracks = { "track_01", "track_05", "track_06", "rtg_01" };
    private static readonly List<object> scenes = new List<object>();
    private static readonly List<string> errors = new List<string>();
    private static int phase, track, startFrame;
    private static double began;
    private static string status;

    static FormulaSurfacePlayValidation()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Tools/Formula Career/Preview Gameplay Surface Refinement")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaSurfaces-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = track = 0; scenes.Clear(); errors.Clear(); status = "running"; began = EditorApplication.timeSinceStartup;
            Application.runInBackground = true; Time.timeScale = 1;
            Directory.CreateDirectory(Folder);
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null); SessionState.SetBool(Key, false);
            string previous = SessionState.GetString(Key + ".Previous", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - began;
            if (age > 180) throw new Exception("Surface gameplay preview timed out in phase " + phase);
            if (phase == 0)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null) return;
                Launch();
            }
            else if (phase == 1)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.GetComponent<FormulaCircuit>()?.trackId != Tracks[track] || game.CarController == null || !game.FormulaRaceStarted) return;
                phase = 2; began = EditorApplication.timeSinceStartup; startFrame = Time.frameCount;
            }
            else if (phase == 2)
            {
                if (age < 4 || Time.frameCount - startFrame < 4 || ShaderUtil.anythingCompiling) return;
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.GetComponent<FormulaCircuit>()?.trackId != Tracks[track] || !game.FormulaRaceStarted || game.FormulaElapsed < 1) return;
                var camera = Camera.allCameras.Where(c => c.enabled && c.targetTexture == null && c.cameraType == CameraType.Game).OrderByDescending(c => c.pixelWidth * c.pixelHeight).First();
                var data = camera.GetUniversalAdditionalCameraData();
                if (!data.renderPostProcessing || !camera.allowHDR || !UniversalRenderPipeline.asset.supportsHDR) throw new Exception("Gameplay camera is missing the authored HDR surface grade.");
                var surfaces = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials).Where(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith("Assets/Formula/Racing/Surfaces/Materials/", StringComparison.Ordinal)).Distinct().ToArray();
                if (surfaces.Length == 0 || surfaces.Any(m => !m.shader.isSupported)) throw new Exception("Authored gameplay materials are absent or unsupported.");
                var grade = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).First(v => v.isGlobal && AssetDatabase.GetAssetPath(v.sharedProfile).Contains("Circuit Surface Grade"));
                if (!grade.sharedProfile.TryGet<ColorAdjustments>(out var color) || color.contrast.value != 8 || !color.contrast.overrideState) throw new Exception("Gameplay loaded the old high contrast grade.");
                scenes.Add(new { track = Tracks[track], scene = game.gameObject.scene.path, framesAdvanced = Time.frameCount - startFrame, width = camera.pixelWidth, height = camera.pixelHeight, materialCount = surfaces.Length, hdr = camera.allowHDR, postProcessing = data.renderPostProcessing, contrast = color.contrast.value, liveTime = game.FormulaElapsed });
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(Folder + "/" + Tracks[track] + ".png");
                phase = 3; began = EditorApplication.timeSinceStartup; Write();
            }
            else if (phase == 3 && age > .8f)
            {
                string capture = Folder + "/" + Tracks[track] + ".png";
                if (!File.Exists(capture) || new FileInfo(capture).Length < 10000) throw new Exception("Gameplay capture was not written: " + capture);
                track++;
                if (track < Tracks.Length) Launch();
                else { status = errors.Count == 0 ? "passed" : "failed"; Write(); EditorApplication.ExitPlaymode(); }
            }
        }
        catch (Exception e) { errors.Add(e.Message); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }

    private static void Launch()
    {
        string error = FormulaSession.Launch("trial:" + Tracks[track], "time_trial");
        if (error != null) throw new Exception(error);
        phase = 1; began = EditorApplication.timeSinceStartup;
    }
    private static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception)) errors.Add(message);
    }
    private static void Write() => File.WriteAllText("Docs/AI/FormulaSurfaces/PlayValidation.json", JsonConvert.SerializeObject(new { status, unity = Application.unityVersion, isolatedSave = true, method = "Normal solo Time Attack loading and advancing gameplay frames; no scripted race completion or driving-quality claim", scenes, errors }, Formatting.Indented));
}
