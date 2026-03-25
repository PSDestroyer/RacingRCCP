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
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Server-authoritative rate limiter for RCCP AI Assistant.
///
/// This class displays usage information from the server.
/// All rate limiting enforcement is done server-side.
/// Local values are cached from server responses for UI display only.
///
/// Users with their own API key bypass all limits.
/// </summary>
public static class RCCP_AIRateLimiter {

    #region Cached Server Data

    /// <summary>
    /// Cached usage data from the server. Used for UI display only.
    /// </summary>
    private static RCCP_ServerProxy.ServerUsage _cachedUsage;

    /// <summary>
    /// Gets cached usage data, loading from EditorPrefs if needed.
    /// </summary>
    private static RCCP_ServerProxy.ServerUsage CachedUsage {
        get {
            if (_cachedUsage == null) {
                LoadCachedUsage();
            }
            return _cachedUsage;
        }
    }

    /// <summary>
    /// Loads cached usage from EditorPrefs.
    /// </summary>
    private static void LoadCachedUsage() {
        string json = RCCP_AIEditorPrefs.CachedUsage;
        if (!string.IsNullOrEmpty(json)) {
            try {
                _cachedUsage = JsonUtility.FromJson<RCCP_ServerProxy.ServerUsage>(json);
            } catch {
                _cachedUsage = CreateDefaultUsage();
            }
        } else {
            _cachedUsage = CreateDefaultUsage();
        }
    }

    /// <summary>
    /// Creates default usage data for new users.
    /// </summary>
    private static RCCP_ServerProxy.ServerUsage CreateDefaultUsage() {
        return new RCCP_ServerProxy.ServerUsage {
            tier = "setup",
            setupPoolRemaining = 400,
            setupPoolTotal = 400,
            dailyUsed = 0,
            dailyLimit = 20,
            dailyRemaining = 20,
            hourlyUsed = 0,
            hourlyLimit = 20, // 20/hour during setup, 10/hour after
            totalRequests = 0,
            secondsUntilDailyReset = 0,
            secondsUntilHourlyReset = 0,
            usesOwnKey = false
        };
    }

    #endregion

    #region Properties (Read from Cached Server Data)

    /// <summary>
    /// Whether the user has configured their own API key (bypasses all limits).
    /// </summary>
    public static bool UseOwnApiKey {
        get => RCCP_AIEditorPrefs.UseOwnApiKey;
        set => RCCP_AIEditorPrefs.UseOwnApiKey = value;
    }

    /// <summary>
    /// Developer mode enables debug features.
    /// </summary>
    public static bool IsDeveloperMode => RCCP_AIEditorPrefs.DeveloperMode;

    /// <summary>
    /// Remaining requests in the setup pool (from cached server data).
    /// </summary>
    public static int SetupPoolRemaining => CachedUsage?.setupPoolRemaining ?? 400;

    /// <summary>
    /// Total setup pool size (from cached server data).
    /// </summary>
    public static int SetupPoolTotal => CachedUsage?.setupPoolTotal ?? 400;

    /// <summary>
    /// Whether we're still in the setup phase.
    /// </summary>
    public static bool IsInSetupPhase => CachedUsage?.tier == "setup";

    /// <summary>
    /// Requests used today (from cached server data).
    /// </summary>
    public static int DailyUsed => CachedUsage?.dailyUsed ?? 0;

    /// <summary>
    /// Daily limit (from cached server data).
    /// </summary>
    public static int DailyLimit => CachedUsage?.dailyLimit ?? 20;

    /// <summary>
    /// Remaining daily requests (from cached server data).
    /// </summary>
    public static int DailyRemaining => CachedUsage?.dailyRemaining ?? 20;

    /// <summary>
    /// Requests used in the current hour (from cached server data).
    /// </summary>
    public static int HourlyUsed => CachedUsage?.hourlyUsed ?? 0;

    /// <summary>
    /// Hourly limit (from cached server data).
    /// </summary>
    public static int HourlyLimit => CachedUsage?.hourlyLimit ?? 20; // 20/hour setup, 10/hour daily

