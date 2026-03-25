//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Simplified design system for RCCP AI Assistant.
/// Returns GUI.skin styles directly - customize via GUISkin asset.
/// </summary>
public static class RCCP_AIDesignSystem {

    #region Color Constants

    /// <summary>
    /// Semantic color constants for use in code.
    /// </summary>
    public static class Colors {
        // Semantic status colors
        public static readonly Color Success = new Color32(106, 153, 85, 255);
        public static readonly Color Warning = new Color32(204, 167, 86, 255);
        public static readonly Color Error = new Color32(199, 84, 80, 255);
        public static readonly Color Info = new Color32(86, 156, 214, 255);

        // Button background colors
        public static readonly Color SuccessBg = new Color(0.28f, 0.7f, 0.35f, 1f);   // Green apply button
        public static readonly Color DangerBg = new Color(0.9f, 0.3f, 0.3f, 1f);      // Red delete button
        public static readonly Color NeutralBg = new Color(0.32f, 0.32f, 0.38f, 1f);  // Gray cancel button

        // Accent colors
        public static readonly Color AccentPrimary = new Color32(90, 155, 213, 255);
        public static readonly Color AccentMuted = new Color32(61, 110, 153, 255);
        public static readonly Color AccentCyan = new Color(0.3f, 0.8f, 1f, 1f);      // Cyan highlight
        public static readonly Color AiSuggestion = new Color32(123, 104, 181, 255);
        public static readonly Color AiPreview = new Color32(74, 107, 138, 255);

        // Text colors
        public static readonly Color TextPrimary = new Color32(212, 212, 212, 255);
        public static readonly Color TextSecondary = new Color32(154, 154, 154, 255);
        public static readonly Color TextMuted = new Color(0.68f, 0.68f, 0.72f, 1f);  // Lighter muted
        public static readonly Color TextDisabled = new Color32(90, 90, 90, 255);
        public static readonly Color TextInverse = new Color32(26, 26, 26, 255);

        // Link colors
        public static readonly Color Link = new Color(0.6f, 0.8f, 1f, 1f);
        public static readonly Color LinkHover = new Color(0.8f, 0.9f, 1f, 1f);

        // Diff colors (for showing changes)
        public static readonly Color DiffAddedBg = new Color(0.1f, 0.3f, 0.1f, 0.3f);
        public static readonly Color DiffAddedText = new Color(0.6f, 0.9f, 0.6f, 1f);
        public static readonly Color DiffRemovedBg = new Color(0.3f, 0.1f, 0.1f, 0.3f);
        public static readonly Color DiffRemovedText = new Color(0.9f, 0.6f, 0.6f, 1f);

        // Background colors
        public static readonly Color BgBase = new Color32(56, 56, 56, 255);
        public static readonly Color BgElevated = new Color32(62, 62, 62, 255);
        public static readonly Color BgRecessed = new Color32(45, 45, 45, 255);
        public static readonly Color BgHover = new Color32(74, 74, 74, 255);
        public static readonly Color BgSelected = new Color32(44, 93, 135, 255);
        public static readonly Color BgDark = new Color(0.2f, 0.2f, 0.2f, 1f);        // Darker background

        // Border colors
        public static readonly Color BorderDefault = new Color32(35, 35, 35, 255);
        public static readonly Color BorderLight = new Color32(77, 77, 77, 255);

        public static Color GetBgBase() => EditorGUIUtility.isProSkin ? BgBase : new Color32(194, 194, 194, 255);
        public static Color GetTextPrimary() => EditorGUIUtility.isProSkin ? TextPrimary : new Color32(32, 32, 32, 255);
        public static Color GetTextSecondary() => EditorGUIUtility.isProSkin ? TextSecondary : new Color32(96, 96, 96, 255);

        public static Color Lighten(Color color, float amount) => new Color(
            Mathf.Min(1f, color.r + amount),
            Mathf.Min(1f, color.g + amount),
            Mathf.Min(1f, color.b + amount),
            color.a);

        public static Color Darken(Color color, float amount) => new Color(
            Mathf.Max(0f, color.r - amount),
            Mathf.Max(0f, color.g - amount),
            Mathf.Max(0f, color.b - amount),
            color.a);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        public static Color Mix(Color a, Color b, float ratio) => Color.Lerp(a, b, ratio);
    }

    #endregion

    #region Spacing Constants

