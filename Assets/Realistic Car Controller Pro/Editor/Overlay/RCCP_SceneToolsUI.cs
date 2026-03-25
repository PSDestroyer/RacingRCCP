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
using System.Linq;

/// <summary>
/// Shared UI builder for RCCP Scene Tools.
/// Handles visual construction and styling for both Overlay and Window versions.
/// </summary>
public static class RCCP_SceneToolsUI {

    #region Styling

    /// <summary>
    /// Gets the current scale factor for UI elements (dynamic DPI-based).
    /// </summary>
    public static float GetScaleFactor() {
        return Mathf.Clamp(EditorGUIUtility.pixelsPerPoint * 0.8f, 0.75f, 1f);
    }

    public static void ApplyDefaultStyling(VisualElement element, bool isFloating = false) {

        // Modern backgrounds with better contrast.
        element.style.backgroundColor = EditorGUIUtility.isProSkin
            ? new Color(0.14f, 0.14f, 0.14f)  // Darker for Pro skin #232323
            : new Color(0.86f, 0.86f, 0.86f); // Lighter for Personal skin #DADADA

        element.style.color = EditorGUIUtility.isProSkin
            ? Color.white
            : Color.black;

        // Improved border contrast.
        Color borderColor = EditorGUIUtility.isProSkin
            ? new Color(0.08f, 0.08f, 0.08f)  // Darker borders for Pro
            : new Color(0.6f, 0.6f, 0.6f);    // Darker borders for Personal

        element.style.borderTopWidth = 1;
        element.style.borderBottomWidth = 1;
        element.style.borderLeftWidth = 1;
        element.style.borderRightWidth = 1;
        element.style.borderTopColor = borderColor;
        element.style.borderBottomColor = borderColor;
        element.style.borderLeftColor = borderColor;
        element.style.borderRightColor = borderColor;

        if (isFloating) {
            element.style.borderTopWidth = 2;
            element.style.borderBottomWidth = 2;
            element.style.borderLeftWidth = 2;
            element.style.borderRightWidth = 2;
        }

    }

    private static void ApplyActiveTabStyle(Button tabButton) {

        // Higher contrast active color - RCCP orange accent.
        tabButton.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.4f); // Orange with transparency
        tabButton.style.color = Color.white;

