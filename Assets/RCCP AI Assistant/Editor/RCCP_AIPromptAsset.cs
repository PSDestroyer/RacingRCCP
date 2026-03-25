//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System;
using UnityEngine;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// ScriptableObject that holds AI prompt configuration for a panel.
/// Create one asset per panel type.
/// </summary>
[CreateAssetMenu(fileName = "New AI Prompt", menuName = "BoneCracker Games/RCCP/AI Prompt Asset")]
public class RCCP_AIPromptAsset : ScriptableObject {

    [Header("Panel Info")]
    [Tooltip("Display name shown in tab")]
    public string panelName = "New Panel";

    [Tooltip("Icon emoji or character")]
    public string panelIcon = "🔧";

    [Tooltip("Short description of panel functionality")]
    [TextArea(2, 3)]
    public string panelDescription = "Description of what this panel does.";

    [Header("AI Configuration")]
    [Tooltip("System prompt sent to AI. Keep focused and minimal.")]
    [TextArea(10, 30)]
    public string systemPrompt = "";

    [Tooltip("Example prompts for quick selection buttons")]
    public string[] examplePrompts = new string[] {
        "Example prompt 1",
        "Example prompt 2",
        "Example prompt 3"
    };

    [Tooltip("Placeholder text shown in empty prompt field")]
    public string placeholderText = "Describe what you want...";

    [Header("Panel Settings")]
    [Tooltip("Panel type determines which UI and apply logic to use")]
    public PanelType panelType = PanelType.Generic;

    [Tooltip("Does this panel require a vehicle selection?")]
    public bool requiresVehicle = false;

    [Tooltip("Does this panel require an existing RCCP_CarController?")]
    public bool requiresRCCPController = false;

    [Tooltip("Include mesh analysis in prompt?")]
    public bool includeMeshAnalysis = false;

    [Tooltip("Include current component values in prompt?")]
    public bool includeCurrentState = false;

    /// <summary>
    /// Panel types - determines which UI and apply logic to use
    /// </summary>
    public enum PanelType {
        Generic,
        VehicleCreation,
        VehicleCustomization,
        Behaviors,
        Wheels,
        Audio,
        Lights,
        Damage,
        Diagnostics  // Special panel - runs local checks, no AI needed
    }

    /// <summary>
    /// Estimate token count for this prompt
    /// </summary>
    public int EstimatedTokens {
        get {
            if (string.IsNullOrEmpty(systemPrompt)) return 0;
            // Average token is ~3.5 characters for mixed code/text content
            return Mathf.CeilToInt(systemPrompt.Length / 3.5f);
        }
    }

    /// <summary>
    /// Validate the prompt asset
    /// </summary>
    public bool IsValid(out string error) {
        if (string.IsNullOrEmpty(panelName)) {
            error = "Panel name is required";
            return false;
        }

        // Diagnostics panel doesn't need a system prompt (no AI)
        if (panelType != PanelType.Diagnostics) {
            if (string.IsNullOrEmpty(systemPrompt)) {
                error = "System prompt is required";
                return false;
            }

            if (systemPrompt.Length < 100) {
                error = "System prompt seems too short";
                return false;
            }
        }

        error = null;
        return true;
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
