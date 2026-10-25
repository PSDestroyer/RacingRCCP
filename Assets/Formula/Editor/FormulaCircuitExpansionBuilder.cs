using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using Unity.AI.Navigation;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds fixed assets from vendor sources. No generation or navigation baking in a player.
[InitializeOnLoad]
public static class FormulaCircuitExpansionBuilder
{
    private const string Folder = "Assets/Formula/Racing";
    private const string SystemsPath = Folder + "/FormulaRaceSystems.prefab";
    private const string ReportPath = "Docs/AI/FormulaCircuits/Build.json";
    private static readonly string[] Names = { "Grand Prix Circuit", "Technical Circuit", "Alpine Circuit", "Autumn Circuit", "Alpine Winter Circuit", "Grand Prix Wet Circuit", "City Lights Circuit", "Terrain Circuit", "Apex Park", "Velocity Ring", "Crown Valley", "Summit Raceway", "Horizon Park", "Falcon Circuit", "Aurora Raceway", "Meridian Ring", "Vertex Circuit", "Pinnacle Park" };
    private static readonly int[] Seeds = { 270101, 270223, 270317, 270419, 270521, 270617, 270727, 270829, 270937, 271043 };
    private static SceneSetup[] previous;
    private static int next;
    private static bool running;
    private static Material raceSky;
    private static bool validateAfterBuild, resumeAfterBuild;
    private static readonly List<object> results = new List<object>();
    private static readonly HashSet<string> correctedTrackIds = new HashSet<string>();
    private static readonly List<object> heightCorrections = new List<object>();
    private static readonly List<object> reloaded = new List<object>();
    private static readonly List<string> fingerprints = new List<string>();
    static FormulaCircuitExpansionBuilder()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Formula Career/Build and Drive Validate 18 Circuits")]
    public static void BuildAndValidate() { Build(); validateAfterBuild = true; }

    [MenuItem("Tools/Formula Career/Rebuild and Resume Circuit Validation")]
    public static void RebuildAndResume() { Build(); validateAfterBuild = true; resumeAfterBuild = true; }