    /// <summary>
    /// Remaining requests this hour.
    /// </summary>
    public static int HourlyRemaining => Mathf.Max(0, HourlyLimit - HourlyUsed);

    /// <summary>
    /// Total requests made (lifetime, from cached server data).
    /// </summary>
    public static int TotalRequests => CachedUsage?.totalRequests ?? 0;

    /// <summary>
    /// Time until daily limit resets (formatted string).
    /// </summary>
    public static string TimeUntilDailyReset {
        get {
            int seconds = CachedUsage?.secondsUntilDailyReset ?? 0;
            if (seconds <= 0) {
                // Estimate from local time if server didn't provide
                DateTime now = DateTime.Now;
                DateTime midnight = now.Date.AddDays(1);
                TimeSpan remaining = midnight - now;
                seconds = (int)remaining.TotalSeconds;
            }

            if (seconds >= 3600)
                return $"{seconds / 3600}h {(seconds % 3600) / 60}m";
            else
                return $"{seconds / 60}m";
        }
    }

    /// <summary>
    /// Seconds until hourly limit resets.
    /// </summary>
    public static float SecondsUntilHourlyReset => CachedUsage?.secondsUntilHourlyReset ?? 0;

    #endregion

    #region Server Sync

    /// <summary>
    /// Syncs local cache from server response.
    /// Server is authoritative - this overwrites cached values.
    /// </summary>
    public static void SyncFromServer(RCCP_ServerProxy.ServerUsage serverUsage) {
        if (serverUsage == null) return;

        _cachedUsage = serverUsage;

        // Persist to EditorPrefs for next session
        string json = JsonUtility.ToJson(serverUsage);
        RCCP_AIEditorPrefs.CachedUsage = json;
        RCCP_AIEditorPrefs.LastServerSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Update UseOwnApiKey from server
        RCCP_AIEditorPrefs.UseOwnApiKey = serverUsage.usesOwnKey;

        if (VerboseLogging) {
            Debug.Log($"[RCCP AI Rate Limiter] Synced from server. " +
                     $"Tier: {serverUsage.tier}, " +
                     $"Setup: {serverUsage.setupPoolRemaining}/{serverUsage.setupPoolTotal}, " +
                     $"Daily: {serverUsage.dailyUsed}/{serverUsage.dailyLimit}, " +
                     $"Total: {serverUsage.totalRequests}");
        }
    }

    /// <summary>
    /// Clears cached usage data. Call when device token changes.
    /// </summary>
    public static void ClearCache() {
        _cachedUsage = null;
        RCCP_AIEditorPrefs.CachedUsage = "";
    }

    /// <summary>
    /// Checks if we have cached usage data.
    /// </summary>
    public static bool HasCachedUsage => !string.IsNullOrEmpty(RCCP_AIEditorPrefs.CachedUsage);

    /// <summary>
    /// Gets the last server sync time.
    /// </summary>
    public static string LastSyncTime => RCCP_AIEditorPrefs.LastServerSync;

    #endregion

    #region Status Display Methods

    /// <summary>
    /// Gets a user-friendly status message for display.
    /// </summary>
    public static string GetStatusMessage() {
        if (UseOwnApiKey) {
            return $"Using your API key (unlimited) | Total requests: {TotalRequests}";
        }

        if (IsInSetupPhase) {
            return $"Setup: {SetupPoolRemaining}/{SetupPoolTotal} requests remaining";
        } else {
            return $"Daily: {DailyRemaining}/{DailyLimit} remaining | Resets in {TimeUntilDailyReset}";
        }
    }

    /// <summary>
    /// Gets the current usage tier for UI display.
    /// </summary>
    public static UsageTier GetCurrentTier() {
        if (UseOwnApiKey) return UsageTier.OwnApiKey;
        if (IsInSetupPhase) return UsageTier.SetupPhase;
        return UsageTier.DailyFree;
    }

    #endregion

