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
using System;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
#endif

/// <summary>
/// Helper class that provides docking guidance and tips for the RCCP Scene View Overlay.
/// Helps users understand how to dock/undock the overlay panel.
/// </summary>
public static class RCCP_OverlayDockingHelper {

    #region Constants

    // Preference keys for tracking user experience.
    private const string PREF_FIRST_TIME_SHOWN = "RCCP_Overlay_FirstTimeShown";
    private const string PREF_DOCKING_TIP_SHOWN = "RCCP_Overlay_DockingTipShown";
    private const string PREF_HELP_DISMISSED_COUNT = "RCCP_Overlay_HelpDismissedCount";
    private const string PREF_LAST_DOCKED_STATE = "RCCP_Overlay_LastDockedState";

    #endregion

    #region Public Methods

    /// <summary>
    /// Checks if this is the first time the overlay is being shown.
    /// </summary>
    public static bool IsFirstTimeUser() {

        return !EditorPrefs.GetBool(PREF_FIRST_TIME_SHOWN, false);

    }

    /// <summary>
    /// Marks that the overlay has been shown to the user.
    /// </summary>
    public static void MarkAsShown() {

        EditorPrefs.SetBool(PREF_FIRST_TIME_SHOWN, true);

    }

    /// <summary>
    /// Shows a help dialog explaining how to dock the overlay.
    /// </summary>
    public static void ShowDockingHelp() {

        string title = "RCCP Scene Tools - Docking Guide";
        string message = "The RCCP Scene Tools panel can be docked to your Scene view toolbar for quick access!\n\n" +
                       "HOW TO DOCK:\n" +
                       "- Drag the panel header (where it says 'RCCP Scene Tools')\n" +
                       "- Drop it onto the Scene view toolbar (near the top)\n" +
                       "- The panel will snap into place as a button\n\n" +
                       "HOW TO UNDOCK:\n" +
                       "- Click the docked button to show the panel\n" +
                       "- Drag the panel away from the toolbar\n\n" +
                       "HOW TO CLOSE:\n" +
                       "- Click the x button in the top-right corner\n" +
                       "- Reopen via Tools > BoneCracker Games > Realistic Car Controller Pro > Scene Tools\n\n" +
                       "TIPS:\n" +
                       "- Right-click the panel header for more options\n" +
                       "- The panel remembers its docked/undocked state";

        EditorUtility.DisplayDialog(title, message, "Got it!");
        EditorPrefs.SetBool(PREF_DOCKING_TIP_SHOWN, true);

    }

    /// <summary>
    /// Creates a help button for inline header placement.
    /// </summary>
    public static Button CreateHelpButtonInline() {

        Button helpButton = new Button(() => ShowDockingHelp());
        helpButton.text = "?";
        helpButton.tooltip = "Learn how to dock this panel to the toolbar";
        helpButton.style.width = 22;
        helpButton.style.height = 22;
        helpButton.style.marginLeft = 4;

        // Modern styling - RCCP orange accent.
        helpButton.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.85f); // Orange
        helpButton.style.color = Color.white;
        helpButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        helpButton.style.fontSize = 12;

        // Rounded corners.
        helpButton.style.borderTopLeftRadius = 4;
        helpButton.style.borderTopRightRadius = 4;
        helpButton.style.borderBottomLeftRadius = 4;
        helpButton.style.borderBottomRightRadius = 4;

        // Hover effect.
        helpButton.RegisterCallback<MouseEnterEvent>(evt => {
            helpButton.style.backgroundColor = new Color(0.96f, 0.59f, 0.34f, 1f); // Brighter orange on hover
        });

