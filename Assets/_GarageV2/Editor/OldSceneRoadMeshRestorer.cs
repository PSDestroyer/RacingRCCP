using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OldSceneRoadMeshRestorer
{
    private const string OldScenesRoot = "Assets/_GarageV2/Resources/OldScenes";
    private static readonly string[] RootNamesToSync = { "Road_Network" };

    [MenuItem("Tools/RacingRCCP/Restore Active Scene Road Meshes From Old Scene")]
    private static void RestoreActiveSceneRoadMeshes()
    {
        Scene targetScene = SceneManager.GetActiveScene();

        if (!targetScene.IsValid() || string.IsNullOrWhiteSpace(targetScene.path))
        {
            EditorUtility.DisplayDialog(
                "Restore Road Meshes",
                "Open the target scene first.",
                "OK");
            return;
        }

        string sourceScenePath = Path.Combine(OldScenesRoot, targetScene.name + ".unity").Replace("\\", "/");

        if (!File.Exists(sourceScenePath))
        {
            EditorUtility.DisplayDialog(
                "Restore Road Meshes",
                $"No matching old scene was found at:\n{sourceScenePath}",
                "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene sourceScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Additive);
        var embeddedMeshMap = new Dictionary<Mesh, Mesh>();

        try
        {
            int syncedRoots = 0;

            foreach (string rootName in RootNamesToSync)
            {
                Transform sourceRoot = FindRootTransform(sourceScene, rootName);

                if (sourceRoot == null)
                    continue;

                Transform targetRoot = FindRootTransform(targetScene, rootName);

                if (targetRoot == null)
                {
                    GameObject rootClone = Object.Instantiate(sourceRoot.gameObject);
                    rootClone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(rootClone, targetScene);
                    targetRoot = rootClone.transform;
                }

                SyncHierarchy(sourceRoot, targetRoot, embeddedMeshMap);
                syncedRoots++;
            }

            if (syncedRoots == 0)
            {
                EditorUtility.DisplayDialog(
                    "Restore Road Meshes",
                    "No supported road root was found in the old scene.",
                    "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);

            EditorUtility.DisplayDialog(
                "Restore Road Meshes",
                $"Road meshes restored from:\n{sourceScenePath}",
                "OK");
        }
        finally
        {
            EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    [MenuItem("Tools/RacingRCCP/Restore Active Scene Road Meshes From Old Scene", true)]
    private static bool ValidateRestoreActiveSceneRoadMeshes()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.IsValid() && !string.IsNullOrWhiteSpace(scene.path);
    }

    private static void SyncHierarchy(Transform source, Transform target, Dictionary<Mesh, Mesh> embeddedMeshMap)
    {
        Undo.RegisterFullObjectHierarchyUndo(target.gameObject, "Restore Road Meshes");

        CopyGameObjectSettings(source.gameObject, target.gameObject);
        CopyTransform(source, target);
        CopyMeshComponents(source.gameObject, target.gameObject, embeddedMeshMap);

        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            Transform targetChild = GetMatchingChild(target, sourceChild, i);

            if (targetChild == null)
            {
                GameObject childClone = Object.Instantiate(sourceChild.gameObject, target);
                childClone.name = sourceChild.name;
                targetChild = childClone.transform;
            }

            SyncHierarchy(sourceChild, targetChild, embeddedMeshMap);
        }
    }

    private static Transform GetMatchingChild(Transform targetParent, Transform sourceChild, int siblingIndex)
    {
        if (siblingIndex < targetParent.childCount)
        {
            Transform indexedChild = targetParent.GetChild(siblingIndex);

            if (indexedChild.name == sourceChild.name)
                return indexedChild;
        }

        for (int i = 0; i < targetParent.childCount; i++)
        {
            Transform candidate = targetParent.GetChild(i);

            if (candidate.name == sourceChild.name)
                return candidate;
        }

        return null;
    }

    private static void CopyGameObjectSettings(GameObject source, GameObject target)
    {
        target.name = source.name;
        target.tag = source.tag;
        target.layer = source.layer;
        target.isStatic = source.isStatic;
        target.SetActive(source.activeSelf);
        EditorUtility.SetDirty(target);
    }

    private static void CopyTransform(Transform source, Transform target)
    {
        Undo.RecordObject(target, "Restore Road Transform");
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        EditorUtility.SetDirty(target);
    }

    private static void CopyMeshComponents(GameObject source, GameObject target, Dictionary<Mesh, Mesh> embeddedMeshMap)
    {
        MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();

        if (sourceMeshFilter != null)
        {
            MeshFilter targetMeshFilter = target.GetComponent<MeshFilter>();

            if (targetMeshFilter == null)
                targetMeshFilter = Undo.AddComponent<MeshFilter>(target);

            Undo.RecordObject(targetMeshFilter, "Restore Road Mesh Filter");
            targetMeshFilter.sharedMesh = GetTargetMeshReference(sourceMeshFilter.sharedMesh, embeddedMeshMap);
            EditorUtility.SetDirty(targetMeshFilter);
        }

        MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();

        if (sourceMeshRenderer != null)
        {
            MeshRenderer targetMeshRenderer = target.GetComponent<MeshRenderer>();

            if (targetMeshRenderer == null)
                targetMeshRenderer = Undo.AddComponent<MeshRenderer>(target);

            Undo.RecordObject(targetMeshRenderer, "Restore Road Mesh Renderer");
            EditorUtility.CopySerialized(sourceMeshRenderer, targetMeshRenderer);
            EditorUtility.SetDirty(targetMeshRenderer);
        }

        MeshCollider sourceMeshCollider = source.GetComponent<MeshCollider>();

        if (sourceMeshCollider != null)
        {
            MeshCollider targetMeshCollider = target.GetComponent<MeshCollider>();

            if (targetMeshCollider == null)
                targetMeshCollider = Undo.AddComponent<MeshCollider>(target);

            Undo.RecordObject(targetMeshCollider, "Restore Road Mesh Collider");
            EditorUtility.CopySerialized(sourceMeshCollider, targetMeshCollider);
            targetMeshCollider.sharedMesh = GetTargetMeshReference(sourceMeshCollider.sharedMesh, embeddedMeshMap);
            EditorUtility.SetDirty(targetMeshCollider);
        }
    }

    private static Mesh GetTargetMeshReference(Mesh sourceMesh, Dictionary<Mesh, Mesh> embeddedMeshMap)
    {
        if (sourceMesh == null)
            return null;

        string assetPath = AssetDatabase.GetAssetPath(sourceMesh);

        if (!string.IsNullOrEmpty(assetPath))
            return sourceMesh;

        if (embeddedMeshMap.TryGetValue(sourceMesh, out Mesh clonedMesh))
            return clonedMesh;

        Mesh meshClone = Object.Instantiate(sourceMesh);
        meshClone.name = sourceMesh.name;
        embeddedMeshMap[sourceMesh] = meshClone;
        return meshClone;
    }

    private static Transform FindRootTransform(Scene scene, string rootName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == rootName)
                return rootObject.transform;
        }

        return null;
    }
}
