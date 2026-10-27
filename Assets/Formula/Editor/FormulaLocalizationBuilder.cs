using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class FormulaLocalizationBuilder
{
    public const string Folder = "Assets/Formula/Localization";
    public static readonly string[] Prefabs = { FormulaMenuHierarchyBuilder.PrefabPath, FormulaPlayerAvatarBuilder.ResultsPath,
        "Assets/Formula/Resources/Formula/UI/FormulaRace.prefab", "Assets/Formula/Resources/Formula/UI/FormulaLoading.prefab" };
    private static readonly Regex Calls = new Regex("FormulaLocalization\\.[TF]\\(\"((?:\\\\.|[^\"\\\\])*)\"");

    [MenuItem("Tools/Formula Career/Inventory Localization")]
    public static void Inventory()
    {
        Directory.CreateDirectory("Docs/AI/FormulaLocalization");
        var keys = RuntimeKeys();
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureLanguage(root); foreach (var label in StaticLabels(root)) keys.Add(Source(label)); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllText("Docs/AI/FormulaLocalization/Inventory.json", JsonConvert.SerializeObject(keys.OrderBy(k => k).ToArray(), Formatting.Indented));
        Debug.Log("FORMULA_LOCALIZATION_INVENTORY " + keys.Count);
    }
    private static HashSet<string> RuntimeKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles("Assets/Formula/Scripts", "*.cs", SearchOption.AllDirectories))
            foreach (Match match in Calls.Matches(File.ReadAllText(path))) keys.Add(JsonConvert.DeserializeObject<string>("\"" + match.Groups[1].Value + "\""));
        var catalog = FormulaCatalog.Current;
        foreach (var e in catalog.careerEvents) keys.Add(e.name);
        foreach (var b in catalog.upgrades) { keys.Add(b.name); keys.Add(b.intendedEffect); }
        foreach (var s in catalog.freeSetups) keys.Add(s.name);
        foreach (var track in FormulaAssets.Current.tracks) keys.Add(track.displayName);
        keys.RemoveWhere(k => !Translatable(k));
        keys.UnionWith(new[] { "Language is unavailable.", "Saving", "GO", "MET", "NEEDED", "BRONZE", "SILVER", "GOLD", "FORMAT", "SESSION" });
        return keys;
    }
    private static bool Translatable(string text) => !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, "[A-Za-z]");
    private static string Source(TMP_Text label)
    {
        var binding = label.GetComponent<LocalizeStringEvent>();
        return binding != null && binding.StringReference.TableReference.TableCollectionName == FormulaLocalization.TableName ? binding.StringReference.TableEntryReference.Key : label.text;
    }
    private static HashSet<TMP_Text> DynamicLabels(GameObject root)
    {
        var dynamic = new HashSet<TMP_Text>();
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || component.GetType().Namespace != "FormulaGame") continue;
            var properties = new SerializedObject(component).GetIterator();
            while (properties.NextVisible(true))
            {
                if (properties.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (component is FormulaResultsView && properties.propertyPath.StartsWith("metricCaptions") && (properties.propertyPath.EndsWith("[2]") || properties.propertyPath.EndsWith("[3]"))) continue;
                if (properties.objectReferenceValue is TMP_Text label) dynamic.Add(label);
                if (properties.objectReferenceValue is UnityEngine.UI.Button button &&
                    (component is FormulaResultsView && new[] { "retry", "next", "menu" }.Contains(properties.name) ||
                     component is FormulaRaceView && new[] { "confirm", "cancel" }.Contains(properties.name) || component is FormulaControlsView && properties.name == "cancelRebind"))
                    foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) dynamic.Add(text);
            }
        }
        return dynamic;
    }
    private static IEnumerable<TMP_Text> StaticLabels(GameObject root)
    {
        var dynamic = DynamicLabels(root);
        return root.GetComponentsInChildren<TMP_Text>(true).Where(t => !dynamic.Contains(t) && t.GetComponent<FormulaDevicePrompt>() == null && Translatable(Source(t)));
    }
    public static void ConfigureLanguage(GameObject root)
    {
        var settings = root.GetComponentInChildren<FormulaSettingsView>(true); if (settings == null) return;
        var parent = settings.transform.Find("Assists"); var existing = parent.Find("settings-language");
        var button = existing != null ? existing.GetComponent<UnityEngine.UI.Button>() :
            FormulaMenuStyle.Button(parent, "settings-language", "LANGUAGE  /  English", 30, 556, 540, 56, () => { });
        FormulaMenuTheme.Place((RectTransform)button.transform, 30, 556, 540, 56);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        FormulaExperienceBuilder.Format(label, 20, 0, 500, 56, 25);
        FormulaMenuTheme.StyleButton(button);
        var fields = new SerializedObject(settings);
        fields.FindProperty("language").objectReferenceValue = button;
        fields.FindProperty("languageLabel").objectReferenceValue = label;
        fields.ApplyModifiedPropertiesWithoutUndo();
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
            selectOnUp = parent.Find("assist-esp").GetComponent<UnityEngine.UI.Button>(),
            selectOnDown = parent.Find("assist-abs").GetComponent<UnityEngine.UI.Button>(),
            selectOnRight = parent.Find("open-ACCESSIBILITY")?.GetComponent<UnityEngine.UI.Button>() };
    }
    [MenuItem("Tools/Formula Career/Build Nintendo Language Localization")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null) throw new InvalidOperationException("Existing Localization Settings are required.");
        var en = LocalizationEditorSettings.GetLocales().Single(l => l.Identifier.Code == "en");
        var locales = new List<Locale>();
        foreach (string code in FormulaLocalization.SupportedCodes)
        {
            var locale = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (locale == null)
            {
                locale = Locale.CreateLocale(code); AssetDatabase.CreateAsset(locale, Folder + "/Locale-" + code + ".asset"); LocalizationEditorSettings.AddLocale(locale);
            }
            if (code != "en" && locale.Metadata.GetMetadata<FallbackLocale>() == null) locale.Metadata.AddMetadata(new FallbackLocale(en));
            EditorUtility.SetDirty(locale); locales.Add(locale);
        }
        var romanian = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == "ro");
        if (romanian != null) LocalizationEditorSettings.RemoveLocale(romanian);
        var selectors = settings.GetStartupLocaleSelectors();
        selectors.RemoveAll(s => s is FormulaSavedLocaleSelector);
        selectors.Insert(0, new FormulaSavedLocaleSelector());
        settings.GetStringDatabase().UseFallback = true; EditorUtility.SetDirty(settings);
        var collection = LocalizationEditorSettings.GetStringTableCollection(FormulaLocalization.TableName) ??
            LocalizationEditorSettings.CreateStringTableCollection(FormulaLocalization.TableName, Folder, locales);
        foreach (var unwanted in collection.StringTables.Where(t => !FormulaLocalization.IsSupported(t.LocaleIdentifier.Code)).ToArray())
        { string path = AssetDatabase.GetAssetPath(unwanted); collection.RemoveTable(unwanted); AssetDatabase.DeleteAsset(path); }
        foreach (var locale in locales) if (collection.GetTable(locale.Identifier) == null) collection.AddNewTable(locale.Identifier);
        var translations = locales.ToDictionary(l => l.Identifier.Code,
            l => JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Folder + "/Translations/" + l.Identifier.Code + ".json")));
        var keys = RuntimeKeys();
        keys.UnionWith(collection.SharedData.Entries.Select(e => e.Key));
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { foreach (var label in StaticLabels(root)) keys.Add(Source(label)); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var gaps = new List<string>();
        foreach (string key in keys)
        {
            foreach (var table in collection.StringTables)
            {
                string code = table.LocaleIdentifier.Code;
                if (translations[code].TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value)) table.AddEntry(key, value);
                else gaps.Add(code + " / " + key);
            }
        }
        foreach (var table in collection.StringTables) { EditorUtility.SetDirty(table); LocalizationEditorSettings.SetPreloadTableFlag(table, true); }
        EditorUtility.SetDirty(collection); EditorUtility.SetDirty(collection.SharedData);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Docs/AI/FormulaLocalization");
        File.WriteAllText("Docs/AI/FormulaLocalization/Gaps.json", JsonConvert.SerializeObject(gaps, Formatting.Indented));
        if (gaps.Count > 0) throw new InvalidOperationException("Translations missing: " + gaps.Count + ". See Gaps.json.");
        PopulateLegacyTables(locales, translations);
        ConfigureFontTables(locales, translations);
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureLanguage(root);
                ConfigureFonts(root);
                foreach (var label in DynamicLabels(root))
                {
                    var binding = label.GetComponent<LocalizeStringEvent>();
                    if (binding != null && binding.StringReference.TableReference.TableCollectionName == FormulaLocalization.TableName) UnityEngine.Object.DestroyImmediate(binding);
                }
                foreach (var label in StaticLabels(root))
                {
                    string key = Source(label);
                    var localize = label.GetComponent<LocalizeStringEvent>() ?? label.gameObject.AddComponent<LocalizeStringEvent>();
                    localize.StringReference = new LocalizedString(FormulaLocalization.TableName, key);
                    for (int i = localize.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(localize.OnUpdateString, i);
                    var setter = (UnityEngine.Events.UnityAction<string>)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<string>), label, typeof(TMP_Text).GetProperty("text").GetSetMethod());
                    UnityEventTools.AddPersistentListener(localize.OnUpdateString, setter);
                    localize.OnUpdateString.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        ConfigureUsernameFonts();
        Debug.Log("FORMULA_LOCALIZATION_BUILT " + keys.Count + " keys × " + locales.Count + " languages.");
    }
    private static void ConfigureUsernameFonts()
    {
        const string path = "Assets/Scenes/Username.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid();
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save Username scene changes before rebuilding localization.");
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects()) ConfigureFonts(root);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static void ConfigureFonts(GameObject root)
    {
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.font == null || new[] { "F27", "FORMULA", "2027", "27", "RISE OF LEGENDS" }.Contains(label.text)) continue;
            var existing = label.GetComponent<FormulaLocalizedFont>();
            if (existing != null) continue;
            string key = label.font.name.Contains("Teko") ? "Heading" : label.font.name.Contains("SemiBold") ? "Emphasis" : "Body";
            var binding = label.gameObject.AddComponent<FormulaLocalizedFont>();
            binding.Configure(key, label.fontSize, key == "Heading");
        }
    }
    private static void PopulateLegacyTables(List<Locale> locales, Dictionary<string, Dictionary<string, string>> translations)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
        if (collection == null) return;
        var english = collection.StringTables.Single(t => t.LocaleIdentifier.Code == "en");
        foreach (var entry in english.Values)
        {
            if (translations["en"].TryGetValue(entry.Value, out string reviewed)) entry.Value = reviewed;
        }
        EditorUtility.SetDirty(english);
        foreach (var locale in locales)
        {
            string code = locale.Identifier.Code;
            if (code == "en") continue;
            var table = collection.GetTable(locale.Identifier) as StringTable ?? collection.AddNewTable(locale.Identifier) as StringTable;
            foreach (var key in collection.SharedData.Entries)
            {
                string source = english.GetEntry(key.Id)?.Value;
                if (string.IsNullOrWhiteSpace(source) || !translations[code].TryGetValue(source, out string value) || string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("Missing legacy translation: " + code + " / " + source);
                table.AddEntry(key.Id, value);
            }
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection); EditorUtility.SetDirty(collection.SharedData);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection); AssetDatabase.SaveAssets();
    }
    private static void ConfigureFontTables(List<Locale> locales, Dictionary<string, Dictionary<string, string>> translations)
    {
        var collection = LocalizationEditorSettings.GetAssetTableCollection(FormulaLocalization.FontTableName) ??
            LocalizationEditorSettings.CreateAssetTableCollection(FormulaLocalization.FontTableName, Folder, locales);
        EnsureFont("Symbols", "Assets/TextMesh Pro/Fonts/LiberationSans.ttf", "←→‹›−·", 256, false);
        string latinChars = string.Concat(translations.Where(p => !p.Key.StartsWith("zh") && p.Key != "ja" && p.Key != "ko").SelectMany(p => p.Value.Values));
        var latin = EnsureFont("Latin", "Assets/_GarageV2/UI/Fonts/Roboto/static/Roboto_Condensed-Regular.ttf", latinChars, 1024, false);
        if (TMP_Settings.fallbackFontAssets == null) TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
        TMP_Settings.fallbackFontAssets.RemoveAll(f => f != null && f.name == "Formula Romanian Diacritics");
        if (!TMP_Settings.fallbackFontAssets.Contains(latin)) TMP_Settings.fallbackFontAssets.Add(latin);
        EditorUtility.SetDirty(TMP_Settings.instance);
        var cjkCodes = new Dictionary<string, string> { {"ja","JP"}, {"ko","KR"}, {"zh-Hans","SC"}, {"zh-Hant","TC"} };
        var nativeNames = new Dictionary<string, string> { {"ja","日本語月日"}, {"ko","한국어월일"}, {"zh-Hans","简体中文月日"}, {"zh-Hant","繁體中文月日"} };
        foreach (var locale in locales)
        {
            string code = locale.Identifier.Code;
            TMP_FontAsset font = code == "ru" ? latin : null;
            if (cjkCodes.TryGetValue(code, out string suffix))
                font = EnsureFont("CJK-" + code, Folder + "/Fonts/NotoSans" + suffix + "-Regular.otf", string.Concat(translations[code].Values) + nativeNames[code], 2048, true);
            string profilePath = Folder + "/Fonts/Profile-" + (font == null ? "Authored" : code) + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<FormulaFontProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<FormulaFontProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            profile.font = font; EditorUtility.SetDirty(profile);
            string[] keys = { "Heading", "Emphasis", "Body" };
            for (int i = 0; i < keys.Length; i++) collection.AddAssetToTable(locale.Identifier, keys[i], profile);
        }
        foreach (var table in collection.AssetTables) { EditorUtility.SetDirty(table); LocalizationEditorSettings.SetPreloadTableFlag(table, true); }
        EditorUtility.SetDirty(collection); EditorUtility.SetDirty(collection.SharedData);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection); AssetDatabase.SaveAssets();
    }
    private static TMP_FontAsset EnsureFont(string name, string source, string characters, int atlasSize, bool cjk)
    {
        string path = Folder + "/Fonts/Formula " + name + ".asset";
        string oldPath = Folder + "/Fonts/" + name + ".asset";
        if (AssetDatabase.LoadMainAssetAtPath(path) == null && AssetDatabase.LoadMainAssetAtPath(oldPath) != null) AssetDatabase.MoveAsset(oldPath, path);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(source);
        if (sourceFont == null) throw new InvalidOperationException("Missing locale font: " + source);
        bool create = font == null;
        if (create) font = TMP_FontAsset.CreateFontAsset(sourceFont, cjk ? 48 : 64, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
        // Keep the translated glyphs baked by this builder. TMP otherwise clears
        // dynamic atlases before player builds, causing first-use rasterization.
        var fontSettings = new SerializedObject(font);
        fontSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        fontSettings.ApplyModifiedPropertiesWithoutUndo();
        font.name = "Formula " + name;
        if (name != "Symbols") font.fallbackFontAssetTable = new List<TMP_FontAsset> { AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/Fonts/Formula Symbols.asset") };
        string glyphs = new string((characters + "0123456789.,:;!?+-/%() ").Where(c => !char.IsControl(c)).Distinct().ToArray());
        // Rich-text syntax is harmless here. Unicode text is baked once to avoid first-use hitches.
        if (!font.TryAddCharacters(glyphs, out string missing))
        {
            // Noto region subsets need not contain the other languages' native names.
            string required = new string(characters.Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (!font.HasCharacters(required, out uint[] requiredMissing, true, false))
                throw new InvalidOperationException(name + " missing glyphs: " + string.Join(",", requiredMissing));
        }
        if (create) AssetDatabase.CreateAsset(font, path);
        foreach (var atlas in font.atlasTextures) { if (!EditorUtility.IsPersistent(atlas)) AssetDatabase.AddObjectToAsset(atlas, font); EditorUtility.SetDirty(atlas); }
        font.material.mainTexture = font.atlasTexture; if (!EditorUtility.IsPersistent(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font.material); EditorUtility.SetDirty(font); return font;
    }
}
