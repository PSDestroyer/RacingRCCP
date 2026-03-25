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
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
#endif

using UnityEditorInternal;

/// <summary>
/// Modern Scene View overlay for RCCP using Unity's Overlay API.
/// Provides quick access to vehicles, diagnostics, AI assistant (if installed), and settings.
/// </summary>
#if UNITY_2021_2_OR_NEWER
[Overlay(typeof(SceneView), "RCCP Scene Tools", false)]
public class RCCP_SceneViewOverlay : Overlay, ITransientOverlay {
#else
public class RCCP_SceneViewOverlay {
#endif

    #region Variables

    private RCCP_SceneToolsController controller;

    // UI Elements.
    private VisualElement rootContainer;
    private VisualElement contentContainer;
    private VisualElement tabContainer;
    private Button dockToggleButton;

    #endregion

    #region Properties

#if UNITY_2021_2_OR_NEWER
    /// <summary>
    /// Determines if overlay should be visible.
    /// </summary>
    public bool visible => EditorPrefs.GetBool("RCCP_Overlay_Visible", false);
#endif

    /// <summary>
    /// Static accessor for scale factor (for external classes/tabs).
    /// </summary>
    public static float GetStaticScaleFactor() {
        return RCCP_SceneToolsUI.GetScaleFactor();
    }

    #endregion

    #region Initialization

#if UNITY_2021_2_OR_NEWER
    public override void OnCreated() {

        base.OnCreated();

        // Initialize Shared Controller.
        controller = new RCCP_SceneToolsController("RCCP_Overlay");

        // Track docking state when it changes.
        floatingPositionChanged += OnFloatingPositionChanged;

    }

    /// <summary>
    /// Called when the overlay floating position changes (docking/undocking).
    /// </summary>
    private void OnFloatingPositionChanged(Vector3 position) {

        bool wasDockedBefore = RCCP_OverlayDockingHelper.GetLastDockedState();

        if (wasDockedBefore) {

            // Just undocked - we're now floating.
            RCCP_OverlayDockingHelper.SaveDockedState(false);
            RCCP_OverlayDockingHelper.ShowDockingNotification(false);

            // Force refresh when undocking.
            SceneView.RepaintAll();

        } else {

            // Still floating - show occasional tips.
            if (UnityEngine.Random.Range(0, 50) == 0 && !RCCP_OverlayDockingHelper.IsFirstTimeUser()) {
                Debug.Log("[RCCP] Tip: Drag this panel to the Scene view toolbar to dock it!");
            }

        }

        // Update dock button visual state.
        UpdateDockButtonState();

    }
#endif

    /// <summary>
    /// Closes the overlay panel by hiding it.
    /// </summary>
    private void ClosePanel() {
#if UNITY_2021_2_OR_NEWER
        // Persist visibility.
        EditorPrefs.SetBool("RCCP_Overlay_Visible", false);

        // Instant hide.
        try {
            if (rootContainer != null) {
                rootContainer.SetEnabled(false);
                rootContainer.style.display = DisplayStyle.None;
                rootContainer.MarkDirtyRepaint();
            }
        } catch { }

        // Stop Update loop.
        try {
            EditorApplication.update -= OnUpdate;
        } catch { }

        // Flip Overlay flag.
        displayed = false;

        // Force repaint.
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) {
            sv.rootVisualElement.MarkDirtyRepaint();
            sv.Repaint();
        }

        InternalEditorUtility.RepaintAllViews();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
#endif
    }

