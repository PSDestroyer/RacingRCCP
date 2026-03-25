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

namespace BoneCrackerGames.RCCP.AIAssistant {

    public partial class RCCP_AIAssistantWindow {

        // Sidebar banner texture
        private Texture2D sidebarBannerTexture;

        #region Navigation & Panel Switching

        /// <summary>
        /// Switches to a specific panel type
        /// </summary>
        private void SwitchToPanel(RCCP_AIPromptAsset.PanelType targetType) {
            if (availablePrompts == null) return;

            // Prevent programmatic tab switching during AI request processing
            if (isProcessing || isBatchProcessing) {
                ShowAnimatedStatus("Cannot switch tabs while processing", MessageType.Warning);
                return;
            }

            for (int i = 0; i < availablePrompts.Length; i++) {
                if (availablePrompts[i].panelType == targetType) {
                    SaveCurrentPanelState();
                    currentPromptIndex = i;
                    RCCP_AIEditorPrefs.SelectedPanel = currentPromptIndex;
                    RestorePanelState(i);
                    ClearStatus();
                    RefreshSelection();

                    // Force Configure mode for VehicleCreation panel (Ask mode not supported)
                    if (targetType == RCCP_AIPromptAsset.PanelType.VehicleCreation) {
                        promptMode = RCCP_AIConfig.PromptMode.Request;
                        RCCP_AIEditorPrefs.PromptMode = (int)promptMode;
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Saves the current panel's state before switching to another panel.
        /// </summary>
        private void SaveCurrentPanelState() {
            if (!panelStates.ContainsKey(currentPromptIndex)) {
                panelStates[currentPromptIndex] = new PanelState();
            }

            var state = panelStates[currentPromptIndex];
            state.userPrompt = userPrompt;
            state.aiResponse = aiResponse;
            state.beforeStateSnapshot = beforeStateSnapshot;
            state.showPreview = showPreview;
            state.changesApplied = changesApplied;

            // Exit review mode when switching panels
            ExitReviewMode();
        }

        /// <summary>
        /// Restores a panel's state when switching to it.
        /// </summary>
        private void RestorePanelState(int panelIndex) {
            if (panelStates.ContainsKey(panelIndex)) {
                var state = panelStates[panelIndex];
                userPrompt = state.userPrompt;
                aiResponse = state.aiResponse;
                beforeStateSnapshot = state.beforeStateSnapshot;
                showPreview = state.showPreview;
                changesApplied = state.changesApplied;
            } else {
                // No saved state - clear to defaults
                userPrompt = "";
                aiResponse = "";
                beforeStateSnapshot = "";
                showPreview = false;
                changesApplied = false;
            }

            // Auto-apply should be enabled by default for Vehicle Creation panel only
            if (availablePrompts != null && panelIndex < availablePrompts.Length) {
                var prompt = availablePrompts[panelIndex];
                if (prompt != null && prompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation) {
                    autoApply = true;
                } else {
                    autoApply = false;
                }
            }
        }

        private void ClearStatus() {
            statusMessage = "";
            statusType = MessageType.None;
        }

        #endregion

        #region UI Drawing - Sidebar

        private void DrawSidebar() {
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.Sidebar, GUILayout.Width(sidebarWidth), GUILayout.MinWidth(180f), GUILayout.MaxWidth(sidebarWidth), GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));

            // Sidebar background
            Rect sidebarRect = GUILayoutUtility.GetRect(sidebarWidth, position.height, GUILayout.Width(sidebarWidth), GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(sidebarRect, SidebarBgColor);

            GUILayout.Space(-sidebarRect.height);

            EditorGUILayout.BeginVertical(GUILayout.Width(sidebarWidth), GUILayout.MinWidth(180f), GUILayout.MaxWidth(sidebarWidth), GUILayout.ExpandWidth(false));
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);

            // Logo/Title - sidebar banner
            if (sidebarBannerTexture == null) {
                sidebarBannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/RCCP AI Assistant/Editor/Resources/Generated/sidebar_banner.png");
            }

            if (sidebarBannerTexture != null) {
                float bannerWidth = sidebarWidth - 32; // Account for padding
                float bannerHeight = bannerWidth * sidebarBannerTexture.height / sidebarBannerTexture.width;

                // Reserve space and draw centered
                Rect rect = GUILayoutUtility.GetRect(sidebarWidth, bannerHeight);
                float drawX = rect.x + (rect.width - bannerWidth) / 2;
                Rect drawRect = new Rect(drawX, rect.y, bannerWidth, bannerHeight);

                GUI.DrawTexture(drawRect, sidebarBannerTexture, ScaleMode.ScaleToFit);
            } else {
                // Fallback to text if banner not found
                GUILayout.Label("RCCP AI", RCCP_AIDesignSystem.LabelTitleAccent);
                GUILayout.Label("Assistant", RCCP_AIDesignSystem.LabelSecondary);
            }

            RCCP_AIDesignSystem.Space(S5);
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S5);

            // Navigation
            sidebarScrollPosition = EditorGUILayout.BeginScrollView(sidebarScrollPosition, GUIStyle.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (availablePrompts != null) {
                // During refinement, use saved panel index to prevent UI flicker
                int displayPanelIndex = isExecutingRefinement && refinementRestoreInfo.HasValue
                    ? refinementRestoreInfo.Value.panelIndex
                    : currentPromptIndex;

                for (int i = 0; i < availablePrompts.Length; i++) {
                    var prompt = availablePrompts[i];
                    bool isActive = i == displayPanelIndex;

                    GUIStyle style = isActive ? sidebarButtonActiveStyle : sidebarButtonStyle;

                    // Use custom icon if available
                    GUIContent buttonContent = GetPanelIconContent(prompt);

                    // Reserve space for the button
                    Rect btnRect = GUILayoutUtility.GetRect(buttonContent, style, GUILayout.Height(RCCP_AIDesignSystem.Heights.SidebarItem));

                    // Draw selection background for active item
                    if (isActive) {
                        // Draw rounded pill selection background
                        Rect pillRect = new Rect(btnRect.x + 4, btnRect.y + 1, btnRect.width - 8, btnRect.height - 2);
                        RCCP_AIDesignSystem.DrawRoundedRect(pillRect, RCCP_AIDesignSystem.Colors.BgSelected, 6);

                        // Draw accent indicator on the left (inside the pill)
                        float barWidth = 3f;
                        float barHeight = btnRect.height * 0.6f;

                        if (enableAnimations) {
                            float animatedHeight = barHeight * panelTransitionAlpha;
                            float yOffset = (btnRect.height - animatedHeight) * 0.5f;
                            Rect accentRect = new Rect(btnRect.x + 8, btnRect.y + yOffset, barWidth, animatedHeight);
                            RCCP_AIDesignSystem.DrawRoundedRect(accentRect, AccentColor, 1);
                        } else {
                            float yOffset = (btnRect.height - barHeight) * 0.5f;
                            Rect accentRect = new Rect(btnRect.x + 8, btnRect.y + yOffset, barWidth, barHeight);
                            RCCP_AIDesignSystem.DrawRoundedRect(accentRect, AccentColor, 1);
                        }
                    }

                    // Draw the button (transparent when active to show background)
                    GUI.backgroundColor = isActive ? Color.clear : Color.white;
                    if (GUI.Button(btnRect, buttonContent, style)) {
                        if (!isActive) {
                            // Prevent tab switching during AI request processing
                            if (isProcessing || isBatchProcessing) {
                                ShowAnimatedStatus("Cannot switch tabs while processing", MessageType.Warning);
                                GUI.backgroundColor = Color.white;
                                continue;
                            }

                            // Save current panel's state before switching
                            SaveCurrentPanelState();

                            currentPromptIndex = i;

                            // Save panel selection via centralized RCCP_AIEditorPrefs
                            RCCP_AIEditorPrefs.SelectedPanel = currentPromptIndex;

                            // Restore new panel's state
                            RestorePanelState(i);

                            ClearStatus();
                            RefreshSelection();

                            // Force Configure mode for VehicleCreation panel (Ask mode not supported)
                            if (availablePrompts[i].panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation) {
                                promptMode = RCCP_AIConfig.PromptMode.Request;
                                RCCP_AIEditorPrefs.PromptMode = (int)promptMode;
                            }
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            // Bottom buttons
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S5);

            // Vehicle History button - show count if vehicle has history
            string historyText = "  Vehicle History";
            if (HasRCCPController) {
                var history = selectedController.GetComponent<RCCP_AIHistory>();
                if (history != null && history.Count > 0) {
                    historyText = $"  Vehicle History ({history.Count})";
                }
            }
            if (GUILayout.Button(new GUIContent($"📋 {historyText}"), sidebarButtonStyle)) {
                showHistory = true;
                showPromptHistory = false;
                selectedHistoryIndex = -1;
            }

            // Prompt History button - show count
            int promptHistoryCount = RCCP_AIPromptHistory.Count;
            string promptHistoryText = promptHistoryCount > 0
                ? $"  Prompt History ({promptHistoryCount})"
                : "  Prompt History";
            if (GUILayout.Button(new GUIContent($"💬 {promptHistoryText}"), sidebarButtonStyle)) {
                showPromptHistory = true;
                showHistory = false;
                selectedPromptHistoryId = null;
            }

            if (GUILayout.Button(new GUIContent("⚙  Settings"), sidebarButtonStyle)) {
                showSettings = true;
            }

            // Restart button (developer mode only)
            if (developerMode) {
                if (GUILayout.Button(new GUIContent("🔄  Restart"), sidebarButtonStyle)) {
                    RestartWindow();
                }
            }

            RCCP_AIDesignSystem.Space(S5);

            // API Status indicator
            DrawAPIStatus();

            RCCP_AIDesignSystem.Space(S6);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void DrawAPIStatus() {
            EditorGUILayout.BeginHorizontal();

            bool hasKey = HasValidAuth;
            bool isServerProxy = RCCP_AISettings.Instance?.useServerProxy ?? false;
            Color statusColor = hasKey ? DS.Success : DS.Error;
            string statusText = hasKey ? (isServerProxy ? "🛜 Server Proxy" : "🛜 API Ready") : "No API Key";

            // Draw status pill
            GUILayout.Label(statusText, hasKey ? RCCP_AIDesignSystem.PillSuccess : RCCP_AIDesignSystem.PillError);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Get GUIContent with icon for a panel (for sidebar buttons).
        /// Uses the emoji icon from the prompt asset if available.
        /// </summary>
        private GUIContent GetPanelIconContent(RCCP_AIPromptAsset prompt) {
            string panelName = prompt?.panelName ?? "Unknown";

            // Use the emoji icon from the prompt asset, or fall back to panel type icons
            string icon = !string.IsNullOrEmpty(prompt?.panelIcon)
                ? prompt.panelIcon
                : GetPanelTypeIcon(prompt?.panelType ?? RCCP_AIPromptAsset.PanelType.Generic);

            return new GUIContent($"{icon}  {panelName}");
        }

        /// <summary>
        /// Returns an emoji icon for each panel type as fallback.
        /// </summary>
        private string GetPanelTypeIcon(RCCP_AIPromptAsset.PanelType panelType) {
            switch (panelType) {
                case RCCP_AIPromptAsset.PanelType.VehicleCreation:
                return "🚗";
                case RCCP_AIPromptAsset.PanelType.VehicleCustomization:
                return "🔧";
                case RCCP_AIPromptAsset.PanelType.Behaviors:
                return "🎚️";
                case RCCP_AIPromptAsset.PanelType.Wheels:
                return "⚙️";
                case RCCP_AIPromptAsset.PanelType.Audio:
                return "🔊";
                case RCCP_AIPromptAsset.PanelType.Lights:
                return "💡";
                case RCCP_AIPromptAsset.PanelType.Damage:
                return "💥";
                case RCCP_AIPromptAsset.PanelType.Diagnostics:
                return "🩺";
                default:
                return "📋";
            }
        }

        #endregion

        #region UI Drawing - Header

        private void DrawHeader() {
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);

            // Define styles for the banner
            GUIStyle bannerStyle = new GUIStyle(GUI.skin.box);
            bannerStyle.padding = new RectOffset(16, 16, 12, 12);
            bannerStyle.margin = new RectOffset(16, 16, 0, 10);
            bannerStyle.normal.background = RCCP_AIDesignSystem.GetTexture(RCCP_AIDesignSystem.Colors.BgElevated);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = RCCP_AIDesignSystem.Typography.SizeXL;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.normal.textColor = RCCP_AIDesignSystem.Colors.GetTextPrimary();

            GUIStyle descStyle = new GUIStyle(EditorStyles.label);
            descStyle.wordWrap = true;
            descStyle.normal.textColor = RCCP_AIDesignSystem.Colors.GetTextSecondary();
            descStyle.fontSize = RCCP_AIDesignSystem.Typography.SizeMD;

            EditorGUILayout.BeginVertical(bannerStyle);
            {
                EditorGUILayout.BeginHorizontal();

                if (CurrentPrompt != null) {
                    // Draw emoji icon and panel name
                    string icon = !string.IsNullOrEmpty(CurrentPrompt.panelIcon) ? CurrentPrompt.panelIcon : "";
                    string title = CurrentPrompt.panelName;

                    if (!string.IsNullOrEmpty(icon)) {
                        GUILayout.Label(icon, new GUIStyle(titleStyle) { fontSize = RCCP_AIDesignSystem.Typography.Size2XL }, GUILayout.Width(30), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button));
                        RCCP_AIDesignSystem.Space(S4);
                    }

                    GUILayout.Label(title, titleStyle);
                }

                GUILayout.FlexibleSpace();

                // Version indicator badge
                DrawVersionIndicator();

                RCCP_AIDesignSystem.Space(S4);

                // Window mode toggle button (dock/undock)
                DrawWindowModeToggle();

                EditorGUILayout.EndHorizontal();

                // Description
                if (CurrentPrompt != null && !string.IsNullOrEmpty(CurrentPrompt.panelDescription)) {
                    RCCP_AIDesignSystem.Space(S3);
                    GUILayout.Label(CurrentPrompt.panelDescription, descStyle);
                }
            }
            EditorGUILayout.EndVertical();

            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);
        }

        /// <summary>
        /// Draws the RCCP version indicator badge.
        /// Shows green checkmark for V2.2+, warning icon for V2.0.
        /// </summary>
        private void DrawVersionIndicator() {
            var (icon, text, isWarning) = RCCP_AIVersionDetector.GetVersionBadge();

            Color badgeColor = isWarning ? RCCP_AIDesignSystem.Colors.Warning : RCCP_AIDesignSystem.Colors.Success;
            Color bgColor = RCCP_AIDesignSystem.Colors.WithAlpha(badgeColor, 0.15f);

            string tooltip = isWarning
                ? "RCCP V2.0 detected - some features limited. Upgrade to V2.2+ recommended."
                : "RCCP V2.2+ detected - all features available.";

            GUIStyle badgeStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = RCCP_AIDesignSystem.Typography.SizeXS,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(6, 6, 2, 2),
                normal = { textColor = badgeColor }
            };

            GUIContent content = new GUIContent($"{icon} {text}", tooltip);
            Vector2 size = badgeStyle.CalcSize(content);

            Rect rect = GUILayoutUtility.GetRect(size.x + 4, 18);

            // Background
            EditorGUI.DrawRect(rect, bgColor);

            // Border
            float t = 1;
            Color borderColor = RCCP_AIDesignSystem.Colors.WithAlpha(badgeColor, 0.3f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), borderColor);

            // Text
            GUI.Label(rect, content, badgeStyle);
        }

        /// <summary>
        /// Draws the window mode toggle button (dock/undock).
        /// </summary>
        private void DrawWindowModeToggle() {
            bool isUtility = RCCP_AIEditorPrefs.UseUtilityWindow;

            // Icon and tooltip based on current mode
            string icon = isUtility ? "⊞" : "📌";
            string tooltip = isUtility
                ? "Switch to dockable window (resizable)"
                : "Switch to fixed window (non-dockable)";

            GUIStyle buttonStyle = new GUIStyle(RCCP_AIDesignSystem.ButtonSmall) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
                padding = new RectOffset(6, 6, 2, 2)
            };

            if (GUILayout.Button(new GUIContent(icon, tooltip), buttonStyle, GUILayout.Width(28), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
                ToggleWindowMode();
            }
        }

        /// <summary>
        /// Toggles between utility (fixed) and regular (dockable) window mode.
        /// </summary>
        private void ToggleWindowMode() {
            bool newMode = !RCCP_AIEditorPrefs.UseUtilityWindow;
            RCCP_AIEditorPrefs.UseUtilityWindow = newMode;

            // Store current position
            Vector2 pos = position.position;

            // Close current window
            Close();

            // Reopen in new mode after delay
            EditorApplication.delayCall += () => {
                var window = GetWindow<RCCP_AIAssistantWindow>(newMode, "RCCP AI Assistant");
                window.position = new Rect(pos.x, pos.y, WindowSize.x, WindowSize.y);
            };
        }

        #endregion

    }

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
