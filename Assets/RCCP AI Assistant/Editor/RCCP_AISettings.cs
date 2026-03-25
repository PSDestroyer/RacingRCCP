//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Main settings for RCCP AI Assistant.
/// Holds all prompt assets and global configuration.
/// Place in Resources folder as "RCCP_AISettings"
/// </summary>
[CreateAssetMenu(fileName = "RCCP_AISettings", menuName = "BoneCracker Games/RCCP/AI Settings")]
public class RCCP_AISettings : ScriptableObject {

    #region Singleton

    private static RCCP_AISettings _instance;
    private static bool _warnedAboutMissing = false;

    public static RCCP_AISettings Instance {
        get {
            if (_instance == null) {
                _instance = Resources.Load<RCCP_AISettings>("RCCP_AISettings");

                // Warn once if settings asset is missing
                if (_instance == null && !_warnedAboutMissing) {
                    _warnedAboutMissing = true;
                    Debug.LogWarning("[RCCP AI] RCCP_AISettings asset not found in Resources folder. " +
                        "Open the AI Assistant window (Tools > BoneCracker Games > RCCP AI Assistant) to auto-create it.");
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Resets the singleton instance. Call this after creating the settings asset.
    /// </summary>
    public static void ResetInstance() {
        _instance = null;
        _warnedAboutMissing = false;
    }

    #endregion

    [Header("API Configuration")]
    [Tooltip("API endpoint (direct Claude API - used when server proxy is disabled)")]
    public string apiEndpoint = "https://api.anthropic.com/v1/messages";

    [Header("Server Proxy (Usage Protection)")]
    [Tooltip("Enable to route requests through your server for usage tracking")]
    public bool useServerProxy = true;

    [Tooltip("URL of your proxy server API endpoint")]
    public string serverUrl = "https://www.bonecrackergames.com/rccp-api/api.php";

    [Tooltip("Timeout in seconds for server requests")]
    public int serverTimeout = 120;

    [Tooltip("Model to use for text queries (cheaper, faster options available)")]
    public string textModel = "claude-sonnet-4-5-20250929";

    [Tooltip("Model to use for vision/image analysis queries")]
    public string visionModel = "claude-sonnet-4-5-20250929";

    [Tooltip("Legacy field - use textModel instead")]
    [HideInInspector]
    public string model = "claude-sonnet-4-5-20250929";

    [Tooltip("Maximum tokens for response")]
    public int maxTokens = 4096;

    /// <summary>
    /// Available Claude models for selection
    /// </summary>
    public static readonly string[] AvailableModels = new string[] {
        "claude-haiku-4-5-20251001",
        "claude-sonnet-4-5-20250929",
        "claude-opus-4-5-20251101"
    };

    /// <summary>
    /// Display names for the models
    /// </summary>
    public static readonly string[] ModelDisplayNames = new string[] {
        "Haiku 4.5 (Fastest, Cheapest)",
        "Sonnet 4.5 (Balanced)",
        "Opus 4.5 (Most Capable, Expensive)"
    };

    /// <summary>
    /// Cost multipliers relative to Sonnet (for display)
    /// </summary>
    public static readonly float[] ModelCostMultipliers = new float[] {
        0.08f,  // Haiku ~12x cheaper
        1.0f,   // Sonnet baseline
        5.0f    // Opus ~5x more expensive
    };

    [Tooltip("Maximum character limit for user prompt")]
    public int maxPromptLength = 1000;

    [Header("Panel Prompts")]
    [Tooltip("All available panel prompt assets")]
    public RCCP_AIPromptAsset[] prompts;

    [Header("Welcome & Onboarding")]
    [Tooltip("Show welcome panel on first startup")]
    public bool showWelcomeOnStartup = true;

    // Note: hasSeenWelcome moved to EditorPrefs (user-specific, key: "RCCP_AI_HasSeenWelcome")

    [Header("Developer Options")]
    [Tooltip("Enable verbose logging to console for debugging")]
    public bool verboseLogging = false;

    [Tooltip("DANGEROUS: Disable TLS certificate validation. Only use for local development with self-signed certificates.")]
    public bool debugDisableTLSValidation = false;

    [Header("UI Customization")]
    [Tooltip("Custom GUISkin to apply to the AI Assistant window. Leave empty to use default Unity styles.")]
    public GUISkin customSkin;

    [Tooltip("If true, pressing Enter (without modifiers) will send the prompt. If false, use Ctrl+Enter.")]
    public bool sendOnEnter = false;

    [Header("Global Prompt Parts")]
    [Tooltip("Added to ALL prompts as prefix")]
    [TextArea(5, 10)]
    public string globalPrefix = "RESPOND WITH ONLY JSON - no markdown, no backticks, no explanation outside JSON.\n\nMINIMAL CHANGES RULE: Include ONLY fields the user explicitly requests. Do NOT add related or 'helpful' settings. When in doubt, include FEWER fields.";

    [Tooltip("Added to end of ALL prompts")]
    [TextArea(3, 5)]
    public string globalSuffix = "";

    /// <summary>
    /// Get prompt asset by panel type
    /// </summary>
    public RCCP_AIPromptAsset GetPrompt(RCCP_AIPromptAsset.PanelType type) {
        if (prompts == null) return null;

        foreach (var prompt in prompts) {
            if (prompt != null && prompt.panelType == type) {
                return prompt;
            }
        }
        return null;
    }

    /// <summary>
    /// Get full system prompt with global prefix/suffix and component defaults
    /// </summary>
    public string GetFullSystemPrompt(RCCP_AIPromptAsset promptAsset) {
        if (promptAsset == null) return "";

        string full = "";

        if (!string.IsNullOrEmpty(globalPrefix)) {
            full += globalPrefix + "\n\n";
        }

        full += promptAsset.systemPrompt;

        if (!string.IsNullOrEmpty(globalSuffix)) {
            full += "\n\n" + globalSuffix;
        }

        // Append component defaults from RCCP (single source of truth)
#if UNITY_EDITOR
        var defaults = RCCP_AIComponentDefaults.Instance;
        if (defaults != null) {
            full += "\n\n" + defaults.GetDefaultsAsPromptSection();
        }
#endif

        return full;
    }

    /// <summary>
    /// Get all valid prompts
    /// </summary>
    public RCCP_AIPromptAsset[] GetValidPrompts() {
        if (prompts == null) return new RCCP_AIPromptAsset[0];

        var valid = new List<RCCP_AIPromptAsset>();
        foreach (var p in prompts) {
            if (p != null && p.IsValid(out _)) {
                valid.Add(p);
            }
        }
        return valid.ToArray();
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
