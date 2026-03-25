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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

namespace BoneCrackerGames.RCCP.AIAssistant {

public partial class RCCP_AIAssistantWindow {

    #region API

    /// <summary>
    /// Generates a vehicle configuration using Quick Create mode.
    /// Uses a fixed prompt optimized for automatic vehicle type detection.
    /// </summary>
    private void GenerateQuickCreate() {
        // Check if batch mode (multiple vehicles selected)
        bool isVehicleCreationPanel = CurrentPrompt != null && CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        if (hasMultipleSelection && isVehicleCreationPanel && batchVehicles.Count > 0) {
            // Batch mode: use a generic prompt that lets each vehicle's name and mesh analysis drive the configuration
            userPrompt = "Create a realistic vehicle configuration based on the vehicle name and mesh analysis. " +
                         "Determine the vehicle type from the name and dimensions, then optimize drivetrain, suspension, " +
                         "and handling for that specific vehicle type. Use appropriate defaults.";
        } else {
            // Single vehicle: use detected type for a more specific prompt
            string vehicleType = GetDetectedVehicleType();

            userPrompt = $"Create a realistic {vehicleType.ToLower()} configuration based on the mesh analysis. " +
                         $"Optimize drivetrain, suspension, and handling for a typical {vehicleType.ToLower()}. " +
                         $"Use appropriate defaults for this vehicle type.";
        }

        // Call the standard Generate method
        Generate();
    }

    private void Generate() {
        var aiSettings = RCCP_AISettings.Instance;

        // Check if using server proxy mode
        bool useProxy = aiSettings != null && aiSettings.useServerProxy;

        if (CurrentPrompt == null) {
            SetStatus("Error: No prompt configured", MessageType.Error);
            return;
        }

        // API key is only required when NOT using server proxy
        if (!useProxy && string.IsNullOrEmpty(apiKey)) {
            SetStatus("Error: No API key configured", MessageType.Error);
            return;
        }

        // Server proxy mode - ensure device is registered
        if (useProxy) {
            if (!RCCP_ServerProxy.IsRegistered) {
                SetStatus("Registering device with server...", MessageType.Info);
                isProcessing = true;
                RCCP_ServerProxy.RegisterDevice(this, (success, message) => {
                    isProcessing = false;
                    if (success) {
                        // Registration successful, retry Generate
                        Generate();
                    } else {
                        SetStatus($"Registration failed: {message}", MessageType.Error);
                    }
                    Repaint();
                });
                return;
            }

        }
        // Server enforces rate limits - no local pre-flight check needed

        // Check for batch mode (Vehicle Creation with multiple selection)
        bool isVehicleCreationPanel = CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCreation;
        bool isCustomizationPanel = CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.VehicleCustomization;
        bool isLightsPanel = CurrentPrompt.panelType == RCCP_AIPromptAsset.PanelType.Lights;

        if (hasMultipleSelection && isVehicleCreationPanel && batchVehicles.Count > 0) {
            // Start batch processing for vehicle creation
            StartBatchProcessing();
            return;
        }

        // Batch customization - process each vehicle individually (like Vehicle Creation)
        // This ensures each vehicle gets AI response based on its own current state
        bool supportsBatch = isCustomizationPanel || isLightsPanel;
        if (hasMultipleSelection && supportsBatch && batchCustomizationVehicles.Count == 0) {
            foreach (GameObject go in Selection.gameObjects) {
                RCCP_CarController controller = RCCP_AIUtility.GetRCCPController(go);
                if (controller != null && !batchCustomizationVehicles.Contains(controller)) {
                    batchCustomizationVehicles.Add(controller);
                }
            }
            if (settings.verboseLogging && batchCustomizationVehicles.Count > 0) {
                Debug.Log($"[RCCP AI] Batch list populated on Generate: {batchCustomizationVehicles.Count} vehicles");
            }
        }
        if (hasMultipleSelection && supportsBatch && batchCustomizationVehicles.Count > 0) {
            // Start batch processing - each vehicle gets its own AI request with its own state
            StartBatchCustomizationProcessing();
            return;
        }

        // Block other panels with multiple selection (except customization and lights which are handled above)
        if (hasMultipleSelection && !supportsBatch) {
            SetStatus("Error: Please select only one object", MessageType.Error);
            return;
        }

        if (CurrentPrompt.requiresVehicle && selectedVehicle == null) {
            SetStatus("Error: Please select a vehicle", MessageType.Error);
            return;
        }

        if (CurrentPrompt.requiresVehicle && !CurrentPrompt.requiresRCCPController && !isSelectionInScene) {
            SetStatus("Error: Drag the model into the Scene first", MessageType.Error);
            return;
        }

        if (CurrentPrompt.requiresRCCPController && !HasRCCPController) {
            SetStatus("Error: Select a vehicle with RCCP_CarController", MessageType.Error);
            return;
        }

        // Add to recent prompts for quick access later
        AddToRecentPrompts(userPrompt);

        // Store the current selection as the pending apply target
        // This ensures Apply uses the correct vehicle even if selection changes
        pendingApplyVehicle = selectedVehicle;
        pendingApplyController = selectedController;
        pendingEligibility = currentEligibility;  // Store wheel detection for vehicle creation

        isProcessing = true;
        requestStartTime = (float)EditorApplication.timeSinceStartup;
        currentRetryCount = 0;  // Reset retry counter
        SetStatus("Generating configuration...", MessageType.Info);
        aiResponse = "";
        showPreview = false;
        changesApplied = false;  // Reset applied flag for new generation

        // Capture before state for comparison (only for customization of existing vehicles)
        if (HasRCCPController) {
            beforeStateSnapshot = RCCP_AIVehicleBuilder.CaptureVehicleState(selectedController);
        } else {
            beforeStateSnapshot = "";
        }

        currentRequestCoroutine = EditorCoroutineUtility.StartCoroutine(SendRequest(), this);
    }

    /// <summary>
    /// Starts batch processing for multiple vehicle creation.
    /// Processes each vehicle sequentially with individual AI requests.
    /// </summary>
    private void StartBatchProcessing() {
        if (string.IsNullOrEmpty(userPrompt)) {
            SetStatus("Error: Please enter a description for the vehicles", MessageType.Error);
            return;
        }

        var aiSettings = RCCP_AISettings.Instance;
        bool useProxy = aiSettings != null && aiSettings.useServerProxy;

        // Server proxy mode - ensure device is registered (same as Generate())
        if (useProxy) {
            if (!RCCP_ServerProxy.IsRegistered) {
                SetStatus("Registering device with server...", MessageType.Info);
                isProcessing = true;
                RCCP_ServerProxy.RegisterDevice(this, (success, message) => {
                    isProcessing = false;
                    if (success) {
                        // Registration successful, retry StartBatchProcessing
                        StartBatchProcessing();
                    } else {
                        SetStatus($"Registration failed: {message}", MessageType.Error);
                    }
                    Repaint();
                });
                return;
            }

        }
        // Server enforces rate limits - no local pre-flight check needed

        // Store the prompt for batch use
        batchUserPrompt = userPrompt;
        batchResponses.Clear();
        currentBatchIndex = 0;
        isBatchProcessing = true;
        isProcessing = true;

        // Add to recent prompts
        AddToRecentPrompts(userPrompt);

        SetStatus($"Processing batch: 1/{batchVehicles.Count}", MessageType.Info);
        requestStartTime = (float)EditorApplication.timeSinceStartup;

        currentRequestCoroutine = EditorCoroutineUtility.StartCoroutine(ProcessBatchRequests(), this);
    }

