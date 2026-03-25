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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Enhanced custom editor for RCCP_AIPromptAsset with statistics, validation,
/// templates, search/replace, import/export, section navigation, and comparison.
/// </summary>
[CustomEditor(typeof(RCCP_AIPromptAsset))]
public class RCCP_AIPromptAssetEditor : Editor {

    #region Serialized Properties
    private SerializedProperty panelName;
    private SerializedProperty panelIcon;
    private SerializedProperty panelDescription;
    private SerializedProperty systemPrompt;
    private SerializedProperty examplePrompts;
    private SerializedProperty placeholderText;
    private SerializedProperty panelType;
    private SerializedProperty requiresVehicle;
    private SerializedProperty requiresRCCPController;
    private SerializedProperty includeMeshAnalysis;
    private SerializedProperty includeCurrentState;
    #endregion

    #region Editor State
    private enum EditorMode { Edit, Preview, Compare }
    private EditorMode currentMode = EditorMode.Edit;

    // Foldout states
    private bool showPanelInfo = true;
    private bool showAIConfig = true;
    private bool showExamples = true;
    private bool showSettings = true;
    private bool showStatistics = true;
    private bool showValidation = false;
    private bool showSearchReplace = false;
    private bool showTemplates = false;

    // Search/Replace state
    private string searchText = "";
    private string replaceText = "";
    private bool searchCaseSensitive = false;
    private int searchMatchCount = 0;

    // Section navigation
    private List<string> sections = new List<string>();
    private int selectedSectionIndex = 0;

    // Comparison
    private RCCP_AIPromptAsset compareTarget = null;
    private Vector2 compareScrollLeft = Vector2.zero;
    private Vector2 compareScrollRight = Vector2.zero;

    // Scroll position for prompt text
    private Vector2 promptScrollPosition = Vector2.zero;

    // Cached statistics
    private PromptStatistics cachedStats = new PromptStatistics();
    private string lastPromptHash = "";

    // Cached validation
    private List<ValidationIssue> validationIssues = new List<ValidationIssue>();
    #endregion

    #region Styles
    private GUIStyle previewStyle;
    private GUIStyle diffAddedStyle;
    private GUIStyle diffRemovedStyle;
    private bool stylesInitialized = false;

    // Toolbar options
    private readonly string[] modeLabels = { "Edit", "Preview", "Compare" };

    // Custom GUISkin support
    private GUISkin customSkin;
    private GUISkin originalSkin;
    private bool skinLoaded = false;
    #endregion

    #region Initialization
    private void OnEnable() {
        panelName = serializedObject.FindProperty("panelName");
        panelIcon = serializedObject.FindProperty("panelIcon");
        panelDescription = serializedObject.FindProperty("panelDescription");
        systemPrompt = serializedObject.FindProperty("systemPrompt");
        examplePrompts = serializedObject.FindProperty("examplePrompts");
        placeholderText = serializedObject.FindProperty("placeholderText");
        panelType = serializedObject.FindProperty("panelType");
        requiresVehicle = serializedObject.FindProperty("requiresVehicle");
        requiresRCCPController = serializedObject.FindProperty("requiresRCCPController");
        includeMeshAnalysis = serializedObject.FindProperty("includeMeshAnalysis");
        includeCurrentState = serializedObject.FindProperty("includeCurrentState");

        // Load saved foldout states
        LoadFoldoutStates();

        // Load custom GUISkin
        LoadCustomSkin();

        // Initial stats calculation
        UpdateStatistics();
        UpdateSections();
    }

    private void LoadCustomSkin() {
        if (skinLoaded) return;

        // Try to load from RCCP_AISettings first
        var settings = Resources.Load<RCCP_AISettings>("RCCP_AISettings");
        if (settings != null && settings.customSkin != null) {
            customSkin = settings.customSkin;
        } else {
            // Fallback: load directly from Resources
            customSkin = Resources.Load<GUISkin>("RCCP_AI_Guiskin");
        }

        skinLoaded = true;
    }

    private void OnDisable() {
        stylesInitialized = false;
        skinLoaded = false;
        customSkin = null;
        originalSkin = null;
    }

