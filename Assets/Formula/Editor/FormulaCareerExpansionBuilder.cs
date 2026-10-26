using System;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;

// Adds paging to the authored views, preserving the approved six-card layout.
public static class FormulaCareerExpansionBuilder
{
    [MenuItem("Tools/Formula Career/Integrate Full Career")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            var menu = root.GetComponent<FormulaMenuView>();
            StyleCareer(menu.Career); StyleProfile(menu.Profile);
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("FORMULA_FULL_CAREER_AUTHORED");
    }

    public static void StyleCareer(FormulaCareerView view)
    {
        foreach (string name in new[] { "Figma Scroll Rail", "Figma Scroll Thumb", "Figma Career Footer/Title Rule" })
        {
            var old = view.transform.Find(name); if (old != null) old.gameObject.SetActive(false);
        }
        var paging = Group(view.transform, "Career Paging", 430, 654, 640, 38);
        var previous = Pager(paging, "career-previous", "‹  PREVIOUS", 0, 145);
        var next = Pager(paging, "career-next", "NEXT  ›", 475, 145);
        Set(view, "previousPage", previous); Set(view, "nextPage", next);
        Set(view, "pageLabel", Label(paging, "Event Range", Range(), 155, 0, 310, 38, 20));
    }

    public static void StyleProfile(FormulaProfileView view)
    {
        var data = new SerializedObject(view); var rows = data.FindProperty("medals");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var medal = (TMP_Text)row.FindPropertyRelative("medal").objectReferenceValue;
            var root = (RectTransform)medal.transform.parent;
            var title = root.Find("Event").GetComponent<TMP_Text>();
            row.FindPropertyRelative("eventName").objectReferenceValue = title;
            FormulaMenuTheme.Place(root, 0, i * 69, 830, 63);
            title.text = (i + 1).ToString("00") + "  /  " + FormulaCatalog.Current.careerEvents[i].name;
        }
        var summary = (TMP_Text)data.FindProperty("summary").objectReferenceValue;
        summary.text = summary.text.Replace("/6 EVENTS", "/" + FormulaCatalog.Current.careerEvents.Count + " EVENTS");
        data.ApplyModifiedPropertiesWithoutUndo();
        var paging = Group(view.transform, "Profile Paging", 420, 571, 830, 36);
        var previous = Pager(paging, "profile-previous", "‹  PREVIOUS", 0, 190);
        var next = Pager(paging, "profile-next", "NEXT  ›", 640, 190);
        Set(view, "previousPage", previous); Set(view, "nextPage", next);
        Set(view, "pageLabel", Label(paging, "Event Range", Range(), 200, 0, 430, 36, 20));
        var back = view.GetComponentInParent<FormulaMenuView>().Back;
        previous.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = next, selectOnRight = next, selectOnUp = back, selectOnDown = back };
        next.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = previous, selectOnRight = previous, selectOnUp = back, selectOnDown = back };
    }

    [MenuItem("Tools/Formula Career/Validate Full Career Catalog")]
    public static void Validate()
    {
        var catalog = FormulaCatalog.Current; var tracks = FormulaAssets.Current.tracks;
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (catalog.careerEvents.Count != 22 || tracks.Length != 18 || catalog.careerEvents.Select(e => e.id).Distinct().Count() != 22)
            throw new InvalidOperationException("Career needs 22 stable events across 18 circuits.");
        foreach (var track in tracks)
            if (!catalog.careerEvents.Any(e => e.trackId == track.id) || !File.Exists(track.scenePath) || !enabled.Contains(track.scenePath))
                throw new InvalidOperationException("Circuit missing from Career or enabled build scenes: " + track.id);
        for (int i = 0; i < catalog.careerEvents.Count; i++)
        {
            var entry = catalog.careerEvents[i];
            if (tracks.All(t => t.id != entry.trackId) || FormulaSeasonCalendar.Current.Round(entry.calendarRound) == null)
                throw new InvalidOperationException("Career has an invalid track or calendar binding: " + entry.id);
            if (i > 0 && ((string)entry.unlock["eventId"] != catalog.careerEvents[i - 1].id || (string)entry.unlock["minimumMedal"] != "bronze"))
                throw new InvalidOperationException("Career bronze chain is broken: " + entry.id);
        }
        FormulaProgressionValidation.Run();
        Directory.CreateDirectory("Docs/AI/FormulaCareerExpansion");
        File.WriteAllText("Docs/AI/FormulaCareerExpansion/CatalogValidation.json", JsonConvert.SerializeObject(new
        {
            status = "passed", unity = Application.unityVersion, events = catalog.careerEvents.Count, circuits = tracks.Length,
            racingVisuals = tracks.Count(t => t.sourcePack == "Racing Visuals"), rtg = tracks.Count(t => t.sourcePack == "RTG 1.3"),
            pages = 4, openingSeriesRewardsPreserved = true,
            schedule = catalog.careerEvents.Select(e => new { e.id, e.name, e.trackId, e.laps, e.opponentCount, e.opponentUpgradeLevel, date = FormulaSeasonCalendar.Current.Caption(e), e.baseValidFinishReward, e.oneTimeMedalRewards })
        }, Formatting.Indented));
    }

    private static string Range() => "EVENTS 01–06  /  " + FormulaCatalog.Current.careerEvents.Count.ToString("00");
    private static RectTransform Group(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = parent.Find(name) as RectTransform ?? FormulaUI.Rect(parent, name, x, y, width, height);
        FormulaMenuTheme.Place(rect, x, y, width, height); return rect;
    }
    private static UnityEngine.UI.Button Pager(Transform parent, string name, string title, float x, float width)
    {
        var existing = parent.Find(name);
        var button = existing == null ? FormulaMenuStyle.Button(parent, name, title, x, 0, width, 36, () => { }) : existing.GetComponent<UnityEngine.UI.Button>();
        FormulaMenuTheme.Place((RectTransform)button.transform, x, 0, width, 36); FormulaMenuTheme.StyleButton(button);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        Configure(label, 12, 0, width - 24, 36, 22); label.text = title; return button;
    }
    private static TMP_Text Label(Transform parent, string name, string title, float x, float y, float width, float height, int size)
    {
        var child = parent.Find(name);
        var label = child == null ? FormulaMenuStyle.Text(parent, title, x, y, width, height, size) : child.GetComponent<TMP_Text>();
        label.name = name; label.text = title; Configure(label, x, y, width, height, size); label.alignment = TextAlignmentOptions.Center; return label;
    }
    private static void Configure(TMP_Text label, float x, float y, float width, float height, int size)
    {
        FormulaMenuTheme.Place(label.rectTransform, x, y, width, height);
        label.font = FormulaMenuAssets.Current.strongFont; label.fontSize = label.fontSizeMin = label.fontSizeMax = size;
        label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis; label.color = Color.white; label.raycastTarget = false;
    }
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
}
