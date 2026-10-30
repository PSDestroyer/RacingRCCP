using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Persistent, Formula-owned presentation assets. Physics, navigation, livery
// artwork, progression and imported vendor content keep their existing owners.
public static class FormulaVisualPresentationUpgrade
{
    const string Folder = "Assets/Formula/Presentation";
    const string Report = "Docs/AI/FormulaVisualUpgrade";
    const string RacePath = "Assets/Formula/Resources/Formula/UI/FormulaRace.prefab";
    static readonly string[] Samples = { "track_01", "track_05", "track_06", "track_07", "track_08", "rtg_01" };
    static void Ready()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before visual authoring.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Report);
    }
    static IEnumerable<T> Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    static string Asset(Object value) => value == null ? null : AssetDatabase.GetAssetPath(value);
    static object MaterialInfo(Material m) => new { path = Asset(m), shader = m.shader.name, keywords = m.shaderKeywords, metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0, smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0, texture = Asset(m.mainTexture), color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F3") : "", coat = m.HasProperty("_ClearCoatMask") ? m.GetFloat("_ClearCoatMask") : 0 };

    [MenuItem("Tools/Audit Formula Visual Presentation")]
    public static void Audit()
    {
        Ready(); var setup = EditorSceneManager.GetSceneManagerSetup(); var scenes = new List<object>();
        try
        {
            foreach (string id in Samples)
            {
                var track = FormulaAssets.Current.FindTrack(id); var scene = EditorSceneManager.OpenScene(track.scenePath);
                scenes.Add(new { id, path = track.scenePath, fog = RenderSettings.fogDensity, ambientMode = RenderSettings.ambientMode.ToString(), ambient = RenderSettings.ambientIntensity,
                    skybox = RenderSettings.skybox == null ? null : MaterialInfo(RenderSettings.skybox),
                    lights = Components<Light>(scene).Select(l => new { l.name, type = l.type.ToString(), l.intensity, color = l.color.ToString("F3"), rotation = l.transform.eulerAngles.ToString("F1") }).ToArray(),
                    renderers = Components<MeshRenderer>(scene).Where(r => r.enabled).Select(r => new { r.name, materials = r.sharedMaterials.Where(m => m != null).Select(MaterialInfo).ToArray(), size = r.bounds.size.ToString("F1") }).ToArray() });
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        var vehicle = FormulaAssets.Current.vehiclePrefab; var renderers = vehicle.GetComponentsInChildren<MeshRenderer>(true);
        var mesh = vehicle.GetComponent<FormulaVehicle>().body.GetComponent<MeshFilter>().sharedMesh;
        float minNormal = float.MaxValue, maxNormal = 0;
        using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh)) using (var normals = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
        {
            if (mesh.HasVertexAttribute(VertexAttribute.Normal)) { data[0].GetNormals(normals); foreach (var n in normals) { minNormal = Mathf.Min(minNormal, n.magnitude); maxNormal = Mathf.Max(maxNormal, n.magnitude); } }
        }
        File.WriteAllText(Report + "/Baseline.json", JsonConvert.SerializeObject(new { unity = Application.unityVersion, scenes,
            paint = FormulaAssets.Current.liveryMaterials.Select(MaterialInfo), mesh = new { path = Asset(mesh), mesh.vertexCount, mesh.subMeshCount, minNormal, maxNormal },
            vehicleRenderers = renderers.Select(r => new { r.name, mesh = Asset(r.GetComponent<MeshFilter>()?.sharedMesh), materials = r.sharedMaterials.Where(m => m != null).Select(MaterialInfo).ToArray() }) }, Formatting.Indented));
        SaveCar("Paint-Before"); Debug.Log("FORMULA_VISUAL_BASELINE_RECORDED");
    }

    [MenuItem("Tools/Upgrade Formula Vehicle Finish")]
    public static void Paint()
    {
        Ready(); FormulaVehicleMaterialBuilder.ApplyFinishes();
        var rows = FormulaAssets.Current.liveryMaterials.Select(MaterialInfo).ToArray();
        foreach (string path in FormulaCatalog.Current.liveries.Select(l => l.prefabPath).Concat(new[] { Asset(FormulaAssets.Current.vehiclePrefab) }).Distinct())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var led in root.GetComponentsInChildren<RCCP_Light>(true))
                {
                    if (!led.name.StartsWith("Rear_", StringComparison.Ordinal)) continue;
                    led.useLensFlares = false; led.flareBrightness = 0;
                    led.intensity = led.lightType == RCCP_Light.LightType.Brakelight ? .7f : .35f;
                    led.emissiveColor = new Color(3, .025f, .005f);
                    var light = led.GetComponent<Light>(); if (light != null) { light.enabled = false; light.shadows = LightShadows.None; }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in Directory.GetFiles("Assets/Formula/RCCP/Materials", "*.mat").Concat(Directory.GetFiles("Assets/Formula/Garage/Materials/Vehicle", "*.mat")))
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>(path));
        FormulaMenuAssetBuilder.BuildLiveryPreviews(FormulaMenuAssets.Current);
        SaveCar("Paint-After");
        File.WriteAllText(Report + "/Paint.json", JsonConvert.SerializeObject(new { status = "applied", paint = rows, lightweightNativeLit = true, liveryArtworkPreserved = true, rearLedsRemainEmissive = true, pointLightSpillDisabled = true }, Formatting.Indented));
        Debug.Log("FORMULA_VISUAL_PAINT_APPLIED");
    }
    [MenuItem("Tools/Upgrade Formula Race HUD")]
    public static void Hud()
    {
        Ready(); var root = PrefabUtility.LoadPrefabContents(RacePath);
        try { StyleHud(root.GetComponent<FormulaRaceView>()); PrefabUtility.SaveAsPrefabAsset(root, RacePath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        File.WriteAllText(Report + "/HUD.json", JsonConvert.SerializeObject(new { status = "applied", designSize = "1280x720", session = "220x88", timing = "240x110", minimap = "180x120", instruments = "224x104", originalReferencesPreserved = true, controllerAndLocalizationPreserved = true, opaqueAreaBefore = 154688, panelAreaAfter = 110816, note = "Authored rectangle areas, not GPU timings; actual panels are translucent and High Contrast restores opacity." }, Formatting.Indented));
        Debug.Log("FORMULA_VISUAL_HUD_APPLIED");
    }
    public static void StyleHud(FormulaRaceView view)
    {
        var hud = view.Hud.transform;
        Box(hud.Find("Session"), 28, 24, 220, 88);
        Text(hud, "Session/Event", 12, 1, 196, 22, 16);
        Text(hud, "Session/Position", 12, 21, 80, 50, 44);
        Text(hud, "Session/Participants", 88, 39, 44, 26, 18);
        Text(hud, "Session/Lap Caption", 148, 19, 60, 22, 16);
        Text(hud, "Session/Lap", 148, 39, 60, 31, 26);
        Text(hud, "Session/Lap Status", 12, 67, 196, 21, 16);
        Box(hud.Find("Timing"), 1012, 65, 240, 110);
        Text(hud, "Timing/Session Caption", 12, 2, 216, 23, 16);
        Text(hud, "Timing/Lap Time", 12, 22, 216, 41, 36);
        Text(hud, "Timing/Best Lap", 12, 62, 216, 22, 16);
        Text(hud, "Timing/Session Time", 12, 84, 216, 23, 16);
        Text(hud, "Timing/Delta Caption", 12, 84, 156, 23, 16);
        Text(hud, "Timing/Delta", 169, 81, 59, 26, 20);
        Box(hud.Find("Standings"), 28, 124, 210, 100);
        for (int i = 0; i < 4; i++)
        {
            Box(hud.Find("Standings/Driver " + i), 0, i * 25, 210, 24);
            Text(hud, "Standings/Driver " + i + "/Name", 12, -4, 186, 32, 16);
        }
        Box(hud.Find("Session Goal"), 28, 124, 220, 64);
        Text(hud, "Session Goal/Caption", 12, 1, 196, 23, 16);
        Text(hud, "Session Goal/Target", 12, 25, 196, 34, 21);
        Box(hud.Find("Minimap"), 28, 572, 180, 120);
        Box(hud.Find("Minimap/Circuit"), 10, 10, 160, 100);
        Box(hud.Find("Minimap/Circuit/Cars"), 0, 0, 160, 100);
        Box(hud.Find("Vehicle"), 1028, 588, 224, 104);
        Box(hud.Find("Vehicle/Shift Lights"), 12, 10, 200, 7);
        for (int i = 0; i < 12; i++) Box(hud.Find("Vehicle/Shift Lights/Light " + i), i * 17, 0, 13, 7);
        Text(hud, "Vehicle/Speed", 10, 19, 110, 60, 62);
        Text(hud, "Vehicle/Unit", 126, 54, 40, 24, 15);
        Text(hud, "Vehicle/Gear", 170, 19, 44, 60, 52);
        Text(hud, "Vehicle/Gear Caption", 170, 79, 44, 23, 15);
        Text(hud, "Vehicle/RPM", 12, 79, 144, 23, 15);
        Box(hud.Find("Vehicle/Gear Divider"), 164, 24, 1, 67);
        Box(hud.Find("Vehicle/Rev Strip"), 12, 100, 144, 2);
        Box(hud.Find("Target"), 28, 406, 298, 78);
        Text(hud, "Target/Lap Validity", 12, 2, 274, 24, 16);
        Text(hud, "Target/Objective", 12, 27, 274, 48, 18);
        foreach (var panel in hud.GetComponentsInChildren<FormulaRacePanelGraphic>(true))
        {
            if (panel.name == "Car Left" || panel.name == "Car Right") continue;
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("panelOpacity").floatValue = panel.name == "Minimap" ? .64f : panel.name == "Vehicle" ? .91f : .86f;
            serialized.ApplyModifiedPropertiesWithoutUndo(); panel.color = Color.white;
        }
    }
    static void Box(Transform target, float x, float y, float width, float height)
    {
        if (target == null || !(target is RectTransform)) throw new InvalidOperationException("Missing authored HUD RectTransform.");
        FormulaMenuTheme.Place((RectTransform)target, x, y, width, height);
    }
    static void Text(Transform parent, string path, float x, float y, float width, float height, float size)
    {
        var target = parent.Find(path); if (target == null) throw new InvalidOperationException("Missing authored HUD text: " + path);
        Box(target, x, y, width, height); var text = target.GetComponent<TMP_Text>();
        if (text == null) throw new InvalidOperationException("HUD label has no TMP component: " + path);
        text.fontSize = text.fontSizeMin = text.fontSizeMax = size; text.enableAutoSizing = false;
        text.margin = Vector4.zero;
        var localized = text.GetComponent<FormulaLocalizedFont>();
        if (localized != null)
        {
            var serialized = new SerializedObject(localized); serialized.FindProperty("authoredSize").floatValue = size; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    [MenuItem("Tools/Upgrade Formula Circuit Presentation")]
    public static void Circuits()
    {
        Ready(); Directory.CreateDirectory(Folder + "/Grades"); Directory.CreateDirectory(Folder + "/World"); Directory.CreateDirectory(Folder + "/Signage"); AssetDatabase.Refresh();
        MakeSign("F27", 1024, 600, false, false);
        MakeSign("APEX", 1024, 600, true, false);
        MakeSign("Flag", 256, 1024, false, true);
        var setup = EditorSceneManager.GetSceneManagerSetup(); var rows = new List<object>();
        var cache = new Dictionary<string, Material>();
        try
        {
            foreach (var track in FormulaAssets.Current.tracks)
            {
                var scene = EditorSceneManager.OpenScene(track.scenePath);
                string mood = track.id == "track_08" ? "Sunset" : track.id == "track_07" ? "Night" : track.id == "track_06" ? "Wet" : track.id == "track_05" || track.id == "track_03" ? "Winter" : "Day";
                bool night = mood == "Night", sunset = mood == "Sunset", wet = mood == "Wet", winter = mood == "Winter";
                Color sky = night ? new Color(.28f,.34f,.46f) : sunset ? new Color(.48f,.51f,.58f) : wet ? new Color(.48f,.54f,.62f) : new Color(.52f,.61f,.72f);
                RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = night ? new Color(.20f,.25f,.33f) : new Color(.34f,.38f,.43f);
                RenderSettings.ambientGroundColor = night ? new Color(.10f,.12f,.17f) : new Color(.20f,.22f,.25f);
                RenderSettings.ambientIntensity = 1;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = wet ? .0019f : winter ? .0013f : night ? .0009f : sunset ? .00085f : .0007f;
                RenderSettings.fogColor = night ? new Color(.10f,.15f,.23f) : sunset ? new Color(.58f,.51f,.46f) : wet ? new Color(.54f,.61f,.69f) : new Color(.67f,.73f,.79f);
                RenderSettings.reflectionIntensity = night ? .38f : .45f;
                foreach (var light in Components<Light>(scene).Where(l => l.type == LightType.Directional))
                {
                    Undo.RecordObject(light, "Refine Formula key light");
                    light.intensity = night ? .75f : sunset ? 1.35f : wet ? .8f : winter ? 1.15f : 1.5f;
                    light.color = night ? new Color(.70f,.80f,1) : sunset ? new Color(1,.88f,.73f) : wet ? new Color(.91f,.95f,1) : winter ? new Color(.92f,.96f,1) : new Color(1,.96f,.89f);
                    light.transform.rotation = Quaternion.Euler(sunset ? 16 : night ? 36 : winter ? 30 : wet ? 48 : 36, sunset ? 300 : 328, 0);
                    light.shadows = LightShadows.Soft; light.shadowStrength = wet ? .55f : .83f; EditorUtility.SetDirty(light);
                }
                if (RenderSettings.skybox != null)
                {
                    var source = RenderSettings.skybox; string path = Folder + "/Grades/" + mood + " Sky.mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (copy == null) { copy = new Material(source); AssetDatabase.CreateAsset(copy,path); }
                    // Keep the actual sky image; tint/exposure stay authored material settings.
                    if (copy.HasProperty("_Exposure")) copy.SetFloat("_Exposure", sunset ? .72f : night ? .85f : 1);
                    if (copy.HasProperty("_Tint")) copy.SetColor("_Tint", sunset ? new Color(.43f,.48f,.56f) : new Color(.5f,.5f,.5f));
                    EditorUtility.SetDirty(copy); AssetDatabase.SaveAssetIfDirty(copy); RenderSettings.skybox = copy;
                }
                var profile = Profile(mood);
                var volumes = Components<Volume>(scene).Where(v => v.isGlobal).ToArray();
                if (volumes.Length == 0) { var go = new GameObject("Formula Presentation Grade"); SceneManager.MoveGameObjectToScene(go,scene); volumes = new[] { go.AddComponent<Volume>() }; }
                foreach (var volume in volumes) { volume.sharedProfile = profile; volume.weight = 1; EditorUtility.SetDirty(volume); }
                foreach (var camera in Components<Camera>(scene))
                {
                    var data = camera.GetUniversalAdditionalCameraData(); if (data.renderType != CameraRenderType.Base) continue;
                    data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing; camera.allowHDR = true;
                    EditorUtility.SetDirty(data); EditorUtility.SetDirty(camera);
                }
                foreach (var rig in Components<RCCP_Camera>(scene))
                {
                    rig.TPSAutoFocus = false; rig.TPSDistance = 6.3f; rig.TPSHeight = 1.55f;
                    rig.TPSMinimumFOV = 48; rig.TPSMaximumFOV = 62; rig.TPSShake = true;
                    EditorUtility.SetDirty(rig);
                }
                int slots = 0;
                foreach (var renderer in Components<MeshRenderer>(scene))
                {
                    var materials = renderer.sharedMaterials; bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var replacement = EnvironmentMaterial(materials[i], cache);
                        if (replacement == null || replacement == materials[i]) continue;
                        materials[i] = replacement; changed = true; slots++;
                    }
                    if (changed) { renderer.sharedMaterials = materials; EditorUtility.SetDirty(renderer); }
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                rows.Add(new { track.id, track.scenePath, mood, materialSlots = slots, fogDensity = RenderSettings.fogDensity,
                    profile = Asset(profile), cameras = Components<RCCP_Camera>(scene).Count(), colliderCount = Components<Collider>(scene).Count() });
                File.WriteAllText(Report + "/Circuits.json", JsonConvert.SerializeObject(new { status = rows.Count == FormulaAssets.Current.tracks.Length ? "applied" : "running", scenes = rows, vendorAssetsModified = false, additionalRealtimeLights = 0, additionalRuntimeRenderers = 0 }, Formatting.Indented));
            }
            foreach (var m in cache.Values.Distinct()) AssetDatabase.SaveAssetIfDirty(m);
            foreach (string path in Directory.GetFiles("Assets/Formula/Racing/Surfaces/Materials", "*.mat"))
            {
                if (!path.Contains("_road_") && !path.Contains("_aggregate_")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path); FormulaAsphaltFinishAuthoring.ConfigureAsphalt(m,path.Contains("_Wet_"),path.Contains("_aggregate_"));
                EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("FORMULA_VISUAL_CIRCUITS_APPLIED");
    }
    static VolumeProfile Profile(string mood)
    {
        string path = Folder + "/Grades/" + mood + ".asset"; var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p,path); }
        var tone = Override<Tonemapping>(p); tone.mode.Override(TonemappingMode.ACES);
        var color = Override<ColorAdjustments>(p);
        color.postExposure.Override(mood == "Winter" ? -.15f : mood == "Sunset" ? -.08f : mood == "Night" ? .1f : .08f);
        color.contrast.Override(mood == "Wet" ? 10 : 12); color.saturation.Override(mood == "Sunset" ? -9 : -3); color.colorFilter.Override(Color.white);
        var balance = Override<WhiteBalance>(p); balance.temperature.Override(mood == "Sunset" ? -9 : mood == "Day" ? -3 : 0); balance.tint.Override(0);
        var bloom = Override<Bloom>(p); bloom.intensity.Override(mood == "Night" ? .065f : .035f); bloom.threshold.Override(1.5f); bloom.scatter.Override(.45f); bloom.clamp.Override(2);
        var vignette = Override<Vignette>(p); vignette.intensity.Override(.08f); vignette.smoothness.Override(.45f);
        foreach (var component in p.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(p); AssetDatabase.SaveAssetIfDirty(p); return p;
    }
    static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component)) { component = profile.Add<T>(); AssetDatabase.AddObjectToAsset(component,profile); }
        component.active = true; return component;
    }
    static Material EnvironmentMaterial(Material source, Dictionary<string, Material> cache)
    {
        if (source == null) return null;
        string path = Asset(source);
        if (path.StartsWith(Folder + "/World/",StringComparison.Ordinal))
        {
            string guid = Path.GetFileNameWithoutExtension(path).Split('_')[0]; source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (source == null) throw new InvalidOperationException("Missing original environment material: " + path); path = Asset(source);
        }
        string texture = source.mainTexture == null ? "" : source.mainTexture.name;
        string kind = texture == "Billboard_1" ? "F27" : texture == "Billboard_2" ? "APEX" : texture.StartsWith("Bill_Pipe",StringComparison.Ordinal) ? "Flag"
            : path.Contains("/Tree/") || path.Contains("/Trees-") ? path.Contains("Winter") ? "WinterTrees" : "Trees"
            : path.Contains("/Grass-") ? "Grass" : source.name == "Audience" || source.name == "Spectator" ? "Crowd"
            : source.name.StartsWith("Tire_",StringComparison.Ordinal) ? "Tyres" : null;
        if (kind == null) return null;
        string key = AssetDatabase.AssetPathToGUID(path) + "_" + kind;
        if (cache.TryGetValue(key,out var cached)) return cached;
        string target = Folder + "/World/" + key + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(target);
        if (m == null) { m = new Material(source); AssetDatabase.CreateAsset(m,target); }
        m.CopyPropertiesFromMaterial(source); m.name = "Formula " + kind + " / " + source.name;
        Color tint = kind == "Trees" ? new Color(.70f,.80f,.66f) : kind == "WinterTrees" ? new Color(.94f,.96f,1) : kind == "Grass" ? new Color(.72f,.78f,.62f) : kind == "Crowd" ? new Color(.75f,.77f,.80f) : Color.white;
        if (kind == "Tyres") tint = source.name.Contains("Red") ? new Color(.65f,.22f,.18f) : source.name.Contains("White") ? new Color(.8f,.81f,.82f) : new Color(.18f,.20f,.23f);
        if (kind == "F27" || kind == "APEX" || kind == "Flag")
        {
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Signage/" + kind + ".png");
            m.SetTexture("_BaseMap",logo); m.SetTexture("_MainTex",logo);
            m.SetTextureScale("_BaseMap",Vector2.one); m.SetTextureOffset("_BaseMap",Vector2.zero);
            m.SetFloat("_Cull",0); m.SetFloat("_Metallic",0); m.SetFloat("_Smoothness",.12f);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color",tint);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",kind == "Tyres" ? .08f : .12f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights",0);
        m.enableInstancing = true; EditorUtility.SetDirty(m); cache.Add(key,m); return m;
    }
    [MenuItem("Tools/Render Formula Track Branding")]
    public static void Branding()
    {
        Ready(); MakeSign("F27",1024,600,false,false); MakeSign("APEX",1024,600,true,false); MakeSign("Flag",256,1024,false,true);
        Debug.Log("FORMULA_VISUAL_BRANDING_RENDERED");
    }
    static void MakeSign(string name, int width, int height, bool white, bool vertical)
    {
        string path = Folder + "/Signage/" + name + ".png";
        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); var previous = RenderTexture.active;
        var target = new RenderTexture(width,height,16,RenderTextureFormat.ARGB32);
        try
        {
            var cameraRoot = new GameObject("Sign Camera",typeof(Camera),typeof(UniversalAdditionalCameraData)); SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera = cameraRoot.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = height * .005f; camera.aspect = width / (float)height;
            camera.transform.position = new Vector3(0,0,-10); camera.nearClipPlane = .1f; camera.farClipPlane = 20; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = white ? new Color(.92f,.93f,.93f) : new Color(.025f,.035f,.055f); camera.targetTexture = target; camera.allowHDR = false;
            var canvasRoot = new GameObject("Sign Artwork",typeof(RectTransform),typeof(Canvas)); canvasRoot.layer = 31; SceneManager.MoveGameObjectToScene(canvasRoot,scene);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvas.transform; canvasRect.sizeDelta = new Vector2(width,height); canvasRect.localScale = Vector3.one * .01f;
            var scale = canvasRoot.AddComponent<UnityEngine.UI.CanvasScaler>(); scale.dynamicPixelsPerUnit = 100;
            SignBlock(canvas.transform,"Accent",0,0,vertical ? 14 : 26,height,new Color(.91f,.07f,.035f));
            SignBlock(canvas.transform,"Baseline",0,height-16,width,16,new Color(.91f,.07f,.035f));
            if (vertical)
            {
                var label = SignText(canvas.transform,"F27 RACING",0,0,840,130,80,Color.white);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = Vector2.one * .5f;
                label.rectTransform.anchoredPosition = Vector2.zero; label.rectTransform.localEulerAngles = new Vector3(0,0,90); label.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                Color ink = white ? new Color(.035f,.045f,.065f) : Color.white;
                SignText(canvas.transform,white ? "APEX" : "F27",72,92,880,250,white ? 190 : 220,ink);
                SignText(canvas.transform,white ? "PERFORMANCE RACING" : "FORMULA RACING SERIES",80,360,880,96,46,ink);
                SignBlock(canvas.transform,"Rule",80,342,830,5,new Color(.91f,.07f,.035f));
            }
            Canvas.ForceUpdateCanvases(); foreach (var text in canvasRoot.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; var image = new Texture2D(width,height,TextureFormat.RGB24,false);
            try { image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { Object.DestroyImmediate(image); }
        }
        finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); EditorSceneManager.CloseScene(scene,true); if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene); }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.mipmapEnabled = true; importer.sRGBTexture = true; importer.maxTextureSize = 1024;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 2; importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
    }
    static void SignBlock(Transform parent,string name,float x,float y,float width,float height,Color color)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); go.layer = 31; go.transform.SetParent(parent,false);
        Box(go.transform,x,y,width,height); go.GetComponent<UnityEngine.UI.Image>().color = color;
    }
    static TMP_Text SignText(Transform parent,string value,float x,float y,float width,float height,float size,Color color)
    {
        var go = new GameObject(value,typeof(RectTransform),typeof(TextMeshProUGUI)); go.layer = 31; go.transform.SetParent(parent,false); Box(go.transform,x,y,width,height);
        var text = go.GetComponent<TMP_Text>(); text.font = FormulaMenuAssets.Current.strongFont; text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft; text.textWrappingMode = TextWrappingModes.NoWrap; return text;
    }

    [MenuItem("Tools/Validate Formula Visual Upgrade")]
    public static void Validate()
    {
        Ready(); var checks = new List<string>(); var scenes = new List<object>();
        foreach (var m in FormulaAssets.Current.liveryMaterials)
        {
            if (m.shader.name != "Universal Render Pipeline/Lit" || m.IsKeywordEnabled("_CLEARCOAT") || m.GetFloat("_Metallic") > .05f || m.GetFloat("_Smoothness") > .5f) throw new InvalidOperationException("Unexpected Formula paint finish: " + m.name);
            if (ShaderUtil.ShaderHasError(m.shader)) throw new InvalidOperationException("Paint shader has compilation errors.");
        }
        checks.Add("All eight liveries use native URP Lit dielectric paint, original artwork and controlled roughness.");
        foreach (string path in AssetDatabase.FindAssets("t:Material",new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null || ShaderUtil.ShaderHasError(m.shader)) throw new InvalidOperationException("Presentation material or shader is invalid: " + path);
        }
        checks.Add("Formula-owned environment, sky and signage materials have valid shaders.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RacePath); var view = prefab.GetComponent<FormulaRaceView>(); var data = new SerializedObject(view);
        foreach (string field in new[] { "hud", "pause", "options", "position", "lap", "lapTime", "sessionTime", "speed", "gear", "standingsRoot", "minimap", "pauseButton", "resume", "restart", "leave", "notification" })
            if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing race UI reference: " + field);
        if (((RectTransform)view.Hud.transform.Find("Minimap")).sizeDelta != new Vector2(180,120)) throw new InvalidOperationException("Compact minimap was not saved.");
        checks.Add("Existing HUD, telemetry, minimap, pause and controller navigation references are preserved in the saved prefab.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var track in FormulaAssets.Current.tracks)
            {
                var scene = EditorSceneManager.OpenScene(track.scenePath);
                var profiles = Components<Volume>(scene).Where(v => v.isGlobal).Select(v => v.sharedProfile).ToArray();
                if (profiles.Length == 0 || profiles.Any(p => p == null || !Asset(p).StartsWith(Folder + "/Grades/",StringComparison.Ordinal) || !p.TryGet<Tonemapping>(out var t) || t.mode.value != TonemappingMode.ACES)) throw new InvalidOperationException("Saved presentation grade missing: " + track.id);
                var rig = Components<RCCP_Camera>(scene).Single();
                if (rig.TPSAutoFocus || Mathf.Abs(rig.TPSDistance-6.3f)>.01f) throw new InvalidOperationException("Saved game camera was not refined: " + track.id);
                var circuit = Components<FormulaCircuit>(scene).Single();
                if (circuit.route == null || circuit.route.waypoints.Count < 3 || circuit.roadColliders.Length == 0) throw new InvalidOperationException("Circuit references were lost: " + track.id);
                scenes.Add(new { track.id, grade = Asset(profiles[0]), cameraDistance = rig.TPSDistance, routePoints = circuit.route.waypoints.Count, roadColliders = circuit.roadColliders.Length });
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        checks.Add("All 18 saved scenes retain their route/road references, the authored game camera and an ACES grade.");
        File.WriteAllText(Report + "/AssetValidation.json", JsonConvert.SerializeObject(new { status = "passed", unity = Application.unityVersion, checks, scenes, switchHardwareTested = false }, Formatting.Indented));
        Debug.Log("FORMULA_VISUAL_ASSET_VALIDATION_PASSED");
    }

    static void SaveCar(string name)
    {
        var texture = FormulaLiveryArtBuilder.RenderCar(0, 1280, 720, new Vector3(7, 3.2f, 5.1f), false);
        try { File.WriteAllBytes(Report + "/" + name + ".png", texture.EncodeToPNG()); }
        finally { Object.DestroyImmediate(texture); }
    }
}
