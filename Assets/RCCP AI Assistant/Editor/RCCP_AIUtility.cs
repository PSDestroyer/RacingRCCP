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
using System.IO;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Utility class providing centralized helper methods for RCCP AI Assistant.
/// </summary>
public static class RCCP_AIUtility {

    #region API Usage Tracking (Read-Only from Server)

    // Rate limiting is now server-authoritative.
    // These properties provide read-only access to cached server data for UI display.

    /// <summary>
    /// Gets the current query count (total requests made).
    /// </summary>
    public static int QueryCount => RCCP_AIRateLimiter.TotalRequests;

    /// <summary>
    /// Gets or sets whether the user is using their own API key (unlimited).
    /// </summary>
    public static bool UseOwnApiKey {
        get => RCCP_AIRateLimiter.UseOwnApiKey;
        set => RCCP_AIRateLimiter.UseOwnApiKey = value;
    }

    /// <summary>
    /// Gets whether developer mode is enabled.
    /// </summary>
    public static bool IsDeveloperMode => RCCP_AIRateLimiter.IsDeveloperMode;

    /// <summary>
    /// Gets the remaining queries for free tier users (from cached server data).
    /// Returns setup pool remaining if in Phase 1, daily remaining if in Phase 2.
    /// </summary>
    public static int RemainingFreeQueries {
        get {
            if (UseOwnApiKey) return -1;
            if (RCCP_AIRateLimiter.IsInSetupPhase)
                return RCCP_AIRateLimiter.SetupPoolRemaining;
            return RCCP_AIRateLimiter.DailyRemaining;
        }
    }

    #endregion

    #region Dynamic Paths

    // Cached root path - computed once per session
    private static string _cachedRootPath = null;
    private const string DEFAULT_ROOT_PATH = "Assets/RCCP AI Assistant";

    /// <summary>
    /// Gets the root path of the RCCP AI Assistant folder dynamically.
    /// This allows users to move/rename the folder without breaking functionality.
    /// </summary>
    public static string RootPath {
        get {
            if (string.IsNullOrEmpty(_cachedRootPath)) {
                _cachedRootPath = FindRootPath();
            }
            return _cachedRootPath;
        }
    }

    /// <summary>
    /// Forces recalculation of the root path. Call after folder moves.
    /// </summary>
    public static void RefreshRootPath() {
        _cachedRootPath = null;
    }

    /// <summary>
    /// Finds the root path by locating this script file.
    /// </summary>
    private static string FindRootPath() {
        // Find RCCP_AIUtility.cs script asset
        string[] guids = AssetDatabase.FindAssets("t:Script RCCP_AIUtility");
        if (guids.Length > 0) {
            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            // scriptPath = ".../RCCP AI Assistant/Editor/RCCP_AIUtility.cs"
            // We need to go up 2 levels: Editor -> RCCP AI Assistant
            string editorFolder = Path.GetDirectoryName(scriptPath);
            string rootFolder = Path.GetDirectoryName(editorFolder);

            if (!string.IsNullOrEmpty(rootFolder)) {
                // Normalize path separators for Unity
                return rootFolder.Replace("\\", "/");
            }
        }

        // Fallback: try finding RCCP_AISettings asset
        string[] settingsGuids = AssetDatabase.FindAssets("t:RCCP_AISettings");
        if (settingsGuids.Length > 0) {
            string settingsPath = AssetDatabase.GUIDToAssetPath(settingsGuids[0]);
            // settingsPath = ".../RCCP AI Assistant/Editor/Resources/RCCP_AISettings.asset"
            string resourcesFolder = Path.GetDirectoryName(settingsPath);
            string editorFolder = Path.GetDirectoryName(resourcesFolder);
            string rootFolder = Path.GetDirectoryName(editorFolder);

            if (!string.IsNullOrEmpty(rootFolder)) {
                return rootFolder.Replace("\\", "/");
            }
        }

        Debug.LogWarning("[RCCP AI] Could not dynamically find RCCP AI Assistant folder. Using default path.");
        return DEFAULT_ROOT_PATH;
    }

    /// <summary>
    /// Gets the Editor folder path.
    /// </summary>
    public static string EditorPath => $"{RootPath}/Editor";

