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
using UnityEngine;

namespace RCCP_AI_Installer {
    /// <summary>
    /// Multi-step installation wizard with visual progress tracking.
    /// Guides users through installing the RCCP AI Setup Assistant.
    /// </summary>
    public class RCCP_AI_SetupWizard : EditorWindow {
        #region Constants

        private const int TOTAL_STEPS = 5;
        private const float MIN_WIDTH = 550f;
        private const float MIN_HEIGHT = 650f;
        private const float MAX_WIDTH = 700f;
        private const float MAX_HEIGHT = 950f;
        private const float HORIZONTAL_PADDING = 15f;

        private static readonly string[] STEP_NAMES = new[]
        {
            "Welcome",
            "Dependencies",
            "API Options",
            "Import",
            "Complete"
        };

        // SessionState keys
        private const string SESSION_STEP = "RCCP_AI_Installer_Step";
        private const string SESSION_SHOWN = "RCCP_AI_Installer_Shown";

        #endregion

        #region State

        private int _currentStep = 0;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.Info;
        private bool _isProcessing = false;
        private Vector2 _scrollPosition;

        // Dependency status cache
        private DependencyStatus _dependencyStatus;
        private double _lastStatusRefresh;
        private const double STATUS_REFRESH_INTERVAL = 1.0; // seconds

        #endregion

        #region Menu & Auto-Open

        [MenuItem("Tools/BoneCracker Games/RCCP AI Assistant/Setup Wizard", false, 50)]
        public static void ShowWindow() {
            var window = GetWindow<RCCP_AI_SetupWizard>("RCCP AI Setup");
            window.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            window.maxSize = new Vector2(MAX_WIDTH, MAX_HEIGHT);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void AutoOpenOnFirstImport() {
            // Don't auto-open if already shown this session
            if (SessionState.GetBool(SESSION_SHOWN, false))
                return;

            // Don't auto-open if main package is already installed
            if (RCCP_AI_DependencyChecker.IsMainPackageInstalled())
                return;

            // Mark as shown
            SessionState.SetBool(SESSION_SHOWN, true);

            // Delay to let Unity finish loading
            EditorApplication.delayCall += () => {
                ShowWindow();
            };
        }

        #endregion

        #region Unity Callbacks

        private void OnEnable() {
            // Restore state from session
            _currentStep = SessionState.GetInt(SESSION_STEP, 0);

            // Validate step (in case main package was installed externally)
            if (RCCP_AI_DependencyChecker.IsMainPackageInstalled() && _currentStep < TOTAL_STEPS - 1) {
                _currentStep = TOTAL_STEPS - 1; // Jump to Complete step
            }

            RefreshDependencyStatus();
        }

        private void OnDisable() {
            // Save state
            SessionState.SetInt(SESSION_STEP, _currentStep);

            // Clear any progress bars
            RCCP_AI_AutoInstaller.ClearProgressBar();
        }

        private void OnGUI() {
            // Periodic status refresh
            if (EditorApplication.timeSinceStartup - _lastStatusRefresh > STATUS_REFRESH_INTERVAL) {
                RefreshDependencyStatus();
            }

            // Add horizontal padding wrapper
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(HORIZONTAL_PADDING);

            EditorGUILayout.BeginVertical();

            EditorGUILayout.Space(15);

            // Header
            EditorGUILayout.LabelField("RCCP AI Setup Assistant", RCCP_AI_InstallerStyles.HeaderStyle);
            EditorGUILayout.LabelField("Installation Wizard", RCCP_AI_InstallerStyles.BodyCenteredStyle);

            EditorGUILayout.Space(10);

            // Step indicator
            RCCP_AI_InstallerStyles.DrawStepIndicator(_currentStep, TOTAL_STEPS, STEP_NAMES);

            EditorGUILayout.Space(15);
            RCCP_AI_InstallerStyles.DrawSeparator();

            // Scrollable content area
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Draw current step content
            switch (_currentStep) {
                case 0: DrawWelcomeStep(); break;
                case 1: DrawDependenciesStep(); break;
                case 2: DrawApiInfoStep(); break;
                case 3: DrawImportStep(); break;
                case 4: DrawCompleteStep(); break;
            }

            EditorGUILayout.EndScrollView();

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage)) {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }

            // Navigation buttons
            EditorGUILayout.Space(10);
            RCCP_AI_InstallerStyles.DrawSeparator();
            DrawNavigationButtons();

            EditorGUILayout.Space(10);

            EditorGUILayout.EndVertical();

            GUILayout.Space(HORIZONTAL_PADDING);
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Step 0: Welcome

