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

namespace BoneCrackerGames.RCCP.AIAssistant {

    /// <summary>
    /// Detects and fixes inconsistent line endings in RCCP AI Assistant scripts.
    /// Now opt-in via menu item instead of automatic on domain reload.
    /// </summary>
    public static class RCCP_AILineEndingsFixer {

        private const string MENU_PATH = "Tools/BoneCracker Games/RCCP AI Assistant/Fix Line Endings";

        [MenuItem(MENU_PATH, priority = 200)]
        private static void FixLineEndingsMenuItem() {

            string rootPath = RCCP_AIUtility.RootPath;

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) {

                EditorUtility.DisplayDialog(
                    "Line Endings Fixer",
                    "Could not find RCCP AI Assistant folder.",
                    "OK");
                return;

            }

            string[] csFiles = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);

            // Count files that need fixing
            int needsFixCount = 0;
            foreach (string filePath in csFiles) {
                if (NeedsLineEndingFix(filePath)) {
                    needsFixCount++;
                }
            }

            if (needsFixCount == 0) {
                EditorUtility.DisplayDialog(
                    "Line Endings Fixer",
                    "No files need line ending fixes. All files have consistent line endings.",
                    "OK");
                return;
            }

            // Show confirmation dialog
            bool proceed = EditorUtility.DisplayDialog(
                "Fix Line Endings",
                $"Found {needsFixCount} file(s) with mixed line endings in:\n{rootPath}\n\nNormalize line endings to LF (Unix-style)?",
                "Fix Files",
                "Cancel");

            if (!proceed) return;

            // Perform the fix
            int fixedCount = 0;
            foreach (string filePath in csFiles) {

                if (TryFixLineEndings(filePath))
                    fixedCount++;

            }

            EditorUtility.DisplayDialog(
                "Line Endings Fixer",
                $"Fixed line endings in {fixedCount} file(s).",
                "OK");

            if (fixedCount > 0) {
                AssetDatabase.Refresh();
            }

        }

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateFixLineEndingsMenuItem() {
            // Disable menu item during compilation
            return !EditorApplication.isCompiling;
        }

        /// <summary>
        /// Checks if a file has mixed line endings that need fixing.
        /// </summary>
        private static bool NeedsLineEndingFix(string filePath) {

            try {

                string content = File.ReadAllText(filePath);

                // Check for mixed line endings: has both CRLF (\r\n) and standalone LF (\n)
                bool hasCRLF = content.Contains("\r\n");
                bool hasStandaloneLF = Regex.IsMatch(content, @"(?<!\r)\n");

                return hasCRLF && hasStandaloneLF;

            } catch {
                return false;
            }

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

                    Debug.Log($"[RCCP AI Line Endings Fixer] Fixed: {relativePath}");

                    return true;

                }

            } catch (System.Exception ex) {

                Debug.LogWarning($"[RCCP AI Line Endings Fixer] Error processing {filePath}: {ex.Message}");

            }

            return false;

        }

    }

}
#endif