        helpButton.RegisterCallback<MouseLeaveEvent>(evt => {
            helpButton.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.85f);
        });

        return helpButton;

    }

    /// <summary>
    /// Creates a close button for inline header placement.
    /// </summary>
    public static Button CreateCloseButtonInline(System.Action onClose) {

        Button closeButton = new Button(onClose);
        closeButton.text = "\u00d7";
        closeButton.tooltip = "Close panel (reopen via Tools > BoneCracker Games > Realistic Car Controller Pro > Scene Tools)";
        closeButton.style.width = 22;
        closeButton.style.height = 22;
        closeButton.style.marginLeft = 4;

        // Modern styling - red color for close action.
        closeButton.style.backgroundColor = new Color(0.79f, 0.29f, 0.29f, 0.85f); // Softer red #C94A4A
        closeButton.style.color = Color.white;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.fontSize = 16;

        // Rounded corners.
        closeButton.style.borderTopLeftRadius = 4;
        closeButton.style.borderTopRightRadius = 4;
        closeButton.style.borderBottomLeftRadius = 4;
        closeButton.style.borderBottomRightRadius = 4;

        // Hover effect.
        closeButton.RegisterCallback<MouseEnterEvent>(evt => {
            closeButton.style.backgroundColor = new Color(0.89f, 0.39f, 0.39f, 1f); // Brighter red
        });

        closeButton.RegisterCallback<MouseLeaveEvent>(evt => {
            closeButton.style.backgroundColor = new Color(0.79f, 0.29f, 0.29f, 0.85f);
        });

        return closeButton;

    }

    /// <summary>
    /// Creates a dock/undock toggle button for inline header placement.
    /// </summary>
    public static Button CreateDockToggleButtonInline(System.Action onClick, bool isDocked) {

        Button dockButton = new Button(onClick);
        dockButton.text = isDocked ? "\u2197" : "\u2199"; // Arrow symbols.
        dockButton.tooltip = isDocked
            ? "Undock from toolbar (float panel)"
            : "Show docking instructions";
        dockButton.style.width = 22;
        dockButton.style.height = 22;
        dockButton.style.marginLeft = 4;

        // Modern styling with better contrast.
        Color normalColor = isDocked
            ? new Color(0.86f, 0.49f, 0.24f, 0.85f)  // Orange when docked
            : new Color(0.35f, 0.35f, 0.35f, 0.85f); // Gray when floating

        Color hoverColor = isDocked
            ? new Color(0.96f, 0.59f, 0.34f, 1f)     // Brighter orange on hover
            : new Color(0.45f, 0.45f, 0.45f, 1f);    // Lighter gray on hover

        dockButton.style.backgroundColor = normalColor;
        dockButton.style.color = Color.white;
        dockButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        dockButton.style.fontSize = 14;

        // Rounded corners.
        dockButton.style.borderTopLeftRadius = 4;
        dockButton.style.borderTopRightRadius = 4;
        dockButton.style.borderBottomLeftRadius = 4;
        dockButton.style.borderBottomRightRadius = 4;

        // Hover effects.
        dockButton.RegisterCallback<MouseEnterEvent>(evt => {
            dockButton.style.backgroundColor = hoverColor;
        });

        dockButton.RegisterCallback<MouseLeaveEvent>(evt => {
            dockButton.style.backgroundColor = normalColor;
        });

        dockButton.SetEnabled(true);

        return dockButton;

    }

    /// <summary>
    /// Creates a first-time user tooltip.
    /// </summary>
    public static VisualElement CreateFirstTimeTooltip() {

        VisualElement tooltip = new VisualElement();
        tooltip.name = "first-time-tooltip";
        tooltip.style.position = Position.Absolute;

        // RCCP orange gradient-style background.
        tooltip.style.backgroundColor = new Color(0.86f, 0.49f, 0.24f, 0.96f);
        tooltip.style.paddingTop = 12;
        tooltip.style.paddingBottom = 12;
        tooltip.style.paddingLeft = 16;
        tooltip.style.paddingRight = 16;
        tooltip.style.top = 40;
        tooltip.style.left = 10;
        tooltip.style.right = 10;

        // Rounded corners.
        tooltip.style.borderTopLeftRadius = 6;
        tooltip.style.borderTopRightRadius = 6;
        tooltip.style.borderBottomLeftRadius = 6;
        tooltip.style.borderBottomRightRadius = 6;

        // Subtle border.
        tooltip.style.borderTopWidth = 1;
        tooltip.style.borderBottomWidth = 1;
        tooltip.style.borderLeftWidth = 1;
        tooltip.style.borderRightWidth = 1;
        tooltip.style.borderTopColor = new Color(1f, 0.7f, 0.5f, 0.5f);
        tooltip.style.borderBottomColor = new Color(0.6f, 0.3f, 0.1f, 0.8f);
        tooltip.style.borderLeftColor = new Color(0.9f, 0.5f, 0.3f, 0.6f);
        tooltip.style.borderRightColor = new Color(0.9f, 0.5f, 0.3f, 0.6f);

        // Arrow pointing to header.
        VisualElement arrow = new VisualElement();
        arrow.style.position = Position.Absolute;
        arrow.style.width = 0;
        arrow.style.height = 0;
        arrow.style.borderBottomWidth = 10;
        arrow.style.borderBottomColor = new Color(0.86f, 0.49f, 0.24f, 0.96f);
        arrow.style.borderLeftWidth = 10;
        arrow.style.borderLeftColor = Color.clear;
        arrow.style.borderRightWidth = 10;
        arrow.style.borderRightColor = Color.clear;
        arrow.style.top = -10;
        arrow.style.left = 20;
        tooltip.Add(arrow);

        // Title.
        Label titleLabel = new Label("TIP: You can dock this panel!");
        titleLabel.style.color = Color.white;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.fontSize = 13;
        titleLabel.style.marginBottom = 6;
        tooltip.Add(titleLabel);

        // Description.
        Label descLabel = new Label("Drag this panel to the Scene view toolbar to dock it as a button for quick access.");
        descLabel.style.color = new Color(0.95f, 0.95f, 0.95f);
        descLabel.style.fontSize = 11;
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.marginBottom = 10;
        tooltip.Add(descLabel);

        // Dismiss button.
        Button dismissButton = new Button(() => {
            tooltip.style.display = DisplayStyle.None;
            MarkAsShown();
        });
        dismissButton.text = "Got it";
        dismissButton.style.alignSelf = Align.FlexEnd;
        dismissButton.style.backgroundColor = new Color(0.66f, 0.29f, 0.04f, 0.9f); // Darker orange
        dismissButton.style.color = Color.white;
        dismissButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        dismissButton.style.paddingTop = 6;
        dismissButton.style.paddingBottom = 6;
        dismissButton.style.paddingLeft = 16;
        dismissButton.style.paddingRight = 16;
        dismissButton.style.borderTopLeftRadius = 4;
        dismissButton.style.borderTopRightRadius = 4;
        dismissButton.style.borderBottomLeftRadius = 4;
        dismissButton.style.borderBottomRightRadius = 4;

        // Hover effect on dismiss button.
        dismissButton.RegisterCallback<MouseEnterEvent>(evt => {
            dismissButton.style.backgroundColor = new Color(0.76f, 0.39f, 0.14f, 1f);
        });

        dismissButton.RegisterCallback<MouseLeaveEvent>(evt => {
            dismissButton.style.backgroundColor = new Color(0.66f, 0.29f, 0.04f, 0.9f);
        });

        tooltip.Add(dismissButton);

        // Auto-hide after 10 seconds.
        var hideTimer = EditorApplication.timeSinceStartup;
        EditorApplication.CallbackFunction autoHide = null;
        autoHide = () => {
            if (EditorApplication.timeSinceStartup - hideTimer > 10) {
                if (tooltip.style.display != DisplayStyle.None) {
                    tooltip.style.display = DisplayStyle.None;
                    MarkAsShown();
                }
                EditorApplication.update -= autoHide;
            }
        };
        EditorApplication.update += autoHide;

        return tooltip;

    }

    /// <summary>
    /// Creates a context menu for the overlay.
    /// </summary>
    public static void ShowContextMenu(Vector2 mousePosition, System.Action closeAction = null) {

        GenericMenu menu = new GenericMenu();

        if (closeAction != null) {

            menu.AddItem(new GUIContent("Close Panel"), false, () => {
                closeAction();
            });

            menu.AddSeparator("");

        }

        menu.AddItem(new GUIContent("Dock to Toolbar"), false, () => {
            Debug.Log("[RCCP] To dock: Drag the panel header to the Scene view toolbar");
            ShowDockingHelp();
        });

        menu.AddItem(new GUIContent("Show Docking Help"), false, ShowDockingHelp);

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Reset Position"), false, () => {
            Debug.Log("[RCCP] Panel position reset to default");
        });

        // Only show AI Assistant option if installed.
        if (RCCP_SceneToolsController.IsAIAssistantInstalled) {
            menu.AddItem(new GUIContent("Open AI Assistant Window"), false, () => {
                // Use reflection to call ShowWindow since it's in a separate package.
                var windowType = System.Type.GetType("RCCP_AIAssistantWindow, Assembly-CSharp-Editor");
                if (windowType != null) {
                    var method = windowType.GetMethod("ShowWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    method?.Invoke(null, null);
                }
            });
        }

        menu.ShowAsContext();

    }

    /// <summary>
    /// Saves the current docked state.
    /// </summary>
    public static void SaveDockedState(bool isDocked) {

        EditorPrefs.SetBool(PREF_LAST_DOCKED_STATE, isDocked);

    }

    /// <summary>
    /// Gets the last docked state.
    /// </summary>
    public static bool GetLastDockedState() {

        return EditorPrefs.GetBool(PREF_LAST_DOCKED_STATE, false);

    }

    /// <summary>
    /// Shows a notification when the panel is docked/undocked.
    /// </summary>
    public static void ShowDockingNotification(bool isDocked) {

        if (SceneView.lastActiveSceneView != null) {

            string message = isDocked ?
                "Panel docked to toolbar! Click the button to toggle." :
                "Panel undocked. Drag to toolbar to re-dock.";

            SceneView.lastActiveSceneView.ShowNotification(new GUIContent(message), 2f);

        }

    }

    #endregion

}

#endif
