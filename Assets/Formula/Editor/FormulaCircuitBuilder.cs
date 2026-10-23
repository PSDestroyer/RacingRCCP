using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class FormulaCircuitBuilder
{
    [MenuItem("Tools/Formula Career/Register Career Scenes")]
    public static void RegisterScenes()
    {
        Directory.CreateDirectory("Docs/AI/FormulaCareer");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (var track in FormulaAssets.Current.tracks)
            if (!scenes.Any(s => s.path == track.scenePath)) scenes.Add(new EditorBuildSettingsScene(track.scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        File.WriteAllText("Docs/AI/FormulaCareer/BuildScenes.txt", string.Join("\n", EditorBuildSettings.scenes.Select(s => s.enabled + " " + s.path)));
        Debug.Log("FORMULA_BUILD_SCENES_REGISTERED");
    }

    [MenuItem("Tools/Formula Career/Prepare Two Circuits")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before preparing circuits.");
        FormulaIntegrationBuilder.PrepareAssets();
        Directory.CreateDirectory("Assets/Formula/Scenes"); Directory.CreateDirectory("Assets/Formula/Navigation"); AssetDatabase.Refresh();
        var original = SceneManager.GetActiveScene();
        var template = EditorSceneManager.OpenScene("Assets/RaceTracksPack/Scenes/Track A/RT_Track_A.unity", OpenSceneMode.Additive);
        var group = new GameObject("RaceSystems"); SceneManager.MoveGameObjectToScene(group, template);
        var keep = new HashSet<string> { "GameplayManager", "HUD", "Spawn", "RCCP_SceneManager", "RCCP_Camera", "RCCP_FixedCamera", "RCCP_CinematicCamera", "RCCP_SkidmarksManager" };
        foreach (var root in template.GetRootGameObjects()) if (keep.Contains(root.name)) root.transform.SetParent(group.transform, true);
        var tracks = new List<FormulaAssets.Track>();
        try
        {
            for (int index = 1; index <= 2; index++)
            {
                string path = "Assets/Formula/Scenes/Formula_Circuit_0" + index + ".unity";
                if (File.Exists(path)) throw new InvalidOperationException("Circuit already exists; preserve manual changes: " + path);
                string visualPath = "Assets/Racing_Visuals/Scenes/Race_Track_" + index + "_Visual.unity";
                Scene visual = SceneManager.GetSceneByPath(visualPath); bool loaded = visual.IsValid() && visual.isLoaded;
                if (!loaded) visual = EditorSceneManager.OpenScene(visualPath, OpenSceneMode.Additive);
                Scene output = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    var visualRoots = visual.GetRootGameObjects();
                    var camera = visualRoots.First(r => r.name == "Camera_Parent");
                    var road = visualRoots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).First(m => m.name == "road_0001");
                    var vertices = road.sharedMesh.vertices; var uv = road.sharedMesh.uv;
                    var rows = Enumerable.Range(0, vertices.Length).GroupBy(i => Mathf.RoundToInt(uv[i].y * 1000f)).OrderBy(g => g.Key).ToArray();
                    if (rows.Any(row => row.Count() != 2)) throw new InvalidOperationException("Road cross sections must have two edges.");
                    var centers = rows.Select(row => row.Aggregate(Vector3.zero, (v, i) => v + road.transform.TransformPoint(vertices[i])) / 2f).ToList();
                    if (Vector3.Distance(centers[0], centers[centers.Count - 1]) < 1f) centers.RemoveAt(centers.Count - 1);
                    float halfWidth = rows.Average(row => Vector3.Distance(vertices[row.First()], vertices[row.Last()]) * .5f);
                    int start = Enumerable.Range(0, centers.Count).OrderBy(i => Vector3.Distance(centers[i], camera.transform.position)).First();
                    centers = Enumerable.Range(0, centers.Count).Select(i => centers[(i + start) % centers.Count]).ToList();
                    SceneManager.SetActiveScene(output);
                    foreach (var root in visualRoots)
                    {
                        if (root.name == "Camera_Parent" || root.name.StartsWith("RCCP_")) continue;
                        UnityEngine.Object.Instantiate(root).name = root.name;
                    }
                    var systems = UnityEngine.Object.Instantiate(group); systems.name = "RaceSystems";
                    var game = systems.GetComponentInChildren<GamePlayManager>(true);
                    var routeObject = new GameObject("FormulaWaypoints");
                    var route = routeObject.AddComponent<RCCP_AIWaypointsContainer>(); route.waypoints.Clear();
                    for (int i = 0; i < centers.Count; i += 3)
                    {
                        var point = new GameObject("Waypoint_" + route.waypoints.Count.ToString("000")); point.transform.SetParent(routeObject.transform, false);
                        point.transform.position = centers[i] + Vector3.up * .4f;
                        point.transform.rotation = Quaternion.LookRotation(centers[(i + 3) % centers.Count] - centers[i], Vector3.up);
                        route.waypoints.Add(point.AddComponent<RCCP_Waypoint>());
                    }
                    game.raceWaypoints = route;
                    game.SpawnPoint.position = centers[0] + Vector3.up * .3f;
                    game.SpawnPoint.rotation = Quaternion.LookRotation(centers[3] - centers[0]);
                    game.opponentSpawnPoints = Array.Empty<Transform>(); game.useCurrentMapModeSettings = false;
                    game.usePlayerCarForOpponents = true; game.copyPlayerPerformanceOnSpawn = false; game.enableDynamicRacePacing = false;
                    game.waypointReachDistance = Mathf.Clamp(halfWidth * .75f, 4f, 7f);
                    var raceUI = game.gameObject.AddComponent<FormulaRaceUI>(); raceUI.trackHalfWidth = halfWidth + 1f;
                    var newRoad = output.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).First(m => m.name == "road_0001");
                    var collider = newRoad.GetComponent<MeshCollider>() ?? newRoad.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = newRoad.sharedMesh; collider.convex = false;
                    // Bake only the drivable road, avoiding scenery and other loaded scenes.
                    var navigation = new GameObject("FormulaNavigation"); var surface = navigation.AddComponent<NavMeshSurface>();
                    surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                    var source = new GameObject("NavigationSource"); source.transform.SetParent(navigation.transform, false);
                    source.transform.SetPositionAndRotation(newRoad.transform.position, newRoad.transform.rotation); source.transform.localScale = newRoad.transform.lossyScale;
                    source.AddComponent<MeshCollider>().sharedMesh = newRoad.sharedMesh;
                    surface.BuildNavMesh();
                    string navPath = "Assets/Formula/Navigation/Formula_Circuit_0" + index + ".asset";
                    AssetDatabase.CreateAsset(surface.navMeshData, navPath);
                    source.SetActive(false);
                    float length = 0;
                    for (int i = 0; i < centers.Count; i++) length += Vector3.Distance(centers[i], centers[(i + 1) % centers.Count]);
                    tracks.Add(new FormulaAssets.Track { id = "track_0" + index, displayName = index == 1 ? "Grand Prix Circuit" : "Technical Circuit", scenePath = path, referenceLapSeconds = length / 32f, referenceCalibrated = false });
                    EditorSceneManager.SaveScene(output, path);
                }
                finally { EditorSceneManager.CloseScene(output, true); if (!loaded) EditorSceneManager.CloseScene(visual, true); }
            }
            var assets = FormulaAssets.Current; assets.tracks = tracks.ToArray(); EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            RegisterScenes();
            Debug.Log("FORMULA_CIRCUITS_PREPARED: two road-derived routes and navigation meshes.");
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(template, true); }
    }
}
