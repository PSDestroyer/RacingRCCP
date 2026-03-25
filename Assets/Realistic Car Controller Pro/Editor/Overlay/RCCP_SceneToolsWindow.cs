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

/// <summary>
/// EditorWindow version of RCCP Scene Tools.
/// Provides the same functionality as the Overlay for Unity versions that don't support Overlays.
/// </summary>
public class RCCP_SceneToolsWindow : EditorWindow {

    #region Variables

    private RCCP_SceneToolsController controller;

    // UI Elements.
    private VisualElement rootContainer;
    private VisualElement contentContainer;
    private VisualElement tabContainer;

    #endregion

    #region Window Management

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Scene Tools/Open Window")]
    public static void ShowWindow() {

        var window = GetWindow<RCCP_SceneToolsWindow>();
        window.titleContent = new GUIContent("RCCP Scene Tools");
        window.minSize = new Vector2(350, 400);
        window.Show();

    }

    #endregion

    #region Lifecycle

    private void OnEnable() {

        // Initialize controller.
        controller = new RCCP_SceneToolsController("RCCP_Window");

        // Build UI.
        BuildUI();

        // Register update callback.
        EditorApplication.update += OnUpdate;

    }

    private void OnDisable() {

        EditorApplication.update -= OnUpdate;

        if (controller != null) {
            controller.Cleanup();
        }

    }

    #endregion

    #region UI Creation

    private void BuildUI() {

        float scale = RCCP_SceneToolsUI.GetScaleFactor();

        // Clear existing content.
        rootVisualElement.Clear();

        // Create root container.
        rootContainer = new VisualElement();
        rootContainer.name = "rccp-window-root";
        rootContainer.style.flexGrow = 1;
        rootContainer.style.flexShrink = 1;

        // Apply default styling.
        RCCP_SceneToolsUI.ApplyDefaultStyling(rootContainer, false);

        // Create Header (no close button or dock button for window).
        var header = RCCP_SceneToolsUI.CreateHeaderRow(null, null, false, false);
        rootContainer.Add(header);

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

        // Add root to window.
        rootVisualElement.Add(rootContainer);

    }

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

    private void OnUpdate() {

        if (controller != null) {
            controller.Update();
        }

    }

    #endregion

}

#endif
