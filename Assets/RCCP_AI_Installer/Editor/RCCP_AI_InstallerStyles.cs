//----------------------------------------------
//        RCCP AI Setup Assistant - Installer
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RCCP_AI_Installer
{
    /// <summary>
    /// Minimal standalone styling for the installer UI.
    /// Does not depend on the main RCCP AI Assistant package.
    /// </summary>
    public static class RCCP_AI_InstallerStyles
    {
        #region Colors

        // Semantic colors
        public static Color AccentColor => new Color32(90, 155, 213, 255);
        public static Color SuccessColor => new Color32(106, 153, 85, 255);
        public static Color WarningColor => new Color32(204, 167, 86, 255);
        public static Color ErrorColor => new Color32(199, 84, 80, 255);
        public static Color InfoColor => new Color32(86, 156, 214, 255);

        // Theme-aware colors
        public static Color BgBase => EditorGUIUtility.isProSkin
            ? new Color32(56, 56, 56, 255)
            : new Color32(194, 194, 194, 255);

        public static Color BgElevated => EditorGUIUtility.isProSkin
            ? new Color32(62, 62, 62, 255)
            : new Color32(210, 210, 210, 255);

        public static Color BgRecessed => EditorGUIUtility.isProSkin
            ? new Color32(45, 45, 45, 255)
            : new Color32(180, 180, 180, 255);

        public static Color TextPrimary => EditorGUIUtility.isProSkin
            ? new Color32(212, 212, 212, 255)
            : new Color32(32, 32, 32, 255);

        public static Color TextSecondary => EditorGUIUtility.isProSkin
            ? new Color32(154, 154, 154, 255)
            : new Color32(96, 96, 96, 255);

        public static Color BorderColor => EditorGUIUtility.isProSkin
            ? new Color32(35, 35, 35, 255)
            : new Color32(140, 140, 140, 255);

        public static Color WithAlpha(Color color, float alpha) =>
            new Color(color.r, color.g, color.b, alpha);

        #endregion

        #region Cached Styles

        private static GUIStyle _headerStyle;
        private static GUIStyle _subheaderStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _bodyCenteredStyle;
        private static GUIStyle _stepIndicatorStyle;
        private static GUIStyle _stepIndicatorActiveStyle;
        private static GUIStyle _stepIndicatorCompleteStyle;
        private static GUIStyle _buttonPrimaryStyle;
        private static GUIStyle _buttonSecondaryStyle;
        private static GUIStyle _panelStyle;
        private static GUIStyle _statusSuccessStyle;
        private static GUIStyle _statusErrorStyle;
        private static GUIStyle _statusWarningStyle;
        private static GUIStyle _linkStyle;

        public static GUIStyle HeaderStyle {
            get {
                if (_headerStyle == null) {
                    _headerStyle = new GUIStyle(EditorStyles.boldLabel) {
                        fontSize = 18,
                        alignment = TextAnchor.MiddleCenter,
                        margin = new RectOffset(0, 0, 10, 10)
                    };
                    _headerStyle.normal.textColor = TextPrimary;
                }
                return _headerStyle;
            }
        }

        public static GUIStyle SubheaderStyle {
            get {
                if (_subheaderStyle == null) {
                    _subheaderStyle = new GUIStyle(EditorStyles.boldLabel) {
                        fontSize = 14,
                        alignment = TextAnchor.MiddleLeft,
                        margin = new RectOffset(0, 0, 8, 4)
                    };
                    _subheaderStyle.normal.textColor = TextPrimary;
                }
                return _subheaderStyle;
            }
        }

        public static GUIStyle BodyStyle {
            get {
                if (_bodyStyle == null) {
                    _bodyStyle = new GUIStyle(EditorStyles.label) {
                        fontSize = 12,
                        wordWrap = true,
                        alignment = TextAnchor.UpperLeft,
                        margin = new RectOffset(0, 0, 4, 4)
                    };
                    _bodyStyle.normal.textColor = TextSecondary;
                }
                return _bodyStyle;
            }
        }

        public static GUIStyle BodyCenteredStyle {
            get {
                if (_bodyCenteredStyle == null) {
                    _bodyCenteredStyle = new GUIStyle(BodyStyle) {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return _bodyCenteredStyle;
            }
        }

        public static GUIStyle StepIndicatorStyle {
            get {
                if (_stepIndicatorStyle == null) {
                    _stepIndicatorStyle = new GUIStyle(EditorStyles.miniLabel) {
                        fontSize = 10,
                        alignment = TextAnchor.MiddleCenter,
                        fixedWidth = 22,
                        fixedHeight = 22
                    };
                    _stepIndicatorStyle.normal.textColor = TextSecondary;
                }
                return _stepIndicatorStyle;
            }
        }

        public static GUIStyle StepIndicatorActiveStyle {
            get {
                if (_stepIndicatorActiveStyle == null) {
                    _stepIndicatorActiveStyle = new GUIStyle(StepIndicatorStyle);
                    _stepIndicatorActiveStyle.normal.textColor = AccentColor;
                }
                return _stepIndicatorActiveStyle;
            }
        }

        public static GUIStyle StepIndicatorCompleteStyle {
            get {
                if (_stepIndicatorCompleteStyle == null) {
                    _stepIndicatorCompleteStyle = new GUIStyle(StepIndicatorStyle);
                    _stepIndicatorCompleteStyle.normal.textColor = SuccessColor;
                }
                return _stepIndicatorCompleteStyle;
            }
        }

        public static GUIStyle ButtonPrimaryStyle {
            get {
                if (_buttonPrimaryStyle == null) {
                    _buttonPrimaryStyle = new GUIStyle(GUI.skin.button) {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        fixedHeight = 30,
                        padding = new RectOffset(20, 20, 6, 6)
                    };
                }
                return _buttonPrimaryStyle;
            }
        }

        public static GUIStyle ButtonSecondaryStyle {
            get {
                if (_buttonSecondaryStyle == null) {
                    _buttonSecondaryStyle = new GUIStyle(GUI.skin.button) {
                        fontSize = 11,
                        fixedHeight = 24,
                        padding = new RectOffset(12, 12, 4, 4)
                    };
                }
                return _buttonSecondaryStyle;
            }
        }

        public static GUIStyle PanelStyle {
            get {
                if (_panelStyle == null) {
                    _panelStyle = new GUIStyle(EditorStyles.helpBox) {
                        padding = new RectOffset(12, 12, 10, 10),
                        margin = new RectOffset(0, 0, 4, 4)
                    };
                }
                return _panelStyle;
            }
        }

        public static GUIStyle StatusSuccessStyle {
            get {
                if (_statusSuccessStyle == null) {
                    _statusSuccessStyle = new GUIStyle(EditorStyles.label) {
                        fontSize = 11
                    };
                    _statusSuccessStyle.normal.textColor = SuccessColor;
                }
                return _statusSuccessStyle;
            }
        }

        public static GUIStyle StatusErrorStyle {
            get {
                if (_statusErrorStyle == null) {
                    _statusErrorStyle = new GUIStyle(EditorStyles.label) {
                        fontSize = 11
                    };
                    _statusErrorStyle.normal.textColor = ErrorColor;
                }
                return _statusErrorStyle;
            }
        }

        public static GUIStyle StatusWarningStyle {
            get {
                if (_statusWarningStyle == null) {
                    _statusWarningStyle = new GUIStyle(EditorStyles.label) {
                        fontSize = 11
                    };
                    _statusWarningStyle.normal.textColor = WarningColor;
                }
                return _statusWarningStyle;
            }
        }

        public static GUIStyle LinkStyle {
            get {
                if (_linkStyle == null) {
                    _linkStyle = new GUIStyle(EditorStyles.label) {
                        fontSize = 11
                    };
                    _linkStyle.normal.textColor = AccentColor;
                    _linkStyle.hover.textColor = WithAlpha(AccentColor, 0.8f);
                }
                return _linkStyle;
            }
        }

        #endregion

        #region Texture Cache

        private static Dictionary<Color, Texture2D> _textureCache = new Dictionary<Color, Texture2D>();

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

        public static void ClearCache() {
            foreach (var tex in _textureCache.Values)
                if (tex != null) Object.DestroyImmediate(tex);
            _textureCache.Clear();

            // Clear cached styles
            _headerStyle = null;
            _subheaderStyle = null;
            _bodyStyle = null;
            _bodyCenteredStyle = null;
            _stepIndicatorStyle = null;
            _stepIndicatorActiveStyle = null;
            _stepIndicatorCompleteStyle = null;
            _buttonPrimaryStyle = null;
            _buttonSecondaryStyle = null;
            _panelStyle = null;
            _statusSuccessStyle = null;
            _statusErrorStyle = null;
            _statusWarningStyle = null;
            _linkStyle = null;
        }

        [InitializeOnLoadMethod]
        private static void Initialize() {
            EditorApplication.quitting += ClearCache;
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Draws a status line with icon and text.
        /// </summary>
        public static void DrawStatusLine(string label, string status, bool isOk, bool isWarning = false) {
            EditorGUILayout.BeginHorizontal();

            // Icon
            GUIContent icon;
            if (isOk) {
                icon = EditorGUIUtility.IconContent("TestPassed");
            } else if (isWarning) {
                icon = EditorGUIUtility.IconContent("console.warnicon.sml");
            } else {
                icon = EditorGUIUtility.IconContent("TestFailed");
            }

            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

            // Label
            EditorGUILayout.LabelField(label, GUILayout.Width(200));

            // Status
            GUIStyle statusStyle = isOk ? StatusSuccessStyle : (isWarning ? StatusWarningStyle : StatusErrorStyle);
            EditorGUILayout.LabelField(status, statusStyle);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws the step indicator circles at the top of the wizard.
        /// </summary>
        public static void DrawStepIndicator(int currentStep, int totalSteps, string[] stepNames) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int i = 0; i < totalSteps; i++) {
                bool isComplete = i < currentStep;
                bool isCurrent = i == currentStep;

                // Step circle
                EditorGUILayout.BeginVertical(GUILayout.Width(80));

                // Circle/Checkmark
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (isComplete) {
                    // Checkmark
                    GUI.color = SuccessColor;
                    GUILayout.Label(EditorGUIUtility.IconContent("TestPassed"), GUILayout.Width(22), GUILayout.Height(22));
                    GUI.color = Color.white;
                } else {
                    // Number in circle
                    Rect circleRect = GUILayoutUtility.GetRect(22, 22);

                    // Draw circle background
                    Color circleColor = isCurrent ? AccentColor : WithAlpha(TextSecondary, 0.3f);
                    EditorGUI.DrawRect(new Rect(circleRect.x, circleRect.y, 22, 22), circleColor);

                    // Draw number - white text on colored background for current, gray for inactive
                    GUIStyle numberStyle = new GUIStyle(EditorStyles.miniLabel) {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };
                    numberStyle.normal.textColor = isCurrent ? Color.white : TextSecondary;
                    GUI.Label(circleRect, (i + 1).ToString(), numberStyle);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // Step name
                GUIStyle nameStyle = new GUIStyle(EditorStyles.miniLabel) {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9
                };
                nameStyle.normal.textColor = isCurrent ? AccentColor : (isComplete ? SuccessColor : TextSecondary);

                string displayName = i < stepNames.Length ? stepNames[i] : $"Step {i + 1}";
                GUILayout.Label(displayName, nameStyle);

                EditorGUILayout.EndVertical();

                // Arrow between steps
                if (i < totalSteps - 1) {
                    GUILayout.Label("→", StepIndicatorStyle, GUILayout.Width(20));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a horizontal separator line.
        /// </summary>
        public static void DrawSeparator() {
            GUILayout.Space(8);
            Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, BorderColor);
            GUILayout.Space(8);
        }

        /// <summary>
        /// Draws a feature item with icon and description.
        /// </summary>
        public static void DrawFeatureItem(string icon, string title, string description) {
            EditorGUILayout.BeginHorizontal(PanelStyle);

            // Icon placeholder (use Unity built-in or custom)
            GUILayout.Label(icon, GUILayout.Width(24), GUILayout.Height(24));

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, BodyStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a clickable link.
        /// </summary>
        public static bool DrawLink(string text) {
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), LinkStyle);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            bool clicked = GUI.Button(rect, text, LinkStyle);
            return clicked;
        }

        /// <summary>
        /// Draws a centered button.
        /// </summary>
        public static bool DrawCenteredButton(string text, float width = 200f) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool clicked = GUILayout.Button(text, ButtonPrimaryStyle, GUILayout.Width(width));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return clicked;
        }

        /// <summary>
        /// Draws an info box with custom styling.
        /// </summary>
        public static void DrawInfoBox(string message, MessageType type = MessageType.Info) {
            EditorGUILayout.HelpBox(message, type);
        }

        #endregion
    }
}
#endif
