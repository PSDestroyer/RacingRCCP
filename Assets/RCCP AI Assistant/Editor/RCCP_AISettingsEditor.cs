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

/// <summary>
/// Custom inspector for RCCP_AISettings ScriptableObject.
/// Uses RCCP_AIDesignSystem for consistent styling with the AI Assistant window.
/// </summary>
[CustomEditor(typeof(RCCP_AISettings))]
public class RCCP_AISettingsEditor : Editor {

    // Foldout states
    private bool foldoutApiConfig = true;
    private bool foldoutPrompts = false;
    private bool foldoutWelcome = false;
    private bool foldoutUI = false;
    private bool foldoutGlobalPrompts = false;
    private bool foldoutDeveloper = false;

    // Scroll position
    private Vector2 scrollPosition;

    // Cached reference
    private RCCP_AISettings settings;

    private void OnEnable() {
        settings = (RCCP_AISettings)target;
    }

    public override bool RequiresConstantRepaint() {
        return RCCP_AIEditorPrefs.ForceRepaint || base.RequiresConstantRepaint();
    }

    public override void OnInspectorGUI() {
        if (settings == null) {
            settings = (RCCP_AISettings)target;
            if (settings == null) return;
        }

        // Apply custom GUISkin if available
        GUISkin oldSkin = GUI.skin;
        if (settings.customSkin != null) {
            GUI.skin = settings.customSkin;
        }

        serializedObject.Update();

        // Header
        DrawHeader();

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Main content in scroll view
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // API Configuration Section
        DrawFoldoutSection(ref foldoutApiConfig, "API Configuration", DrawApiConfigSection,
            "Configure API endpoints, models, and token limits");

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Panel Prompts Section
        DrawFoldoutSection(ref foldoutPrompts, "Panel Prompts", DrawPromptsSection,
            "View and manage AI prompt assets for each panel");

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Welcome & Onboarding Section
        DrawFoldoutSection(ref foldoutWelcome, "Welcome & Onboarding", DrawWelcomeSection,
            "Configure welcome screen behavior");

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // UI Customization Section
        DrawFoldoutSection(ref foldoutUI, "UI Customization", DrawUISection,
            "Customize the AI Assistant interface");

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Global Prompt Parts Section
        DrawFoldoutSection(ref foldoutGlobalPrompts, "Global Prompt Parts", DrawGlobalPromptsSection,
            "Add prefix/suffix text to ALL prompts");

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Developer Options Section
        DrawFoldoutSection(ref foldoutDeveloper, "Developer Options", DrawDeveloperSection,
            "Advanced debugging and development settings");

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space7);

        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();

