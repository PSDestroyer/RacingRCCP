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

    #region Apply

    /// <summary>
    /// Prepares a vehicle GameObject for modification by handling prefab unpacking and activation.
    /// Call this before any vehicle modification to ensure consistent behavior.
    /// Shows confirmation dialog before unpacking prefabs.
    /// </summary>
    /// <param name="vehicle">The vehicle GameObject to prepare</param>
    /// <param name="isRawModel">True if this is a raw 3D model (no RCCP controller yet), false for existing RCCP vehicles</param>
    /// <exception cref="OperationCanceledException">Thrown when user cancels prefab unpack</exception>
    private void PrepareVehicleForModification(GameObject vehicle, bool isRawModel = false) {
        if (vehicle == null) return;

        // Unpack prefab if needed - WITH USER CONFIRMATION
        if (PrefabUtility.IsPartOfPrefabInstance(vehicle)) {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(vehicle);
            string prefabName = !string.IsNullOrEmpty(prefabPath)
                ? System.IO.Path.GetFileNameWithoutExtension(prefabPath)
                : "Unknown Prefab";

            bool shouldUnpack = EditorUtility.DisplayDialog(
                "Unpack Prefab?",
                $"The selected object '{vehicle.name}' is a prefab instance of '{prefabName}'.\n\n" +
                "To modify this vehicle with AI configuration, it must be unpacked from its prefab.\n\n" +
                "This will break the prefab link permanently. Continue?",
                "Unpack and Continue",
                "Cancel"
            );

            if (!shouldUnpack) {
                throw new OperationCanceledException("User cancelled prefab unpack");
            }

            PrefabUtility.UnpackPrefabInstance(vehicle, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            isPrefabInstance = false;
            if (settings.verboseLogging) Debug.Log($"[RCCP AI] Unpacked prefab instance: {vehicle.name}");
        }

        // Activate vehicle if inactive
        if (!vehicle.activeInHierarchy) {
            RCCP_AIUtility.ActivateIfInactive(vehicle);
            isVehicleInactive = false;
        }
    }

    /// <summary>
    /// Applies the AI-generated configuration to the vehicle.
    /// </summary>
    /// <returns>True if configuration was applied successfully, false on error or cancel.</returns>
    private bool ApplyConfiguration() {
        if (CurrentPrompt == null || string.IsNullOrEmpty(aiResponse)) {
            SetStatus("Error: No configuration to apply", MessageType.Error);
            return false;
        }

        try {
            string json = ExtractJson(aiResponse);

            switch (CurrentPrompt.panelType) {
                case RCCP_AIPromptAsset.PanelType.VehicleCreation:
                    ApplyVehicleCreation(json);
                    break;
                case RCCP_AIPromptAsset.PanelType.VehicleCustomization:
                    ApplyVehicleCustomization(json);
                    break;
                case RCCP_AIPromptAsset.PanelType.Behaviors:
                    ApplyBehaviors(json);
                    break;
                case RCCP_AIPromptAsset.PanelType.Wheels:
                    ApplyWheels(json);
                    break;
                case RCCP_AIPromptAsset.PanelType.Lights:
                    ApplyLights(json);
                    break;
                case RCCP_AIPromptAsset.PanelType.Damage:
                    ApplyDamage(json);
                    break;
                case RCCP_AIPromptAsset.PanelType.Audio:
                    ApplyAudio(json);
                    break;
                default:
                    ApplyGeneric(json);
                    break;
            }

            // Mark the prompt history entry as applied
            if (!string.IsNullOrEmpty(lastPromptHistoryEntryId)) {
                RCCP_AIPromptHistory.MarkAsApplied(lastPromptHistoryEntryId);
            }

            SetStatus("Configuration applied successfully!", MessageType.Info);
            return true;
        } catch (OperationCanceledException ex) {
            // User cancelled operation (e.g., prefab unpack dialog, friction conflict)
            // Don't mark as applied, don't show success
            string message = !string.IsNullOrEmpty(ex.Message) ? ex.Message : "Operation cancelled";
            SetStatus(message, MessageType.Info);
            return false;
        } catch (Exception e) {
            SetStatus($"Apply Error: {e.Message}", MessageType.Error);
            Debug.LogError($"[RCCP AI] Apply error: {e}");
            return false;
        } finally {
            // Clear pending apply targets after apply attempt (success or failure)
            // They will be re-set on the next Generate call
            pendingApplyVehicle = null;
            pendingApplyController = null;
            pendingEligibility = null;
        }
    }

    private void ApplyVehicleCreation(string json) {
        // Use pending apply target (stored when Generate was called) to handle selection changes
        GameObject targetVehicle = pendingApplyVehicle != null ? pendingApplyVehicle : selectedVehicle;

        if (targetVehicle == null) throw new Exception("No vehicle selected");

        // Let OperationCanceledException propagate to ApplyConfiguration()
        PrepareVehicleForModification(targetVehicle, isRawModel: true);

        Undo.RegisterFullObjectHierarchyUndo(targetVehicle, "RCCP AI Create");

        // Set history context before applying
        RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
            panelType = CurrentPrompt?.panelName ?? "Vehicle Creation",
            userPrompt = userPrompt,
            explanation = ExtractExplanation(json),
            appliedJson = json
        };

        try {
            var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(json);

            HandleMaximumSpeedSelection(config);

            // Auto-assign detected wheels from eligibility check
            // Use pendingEligibility (stored when Generate was clicked) to handle selection changes
            var eligibility = pendingEligibility ?? currentEligibility;
            if (eligibility?.wheelCandidates != null &&
                eligibility.wheelCandidates.Count >= 4 &&
                (eligibility.wheelStatus == EligibilityStatus.Pass ||
                 eligibility.wheelStatus == EligibilityStatus.Warning))
            {
                var detectedWheels = ConvertWheelCandidatesToDetectedWheels(
                    eligibility.wheelCandidates,
                    targetVehicle.transform);

                if (detectedWheels != null)
                {
                    config.detectedWheels = detectedWheels;
                    if (settings.verboseLogging)
                        Debug.Log($"[RCCP AI] Auto-detected wheels: FL={detectedWheels.frontLeft}, FR={detectedWheels.frontRight}, RL={detectedWheels.rearLeft}, RR={detectedWheels.rearRight}");
                }
            }

            RCCP_AIVehicleBuilder.BuildVehicle(targetVehicle, config);
        } finally {
            // Always clear context, even on exception
            RCCP_AIVehicleBuilder.CurrentContext = null;
        }

        // Refresh selection to pick up the new controller
        RefreshSelection();

        // Auto-chain refinement (skip for batch mode)
        // If user prompt contains detailed customization intent, run through customization prompt
        if (!isBatchProcessing && HasDetailedCustomizationIntent(userPrompt)) {
            pendingRefinementPrompt = userPrompt;
            isRefinementPending = true;
            refinementRetryCount = 0;

            // Defer to next frame to ensure selection is complete
            EditorApplication.delayCall += ExecutePendingRefinement;
        }
    }

    /// <summary>
    /// Executes pending post-creation refinement using the customization prompt.
    /// </summary>
    private void ExecutePendingRefinement() {
        if (!isRefinementPending || string.IsNullOrEmpty(pendingRefinementPrompt)) {
            ClearRefinementState();
            return;
        }

        // Retry cap to prevent infinite loop
        refinementRetryCount++;
        if (refinementRetryCount > MaxRefinementRetries) {
            Debug.LogWarning("[RCCP AI] Refinement timed out waiting for controller");
            SetStatus("Vehicle created (customization skipped - controller not ready)", MessageType.Warning);
            ClearRefinementState();
            return;
        }

        if (selectedController == null) {
            // Vehicle not ready yet, try again next frame
            EditorApplication.delayCall += ExecutePendingRefinement;
            return;
        }

        isRefinementPending = false;
        isExecutingRefinement = true;

        // Store current panel context
        int savedPanelIndex = currentPromptIndex;
        string savedUserPrompt = userPrompt;
        string savedAiResponse = aiResponse;

        // Find customization panel index
        var customizationPrompt = settings.GetPrompt(RCCP_AIPromptAsset.PanelType.VehicleCustomization);
        int customizationIndex = -1;
        if (availablePrompts != null) {
            for (int i = 0; i < availablePrompts.Length; i++) {
                if (availablePrompts[i] == customizationPrompt) {
                    customizationIndex = i;
                    break;
                }
            }
        }

        if (customizationIndex < 0) {
            Debug.LogWarning("[RCCP AI] Customization prompt not found, skipping refinement");
            ClearRefinementState();
            return;
        }

        // Temporarily switch context (UI won't update due to isExecutingRefinement guard)
        currentPromptIndex = customizationIndex;
        userPrompt = pendingRefinementPrompt;
        aiResponse = "";

        refinementRestoreInfo = new RefinementRestoreInfo {
            panelIndex = savedPanelIndex,
            userPrompt = savedUserPrompt,
            aiResponse = savedAiResponse
        };

        SetStatus("Applying detailed customization...", MessageType.Info);

        // Use existing Generate() - it will use CurrentPrompt (now customization)
        // and call ApplyVehicleCustomization via ApplyConfiguration
        Generate();
    }

    /// <summary>
    /// Applies vehicle creation to all vehicles in the batch.
    /// Each vehicle gets its own AI-generated configuration.
    /// </summary>
    private void ApplyBatchVehicleCreation() {
        if (batchVehicles.Count == 0 || batchResponses.Count == 0) {
            SetStatus("Error: No batch vehicles to apply", MessageType.Error);
            return;
        }

        // Optional: Batch prefab preflight dialog - ask once instead of per-vehicle
        int prefabCount = 0;
        foreach (var vehicle in batchVehicles) {
            if (vehicle != null && PrefabUtility.IsPartOfPrefabInstance(vehicle))
                prefabCount++;
        }

        bool skipPrefabs = false;
        if (prefabCount > 0) {
            int choice = EditorUtility.DisplayDialogComplex(
                "Unpack Prefabs?",
                $"{prefabCount} of {batchVehicles.Count} vehicles are prefab instances.\n\n" +
                "To modify them, they must be unpacked from their prefabs.\n\n" +
                "Choose how to proceed:",
                "Unpack All",
                "Cancel Batch",
                "Skip Prefabs"
            );

            if (choice == 1) {
                // Cancel
                SetStatus("Batch apply cancelled.", MessageType.Info);
                return;
            }
            if (choice == 2) {
                // Skip prefabs - filter them out
                skipPrefabs = true;
            }
            if (choice == 0) {
                // Unpack All - do it now so PrepareVehicleForModification won't ask again
                foreach (var vehicle in batchVehicles) {
                    if (vehicle != null && PrefabUtility.IsPartOfPrefabInstance(vehicle)) {
                        PrefabUtility.UnpackPrefabInstance(vehicle, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                        if (settings.verboseLogging) Debug.Log($"[RCCP AI] Batch unpacked prefab: {vehicle.name}");
                    }
                }
            }
        }

        // Fix #2/#3: Check if any config has maximumSpeed and ask once for all vehicles
        int batchSpeedChoice = 0; // Default: Drivetrain Only
        bool anyHasMaxSpeed = false;
        float representativeSpeed = 0f;

        foreach (var kvp in batchResponses) {
            try {
                string json = ExtractJson(kvp.Value);
                var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(json);
                if (config?.engine != null && config.engine.maximumSpeed > 0) {
                    anyHasMaxSpeed = true;
                    representativeSpeed = config.engine.maximumSpeed;
                    break;
                }
            } catch { /* Ignore parse errors during preflight check */ }
        }

        if (anyHasMaxSpeed) {
            batchSpeedChoice = EditorUtility.DisplayDialogComplex(
                "Maximum Speed (Batch)",
                $"One or more vehicles have maximum speed configured (e.g., {representativeSpeed:F0} km/h).\n\nChoose how to apply it to ALL vehicles:",
                "Drivetrain Only",
                "Engine Max Speed",
                "Limiter (Per Gear)"
            );
        }

        int successCount = 0;
        int failCount = 0;
        List<string> failedVehicles = new List<string>();

        // Begin undo group for the entire batch
        Undo.SetCurrentGroupName("RCCP AI Batch Create");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (GameObject vehicle in batchVehicles) {
            if (vehicle == null) continue;

            // Skip prefabs if user chose "Skip Prefabs" in preflight dialog
            if (skipPrefabs && PrefabUtility.IsPartOfPrefabInstance(vehicle)) {
                failedVehicles.Add($"{vehicle.name} (skipped - prefab)");
                failCount++;
                continue;
            }

            if (!batchResponses.TryGetValue(vehicle, out string response)) {
                failedVehicles.Add($"{vehicle.name} (no response)");
                failCount++;
                continue;
            }

            try {
                string json = ExtractJson(response);

                try {
                    PrepareVehicleForModification(vehicle, isRawModel: true);
                } catch (OperationCanceledException) {
                    failedVehicles.Add($"{vehicle.name} (prefab unpack cancelled)");
                    failCount++;
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(vehicle, "RCCP AI Batch Create");

                // Set history context for this vehicle
                RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
                    panelType = "Batch Vehicle Creation",
                    userPrompt = $"[Batch] {batchUserPrompt}",
                    explanation = ExtractExplanation(json),
                    appliedJson = json
                };

                try {
                    var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(json);

                    // Apply batch max-speed choice (Fix #2/#3)
                    ApplyBatchMaximumSpeedChoice(config, batchSpeedChoice);

                    // Auto-assign detected wheels from batch eligibility check
                    if (batchEligibility.TryGetValue(vehicle, out EligibilityCheck eligibility) &&
                        eligibility?.wheelCandidates != null &&
                        eligibility.wheelCandidates.Count >= 4 &&
                        (eligibility.wheelStatus == EligibilityStatus.Pass ||
                         eligibility.wheelStatus == EligibilityStatus.Warning))
                    {
                        var detectedWheels = ConvertWheelCandidatesToDetectedWheels(
                            eligibility.wheelCandidates,
                            vehicle.transform);

                        if (detectedWheels != null)
                        {
                            config.detectedWheels = detectedWheels;
                            if (settings.verboseLogging)
                                Debug.Log($"[RCCP AI] Auto-detected wheels for {vehicle.name}: FL={detectedWheels.frontLeft}, FR={detectedWheels.frontRight}, RL={detectedWheels.rearLeft}, RR={detectedWheels.rearRight}");
                        }
                    }

                    RCCP_AIVehicleBuilder.BuildVehicle(vehicle, config);
                    successCount++;
                } finally {
                    // Always clear context, even on exception
                    RCCP_AIVehicleBuilder.CurrentContext = null;
                }
            } catch (Exception e) {
                failedVehicles.Add($"{vehicle.name}: {e.Message}");
                failCount++;
                Debug.LogError($"[RCCP AI] Batch apply error for {vehicle.name}: {e}");
            }
        }

        // Collapse undo group
        Undo.CollapseUndoOperations(undoGroup);

        // Report results
        changesApplied = true;
        if (failCount == 0) {
            SetStatus($"Batch applied successfully! {successCount} vehicles created.", MessageType.Info);
        } else {
            SetStatus($"Batch completed: {successCount} succeeded, {failCount} failed", MessageType.Warning);
            foreach (string failure in failedVehicles) {
                Debug.LogWarning($"[RCCP AI] Batch failure: {failure}");
            }
        }

        // Clear batch state after applying (Fix #1)
        batchResponses.Clear();
        batchVehicles.Clear();
        batchMeshAnalysis.Clear();
        batchEligibility.Clear();
        batchSizeWarnings.Clear();
        isBatchProcessing = false;

        // Refresh selection
        RefreshSelection();
        Repaint();
    }

    /// <summary>
    /// Applies the batch max-speed choice to a config. (Fix #2/#3)
    /// </summary>
    /// <param name="config">The vehicle config to modify</param>
    /// <param name="choice">0=Drivetrain Only, 1=Engine Max Speed, 2=Limiter</param>
    private void ApplyBatchMaximumSpeedChoice(RCCP_AIConfig.VehicleSetupConfig config, int choice) {
        if (config?.engine == null || config.engine.maximumSpeed <= 0f)
            return;

        float targetSpeed = config.engine.maximumSpeed;

        switch (choice) {
            case 0:
                // Drivetrain only (clear max speed)
                config.engine.maximumSpeed = 0f;
                if (config.limiter != null) {
                    config.limiter.enabled = false;
                    config.limiter.limitSpeedAtGear = null;
                }
                break;
            case 1:
                // Engine maximumSpeed (keep as-is)
                break;
            case 2:
                // Limiter (per gear)
                ApplyBatchMaximumSpeedViaLimiter(config, targetSpeed);
                break;
        }
    }

    /// <summary>
    /// Applies max speed via limiter for batch vehicles. (Fix #2/#3)
    /// Uses config gear count since there's no existing controller.
    /// </summary>
    private void ApplyBatchMaximumSpeedViaLimiter(RCCP_AIConfig.VehicleSetupConfig config, float targetSpeed) {
        config.engine.maximumSpeed = 0f;

        // Get gear count from config (batch vehicles don't have existing controllers)
        int gearCount = 0;
        if (config.gearbox != null && config.gearbox.gearRatios != null && config.gearbox.gearRatios.Length > 0) {
            gearCount = config.gearbox.gearRatios.Length;
        }

        if (gearCount <= 0) {
            // Fallback to engine max speed if no gearbox in config
            config.engine.maximumSpeed = targetSpeed;
            return;
        }

        float[] limitSpeeds = new float[gearCount];
        for (int i = 0; i < limitSpeeds.Length; i++)
            limitSpeeds[i] = -1f;

        limitSpeeds[limitSpeeds.Length - 1] = targetSpeed;

        if (config.limiter == null)
            config.limiter = new RCCP_AIConfig.LimiterConfig();

        config.limiter.enabled = true;
        config.limiter.remove = false;
        config.limiter.limitSpeedAtGear = limitSpeeds;
    }

    /// <summary>
    /// Applies max speed choice for batch customization, using the vehicle's existing gearbox
    /// when the config doesn't include gearbox data. This matches single-vehicle behavior.
    /// </summary>
    private void ApplyBatchMaximumSpeedChoiceForCustomization(RCCP_AIConfig.VehicleSetupConfig config, int choice, RCCP_CarController controller) {
        if (config?.engine == null || config.engine.maximumSpeed <= 0f)
            return;

        float targetSpeed = config.engine.maximumSpeed;

        switch (choice) {
            case 0:
                // Engine maximumSpeed (keep as-is)
                break;
            case 1:
                // Limiter (per gear) - use vehicle's existing gearbox if config doesn't have one
                ApplyBatchMaximumSpeedViaLimiterForCustomization(config, targetSpeed, controller);
                break;
            case 2:
                // Drivetrain only (clear max speed)
                config.engine.maximumSpeed = 0f;
                if (config.limiter != null) {
                    config.limiter.enabled = false;
                    config.limiter.limitSpeedAtGear = null;
                }
                break;
        }
    }

    /// <summary>
    /// Applies max speed via limiter for batch customization vehicles.
    /// Prefers the vehicle's existing gearbox gear count (matches single-vehicle behavior),
    /// falls back to config gearbox if vehicle has none.
    /// </summary>
    private void ApplyBatchMaximumSpeedViaLimiterForCustomization(RCCP_AIConfig.VehicleSetupConfig config, float targetSpeed, RCCP_CarController controller) {
        config.engine.maximumSpeed = 0f;

        // First try vehicle's existing gearbox (matches single-vehicle GetLimiterGearCount behavior)
        int gearCount = 0;
        if (controller != null) {
            RCCP_Gearbox existingGearbox = controller.GetComponentInChildren<RCCP_Gearbox>(true);
            if (existingGearbox != null && existingGearbox.gearRatios != null && existingGearbox.gearRatios.Length > 0) {
                gearCount = existingGearbox.gearRatios.Length;
            }
        }

        // Fall back to config gearbox if vehicle has no gearbox
        if (gearCount <= 0 && config.gearbox != null && config.gearbox.gearRatios != null && config.gearbox.gearRatios.Length > 0) {
            gearCount = config.gearbox.gearRatios.Length;
        }

        if (gearCount <= 0) {
            // Fallback to engine max speed if no gearbox anywhere
            config.engine.maximumSpeed = targetSpeed;
            return;
        }

        float[] limitSpeeds = new float[gearCount];
        for (int i = 0; i < limitSpeeds.Length; i++)
            limitSpeeds[i] = -1f;

        limitSpeeds[limitSpeeds.Length - 1] = targetSpeed;

        if (config.limiter == null)
            config.limiter = new RCCP_AIConfig.LimiterConfig();

        config.limiter.enabled = true;
        config.limiter.remove = false;
        config.limiter.limitSpeedAtGear = limitSpeeds;
    }

    private void ApplyVehicleCustomization(string json) {
        // Use pending apply target (stored when Generate was called) to handle selection changes
        RCCP_CarController targetController = pendingApplyController != null ? pendingApplyController : selectedController;

        if (targetController == null) throw new Exception("No RCCP vehicle selected");

        // Let OperationCanceledException propagate to ApplyConfiguration()
        PrepareVehicleForModification(targetController.gameObject);

        Undo.RegisterFullObjectHierarchyUndo(targetController.gameObject, "RCCP AI Customize");

        // Set history context before applying
        RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
            panelType = CurrentPrompt?.panelName ?? "Vehicle Customization",
            userPrompt = userPrompt,
            explanation = ExtractExplanation(json),
            appliedJson = json
        };

        try {
            var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(json);

            HandleMaximumSpeedSelection(config);

            // Check for behavior friction conflict before applying
            if (HasFrictionInVehicleConfig(config)) {
                var conflictInfo = RCCP_AIVehicleBuilder.CheckBehaviorFrictionConflict(targetController);

                if (conflictInfo.hasConflict) {
                    var resolution = ShowBehaviorFrictionConflictDialog(conflictInfo);

                    switch (resolution) {
                        case FrictionConflictResolution.Cancel:
                            if (isExecutingRefinement) {
                                RestoreFromRefinement();
                                // Throw to signal cancellation to ApplyConfiguration
                                throw new OperationCanceledException("Vehicle created (customization cancelled)");
                            } else {
                                throw new OperationCanceledException("Vehicle customization cancelled due to friction conflict");
                            }

                        case FrictionConflictResolution.ModifyBehaviorPreset:
                            ApplyWheelFrictionToBehaviorPreset(conflictInfo.activeBehavior, config.wheelFriction);
                            // Nullify friction so VehicleBuilder doesn't apply it to wheels
                            config.wheelFriction = null;
                            SetStatus($"Friction applied to '{conflictInfo.activeBehaviorName}' preset. Other changes applied.", MessageType.Info);
                            break;

                        case FrictionConflictResolution.DisableBehaviorForVehicle:
                            DisableBehaviorForVehicle(targetController);
                            // Proceed with normal application
                            break;
                    }
                }
            }

            RCCP_AIVehicleBuilder.CustomizeVehicle(targetController, config, false, false, json);
        } finally {
            // Always clear context, even on exception
            RCCP_AIVehicleBuilder.CurrentContext = null;
        }

        // Refresh selection to see changes in inspector
        RefreshSelection();
    }

    /// <summary>
    /// Apply customization to multiple vehicles (batch mode).
    /// Each vehicle gets its own AI-generated configuration based on its own state.
    /// This ensures "increase power by 20%" correctly applies to each vehicle's values.
    /// </summary>
    private void ApplyBatchVehicleCustomization() {
        if (batchCustomizationVehicles.Count == 0) {
            SetStatus("Error: No vehicles to customize", MessageType.Error);
            ClearBatchCustomizationState();
            return;
        }

        if (batchCustomizationResponses.Count == 0) {
            SetStatus("Error: No configurations to apply", MessageType.Error);
            ClearBatchCustomizationState();
            return;
        }

        // Preflight: Check for prefabs and ask once for all
        int prefabCount = 0;
        foreach (var controller in batchCustomizationVehicles) {
            if (controller != null && PrefabUtility.IsPartOfPrefabInstance(controller.gameObject))
                prefabCount++;
        }

        bool skipPrefabs = false;
        if (prefabCount > 0) {
            int choice = EditorUtility.DisplayDialogComplex(
                "Unpack Prefabs?",
                $"{prefabCount} of {batchCustomizationVehicles.Count} vehicles are prefab instances.\n\n" +
                "To modify them, they must be unpacked from their prefabs.\n\n" +
                "Choose how to proceed:",
                "Unpack All",
                "Cancel Batch",
                "Skip Prefabs"
            );

            if (choice == 1) {
                // Cancel
                SetStatus("Batch customization cancelled.", MessageType.Info);
                ClearBatchCustomizationState();
                return;
            }
            if (choice == 2) {
                // Skip prefabs
                skipPrefabs = true;
            }
            if (choice == 0) {
                // Unpack All - do it now so PrepareVehicleForModification won't ask again
                foreach (var controller in batchCustomizationVehicles) {
                    if (controller != null && PrefabUtility.IsPartOfPrefabInstance(controller.gameObject)) {
                        PrefabUtility.UnpackPrefabInstance(controller.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                        if (settings != null && settings.verboseLogging) Debug.Log($"[RCCP AI] Batch unpacked prefab: {controller.gameObject.name}");
                    }
                }
            }
        }

        // Check if any response contains max speed to show dialog once
        int batchSpeedChoice = 0;  // 0 = Drivetrain Only, 1 = Engine Max Speed, 2 = Limiter
        bool hasMaxSpeed = false;
        float sampleMaxSpeed = 0f;

        foreach (var kvp in batchCustomizationResponses) {
            string json = ExtractJson(kvp.Value);
            if (string.IsNullOrEmpty(json)) continue;
            try {
                var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(json);
                if (config?.engine?.maximumSpeed > 0) {
                    hasMaxSpeed = true;
                    sampleMaxSpeed = config.engine.maximumSpeed;
                    break;
                }
            } catch { }
        }

        if (hasMaxSpeed) {
            batchSpeedChoice = EditorUtility.DisplayDialogComplex(
                "Maximum Speed (Batch)",
                $"Maximum speed is configured (e.g., {sampleMaxSpeed} km/h).\n\n" +
                "Choose how to apply it to ALL vehicles:",
                "Drivetrain Only",
                "Engine Max Speed",
                "Limiter (Per Gear)"
            );
        }

        int undoGroup = Undo.GetCurrentGroup();
        int successCount = 0;
        int failCount = 0;
        int skippedCount = 0;

        foreach (RCCP_CarController controller in batchCustomizationVehicles) {
            if (controller == null) continue;

            // Skip prefabs if user chose "Skip Prefabs" in preflight dialog
            if (skipPrefabs && PrefabUtility.IsPartOfPrefabInstance(controller.gameObject)) {
                skippedCount++;
                if (settings != null && settings.verboseLogging) {
                    Debug.Log($"[RCCP AI] Skipped prefab: {controller.gameObject.name}");
                }
                continue;
            }

            // Get this vehicle's specific response
            if (!batchCustomizationResponses.TryGetValue(controller, out string vehicleResponse)) {
                skippedCount++;
                if (settings != null && settings.verboseLogging) {
                    Debug.LogWarning($"[RCCP AI] No response for {controller.gameObject.name}, skipping");
                }
                continue;
            }

            string json = ExtractJson(vehicleResponse);
            if (string.IsNullOrEmpty(json)) {
                failCount++;
                Debug.LogError($"[RCCP AI] Failed to extract JSON for {controller.gameObject.name}");
                continue;
            }

            try {
                // Prepare vehicle for modification (handles prefab unpack)
                PrepareVehicleForModification(controller.gameObject);

                // Register for undo
                Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "RCCP AI Batch Customize");

                // Parse this vehicle's specific config
                var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(json);
                if (config == null) throw new Exception("Failed to parse config");

                // Set history context for this vehicle
                RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
                    panelType = CurrentPrompt?.panelName ?? "Vehicle Customization",
                    userPrompt = batchCustomizationUserPrompt,
                    explanation = ExtractExplanation(json),
                    appliedJson = json
                };

                // Apply max-speed choice (pass controller for limiter gear count lookup)
                ApplyBatchMaximumSpeedChoiceForCustomization(config, batchSpeedChoice, controller);

                // Apply customization with this vehicle's specific config
                RCCP_AIVehicleBuilder.CustomizeVehicle(controller, config, false, false, json, skipRefreshSelection: true);

                successCount++;

                if (settings != null && settings.verboseLogging) {
                    Debug.Log($"[RCCP AI] Batch customized: {controller.gameObject.name} with vehicle-specific config");
                }
            }
            catch (OperationCanceledException) {
                failCount++;
            }
            catch (Exception e) {
                failCount++;
                Debug.LogError($"[RCCP AI] Failed to customize {controller.gameObject.name}: {e.Message}");
            }
            finally {
                RCCP_AIVehicleBuilder.CurrentContext = null;
            }
        }

        // Collapse all operations into single undo
        Undo.CollapseUndoOperations(undoGroup);

        // Report results
        if (failCount == 0 && skippedCount == 0) {
            SetStatus($"Successfully customized {successCount} vehicles!", MessageType.Info);
            if (enableAnimations) successFlashAlpha = 1f;
            ClearBatchCustomizationState();
            changesApplied = true;
        } else if (successCount > 0) {
            string msg = $"Customized {successCount} vehicles";
            if (failCount > 0) msg += $", {failCount} failed";
            if (skippedCount > 0) msg += $", {skippedCount} skipped";
            SetStatus(msg, MessageType.Warning);
            ClearBatchCustomizationState();
            changesApplied = true;
        } else {
            SetStatus($"Failed to customize all vehicles", MessageType.Error);
            // Keep state for retry
        }

        RefreshSelection();
        Repaint();
    }

    /// <summary>
    /// Applies lights configuration to all vehicles in batch mode.
    /// Called from the Apply button when Lights panel is in batch customization mode.
    /// </summary>
    private void ApplyBatchLights() {
        if (batchCustomizationVehicles.Count == 0) {
            SetStatus("Error: No vehicles to configure", MessageType.Error);
            isBatchCustomization = false;
            return;
        }

        if (string.IsNullOrEmpty(aiResponse)) {
            SetStatus("Error: No configuration to apply", MessageType.Error);
            isBatchCustomization = false;
            return;
        }

        string json = ExtractJson(aiResponse);
        if (string.IsNullOrEmpty(json)) {
            SetStatus("Error: Failed to extract configuration", MessageType.Error);
            isBatchCustomization = false;
            return;
        }

        Debug.Log($"[RCCP AI] Batch applying lights to {batchCustomizationVehicles.Count} vehicles");

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("RCCP AI Batch Lights");

        int successCount = 0;
        int failCount = 0;

        foreach (RCCP_CarController controller in batchCustomizationVehicles) {
            if (controller == null) continue;

            try {
                Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "RCCP AI Batch Lights");
                ApplyLightsToVehicle(controller, json);
                successCount++;

                if (settings.verboseLogging) {
                    Debug.Log($"[RCCP AI] Batch lights applied: {controller.gameObject.name}");
                }
            } catch (OperationCanceledException) {
                // User cancelled (e.g., prefab unpack dialog)
                failCount++;
                Debug.Log($"[RCCP AI] Batch lights cancelled for: {controller.gameObject.name}");
            } catch (Exception e) {
                failCount++;
                Debug.LogError($"[RCCP AI] Failed to apply lights to {controller.gameObject.name}: {e.Message}");
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        // Report results
        if (successCount > 0 && failCount == 0) {
            SetStatus($"Applied lights to {successCount} vehicles!", MessageType.Info);
            if (enableAnimations) successFlashAlpha = 1f;

            // Mark prompt history entry as applied
            if (!string.IsNullOrEmpty(lastPromptHistoryEntryId)) {
                RCCP_AIPromptHistory.MarkAsApplied(lastPromptHistoryEntryId);
            }

            // Clear batch state on success
            ClearBatchCustomizationState();
            changesApplied = true;
        } else if (successCount > 0) {
            SetStatus($"Lights applied to {successCount}, {failCount} failed", MessageType.Warning);

            // Mark prompt history entry as applied
            if (!string.IsNullOrEmpty(lastPromptHistoryEntryId)) {
                RCCP_AIPromptHistory.MarkAsApplied(lastPromptHistoryEntryId);
            }

            // Clear batch state on partial success
            ClearBatchCustomizationState();
            changesApplied = true;
        } else {
            // All failed - keep response and batch state for retry
            SetStatus($"Failed to apply lights to all {failCount} vehicles", MessageType.Error);
        }

        RefreshSelection();
        Repaint();
    }

    private void HandleMaximumSpeedSelection(RCCP_AIConfig.VehicleSetupConfig config) {
        if (config?.engine == null || config.engine.maximumSpeed <= 0f)
            return;

        float targetSpeed = config.engine.maximumSpeed;

        int choice = EditorUtility.DisplayDialogComplex(
            "Maximum Speed",
            $"This request sets maximum speed to {targetSpeed:F0} km/h.\n\nChoose how to apply it:",
            "Drivetrain Only",
            "Engine Max Speed",
            "Limiter (Per Gear)"
        );

        switch (choice) {
            case 0:
                // Drivetrain only (clear max speed)
                ApplyMaximumSpeedViaDrivetrain(config);
                break;
            case 1:
                // Engine maximumSpeed (final drive ratio target)
                break;
            case 2:
                ApplyMaximumSpeedViaLimiter(config, targetSpeed);
                break;
        }
    }

    private void ApplyMaximumSpeedViaLimiter(RCCP_AIConfig.VehicleSetupConfig config, float targetSpeed) {
        config.engine.maximumSpeed = 0f;

        int gearCount = GetLimiterGearCount(config);
        if (gearCount <= 0) {
            EditorUtility.DisplayDialog(
                "Limiter Requires Gearbox",
                "No gearbox gear ratios were found for this vehicle. Using Engine Max Speed instead.",
                "OK"
            );
            config.engine.maximumSpeed = targetSpeed;
            return;
        }

        float[] limitSpeeds = new float[gearCount];
        for (int i = 0; i < limitSpeeds.Length; i++)
            limitSpeeds[i] = -1f;

        limitSpeeds[limitSpeeds.Length - 1] = targetSpeed;

        if (config.limiter == null)
            config.limiter = new RCCP_AIConfig.LimiterConfig();

        config.limiter.enabled = true;
        config.limiter.remove = false;
        config.limiter.limitSpeedAtGear = limitSpeeds;
    }

    private void ApplyMaximumSpeedViaDrivetrain(RCCP_AIConfig.VehicleSetupConfig config) {
        config.engine.maximumSpeed = 0f;

        if (config.limiter != null) {
            config.limiter.enabled = false;
            config.limiter.remove = false;
            config.limiter.limitSpeedAtGear = null;
        }
    }

    private int GetLimiterGearCount(RCCP_AIConfig.VehicleSetupConfig config) {
        if (selectedController != null) {
            RCCP_Gearbox gearbox = selectedController.GetComponentInChildren<RCCP_Gearbox>(true);
            if (gearbox != null && gearbox.gearRatios != null && gearbox.gearRatios.Length > 0)
                return gearbox.gearRatios.Length;
        }

        if (config.gearbox != null && config.gearbox.gearRatios != null && config.gearbox.gearRatios.Length > 0)
            return config.gearbox.gearRatios.Length;

        return 0;
    }

    private void ApplyBehaviors(string json) {
        var rccp_settings = Resources.Load<RCCP_Settings>("RCCP_Settings");
        if (rccp_settings == null) throw new Exception("RCCP_Settings not found in Resources folder");
        if (rccp_settings.behaviorTypes == null || rccp_settings.behaviorTypes.Length == 0)
            throw new Exception("No behavior presets found in RCCP_Settings");

        Undo.RecordObject(rccp_settings, "RCCP AI Behavior");

        // Parse the behavior config from AI response
        var config = JsonUtility.FromJson<RCCP_AIConfig.BehaviorConfig>(json);
        if (config == null) throw new Exception("Failed to parse behavior configuration");

        string action = !string.IsNullOrEmpty(config.action) ? config.action.ToLower() : "switch";
        string behaviorName = config.behaviorName;

        switch (action) {
            case "switch":
                ApplyBehaviors_Switch(rccp_settings, behaviorName);
                break;
            case "modify":
                ApplyBehaviors_Modify(rccp_settings, config, json);
                break;
            case "create":
                ApplyBehaviors_Create(rccp_settings, config);
                break;
            case "disable":
                ApplyBehaviors_Disable(rccp_settings);
                break;
            case "enable":
                ApplyBehaviors_Enable(rccp_settings);
                break;
            default:
                // Default to switch if unknown action
                ApplyBehaviors_Switch(rccp_settings, behaviorName);
                break;
        }

        EditorUtility.SetDirty(rccp_settings);
        AssetDatabase.SaveAssets();
    }

    private void ApplyBehaviors_Switch(RCCP_Settings settings, string behaviorName) {
        // Validate behaviorName to prevent NullReferenceException
        if (string.IsNullOrEmpty(behaviorName)) {
            throw new Exception("Behavior name is null or empty. The AI response may be malformed.");
        }

        // Find behavior by name (case-insensitive)
        int foundIndex = -1;
        for (int i = 0; i < settings.behaviorTypes.Length; i++) {
            if (string.Equals(settings.behaviorTypes[i].behaviorName, behaviorName, StringComparison.OrdinalIgnoreCase)) {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex < 0) {
            // Try partial match (behaviorName already validated above)
            string lowerBehaviorName = behaviorName.ToLower();
            for (int i = 0; i < settings.behaviorTypes.Length; i++) {
                if (settings.behaviorTypes[i].behaviorName != null &&
                    settings.behaviorTypes[i].behaviorName.ToLower().Contains(lowerBehaviorName)) {
                    foundIndex = i;
                    break;
                }
            }
        }

        if (foundIndex < 0)
            throw new Exception($"Behavior preset '{behaviorName}' not found. Available: {string.Join(", ", settings.behaviorTypes.Select(b => b.behaviorName))}");

        // Enable override and select the behavior
        settings.overrideBehavior = true;
        settings.behaviorSelectedIndex = foundIndex;

        string selectedName = settings.behaviorTypes[foundIndex].behaviorName;
        SetStatus($"Switched to '{selectedName}' behavior. Override is now ENABLED - all vehicles will use this preset.", MessageType.Info);
    }

    private void ApplyBehaviors_Modify(RCCP_Settings settings, RCCP_AIConfig.BehaviorConfig config, string originalJson = null) {
        // Find behavior by name
        int foundIndex = -1;
        for (int i = 0; i < settings.behaviorTypes.Length; i++) {
            if (string.Equals(settings.behaviorTypes[i].behaviorName, config.behaviorName, StringComparison.OrdinalIgnoreCase)) {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex < 0)
            throw new Exception($"Behavior preset '{config.behaviorName}' not found for modification");

        var behavior = settings.behaviorTypes[foundIndex];

        // Create allTrue config for boolean detection (only for modify, not create)
        RCCP_AIConfig.BehaviorConfig configAllTrue = null;
        if (!string.IsNullOrEmpty(originalJson)) {
            configAllTrue = CreateAllTrueBehaviorConfig();
            JsonUtility.FromJsonOverwrite(originalJson, configAllTrue);
        }

        // Apply modifications from config with allTrue detection
        ApplyBehaviorSettings(behavior, config, configAllTrue);

        // Enable override and select the modified behavior
        settings.overrideBehavior = true;
        settings.behaviorSelectedIndex = foundIndex;

        SetStatus($"Modified '{behavior.behaviorName}' behavior. Override is now ENABLED - all vehicles will use this preset.", MessageType.Info);
    }

    private void ApplyBehaviors_Create(RCCP_Settings settings, RCCP_AIConfig.BehaviorConfig config) {
        // Create new BehaviorType
        var newBehavior = new RCCP_Settings.BehaviorType();

        newBehavior.behaviorName = !string.IsNullOrEmpty(config.behaviorName)
            ? config.behaviorName
            : "AI Generated Behavior";

        // Apply all settings from config
        ApplyBehaviorSettings(newBehavior, config);

        // Add the new behavior to the array
        var behaviors = new List<RCCP_Settings.BehaviorType>(settings.behaviorTypes);
        behaviors.Add(newBehavior);
        settings.behaviorTypes = behaviors.ToArray();

        // Enable override and select the new behavior
        settings.overrideBehavior = true;
        settings.behaviorSelectedIndex = settings.behaviorTypes.Length - 1;

        SetStatus($"Created '{newBehavior.behaviorName}' behavior. Override is now ENABLED - all vehicles will use this preset.", MessageType.Info);
    }

    private void ApplyBehaviors_Disable(RCCP_Settings settings) {
        // Disable the global behavior override so vehicles use their own settings
        settings.overrideBehavior = false;

        SetStatus("Behavior override DISABLED. Vehicles will now use their own individual behavior settings.", MessageType.Info);
    }

    private void ApplyBehaviors_Enable(RCCP_Settings settings) {
        // Enable the global behavior override so all vehicles use the selected preset
        settings.overrideBehavior = true;

        string currentBehavior = settings.behaviorSelectedIndex >= 0 && settings.behaviorSelectedIndex < settings.behaviorTypes.Length
            ? settings.behaviorTypes[settings.behaviorSelectedIndex].behaviorName
            : "Unknown";

        SetStatus($"Behavior override ENABLED. All vehicles will now use the '{currentBehavior}' preset.", MessageType.Info);
    }

    private void ApplyBehaviorSettings(RCCP_Settings.BehaviorType behavior, RCCP_AIConfig.BehaviorConfig config, RCCP_AIConfig.BehaviorConfig configAllTrue = null) {
        // Helper to safely apply booleans only when explicitly set in JSON
        // If configAllTrue is null (e.g., for "create" action), always apply the value
        void ApplyBool(ref bool field, bool value, bool allTrueValue) {
            if (configAllTrue == null) {
                // No allTrue config (create action) - apply directly
                field = value;
            } else if (value) {
                // Value is true - user explicitly set true
                field = true;
            } else if (!allTrueValue) {
                // Value is false AND allTrue is false - user explicitly set false
                field = false;
            }
            // Else: value is false AND allTrue is true - field was missing, don't change
        }

        // Stability systems
        ApplyBool(ref behavior.ABS, config.ABS, configAllTrue?.ABS ?? true);
        ApplyBool(ref behavior.ESP, config.ESP, configAllTrue?.ESP ?? true);
        ApplyBool(ref behavior.TCS, config.TCS, configAllTrue?.TCS ?? true);
        ApplyBool(ref behavior.steeringHelper, config.steeringHelper, configAllTrue?.steeringHelper ?? true);
        ApplyBool(ref behavior.tractionHelper, config.tractionHelper, configAllTrue?.tractionHelper ?? true);
        ApplyBool(ref behavior.angularDragHelper, config.angularDragHelper, configAllTrue?.angularDragHelper ?? true);

        // Drift settings
        ApplyBool(ref behavior.driftMode, config.driftMode, configAllTrue?.driftMode ?? true);
        ApplyBool(ref behavior.driftAngleLimiter, config.driftAngleLimiter, configAllTrue?.driftAngleLimiter ?? true);
        if (config.driftAngleLimit > 0)
            behavior.driftAngleLimit = Mathf.Clamp(config.driftAngleLimit, 0f, 90f);
        if (config.driftAngleCorrectionFactor > 0)
            behavior.driftAngleCorrectionFactor = Mathf.Clamp(config.driftAngleCorrectionFactor, 0f, 10f);

        // Steering
        if (config.steeringSensitivity > 0)
            behavior.steeringSensitivity = config.steeringSensitivity;
        ApplyBool(ref behavior.counterSteering, config.counterSteering, configAllTrue?.counterSteering ?? true);
        ApplyBool(ref behavior.limitSteering, config.limitSteering, configAllTrue?.limitSteering ?? true);

        // Steering curve (speed -> multiplier 0-1)
        if (config.steeringCurve != null && config.steeringCurve.HasValues) {
            AnimationCurve curve = config.steeringCurve.ToAnimationCurve();
            if (curve != null) {
                behavior.steeringCurve = curve;
            }
        }

        // Differential type
        if (!string.IsNullOrEmpty(config.differentialType)) {
            switch (config.differentialType.ToLower()) {
                case "open":
                    behavior.differentialType = RCCP_Differential.DifferentialType.Open;
                    break;
                case "limited":
                    behavior.differentialType = RCCP_Differential.DifferentialType.Limited;
                    break;
                case "fulllocked":
                case "locked":
                    behavior.differentialType = RCCP_Differential.DifferentialType.FullLocked;
                    break;
                case "direct":
                    behavior.differentialType = RCCP_Differential.DifferentialType.Direct;
                    break;
            }
        }

        // Helper strengths
        if (config.steeringHelperStrengthMin > 0)
            behavior.steeringHelperStrengthMinimum = Mathf.Clamp01(config.steeringHelperStrengthMin);
        if (config.steeringHelperStrengthMax > 0)
            behavior.steeringHelperStrengthMaximum = Mathf.Clamp01(config.steeringHelperStrengthMax);
        if (config.tractionHelperStrengthMin > 0)
            behavior.tractionHelperStrengthMinimum = Mathf.Clamp01(config.tractionHelperStrengthMin);
        if (config.tractionHelperStrengthMax > 0)
            behavior.tractionHelperStrengthMaximum = Mathf.Clamp01(config.tractionHelperStrengthMax);

        // Gear shifting
        if (config.gearShiftingThreshold > 0)
            behavior.gearShiftingThreshold = Mathf.Clamp(config.gearShiftingThreshold, 0.1f, 0.9f);

        // Wheel friction - Front
        if (config.forwardExtremumSlip_F > 0)
            behavior.forwardExtremumSlip_F = config.forwardExtremumSlip_F;
        if (config.forwardExtremumValue_F > 0)
            behavior.forwardExtremumValue_F = config.forwardExtremumValue_F;
        if (config.forwardAsymptoteSlip_F > 0)
            behavior.forwardAsymptoteSlip_F = config.forwardAsymptoteSlip_F;
        if (config.forwardAsymptoteValue_F > 0)
            behavior.forwardAsymptoteValue_F = config.forwardAsymptoteValue_F;
        if (config.sidewaysExtremumSlip_F > 0)
            behavior.sidewaysExtremumSlip_F = config.sidewaysExtremumSlip_F;
        if (config.sidewaysExtremumValue_F > 0)
            behavior.sidewaysExtremumValue_F = config.sidewaysExtremumValue_F;
        if (config.sidewaysAsymptoteSlip_F > 0)
            behavior.sidewaysAsymptoteSlip_F = config.sidewaysAsymptoteSlip_F;
        if (config.sidewaysAsymptoteValue_F > 0)
            behavior.sidewaysAsymptoteValue_F = config.sidewaysAsymptoteValue_F;

        // Wheel friction - Rear
        if (config.forwardExtremumSlip_R > 0)
            behavior.forwardExtremumSlip_R = config.forwardExtremumSlip_R;
        if (config.forwardExtremumValue_R > 0)
            behavior.forwardExtremumValue_R = config.forwardExtremumValue_R;
        if (config.forwardAsymptoteSlip_R > 0)
            behavior.forwardAsymptoteSlip_R = config.forwardAsymptoteSlip_R;
        if (config.forwardAsymptoteValue_R > 0)
            behavior.forwardAsymptoteValue_R = config.forwardAsymptoteValue_R;
        if (config.sidewaysExtremumSlip_R > 0)
            behavior.sidewaysExtremumSlip_R = config.sidewaysExtremumSlip_R;
        if (config.sidewaysExtremumValue_R > 0)
            behavior.sidewaysExtremumValue_R = config.sidewaysExtremumValue_R;
        if (config.sidewaysAsymptoteSlip_R > 0)
            behavior.sidewaysAsymptoteSlip_R = config.sidewaysAsymptoteSlip_R;
        if (config.sidewaysAsymptoteValue_R > 0)
            behavior.sidewaysAsymptoteValue_R = config.sidewaysAsymptoteValue_R;
    }

    /// <summary>
    /// Creates a BehaviorConfig with all booleans set to true for JSON detection.
    /// After FromJsonOverwrite, any boolean that is still true was missing from JSON.
    /// </summary>
    private static RCCP_AIConfig.BehaviorConfig CreateAllTrueBehaviorConfig() {
        return new RCCP_AIConfig.BehaviorConfig {
            // Stability systems
            ABS = true,
            ESP = true,
            TCS = true,
            steeringHelper = true,
            tractionHelper = true,
            angularDragHelper = true,
            // Drift settings
            driftMode = true,
            driftAngleLimiter = true,
            // Steering
            counterSteering = true,
            limitSteering = true
        };
    }

    #region Behavior Friction Conflict Resolution

    /// <summary>
    /// User's choice for resolving behavior/friction conflict.
    /// </summary>
    private enum FrictionConflictResolution {
        Cancel,                   // Don't apply changes
        ModifyBehaviorPreset,     // Write friction to the active behavior preset
        DisableBehaviorForVehicle // Set ineffectiveBehavior = true
    }

    /// <summary>
    /// Shows a dialog explaining the behavior/friction conflict and returns user's choice.
    /// </summary>
    private FrictionConflictResolution ShowBehaviorFrictionConflictDialog(
        RCCP_AIVehicleBuilder.BehaviorConflictInfo conflictInfo) {

        string title = "Behavior Preset Conflict";

        string source = conflictInfo.isGlobalOverride
            ? "Global Override (RCCP_Settings)"
            : "Per-Vehicle Custom Behavior";

        string message = $"The vehicle has an active behavior preset that will override your friction settings at runtime.\n\n" +
            $"Active Preset: {conflictInfo.activeBehaviorName}\n" +
            $"Source: {source}\n\n" +
            "Choose how to resolve this:\n\n" +
            "1. MODIFY PRESET - Write your friction values to the behavior preset.\n" +
            "   (All vehicles using this preset will share these friction values)\n\n" +
            "2. DISABLE BEHAVIOR - Make this vehicle ignore all behavior settings.\n" +
            "   (Only this vehicle's friction values will persist)\n\n" +
            "3. CANCEL - Don't apply friction changes.";

        // EditorUtility.DisplayDialogComplex returns:
        // 0 = OK (first button), 1 = Cancel (second button), 2 = Alt (third button)
        int choice = EditorUtility.DisplayDialogComplex(
            title,
            message,
            "Modify Preset",      // Button 0
            "Cancel",             // Button 1
            "Disable Behavior"    // Button 2
        );

        switch (choice) {
            case 0: return FrictionConflictResolution.ModifyBehaviorPreset;
            case 1: return FrictionConflictResolution.Cancel;
            case 2: return FrictionConflictResolution.DisableBehaviorForVehicle;
            default: return FrictionConflictResolution.Cancel;
        }
    }

    /// <summary>
    /// Applies friction values from WheelConfig to the specified behavior preset.
    /// Uses per-axle overrides if available (front/rear).
    /// </summary>
    private void ApplyFrictionToBehaviorPreset(
        RCCP_Settings.BehaviorType behavior,
        RCCP_AIConfig.WheelConfig wheelConfig) {

        var rccp_settings = Resources.Load<RCCP_Settings>("RCCP_Settings");
        if (rccp_settings == null) return;

        Undo.RecordObject(rccp_settings, "RCCP AI Modify Behavior Friction");

        // Front friction - use front override if available, else global config
        RCCP_AIConfig.FrictionCurveConfig frontForward = wheelConfig.front?.forwardFriction ?? wheelConfig.forwardFriction;
        RCCP_AIConfig.FrictionCurveConfig frontSideways = wheelConfig.front?.sidewaysFriction ?? wheelConfig.sidewaysFriction;

        if (frontForward != null && frontForward.HasValues) {
            behavior.forwardExtremumSlip_F = frontForward.extremumSlip;
            behavior.forwardExtremumValue_F = frontForward.extremumValue;
            behavior.forwardAsymptoteSlip_F = frontForward.asymptoteSlip;
            behavior.forwardAsymptoteValue_F = frontForward.asymptoteValue;
        }

        if (frontSideways != null && frontSideways.HasValues) {
            behavior.sidewaysExtremumSlip_F = frontSideways.extremumSlip;
            behavior.sidewaysExtremumValue_F = frontSideways.extremumValue;
            behavior.sidewaysAsymptoteSlip_F = frontSideways.asymptoteSlip;
            behavior.sidewaysAsymptoteValue_F = frontSideways.asymptoteValue;
        }

        // Rear friction - use rear override if available, else global config
        RCCP_AIConfig.FrictionCurveConfig rearForward = wheelConfig.rear?.forwardFriction ?? wheelConfig.forwardFriction;
        RCCP_AIConfig.FrictionCurveConfig rearSideways = wheelConfig.rear?.sidewaysFriction ?? wheelConfig.sidewaysFriction;

        if (rearForward != null && rearForward.HasValues) {
            behavior.forwardExtremumSlip_R = rearForward.extremumSlip;
            behavior.forwardExtremumValue_R = rearForward.extremumValue;
            behavior.forwardAsymptoteSlip_R = rearForward.asymptoteSlip;
            behavior.forwardAsymptoteValue_R = rearForward.asymptoteValue;
        }

        if (rearSideways != null && rearSideways.HasValues) {
            behavior.sidewaysExtremumSlip_R = rearSideways.extremumSlip;
            behavior.sidewaysExtremumValue_R = rearSideways.extremumValue;
            behavior.sidewaysAsymptoteSlip_R = rearSideways.asymptoteSlip;
            behavior.sidewaysAsymptoteValue_R = rearSideways.asymptoteValue;
        }

        EditorUtility.SetDirty(rccp_settings);
        AssetDatabase.SaveAssets();

        if (settings.verboseLogging) {
            Debug.Log($"[RCCP AI] Applied friction values to behavior preset: {behavior.behaviorName}");
        }
    }

    /// <summary>
    /// Applies WheelFrictionConfig (from VehicleSetupConfig) to behavior preset.
    /// This is used when customizing vehicles with wheelFriction config.
    /// </summary>
    private void ApplyWheelFrictionToBehaviorPreset(
        RCCP_Settings.BehaviorType behavior,
        RCCP_AIConfig.WheelFrictionConfig frictionConfig) {

        var rccp_settings = Resources.Load<RCCP_Settings>("RCCP_Settings");
        if (rccp_settings == null) return;

        Undo.RecordObject(rccp_settings, "RCCP AI Modify Behavior Friction");

        // Get friction values - either from custom curves or preset type
        RCCP_AIConfig.FrictionCurveConfig forward = frictionConfig.forward;
        RCCP_AIConfig.FrictionCurveConfig sideways = frictionConfig.sideways;

        // If using preset type, get the preset values
        if (!string.IsNullOrEmpty(frictionConfig.type)) {
            var preset = RCCP_AIConfig.FrictionPresets.GetPreset(frictionConfig.type);
            forward = forward ?? preset.forward;
            sideways = sideways ?? preset.sideways;
        }

        // Apply to both front and rear (vehicle customization doesn't distinguish per-axle)
        if (forward != null && forward.HasValues) {
            behavior.forwardExtremumSlip_F = forward.extremumSlip;
            behavior.forwardExtremumValue_F = forward.extremumValue;
            behavior.forwardAsymptoteSlip_F = forward.asymptoteSlip;
            behavior.forwardAsymptoteValue_F = forward.asymptoteValue;

            behavior.forwardExtremumSlip_R = forward.extremumSlip;
            behavior.forwardExtremumValue_R = forward.extremumValue;
            behavior.forwardAsymptoteSlip_R = forward.asymptoteSlip;
            behavior.forwardAsymptoteValue_R = forward.asymptoteValue;
        }

        if (sideways != null && sideways.HasValues) {
            behavior.sidewaysExtremumSlip_F = sideways.extremumSlip;
            behavior.sidewaysExtremumValue_F = sideways.extremumValue;
            behavior.sidewaysAsymptoteSlip_F = sideways.asymptoteSlip;
            behavior.sidewaysAsymptoteValue_F = sideways.asymptoteValue;

            behavior.sidewaysExtremumSlip_R = sideways.extremumSlip;
            behavior.sidewaysExtremumValue_R = sideways.extremumValue;
            behavior.sidewaysAsymptoteSlip_R = sideways.asymptoteSlip;
            behavior.sidewaysAsymptoteValue_R = sideways.asymptoteValue;
        }

        EditorUtility.SetDirty(rccp_settings);
        AssetDatabase.SaveAssets();

        if (settings.verboseLogging) {
            Debug.Log($"[RCCP AI] Applied friction values to behavior preset: {behavior.behaviorName}");
        }
    }

    /// <summary>
    /// Disables behavior influence for the specified vehicle.
    /// Sets ineffectiveBehavior = true so RCCP skips all behavior application.
    /// </summary>
    private void DisableBehaviorForVehicle(RCCP_CarController carController) {
        Undo.RecordObject(carController, "RCCP AI Disable Behavior");
        carController.ineffectiveBehavior = true;
        EditorUtility.SetDirty(carController);

        if (settings.verboseLogging) {
            Debug.Log($"[RCCP AI] Disabled behavior for vehicle: {carController.name}");
        }
    }

    /// <summary>
    /// Checks if the WheelConfig contains friction values that would be affected by behavior.
    /// </summary>
    private bool HasFrictionInWheelConfig(RCCP_AIConfig.WheelConfig config) {
        if (config == null) return false;

        // Check global friction
        if (config.forwardFriction != null && config.forwardFriction.HasValues) return true;
        if (config.sidewaysFriction != null && config.sidewaysFriction.HasValues) return true;

        // Check front axle friction
        if (config.front != null) {
            if (config.front.forwardFriction != null && config.front.forwardFriction.HasValues) return true;
            if (config.front.sidewaysFriction != null && config.front.sidewaysFriction.HasValues) return true;
        }

        // Check rear axle friction
        if (config.rear != null) {
            if (config.rear.forwardFriction != null && config.rear.forwardFriction.HasValues) return true;
            if (config.rear.sidewaysFriction != null && config.rear.sidewaysFriction.HasValues) return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if VehicleSetupConfig contains friction values.
    /// </summary>
    private bool HasFrictionInVehicleConfig(RCCP_AIConfig.VehicleSetupConfig config) {
        if (config?.wheelFriction == null) return false;

        if (config.wheelFriction.forward != null && config.wheelFriction.forward.HasValues) return true;
        if (config.wheelFriction.sideways != null && config.wheelFriction.sideways.HasValues) return true;
        if (!string.IsNullOrEmpty(config.wheelFriction.type)) return true;

        return false;
    }

    #endregion

    private void ApplyWheels(string json) {
        if (!HasRCCPController) throw new Exception("No RCCP vehicle selected");

        // Let OperationCanceledException propagate to ApplyConfiguration()
        PrepareVehicleForModification(selectedController.gameObject);

        Undo.RegisterFullObjectHierarchyUndo(selectedController.gameObject, "RCCP AI Wheels");

        var config = JsonUtility.FromJson<RCCP_AIConfig.WheelConfig>(json);
        if (config == null) throw new Exception("Failed to parse wheel configuration");

        // Set history context for vehicle history tracking
        RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
            panelType = CurrentPrompt?.panelName ?? "Wheels and Tires",
            userPrompt = userPrompt,
            explanation = config.explanation ?? "",
            appliedJson = json
        };

        // Capture state before applying for history
        string beforeState = RCCP_AIVehicleBuilder.CaptureVehicleState(selectedController);
        string beforeStateJson = RCCP_AIVehicleBuilder.CaptureVehicleStateAsJson(selectedController);

        try {
            // Check for behavior friction conflict before applying
            bool skipFrictionApplication = false;
            string frictionConflictStatus = null;  // Track if we resolved a conflict
            if (HasFrictionInWheelConfig(config)) {
                var conflictInfo = RCCP_AIVehicleBuilder.CheckBehaviorFrictionConflict(selectedController);

                if (conflictInfo.hasConflict) {
                    var resolution = ShowBehaviorFrictionConflictDialog(conflictInfo);

                    switch (resolution) {
                        case FrictionConflictResolution.Cancel:
                            throw new OperationCanceledException("Wheel friction changes cancelled due to behavior conflict");

                        case FrictionConflictResolution.ModifyBehaviorPreset:
                            ApplyFrictionToBehaviorPreset(conflictInfo.activeBehavior, config);
                            skipFrictionApplication = true;  // Don't apply friction to wheels directly
                            frictionConflictStatus = $"Friction applied to '{conflictInfo.activeBehaviorName}' behavior preset";
                            break;

                        case FrictionConflictResolution.DisableBehaviorForVehicle:
                            DisableBehaviorForVehicle(selectedController);
                            // Continue to apply friction directly to wheels
                            break;
                    }
                }
            }

            // If friction was redirected to behavior preset, clear friction from config
            // so ApplyWheelAlignmentAndFriction doesn't apply it directly to wheels
            if (skipFrictionApplication) {
                config.forwardFriction = null;
                config.sidewaysFriction = null;
                if (config.front != null) {
                    config.front.forwardFriction = null;
                    config.front.sidewaysFriction = null;
                }
                if (config.rear != null) {
                    config.rear.forwardFriction = null;
                    config.rear.sidewaysFriction = null;
                }
            }

            // Delegate to VehicleBuilder for the actual work
            int wheelsModified = RCCP_AIVehicleBuilder.ApplyWheelAlignmentAndFriction(selectedController, config);

            if (wheelsModified == 0)
                throw new Exception("No wheels were modified - check vehicle configuration");

            // Capture state after applying and log history
            string afterState = RCCP_AIVehicleBuilder.CaptureVehicleState(selectedController);
            RCCP_AIVehicleBuilder.LogHistory(
                selectedController.gameObject,
                beforeState,
                beforeStateJson,
                afterState,
                config.explanation ?? ""
            );

            // Show appropriate status message
            if (frictionConflictStatus != null) {
                // Include both friction resolution and wheel modification info
                SetStatus($"{frictionConflictStatus}. Modified {wheelsModified} wheel(s) (other settings)", MessageType.Info);
            } else {
                SetStatus($"Modified {wheelsModified} wheel(s)", MessageType.Info);
            }
        } finally {
            // Always clear context
            RCCP_AIVehicleBuilder.CurrentContext = null;
        }
    }

    private void ApplyLights(string json) {
        if (!HasRCCPController) throw new Exception("No RCCP vehicle selected");
        ApplyLightsToVehicle(selectedController, json);

        // Set status message for single vehicle apply (batch apply sets its own status)
        var config = JsonUtility.FromJson<RCCP_AIConfig.LightsConfig>(json);
        int lightsCount = config?.lights?.Length ?? 0;
        SetStatus($"Applied lights configuration ({lightsCount} light types)", MessageType.Info);
    }

    /// <summary>
    /// Applies lights configuration to a specific vehicle controller.
    /// Used for batch operations where selectedController may differ from target.
    /// </summary>
    /// <param name="controller">The vehicle to apply lights to</param>
    /// <param name="json">The lights configuration JSON</param>
    private void ApplyLightsToVehicle(RCCP_CarController controller, string json) {
        if (controller == null) throw new Exception("No vehicle controller provided");

        // Let OperationCanceledException propagate to caller
        PrepareVehicleForModification(controller.gameObject);

        Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "RCCP AI Lights");

        var config = JsonUtility.FromJson<RCCP_AIConfig.LightsConfig>(json);
        if (config == null) throw new Exception("Failed to parse lights configuration");
        if (config.lights == null || config.lights.Length == 0)
            throw new Exception("No lights specified in configuration");

        // Set history context for vehicle history tracking
        RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
            panelType = CurrentPrompt?.panelName ?? "Lights Setup",
            userPrompt = userPrompt,
            explanation = config.explanation ?? "",
            appliedJson = json
        };

        // Capture state before applying for history
        string beforeState = RCCP_AIVehicleBuilder.CaptureVehicleState(controller);
        string beforeStateJson = RCCP_AIVehicleBuilder.CaptureVehicleStateAsJson(controller);

        try {
            // Delegate to VehicleBuilder for the actual work
            var (lightsCreated, lightsModified) = RCCP_AIVehicleBuilder.ApplyLightsSettings(controller, config);

            if (lightsCreated == 0 && lightsModified == 0)
                throw new Exception("No valid light types found in configuration");

            // Capture state after applying and log history
            string afterState = RCCP_AIVehicleBuilder.CaptureVehicleState(controller);
            RCCP_AIVehicleBuilder.LogHistory(
                controller.gameObject,
                beforeState,
                beforeStateJson,
                afterState,
                config.explanation ?? ""
            );

            if (settings.verboseLogging) {
                string status = lightsCreated > 0
                    ? $"[RCCP AI] {controller.gameObject.name}: Created {lightsCreated} and modified {lightsModified} light(s)"
                    : $"[RCCP AI] {controller.gameObject.name}: Modified {lightsModified} light(s)";
                Debug.Log(status);
            }
        } finally {
            // Always clear context
            RCCP_AIVehicleBuilder.CurrentContext = null;
        }
    }

    private void ApplyDamage(string json) {
        if (!HasRCCPController) throw new Exception("No RCCP vehicle selected");

        // Let OperationCanceledException propagate to ApplyConfiguration()
        PrepareVehicleForModification(selectedController.gameObject);

        Undo.RegisterFullObjectHierarchyUndo(selectedController.gameObject, "RCCP AI Damage");

        var config = JsonUtility.FromJson<RCCP_AIConfig.DamageConfig>(json);
        if (config == null) throw new Exception("Failed to parse damage configuration");

        // Create allTrue config for boolean detection
        var configAllTrue = CreateAllTrueDamageConfig();
        JsonUtility.FromJsonOverwrite(json, configAllTrue);

        // Delegate to VehicleBuilder for the actual work
        int partsCreated = RCCP_AIVehicleBuilder.ApplyDamageSettings(selectedController, config, configAllTrue);

        string status = config.meshDeformation
            ? $"Damage configured: max={config.maximumDamage:F2}, radius={config.deformationRadius:F2}"
            : "Damage settings applied";
        if (partsCreated > 0)
            status += $", {partsCreated} detachable parts configured";
        SetStatus(status, MessageType.Info);
    }

    /// <summary>
    /// Creates a DamageConfig with all booleans set to true for JSON detection.
    /// After FromJsonOverwrite, any boolean that is still true was missing from JSON.
    /// </summary>
    private static RCCP_AIConfig.DamageConfig CreateAllTrueDamageConfig() {
        return new RCCP_AIConfig.DamageConfig {
            meshDeformation = true,
            automaticInstallation = true,
            wheelDamage = true,
            wheelDetachment = true,
            lightDamage = true,
            partDamage = true
        };
    }

    private void ApplyAudio(string json) {
        if (!HasRCCPController) throw new Exception("No RCCP vehicle selected");

        // Let OperationCanceledException propagate to ApplyConfiguration()
        PrepareVehicleForModification(selectedController.gameObject);

        Undo.RegisterFullObjectHierarchyUndo(selectedController.gameObject, "RCCP AI Audio");

        var config = JsonUtility.FromJson<RCCP_AIConfig.AudioConfig>(json);
        if (config == null) throw new Exception("Failed to parse audio configuration");

        // Set history context for vehicle history tracking
        RCCP_AIVehicleBuilder.CurrentContext = new RCCP_AIVehicleBuilder.HistoryContext {
            panelType = CurrentPrompt?.panelName ?? "Audio Setup",
            userPrompt = userPrompt,
            explanation = config.explanation ?? "",
            appliedJson = json
        };

        // Capture state before applying for history
        string beforeState = RCCP_AIVehicleBuilder.CaptureVehicleState(selectedController);
        string beforeStateJson = RCCP_AIVehicleBuilder.CaptureVehicleStateAsJson(selectedController);

        try {
            // Delegate to VehicleBuilder for the actual work
            int layersModified = RCCP_AIVehicleBuilder.ApplyAudioSettings(selectedController, config);

            if (layersModified == 0)
                throw new Exception("No engine sound layers were modified - check configuration");

            // Capture state after applying and log history
            string afterState = RCCP_AIVehicleBuilder.CaptureVehicleState(selectedController);
            RCCP_AIVehicleBuilder.LogHistory(
                selectedController.gameObject,
                beforeState,
                beforeStateJson,
                afterState,
                config.explanation ?? ""
            );

            SetStatus($"Modified {layersModified} engine sound layer(s)", MessageType.Info);
        } finally {
            // Always clear context
            RCCP_AIVehicleBuilder.CurrentContext = null;
        }
    }

    private void ApplyGeneric(string json) {
        // Generic panel - no specific apply logic, just show in developer mode
        if (developerMode)
            Debug.Log($"[RCCP AI] Generic response: {json}");
    }

    #endregion

    #region Create Default Settings

    private void CreateDefaultSettings() {
        // Use dynamic paths from RCCP_AIUtility
        string resourcesPath = RCCP_AIUtility.ResourcesPath;
        string promptsPath = RCCP_AIUtility.PromptsPath;

        // Ensure folder structure exists
        RCCP_AIUtility.EnsureFolderStructure();

        var newSettings = ScriptableObject.CreateInstance<RCCP_AISettings>();
        AssetDatabase.CreateAsset(newSettings, RCCP_AIUtility.SettingsAssetPath);

        var samplePrompt = ScriptableObject.CreateInstance<RCCP_AIPromptAsset>();
        samplePrompt.panelName = "Vehicle Creation";
        samplePrompt.panelIcon = "🚗";
        samplePrompt.panelDescription = "Create complete RCCP vehicle configurations from natural language descriptions.";
        samplePrompt.panelType = RCCP_AIPromptAsset.PanelType.VehicleCreation;
        samplePrompt.requiresVehicle = true;
        samplePrompt.includeMeshAnalysis = true;
        samplePrompt.systemPrompt = "You are RCCP Vehicle Creation Assistant.\n\nRESPOND WITH ONLY JSON.";
        samplePrompt.examplePrompts = new[] {
            "Create a regular new vehicle",
            "Create a drift car with loose rear end",
            "Create a realistic family sedan",
            "Create a track-focused race car",
            "Create an AWD rally vehicle"
        };
        AssetDatabase.CreateAsset(samplePrompt, promptsPath + "/Prompt_VehicleCreation.asset");

        // Create Diagnostics prompt (special panel - no AI needed)
        var diagnosticsPrompt = ScriptableObject.CreateInstance<RCCP_AIPromptAsset>();
        diagnosticsPrompt.panelName = "Diagnostics";
        diagnosticsPrompt.panelIcon = "🩺";
        diagnosticsPrompt.panelDescription = "Check vehicle configuration for issues and get suggestions for fixes.";
        diagnosticsPrompt.panelType = RCCP_AIPromptAsset.PanelType.Diagnostics;
        diagnosticsPrompt.requiresVehicle = false;  // Handled specially in UI
        diagnosticsPrompt.requiresRCCPController = false;  // Handled specially in UI
        diagnosticsPrompt.systemPrompt = "";  // Not needed for diagnostics
        diagnosticsPrompt.examplePrompts = new string[0];  // No quick prompts needed
        AssetDatabase.CreateAsset(diagnosticsPrompt, promptsPath + "/Prompt_Diagnostics.asset");

        newSettings.prompts = new[] { samplePrompt, diagnosticsPrompt };
        EditorUtility.SetDirty(newSettings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LoadSettings();
        SetStatus("Settings created! Configure prompts in Resources/Prompts/", MessageType.Info);
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
