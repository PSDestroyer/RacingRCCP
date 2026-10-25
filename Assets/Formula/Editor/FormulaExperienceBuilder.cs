using System;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;

// Explicit authoring pass: the shipped prefab is the UI, including in Edit Mode.
public static class FormulaExperienceBuilder
{
    [MenuItem("Tools/Formula Career/Correct Review Issues")]
    public static void CorrectReviewIssues()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            var view = root.GetComponent<FormulaMenuView>(); Settings(view.Settings); ReserveAccessibilityLayout(view);
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_REVIEW_ISSUES_CORRECTED");
    }
    [MenuItem("Tools/Formula Career/Integrate Experience Review")]
    public static void Apply()
    {
        FormulaMenuUnifiedBuilder.Apply();
        BuildRace();
        AssetDatabase.SaveAssets();
        Debug.Log("FORMULA_EXPERIENCE_REVIEW_INTEGRATED");
    }
    public static void Style(FormulaMenuView view)
    {
        view.transform.Find("Footer/Input Hints").GetComponent<TMP_Text>().text = "Enter · Select";
        FormulaMenuAssets.Current.bodyFont.TryAddCharacters("›"); EditorUtility.SetDirty(FormulaMenuAssets.Current.bodyFont);
        if (view.GetComponent<FormulaPresentation>() == null) view.gameObject.AddComponent<FormulaPresentation>();
        foreach (var text in view.GetComponentsInChildren<TMP_Text>(true))
            if (text.fontSize < 18 && text.fontSize > 0) { text.fontSize = text.fontSizeMin = text.fontSizeMax = 18; }
        foreach (var name in new[] { "Header/Formula Logo", "Screens/Garage/Upgrade/Upgrade Logo", "Screens/Career/Figma Career Header/Career Logo" })
        { var logo = view.transform.Find(name); if (logo != null) Place(logo, 23, -6, 322, 110); }
        foreach (var button in view.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            if (button.transform.Find("Back Prompt") != null || button.transform.Find("B Button") != null)
            {
                var bitmap = button.transform.Find("Back Prompt") ?? button.transform.Find("B Button");
                UnityEngine.Object.DestroyImmediate(bitmap.gameObject);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                Place(button.transform, 1080, 33, 170, 40); Format(label, 10, 0, 150, 40, 22); label.text = "Esc · Back";
                if (label.GetComponent<FormulaDevicePrompt>() == null) label.gameObject.AddComponent<FormulaDevicePrompt>();
                FormulaMenuTheme.StyleButton(button);
            }
        var shade = view.transform.Find("Screen Shade"); Place(shade, 0, 100, 1280, 524); shade.SetAsLastSibling(); shade.SetSiblingIndex(view.Screens.GetSiblingIndex());
        Career(view.Career); Briefing(view.Dialogs); Garage(view.Garage); Quick(view.Quick); Profile(view.Profile); Settings(view.Settings);
        FormulaHomeReferenceBuilder.StyleChrome(view);
        ReserveAccessibilityLayout(view);
    }
    private static void ReserveAccessibilityLayout(FormulaMenuView view)
    {
        var upgrade = view.Garage.Upgrade;
        var rows = new SerializedObject(upgrade).FindProperty("levels");
        for(int i=0;i<rows.arraySize;i++)
        {
            var row=rows.GetArrayElementAtIndex(i); var button=(UnityEngine.UI.Button)row.FindPropertyRelative("button").objectReferenceValue;
            Place(button.transform,0,i*86,237,82); FormulaMenuTheme.StyleButton(button);
            Place(((TMP_Text)row.FindPropertyRelative("status").objectReferenceValue).transform,18,44,170,38);
        }
        Place(upgrade.transform.Find("Level Details"),30,545,470,78);
        var setup=view.Garage.transform.Find("Setup"); var statistics=setup.Find("RCCP Statistics");
        Place(statistics,520,410,730,194); Place(statistics.Find("Comparison Panel"),0,0,730,194);
        var explanation=statistics.Find("Setup Explanation").GetComponent<TMP_Text>();
        explanation.text="Less downforce: more straight-line speed.\nMore downforce: more fast-corner grip."; Place(explanation.transform,20,128,690,60);
        foreach(Transform card in setup.Find("Setup Cards"))
        {
            var description=card.Find("Setup Effect").GetComponent<TMP_Text>();
            if(card.name=="setup-balanced") description.text="Balanced cornering and speed.";
            Place(card.Find("Setup Status"),20,77,300,34);
        }
        var carousel=view.Garage.transform.Find("Livree/Livery Cards"); Place(carousel,30,487,1220,136);
        foreach(var card in carousel.GetComponentsInChildren<FormulaLiveryCard>(true))
        {
            var rect=(RectTransform)card.transform; rect.sizeDelta=new Vector2(145,136); FormulaMenuTheme.StyleButton(card.GetComponent<UnityEngine.UI.Button>());
            Place(card.transform.Find("Ownership"),9,104,127,32);
            var state=card.transform.Find("Ownership").GetComponent<TMP_Text>(); if(state.text=="CAREER REWARD")state.text="REWARD";
        }
        var requirements=Ref<TMP_Text>(view.Dialogs,"liveryRequirements"); requirements.fontSize=requirements.fontSizeMin=requirements.fontSizeMax=20;
        foreach(Transform row in view.Profile.transform.Find("Career Medals"))
        {
            var rect=(RectTransform)row; rect.sizeDelta=new Vector2(830,69);
            var surface=row.Find("Upgrade Surface"); if(surface!=null)Place(surface,0,0,830,69);
            Place(row.Find("Event"),20,-2,570,40); Place(row.Find("Personal Best"),20,36,570,33);
        }
        ReserveSingleLineText(view.gameObject);
    }
    private static void ReserveSingleLineText(GameObject root)
    {
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.font==FormulaMenuAssets.Current.titleFont || text.fontSize>32 || text.text.Contains("\n"))continue;
            var rect=text.rectTransform; float height=Mathf.Max(rect.sizeDelta.y,text.fontSize*1.8f), extra=height-rect.sizeDelta.y;
            rect.sizeDelta=new Vector2(rect.sizeDelta.x,height); rect.anchoredPosition+=Vector2.up*(extra*.5f);
        }
    }
    private static void Career(FormulaCareerView view)
    {
        var rail = view.transform.Find("Figma Scroll Rail"); if (rail != null) rail.gameObject.SetActive(false);
        foreach (var card in view.GetComponentsInChildren<FormulaEventCard>(true))
        {
            var root = card.transform;
            var oldAction = root.Find("Figma To Race"); if (oldAction != null) oldAction.gameObject.SetActive(false);
            var title = Ref<TMP_Text>(card, "eventName"); title.text = FormulaCatalog.Current.Event(card.EventId).name.ToUpperInvariant(); Format(title, 20, 53, 356, 46, 38, true); title.enableAutoSizing = true; title.fontSizeMin = 32; title.fontSizeMax = 38;
            Format(Ref<TMP_Text>(card, "stage"), 280, 12, 98, 26, 18);
            var track = Ref<TMP_Text>(card, "trackName"); track.text = FormulaAssets.Current.FindTrack(FormulaCatalog.Current.Event(card.EventId).trackId).displayName; Format(track, 20, 100, 356, 28, 20);
            Format(Ref<TMP_Text>(card, "state"), 20, 139, 180, 30, 24);
            Place(root.Find("Circuit Outline"), 255, 124, 120, 48);
            var icon = root.GetComponentInChildren<FormulaMissionIcon>(true); if (icon != null) Place(icon.transform, 20, 15, 28, 28);
            Set(card, "availability", Label(root, "Availability", card.EventId == "g01" ? "NEXT EVENT" : "LOCKED", 60, 13, 215, 28, 19));
            Set(card, "earnedMedal", Label(root, "Earned Medal", "", 20, 177, 160, 28, 18));
            Set(card, "cardAction", Label(root, "Card Action", card.EventId == "g01" ? "VIEW EVENT  ›" : "VIEW REQUIREMENT  ›", 182, 177, 196, 28, 18));
            root.GetComponent<FormulaMenuFocus>().StyleFigma(root.Find("Upgrade Focus").GetComponent<FormulaPanelGraphic>(), null, root.Find("Upgrade Normal").GetComponent<FormulaPanelGraphic>());
        }
    }
    private static void Briefing(FormulaMenuDialogs view)
    {
        var root = view.transform.Find("Race Briefing"); root.GetComponent<UnityEngine.UI.Image>().color = new Color32(8, 5, 6, 255);
        var goal = root.Find("Goal").GetComponent<TMP_Text>(); Format(goal, 338, 151, 690, 44, 28); goal.enableAutoSizing = true; goal.fontSizeMin = 24; goal.fontSizeMax = 28;
        var note = root.Find("Note").GetComponent<TMP_Text>(); Format(note, 338, 402, 690, 70, 19); note.textWrappingMode = TextWrappingModes.Normal;
        note.text = "Rewards include unclaimed medal bonuses.\nValid laps require staying within track limits and passing every checkpoint.";
        root.Find("Circuit").GetComponent<UnityEngine.UI.Image>().color = new Color32(8, 5, 6, 255);
        foreach (string name in new[] { "Objective Heading", "Targets Heading", "Reward" }) root.Find(name).gameObject.SetActive(true);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.fontSize < 18) text.fontSize = 18;
    }
    private static void Garage(FormulaGarageView garage)
    {
        var upgrade = garage.Upgrade;
        var serialized = new SerializedObject(upgrade); var levels = serialized.FindProperty("levels");
        for (int i = 0; i < levels.arraySize; i++)
        {
            var row = levels.GetArrayElementAtIndex(i); var button = (UnityEngine.UI.Button)row.FindPropertyRelative("button").objectReferenceValue;
            ((UnityEngine.UI.RawImage)row.FindPropertyRelative("surface").objectReferenceValue).enabled = false;
            FormulaMenuTheme.StyleButton(button);
            var value = (TMP_Text)row.FindPropertyRelative("value").objectReferenceValue; Format(value, 18, 6, 158, 40, 30, true);
            var duplicate = button.transform.Find("Level Name"); if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            Label(button.transform, "Level", i == 0 ? "STOCK" : "LVL " + i, 165, 10, 60, 28, 18);
            var status = (TMP_Text)row.FindPropertyRelative("status").objectReferenceValue; Format(status, 18, 50, 170, 29, 21);
            var pricePlate = Group(button.transform, "State Backplate", 12, 48, 213, 32); var ink = pricePlate.GetComponent<UnityEngine.UI.Image>() ?? pricePlate.gameObject.AddComponent<UnityEngine.UI.Image>(); ink.color = new Color32(8, 5, 6, 255); ink.raycastTarget = false;
            pricePlate.SetAsLastSibling(); status.transform.SetAsLastSibling();
            var coin = (UnityEngine.UI.RawImage)row.FindPropertyRelative("coin").objectReferenceValue; Place(coin.transform, 192, 54, 20, 20); coin.color = new Color(1, .8f, .2f, .65f); coin.transform.SetAsLastSibling();
        }
        foreach (var stats in garage.GetComponentsInChildren<FormulaStatisticsView>(true))
        {
            bool setup = stats.transform.IsChildOf(garage.transform.Find("Setup"));
            var root = stats.transform; Place(root, 520, setup ? 410 : 405, 730, setup ? 180 : 210);
            var art = root.Find("Statistics Surface"); if (art != null) art.gameObject.SetActive(false);
            Surface(root, "Comparison Panel", 0, 0, 730, setup ? 180 : 210).SetAsFirstSibling();
            Set(stats, "comparison", Label(root, "Comparison Legend", "CURRENT CONFIGURATION", 20, 8, 690, 26, 18));
            string[] fields = { "torque", "spring", "aero", "brakes" }; string[] ids = { "engine", "suspension", "aero", "brakes" };
            var data = new SerializedObject(stats); var rows = data.FindProperty("development");
            for (int i = 0; i < fields.Length; i++)
            {
                var value = Ref<TMP_Text>(stats, fields[i]); var bars = root.Find("Stats-" + ids[i]);
                TMP_Text caption = null;
                for (int j = 0; j < rows.arraySize; j++) if (rows.GetArrayElementAtIndex(j).FindPropertyRelative("branchId").stringValue == ids[i]) caption = (TMP_Text)rows.GetArrayElementAtIndex(j).FindPropertyRelative("label").objectReferenceValue;
                if (caption == null) continue;
                value.gameObject.SetActive(!setup || i == 2); caption.gameObject.SetActive(!setup || i == 2); if (bars != null) bars.gameObject.SetActive(!setup);
                float y = setup ? 50 : 40 + i * 33;
                Format(caption, 20, y, 150, 30, 18); Format(value, setup ? 260 : 320, y, setup ? 450 : 390, 30, 22); value.alignment = TextAlignmentOptions.MidlineRight;
                if (bars != null) Place(bars, 173, y + 13, 140, 6);
            }
            var drag = Ref<TMP_Text>(stats, "drag"); Format(drag, 20, setup ? 98 : 175, 690, 28, 19);
            if (setup) { var effect = Label(root, "Setup Explanation", "Less downforce improves speed on straights. More downforce improves fast-corner grip.", 20, 133, 690, 40, 19); effect.textWrappingMode = TextWrappingModes.Normal; }
        }
        var details = garage.transform.Find("Livree/Livery Details"); var policy = details.Find("Funds"); if (policy != null) policy.GetComponent<TMP_Text>().fontSize = 19;
    }
    private static void Quick(FormulaQuickView view)
    {
        if (FormulaAssets.Current.tracks.Length > 2) { FormulaCircuitSelectionBuilder.Style(view); return; }
        var list = view.transform.Find("Event Cards"); var cards = list.GetComponentsInChildren<FormulaEventCard>(true);
        var kept = cards.Take(2).ToArray(); SetArray(view, "events", kept.Cast<UnityEngine.Object>().ToArray());
        foreach (var card in cards.Skip(2)) UnityEngine.Object.DestroyImmediate(card.gameObject);
        var modes = new UnityEngine.Object[3]; var markers = new UnityEngine.Object[3];
        string[] names = { "RACE", "TIME ATTACK", "PRACTICE" };
        Label(view.transform, "Mode Heading", "01  /  CHOOSE A MODE", 30, 115, 1000, 32, 22);
        for (int i = 0; i < 3; i++) { var b = Button(view.transform, "mode-" + i, names[i], 30 + i * 410, 158, 390, 68); modes[i] = b; markers[i] = Line(b.transform, "Active Mode", 18, 65, 354, 3).gameObject; }
        SetArray(view, "modes", modes); SetArray(view, "modeMarkers", markers);
        Label(view.transform, "Circuit Heading", "02  /  CHOOSE A CIRCUIT", 30, 248, 1000, 32, 22);
        for (int i = 0; i < kept.Length; i++)
        {
            var card = kept[i]; var root = card.transform; Place(root, 30 + i * 620, 294, 600, 154); FormulaMenuTheme.StyleButton(root.GetComponent<UnityEngine.UI.Button>());
            var mode = root.Find("Mode").GetComponent<TMP_Text>(); mode.text = FormulaAssets.Current.tracks[i].displayName.ToUpperInvariant(); Format(mode, 20, 12, 440, 50, 38, true);
            var circuit = root.Find("Circuit").GetComponent<TMP_Text>(); circuit.text = "REVIEW SESSION  ›"; Format(circuit, 20, 101, 360, 34, 22);
            var policy = root.Find("Reward Rule"); if (policy != null) policy.gameObject.SetActive(false);
            Place(root.Find("Circuit Outline"), 410, 50, 160, 80);
        }
        Set(view, "sessionDetails", Label(view.transform, "Session Details", "RACE  /  3 LAPS  /  3 AI DRIVERS\nYour equipped upgrades and setup are used.", 30, 477, 1220, 78, 24));
        Ref<TMP_Text>(view, "sessionDetails").textWrappingMode = TextWrappingModes.Normal;
        var rule = view.transform.Find("Career Policy").GetComponent<TMP_Text>(); rule.text = "Quick sessions do not award career credits or XP."; Format(rule, 30, 573, 1220, 32, 19);
    }
    private static void Profile(FormulaProfileView view)
    {
        var summary = view.transform.Find("Pilot Summary"); var progress = summary.Find("Progress").GetComponent<TMP_Text>(); Format(progress, 24, 104, 312, 139, 26); progress.lineSpacing = 3;
        Set(view, "experience", Label(summary, "Experience", "0 / 3,000 XP", 24, 250, 312, 30, 18));
        var fill = Fill(summary, "XP", 24, 289, 312, 5); Set(view, "xpFill", fill);
        var reward = Label(summary, "Next Reward", "NEXT LIVERY", 24, 321, 312, 90, 20); reward.textWrappingMode = TextWrappingModes.Normal; Set(view, "nextReward", reward);
        var data = new SerializedObject(view); var medals = data.FindProperty("medals");
        for (int i = 0; i < medals.arraySize; i++)
        {
            var row = medals.GetArrayElementAtIndex(i); var medal = (TMP_Text)row.FindPropertyRelative("medal").objectReferenceValue; var root = medal.transform.parent;
            Format(root.Find("Event").GetComponent<TMP_Text>(), 20, 0, 570, 34, 24);
            row.FindPropertyRelative("record").objectReferenceValue = Label(root, "Personal Best", "BEST LAP  —", 20, 33, 570, 26, 18);
            Format(medal, 610, 10, 200, 38, 23);
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Settings(FormulaSettingsView view)
    {
        var overview = view.transform.Find("Assists"); var open = overview.Find("existing-settings"); Place(open, 600, 194, 650, 78); FormulaMenuTheme.StyleButton(open.GetComponent<UnityEngine.UI.Button>());
        Format(open.GetComponentInChildren<TMP_Text>(true), 76, 0, 554, 78, 32, true);
        var icon = open.Find("Figma Icon"); if (icon != null) Place(icon, 20, 23, 32, 32);
        foreach (string name in new[] { "Description", "Action" }) { var child = open.Find(name); if (child != null) child.gameObject.SetActive(false); }
        var details = view.transform.Find("Audio and Controls"); var tabs = details.Find("Tabs"); Place(tabs, 30, 115, 1220, 42);
        var audio = tabs.Find("settings-audio"); var controls = tabs.Find("settings-controls");
        Place(audio, 0, 0, 230, 42); Place(controls, 247, 0, 230, 42);
        foreach (var existing in new[] { audio, controls }) { var marker = existing.Find("Active Section"); if (marker != null) Place(marker, 18, 40, 194, 2); Format(existing.GetComponentInChildren<TMP_Text>(true), 18, 0, 194, 42, 23); FormulaMenuTheme.StyleButton(existing.GetComponent<UnityEngine.UI.Button>()); }
        Set(view, "audioMarker", audio.Find("Active Section").gameObject); Set(view, "controlsMarker", controls.Find("Active Section").gameObject);
        string[] sections = { "DISPLAY", "GRAPHICS", "ACCESSIBILITY" }; var shortcuts = new UnityEngine.Object[3]; var newTabs = new UnityEngine.Object[3]; var pages = new UnityEngine.Object[3]; var markers = new UnityEngine.Object[3];
        var options = new UnityEngine.Object[8]; var labels = new UnityEngine.Object[8]; int[] groups = { 0, 0, 0, 1, 1, 2, 2, 2 }; int[] counts = new int[3];
        string[] captions = { "DISPLAY MODE", "SAFE AREA", "FRAME LIMIT", "QUALITY", "V-SYNC", "TEXT SIZE", "REDUCED MOTION", "HIGH CONTRAST" };
        string[] descriptions = { "Toggle between full screen and windowed display.", "Keep controls away from the edges of your display.", "Choose the frame-rate cap. V-sync can override this limit.", "Cycle the graphics presets configured for this build.", "Synchronise frame presentation with your display.", "Increase body text up to 115%.", "Use instant screen and selection changes.", "Darken the garage behind menus for stronger text contrast." };
        for (int i = 0; i < 3; i++)
        {
            shortcuts[i] = Button(overview, "open-" + sections[i], sections[i], 600, 290 + i * 94, 650, 78);
            var tab = Button(tabs, "tab-" + sections[i], sections[i], 494 + i * 247, 0, 232, 42); newTabs[i] = tab; Format(tab.GetComponentInChildren<TMP_Text>(true), 12, 0, 208, 42, 21);
            markers[i] = Line(tab.transform, "Active Preference", 12, 40, 208, 2).gameObject;
            var page = Group(details, sections[i], 0, 0, 1280, 624); pages[i] = page.gameObject; page.gameObject.SetActive(false);
        }
        for (int i = 0; i < options.Length; i++)
        {
            var parent = ((GameObject)pages[groups[i]]).transform; int row = counts[groups[i]]++;
            var b = Button(parent, "preference-" + i, captions[i], 30, 190 + row * 126, 1220, 106); options[i] = b;
            var label = b.GetComponentInChildren<TMP_Text>(true); Format(label, 20, 8, 1180, 40, 29); labels[i] = label;
            Label(b.transform, "Description", descriptions[i], 20, 57, 1180, 32, 21);
        }
        SetArray(view, "preferenceTabs", newTabs); SetArray(view, "preferenceShortcuts", shortcuts); SetArray(view, "preferencePages", pages); SetArray(view, "preferenceMarkers", markers); SetArray(view, "preferenceButtons", options); SetArray(view, "preferenceLabels", labels);
        var waiting = details.Find("Controls/Waiting for Input"); if (waiting != null) waiting.GetComponent<UnityEngine.UI.Image>().color = new Color32(8, 5, 6, 255);
    }
    public static void StyleResults(GameObject root)
    {
        var view = root.GetComponent<FormulaResultsView>();
        Set(view, "resultCaption", root.transform.Find("Result Caption").GetComponent<TMP_Text>());
        Place(root.transform.Find("Header/Formula Logo"), 23, -6, 322, 110);
        Format(Ref<TMP_Text>(view, "eventName"), 30, 144, 1220, 60, 44, true);
        Format(Ref<TMP_Text>(view, "medal"), 30, 208, 580, 78, 64, true);
        var metricCaptions = new UnityEngine.Object[4];
        for (int i = 0; i < 4; i++)
        {
            var panel = root.transform.Find("Result " + new[] { "elapsed", "bestLap", "credits", "xp" }[i]);
            Place(panel, i < 2 ? 30 + i * 310 : 650 + (i - 2) * 310, i < 2 ? 320 : 208, 290, i < 2 ? 84 : 116);
            foreach (var surface in panel.GetComponentsInChildren<FormulaPanelGraphic>(true)) Place(surface.transform, 0, 0, 290, i < 2 ? 84 : 116);
            var caption = panel.Find("Caption").GetComponent<TMP_Text>(); Format(caption, 16, 4, 258, 26, 18); metricCaptions[i] = caption;
            Format(panel.Find("Value").GetComponent<TMP_Text>(), 16, 30, 258, i < 2 ? 54 : 86, i < 2 ? 44 : 52, true);
        }
        SetArray(view, "metricCaptions", metricCaptions);
        var progression = Group(root.transform, "Progression", 650, 348, 600, 52); Set(view, "progressionRoot", progression);
        foreach (string name in new[] { "XP Progress", "Experience Bar" })
        {
            var previous = root.transform.Find(name); if (previous != null) previous.SetParent(progression, false);
        }
        Set(view, "progress", Label(progression, "XP Progress", "LEVEL 1  /  0 OF 3,000 XP", 0, 0, 600, 28, 19));
        Set(view, "xpFill", Fill(progression, "Experience Bar", 0, 44, 600, 4));
        var target = Label(root.transform, "Next Medal", "NEXT MEDAL", 30, 482, 1000, 40, 22); target.textWrappingMode = TextWrappingModes.Normal; Set(view, "target", target);
        Format(root.transform.Find("Level Bonus").GetComponent<TMP_Text>(), 650, 402, 390, 28, 19);
        Format(root.transform.Find("Unlocks").GetComponent<TMP_Text>(), 30, 414, 580, 62, 20);
        root.transform.Find("Unlocks").GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
        var art = Group(root.transform, "Reward Livery", 1050, 414, 200, 100); var thumbnail = art.GetComponent<UnityEngine.UI.RawImage>() ?? art.gameObject.AddComponent<UnityEngine.UI.RawImage>(); thumbnail.raycastTarget = false; art.gameObject.SetActive(false); Set(view, "rewardLivery", thumbnail);
        var next = Label(root.transform, "Next Event", "", 650, 432, 390, 44, 18); next.textWrappingMode = TextWrappingModes.Normal; Set(view, "nextContext", next);
    }
    private static void BuildRace() { FormulaRaceAuthoring.Build(); }
    public static RectTransform Group(Transform p, string name, float x, float y, float w, float h) { var r = p.Find(name) as RectTransform ?? FormulaUI.Rect(p, name, x, y, w, h); Place(r, x, y, w, h); return r; }
    public static RectTransform Surface(Transform p, string name, float x, float y, float w, float h) { var r = Group(p, name, x, y, w, h); FormulaMenuTheme.Surface(r, "Surface", 0, 0, w, h).transform.SetAsFirstSibling(); return r; }
    public static RectTransform Line(Transform p, string name, float x, float y, float w, float h) { var r = Group(p, name, x, y, w, h); var image = r.GetComponent<UnityEngine.UI.Image>() ?? r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = FormulaUI.Red; image.raycastTarget = false; return r; }
    public static UnityEngine.UI.Image Fill(Transform p, string name, float x, float y, float w, float h) { var track = Line(p, name, x, y, w, h); track.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, .18f); var r = Line(track, "Fill", 0, 0, w, h); var image = r.GetComponent<UnityEngine.UI.Image>(); image.sprite = FormulaMenuAssets.Current.panelSprite; image.type = UnityEngine.UI.Image.Type.Filled; image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; return image; }
    public static TMP_Text Label(Transform p, string name, string text, float x, float y, float w, float h, int size, bool title = false) { var label = p.Find(name)?.GetComponent<TMP_Text>() ?? FormulaMenuStyle.Text(p, text, x, y, w, h, size); label.name = name; label.text = text; Format(label, x, y, w, h, size, title); return label; }
    public static void Format(TMP_Text text, float x, float y, float w, float h, int size, bool title = false) { Place(text.transform, x, y, w, h); text.font = title ? FormulaMenuAssets.Current.titleFont : FormulaMenuAssets.Current.bodyFont; text.fontSize = text.fontSizeMin = text.fontSizeMax = size; text.enableAutoSizing = false; text.fontStyle = FontStyles.Normal; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow; text.alignment = TextAlignmentOptions.MidlineLeft; text.color = Color.white; text.raycastTarget = false; }
    public static UnityEngine.UI.Button Button(Transform p, string name, string label, float x, float y, float w, float h) { var b = p.Find(name)?.GetComponent<UnityEngine.UI.Button>() ?? FormulaMenuStyle.Button(p, name, label, x, y, w, h, () => { }); Place(b.transform, x, y, w, h); var text = b.GetComponentInChildren<TMP_Text>(true); text.text = label; Format(text, 18, 0, w - 36, h, 26); FormulaMenuTheme.StyleButton(b); return b; }
    public static void Place(Transform t, float x, float y, float w, float h) => FormulaMenuTheme.Place((RectTransform)t, x, y, w, h);
    public static T Ref<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object => (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;
    public static void Set(UnityEngine.Object target, string field, UnityEngine.Object value) { var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    public static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values) { var s = new SerializedObject(target); var p = s.FindProperty(field); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; s.ApplyModifiedPropertiesWithoutUndo(); }
}
