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
using System.Linq;

/// <summary>
/// Diagnostics tab content for RCCP Scene View Overlay.
/// Provides scene validation summary and missing component warnings.
/// </summary>
public class RCCP_DiagnosticsTab : IRCCP_OverlayContent {

    #region Variables

    private VisualElement rootElement;
    private VisualElement diagnosticsContainer;
    private Label statusLabel;

    #endregion

    #region Interface Implementation

    public VisualElement CreateContent(string searchQuery) {

        rootElement = new VisualElement();
        rootElement.name = "rccp-diagnostics-tab";
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

        // Create diagnostics container.
        diagnosticsContainer = new VisualElement();
        diagnosticsContainer.name = "diagnostics-container";
        diagnosticsContainer.style.paddingBottom = 20 * scale;

        // Create scroll view.
        ScrollView scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.Add(diagnosticsContainer);
        rootElement.Add(scrollView);

        // Run diagnostics.
        RefreshDiagnostics(searchQuery);

        return rootElement;

    }

    public void OnUpdate() {

        // Periodic refresh can be added here if needed.

    }

    public void OnDestroy() {

        // Cleanup if needed.

    }

    #endregion

    #region Diagnostics Display

    private void RefreshDiagnostics(string searchQuery) {

        diagnosticsContainer.Clear();

        var stats = RCCP_SceneDataCache.GetStatistics();
        var vehicles = RCCP_SceneDataCache.GetVehicles();

        // Update status.
        UpdateStatusLabel(stats);

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        // Scene Overview Section.
        CreateSection("Scene Overview", diagnosticsContainer);
        CreateDiagnosticCard(
            "RCCP Settings",
            stats.hasRCCPSettings ? "Found" : "Missing",
            stats.hasRCCPSettings ? DiagnosticLevel.Success : DiagnosticLevel.Error,
            stats.hasRCCPSettings ? null : (System.Action)(() => Selection.activeObject = RCCP_Settings.Instance),
            stats.hasRCCPSettings ? "\u2705" : "\u274c"
        );

        CreateDiagnosticCard(
            "RCCP Scene Manager",
            stats.hasRCCPSceneManager ? "Found" : "Missing",
            stats.hasRCCPSceneManager ? DiagnosticLevel.Success : DiagnosticLevel.Warning,
            null,
            stats.hasRCCPSceneManager ? "\u2705" : "\u26a0\ufe0f"
        );

        CreateDiagnosticCard(
            "Total Vehicles",
            stats.totalVehicles.ToString(),
            stats.totalVehicles > 0 ? DiagnosticLevel.Success : DiagnosticLevel.Info,
            null,
            "\ud83d\ude97"
        );

        // Vehicle Status Section.
        if (vehicles.Count > 0) {

            CreateSection("Vehicle Status", diagnosticsContainer);

            CreateDiagnosticCard(
                "Fully Configured",
                $"{stats.fullyConfiguredVehicles} / {stats.totalVehicles}",
                stats.fullyConfiguredVehicles == stats.totalVehicles ? DiagnosticLevel.Success : DiagnosticLevel.Warning,
                null,
                "\u2705"
            );

            if (stats.vehiclesWithMissingRequired > 0) {
                CreateDiagnosticCard(
                    "Missing Required Components",
                    $"{stats.vehiclesWithMissingRequired} vehicle(s)",
                    DiagnosticLevel.Error,
                    null,
                    "\u274c"
                );
            }

            if (stats.partiallyConfiguredVehicles > 0) {
                CreateDiagnosticCard(
                    "Missing Optional Components",
                    $"{stats.partiallyConfiguredVehicles} vehicle(s)",
                    DiagnosticLevel.Warning,
                    null,
                    "\u26a0\ufe0f"
                );
            }

            // Per-vehicle diagnostics.
            CreateSection("Vehicle Details", diagnosticsContainer);

            foreach (var vehicle in vehicles) {

                if (vehicle == null) continue;

                // Filter check.
                if (!string.IsNullOrEmpty(searchQuery)) {
                    string lowerSearch = searchQuery.ToLower();
                    if (!vehicle.gameObject.name.ToLower().Contains(lowerSearch)) {
                        continue;
                    }
                }

                var status = RCCP_SceneDataCache.GetVehicleComponentStatus(vehicle);
                CreateVehicleDiagnosticCard(vehicle, status);

            }

        }

        // Action buttons.
        CreateActionButtons();

    }

    private void CreateSection(string title, VisualElement parent) {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        Label sectionLabel = new Label(title);
        sectionLabel.style.fontSize = 9 * scale;
        sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        sectionLabel.style.marginTop = 5 * scale;
        sectionLabel.style.marginBottom = 2 * scale;
        sectionLabel.style.paddingLeft = 4 * scale;
        sectionLabel.style.color = EditorGUIUtility.isProSkin
            ? new Color(0.7f, 0.7f, 0.7f)
            : new Color(0.3f, 0.3f, 0.3f);
        parent.Add(sectionLabel);

    }

