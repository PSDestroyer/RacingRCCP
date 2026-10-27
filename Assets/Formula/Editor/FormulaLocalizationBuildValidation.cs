using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;

public static class FormulaLocalizationBuildValidation
{
    [MenuItem("Tools/Formula Career/Build and Audit Localization Content")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        const string folder = "Docs/AI/FormulaLocalization";
        Directory.CreateDirectory(folder);
        var fontAudit = new List<object>();
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FormulaLocalizationBuilder.Folder + "/Fonts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
            bool clearsOnBuild = new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue;
            bool materialSaved = AssetDatabase.LoadAllAssetsAtPath(path).Any(a => a is Material);
            if (clearsOnBuild || !materialSaved || font.characterTable.Count == 0 || !font.isMultiAtlasTexturesEnabled)
                throw new InvalidOperationException("Localized font is not ready for builds: " + path);
            fontAudit.Add(new { font = font.name, characters = font.characterTable.Count, atlases = font.atlasTextures.Length, clearsOnBuild, materialSaved });
        }
        File.WriteAllText(folder + "/FontAudit.json", JsonConvert.SerializeObject(fontAudit, Formatting.Indented));
        var gaps = new List<string>(); int count = 0;
        foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
            foreach (var key in collection.SharedData.Entries) foreach (var table in collection.StringTables)
            {
                count++;
                if (string.IsNullOrWhiteSpace(table.GetEntry(key.Id)?.Value)) gaps.Add(collection.TableCollectionName + " / " + table.LocaleIdentifier.Code + " / " + key.Key);
            }
        File.WriteAllText(folder + "/TableAudit.json", JsonConvert.SerializeObject(new { checkedEntries = count, gaps }, Formatting.Indented));
        File.WriteAllText(folder + "/ContentBuild.json", "{\"status\":\"running\"}");
        try
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            bool passed = result != null && string.IsNullOrEmpty(result.Error);
            File.WriteAllText(folder + "/ContentBuild.json", JsonConvert.SerializeObject(new { status = passed ? "passed" : "failed", error = result?.Error, target = EditorUserBuildSettings.activeBuildTarget.ToString(), unity = Application.unityVersion }, Formatting.Indented));
        }
        catch (Exception error)
        {
            File.WriteAllText(folder + "/ContentBuild.json", JsonConvert.SerializeObject(new { status = "failed", error = error.ToString() }, Formatting.Indented));
        }
    }
}
