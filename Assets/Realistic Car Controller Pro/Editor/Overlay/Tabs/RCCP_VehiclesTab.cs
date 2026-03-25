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
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// Vehicles tab content for RCCP Scene View Overlay.
/// Provides quick access to all RCCP vehicles in the scene.
/// </summary>
public class RCCP_VehiclesTab : IRCCP_OverlayContent {

    #region Variables

    private VisualElement rootElement;
    private VisualElement vehiclesContainer;
    private Label statusLabel;

    #endregion

    #region Interface Implementation

    public VisualElement CreateContent(string searchQuery) {

        rootElement = new VisualElement();
        rootElement.name = "rccp-vehicles-tab";
        rootElement.style.flexGrow = 1;
        rootElement.style.flexShrink = 1;
        rootElement.style.height = Length.Percent(100);

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        // Create status label.
        statusLabel = new Label();
        statusLabel.style.marginBottom = 4 * scale;
        statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        statusLabel.style.height = 14 * scale;
        statusLabel.style.fontSize = 9 * scale;
        rootElement.Add(statusLabel);

        // Create vehicles container.
        vehiclesContainer = new VisualElement();
        vehiclesContainer.name = "vehicles-container";
        vehiclesContainer.style.paddingBottom = 20 * scale;

        // Create scroll view.
        ScrollView scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.Add(vehiclesContainer);
        rootElement.Add(scrollView);

        // Load vehicles.
        RefreshVehicles(searchQuery);

        return rootElement;

    }

    public void OnUpdate() {

        // Periodic refresh handled by RefreshVehicles if needed.

    }

    public void OnDestroy() {

        // Cleanup if needed.

    }

    #endregion

    #region Vehicle Display

    private void RefreshVehicles(string searchQuery) {

        vehiclesContainer.Clear();

        var vehicles = RCCP_SceneDataCache.GetVehicles(searchQuery);
        var stats = RCCP_SceneDataCache.GetStatistics();

        // Update status.
        UpdateStatusLabel(stats);

        if (vehicles.Count == 0) {

            CreateEmptyState();
            return;

        }

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        // Create vehicle cards.
        foreach (var vehicle in vehicles) {

            if (vehicle == null) continue;

            CreateVehicleCard(vehicle, searchQuery);

        }

        // Add action buttons at the bottom.
        CreateActionButtons();

    }

