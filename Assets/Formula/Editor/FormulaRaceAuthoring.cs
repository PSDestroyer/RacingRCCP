using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;
using static FormulaExperienceBuilder;

public static class FormulaRaceAuthoring
{
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
            FormulaMenuAssets.Current.racePrefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Formula/Resources/Formula/UI/FormulaRace.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Loading(); EditorUtility.SetDirty(FormulaMenuAssets.Current);
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
