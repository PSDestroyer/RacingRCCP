using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;
using static FormulaExperienceBuilder;

public static class FormulaRaceAuthoring
{
    private const string RacePath = "Assets/Formula/Resources/Formula/UI/FormulaRace.prefab";
    [MenuItem("Tools/Formula Career/Refine Gameplay HUD")]
    public static void RefineHud()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(RacePath);
        try
        {
            Hud(root.GetComponent<FormulaRaceView>());
            PrefabUtility.SaveAsPrefabAsset(root, RacePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        const string resultsPath = "Assets/Formula/Resources/Formula/UI/FormulaResults.prefab";
        var results = PrefabUtility.LoadPrefabContents(resultsPath);
        try
        {
            FormulaExperienceBuilder.StyleResults(results);
            PrefabUtility.SaveAsPrefabAsset(results, resultsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(results); }
        AssetDatabase.SaveAssets();
        Debug.Log("FORMULA_GAMEPLAY_HUD_AUTHORED");
    }
    public static void Build()
    {
        var root = new GameObject("Race Presentation", typeof(RectTransform), typeof(FormulaRaceView)); Place(root.transform, 0, 0, 1280, 720);
        try
        {
            var view = root.GetComponent<FormulaRaceView>();
            var hud = Group(root.transform, "HUD", 0, 0, 1280, 720); Set(view, "hud", hud.gameObject);
            var top = Surface(hud, "Session", 30, 24, 320, 84);
            Set(view, "eventName", Label(top, "Event", "SHAKEDOWN", 18, 4, 284, 40, 32, true));
            Set(view, "position", Label(top, "Position", "SOLO", 18, 45, 120, 30, 23));
            Set(view, "lap", Label(top, "Lap", "LAP 1 / 1", 156, 45, 146, 30, 23));
            var timing = Surface(hud, "Timing", 690, 24, 380, 84);
            Label(timing, "Session Caption", "SESSION", 18, 6, 160, 26, 18); Set(view, "sessionTime", Label(timing, "Session Time", "—", 18, 35, 160, 44, 34, true));
            Label(timing, "Lap Caption", "CURRENT LAP", 200, 6, 162, 26, 18); Set(view, "lapTime", Label(timing, "Lap Time", "—", 200, 35, 162, 44, 34, true));
            var best = Surface(hud, "Personal Best", 690, 116, 380, 34); Set(view, "bestLap", Label(best, "Best Lap", "BEST  —", 18, 0, 344, 34, 21));
            Set(view, "pauseButton", Button(hud, "race-pause", "PAUSE", 1090, 24, 160, 52));
            var target = Surface(hud, "Target", 30, 606, 640, 84);
            Set(view, "objective", Label(target, "Objective", "OBJECTIVE  /  COMPLETE A VALID LAP", 18, 6, 604, 30, 21));
            var validity = Label(target, "Lap Validity", "VALID LAP", 18, 38, 604, 44, 19); validity.textWrappingMode = TextWrappingModes.Normal; Set(view, "validity", validity);
            var countdown = Label(hud, "Start Message", "", 400, 248, 480, 140, 80, true); countdown.alignment = TextAlignmentOptions.Center; Set(view, "startMessage", countdown);
            var car = Surface(hud, "Vehicle", 930, 536, 320, 154);
            Set(view, "speed", Label(car, "Speed", "0", 20, 8, 170, 76, 64, true)); Label(car, "Unit", "KM/H", 22, 84, 120, 27, 20);
            Set(view, "gear", Label(car, "Gear", "N", 228, 8, 72, 76, 64, true)); Label(car, "Gear Caption", "GEAR", 228, 84, 72, 27, 20);
            Set(view, "rpm", Label(car, "RPM", "0 RPM", 20, 117, 260, 27, 19)); Set(view, "revFill", Fill(car, "Rev Strip", 20, 107, 280, 5));
            var pause = Backdrop(root.transform, "Pause"); Set(view, "pause", pause.gameObject);
            var overview = Surface(pause, "Overview", 320, 104, 640, 512); Set(view, "pauseOverview", overview.gameObject);
            Label(overview, "Caption", "FORMULA G27  /  SESSION", 30, 20, 580, 30, 20); Label(overview, "Heading", "SESSION PAUSED", 30, 55, 580, 66, 54, true);
            Set(view, "resume", Button(overview, "resume", "RESUME", 30, 145, 580, 66)); Set(view, "restart", Button(overview, "restart", "RESTART SESSION", 30, 225, 580, 66));
            Set(view, "settings", Button(overview, "audio", "AUDIO", 30, 305, 580, 66)); Set(view, "leave", Button(overview, "leave", "LEAVE SESSION", 30, 385, 580, 66));
            var confirmation = Surface(pause, "Confirmation", 230, 176, 820, 368); Set(view, "pauseConfirmation", confirmation.gameObject);
            Set(view, "confirmTitle", Label(confirmation, "Heading", "LEAVE SESSION?", 32, 24, 756, 68, 50, true));
            var message = Label(confirmation, "Message", "Your unfinished run will not award credits or XP.", 32, 118, 756, 106, 26); message.textWrappingMode = TextWrappingModes.Normal; Set(view, "confirmDetails", message);
            Set(view, "cancel", Button(confirmation, "cancel", "KEEP RACING", 32, 270, 370, 66)); Set(view, "confirm", Button(confirmation, "confirm", "CONFIRM", 418, 270, 370, 66)); confirmation.gameObject.SetActive(false);
            var options = Backdrop(root.transform, "Audio Options"); Set(view, "options", options.gameObject);
            Label(options, "Heading", "AUDIO", 150, 120, 980, 64, 54, true);
            var sliders = new UnityEngine.Object[3]; var values = new UnityEngine.Object[3];
            for (int i = 0; i < 3; i++)
            {
                var row = Surface(options, "Volume " + i, 150, 218 + i * 92, 980, 76);
                Label(row, "Caption", new[] { "SOUND EFFECTS", "VEHICLE", "MUSIC" }[i], 20, 20, 240, 36, 24);
                var sliderRoot = Group(row, "Slider", 300, 20, 560, 36); var slider = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
                Line(sliderRoot, "Track", 0, 15, 560, 6).GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, .22f);
                var fillArea = Group(sliderRoot, "Fill Area", 0, 15, 560, 6); var fill = Line(fillArea, "Fill", 0, 0, 560, 6); slider.fillRect = fill;
                var handleArea = Group(sliderRoot, "Handle Area", 0, 0, 560, 36); var handle = Line(handleArea, "Handle", 0, 0, 12, 36); handle.GetComponent<UnityEngine.UI.Image>().color = Color.white;
                slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>(); slider.minValue = 0; slider.maxValue = 1;
                sliders[i] = slider; values[i] = Label(row, "Value", "100%", 890, 20, 70, 36, 24);
            }
            SetArray(view, "volumes", sliders); SetArray(view, "volumeValues", values); Set(view, "optionsBack", Button(options, "audio-back", "BACK", 150, 526, 980, 66));
            var error = Backdrop(root.transform, "Save Error"); Set(view, "error", error.gameObject);
            var errorPanel = Surface(error, "Message", 170, 170, 940, 380);
            Label(errorPanel, "Heading", "RESULT NOT SAVED", 32, 24, 876, 66, 52, true);
            var errorText = Label(errorPanel, "Details", "Retry saving to keep your rewards.", 32, 118, 876, 126, 26); errorText.textWrappingMode = TextWrappingModes.Normal; Set(view, "errorDetails", errorText);
            Set(view, "retrySave", Button(errorPanel, "retry-save", "RETRY SAVING", 32, 280, 430, 66)); Set(view, "leaveError", Button(errorPanel, "leave-without-saving", "LEAVE WITHOUT SAVING", 478, 280, 430, 66));
            pause.gameObject.SetActive(false); options.gameObject.SetActive(false); error.gameObject.SetActive(false);
            Hud(view);
            FormulaMenuAssets.Current.racePrefab = PrefabUtility.SaveAsPrefabAsset(root, RacePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Loading(); EditorUtility.SetDirty(FormulaMenuAssets.Current);
    }
    private static void Hud(FormulaRaceView view)
    {
        var hud = view.transform.Find("HUD");
        var session = HudPanel(hud, "Session", 32, 28, 240, 104);
        Set(view, "eventName", Label(session, "Event", "QUICK RACE", 16, 3, 208, 26, 18));
        Set(view, "position", Label(session, "Position", "P1", 16, 26, 92, 54, 52, true));
        Set(view, "participantCount", Label(session, "Participants", "/ 4", 94, 46, 46, 28, 20));
        Label(session, "Lap Caption", "LAP", 154, 27, 70, 26, 18);
        Set(view, "lap", Label(session, "Lap", "1 / 3", 154, 48, 70, 36, 30, true));
        Set(view, "lapStatus", Label(session, "Lap Status", "", 16, 80, 208, 24, 18));

        var timing = HudPanel(hud, "Timing", 976, 78, 272, 128); Set(view, "timingRoot", timing);
        Set(view, "timingCaption", Label(timing, "Session Caption", "CURRENT LAP", 16, 3, 240, 26, 18));
        Set(view, "lapTime", Label(timing, "Lap Time", "00:00.000", 16, 24, 240, 52, 44, true));
        var oldBest = hud.Find("Personal Best");
        if (oldBest != null)
        {
            var text = oldBest.Find("Best Lap");
            if (text != null) text.SetParent(timing, false);
            UnityEngine.Object.DestroyImmediate(oldBest.gameObject);
        }
        var unusedCaption = timing.Find("Lap Caption");
        if (unusedCaption != null) UnityEngine.Object.DestroyImmediate(unusedCaption.gameObject);
        Set(view, "bestLap", Label(timing, "Best Lap", "FIRST TIMED LAP", 16, 74, 240, 28, 18));
        Set(view, "sessionTime", Label(timing, "Session Time", "", 16, 101, 240, 27, 18));
        Set(view, "deltaCaption", Label(timing, "Delta Caption", "AWAITING SPLIT", 16, 101, 164, 27, 18));
        var delta = Label(timing, "Delta", "", 180, 98, 76, 30, 22); delta.alignment = TextAlignmentOptions.MidlineRight; Set(view, "delta", delta);

        var pauseButton = Button(hud, "race-pause", "PAUSE", 1096, 28, 152, 34); Set(view, "pauseButton", pauseButton);
        Format(pauseButton.GetComponentInChildren<TMP_Text>(true), 64, 0, 78, 34, 18);
        var key = Group(pauseButton.transform, "Key", 8, 5, 48, 24);
        var keyImage = key.GetComponent<UnityEngine.UI.Image>();
        if (keyImage == null) keyImage = key.gameObject.AddComponent<UnityEngine.UI.Image>();
        keyImage.color = new Color(1, 1, 1, .10f); keyImage.raycastTarget = false;
        var hint = Label(key, "Hint", "ESC", 0, 0, 48, 24, 18); hint.alignment = TextAlignmentOptions.Center; Set(view, "pauseHint", hint);

        var goal = HudPanel(hud, "Session Goal", 32, 144, 240, 68); Set(view, "careerGoal", goal.gameObject);
        Set(view, "goalCaption", Label(goal, "Caption", "PASS TARGET", 16, 3, 208, 26, 18));
        Set(view, "goalValue", Label(goal, "Target", "ONE VALID LAP", 16, 27, 208, 36, 24, true));
        Set(view, "goalProgress", Label(goal, "Progress", "STAY WITHIN TRACK LIMITS", 16, 57, 232, 28, 18));
        goal.Find("Progress").gameObject.SetActive(false);
        var ranking = Group(hud, "Standings", 32, 224, 240, 112); Set(view, "standingsRoot", ranking.gameObject);
        var rows = new UnityEngine.Object[4]; var highlights = new UnityEngine.Object[4];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Group(ranking, "Driver " + i, 0, i * 28, 240, 26);
            var image = row.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = row.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, 1); image.raycastTarget = false; highlights[i] = image;
            // TMP Ellipsis also clips by line height; reserve font metrics at 115% text size.
            var name = Label(row, "Name", (i + 1).ToString("00") + "   DRIVER", 16, -5, 208, 36, 18);
            name.overflowMode = TextOverflowModes.Ellipsis; rows[i] = name;
        }
        SetArray(view, "standings", rows); SetArray(view, "standingHighlights", highlights);

        var minimap = HudPanel(hud, "Minimap", 32, 528, 240, 160);
        Label(minimap, "Caption", "CIRCUIT MAP", 16, 4, 232, 27, 18).gameObject.SetActive(false);
        var routeRoot = Group(minimap, "Circuit", 12, 12, 216, 136);
        var route = routeRoot.GetComponent<FormulaRaceMinimap>();
        if (route == null) route = routeRoot.gameObject.AddComponent<FormulaRaceMinimap>();
        route.color = Color.white; route.raycastTarget = false; Set(view, "minimap", route);
        var markerRoot = Group(routeRoot, "Cars", 0, 0, 216, 136);
        var markers = markerRoot.GetComponent<FormulaMinimapMarkers>();
        if (markers == null) markers = markerRoot.gameObject.AddComponent<FormulaMinimapMarkers>();
        markers.color = Color.white; markers.raycastTarget = false; Set(route, "markers", markers);
        Line(minimap, "Player Key", 16, 190, 6, 6).GetComponent<UnityEngine.UI.Image>().color = FormulaUI.Accent;
        Label(minimap, "Player Legend", "YOU", 30, 181, 60, 27, 18).color = FormulaUI.Accent;
        var rivalsLegend = Group(minimap, "Rivals Legend", 106, 181, 140, 27); Set(route, "rivalsLegend", rivalsLegend.gameObject);
        Line(rivalsLegend, "Key", 0, 9, 6, 6).GetComponent<UnityEngine.UI.Image>().color = Color.white;
        Label(rivalsLegend, "Label", "RIVALS", 14, 0, 126, 27, 18);
        minimap.Find("Player Key").gameObject.SetActive(false);
        minimap.Find("Player Legend").gameObject.SetActive(false);
        rivalsLegend.gameObject.SetActive(false);

        var proximity = Group(hud, "Proximity", 400, 410, 480, 72);
        var left = HudPanel(proximity, "Car Left", 0, 0, 36, 72);
        left.GetComponent<FormulaRacePanelGraphic>().ConfigureProximity(false);
        var right = HudPanel(proximity, "Car Right", 444, 0, 36, 72);
        right.GetComponent<FormulaRacePanelGraphic>().ConfigureProximity(true);
        foreach (var side in new[] { left, right })
        {
            var oldLabel = side.Find("Signal"); if (oldLabel != null) oldLabel.gameObject.SetActive(false);
        }
        Set(view, "carLeft", left.gameObject); Set(view, "carRight", right.gameObject);
        left.gameObject.SetActive(false); right.gameObject.SetActive(false);

        var target = HudPanel(hud, "Target", 32, 428, 344, 88);
        var notification = target.GetComponent<CanvasGroup>();
        if (notification == null) notification = target.gameObject.AddComponent<CanvasGroup>();
        notification.alpha = 0; notification.interactable = notification.blocksRaycasts = false; Set(view, "notification", notification);
        var heading = Label(target, "Lap Validity", "SESSION OBJECTIVE", 16, 3, 312, 26, 18); Set(view, "validity", heading);
        var body = Label(target, "Objective", "Complete a valid lap", 16, 28, 312, 56, 20);
        body.textWrappingMode = TextWrappingModes.Normal;
        Set(view, "objective", body);
        var countdown = Label(hud, "Start Message", "", 400, 248, 480, 140, 88, true);
        countdown.alignment = TextAlignmentOptions.Center; Set(view, "startMessage", countdown);

        var car = HudPanel(hud, "Vehicle", 976, 572, 272, 116);
        var lights = Group(car, "Shift Lights", 12, 12, 248, 8); var lamps = new UnityEngine.Object[12];
        for (int i = 0; i < lamps.Length; i++)
        {
            var image = Line(lights, "Light " + i, i * 21, 0, 17, 8).GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1, 1, 1, .16f); lamps[i] = image;
        }
        SetArray(view, "shiftLights", lamps);
        var speed = Label(car, "Speed", "0", 12, 20, 122, 68, 72, true); speed.alignment = TextAlignmentOptions.MidlineRight; Set(view, "speed", speed);
        Label(car, "Unit", "KM/H", 144, 60, 46, 26, 18);
        var gear = Label(car, "Gear", "N", 208, 21, 48, 68, 60, true); gear.alignment = TextAlignmentOptions.Center; Set(view, "gear", gear);
        Label(car, "Gear Caption", "GEAR", 208, 85, 48, 26, 18);
        var separator = Line(car, "Gear Divider", 198, 28, 1, 72).GetComponent<UnityEngine.UI.Image>(); separator.color = new Color(1, 1, 1, .18f);
        Set(view, "rpm", Label(car, "RPM", "0 RPM", 12, 84, 180, 26, 18));
        Set(view, "revFill", Fill(car, "Rev Strip", 12, 111, 180, 3));

        var overview = view.transform.Find("Pause/Overview");
        Place(overview, 320, 68, 640, 584);
        Surface(view.transform.Find("Pause"), "Overview", 320, 68, 640, 584);
        Format(overview.Find("Heading").GetComponent<TMP_Text>(), 30, 55, 580, 60, 48, true);
        var summary = Label(overview, "Session Objective", "PASS TARGET / ONE VALID LAP", 30, 126, 580, 112, 22);
        summary.textWrappingMode = TextWrappingModes.Normal; Set(view, "pauseObjective", summary);
        string[] actions = { "resume", "restart", "audio", "leave" };
        string[] labels = { "RESUME", "RESTART SESSION", "AUDIO", "LEAVE SESSION" };
        for (int i = 0; i < actions.Length; i++) Button(overview, actions[i], labels[i], 30, 252 + i * 68, 580, 60);
    }
    private static RectTransform HudPanel(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = Group(parent, name, x, y, width, height);
        var oldSurface = rect.Find("Surface");
        if (oldSurface != null) UnityEngine.Object.DestroyImmediate(oldSurface.gameObject);
        var graphic = rect.GetComponent<FormulaRacePanelGraphic>();
        if (graphic == null) graphic = rect.gameObject.AddComponent<FormulaRacePanelGraphic>();
        graphic.color = Color.white; graphic.raycastTarget = false;
        return rect;
    }
    private static RectTransform Backdrop(Transform p, string name)
    {
        var root = Group(p, name, 0, 0, 1280, 720); var image = root.GetComponent<UnityEngine.UI.Image>() ?? root.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(0, 0, 0, .96f); image.raycastTarget = true; return root;
    }
    private static void Loading()
    {
        var root = new GameObject("Formula Loading", typeof(RectTransform), typeof(FormulaLoadingView)); Place(root.transform, 0, 0, 1280, 720);
        try
        {
            var view = root.GetComponent<FormulaLoadingView>(); var background = Backdrop(root.transform, "Backdrop");
            background.GetComponent<UnityEngine.UI.Image>().color = new Color32(8, 5, 6, 255);
            Label(background, "Brand", "FORMULA G27", 70, 65, 1140, 100, 76, true);
            Set(view, "caption", Label(background, "Caption", "PREPARING YOUR SESSION", 70, 304, 1140, 32, 22));
            Set(view, "circuit", Label(background, "Circuit", "GRAND PRIX CIRCUIT", 70, 349, 1140, 80, 58, true));
            Set(view, "progress", Label(background, "Progress", "LOADING  /  0%", 70, 534, 1140, 38, 26)); Set(view, "fill", Fill(background, "Progress Bar", 70, 590, 1140, 6));
            Label(background, "Tip", "Stay within track limits and pass every checkpoint to record a valid lap.", 70, 637, 1140, 32, 22);
            FormulaMenuAssets.Current.loadingPrefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Formula/Resources/Formula/UI/FormulaLoading.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