    /// <summary>
    /// Coroutine that processes each vehicle in the batch sequentially.
    /// </summary>
    private IEnumerator ProcessBatchRequests() {
        try {
            while (currentBatchIndex < batchVehicles.Count && isBatchProcessing) {
                GameObject currentVehicle = batchVehicles[currentBatchIndex];
                if (currentVehicle == null) {
                    currentBatchIndex++;
                    continue;
                }

                // Server enforces rate limits - will return 429 if limit reached
                SetStatus($"Processing {currentBatchIndex + 1}/{batchVehicles.Count}: {currentVehicle.name}", MessageType.Info);
                Repaint();

                // Get mesh analysis for this specific vehicle
                string vehicleMeshAnalysis = batchMeshAnalysis.ContainsKey(currentVehicle)
                    ? batchMeshAnalysis[currentVehicle]
                    : AnalyzeMeshForObject(currentVehicle);

                // Build prompt with this vehicle's mesh analysis and name
                string fullUserPrompt = BuildBatchUserPrompt(batchUserPrompt, vehicleMeshAnalysis, currentVehicle.name);

                // Send request for this vehicle
                yield return SendBatchRequest(currentVehicle, fullUserPrompt);

                // Check if window was closed during yield
                if (this == null) yield break;

                currentBatchIndex++;
                requestStartTime = (float)EditorApplication.timeSinceStartup;  // Reset timeout for next request

                // Add delay between requests when using server proxy to respect rate limits
                // The server enforces a cooldown between requests to prevent abuse
                var aiSettings = RCCP_AISettings.Instance;
                bool useProxy = aiSettings != null && aiSettings.useServerProxy;
                if (useProxy && currentBatchIndex < batchVehicles.Count && batchResponses.ContainsKey(currentVehicle)) {
                    SetStatus($"Waiting for rate limit... ({currentBatchIndex}/{batchVehicles.Count} complete)", MessageType.Info);
                    Repaint();
                    yield return new EditorWaitForSeconds(BATCH_REQUEST_DELAY_SECONDS);

                    // Check if window was closed during delay
                    if (this == null) yield break;
                }
            }

            // Check if window still exists before updating UI
            if (this == null) yield break;

            // Batch complete - allow partial success (Apply button shown when batchResponses.Count > 0)
            if (batchResponses.Count > 0) {
                bool allSucceeded = batchResponses.Count == batchVehicles.Count;

                if (allSucceeded && autoApply) {
                    // Auto-apply only when ALL vehicles succeeded
                    SetStatus($"Batch complete! Auto-applying {batchVehicles.Count} vehicles...", MessageType.Info);
                    Repaint();
                    // Defer to next frame to allow UI to update
                    EditorApplication.delayCall += ApplyBatchVehicleCreation;
                } else if (allSucceeded) {
                    SetStatus($"Batch complete! {batchVehicles.Count} vehicles configured. Click 'Apply All' to create.", MessageType.Info);
                } else {
                    // Partial success - still allow Apply (button shows count)
                    SetStatus($"Batch completed: {batchResponses.Count}/{batchVehicles.Count} ready to apply. Click 'Apply All' to create successful vehicles.", MessageType.Warning);
                }
            } else {
                SetStatus("Batch failed: No vehicles were configured successfully.", MessageType.Error);
            }

            // Check for pending rate limit dialogs (e.g., setup pool exhausted during batch)
            if (RCCP_AIRateLimiter.CheckAndShowPendingDialogs()) {
                showSettings = true;
                Repaint();
            }
        }
        finally {
            // ALWAYS reset state, even on exception
            isBatchProcessing = false;
            isProcessing = false;
            currentRequestCoroutine = null;
            if (this != null) Repaint();
        }
    }

    private string BuildBatchUserPrompt(string prompt, string vehicleMeshAnalysis, string vehicleName) {
        StringBuilder sb = new StringBuilder();

        // Add mode instruction (matching BuildFullUserPrompt behavior)
        if (promptMode == RCCP_AIConfig.PromptMode.Ask) {
            sb.AppendLine("[USER INTENT: QUESTION]");
            sb.AppendLine("The user is asking a question or seeking explanation about RCCP settings.");
            sb.AppendLine();
        } else {
            sb.AppendLine("[USER INTENT: CONFIGURATION REQUEST]");
            sb.AppendLine("The user wants to configure or modify vehicle settings. Respond with ONLY valid JSON that can be applied.");
            sb.AppendLine();
        }

        // Include vehicle name so the AI knows which specific vehicle it is configuring
        if (!string.IsNullOrEmpty(vehicleName)) {
            sb.AppendLine($"Vehicle Name: \"{vehicleName}\"");
            sb.AppendLine("Use the vehicle name as a hint for the vehicle type and configure accordingly.");
            sb.AppendLine();
        }

        sb.AppendLine(prompt);

        if (CurrentPrompt.includeMeshAnalysis && !string.IsNullOrEmpty(vehicleMeshAnalysis)) {
            sb.AppendLine();
            sb.AppendLine("Mesh Analysis:");
            sb.AppendLine(vehicleMeshAnalysis);
        }

        return sb.ToString();
    }

    private IEnumerator SendBatchRequest(GameObject vehicle, string fullUserPrompt) {
        var aiSettings = RCCP_AISettings.Instance;

        // Check if should use server proxy (same as SendRequest)
        if (aiSettings != null && aiSettings.useServerProxy) {
            yield return SendBatchRequestViaProxy(vehicle, fullUserPrompt);
            yield break;
        }

        // Direct API mode (user's own key)
        yield return SendBatchRequestDirect(vehicle, fullUserPrompt);
    }

    /// <summary>
    /// Sends batch request via the server proxy (protects API key on server).
    /// </summary>
    private IEnumerator SendBatchRequestViaProxy(GameObject vehicle, string fullUserPrompt) {
        string systemPrompt = settings != null
            ? settings.GetFullSystemPrompt(CurrentPrompt)
            : CurrentPrompt.systemPrompt;

        var aiSettings = RCCP_AISettings.Instance;

        bool responseReceived = false;
        RCCP_ServerProxy.QueryResult queryResult = null;

        RCCP_ServerProxy.SendQuery(
            this,
            aiSettings.textModel,
            aiSettings.maxTokens,
            systemPrompt,
            fullUserPrompt,
            "BatchVehicleCreation",
            (result) => {
                queryResult = result;
                responseReceived = true;
            }
        );

        // Wait for response
        float startTime = (float)EditorApplication.timeSinceStartup;
        while (!responseReceived) {
            if (EditorApplication.timeSinceStartup - startTime > aiSettings.serverTimeout) {
                Debug.LogError($"Batch request timed out for {vehicle?.name ?? "Unknown"}");
                yield break;
            }
            yield return null;
        }

        if (queryResult != null && queryResult.Success) {
            batchResponses[vehicle] = queryResult.Content;

            // Sync usage from server response
            if (queryResult.Usage != null) {
                RCCP_AIRateLimiter.SyncFromServer(queryResult.Usage);
            }

            // Save to prompt history
            SaveToPromptHistory($"[Batch] {batchUserPrompt} - {vehicle?.name ?? "Unknown"}", queryResult.Content, false);
        } else {
            if (queryResult?.Error != null &&
                (queryResult.Error.Contains("Invalid device token") || queryResult.Error.Contains("invalid token"))) {
                RCCP_ServerProxy.ClearRegistration();
                Debug.LogWarning($"[RCCP AI] Device session expired during batch for {vehicle?.name}. Please try again.");
            } else {
                Debug.LogError($"Batch API Error for {vehicle?.name ?? "Unknown"}: {queryResult?.Error ?? "Unknown error"}");
            }
        }

        Repaint();
    }