    public static class Spacing {
        public const int Space0 = 0;
        public const int Space1 = 2;
        public const int Space2 = 4;
        public const int Space3 = 6;
        public const int Space4 = 8;
        public const int Space5 = 12;
        public const int Space6 = 16;
        public const int Space7 = 24;
        public const int Space8 = 32;
        public const int PanelPadding = Space5;

        public static RectOffset Uniform(int v) => new RectOffset(v, v, v, v);
        public static RectOffset HV(int h, int v) => new RectOffset(h, h, v, v);
        public static RectOffset LRTB(int l, int r, int t, int b) => new RectOffset(l, r, t, b);
    }

    #endregion

    #region Typography Constants

    public static class Typography {
        public const int SizeXS = 9;
        public const int SizeSM = 10;
        public const int SizeBase = 11;
        public const int SizeMD = 12;
        public const int SizeMDL = 13;      // Medium-Large (between MD and LG)
        public const int SizeLG = 14;
        public const int SizeXL = 16;
        public const int Size2XL = 20;      // Section headers
        public const int Size3XL = 24;      // Panel headers
        public const int Size4XL = 28;      // Welcome/splash headers
    }

    #endregion

    #region Heights Constants

    public static class Heights {
        // Button heights - use these for consistency
        public const int ButtonSmall = 18;      // Tiny buttons (help ?)
        public const int ButtonInline = 20;     // Small inline buttons (clear, copy icon)
        public const int Button = 24;           // Standard buttons (back, export)
        public const int ButtonMedium = 26;     // Medium buttons (clear, copy JSON)
        public const int ButtonAction = 28;     // Action buttons (save, test, apply)
        public const int ButtonLarge = 32;      // Primary action buttons (generate, run diagnostics)
        public const int ButtonHero = 36;       // Hero/CTA buttons (get started)

        // Other element heights
        public const int ProgressBar = 4;       // Thin progress indicators
        public const int ProgressBarThick = 8;  // Thicker progress bars
        public const int SliderTrack = 10;      // Slider track height
        public const int IconSmall = 14;        // Small icons/arrows
        public const int Pill = 16;             // Pills and badges
        public const int Field = 18;            // Input fields
        public const int IconButton = 20;       // Icon-only buttons
        public const int ListItem = 22;         // List item rows
        public const int TabItem = 24;          // Tab heights
        public const int Container = 30;        // Container/row heights
        public const int SidebarItem = 34;      // Sidebar navigation items
        public const int Card = 38;             // Card element heights
        public const int Toolbar = 44;          // Toolbar heights
    }

    #endregion

    #region Styles - Direct GUI.skin Access

    // Buttons
    public static GUIStyle ButtonPrimary => GUI.skin.button;
    public static GUIStyle ButtonSecondary => GUI.skin.button;
    public static GUIStyle ButtonDanger => GUI.skin.button;
    public static GUIStyle ButtonSuccess => GUI.skin.button;
    public static GUIStyle ButtonSmall => GUI.skin.button;
    public static GUIStyle ButtonIcon => GUI.skin.button;

    // Panels
    public static GUIStyle PanelElevated => GUI.skin.box;
    public static GUIStyle PanelRecessed => GUI.skin.box;
    public static GUIStyle Card => GUI.skin.box;
    public static GUIStyle PanelAI => GUI.skin.box;
    public static GUIStyle PanelPreview => GUI.skin.box;

    // Labels
    public static GUIStyle LabelPrimary => GUI.skin.label;
    public static GUIStyle LabelSecondary => GUI.skin.label;
    public static GUIStyle LabelSmall => GUI.skin.label;
    public static GUIStyle LabelHeader => GUI.skin.label;
    public static GUIStyle LabelTitle => GUI.skin.label;
    public static GUIStyle LabelWindowTitle => GUI.skin.label;
    public static GUIStyle LabelTitleAccent => GUI.skin.label;
    public static GUIStyle LabelCentered => GUI.skin.label;
    public static GUIStyle LabelRight => GUI.skin.label;
    public static GUIStyle LabelMono => GUI.skin.label;
    public static GUIStyle LabelSuccess => GUI.skin.label;
    public static GUIStyle LabelWarning => GUI.skin.label;
    public static GUIStyle LabelError => GUI.skin.label;
    public static GUIStyle LabelAI => GUI.skin.label;

