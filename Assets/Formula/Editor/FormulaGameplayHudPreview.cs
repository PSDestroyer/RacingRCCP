using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Explicit HUD acceptance: real scene/AI/input plus labelled synthetic telemetry boundary checks.
[InitializeOnLoad]
public static class FormulaGameplayHudPreview
{
    private const string Active = "Formula.Hud.Active", Stage = "Formula.Hud.Stage", Previous = "Formula.Hud.StartScene";
    private const string Folder = "Docs/AI/FormulaUI/GameplayPolish";
    [Serializable] private sealed class Report
    {
        public string phase, unityVersion, method = "Graphical Editor, isolated save, real race and input. Split/RPM/proximity and career finishes are scripted boundary checks; no natural race completion claim.";
        public int width, height, startFrame, endFrame;
        public string[] passed = Array.Empty<string>(), errors = Array.Empty<string>();
    }
    private static Report report;
    private static double started;
    private static int frame;
    private static int retrySceneHandle;
    private static Vector3 initialPosition;
    private static Vector2 initialMapPosition;
    private static Keyboard keyboard;
    private static Gamepad pad;
    private static double splitBase;
    private static GameObject previewResult;
    static FormulaGameplayHudPreview()
    {
        EditorApplication.playModeStateChanged += OnMode;
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }
    [MenuItem("Tools/Formula Career/Preview Gameplay HUD")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        FormulaRaceAuthoring.RefineHud();
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaHud-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("Formula.Hud.Background", Application.runInBackground);
        var assembly = typeof(Editor).Assembly;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        var gameView = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        int index = (int)gameView.GetType().GetProperty("selectedSizeIndex", flags).GetValue(gameView);
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.GetProperty("instance", flags).GetValue(null);
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { sizesType.GetProperty("currentGroupType", flags).GetValue(sizes) });
        var size = group.GetType().GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { index });
        SessionState.SetInt("Formula.Hud.Width", (int)size.GetType().GetProperty("width", flags).GetValue(size));
        SessionState.SetInt("Formula.Hud.Height", (int)size.GetType().GetProperty("height", flags).GetValue(size));
        SessionState.SetBool(Active, true); SessionState.SetInt(Stage, 0);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    [MenuItem("Tools/Formula Career/Preview Gameplay HUD at 1080p")]
    public static void Run1080() { FormulaGameViewValidation.Select1080(); Run(); }
    private static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report { phase = "menu", unityVersion = Application.unityVersion, width = SessionState.GetInt("Formula.Hud.Width", 0), height = SessionState.GetInt("Formula.Hud.Height", 0), startFrame = Time.frameCount };
            started = EditorApplication.timeSinceStartup; Application.runInBackground = true;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (pad != null) InputSystem.RemoveDevice(pad);
            keyboard = null; pad = null;
            SessionState.SetBool(Active, false); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
            Application.runInBackground = SessionState.GetBool("Formula.Hud.Background", false);
            string previous = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            FormulaGameViewValidation.Restore();
        }
    }
    private static T Field<T>(FormulaRaceView view, string name) => (T)typeof(FormulaRaceView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    private static void Pass(string text) { report.passed = report.passed.Concat(new[] { text }).ToArray(); Save(); }
    private static void Save()
    {
        Directory.CreateDirectory(Folder); report.endFrame = Time.frameCount;
        File.WriteAllText(Folder + "/Validation-" + report.width + ".json", JsonUtility.ToJson(report, true));
    }
    private static void Next(int stage, string phase) { SessionState.SetInt(Stage, stage); report.phase = phase; started = EditorApplication.timeSinceStartup; Save(); }
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Folder + "/" + name + "-" + report.width + ".png");
    private static void Click(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    private static void CheckLayout(FormulaRaceView view)
    {
        Canvas.ForceUpdateCanvases();
        CheckTextLayout(view.Hud.transform);
        var corners = new Vector3[4];
        foreach (Transform group in view.Hud.transform)
        {
            if (!group.gameObject.activeInHierarchy) continue;
            ((RectTransform)group).GetWorldCorners(corners);
            var local = corners.Select(c => view.transform.InverseTransformPoint(c));
            Require(local.All(c => c.x >= -1 && c.x <= 1281 && c.y >= -721 && c.y <= 1), "HUD group outside its authored viewport: " + group.name);
        }
    }
    private static void CheckTextLayout(Transform root)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in root.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            if (string.IsNullOrWhiteSpace(text.text) || text.GetComponentInParent<CanvasGroup>()?.alpha == 0) continue;
            Require(text.textWrappingMode != TextWrappingModes.NoWrap || text.preferredWidth <= text.rectTransform.rect.width + 2 || text.overflowMode == TextOverflowModes.Ellipsis,
                "Text exceeds its slot: " + text.name + " / " + text.text);
            // Font ascender/descender bounds include unused space; measure the rendered glyphs.
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                var glyph = text.textInfo.characterInfo[i]; if (!glyph.isVisible) continue;
                low = Mathf.Min(low, glyph.bottomLeft.y); high = Mathf.Max(high, glyph.topRight.y);
            }
            Require(!float.IsInfinity(low) && !float.IsInfinity(high), "Nonempty label generated no visible glyphs: " + text.name + " / " + text.text);
            Require(high - low <= text.rectTransform.rect.height + 2, "Text exceeds its slot height: " + text.name + " / " + (high - low) + " vs " + text.rectTransform.rect.height);
        }
    }
    private static void Update()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || report == null) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - started; Require(age < 150, "HUD phase timed out: " + report.phase);
            if (age < .5 || ShaderUtil.anythingCompiling) return;
            int stage = SessionState.GetInt(Stage, 0);
            var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
            var view = UnityEngine.Object.FindFirstObjectByType<FormulaRaceView>();
            if (stage == 0)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null) return;
                Require(FormulaSession.Launch("race:track_01", "quick") == null, "Quick Race failed to launch."); Next(1, "race");
            }
            else if (stage == 1)
            {
                if (game == null || view == null || game.CarController == null || !game.FormulaRaceStarted || age < 5) return;
                Require(Field<TMP_Text>(view, "position").text == "P" + game.FormulaPosition, "Position disagrees with race authority.");
                Require(Field<TMP_Text>(view, "timingCaption").text == "CURRENT LAP" && Field<TMP_Text>(view, "sessionTime").gameObject.activeInHierarchy, "Lap and race clocks must be distinctly labelled from the first lap.");
                Require(Field<TMP_Text[]>(view, "standings").Count(t => t.gameObject.activeInHierarchy && t.text.Contains("YOU")) == 1, "Leaderboard must identify the actual player once.");
                Require(!view.Hud.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "VALID LAP"), "Premature valid-lap confirmation remains.");
                Require(Time.frameCount > report.startFrame + 10, "Player frames did not advance.");
                Require(!Field<GameObject>(view, "careerGoal").activeSelf, "Quick Race retains an unnecessary permanent objective.");
                Require(!Field<TMP_Text[]>(view, "standings").Any(t => t.text.Contains("AI ")), "Placeholder driver names remain in the race standings.");
                Pass("Real four-car race shows authoritative position, fictional driver names, distinct clocks and no redundant goal panel");
                var map = view.Hud.GetComponentInChildren<FormulaRaceMinimap>();
                var markers = map.GetComponentInChildren<FormulaMinimapMarkers>();
                Require(map.IsActive() && map.RoutePointCount == game.raceWaypoints.waypoints.Count && markers.OpponentMarkerCount == 3, "Race minimap must show the active route, player and three actual rivals.");
                var rect = map.rectTransform.rect;
                foreach (var point in game.raceWaypoints.waypoints)
                    Require(rect.Contains(map.WorldToMap(point.transform.position)), "Circuit route extends outside its minimap.");
                Pass("Minimap fits the live Racing Visuals circuit and includes all three real AI cars");
                CheckLayout(view); frame = Time.frameCount; Next(2, "quiet-hud");
                CheckDryRoad("Racing_Game", "Asphalt");
                CheckAudio();
            }
            else if (stage == 2 && age > 8)
            {
                Require(Field<CanvasGroup>(view, "notification").alpha == 0 && Time.frameCount > frame + 10, "Intro objective did not disappear during advancing gameplay.");
                Capture("Race-Clean");
                float area = view.Hud.GetComponentsInChildren<FormulaRacePanelGraphic>().Where(g => g.GetComponentInParent<CanvasGroup>()?.alpha != 0).Sum(g => g.rectTransform.rect.width * g.rectTransform.rect.height);
                area += Field<UnityEngine.UI.Image[]>(view, "standingHighlights").Where(g => g.gameObject.activeInHierarchy).Sum(g => g.rectTransform.rect.width * g.rectTransform.rect.height);
                Require(area < 217040 * .8f, "Quick Race panel coverage was not reduced by at least twenty percent.");
                Pass("Intro disappears and live Quick Race panel coverage is reduced by at least twenty percent");
                initialPosition = game.CarController.transform.position;
                initialMapPosition = view.Hud.GetComponentInChildren<FormulaMinimapMarkers>().PlayerMapPosition;
                keyboard = InputSystem.AddDevice<Keyboard>("HUD keyboard acceptance");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); Next(3, "keyboard-driving");
            }
            else if (stage == 3 && age > 3)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Require(Vector3.Distance(initialPosition, game.CarController.transform.position) > .5f, "Keyboard did not move the real RCCP vehicle.");
                Require(Vector2.Distance(initialMapPosition, view.Hud.GetComponentInChildren<FormulaMinimapMarkers>().PlayerMapPosition) > .01f, "Player marker did not follow real driving on the minimap.");
                Pass("Keyboard drives the RCCP car; displayed speed and gear come from its telemetry");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); Next(4, "keyboard-pause");
            }
            else if (stage == 4)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Require(Time.timeScale == 0 && !view.Hud.activeSelf, "Escape must pause once and hide the gameplay HUD.");
                Require(Field<TMP_Text>(view, "pauseObjective").gameObject.activeInHierarchy && Field<TMP_Text>(view, "pauseObjective").text.Contains("Race for your best finishing position"), "Pause must recap the current session goal.");
                Pass("Pause contains a readable goal and current progress recap");
                Capture("Pause"); Next(5, "keyboard-resume-input");
            }
            else if (stage == 5)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); Next(6, "keyboard-resume");
            }
            else if (stage == 6)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Require(Time.timeScale == 1 && view.Hud.activeSelf, "Escape did not resume the HUD.");
                InputSystem.RemoveDevice(keyboard); keyboard = null;
                Pass("Escape pauses/resumes once; HUD is hidden behind pause and restored on resume");
                pad = InputSystem.AddDevice<Gamepad>("HUD gamepad acceptance");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start)); Next(7, "gamepad-pause");
            }
            else if (stage == 7)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState()); Require(Time.timeScale == 0, "Gamepad Start did not pause."); Next(8, "gamepad-resume-input");
            }
            else if (stage == 8)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start)); Next(9, "gamepad-resume");
            }
            else if (stage == 9)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState()); Require(Time.timeScale == 1, "Gamepad Start did not resume.");
                Require(Field<TMP_Text>(view, "pauseHint").text == "MENU", "Pause hint did not follow the active input device.");
                Pass("Virtual gamepad Start pauses/resumes and changes the pause prompt");
                InputSystem.RemoveDevice(pad); pad = null;
                float rpm = game.CarController.engineRPM;
                game.CarController.engineRPM = game.CarController.maxEngineRPM; view.Tick(game);
                Require(Field<UnityEngine.UI.Image[]>(view, "shiftLights").All(l => l.color.a > .99f), "Full RPM did not light all twelve shift lamps.");
                game.CarController.engineRPM = game.CarController.minEngineRPM; view.Tick(game);
                Require(Field<UnityEngine.UI.Image[]>(view, "shiftLights").All(l => l.color.a < .2f), "Idle RPM did not dim shift lamps.");
                game.CarController.engineRPM = rpm; view.Tick(game);
                Pass("Synthetic idle/limiter telemetry verifies twelve shift lamps without changing vehicle tuning");
                CheckProximity(game, view);
                FormulaProgression.State.textScale = 115; FormulaProgression.State.safeAreaPercent = 95;
                view.GetComponentInParent<FormulaPresentation>().Refresh(); Capture("Proximity-Text115"); Next(10, "accessibility");
            }
            else if (stage == 10)
            {
                CheckLayout(view); Capture("Race-Text115-Safe95"); Pass("HUD remains in bounds and text fits at 115 percent with 95 percent safe area");
                Next(100, "accessibility-capture");
            }
            else if (stage == 100)
            {
                FormulaProgression.State.textScale = 100; FormulaProgression.State.safeAreaPercent = 100;
                FormulaSession.ReturnToMenu(); Next(11, "return-menu");
            }
            else if (stage == 11)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null) return;
                Require(FormulaSession.Launch("trial:rtg_02", "time_trial") == null, "Solo RTG event failed to launch."); Next(12, "solo");
            }
            else if (stage == 12)
            {
                if (game == null || view == null || game.CarController == null || !game.FormulaRaceStarted || game.FormulaElapsed < 3) return;
                Require(!Field<GameObject>(view, "standingsRoot").activeSelf && Field<TMP_Text>(view, "timingCaption").text == "CURRENT LAP", "Solo session has race-only UI.");
                Require(Field<TMP_Text>(view, "bestLap").text == "FIRST TIMED LAP" && !Field<TMP_Text>(view, "delta").gameObject.activeSelf, "Missing reference must be explained, not shown as a blank delta.");
                Require(!Field<GameObject>(view, "carLeft").activeSelf && !Field<GameObject>(view, "carRight").activeSelf && !Field<GameObject>(view, "careerGoal").activeSelf, "Solo free mode shows race warnings or a redundant career goal.");
                Capture("Solo"); Pass("Solo Time Attack explains the first attempt and hides the free-session goal, standings and proximity");
                var map = view.Hud.GetComponentInChildren<FormulaRaceMinimap>();
                Require(game.GetComponent<FormulaCircuit>().trackId == "rtg_02" && map.RoutePointCount == game.raceWaypoints.waypoints.Count && map.GetComponentInChildren<FormulaMinimapMarkers>().OpponentMarkerCount == 0, "RTG minimap must use its own route and show no stale rival markers in solo mode.");
                Require(!view.Hud.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "RIVALS"), "Solo minimap retains an unused rivals legend.");
                Pass("RTG circuit map rebinds on scene change with its own geometry and no rivals in solo mode");
                CheckDryRoad("RTG", "Road");
                FormulaSession.InvalidateLap(); Next(13, "invalid-feedback");
            }
            else if (stage == 13)
            {
                Require(Field<TMP_Text>(view, "lapStatus").text == "LAP INVALID" && Field<CanvasGroup>(view, "notification").alpha > 0, "Invalid lap warning is missing.");
                Capture("Invalid-Lap"); Next(14, "persistent-invalid");
            }
            else if (stage == 14 && age > 6)
            {
                Require(Field<CanvasGroup>(view, "notification").alpha == 0 && Field<TMP_Text>(view, "lapStatus").text == "LAP INVALID", "Invalid-lap state must persist after its toast disappears.");
                Pass("Track limits show a temporary explanation and a persistent invalid-lap indicator");
                splitBase = game.FormulaElapsed; FormulaSession.Lap(splitBase);
                FormulaSession.Checkpoint(0, splitBase + 10); FormulaSession.Checkpoint(1, splitBase + 20);
                FormulaSession.Lap(splitBase + 100); Next(15, "valid-lap-feedback");
            }
            else if (stage == 15)
            {
                Require(FormulaSession.ValidLaps == 1 && Field<TMP_Text>(view, "validity").text == "NEW BEST LAP", "Valid-lap confirmation must follow an actual lap notification.");
                FormulaSession.Checkpoint(0, splitBase + 109.5); Next(16, "split-comparison");
            }
            else if (stage == 16)
            {
                Require(Field<TMP_Text>(view, "delta").text == "-0.50", "Split must compare the same checkpoint with the valid best lap.");
                Capture("Split-To-Best"); Pass("Synthetic checkpoint/lap events confirm a valid best lap and a measured split; invalid laps never replace the reference");
                Next(160, "split-capture");
            }
            else if (stage == 160)
            {
                FormulaSession.InvalidateLap(); Next(17, "invalid-split");
            }
            else if (stage == 17)
            {
                Require(Field<TMP_Text>(view, "delta").text == "—", "Invalid lap must suppress split comparisons.");
                Require(FormulaProgression.Data.money == 0, "HUD validation changed credits.");
                Pass("Invalid lap suppresses its delta; HUD changes do not spend credits");
                FormulaSession.ReturnToMenu(); Next(18, "shakedown-menu");
            }
            else if (stage == 18)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null) return;
                Require(FormulaSession.Launch("g01", "career") == null, "Shakedown failed to launch."); Next(19, "one-lap-career");
            }
            else if (stage == 19)
            {
                if (game == null || view == null || !game.FormulaRaceStarted || game.FormulaElapsed < 3) return;
                Require(game.totalRaceLaps == 1 && Field<TMP_Text>(view, "goalValue").text == "ONE VALID LAP" && Field<GameObject>(view, "careerGoal").activeSelf, "Shakedown goal is unclear.");
                Require(Field<CanvasGroup>(view, "notification").alpha == 0, "Career repeats its permanent goal in an intro notification.");
                FormulaSession.InvalidateLap(); Next(20, "final-lap-invalid");
            }
            else if (stage == 20)
            {
                Require(Field<TMP_Text>(view, "objective").text == "Finish session, then select Retry" && Field<TMP_Text>(view, "goalProgress").text == "RETRY AFTER FINISH", "One-lap invalidation falsely promises another lap.");
                Capture("Shakedown-Invalid"); Pass("Only/final invalid lap directs Retry after finish instead of promising a nonexistent next lap");
                FormulaSession.Lap(game.FormulaElapsed);
                Require(FormulaSession.Complete(true, 1, game.FormulaElapsed) != null, "Scripted invalid finish could not save its result."); Next(21, "failed-result");
            }
            else if (stage == 21)
            {
                var result = UnityEngine.Object.FindFirstObjectByType<FormulaResultsView>(); if (result == null) return;
                Require(result.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "NO VALID LAP"), "Failure result does not explain the missing valid lap.");
                Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "retry" && !result.transform.Find("next").gameObject.activeSelf, "Failure must prioritize Retry without an unrelated next event.");
                Require(FormulaProgression.Data.money == 0 && FormulaProgression.State.Best("g01") == Medal.None, "Invalid Shakedown awarded career progress.");
                Require(!result.transform.Find("Result credits").gameObject.activeSelf && !result.transform.Find("Result xp").gameObject.activeSelf && !result.transform.Find("Progression").gameObject.activeSelf, "Zero rewards or unchanged XP dominate the failed result.");
                Require(result.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "RESULT SAVED") && !result.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "PROGRESS SAVED"), "Failure incorrectly claims progression.");
                CheckTextLayout(result.transform);
                Capture("Failed-Result"); Pass("Scripted invalid Shakedown result explains failure, focuses Retry and awards no medal or credits"); Next(22, "retry-action");
            }
            else if (stage == 22)
            {
                retrySceneHandle = game.gameObject.scene.handle;
                Click("retry"); Next(23, "retry-reload");
            }
            else if (stage == 23)
            {
                if (game == null || view == null || game.gameObject.scene.handle == retrySceneHandle || !game.FormulaRaceStarted || game.FormulaElapsed < 2) return;
                Require(!FormulaSession.LapInvalid && FormulaSession.ValidLaps == 0, "Retry retained invalid-lap state.");
                FormulaSession.Lap(250);
                Require(FormulaSession.Complete(true, 1, 250) != null, "Scripted valid finish could not save its result."); Next(24, "passed-result");
            }
            else if (stage == 24)
            {
                var result = UnityEngine.Object.FindFirstObjectByType<FormulaResultsView>(); if (result == null) return;
                Require(FormulaProgression.State.Best("g01") == Medal.Bronze && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "next", "Passed result must focus the next career event.");
                Require(result.transform.Find("Result credits").gameObject.activeSelf && result.transform.Find("Result xp").gameObject.activeSelf && result.transform.Find("Progression").gameObject.activeSelf, "Passed career result lost actual rewards or XP progress.");
                CheckTextLayout(result.transform);
                Capture("Passed-Result"); Pass("Retry really reloads a clean session; scripted valid Shakedown saves Bronze and prioritizes Next Event"); Next(25, "next-action");
            }
            else if (stage == 25)
            {
                Click("next"); Next(26, "career-review");
            }
            else if (stage == 26)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>(); if (menu == null) return;
                Require(menu.View.Dialogs.IsOpen && !FormulaSession.InRace, "Next event must open its briefing.");
                Pass("Next Event returns to its career briefing with progress retained");
                Require(FormulaSession.Launch("g02", "career") == null, "Career Time Attack failed to launch."); Next(27, "career-time-target");
            }
            else if (stage == 27)
            {
                if (game == null || view == null || !game.FormulaRaceStarted || game.FormulaElapsed < 2) return;
                string target = FormulaRaceUI.TimeText(FormulaAssets.Current.FindTrack(FormulaSession.CurrentEvent.trackId).referenceLapSeconds * 1.2);
                Require(Field<TMP_Text>(view, "goalValue").text == target && Field<TMP_Text>(view, "goalCaption").text == "TIME TO BEAT" && Field<TMP_Text>(view, "pauseObjective").text.Contains(target + " OR FASTER"), "Career time target is missing from HUD or Pause.");
                Require(Field<CanvasGroup>(view, "notification").alpha == 0, "Career duplicates its time target in a toast.");
                FormulaProgression.State.textScale = 115; FormulaProgression.State.safeAreaPercent = 95;
                view.GetComponentInParent<FormulaPresentation>().Refresh();
                CheckLayout(view); Capture("Career-Time-Target"); Pass("Career Time Attack retains the exact pass time in HUD and Pause");
                Next(28, "final-capture");
            }
            else if (stage == 28)
            {
                CheckResultVariants(view);
                previewResult = UnityEngine.Object.Instantiate(FormulaMenuAssets.Current.resultsPrefab, view.transform.parent, false);
                view.GetComponentInParent<FormulaPresentation>().Register((RectTransform)previewResult.transform);
                string mode = FormulaProgression.State.activeMode;
                FormulaProgression.State.activeMode = "time_trial";
                previewResult.GetComponent<FormulaResultsView>().Present(new FormulaReceipt { eventId = "trial:rtg_02", elapsedSeconds = 100, bestLapSeconds = 90, personalBest = true });
                FormulaProgression.State.activeMode = mode;
                Next(29, "free-result-preview");
            }
            else if (stage == 29)
            {
                CheckTextLayout(previewResult.transform); Capture("Free-Result-Text115"); Next(30, "free-result-capture");
            }
            else if (stage == 30)
            {
                previewResult.GetComponent<FormulaResultsView>().Present(new FormulaReceipt { eventId = "g01", medal = Medal.Bronze, elapsedSeconds = 250, bestLapSeconds = 250, credits = 1200, xp = 1000, levelCredits = 150, newLiveries = new System.Collections.Generic.List<string> { FormulaCatalog.Current.liveries[1].id } });
                Require(previewResult.transform.Find("Reward Livery").GetComponent<CanvasGroup>().alpha == 0, "Livery reveal did not begin from its authored transition state.");
                Require(previewResult.GetComponent<CanvasGroup>().interactable && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "next", "Result transition blocks immediate navigation.");
                Next(31, "livery-result-preview");
            }
            else if (stage == 31)
            {
                Require(previewResult.transform.Find("Reward Livery").GetComponent<CanvasGroup>().alpha == 1 && previewResult.GetComponent<CanvasGroup>().alpha == 1, "Result/livery reveal did not settle.");
                CheckTextLayout(previewResult.transform); Capture("Livery-Reward-Text115");
                Next(32, "reward-capture");
            }
            else if (stage == 32)
            {
                pad = InputSystem.AddDevice<Gamepad>("Results navigation acceptance");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadRight));
                Next(33, "result-gamepad-navigation");
            }
            else if (stage == 33)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState());
                Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "menu", "Gamepad cannot move from Next Event to Menu on results.");
                InputSystem.RemoveDevice(pad); pad = null;
                Pass("Result and livery fades settle without delaying focus; real UI actions respond to a virtual gamepad");
                UnityEngine.Object.Destroy(previewResult); previewResult = null;
                report.phase = "complete"; Save(); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception error)
        {
            report.errors = report.errors.Concat(new[] { error.Message + "\n" + error.StackTrace }).ToArray(); report.phase = "failed"; Save(); EditorApplication.ExitPlaymode();
        }
    }
    private static void CheckProximity(GamePlayManager game, FormulaRaceView view)
    {
        var rivals = game.aiRacers.Where(r => r != null && r.racerTransform != null).ToArray();
        Require(rivals.Length >= 2, "Proximity probe needs two actual rival cars.");
        var original = rivals.Select(r => r.racerTransform.position).ToArray();
        var player = game.CarController.transform;
        Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized);
        try
        {
            // Geometry probe starts without a previous real overlap's 0.4-second release hold.
            foreach (string name in new[] { "leftUntil", "rightUntil" })
                typeof(FormulaRaceView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 0f);
            for (int i = 0; i < rivals.Length; i++) rivals[i].racerTransform.position = player.position + Vector3.up * 10;
            view.Tick(game);
            Require(!Field<GameObject>(view, "carLeft").activeSelf && !Field<GameObject>(view, "carRight").activeSelf, "Cars on an overpass trigger an overlap warning.");
            rivals[0].racerTransform.position = player.position - right * 4;
            rivals[1].racerTransform.position = player.position + right * 4;
            view.Tick(game);
            Require(Field<GameObject>(view, "carLeft").activeSelf && Field<GameObject>(view, "carRight").activeSelf, "Actual rivals on both sides must trigger directional warnings.");
            Require(Field<GameObject>(view, "carLeft").GetComponentsInChildren<TMP_Text>().Length == 0 && Field<GameObject>(view, "carRight").GetComponentsInChildren<TMP_Text>().Length == 0, "Proximity still requires reading a text label.");
            Pass("Scripted real-rival positions verify both proximity sides and reject cars on another elevation; no time gaps are fabricated");
        }
        finally { for (int i = 0; i < rivals.Length; i++) rivals[i].racerTransform.position = original[i]; }
    }
    private static void CheckResultVariants(FormulaRaceView race)
    {
        var state = FormulaProgression.State;
        string mode = state.activeMode;
        var system = UnityEngine.EventSystems.EventSystem.current;
        var selection = system.currentSelectedGameObject;
        var root = UnityEngine.Object.Instantiate(FormulaMenuAssets.Current.resultsPrefab, race.transform.parent, false);
        var result = root.GetComponent<FormulaResultsView>();
        race.GetComponentInParent<FormulaPresentation>().Register((RectTransform)root.transform);
        int money = FormulaProgression.Data.money;
        bool reducedMotion = state.reducedMotion;
        try
        {
            state.reducedMotion = true; // All text is visible during immediate variant geometry checks.
            state.activeMode = "career";
            result.Present(new FormulaReceipt { eventId = "g02", bestLapSeconds = 999, elapsedSeconds = 1000, credits = 300, xp = 100 });
            Require(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "TARGET MISSED") && system.currentSelectedGameObject.name == "retry", "Missed career time target must explain failure and focus Retry.");
            Require(root.transform.Find("Result credits").gameObject.activeSelf && root.transform.Find("Result xp").gameObject.activeSelf, "Failure hides actual valid-finish rewards.");
            CheckTextLayout(root.transform);
            result.Present(new FormulaReceipt { eventId = "g03", bestLapSeconds = 90, elapsedSeconds = 300, finishPosition = 4 });
            Require(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "OUTSIDE TOP 3") && system.currentSelectedGameObject.name == "retry", "P4 failure must identify the missed position requirement.");
            CheckTextLayout(root.transform);
            state.activeMode = "time_trial";
            result.Present(new FormulaReceipt { eventId = "trial:rtg_02", elapsedSeconds = 100 });
            Require(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "NO VALID LAP") && system.currentSelectedGameObject.name == "retry", "Free session without a valid lap must also focus Retry.");
            CheckTextLayout(root.transform);
            Require(((RectTransform)root.transform.Find("Unlocks")).anchoredPosition.x == 30 && ((RectTransform)root.transform.Find("Next Medal")).anchoredPosition.x == 30, "Failure status is detached from its explanation and Retry.");
            result.Present(new FormulaReceipt { eventId = "trial:rtg_02", elapsedSeconds = 100, bestLapSeconds = 90, personalBest = true });
            Require(!root.transform.Find("Result credits").gameObject.activeSelf && !root.transform.Find("Result xp").gameObject.activeSelf && ((RectTransform)root.transform.Find("Result bestLap")).anchoredPosition.y == -208, "Free session should emphasize lap performance instead of zero rewards.");
            CheckTextLayout(root.transform);
            state.activeMode = "career";
            result.Present(new FormulaReceipt { eventId = "g01", medal = Medal.Bronze, bestLapSeconds = 250, elapsedSeconds = 250, credits = 1200, xp = 1000, levelCredits = 150, newLiveries = new System.Collections.Generic.List<string> { FormulaCatalog.Current.liveries[1].id } });
            Require(!root.transform.Find("Result bestLap").gameObject.activeSelf && root.transform.Find("Result elapsed/Caption").GetComponent<TMP_Text>().text == "LAP TIME", "Single-lap result repeats the same clock.");
            var art = (RectTransform)root.transform.Find("Reward Livery");
            Require(art.rect.width * art.rect.height >= 200 * 100 * 2.8f && art.GetComponent<CanvasGroup>().alpha == 1 && !art.GetComponent<CanvasGroup>().blocksRaycasts, "Livery art is too small, animated in reduced motion or blocks input.");
            Require(system.currentSelectedGameObject.name == "next" && ((RectTransform)root.transform.Find("retry")).rect.width == 390, "Reused results must restore the three-action layout and Next Event focus after failure.");
            Require(FormulaProgression.Data.money == money, "Result presentation variants altered the economy.");
            CheckTextLayout(root.transform);
            Pass("Synthetic result variants at 115 percent verify missed time, P4, free-session failure/success, real nonzero rewards and repeated outcome layouts without changing credits");
            Pass("Single-lap clock is unique; failure explanation, saved status and Retry align; livery art is 2.8 times larger and reduced motion is immediate");
        }
        finally
        {
            state.activeMode = mode; state.reducedMotion = reducedMotion; system.SetSelectedGameObject(selection); UnityEngine.Object.Destroy(root);
        }
    }
    private static void CheckDryRoad(string family, string name)
    {
        var materials = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials).Where(m => m != null && m.name.StartsWith(name) && m.name.EndsWith("_Dry")).Distinct().ToArray();
        Require(materials.Length > 0 && materials.All(m => Mathf.Approximately(m.GetFloat("_Smoothness"), .22f) && (!m.HasProperty("_Metallic") || m.GetFloat("_Metallic") == 0)), family + " dry road was not rebound to the matte asphalt material.");
        Require(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).All(l => l.intensity <= 2), "Gameplay sun still clips the vehicle highlights.");
        Pass(family + " live scene uses Formula-owned dry asphalt, with smoothness 0.22 and metallic 0");
    }
    private static void CheckAudio()
    {
        var assets = FormulaMenuAssets.Current;
        var clips = new[] { assets.focusSound, assets.confirmSound, assets.recordSound, assets.resultSound, assets.unlockSound, assets.endSound };
        Require(SoundManager.Instance != null && clips.All(c => c != null && c.length > 0 && c.length < 1 && c.channels == 1), "Formula UI audio cues are missing or incorrectly imported.");
        var source = (AudioSource)typeof(SoundManager).GetField("sfxSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(SoundManager.Instance);
        Require(source != null && source.outputAudioMixerGroup == SoundManager.Instance.sfxMixerGroup && source.spatialBlend == 0, "UI cues do not use the existing 2D SFX mixer route.");
        Pass("Six original mono cues under one second use the existing 2D SFX source and mixer; no additional AudioSources are created for feedback");
    }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (report != null && SessionState.GetBool(Active, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
        { report.errors = report.errors.Concat(new[] { message }).ToArray(); Save(); }
    }
}