    private void CreateEmptyState() {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement emptyState = new VisualElement();
        emptyState.style.alignItems = Align.Center;
        emptyState.style.justifyContent = Justify.Center;
        emptyState.style.paddingTop = 30 * scale;
        emptyState.style.paddingBottom = 30 * scale;

        Label iconLabel = new Label("\ud83d\ude97");
        iconLabel.style.fontSize = 36 * scale;
        iconLabel.style.marginBottom = 8 * scale;
        emptyState.Add(iconLabel);

        Label titleLabel = new Label("No RCCP Vehicles Found");
        titleLabel.style.fontSize = 12 * scale;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4 * scale;
        emptyState.Add(titleLabel);

        Label descLabel = new Label("Add RCCP_CarController to a vehicle.");
        descLabel.style.fontSize = 10 * scale;
        descLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        descLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        emptyState.Add(descLabel);

        // Show AI Assistant button only if installed.
        if (RCCP_SceneToolsController.IsAIAssistantInstalled) {

            Button createButton = new Button(() => {
                OpenAIAssistantWindow();
            });
            createButton.text = "Open AI Assistant";
            createButton.style.marginTop = 12 * scale;
            createButton.style.paddingTop = 6 * scale;
            createButton.style.paddingBottom = 6 * scale;
            createButton.style.paddingLeft = 12 * scale;
            createButton.style.paddingRight = 12 * scale;
            createButton.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.85f);
            createButton.style.color = Color.white;
            createButton.style.borderTopLeftRadius = 4;
            createButton.style.borderTopRightRadius = 4;
            createButton.style.borderBottomLeftRadius = 4;
            createButton.style.borderBottomRightRadius = 4;
            emptyState.Add(createButton);

        } else {

            Button getAIButton = new Button(() => {
                Application.OpenURL(RCCP_AssetPaths.AIAssistant);
            });
            getAIButton.text = "Get AI Assistant";
            getAIButton.tooltip = "Configure vehicles with AI - Get on Asset Store";
            getAIButton.style.marginTop = 12 * scale;
            getAIButton.style.paddingTop = 6 * scale;
            getAIButton.style.paddingBottom = 6 * scale;
            getAIButton.style.paddingLeft = 12 * scale;
            getAIButton.style.paddingRight = 12 * scale;
            getAIButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.65f, 0.85f);
            getAIButton.style.color = Color.white;
            getAIButton.style.borderTopLeftRadius = 4;
            getAIButton.style.borderTopRightRadius = 4;
            getAIButton.style.borderBottomLeftRadius = 4;
            getAIButton.style.borderBottomRightRadius = 4;
            emptyState.Add(getAIButton);

        }

        vehiclesContainer.Add(emptyState);

    }

    private void CreateVehicleCard(RCCP_CarController vehicle, string searchQuery) {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();
        var status = RCCP_SceneDataCache.GetVehicleComponentStatus(vehicle);

        VisualElement card = new VisualElement();
        card.name = $"card-{vehicle.gameObject.name.ToLower().Replace(" ", "-")}";
        card.style.flexDirection = FlexDirection.Row;
        card.style.paddingTop = 4 * scale;
        card.style.paddingBottom = 4 * scale;
        card.style.paddingLeft = 6 * scale;
        card.style.paddingRight = 6 * scale;
        card.style.marginBottom = 1 * scale;

        // Background color based on status.
        if (status.IsFullyConfigured) {
            card.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f, 0.15f); // Green tint
        } else if (status.HasCriticalIssues) {
            card.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f, 0.15f); // Red tint
        } else {
            card.style.backgroundColor = new Color(0.5f, 0.4f, 0.2f, 0.15f); // Orange tint
        }

        card.style.borderTopLeftRadius = 3;
        card.style.borderTopRightRadius = 3;
        card.style.borderBottomLeftRadius = 3;
        card.style.borderBottomRightRadius = 3;

        // Icon.
        Label iconLabel = new Label("\ud83d\ude97");
        iconLabel.style.fontSize = 14 * scale;
        iconLabel.style.marginRight = 4 * scale;
        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        card.Add(iconLabel);

        // Info container.
        VisualElement infoContainer = new VisualElement();
        infoContainer.style.flexGrow = 1;

        Label titleLabel = new Label(vehicle.gameObject.name);
        titleLabel.style.fontSize = 9 * scale;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        infoContainer.Add(titleLabel);

        Label componentLabel = new Label($"Components: {status.ActiveComponents}/{status.TotalComponents}");
        componentLabel.style.fontSize = 8 * scale;
        componentLabel.style.color = EditorGUIUtility.isProSkin
            ? new Color(0.7f, 0.7f, 0.7f)
            : new Color(0.4f, 0.4f, 0.4f);
        infoContainer.Add(componentLabel);

        card.Add(infoContainer);

        // Status indicator.
        VisualElement statusIndicator = new VisualElement();
        statusIndicator.style.width = 6 * scale;
        statusIndicator.style.height = 6 * scale;
        statusIndicator.style.borderTopLeftRadius = 3 * scale;
        statusIndicator.style.borderTopRightRadius = 3 * scale;
        statusIndicator.style.borderBottomLeftRadius = 3 * scale;
        statusIndicator.style.borderBottomRightRadius = 3 * scale;
        statusIndicator.style.alignSelf = Align.Center;
        statusIndicator.style.marginRight = 3 * scale;

        if (status.IsFullyConfigured) {
            statusIndicator.style.backgroundColor = new Color(0.3f, 0.9f, 0.3f); // Green
            statusIndicator.tooltip = "Fully configured";
        } else if (status.HasCriticalIssues) {
            statusIndicator.style.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // Red
            statusIndicator.tooltip = "Missing required components";
        } else {
            statusIndicator.style.backgroundColor = new Color(1f, 0.6f, 0.2f); // Orange
            statusIndicator.tooltip = "Missing optional components";
        }

        card.Add(statusIndicator);

        // Action buttons container.
        VisualElement actionsContainer = new VisualElement();
        actionsContainer.style.flexDirection = FlexDirection.Row;

        Button selectButton = new Button(() => SelectVehicle(vehicle));
        selectButton.text = "Select";
        selectButton.style.width = 36 * scale;
        selectButton.style.height = 16 * scale;
        selectButton.style.fontSize = 8 * scale;
        selectButton.style.marginRight = 2 * scale;
        actionsContainer.Add(selectButton);

        Button frameButton = new Button(() => FrameVehicle(vehicle));
        frameButton.text = "Frame";
        frameButton.style.width = 36 * scale;
        frameButton.style.height = 16 * scale;
        frameButton.style.fontSize = 8 * scale;
        frameButton.style.marginRight = 2 * scale;
        actionsContainer.Add(frameButton);

        // AI button only if AI Assistant is installed.
        if (RCCP_SceneToolsController.IsAIAssistantInstalled) {

            Button aiButton = new Button(() => OpenAIAssistant(vehicle));
            aiButton.text = "AI";
            aiButton.tooltip = "Open AI Assistant for this vehicle";
            aiButton.style.width = 22 * scale;
            aiButton.style.height = 16 * scale;
            aiButton.style.fontSize = 8 * scale;
            aiButton.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.85f);
            aiButton.style.color = Color.white;
            actionsContainer.Add(aiButton);

        } else {

            Button aiPromoButton = new Button(() => Application.OpenURL(RCCP_AssetPaths.AIAssistant));
            aiPromoButton.text = "AI";
            aiPromoButton.tooltip = "Get AI Assistant on Asset Store";
            aiPromoButton.style.width = 22 * scale;
            aiPromoButton.style.height = 16 * scale;
            aiPromoButton.style.fontSize = 8 * scale;
            aiPromoButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.65f, 0.85f);
            aiPromoButton.style.color = Color.white;
            actionsContainer.Add(aiPromoButton);

        }

        card.Add(actionsContainer);

        vehiclesContainer.Add(card);

    }

    private void CreateActionButtons() {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement buttonsContainer = new VisualElement();
        buttonsContainer.style.flexDirection = FlexDirection.Row;
        buttonsContainer.style.justifyContent = Justify.Center;
        buttonsContainer.style.marginTop = 6 * scale;
        buttonsContainer.style.paddingTop = 6 * scale;
        buttonsContainer.style.borderTopWidth = 1;
        buttonsContainer.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);

        Button frameAllButton = new Button(FrameAllVehicles);
        frameAllButton.text = "Frame All";
        frameAllButton.style.flexGrow = 1;
        frameAllButton.style.marginRight = 2 * scale;
        frameAllButton.style.height = 20 * scale;
        frameAllButton.style.fontSize = 9 * scale;
        buttonsContainer.Add(frameAllButton);

        Button refreshButton = new Button(() => RefreshVehicles(""));
        refreshButton.text = "Refresh";
        refreshButton.style.flexGrow = 1;
        refreshButton.style.marginLeft = 2 * scale;
        refreshButton.style.height = 20 * scale;
        refreshButton.style.fontSize = 9 * scale;
        buttonsContainer.Add(refreshButton);

        vehiclesContainer.Add(buttonsContainer);

    }

    private void UpdateStatusLabel(RCCP_SceneDataCache.SceneStatistics stats) {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        statusLabel.text = $"Vehicles: {stats.totalVehicles} | Configured: {stats.fullyConfiguredVehicles}";

        if (stats.totalVehicles == 0) {
            statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        } else if (stats.fullyConfiguredVehicles == stats.totalVehicles) {
            statusLabel.style.color = new Color(0.3f, 0.8f, 0.3f); // Green
        } else if (stats.vehiclesWithMissingRequired > 0) {
            statusLabel.style.color = new Color(0.9f, 0.3f, 0.3f); // Red
        } else {
            statusLabel.style.color = new Color(1f, 0.8f, 0.2f); // Orange
        }

    }

    #endregion

    #region Actions

    private void SelectVehicle(RCCP_CarController vehicle) {

        if (vehicle != null) {
            Selection.activeGameObject = vehicle.gameObject;
            EditorGUIUtility.PingObject(vehicle.gameObject);
        }

    }

    private void FrameVehicle(RCCP_CarController vehicle) {

        if (vehicle != null) {
            Selection.activeGameObject = vehicle.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

    }

    private void OpenAIAssistant(RCCP_CarController vehicle) {

        if (vehicle != null) {
            Selection.activeGameObject = vehicle.gameObject;
            OpenAIAssistantWindow();
        }

    }

    private void OpenAIAssistantWindow() {

        // Use reflection to open AI Assistant window.
        var windowType = System.Type.GetType("RCCP_AIAssistantWindow, Assembly-CSharp-Editor");
        if (windowType != null) {
            var method = windowType.GetMethod("ShowWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }

    }

    private void FrameAllVehicles() {

        var vehicles = RCCP_SceneDataCache.GetVehicles();

        if (vehicles.Count > 0) {
            Selection.objects = vehicles.Select(v => v.gameObject).Cast<Object>().ToArray();
            SceneView.lastActiveSceneView?.FrameSelected();
        }

    }

    #endregion

}

#endif