    // Input Fields
    public static GUIStyle TextField => GUI.skin.textField;
    public static GUIStyle TextArea => GUI.skin.textArea;
    public static GUIStyle TextAreaMono => GUI.skin.textArea;

    // Lists
    public static GUIStyle ListItem => GUI.skin.label;
    public static GUIStyle ListItemSelected => GUI.skin.label;
    public static GUIStyle TableHeader => GUI.skin.label;

    // Tabs
    public static GUIStyle TabBar => GUI.skin.box;
    public static GUIStyle TabInactive => GUI.skin.button;
    public static GUIStyle TabActive => GUI.skin.button;

    // Sidebar
    public static GUIStyle Sidebar => GUI.skin.box;
    public static GUIStyle SidebarItem => GUI.skin.button;
    public static GUIStyle SidebarItemActive => GUI.skin.button;

    // Pills
    public static GUIStyle PillDefault => GUI.skin.button;
    public static GUIStyle PillSuccess => GUI.skin.button;
    public static GUIStyle PillWarning => GUI.skin.button;
    public static GUIStyle PillError => GUI.skin.button;
    public static GUIStyle PillAI => GUI.skin.button;
    public static GUIStyle PillInfo => GUI.skin.button;

    // Other
    public static GUIStyle FoldoutHeader => EditorStyles.foldout;
    public static GUIStyle Separator => GUI.skin.box;
    public static GUIStyle SeparatorLight => GUI.skin.box;
    public static GUIStyle Toolbar => EditorStyles.toolbar;
    public static GUIStyle ToolbarButton => EditorStyles.toolbarButton;
    public static GUIStyle ToolbarSearch => EditorStyles.toolbarSearchField;
    public static GUIStyle PopupStyle => EditorStyles.popup;

    #endregion

    #region Texture Cache

    private static Dictionary<Color, Texture2D> _textureCache = new Dictionary<Color, Texture2D>();
    private static Dictionary<string, Texture2D> _shapeCache = new Dictionary<string, Texture2D>();

    public static Texture2D GetTexture(Color color) {
        if (_textureCache.TryGetValue(color, out Texture2D cached) && cached != null)
            return cached;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        tex.hideFlags = HideFlags.DontSave;
        _textureCache[color] = tex;
        return tex;
    }

