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
/// Custom editor for RCCP_AIComponentDefaults to display extracted values
/// and provide easy access to re-extraction tools with a polished UI using the AI Design System.
/// </summary>
[CustomEditor(typeof(RCCP_AIComponentDefaults))]
public class RCCP_AIComponentDefaultsEditor : Editor {

    private RCCP_AIComponentDefaults defaults;
    private Vector2 promptScrollPos;

    // Custom GUISkin support
    private GUISkin customSkin;
    private GUISkin originalSkin;
    private bool skinLoaded = false;
    
    // Foldout states
    private bool showPromptPreview = false;
    private bool showCoreComponents = true;
    private bool showAdditionalCore = false;
    private bool showAddonComponents = false;
    private bool showUpgradeComponents = false;

    private void OnEnable() {
        defaults = (RCCP_AIComponentDefaults)target;
        LoadCustomSkin();
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

    public override bool RequiresConstantRepaint() {
        return RCCP_AIEditorPrefs.ForceRepaint || base.RequiresConstantRepaint();
    }

    public override void OnInspectorGUI() {
        // Apply custom skin
        ApplyCustomSkin();

        serializedObject.Update();

        // Main Container
        EditorGUILayout.BeginVertical();

        DrawHeader();
        RCCP_AIDesignSystem.Space(5);
        
        DrawMetadataPanel();
        RCCP_AIDesignSystem.Space(10);
        
        DrawActionPanel();
        RCCP_AIDesignSystem.Space(10);
        
        DrawPromptPreview();
        RCCP_AIDesignSystem.Space(10);
        
        DrawComponents();

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();

        // Restore original skin
        RestoreOriginalSkin();
    }

    private new void DrawHeader() {
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelElevated);
        EditorGUILayout.LabelField("RCCP Component Defaults", RCCP_AIDesignSystem.LabelTitle);
        EditorGUILayout.LabelField("Stores default values extracted from RCCP components for AI context.", RCCP_AIDesignSystem.LabelSecondary);
        RCCP_AIDesignSystem.EndPanel();
    }

    private void DrawMetadataPanel() {
        RCCP_AIDesignSystem.DrawSectionHeader("Asset Metadata");
        
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);
        
        // RCCP Version
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("RCCP Version:", GUILayout.Width(120));
        GUIStyle versionStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader);
        if (defaults.rccpVersion == "Unknown" || string.IsNullOrEmpty(defaults.rccpVersion)) {
            versionStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Warning;
        } else {
            versionStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Success;
        }
        EditorGUILayout.LabelField(defaults.rccpVersion ?? "Unknown", versionStyle);
        EditorGUILayout.EndHorizontal();

        // Last Extracted Date
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Last Extracted:", GUILayout.Width(120));
        EditorGUILayout.LabelField(defaults.extractedDate ?? "Never", RCCP_AIDesignSystem.LabelPrimary);
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.EndPanel();
    }

    private void DrawActionPanel() {
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelRecessed);
        
        EditorGUILayout.LabelField("Synchronization", RCCP_AIDesignSystem.LabelHeader);
        EditorGUILayout.HelpBox("Run this extractor after updating RCCP to ensure the AI has the latest default values.", MessageType.Info);
        
        RCCP_AIDesignSystem.Space(10);
        
        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.AccentPrimary;
        if (GUILayout.Button("Extract / Update Defaults", RCCP_AIDesignSystem.ButtonPrimary, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero))) {
            RCCP_AIDefaultsExtractor.ExtractDefaults();
        }
        GUI.backgroundColor = Color.white;
        
        RCCP_AIDesignSystem.EndPanel();
    }

    private void DrawPromptPreview() {
        showPromptPreview = EditorGUILayout.Foldout(showPromptPreview, "Prompt Section Preview", true, RCCP_AIDesignSystem.FoldoutHeader);
        if (showPromptPreview) {
            string promptText = defaults.GetDefaultsAsPromptSection();
            
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelPreview);
            
            // Text Area
            promptScrollPos = EditorGUILayout.BeginScrollView(promptScrollPos, GUILayout.Height(200));
            EditorGUILayout.TextArea(promptText, RCCP_AIDesignSystem.TextAreaMono);
            EditorGUILayout.EndScrollView();
            
            RCCP_AIDesignSystem.Space(10);

            // Copy Button
            if (GUILayout.Button("Copy to Clipboard", RCCP_AIDesignSystem.ButtonPrimary, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
                EditorGUIUtility.systemCopyBuffer = promptText;
                Debug.Log("[RCCP AI] Defaults prompt section copied to clipboard.");
            }
            
            RCCP_AIDesignSystem.EndPanel();
        }
    }

    private void DrawComponents() {
        RCCP_AIDesignSystem.DrawSectionHeader("Component Configuration");

        // Core Components
        DrawComponentSection(ref showCoreComponents, "Core Components", () => {
            DrawProperty("rigidbody");
            DrawProperty("engine");
            DrawProperty("gearbox");
            DrawProperty("clutch");
            DrawProperty("differential");
            DrawProperty("axle");
            DrawProperty("stability");
            DrawProperty("wheelCollider");
            DrawProperty("aeroDynamics");
            DrawProperty("nos");
            DrawProperty("fuelTank");
            DrawProperty("limiter");
            DrawProperty("damage");
            DrawProperty("input");
        });

        // Additional Core Components
        DrawComponentSection(ref showAdditionalCore, "Additional Core", () => {
            DrawProperty("audio");
            DrawProperty("lights");
            DrawProperty("particles");
            DrawProperty("lod");
        });

        // Addon Components
        DrawComponentSection(ref showAddonComponents, "Addon Components", () => {
            DrawProperty("ai");
            DrawProperty("aiObstacleAvoidance");
            DrawProperty("bodyTilt");
            DrawProperty("exhausts");
            DrawProperty("trailerAttacher");
            DrawProperty("wheelBlur");
            DrawProperty("recorder");
            DrawProperty("detachablePart");
            DrawProperty("visualDashboard");
            DrawProperty("exteriorCameras");
        });

        // Upgrade Components
        DrawComponentSection(ref showUpgradeComponents, "Upgrade Defaults", () => {
            DrawProperty("upgradeEngine");
            DrawProperty("upgradeBrake");
            DrawProperty("upgradeHandling");
            DrawProperty("upgradeSpeed");
            DrawProperty("upgradeSpoiler");
            DrawProperty("upgradePaint");
            DrawProperty("upgradeNeon");
            DrawProperty("upgradeDecal");
            DrawProperty("upgradeSiren");
        });
    }

    private void DrawComponentSection(ref bool toggleState, string title, System.Action drawContent) {
        toggleState = EditorGUILayout.Foldout(toggleState, title, true, RCCP_AIDesignSystem.FoldoutHeader);
        if (toggleState) {
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);
            EditorGUI.indentLevel++;
            RCCP_AIDesignSystem.Space(5);
            drawContent.Invoke();
            RCCP_AIDesignSystem.Space(5);
            EditorGUI.indentLevel--;
            RCCP_AIDesignSystem.EndPanel();
        }
        RCCP_AIDesignSystem.Space(5);
    }

    private void DrawProperty(string propertyName) {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        if (prop != null) {
            EditorGUILayout.PropertyField(prop, true);
        } else {
            EditorGUILayout.HelpBox($"Property '{propertyName}' not found!", MessageType.Error);
        }
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
