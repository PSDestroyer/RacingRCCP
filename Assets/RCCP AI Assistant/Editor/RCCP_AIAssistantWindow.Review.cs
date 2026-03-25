//----------------------------------------------
//        RCCP AI Setup Assistant
//        Review Panel Integration
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
using UnityEditor;
using UnityEngine;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Partial class extending RCCP_AIAssistantWindow with Review Panel functionality.
/// </summary>
public partial class RCCP_AIAssistantWindow {
    
    #region Review Panel State
    
    private RCCP_AIReviewPanel reviewPanel;
    private RCCP_AIReviewData currentReviewData;
    private bool isInReviewMode;
    private bool reviewPanelApplied;
    
    #endregion
    
    #region Review Panel Initialization
    
    /// <summary>
    /// Initialize the review panel. Call this in OnEnable.
    /// </summary>
    private void InitializeReviewPanel() {
        if (reviewPanel == null) {
            reviewPanel = new RCCP_AIReviewPanel();
            
            // Wire up callbacks
            reviewPanel.OnApplySelected = OnReviewApplySelected;
            reviewPanel.OnBack = OnReviewBack;
            reviewPanel.OnRegenerate = OnReviewRegenerate;
            reviewPanel.OnCopyJson = OnReviewCopyJson;
            reviewPanel.OnSavePreset = OnReviewSavePreset;
            reviewPanel.OnSelectComponent = OnReviewSelectComponent;
        }
    }
    
    #endregion
    
    #region Review Panel Workflow
    
    /// <summary>
    /// Transition to review mode after receiving AI response.
    /// Call this instead of directly showing the response.
    /// </summary>
    private void TransitionToReviewMode(string response, string model, float cost, int tokens) {
        InitializeReviewPanel();
        
        // Create review data from the response (pass panel type for proper JSON parsing)
        var panelType = CurrentPrompt != null ? CurrentPrompt.panelType : RCCP_AIPromptAsset.PanelType.Generic;
        currentReviewData = RCCP_AIReviewPanel.CreateFromResponse(
            selectedController,
            response,
            model,
            cost,
            tokens,
            panelType
        );
        
        if (currentReviewData != null && currentReviewData.TotalChanges > 0) {
            reviewPanel.SetReviewData(currentReviewData);
            isInReviewMode = true;
            reviewPanelApplied = false;
            
            // Store the raw response for fallback
            aiResponse = response;
        } else {
            // No structured changes - show as normal response
            aiResponse = response;
            isInReviewMode = false;
        }
    }
    
    /// <summary>
    /// Check if we should use review mode for the current response.
    /// </summary>
    private bool ShouldUseReviewMode() {
        // Only use review mode for Request mode with JSON responses
        if (promptMode != RCCP_AIConfig.PromptMode.Request) return false;
        if (string.IsNullOrEmpty(aiResponse)) return false;
        if (!IsJsonResponse(aiResponse)) return false;
        if (autoApply) return false;  // Skip review when auto-apply is on
        
        return true;
    }
    
    /// <summary>
    /// Draw the review panel when in review mode.
    /// Returns true if should continue to next step.
    /// </summary>
    private bool DrawReviewModeContent() {
        if (!isInReviewMode || reviewPanel == null || currentReviewData == null) {
            return false;
        }
        
        // Draw header
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("✨", GUILayout.Width(20));
        GUILayout.Label("Review & Apply", RCCP_AIDesignSystem.LabelHeader);
        EditorGUILayout.EndHorizontal();
        
        RCCP_AIDesignSystem.Space(S4);
        
        // Draw the review panel
        bool applyClicked = reviewPanel.Draw();
        
        return applyClicked;
    }
    
    /// <summary>
    /// Exit review mode and return to describe step.
    /// </summary>
    private void ExitReviewMode() {
        isInReviewMode = false;
        currentReviewData = null;
    }
    
    #endregion
    
    #region Review Panel Callbacks
    
