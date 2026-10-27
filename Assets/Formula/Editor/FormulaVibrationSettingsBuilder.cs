using System;
using System.IO;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class FormulaVibrationSettingsBuilder
{
    [MenuItem("Tools/Formula Career/Add Vibration Intensity Setting")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            var view = root.GetComponentInChildren<FormulaSettingsView>(true);
            Configure(view);
            if (PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath) == null) throw new IOException("Could not save vibration slider.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("FORMULA_VIBRATION_INTENSITY_SETTING_ADDED");
    }

    public static void Configure(FormulaSettingsView view)
    {
        if (view == null) throw new InvalidOperationException("Settings view is missing.");
        var serialized = new SerializedObject(view);
        var page = ((GameObject)serialized.FindProperty("audioPage").objectReferenceValue).transform;
        var volume = (UnityEngine.UI.Slider)serialized.FindProperty("volumes").GetArrayElementAtIndex(0).objectReferenceValue;
        var toggle = (UnityEngine.UI.Button)serialized.FindProperty("vibration").objectReferenceValue;
        var existing = page.Find("Vibration Intensity");
        var row = existing != null ? (RectTransform)existing : (RectTransform)UnityEngine.Object.Instantiate(volume.transform.parent.gameObject, page, false).transform;
        row.name = "Vibration Intensity";
        FormulaMenuTheme.Place(row, 30, 553, 1220, 60);
        var title = row.Find("Channel").GetComponent<TMP_Text>();
        title.text = "Vibration intensity";
        FormulaMenuTheme.Place((RectTransform)title.transform, 22, 9, 290, 42);
        var slider = row.GetComponentInChildren<UnityEngine.UI.Slider>(true);
        slider.name = "vibration-intensity";
        FormulaMenuTheme.Place((RectTransform)slider.transform, 328, 13, 650, 34);
        slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(65);
        var value = row.Find("Percentage").GetComponent<TMP_Text>();
        FormulaMenuTheme.Place((RectTransform)value.transform, 1008, 9, 166, 42);
        value.text = "65%";
        FormulaMenuTheme.Place((RectTransform)toggle.transform, 30, 475, 1220, 66);
        serialized.FindProperty("vibrationIntensity").objectReferenceValue = slider;
        serialized.FindProperty("vibrationIntensityValue").objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var volumes = serialized.FindProperty("volumes");
        var audioTab = (UnityEngine.UI.Button)serialized.FindProperty("audioTab").objectReferenceValue;
        for (int i = 0; i < volumes.arraySize; i++)
        {
            var current = (UnityEngine.UI.Slider)volumes.GetArrayElementAtIndex(i).objectReferenceValue;
            var up = i == 0 ? (UnityEngine.UI.Selectable)audioTab : (UnityEngine.UI.Slider)volumes.GetArrayElementAtIndex(i - 1).objectReferenceValue;
            var down = i == volumes.arraySize - 1 ? (UnityEngine.UI.Selectable)toggle : (UnityEngine.UI.Slider)volumes.GetArrayElementAtIndex(i + 1).objectReferenceValue;
            Navigation(current, up, down);
        }
        Navigation(toggle, (UnityEngine.UI.Slider)volumes.GetArrayElementAtIndex(volumes.arraySize - 1).objectReferenceValue, slider);
        Navigation(slider, toggle, audioTab);
    }

    private static void Navigation(UnityEngine.UI.Selectable control, UnityEngine.UI.Selectable up, UnityEngine.UI.Selectable down)
    {
        control.navigation = new UnityEngine.UI.Navigation
        {
            mode = UnityEngine.UI.Navigation.Mode.Explicit,
            selectOnUp = up, selectOnDown = down
        };
    }
}
