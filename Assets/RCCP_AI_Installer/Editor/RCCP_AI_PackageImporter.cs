//----------------------------------------------
//        RCCP AI Setup Assistant - Installer
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RCCP_AI_Installer
{
    /// <summary>
    /// Handles extraction and import of the bundled main package.
    /// Manages domain reload recovery.
    /// Note: BCG_RCCP_AI scripting symbol is set by RCCP_AIInitLoad.cs after import.
    /// </summary>
    public static class RCCP_AI_PackageImporter
    {
        #region Constants

        private const string TEMP_PACKAGE_PATH = "Temp/RCCP_AI_Import.unitypackage";
        private const string PENDING_IMPORT_KEY = "RCCP_AI_PendingImport";
        private const string IMPORT_CALLBACK_KEY = "RCCP_AI_ImportCallback";

        #endregion

        #region State

        private static Action<bool, string> _importCallback;
        private static bool _isImporting;

        /// <summary>
        /// Whether an import is currently in progress.
        /// </summary>
        public static bool IsImporting => _isImporting;

        /// <summary>
        /// Current import status message.
        /// </summary>
        public static string StatusMessage { get; private set; } = "";

        #endregion

        #region Domain Reload Recovery

        /// <summary>
        /// Checks for pending import after domain reload.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void CheckPendingImport()
        {
            // Check if we were mid-import when domain reloaded
            if (SessionState.GetBool(PENDING_IMPORT_KEY, false))
            {
                SessionState.SetBool(PENDING_IMPORT_KEY, false);

                // Delay to let Unity finish loading
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[RCCP AI Installer] Resuming package import after domain reload...");
                    ExtractAndImportPackage(null);
                };
            }
        }

        #endregion

        #region Main Import Flow

        /// <summary>
        /// Starts the import process for the main package.
        /// </summary>
        /// <param name="onComplete">Callback with (success, message)</param>
        public static void StartImport(Action<bool, string> onComplete)
        {
            if (_isImporting)
            {
                onComplete?.Invoke(false, "Import already in progress.");
                return;
            }

            _isImporting = true;
            _importCallback = onComplete;
            StatusMessage = "Preparing import...";

            try
            {
                // Step 1: Verify dependencies
                if (!RCCP_AI_DependencyChecker.AreAllDependenciesInstalled())
                {
                    CompleteImport(false, "Cannot import: dependencies not installed. Please install RCCP and Editor Coroutines first.");
                    return;
                }

                // Step 2: Check if already installed
                if (RCCP_AI_DependencyChecker.IsMainPackageInstalled())
                {
                    bool reimport = EditorUtility.DisplayDialog(
                        "Already Installed",
                        "RCCP AI Assistant is already installed. Do you want to reimport?\n\n" +
                        "This will overwrite existing files.",
                        "Reimport", "Cancel");

                    if (!reimport)
                    {
                        CompleteImport(false, "Import cancelled by user.");
                        return;
                    }
                }

                // Step 3: Extract and import
                // Note: BCG_RCCP_AI scripting symbol is set by RCCP_AIInitLoad.cs
                // after the package is imported to avoid compile errors
                ExtractAndImportPackage(onComplete);
            }
            catch (Exception ex)
            {
                CompleteImport(false, $"Import failed: {ex.Message}");
            }
        }

        #endregion

        #region Package Extraction

        /// <summary>
        /// Extracts and imports the embedded package.
        /// </summary>
        private static void ExtractAndImportPackage(Action<bool, string> onComplete)
        {
            _importCallback = onComplete;
            StatusMessage = "Finding embedded package...";

            try
            {
                // Find embedded package
                string sourcePath = RCCP_AI_DependencyChecker.FindEmbeddedPackagePath();
                if (string.IsNullOrEmpty(sourcePath))
                {
                    CompleteImport(false, "Embedded package not found! Please re-download the installer from the Asset Store.");
                    return;
                }

                StatusMessage = "Extracting package...";

                // Generate temp path (handle file locks)
                string tempPath = GetUniqueTempPath();

                // Copy to temp
                File.Copy(sourcePath, tempPath, overwrite: true);
                Debug.Log($"[RCCP AI Installer] Extracted to: {tempPath}");

                StatusMessage = "Importing package...";

                // Subscribe to import events
                AssetDatabase.importPackageCompleted += OnImportCompleted;
                AssetDatabase.importPackageCancelled += OnImportCancelled;
                AssetDatabase.importPackageFailed += OnImportFailed;

                // Start import (interactive=false for automated install)
                AssetDatabase.ImportPackage(tempPath, interactive: false);
            }
            catch (Exception ex)
            {
                CompleteImport(false, $"Extraction failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a unique temp path for the package.
        /// </summary>
        private static string GetUniqueTempPath()
        {
            string basePath = TEMP_PACKAGE_PATH;

            // If file exists and locked, generate unique name
            if (File.Exists(basePath))
            {
                try
                {
                    File.Delete(basePath);
                }
                catch
                {
                    // File locked, use unique name
                    basePath = $"Temp/RCCP_AI_Import_{Guid.NewGuid():N}.unitypackage";
                }
            }

            return basePath;
        }

        #endregion

        #region Import Callbacks

        private static void OnImportCompleted(string packageName)
        {
            UnsubscribeFromImportEvents();

            Debug.Log($"[RCCP AI Installer] Package imported successfully: {packageName}");

            // Clean up temp file
            CleanupTempFiles();

            // Refresh asset database
            AssetDatabase.Refresh();

            // Open main window after a delay
            EditorApplication.delayCall += () =>
            {
                OpenMainWindow();
            };

            CompleteImport(true, "RCCP AI Assistant installed successfully!");
        }

        private static void OnImportCancelled(string packageName)
        {
            UnsubscribeFromImportEvents();
            CleanupTempFiles();

            Debug.LogWarning($"[RCCP AI Installer] Package import cancelled: {packageName}");
            CompleteImport(false, "Import was cancelled.");
        }

        private static void OnImportFailed(string packageName, string errorMessage)
        {
            UnsubscribeFromImportEvents();
            CleanupTempFiles();

            Debug.LogError($"[RCCP AI Installer] Package import failed: {packageName} - {errorMessage}");
            CompleteImport(false, $"Import failed: {errorMessage}");
        }

        private static void UnsubscribeFromImportEvents()
        {
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageCancelled -= OnImportCancelled;
            AssetDatabase.importPackageFailed -= OnImportFailed;
        }

        #endregion

        #region Completion

        private static void CompleteImport(bool success, string message)
        {
            _isImporting = false;
            StatusMessage = message;

            if (success)
            {
                Debug.Log($"[RCCP AI Installer] {message}");
            }
            else
            {
                Debug.LogError($"[RCCP AI Installer] {message}");
            }

            var callback = _importCallback;
            _importCallback = null;
            callback?.Invoke(success, message);
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleans up temporary files created during import.
        /// </summary>
        private static void CleanupTempFiles()
        {
            try
            {
                string[] tempFiles = Directory.GetFiles("Temp", "RCCP_AI_Import*.unitypackage");
                foreach (string file in tempFiles)
                {
                    try { File.Delete(file); }
                    catch { /* Ignore locked files */ }
                }
            }
            catch { /* Ignore errors */ }
        }

        #endregion

        #region Post-Install

        /// <summary>
        /// Opens the main RCCP AI Assistant window via reflection.
        /// </summary>
        public static void OpenMainWindow()
        {
            try
            {
                // Try to find and open the main window via reflection
                // This avoids compile-time dependency on the main package

                // Try different assembly names
                Type windowType = Type.GetType("RCCP_AIAssistantWindow, Assembly-CSharp-Editor");

                if (windowType == null)
                {
                    // Try finding by searching all assemblies
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        windowType = assembly.GetType("RCCP_AIAssistantWindow");
                        if (windowType != null)
                            break;
                    }
                }

                if (windowType != null)
                {
                    var showMethod = windowType.GetMethod("ShowWindow",
                        BindingFlags.Public | BindingFlags.Static);

                    if (showMethod != null)
                    {
                        showMethod.Invoke(null, null);
                        Debug.Log("[RCCP AI Installer] Opened RCCP AI Assistant window.");
                        return;
                    }
                }

                // Fallback: Try menu item
                EditorApplication.ExecuteMenuItem("Tools/BoneCracker Games/RCCP AI Assistant/Open Assistant");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RCCP AI Installer] Could not open main window: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes the installer files after successful installation.
        /// </summary>
        public static bool DeleteInstallerFiles()
        {
            if (!RCCP_AI_DependencyChecker.IsMainPackageInstalled())
            {
                Debug.LogWarning("[RCCP AI Installer] Cannot delete installer: main package not installed.");
                return false;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Installer",
                "Delete the installer files?\n\n" +
                "The main RCCP AI Assistant package is already installed.\n" +
                "You can re-download the installer from the Asset Store if needed.",
                "Delete", "Keep");

            if (!confirmed)
                return false;

            try
            {
                AssetDatabase.DeleteAsset("Assets/RCCP_AI_Installer");
                Debug.Log("[RCCP AI Installer] Installer files deleted.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RCCP AI Installer] Failed to delete installer: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
#endif
