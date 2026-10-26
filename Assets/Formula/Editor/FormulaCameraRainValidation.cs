using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// A fixed, paused view of the actual rain race isolates the overlay's render
// cost. Save data is redirected to a temporary file for the whole play session.
[InitializeOnLoad]
public static class FormulaCameraRainValidation
{
    private const string Key = "Formula.CameraRainValidation";
    private static int phase, frames;
    private static double started;
    private static FormulaCameraRain rain;
    private static readonly List<int> baseline = new List<int>(), wet = new List<int>();
    private static readonly List<string> errors = new List<string>();
    private static bool lifecyclePassed, menuHidden;
    private static RCCP_CarController testCar;
    private static float driveAge, maxSpeed, leftInertia, rightInertia, brakeInertia;
    private static float coveredFraction;
    private static readonly List<object> motion = new List<object>();
    static FormulaCameraRainValidation()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Tools/Formula Career/Validate Lightweight Camera Rain")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before validation.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(FormulaCameraRainBuilder.MaterialPath);
        if (material == null || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Rain material has shader errors.");
        SessionState.SetString(Key + ".StartScene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        string previousSave = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSave", previousSave != null);
        SessionState.SetString(Key + ".Save", previousSave ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaCameraRain-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FormulaCameraRainBuilder.ScenePath);
        EditorApplication.EnterPlaymode();
    }

