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

// Explicit, isolated photo session: actual race cameras and live HUD only.
// Never changes scene assets, materials, vehicle poses, velocities or HUD text.
[InitializeOnLoad]
public static class FormulaMarketingCapture
{
    const string Key = "Formula.MarketingCapture";
    const string OriginalProfile = "Assets/Formula/Racing/Surfaces/Circuit Surface Grade.asset";
    static bool PreviewOnly => SessionState.GetBool(Key + ".Preview", false);
    static bool Lookback => SessionState.GetBool(Key + ".Lookback", false);
    static string Folder => "Marketing/Screenshots/2026-10-30/Original-Volume" + (PreviewOnly ? "/Preview" : Lookback ? "/Lookback" : "");
    static readonly string[] LookbackTracks = { "track_01", "rtg_01", "track_05", "track_08" };
    static readonly string[] LookbackLiveries = { "generic_orange", "generic_blue", "generic_blue", "generic_blue" };
    static readonly float[] LookbackMoments = { 2.8f, 4.3f, 5.8f, 7.3f, 8.8f, 10.3f, 12.3f, 14.3f, 16.3f, 18.3f };
    static readonly string[] PreviewTracks = { "track_01", "track_08" };
    static readonly string[] PreviewLiveries = { "generic_orange", "generic_blue" };
    static readonly float[] PreviewMoments = { 4.3f, 8.8f, 14.3f };
    static string[] ActiveTracks => PreviewOnly ? PreviewTracks : Lookback ? LookbackTracks : Tracks;
    static string[] ActiveLiveries => PreviewOnly ? PreviewLiveries : Lookback ? LookbackLiveries : Liveries;
    static float[] ActiveMoments => PreviewOnly ? PreviewMoments : Lookback ? LookbackMoments : Moments;
    static readonly string[] Tracks = { "track_01", "rtg_01", "track_06", "track_05", "track_08", "track_07" };
    static readonly string[] Liveries = { "generic_orange", "generic_blue", "generic_orange", "generic_red", "generic_blue", "generic_yellow" };
    static readonly float[] Moments = { 2.8f, 4.3f, 5.8f, 7.3f, 8.8f, 10.3f, 12.3f, 14.3f, 16.3f, 18.3f };
    static readonly List<object> captures = new List<object>();
    static readonly List<object> rejected = new List<object>();
    static readonly List<string> errors = new List<string>();
    static int phase, shot, sample, frame, attempt, trackCaptures;
    static double phaseAt, lastReport;
    static float driveAt;
    static string status, pending;
    static GamePlayManager game;
    static RCCP_Camera rig;
    static RCCP_AI driver;
    static bool released;