    private void InitStyles() {
        if (stylesInitialized) return;

        previewStyle = new GUIStyle(EditorStyles.wordWrappedLabel) {
            padding = new RectOffset(8, 8, 8, 8),
            richText = false
        };

        // Create cached textures for diff styles using Design System
        Texture2D diffAddedBg = RCCP_AIDesignSystem.GetTexture(RCCP_AIDesignSystem.Colors.DiffAddedBg);
        Texture2D diffRemovedBg = RCCP_AIDesignSystem.GetTexture(RCCP_AIDesignSystem.Colors.DiffRemovedBg);

        diffAddedStyle = new GUIStyle(EditorStyles.label) {
            normal = {
                textColor = RCCP_AIDesignSystem.Colors.DiffAddedText,
                background = diffAddedBg
            },
            wordWrap = true,
            richText = false
        };

        diffRemovedStyle = new GUIStyle(EditorStyles.label) {
            normal = {
                textColor = RCCP_AIDesignSystem.Colors.DiffRemovedText,
                background = diffRemovedBg
            },
            wordWrap = true,
            richText = false
        };

        stylesInitialized = true;
    }
    #endregion

    #region Main GUI
    public override bool RequiresConstantRepaint() {
        return RCCP_AIEditorPrefs.ForceRepaint || base.RequiresConstantRepaint();
    }

    public override void OnInspectorGUI() {
        // Apply custom skin
        ApplyCustomSkin();

        InitStyles();
        serializedObject.Update();

        // Mode Toolbar
        DrawModeToolbar();

        RCCP_AIDesignSystem.Space(5);

        switch (currentMode) {
            case EditorMode.Edit:
                DrawEditMode();
                break;
            case EditorMode.Preview:
                DrawPreviewMode();
                break;
            case EditorMode.Compare:
                DrawCompareMode();
                break;
        }

        serializedObject.ApplyModifiedProperties();

        // Check for changes
        string currentHash = systemPrompt.stringValue?.GetHashCode().ToString() ?? "";
        if (currentHash != lastPromptHash) {
            lastPromptHash = currentHash;
            UpdateStatistics();
            UpdateSections();
            RunValidation();
        }

        // Restore original skin
        RestoreOriginalSkin();
    }

    private void ApplyCustomSkin() {
        if (customSkin != null) {
            originalSkin = GUI.skin;
            GUI.skin = customSkin;
        }
    }

    private void RestoreOriginalSkin() {
        if (originalSkin != null) {
            GUI.skin = originalSkin;
        }
    }

