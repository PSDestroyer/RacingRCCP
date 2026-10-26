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
    [MenuItem("Tools/Formula Career/Preview and Validate Menu at 1080p")]
    public static void Run1080()
    {
        FormulaGameViewValidation.Select1080(); Run();
    }
    [MenuItem("Tools/Formula Career/Preview and Validate Figma Menu")]
    public static void Run() => BeginRun(false);
    [MenuItem("Tools/Formula Career/Preview Home Reference at 1080p")]
    public static void RunHomeReference() { FormulaGameViewValidation.Select1080(); BeginRun(true); }
    private static void BeginRun(bool homeOnly)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (FormulaMenuAssets.Current == null) throw new InvalidOperationException("Build Figma Menu Assets first.");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaMenu-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active, true); SessionState.SetInt(Stage, 0);
        SessionState.SetBool("FormulaMenu.Check.HomeOnly",homeOnly);
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
            FormulaGameViewValidation.Restore();
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
        Directory.CreateDirectory("Docs/AI/FormulaUI"); File.WriteAllText("Docs/AI/FormulaUI/"+(SessionState.GetBool("FormulaMenu.Check.HomeOnly",false)?"HomeReferenceValidation.json":"RuntimeValidation.json"), json);
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
                // Asset import can consume wall time before the first player frames.
                if (Time.frameCount <= report.startFrame + 5) return;
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleAsync;
                if (!locale.IsDone) return;
                Require(locale.Result != null && locale.Result.Identifier.Code == "en", "English was not selected as the startup locale.");
                Require(menu != null && menu.CurrentPage == "Acasă" && car.IsReady, "Home/Generic failed to initialize.");
                Require(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Legacy or duplicate Canvas remains in the scene.");
                Require(menu.legacyCanvas == null && menu.legacyNavigation == null, "Menu still depends on the legacy UI.");
                Require(menu.View.Screens.childCount == 6 && menu.View.Screens.Cast<Transform>().Count(t => t.gameObject.activeSelf) == 1, "Exactly one of the six authored screens must be active.");
                ValidateResultModes(menu);
                FormulaTextFitValidation.Remember(menu.View);
                authoredUiObjects = canvas.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).OrderBy(id => id).ToArray();
                Require(canvas.transform.Find("Shell Lower Shade").GetSiblingIndex() < canvas.transform.Find("Screens").GetSiblingIndex(), "The stage shade covers page content.");
                Pass("Serialized menu is active immediately with one Canvas, six persistent screens and no legacy presenter");
                Require(Time.frameCount > report.startFrame + 5, "Runtime frames did not advance.");
                Require(FormulaMenuAssets.Current.liveries.Length == 8 && FormulaMenuAssets.Current.liveries.All(t => t != null), "Livery artwork missing.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.font == FormulaMenuAssets.Current.titleFont), "Teko title font not rendered.");
                Require(menu.View.Home.GetComponentsInChildren<FormulaMissionIcon>().Count(i => i.name == "Navigation Icon") == 5, "Home must have five crisp navigation icons.");
                Require(UnityEngine.Object.FindObjectsByType<RCCP_CarController>(FindObjectsSortMode.None).Length == 1, "Duplicate preview car.");
                var displayWheels = car.GetComponentsInChildren<RCCP_WheelCollider>();
                Require(displayWheels.Length == 4 && displayWheels.All(w => w.wheelModel != null && w.wheelModel.gameObject.activeInHierarchy && w.wheelModel.GetComponentsInChildren<Renderer>().Any(r => r.enabled)), "Parked Generic must keep all four wheel models visible.");
                ValidateHomeFraming(car);
                ValidateSurfaceResponse(car);
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
                Require(Button("home-garage").transform.Find("Selection Arrow").gameObject.activeInHierarchy && Button("home-garage").transform.Find("Selection Arrow").GetComponent<FormulaMissionIcon>().color.a > .95f, "The focused Home action has no visible arrow.");
                Capture("HomeGarageFocus"); Next(99, "home-focus-restore");
            }
            else if (stage == 99)
            {
                EventSystem.current.SetSelectedGameObject(Button("home-career").gameObject);
                if(SessionState.GetBool("FormulaMenu.Check.HomeOnly",false))
                {
                    simulatedPad=InputSystem.AddDevice<Gamepad>("Formula Home validation");
                    InputSystem.QueueStateEvent(simulatedPad,new GamepadState().WithButton(GamepadButton.DpadDown)); Next(990,"home-controller-garage");
                }
                else Next(100, "career-navigation");
            }
            else if(stage>=990 && stage<=993)
            {
                InputSystem.QueueStateEvent(simulatedPad,new GamepadState());
                string[] expected={"home-garage","home-quick","home-profile","home-settings"};
                Require(EventSystem.current.currentSelectedGameObject.name==expected[stage-990],"Home D-pad navigation did not follow the visible action order.");
                InputSystem.QueueStateEvent(simulatedPad,new GamepadState().WithButton(stage==992?GamepadButton.DpadRight:stage==993?GamepadButton.South:GamepadButton.DpadDown));
                Next(stage+1,"home-controller-"+(stage+1));
            }
            else if(stage==994)
            {
                InputSystem.QueueStateEvent(simulatedPad,new GamepadState()); Require(menu.CurrentPage=="Setări","Home controller Submit did not open Settings.");
                InputSystem.QueueStateEvent(simulatedPad,new GamepadState().WithButton(GamepadButton.East)); Next(995,"home-controller-return");
            }
            else if(stage==995)
            {
                InputSystem.QueueStateEvent(simulatedPad,new GamepadState());
                Require(menu.CurrentPage=="Acasă" && EventSystem.current.currentSelectedGameObject.name=="home-settings","Home did not restore the action that opened Settings.");
                InputSystem.RemoveDevice(simulatedPad); simulatedPad=null;
                Pass("Virtual controller traverses Career, Garage, Quick Race, Profile and Settings; Submit/Back restore source focus");
                FormulaProgression.Commit(d=>{ d.formula.textScale=115; d.formula.safeAreaPercent=95; return null; },out _);
                Next(996,"home-larger-text");
            }
            else if(stage==996)
            {
                Require(menu.View.Home.GetComponentsInChildren<UnityEngine.UI.Button>().Length==5,"Home gained extra actions.");
                foreach(var text in menu.View.Home.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate(); Require(!text.enableAutoSizing && text.preferredWidth<=text.rectTransform.rect.width+1,"Home label does not retain readable 115 percent text.");
                }
                var block=new MaterialPropertyBlock(); car.body.GetPropertyBlock(block,0);
                Require(block.isEmpty && car.body.sharedMaterial.GetFloat("_Smoothness")>.5f && car.body.sharedMaterial.shader.name=="Universal Render Pipeline/Complex Lit","Dry presentation does not use its native PBR finish.");
                Pass("Home retains five readable actions at 115 percent text and 95 percent safe area; dry presentation uses the authored PBR paint finish");
                Capture("HomeLargeText"); Next(997,"home-final-capture");
            }
            else if(stage==997) { Next(998,"complete"); EditorApplication.ExitPlaymode(); }
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
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "MISSING 1,100 CR" && t.color == FormulaUI.Red), "Missing credits must remain highlighted even when the medal is also missing.");
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
                Require(FormulaCatalog.Current.careerEvents.Take(6).All(e => Button(e.id) != null), "Opening career card missing.");
                Require(Button("g06").interactable, "Locked events must remain inspectable.");
                Pass("Six career cards preserve event locks and rewards");
                Require(Button("g01").GetComponent<RectTransform>().sizeDelta == new Vector2(398, 216), "Career card dimensions differ from Figma.");
                Require(!canvas.transform.Find("Header").gameObject.activeSelf && !canvas.transform.Find("Footer").gameObject.activeSelf, "Legacy chrome overlaps Figma Career.");
                var season = FormulaSeasonCalendar.Current;
                Require(season.year == 2027 && season.rounds.Count == 24 && season.Round(1).startDate == "2027-03-12" && season.Round(1).endDate == "2027-03-14", "2027 season opening weekend does not match the official calendar.");
                Require(FormulaCatalog.Current.careerEvents.Select(season.Caption).Distinct().Count() == FormulaCatalog.Current.careerEvents.Count && season.Caption(FormulaCatalog.Current.Event("g06")) == "30 APR–02 MAY", "Career dates repeat or the cross-month weekend is incorrect.");
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
                Pass("Eight livery previews never mutate ownership; explicit free equip preserves wallet and development"); Next(300, "livery-locked-review");
            }
            else if (stage == 300)
            {
                Click("generic_red"); Require(Button("livery-apply").interactable && Button("generic_red").FindSelectableOnUp()?.name == "livery-apply", "Locked livery cannot be inspected or reached with controller navigation.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Balance: 0 CR · Price: 1,500 CR") && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "MISSING 1,500 CR" && t.color == FormulaUI.Red), "Locked purchasable livery hides its price or credit shortfall.");
                Click("livery-apply"); Next(301, "livery-locked-dialog");
            }
            else if (stage == 301)
            {
                Require(menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("NEEDED · Bronze in " + FormulaCatalog.Current.Event("g03").name)), "Livery dialog does not explain the missing medal.");
                Require(Button("confirm").GetComponentInChildren<TMP_Text>().text == "View " + FormulaCatalog.Current.Event("g03").name && EventSystem.current.currentSelectedGameObject.name == "confirm", "Locked livery has no actionable event link.");
                Capture("LiveryLocked"); Next(302, "livery-controller-submit");
            }
            else if (stage == 302)
            {
                simulatedPad = InputSystem.AddDevice<Gamepad>("Formula livery validation");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.South)); Next(303, "livery-prerequisite-event");
            }
            else if (stage == 303)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState());
                Require(menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text == FormulaCatalog.Current.Event("g03").name.ToUpperInvariant()), "Livery event link opened the wrong prerequisite.");
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState().WithButton(GamepadButton.East)); Next(304, "livery-controller-back");
            }
            else if (stage == 304)
            {
                InputSystem.QueueStateEvent(simulatedPad, new GamepadState()); InputSystem.RemoveDevice(simulatedPad); simulatedPad = null;
                Require(!menu.View.Dialogs.IsOpen && menu.CurrentGarageTab == "Livree" && EventSystem.current.currentSelectedGameObject.name == "livery-apply" && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == FormulaCatalog.Current.Skin("generic_red").displayName), "Back from the prerequisite lost livery preview or focus.");
                Require(FormulaProgression.Data.money == 0 && FormulaProgression.State.equippedLivery == "generic_blue" && !FormulaProgression.State.ownedLiveries.Contains("generic_red"), "Locked livery inspection changed the save.");
                Pass("Locked paid livery shows both medal and credit blockers; virtual gamepad opens its event and returns to the same preview without mutation");
                Require(FormulaProgression.Commit(d => { d.formula.medals["g03"] = Medal.Bronze; d.money = 400; return null; }, out string error), error);
                Click("generic_red"); Click("livery-apply"); Next(305, "livery-partial-balance");
            }
            else if (stage == 305)
            {
                Require(menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "MISSING 1,100 CR" && t.color == FormulaUI.Red) && Button("confirm").GetComponentInChildren<TMP_Text>().text == "Earn credits · " + FormulaCatalog.Current.Event("g03").name, "Livery partial balance or reward-event action is incorrect.");
                Capture("LiveryCredits"); Next(306, "livery-purchase-review");
            }
            else if (stage == 306)
            {
                Click("cancel"); Require(FormulaProgression.Data.money == 400 && !FormulaProgression.State.ownedLiveries.Contains("generic_red"), "Insufficient-credit livery review charged the player.");
                Require(FormulaProgression.Commit(d => { d.money = 1500; return null; }, out string error), error);
                Click("generic_red"); Click("livery-apply"); Next(307, "livery-exact-balance");
            }
            else if (stage == 307)
            {
                Require(EventSystem.current.currentSelectedGameObject.name == "cancel" && Button("confirm").GetComponentInChildren<TMP_Text>().text == "Buy and equip · 1,500 CR", "Affordable livery must show the exact price and initially focus Back.");
                Require(!menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text.StartsWith("MISSING")), "Livery shortfall did not clear at exact funds.");
                Capture("LiveryPurchase"); Next(308, "livery-purchase-confirm");
            }
            else if (stage == 308)
            {
                Click("cancel"); Require(FormulaProgression.Data.money == 1500 && !FormulaProgression.State.ownedLiveries.Contains("generic_red"), "Cancel purchased the livery.");
                Click("livery-apply"); Click("confirm"); Next(309, "livery-purchase-result");
            }
            else if (stage == 309)
            {
                Require(FormulaProgression.Data.money == 0 && FormulaProgression.State.ownedLiveries.Contains("generic_red") && FormulaProgression.State.equippedLivery == "generic_red" && Mathf.Approximately(car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM, 480), "Paid livery price, ownership, equip or unchanged performance is incorrect.");
                Require(EventSystem.current.currentSelectedGameObject.name == "generic_red", "Livery purchase should focus the equipped card when its action becomes disabled.");
                Click("generic_blue"); Click("livery-apply"); Require(FormulaProgression.Data.money == 0 && FormulaProgression.State.equippedLivery == "generic_blue", "Owned livery equip should remain free.");
                Pass("Paid liveries handle partial/exact funds, cancel and purchase atomically; owned equip is free and physics are unchanged");
                Require(FormulaProgression.Commit(d => { d.formula.medals["g01"] = Medal.Gold; return null; }, out string error), error);
                Click("generic_purple"); Click("livery-apply"); Next(310, "livery-six-medals");
            }
            else if (stage == 310)
            {
                var requirement=menu.View.Dialogs.transform.Find("Confirmation/Livery Details/Requirements").GetComponent<TMP_Text>();
                Require(requirement.GetParsedText().Split('\n').Length == 6 && requirement.text.Contains("MET · Gold in Shakedown") && requirement.text.Contains("NEEDED · Gold in First Flying Lap"), "Nova Purple does not show each met/missing Gold medal.");
                Require(Button("confirm").GetComponentInChildren<TMP_Text>().text == "View First Flying Lap" && !menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text.StartsWith("MISSING") || t.text.StartsWith("Balance:")), "Free achievement reward was presented as a credit purchase.");
                foreach(var text in menu.View.Dialogs.transform.Find("Confirmation").GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Require(text.preferredHeight <= text.rectTransform.rect.height + 1 && text.preferredWidth <= text.rectTransform.rect.width + 1, "Livery text exceeds its field: " + text.name); }
                Capture("LiveryReward"); Next(311, "livery-reward-event");
            }
            else if (stage == 311)
            {
                Click("confirm"); Require(menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "FIRST FLYING LAP"), "Multi-medal reward did not link to its next missing objective."); menu.Back();
                Require(FormulaProgression.Commit(d => { foreach(var entry in FormulaCatalog.Current.careerEvents) d.formula.medals[entry.id]=Medal.Gold; return null; }, out string error), error);
                Click("generic_purple"); Click("livery-apply"); Require(Button("confirm").GetComponentInChildren<TMP_Text>().text == "Claim and equip · Free", "Completed achievement should be claimable for free."); Click("confirm");
                Require(FormulaProgression.State.ownedLiveries.Contains("generic_purple") && FormulaProgression.State.equippedLivery == "generic_purple" && FormulaProgression.Data.money == 0, "Claiming a completed achievement charged credits or failed to grant ownership.");
                Click("generic_blue"); Click("livery-apply"); Require(FormulaProgression.Commit(d => { d.formula.medals.Clear(); return null; }, out error), error);
                Pass("Career reward shows six individual medal requirements, links to the next missing objective and claims completed rewards for free"); Next(312, "all-livery-states");
            }
            else if (stage == 312)
            {
                float torqueBefore = car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM;
                foreach(var livery in FormulaCatalog.Current.liveries)
                {
                    Click(livery.id);
                    int artIndex = FormulaCatalog.Current.liveries.IndexOf(livery);
                    Require(Button(livery.id).transform.Find("Livery Name").GetComponent<TMP_Text>().text == livery.displayName, "Livery card does not show the catalog name: " + livery.id);
                    Require(car.body.sharedMaterials.Length == 2 && car.body.sharedMaterial == FormulaAssets.Current.liveryMaterials[artIndex], "The selected livery artwork or carbon floor did not reach the live car: " + livery.id);
                    Require(car.body.GetComponent<MeshFilter>().sharedMesh.subMeshCount == 2, "Live Generic lost its separate carbon floor.");
                    Require(Mathf.Approximately(car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM, torqueBefore), "Livery preview changed performance.");
                    foreach(var text in menu.View.Garage.transform.Find("Livree/Livery Details").GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Require(text.preferredHeight <= text.rectTransform.rect.height + 1 && text.preferredWidth <= text.rectTransform.rect.width + 1, "Livery inspector text exceeds its field: " + text.name + " / " + livery.id); }
                }
                Require(FormulaProgression.State.equippedLivery == "generic_blue" && FormulaProgression.Data.money == 0, "Cycling all livery previews changed progression.");
                Click("generic_blue"); Pass("All eight original liveries reach the live car, retain the carbon floor and performance, fit their fields and preview without equipping"); Next(30, "livery-render");
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
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "MISSING 1,200 CR" && t.color == FormulaUI.Red) && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Balance: 0 CR · Upgrade cost: 1,200 CR"), "Credit shortfall, balance and price must be explicit.");
                Require(Button("confirm").interactable && Button("confirm").GetComponentInChildren<TMP_Text>().text == "Earn credits · Shakedown", "Insufficient funds should offer the reward event.");
                Next(4001, "upgrade-credit-shortfall");
            }
            else if (stage == 4001) { Capture("UpgradeCredits"); Next(4002, "upgrade-credit-event"); }
            else if (stage == 4002)
            {
                Click("confirm"); Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "SHAKEDOWN"), "Credit shortfall did not link to a reward event."); menu.Back();
                Require(FormulaProgression.Data.money == 0 && FormulaProgression.State.Owned("engine") == 0 && EventSystem.current.currentSelectedGameObject.name == "upgrade-level-1", "Insufficient-credit path mutated the save or lost focus.");
                Pass("Insufficient funds states the exact shortfall and links to a replayable reward event");
                Require(FormulaProgression.Commit(d => { d.money = 400; return null; }, out string error), error);
                Click("upgrade-level-1"); Next(40021, "partial-credit-shortfall");
            }
            else if (stage == 40021)
            {
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "MISSING 800 CR" && t.color == FormulaUI.Red) && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Balance: 400 CR · Upgrade cost: 1,200 CR"), "Partial balance does not show the exact missing amount.");
                Require(Button("upgrade-level-1").transform.Find("Ownership and Price").GetComponent<TMP_Text>().color == FormulaUI.Red, "Unaffordable level price is not highlighted.");
                Capture("UpgradeCreditsPartial"); Next(40022, "exact-credit-balance");
            }
            else if (stage == 40022)
            {
                Click("cancel"); Require(FormulaProgression.Commit(d => { d.money = 1200; return null; }, out string error), error); Click("upgrade-level-1");
                Require(Button("confirm").GetComponentInChildren<TMP_Text>().text == "Buy and equip · 1,200 CR" && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Upgrade cost · 1,200 CR" && t.color == Color.white), "Exact funds did not clear the shortfall state.");
                Require(FormulaProgression.State.Owned("engine") == 0 && FormulaProgression.Data.money == 1200, "Reviewing an affordable upgrade changed the save.");
                Click("cancel"); Pass("Missing credits are red for zero/partial balances, both medal and credit blockers remain visible, and exact funds restore the purchase action without spending"); Next(4003, "upgrade-unlocked");
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
                SessionState.SetString("FormulaMenu.Check.BeforeConflict", RCCP_InputActions.Instance.inputActions.SaveBindingOverridesAsJson());
                InputSystem.QueueStateEvent(simulatedKeyboard, new KeyboardState(Key.S)); Next(8031, "rebind-conflict");
            }
            else if (stage == 8031)
            {
                InputSystem.QueueStateEvent(simulatedKeyboard, new KeyboardState());
                Require(menu.View.Settings.IsRebinding && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("INPUT ALREADY IN USE") && t.text.Contains("Brake")), "A duplicate driving input was not explained.");
                Require(RCCP_InputActions.Instance.inputActions.SaveBindingOverridesAsJson() == SessionState.GetString("FormulaMenu.Check.BeforeConflict", ""), "Rejected input changed a binding.");
                Require(EventSystem.current.currentSelectedGameObject.name == "rebind-cancel", "Conflict notice lost controller focus.");
                Capture("BindingConflict"); Next(8032, "rebind-conflict-dismiss");
            }
            else if (stage == 8032)
            {
                Click("rebind-cancel"); Require(!menu.View.Settings.IsRebinding && EventSystem.current.currentSelectedGameObject.name == "rebind-0", "Dismissing a conflict did not restore the binding row.");
                Pass("Conflicting input is rejected without mutation and explains the occupied action; dismiss restores source focus");
                Click("rebind-0");
                InputSystem.QueueStateEvent(simulatedKeyboard, new KeyboardState(Key.Y)); Next(804, "rebind-apply");
            }
            else if (stage == 804)
            {
                InputSystem.QueueStateEvent(simulatedKeyboard, new KeyboardState());
                Require(!menu.View.Settings.IsRebinding && RCCP_InputActions.Instance.inputActions.FindAction("Vehicle/Throttle").bindings.Any(b => b.overridePath == "<Keyboard>/y") && FormulaProgression.Data.inputRebindsJson.Contains("<Keyboard>/y"), "Interactive rebind did not commit through RCCP/save.");
                InputSystem.RemoveDevice(simulatedKeyboard); simulatedKeyboard = null;
                menu.Back(); Require(menu.CurrentPage == "Setări" && !menu.View.Settings.DetailsOpen && canvas.enabled, "New settings failed to return.");
                Pass("New audio and controls preserve volume, cancel and commit interactive bindings through the existing RCCP save");
                Click("open-ACCESSIBILITY"); Click("preference-5"); Click("preference-6"); Click("preference-7");
                Require(FormulaProgression.State.textScale == 115 && FormulaProgression.State.reducedMotion && FormulaProgression.State.highContrast, "Accessibility preferences did not apply.");
                Next(805, "accessibility-preview");
            }
            else if (stage == 805)
            {
                Require(canvas.transform.Find("Screen Shade").gameObject.activeInHierarchy && canvas.transform.Find("Screen Shade").GetComponent<UnityEngine.UI.Image>().color.a >= .79f, "High contrast does not darken the background.");
                Capture("Accessibility"); Next(8051, "accessibility-capture");
            }
            else if (stage == 8051)
            {
                Click("tab-DISPLAY"); Click("preference-1");
                Require(FormulaProgression.State.safeAreaPercent == 95, "Safe area preference did not apply.");
                Next(806, "display-preview");
            }
            else if (stage == 806)
            {
                Capture("Display"); Next(8061, "display-capture");
            }
            else if (stage == 8061)
            {
                Click("tab-GRAPHICS");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text.StartsWith("QUALITY  /")), "Graphics page has no actual quality setting.");
                Next(807, "graphics-preview");
            }
            else if (stage == 807)
            {
                Capture("Graphics"); Next(8071, "graphics-capture");
            }
            else if (stage == 8071)
            {
                FormulaProgression.Commit(d => { d.formula.bestLapSeconds["g01"] = 52.432; return null; }, out _);
                menu.Back(); menu.Show("Profil"); Next(808, "profile-records");
            }
            else if (stage == 808)
            {
                Require(canvas.GetComponentsInChildren<TMP_Text>().Count(t => t.text.StartsWith("BEST LAP")) == 6, "Profile does not expose six lap records.");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "BEST LAP  00:52.432"), "Profile lap record differs from the saved best lap.");
                Capture("Profile"); Next(8081, "profile-capture");
            }
            else if (stage == 8081)
            {
                menu.Show("Carieră"); Next(809, "readability-preview");
            }
            else if (stage == 809)
            {
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "COMPLETED") && canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "NEXT EVENT"), "Career progression states are missing.");
                FormulaTextFitValidation.Validate(menu);
                Pass("115 percent body text retains its requested size and fits Home, Career, briefing, Garage, Profile, Quick Race and Settings");
                Capture("CareerLargeText"); Next(8091, "readability-capture");
            }
            else if (stage == 8091)
            {
                FormulaProgression.Commit(d => { d.formula.textScale = 100; d.formula.safeAreaPercent = 100; d.formula.reducedMotion = false; d.formula.highContrast = false; return null; }, out _);
                Pass("Display safe area, larger text, reduced motion, contrast, graphics page, XP and six profile records are available without altering the wallet");
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
                Require(canvas.GetComponentsInChildren<FormulaEventCard>().Length == (FormulaAssets.Current.tracks.Length > 2 ? 6 : 2), "Quick selection must show one card per circuit on its current page.");
                Click("mode-1"); Click("quick-0");
                Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "TIME ATTACK"), "Selected quick mode was lost in briefing."); Require(canvas.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("No career credits or XP")), "Quick race briefing promises career rewards.");
                Capture("QuickBriefing"); Next(901, "quick-briefing-back");
            }
            else if (stage == 901) { menu.Back(); Pass("Quick sessions separate mode and circuit, preserve the selected Time Attack mode and clearly state that no career credits or XP are awarded"); Next(90, "virtual-controller"); }
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
                Require(FormulaProgression.State.equippedLivery == "generic_blue" && FormulaProgression.State.ownedLiveries.Contains("generic_red") && FormulaProgression.State.ownedLiveries.Contains("generic_purple") && FormulaProgression.State.setup == "speed" && FormulaProgression.State.Owned("engine") == 1 && menu.CurrentGarageTab == "Setup", "Progress or last Garage tab was lost on scene reload.");
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
    private static void ValidateResultModes(FormulaMainMenu menu)
    {
        var state = FormulaProgression.State; string previous = state.activeMode;
        string[] modes = { "quick", "time_trial", "quick" }, events = { "g03", "g02", "g01" }, names = { "Quick Race", "Time Attack", "Practice" };
        try
        {
            for (int i = 0; i < modes.Length; i++)
            {
                state.activeMode = modes[i];
                var root = UnityEngine.Object.Instantiate(FormulaMenuAssets.Current.resultsPrefab, menu.View.Canvas.transform, false);
                try
                {
                    root.GetComponent<FormulaResultsView>().Present(new FormulaReceipt { eventId = events[i], bestLapSeconds = 90, finishPosition = 2, personalBest = i == 1 });
                    Require(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text == names[i] + " · No career credits or XP"), "Results mislabels the " + names[i] + " mode.");
                    string status = i == 0 ? "P2" : i == 1 ? "PERSONAL BEST" : "SESSION COMPLETE";
                    Require(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text == status), "Free-session result uses an uncalibrated medal target.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Pass("Results identifies Quick Race, Time Attack and Practice without promising career rewards");
        }
        finally { state.activeMode = previous; }
    }
    private static void ValidateSurfaceResponse(FormulaVehicle car)
    {
        var surface = car.GetComponent<FormulaVehicleSurface>();
        Require(surface != null && surface.SurfaceCount == 11, "Generic has no complete surface bindings.");
        float originalWetness = surface.Wetness;
        var originalPaint = car.body.sharedMaterial;
        float drySmoothness = originalPaint.GetFloat("_Smoothness");
        var dryColor = originalPaint.GetColor("_BaseColor");
        float torque = car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM;
        var tyre = car.GetComponentsInChildren<MeshRenderer>().First(r => r.name.StartsWith("Generic_Tyre", StringComparison.Ordinal));
        var rim = car.GetComponentsInChildren<MeshRenderer>().First(r => r.name.StartsWith("Generic_Rim", StringComparison.Ordinal));
        var block = new MaterialPropertyBlock();
        try
        {
            surface.SetWetness(1);
            car.body.GetPropertyBlock(block, 0); Require(block.GetFloat("_Smoothness") >= .89f && block.GetFloat("_ClearCoatMask") > .99f, "Wet paint did not gain a water-film sheen.");
            car.body.GetPropertyBlock(block, 1); Require(Mathf.Approximately(block.GetFloat("_Smoothness"), .78f) && block.GetFloat("_Metallic") == 0, "Wet carbon is not independent from paint.");
            tyre.GetPropertyBlock(block, 0); Require(Mathf.Approximately(block.GetFloat("_Smoothness"), .72f) && block.GetFloat("_Metallic") == 0, "Wet tyre response changed rubber into metal.");
            rim.GetPropertyBlock(block, 0); Require(Mathf.Approximately(block.GetFloat("_Metallic"), .92f), "Rim lost its metal finish when wet.");
            Require(Mathf.Approximately(originalPaint.GetFloat("_Smoothness"), drySmoothness) && originalPaint.GetColor("_BaseColor") == dryColor, "Wetness modified a shared paint asset.");
            car.PreviewLivery("generic_black"); car.body.GetPropertyBlock(block, 0);
            Require(surface.Wetness == 1 && block.GetFloat("_Smoothness") >= .89f, "Livery change lost the current wetness.");
            surface.SetWetness(-1); Require(surface.Wetness == 0, "Wetness lower bound is not clamped.");
            for (int i = 0; i < 10; i++) { surface.SetWetness(2); surface.SetWetness(0); }
            car.PreviewLivery(FormulaProgression.State.equippedLivery);
            car.body.GetPropertyBlock(block, 0);
            Require(block.isEmpty && car.body.sharedMaterial == originalPaint && Mathf.Approximately(originalPaint.GetFloat("_Smoothness"), drySmoothness), "Dry/wet cycles compound the finish or leave property blocks on a dry car.");
            Require(Mathf.Approximately(car.GetComponent<RCCP_CarController>().Engine.maximumTorqueAsNM, torque), "Surface wetness changed vehicle physics.");
            Pass("Dry/wet surfaces preserve liveries and physics, retain metal rims/nonmetal rubber, isolate carbon, clamp values and restore without mutating shared materials");
        }
        finally { car.PreviewLivery(FormulaProgression.State.equippedLivery); surface.SetWetness(originalWetness); }
    }

    private static void ValidateHomeFraming(FormulaVehicle car)
    {
        Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        foreach(var renderer in car.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy))
        {
            var bounds=renderer.bounds;
            for(int i=0;i<8;i++)
            {
                var world=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector2 point=Camera.main.WorldToViewportPoint(world); min=Vector2.Min(min,point); max=Vector2.Max(max,point);
            }
        }
        File.WriteAllText("Docs/AI/FormulaUI/HomeFraming.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { min=new[] { min.x,min.y },max=new[] { max.x,max.y },Camera.main.fieldOfView },Newtonsoft.Json.Formatting.Indented));
        Require(min.x>.35f && max.x<.99f && min.y>.15f && max.y<.85f,"Home car framing overlaps navigation, leaves the camera or meets the footer: "+min+" to "+max);
        Pass("Home keeps the whole Generic inside the hero area with five actions and a visible controller selection");
    }
}