    private static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            phase = frames = 0; lifecyclePassed = menuHidden = false;
            driveAge = maxSpeed = leftInertia = rightInertia = brakeInertia = coveredFraction = 0;
            testCar = null; motion.Clear();
            baseline.Clear(); wet.Clear(); errors.Clear(); rain = null;
            started = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
        }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1;
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSave", false) ? SessionState.GetString(Key + ".Save", "") : null);
            string previous = SessionState.GetString(Key + ".StartScene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Key, false);
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 120, "Camera rain validation timed out in phase " + phase);
            // Editor updates may outnumber rendered frames. Sample only once
            // per real frame so baseline and wet counts use actual rendering.
            int frame = Time.frameCount;
            if (frame == frames) return;
            frames = frame;
            if (phase == 0)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.CarController == null || !game.FormulaRaceStarted) return;
                rain = UnityEngine.Object.FindFirstObjectByType<FormulaCameraRain>();
                Require(rain != null && rain.RainCanvas != null && rain.RainCanvas.enabled, "Rain did not appear in the wet race.");
                Require(rain.RainCanvas.sortingOrder == -100 && rain.RainCanvas.GetComponentsInChildren<FormulaLensDroplets>().Length == 1, "Rain must be one graphic below the HUD.");
                var image = rain.Droplets;
                Require(!image.raycastTarget && image.material.passCount == 1 && image.material.shader.isSupported, "Rain adds input interception or an unsupported/multiple-pass shader.");
                Require(image.mainTexture != null && image.mainTexture.width == 512 && !ShaderUtil.ShaderHasError(image.material.shader), "Rain normal or shader is invalid.");
                Require(image.SimulatedDropCount == 22 && image.CoveredScreenFraction > .0001f && image.CoveredScreenFraction < .02f, "Rain geometry is missing, too dense or rasterizes too much of the screen.");
                testCar = game.CarController;
                testCar.canControl = true; testCar.externalControl = false;
                phase = 7; driveAge = 0;
            }
            else if (phase >= 7 && phase <= 10)
            {
                driveAge += Time.deltaTime;
                maxSpeed = Mathf.Max(maxSpeed, rain.SpeedMetersPerSecond);
                if (phase == 8) leftInertia = Mathf.Max(leftInertia, rain.LensInertia.x);
                if (phase == 9) rightInertia = Mathf.Min(rightInertia, rain.LensInertia.x);
                if (phase == 10) brakeInertia = Mathf.Max(brakeInertia, rain.LensInertia.z);
                testCar.Inputs.OverrideInputs(new RCCP_Inputs
                {
                    throttleInput = phase == 7 ? 1 : phase == 10 ? 0 : .5f,
                    steerInput = phase == 8 ? -.35f : phase == 9 ? .35f : 0,
                    brakeInput = phase == 10 ? 1 : 0
                });
                float duration = phase == 7 ? 2.5f : phase == 10 ? 1.8f : 1.2f;
                if (driveAge < duration) return;
                motion.Add(new { stage = phase == 7 ? "accelerate" : phase == 8 ? "left" : phase == 9 ? "right" : "brake", speed = rain.SpeedMetersPerSecond, acceleration = rain.WorldAcceleration.ToString("F3"), inertia = rain.LensInertia.ToString("F3"), beadVelocity = rain.Droplets.MeanVelocity.ToString("F4") });
                if (phase < 10) { phase++; driveAge = 0; return; }
                testCar.Inputs.OverrideInputs(new RCCP_Inputs());
                Require(rain.TelemetrySource == testCar.Rigid && maxSpeed > 5, "Rain did not sample the moving player's actual Rigidbody.");
                Require(leftInertia > .03f && rightInertia < -.03f && brakeInertia > .1f, "Real steering/braking did not reach lens inertia.");
                coveredFraction = rain.Droplets.CoveredScreenFraction;
                Time.timeScale = 0; rain.Intensity = 0; sampleFrame = frame; phase = 1;
            }
            else if (phase == 1 && frame - sampleFrame > 10)
            {
                baseline.Add(UnityStats.drawCalls);
                if (baseline.Count < 30) return;
                ScreenCapture.CaptureScreenshot(FormulaCameraRainBuilder.ReportFolder + "/CameraRainOff.png");
                rain.Intensity = .72f; sampleFrame = frame; phase = 2;
            }
            else if (phase == 2 && frame - sampleFrame > 10)
            {
                wet.Add(UnityStats.drawCalls);
                if (wet.Count < 30) return;
                Require(rain.RainCanvas.enabled, "Enabled rain is invisible.");
                Require(Median(wet) - Median(baseline) == 1, "The visible rain overlay must add exactly one draw call in the fixed view.");
                ScreenCapture.CaptureScreenshot(FormulaCameraRainBuilder.ReportFolder + "/CameraRainOn.png");
                sampleFrame = frame; phase = 3;
            }
            else if (phase == 3 && frame - sampleFrame > 3)
            {
                rain.enabled = false; sampleFrame = frame; phase = 4;
            }
            else if (phase == 4 && frame - sampleFrame > 3)
            {
                Require(rain.RainCanvas == null && rain.GetComponentsInChildren<Canvas>().Length == 0, "Disabling rain leaked its canvas.");
                rain.enabled = true; sampleFrame = frame; phase = 5;
            }
            else if (phase == 5 && frame - sampleFrame > 3)
            {
                Require(rain.GetComponentsInChildren<Canvas>().Length == 1 && rain.RainCanvas.enabled, "Re-enabling rain duplicates or loses the overlay.");
                lifecyclePassed = true;
                FormulaSession.EnterMenu(); sampleFrame = frame; phase = 6;
            }
            else if (phase == 6 && frame - sampleFrame > 3)
            {
                menuHidden = !rain.RainCanvas.enabled;
                Require(menuHidden && errors.Count == 0, "Rain remains visible outside a race, or runtime errors occurred.");
                Write("passed", null); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception exception)
        {
            Write("failed", exception.ToString()); EditorApplication.ExitPlaymode();
        }
    }

    private static int sampleFrame;
    private static int Median(List<int> values) => values.Count == 0 ? -1 : values.OrderBy(x => x).ElementAt(values.Count / 2);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Write(string status, string failure)
    {
        Directory.CreateDirectory(FormulaCameraRainBuilder.ReportFolder);
        File.WriteAllText(FormulaCameraRainBuilder.ReportFolder + "/PlayValidation.json", JsonConvert.SerializeObject(new
        {
            status, failure, unity = Application.unityVersion, scene = FormulaCameraRainBuilder.ScenePath,
            platform = Application.platform.ToString(), graphics = SystemInfo.graphicsDeviceName,
            method = "30 distinct rendered frames per mode, paused actual wet race, same camera and HUD; UnityStats.drawCalls includes Editor rendering.",
            baselineDrawCallsMedian = Median(baseline), wetDrawCallsMedian = Median(wet),
            drawCallsDelta = Median(wet) - Median(baseline), baseline, wet,
            dropCount = FormulaLensDroplets.DropCount, coveredScreenFraction = coveredFraction,
            maxSpeed, leftInertia, rightInertia, brakeInertia, motion,
            motionMethod = "Real player RCCP input overrides: 2.5s throttle, 1.2s left, 1.2s right, 1.8s brake. No seeded velocities or pose writes.",
            lifecyclePassed, menuHidden, isolatedSave = true, errors,
            switchDeviceProfiled = false, gpuMillisecondsMeasured = false
        }, Formatting.Indented));
    }
    private static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(message);
    }
}
