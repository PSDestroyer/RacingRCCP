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
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using static BoneCrackerGames.RCCP.AIAssistant.RCCP_AIPromptAsset;

namespace BoneCrackerGames.RCCP.AIAssistant {

public partial class RCCP_AIAssistantWindow {

    #region UI Drawing - Settings

    private void DrawSettingsPanel() {
        EditorGUILayout.BeginVertical();

        // Header with status bar
        RCCP_AIDesignSystem.Space(S6);
        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S7);

        if (GUILayout.Button("← Back", GUILayout.Width(70), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            showSettings = false;
            LoadSettings();
        }

        RCCP_AIDesignSystem.Space(S6);
        GUILayout.Label("Settings", headerStyle);
        GUILayout.FlexibleSpace();

        // Status indicator in header
        DrawApiStatusIndicator();

        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S7);

        mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.BeginVertical();

        // RCCP Version Warning (V2.0 compatibility mode)
#if !RCCP_V2_2_OR_NEWER
        EditorGUILayout.HelpBox(
            "RCCP V2.0 detected. Some features are limited:\n" +
            "- Wheel grip property not available\n" +
            "- Diagnostics use simplified checks\n\n" +
            "Upgrade to RCCP V2.2+ for full functionality.",
            MessageType.Warning);
        RCCP_AIDesignSystem.Space(S5);
#endif

        // Verification Status (always visible at top - protects entire asset)
        DrawVerificationStatusSection();
        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // API Configuration (Collapsible)
        DrawSettingsFoldoutSection(ref foldoutApiConfig, "🔑  API Configuration", DrawApiConfigSection,
            "Configure API access method and settings",
            v => RCCP_AIEditorPrefs.FoldoutApiConfig = v);

        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // Prompt Assets (Collapsible)
        DrawSettingsFoldoutSection(ref foldoutPromptAssets, "📝  Prompt Assets", DrawPromptAssetsSection,
            "View and manage AI prompt configurations",
            v => RCCP_AIEditorPrefs.FoldoutPromptAssets = v);

        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // UI Settings (Collapsible)
        DrawSettingsFoldoutSection(ref foldoutUISettings, "🎨  UI Settings", DrawUISettingsSection,
            "Customize quick prompts and input behavior",
            v => RCCP_AIEditorPrefs.FoldoutUISettings = v);

        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // Welcome & Help (Collapsible)
        DrawSettingsFoldoutSection(ref foldoutWelcomeHelp, "👋  Welcome & Help", DrawWelcomeHelpSection,
            "Show welcome screen and get help",
            v => RCCP_AIEditorPrefs.FoldoutWelcomeHelp = v);

        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // Animation Settings (Collapsible)
        DrawSettingsFoldoutSection(ref foldoutAnimSettings, "✨  Animation Settings", DrawAnimationSettingsSection,
            "Control UI animations and transitions",
            v => RCCP_AIEditorPrefs.FoldoutAnimSettings = v);

        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // Keyboard Shortcuts (Collapsible)
        DrawSettingsFoldoutSection(ref foldoutShortcuts, "⌨  Keyboard Shortcuts", DrawKeyboardShortcutsSection,
            "Configure keyboard shortcuts to quickly open the AI Assistant",
            v => RCCP_AIEditorPrefs.FoldoutShortcuts = v);

        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // Developer Options (Collapsible)
        DrawSettingsFoldoutSection(ref foldoutDevOptions, "🛠  Developer Options", DrawDeveloperOptionsSection,
            "Advanced debugging and development tools",
            v => RCCP_AIEditorPrefs.FoldoutDevOptions = v);

        EditorGUILayout.EndVertical();
        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSettingsFoldoutSection(ref bool foldout, string title, Action drawContent, string tooltip, Action<bool> onFoldoutChanged = null) {
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelElevated);

        // Foldout header with tooltip
        EditorGUILayout.BeginHorizontal();
        GUIContent headerContent = new GUIContent(title, tooltip);

        bool previousValue = foldout;
        foldout = EditorGUILayout.Foldout(foldout, headerContent, true, RCCP_AIDesignSystem.FoldoutHeader);

        // Save foldout state if it changed
        if (foldout != previousValue) {
            onFoldoutChanged?.Invoke(foldout);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (foldout) {
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);
            drawContent?.Invoke();
        }

        RCCP_AIDesignSystem.EndPanel();
    }

    private void DrawApiStatusIndicator() {
        string statusText;
        GUIStyle pillStyle;

        // When not using own API key, show server proxy status instead
        if (!RCCP_AIUtility.UseOwnApiKey) {
            if (serverTestState == ServerTestState.Success) {
                statusText = "✅ Server";
                pillStyle = RCCP_AIDesignSystem.PillSuccess;
            } else if (serverTestState == ServerTestState.Failed) {
                statusText = "❌ Server";
                pillStyle = RCCP_AIDesignSystem.PillError;
            } else {
                statusText = "○ Server Proxy";
                pillStyle = RCCP_AIDesignSystem.PillDefault;
            }
            GUILayout.Label(statusText, pillStyle);
            return;
        }

        // Using own API key - show API key status
        switch (apiValidationState) {
            case ApiValidationState.Valid:
                statusText = "✅ Connected";
                pillStyle = RCCP_AIDesignSystem.PillSuccess;
                break;
            case ApiValidationState.Invalid:
                statusText = "❌ Invalid";
                pillStyle = RCCP_AIDesignSystem.PillError;
                break;
            case ApiValidationState.Validating:
                statusText = "🔄️ Testing...";
                pillStyle = RCCP_AIDesignSystem.PillWarning;
                break;
            default:
                statusText = string.IsNullOrEmpty(apiKey) ? "○ No Key" : "○ Not Tested";
                pillStyle = RCCP_AIDesignSystem.PillDefault;
                break;
        }

        GUILayout.Label(statusText, pillStyle);

        // Show last request time if valid
        if (apiValidationState == ApiValidationState.Valid && lastApiRequestTime > 0) {
            double timeSinceRequest = EditorApplication.timeSinceStartup - lastApiRequestTime;
            string timeAgo = timeSinceRequest < 60 ? "just now" :
                            timeSinceRequest < 3600 ? $"{(int)(timeSinceRequest / 60)}m ago" :
                            $"{(int)(timeSinceRequest / 3600)}h ago";
            GUILayout.Label($"(Last: {timeAgo})", RCCP_AIDesignSystem.LabelSmall);
        }
    }

