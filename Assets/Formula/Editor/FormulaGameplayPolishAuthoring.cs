using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FormulaGameplayPolishAuthoring
{
    public static void Audio()
    {
        var assets = FormulaMenuAssets.Current;
        string[] names = { "Focus", "Confirm", "Record", "Result", "Unlock", "End" };
        string[] fields = { "focusSound", "confirmSound", "recordSound", "resultSound", "unlockSound", "endSound" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = "Assets/Formula/Audio/UI/" + names[i] + ".wav";
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            if (!importer.forceToMono || settings.compressionFormat != AudioCompressionFormat.PCM)
            {
                importer.forceToMono = true; settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            FormulaExperienceBuilder.Set(assets, fields[i], AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }
        EditorUtility.SetDirty(assets);
    }

    [MenuItem("Tools/Formula Career/Apply Gameplay Surface Polish")]
    public static void Surfaces()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        const string folder = "Assets/Formula/Racing/Materials/DryRoad";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var previous = EditorSceneManager.GetSceneManagerSetup();
        var replacements = new Dictionary<Material, Material>();
        var rows = new List<object>();
        try
        {
            foreach (string scenePath in Directory.GetFiles("Assets/Formula/Scenes", "*.unity").Where(p => p.Contains("Formula_Circuit_") || p.Contains("Formula_RTG_")).OrderBy(p => p))
            {
                // The explicitly wet event keeps its wet surface presentation.
                if (scenePath.EndsWith("Formula_Circuit_06.unity", StringComparison.Ordinal)) continue;
                var scene = EditorSceneManager.OpenScene(scenePath);
                int changed = 0;
                var sun = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Directional).ToArray();
                var lighting = sun.Select(l => new { name = l.name, before = l.intensity, after = Mathf.Min(l.intensity, 2f) }).ToArray();
                bool lightChanged = false;
                foreach (var light in sun)
                    if (light.intensity > 2f) { light.intensity = 2f; EditorUtility.SetDirty(light); lightChanged = true; }
                foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
                {
                    var materials = renderer.sharedMaterials; bool dirty = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var source = materials[i]; string path = AssetDatabase.GetAssetPath(source);
                        if (path.StartsWith(folder + "/", StringComparison.Ordinal))
                        {
                            string original = source.name.Replace("_Dry", "");
                            path = original.StartsWith("Asphalt", StringComparison.Ordinal) ? "Assets/Racing_Game/Textures/Roads/Materials/" + original + ".mat" : "Assets/RTG 1.3/Materials/" + original + ".mat";
                            source = AssetDatabase.LoadAssetAtPath<Material>(path);
                        }
                        bool road = path.StartsWith("Assets/Racing_Game/Textures/Roads/Materials/Asphalt", StringComparison.Ordinal)
                            || path.StartsWith("Assets/RTG 1.3/Materials/Road", StringComparison.Ordinal);
                        if (!road || source == null || !source.HasProperty("_Smoothness")) continue;
                        if (!replacements.TryGetValue(source, out var replacement))
                        {
                            string clonePath = folder + "/" + source.name + "_Dry.mat";
                            replacement = AssetDatabase.LoadAssetAtPath<Material>(clonePath);
                            if (replacement == null) { replacement = new Material(source); AssetDatabase.CreateAsset(replacement, clonePath); }
                            replacement.SetFloat("_Smoothness", .22f);
                            if (replacement.HasProperty("_Metallic")) replacement.SetFloat("_Metallic", 0);
                            if (replacement.HasProperty("_SpecColor")) replacement.SetColor("_SpecColor", new Color(.04f, .04f, .04f, 1));
                            if (replacement.HasProperty("_SmoothnessTextureChannel")) replacement.SetFloat("_SmoothnessTextureChannel", 0);
                            replacement.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                            replacement.DisableKeyword("_GLOSSINESS_FROM_BASE_ALPHA");
                            EditorUtility.SetDirty(replacement); replacements.Add(source, replacement);
                        }
                        if (materials[i] != replacement) { materials[i] = replacement; dirty = true; }
                        changed++;
                    }
                    if (dirty) { renderer.sharedMaterials = materials; EditorUtility.SetDirty(renderer); }
                }
                if (changed > 0 || lightChanged) EditorSceneManager.SaveScene(scene);
                rows.Add(new { scene = scenePath, dryMaterialSlots = changed, directionalLights = lighting });
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/AI/FormulaUI/GameplayPolish");
            File.WriteAllText("Docs/AI/FormulaUI/GameplayPolish/SurfaceAuthoring.json", JsonConvert.SerializeObject(new
            {
                materials = replacements.Select(p => new { source = AssetDatabase.GetAssetPath(p.Key), before = p.Key.GetFloat("_Smoothness"), replacement = AssetDatabase.GetAssetPath(p.Value), shader = p.Value.shader.name, smoothness = p.Value.GetFloat("_Smoothness"), metallic = p.Value.HasProperty("_Metallic") ? p.Value.GetFloat("_Metallic") : 0 }),
                scenes = rows, wetScenePreserved = "Formula_Circuit_06", vendorMaterialsUnmodified = true
            }, Formatting.Indented));
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        Debug.Log("FORMULA_GAMEPLAY_SURFACES_AUTHORED");
    }
}
