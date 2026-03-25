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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

namespace BoneCrackerGames.RCCP.AIAssistant {

    /// <summary>
    /// Main window for RCCP AI Assistant.
    /// Uses ScriptableObject assets for prompts.
    /// </summary>
    public partial class RCCP_AIAssistantWindow : EditorWindow {

        #region Variables

        // Settings
        private RCCP_AISettings settings;
        private RCCP_AIPromptAsset[] availablePrompts;
        private int currentPromptIndex = 0;

        private RCCP_AIPromptAsset CurrentPrompt =>
            availablePrompts != null && currentPromptIndex < availablePrompts.Length
                ? availablePrompts[currentPromptIndex]
                : null;

        /// <summary>
        /// Returns true if the currently selected GameObject has RCCP_CarController installed.
        /// Uses the cached selectedController value which is updated via RefreshSelection().
        /// </summary>
        private bool HasRCCPController => selectedController != null;

        // UI State
        private Vector2 mainScrollPosition;
        private float scrollTargetY = -1f;  // -1 means no animation, >= 0 means animating to target
        private const float SCROLL_ANIMATION_SPEED = 8f;  // Lerp speed
        private Vector2 sidebarScrollPosition;
        private Vector2 meshScrollPosition;
        private Vector2 responseScrollPosition;
        private Vector2 historyScrollPosition;
        private Vector2 historyDetailScrollPosition;
        private Vector2 proposedChangesScrollPosition;
        private bool showSettings = false;
        private bool showHistory = false;
        private bool showPromptHistory = false;
        private bool showWelcome = false;
        private float welcomePanelAlpha = 0f;
        private int selectedHistoryIndex = -1;
        private string selectedPromptHistoryId = null;
        private string promptHistorySearch = "";
        private string promptHistoryPanelFilter = "All";
        private int promptHistoryAppliedFilter = 0;  // 0=All, 1=Applied, 2=Not Applied
        private Vector2 promptHistoryScrollPosition;
        private Vector2 promptHistoryDetailScrollPosition;
        private float sidebarWidth = 200f;

        // Prompt History UI improvements
        private RCCP_AIPromptHistory.PromptHistorySortOption promptHistorySortOption = RCCP_AIPromptHistory.PromptHistorySortOption.NewestFirst;
        private HashSet<string> selectedHistoryEntries = new HashSet<string>();  // For bulk selection
        private bool promptHistoryBulkMode = false;
        private bool showCopyOptions = false;
        private Dictionary<string, bool> jsonSectionFoldouts = new Dictionary<string, bool>();  // For collapsible JSON

        // User Input
        private string userPrompt = "";
        private GameObject selectedVehicle;
        private RCCP_CarController selectedController;
        private bool isSelectionInScene = false;
        private bool hasExistingRigidbodies = false;
        private int existingRigidbodyCount = 0;
        private bool hasExistingWheelColliders = false;
        private int existingWheelColliderCount = 0;
        private bool isPrefabInstance = false;
        private bool isVehicleInactive = false;
        private bool hasMultipleSelection = false;
        private int vehiclePickerControlID;

        // Track selected object's active state to detect enable/disable while selected
        // This handles the case where Selection.selectionChanged doesn't fire when
        // the selected object itself changes state (only fires when selection changes)
        private bool lastKnownActiveState = false;
        private Vector3 lastKnownScale = Vector3.one;  // Track scale changes for eligibility recalculation

        // Flag to prevent race condition during selection refresh
        // When RCCP_AIUtility.RefreshSelection() deselects/reselects, this prevents the window from clearing state
        private static bool isRefreshingSelection = false;

        // Size Warning System - Typical car: 4.5m x 1.8m x 1.5m (L x W x H)
        private const float SIZE_MIN_DIMENSION = 0.5f;   // Minimum expected size (meters) - below this is likely a toy/model
        private const float SIZE_MAX_LENGTH = 5.5f;      // Above typical sedan/SUV length - trucks, vans, buses
        private const float SIZE_MAX_WIDTH = 2.1f;       // Above typical car width - wider vehicles
        private const float SIZE_MAX_HEIGHT = 1.9f;      // Above typical car height - trucks, SUVs, vans
        private const float SIZE_TYPICAL_CAR_MIN = 3.5f; // Typical car minimum length (compact)
        private const float SIZE_TYPICAL_CAR_MAX = 5f;   // Typical car maximum length (sedan/SUV)
        private const float SIZE_ASPECT_RATIO_MIN = 1.5f; // Minimum length/width ratio for cars
        private const float SIZE_ASPECT_RATIO_MAX = 4f;  // Maximum length/width ratio
        private const float SIZE_BATCH_DIFF_THRESHOLD = 1.5f; // Warn if vehicles differ by more than 50% in size

        private List<SizeWarning> currentSizeWarnings = new List<SizeWarning>();
        private Dictionary<GameObject, List<SizeWarning>> batchSizeWarnings = new Dictionary<GameObject, List<SizeWarning>>();

        private enum SizeWarningLevel { Info, Warning, Error }
        private class SizeWarning {
            public SizeWarningLevel level;
            public string message;
            public SizeWarning(SizeWarningLevel level, string message) {
                this.level = level;
                this.message = message;
            }
        }

        // Eligibility Check System - Validates if model is suitable for RCCP conversion
        private enum EligibilityStatus { Pass, Warning, Fail, Unknown }

        private class WheelCandidate {
            public Transform transform;
            public string name;
            public Vector3 localPosition;
            public float estimatedRadius;
            public bool isSeparateMesh;
            public string axleGuess;  // "FL", "FR", "RL", "RR", or "Unknown"

            public WheelCandidate(Transform t, float radius, bool separateMesh) {
                transform = t;
                name = t.name;
                localPosition = t.localPosition;
                estimatedRadius = radius;
                isSeparateMesh = separateMesh;
                axleGuess = "Unknown";
            }
        }

        private class EligibilityCheck {
            // Overall status
            public EligibilityStatus overallStatus = EligibilityStatus.Unknown;

            // Scale check
            public EligibilityStatus scaleStatus = EligibilityStatus.Unknown;
            public string scaleMessage = "";
            public Vector3 dimensions = Vector3.zero;

            // Orientation check
            public EligibilityStatus orientationStatus = EligibilityStatus.Unknown;
            public string orientationMessage = "";
            public bool zIsForward = true;
            public bool yIsUp = true;
            public bool needsRotation = false;
            public Vector3 suggestedRotation = Vector3.zero;

            // Wheel check
            public EligibilityStatus wheelStatus = EligibilityStatus.Unknown;
            public string wheelMessage = "";
            public List<WheelCandidate> wheelCandidates = new List<WheelCandidate>();
            public int separateWheelCount = 0;
            public bool wheelsMergedWithBody = false;

            // Helper to calculate overall status
            public void CalculateOverallStatus() {
                if (scaleStatus == EligibilityStatus.Fail ||
                    orientationStatus == EligibilityStatus.Fail ||
                    wheelStatus == EligibilityStatus.Fail) {
                    overallStatus = EligibilityStatus.Fail;
                } else if (scaleStatus == EligibilityStatus.Warning ||
                           orientationStatus == EligibilityStatus.Warning ||
                           wheelStatus == EligibilityStatus.Warning) {
                    overallStatus = EligibilityStatus.Warning;
                } else if (scaleStatus == EligibilityStatus.Pass &&
                           orientationStatus == EligibilityStatus.Pass &&
                           wheelStatus == EligibilityStatus.Pass) {
                    overallStatus = EligibilityStatus.Pass;
                } else {
                    overallStatus = EligibilityStatus.Unknown;
                }
            }

            // Get status icon
            public static string GetStatusIcon(EligibilityStatus status) {
                switch (status) {
                    case EligibilityStatus.Pass: return "✓";
                    case EligibilityStatus.Warning: return "⚠";
                    case EligibilityStatus.Fail: return "✗";
                    default: return "?";
                }
            }

