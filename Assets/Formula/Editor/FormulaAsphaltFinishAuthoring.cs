using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Entirely offline authoring: existing Lit road passes + one shared, single-pass
// rubber renderer per circuit. No runtime generators, colliders or update loops.
public static class FormulaAsphaltFinishAuthoring
{
    public const string Report = "Docs/AI/FormulaAsphalt";
    private const string Surfaces = "Assets/Formula/Racing/Surfaces";
    private const string RootName = "Formula Authored Tyre Wear";
    private static readonly string[] Reviews = { "Formula_Circuit_01", "Formula_Circuit_02", "Formula_Circuit_06", "Formula_Circuit_08", "Formula_RTG_01" };
    private static readonly List<object> results = new List<object>();
    private static string[] scenes;
    private static int phase, index;
    private static bool running;
    private static Material rubber;

    [MenuItem("Tools/Formula Career/Surfaces/Apply Damp Asphalt And Tyre Wear")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Surface authoring requires Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene edits before authoring.");
        scenes = FormulaAssets.Current.tracks.Select(t => t.scenePath).Distinct().ToArray();
        if (scenes.Length != 18) throw new InvalidOperationException("Expected all 18 gameplay circuits.");
        Directory.CreateDirectory(Report + "/Before"); Directory.CreateDirectory(Report + "/After");
        Directory.CreateDirectory(Surfaces + "/TyreWear");
        AssetDatabase.Refresh(); results.Clear(); phase = 0; index = 0; running = true;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (phase == 0)
            {
                ReviewBefore(Reviews[index++]);
                if (index == Reviews.Length) { phase = 1; index = 0; }
            }
            else if (phase == 1)
            {
                PrepareMask(); ApplyMaterials(); rubber = PrepareRubber(); phase = 2;
            }
            else
            {
                Author(scenes[index++]);
                File.WriteAllText(Report + "/Authoring.json", JsonConvert.SerializeObject(new
                {
                    status = index == scenes.Length ? "complete" : "running", inspected = index, total = scenes.Length,
                    tracks = results, additionalRenderersPerScene = 1, additionalRealtimeCameras = 0,
                    runtimeGeneration = false, switchHardwareTested = false
                }, Formatting.Indented));
                if (index == scenes.Length)
                {
                    Stop(); Validate(); Debug.Log("FORMULA_ASPHALT_FINISH_COMPLETE: 18 circuits");
                }
            }
        }
        catch (Exception ex) { Stop(); File.WriteAllText(Report + "/Error.txt", ex.ToString()); Debug.LogException(ex); }
    }

    private static void Stop() { running = false; EditorApplication.update -= Tick; }

    // Preserve the existing texture GUID and Switch importer override. Its RGB
    // stays zero (dielectric asphalt); only alpha describes the water sheen.
    public static Texture2D PrepareMask()
    {
        const string path = Surfaces + "/Textures/Asphalt Smoothness.png";
        var texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false, true);
        try
        {
            var pixels = new Color32[1024 * 1024];
            for (int y = 0; y < 1024; y++) for (int x = 0; x < 1024; x++)
            {
                // Periodic noise: matching edges remain seamless at repeated UVs.
                float u = x / 1024f, v = y / 1024f;
                float noise = PeriodicNoise(u, v, 4) * .7f + PeriodicNoise(u, v, 17) * .3f;
                pixels[y * 1024 + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt((.62f + .24f * noise) * 255));
            }
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float PeriodicNoise(float u, float v, float scale)
    {
        float a = Mathf.Lerp(Mathf.PerlinNoise(u * scale + 23, v * scale + 37), Mathf.PerlinNoise((u - 1) * scale + 23, v * scale + 37), u);
        float b = Mathf.Lerp(Mathf.PerlinNoise(u * scale + 23, (v - 1) * scale + 37), Mathf.PerlinNoise((u - 1) * scale + 23, (v - 1) * scale + 37), u);
        return Mathf.Lerp(a, b, v);
    }

    public static void ConfigureAsphalt(Material material, bool wet, bool aggregate)
    {
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", wet ? .90f : .76f);
        material.SetFloat("_SmoothnessTextureChannel", 0);
        material.SetFloat("_SpecularHighlights", 1); material.SetFloat("_EnvironmentReflections", 1);
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Surfaces + "/Textures/Asphalt Smoothness.png"));
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        // Keep the existing normal/detail samples and all lane markings intact.
        material.SetColor("_BaseColor", aggregate
            ? wet ? new Color(.67f, .70f, .73f) : new Color(.75f, .77f, .79f)
            : wet ? new Color(.68f, .72f, .76f) : new Color(.81f, .83f, .85f));
    }

    private static void ApplyMaterials()
    {
        var rows = new List<object>();
        foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { Surfaces + "/Materials" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            bool aggregate = path.Contains("_aggregate_");
            if (!aggregate && !path.Contains("_road_")) continue;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material.shader.name != "Universal Render Pipeline/Lit") throw new InvalidOperationException("Unexpected road shader: " + path);
            Undo.RecordObject(material, "Damp Formula asphalt"); ConfigureAsphalt(material, path.Contains("_Wet_"), aggregate);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            rows.Add(new { path, smoothnessMultiplier = material.GetFloat("_Smoothness"), shader = material.shader.name });
        }
        File.WriteAllText(Report + "/Materials.json", JsonConvert.SerializeObject(rows, Formatting.Indented));
    }

    private static Material PrepareRubber()
    {
        const string texturePath = Surfaces + "/TyreWear/Tread Coverage.png";
        var texture = new Texture2D(64, 256, TextureFormat.RGB24, false, true);
        try
        {
            var pixels = new Color[64 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 64; x++)
            {
                float u = (x + .5f) / 64, v = (y + .5f) / 256;
                float edge = Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) / .16f);
                float grain = .70f + .30f * Mathf.PerlinNoise(u * 27 + 7, v * 43 + 11);
                float groove = 1;
                foreach (float center in new[] { .28f, .50f, .72f }) groove *= Mathf.Lerp(.24f, 1, Mathf.SmoothStep(0, 1, Mathf.Abs(u - center) / .035f));
                // Faint oblique tread breaks, softened enough to survive mipmaps.
                float diagonal = Mathf.Abs(Mathf.Sin((v * 16 + Mathf.Abs(u - .5f) * 2) * Mathf.PI));
                float tread = edge * grain * groove * Mathf.Lerp(.62f, 1, Mathf.SmoothStep(.10f, .35f, diagonal));
                pixels[y * 64 + x] = new Color(tread, tread, tread, 1);
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.sRGBTexture = false; importer.isReadable = false; importer.mipmapEnabled = true;
        importer.wrapModeU = TextureWrapMode.Clamp; importer.wrapModeV = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4; importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var settings = importer.GetPlatformTextureSettings("Nintendo Switch");
        settings.name = "Nintendo Switch"; settings.overridden = true; settings.maxTextureSize = 256;
        settings.format = TextureImporterFormat.ASTC_6x6; settings.compressionQuality = 80;
        settings.textureCompression = TextureImporterCompression.Compressed; settings.crunchedCompression = false;
        importer.SetPlatformTextureSettings(settings); importer.SaveAndReimport();
        const string path = Surfaces + "/TyreWear/Track Rubber Wear.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Formula/Track Rubber Wear");
        if (shader == null) throw new InvalidOperationException("Rubber shader missing.");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetFloat("_Strength", .62f); material.renderQueue = 2980;
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
    }

    private static void Author(string path)
    {
        var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Scene has unsaved edits: " + path);
            string backup = Path.Combine(Path.GetTempPath(), "formula-asphalt", "Scenes", Path.GetFileName(path));
            Directory.CreateDirectory(Path.GetDirectoryName(backup)); if (!File.Exists(backup)) File.Copy(path, backup);
            var circuit = Components<FormulaCircuit>(scene).Single();
            var existing = scene.GetRootGameObjects().Where(r => r.name == RootName).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("Duplicate authored wear roots: " + path);
            var root = existing.SingleOrDefault();
            if (root == null) { root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene); Undo.RegisterCreatedObjectUndo(root, "Author Formula tyre wear"); }
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            var filter = root.GetComponent<MeshFilter>(); if (filter == null) filter = Undo.AddComponent<MeshFilter>(root);
            var renderer = root.GetComponent<MeshRenderer>(); if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(root);
            var mesh = BuildWear(circuit, out int samples, out int missed, out float length, out float maxProjection);
            string meshPath = Surfaces + "/TyreWear/" + Path.GetFileNameWithoutExtension(path) + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) { saved = mesh; AssetDatabase.CreateAsset(saved, meshPath); }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
            EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved);
            Undo.RecordObjects(new Object[] { filter, renderer }, "Set Formula tyre wear mesh");
            filter.sharedMesh = saved; renderer.sharedMaterial = rubber;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            results.Add(new { scene = path, samples, missedSamples = missed, coverage = 1f - missed / (float)samples,
                routeMetres = length, vertexCount = saved.vertexCount, indices = saved.GetIndexCount(0), maxProjectionMetres = maxProjection,
                mesh = meshPath, extraForwardPasses = 1, cpuReadable = saved.isReadable });
            if (Reviews.Contains(Path.GetFileNameWithoutExtension(path))) Capture(scene, Report + "/After/" + Path.GetFileNameWithoutExtension(path));
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static Mesh BuildWear(FormulaCircuit circuit, out int count, out int missed, out float length, out float maxProjection)
    {
        var points = circuit.route.waypoints.Select(w => w.transform.position).ToArray();
        var centers = new List<Vector3>(); var distances = new List<float>(); var cornering = new List<float>(); length = 0;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 a = points[(i + points.Length - 1) % points.Length], b = points[i], c = points[(i + 1) % points.Length], d = points[(i + 2) % points.Length];
            float segmentLength = Vector3.Distance(b, c); int steps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / 1.8f));
            float corner = Mathf.Clamp01(Vector3.Angle(b - a, c - b) / 40f);
            for (int j = 0; j < steps; j++)
            {
                float t = j / (float)steps, t2 = t * t, t3 = t2 * t;
                Vector3 straight = Vector3.Lerp(b, c, t);
                Vector3 curve = .5f * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
                centers.Add(straight + Vector3.ClampMagnitude(curve - straight, circuit.halfWidth * .22f));
                distances.Add(length + segmentLength * t); cornering.Add(corner);
            }
            length += segmentLength;
        }
        count = centers.Count; missed = 0; maxProjection = 0;
        if (count < 8 || (count + 1) * 4 > 32000) throw new InvalidOperationException("Wear mesh outside its vertex budget: " + circuit.trackId);
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color32>(); var indices = new List<int>();
        var valid = new bool[count];
        for (int i = 0; i <= count; i++)
        {
            int k = i % count;
            Vector3 forward = centers[(k + 1) % count] - centers[(k + count - 1) % count]; forward.y = 0;
            Vector3 side = Vector3.Cross(Vector3.up, forward.normalized);
            float distance = i == count ? length : distances[k];
            float wander = .22f * Mathf.Sin(2 * Mathf.PI * distances[k] / length * 7);
            float patch = .5f + .5f * Mathf.Sin(distances[k] * .079f + Mathf.Sin(distances[k] * .023f));
            // Continuous faint wear, interrupted stronger deposits near corners.
            float coverage = .11f + patch * .17f + cornering[k] * .22f;
            bool supported = true;
            for (int tyre = 0; tyre < 2; tyre++) for (int edge = 0; edge < 2; edge++)
            {
                float offset = (tyre == 0 ? -.74f : .74f) + wander + (edge == 0 ? -.16f : .16f);
                Vector3 requested = centers[k] + side * offset;
                if (!Road(circuit.roadColliders, requested, out Vector3 ground, out Vector3 normal, out float projection)) supported = false;
                maxProjection = Mathf.Max(maxProjection, projection);
                vertices.Add(ground + normal * .012f);
                uv.Add(new Vector2(edge, distance / length * Mathf.Max(1, Mathf.Round(length / 2.8f)))); colors.Add(new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255)));
            }
            if (i < count) { valid[i] = supported; if (!supported) missed++; }
        }
        for (int i = 0; i < count; i++)
        {
            if (!valid[i] || !valid[(i + 1) % count]) continue;
            for (int tyre = 0; tyre < 2; tyre++)
            {
                int a = i * 4 + tyre * 2, b = (i + 1) * 4 + tyre * 2;
                indices.Add(a); indices.Add(b); indices.Add(a + 1); indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
            }
        }
        if (missed > count * .02f) throw new InvalidOperationException("Road projection misses more than 2% of " + circuit.trackId + ": " + missed + "/" + count);
        var mesh = new Mesh { name = circuit.trackId + " Authored Tyre Wear", indexFormat = IndexFormat.UInt16 };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
        mesh.UploadMeshData(true); return mesh;
    }

    private static bool Road(Collider[] roads, Vector3 requested, out Vector3 ground, out Vector3 normal, out float difference)
    {
        ground = requested; normal = Vector3.up; difference = float.PositiveInfinity;
        foreach (var collider in roads)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            var ray = new Ray(requested + Vector3.up * 5, Vector3.down); float remaining = 12;
            for (int deck = 0; deck < 8 && remaining > 0; deck++)
            {
                if (!collider.Raycast(ray, out var hit, remaining)) break;
                float delta = Mathf.Abs(hit.point.y - requested.y);
                if (hit.normal.y > .45f && delta < difference) { difference = delta; ground = hit.point; normal = hit.normal; }
                remaining -= hit.distance + .02f; ray.origin = hit.point + Vector3.down * .02f;
            }
        }
        if (float.IsPositiveInfinity(difference)) { difference = 0; return false; }
        return difference < 4;
    }

    private static void ReviewBefore(string name)
    {
        var scene = SceneManager.GetSceneByPath("Assets/Formula/Scenes/" + name + ".unity"); bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene("Assets/Formula/Scenes/" + name + ".unity", OpenSceneMode.Additive);
        try { Capture(scene, Report + "/Before/" + name); }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void Capture(Scene scene, string path)
    {
        var circuit = Components<FormulaCircuit>(scene).Single();
        var previousScene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
        var go = new GameObject("Asphalt inspection camera", typeof(Camera), typeof(UniversalAdditionalCameraData)); SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.allowHDR = true; camera.fieldOfView = 62; camera.aspect = 16f / 9;
        camera.farClipPlane = 1800; camera.clearFlags = CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); var old = RenderTexture.active;
        try
        {
            for (int view = 0; view < 2; view++)
            {
                int index = view == 0 ? 0 : circuit.route.waypoints.Count / 3;
                Vector3 focus = circuit.route.waypoints[index].transform.position;
                Vector3 forward = circuit.route.waypoints[(index + 1) % circuit.route.waypoints.Count].transform.position - focus; forward.y = 0; forward.Normalize();
                camera.transform.position = focus - forward * (view == 0 ? 8 : 6) + Vector3.up * (view == 0 ? 3.5f : 1.8f);
                camera.transform.LookAt(focus + forward * (view == 0 ? 12 : 9));
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(path + "-" + view + ".png", image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
        }
        finally { RenderTexture.active = old; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene); }
    }

    private static IEnumerable<T> Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    [MenuItem("Tools/Formula Career/Surfaces/Validate Damp Asphalt Assets")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validate authored assets in Edit Mode.");
        var failures = new List<string>(); int roads = 0, meshes = 0;
        foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { Surfaces + "/Materials" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (!path.Contains("_road_") && !path.Contains("_aggregate_")) continue;
            roads++; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material.shader.name != "Universal Render Pipeline/Lit" || material.GetFloat("_Metallic") != 0 || !material.IsKeywordEnabled("_METALLICSPECGLOSSMAP") || material.GetTexture("_MetallicGlossMap") == null)
                failures.Add("Invalid damp asphalt: " + path);
            if (ShaderUtil.GetShaderMessages(material.shader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)) failures.Add("Road shader errors: " + path);
        }
        foreach (string path in AssetDatabase.FindAssets("t:Mesh", new[] { Surfaces + "/TyreWear" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            meshes++; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh.vertexCount > 32000 || mesh.subMeshCount != 1 || mesh.GetIndexCount(0) == 0 || mesh.isReadable) failures.Add("Wear mesh budget/readability: " + path);
        }
        var rubber = AssetDatabase.LoadAssetAtPath<Material>(Surfaces + "/TyreWear/Track Rubber Wear.mat");
        if (rubber == null || rubber.passCount != 1 || ShaderUtil.GetShaderMessages(rubber.shader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)) failures.Add("Rubber shader missing, invalid or has extra passes.");
        foreach (string path in new[] { Surfaces + "/Textures/Asphalt Smoothness.png", Surfaces + "/TyreWear/Tread Coverage.png" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); var settings = importer.GetPlatformTextureSettings("Nintendo Switch");
            if (importer.isReadable || !importer.mipmapEnabled || importer.sRGBTexture || !settings.overridden || settings.format != TextureImporterFormat.ASTC_6x6) failures.Add("Texture importer budget: " + path);
        }
        if (meshes != 18) failures.Add("Expected 18 wear meshes; found " + meshes);
        var bindings = new List<object>();
        foreach (string path in FormulaAssets.Current.tracks.Select(t => t.scenePath).Distinct())
        {
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects().Where(r => r.name == RootName).ToArray();
                var renderer = roots.Length == 1 ? roots[0].GetComponent<MeshRenderer>() : null;
                var filter = roots.Length == 1 ? roots[0].GetComponent<MeshFilter>() : null;
                if (renderer == null || filter == null || filter.sharedMesh == null || renderer.sharedMaterial != rubber ||
                    roots[0].GetComponents<Component>().Length != 3 || renderer.shadowCastingMode != ShadowCastingMode.Off ||
                    renderer.receiveShadows || filter.sharedMesh.subMeshCount != 1 || filter.sharedMesh.isReadable)
                    failures.Add("Persisted tyre wear binding invalid: " + path);
                int asphaltSlots = Components<MeshRenderer>(scene).Where(r => r.enabled && r.gameObject.activeInHierarchy)
                    .SelectMany(r => r.sharedMaterials).Count(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(Surfaces + "/Materials/", StringComparison.Ordinal) &&
                        (AssetDatabase.GetAssetPath(m).Contains("_road_") || AssetDatabase.GetAssetPath(m).Contains("_aggregate_")));
                if (asphaltSlots == 0) failures.Add("No active authored asphalt found: " + path);
                bindings.Add(new { scene = path, asphaltSlots, tyreRenderers = roots.Length, mesh = filter == null ? null : AssetDatabase.GetAssetPath(filter.sharedMesh) });
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "/Validation.json", JsonConvert.SerializeObject(new { status = failures.Count == 0 ? "passed" : "failed", roads, meshes,
            unity = Application.unityVersion, rubberPasses = rubber == null ? 0 : rubber.passCount, sceneBindings = bindings, switchHardwareTested = false, failures }, Formatting.Indented));
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
    }
}
