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
using UnityEditor.ShortcutManagement;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Handles global keyboard shortcuts for the RCCP AI Assistant.
/// Uses Unity's ShortcutManagement API for proper shortcut handling.
/// Default shortcut: Ctrl+Shift+W (Cmd+Shift+W on Mac)
/// Users can rebind via Edit > Shortcuts > RCCP AI Assistant
/// </summary>
public static class RCCP_AIShortcutHandler {

    private const string SHORTCUT_ID = "RCCP AI Assistant/Open Window";

    /// <summary>
    /// Opens the RCCP AI Assistant window.
    /// Default shortcut: Ctrl+Shift+W (Cmd+Shift+W on Mac)
    /// Can be rebound in Edit > Shortcuts > RCCP AI Assistant
    /// </summary>
    [Shortcut(SHORTCUT_ID, KeyCode.W, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
    public static void OpenAIAssistant() {
        // Check if shortcuts are enabled in preferences
        if (!RCCP_AIEditorPrefs.EnableShortcuts) return;

        RCCP_AIAssistantWindow.ShowWindow();
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
