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

public partial class RCCP_AIAssistantWindow {

    #region UI Drawing - Vehicle History

    private void DrawHistoryPanel() {
        EditorGUILayout.BeginVertical();

        // Header
        RCCP_AIDesignSystem.Space(S6);
        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S7);

        if (GUILayout.Button("← Back", GUILayout.Width(70), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            showHistory = false;
            selectedHistoryIndex = -1;
        }

        RCCP_AIDesignSystem.Space(S6);
        GUILayout.Label("📋  Modification History", headerStyle);
        GUILayout.FlexibleSpace();
        
        // Vehicle Info Badge
        if (hasMultipleSelection) {
            GUILayout.Label("Multiple vehicles selected", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                fontStyle = FontStyle.Italic,
                normal = { textColor = RCCP_AIDesignSystem.Colors.Warning },
                alignment = TextAnchor.MiddleRight
            });
        } else if (HasRCCPController) {
            GUIStyle vehicleBadgeStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = AccentColor },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            GUILayout.Label($"Vehicle: {selectedController.gameObject.name}", vehicleBadgeStyle);
        } else {
            GUILayout.Label("No RCCP vehicle selected", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                fontStyle = FontStyle.Italic,
                normal = { textColor = Color.gray },
                alignment = TextAnchor.MiddleRight
            });
        }
        
        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S6);

        // Check for multiple selection - history only works for single vehicle
        if (hasMultipleSelection) {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(stepBoxStyle, GUILayout.MinWidth(300), GUILayout.MaxWidth(450));

            GUILayout.Label("Multiple Vehicles Selected", new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
                alignment = TextAnchor.MiddleCenter
            });
            RCCP_AIDesignSystem.Space(S5);
            GUILayout.Label("Please select a single vehicle to view its modification history.",
                new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                });

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            RCCP_AIDesignSystem.Space(S7);
            EditorGUILayout.EndVertical();
            return;
        }

        // Main content area
        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S7);

        RCCP_AIHistory history = selectedController?.GetComponent<RCCP_AIHistory>();

        if (history == null || history.Count == 0) {
            // No history message
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(stepBoxStyle, GUILayout.MinWidth(300), GUILayout.MaxWidth(450));

            GUILayout.Label("No History", new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
                alignment = TextAnchor.MiddleCenter
            });
            RCCP_AIDesignSystem.Space(S5);
            GUILayout.Label("AI modifications to this vehicle will appear here.\nUse the Vehicle Customization panel to make changes.",
                new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                });

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        } else {
            // Split view: list on left, details on right - responsive width
            
            // --- Left Column: Entry List ---
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(280), GUILayout.MaxWidth(420), GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Entries ({history.Count})", RCCP_AIDesignSystem.LabelHeader);
            GUILayout.FlexibleSpace();
            
            // Clear All Button
            if (GUILayout.Button("Clear All", GUILayout.Width(70), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonInline))) {
                if (EditorUtility.DisplayDialog("Clear History",
                    "Are you sure you want to clear all history entries for this vehicle?",
                    "Clear", "Cancel")) {
                    Undo.RecordObject(history, "Clear AI History");
                    history.ClearHistory();
                    EditorUtility.SetDirty(history);
                    selectedHistoryIndex = -1;
                }
            }
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S2);

            historyScrollPosition = EditorGUILayout.BeginScrollView(historyScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            for (int i = 0; i < history.Count; i++) {
                var entry = history.GetEntry(i);
                if (entry == null) continue;

                DrawHistoryListItem(entry, i);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            RCCP_AIDesignSystem.Space(S6);

            // --- Right Column: Details ---
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (selectedHistoryIndex >= 0 && selectedHistoryIndex < history.Count) {
                var entry = history.GetEntry(selectedHistoryIndex);
                DrawHistoryEntryDetails(entry, history, selectedHistoryIndex);
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

        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.EndVertical();
    }

    private void DrawHistoryListItem(RCCP_AIHistory.HistoryEntry entry, int index) {
        bool isSelected = index == selectedHistoryIndex;

        // Item container style matching Prompt History
        GUIStyle itemStyle = new GUIStyle(isSelected ? RCCP_AIDesignSystem.PanelElevated : RCCP_AIDesignSystem.PanelRecessed) {
            padding = new RectOffset(12, 12, 10, 10),
            margin = new RectOffset(0, 0, 2, 2)
        };

        Color oldBg = GUI.backgroundColor;
        if (isSelected) {
            GUI.backgroundColor = AccentColor * 0.5f;
        } else {
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.WithAlpha(Color.white, 0.05f);
        }

        EditorGUILayout.BeginVertical(itemStyle);

        // Header Row: Panel Name + Time
        EditorGUILayout.BeginHorizontal();
        
        // Panel Badge
        string panelName = string.IsNullOrEmpty(entry.panelType) ? "Modification" : entry.panelType;
        GUIStyle badgeStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = AccentColor },
            fontStyle = FontStyle.Bold,
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Overflow
        };
        GUILayout.Label(panelName, badgeStyle);

        GUILayout.FlexibleSpace();

        // Timestamp
        GUILayout.Label(FormatHistoryTimestamp(entry.timestamp), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            alignment = TextAnchor.MiddleRight
        });
        
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S3);

        // Prompt Preview
        string promptText = entry.userPrompt ?? "";
        // Truncate if too long
        string displayPrompt = promptText.Replace("\n", " ").Trim();
        if (displayPrompt.Length > 80) displayPrompt = displayPrompt.Substring(0, 80) + "...";

        GUIStyle promptStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            wordWrap = true,
            clipping = TextClipping.Clip,
            normal = { textColor = isSelected ? Color.white : RCCP_AIDesignSystem.Colors.TextPrimary },
            fontStyle = FontStyle.Normal
        };
        GUILayout.Label(displayPrompt, promptStyle);

        EditorGUILayout.EndVertical();

        GUI.backgroundColor = oldBg;

        // Separator (unless selected)
        if (!isSelected) {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            Rect separatorRect = new Rect(lastRect.x + 10, lastRect.yMax + 1, lastRect.width - 20, 1);
            EditorGUI.DrawRect(separatorRect, RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.BorderLight, 0.5f));
        }

        // Handle Click
        Rect itemRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition)) {
            selectedHistoryIndex = index;
            Event.current.Use();
            Repaint();
        }
    }

    private string FormatHistoryTimestamp(string timestampStr) {
        if (DateTime.TryParse(timestampStr, out DateTime dt)) {
            if (dt.Date == DateTime.Now.Date) return dt.ToString("HH:mm");
            if (dt.Date == DateTime.Now.AddDays(-1).Date) return "Yesterday " + dt.ToString("HH:mm");
            return dt.ToString("MMM dd");
        }
        return timestampStr;
    }

    private void DrawHistoryEntryDetails(RCCP_AIHistory.HistoryEntry entry, RCCP_AIHistory history, int index) {
        if (entry == null) return;

        // Header
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("ENTRY DETAILS", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold, normal = { textColor = Color.gray } });
        GUILayout.FlexibleSpace();
        
        // Restore/Delete buttons in header for quick access
        bool canRestore = entry.CanRestore && HasRCCPController;
        GUI.enabled = canRestore;
        if (GUILayout.Button("↶ Restore", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(80), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonInline))) {
             if (EditorUtility.DisplayDialog("Restore Vehicle State",
                "This will restore the vehicle to its state BEFORE this modification was applied.\n\nThis action will be recorded in history and can be undone.",
                "Restore", "Cancel")) {
                RestoreVehicleState(entry);
            }
        }
        GUI.enabled = true;

        RCCP_AIDesignSystem.Space(S2);

        if (GUILayout.Button("Delete", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(60), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonInline))) {
            Undo.RecordObject(history, "Delete AI History Entry");
            history.RemoveEntry(index);
            EditorUtility.SetDirty(history);
            selectedHistoryIndex = -1;
            Repaint();
            EditorGUILayout.EndHorizontal();  // Close layout before early return
            return;
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S4);

        historyDetailScrollPosition = EditorGUILayout.BeginScrollView(historyDetailScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // --- Metadata Card ---
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated, GUILayout.MinHeight(60));
        EditorGUILayout.BeginVertical(new GUIStyle() { padding = new RectOffset(12, 12, 10, 10) });

        EditorGUILayout.BeginHorizontal();
        GUIStyle panelLabelStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            normal = { textColor = AccentColor }
        };
        GUILayout.Label(string.IsNullOrEmpty(entry.panelType) ? "Modification" : entry.panelType, panelLabelStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label(entry.timestamp, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = Color.gray },
            alignment = TextAnchor.MiddleRight
        });
        EditorGUILayout.EndHorizontal();

        // Restore status message
        if (!canRestore) {
            RCCP_AIDesignSystem.Space(S2);
            string reason = !entry.CanRestore
                ? "(Restore unavailable - entry created before this feature)"
                : "(Select a vehicle to enable restore)";
            GUILayout.Label(reason, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Warning },
                fontStyle = FontStyle.Italic
            });
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S6);

        // --- User Prompt ---
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
        GUILayout.Label(entry.userPrompt ?? "(none)", new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            wordWrap = true,
            padding = new RectOffset(10, 10, 10, 10),
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary }
        });
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S6);

        // --- AI Explanation ---
        if (!string.IsNullOrEmpty(entry.explanation)) {
            EditorGUILayout.LabelField("AI EXPLANATION", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold, normal = { textColor = Color.gray } });
            RCCP_AIDesignSystem.Space(S2);
            EditorGUILayout.BeginVertical(stepBoxStyle);
            GUILayout.Label(entry.explanation, new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                wordWrap = true,
                padding = new RectOffset(10, 10, 10, 10),
                fontSize = RCCP_AIDesignSystem.Typography.SizeMD
            });
            EditorGUILayout.EndVertical();
            RCCP_AIDesignSystem.Space(S6);
        }

        // --- Before/After Diff ---
        EditorGUILayout.LabelField("CHANGES", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold, normal = { textColor = Color.gray } });
        RCCP_AIDesignSystem.Space(S2);

        EditorGUILayout.BeginHorizontal();

        // Get highlighted versions
        var (beforeHighlighted, afterHighlighted) = HighlightStateDiff(entry.beforeState, entry.afterState);

        // Style for diff display with rich text support
        var diffStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            wordWrap = true,
            richText = true
        };

        // Before state
        EditorGUILayout.BeginVertical(stepBoxStyle, GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Before", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Error, 0.2f) }
        });
        GUILayout.FlexibleSpace();
        GUILayout.Label("(changed = red)", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
            fontSize = RCCP_AIDesignSystem.Typography.SizeXS
        });
        EditorGUILayout.EndHorizontal();
        RCCP_AIDesignSystem.Space(S2);
        
        if (!string.IsNullOrEmpty(entry.beforeState)) {
            GUILayout.Label(beforeHighlighted, diffStyle);
        } else {
            GUILayout.Label("(not captured)", RCCP_AIDesignSystem.LabelSmall);
        }
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S2);

        // After state
        EditorGUILayout.BeginVertical(stepBoxStyle, GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("After", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Success, 0.2f) }
        });
        GUILayout.FlexibleSpace();
        GUILayout.Label("(changed = green)", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
            fontSize = RCCP_AIDesignSystem.Typography.SizeXS
        });
        EditorGUILayout.EndHorizontal();
        RCCP_AIDesignSystem.Space(S2);

        if (!string.IsNullOrEmpty(entry.afterState)) {
            GUILayout.Label(afterHighlighted, diffStyle);
        } else {
            GUILayout.Label("(not captured)", RCCP_AIDesignSystem.LabelSmall);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S6);

        // Applied JSON (only in developer mode)
        if (developerMode && !string.IsNullOrEmpty(entry.appliedJson)) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("APPLIED JSON", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold, normal = { textColor = Color.gray } });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy JSON", RCCP_AIDesignSystem.ButtonSecondary)) {
                GUIUtility.systemCopyBuffer = entry.appliedJson;
                SetStatus("JSON copied to clipboard!", MessageType.Info);
            }
            EditorGUILayout.EndHorizontal();
            
            RCCP_AIDesignSystem.Space(S2);
            
            EditorGUILayout.BeginVertical(stepBoxStyle);
            string jsonPreview = entry.appliedJson;
            if (jsonPreview.Length > 500) {
                jsonPreview = jsonPreview.Substring(0, 500) + "\n...";
            }
            GUILayout.Label(jsonPreview, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                wordWrap = true,
                padding = new RectOffset(10, 10, 10, 10),
                normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Success, 0.15f) }
            });
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void RestoreVehicleState(RCCP_AIHistory.HistoryEntry entry) {
        if (entry == null || !entry.CanRestore || !HasRCCPController) {
            SetStatus("Cannot restore: Invalid entry or no vehicle selected", MessageType.Error);
            return;
        }

        try {
            Undo.RegisterFullObjectHierarchyUndo(selectedController.gameObject, "RCCP AI Restore");

            // Capture current state BEFORE restoring (this becomes "before" state of restore entry)
            string stateBeforeRestore = RCCP_AIVehicleBuilder.CaptureVehicleState(selectedController);
            string jsonBeforeRestore = RCCP_AIVehicleBuilder.CaptureVehicleStateAsJson(selectedController);

            // Set history context for the restore operation
            RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
                panelType = "History Restore",
                userPrompt = $"↶ Restore to: {entry.timestamp}",
                explanation = $"Restored vehicle to previous state from entry: \"{entry.userPrompt}\"",
                appliedJson = entry.beforeStateJson
            };

            try {
                // Restore using the saved JSON state
                RCCP_AIVehicleBuilder.RestoreFromHistory(selectedController, entry.beforeStateJson);

                // Capture state AFTER restoring (this becomes "after" state of restore entry)
                string stateAfterRestore = RCCP_AIVehicleBuilder.CaptureVehicleState(selectedController);

                // Create history entry for the restore action
                var history = selectedController.GetComponent<RCCP_AIHistory>();
                if (history != null) {
                    var restoreEntry = new RCCP_AIHistory.HistoryEntry {
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        panelType = "History Restore",
                        userPrompt = $"↶ Restore to: {entry.timestamp}",
                        explanation = $"Restored vehicle to previous state from entry: \"{entry.userPrompt}\"",
                        beforeState = stateBeforeRestore,
                        afterState = stateAfterRestore,
                        beforeStateJson = jsonBeforeRestore,
                        appliedJson = entry.beforeStateJson
                    };

                    Undo.RecordObject(history, "Log Restore History");
                    history.AddEntry(restoreEntry);
                    EditorUtility.SetDirty(history);
                }

                SetStatus("Vehicle restored to previous state!", MessageType.Info);
                RefreshSelection();
            } finally {
                // Always clear context, even on exception
                RCCP_AIVehicleBuilder.CurrentContext = null;
            }
        } catch (Exception e) {
            SetStatus($"Restore failed: {e.Message}", MessageType.Error);
            Debug.LogError($"[RCCP AI] Restore error: {e}");
        }
    }

    private void DrawNoSettingsWarning() {
        RCCP_AIDesignSystem.Space(S8);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginVertical(GUILayout.Width(400));

        EditorGUILayout.BeginVertical(stepBoxStyle);

        GUILayout.Label("Setup Required", new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeXL,
            alignment = TextAnchor.MiddleCenter
        });

        RCCP_AIDesignSystem.Space(S6);

        GUILayout.Label("RCCP AI Settings asset not found.\n\nClick the button below to create default settings and prompts.",
            new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            });

        RCCP_AIDesignSystem.Space(S7);

        if (GUILayout.Button("Create AI Settings", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero))) {
            CreateDefaultSettings();
        }

        RCCP_AIDesignSystem.Space(S5);

        if (GUILayout.Button("Reload", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            LoadSettings();
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