    /// <summary>
    /// Generates or retrieves a cached anti-aliased circle texture.
    /// </summary>
    public static Texture2D GetCircleTexture(int size, Color color) {
        string key = $"circle_{size}_{color.GetHashCode()}";
        if (_shapeCache.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        float center = size * 0.5f;
        float radius = center - 1f; // Leave 1px padding for AA

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = 1f - Mathf.Clamp01(dist - radius);
                colors[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        tex.hideFlags = HideFlags.DontSave;
        _shapeCache[key] = tex;
        return tex;
    }

    /// <summary>
    /// Generates or retrieves a cached rounded rectangle texture (pill shape).
    /// </summary>
    public static Texture2D GetRoundedRectTexture(int width, int height, int radius, Color color) {
        string key = $"roundrect_{width}_{height}_{radius}_{color.GetHashCode()}";
        if (_shapeCache.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] colors = new Color[width * height];
        
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                // Calculate distance to nearest corner or edge
                float alpha = 1f;
                
                // Corners
                if (x < radius && y < radius) // Bottom-Left
                    alpha = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(radius - 0.5f, radius - 0.5f)) - (radius - 0.5f));
                else if (x >= width - radius && y < radius) // Bottom-Right
                    alpha = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 0.5f, radius - 0.5f)) - (radius - 0.5f));
                else if (x < radius && y >= height - radius) // Top-Left
                    alpha = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(radius - 0.5f, height - radius - 0.5f)) - (radius - 0.5f));
                else if (x >= width - radius && y >= height - radius) // Top-Right
                    alpha = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 0.5f, height - radius - 0.5f)) - (radius - 0.5f));
                
                colors[y * width + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        tex.hideFlags = HideFlags.DontSave;
        _shapeCache[key] = tex;
        return tex;
    }

    public static void ClearTextureCache() {
        foreach (var tex in _textureCache.Values)
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        _textureCache.Clear();

        foreach (var tex in _shapeCache.Values)
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        _shapeCache.Clear();
    }

    #endregion

    #region Helper Methods

    public static void DrawCircle(Rect rect, Color color) {
        // Use cached texture for AA circle
        int size = Mathf.CeilToInt(Mathf.Max(rect.width, rect.height));
        // Clamp size to reasonable limits to avoid massive texture generation
        size = Mathf.Clamp(size, 4, 128);
        Texture2D circleTex = GetCircleTexture(size, color);
        GUI.DrawTexture(rect, circleTex, ScaleMode.ScaleToFit, true);
    }

    public static void DrawRoundedRect(Rect rect, Color color, int radius = 4) {
        // Round size to nearest even number to reduce cache misses
        int w = Mathf.CeilToInt(rect.width / 2) * 2;
        int h = Mathf.CeilToInt(rect.height / 2) * 2;
        
        // Clamp to reasonably high limits to prevent texture explosion but allow wide UI
        w = Mathf.Clamp(w, 4, 2048);
        h = Mathf.Clamp(h, 4, 512);
        
        Texture2D tex = GetRoundedRectTexture(w, h, radius, color);
        GUI.DrawTexture(rect, tex);
    }

    public static void DrawSeparator(bool light = false) {
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        Color color = light ? Colors.BorderLight : Colors.BorderDefault;
        EditorGUI.DrawRect(rect, color);
    }

    public static void Space(int amount = -1) {
        GUILayout.Space(amount < 0 ? Spacing.Space4 : amount);
    }

    public static void DrawPill(string text, GUIStyle style = null) {
        GUILayout.Label(text, style ?? GUI.skin.button);
    }

    public static void DrawSectionHeader(string text, bool separator = true) {
        if (separator) {
            Space(Spacing.Space4);
            DrawSeparator(true);
        }
        Space(Spacing.Space2);
        GUILayout.Label(text, EditorStyles.boldLabel);
        Space(Spacing.Space2);
    }

    public static void BeginPanel(GUIStyle style = null) {
        EditorGUILayout.BeginVertical(style ?? GUI.skin.box);
    }

    public static void EndPanel() {
        EditorGUILayout.EndVertical();
    }

    public static void DrawTabUnderline(Rect tabRect) {
        EditorGUI.DrawRect(new Rect(tabRect.x, tabRect.yMax - 2, tabRect.width, 2), Colors.AccentPrimary);
    }

    public static void DrawSidebarAccent(Rect itemRect) {
        EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, 3, itemRect.height), Colors.AccentPrimary);
    }

    public static void DrawAIIndicator(Rect rect) {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), Colors.AiSuggestion);
    }

    // Standard control wrappers
    public static int Popup(int index, string[] options, params GUILayoutOption[] opts) =>
        EditorGUILayout.Popup(index, options, opts);

    public static int Popup(string label, int index, string[] options, params GUILayoutOption[] opts) =>
        EditorGUILayout.Popup(label, index, options, opts);

    public static int IntSlider(int value, int min, int max, params GUILayoutOption[] opts) =>
        EditorGUILayout.IntSlider(value, min, max, opts);

    public static int IntSlider(string label, int value, int min, int max, params GUILayoutOption[] opts) =>
        EditorGUILayout.IntSlider(label, value, min, max, opts);

    public static T ObjectField<T>(T obj, bool allowScene, params GUILayoutOption[] opts) where T : UnityEngine.Object =>
        (T)EditorGUILayout.ObjectField(obj, typeof(T), allowScene, opts);

    public static T ObjectField<T>(string label, T obj, bool allowScene, params GUILayoutOption[] opts) where T : UnityEngine.Object =>
        (T)EditorGUILayout.ObjectField(label, obj, typeof(T), allowScene, opts);

    public static string StyledTextField(string value, params GUILayoutOption[] opts) =>
        EditorGUILayout.TextField(value, opts);

    public static string StyledTextField(string label, string value, params GUILayoutOption[] opts) =>
        EditorGUILayout.TextField(label, value, opts);

    public static string StyledTextArea(string value, params GUILayoutOption[] opts) =>
        EditorGUILayout.TextArea(value, opts);

    #endregion

    #region Initialization

    [InitializeOnLoadMethod]
    private static void Initialize() {
        // Unsubscribe first to prevent accumulation on assembly reload
        EditorApplication.quitting -= ClearTextureCache;
        AssemblyReloadEvents.beforeAssemblyReload -= ClearTextureCache;
        EditorApplication.quitting += ClearTextureCache;
        AssemblyReloadEvents.beforeAssemblyReload += ClearTextureCache;
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
