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
using System.Collections.Generic;

namespace BoneCrackerGames.RCCP.AIAssistant {

public partial class RCCP_AIAssistantWindow {

    #region Vision Light Detection

    /// <summary>
    /// Draws the Vision Light Detection button and status for the Lights panel.
    /// </summary>
    private void DrawVisionLightDetectionButton() {
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        // Header with icon
        EditorGUILayout.BeginHorizontal();
        GUIStyle headerStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeMD
        };
        GUILayout.Label("✨ Auto Create Lights", headerStyle);
        GUILayout.FlexibleSpace();

        // Help button
        if (GUILayout.Button(new GUIContent("?", "Uses AI vision to analyze your vehicle model and detect where lights should be placed. Takes screenshots from front and rear, then identifies headlights, taillights, indicators, and other light positions."), GUILayout.Width(22), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonSmall))) {
            EditorUtility.DisplayDialog("Auto Create Lights",
                "This feature uses Claude's vision capabilities to analyze your vehicle:\n\n" +
                "1. Takes screenshots from front and rear angles\n" +
                "2. Sends images to Claude Vision API\n" +
                "3. AI identifies light housings and positions\n" +
                "4. Shows preview with adjustable positions\n" +
                "5. Creates lights at detected locations\n\n" +
                "This is especially useful for vehicles where lights aren't clearly named in the model hierarchy.",
                "Got it");
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(S2);

        // Description
        GUIStyle descStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.TextSecondary, 0.1f) },
            wordWrap = true
        };
        GUILayout.Label("Automatically create and position lights on your vehicle using AI vision analysis.", descStyle);

        RCCP_AIDesignSystem.Space(S2);

        // Check if vehicle has RCCP_Lights component - inform beginners if missing
        bool hasLightsManager = selectedController != null &&
            selectedController.GetComponentInChildren<RCCP_Lights>(true) != null;

        if (!hasLightsManager && HasRCCPController) {
            EditorGUILayout.HelpBox(
                "This vehicle doesn't have lights yet. They will be automatically created when you apply changes.",
                MessageType.Info
            );
            RCCP_AIDesignSystem.Space(S2);
        }

        // Cost warning for vision features
        GUIStyle costStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Warning },
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
        GUILayout.Label("⚠ Image analysis costs ~10x more than text-only requests", costStyle);

        // Show vision limit for proxy users (not using own API key)
        var settings = RCCP_AISettings.Instance;
        if (settings != null && settings.useServerProxy && !RCCP_AIEditorPrefs.UseOwnApiKey) {
            var usage = RCCP_ServerProxy.CachedUsage;
            if (usage != null && usage.visionDailyLimit > 0) {
                int remaining = Mathf.Max(0, usage.visionDailyLimit - usage.visionUsedToday);
                GUIStyle limitStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = remaining > 0 ? RCCP_AIDesignSystem.Colors.TextSecondary : RCCP_AIDesignSystem.Colors.Error },
                    fontStyle = FontStyle.Italic
                };
                GUILayout.Label($"Vision requests: {remaining}/{usage.visionDailyLimit} remaining today", limitStyle);
            }
        }

        RCCP_AIDesignSystem.Space(S4);

        // Status/Progress
        if (isVisionDetecting) {
            // Show progress
            EditorGUILayout.BeginHorizontal();
            int dots = (int)(EditorApplication.timeSinceStartup * 2) % 4;
            string dotStr = new string('.', dots);
            GUILayout.Label($"⏳ Analyzing vehicle{dotStr}", RCCP_AIDesignSystem.LabelHeader);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(60))) {
                RCCP_AIVisionLightDetector_V2.Instance.Cancel();
                isVisionDetecting = false;
            }
            EditorGUILayout.EndHorizontal();

            // Animated progress bar
            Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(RCCP_AIDesignSystem.Heights.ProgressBar));
            EditorGUI.DrawRect(progressRect, RCCP_AIDesignSystem.Colors.BgRecessed);
            float progress = (float)((EditorApplication.timeSinceStartup * 0.3) % 1.0);
            float barWidth = progressRect.width * 0.3f;
            float barX = progressRect.x + (progressRect.width - barWidth) * progress;
            EditorGUI.DrawRect(new Rect(barX, progressRect.y, barWidth, progressRect.height), AccentColor);
        } else {
            // Show button
            EditorGUILayout.BeginHorizontal();

            bool canDetect = !isProcessing && HasValidAuth && HasRCCPController;

            EditorGUI.BeginDisabledGroup(!canDetect);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = RCCP_AIDesignSystem.Colors.Success;  // Green accent for prominence

            if (GUILayout.Button(new GUIContent("✨ Auto Create Lights", "Automatically create headlights, taillights, and indicators using AI vision"), GUILayout.Height(RCCP_AIDesignSystem.Heights.ButtonLarge))) {
                StartVisionLightDetection();
            }

            GUI.backgroundColor = oldBg;
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            // Show requirements if can't detect
            if (!canDetect) {
                string reason = "";
                if (!HasValidAuth) reason = "API key or server proxy required";
                else if (!HasRCCPController) reason = "Select an RCCP vehicle";
                else if (isProcessing) reason = "Wait for current operation";

                if (!string.IsNullOrEmpty(reason)) {
                    GUIStyle reasonStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                        normal = { textColor = RCCP_AIDesignSystem.Colors.Warning },
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUILayout.Label(reason, reasonStyle);
                }
            }
        }

        // Show last result summary if available (V2 takes priority)
        if (visionDetectionResultV2 != null && visionDetectionResultV2.success) {
            RCCP_AIDesignSystem.Space(S2);
            EditorGUILayout.BeginHorizontal();
            GUIStyle resultStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                normal = { textColor = RCCP_AIDesignSystem.Colors.Success }
            };
            int count = visionDetectionResultV2.lights?.Count ?? 0;
            GUILayout.Label($"✓ Last detection: {count} lights found", resultStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("View Results", RCCP_AIDesignSystem.ButtonSecondary, GUILayout.Width(80))) {
                ShowVisionDetectionPreviewV2();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Starts the vision-based light detection process (V2).
    /// </summary>
    private void StartVisionLightDetection() {
        if (isVisionDetecting || !HasRCCPController || !HasValidAuth)
            return;

        isVisionDetecting = true;
        visionDetectionResultV2 = null;

        Debug.Log("[RCCP AI] Starting vision-based light detection (V2)...");

        RCCP_AIVisionLightDetector_V2.Instance.DetectLights(
            selectedController.gameObject,
            apiKey,
            OnVisionDetectionCompleteV2,
            this
        );
    }

    /// <summary>
    /// Called when V2 vision detection completes.
    /// </summary>
    private void OnVisionDetectionCompleteV2(RCCP_AIVisionLightDetector_V2.DetectionResult result) {
        isVisionDetecting = false;
        visionDetectionResultV2 = result;

        if (result.success) {
            Debug.Log($"[RCCP AI] Vision detection V2 complete: {result.lights.Count} lights detected");
            SetStatus($"Detected {result.lights.Count} light positions", MessageType.Info);

            // Show the preview window
            ShowVisionDetectionPreviewV2();
        } else {
            Debug.LogError($"[RCCP AI] Vision detection V2 failed: {result.error}");
            SetStatus($"Detection failed: {result.error}", MessageType.Error);
        }

        Repaint();
    }

    /// <summary>
    /// Shows the V2 vision detection preview window.
    /// </summary>
    private void ShowVisionDetectionPreviewV2() {
        if (visionDetectionResultV2 == null || !visionDetectionResultV2.success)
            return;

        RCCP_AIVisionLightPreview_V2.Show(
            visionDetectionResultV2,
            OnVisionLightsApplyV2,
            OnVisionLightsCancelV2,
            apiKey  // Pass API key for re-capture functionality
        );
    }

    /// <summary>
    /// Called when user applies detected lights from V2 preview window.
    /// </summary>
    private void OnVisionLightsApplyV2(List<RCCP_AIVisionLightDetector_V2.DetectedLight> lights) {
        if (lights == null || lights.Count == 0 || !HasRCCPController)
            return;

        // Check for existing lights
        var existingLights = selectedController.GetComponentsInChildren<RCCP_Light>(true);
        int existingCount = existingLights != null ? existingLights.Length : 0;

        bool shouldReplace = false;

        if (existingCount > 0) {
            int choice = EditorUtility.DisplayDialogComplex(
                "Existing Lights Found",
                "This vehicle already has " + existingCount + " light(s).\n\nWhat would you like to do?",
                "Replace All",
                "Cancel",
                "Add to Existing"
            );

            if (choice == 1) {
                Debug.Log("[RCCP AI] Light application cancelled by user");
                return;
            }

            shouldReplace = (choice == 0);
        }

        Undo.RegisterFullObjectHierarchyUndo(selectedController.gameObject, "RCCP AI Vision Lights V2");

        // Get or create RCCP_Lights manager (use direct lookup for Editor safety - cached properties can become stale)
        var lightsManager = selectedController.GetComponentInChildren<RCCP_Lights>(true);
        if (lightsManager == null) {
            GameObject lightsObj = new GameObject("RCCP_Lights");
            Undo.RegisterCreatedObjectUndo(lightsObj, "Create RCCP_Lights");
            lightsObj.transform.SetParent(selectedController.transform);
            lightsObj.transform.localPosition = Vector3.zero;
            lightsObj.transform.localRotation = Quaternion.identity;
            lightsManager = lightsObj.AddComponent<RCCP_Lights>();
        }

        // Remove existing lights if user chose "Replace All"
        if (shouldReplace && existingCount > 0) {
            Debug.Log($"[RCCP AI] Removing {existingCount} existing lights...");
            foreach (var existingLight in existingLights) {
                if (existingLight != null && existingLight.gameObject != null) {
                    Undo.DestroyObjectImmediate(existingLight.gameObject);
                }
            }
            lightsManager.GetAllLights();
        }

        int created = 0;
        int relocated = 0;

        var remainingLights = shouldReplace ? new RCCP_Light[0] : selectedController.GetComponentsInChildren<RCCP_Light>(true);
        float duplicateDistanceThreshold = 0.15f;

        foreach (var detectedLight in lights) {
            if (!detectedLight.enabled) continue;

            // Get the final world position (includes user offset)
            Vector3 worldPos = detectedLight.FinalWorldPosition;
            Vector3 localPos = selectedController.transform.InverseTransformPoint(worldPos);

            // Determine actual vehicle side from local X position
            string actualSide = localPos.x < -0.01f ? "Left" : (localPos.x > 0.01f ? "Right" : "Center");

            // Convert to RCCP light type
            RCCP_Light.LightType lightType = RCCP_AIVisionLightDetector_V2.ToRCCPLightType(detectedLight);

            // Adjust for indicators based on actual position
            if (detectedLight.lightType.ToLower() == "indicator") {
                lightType = localPos.x < 0f
                    ? RCCP_Light.LightType.IndicatorLeftLight
                    : RCCP_Light.LightType.IndicatorRightLight;
            }

            // Set rotation based on front/rear
            Quaternion rotation = detectedLight.view == "front"
                ? Quaternion.identity
                : Quaternion.Euler(0, 180f, 0);

            // Check for existing light of same type nearby
            RCCP_Light existingLight = null;
            if (remainingLights.Length > 0) {
                foreach (var existing in remainingLights) {
                    if (existing == null) continue;
                    if (existing.lightType != lightType) continue;

                    Vector3 existingLocalPos = selectedController.transform.InverseTransformPoint(existing.transform.position);
                    float distance = Vector3.Distance(localPos, existingLocalPos);

                    if (distance < duplicateDistanceThreshold) {
                        existingLight = existing;
                        break;
                    }
                }
            }

            if (existingLight != null) {
                Undo.RecordObject(existingLight.transform, "Relocate Light");
                existingLight.transform.localPosition = localPos;
                existingLight.transform.localRotation = rotation;
                Debug.Log($"[RCCP AI] Relocated existing {lightType} to {localPos}");
                relocated++;
                continue;
            }

            // Create new light
            string lightName = $"RCCP_{detectedLight.lightType}_{actualSide}";
            Color color = RCCP_AIVehicleBuilder.GetDefaultColorForType(lightType);
            float intensity = RCCP_AIVehicleBuilder.GetDefaultIntensityForType(lightType);

            RCCP_AIVehicleBuilder.SpawnLightWithRotation(lightsManager, lightName, localPos, rotation, lightType, color, intensity);
            created++;
        }

        lightsManager.GetAllLights();

        string statusMsg;
        if (relocated > 0 && created > 0)
            statusMsg = $"Created {created} lights, relocated {relocated} existing";
        else if (relocated > 0)
            statusMsg = $"Relocated {relocated} existing lights";
        else
            statusMsg = $"Created {created} lights from V2 vision detection";
        SetStatus(statusMsg, MessageType.Info);
        Debug.Log($"[RCCP AI] {statusMsg}");

        visionDetectionResultV2 = null;
        Repaint();
    }

    /// <summary>
    /// Called when user cancels the V2 vision detection preview.
    /// </summary>
    private void OnVisionLightsCancelV2() {
        Debug.Log("[RCCP AI] Vision detection V2 preview cancelled");
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif