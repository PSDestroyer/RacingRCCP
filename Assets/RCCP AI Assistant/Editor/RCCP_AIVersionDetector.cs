//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Detects RCCP version at compile time by checking for V2.2-only files.
/// Sets/removes the RCCP_V2_2_OR_NEWER scripting define symbol automatically.
///
/// IMPORTANT: This class must have ZERO RCCP type references to always compile,
/// regardless of which RCCP version is installed.
/// </summary>
[InitializeOnLoad]
public static class RCCP_AIVersionDetector {

    /// <summary>
    /// The scripting define symbol set when RCCP V2.2+ is detected
    /// </summary>
    public const string RCCP_V2_2_SYMBOL = "RCCP_V2_2_OR_NEWER";

    /// <summary>
    /// EditorPrefs key for tracking if the V2.0 warning dialog has been shown
    /// </summary>
    private const string V2_0_WARNING_SHOWN_KEY = "RCCP_AI_V20WarningShown";

    /// <summary>
    /// Files that only exist in RCCP V2.2+
    /// Checking for these files determines version without type references
    /// </summary>
    private static readonly string[] V2_2_Indicator_Files = {
        "RCCP_VehicleValidator.cs"
    };

    /// <summary>
    /// Additional files that indicate V2.2+ (alternative checks)
    /// </summary>
    private static readonly string[] V2_2_Indicator_Files_Alt = {
        // Add alternative indicator files here if needed
    };

    static RCCP_AIVersionDetector() {
        // Run detection on domain reload
        EditorApplication.delayCall += DetectAndUpdateSymbol;
    }

    /// <summary>
    /// Detects RCCP version and updates scripting define symbols accordingly.
    /// </summary>
    public static void DetectAndUpdateSymbol() {
        bool isV2_2OrNewer = DetectRCCPVersion();
        UpdateScriptingDefineSymbol(isV2_2OrNewer);
    }

