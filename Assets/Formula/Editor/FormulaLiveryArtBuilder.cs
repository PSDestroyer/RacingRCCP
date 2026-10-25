using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FormulaGame;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TMPro;

// Updates the existing material assets so prefab references and saved livery IDs survive.
public static class FormulaLiveryArtBuilder
{
    private const string Textures = "Assets/Formula/Liveries/Textures/";
    private const string Materials = "Assets/Formula/RCCP/Materials/";
    private const string Review = "Docs/AI/FormulaLiveries/";
    private static readonly string[] Artwork = { "ApexWorks", "VelocityNight", "CorsaHeritage", "AzureTidal", "PulseSignal", "VoltFactory", "NovaCrown", "ForestSummit" };

    [MenuItem("Tools/Formula Career/Apply Original Generic Liveries")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (string name in Artwork.Concat(new[] { "G27Slick" }))
            if (!File.Exists(Textures + name + ".png")) throw new FileNotFoundException("Missing livery artwork", Textures + name + ".png");
        AssetDatabase.Refresh();
        var assets = FormulaAssets.Current;
        if (assets.liveryMaterials.Length != Artwork.Length) throw new InvalidOperationException("Expected eight Generic livery materials.");
        for (int i = 0; i < Artwork.Length; i++)
        {
            var material = assets.liveryMaterials[i];
            Undo.RecordObject(material, "Replace Generic livery artwork");
            Paint(material, Import(Artwork[i]), Color.white);
            material.name = "G27 " + FormulaCatalog.Current.liveries[i].displayName;
        }
        Paint(AssetDatabase.LoadAssetAtPath<Material>(Materials + "Material_4_34283242.mat"), Import("G27Slick"), Color.white);
        // These two maps contained a real rim mark and a modding credit plate.
        // Keep the original imported source files, but use unbranded finishes in the game.
        var rim = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Material_3_f48e9479.mat");
        Paint(rim, null, new Color(.13f, .145f, .16f)); rim.SetFloat("_Metallic", .65f); rim.SetFloat("_Smoothness", .3f);
        var carbon = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Material_2_7936c856.mat");
        Paint(carbon, null, new Color(.025f, .03f, .035f)); carbon.SetFloat("_Metallic", .05f); carbon.SetFloat("_Smoothness", .2f);
        SeparateCarbonFloor(carbon);
        SyncDisplayCopies();
        FormulaVehicleMaterialBuilder.ApplyFinishes();
        RefreshAuthoredName();
        FormulaMenuAssetBuilder.BuildLiveryPreviews(FormulaMenuAssets.Current);
        AssetDatabase.SaveAssets();
        RenderReview();
        Validate();
        Debug.Log("FORMULA_ORIGINAL_LIVERIES_APPLIED: eight original liveries, clean tyres/rims/carbon, refreshed previews.");
    }

