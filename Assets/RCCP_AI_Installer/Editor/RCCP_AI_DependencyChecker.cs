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
using UnityEditor;
using UnityEngine;

namespace RCCP_AI_Installer
{
    /// <summary>
    /// Dependency status container.
    /// </summary>
    public struct DependencyStatus
    {
        public bool unityVersionOk;
        public string unityVersionMessage;
        public bool rccpInstalled;
        public string rccpVersion;
        public bool rccpVersionRecommended;  // True if V2.2 or newer
        public bool editorCoroutinesInstalled;
        public bool mainPackageInstalled;
        public bool mainPackageConfigured;
    }

    /// <summary>
    /// Validates all dependencies without hard references to external types.
    /// Uses reflection and file detection for maximum compatibility.
    /// </summary>
    public static class RCCP_AI_DependencyChecker
    {
        #region Constants

        private const string RCCP_FOLDER_PATH = "Assets/Realistic Car Controller Pro";
        private const string MAIN_PACKAGE_PATH = "Assets/RCCP AI Assistant";
        private const string SETTINGS_RESOURCE_PATH = "RCCP_AISettings";
        private const int MIN_UNITY_MAJOR_VERSION = 6000;  // Unity 6

        #endregion

        #region Unity Version

        /// <summary>
        /// Validates Unity version is 6000.0+ (Unity 6).
        /// </summary>
        public static bool ValidateUnityVersion()
        {
            string version = Application.unityVersion;
            string[] parts = version.Split('.');

            if (int.TryParse(parts[0], out int major))
            {
                return major >= MIN_UNITY_MAJOR_VERSION;
            }
            return false;
        }

        /// <summary>
        /// Gets a human-readable Unity version status message.
        /// </summary>
        public static string GetUnityVersionMessage()
        {
            bool isSupported = ValidateUnityVersion();
            return isSupported
                ? $"Unity {Application.unityVersion} - Supported"
                : $"Unity {Application.unityVersion} - Requires Unity 6 (6000.0+)";
        }

        #endregion

        #region RCCP Detection

        /// <summary>
        /// Checks if Realistic Car Controller Pro is installed.
        /// Uses multiple detection methods for reliability.
        /// </summary>
        public static bool IsRCCPInstalled()
        {
            // Method 1: Check folder exists
            if (Directory.Exists(RCCP_FOLDER_PATH))
                return true;

            // Method 2: Check for scripting symbol
            string symbols = PlayerSettings.GetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.Standalone);
            if (symbols.Contains("BCG_RCCP"))
                return true;

            // Method 3: Type reflection
            Type rccpType = Type.GetType("RCCP_CarController, RealisticCarControllerPro.Runtime");
            if (rccpType != null)
                return true;

            // Method 4: Alternative assembly name
            rccpType = Type.GetType("RCCP_CarController, Assembly-CSharp");
            if (rccpType != null)
                return true;

            return false;
        }

