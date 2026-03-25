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

/// <summary>
/// AI Assistant tab content for RCCP Scene View Overlay.
/// Provides quick launch buttons for each AI Assistant panel type.
/// This tab is only shown when RCCP AI Assistant package is installed.
/// </summary>
public class RCCP_AIAssistantTab : IRCCP_OverlayContent {

    #region Variables

    private VisualElement rootElement;
    private VisualElement buttonsContainer;

    #endregion

    #region Interface Implementation

    public VisualElement CreateContent(string searchQuery) {

        rootElement = new VisualElement();
        rootElement.name = "rccp-ai-assistant-tab";
        rootElement.style.flexGrow = 1;
        rootElement.style.flexShrink = 1;
        rootElement.style.height = Length.Percent(100);

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        // Header.
        Label headerLabel = new Label("Quick Launch AI Assistant");
        headerLabel.style.fontSize = 10 * scale;
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        headerLabel.style.marginBottom = 8 * scale;
        headerLabel.style.marginTop = 6 * scale;
        rootElement.Add(headerLabel);

        // Description.
        Label descLabel = new Label("Select a panel to open the AI Assistant:");
        descLabel.style.fontSize = 8 * scale;
        descLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        descLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        descLabel.style.marginBottom = 8 * scale;
        rootElement.Add(descLabel);

        // Buttons container.
        buttonsContainer = new VisualElement();
        buttonsContainer.name = "ai-buttons-container";
        buttonsContainer.style.paddingLeft = 6 * scale;
        buttonsContainer.style.paddingRight = 6 * scale;

        // Create scroll view.
        ScrollView scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.Add(buttonsContainer);
        rootElement.Add(scrollView);

        // Create panel buttons.
        CreatePanelButtons(searchQuery);

        return rootElement;

    }

    public void OnUpdate() {

        // No periodic update needed.

    }

    public void OnDestroy() {

        // Cleanup if needed.

    }

    #endregion

    #region Panel Buttons

    private void CreatePanelButtons(string searchQuery) {

        buttonsContainer.Clear();

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        // Vehicle Creation.
        CreatePanelButton(
            "Vehicle Creation",
            "Create a new RCCP vehicle from a 3D model",
            "\ud83d\ude97",
            new Color(0.2f, 0.6f, 0.3f, 0.3f),
            () => OpenAIAssistantPanel("VehicleCreation"),
            searchQuery
        );

        // Vehicle Customization.
        CreatePanelButton(
            "Vehicle Customization",
            "Modify an existing RCCP vehicle",
            "\ud83d\udd27",
            new Color(0.3f, 0.5f, 0.7f, 0.3f),
            () => OpenAIAssistantPanel("VehicleCustomization"),
            searchQuery
        );

        // Behaviors.
        CreatePanelButton(
            "Behaviors",
            "Configure driving presets (Arcade, Drift, Simulation)",
            "\ud83c\udfae",
            new Color(0.6f, 0.4f, 0.7f, 0.3f),
            () => OpenAIAssistantPanel("Behaviors"),
            searchQuery
        );

        // Wheels.
        CreatePanelButton(
            "Wheels",
            "Suspension, friction, camber and caster settings",
            "\u2699\ufe0f",
            new Color(0.5f, 0.5f, 0.5f, 0.3f),
            () => OpenAIAssistantPanel("Wheels"),
            searchQuery
        );

        // Audio.
        CreatePanelButton(
            "Audio",
            "Engine sound layers and audio configuration",
            "\ud83d\udd0a",
            new Color(0.7f, 0.5f, 0.3f, 0.3f),
            () => OpenAIAssistantPanel("Audio"),
            searchQuery
        );

        // Lights.
        CreatePanelButton(
            "Lights",
            "Headlights, brake lights, and indicators",
            "\ud83d\udca1",
            new Color(0.8f, 0.7f, 0.2f, 0.3f),
            () => OpenAIAssistantPanel("Lights"),
            searchQuery
        );

        // Damage.
        CreatePanelButton(
            "Damage",
            "Mesh deformation and damage settings",
            "\ud83d\udca5",
            new Color(0.7f, 0.3f, 0.3f, 0.3f),
            () => OpenAIAssistantPanel("Damage"),
            searchQuery
        );

        // Diagnostics.
        CreatePanelButton(
            "Diagnostics",
            "Local vehicle checks (no AI required)",
            "\ud83d\udcca",
            new Color(0.4f, 0.6f, 0.5f, 0.3f),
            () => OpenAIAssistantPanel("Diagnostics"),
            searchQuery
        );

        // Open Full Window button.
        CreateOpenWindowButton();

    }

