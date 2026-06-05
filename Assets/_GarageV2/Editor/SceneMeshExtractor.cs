using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SceneMeshExtractor
{
    [MenuItem("Tools/RacingRCCP/Extract Selected Scene Meshes")]
    private static void ExtractSelectedSceneMeshes()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Extract Scene Meshes", "Select one or more GameObjects from the scene first.", "OK");
            return;
        }

        string absoluteFolder = EditorUtility.OpenFolderPanel(
            "Choose Folder For Extracted Meshes",
            Path.Combine(Application.dataPath, "_GarageV2"),
            "ExtractedSceneMeshes");

        string targetFolder = AbsoluteToAssetPath(absoluteFolder);

        if (string.IsNullOrWhiteSpace(targetFolder))
            return;

        HashSet<Mesh> processedMeshes = new HashSet<Mesh>();
        int extractedCount = 0;

        foreach (GameObject selectedObject in selectedObjects)
        {
            MeshFilter[] meshFilters = selectedObject.GetComponentsInChildren<MeshFilter>(true);

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh sourceMesh = meshFilter.sharedMesh;

                if (sourceMesh == null || processedMeshes.Contains(sourceMesh))
                    continue;

                string originalPath = AssetDatabase.GetAssetPath(sourceMesh);

                if (!string.IsNullOrEmpty(originalPath))
                    continue;

                Mesh meshCopy = Object.Instantiate(sourceMesh);
                meshCopy.name = sourceMesh.name;

                string safeFileName = SanitizeFileName(meshCopy.name);
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetFolder, safeFileName + ".asset"));

                AssetDatabase.CreateAsset(meshCopy, assetPath);
                processedMeshes.Add(sourceMesh);
                extractedCount++;

                Mesh extractedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                RebindMeshReferences(selectedObject.scene, sourceMesh, extractedMesh);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Extract Scene Meshes",
            extractedCount > 0
                ? $"Extracted {extractedCount} mesh asset(s) and rebound scene references."
                : "No embedded scene meshes were found in the current selection.",
            "OK");
    }

    [MenuItem("Tools/RacingRCCP/Extract Selected Scene Meshes", true)]
    private static bool ValidateExtractSelectedSceneMeshes()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    private static void RebindMeshReferences(UnityEngine.SceneManagement.Scene scene, Mesh sourceMesh, Mesh extractedMesh)
    {
        if (!scene.IsValid())
            return;

        GameObject[] rootObjects = scene.GetRootGameObjects();

        foreach (GameObject rootObject in rootObjects)
        {
            MeshFilter[] meshFilters = rootObject.GetComponentsInChildren<MeshFilter>(true);

            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != sourceMesh)
                    continue;

                Undo.RecordObject(meshFilter, "Rebind Extracted Mesh");
                meshFilter.sharedMesh = extractedMesh;
                EditorUtility.SetDirty(meshFilter);

                MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();

                if (meshCollider != null && meshCollider.sharedMesh == sourceMesh)
                {
                    Undo.RecordObject(meshCollider, "Rebind Extracted Mesh Collider");
                    meshCollider.sharedMesh = extractedMesh;
                    EditorUtility.SetDirty(meshCollider);
                }
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalidChar, '_');

        return string.IsNullOrWhiteSpace(fileName) ? "SceneMesh" : fileName;
    }

    private static string AbsoluteToAssetPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        string normalizedAbsolutePath = absolutePath.Replace('\\', '/');
        string normalizedAssetsPath = Application.dataPath.Replace('\\', '/');

        if (!normalizedAbsolutePath.StartsWith(normalizedAssetsPath))
        {
            EditorUtility.DisplayDialog(
                "Invalid Folder",
                "Choose a folder inside this project's Assets directory.",
                "OK");
            return null;
        }

        return "Assets" + normalizedAbsolutePath.Substring(normalizedAssetsPath.Length);
    }
}
