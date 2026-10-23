using System;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Explicit menu acceptance run with an isolated save. No automatic work on reload.
[InitializeOnLoad]
public static class FormulaMenuPreview
{
    private const string Active = "FormulaMenu.Check.Active", Stage = "FormulaMenu.Check.Stage", Previous = "FormulaMenu.Check.Previous", ReportKey = "FormulaMenu.Check.Report";
    [Serializable] private sealed class Report
    {
        public string phase;
        public string[] passed = Array.Empty<string>(), errors = Array.Empty<string>();
        public float equippedTorque, equippedBrakes, equippedSpring, equippedDownforce, equippedDrag;
        public int frame, startFrame;
    }
    private static Report report;
    private static double started;
    private static Gamepad simulatedPad;
    private static Keyboard simulatedKeyboard;
    private static int[] authoredUiObjects;
    static FormulaMenuPreview()
    {
        EditorApplication.playModeStateChanged += OnMode;
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }
    [MenuItem("Tools/Formula Career/Preview and Validate Figma Menu")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (FormulaMenuAssets.Current == null) throw new InvalidOperationException("Build Figma Menu Assets first.");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaMenu-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active, true); SessionState.SetInt(Stage, 0);
        SessionState.SetString(ReportKey, JsonUtility.ToJson(new Report()));
        SessionState.SetString("FormulaMenu.Check.InputOverrides", RCCP_InputActions.Instance.inputActions.SaveBindingOverridesAsJson());
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, "")); started = EditorApplication.timeSinceStartup;
            report.startFrame = Time.frameCount; Application.runInBackground = true;
            var view = Type.GetType("UnityEditor.GameView,UnityEditor"); if (view != null) EditorWindow.GetWindow(view).Focus();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            var actions = RCCP_InputActions.Instance.inputActions;
            actions.RemoveAllBindingOverrides(); actions.LoadBindingOverridesFromJson(SessionState.GetString("FormulaMenu.Check.InputOverrides", ""));
            SessionState.SetBool(Active, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
            string previous = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Pass(string message) => report.passed = report.passed.Concat(new[] { message }).ToArray();
    private static UnityEngine.UI.Button Button(string id) => UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b => b.name == id && b.gameObject.activeInHierarchy);
    private static void Click(string id)
    {
        var button = Button(id); Require(button.IsInteractable(), "Control is not actionable: " + id);
        EventSystem.current.SetSelectedGameObject(button.gameObject); button.onClick.Invoke();
    }
    private static void Next(int stage, string phase)
    {
        report.frame = Time.frameCount; report.phase = phase;
        SessionState.SetInt(Stage, stage); started = EditorApplication.timeSinceStartup;
        string json = JsonUtility.ToJson(report, true); SessionState.SetString(ReportKey, json);
        Directory.CreateDirectory("Docs/AI/FormulaUI"); File.WriteAllText("Docs/AI/FormulaUI/RuntimeValidation.json", json);
    }
    private static void Capture(string name)
    {
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t => t.font == FormulaMenuAssets.Current.titleFont && !string.IsNullOrWhiteSpace(t.text)))
        {
            text.ForceMeshUpdate();
            Require(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c => c.isVisible), "Title has no rendered glyphs: " + text.name + " / " + text.text);
        }
        foreach (var text in UnityEngine.Object.FindFirstObjectByType<FormulaMenuView>().GetComponentsInChildren<TMP_Text>())
            Require(!System.Text.RegularExpressions.Regex.IsMatch(text.text, "[ăâîșțĂÂÎȘȚşţŞŢ]|\\b(?:GARAJ|DEZVOLTARE|LIVREE|NIVEL|PROBE|BRONZ|ARGINT|AUR)\\b"), "Romanian display text remains: " + text.text);
        ScreenCapture.CaptureScreenshot("Docs/AI/FormulaUI/" + name + ".png");
    }
    private static void Update()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - started;
            Require(age < 60, "Figma menu phase timed out: " + report.phase); if (age < .5) return;
            int stage = SessionState.GetInt(Stage, 0);
            var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>();
            var car = UnityEngine.Object.FindFirstObjectByType<FormulaVehicle>();
            var canvas = menu != null ? menu.View.Canvas : null;
            if (stage == 0 && age > 2)
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleAsync;
                if (!locale.IsDone) return;
                Require(locale.Result != null && locale.Result.Identifier.Code == "en", "English was not selected as the startup locale.");
                Require(menu != null && menu.CurrentPage == "Acasă" && car.IsReady, "Home/Generic failed to initialize.");
                Require(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Legacy or duplicate Canvas remains in the scene.");
                Require(menu.legacyCanvas == null && menu.legacyNavigation == null, "Menu still depends on the legacy UI.");
                Require(menu.View.Screens.childCount == 6 && menu.View.Screens.Cast<Transform>().Count(t => t.gameObject.activeSelf) == 1, "Exactly one of the six authored screens must be active.");
                authoredUiObjects = canvas.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).OrderBy(id => id).ToArray();
                Pass("Serialized menu is active immediately with one Canvas, six persistent screens and no legacy presenter");
                Require(Time.frameCount > report.startFrame + 5, "Runtime frames did not advance.");
                Require(FormulaMenuAssets.Current.liveries.Length == 8 && FormulaMenuAssets.Current.liveries.All(t => t != null), "Livery artwork missing.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.font == FormulaMenuAssets.Current.titleFont), "Teko title font not rendered.");
                Require(canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Count(i => i.texture != null) >= 8, "Original menu icon slots missing.");
                Require(UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None).Length == 1, "Duplicate preview car.");
                var displayWheels = car.GetComponentsInChildren<RCCP_WheelCollider>();
                Require(displayWheels.Length == 4 && displayWheels.All(w => w.wheelModel != null && w.wheelModel.gameObject.activeInHierarchy && w.wheelModel.GetComponentsInChildren<Renderer>().Any(r => r.enabled)), "Parked Generic must keep all four wheel models visible.");
                Pass("Home renders with Teko/Rajdhani, original Figma artwork, one live Generic and advancing frames");
                Capture("Home"); Next(100, "continue-briefing");
            }
            else if (stage == 100) { Click("continue-career"); Next(101, "briefing-render"); }
            else if (stage == 101)
            {
                Require(menu.View.Dialogs.IsOpen && EventSystem.current.currentSelectedGameObject.name == "confirm", "Continue did not focus the free race start action.");
                Require(canvas.GetComponentsInChildren<FormulaTrackMap>().Any(), "Briefing circuit outline missing.");
                Capture("EventBriefing"); Next(102, "briefing-back");
            }
            else if (stage == 102)
            {
                menu.Back(); Require(EventSystem.current.currentSelectedGameObject.name == "continue-career", "Briefing cancel lost Continue focus.");
                Require(FormulaProgression.Data.money == 0, "Inspecting the race spent credits.");
                Pass("Continue opens a structured race briefing with circuit outline and primary launch focus; Back restores Continue without spending");
                Next(1, "career");
            }
            else if (stage == 1)
            {
                Click("home-career"); Require(menu.CurrentPage == "Carieră", "Career overview is unreachable from Home.");
                Pass("Career overview is reachable through the actual Home navigation"); Next(2, "career-capture");
            }
            else if (stage == 2)
            {
                Require(FormulaCatalog.Current.careerEvents.All(e => Button(e.id) != null), "Career card missing.");
                Require(Button("g06").interactable, "Locked events must remain inspectable.");
                Pass("Six career cards preserve event locks and rewards"); Capture("Career"); Next(21, "locked-inspection");
            }
            else if (stage == 21) { Click("g06"); Require(!Button("confirm").interactable, "Locked event allowed launch."); Next(22, "locked-dialog"); }
            else if (stage == 22) { Capture("LockedEvent"); Next(23, "locked-return"); }
            else if (stage == 23)
            {
                Click("cancel"); Require(EventSystem.current.currentSelectedGameObject.name == "g06", "Closing event details lost the selected career card.");
                Pass("Locked career content can be inspected, cannot launch and restores the original card focus"); Next(20, "garage-open");
            }
            else if (stage == 20) { menu.Show("Garaj"); Click("garage-2"); Next(3, "liveries"); }
            else if (stage == 3)
            {
                Click("generic_blue"); Require(FormulaProgression.State.equippedLivery == "generic_orange", "Preview silently equipped the livery.");
                Click("livery-apply"); Require(FormulaProgression.State.equippedLivery == "generic_blue" && FormulaProgression.Data.money == 0, "Explicit free livery equip failed.");
                Require(canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Count(i => i.name == "Generic Livery Thumbnail" && i.texture != null) == 8, "Eight individual Generic thumbnails not rendered.");
                Pass("Eight livery previews never mutate ownership; explicit free equip preserves wallet and development"); Next(30, "livery-render");
            }
            else if (stage == 30) { Capture("Liveries"); Next(31, "setup-open"); }
            else if (stage == 31) { Click("garage-1"); Click("setup-speed"); Next(4, "setup-capture"); }
            else if (stage == 4)
            {
                var controller = car.GetComponent<RCCP_CarController>();
                Require(Mathf.Approximately(controller.AeroDynamics.downForce, 6f * .92f), "Speed setup was not applied to RCCP.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == controller.AeroDynamics.downForce.ToString("0.##")), "UI statistics are stale after setup.");
                Pass("Setup statistics refresh from applied RCCP values after progression events"); Capture("Setup"); Next(40, "setup-render");
            }
            else if (stage == 40)
            {
                Require(FormulaProgression.Commit(d => { d.money = 20000; d.formula.medals["g01"] = Medal.Bronze; return null; }, out string error), error);
                Click("garage-0"); Click("buy-engine"); Next(5, "purchase-dialog");
            }
            else if (stage == 5)
            {
                var selected = EventSystem.current.currentSelectedGameObject?.GetComponent<UnityEngine.UI.Button>();
                Require(selected != null && (selected.name == "cancel" || selected.name == "confirm"), "Dialog did not capture controller focus.");
                Require(selected.FindSelectableOnRight()?.name == "confirm", "Dialog controller navigation escaped.");
                Capture("Purchase"); Next(50, "purchase-render");
            }
            else if (stage == 50)
            {
                Click("cancel"); Require(EventSystem.current.currentSelectedGameObject.name == "buy-engine", "Purchase cancel did not restore its origin button."); Require(FormulaProgression.Data.money == 20000 && FormulaProgression.State.Owned("engine") == 0, "Cancel changed the save.");
                Click("buy-engine"); Click("confirm"); Next(6, "development-capture");
            }
            else if (stage == 6)
            {
                var controller = car.GetComponent<RCCP_CarController>();
                Require(FormulaProgression.State.Owned("engine") == 1 && FormulaProgression.Data.money == 20000 - FormulaCatalog.Current.Upgrade("engine").levels[0].incrementalPriceCredits, "Upgrade price/ownership mismatch.");
                report.equippedTorque = controller.Engine.maximumTorqueAsNM; report.equippedBrakes = controller.FrontAxle.maxBrakeTorque;
                report.equippedDownforce = controller.AeroDynamics.downForce; report.equippedDrag = controller.AeroDynamics.airResistance;
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == controller.Engine.maximumTorqueAsNM.ToString("0.#") + " Nm"), "UI torque differs from RCCP.");
                Require(EventSystem.current.currentSelectedGameObject.name == "buy-engine", "Successful purchase lost its source focus.");
                Pass("Upgrade purchase/cancel, source focus, credit cost, ownership and live torque statistics agree");
                Click("buy-suspension"); Click("confirm"); Next(61, "suspension-statistics");
            }
            else if (stage == 61)
            {
                report.equippedSpring = car.GetComponentInChildren<WheelCollider>().suspensionSpring.spring;
                Require(FormulaProgression.State.Owned("suspension") == 1, "Suspension purchase failed.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == report.equippedSpring.ToString("0") + " N/m"), "Suspension statistic differs from applied wheel spring.");
                Pass("Suspension statistic follows the equipped physical spring value");
                Capture("Development"); Next(62, "level-selector");
            }
            else if (stage == 62) { Click("equip-engine"); Next(63, "level-selection-capture"); }
            else if (stage == 63) { Capture("UpgradeLevels"); Next(631, "level-selection-render"); }
            else if (stage == 631) { Click("level-engine-0"); Next(64, "stock-level"); }
            else if (stage == 64)
            {
                Require(FormulaProgression.State.Equipped("engine") == 0 && Mathf.Approximately(car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM, 480), "Explicit stock level did not apply.");
                Click("equip-engine"); Click("level-engine-1"); Next(65, "owned-level");
            }
            else if (stage == 65)
            {
                Require(FormulaProgression.State.Equipped("engine") == 1 && Mathf.Approximately(car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM, 494.4f), "Owned level did not reapply.");
                int expectedWallet = 20000 - FormulaCatalog.Current.Upgrade("engine").levels[0].incrementalPriceCredits - FormulaCatalog.Current.Upgrade("suspension").levels[0].incrementalPriceCredits;
                Require(FormulaProgression.Data.money == expectedWallet, "Free level switching charged credits.");
                Pass("Explicit owned-level selector applies stock/upgraded physics without charges"); Next(60, "profile-open");
            }
            else if (stage == 60) { menu.Show("Profil"); Next(7, "profile"); }
            else if (stage == 7) { Capture("Profile"); Next(70, "profile-render"); }
            else if (stage == 70) { menu.Show("Setări"); Next(8, "settings"); }
            else if (stage == 8)
            {
                Click("assist-abs"); Require(!FormulaProgression.State.abs, "ABS toggle failed.");
                Next(81, "settings-toggle-render");
            }
            else if (stage == 81) { Capture("Settings"); Next(80, "settings-render"); }
            else if (stage == 80)
            {
                Click("existing-settings"); Require(menu.View.Settings.DetailsOpen, "New audio/controls panel did not open."); Next(801, "audio-render");
            }
            else if (stage == 801)
            {
                Capture("Audio");
                var slider = canvas.GetComponentsInChildren<UnityEngine.UI.Slider>().Single(v => v.name == "volume-0"); slider.value = .42f;
                Require(Mathf.Approximately(FormulaProgression.Data.soundLevel, .42f), "Audio slider failed to update saved volume.");
                Next(8011, "audio-capture-render");
            }
            else if (stage == 8011) { Click("settings-controls"); Next(802, "controls-render"); }
            else if (stage == 802)
            {
                Capture("Controls"); Next(803, "rebind-cancel");
            }
            else if (stage == 803)
            {
                Click("rebind-0"); Require(menu.View.Settings.IsRebinding, "Input rebind did not start.");
                Click("rebind-cancel"); Require(!menu.View.Settings.IsRebinding, "Input rebind did not cancel.");
                Click("rebind-0"); simulatedKeyboard = InputSystem.AddDevice<Keyboard>("Formula binding validation");
                InputSystem.QueueStateEvent(simulatedKeyboard, new KeyboardState(Key.Y)); Next(804, "rebind-apply");
            }
            else if (stage == 804)
            {
                InputSystem.QueueStateEvent(simulatedKeyboard, new KeyboardState());
                Require(!menu.View.Settings.IsRebinding && RCCP_InputActions.Instance.inputActions.FindAction("Vehicle/Throttle").bindings.Any(b => b.overridePath == "<Keyboard>/y") && FormulaProgression.Data.inputRebindsJson.Contains("<Keyboard>/y"), "Interactive rebind did not commit through RCCP/save.");
                InputSystem.RemoveDevice(simulatedKeyboard); simulatedKeyboard = null;
                menu.Back(); Require(menu.CurrentPage == "Setări" && !menu.View.Settings.DetailsOpen && canvas.enabled, "New settings failed to return.");
                Pass("New audio and controls preserve volume, cancel and commit interactive bindings through the existing RCCP save");
                menu.Show("Curse rapide"); Next(9, "quick");
            }
            else if (stage == 9)
            {
                Capture("Quick");
                var canvasRect = (RectTransform)canvas.transform;
                foreach (var b in canvas.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    var corners = new Vector3[4]; ((RectTransform)b.transform).GetWorldCorners(corners);
                    Require(corners.Select(v => canvasRect.InverseTransformPoint(v)).All(v => v.x >= canvasRect.rect.xMin - .5f && v.x <= canvasRect.rect.xMax + .5f && v.y >= canvasRect.rect.yMin - .5f && v.y <= canvasRect.rect.yMax + .5f), "Button outside canvas: " + b.name);
                }
                var currentObjects = canvas.GetComponentsInChildren<Transform>(true);
                var currentIds = currentObjects.Select(t => t.GetInstanceID()).ToArray();
                var added = currentObjects.Where(t => !authoredUiObjects.Contains(t.GetInstanceID())).ToArray();
                File.WriteAllText("Docs/AI/FormulaUI/RuntimeHierarchyDelta.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { removedCount = authoredUiObjects.Count(id => !currentIds.Contains(id)), added = added.Select(t => new { t.name, parent = t.parent.name, components = t.GetComponents<Component>().Select(c => c.GetType().Name).ToArray() }).ToArray() }, Newtonsoft.Json.Formatting.Indented));
                Require(authoredUiObjects.All(id => currentIds.Contains(id)) && added.All(t => t.GetComponent<TMP_SubMeshUI>() != null), "Navigation changed authored UI objects; inspect RuntimeHierarchyDelta.json.");
                Pass("Navigation, purchase and dialogs preserve every authored UI object; visible quick buttons fit the viewport");
                Next(900, "quick-briefing");
            }
            else if (stage == 900)
            {
                Click("quick-0"); Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("No career credits or XP")), "Quick race briefing promises career rewards.");
                Capture("QuickBriefing"); Next(901, "quick-briefing-back");
            }
            else if (stage == 901) { menu.Back(); Pass("Quick race briefing clearly states that no career credits or XP are awarded"); Next(90, "virtual-controller"); }
            else if (stage == 90)
            {
                menu.Show("Acasă"); Click("home-garage"); Click("garage-0");
                simulatedPad = InputSystem.AddDevice<Gamepad>("Formula UX validation");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.RightShoulder)); Next(91, "bumper-input");
            }
            else if (stage == 91)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(menu.CurrentGarageTab == "Setup", "Gamepad bumper did not switch garage section.");
                EventSystem.current.SetSelectedGameObject(Button("setup-balanced").gameObject);
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.South)); Next(92, "submit-input");
            }
            else if (stage == 92)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(FormulaProgression.State.setup == "balanced", "Gamepad submit did not activate the focused setup.");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.East)); Next(93, "back-input");
            }
            else if (stage == 93)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(menu.CurrentPage == "Acasă", "Gamepad cancel did not return to the hub.");
                Require(EventSystem.current.currentSelectedGameObject.name == "home-garage", "Returning from garage lost home focus.");
                InputSystem.RemoveDevice(simulatedPad); simulatedPad = null;
                Pass("Virtual gamepad bumper, submit, cancel and focus restoration work through runtime input");
                Click("home-garage"); Click("setup-speed"); Next(94, "controller-save");
            }
            else if (stage == 94) { FormulaSession.ReturnToMenu(); Next(10, "reload"); }
            else if (stage == 10 && age > 2)
            {
                Require(menu != null && menu.CurrentPage == "Acasă", "Menu reload failed.");
                Require(FormulaProgression.State.equippedLivery == "generic_blue" && FormulaProgression.State.setup == "speed" && FormulaProgression.State.Owned("engine") == 1, "Progress was lost on scene reload.");
                Pass("Scene reload preserves equipped livery, setup and purchased development");
                Next(11, "complete"); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error)
        {
            if (simulatedPad != null && simulatedPad.added) InputSystem.RemoveDevice(simulatedPad); simulatedPad = null;
            if (simulatedKeyboard != null && simulatedKeyboard.added) InputSystem.RemoveDevice(simulatedKeyboard); simulatedKeyboard = null;
            report.errors = report.errors.Concat(new[] { error.Message + "\n" + error.StackTrace }).ToArray(); Next(99, "failed"); EditorApplication.ExitPlaymode();
        }
    }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (report == null || !SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors = report.errors.Concat(new[] { message }).ToArray();
    }
}