    private void DrawApiConfigSection() {
        var aiSettings = RCCP_AISettings.Instance;

        // API Source selector - toolbar style (full width)
        int currentSource = RCCP_AIUtility.UseOwnApiKey ? 1 : 0;
        string[] sourceOptions = new string[] { "Server Proxy (Free Tier)", "Own API Key (Unlimited)" };

        EditorGUI.BeginChangeCheck();
        int newSource = GUILayout.Toolbar(currentSource, sourceOptions, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction));
        if (EditorGUI.EndChangeCheck()) {
            bool usesOwnKey = (newSource == 1);
            RCCP_AIUtility.UseOwnApiKey = usesOwnKey;
            // Sync the server proxy setting
            if (aiSettings != null) {
                Undo.RecordObject(aiSettings, "Change API Source");
                aiSettings.useServerProxy = (newSource == 0);
                EditorUtility.SetDirty(aiSettings);
            }
            // Notify server of API mode change
            if (RCCP_ServerProxy.IsRegistered) {
                RCCP_ServerProxy.SetOwnKey(this, usesOwnKey, (success, message) => {
                    if (!success)
                        Debug.LogWarning($"[RCCP AI] Failed to sync API mode with server: {message}");
                });
            }
        }

        RCCP_AIDesignSystem.Space(S5);

        // TWO COLUMN LAYOUT
        EditorGUILayout.BeginHorizontal();

        // LEFT COLUMN - Connection Settings (fixed width)
        EditorGUILayout.BeginVertical(GUILayout.Width(280));

        GUILayout.Label("Connection", EditorStyles.boldLabel);
        RCCP_AIDesignSystem.Space(S2);

        if (RCCP_AIUtility.UseOwnApiKey) {
            DrawOwnApiKeySettings();
        } else {
            DrawServerProxySettings();
        }

        GUILayout.FlexibleSpace(); // Anchor to top
        EditorGUILayout.EndVertical();

        // VERTICAL SEPARATOR
        RCCP_AIDesignSystem.Space(S4);
        Rect separatorRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(separatorRect, RCCP_AIDesignSystem.Colors.BorderLight);
        RCCP_AIDesignSystem.Space(S4);

        // RIGHT COLUMN - Model Selection & Usage (expand to fill)
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        GUILayout.Label("Model Selection", EditorStyles.boldLabel);
        RCCP_AIDesignSystem.Space(S2);

        DrawModelSelectionSection();

        // Only show usage section when using server proxy
        if (!RCCP_AIRateLimiter.UseOwnApiKey) {
            RCCP_AIDesignSystem.Space(S4);
            GUILayout.Label("Usage", EditorStyles.boldLabel);
            RCCP_AIDesignSystem.Space(S2);
            DrawApiUsageSection();
        }