        private void DrawWelcomeStep() {
            EditorGUILayout.LabelField("Welcome!", RCCP_AI_InstallerStyles.SubheaderStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "This wizard will guide you through installing the RCCP AI Setup Assistant - " +
                "an AI-powered tool for configuring vehicles using natural language.",
                RCCP_AI_InstallerStyles.BodyStyle);

            EditorGUILayout.Space(15);

            EditorGUILayout.BeginVertical(RCCP_AI_InstallerStyles.PanelStyle);
            EditorGUILayout.LabelField("What this will do:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("• Check for required dependencies", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("• Install Unity Editor Coroutines (if needed)", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("• Explain API options (free tier included)", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("• Import the main RCCP AI Assistant package", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            // System check
            EditorGUILayout.LabelField("System Check", RCCP_AI_InstallerStyles.SubheaderStyle);
            EditorGUILayout.Space(5);

            RCCP_AI_InstallerStyles.DrawStatusLine(
                "Unity Version",
                _dependencyStatus.unityVersionMessage,
                _dependencyStatus.unityVersionOk);

            if (!_dependencyStatus.unityVersionOk) {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Your Unity version may not be fully supported. Some features might not work correctly.",
                    MessageType.Warning);
            }
        }

        #endregion

        #region Step 1: Dependencies