    #region Enums

    /// <summary>
    /// Current usage tier for the user.
    /// </summary>
    public enum UsageTier {
        SetupPhase,    // Using setup pool (Phase 1)
        DailyFree,     // Using daily allowance (Phase 2)
        OwnApiKey      // Using own API key (unlimited)
    }

    #endregion

    #region Dialog Tracking

    // Dialog tracking is kept for UI purposes only
    // The server still enforces limits regardless of dialogs

    /// <summary>
    /// Checks if the setup exhausted dialog has been shown.
    /// </summary>
    public static bool HasShownSetupExhaustedDialog() {
        return RCCP_AIEditorPrefs.ShownSetupExhaustedDialog;
    }

    /// <summary>
    /// Marks the setup exhausted dialog as shown.
    /// </summary>
    public static void MarkSetupExhaustedDialogShown() {
        RCCP_AIEditorPrefs.ShownSetupExhaustedDialog = true;
    }

    /// <summary>
    /// Resets dialog tracking (for testing).
    /// </summary>
    public static void ResetDialogTracking() {
        RCCP_AIEditorPrefs.ShownSetupExhaustedDialog = false;
        RCCP_AIEditorPrefs.ShownDailyLimitDate = "";
        if (VerboseLogging) {
            Debug.Log("[RCCP AI Rate Limiter] Dialog tracking reset");
        }
    }

    /// <summary>
    /// Checks for pending dialogs and shows them if needed.
    /// Returns true if a dialog was shown.
    /// </summary>
    public static bool CheckAndShowPendingDialogs() {
        if (UseOwnApiKey) return false;

        // Check if setup pool just exhausted and we haven't shown the dialog yet
        if (!IsInSetupPhase && !HasShownSetupExhaustedDialog()) {
            // Setup phase just ended - show transition dialog
            MarkSetupExhaustedDialogShown();
            bool getApiKey = EditorUtility.DisplayDialog(
                "Setup Phase Complete",
                $"You've used your initial setup pool of requests.\n\n" +
                $"From now on, you'll have {DailyLimit} free requests per day.\n\n" +
                $"For unlimited requests, you can use your own Claude API key in Settings.",
                "Get API Key",
                "Got it");
            if (getApiKey) {
                Application.OpenURL("https://platform.claude.com/dashboard");
            }
            return true;
        }

        // Check if daily limit reached today and we haven't shown today's dialog
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        if (!IsInSetupPhase && DailyRemaining == 0 && RCCP_AIEditorPrefs.ShownDailyLimitDate != today) {
            RCCP_AIEditorPrefs.ShownDailyLimitDate = today;
            bool getApiKey = EditorUtility.DisplayDialog(
                "Daily Limit Reached",
                $"You've reached today's limit of {DailyLimit} free requests.\n\n" +
                $"The limit resets at midnight.\n\n" +
                $"For unlimited requests, use your own Claude API key in Settings.",
                "Get API Key",
                "OK");
            if (getApiKey) {
                Application.OpenURL("https://platform.claude.com/dashboard");
            }
            return true;
        }

        return false;
    }

    #endregion

    #region UI Helper Methods

    /// <summary>
    /// Gets progress value for setup pool (0-1).
    /// </summary>
    public static float GetSetupPoolProgress() {
        if (SetupPoolTotal <= 0) return 1f;
        return (float)SetupPoolRemaining / SetupPoolTotal;
    }

    /// <summary>
    /// Gets progress value for daily usage (0-1, where 1 = fully used).
    /// </summary>
    public static float GetDailyUsageProgress() {
        if (DailyLimit <= 0) return 0f;
        return (float)DailyUsed / DailyLimit;
    }

    /// <summary>
    /// Gets progress value for hourly usage (0-1, where 1 = fully used).
    /// </summary>
    public static float GetHourlyUsageProgress() {
        if (HourlyLimit <= 0) return 0f;
        return (float)HourlyUsed / HourlyLimit;
    }