    /// <summary>
    /// Gets the Resources folder path inside Editor.
    /// </summary>
    public static string ResourcesPath => $"{RootPath}/Editor/Resources";

    /// <summary>
    /// Gets the Prompts folder path.
    /// </summary>
    public static string PromptsPath => $"{RootPath}/Editor/Resources/Prompts";

    /// <summary>
    /// Gets the Textures folder path.
    /// </summary>
    public static string TexturesPath => $"{RootPath}/Editor/Textures";

    /// <summary>
    /// Gets the path for the settings asset.
    /// </summary>
    public static string SettingsAssetPath => $"{ResourcesPath}/RCCP_AISettings.asset";

    /// <summary>
    /// Gets the path for the component defaults asset.
    /// </summary>
    public static string ComponentDefaultsAssetPath => $"{ResourcesPath}/RCCP_AIComponentDefaults.asset";

    /// <summary>
    /// Gets the path for the GUISkin asset.
    /// </summary>
    public static string GUISkinPath => $"{ResourcesPath}/RCCP_AI_Guiskin.guiskin";

    #region Library Data Paths (Gitignored)

    // Data that should NOT be committed to VCS goes in Library/
    private const string LIBRARY_DATA_FOLDER = "RCCP_AIAssistant";

    /// <summary>
    /// Gets the path for RCCP AI Assistant data in the Library folder.
    /// This path is always gitignored and should be used for logs, history, and debug data.
    /// </summary>
    public static string LibraryDataPath {
        get {
            // Unity's Library folder is at project root level
            string projectPath = Application.dataPath.Replace("/Assets", "");
            return Path.Combine(projectPath, "Library", LIBRARY_DATA_FOLDER).Replace("\\", "/");
        }
    }

    /// <summary>
    /// Gets the path for the debug screenshots folder (in Library - gitignored).
    /// </summary>
    public static string DebugScreenshotsPath => $"{LibraryDataPath}/DebugScreenshots";

    /// <summary>
    /// Gets the path for the logs folder (in Library - gitignored).
    /// </summary>
    public static string LogsPath => $"{LibraryDataPath}/Logs";

    /// <summary>
    /// Gets the path for the prompt history file (in Library - gitignored).
    /// </summary>
    public static string HistoryPath => $"{LibraryDataPath}/History";

    /// <summary>
    /// Ensures the Library data folder structure exists.
    /// </summary>
    public static void EnsureLibraryFolderStructure() {
        try {
            if (!Directory.Exists(LibraryDataPath)) {
                Directory.CreateDirectory(LibraryDataPath);
            }
            if (!Directory.Exists(LogsPath)) {
                Directory.CreateDirectory(LogsPath);
            }
            if (!Directory.Exists(HistoryPath)) {
                Directory.CreateDirectory(HistoryPath);
            }
            if (!Directory.Exists(DebugScreenshotsPath)) {
                Directory.CreateDirectory(DebugScreenshotsPath);
            }
        } catch (Exception e) {
            Debug.LogWarning($"[RCCP AI] Failed to create Library folder structure: {e.Message}");
        }
    }

    #endregion

    // Legacy paths - kept for migration purposes only
    private static string LegacyDebugScreenshotsPath => $"{RootPath}/DebugScreenshots";
    private static string LegacyLogsPath => $"{RootPath}/Logs";