    private static void SeparateCarbonFloor(Material carbon)
    {
        const string meshPath = "Assets/Formula/Liveries/GenericPaintedBody.asset";
        var vehicle = FormulaAssets.Current.vehiclePrefab;
        var body = vehicle.GetComponent<FormulaVehicle>().body;
        var filter = body.GetComponent<MeshFilter>();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = "Generic painted shell and carbon floor";
            int[] triangles = mesh.triangles; var vertices = mesh.vertices;
            var painted = new List<int>(); var floor = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var center = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3;
                center = vehicle.transform.InverseTransformPoint(body.transform.TransformPoint(center));
                // Leave the low front wing painted. The floor and diffuser sit
                // behind the front axle and should never inherit sidepod decals.
                var destination = center.y < .18f && center.z < 1.5f ? floor : painted;
                destination.Add(triangles[i]); destination.Add(triangles[i + 1]); destination.Add(triangles[i + 2]);
            }
            if (floor.Count == 0 || painted.Count == 0) throw new InvalidOperationException("Carbon floor classification failed.");
            mesh.subMeshCount = 2; mesh.SetTriangles(painted, 0); mesh.SetTriangles(floor, 1); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        foreach (string path in FormulaCatalog.Current.liveries.Select(l => l.prefabPath).Concat(new[] { AssetDatabase.GetAssetPath(vehicle) }))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == body.name);
                target.GetComponent<MeshFilter>().sharedMesh = mesh;
                target.sharedMaterials = new[] { target.sharedMaterial, carbon };
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // The authored parked car is a meshes-only copy in each presentation scene.
        // It receives the same mesh, without changing its approved camera or layout.
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene })
        {
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (loaded && scene.isDirty) throw new InvalidOperationException("Save scene changes first: " + path);
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var target in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name == body.name))
                {
                    target.GetComponent<MeshFilter>().sharedMesh = mesh;
                    target.sharedMaterials = new[] { target.sharedMaterial, carbon };
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }

    private static void RefreshAuthoredName()
    {
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            foreach (var card in root.GetComponentsInChildren<FormulaLiveryCard>(true))
            {
                var label = card.transform.Find("Livery Name").GetComponent<TMP_Text>();
                label.text = FormulaCatalog.Current.Skin(card.LiveryId).displayName;
                var serialized = new SerializedObject(card);
                serialized.FindProperty("liveryName").objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.text == "Apex Orange") text.text = FormulaCatalog.Current.liveries[0].displayName;
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Texture2D Import(string name)
    {
        string path = Textures + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true; importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4; importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 80;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void Paint(Material material, Texture texture, Color color)
    {
        if (material == null) throw new InvalidOperationException("Generic material is missing.");
        material.SetTexture("_BaseMap", texture); material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", color); material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
    }

    private static void SyncDisplayCopies()
    {
        const string folder = "Assets/Formula/Garage/Materials/Vehicle/";
        foreach (string path in Directory.GetFiles(folder, "*_Display.mat"))
        {
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            string source = Materials + Path.GetFileNameWithoutExtension(path).Replace("_Display", "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(source);
            if (material == null) throw new FileNotFoundException("Display material source is missing", source);
            float smoothness = copy.GetFloat("_Smoothness"), metallic = copy.GetFloat("_Metallic");
            Undo.RecordObject(copy, "Refresh parked Generic artwork");
            copy.CopyPropertiesFromMaterial(material);
            copy.SetFloat("_Smoothness", smoothness); copy.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(copy);
        }
    }

    [MenuItem("Tools/Formula Career/Render Original Livery Review")]
    public static void RenderReview()
    {
        Directory.CreateDirectory(Review);
        for (int i = 0; i < Artwork.Length; i++)
        {
            Render(i, new Vector3(7, 3.2f, 5.1f), Review + Artwork[i] + "-Front.png");
            Render(i, new Vector3(-8, 2.5f, -5.8f), Review + Artwork[i] + "-Rear.png");
        }
    }

    private static void Render(int index, Vector3 cameraOffset, string path)
    {
        var texture = RenderCar(index, 1280, 720, cameraOffset, false);
        try { File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    // URP renders an isolated camera explicitly. PreviewRenderUtility's SRP path
    // can retain a stale projection/camera viewport and crop the thumbnails.
    internal static Texture2D RenderCar(int index, int width, int height, Vector3 direction, bool transparent, float wetness = 0)
    {
        var source = FormulaAssets.Current.vehiclePrefab;
        var body = source.GetComponent<FormulaVehicle>().body;
        var previousScene = SceneManager.GetActiveScene();
        var otherLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var previousTarget = RenderTexture.active;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        try
        {
            SceneManager.SetActiveScene(scene);
            foreach (var light in otherLights) light.enabled = false;
            var root = new GameObject("Generic livery review");
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var original = filter.GetComponent<MeshRenderer>();
                if (original == null || !original.enabled) continue;
                var child = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = 31 };
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = source.transform.InverseTransformPoint(filter.transform.position);
                child.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
                child.transform.localScale = filter.transform.lossyScale;
                child.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterials = original.sharedMaterials;
                if (original == body) renderer.sharedMaterial = FormulaAssets.Current.liveryMaterials[index];
            }
            FormulaVehicleMaterialBuilder.BindSurfaces(root, body.name);
            root.GetComponent<FormulaVehicleSurface>().SetWetness(wetness);
            var bounds = root.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            root.transform.position = -bounds.center;
            var camera = new GameObject("Livery Camera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            camera.enabled = false; camera.cullingMask = 1 << 31;
            camera.fieldOfView = 32; camera.aspect = width / (float)height;
            camera.nearClipPlane = .05f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = transparent ? Color.clear : new Color(.07f, .075f, .085f);
            camera.allowHDR = false; camera.allowMSAA = true;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.volumeLayerMask = 0;
            var rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
            float tan = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad), distance = 0;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var corner = Quaternion.Inverse(rotation) * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                distance = Mathf.Max(distance, Mathf.Abs(corner.y) / tan - corner.z, Mathf.Abs(corner.x) / (tan * camera.aspect) - corner.z);
            }
            camera.transform.SetPositionAndRotation(direction.normalized * distance * 1.1f, rotation);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .6f); RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Formula/Garage/Garage Reflection.exr");
            RenderSettings.reflectionIntensity = 1;
            StudioLight("Key", Quaternion.Euler(40, 25, 0), 1.4f);
            StudioLight("Fill", Quaternion.Euler(325, 210, 0), 1.1f);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            return texture;
        }
        finally
        {
            RenderTexture.active = previousTarget; target.Release(); UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            foreach (var light in otherLights) if (light != null) light.enabled = true;
        }
    }

    private static void StudioLight(string name, Quaternion rotation, float intensity)
    {
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = intensity; light.color = Color.white;
        light.cullingMask = 1 << 31; light.transform.rotation = rotation;
    }

    [MenuItem("Tools/Formula Career/Validate Original Generic Liveries")]
    public static void Validate()
    {
        var assets = FormulaAssets.Current;
        string[] expectedIds = { "generic_orange", "generic_black", "generic_red", "generic_blue", "generic_pulse", "generic_yellow", "generic_purple", "generic_green" };
        if (!FormulaCatalog.Current.liveries.Select(l => l.id).SequenceEqual(expectedIds)) throw new InvalidOperationException("Stable livery IDs/order changed.");
        for (int i = 0; i < Artwork.Length; i++)
        {
            if (AssetDatabase.GetAssetPath(assets.liveryMaterials[i].GetTexture("_BaseMap")) != Textures + Artwork[i] + ".png") throw new InvalidOperationException("Incorrect livery binding at index " + i);
            if (FormulaMenuAssets.Current.liveries[i] == null) throw new InvalidOperationException("Missing livery thumbnail " + i);
        }
        foreach (var renderer in assets.vehiclePrefab.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials.Where(m => m != null))
                foreach (string property in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(property); if (texture == null) continue;
                    string path = AssetDatabase.GetAssetPath(texture);
                    if (path.Contains("formula-1-generico-2")) throw new InvalidOperationException("Branded source map still bound: " + renderer.name + " / " + property);
                }
        Directory.CreateDirectory(Review);
        File.WriteAllText(Review + "AssetValidation.json", JsonUtility.ToJson(new ValidationReport
        {
            generatedUtc = DateTime.UtcNow.ToString("O"), liveryIds = expectedIds,
            artwork = Artwork, originalSourceTexturesReferenced = false,
            checks = new[] { "Eight stable IDs in their original order", "Eight distinct body artwork maps", "Eight regenerated thumbnails", "No imported branded source texture on the gameplay prefab", "Display material copies refreshed" }
        }, true));
    }

    [Serializable] private sealed class ValidationReport
    {
        public string generatedUtc;
        public string[] liveryIds, artwork, checks;
        public bool originalSourceTexturesReferenced;
    }
}
