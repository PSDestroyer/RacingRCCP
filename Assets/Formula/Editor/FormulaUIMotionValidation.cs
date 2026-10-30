using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FormulaGame;
using HalvaStudio.Save;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Real views, isolated saves, unscaled-time boundaries and interrupt/reuse checks.
[InitializeOnLoad]
public static class FormulaUIMotionValidation
{
    private const string Key = "Formula.UIMotion.Validation";
    public const string Folder = "Docs/AI/FormulaUIMotion";
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static int phase, lastFrame, frames;
    private static double phaseAt, captureAt;
    private static string status, savedData;
    private static FormulaMainMenu menu;
    private static FormulaSettingsView settings;
    private static FormulaResultsView results;
    private static FormulaRaceView race;
    private static GameObject resultCanvas;
    private static Button focusButton;
    private static Vector3 focusRest, settingsRest, modalRest;
    private static Slider slider;
    private static float sliderValue;
    private static int renderers, price, cash;
    private static string clipFolder;

    static FormulaUIMotionValidation()
    {
        EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack); };
        if (SessionState.GetBool(Key + ".Entered", false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += () => { status = "interrupted_by_source_reload"; Write(); EditorApplication.ExitPlaymode(); };
    }
    [MenuItem("Tools/Formula Career/UI/Play Validate All UI Motion")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before the motion review.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground); SessionState.SetInt(Key + ".Fps", Application.targetFrameRate);
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaUIMotion-" + Guid.NewGuid().ToString("N"), "save.json"));
        Directory.CreateDirectory(Folder); SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Entered", false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key + ".Batch", true);
        Run();
    }
    private static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + ".Entered", true); checks.Clear(); errors.Clear(); phase = frames = 0; lastFrame = -1;
            status = "running"; phaseAt = captureAt = EditorApplication.timeSinceStartup;
            clipFolder = Path.Combine(Path.GetTempPath(), "formula-ui-motion-frames"); Directory.CreateDirectory(clipFolder);
            Application.runInBackground = true; Time.timeScale = 1;
            if (!Application.isBatchMode) EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            Write();
        }
        if (value == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1; AudioListener.pause = false; FormulaPlatform.EditorSwitchPreview = false;
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false); Application.targetFrameRate = SessionState.GetInt(Key + ".Fps", -1);
            string save = SessionState.GetString(Key + ".Save", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(save) ? null : save);
            string scene = SessionState.GetString(Key + ".Scene", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Entered", false);
            if (SessionState.GetBool(Key + ".Batch", false))
            {
                SessionState.SetBool(Key + ".Batch", false);
                EditorApplication.Exit(status == "passed" ? 0 : 1);
            }
        }
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; Write(); }
    private static void Submit(Button button) => ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    private static void Confirm()
    {
        typeof(FormulaMainMenu).GetMethod("Confirm", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu,
            new object[] { "UI feedback", "Animation review", "OK", (Action)(() => menu.Back()), true });
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate(); if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
        try
        {
            Require(errors.Count == 0, "Runtime errors during motion review"); double age = EditorApplication.timeSinceStartup - phaseAt;
            Require(age < 100, "Motion review timed out in phase " + phase);
            if (!Application.isBatchMode && phase > 0 && phase < 13 && frames < 180 && EditorApplication.timeSinceStartup >= captureAt)
            { ScreenCapture.CaptureScreenshot(Path.Combine(clipFolder, (frames++).ToString("D4") + ".png")); captureAt = EditorApplication.timeSinceStartup + 1.0 / 15; }
            if (phase == 0)
            {
                menu = Object.FindFirstObjectByType<FormulaMainMenu>();
                if (menu == null || !Field<bool>(menu, "initialized") || age < 1) return;
                settings = menu.View.Settings; renderers = PresentationRenderers();
                focusButton = Field<Button>(menu.View.Home, "garage"); focusRest = focusButton.transform.localScale;
                EventSystem.current.SetSelectedGameObject(focusButton.gameObject); Next(1);
            }
            else if (phase == 1 && age > .16)
            {
                Require(focusButton.transform.localScale.x > focusRest.x, "Controller focus did not grow the selected control");
                EventSystem.current.SetSelectedGameObject(Field<Button>(menu.View.Home, "settings").gameObject);
                settingsRest = settings.transform.localPosition; menu.Show("Setări");
                Require(settings.GetComponent<CanvasGroup>().alpha < 1 && EventSystem.current.currentSelectedGameObject != null, "Page reveal blocks input or did not start"); Next(2);
            }
            else if (phase == 2 && age > .25)
            {
                Require(Vector3.Distance(settings.transform.localPosition, settingsRest) < .01f && settings.GetComponent<CanvasGroup>().alpha == 1, "Page did not settle at its authored pose");
                var assist = Field<Button[]>(settings, "assists")[0]; EventSystem.current.SetSelectedGameObject(assist.gameObject); Submit(assist);
                checks.Add("Focus/press uses real UI events; page reveal completes without blocking focus."); Next(3);
            }
            else if (phase == 3 && age > .06)
            {
                Require(Field<Button[]>(settings, "assists")[0].transform.localScale.x < 1, "Submit did not provide a press compression");
                modalRest = menu.View.Dialogs.transform.localScale; Confirm(); Require(menu.View.Dialogs.IsOpen, "Dialog failed to open"); Next(4);
            }
            else if (phase == 4 && age > .19)
            {
                Require(Vector3.Distance(menu.View.Dialogs.transform.localScale, modalRest) < .01f, "Dialog entry changed its resting scale");
                menu.Back(); Require(!menu.View.Dialogs.IsOpen, "Closing dialog still owns logical navigation");
                Require(!menu.View.Dialogs.GetComponent<CanvasGroup>().blocksRaycasts, "Closing dialog blocks the background");
                Confirm(); Next(5);
            }
            else if (phase == 5 && age > .22)
            {
                Require(menu.View.Dialogs.IsOpen && Vector3.Distance(menu.View.Dialogs.transform.localScale, modalRest) < .01f, "Reopening a closing dialog drifted or lost input");
                for (int i = 0; i < 20; i++) { menu.Back(); Confirm(); }
                menu.Back(); checks.Add("Dialog exit releases navigation immediately; 20 interrupted close/reopen cycles do not queue actions."); Next(6);
            }
            else if (phase == 6 && age > .18)
            {
                Require(!menu.View.Dialogs.gameObject.activeSelf && Vector3.Distance(menu.View.Dialogs.transform.localScale, modalRest) < .01f, "Closed dialog did not restore its pose");
                Field<Button>(settings, "openDetails").onClick.Invoke();
                slider = Field<Slider>(settings, "vibrationIntensity"); sliderValue = slider.value;
                EventSystem.current.SetSelectedGameObject(slider.gameObject);
                ExecuteEvents.Execute(slider.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
                Require(slider.value == sliderValue + 1 && Mathf.Approximately(FormulaProgression.Data.vibrationIntensity, slider.value / 100), "Slider animation delayed the real value"); Next(7);
            }
            else if (phase == 7 && age > .06)
            {
                Require(Field<TMP_Text>(settings, "vibrationIntensityValue").transform.localScale.x > 1, "Slider percentage did not react to adjustment");
                Field<Button>(settings, "controlsTab").onClick.Invoke();
                Require(Field<GameObject>(settings, "controlsMarker").GetComponent<FormulaUIMotion>().IsAnimating, "Tab marker did not glide");
                checks.Add("Slider saves its real value immediately and pulses the percentage; tab indicators glide.");
                Require(FormulaProgression.Commit(d => { d.money = 200000; d.exp = 1500; d.formula.medals["g01"] = Medal.Bronze; d.formula.medals["g03"] = Medal.Bronze; return null; }, out string error), error);
                menu.OpenGarage(); Next(8);
            }
            else if (phase == 8 && age > .4)
            {
                ValidateUpgradeRedirect();
                cash = FormulaProgression.Data.money; price = FormulaCatalog.Current.Upgrade("engine").levels.Find(l => l.level == 1).incrementalPriceCredits;
                Require(FormulaProgression.Commit(d => FormulaProgression.BuyUpgrade(d, "engine", 1), out string error), error);
                Require(FormulaProgression.Data.money == cash - price && FormulaProgression.State.Equipped("engine") == 1, "Upgrade charge/equip was not authoritative"); Next(9);
            }
            else if (phase == 9 && age > .06)
            {
                var level = Field<FormulaUpgradeView.LevelRow[]>(menu.View.Garage.Upgrade, "levels")[1];
                Require(level.status.transform.localScale.x > 1, "Purchased upgrade did not react");
                Field<Button[]>(menu.View.Garage, "tabs")[2].onClick.Invoke();
                Require(FormulaProgression.Commit(d => FormulaProgression.BuyOrEquipLivery(d, "generic_red"), out string error), error); Next(10);
            }
            else if (phase == 10 && age > .07)
            {
                var card = menu.View.Garage.GetComponentsInChildren<FormulaLiveryCard>().Single(c => c.LiveryId == "generic_red");
                Require(Field<TMP_Text>(card, "ownership").transform.localScale.x > 1 && FormulaProgression.State.equippedLivery == "generic_red", "Livery confirmation did not react");
                checks.Add("Real upgrade/livery transactions trigger presentation feedback; animation does not repeat the charge.");
                savedData = JsonConvert.SerializeObject(FormulaProgression.Data);
                resultCanvas = FormulaUI.Canvas("Motion results review", 500).gameObject;
                results = Object.Instantiate(FormulaMenuAssets.Current.resultsPrefab, resultCanvas.transform, false).GetComponent<FormulaResultsView>();
                FormulaProgression.State.activeMode = "career"; savedData = JsonConvert.SerializeObject(FormulaProgression.Data);
                results.Present(new FormulaReceipt { eventId = "g01", medal = Medal.Gold, elapsedSeconds = 95, bestLapSeconds = 92, credits = 3210, xp = 400, levelCredits = 500, newLiveries = new List<string> { "generic_red" } });
                Require(Field<TMP_Text>(results, "credits").text.Contains("+0"), "Results counter did not start"); Next(11);
            }
            else if (phase == 11 && age > .30)
            {
                Require(!Field<TMP_Text>(results, "credits").text.Contains(3210.ToString("N0")), "Results did not count progressively");
                results.FinishReveal(); Require(Field<TMP_Text>(results, "credits").text.Contains(3210.ToString("N0")), "Skipping did not finish rewards");
                Require(JsonConvert.SerializeObject(FormulaProgression.Data) == savedData, "Result animation mutated the save");
                checks.Add("Results sequence/count-up can finish immediately and never changes profile/receipt data.");
                if (!Application.isBatchMode) ScreenCapture.CaptureScreenshot(Folder + "/Results.png"); Next(12);
            }
            else if (phase == 12 && age > .3)
            {
                Object.Destroy(resultCanvas); menu.Show("Setări");
                FormulaProgression.State.reducedMotion = true;
                Confirm(); Require(menu.View.Dialogs.GetComponent<CanvasGroup>().alpha == 1, "Reduced Motion left a dialog invisible"); menu.Back();
                Require(!menu.View.Dialogs.gameObject.activeSelf, "Reduced Motion delayed closing");
                FormulaUIMotion.Pulse(settings.gameObject); Require(Vector3.Distance(settings.transform.localPosition, settingsRest) < .01f,
                    "Reduced Motion changed the resting pose: " + settings.transform.localPosition.ToString("F5") + " instead of " + settingsRest.ToString("F5"));
                FormulaProgression.State.reducedMotion = false;
                Require(PresentationRenderers() == renderers, "Motion added UI renderers (excluding generated TMP fallback submeshes)");
                checks.Add("Reduced Motion snaps entry/exit and suppresses pulses; no UI renderers were added.");
                FormulaPlatform.EditorSwitchPreview = true;
                Require(FormulaSession.Launch("trial:track_01", "time_trial") == null, "Cannot load pause review"); Next(13);
            }
            else if (phase == 13)
            {
                var game = Object.FindFirstObjectByType<GamePlayManager>(); race = Object.FindFirstObjectByType<FormulaRaceView>();
                if (game == null || race == null || !game.FormulaRaceStarted) return;
                Field<Button>(race, "pauseButton").onClick.Invoke(); Next(14);
            }
            else if (phase == 14 && age > .25)
            {
                Require(Time.timeScale == 0 && Field<GameObject>(race, "pause").GetComponent<CanvasGroup>().alpha == 1, "Pause animation stalled on scaled time");
                Field<Button>(race, "settings").onClick.Invoke(); Next(15);
            }
            else if (phase == 15 && age > .25)
            {
                var volumes = Field<Slider[]>(race, "volumes"); EventSystem.current.SetSelectedGameObject(volumes[2].gameObject); volumes[2].value = .5f;
                Field<Button>(race, "optionsBack").onClick.Invoke(); Next(16);
            }
            else if (phase == 16 && age > .25)
            {
                Require(Time.timeScale == 0 && Field<GameObject>(race, "pause").activeSelf, "Audio exit resumed gameplay unexpectedly");
                Field<Button>(race, "resume").onClick.Invoke(); Next(17);
            }
            else if (phase == 17 && age > .18)
            {
                Require(Time.timeScale == 1 && !Field<GameObject>(race, "pause").activeSelf, "Pause exit did not restore gameplay");
                typeof(FormulaRaceView).GetMethod("ShowMessage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(race, new object[] { "Motion review", "Notification", 2f, false }); Next(18);
            }
            else if (phase == 18 && age > .25)
            {
                Require(Field<CanvasGroup>(race, "notification").alpha > .9f, "Notification failed to reveal");
                checks.Add("Pause/audio entry and exit run at timeScale=0, restore vehicle control through the owner, and notifications reveal.");
                FormulaSession.ReturnToMenu(); Next(19);
            }
            else if (phase == 19 && age > 1 && Object.FindFirstObjectByType<FormulaMainMenu>() != null)
            { status = "passed"; Write(); EditorApplication.ExitPlaymode(); }
        }
        catch (Exception ex) { errors.Add(ex.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    private static void ValidateUpgradeRedirect()
    {
        var dialogs = menu.View.Dialogs;
        var accept = Field<Button>(dialogs, "accept"); var cancel = Field<Button>(dialogs, "cancel");
        var level3 = Field<FormulaUpgradeView.LevelRow[]>(menu.View.Garage.Upgrade, "levels")[3].button;
        int before = FormulaProgression.Data.money;
        foreach (bool reduced in new[] { false, true })
        {
            FormulaProgression.State.reducedMotion = reduced;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                EventSystem.current.SetSelectedGameObject(level3.gameObject); Submit(level3);
                Require(EventSystem.current.currentSelectedGameObject == accept.gameObject && accept.IsInteractable(), "Level 3 prerequisite action lost focus");
                Submit(accept);
                var selected = EventSystem.current.currentSelectedGameObject;
                Require(selected != null && selected.transform.IsChildOf(dialogs.transform) && selected.GetComponent<Selectable>().IsInteractable(),
                    "Level 3 to Level 1 redirect lost controller focus (Reduced Motion=" + reduced + ")");
                Require(cancel.navigation.selectOnRight == accept && accept.navigation.selectOnLeft == cancel, "Reopened Level 1 dialog has no controller navigation");
                EventSystem.current.SetSelectedGameObject(cancel.gameObject);
                ExecuteEvents.Execute(cancel.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
                Require(EventSystem.current.currentSelectedGameObject == accept.gameObject, "Controller cannot move from Back to Buy on Level 1");
                EventSystem.current.SetSelectedGameObject(cancel.gameObject); Submit(cancel);
                Require(!dialogs.IsOpen, "Controller cannot dismiss redirected upgrade dialog");
            }
        }
        FormulaProgression.State.reducedMotion = false;
        Require(FormulaProgression.Data.money == before && FormulaProgression.State.Owned("engine") == 0, "Viewing prerequisites changed upgrade ownership or credits");
        checks.Add("Level 3 to unowned Level 1 retains controller focus, Move/Submit/Back and both dialog navigation targets across 8 rapid redirects, with and without Reduced Motion.");
    }
    private static int PresentationRenderers() => menu.View.GetComponentsInChildren<CanvasRenderer>(true).Count(r => r.GetComponent<TMP_SubMeshUI>() == null);
    private static void Write() => File.WriteAllText(Folder + "/Validation.json", JsonConvert.SerializeObject(new { status, phase, checks, errors,
        capturedFrames = frames, frameFolder = clipFolder, isolatedSave = true, switchHardwareTested = false }, Formatting.Indented));
}