    static FormulaMarketingCapture()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception))
                errors.Add(message + "\n" + stack);
        };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Preview() { if (SessionState.GetBool(Key, false)) FormulaPlatform.EditorSwitchPreview = true; }

    [MenuItem("Tools/Formula Career/Marketing/Capture Dynamic HUD Screenshots")]
    public static void Run() => Start(false);
    [MenuItem("Tools/Refine Marketing HUD Action")]
    static void RunLookback() => Start(true);
    [MenuItem("Tools/Preview Formula Visual Upgrade")]
    static void RunPreview() => Start(false,true);
    static void Start(bool lookback, bool previewOnly = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before starting the photo session.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before capture.");
        SessionState.SetBool(Key + ".Lookback", lookback); SessionState.SetBool(Key + ".Preview", previewOnly);
        Directory.CreateDirectory(Folder + "/Candidates");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetInt(Key + ".Fps", Application.targetFrameRate);
        SessionState.SetBool(Key + ".SwitchPreview", FormulaPlatform.EditorSwitchPreview);
        FormulaGameViewValidation.Select1080();
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaMarketingHUD-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    [MenuItem("Tools/Capture Marketing Screenshots")]
    static void RunFromTools() => Run();
    [MenuItem("Tools/Formula Career/Marketing/Stop Screenshot Capture")]
    public static void Stop() { if (SessionState.GetBool(Key, false)) { status = "stopped"; Write(); EditorApplication.ExitPlaymode(); } }

    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            FormulaPlatform.EditorSwitchPreview = true;
            captures.Clear(); rejected.Clear(); errors.Clear(); phase = shot = sample = attempt = trackCaptures = 0;
            status = "running"; phaseAt = EditorApplication.timeSinceStartup;
            Application.runInBackground = true; Time.timeScale = 1;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus(); Write();
        }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1;
            FormulaPlatform.EditorSwitchPreview = SessionState.GetBool(Key + ".SwitchPreview", false);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            Application.targetFrameRate = SessionState.GetInt(Key + ".Fps", -1);
            string previous = SessionState.GetString(Key + ".Save", "");
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(previous) ? null : previous);
            previous = SessionState.GetString(Key + ".Scene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            FormulaGameViewValidation.Restore(); SessionState.SetBool(Key, false);
            Debug.Log("FORMULA_MARKETING_HUD_" + status?.ToUpperInvariant());
        }
    }
    static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; frame = Time.frameCount; Write(); }
    static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - phaseAt;
            Require(age < 180, "HUD capture timed out in phase " + phase);
            if (EditorApplication.timeSinceStartup - lastReport > 3) { Write(); lastReport = EditorApplication.timeSinceStartup; }
            var menu = Object.FindFirstObjectByType<FormulaMainMenu>();
            if (phase == 0)
            {
                if (menu == null || menu.CurrentPage != "Acasă" || !FormulaLocalization.Ready || age < 2) return;
                var d = FormulaProgression.Data; d.PlayerName = "Racer"; d.language = "en";
                var p = FormulaProgression.State;
                p.ownedLiveries = FormulaCatalog.Current.liveries.Select(l => l.id).ToList();
                Require(FormulaLocalization.ChangeLanguage("en", out string error), error);
                Launch();
            }
            else if (phase == 10)
            {
                game = Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.GetComponent<FormulaCircuit>()?.trackId != ActiveTracks[shot] || game.CarController == null || !game.FormulaRaceStarted || !game.CarController.GetComponent<FormulaVehicle>().IsReady) return;
                var volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Where(v => v.isGlobal).ToArray();
                Require(volumes.Length > 0 && volumes.All(v => AssetDatabase.GetAssetPath(v.sharedProfile) == OriginalProfile), "The restored original Global Volume must be used for these captures.");
                FormulaSession.PrepareAgent(game.CarController.gameObject);
                driver = game.CarController.GetComponent<RCCP_AI>() ?? game.CarController.gameObject.AddComponent<RCCP_AI>();
                driver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; driver.waypointsContainer = game.raceWaypoints;
                game.GetComponent<FormulaCircuit>().ConfigureDriver(driver);
                driver.stopNow = attempt > 0; released = attempt == 0;
                rig = RCCP_SceneManager.Instance.activePlayerCamera;
                Require(rig != null && rig.actualCamera != null, "Active gameplay camera missing.");
                // Use the persistent camera settings authored in the circuit scene.
                rig.lookBackNow = Lookback;
                rig.ChangeCamera(RCCP_Camera.CameraMode.TPS); rig.ResetCamera();
                driveAt = Time.time; sample = 0; Next(11);
            }
            else if (phase == 11)
            {
                rig.lookBackNow = Lookback;
                float elapsed = Time.time - driveAt;
                if (!released && elapsed > .65f) { driver.stopNow = false; released = true; }
                if (sample >= ActiveMoments.Length) { FormulaSession.ReturnToMenu(); Next(14); return; }
                if (elapsed < ActiveMoments[sample] || ShaderUtil.anythingCompiling) return;
                var view = Object.FindFirstObjectByType<FormulaRaceView>();
                var car = game.CarController;
                int groundedWheels = car.GetComponentsInChildren<WheelCollider>().Count(w => w.enabled && w.isGrounded);
                var reasons = new List<string>();
                if (FormulaSession.LapInvalid || game.FormulaWrongWay) reasons.Add("invalid lap or wrong way");
                if (Vector3.Dot(car.transform.up, Vector3.up) < .9f || groundedWheels < 2) reasons.Add("not upright and grounded");
                if (car.absoluteSpeed < 42) reasons.Add("speed below 42 km/h");
                if (view == null || !view.Hud.activeInHierarchy || !view.GetComponentInParent<Canvas>().enabled) reasons.Add("HUD not visible");
                if (reasons.Count > 0)
                {
                    rejected.Add(new { track = ActiveTracks[shot], attempt, sample, speedKph = car.absoluteSpeed, reasons }); sample++; Write(); return;
                }
                Capture(); Next(12);
            }
            else if (phase == 12 && age > .18 && Time.frameCount - frame > 2)
            {
                VerifyFile(); sample++;
                // Include a few real onboard views where that camera is authored.
                if (!Lookback && sample == 6 && (shot == 2 || shot == 5 || shot == 7) && rig.cameraTarget.HoodCamera != null)
                    rig.ChangeCamera(RCCP_Camera.CameraMode.FPS);
                if (sample == 9) rig.ChangeCamera(RCCP_Camera.CameraMode.TPS);
                Next(11);
            }
            else if (phase == 14 && menu != null && age > 1)
            {
                if (!PreviewOnly && trackCaptures < 4 && attempt == 0) { attempt++; Launch(); return; }
                shot++; attempt = trackCaptures = 0;
                if (shot < ActiveTracks.Length) Launch();
                else { status = errors.Count == 0 ? "captured" : "captured_with_errors"; Write(); EditorApplication.ExitPlaymode(); }
            }
        }
        catch (Exception failure) { errors.Add(failure.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    static void Launch()
    {
        FormulaProgression.State.equippedLivery = ActiveLiveries[shot];
        Require(FormulaSession.Launch("race:" + ActiveTracks[shot], "quick") == null, "Could not launch race."); Next(10);
    }
    static void Capture()
    {
        var car = game.CarController; var camera = rig.actualCamera;
        float nearest = 10000; int visibleOpponents = 0;
        foreach (var rival in game.aiRacers)
        {
            nearest = Mathf.Min(nearest, Vector3.Distance(car.transform.position, rival.racerTransform.position));
            Vector3 p = camera.WorldToViewportPoint(rival.racerTransform.position + Vector3.up * .5f);
            if (p.z > 0 && p.x > .08f && p.x < .92f && p.y > .12f && p.y < .85f) visibleOpponents++;
        }
        pending = Folder + "/Candidates/" + (shot + 1).ToString("00") + "-" + ActiveTracks[shot] + "-a" + attempt + "-s" + sample.ToString("00") + "-" + (Lookback ? "LookBack" : rig.cameraMode.ToString()) + ".png";
        Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(pending);
        captures.Add(new { file = pending, scene = game.gameObject.scene.path, track = ActiveTracks[shot], attempt, sample,
            raceTime = game.FormulaElapsed, speedKph = car.absoluteSpeed, position = game.FormulaPosition,
            nearestOpponentMeters = nearest, visibleOpponents, steer = driver.inputs.steerInput,
            cameraMode = rig.cameraMode.ToString(), lookBackControl = Lookback, lapInvalid = FormulaSession.LapInvalid, hudVisible = true,
            method = "Unretouched live race; actual RCCP camera and complete authored HUD; real AI inputs and physics; no pose/velocity or UI-text writes" });
        trackCaptures++; Write();
    }
    static void VerifyFile()
    {
        Require(File.Exists(pending) && new FileInfo(pending).Length > 10000, "Screenshot was not written: " + pending);
        byte[] bytes = File.ReadAllBytes(pending);
        int width = bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19];
        int height = bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23];
        Require(width == 1920 && height == 1080, "Screenshot PNG must be 1920 x 1080, got " + width + " x " + height);
    }
    static void Write()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/CaptureSession.json", JsonConvert.SerializeObject(new
        {
            status, phase, shot, sample, attempt, trackCaptures, unity = Application.unityVersion,
            source = "Unity Editor on macOS, existing graphics profile, Nintendo UI preview",
            switchHardwareCaptured = false, isolatedSave = true, usesPersistentVisualUpgrade = true, captureSessionAssetChanges = false,
            globalVolumeProfile = OriginalProfile,
            retouching = "none", captures, rejected, errors
        }, Formatting.Indented));
    }
}