        private void DrawDependenciesStep() {
            EditorGUILayout.LabelField("Required Dependencies", RCCP_AI_InstallerStyles.SubheaderStyle);
            EditorGUILayout.Space(10);

            // RCCP Check - show version status with different indicators
            string rccpStatusText;
            bool rccpStatusOk;

            if (!_dependencyStatus.rccpInstalled) {
                rccpStatusText = "Not Found";
                rccpStatusOk = false;
            } else if (_dependencyStatus.rccpVersionRecommended) {
                rccpStatusText = $"Installed ({_dependencyStatus.rccpVersion})";
                rccpStatusOk = true;
            } else {
                // Installed but older version
                rccpStatusText = $"Installed ({_dependencyStatus.rccpVersion}) - Update Recommended";
                rccpStatusOk = true; // Still green, but we'll show a warning
            }

            EditorGUILayout.BeginHorizontal();
            RCCP_AI_InstallerStyles.DrawStatusLine(
                "Realistic Car Controller Pro",
                rccpStatusText,
                rccpStatusOk);
            EditorGUILayout.EndHorizontal();

            if (!_dependencyStatus.rccpInstalled) {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(RCCP_AI_InstallerStyles.PanelStyle);
                EditorGUILayout.LabelField(
                    "RCCP AI Assistant requires Realistic Car Controller Pro.\n" +
                    "Please install RCCP from the Asset Store first.",
                    RCCP_AI_InstallerStyles.BodyStyle);
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Open Asset Store", RCCP_AI_InstallerStyles.ButtonSecondaryStyle, GUILayout.Width(150))) {
                    Application.OpenURL("https://u3d.as/22Bf");
                }
                EditorGUILayout.EndVertical();
            } else if (!_dependencyStatus.rccpVersionRecommended) {
                // RCCP is installed but it's an older version (V2.0 or V2.1)
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "RCCP V2.2 or newer is recommended for full compatibility.\n" +
                    "You can still use RCCP AI Assistant with your current version, but some features may not work correctly.\n" +
                    "Consider updating RCCP to the latest version for the best experience.",
                    MessageType.Warning);
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Update RCCP (Asset Store)", RCCP_AI_InstallerStyles.ButtonSecondaryStyle, GUILayout.Width(200))) {
                    Application.OpenURL("https://u3d.as/22Bf");
                }
            }

            EditorGUILayout.Space(10);

            // Editor Coroutines Check
            EditorGUILayout.BeginHorizontal();
            RCCP_AI_InstallerStyles.DrawStatusLine(
                "Editor Coroutines",
                _dependencyStatus.editorCoroutinesInstalled ? "Installed" : "Not Installed",
                _dependencyStatus.editorCoroutinesInstalled);

            if (!_dependencyStatus.editorCoroutinesInstalled && !_isProcessing) {
                if (GUILayout.Button("Install", RCCP_AI_InstallerStyles.ButtonSecondaryStyle, GUILayout.Width(80))) {
                    InstallEditorCoroutines();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!_dependencyStatus.editorCoroutinesInstalled) {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(
                    "Required for asynchronous API calls in the editor.",
                    RCCP_AI_InstallerStyles.BodyStyle);
            }

            EditorGUILayout.Space(15);

            // Refresh button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh Status", RCCP_AI_InstallerStyles.ButtonSecondaryStyle, GUILayout.Width(120))) {
                RefreshDependencyStatus();
                Repaint();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Show success message when all deps installed
            if (_dependencyStatus.rccpInstalled && _dependencyStatus.editorCoroutinesInstalled) {
                EditorGUILayout.Space(15);
                EditorGUILayout.HelpBox("All dependencies are installed! Click Next to continue.", MessageType.Info);
            }
        }

        private void InstallEditorCoroutines() {
            _isProcessing = true;
            _statusMessage = "Installing Editor Coroutines...";
            _statusType = MessageType.Info;

            RCCP_AI_AutoInstaller.InstallEditorCoroutines((success, message) => {
                _isProcessing = false;
                _statusMessage = message;
                _statusType = success ? MessageType.Info : MessageType.Error;
                RefreshDependencyStatus();
                Repaint();
            });
        }

        #endregion

        #region Step 2: API Info

        private void DrawApiInfoStep() {
            EditorGUILayout.LabelField("API Configuration", RCCP_AI_InstallerStyles.SubheaderStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "The RCCP AI Assistant uses Claude AI for natural language vehicle configuration. " +
                "No API key is required to get started!",
                RCCP_AI_InstallerStyles.BodyStyle);

            EditorGUILayout.Space(15);

            // Free tier info
            EditorGUILayout.BeginVertical(RCCP_AI_InstallerStyles.PanelStyle);
            EditorGUILayout.LabelField("Free Tier (Default - No API Key Needed)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("• 400 setup requests to get started", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("• Then 25 requests per day", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("• Uses our proxy server, no account needed", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Optional API key info
            EditorGUILayout.BeginVertical(RCCP_AI_InstallerStyles.PanelStyle);
            EditorGUILayout.LabelField("Own API Key (Optional - Unlimited)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("For unlimited requests, you can use your own Claude API key:", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("1. Visit console.anthropic.com", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("2. Create an account and get an API key", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("3. Configure it in Settings after installation", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            if (RCCP_AI_InstallerStyles.DrawCenteredButton("Open Anthropic Console", 200)) {
                Application.OpenURL("https://console.anthropic.com/");
            }

            EditorGUILayout.Space(15);

            EditorGUILayout.HelpBox(
                "The free tier is ready to use immediately after installation. " +
                "Click Next to continue.",
                MessageType.Info);
        }

        #endregion

        #region Step 3: Import

        private void DrawImportStep() {
            EditorGUILayout.LabelField("Import RCCP AI Assistant", RCCP_AI_InstallerStyles.SubheaderStyle);
            EditorGUILayout.Space(10);

            bool alreadyInstalled = RCCP_AI_DependencyChecker.IsMainPackageInstalled();

            if (alreadyInstalled) {
                EditorGUILayout.HelpBox(
                    "RCCP AI Assistant is already installed!\n\n" +
                    "Click Next to continue, or Reimport to reinstall.",
                    MessageType.Info);

                EditorGUILayout.Space(10);

                EditorGUI.BeginDisabledGroup(_isProcessing);
                if (RCCP_AI_InstallerStyles.DrawCenteredButton("Reimport Package", 180)) {
                    StartImport();
                }
                EditorGUI.EndDisabledGroup();
            } else {
                EditorGUILayout.LabelField(
                    "Ready to import the main package containing:",
                    RCCP_AI_InstallerStyles.BodyStyle);

                EditorGUILayout.Space(10);

                EditorGUILayout.BeginVertical(RCCP_AI_InstallerStyles.PanelStyle);
                EditorGUILayout.LabelField("• AI-powered vehicle creation wizard", RCCP_AI_InstallerStyles.BodyStyle);
                EditorGUILayout.LabelField("• Natural language customization panels", RCCP_AI_InstallerStyles.BodyStyle);
                EditorGUILayout.LabelField("• Behavior preset configuration", RCCP_AI_InstallerStyles.BodyStyle);
                EditorGUILayout.LabelField("• Wheel, audio, lights, damage panels", RCCP_AI_InstallerStyles.BodyStyle);
                EditorGUILayout.LabelField("• Vision-based light detection", RCCP_AI_InstallerStyles.BodyStyle);
                EditorGUILayout.LabelField("• Complete documentation", RCCP_AI_InstallerStyles.BodyStyle);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(15);

                // Check if embedded package exists
                bool packageExists = RCCP_AI_DependencyChecker.IsEmbeddedPackageAvailable();

                if (!packageExists) {
                    EditorGUILayout.HelpBox(
                        "Embedded package not found!\n\n" +
                        "Please re-download the installer from the Asset Store.",
                        MessageType.Error);
                } else {
                    EditorGUI.BeginDisabledGroup(_isProcessing);
                    if (RCCP_AI_InstallerStyles.DrawCenteredButton("Import RCCP AI Assistant", 220)) {
                        StartImport();
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }

            // Show progress
            if (_isProcessing) {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(RCCP_AI_PackageImporter.StatusMessage, RCCP_AI_InstallerStyles.BodyCenteredStyle);
            }
        }

        private void StartImport() {
            _isProcessing = true;
            _statusMessage = "Importing package...";
            _statusType = MessageType.Info;

            RCCP_AI_PackageImporter.StartImport((success, message) => {
                _isProcessing = false;
                _statusMessage = message;
                _statusType = success ? MessageType.Info : MessageType.Error;

                if (success) {
                    _currentStep = TOTAL_STEPS - 1; // Jump to Complete
                    SessionState.SetInt(SESSION_STEP, _currentStep);
                }

                RefreshDependencyStatus();
                Repaint();
            });
        }

        #endregion

        #region Step 4: Complete

        private void DrawCompleteStep() {
            EditorGUILayout.Space(20);

            // Success icon/message
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.color = RCCP_AI_InstallerStyles.SuccessColor;
            GUILayout.Label(EditorGUIUtility.IconContent("TestPassed"), GUILayout.Width(48), GUILayout.Height(48));
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            var successStyle = new GUIStyle(RCCP_AI_InstallerStyles.HeaderStyle);
            successStyle.normal.textColor = RCCP_AI_InstallerStyles.SuccessColor;
            EditorGUILayout.LabelField("Installation Complete!", successStyle);

            EditorGUILayout.Space(15);

            EditorGUILayout.LabelField(
                "RCCP AI Setup Assistant has been installed successfully.",
                RCCP_AI_InstallerStyles.BodyCenteredStyle);

            EditorGUILayout.Space(20);

            EditorGUILayout.BeginVertical(RCCP_AI_InstallerStyles.PanelStyle);
            EditorGUILayout.LabelField("Next steps:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("1. Select a vehicle or 3D model in your scene", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("2. Open the AI Assistant and start creating!", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.LabelField("3. (Optional) Add your API key in Settings for unlimited use", RCCP_AI_InstallerStyles.BodyStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            if (RCCP_AI_InstallerStyles.DrawCenteredButton("Open RCCP AI Assistant", 220)) {
                RCCP_AI_PackageImporter.OpenMainWindow();
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Access anytime via: Tools > BoneCracker Games > RCCP AI Assistant",
                RCCP_AI_InstallerStyles.BodyCenteredStyle);

            EditorGUILayout.Space(20);

            // Delete installer option
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete Installer Files", RCCP_AI_InstallerStyles.ButtonSecondaryStyle, GUILayout.Width(150))) {
                if (RCCP_AI_PackageImporter.DeleteInstallerFiles()) {
                    RCCP_AI_PackageImporter.OpenMainWindow();
                    Close();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Navigation

        private void DrawNavigationButtons() {
            EditorGUILayout.BeginHorizontal();

            // Back button
            EditorGUI.BeginDisabledGroup(_currentStep == 0 || _isProcessing);
            if (GUILayout.Button("← Back", RCCP_AI_InstallerStyles.ButtonSecondaryStyle, GUILayout.Width(100))) {
                _currentStep--;
                _statusMessage = "";
                SessionState.SetInt(SESSION_STEP, _currentStep);
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            // Next/Finish button
            if (_currentStep == TOTAL_STEPS - 1) {
                // Complete step - show Close button
                if (GUILayout.Button("Close", RCCP_AI_InstallerStyles.ButtonPrimaryStyle, GUILayout.Width(100))) {
                    SessionState.EraseInt(SESSION_STEP);
                    Close();
                }
            } else {
                // Other steps - show Next button
                bool canProceed = CanProceedToNextStep();
                EditorGUI.BeginDisabledGroup(!canProceed || _isProcessing);
                if (GUILayout.Button("Next →", RCCP_AI_InstallerStyles.ButtonPrimaryStyle, GUILayout.Width(100))) {
                    _currentStep++;
                    _statusMessage = "";
                    SessionState.SetInt(SESSION_STEP, _currentStep);

                    // Auto-advance from Import step if already installed
                    if (_currentStep == 3 && RCCP_AI_DependencyChecker.IsMainPackageInstalled()) {
                        _currentStep = TOTAL_STEPS - 1;
                        SessionState.SetInt(SESSION_STEP, _currentStep);
                    }
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool CanProceedToNextStep() {
            switch (_currentStep) {
                case 0: // Welcome - always can proceed
                return true;

                case 1: // Dependencies - all must be installed
                return _dependencyStatus.rccpInstalled && _dependencyStatus.editorCoroutinesInstalled;

                case 2: // API Info - always can proceed (informational only)
                return true;

                case 3: // Import - package must exist
                return RCCP_AI_DependencyChecker.IsMainPackageInstalled();

                default:
                return true;
            }
        }

        #endregion

        #region Helpers

        private void RefreshDependencyStatus() {
            _dependencyStatus = RCCP_AI_DependencyChecker.GetFullStatus();
            _lastStatusRefresh = EditorApplication.timeSinceStartup;
        }

        #endregion
    }
}
#endif
