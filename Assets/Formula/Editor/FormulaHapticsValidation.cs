using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

// Intercepts the normal Gamepad API without driving a physical controller.
[InputControlLayout(stateType = typeof(GamepadState))]
public sealed class FormulaValidationGamepad : Gamepad
{
    public float low, high, maximum;
    public int commands, nonzeroCommands;
    public bool outOfRange;
    public override void SetMotorSpeeds(float lowFrequency, float highFrequency)
    {
        low = lowFrequency; high = highFrequency; commands++;
        if (low > 0 || high > 0) nonzeroCommands++;
        maximum = Mathf.Max(maximum, Mathf.Max(low, high));
        outOfRange |= low < 0 || high < 0 || low > 1 || high > 1 || float.IsNaN(low) || float.IsNaN(high);
    }
    public override void ResetHaptics() => SetMotorSpeeds(0, 0);
}

[InitializeOnLoad]
public static class FormulaHapticsValidation
{
    private const string Key = "Formula.HapticsValidation";
    private const string Folder = "Docs/AI/FormulaHaptics";
    private static FormulaValidationGamepad first, second;
    private static Gamepad previousPad;
    private static Keyboard keyboard;
    private static GamePlayManager game;
    private static RCCP_CarController car;
    private static FormulaControllerHaptics haptics;
    private static int phase, lastFrame, enteredFrame, impactsBeforeBarrier;
    private static float phaseTime, maximumSpeed;
    private static double started;
    private static bool originalVibration;
    private static float originalIntensity;
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    static FormulaHapticsValidation()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Tools/Formula Career/Validate Controller Vibration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save edited scenes before validation.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        string save = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSave", save != null); SessionState.SetString(Key + ".Save", save ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaHaptics-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/Validation.json", "{\"status\":\"running\"}");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Formula/Scenes/Formula_Circuit_01.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            phase = lastFrame = enteredFrame = 0; maximumSpeed = 0;
            checks.Clear(); errors.Clear(); started = EditorApplication.timeSinceStartup;
            first = second = null; keyboard = null; haptics = null;
            Application.runInBackground = true;
        }
        if (mode == PlayModeStateChange.ExitingPlayMode) Cleanup();
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSave", false) ? SessionState.GetString(Key + ".Save", "") : null);
            string scene = SessionState.GetString(Key + ".Scene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            SessionState.SetBool(Key, false);
        }
    }
    private static void Next() { phase++; enteredFrame = Time.frameCount; phaseTime = Time.unscaledTime; }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 120, "Timed out in haptics phase " + phase);
            // RCCP may clear the input object when control state changes or
            // while shifting. Supply each rendered frame through its normal API.
            if (phase > 0 && phase < 12 && car != null && Time.timeScale > 0)
                car.Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = 1 });
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.CarController == null || !game.FormulaRaceStarted) return;
                car = game.CarController; haptics = game.GetComponent<FormulaControllerHaptics>();
                Require(haptics != null && UnityEngine.Object.FindObjectsByType<FormulaControllerHaptics>(FindObjectsSortMode.None).Length == 1, "Expected one haptics owner for the race.");
                originalVibration = FormulaProgression.Data.vibrationsState;
                originalIntensity = FormulaProgression.Data.vibrationIntensity;
                FormulaProgression.Data.vibrationsState = true;
                previousPad = Gamepad.current;
                InputSystem.RegisterLayout<FormulaValidationGamepad>();
                first = InputSystem.AddDevice<FormulaValidationGamepad>();
                InputSystem.QueueStateEvent(first, new GamepadState { rightTrigger = 1 }); first.MakeCurrent();
                car.canControl = true; car.externalControl = false;
                car.Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = 1 });
                Next();
            }
            else if (phase == 1)
            {
                maximumSpeed = Mathf.Max(maximumSpeed, car.Rigid.linearVelocity.magnitude);
                if (Time.unscaledTime - phaseTime < 2.3f) return;
                Require(first.nonzeroCommands > 0 && maximumSpeed > 3 && haptics.Player == car, "Moving player did not drive the virtual gamepad.");
                checks.Add("Actual race acceleration produces bounded motor commands through the normal Gamepad API.");
                FormulaProgression.Data.vibrationIntensity = 0;
                phase = 15; enteredFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            else if (phase == 15 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(first) && haptics.Intensity == 0, "Zero saved intensity left the motors running.");
                checks.Add("Saved 0% intensity immediately stops both motors without changing the On/Off preference.");
                FormulaProgression.Data.vibrationIntensity = .25f; Next();
            }
            else if (phase == 16 && Time.unscaledTime - phaseTime > .25f)
            {
                Require(!Silent(first) && Mathf.Approximately(haptics.Intensity, .25f), "Live saved intensity change did not reach the haptics owner.");
                checks.Add("Live strength changes are consumed by the actual race haptics owner.");
                FormulaProgression.Data.vibrationIntensity = originalIntensity;
                Time.timeScale = 0; phase = 2; enteredFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            else if (phase == 2 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(first), "Pause left motors running."); checks.Add("Pause stops both motors immediately.");
                Time.timeScale = 1; FormulaProgression.Data.vibrationsState = false; Next();
            }
            else if (phase == 3 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(first), "Vibration Off left motors running."); checks.Add("Existing Vibration Off preference suppresses output.");
                FormulaProgression.Data.vibrationsState = true; Next();
            }
            else if (phase == 4 && Time.unscaledTime - phaseTime > .25f)
            {
                Require(!Silent(first), "Re-enabling vibration did not resume feedback.");
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W)); Next();
            }
            else if (phase == 5 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(first), "Keyboard takeover left the pad vibrating."); checks.Add("Keyboard takeover stops controller feedback.");
                InputSystem.RemoveDevice(keyboard); keyboard = null;
                second = InputSystem.AddDevice<FormulaValidationGamepad>();
                InputSystem.QueueStateEvent(second, new GamepadState { rightTrigger = 1 }); second.MakeCurrent(); Next();
            }
            else if (phase == 6 && Time.unscaledTime - phaseTime > .25f)
            {
                Require(Silent(first) && !Silent(second), "Device switch did not stop the previous pad and select the new one.");
                checks.Add("Switching active gamepads stops the old motors and drives only the new pad.");
                haptics.SendMessage("OnApplicationFocus", false); Next();
            }
            else if (phase == 7 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(second), "Focus loss left motors running."); checks.Add("Focus-loss callback stops rumble.");
                haptics.SendMessage("OnApplicationFocus", true); Next();
            }
            else if (phase == 8 && Time.unscaledTime - phaseTime > .25f)
            {
                Require(!Silent(second), "Focus recovery did not resume rumble.");
                car.Inputs.overrideExternalInputs = true;
                phase = 13; enteredFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            else if (phase == 13 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(second), "External/AI input takeover left rumble running.");
                checks.Add("External/AI input takeover suppresses player feedback.");
                car.Inputs.overrideExternalInputs = false; Next();
            }
            else if (phase == 14 && Time.unscaledTime - phaseTime > .25f)
            {
                Require(!Silent(second), "Player control recovery did not resume feedback.");
                haptics.enabled = false; Require(Silent(second), "Disabling the owner left motors running.");
                haptics.enabled = true; checks.Add("Disable/re-enable clears pending impulses and motor output.");
                phase = 9; enteredFrame = Time.frameCount; phaseTime = Time.unscaledTime;
            }
            else if (phase == 9 && Time.unscaledTime - phaseTime > .25f)
            {
                Require(!Silent(second), "Re-enabled owner did not resume feedback.");
                var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube); barrier.name = "Haptics Runtime Impact Fixture";
                barrier.transform.position = car.transform.position + car.transform.forward * 12 + Vector3.up;
                barrier.transform.rotation = car.transform.rotation; barrier.transform.localScale = new Vector3(30, 3, 1);
                impactsBeforeBarrier = haptics.ImpactPulseCount;
                Next();
            }
            else if (phase == 10)
            {
                if (haptics.ImpactPulseCount <= impactsBeforeBarrier && Time.unscaledTime - phaseTime < 5) return;
                Require(haptics.ImpactPulseCount > impactsBeforeBarrier, "Actual barrier contact did not generate an impact pulse.");
                checks.Add("Actual player/body collision generates an impact pulse through RCCP collision events.");
                InputSystem.DisableDevice(second); Next();
            }
            else if (phase == 11 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(second), "Disabling the connected device left motors running."); checks.Add("Device-disable notification stops the motors.");
                InputSystem.EnableDevice(second);
                InputSystem.QueueStateEvent(second, new GamepadState().WithButton(GamepadButton.South)); second.MakeCurrent();
                FormulaSession.EnterMenu(); Next();
            }
            else if (phase == 12 && Time.frameCount - enteredFrame > 3)
            {
                Require(Silent(first) && Silent(second) && errors.Count == 0, "Menu state left rumble running or runtime errors occurred.");
                Require(!first.outOfRange && !second.outOfRange, "Motor output exceeded [0,1].");
                checks.Add("Leaving the race stops feedback; all observed output values stay in [0,1].");
                Write("passed", null); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception exception) { Write("failed", exception.ToString()); EditorApplication.ExitPlaymode(); }
    }
    private static bool Silent(FormulaValidationGamepad device) => device.low == 0 && device.high == 0;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Write(string status, string failure)
    {
        File.WriteAllText(Folder + "/Validation.json", JsonConvert.SerializeObject(new
        {
            status, failure, unity = Application.unityVersion, platform = Application.platform.ToString(),
            checks, errors, maximumSpeed, impactPulses = haptics != null ? haptics.ImpactPulseCount : 0,
            firstCommands = first?.commands, secondCommands = second?.commands,
            firstMaximum = first?.maximum, secondMaximum = second?.maximum,
            outputHzLimit = 20, isolatedSave = true, physicalControllerTested = false,
            switchDeviceTested = false, switchNativeInputPackageInstalled = false
        }, Formatting.Indented));
    }
    private static void Cleanup()
    {
        if (haptics != null) haptics.enabled = false;
        if (game != null) { FormulaProgression.Data.vibrationsState = originalVibration; FormulaProgression.Data.vibrationIntensity = originalIntensity; }
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (first != null && first.added) InputSystem.RemoveDevice(first);
        if (second != null && second.added) InputSystem.RemoveDevice(second);
        if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
    }
    private static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(message);
    }
}
