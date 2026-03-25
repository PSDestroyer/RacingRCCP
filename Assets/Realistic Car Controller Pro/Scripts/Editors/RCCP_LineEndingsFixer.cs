//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Automatically detects and fixes inconsistent line endings in RCCP scripts.
/// Runs once per Unity session on domain reload.
/// </summary>
[InitializeOnLoad]
public static class RCCP_LineEndingsFixer {

    private const string SESSION_KEY = "RCCP_LineEndingsFixer_Checked";

    static RCCP_LineEndingsFixer() {

        // Only run once per session
        if (SessionState.GetBool(SESSION_KEY, false))
            return;

        SessionState.SetBool(SESSION_KEY, true);

        // Delay to ensure Unity is fully loaded
        EditorApplication.delayCall += CheckAndFixLineEndings;

    }

    private static void CheckAndFixLineEndings() {

        string rootPath = GetRCCPRootPath();

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) {

            Debug.LogWarning("[RCCP Line Endings Fixer] Could not find RCCP folder.");
            return;

        }

        string[] csFiles = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);
        int fixedCount = 0;

        foreach (string filePath in csFiles) {

            if (TryFixLineEndings(filePath))
                fixedCount++;

        }

        if (fixedCount > 0)
            Debug.Log($"[RCCP Line Endings Fixer] Fixed line endings in {fixedCount} file(s).");

    }

    /// <summary>
    /// Gets the RCCP root folder path by finding the RCCP_Settings asset.
    /// </summary>
    private static string GetRCCPRootPath() {

        string[] guids = AssetDatabase.FindAssets("t:RCCP_Settings");

        if (guids.Length == 0)
            return null;

        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);

        // RCCP_Settings is typically at: [RCCP Root]/Resources/RCCP_Settings.asset
        // Navigate up to find the root folder
        string directory = Path.GetDirectoryName(assetPath);

        if (directory.EndsWith("Resources")) {

            string parentDir = Path.GetDirectoryName(directory);
            return Path.GetFullPath(parentDir);

        }

        // Fallback: return the directory containing the settings
        return Path.GetFullPath(directory);

    }

    /// <summary>
    /// Checks if a file has mixed line endings and fixes them.
    /// Returns true if the file was modified.
    /// </summary>
    private static bool TryFixLineEndings(string filePath) {

        try {

            string content = File.ReadAllText(filePath);

            // Check for mixed line endings: has both CRLF (\r\n) and standalone LF (\n)
            bool hasCRLF = content.Contains("\r\n");
            bool hasStandaloneLF = Regex.IsMatch(content, @"(?<!\r)\n");

            if (hasCRLF && hasStandaloneLF) {

                // Normalize to LF: first convert CRLF to LF, then ensure no CR remains
                string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");

                File.WriteAllText(filePath, normalized);

                string relativePath = filePath.Replace("\\", "/");
                int assetsIndex = relativePath.IndexOf("Assets/");

                if (assetsIndex >= 0)
                    relativePath = relativePath.Substring(assetsIndex);

                Debug.Log($"[RCCP Line Endings Fixer] Fixed: {relativePath}");

                return true;

            }

        } catch (System.Exception ex) {

            Debug.LogWarning($"[RCCP Line Endings Fixer] Error processing {filePath}: {ex.Message}");

        }

        return false;

    }

}
#endif