            // Get status color - using design system
            public static Color GetStatusColor(EligibilityStatus status) {
                switch (status) {
                    case EligibilityStatus.Pass: return RCCP_AIDesignSystem.Colors.Success;
                    case EligibilityStatus.Warning: return RCCP_AIDesignSystem.Colors.Warning;
                    case EligibilityStatus.Fail: return RCCP_AIDesignSystem.Colors.Error;
                    default: return RCCP_AIDesignSystem.Colors.TextDisabled;
                }
            }
        }

        /// <summary>
        /// Converts eligibility check wheel candidates to DetectedWheels config.
        /// Uses RCCP-style position-based detection: forward direction for front/rear, X position for left/right.
        /// </summary>
        private RCCP_AIConfig.DetectedWheels ConvertWheelCandidatesToDetectedWheels(
            List<WheelCandidate> candidates,
            Transform vehicleRoot) {
            if (candidates == null || candidates.Count < 4 || vehicleRoot == null)
                return null;

            var result = new RCCP_AIConfig.DetectedWheels();

            // Use RCCP-style detection: dot product with forward for front/rear, X position for left/right
            // This is more reliable than relying on axleGuess which may not be set correctly
            Vector3 vehicleCenter = vehicleRoot.position;
            Vector3 vehicleForward = vehicleRoot.forward;

            // Separate into front and rear using dot product (RCCP approach)
            var frontWheels = new List<WheelCandidate>();
            var rearWheels = new List<WheelCandidate>();

            foreach (var wheel in candidates) {
                Vector3 directionToWheel = wheel.transform.position - vehicleCenter;
                float dotProduct = Vector3.Dot(vehicleForward, directionToWheel);

                if (dotProduct > 0)
                    frontWheels.Add(wheel);
                else
                    rearWheels.Add(wheel);
            }

            // Need at least 2 front and 2 rear wheels
            if (frontWheels.Count < 2 || rearWheels.Count < 2) {
                Debug.LogWarning($"[RCCP AI] Wheel position detection failed: {frontWheels.Count} front, {rearWheels.Count} rear");
                return null;
            }

            // Sort front wheels by X position (left to right in world space, relative to vehicle)
            // For 6+ wheel vehicles, take the 2 most extreme X positions
            var sortedFront = frontWheels
                .OrderBy(w => vehicleRoot.InverseTransformPoint(w.transform.position).x)
                .ToList();
            var sortedRear = rearWheels
                .OrderBy(w => vehicleRoot.InverseTransformPoint(w.transform.position).x)
                .ToList();

            // Assign: lowest X = left, highest X = right
            result.frontLeft = GetWheelTransformPath(sortedFront.First().transform, vehicleRoot);
            result.frontRight = GetWheelTransformPath(sortedFront.Last().transform, vehicleRoot);
            result.rearLeft = GetWheelTransformPath(sortedRear.First().transform, vehicleRoot);
            result.rearRight = GetWheelTransformPath(sortedRear.Last().transform, vehicleRoot);

            // Validate all 4 wheels were assigned
            if (string.IsNullOrEmpty(result.frontLeft) ||
                string.IsNullOrEmpty(result.frontRight) ||
                string.IsNullOrEmpty(result.rearLeft) ||
                string.IsNullOrEmpty(result.rearRight)) {
                return null;  // Incomplete detection, use manual fallback
            }

            return result;
        }

        /// <summary>
        /// Gets the hierarchical path from root to target transform.
        /// E.g., "Body/Suspension/WheelFL"
        /// </summary>
        private string GetWheelTransformPath(Transform target, Transform root) {
            if (target == null || root == null) return null;

            var pathParts = new List<string>();
            Transform current = target;

            while (current != null && current != root) {
                pathParts.Insert(0, current.name);
                current = current.parent;
            }

            return pathParts.Count > 0 ? string.Join("/", pathParts) : null;
        }

        // Current eligibility check result (single selection)
        private EligibilityCheck currentEligibility = null;

        // Batch eligibility checks
        private Dictionary<GameObject, EligibilityCheck> batchEligibility = new Dictionary<GameObject, EligibilityCheck>();

        // Eligibility UI state
        private bool eligibilityFoldout = true;

        // Batch Processing State (for multiple vehicle creation)
        private List<GameObject> batchVehicles = new List<GameObject>();
        private int currentBatchIndex = 0;
        private bool isBatchProcessing = false;
        private Dictionary<GameObject, string> batchResponses = new Dictionary<GameObject, string>();
        private Dictionary<GameObject, string> batchMeshAnalysis = new Dictionary<GameObject, string>();
        private string batchUserPrompt = "";  // Store the user prompt for batch operations

        // Batch Customization State (for multiple vehicle customization)
        // Now processes each vehicle individually like Vehicle Creation does
        private List<RCCP_CarController> batchCustomizationVehicles = new List<RCCP_CarController>();
        private Dictionary<RCCP_CarController, string> batchCustomizationResponses = new Dictionary<RCCP_CarController, string>();
        private int currentBatchCustomizationIndex = 0;
        private bool isBatchCustomizationProcessing = false;
        private string batchCustomizationUserPrompt = "";
        private bool isBatchCustomization = false;  // Legacy flag for UI display

        // AI State
        private string aiResponse = "";
        private string lastPromptHistoryEntryId = "";  // Track the last entry for marking as applied
        private string beforeStateSnapshot = "";  // Captured before generating for comparison
        private string statusMessage = "";
        private MessageType statusType = MessageType.None;
        private bool isProcessing = false;
        private bool autoApply = false;
        private RCCP_AIConfig.PromptMode promptMode = RCCP_AIConfig.PromptMode.Request;  // Ask vs Request mode
        private bool showPreview = false;
        private bool changesApplied = false;  // Track if current response has been applied
        private string[] lastRejectionSuggestions = null;  // Suggestions from AI when request is rejected

        // Per-panel state storage (preserves response when switching tabs)
        private class PanelState {
            public string userPrompt = "";
            public string aiResponse = "";
            public string beforeStateSnapshot = "";
            public bool showPreview = false;
            public bool changesApplied = false;
        }
        private Dictionary<int, PanelState> panelStates = new Dictionary<int, PanelState>();

        // Pending apply target - stores the vehicle that was selected when response was generated
        // This prevents issues when user changes selection before clicking Apply
        private GameObject pendingApplyVehicle = null;
        private RCCP_CarController pendingApplyController = null;
        private EligibilityCheck pendingEligibility = null;  // Stores wheel detection for vehicle creation

        // Request timeout handling - uses server timeout from settings
        private float RequestTimeoutSeconds =>
            RCCP_AISettings.Instance != null ? RCCP_AISettings.Instance.serverTimeout : 120f;
        private float requestStartTime = 0f;
        private EditorCoroutine currentRequestCoroutine = null;

        // Retry logic
        private const int MAX_RETRY_COUNT = 3;
        private const float RETRY_DELAY_SECONDS = 2f;
        private int currentRetryCount = 0;

        // Batch processing delay - respects server rate limit (server enforces 2s between requests)
        // Use 2.5s for safety margin to account for network latency variance
        private const float BATCH_REQUEST_DELAY_SECONDS = 2.5f;

        // Post-creation refinement state (auto-chain creation → customization)
        private bool isRefinementPending = false;
        private string pendingRefinementPrompt = null;
        private bool isExecutingRefinement = false;
        private int refinementRetryCount = 0;
        private const int MaxRefinementRetries = 10;

        private struct RefinementRestoreInfo {
            public int panelIndex;
            public string userPrompt;
            public string aiResponse;
        }
        private RefinementRestoreInfo? refinementRestoreInfo;

