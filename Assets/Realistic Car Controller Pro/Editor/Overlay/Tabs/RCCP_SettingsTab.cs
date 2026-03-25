//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using BoneCrackerGames.RCCP.CoreProtection;

/// <summary>
/// Settings tab content for RCCP Scene View Overlay.
/// Provides quick access to RCCP settings and API key status (if AI Assistant is installed).
/// </summary>
public class RCCP_SettingsTab : IRCCP_OverlayContent {

    #region Variables

    private VisualElement rootElement;
    private VisualElement settingsContainer;

    #endregion

    #region Interface Implementation

    public VisualElement CreateContent(string searchQuery) {

        rootElement = new VisualElement();
        rootElement.name = "rccp-settings-tab";
        rootElement.style.flexGrow = 1;
        rootElement.style.flexShrink = 1;
        rootElement.style.height = Length.Percent(100);

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        // Settings container.
        settingsContainer = new VisualElement();
        settingsContainer.name = "settings-container";
        settingsContainer.style.paddingBottom = 20 * scale;

        // Create scroll view.
        ScrollView scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.Add(settingsContainer);
        rootElement.Add(scrollView);

        // Refresh settings.
        RefreshSettings(searchQuery);

        return rootElement;

    }

    public void OnUpdate() {

        // No periodic update needed.

    }

    public void OnDestroy() {

        // Cleanup if needed.

    }

    #endregion

    #region Settings Display

    private void RefreshSettings(string searchQuery) {

        settingsContainer.Clear();

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        // AI Assistant Section (only if installed).
        if (RCCP_SceneToolsController.IsAIAssistantInstalled) {

            CreateSection("AI Assistant", settingsContainer);

            // API Key Status - check both own API key and server proxy mode.
            bool hasValidAuth = false;
            bool isServerProxy = false;
            string statusText = "Not Set";

            // Check if using server proxy mode via RCCP_AISettings
            var settingsType = System.Type.GetType("BoneCrackerGames.RCCP.AIAssistant.RCCP_AISettings, Assembly-CSharp-Editor");
            if (settingsType != null) {
                var instanceProp = settingsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp != null) {
                    var settings = instanceProp.GetValue(null);
                    if (settings != null) {
                        var useServerProxyField = settingsType.GetField("useServerProxy");
                        if (useServerProxyField != null) {
                            isServerProxy = (bool)useServerProxyField.GetValue(settings);
                        }
                    }
                }
            }

            if (isServerProxy) {
                hasValidAuth = true;
                statusText = "Server Proxy";
            } else {
                // Check for own API key
                string apiKey = EditorPrefs.GetString("RCCP_AI_ApiKey", "");
                hasValidAuth = !string.IsNullOrEmpty(apiKey);
                statusText = hasValidAuth ? "Configured" : "Not Set";
            }

            CreateSettingCard(
                "API Key Status",
                statusText,
                hasValidAuth ? "\u2705" : "\u274c",
                hasValidAuth ? new Color(0.2f, 0.5f, 0.2f, 0.15f) : new Color(0.5f, 0.2f, 0.2f, 0.15f),
                () => OpenAIAssistantWindow(),
                searchQuery
            );

            // Open AI Assistant Settings.
            CreateSettingCard(
                "AI Assistant Settings",
                "Configure API key and preferences",
                "\u2699\ufe0f",
                new Color(0.3f, 0.3f, 0.5f, 0.15f),
                () => OpenAIAssistantWindow(),
                searchQuery
            );

        }

        // Verification Status Section.
        CreateSection("Verification", settingsContainer);

        bool coreVerified = RCCP_CoreServerProxy.IsVerified;
        string verificationStatusText = coreVerified ? "Verified" : "Not verified";

        CreateSettingCard(
            "Purchase Verification",
            verificationStatusText,
            coreVerified ? "\u2705" : "\u26a0\ufe0f",
            coreVerified ? new Color(0.2f, 0.5f, 0.2f, 0.15f) : new Color(0.5f, 0.3f, 0.1f, 0.15f),
            () => { if (coreVerified) RCCP_WelcomeWindow.OpenWindow(); else RCCP_WelcomeWindow.OpenWindowWithVerification(); },
            searchQuery
        );

        // RCCP Settings Section.
        CreateSection("RCCP Settings", settingsContainer);

        // RCCP Settings Asset.
        CreateSettingCard(
            "RCCP Settings",
            RCCP_Settings.Instance != null ? "Select to edit" : "Not found",
            "\ud83d\udcdd",
            RCCP_Settings.Instance != null
                ? new Color(0.2f, 0.5f, 0.2f, 0.15f)
                : new Color(0.5f, 0.2f, 0.2f, 0.15f),
            () => {
                if (RCCP_Settings.Instance != null) {
                    Selection.activeObject = RCCP_Settings.Instance;
                    EditorGUIUtility.PingObject(RCCP_Settings.Instance);
                }
            },
            searchQuery
        );

        // Ground Materials.
        CreateSettingCard(
            "Ground Materials",
            "Surface friction database",
            "\ud83c\udf33",
            new Color(0.3f, 0.4f, 0.3f, 0.15f),
            () => {
                var groundMats = RCCP_GroundMaterials.Instance;
                if (groundMats != null) {
                    Selection.activeObject = groundMats;
                    EditorGUIUtility.PingObject(groundMats);
                }
            },
            searchQuery
        );

        // Input Actions.
        CreateSettingCard(
            "Input Actions",
            "Input system configuration",
            "\ud83c\udfae",
            new Color(0.4f, 0.3f, 0.5f, 0.15f),
            () => {
                var inputActions = RCCP_InputActions.Instance;
                if (inputActions != null) {
                    Selection.activeObject = inputActions;
                    EditorGUIUtility.PingObject(inputActions);
                }
            },
            searchQuery
        );