    /// <summary>
    /// Sends batch request directly to Claude API (user's own key).
    /// </summary>
    private IEnumerator SendBatchRequestDirect(GameObject vehicle, string fullUserPrompt) {
        string systemPrompt = settings != null
            ? settings.GetFullSystemPrompt(CurrentPrompt)
            : CurrentPrompt.systemPrompt;

        var aiSettings = RCCP_AISettings.Instance;
        string json = JsonUtility.ToJson(new APIRequest {
            model = aiSettings.textModel,
            max_tokens = aiSettings.maxTokens,
            system = new SystemBlock[] {
                new SystemBlock {
                    type = "text",
                    text = systemPrompt,
                    cache_control = new CacheControl { type = "ephemeral" }
                }
            },
            messages = new Message[] { new Message { role = "user", content = fullUserPrompt } }
        });

        using (var www = new UnityWebRequest(aiSettings.apiEndpoint, "POST")) {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("x-api-key", apiKey);
            www.SetRequestHeader("anthropic-version", "2023-06-01");
            www.SetRequestHeader("anthropic-beta", "prompt-caching-2024-07-31");
            www.timeout = 60;  // 60 second timeout for API calls

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                apiValidationState = ApiValidationState.Valid;
                lastApiRequestTime = EditorApplication.timeSinceStartup;

                var response = JsonUtility.FromJson<APIResponse>(www.downloadHandler.text);
                if (response.content != null && response.content.Length > 0) {
                    batchResponses[vehicle] = response.content[0].text;

                    // Save to prompt history
                    SaveToPromptHistory($"[Batch] {batchUserPrompt} - {vehicle?.name ?? "Unknown"}", response.content[0].text, false);
                }
            } else {
                Debug.LogError($"Batch API Error for {vehicle?.name ?? "Unknown"}: {www.error}");
            }

            Repaint();
        }
    }

    // ==========================================
    // BATCH CUSTOMIZATION PROCESSING
    // Processes each vehicle individually so each gets its own AI response
    // based on its own current state (engine, gears, mass, etc.)
    // ==========================================

    /// <summary>
    /// Starts batch processing for multiple vehicle customization.
    /// Each vehicle is processed sequentially with individual AI requests.
    /// This ensures "increase power by 20%" correctly applies to each vehicle's own values.
    /// </summary>
    private void StartBatchCustomizationProcessing() {
        if (string.IsNullOrEmpty(userPrompt)) {
            SetStatus("Error: Please enter a customization request", MessageType.Error);
            return;
        }

        var aiSettings = RCCP_AISettings.Instance;
        bool useProxy = aiSettings != null && aiSettings.useServerProxy;

        // Server proxy mode - ensure device is registered
        if (useProxy) {
            if (!RCCP_ServerProxy.IsRegistered) {
                SetStatus("Registering device with server...", MessageType.Info);
                isProcessing = true;
                RCCP_ServerProxy.RegisterDevice(this, (success, message) => {
                    isProcessing = false;
                    if (success) {
                        StartBatchCustomizationProcessing();
                    } else {
                        SetStatus($"Registration failed: {message}", MessageType.Error);
                    }
                    Repaint();
                });
                return;
            }
        }

        // Store the prompt for batch use
        batchCustomizationUserPrompt = userPrompt;
        batchCustomizationResponses.Clear();
        currentBatchCustomizationIndex = 0;
        isBatchCustomizationProcessing = true;
        isBatchCustomization = true;  // For UI display
        isProcessing = true;

        // Add to recent prompts
        AddToRecentPrompts(userPrompt);

        SetStatus($"Processing batch customization: 1/{batchCustomizationVehicles.Count}", MessageType.Info);
        requestStartTime = (float)EditorApplication.timeSinceStartup;

        currentRequestCoroutine = EditorCoroutineUtility.StartCoroutine(ProcessBatchCustomizationRequests(), this);
    }

    /// <summary>
    /// Coroutine that processes each vehicle customization sequentially.
    /// Each vehicle gets its own AI request with its own current state context.
    /// </summary>
    private IEnumerator ProcessBatchCustomizationRequests() {
        try {
            while (currentBatchCustomizationIndex < batchCustomizationVehicles.Count && isBatchCustomizationProcessing) {
                RCCP_CarController controller = batchCustomizationVehicles[currentBatchCustomizationIndex];
                if (controller == null) {
                    currentBatchCustomizationIndex++;
                    continue;
                }

                SetStatus($"Processing {currentBatchCustomizationIndex + 1}/{batchCustomizationVehicles.Count}: {controller.gameObject.name}", MessageType.Info);
                Repaint();

                // Build prompt with THIS vehicle's current state
                string fullUserPrompt = BuildBatchCustomizationUserPrompt(batchCustomizationUserPrompt, controller);

                // Send request for this vehicle
                yield return SendBatchCustomizationRequest(controller, fullUserPrompt);

                // Check if window was closed during yield
                if (this == null) yield break;

                currentBatchCustomizationIndex++;
                requestStartTime = (float)EditorApplication.timeSinceStartup;

                // Add delay between requests when using server proxy to respect rate limits
                var aiSettings = RCCP_AISettings.Instance;
                bool useProxy = aiSettings != null && aiSettings.useServerProxy;
                if (useProxy && currentBatchCustomizationIndex < batchCustomizationVehicles.Count && batchCustomizationResponses.ContainsKey(controller)) {
                    SetStatus($"Waiting for rate limit... ({currentBatchCustomizationIndex}/{batchCustomizationVehicles.Count} complete)", MessageType.Info);
                    Repaint();
                    yield return new EditorWaitForSeconds(BATCH_REQUEST_DELAY_SECONDS);

                    // Check if window was closed during delay
                    if (this == null) yield break;
                }
            }

            // Check if window still exists before updating UI
            if (this == null) yield break;

            // Batch complete
            if (batchCustomizationResponses.Count > 0) {
                bool allSucceeded = batchCustomizationResponses.Count == batchCustomizationVehicles.Count;

                if (allSucceeded && autoApply) {
                    SetStatus($"Batch complete! Auto-applying to {batchCustomizationVehicles.Count} vehicles...", MessageType.Info);
                    Repaint();
                    EditorApplication.delayCall += ApplyBatchVehicleCustomization;
                } else if (allSucceeded) {
                    SetStatus($"Batch complete! {batchCustomizationVehicles.Count} vehicles configured. Click 'Apply All' to customize.", MessageType.Info);
                } else {
                    SetStatus($"Batch completed: {batchCustomizationResponses.Count}/{batchCustomizationVehicles.Count} ready to apply.", MessageType.Warning);
                }
            } else {
                SetStatus("Batch failed: No vehicles were configured successfully.", MessageType.Error);
            }

            if (RCCP_AIRateLimiter.CheckAndShowPendingDialogs()) {
                showSettings = true;
                Repaint();
            }
        }
        finally {
            isBatchCustomizationProcessing = false;
            isProcessing = false;
            currentRequestCoroutine = null;
            if (this != null) Repaint();
        }
    }