        // 2px bottom indicator for active tab.
        tabButton.style.borderBottomWidth = 2;
        tabButton.style.borderBottomColor = new Color(0.86f, 0.49f, 0.24f); // Orange

    }

    #endregion

    #region UI Components

    public static VisualElement CreateHeaderRow(System.Action onClose, System.Action onDockToggle = null, bool isDocked = false, bool showDockButton = true) {

        float scale = GetScaleFactor();
        VisualElement headerRow = new VisualElement();
        headerRow.name = "rccp-header-row";
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.height = 30 * scale;
        headerRow.style.minHeight = 30 * scale;
        headerRow.style.maxHeight = 30 * scale;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.paddingRight = 6 * scale;
        headerRow.style.paddingLeft = 6 * scale;
        headerRow.style.paddingTop = 4 * scale;
        headerRow.style.paddingBottom = 4 * scale;

        // Modern gradient-style background.
        headerRow.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);

        // Thicker bottom border.
        headerRow.style.borderBottomWidth = 2;
        headerRow.style.borderBottomColor = new Color(0.04f, 0.04f, 0.04f);

        // Title.
        Label titleLabel = new Label("RCCP Scene Tools");
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 12 * scale;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerRow.Add(titleLabel);

        // Button container.
        VisualElement buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;
        buttonContainer.style.alignItems = Align.Center;

        // Close Button (if provided).
        if (onClose != null) {
            Button closeButton = RCCP_OverlayDockingHelper.CreateCloseButtonInline(onClose);
            buttonContainer.Add(closeButton);
        }

        // Dock Button.
        if (showDockButton && onDockToggle != null) {
            Button dockToggleButton = RCCP_OverlayDockingHelper.CreateDockToggleButtonInline(onDockToggle, isDocked);
            dockToggleButton.name = "dock-toggle-button";
            buttonContainer.Add(dockToggleButton);
        }

        // Help Button.
        Button helpButton = RCCP_OverlayDockingHelper.CreateHelpButtonInline();
        buttonContainer.Add(helpButton);

        headerRow.Add(buttonContainer);

        return headerRow;

    }

    public static VisualElement CreateSearchBar(RCCP_SceneToolsController controller, System.Action<string> onSearchChanged) {

        float scale = GetScaleFactor();
        VisualElement searchContainer = new VisualElement();
        searchContainer.name = "rccp-search-container";
        searchContainer.style.flexDirection = FlexDirection.Row;
        searchContainer.style.height = 32 * scale;
        searchContainer.style.minHeight = 32 * scale;
        searchContainer.style.maxHeight = 32 * scale;
        searchContainer.style.paddingTop = 5 * scale;
        searchContainer.style.paddingBottom = 5 * scale;
        searchContainer.style.paddingLeft = 6 * scale;
        searchContainer.style.paddingRight = 6 * scale;

        searchContainer.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.5f);
        searchContainer.style.borderBottomWidth = 1;
        searchContainer.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f);

        Label searchIcon = new Label("\ud83d\udd0d");
        searchIcon.style.marginRight = 6 * scale;
        searchIcon.style.fontSize = 13 * scale;
        searchIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
        searchIcon.style.color = new Color(0.5f, 0.5f, 0.5f);
        searchContainer.Add(searchIcon);

        TextField searchField = new TextField();
        searchField.name = "rccp-search-field";
        searchField.style.flexGrow = 1;
        searchField.style.fontSize = 11 * scale;
        searchField.value = controller.CurrentSearchQuery;
        searchField.tooltip = "Search RCCP vehicles and components...";

        searchField.style.borderTopLeftRadius = 4;
        searchField.style.borderTopRightRadius = 4;
        searchField.style.borderBottomLeftRadius = 4;
        searchField.style.borderBottomRightRadius = 4;

        // Clear Button.
        Button clearButton = new Button(() => {
            controller.ClearSearch();
            searchField.value = "";
            onSearchChanged?.Invoke("");
        });
        clearButton.name = "clear-button";
        clearButton.text = "\u2715";
        clearButton.tooltip = "Clear search";
        clearButton.style.width = 20 * scale;
        clearButton.style.height = 20 * scale;
        clearButton.style.fontSize = 12 * scale;
        clearButton.style.display = string.IsNullOrEmpty(controller.CurrentSearchQuery) ? DisplayStyle.None : DisplayStyle.Flex;
        clearButton.style.marginLeft = 4 * scale;

        clearButton.style.borderTopLeftRadius = 3;
        clearButton.style.borderTopRightRadius = 3;
        clearButton.style.borderBottomLeftRadius = 3;
        clearButton.style.borderBottomRightRadius = 3;

        clearButton.RegisterCallback<MouseEnterEvent>(evt => {
            clearButton.style.backgroundColor = new Color(0.85f, 0.29f, 0.29f, 0.8f);
        });
        clearButton.RegisterCallback<MouseLeaveEvent>(evt => {
            clearButton.style.backgroundColor = Color.clear;
        });

        // Search Callback.
        searchField.RegisterValueChangedCallback(evt => {
            controller.CurrentSearchQuery = evt.newValue;
            controller.AddToSearchHistory(evt.newValue);
            clearButton.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.None : DisplayStyle.Flex;
            onSearchChanged?.Invoke(evt.newValue);
        });

        searchContainer.Add(searchField);
        searchContainer.Add(clearButton);

        return searchContainer;

    }

    public static VisualElement CreateTabBar(RCCP_SceneToolsController controller, System.Action<int> onTabSelected) {

        float scale = GetScaleFactor();
        VisualElement tabContainer = new VisualElement();
        tabContainer.name = "rccp-tab-container";
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.height = 32 * scale;
        tabContainer.style.minHeight = 32 * scale;
        tabContainer.style.maxHeight = 32 * scale;
        tabContainer.style.borderBottomWidth = 1;
        tabContainer.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
        tabContainer.style.marginBottom = 4 * scale;

        for (int i = 0; i < controller.TabNames.Length; i++) {

            int tabIndex = i;
            Button tabButton = new Button(() => onTabSelected(tabIndex));
            tabButton.text = controller.TabNames[i];
            tabButton.name = $"rccp-tab-{controller.TabNames[i].ToLower().Replace(" ", "-")}";
            tabButton.AddToClassList("rccp-tab-button");

            tabButton.style.flexGrow = 1;
            tabButton.style.paddingTop = 6 * scale;
            tabButton.style.paddingBottom = 6 * scale;
            tabButton.style.paddingLeft = 2 * scale;
            tabButton.style.paddingRight = 2 * scale;
            tabButton.style.fontSize = 10 * scale;
            tabButton.style.unityTextAlign = TextAnchor.MiddleCenter;

            tabButton.style.borderTopLeftRadius = 4;
            tabButton.style.borderTopRightRadius = 4;

            // Initial State.
            if (i == controller.CurrentTabIndex) {
                tabButton.AddToClassList("rccp-tab-active");
                ApplyActiveTabStyle(tabButton);
            } else {
                tabButton.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
                tabButton.style.color = new Color(0.7f, 0.7f, 0.7f);

                tabButton.RegisterCallback<MouseEnterEvent>(evt => {
                    if (!tabButton.ClassListContains("rccp-tab-active")) {
                        tabButton.style.backgroundColor = new Color(0.23f, 0.23f, 0.23f);
                    }
                });

                tabButton.RegisterCallback<MouseLeaveEvent>(evt => {
                    if (!tabButton.ClassListContains("rccp-tab-active")) {
                        tabButton.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
                    }
                });
            }

            tabContainer.Add(tabButton);

        }

        return tabContainer;

    }

    public static void UpdateTabButtons(VisualElement tabContainer, int currentTabIndex) {

        var tabButtons = tabContainer.Query<Button>().ToList();

        for (int i = 0; i < tabButtons.Count; i++) {

            if (i == currentTabIndex) {
                tabButtons[i].AddToClassList("rccp-tab-active");
                ApplyActiveTabStyle(tabButtons[i]);
            } else {
                tabButtons[i].RemoveFromClassList("rccp-tab-active");
                tabButtons[i].style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
                tabButtons[i].style.color = new Color(0.7f, 0.7f, 0.7f);
                tabButtons[i].style.borderBottomWidth = 0;
            }

        }

    }

    public static VisualElement CreateFooter(string versionText = "RCCP Scene Tools") {

        float scale = GetScaleFactor();
        VisualElement footerRoot = new VisualElement();

        VisualElement separator = new VisualElement();
        separator.style.height = 1;
        separator.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        separator.style.marginTop = 4 * scale;
        separator.style.marginBottom = 4 * scale;
        footerRoot.Add(separator);

        VisualElement footerContainer = new VisualElement();
        footerContainer.name = "rccp-footer";
        footerContainer.style.flexDirection = FlexDirection.Row;
        footerContainer.style.justifyContent = Justify.Center;
        footerContainer.style.paddingTop = 4 * scale;
        footerContainer.style.paddingBottom = 4 * scale;

        Label versionLabel = new Label(versionText);
        versionLabel.style.fontSize = 9 * scale;
        versionLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
        versionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        footerContainer.Add(versionLabel);

        footerRoot.Add(footerContainer);
        return footerRoot;

    }

    #endregion

}

#endif
