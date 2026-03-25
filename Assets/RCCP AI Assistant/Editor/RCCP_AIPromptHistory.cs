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
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Manages global prompt history for the RCCP AI Assistant.
/// Stores all prompts and responses regardless of whether changes were applied.
/// </summary>
public static class RCCP_AIPromptHistory {

    #region Constants

    private const string HISTORY_FILE_NAME = "RCCP_AIPromptHistory.json";
    private const int MAX_HISTORY_ENTRIES = 100;
    private const int RESPONSE_PREVIEW_LENGTH = 200;

    #endregion

    #region Data Classes

    [Serializable]
    public class PromptHistoryEntry {
        public string id;
        public string timestamp;
        public string panelType;
        public string panelName;
        public string userPrompt;
        public string aiResponsePreview;
        public string fullAiResponse;
        public string vehicleName;
        public bool wasApplied;
        public bool isInformational;  // True if no changes were generated (just Q&A)
        public bool isFavorite;       // User starred this entry
        public int tokenCount;        // Estimated tokens used

        public DateTime TimestampDate => DateTime.TryParse(timestamp, out var dt) ? dt : DateTime.MinValue;

        /// <summary>
        /// Gets the estimated cost based on token count
        /// </summary>
        public float EstimatedCost => tokenCount * 0.000003f; // ~$3 per 1M tokens

