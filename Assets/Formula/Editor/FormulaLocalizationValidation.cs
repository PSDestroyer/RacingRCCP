using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FormulaGame;
using HalvaStudio.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

[InitializeOnLoad]
public static class FormulaLocalizationValidation
{
    private const string Key = "Formula.TwelveLocaleValidation", Folder = "Docs/AI/FormulaLocalization";
    private static readonly string[] Pages = { "Acasă", "Setări", "Carieră", "Garaj", "Curse rapide", "Profil",
        "Audio", "Keyboard", "Gamepad", "Wheel", "Display", "Graphics", "Accessibility", "GarageSetup", "GarageLiveries", "CareerLast", "ProfileLast",
        "KeyboardLast", "GamepadLast", "WheelLast" };
    private static readonly List<string> checks = new List<string>(), errors = new List<string>(), overflow = new List<string>();
    private static int phase, localeIndex, pageIndex, frame, capturedFrame;
    private static float entered;
    private static double started;
    private static FormulaMainMenu menu;
    private static FormulaRaceView race;
    private static FormulaResultsView results;
    private static Gamepad pad, previousPad;
    static FormulaLocalizationValidation()
    {
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode; Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Validate Nintendo Language Localization")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var collection = LocalizationEditorSettings.GetStringTableCollection(FormulaLocalization.TableName);
        Require(collection.StringTables.Count == 12, "FormulaUI must contain exactly 12 locale tables.");
        int entries = 0;
        foreach (var key in collection.SharedData.Entries) foreach (var table in collection.StringTables)
        {
            string value = table.GetEntry(key.Id)?.Value;
            Require(!string.IsNullOrWhiteSpace(value), "Missing " + table.LocaleIdentifier.Code + ": " + key.Key);
            Require(Tokens(key.Key).SequenceEqual(Tokens(value)), "Format tokens changed: " + table.LocaleIdentifier.Code + " / " + key.Key);
            Require(!value.Contains("▁") && !value.Contains("<unk>") && !Regex.IsMatch(value, @"(.)\1{8,}"), "Translation artifact: " + key.Key); entries++;
        }
        Require(entries >= 6000, "Insufficient FormulaUI coverage.");
        var fonts = LocalizationEditorSettings.GetAssetTableCollection(FormulaLocalization.FontTableName);
        Require(fonts.AssetTables.Count == 12 && fonts.AssetTables.All(t => new[] { "Heading", "Emphasis", "Body" }.All(k => !string.IsNullOrEmpty(t.GetEntry(k)?.Guid))), "Incomplete locale font tables.");
        foreach (string path in FormulaLocalizationBuilder.Prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var binding in prefab.GetComponentsInChildren<LocalizeStringEvent>(true))
                Require(binding.OnUpdateString.GetPersistentEventCount() == 1 && binding.OnUpdateString.GetPersistentTarget(0) is TMP_Text && binding.OnUpdateString.GetPersistentMethodName(0) == "set_text" && binding.OnUpdateString.GetPersistentListenerState(0) == UnityEngine.Events.UnityEventCallState.EditorAndRuntime, "Dormant binding: " + path);
        }
        SessionState.SetInt(Key + ".Entries", entries);
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        string old = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSave", old != null); SessionState.SetString(Key + ".Save", old ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        string save = Path.Combine(Path.GetTempPath(), "FormulaTwelveLocales-" + Guid.NewGuid().ToString("N"), "save.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)); File.WriteAllText(save, "{\"PlayerName\":\"My Nickname\",\"money\":1234,\"language\":\"en\",\"futureSetting\":42}");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", save);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Restart", false); SessionState.SetBool(Key + ".Finished", false);
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/Validation.json", "{\"status\":\"running\"}");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    private static string[] Tokens(string text) => Regex.Matches(text ?? "", @"\{\d+(?::[^}]+)?\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v).ToArray();
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = SessionState.GetBool(Key + ".Restart", false) ? 100 : 0;
            localeIndex = pageIndex = capturedFrame = 0; started = EditorApplication.timeSinceStartup; pad = null; Application.runInBackground = true;
            checks.Clear(); errors.Clear(); overflow.Clear();
            if (phase == 100)
            {
                var first = JObject.Parse(File.ReadAllText(Folder + "/FirstRun.json"));
                checks.AddRange(first["checks"].ToObject<List<string>>()); errors.AddRange(first["errors"].ToObject<List<string>>()); overflow.AddRange(first["overflow"].ToObject<List<string>>());
            }
            else checks.Add("12 complete FormulaUI and font tables; persistent TMP bindings; unchanged indexed format tokens.");
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetBool(Key + ".Restart", false) && !SessionState.GetBool(Key + ".Finished", false)) { EditorApplication.delayCall += EditorApplication.EnterPlaymode; return; }
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false); Time.timeScale = 1;
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSave", false) ? SessionState.GetString(Key + ".Save", "") : null);
            string path = SessionState.GetString(Key + ".Scene", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.SetBool(Key, false);
            if (JObject.Parse(File.ReadAllText(Folder + "/Validation.json"))["status"]?.ToString() == "passed") EditorApplication.delayCall += FormulaLocalizationBuildValidation.Run;
        }
    }
    private static bool FontsReady(GameObject root) => root.GetComponentsInChildren<FormulaLocalizedFont>().All(f => f.Ready);
    private static void Inspect(GameObject root, string name)
    {
        Canvas.ForceUpdateCanvases();
        string localeCode = LocalizationSettings.SelectedLocale.Identifier.Code;
        var expectedTable = (UnityEngine.Localization.Tables.StringTable)LocalizationEditorSettings.GetStringTableCollection(FormulaLocalization.TableName).GetTable(localeCode);
        foreach (var entry in expectedTable.SharedData.Entries)
            Require(FormulaLocalization.T(entry.Key) == expectedTable.GetEntry(entry.Id).Value, "Cached locale differs from " + localeCode + " / " + entry.Key);
        if (menu != null && menu.CurrentPage == "Curse rapide")
        {
            var quick = new SerializedObject(menu.View.Quick);
            var caption = (TMP_Text)quick.FindProperty("sessionDetails").objectReferenceValue;
            Require(caption.text == expectedTable.GetEntry("RACE  /  3 LAPS  /  3 AI DRIVERS  ·  Equipped car").Value, "Quick-race summary retained a different locale.");
        }
        foreach (var label in root.GetComponentsInChildren<TMP_Text>())
        {
            label.ForceMeshUpdate(); Require(!label.text.Contains("No translation found"), "Translation missing: " + name);
            string visible = Regex.Replace(label.text, "<[^>]*>", ""); visible = new string(visible.Where(c => !char.IsControl(c)).ToArray());
            Require(label.font.HasCharacters(visible, out uint[] missing, true, true), name + " / " + label.name + " missing glyphs " + string.Join(",", missing ?? new uint[0]));
            if (label.isTextOverflowing) overflow.Add(name + " / " + label.name + " / " + label.text);
            var button = label.GetComponentInParent<UnityEngine.UI.Button>();
            if (button != null && visible.Length > 0)
            {
                var rect = (RectTransform)button.transform;
                var bounds = label.textBounds;
                var first = rect.InverseTransformPoint(label.transform.TransformPoint(bounds.min));
                var last = rect.InverseTransformPoint(label.transform.TransformPoint(bounds.max));
                var allowed = rect.rect; allowed.xMin -= 3; allowed.yMin -= 3; allowed.xMax += 3; allowed.yMax += 3;
                if (!allowed.Contains(first) || !allowed.Contains(last))
                    overflow.Add(name + " / outside button " + button.name + " / " + label.text);
            }
        }
    }
    private static void ShowMenuPage()
    {
        string page = Pages[pageIndex];
        if (pageIndex < 6) { menu.Show(page); if (pageIndex == 1) menu.View.Settings.BackWithinPage(); }
        else if (pageIndex <= 12 || pageIndex >= 17)
        {
            menu.Show("Setări");
            var settings = menu.View.Settings; settings.BackWithinPage();
            var fields = new SerializedObject(settings);
            if (pageIndex >= 10 && pageIndex <= 12)
                ((UnityEngine.UI.Button)fields.FindProperty("preferenceShortcuts").GetArrayElementAtIndex(pageIndex - 10).objectReferenceValue).onClick.Invoke();
            else
            {
                ((UnityEngine.UI.Button)fields.FindProperty("openDetails").objectReferenceValue).onClick.Invoke();
                if (pageIndex >= 7)
                {
                    ((UnityEngine.UI.Button)fields.FindProperty("controlsTab").objectReferenceValue).onClick.Invoke();
                    var controls = new SerializedObject(settings.Controls);
                    string device = pageIndex == 7 || pageIndex == 17 ? "keyboard" : pageIndex == 8 || pageIndex == 18 ? "gamepad" : "wheel";
                    ((UnityEngine.UI.Button)controls.FindProperty(device).objectReferenceValue).onClick.Invoke();
                    if (pageIndex >= 17)
                    {
                        var next = (UnityEngine.UI.Button)controls.FindProperty("next").objectReferenceValue;
                        for (int i = 0; i < 10 && next.interactable; i++) next.onClick.Invoke();
                    }
                }
            }
        }
        else if (pageIndex <= 14) menu.Show("Garaj");
        else if (pageIndex == 15)
        {
            menu.Show("Carieră"); menu.View.Career.SetPage(menu.View.Career.PageCount - 1);
        }
        else
        {
            menu.Show("Profil");
            var profile = new SerializedObject(menu.View.Profile);
            ((UnityEngine.UI.Button)profile.FindProperty("nextPage").objectReferenceValue).onClick.Invoke();
        }
        if (pageIndex == 3 || pageIndex == 13 || pageIndex == 14)
        {
            var garage = new SerializedObject(menu.View.Garage);
            int tab = pageIndex == 3 ? 0 : pageIndex == 13 ? 1 : 2;
            ((UnityEngine.UI.Button)garage.FindProperty("tabs").GetArrayElementAtIndex(tab).objectReferenceValue).onClick.Invoke();
        }
    }
    private static void AdvancePage()
    {
        pageIndex++;
        if (pageIndex < Pages.Length) ShowMenuPage();
        else { pageIndex = 0; menu.Show("Setări"); menu.View.Settings.BackWithinPage(); phase = 4; }
        entered = Time.unscaledTime;
    }
    private static void InspectVibrationStates()
    {
        var fields = new SerializedObject(menu.View.Settings);
        var label = (TMP_Text)fields.FindProperty("vibrationLabel").objectReferenceValue;
        bool original = FormulaProgression.Data.vibrationsState;
        try
        {
            foreach (bool enabled in new[] { false, true })
            {
                FormulaProgression.Data.vibrationsState = enabled; menu.View.Settings.Refresh();
                Require(label.text == FormulaLocalization.T(enabled ? "Vibration · On" : "Vibration · Off"), "Vibration state did not use its complete localized phrase.");
                label.ForceMeshUpdate(); Require(!label.isTextOverflowing, "Vibration state overflows: " + label.text);
            }
        }
        finally { FormulaProgression.Data.vibrationsState = original; menu.View.Settings.Refresh(); }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 600, "Localization validation timed out: " + phase);
            if (phase == 0 || phase == 100)
            {
                menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>();
                if (menu == null || !FormulaLocalization.Ready || !FontsReady(menu.gameObject) || Time.timeSinceLevelLoad < 1) return;
                Require(FormulaProgression.Data.PlayerName == "My Nickname" && FormulaProgression.Data.money == 1234 && FormulaProgression.Data.additionalData.ContainsKey("futureSetting"), "Identity or progress changed.");
                if (phase == 100)
                {
                    Require(LocalizationSettings.SelectedLocale.Identifier.Code == "ja", "Saved Japanese locale did not survive restart.");
                    checks.Add("A full Play Mode restart restores the saved Japanese locale.");
                    Require(FormulaSession.Launch(FormulaCatalog.Current.careerEvents[0].id, "career") == null, "Could not launch race."); phase = 101; return;
                }
                Require(LocalizationSettings.SelectedLocale.Identifier.Code == "en", "Saved English selection ignored.");
                previousPad = Gamepad.current; pad = InputSystem.AddDevice<Gamepad>(); pad.MakeCurrent();
                ShowMenuPage(); phase = 1; entered = Time.unscaledTime;
            }
            else if (phase == 1 && Time.unscaledTime - entered > .7f && FormulaLocalization.Ready && FontsReady(menu.gameObject))
            {
                string code = FormulaLocalization.SupportedCodes[localeIndex];
                Require(LocalizationSettings.SelectedLocale.Identifier.Code == code, "Wrong locale after gamepad: " + code);
                if (capturedFrame == 0)
                {
                    FormulaProgression.State.textScale = 115; FormulaPresentation.ApplyVideoPreferences(); menu.GetComponentInChildren<FormulaPresentation>()?.Refresh(); menu.View.Settings.Refresh();
                    if (Pages[pageIndex] == "Audio") InspectVibrationStates();
                    Inspect(menu.gameObject, code + " / " + Pages[pageIndex]);
                    ScreenCapture.CaptureScreenshot(Folder + "/" + code + "-" + (pageIndex < 6 ? pageIndex.ToString() : Pages[pageIndex]) + ".png"); capturedFrame = Time.frameCount;
                }
                else if (Time.frameCount > capturedFrame + 1)
                {
                    capturedFrame = 0;
                    if (pageIndex == 3)
                    {
                        menu.View.Garage.Upgrade.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "upgrade-level-1").onClick.Invoke();
                        phase = 6; entered = Time.unscaledTime;
                    }
                    else AdvancePage();
                }
            }
            else if (phase == 6 && Time.unscaledTime - entered > .7f && FontsReady(menu.gameObject))
            {
                var fields = new SerializedObject(menu.View.Dialogs);
                var requirement = (TMP_Text)fields.FindProperty("upgradeRequirement").objectReferenceValue;
                string expected = FormulaLocalization.T("LOCKED") + "\n" + FormulaLocalization.F("Earn {0} in {1} to unlock this upgrade.", FormulaCatalog.MedalName(Medal.Bronze), FormulaLocalization.T(FormulaCatalog.Current.careerEvents[0].name));
                Require(requirement.text == expected, "Upgrade unlock dialog is not localized: " + requirement.text);
                Inspect(menu.gameObject, FormulaLocalization.SupportedCodes[localeIndex] + " / upgrade dialog");
                ScreenCapture.CaptureScreenshot(Folder + "/" + FormulaLocalization.SupportedCodes[localeIndex] + "-UpgradeDialog.png"); phase = 7; frame = Time.frameCount;
            }
            else if (phase == 7 && Time.frameCount > frame + 1) { menu.Back(); AdvancePage(); phase = 1; }
            else if (phase == 4 && Time.unscaledTime - entered > .7f && FontsReady(menu.gameObject))
            {
                var button = menu.View.Settings.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "settings-language");
                EventSystem.current.SetSelectedGameObject(button.gameObject); InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                phase = 2; frame = Time.frameCount;
            }
            else if (phase == 2 && Time.frameCount > frame + 3)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState()); localeIndex++; phase = 3; frame = Time.frameCount;
            }
            else if (phase == 3 && Time.frameCount > frame + 4 && FormulaLocalization.Ready && FontsReady(menu.gameObject))
            {
                string expected = FormulaLocalization.SupportedCodes[localeIndex % 12];
                Require(LocalizationSettings.SelectedLocale.Identifier.Code == expected, "Selector failed to cycle: " + expected);
                Require(((SaveManager.SaveData)SaveManager.Instance.LoadEditor(typeof(SaveManager.SaveData))).language == expected, "Language was not saved.");
                if (localeIndex < 12) { ShowMenuPage(); phase = 1; entered = Time.unscaledTime; }
                else
                {
                    Require(!FormulaLocalization.ChangeLanguage("ro", out _) && !FormulaLocalization.ChangeLanguage("xx", out _), "Unsupported language accepted.");
                    Require(FormulaLocalization.ChangeLanguage("ja", out _), "Could not persist Japanese.");
                    checks.Add("A virtual gamepad cycles all 12 languages and back to English; every selection persists without changing identity or progress.");
                    checks.Add("20 menu views and an upgrade unlock dialog render in every locale at 115% text size, including all Settings panels, first/last pages for every controller type, garage setups/liveries and later career/profile pages; live glyph and cached-table checks pass.");
                    File.WriteAllText(Folder + "/FirstRun.json", JsonConvert.SerializeObject(new { checks, errors, overflow }, Formatting.Indented));
                    SessionState.SetBool(Key + ".Restart", true); EditorApplication.ExitPlaymode();
                }
            }
            else if (phase == 101)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>(); race = UnityEngine.Object.FindFirstObjectByType<FormulaRaceView>();
                if (game == null || race == null || !game.FormulaRaceStarted || !FontsReady(race.gameObject)) return;
                var fields = new SerializedObject(race);
                Require(((TMP_Text)fields.FindProperty("goalCaption").objectReferenceValue).text == FormulaLocalization.T("PASS TARGET"), "Race goal not localized.");
                ((UnityEngine.UI.Button)fields.FindProperty("pauseButton").objectReferenceValue).onClick.Invoke();
                ((UnityEngine.UI.Button)fields.FindProperty("restart").objectReferenceValue).onClick.Invoke();
                Require(((TMP_Text)fields.FindProperty("confirmTitle").objectReferenceValue).text == FormulaLocalization.T("RESTART SESSION?"), "Restart title not localized.");
                Require(((UnityEngine.UI.Button)fields.FindProperty("confirm").objectReferenceValue).GetComponentInChildren<TMP_Text>().text == FormulaLocalization.T("RESTART SESSION"), "Restart action changed with locale.");
                phase = 102; entered = Time.unscaledTime;
            }
            else if (phase == 102 && Time.unscaledTime - entered > .7f && FontsReady(race.gameObject))
            {
                Inspect(race.gameObject, "ja / race"); ScreenCapture.CaptureScreenshot(Folder + "/ja-RacePause.png"); phase = 103; frame = Time.frameCount;
            }
            else if (phase == 103 && Time.frameCount > frame + 1)
            {
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FormulaPlayerAvatarBuilder.ResultsPath), race.transform.parent, false);
                results = root.GetComponent<FormulaResultsView>();
                results.Present(new FormulaReceipt { eventId = FormulaCatalog.Current.careerEvents[0].id, medal = Medal.Bronze, credits = 150, xp = 200, elapsedSeconds = 90, bestLapSeconds = 90, finishPosition = 1 });
                phase = 104; entered = Time.unscaledTime;
            }
            else if (phase == 104 && Time.unscaledTime - entered > .7f && FontsReady(results.gameObject))
            {
                var fields = new SerializedObject(results); var captions = fields.FindProperty("metricCaptions");
                Require(((TMP_Text)captions.GetArrayElementAtIndex(2).objectReferenceValue).text == FormulaLocalization.T("CREDITS"), "Reward credits caption not localized.");
                Require(((TMP_Text)captions.GetArrayElementAtIndex(3).objectReferenceValue).text == FormulaLocalization.T("EXPERIENCE"), "XP caption not localized.");
                Inspect(results.gameObject, "ja / results"); ScreenCapture.CaptureScreenshot(Folder + "/ja-Results.png");
                checks.Add("An actual Japanese career race and restart dialog retain the correct actions; representative results localize medal/reward captions.");
                phase = 105; frame = Time.frameCount;
            }
            else if (phase == 105 && Time.frameCount > frame + 1)
            {
                Require(errors.Count == 0, "Runtime errors occurred: " + string.Join("; ", errors)); Write("passed", null); SessionState.SetBool(Key + ".Finished", true); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception e) { Write("failed", e.ToString()); SessionState.SetBool(Key + ".Finished", true); EditorApplication.ExitPlaymode(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Write(string status, string failure) => File.WriteAllText(Folder + "/Validation.json", JsonConvert.SerializeObject(new { status, failure, checkedEntries = SessionState.GetInt(Key + ".Entries", 0), checks, errors, overflow, unity = Application.unityVersion }, Formatting.Indented));
    private static void Log(string message, string trace, LogType type)
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
            (type == LogType.Warning && message.Contains("was not found in the [") && message.Contains("font asset"))) errors.Add(message);
    }
}
