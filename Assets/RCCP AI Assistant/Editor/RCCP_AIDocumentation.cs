//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Opens the RCCP AI Assistant documentation viewer.
/// Uses the RDV (Realistic Documentation Viewer) system.
/// </summary>
public static class RCCP_AIDocumentation {

    /// <summary>
    /// Path to RCCP AI Assistant documentation relative to Assets folder.
    /// </summary>
    private const string DOCS_PATH = "RCCP AI Assistant/Documentation";

    /// <summary>
    /// Opens the RCCP AI Assistant documentation viewer.
    /// </summary>
    [MenuItem("Tools/BoneCracker Games/RCCP AI Assistant/Documentation", false, 50)]
    public static void OpenDocumentation() {
        // Try to use RDV if available
        var rdvType = System.Type.GetType("BoneCrackerGames.RDV.RDV_DocumentationViewer, Assembly-CSharp-Editor");

        if (rdvType != null) {
            // RDV is available - use it
            var showWindowMethod = rdvType.GetMethod("ShowWindow", new[] { typeof(string) });
            if (showWindowMethod != null) {
                showWindowMethod.Invoke(null, new object[] { DOCS_PATH });
                return;
            }
        }

        // Fallback: Open documentation folder in file browser
        string fullPath = System.IO.Path.Combine(Application.dataPath, DOCS_PATH);
        if (System.IO.Directory.Exists(fullPath)) {
            string readmePath = System.IO.Path.Combine(fullPath, "README.md");
            if (System.IO.File.Exists(readmePath)) {
                // Try to open README in default markdown viewer
                EditorUtility.RevealInFinder(readmePath);
            } else {
                EditorUtility.RevealInFinder(fullPath);
            }
        } else {
            Debug.LogWarning("[RCCP AI] Documentation folder not found at: " + fullPath);
            EditorUtility.DisplayDialog("Documentation Not Found",
                "The documentation folder was not found.\n\nExpected location:\n" + fullPath,
                "OK");
        }
    }

    /// <summary>
    /// Opens the documentation viewer and navigates to a specific file.
    /// </summary>
    /// <param name="relativePath">Path relative to Documentation folder (e.g., "01_Getting_Started/Quick_Start.md")</param>
    public static void OpenDocumentationPage(string relativePath) {
        // For now, just open the main documentation
        // The RDV system will handle navigation internally
        OpenDocumentation();
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
