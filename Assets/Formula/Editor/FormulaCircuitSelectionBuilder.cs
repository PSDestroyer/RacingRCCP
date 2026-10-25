using System;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FormulaCircuitSelectionBuilder
{
    [MenuItem("Tools/Formula Career/Update Circuit Selection")]
    public static void Apply()
    {
        FormulaMenuPremiumBuilder.PrepareArt();
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try { Style(root.GetComponent<FormulaMenuView>().Quick); PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_CIRCUIT_SELECTOR_AUTHORED");
    }
    public static void Style(FormulaQuickView view)
    {
        var list = view.transform.Find("Event Cards");
        var cards = list.GetComponentsInChildren<FormulaEventCard>(true).ToList();
        if (cards.Count == 0) throw new InvalidOperationException("Existing circuit card is missing.");
        while (cards.Count < 6) { var copy = Object.Instantiate(cards[0].gameObject, list); copy.name = "Circuit Slot " + cards.Count; cards.Add(copy.GetComponent<FormulaEventCard>()); }
        for (int i = 0; i < 6; i++)
        {
            var card = cards[i]; Place(card.transform, 30 + i % 3 * 412, 294 + i / 3 * 144, 394, 140);
            var title = card.transform.Find("Mode").GetComponent<TMP_Text>(); Format(title, 18, 31, 218, 51, 29); title.textWrappingMode = TextWrappingModes.Normal;
            var action = card.transform.Find("Circuit").GetComponent<TMP_Text>(); Format(action, 18, 108, 250, 30, 19);
            var stage = Label(card.transform, "Circuit Number", "CIRCUIT " + (i + 1).ToString("00"), 18, 7, 220, 22, 17);
            var metadata = Label(card.transform, "Circuit Metadata", "", 18, 82, 358, 26, 17);
            var map = card.GetComponentInChildren<FormulaTrackMap>(true);
            if (map == null) map = FormulaUI.Rect(card.transform, "Circuit Outline", 242, 15, 136, 64).gameObject.AddComponent<FormulaTrackMap>();
            Place(map.transform, 242, 15, 136, 64); map.raycastTarget = false; map.color = Color.white;
            var policy = card.transform.Find("Reward Rule"); if (policy != null) policy.gameObject.SetActive(false);
            Set(card, "eventName", title); Set(card, "trackName", action); Set(card, "stage", stage); Set(card, "format", metadata);
            Set(card, "figmaLayout", false); Set(card, "mode", "quick");
            if (i < FormulaAssets.Current.tracks.Length) card.ShowCircuit(FormulaAssets.Current.tracks[i], i, "race");
            FormulaMenuTheme.StyleButton(card.GetComponent<UnityEngine.UI.Button>());
        }
        SetArray(view, "events", cards.Take(6).Cast<Object>().ToArray());
        var details = Serialized(view).FindProperty("sessionDetails").objectReferenceValue as TMP_Text;
        if (details != null) { Format(details, 570, 108, 680, 42, 18); details.textWrappingMode = TextWrappingModes.Normal; }
        var heading = view.transform.Find("Circuit Heading"); if (heading != null) Place(heading, 30, 248, 700, 32);
        var previous = Pager(view.transform, "Previous Circuits", "‹  PREVIOUS", 30);
        var next = Pager(view.transform, "Next Circuits", "NEXT  ›", 1040);
        Set(view, "previousPage", previous); Set(view, "nextPage", next);
        Set(view, "pageLabel", Label(view.transform, "Circuit Page", "CIRCUITS 01–06  /  " + FormulaAssets.Current.tracks.Length.ToString("00"), 900, 248, 350, 32, 22));
        var policyText = view.transform.Find("Career Policy")?.GetComponent<TMP_Text>();
        if (policyText != null) { Format(policyText, 300, 594, 700, 28, 18); policyText.text = "Quick sessions do not award career credits or XP."; }
        var modes = Serialized(view).FindProperty("modes"); var modeButtons = Enumerable.Range(0, modes.arraySize).Select(i => (UnityEngine.UI.Button)modes.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var buttons = cards.Take(6).Select(c => c.GetComponent<UnityEngine.UI.Button>()).ToArray();
        var back = view.GetComponentInParent<FormulaMenuView>().Back;
        for (int i = 0; i < 3; i++) Link(modeButtons[i], back, buttons[i], modeButtons[(i + 2) % 3], modeButtons[(i + 1) % 3]);
        for (int i = 0; i < buttons.Length; i++) Link(buttons[i], i < 3 ? modeButtons[i] : buttons[i - 3], i < 3 ? buttons[i + 3] : i == 5 ? next : previous, buttons[(i + 5) % 6], buttons[(i + 1) % 6]);
        Link(previous, buttons[3], modeButtons[0], next, next); Link(next, buttons[5], modeButtons[2], previous, previous);
    }
    private static void Link(UnityEngine.UI.Button b, UnityEngine.UI.Selectable up, UnityEngine.UI.Selectable down, UnityEngine.UI.Selectable left, UnityEngine.UI.Selectable right) => b.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
    private static UnityEngine.UI.Button Pager(Transform parent, string name, string text, float x)
    {
        var existing = parent.Find(name); UnityEngine.UI.Button button;
        if (existing == null) button = FormulaMenuStyle.Button(parent, name, text, x, 588, 210, 36, () => { });
        else button = existing.GetComponent<UnityEngine.UI.Button>();
        Place(button.transform, x, 588, 210, 36); FormulaMenuTheme.StyleButton(button);
        var label = button.GetComponentInChildren<TMP_Text>(true); Format(label, 12, 0, 186, 36, 22); label.text = text; return button;
    }
    private static void Place(Transform t, float x, float y, float w, float h) => FormulaMenuTheme.Place((RectTransform)t, x, y, w, h);
    private static void Format(TMP_Text t, float x, float y, float w, float h, int size) { Place(t.transform, x, y, w, h); t.fontSize = t.fontSizeMax = size; t.fontSizeMin = size * .85f; t.enableAutoSizing = true; t.raycastTarget = false; }
    private static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, int size)
    {
        var child = parent.Find(name); var text = child == null ? FormulaMenuStyle.Text(parent, value, x, y, w, h, size) : child.GetComponent<TMP_Text>(); text.name = name; text.text = value; Format(text, x, y, w, h, size); return text;
    }
    private static SerializedObject Serialized(Object target) => new SerializedObject(target);
    private static void Set(Object target, string field, Object value) { var s = Serialized(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string field, string value) { var s = Serialized(target); s.FindProperty(field).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string field, bool value) { var s = Serialized(target); s.FindProperty(field).boolValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetArray(Object target, string field, Object[] values) { var s = Serialized(target); var p = s.FindProperty(field); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; s.ApplyModifiedPropertiesWithoutUndo(); }
}