        /// <summary>
        /// Attempts to get the RCCP version string.
        /// Returns "Unknown" if version cannot be determined.
        /// </summary>
        public static string GetRCCPVersionIfAvailable()
        {
            try
            {
                // Try to load RCCP_Settings and get version via reflection
                Type settingsType = Type.GetType("RCCP_Settings, RealisticCarControllerPro.Runtime")
                    ?? Type.GetType("RCCP_Settings, Assembly-CSharp");

                if (settingsType != null)
                {
                    var instanceProp = settingsType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (instanceProp != null)
                    {
                        var instance = instanceProp.GetValue(null);
                        if (instance != null)
                        {
                            var versionField = settingsType.GetField("version",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                            if (versionField != null)
                            {
                                var version = versionField.GetValue(instance);
                                if (version != null)
                                    return version.ToString();
                            }
                        }
                    }
                }

                // Fallback: Check version file if exists
                string versionFilePath = Path.Combine(RCCP_FOLDER_PATH, "version.txt");
                if (File.Exists(versionFilePath))
                {
                    return File.ReadAllText(versionFilePath).Trim();
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return IsRCCPInstalled() ? "Detected" : "Not Found";
        }

        /// <summary>
        /// Checks if RCCP version 2.2 or newer is installed.
        /// This is the recommended version for RCCP AI Assistant.
        /// </summary>
        public static bool IsRCCPVersionRecommended()
        {
            if (!IsRCCPInstalled())
                return false;

            // Method 1: Check scripting define symbol (most reliable)
            string symbols = PlayerSettings.GetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.Standalone);
            if (symbols.Contains("RCCP_V2_2_OR_NEWER"))
                return true;

            // Method 2: Check RCCP_Version class via reflection
            try
            {
                Type versionType = Type.GetType("RCCP_Version, RealisticCarControllerPro.Runtime")
                    ?? Type.GetType("RCCP_Version, Assembly-CSharp");

                if (versionType != null)
                {
                    var versionField = versionType.GetField("version",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.FlattenHierarchy);

                    if (versionField != null)
                    {
                        string version = versionField.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(version))
                        {
                            // Parse version string (e.g., "V2.2", "V2.0", "V2.1")
                            return IsVersionAtLeast(version, 2, 2);
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            // Method 3: Check version from GetRCCPVersionIfAvailable
            string detectedVersion = GetRCCPVersionIfAvailable();
            if (!string.IsNullOrEmpty(detectedVersion) && detectedVersion != "Detected" && detectedVersion != "Not Found")
            {
                return IsVersionAtLeast(detectedVersion, 2, 2);
            }

            return false;
        }

        /// <summary>
        /// Parses a version string and checks if it's at least the specified major.minor version.
        /// Supports formats: "V2.2", "2.2", "v2.2.1", etc.
        /// </summary>
        private static bool IsVersionAtLeast(string versionString, int minMajor, int minMinor)
        {
            if (string.IsNullOrEmpty(versionString))
                return false;

            // Remove common prefixes
            versionString = versionString.TrimStart('V', 'v', ' ');

            // Split by dot
            string[] parts = versionString.Split('.');
            if (parts.Length < 2)
                return false;

            if (int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                if (major > minMajor)
                    return true;
                if (major == minMajor && minor >= minMinor)
                    return true;
            }

            return false;
        }

        #endregion

        #region Editor Coroutines Detection

        /// <summary>
        /// Checks if Editor Coroutines package is installed.
        /// </summary>
        public static bool IsEditorCoroutinesInstalled()
        {
            // Method 1: Check manifest.json
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                string content = File.ReadAllText(manifestPath);
                if (content.Contains("com.unity.editorcoroutines"))
                    return true;
            }

            // Method 2: Type reflection
            Type coroutineType = Type.GetType("Unity.EditorCoroutines.Editor.EditorCoroutineUtility, Unity.EditorCoroutines.Editor");
            if (coroutineType != null)
                return true;

            return false;
        }

        /// <summary>
        /// Gets the Editor Coroutines version if installed.
        /// </summary>
        public static string GetEditorCoroutinesVersion()
        {
            try
            {
                string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (File.Exists(manifestPath))
                {
                    string content = File.ReadAllText(manifestPath);

                    // Simple parsing - look for version after package name
                    int pkgIndex = content.IndexOf("com.unity.editorcoroutines");
                    if (pkgIndex >= 0)
                    {
                        int colonIndex = content.IndexOf(':', pkgIndex);
                        int quoteStart = content.IndexOf('"', colonIndex);
                        int quoteEnd = content.IndexOf('"', quoteStart + 1);

                        if (quoteStart >= 0 && quoteEnd > quoteStart)
                        {
                            return content.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return IsEditorCoroutinesInstalled() ? "Installed" : "Not Installed";
        }

        #endregion

        #region Main Package Detection

        /// <summary>
        /// Checks if the main RCCP AI Assistant package is installed.
        /// </summary>
        public static bool IsMainPackageInstalled()
        {
            return Directory.Exists(MAIN_PACKAGE_PATH);
        }

        /// <summary>
        /// Checks if the main package is properly configured with settings.
        /// </summary>
        public static bool IsMainPackageConfigured()
        {
            if (!IsMainPackageInstalled())
                return false;

            // Check if settings asset exists
            var settings = Resources.Load<ScriptableObject>(SETTINGS_RESOURCE_PATH);
            return settings != null;
        }

        #endregion

        #region Aggregate Checks

        /// <summary>
        /// Checks if all required dependencies are installed.
        /// </summary>
        public static bool AreAllDependenciesInstalled()
        {
            return IsRCCPInstalled() && IsEditorCoroutinesInstalled();
        }

        /// <summary>
        /// Gets a complete status of all dependencies.
        /// </summary>
        public static DependencyStatus GetFullStatus()
        {
            return new DependencyStatus
            {
                unityVersionOk = ValidateUnityVersion(),
                unityVersionMessage = GetUnityVersionMessage(),
                rccpInstalled = IsRCCPInstalled(),
                rccpVersion = GetRCCPVersionIfAvailable(),
                rccpVersionRecommended = IsRCCPVersionRecommended(),
                editorCoroutinesInstalled = IsEditorCoroutinesInstalled(),
                mainPackageInstalled = IsMainPackageInstalled(),
                mainPackageConfigured = IsMainPackageConfigured()
            };
        }

        /// <summary>
        /// Gets a human-readable dependency status string.
        /// </summary>
        public static string GetDependencyStatusString()
        {
            var status = GetFullStatus();
            return $@"
Unity Version: {status.unityVersionMessage}
RCCP: {(status.rccpInstalled ? $"Installed ({status.rccpVersion})" : "Not Found")}
Editor Coroutines: {(status.editorCoroutinesInstalled ? "Installed" : "Not Installed")}
RCCP AI Assistant: {(status.mainPackageInstalled ? (status.mainPackageConfigured ? "Installed & Configured" : "Installed (needs setup)") : "Not Installed")}
";
        }

        #endregion

        #region Embedded Package Detection

        /// <summary>
        /// Checks if the embedded main package exists in the installer.
        /// </summary>
        public static bool IsEmbeddedPackageAvailable()
        {
            string path = FindEmbeddedPackagePath();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        /// <summary>
        /// Finds the path to the embedded .unitypackage file.
        /// </summary>
        public static string FindEmbeddedPackagePath()
        {
            // Primary path
            string primaryPath = "Assets/RCCP_AI_Installer/Editor/Resources/RCCP_AI_Complete.unitypackage";
            if (File.Exists(primaryPath))
                return primaryPath;

            // Alternative paths
            string[] alternativePaths = new[]
            {
                "Assets/RCCP_AI_Installer/Resources/RCCP_AI_Complete.unitypackage",
                "Assets/RCCP_AI_Installer/RCCP_AI_Complete.unitypackage"
            };

            foreach (string path in alternativePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Search by GUID
            string[] guids = AssetDatabase.FindAssets("RCCP_AI_Complete t:DefaultAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".unitypackage"))
                    return path;
            }

            return null;
        }

        #endregion
    }
}
#endif