    /// <summary>
    /// Toggles the docking state of the overlay panel.
    /// </summary>
    private void ToggleDocking() {

#if UNITY_2021_2_OR_NEWER
        // Check current docking state.
        bool isDocked = this.collapsed && this.isInToolbar;

        if (isDocked) {

            // Undock the panel.
            this.Undock();

            if (SceneView.lastActiveSceneView != null) {
                SceneView.lastActiveSceneView.ShowNotification(
                    new GUIContent("Panel undocked. Drag to toolbar to re-dock."),
                    2f
                );
            }

            RCCP_OverlayDockingHelper.SaveDockedState(false);

        } else {

            // Cannot programmatically dock (Unity API limitation).
            RCCP_OverlayDockingHelper.ShowDockingHelp();

        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();

        UpdateDockButtonState();
#endif

    }

    #endregion

    #region UI Creation

#if UNITY_2021_2_OR_NEWER
    public override VisualElement CreatePanelContent() {

        float scale = RCCP_SceneToolsUI.GetScaleFactor();

        // Create root container.
        rootContainer = new VisualElement();
        rootContainer.name = "rccp-overlay-root";

        // Responsive width based on docking state.
        bool isDocked = this.collapsed && this.isInToolbar;
        float targetWidth = isDocked ? 380 : 420;

        rootContainer.style.width = targetWidth;
        rootContainer.style.minWidth = targetWidth;
        rootContainer.style.height = 600 * scale;
        rootContainer.style.minHeight = 400 * scale;
        rootContainer.style.maxHeight = 600 * scale;

        // Apply default styling.
        RCCP_SceneToolsUI.ApplyDefaultStyling(rootContainer, !isDocked);

        // Smooth fade-in.
        rootContainer.style.opacity = 0;
        rootContainer.schedule.Execute(() => {
            rootContainer.style.opacity = 1;
        }).ExecuteLater(50);

        // Create Header.
        var header = RCCP_SceneToolsUI.CreateHeaderRow(ClosePanel, ToggleDocking, isDocked, true);
        rootContainer.Add(header);

        // Store reference to dock button for updates.
        dockToggleButton = header.Q<Button>("dock-toggle-button");

        // Create Search Bar.
        rootContainer.Add(RCCP_SceneToolsUI.CreateSearchBar(controller, (query) => {
            LoadTabContent();
        }));

        // Create Tab Bar.
        tabContainer = RCCP_SceneToolsUI.CreateTabBar(controller, (index) => {
            controller.CurrentTabIndex = index;
            RCCP_SceneToolsUI.UpdateTabButtons(tabContainer, index);
            LoadTabContent();
        });
        rootContainer.Add(tabContainer);

        // Content Container.
        contentContainer = new VisualElement();
        contentContainer.name = "rccp-content-container";
        contentContainer.style.flexGrow = 1;
        contentContainer.style.flexShrink = 1;
        contentContainer.style.overflow = Overflow.Hidden;
        contentContainer.style.paddingTop = 6 * scale;
        contentContainer.style.paddingBottom = 6 * scale;
        contentContainer.style.paddingLeft = 6 * scale;
        contentContainer.style.paddingRight = 6 * scale;
        rootContainer.Add(contentContainer);

        // Initial Content Load.
        LoadTabContent();

        // Footer text varies based on AI Assistant availability.
        string footerText = RCCP_SceneToolsController.IsAIAssistantInstalled
            ? "RCCP Scene Tools + AI Assistant"
            : "RCCP Scene Tools";
        rootContainer.Add(RCCP_SceneToolsUI.CreateFooter(footerText));

        // First-time Tooltip.
        if (RCCP_OverlayDockingHelper.IsFirstTimeUser()) {
            var tooltip = RCCP_OverlayDockingHelper.CreateFirstTimeTooltip();
            rootContainer.Add(tooltip);
        }

        // Register callbacks.
        rootContainer.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        rootContainer.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

        // Context Menu.
        rootContainer.RegisterCallback<ContextClickEvent>(evt => {
            RCCP_OverlayDockingHelper.ShowContextMenu(evt.mousePosition, ClosePanel);
            evt.StopPropagation();
        });

        return rootContainer;

    }
#endif

    private void LoadTabContent() {

        contentContainer.Clear();
        var provider = controller.GetCurrentContentProvider();

        if (provider != null) {
            VisualElement content = provider.CreateContent(controller.CurrentSearchQuery);
            if (content != null) {
                content.style.flexGrow = 1;
                content.style.flexShrink = 1;
                content.style.height = Length.Percent(100);
                contentContainer.Add(content);
            }
        }

    }

    #endregion

    #region Update Loop

    private void OnAttachToPanel(AttachToPanelEvent evt) {
        EditorApplication.update += OnUpdate;
    }

    private void OnDetachFromPanel(DetachFromPanelEvent evt) {
        EditorApplication.update -= OnUpdate;
    }

    private void OnUpdate() {

        controller.Update();
        CheckDockingState();

    }

    /// <summary>
    /// Checks and tracks the overlay docking state.
    /// </summary>
    private void CheckDockingState() {

#if UNITY_2021_2_OR_NEWER
        // Check if overlay is docked.
        bool isCurrentlyDocked = this.collapsed && this.isInToolbar;
        bool wasDockedBefore = RCCP_OverlayDockingHelper.GetLastDockedState();

        // Detect state change.
        if (isCurrentlyDocked != wasDockedBefore) {

            RCCP_OverlayDockingHelper.SaveDockedState(isCurrentlyDocked);

            if (!RCCP_OverlayDockingHelper.IsFirstTimeUser()) {
                RCCP_OverlayDockingHelper.ShowDockingNotification(isCurrentlyDocked);
            }

            UpdateDockButtonState();
            SceneView.RepaintAll();

        }
#endif

    }

    private void UpdateDockButtonState() {
#if UNITY_2021_2_OR_NEWER
        if (dockToggleButton == null) return;

        bool isDocked = this.collapsed && this.isInToolbar;

        dockToggleButton.text = isDocked ? "\u2197" : "\u2199";
        dockToggleButton.tooltip = isDocked ? "Undock from toolbar (float panel)" : "Show docking instructions";

        dockToggleButton.style.backgroundColor = isDocked
            ? new Color(0.86f, 0.49f, 0.24f, 0.85f) // Orange when docked
            : new Color(0.35f, 0.35f, 0.35f, 0.85f); // Gray when floating
#endif
    }

    #endregion

    #region Cleanup

#if UNITY_2021_2_OR_NEWER
    public override void OnWillBeDestroyed() {

        base.OnWillBeDestroyed();

        floatingPositionChanged -= OnFloatingPositionChanged;

        if (controller != null) {
            controller.Cleanup();
        }

    }
#endif

    #endregion

    #region Menu Item

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Scene Tools/Toggle Overlay")]
    public static void ToggleOverlay() {

#if UNITY_2021_2_OR_NEWER
        bool currentVisibility = EditorPrefs.GetBool("RCCP_Overlay_Visible", false);
        EditorPrefs.SetBool("RCCP_Overlay_Visible", !currentVisibility);

        // Force repaint to update overlay visibility.
        SceneView.RepaintAll();

        Debug.Log($"[RCCP] Scene Tools Overlay {(!currentVisibility ? "enabled" : "disabled")}. " +
                  "You may need to enable it from the Scene view overlay menu (three dots).");
#else
        // For older Unity versions, open the Window instead.
        RCCP_SceneToolsWindow.ShowWindow();
#endif

    }

    #endregion

}

#endif