        // Diagnostics State
        private List<RCCP_AIVehicleDiagnostics.DiagnosticResult> diagnosticResults;
        private Vector2 diagnosticsScrollPosition;
        private bool showInfoMessages = true;
        private bool showWarningMessages = true;
        private bool showErrorMessages = true;

        // Quick Prompt Shuffle
        private int quickPromptDisplayCount = 5;  // Configurable via settings (0-25)
        private const int QUICK_PROMPT_MIN = 0;
        private const int QUICK_PROMPT_MAX = 25;
        private List<int> displayedQuickPromptIndices = new List<int>();
        private HashSet<int> usedQuickPromptIndices = new HashSet<int>();
        private int lastPromptIndexForShuffle = -1;

        // Quick Prompts UI
        private bool quickPromptsFoldout = true;
        private bool showRecentPrompts = false;
        private List<string> recentPrompts = new List<string>();
        private const int MAX_RECENT_PROMPTS = 5;

        // Mesh Analysis
        private string meshAnalysis = "";
        private bool meshAnalysisFoldout = false;
        private Vector2 meshAnalysisScrollPosition = Vector2.zero;

        // Vehicle Creation Mode Toggle (Quick Create vs Custom Prompt)
        private bool useQuickCreateMode = true;

        // Vision-based Light Detection
        private bool isVisionDetecting = false;
        private RCCP_AIVisionLightDetector_V2.DetectionResult visionDetectionResultV2 = null;

        // Help tooltips
        private static readonly Dictionary<string, string> StepTooltips = new Dictionary<string, string> {
        { "Select Target", "Choose the 3D model or existing RCCP vehicle you want to configure. For new vehicles, always select the ROOT GameObject of the model in the scene hierarchy." },
        { "Describe What You Want", "Use natural language to describe the vehicle configuration. Be specific about performance, handling, and behavior characteristics." },
        { "Review & Apply", "Review the AI-generated configuration before applying. You can see a comparison of changes and apply or discard them." },
        { "Select Wheels", "Assign wheel transforms for front and rear axles. Click each button and select the corresponding wheel from the hierarchy." },
        { "Body Colliders", "Add physics colliders to the vehicle body for realistic collision detection and damage simulation." }
    };

        // Settings panel
        private string apiKey = "";
        private bool showApiKey = false;
        private bool developerMode = false;

        /// <summary>
        /// Returns true if we have valid authentication - either a local API key OR server proxy is enabled.
        /// </summary>
        private bool HasValidAuth => !string.IsNullOrEmpty(apiKey) ||
            (RCCP_AISettings.Instance?.useServerProxy ?? false);
        private bool forceRepaint = false;
        private bool hasSeenWelcome = false;

        // Settings panel - API validation
        private enum ApiValidationState { Unknown, Validating, Valid, Invalid }
        private ApiValidationState apiValidationState = ApiValidationState.Unknown;
        private string apiValidationMessage = "";
        private double lastApiRequestTime = 0;

        // Settings panel - Section foldouts
        private bool foldoutApiConfig = false;
        private bool foldoutPromptAssets = false;
        private bool foldoutUISettings = false;
        private bool foldoutWelcomeHelp = false;
        private bool foldoutAnimSettings = false;
        private bool foldoutShortcuts = false;
        private bool foldoutDevOptions = false;

        // Server Proxy testing state
        private enum ServerTestState { Unknown, Testing, Success, Failed }
        private ServerTestState serverTestState = ServerTestState.Unknown;
        private string serverTestMessage = "";

        // Animation Settings
        private bool enableAnimations = true;
        private float animationSpeed = 1.0f;  // 0.5 = slow, 1.0 = normal, 2.0 = fast
        private const float ANIMATION_SPEED_MIN = 0.5f;
        private const float ANIMATION_SPEED_MAX = 2.0f;

        // Animation State
        private float panelTransitionAlpha = 1f;
        private float panelTransitionTarget = 1f;
        private int lastPanelIndex = -1;
        private float responseAppearAlpha = 0f;
        private float responseAppearTarget = 0f;
        private string lastAiResponse = "";
        private float stepAnimationProgress = 1f;
        private float statusMessageAlpha = 0f;
        private float statusMessageTarget = 0f;
        private double statusMessageStartTime = 0;
        private const float STATUS_FADE_DURATION = 3f;  // Seconds before status fades
        private float processingPulse = 0f;  // For pulsing effect during processing
        private float settingsPanelAlpha = 0f;
        private float historyPanelAlpha = 0f;
        private float successFlashAlpha = 0f;  // For success flash effect after applying
        private double lastAnimationUpdateTime = 0;  // For calculating real deltaTime
        private double quickPromptInsertTime = 0;  // For brief visual feedback when quick prompt inserted

        // Colors - Using design system (DS = Design System alias for readability)
        private static class DS {
            public static Color Accent => RCCP_AIDesignSystem.Colors.AccentPrimary;
            public static Color AccentMuted => RCCP_AIDesignSystem.Colors.AccentMuted;
            public static Color BgBase => RCCP_AIDesignSystem.Colors.GetBgBase();
            public static Color BgElevated => RCCP_AIDesignSystem.Colors.BgElevated;
            public static Color BgRecessed => RCCP_AIDesignSystem.Colors.BgRecessed;
            public static Color BgHover => RCCP_AIDesignSystem.Colors.BgHover;
            public static Color BgSelected => RCCP_AIDesignSystem.Colors.BgSelected;
            public static Color TextPrimary => RCCP_AIDesignSystem.Colors.GetTextPrimary();
            public static Color TextSecondary => RCCP_AIDesignSystem.Colors.GetTextSecondary();
            public static Color TextDisabled => RCCP_AIDesignSystem.Colors.TextDisabled;
            public static Color Success => RCCP_AIDesignSystem.Colors.Success;
            public static Color Warning => RCCP_AIDesignSystem.Colors.Warning;
            public static Color Error => RCCP_AIDesignSystem.Colors.Error;
            public static Color AiSuggestion => RCCP_AIDesignSystem.Colors.AiSuggestion;
            public static Color AiPreview => RCCP_AIDesignSystem.Colors.AiPreview;
            public static Color Border => RCCP_AIDesignSystem.Colors.BorderDefault;
            public static Color BorderLight => RCCP_AIDesignSystem.Colors.BorderLight;
        }

        // Legacy color aliases (for backward compatibility during migration)
        private static Color AccentColor => DS.Accent;
        private static Color SidebarBgColor => RCCP_AIDesignSystem.Colors.BgBase;
        private static Color ContentBgColor => RCCP_AIDesignSystem.Colors.BgElevated;
        private static Color StepActiveColor => RCCP_AIDesignSystem.Colors.WithAlpha(DS.Accent, 0.3f);
        private static Color StepCompleteColor => RCCP_AIDesignSystem.Colors.WithAlpha(DS.Success, 0.3f);
        private static Color ButtonHoverColor => DS.BgHover;

        // Styles - now using design system (cached internally by RCCP_AIDesignSystem)
        private GUIStyle headerStyle => RCCP_AIDesignSystem.LabelWindowTitle;
        private GUIStyle sidebarButtonStyle => RCCP_AIDesignSystem.SidebarItem;
        private GUIStyle sidebarButtonActiveStyle => RCCP_AIDesignSystem.SidebarItemActive;
        private GUIStyle stepBoxStyle => RCCP_AIDesignSystem.PanelElevated;
        private GUIStyle stepNumberStyle => _stepNumberStyle;
        private GUIStyle chipStyle => RCCP_AIDesignSystem.ButtonSmall;
        private GUIStyle previewBoxStyle => RCCP_AIDesignSystem.PanelRecessed;
        private GUIStyle statusBarStyle => _statusBarStyle;