    private void OnReviewApplySelected(List<string> enabledGroups) {
        // Check for panels that use their own config format (not VehicleSetupConfig)
        bool isWheelsPanel = CurrentPrompt != null &&
            CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Wheels;
        bool isAudioPanel = CurrentPrompt != null &&
            CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Audio;
        bool isLightsPanel = CurrentPrompt != null &&
            CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;
        bool isDamagePanel = CurrentPrompt != null &&
            CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Damage;

        // These panels don't have parsedConfig, but have rawJson
        // Note: Lights supports batch mode so has special handling below
        bool hasSpecialConfig = isWheelsPanel || isAudioPanel || isLightsPanel || isDamagePanel;
        if (currentReviewData == null || (currentReviewData.parsedConfig == null && !hasSpecialConfig)) {
            SetStatus("No configuration to apply", MessageType.Error);
            return;
        }

        // Check if this is Vehicle Creation panel - needs different handling
        bool isVehicleCreation = CurrentPrompt != null &&
            CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;

        // Handle Lights batch customization (before single-vehicle special panel handling)
        if (isLightsPanel && isBatchCustomization && batchCustomizationVehicles.Count > 0) {
            Debug.Log($"[RCCP AI] Batch applying lights to {batchCustomizationVehicles.Count} vehicles");

            int successCount = 0;
            int failCount = 0;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("RCCP AI Batch Lights");

            foreach (RCCP_CarController controller in batchCustomizationVehicles) {
                if (controller == null) continue;
                try {
                    Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "RCCP AI Batch Lights");
                    ApplyLightsToVehicle(controller, currentReviewData.rawJson);
                    successCount++;
                } catch (Exception e) {
                    failCount++;
                    Debug.LogError($"[RCCP AI] Failed lights on {controller.gameObject.name}: {e.Message}");
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Report results
            if (failCount == 0) {
                SetStatus($"Applied lights to {successCount} vehicles!", MessageType.Info);
                if (enableAnimations) successFlashAlpha = 1f;
            } else {
                SetStatus($"Lights applied to {successCount}, {failCount} failed", MessageType.Warning);
            }

            reviewPanelApplied = true;
            changesApplied = true;
            batchCustomizationVehicles.Clear();
            batchCustomizationResponses.Clear();
            isBatchCustomization = false;
            isBatchCustomizationProcessing = false;
            ExitReviewMode();
            Repaint();
            return;
        }

        // Wheels/Audio/Lights/Damage panels (single vehicle) use ApplyConfiguration() which routes to their specific apply methods
        if (isWheelsPanel || isAudioPanel || isLightsPanel || isDamagePanel) {
            bool success = ApplyConfiguration();

            if (success) {
                reviewPanelApplied = true;
                changesApplied = true;
                MarkCurrentPromptAsApplied();

                if (enableAnimations) {
                    successFlashAlpha = 1f;
                }

                ExitReviewMode();
            }

            Repaint();
            return;
        }

        if (isVehicleCreation) {
            // For vehicle creation, use ApplyConfiguration() which routes to ApplyVehicleCreation()
            // and uses pendingApplyVehicle (the 3D model selected when Generate was clicked)
            bool success = ApplyConfiguration();

            if (success) {
                // Mark as applied
                reviewPanelApplied = true;
                changesApplied = true;
                MarkCurrentPromptAsApplied();

                // Trigger success animation
                if (enableAnimations) {
                    successFlashAlpha = 1f;
                }

                // Exit review mode after successful apply
                ExitReviewMode();
            }
            // ApplyConfiguration already sets status messages

            Repaint();
            return;
        }

        // Check for batch customization mode - apply to all selected vehicles
        if (isBatchCustomization && batchCustomizationVehicles.Count > 0) {
            Debug.Log($"[RCCP AI] Batch applying {currentReviewData.TotalEnabledChanges} changes to {batchCustomizationVehicles.Count} vehicles");

            int successCount = 0;
            int failCount = 0;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("RCCP AI Batch Customize (Review)");

            // Create filtered config once for all vehicles
            var filteredConfig = CreateFilteredConfig(currentReviewData.parsedConfig, enabledGroups);

            foreach (RCCP_CarController controller in batchCustomizationVehicles) {
                if (controller == null) continue;

                try {
                    Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "RCCP AI Batch Customize");

                    // Set history context for this vehicle
                    RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
                        panelType = CurrentPrompt?.panelName ?? "Vehicle Customization",
                        userPrompt = userPrompt,
                        explanation = currentReviewData.parsedConfig?.explanation ?? "",
                        appliedJson = currentReviewData.rawJson
                    };

                    RCCP_AIVehicleBuilder.CustomizeVehicle(
                        controller,
                        filteredConfig,
                        forceApplyAll: false,
                        skipHistory: false,
                        originalJson: currentReviewData.rawJson,
                        skipRefreshSelection: true
                    );

                    successCount++;

                    if (settings != null && settings.verboseLogging) {
                        Debug.Log($"[RCCP AI] Batch customized: {controller.gameObject.name}");
                    }
                } catch (Exception e) {
                    failCount++;
                    Debug.LogError($"[RCCP AI] Failed to customize {controller.gameObject.name}: {e.Message}");
                } finally {
                    RCCP_AIVehicleBuilder.CurrentContext = null;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Report results and update state
            if (failCount == 0) {
                SetStatus($"Successfully customized {successCount} vehicles!", MessageType.Info);
                if (enableAnimations) successFlashAlpha = 1f;
            } else if (successCount > 0) {
                SetStatus($"Customized {successCount} vehicles, {failCount} failed", MessageType.Warning);
            } else {
                SetStatus($"Failed to customize all {failCount} vehicles", MessageType.Error);
            }

            // Update prompt history
            if (!string.IsNullOrEmpty(lastPromptHistoryEntryId)) {
                RCCP_AIPromptHistory.MarkAsApplied(lastPromptHistoryEntryId);
            }

            // Mark as applied and clear batch state
            reviewPanelApplied = true;
            changesApplied = true;
            batchCustomizationVehicles.Clear();
            batchCustomizationResponses.Clear();
            isBatchCustomization = false;
            isBatchCustomizationProcessing = false;

            // Exit review mode after batch apply
            ExitReviewMode();
            RefreshSelection();
            Repaint();
            return;
        }

        // For single vehicle customization: use pendingApplyController if available (stored when Generate was clicked)
        RCCP_CarController targetController = pendingApplyController != null ? pendingApplyController : selectedController;

        if (targetController == null) {
            SetStatus("No vehicle selected", MessageType.Error);
            return;
        }

        Debug.Log($"[RCCP AI] Applying {currentReviewData.TotalEnabledChanges} changes from groups: {string.Join(", ", enabledGroups)}");

        try {
            // Create a filtered config with only the enabled groups
            var filteredConfig = CreateFilteredConfig(currentReviewData.parsedConfig, enabledGroups);

            // Set history context before applying (same pattern as batch case)
            RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
                panelType = CurrentPrompt?.panelName ?? "Vehicle Customization",
                userPrompt = userPrompt,
                explanation = currentReviewData.parsedConfig?.explanation ?? "",
                appliedJson = currentReviewData.rawJson
            };

            try {
                // Apply using the existing CustomizeVehicle method
                RCCP_AIVehicleBuilder.CustomizeVehicle(
                    targetController,
                    filteredConfig,
                    forceApplyAll: false,
                    skipHistory: false,
                    originalJson: currentReviewData.rawJson
                );
            } finally {
                RCCP_AIVehicleBuilder.CurrentContext = null;  // Always clear context
            }

            // Mark as applied
            reviewPanelApplied = true;
            changesApplied = true;

            // Update prompt history
            if (!string.IsNullOrEmpty(lastPromptHistoryEntryId)) {
                RCCP_AIPromptHistory.MarkAsApplied(lastPromptHistoryEntryId);
            }

            // Trigger success animation
            if (enableAnimations) {
                successFlashAlpha = 1f;
            }

            SetStatus($"✓ Applied {currentReviewData.TotalEnabledChanges} changes successfully!", MessageType.Info);

            // Exit review mode after successful apply
            ExitReviewMode();

        } catch (OperationCanceledException ex) {
            // User cancelled (e.g., prefab unpack dialog)
            string message = !string.IsNullOrEmpty(ex.Message) ? ex.Message : "Operation cancelled";
            SetStatus(message, MessageType.Info);
        } catch (Exception ex) {
            Debug.LogError($"[RCCP AI] Failed to apply configuration: {ex.Message}\n{ex.StackTrace}");
            SetStatus($"Failed to apply: {ex.Message}", MessageType.Error);
        } finally {
            // Clear pending apply targets after attempt
            pendingApplyVehicle = null;
            pendingApplyController = null;
            pendingEligibility = null;
        }

        Repaint();
    }
    