    private void CreatePanelButton(string title, string description, string icon, Color bgColor, System.Action onClick, string searchQuery) {

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
        card.style.paddingTop = 8 * scale;
        card.style.paddingBottom = 8 * scale;
        card.style.paddingLeft = 8 * scale;
        card.style.paddingRight = 8 * scale;
        card.style.marginBottom = 4 * scale;
        card.style.backgroundColor = bgColor;
        card.style.borderTopLeftRadius = 4;
        card.style.borderTopRightRadius = 4;
        card.style.borderBottomLeftRadius = 4;
        card.style.borderBottomRightRadius = 4;

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
        iconLabel.style.fontSize = 20 * scale;
        iconLabel.style.marginRight = 8 * scale;
        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        iconLabel.style.width = 28 * scale;
        card.Add(iconLabel);

        // Info container.
        VisualElement infoContainer = new VisualElement();
        infoContainer.style.flexGrow = 1;
        infoContainer.style.justifyContent = Justify.Center;

        Label titleLabel = new Label(title);
        titleLabel.style.fontSize = 10 * scale;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 1 * scale;
        infoContainer.Add(titleLabel);

        Label descLabel = new Label(description);
        descLabel.style.fontSize = 8 * scale;
        descLabel.style.color = EditorGUIUtility.isProSkin
            ? new Color(0.7f, 0.7f, 0.7f)
            : new Color(0.4f, 0.4f, 0.4f);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        infoContainer.Add(descLabel);

        card.Add(infoContainer);

        // Arrow indicator.
        Label arrowLabel = new Label("\u25b6");
        arrowLabel.style.fontSize = 10 * scale;
        arrowLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
        arrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        card.Add(arrowLabel);

        buttonsContainer.Add(card);

    }

    private void CreateOpenWindowButton() {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement separator = new VisualElement();
        separator.style.height = 1;
        separator.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        separator.style.marginTop = 10 * scale;
        separator.style.marginBottom = 10 * scale;
        buttonsContainer.Add(separator);

        Button openWindowButton = new Button(() => {
            OpenAIAssistantWindow();
        });
        openWindowButton.text = "Open Full AI Assistant Window";
        openWindowButton.style.height = 28 * scale;
        openWindowButton.style.fontSize = 10 * scale;
        openWindowButton.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.85f);
        openWindowButton.style.color = Color.white;
        openWindowButton.style.borderTopLeftRadius = 4;
        openWindowButton.style.borderTopRightRadius = 4;
        openWindowButton.style.borderBottomLeftRadius = 4;
        openWindowButton.style.borderBottomRightRadius = 4;

        openWindowButton.RegisterCallback<MouseEnterEvent>(evt => {
            openWindowButton.style.backgroundColor = new Color(0.96f, 0.59f, 0.34f, 1f);
        });

        openWindowButton.RegisterCallback<MouseLeaveEvent>(evt => {
            openWindowButton.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.85f);
        });

        buttonsContainer.Add(openWindowButton);

    }

    private void OpenAIAssistantPanel(string panelType) {

        // Open the AI Assistant window.
        OpenAIAssistantWindow();

        // Note: The user can navigate to the desired panel from the window.

    }

    private void OpenAIAssistantWindow() {

        // Use reflection to open AI Assistant window.
        var windowType = System.Type.GetType("BoneCrackerGames.RCCP.AIAssistant.RCCP_AIAssistantWindow, Assembly-CSharp-Editor");
        if (windowType != null) {
            var method = windowType.GetMethod("ShowWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }

    }

    #endregion

}

#endif
