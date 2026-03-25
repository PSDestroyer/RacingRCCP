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

/// <summary>
/// Diagnostic system for checking RCCP vehicle configurations and finding issues.
/// This class now wraps the central RCCP_VehicleValidator for backward compatibility
/// with the AI Assistant's diagnostic UI.
/// </summary>
public static class RCCP_AIVehicleDiagnostics {

    /// <summary>
    /// Severity level of a diagnostic issue (maps to RCCP_VehicleValidator.Severity)
    /// </summary>
    public enum Severity {
        Info,       // Informational, not necessarily a problem
        Warning,    // Potential issue, vehicle may still work
        Error       // Critical issue, vehicle likely won't work correctly
    }

    /// <summary>
    /// Represents a single diagnostic result (backward compatible format)
    /// </summary>
    public class DiagnosticResult {
        public Severity severity;
        public string category;
        public string message;
        public string suggestion;
        public UnityEngine.Object targetObject;
        public Action autoFix;

        public DiagnosticResult(Severity severity, string category, string message, string suggestion = null, UnityEngine.Object target = null, Action autoFix = null) {
            this.severity = severity;
            this.category = category;
            this.message = message;
            this.suggestion = suggestion;
            this.targetObject = target;
            this.autoFix = autoFix;
        }

        public bool CanAutoFix => autoFix != null;
    }

    /// <summary>
    /// Runs all diagnostics on the given vehicle and returns a list of issues found.
    /// Delegates to RCCP_VehicleValidator on V2.2+, falls back to local checks on V2.0.
    /// </summary>
    public static List<DiagnosticResult> RunDiagnostics(RCCP_CarController carController) {
        var results = new List<DiagnosticResult>();

        if (carController == null) {
            results.Add(new DiagnosticResult(Severity.Error, "General", "No RCCP vehicle selected"));
            return results;
        }

#if RCCP_V2_2_OR_NEWER
        // Delegate to central validator (V2.2+)
        var validatorResults = RCCP_VehicleValidator.ValidateVehicle(carController);

        // Convert to legacy DiagnosticResult format for backward compatibility
        foreach (var vr in validatorResults) {
            results.Add(new DiagnosticResult(
                ConvertSeverity(vr.severity),
                vr.category.ToString(),
                vr.message,
                vr.suggestion,
                vr.targetObject,
                vr.autoFix
            ));
        }
#else
        // Fallback to local diagnostics (V2.0 compatibility)
        RunLocalDiagnostics(carController, results);
#endif

        return results;
    }

#if RCCP_V2_2_OR_NEWER
    /// <summary>
    /// Converts central validator severity to AI diagnostics severity
    /// </summary>
    private static Severity ConvertSeverity(RCCP_VehicleValidator.Severity severity) {
        return severity switch {
            RCCP_VehicleValidator.Severity.Info => Severity.Info,
            RCCP_VehicleValidator.Severity.Warning => Severity.Warning,
            RCCP_VehicleValidator.Severity.Error => Severity.Error,
            _ => Severity.Info
        };
    }
#endif

