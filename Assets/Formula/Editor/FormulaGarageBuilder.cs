using System;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Generates editable scene geometry and shared assets; no geometry generation runs in the player.
public static class FormulaGarageBuilder
{
    public const string GarageScene = "Assets/Formula/Scenes/Formula_Garage.unity";
    private const string MenuScene = "Assets/Scenes/Menu.unity";
    private const string Folder = "Assets/Formula/Garage";
    private const string StagePrefab = Folder + "/Formula_Paddock_Garage.prefab";
    private const string StageName = "Formula Paddock Garage";
    private static Material red, white, wine, yellow, floor, led, goldLed, rubber;

    [MenuItem("Tools/Formula Career/Create Formula Garage")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Scene menu = SceneManager.GetSceneByPath(MenuScene);
        if (menu.IsValid() && menu.isDirty) throw new InvalidOperationException("Save changes to Menu before generating the garage.");
        var previous = SceneManager.GetActiveScene();
        Directory.CreateDirectory(Folder + "/Materials"); AssetDatabase.Refresh();
        CreateMaterials();
        CreateStagePrefab();
        bool menuAlreadyLoaded = menu.IsValid() && menu.isLoaded;
        if (!menuAlreadyLoaded) menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
        try
        {
            ConfigureScene(menu, false);
            EditorSceneManager.SaveScene(menu);
            if (System.IO.File.Exists(FormulaMenuHierarchyBuilder.PrefabPath)) FormulaMenuHierarchyBuilder.Install(MenuScene);
            if (!File.Exists(GarageScene)) EditorSceneManager.SaveScene(menu, GarageScene, true);
        }
        finally { if (!menuAlreadyLoaded) EditorSceneManager.CloseScene(menu, true); }
        Scene garage = SceneManager.GetSceneByPath(GarageScene); bool garageLoaded = garage.IsValid() && garage.isLoaded;
        if (!garageLoaded) garage = EditorSceneManager.OpenScene(GarageScene, OpenSceneMode.Additive);
        try { ConfigureScene(garage, true); EditorSceneManager.SaveScene(garage); if (System.IO.File.Exists(FormulaMenuHierarchyBuilder.PrefabPath)) FormulaMenuHierarchyBuilder.Install(GarageScene); WriteReport(garage); }
        finally { if (!garageLoaded) EditorSceneManager.CloseScene(garage, true); }
        if (!EditorBuildSettings.scenes.Any(s => s.path == GarageScene))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(GarageScene, true) }).ToArray();
        AssetDatabase.SaveAssets();
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        Debug.Log("FORMULA_GARAGE_CREATED: " + GarageScene);
    }

    private static void CreateMaterials()
    {
        red = Material("Team Red", new Color32(200, 16, 46, 255), .1f, .38f);
        white = Material("Ceramic White", new Color32(255, 250, 242, 255), .05f, .3f);
        wine = Material("Anodized Deep Red", new Color32(51, 0, 14, 255), .3f, .38f);
        yellow = Material("Safety Yellow", new Color32(255, 218, 30, 255), .05f, .32f);
        floor = Material("Ivory Epoxy", new Color32(240, 226, 211, 255), .12f, .55f);
        rubber = Material("Service Rubber", new Color32(25, 4, 9, 255), 0, .15f);
        led = Material("White Light", Color.white, 0, .25f, Color.white * 3);
        goldLed = Material("Yellow Light", new Color32(255, 220, 35, 255), 0, .25f, new Color(1, .68f, .02f) * 2);
    }
    private static Material Material(string name, Color color, float metal, float smooth, Color? emission = null)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; }
        EditorUtility.SetDirty(m); return m;
    }
    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    private static GameObject Part(Transform parent, string name, Vector3 pos, Vector3 size, Material material, PrimitiveType type = PrimitiveType.Cube, bool collider = false)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        return go;
    }
    private static void Label(Transform parent, string name, string value, Vector3 pos, Vector2 size, Color color, float angle = 180)
    {
        var go = new GameObject(name, typeof(TextMeshPro)); go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, angle, 0);
        var t = go.GetComponent<TextMeshPro>(); t.text = value; t.font = TMP_Settings.defaultFontAsset;
        t.rectTransform.sizeDelta = size; t.enableAutoSizing = true; t.fontSizeMin = .5f; t.fontSizeMax = 24;
        t.color = color; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }
    private static void CreateStagePrefab()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = new GameObject(StageName); root.transform.position = Vector3.zero;
        try
        {
            var structure = Group(root.transform, "Architecture");
            Part(structure, "Epoxy Floor", new Vector3(0, -.15f, 0), new Vector3(20, .3f, 22), floor, collider: true);
            Part(structure, "Rear Wall", new Vector3(0, 3.25f, -7), new Vector3(20, 6.5f, .3f), white);
            Part(structure, "Left Wall", new Vector3(-9.7f, 3.25f, 0), new Vector3(.3f, 6.5f, 14), white);
            Part(structure, "Right Wall", new Vector3(9.7f, 3.25f, 0), new Vector3(.3f, 6.5f, 14), white);
            Part(structure, "Rear Team Stripe", new Vector3(0, 2.4f, -6.78f), new Vector3(19.2f, 2.9f, .12f), red);
            Part(structure, "Rear Skirting", new Vector3(0, .25f, -6.73f), new Vector3(19.2f, .5f, .18f), wine);
            Part(structure, "Ceiling", new Vector3(0, 6.6f, -.5f), new Vector3(20, .2f, 15), wine).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int i = -4; i <= 4; i++)
            {
                Part(structure, "Wall Panel Joint " + i, new Vector3(i * 2.1f, 4.9f, -6.82f), new Vector3(.035f, 2.9f, .03f), wine);
                Part(structure, "Ceiling Rib " + i, new Vector3(i * 2.1f, 6.25f, -.5f), new Vector3(.12f, .35f, 14.8f), red).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Part(structure, "Bay Pillar " + side, new Vector3(side * 8.7f, 3.15f, 4.8f), new Vector3(.45f, 6.3f, .5f), red);
                Part(structure, "Pillar Light " + side, new Vector3(side * 8.44f, 3.5f, 4.8f), new Vector3(.035f, 3.2f, .22f), led);
                Part(structure, "Wall Lower Red " + side, new Vector3(side * 9.51f, 1.4f, -.5f), new Vector3(.12f, 2.2f, 12), red);
            }
            var bay = Group(root.transform, "Service Bay Markings");
            Part(bay, "Service Pad", new Vector3(0, .006f, .2f), new Vector3(5.8f, .012f, 8.4f), white);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(bay, "Pad Red Edge " + side, new Vector3(side * 3.02f, .014f, .2f), new Vector3(.12f, .016f, 8.7f), red);
                Part(bay, "Walkway " + side, new Vector3(side * 5, .012f, .2f), new Vector3(.055f, .012f, 14), yellow);
            }
            Part(bay, "Front Stop Line", new Vector3(0, .018f, 4.5f), new Vector3(6.1f, .016f, .14f), red);
            Part(bay, "Rear Stop Line", new Vector3(0, .018f, -4.1f), new Vector3(6.1f, .016f, .14f), red);
            for (int i = 0; i < 12; i++)
            {
                var stripe = Part(bay, "Safety Chevron " + i, new Vector3(-7.8f + i * .45f, .017f, 4.6f), new Vector3(.2f, .015f, .8f), yellow);
                stripe.transform.localRotation = Quaternion.Euler(0, 30, 0);
            }
            var branding = Group(root.transform, "Team Identity");
            Label(branding, "Team Name", "FORMULA  /  G27", new Vector3(.3f, 4.65f, -6.79f), new Vector2(11, 1.3f), new Color32(200, 16, 46, 255));
            Label(branding, "Bay Title", "GENERIC  /  RACE ENGINEERING", new Vector3(.2f, 3.35f, -6.64f), new Vector2(11, .55f), Color.white);
            Label(branding, "Bay Number", "01", new Vector3(7.6f, 4.65f, -6.78f), new Vector2(2, 1.2f), new Color32(200, 16, 46, 255));
            Label(branding, "Team Motto", "BUILD.  RACE.  EVOLVE.", new Vector3(.2f, 2.7f, -6.64f), new Vector2(9, .36f), new Color32(255, 225, 59, 255));
            var equipment = Group(root.transform, "Pit Equipment");
            Workbench(equipment, -6.5f); Workbench(equipment, 5.9f);
            ToolTrolley(equipment, new Vector3(-4.7f, 0, .1f));
            ToolTrolley(equipment, new Vector3(5.8f, 0, 1.8f));
            TyreRack(equipment, new Vector3(7.8f, 0, -3.8f));
            PitJack(equipment);
            Part(equipment, "Driver Helmet", new Vector3(5.1f, 1.78f, -5.65f), new Vector3(.43f, .48f, .46f), white, PrimitiveType.Sphere);
            Part(equipment, "Helmet Visor", new Vector3(5.1f, 1.81f, -5.43f), new Vector3(.36f, .12f, .055f), wine);
            var lighting = Group(root.transform, "Lighting");
            for (int z = -4; z <= 4; z += 4)
            {
                Part(lighting, "Overhead Housing " + z, new Vector3(0, 5.8f, z), new Vector3(7.8f, .14f, .7f), red).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Part(lighting, "Overhead LED " + z, new Vector3(0, 5.72f, z), new Vector3(7.4f, .04f, .4f), led).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Part(lighting, "Rear Light Left", new Vector3(-8.2f, 4.5f, -6.5f), new Vector3(.06f, 3, .06f), goldLed);
            Part(lighting, "Rear Light Right", new Vector3(8.2f, 4.5f, -6.5f), new Vector3(.06f, 3, .06f), goldLed);
            Light(lighting, "Main Softbox", LightType.Directional, new Vector3(0, 5, 0), new Vector3(48, -25, 0), 1.5f, true);
            Light(lighting, "Front Fill", LightType.Point, new Vector3(2, 4.5f, 5), Vector3.zero, 45);
            Light(lighting, "Rear Fill", LightType.Point, new Vector3(-2, 4, -4), Vector3.zero, 35);
            Group(root.transform, "Vehicle Anchor").localPosition = new Vector3(0, .1f, .2f);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Garage Atmosphere.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, Folder + "/Garage Atmosphere.asset");
                var bloom = profile.Add<Bloom>(); bloom.intensity.Override(.12f); bloom.threshold.Override(1.2f); AssetDatabase.AddObjectToAsset(bloom, profile);
                var tone = profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES); AssetDatabase.AddObjectToAsset(tone, profile);
            }
            var volume = Group(root.transform, "Garage Atmosphere").gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .43f, .40f);
            var probe = Group(root.transform, "Baked Interior Reflection").gameObject.AddComponent<ReflectionProbe>();
            probe.transform.localPosition = new Vector3(0, 2, 0); probe.size = new Vector3(19, 7, 21);
            probe.mode = ReflectionProbeMode.Baked; probe.boxProjection = true; probe.resolution = 128; probe.hdr = true;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor; probe.backgroundColor = new Color32(48, 0, 11, 255); probe.farClipPlane = 40;
            string reflectionPath = Folder + "/Garage Reflection.exr";
            BakeStageReflection(root, probe, reflectionPath);
            AssetDatabase.ImportAsset(reflectionPath); probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
            PrefabUtility.SaveAsPrefabAsset(root, StagePrefab);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
    internal static void BakeStageReflection(GameObject stage, ReflectionProbe probe, string path)
    {
        // Regeneration may run with Menu open at the same origin. Exclude that scene's
        // geometry and lights from the bake, then restore all temporary editor state.
        var objects = stage.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).ToArray();
        var layers = objects.Select(o => o.layer).ToArray();
        var otherLights = Resources.FindObjectsOfTypeAll<Light>().Where(l => l.enabled && l.gameObject.scene.IsValid() && !l.transform.IsChildOf(stage.transform)).ToArray();
        try
        {
            foreach (var light in otherLights) light.enabled = false;
            foreach (var go in objects) go.layer = 31;
            probe.cullingMask = 1 << 31;
            if (!Lightmapping.BakeReflectionProbe(probe, path)) throw new InvalidOperationException("Interior reflection bake failed.");
        }
        finally
        {
            for (int i = 0; i < objects.Length; i++) objects[i].layer = layers[i];
            foreach (var light in otherLights) if (light != null) light.enabled = true;
            probe.cullingMask = ~0;
        }
    }
    private static void Workbench(Transform parent, float x)
    {
        var group = Group(parent, "Engineering Station " + x); group.localPosition = new Vector3(x, 0, -5.8f);
        Part(group, "Cabinet", new Vector3(0, .7f, 0), new Vector3(3.4f, 1.4f, 1.1f), red);
        Part(group, "Worktop", new Vector3(0, 1.44f, 0), new Vector3(3.6f, .12f, 1.25f), white);
        Part(group, "Toe Recess", new Vector3(0, .12f, .57f), new Vector3(3.25f, .16f, .05f), wine);
        for (int i = 0; i < 4; i++)
        {
            float xx = -1.27f + i * .85f;
            Part(group, "Drawer", new Vector3(xx, .8f, .568f), new Vector3(.8f, 1.05f, .035f), wine);
            Part(group, "Drawer Face", new Vector3(xx, .8f, .59f), new Vector3(.76f, .99f, .025f), red);
            Part(group, "Handle", new Vector3(xx, 1.15f, .63f), new Vector3(.42f, .035f, .06f), yellow);
        }
        Part(group, "Monitor Stand", new Vector3(0, 1.7f, -.17f), new Vector3(.13f, .55f, .12f), wine);
        Part(group, "Monitor Frame", new Vector3(0, 2.1f, -.1f), new Vector3(2.4f, 1.15f, .12f), white);
        Part(group, "Monitor Screen", new Vector3(0, 2.1f, -.025f), new Vector3(2.26f, 1.02f, .035f), wine);
        Label(group, "Station Display", "G27  /  ENGINEERING", new Vector3(0, 2.42f, .002f), new Vector2(2, .22f), Color.white);
        for (int i = 0; i < 5; i++)
            Part(group, "Telemetry Bar", new Vector3(-.86f + i * .4f, 1.95f, .008f), new Vector3(.25f, .12f + i * .06f, .01f), i % 2 == 0 ? yellow : red);
        Part(group, "Keyboard", new Vector3(0, 1.53f, .23f), new Vector3(1.05f, .04f, .34f), wine);
        for (int i = 0; i < 4; i++)
            Part(group, "Keyboard Row", new Vector3(0, 1.555f, .12f + i * .07f), new Vector3(.92f, .008f, .035f), white);
    }
    private static void ToolTrolley(Transform parent, Vector3 pos)
    {
        var group = Group(parent, "Pit Tool Trolley"); group.localPosition = pos;
        Part(group, "Body", new Vector3(0, .68f, 0), new Vector3(1.35f, .95f, .75f), red);
        Part(group, "Rubber Top", new Vector3(0, 1.18f, 0), new Vector3(1.43f, .08f, .84f), rubber);
        for (int i = 0; i < 5; i++)
        {
            Part(group, "Drawer Gap", new Vector3(0, .38f + i * .155f, .382f), new Vector3(1.22f, .025f, .02f), wine);
            Part(group, "Pull Handle", new Vector3(0, .43f + i * .155f, .41f), new Vector3(.9f, .025f, .04f), white);
        }
        foreach (float xx in new[] { -.48f, .48f }) foreach (float zz in new[] { -.25f, .25f })
        {
            var wheel = Part(group, "Caster", new Vector3(xx, .15f, zz), new Vector3(.24f, .065f, .24f), rubber, PrimitiveType.Cylinder);
            wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        Part(group, "Power Tool", new Vector3(.25f, 1.32f, 0), new Vector3(.12f, .2f, .32f), yellow);
        Part(group, "Socket Tray", new Vector3(-.3f, 1.24f, 0), new Vector3(.4f, .04f, .32f), white);
    }
    private static void TyreRack(Transform parent, Vector3 pos)
    {
        var rack = Group(parent, "Formula Tyre Rack"); rack.localPosition = pos;
        Part(rack, "Lower Shelf", new Vector3(0, .15f, 0), new Vector3(2.3f, .12f, 1.2f), red);
        Part(rack, "Upper Shelf", new Vector3(0, 1.4f, 0), new Vector3(2.3f, .1f, 1.2f), red);
        foreach (float x in new[] { -1.08f, 1.08f }) foreach (float z in new[] { -.52f, .52f })
            Part(rack, "Frame Upright", new Vector3(x, 1.35f, z), new Vector3(.065f, 2.7f, .065f), white);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Formula/Resources/Formula/Vehicles/Formula_Generic.prefab");
        var filters = source.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == "Generic_Tyre_0" || m.name == "Generic_Rim_0").ToArray();
        for (int row = 0; row < 2; row++) for (int column = 0; column < 3; column++)
        {
            var wheel = Group(rack, "Spare Formula Wheel");
            var pivot = filters[0].transform.parent;
            foreach (var f in filters)
            {
                var go = new GameObject(f.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(wheel, false);
                go.transform.localPosition = pivot.InverseTransformPoint(f.transform.position);
                go.transform.localRotation = Quaternion.Inverse(pivot.rotation) * f.transform.rotation;
                go.transform.localScale = f.transform.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh = f.sharedMesh; go.GetComponent<MeshRenderer>().sharedMaterials = f.GetComponent<MeshRenderer>().sharedMaterials;
            }
            wheel.localScale = Vector3.one * 1.5f;
            wheel.localRotation = Quaternion.Euler(0, 90, 0); wheel.localPosition = new Vector3(-.7f + column * .7f, .65f + row * 1.25f, 0);
        }
    }
    private static void PitJack(Transform parent)
    {
        var jack = Group(parent, "Front Wing Quick Lift Jack"); jack.localPosition = new Vector3(-3.9f, 0, 2.6f);
        jack.localEulerAngles = new Vector3(0, -20, 0);
        Part(jack, "Low Profile Frame", new Vector3(0, .17f, 0), new Vector3(.75f, .16f, 1.25f), red);
        Part(jack, "Wing Cradle", new Vector3(0, .34f, -.5f), new Vector3(.65f, .16f, .3f), rubber);
        foreach (float x in new[] { -.46f, .46f })
        {
            var wheel = Part(jack, "Jack Wheel", new Vector3(x, .16f, .3f), new Vector3(.3f, .065f, .3f), rubber, PrimitiveType.Cylinder);
            wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        Vector3 from = new Vector3(0, .25f, .3f), to = new Vector3(0, 1.5f, 1.5f);
        var handle = Part(jack, "Quick Lift Lever", (from + to) * .5f, new Vector3(.055f, Vector3.Distance(from, to) * .5f, .055f), yellow, PrimitiveType.Cylinder);
        handle.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        Part(jack, "Lever Grip", to, new Vector3(.7f, .08f, .08f), wine);
    }
    private static void Light(Transform parent, string name, LightType type, Vector3 pos, Vector3 angles, float intensity, bool shadows = false)
    {
        var go = Group(parent, name); go.localPosition = pos; go.localEulerAngles = angles;
        var light = go.gameObject.AddComponent<Light>(); light.type = type; light.color = new Color(1, .96f, .9f);
        light.intensity = intensity; light.range = 18; light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = .65f;
    }
    private static void ConfigureScene(Scene scene, bool garageEntry)
    {
        SceneManager.SetActiveScene(scene);
        var roots = scene.GetRootGameObjects();
        var menu = roots.SelectMany(r => r.GetComponentsInChildren<FormulaMainMenu>(true)).Single();
        // Authored menu scenes group the stage under Presentation instead of leaving it at the root.
        var groupedStage = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == StageName && t.parent != null);
        if (groupedStage != null) Undo.DestroyObjectImmediate(groupedStage.gameObject);
        foreach (var previousPreview in roots.SelectMany(r => r.GetComponentsInChildren<FormulaGarageVehiclePreview>(true)))
            foreach (Transform child in previousPreview.transform) if (child.CompareTag("EditorOnly")) Undo.DestroyObjectImmediate(child.gameObject);
        foreach (var root in roots.Where(r => new[] { "Sci-Fi_Neon_City_fbx", "Props", "Lights", "People", "Plane", StageName, "Editor Garage Vehicle" }.Contains(r.name)))
            Undo.DestroyObjectImmediate(root);
        var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefab), scene);
        stage.name = StageName;
        for (int i = menu.spawnPoint.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(menu.spawnPoint.GetChild(i).gameObject);
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        foreach (var c in camera.GetComponents<Behaviour>()) if (c.GetType().Name == "CinemachineBrain") c.enabled = false;
        foreach (var orbit in roots.SelectMany(r => r != null ? r.GetComponentsInChildren<MenuCameraOrbit>(true) : Array.Empty<MenuCameraOrbit>())) orbit.enabled = false;
        camera.transform.position = new Vector3(5.7f, 2.7f, 7.5f);
        Vector3 car = new Vector3(0, 1f, .2f); Vector3 right = Vector3.Cross(Vector3.up, car - camera.transform.position).normalized;
        camera.transform.rotation = Quaternion.LookRotation(car - right * 2.8f - camera.transform.position);
        camera.fieldOfView = 38; camera.nearClipPlane = .15f; camera.farClipPlane = 55;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(48, 0, 11, 255); camera.allowHDR = true;
        // The previous city occlusion bake does not describe this newly authored interior.
        camera.useOcclusionCulling = false;
        var extra = camera.GetComponent<UniversalAdditionalCameraData>();
        if (extra != null) { extra.renderPostProcessing = true; extra.antialiasing = AntialiasingMode.FastApproximateAntialiasing; }
        menu.spawnPoint.position = new Vector3(0, .1f, .2f); menu.spawnPoint.rotation = Quaternion.identity;
        menu.legacyDisplayCars = Array.Empty<GameObject>();
        var preview = CreateEditorVehicle(scene);
        var serialized = new SerializedObject(menu);
        serialized.FindProperty("editorGaragePreview").objectReferenceValue = preview;
        serialized.FindProperty("startInGarage").boolValue = garageEntry;
        serialized.FindProperty("previewHorizontalOffset").floatValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.48f, .43f, .40f); RenderSettings.ambientIntensity = 1;
        RenderSettings.sun = stage.GetComponentsInChildren<Light>().First(l => l.type == LightType.Directional);
        EditorSceneManager.MarkSceneDirty(scene);
    }
    private static GameObject CreateEditorVehicle(Scene scene)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Formula/Resources/Formula/Vehicles/Formula_Generic.prefab");
        var root = new GameObject("Editor Garage Vehicle"); SceneManager.MoveGameObjectToScene(root, scene); root.tag = "EditorOnly";
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>(); if (renderer == null || !renderer.enabled) continue;
            var go = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root.transform, false);
            go.transform.localPosition = source.transform.InverseTransformPoint(filter.transform.position);
            go.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
            go.transform.localScale = filter.transform.lossyScale;
            go.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh; go.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        float bottom = root.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y);
        root.transform.position = new Vector3(0, .025f - bottom, .2f); return root;
    }
    [Serializable] private class Report { public string scene, prefab; public int renderers, lights, shadowLights, cameras, eventSystems; public bool menuWired, legacyCityRemoved; public string[] lightInventory, eventInventory, rootInventory; }
    private static void WriteReport(Scene scene)
    {
        var roots = scene.GetRootGameObjects(); var lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).ToArray();
        var menu = roots.SelectMany(r => r.GetComponentsInChildren<FormulaMainMenu>(true)).Single();
        var report = new Report { scene = GarageScene, prefab = StagePrefab, renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Count(r => r.enabled && r.gameObject.activeInHierarchy), lights = lights.Count(l => l.isActiveAndEnabled), shadowLights = lights.Count(l => l.isActiveAndEnabled && l.shadows != LightShadows.None), cameras = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Count(c => c.isActiveAndEnabled), eventSystems = roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count(e => e.isActiveAndEnabled), menuWired = menu.spawnPoint != null && menu.View != null, legacyCityRemoved = roots.All(r => r.name != "Sci-Fi_Neon_City_fbx"), lightInventory = lights.Select(l => l.transform.root.name + "/" + l.name + " active=" + l.isActiveAndEnabled).ToArray(), eventInventory = roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Select(e => e.transform.root.name + "/" + e.name + " active=" + e.isActiveAndEnabled).ToArray(), rootInventory = roots.Select(r => r.name + " renderers=" + r.GetComponentsInChildren<Renderer>(true).Length).ToArray() };
        Directory.CreateDirectory("Docs/AI/FormulaGarage"); File.WriteAllText("Docs/AI/FormulaGarage/SceneSetup.json", JsonUtility.ToJson(report, true));
        if (!report.menuWired || !report.legacyCityRemoved || report.cameras != 1 || report.eventSystems != 1) throw new InvalidOperationException("Garage scene validation failed. See SceneSetup.json.");
    }
}
