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
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;

namespace BoneCrackerGames.RCCP.AIAssistant {

public partial class RCCP_AIAssistantWindow {

    #region UI Drawing - Workflow

    // Spacing aliases for cleaner code
    private static int S2 => RCCP_AIDesignSystem.Spacing.Space2;  // 4px
    private static int S3 => RCCP_AIDesignSystem.Spacing.Space3;  // 6px
    private static int S4 => RCCP_AIDesignSystem.Spacing.Space4;  // 8px
    private static int S5 => RCCP_AIDesignSystem.Spacing.Space5;  // 12px
    private static int S6 => RCCP_AIDesignSystem.Spacing.Space6;  // 16px
    private static int S7 => RCCP_AIDesignSystem.Spacing.Space7;  // 24px
    private static int S8 => RCCP_AIDesignSystem.Spacing.Space8;  // 32px

    private void DrawWorkflow() {
        EditorGUILayout.BeginHorizontal();
        RCCP_AIDesignSystem.Space(S6);  // 16px side margin
        EditorGUILayout.BeginVertical();

        // Check if this is the Diagnostics panel (special handling - no AI needed)
        if (CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Diagnostics) {
            DrawDiagnosticsPanel();
            RCCP_AIDesignSystem.Space(S8);  // 32px bottom spacer
            EditorGUILayout.EndVertical();
            RCCP_AIDesignSystem.Space(S6);  // 16px side margin
            EditorGUILayout.EndHorizontal();
            return;
        }

        // Draw progress indicator at top
        DrawProgressIndicator();

        // Step 1: Select Target
        if (CurrentPrompt != null && (CurrentPrompt.requiresVehicle || CurrentPrompt.requiresRCCPController)) {
            DrawStep1_SelectTarget();
        }

        RCCP_AIDesignSystem.Space(S5);  // 12px before separator
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);  // 12px after separator

        // Step 2: Describe
        DrawStep2_Describe();

        // Step 3: Preview & Apply (only if we have a response)
        // For batch customization, check batchCustomizationResponses instead of aiResponse
        bool hasResponse = !string.IsNullOrEmpty(aiResponse);
        bool hasBatchCustomizationResponse = isBatchCustomization && batchCustomizationResponses.Count > 0;
        if (hasResponse || hasBatchCustomizationResponse) {
            RCCP_AIDesignSystem.Space(S5);  // 12px before separator
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S5);  // 12px after separator

            DrawStep3_Preview();
        }

        // Post-creation tools - show ONLY on Vehicle Creation panel after car is created
        bool isVehicleCreation = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        if (isVehicleCreation && HasRCCPController) {
            DrawPostCreationSetup();
        }

        RCCP_AIDesignSystem.Space(S8);  // 32px bottom spacer
        EditorGUILayout.EndVertical();
        RCCP_AIDesignSystem.Space(S6);  // 16px side margin
        EditorGUILayout.EndHorizontal();

