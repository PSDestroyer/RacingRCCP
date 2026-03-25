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
/// Stores AI modification history on a vehicle.
/// Attach to vehicles to track all AI-assisted changes.
/// Hidden from Inspector - access via AI Assistant window.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")] // Hide from Add Component menu
public class RCCP_AIHistory : MonoBehaviour {

    [SerializeField]
    private List<HistoryEntry> entries = new List<HistoryEntry>();

    /// <summary>
    /// Maximum number of entries to keep (oldest are removed when exceeded)
    /// </summary>
    public int maxEntries = 50;

    private void OnEnable() {
        // Hide this component from the Inspector
        hideFlags = HideFlags.HideInInspector;
    }

    private void Reset() {
        // Also hide when first added
        hideFlags = HideFlags.HideInInspector;
    }

    /// <summary>
    /// Get all history entries (newest first)
    /// </summary>
    public List<HistoryEntry> Entries => entries;

    /// <summary>
    /// Get entry count
    /// </summary>
    public int Count => entries.Count;

    /// <summary>
    /// Add a new history entry
    /// </summary>
    public void AddEntry(HistoryEntry entry) {
        if (entry == null) return;

        // Insert at beginning (newest first)
        entries.Insert(0, entry);

        // Trim old entries if exceeding max
        while (entries.Count > maxEntries) {
            entries.RemoveAt(entries.Count - 1);
        }
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    public void ClearHistory() {
        entries.Clear();
    }

    /// <summary>
    /// Get entry by index
    /// </summary>
    public HistoryEntry GetEntry(int index) {
        if (index < 0 || index >= entries.Count) return null;
        return entries[index];
    }

    /// <summary>
    /// Remove entry by index
    /// </summary>
    public void RemoveEntry(int index) {
        if (index >= 0 && index < entries.Count) {
            entries.RemoveAt(index);
        }
    }

    /// <summary>
    /// Represents a single AI modification entry
    /// </summary>
    [Serializable]
    public class HistoryEntry {

        [Header("Metadata")]
        public string timestamp;
        public string panelType;
        public string userPrompt;

        [Header("AI Response")]
        [TextArea(2, 5)]
        public string explanation;

        [Header("Configuration")]
        [TextArea(5, 15)]
        public string appliedJson;

        [Header("Before State")]
        [TextArea(5, 15)]
        public string beforeState;

        [Header("Before State JSON (for restore)")]
        [TextArea(5, 15)]
        public string beforeStateJson;

        [Header("After State")]
        [TextArea(5, 15)]
        public string afterState;

        /// <summary>
        /// Create a new history entry with current timestamp
        /// </summary>
        public HistoryEntry() {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Create a history entry with all details
        /// </summary>
        public HistoryEntry(
            string panelType,
            string userPrompt,
            string explanation,
            string appliedJson,
            string beforeState,
            string beforeStateJson,
            string afterState
        ) {
            this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            this.panelType = panelType;
            this.userPrompt = userPrompt;
            this.explanation = explanation;
            this.appliedJson = appliedJson;
            this.beforeState = beforeState;
            this.beforeStateJson = beforeStateJson;
            this.afterState = afterState;
        }

        /// <summary>
        /// Check if this entry can be restored
        /// </summary>
        public bool CanRestore => !string.IsNullOrEmpty(beforeStateJson);

        /// <summary>
        /// Get a short summary for display in list
        /// </summary>
        public string GetSummary() {
            string shortPrompt = userPrompt;
            if (!string.IsNullOrEmpty(shortPrompt) && shortPrompt.Length > 40) {
                shortPrompt = shortPrompt.Substring(0, 37) + "...";
            }
            return $"[{timestamp}] {shortPrompt}";
        }

        /// <summary>
        /// Get formatted date only
        /// </summary>
        public string GetDate() {
            if (DateTime.TryParse(timestamp, out DateTime dt)) {
                return dt.ToString("MMM dd, yyyy");
            }
            return timestamp;
        }

        /// <summary>
        /// Get formatted time only
        /// </summary>
        public string GetTime() {
            if (DateTime.TryParse(timestamp, out DateTime dt)) {
                return dt.ToString("HH:mm");
            }
            return "";
        }
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