        // Custom styles that need special configuration
        private GUIStyle _stepNumberStyle;
        private GUIStyle _statusBarStyle;
        private bool stylesInitialized = false;

        // Custom GUISkin support
        private GUISkin cachedCustomSkin;
        private GUISkin originalSkin;

        // Cached textures for OnGUI (avoid creating new textures every frame)
        private Texture2D chipExactMatchTexture;
        private Texture2D chipPartialMatchTexture;
        private Texture2D jsonPreviewTexture;

        #endregion

        #region Menu Items

        [MenuItem("Tools/BoneCracker Games/RCCP AI Assistant/Open Assistant", false, 40)]
        public static void ShowWindow() {
            // utility: true = fixed-size floating window, false = dockable resizable window
            bool useUtility = RCCP_AIEditorPrefs.UseUtilityWindow;
            var window = GetWindow<RCCP_AIAssistantWindow>(useUtility, "RCCP AI Assistant");
        }

        /// <summary>
        /// Closes and reopens the window to refresh its state.
        /// </summary>
        private void RestartWindow() {
            // Confirm before restarting
            if (!EditorUtility.DisplayDialog(
                "Restart RCCP AI Assistant",
                "This will close and reopen the window.\n\nAny unsaved AI responses will be lost.\n\nContinue?",
                "Restart",
                "Cancel")) {
                return;
            }

            // Store current position (x,y only - size is fixed by OnEnable)
            Vector2 windowPos = position.position;

            // Close this window
            Close();

            // Reopen after a short delay to ensure clean state
            EditorApplication.delayCall += () => {
                bool useUtility = RCCP_AIEditorPrefs.UseUtilityWindow;
                var window = GetWindow<RCCP_AIAssistantWindow>(useUtility, "RCCP AI Assistant");
                // Only restore position, not size (OnEnable sets size based on mode)
                window.position = new Rect(windowPos.x, windowPos.y, WindowSize.x, WindowSize.y);
            };
        }

        #endregion

        #region Unity Callbacks

        // Window size constant - used in OnEnable
        private static readonly Vector2 WindowSize = new Vector2(900, 800);

        private void OnEnable() {
            // Set window size constraints based on mode
            minSize = WindowSize;
            // Only lock maxSize in utility mode (fixed size)
            if (RCCP_AIEditorPrefs.UseUtilityWindow) {
                maxSize = WindowSize;
            }

            LoadSettings();
            LoadApiKey();
            RefreshSelection();
            Selection.selectionChanged += OnSelectionChanged;

            // Subscribe to editor update for smooth animations (60fps)
            EditorApplication.update += OnEditorUpdate;
            lastAnimationUpdateTime = EditorApplication.timeSinceStartup;

            // Subscribe to Play mode changes to prevent entering Play mode during processing
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Initialize review panel for structured change preview
            InitializeReviewPanel();

            // Check if we should show welcome panel
            CheckShowWelcome();
        }

        private void CheckShowWelcome() {
            if (settings != null && settings.showWelcomeOnStartup && !hasSeenWelcome) {
                showWelcome = true;
            }
        }

        private void OnDisable() {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            // Cancel any active API request coroutine to prevent orphaned coroutines
            if (currentRequestCoroutine != null) {
                EditorCoroutineUtility.StopCoroutine(currentRequestCoroutine);
                currentRequestCoroutine = null;
            }

            // Reset processing state to avoid stale state on window reopen
            isProcessing = false;
            isBatchProcessing = false;
        }

        /// <summary>
        /// Handles play mode state changes:
        /// - Prevents entering Play mode while an AI request is processing
        /// - Refreshes selection when entering/exiting play mode to avoid stale references
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state) {
            // Block entering Play mode while processing
            if ((isProcessing || isBatchProcessing) && state == PlayModeStateChange.ExitingEditMode) {
                EditorApplication.isPlaying = false;
                EditorUtility.DisplayDialog(
                    "RCCP AI Assistant",
                    "Cannot enter Play Mode while an AI request is processing.\n\nPlease wait for the request to complete or cancel it.",
                    "OK"
                );
                return;
            }

            // Clear cached references during play mode transitions to avoid stale references
            // This is necessary because Unity destroys and recreates scene objects when entering/exiting play mode
            switch (state) {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.ExitingPlayMode:
                // Clear all cached references before the transition
                // The objects are about to be destroyed
                ClearSelectionState();
                // Repaint immediately so UI reflects cleared state
                Repaint();
                break;

                case PlayModeStateChange.EnteredEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                // After transition completes, refresh from the current Selection
                // Use delayCall to ensure Unity has finished its internal state updates
                EditorApplication.delayCall += () => {
                    RefreshSelection();
                    Repaint();
                };
                break;
            }
        }

        /// <summary>
        /// Clears all cached selection state to avoid stale references.
        /// Called during play mode transitions.
        /// </summary>
        private void ClearSelectionState() {
            selectedVehicle = null;
            selectedController = null;
            isSelectionInScene = false;
            hasExistingRigidbodies = false;
            existingRigidbodyCount = 0;
            hasExistingWheelColliders = false;
            existingWheelColliderCount = 0;
            isPrefabInstance = false;
            isVehicleInactive = false;
            lastKnownActiveState = false;
            lastKnownScale = Vector3.one;
            meshAnalysis = "";
            currentSizeWarnings.Clear();
            currentEligibility = null;

            // Clear batch state as well
            if (!isBatchProcessing) {
                batchVehicles.Clear();
                batchMeshAnalysis.Clear();
                batchResponses.Clear();
                batchSizeWarnings.Clear();
                batchEligibility.Clear();
            }
        }

        /// <summary>
        /// Called at editor frame rate (~60fps) for smooth animations.
        /// </summary>
        private void OnEditorUpdate() {
            // Check if selected object's active state changed (handles enable/disable while selected)
            CheckSelectedObjectStateChanged();

            // Check for request timeout (moved from dead OnInspectorGUI)
            CheckRequestTimeout();

            // Determine if we should repaint based on window state
            bool shouldRepaint = forceRepaint || ShouldRepaintWindow();

            // Update animations and only repaint if values actually changed
            if (enableAnimations) {
                bool needsRepaint = UpdateAnimations();
                if (needsRepaint || shouldRepaint) {
                    Repaint();
                }
            } else if (isProcessing || shouldRepaint) {
                // Repaint for non-animation updates (processing state, etc.)
                Repaint();
            }
        }

        /// <summary>
        /// Checks if the selected object's active state has changed since last check.
        /// This handles the case where user enables/disables a vehicle while it's selected.
        /// Selection.selectionChanged only fires when the selection itself changes, not when
        /// the selected object's properties change.
        /// </summary>
        private void CheckSelectedObjectStateChanged() {
            GameObject selected = Selection.activeGameObject;

            // Handle Unity's fake null during play mode transitions
            if (selected != null && !selected) {
                selected = null;
            }

            if (selected != null) {
                bool currentActiveState = selected.activeInHierarchy;
                Vector3 currentScale = selected.transform.lossyScale;
                bool stateChanged = currentActiveState != lastKnownActiveState;
                bool scaleChanged = currentScale != lastKnownScale;

                if (stateChanged || scaleChanged) {
                    lastKnownActiveState = currentActiveState;
                    lastKnownScale = currentScale;
                    RefreshSelection();
                    Repaint();
                }
            } else {
                lastKnownActiveState = false;
                lastKnownScale = Vector3.one;
            }
        }

        /// <summary>
        /// Determines if window should repaint based on focus/hover state.
        /// Used when forceRepaint is disabled to still allow responsive UI.
        /// </summary>
        private bool ShouldRepaintWindow() {
            // Always repaint if window is focused
            if (focusedWindow == this) return true;

            // Repaint if mouse is over the window
            if (mouseOverWindow == this) return true;

            return false;
        }

