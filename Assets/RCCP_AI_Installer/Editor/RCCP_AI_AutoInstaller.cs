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
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace RCCP_AI_Installer
{
    /// <summary>
    /// Automatically installs required Unity packages via Package Manager API.
    /// Uses polling pattern since Editor Coroutines may not be available yet.
    /// </summary>
    public static class RCCP_AI_AutoInstaller
    {
        #region State

        private static AddRequest _addRequest;
        private static Action<bool, string> _completionCallback;
        private static string _currentPackage;
        private static bool _isInstalling;

        /// <summary>
        /// Whether an installation is currently in progress.
        /// </summary>
        public static bool IsInstalling => _isInstalling;

        /// <summary>
        /// Current installation status message.
        /// </summary>
        public static string StatusMessage { get; private set; } = "";

        #endregion

        #region Editor Coroutines Installation

        /// <summary>
        /// Installs the Editor Coroutines package.
        /// </summary>
        /// <param name="onComplete">Callback with (success, message)</param>
        public static void InstallEditorCoroutines(Action<bool, string> onComplete)
        {
            InstallPackage("com.unity.editorcoroutines", "Editor Coroutines", onComplete);
        }

        #endregion

        #region Generic Package Installation

        /// <summary>
        /// Installs a Unity package by ID.
        /// </summary>
        /// <param name="packageId">Package identifier (e.g., "com.unity.editorcoroutines")</param>
        /// <param name="displayName">Human-readable name for status messages</param>
        /// <param name="onComplete">Callback with (success, message)</param>
        public static void InstallPackage(string packageId, string displayName, Action<bool, string> onComplete)
        {
            if (_isInstalling)
            {
                onComplete?.Invoke(false, "Another installation is already in progress.");
                return;
            }

            _isInstalling = true;
            _currentPackage = displayName;
            _completionCallback = onComplete;
            StatusMessage = $"Installing {displayName}...";

            Debug.Log($"[RCCP AI Installer] Starting installation of {packageId}");

            try
            {
                // Start the package add request
                _addRequest = Client.Add(packageId);

                // Subscribe to editor update for polling
                EditorApplication.update += PollInstallProgress;
            }
            catch (Exception ex)
            {
                _isInstalling = false;
                StatusMessage = $"Failed to start installation: {ex.Message}";
                Debug.LogError($"[RCCP AI Installer] {StatusMessage}");
                onComplete?.Invoke(false, StatusMessage);
            }
        }

        /// <summary>
        /// Polls the installation progress until complete.
        /// </summary>
        private static void PollInstallProgress()
        {
            if (_addRequest == null)
            {
                EditorApplication.update -= PollInstallProgress;
                return;
            }

            if (!_addRequest.IsCompleted)
            {
                // Still in progress
                return;
            }

            // Installation complete
            EditorApplication.update -= PollInstallProgress;

            bool success = _addRequest.Status == StatusCode.Success;
            string message;

            if (success)
            {
                message = $"{_currentPackage} installed successfully.";
                Debug.Log($"[RCCP AI Installer] {message}");
            }
            else
            {
                message = $"Failed to install {_currentPackage}: {_addRequest.Error?.message ?? "Unknown error"}";
                Debug.LogError($"[RCCP AI Installer] {message}");
            }

            StatusMessage = message;
            _isInstalling = false;

            // Invoke callback
            var callback = _completionCallback;
            _completionCallback = null;
            _addRequest = null;
            _currentPackage = null;

            callback?.Invoke(success, message);
        }

        #endregion

        #region Cancel

        /// <summary>
        /// Cancels any pending installation (cannot cancel in-progress Package Manager operations).
        /// </summary>
        public static void Cancel()
        {
            if (_isInstalling)
            {
                EditorApplication.update -= PollInstallProgress;
                _isInstalling = false;
                StatusMessage = "Installation cancelled.";
                _completionCallback?.Invoke(false, StatusMessage);
                _completionCallback = null;
                _addRequest = null;
                _currentPackage = null;
            }
        }

        #endregion

        #region Progress Display

        /// <summary>
        /// Shows a progress bar during installation.
        /// Call this from OnGUI to display progress.
        /// </summary>
        public static void ShowProgressBar()
        {
            if (_isInstalling)
            {
                EditorUtility.DisplayProgressBar(
                    "Installing Package",
                    StatusMessage,
                    -1f);  // Indeterminate progress
            }
            else
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Clears the progress bar.
        /// </summary>
        public static void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }

        #endregion

        #region Utility

        /// <summary>
        /// Opens the Package Manager window.
        /// </summary>
        public static void OpenPackageManager()
        {
            UnityEditor.PackageManager.UI.Window.Open("");
        }

        /// <summary>
        /// Opens the Package Manager window with a specific package selected.
        /// </summary>
        public static void OpenPackageManager(string packageId)
        {
            UnityEditor.PackageManager.UI.Window.Open(packageId);
        }

        #endregion
    }
}
#endif
