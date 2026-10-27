using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using HalvaStudio.Save;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class FormulaVibrationSettingsValidation
{
    private const string Key = "Formula.VibrationSettingsValidation";
    private const string Folder = "Docs/AI/FormulaHaptics";
    private static int phase, lastFrame, enteredFrame;
    private static double started;
    private static FormulaMainMenu menu;
    private static FormulaSettingsView settings;
    private static UnityEngine.UI.Slider slider;
    private static UnityEngine.UI.Button toggle;
    private static TMP_Text valueLabel;
    private static Gamepad pad, previousPad;
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();

    static FormulaVibrationSettingsValidation()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Validate Vibration Intensity Setting")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        string save = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSave", save != null); SessionState.SetString(Key + ".Save", save ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaVibrationSettings-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/SettingsValidation.json", "{\"status\":\"running\"}");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = lastFrame = enteredFrame = 0; checks.Clear(); errors.Clear(); pad = null;
            started = EditorApplication.timeSinceStartup; Application.runInBackground = true;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSave", false) ? SessionState.GetString(Key + ".Save", "") : null);
            string scene = SessionState.GetString(Key + ".Scene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            SessionState.SetBool(Key, false);
        }
    }
    private static void Next() { phase++; enteredFrame = Time.frameCount; }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 90, "Settings validation timed out in phase " + phase);
            if (phase == 0)
            {
                menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>();
                if (menu == null || menu.View == null || Time.timeSinceLevelLoad < 1) return;
                var old = JsonConvert.DeserializeObject<SaveManager.SaveData>("{\"money\":123,\"legacySetting\":42}");
                Require(Mathf.Approximately(old.vibrationIntensity, .65f), "Old save did not retain the default rumble strength.");
                old.vibrationIntensity = 2; FormulaProgression.Ensure(old); Require(old.vibrationIntensity == 1, "Save upper intensity bound is not clamped.");
                old.vibrationIntensity = float.NaN; FormulaProgression.Ensure(old); Require(Mathf.Approximately(old.vibrationIntensity, .65f), "Invalid intensity did not recover the safe default.");
                Require(JsonConvert.SerializeObject(old).Contains("legacySetting"), "Save migration lost unknown data.");
                checks.Add("Old saves default to 65%; invalid values are sanitized and unrelated save data survives.");
                menu.Show("Setări"); settings = menu.View.Settings;
                settings.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "existing-settings").onClick.Invoke();
                slider = settings.GetComponentsInChildren<UnityEngine.UI.Slider>().Single(s => s.name == "vibration-intensity");
                toggle = settings.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "settings-vibration");
                valueLabel = slider.transform.parent.Find("Percentage").GetComponent<TMP_Text>();
                Require(slider.minValue == 0 && slider.maxValue == 100 && slider.wholeNumbers && slider.value == 65, "Authored intensity slider has wrong range/default.");
                previousPad = Gamepad.current; pad = InputSystem.AddDevice<Gamepad>(); pad.MakeCurrent();
                EventSystem.current.SetSelectedGameObject(slider.gameObject);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadRight)); Next();
            }
            else if (phase == 1 && Time.frameCount - enteredFrame > 3)
            {
                Require(slider.value == 66 && Mathf.Approximately(FormulaProgression.Data.vibrationIntensity, .66f) && valueLabel.text == "66%", "Gamepad right did not change the real slider and saved value by 1%.");
                checks.Add("Gamepad D-pad adjusts the authored slider by 1% and updates the displayed/saved value immediately.");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadUp)); Next();
            }
            else if (phase == 2 && Time.frameCount - enteredFrame > 3)
            {
                Require(EventSystem.current.currentSelectedGameObject == toggle.gameObject, "Gamepad Up did not reach Vibration On/Off.");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); Next();
            }
            else if (phase == 3 && Time.frameCount - enteredFrame > 3)
            {
                Require(EventSystem.current.currentSelectedGameObject == slider.gameObject, "Gamepad Down did not return to intensity.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                checks.Add("Controller navigation moves between Vibration On/Off and intensity without trapping focus.");
                slider.value = 0; Require(FormulaProgression.Data.vibrationIntensity == 0 && valueLabel.text == "0%", "Zero intensity boundary failed.");
                slider.value = 100; Require(FormulaProgression.Data.vibrationIntensity == 1 && valueLabel.text == "100%", "Full intensity boundary failed.");
                slider.value = 25; toggle.onClick.Invoke();
                Require(!FormulaProgression.Data.vibrationsState && slider.value == 25, "Off discarded the chosen intensity.");
                toggle.onClick.Invoke(); Require(FormulaProgression.Data.vibrationsState && slider.value == 25, "On failed to preserve intensity.");
                checks.Add("0% and 100% boundaries work, and On/Off preserves the chosen strength.");
                menu.Back();
                var loaded = (SaveManager.SaveData)SaveManager.Instance.LoadEditor(typeof(SaveManager.SaveData));
                Require(Mathf.Approximately(loaded.vibrationIntensity, .25f) && loaded.vibrationsState, "Leaving settings did not persist the preference to disk.");
                SaveManager.Instance.saveData = loaded; FormulaProgression.Ensure(loaded);
                settings.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "existing-settings").onClick.Invoke(); settings.Refresh();
                Require(slider.value == 25 && valueLabel.text == "25%", "Reloaded preference was not reflected by the UI.");
                checks.Add("Settings save through SaveManager, reload from disk and restore the slider correctly.");
                FormulaProgression.State.textScale = 115; FormulaPresentation.ApplyVideoPreferences(); Next();
            }
            else if (phase == 4 && Time.frameCount - enteredFrame > 5)
            {
                var title = slider.transform.parent.Find("Channel").GetComponent<TMP_Text>();
                title.ForceMeshUpdate(); valueLabel.ForceMeshUpdate();
                Require(!title.isTextOverflowing && !valueLabel.isTextOverflowing, "Vibration label overflows at 115% text scale.");
                checks.Add("Intensity label and value fit at 115% text size.");
                FormulaProgression.State.textScale = 100; FormulaPresentation.ApplyVideoPreferences();
                EventSystem.current.SetSelectedGameObject(slider.gameObject); Next();
            }
            else if (phase == 5 && Time.frameCount - enteredFrame > 5)
            {
                ScreenCapture.CaptureScreenshot(Folder + "/VibrationSettings.png"); Next();
            }
            else if (phase == 6 && Time.frameCount - enteredFrame > 3)
            {
                Require(errors.Count == 0, "Runtime errors occurred."); Write("passed", null); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception exception) { Write("failed", exception.ToString()); EditorApplication.ExitPlaymode(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Write(string status, string failure) => File.WriteAllText(Folder + "/SettingsValidation.json", JsonConvert.SerializeObject(new { status, failure, unity = Application.unityVersion, checks, errors, isolatedSave = true }, Formatting.Indented));
    private static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(message);
    }
}
