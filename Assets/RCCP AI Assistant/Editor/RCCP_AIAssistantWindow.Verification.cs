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

    // Verification Panel State
    private string invoiceInput = "";
    private bool isVerifying = false;
    private string verificationError = "";
    private string verificationSuccess = "";
    private int verificationRetryAfter = 0;
    private DateTime verificationRetryTime = DateTime.MinValue;

    // Verification Panel Styles
    private GUIStyle verificationTitleStyle;
    private GUIStyle verificationSubtitleStyle;
    private GUIStyle verificationInputStyle;
    private GUIStyle verificationErrorStyle;
    private GUIStyle verificationSuccessStyle;
    private GUIStyle verificationInfoStyle;
    private GUIStyle verificationLinkStyle;
    private bool verificationStylesInitialized = false;

    private void InitializeVerificationStyles() {
        if (verificationStylesInitialized && stylesInitialized) return;

        verificationTitleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelHeader) {
            fontSize = RCCP_AIDesignSystem.Typography.Size3XL,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = AccentColor }
        };

        verificationSubtitleStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        };

        verificationInputStyle = new GUIStyle(RCCP_AIDesignSystem.TextField) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeLG,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(15, 15, 10, 10)
        };

        verificationErrorStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Error }
        };

        verificationSuccessStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            fontSize = RCCP_AIDesignSystem.Typography.SizeBase,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = RCCP_AIDesignSystem.Colors.Success }
        };

        verificationInfoStyle = new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = RCCP_AIDesignSystem.Colors.TextSecondary }
        };

        verificationLinkStyle = new GUIStyle(RCCP_AIDesignSystem.LabelPrimary) {
            normal = { textColor = RCCP_AIDesignSystem.Colors.Info },
            hover = { textColor = RCCP_AIDesignSystem.Colors.Lighten(RCCP_AIDesignSystem.Colors.Info, 0.2f) },
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };

        verificationStylesInitialized = true;
    }

    /// <summary>
    /// Returns true if verification panel should be shown.
    /// Shows when: device is not verified (regardless of API mode).
    /// Verification protects the entire asset against piracy.
    /// </summary>
    private bool ShouldShowVerificationPanel() {
        // Always require verification - protects entire asset regardless of API mode
        return !RCCP_ServerProxy.IsVerified;
    }

    #region UI Drawing - Verification Panel

    private void DrawVerificationPanel() {
        InitializeVerificationStyles();

        // Background - fill entire window
        Rect bgRect = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(bgRect, RCCP_AIDesignSystem.Colors.BgBase);

        // Center the content
        float panelWidth = Mathf.Min(500, position.width - 60);
        float panelHeight = 500;
        float xOffset = (position.width - panelWidth) / 2;
        float yOffset = (position.height - panelHeight) / 2;

        GUILayout.BeginArea(new Rect(xOffset, yOffset, panelWidth, panelHeight));
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelElevated);

        // Header with icon
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space7);
        GUILayout.Label("Purchase Verification", verificationTitleStyle);
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);

        GUILayout.Label(
            "Please verify your Unity Asset Store purchase to use the AI Assistant.",
            verificationSubtitleStyle);

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space6);

        // Invoice input section
        EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelRecessed);
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);

        GUILayout.Label("Invoice / Order Number", RCCP_AIDesignSystem.LabelHeader);
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Invoice input field
        GUI.SetNextControlName("InvoiceInput");
        invoiceInput = EditorGUILayout.TextField(invoiceInput, verificationInputStyle, GUILayout.Height(40));

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
        GUILayout.Label("Enter your Unity Asset Store invoice or order number",
            verificationInfoStyle);

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);
        EditorGUILayout.EndVertical();

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Error/Success messages
        if (!string.IsNullOrEmpty(verificationError)) {
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelRecessed);
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
            GUILayout.Label(verificationError, verificationErrorStyle);
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
            EditorGUILayout.EndVertical();
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
        }

        if (!string.IsNullOrEmpty(verificationSuccess)) {
            EditorGUILayout.BeginVertical(RCCP_AIDesignSystem.PanelRecessed);
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
            GUILayout.Label(verificationSuccess, verificationSuccessStyle);
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
            EditorGUILayout.EndVertical();
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
        }

        // Rate limit warning
        if (verificationRetryTime > DateTime.Now) {
            int secondsRemaining = (int)(verificationRetryTime - DateTime.Now).TotalSeconds;
            int minutesRemaining = Mathf.CeilToInt(secondsRemaining / 60f);
            EditorGUILayout.HelpBox(
                $"Too many attempts. Please wait {minutesRemaining} minute(s) before trying again.",
                MessageType.Warning);
            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);
        }

        // Verify button
        bool canVerify = !string.IsNullOrWhiteSpace(invoiceInput) &&
                        !isVerifying &&
                        verificationRetryTime <= DateTime.Now;

        EditorGUI.BeginDisabledGroup(!canVerify);
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = AccentColor;

        string buttonText = isVerifying ? "Verifying..." : "Verify Purchase";
        if (GUILayout.Button(buttonText, GUILayout.Height(45))) {
            StartVerification();
        }

        GUI.backgroundColor = oldBg;
        EditorGUI.EndDisabledGroup();

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);
        RCCP_AIDesignSystem.DrawSeparator(true);
        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);

        // Help link
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("How do I find my invoice or order number?", verificationLinkStyle)) {
            Application.OpenURL("https://assetstore.unity.com/orders");
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);

        // Troubleshooting link
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Connection issues? Reset session", verificationLinkStyle)) {
            RCCP_ServerProxy.ClearRegistration();
            verificationError = "";
            verificationSuccess = "Session reset. Please try again.";
            Repaint();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);
        EditorGUILayout.EndVertical();
        GUILayout.EndArea();

        // Handle Enter key to submit
        Event e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) {
            if (canVerify && GUI.GetNameOfFocusedControl() == "InvoiceInput") {
                StartVerification();
                e.Use();
            }
        }
    }

    private void StartVerification() {
        if (string.IsNullOrWhiteSpace(invoiceInput)) return;
        if (isVerifying) return;
        if (verificationRetryTime > DateTime.Now) return;

        isVerifying = true;
        verificationError = "";
        verificationSuccess = "";
        Repaint();

        // Ensure registered first
        if (!RCCP_ServerProxy.IsRegistered) {
            RCCP_ServerProxy.RegisterDevice(this, (regSuccess, regMessage) => {
                if (regSuccess) {
                    PerformVerification();
                } else {
                    isVerifying = false;
                    verificationError = $"Failed to connect to server: {regMessage}";
                    Repaint();
                }
            });
        } else {
            PerformVerification();
        }
    }

    private void PerformVerification() {
        RCCP_ServerProxy.VerifyInvoice(this, invoiceInput.Trim(), (result) => {
            isVerifying = false;

            // Handle invalid device token - clear and retry registration
            if (!result.Success && result.Error != null &&
                (result.Error.Contains("Invalid device token") || result.Error.Contains("invalid token"))) {
                Debug.Log("[RCCP AI] Device token invalid, clearing and re-registering...");
                RCCP_ServerProxy.ClearRegistration();
                verificationError = "Session expired. Please try again.";
                Repaint();
                return;
            }

            if (result.Success && result.Verified) {
                verificationSuccess = result.Message ?? "Purchase verified successfully!";
                verificationError = "";
                invoiceInput = "";

                // Refresh the window after short delay
                EditorApplication.delayCall += () => {
                    Repaint();
                };
            } else {
                verificationError = result.Error ?? "Verification failed. Please check your invoice or order number.";
                verificationSuccess = "";

                // Handle rate limiting
                if (result.RetryAfter > 0) {
                    verificationRetryAfter = result.RetryAfter;
                    verificationRetryTime = DateTime.Now.AddSeconds(result.RetryAfter);
                }
            }

            Repaint();
        });
    }

    #endregion

    #region Verification Status Display (for Settings panel)

    /// <summary>
    /// Draws the verification status section for the Settings panel.
    /// </summary>
    private void DrawVerificationStatusSection() {
        RCCP_AIDesignSystem.BeginPanel(RCCP_AIDesignSystem.PanelElevated);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Verification Status", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        // Status indicator
        if (RCCP_ServerProxy.IsVerified) {
            GUILayout.Label("Verified", RCCP_AIDesignSystem.PillSuccess);
        } else {
            GUILayout.Label("Not Verified", RCCP_AIDesignSystem.PillWarning);
        }
        EditorGUILayout.EndHorizontal();

        RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);

        if (RCCP_ServerProxy.IsVerified) {
            // Show verification details
            string maskedInvoice = RCCP_AIEditorPrefs.VerifiedInvoiceMasked;
            string packageName = RCCP_AIEditorPrefs.VerifiedPackageName;
            string verifiedDate = RCCP_AIEditorPrefs.VerificationDate;

            if (!string.IsNullOrEmpty(maskedInvoice)) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Invoice:", GUILayout.Width(80));
                GUILayout.Label(maskedInvoice, RCCP_AIDesignSystem.LabelPrimary);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(packageName)) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Package:", GUILayout.Width(80));
                GUILayout.Label(packageName, RCCP_AIDesignSystem.LabelPrimary);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(verifiedDate)) {
                try {
                    DateTime date = DateTime.Parse(verifiedDate);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Verified:", GUILayout.Width(80));
                    GUILayout.Label(date.ToLocalTime().ToString("g"), RCCP_AIDesignSystem.LabelSmall);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                } catch { }
            }

            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space4);

            // Re-verify button (troubleshooting)
            if (developerMode) {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Verification Cache", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
                    if (EditorUtility.DisplayDialog("Clear Verification",
                        "This will clear the local verification cache. You will need to verify again on next use.\n\nServer-side verification is not affected.",
                        "Clear Cache", "Cancel")) {
                        RCCP_ServerProxy.ClearVerificationCache();
                        Repaint();
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        } else {
            GUILayout.Label("Please verify your purchase to use the AI Assistant.",
                new GUIStyle(RCCP_AIDesignSystem.LabelSmall) {
                    normal = { textColor = RCCP_AIDesignSystem.Colors.Warning },
                    wordWrap = true
                });

            RCCP_AIDesignSystem.Space(RCCP_AIDesignSystem.Spacing.Space2);

            if (GUILayout.Button("Verify Now", GUILayout.Height(RCCP_AIDesignSystem.Heights.Button))) {
                showSettings = false;
                // The verification panel will show automatically
                Repaint();
            }
        }

        RCCP_AIDesignSystem.EndPanel();
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
