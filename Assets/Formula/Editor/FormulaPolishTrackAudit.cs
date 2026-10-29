using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Offline inspection only: no scene changes and no player-side scanning.
public static class FormulaPolishTrackAudit
{
    public const string Folder = "Docs/AI/FormulaPolish";
    private static readonly List<object> results = new List<object>();
    private static string[] scenes;
    private static int index;
    private static bool running;
    [MenuItem("Tools/Formula Career/Polish/Audit All Track Obstacles")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Track audit requires Edit Mode.");
        scenes = FormulaAssets.Current.tracks.Select(t => t.scenePath).Distinct().ToArray();
        Directory.CreateDirectory(Folder); results.Clear(); index = 0; running = true;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (!running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            string path = scenes[index];
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { results.Add(Inspect(scene)); }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            index++;
            File.WriteAllText(Folder + "/TrackAudit.json", JsonConvert.SerializeObject(new { status = index == scenes.Length ? "complete" : "running", inspected = index, total = scenes.Length, tracks = results }, Formatting.Indented));
            if (index == scenes.Length) { Stop(); Debug.Log("FORMULA_POLISH_TRACK_AUDIT_COMPLETE: " + index); }
        }
        catch (Exception ex) { Stop(); File.WriteAllText(Folder + "/TrackAuditError.txt", ex.ToString()); Debug.LogException(ex); }
    }
    private static void Stop() { running = false; EditorApplication.update -= Tick; }
    private static object Inspect(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var circuit = roots.SelectMany(r => r.GetComponentsInChildren<FormulaCircuit>(true)).Single();
        var points = circuit.route.waypoints.Select(w => w.transform.position).ToArray();
        var grounds = roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Where(c => c.enabled && !c.isTrigger &&
            (c is TerrainCollider || c is MeshCollider) && !Regex.IsMatch(AnimationUtility.CalculateTransformPath(c.transform, null), @"(?i)(trees?|palm|pine|arrow|barrier)")).ToArray();
        var objects = new List<object>(); int trees = 0, arrows = 0;
        foreach (var renderer in roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer) continue;
            string hierarchy = AnimationUtility.CalculateTransformPath(renderer.transform, null);
            string lower = hierarchy.ToLowerInvariant();
            bool arrow = lower.Contains("arrow") || lower.Contains("chevron");
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
            string asset = source.ToLowerInvariant();
            bool tree = Regex.IsMatch(lower + "/" + asset, @"(^|[/_\s-])(trees?|palm|pine|fir|oak)([/_\s\d-]|$)");
            if (!arrow && !tree) continue;
            if (tree) trees++; if (arrow) arrows++;
            var filter = renderer.GetComponent<MeshFilter>();
            if (tree && filter != null && filter.sharedMesh != null && renderer.bounds.size.magnitude > 35)
            {
                var components = FormulaPolishTreeMesh.Inspect(filter.sharedMesh, filter.transform, out _);
                var blocked = new List<object>();
                foreach (var component in components)
                {
                    Vector3 origin = component.bounds.center; origin.y = component.bounds.min.y;
                    float dist = Closest(points, origin, out Vector3 q, out _);
                    if (dist < circuit.halfWidth + .6f && component.bounds.min.y < q.y + 3 && component.bounds.max.y > q.y - .3f)
                        blocked.Add(new { position = V(origin), nearest = V(q), distance = dist, size = V(component.bounds.size), vertices = component.vertices.Count });
                }
                objects.Add(new { hierarchy, source, kind = "combinedTrees", intrusion = blocked.Count > 0, buried = false, sourceMesh = AssetDatabase.GetAssetPath(filter.sharedMesh), totalTreeGroups = components.Count, blocked });
                continue;
            }
            Vector3 anchor = renderer.bounds.center;
            anchor.y = renderer.bounds.min.y;
            float distance = Closest(points, anchor, out Vector3 nearest, out Vector3 tangent);
            float height = anchor.y - nearest.y;
            Vector3 side = Vector3.Cross(Vector3.up, tangent);
            float extent = arrow ? Mathf.Abs(side.x) * renderer.bounds.extents.x + Mathf.Abs(side.z) * renderer.bounds.extents.z : .6f;
            bool intrusion = distance < circuit.halfWidth + extent + .75f && Mathf.Abs(height) < (arrow ? 7 : 4);
            float groundHeight = FormulaCircuit.SampleRoad(grounds, anchor, out Vector3 ground) ? anchor.y - ground.y : height;
            bool buried = arrow && groundHeight < -.15f && groundHeight > -20 && distance < circuit.halfWidth + 20;
            if (!arrow && !intrusion) continue;
            objects.Add(new { hierarchy, source, kind = arrow ? "arrow" : "tree", intrusion, buried, distance, height, groundHeight, position = V(renderer.transform.position), boundsCenter = V(renderer.bounds.center), boundsSize = V(renderer.bounds.size), nearest = V(nearest), tangent = V(tangent) });
        }
        var terrainRows = new List<object>();
        foreach (var terrain in roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
        {
            if (terrain.terrainData == null) continue;
            var data = terrain.terrainData; var instances = data.treeInstances; var blocked = new List<object>();
            for (int i = 0; i < instances.Length; i++)
            {
                Vector3 position = terrain.transform.position + Vector3.Scale(instances[i].position, data.size);
                float distance = Closest(points, position, out Vector3 nearest, out _);
                if (distance < circuit.halfWidth + .8f && Mathf.Abs(position.y - nearest.y) < 4)
                    blocked.Add(new { index = i, prototype = instances[i].prototypeIndex, distance, position = V(position), nearest = V(nearest) });
            }
            terrainRows.Add(new { hierarchy = AnimationUtility.CalculateTransformPath(terrain.transform, null), asset = AssetDatabase.GetAssetPath(data), totalTrees = instances.Length, blocked });
        }
        return new { scene = scene.path, circuit.trackId, circuit.halfWidth, treeRenderers = trees, arrowRenderers = arrows, objects, terrains = terrainRows };
    }
    public static float Closest(Vector3[] points, Vector3 p, out Vector3 closest, out Vector3 tangent)
    {
        float score = float.PositiveInfinity; closest = points[0]; tangent = Vector3.forward;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 a = points[i], b = points[(i + 1) % points.Length];
            Vector3 d = b - a; d.y = 0;
            Vector3 offset = p - a; offset.y = 0;
            float t = d.sqrMagnitude < .0001f ? 0 : Mathf.Clamp01(Vector3.Dot(offset, d) / d.sqrMagnitude);
            Vector3 q = Vector3.Lerp(a, b, t), error = p - q;
            float candidate = error.x * error.x + error.z * error.z + error.y * error.y * 4;
            if (candidate < score) { score = candidate; closest = q; tangent = d.normalized; }
        }
        Vector3 flat = p - closest; flat.y = 0; return flat.magnitude;
    }
    private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
}
