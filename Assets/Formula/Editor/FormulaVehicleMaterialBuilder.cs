using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FormulaVehicleMaterialBuilder
{
    private const string Details = "Assets/Formula/Materials/SurfaceDetails/";
    private const string Materials = "Assets/Formula/RCCP/Materials/";
    private const string CarbonSource = "Assets/CarImport/Wave1/2015-bmw-30-csl-hommage-concept/source/FINAL_MODEL/";

    [MenuItem("Tools/Formula Career/Apply Vehicle PBR Finishes")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        ApplyFinishes();
        string bodyName = FormulaAssets.Current.vehiclePrefab.GetComponent<FormulaVehicle>().body.name;
        foreach (string path in FormulaCatalog.Current.liveries.Select(l => l.prefabPath).Concat(new[] { AssetDatabase.GetAssetPath(FormulaAssets.Current.vehiclePrefab) }))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { BindSurfaces(root, bodyName); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene })
        {
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (loaded && scene.isDirty) throw new InvalidOperationException("Save scene changes first: " + path);
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var preview = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FormulaGarageVehiclePreview>(true)).Single();
                var parked = (GameObject)new SerializedObject(preview).FindProperty("editorVehicle").objectReferenceValue;
                if (parked != null) BindSurfaces(parked, bodyName);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        FormulaMenuAssetBuilder.BuildLiveryPreviews(FormulaMenuAssets.Current);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Docs/AI/FormulaMaterials");
        foreach (float wetness in new[] { 0f, 1f })
        {
            var texture = FormulaLiveryArtBuilder.RenderCar(0, 1280, 720, new Vector3(7, 3.2f, 5.1f), false, wetness);
            try { File.WriteAllBytes("Docs/AI/FormulaMaterials/" + (wetness == 0 ? "Dry" : "Wet") + ".png", texture.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        Validate();
        Debug.Log("FORMULA_VEHICLE_PBR_APPLIED: clear-coated paint, woven carbon, metal rims, matte rubber, per-slot cosmetic wetness.");
    }

    // Shared with livery authoring so rebuilding artwork preserves its surface finish.
    public static void ApplyFinishes()
    {
        Directory.CreateDirectory(Details);
        CopyTexture(CarbonSource + "common_carbon05_black_diff.png", "CarbonWeave.png");
        CopyTexture(CarbonSource + "common_carbon05_norm.png", "CarbonWeaveNormal.png");
        CopyTexture("Assets/Car Paint Shader/VoronoiN.png", "MicroSurfaceNormal.png");
        AssetDatabase.Refresh();
        Texture2D carbonColor = Detail("CarbonWeave.png", false), carbonNormal = Detail("CarbonWeaveNormal.png", true), grain = Detail("MicroSurfaceNormal.png", true);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var coat = Shader.Find("Universal Render Pipeline/Complex Lit");
        if (lit == null || coat == null) throw new InvalidOperationException("URP Lit shaders are missing.");
        for (int i = 0; i < FormulaAssets.Current.liveryMaterials.Length; i++)
        {
            var material = FormulaAssets.Current.liveryMaterials[i];
            Undo.RecordObject(material, "Configure Formula paint finish");
            material.shader = coat; material.shaderKeywords = Array.Empty<string>();
            bool satin = FormulaCatalog.Current.liveries[i].id == "generic_black";
            SetPbr(material, i == 6 ? .3f : .16f, satin ? .46f : .68f);
            material.SetFloat("_ClearCoat", 1); material.SetFloat("_ClearCoatMask", satin ? .45f : .85f);
            material.SetFloat("_ClearCoatSmoothness", satin ? .84f : .94f); material.EnableKeyword("_CLEARCOAT");
            MicroDetail(material, grain, .045f, 180);
            EditorUtility.SetDirty(material);
        }
        var carbon = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Material_2_7936c856.mat");
        Undo.RecordObject(carbon, "Configure Formula carbon finish");
        carbon.shader = lit; carbon.shaderKeywords = Array.Empty<string>(); SetPbr(carbon, 0, .42f);
        carbon.SetTexture("_BaseMap", carbonColor); carbon.SetTexture("_MainTex", carbonColor);
        carbon.SetColor("_BaseColor", new Color(.16f, .17f, .18f)); carbon.SetColor("_Color", new Color(.16f, .17f, .18f));
        carbon.SetTextureScale("_BaseMap", new Vector2(18, 18)); carbon.SetTexture("_BumpMap", carbonNormal); carbon.SetFloat("_BumpScale", .035f); carbon.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(carbon);
        var rim = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Material_3_f48e9479.mat");
        Undo.RecordObject(rim, "Configure Formula rim finish");
        rim.shader = lit; rim.shaderKeywords = Array.Empty<string>(); SetPbr(rim, .92f, .72f);
        rim.SetColor("_BaseColor", new Color(.3f, .33f, .36f)); rim.SetColor("_Color", new Color(.3f, .33f, .36f)); EditorUtility.SetDirty(rim);
        var tyre = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Material_4_34283242.mat");
        Undo.RecordObject(tyre, "Configure Formula rubber finish");
        tyre.shader = lit; tyre.shaderKeywords = Array.Empty<string>(); SetPbr(tyre, 0, .2f);
        tyre.SetColor("_BaseColor", new Color(.85f, .85f, .85f)); tyre.SetColor("_Color", new Color(.85f, .85f, .85f)); MicroDetail(tyre, grain, .12f, 140); EditorUtility.SetDirty(tyre);
        foreach (string path in Directory.GetFiles("Assets/Formula/Garage/Materials/Vehicle", "*_Display.mat"))
        {
            var display = AssetDatabase.LoadAssetAtPath<Material>(path);
            var source = AssetDatabase.LoadAssetAtPath<Material>(Materials + Path.GetFileNameWithoutExtension(path).Replace("_Display", "") + ".mat");
            if (source == null) throw new FileNotFoundException("Missing display source", path);
            Undo.RecordObject(display, "Synchronize Formula display finish");
            display.shader = source.shader; display.CopyPropertiesFromMaterial(source); EditorUtility.SetDirty(display);
        }
    }

    private static void SetPbr(Material material, float metallic, float smoothness)
    {
        material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Surface", 0); material.SetFloat("_WorkflowMode", 1);
        material.SetFloat("_SpecularHighlights", 1); material.SetFloat("_EnvironmentReflections", 1);
        material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = 2000;
    }
    private static void MicroDetail(Material material, Texture normal, float strength, float tiling)
    {
        material.SetTexture("_DetailNormalMap", normal); material.SetFloat("_DetailNormalMapScale", strength);
        material.SetFloat("_DetailAlbedoMapScale", 0); material.SetTextureScale("_DetailAlbedoMap", new Vector2(tiling, tiling)); material.EnableKeyword("_DETAIL_SCALED");
    }
    private static void CopyTexture(string source, string name)
    {
        if (!File.Exists(Details + name)) File.Copy(source, Details + name);
    }
    private static Texture2D Detail(string name, bool normal)
    {
        string path = Details + name; var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.convertToNormalmap = false; importer.sRGBTexture = !normal;
        importer.maxTextureSize = 512; importer.mipmapEnabled = true; importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    internal static void BindSurfaces(GameObject root, string bodyName)
    {
        var surface = root.GetComponent<FormulaVehicleSurface>(); if (surface == null) surface = root.AddComponent<FormulaVehicleSurface>();
        var bindings = new List<(Renderer renderer, int slot, FormulaVehicleSurface.SurfaceKind kind)>();
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.name == bodyName)
            {
                bindings.Add((renderer, 0, FormulaVehicleSurface.SurfaceKind.Paint));
                if (renderer.sharedMaterials.Length > 1) bindings.Add((renderer, 1, FormulaVehicleSurface.SurfaceKind.Carbon));
            }
            else if (renderer.name.StartsWith("Generic_Rim", StringComparison.Ordinal)) bindings.Add((renderer, 0, FormulaVehicleSurface.SurfaceKind.Rim));
            else if (renderer.name.StartsWith("Generic_Tyre", StringComparison.Ordinal)) bindings.Add((renderer, 0, FormulaVehicleSurface.SurfaceKind.Rubber));
            else if (renderer.name == "Object_Material_2_2") bindings.Add((renderer, 0, FormulaVehicleSurface.SurfaceKind.Carbon));
        }
        if (bindings.Count != 11) throw new InvalidOperationException("Expected 11 vehicle surface slots, got " + bindings.Count);
        var serialized = new SerializedObject(surface); var array = serialized.FindProperty("surfaces"); array.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            var item = array.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("renderer").objectReferenceValue = bindings[i].renderer;
            item.FindPropertyRelative("materialIndex").intValue = bindings[i].slot;
            item.FindPropertyRelative("kind").enumValueIndex = (int)bindings[i].kind;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo(); surface.RefreshMaterials();
    }

    [MenuItem("Tools/Formula Career/Validate Vehicle PBR Finishes")]
    public static void Validate()
    {
        foreach (var material in FormulaAssets.Current.liveryMaterials)
        {
            if (material.shader.name != "Universal Render Pipeline/Complex Lit" || !material.IsKeywordEnabled("_CLEARCOAT") || material.GetFloat("_Smoothness") < .4f) throw new InvalidOperationException("Paint finish is not configured: " + material.name);
            var errors = ShaderUtil.GetShaderMessages(material.shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.message)));
        }
        var vehicle = FormulaAssets.Current.vehiclePrefab;
        if (vehicle.GetComponent<FormulaVehicleSurface>()?.SurfaceCount != 11) throw new InvalidOperationException("Gameplay wetness bindings missing.");
        Directory.CreateDirectory("Docs/AI/FormulaMaterials");
        File.WriteAllText("Docs/AI/FormulaMaterials/AssetValidation.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            finishes = new[] { new { surface = "Paint", roughness = .32f, metallic = .16f }, new { surface = "Carbon", roughness = .58f, metallic = 0f }, new { surface = "Rim", roughness = .28f, metallic = .92f }, new { surface = "Rubber", roughness = .8f, metallic = 0f } },
            dryByDefault = true, wetnessRange = new[] { 0, 1 }, surfaceSlots = 11,
            paintShader = "Universal Render Pipeline/Complex Lit", shaderErrors = 0,
            detailTextures = new[] { Details + "CarbonWeave.png", Details + "CarbonWeaveNormal.png", Details + "MicroSurfaceNormal.png" },
            source = "Existing unbranded project textures copied unchanged; native URP shaders. No rain or tyre-grip simulation."
        }, Newtonsoft.Json.Formatting.Indented));
    }
}
