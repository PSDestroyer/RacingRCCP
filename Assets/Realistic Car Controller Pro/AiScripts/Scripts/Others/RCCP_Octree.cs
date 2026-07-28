//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Octree data structure for efficient spatial partitioning of mesh vertices.
/// Used by the damage system to find nearest vertices for deformation.
/// </summary>
[System.Serializable]
public class RCCP_Octree {

    /// <summary>Root node of the octree containing all vertices.</summary>
    public RCCP_OctreeNode root;

    private readonly int maxDepth = 20;
    private readonly int maxVerticesPerNode = 5000;

    /// <summary>
    /// Creates a new octree from a mesh filter's mesh data.
    /// </summary>
    /// <param name="meshFilter">The mesh filter containing the mesh to partition.</param>
    public RCCP_Octree(MeshFilter meshFilter) {

        root = new RCCP_OctreeNode(meshFilter);

    }

    /// <summary>
    /// Inserts a vertex into the octree.
    /// </summary>
    /// <param name="vertex">The world-space vertex position to insert.</param>
    public void Insert(Vector3 vertex) {

        Insert(root, vertex, 0);

    }

    private void Insert(RCCP_OctreeNode node, Vector3 vertex, int depth) {

        if (node.IsLeaf) {

            node.vertices.Add(vertex);

            if (node.vertices.Count > maxVerticesPerNode && depth < maxDepth) {

                Subdivide(node);
                List<Vector3> verticesToReinsert = new List<Vector3>(node.vertices);
                node.vertices.Clear();

                foreach (Vector3 v in verticesToReinsert)
                    Insert(node, v, depth);

            }

        } else {

            // Insert into the appropriate child node

            foreach (var child in node.children) {

                if (child.bounds.Contains(vertex)) {

                    Insert(child, vertex, depth + 1);
                    break;

                }

            }

        }

    }

    private void Subdivide(RCCP_OctreeNode node) {

        node.children = new RCCP_OctreeNode[8];
        Vector3 size = node.bounds.size / 2f;
        Vector3 center = node.bounds.center;

        // Create 8 children nodes (split the current bounds into 8 smaller bounds)
        for (int i = 0; i < 8; i++) {

            Vector3 newCenter = center + new Vector3(size.x * ((i & 1) == 0 ? -0.5f : 0.5f),
                                                     size.y * ((i & 2) == 0 ? -0.5f : 0.5f),
                                                     size.z * ((i & 4) == 0 ? -0.5f : 0.5f));
            node.children[i] = new RCCP_OctreeNode(new Bounds(newCenter, size));

        }

    }

    /// <summary>
    /// Finds the nearest vertex in the octree to the given point.
    /// </summary>
    /// <param name="point">The world-space point to search from.</param>
    /// <param name="meshFilter">The mesh filter containing the original mesh.</param>
    /// <returns>The nearest vertex position.</returns>
    public Vector3 FindNearestVertex(Vector3 point, MeshFilter meshFilter) {

        return FindNearestVertex(root, point, meshFilter);

    }

    private Vector3 FindNearestVertex(RCCP_OctreeNode node, Vector3 point, MeshFilter meshFilter) {

        float minDistSqr = Mathf.Infinity;
        Vector3 bestVertex = Vector3.zero;

        if (node.IsLeaf) {

            foreach (var vertex in node.vertices) {

                float distSqr = (vertex - point).sqrMagnitude;

                if (distSqr < minDistSqr) {

                    minDistSqr = distSqr;
                    bestVertex = vertex;

                }

            }

        } else {

            foreach (var child in node.children) {

                if (child != null || child.bounds.SqrDistance(point) < minDistSqr) {

                    foreach (var vertex in child.vertices) {

                        float distSqr = (vertex - point).sqrMagnitude;

                        if (distSqr < minDistSqr) {

                            minDistSqr = distSqr;
                            bestVertex = vertex;

                        }

                    }

                }

            }

        }

        return bestVertex;

    }

}