    private void CreateDiagnosticCard(string title, string value, DiagnosticLevel level, System.Action onClick, string icon) {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.paddingTop = 3 * scale;
        card.style.paddingBottom = 3 * scale;
        card.style.paddingLeft = 5 * scale;
        card.style.paddingRight = 5 * scale;
        card.style.marginBottom = 1 * scale;
        card.style.alignItems = Align.Center;

        // Background color based on level.
        switch (level) {
            case DiagnosticLevel.Success:
                card.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f, 0.1f);
                break;
            case DiagnosticLevel.Warning:
                card.style.backgroundColor = new Color(0.5f, 0.4f, 0.2f, 0.1f);
                break;
            case DiagnosticLevel.Error:
                card.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f, 0.1f);
                break;
            default:
                card.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.1f);
                break;
        }

        // Icon.
        Label iconLabel = new Label(icon);
        iconLabel.style.fontSize = 10 * scale;
        iconLabel.style.marginRight = 5 * scale;
        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        iconLabel.style.width = 14 * scale;
        card.Add(iconLabel);

        // Title.
        Label titleLabel = new Label(title);
        titleLabel.style.fontSize = 8 * scale;
        titleLabel.style.flexGrow = 1;
        card.Add(titleLabel);

        // Value.
        Label valueLabel = new Label(value);
        valueLabel.style.fontSize = 8 * scale;
        valueLabel.style.color = GetLevelColor(level);
        valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        card.Add(valueLabel);

        // Make clickable if action provided.
        if (onClick != null) {
            card.RegisterCallback<ClickEvent>(evt => onClick());
        }

        diagnosticsContainer.Add(card);

    }

    private void CreateVehicleDiagnosticCard(RCCP_CarController vehicle, RCCP_SceneDataCache.VehicleComponentStatus status) {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement card = new VisualElement();
        card.style.paddingTop = 3 * scale;
        card.style.paddingBottom = 3 * scale;
        card.style.paddingLeft = 5 * scale;
        card.style.paddingRight = 5 * scale;
        card.style.marginBottom = 1 * scale;

        // Background based on status.
        if (status.IsFullyConfigured) {
            card.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f, 0.1f);
        } else if (status.HasCriticalIssues) {
            card.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f, 0.1f);
        } else {
            card.style.backgroundColor = new Color(0.5f, 0.4f, 0.2f, 0.1f);
        }

        card.style.borderTopLeftRadius = 3;
        card.style.borderTopRightRadius = 3;
        card.style.borderBottomLeftRadius = 3;
        card.style.borderBottomRightRadius = 3;

        // Header row.
        VisualElement headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 2 * scale;

        Label vehicleLabel = new Label(vehicle.gameObject.name);
        vehicleLabel.style.fontSize = 8 * scale;
        vehicleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        vehicleLabel.style.flexGrow = 1;
        headerRow.Add(vehicleLabel);

        Button selectButton = new Button(() => {
            Selection.activeGameObject = vehicle.gameObject;
            EditorGUIUtility.PingObject(vehicle.gameObject);
        });
        selectButton.text = "Select";
        selectButton.style.height = 14 * scale;
        selectButton.style.fontSize = 7 * scale;
        headerRow.Add(selectButton);

        card.Add(headerRow);

        // Component status.
        VisualElement componentsRow = new VisualElement();
        componentsRow.style.flexDirection = FlexDirection.Row;
        componentsRow.style.flexWrap = Wrap.Wrap;

        // Required components.
        AddComponentIndicator(componentsRow, "Engine", status.hasEngine, true, scale);
        AddComponentIndicator(componentsRow, "Gearbox", status.hasGearbox, true, scale);
        AddComponentIndicator(componentsRow, "Clutch", status.hasClutch, true, scale);
        AddComponentIndicator(componentsRow, "Diff", status.hasDifferential, true, scale);
        AddComponentIndicator(componentsRow, "Axles", status.hasAxles, true, scale);
        AddComponentIndicator(componentsRow, "Input", status.hasInput, true, scale);

        // Optional components.
        AddComponentIndicator(componentsRow, "Audio", status.hasAudio, false, scale);
        AddComponentIndicator(componentsRow, "Lights", status.hasLights, false, scale);
        AddComponentIndicator(componentsRow, "Stability", status.hasStability, false, scale);
        AddComponentIndicator(componentsRow, "Damage", status.hasDamage, false, scale);

        card.Add(componentsRow);

        diagnosticsContainer.Add(card);

    }

    private void AddComponentIndicator(VisualElement parent, string name, bool hasComponent, bool isRequired, float scale) {

        VisualElement indicator = new VisualElement();
        indicator.style.flexDirection = FlexDirection.Row;
        indicator.style.alignItems = Align.Center;
        indicator.style.marginRight = 5 * scale;
        indicator.style.marginBottom = 1 * scale;

        // Status dot.
        VisualElement dot = new VisualElement();
        dot.style.width = 5 * scale;
        dot.style.height = 5 * scale;
        dot.style.borderTopLeftRadius = 2.5f * scale;
        dot.style.borderTopRightRadius = 2.5f * scale;
        dot.style.borderBottomLeftRadius = 2.5f * scale;
        dot.style.borderBottomRightRadius = 2.5f * scale;
        dot.style.marginRight = 2 * scale;

        if (hasComponent) {
            dot.style.backgroundColor = new Color(0.3f, 0.9f, 0.3f); // Green
        } else if (isRequired) {
            dot.style.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // Red
        } else {
            dot.style.backgroundColor = new Color(0.6f, 0.6f, 0.6f); // Gray
        }

        indicator.Add(dot);

        // Name.
        Label nameLabel = new Label(name);
        nameLabel.style.fontSize = 7 * scale;
        nameLabel.style.color = hasComponent
            ? new Color(0.8f, 0.8f, 0.8f)
            : new Color(0.5f, 0.5f, 0.5f);
        indicator.Add(nameLabel);

        parent.Add(indicator);

    }

    private void CreateActionButtons() {

        float scale = RCCP_SceneViewOverlay.GetStaticScaleFactor();

        VisualElement buttonsContainer = new VisualElement();
        buttonsContainer.style.flexDirection = FlexDirection.Row;
        buttonsContainer.style.justifyContent = Justify.Center;
        buttonsContainer.style.marginTop = 5 * scale;
        buttonsContainer.style.paddingTop = 5 * scale;
        buttonsContainer.style.borderTopWidth = 1;
        buttonsContainer.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);

        // Full Diagnostics button only if AI Assistant is installed.
        if (RCCP_SceneToolsController.IsAIAssistantInstalled) {

            Button fullDiagButton = new Button(() => {
                OpenAIAssistantWindow();
            });
            fullDiagButton.text = "Full Diagnostics";
            fullDiagButton.style.flexGrow = 1;
            fullDiagButton.style.marginRight = 2 * scale;
            fullDiagButton.style.height = 18 * scale;
            fullDiagButton.style.fontSize = 8 * scale;
            buttonsContainer.Add(fullDiagButton);

        } else {

            Button getAIDiagButton = new Button(() => {
                Application.OpenURL(RCCP_AssetPaths.AIAssistant);
            });
            getAIDiagButton.text = "Get AI Diagnostics";
            getAIDiagButton.tooltip = "Get full AI-powered diagnostics on Asset Store";
            getAIDiagButton.style.flexGrow = 1;
            getAIDiagButton.style.marginRight = 2 * scale;
            getAIDiagButton.style.height = 18 * scale;
            getAIDiagButton.style.fontSize = 8 * scale;
            getAIDiagButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.65f, 0.85f);
            getAIDiagButton.style.color = Color.white;
            buttonsContainer.Add(getAIDiagButton);

        }

        Button refreshButton = new Button(() => RefreshDiagnostics(""));
        refreshButton.text = "Refresh";
        refreshButton.style.flexGrow = 1;
        refreshButton.style.marginLeft = RCCP_SceneToolsController.IsAIAssistantInstalled ? 2 * scale : 0;
        refreshButton.style.height = 18 * scale;
        refreshButton.style.fontSize = 8 * scale;
        buttonsContainer.Add(refreshButton);

        diagnosticsContainer.Add(buttonsContainer);

    }

    private void UpdateStatusLabel(RCCP_SceneDataCache.SceneStatistics stats) {

        int issues = stats.vehiclesWithMissingRequired + (stats.hasRCCPSettings ? 0 : 1);

        if (issues == 0) {
            statusLabel.text = "\u2705 All checks passed";
            statusLabel.style.color = new Color(0.3f, 0.8f, 0.3f);
        } else {
            statusLabel.text = $"\u26a0\ufe0f {issues} issue(s) found";
            statusLabel.style.color = new Color(1f, 0.6f, 0.2f);
        }

    }

    private Color GetLevelColor(DiagnosticLevel level) {

        switch (level) {
            case DiagnosticLevel.Success:
                return new Color(0.3f, 0.8f, 0.3f);
            case DiagnosticLevel.Warning:
                return new Color(1f, 0.8f, 0.2f);
            case DiagnosticLevel.Error:
                return new Color(0.9f, 0.3f, 0.3f);
            default:
                return new Color(0.7f, 0.7f, 0.7f);
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

    #endregion

    #region Enums

    private enum DiagnosticLevel {
        Info,
        Success,
        Warning,
        Error
    }

    #endregion

}

#endif
