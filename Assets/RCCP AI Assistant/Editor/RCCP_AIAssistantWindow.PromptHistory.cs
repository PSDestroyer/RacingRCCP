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
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

public partial class RCCP_AIAssistantWindow {

    #region UI Drawing - Prompt History

    private void DrawPromptHistoryPanel() {
        EditorGUILayout.BeginVertical();

        // Header with stats
        RCCP_AIDesignSystem.Space(S6);
        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S7);

        if (GUILayout.Button("← Back", GUILayout.Width(70), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            showPromptHistory = false;
            selectedPromptHistoryId = null;
            promptHistoryBulkMode = false;
            selectedHistoryEntries.Clear();
        }

        RCCP_AIDesignSystem.Space(S6);
        GUILayout.Label("💬  Prompt History", headerStyle);

        // Stats display
        RCCP_AIDesignSystem.Space(S7);
        int totalTokens = RCCP_AIPromptHistory.GetTotalTokenCount();
        GUIStyle statsStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        };

        // Only show cost when using own API key (not server proxy)
        bool showCost = RCCP_AISettings.Instance != null && !RCCP_AISettings.Instance.useServerProxy;
        if (showCost) {
            float totalCost = RCCP_AIPromptHistory.GetTotalEstimatedCost();
            GUILayout.Label($"📊 {RCCP_AIPromptHistory.Count} entries  •  ~{totalTokens:N0} tokens  •  ~${totalCost:F4}", statsStyle);
        } else {
            GUILayout.Label($"📊 {RCCP_AIPromptHistory.Count} entries  •  ~{totalTokens:N0} tokens", statsStyle);
        }

        GUILayout.FlexibleSpace();

        // Export button
        EditorGUI.BeginDisabledGroup(RCCP_AIPromptHistory.Count == 0);
        if (GUILayout.Button("📤 Export", GUILayout.Width(70), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            ExportPromptHistory();
        }

        // Clear all button
        if (GUILayout.Button("Clear All", GUILayout.Width(70), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            if (EditorUtility.DisplayDialog("Clear Prompt History",
                "Are you sure you want to delete all prompt history entries? This cannot be undone.",
                "Clear All", "Cancel")) {
                RCCP_AIPromptHistory.ClearAll();
                selectedPromptHistoryId = null;
                selectedHistoryEntries.Clear();
            }
        }
        EditorGUI.EndDisabledGroup();

        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S5);

        // Search, filters, and sort options
        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S7);