        /// <summary>
        /// Updates all animation states. Returns true if any value changed (repaint needed).
        /// </summary>
        private bool UpdateAnimations() {
            bool needsRepaint = false;
            const float epsilon = 0.001f;

            // Calculate real deltaTime for frame-rate independent animations
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - lastAnimationUpdateTime);
            lastAnimationUpdateTime = currentTime;

            // Clamp deltaTime to avoid large jumps (e.g., after pause or first frame)
            deltaTime = Mathf.Clamp(deltaTime, 0.001f, 0.1f);

            float speed = animationSpeed * 8f;  // Base animation speed multiplier

            // Panel transition fade
            float newPanelAlpha = Mathf.MoveTowards(panelTransitionAlpha, panelTransitionTarget, deltaTime * speed);
            if (Mathf.Abs(newPanelAlpha - panelTransitionAlpha) > epsilon) needsRepaint = true;
            panelTransitionAlpha = newPanelAlpha;

            // Response appear animation
            float newResponseAlpha = Mathf.MoveTowards(responseAppearAlpha, responseAppearTarget, deltaTime * speed);
            if (Mathf.Abs(newResponseAlpha - responseAppearAlpha) > epsilon) needsRepaint = true;
            responseAppearAlpha = newResponseAlpha;

            // Step animation progress
            float newStepProgress = Mathf.MoveTowards(stepAnimationProgress, 1f, deltaTime * speed);
            if (Mathf.Abs(newStepProgress - stepAnimationProgress) > epsilon) needsRepaint = true;
            stepAnimationProgress = newStepProgress;

            // Status message fade (auto-fade after duration)
            if (statusMessageTarget > 0f && EditorApplication.timeSinceStartup - statusMessageStartTime > STATUS_FADE_DURATION) {
                statusMessageTarget = 0f;
            }
            float newStatusAlpha = Mathf.MoveTowards(statusMessageAlpha, statusMessageTarget, deltaTime * speed * 0.5f);
            if (Mathf.Abs(newStatusAlpha - statusMessageAlpha) > epsilon) needsRepaint = true;
            statusMessageAlpha = newStatusAlpha;

            // Processing pulse effect (sine wave) - always needs repaint while processing
            if (isProcessing) {
                processingPulse = (Mathf.Sin((float)EditorApplication.timeSinceStartup * 4f) + 1f) * 0.5f;
                needsRepaint = true;
            } else {
                float newPulse = Mathf.MoveTowards(processingPulse, 0f, deltaTime * speed);
                if (Mathf.Abs(newPulse - processingPulse) > epsilon) needsRepaint = true;
                processingPulse = newPulse;
            }

            // Settings/History/Welcome panel transitions
            float newSettings = Mathf.MoveTowards(settingsPanelAlpha, showSettings ? 1f : 0f, deltaTime * speed);
            if (Mathf.Abs(newSettings - settingsPanelAlpha) > epsilon) needsRepaint = true;
            settingsPanelAlpha = newSettings;

            float newHistory = Mathf.MoveTowards(historyPanelAlpha, (showHistory || showPromptHistory) ? 1f : 0f, deltaTime * speed);
            if (Mathf.Abs(newHistory - historyPanelAlpha) > epsilon) needsRepaint = true;
            historyPanelAlpha = newHistory;

            float newWelcome = Mathf.MoveTowards(welcomePanelAlpha, showWelcome ? 1f : 0f, deltaTime * speed);
            if (Mathf.Abs(newWelcome - welcomePanelAlpha) > epsilon) needsRepaint = true;
            welcomePanelAlpha = newWelcome;

            // Success flash fade out
            float newFlash = Mathf.MoveTowards(successFlashAlpha, 0f, deltaTime * speed * 1.5f);
            if (Mathf.Abs(newFlash - successFlashAlpha) > epsilon) needsRepaint = true;
            successFlashAlpha = newFlash;

            // Detect panel change for transition
            if (lastPanelIndex != currentPromptIndex) {
                panelTransitionTarget = 0f;
                panelTransitionAlpha = 0f;
                lastPanelIndex = currentPromptIndex;
                // Trigger fade-in after brief fade-out
                EditorApplication.delayCall += () => {
                    panelTransitionTarget = 1f;
                };
                needsRepaint = true;
            }

            // Detect new AI response (single vehicle mode)
            if (aiResponse != lastAiResponse) {
                if (!string.IsNullOrEmpty(aiResponse) && string.IsNullOrEmpty(lastAiResponse)) {
                    // New response appeared - trigger fade in
                    responseAppearAlpha = 0f;
                    responseAppearTarget = 1f;
                }
                lastAiResponse = aiResponse;
                needsRepaint = true;
            }

            // Detect new batch customization responses (multi-vehicle mode)
            // Trigger fade-in when batch responses appear
            if (isBatchCustomization && batchCustomizationResponses.Count > 0 && responseAppearTarget == 0f) {
                responseAppearAlpha = 0f;
                responseAppearTarget = 1f;
                needsRepaint = true;
            }