        // Restore original skin
        GUI.skin = oldSkin;
    }

    private new void DrawHeader() {
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelElevated);

        EditorGUILayout.BeginHorizontal();

        // Title
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = RCCP_AIDesignSystem.Colors.GetTextPrimary();

        GUILayout.Label("RCCP AI Settings", titleStyle);

        GUILayout.FlexibleSpace();

        // Open Assistant button
        if (GUILayout.Button("Open AI Assistant", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button), GUILayout.Width(120))) {
            EditorApplication.ExecuteMenuItem("Tools/BoneCracker Games/RCCP AI Assistant/Open Assistant");
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Description
        GUIStyle descStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            wordWrap = true
        };
        descStyle.normal.textColor = RCCP_AIDesignSystem.Colors.GetTextSecondary();
        GUILayout.Label("Main configuration for the RCCP AI Setup Assistant.", descStyle);

        RCCP_AIDesignSystem.EndPanel();
    }

    private void DrawFoldoutSection(ref bool foldout, string title, System.Action drawContent, string tooltip) {
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelElevated);

        // Wrap foldout in horizontal with left padding to keep arrow inside panel
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2); // Left padding for arrow
        GUIContent headerContent = new GUIContent(title, tooltip);
        foldout = EditorGUILayout.Foldout(foldout, headerContent, true, RCCP_AIDesignSystem.FoldoutHeader);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (foldout) {
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Use explicit spacing for content indentation
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space7); // Left indent for content
            EditorGUILayout.BeginVertical();

            drawContent?.Invoke();

            EditorGUILayout.EndVertical();
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4); // Right padding
            EditorGUILayout.EndHorizontal();
        }

        RCCP_AIDesignSystem.EndPanel();
    }

    private void DrawApiConfigSection() {
        // API Endpoint
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("API Endpoint", "Direct Claude API endpoint URL"), GUILayout.Width(120));
        EditorGUI.BeginChangeCheck();
        string newEndpoint = EditorGUILayout.TextField(settings.apiEndpoint);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change API Endpoint");
            settings.apiEndpoint = newEndpoint;
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Server Proxy Settings
        GUILayout.Label("Server Proxy", EditorStyles.boldLabel);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Use Server Proxy toggle
        EditorGUI.BeginChangeCheck();
        bool newUseProxy = EditorGUILayout.Toggle(
            new GUIContent("Use Server Proxy", "Route requests through your server for usage tracking"),
            settings.useServerProxy);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Toggle Server Proxy");
            settings.useServerProxy = newUseProxy;
            EditorUtility.SetDirty(settings);
        }

        // Server URL
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Server URL", "URL of your proxy server API endpoint"), GUILayout.Width(120));
        EditorGUI.BeginChangeCheck();
        string newServerUrl = EditorGUILayout.TextField(settings.serverUrl);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change Server URL");
            settings.serverUrl = newServerUrl;
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();

        // Server Timeout
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Timeout (seconds)", "Timeout in seconds for server requests"), GUILayout.Width(120));
        EditorGUI.BeginChangeCheck();
        int newTimeout = EditorGUILayout.IntSlider(settings.serverTimeout, 30, 300);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change Server Timeout");
            settings.serverTimeout = newTimeout;
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Model Selection
        GUILayout.Label("Model Selection", EditorStyles.boldLabel);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Text Model
        DrawModelDropdown("Text Model", "Model used for text-based queries",
            settings.textModel, (newModel) => {
                Undo.RecordObject(settings, "Change Text Model");
                settings.textModel = newModel;
                EditorUtility.SetDirty(settings);
            });

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Vision Model
        DrawModelDropdown("Vision Model", "Model used for vision/image analysis",
            settings.visionModel, (newModel) => {
                Undo.RecordObject(settings, "Change Vision Model");
                settings.visionModel = newModel;
                EditorUtility.SetDirty(settings);
            });

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Token Settings
        GUILayout.Label("Token Settings", EditorStyles.boldLabel);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Max Tokens
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Max Tokens", "Maximum tokens for response"), GUILayout.Width(120));
        EditorGUI.BeginChangeCheck();
        int newMaxTokens = EditorGUILayout.IntField(settings.maxTokens);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change Max Tokens");
            settings.maxTokens = Mathf.Clamp(newMaxTokens, 100, 100000);
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();

        // Max Prompt Length
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Max Prompt Length", "Maximum character limit for user prompt"), GUILayout.Width(120));
        EditorGUI.BeginChangeCheck();
        int newMaxPrompt = EditorGUILayout.IntField(settings.maxPromptLength);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change Max Prompt Length");
            settings.maxPromptLength = Mathf.Clamp(newMaxPrompt, 100, 10000);
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawModelDropdown(string label, string tooltip, string currentModel, System.Action<string> onChange) {
        int currentIndex = System.Array.IndexOf(RCCP_AISettings.AvailableModels, currentModel);
        if (currentIndex < 0) currentIndex = 1; // Default to Sonnet

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(120));

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup(currentIndex, RCCP_AISettings.ModelDisplayNames);
        if (EditorGUI.EndChangeCheck()) {
            onChange?.Invoke(RCCP_AISettings.AvailableModels[newIndex]);
        }
        EditorGUILayout.EndHorizontal();

        // Cost indicator
        float costMult = RCCP_AISettings.ModelCostMultipliers[newIndex];
        string costLabel = costMult < 1f ? $"~{costMult:P0} of Sonnet cost" :
                          costMult > 1f ? $"~{costMult:F0}x Sonnet cost" : "Baseline cost";
        GUIStyle costStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall);
        costStyle.normal.textColor = costMult <= 1f ? RCCP_AIDesignSystem.Colors.Success : RCCP_AIDesignSystem.Colors.Warning;
        EditorGUILayout.LabelField($"   {costLabel}", costStyle);
    }

    private void DrawPromptsSection() {
        if (settings.prompts == null || settings.prompts.Length == 0) {
            EditorGUILayout.HelpBox("No prompt assets configured.", MessageType.Warning);
            return;
        }

        GUILayout.Label($"Loaded Prompts: {settings.prompts.Length}", RCCP_AIDesignSystem.LabelSmall);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        for (int i = 0; i < settings.prompts.Length; i++) {
            var prompt = settings.prompts[i];
            if (prompt == null) {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem));
                GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

                GUIStyle errorStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary);
                errorStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Error;
                GUILayout.Label("Missing", errorStyle, GUILayout.Width(15));
                GUILayout.Label($"Prompt [{i}] is null", RCCP_AIDesignSystem.LabelSmall);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                continue;
            }

            DrawPromptRow(prompt);
        }

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Edit prompts array
        EditorGUI.BeginChangeCheck();
        SerializedProperty promptsProp = serializedObject.FindProperty("prompts");
        EditorGUILayout.PropertyField(promptsProp, new GUIContent("Prompts Array"), true);
        if (EditorGUI.EndChangeCheck()) {
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawPromptRow(RCCP_AIPromptAsset prompt) {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem));
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Status indicator
        bool isValid = prompt.IsValid(out string validationMsg);
        int tokens = prompt.EstimatedTokens;
        bool isDiagnostics = prompt.panelType == RCCP_AIPromptAsset.PanelType.Diagnostics;

        string statusIcon;
        Color statusColor;
        string statusTooltip;

        if (!isValid && !isDiagnostics) {
            statusIcon = "X";
            statusColor = RCCP_AIDesignSystem.Colors.Error;
            statusTooltip = $"Invalid: {validationMsg}";
        } else if (tokens == 0 && !isDiagnostics) {
            statusIcon = "!";
            statusColor = RCCP_AIDesignSystem.Colors.Warning;
            statusTooltip = "Not configured - no system prompt defined";
        } else if (isDiagnostics) {
            statusIcon = "O";
            statusColor = RCCP_AIDesignSystem.Colors.Info;
            statusTooltip = "Local diagnostics (no AI required)";
        } else {
            statusIcon = "O";
            statusColor = RCCP_AIDesignSystem.Colors.Success;
            statusTooltip = "Loaded and valid";
        }

        GUIStyle iconStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontStyle = FontStyle.Bold,
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase
        };
        iconStyle.normal.textColor = statusColor;
        GUILayout.Label(new GUIContent(statusIcon, statusTooltip), iconStyle, GUILayout.Width(15));

        // Clickable prompt name
        GUIStyle linkStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            clipping = TextClipping.Clip
        };
        linkStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Link;
        linkStyle.hover.textColor = RCCP_AIDesignSystem.Colors.LinkHover;

        string displayName = $"{prompt.panelIcon} {prompt.panelName}";
        if (GUILayout.Button(new GUIContent(displayName, "Click to select this prompt asset"), linkStyle, GUILayout.Width(160))) {
            Selection.activeObject = prompt;
            EditorGUIUtility.PingObject(prompt);
        }

        // Token count
        string tokenText;
        if (isDiagnostics) {
            tokenText = "Local only";
        } else if (tokens == 0) {
            tokenText = "Not configured";
        } else {
            tokenText = $"~{tokens} tokens";
        }

        GUIStyle tokenStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall);
        tokenStyle.normal.textColor = isDiagnostics ? RCCP_AIDesignSystem.Colors.Info :
                                      tokens == 0 ? RCCP_AIDesignSystem.Colors.Warning :
                                      RCCP_AIDesignSystem.Colors.GetTextSecondary();
        tokenStyle.fontStyle = (tokens == 0 && !isDiagnostics) ? FontStyle.Italic : FontStyle.Normal;

        GUILayout.Label(tokenText, tokenStyle, GUILayout.Width(100));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawWelcomeSection() {
        // Show Welcome on Startup
        EditorGUI.BeginChangeCheck();
        bool newShowWelcome = EditorGUILayout.Toggle(
            new GUIContent("Show Welcome on Startup", "Display the welcome panel when opening the AI Assistant for the first time"),
            settings.showWelcomeOnStartup);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Toggle Show Welcome");
            settings.showWelcomeOnStartup = newShowWelcome;
            EditorUtility.SetDirty(settings);
        }

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        GUIStyle descStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            wordWrap = true
        };
        descStyle.normal.textColor = RCCP_AIDesignSystem.Colors.GetTextSecondary();
        GUILayout.Label("Controls whether the welcome panel appears on first use. User-specific 'has seen' state is stored in EditorPrefs.", descStyle);
    }

    private void DrawUISection() {
        // Custom Skin
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Custom Skin", "Custom GUISkin to apply to the AI Assistant window"), GUILayout.Width(120));
        EditorGUI.BeginChangeCheck();
        GUISkin newSkin = (GUISkin)EditorGUILayout.ObjectField(settings.customSkin, typeof(GUISkin), false);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change Custom Skin");
            settings.customSkin = newSkin;
            EditorUtility.SetDirty(settings);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Send on Enter
        EditorGUI.BeginChangeCheck();
        bool newSendOnEnter = EditorGUILayout.Toggle(
            new GUIContent("Send on Enter", "If enabled, pressing Enter will send the prompt. Use Shift+Enter for new line."),
            settings.sendOnEnter);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Toggle Send on Enter");
            settings.sendOnEnter = newSendOnEnter;
            EditorUtility.SetDirty(settings);
        }
    }

    private void DrawGlobalPromptsSection() {
        GUIStyle descStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            wordWrap = true
        };
        descStyle.normal.textColor = RCCP_AIDesignSystem.Colors.GetTextSecondary();

        // Global Prefix
        GUILayout.Label(new GUIContent("Global Prefix", "Text added to the START of ALL prompts"), EditorStyles.boldLabel);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);
        GUILayout.Label("This text is prepended to every system prompt before sending to the AI.", descStyle);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        EditorGUI.BeginChangeCheck();
        string newPrefix = EditorGUILayout.TextArea(settings.globalPrefix, GUILayout.MinHeight(60));
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change Global Prefix");
            settings.globalPrefix = newPrefix;
            EditorUtility.SetDirty(settings);
        }

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.PanelPadding);

        // Global Suffix
        GUILayout.Label(new GUIContent("Global Suffix", "Text added to the END of ALL prompts"), EditorStyles.boldLabel);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);
        GUILayout.Label("This text is appended to every system prompt before sending to the AI.", descStyle);
        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        EditorGUI.BeginChangeCheck();
        string newSuffix = EditorGUILayout.TextArea(settings.globalSuffix, GUILayout.MinHeight(40));
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Change Global Suffix");
            settings.globalSuffix = newSuffix;
            EditorUtility.SetDirty(settings);
        }
    }

    private void DrawDeveloperSection() {
        // Verbose Logging
        EditorGUI.BeginChangeCheck();
        bool newVerbose = EditorGUILayout.Toggle(
            new GUIContent("Verbose Logging", "Enable verbose logging to console for debugging"),
            settings.verboseLogging);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(settings, "Toggle Verbose Logging");
            settings.verboseLogging = newVerbose;
            EditorUtility.SetDirty(settings);
        }

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // TLS Validation Warning
        if (settings.debugDisableTLSValidation) {
            EditorGUILayout.HelpBox("TLS Validation is DISABLED! This is insecure and should only be used for local development.", MessageType.Error);
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);
        }

        // Disable TLS Validation
        EditorGUI.BeginChangeCheck();
        bool newDisableTLS = EditorGUILayout.Toggle(
            new GUIContent("Disable TLS Validation", "DANGEROUS: Disable TLS certificate validation. Only use for local development with self-signed certificates."),
            settings.debugDisableTLSValidation);
        if (EditorGUI.EndChangeCheck()) {
            if (newDisableTLS) {
                // Show confirmation dialog
                if (EditorUtility.DisplayDialog("DANGEROUS: Disable TLS Validation",
                    "Disabling TLS validation is insecure and should ONLY be used for local development with self-signed certificates.\n\nReal API keys and data could be intercepted by third parties in this mode. Are you sure?",
                    "Disable Security", "Cancel")) {
                    Undo.RecordObject(settings, "Disable TLS Validation");
                    settings.debugDisableTLSValidation = true;
                    EditorUtility.SetDirty(settings);
                }
            } else {
                Undo.RecordObject(settings, "Enable TLS Validation");
                settings.debugDisableTLSValidation = false;
                EditorUtility.SetDirty(settings);
            }
        }

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space4);

        GUIStyle warningStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            wordWrap = true
        };
        warningStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Warning;
        GUILayout.Label("Additional developer options are available in the AI Assistant window's Settings panel.", warningStyle);
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
