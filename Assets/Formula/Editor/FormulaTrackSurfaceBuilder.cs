using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Authored gameplay surfaces only. Vendor materials, road meshes and colliders
// remain the source of truth; visual copies live under Formula/Racing.
public static class FormulaTrackSurfaceBuilder
{
    private const string Folder = "Assets/Formula/Racing/Surfaces";
    private const string ReportFolder = "Docs/AI/FormulaSurfaces";
    private static readonly string[] RepresentativeScenes = { "Formula_Circuit_01", "Formula_Circuit_05", "Formula_Circuit_06", "Formula_Circuit_08", "Formula_RTG_01" };
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private static readonly Dictionary<string, TerrainData> terrains = new Dictionary<string, TerrainData>();
    private static Texture2D asphalt, asphaltNormal, asphaltMask, gravel, gravelNormal;

    [MenuItem("Tools/Formula Career/Refine Gameplay Surface Materials")]
    public static void Apply()
    {
        RequireEditMode();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory(Folder + "/Materials"); Directory.CreateDirectory(Folder + "/Textures");
        Directory.CreateDirectory(Folder + "/Terrain"); Directory.CreateDirectory(ReportFolder + "/After");
        AssetDatabase.Refresh(); materials.Clear(); terrains.Clear();
        PrepareTextures(); var grade = Grade(); var rows = new List<object>();
        var paths = Directory.GetFiles("Assets/Formula/Scenes", "*.unity").Where(p => p.Contains("Formula_Circuit_") || p.Contains("Formula_RTG_")).OrderBy(p => p).ToArray();
        try
        {
            foreach (string path in paths)
            {
                var scene = EditorSceneManager.OpenScene(path); bool wet = path.EndsWith("Formula_Circuit_06.unity", StringComparison.Ordinal);
                int changed = 0;
                foreach (var renderer in Components<MeshRenderer>(scene))
                {
                    var slots = renderer.sharedMaterials; bool dirty = false;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = slots[i]; if (source == null) continue;
                        string sourcePath = AssetDatabase.GetAssetPath(source);
                        // Source assets are recovered by GUID, making repeat authoring stable.
                        if (sourcePath.StartsWith(Folder + "/Materials/", StringComparison.Ordinal))
                        {
                            string guid = Path.GetFileNameWithoutExtension(sourcePath).Split('_')[0];
                            sourcePath = AssetDatabase.GUIDToAssetPath(guid); source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                        }
                        if (source == null || !SurfaceKind(sourcePath, out string kind)) continue;
                        Vector2 metres = MetresPerUv(renderer);
                        var replacement = Surface(source, sourcePath, kind, wet, metres);
                        if (slots[i] != replacement) { slots[i] = replacement; dirty = true; }
                        changed++;
                    }
                    if (dirty) { Undo.RecordObject(renderer, "Refine Formula track surface"); renderer.sharedMaterials = slots; EditorUtility.SetDirty(renderer); }
                }
                foreach (var terrain in Components<Terrain>(scene)) RefineTerrain(terrain, wet);
                var volumes = Components<Volume>(scene).Where(v => v.isGlobal).ToArray();
                if (volumes.Length == 0) volumes = new[] { new GameObject("Formula Surface Grade").AddComponent<Volume>() };
                foreach (var volume in volumes) { volume.isGlobal = true; volume.sharedProfile = grade; EditorUtility.SetDirty(volume); }
                foreach (var camera in Components<Camera>(scene))
                {
                    var data = camera.GetUniversalAdditionalCameraData();
                    if (data.renderType != CameraRenderType.Base) continue;
                    data.renderPostProcessing = true; data.volumeLayerMask |= 1; camera.allowHDR = true; EditorUtility.SetDirty(data); EditorUtility.SetDirty(camera);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                rows.Add(new { scene = path, surfaceSlots = changed, wet, audit = Describe(scene) });
                if (RepresentativeScenes.Contains(Path.GetFileNameWithoutExtension(path))) Capture(scene, ReportFolder + "/After/" + Path.GetFileNameWithoutExtension(path) + ".png");
                File.WriteAllText(ReportFolder + "/Authoring.json", JsonConvert.SerializeObject(new { status = rows.Count == paths.Length ? "complete" : "in_progress", scenes = rows, vendorAssetsUnmodified = true, physicsSettingsUnmodified = true, textureMaximumSize = 1024 }, Formatting.Indented));
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        LimitRearLightSpill(); Validate(); Debug.Log("FORMULA_GAMEPLAY_SURFACES_REFINED");
    }

    [MenuItem("Tools/Formula Career/Refine Formula Rear Light Spill")]
    public static void LimitRearLightSpill()
    {
        RequireEditMode(); var rows = new List<object>();
        var paths = FormulaCatalog.Current.liveries.Select(l => l.prefabPath).Concat(new[] { AssetDatabase.GetAssetPath(FormulaAssets.Current.vehiclePrefab) }).Distinct();
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = 0;
                foreach (var lamp in root.GetComponentsInChildren<RCCP_Light>(true))
                {
                    if (!lamp.name.StartsWith("Rear_", StringComparison.Ordinal)) continue;
                    var light = lamp.GetComponent<Light>();
                    light.range = lamp.lightType == RCCP_Light.LightType.Brakelight ? .9f : .6f; count++;
                }
                if (count != 3) throw new InvalidOperationException("Expected three Formula rear LEDs: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path); rows.Add(new { prefab = path, rearLights = count, tailRange = .6f, brakeRange = .9f });
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllText(ReportFolder + "/RearLightAudit.json", JsonConvert.SerializeObject(rows, Formatting.Indented));
    }

    private static void PrepareTextures()
    {
        asphalt = CopyTexture("Assets/JapaneseCity/Textures/jcGrdAsphaltA.png", "Asphalt Aggregate.png", false, true);
        asphaltNormal = CopyTexture("Assets/JapaneseCity/Textures/jcGrdAsphaltA_n.png", "Asphalt Normal.png", true, false);
        asphaltMask = CopyTexture("Assets/JapaneseCity/Textures/jcGrdAsphaltA_mt.png", "Asphalt Smoothness.png", false, false);
        gravel = CopyTexture("Assets/Realistic Car Controller Pro/Addons/Installed/Demo Content/Textures/Terrain/Gravel_2.png", "Runoff Gravel.png", false, true);
        gravelNormal = CopyTexture("Assets/Realistic Car Controller Pro/Addons/Installed/Demo Content/Textures/Terrain/Gravel_2N.png", "Runoff Gravel Normal.png", true, false);
    }

    private static Texture2D CopyTexture(string source, string name, bool normal, bool srgb)
    {
        string path = Folder + "/Textures/" + name;
        if (!File.Exists(path)) { File.Copy(source, path); AssetDatabase.ImportAsset(path); }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.convertToNormalmap = false; importer.sRGBTexture = srgb; importer.maxTextureSize = 1024;
        importer.mipmapEnabled = true; importer.isReadable = false; importer.anisoLevel = 4;
        importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static bool SurfaceKind(string path, out string kind)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name == "RT_Asphalt") { kind = "aggregate"; return true; }
        if (path.Contains("DryRoad/") || (path.StartsWith("Assets/Racing_Game/", StringComparison.Ordinal) && (name.StartsWith("Asphalt", StringComparison.Ordinal) || name == "Winter"))) { kind = "road"; return true; }
        if (path.Contains("/Rail/Materials/") && (name.EndsWith("_Color", StringComparison.Ordinal) || name == "Track_Rail")) { kind = "paint"; return true; }
        if ((path.StartsWith("Assets/Racing_Game/", StringComparison.Ordinal) || path.StartsWith("Assets/RTG 1.3/", StringComparison.Ordinal)) && (name == "Wall_Cement" || name == "Cement" || name == "Wall-01")) { kind = "concrete"; return true; }
        kind = null; return false;
    }

    private static Material Surface(Material source, string sourcePath, string kind, bool wet, Vector2 metres)
    {
        // Quantized world density keeps aggregate at a physical scale without
        // replacing mesh UVs or repeating an entire lane-marking atlas.
        Vector2 tiling = new Vector2(Mathf.Max(.5f, Mathf.Round(metres.x / 2 * 2) * .5f), Mathf.Max(.5f, Mathf.Round(metres.y / 2 * 2) * .5f));
        if (kind == "concrete") tiling = Vector2.one * 4;
        string key = AssetDatabase.AssetPathToGUID(sourcePath) + "_" + (wet ? "Wet" : "Dry") + "_" + kind + "_" + tiling.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "x" + tiling.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        if (materials.TryGetValue(key, out var cached)) return cached;
        string path = Folder + "/Materials/" + key + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
        material.shader = Shader.Find("Universal Render Pipeline/Lit"); material.CopyPropertiesFromMaterial(source);
        material.shaderKeywords = Array.Empty<string>(); material.name = source.name + (wet ? " Wet" : " Dry") + " Circuit";
        material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0); material.SetFloat("_Metallic", 0);
        material.SetFloat("_WorkflowMode", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
        material.SetFloat("_SpecularHighlights", 1); material.SetFloat("_EnvironmentReflections", 1);
        material.SetColor("_SpecColor", new Color(.04f, .04f, .04f));
        material.SetTexture("_MetallicGlossMap", null); material.SetTexture("_EmissionMap", null); material.SetColor("_EmissionColor", Color.black);
        material.SetTexture("_BumpMap", null); material.SetTexture("_DetailNormalMap", null); material.SetTexture("_DetailAlbedoMap", null);
        material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = 2000; material.enableInstancing = true;
        if (kind == "aggregate")
        {
            material.SetTexture("_BaseMap", asphalt); material.SetColor("_BaseColor", wet ? new Color(.7f, .74f, .78f) : new Color(.8f, .82f, .84f));
            material.SetTextureScale("_BaseMap", tiling); material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTexture("_BumpMap", asphaltNormal); material.SetFloat("_BumpScale", wet ? .15f : .25f); material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", wet ? .63f : .85f);
            if (!wet) { material.SetTexture("_MetallicGlossMap", asphaltMask); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        }
        else
        {
            material.SetFloat("_Smoothness", kind == "paint" ? wet ? .43f : .19f : kind == "concrete" ? .13f : wet ? .61f : .2f);
            material.SetTexture("_DetailNormalMap", asphaltNormal); material.SetFloat("_DetailNormalMapScale", kind == "paint" ? .18f : .22f);
            material.SetFloat("_DetailAlbedoMapScale", 0); material.SetTextureScale("_DetailAlbedoMap", tiling);
            material.SetFloat("_UVSec", 0); material.EnableKeyword("_DETAIL_SCALED");
            if (kind == "road")
            {
                // Retain the original edge lines while adding fine aggregate to
                // an otherwise uniform road atlas. Avoid grain at gravel scale.
                material.SetColor("_BaseColor", wet ? new Color(.68f, .72f, .76f) : new Color(.86f, .88f, .9f));
                material.SetTexture("_DetailAlbedoMap", asphalt); material.SetFloat("_DetailAlbedoMapScale", wet ? .2f : .28f);
            }
            if (kind == "paint")
            {
                string name = source.name;
                Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
                if (name.Contains("Green")) color = new Color(.22f, .46f, .27f);
                else if (name.Contains("Orange")) color = new Color(.78f, .3f, .08f);
                else if (name.Contains("Blue")) color = new Color(.16f, .28f, .46f);
                else if (name.Contains("Red")) color = new Color(.65f, .12f, .09f);
                material.SetColor("_BaseColor", color);
            }
        }
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); materials.Add(key, material); return material;
    }

    private static VolumeProfile Grade()
    {
        const string path = Folder + "/Circuit Surface Grade.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
        var color = Override<ColorAdjustments>(profile); color.postExposure.Override(.15f); color.contrast.Override(8); color.saturation.Override(-3);
        var tonemapping = Override<Tonemapping>(profile); tonemapping.mode.Override(TonemappingMode.Neutral);
        var bloom = Override<Bloom>(profile); bloom.threshold.Override(1.2f); bloom.intensity.Override(.06f); bloom.scatter.Override(.55f); bloom.clamp.Override(3);
        var vignette = Override<Vignette>(profile); vignette.intensity.Override(.1f); vignette.smoothness.Override(.4f);
        var lift = Override<LiftGammaGain>(profile); lift.lift.Override(new Vector4(1, 1, 1, 0)); lift.gamma.Override(new Vector4(1, 1, 1, 0)); lift.gain.Override(new Vector4(1, 1, 1, 0));
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); return profile;
    }

    private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component)) { component = profile.Add<T>(); AssetDatabase.AddObjectToAsset(component, profile); }
        component.active = true; EditorUtility.SetDirty(component); return component;
    }

    private static void RefineTerrain(Terrain terrain, bool wet)
    {
        string sourcePath = AssetDatabase.GetAssetPath(terrain.terrainData);
        if (sourcePath.StartsWith(Folder + "/Terrain/", StringComparison.Ordinal))
        {
            string guid = Path.GetFileNameWithoutExtension(sourcePath).Split('_')[0]; sourcePath = AssetDatabase.GUIDToAssetPath(guid);
        }
        string key = AssetDatabase.AssetPathToGUID(sourcePath) + (wet ? "_Wet" : "_Dry");
        if (!terrains.TryGetValue(key, out var data))
        {
            string path = Folder + "/Terrain/" + key + ".asset";
            if (!File.Exists(path) && !AssetDatabase.CopyAsset(sourcePath, path)) throw new IOException("Could not copy Formula visual terrain: " + sourcePath);
            data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            var original = AssetDatabase.LoadAssetAtPath<TerrainData>(sourcePath); var layers = new TerrainLayer[original.terrainLayers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var source = original.terrainLayers[i]; string layerPath = Folder + "/Terrain/" + key + "_Layer" + i + ".terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (layer == null) { layer = Object.Instantiate(source); AssetDatabase.CreateAsset(layer, layerPath); }
                layer.metallic = 0; layer.smoothness = wet ? .18f : .06f;
                if (source.name == "Desert_Layer_3")
                {
                    layer.diffuseTexture = gravel; layer.normalMapTexture = gravelNormal; layer.normalScale = .22f;
                    layer.tileSize = new Vector2(3, 3); layer.diffuseRemapMin = Vector4.zero; layer.diffuseRemapMax = Vector4.one;
                }
                EditorUtility.SetDirty(layer); AssetDatabase.SaveAssetIfDirty(layer); layers[i] = layer;
            }
            data.terrainLayers = layers; EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data); terrains.Add(key, data);
        }
        Undo.RecordObject(terrain, "Refine Formula terrain surfaces"); terrain.terrainData = data;
        var collider = terrain.GetComponent<TerrainCollider>(); if (collider != null) { collider.terrainData = data; EditorUtility.SetDirty(collider); }
        EditorUtility.SetDirty(terrain);
    }

    [MenuItem("Tools/Formula Career/Validate Gameplay Surface Materials")]
    public static void Validate()
    {
        var paths = AssetDatabase.FindAssets("t:Material", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        foreach (string path in paths)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material.shader.name != "Universal Render Pipeline/Lit" || material.GetFloat("_Metallic") != 0) throw new InvalidOperationException("Invalid physical track surface: " + path);
            if (ShaderUtil.GetShaderMessages(material.shader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)) throw new InvalidOperationException("Track surface shader compilation failed.");
        }
        foreach (string path in AssetDatabase.FindAssets("t:TerrainData", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var copy = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            string guid = Path.GetFileNameWithoutExtension(path).Split('_')[0]; var source = AssetDatabase.LoadAssetAtPath<TerrainData>(AssetDatabase.GUIDToAssetPath(guid));
            if (copy.heightmapResolution != source.heightmapResolution || copy.size != source.size) throw new InvalidOperationException("Surface authoring changed terrain dimensions.");
            var before = source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution); var after = copy.GetHeights(0, 0, copy.heightmapResolution, copy.heightmapResolution);
            for (int x = 0; x < source.heightmapResolution; x++) for (int y = 0; y < source.heightmapResolution; y++)
                if (before[x, y] != after[x, y]) throw new InvalidOperationException("Surface authoring changed terrain geometry: " + path);
        }
        File.WriteAllText(ReportFolder + "/Validation.json", JsonConvert.SerializeObject(new { status = "passed", shader = "URP Lit", materials = paths.Length, terrainHeightsUnchanged = true, vendorAssetsUnmodified = true, unity = Application.unityVersion }, Formatting.Indented));
    }

    [MenuItem("Tools/Formula Career/Inspect Gameplay Surface Materials")]
    public static void Inspect()
    {
        RequireEditMode();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory(ReportFolder + "/Before");
        var rows = new List<object>();
        try
        {
            foreach (string name in RepresentativeScenes)
            {
                var scene = EditorSceneManager.OpenScene("Assets/Formula/Scenes/" + name + ".unity");
                rows.Add(Describe(scene));
                Capture(scene, ReportFolder + "/Before/" + name + ".png");
            }
            File.WriteAllText(ReportFolder + "/Before.json", JsonConvert.SerializeObject(rows, Formatting.Indented));
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("FORMULA_GAMEPLAY_SURFACES_INSPECTED");
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring surfaces.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene before running surface authoring: " + SceneManager.GetSceneAt(i).path);
    }

    private static IEnumerable<T> Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    private static object Describe(Scene scene)
    {
        var circuit = Components<FormulaCircuit>(scene).Single();
        return new
        {
            scene = scene.path, track = circuit.trackId,
            roads = Components<MeshRenderer>(scene).Where(r => r.sharedMaterials.Any(m => m != null && IsSurfaceName(AssetDatabase.GetAssetPath(m))))
                .Take(32).Select(r => new { r.name, bounds = r.bounds.size.ToString("F2"), uvMetres = MetresPerUv(r).ToString("F3"), materials = r.sharedMaterials.Select(m => m == null ? null : new { source = AssetDatabase.GetAssetPath(m), shader = m.shader.name, albedo = m.HasProperty("_BaseMap") ? AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")) : "", smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : -1 }).ToArray() }).ToArray(),
            terrains = Components<Terrain>(scene).Select(t => new { t.name, material = AssetDatabase.GetAssetPath(t.materialTemplate), data = AssetDatabase.GetAssetPath(t.terrainData), layers = t.terrainData.terrainLayers.Select(l => new { l.name, diffuse = AssetDatabase.GetAssetPath(l.diffuseTexture), normal = AssetDatabase.GetAssetPath(l.normalMapTexture), tileSize = l.tileSize.ToString(), l.metallic, l.smoothness, diffuseRemapMin = l.diffuseRemapMin.ToString(), diffuseRemapMax = l.diffuseRemapMax.ToString() }).ToArray() }).ToArray(),
            volumes = Components<Volume>(scene).Select(v => new { v.name, profile = AssetDatabase.GetAssetPath(v.sharedProfile), overrides = v.sharedProfile == null ? null : v.sharedProfile.components.Where(c => c != null).Select(c => c.GetType().Name).ToArray() }).ToArray(),
            hdr = UniversalRenderPipeline.asset.supportsHDR
        };
    }

    private static bool IsSurfaceName(string path) => path.StartsWith(Folder + "/Materials/", StringComparison.Ordinal) || new[] { "Asphalt", "Road-", "Green_Color", "Orange_Color", "Track_Rail", "Winter.mat" }.Any(path.Contains);

    private static Vector2 MetresPerUv(MeshRenderer renderer)
    {
        var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) return Vector2.one;
        using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
        using (var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
        using (var uv = new NativeArray<Vector2>(mesh.vertexCount, Allocator.Temp))
        {
            data[0].GetVertices(vertices); data[0].GetUVs(0, uv);
            var x = new List<float>(); var y = new List<float>();
            for (int submesh = 0; submesh < data[0].subMeshCount && x.Count < 64; submesh++)
            {
              using (var indices = new NativeArray<int>(data[0].GetSubMesh(submesh).indexCount, Allocator.Temp))
              {
               data[0].GetIndices(indices, submesh);
               for (int i = 0; i + 2 < indices.Length && x.Count < 64; i += 3)
               {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                Vector2 u = uv[b] - uv[a], v = uv[c] - uv[a]; float determinant = u.x * v.y - u.y * v.x;
                if (Mathf.Abs(determinant) < .000001f) continue;
                Vector3 p = renderer.transform.TransformVector(vertices[b] - vertices[a]), q = renderer.transform.TransformVector(vertices[c] - vertices[a]);
                x.Add(((p * v.y - q * u.y) / determinant).magnitude); y.Add(((q * u.x - p * v.x) / determinant).magnitude);
               }
              }
            }
            if (x.Count == 0) return Vector2.one;
            x.Sort(); y.Sort(); return new Vector2(x[x.Count / 2], y[y.Count / 2]);
        }
    }

    private static void Capture(Scene scene, string path)
    {
        var circuit = Components<FormulaCircuit>(scene).Single(); var grid = circuit.startingGrid[0];
        GameObject car = null, cameraObject = null;
        var previousTarget = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 2 };
        try
        {
            car = (GameObject)PrefabUtility.InstantiatePrefab(FormulaAssets.Current.vehiclePrefab, scene);
            car.transform.SetPositionAndRotation(grid.position, grid.rotation);
            cameraObject = new GameObject("Surface review camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.fieldOfView = 58; camera.aspect = 1280f / 720; camera.nearClipPlane = .1f; camera.farClipPlane = 1800;
            camera.allowHDR = true; camera.allowMSAA = true; camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = grid.position - grid.forward * 8.5f + Vector3.up * 3.1f;
            camera.transform.rotation = Quaternion.LookRotation(grid.position + grid.forward * 8 + Vector3.up * .6f - camera.transform.position);
            var additional = camera.GetUniversalAdditionalCameraData(); additional.renderPostProcessing = true; additional.volumeLayerMask = ~0;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try { pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG()); }
            finally { Object.DestroyImmediate(pixels); }
        }
        finally
        {
            RenderTexture.active = previousTarget; target.Release(); Object.DestroyImmediate(target);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (car != null) Object.DestroyImmediate(car);
        }
    }
}
