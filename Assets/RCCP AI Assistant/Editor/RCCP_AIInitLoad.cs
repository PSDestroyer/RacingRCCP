//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BoneCrackerGames.RCCP.AIAssistant {

public class RCCP_AIInitLoad {

    // Use dynamic paths from RCCP_AIUtility
    private static string RESOURCES_PATH => RCCP_AIUtility.ResourcesPath;
    private static string PROMPTS_PATH => RCCP_AIUtility.PromptsPath;
    private static string SETTINGS_ASSET_PATH => RCCP_AIUtility.SettingsAssetPath;

    [InitializeOnLoadMethod]
    public static void InitOnLoad() {

        EditorApplication.delayCall += EditorDelayedUpdate;

    }

    public static void EditorDelayedUpdate() {

        bool hasKey = false;

#if BCG_RCCP_AI
        hasKey = true;
#endif

        if (!hasKey) {

            // First time installation - set the scripting symbol
            RCCP_AISetScriptingSymbol.SetEnabled("BCG_RCCP_AI", true);

            // Create default assets if they don't exist
            CreateDefaultAssets();

            // Show welcome dialog
            EditorUtility.DisplayDialog(
                "RCCP AI Setup Assistant | Installed",
                "RCCP AI Setup Assistant has been installed successfully!\n\n" +
                "Access it from: Tools > BoneCracker Games > RCCP AI Assistant > Open Assistant\n" +
                "Shortcut: Ctrl+Shift+W\n\n" +
                "The free tier is ready to use. For unlimited requests, you can configure your own Claude API key in the settings.",
                "Got it!"
            );

            // Optionally open the window
            EditorApplication.delayCall += () => {
                RCCP_AIAssistantWindow.ShowWindow();
            };

        }

    }

    /// <summary>
    /// Creates all default assets if they don't exist.
    /// Can be called from anywhere to ensure assets are present.
    /// </summary>
    public static void CreateDefaultAssets() {

        EnsureFoldersExist();

        // Create or get settings asset
        RCCP_AISettings settings = CreateOrGetSettings();

        // Wire up prompts array in settings (prompts are shipped with the package)
        WireUpPromptsArray(settings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

    }

    /// <summary>
    /// Ensures the required folder structure exists
    /// </summary>
    private static void EnsureFoldersExist() {
        // Use centralized folder structure creation
        RCCP_AIUtility.EnsureFolderStructure();
    }

    /// <summary>
    /// Creates or returns the existing settings asset
    /// </summary>
    private static RCCP_AISettings CreateOrGetSettings() {

        RCCP_AISettings settings = AssetDatabase.LoadAssetAtPath<RCCP_AISettings>(SETTINGS_ASSET_PATH);

        if (settings == null) {

            // Try loading from Resources as fallback
            settings = Resources.Load<RCCP_AISettings>("RCCP_AISettings");

        }

        if (settings == null) {

            settings = ScriptableObject.CreateInstance<RCCP_AISettings>();
            AssetDatabase.CreateAsset(settings, SETTINGS_ASSET_PATH);
            Debug.Log("[RCCP AI] Created default settings asset.");

            // Reset singleton so it picks up the newly created asset
            RCCP_AISettings.ResetInstance();

        }

        return settings;

    }

    /// <summary>
    /// Wires up the prompts array in the settings asset
    /// </summary>
    private static void WireUpPromptsArray(RCCP_AISettings settings) {

        if (settings == null) return;

        // Load all prompt assets from the folder
        string[] guids = AssetDatabase.FindAssets("t:RCCP_AIPromptAsset", new[] { PROMPTS_PATH });
        List<RCCP_AIPromptAsset> promptList = new List<RCCP_AIPromptAsset>();

        // Normalize PROMPTS_PATH to ensure consistent comparison (remove trailing slashes)
        string rootPromptsPath = PROMPTS_PATH.Replace("\\", "/").TrimEnd('/');

        foreach (string guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // Check if file is directly in the prompts folder, NOT in a subfolder
            string directory = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            
            // Allow exact match or if PROMPTS_PATH is relative and directory resolves to it
            // Simple check: directory must end with the prompts path name
            if (directory.Equals(rootPromptsPath, System.StringComparison.OrdinalIgnoreCase)) {
                RCCP_AIPromptAsset prompt = AssetDatabase.LoadAssetAtPath<RCCP_AIPromptAsset>(path);
                if (prompt != null)
                    promptList.Add(prompt);
            }
        }

        RCCP_AIPromptAsset[] prompts = promptList.ToArray();

        if (prompts.Length == 0) {
            Debug.LogWarning("[RCCP AI] No prompt assets found to wire up.");
            return;
        }

        // Check if wiring is needed
        bool needsWiring = false;

        if (settings.prompts == null || settings.prompts.Length != prompts.Length) {
            needsWiring = true;
        } else {
            // Check if any prompts are missing or null
            foreach (var p in settings.prompts) {
                if (p == null) {
                    needsWiring = true;
                    break;
                }
            }
        }

        if (needsWiring) {

            // Sort prompts by panel type for consistent ordering
            System.Array.Sort(prompts, (a, b) => ((int)a.panelType).CompareTo((int)b.panelType));

            Undo.RecordObject(settings, "Wire Up Prompts Array");
            settings.prompts = prompts;
            EditorUtility.SetDirty(settings);
            Debug.Log($"[RCCP AI] Wired up {prompts.Length} prompts to settings.");

        }

    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