    /// <summary>
    /// Gets the color for the usage bar based on remaining capacity.
    /// </summary>
    public static Color GetUsageBarColor() {
        if (UseOwnApiKey) return RCCP_AIDesignSystem.Colors.Info; // Blue

        float remaining;
        if (IsInSetupPhase) {
            remaining = GetSetupPoolProgress();
        } else {
            remaining = 1f - GetDailyUsageProgress();
        }

        if (remaining > 0.5f) return RCCP_AIDesignSystem.Colors.Success; // Green
        if (remaining > 0.2f) return RCCP_AIDesignSystem.Colors.Warning; // Yellow
        return RCCP_AIDesignSystem.Colors.Error;                         // Red
    }

    /// <summary>
    /// Draws the usage UI section (call from Settings panel).
    /// Returns early if using own API key (usage section should be hidden).
    /// </summary>
    public static void DrawUsageUI() {
        // Hide usage section when using own API key
        if (UseOwnApiKey) {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        UsageTier tier = GetCurrentTier();

        // Tier label
        string tierLabel = tier switch {
            UsageTier.OwnApiKey => "Tier: Own API Key (Unlimited)",
            UsageTier.SetupPhase => "Tier: Setup Phase (No Daily Limit)",
            UsageTier.DailyFree => "Tier: Free (Daily Limit)",
            _ => "Unknown"
        };
        EditorGUILayout.LabelField(tierLabel, EditorStyles.boldLabel);

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Setup pool or daily progress
        if (IsInSetupPhase) {
            EditorGUILayout.LabelField($"Setup Requests: {SetupPoolRemaining} / {SetupPoolTotal}");
            DrawProgressBar(GetSetupPoolProgress(), GetUsageBarColor());
        } else {
            EditorGUILayout.LabelField($"Daily Requests: {DailyUsed} / {DailyLimit} used");
            DrawProgressBar(GetDailyUsageProgress(), GetUsageBarColor());
            EditorGUILayout.LabelField($"Resets in: {TimeUntilDailyReset}");
        }

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Hourly burst indicator
        EditorGUILayout.LabelField($"Hourly Burst: {HourlyUsed} / {HourlyLimit}");
        if (HourlyRemaining == 0) {
            int minutes = Mathf.CeilToInt(SecondsUntilHourlyReset / 60f);
            EditorGUILayout.LabelField($"Resets in: {minutes}m", EditorStyles.miniLabel);
        }

        GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Warning messages
        if (!IsInSetupPhase && DailyRemaining <= 3 && DailyRemaining > 0) {
            EditorGUILayout.HelpBox(
                $"Only {DailyRemaining} requests remaining today. Switch to Own API Key for unlimited.",
                MessageType.Warning);
        } else if (!IsInSetupPhase && DailyRemaining == 0) {
            EditorGUILayout.HelpBox(
                "Daily limit reached. Switch to Own API Key to continue, or wait until tomorrow.",
                MessageType.Error);
        } else if (IsInSetupPhase && SetupPoolRemaining <= 10) {
            EditorGUILayout.HelpBox(
                $"Only {SetupPoolRemaining} setup requests left. After that, you'll have {DailyLimit}/day.",
                MessageType.Info);
        }

        // Show last sync time
        if (!string.IsNullOrEmpty(LastSyncTime)) {
            GUILayout.Space(RCCP_AIDesignSystem.Spacing.Space1);
            EditorGUILayout.LabelField($"Last sync: {LastSyncTime}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawProgressBar(float progress, Color color) {
        Rect rect = EditorGUILayout.GetControlRect(false, 16);

        // Background
        EditorGUI.DrawRect(rect, RCCP_AIDesignSystem.Colors.BgDark);

        // Fill (inverted for "remaining" display in setup, normal for "used" in daily)
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);
        EditorGUI.DrawRect(fillRect, color);
    }

    #endregion

    #region Helper Methods

    private static bool VerboseLogging {
        get {
            var settings = RCCP_AISettings.Instance;
            return settings != null && settings.verboseLogging;
        }
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
