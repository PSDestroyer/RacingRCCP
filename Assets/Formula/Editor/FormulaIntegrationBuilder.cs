using System;
using System.IO;
using System.Linq;
using System.Text;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FormulaIntegrationBuilder
{
    private const string ResourcesFolder = "Assets/Formula/Resources/Formula";
    [MenuItem("Tools/Formula Career/Connect Menu and Circuit Cameras")]
    public static void ConnectMenuAndCameras()
    {
        Scene original = SceneManager.GetActiveScene();
        Scene menuScene = SceneManager.GetSceneByPath("Assets/Scenes/Menu.unity");
        bool menuLoaded = menuScene.IsValid() && menuScene.isLoaded;
        if (!menuLoaded) menuScene = EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity", OpenSceneMode.Additive);
        try
        {
            var roots = menuScene.GetRootGameObjects();
            var menu = roots.SelectMany(r => r.GetComponentsInChildren<FormulaMainMenu>(true)).FirstOrDefault();
            if (menu == null || menu.View == null)
            {
                var selection = roots.SelectMany(r => r.GetComponentsInChildren<CarSelection>(true)).First();
                var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).First(c => c.name == "Canvas");
                var navigation = roots.SelectMany(r => r.GetComponentsInChildren<GarageUIController>(true)).First();
                if (menu == null) { var go = new GameObject("FormulaMainMenu"); SceneManager.MoveGameObjectToScene(go, menuScene); menu = go.AddComponent<FormulaMainMenu>(); }
                menu.legacyCanvas = canvas; menu.legacyNavigation = navigation; menu.spawnPoint = selection.spawnCarPoint;
                menu.legacyBack = new SerializedObject(navigation).FindProperty("back").objectReferenceValue as UnityEngine.UI.Button;
            }
            foreach (var component in roots.SelectMany(r => r.GetComponentsInChildren<RCCP_Component>(true)))
                if (component.GetComponentInParent<RCCP_CarController>(true) == null) { component.enabled = false; component.gameObject.SetActive(false); }
            EditorSceneManager.SaveScene(menuScene);
        }
        finally { if (!menuLoaded) EditorSceneManager.CloseScene(menuScene, true); }
        foreach (int index in new[] { 1, 2 })
        {
            string sourcePath = "Assets/Racing_Visuals/Scenes/Race_Track_" + index + "_Visual.unity";
            Scene visual = SceneManager.GetSceneByPath(sourcePath); bool loaded = visual.IsValid() && visual.isLoaded;
            if (!loaded) visual = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            Scene scene = EditorSceneManager.OpenScene("Assets/Formula/Scenes/Formula_Circuit_0" + index + ".unity", OpenSceneMode.Additive);
            try
            {
                var cameraRoot = visual.GetRootGameObjects().First(r => r.name == "Camera_Parent");
                var source = cameraRoot.GetComponentInChildren<Camera>(true);
                foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
                {
                    camera.cullingMask = source.cullingMask; camera.clearFlags = source.clearFlags; camera.backgroundColor = source.backgroundColor;
                    camera.nearClipPlane = .1f; camera.farClipPlane = source.farClipPlane;
                    var extra = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    var sourceExtra = source.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (extra != null && sourceExtra != null) { extra.renderPostProcessing = sourceExtra.renderPostProcessing; extra.volumeLayerMask = sourceExtra.volumeLayerMask; }
                }
                SceneManager.SetActiveScene(visual); Material sky = RenderSettings.skybox; Color ambient = RenderSettings.ambientLight; float intensity = RenderSettings.ambientIntensity;
                bool fog = RenderSettings.fog; Color fogColor = RenderSettings.fogColor; float density = RenderSettings.fogDensity;
                SceneManager.SetActiveScene(scene); RenderSettings.skybox = sky; RenderSettings.ambientLight = ambient; RenderSettings.ambientIntensity = intensity;
                RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = density;
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (!loaded) EditorSceneManager.CloseScene(visual, true); }
        }
        SceneManager.SetActiveScene(original);
        Debug.Log("FORMULA_MENU_AND_CAMERAS_CONNECTED");
    }

    [MenuItem("Tools/Formula Career/Inspect Integration Scenes")]
    public static void Inspect()
    {
        var output = new StringBuilder();
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", "Assets/Scenes/GameplayTestScene.unity", "Assets/RaceTracksPack/Scenes/Track A/RT_Track_A.unity", "Assets/RaceTracksPack/Scenes/Track B/RT_Track_B_Final.unity", "Assets/Racing_Visuals/Scenes/Race_Track_1_Visual.unity", "Assets/Racing_Visuals/Scenes/Race_Track_2_Visual.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                output.AppendLine("SCENE " + path);
                foreach (var root in scene.GetRootGameObjects())
                {
                    output.AppendLine("ROOT " + root.name + " " + root.transform.position);
                    if (path.Contains("Track B") || root.name == "Road Network")
                        foreach (var t in root.GetComponentsInChildren<Transform>(true).Take(160))
                        {
                            var r = t.GetComponent<Renderer>();
                            var m = t.GetComponent<MeshFilter>();
                            output.AppendLine("PART " + t.name + " pos=" + t.position + " bounds=" + (r != null ? r.bounds.ToString() : "-") + " mesh=" + (m != null && m.sharedMesh != null ? m.sharedMesh.name : "-"));
                        }
                    foreach (var c in root.GetComponentsInChildren<RCCP_AIWaypointsContainer>(true)) output.AppendLine("WAYPOINTS " + c.name + " count=" + c.waypoints.Count);
                    foreach (var c in root.GetComponentsInChildren<GamePlayManager>(true)) output.AppendLine("GAMEPLAY " + c.name + " spawn=" + c.SpawnPoint?.position + " wp=" + c.raceWaypoints?.name);
                    foreach (var c in root.GetComponentsInChildren<Canvas>(true)) output.AppendLine("CANVAS " + c.name + " parent=" + c.transform.parent?.name);
                }
            }
            finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
        Directory.CreateDirectory("Docs/AI");
        File.WriteAllText("Docs/AI/FormulaIntegrationScenes.txt", output.ToString());
        Debug.Log("FORMULA_SCENE_INSPECTION_COMPLETE");
    }
    [MenuItem("Tools/Formula Career/Inspect Road Meshes")]
    public static void InspectRoadMeshes()
    {
        var output = new StringBuilder();
        foreach (int index in new[] { 1, 2 })
        {
            string path = "Assets/Racing_Visuals/Scenes/Race_Track_" + index + "_Visual.unity";
            Scene scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var road = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).First(m => m.name == "road_0001");
                var vertices = road.sharedMesh.vertices; var uv = road.sharedMesh.uv;
                var groups = Enumerable.Range(0, vertices.Length).GroupBy(i => Mathf.RoundToInt(uv[i].y * 1000f)).OrderBy(g => g.Key);
                output.AppendLine("ROAD " + index + " vertices=" + vertices.Length + " groups=" + groups.Count());
                foreach (var group in groups)
                {
                    Vector3 center = group.Aggregate(Vector3.zero, (v, i) => v + road.transform.TransformPoint(vertices[i])) / group.Count();
                    output.AppendLine(group.Key + " count=" + group.Count() + " center=" + center.ToString("F3"));
                }
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        File.WriteAllText("Docs/AI/FormulaRoadMeshes.txt", output.ToString());
        Debug.Log("FORMULA_ROAD_MESH_INSPECTION_COMPLETE");
    }

    [MenuItem("Tools/Formula Career/Prepare Generic Assets")]
    public static void PrepareAssets()
    {
        Directory.CreateDirectory(ResourcesFolder + "/Vehicles"); AssetDatabase.Refresh();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Formula/RCCP/Prefabs/Generic/Formula_Generic_RCCP_01.prefab");
        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        try
        {
            var vehicle = root.GetComponent<FormulaVehicle>() ?? root.AddComponent<FormulaVehicle>();
            vehicle.body = root.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Object_Material_1_3");
            foreach (var axle in root.GetComponentsInChildren<RCCP_Axle>(true)) axle.maxBrakeTorque = 1400f;
            PrefabUtility.SaveAsPrefabAsset(root, ResourcesFolder + "/Vehicles/Formula_Generic.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var assets = AssetDatabase.LoadAssetAtPath<FormulaAssets>(ResourcesFolder + "/FormulaAssets.asset");
        if (assets == null) { assets = ScriptableObject.CreateInstance<FormulaAssets>(); AssetDatabase.CreateAsset(assets, ResourcesFolder + "/FormulaAssets.asset"); }
        assets.vehiclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesFolder + "/Vehicles/Formula_Generic.prefab");
        assets.liveryMaterials = Enumerable.Range(1, 8).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Formula/RCCP/Prefabs/Generic/Formula_Generic_RCCP_" + i.ToString("00") + ".prefab").GetComponentsInChildren<Renderer>(true).First(r => r.name == "Object_Material_1_3").sharedMaterial).ToArray();
        if (assets.tracks == null) assets.tracks = Array.Empty<FormulaAssets.Track>();
        EditorUtility.SetDirty(assets);
        var car = AssetDatabase.LoadAssetAtPath<CarSO>(ResourcesFolder + "/FormulaGenericCar.asset");
        if (car == null) { car = ScriptableObject.CreateInstance<CarSO>(); AssetDatabase.CreateAsset(car, ResourcesFolder + "/FormulaGenericCar.asset"); }
        var serialized = new SerializedObject(car);
        serialized.FindProperty("_id").intValue = 0;
        serialized.FindProperty("_carName").stringValue = "Formula G27";
        serialized.FindProperty("_carPrefabLocation").stringValue = "Formula/Vehicles/Formula_Generic";
        serialized.FindProperty("_price").intValue = 0;
        serialized.FindProperty("_carInfo").stringValue = "Formula single-seater · rear-wheel drive · 8 gears";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("FORMULA_GENERIC_ASSETS_READY");
    }
}
