using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

// Imported RTG vegetation combines an entire module's trees into one mesh.
// Inspect connected islands, then join leaf/trunk islands around the same tree.
public static class FormulaPolishTreeMesh
{
    public sealed class Tree
    {
        public Bounds bounds;
        public readonly List<int> vertices = new List<int>();
    }
    public static List<Tree> Inspect(Mesh mesh, Transform transform, out int[][] submeshes)
    {
        using (var meshes = MeshUtility.AcquireReadOnlyMeshData(mesh))
        using (var positions = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
        {
            var data = meshes[0]; data.GetVertices(positions);
            var parents = new int[positions.Length];
            for (int i = 0; i < parents.Length; i++) parents[i] = i;
            var welded = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i]; var key = new Vector3Int(Mathf.RoundToInt(p.x * 1000), Mathf.RoundToInt(p.y * 1000), Mathf.RoundToInt(p.z * 1000));
                if (welded.TryGetValue(key, out int other)) Join(parents, i, other); else welded.Add(key, i);
            }
            submeshes = new int[data.subMeshCount][];
            for (int sub = 0; sub < data.subMeshCount; sub++)
            {
                var desc = data.GetSubMesh(sub);
                using (var indices = new NativeArray<int>(desc.indexCount, Allocator.Temp))
                {
                    data.GetIndices(indices, sub); submeshes[sub] = indices.ToArray();
                    for (int i = 0; i + 2 < indices.Length; i += 3) { Join(parents, indices[i], indices[i + 1]); Join(parents, indices[i], indices[i + 2]); }
                }
            }
            var islands = new Dictionary<int, Tree>();
            for (int i = 0; i < positions.Length; i++)
            {
                int root = Find(parents, i); Vector3 world = transform.TransformPoint(positions[i]);
                if (!islands.TryGetValue(root, out var tree)) { tree = new Tree { bounds = new Bounds(world, Vector3.zero) }; islands.Add(root, tree); }
                tree.bounds.Encapsulate(world); tree.vertices.Add(i);
            }
            var parts = new List<Tree>(islands.Values); var groups = new int[parts.Count];
            for (int i = 0; i < groups.Length; i++) groups[i] = i;
            for (int a = 0; a < parts.Count; a++)
                for (int b = 0; b < a; b++)
                {
                    Vector3 d = parts[a].bounds.center - parts[b].bounds.center; d.y = 0;
                    if (d.sqrMagnitude < 4) Join(groups, a, b);
                }
            var trees = new Dictionary<int, Tree>();
            for (int i = 0; i < parts.Count; i++)
            {
                int root = Find(groups, i);
                if (!trees.TryGetValue(root, out var tree)) { tree = new Tree { bounds = parts[i].bounds }; trees.Add(root, tree); }
                tree.bounds.Encapsulate(parts[i].bounds); tree.vertices.AddRange(parts[i].vertices);
            }
            return new List<Tree>(trees.Values);
        }
    }
    private static int Find(int[] p, int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }
    private static void Join(int[] p, int a, int b) { int ra = Find(p, a), rb = Find(p, b); if (ra != rb) p[ra] = rb; }
}