    /// <summary>
    /// Runs local diagnostics for V2.0 compatibility.
    /// Provides basic checks without relying on RCCP_VehicleValidator.
    /// </summary>
    private static void RunLocalDiagnostics(RCCP_CarController carController, List<DiagnosticResult> results) {
        // Check for Rigidbody
        var rb = carController.GetComponent<Rigidbody>();
        if (rb == null) {
            results.Add(new DiagnosticResult(Severity.Error, "Physics", "Missing Rigidbody component",
                "Add a Rigidbody component to the vehicle", carController));
        } else {
            if (rb.mass < 100f) {
                results.Add(new DiagnosticResult(Severity.Warning, "Physics", $"Vehicle mass ({rb.mass}kg) is very low",
                    "Consider increasing mass to at least 500kg for realistic behavior", rb));
            }
            if (rb.mass > 10000f) {
                results.Add(new DiagnosticResult(Severity.Warning, "Physics", $"Vehicle mass ({rb.mass}kg) is very high",
                    "Consider reducing mass for better performance", rb));
            }
        }

        // Check for Engine
        var engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        if (engine == null) {
            results.Add(new DiagnosticResult(Severity.Error, "Drivetrain", "Missing Engine component",
                "Add an RCCP_Engine component to the vehicle", carController));
        } else {
            if (engine.maximumTorqueAsNM <= 0) {
                results.Add(new DiagnosticResult(Severity.Error, "Drivetrain", "Engine has zero or negative torque",
                    "Set maximumTorqueAsNM to a positive value", engine));
            }
        }

        // Check for Gearbox
        var gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        if (gearbox == null) {
            results.Add(new DiagnosticResult(Severity.Error, "Drivetrain", "Missing Gearbox component",
                "Add an RCCP_Gearbox component to the vehicle", carController));
        } else {
            if (gearbox.gearRatios == null || gearbox.gearRatios.Length == 0) {
                results.Add(new DiagnosticResult(Severity.Error, "Drivetrain", "Gearbox has no gear ratios",
                    "Configure gear ratios in the gearbox component", gearbox));
            }
        }

        // Check for Axles
        var axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        if (axles == null || axles.Length == 0) {
            results.Add(new DiagnosticResult(Severity.Error, "Chassis", "No axles found on vehicle",
                "Add RCCP_Axle components for front and rear axles", carController));
        } else {
            foreach (var axle in axles) {
                if (axle.leftWheelCollider == null || axle.rightWheelCollider == null) {
                    results.Add(new DiagnosticResult(Severity.Warning, "Chassis", $"Axle '{axle.name}' is missing wheel collider references",
                        "Assign wheel colliders to the axle", axle));
                }
            }
        }

        // Check for Differential
        var diffs = carController.GetComponentsInChildren<RCCP_Differential>(true);
        if (diffs == null || diffs.Length == 0) {
            results.Add(new DiagnosticResult(Severity.Warning, "Drivetrain", "No differential found",
                "Add an RCCP_Differential component for power distribution", carController));
        }

        // Check for Stability (optional but recommended)
        var stability = carController.GetComponentInChildren<RCCP_Stability>(true);
        if (stability == null) {
            results.Add(new DiagnosticResult(Severity.Info, "Handling", "No stability component found",
                "Consider adding RCCP_Stability for ABS, ESP, and TCS features", carController));
        }

        // Check for AeroDynamics (optional but recommended)
        var aero = carController.GetComponentInChildren<RCCP_AeroDynamics>(true);
        if (aero == null) {
            results.Add(new DiagnosticResult(Severity.Info, "Physics", "No aerodynamics component found",
                "Consider adding RCCP_AeroDynamics for downforce and air resistance", carController));
        }

        // If no issues found, add a success message
        if (results.Count == 0) {
            results.Add(new DiagnosticResult(Severity.Info, "General", "No issues found - vehicle configuration looks good!"));
        }
    }

    /// <summary>
    /// Gets a summary of diagnostic results
    /// </summary>
    public static (int errors, int warnings, int info) GetSummary(List<DiagnosticResult> results) {
        int errors = results.Count(r => r.severity == Severity.Error);
        int warnings = results.Count(r => r.severity == Severity.Warning);
        int info = results.Count(r => r.severity == Severity.Info);
        return (errors, warnings, info);
    }

    /// <summary>
    /// Attempts to automatically fix common issues.
    /// Delegates to central validator's AutoFixAll on V2.2+, executes local fixes on V2.0.
    /// </summary>
    public static int AutoFixIssues(RCCP_CarController carController, List<DiagnosticResult> results) {
        if (carController == null) return 0;

#if RCCP_V2_2_OR_NEWER
        // Get fresh results from central validator with auto-fix actions
        var validatorResults = RCCP_VehicleValidator.ValidateVehicle(carController);

        // Execute all auto-fixes
        return RCCP_VehicleValidator.AutoFixAll(validatorResults);
#else
        // Fallback: execute local autoFix actions from results
        int fixedCount = 0;
        foreach (var result in results) {
            if (result.CanAutoFix) {
                try {
                    result.autoFix?.Invoke();
                    fixedCount++;
                } catch (System.Exception ex) {
                    UnityEngine.Debug.LogWarning($"[RCCP AI] Auto-fix failed for '{result.message}': {ex.Message}");
                }
            }
        }
        return fixedCount;
#endif
    }
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
