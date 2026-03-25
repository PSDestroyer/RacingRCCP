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

/// <summary>
/// Utility to auto-assign textures to the RCCP AI GUISkin.
/// Run from menu: Tools > BoneCracker Games > RCCP AI Assistant > Setup GUISkin Textures
/// </summary>
public static class RCCP_AIGUISkinSetup {

    // Use dynamic paths from RCCP_AIUtility
    private static string SKIN_PATH => RCCP_AIUtility.GUISkinPath;
    private static string TEXTURES_PATH => RCCP_AIUtility.TexturesPath + "/";

    // Internal method - no menu item, call from code or setup wizard
    public static void SetupGUISkinTextures() {
        // Load the GUISkin
        GUISkin skin = AssetDatabase.LoadAssetAtPath<GUISkin>(SKIN_PATH);
        if (skin == null) {
            Debug.LogError($"[RCCP AI] GUISkin not found at: {SKIN_PATH}");
            return;
        }

        int assignedCount = 0;

        // === BUTTON ===
        assignedCount += AssignTexture(skin.button.normal, "btn_normal");
        assignedCount += AssignTexture(skin.button.hover, "btn_hover");
        assignedCount += AssignTexture(skin.button.active, "btn_active");
        assignedCount += AssignTexture(skin.button.onNormal, "btn_on_normal");
        assignedCount += AssignTexture(skin.button.onHover, "btn_on_hover");
        assignedCount += AssignTexture(skin.button.onActive, "btn_on_active");

        // === TOGGLE ===
        assignedCount += AssignTexture(skin.toggle.normal, "toggle_off");
        assignedCount += AssignTexture(skin.toggle.hover, "toggle_off_hover", "toggle_off"); // fallback
        assignedCount += AssignTexture(skin.toggle.active, "toggle_off_active", "toggle_off"); // fallback
        assignedCount += AssignTexture(skin.toggle.onNormal, "toggle_on");
        assignedCount += AssignTexture(skin.toggle.onHover, "toggle_on_hover", "toggle_on"); // fallback
        assignedCount += AssignTexture(skin.toggle.onActive, "toggle_on_active", "toggle_on"); // fallback

        // === TEXT FIELD ===
        assignedCount += AssignTexture(skin.textField.normal, "field_normal");
        assignedCount += AssignTexture(skin.textField.hover, "field_hover", "field_normal"); // fallback
        assignedCount += AssignTexture(skin.textField.focused, "field_focused");
        assignedCount += AssignTexture(skin.textField.onNormal, "field_focused"); // use focused for on state

        // === TEXT AREA ===
        assignedCount += AssignTexture(skin.textArea.normal, "field_normal");
        assignedCount += AssignTexture(skin.textArea.hover, "field_hover", "field_normal"); // fallback
        assignedCount += AssignTexture(skin.textArea.focused, "field_focused");
        assignedCount += AssignTexture(skin.textArea.onNormal, "field_focused");

        // === BOX ===
        assignedCount += AssignTexture(skin.box.normal, "box_bg", "field_normal"); // fallback to field

        // === WINDOW ===
        assignedCount += AssignTexture(skin.window.normal, "window_bg", "box_bg", "field_normal");
        assignedCount += AssignTexture(skin.window.onNormal, "window_bg", "box_bg", "field_normal");

        // === HORIZONTAL SLIDER ===
        assignedCount += AssignTexture(skin.horizontalSlider.normal, "slider_track", "slider_h_track");
        assignedCount += AssignTexture(skin.horizontalSliderThumb.normal, "slider_thumb", "slider_h_thumb");
        assignedCount += AssignTexture(skin.horizontalSliderThumb.hover, "slider_thumb_hover", "slider_thumb");
        assignedCount += AssignTexture(skin.horizontalSliderThumb.active, "slider_thumb_active", "slider_thumb");

        // === VERTICAL SLIDER ===
        assignedCount += AssignTexture(skin.verticalSlider.normal, "slider_track_v", "slider_track");
        assignedCount += AssignTexture(skin.verticalSliderThumb.normal, "slider_thumb");
        assignedCount += AssignTexture(skin.verticalSliderThumb.hover, "slider_thumb_hover", "slider_thumb");
        assignedCount += AssignTexture(skin.verticalSliderThumb.active, "slider_thumb_active", "slider_thumb");

        // === HORIZONTAL SCROLLBAR ===
        assignedCount += AssignTexture(skin.horizontalScrollbar.normal, "scrollbar_track", "scrollbar_h_track");
        assignedCount += AssignTexture(skin.horizontalScrollbarThumb.normal, "scrollbar_thumb", "scrollbar_h_thumb");

        // === VERTICAL SCROLLBAR ===
        assignedCount += AssignTexture(skin.verticalScrollbar.normal, "scrollbar_track_v", "scrollbar_track");
        assignedCount += AssignTexture(skin.verticalScrollbarThumb.normal, "scrollbar_thumb", "scrollbar_v_thumb");

        // Mark the skin as dirty and save
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[RCCP AI] GUISkin setup complete! Assigned {assignedCount} textures.");

        // Select the skin in the project
        Selection.activeObject = skin;
        EditorGUIUtility.PingObject(skin);
    }

    /// <summary>
    /// Assigns a texture to a GUIStyleState, with fallback options.
    /// </summary>
    private static int AssignTexture(GUIStyleState state, params string[] textureNames) {
        foreach (string name in textureNames) {
            if (string.IsNullOrEmpty(name)) continue;

            string path = TEXTURES_PATH + name + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (texture != null) {
                state.background = texture;
                return 1;
            }
        }

        // No texture found
        if (textureNames.Length > 0 && !string.IsNullOrEmpty(textureNames[0])) {
            Debug.LogWarning($"[RCCP AI] Texture not found: {textureNames[0]}.png");
        }
        return 0;
    }

    // Validation method for internal use
    public static bool ValidateSetupGUISkinTextures() {
        return AssetDatabase.LoadAssetAtPath<GUISkin>(SKIN_PATH) != null;
    }

    /// <summary>
    /// Assigns the GUISkin to RCCP_AISettings after setup.
    /// Internal method - no menu item, call from code or setup wizard
    /// </summary>
    public static void AssignGUISkinToSettings() {
        // Load the GUISkin
        GUISkin skin = AssetDatabase.LoadAssetAtPath<GUISkin>(SKIN_PATH);
        if (skin == null) {
            Debug.LogError($"[RCCP AI] GUISkin not found at: {SKIN_PATH}");
            return;
        }

        // Load settings
        RCCP_AISettings settings = Resources.Load<RCCP_AISettings>("RCCP_AISettings");
        if (settings == null) {
            Debug.LogError("[RCCP AI] RCCP_AISettings not found in Resources folder.");
            return;
        }

        // Assign skin
        settings.customSkin = skin;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log("[RCCP AI] GUISkin assigned to RCCP_AISettings.customSkin");

        // Select settings
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    /// <summary>
    /// Quick setup: Assigns textures AND links skin to settings in one step.
    /// Internal method - no menu item, call from code or setup wizard
    /// </summary>
    public static void QuickSetupGUISkin() {
        SetupGUISkinTextures();
        AssignGUISkinToSettings();
        Debug.Log("[RCCP AI] Quick setup complete! GUISkin is ready to use.");
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
