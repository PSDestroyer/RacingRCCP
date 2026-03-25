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

    // Cached Styles
    private GUIStyle welcomeTitleStyle;
    private GUIStyle welcomeSubtitleStyle;
    private GUIStyle welcomeLimitsBoxStyle;
    private GUIStyle welcomeLimitsTitleStyle;
    private GUIStyle welcomeLimitsBodyStyle;
    private GUIStyle welcomeTipBoxStyle;
    private GUIStyle welcomeTipTitleStyle;
    private GUIStyle welcomeTipBodyStyle;
    private GUIStyle welcomeFeatureIconStyle;
    private GUIStyle welcomeFeatureTitleStyle;
    private GUIStyle welcomeFeatureDescStyle;
    private GUIStyle welcomeButtonStyle;
    private GUIStyle welcomeStatusStyle;
    private GUIStyle welcomeVersionStyle;
    private bool welcomeStylesInitialized = false;

    // Scroll state
    private Vector2 welcomeScrollPosition;

    // Banner texture
    private Texture2D welcomeBannerTexture;

    private void InitializeWelcomeStyles() {
        if (welcomeStylesInitialized && stylesInitialized) return;

        welcomeTitleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.Size4XL,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = AccentColor }
        };

        welcomeSubtitleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.TextSecondary, 0.1f) }
        };

        welcomeLimitsBoxStyle = new GUIStyle(RCCP_AIDesignSystem.PanelRecessed) {
            padding = new RectOffset(15, 15, 10, 10)
        };

        welcomeLimitsTitleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Info }
        };

        welcomeLimitsBodyStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary },
            wordWrap = true
        };

        welcomeTipBoxStyle = new GUIStyle(RCCP_AIDesignSystem.PanelElevated) {
            padding = new RectOffset(15, 15, 10, 10)
        };

        welcomeTipTitleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            normal = { textColor = AccentColor }
        };

        welcomeTipBodyStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary },
            wordWrap = true
        };

        welcomeFeatureIconStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.Size3XL,
            alignment = TextAnchor.MiddleCenter
        };

        welcomeFeatureTitleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMDL
        };

        welcomeFeatureDescStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            wordWrap = true,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        };

        welcomeButtonStyle = new GUIStyle(GUI.skin.button) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(25, 25, 10, 10)
        };
        welcomeButtonStyle.normal.textColor = Color.white;
        welcomeButtonStyle.hover.textColor = Color.white;
        welcomeButtonStyle.active.textColor = Color.white;
        welcomeButtonStyle.focused.textColor = Color.white;

        welcomeStatusStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase
        };

        welcomeVersionStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        };

        // Load banner texture
        if (welcomeBannerTexture == null) {
            welcomeBannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/RCCP AI Assistant/Editor/Resources/Generated/welcome_banner.png");
        }

        welcomeStylesInitialized = true;
    }

    #region UI Drawing - Welcome Panel

    private void DrawWelcomePanel() {
        InitializeWelcomeStyles();

        // Apply fade animation
        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, welcomePanelAlpha);

        // Background - fill entire window
        Rect bgRect = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(bgRect, RCCP_AIDesignSystem.Colors.BgBase);

        // Adaptive sizing - use full width with consistent padding
        float horizontalPadding = 15;
        float verticalPadding = 10;
        float availableWidth = position.width - (horizontalPadding * 2);
        float availableHeight = position.height - (verticalPadding * 2);

        GUILayout.BeginArea(new Rect(horizontalPadding, verticalPadding, availableWidth, availableHeight));

        // Main Panel Box
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated, GUILayout.ExpandHeight(true));

        // Scroll view wraps everything for small windows
        welcomeScrollPosition = EditorGUILayout.BeginScrollView(welcomeScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

        RCCP_AIDesignSystem.Space(S4);

        // Banner Image - compact size
        if (welcomeBannerTexture != null) {
            float maxBannerWidth = Mathf.Min(320, availableWidth - 40);
            float bannerWidth = Mathf.Min(welcomeBannerTexture.width, maxBannerWidth);
            float bannerHeight = bannerWidth * welcomeBannerTexture.height / welcomeBannerTexture.width;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(welcomeBannerTexture, GUILayout.Width(bannerWidth), GUILayout.Height(bannerHeight));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S4);
        }

        // Title & Subtitle
        GUILayout.Label("RCCP AI Setup Assistant", welcomeTitleStyle);
        GUILayout.Label("Configure vehicles with natural language", welcomeSubtitleStyle);
        RCCP_AIDesignSystem.Space(S5);

        // Compact feature grid - 2 columns, shorter descriptions
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Left column
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(140), GUILayout.MaxWidth(260));
        DrawFeatureItemCompact("🚗", "Create Vehicles", "Build RCCP vehicles from 3D models", RCCP_AIPromptAsset.PanelType.VehicleCreation);
        DrawFeatureItemCompact("⚙️", "Customize Performance", "Engine, gearbox, differential", RCCP_AIPromptAsset.PanelType.VehicleCustomization);
        DrawFeatureItemCompact("🏁", "Behavior Presets", "Switch or create RCCP_Settings presets", RCCP_AIPromptAsset.PanelType.Behaviors);
        DrawFeatureItemCompact("🛞", "Wheels & Suspension", "Friction, camber, toe settings", RCCP_AIPromptAsset.PanelType.Wheels);
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S5);

        // Right column
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(140), GUILayout.MaxWidth(260));
        DrawFeatureItemCompact("🔊", "Audio Setup", "Engine sounds, exhaust pops", RCCP_AIPromptAsset.PanelType.Audio);
        DrawFeatureItemCompact("💡", "Lights & Effects", "Headlights, indicators, brakes", RCCP_AIPromptAsset.PanelType.Lights);
        DrawFeatureItemCompact("💥", "Damage System", "Deformation, detachable parts", RCCP_AIPromptAsset.PanelType.Damage);
        DrawFeatureItemCompact("🔍", "Diagnostics", "Analyze and fix vehicles", RCCP_AIPromptAsset.PanelType.Diagnostics);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S5);

        // Combined compact info box
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginVertical(GUILayout.MaxWidth(520));

        EditorGUILayout.BeginVertical(welcomeLimitsBoxStyle);
        GUILayout.Label("💡 Quick Start: Select a 3D model → Pick a panel → Describe what you want → Apply", welcomeLimitsBodyStyle);
        RCCP_AIDesignSystem.Space(S2);
        GUILayout.Label("ℹ️ Free: 400 setup requests, then 30/day. Add your API key in Settings for unlimited.", welcomeLimitsBodyStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S5);

        EditorGUILayout.EndScrollView();

        // Footer - fixed at bottom
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S4);

        // Buttons row
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Don't show again checkbox
        if (settings != null) {
            bool dontShowAgain = !settings.showWelcomeOnStartup;
            bool newDontShowAgain = GUILayout.Toggle(dontShowAgain, "  Don't show on startup", GUILayout.Width(170));
            if (newDontShowAgain != dontShowAgain) {
                settings.showWelcomeOnStartup = !newDontShowAgain;
                EditorUtility.SetDirty(settings);
            }
        }

        RCCP_AIDesignSystem.Space(S5);

        // Get Started button
        GUI.backgroundColor = AccentColor;
        if (GUILayout.Button("Get Started →", welcomeButtonStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge), GUILayout.Width(170))) {
            CloseWelcomePanel();
            SwitchToPanel(RCCP_AIPromptAsset.PanelType.VehicleCreation);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S4);

        // Status footer
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        bool hasApiKey = HasValidAuth;
        bool isServerProxy = RCCP_AISettings.Instance?.useServerProxy ?? false;
        Color statusColor = hasApiKey ? RCCP_AIDesignSystem.Colors.Success : RCCP_AIDesignSystem.Colors.Error;
        string statusIcon = hasApiKey ? "●" : "○";
        string statusText = hasApiKey ? (isServerProxy ? "Server Proxy Active" : "API Key Configured") : "API Key Required";

        welcomeStatusStyle.normal.textColor = statusColor;
        GUILayout.Label($"{statusIcon} {statusText}", welcomeStatusStyle);

        RCCP_AIDesignSystem.Space(S5);
        GUILayout.Label("•", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { normal = { textColor = RCCP_AIDesignSystem.Colors.TextDisabled } });
        RCCP_AIDesignSystem.Space(S5);
        GUILayout.Label("v1.0.0", welcomeVersionStyle);
        RCCP_AIDesignSystem.Space(S5);
        GUILayout.Label("•", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { normal = { textColor = RCCP_AIDesignSystem.Colors.TextDisabled } });
        RCCP_AIDesignSystem.Space(S5);
        GUILayout.Label("© 2026 BoneCracker Games", welcomeVersionStyle);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S4);

        EditorGUILayout.EndVertical();

        GUILayout.EndArea();

        GUI.color = oldColor;
    }

    private void DrawFeatureItemCompact(string icon, string title, string description, RCCP_AIPromptAsset.PanelType? panelType = null) {
        Rect itemRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero));

        bool isHovered = itemRect.Contains(Event.current.mousePosition);
        if (isHovered && panelType.HasValue) {
            EditorGUI.DrawRect(new Rect(itemRect.x - 3, itemRect.y, itemRect.width + 6, itemRect.height),
                RCCP_AIDesignSystem.Colors.WithAlpha(Color.white, 0.05f));
            EditorGUIUtility.AddCursorRect(itemRect, MouseCursor.Link);
        }

        GUILayout.Label(icon, welcomeFeatureIconStyle, GUILayout.Width(36), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction));

        EditorGUILayout.BeginVertical();
        welcomeFeatureTitleStyle.normal.textColor = isHovered && panelType.HasValue ? AccentColor : Color.white;
        GUILayout.Label(title, welcomeFeatureTitleStyle);
        GUILayout.Label(description, welcomeFeatureDescStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        if (panelType.HasValue && Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition)) {
            CloseWelcomePanel();
            SwitchToPanel(panelType.Value);
            Event.current.Use();
        }
    }

    private void DrawFeatureItem(string icon, string title, string description, RCCP_AIPromptAsset.PanelType? panelType = null) {
        Rect itemRect = EditorGUILayout.BeginHorizontal();

        // Hover effect
        bool isHovered = itemRect.Contains(Event.current.mousePosition);
        if (isHovered && panelType.HasValue) {
            EditorGUI.DrawRect(new Rect(itemRect.x - 5, itemRect.y - 2, itemRect.width + 10, itemRect.height + 4),
                RCCP_AIDesignSystem.Colors.WithAlpha(Color.white, 0.05f));
            EditorGUIUtility.AddCursorRect(itemRect, MouseCursor.Link);
        }

        GUILayout.Label(icon, welcomeFeatureIconStyle, GUILayout.Width(35), GUILayout.Height(RCCP_AIDesignSystem.Heights.SidebarItem));

        RCCP_AIDesignSystem.Space(S4);

        EditorGUILayout.BeginVertical();
        
        welcomeFeatureTitleStyle.normal.textColor = isHovered && panelType.HasValue ? AccentColor : Color.white;
        GUILayout.Label(title, welcomeFeatureTitleStyle);

        GUILayout.Label(description, welcomeFeatureDescStyle);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        if (panelType.HasValue && Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition)) {
            CloseWelcomePanel();
            SwitchToPanel(panelType.Value);
            Event.current.Use();
        }
    }

    private void CloseWelcomePanel() {
        showWelcome = false;

        if (!hasSeenWelcome) {
            hasSeenWelcome = true;
            RCCP_AIEditorPrefs.HasSeenWelcome = true;
        }
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
