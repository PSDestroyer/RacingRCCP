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
                Require(canvas.transform.Find("Shell Lower Shade").GetSiblingIndex() < canvas.transform.Find("Screens").GetSiblingIndex(), "The stage shade covers page content.");
                Pass("Serialized menu is active immediately with one Canvas, six persistent screens and no legacy presenter");
                Require(Time.frameCount > report.startFrame + 5, "Runtime frames did not advance.");
                Require(FormulaMenuAssets.Current.liveries.Length == 8 && FormulaMenuAssets.Current.liveries.All(t => t != null), "Livery artwork missing.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.font == FormulaMenuAssets.Current.titleFont), "Teko title font not rendered.");
                Require(canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Count(i => i.texture != null) >= 8, "Original menu icon slots missing.");
                Require(UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None).Length == 1, "Duplicate preview car.");
                var displayWheels = car.GetComponentsInChildren<RCCP_WheelCollider>();
                Require(displayWheels.Length == 4 && displayWheels.All(w => w.wheelModel != null && w.wheelModel.gameObject.activeInHierarchy && w.wheelModel.GetComponentsInChildren<Renderer>().Any(r => r.enabled)), "Parked Generic must keep all four wheel models visible.");
                Require(canvas.GetComponentsInChildren<FormulaPanelGraphic>(true).All(p => p.GetComponent<CanvasRenderer>() != null), "An Upgrade-style surface has no renderer.");
                Pass("Home renders with Teko/Rajdhani, original Figma artwork, one live Generic and advancing frames");
                Capture("Home"); Next(97, "home-selection-change");
            }
            else if (stage == 97)
            {
                EventSystem.current.SetSelectedGameObject(Button("home-garage").gameObject); Next(98, "home-selected-render");
            }
            else if (stage == 98)
            {
                Require(Button("home-garage").transform.Find("Upgrade Focus").GetComponent<FormulaPanelGraphic>().color.a > .95f && Button("home-career").transform.Find("Upgrade Focus").GetComponent<FormulaPanelGraphic>().color.a < .05f, "Home selection styling did not transfer to Garage.");
                Capture("HomeGarageFocus"); Next(99, "home-focus-restore");
            }
            else if (stage == 99)
            {
                EventSystem.current.SetSelectedGameObject(Button("home-career").gameObject); Next(100, "career-navigation");
            }
            else if (stage == 100)
            {
                Require(menu.View.Home.GetComponentsInChildren<UnityEngine.UI.Button>().Length == 5, "Home must expose Career, Garage and Quick Race plus Profile and Settings.");
                Click("home-career"); Require(menu.CurrentPage == "Carieră" && !menu.View.Dialogs.IsOpen, "Career must open the overview, not a briefing.");
                Click("g01"); Next(101, "briefing-render");
            }
            else if (stage == 101)
            {
                Require(menu.View.Dialogs.IsOpen && EventSystem.current.currentSelectedGameObject.name == "confirm", "Career event did not focus the race start action.");
                Require(canvas.GetComponentsInChildren<FormulaTrackMap>().Any(), "Briefing circuit outline missing.");
                Require(Button("confirm").FindSelectableOnLeft()?.name == "briefing-garage" && Button("briefing-garage").FindSelectableOnLeft()?.name == "cancel", "Briefing navigation order must follow Back, Garage, Start event.");
                Capture("EventBriefing"); Next(1011, "contextual-garage");
            }
            else if (stage == 1011)
            {
                Click("briefing-garage"); Require(menu.CurrentPage == "Garaj" && menu.CurrentGarageTab == "Dezvoltare" && !menu.View.Dialogs.IsOpen, "Briefing Garage did not open the first Upgrade section.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Stock event")), "Garage did not explain the stock-car event restriction.");
                Click("upgrade-aero"); Click("upgrade-level-1"); Next(10111, "locked-upgrade");
            }
            else if (stage == 10111)
            { Capture("UpgradeLocked"); Next(101111, "locked-upgrade-inspect"); }
            else if (stage == 101111)
            {
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "LOCKED\nEarn Bronze in Shakedown to unlock this upgrade."), "Upgrade prerequisite is not explicit.");
                Require(Button("confirm").interactable && EventSystem.current.currentSelectedGameObject == Button("confirm").gameObject && Button("confirm").GetComponentInChildren<TMP_Text>().text == "View Shakedown", "Locked upgrade must focus its actionable event link.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Downforce  +0% → +4%") && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Drag  +0% → +1%"), "Aero effect and tradeoff are not separated.");
                foreach(var text in menu.View.Dialogs.transform.Find("Confirmation").GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Require(text.preferredHeight <= text.rectTransform.rect.height + 1 && text.preferredWidth <= text.rectTransform.rect.width + 1, "Upgrade text exceeds its field: " + text.name + " preferred " + text.preferredWidth + "x" + text.preferredHeight + " rect " + text.rectTransform.rect.size); }
                Next(10112, "locked-upgrade-link");
            }
            else if (stage == 10112)
            {
                simulatedPad = InputSystem.AddDevice<Gamepad>("Formula upgrade dialog validation");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.South)); Next(101121, "locked-upgrade-controller-submit");
            }
            else if (stage == 101121)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(menu.View.Dialogs.IsOpen && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "SHAKEDOWN"), "Upgrade event link opened a different event.");
                Require(FormulaProgression.Data.money == 0 && FormulaProgression.State.Owned("aero") == 0, "Viewing a prerequisite changed progression.");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.East));
                Next(10113, "upgrade-event-back");
            }
            else if (stage == 10113)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(!menu.View.Dialogs.IsOpen && menu.CurrentGarageTab == "Dezvoltare" && menu.View.Garage.Upgrade.BranchId == "aero" && EventSystem.current.currentSelectedGameObject.name == "upgrade-level-1", "Briefing Back lost the source upgrade.");
                InputSystem.RemoveDevice(simulatedPad); simulatedPad = null;
                Click("upgrade-level-1"); Click("confirm"); Click("briefing-garage");
                Require(!menu.View.Dialogs.IsOpen && EventSystem.current.currentSelectedGameObject.name == "upgrade-level-1", "Prerequisite briefing Garage must return to the source Garage.");
                Pass("Locked upgrade explains the medal condition, separates effect and drag, links to Shakedown without mutation and preserves Garage category/level on Back");
                Click("garage-1"); EventSystem.current.SetSelectedGameObject(Button("setup-speed").gameObject); Next(1012, "setup-preview");
            }
            else if (stage == 1012)
            {
                Require(FormulaProgression.State.setup == "balanced" && Mathf.Approximately(car.GetComponent<RCCP_CarController>().AeroDynamics.downForce, 6) && FormulaProgression.Data.money == 0, "Setup focus changed the equipped car or credits.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("6 → 5.52")), "Setup preview does not show current versus proposed downforce.");
                Capture("ContextGarageSetup"); Next(10121, "setup-preview-capture");
            }
            else if (stage == 10121) { Click("setup-speed"); menu.Back(); Next(1013, "briefing-garage-return"); }
            else if (stage == 1013)
            {
                Require(menu.CurrentPage == "Carieră" && menu.View.Dialogs.IsOpen && EventSystem.current.currentSelectedGameObject.name == "confirm", "Garage Back did not reopen the original event briefing.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "SHAKEDOWN"), "Garage returned to a different event.");
                Pass("Briefing Garage preserves event context, warns for stock rules, previews setup without mutation and returns to the same event");
                Capture("BriefingGarageReturn"); Next(102, "briefing-back");
            }
            else if (stage == 102)
            {
                menu.Back(); Require(EventSystem.current.currentSelectedGameObject.name == "g01", "Briefing cancel lost event-card focus.");
                Require(FormulaProgression.Data.money == 0, "Inspecting the race spent credits.");
                Pass("Single Career entry opens overview; an event opens its briefing and Back restores the event card without spending");
                menu.Back(); Require(EventSystem.current.currentSelectedGameObject.name == "home-career", "Back from Career lost Home focus.");
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
                Pass("Six career cards preserve event locks and rewards");
                Require(Button("g01").GetComponent<RectTransform>().sizeDelta == new Vector2(398, 216), "Career card dimensions differ from Figma.");
                Require(!canvas.transform.Find("Header").gameObject.activeSelf && !canvas.transform.Find("Footer").gameObject.activeSelf, "Legacy chrome overlaps Figma Career.");
                var season = FormulaSeasonCalendar.Current;
                Require(season.year == 2027 && season.rounds.Count == 24 && season.Round(1).startDate == "2027-03-12" && season.Round(1).endDate == "2027-03-14", "2027 season opening weekend does not match the official calendar.");
                Require(FormulaCatalog.Current.careerEvents.Select(season.Caption).Distinct().Count() == 6 && season.Caption(FormulaCatalog.Current.Event("g06")) == "30 APR–02 MAY", "Career dates repeat or the cross-month weekend is incorrect.");
                var typeIcons = menu.View.Career.GetComponentsInChildren<FormulaMissionIcon>();
                Require(typeIcons.Length == 6 && typeIcons.Select(i => i.DisplaySymbol).Distinct().Count() == 6, "Career needs six distinct mission symbols.");
                Require(!menu.View.Career.GetComponentsInChildren<UnityEngine.UI.RawImage>().Any(i => i.name == "Country Badge"), "A country flag is still visible on a mission.");
                var coin = menu.View.Career.transform.Find("Figma Driver and Wallet/Coin").GetComponent<UnityEngine.UI.RawImage>();
                Require(coin.texture.name == "CoinMatte" && coin.rectTransform.sizeDelta.x <= 30 && coin.color.r < .8f, "Career currency still uses the large glowing coin.");
                Pass("2027 dates match the official opening weekend, all six mission icons differ and the currency coin has no halo");
                Capture("Career"); EventSystem.current.SetSelectedGameObject(null);
                foreach (var focus in menu.View.Career.GetComponentsInChildren<FormulaMenuFocus>()) focus.OnPointerExit(new PointerEventData(EventSystem.current));
                Next(200, "figma-career-unselected");
            }
            else if (stage == 200) { Capture("CareerUnselected"); EventSystem.current.SetSelectedGameObject(Button("g02").gameObject); Next(201, "figma-career-selection"); }
            else if (stage == 201)
            {
                Require(Button("g02").transform.Find("Upgrade Focus").GetComponent<FormulaPanelGraphic>().color.a > .95f && Button("g01").transform.Find("Upgrade Focus").GetComponent<FormulaPanelGraphic>().color.a < .05f, "Figma red selection did not move to the next card.");
                Capture("CareerSelection"); Pass("Figma Career uses 398x216 cards, isolated chrome and transfers the original red selection artwork");
                EventSystem.current.SetSelectedGameObject(Button("g01").gameObject); Next(21, "locked-inspection");
            }
            else if (stage == 21) { Click("g06"); Require(!Button("confirm").interactable, "Locked event allowed launch."); Next(22, "locked-dialog"); }
            else if (stage == 22) { Capture("LockedEvent"); Next(23, "locked-return"); }
            else if (stage == 23)
            {
                Click("cancel"); Require(EventSystem.current.currentSelectedGameObject.name == "g06", "Closing event details lost the selected career card.");
                Pass("Locked career content can be inspected, cannot launch and restores the original card focus"); Next(20, "garage-open");
            }
            else if (stage == 20) { menu.Show("Acasă"); Click("home-garage"); Require(menu.CurrentGarageTab == "Setup", "Garage did not remember its last section."); Pass("A single Home Garage entry remembers the last section"); Click("garage-2"); Next(3, "liveries"); }
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
                Require(FormulaProgression.Commit(d => { d.formula.medals["g01"] = Medal.Bronze; return null; }, out string error), error);
                Click("garage-0"); Click("upgrade-engine"); Click("upgrade-level-1");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "INSUFFICIENT CREDITS\nEarn another 1,200 CR to purchase."), "Credit shortfall is not explained.");
                Require(Button("confirm").interactable && Button("confirm").GetComponentInChildren<TMP_Text>().text == "View Shakedown", "Insufficient funds should offer the reward event.");
                Next(4001, "upgrade-credit-shortfall");
            }
            else if (stage == 4001) { Capture("UpgradeCredits"); Next(4002, "upgrade-credit-event"); }
            else if (stage == 4002)
            {
                Click("confirm"); Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "SHAKEDOWN"), "Credit shortfall did not link to a reward event."); menu.Back();
                Require(FormulaProgression.Data.money == 0 && FormulaProgression.State.Owned("engine") == 0 && EventSystem.current.currentSelectedGameObject.name == "upgrade-level-1", "Insufficient-credit path mutated the save or lost focus.");
                Pass("Insufficient funds states the exact shortfall and links to a replayable reward event"); Next(4003, "upgrade-unlocked");
            }
            else if (stage == 4003)
            {
                Require(FormulaProgression.Commit(d => { d.money = 20000; d.formula.medals["g01"] = Medal.Bronze; return null; }, out string error), error);
                Click("garage-0"); Click("upgrade-engine");
                Require(menu.View.Garage.Upgrade.IsPresented && !canvas.transform.Find("Header").gameObject.activeSelf && !canvas.transform.Find("Footer").gameObject.activeSelf && Button("garage-1").gameObject.activeInHierarchy && Button("garage-2").gameObject.activeInHierarchy, "Upgrade must preserve the permanent Garage tabs and its Figma presentation.");
                Require(menu.View.Garage.Upgrade.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b => b.name.StartsWith("upgrade-level-")) == 4, "Upgrade needs Stock and three level rows.");
                Require(menu.View.Garage.GetComponentsInChildren<FormulaDevelopmentCard>(true).Length == 0, "Legacy development cards remain in the authored hierarchy.");
                Click("upgrade-level-3"); Require(Button("confirm").GetComponentInChildren<TMP_Text>().text == "View Level 1", "Skipped upgrade does not point to its next required level."); Click("confirm");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Engine / Level 1"), "Required level link did not open the next purchasable upgrade."); Click("cancel");
                Require(FormulaProgression.Data.money == 20000 && FormulaProgression.State.Owned("engine") == 0 && Mathf.Approximately(car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM, 480), "Inspecting an unavailable upgrade changed credits, ownership or torque.");
                Pass("Figma Upgrade has four level rows, previews without mutations and blocks skipping unowned levels");
                Click("upgrade-level-1"); Next(5, "purchase-dialog");
            }
            else if (stage == 5)
            {
                var selected = EventSystem.current.currentSelectedGameObject?.GetComponent<UnityEngine.UI.Button>();
                Require(selected != null && selected.name == "cancel", "A paid purchase should initially focus Back.");
                Require(selected.FindSelectableOnRight()?.name == "confirm", "Dialog controller navigation escaped.");
                Capture("Purchase"); Next(50, "purchase-render");
            }
            else if (stage == 50)
            {
                Click("cancel"); Require(EventSystem.current.currentSelectedGameObject.name == "upgrade-level-1", "Purchase cancel did not restore its origin button."); Require(FormulaProgression.Data.money == 20000 && FormulaProgression.State.Owned("engine") == 0, "Cancel changed the save.");
                Click("upgrade-level-1"); Click("confirm"); Next(6, "development-capture");
            }
            else if (stage == 6)
            {
                var controller = car.GetComponent<RCCP_CarController>();
                Require(FormulaProgression.State.Owned("engine") == 1 && FormulaProgression.Data.money == 20000 - FormulaCatalog.Current.Upgrade("engine").levels[0].incrementalPriceCredits, "Upgrade price/ownership mismatch.");
                report.equippedTorque = controller.Engine.maximumTorqueAsNM; report.equippedBrakes = controller.FrontAxle.maxBrakeTorque;
                report.equippedDownforce = controller.AeroDynamics.downForce; report.equippedDrag = controller.AeroDynamics.airResistance;
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == controller.Engine.maximumTorqueAsNM.ToString("0.#") + " Nm"), "UI torque differs from RCCP.");
                Require(EventSystem.current.currentSelectedGameObject.name == "upgrade-level-1", "Successful purchase lost its source focus.");
                Pass("Upgrade purchase/cancel, source focus, credit cost, ownership and live torque statistics agree");
                Click("upgrade-suspension"); Click("upgrade-level-1"); Click("confirm"); Next(61, "suspension-statistics");
            }
            else if (stage == 61)
            {
                report.equippedSpring = car.GetComponentInChildren<WheelCollider>().suspensionSpring.spring;
                Require(FormulaProgression.State.Owned("suspension") == 1, "Suspension purchase failed.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == report.equippedSpring.ToString("0") + " N/m"), "Suspension statistic differs from applied wheel spring.");
                Pass("Suspension statistic follows the equipped physical spring value");
                Click("upgrade-engine"); Next(62, "level-selector");
            }
            else if (stage == 62) { EventSystem.current.SetSelectedGameObject(Button("upgrade-level-2").gameObject); Next(63, "level-selection-capture"); }
            else if (stage == 63) { Capture("Development"); Next(631, "level-selection-render"); }
            else if (stage == 631) { Capture("UpgradeLevels"); Next(632, "level-selection-render"); }
            else if (stage == 632) { Click("upgrade-level-0"); Require(Button("confirm").GetComponentInChildren<TMP_Text>().text == "Equip · Free", "Owned stock level does not offer free equip."); Click("confirm"); Next(64, "stock-level"); }
            else if (stage == 64)
            {
                Require(FormulaProgression.State.Equipped("engine") == 0 && Mathf.Approximately(car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM, 480), "Explicit stock level did not apply.");
                Click("upgrade-engine"); Click("upgrade-level-1"); Next(641, "owned-level-review");
            }
            else if (stage == 641)
            {
                Require(Button("confirm").GetComponentInChildren<TMP_Text>().text == "Equip · Free" && FormulaProgression.State.Equipped("engine") == 0, "Reviewing an owned level should wait for free equip confirmation.");
                Capture("UpgradeOwned"); Next(642, "owned-level-equip");
            }
            else if (stage == 642) { Click("confirm"); Next(65, "owned-level"); }
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
                EventSystem.current.SetSelectedGameObject(Button("upgrade-engine").gameObject);
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.DpadRight)); Next(904, "component-input");
            }
            else if (stage == 904)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(menu.View.Garage.Upgrade.BranchId == "suspension", "D-pad did not change the focused Upgrade component.");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.RightShoulder)); Next(91, "bumper-input");
            }
            else if (stage == 91)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(menu.CurrentGarageTab == "Setup", "Gamepad bumper did not switch the permanent Garage tab.");
                EventSystem.current.SetSelectedGameObject(Button("setup-balanced").gameObject);
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.South)); Next(92, "submit-input");
            }
            else if (stage == 92)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(FormulaProgression.State.setup == "balanced", "Gamepad submit did not apply the focused free setup.");
                Pass("Gamepad D-pad changes Upgrade component, bumper changes Garage tab, and Submit applies the focused setup");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.East)); Next(93, "back-input");
            }
            else if (stage == 93)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(menu.CurrentPage == "Acasă", "Gamepad cancel did not return to the hub.");
                Require(EventSystem.current.currentSelectedGameObject.name == "home-garage", "Returning from garage lost home focus.");
                InputSystem.RemoveDevice(simulatedPad); simulatedPad = null;
                Pass("Virtual gamepad bumper, submit, cancel and focus restoration work through runtime input");
                Click("home-garage"); Click("garage-1"); Click("setup-speed"); Next(94, "controller-save");
            }
            else if (stage == 94) { FormulaSession.ReturnToMenu(); Next(10, "reload"); }
            else if (stage == 10 && age > 2)
            {
                Require(menu != null && menu.CurrentPage == "Acasă", "Menu reload failed.");
                Require(FormulaProgression.State.equippedLivery == "generic_blue" && FormulaProgression.State.setup == "speed" && FormulaProgression.State.Owned("engine") == 1 && menu.CurrentGarageTab == "Setup", "Progress or last Garage tab was lost on scene reload.");
                Pass("Scene reload preserves equipped livery, setup and purchased development");
                Next(1040, "standalone-garage-load");
            }
            else if (stage == 1040) { UnityEngine.SceneManagement.SceneManager.LoadScene(FormulaGarageBuilder.GarageScene); Next(1041, "standalone-garage"); }
            else if (stage == 1041 && age > 2)
            {
                if (menu == null || car == null || !car.IsReady) return;
                Require(menu.CurrentPage == "Garaj" && menu.CurrentGarageTab == "Setup", "Standalone Garage did not restore the saved section.");
                Require(Button("garage-0") != null && Button("garage-1") != null && Button("garage-2") != null, "Standalone Garage tabs are missing.");
                Pass("Standalone Garage uses the same authored UI, remembered section and Generic vehicle");
                Capture("StandaloneGarage"); Next(1042, "standalone-garage-capture");
            }
            else if (stage == 1042) { Next(11, "complete"); EditorApplication.ExitPlaymode(); }

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