    [MenuItem("Tools/Formula Career/Build 18 Formula Circuits")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || running) throw new InvalidOperationException("Exit Play Mode or finish the current circuit build first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before building circuits. No unsaved edits were discarded.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Folder + "/Meshes"); Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory("Assets/Formula/Navigation"); Directory.CreateDirectory("Docs/AI/FormulaCircuits");
        validateAfterBuild = resumeAfterBuild = false;
        AssetDatabase.Refresh(); previous = EditorSceneManager.GetSceneManagerSetup(); results.Clear(); reloaded.Clear(); heightCorrections.Clear(); correctedTrackIds.Clear(); fingerprints.Clear(); next = 0;
        var template = EditorSceneManager.OpenScene("Assets/Formula/Scenes/Formula_Circuit_01.unity", OpenSceneMode.Single);
        raceSky = RenderSettings.skybox;
        var systems = template.GetRootGameObjects().First(r => r.name == "RaceSystems");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath) == null) PrefabUtility.SaveAsPrefabAsset(systems, SystemsPath);
        running = true; WriteReport("building", null);
        Debug.Log("FORMULA_18_CIRCUITS_BUILD_STARTED");
    }

    private static void Tick()
    {
        if (!running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (next < 18)
            {
                int index = next == 17 ? 7 : next >= 7 ? next + 1 : next;
                EditorUtility.DisplayProgressBar("Formula circuits", (next + 1) + "/18 · " + Names[index], next / 18f);
                BuildCircuit(index); next++; WriteReport("building", null); return;
            }
            if (next < 36)
            {
                var track = FormulaAssets.Current.tracks[next - 18];
                EditorUtility.DisplayProgressBar("Verify saved circuits", track.displayName, (next - 18) / 18f);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var saved = EditorSceneManager.OpenScene(track.scenePath, OpenSceneMode.Single);
                var game = Object.FindFirstObjectByType<GamePlayManager>();
                var circuit = game.GetComponent<FormulaCircuit>();
                if (game.raceWaypoints != circuit.route || game.checkpointPrefab != null || game.opponentSpawnPoints.Length != 7 || circuit.aiSpeedPlan.Length != circuit.route.waypoints.Count)
                    throw new InvalidOperationException("Saved race configuration did not persist: " + track.id);
                if (game.transform.root.GetComponentsInChildren<Canvas>(true).Any(c => c.enabled))
                    throw new InvalidOperationException("Legacy HUD remained enabled: " + track.id);
                reloaded.Add(new { track.id, checks = Validate(saved, circuit, game) });
                next++; WriteReport("verifying_saved_assets", null); return;
            }
            var assets = FormulaAssets.Current;
            assets.tracks = Enumerable.Range(0, 18).Select(i => assets.FindTrack(Id(i))).ToArray();
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            FormulaCircuitBuilder.RegisterScenes();
            FormulaCircuitSelectionBuilder.Apply();
            const string driveReport = "Docs/AI/FormulaCircuits/PlayValidation.json";
            if (correctedTrackIds.Count > 0 && File.Exists(driveReport))
            {
                File.Copy(driveReport, "Docs/AI/FormulaCircuits/DriveBeforeAltitudeFix-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json", true);
                var report = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(driveReport));
                foreach (var result in report["results"])
                    if (correctedTrackIds.Contains((string)result["track"])) { result["passed"] = false; result["reason"] = "Checkpoint altitude corrected; repeat driving validation."; }
                File.WriteAllText(driveReport, report.ToString());
            }
            WriteReport("complete", null); Finish(); Debug.Log("FORMULA_18_CIRCUITS_BUILD_COMPLETE");
            if (validateAfterBuild) { validateAfterBuild = false; if (resumeAfterBuild) FormulaCircuitPlayValidation.Resume(); else FormulaCircuitPlayValidation.Run(); resumeAfterBuild = false; }
        }
        catch (Exception failure) { WriteReport("failed", failure.ToString()); Finish(); Debug.LogException(failure); }
    }
    private static string Id(int index) => index < 8 ? "track_" + (index + 1).ToString("00") : "rtg_" + (index - 7).ToString("00");
    private static string PathFor(int index) => "Assets/Formula/Scenes/" + (index < 8 ? "Formula_Circuit_" + (index + 1).ToString("00") : "Formula_RTG_" + (index - 7).ToString("00")) + ".unity";
    private static void Finish()
    {
        running = false; EditorUtility.ClearProgressBar();
        if (previous != null) EditorSceneManager.RestoreSceneManagerSetup(previous);
    }
    private static void WriteReport(string phase, string error) => File.WriteAllText(ReportPath, JsonConvert.SerializeObject(new { phase, completed = results.Count, next, error, circuits = results, reloaded, heightCorrections }, Formatting.Indented));

    private static void BuildCircuit(int index)
    {
        string path = PathFor(index); Scene scene;
        if (File.Exists(path)) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        else if (index < 8)
        {
            string source = "Assets/Racing_Visuals/Scenes/Race_Track_" + (index + 1) + "_Visual.unity";
            scene = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
            // Save a copy before touching source content or scene-local meshes.
            if (!EditorSceneManager.SaveScene(scene, path, true)) throw new InvalidOperationException("Could not copy " + source);
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Camera_Parent" || root.name.StartsWith("RCCP_")) Object.DestroyImmediate(root);
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var random = UnityEngine.Random.state;
            var generatorObject = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RTG 1.3/TrackGenerator.prefab"));
            try
            {
                var generator = generatorObject.GetComponent<Track_Generator>();
                // Tunnel variants in this legacy pack have an open road collider
                // at their module seam. Author circuits from the continuous variants.
                foreach (var field in typeof(Track_Generator.Tipos).GetFields())
                    if (field.GetValue(generator.track) is GameObject[] modules)
                    {
                        var continuous = modules.Where(m => m != null && !m.name.Contains("(T)")).ToArray();
                        if (continuous.Length == 0) throw new InvalidOperationException("No continuous RTG module for " + field.Name);
                        field.SetValue(generator.track, continuous);
                    }
                UnityEngine.Random.InitState(Seeds[index - 8]);
                if (!generator.CreateTrack(true, false, index < 12 ? 1 : 2)) throw new InvalidOperationException("RTG generation failed.");
            }
            finally { UnityEngine.Random.state = random; Object.DestroyImmediate(generatorObject); }
            var sun = new GameObject("Lighting").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.6f; sun.transform.rotation = Quaternion.Euler(42, -32, 0); sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .65f, .8f); RenderSettings.ambientEquatorColor = new Color(.38f, .41f, .45f); RenderSettings.ambientGroundColor = new Color(.18f, .19f, .2f);
            if (raceSky != null) RenderSettings.skybox = raceSky;
        }
        SceneManager.SetActiveScene(scene);
        if (index < 8)
            foreach (var root in scene.GetRootGameObjects().Where(r => r.name == "Camera_Parent").ToArray()) Object.DestroyImmediate(root);
        var roots = scene.GetRootGameObjects();
        var game = roots.SelectMany(r => r.GetComponentsInChildren<GamePlayManager>(true)).FirstOrDefault();
        if (game == null) game = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath), scene)).GetComponentInChildren<GamePlayManager>(true);
        game.gameObject.SetActive(true);
        var circuit = game.GetComponent<FormulaCircuit>();
        bool authored = circuit != null;
        if (!authored) circuit = game.gameObject.AddComponent<FormulaCircuit>();
        var roadColliders = new List<Collider>(); List<Vector3> centers; float halfWidth;
        if (index < 8) centers = VisualRoute(scene, index, roadColliders, out halfWidth);
        else centers = RTGRoute(scene, roadColliders, out halfWidth);
        if (centers.Count < 12) throw new InvalidOperationException("Too few route sections for " + path);
        if (index >= 8) File.WriteAllText("Docs/AI/FormulaCircuits/RTGGeometry.json", JsonConvert.SerializeObject(new { index, centers = centers.Select(v => new[] { v.x, v.y, v.z }).ToArray(), halfWidth, roads = roadColliders.Select(c => new { path = AnimationUtility.CalculateTransformPath(c.transform, null), min = c.bounds.min.ToString("F3"), max = c.bounds.max.ToString("F3") }).ToArray() }, Formatting.Indented));
        float length = Length(centers);
        string fingerprint = string.Join("|", centers.Where((p, i) => i % 10 == 0).Select(p => p.ToString("F1")));
        if (index >= 8 && fingerprints.Contains(fingerprint)) throw new InvalidOperationException("Duplicate RTG layout: " + path);
        if (index >= 8) fingerprints.Add(fingerprint);
        if (!authored || index == 7)
        {
            var old = roots.SelectMany(r => r.GetComponentsInChildren<RCCP_AIWaypointsContainer>(true)).FirstOrDefault();
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var routeRoot = new GameObject("FormulaWaypoints");
            var route = routeRoot.AddComponent<RCCP_AIWaypointsContainer>(); route.waypoints.Clear();
            var sampled = Resample(centers, 12);
            // Legacy RTG waypoints span whole straights. Interpolating their Y
            // values can put checkpoints below an elevated road or a ramp.
            for (int i = 0; i < sampled.Count; i++)
                if (RoadHit(roadColliders, sampled[i], out var ground)) sampled[i] = ground;
            for (int i = 0; i < sampled.Count; i++)
            {
                var point = new GameObject("Checkpoint_" + i.ToString("000")); point.transform.SetParent(routeRoot.transform);
                point.transform.SetPositionAndRotation(sampled[i] + Vector3.up * .35f, Quaternion.LookRotation(sampled[(i + 1) % sampled.Count] - sampled[i]));
                route.waypoints.Add(point.AddComponent<RCCP_Waypoint>());
            }
            circuit.route = route; game.raceWaypoints = route;
        }
        circuit.aiCornerSpeedScale = index == 7 ? .6f : index >= 8 ? .8f : 1;
        circuit.trackId = Id(index); circuit.sourcePack = index < 8 ? "Racing Visuals" : "RTG 1.3";
        circuit.halfWidth = halfWidth; circuit.lengthMeters = length; circuit.roadColliders = roadColliders.ToArray();
        game.raceWaypoints = circuit.route;
        if (index >= 8)
        {
            var points = circuit.route.waypoints.Select(w => w.transform.position - Vector3.up * .35f).ToArray();
            var changes = new List<object>();
            for (int i = 0; i < points.Length; i++)
            {
                // At a crossing, the surrounding route identifies the intended
                // deck. A first-hit ray can select the road seven metres above it.
                var heights = Enumerable.Range(-3, 7).Select(k => points[(i + k + points.Length) % points.Length].y).OrderBy(y => y).ToArray();
                Vector3 query = points[i]; query.y = heights[3];
                if (RoadHit(roadColliders, query, out var ground))
                {
                    if (Mathf.Abs(ground.y - points[i].y) > .05f) changes.Add(new { checkpoint = i, before = points[i].y, after = ground.y });
                    circuit.route.waypoints[i].transform.position = ground + Vector3.up * .35f;
                }
            }
            heightCorrections.Add(new { circuit.trackId, changes });
            if (changes.Count > 0) correctedTrackIds.Add(circuit.trackId);
            length = Length(circuit.route.waypoints.Select(w => w.transform.position - Vector3.up * .35f).ToList());
            circuit.lengthMeters = length;
        }
        if (index == 7) { BuildTerrainBarriers(scene, centers, halfWidth); ClearTerrainObstacles(scene, circuit, centers); }
        game.useCurrentMapModeSettings = false; game.usePlayerCarForOpponents = true; game.copyPlayerPerformanceOnSpawn = false; game.enableDynamicRacePacing = false;
        game.RaceType = RaceType.Racing; game.totalRaceLaps = 3; game.opponentCount = 3;
        game.checkpointPrefab = null;
        game.autoSpawnOpponents = true; game.showLiveLeaderboard = true;
        foreach (var legacyCanvas in game.transform.root.GetComponentsInChildren<Canvas>(true))
        {
            legacyCanvas.enabled = false;
            if (PrefabUtility.IsPartOfPrefabInstance(legacyCanvas)) PrefabUtility.RecordPrefabInstancePropertyModifications(legacyCanvas);
        }
        game.waypointReachDistance = Mathf.Clamp(halfWidth * .65f, 4f, 7f);
        var raceUI = game.GetComponent<FormulaRaceUI>(); if (raceUI == null) raceUI = game.gameObject.AddComponent<FormulaRaceUI>(); raceUI.trackHalfWidth = halfWidth + .5f;
        if (index == 7)
        {
            foreach (var point in circuit.route.waypoints)
                if (RoadHit(roadColliders, point.transform.position, out var ground)) point.transform.position = ground + Vector3.up * .35f;
        }
        if (!authored || index == 7 || (halfWidth < 6 && circuit.startingGrid.Length > 1 &&
            Mathf.Abs(Vector3.Dot(circuit.startingGrid[1].position - circuit.startingGrid[0].position, circuit.startingGrid[0].right)) > 1))
        {
            if (authored)
            {
                if (index == 7) File.WriteAllText("Docs/AI/FormulaCircuits/TerrainGridClearance.json", JsonConvert.SerializeObject(circuit.startingGrid.Select(slot => new { slot.name, blockers = GridBlockers(slot, circuit).Select(c => c.name).ToArray() }), Formatting.Indented));
                Object.DestroyImmediate(circuit.startFinish.parent.gameObject);
            }
            BuildGrid(circuit, game);
        }
        if (!authored || index == 7) BakeNavigation(scene, roadColliders, path);
        CleanPresentation(scene, index);
        circuit.BuildSpeedPlan();
        if (PrefabUtility.IsPartOfPrefabInstance(game)) PrefabUtility.RecordPrefabInstancePropertyModifications(game);
        if (PrefabUtility.IsPartOfPrefabInstance(raceUI)) PrefabUtility.RecordPrefabInstancePropertyModifications(raceUI);
        var checks = Validate(scene, circuit, game);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, path);
        var asset = FormulaAssets.Current; var track = asset.FindTrack(circuit.trackId);
        if (track == null)
        {
            track = new FormulaAssets.Track { id = circuit.trackId, displayName = Names[index], scenePath = path, referenceLapSeconds = length / 32, referenceCalibrated = false };
            asset.tracks = asset.tracks.Concat(new[] { track }).ToArray();
        }
        track.displayName = Names[index]; track.sourcePack = circuit.sourcePack; track.lengthMeters = length;
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
        results.Add(new { id = circuit.trackId, name = track.displayName, path, lengthMeters = length, halfWidth, checkpoints = circuit.route.waypoints.Count, grid = circuit.startingGrid.Length, roadColliders = roadColliders.Count, checks });
    }

    private static List<Vector3> VisualRoute(Scene scene, int index, List<Collider> colliders, out float halfWidth)
    {
        var filters = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).ToArray();
        if (index == 6)
        {
            // The city export stores its asphalt in five separate strips.
            var names = new HashSet<string> { "polySurface2", "polySurface4", "polySurface5", "polySurface6", "polySurface7" };
            File.WriteAllText("Docs/AI/FormulaCircuits/CityGeometry.json", JsonConvert.SerializeObject(filters.Where(m => names.Contains(m.name) && m.sharedMesh != null && m.sharedMesh.vertexCount > 0).Select(m =>
            {
                var p = Strip(m, out float w);
                return new { path = AnimationUtility.CalculateTransformPath(m.transform, null), mesh = m.sharedMesh.name, width = w, active = m.gameObject.activeInHierarchy,
                    visible = m.GetComponent<Renderer>() != null && m.GetComponent<Renderer>().enabled, points = p.Select(v => new[] { v.x, v.y, v.z }).ToArray() };
            }).ToArray(), Formatting.Indented));
            var strips = new List<List<Vector3>>(); halfWidth = float.MaxValue;
            foreach (var part in filters.Where(m => names.Contains(m.name) && m.sharedMesh != null && m.sharedMesh.vertexCount > 0)
                .GroupBy(m => m.sharedMesh).Select(g => g.OrderByDescending(m => m.gameObject.activeInHierarchy && m.GetComponent<Renderer>() != null && m.GetComponent<Renderer>().enabled).First()))
            {
                var strip = Strip(part, out float width); halfWidth = Mathf.Min(halfWidth, width); strips.Add(strip); AddRoadCollider(part, colliders);
            }
            if (strips.Count != 5) throw new InvalidOperationException("City asphalt requires its five source strips.");
            var joined = strips[0]; strips.RemoveAt(0);
            while (strips.Count > 0)
            {
                var end = joined[joined.Count - 1];
                var candidate = strips.Select((s, i) => new { i, reverse = Vector3.Distance(end, s[s.Count - 1]) < Vector3.Distance(end, s[0]), distance = Mathf.Min(Vector3.Distance(end, s[0]), Vector3.Distance(end, s[s.Count - 1])) }).OrderBy(c => c.distance).First();
                if (candidate.distance > 25) throw new InvalidOperationException("City asphalt has a gap: " + candidate.distance);
                var strip = strips[candidate.i]; if (candidate.reverse) strip.Reverse(); joined.AddRange(strip); strips.RemoveAt(candidate.i);
            }
            if (Vector3.Distance(joined[0], joined[joined.Count - 1]) > 25) throw new InvalidOperationException("City circuit does not close.");
            return RotateToStraight(joined);
        }
        var road = filters.FirstOrDefault(m => m.name == "road_0001" && m.sharedMesh != null && m.sharedMesh.vertexCount > 100);
        if (road == null) road = filters.FirstOrDefault(m => m.name == "Minimap_Road" && m.sharedMesh != null && m.sharedMesh.vertexCount > 100);
        if (road == null) throw new InvalidOperationException("Road strip needs explicit mapping for visual circuit " + (index + 1));
        var centers = Strip(road, out halfWidth);
        centers = RotateToStraight(centers);
        AddRoadCollider(road, colliders);
        if (!road.gameObject.activeInHierarchy) road.gameObject.SetActive(true);
        if (index == 7)
        {
            centers = TerrainPavement(scene, centers, colliders);
            halfWidth = 8;
        }
        if (index == 7) File.WriteAllText("Docs/AI/FormulaCircuits/TerrainGeometry.json", JsonConvert.SerializeObject(new { halfWidth, centers = centers.Select(v => new[] { v.x, v.y, v.z }).ToArray(), position = road.transform.position.ToString("F3"), scale = road.transform.lossyScale.ToString("F3"), rotation = road.transform.eulerAngles.ToString("F3") }, Formatting.Indented));
        return centers;
    }
    private static List<Vector3> TerrainPavement(Scene scene, List<Vector3> source, List<Collider> roads)
    {
        // The off-road minimap has steep banks and some inverted triangles. A
        // Formula car needs a continuous paved ribbon, not that display mesh.
        var terrain = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
        var original = AssetDatabase.LoadAssetAtPath<TerrainData>(AssetDatabase.GUIDToAssetPath("9f803f3ab7b469c4c9db1b9b6a8a877e"));
        if (original == null) throw new InvalidOperationException("Original terrain data is missing.");
        // The minimap strip folds back on itself. A clean Formula route is
        // authored within this desert venue, independently of that display mesh.
        var controls = new List<Vector3> { new Vector3(440, 0, 520), new Vector3(440, 0, 480), new Vector3(410, 0, 455), new Vector3(365, 0, 485), new Vector3(335, 0, 540), new Vector3(300, 0, 610), new Vector3(245, 0, 670), new Vector3(260, 0, 710), new Vector3(360, 0, 725), new Vector3(480, 0, 725), new Vector3(580, 0, 710), new Vector3(635, 0, 665), new Vector3(650, 0, 565), new Vector3(625, 0, 475), new Vector3(590, 0, 430), new Vector3(550, 0, 450), new Vector3(530, 0, 510), new Vector3(490, 0, 570), new Vector3(455, 0, 570), new Vector3(440, 0, 545) };
        for (int pass = 0; pass < 3; pass++)
        {
            var rounded = new List<Vector3>();
            for (int i = 0; i < controls.Count; i++)
            {
                rounded.Add(Vector3.Lerp(controls[i], controls[(i + 1) % controls.Count], .25f));
                rounded.Add(Vector3.Lerp(controls[i], controls[(i + 1) % controls.Count], .75f));
            }
            controls = rounded;
        }
        var centers = Resample(RotateToStraight(controls), 5); const float width = 8;
        for (int i = 0; i < centers.Count; i++)
        {
            var point = centers[i]; var forward = centers[(i + 1) % centers.Count] - point; forward.y = 0;
            var side = Vector3.Cross(Vector3.up, forward.normalized); float height = float.NegativeInfinity;
            foreach (float offset in new[] { -width, 0f, width })
            {
                var local = terrain.transform.InverseTransformPoint(point + side * offset);
                float y = original.GetInterpolatedHeight(local.x / original.size.x, local.z / original.size.z);
                height = Mathf.Max(height, terrain.transform.TransformPoint(new Vector3(local.x, y, local.z)).y);
            }
            point.y = height + .12f; centers[i] = point;
        }
        // A low Formula chassis needs an engineered vertical profile. The source
        // off-road terrain has abrupt 38% crests that trap the underbody.
        for (int pass = 0; pass < 4; pass++)
        {
            var heights = centers.Select(p => p.y).ToArray();
            for (int i = 0; i < centers.Count; i++)
            {
                float sum = 0, weights = 0;
                for (int k = -10; k <= 10; k++) { float weight = 11 - Mathf.Abs(k); sum += heights[(i + k + heights.Length) % heights.Length] * weight; weights += weight; }
                var point = centers[i]; point.y = sum / weights; centers[i] = point;
            }
        }
        for (int pass = 0; pass < centers.Count; pass++)
            for (int i = 0; i < centers.Count; i++)
            {
                int next = (i + 1) % centers.Count; var delta = centers[next] - centers[i]; delta.y = 0;
                float rise = delta.magnitude * .08f;
                var point = centers[i]; point.y = Mathf.Min(point.y, centers[next].y + rise); centers[i] = point;
                point = centers[next]; point.y = Mathf.Min(point.y, centers[i].y + rise); centers[next] = point;
            }
        GradeTerrain(terrain, original, centers, width);
        var vertices = new Vector3[(centers.Count + 1) * 2]; var uv = new Vector2[vertices.Length]; var triangles = new int[centers.Count * 6];
        float distance = 0;
        for (int i = 0; i <= centers.Count; i++)
        {
            int at = i % centers.Count; var forward = centers[(at + 1) % centers.Count] - centers[(at + centers.Count - 1) % centers.Count]; forward.y = 0;
            var side = Vector3.Cross(Vector3.up, forward.normalized);
            vertices[i * 2] = centers[at] - side * width; vertices[i * 2 + 1] = centers[at] + side * width;
            if (i > 0) distance += Vector3.Distance(centers[(i - 1) % centers.Count], centers[at]);
            uv[i * 2] = new Vector2(0, distance / 4); uv[i * 2 + 1] = new Vector2(1, distance / 4);
            if (i == centers.Count) continue;
            int v = i * 2, t = i * 6; triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1; triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        string path = Folder + "/Meshes/TerrainPavement.asset";
        var mesh = new Mesh { name = "Terrain Formula Pavement", vertices = vertices, uv = uv, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
        else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
        var asphalt = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Formula Asphalt");
        if (asphalt == null) asphalt = new GameObject("Formula Asphalt");
        asphalt.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); asphalt.transform.localScale = Vector3.one;
        var filter = asphalt.GetComponent<MeshFilter>(); if (filter == null) filter = asphalt.AddComponent<MeshFilter>(); filter.sharedMesh = saved;
        var renderer = asphalt.GetComponent<MeshRenderer>(); if (renderer == null) renderer = asphalt.AddComponent<MeshRenderer>(); renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Racing_Game/Textures/Roads/Materials/Asphalt_1.mat");
        foreach (var old in roads)
        {
            old.enabled = false;
            if (PrefabUtility.IsPartOfPrefabInstance(old)) PrefabUtility.RecordPrefabInstancePropertyModifications(old);
        }
        roads.Clear(); AddRoadCollider(filter, roads);
        return centers;
    }
    private static void ClearTerrainObstacles(Scene scene, FormulaCircuit circuit, List<Vector3> points)
    {
        Physics.SyncTransforms(); var cleared = new HashSet<Collider>();
        for (int i = 0; i < points.Count; i++)
        {
            var direction = points[(i + 1) % points.Count] - points[i];
            foreach (var collider in Physics.OverlapBox(points[i] + Vector3.up * 1.1f, new Vector3(circuit.halfWidth, .6f, 3), Quaternion.LookRotation(direction), ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider is TerrainCollider || circuit.roadColliders.Contains(collider) || collider.transform.IsChildOf(circuit.transform.root) || collider.transform.root.name == "Formula Barriers") continue;
                cleared.Add(collider);
            }
        }
        File.WriteAllText("Docs/AI/FormulaCircuits/ClearedTerrainObstacles.json", JsonConvert.SerializeObject(cleared.Select(c => AnimationUtility.CalculateTransformPath(c.transform, null)).ToArray(), Formatting.Indented));
        foreach (var collider in cleared)
        {
            collider.enabled = false;
            if (PrefabUtility.IsPartOfPrefabInstance(collider)) PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            var renderer = collider.GetComponent<Renderer>();
            if (renderer != null) { renderer.enabled = false; if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
        }
    }
    private static void BuildTerrainBarriers(Scene scene, List<Vector3> points, float width)
    {
        var old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Formula Barriers");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Formula Barriers");
        var red = Solid("Barrier Red", new Color(.7f, .025f, .018f)); var white = Solid("Barrier White", new Color(.8f, .8f, .75f));
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                var before = points[(i + points.Count - 1) % points.Count]; var after = points[(j + 1) % points.Count];
                Vector3 a = points[i] + Vector3.Cross(Vector3.up, (points[j] - before).normalized).normalized * (width + .2f) * side;
                Vector3 b = points[j] + Vector3.Cross(Vector3.up, (after - points[i]).normalized).normalized * (width + .2f) * side;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Guardrail " + side + " " + i.ToString("000"); wall.transform.SetParent(root.transform);
                wall.transform.SetPositionAndRotation((a + b) * .5f + Vector3.up * .35f, Quaternion.LookRotation(b - a));
                wall.transform.localScale = new Vector3(.4f, .7f, Vector3.Distance(a, b) + .15f);
                wall.GetComponent<Renderer>().sharedMaterial = i / 3 % 2 == 0 ? red : white; wall.isStatic = true;
            }
    }
    private static void GradeTerrain(Terrain terrain, TerrainData original, List<Vector3> road, float width)
    {
        string path = Folder + "/Meshes/TerrainCircuitGround.asset";
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (data == null) { data = Object.Instantiate(original); data.name = "Terrain Circuit Ground"; AssetDatabase.CreateAsset(data, path); }
        int size = original.heightmapResolution; var heights = original.GetHeights(0, 0, size, size);
        const float shoulder = 8;
        for (int i = 0; i < road.Count; i++)
        {
            Vector3 a = terrain.transform.InverseTransformPoint(road[i]), b = terrain.transform.InverseTransformPoint(road[(i + 1) % road.Count]);
            float radius = width + shoulder;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius) / data.size.x * (size - 1)), 0, size - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius) / data.size.x * (size - 1)), 0, size - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z, b.z) - radius) / data.size.z * (size - 1)), 0, size - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z, b.z) + radius) / data.size.z * (size - 1)), 0, size - 1);
            var segment = new Vector2(b.x - a.x, b.z - a.z);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                var offset = new Vector2((float)x / (size - 1) * data.size.x - a.x, (float)z / (size - 1) * data.size.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(offset, segment) / Mathf.Max(.001f, segment.sqrMagnitude));
                float distance = (offset - segment * t).magnitude;
                if (distance > radius) continue;
                float target = (Mathf.Lerp(a.y, b.y, t) - .45f) / data.size.y;
                float blend = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(width + 2, radius, distance));
                heights[z, x] = Mathf.Min(heights[z, x], Mathf.Lerp(heights[z, x], target, blend));
            }
        }
        data.SetHeights(0, 0, heights); EditorUtility.SetDirty(data);
        terrain.terrainData = data; terrain.GetComponent<TerrainCollider>().terrainData = data;
        if (PrefabUtility.IsPartOfPrefabInstance(terrain)) PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
        if (PrefabUtility.IsPartOfPrefabInstance(terrain.GetComponent<TerrainCollider>())) PrefabUtility.RecordPrefabInstancePropertyModifications(terrain.GetComponent<TerrainCollider>());
    }
    private static void AddRoadCollider(MeshFilter road, List<Collider> colliders)
    {
        var collider = road.GetComponent<MeshCollider>(); if (collider == null) collider = road.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = road.sharedMesh; collider.convex = false; collider.enabled = true; collider.isTrigger = false; road.gameObject.layer = 0; colliders.Add(collider);
    }
    private static List<Vector3> Strip(MeshFilter road, out float halfWidth)
    {
        ReadMesh(road.sharedMesh, out Vector3[] vertices, out Vector2[] uv);
        // Some imported strips have internal width divisions and split UV seams.
        // Their extreme U coordinates still identify the two asphalt edges.
        var rows = Enumerable.Range(0, vertices.Length).GroupBy(i => Mathf.RoundToInt(uv[i].y * 1000)).OrderBy(g => g.Key)
            .Where(g => g.Max(i => uv[i].x) - g.Min(i => uv[i].x) > .1f).ToArray();
        var edges = rows.Select(g => new[] { road.transform.TransformPoint(vertices[g.OrderBy(i => uv[i].x).First()]), road.transform.TransformPoint(vertices[g.OrderBy(i => uv[i].x).Last()]) }).ToArray();
        if (edges.Length < 8) throw new InvalidOperationException("No continuous road UV strip: " + road.name);
        var centers = edges.Select(e => (e[0] + e[1]) * .5f).ToList();
        var gaps = Enumerable.Range(0, centers.Count - 1).Select(i => new { index = i, distance = Vector3.Distance(centers[i], centers[i + 1]) }).ToArray();
        var largest = gaps.OrderByDescending(g => g.distance).First();
        float typical = gaps.Select(g => g.distance).OrderBy(d => d).ElementAt(gaps.Length / 2);
        // A city strip wraps its texture coordinates halfway through the mesh.
        // Move that UV seam to the open ends instead of connecting across town.
        if (largest.distance > Mathf.Max(40, typical * 5))
            centers = centers.Skip(largest.index + 1).Concat(centers.Take(largest.index + 1)).ToList();
        if (Vector3.Distance(centers[0], centers[centers.Count - 1]) < 1) centers.RemoveAt(centers.Count - 1);
        halfWidth = edges.Min(e => Vector3.Distance(e[0], e[1]) * .5f);
        return centers;
    }
    private static List<Vector3> RotateToStraight(List<Vector3> centers)
    {
        // Prefer a straight large enough for a four-row grid; do not start on a hairpin.
        int start = Enumerable.Range(0, centers.Count).OrderBy(i => Vector3.Angle(centers[(i + 8) % centers.Count] - centers[i], centers[i] - centers[(i + centers.Count - 8) % centers.Count])).First();
        return Enumerable.Range(0, centers.Count).Select(i => centers[(start + i) % centers.Count]).ToList();
    }

    private static List<Vector3> RTGRoute(Scene scene, List<Collider> colliders, out float halfWidth)
    {
        var roots = scene.GetRootGameObjects();
        var route = roots.SelectMany(r => r.GetComponentsInChildren<WaypointCircuit>(true)).FirstOrDefault();
        List<Vector3> points;
        if (route != null) points = route.transform.Cast<Transform>().Select(t => t.position).ToList();
        else
        {
            var existing = roots.SelectMany(r => r.GetComponentsInChildren<FormulaCircuit>(true)).First();
            points = existing.route.waypoints.Select(w => w.transform.position - Vector3.up * .35f).ToList();
        }
        for (int i = points.Count - 1; i > 0; i--) if (Vector3.Distance(points[i], points[i - 1]) < .5f) points.RemoveAt(i);
        if (Vector3.Distance(points[0], points[points.Count - 1]) < 1) points.RemoveAt(points.Count - 1);
        foreach (var mesh in roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).Where(m => m.name == "Collider-Road"))
        {
            var collider = mesh.GetComponent<MeshCollider>(); if (collider == null) collider = mesh.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = mesh.sharedMesh; collider.enabled = true; collider.isTrigger = false; colliders.Add(collider);
        }
        if (colliders.Count == 0) throw new InvalidOperationException("RTG road collision source missing.");
        // Width is measured by road-only collider rays rather than the grass or scenery.
        Physics.SyncTransforms(); halfWidth = 9;
        foreach (int i in Enumerable.Range(0, points.Count).Where(i => i % 4 == 0))
        {
            Vector3 tangent = (points[(i + 1) % points.Count] - points[i]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            for (float w = 3; w <= 12; w += .5f)
            {
                if (!RoadHit(colliders, points[i] + side * w, out _) || !RoadHit(colliders, points[i] - side * w, out _)) { halfWidth = Mathf.Min(halfWidth, w - .5f); break; }
            }
        }
        foreach (var old in roots.SelectMany(r => r.GetComponentsInChildren<WaypointCircuit>(true)).ToArray()) Object.DestroyImmediate(old.gameObject);
        foreach (var config in roots.SelectMany(r => r.GetComponentsInChildren<Track_Config>(true)).ToArray()) Object.DestroyImmediate(config);
        return points;
    }
    internal static bool RoadHit(IReadOnlyList<Collider> roads, Vector3 point, out Vector3 hit) => FormulaCircuit.SampleRoad(roads, point, out hit);

    private static void ReadMesh(Mesh mesh, out Vector3[] vertices, out Vector2[] uv)
    {
        using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
        using (var positions = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
        using (var coords = new NativeArray<Vector2>(mesh.vertexCount, Allocator.Temp))
        {
            data[0].GetVertices(positions); data[0].GetUVs(0, coords); vertices = positions.ToArray(); uv = coords.ToArray();
        }
    }
    private static float Length(IList<Vector3> points) { float sum = 0; for (int i = 0; i < points.Count; i++) sum += Vector3.Distance(points[i], points[(i + 1) % points.Count]); return sum; }
    private static Vector3 At(IList<Vector3> points, float distance)
    {
        distance = Mathf.Repeat(distance, Length(points));
        for (int i = 0; i < points.Count; i++) { float span = Vector3.Distance(points[i], points[(i + 1) % points.Count]); if (distance <= span) return Vector3.Lerp(points[i], points[(i + 1) % points.Count], distance / Mathf.Max(.001f, span)); distance -= span; }
        return points[0];
    }
    private static List<Vector3> Resample(List<Vector3> points, float spacing)
    {
        float length = Length(points); int count = Mathf.CeilToInt(length / spacing);
        return Enumerable.Range(0, count).Select(i => At(points, i * length / count)).ToList();
    }
    private static void BuildGrid(FormulaCircuit circuit, GamePlayManager game)
    {
        var points = circuit.route.waypoints.Select(w => w.transform.position - Vector3.up * .35f).ToList(); float length = Length(points);
        var root = new GameObject("Starting Grid"); root.transform.SetParent(circuit.transform);
        var line = new GameObject("Start Finish"); line.transform.SetParent(root.transform);
        Vector3 finish = points[points.Count - 1], forward = (points[0] - finish).normalized;
        line.transform.SetPositionAndRotation(finish + Vector3.up * .03f, Quaternion.LookRotation(forward)); circuit.startFinish = line.transform;
        var grid = new Transform[8];
        int columns = circuit.halfWidth >= 6 ? 2 : 1;
        for (int i = 0; i < grid.Length; i++)
        {
            float distance = length - Vector3.Distance(points[0], finish) - 9 - i / columns * 10 - (columns == 2 ? i % 2 * 3 : 0);
            Vector3 center = At(points, distance); Vector3 direction = (At(points, distance + 3) - center).normalized;
            float lateral = columns == 1 ? 0 : (i % 2 == 0 ? -1 : 1) * Mathf.Min(2, circuit.halfWidth - 1.5f);
            Vector3 position = center + Vector3.Cross(Vector3.up, direction).normalized * lateral;
            if (!RoadHit(circuit.roadColliders, position, out var ground)) throw new InvalidOperationException("Grid slot is not on the road: " + i);
            var slot = new GameObject(i == 0 ? "Player P01" : "Opponent P" + (i + 1).ToString("00")); slot.transform.SetParent(root.transform);
            slot.transform.SetPositionAndRotation(ground + Vector3.up * .35f, Quaternion.LookRotation(direction)); grid[i] = slot.transform;
        }
        circuit.startingGrid = grid;
        game.SpawnPoint.SetPositionAndRotation(grid[0].position, grid[0].rotation); game.opponentSpawnPoints = grid.Skip(1).ToArray();
        Checker(line.transform, circuit.halfWidth);
    }
    private static void Checker(Transform parent, float halfWidth)
    {
        var black = Solid("Grid Black", new Color(.012f, .012f, .015f)); var white = Solid("Grid White", new Color(.85f, .85f, .8f));
        for (int r = 0; r < 2; r++) for (int c = 0; c < 16; c++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube); tile.name = "Line " + r + " " + c; Object.DestroyImmediate(tile.GetComponent<Collider>());
            tile.transform.SetParent(parent, false); tile.transform.localPosition = new Vector3(-halfWidth + (c + .5f) * halfWidth / 8, 0, (r - .5f) * .7f);
            tile.transform.localScale = new Vector3(halfWidth / 8, .008f, .7f); tile.GetComponent<Renderer>().sharedMaterial = (r + c) % 2 == 0 ? black : white;
        }
    }
    private static Material Solid(string name, Color color)
    {
        string path = Folder + "/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = name; material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .15f); AssetDatabase.CreateAsset(material, path); }
        return material;
    }
    private static void BakeNavigation(Scene scene, IEnumerable<Collider> colliders, string path)
    {
        foreach (var old in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NavMeshSurface>(true)).ToArray()) Object.DestroyImmediate(old.gameObject);
        var navigation = new GameObject("FormulaNavigation"); var surface = navigation.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true; surface.voxelSize = .3f;
        var sources = new GameObject("Bake Sources"); sources.transform.SetParent(navigation.transform);
        foreach (var c in colliders.OfType<MeshCollider>())
        {
            var source = new GameObject("Road Source"); source.transform.SetParent(sources.transform);
            source.transform.SetPositionAndRotation(c.transform.position, c.transform.rotation); source.transform.localScale = c.transform.lossyScale;
            source.AddComponent<MeshCollider>().sharedMesh = c.sharedMesh;
        }
        surface.BuildNavMesh();
        if (surface.navMeshData == null) throw new InvalidOperationException("Navigation baking failed.");
        string navPath = "Assets/Formula/Navigation/" + System.IO.Path.GetFileNameWithoutExtension(path) + ".asset";
        var oldData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if (oldData == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        else { EditorUtility.CopySerialized(surface.navMeshData, oldData); surface.RemoveData(); surface.navMeshData = oldData; surface.AddData(); EditorUtility.SetDirty(oldData); }
        Object.DestroyImmediate(sources);
    }
    private static void CleanPresentation(Scene scene, int index)
    {
        // Display cameras from the generator are unnecessary during an actual race.
        foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray())
            if (camera.GetComponentInParent<Track_Config>() != null || camera.name.Contains("BigScreen")) Object.DestroyImmediate(camera.gameObject);
        foreach (var generator in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FreeflightCamera>(true)).ToArray()) Object.DestroyImmediate(generator);
        foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
        {
            if (renderer.GetComponentsInParent<Transform>(true).Any(t => t.name == "Minimap_Road")) { renderer.enabled = false; if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); continue; }
            var materials = renderer.sharedMaterials; bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i]; if (material == null || material.shader == null || material.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                string source = AssetDatabase.GetAssetPath(material);
                string key = AssetDatabase.AssetPathToGUID(source) + "_" + material.name.Replace('/', '_');
                string path = Folder + "/Materials/" + key + ".mat";
                var replacement = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (replacement == null)
                {
                    replacement = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); replacement.name = material.name + " Formula";
                    if (material.HasProperty("_MainTex")) { replacement.SetTexture("_BaseMap", material.GetTexture("_MainTex")); replacement.SetTextureScale("_BaseMap", material.GetTextureScale("_MainTex")); replacement.SetTextureOffset("_BaseMap", material.GetTextureOffset("_MainTex")); }
                    if (material.HasProperty("_Color")) replacement.SetColor("_BaseColor", material.GetColor("_Color"));
                    if (material.IsKeywordEnabled("_ALPHATEST_ON") || material.shader.name.Contains("Cutout")) { replacement.SetFloat("_AlphaClip", 1); replacement.EnableKeyword("_ALPHATEST_ON"); replacement.renderQueue = 2450; }
                    replacement.SetFloat("_Smoothness", .18f); replacement.SetFloat("_Cull", 0);
                    AssetDatabase.CreateAsset(replacement, path);
                }
                materials[i] = replacement; changed = true;
            }
            if (changed)
            {
                renderer.sharedMaterials = materials;
                if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
    }
    private static Collider[] GridBlockers(Transform slot, FormulaCircuit circuit) =>
        Physics.OverlapBox(slot.position + Vector3.up * .6f, new Vector3(1.15f, .35f, 2.6f), slot.rotation, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => !(c is TerrainCollider) && !circuit.roadColliders.Contains(c) && !c.transform.IsChildOf(circuit.transform.root)).ToArray();
    private static object Validate(Scene scene, FormulaCircuit circuit, GamePlayManager game)
    {
        Physics.SyncTransforms();
        var route = circuit.route.waypoints; var errors = new List<string>(); int navSamples = 0, roadSamples = 0;
        if (circuit.aiSpeedPlan == null || circuit.aiSpeedPlan.Length != route.Count) errors.Add("AI speed plan missing.");
        else for (int i = 0; i < route.Count; i++)
        {
            int next = (i + 1) % route.Count;
            float speed = circuit.aiSpeedPlan[i], nextSpeed = circuit.aiSpeedPlan[next];
            float distance = Vector3.Distance(route[i].transform.position, route[next].transform.position);
            if (float.IsNaN(speed) || speed < 20 || speed > 170 || speed * speed > nextSpeed * nextSpeed + 9 * distance * 3.6f * 3.6f + .1f)
                errors.Add("Unsafe AI braking profile at " + i);
        }
        for (int i = 0; i < route.Count; i++)
        {
            Vector3 p = route[i].transform.position;
            if (RoadHit(circuit.roadColliders, p, out _)) roadSamples++; else errors.Add("Checkpoint not over road " + i);
            if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) navSamples++; else errors.Add("Checkpoint not on navigation " + i);
            var next = route[(i + 1) % route.Count].transform.position;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(p, next, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) errors.Add("Navigation disconnected " + i);
            else
            {
                float distance = 0; for (int k = 1; k < path.corners.Length; k++) distance += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                if (distance > Mathf.Max(40, Vector3.Distance(p, next) * 3)) errors.Add("Navigation takes a wrong-deck detour at " + i + ": " + distance.ToString("F1") + " m");
            }
        }
        foreach (var slot in circuit.startingGrid)
        {
            if (!RoadHit(circuit.roadColliders, slot.position, out _) || !NavMesh.SamplePosition(slot.position, out _, 2, NavMesh.AllAreas)) errors.Add("Unsafe grid " + slot.name);
            var blockers = GridBlockers(slot, circuit);
            if (blockers.Length > 0) errors.Add("Grid volume obstructed " + slot.name + ": " + string.Join(",", blockers.Select(c => c.name)));
        }
        foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) errors.Add("Missing script " + transform.name);
        if (errors.Count > 0)
        {
            File.WriteAllText("Docs/AI/FormulaCircuits/FailedRoute.json", JsonConvert.SerializeObject(new { circuit.trackId, errors, points = route.Select(w => new { position = new[] { w.transform.position.x, w.transform.position.y, w.transform.position.z }, onRoad = RoadHit(circuit.roadColliders, w.transform.position, out _) }).ToArray() }, Formatting.Indented));
            throw new InvalidOperationException(circuit.trackId + ": " + string.Join("; ", errors.Take(30)));
        }
        return new { roadSamples, navSamples, connectedLoop = true, validGridSlots = circuit.startingGrid.Length, missingScripts = 0 };
    }
}