        // Search box
        GUILayout.Label("Search:", GUILayout.Width(50), GUILayout.ExpandWidth(false));
        var searchFieldStyle = new GUIStyle(RCCP_AIDesignSystem.TextField);
        searchFieldStyle.normal.textColor = RCCP_AIDesignSystem.Colors.TextPrimary;
        searchFieldStyle.focused.textColor = RCCP_AIDesignSystem.Colors.TextPrimary;
        promptHistorySearch = EditorGUILayout.TextField(promptHistorySearch, searchFieldStyle, GUILayout.MinWidth(120), GUILayout.MaxWidth(180), GUILayout.ExpandWidth(true));
        if (!string.IsNullOrEmpty(promptHistorySearch)) {
            if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.ExpandWidth(false))) {
                promptHistorySearch = "";
                GUI.FocusControl(null);
            }
        }

        RCCP_AIDesignSystem.Space(S6);

        // Panel type filter
        GUILayout.Label("Panel:", GUILayout.Width(40), GUILayout.ExpandWidth(false));
        List<string> panelTypes = new List<string> { "All" };
        panelTypes.AddRange(RCCP_AIPromptHistory.GetUniquePanelTypes());
        int currentPanelIndex = panelTypes.IndexOf(promptHistoryPanelFilter);
        if (currentPanelIndex < 0) currentPanelIndex = 0;
        int newPanelIndex = EditorGUILayout.Popup(currentPanelIndex, panelTypes.ToArray(), GUILayout.MinWidth(80), GUILayout.MaxWidth(110), GUILayout.ExpandWidth(false));
        promptHistoryPanelFilter = panelTypes[newPanelIndex];

        RCCP_AIDesignSystem.Space(S5);

        // Applied filter
        GUILayout.Label("Status:", GUILayout.Width(45), GUILayout.ExpandWidth(false));
        string[] appliedOptions = { "All", "Applied", "Pending" };
        promptHistoryAppliedFilter = EditorGUILayout.Popup(promptHistoryAppliedFilter, appliedOptions, GUILayout.MinWidth(60), GUILayout.MaxWidth(80), GUILayout.ExpandWidth(false));

        RCCP_AIDesignSystem.Space(S5);

        // Sort options dropdown
        GUILayout.Label("Sort:", GUILayout.Width(35), GUILayout.ExpandWidth(false));
        string[] sortOptions = { "Newest First", "Oldest First", "By Panel", "By Vehicle", "Favorites First" };
        int currentSort = (int)promptHistorySortOption;
        int newSort = EditorGUILayout.Popup(currentSort, sortOptions, GUILayout.Width(100));
        promptHistorySortOption = (RCCP_AIPromptHistory.PromptHistorySortOption)newSort;

        GUILayout.FlexibleSpace();

        // Bulk mode toggle
        EditorGUI.BeginDisabledGroup(RCCP_AIPromptHistory.Count == 0);
        bool newBulkMode = GUILayout.Toggle(promptHistoryBulkMode, "☑ Bulk", GUILayout.Width(60));
        if (newBulkMode != promptHistoryBulkMode) {
            promptHistoryBulkMode = newBulkMode;
            if (!promptHistoryBulkMode) selectedHistoryEntries.Clear();
        }
        EditorGUI.EndDisabledGroup();

        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.EndHorizontal();

        // Bulk actions bar
        if (promptHistoryBulkMode && selectedHistoryEntries.Count > 0) {
            RCCP_AIDesignSystem.Space(S2);
            EditorGUILayout.BeginHorizontal();
            RCCP_AIDesignSystem.Space(S7);

            GUILayout.Label($"{selectedHistoryEntries.Count} selected", RCCP_AIDesignSystem.LabelHeader);
            RCCP_AIDesignSystem.Space(S5);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Error;
            if (GUILayout.Button("Delete Selected", GUILayout.Width(110), GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem))) {
                if (EditorUtility.DisplayDialog("Delete Selected Entries",
                    $"Are you sure you want to delete {selectedHistoryEntries.Count} entries?",
                    "Delete", "Cancel")) {
                    RCCP_AIPromptHistory.DeleteEntries(selectedHistoryEntries.ToList());
                    selectedHistoryEntries.Clear();
                    selectedPromptHistoryId = null;
                }
            }
            GUI.backgroundColor = oldBg;

            if (GUILayout.Button("Clear Selection", GUILayout.Width(100), GUILayout.Height(RCCP_AIDesignSystem.Heights.ListItem))) {
                selectedHistoryEntries.Clear();
            }

            GUILayout.FlexibleSpace();
            RCCP_AIDesignSystem.Space(S7);
            EditorGUILayout.EndHorizontal();
        }

        RCCP_AIDesignSystem.Space(S5);

        // Main content area - split view
        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S7);

        // Get filtered and sorted entries
        bool? appliedFilter = promptHistoryAppliedFilter == 0 ? null : (bool?)(promptHistoryAppliedFilter == 1);
        var entries = RCCP_AIPromptHistory.GetFilteredEntries(
            promptHistorySearch,
            promptHistoryPanelFilter,
            appliedFilter,
            100
        );

        // Apply sorting
        entries = RCCP_AIPromptHistory.GetSortedEntries(entries, promptHistorySortOption);

        if (entries.Count == 0) {
            // No entries message
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(stepBoxStyle, GUILayout.MinWidth(300), GUILayout.MaxWidth(450));

            GUILayout.Label("No Prompt History", new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
                alignment = TextAnchor.MiddleCenter
            });

            RCCP_AIDesignSystem.Space(S5);

            string emptyMessage = string.IsNullOrEmpty(promptHistorySearch) && promptHistoryPanelFilter == "All"
                ? "Your AI prompts and responses will appear here.\nHistory is saved automatically when you generate configurations."
                : "No entries match your search criteria.";

            GUILayout.Label(emptyMessage, new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.gray }
            });

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        } else {
            // Entry list (left side) with day grouping - responsive width
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(280), GUILayout.MaxWidth(420), GUILayout.ExpandWidth(true));
            GUILayout.Label($"Entries ({entries.Count})", RCCP_AIDesignSystem.LabelHeader);
            RCCP_AIDesignSystem.Space(S2);

            promptHistoryScrollPosition = EditorGUILayout.BeginScrollView(promptHistoryScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            string lastDayGroup = "";
            foreach (var entry in entries) {
                // Day group header
                string dayGroup = GetDayGroupLabel(entry.TimestampDate);
                if (dayGroup != lastDayGroup) {
                    DrawDayGroupHeader(dayGroup);
                    lastDayGroup = dayGroup;
                }

                DrawPromptHistoryListItem(entry);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            RCCP_AIDesignSystem.Space(S6);

            // Entry details (right side)
            EditorGUILayout.BeginVertical();
            if (!string.IsNullOrEmpty(selectedPromptHistoryId)) {
                DrawPromptHistoryEntryDetails(RCCP_AIPromptHistory.GetEntry(selectedPromptHistoryId));
            } else {
                // No selection message
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select an entry to view details", new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                });
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();
        }

        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawDayGroupHeader(string label) {
        RCCP_AIDesignSystem.Space(S4);
        EditorGUILayout.BeginHorizontal();
        GUIStyle groupStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            fontStyle = FontStyle.Bold,
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        };
        GUILayout.Label(label.ToUpper(), groupStyle);

        // Separator line
        Rect lineRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        lineRect.y += 5;
        EditorGUI.DrawRect(lineRect, RCCP_AIDesignSystem.Colors.BorderLight);

        EditorGUILayout.EndHorizontal();
        RCCP_AIDesignSystem.Space(S2);
    }

    private string GetDayGroupLabel(DateTime date) {
        DateTime today = DateTime.Now.Date;
        DateTime entryDate = date.Date;

        if (entryDate == today) return "Today";
        if (entryDate == today.AddDays(-1)) return "Yesterday";
        if (entryDate >= today.AddDays(-7)) return "This Week";
        if (entryDate >= today.AddDays(-30)) return "This Month";
        return date.ToString("MMMM yyyy");
    }

    private void DrawPromptHistoryListItem(RCCP_AIPromptHistory.PromptHistoryEntry entry) {
        bool isSelected = selectedPromptHistoryId == entry.id;
        bool isBulkSelected = selectedHistoryEntries.Contains(entry.id);

        // Item container with more padding and better margins
        GUIStyle itemStyle = new GUIStyle(isSelected ? RCCP_AIDesignSystem.PanelElevated : RCCP_AIDesignSystem.PanelRecessed) {
            padding = new RectOffset(12, 12, 10, 10),
            margin = new RectOffset(0, 0, 2, 2)
        };

        Color oldBg = GUI.backgroundColor;
        if (isSelected) {
            GUI.backgroundColor = AccentColor * 0.5f;
        } else if (isBulkSelected) {
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.BgSelected;
        } else {
            // Very subtle tint for unselected items to differentiate from the background
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.WithAlpha(Color.white, 0.05f);
        }

        EditorGUILayout.BeginVertical(itemStyle);

        // --- Row 1: Header Info ---
        EditorGUILayout.BeginHorizontal();

        // Bulk selection checkbox
        if (promptHistoryBulkMode) {
            bool newSelected = GUILayout.Toggle(isBulkSelected, "", GUILayout.Width(20));
            if (newSelected != isBulkSelected) {
                if (newSelected) selectedHistoryEntries.Add(entry.id);
                else selectedHistoryEntries.Remove(entry.id);
            }
        }

        // Favorite star
        GUIStyle starStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = entry.isFavorite ? RCCP_AIDesignSystem.Colors.Warning : RCCP_AIDesignSystem.Colors.TextDisabled },
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            alignment = TextAnchor.MiddleLeft
        };
        if (GUILayout.Button(entry.isFavorite ? "★" : "☆", starStyle, GUILayout.Width(24), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall))) {
            RCCP_AIPromptHistory.ToggleFavorite(entry.id);
        }

        // Panel badge
        GUIStyle badgeStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = AccentColor },
            fontStyle = FontStyle.Bold,
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Overflow
        };
        GUILayout.Label(entry.panelName, badgeStyle);

        RCCP_AIDesignSystem.Space(S5);

        // Status indicator
        if (entry.wasApplied) {
            GUILayout.Label("✓ Applied", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Success },
                fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
                alignment = TextAnchor.MiddleLeft
            }, GUILayout.ExpandWidth(false));
        } else {
            GUILayout.Label("○ Pending", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Warning },
                fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
                alignment = TextAnchor.MiddleLeft
            }, GUILayout.ExpandWidth(false));
        }

        GUILayout.FlexibleSpace();

        // Timestamp
        GUILayout.Label(FormatTimestamp(entry.timestamp), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            alignment = TextAnchor.MiddleRight
        });

        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S3);

        // --- Row 2: Prompt Preview ---
        GUIStyle promptStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            wordWrap = true,
            clipping = TextClipping.Clip,
            normal = { textColor = isSelected ? Color.white : RCCP_AIDesignSystem.Colors.TextPrimary },
            fontStyle = FontStyle.Normal
        };
        
        // Show truncated prompt text
        string displayPrompt = entry.userPrompt.Replace("\n", " ").Trim();
        if (displayPrompt.Length > 80) displayPrompt = displayPrompt.Substring(0, 80) + "...";
        
        GUIContent promptContent = new GUIContent(displayPrompt, entry.userPrompt);
        GUILayout.Label(promptContent, promptStyle);

        RCCP_AIDesignSystem.Space(S3);

        // --- Row 3: Footer Info ---
        EditorGUILayout.BeginHorizontal();

        // Vehicle name
        if (!string.IsNullOrEmpty(entry.vehicleName)) {
            GUIContent vehicleContent = new GUIContent($"🚗 {entry.vehicleName}", "Target Vehicle");
            GUILayout.Label(vehicleContent, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.TextSecondary, 0.1f) },
                fontSize = RCCP_AIDesignSystem.Typography.SizeSM
            });
        }

        GUILayout.FlexibleSpace();

        // Token count
        if (entry.tokenCount > 0) {
            GUILayout.Label($"~{entry.tokenCount:N0} tokens", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                fontSize = RCCP_AIDesignSystem.Typography.SizeXS,
                alignment = TextAnchor.MiddleRight
            });
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        GUI.backgroundColor = oldBg;

        // Draw separator line after each item except if it's selected (it has its own highlight)
        if (!isSelected) {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            Rect separatorRect = new Rect(lastRect.x + 10, lastRect.yMax + 1, lastRect.width - 20, 1);
            EditorGUI.DrawRect(separatorRect, RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.BorderLight, 0.5f));
        }

        // Handle click
        Rect itemRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition)) {
            if (!promptHistoryBulkMode) {
                selectedPromptHistoryId = entry.id;
            }
            Event.current.Use();
            Repaint();
        }
    }

    private void DrawPromptHistoryEntryDetails(RCCP_AIPromptHistory.PromptHistoryEntry entry) {
        if (entry == null) return;

        // Header with favorite toggle
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("ENTRY DETAILS", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold, normal = { textColor = Color.gray } });
        GUILayout.FlexibleSpace();

        // Favorite button
        GUIStyle favStyle = new GUIStyle(RCCP_AIDesignSystem.ButtonSecondary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            padding = new RectOffset(8, 8, 2, 2)
        };
        if (GUILayout.Button(entry.isFavorite ? "★ Favorited" : "☆ Favorite", favStyle, GUILayout.Width(90), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonInline))) {
            RCCP_AIPromptHistory.ToggleFavorite(entry.id);
        }

        RCCP_AIDesignSystem.Space(S2);

        // Export single entry
        if (GUILayout.Button(new GUIContent("📤", "Export as JSON"), GUILayout.Width(28), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonInline))) {
            ExportSingleEntry(entry);
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S4);

        promptHistoryDetailScrollPosition = EditorGUILayout.BeginScrollView(promptHistoryDetailScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // --- Metadata Card ---
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated, GUILayout.MinHeight(80));
        EditorGUILayout.BeginVertical(new GUIStyle() { padding = new RectOffset(12, 12, 10, 10) });

        // Row 1: Panel and Timestamp
        EditorGUILayout.BeginHorizontal();
        
        GUIStyle panelLabelStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            normal = { textColor = AccentColor }
        };
        GUILayout.Label(entry.panelName, panelLabelStyle);
        
        GUILayout.FlexibleSpace();
        
        GUILayout.Label(entry.timestamp, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = Color.gray },
            alignment = TextAnchor.MiddleRight
        });
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S5);

        // Row 2: Vehicle and Status
        EditorGUILayout.BeginHorizontal();
        
        if (!string.IsNullOrEmpty(entry.vehicleName)) {
            GUILayout.Label("🚗", GUILayout.Width(20));
            GUILayout.Label(entry.vehicleName, RCCP_AIDesignSystem.LabelPrimary);
            RCCP_AIDesignSystem.Space(S7);
        }

        // Status Badge
        string statusText = entry.wasApplied ? "✓ Applied" : "○ Pending";
        Color statusColor = entry.wasApplied ? RCCP_AIDesignSystem.Colors.Success : RCCP_AIDesignSystem.Colors.Warning;
        
        GUIStyle statusBadgeStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = statusColor },
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(6, 6, 2, 2)
        };
        GUILayout.Label(statusText, statusBadgeStyle);

        GUILayout.FlexibleSpace();

        // Token count and cost - only show cost when using own API key
        if (entry.tokenCount > 0) {
            bool showEntryCost = RCCP_AISettings.Instance != null && !RCCP_AISettings.Instance.useServerProxy;
            string tokenLabel = showEntryCost
                ? $"~{entry.tokenCount:N0} tokens  •  ~${entry.EstimatedCost:F4}"
                : $"~{entry.tokenCount:N0} tokens";
            GUILayout.Label(tokenLabel, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                alignment = TextAnchor.MiddleRight
            });
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S6);

        // --- User Prompt Section ---
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("YOUR PROMPT", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold, normal = { textColor = Color.gray } });
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall))) {
            EditorGUIUtility.systemCopyBuffer = entry.userPrompt;
            SetStatus("Prompt copied to clipboard", MessageType.Info);
        }
        EditorGUILayout.EndHorizontal();
        
        RCCP_AIDesignSystem.Space(S2);
        
        EditorGUILayout.BeginVertical(stepBoxStyle);
        var userPromptStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            wordWrap = true,
            padding = new RectOffset(10, 10, 10, 10),
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary }
        };
        EditorGUILayout.SelectableLabel(entry.userPrompt, userPromptStyle, GUILayout.MinHeight(40), GUILayout.ExpandHeight(false));
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S6);

        // --- AI Response Section ---
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("AI RESPONSE", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold, normal = { textColor = Color.gray } });
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Copy ▾", GUILayout.Width(60), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall))) {
            showCopyOptions = !showCopyOptions;
        }
        EditorGUILayout.EndHorizontal();

        if (showCopyOptions) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated, GUILayout.Width(140));
            if (GUILayout.Button("Copy All", RCCP_AIDesignSystem.ButtonSecondary)) {
                EditorGUIUtility.systemCopyBuffer = entry.fullAiResponse;
                SetStatus("Full response copied", MessageType.Info);
                showCopyOptions = false;
            }
            if (GUILayout.Button("Copy Prompt Only", RCCP_AIDesignSystem.ButtonSecondary)) {
                EditorGUIUtility.systemCopyBuffer = entry.userPrompt;
                SetStatus("Prompt copied", MessageType.Info);
                showCopyOptions = false;
            }
            if (GUILayout.Button("Copy as JSON", RCCP_AIDesignSystem.ButtonSecondary)) {
                EditorGUIUtility.systemCopyBuffer = RCCP_AIPromptHistory.ExportEntryToJson(entry.id);
                SetStatus("Entry copied as JSON", MessageType.Info);
                showCopyOptions = false;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        RCCP_AIDesignSystem.Space(S2);

        EditorGUILayout.BeginVertical(stepBoxStyle);
        DrawSyntaxHighlightedJson(entry.id, entry.fullAiResponse);
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S7);

        // --- Footer Actions ---
        EditorGUILayout.BeginHorizontal();

        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = AccentColor;
        if (GUILayout.Button("↻ REUSE THIS PROMPT", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero), GUILayout.ExpandWidth(true))) {
            SwitchToPanel(GetPanelTypeFromString(entry.panelType));
            userPrompt = entry.userPrompt;
            showPromptHistory = false;
        }
        GUI.backgroundColor = oldBg;

        RCCP_AIDesignSystem.Space(S5);

        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Error;
        if (GUILayout.Button("DELETE", GUILayout.Width(80), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero))) {
            if (EditorUtility.DisplayDialog("Delete Entry",
                "Are you sure you want to delete this entry?",
                "Delete", "Cancel")) {
                RCCP_AIPromptHistory.DeleteEntry(entry.id);
                selectedPromptHistoryId = null;
            }
        }
        GUI.backgroundColor = oldBg;

        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S5);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSyntaxHighlightedJson(string entryId, string json) {
        if (string.IsNullOrEmpty(json)) return;

        // Check if it looks like JSON
        bool isJson = json.TrimStart().StartsWith("{") || json.TrimStart().StartsWith("[");

        if (!jsonSectionFoldouts.ContainsKey(entryId)) {
            jsonSectionFoldouts[entryId] = false;
        }

        if (isJson) {
            // Collapsible section for full JSON
            EditorGUILayout.BeginHorizontal();
            jsonSectionFoldouts[entryId] = EditorGUILayout.Foldout(jsonSectionFoldouts[entryId], "Show Full JSON", true);
            EditorGUILayout.EndHorizontal();

            if (jsonSectionFoldouts[entryId]) {
                // Syntax highlighted JSON
                string highlighted = HighlightJsonSyntax(json);
                GUIStyle jsonStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                    richText = true,
                    wordWrap = true,
                    padding = new RectOffset(8, 8, 8, 8),
                    fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
                    normal = { background = jsonPreviewTexture }
                };

                EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);
                EditorGUILayout.LabelField(highlighted, jsonStyle, GUILayout.MinHeight(100));
                EditorGUILayout.EndVertical();
            } else {
                // Show preview
                string preview = json.Length > 150 ? json.Substring(0, 147) + "..." : json;
                GUIStyle previewStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                    fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
                    wordWrap = true,
                    normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
                };
                GUILayout.Label(preview, previewStyle);
            }
        } else {
            // Plain text response
            var jsonStyle = new GUIStyle(RCCP_AIDesignSystem.TextArea) {
                wordWrap = true,
                padding = new RectOffset(8, 8, 8, 8),
                fontSize = RCCP_AIDesignSystem.Typography.SizeBase
            };
            jsonStyle.normal.textColor = RCCP_AIDesignSystem.Colors.TextPrimary;
            EditorGUILayout.SelectableLabel(json, jsonStyle, GUILayout.MinHeight(100));
        }
    }

    /// <summary>
    /// Highlights differences between before and after states using rich text.
    /// Changed lines are highlighted with the specified color.
    /// </summary>
    private (string beforeHighlighted, string afterHighlighted) HighlightStateDiff(string beforeState, string afterState) {
        if (string.IsNullOrEmpty(beforeState) || string.IsNullOrEmpty(afterState)) {
            return (beforeState ?? "", afterState ?? "");
        }

        string[] beforeLines = beforeState.Split('\n');
        string[] afterLines = afterState.Split('\n');

        var beforeResult = new System.Text.StringBuilder();
        var afterResult = new System.Text.StringBuilder();

        // Colors for highlighting
        string changedColorBefore = "#FF9999";  // Light red for removed/changed in before
        string changedColorAfter = "#99FF99";   // Light green for added/changed in after
        string unchangedColor = "#888888";      // Gray for unchanged lines

        // Create dictionaries for quick lookup
        var beforeDict = new Dictionary<string, int>();
        var afterDict = new Dictionary<string, int>();

        for (int i = 0; i < beforeLines.Length; i++) {
            string trimmed = beforeLines[i].Trim();
            if (!string.IsNullOrEmpty(trimmed)) {
                beforeDict[trimmed] = i;
            }
        }

        for (int i = 0; i < afterLines.Length; i++) {
            string trimmed = afterLines[i].Trim();
            if (!string.IsNullOrEmpty(trimmed)) {
                afterDict[trimmed] = i;
            }
        }

        // Process before lines
        foreach (string line in beforeLines) {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) {
                beforeResult.AppendLine();
                continue;
            }

            // Check if this exact line exists in after
            if (afterDict.ContainsKey(trimmed)) {
                // Line unchanged - show in gray
                beforeResult.AppendLine($"<color={unchangedColor}>{EscapeRichText(line)}</color>");
            } else {
                // Line changed or removed - highlight in red
                beforeResult.AppendLine($"<color={changedColorBefore}><b>{EscapeRichText(line)}</b></color>");
            }
        }

        // Process after lines
        foreach (string line in afterLines) {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) {
                afterResult.AppendLine();
                continue;
            }

            // Check if this exact line exists in before
            if (beforeDict.ContainsKey(trimmed)) {
                // Line unchanged - show in gray
                afterResult.AppendLine($"<color={unchangedColor}>{EscapeRichText(line)}</color>");
            } else {
                // Line changed or added - highlight in green
                afterResult.AppendLine($"<color={changedColorAfter}><b>{EscapeRichText(line)}</b></color>");
            }
        }

        return (beforeResult.ToString().TrimEnd(), afterResult.ToString().TrimEnd());
    }

    /// <summary>
    /// Escapes characters that have special meaning in Unity rich text.
    /// </summary>
    private string EscapeRichText(string text) {
        if (string.IsNullOrEmpty(text)) return text;
        // Escape < and > to prevent rich text interpretation
        return text.Replace("<", "‹").Replace(">", "›");
    }

    private string HighlightJsonSyntax(string json) {
        if (string.IsNullOrEmpty(json)) return json;

        // Color definitions
        string keyColor = "#9CDCFE";      // Light blue for keys
        string stringColor = "#CE9178";    // Orange for strings
        string numberColor = "#B5CEA8";    // Light green for numbers
        string boolColor = "#569CD6";      // Blue for true/false
        string nullColor = "#569CD6";      // Blue for null
        string bracketColor = "#D4D4D4";   // Light gray for brackets

        System.Text.StringBuilder result = new System.Text.StringBuilder();
        bool inString = false;
        bool isKey = true;

        for (int i = 0; i < json.Length; i++) {
            char c = json[i];

            if (c == '"' && !IsEscapedQuoteAt(json, i)) {
                if (!inString) {
                    inString = true;
                    string color = isKey ? keyColor : stringColor;
                    result.Append($"<color={color}>\"");
                } else {
                    result.Append("\"</color>");
                    inString = false;
                }
            } else if (inString) {
                result.Append(c);
            } else if (c == ':') {
                result.Append(c);
                isKey = false;
            } else if (c == ',' || c == '{' || c == '}') {
                if (c == '{' || c == '}') {
                    result.Append($"<color={bracketColor}>{c}</color>");
                } else {
                    result.Append(c);
                }
                isKey = true;
            } else if (c == '[' || c == ']') {
                result.Append($"<color={bracketColor}>{c}</color>");
            } else if (char.IsDigit(c) || c == '-' || c == '.') {
                result.Append($"<color={numberColor}>{c}</color>");
            } else if (i + 3 < json.Length && json.Substring(i, 4) == "true") {
                result.Append($"<color={boolColor}>true</color>");
                i += 3;
            } else if (i + 4 < json.Length && json.Substring(i, 5) == "false") {
                result.Append($"<color={boolColor}>false</color>");
                i += 4;
            } else if (i + 3 < json.Length && json.Substring(i, 4) == "null") {
                result.Append($"<color={nullColor}>null</color>");
                i += 3;
            } else {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Checks if a quote character at the given index is escaped.
    /// Counts consecutive backslashes before the quote - odd count means escaped.
    /// </summary>
    private static bool IsEscapedQuoteAt(string s, int quoteIndex) {
        if (quoteIndex == 0) return false;
        int backslashCount = 0;
        int i = quoteIndex - 1;
        while (i >= 0 && s[i] == '\\') {
            backslashCount++;
            i--;
        }
        return backslashCount % 2 == 1;
    }

    private void ExportPromptHistory() {
        string path = EditorUtility.SaveFilePanel(
            "Export Prompt History",
            "",
            "RCCP_PromptHistory_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            "json"
        );

        if (!string.IsNullOrEmpty(path)) {
            string json = RCCP_AIPromptHistory.ExportToJson();
            System.IO.File.WriteAllText(path, json);
            SetStatus($"Exported {RCCP_AIPromptHistory.Count} entries to {System.IO.Path.GetFileName(path)}", MessageType.Info);
        }
    }

    private void ExportSingleEntry(RCCP_AIPromptHistory.PromptHistoryEntry entry) {
        string path = EditorUtility.SaveFilePanel(
            "Export Entry",
            "",
            $"RCCP_Prompt_{entry.id}",
            "json"
        );

        if (!string.IsNullOrEmpty(path)) {
            string json = RCCP_AIPromptHistory.ExportEntryToJson(entry.id);
            System.IO.File.WriteAllText(path, json);
            SetStatus($"Entry exported to {System.IO.Path.GetFileName(path)}", MessageType.Info);
        }
    }

    private string FormatTimestamp(string timestamp) {
        if (DateTime.TryParse(timestamp, out DateTime dt)) {
            TimeSpan diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return dt.ToString("MMM dd");
        }
        return timestamp;
    }

    private RCCP_AIPromptAsset.PanelType GetPanelTypeFromString(string panelType) {
        if (Enum.TryParse<RCCP_AIPromptAsset.PanelType>(panelType, out var result)) {
            return result;
        }
        return RCCP_AIPromptAsset.PanelType.VehicleCustomization;
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
