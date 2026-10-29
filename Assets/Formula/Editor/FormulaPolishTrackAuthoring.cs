using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FormulaGame;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FormulaPolishTrackAuthoring
{
    [MenuItem("Tools/Formula Career/Polish/Apply Audited Track Clearance")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before track authoring.");
        string folder = FormulaPolishTrackAudit.Folder;
        var audit = JObject.Parse(File.ReadAllText(folder + "/TrackAudit.json"));
        if ((string)audit["status"] != "complete" || (int)audit["inspected"] != 18) throw new InvalidOperationException("Complete the 18-circuit audit first.");
        File.Copy(folder + "/TrackAudit.json", folder + "/TrackAuditBefore.json", true);
        var changes = new List<object>();
        foreach (var track in audit["tracks"])
        {
            var issues = track["objects"].Where(o => (bool)o["intrusion"] || (bool)o["buried"]).ToArray();
            if (issues.Length == 0) continue;
            if (issues.Any(o => (string)o["kind"] == "combinedTrees")) throw new InvalidOperationException("Combined tree meshes require individual geometry review, not whole-forest removal.");
            string path = (string)track["scene"];
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Save the user's scene edits first: " + path);
            string backup = Path.Combine(Path.GetTempPath(), "formula-polish", "scenes", Path.GetFileName(path));
            Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(path, backup, true);
            try
            {
                var roots = scene.GetRootGameObjects();
                var circuit = roots.SelectMany(r => r.GetComponentsInChildren<FormulaCircuit>(true)).Single();
                var points = circuit.route.waypoints.Select(w => w.transform.position).ToArray();
                var grounds = roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Where(c => c.enabled && !c.isTrigger &&
                    (c is TerrainCollider || c is MeshCollider) && !Regex.IsMatch(AnimationUtility.CalculateTransformPath(c.transform, null), @"(?i)(trees?|palm|pine|arrow|barrier)")).ToArray();
                // Validate all object identities before modifying this scene.
                foreach (var issue in issues)
                {
                    var t = Find(roots, (string)issue["hierarchy"]);
                    if (t == null || Vector3.Distance(t.position, Vector(issue["position"])) > .05f)
                        throw new InvalidOperationException("Stale audit entry: " + issue["hierarchy"]);
                }
                Capture(scene, points, folder + "/Track08-Before.png");
                var hidden = new HashSet<Transform>();
                foreach (var issue in issues.Where(o => (string)o["kind"] == "tree"))
                {
                    var t = Find(roots, (string)issue["hierarchy"]);
                    while (t.parent != null && !Regex.IsMatch(t.name, @"(?i)(^|[_\s])(palm|pine|tree|oak|fir)")) t = t.parent;
                    if (!hidden.Add(t)) continue;
                    Undo.RecordObject(t.gameObject, "Clear tree from Formula racing corridor");
                    t.gameObject.SetActive(false); PrefabOverride(t.gameObject);
                    changes.Add(new { scene = path, hierarchy = AnimationUtility.CalculateTransformPath(t, null), action = "disabled complete tree instance", before = V(t.position), after = V(t.position) });
                }
                foreach (var issue in issues.Where(o => (string)o["kind"] == "arrow"))
                {
                    var t = Find(roots, (string)issue["hierarchy"]); var renderer = t.GetComponent<Renderer>();
                    var before = t.position; var anchor = renderer.bounds.center; anchor.y = renderer.bounds.min.y;
                    FormulaPolishTrackAudit.Closest(points, anchor, out var nearest, out var tangent);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    float extent = Mathf.Abs(side.x) * renderer.bounds.extents.x + Mathf.Abs(side.z) * renderer.bounds.extents.z;
                    float preferred = Vector3.Dot(anchor - nearest, side) < 0 ? -1 : 1;
                    Vector3 destination = anchor;
                    if ((bool)issue["intrusion"])
                    {
                        bool found = false;
                        for (int attempt = 0; attempt < 10 && !found; attempt++)
                        {
                            float sign = attempt % 2 == 0 ? preferred : -preferred;
                            destination = nearest + side * sign * (circuit.halfWidth + extent + 1.5f + attempt / 2 * 3);
                            if (FormulaCircuit.SampleRoad(grounds, destination, out var ground)) destination.y = ground.y;
                            float clearance = FormulaPolishTrackAudit.Closest(points, destination, out _, out _);
                            found = clearance > circuit.halfWidth + extent + 1;
                        }
                        if (!found) throw new InvalidOperationException("No safe roadside position: " + issue["hierarchy"]);
                    }
                    if (FormulaCircuit.SampleRoad(grounds, destination, out var surface)) destination.y = surface.y;
                    else destination.y = nearest.y;
                    Vector3 pivot = t.position - anchor;
                    Undo.RecordObject(t, "Place Formula direction arrow beside the road");
                    t.position = destination + pivot + Vector3.up * .25f; PrefabOverride(t);
                    Physics.SyncTransforms();
                    changes.Add(new { scene = path, hierarchy = (string)issue["hierarchy"], action = "moved arrow outside driving corridor and grounded its base", before = V(before), after = V(t.position) });
                }
                Capture(scene, points, folder + "/Track08-After.png");
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        File.WriteAllText(folder + "/TrackChanges.json", JsonConvert.SerializeObject(new { changes, runtimeScanningAdded = false, sourcePrefabAssetsModified = false }, Formatting.Indented));
        FormulaPolishTrackAudit.Run();
    }
    private static void PrefabOverride(UnityEngine.Object obj) { EditorUtility.SetDirty(obj); if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
    private static Transform Find(GameObject[] roots, string path)
    {
        int slash = path.IndexOf('/'); string first = slash < 0 ? path : path.Substring(0, slash);
        var root = roots.FirstOrDefault(r => r.name == first);
        return root == null ? null : slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
    }
    private static Vector3 Vector(JToken v) => new Vector3((float)v[0], (float)v[1], (float)v[2]);
    private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    private static void Capture(Scene scene, Vector3[] route, string path)
    {
        var go = new GameObject("Polish inspection camera"); SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.AddComponent<Camera>(); var rt = new RenderTexture(960, 540, 24);
        var old = RenderTexture.active; Texture2D image = null;
        try
        {
            Vector3 focus = new Vector3(639, 22, 556);
            FormulaPolishTrackAudit.Closest(route, focus, out _, out var direction);
            go.transform.position = focus - direction * 29 + Vector3.up * 18 + Vector3.Cross(Vector3.up, direction) * 10;
            go.transform.LookAt(focus); camera.fieldOfView = 65; camera.farClipPlane = 1800; camera.targetTexture = rt;
            camera.Render(); RenderTexture.active = rt; image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = old; if (image != null) UnityEngine.Object.DestroyImmediate(image); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go); }
    }
}