        public PromptHistoryEntry() {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    [Serializable]
    private class PromptHistoryData {
        public List<PromptHistoryEntry> entries = new List<PromptHistoryEntry>();
    }

    #endregion

    #region Private Fields

    private static PromptHistoryData _historyData;
    private static string _historyFilePath;
    private static bool _isLoaded = false;

    #endregion

    #region Properties

    public static int Count => GetHistory().entries.Count;

    public static List<PromptHistoryEntry> Entries => GetHistory().entries;

    /// <summary>
    /// Gets the legacy history file path (in Assets folder - for migration only).
    /// </summary>
    private static string LegacyHistoryFilePath => Path.Combine(RCCP_AIUtility.EditorPath, HISTORY_FILE_NAME);

    private static string HistoryFilePath {
        get {
            if (string.IsNullOrEmpty(_historyFilePath)) {
                // Store in Library folder (gitignored) instead of Assets
                RCCP_AIUtility.EnsureLibraryFolderStructure();
                string basePath = RCCP_AIUtility.HistoryPath;
                _historyFilePath = Path.Combine(basePath, HISTORY_FILE_NAME);

                // Migrate from old location if needed
                MigrateFromLegacyPath();
            }
            return _historyFilePath;
        }
    }

    /// <summary>
    /// Migrates history from old Assets path to new Library path (one-time operation).
    /// </summary>
    private static void MigrateFromLegacyPath() {
        try {
            string legacyPath = LegacyHistoryFilePath;

            // Check if old file exists and new file doesn't
            if (File.Exists(legacyPath) && !File.Exists(_historyFilePath)) {
                // Move the file to new location
                File.Move(legacyPath, _historyFilePath);
                Debug.Log($"[RCCP AI] Migrated prompt history from Assets to Library folder.");

                // Try to delete the old .meta file too
                string metaPath = legacyPath + ".meta";
                if (File.Exists(metaPath)) {
                    File.Delete(metaPath);
                }
            }
        } catch (Exception e) {
            Debug.LogWarning($"[RCCP AI] Failed to migrate prompt history: {e.Message}");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a new entry to the prompt history
    /// </summary>
    /// <returns>The ID of the newly created entry</returns>
    public static string AddEntry(
        string panelType,
        string panelName,
        string userPrompt,
        string aiResponse,
        string vehicleName = null,
        bool wasApplied = false,
        bool isInformational = false,
        int tokenCount = 0
    ) {
        var history = GetHistory();

        // Estimate token count if not provided
        if (tokenCount == 0) {
            tokenCount = EstimateTokenCount(userPrompt, aiResponse);
        }

        var entry = new PromptHistoryEntry {
            panelType = panelType,
            panelName = panelName,
            userPrompt = userPrompt,
            fullAiResponse = aiResponse,
            aiResponsePreview = TruncateResponse(aiResponse),
            vehicleName = vehicleName ?? "",
            wasApplied = wasApplied,
            isInformational = isInformational,
            tokenCount = tokenCount
        };

        // Add to beginning (newest first)
        history.entries.Insert(0, entry);

        // Trim to max entries
        while (history.entries.Count > MAX_HISTORY_ENTRIES) {
            history.entries.RemoveAt(history.entries.Count - 1);
        }

        SaveHistory();

        return entry.id;
    }

    /// <summary>
    /// Estimates token count from prompt and response
    /// </summary>
    private static int EstimateTokenCount(string prompt, string response) {
        int promptTokens = string.IsNullOrEmpty(prompt) ? 0 : (int)(prompt.Length / 3.5f);
        int responseTokens = string.IsNullOrEmpty(response) ? 0 : (int)(response.Length / 3.5f);
        return promptTokens + responseTokens;
    }

    /// <summary>
    /// Marks an entry as applied (when user applies changes after generation)
    /// </summary>
    public static void MarkAsApplied(string entryId) {
        var history = GetHistory();
        var entry = history.entries.FirstOrDefault(e => e.id == entryId);
        if (entry != null) {
            entry.wasApplied = true;
            SaveHistory();
        }
    }

    /// <summary>
    /// Gets entries filtered by criteria
    /// </summary>
    public static List<PromptHistoryEntry> GetFilteredEntries(
        string searchText = null,
        string panelTypeFilter = null,
        bool? appliedFilter = null,
        int limit = 50
    ) {
        var history = GetHistory();
        IEnumerable<PromptHistoryEntry> filtered = history.entries;

        // Filter by search text
        if (!string.IsNullOrEmpty(searchText)) {
            string search = searchText.ToLower();
            filtered = filtered.Where(e =>
                e.userPrompt.ToLower().Contains(search) ||
                e.aiResponsePreview.ToLower().Contains(search) ||
                e.vehicleName.ToLower().Contains(search)
            );
        }

        // Filter by panel type
        if (!string.IsNullOrEmpty(panelTypeFilter) && panelTypeFilter != "All") {
            filtered = filtered.Where(e => e.panelType == panelTypeFilter);
        }

        // Filter by applied status
        if (appliedFilter.HasValue) {
            filtered = filtered.Where(e => e.wasApplied == appliedFilter.Value);
        }

        return filtered.Take(limit).ToList();
    }

    /// <summary>
    /// Gets a specific entry by ID
    /// </summary>
    public static PromptHistoryEntry GetEntry(string entryId) {
        return GetHistory().entries.FirstOrDefault(e => e.id == entryId);
    }

    /// <summary>
    /// Deletes a specific entry
    /// </summary>
    public static void DeleteEntry(string entryId) {
        var history = GetHistory();
        history.entries.RemoveAll(e => e.id == entryId);
        SaveHistory();
    }

    /// <summary>
    /// Clears all history
    /// </summary>
    public static void ClearAll() {
        var history = GetHistory();
        history.entries.Clear();
        SaveHistory();
    }

    /// <summary>
    /// Gets unique panel types from history for filtering
    /// </summary>
    public static List<string> GetUniquePanelTypes() {
        return GetHistory().entries
            .Select(e => e.panelType)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    /// <summary>
    /// Forces reload from disk
    /// </summary>
    public static void Reload() {
        _isLoaded = false;
        _historyData = null;
        GetHistory();
    }

    /// <summary>
    /// Toggles favorite status for an entry
    /// </summary>
    public static void ToggleFavorite(string entryId) {
        var history = GetHistory();
        var entry = history.entries.FirstOrDefault(e => e.id == entryId);
        if (entry != null) {
            entry.isFavorite = !entry.isFavorite;
            SaveHistory();
        }
    }

    /// <summary>
    /// Gets entries sorted by the specified criteria
    /// </summary>
    public static List<PromptHistoryEntry> GetSortedEntries(
        List<PromptHistoryEntry> entries,
        PromptHistorySortOption sortOption
    ) {
        switch (sortOption) {
            case PromptHistorySortOption.NewestFirst:
                return entries.OrderByDescending(e => e.TimestampDate).ToList();
            case PromptHistorySortOption.OldestFirst:
                return entries.OrderBy(e => e.TimestampDate).ToList();
            case PromptHistorySortOption.ByPanel:
                return entries.OrderBy(e => e.panelName).ThenByDescending(e => e.TimestampDate).ToList();
            case PromptHistorySortOption.ByVehicle:
                return entries.OrderBy(e => e.vehicleName).ThenByDescending(e => e.TimestampDate).ToList();
            case PromptHistorySortOption.FavoritesFirst:
                return entries.OrderByDescending(e => e.isFavorite).ThenByDescending(e => e.TimestampDate).ToList();
            default:
                return entries;
        }
    }

    /// <summary>
    /// Exports entries to JSON string
    /// </summary>
    public static string ExportToJson(List<PromptHistoryEntry> entries = null) {
        var toExport = entries ?? GetHistory().entries;
        var exportData = new PromptHistoryData { entries = toExport.ToList() };
        return JsonUtility.ToJson(exportData, true);
    }

    /// <summary>
    /// Exports a single entry to JSON string
    /// </summary>
    public static string ExportEntryToJson(string entryId) {
        var entry = GetEntry(entryId);
        if (entry == null) return "{}";
        return JsonUtility.ToJson(entry, true);
    }

    /// <summary>
    /// Deletes multiple entries by ID
    /// </summary>
    public static void DeleteEntries(List<string> entryIds) {
        var history = GetHistory();
        history.entries.RemoveAll(e => entryIds.Contains(e.id));
        SaveHistory();
    }

    /// <summary>
    /// Gets total token count across all entries
    /// </summary>
    public static int GetTotalTokenCount() {
        return GetHistory().entries.Sum(e => e.tokenCount);
    }

    /// <summary>
    /// Gets total estimated cost
    /// </summary>
    public static float GetTotalEstimatedCost() {
        return GetHistory().entries.Sum(e => e.EstimatedCost);
    }

    #endregion

    #region Enums

    public enum PromptHistorySortOption {
        NewestFirst,
        OldestFirst,
        ByPanel,
        ByVehicle,
        FavoritesFirst
    }

    #endregion

    #region Private Methods

    private static PromptHistoryData GetHistory() {
        if (!_isLoaded || _historyData == null) {
            LoadHistory();
        }
        return _historyData;
    }

    private static void LoadHistory() {
        _historyData = new PromptHistoryData();

        try {
            if (File.Exists(HistoryFilePath)) {
                string json = File.ReadAllText(HistoryFilePath);
                _historyData = JsonUtility.FromJson<PromptHistoryData>(json) ?? new PromptHistoryData();
            }
        } catch (Exception ex) {
            Debug.LogWarning($"[RCCP AI] Failed to load prompt history: {ex.Message}");
            _historyData = new PromptHistoryData();
        }

        _isLoaded = true;
    }

    private static void SaveHistory() {
        try {
            string json = JsonUtility.ToJson(_historyData, true);
            File.WriteAllText(HistoryFilePath, json);
        } catch (Exception ex) {
            Debug.LogError($"[RCCP AI] Failed to save prompt history: {ex.Message}");
        }
    }

    private static string TruncateResponse(string response) {
        if (string.IsNullOrEmpty(response)) return "";

        // Try to extract just the explanation if it's JSON
        if (response.Contains("\"explanation\"")) {
            try {
                int start = response.IndexOf("\"explanation\"");
                if (start >= 0) {
                    int colonPos = response.IndexOf(':', start);
                    int quoteStart = response.IndexOf('"', colonPos + 1);
                    int quoteEnd = response.IndexOf('"', quoteStart + 1);
                    if (quoteStart >= 0 && quoteEnd > quoteStart) {
                        string explanation = response.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                        if (explanation.Length > RESPONSE_PREVIEW_LENGTH) {
                            return explanation.Substring(0, RESPONSE_PREVIEW_LENGTH) + "...";
                        }
                        return explanation;
                    }
                }
            } catch {
                // Fall through to default truncation
            }
        }

        // Default truncation
        if (response.Length > RESPONSE_PREVIEW_LENGTH) {
            return response.Substring(0, RESPONSE_PREVIEW_LENGTH) + "...";
        }
        return response;
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