    /// <summary>
    /// Creates a filtered config containing only the specified groups.
    /// </summary>
    private RCCP_AIConfig.VehicleSetupConfig CreateFilteredConfig(
        RCCP_AIConfig.VehicleSetupConfig source, 
        List<string> enabledGroups) {
        
        var filtered = new RCCP_AIConfig.VehicleSetupConfig();
        filtered.explanation = source.explanation;
        
        foreach (string group in enabledGroups) {
            switch (group) {
                case "Chassis":
                    filtered.vehicleConfig = source.vehicleConfig;
                    filtered.driveType = source.driveType;
                    break;
                case "Engine":
                    filtered.engine = source.engine;
                    break;
                case "Gearbox":
                    filtered.gearbox = source.gearbox;
                    break;
                case "Clutch":
                    filtered.clutch = source.clutch;
                    break;
                case "Differential":
                    filtered.differential = source.differential;
                    if (string.IsNullOrEmpty(filtered.driveType)) {
                        filtered.driveType = source.driveType;
                    }
                    break;
                case "Suspension":
                    filtered.suspension = source.suspension;
                    filtered.axles = source.axles;
                    break;
                case "Stability":
                    filtered.stability = source.stability;
                    break;
                case "Aerodynamics":
                    filtered.aeroDynamics = source.aeroDynamics;
                    break;
                case "Tires":
                    filtered.wheelFriction = source.wheelFriction;
                    filtered.wheels = source.wheels;
                    break;
                case "Add-ons":
                    filtered.nos = source.nos;
                    filtered.fuelTank = source.fuelTank;
                    filtered.limiter = source.limiter;
                    break;
                case "Input":
                    filtered.input = source.input;
                    break;
            }
        }
        
        return filtered;
    }
    