    private void DrawModeToolbar() {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Mode selection using proper toolbar
        int newMode = GUILayout.Toolbar((int)currentMode, modeLabels, EditorStyles.toolbarButton, GUILayout.Width(200));
        if (newMode != (int)currentMode) {
            currentMode = (EditorMode)newMode;
        }

        GUILayout.FlexibleSpace();

        // Quick actions
        if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(50))) {
            ExportPrompt();
        }
        if (GUILayout.Button("Import", EditorStyles.toolbarButton, GUILayout.Width(50))) {
            ImportPrompt();
        }

        EditorGUILayout.EndHorizontal();
    }
    #endregion

    #region Edit Mode
    private void DrawEditMode() {
        // Statistics Panel
        DrawStatisticsPanel();

        // Validation Panel
        DrawValidationPanel();

        // Search & Replace Panel
        DrawSearchReplacePanel();

        // Templates Panel
        DrawTemplatesPanel();

        RCCP_AIDesignSystem.Space(5);

        // Panel Info Section
        showPanelInfo = EditorGUILayout.Foldout(showPanelInfo, "Panel Info", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showPanelInfo) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);
            EditorGUI.indentLevel++;
            RCCP_AIDesignSystem.Space(2);
            EditorGUILayout.PropertyField(panelName);
            EditorGUILayout.PropertyField(panelIcon);
            EditorGUILayout.PropertyField(panelDescription);
            RCCP_AIDesignSystem.Space(2);
            EditorGUI.indentLevel--;
            RCCP_AIDesignSystem.EndPanel();
        }

        RCCP_AIDesignSystem.Space(5);

        // AI Configuration Section
        showAIConfig = EditorGUILayout.Foldout(showAIConfig, "AI Configuration", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showAIConfig) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);
            EditorGUI.indentLevel++;
            RCCP_AIDesignSystem.Space(2);

            // Section Navigation
            DrawSectionNavigation();

            // System Prompt
            EditorGUILayout.LabelField("System Prompt", RCCP_AIDesignSystem.LabelHeader);

            promptScrollPosition = EditorGUILayout.BeginScrollView(promptScrollPosition, GUILayout.Height(300));
            EditorGUILayout.PropertyField(systemPrompt, GUIContent.none);
            EditorGUILayout.EndScrollView();

            // Disclaimer
            GUIStyle disclaimerStyle = new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = RCCP_AIDesignSystem.Colors.GetTextSecondary() },
                fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
                wordWrap = true
            };
            GUILayout.Label("AI can make mistakes. Always review before applying.", disclaimerStyle);

            RCCP_AIDesignSystem.Space(2);
            EditorGUI.indentLevel--;
            RCCP_AIDesignSystem.EndPanel();
        }

        RCCP_AIDesignSystem.Space(5);

        // Example Prompts Section
        showExamples = EditorGUILayout.Foldout(showExamples, "Example Prompts", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showExamples) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);
            EditorGUI.indentLevel++;
            RCCP_AIDesignSystem.Space(2);
            EditorGUILayout.PropertyField(examplePrompts, true);
            EditorGUILayout.PropertyField(placeholderText);
            RCCP_AIDesignSystem.Space(2);
            EditorGUI.indentLevel--;
            RCCP_AIDesignSystem.EndPanel();
        }

        RCCP_AIDesignSystem.Space(5);

        // Panel Settings Section
        showSettings = EditorGUILayout.Foldout(showSettings, "Panel Settings", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showSettings) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);
            EditorGUI.indentLevel++;
            RCCP_AIDesignSystem.Space(2);
            EditorGUILayout.PropertyField(panelType);
            EditorGUILayout.PropertyField(requiresVehicle);
            EditorGUILayout.PropertyField(requiresRCCPController);
            EditorGUILayout.PropertyField(includeMeshAnalysis);
            EditorGUILayout.PropertyField(includeCurrentState);
            RCCP_AIDesignSystem.Space(2);
            EditorGUI.indentLevel--;
            RCCP_AIDesignSystem.EndPanel();
        }

        SaveFoldoutStates();
    }
    #endregion

    #region Statistics Panel
    private void DrawStatisticsPanel() {
        showStatistics = EditorGUILayout.Foldout(showStatistics, "Statistics", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showStatistics) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Characters: {cachedStats.CharCount:N0}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"Words: {cachedStats.WordCount:N0}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"Lines: {cachedStats.LineCount:N0}", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Est. Tokens: {cachedStats.TokenEstimate:N0}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"Sections: {cachedStats.SectionCount}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"JSON Blocks: {cachedStats.JsonBlockCount}", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Token warning
            if (cachedStats.TokenEstimate > 8000) {
                RCCP_AIDesignSystem.Space(3);
                GUIStyle warningStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary);
                warningStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Warning;
                warningStyle.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField("Warning: Prompt exceeds 8000 tokens - may be too long", warningStyle);
            } else if (cachedStats.TokenEstimate > 6000) {
                RCCP_AIDesignSystem.Space(3);
                EditorGUILayout.LabelField("Note: Prompt is getting long (>6000 tokens)", EditorStyles.miniLabel);
            }

            RCCP_AIDesignSystem.EndPanel();
        }
    }

    private void UpdateStatistics() {
        string prompt = systemPrompt?.stringValue ?? "";
        cachedStats.CharCount = prompt.Length;
        cachedStats.WordCount = string.IsNullOrWhiteSpace(prompt) ? 0 :
            Regex.Matches(prompt, @"\b\w+\b").Count;
        cachedStats.LineCount = string.IsNullOrWhiteSpace(prompt) ? 0 :
            prompt.Split('\n').Length;
        cachedStats.TokenEstimate = prompt.Length / 4;
        cachedStats.SectionCount = Regex.Matches(prompt, @"===\.*===").Count;
        cachedStats.JsonBlockCount = Regex.Matches(prompt, @"\{[\s\S]*?\}").Count;
    }
    #endregion

    #region Validation Panel
    private void DrawValidationPanel() {
        showValidation = EditorGUILayout.Foldout(showValidation,
            $"Validation ({validationIssues.Count} issues)", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showValidation) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);

            if (GUILayout.Button("Run Validation", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
                RunValidation();
            }

            RCCP_AIDesignSystem.Space(5);

            if (validationIssues.Count == 0) {
                GUIStyle successStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary);
                successStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Success;
                successStyle.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField("No issues found", successStyle);
            } else {
                foreach (var issue in validationIssues) {
                    EditorGUILayout.BeginHorizontal();

                    GUIStyle style = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary);
                    style.normal.textColor = issue.Severity == ValidationSeverity.Error ? 
                        RCCP_AIDesignSystem.Colors.Error : RCCP_AIDesignSystem.Colors.Warning;
                    style.fontStyle = FontStyle.Bold;

                    string icon = issue.Severity == ValidationSeverity.Error ? "X" : "!";

                    EditorGUILayout.LabelField($"[{icon}] {issue.Message}", style);

                    if (!string.IsNullOrEmpty(issue.QuickFix) && GUILayout.Button("Fix", RCCP_AIDesignSystem.ButtonSmall, GUILayout.Width(40))) {
                        ApplyQuickFix(issue);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            RCCP_AIDesignSystem.EndPanel();
        }
    }

    private void RunValidation() {
        validationIssues.Clear();
        string prompt = systemPrompt?.stringValue ?? "";

        // Check for markdown headers
        if (Regex.IsMatch(prompt, @"^#+\s", RegexOptions.Multiline)) {
            validationIssues.Add(new ValidationIssue {
                Message = "Contains markdown headers (#). Use === SECTION === instead.",
                Severity = ValidationSeverity.Warning,
                QuickFix = "remove_headers"
            });
        }

        // Check for bold/italic markdown
        if (Regex.IsMatch(prompt, @"\*\*[^*]+\*\*|\*[^*]+\*")) {
            validationIssues.Add(new ValidationIssue {
                Message = "Contains markdown bold/italic (*). Use CAPS for emphasis.",
                Severity = ValidationSeverity.Warning,
                QuickFix = "remove_markdown_emphasis"
            });
        }

        // Check for code blocks
        if (prompt.Contains("```")) {
            validationIssues.Add(new ValidationIssue {
                Message = "Contains markdown code blocks (```). Use plain JSON examples.",
                Severity = ValidationSeverity.Warning,
                QuickFix = "remove_code_blocks"
            });
        }

        // Check for JSON-only requirement
        if (!prompt.Contains("JSON") || !Regex.IsMatch(prompt, @"(ONLY|only).*(JSON|json)", RegexOptions.IgnoreCase)) {
            validationIssues.Add(new ValidationIssue {
                Message = "Missing 'RESPOND WITH ONLY JSON' requirement.",
                Severity = ValidationSeverity.Error
            });
        }

        // Check for rejection handling (for non-Diagnostics)
        RCCP_AIPromptAsset asset = (RCCP_AIPromptAsset)target;
        if (asset.panelType != RCCP_AIPromptAsset.PanelType.Diagnostics) {
            if (!prompt.Contains("rejected") && !prompt.Contains("REJECT")) {
                validationIssues.Add(new ValidationIssue {
                    Message = "Missing input validation / rejection handling.",
                    Severity = ValidationSeverity.Warning
                });
            }
        }

        // Check for valid JSON structure in examples
        var jsonMatches = Regex.Matches(prompt, @"\{[^{}]*\}");
        foreach (Match match in jsonMatches) {
            string json = match.Value;
            // Basic structure check - JSON should have quotes and colons
            bool hasQuotes = json.Contains("\"");
            bool hasColons = json.Contains(":");
            bool hasBothBraces = json.StartsWith("{") && json.EndsWith("}");

            if (hasBothBraces && (!hasQuotes || !hasColons) && json.Length > 2) {
                // Looks like malformed JSON
                string preview = json.Length > 30 ? json.Substring(0, 30) + "..." : json;
                validationIssues.Add(new ValidationIssue {
                    Message = $"Potentially malformed JSON: {preview}",
                    Severity = ValidationSeverity.Warning
                });
            }
        }

        // Check prompt length
        if (prompt.Length < 200 && asset.panelType != RCCP_AIPromptAsset.PanelType.Diagnostics) {
            validationIssues.Add(new ValidationIssue {
                Message = "Prompt seems too short. Add more context and examples.",
                Severity = ValidationSeverity.Warning
            });
        }
    }

    private void ApplyQuickFix(ValidationIssue issue) {
        string prompt = systemPrompt.stringValue;

        switch (issue.QuickFix) {
            case "remove_headers":
                prompt = Regex.Replace(prompt, @"^#+\s*(.+)$", "=== $1 ===", RegexOptions.Multiline);
                break;
            case "remove_markdown_emphasis":
                prompt = Regex.Replace(prompt, @"\*\*([^*]+)\*\*", m => m.Groups[1].Value.ToUpper());
                prompt = Regex.Replace(prompt, @"\*([^*]+)\*", m => m.Groups[1].Value.ToUpper());
                break;
            case "remove_code_blocks":
                prompt = Regex.Replace(prompt, @"```\w*\n?", "");
                break;
        }

        Undo.RecordObject(target, "Apply Quick Fix");
        systemPrompt.stringValue = prompt;
        serializedObject.ApplyModifiedProperties();
        RunValidation();
    }
    #endregion

    #region Search & Replace Panel
    private void DrawSearchReplacePanel() {
        showSearchReplace = EditorGUILayout.Foldout(showSearchReplace, "Search & Replace", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showSearchReplace) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);

            // Search field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Find:", GUILayout.Width(50));
            string newSearchText = EditorGUILayout.TextField(searchText);
            if (newSearchText != searchText) {
                searchText = newSearchText;
                UpdateSearchCount();
            }
            bool newCaseSensitive = GUILayout.Toggle(searchCaseSensitive, "Aa", GUILayout.Width(30));
            if (newCaseSensitive != searchCaseSensitive) {
                searchCaseSensitive = newCaseSensitive;
                UpdateSearchCount();
            }
            EditorGUILayout.EndHorizontal();

            // Match count
            if (!string.IsNullOrEmpty(searchText)) {
                EditorGUILayout.LabelField($"Found: {searchMatchCount} matches", EditorStyles.miniLabel);
            }

            // Replace field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Replace:", GUILayout.Width(50));
            replaceText = EditorGUILayout.TextField(replaceText);
            EditorGUILayout.EndHorizontal();

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !string.IsNullOrEmpty(searchText) && searchMatchCount > 0;
            if (GUILayout.Button("Replace Next", RCCP_AIDesignSystem.ButtonSecondary)) {
                ReplaceNext();
            }
            if (GUILayout.Button("Replace All", RCCP_AIDesignSystem.ButtonSecondary)) {
                ReplaceAll();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.EndPanel();
        }
    }

    private void UpdateSearchCount() {
        if (string.IsNullOrEmpty(searchText)) {
            searchMatchCount = 0;
            return;
        }

        string prompt = systemPrompt?.stringValue ?? "";
        var options = searchCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        searchMatchCount = Regex.Matches(prompt, Regex.Escape(searchText), options).Count;
    }

    private void ReplaceNext() {
        string prompt = systemPrompt.stringValue;
        var options = searchCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        var match = Regex.Match(prompt, Regex.Escape(searchText), options);
        if (match.Success) {
            Undo.RecordObject(target, "Replace Text");
            systemPrompt.stringValue = prompt.Substring(0, match.Index) + replaceText +
                prompt.Substring(match.Index + match.Length);
            serializedObject.ApplyModifiedProperties();
        }

        UpdateSearchCount();
    }

    private void ReplaceAll() {
        string prompt = systemPrompt.stringValue;
        var options = searchCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        Undo.RecordObject(target, "Replace All");
        systemPrompt.stringValue = Regex.Replace(prompt, Regex.Escape(searchText), replaceText, options);
        serializedObject.ApplyModifiedProperties();
        UpdateSearchCount();
    }
    #endregion

    #region Templates Panel
    private void DrawTemplatesPanel() {
        showTemplates = EditorGUILayout.Foldout(showTemplates, "Quick Insert Templates", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showTemplates) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Input Validation", RCCP_AIDesignSystem.ButtonSecondary)) {
                InsertTemplate(PromptTemplates.InputValidation);
            }
            if (GUILayout.Button("JSON Response Format", RCCP_AIDesignSystem.ButtonSecondary)) {
                InsertTemplate(PromptTemplates.JsonFormat);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rejection Response", RCCP_AIDesignSystem.ButtonSecondary)) {
                InsertTemplate(PromptTemplates.RejectionResponse);
            }
            if (GUILayout.Button("Accept/Reject Examples", RCCP_AIDesignSystem.ButtonSecondary)) {
                InsertTemplate(PromptTemplates.AcceptRejectExamples);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Section Header", RCCP_AIDesignSystem.ButtonSecondary)) {
                InsertTemplate(PromptTemplates.SectionHeader);
            }
            if (GUILayout.Button("Per-Axle Override", RCCP_AIDesignSystem.ButtonSecondary)) {
                InsertTemplate(PromptTemplates.PerAxleOverride);
            }
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.EndPanel();
        }
    }

    private void InsertTemplate(string template) {
        Undo.RecordObject(target, "Insert Template");
        systemPrompt.stringValue += "\n\n" + template;
        serializedObject.ApplyModifiedProperties();
    }
    #endregion

    #region Section Navigation
    private void DrawSectionNavigation() {
        if (sections.Count > 0) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Jump to Section:", GUILayout.Width(100));

            int newIndex = EditorGUILayout.Popup(selectedSectionIndex, sections.ToArray());
            if (newIndex != selectedSectionIndex && newIndex >= 0 && newIndex < sections.Count) {
                selectedSectionIndex = newIndex;
                // Focus on section (would need more complex implementation for actual scrolling)
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void UpdateSections() {
        sections.Clear();
        string prompt = systemPrompt?.stringValue ?? "";

        var matches = Regex.Matches(prompt, @"===\s*([^=]+?)\s*===");
        foreach (Match match in matches) {
            sections.Add(match.Groups[1].Value.Trim());
        }

        if (selectedSectionIndex >= sections.Count) {
            selectedSectionIndex = 0;
        }
    }
    #endregion

    #region Preview Mode
    private void DrawPreviewMode() {
        EditorGUILayout.LabelField("Preview Mode", RCCP_AIDesignSystem.LabelHeader);
        EditorGUILayout.HelpBox("Read-only view of the prompt with formatted sections.", MessageType.Info);

        string prompt = systemPrompt?.stringValue ?? "";

        promptScrollPosition = EditorGUILayout.BeginScrollView(promptScrollPosition, GUILayout.ExpandHeight(true));

        // Parse and display sections
        var sectionMatches = Regex.Split(prompt, @"(===\s*[^=]+?\s*===)");

        foreach (string part in sectionMatches) {
            if (Regex.IsMatch(part, @"===\s*[^=]+?\s*===")) {
                // Section header
                RCCP_AIDesignSystem.Space(10);
                RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelRecessed);
                EditorGUILayout.LabelField(part.Trim(), RCCP_AIDesignSystem.LabelHeader);
                RCCP_AIDesignSystem.EndPanel();
                RCCP_AIDesignSystem.Space(5);
            } else if (!string.IsNullOrWhiteSpace(part)) {
                // Content - use SelectableLabel for better handling of long text
                string trimmed = part.Trim();
                float height = previewStyle.CalcHeight(new GUIContent(trimmed), EditorGUIUtility.currentViewWidth - 40);
                EditorGUILayout.SelectableLabel(trimmed, previewStyle, GUILayout.Height(height));
            }
        }

        EditorGUILayout.EndScrollView();
    }
    #endregion

    #region Compare Mode
    private void DrawCompareMode() {
        EditorGUILayout.LabelField("Compare Mode", RCCP_AIDesignSystem.LabelHeader);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Compare with:", GUILayout.Width(100));
        compareTarget = (RCCP_AIPromptAsset)EditorGUILayout.ObjectField(compareTarget,
            typeof(RCCP_AIPromptAsset), false);
        EditorGUILayout.EndHorizontal();

        if (compareTarget == null) {
            EditorGUILayout.HelpBox("Select another prompt asset to compare.", MessageType.Info);

            // Show available prompts
            EditorGUILayout.LabelField("Available Prompts:", RCCP_AIDesignSystem.LabelHeader);
            var allPrompts = Resources.LoadAll<RCCP_AIPromptAsset>("Prompts");
            foreach (var p in allPrompts) {
                if (p != target) {
                    if (GUILayout.Button(p.panelName, RCCP_AIDesignSystem.ButtonSecondary)) {
                        compareTarget = p;
                    }
                }
            }
            return;
        }

        RCCP_AIDesignSystem.Space(10);

        // Side by side view
        EditorGUILayout.BeginHorizontal();

        // Left panel (current)
        EditorGUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 20));
        EditorGUILayout.LabelField($"Current: {((RCCP_AIPromptAsset)target).panelName}", RCCP_AIDesignSystem.LabelHeader);
        compareScrollLeft = EditorGUILayout.BeginScrollView(compareScrollLeft, GUILayout.Height(400));
        DrawDiffContent(systemPrompt.stringValue, compareTarget.systemPrompt, true);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Right panel (compare target)
        EditorGUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 20));
        EditorGUILayout.LabelField($"Compare: {compareTarget.panelName}", RCCP_AIDesignSystem.LabelHeader);
        compareScrollRight = EditorGUILayout.BeginScrollView(compareScrollRight, GUILayout.Height(400));
        DrawDiffContent(compareTarget.systemPrompt, systemPrompt.stringValue, false);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDiffContent(string content, string otherContent, bool isLeft) {
        if (string.IsNullOrEmpty(content)) {
            EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
            return;
        }

        string[] lines = content.Split('\n');
        string[] otherLines = otherContent?.Split('\n') ?? new string[0];
        HashSet<string> otherLineSet = new HashSet<string>(otherLines);

        foreach (string line in lines) {
            string trimmedLine = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmedLine)) {
                RCCP_AIDesignSystem.Space(5);
            } else if (!otherLineSet.Contains(trimmedLine)) {
                // Line is unique to this side
                EditorGUILayout.LabelField((isLeft ? "+ " : "- ") + trimmedLine,
                    isLeft ? diffAddedStyle : diffRemovedStyle);
            } else {
                // Line exists in both
                EditorGUILayout.LabelField("  " + trimmedLine, EditorStyles.label);
            }
        }
    }
    #endregion

    #region Import/Export
    private void ExportPrompt() {
        RCCP_AIPromptAsset asset = (RCCP_AIPromptAsset)target;
        string defaultName = $"{asset.panelName.Replace(" ", "_")}_prompt.txt";
        string path = EditorUtility.SaveFilePanel("Export Prompt", "", defaultName, "txt");

        if (!string.IsNullOrEmpty(path)) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"# Panel: {asset.panelName}");
            sb.AppendLine($"# Type: {asset.panelType}");
            sb.AppendLine($"# Exported: {DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("=== SYSTEM PROMPT ===");
            sb.AppendLine();
            sb.Append(asset.systemPrompt);

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"Prompt exported to: {path}");
        }
    }

    private void ImportPrompt() {
        string path = EditorUtility.OpenFilePanel("Import Prompt", "", "txt");

        if (!string.IsNullOrEmpty(path)) {
            string content = File.ReadAllText(path);

            // Strip header comments if present
            var lines = content.Split('\n').ToList();
            while (lines.Count > 0 && (lines[0].StartsWith("#") || string.IsNullOrWhiteSpace(lines[0]))) {
                lines.RemoveAt(0);
            }

            // Remove "=== SYSTEM PROMPT ===" line if present
            if (lines.Count > 0 && lines[0].Contains("SYSTEM PROMPT")) {
                lines.RemoveAt(0);
            }
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) {
                lines.RemoveAt(0);
            }

            string importedContent = string.Join("\n", lines);

            Undo.RecordObject(target, "Import Prompt");
            systemPrompt.stringValue = importedContent;
            serializedObject.ApplyModifiedProperties();

            Debug.Log($"Prompt imported from: {path}");
        }
    }
    #endregion

    #region Persistence
    private void LoadFoldoutStates() {
        int instanceId = target.GetInstanceID();
        showPanelInfo = RCCP_AIEditorPrefs.GetPromptEditorFoldout(instanceId, "PanelInfo", true);
        showAIConfig = RCCP_AIEditorPrefs.GetPromptEditorFoldout(instanceId, "AIConfig", true);
        showExamples = RCCP_AIEditorPrefs.GetPromptEditorFoldout(instanceId, "Examples", true);
        showSettings = RCCP_AIEditorPrefs.GetPromptEditorFoldout(instanceId, "Settings", true);
        showStatistics = RCCP_AIEditorPrefs.GetPromptEditorFoldout(instanceId, "Statistics", true);
    }

    private void SaveFoldoutStates() {
        int instanceId = target.GetInstanceID();
        RCCP_AIEditorPrefs.SetPromptEditorFoldout(instanceId, "PanelInfo", showPanelInfo);
        RCCP_AIEditorPrefs.SetPromptEditorFoldout(instanceId, "AIConfig", showAIConfig);
        RCCP_AIEditorPrefs.SetPromptEditorFoldout(instanceId, "Examples", showExamples);
        RCCP_AIEditorPrefs.SetPromptEditorFoldout(instanceId, "Settings", showSettings);
        RCCP_AIEditorPrefs.SetPromptEditorFoldout(instanceId, "Statistics", showStatistics);
    }
    #endregion

    #region Helper Classes
    private class PromptStatistics {
        public int CharCount;
        public int WordCount;
        public int LineCount;
        public int TokenEstimate;
        public int SectionCount;
        public int JsonBlockCount;
    }

    private enum ValidationSeverity { Warning, Error }

    private class ValidationIssue {
        public string Message;
        public ValidationSeverity Severity;
        public string QuickFix;
    }

    private static class PromptTemplates {
        public const string InputValidation = @"=== INPUT VALIDATION - READ FIRST ===

Before generating any configuration, validate the user request is meaningful.

REJECT the request if it is:
- Random characters or keyboard mashing (asdfgh, qwerty, 123456, etc.)
- Repeated nonsense patterns (aaaaaa, abcabc, blahblah, etc.)
- Single meaningless characters or symbols
- Non-language gibberish that cannot be interpreted
- Completely unrelated to the panel topic";

        public const string JsonFormat = @"RESPOND WITH ONLY VALID JSON - no text before or after the JSON object.

JSON FORMAT
{
  ""field1"": ""value"",
  ""field2"": 0,
  ""explanation"": ""Brief explanation of changes""
}";

        public const string RejectionResponse = @"When rejecting, respond with ONLY this JSON:
{
  ""rejected"": true,
  ""reason"": ""Your request doesn't appear to be related. Please describe what you want to change."",
  ""suggestions"": [""Example 1"", ""Example 2"", ""Example 3""]
}";

        public const string AcceptRejectExamples = @"Examples of requests to REJECT:
- ""asdasdasdasdasd"" -> rejected (keyboard mashing)
- ""zzzzzzzzz"" -> rejected (repeated characters)
- ""hello world"" -> rejected (not related)

Examples of requests to ACCEPT:
- ""keyword1"" -> valid (relevant description)
- ""keyword2"" -> valid (relevant description)";

        public const string SectionHeader = @"=== NEW SECTION NAME ===

Section content goes here.";

        public const string PerAxleOverride = @"PER-AXLE OVERRIDES
Use ""front"" and ""rear"" objects to set different values per axle.

{
  ""globalField"": 1.0,
  ""front"": {
    ""field"": 1.2
  },
  ""rear"": {
    ""field"": 0.8
  },
  ""explanation"": ""Different front/rear values""
}";
    }
    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