        GUILayout.FlexibleSpace(); // Anchor to top
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawOwnApiKeySettings() {
        // API Key field
        EditorGUILayout.BeginHorizontal();
        GUIContent apiKeyLabel = new GUIContent("API Key", "Your Anthropic API key for Claude AI access");
        GUILayout.Label(apiKeyLabel, GUILayout.MinWidth(60), GUILayout.MaxWidth(100), GUILayout.ExpandWidth(false));
        var fieldStyle = new GUIStyle(RCCP_AIDesignSystem.TextField);
        fieldStyle.normal.textColor = RCCP_AIDesignSystem.Colors.TextPrimary;
        fieldStyle.focused.textColor = RCCP_AIDesignSystem.Colors.TextPrimary;
        apiKey = showApiKey
            ? EditorGUILayout.TextField(apiKey, fieldStyle)
            : EditorGUILayout.PasswordField(apiKey, fieldStyle);
        if (GUILayout.Button(showApiKey ? "Hide" : "Show", GUILayout.Width(50))) {
            showApiKey = !showApiKey;
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        // Help link - styled as clickable URL
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Get your API key from", RCCP_AIDesignSystem.LabelSmall, GUILayout.MinWidth(100), GUILayout.ExpandWidth(false));

        // Create a link-style button that looks clickable
        GUIStyle linkStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Info },
            hover = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Info, 0.2f) },
            active = { textColor = RCCP_AIDesignSystem.Colors.Darken(RCCP_AIDesignSystem.Colors.Info, 0.1f) },
            fontStyle = FontStyle.Normal,
            stretchWidth = false
        };

        GUIContent linkContent = new GUIContent("console.anthropic.com", "Click to open Anthropic Console in browser");
        Rect linkRect = GUILayoutUtility.GetRect(linkContent, linkStyle);

        // Draw underline to make it look like a hyperlink
        EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
        if (GUI.Button(linkRect, linkContent, linkStyle)) {
            Application.OpenURL("https://console.anthropic.com");
        }

        // Draw underline
        Rect underlineRect = new Rect(linkRect.x, linkRect.yMax - 1, linkRect.width, 1);
        EditorGUI.DrawRect(underlineRect, RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Info, 0.6f));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S5);

        // Button row (horizontal grouping)
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Save API Key", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
            RCCP_AIEditorPrefs.ApiKey = apiKey;
            apiValidationState = ApiValidationState.Unknown;
            SetStatus("API Key saved!", MessageType.Info);
        }

        RCCP_AIDesignSystem.Space(S2);

        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(apiKey) || apiValidationState == ApiValidationState.Validating);
        if (GUILayout.Button("Test Connection", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
            TestApiConnection();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        // Validation result
        if (!string.IsNullOrEmpty(apiValidationMessage)) {
            RCCP_AIDesignSystem.Space(S4);
            Color msgColor = apiValidationState == ApiValidationState.Valid
                ? RCCP_AIDesignSystem.Colors.Success
                : apiValidationState == ApiValidationState.Invalid
                    ? RCCP_AIDesignSystem.Colors.Error
                    : RCCP_AIDesignSystem.Colors.TextSecondary;

            GUIStyle msgStyle = new GUIStyle(RCCP_AIDesignSystem.PanelElevated) {
                richText = true,
                padding = new RectOffset(8, 8, 6, 6)
            };

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = msgColor * 0.3f + Color.black * 0.7f;
            GUILayout.Label(apiValidationMessage, msgStyle);
            GUI.backgroundColor = oldBg;
        }
    }

    private void DrawServerProxySettings() {
        var aiSettings = RCCP_AISettings.Instance;
        if (aiSettings == null) {
            EditorGUILayout.HelpBox("Settings asset not found.", MessageType.Error);
            return;
        }

        // Server URL (collapsed by default, expandable for advanced users)
        if (developerMode) {
            EditorGUILayout.LabelField("Server URL", RCCP_AIDesignSystem.LabelSmall);
            string newUrl = EditorGUILayout.TextField(aiSettings.serverUrl);
            if (newUrl != aiSettings.serverUrl) {
                Undo.RecordObject(aiSettings, "Change Server URL");
                aiSettings.serverUrl = newUrl;
                EditorUtility.SetDirty(aiSettings);
            }

            RCCP_AIDesignSystem.Space(S2);

            // Server Timeout
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Timeout (seconds)", GUILayout.Width(120));
            int newTimeout = EditorGUILayout.IntSlider(aiSettings.serverTimeout, 30, 300);
            if (newTimeout != aiSettings.serverTimeout) {
                Undo.RecordObject(aiSettings, "Change Server Timeout");
                aiSettings.serverTimeout = newTimeout;
                EditorUtility.SetDirty(aiSettings);
            }
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S5);
        }

        // Test Server Button
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(serverTestState == ServerTestState.Testing || string.IsNullOrEmpty(aiSettings.serverUrl));
        if (GUILayout.Button("Test Server Connection", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
            TestServerConnection();
        }
        EditorGUI.EndDisabledGroup();

        // Clear Registration button (developer mode only)
        if (developerMode) {
            if (GUILayout.Button("Clear Registration", GUILayout.Width(120), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
                if (EditorUtility.DisplayDialog("Clear Registration",
                    "This will clear your device registration. You'll be registered again on next use.\n\nContinue?",
                    "Clear", "Cancel")) {
                    RCCP_ServerProxy.ClearRegistration();
                    serverTestState = ServerTestState.Unknown;
                    serverTestMessage = "Registration cleared.";
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // Test result
        if (!string.IsNullOrEmpty(serverTestMessage)) {
            RCCP_AIDesignSystem.Space(S4);
            Color msgColor = serverTestState == ServerTestState.Success
                ? RCCP_AIDesignSystem.Colors.Success
                : serverTestState == ServerTestState.Failed
                    ? RCCP_AIDesignSystem.Colors.Error
                    : RCCP_AIDesignSystem.Colors.TextSecondary;

            GUIStyle msgStyle = new GUIStyle(RCCP_AIDesignSystem.PanelElevated) {
                richText = true,
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = msgColor * 0.3f + Color.black * 0.7f;
            GUILayout.Label(serverTestMessage, msgStyle);
            GUI.backgroundColor = oldBg;
        }

        // Registration status (Compact)
        RCCP_AIDesignSystem.Space(S2);
        string regStatus = RCCP_ServerProxy.IsRegistered ? "Active" : "Inactive";
        string tokenInfo = (RCCP_ServerProxy.IsRegistered && developerMode) ? $" ({RCCP_ServerProxy.DeviceToken.Substring(0, Mathf.Min(8, RCCP_ServerProxy.DeviceToken.Length))}...)" : "";
        
        GUILayout.Label($"Registration: {regStatus}{tokenInfo}", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_ServerProxy.IsRegistered ? RCCP_AIDesignSystem.Colors.Success : RCCP_AIDesignSystem.Colors.Error }
        });
    }

    private void DrawApiUsageSection() {
        // Hide usage section entirely when using own API key
        if (RCCP_AIRateLimiter.UseOwnApiKey) {
            return;
        }

        var tier = RCCP_AIRateLimiter.GetCurrentTier();

        if (tier == RCCP_AIRateLimiter.UsageTier.SetupPhase) {
            // Setup phase - using initial pool (values from cached server data)
            int remaining = RCCP_AIRateLimiter.SetupPoolRemaining;
            int total = RCCP_AIRateLimiter.SetupPoolTotal;

            EditorGUILayout.LabelField("Tier: Setup Phase", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Setup Requests: {remaining} / {total} remaining");

            // Progress bar (shows remaining, not used)
            Rect progressRect = EditorGUILayout.GetControlRect(false, 18);
            float progress = RCCP_AIRateLimiter.GetSetupPoolProgress();
            Color barColor = RCCP_AIRateLimiter.GetUsageBarColor();

            EditorGUI.DrawRect(progressRect, RCCP_AIDesignSystem.Colors.BgRecessed);
            Rect filledRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height);
            EditorGUI.DrawRect(filledRect, barColor);

            RCCP_AIDesignSystem.Space(S2);
            GUIStyle infoStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                wordWrap = true
            };
            GUILayout.Label($"No daily limit during setup. After setup: {RCCP_AIRateLimiter.DailyLimit}/day free.", infoStyle);

            // Warning when running low
            if (remaining <= 10 && remaining > 0) {
                RCCP_AIDesignSystem.Space(S2);
                EditorGUILayout.HelpBox(
                    $"Only {remaining} setup requests left. After that, you'll have {RCCP_AIRateLimiter.DailyLimit} free requests per day.",
                    MessageType.Info);
            }

        } else {
            // Daily free tier (values from cached server data)
            int dailyUsed = RCCP_AIRateLimiter.DailyUsed;
            int dailyLimit = RCCP_AIRateLimiter.DailyLimit;
            int dailyRemaining = RCCP_AIRateLimiter.DailyRemaining;

            EditorGUILayout.LabelField("Tier: Free (Daily Limit)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Daily Requests: {dailyUsed} / {dailyLimit} used");

            // Progress bar
            Rect progressRect = EditorGUILayout.GetControlRect(false, 18);
            float progress = RCCP_AIRateLimiter.GetDailyUsageProgress();
            Color barColor = RCCP_AIRateLimiter.GetUsageBarColor();

            EditorGUI.DrawRect(progressRect, RCCP_AIDesignSystem.Colors.BgRecessed);
            Rect filledRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height);
            EditorGUI.DrawRect(filledRect, barColor);

            EditorGUILayout.LabelField($"Resets in: {RCCP_AIRateLimiter.TimeUntilDailyReset}");

            // Warnings
            if (dailyRemaining <= 3 && dailyRemaining > 0) {
                RCCP_AIDesignSystem.Space(S2);
                EditorGUILayout.HelpBox(
                    $"Only {dailyRemaining} requests remaining today. Switch to Own API Key for unlimited.",
                    MessageType.Warning);
            } else if (dailyRemaining == 0) {
                RCCP_AIDesignSystem.Space(S2);
                EditorGUILayout.HelpBox(
                    "Daily limit reached. Switch to Own API Key to continue, or wait until tomorrow.",
                    MessageType.Error);
            }
        }

        // Hourly burst indicator (values from cached server data)
        RCCP_AIDesignSystem.Space(S4);
        int hourlyUsed = RCCP_AIRateLimiter.HourlyUsed;
        int hourlyLimit = RCCP_AIRateLimiter.HourlyLimit;
        int hourlyRemaining = RCCP_AIRateLimiter.HourlyRemaining;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Burst Limit: {hourlyUsed}/{hourlyLimit} this hour", GUILayout.Width(160));

        if (hourlyRemaining == 0) {
            int minutes = Mathf.CeilToInt(RCCP_AIRateLimiter.SecondsUntilHourlyReset / 60f);
            GUIStyle waitStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Warning }
            };
            GUILayout.Label($"(resets in {minutes}m)", waitStyle);
        }
        EditorGUILayout.EndHorizontal();

        // Last sync time
        string lastSync = RCCP_AIRateLimiter.LastSyncTime;
        if (!string.IsNullOrEmpty(lastSync)) {
            RCCP_AIDesignSystem.Space(S2);
            GUIStyle syncStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
            };
            GUILayout.Label($"Last sync: {lastSync}", syncStyle);
        }

        // Debug buttons (developer mode only) - commented for potential future use
        // if (developerMode) {
        //     RCCP_AIDesignSystem.Space(S2);
        //     EditorGUILayout.BeginHorizontal();
        //     if (GUILayout.Button("Reset Dialog Tracking", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
        //         RCCP_AIRateLimiter.ResetDialogTracking();
        //         SetStatus("Dialog tracking reset - dialogs will show again", MessageType.Info);
        //     }
        //     EditorGUILayout.EndHorizontal();
        // }
    }

    private void DrawModelSelectionSection() {
        if (settings == null) {
            EditorGUILayout.HelpBox("Settings not loaded", MessageType.Warning);
            return;
        }

        // Check if using Server Proxy mode - force Haiku and disable selection
        bool isServerProxyMode = !RCCP_AIUtility.UseOwnApiKey;

        // Get current model indices
        int textModelIndex = System.Array.IndexOf(RCCP_AISettings.AvailableModels, settings.textModel);
        int visionModelIndex = System.Array.IndexOf(RCCP_AISettings.AvailableModels, settings.visionModel);

        // Default to Sonnet if not found (for API mode), or Haiku for Server Proxy
        if (textModelIndex < 0) textModelIndex = isServerProxyMode ? 0 : 1;
        if (visionModelIndex < 0) visionModelIndex = isServerProxyMode ? 0 : 1;

        // Force Haiku for Server Proxy mode
        if (isServerProxyMode) {
            // Ensure settings are set to Haiku
            if (textModelIndex != 0 || visionModelIndex != 0) {
                Undo.RecordObject(settings, "Set Server Proxy Models to Haiku");
                settings.textModel = RCCP_AISettings.AvailableModels[0]; // Haiku
                settings.visionModel = RCCP_AISettings.AvailableModels[0]; // Haiku
                EditorUtility.SetDirty(settings);
            }
            textModelIndex = 0;
            visionModelIndex = 0;
        }

        // Disable dropdowns in Server Proxy mode
        EditorGUI.BeginDisabledGroup(isServerProxyMode);

        // Text Model Dropdown
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Text Model", "Model used for text-based queries (customization, behaviors, etc.)"),
            GUILayout.Width(100));
        EditorGUI.BeginChangeCheck();
        int newTextIndex = EditorGUILayout.Popup(textModelIndex, RCCP_AISettings.ModelDisplayNames);
        if (EditorGUI.EndChangeCheck() && !isServerProxyMode) {
            Undo.RecordObject(settings, "Change Text Model");
            settings.textModel = RCCP_AISettings.AvailableModels[newTextIndex];
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();

        // Show cost indicator for text
        float textCostMult = RCCP_AISettings.ModelCostMultipliers[isServerProxyMode ? 0 : newTextIndex];
        string textCostLabel = textCostMult < 1f ? $"~{textCostMult:P0} of Sonnet cost" :
                              textCostMult > 1f ? $"~{textCostMult:F0}x Sonnet cost" : "Baseline cost";
        GUIStyle costStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = textCostMult <= 1f ? RCCP_AIDesignSystem.Colors.Success : RCCP_AIDesignSystem.Colors.Warning }
        };
        EditorGUILayout.LabelField($"   {textCostLabel}", costStyle);

        RCCP_AIDesignSystem.Space(S4);

        // Vision Model Dropdown
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Vision Model", "Model used for image analysis (light detection). Sonnet recommended for best results."),
            GUILayout.Width(100));
        EditorGUI.BeginChangeCheck();
        int newVisionIndex = EditorGUILayout.Popup(visionModelIndex, RCCP_AISettings.ModelDisplayNames);
        if (EditorGUI.EndChangeCheck() && !isServerProxyMode) {
            Undo.RecordObject(settings, "Change Vision Model");
            settings.visionModel = RCCP_AISettings.AvailableModels[newVisionIndex];
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();

        // Show cost indicator for vision
        float visionCostMult = RCCP_AISettings.ModelCostMultipliers[isServerProxyMode ? 0 : newVisionIndex];
        string visionCostLabel = visionCostMult < 1f ? $"~{visionCostMult:P0} of Sonnet cost" :
                                visionCostMult > 1f ? $"~{visionCostMult:F0}x Sonnet cost" : "Baseline cost";
        GUIStyle visionCostStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = visionCostMult <= 1f ? RCCP_AIDesignSystem.Colors.Success : RCCP_AIDesignSystem.Colors.Warning }
        };
        EditorGUILayout.LabelField($"   {visionCostLabel}", visionCostStyle);

        EditorGUI.EndDisabledGroup();

        // Server Proxy mode info message
        if (isServerProxyMode) {
            RCCP_AIDesignSystem.Space(S2);
            EditorGUILayout.HelpBox("Server Proxy mode uses Haiku for all requests. Switch to Own API Key to select different models.", MessageType.Info);
        }
        // Warning if using Haiku for vision (only in API mode)
        else if (newVisionIndex == 0) {
            RCCP_AIDesignSystem.Space(S2);
            EditorGUILayout.HelpBox("Haiku may produce lower quality results for vision tasks. Sonnet recommended.", MessageType.Warning);
        }
    }

    private void DrawPromptAssetsSection() {
        // Settings asset field with button row
        EditorGUILayout.ObjectField("Settings Asset", settings, typeof(RCCP_AISettings), false);

        RCCP_AIDesignSystem.Space(S2);

        // Button row
        EditorGUILayout.BeginHorizontal();
        if (settings != null) {
            if (GUILayout.Button("Select in Project", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            }
        }
        if (GUILayout.Button("Reload Settings", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            LoadSettings();
            SetStatus("Settings reloaded", MessageType.Info);
        }
        EditorGUILayout.EndHorizontal();

        if (availablePrompts != null && availablePrompts.Length > 0) {
            RCCP_AIDesignSystem.Space(S3);
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S3);

            GUILayout.Label("Loaded Prompts:", RCCP_AIDesignSystem.LabelSmall);
            RCCP_AIDesignSystem.Space(S2);

            foreach (var p in availablePrompts) {
                DrawPromptAssetRow(p);
            }
        }
    }

    private void DrawPromptAssetRow(RCCP_AIPromptAsset prompt) {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem)); // Fixed row height
        RCCP_AIDesignSystem.Space(S2);

        // Status indicator
        bool isValid = prompt.IsValid(out string validationMsg);
        int tokens = prompt.EstimatedTokens;
        bool isDiagnostics = prompt.panelType == PanelType.Diagnostics;

        string statusIcon;
        Color statusColor;
        string statusTooltip;

        if (!isValid && !isDiagnostics) {
            statusIcon = "✗";
            statusColor = RCCP_AIDesignSystem.Colors.Error;
            statusTooltip = $"Invalid: {validationMsg}";
        } else if (tokens == 0 && !isDiagnostics) {
            statusIcon = "⚠";
            statusColor = RCCP_AIDesignSystem.Colors.Warning;
            statusTooltip = "Not configured - no system prompt defined";
        } else if (isDiagnostics) {
            // Diagnostics is local-only, no AI prompt needed
            statusIcon = "✓";
            statusColor = RCCP_AIDesignSystem.Colors.Info;
            statusTooltip = "Local diagnostics (no AI required)";
        } else {
            statusIcon = "✓";
            statusColor = RCCP_AIDesignSystem.Colors.Success;
            statusTooltip = "Loaded and valid";
        }

        GUIStyle iconStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            normal = { textColor = statusColor },
            fontStyle = FontStyle.Bold,
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase
        };
        GUILayout.Label(new GUIContent(statusIcon, statusTooltip), iconStyle, GUILayout.Width(15));

        // Clickable prompt name with fixed width to prevent wrapping
        GUIStyle linkStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Info },
            hover = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Info, 0.2f) },
            clipping = TextClipping.Clip
        };

        string displayName = $"{prompt.panelIcon} {prompt.panelName}";
        if (GUILayout.Button(new GUIContent(displayName, "Click to edit this prompt asset"), linkStyle, GUILayout.Width(160))) {
            Selection.activeObject = prompt;
            EditorGUIUtility.PingObject(prompt);
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }

        // Token count with cost estimate - fixed width for alignment
        string tokenText;
        string costTooltip;
        if (isDiagnostics) {
            tokenText = "Local only";
            costTooltip = "Diagnostics runs locally without AI";
        } else if (tokens == 0) {
            tokenText = "Not configured";
            costTooltip = "This prompt needs to be configured";
        } else {
            float estimatedCost = tokens * 0.000003f * 2.5f; // Multiplied by 2.5 to account for user message + output tokens
            tokenText = $"~{tokens} tokens";
            costTooltip = $"Estimated cost: ~${estimatedCost:F4} per request (input + output estimate)";
        }

        GUIStyle tokenStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = isDiagnostics ? RCCP_AIDesignSystem.Colors.Info :
                                   tokens == 0 ? RCCP_AIDesignSystem.Colors.Warning : RCCP_AIDesignSystem.Colors.TextSecondary },
            fontStyle = (tokens == 0 && !isDiagnostics) ? FontStyle.Italic : FontStyle.Normal
        };

        GUILayout.Label(new GUIContent(tokenText, costTooltip), tokenStyle, GUILayout.Width(100));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawUISettingsSection() {
        // Quick Prompts slider
        EditorGUILayout.BeginHorizontal();
        GUIContent qpLabel = new GUIContent("Quick Prompts to Display",
            "Number of suggested prompts shown below the input field for quick access");
        GUILayout.Label(qpLabel, GUILayout.MinWidth(140), GUILayout.ExpandWidth(false));

        EditorGUI.BeginChangeCheck();
        quickPromptDisplayCount = EditorGUILayout.IntSlider(quickPromptDisplayCount, QUICK_PROMPT_MIN, QUICK_PROMPT_MAX);
        if (EditorGUI.EndChangeCheck()) {
            RCCP_AIEditorPrefs.QuickPromptCount = quickPromptDisplayCount;
            displayedQuickPromptIndices.Clear();
            usedQuickPromptIndices.Clear();
        }
        EditorGUILayout.EndHorizontal();

        // Description and reset button on same line
        EditorGUILayout.BeginHorizontal();
        string quickPromptDesc = quickPromptDisplayCount == 0
            ? "Quick prompts are disabled"
            : $"Showing {quickPromptDisplayCount} quick prompt suggestions per panel";
        GUILayout.Label(quickPromptDesc, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        });
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset to Default (5)", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall), GUILayout.Width(120))) {
            quickPromptDisplayCount = 5;
            RCCP_AIEditorPrefs.QuickPromptCount = 5;
            displayedQuickPromptIndices.Clear();
            usedQuickPromptIndices.Clear();
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S3);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S3);

        if (settings != null) {
            // Send on Enter toggle
            EditorGUI.BeginChangeCheck();
            GUIContent enterToggle = new GUIContent(" Send Prompt on Enter",
                "If enabled, pressing Enter will send the prompt. Shift+Enter for new line.");
            bool newSendOnEnter = GUILayout.Toggle(settings.sendOnEnter, enterToggle);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(settings, "Change Send On Enter");
                settings.sendOnEnter = newSendOnEnter;
                EditorUtility.SetDirty(settings);
            }
        }
    }

    private void DrawWelcomeHelpSection() {
        // Show Welcome button
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Welcome Screen", GUILayout.MinWidth(100), GUILayout.ExpandWidth(false));

        GUI.backgroundColor = AccentColor;
        if (GUILayout.Button("Show Welcome", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button), GUILayout.Width(120))) {
            showSettings = false;
            showWelcome = true;
        }
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);
        GUILayout.Label("View the welcome panel with feature overview and quick start guide",
            new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                wordWrap = true
            });

        RCCP_AIDesignSystem.Space(S6);

        // Show on startup toggle
        EditorGUI.BeginChangeCheck();
        GUIContent startupToggle = new GUIContent(" Show Welcome on Startup",
            "Display the welcome panel when opening the AI Assistant for the first time");
        bool showOnStartup = settings != null && settings.showWelcomeOnStartup;
        bool newShowOnStartup = GUILayout.Toggle(showOnStartup, startupToggle);
        if (EditorGUI.EndChangeCheck() && settings != null) {
            Undo.RecordObject(settings, "Change Show Welcome on Startup");
            settings.showWelcomeOnStartup = newShowOnStartup;
            EditorUtility.SetDirty(settings);
        }

        RCCP_AIDesignSystem.Space(S6);

        // Reset "has seen welcome" button (developer mode only)
        if (hasSeenWelcome && developerMode) {
            if (GUILayout.Button("Reset First-Time Experience", GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem), GUILayout.Width(180))) {
                hasSeenWelcome = false;
                RCCP_AIEditorPrefs.HasSeenWelcome = false;
                if (settings != null) {
                    Undo.RecordObject(settings, "Reset First-Time Experience");
                    settings.showWelcomeOnStartup = true;
                    EditorUtility.SetDirty(settings);
                }
            }
            GUILayout.Label("This will show the welcome panel next time you open the assistant",
                new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                    wordWrap = true
                });
        }
    }

    private void DrawAnimationSettingsSection() {
        // Enable toggle + Reset button on same line
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        GUIContent animToggle = new GUIContent(" Enable UI Animations",
            "Enable smooth transitions and visual feedback animations");
        enableAnimations = GUILayout.Toggle(enableAnimations, animToggle);
        if (EditorGUI.EndChangeCheck()) {
            RCCP_AIEditorPrefs.EnableAnimations = enableAnimations;
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset to Default", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall), GUILayout.Width(110))) {
            animationSpeed = 1.0f;
            enableAnimations = true;
            RCCP_AIEditorPrefs.AnimationSpeed = 1.0f;
            RCCP_AIEditorPrefs.EnableAnimations = true;
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S3);

        EditorGUI.BeginDisabledGroup(!enableAnimations);

        // Animation speed slider
        EditorGUILayout.BeginHorizontal();
        GUIContent speedLabel = new GUIContent("Animation Speed",
            "Controls how fast UI animations play (0.5 = slow, 2.0 = fast)");
        GUILayout.Label(speedLabel, GUILayout.MinWidth(100), GUILayout.ExpandWidth(false));

        EditorGUI.BeginChangeCheck();
        animationSpeed = EditorGUILayout.Slider(animationSpeed, ANIMATION_SPEED_MIN, ANIMATION_SPEED_MAX);
        if (EditorGUI.EndChangeCheck()) {
            RCCP_AIEditorPrefs.AnimationSpeed = animationSpeed;
        }

        // Speed description inline
        string speedDesc = animationSpeed < 0.8f ? "Slow" :
                          animationSpeed > 1.5f ? "Fast" : "Normal";
        GUILayout.Label($"({speedDesc})", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        }, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        if (!enableAnimations) {
            GUILayout.Label("Animations are disabled for better performance", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                fontStyle = FontStyle.Italic
            });
        }
    }

    private void DrawKeyboardShortcutsSection() {
        string modifierKey = Application.platform == RuntimePlatform.OSXEditor ? "Cmd" : "Ctrl";

        // Toggle + Shortcut + Edit button on same line
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        GUIContent shortcutToggle = new GUIContent(" Enable Keyboard Shortcuts",
            "Enable global keyboard shortcuts to open the AI Assistant from anywhere in the editor");
        bool enableShortcuts = RCCP_AIEditorPrefs.EnableShortcuts;
        enableShortcuts = GUILayout.Toggle(enableShortcuts, shortcutToggle, GUILayout.Width(180));
        if (EditorGUI.EndChangeCheck()) {
            RCCP_AIEditorPrefs.EnableShortcuts = enableShortcuts;
        }

        // Shortcut display
        GUIStyle shortcutDisplayStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            normal = { textColor = DS.Accent },
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD
        };
        GUILayout.Label($"{modifierKey}+Shift+W", shortcutDisplayStyle, GUILayout.Width(100));

        GUILayout.FlexibleSpace();

        // Edit button
        if (GUILayout.Button("Edit Shortcuts...", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall), GUILayout.Width(110))) {
            EditorApplication.ExecuteMenuItem("Edit/Shortcuts...");
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        // Info text (simplified)
        GUIStyle infoStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
            wordWrap = true
        };

        if (enableShortcuts) {
            GUILayout.Label($"Press {modifierKey}+Shift+W anywhere in Unity to open the AI Assistant. Search \"RCCP AI Assistant\" in Edit > Shortcuts to customize.",
                infoStyle);
        } else {
            GUILayout.Label("Shortcuts disabled. Use menu: Tools > BoneCracker Games > RCCP AI Assistant.",
                infoStyle);
        }
    }

    private void DrawDeveloperOptionsSection() {
        // Developer Mode toggle - ALWAYS visible (the gate)
        EditorGUI.BeginChangeCheck();
        GUIContent devModeToggle = new GUIContent("Developer Mode",
            "Enable advanced debugging tools: raw JSON output, server diagnostics, response timing, and more");
        developerMode = GUILayout.Toggle(developerMode, devModeToggle);
        if (EditorGUI.EndChangeCheck()) {
            RCCP_AIEditorPrefs.DeveloperMode = developerMode;
        }

        RCCP_AIDesignSystem.Space(S2);

        // Developer mode only options
        if (developerMode) {
            EditorGUILayout.BeginHorizontal();
            RCCP_AIDesignSystem.Space(S7);
            EditorGUILayout.BeginVertical();

            // ========== Logging & Diagnostics ==========
            GUILayout.Label("Logging & Diagnostics", EditorStyles.boldLabel);
            RCCP_AIDesignSystem.Space(S2);

            // Verbose Logging + Save Vision Debug Screenshots (horizontal)
            EditorGUILayout.BeginHorizontal();
            if (settings != null) {
                EditorGUI.BeginChangeCheck();
                GUIContent verboseToggle = new GUIContent("Verbose Logging",
                    "Log detailed information to Unity console for troubleshooting");
                bool newVerboseLogging = GUILayout.Toggle(settings.verboseLogging, verboseToggle, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(settings, "Change Verbose Logging");
                    settings.verboseLogging = newVerboseLogging;
                    EditorUtility.SetDirty(settings);
                }
            }

            bool saveScreenshots = RCCP_AIVisionLightDetector_V2.Instance.SaveDebugScreenshots;
            EditorGUI.BeginChangeCheck();
            GUIContent screenshotToggle = new GUIContent("Save Vision Debug Screenshots",
                $"Save captured screenshots to {RCCP_AIUtility.DebugScreenshotsPath}/ when using Vision-Based Light Detection");
            bool newSaveScreenshots = GUILayout.Toggle(saveScreenshots, screenshotToggle);
            if (EditorGUI.EndChangeCheck()) {
                RCCP_AIVisionLightDetector_V2.Instance.SaveDebugScreenshots = newSaveScreenshots;
            }
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S2);

            // Open Error Log button
            if (GUILayout.Button("Open Error Log", GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem), GUILayout.Width(150))) {
                RCCP_AIUtility.OpenLogFile();
            }

            RCCP_AIDesignSystem.Space(S4);
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S4);

            // ========== Server Diagnostics ==========
            GUILayout.Label("Server Diagnostics", EditorStyles.boldLabel);
            RCCP_AIDesignSystem.Space(S2);

            // Server info in compact horizontal layout
            EditorGUILayout.BeginHorizontal();
            string registrationStatus = RCCP_ServerProxy.IsRegistered ? "Active" : "Not Registered";
            GUILayout.Label($"Status: {registrationStatus}", GUILayout.Width(140));

            string lastSync = RCCP_AIEditorPrefs.LastServerSync;
            if (string.IsNullOrEmpty(lastSync)) lastSync = "Never";
            GUILayout.Label($"Last Sync: {lastSync}", GUILayout.Width(200));

            string token = RCCP_AIEditorPrefs.DeviceToken;
            string displayToken = string.IsNullOrEmpty(token) ? "(none)" :
                token.Length > 8 ? token.Substring(0, 8) + "..." : token;
            GUILayout.Label($"Token: {displayToken}");

            if (!string.IsNullOrEmpty(token) && GUILayout.Button("Copy", GUILayout.Width(50))) {
                EditorGUIUtility.systemCopyBuffer = token;
                Debug.Log("[RCCP AI] Device token copied to clipboard");
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S2);

            // Force Server Sync button
            if (GUILayout.Button("Force Server Sync", GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem), GUILayout.Width(150))) {
                ForceServerSync();
            }

            RCCP_AIDesignSystem.Space(S4);
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S4);

            // ========== Response Debugging ==========
            GUILayout.Label("Response Debugging", EditorStyles.boldLabel);
            RCCP_AIDesignSystem.Space(S2);

            // View Raw AI Responses + Show Request/Response Times (horizontal)
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool viewRaw = RCCP_AIEditorPrefs.ViewRawAIResponses;
            GUIContent rawToggle = new GUIContent("View Raw AI Responses",
                "Show the raw JSON response from Claude instead of parsed output");
            viewRaw = GUILayout.Toggle(viewRaw, rawToggle, GUILayout.Width(180));
            if (EditorGUI.EndChangeCheck()) {
                RCCP_AIEditorPrefs.ViewRawAIResponses = viewRaw;
            }

            EditorGUI.BeginChangeCheck();
            bool showTimes = RCCP_AIEditorPrefs.ShowRequestResponseTimes;
            GUIContent timesToggle = new GUIContent("Show Request/Response Times",
                "Display how long each API request takes");
            showTimes = GUILayout.Toggle(showTimes, timesToggle);
            if (EditorGUI.EndChangeCheck()) {
                RCCP_AIEditorPrefs.ShowRequestResponseTimes = showTimes;
            }

            // Show last response time inline if enabled
            if (showTimes) {
                long lastMs = RCCP_AIEditorPrefs.LastResponseTimeMs;
                if (lastMs > 0) {
                    GUILayout.Label($"(Last: {lastMs}ms)",
                        new GUIStyle(EditorStyles.miniLabel) {
                            normal = { textColor = RCCP_AIDesignSystem.Colors.Success }
                        });
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S4);
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S4);

            // ========== Advanced Options ==========
            GUILayout.Label("Advanced Options", EditorStyles.boldLabel);
            RCCP_AIDesignSystem.Space(S2);

            // Force Repaint + Disable TLS Validation (horizontal)
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            GUIContent forceRepaintToggle = new GUIContent("Force Continuous Repaint",
                "Always call Repaint() regardless of processing state. Useful for debugging UI update issues.");
            forceRepaint = GUILayout.Toggle(forceRepaint, forceRepaintToggle, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck()) {
                RCCP_AIEditorPrefs.ForceRepaint = forceRepaint;
            }

            if (settings != null) {
                EditorGUI.BeginChangeCheck();
                GUIContent tlsToggle = new GUIContent("Disable TLS Validation (DANGEROUS)",
                    "Bypasses SSL certificate checks. Use ONLY for local development with self-signed certificates.");

                bool toggleValue = GUILayout.Toggle(settings.debugDisableTLSValidation, tlsToggle);

                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(settings, "Change TLS Validation");
                    if (toggleValue) {
                        if (EditorUtility.DisplayDialog("DANGEROUS: Disable TLS Validation",
                            "Disabling TLS validation is insecure and should ONLY be used for local development with self-signed certificates.\n\nReal API keys and data could be intercepted by third parties in this mode. Are you sure you want to proceed?",
                            "Disable Security", "Cancel")) {
                            settings.debugDisableTLSValidation = true;
                        } else {
                            settings.debugDisableTLSValidation = false;
                        }
                    } else {
                        settings.debugDisableTLSValidation = false;
                    }
                    EditorUtility.SetDirty(settings);
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S4);

            // Extract Component Defaults button with description inline
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Extract Component Defaults", GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem), GUILayout.Width(200))) {
                RCCP_AIDefaultsExtractor.ExtractDefaults();
            }
            GUILayout.Label("Re-extract default values from RCCP components",
                new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
                });
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S4);
            RCCP_AIDesignSystem.DrawSeparator(true);
        }

        RCCP_AIDesignSystem.Space(S4);

        // Reset buttons row (horizontal)
        EditorGUILayout.BeginHorizontal();
        Color oldColor = GUI.backgroundColor;

        // Reset All Settings to Default button
        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Error;
        if (GUILayout.Button("Reset All Settings to Default", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
            if (EditorUtility.DisplayDialog("Reset Settings",
                "Are you sure you want to reset all settings to their default values?\n\nThis will clear:\n- All UI preferences\n- Device registration\n- Verification status\n\nYou will need to verify your purchase again.",
                "Reset", "Cancel")) {
                ResetSettingsToDefault();
            }
        }

        // Prepare for Release button - only visible in developer mode
        if (developerMode) {
            RCCP_AIDesignSystem.Space(S2);
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.DangerBg;
            if (GUILayout.Button("Prepare for Release", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
                string message =
                    "This will reset EVERYTHING for Asset Store release:\n\n" +
                    "- API key and device registration\n" +
                    "- All usage data and rate limits\n" +
                    "- Prompt history\n" +
                    "- UI preferences and foldout states\n" +
                    "- Developer mode (will be disabled)\n\n" +
                    "This action CANNOT be undone!";

                if (EditorUtility.DisplayDialog("Prepare for Release",
                    message,
                    "Reset Everything", "Cancel")) {
                    PrepareForRelease();
                }
            }
        }

        GUI.backgroundColor = oldColor;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Forces a sync with the server to refresh usage data.
    /// If not registered, will auto-register first.
    /// </summary>
    private void ForceServerSync() {
        if (!RCCP_ServerProxy.IsRegistered) {
            SetStatus("Registering device...", MessageType.Info);
            RCCP_ServerProxy.RegisterDevice(this, (success, message) => {
                if (success) {
                    SetStatus("Registered and synced!", MessageType.Info);
                } else {
                    SetStatus($"Registration failed: {message}", MessageType.Error);
                }
                Repaint();
            });
        } else {
            SetStatus("Syncing with server...", MessageType.Info);
            RCCP_ServerProxy.GetStatus(this, (usage) => {
                if (usage != null) {
                    RCCP_AIRateLimiter.SyncFromServer(usage);
                    SetStatus("Synced successfully!", MessageType.Info);
                } else {
                    SetStatus("Sync failed - server unavailable", MessageType.Error);
                }
                Repaint();
            });
        }
    }

    private void TestApiConnection() {
        if (string.IsNullOrEmpty(apiKey)) {
            apiValidationState = ApiValidationState.Invalid;
            apiValidationMessage = "Please enter an API key first.";
            return;
        }

        apiValidationState = ApiValidationState.Validating;
        apiValidationMessage = "Testing connection...";
        Repaint();

        EditorCoroutineUtility.StartCoroutine(TestApiConnectionCoroutine(), this);
    }

    private IEnumerator TestApiConnectionCoroutine() {
        var aiSettings = RCCP_AISettings.Instance;
        string testMessage = $"{{\"model\":\"{aiSettings.textModel}\",\"max_tokens\":10,\"messages\":[{{\"role\":\"user\",\"content\":\"Hi\"}}]}}";

        using (UnityWebRequest www = new UnityWebRequest(aiSettings.apiEndpoint, "POST")) {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(testMessage);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("x-api-key", apiKey);
            www.SetRequestHeader("anthropic-version", "2023-06-01");
            www.timeout = 15;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                apiValidationState = ApiValidationState.Valid;
                apiValidationMessage = "✓ Connection successful! Your API key is valid.";
                lastApiRequestTime = EditorApplication.timeSinceStartup;
            } else {
                apiValidationState = ApiValidationState.Invalid;
                if (www.responseCode == 401) {
                    apiValidationMessage = "✗ Invalid API key. Please check your key and try again.";
                } else if (www.responseCode == 429) {
                    apiValidationMessage = "⚠ Rate limited. Your key is valid but you've hit the rate limit.";
                    apiValidationState = ApiValidationState.Valid; // Key is still valid
                    lastApiRequestTime = EditorApplication.timeSinceStartup;
                } else {
                    apiValidationMessage = $"✗ Connection failed: {www.error} (Code: {www.responseCode})";
                }
            }
        }

        Repaint();
    }

    private void TestServerConnection() {
        serverTestState = ServerTestState.Testing;
        serverTestMessage = "Testing server connection...";
        Repaint();

        EditorCoroutineUtility.StartCoroutine(TestServerConnectionCoroutine(), this);
    }

    private IEnumerator TestServerConnectionCoroutine() {
        var aiSettings = RCCP_AISettings.Instance;
        string healthUrl = aiSettings.serverUrl + "?action=health";

        // Initialize SSL before making request
        RCCP_ServerProxy.Initialize();

        using (UnityWebRequest www = UnityWebRequest.Get(healthUrl)) {
            // Note: SSL is configured globally by RCCP_ServerProxy.Initialize()
            www.timeout = 30;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                try {
                    // Try to parse the response
                    string response = www.downloadHandler.text;
                    if (response.Contains("\"success\":true") && response.Contains("\"status\":\"ok\"")) {
                        serverTestState = ServerTestState.Success;
                        serverTestMessage = "✓ Server is online and responding correctly!";
                    } else {
                        serverTestState = ServerTestState.Success;
                        serverTestMessage = $"✓ Server responded but with unexpected format:\n{response.Substring(0, Mathf.Min(200, response.Length))}";
                    }
                } catch (Exception ex) {
                    serverTestState = ServerTestState.Failed;
                    serverTestMessage = $"✗ Failed to parse server response: {ex.Message}";
                }
            } else {
                serverTestState = ServerTestState.Failed;
                serverTestMessage = $"✗ Server connection failed: {www.error}\n\nURL: {healthUrl}\n\nMake sure the server is accessible and the URL is correct.";
            }
        }

        Repaint();
    }

    private void ResetSettingsToDefault() {
        // Reset UI settings via centralized RCCP_AIEditorPrefs
        quickPromptDisplayCount = 5;
        RCCP_AIEditorPrefs.QuickPromptCount = 5;

        // Reset animation settings
        enableAnimations = true;
        animationSpeed = 1.0f;
        RCCP_AIEditorPrefs.EnableAnimations = true;
        RCCP_AIEditorPrefs.AnimationSpeed = 1.0f;

        // Reset developer options
        developerMode = false;
        RCCP_AIEditorPrefs.DeveloperMode = false;
        forceRepaint = false;
        RCCP_AIEditorPrefs.ForceRepaint = false;
        RCCP_AIVisionLightDetector_V2.Instance.SaveDebugScreenshots = false;

        if (settings != null) {
            Undo.RecordObject(settings, "Reset Settings to Default");
            settings.verboseLogging = false;
            EditorUtility.SetDirty(settings);
        }

        // Reset keyboard shortcuts (just the enable flag - binding managed by Unity Shortcuts)
        RCCP_AIEditorPrefs.EnableShortcuts = true;

        // Reset foldout states (local and persisted)
        foldoutApiConfig = false;
        foldoutPromptAssets = false;
        foldoutUISettings = false;
        foldoutWelcomeHelp = false;
        foldoutAnimSettings = false;
        foldoutShortcuts = false;
        foldoutDevOptions = false;
        RCCP_AIEditorPrefs.FoldoutApiConfig = false;
        RCCP_AIEditorPrefs.FoldoutPromptAssets = false;
        RCCP_AIEditorPrefs.FoldoutUISettings = false;
        RCCP_AIEditorPrefs.FoldoutWelcomeHelp = false;
        RCCP_AIEditorPrefs.FoldoutAnimSettings = false;
        RCCP_AIEditorPrefs.FoldoutShortcuts = false;
        RCCP_AIEditorPrefs.FoldoutDevOptions = false;

        // Clear validation state
        apiValidationState = ApiValidationState.Unknown;
        apiValidationMessage = "";

        // Clear server test state
        serverTestState = ServerTestState.Unknown;
        serverTestMessage = "";

        // Clear quick prompt cache
        displayedQuickPromptIndices.Clear();
        usedQuickPromptIndices.Clear();

        // Clear registration and verification (user must verify again)
        RCCP_ServerProxy.ClearRegistration();
        RCCP_AIEditorPrefs.ClearVerification();

        SetStatus("Settings reset to default values", MessageType.Info);

        // Close settings so verification panel shows
        showSettings = false;
    }

    /// <summary>
    /// Resets EVERYTHING for asset release preparation.
    /// Clears all personal data, API keys, history, and resets to clean state.
    /// </summary>
    private void PrepareForRelease() {
        // Reset ALL EditorPrefs (includes API key, device token, rate limiter, UI prefs, developer options, security data)
        RCCP_AIEditorPrefs.ResetAll();

        // Explicitly clear server proxy registration (clears cached token)
        RCCP_ServerProxy.ClearRegistration();

        // Clear prompt history
        RCCP_AIPromptHistory.ClearAll();

        // Reset settings ScriptableObject
        if (settings != null) {
            Undo.RecordObject(settings, "Prepare for Release");
            settings.showWelcomeOnStartup = true;
            settings.verboseLogging = false;
            settings.debugDisableTLSValidation = false;
            settings.sendOnEnter = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Reset local window state variables
        apiKey = "";
        showApiKey = false;
        apiValidationState = ApiValidationState.Unknown;
        apiValidationMessage = "";
        serverTestState = ServerTestState.Unknown;
        serverTestMessage = "";

        // Reset UI state
        developerMode = false;
        enableAnimations = true;
        animationSpeed = RCCP_AIEditorPrefs.Defaults.AnimationSpeed;
        quickPromptDisplayCount = RCCP_AIEditorPrefs.Defaults.QuickPromptCount;
        forceRepaint = false;
        hasSeenWelcome = false;
        showWelcome = true; // Show welcome on next open

        // Reset foldout states
        foldoutApiConfig = false;
        foldoutPromptAssets = false;
        foldoutUISettings = false;
        foldoutWelcomeHelp = false;
        foldoutAnimSettings = false;
        foldoutShortcuts = false;
        foldoutDevOptions = false;

        // Clear quick prompt cache
        displayedQuickPromptIndices.Clear();
        usedQuickPromptIndices.Clear();

        // Clear vision detector debug settings
        RCCP_AIVisionLightDetector_V2.Instance.SaveDebugScreenshots = false;

        // Log completion
        Debug.Log("[RCCP AI] Asset prepared for release - all personal data and settings have been cleared.");

        // Show success and close settings
        SetStatus("Asset prepared for release! All data cleared.", MessageType.Info);
        showSettings = false;

        // Force repaint to reflect changes
        Repaint();
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
