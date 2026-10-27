using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

// One-way English text migration. It never renames hierarchy objects or save/catalog IDs.
public static class FormulaEnglishBuilder
{
    private static Dictionary<string, string> copy;
    private static Regex tokens;
    public static string Translate(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (copy == null)
        {
            copy = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("Assets/Formula/Editor/FormulaEnglishText.json"));
            tokens = new Regex(string.Join("|", copy.Keys.OrderByDescending(k => k.Length).Select(k => @"(?<!\w)" + Regex.Escape(k) + @"(?!\w)")));
        }
        // A single pass is idempotent: e.g. BRONZE must never become BRONZEE.
        return tokens.Replace(value, m => copy[m.Value]);
    }
    public static int TranslateHierarchy(GameObject root)
    {
        int changed = 0;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string english = Translate(text.text); if (english == text.text) continue;
            text.text = english; EditorUtility.SetDirty(text); PrefabUtility.RecordPrefabInstancePropertyModifications(text); changed++;
        }
        foreach (var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            string english = Translate(text.text); if (english == text.text) continue;
            text.text = english; EditorUtility.SetDirty(text); PrefabUtility.RecordPrefabInstancePropertyModifications(text); changed++;
        }
        return changed;
    }
    [MenuItem("Tools/Formula Career/Apply English Text")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            if (scene.isDirty) throw new InvalidOperationException("Save scene changes first: " + scene.path);
        copy = null; tokens = null;
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            string guid = AssetDatabase.FindAssets("t:LocalizationSettings", new[] { "Assets" }).FirstOrDefault();
            if (guid == null) throw new InvalidOperationException("Existing Localization Settings were not found.");
            settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(AssetDatabase.GUIDToAssetPath(guid));
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        var english = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == "en");
        if (english == null) throw new InvalidOperationException("Existing English locale was not found.");
        var selectors = settings.GetStartupLocaleSelectors();
        selectors.RemoveAll(s => s is SpecificLocaleSelector);
        int savedSelector = selectors.FindIndex(s => s is FormulaSavedLocaleSelector);
        selectors.Insert(savedSelector < 0 ? 0 : savedSelector + 1, new SpecificLocaleSelector { LocaleId = english.Identifier });
        LocalizationSettings.ProjectLocale = english;
        EditorUtility.SetDirty(settings);
        var table = LocalizationEditorSettings.GetStringTableCollection("UI");
        if (table == null || !table.StringTables.Any(t => t.LocaleIdentifier.Code == "en")) throw new InvalidOperationException("Existing English UI string table was not found.");
        // Keep existing tables and the player's saved language; English remains the fallback.
        int count = 0;
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try { count += TranslateHierarchy(root); PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var assets = FormulaAssets.Current;
        foreach (var track in assets.tracks) track.displayName = Translate(track.displayName);
        EditorUtility.SetDirty(assets);
        var car = AssetDatabase.LoadMainAssetAtPath("Assets/Formula/Resources/Formula/FormulaGenericCar.asset");
        var carData = new SerializedObject(car); var info = carData.FindProperty("_carInfo"); info.stringValue = Translate(info.stringValue); carData.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(car);
        foreach (string path in new[] { "Assets/Scenes/Init.unity", "Assets/Scenes/Username.unity", "Assets/Scenes/Menu.unity", "Assets/Scenes/GameplayTestScene.unity", FormulaGarageBuilder.GarageScene, "Assets/Formula/Scenes/Formula_Circuit_01.unity", "Assets/Formula/Scenes/Formula_Circuit_02.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int sceneChanges = scene.GetRootGameObjects().Sum(TranslateHierarchy);
                if (sceneChanges > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); count += sceneChanges; }
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Docs/AI/FormulaUI/English");
        File.WriteAllText("Docs/AI/FormulaUI/English/Authoring.json", JsonConvert.SerializeObject(new { atUtc = DateTime.UtcNow, changedLabels = count, startupLocale = english.Identifier.Code, existingTable = "UI", scope = "Text only. Hierarchy names, navigation/save IDs, prices and layout preserved." }, Formatting.Indented));
        Debug.Log("FORMULA_ENGLISH_TEXT_APPLIED: " + count + " authored labels; English startup locale.");
    }
}
