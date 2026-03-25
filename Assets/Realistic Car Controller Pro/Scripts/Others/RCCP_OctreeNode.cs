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
/// Represents a single node in the RCCP_Octree data structure.
/// Each node contains a bounding volume and either vertices (if leaf) or child nodes (if branch).
/// </summary>
[System.Serializable]
public class RCCP_OctreeNode {

    /// <summary>Reference to the mesh filter for this octree branch.</summary>
    public MeshFilter meshFilter;

    /// <summary>Axis-aligned bounding box for this node.</summary>
    public Bounds bounds;

    /// <summary>List of vertices contained in this node (only populated for leaf nodes).</summary>
    [System.NonSerialized]
    public List<Vector3> vertices;

    /// <summary>Array of 8 child nodes (null for leaf nodes).</summary>
    [System.NonSerialized]
    public RCCP_OctreeNode[] children;

    /// <summary>True if this node has no children and contains vertices directly.</summary>
    public bool IsLeaf => children == null;

    /// <summary>
    /// Creates an octree node from a mesh filter's bounds.
    /// </summary>
    /// <param name="meshFilter">The mesh filter to use for bounds.</param>
    public RCCP_OctreeNode(MeshFilter meshFilter) {

        this.meshFilter = meshFilter;
        this.bounds = meshFilter.mesh.bounds;
        this.bounds.center = meshFilter.mesh.bounds.center;

        vertices = new List<Vector3>();

    }

    /// <summary>
    /// Creates an octree node with specified bounds.
    /// </summary>
    /// <param name="bounds">The bounding volume for this node.</param>
    public RCCP_OctreeNode(Bounds bounds) {

        this.bounds = bounds;
        this.bounds.center = bounds.center;

        vertices = new List<Vector3>();

    }

}