    /// <summary>
    /// Builds the user prompt with a specific vehicle's current state.
    /// This ensures each vehicle's prompt includes its own engine, gears, mass, etc.
    /// </summary>
    private string BuildBatchCustomizationUserPrompt(string prompt, RCCP_CarController controller) {
        StringBuilder sb = new StringBuilder();

        // Add mode instruction
        if (promptMode == RCCP_AIConfig.PromptMode.Ask) {
            sb.AppendLine("[USER INTENT: QUESTION]");
            sb.AppendLine("The user is asking a question or seeking explanation about RCCP settings.");
            sb.AppendLine();
        } else {
            sb.AppendLine("[USER INTENT: CONFIGURATION REQUEST]");
            sb.AppendLine("The user wants to configure or modify vehicle settings. Respond with ONLY valid JSON.");
            sb.AppendLine();
        }

        // Include vehicle name so the AI knows which specific vehicle it is customizing
        if (controller != null && controller.gameObject != null) {
            sb.AppendLine($"Vehicle Name: \"{controller.gameObject.name}\"");
            sb.AppendLine("Use the vehicle name as a hint for the vehicle type and customize accordingly.");
            sb.AppendLine();
        }

        sb.AppendLine(prompt);

        // Include THIS vehicle's current state
        if (CurrentPrompt.includeCurrentState && controller != null) {
            sb.AppendLine();
            sb.AppendLine("Current State:");
            AppendVehicleState(sb, controller);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Appends a specific vehicle's current state to the prompt.
    /// Extracted from AppendCurrentState to work with any controller.
    /// </summary>
    private void AppendVehicleState(StringBuilder sb, RCCP_CarController controller) {
        var rb = controller.GetComponent<Rigidbody>();
        var engine = controller.GetComponentInChildren<RCCP_Engine>(true);
        var clutch = controller.GetComponentInChildren<RCCP_Clutch>(true);
        var gearbox = controller.GetComponentInChildren<RCCP_Gearbox>(true);
        var stability = controller.GetComponentInChildren<RCCP_Stability>(true);
        var diffs = controller.GetComponentsInChildren<RCCP_Differential>(true);
        var axles = controller.GetComponentsInChildren<RCCP_Axle>(true);
        var wheelColliders = controller.GetComponentsInChildren<RCCP_WheelCollider>(true);
        var aero = controller.GetComponentInChildren<RCCP_AeroDynamics>(true);
        var otherAddons = controller.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Vehicle basics
        if (rb != null) {
            sb.AppendLine($"Vehicle: mass={rb.mass}kg");
        }

        // Behavior settings
#if RCCP_V2_2_OR_NEWER
        if (controller.useCustomBehavior) {
            var rccpSettings = RCCP_Settings.Instance;
            if (rccpSettings != null && rccpSettings.behaviorTypes != null &&
                controller.customBehaviorIndex >= 0 &&
                controller.customBehaviorIndex < rccpSettings.behaviorTypes.Length) {
                var behavior = rccpSettings.behaviorTypes[controller.customBehaviorIndex];
                sb.AppendLine($"Behavior: useCustomBehavior=true, preset=\"{behavior.behaviorName}\" (index={controller.customBehaviorIndex})");
            } else {
                sb.AppendLine($"Behavior: useCustomBehavior=true, customBehaviorIndex={controller.customBehaviorIndex} (invalid index)");
            }
        } else {
            var rccpSettings = RCCP_Settings.Instance;
            string globalBehavior = "unknown";
            if (rccpSettings != null && rccpSettings.behaviorTypes != null &&
                rccpSettings.behaviorSelectedIndex >= 0 &&
                rccpSettings.behaviorSelectedIndex < rccpSettings.behaviorTypes.Length) {
                globalBehavior = rccpSettings.behaviorTypes[rccpSettings.behaviorSelectedIndex].behaviorName;
            }
            sb.AppendLine($"Behavior: useCustomBehavior=false, inheritsGlobal=\"{globalBehavior}\"");
        }
#else
        // V2.0: Only show global behavior (custom behavior not available)
        {
            var rccpSettings = RCCP_Settings.Instance;
            string globalBehavior = "unknown";
            if (rccpSettings != null && rccpSettings.behaviorTypes != null &&
                rccpSettings.behaviorSelectedIndex >= 0 &&
                rccpSettings.behaviorSelectedIndex < rccpSettings.behaviorTypes.Length) {
                globalBehavior = rccpSettings.behaviorTypes[rccpSettings.behaviorSelectedIndex].behaviorName;
            }
            sb.AppendLine($"Behavior: inheritsGlobal=\"{globalBehavior}\"");
        }
#endif

        // Engine
        if (engine != null) {
            sb.AppendLine($"Engine: torque={engine.maximumTorqueAsNM}Nm, minRPM={engine.minEngineRPM}, maxRPM={engine.maxEngineRPM}, maxSpeed={engine.maximumSpeed}km/h, turbo={engine.turboCharged}");
            if (engine.turboCharged) {
                sb.AppendLine($"  Turbo: psi={engine.maxTurboChargePsi}, coefficient={engine.turboChargerCoEfficient}");
            }
        }

        // Clutch
        if (clutch != null) {
            sb.AppendLine($"Clutch: engageRPM={clutch.engageRPM}, inertia={clutch.clutchInertia}, automatic={clutch.automaticClutch}");
        }

        // Gearbox
        if (gearbox != null) {
            string ratios = gearbox.gearRatios != null ? string.Join(",", gearbox.gearRatios) : "none";
            sb.AppendLine($"Gearbox: type={gearbox.transmissionType}, gears={gearbox.gearRatios?.Length}, shiftTime={gearbox.shiftingTime}s, threshold={gearbox.shiftThreshold}");
            sb.AppendLine($"  Ratios: [{ratios}]");
        }

        // Differential - determine drive type
        bool frontPower = false, rearPower = false;
        if (diffs != null && diffs.Length > 0) {
            foreach (var diff in diffs) {
                if (diff == null || diff.connectedAxle == null) continue;
                bool isFrontAxle = diff.connectedAxle.GetComponentsInChildren<RCCP_WheelCollider>(true)
                    .Any(w => w != null && w.transform.localPosition.z > 0);
                if (isFrontAxle) frontPower = true;
                else rearPower = true;
            }
        }
        string driveType = (frontPower && rearPower) ? "AWD" : (frontPower ? "FWD" : (rearPower ? "RWD" : "Unknown"));
        sb.AppendLine($"DriveType: {driveType}");

        foreach (var diff in diffs ?? new RCCP_Differential[0]) {
            if (diff == null) continue;
            sb.AppendLine($"Differential: finalRatio={diff.finalDriveRatio}");
        }

        // Axles
        if (axles != null) {
            foreach (var axle in axles) {
                if (axle == null) continue;
                string axleName = axle.transform.localPosition.z > 0 ? "Front" : "Rear";
                sb.AppendLine($"Axle ({axleName}): steer={axle.isSteer}, handbrake={axle.isHandbrake}, maxSteer={axle.maxSteerAngle}");
            }
        }

        // Wheels (get WheelCollider component for radius/width)
        if (wheelColliders != null && wheelColliders.Length > 0 && wheelColliders[0] != null) {
            var wc = wheelColliders[0].GetComponent<WheelCollider>();
            if (wc != null) {
                sb.AppendLine($"Wheels: radius={wc.radius}m, count={wheelColliders.Length}");
            }
        }

        // Stability
        if (stability != null) {
            sb.AppendLine($"Stability: ABS={stability.ABS}, ESP={stability.ESP}, TCS={stability.TCS}");
        }

        // Aerodynamics
        if (aero != null) {
            sb.AppendLine($"Aero: downforce={aero.downForce}");
        }

        // Other addons - check child components
        if (otherAddons != null) {
            var limiter = otherAddons.GetComponentInChildren<RCCP_Limiter>(true);
            var nos = otherAddons.GetComponentInChildren<RCCP_Nos>(true);
            sb.AppendLine($"Addons: limiter={limiter != null}, nos={nos != null}");
        }
    }

    private IEnumerator SendBatchCustomizationRequest(RCCP_CarController controller, string fullUserPrompt) {
        var aiSettings = RCCP_AISettings.Instance;

        if (aiSettings != null && aiSettings.useServerProxy) {
            yield return SendBatchCustomizationRequestViaProxy(controller, fullUserPrompt);
            yield break;
        }

        yield return SendBatchCustomizationRequestDirect(controller, fullUserPrompt);
    }

    private IEnumerator SendBatchCustomizationRequestViaProxy(RCCP_CarController controller, string fullUserPrompt) {
        string systemPrompt = settings != null
            ? settings.GetFullSystemPrompt(CurrentPrompt)
            : CurrentPrompt.systemPrompt;

        var aiSettings = RCCP_AISettings.Instance;

        bool responseReceived = false;
        RCCP_ServerProxy.QueryResult queryResult = null;

        RCCP_ServerProxy.SendQuery(
            this,
            aiSettings.textModel,
            aiSettings.maxTokens,
            systemPrompt,
            fullUserPrompt,
            "BatchCustomization",
            (result) => {
                queryResult = result;
                responseReceived = true;
            }
        );

        while (!responseReceived) {
            yield return null;
        }

        if (queryResult != null && queryResult.Success) {
            batchCustomizationResponses[controller] = queryResult.Content;
            SaveToPromptHistory($"[Batch Customize] {batchCustomizationUserPrompt} - {controller.gameObject.name}", queryResult.Content, false);
        } else {
            if (queryResult?.Error != null &&
                (queryResult.Error.Contains("Invalid device token") || queryResult.Error.Contains("invalid token"))) {
                RCCP_ServerProxy.ClearRegistration();
                Debug.LogWarning($"[RCCP AI] Device session expired during batch customization for {controller.gameObject.name}. Please try again.");
            } else {
                Debug.LogError($"[RCCP AI] Batch customization error for {controller.gameObject.name}: {queryResult?.Error ?? "Unknown error"}");
            }
        }

        Repaint();
    }

    private IEnumerator SendBatchCustomizationRequestDirect(RCCP_CarController controller, string fullUserPrompt) {
        string systemPrompt = settings != null
            ? settings.GetFullSystemPrompt(CurrentPrompt)
            : CurrentPrompt.systemPrompt;

        var aiSettings = RCCP_AISettings.Instance;
        string json = JsonUtility.ToJson(new APIRequest {
            model = aiSettings.textModel,
            max_tokens = aiSettings.maxTokens,
            system = new SystemBlock[] {
                new SystemBlock {
                    type = "text",
                    text = systemPrompt,
                    cache_control = new CacheControl { type = "ephemeral" }
                }
            },
            messages = new Message[] { new Message { role = "user", content = fullUserPrompt } }
        });

        using (var www = new UnityWebRequest(aiSettings.apiEndpoint, "POST")) {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("x-api-key", apiKey);
            www.SetRequestHeader("anthropic-version", "2023-06-01");
            www.SetRequestHeader("anthropic-beta", "prompt-caching-2024-07-31");
            www.timeout = 60;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                apiValidationState = ApiValidationState.Valid;
                lastApiRequestTime = EditorApplication.timeSinceStartup;

                var response = JsonUtility.FromJson<APIResponse>(www.downloadHandler.text);
                if (response.content != null && response.content.Length > 0) {
                    batchCustomizationResponses[controller] = response.content[0].text;
                    SaveToPromptHistory($"[Batch Customize] {batchCustomizationUserPrompt} - {controller.gameObject.name}", response.content[0].text, false);
                }
            } else {
                Debug.LogError($"[RCCP AI] Batch customization API error for {controller.gameObject.name}: {www.error}");
            }

            Repaint();
        }
    }

    // ==========================================
    // END BATCH CUSTOMIZATION PROCESSING
    // ==========================================

    /// <summary>
    /// Checks for request timeout and handles it.
    /// Called from OnInspectorUpdate.
    /// </summary>
    private void CheckRequestTimeout() {
        if (!isProcessing) return;

        float elapsed = (float)EditorApplication.timeSinceStartup - requestStartTime;
        if (elapsed > RequestTimeoutSeconds) {
            isProcessing = false;
            if (currentRequestCoroutine != null) {
                EditorCoroutineUtility.StopCoroutine(currentRequestCoroutine);
                currentRequestCoroutine = null;
            }
            SetStatus($"Request timed out after {RequestTimeoutSeconds} seconds. Please try again.", MessageType.Error);
            Repaint();
        }
    }

    private IEnumerator SendRequest() {
        var aiSettings = RCCP_AISettings.Instance;

        // Check if should use server proxy
        if (aiSettings != null && aiSettings.useServerProxy) {
            yield return SendRequestViaProxy();
            yield break;
        }

        // Direct API mode (user's own key)
        yield return SendRequestDirect();
    }

    /// <summary>
    /// Sends request via the server proxy (bundled API key protected on server).
    /// </summary>
    private IEnumerator SendRequestViaProxy() {
        try {
            string systemPrompt = settings != null
                ? settings.GetFullSystemPrompt(CurrentPrompt)
                : CurrentPrompt.systemPrompt;

            string fullUserPrompt = BuildFullUserPrompt();
            var aiSettings = RCCP_AISettings.Instance;

            // Configure server URL
            RCCP_ServerProxy.ServerUrl = aiSettings.serverUrl;

            bool responseReceived = false;
            RCCP_ServerProxy.QueryResult queryResult = null;

            // Send via proxy
            RCCP_ServerProxy.SendQuery(
                this,
                aiSettings.textModel,
                aiSettings.maxTokens,
                systemPrompt,
                fullUserPrompt,
                CurrentPrompt.panelType.ToString(),
                (result) => {
                    queryResult = result;
                    responseReceived = true;
                }
            );

            // Wait for response
            float startTime = (float)EditorApplication.timeSinceStartup;
            while (!responseReceived) {
                if (EditorApplication.timeSinceStartup - startTime > aiSettings.serverTimeout) {
                    if (isExecutingRefinement) {
                        RestoreFromRefinement();
                        SetStatus($"Vehicle created (customization timed out)", MessageType.Warning);
                    } else {
                        SetStatus($"Request timed out after {aiSettings.serverTimeout} seconds", MessageType.Error);
                    }
                    yield break;
                }
                yield return null;
            }

            // Check if window was closed
            if (this == null) yield break;

            if (queryResult.Success) {
                aiResponse = queryResult.Content;
                showPreview = false;

                // Sync usage from server response
                if (queryResult.Usage != null) {
                    RCCP_AIRateLimiter.SyncFromServer(queryResult.Usage);
                }

                // Check if AI rejected the request
                string extractedJson = ExtractJson(aiResponse);
                if (IsRejectionResponse(extractedJson, out string rejectionReason, out string[] rejectionSuggestions)) {
                    if (isExecutingRefinement) {
                        RestoreFromRefinement();
                        SetStatus("Vehicle created (customization rejected)", MessageType.Warning);
                    } else {
                        SetStatus(rejectionReason, MessageType.Warning);
                    }
                    lastRejectionSuggestions = rejectionSuggestions;
                    aiResponse = "";
                    yield break;
                }
                lastRejectionSuggestions = null;

                // Save to prompt history
                SaveToPromptHistory(userPrompt, aiResponse, autoApply);

                // Scroll to bottom
                EditorApplication.delayCall += () => {
                    scrollTargetY = float.MaxValue;
                    Repaint();
                };

                // Note: Batch customization now uses StartBatchCustomizationProcessing()
                // which processes each vehicle sequentially, so it never reaches here

                // Auto-apply handling (also force auto-apply during refinement)
                if ((autoApply || isExecutingRefinement) && promptMode == RCCP_AIConfig.PromptMode.Request) {
                    bool applySuccess = ApplyConfiguration();

                    // Handle refinement completion
                    if (isExecutingRefinement) {
                        RestoreFromRefinement();
                        if (applySuccess) {
                            SetStatus("Vehicle created and customized!", MessageType.Info);
                        }
                        // If apply failed, keep the error status from ApplyConfiguration
                    } else if (applySuccess && enableAnimations) {
                        successFlashAlpha = 1f;
                    }
                } else if (promptMode == RCCP_AIConfig.PromptMode.Request) {
                    if (ShouldUseReviewMode()) {
                        float cost = queryResult.InputTokens * 0.000003f;
                        TransitionToReviewMode(aiResponse, GetCurrentModelDisplayName(), cost, queryResult.InputTokens);
                        SetStatus("Configuration ready for review!", MessageType.Info);
                    } else {
                        SetStatus("Configuration generated! Review and apply.", MessageType.Info);
                    }
                } else {
                    SetStatus("Response received!", MessageType.Info);
                }

                // Check for pending rate limit dialogs
                if (RCCP_AIRateLimiter.CheckAndShowPendingDialogs()) {
                    showSettings = true;
                    Repaint();
                }
            } else {
                // Handle invalid device token - clear and allow re-registration
                if (queryResult.Error != null &&
                    (queryResult.Error.Contains("Invalid device token") || queryResult.Error.Contains("invalid token"))) {
                    RCCP_ServerProxy.ClearRegistration();
                    SetStatus("Session expired. Please try again.", MessageType.Warning);
                } else if (isExecutingRefinement) {
                    RestoreFromRefinement();
                    SetStatus($"Vehicle created (customization failed: {queryResult.Error})", MessageType.Warning);
                } else {
                    SetStatus($"Error: {queryResult.Error}", MessageType.Error);
                }
                RCCP_AIUtility.LogApiError(aiSettings.serverUrl, queryResult.Error);
            }
        }
        finally {
            isProcessing = false;
            currentRequestCoroutine = null;
            if (this != null) Repaint();
        }
    }

    /// <summary>
    /// Sends request directly to Claude API (user's own key).
    /// </summary>
    private IEnumerator SendRequestDirect() {
        try {
            string systemPrompt = settings != null
                ? settings.GetFullSystemPrompt(CurrentPrompt)
                : CurrentPrompt.systemPrompt;

            string fullUserPrompt = BuildFullUserPrompt();

            var aiSettings = RCCP_AISettings.Instance;
            string json = JsonUtility.ToJson(new APIRequest {
                model = aiSettings.textModel,
                max_tokens = aiSettings.maxTokens,
                system = new SystemBlock[] {
                    new SystemBlock {
                        type = "text",
                        text = systemPrompt,
                        cache_control = new CacheControl { type = "ephemeral" }
                    }
                },
                messages = new Message[] { new Message { role = "user", content = fullUserPrompt } }
            });

            bool requestSucceeded = false;
            string lastError = "";

            while (!requestSucceeded && currentRetryCount <= MAX_RETRY_COUNT) {
                using (var www = new UnityWebRequest(aiSettings.apiEndpoint, "POST")) {
                    www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");
                    www.SetRequestHeader("x-api-key", apiKey);
                    www.SetRequestHeader("anthropic-version", "2023-06-01");
                    www.SetRequestHeader("anthropic-beta", "prompt-caching-2024-07-31");
                    www.timeout = 60;  // 60 second timeout for API calls

                    yield return www.SendWebRequest();

                    // Check if window was closed during yield
                    if (this == null) yield break;

                    if (www.result == UnityWebRequest.Result.Success) {
                        requestSucceeded = true;

                        // Update API validation state on successful request
                        apiValidationState = ApiValidationState.Valid;
                        lastApiRequestTime = EditorApplication.timeSinceStartup;

                        var response = JsonUtility.FromJson<APIResponse>(www.downloadHandler.text);
                        if (response.content != null && response.content.Length > 0) {
                            aiResponse = response.content[0].text;
                            showPreview = false;  // Keep foldout collapsed by default

                            // Check if AI rejected the request (invalid/nonsense input)
                            string extractedJson = ExtractJson(aiResponse);
                            if (IsRejectionResponse(extractedJson, out string rejectionReason, out string[] rejectionSuggestions)) {
                                if (isExecutingRefinement) {
                                    RestoreFromRefinement();
                                    SetStatus("Vehicle created (customization rejected)", MessageType.Warning);
                                } else {
                                    SetStatus(rejectionReason, MessageType.Warning);
                                }
                                lastRejectionSuggestions = rejectionSuggestions;
                                aiResponse = "";  // Clear response to not show invalid JSON
                                yield break;
                            }
                            lastRejectionSuggestions = null;  // Clear any previous rejection suggestions

                            // Save to prompt history
                            SaveToPromptHistory(userPrompt, aiResponse, autoApply);

                            // Scroll to bottom smoothly when response is received (deferred to next frame for proper layout)
                            EditorApplication.delayCall += () => {
                                scrollTargetY = float.MaxValue;  // Will be clamped by scroll view
                                Repaint();
                            };

                            // Note: Batch customization now uses StartBatchCustomizationProcessing()
                            // which processes each vehicle sequentially, so it never reaches here

                            // Auto-apply only in Request mode (Ask mode doesn't have JSON to apply)
                            // Also force auto-apply during refinement
                            if ((autoApply || isExecutingRefinement) && promptMode == RCCP_AIConfig.PromptMode.Request) {
                                bool applySuccess = ApplyConfiguration();

                                // Handle refinement completion
                                if (isExecutingRefinement) {
                                    RestoreFromRefinement();
                                    if (applySuccess) {
                                        SetStatus("Vehicle created and customized!", MessageType.Info);
                                    }
                                    // If apply failed, keep the error status from ApplyConfiguration
                                } else if (applySuccess && enableAnimations) {
                                    successFlashAlpha = 1f;
                                }
                            } else if (promptMode == RCCP_AIConfig.PromptMode.Request) {
                                // Transition to review mode for structured preview
                                if (ShouldUseReviewMode()) {
                                    float cost = response.usage != null ? response.usage.input_tokens * 0.000003f : 0f;
                                    int tokens = response.usage != null ? response.usage.input_tokens : 0;
                                    TransitionToReviewMode(aiResponse, GetCurrentModelDisplayName(), cost, tokens);
                                    SetStatus("Configuration ready for review!", MessageType.Info);
                                } else {
                                    SetStatus("Configuration generated! Review and apply.", MessageType.Info);
                                }
                            } else {
                                // Ask mode - just display the response
                                SetStatus("Response received!", MessageType.Info);
                            }

                            // Check for pending rate limit dialogs (e.g., setup pool exhausted)
                            if (RCCP_AIRateLimiter.CheckAndShowPendingDialogs()) {
                                showSettings = true;
                                Repaint();
                            }
                        } else {
                            if (isExecutingRefinement) {
                                RestoreFromRefinement();
                                SetStatus("Vehicle created (customization failed: empty response)", MessageType.Warning);
                            } else {
                                SetStatus("Error: Empty response from API", MessageType.Error);
                            }
                            RCCP_AIUtility.LogApiError(aiSettings.apiEndpoint, "Empty response from API");
                        }
                    } else {
                        lastError = www.error;
                        long httpCode = www.responseCode;

                        // Log the error
                        RCCP_AIUtility.LogApiError(aiSettings.apiEndpoint, lastError, (int)httpCode);

                        // Check if error is retryable (network errors, 5xx server errors, 429 rate limit)
                        bool isRetryable = www.result == UnityWebRequest.Result.ConnectionError ||
                                           (httpCode >= 500 && httpCode < 600) ||
                                           httpCode == 429;

                        if (isRetryable && currentRetryCount < MAX_RETRY_COUNT) {
                            currentRetryCount++;
                            SetStatus($"API Error, retrying ({currentRetryCount}/{MAX_RETRY_COUNT})...", MessageType.Warning);
                            Repaint();

                            // Wait before retrying
                            float retryDelay = RETRY_DELAY_SECONDS * currentRetryCount;  // Exponential backoff
                            yield return new EditorWaitForSeconds(retryDelay);

                            // Check if window was closed during retry wait
                            if (this == null) yield break;
                        } else {
                            // Non-retryable error or max retries reached
                            if (isExecutingRefinement) {
                                RestoreFromRefinement();
                                SetStatus($"Vehicle created (customization failed: {lastError})", MessageType.Warning);
                            } else if (currentRetryCount >= MAX_RETRY_COUNT) {
                                SetStatus($"API Error after {MAX_RETRY_COUNT} retries: {lastError}", MessageType.Error);
                            } else {
                                SetStatus($"API Error: {lastError}", MessageType.Error);
                            }
                            break;
                        }
                    }
                }
            }
        }
        finally {
            // ALWAYS reset state, even on exception
            isProcessing = false;
            currentRequestCoroutine = null;
            if (this != null) Repaint();
        }
    }

    private string BuildFullUserPrompt() {
        StringBuilder sb = new StringBuilder();

        // Prepend mode instruction to clarify user intent
        if (promptMode == RCCP_AIConfig.PromptMode.Ask) {
            sb.AppendLine("[USER INTENT: QUESTION]");
            sb.AppendLine("The user is asking a question or seeking explanation about RCCP settings, components, or vehicle physics.");
            sb.AppendLine("Respond with helpful explanatory text. You may include JSON examples if relevant, but focus on explaining concepts clearly.");
            sb.AppendLine("Do NOT return only JSON - provide a conversational, educational response.");
            sb.AppendLine();
        } else {
            sb.AppendLine("[USER INTENT: CONFIGURATION REQUEST]");
            sb.AppendLine("The user wants to configure or modify vehicle settings. Respond with ONLY valid JSON that can be applied.");
            sb.AppendLine();
        }

        sb.AppendLine(userPrompt);

        if (CurrentPrompt.includeMeshAnalysis && !string.IsNullOrEmpty(meshAnalysis)) {
            sb.AppendLine();
            sb.AppendLine("Mesh Analysis:");
            sb.AppendLine(meshAnalysis);
        }

        if (CurrentPrompt.includeCurrentState && HasRCCPController) {
            sb.AppendLine();
            sb.AppendLine("Current State:");
            AppendCurrentState(sb);
        }

        return sb.ToString();
    }

    private void AppendCurrentState(StringBuilder sb) {
        var rb = selectedController.GetComponent<Rigidbody>();
        var engine = selectedController.GetComponentInChildren<RCCP_Engine>(true);
        var clutch = selectedController.GetComponentInChildren<RCCP_Clutch>(true);
        var gearbox = selectedController.GetComponentInChildren<RCCP_Gearbox>(true);
        var stability = selectedController.GetComponentInChildren<RCCP_Stability>(true);
        var diffs = selectedController.GetComponentsInChildren<RCCP_Differential>(true);
        var axles = selectedController.GetComponentsInChildren<RCCP_Axle>(true);
        var wheelColliders = selectedController.GetComponentsInChildren<RCCP_WheelCollider>(true);
        var aero = selectedController.GetComponentInChildren<RCCP_AeroDynamics>(true);
        var otherAddons = selectedController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Vehicle basics
        if (rb != null) {
            sb.AppendLine($"Vehicle: mass={rb.mass}kg");
        }

        // Behavior settings - important for AI to know if vehicle has custom behavior
#if RCCP_V2_2_OR_NEWER
        if (selectedController.useCustomBehavior) {
            var rccpSettings = RCCP_Settings.Instance;
            if (rccpSettings != null && rccpSettings.behaviorTypes != null &&
                selectedController.customBehaviorIndex >= 0 &&
                selectedController.customBehaviorIndex < rccpSettings.behaviorTypes.Length) {
                var behavior = rccpSettings.behaviorTypes[selectedController.customBehaviorIndex];
                sb.AppendLine($"Behavior: useCustomBehavior=true, preset=\"{behavior.behaviorName}\" (index={selectedController.customBehaviorIndex})");
            } else {
                sb.AppendLine($"Behavior: useCustomBehavior=true, customBehaviorIndex={selectedController.customBehaviorIndex} (invalid index)");
            }
        } else {
            // Also show what the global behavior is for context
            var rccpSettings = RCCP_Settings.Instance;
            string globalBehavior = "unknown";
            if (rccpSettings != null && rccpSettings.behaviorTypes != null &&
                rccpSettings.behaviorSelectedIndex >= 0 &&
                rccpSettings.behaviorSelectedIndex < rccpSettings.behaviorTypes.Length) {
                globalBehavior = rccpSettings.behaviorTypes[rccpSettings.behaviorSelectedIndex].behaviorName;
            }
            sb.AppendLine($"Behavior: useCustomBehavior=false, inheritsGlobal=\"{globalBehavior}\"");
        }
#else
        // V2.0: Only show global behavior (custom behavior not available)
        {
            var rccpSettings = RCCP_Settings.Instance;
            string globalBehavior = "unknown";
            if (rccpSettings != null && rccpSettings.behaviorTypes != null &&
                rccpSettings.behaviorSelectedIndex >= 0 &&
                rccpSettings.behaviorSelectedIndex < rccpSettings.behaviorTypes.Length) {
                globalBehavior = rccpSettings.behaviorTypes[rccpSettings.behaviorSelectedIndex].behaviorName;
            }
            sb.AppendLine($"Behavior: inheritsGlobal=\"{globalBehavior}\"");
        }
#endif

        // Engine
        if (engine != null) {
            sb.AppendLine($"Engine: torque={engine.maximumTorqueAsNM}Nm, minRPM={engine.minEngineRPM}, maxRPM={engine.maxEngineRPM}, maxSpeed={engine.maximumSpeed}km/h, turbo={engine.turboCharged}");
            if (engine.turboCharged) {
                sb.AppendLine($"  Turbo: psi={engine.maxTurboChargePsi}, coefficient={engine.turboChargerCoEfficient}");
            }
        }

        // Clutch
        if (clutch != null) {
            sb.AppendLine($"Clutch: engageRPM={clutch.engageRPM}, inertia={clutch.clutchInertia}, automatic={clutch.automaticClutch}");
        }

        // Gearbox
        if (gearbox != null) {
            string ratios = gearbox.gearRatios != null ? string.Join(",", gearbox.gearRatios) : "none";
            sb.AppendLine($"Gearbox: type={gearbox.transmissionType}, gears={gearbox.gearRatios?.Length}, shiftTime={gearbox.shiftingTime}s, threshold={gearbox.shiftThreshold}");
            sb.AppendLine($"  Ratios: [{ratios}]");
        }

        // Differential - also determine drive type based on differential connections
        // NOTE: axle.isPower is a runtime property set by differential, so in editor we must
        // check which axle each differential is connected to instead
        bool frontPower = false, rearPower = false;
        if (diffs != null && diffs.Length > 0) {
            foreach (var diff in diffs) {
                string axleName = diff.connectedAxle != null ? diff.connectedAxle.gameObject.name : "unknown";
                sb.AppendLine($"Differential ({axleName}): type={diff.differentialType}, slipRatio={diff.limitedSlipRatio}, finalDrive={diff.finalDriveRatio}");

                // Determine which axle has power based on differential connection
                if (diff.connectedAxle != null) {
                    bool isFrontAxle = diff.connectedAxle.gameObject.name.ToLower().Contains("front");
                    bool isRearAxle = diff.connectedAxle.gameObject.name.ToLower().Contains("rear");

                    if (isFrontAxle) frontPower = true;
                    else if (isRearAxle) rearPower = true;
                    else {
                        // Fallback: if axle name doesn't indicate front/rear, check position
                        // Front axle should have higher Z (more forward) in vehicle local space
                        if (axles != null && axles.Length >= 2) {
                            float connectedZ = selectedController.transform.InverseTransformPoint(
                                diff.connectedAxle.transform.position).z;
                            float maxZ = float.MinValue;
                            foreach (var ax in axles) {
                                float z = selectedController.transform.InverseTransformPoint(ax.transform.position).z;
                                if (z > maxZ) maxZ = z;
                            }
                            // If connected axle is at max Z, it's likely front
                            if (Mathf.Approximately(connectedZ, maxZ)) frontPower = true;
                            else rearPower = true;
                        } else {
                            // Default to rear if we can't determine
                            rearPower = true;
                        }
                    }
                }
            }
        }

        // Axles info
        if (axles != null && axles.Length > 0) {
            foreach (var axle in axles) {
                // Show actual power based on differential connection, not runtime isPower
                bool hasPower = false;
                if (diffs != null) {
                    foreach (var diff in diffs) {
                        if (diff.connectedAxle == axle) {
                            hasPower = true;
                            break;
                        }
                    }
                }

                sb.AppendLine($"Axle ({axle.gameObject.name}): steer={axle.isSteer}, brake={axle.isBrake}, power={hasPower}, handbrake={axle.isHandbrake}");
                sb.AppendLine($"  maxSteerAngle={axle.maxSteerAngle}, maxBrakeTorque={axle.maxBrakeTorque}, antiroll={axle.antirollForce}");
            }
            string driveType = (frontPower && rearPower) ? "AWD" : (frontPower ? "FWD" : "RWD");
            sb.AppendLine($"DriveType: {driveType}");
        }

        // Stability
        if (stability != null) {
            sb.AppendLine($"Stability: ABS={stability.ABS}, ESP={stability.ESP}, TCS={stability.TCS}");
            sb.AppendLine($"  steerHelper={stability.steeringHelper} ({stability.steerHelperStrength}), tractionHelper={stability.tractionHelper} ({stability.tractionHelperStrength})");
        }

        // Suspension (from first wheel collider)
        if (wheelColliders != null && wheelColliders.Length > 0 && wheelColliders[0] != null) {
            var wc = wheelColliders[0].GetComponent<WheelCollider>();
            if (wc != null) {
                sb.AppendLine($"Suspension: distance={wc.suspensionDistance}m, spring={wc.suspensionSpring.spring}, damper={wc.suspensionSpring.damper}");
                sb.AppendLine($"Friction (forward): extremum={wc.forwardFriction.extremumSlip}/{wc.forwardFriction.extremumValue}, asymptote={wc.forwardFriction.asymptoteSlip}/{wc.forwardFriction.asymptoteValue}");
                sb.AppendLine($"Friction (sideways): extremum={wc.sidewaysFriction.extremumSlip}/{wc.sidewaysFriction.extremumValue}, asymptote={wc.sidewaysFriction.asymptoteSlip}/{wc.sidewaysFriction.asymptoteValue}");
            }
        }

        // Aerodynamics
        if (aero != null) {
            sb.AppendLine($"Aero: downForce={aero.downForce}");
        }

        // Other Addons - use GetComponentInChildren instead of runtime properties
        if (otherAddons != null) {
            // NOS
            var nos = otherAddons.GetComponentInChildren<RCCP_Nos>(true);
            if (nos != null) {
                sb.AppendLine($"NOS: enabled={nos.enabled}, multiplier={nos.torqueMultiplier}, duration={nos.durationTime}s");
            }

            // Fuel Tank
            var fuelTank = otherAddons.GetComponentInChildren<RCCP_FuelTank>(true);
            if (fuelTank != null) {
                sb.AppendLine($"FuelTank: enabled={fuelTank.enabled}, capacity={fuelTank.fuelTankCapacity}L, fill={fuelTank.fuelTankFillAmount * 100}%");
            }

            // Limiter
            var limiter = otherAddons.GetComponentInChildren<RCCP_Limiter>(true);
            if (limiter != null) {
                string limits = limiter.limitSpeedAtGear != null ? string.Join(",", limiter.limitSpeedAtGear) : "none";
                sb.AppendLine($"Limiter: enabled={limiter.enabled}, speeds=[{limits}]");
            }
        }
    }

    /// <summary>
    /// Checks if the AI response is a rejection (invalid/nonsense input detected).
    /// </summary>
    private bool IsRejectionResponse(string response, out string reason, out string[] suggestions) {
        reason = null;
        suggestions = null;

        if (string.IsNullOrEmpty(response))
            return false;

        try {
            // Try to parse as rejection response
            var rejection = JsonUtility.FromJson<RCCP_AIConfig.RejectionResponse>(response);
            if (rejection != null && rejection.rejected) {
                reason = rejection.reason ?? "Request not understood";
                suggestions = rejection.suggestions;
                return true;
            }
        } catch {
            // Not a rejection response, continue normally
        }
        return false;
    }

    /// <summary>
    /// Checks if user prompt contains detailed customization intent beyond basic vehicle type.
    /// Used to determine if post-creation refinement should be triggered.
    /// </summary>
    private bool HasDetailedCustomizationIntent(string prompt) {
        if (string.IsNullOrEmpty(prompt)) return false;

        // Specific numeric values with units (case-insensitive)
        // Matches: 500nm, 100 HP, 250kph, 8000rpm, etc.
        if (Regex.IsMatch(
            prompt,
            @"\d+\s*(nm|hp|kg|psi|kmh|km/h|kph|mph|rpm)",
            RegexOptions.IgnoreCase))
            return true;

        string lower = prompt.ToLower();

        // Detailed parameter keywords (beyond basic vehicle types)
        string[] detailKeywords = {
            "stiff", "soft", "hard", "locked", "limited", "open diff",
            "turbo", "nos", "nitro", "downforce", "antiroll", "anti-roll",
            "camber", "caster", "grip", "friction", "spring", "damper",
            "abs", "esp", "tcs", "stability", "traction"
        };

        foreach (var keyword in detailKeywords) {
            if (lower.Contains(keyword)) return true;
        }

        return false;
    }

    /// <summary>
    /// Restores panel context after refinement completes (success, error, or cancel).
    /// </summary>
    private void RestoreFromRefinement() {
        if (refinementRestoreInfo.HasValue) {
            var restore = refinementRestoreInfo.Value;
            currentPromptIndex = restore.panelIndex;
            userPrompt = restore.userPrompt;
            // Keep aiResponse from refinement for debugging/reference
        }
        ClearRefinementState();
    }

    /// <summary>
    /// Clears all refinement-related state.
    /// </summary>
    private void ClearRefinementState() {
        isRefinementPending = false;
        isExecutingRefinement = false;
        pendingRefinementPrompt = null;
        refinementRetryCount = 0;
        refinementRestoreInfo = null;
    }

    #endregion

    #region API Classes

    [Serializable]
    private class APIRequest {
        public string model;
        public int max_tokens;
        public SystemBlock[] system;  // Changed to array for cache support
        public Message[] messages;
    }

    [Serializable]
    private class SystemBlock {
        public string type;
        public string text;
        public CacheControl cache_control;
    }

    [Serializable]
    private class CacheControl {
        public string type;
    }

    [Serializable]
    private class Message {
        public string role;
        public string content;
    }

    [Serializable]
    private class APIResponse {
        public Content[] content;
        public Usage usage;  // Added to track cache stats
    }

    [Serializable]
    private class Content {
        public string type;
        public string text;
    }

    [Serializable]
    private class Usage {
        public int input_tokens;
        public int output_tokens;
        public int cache_creation_input_tokens;
        public int cache_read_input_tokens;
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
