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
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

    /// <summary>
    /// V2 Preview window for vision-based light detection.
    /// Shows captured images with detected positions and allows per-light offset adjustment.
    /// </summary>
    public class RCCP_AIVisionLightPreview_V2 : EditorWindow {

        #region State

        private RCCP_AIVisionLightDetector_V2.DetectionResult result;
        private Action<List<RCCP_AIVisionLightDetector_V2.DetectedLight>> onApply;
        private Action onCancel;

        private Vector2 scrollPosition;
        private Vector2 lightListScrollPosition;
        private int selectedLightIndex = -1;

        // Tabbed view system
        private enum ViewTab { Front, Rear, Side }
        private ViewTab currentViewTab = ViewTab.Front;
        private float imageZoom = .85f;
        private float gizmoSize = .5f;

        // Position scale - adjusts how normalized coordinates map to vehicle bounds
        // Useful when the vehicle doesn't fill the image exactly as expected
        private float positionScaleX = 1.0f;
        private float positionScaleY = 1.0f;

        // Workflow step tracking
        private enum WorkflowStep { Capture, Detect, Adjust, Apply }
        private WorkflowStep currentStep = WorkflowStep.Adjust;

        // Capture settings
        private float orthoSizeMultiplier = 1.0f;
        private bool isRecapturing = false;
        private string apiKey;  // Stored for re-capture

        /// <summary>
        /// Returns true if we have valid authentication for API calls.
        /// Either a local API key OR server proxy mode is enabled.
        /// </summary>
        private bool HasValidAuth => !string.IsNullOrEmpty(apiKey) ||
            (RCCP_AISettings.Instance?.useServerProxy ?? false);

        // Light pairing system
        private Dictionary<int, int> lightPairs = new Dictionary<int, int>(); // Maps light index to its pair index
        private bool symmetryEnabled = true;
        private float pairThreshold = 0.15f; // Distance threshold for pairing (in normalized coords)

        // Drag state for image markers
        private bool isDragging = false;
        private int draggingLightIndex = -1;
        private string draggingViewType = "";
        private Rect draggingImageRect;

        // Light group foldout states (for collapsible sections)
        private Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();

        // Colors - use design system where applicable
        private static readonly Color SelectedColor = new Color(1f, 1f, 0f, 1f);           // Yellow for gizmo selection
        private static Color EnabledColor => RCCP_AIDesignSystem.Colors.SuccessBg;
        private static Color DisabledColor => RCCP_AIDesignSystem.Colors.TextDisabled;
        private static Color PairHighlightColor => RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.AccentCyan, 0.4f);
        private static Color AccentBlue => RCCP_AIDesignSystem.Colors.AccentPrimary;
        private static Color AccentBlueSoft => RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.AccentPrimary, 0.18f);
        private static Color SurfaceDark => RCCP_AIDesignSystem.Colors.BgRecessed;
        private static Color SurfaceMid => RCCP_AIDesignSystem.Colors.BgBase;
        private static Color SurfaceBorder => RCCP_AIDesignSystem.Colors.BorderLight;
        private static Color MutedTextColor => RCCP_AIDesignSystem.Colors.TextMuted;
        private static readonly Color StepActiveColor = new Color(0.3f, 0.55f, 0.95f, 1f); // Workflow-specific
        private static readonly Color StepCompletedColor = new Color(0.25f, 0.3f, 0.35f, 1f);
        private static readonly Color StepPendingColor = new Color(0.5f, 0.5f, 0.55f, 1f);

        // Light type icons (unicode)
        private static readonly Dictionary<string, string> LightTypeIcons = new Dictionary<string, string> {
        { "headlight_low", "\u2600" },    // ☀ sun
        { "headlight_high", "\u2605" },   // ★ star
        { "brakelight", "\u25CF" },       // ● filled circle (red)
        { "taillight", "\u25CB" },        // ○ circle outline
        { "indicator", "\u25B6" },        // ▶ triangle
        { "reverse", "\u25A1" }           // □ square (white)
    };

        // Nudge amount for +/- buttons
        private const float NUDGE_AMOUNT = 0.01f;
        private const float NUDGE_AMOUNT_LARGE = 0.05f;

        // Developer mode - uses the same setting as the main AI Assistant window
        // Toggle via: AI Assistant Window > Settings > Developer Options
        private static bool developerMode => RCCP_AIEditorPrefs.DeveloperMode;

        #endregion

        #region Public API

        /// <summary>
        /// Opens the preview window with detection results.
        /// </summary>
        public static RCCP_AIVisionLightPreview_V2 Show(
            RCCP_AIVisionLightDetector_V2.DetectionResult detectionResult,
            Action<List<RCCP_AIVisionLightDetector_V2.DetectedLight>> applyCallback,
            Action cancelCallback = null,
            string storedApiKey = null) {

            var window = GetWindow<RCCP_AIVisionLightPreview_V2>("Light Detection Preview V2");
            window.result = detectionResult;
            window.onApply = applyCallback;
            window.onCancel = cancelCallback;
            window.apiKey = storedApiKey;
            window.orthoSizeMultiplier = detectionResult.orthoSizeMultiplier;
            window.Show();

            // Build light pairs after loading result
            window.BuildLightPairs();

            // Note: SceneView subscription is handled in OnEnable/OnDisable

            return window;
        }

        #endregion

        #region Light Pairing System

        /// <summary>
        /// Builds pairs of lights based on their type, side, and position.
        /// Lights on opposite sides (left/right) with the same type and similar Y/Z positions are considered pairs.
        /// </summary>
        private void BuildLightPairs() {
            lightPairs.Clear();

            if (result == null || result.lights == null || result.lights.Count == 0)
                return;

            for (int i = 0; i < result.lights.Count; i++) {
                // Skip if already paired
                if (lightPairs.ContainsKey(i))
                    continue;

                var lightA = result.lights[i];

                // Skip center lights - they don't have pairs
                if (lightA.side?.ToLower() == "center")
                    continue;

                // Find best matching pair
                int bestPairIndex = -1;
                float bestDistance = float.MaxValue;

                for (int j = i + 1; j < result.lights.Count; j++) {
                    // Skip if already paired
                    if (lightPairs.ContainsKey(j))
                        continue;

                    var lightB = result.lights[j];

                    // Check if types match (or are left/right indicators)
                    if (!AreTypesCompatibleForPairing(lightA, lightB))
                        continue;

                    // Must be on opposite sides
                    if (!AreOppositeSides(lightA.side, lightB.side))
                        continue;

                    // Must be from the same view (front or rear)
                    if (lightA.view != lightB.view)
                        continue;

                    // Calculate distance based on Y and Z positions (X should be mirrored, so we ignore it)
                    float yDiff = Mathf.Abs(lightA.normalizedY - lightB.normalizedY);
                    float zDiff = Mathf.Abs(lightA.normalizedZ - lightB.normalizedZ);
                    float distance = Mathf.Sqrt(yDiff * yDiff + zDiff * zDiff);

                    if (distance < bestDistance && distance < pairThreshold) {
                        bestDistance = distance;
                        bestPairIndex = j;
                    }
                }

                // Create bidirectional pair mapping
                if (bestPairIndex >= 0) {
                    lightPairs[i] = bestPairIndex;
                    lightPairs[bestPairIndex] = i;
                    Debug.Log($"[RCCP Vision V2] Paired: {lightA.lightType} ({lightA.side}) <-> {result.lights[bestPairIndex].lightType} ({result.lights[bestPairIndex].side})");
                }
            }

            Debug.Log($"[RCCP Vision V2] Found {lightPairs.Count / 2} light pairs");
        }

        /// <summary>
        /// Checks if two light types are compatible for pairing.
        /// Same types always match, and left/right indicators can pair with each other.
        /// </summary>
        private bool AreTypesCompatibleForPairing(RCCP_AIVisionLightDetector_V2.DetectedLight a, RCCP_AIVisionLightDetector_V2.DetectedLight b) {
            string typeA = a.lightType?.ToLower() ?? "";
            string typeB = b.lightType?.ToLower() ?? "";

            // Exact match
            if (typeA == typeB)
                return true;

            // Indicators are all "indicator" type but can be on different sides
            if (typeA == "indicator" && typeB == "indicator")
                return true;

            return false;
        }

        /// <summary>
        /// Checks if two sides are opposite (left vs right).
        /// </summary>
        private bool AreOppositeSides(string sideA, string sideB) {
            if (string.IsNullOrEmpty(sideA) || string.IsNullOrEmpty(sideB))
                return false;

            sideA = sideA.ToLower();
            sideB = sideB.ToLower();

            return (sideA == "left" && sideB == "right") || (sideA == "right" && sideB == "left");
        }

        /// <summary>
        /// Gets the pair index for a given light index, or -1 if no pair exists.
        /// </summary>
        private int GetPairIndex(int lightIndex) {
            if (lightPairs.TryGetValue(lightIndex, out int pairIndex))
                return pairIndex;
            return -1;
        }

        /// <summary>
        /// Checks if a light has a pair.
        /// </summary>
        private bool HasPair(int lightIndex) {
            return lightPairs.ContainsKey(lightIndex);
        }

        #endregion

        #region Lifecycle

        private void OnEnable() {
            minSize = new Vector2(1050, 690);

            // Unsubscribe first to prevent duplicate subscriptions
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable() {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnDestroy() {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void Update() {
            if (RCCP_AIEditorPrefs.ForceRepaint) {
                Repaint();
            }
        }

        #endregion

        #region Main GUI

        private void OnGUI() {
            if (result == null) {
                EditorGUILayout.HelpBox("No detection result available.", MessageType.Warning);
                if (GUILayout.Button("Close")) Close();
                return;
            }

            // Handle keyboard shortcuts
            HandleKeyboardInput();

            // Calculate fixed heights
            float workflowBarHeight = 44f;
            float bottomBarHeight = 52f;
            float scrollAreaHeight = position.height - workflowBarHeight - bottomBarHeight;

            // Draw workflow step indicator at top (fixed, prominent, not scrolled)
            DrawWorkflowSteps();

            // Scrollable content area with fixed height
            EditorGUILayout.BeginVertical(GUILayout.Height(scrollAreaHeight));

            // Outer padding wrapper
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);
            EditorGUILayout.BeginVertical();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
            DrawCaptureSettings();
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Zoom control row - FIXED position, outside of expandable panels
            DrawZoomControlRow();
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

            // Main content: Images left, Controls right
            // Account for outer padding (12px each side = 24px total)
            float availableWidth = position.width - 24;
            EditorGUILayout.BeginHorizontal();

            // Left panel - Tabbed Image View (55% width)
            EditorGUILayout.BeginVertical(GUILayout.Width(availableWidth * 0.55f));
            DrawTabbedImagePanel();
            EditorGUILayout.EndVertical();

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Right panel - Light list and offset controls (45% width) - expands vertically
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawGroupedLightListPanel();
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
            DrawQuickActionsBar();
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
            DrawOffsetPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
            DrawFooter();

            EditorGUILayout.EndScrollView();

            // Close outer padding wrapper
            EditorGUILayout.EndVertical();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Fixed bottom action bar (always visible at bottom)
            DrawBottomActionBar();

            // Force scene view update for gizmos
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Draws a fixed bottom action bar with Cancel and Apply buttons.
        /// Always visible at the bottom of the window.
        /// </summary>
        private void DrawBottomActionBar() {
            // Count enabled lights
            int enabledCount = 0;
            if (result?.lights != null) {
                foreach (var l in result.lights) {
                    if (l.enabled) enabledCount++;
                }
            }

            float barHeight = 52f;

            // Bottom bar container with background
            Rect barRect = EditorGUILayout.BeginVertical(GUILayout.Height(barHeight));

            // Draw background
            EditorGUI.DrawRect(barRect, SurfaceMid);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), SurfaceBorder);

            // Vertical centering
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space6);

            // Cancel button (left)
            GUIStyle cancelStyle = new GUIStyle(GUI.skin.button) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(18, 18, 8, 8)
            };
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.NeutralBg;
            if (GUILayout.Button("Cancel", cancelStyle, GUILayout.Width(120), GUILayout.Height(RCCP_AIDesignSystem.Heights.SidebarItem))) {
                onCancel?.Invoke();
                Close();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();

            // Light count summary in center
            GUIStyle summaryStyle = new GUIStyle(EditorStyles.label) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = MutedTextColor }
            };
            GUILayout.Label($"{enabledCount} of {result?.lights?.Count ?? 0} lights selected", summaryStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero));

            GUILayout.FlexibleSpace();

            // Apply button (right) - PROMINENT
            GUIStyle applyStyle = new GUIStyle(GUI.skin.button) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeMDL,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(20, 20, 8, 8)
            };

            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.SuccessBg;
            GUI.color = Color.white;

            if (GUILayout.Button($"\u2713 Apply {enabledCount} Lights", applyStyle, GUILayout.MinWidth(170), GUILayout.Height(RCCP_AIDesignSystem.Heights.SidebarItem))) {
                currentStep = WorkflowStep.Apply;
                var enabledLights = new List<RCCP_AIVisionLightDetector_V2.DetectedLight>();
                foreach (var l in result.lights) {
                    if (l.enabled)
                        enabledLights.Add(l);
                }
                onApply?.Invoke(enabledLights);
                Close();
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space6);
            EditorGUILayout.EndHorizontal();

            // Vertical centering
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Keyboard Shortcuts

        /// <summary>
        /// Handles keyboard shortcuts for view switching and light navigation.
        /// </summary>
        private void HandleKeyboardInput() {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            bool handled = false;

            // View switching: 1, 2, 3 keys
            switch (e.keyCode) {
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                if (result.frontCapture != null) {
                    currentViewTab = ViewTab.Front;
                    handled = true;
                }
                break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                if (result.rearCapture != null) {
                    currentViewTab = ViewTab.Rear;
                    handled = true;
                }
                break;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                if (result.sideCapture != null) {
                    currentViewTab = ViewTab.Side;
                    handled = true;
                }
                break;

                // Light navigation: Arrow keys
                case KeyCode.LeftArrow:
                NavigateLight(-1);
                handled = true;
                break;
                case KeyCode.RightArrow:
                NavigateLight(1);
                handled = true;
                break;

                // Toggle selected light: Space
                case KeyCode.Space:
                if (selectedLightIndex >= 0 && selectedLightIndex < result.lights.Count) {
                    var light = result.lights[selectedLightIndex];
                    light.enabled = !light.enabled;
                    // Sync pair if symmetry enabled
                    if (symmetryEnabled && HasPair(selectedLightIndex)) {
                        int pairIdx = GetPairIndex(selectedLightIndex);
                        if (pairIdx >= 0) {
                            result.lights[pairIdx].enabled = light.enabled;
                        }
                    }
                    handled = true;
                }
                break;

                // Reset offset: R key
                case KeyCode.R:
                if (selectedLightIndex >= 0 && selectedLightIndex < result.lights.Count) {
                    var light = result.lights[selectedLightIndex];
                    light.userOffset = Vector3.zero;
                    if (symmetryEnabled && HasPair(selectedLightIndex)) {
                        int pairIdx = GetPairIndex(selectedLightIndex);
                        if (pairIdx >= 0) {
                            result.lights[pairIdx].userOffset = Vector3.zero;
                            UpdateWorldPosition(result.lights[pairIdx]);
                        }
                    }
                    UpdateWorldPosition(light);
                    handled = true;
                }
                break;
            }

            if (handled) {
                e.Use();
                Repaint();
            }
        }

        #endregion

        #region Workflow Steps

        private void DrawWorkflowSteps() {
            // Prominent workflow bar with background
            Rect barRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(RCCP_AIDesignSystem.Heights.Toolbar));

            // Draw background
            EditorGUI.DrawRect(barRect, SurfaceDark);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1, barRect.width, 1), SurfaceBorder);

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space7);

            string[] steps = { "1. Capture", "2. Detect", "3. Adjust", "4. Apply" };
            WorkflowStep[] stepEnums = { WorkflowStep.Capture, WorkflowStep.Detect, WorkflowStep.Adjust, WorkflowStep.Apply };

            for (int i = 0; i < steps.Length; i++) {
                // Determine step state
                bool isCompleted = (int)stepEnums[i] < (int)currentStep;
                bool isActive = stepEnums[i] == currentStep;

                // Get colors for this step - much higher contrast for active
                Color bgColor, textColor, borderColor;
                if (isCompleted) {
                    bgColor = new Color(0.22f, 0.24f, 0.28f, 1f);
                    textColor = new Color(0.78f, 0.8f, 0.84f, 1f);
                    borderColor = Color.clear;
                } else if (isActive) {
                    bgColor = new Color(0.2f, 0.32f, 0.48f, 1f);
                    textColor = Color.white;
                    borderColor = AccentBlue;
                } else {
                    bgColor = new Color(0.2f, 0.2f, 0.23f, 1f);
                    textColor = StepPendingColor;
                    borderColor = Color.clear;
                }

                // Active step is larger
                float stepHeight = isActive ? 32f : 26f;
                float stepMinWidth = isActive ? 110f : 90f;
                int fontSize = isActive ? 13 : 11;

                // Draw step pill
                GUIStyle stepStyle = new GUIStyle(EditorStyles.label) {
                    fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = fontSize,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = textColor }
                };

                string prefix = isCompleted ? "\u2713 " : "";  // ✓ checkmark

                // Reserve space for step
                Rect stepRect = GUILayoutUtility.GetRect(stepMinWidth, stepHeight, GUILayout.MinWidth(stepMinWidth), GUILayout.Height(stepHeight));

                // Center vertically in the bar
                float yOffset = (barRect.height - stepHeight) / 2f;
                stepRect.y = barRect.y + yOffset;

                // Draw glow effect for active step
                if (isActive) {
                    // Subtle glow
                    EditorGUI.DrawRect(new Rect(stepRect.x - 2, stepRect.y - 2, stepRect.width + 4, stepRect.height + 4), AccentBlueSoft);

                    // Border
                    float borderWidth = 1f;
                    EditorGUI.DrawRect(new Rect(stepRect.x - borderWidth, stepRect.y - borderWidth, stepRect.width + borderWidth * 2, borderWidth), borderColor);
                    EditorGUI.DrawRect(new Rect(stepRect.x - borderWidth, stepRect.yMax, stepRect.width + borderWidth * 2, borderWidth), borderColor);
                    EditorGUI.DrawRect(new Rect(stepRect.x - borderWidth, stepRect.y, borderWidth, stepRect.height), borderColor);
                    EditorGUI.DrawRect(new Rect(stepRect.xMax, stepRect.y, borderWidth, stepRect.height), borderColor);
                }

                // Background
                EditorGUI.DrawRect(stepRect, bgColor);

                // Text
                GUI.Label(stepRect, prefix + steps[i], stepStyle);

                // Draw connector arrow (except for last step)
                if (i < steps.Length - 1) {
                    Color arrowColor = isCompleted ? new Color(0.45f, 0.55f, 0.65f, 1f) : new Color(0.38f, 0.38f, 0.42f, 1f);
                    GUIStyle arrowStyle = new GUIStyle(EditorStyles.label) {
                        fontSize = RCCP_AIDesignSystem.Typography.SizeXL,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = arrowColor }
                    };
                    GUILayout.Label("\u2192", arrowStyle, GUILayout.Width(28));  // → arrow
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space7);
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Header

        private void DrawHeader() {
            GUIStyle headerBoxStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(10, 10, 8, 8)
            };
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
                alignment = TextAnchor.MiddleLeft
            };
            GUIStyle statStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = AccentBlue }
            };
            GUIStyle statSubStyle = new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = MutedTextColor }
            };
            GUIStyle mutedLabelStyle = new GUIStyle(EditorStyles.miniLabel) {
                normal = { textColor = MutedTextColor }
            };
            GUIStyle descriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) {
                normal = { textColor = MutedTextColor }
            };

            EditorGUILayout.BeginVertical(headerBoxStyle);

            int total = result.lights?.Count ?? 0;
            int enabledCount = 0;
            int front = 0, rear = 0;
            if (result.lights != null) {
                foreach (var l in result.lights) {
                    if (l.enabled) enabledCount++;
                    if (l.view == "front") front++;
                    else rear++;
                }
            }

            int expected = GetExpectedLightCount(result.vehicleType);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AI Vision Light Detection", titleStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.Width(240));
            EditorGUILayout.LabelField($"Detected: {total} lights ({enabledCount} enabled)", statStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Front: {front} | Rear: {rear}", statSubStyle, GUILayout.Width(110));
            if (total < expected) {
                GUIStyle warnStyle = new GUIStyle(EditorStyles.miniLabel) {
                    normal = { textColor = new Color(1f, 0.8f, 0.2f, 1f) }
                };
                EditorGUILayout.LabelField($"(Expected ~{expected})", warnStyle);
            } else if (total > expected) {
                GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel) {
                    normal = { textColor = new Color(0.6f, 0.75f, 0.9f, 1f) }
                };
                EditorGUILayout.LabelField($"(Expected ~{expected})", infoStyle);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);

            // Vehicle info row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Vehicle", mutedLabelStyle, GUILayout.Width(50));
            EditorGUILayout.SelectableLabel(result.vehicle?.name ?? "Unknown", EditorStyles.textField, GUILayout.Height(RCCP_AIDesignSystem.Heights.Field), GUILayout.MinWidth(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Type", mutedLabelStyle, GUILayout.Width(50));
            EditorGUILayout.LabelField(result.vehicleType ?? "Unknown");
            Vector3 size = result.localBounds.size;
            EditorGUILayout.LabelField($"({size.x:F1}W x {size.y:F1}H x {size.z:F1}L)", mutedLabelStyle);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            // Explanation text (if any)
            if (!string.IsNullOrEmpty(result.explanation)) {
                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);
                EditorGUILayout.LabelField(result.explanation, descriptionStyle);
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Capture Settings

        private void DrawCaptureSettings() {
            // Only show capture settings in developer mode (toggle via AI Assistant Window > Settings > Developer Options)
            if (developerMode) {
                // Small header indicating dev mode is on
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = new Color(0.7f, 0.85f, 1f);
                GUILayout.Label("\u2699 Developer Mode", EditorStyles.miniLabel, GUILayout.Width(100));
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Capture Settings", EditorStyles.boldLabel, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Ortho Size: {result.calculatedOrthoSize:F2}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("Ortho Multiplier:", GUILayout.Width(100));
                orthoSizeMultiplier = EditorGUILayout.Slider(orthoSizeMultiplier, 0.5f, 3.0f);

                EditorGUI.BeginDisabledGroup(isRecapturing || !HasValidAuth);
                if (GUILayout.Button(isRecapturing ? "Re-capturing..." : "Re-capture", GUILayout.Width(90))) {
                    StartRecapture();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Lower = zoom in, Higher = zoom out", EditorStyles.centeredGreyMiniLabel);

                if (!HasValidAuth) {
                    EditorGUILayout.HelpBox("API key or server proxy not available. Re-detect from main window.", MessageType.Info);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void StartRecapture() {
            if (isRecapturing || result == null || result.vehicle == null || !HasValidAuth)
                return;

            isRecapturing = true;

            // Update the detector's ortho size multiplier
            RCCP_AIVisionLightDetector_V2.Instance.OrthoSizeMultiplier = orthoSizeMultiplier;

            // Start new detection
            RCCP_AIVisionLightDetector_V2.Instance.DetectLights(
                result.vehicle,
                apiKey,
                OnRecaptureComplete,
                this
            );
        }

        private void OnRecaptureComplete(RCCP_AIVisionLightDetector_V2.DetectionResult newResult) {
            isRecapturing = false;

            if (newResult.success) {
                // Cleanup old textures
                result.Cleanup();

                // Update with new result
                result = newResult;
                orthoSizeMultiplier = newResult.orthoSizeMultiplier;
                selectedLightIndex = -1;

                // Rebuild light pairs with new detection result
                BuildLightPairs();

                Debug.Log($"[RCCP Vision V2] Re-capture complete: {newResult.lights.Count} lights detected with ortho multiplier {orthoSizeMultiplier:F2}");
            } else {
                Debug.LogError($"[RCCP Vision V2] Re-capture failed: {newResult.error}");
                EditorUtility.DisplayDialog("Re-capture Failed", newResult.error, "OK");
            }

            Repaint();
        }

        #endregion

        #region Zoom Control Row

        // Zoom presets
        private static readonly string[] zoomPresets = { "50%", "75%", "100%", "125%", "150%", "200%" };
        private static readonly float[] zoomValues = { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f };

        /// <summary>
        /// Draws unified control toolbar with zoom, view tabs, navigation, and gizmo size.
        /// </summary>
        private void DrawZoomControlRow() {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Zoom dropdown
            EditorGUILayout.LabelField("\ud83d\udd0d", GUILayout.Width(16)); // Magnifier icon
            int currentZoomIndex = GetZoomPresetIndex(imageZoom);
            int newZoomIndex = EditorGUILayout.Popup(currentZoomIndex, zoomPresets, EditorStyles.toolbarDropDown, GUILayout.Width(56));
            if (newZoomIndex != currentZoomIndex) {
                imageZoom = zoomValues[newZoomIndex];
            }

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Separator
            EditorGUILayout.LabelField("|", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(8));

            // View tabs (compact toggle buttons)
            GUI.enabled = result.frontCapture != null;
            if (GUILayout.Toggle(currentViewTab == ViewTab.Front, "Front", EditorStyles.toolbarButton, GUILayout.Width(45))) {
                if (currentViewTab != ViewTab.Front) currentViewTab = ViewTab.Front;
            }
            GUI.enabled = result.rearCapture != null;
            if (GUILayout.Toggle(currentViewTab == ViewTab.Rear, "Rear", EditorStyles.toolbarButton, GUILayout.Width(40))) {
                if (currentViewTab != ViewTab.Rear) currentViewTab = ViewTab.Rear;
            }
            GUI.enabled = result.sideCapture != null;
            if (GUILayout.Toggle(currentViewTab == ViewTab.Side, "Side", EditorStyles.toolbarButton, GUILayout.Width(40))) {
                if (currentViewTab != ViewTab.Side) currentViewTab = ViewTab.Side;
            }
            GUI.enabled = true;

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Separator
            EditorGUILayout.LabelField("|", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(8));

            // Navigation buttons
            GUI.enabled = selectedLightIndex > 0;
            if (GUILayout.Button("\u25C0", EditorStyles.toolbarButton, GUILayout.Width(22))) {
                NavigateLight(-1);
            }
            GUI.enabled = selectedLightIndex >= 0 && selectedLightIndex < (result.lights?.Count ?? 0) - 1;
            if (GUILayout.Button("\u25B6", EditorStyles.toolbarButton, GUILayout.Width(22))) {
                NavigateLight(1);
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Position scale sliders - adjust mapping of coordinates to vehicle bounds
            EditorGUILayout.LabelField("Scale:", EditorStyles.miniLabel, GUILayout.Width(35));
            EditorGUILayout.LabelField("X", EditorStyles.miniLabel, GUILayout.Width(12));
            EditorGUI.BeginChangeCheck();
            positionScaleX = GUILayout.HorizontalSlider(positionScaleX, 0.7f, 1.3f, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck()) {
                RecalculateAllWorldPositions();
            }
            EditorGUILayout.LabelField($"{positionScaleX:F2}", EditorStyles.miniLabel, GUILayout.Width(28));

            EditorGUILayout.LabelField("Y", EditorStyles.miniLabel, GUILayout.Width(12));
            EditorGUI.BeginChangeCheck();
            positionScaleY = GUILayout.HorizontalSlider(positionScaleY, 0.7f, 1.3f, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck()) {
                RecalculateAllWorldPositions();
            }
            EditorGUILayout.LabelField($"{positionScaleY:F2}", EditorStyles.miniLabel, GUILayout.Width(28));

            // Reset button
            if (GUILayout.Button("R", EditorStyles.toolbarButton, GUILayout.Width(20))) {
                positionScaleX = 1.0f;
                positionScaleY = 1.0f;
                RecalculateAllWorldPositions();
            }

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

            // Separator
            EditorGUILayout.LabelField("|", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(8));

            // Gizmo size (compact)
            EditorGUILayout.LabelField("\u25CE", GUILayout.Width(14)); // Circle icon for gizmo
            gizmoSize = GUILayout.HorizontalSlider(gizmoSize, 0.25f, 1.2f, GUILayout.Width(50));
            EditorGUILayout.LabelField($"{gizmoSize:F1}x", EditorStyles.miniLabel, GUILayout.Width(28));

            EditorGUILayout.EndHorizontal();
        }

        private int GetZoomPresetIndex(float zoom) {
            // Find closest preset
            int closest = 0;
            float minDiff = float.MaxValue;
            for (int i = 0; i < zoomValues.Length; i++) {
                float diff = Mathf.Abs(zoomValues[i] - zoom);
                if (diff < minDiff) {
                    minDiff = diff;
                    closest = i;
                }
            }
            return closest;
        }

        #endregion

        #region Tabbed Image Panel

        private void DrawTabbedImagePanel() {
            GUIStyle panelStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(10, 10, 8, 8)
            };
            EditorGUILayout.BeginVertical(panelStyle);

            // Note: Tabs moved to unified toolbar above

            // Click/drag hint
            GUIStyle hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) {
                richText = true,
                normal = { textColor = MutedTextColor }
            };
            EditorGUILayout.LabelField("Click markers to select \u2022 Drag to reposition \u2022 <b>\u2190\u2192</b> Navigate \u2022 <b>1/2/3</b> Switch view \u2022 <b>Space</b> Toggle \u2022 <b>R</b> Reset", hintStyle);
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);

            // Calculate dynamic image size based on panel width - fill available space
            // Account for: workflow bar (44px), bottom bar (52px), header (~100px), footer (~80px), thumbnails (~140px), margins
            float panelWidth = (position.width - 24) * 0.55f - 20;
            float panelHeight = position.height - 44 - 52 - 350;  // Available height for image
            float imageSize = Mathf.Max(200, Mathf.Min(panelWidth, panelHeight, 500)) * imageZoom;

            // Draw current tab's image
            switch (currentViewTab) {
                case ViewTab.Front:
                if (result.frontCapture != null)
                    DrawImageWithMarkers(result.frontCapture, "front", imageSize);
                else
                    EditorGUILayout.HelpBox("Front view not captured.", MessageType.Warning);
                break;
                case ViewTab.Rear:
                if (result.rearCapture != null)
                    DrawImageWithMarkers(result.rearCapture, "rear", imageSize);
                else
                    EditorGUILayout.HelpBox("Rear view not captured.", MessageType.Warning);
                break;
                case ViewTab.Side:
                if (result.sideCapture != null)
                    DrawImageWithMarkers(result.sideCapture, "side", imageSize);
                else
                    EditorGUILayout.HelpBox("Side view not captured.", MessageType.Warning);
                break;
            }

            // Thumbnail strip below
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
            DrawThumbnailStrip();

            EditorGUILayout.EndVertical();
        }

        private void DrawThumbnailStrip() {
            GUIStyle stripStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(6, 6, 6, 6)
            };
            EditorGUILayout.BeginVertical(stripStyle);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            float thumbSize = 110;  // Larger thumbnails for better visibility

            // Count lights per view
            int frontCount = CountLightsForView("front");
            int rearCount = CountLightsForView("rear");

            // Front thumbnail
            if (result.frontCapture != null) {
                DrawThumbnail(result.frontCapture, ViewTab.Front, thumbSize, "Front", frontCount);
            }

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Rear thumbnail
            if (result.rearCapture != null) {
                DrawThumbnail(result.rearCapture, ViewTab.Rear, thumbSize, "Rear", rearCount);
            }

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Side thumbnail
            if (result.sideCapture != null) {
                DrawThumbnail(result.sideCapture, ViewTab.Side, thumbSize, "Side", -1); // -1 = depth view, no count
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private int CountLightsForView(string view) {
            if (result?.lights == null) return 0;
            int count = 0;
            foreach (var light in result.lights) {
                if (light.view?.ToLower() == view && light.enabled)
                    count++;
            }
            return count;
        }

        private void DrawThumbnail(Texture2D texture, ViewTab tab, float size, string label, int lightCount) {
            bool isActive = currentViewTab == tab;

            // Reserve space for thumbnail + label below
            EditorGUILayout.BeginVertical(GUILayout.Width(size));

            Rect thumbRect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));

            // Selection border - thicker when selected (3px)
            if (isActive) {
                float borderWidth = 3f;
                EditorGUI.DrawRect(new Rect(thumbRect.x - borderWidth, thumbRect.y - borderWidth,
                    thumbRect.width + borderWidth * 2, thumbRect.height + borderWidth * 2), AccentBlue);
            } else {
                // Subtle border when not selected
                EditorGUI.DrawRect(new Rect(thumbRect.x - 1, thumbRect.y - 1,
                    thumbRect.width + 2, thumbRect.height + 2), SurfaceBorder);
            }

            // Texture
            GUI.DrawTexture(thumbRect, texture, ScaleMode.ScaleToFit);

            // Light count badge (top-right corner)
            if (lightCount >= 0) {
                GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel) {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
                    fontStyle = FontStyle.Bold
                };
                float badgeSize = 20f;
                Rect badgeRect = new Rect(thumbRect.xMax - badgeSize - 4, thumbRect.y + 4, badgeSize, badgeSize);
                Color badgeColor = lightCount > 0 ? new Color(0.2f, 0.6f, 0.9f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.7f);
                EditorGUI.DrawRect(badgeRect, badgeColor);
                GUI.Label(badgeRect, lightCount.ToString(), badgeStyle);
            }

            // Label below thumbnail
            GUIStyle labelStyle = new GUIStyle(isActive ? EditorStyles.boldLabel : EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = isActive ? 11 : 10,
                normal = { textColor = isActive ? Color.white : MutedTextColor }
            };
            EditorGUILayout.LabelField(label, labelStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.Pill));

            EditorGUILayout.EndVertical();

            // Click to switch
            if (Event.current.type == EventType.MouseDown && thumbRect.Contains(Event.current.mousePosition)) {
                currentViewTab = tab;
                Event.current.Use();
                Repaint();
            }

            // Hover cursor
            EditorGUIUtility.AddCursorRect(thumbRect, MouseCursor.Link);
        }

        private void DrawImageWithMarkers(Texture2D image, string viewType, float size) {
            // Center the image horizontally
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();

            Rect imageRect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            Rect borderRect = new Rect(imageRect.x - 1, imageRect.y - 1, imageRect.width + 2, imageRect.height + 2);
            EditorGUI.DrawRect(borderRect, SurfaceBorder);
            EditorGUI.DrawRect(imageRect, SurfaceDark);
            GUI.DrawTexture(imageRect, image, ScaleMode.ScaleToFit);

            // Handle drag events for this image area
            HandleDragEvents(imageRect, viewType);

            // Draw markers for lights
            if (result.lights != null) {
                for (int i = 0; i < result.lights.Count; i++) {
                    var light = result.lights[i];

                    // Filter by view
                    if (viewType == "side") {
                        // Side view shows all lights by Z position
                        DrawSideViewMarker(imageRect, light, i, viewType);
                    } else if (light.view == viewType) {
                        DrawFrontRearMarker(imageRect, light, i, viewType);
                    }
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Handles mouse drag events for repositioning light markers in the image views.
        /// </summary>
        private void HandleDragEvents(Rect imageRect, string viewType) {
            Event e = Event.current;

            // Handle dragging
            if (isDragging && draggingViewType == viewType) {
                if (e.type == EventType.MouseDrag) {
                    // Calculate new position from mouse
                    ApplyDragOffset(imageRect, viewType, e.mousePosition);
                    e.Use();
                    Repaint();
                } else if (e.type == EventType.MouseUp) {
                    // End drag
                    isDragging = false;
                    draggingLightIndex = -1;
                    draggingViewType = "";
                    e.Use();
                    Repaint();
                }
            }

            // Show drag cursor when over a selected light marker
            if (selectedLightIndex >= 0 && imageRect.Contains(e.mousePosition)) {
                EditorGUIUtility.AddCursorRect(imageRect, MouseCursor.MoveArrow);
            }
        }

        /// <summary>
        /// Applies drag offset based on mouse position in the image.
        /// Offsets are calculated in local space then transformed to world space
        /// for consistency with scene view gizmo dragging.
        /// </summary>
        private void ApplyDragOffset(Rect imageRect, string viewType, Vector2 mousePos) {
            if (draggingLightIndex < 0 || draggingLightIndex >= result.lights.Count)
                return;

            var light = result.lights[draggingLightIndex];
            Vector3 boundsSize = result.localBounds.size;

            // Convert mouse position to normalized image coordinates (0-1)
            float normMouseX = (mousePos.x - imageRect.x) / imageRect.width;
            float normMouseY = (mousePos.y - imageRect.y) / imageRect.height;

            // Get vehicle extents in image
            float extentX, extentY, paddingX, paddingY;

            if (viewType == "side") {
                GetSideImageExtents(out extentX, out extentY, out paddingX, out paddingY);

                // Convert normalized mouse position to vehicle-relative coordinates
                // Account for padding
                float vehicleNormX = (normMouseX - paddingX) / extentX;
                float vehicleNormY = (normMouseY - paddingY) / extentY;

                // In side view: left of image = front (Z=1), right = back (Z=0)
                // So: vehicleNormX -> normalizedZ = 1 - vehicleNormX
                // And: vehicleNormY -> normalizedY = 1 - vehicleNormY (inverted Y)

                float targetNormZ = 1f - vehicleNormX;
                float targetNormY = 1f - vehicleNormY;

                // Calculate offset needed to move light to target position (in local space)
                float localOffsetZ = (targetNormZ - light.normalizedZ) * boundsSize.z;
                float localOffsetY = (targetNormY - light.normalizedY) * boundsSize.y;

                // PRESERVE existing X offset from front/rear view adjustments
                Vector3 existingLocalOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);
                Vector3 localOffset = new Vector3(existingLocalOffset.x, localOffsetY, localOffsetZ);
                Vector3 worldOffset = result.vehicle.transform.TransformVector(localOffset);
                light.userOffset = worldOffset;

                // Sync with pair (Y and Z are the same for pairs in side view)
                // PRESERVE the pair's existing X offset (pairs have mirrored X positions)
                if (symmetryEnabled && HasPair(draggingLightIndex)) {
                    int pairIdx = GetPairIndex(draggingLightIndex);
                    if (pairIdx >= 0) {
                        Vector3 pairExistingLocalOffset = result.vehicle.transform.InverseTransformVector(result.lights[pairIdx].userOffset);
                        Vector3 pairLocalOffset = new Vector3(pairExistingLocalOffset.x, localOffsetY, localOffsetZ);
                        Vector3 pairWorldOffset = result.vehicle.transform.TransformVector(pairLocalOffset);
                        result.lights[pairIdx].userOffset = pairWorldOffset;
                    }
                }
            } else {
                // Front or rear view
                GetFrontRearImageExtents(out extentX, out extentY, out paddingX, out paddingY);

                float vehicleNormX = (normMouseX - paddingX) / extentX;
                float vehicleNormY = (normMouseY - paddingY) / extentY;

                // Y is inverted in GUI
                float targetNormY = 1f - vehicleNormY;

                // X handling depends on view type and needs coordinate correction
                float targetNormX;
                if (viewType == "front") {
                    // Front view: image left = vehicle right, so flip
                    targetNormX = 1f - vehicleNormX;
                } else {
                    // Rear view: image left = vehicle left, no flip
                    targetNormX = vehicleNormX;
                }

                // The light's base normalizedX is IMAGE-relative, but we've already corrected it
                // Need to work backwards from the displayed (corrected) X
                float baseCorrectedX = light.normalizedX;
                if (viewType == "front") {
                    baseCorrectedX = 1f - light.normalizedX;
                }

                // Calculate offset in local space
                float localOffsetX = (targetNormX - baseCorrectedX) * boundsSize.x;
                float localOffsetY = (targetNormY - light.normalizedY) * boundsSize.y;

                // PRESERVE existing Z offset from side view adjustments
                Vector3 existingLocalOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);
                Vector3 localOffset = new Vector3(localOffsetX, localOffsetY, existingLocalOffset.z);
                Vector3 worldOffset = result.vehicle.transform.TransformVector(localOffset);
                light.userOffset = worldOffset;

                // Sync with pair - mirror X offset in local space, then transform to world
                // PRESERVE existing Z offset of the paired light
                if (symmetryEnabled && HasPair(draggingLightIndex)) {
                    int pairIdx = GetPairIndex(draggingLightIndex);
                    if (pairIdx >= 0) {
                        Vector3 pairExistingLocalOffset = result.vehicle.transform.InverseTransformVector(result.lights[pairIdx].userOffset);
                        Vector3 mirroredLocalOffset = new Vector3(-localOffsetX, localOffsetY, pairExistingLocalOffset.z);
                        Vector3 mirroredWorldOffset = result.vehicle.transform.TransformVector(mirroredLocalOffset);
                        result.lights[pairIdx].userOffset = mirroredWorldOffset;
                    }
                }
            }

            UpdateWorldPosition(light);
        }

        /// <summary>
        /// Calculates the vehicle extent and padding in the captured image for front/rear views.
        /// The vehicle doesn't fill the entire image due to ortho camera padding and aspect ratio.
        /// </summary>
        private void GetFrontRearImageExtents(out float extentX, out float extentY, out float paddingX, out float paddingY) {
            Vector3 size = result.localBounds.size;
            float vehicleWidth = size.x;
            float vehicleHeight = size.y;
            float multiplier = Mathf.Max(0.5f, result.orthoSizeMultiplier);

            // Camera uses: orthoSize = max(width, height) / 2 * CAMERA_PADDING * multiplier
            float relevantSize = Mathf.Max(vehicleWidth, vehicleHeight);
            float totalVisibleSize = relevantSize * RCCP_AIVisionLightDetector_V2.CAMERA_PADDING * multiplier;

            // Vehicle extent in each dimension
            extentX = vehicleWidth / totalVisibleSize;
            extentY = vehicleHeight / totalVisibleSize;

            // Padding (centered)
            paddingX = (1f - extentX) / 2f;
            paddingY = (1f - extentY) / 2f;
        }

        /// <summary>
        /// Calculates the vehicle extent and padding in the captured image for side view.
        /// </summary>
        private void GetSideImageExtents(out float extentX, out float extentY, out float paddingX, out float paddingY) {
            Vector3 size = result.localBounds.size;
            float vehicleLength = size.z;
            float vehicleHeight = size.y;
            float multiplier = Mathf.Max(0.5f, result.orthoSizeMultiplier);

            // Camera uses: orthoSize = max(length, height) / 2 * CAMERA_PADDING * multiplier
            float relevantSize = Mathf.Max(vehicleLength, vehicleHeight);
            float totalVisibleSize = relevantSize * RCCP_AIVisionLightDetector_V2.CAMERA_PADDING * multiplier;

            // Vehicle extent in each dimension
            extentX = vehicleLength / totalVisibleSize;
            extentY = vehicleHeight / totalVisibleSize;

            // Padding (centered)
            paddingX = (1f - extentX) / 2f;
            paddingY = (1f - extentY) / 2f;
        }

        private void DrawFrontRearMarker(Rect imageRect, RCCP_AIVisionLightDetector_V2.DetectedLight light, int index, string viewType) {
            // Get vehicle extent in image (accounting for ortho padding and aspect ratio)
            GetFrontRearImageExtents(out float extentX, out float extentY, out float paddingX, out float paddingY);

            // normalizedX/Y are vehicle-relative (0-1 across vehicle bounds)
            // Apply position scale (scale from center 0.5)
            float scaledNormX = 0.5f + (light.normalizedX - 0.5f) * positionScaleX;
            float scaledNormY = 0.5f + (light.normalizedY - 0.5f) * positionScaleY;

            float imageNormX = scaledNormX;

            // Y is inverted (Unity GUI has Y=0 at top, but normalizedY 1 = top of vehicle)
            float imageNormY = 1f - scaledNormY;

            // Convert from AI coordinates (vehicle extent) to full image coordinates
            float imageX = paddingX + imageNormX * extentX;
            float imageY = paddingY + imageNormY * extentY;

            // Apply user offset for visualization
            // userOffset is in world space, transform back to local space for image display
            Vector3 boundsSize = result.localBounds.size;
            Vector3 localOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);
            float offsetX = (localOffset.x / boundsSize.x) * extentX;
            float offsetY = (localOffset.y / boundsSize.y) * extentY;

            if (viewType == "front") {
                // In front view, positive X offset (vehicle right) moves marker LEFT in image
                imageX -= offsetX;
            } else {
                // In rear view, positive X offset (vehicle right) moves marker RIGHT in image
                imageX += offsetX;
            }
            imageY -= offsetY;

            float x = imageRect.x + imageX * imageRect.width;
            float y = imageRect.y + imageY * imageRect.height;

            DrawMarker(x, y, light, index, viewType, imageRect);
        }

        private void DrawSideViewMarker(Rect imageRect, RCCP_AIVisionLightDetector_V2.DetectedLight light, int index, string viewType) {
            // Get vehicle extent in image (accounting for ortho padding and aspect ratio)
            GetSideImageExtents(out float extentX, out float extentY, out float paddingX, out float paddingY);

            // Side view (from left): In the image, LEFT = front of car, RIGHT = rear of car
            // AI prompt says: "Left edge of car in image = Front (1.0), Right edge = Rear (0.0)"
            // So normalizedZ=1.0 (front) should appear on LEFT of image (imageX=0)
            // normalizedZ=0.0 (rear) should appear on RIGHT of image (imageX=1)
            float imageNormX = 1f - light.normalizedZ;

            // Apply Y scale and invert (Unity GUI has Y=0 at top, but normalizedY 1 = top of vehicle)
            float scaledNormY = 0.5f + (light.normalizedY - 0.5f) * positionScaleY;
            float imageNormY = 1f - scaledNormY;

            // Convert from normalized to image coordinates
            float imageX = paddingX + imageNormX * extentX;
            float imageY = paddingY + imageNormY * extentY;

            // Apply user offset
            // userOffset is in world space, transform back to local space for image display
            Vector3 boundsSize = result.localBounds.size;
            Vector3 localOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);
            float offsetZ = (localOffset.z / boundsSize.z) * extentX;
            float offsetY = (localOffset.y / boundsSize.y) * extentY;

            // Positive Z offset (moving toward front) moves marker LEFT in side view
            imageX -= offsetZ;
            imageY -= offsetY;

            float x = imageRect.x + imageX * imageRect.width;
            float y = imageRect.y + imageY * imageRect.height;

            DrawMarker(x, y, light, index, viewType, imageRect);
        }

        private void DrawMarker(float x, float y, RCCP_AIVisionLightDetector_V2.DetectedLight light, int index) {
            DrawMarker(x, y, light, index, "", Rect.zero);
        }

        private void DrawMarker(float x, float y, RCCP_AIVisionLightDetector_V2.DetectedLight light, int index, string viewType, Rect imageRect) {
            bool isSelected = (index == selectedLightIndex);
            bool hasPair = HasPair(index);
            int pairIndex = GetPairIndex(index);
            bool isPairOfSelected = pairIndex >= 0 && pairIndex == selectedLightIndex;
            bool isBeingDragged = isDragging && draggingLightIndex == index;

            // Base radius with gizmo size multiplier - larger for better visibility
            float baseRadius = 12f * gizmoSize;
            float radius = isSelected ? baseRadius * 1.4f : (isPairOfSelected ? baseRadius * 1.2f : baseRadius);

            // Make dragged marker slightly larger
            if (isBeingDragged) {
                radius = baseRadius * 1.75f;
            }

            Color color = light.enabled
                ? RCCP_AIVisionLightDetector_V2.GetLightColor(light.lightType)
                : DisabledColor;

            // Drag indicator glow (green when dragging)
            if (isBeingDragged) {
                Handles.BeginGUI();
                Handles.color = new Color(0.3f, 1f, 0.3f, 0.5f);
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, radius * 2f);
                Handles.EndGUI();
            }
            // Selection glow (yellow for selected)
            else if (isSelected) {
                Handles.BeginGUI();
                Handles.color = new Color(1f, 1f, 0.3f, 0.4f);
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, radius * 1.8f);
                Handles.EndGUI();
            }
            // Pair highlight glow (cyan for paired light when its pair is selected)
            else if (isPairOfSelected && symmetryEnabled) {
                Handles.BeginGUI();
                Handles.color = new Color(0.3f, 0.8f, 1f, 0.35f);
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, radius * 1.6f);
                Handles.EndGUI();
            }

            // Filled circle - larger fill for better type color visibility
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, radius * 0.92f);

            // Border (green when dragging, yellow for selected, cyan for pair, black otherwise)
            if (isBeingDragged) {
                Handles.color = new Color(0.3f, 1f, 0.3f, 1f);
                Handles.DrawWireDisc(new Vector3(x, y, 0), Vector3.forward, radius);
                Handles.DrawWireDisc(new Vector3(x, y, 0), Vector3.forward, radius + 3f);
            } else if (isSelected) {
                Handles.color = SelectedColor;
                Handles.DrawWireDisc(new Vector3(x, y, 0), Vector3.forward, radius);
                Handles.DrawWireDisc(new Vector3(x, y, 0), Vector3.forward, radius + 2f);
            } else if (isPairOfSelected && symmetryEnabled) {
                Handles.color = new Color(0.3f, 0.8f, 1f, 1f);
                Handles.DrawWireDisc(new Vector3(x, y, 0), Vector3.forward, radius);
                Handles.DrawWireDisc(new Vector3(x, y, 0), Vector3.forward, radius + 2f);
            } else {
                Handles.color = Color.black;
                Handles.DrawWireDisc(new Vector3(x, y, 0), Vector3.forward, radius);
            }

            // Pair link indicator (small line)
            if (hasPair && !isSelected && !isPairOfSelected && !isBeingDragged) {
                Handles.color = new Color(0.5f, 0.7f, 1f, 0.5f);
                // Draw a small tick to indicate pairing
                Handles.DrawLine(new Vector3(x, y - radius - 2, 0), new Vector3(x, y - radius - 6, 0));
            }

            Handles.EndGUI();

            // Click and drag detection
            Rect clickRect = new Rect(x - radius - 5, y - radius - 5, (radius + 5) * 2, (radius + 5) * 2);
            Event e = Event.current;

            if (e.type == EventType.MouseDown && clickRect.Contains(e.mousePosition)) {
                // Select the light
                selectedLightIndex = index;

                // Start dragging if this light is in the appropriate view
                if (!string.IsNullOrEmpty(viewType) && imageRect.width > 0) {
                    isDragging = true;
                    draggingLightIndex = index;
                    draggingViewType = viewType;
                    draggingImageRect = imageRect;
                }

                e.Use();
                Repaint();
            }

            // Show move cursor over selectable markers
            if (clickRect.Contains(e.mousePosition)) {
                EditorGUIUtility.AddCursorRect(clickRect, MouseCursor.MoveArrow);
            }
        }

        #endregion

        #region Grouped Light List Panel

        private void DrawGroupedLightListPanel() {
            int total = result.lights?.Count ?? 0;
            int enabledCount = 0;
            if (result.lights != null) {
                foreach (var l in result.lights) {
                    if (l.enabled) enabledCount++;
                }
            }

            // Header with navigation
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) {
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField($"Detected Lights ({enabledCount}/{total})", headerStyle);
            GUILayout.FlexibleSpace();

            // Prev/Next navigation buttons
            GUI.enabled = selectedLightIndex > 0;
            if (GUILayout.Button("\u25C0 Prev", EditorStyles.toolbarButton, GUILayout.Width(55))) {
                NavigateLight(-1);
            }
            GUI.enabled = selectedLightIndex < (result.lights?.Count ?? 0) - 1;
            if (GUILayout.Button("Next \u25B6", EditorStyles.toolbarButton, GUILayout.Width(55))) {
                NavigateLight(1);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Use flexible height with minimum - fills available space
            GUIStyle listBoxStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(8, 8, 6, 6)
            };
            EditorGUILayout.BeginVertical(listBoxStyle, GUILayout.MinHeight(180), GUILayout.ExpandHeight(true));

            if (result.lights == null || result.lights.Count == 0) {
                EditorGUILayout.HelpBox("No lights detected.", MessageType.Info);
            } else {
                lightListScrollPosition = EditorGUILayout.BeginScrollView(lightListScrollPosition, GUILayout.ExpandHeight(true));

                // Group lights by type
                DrawLightGroup("Headlights", "headlight_low", "headlight_high");
                DrawLightGroup("Indicators", "indicator");
                DrawLightGroup("Taillights", "taillight");
                DrawLightGroup("Brakelights", "brakelight");
                DrawLightGroup("Reverse Lights", "reverse");

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();

            // Batch controls
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Enable All", EditorStyles.toolbarButton, GUILayout.Width(80))) {
                foreach (var l in result.lights) l.enabled = true;
                Repaint();
            }
            if (GUILayout.Button("Disable All", EditorStyles.toolbarButton, GUILayout.Width(80))) {
                foreach (var l in result.lights) l.enabled = false;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void NavigateLight(int direction) {
            if (result.lights == null || result.lights.Count == 0) return;

            selectedLightIndex = Mathf.Clamp(selectedLightIndex + direction, 0, result.lights.Count - 1);
            var light = result.lights[selectedLightIndex];

            // Focus scene view
            if (SceneView.lastActiveSceneView != null && light.enabled) {
                SceneView.lastActiveSceneView.LookAt(light.FinalWorldPosition, SceneView.lastActiveSceneView.rotation, 2f);
            }

            // Auto-switch to appropriate view tab
            if (light.view == "front") currentViewTab = ViewTab.Front;
            else if (light.view == "rear") currentViewTab = ViewTab.Rear;

            Repaint();
        }

        private bool DrawLightGroup(string groupName, params string[] lightTypes) {
            // Find lights matching these types
            List<int> matchingIndices = new List<int>();
            for (int i = 0; i < result.lights.Count; i++) {
                string type = result.lights[i].lightType?.ToLower() ?? "";
                foreach (var lt in lightTypes) {
                    if (type == lt || type.StartsWith(lt)) {
                        matchingIndices.Add(i);
                        break;
                    }
                }
            }

            if (matchingIndices.Count == 0) return false;

            // Initialize foldout state if not present
            if (!groupFoldouts.ContainsKey(groupName))
                groupFoldouts[groupName] = true;

            // Group container
            GUIStyle groupBoxStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(8, 8, 6, 6)
            };
            EditorGUILayout.BeginVertical(groupBoxStyle);

            // Collapsible header with On/Off buttons
            EditorGUILayout.BeginHorizontal();

            // Get icon for group
            string icon = GetLightIcon(lightTypes[0]);

            // Count enabled lights in this group
            int enabledCount = 0;
            foreach (int idx in matchingIndices) {
                if (result.lights[idx].enabled) enabledCount++;
            }

            // Foldout with icon and count
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout) {
                fontStyle = FontStyle.Bold,
                fontSize = RCCP_AIDesignSystem.Typography.SizeBase
            };
            groupFoldouts[groupName] = EditorGUILayout.Foldout(groupFoldouts[groupName],
                $"{icon} {groupName} ({enabledCount}/{matchingIndices.Count})", true, foldoutStyle);

            GUILayout.FlexibleSpace();

            // Group On/Off buttons
            if (GUILayout.Button("On", EditorStyles.miniButtonLeft, GUILayout.Width(28))) {
                foreach (int idx in matchingIndices) result.lights[idx].enabled = true;
                Repaint();
            }
            if (GUILayout.Button("Off", EditorStyles.miniButtonRight, GUILayout.Width(28))) {
                foreach (int idx in matchingIndices) result.lights[idx].enabled = false;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();

            // Only draw lights if expanded
            if (groupFoldouts[groupName]) {
                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);

                // Two-column layout for paired lights
                EditorGUILayout.BeginHorizontal();

                // Left column (left-side lights)
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
                foreach (int idx in matchingIndices) {
                    if (result.lights[idx].side?.ToLower() == "left") {
                        DrawCompactLightItem(result.lights[idx], idx);
                    }
                }
                EditorGUILayout.EndVertical();

                GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

                // Right column (right-side lights)
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
                foreach (int idx in matchingIndices) {
                    if (result.lights[idx].side?.ToLower() == "right") {
                        DrawCompactLightItem(result.lights[idx], idx);
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                // Center lights (if any)
                foreach (int idx in matchingIndices) {
                    if (result.lights[idx].side?.ToLower() == "center") {
                        DrawCompactLightItem(result.lights[idx], idx);
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);
            return true;
        }

        private void DrawCompactLightItem(RCCP_AIVisionLightDetector_V2.DetectedLight light, int index) {
            bool isSelected = (index == selectedLightIndex);
            bool hasPair = HasPair(index);
            int pairIndex = GetPairIndex(index);
            bool isPairSelected = pairIndex >= 0 && pairIndex == selectedLightIndex;

            // Card container
            Rect cardRect = EditorGUILayout.BeginVertical(GUILayout.Height(RCCP_AIDesignSystem.Heights.Card));

            // Background
            Color bgColor = isSelected ? new Color(0.2f, 0.3f, 0.42f, 0.75f)
                : isPairSelected && symmetryEnabled ? new Color(0.2f, 0.26f, 0.32f, 0.45f)
                : new Color(0.18f, 0.18f, 0.2f, 0.35f);
            EditorGUI.DrawRect(cardRect, bgColor);

            // Selection indicator (left border)
            if (isSelected) {
                EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3, cardRect.height), SelectedColor);
            } else if (isPairSelected && symmetryEnabled) {
                EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 2, cardRect.height), PairHighlightColor);
            }

            // Row 1: Toggle + Icon + Side label
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

            // Enable toggle
            bool newEnabled = EditorGUILayout.Toggle(light.enabled, GUILayout.Width(16));
            if (newEnabled != light.enabled) {
                light.enabled = newEnabled;
                if (symmetryEnabled && hasPair && pairIndex >= 0) {
                    result.lights[pairIndex].enabled = newEnabled;
                }
                Repaint();
            }

            // Type icon (colored)
            Color typeColor = RCCP_AIVisionLightDetector_V2.GetLightColor(light.lightType);
            string icon = GetLightIcon(light.lightType);
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeMDL,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = light.enabled ? typeColor : DisabledColor }
            };
            EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(18));

            // Side label (clickable)
            string sideLabel = light.side?.ToLower() == "left" ? "Left" : (light.side?.ToLower() == "right" ? "Right" : "Center");
            GUIStyle sideLabelStyle = new GUIStyle(isSelected ? EditorStyles.boldLabel : EditorStyles.label) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = isSelected ? Color.white : MutedTextColor }
            };

            // Make the label area clickable
            Rect labelRect = GUILayoutUtility.GetRect(new GUIContent(sideLabel), sideLabelStyle, GUILayout.ExpandWidth(true));
            if (GUI.Button(labelRect, sideLabel, sideLabelStyle)) {
                SelectLight(index);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Row 2: Confidence bar (full width with padding)
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space3);
            DrawConfidenceBar(light.confidence, -1); // -1 = flexible width
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space3);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);
            EditorGUILayout.EndVertical();

            // Make entire card clickable
            if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition)) {
                SelectLight(index);
                Event.current.Use();
            }

            // Hover cursor
            EditorGUIUtility.AddCursorRect(cardRect, MouseCursor.Link);

            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1); // Space between cards
        }

        private void DrawConfidenceBar(float confidence, float width) {
            // Support flexible width (-1 = expand to fill)
            Rect barRect;
            if (width < 0) {
                barRect = GUILayoutUtility.GetRect(50, 10, GUILayout.ExpandWidth(true), GUILayout.Height(RCCP_AIDesignSystem.Heights.SliderTrack));
            } else {
                barRect = GUILayoutUtility.GetRect(width, 12, GUILayout.Width(width));
            }

            // Background with rounded appearance
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            // Fill
            Color fillColor = GetConfidenceColor(confidence);
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * confidence, barRect.height);
            EditorGUI.DrawRect(fillRect, fillColor);

            // Subtle border
            Color borderColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), borderColor);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1, barRect.width, 1), borderColor);

            // Percentage text overlay with tooltip (only show for wider bars)
            if (barRect.width >= 40) {
                GUIStyle percentStyle = new GUIStyle(EditorStyles.miniLabel) {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontSize = RCCP_AIDesignSystem.Typography.SizeXS
                };
                string tooltipText = confidence >= 0.85f ? "High confidence - AI is very sure about this position"
                    : confidence >= 0.6f ? "Medium confidence - position may need adjustment"
                    : "Low confidence - verify and adjust position manually";
                GUI.Label(barRect, new GUIContent($"{confidence:P0}", tooltipText), percentStyle);
            }
        }

        private void SelectLight(int index) {
            selectedLightIndex = index;
            var light = result.lights[index];

            // Focus scene view
            if (SceneView.lastActiveSceneView != null && light.enabled) {
                SceneView.lastActiveSceneView.LookAt(light.FinalWorldPosition, SceneView.lastActiveSceneView.rotation, 2f);
            }

            // Auto-switch view tab
            if (light.view == "front") currentViewTab = ViewTab.Front;
            else if (light.view == "rear") currentViewTab = ViewTab.Rear;

            Repaint();
        }

        private string GetLightIcon(string lightType) {
            if (LightTypeIcons.TryGetValue(lightType?.ToLower() ?? "", out string icon))
                return icon;
            return "\u25CF";  // Default filled circle
        }

        private Color GetConfidenceColor(float confidence) {
            if (confidence >= 0.85f) return EnabledColor;
            if (confidence >= 0.6f) return new Color(1f, 0.8f, 0.2f);
            return new Color(1f, 0.4f, 0.3f);
        }

        /// <summary>
        /// Gets the typical/expected light count for a vehicle type.
        /// </summary>
        private int GetExpectedLightCount(string vehicleType) {
            switch (vehicleType?.ToLower()) {
                case "sedan":
                case "coupe":
                case "hatchback":
                case "wagon":
                case "suv":
                case "crossover":
                return 8;  // 2 headlights, 2 indicators front, 2 taillights, 2 indicators rear
                case "truck":
                case "pickup":
                return 6;  // Simpler light setup
                case "van":
                case "minivan":
                return 8;
                case "sports":
                case "supercar":
                return 10; // May have extra lights
                case "motorcycle":
                case "bike":
                return 4;  // 1 headlight, 1 taillight, 2 indicators
                default:
                return 6;  // Conservative default
            }
        }

        #endregion

        #region Quick Actions Bar

        /// <summary>
        /// Draws contextual quick actions for the selected light.
        /// </summary>
        private void DrawQuickActionsBar() {
            if (selectedLightIndex < 0 || selectedLightIndex >= result.lights.Count) return;

            var light = result.lights[selectedLightIndex];
            int pairIndex = GetPairIndex(selectedLightIndex);
            bool hasPair = pairIndex >= 0;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Selected light info
            string icon = GetLightIcon(light.lightType);
            Color typeColor = RCCP_AIVisionLightDetector_V2.GetLightColor(light.lightType);
            GUIStyle infoStyle = new GUIStyle(EditorStyles.boldLabel) {
                normal = { textColor = typeColor }
            };

            string sideLabel = light.side?.ToLower() == "left" ? "Left" : (light.side?.ToLower() == "right" ? "Right" : "Center");
            EditorGUILayout.LabelField($"{icon} {sideLabel}", infoStyle, GUILayout.Width(60));

            GUILayout.FlexibleSpace();

            // Mirror to pair button
            if (hasPair) {
                if (GUILayout.Button("Mirror \u21C4", EditorStyles.toolbarButton, GUILayout.Width(65))) {
                    MirrorToPair(selectedLightIndex);
                }
            }

            // Reset offset button
            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(50))) {
                ResetLightOffset(selectedLightIndex);
            }

            // Enable/Disable toggle button
            GUI.color = light.enabled ? EnabledColor : new Color(0.7f, 0.3f, 0.3f);
            if (GUILayout.Button(light.enabled ? "\u2713 On" : "\u2717 Off", EditorStyles.toolbarButton, GUILayout.Width(45))) {
                light.enabled = !light.enabled;
                if (symmetryEnabled && hasPair) {
                    result.lights[pairIndex].enabled = light.enabled;
                }
                Repaint();
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void ResetLightOffset(int lightIndex) {
            if (lightIndex < 0 || lightIndex >= result.lights.Count) return;

            var light = result.lights[lightIndex];
            light.userOffset = Vector3.zero;
            UpdateWorldPosition(light);

            // Also reset pair if symmetry enabled
            if (symmetryEnabled && HasPair(lightIndex)) {
                int pairIndex = GetPairIndex(lightIndex);
                if (pairIndex >= 0) {
                    result.lights[pairIndex].userOffset = Vector3.zero;
                    UpdateWorldPosition(result.lights[pairIndex]);
                }
            }

            Repaint();
        }

        #endregion

        #region Offset Panel

        private void DrawOffsetPanel() {
            // Collapsed state when no selection
            if (selectedLightIndex < 0 || selectedLightIndex >= result.lights.Count) {
                GUIStyle emptyBoxStyle = new GUIStyle(EditorStyles.helpBox) {
                    padding = new RectOffset(8, 8, 6, 6)
                };
                GUIStyle emptyTitleStyle = new GUIStyle(EditorStyles.boldLabel) {
                    fontSize = RCCP_AIDesignSystem.Typography.SizeBase
                };
                GUIStyle emptyHintStyle = new GUIStyle(EditorStyles.miniLabel) {
                    normal = { textColor = MutedTextColor }
                };

                EditorGUILayout.BeginVertical(emptyBoxStyle);
                EditorGUILayout.LabelField("Position Adjustment", emptyTitleStyle);
                EditorGUILayout.LabelField("Select a light in the list or preview to adjust offsets.", emptyHintStyle);
                EditorGUILayout.EndVertical();
                return;
            }

            GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeMD
            };
            GUIStyle panelStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(8, 8, 6, 6)
            };
            GUIStyle fieldLabelStyle = new GUIStyle(EditorStyles.miniLabel) {
                normal = { textColor = MutedTextColor }
            };

            EditorGUILayout.LabelField("Position Adjustment", sectionTitleStyle);

            EditorGUILayout.BeginVertical(panelStyle);

            {
                var light = result.lights[selectedLightIndex];
                bool hasPair = HasPair(selectedLightIndex);
                int pairIndex = GetPairIndex(selectedLightIndex);

                // Light info with icon
                string icon = GetLightIcon(light.lightType);
                EditorGUILayout.LabelField($"{icon} {light.lightType} ({light.side})", EditorStyles.boldLabel);

                // Pair info
                if (hasPair && pairIndex >= 0) {
                    var pairLight = result.lights[pairIndex];
                    EditorGUILayout.BeginHorizontal();
                    GUI.color = AccentBlue;
                    EditorGUILayout.LabelField($"\u2194 Paired: {pairLight.lightType} ({pairLight.side})", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);

                // Symmetry toggle with Smart Pair button
                if (hasPair) {
                    EditorGUILayout.BeginHorizontal();
                    symmetryEnabled = EditorGUILayout.Toggle(symmetryEnabled, GUILayout.Width(18));
                    EditorGUILayout.LabelField("Sync pair (mirror X)", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    // Smart Pair button - copy position to pair
                    if (GUILayout.Button("\u21C4 Mirror", EditorStyles.miniButton, GUILayout.Width(55))) {
                        MirrorToPair(selectedLightIndex);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);

                // Type and Side dropdowns
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Type", fieldLabelStyle, GUILayout.Width(35));
                string[] types = { "headlight_low", "headlight_high", "brakelight", "taillight", "indicator", "reverse" };
                int currentType = Array.IndexOf(types, light.lightType?.ToLower() ?? "");
                if (currentType < 0) currentType = 0;
                int newType = EditorGUILayout.Popup(currentType, types, GUILayout.Width(100));
                if (newType != currentType) {
                    light.lightType = types[newType];
                    if (symmetryEnabled && hasPair && pairIndex >= 0) {
                        result.lights[pairIndex].lightType = types[newType];
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Side", fieldLabelStyle, GUILayout.Width(35));
                string[] sides = { "left", "right", "center" };
                int currentSide = Array.IndexOf(sides, light.side?.ToLower() ?? "");
                if (currentSide < 0) currentSide = 0;
                int newSide = EditorGUILayout.Popup(currentSide, sides, GUILayout.Width(60));
                if (newSide != currentSide) {
                    light.side = sides[newSide];
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space3);

                // Offset controls with nudge buttons
                // Display offsets in local/vehicle space for user clarity
                EditorGUILayout.LabelField("Offset (vehicle-relative):", EditorStyles.miniBoldLabel);

                Vector3 localOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);
                DrawOffsetRow("X", localOffset.x, hasPair, pairIndex, true);
                DrawOffsetRow("Y", localOffset.y, hasPair, pairIndex, false);
                DrawOffsetRow("Z", localOffset.z, hasPair, pairIndex, false);

                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

                // Reset button
                if (GUILayout.Button("Reset Offset", EditorStyles.miniButton)) {
                    light.userOffset = Vector3.zero;
                    if (symmetryEnabled && hasPair && pairIndex >= 0) {
                        result.lights[pairIndex].userOffset = Vector3.zero;
                        UpdateWorldPosition(result.lights[pairIndex]);
                    }
                    UpdateWorldPosition(light);
                }

                EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

                // Compact position info
                DrawPositionInfo(light);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a single offset axis row with nudge buttons and slider.
        /// Values are displayed in local/vehicle space for user clarity.
        /// </summary>
        private void DrawOffsetRow(string axis, float localValue, bool hasPair, int pairIndex, bool mirrorForPair) {
            EditorGUILayout.BeginHorizontal();

            // Axis label with direction hint
            string axisLabel = axis == "X" ? "X:" : (axis == "Y" ? "Y:" : "Z:");
            EditorGUILayout.LabelField(axisLabel, GUILayout.Width(18));

            // Nudge buttons (-)
            if (GUILayout.Button("\u25C0\u25C0", EditorStyles.miniButtonLeft, GUILayout.Width(28))) {
                ApplyLocalNudge(axis, -NUDGE_AMOUNT_LARGE, hasPair, pairIndex, mirrorForPair);
            }
            if (GUILayout.Button("\u25C0", EditorStyles.miniButtonMid, GUILayout.Width(20))) {
                ApplyLocalNudge(axis, -NUDGE_AMOUNT, hasPair, pairIndex, mirrorForPair);
            }

            // Slider without label (use GUILayout.HorizontalSlider for cleaner look)
            Rect sliderRect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.horizontalSlider, GUILayout.MinWidth(80), GUILayout.Height(RCCP_AIDesignSystem.Heights.Field));
            float newValue = GUI.HorizontalSlider(sliderRect, localValue, -1f, 1f);
            if (!Mathf.Approximately(newValue, localValue)) {
                ApplyLocalOffsetChange(axis, newValue, hasPair, pairIndex, mirrorForPair);
            }

            // Nudge buttons (+)
            if (GUILayout.Button("\u25B6", EditorStyles.miniButtonMid, GUILayout.Width(20))) {
                ApplyLocalNudge(axis, NUDGE_AMOUNT, hasPair, pairIndex, mirrorForPair);
            }
            if (GUILayout.Button("\u25B6\u25B6", EditorStyles.miniButtonRight, GUILayout.Width(28))) {
                ApplyLocalNudge(axis, NUDGE_AMOUNT_LARGE, hasPair, pairIndex, mirrorForPair);
            }

            // Editable value field (2 decimal places)
            float editedValue = EditorGUILayout.FloatField(localValue, GUILayout.Width(50));
            if (!Mathf.Approximately(editedValue, localValue)) {
                ApplyLocalOffsetChange(axis, Mathf.Clamp(editedValue, -1f, 1f), hasPair, pairIndex, mirrorForPair);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Applies a nudge delta to the offset in local/vehicle space.
        /// </summary>
        private void ApplyLocalNudge(string axis, float delta, bool hasPair, int pairIndex, bool mirrorForPair) {
            var light = result.lights[selectedLightIndex];

            // Get current offset in local space
            Vector3 localOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);
            float currentValue = 0f;

            switch (axis) {
                case "X": currentValue = localOffset.x; break;
                case "Y": currentValue = localOffset.y; break;
                case "Z": currentValue = localOffset.z; break;
            }

            ApplyLocalOffsetChange(axis, Mathf.Clamp(currentValue + delta, -1f, 1f), hasPair, pairIndex, mirrorForPair);
        }

        /// <summary>
        /// Applies a new offset value in local/vehicle space.
        /// Transforms to world space before storing.
        /// </summary>
        private void ApplyLocalOffsetChange(string axis, float newLocalValue, bool hasPair, int pairIndex, bool mirrorForPair) {
            var light = result.lights[selectedLightIndex];

            // Get current offset in local space
            Vector3 localOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);

            // Update the appropriate axis
            switch (axis) {
                case "X": localOffset.x = newLocalValue; break;
                case "Y": localOffset.y = newLocalValue; break;
                case "Z": localOffset.z = newLocalValue; break;
            }

            // Transform back to world space and store
            light.userOffset = result.vehicle.transform.TransformVector(localOffset);

            // Sync with pair if enabled
            if (symmetryEnabled && hasPair && pairIndex >= 0) {
                Vector3 pairLocalOffset = localOffset;
                if (mirrorForPair && axis == "X") {
                    pairLocalOffset.x = -newLocalValue;
                }
                result.lights[pairIndex].userOffset = result.vehicle.transform.TransformVector(pairLocalOffset);
                UpdateWorldPosition(result.lights[pairIndex]);
            }

            UpdateWorldPosition(light);
        }

        private void MirrorToPair(int lightIndex) {
            if (!HasPair(lightIndex)) return;

            int pairIndex = GetPairIndex(lightIndex);
            if (pairIndex < 0) return;

            var light = result.lights[lightIndex];
            var pairLight = result.lights[pairIndex];

            // Mirror X in local space, copy Y and Z
            Vector3 localOffset = result.vehicle.transform.InverseTransformVector(light.userOffset);
            Vector3 mirroredLocalOffset = new Vector3(-localOffset.x, localOffset.y, localOffset.z);
            pairLight.userOffset = result.vehicle.transform.TransformVector(mirroredLocalOffset);

            UpdateWorldPosition(pairLight);
            Repaint();
        }

        private void DrawPositionInfo(RCCP_AIVisionLightDetector_V2.DetectedLight light) {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle compactStyle = new GUIStyle(EditorStyles.miniLabel) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeXS
            };

            // Single line format with clean decimals
            Vector3 norm = new Vector3(light.normalizedX, light.normalizedY, light.normalizedZ);
            Vector3 local = light.localPosition;
            Vector3 world = light.FinalWorldPosition;

            EditorGUILayout.LabelField($"Norm: ({norm.x:F2}, {norm.y:F2}, {norm.z:F2})", compactStyle);
            EditorGUILayout.LabelField($"Local: ({local.x:F2}, {local.y:F2}, {local.z:F2})", compactStyle);
            EditorGUILayout.LabelField($"World: ({world.x:F2}, {world.y:F2}, {world.z:F2})", compactStyle);

            EditorGUILayout.EndVertical();
        }

        private void UpdateWorldPosition(RCCP_AIVisionLightDetector_V2.DetectedLight light) {
            // Recalculate world position with scale and offset
            RecalculateLightWorldPosition(light);
            Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Recalculates world position for a single light using current scale factors.
        /// </summary>
        private void RecalculateLightWorldPosition(RCCP_AIVisionLightDetector_V2.DetectedLight light) {
            if (result?.vehicle == null) return;

            // Apply scale to normalized coordinates (scale from center 0.5)
            float scaledX = 0.5f + (light.normalizedX - 0.5f) * positionScaleX;
            float scaledY = 0.5f + (light.normalizedY - 0.5f) * positionScaleY;

            // Convert image X to vehicle X (front view is mirrored)
            float vehicleX = scaledX;
            if (light.view?.ToLower() == "front") {
                vehicleX = 1.0f - scaledX;
            }

            // Calculate local position from scaled normalized coordinates
            light.localPosition = new Vector3(
                Mathf.Lerp(result.localBounds.min.x, result.localBounds.max.x, vehicleX),
                Mathf.Lerp(result.localBounds.min.y, result.localBounds.max.y, scaledY),
                Mathf.Lerp(result.localBounds.min.z, result.localBounds.max.z, light.normalizedZ)
            );

            // Transform to world space
            light.worldPosition = result.vehicle.transform.TransformPoint(light.localPosition);
        }

        /// <summary>
        /// Recalculates world positions for all lights using current scale factors.
        /// Called when scale sliders change.
        /// </summary>
        private void RecalculateAllWorldPositions() {
            if (result?.lights == null) return;

            foreach (var light in result.lights) {
                RecalculateLightWorldPosition(light);
            }

            Repaint();
            SceneView.RepaintAll();
        }

        #endregion

        #region Footer

        private void DrawFooter() {
            // Info notification about adding lights later
            GUIStyle tipBoxStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(8, 8, 4, 4)
            };
            EditorGUILayout.BeginVertical(tipBoxStyle);

            GUIStyle infoStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = MutedTextColor }
            };

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("\u2139", GUILayout.Width(16));  // ℹ info icon
            EditorGUILayout.LabelField(
                "<b>Tip:</b> After applying, you can add missing lights or fine-tune positions by selecting your vehicle " +
                "and expanding the <b>RCCP_Lights</b> component in the Inspector. You can add, edit, or remove lights there.",
                infoStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Help text for controls with keyboard shortcuts
            EditorGUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);
            GUIStyle helpStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) {
                richText = true,
                normal = { textColor = MutedTextColor }
            };
            EditorGUILayout.LabelField("Click markers to select \u2022 Drag to reposition \u2022 <b>\u2190\u2192</b> Navigate \u2022 <b>1/2/3</b> Switch view \u2022 <b>Space</b> Toggle \u2022 <b>R</b> Reset", helpStyle);
        }

        #endregion

        #region Scene View Gizmos

        private void OnSceneGUI(SceneView sceneView) {
            if (result == null || result.lights == null || result.vehicle == null)
                return;

            // Draw vehicle bounds
            Handles.color = Color.cyan;
            Matrix4x4 matrix = result.vehicle.transform.localToWorldMatrix;
            Handles.matrix = matrix;
            Handles.DrawWireCube(result.localBounds.center, result.localBounds.size);
            Handles.matrix = Matrix4x4.identity;

            // Draw each light
            for (int i = 0; i < result.lights.Count; i++) {
                var light = result.lights[i];
                if (!light.enabled) continue;

                bool isSelected = (i == selectedLightIndex);
                bool hasPair = HasPair(i);
                int pairIndex = GetPairIndex(i);
                bool isPairOfSelected = pairIndex >= 0 && pairIndex == selectedLightIndex;
                Vector3 pos = light.FinalWorldPosition;

                // Get direction for disc normal
                Vector3 normal = light.view == "front"
                    ? result.vehicle.transform.forward
                    : -result.vehicle.transform.forward;

                Color color = RCCP_AIVisionLightDetector_V2.GetLightColor(light.lightType);
                float discSize = isSelected ? 0.14f : (isPairOfSelected ? 0.11f : 0.08f);

                // Selection glow (yellow for selected)
                if (isSelected) {
                    Handles.color = new Color(1f, 1f, 0.5f, 0.25f);
                    Handles.DrawSolidDisc(pos, normal, discSize * 2.2f);
                }
                // Pair highlight glow (cyan for paired light when its pair is selected)
                else if (isPairOfSelected && symmetryEnabled) {
                    Handles.color = new Color(0.3f, 0.8f, 1f, 0.2f);
                    Handles.DrawSolidDisc(pos, normal, discSize * 2f);
                }

                // Filled disc
                Handles.color = color;
                Handles.DrawSolidDisc(pos, normal, discSize * 0.8f);

                // Outline (yellow for selected, cyan for pair, black otherwise)
                if (isSelected) {
                    Handles.color = SelectedColor;
                    Handles.DrawWireDisc(pos, normal, discSize);
                    Handles.DrawWireDisc(pos, normal, discSize * 1.2f);
                } else if (isPairOfSelected && symmetryEnabled) {
                    Handles.color = new Color(0.3f, 0.8f, 1f, 1f);
                    Handles.DrawWireDisc(pos, normal, discSize);
                    Handles.DrawWireDisc(pos, normal, discSize * 1.15f);
                } else {
                    Handles.color = new Color(0, 0, 0, 0.8f);
                    Handles.DrawWireDisc(pos, normal, discSize);
                }

                // Draw connection line between selected light and its pair
                if (isSelected && symmetryEnabled && hasPair && pairIndex >= 0) {
                    var pairLight = result.lights[pairIndex];
                    if (pairLight.enabled) {
                        Vector3 pairPos = pairLight.FinalWorldPosition;
                        Handles.color = new Color(0.3f, 0.8f, 1f, 0.5f);
                        Handles.DrawDottedLine(pos, pairPos, 4f);
                    }
                }

                // Position handle for selected light
                if (isSelected) {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) {
                        // Calculate delta and apply as offset (world space)
                        Vector3 delta = newPos - light.worldPosition;
                        light.userOffset = delta;

                        // Sync with pair if symmetry is enabled
                        // Mirror local X, keep local Y and Z the same
                        if (symmetryEnabled && hasPair && pairIndex >= 0) {
                            Vector3 localDelta = result.vehicle.transform.InverseTransformVector(delta);
                            Vector3 mirroredLocalDelta = new Vector3(-localDelta.x, localDelta.y, localDelta.z);
                            result.lights[pairIndex].userOffset = result.vehicle.transform.TransformVector(mirroredLocalDelta);
                        }

                        Repaint();
                    }
                }

                // Label
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel) {
                    normal = { textColor = isSelected ? SelectedColor : (isPairOfSelected && symmetryEnabled ? new Color(0.3f, 0.8f, 1f) : color) },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = isSelected || isPairOfSelected ? FontStyle.Bold : FontStyle.Normal
                };
                string pairIndicator = hasPair ? " [P]" : "";
                Handles.Label(pos + Vector3.up * 0.15f, $"{light.lightType}{pairIndicator}\n({light.side})", labelStyle);

                // Clickable area
                Handles.color = new Color(1, 1, 1, 0.01f);
                if (Handles.Button(pos, Quaternion.identity, discSize, discSize * 1.5f, Handles.SphereHandleCap)) {
                    selectedLightIndex = i;
                    Repaint();
                }
            }
        }

        #endregion
    }

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