    private void OnReviewBack() {
        ExitReviewMode();
        
        // Keep the response visible so user can see what was generated
        // aiResponse remains unchanged
        
        SetStatus("Returned to edit mode", MessageType.Info);
        Repaint();
    }
    
    private void OnReviewRegenerate() {
        ExitReviewMode();
        
        // Clear response and regenerate
        aiResponse = "";
        currentReviewData = null;
        
        // Trigger generation with same prompt
        if (!string.IsNullOrEmpty(userPrompt)) {
            Generate();
        }
        
        Repaint();
    }
    
    private void OnReviewCopyJson(string json) {
        GUIUtility.systemCopyBuffer = json;
        SetStatus("JSON copied to clipboard", MessageType.Info);
    }
    
    private void OnReviewSavePreset(RCCP_AIReviewData data) {
        string defaultName = $"{data.vehicleName}_config";
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Configuration Preset",
            defaultName,
            "json",
            "Save the AI-generated configuration as a preset"
        );
        
        if (!string.IsNullOrEmpty(path)) {
            try {
                System.IO.File.WriteAllText(path, data.rawJson);
                AssetDatabase.Refresh();
                SetStatus($"Preset saved to: {path}", MessageType.Info);
            } catch (Exception ex) {
                Debug.LogError($"[RCCP AI] Failed to save preset: {ex.Message}");
                SetStatus($"Failed to save preset: {ex.Message}", MessageType.Error);
            }
        }
    }
    
    private UnityEngine.Object OnReviewSelectComponent(string componentType) {
        if (selectedController == null) return null;
        
        Component component = null;
        
        switch (componentType) {
            case "RCCP_Engine":
                component = selectedController.GetComponentInChildren<RCCP_Engine>(true);
                break;
            case "RCCP_Gearbox":
                component = selectedController.GetComponentInChildren<RCCP_Gearbox>(true);
                break;
            case "RCCP_Clutch":
                component = selectedController.GetComponentInChildren<RCCP_Clutch>(true);
                break;
            case "RCCP_Differential":
                component = selectedController.GetComponentInChildren<RCCP_Differential>(true);
                break;
            case "RCCP_Stability":
                component = selectedController.GetComponentInChildren<RCCP_Stability>(true);
                break;
            case "RCCP_AeroDynamics":
                component = selectedController.GetComponentInChildren<RCCP_AeroDynamics>(true);
                break;
            case "RCCP_Nos":
                component = selectedController.GetComponentInChildren<RCCP_Nos>(true);
                break;
            case "RCCP_FuelTank":
                component = selectedController.GetComponentInChildren<RCCP_FuelTank>(true);
                break;
            case "RCCP_Limiter":
                component = selectedController.GetComponentInChildren<RCCP_Limiter>(true);
                break;
            case "RCCP_Input":
                component = selectedController.GetComponentInChildren<RCCP_Input>(true);
                break;
            case "RCCP_Axle":
            case "RCCP_Axle_Front":
                component = FindFrontAxle(selectedController);
                break;
            case "RCCP_Axle_Rear":
                component = FindRearAxle(selectedController);
                break;
            case "RCCP_WheelCollider":
            case "RCCP_WheelCollider_Front":
                component = FindFrontAxle(selectedController)?.leftWheelCollider;
                break;
            case "RCCP_WheelCollider_Rear":
                component = FindRearAxle(selectedController)?.leftWheelCollider;
                break;
            case "Rigidbody":
                component = selectedController.GetComponent<Rigidbody>();
                break;
            case "RCCP_CarController":
                component = selectedController;
                break;
            default:
                // Try to find by type name
                var type = Type.GetType(componentType);
                if (type != null) {
                    component = selectedController.GetComponentInChildren(type, true);
                }
                break;
        }
        
        if (component != null) {
            Selection.activeObject = component.gameObject;
            EditorGUIUtility.PingObject(component);
        }
        
        return component;
    }

    /// <summary>
    /// Find the front axle by comparing wheel collider Z positions (higher Z = front).
    /// </summary>
    private RCCP_Axle FindFrontAxle(RCCP_CarController controller) {
        var axles = controller.GetComponentsInChildren<RCCP_Axle>(true);
        if (axles == null || axles.Length == 0) return null;

        RCCP_Axle frontAxle = null;
        float highestZ = float.MinValue;

        foreach (var axle in axles) {
            if (axle == null || axle.leftWheelCollider == null) continue;
            float wheelZ = controller.transform.InverseTransformPoint(
                axle.leftWheelCollider.transform.position).z;
            if (wheelZ > highestZ) {
                highestZ = wheelZ;
                frontAxle = axle;
            }
        }
        return frontAxle;
    }

    /// <summary>
    /// Find the rear axle by comparing wheel collider Z positions (lower Z = rear).
    /// </summary>
    private RCCP_Axle FindRearAxle(RCCP_CarController controller) {
        var axles = controller.GetComponentsInChildren<RCCP_Axle>(true);
        if (axles == null || axles.Length == 0) return null;

        RCCP_Axle rearAxle = null;
        float lowestZ = float.MaxValue;

        foreach (var axle in axles) {
            if (axle == null || axle.leftWheelCollider == null) continue;
            float wheelZ = controller.transform.InverseTransformPoint(
                axle.leftWheelCollider.transform.position).z;
            if (wheelZ < lowestZ) {
                lowestZ = wheelZ;
                rearAxle = axle;
            }
        }
        return rearAxle;
    }

    #endregion

    #region Review Mode Drawing Helper
    
    /// <summary>
    /// Alternative Step 3 drawing that uses review panel when in review mode.
    /// Can be called from DrawStep3_Preview to replace normal preview with review panel.
    /// </summary>
    private void DrawStep3_ReviewPanel() {
        if (!isInReviewMode || reviewPanel == null) {
            return;
        }
        
        bool isVehicleCreation = CurrentPrompt != null && 
            CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        
        if (isVehicleCreation) {
            // Show as continuation without step number
            EditorGUILayout.BeginVertical(stepBoxStyle);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("✨", GUILayout.Width(20));
            GUILayout.Label("Review & Apply", RCCP_AIDesignSystem.LabelHeader);
            EditorGUILayout.EndHorizontal();
            RCCP_AIDesignSystem.Space(S2);
        } else {
            DrawStepHeader("3", "Review & Apply", reviewPanelApplied, !reviewPanelApplied, "▶");
            EditorGUILayout.BeginVertical(stepBoxStyle);
        }
        
        // Draw review panel content
        reviewPanel.Draw();
        
        EditorGUILayout.EndVertical();
        RCCP_AIDesignSystem.Space(S5);
    }
    
    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