    /// <summary>
    /// Ensures the base folder structure exists. Creates folders if missing.
    /// </summary>
    public static void EnsureFolderStructure() {
        // Create root if needed (only if using default path)
        if (!AssetDatabase.IsValidFolder(RootPath)) {
            string parent = Path.GetDirectoryName(RootPath)?.Replace("\\", "/") ?? "Assets";
            string folderName = Path.GetFileName(RootPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        // Create Editor folder
        if (!AssetDatabase.IsValidFolder(EditorPath)) {
            AssetDatabase.CreateFolder(RootPath, "Editor");
        }

        // Create Resources folder
        if (!AssetDatabase.IsValidFolder(ResourcesPath)) {
            AssetDatabase.CreateFolder(EditorPath, "Resources");
        }

        // Create Prompts folder
        if (!AssetDatabase.IsValidFolder(PromptsPath)) {
            AssetDatabase.CreateFolder(ResourcesPath, "Prompts");
        }
    }

    #endregion

    #region Error Logging

    private static string LogFolder {
        get {
            EnsureLibraryFolderStructure();
            return LogsPath;
        }
    }
    private const string LOG_FILE_NAME = "rccp_ai_errors.log";
    private const int MAX_LOG_SIZE_KB = 500;  // Max log file size before rotation

    /// <summary>
    /// Logs an error to the error log file.
    /// </summary>
    /// <param name="source">The source of the error (e.g., "API", "VehicleBuilder")</param>
    /// <param name="message">The error message</param>
    /// <param name="exception">Optional exception details</param>
    public static void LogError(string source, string message, Exception exception = null) {
        try {
            // Ensure log directory exists
            if (!Directory.Exists(LogFolder)) {
                Directory.CreateDirectory(LogFolder);
            }

            string logPath = Path.Combine(LogFolder, LOG_FILE_NAME);

            // Check log file size and rotate if needed
            if (File.Exists(logPath)) {
                FileInfo fileInfo = new FileInfo(logPath);
                if (fileInfo.Length > MAX_LOG_SIZE_KB * 1024) {
                    RotateLogFile(logPath);
                }
            }

            // Build log entry
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] [{source}] {message}";

            if (exception != null) {
                logEntry += $"\n  Exception: {exception.GetType().Name}: {exception.Message}";
                if (!string.IsNullOrEmpty(exception.StackTrace)) {
                    logEntry += $"\n  Stack: {exception.StackTrace.Split('\n')[0]}";
                }
            }

            logEntry += "\n";

            // Append to log file
            File.AppendAllText(logPath, logEntry);

            // Also log to Unity console if verbose mode
            if (RCCP_AIVehicleBuilder.VerboseLogging) {
                Debug.Log($"[RCCP AI Log] {message}");
            }
        } catch (Exception e) {
            // Don't let logging errors break the main flow
            Debug.LogWarning($"[RCCP AI] Failed to write to log file: {e.Message}");
        }
    }

    /// <summary>
    /// Logs an API error specifically.
    /// </summary>
    public static void LogApiError(string endpoint, string error, int httpCode = 0) {
        string message = $"API Error: {error}";
        if (httpCode > 0) {
            message = $"API Error (HTTP {httpCode}): {error}";
        }
        message += $" | Endpoint: {endpoint}";
        LogError("API", message);
    }

    /// <summary>
    /// Rotates the log file by renaming with timestamp.
    /// </summary>
    private static void RotateLogFile(string logPath) {
        try {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = logPath.Replace(".log", $"_{timestamp}.log");
            File.Move(logPath, backupPath);

            // Clean up old log files (keep last 5)
            CleanupOldLogs();
        } catch (Exception e) {
            Debug.LogWarning($"[RCCP AI] Failed to rotate log file: {e.Message}");
        }
    }

    /// <summary>
    /// Cleans up old log files, keeping only the most recent ones.
    /// </summary>
    private static void CleanupOldLogs() {
        try {
            if (!Directory.Exists(LogFolder)) return;

            string[] logFiles = Directory.GetFiles(LogFolder, "rccp_ai_errors*.log");
            if (logFiles.Length <= 5) return;

            // Sort by creation time and delete oldest
            Array.Sort(logFiles, (a, b) => File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

            for (int i = 0; i < logFiles.Length - 5; i++) {
                File.Delete(logFiles[i]);
            }
        } catch (Exception e) {
            Debug.LogWarning($"[RCCP AI] Failed to cleanup old logs: {e.Message}");
        }
    }

    /// <summary>
    /// Gets the path to the error log file.
    /// </summary>
    public static string GetLogFilePath() {
        return Path.Combine(LogFolder, LOG_FILE_NAME);
    }

    /// <summary>
    /// Opens the error log file in the default text editor.
    /// </summary>
    public static void OpenLogFile() {
        string logPath = GetLogFilePath();
        if (File.Exists(logPath)) {
            EditorUtility.OpenWithDefaultApp(logPath);
        } else {
            Debug.Log("[RCCP AI] No error log file exists yet.");
        }
    }

    #endregion

    #region Float Value Extension Methods

    /// <summary>
    /// Checks if a float value was explicitly specified in configuration.
    /// Using float.NaN as the "unset" sentinel is best practice, but we also support
    /// checking for zero values for backward compatibility.
    /// </summary>
    /// <param name="value">The float value to check</param>
    /// <param name="allowZero">If true, zero is considered a valid specified value.
    /// If false, zero is treated as "not specified".</param>
    /// <returns>True if the value was explicitly specified</returns>
    public static bool IsSpecified(this float value, bool allowZero = false) {
        // NaN always means "not specified"
        if (float.IsNaN(value)) return false;

        // If we allow zero, any non-NaN value is valid
        if (allowZero) return true;

        // Otherwise, treat zero as "not specified"
        return !Mathf.Approximately(value, 0f);
    }

    #endregion

    #region JSON Utilities

    /// <summary>
    /// Extracts JSON content from a response that may be wrapped in markdown code fences.
    /// Handles ```json blocks, plain ``` blocks, and raw JSON.
    /// </summary>
    /// <param name="response">The response string that may contain JSON</param>
    /// <returns>The extracted JSON string, or the original response if no JSON found</returns>
    public static string ExtractJson(string response) {
        if (string.IsNullOrEmpty(response)) return response;

        // Handle markdown-wrapped JSON (```json or ```)
        if (response.Contains("```")) {
            int jsonStart = response.IndexOf("```json");
            if (jsonStart >= 0) {
                // Found ```json block - skip past the opening fence and newline
                jsonStart = response.IndexOf('\n', jsonStart) + 1;
            } else {
                // Try plain ``` block
                jsonStart = response.IndexOf("```") + 3;
                int newline = response.IndexOf('\n', jsonStart);
                if (newline > jsonStart) jsonStart = newline + 1;
            }

            int jsonEnd = response.LastIndexOf("```");
            if (jsonEnd > jsonStart) {
                return response.Substring(jsonStart, jsonEnd - jsonStart).Trim();
            }
        }

        // Fall back to raw JSON extraction - find outermost braces
        int start = response.IndexOf('{');
        int end = response.LastIndexOf('}');
        if (start >= 0 && end > start) {
            return response.Substring(start, end - start + 1);
        }

        return response;
    }

    #endregion

    /// <summary>
    /// Checks if a GameObject has RCCP_CarController installed (on itself or any parent).
    /// </summary>
    /// <param name="gameObject">The GameObject to check</param>
    /// <returns>True if RCCP_CarController is found on the object or any parent</returns>
    public static bool HasRCCP(GameObject gameObject) {
        if (gameObject == null) return false;
        return gameObject.GetComponentInParent<RCCP_CarController>() != null;
    }

    /// <summary>
    /// Gets the RCCP_CarController from a GameObject (checks itself and parents).
    /// </summary>
    /// <param name="gameObject">The GameObject to check</param>
    /// <returns>The RCCP_CarController if found, null otherwise</returns>
    public static RCCP_CarController GetRCCPController(GameObject gameObject) {
        if (gameObject == null) return null;
        return gameObject.GetComponentInParent<RCCP_CarController>();
    }

    /// <summary>
    /// Checks if the currently selected GameObject in the Unity Editor has RCCP installed.
    /// </summary>
    /// <returns>True if current selection has RCCP_CarController</returns>
    public static bool SelectionHasRCCP() {
        return HasRCCP(Selection.activeGameObject);
    }

    /// <summary>
    /// Gets the RCCP_CarController from the currently selected GameObject in the Unity Editor.
    /// </summary>
    /// <returns>The RCCP_CarController if found on selection, null otherwise</returns>
    public static RCCP_CarController GetSelectionRCCPController() {
        return GetRCCPController(Selection.activeGameObject);
    }

    /// <summary>
    /// Checks if a GameObject is a valid target for RCCP vehicle creation (no existing RCCP).
    /// </summary>
    /// <param name="gameObject">The GameObject to check</param>
    /// <returns>True if the object can have RCCP added to it</returns>
    public static bool CanAddRCCP(GameObject gameObject) {
        return gameObject != null && !HasRCCP(gameObject);
    }

    /// <summary>
    /// Checks if a GameObject is a scene object (not a prefab asset).
    /// </summary>
    /// <param name="gameObject">The GameObject to check</param>
    /// <returns>True if the object is in a scene</returns>
    public static bool IsSceneObject(GameObject gameObject) {
        if (gameObject == null) return false;
        return !EditorUtility.IsPersistent(gameObject) && gameObject.scene.IsValid();
    }

    /// <summary>
    /// Checks if a GameObject is part of a prefab instance.
    /// </summary>
    /// <param name="gameObject">The GameObject to check</param>
    /// <returns>True if the object is part of a prefab instance</returns>
    public static bool IsPrefabInstance(GameObject gameObject) {
        if (gameObject == null) return false;
        return PrefabUtility.IsPartOfPrefabInstance(gameObject);
    }

    /// <summary>
    /// Unpacks a prefab instance if needed. Call this before creating/destroying child GameObjects.
    /// Shows a confirmation dialog to warn the user about prefab modification.
    /// </summary>
    /// <param name="gameObject">The GameObject to check and unpack</param>
    /// <returns>True if prefab was unpacked or wasn't a prefab, false if unpacking failed or user cancelled</returns>
    public static bool UnpackPrefabIfNeeded(GameObject gameObject) {
        if (gameObject == null) return false;

        if (!PrefabUtility.IsPartOfPrefabInstance(gameObject)) {
            return true; // Not a prefab, no unpacking needed
        }

        // Find the root of the prefab instance
        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
        if (prefabRoot == null) {
            prefabRoot = gameObject;
        }

        // Show confirmation dialog before unpacking
        bool userConfirmed = EditorUtility.DisplayDialog(
            "Prefab Modification Required",
            $"The vehicle '{prefabRoot.name}' is a prefab instance. To modify its hierarchy (add/remove components), it must be unpacked.\n\n" +
            "This will break the link to the original prefab. You can undo this action if needed.\n\n" +
            "Do you want to continue?",
            "Unpack and Continue",
            "Cancel"
        );

        if (!userConfirmed) {
            Debug.Log("[RCCP AI] User cancelled prefab unpack operation.");
            return false;
        }

        try {
            PrefabUtility.UnpackPrefabInstance(prefabRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (RCCP_AIVehicleBuilder.VerboseLogging) Debug.Log($"[RCCP AI] Unpacked prefab instance: {prefabRoot.name}");
            return true;
        } catch (System.Exception e) {
            Debug.LogWarning($"[RCCP AI] Failed to unpack prefab: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Refreshes the selection by deselecting and reselecting the GameObject.
    /// This forces editor scripts to reinitialize (OnEnable/OnDisable) and update their cached values.
    /// Only works for scene objects, not prefab assets stored in the project.
    /// </summary>
    /// <param name="gameObject">The GameObject to refresh selection for</param>
    public static void RefreshSelection(GameObject gameObject) {
        if (gameObject == null) return;

        // Only refresh scene objects, not prefab assets in the project
        if (!IsSceneObject(gameObject)) return;

        // Store instance ID instead of reference (safer if object gets modified)
        int instanceId = gameObject.GetInstanceID();

        // Signal that we're starting a programmatic refresh
        // This prevents the AI Assistant window from clearing its state during the deselect
        RCCP_AIAssistantWindow.BeginRefreshSelection();

        Selection.activeGameObject = null;

        // Use delayCall to reselect on next editor update
        EditorApplication.delayCall += () => {
            // Find the object by instance ID (safer than holding reference)
            UnityEngine.Object obj = Resources.InstanceIDToObject(instanceId);
            if (obj != null && obj is GameObject go) {
                Selection.activeGameObject = go;
            }
            // Signal refresh complete and update windows
            RCCP_AIAssistantWindow.EndRefreshSelection();
        };
    }

    /// <summary>
    /// Activates a GameObject if it's inactive. Call this before applying configurations.
    /// Records an undo operation for the activation.
    /// </summary>
    /// <param name="gameObject">The GameObject to activate</param>
    /// <returns>True if the object was activated, false if it was already active or null</returns>
    public static bool ActivateIfInactive(GameObject gameObject) {
        if (gameObject == null) return false;

        if (!gameObject.activeInHierarchy) {
            Undo.RecordObject(gameObject, "Activate Vehicle");
            gameObject.SetActive(true);
            if (RCCP_AIVehicleBuilder.VerboseLogging) Debug.Log($"[RCCP AI] Activated inactive vehicle: {gameObject.name}");
            return true;
        }

        return false;
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
