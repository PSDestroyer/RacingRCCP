using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Editor-only preview of the exact platform policy used by UNITY_SWITCH builds.
// It cannot validate Nintendo SDK integration or target GPU behaviour.
[InitializeOnLoad]
public static class FormulaSwitchPresentationValidation
{
    public const string Folder = "Docs/AI/FormulaSwitchPresentation";
    private const string Key = "Formula.SwitchPresentation.Test";
    private static readonly string[] NewKeys = { "Left stick", "Right stick", "Press left stick", "Press right stick", "Directional buttons", "\nB · Cancel", "Joy-Con / Pro Controller", "Volume and vibration\nJoy-Con / Pro Controller" };
    private static readonly List<string> errors = new List<string>();
    private static readonly List<object> results = new List<object>();
    private static int phase, locale;
    private static double phaseAt;
    private static string status;
    private static FormulaMainMenu menu;
    private static FormulaSettingsView settings;

    static FormulaSwitchPresentationValidation()
    {
        // Initialize before scene Awake/Start on the Play Mode domain reload.
        FormulaPlatform.EditorSwitchPreview = SessionState.GetBool(Key, false);
        EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack);
        };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Preview() { if (SessionState.GetBool(Key, false)) FormulaPlatform.EditorSwitchPreview = true; }

    [MenuItem("Tools/Formula Career/Switch/Update Platform UI Copy")]
    public static void UpdateCopy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Update localized copy in Edit Mode.");
        var collection = LocalizationEditorSettings.GetStringTableCollection(FormulaLocalization.TableName);
        if (collection == null) throw new InvalidOperationException("Formula localization collection missing.");
        foreach (string code in FormulaLocalization.SupportedCodes)
        {
            var translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("Assets/Formula/Localization/Translations/" + code + ".json"));
            var table = collection.StringTables.Single(t => t.LocaleIdentifier.Code == code);
            foreach (string key in NewKeys)
            {
                if (!translations.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Platform copy missing: " + code + "/" + key);
                table.AddEntry(key, text);
            }
            EditorUtility.SetDirty(table); AssetDatabase.SaveAssetIfDirty(table);
            if (code == "ja" || code == "ko" || code.StartsWith("zh-", StringComparison.Ordinal))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Formula/Localization/Fonts/Formula CJK-" + code + ".asset");
                string chars = new string(string.Concat(NewKeys.Select(k => translations[k])).Where(c => !char.IsControl(c)).Distinct().ToArray());
                if (!font.TryAddCharacters(chars, out string missing) && !font.HasCharacters(chars, out uint[] _, true, false))
                    throw new InvalidOperationException("Platform font glyphs missing: " + code + " / " + missing);
                foreach (var atlas in font.atlasTextures)
                {
                    if (!EditorUtility.IsPersistent(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
                    EditorUtility.SetDirty(atlas);
                }
                EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            }
        }
        EditorUtility.SetDirty(collection.SharedData); AssetDatabase.SaveAssetIfDirty(collection.SharedData);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/Localization.json", JsonConvert.SerializeObject(new { status = "passed", locales = 12, keys = NewKeys, fontGlyphsPrewarmed = true }, Formatting.Indented));
    }

    [MenuItem("Tools/Formula Career/Switch/Play Validate Platform Presentation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetInt(Key + ".Fps", Application.targetFrameRate);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaSwitchPresentation-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key, true); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            FormulaPlatform.EditorSwitchPreview = true; errors.Clear(); results.Clear(); phase = locale = 0; status = "running";
            phaseAt = EditorApplication.timeSinceStartup; Application.runInBackground = true; Directory.CreateDirectory(Folder);
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus(); Write();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            FormulaPlatform.EditorSwitchPreview = false;
            Application.targetFrameRate = SessionState.GetInt(Key + ".Fps", -1); Time.timeScale = 1;
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            string save = SessionState.GetString(Key + ".Save", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(save) ? null : save);
            string scene = SessionState.GetString(Key + ".Scene", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            SessionState.SetBool(Key, false);
        }
    }
    private static T Field<T>(object value, string field) => (T)value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; Write(); }
    private static void Scan(Transform root)
    {
        string[] forbidden = { "Keyboard", "KEYBOARD", "Gamepad", "GAMEPAD", "Steering wheel", "STEERING WHEEL", "DISPLAY", "GRAPHICS", "DISPLAY MODE", "FRAME LIMIT", "QUALITY", "V-SYNC", "Volume and vibration\nKeyboard, gamepad and steering wheel" };
        foreach (var text in root.GetComponentsInChildren<TMP_Text>())
        {
            if (!text.enabled) continue;
            var source = text.GetComponent<LocalizeStringEvent>(); string key = source?.StringReference.TableEntryReference.Key;
            Require(!forbidden.Contains(key), "Foreign/video source visible: " + key);
            Require(!System.Text.RegularExpressions.Regex.IsMatch(text.text, @"(?i)\b(Xbox|PlayStation|DualShock|DualSense|Keyboard|Gamepad|WASD|ESC|LB|RB|LT|RT)\b"), "Foreign input text visible: " + text.text);
        }
    }
    private static void CheckControls()
    {
        var controls = settings.Controls;
        Require(!Field<Button>(controls, "keyboard").gameObject.activeSelf && !Field<Button>(controls, "gamepad").gameObject.activeSelf && !Field<Button>(controls, "wheel").gameObject.activeSelf, "Redundant/foreign device tabs visible");
        Require(Field<TMP_Text>(controls, "pageLabel").text.Contains(FormulaPlatform.SwitchControllerName), "Nintendo controller heading missing");
        foreach (var row in Field<FormulaControlsView.Row[]>(controls, "rows").Where(r => r.button.gameObject.activeSelf))
            Require(row.binding.text != "—" && !string.IsNullOrEmpty(row.binding.text), "Unmapped Nintendo binding for " + row.action.text);
        Scan(menu.View.transform);
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            Require(errors.Count == 0, "Errors during Switch presentation preview");
            double age = EditorApplication.timeSinceStartup - phaseAt; Require(age < 90, "Presentation test timed out in phase " + phase);
            if (phase == 0)
            {
                menu = Object.FindFirstObjectByType<FormulaMainMenu>();
                if (menu == null || !FormulaLocalization.Ready || age < 2) return;
                Require(FormulaInputHints.Gamepad && FormulaInputHints.Confirm.StartsWith("A") && FormulaInputHints.Back.StartsWith("B"), "Switch prompts need a connected pad");
                Require(!FormulaInputHints.Sections.Contains("LB"), "Non-Nintendo tab buttons");
                var ui = UnityEngine.EventSystems.EventSystem.current.GetComponent<InputSystemUIInputModule>();
                File.WriteAllText(Folder + "/UIBindings.json", JsonConvert.SerializeObject(new { submit = ui.submit.action.bindings.Select(b => new { b.path, b.effectivePath }), cancel = ui.cancel.action.bindings.Select(b => new { b.path, b.effectivePath }) }, Formatting.Indented));
                Require(ui.submit.action.bindings.Any(b => b.effectivePath == "<Gamepad>/buttonEast") && ui.cancel.action.bindings.Any(b => b.effectivePath == "<Gamepad>/buttonSouth"), "Nintendo A/B UI mapping missing");
                var profile = FormulaProgression.State; int quality = QualitySettings.GetQualityLevel(), sync = QualitySettings.vSyncCount; bool fullscreen = Screen.fullScreen;
                profile.qualityLevel = 999; profile.vSync = sync == 0 ? 1 : 0; profile.frameLimit = 120; profile.displayMode = fullscreen ? 0 : 1;
                FormulaPresentation.ApplyVideoPreferences();
                Require(QualitySettings.GetQualityLevel() == quality && QualitySettings.vSyncCount == sync && Screen.fullScreen == fullscreen && Application.targetFrameRate == 60, "Old video preferences affect Switch");
                menu.Show("Setări");
                settings = menu.View.Settings;
                var shortcuts = Field<Button[]>(settings, "preferenceShortcuts"); var tabs = Field<Button[]>(settings, "preferenceTabs");
                Require(!shortcuts[0].gameObject.activeSelf && !shortcuts[1].gameObject.activeSelf && !tabs[0].gameObject.activeSelf && !tabs[1].gameObject.activeSelf, "Video entry points visible");
                foreach (int i in new[] { 0, 2, 3, 4 }) { var button = Field<Button[]>(settings, "preferenceButtons")[i]; Require(!button.gameObject.activeSelf, "Video option active"); button.onClick.Invoke(); }
                Require(profile.qualityLevel == 999 && profile.frameLimit == 120 && profile.vSync != sync, "Hidden callback modified video preferences");
                Field<Button>(settings, "openDetails").onClick.Invoke(); Field<Button>(settings, "controlsTab").onClick.Invoke();
                CheckControls(); var rows = Field<FormulaControlsView.Row[]>(settings.Controls, "rows"); rows[0].button.onClick.Invoke();
                Require(settings.Controls.IsRebinding && Field<TMP_Text>(settings.Controls, "prompt").text.Contains("B ·"), "Nintendo rebind cancel prompt missing");
                settings.Controls.CancelRebind(); Require(!settings.Controls.IsRebinding, "Rebind cancellation failed");
                results.Add(new { check = "Nintendo startup, hidden callbacks, saved video preferences, A/B mapping, controls and rebind", status = "passed" });
                Next(1);
            }
            else if (phase == 1)
            {
                string code = FormulaLocalization.SupportedCodes[locale];
                Require(FormulaLocalization.ChangeLanguage(code, out string error), error); Next(2);
            }
            else if (phase == 2)
            {
                if (!FormulaLocalization.Ready || age < .6 || settings.Controls.GetComponentsInChildren<FormulaLocalizedFont>().Any(f => !f.Ready)) return;
                settings.Refresh(); CheckControls();
                var next = Field<Button>(settings.Controls, "next");
                while (next.interactable) { next.onClick.Invoke(); CheckControls(); }
                var previous = Field<Button>(settings.Controls, "previous"); while (previous.interactable) previous.onClick.Invoke();
                results.Add(new { locale = FormulaLocalization.SupportedCodes[locale], check = "all controller pages and platform strings", status = "passed" });
                if (new[] { "en", "de", "ja", "ko", "zh-Hans", "zh-Hant" }.Contains(FormulaLocalization.SupportedCodes[locale])) ScreenCapture.CaptureScreenshot(Folder + "/Controls-" + FormulaLocalization.SupportedCodes[locale] + ".png");
                Next(3);
            }
            else if (phase == 3 && age > .5)
            {
                locale++;
                if (locale < FormulaLocalization.SupportedCodes.Length) Next(1);
                else
                {
                    Field<Button[]>(settings, "preferenceTabs")[2].onClick.Invoke();
                    var buttons = Field<Button[]>(settings, "preferenceButtons"); var pages = Field<GameObject[]>(settings, "preferencePages");
                    Require(buttons[1].transform.parent == pages[2].transform, "Safe area did not move to accessibility");
                    foreach (int i in new[] { 1, 5, 6, 7 }) Require(buttons[i].gameObject.activeInHierarchy, "Accessibility option hidden");
                    int safeArea = FormulaProgression.State.safeAreaPercent; buttons[1].onClick.Invoke(); Require(FormulaProgression.State.safeAreaPercent != safeArea, "Safe area cannot be adjusted");
                    Scan(menu.View.transform); ScreenCapture.CaptureScreenshot(Folder + "/Accessibility.png"); Next(4);
                }
            }
            else if (phase == 4 && age > .6)
            {
                // A fresh copy verifies desktop presentation still exposes its own
                // device choices and display/graphics tabs. It never enters a scene.
                FormulaPlatform.EditorSwitchPreview = false;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormulaMenuHierarchyBuilder.PrefabPath); var desktop = Object.Instantiate(prefab);
                try
                {
                    desktop.SetActive(false); var view = desktop.GetComponent<FormulaMenuView>(); view.Settings.Bind(_ => { });
                    Require(Field<Button>(view.Settings.Controls, "keyboard").gameObject.activeSelf && Field<Button>(view.Settings.Controls, "wheel").gameObject.activeSelf, "Desktop device options changed");
                    Require(Field<Button[]>(view.Settings, "preferenceTabs").All(b => b.gameObject.activeSelf), "Desktop video tabs changed");
                    results.Add(new { check = "desktop device and video UI preserved", status = "passed" });
                }
                finally { Object.DestroyImmediate(desktop); FormulaPlatform.EditorSwitchPreview = true; }
                Require(FormulaLocalization.ChangeLanguage("en", out string error), error);
                Next(8);
            }
            else if (phase == 8)
            {
                if (!FormulaLocalization.Ready || age < 1) return;
                Require(FormulaSession.Launch("trial:track_01", "time_trial") == null, "Could not launch HUD check"); Next(5);
            }
            else if (phase == 5)
            {
                var game = Object.FindFirstObjectByType<GamePlayManager>(); var view = Object.FindFirstObjectByType<FormulaRaceView>();
                if (game == null || view == null || !game.FormulaRaceStarted || age < 2) return;
                Require(Field<TMP_Text>(view, "pauseHint").text == "+", "Race HUD uses a non-Nintendo pause hint");
                Scan(view.transform); Field<Button>(view, "pauseButton").onClick.Invoke();
                Scan(view.transform); Field<Button>(view, "settings").onClick.Invoke(); Scan(view.transform);
                ScreenCapture.CaptureScreenshot(Folder + "/RaceOptions.png");
                results.Add(new { check = "race HUD + pause and audio options", status = "passed" }); Next(6);
            }
            else if (phase == 6 && age > .5) { Time.timeScale = 1; FormulaSession.ReturnToMenu(); Next(7); }
            else if (phase == 7 && age > 1 && Object.FindFirstObjectByType<FormulaMainMenu>() != null)
            { status = "passed"; Write(); EditorApplication.ExitPlaymode(); }
        }
        catch (Exception ex) { errors.Add(ex.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    private static void Write() => File.WriteAllText(Folder + "/PlayValidation.json", JsonConvert.SerializeObject(new { status, phase, checks = results,
        mode = "Editor preview using the production Switch presentation policy", nativeSwitchBuild = false, isolatedSave = true, errors }, Formatting.Indented));
}