        // Bottom separator for sticky context bar
        RCCP_AIDesignSystem.DrawSeparator(true);
    }

    /// <summary>
    /// Draws a visual progress indicator showing workflow step completion
    /// </summary>
    private void DrawProgressIndicator() {
        bool requiresTarget = CurrentPrompt != null && (CurrentPrompt.requiresVehicle || CurrentPrompt.requiresRCCPController);
        bool isVehicleCreation = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        bool isVehicleCustomization = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCustomization;
        bool isLightsPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;

        // Calculate step states - batch mode is valid for Vehicle Creation, Customization, or Lights
        // Include active batch operation (isBatchCustomization) even if selection changed
        bool isBatchModeValid = isVehicleCreation && hasMultipleSelection && batchVehicles.Count > 0;
        bool isBatchCustomizationValid = (isVehicleCustomization || isLightsPanel) && (isBatchCustomization || (hasMultipleSelection && batchCustomizationVehicles.Count > 0));
        bool anyBatchModeValid = isBatchModeValid || isBatchCustomizationValid;

        bool step1Complete = anyBatchModeValid || (!hasMultipleSelection && (!requiresTarget || (CurrentPrompt.requiresRCCPController ? HasRCCPController : (selectedVehicle != null && isSelectionInScene))));
        // In batch mode, step 2 is complete when all batch responses are received
        // For batch customization, check batchCustomizationResponses (each vehicle has its own response)
        bool step2Complete = isBatchModeValid
            ? (batchResponses.Count == batchVehicles.Count && batchVehicles.Count > 0)
            : (isBatchCustomizationValid ? (isBatchCustomization && batchCustomizationResponses.Count > 0) : !string.IsNullOrEmpty(aiResponse));
        bool step3Complete = changesApplied;

        // For vehicle creation, check if post-setup steps are relevant
        bool showPostSetup = isVehicleCreation && HasRCCPController;
        bool step4Complete = showPostSetup && HasWheelsAssigned();
        bool step5Complete = showPostSetup && HasBodyColliders();

        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);
        EditorGUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        // Build step list based on context
        List<(string name, bool complete, bool active)> steps = new List<(string, bool, bool)>();

        if (requiresTarget) {
            steps.Add(("Target", step1Complete, !step1Complete));
        }
        steps.Add(("Describe", step2Complete, step1Complete && !step2Complete));
        steps.Add(("Apply", step3Complete, step2Complete && !step3Complete));

        if (showPostSetup) {
            steps.Add(("Wheels", step4Complete, step3Complete && !step4Complete));
            steps.Add(("Colliders", step5Complete, step4Complete && !step5Complete));
        }

        // Draw each step
        for (int i = 0; i < steps.Count; i++) {
            var step = steps[i];
            DrawProgressStep(step.name, step.complete, step.active);

            // Draw connector line between steps
            if (i < steps.Count - 1) {
                DrawProgressConnector(step.complete);
            }
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S2);  // 4px after progress indicator
    }

    private void DrawProgressStep(string name, bool isComplete, bool isActive) {
        Color circleColor;
        Color textColor;
        string symbol;

        if (isComplete) {
            circleColor = DS.Success;
            textColor = DS.TextPrimary;
            symbol = "✓";
        } else if (isActive) {
            circleColor = DS.Accent;
            textColor = DS.TextPrimary;
            symbol = "●";
        } else {
            circleColor = DS.BgHover;
            textColor = DS.TextSecondary;
            symbol = "○";
        }

        EditorGUILayout.BeginVertical(GUILayout.MinWidth(50), GUILayout.MaxWidth(70));

        // Circle with symbol
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUIStyle symbolStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = circleColor }
        };
        GUILayout.Label(symbol, symbolStyle, GUILayout.Width(16), GUILayout.Height(RCCP_AIDesignSystem.Heights.Pill));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // Label
        GUIStyle labelStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = textColor },
            fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal
        };
        GUILayout.Label(name, labelStyle);

        EditorGUILayout.EndVertical();
    }

    private void DrawProgressConnector(bool isComplete) {
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space1);
        Rect lineRect = GUILayoutUtility.GetRect(20, 2, GUILayout.Width(20));
        lineRect.y += 6;
        EditorGUI.DrawRect(lineRect, isComplete ? DS.Success : DS.BgHover);
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space1);
    }

    private bool HasWheelsAssigned() {
        if (!HasRCCPController) return false;
        var axles = selectedController.GetComponentInChildren<RCCP_Axles>(true);
        if (axles == null) return false;
        var axleList = axles.GetComponentsInChildren<RCCP_Axle>(true);
        if (axleList == null || axleList.Length < 2) return false;

        // Check that at least 2 axles have proper wheel colliders with wheel models
        int validAxleCount = 0;
        foreach (var axle in axleList) {
            bool hasLeftWheel = axle.leftWheelCollider != null && axle.leftWheelModel != null;
            bool hasRightWheel = axle.rightWheelCollider != null && axle.rightWheelModel != null;
            if (hasLeftWheel && hasRightWheel) {
                validAxleCount++;
            }
        }
        return validAxleCount >= 2;
    }

    private bool HasBodyColliders() {
        if (!HasRCCPController) return false;
        var colliders = selectedController.GetComponentsInChildren<Collider>(true);

        // Minimum size threshold to ignore tiny colliders
        const float minSize = 0.1f;

        int validColliders = 0;
        foreach (var col in colliders) {
            // Skip wheel colliders
            if (col is WheelCollider) continue;

            // Skip trigger colliders
            if (col.isTrigger) continue;

            // Skip tiny colliders based on bounds size
            Vector3 size = col.bounds.size;
            if (size.x < minSize && size.y < minSize && size.z < minSize) continue;

            validColliders++;
        }
        return validColliders > 0;
    }

    /// <summary>
    /// Returns true if any step before the given step number still needs completion.
    /// Used to determine if a step should be highlighted as "current".
    /// </summary>
    private bool NeedsPriorStep(int stepNumber) {
        bool isVehicleCreation = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        bool isVehicleCustomization = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCustomization;
        bool isLightsPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;
        bool isBatchModeValid = isVehicleCreation && hasMultipleSelection && batchVehicles.Count > 0;
        // Include active batch operation (isBatchCustomization) even if selection changed
        bool isBatchCustomizationValid = (isVehicleCustomization || isLightsPanel) && (isBatchCustomization || (hasMultipleSelection && batchCustomizationVehicles.Count > 0));
        bool anyBatchModeValid = isBatchModeValid || isBatchCustomizationValid;

        // Step 1: Select Target (batch mode counts as complete)
        bool step1Complete = anyBatchModeValid || (CurrentPrompt != null && CurrentPrompt.requiresRCCPController
            ? HasRCCPController
            : (selectedVehicle != null && isSelectionInScene));

        if (stepNumber > 1 && !step1Complete) return true;

        // Step 2: Describe What You Want (AI generation)
        // In batch mode, complete when all responses received. Otherwise: AI response exists OR vehicle already has RCCP (bypassed)
        // For batch customization, check batchCustomizationResponses (each vehicle has its own response)
        bool step2Complete = isBatchModeValid
            ? (batchResponses.Count == batchVehicles.Count && batchVehicles.Count > 0)
            : (isBatchCustomizationValid ? (isBatchCustomization && batchCustomizationResponses.Count > 0) : (!string.IsNullOrEmpty(aiResponse) || HasRCCPController));

        if (stepNumber > 2 && !step2Complete) return true;

        // Step 3: Select Wheels
        bool step3Complete = HasWheelsAssigned();

        if (stepNumber > 3 && !step3Complete) return true;

        return false;
    }

    private void DrawStep1_SelectTarget() {
        // Handle ObjectPicker selection result
        if (Event.current.commandName == "ObjectSelectorClosed" &&
            EditorGUIUtility.GetObjectPickerControlID() == vehiclePickerControlID) {
            var picked = EditorGUIUtility.GetObjectPickerObject();
            if (picked != null) {
                if (picked is RCCP_CarController controller)
                    Selection.activeGameObject = controller.gameObject;
                else if (picked is GameObject go)
                    Selection.activeGameObject = go;
            }
            Event.current.Use();
        }

        bool isComplete = CurrentPrompt != null && CurrentPrompt.requiresRCCPController
            ? HasRCCPController
            : (selectedVehicle != null && isSelectionInScene);
        bool isCurrent = !isComplete;  // Step 1 is current when not complete

        // Generate summary for completed state
        string stepSummary = null;
        bool isVehicleCreationPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        bool isBatchModeActive = isVehicleCreationPanel && hasMultipleSelection && batchVehicles.Count > 0;

        if (isBatchModeActive) {
            // Batch mode summary
            stepSummary = "📦 " + batchVehicles.Count + " vehicles selected";
        } else if (isComplete && selectedVehicle != null) {
            string sizeSummary = GetMeshAnalysisSummary();
            if (!string.IsNullOrEmpty(sizeSummary) && sizeSummary.Contains("Size:")) {
                // Extract just dimensions
                string dims = sizeSummary.Replace("Size:", "").Trim();
                stepSummary = selectedVehicle.name + " (" + dims + ")";
            } else {
                stepSummary = selectedVehicle.name;
            }
        }

        DrawStepHeader("1", "Select Target", isComplete || isBatchModeActive, isCurrent, "▶", stepSummary);

        EditorGUILayout.BeginVertical(stepBoxStyle);

        EditorGUI.BeginChangeCheck();
        selectedVehicle = (GameObject)EditorGUILayout.ObjectField(
            CurrentPrompt.requiresRCCPController ? "RCCP Vehicle" : "Vehicle Model",
            selectedVehicle,
            typeof(GameObject),
            true
        );
        if (EditorGUI.EndChangeCheck()) {
            RefreshSelection();
        }

        // Multiple selection handling
        // Show batch UI when: multiple selection OR active batch operation (even if selection changed)
        bool isCustomizationPanel = CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCustomization;
        bool isLightsPanel = CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;
        bool supportsBatchMode = isCustomizationPanel || isLightsPanel;
        bool shouldShowBatchUI = hasMultipleSelection || (isBatchCustomization && batchCustomizationVehicles.Count > 0);
        if (shouldShowBatchUI) {
            if (isVehicleCreationPanel && batchVehicles.Count > 0) {
                // Show batch mode UI for Vehicle Creation
                DrawBatchModeUI();
                EditorGUILayout.EndVertical();
                RCCP_AIDesignSystem.Space(S5);
                return;
            } else if (supportsBatchMode && (batchCustomizationVehicles.Count > 0 || isBatchCustomization)) {
                // Show batch mode UI for Vehicle Customization and Lights
                // Show when: batch vehicles exist OR we're in active batch operation
                DrawBatchCustomizationUI(isLightsPanel ? "Batch Lights" : "Batch Customization");
                EditorGUILayout.EndVertical();
                RCCP_AIDesignSystem.Space(S5);
                return;
            } else if (hasMultipleSelection) {
                // Standard warning for other panels (only show when actually multiple selection, not batch mode)
                DrawWarningMessage("Multiple objects selected - please select only one");
                EditorGUILayout.EndVertical();
                RCCP_AIDesignSystem.Space(S5);
                return;
            }
        }

        // Status messages - only show when no valid target (collapsed state otherwise)
        // When isComplete, the context bar already shows target info
        if (!isComplete) {
            if (CurrentPrompt.requiresRCCPController) {
                if (!HasRCCPController) {
                    DrawInfoMessage("Select a vehicle with RCCP_CarController component");
                } else {
                    DrawSuccessMessage(selectedController.gameObject.name);
                }
            } else if (CurrentPrompt.requiresVehicle) {
                if (selectedVehicle == null) {
                    if (isVehicleCreationPanel)
                        DrawInfoMessage("Select the root GameObject of the model from the Scene");
                    else
                        DrawInfoMessage("Select a 3D model from the Scene");
                } else if (!isSelectionInScene) {
                    DrawWarningMessage("Drag the model into the Scene first");
                } else {
                    DrawSuccessMessage(selectedVehicle.name);

                    if (isVehicleCreationPanel && selectedVehicle.transform.parent != null) {
                        DrawWarningMessage("Selection has a parent. Make sure you selected the ROOT of the car.");
                    }

                    if (hasExistingRigidbodies) {
                        DrawWarningMessage($"Found {existingRigidbodyCount} Rigidbody(s) - will be removed");
                    }
                    if (hasExistingWheelColliders) {
                        DrawWarningMessage($"Found {existingWheelColliderCount} WheelCollider(s) - will be removed");
                    }
                    if (isPrefabInstance) {
                        DrawInfoMessage("Prefab will be unpacked automatically");
                    }

                    // Size warnings for single vehicle (legacy - now part of eligibility)
                    if (currentSizeWarnings.Count > 0 && currentEligibility == null) {
                        foreach (var warning in currentSizeWarnings) {
                            if (warning.level == SizeWarningLevel.Error) {
                                DrawSizeErrorMessage(warning.message);
                            } else if (warning.level == SizeWarningLevel.Warning) {
                                DrawWarningMessage(warning.message);
                            } else {
                                DrawInfoMessage(warning.message);
                            }
                        }
                    }

                    // Eligibility check panel for Vehicle Creation
                    if (isVehicleCreationPanel && currentEligibility != null) {
                        RCCP_AIDesignSystem.Space(S4);
                        DrawEligibilityCheckPanel(currentEligibility);
                    }
                }
            }
        }

        // Mesh analysis - collapsible with summary
        if (CurrentPrompt.includeMeshAnalysis && !string.IsNullOrEmpty(meshAnalysis)) {
            RCCP_AIDesignSystem.Space(S4);

            // Parse quick summary from mesh analysis
            string summaryLine = GetMeshAnalysisSummary();

            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated, GUILayout.ExpandHeight(false));

            EditorGUILayout.BeginHorizontal();
            meshAnalysisFoldout = EditorGUILayout.Foldout(meshAnalysisFoldout, "Mesh Analysis", true);
            GUILayout.Label(summaryLine, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
            });
            EditorGUILayout.EndHorizontal();

            if (meshAnalysisFoldout) {
                // Use scroll view with reasonable max height for proper scrolling
                GUIStyle scrollStyle = new GUIStyle(RCCP_AIDesignSystem.PanelElevated) {
                    padding = new RectOffset(6, 6, 4, 4),
                    margin = new RectOffset(0, 0, 2, 2)
                };
                meshAnalysisScrollPosition = EditorGUILayout.BeginScrollView(
                    meshAnalysisScrollPosition,
                    scrollStyle,
                    GUILayout.MinHeight(60),
                    GUILayout.MaxHeight(200),
                    GUILayout.ExpandWidth(true)
                );
                GUILayout.Label(meshAnalysis, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { wordWrap = true });
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
        RCCP_AIDesignSystem.Space(S5);
    }

    private void DrawStep2_Describe() {
        bool hasResponse = !string.IsNullOrEmpty(aiResponse);
        bool isVehicleCreation = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        bool isVehicleCustomization = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCustomization;
        bool isLightsPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;

        // Check if Step 1 is complete (or not required) - batch mode is valid for Vehicle Creation, Customization, or Lights
        // Include active batch operation (isBatchCustomization) even if selection changed
        bool isBatchModeValid = isVehicleCreation && hasMultipleSelection && batchVehicles.Count > 0;
        bool isBatchCustomizationValid = (isVehicleCustomization || isLightsPanel) && (isBatchCustomization || (hasMultipleSelection && batchCustomizationVehicles.Count > 0));
        bool anyBatchModeValid = isBatchModeValid || isBatchCustomizationValid;

        bool step1Complete = anyBatchModeValid || !hasMultipleSelection;
        if (step1Complete && !anyBatchModeValid && CurrentPrompt != null && (CurrentPrompt.requiresVehicle || CurrentPrompt.requiresRCCPController)) {
            step1Complete = CurrentPrompt.requiresRCCPController
                ? HasRCCPController
                : (selectedVehicle != null && isSelectionInScene);
        }

        // Step 2 is complete when we have an AI response
        // Step 2 is active when Step 1 is complete and we don't have a response yet
        bool isComplete = hasResponse;
        bool isActive = step1Complete && !hasResponse;

        // Check if the vehicle already has RCCP
        bool alreadyHasRCCP = HasRCCPController;

        if (isVehicleCreation && alreadyHasRCCP) {
            // Show warning instead of prompt area with action buttons
            // Mark as complete since the vehicle already has RCCP (creation was done)
            DrawStepHeader("2", "Describe What You Want", true, false);

            EditorGUILayout.BeginVertical(stepBoxStyle);
            EditorGUILayout.HelpBox(
                "This vehicle already has RCCP_CarController installed.",
                MessageType.Warning
            );

            RCCP_AIDesignSystem.Space(S4);

            Color oldBg1 = GUI.backgroundColor;
            GUI.backgroundColor = AccentColor;
            if (GUILayout.Button("  Switch to Vehicle Customization  ", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction))) {
                // Find Vehicle Customization prompt index and switch to it
                SwitchToPanel(RCCP_AIPromptAsset.PanelType.VehicleCustomization);
            }
            GUI.backgroundColor = oldBg1;

            EditorGUILayout.EndVertical();
            RCCP_AIDesignSystem.Space(S5);
            return;
        }

        // Show locked state if Step 1 not complete
        bool requiresStep1 = CurrentPrompt != null && (CurrentPrompt.requiresVehicle || CurrentPrompt.requiresRCCPController);
        if (requiresStep1 && !step1Complete) {
            DrawStepHeader("2", "Describe What You Want", false, false);

            EditorGUILayout.BeginVertical(stepBoxStyle);

            // Enhanced empty state with action buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(300));

            RCCP_AIDesignSystem.Space(S6);

            // Lock icon
            GUIStyle lockStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = RCCP_AIDesignSystem.Typography.Size3XL,
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextDisabled }
            };
            GUILayout.Label("🔒", lockStyle);

            // Message
            GUIStyle messageStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                wordWrap = true
            };
            string lockMessage = CurrentPrompt.requiresRCCPController
                ? "Select an RCCP vehicle to continue"
                : "Select a 3D model from the scene to continue";
            GUILayout.Label(lockMessage, messageStyle);

            RCCP_AIDesignSystem.Space(S5);

            // Action button - Select from Scene
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(CurrentPrompt.requiresRCCPController
                    ? "🎯 Select RCCP Vehicle..."
                    : "🎯 Select from Scene...",
                RCCP_AIDesignSystem.ButtonPrimary, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonAction), GUILayout.Width(180))) {
                // Show object picker dialog for vehicle selection
                vehiclePickerControlID = GUIUtility.GetControlID(FocusType.Passive);
                if (CurrentPrompt.requiresRCCPController) {
                    EditorGUIUtility.ShowObjectPicker<RCCP_CarController>(
                        selectedController, true, "", vehiclePickerControlID);
                } else {
                    EditorGUIUtility.ShowObjectPicker<GameObject>(
                        selectedVehicle, true, "", vehiclePickerControlID);
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Hint text
            RCCP_AIDesignSystem.Space(S4);
            GUIStyle tipStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextDisabled }
            };
            GUILayout.Label("💡 Tip: Or select directly in Hierarchy window", tipStyle);

            RCCP_AIDesignSystem.Space(S6);

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            RCCP_AIDesignSystem.Space(S5);
            return;
        }

        // Vision-based Light Detection button (only for Lights panel) - show prominently above prompt area
        if (CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights && HasRCCPController) {
            DrawVisionLightDetectionButton();
            RCCP_AIDesignSystem.Space(S4);
        }

        DrawStepHeader("2", "Describe What You Want", isComplete, isActive, "▶");

        EditorGUILayout.BeginVertical(stepBoxStyle);

        // Disable all input controls while processing a request
        EditorGUI.BeginDisabledGroup(isProcessing);

        // Vehicle Creation Mode Toggle: Quick Create / Custom Prompt
        // Only show for Vehicle Creation panel
        if (isVehicleCreation) {
            DrawCreationModeToggle();

            // If Quick Create mode is selected, draw that UI and return
            if (useQuickCreateMode) {
                DrawQuickCreateMode();
                EditorGUI.EndDisabledGroup();  // End isProcessing disabled group
                EditorGUILayout.EndVertical();
                RCCP_AIDesignSystem.Space(S5);
                return;
            }
        }

        // Prompt Mode Segment Control [Configure | Ask]
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Segment control styling
        Color configureActiveColor = RCCP_AIDesignSystem.Colors.Warning;  // Orange
        Color askActiveColor = RCCP_AIDesignSystem.Colors.Info;       // Blue
        Color inactiveColor = RCCP_AIDesignSystem.Colors.TextDisabled;
        Color textActiveColor = Color.white;
        Color textInactiveColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.TextSecondary, 0.1f);

        bool isConfigureMode = promptMode == RCCP_AIConfig.PromptMode.Request;
        bool isAskMode = promptMode == RCCP_AIConfig.PromptMode.Ask;

        // Note: isVehicleCreation is already defined at the top of this method
        // and is used to check if Ask mode should be disabled

        // Configure button (left side of segment)
        GUIStyle configureStyle = new GUIStyle(RCCP_AIDesignSystem.ButtonSmall) {
            normal = { textColor = isConfigureMode ? textActiveColor : textInactiveColor },
            fontStyle = isConfigureMode ? FontStyle.Bold : FontStyle.Normal,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(12, 12, 6, 6)
        };
        Color oldBgConfigure = GUI.backgroundColor;
        GUI.backgroundColor = isConfigureMode ? configureActiveColor : inactiveColor;
        if (GUILayout.Button(new GUIContent("Configure", "AI will return settings you can review and apply"), configureStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            if (promptMode != RCCP_AIConfig.PromptMode.Request) {
                promptMode = RCCP_AIConfig.PromptMode.Request;
                RCCP_AIEditorPrefs.PromptMode = (int)promptMode;
                userPrompt = "";
                GUI.FocusControl(null);
            }
        }
        GUI.backgroundColor = oldBgConfigure;

        GUILayout.Space(-1); // Overlap borders for connected look

        // Ask button (right side of segment) - disabled for VehicleCreation panel
        EditorGUI.BeginDisabledGroup(isVehicleCreation);
        GUIStyle askStyle = new GUIStyle(RCCP_AIDesignSystem.ButtonSmall) {
            normal = { textColor = isAskMode && !isVehicleCreation ? textActiveColor : textInactiveColor },
            fontStyle = isAskMode && !isVehicleCreation ? FontStyle.Bold : FontStyle.Normal,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(12, 12, 6, 6)
        };
        Color oldBgAsk = GUI.backgroundColor;
        GUI.backgroundColor = isAskMode && !isVehicleCreation ? askActiveColor : inactiveColor;
        string askTooltip = isVehicleCreation
            ? "Ask mode is not available for Vehicle Creation"
            : "AI will explain or answer questions (no changes)";
        if (GUILayout.Button(new GUIContent("Ask", askTooltip), askStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            if (promptMode != RCCP_AIConfig.PromptMode.Ask) {
                promptMode = RCCP_AIConfig.PromptMode.Ask;
                RCCP_AIEditorPrefs.PromptMode = (int)promptMode;
                userPrompt = "";
                GUI.FocusControl(null);
            }
        }
        GUI.backgroundColor = oldBgAsk;
        EditorGUI.EndDisabledGroup();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // Mode hint text
        string modeHint = isConfigureMode
            ? "Returns settings you can review and apply"
            : "Returns advice only (no changes)";
        GUIStyle modeHintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.6f) }
        };
        EditorGUILayout.LabelField(modeHint, modeHintStyle);

        RCCP_AIDesignSystem.Space(S2);

        // Text input
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelRecessed);

        GUIStyle textAreaStyle = new GUIStyle(RCCP_AIDesignSystem.TextArea) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            wordWrap = true,
            padding = RCCP_AIDesignSystem.Spacing.Uniform(RCCP_AIDesignSystem.Spacing.Space4)
        };
        textAreaStyle.normal.textColor = DS.TextPrimary;
        textAreaStyle.focused.textColor = DS.TextPrimary;
        textAreaStyle.active.textColor = DS.TextPrimary;

        EditorGUILayout.BeginHorizontal();

        // Text area with placeholder
        Rect textAreaRect = GUILayoutUtility.GetRect(GUIContent.none, textAreaStyle, GUILayout.MinHeight(80), GUILayout.ExpandWidth(true));

        // Draw colored outline when auto-apply is enabled (outside the text area)
        if (autoApply) {
            Color outlineColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Warning, 0.35f); // Bright orange - matches Generate button
            Rect borderRect = new Rect(textAreaRect.x - 2, textAreaRect.y - 2, textAreaRect.width + 4, textAreaRect.height + 4);
            RCCP_AIDesignSystem.DrawRoundedRect(borderRect, outlineColor, 4);
        }

        // Draw brief green glow when quick prompt was just inserted
        double timeSinceInsert = EditorApplication.timeSinceStartup - quickPromptInsertTime;
        if (timeSinceInsert < 0.5f) {
            float glowAlpha = (float)(1f - (timeSinceInsert / 0.5f)) * 0.4f;
            Color glowColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Success, glowAlpha); // Green glow
            Rect glowRect = new Rect(textAreaRect.x - 2, textAreaRect.y - 2, textAreaRect.width + 4, textAreaRect.height + 4);
            RCCP_AIDesignSystem.DrawRoundedRect(glowRect, glowColor, 4);
            Repaint(); // Continue animating
        }

        // Set control name so we can focus it from quick prompts
        GUI.SetNextControlName("PromptTextArea");
        userPrompt = EditorGUI.TextArea(textAreaRect, userPrompt, textAreaStyle);

        // Enforce character limit
        if (RCCP_AISettings.Instance != null && userPrompt.Length > RCCP_AISettings.Instance.maxPromptLength)
            userPrompt = userPrompt.Substring(0, RCCP_AISettings.Instance.maxPromptLength);

        // Draw placeholder if empty and not focused
        if (string.IsNullOrEmpty(userPrompt)) {
            GUIStyle placeholderStyle = new GUIStyle(textAreaStyle) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.7f) },
                fontStyle = FontStyle.Italic
            };
            // Use placeholder from ScriptableObject or default
            string placeholder = CurrentPrompt != null && !string.IsNullOrEmpty(CurrentPrompt.placeholderText)
                ? CurrentPrompt.placeholderText
                : "Describe your ideal vehicle (e.g., '400hp drift car with soft suspension')";
            EditorGUI.LabelField(textAreaRect, placeholder, placeholderStyle);
        }

        // Vertical button column for clear and recent
        EditorGUILayout.BeginVertical(GUILayout.Width(26));

        // Clear prompt button (only show if there's text)
        if (!string.IsNullOrEmpty(userPrompt)) {
            if (GUILayout.Button(new GUIContent("✕", "Clear prompt"), GUILayout.Width(24), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
                userPrompt = "";
                GUI.FocusControl(null);
            }
        } else {
            RCCP_AIDesignSystem.Space(S7);
        }

        // Recent prompts button
        if (recentPrompts.Count > 0) {
            if (GUILayout.Button(new GUIContent("🕐", "Recent prompts"), GUILayout.Width(24), GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
                showRecentPrompts = !showRecentPrompts;
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        // Character count and estimated tokens row
        EditorGUILayout.BeginHorizontal();
        int charCount = string.IsNullOrEmpty(userPrompt) ? 0 : userPrompt.Length;
        int maxChars = RCCP_AISettings.Instance != null ? RCCP_AISettings.Instance.maxPromptLength : 1000;
        int wordCount = string.IsNullOrEmpty(userPrompt) ? 0 : userPrompt.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        int estimatedTokens = (int)(charCount / 3.5f); // Rough token estimate

        GUIStyle counterStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall);
        if (charCount >= maxChars)
            counterStyle.normal.textColor = Color.Lerp(DS.TextSecondary, Color.red, 0.7f);

        GUILayout.Label($"{charCount}/{maxChars} chars · {wordCount} words · ~{estimatedTokens} tokens", counterStyle, GUILayout.MinWidth(200), GUILayout.ExpandWidth(false));

        GUILayout.FlexibleSpace();

        // Cost estimate (model-aware) - only show when using own API key
        if (CurrentPrompt != null && !string.IsNullOrEmpty(userPrompt)) {
            string modelName = GetCurrentModelDisplayName();
            Color modelColor = GetModelColor(modelName);

            // Only show cost estimate when using own API key (not server proxy)
            bool showCost = settings != null && !settings.useServerProxy;

            if (showCost) {
                int promptTokens = CurrentPrompt.EstimatedTokens + (int)(userPrompt.Length / 3.5f);
                float estimatedCost = EstimateRequestCost(promptTokens);

                GUIStyle costStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = modelColor },
                    alignment = TextAnchor.MiddleRight
                };
                GUILayout.Label(new GUIContent($"{modelName} ~${estimatedCost:F4}",
                    $"Model: {modelName}\nEstimated cost: ~{promptTokens} input tokens"),
                    costStyle);
                RCCP_AIDesignSystem.Space(S5);
            } else {
                // Just show model name without cost when using server proxy
                GUIStyle modelOnlyStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = modelColor },
                    alignment = TextAnchor.MiddleRight
                };
                GUILayout.Label(new GUIContent(modelName, $"Model: {modelName}"), modelOnlyStyle);
                RCCP_AIDesignSystem.Space(S5);
            }
        }

        // Keyboard shortcut hint
        GUIStyle hintStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.TextDisabled },
            fontStyle = FontStyle.Italic
        };

        string shortcutHint = (settings != null && settings.sendOnEnter)
            ? "Enter to generate (Shift+Enter for new line)"
            : "Ctrl+Enter to generate";
        GUILayout.Label(shortcutHint, hintStyle);
        EditorGUILayout.EndHorizontal();

        // Info text about vehicle-related prompts
        GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.6f) },
            fontStyle = FontStyle.Italic,
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM
        };
        GUILayout.Label("This assistant only handles vehicle setup and tuning requests. Off-topic prompts may result in incorrect configurations.", infoStyle);

        // Recent prompts dropdown
        if (showRecentPrompts && recentPrompts.Count > 0) {
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
            RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.Card);
            GUILayout.Label("Recent prompts:", RCCP_AIDesignSystem.LabelSmall);

            // Clipped button style - handles variable width text properly
            GUIStyle clippedButtonStyle = new GUIStyle(RCCP_AIDesignSystem.ButtonSmall) {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft
            };

            foreach (var recent in recentPrompts) {
                // Use GUIContent with tooltip for full text on hover
                GUIContent content = new GUIContent(recent, recent);
                if (GUILayout.Button(content, clippedButtonStyle, GUILayout.ExpandWidth(true))) {
                    userPrompt = recent;
                    showRecentPrompts = false;
                    GUI.FocusControl(null);
                }
            }
            RCCP_AIDesignSystem.EndPanel();
        }

        RCCP_AIDesignSystem.EndPanel();

        // Quick prompts as chips (with shuffle support) - only show if enabled in settings
        if (quickPromptDisplayCount > 0 && CurrentPrompt != null && CurrentPrompt.examplePrompts != null && CurrentPrompt.examplePrompts.Length > 0) {
            // Initialize quick prompts if needed
            InitializeQuickPrompts();

            RCCP_AIDesignSystem.Space(S4);

            // Collapsible header row
            EditorGUILayout.BeginHorizontal();

            // Foldout arrow and label
            quickPromptsFoldout = EditorGUILayout.Foldout(quickPromptsFoldout, "Quick prompts:", true);

            GUILayout.FlexibleSpace();

            // Smart suggestion based on vehicle size
            if (quickPromptsFoldout && selectedVehicle != null && !string.IsNullOrEmpty(meshAnalysis)) {
                string smartSuggestion = GetSmartSuggestion();
                if (!string.IsNullOrEmpty(smartSuggestion)) {
                    GUIStyle suggestionStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                        normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Warning, 0.2f) },
                        fontStyle = FontStyle.Italic,
                        wordWrap = true
                    };
                    GUILayout.Label(new GUIContent($"💡 Try: {smartSuggestion}", "Suggested based on vehicle size"), suggestionStyle);
                    RCCP_AIDesignSystem.Space(S4);
                }
            }

            // Only show shuffle button if there are more prompts than we display
            if (CurrentPrompt.examplePrompts.Length > quickPromptDisplayCount) {
                if (GUILayout.Button(new GUIContent("🔀", "Show different prompts"), GUILayout.Width(28), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall))) {
                    ShuffleQuickPrompts();
                }
            }
            EditorGUILayout.EndHorizontal();

            // Only show prompts if foldout is open
            if (quickPromptsFoldout) {
                // Use a flow layout for chips
                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();

                float currentRowWidth = 0f;
                float maxRowWidth = position.width - sidebarWidth - 80f; // Account for margins

                // Check if current user prompt matches any quick prompt
                string normalizedUserPrompt = string.IsNullOrEmpty(userPrompt) ? "" : userPrompt.Trim().ToLower();

                // Display only the shuffled subset of prompts
                // Maximum chip width to prevent overly long chips
                const float maxChipWidth = 280f;

                foreach (int promptIndex in displayedQuickPromptIndices) {
                    if (promptIndex < 0 || promptIndex >= CurrentPrompt.examplePrompts.Length) continue;

                    string example = CurrentPrompt.examplePrompts[promptIndex];

                    // Check if this prompt matches user input
                    bool isMatch = !string.IsNullOrEmpty(normalizedUserPrompt) &&
                                   example.ToLower().Contains(normalizedUserPrompt);
                    bool isExactMatch = !string.IsNullOrEmpty(normalizedUserPrompt) &&
                                        example.Trim().ToLower() == normalizedUserPrompt;

                    // Highlight matching prompt (use cached textures to avoid memory leaks)
                    GUIStyle currentChipStyle = new GUIStyle(chipStyle) {
                        clipping = TextClipping.Clip  // Use proper clipping instead of manual truncation
                    };
                    if (isExactMatch) {
                        currentChipStyle.normal.background = chipExactMatchTexture;
                        currentChipStyle.normal.textColor = Color.white;
                    } else if (isMatch) {
                        currentChipStyle.normal.background = chipPartialMatchTexture;
                        currentChipStyle.normal.textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Warning, 0.3f);
                    }

                    // Calculate actual chip width using style
                    GUIContent measureContent = new GUIContent(example);
                    float chipWidth = Mathf.Min(currentChipStyle.CalcSize(measureContent).x + 8f, maxChipWidth);

                    // Start new row if chip doesn't fit
                    if (currentRowWidth + chipWidth > maxRowWidth && currentRowWidth > 0) {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        currentRowWidth = 0f;
                    }

                    // Show full prompt as tooltip on hover
                    string tooltipExtra = isExactMatch ? " (Current selection)" : isMatch ? " (Partial match)" : "";
                    GUIContent chipContent = new GUIContent(example, example + tooltipExtra);

                    // Use GUILayout.Button with max width for proper clipping
                    if (GUILayout.Button(chipContent, currentChipStyle, GUILayout.MaxWidth(maxChipWidth))) {
                        // Clear focus first so TextArea picks up the new value
                        // (TextArea maintains internal buffer when focused that overrides backing variable)
                        GUI.FocusControl(null);
                        userPrompt = example;
                        // Track insertion time for brief visual feedback
                        quickPromptInsertTime = EditorApplication.timeSinceStartup;
                        Repaint();
                    }

                    currentRowWidth += chipWidth;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        RCCP_AIDesignSystem.Space(S6);

        // Generate button row
        EditorGUILayout.BeginHorizontal();

        // Only show auto-apply in Request mode (Ask mode doesn't have anything to apply)
        if (promptMode == RCCP_AIConfig.PromptMode.Request) {
            autoApply = GUILayout.Toggle(autoApply, " Auto-apply after generation", GUILayout.MinWidth(180), GUILayout.ExpandWidth(false));
        } else {
            // Placeholder to maintain layout when auto-apply is hidden
            GUILayout.Space(180);
        }

        GUILayout.FlexibleSpace();

        bool canGenerate = !isProcessing && !string.IsNullOrEmpty(userPrompt) && HasValidAuth;

        // Check for batch mode (Vehicle Creation with multiple selection)
        // Note: isVehicleCreation, isVehicleCustomization, isLightsPanel are already defined at the top of this method
        bool isBatchMode = isVehicleCreation && hasMultipleSelection && batchVehicles.Count > 0;
        // Include active batch operation (isBatchCustomization) even if selection changed
        // Lights panel also supports batch mode like customization
        bool isBatchCustomizationMode = (isVehicleCustomization || isLightsPanel) && (isBatchCustomization || (hasMultipleSelection && batchCustomizationVehicles.Count > 0));

        // Allow batch modes, block other multi-selection
        if (hasMultipleSelection && !isBatchMode && !isBatchCustomizationMode) canGenerate = false;
        if (!isBatchMode && !isBatchCustomizationMode && CurrentPrompt != null && CurrentPrompt.requiresVehicle && selectedVehicle == null) canGenerate = false;
        if (!isBatchMode && !isBatchCustomizationMode && CurrentPrompt != null && CurrentPrompt.requiresVehicle && !CurrentPrompt.requiresRCCPController && !isSelectionInScene) canGenerate = false;
        if (!isBatchCustomizationMode && CurrentPrompt != null && CurrentPrompt.requiresRCCPController && !HasRCCPController) canGenerate = false;

        EditorGUI.BeginDisabledGroup(!canGenerate);

        // Generate/Ask button with accent color and loading animation
        Color oldBg = GUI.backgroundColor;
        if (canGenerate) {
            // Blue for Ask mode, Orange for Request mode
            GUI.backgroundColor = promptMode == RCCP_AIConfig.PromptMode.Ask
                ? RCCP_AIDesignSystem.Colors.Info    // Blue accent
                : RCCP_AIDesignSystem.Colors.Warning;   // Orange accent
        }

        string generateText;
        if (isProcessing) {
            // Animated loading dots
            int dots = (int)(EditorApplication.timeSinceStartup * 2) % 4;
            string dotStr = new string('.', dots);
            if (isBatchProcessing) {
                generateText = $"⏳  Processing {currentBatchIndex + 1}/{batchVehicles.Count}{dotStr}";
            } else {
                generateText = promptMode == RCCP_AIConfig.PromptMode.Ask
                    ? $"⏳  Asking{dotStr}"
                    : $"⏳  Generating{dotStr}";
            }
        } else if (isBatchMode) {
            generateText = $"✨  Generate All ({batchVehicles.Count})";
        } else {
            // Different button text based on mode
            generateText = promptMode == RCCP_AIConfig.PromptMode.Ask
                ? "💬  Ask"
                : "✨  Generate";
        }

        string tooltipShortcut = (settings != null && settings.sendOnEnter) ? "Enter" : "Ctrl+Enter";
        if (GUILayout.Button(new GUIContent(generateText, tooltipShortcut), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge), GUILayout.MinWidth(140))) {
            Generate();
        }

        GUI.backgroundColor = oldBg;
        EditorGUI.EndDisabledGroup();  // End canGenerate disabled group

        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();  // End isProcessing disabled group

        // Progress bar during processing
        if (isProcessing) {
            RCCP_AIDesignSystem.Space(S4);
            DrawProcessingProgressBar();
        }

        // AI disclaimer text
        RCCP_AIDesignSystem.Space(S3);
        GUIStyle disclaimerStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.TextSecondary, 0.5f) },
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            wordWrap = true
        };
        GUILayout.Label("AI can make mistakes. Always review before applying.", disclaimerStyle);

        EditorGUILayout.EndVertical();
        RCCP_AIDesignSystem.Space(S5);
    }

    /// <summary>
    /// Draws the Quick Create / Custom Prompt mode toggle for Vehicle Creation panel.
    /// Located at the top of Step 2.
    /// </summary>
    private void DrawCreationModeToggle() {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Segment control styling
        Color quickCreateActiveColor = RCCP_AIDesignSystem.Colors.Success;  // Green
        Color customPromptActiveColor = RCCP_AIDesignSystem.Colors.Warning;  // Orange
        Color inactiveColor = RCCP_AIDesignSystem.Colors.TextDisabled;
        Color textActiveColor = Color.white;
        Color textInactiveColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.TextSecondary, 0.1f);

        // Quick Create button (left side of segment)
        GUIStyle quickCreateStyle = new GUIStyle(RCCP_AIDesignSystem.ButtonSmall) {
            normal = { textColor = useQuickCreateMode ? textActiveColor : textInactiveColor },
            fontStyle = useQuickCreateMode ? FontStyle.Bold : FontStyle.Normal,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(12, 12, 6, 6)
        };
        Color oldBgQuick = GUI.backgroundColor;
        GUI.backgroundColor = useQuickCreateMode ? quickCreateActiveColor : inactiveColor;
        if (GUILayout.Button(new GUIContent("Quick Create", "One-click vehicle creation based on detected type"), quickCreateStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            if (!useQuickCreateMode) {
                useQuickCreateMode = true;
                RCCP_AIEditorPrefs.UseQuickCreateMode = true;
                GUI.FocusControl(null);
            }
        }
        GUI.backgroundColor = oldBgQuick;

        GUILayout.Space(-1); // Overlap borders for connected look

        // Custom Prompt button (right side of segment)
        GUIStyle customPromptStyle = new GUIStyle(RCCP_AIDesignSystem.ButtonSmall) {
            normal = { textColor = !useQuickCreateMode ? textActiveColor : textInactiveColor },
            fontStyle = !useQuickCreateMode ? FontStyle.Bold : FontStyle.Normal,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(12, 12, 6, 6)
        };
        Color oldBgCustom = GUI.backgroundColor;
        GUI.backgroundColor = !useQuickCreateMode ? customPromptActiveColor : inactiveColor;
        if (GUILayout.Button(new GUIContent("Custom Prompt", "Enter custom description for vehicle configuration"), customPromptStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
            if (useQuickCreateMode) {
                useQuickCreateMode = false;
                RCCP_AIEditorPrefs.UseQuickCreateMode = false;
                GUI.FocusControl(null);
            }
        }
        GUI.backgroundColor = oldBgCustom;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S4);
    }

    /// <summary>
    /// Draws the Quick Create mode UI with detected vehicle type preview and single Create button.
    /// </summary>
    private void DrawQuickCreateMode() {
        // Get detected vehicle info
        string vehicleType = GetDetectedVehicleType();
        string dimensions = GetDetectionSummary();
        string nameHint = GetVehicleNameHint();

        // Vehicle Type Preview Box
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        // Header
        GUIStyle headerStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
            fontStyle = FontStyle.Normal,
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM
        };
        GUILayout.Label("Detected Vehicle Type", headerStyle);

        RCCP_AIDesignSystem.Space(S2);

        // Vehicle type (large, prominent)
        GUIStyle typeStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            fontStyle = FontStyle.Bold,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Success }
        };
        GUILayout.Label(vehicleType, typeStyle);

        // Details row
        RCCP_AIDesignSystem.Space(S2);

        GUIStyle detailStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM
        };

        if (!string.IsNullOrEmpty(dimensions)) {
            GUILayout.Label($"Dimensions: {dimensions}", detailStyle);
        }

        if (!string.IsNullOrEmpty(nameHint)) {
            GUILayout.Label($"Model: \"{nameHint}\"", detailStyle);
        }

        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S5);

        // Create Vehicle button
        bool canGenerate = !isProcessing && selectedVehicle != null && isSelectionInScene && HasValidAuth;

        EditorGUI.BeginDisabledGroup(!canGenerate);

        Color oldBg = GUI.backgroundColor;
        if (canGenerate) {
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Success;  // Green for Quick Create
        }

        string buttonText;
        if (isProcessing) {
            int dots = (int)(EditorApplication.timeSinceStartup * 2) % 4;
            string dotStr = new string('.', dots);
            buttonText = $"⏳  Creating{dotStr}";
        } else {
            buttonText = "🚗  Create Vehicle";
        }

        // Large prominent button
        if (GUILayout.Button(buttonText, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge + 8))) {
            GenerateQuickCreate();
        }

        GUI.backgroundColor = oldBg;
        EditorGUI.EndDisabledGroup();

        RCCP_AIDesignSystem.Space(S4);

        // Helper text
        GUIStyle helperStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.TextSecondary, 0.7f) },
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
        GUILayout.Label("AI will configure drivetrain, suspension, and handling based on detected vehicle type", helperStyle);

        RCCP_AIDesignSystem.Space(S3);

        // Auto-apply toggle
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        autoApply = GUILayout.Toggle(autoApply, " Auto-apply after generation", GUILayout.MinWidth(180), GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // Progress bar during processing
        if (isProcessing) {
            RCCP_AIDesignSystem.Space(S4);
            DrawProcessingProgressBar();
        }

        // AI disclaimer text
        RCCP_AIDesignSystem.Space(S3);
        GUIStyle disclaimerStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.TextSecondary, 0.5f) },
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            wordWrap = true
        };
        GUILayout.Label("AI can make mistakes. Always review before applying.", disclaimerStyle);
    }

    /// <summary>
    /// Draws an animated progress bar during AI request processing.
    /// The bar fills up over the timeout duration to give visual feedback.
    /// </summary>
    private void DrawProcessingProgressBar() {
        float elapsed = (float)EditorApplication.timeSinceStartup - requestStartTime;
        float progress = Mathf.Clamp01(elapsed / RequestTimeoutSeconds);

        // Calculate remaining time
        float remainingTime = Mathf.Max(0, RequestTimeoutSeconds - elapsed);

        // Progress bar container with optional pulsing background
        Color originalBgColor = GUI.backgroundColor;
        if (enableAnimations) {
            float pulseIntensity = processingPulse * 0.15f;
            GUI.backgroundColor = new Color(
                AccentColor.r * pulseIntensity + originalBgColor.r * (1f - pulseIntensity),
                AccentColor.g * pulseIntensity + originalBgColor.g * (1f - pulseIntensity),
                AccentColor.b * pulseIntensity + originalBgColor.b * (1f - pulseIntensity),
                originalBgColor.a
            );
        }

        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        if (enableAnimations) {
            GUI.backgroundColor = originalBgColor;
        }

        // Status text with remaining time (with pulsing color)
        EditorGUILayout.BeginHorizontal();
        Color statusTextColor = enableAnimations
            ? Color.Lerp(AccentColor, Color.white, processingPulse * 0.3f)
            : AccentColor;
        GUILayout.Label("⏳ Generating configuration...", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = statusTextColor },
            fontStyle = FontStyle.Bold
        });
        GUILayout.FlexibleSpace();
        if (developerMode) {
            GUILayout.Label($"{remainingTime:F0}s remaining", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.TextSecondary, 0.1f) }
            });
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        // Draw custom progress bar
        Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(RCCP_AIDesignSystem.Heights.ProgressBarThick), GUILayout.ExpandWidth(true));

        // Background
        EditorGUI.DrawRect(progressRect, RCCP_AIDesignSystem.Colors.BgRecessed);

        // Filled portion with gradient effect
        Rect filledRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height);

        // Color transitions from green to orange to red as time progresses
        Color progressColor;
        if (progress < 0.5f) {
            progressColor = Color.Lerp(RCCP_AIDesignSystem.Colors.Success, AccentColor, progress * 2f);
        } else {
            progressColor = Color.Lerp(AccentColor, RCCP_AIDesignSystem.Colors.Error, (progress - 0.5f) * 2f);
        }

        // Add pulsing brightness to the progress bar
        if (enableAnimations) {
            float brightnessPulse = 1f + processingPulse * 0.2f;
            progressColor = new Color(
                Mathf.Clamp01(progressColor.r * brightnessPulse),
                Mathf.Clamp01(progressColor.g * brightnessPulse),
                Mathf.Clamp01(progressColor.b * brightnessPulse),
                progressColor.a
            );
        }

        EditorGUI.DrawRect(filledRect, progressColor);

        // Animated shimmer effect on progress bar
        if (enableAnimations && filledRect.width > 10) {
            float shimmerPos = ((float)EditorApplication.timeSinceStartup * 100f) % (filledRect.width + 30);
            Rect shimmerRect = new Rect(
                filledRect.x + shimmerPos - 15,
                filledRect.y,
                30,
                filledRect.height
            );
            // Clip shimmer to filled area
            if (shimmerRect.xMax > filledRect.x && shimmerRect.x < filledRect.xMax) {
                shimmerRect.x = Mathf.Max(shimmerRect.x, filledRect.x);
                shimmerRect.xMax = Mathf.Min(shimmerRect.xMax, filledRect.xMax);
                Color shimmerColor = RCCP_AIDesignSystem.Colors.WithAlpha(Color.white, 0.15f);
                EditorGUI.DrawRect(shimmerRect, shimmerColor);
            }
        }

        // Border
        float borderWidth = 1f;
        EditorGUI.DrawRect(new Rect(progressRect.x, progressRect.y, progressRect.width, borderWidth), RCCP_AIDesignSystem.Colors.BorderLight);
        EditorGUI.DrawRect(new Rect(progressRect.x, progressRect.yMax - borderWidth, progressRect.width, borderWidth), RCCP_AIDesignSystem.Colors.BorderLight);
        EditorGUI.DrawRect(new Rect(progressRect.x, progressRect.y, borderWidth, progressRect.height), RCCP_AIDesignSystem.Colors.BorderLight);
        EditorGUI.DrawRect(new Rect(progressRect.xMax - borderWidth, progressRect.y, borderWidth, progressRect.height), RCCP_AIDesignSystem.Colors.BorderLight);

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space1);

        // Percentage text
        GUILayout.Label($"{(progress * 100):F0}%", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        });

        EditorGUILayout.EndVertical();
    }

    private void DrawStep3_Preview() {
        // Apply response fade-in animation
        // For batch customization, skip animation to ensure immediate visibility
        Color originalColor = GUI.color;
        bool isBatchWithResponses = isBatchCustomization && batchCustomizationResponses.Count > 0;
        if (enableAnimations && !isBatchWithResponses) {
            GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * responseAppearAlpha);
        }

        // In Vehicle Creation mode, show review without step number (steps 3 and 4 are for wheels/colliders)
        // In other modes, show as Step 3
        bool isVehicleCreation = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        bool isAskMode = promptMode == RCCP_AIConfig.PromptMode.Ask;

        // Use structured review panel when in review mode (non-Ask mode with parsed JSON)
        if (isInReviewMode && !isAskMode && reviewPanel != null && currentReviewData != null) {
            DrawStep3_ReviewPanel();

            // Restore original color from response animation
            if (enableAnimations) {
                GUI.color = originalColor;
            }
            return;
        }

        // Different header text for Ask mode vs Request mode
        string headerText = isAskMode ? "AI Response" : "Review & Apply";
        string headerIcon = isAskMode ? "💬" : "✨";

        if (isVehicleCreation) {
            // Show as continuation of step 2 without number
            EditorGUILayout.BeginVertical(stepBoxStyle);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(headerIcon, GUILayout.Width(20));
            GUILayout.Label(headerText, RCCP_AIDesignSystem.LabelHeader);
            EditorGUILayout.EndHorizontal();
            RCCP_AIDesignSystem.Space(S2);
        } else {
            DrawStepHeader("3", headerText, false, true, "▶");
            EditorGUILayout.BeginVertical(stepBoxStyle);
        }

        // === SCROLLABLE CONTENT AREA ===
        // AI explanation + Changes preview in a scrollable container
        DrawReviewScrollableContent();

        // Note: Footer (Apply button) is now drawn OUTSIDE the main scroll view
        // See DrawDockedApplyFooter() called after EndScrollView in OnGUI

        EditorGUILayout.EndVertical();

        // Restore original color from response animation
        if (enableAnimations) {
            GUI.color = originalColor;
        }

        RCCP_AIDesignSystem.Space(S5);
    }

    /// <summary>
    /// Draws the scrollable content area containing AI explanation and changes preview.
    /// </summary>
    private void DrawReviewScrollableContent() {
        bool isAskMode = promptMode == RCCP_AIConfig.PromptMode.Ask;

        // Check if we're in batch customization mode with per-vehicle responses
        bool isBatchWithResponses = isBatchCustomization && batchCustomizationResponses.Count > 0 && string.IsNullOrEmpty(aiResponse);

        if (isBatchWithResponses) {
            // Batch customization mode: Show summary of vehicles ready for configuration
            DrawBatchCustomizationPreview();
        } else if (isAskMode) {
            // Ask mode: Display the full response as text (no JSON parsing needed)
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);
            GUILayout.Label(aiResponse, new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
                wordWrap = true,
                richText = true,
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary },
                padding = new RectOffset(8, 8, 8, 8)
            });
            EditorGUILayout.EndVertical();
        } else {
            // Request mode: Display AI explanation
            string explanation = ExtractExplanation(aiResponse);
            if (!string.IsNullOrEmpty(explanation)) {
                EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);
                GUILayout.Label(explanation, new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                    fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
                    wordWrap = true,
                    normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary },
                    padding = new RectOffset(4, 4, 4, 4)
                });
                EditorGUILayout.EndVertical();
                RCCP_AIDesignSystem.Space(S4);
            }

            // Changes preview (only in Request mode)
            DrawChangesSection();
        }
    }

    /// <summary>
    /// Draws the batch customization preview showing each vehicle's status.
    /// In Ask mode, shows full AI responses for each vehicle.
    /// In Request mode, shows compact vehicle list with brief explanations.
    /// </summary>
    private void DrawBatchCustomizationPreview() {
        bool isAskMode = promptMode == RCCP_AIConfig.PromptMode.Ask;

        if (isAskMode) {
            // Ask mode: Show full responses for each vehicle
            GUILayout.Label($"AI Responses for {batchCustomizationResponses.Count} vehicles:", RCCP_AIDesignSystem.LabelHeader);
            RCCP_AIDesignSystem.Space(S4);

            // Pre-create styles to avoid issues with nested struct initialization
            GUIStyle vehicleHeaderStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader);
            vehicleHeaderStyle.fontSize = RCCP_AIDesignSystem.Typography.SizeSM;

            GUIStyle responseTextStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary);
            responseTextStyle.fontSize = RCCP_AIDesignSystem.Typography.SizeMD;
            responseTextStyle.wordWrap = true;
            responseTextStyle.richText = true;
            responseTextStyle.normal.textColor = RCCP_AIDesignSystem.Colors.TextPrimary;
            responseTextStyle.padding = new RectOffset(8, 8, 4, 8);

            foreach (var kvp in batchCustomizationResponses) {
                if (kvp.Key == null) continue;

                EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

                // Vehicle header
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("🚗", GUILayout.Width(20));
                GUILayout.Label(kvp.Key.gameObject.name, vehicleHeaderStyle);
                EditorGUILayout.EndHorizontal();

                RCCP_AIDesignSystem.Space(S2);

                // Full AI response text
                GUILayout.Label(kvp.Value, responseTextStyle);

                EditorGUILayout.EndVertical();
                RCCP_AIDesignSystem.Space(S4);
            }
        } else {
            // Request mode: Show compact vehicle list with brief explanations
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

            GUILayout.Label($"Configurations ready for {batchCustomizationResponses.Count} vehicles:", RCCP_AIDesignSystem.LabelHeader);
            RCCP_AIDesignSystem.Space(S2);

            // Show each vehicle with its status
            foreach (var kvp in batchCustomizationResponses) {
                if (kvp.Key == null) continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("✓", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = RCCP_AIDesignSystem.Colors.Success }
                }, GUILayout.Width(20));
                GUILayout.Label(kvp.Key.gameObject.name, RCCP_AIDesignSystem.LabelSmall);

                // Show brief explanation if available
                string explanation = ExtractExplanation(kvp.Value);
                if (!string.IsNullOrEmpty(explanation)) {
                    GUILayout.FlexibleSpace();
                    // Truncate long explanations
                    if (explanation.Length > 50) {
                        explanation = explanation.Substring(0, 47) + "...";
                    }
                    GUILayout.Label(explanation, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                        normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                        fontStyle = FontStyle.Italic
                    });
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // Show any vehicles that failed
        int failedCount = batchCustomizationVehicles.Count - batchCustomizationResponses.Count;
        if (failedCount > 0) {
            RCCP_AIDesignSystem.Space(S2);
            GUILayout.Label($"{failedCount} vehicle(s) failed to generate configuration", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Warning }
            });
        }
    }

    /// <summary>
    /// Determines if the docked apply footer should be shown.
    /// Only shows when we have an AI response that hasn't been applied yet (or was just applied).
    /// </summary>
    private bool ShouldShowDockedFooter() {
        // Only show for panels that generate AI responses
        if (CurrentPrompt == null) return false;

        // Don't show for diagnostics panel (it has its own UI)
        if (CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Diagnostics) return false;

        // Don't show when in review mode (review panel has its own footer)
        if (isInReviewMode && reviewPanel != null && currentReviewData != null) return false;

        // Show if we have an AI response OR batch customization responses
        bool hasResponse = !string.IsNullOrEmpty(aiResponse);
        bool hasBatchResponse = isBatchCustomization && batchCustomizationResponses.Count > 0;
        return hasResponse || hasBatchResponse;
    }

    /// <summary>
    /// Draws the docked apply footer at the bottom of the panel, outside the scroll view.
    /// This ensures the Apply button is always visible.
    /// </summary>
    private void DrawDockedApplyFooter() {
        // Footer container with dark background - use full width horizontal layout
        GUIStyle footerStyle = new GUIStyle(RCCP_AIDesignSystem.PanelElevated) {
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(8, 8, 6, 6)
        };
        EditorGUILayout.BeginVertical(footerStyle, GUILayout.ExpandWidth(true));

        // Single row: Clear on left, Apply button on right
        EditorGUILayout.BeginHorizontal();

        // Left side - Clear button and optional Copy JSON
        if (GUILayout.Button("Clear", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(60), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonMedium))) {
            aiResponse = "";
            beforeStateSnapshot = "";
            showPreview = false;
            changesApplied = false;
            ExitReviewMode();  // Also exit review mode when clearing
            ClearStatus();
            // Reset batch customization state to unlock selection refresh
            if (isBatchCustomization || isBatchCustomizationProcessing) {
                batchCustomizationVehicles.Clear();
                batchCustomizationResponses.Clear();
                isBatchCustomization = false;
                isBatchCustomizationProcessing = false;
                batchCustomizationUserPrompt = "";
                RefreshSelection();
            }
        }

        if (developerMode) {
            RCCP_AIDesignSystem.Space(S3);
            if (GUILayout.Button("Copy JSON", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(85), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonMedium))) {
                // In batch mode, aggregate all responses; otherwise use single aiResponse
                if (isBatchCustomization && batchCustomizationResponses.Count > 0) {
                    var sb = new System.Text.StringBuilder();
                    foreach (var kvp in batchCustomizationResponses) {
                        if (kvp.Key == null) continue;
                        sb.AppendLine($"=== {kvp.Key.gameObject.name} ===");
                        sb.AppendLine(kvp.Value);
                        sb.AppendLine();
                    }
                    GUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
                    SetStatus($"Copied {batchCustomizationResponses.Count} responses to clipboard!", MessageType.Info);
                } else {
                    GUIUtility.systemCopyBuffer = aiResponse;
                    SetStatus("Copied to clipboard!", MessageType.Info);
                }
            }
        }

        GUILayout.FlexibleSpace();

        // Right side - Apply Button (fixed width)
        // Only show Apply button in Request mode with JSON response (Ask mode never shows Apply)
        // For batch customization, check batchCustomizationResponses instead of aiResponse
        bool hasValidResponse = IsJsonResponse(aiResponse);
        bool hasBatchResponses = isBatchCustomization && batchCustomizationResponses.Count > 0;
        if (promptMode == RCCP_AIConfig.PromptMode.Request && (hasValidResponse || hasBatchResponses)) {
            if (!autoApply) {
                if (changesApplied) {
                    DrawApplyButtonApplied();
                } else {
                    DrawApplyButtonReady();
                }
            } else {
                if (changesApplied) {
                    DrawApplyButtonAutoApplied();
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws the Apply button in ready state (can be clicked).
    /// </summary>
    private void DrawApplyButtonReady() {
        // Wider button for batch mode text
        float buttonWidth = isBatchCustomization ? 220f : 180f;
        float buttonHeight = 32f;

        Rect buttonRect = GUILayoutUtility.GetRect(buttonWidth, buttonHeight, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight));

        // Draw colored background
        Color buttonColor = RCCP_AIDesignSystem.Colors.Success;  // Green
        Color hoverColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Success, 0.1f);

        bool isHovering = buttonRect.Contains(Event.current.mousePosition);
        EditorGUI.DrawRect(buttonRect, isHovering ? hoverColor : buttonColor);

        // Draw button label - show vehicle count for batch customization
        GUIStyle labelStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        // For batch mode, show count of vehicles with responses ready
        int readyCount = batchCustomizationResponses.Count;
        int totalCount = batchCustomizationVehicles.Count;
        string buttonLabel = isBatchCustomization
            ? (readyCount == totalCount ? $"✓  Apply to {readyCount} Vehicles" : $"✓  Apply to {readyCount}/{totalCount} Vehicles")
            : "✓  Apply Configuration";
        GUI.Label(buttonRect, buttonLabel, labelStyle);

        // Handle click
        if (Event.current.type == EventType.MouseDown && buttonRect.Contains(Event.current.mousePosition)) {
            // Use batch apply for batch customization mode
            if (isBatchCustomization) {
                // Check if this is Lights panel - use dedicated lights batch apply
                bool isLightsPanelClick = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;
                if (isLightsPanelClick) {
                    ApplyBatchLights();
                } else {
                    ApplyBatchVehicleCustomization();
                }
            } else {
                bool success = ApplyConfiguration();

                if (success) {
                    changesApplied = true;
                    MarkCurrentPromptAsApplied();

                    // Scroll to bottom smoothly (deferred to next frame for proper layout)
                    EditorApplication.delayCall += () => {
                        scrollTargetY = float.MaxValue;  // Will be clamped by scroll view
                        Repaint();
                    };

                    if (enableAnimations) {
                        successFlashAlpha = 1f;
                    }
                }
            }

            Event.current.Use();
            Repaint();
        }

        if (isHovering) {
            Repaint();
        }
    }

    /// <summary>
    /// Draws the Apply button in applied state (shows confirmation).
    /// </summary>
    private void DrawApplyButtonApplied() {
        float buttonWidth = 180f;
        float buttonHeight = 32f;

        Rect buttonRect = GUILayoutUtility.GetRect(buttonWidth, buttonHeight, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight));

        // Draw success background
        EditorGUI.DrawRect(buttonRect, RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Darken(RCCP_AIDesignSystem.Colors.Success, 0.3f), 0.8f));

        // Draw checkmark and text
        GUIStyle successStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Success, 0.3f) }
        };
        GUI.Label(buttonRect, "✓  Applied", successStyle);

        // Flash effect
        if (enableAnimations && successFlashAlpha > 0.01f) {
            Color flashColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Success, successFlashAlpha * 0.3f);
            EditorGUI.DrawRect(buttonRect, flashColor);
        }
    }

    /// <summary>
    /// Draws the Apply button in auto-applied state.
    /// </summary>
    private void DrawApplyButtonAutoApplied() {
        float buttonWidth = 180f;
        float buttonHeight = 32f;
        Rect buttonRect = GUILayoutUtility.GetRect(buttonWidth, buttonHeight, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight));

        // Draw subtle background
        EditorGUI.DrawRect(buttonRect, RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Darken(RCCP_AIDesignSystem.Colors.Success, 0.35f), 0.6f));

        // Draw text
        GUIStyle autoStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Success, 0.25f) }
        };
        GUI.Label(buttonRect, "✓  Auto-applied", autoStyle);
    }

    private void DrawChangesSection() {
        if (string.IsNullOrEmpty(aiResponse)) return;

        // Section header
        GUIStyle headerStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.TextSecondary, 0.1f) }
        };

        // Check if response is JSON (configuration) or plain text (informational)
        if (IsJsonResponse(aiResponse)) {
            GUILayout.Label("Changes to Apply:", headerStyle);
            RCCP_AIDesignSystem.Space(S2);
            DrawFocusedChangesView();
        } else {
            GUILayout.Label("AI Response:", headerStyle);
            RCCP_AIDesignSystem.Space(S2);
            DrawPlainTextResponse();
        }
    }

    /// <summary>
    /// Checks if the AI response contains JSON (direct or markdown-wrapped).
    /// Returns true for raw JSON or JSON inside markdown code blocks (```json ... ```).
    /// </summary>
    private bool IsJsonResponse(string response) {
        if (string.IsNullOrEmpty(response)) return false;
        string trimmed = response.Trim();

        // Direct JSON (starts with { or [)
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            return true;

        // Markdown-wrapped JSON (```json or ```) - check if there's extractable JSON
        if (trimmed.Contains("```")) {
            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            return start >= 0 && end > start;
        }

        return false;
    }

    /// <summary>
    /// Draws plain text AI response for informational queries.
    /// </summary>
    private void DrawPlainTextResponse() {
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        GUIStyle textStyle = new GUIStyle(EditorStyles.wordWrappedLabel) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            richText = true,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary },
            padding = new RectOffset(8, 8, 8, 8)
        };

        GUILayout.Label(aiResponse, textStyle);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws a focused diff view showing only the changes that will be applied,
    /// grouped by category with a clean card-style layout.
    /// </summary>
    private void DrawFocusedChangesView() {
        if (string.IsNullOrEmpty(aiResponse)) return;

        try {
            string extracted = ExtractJson(aiResponse);

            // Style definitions
            GUIStyle categoryHeaderStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
                normal = { textColor = AccentColor },
                padding = new RectOffset(0, 0, 2, 2)
            };

            GUIStyle changeItemStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
                richText = true,
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextPrimary },
                padding = new RectOffset(8, 0, 1, 1)
            };

            GUIStyle arrowStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
                normal = { textColor = AccentColor },
                alignment = TextAnchor.MiddleCenter
            };

            // Container box - no inner scroll view to avoid scroll traps
            // The main window scroll view handles scrolling
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

            bool hasAnyChanges = false;

            // Wheels panel returns WheelConfig directly, not wrapped in VehicleSetupConfig
            if (CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Wheels) {
                var wheelConfig = JsonUtility.FromJson<RCCP_AIConfig.WheelConfig>(extracted);
                if (wheelConfig != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(wheelConfig)) {
                    hasAnyChanges = DrawWheelChanges(wheelConfig, categoryHeaderStyle, changeItemStyle, arrowStyle);
                }

                if (!hasAnyChanges) {
                    GUILayout.Label("No specific changes detected in JSON.", changeItemStyle);
                }

                EditorGUILayout.EndVertical();
                return;
            }

            // Audio panel returns AudioConfig directly, not wrapped in VehicleSetupConfig
            if (CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Audio) {
                var audioConfig = JsonUtility.FromJson<RCCP_AIConfig.AudioConfig>(extracted);
                if (audioConfig != null && audioConfig.engineSounds != null && audioConfig.engineSounds.Length > 0) {
                    hasAnyChanges = DrawAudioChanges(audioConfig, categoryHeaderStyle, changeItemStyle, arrowStyle);
                }

                if (!hasAnyChanges) {
                    GUILayout.Label("No specific changes detected in JSON.", changeItemStyle);
                }

                EditorGUILayout.EndVertical();
                return;
            }

            // Lights panel returns LightsConfig directly, not wrapped in VehicleSetupConfig
            if (CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights) {
                var lightsConfig = JsonUtility.FromJson<RCCP_AIConfig.LightsConfig>(extracted);
                if (lightsConfig != null && lightsConfig.lights != null && lightsConfig.lights.Length > 0) {
                    hasAnyChanges = DrawLightsChanges(lightsConfig, categoryHeaderStyle, changeItemStyle, arrowStyle);
                }

                if (!hasAnyChanges) {
                    GUILayout.Label("No specific changes detected in JSON.", changeItemStyle);
                }

                EditorGUILayout.EndVertical();
                return;
            }

            // Parse as VehicleSetupConfig for other panels
            var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(extracted);

            // Vehicle Config
            if (config.vehicleConfig != null && config.vehicleConfig.mass > 0) {
                DrawChangeCategory("Vehicle", categoryHeaderStyle);
                DrawChangeItem("Mass", "--", config.vehicleConfig.mass + "kg", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Engine
            if (config.engine != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.engine)) {
                DrawChangeCategory("Engine", categoryHeaderStyle);
                if (config.engine.maximumTorqueAsNM > 0)
                    DrawChangeItem("Torque", "--", config.engine.maximumTorqueAsNM + "Nm", changeItemStyle, arrowStyle);
                if (config.engine.maxEngineRPM > 0)
                    DrawChangeItem("Max RPM", "--", config.engine.maxEngineRPM.ToString(), changeItemStyle, arrowStyle);
                if (config.engine.maximumSpeed > 0)
                    DrawChangeItem("Max Speed", "--", config.engine.maximumSpeed + "km/h", changeItemStyle, arrowStyle);
                if (config.engine.turboCharged)
                    DrawChangeItem("Turbo", "Off", config.engine.maxTurboChargePsi + "psi", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Gearbox
            if (config.gearbox != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.gearbox)) {
                DrawChangeCategory("Gearbox", categoryHeaderStyle);
                if (!string.IsNullOrEmpty(config.gearbox.transmissionType))
                    DrawChangeItem("Type", "--", config.gearbox.transmissionType, changeItemStyle, arrowStyle);
                if (config.gearbox.gearRatios != null && config.gearbox.gearRatios.Length > 0)
                    DrawChangeItem("Gears", "--", config.gearbox.gearRatios.Length.ToString(), changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Clutch
            if (config.clutch != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.clutch)) {
                DrawChangeCategory("Clutch", categoryHeaderStyle);
                if (config.clutch.clutchInertia > 0)
                    DrawChangeItem("Inertia", "--", config.clutch.clutchInertia.ToString("F3"), changeItemStyle, arrowStyle);
                if (config.clutch.engageRPM > 0)
                    DrawChangeItem("Engage RPM", "--", config.clutch.engageRPM.ToString("F0"), changeItemStyle, arrowStyle);
                if (config.clutch.automaticClutch)
                    DrawChangeItem("Automatic", "Off", "On", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Differential
            if (config.differential != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.differential)) {
                DrawChangeCategory("Differential", categoryHeaderStyle);
                if (config.differential.limitedSlipRatio > 0)
                    DrawChangeItem("Slip", "--", config.differential.limitedSlipRatio.ToString("F0"), changeItemStyle, arrowStyle);
                if (config.differential.finalDriveRatio > 0)
                    DrawChangeItem("Final Drive", "--", config.differential.finalDriveRatio.ToString("F2"), changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Drive Type
            if (!string.IsNullOrEmpty(config.driveType)) {
                DrawChangeCategory("Drive Type", categoryHeaderStyle);
                DrawChangeItem("Drive", "--", config.driveType, changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Axles (steering, braking, antiroll)
            if (config.axles != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.axles)) {
                DrawChangeCategory("Axles", categoryHeaderStyle);
                // Front axle
                if (config.axles.front != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.axles.front)) {
                    if (config.axles.front.maxSteerAngle > 0)
                        DrawChangeItem("Front Steer Angle", "--", config.axles.front.maxSteerAngle.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                    if (config.axles.front.steerSpeed > 0)
                        DrawChangeItem("Front Steer Speed", "--", config.axles.front.steerSpeed.ToString("F2"), changeItemStyle, arrowStyle);
                    if (config.axles.front.maxBrakeTorque > 0)
                        DrawChangeItem("Front Brake Torque", "--", config.axles.front.maxBrakeTorque.ToString("F0") + "Nm", changeItemStyle, arrowStyle);
                    if (config.axles.front.antirollForce > 0)
                        DrawChangeItem("Front Antiroll", "--", config.axles.front.antirollForce.ToString("F0"), changeItemStyle, arrowStyle);
                    if (config.axles.front.steerMultiplier != 0 && config.axles.front.steerMultiplier != 1)
                        DrawChangeItem("Front Steer Mult", "1.0", config.axles.front.steerMultiplier.ToString("F2"), changeItemStyle, arrowStyle);
                    if (config.axles.front.brakeMultiplier != 0 && config.axles.front.brakeMultiplier != 1)
                        DrawChangeItem("Front Brake Mult", "1.0", config.axles.front.brakeMultiplier.ToString("F2"), changeItemStyle, arrowStyle);
                }
                // Rear axle
                if (config.axles.rear != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.axles.rear)) {
                    if (config.axles.rear.maxSteerAngle > 0)
                        DrawChangeItem("Rear Steer Angle", "--", config.axles.rear.maxSteerAngle.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                    if (config.axles.rear.steerSpeed > 0)
                        DrawChangeItem("Rear Steer Speed", "--", config.axles.rear.steerSpeed.ToString("F2"), changeItemStyle, arrowStyle);
                    if (config.axles.rear.maxBrakeTorque > 0)
                        DrawChangeItem("Rear Brake Torque", "--", config.axles.rear.maxBrakeTorque.ToString("F0") + "Nm", changeItemStyle, arrowStyle);
                    if (config.axles.rear.maxHandbrakeTorque > 0)
                        DrawChangeItem("Rear Handbrake", "--", config.axles.rear.maxHandbrakeTorque.ToString("F0") + "Nm", changeItemStyle, arrowStyle);
                    if (config.axles.rear.antirollForce > 0)
                        DrawChangeItem("Rear Antiroll", "--", config.axles.rear.antirollForce.ToString("F0"), changeItemStyle, arrowStyle);
                    if (config.axles.rear.brakeMultiplier != 0 && config.axles.rear.brakeMultiplier != 1)
                        DrawChangeItem("Rear Brake Mult", "1.0", config.axles.rear.brakeMultiplier.ToString("F2"), changeItemStyle, arrowStyle);
                }
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Suspension (Front/Rear)
            if (config.suspension != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.suspension)) {
                DrawChangeCategory("Suspension", categoryHeaderStyle);
                if (config.suspension.distance > 0)
                    DrawChangeItem("Distance", "--", config.suspension.distance.ToString("F2") + "m", changeItemStyle, arrowStyle);
                if (config.suspension.spring > 0)
                    DrawChangeItem("Spring", "--", config.suspension.spring.ToString("F0"), changeItemStyle, arrowStyle);
                if (config.suspension.damper > 0)
                    DrawChangeItem("Damper", "--", config.suspension.damper.ToString("F0"), changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Wheel Friction
            if (config.wheelFriction != null && !string.IsNullOrEmpty(config.wheelFriction.type)) {
                DrawChangeCategory("Tires", categoryHeaderStyle);
                DrawChangeItem("Type", "--", config.wheelFriction.type, changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Wheels (alignment, width, grip, friction)
            if (config.wheels != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.wheels)) {
                DrawChangeCategory("Wheels", categoryHeaderStyle);
                // Base wheel settings
                if (config.wheels.camber != 0)
                    DrawChangeItem("Camber", "0", config.wheels.camber.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                if (config.wheels.caster != 0)
                    DrawChangeItem("Caster", "0", config.wheels.caster.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                if (config.wheels.wheelWidth > 0)
                    DrawChangeItem("Width", "--", config.wheels.wheelWidth.ToString("F3") + "m", changeItemStyle, arrowStyle);
                if (config.wheels.grip > 0 && config.wheels.grip != 1f)
                    DrawChangeItem("Grip", "1.0", config.wheels.grip.ToString("F2"), changeItemStyle, arrowStyle);
                // Friction curves
                if (config.wheels.forwardFriction != null && config.wheels.forwardFriction.stiffness > 0)
                    DrawChangeItem("Fwd Friction", "1.0", config.wheels.forwardFriction.stiffness.ToString("F2"), changeItemStyle, arrowStyle);
                if (config.wheels.sidewaysFriction != null && config.wheels.sidewaysFriction.stiffness > 0)
                    DrawChangeItem("Side Friction", "1.0", config.wheels.sidewaysFriction.stiffness.ToString("F2"), changeItemStyle, arrowStyle);
                // Front axle overrides
                if (config.wheels.front != null) {
                    if (config.wheels.front.camber != 0)
                        DrawChangeItem("Front Camber", "0", config.wheels.front.camber.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                    if (config.wheels.front.caster != 0)
                        DrawChangeItem("Front Caster", "0", config.wheels.front.caster.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                    if (config.wheels.front.wheelWidth > 0)
                        DrawChangeItem("Front Width", "--", config.wheels.front.wheelWidth.ToString("F3") + "m", changeItemStyle, arrowStyle);
                    if (config.wheels.front.grip > 0 && config.wheels.front.grip != 1f)
                        DrawChangeItem("Front Grip", "1.0", config.wheels.front.grip.ToString("F2"), changeItemStyle, arrowStyle);
                    if (config.wheels.front.forwardFriction != null && config.wheels.front.forwardFriction.stiffness > 0)
                        DrawChangeItem("Front Fwd Friction", "1.0", config.wheels.front.forwardFriction.stiffness.ToString("F2"), changeItemStyle, arrowStyle);
                    if (config.wheels.front.sidewaysFriction != null && config.wheels.front.sidewaysFriction.stiffness > 0)
                        DrawChangeItem("Front Side Friction", "1.0", config.wheels.front.sidewaysFriction.stiffness.ToString("F2"), changeItemStyle, arrowStyle);
                }
                // Rear axle overrides
                if (config.wheels.rear != null) {
                    if (config.wheels.rear.camber != 0)
                        DrawChangeItem("Rear Camber", "0", config.wheels.rear.camber.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                    if (config.wheels.rear.caster != 0)
                        DrawChangeItem("Rear Caster", "0", config.wheels.rear.caster.ToString("F1") + "\u00B0", changeItemStyle, arrowStyle);
                    if (config.wheels.rear.wheelWidth > 0)
                        DrawChangeItem("Rear Width", "--", config.wheels.rear.wheelWidth.ToString("F3") + "m", changeItemStyle, arrowStyle);
                    if (config.wheels.rear.grip > 0 && config.wheels.rear.grip != 1f)
                        DrawChangeItem("Rear Grip", "1.0", config.wheels.rear.grip.ToString("F2"), changeItemStyle, arrowStyle);
                    if (config.wheels.rear.forwardFriction != null && config.wheels.rear.forwardFriction.stiffness > 0)
                        DrawChangeItem("Rear Fwd Friction", "1.0", config.wheels.rear.forwardFriction.stiffness.ToString("F2"), changeItemStyle, arrowStyle);
                    if (config.wheels.rear.sidewaysFriction != null && config.wheels.rear.sidewaysFriction.stiffness > 0)
                        DrawChangeItem("Rear Side Friction", "1.0", config.wheels.rear.sidewaysFriction.stiffness.ToString("F2"), changeItemStyle, arrowStyle);
                }
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Stability (ABS, ESP, TCS, helpers)
            if (config.stability != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.stability)) {
                DrawChangeCategory("Stability", categoryHeaderStyle);
                if (config.stability.remove)
                    DrawChangeItem("Stability", "Enabled", "Remove", changeItemStyle, arrowStyle);
                if (config.stability.ABS)
                    DrawChangeItem("ABS", "Off", "On", changeItemStyle, arrowStyle);
                if (config.stability.ESP)
                    DrawChangeItem("ESP", "Off", "On", changeItemStyle, arrowStyle);
                if (config.stability.TCS)
                    DrawChangeItem("TCS", "Off", "On", changeItemStyle, arrowStyle);
                if (config.stability.steeringHelper)
                    DrawChangeItem("Steer Helper", "Off", "On", changeItemStyle, arrowStyle);
                if (config.stability.steerHelperStrength > 0)
                    DrawChangeItem("Steer Strength", "--", config.stability.steerHelperStrength.ToString("F2"), changeItemStyle, arrowStyle);
                if (config.stability.tractionHelper)
                    DrawChangeItem("Traction Helper", "Off", "On", changeItemStyle, arrowStyle);
                if (config.stability.tractionHelperStrength > 0)
                    DrawChangeItem("Traction Strength", "--", config.stability.tractionHelperStrength.ToString("F2"), changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // AeroDynamics
            if (config.aeroDynamics != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.aeroDynamics)) {
                DrawChangeCategory("AeroDynamics", categoryHeaderStyle);
                if (config.aeroDynamics.remove)
                    DrawChangeItem("Aero", "Enabled", "Remove", changeItemStyle, arrowStyle);
                if (config.aeroDynamics.downForce > 0)
                    DrawChangeItem("Downforce", "--", config.aeroDynamics.downForce.ToString("F0"), changeItemStyle, arrowStyle);
                if (config.aeroDynamics.airResistance > 0)
                    DrawChangeItem("Air Resistance", "--", config.aeroDynamics.airResistance.ToString("F4"), changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // NOS
            if (config.nos != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.nos)) {
                DrawChangeCategory("NOS", categoryHeaderStyle);
                if (config.nos.remove)
                    DrawChangeItem("NOS", "Enabled", "Remove", changeItemStyle, arrowStyle);
                else if (config.nos.enabled)
                    DrawChangeItem("NOS", "Off", "Enabled", changeItemStyle, arrowStyle);
                if (config.nos.torqueMultiplier > 0)
                    DrawChangeItem("Torque Mult", "--", config.nos.torqueMultiplier.ToString("F1") + "x", changeItemStyle, arrowStyle);
                if (config.nos.durationTime > 0)
                    DrawChangeItem("Duration", "--", config.nos.durationTime.ToString("F1") + "s", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // FuelTank
            if (config.fuelTank != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.fuelTank)) {
                DrawChangeCategory("Fuel Tank", categoryHeaderStyle);
                if (config.fuelTank.remove)
                    DrawChangeItem("Fuel Tank", "Enabled", "Remove", changeItemStyle, arrowStyle);
                else if (config.fuelTank.enabled)
                    DrawChangeItem("Fuel Tank", "Off", "Enabled", changeItemStyle, arrowStyle);
                if (config.fuelTank.fuelTankCapacity > 0)
                    DrawChangeItem("Capacity", "--", config.fuelTank.fuelTankCapacity.ToString("F0") + "L", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Limiter
            if (config.limiter != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.limiter)) {
                DrawChangeCategory("Limiter", categoryHeaderStyle);
                if (config.limiter.remove)
                    DrawChangeItem("Limiter", "Enabled", "Remove", changeItemStyle, arrowStyle);
                else if (config.limiter.enabled)
                    DrawChangeItem("Limiter", "Off", "Enabled", changeItemStyle, arrowStyle);
                if (config.limiter.limitSpeedAtGear != null && config.limiter.limitSpeedAtGear.Length > 0)
                    DrawChangeItem("Speed Limits", "--", config.limiter.limitSpeedAtGear.Length + " gears", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Input (counter-steering, steering limiter)
            if (config.input != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.input)) {
                DrawChangeCategory("Input", categoryHeaderStyle);
                if (config.input.counterSteering)
                    DrawChangeItem("Counter Steer", "Off", "On", changeItemStyle, arrowStyle);
                if (config.input.counterSteerFactor > 0)
                    DrawChangeItem("Counter Factor", "--", config.input.counterSteerFactor.ToString("F2"), changeItemStyle, arrowStyle);
                if (config.input.steeringLimiter)
                    DrawChangeItem("Steer Limiter", "Off", "On", changeItemStyle, arrowStyle);
                if (config.input.autoReverse)
                    DrawChangeItem("Auto Reverse", "Off", "On", changeItemStyle, arrowStyle);
                if (config.input.steeringDeadzone > 0)
                    DrawChangeItem("Deadzone", "--", config.input.steeringDeadzone.ToString("F2"), changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Recorder (remove only)
            if (config.recorder != null && config.recorder.remove) {
                DrawChangeCategory("Recorder", categoryHeaderStyle);
                DrawChangeItem("Recorder", "Enabled", "Remove", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // TrailerAttacher (remove only)
            if (config.trailerAttacher != null && config.trailerAttacher.remove) {
                DrawChangeCategory("Trailer Attacher", categoryHeaderStyle);
                DrawChangeItem("Trailer Attacher", "Enabled", "Remove", changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            // Customizer
            if (config.customizer != null && (config.customizer.remove || !string.IsNullOrEmpty(config.customizer.saveFileName))) {
                DrawChangeCategory("Customizer", categoryHeaderStyle);
                if (config.customizer.remove)
                    DrawChangeItem("Customizer", "Enabled", "Remove", changeItemStyle, arrowStyle);
                if (!string.IsNullOrEmpty(config.customizer.saveFileName))
                    DrawChangeItem("Save File", "--", config.customizer.saveFileName, changeItemStyle, arrowStyle);
                hasAnyChanges = true;
                RCCP_AIDesignSystem.Space(S3);
            }

            if (!hasAnyChanges) {
                GUILayout.Label("No specific changes detected in JSON.", changeItemStyle);
            }

            EditorGUILayout.EndVertical();

        } catch (System.Exception e) {
            EditorGUILayout.HelpBox($"Error previewing changes: {e.Message}", MessageType.Error);
        }
    }

    private void DrawChangeCategory(string categoryName, GUIStyle style) {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(categoryName, style);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        // Separator line
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, RCCP_AIDesignSystem.Colors.BorderLight);
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space1);
    }

    private void DrawChangeItem(string label, string before, string after, GUIStyle itemStyle, GUIStyle arrowStyle) {
        EditorGUILayout.BeginHorizontal();
        // Use flexible width for label to prevent clipping long property names
        GUILayout.Label(label, itemStyle, GUILayout.Width(110));
        GUILayout.FlexibleSpace();
        // Right-aligned value with proper styling - disable word wrap to prevent line breaks
        GUIStyle valueStyle = new GUIStyle(itemStyle) {
            alignment = TextAnchor.MiddleRight,
            wordWrap = false
        };
        GUILayout.Label(after, valueStyle);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws wheel configuration changes for the Wheels panel.
    /// Returns true if any changes were drawn.
    /// </summary>
    private bool DrawWheelChanges(RCCP_AIConfig.WheelConfig config, GUIStyle categoryStyle, GUIStyle itemStyle, GUIStyle arrowStyle) {
        if (config == null) return false;

        bool hasChanges = false;

        // Check if we have any base-level changes
        bool hasBaseChanges = config.camber != 0 || config.caster != 0 || config.wheelWidth > 0 ||
                              (config.grip > 0 && config.grip != 1f) ||
                              RCCP_AIVehicleBuilder.HasMeaningfulValues(config.forwardFriction) ||
                              RCCP_AIVehicleBuilder.HasMeaningfulValues(config.sidewaysFriction);

        if (hasBaseChanges) {
            DrawChangeCategory("Wheels", categoryStyle);
            if (config.camber != 0)
                DrawChangeItem("Camber", "0", config.camber.ToString("F1") + "\u00B0", itemStyle, arrowStyle);
            if (config.caster != 0)
                DrawChangeItem("Caster", "0", config.caster.ToString("F1") + "\u00B0", itemStyle, arrowStyle);
            if (config.wheelWidth > 0)
                DrawChangeItem("Width", "--", config.wheelWidth.ToString("F3") + "m", itemStyle, arrowStyle);
            if (config.grip > 0 && config.grip != 1f)
                DrawChangeItem("Grip", "1.0", config.grip.ToString("F2"), itemStyle, arrowStyle);
            if (config.forwardFriction != null && config.forwardFriction.stiffness > 0)
                DrawChangeItem("Fwd Stiffness", "1.0", config.forwardFriction.stiffness.ToString("F2"), itemStyle, arrowStyle);
            if (config.sidewaysFriction != null && config.sidewaysFriction.stiffness > 0)
                DrawChangeItem("Side Stiffness", "1.0", config.sidewaysFriction.stiffness.ToString("F2"), itemStyle, arrowStyle);
            hasChanges = true;
            RCCP_AIDesignSystem.Space(S3);
        }

        // Front axle overrides
        if (config.front != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.front)) {
            DrawChangeCategory("Front Axle", categoryStyle);
            if (config.front.camber != 0)
                DrawChangeItem("Camber", "0", config.front.camber.ToString("F1") + "\u00B0", itemStyle, arrowStyle);
            if (config.front.caster != 0)
                DrawChangeItem("Caster", "0", config.front.caster.ToString("F1") + "\u00B0", itemStyle, arrowStyle);
            if (config.front.wheelWidth > 0)
                DrawChangeItem("Width", "--", config.front.wheelWidth.ToString("F3") + "m", itemStyle, arrowStyle);
            if (config.front.grip > 0 && config.front.grip != 1f)
                DrawChangeItem("Grip", "1.0", config.front.grip.ToString("F2"), itemStyle, arrowStyle);
            if (config.front.forwardFriction != null && config.front.forwardFriction.stiffness > 0)
                DrawChangeItem("Fwd Stiffness", "1.0", config.front.forwardFriction.stiffness.ToString("F2"), itemStyle, arrowStyle);
            if (config.front.sidewaysFriction != null && config.front.sidewaysFriction.stiffness > 0)
                DrawChangeItem("Side Stiffness", "1.0", config.front.sidewaysFriction.stiffness.ToString("F2"), itemStyle, arrowStyle);
            hasChanges = true;
            RCCP_AIDesignSystem.Space(S3);
        }

        // Rear axle overrides
        if (config.rear != null && RCCP_AIVehicleBuilder.HasMeaningfulValues(config.rear)) {
            DrawChangeCategory("Rear Axle", categoryStyle);
            if (config.rear.camber != 0)
                DrawChangeItem("Camber", "0", config.rear.camber.ToString("F1") + "\u00B0", itemStyle, arrowStyle);
            if (config.rear.caster != 0)
                DrawChangeItem("Caster", "0", config.rear.caster.ToString("F1") + "\u00B0", itemStyle, arrowStyle);
            if (config.rear.wheelWidth > 0)
                DrawChangeItem("Width", "--", config.rear.wheelWidth.ToString("F3") + "m", itemStyle, arrowStyle);
            if (config.rear.grip > 0 && config.rear.grip != 1f)
                DrawChangeItem("Grip", "1.0", config.rear.grip.ToString("F2"), itemStyle, arrowStyle);
            if (config.rear.forwardFriction != null && config.rear.forwardFriction.stiffness > 0)
                DrawChangeItem("Fwd Stiffness", "1.0", config.rear.forwardFriction.stiffness.ToString("F2"), itemStyle, arrowStyle);
            if (config.rear.sidewaysFriction != null && config.rear.sidewaysFriction.stiffness > 0)
                DrawChangeItem("Side Stiffness", "1.0", config.rear.sidewaysFriction.stiffness.ToString("F2"), itemStyle, arrowStyle);
            hasChanges = true;
            RCCP_AIDesignSystem.Space(S3);
        }

        return hasChanges;
    }

    /// <summary>
    /// Draws audio configuration changes for the Audio panel.
    /// Returns true if any changes were drawn.
    /// </summary>
    private bool DrawAudioChanges(RCCP_AIConfig.AudioConfig config, GUIStyle categoryStyle, GUIStyle itemStyle, GUIStyle arrowStyle) {
        if (config == null || config.engineSounds == null || config.engineSounds.Length == 0)
            return false;

        bool hasChanges = false;

        DrawChangeCategory("Engine Sound Layers", categoryStyle);

        for (int i = 0; i < config.engineSounds.Length; i++) {
            var layer = config.engineSounds[i];
            if (layer == null) continue;

            // Determine layer index (use layerIndex if specified, otherwise sequential)
            int layerIndex = layer.layerIndex > 0 ? layer.layerIndex : i;

            // Check if this layer has meaningful settings
            bool hasSettings = layer.minRPM > 0 || layer.maxRPM > 0 ||
                              layer.minPitch > 0 || layer.maxPitch > 0 ||
                              layer.maxVolume > 0 ||
                              layer.ShouldEnable || layer.ShouldDisable;

            if (!hasSettings) continue;

            // Draw layer header
            string layerLabel = $"Layer {layerIndex}";
            if (layer.ShouldDisable) {
                DrawChangeItem(layerLabel, "On", "Off (Disabled)", itemStyle, arrowStyle);
            } else {
                // RPM range
                if (layer.minRPM > 0 || layer.maxRPM > 0) {
                    string rpmRange = $"{layer.minRPM:F0}-{layer.maxRPM:F0} RPM";
                    DrawChangeItem($"{layerLabel} RPM", "--", rpmRange, itemStyle, arrowStyle);
                }

                // Pitch range
                if (layer.minPitch > 0 || layer.maxPitch > 0) {
                    string pitchRange = $"{layer.minPitch:F2}-{layer.maxPitch:F2}";
                    DrawChangeItem($"{layerLabel} Pitch", "--", pitchRange, itemStyle, arrowStyle);
                }

                // Volume
                if (layer.maxVolume > 0) {
                    DrawChangeItem($"{layerLabel} Volume", "--", layer.maxVolume.ToString("F2"), itemStyle, arrowStyle);
                }

                // 3D distance
                if (layer.minDistance > 0 || layer.maxDistance > 0) {
                    string distRange = $"{layer.minDistance:F0}-{layer.maxDistance:F0}m";
                    DrawChangeItem($"{layerLabel} Distance", "--", distRange, itemStyle, arrowStyle);
                }
            }

            hasChanges = true;
        }

        if (hasChanges) {
            RCCP_AIDesignSystem.Space(S3);
        }

        return hasChanges;
    }

    /// <summary>
    /// Draws lights configuration changes for the Lights panel.
    /// Returns true if any changes were drawn.
    /// </summary>
    private bool DrawLightsChanges(RCCP_AIConfig.LightsConfig config, GUIStyle categoryStyle, GUIStyle itemStyle, GUIStyle arrowStyle) {
        if (config == null || config.lights == null || config.lights.Length == 0)
            return false;

        bool hasChanges = false;

        DrawChangeCategory("Lights Configuration", categoryStyle);

        foreach (var light in config.lights) {
            if (light == null || string.IsNullOrEmpty(light.lightType)) continue;

            // Light type header
            string lightLabel = light.lightType.Replace("_", " ");

            // Intensity
            if (light.intensity > 0) {
                DrawChangeItem($"{lightLabel}", "--", $"Intensity: {light.intensity:F1}", itemStyle, arrowStyle);
            }

            // Range
            if (light.range > 0) {
                DrawChangeItem($"{lightLabel} Range", "--", $"{light.range:F0}m", itemStyle, arrowStyle);
            }

            // Spot angle
            if (light.spotAngle > 0) {
                DrawChangeItem($"{lightLabel} Angle", "--", $"{light.spotAngle:F0}°", itemStyle, arrowStyle);
            }

            // Light color
            if (light.lightColor != null) {
                string colorStr = $"{light.lightColor.r:F1}, {light.lightColor.g:F1}, {light.lightColor.b:F1}";
                DrawChangeItem($"{lightLabel} Color", "--", colorStr, itemStyle, arrowStyle);
            }

            // Lens flares
            if (light.ShouldModifyLensFlares) {
                DrawChangeItem($"{lightLabel} Flares", "--", light.useLensFlares == 1 ? "On" : "Off", itemStyle, arrowStyle);
            }

            // Breakable
            if (light.ShouldModifyBreakable) {
                DrawChangeItem($"{lightLabel} Breakable", "--", light.isBreakable == 1 ? "Yes" : "No", itemStyle, arrowStyle);
            }

            hasChanges = true;
        }

        if (hasChanges) {
            RCCP_AIDesignSystem.Space(S3);
        }

        return hasChanges;
    }

    private void DrawStepHeader(string number, string title, bool isComplete, bool isCurrent, string icon = null, string summary = null) {
        EditorGUILayout.BeginHorizontal();

        // Step number badge - circular design like mockups
        float badgeSize = 26f;
        Rect badgeRect = GUILayoutUtility.GetRect(badgeSize, badgeSize, GUILayout.Width(badgeSize), GUILayout.Height(badgeSize));

        // Determine colors based on state using design system
        Color badgeBgColor;
        Color badgeTextColor;
        string badgeContent;

        if (isComplete) {
            badgeBgColor = DS.Success;
            badgeTextColor = RCCP_AIDesignSystem.Colors.TextInverse;
            badgeContent = "✓";
        } else if (isCurrent) {
            // Pulse animation for current step
            float pulse = enableAnimations
                ? 0.85f + 0.15f * Mathf.Sin((float)EditorApplication.timeSinceStartup * 3f * animationSpeed)
                : 1f;
            badgeBgColor = RCCP_AIDesignSystem.Colors.Mix(DS.AccentMuted, DS.Accent, pulse);
            badgeTextColor = RCCP_AIDesignSystem.Colors.TextInverse;
            badgeContent = icon ?? number;
        } else {
            badgeBgColor = DS.BgHover;
            badgeTextColor = DS.TextDisabled;
            badgeContent = number;
        }

        // Draw circular badge background
        DrawCircle(badgeRect, badgeBgColor);

        // Draw number/icon or checkmark centered in badge
        GUIStyle badgeLabelStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = isComplete ? RCCP_AIDesignSystem.Typography.SizeLG : RCCP_AIDesignSystem.Typography.SizeMD,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = badgeTextColor },
            fontStyle = FontStyle.Bold
        };
        GUI.Label(badgeRect, badgeContent, badgeLabelStyle);

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Title with better styling
        GUIStyle titleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD + 1,
            normal = { textColor = isCurrent || isComplete ? DS.TextPrimary : DS.TextDisabled }
        };

        GUILayout.Label(title, titleStyle);

        // Help tooltip button
        if (StepTooltips.TryGetValue(title, out string tooltip)) {
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
            GUIStyle helpStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = DS.TextSecondary },
                hover = { textColor = DS.Accent }
            };
            GUIContent helpContent = new GUIContent("(?)", tooltip);
            GUILayout.Label(helpContent, helpStyle, GUILayout.Width(20));
        }

        // Show summary when step is complete
        if (isComplete && !string.IsNullOrEmpty(summary)) {
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);
            GUIStyle summaryStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = DS.Success },
                fontStyle = FontStyle.Italic,
                clipping = TextClipping.Clip
            };
            // Use tooltip for full text, clipping handles display
            GUIContent summaryContent = new GUIContent(summary, summary);
            GUILayout.Label(summaryContent, summaryStyle, GUILayout.MaxWidth(250));
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);
    }

    private void DrawCircle(Rect rect, Color color) {
        RCCP_AIDesignSystem.DrawCircle(rect, color);
    }

    private void DrawInfoMessage(string message) {
        GUIStyle infoStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Info },
            padding = RCCP_AIDesignSystem.Spacing.LRTB(5, 0, 3, 3),
            wordWrap = true
        };
        GUILayout.Label($"ℹ {message}", infoStyle);
    }

    private void DrawWarningMessage(string message) {
        GUIStyle warnStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.Warning },
            padding = RCCP_AIDesignSystem.Spacing.LRTB(5, 0, 3, 3),
            wordWrap = true
        };
        GUILayout.Label($"⚠ {message}", warnStyle);
    }

    private void DrawSuccessMessage(string message) {
        GUIStyle successStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.Success },
            padding = RCCP_AIDesignSystem.Spacing.LRTB(5, 0, 3, 3),
            wordWrap = true
        };
        GUILayout.Label($"✓ {message}", successStyle);
    }

    private void DrawSizeErrorMessage(string message) {
        GUIStyle errorStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = DS.Error },
            padding = RCCP_AIDesignSystem.Spacing.LRTB(5, 0, 3, 3),
            wordWrap = true
        };
        GUILayout.Label($"⛔ {message}", errorStyle);
    }

    /// <summary>
    /// Draws the eligibility check panel showing Scale, Orientation, and Wheel checks.
    /// </summary>
    private void DrawEligibilityCheckPanel(EligibilityCheck eligibility) {
        if (eligibility == null) return;

        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        // Header with overall status
        EditorGUILayout.BeginHorizontal();
        string overallIcon = EligibilityCheck.GetStatusIcon(eligibility.overallStatus);
        Color overallColor = EligibilityCheck.GetStatusColor(eligibility.overallStatus);

        eligibilityFoldout = EditorGUILayout.Foldout(eligibilityFoldout, "Model Eligibility Check", true);
        GUILayout.FlexibleSpace();

        // Show compact status icons when collapsed
        if (!eligibilityFoldout) {
            GUILayout.Label(EligibilityCheck.GetStatusIcon(eligibility.scaleStatus), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = EligibilityCheck.GetStatusColor(eligibility.scaleStatus) },
                fixedWidth = 14
            });
            GUILayout.Label(EligibilityCheck.GetStatusIcon(eligibility.orientationStatus), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = EligibilityCheck.GetStatusColor(eligibility.orientationStatus) },
                fixedWidth = 14
            });
            GUILayout.Label(EligibilityCheck.GetStatusIcon(eligibility.wheelStatus), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = EligibilityCheck.GetStatusColor(eligibility.wheelStatus) },
                fixedWidth = 14
            });
        }
        
        GUILayout.Label($"{overallIcon} {eligibility.overallStatus}", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = overallColor },
            fontStyle = FontStyle.Bold
        });
        
        EditorGUILayout.EndHorizontal();

        if (eligibilityFoldout) {
            RCCP_AIDesignSystem.Space(S2);
            
            // 1. Scale Check
            DrawEligibilityItem("Scale", eligibility.scaleStatus, eligibility.scaleMessage);
            
            // 2. Orientation Check
            DrawEligibilityItem("Orientation", eligibility.orientationStatus, eligibility.orientationMessage);
            
            // 3. Wheels Check
            DrawEligibilityItem("Wheels", eligibility.wheelStatus, eligibility.wheelMessage);

            // Show wheel auto-assignment preview if 4+ wheels detected
            if (eligibility.wheelCandidates != null && eligibility.wheelCandidates.Count >= 4 &&
                (eligibility.wheelStatus == EligibilityStatus.Pass || eligibility.wheelStatus == EligibilityStatus.Warning))
            {
                var wheelsByAxle = eligibility.wheelCandidates
                    .Where(w => !string.IsNullOrEmpty(w.axleGuess) && w.axleGuess != "Unknown")
                    .OrderBy(w => w.axleGuess)
                    .ToList();

                if (wheelsByAxle.Count >= 4)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Auto-assign:", EditorStyles.miniBoldLabel);
                    foreach (var wheel in wheelsByAxle.Take(4))
                    {
                        EditorGUILayout.LabelField($"  {wheel.axleGuess}: {wheel.name}", EditorStyles.miniLabel);
                    }
                    if (eligibility.wheelCandidates.Count > 4)
                    {
                        EditorGUILayout.LabelField($"  ({eligibility.wheelCandidates.Count - 4} extra wheels ignored)", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            RCCP_AIDesignSystem.Space(S2);
            
            if (eligibility.overallStatus == EligibilityStatus.Pass) {
                DrawSuccessMessage("Model is ready for processing!");
            } else if (eligibility.overallStatus == EligibilityStatus.Warning) {
                DrawWarningMessage("Model has issues but can be processed.");
            } else {
                DrawSizeErrorMessage("Model has critical issues. Please fix before proceeding.");
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEligibilityItem(string title, EligibilityStatus status, string message) {
        EditorGUILayout.BeginHorizontal();
        // Status icon
        GUILayout.Label(EligibilityCheck.GetStatusIcon(status), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = EligibilityCheck.GetStatusColor(status) },
            fixedWidth = 20
        });
        // Fixed width for title to prevent clipping (80px fits "Orientation")
        GUILayout.Label(title, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) { fontStyle = FontStyle.Bold }, GUILayout.Width(80));
        // Message takes remaining space
        GUILayout.Label(message, RCCP_AIDesignSystem.LabelSmall, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    private string ExtractExplanation(string json) {
        if (string.IsNullOrEmpty(json)) return "";

        try {
            // Look for "explanation" field in JSON
            string searchKey = "\"explanation\"";
            int keyIndex = json.IndexOf(searchKey);
            if (keyIndex < 0) return "";

            // Find the colon after the key
            int colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex < 0) return "";

            // Find the opening quote
            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0) return "";

            // Find the closing quote (handle escaped quotes properly)
            int endQuote = startQuote + 1;
            while (endQuote < json.Length) {
                if (json[endQuote] == '"' && !IsEscapedQuote(json, endQuote)) {
                    break;
                }
                endQuote++;
            }

            if (endQuote >= json.Length) return "";

            string explanation = json.Substring(startQuote + 1, endQuote - startQuote - 1);
            // Unescape common escape sequences (order matters: process \\\\ first)
            explanation = explanation.Replace("\\\\", "\x00")
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\x00", "\\");
            return explanation;
        } catch {
            return "";
        }
    }

    /// <summary>
    /// Checks if a quote character at the given index is escaped.
    /// Counts consecutive backslashes before the quote - odd count means escaped.
    /// </summary>
    private static bool IsEscapedQuote(string s, int quoteIndex) {
        int backslashCount = 0;
        int i = quoteIndex - 1;
        while (i >= 0 && s[i] == '\\') {
            backslashCount++;
            i--;
        }
        return backslashCount % 2 == 1;
    }

    private string ExtractJson(string response) {
        return RCCP_AIUtility.ExtractJson(response);
    }

    /// <summary>
    /// Draws the batch mode UI for multiple vehicle creation.
    /// Shows list of selected vehicles, progress during processing, and results.
    /// </summary>
    private void DrawBatchModeUI() {
        RCCP_AIDesignSystem.Space(S4);

        // Header with batch info
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"📦 Batch Mode: {batchVehicles.Count} vehicles selected", RCCP_AIDesignSystem.LabelHeader);
        GUILayout.FlexibleSpace();

        // Cancel button during processing
        if (isBatchProcessing) {
            if (GUILayout.Button("Cancel", GUILayout.Width(60))) {
                CancelBatchProcessing();
            }
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        // Progress bar during processing
        if (isBatchProcessing) {
            float progress = (float)currentBatchIndex / batchVehicles.Count;
            EditorGUI.ProgressBar(
                EditorGUILayout.GetControlRect(GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonInline)),
                progress,
                $"Processing {currentBatchIndex + 1}/{batchVehicles.Count}: {batchVehicles[currentBatchIndex].name}"
            );
            RCCP_AIDesignSystem.Space(S2);
        }

        // Vehicle list
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        for (int i = 0; i < batchVehicles.Count; i++) {
            GameObject vehicle = batchVehicles[i];
            if (vehicle == null) continue;

            EditorGUILayout.BeginHorizontal();

            // Status icon
            string statusIcon = "○";  // Pending
            Color statusColor = RCCP_AIDesignSystem.Colors.TextSecondary;

            if (isBatchProcessing && i == currentBatchIndex) {
                statusIcon = "◐";  // In progress
                statusColor = RCCP_AIDesignSystem.Colors.Info;
            } else if (batchResponses.ContainsKey(vehicle)) {
                statusIcon = "●";  // Completed
                statusColor = RCCP_AIDesignSystem.Colors.Success;
            }

            GUILayout.Label(statusIcon, new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                normal = { textColor = statusColor },
                fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
                fixedWidth = 16
            });

            // Vehicle name
            GUILayout.Label(vehicle.name, GUILayout.MinWidth(120));

            // Size info from mesh analysis
            if (batchMeshAnalysis.TryGetValue(vehicle, out string analysis)) {
                string sizeInfo = "";
                string[] lines = analysis.Split('\n');
                foreach (string line in lines) {
                    if (line.Contains("Size:")) {
                        sizeInfo = line.Replace("Size:", "").Trim();
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(sizeInfo)) {
                    GUILayout.Label(sizeInfo, new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                        normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
                    });
                }
            }

            // Eligibility status icons (Scale, Orientation, Wheels)
            if (batchEligibility.TryGetValue(vehicle, out EligibilityCheck eligibility)) {
                // Show 3 status icons for each check
                GUILayout.Label(EligibilityCheck.GetStatusIcon(eligibility.scaleStatus), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = EligibilityCheck.GetStatusColor(eligibility.scaleStatus) },
                    fixedWidth = 14
                });
                GUILayout.Label(EligibilityCheck.GetStatusIcon(eligibility.orientationStatus), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = EligibilityCheck.GetStatusColor(eligibility.orientationStatus) },
                    fixedWidth = 14
                });
                GUILayout.Label(EligibilityCheck.GetStatusIcon(eligibility.wheelStatus), new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = EligibilityCheck.GetStatusColor(eligibility.wheelStatus) },
                    fixedWidth = 14
                });
            }

            // Root warning icon (Fix #6 - warn if object has parent)
            if (batchSizeWarnings.TryGetValue(vehicle, out var warnings)) {
                bool hasRootWarning = warnings.Exists(w => w.message.Contains("parent"));
                if (hasRootWarning) {
                    GUILayout.Label(new GUIContent("⚠", "Has parent transform - may not be vehicle root"),
                        new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                            normal = { textColor = RCCP_AIDesignSystem.Colors.Warning },
                            fixedWidth = 18
                        });
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();

        // Show Apply All / Clear buttons when batch has responses and auto-apply is disabled
        // Changed from == to > 0 to allow partial success (Fix #5)
        bool batchHasResponses = !isBatchProcessing && batchResponses.Count > 0 && batchVehicles.Count > 0;
        if (batchHasResponses && !autoApply) {
            RCCP_AIDesignSystem.Space(S4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Clear button
            if (GUILayout.Button("Clear", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero), GUILayout.Width(80))) {
                ClearBatchState();
            }

            RCCP_AIDesignSystem.Space(S3);

            // Apply All button - show count for partial success
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Success;

            int readyCount = batchResponses.Count;
            bool isPartialSuccess = readyCount < batchVehicles.Count;
            string buttonText = isPartialSuccess
                ? $"✓  Apply All ({readyCount} of {batchVehicles.Count} vehicles)"
                : $"✓  Apply All ({batchVehicles.Count} vehicles)";

            if (GUILayout.Button(buttonText, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero), GUILayout.MinWidth(200))) {
                ApplyBatchVehicleCreation();
            }

            GUI.backgroundColor = oldBg;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            RCCP_AIDesignSystem.Space(S2);

            // Info message - different for partial success
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            string infoMessage = isPartialSuccess
                ? $"Review the configurations above. {batchVehicles.Count - readyCount} vehicle(s) failed. Click Apply All to create successful vehicles."
                : "Review the configurations above, then click Apply All to create all vehicles.";
            GUILayout.Label(infoMessage,
                new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = isPartialSuccess ? RCCP_AIDesignSystem.Colors.Warning : RCCP_AIDesignSystem.Colors.TextSecondary },
                    alignment = TextAnchor.MiddleCenter
                });
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// Clears all batch state without applying.
    /// </summary>
    private void ClearBatchState() {
        batchResponses.Clear();
        batchVehicles.Clear();
        batchMeshAnalysis.Clear();
        batchEligibility.Clear();
        batchSizeWarnings.Clear();
        batchUserPrompt = "";
        isBatchProcessing = false;
        // Reset animation state so next response triggers fade-in
        responseAppearTarget = 0f;
        responseAppearAlpha = 0f;
        lastAiResponse = "";
        ClearStatus();
        RefreshSelection();
        Repaint();
    }

    /// <summary>
    /// Draws the batch mode UI for Vehicle Customization panel.
    /// Shows list of selected RCCP vehicles. The prompt input and response display
    /// are handled by DrawStep2_Describe and DrawStep3_Preview respectively.
    /// </summary>
    private void DrawBatchCustomizationUI(string title = "Batch Customization") {
        RCCP_AIDesignSystem.Space(S4);

        // Header with cancel button during processing
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"{title} ({batchCustomizationVehicles.Count} RCCP vehicles)", RCCP_AIDesignSystem.LabelHeader);
        GUILayout.FlexibleSpace();

        // Cancel button during processing
        if (isBatchCustomizationProcessing) {
            if (GUILayout.Button("Cancel", GUILayout.Width(60))) {
                CancelBatchCustomizationProcessing();
            }
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        // Progress bar during processing
        if (isBatchCustomizationProcessing && batchCustomizationVehicles.Count > 0) {
            float progress = (float)currentBatchCustomizationIndex / batchCustomizationVehicles.Count;
            string currentName = currentBatchCustomizationIndex < batchCustomizationVehicles.Count &&
                                 batchCustomizationVehicles[currentBatchCustomizationIndex] != null
                ? batchCustomizationVehicles[currentBatchCustomizationIndex].gameObject.name
                : "...";
            EditorGUI.ProgressBar(
                EditorGUILayout.GetControlRect(GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonInline)),
                progress,
                $"Processing {currentBatchCustomizationIndex + 1}/{batchCustomizationVehicles.Count}: {currentName}"
            );
            RCCP_AIDesignSystem.Space(S2);
        }

        // Vehicle list panel
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        // List vehicles (with scroll if many)
        if (batchCustomizationVehicles.Count > 6) {
            batchCustomizationScrollPos = EditorGUILayout.BeginScrollView(batchCustomizationScrollPos, GUILayout.MaxHeight(150));
        }

        for (int i = 0; i < batchCustomizationVehicles.Count; i++) {
            var controller = batchCustomizationVehicles[i];
            if (controller != null) {
                EditorGUILayout.BeginHorizontal();

                // Status icon
                string statusIcon = "○";  // Pending
                Color statusColor = RCCP_AIDesignSystem.Colors.TextSecondary;

                if (isBatchCustomizationProcessing && i == currentBatchCustomizationIndex) {
                    statusIcon = "◐";  // In progress
                    statusColor = RCCP_AIDesignSystem.Colors.Info;
                } else if (batchCustomizationResponses.ContainsKey(controller)) {
                    statusIcon = "●";  // Completed
                    statusColor = RCCP_AIDesignSystem.Colors.Success;
                }

                GUILayout.Label(statusIcon, new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                    normal = { textColor = statusColor },
                    fontSize = RCCP_AIDesignSystem.Typography.SizeMD,
                    fixedWidth = 16
                });

                GUILayout.Label(controller.gameObject.name, RCCP_AIDesignSystem.LabelSmall);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        if (batchCustomizationVehicles.Count > 6) {
            EditorGUILayout.EndScrollView();
        }

        RCCP_AIDesignSystem.Space(S2);
        GUILayout.Label("Each vehicle gets its own AI-generated configuration based on its current state.",
            new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary },
                fontStyle = FontStyle.Italic
            });

        EditorGUILayout.EndVertical();

        // Note: Response display and Apply button are handled by DrawStep3_Preview
        // and DrawDockedApplyFooter which use the standard workflow for batch customization
    }

    /// <summary>
    /// Cancels the batch customization processing.
    /// </summary>
    private void CancelBatchCustomizationProcessing() {
        isBatchCustomizationProcessing = false;
        isProcessing = false;
        if (currentRequestCoroutine != null) {
            EditorCoroutineUtility.StopCoroutine(currentRequestCoroutine);
            currentRequestCoroutine = null;
        }
        SetStatus("Batch customization cancelled", MessageType.Warning);
    }

    /// <summary>
    /// Draws the Apply button for batch customization.
    /// </summary>
    private void DrawBatchCustomizationApplyButton() {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Clear button
        if (GUILayout.Button("Clear", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero), GUILayout.Width(80))) {
            ClearBatchCustomizationState();
            changesApplied = false;
            ClearStatus();
            RefreshSelection();
            Repaint();
        }

        RCCP_AIDesignSystem.Space(S3);

        // Apply button
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Success;

        // Show count of vehicles with responses (each has its own AI-generated config)
        int readyCount = batchCustomizationResponses.Count;
        int totalCount = batchCustomizationVehicles.Count;
        string buttonText = readyCount == totalCount
            ? $"✓  Apply to {readyCount} Vehicles"
            : $"✓  Apply to {readyCount}/{totalCount} Vehicles";
        if (GUILayout.Button(buttonText, GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonHero), GUILayout.MinWidth(200))) {
            ApplyBatchVehicleCustomization();
        }

        GUI.backgroundColor = oldBg;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws the applied status for batch customization.
    /// </summary>
    private void DrawBatchCustomizationAppliedStatus() {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("✓ Configuration applied to all vehicles",
            new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Success },
                fontStyle = FontStyle.Bold
            });
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Clears all batch customization state including per-vehicle responses.
    /// Does NOT modify changesApplied - caller should handle that.
    /// </summary>
    private void ClearBatchCustomizationState() {
        batchCustomizationVehicles.Clear();
        batchCustomizationResponses.Clear();
        isBatchCustomization = false;
        isBatchCustomizationProcessing = false;
        batchCustomizationUserPrompt = "";
        aiResponse = "";
        // Reset animation state so next response triggers fade-in
        responseAppearTarget = 0f;
        responseAppearAlpha = 0f;
        lastAiResponse = "";
        // Note: changesApplied not modified here - let caller decide
    }

    private Vector2 batchCustomizationScrollPos = Vector2.zero;

    private void CancelBatchProcessing() {
        isBatchProcessing = false;
        isProcessing = false;
        if (currentRequestCoroutine != null) {
            EditorCoroutineUtility.StopCoroutine(currentRequestCoroutine);
            currentRequestCoroutine = null;
        }
        SetStatus("Batch processing cancelled", MessageType.Warning);
    }

    private void DrawPostCreationSetup() {
        RCCP_AIDesignSystem.Space(S7);
        EditorGUILayout.LabelField("Step 3 & 4: Post-Creation Setup", EditorStyles.boldLabel);

        // Disable when multiple objects are selected - these steps are for single vehicle only
        bool disableForMultipleSelection = hasMultipleSelection;

        if (disableForMultipleSelection) {
            RCCP_AIDesignSystem.Space(S3);
            EditorGUILayout.HelpBox("Post-creation setup is only available for single vehicle selection.", MessageType.Info);
            RCCP_AIDesignSystem.Space(S3);
        }

        EditorGUI.BeginDisabledGroup(disableForMultipleSelection);

        // Step 3: Wheels
        DrawStep3_Wheels();

        // Step 4: Body Colliders
        DrawStep4_BodyColliders();

        EditorGUI.EndDisabledGroup();
    }

    private void DrawStep3_Wheels() {
        // Step 3 is complete when wheels are assigned, current when it's the active step needing attention
        bool isComplete = HasWheelsAssigned();
        bool isCurrent = HasRCCPController && !isComplete && !NeedsPriorStep(3);

        DrawStepHeader("3", "Select Wheels", isComplete, isCurrent, "▶");

        EditorGUILayout.BeginVertical(stepBoxStyle);

        GUILayout.Label("Select the wheel transforms for front and rear axles:", RCCP_AIDesignSystem.LabelSmall);
        RCCP_AIDesignSystem.Space(S4);

        if (!RCCP_AIEditorPrefs.ShownWheelDetectionWarning) {
            DrawWarningMessage("Auto-detected wheels may not always be accurate. Please verify the selected wheels and re-select them if needed.");
            RCCP_AIDesignSystem.Space(S2);
            if (GUILayout.Button("Got it", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall))) {
                RCCP_AIEditorPrefs.ShownWheelDetectionWarning = true;
            }
            RCCP_AIDesignSystem.Space(S4);
        }

        EditorGUILayout.BeginHorizontal();

        Color oldBg = GUI.backgroundColor;

        // Front Wheels button
        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Info;
        if (GUILayout.Button("🔘  Front Wheels", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge))) {
            OpenFrontWheelSetup();
        }

        // Rear Wheels button
        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Info;
        if (GUILayout.Button("🔘  Rear Wheels", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge))) {
            OpenRearWheelSetup();
        }

        GUI.backgroundColor = oldBg;

        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);
        DrawInfoMessage("Click each button and select the wheel transforms from the hierarchy.");

        EditorGUILayout.EndVertical();
        RCCP_AIDesignSystem.Space(S5);
    }

    private void DrawStep4_BodyColliders() {
        // Step 4 is complete when body colliders exist, current when it's the active step needing attention
        bool isComplete = HasBodyColliders();
        bool isCurrent = HasRCCPController && !isComplete && !NeedsPriorStep(4);

        DrawStepHeader("4", "Body Colliders", isComplete, isCurrent, "▶");

        EditorGUILayout.BeginVertical(stepBoxStyle);

        GUILayout.Label("Add collision meshes to the vehicle body parts:", RCCP_AIDesignSystem.LabelSmall);
        RCCP_AIDesignSystem.Space(S4);

        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Success;
        
        if (GUILayout.Button("📦  Generate Body Colliders", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge))) {
            OpenBodyCollidersWizard();
        }
        
        GUI.backgroundColor = oldBg;
        
        RCCP_AIDesignSystem.Space(S2);
        DrawInfoMessage("Automatically adds MeshColliders or BoxColliders to body meshes.");

        EditorGUILayout.EndVertical();
        RCCP_AIDesignSystem.Space(S5);
    }

    private void MarkCurrentPromptAsApplied() {
        if (CurrentPrompt != null && !string.IsNullOrEmpty(userPrompt) && !string.IsNullOrEmpty(aiResponse)) {
            RCCP_AIPromptHistory.AddEntry(CurrentPrompt.panelType.ToString(), CurrentPrompt.panelName, userPrompt, aiResponse, selectedVehicle != null ? selectedVehicle.name : "", true);
        }
    }

    private void InitializeQuickPrompts() {
        if (CurrentPrompt == null || CurrentPrompt.examplePrompts == null) return;
        
        // If we haven't initialized or prompt changed, re-shuffle
        if (displayedQuickPromptIndices.Count == 0 || lastPromptIndexForShuffle != currentPromptIndex) {
            ShuffleQuickPrompts(true);
            lastPromptIndexForShuffle = currentPromptIndex;
        }
    }

    private void ShuffleQuickPrompts(bool isInitial = false) {
        if (CurrentPrompt == null || CurrentPrompt.examplePrompts == null || CurrentPrompt.examplePrompts.Length == 0) return;

        int totalPrompts = CurrentPrompt.examplePrompts.Length;
        int targetCount = Mathf.Min(quickPromptDisplayCount, totalPrompts);

        // When switching panels (isInitial), start fresh - old indices are from a different panel
        if (isInitial) {
            usedQuickPromptIndices.Clear();
            displayedQuickPromptIndices.Clear();
        }
        // Reset if we've used all prompts (except for the ones currently displayed)
        else if (usedQuickPromptIndices.Count >= totalPrompts - targetCount) {
            usedQuickPromptIndices.Clear();
            // Add currently displayed ones to used so we don't repeat immediately if possible
            foreach (int idx in displayedQuickPromptIndices) {
                if (idx >= 0 && idx < totalPrompts) {
                    usedQuickPromptIndices.Add(idx);
                }
            }
        }

        // Find available indices
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < totalPrompts; i++) {
            if (!usedQuickPromptIndices.Contains(i)) {
                availableIndices.Add(i);
            }
        }

        // If not enough available for our target, reset and use all
        if (availableIndices.Count < targetCount) {
            availableIndices.Clear();
            for (int i = 0; i < totalPrompts; i++) availableIndices.Add(i);
            usedQuickPromptIndices.Clear();
        }

        // Shuffle available
        var rng = new System.Random();
        int n = availableIndices.Count;
        while (n > 1) {
            n--;
            int k = rng.Next(n + 1);
            int value = availableIndices[k];
            availableIndices[k] = availableIndices[n];
            availableIndices[n] = value;
        }

        // Select up to quickPromptDisplayCount
        displayedQuickPromptIndices.Clear();
        int countToTake = Mathf.Min(quickPromptDisplayCount, availableIndices.Count);
        
        for (int i = 0; i < countToTake; i++) {
            int idx = availableIndices[i];
            displayedQuickPromptIndices.Add(idx);
            usedQuickPromptIndices.Add(idx);
        }
    }

    #region UI Drawing - Diagnostics Panel

    private void DrawDiagnosticsPanel() {
        // Vehicle Selection
        EditorGUILayout.BeginVertical(stepBoxStyle);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🔍", GUILayout.Width(20));
        GUILayout.Label("Select RCCP Vehicle", RCCP_AIDesignSystem.LabelHeader);
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        EditorGUI.BeginChangeCheck();
        selectedVehicle = (GameObject)EditorGUILayout.ObjectField(
            "Vehicle",
            selectedVehicle,
            typeof(GameObject),
            true
        );
        if (EditorGUI.EndChangeCheck()) {
            RefreshSelection();
            diagnosticResults = null;  // Clear results when selection changes
        }

        if (!HasRCCPController) {
            EditorGUILayout.HelpBox("Select a vehicle with RCCP_CarController to run diagnostics.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S5);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(S5);

        // Run Diagnostics Button
        EditorGUILayout.BeginVertical(stepBoxStyle);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🩺", GUILayout.Width(20));
        GUILayout.Label("Vehicle Diagnostics", RCCP_AIDesignSystem.LabelHeader);
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        EditorGUI.BeginDisabledGroup(!HasRCCPController);

        EditorGUILayout.BeginHorizontal();

        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Info;
        if (GUILayout.Button("🔍  Run Diagnostics", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge))) {
            diagnosticResults = RCCP_AIVehicleDiagnostics.RunDiagnostics(selectedController);
            var summary = RCCP_AIVehicleDiagnostics.GetSummary(diagnosticResults);
            SetStatus($"Diagnostics complete: {summary.errors} errors, {summary.warnings} warnings, {summary.info} info",
                      summary.errors > 0 ? MessageType.Error : summary.warnings > 0 ? MessageType.Warning : MessageType.Info);
        }
        GUI.backgroundColor = oldBg;

        // Auto-fix button
        if (diagnosticResults != null && diagnosticResults.Count > 0) {
            var summary = RCCP_AIVehicleDiagnostics.GetSummary(diagnosticResults);
            if (summary.errors > 0 || summary.warnings > 0) {
                GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Success;
                if (GUILayout.Button("🔧  Auto-Fix", GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge), GUILayout.Width(100))) {
                    int fixedCount = RCCP_AIVehicleDiagnostics.AutoFixIssues(selectedController, diagnosticResults);
                    if (fixedCount > 0) {
                        SetStatus($"Auto-fixed {fixedCount} issues. Running diagnostics again...", MessageType.Info);
                        diagnosticResults = RCCP_AIVehicleDiagnostics.RunDiagnostics(selectedController);
                    } else {
                        SetStatus("No auto-fixable issues found.", MessageType.Info);
                    }
                }
                GUI.backgroundColor = oldBg;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();

        // Results
        if (diagnosticResults != null && diagnosticResults.Count > 0) {
            RCCP_AIDesignSystem.Space(S5);
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S5);
            
            DrawDiagnosticsResults();
        } else if (diagnosticResults != null && diagnosticResults.Count == 0) {
            RCCP_AIDesignSystem.Space(S5);
            RCCP_AIDesignSystem.DrawSeparator(true);
            RCCP_AIDesignSystem.Space(S5);
            
            EditorGUILayout.BeginVertical(stepBoxStyle);
            EditorGUILayout.HelpBox("No issues found! Vehicle configuration looks good.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawDiagnosticsResults() {
        var summary = RCCP_AIVehicleDiagnostics.GetSummary(diagnosticResults);

        // Summary Header
        EditorGUILayout.BeginVertical(stepBoxStyle);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("📊", GUILayout.Width(20));
        GUILayout.Label("Results Summary", RCCP_AIDesignSystem.LabelHeader);
        GUILayout.FlexibleSpace();

        // Summary counts with colors
        if (summary.errors > 0) {
            GUILayout.Label($"❌ {summary.errors}", new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Error }
            });
        }
        if (summary.warnings > 0) {
            GUILayout.Label($"⚠️ {summary.warnings}", new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Warning }
            });
        }
        if (summary.info > 0) {
            GUILayout.Label($"ℹ️ {summary.info}", new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Info }
            });
        }

        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        // Filter toggles
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Show:", RCCP_AIDesignSystem.LabelSmall, GUILayout.MinWidth(30), GUILayout.ExpandWidth(false));
        showErrorMessages = GUILayout.Toggle(showErrorMessages, $"Errors ({summary.errors})", GUILayout.MinWidth(65), GUILayout.ExpandWidth(false));
        showWarningMessages = GUILayout.Toggle(showWarningMessages, $"Warnings ({summary.warnings})", GUILayout.MinWidth(85), GUILayout.ExpandWidth(false));
        showInfoMessages = GUILayout.Toggle(showInfoMessages, $"Info ({summary.info})", GUILayout.MinWidth(55), GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(S2);

        // Results List - no inner scroll view to avoid scroll traps
        // The main window scroll view handles scrolling
        EditorGUILayout.BeginVertical(stepBoxStyle);

        // Group results by category
        var groupedResults = diagnosticResults
            .Where(r => (r.severity == RCCP_AIVehicleDiagnostics.Severity.Error && showErrorMessages) ||
                       (r.severity == RCCP_AIVehicleDiagnostics.Severity.Warning && showWarningMessages) ||
                       (r.severity == RCCP_AIVehicleDiagnostics.Severity.Info && showInfoMessages))
            .GroupBy(r => r.category)
            .OrderBy(g => g.Key);

        foreach (var group in groupedResults) {
            GUILayout.Label(group.Key, RCCP_AIDesignSystem.LabelHeader);

            foreach (var result in group) {
                DrawDiagnosticResultItem(result);
            }

            RCCP_AIDesignSystem.Space(S2);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawDiagnosticResultItem(RCCP_AIVehicleDiagnostics.DiagnosticResult result) {
        // Determine icon and color based on severity
        string icon;
        Color bgColor;
        switch (result.severity) {
            case RCCP_AIVehicleDiagnostics.Severity.Error:
                icon = "❌";
                bgColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Darken(RCCP_AIDesignSystem.Colors.Error, 0.3f), 0.3f);
                break;
            case RCCP_AIVehicleDiagnostics.Severity.Warning:
                icon = "⚠️";
                bgColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Darken(RCCP_AIDesignSystem.Colors.Warning, 0.3f), 0.3f);
                break;
            default:
                icon = "ℹ️";
                bgColor = RCCP_AIDesignSystem.Colors.WithAlpha(RCCP_AIDesignSystem.Colors.Darken(RCCP_AIDesignSystem.Colors.Info, 0.3f), 0.3f);
                break;
        }

        // Draw item background
        Rect itemRect = EditorGUILayout.BeginVertical();
        EditorGUI.DrawRect(itemRect, bgColor);

        EditorGUILayout.BeginHorizontal();

        // Icon
        GUILayout.Label(icon, GUILayout.Width(20));

        // Message
        EditorGUILayout.BeginVertical();
        GUILayout.Label(result.message, RCCP_AIDesignSystem.LabelPrimary);

        if (!string.IsNullOrEmpty(result.suggestion)) {
            GUILayout.Label($"💡 {result.suggestion}", new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Success, 0.15f) },
                wordWrap = true
            });
        }
        EditorGUILayout.EndVertical();

        // Action buttons
        GUILayout.FlexibleSpace();

        // Help button with tooltip showing detailed info
        string tooltipText = $"Category: {result.category}";
        if (!string.IsNullOrEmpty(result.suggestion)) {
            tooltipText += $"\n\nSuggestion: {result.suggestion}";
        }
        GUIContent helpContent = new GUIContent("?", tooltipText);
        GUILayout.Button(helpContent, RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(22));

        // Select button (if target object is available)
        if (result.targetObject != null) {
            if (GUILayout.Button("Select", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(50))) {
                Selection.activeObject = result.targetObject;
                EditorGUIUtility.PingObject(result.targetObject);
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            }
        }

        // Individual Fix button (if auto-fix is available)
        if (result.CanAutoFix) {
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Success; // Green tint
            if (GUILayout.Button("Fix", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(35))) {
                // Record undo before fixing
                if (result.targetObject != null) {
                    Undo.RecordObject(result.targetObject, $"Fix: {result.message}");
                }

                // Execute the fix
                result.autoFix?.Invoke();

                // Re-run diagnostics to update the list
                if (selectedController != null) {
                    diagnosticResults = RCCP_AIVehicleDiagnostics.RunDiagnostics(selectedController);
                    var summary = RCCP_AIVehicleDiagnostics.GetSummary(diagnosticResults);
                    SetStatus($"Fixed! Remaining: {summary.errors} errors, {summary.warnings} warnings",
                        summary.errors > 0 ? MessageType.Warning : MessageType.Info);
                }
                Repaint();
            }
            GUI.backgroundColor = oldBg;
        }

        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Context Bar

    /// <summary>
    /// Draws a persistent context bar showing target info, model, cost estimate, and API status
    /// </summary>
    private void DrawContextBar() {
        // Skip if no prompt selected
        if (CurrentPrompt == null) return;

        bool isVehicleCreationPanel = CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        bool hasTarget = selectedVehicle != null || (selectedController != null);
        string targetName = selectedController != null ? selectedController.name : (selectedVehicle != null ? selectedVehicle.name : null);
        string sizeSummary = GetMeshAnalysisSummary();

        // Get model display name
        string modelName = GetCurrentModelDisplayName();

        // Estimate cost (rough approximation)
        float estimatedTokens = (userPrompt?.Length ?? 0) / 3.5f + (CurrentPrompt?.systemPrompt?.Length ?? 0) / 3.5f;
        float costEstimate = EstimateRequestCost(estimatedTokens);

        // Check API status (either local key or server proxy enabled)
        bool hasApiKey = HasValidAuth;

        // Context bar style
        GUIStyle barStyle = new GUIStyle(RCCP_AIDesignSystem.PanelRecessed) {
            padding = new RectOffset(10, 10, 4, 4),
            margin = new RectOffset(16, 16, 0, 8)
        };

        EditorGUILayout.BeginHorizontal(barStyle, GUILayout.Height(RCCP_AIDesignSystem.Heights.Button));

        // Style for text elements
        GUIStyle infoStyle = new GUIStyle(EditorStyles.label) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.8f) },
            alignment = TextAnchor.MiddleLeft
        };

        // Style for separators
        GUIStyle separatorStyle = new GUIStyle(EditorStyles.label) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeSM,
            normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.3f) },
            alignment = TextAnchor.MiddleCenter
        };

        // Left side: Target info (with max width to prevent overflow)
        if (CurrentPrompt.requiresVehicle || CurrentPrompt.requiresRCCPController) {
            if (hasTarget && !string.IsNullOrEmpty(targetName)) {
                GUIStyle targetStyle = new GUIStyle(infoStyle) {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = DS.TextPrimary },
                    clipping = TextClipping.Clip
                };

                string targetText = $"📦 {targetName}";
                Vector2 targetSize = targetStyle.CalcSize(new GUIContent(targetText));
                float maxTargetWidth = Mathf.Min(targetSize.x, 220); // Cap at 220px
                EditorGUILayout.LabelField(targetText, targetStyle, GUILayout.Width(maxTargetWidth));

                if (!string.IsNullOrEmpty(sizeSummary)) {
                    EditorGUILayout.LabelField("|", separatorStyle, GUILayout.Width(14));

                    GUIStyle sizeStyle = new GUIStyle(infoStyle) {
                        normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.7f) },
                        clipping = TextClipping.Clip
                    };
                    Vector2 sizeSize = sizeStyle.CalcSize(new GUIContent(sizeSummary));
                    float maxSizeWidth = Mathf.Min(sizeSize.x, 120); // Cap at 120px
                    EditorGUILayout.LabelField(sizeSummary, sizeStyle, GUILayout.Width(maxSizeWidth));
                }
            } else {
                GUIStyle noTargetStyle = new GUIStyle(infoStyle) {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = RCCP_AIDesignSystem.Colors.WithAlpha(DS.TextSecondary, 0.5f) }
                };
                EditorGUILayout.LabelField("No target selected", noTargetStyle, GUILayout.Width(120));
            }
        }

        GUILayout.FlexibleSpace();

        // Right side: Model, Cost, API Status
        
        // Model with color coding
        Color modelColor = GetModelColor(modelName);
        GUIStyle modelStyle = new GUIStyle(infoStyle) {
            normal = { textColor = modelColor }
        };
        Vector2 modelSize = modelStyle.CalcSize(new GUIContent(modelName));
        EditorGUILayout.LabelField(modelName, modelStyle, GUILayout.Width(modelSize.x));

        EditorGUILayout.LabelField("|", separatorStyle, GUILayout.Width(14));

        // Cost estimate - only show when using own API key (not server proxy)
        bool showCostInHeader = RCCP_AISettings.Instance != null && !RCCP_AISettings.Instance.useServerProxy;
        if (showCostInHeader && costEstimate > 0) {
            string costText = $"~${costEstimate:F4}";
            Vector2 costSize = infoStyle.CalcSize(new GUIContent(costText));
            EditorGUILayout.LabelField(costText, infoStyle, GUILayout.Width(costSize.x));

            EditorGUILayout.LabelField("|", separatorStyle, GUILayout.Width(14));
        }

        // API status indicator - also considers vehicle eligibility
        GUIStyle statusStyle = new GUIStyle(infoStyle) {
            fontStyle = FontStyle.Bold
        };

        // Determine status: API key + vehicle eligibility (for Vehicle Creation panel)
        string statusText;
        Color statusColor;
        bool hasEligibilityFail = isVehicleCreationPanel && currentEligibility != null &&
            currentEligibility.overallStatus == EligibilityStatus.Fail;

        if (!hasApiKey) {
            statusText = "● No Key";
            statusColor = RCCP_AIDesignSystem.Colors.Error;
        } else if (hasEligibilityFail) {
            statusText = "● Invalid";
            statusColor = RCCP_AIDesignSystem.Colors.Error;
        } else {
            statusText = "● Ready";
            statusColor = RCCP_AIDesignSystem.Colors.Success;
        }
        statusStyle.normal.textColor = statusColor;
        Vector2 statusSize = statusStyle.CalcSize(new GUIContent(statusText));
        EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(statusSize.x));

        EditorGUILayout.EndHorizontal();

        // Bottom separator for sticky context bar
        RCCP_AIDesignSystem.DrawSeparator(true);
    }

    /// <summary>
    /// Gets a short display name for the current model
    /// </summary>
    private string GetCurrentModelDisplayName() {
        if (RCCP_AISettings.Instance == null) return "Haiku";

        string model = RCCP_AISettings.Instance.textModel;
        if (model.Contains("haiku")) return "Haiku";
        if (model.Contains("sonnet")) return "Sonnet";
        if (model.Contains("opus")) return "Opus";
        return "Haiku";
    }

    /// <summary>
    /// Gets a color for the model name (green for cheap, orange for mid, red for expensive)
    /// </summary>
    private Color GetModelColor(string modelName) {
        return modelName switch {
            "Haiku" => RCCP_AIDesignSystem.Colors.Success,
            "Sonnet" => RCCP_AIDesignSystem.Colors.Warning,
            "Opus" => RCCP_AIDesignSystem.Colors.Error,
            _ => DS.TextSecondary
        };
    }

    /// <summary>
    /// Estimates the cost of a request based on token count
    /// </summary>
    private float EstimateRequestCost(float estimatedTokens) {
        string modelName = GetCurrentModelDisplayName();
        // Rough cost per 1K tokens (input + output combined estimate)
        float costPer1KTokens = modelName switch {
            "Haiku" => 0.001f,    // ~$0.001 per 1K tokens
            "Sonnet" => 0.006f,   // ~$0.006 per 1K tokens
            "Opus" => 0.03f,      // ~$0.03 per 1K tokens
            _ => 0.001f
        };
        return (estimatedTokens / 1000f) * costPer1KTokens;
    }

    #endregion

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