        // Quick Links Section.
        CreateSection("Quick Links", settingsContainer);

        // Documentation.
        CreateSettingCard(
            "Documentation",
            "Open RCCP documentation",
            "\ud83d\udcd6",
            new Color(0.3f, 0.5f, 0.6f, 0.15f),
            () => {
                UnityEngine.Object docAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RCCP_AssetPaths.documentationPath);
                if (docAsset != null)
                    AssetDatabase.OpenAsset(docAsset);
                else
                    EditorUtility.RevealInFinder("Assets/Realistic Car Controller Pro/Documentation");
            },
            searchQuery
        );

        // Support.
        CreateSettingCard(
            "Support",
            "Get help and support",
            "\ud83d\udcac",
            new Color(0.5f, 0.4f, 0.3f, 0.15f),
            () => Application.OpenURL("https://www.bonecrackergames.com/contact/"),
            searchQuery
        );

        // Action buttons.
        CreateActionButtons();

    }

    private void CreateSection(string title, VisualElement parent) {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        Label sectionLabel = new Label(title);
        sectionLabel.style.fontSize = 10 * scale;
        sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        sectionLabel.style.marginTop = 8 * scale;
        sectionLabel.style.marginBottom = 4 * scale;
        sectionLabel.style.paddingLeft = 4 * scale;
        sectionLabel.style.color = EditorGUIUtility.isProSkin
            ? new Color(0.7f, 0.7f, 0.7f)
            : new Color(0.3f, 0.3f, 0.3f);
        parent.Add(sectionLabel);

    }

    private void CreateSettingCard(string title, string description, string icon, Color bgColor, System.Action onClick, string searchQuery) {

        // Filter check.
        if (!string.IsNullOrEmpty(searchQuery)) {
            string lowerSearch = searchQuery.ToLower();
            if (!title.ToLower().Contains(lowerSearch) && !description.ToLower().Contains(lowerSearch)) {
                return;
            }
        }

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.paddingTop = 6 * scale;
        card.style.paddingBottom = 6 * scale;
        card.style.paddingLeft = 8 * scale;
        card.style.paddingRight = 8 * scale;
        card.style.marginBottom = 2 * scale;
        card.style.backgroundColor = bgColor;
        card.style.borderTopLeftRadius = 3;
        card.style.borderTopRightRadius = 3;
        card.style.borderBottomLeftRadius = 3;
        card.style.borderBottomRightRadius = 3;
        card.style.alignItems = Align.Center;

        // Hover effect.
        Color normalBg = bgColor;
        Color hoverBg = new Color(bgColor.r + 0.1f, bgColor.g + 0.1f, bgColor.b + 0.1f, bgColor.a + 0.1f);

        card.RegisterCallback<MouseEnterEvent>(evt => {
            card.style.backgroundColor = hoverBg;
        });

        card.RegisterCallback<MouseLeaveEvent>(evt => {
            card.style.backgroundColor = normalBg;
        });

        card.RegisterCallback<ClickEvent>(evt => onClick());

        // Icon.
        Label iconLabel = new Label(icon);
        iconLabel.style.fontSize = 16 * scale;
        iconLabel.style.marginRight = 8 * scale;
        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        iconLabel.style.width = 22 * scale;
        card.Add(iconLabel);

        // Info container.
        VisualElement infoContainer = new VisualElement();
        infoContainer.style.flexGrow = 1;

        Label titleLabel = new Label(title);
        titleLabel.style.fontSize = 9 * scale;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        infoContainer.Add(titleLabel);

        Label descLabel = new Label(description);
        descLabel.style.fontSize = 8 * scale;
        descLabel.style.color = EditorGUIUtility.isProSkin
            ? new Color(0.7f, 0.7f, 0.7f)
            : new Color(0.4f, 0.4f, 0.4f);
        infoContainer.Add(descLabel);

        card.Add(infoContainer);

        // Arrow.
        Label arrowLabel = new Label("\u25b6");
        arrowLabel.style.fontSize = 10 * scale;
        arrowLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
        card.Add(arrowLabel);

        settingsContainer.Add(card);

    }

    private void CreateActionButtons() {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement buttonsContainer = new VisualElement();
        buttonsContainer.style.flexDirection = FlexDirection.Row;
        buttonsContainer.style.justifyContent = Justify.Center;
        buttonsContainer.style.marginTop = 10 * scale;
        buttonsContainer.style.paddingTop = 8 * scale;
        buttonsContainer.style.borderTopWidth = 1;
        buttonsContainer.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);

        Button openSettingsButton = new Button(() => {
            if (RCCP_Settings.Instance != null) {
                Selection.activeObject = RCCP_Settings.Instance;
                EditorGUIUtility.PingObject(RCCP_Settings.Instance);
            }
        });
        openSettingsButton.text = "Open RCCP Settings";
        openSettingsButton.style.flexGrow = 1;
        openSettingsButton.style.height = 22 * scale;
        openSettingsButton.style.fontSize = 9 * scale;
        buttonsContainer.Add(openSettingsButton);

        settingsContainer.Add(buttonsContainer);

    }

    private void OpenAIAssistantWindow() {

        // Use reflection to open AI Assistant window.
        var windowType = System.Type.GetType("RCCP_AIAssistantWindow, Assembly-CSharp-Editor");
        if (windowType != null) {
            var method = windowType.GetMethod("ShowWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }

    }

    #endregion

}

#endif
