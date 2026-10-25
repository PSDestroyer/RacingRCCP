using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Collections;

public static class FormulaCircuitInventory
{
    [MenuItem("Tools/Formula Career/Inspect Circuit Sources")]
    public static void Inspect()
    {
        var previous = SceneManager.GetActiveScene();
        var paths = Enumerable.Range(1, 8).Select(i => "Assets/Racing_Visuals/Scenes/Race_Track_" + i + "_Visual.unity")
            .Concat(new[] { "Assets/RTG 1.3/Scenes/Scene-01.unity" });
        var result = paths.Select(path =>
        {
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                return new
                {
                    path, roots = roots.Select(r => r.name).ToArray(),
                    largestMeshes = roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).Where(m => m.sharedMesh != null).OrderByDescending(m => m.sharedMesh.vertexCount).Take(15)
                        .Select(m => new { m.name, vertices = m.sharedMesh.vertexCount, mesh = AssetDatabase.GetAssetPath(m.sharedMesh), bounds = m.sharedMesh.bounds.ToString() }).ToArray(),
                    meshCatalog = roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).Where(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0)
                        .GroupBy(m => m.sharedMesh).Select(g => g.First()).Select(m => new { m.name, meshName = m.sharedMesh.name, vertices = m.sharedMesh.vertexCount,
                            bounds = m.sharedMesh.bounds.ToString(), uvRows = Rows(m.sharedMesh), materials = m.GetComponent<Renderer>() != null ? m.GetComponent<Renderer>().sharedMaterials.Where(v => v != null).Select(v => v.name).ToArray() : null }).ToArray(),
                    roads = roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                        .Where(m => m.name.ToLowerInvariant().Contains("road") || m.name.ToLowerInvariant().Contains("asphalt"))
                        .Select(m => new { m.name, mesh = m.sharedMesh == null ? "" : AssetDatabase.GetAssetPath(m.sharedMesh),
                            vertices = m.sharedMesh == null ? 0 : m.sharedMesh.vertexCount,
                            uvRows = m.sharedMesh == null || !m.sharedMesh.isReadable ? null : m.sharedMesh.uv.GroupBy(v => Mathf.RoundToInt(v.y * 1000)).GroupBy(g => g.Count()).Select(g => new { count = g.Key, rows = g.Count() }).ToArray(),
                            scale = m.transform.lossyScale.ToString(), materials = m.GetComponent<Renderer>() != null ? m.GetComponent<Renderer>().sharedMaterials.Select(v => v == null ? "MISSING" : v.name + " / " + v.shader.name).ToArray() : null
                        }).ToArray(),
                    routes = roots.SelectMany(r => r.GetComponentsInChildren<WaypointCircuit>(true)).Select(c => new { c.name, count = c.transform.childCount }).ToArray()
                };
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }).ToArray();
        SceneManager.SetActiveScene(previous);
        Directory.CreateDirectory("Docs/AI/FormulaCircuits");
        File.WriteAllText("Docs/AI/FormulaCircuits/Sources.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        Debug.Log("FORMULA_CIRCUIT_SOURCES_INSPECTED");
    }
    private static object Rows(Mesh mesh)
    {
        if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)) return null;
        using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
        using (var uv = new NativeArray<Vector2>(mesh.vertexCount, Allocator.Temp))
        {
            data[0].GetUVs(0, uv);
            return uv.ToArray().GroupBy(v => Mathf.RoundToInt(v.y * 1000)).GroupBy(g => g.Count()).Select(g => new { count = g.Key, rows = g.Count() }).ToArray();
        }
    }
}