    /// <summary>
    /// Checks if RCCP V2.2+ is installed by looking for version-specific files.
    /// </summary>
    /// <returns>True if V2.2+ features are detected</returns>
    public static bool DetectRCCPVersion() {
        // Find all potential RCCP folders
        string[] rccpFolders = FindRCCPFolders();

        if (rccpFolders.Length == 0) {
            // RCCP not found - assume V2.0 (safer default)
            return false;
        }

        // Check for V2.2 indicator files
        foreach (string folder in rccpFolders) {
            foreach (string indicatorFile in V2_2_Indicator_Files) {
                string[] foundFiles = Directory.GetFiles(folder, indicatorFile, SearchOption.AllDirectories);
                if (foundFiles.Length > 0) {
                    return true;
                }
            }

            // Also check alternative indicators
            foreach (string indicatorFile in V2_2_Indicator_Files_Alt) {
                string[] foundFiles = Directory.GetFiles(folder, indicatorFile, SearchOption.AllDirectories);
                if (foundFiles.Length > 0) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Finds all potential RCCP installation folders.
    /// </summary>
    private static string[] FindRCCPFolders() {
        var folders = new List<string>();
        string assetsPath = Application.dataPath;

        // Look for common RCCP folder names
        string[] searchPatterns = {
            "Realistic Car Controller Pro",
            "RCCP",
            "RealisticCarControllerPro"
        };

        foreach (string pattern in searchPatterns) {
            try {
                string[] found = Directory.GetDirectories(assetsPath, pattern, SearchOption.AllDirectories);
                folders.AddRange(found);
            } catch (Exception) {
                // Ignore directory access errors
            }
        }

        // Also check if there's an RCCP_Settings.cs anywhere (definitive RCCP indicator)
        try {
            string[] settingsFiles = Directory.GetFiles(assetsPath, "RCCP_Settings.cs", SearchOption.AllDirectories);
            foreach (string file in settingsFiles) {
                string dir = Path.GetDirectoryName(file);
                if (dir != null && !folders.Contains(dir)) {
                    // Go up one level to get the RCCP root
                    string parent = Directory.GetParent(dir)?.FullName;
                    if (parent != null && !folders.Contains(parent)) {
                        folders.Add(parent);
                    }
                }
            }
        } catch (Exception) {
            // Ignore
        }

        return folders.Distinct().ToArray();
    }

    /// <summary>
    /// Updates the scripting define symbols based on detected version.
    /// </summary>
    /// <param name="isV2_2OrNewer">Whether V2.2+ was detected</param>
    private static void UpdateScriptingDefineSymbol(bool isV2_2OrNewer) {
        // Get current build target group
        BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

        if (targetGroup == BuildTargetGroup.Unknown) {
            targetGroup = BuildTargetGroup.Standalone;
        }

        // Convert to NamedBuildTarget (new API)
        NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);

        // Get current symbols
        string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        List<string> symbols = currentSymbols.Split(';')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        bool hasSymbol = symbols.Contains(RCCP_V2_2_SYMBOL);
        bool changed = false;

        if (isV2_2OrNewer && !hasSymbol) {
            // Add symbol
            symbols.Add(RCCP_V2_2_SYMBOL);
            changed = true;
            Debug.Log($"[RCCP AI] RCCP V2.2+ detected - enabling full functionality ({RCCP_V2_2_SYMBOL} added)");

            // Clear the warning shown flag when upgrading to V2.2+
            EditorPrefs.DeleteKey(V2_0_WARNING_SHOWN_KEY);
        } else if (!isV2_2OrNewer && hasSymbol) {
            // Remove symbol
            symbols.Remove(RCCP_V2_2_SYMBOL);
            changed = true;
            Debug.Log($"[RCCP AI] RCCP V2.0 detected - using compatibility mode ({RCCP_V2_2_SYMBOL} removed)");

            // Show one-time warning dialog
            ShowV2_0WarningDialog();
        } else if (!isV2_2OrNewer && !hasSymbol) {
            // V2.0 detected but symbol wasn't set (first detection)
            // Show one-time warning dialog
            ShowV2_0WarningDialog();
        }

        if (changed) {
            string newSymbols = string.Join(";", symbols);
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, newSymbols);
        }
    }

    /// <summary>
    /// Shows a one-time warning dialog when RCCP V2.0 is detected.
    /// </summary>
    private static void ShowV2_0WarningDialog() {
        // Only show once per installation
        if (EditorPrefs.GetBool(V2_0_WARNING_SHOWN_KEY, false)) {
            return;
        }

        // Mark as shown
        EditorPrefs.SetBool(V2_0_WARNING_SHOWN_KEY, true);

        // Schedule dialog to avoid showing during domain reload
        EditorApplication.delayCall += () => {
            bool openAssetStore = EditorUtility.DisplayDialog(
                "RCCP AI Assistant - Version Notice",
                "RCCP V2.0 detected.\n\n" +
                "The AI Assistant is running in compatibility mode with limited features:\n\n" +
                "- Wheel grip property not available\n" +
                "- Diagnostics use simplified checks\n" +
                "- Some AI features may be restricted\n\n" +
                "For full functionality, please upgrade to RCCP V2.2 or newer.\n\n" +
                "The AI Assistant will continue to work, but some features will be unavailable.",
                "Import RCCP V2.2",
                "OK"
            );

            if (openAssetStore) {
                Application.OpenURL("https://u3d.as/22Bf");
            }
        };
    }

    /// <summary>
    /// Gets the currently detected RCCP version status.
    /// </summary>
    public static string GetVersionStatus() {
#if RCCP_V2_2_OR_NEWER
        return "RCCP V2.2+ (Full Features)";
#else
        return "RCCP V2.0 (Compatibility Mode)";
#endif
    }

    /// <summary>
    /// Checks if full V2.2 features are available.
    /// </summary>
    public static bool IsV2_2OrNewer {
        get {
#if RCCP_V2_2_OR_NEWER
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Gets a short version status string for UI display.
    /// </summary>
    public static string GetShortVersionStatus() {
#if RCCP_V2_2_OR_NEWER
        return "V2.2+";
#else
        return "V2.0";
#endif
    }

    /// <summary>
    /// Gets a version status badge with icon for UI display.
    /// Returns (icon, text, isWarning)
    /// </summary>
    public static (string icon, string text, bool isWarning) GetVersionBadge() {
#if RCCP_V2_2_OR_NEWER
        return ("✓", "V2.2+", false);
#else
        return ("⚠", "V2.0", true);
#endif
    }

    /// <summary>
    /// Resets the V2.0 warning dialog shown flag (for testing/developer mode).
    /// </summary>
    public static void ResetWarningDialogFlag() {
        EditorPrefs.DeleteKey(V2_0_WARNING_SHOWN_KEY);
        Debug.Log("[RCCP AI] V2.0 warning dialog flag reset - dialog will show again on next detection");
    }
}

/// <summary>
/// Asset postprocessor to detect RCCP version changes when assets are imported.
/// </summary>
public class RCCP_AIVersionPostprocessor : AssetPostprocessor {

    /// <summary>
    /// Called when assets are imported, deleted, or moved.
    /// Triggers version re-detection if RCCP-related files changed.
    /// </summary>
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths) {

        // Check if any RCCP-related files changed
        bool rccpChanged = false;

        foreach (string asset in importedAssets.Concat(deletedAssets).Concat(movedAssets)) {
            if (asset.Contains("RCCP") || asset.Contains("Realistic Car Controller")) {
                rccpChanged = true;
                break;
            }
        }

        if (rccpChanged) {
            // Re-run version detection
            EditorApplication.delayCall += RCCP_AIVersionDetector.DetectAndUpdateSymbol;
        }
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