            return needsRepaint;
        }

        /// <summary>
        /// Helper to get animated alpha value for GUI elements
        /// </summary>
        private float GetAnimatedAlpha(float baseAlpha, float animationAlpha) {
            return enableAnimations ? baseAlpha * animationAlpha : baseAlpha;
        }

        /// <summary>
        /// Triggers a status message with optional auto-fade
        /// </summary>
        private void ShowAnimatedStatus(string message, MessageType type) {
            SetStatus(message, type);
            if (enableAnimations) {
                statusMessageAlpha = 1f;
                statusMessageTarget = 1f;
                statusMessageStartTime = EditorApplication.timeSinceStartup;
            }
        }

        private void OnSelectionChanged() {
            // Skip if utility is doing a programmatic deselect/reselect cycle
            // This prevents the race condition where selection is temporarily null
            if (isRefreshingSelection) return;

            RefreshSelection();
            Repaint();
        }

        private void InitializeStyles() {
            if (stylesInitialized) return;

            // Step number badge - custom style not in design system
            _stepNumberStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
                fontStyle = FontStyle.Bold,
                normal = { textColor = DS.Accent },
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = 28,
                fixedHeight = 28
            };

            // Status bar at bottom - custom style
            _statusBarStyle = new GUIStyle() {
                padding = RCCP_AIDesignSystem.Spacing.HV(
                    RCCP_AIDesignSystem.Spacing.PanelPadding,
                    RCCP_AIDesignSystem.Spacing.Space4
                )
            };
            _statusBarStyle.normal.background = RCCP_AIDesignSystem.GetTexture(
                RCCP_AIDesignSystem.Colors.Darken(DS.BgBase, 0.05f)
            );

            // Initialize cached textures for OnGUI usage
            chipExactMatchTexture = RCCP_AIDesignSystem.GetTexture(
                RCCP_AIDesignSystem.Colors.WithAlpha(DS.Success, 0.8f)
            );
            chipPartialMatchTexture = RCCP_AIDesignSystem.GetTexture(
                RCCP_AIDesignSystem.Colors.WithAlpha(DS.Warning, 0.6f)
            );
            jsonPreviewTexture = RCCP_AIDesignSystem.GetTexture(DS.BgRecessed);

            stylesInitialized = true;
        }

        private Texture2D MakeColorTexture(Color color) {
            // Delegate to design system for caching
            return RCCP_AIDesignSystem.GetTexture(color);
        }

        /// <summary>
        /// Handles keyboard shortcuts for the window
        /// </summary>
        private void HandleKeyboardShortcuts() {
            Event e = Event.current;
            if (e == null) return;

            bool sendOnEnter = settings != null && settings.sendOnEnter;
            bool isEnter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            bool isModifierDown = e.control || e.command;
            bool isShiftDown = e.shift;

            bool shouldGenerate = false;

            if (e.type == EventType.KeyDown && isEnter) {
                if (sendOnEnter) {
                    // If sendOnEnter is TRUE: Enter sends, Shift+Enter adds new line
                    if (isShiftDown) {
                        // Insert newline at cursor position using Unity's TextEditor
                        var textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                        if (textEditor != null && EditorGUIUtility.editingTextField) {
                            textEditor.Insert('\n');
                            userPrompt = textEditor.text;
                            e.Use();
                            Repaint();
                            return;
                        }
                    } else {
                        shouldGenerate = true;
                    }
                } else {
                    // If sendOnEnter is FALSE: Ctrl+Enter sends
                    if (isModifierDown) {
                        shouldGenerate = true;
                    }
                }
            }

            if (shouldGenerate) {
                // Check if we can generate
                bool canGenerate = !isProcessing &&
                                  !string.IsNullOrEmpty(userPrompt) &&
                                  HasValidAuth &&
                                  !showSettings &&
                                  !showHistory &&
                                  !showPromptHistory;

                if (CurrentPrompt != null) {
                    if (CurrentPrompt.requiresVehicle && selectedVehicle == null) canGenerate = false;
                    if (CurrentPrompt.requiresRCCPController && !HasRCCPController) canGenerate = false;
                }

                if (canGenerate) {
                    Generate();
                    e.Use(); // Consume the event
                }
            }

            // Escape to close dropdowns or go back
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) {
                if (showRecentPrompts) {
                    showRecentPrompts = false;
                    e.Use();
                } else if (showWelcome) {
                    CloseWelcomePanel();
                    e.Use();
                } else if (showSettings) {
                    showSettings = false;
                    e.Use();
                } else if (showHistory) {
                    showHistory = false;
                    e.Use();
                } else if (showPromptHistory) {
                    showPromptHistory = false;
                    e.Use();
                }
            }
        }

        private void OnGUI() {
            // Apply custom GUISkin if available (with null check)
            ApplyCustomSkin();

            try {
                // Handle keyboard shortcuts
                HandleKeyboardShortcuts();

                InitializeStyles();

                if (showWelcome) {
                    DrawWelcomePanel();
                    return;
                }

                // Check if verification is required BEFORE showing main UI
                // This blocks access to AI features until verified (unless using own API key)
                if (ShouldShowVerificationPanel()) {
                    DrawVerificationPanel();
                    return;
                }

                if (showSettings) {
                    DrawSettingsPanel();
                    return;
                }

                if (showHistory) {
                    DrawHistoryPanel();
                    return;
                }

                if (showPromptHistory) {
                    DrawPromptHistoryPanel();
                    return;
                }

                EditorGUILayout.BeginHorizontal();

                // Sidebar
                DrawSidebar();

                // Main Content
                EditorGUILayout.BeginVertical();
                DrawHeader();

                if (settings == null || availablePrompts == null || availablePrompts.Length == 0) {
                    DrawNoSettingsWarning();
                } else {
                    // Apply panel transition fade animation
                    Color originalColor = GUI.color;
                    if (enableAnimations) {
                        GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, panelTransitionAlpha);
                    }

                    // Cancel scroll animation if user manually scrolls
                    if (scrollTargetY >= 0 && Event.current.type == EventType.ScrollWheel) {
                        scrollTargetY = -1f;  // User took control, cancel animation
                    }

                    // Smooth scroll animation
                    if (scrollTargetY >= 0) {
                        float previousY = mainScrollPosition.y;
                        // For scroll-to-bottom (float.MaxValue), use a large but reasonable target
                        float effectiveTarget = scrollTargetY > 100000f ? mainScrollPosition.y + 5000f : scrollTargetY;
                        mainScrollPosition.y = Mathf.Lerp(mainScrollPosition.y, effectiveTarget, SCROLL_ANIMATION_SPEED * 0.016f); // ~60fps

                        // Animation complete when: reached target OR scroll position stopped changing (hit bottom)
                        bool reachedTarget = Mathf.Abs(mainScrollPosition.y - scrollTargetY) < 1f;
                        bool stoppedMoving = Mathf.Abs(mainScrollPosition.y - previousY) < 0.5f && mainScrollPosition.y > 10f;

                        if (reachedTarget || (scrollTargetY > 100000f && stoppedMoving)) {
                            scrollTargetY = -1f;  // Animation complete
                        } else {
                            Repaint();  // Keep animating
                        }
                    }

                    // Draw sticky context bar outside scroll view
                    DrawContextBar();

                    Vector2 scrollBefore = mainScrollPosition;
                    mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawWorkflow();
                    EditorGUILayout.EndScrollView();

                    // Cancel animation if user manually dragged scrollbar (scroll changed in unexpected direction)
                    if (scrollTargetY >= 0 && scrollBefore.y > mainScrollPosition.y + 5f) {
                        scrollTargetY = -1f;  // User scrolled up, cancel scroll-to-bottom
                    }

                    // === DOCKED FOOTER (outside scroll view) ===
                    // Only show when we have an AI response to apply
                    if (ShouldShowDockedFooter()) {
                        DrawDockedApplyFooter();
                    }

                    // Restore original color
                    if (enableAnimations) {
                        GUI.color = originalColor;
                    }
                }

                // Status Bar - now part of layout system (inside the vertical)
                DrawStatusBarLayoutBased();

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }
            finally {
                // Always restore original skin, even if early return or exception
                RestoreOriginalSkin();
            }
        }

        /// <summary>
        /// Applies the custom GUISkin from settings if available.
        /// Stores the original skin for restoration.
        /// </summary>
        private void ApplyCustomSkin() {
            originalSkin = GUI.skin;

            // Cache the custom skin reference from settings (with null checks)
            GUISkin newSkin = (settings != null) ? settings.customSkin : null;

            // Detect if skin has changed
            if (newSkin != cachedCustomSkin) {
                cachedCustomSkin = newSkin;
                stylesInitialized = false;  // Force style re-initialization
            }

            // Apply custom skin if available
            if (cachedCustomSkin != null) {
                GUI.skin = cachedCustomSkin;
            }
        }

        /// <summary>
        /// Restores the original GUISkin after rendering.
        /// </summary>
        private void RestoreOriginalSkin() {
            if (originalSkin != null) {
                GUI.skin = originalSkin;
            }
        }

        #endregion

        #region Initialization

        private void LoadSettings() {
            settings = Resources.Load<RCCP_AISettings>("RCCP_AISettings");

            // Auto-create assets if settings is null or prompts are missing
            if (settings == null || settings.prompts == null || settings.prompts.Length == 0) {
                RCCP_AIInitLoad.CreateDefaultAssets();
                settings = Resources.Load<RCCP_AISettings>("RCCP_AISettings");
            }

            if (settings != null) {
                availablePrompts = settings.GetValidPrompts();
            } else {
                availablePrompts = null;
            }
        }

        private void LoadApiKey() {
            // Load all preferences from centralized RCCP_AIEditorPrefs
            apiKey = RCCP_AIEditorPrefs.ApiKey;
            developerMode = RCCP_AIEditorPrefs.DeveloperMode;
            forceRepaint = RCCP_AIEditorPrefs.ForceRepaint;
            hasSeenWelcome = RCCP_AIEditorPrefs.HasSeenWelcome;
            quickPromptDisplayCount = RCCP_AIEditorPrefs.QuickPromptCount;
            quickPromptDisplayCount = Mathf.Clamp(quickPromptDisplayCount, QUICK_PROMPT_MIN, QUICK_PROMPT_MAX);

            // Load animation settings
            enableAnimations = RCCP_AIEditorPrefs.EnableAnimations;
            animationSpeed = RCCP_AIEditorPrefs.AnimationSpeed;
            animationSpeed = Mathf.Clamp(animationSpeed, ANIMATION_SPEED_MIN, ANIMATION_SPEED_MAX);

            // Load prompt mode (Ask vs Request)
            promptMode = (RCCP_AIConfig.PromptMode)RCCP_AIEditorPrefs.PromptMode;

            // Load Quick Create mode setting for Vehicle Creation
            useQuickCreateMode = RCCP_AIEditorPrefs.UseQuickCreateMode;

            // Load settings panel foldout states
            foldoutApiConfig = RCCP_AIEditorPrefs.FoldoutApiConfig;
            foldoutPromptAssets = RCCP_AIEditorPrefs.FoldoutPromptAssets;
            foldoutUISettings = RCCP_AIEditorPrefs.FoldoutUISettings;
            foldoutWelcomeHelp = RCCP_AIEditorPrefs.FoldoutWelcomeHelp;
            foldoutAnimSettings = RCCP_AIEditorPrefs.FoldoutAnimSettings;
            foldoutShortcuts = RCCP_AIEditorPrefs.FoldoutShortcuts;
            foldoutDevOptions = RCCP_AIEditorPrefs.FoldoutDevOptions;

            // Sync useServerProxy with UseOwnApiKey (they should be inverses)
            var aiSettings = RCCP_AISettings.Instance;
            if (aiSettings != null) {
                bool shouldUseProxy = !RCCP_AIUtility.UseOwnApiKey;
                if (aiSettings.useServerProxy != shouldUseProxy) {
                    aiSettings.useServerProxy = shouldUseProxy;
                    EditorUtility.SetDirty(aiSettings);
                }
            }

            // Load saved panel selection
            int savedPanelIndex = RCCP_AIEditorPrefs.SelectedPanel;
            if (availablePrompts != null && savedPanelIndex >= 0 && savedPanelIndex < availablePrompts.Length) {
                currentPromptIndex = savedPanelIndex;
            }

            // Auto-apply should be enabled by default for Vehicle Creation panel only
            // Also force Configure mode for VehicleCreation (Ask mode not supported)
            if (availablePrompts != null && currentPromptIndex < availablePrompts.Length) {
                var prompt = availablePrompts[currentPromptIndex];
                if (prompt != null && prompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation) {
                    autoApply = true;
                    // Force Configure mode - Ask is not available for Vehicle Creation
                    promptMode = RCCP_AIConfig.PromptMode.Request;
                    RCCP_AIEditorPrefs.PromptMode = (int)promptMode;
                } else {
                    autoApply = false;
                }
            }
        }

        private void RefreshSelection() {
            GameObject selected = Selection.activeGameObject;

            // Safety check: Unity's destroyed objects can pass C# null check but fail Unity's null check
            // This can happen during play mode transitions when objects are being destroyed/recreated
            // Using the implicit bool conversion handles Unity's fake null properly
            if (selected != null && !selected) {
                selected = null;
            }

            // Check for multiple selection
            hasMultipleSelection = Selection.gameObjects != null && Selection.gameObjects.Length > 1;

            // Populate batch vehicles list for Vehicle Creation with multiple selection
            bool isVehicleCreationPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
            if (hasMultipleSelection && isVehicleCreationPanel && !isBatchProcessing) {
                batchVehicles.Clear();
                batchMeshAnalysis.Clear();
                batchSizeWarnings.Clear();
                batchEligibility.Clear();
                foreach (GameObject go in Selection.gameObjects) {
                    // Only include scene objects without RCCP_CarController (raw 3D models)
                    if (RCCP_AIUtility.IsSceneObject(go) && RCCP_AIUtility.GetRCCPController(go) == null) {
                        batchVehicles.Add(go);
                        // Pre-analyze mesh for each vehicle
                        batchMeshAnalysis[go] = AnalyzeMeshForObject(go);
                        // Calculate size warnings for each vehicle
                        batchSizeWarnings[go] = GetSizeWarnings(go);
                        // Run eligibility check for each vehicle
                        batchEligibility[go] = RunEligibilityCheck(go);

                        // Add root warning if object has a parent (Fix #6 - root validation)
                        if (go.transform.parent != null) {
                            batchSizeWarnings[go].Add(new SizeWarning(
                                SizeWarningLevel.Warning,
                                "Has parent - may not be vehicle root"
                            ));
                        }
                    }
                }
            } else if (!hasMultipleSelection) {
                // Clear batch state when single selection
                if (!isBatchProcessing) {
                    batchVehicles.Clear();
                    batchMeshAnalysis.Clear();
                    batchResponses.Clear();
                    batchSizeWarnings.Clear();
                    batchEligibility.Clear();
                }
            }

            // Populate batch customization list for VehicleCustomization and Lights with multiple selection
            bool isCustomizationPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCustomization;
            bool isLightsPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;
            bool supportsBatchMode = isCustomizationPanel || isLightsPanel;
            if (hasMultipleSelection && supportsBatchMode && !isBatchCustomization) {
                batchCustomizationVehicles.Clear();
                foreach (GameObject go in Selection.gameObjects) {
                    // Only include RCCP vehicles (existing controllers)
                    RCCP_CarController controller = RCCP_AIUtility.GetRCCPController(go);
                    // Deduplicate: child objects of the same car resolve to the same controller
                    if (controller != null && !batchCustomizationVehicles.Contains(controller)) {
                        batchCustomizationVehicles.Add(controller);
                    }
                }
                if (settings != null && settings.verboseLogging && batchCustomizationVehicles.Count > 0) {
                    Debug.Log($"[RCCP AI] RefreshSelection: Populated batch list with {batchCustomizationVehicles.Count} vehicles from {Selection.gameObjects.Length} selected objects");
                }
            } else if (!hasMultipleSelection && !isBatchCustomization) {
                // Clear customization batch state when single selection
                batchCustomizationVehicles.Clear();
            }
            // Debug: Log when batch list should show but doesn't
            if (settings != null && settings.verboseLogging && supportsBatchMode && Selection.gameObjects != null && Selection.gameObjects.Length > 1 && batchCustomizationVehicles.Count == 0) {
                Debug.LogWarning($"[RCCP AI] RefreshSelection: {Selection.gameObjects.Length} objects selected but batch list is empty. hasMultipleSelection={hasMultipleSelection}, isBatchCustomization={isBatchCustomization}");
            }

            if (selected != null) {
                isSelectionInScene = RCCP_AIUtility.IsSceneObject(selected);
                selectedController = RCCP_AIUtility.GetRCCPController(selected);

                if (HasRCCPController) {
                    selectedVehicle = selectedController.gameObject;
                    hasExistingRigidbodies = false;
                    existingRigidbodyCount = 0;
                    hasExistingWheelColliders = false;
                    existingWheelColliderCount = 0;
                    isPrefabInstance = false;
                    isVehicleInactive = !selectedVehicle.activeInHierarchy;
                    lastKnownActiveState = selectedVehicle.activeInHierarchy;
                    lastKnownScale = selectedVehicle.transform.lossyScale;
                } else {
                    selectedVehicle = selected;

                    Rigidbody[] rigidbodies = selected.GetComponentsInChildren<Rigidbody>(true);
                    existingRigidbodyCount = rigidbodies.Length;
                    hasExistingRigidbodies = existingRigidbodyCount > 0;

                    WheelCollider[] wheelColliders = selected.GetComponentsInChildren<WheelCollider>(true);
                    existingWheelColliderCount = wheelColliders.Length;
                    hasExistingWheelColliders = existingWheelColliderCount > 0;

                    isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(selected);
                    isVehicleInactive = !selected.activeInHierarchy;
                    lastKnownActiveState = selected.activeInHierarchy;
                    lastKnownScale = selected.transform.lossyScale;

                    // Calculate size warnings for single selection (only for raw models)
                    currentSizeWarnings = GetSizeWarnings(selected);

                    // Run eligibility check for single selection (Vehicle Creation only)
                    if (isVehicleCreationPanel) {
                        currentEligibility = RunEligibilityCheck(selected);
                    } else {
                        currentEligibility = null;
                    }
                }

                if (CurrentPrompt != null && CurrentPrompt.includeMeshAnalysis) {
                    AnalyzeMesh();
                }
            } else {
                selectedVehicle = null;
                selectedController = null;
                isSelectionInScene = false;
                hasExistingRigidbodies = false;
                existingRigidbodyCount = 0;
                hasExistingWheelColliders = false;
                existingWheelColliderCount = 0;
                isPrefabInstance = false;
                isVehicleInactive = false;
                lastKnownActiveState = false;
                lastKnownScale = Vector3.one;
                meshAnalysis = "";
                currentSizeWarnings.Clear();
                currentEligibility = null;
            }
        }

        /// <summary>
        /// Called by RCCP_AIUtility.RefreshSelection before deselecting.
        /// Prevents the window from clearing its state during programmatic refresh.
        /// </summary>
        public static void BeginRefreshSelection() {
            isRefreshingSelection = true;
        }

        /// <summary>
        /// Called by RCCP_AIUtility.RefreshSelection after reselecting.
        /// Allows normal selection change handling to resume.
        /// </summary>
        public static void EndRefreshSelection() {
            isRefreshingSelection = false;
            // Force refresh on all open windows
            var windows = Resources.FindObjectsOfTypeAll<RCCP_AIAssistantWindow>();
            foreach (var window in windows) {
                window.RefreshSelection();
                window.Repaint();
            }
        }

        /// <summary>
        /// Generates a summary line from mesh analysis for display in collapsed state
        /// </summary>
        private string GetMeshAnalysisSummary() {
            if (string.IsNullOrEmpty(meshAnalysis)) return "";

            // Try to extract size info
            string[] lines = meshAnalysis.Split('\n');
            foreach (string line in lines) {
                if (line.Contains("Size:")) {
                    // Extract just the dimensions
                    int startIdx = line.IndexOf("Size:");
                    if (startIdx >= 0) {
                        string sizeInfo = line.Substring(startIdx);
                        // Limit length
                        if (sizeInfo.Length > 30) sizeInfo = sizeInfo.Substring(0, 30);
                        return sizeInfo;
                    }
                }
            }

            // Fallback: count components mentioned
            int componentCount = 0;
            foreach (string line in lines) {
                if (line.Trim().StartsWith("RCCP_") || line.Trim().StartsWith("Model_")) {
                    componentCount++;
                }
            }
            return componentCount > 0 ? $"{componentCount} items" : "Click to expand";
        }

        /// <summary>
        /// Generates smart suggestions based on vehicle size
        /// </summary>
        private string GetSmartSuggestion() {
            if (string.IsNullOrEmpty(meshAnalysis)) return "";

            // Parse vehicle dimensions from mesh analysis
            float length = 0f;
            string[] lines = meshAnalysis.Split('\n');
            foreach (string line in lines) {
                if (line.Contains("Size:")) {
                    // Try to extract the longest dimension (length)
                    string[] parts = line.Split(new[] { 'x', 'X', '×' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in parts) {
                        string cleaned = new string(part.Where(c => char.IsDigit(c) || c == '.').ToArray());
                        if (float.TryParse(cleaned, out float val)) {
                            if (val > length) length = val;
                        }
                    }
                }
            }

            // Suggest based on size
            if (length > 8f) {
                return "cargo truck, delivery vehicle";
            } else if (length > 6f) {
                return "commercial van, SUV, pickup truck";
            } else if (length > 5f) {
                return "family sedan, station wagon";
            } else if (length > 4f) {
                return "sports car, compact car, racing car";
            } else if (length > 2f) {
                return "go-kart, ATV, compact vehicle";
            } else if (length > 0f) {
                return "small vehicle, prototype";
            }

            return "";
        }

        /// <summary>
        /// Adds a prompt to recent prompts history
        /// </summary>
        private void AddToRecentPrompts(string prompt) {
            if (string.IsNullOrEmpty(prompt)) return;

            // Remove if already exists (will re-add at top)
            recentPrompts.RemoveAll(p => p.Equals(prompt, StringComparison.OrdinalIgnoreCase));

            // Add to front
            recentPrompts.Insert(0, prompt);

            // Limit size
            if (recentPrompts.Count > MAX_RECENT_PROMPTS) {
                recentPrompts.RemoveAt(recentPrompts.Count - 1);
            }
        }

        #endregion

        #region UI Drawing - Status Bar

        /// <summary>
        /// Draws the status bar using the layout system (no absolute positioning).
        /// This prevents overlap issues with the docked footer.
        /// </summary>
        private void DrawStatusBarLayoutBased() {
            // Status bar container style
            GUIStyle containerStyle = new GUIStyle() {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(15, 15, 6, 6)
            };
            containerStyle.normal.background = RCCP_AIDesignSystem.GetTexture(
                RCCP_AIDesignSystem.Colors.Darken(DS.BgBase, 0.05f));

            EditorGUILayout.BeginHorizontal(containerStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.Container), GUILayout.ExpandWidth(true));

            if (!string.IsNullOrEmpty(statusMessage)) {
                Color msgColor = statusType == MessageType.Error ? DS.Error :
                                statusType == MessageType.Warning ? DS.Warning :
                                statusType == MessageType.Info ? DS.Success :
                                DS.TextSecondary;

                // Apply fade animation to status message
                if (enableAnimations) {
                    msgColor = RCCP_AIDesignSystem.Colors.WithAlpha(msgColor, statusMessageAlpha);
                }

                GUIStyle statusStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = msgColor },
                    wordWrap = true
                };
                GUILayout.Label(statusMessage, statusStyle);
            } else {
                GUILayout.Label("Ready", RCCP_AIDesignSystem.LabelSecondary);
            }

            GUILayout.FlexibleSpace();

            // Footer branding
            GUIStyle footerStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = DS.TextDisabled }
            };
            GUILayout.Label("RCCP AI Assistant", footerStyle);

            EditorGUILayout.EndHorizontal();
        }

        private void SetStatus(string message, MessageType type) {
            statusMessage = message;
            statusType = type;

            // Trigger animation for status message appearance
            if (enableAnimations && !string.IsNullOrEmpty(message)) {
                statusMessageAlpha = 1f;
                statusMessageTarget = 1f;
                statusMessageStartTime = EditorApplication.timeSinceStartup;
            }
        }

        /// <summary>
        /// Saves the current prompt and response to the global prompt history
        /// </summary>
        private void SaveToPromptHistory(string prompt, string response, bool wasAutoApplied) {
            if (CurrentPrompt == null) return;

            string vehicleName = "";
            // For batch customization, list all vehicle names
            if (isBatchCustomization && batchCustomizationVehicles.Count > 0) {
                var names = batchCustomizationVehicles
                    .Where(c => c != null)
                    .Select(c => c.gameObject.name)
                    .ToList();
                vehicleName = string.Join(", ", names);
            } else if (HasRCCPController) {
                vehicleName = selectedController.gameObject.name;
            } else if (selectedVehicle != null) {
                vehicleName = selectedVehicle.name;
            }

            // Determine if this is an informational query (no JSON config returned)
            bool isInformational = !response.TrimStart().StartsWith("{");

            // Store the entry ID so we can mark it as applied later if user manually applies
            lastPromptHistoryEntryId = RCCP_AIPromptHistory.AddEntry(
                panelType: CurrentPrompt.panelType.ToString(),
                panelName: CurrentPrompt.panelName,
                userPrompt: prompt,
                aiResponse: response,
                vehicleName: vehicleName,
                wasApplied: wasAutoApplied,
                isInformational: isInformational
            );
        }

        #endregion

    }

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
