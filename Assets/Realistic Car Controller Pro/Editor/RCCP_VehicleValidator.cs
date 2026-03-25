//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;

/// <summary>
/// Centralized vehicle validation system for RCCP.
/// Replaces all individual CheckMisconfig() methods with a unified validator.
/// </summary>
public static class RCCP_VehicleValidator {

    #region Enums and Classes

    /// <summary>
    /// Severity level of validation issues
    /// </summary>
    public enum Severity {
        Info,       // Informational, not necessarily a problem
        Warning,    // Potential issue, vehicle may still work
        Error       // Critical issue, vehicle likely won't work correctly
    }

    /// <summary>
    /// Categories for grouping validation results
    /// </summary>
    public enum Category {
        General,
        Rigidbody,
        Engine,
        Clutch,
        Gearbox,
        Differential,
        Axles,
        Wheels,
        Stability,
        Audio,
        Lights,
        Damage,
        Input,
        OutputEvents
    }

    /// <summary>
    /// Represents a single validation result
    /// </summary>
    [Serializable]
    public class ValidationResult {
        public Severity severity;
        public Category category;
        public string message;
        public string suggestion;
        public UnityEngine.Object targetObject;
        public Action autoFix;

        public ValidationResult(
            Severity severity,
            Category category,
            string message,
            string suggestion = null,
            UnityEngine.Object target = null,
            Action autoFix = null) {
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
    /// Summary of validation results
    /// </summary>
    public class ValidationSummary {
        public int errorCount;
        public int warningCount;
        public int infoCount;
        public bool IsValid => errorCount == 0;
    }

    #endregion

    #region Main Validation Methods

    /// <summary>
    /// Runs all validations on the given vehicle and returns results.
    /// </summary>
    public static List<ValidationResult> ValidateVehicle(RCCP_CarController carController) {
        var results = new List<ValidationResult>();

        if (carController == null) {
            results.Add(new ValidationResult(
                Severity.Error, Category.General,
                "No RCCP vehicle selected"));
            return results;
        }

        // Run all validation checks
        ValidateRequiredComponents(carController, results);
        ValidateRigidbody(carController, results);
        ValidateEngine(carController, results);
        ValidateClutch(carController, results);
        ValidateGearbox(carController, results);
        ValidateDifferentials(carController, results);
        ValidateAxles(carController, results);
        ValidateWheels(carController, results);
        ValidateStability(carController, results);
        ValidateAudio(carController, results);
        ValidateLights(carController, results);
        ValidateDamage(carController, results);
        ValidateInputs(carController, results);
        ValidateOutputEvents(carController, results);

        // Update completeSetup flags on components
        UpdateComponentSetupFlags(carController, results);

        return results;
    }

    /// <summary>
    /// Gets a summary of validation results
    /// </summary>
    public static ValidationSummary GetSummary(List<ValidationResult> results) {
        return new ValidationSummary {
            errorCount = results.Count(r => r.severity == Severity.Error),
            warningCount = results.Count(r => r.severity == Severity.Warning),
            infoCount = results.Count(r => r.severity == Severity.Info)
        };
    }

    /// <summary>
    /// Attempts to auto-fix all fixable issues
    /// </summary>
    public static int AutoFixAll(List<ValidationResult> results) {
        int fixedCount = 0;
        foreach (var result in results.Where(r => r.CanAutoFix)) {
            try {
                result.autoFix?.Invoke();
                fixedCount++;
            } catch (Exception e) {
                Debug.LogError($"Auto-fix failed for: {result.message}\n{e}");
            }
        }
        return fixedCount;
    }

    #endregion

    #region Required Components Validation

    private static void ValidateRequiredComponents(RCCP_CarController carController, List<ValidationResult> results) {
        // Check for required drivetrain components
        if (carController.GetComponentInChildren<RCCP_Engine>(true) == null) {
            results.Add(new ValidationResult(Severity.Error, Category.General,
                "Missing RCCP_Engine component",
                "Add an Engine component to the vehicle", carController));
        }

        if (carController.GetComponentInChildren<RCCP_Clutch>(true) == null) {
            results.Add(new ValidationResult(Severity.Error, Category.General,
                "Missing RCCP_Clutch component",
                "Add a Clutch component to the vehicle", carController));
        }

        if (carController.GetComponentInChildren<RCCP_Gearbox>(true) == null) {
            results.Add(new ValidationResult(Severity.Error, Category.General,
                "Missing RCCP_Gearbox component",
                "Add a Gearbox component to the vehicle", carController));
        }

        if (carController.GetComponentInChildren<RCCP_Differential>(true) == null) {
            results.Add(new ValidationResult(Severity.Error, Category.General,
                "Missing RCCP_Differential component",
                "Add a Differential component to the vehicle", carController));
        }

        var axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        if (axles == null || axles.Length == 0) {
            results.Add(new ValidationResult(Severity.Error, Category.General,
                "Missing RCCP_Axle components",
                "Add at least one Axle component to the vehicle", carController));
        } else if (axles.Length < 2) {
            results.Add(new ValidationResult(Severity.Warning, Category.General,
                "Vehicle has only one axle",
                "Most vehicles have at least 2 axles (front and rear)", carController));
        }

        if (carController.GetComponentInChildren<RCCP_Input>(true) == null) {
            results.Add(new ValidationResult(Severity.Error, Category.General,
                "Missing RCCP_Input component",
                "Add an Input component to the vehicle", carController));
        }

        // Check for body collider
        Collider[] colliders = carController.GetComponentsInChildren<Collider>(true);
        bool hasBodyCollider = colliders.Any(c => !(c is WheelCollider));
        if (!hasBodyCollider) {
            results.Add(new ValidationResult(Severity.Error, Category.General,
                "No body collider found",
                "Add a collider (Box, Mesh, etc.) to the vehicle body", carController));
        }
    }

    #endregion

    #region Rigidbody Validation

    private static void ValidateRigidbody(RCCP_CarController carController, List<ValidationResult> results) {
        Rigidbody rb = carController.GetComponent<Rigidbody>();

        if (rb == null) {
            results.Add(new ValidationResult(Severity.Error, Category.Rigidbody,
                "Missing Rigidbody component",
                "Add a Rigidbody to the vehicle root", carController));
            return;
        }

        // Mass checks (valid range: 600-30000 kg)
        if (rb.mass < 600f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Rigidbody,
                $"Vehicle mass is below minimum ({rb.mass}kg)",
                "Minimum recommended mass is 600kg. Low mass may cause unstable physics", rb));
        } else if (rb.mass > 30000f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Rigidbody,
                $"Vehicle mass exceeds maximum ({rb.mass}kg)",
                "Maximum recommended mass is 30000kg (heavy truck)", rb));
        } else if (rb.mass < 1000f) {
            results.Add(new ValidationResult(Severity.Info, Category.Rigidbody,
                $"Light vehicle mass ({rb.mass}kg)",
                "Compact cars typically weigh 1200kg", rb));
        } else if (rb.mass > 12500f) {
            results.Add(new ValidationResult(Severity.Info, Category.Rigidbody,
                $"Heavy vehicle mass ({rb.mass}kg)",
                "This is typical for heavy trucks (12500kg) or buses (10000kg)", rb));
        }

        // Interpolation check with auto-fix
        if (rb.interpolation == RigidbodyInterpolation.None) {
            results.Add(new ValidationResult(Severity.Warning, Category.Rigidbody,
                "Rigidbody interpolation is disabled",
                "Enable Interpolate for smoother visual movement", rb,
                () => {
                    Undo.RecordObject(rb, "Fix Rigidbody Interpolation");
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    EditorUtility.SetDirty(rb);
                }));
        }

        // Center of Mass checks
        Vector3 com = rb.centerOfMass;

        // HARD RULE: COM X offset should be 0 (no lateral offset)
        if (Mathf.Abs(com.x) > 0.05f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Rigidbody,
                $"Center of mass has lateral offset (X: {com.x:F2})",
                "HARD RULE: COM X offset should be 0 for balanced handling", rb));
        }

        // COM Y range check (-1.20 to 0.00)
        if (com.y < -1.20f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Rigidbody,
                $"Center of mass is very low (Y: {com.y:F2})",
                "Minimum recommended: -1.20m. COM cannot be below wheel center height", rb));
        } else if (com.y > 0.0f) {
            results.Add(new ValidationResult(Severity.Info, Category.Rigidbody,
                $"Center of mass is above origin (Y: {com.y:F2})",
                "Higher COM causes more body roll. Consider lowering for stability", rb));
        }

        // COM Z range check (-0.60 to 0.60)
        if (com.z < -0.60f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Rigidbody,
                $"Center of mass is far back (Z: {com.z:F2})",
                "Maximum rear offset: -0.60m (RWD vehicles may use negative Z)", rb));
        } else if (com.z > 0.60f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Rigidbody,
                $"Center of mass is far forward (Z: {com.z:F2})",
                "Maximum front offset: 0.60m (FWD vehicles may use positive Z)", rb));
        }
    }

    #endregion

    #region Engine Validation

    private static void ValidateEngine(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        if (engine == null) return;

        // RPM range checks (minRPM 600-1500, maxRPM 4200-12000)
        if (engine.minEngineRPM <= 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "Minimum engine RPM is 0 or below",
                "Set minEngineRPM to a positive value (valid range: 600-1500)", engine));
        } else if (engine.minEngineRPM < 600f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Minimum engine RPM is very low ({engine.minEngineRPM})",
                "Typical idle RPM is 600-1500 (buses/trucks: 600, sports cars: 900-1000)", engine));
        }

        if (engine.maxEngineRPM <= engine.minEngineRPM) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                $"Maximum RPM ({engine.maxEngineRPM}) is not greater than minimum RPM ({engine.minEngineRPM})",
                "maxEngineRPM must be higher than minEngineRPM", engine));
        } else if (engine.maxEngineRPM > 12000f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Maximum RPM is very high ({engine.maxEngineRPM})",
                "Maximum recommended is 12000 RPM (typical race cars: 9000)", engine));
        } else if (engine.maxEngineRPM < 4200f) {
            results.Add(new ValidationResult(Severity.Info, Category.Engine,
                $"Low-revving engine ({engine.maxEngineRPM} RPM)",
                "This is typical for diesel trucks (4200 RPM) or buses (4500 RPM)", engine));
        }

        float rpmRange = engine.maxEngineRPM - engine.minEngineRPM;
        if (rpmRange < 2000f && engine.maxEngineRPM > engine.minEngineRPM) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Engine RPM range is very narrow ({rpmRange} RPM)",
                "Typical engines have 3500-8000 RPM operating range", engine));
        }

        // Torque check (90-2500 Nm)
        if (engine.maximumTorqueAsNM <= 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "Engine has no torque output",
                "Set maximumTorqueAsNM (valid range: 90-2500 Nm)", engine));
        } else if (engine.maximumTorqueAsNM < 90f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Engine torque is very low ({engine.maximumTorqueAsNM} Nm)",
                "Minimum recommended is 90 Nm (economy car)", engine));
        } else if (engine.maximumTorqueAsNM > 2500f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Engine torque exceeds maximum ({engine.maximumTorqueAsNM} Nm)",
                "Maximum recommended is 2500 Nm (heavy truck)", engine));
        }

        // Peak RPM check (from editor)
        if (engine.peakRPM < engine.minEngineRPM || engine.peakRPM > engine.maxEngineRPM) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                $"Peak torque RPM ({engine.peakRPM}) is outside engine RPM range ({engine.minEngineRPM}-{engine.maxEngineRPM})",
                "Peak torque RPM should be between minimum and maximum engine RPM", engine));
        }

        // Acceleration/Deceleration rates (from editor)
        if (engine.engineAccelerationRate <= 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "Engine acceleration rate is 0 or below",
                "Set a positive engine acceleration rate", engine));
        }

        if (engine.engineDecelerationRate <= 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "Engine deceleration rate is 0 or below",
                "Set a positive engine deceleration rate", engine));
        }

        // Speed check (0-400 km/h)
        if (engine.maximumSpeed < 0) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Maximum speed is negative ({engine.maximumSpeed})",
                "Set maximumSpeed >= 0 (0 = no limiter, uses drivetrain ratios)", engine));
        } else if (engine.maximumSpeed > 400f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Maximum speed exceeds limit ({engine.maximumSpeed} km/h)",
                "Maximum recommended is 400 km/h", engine));
        }

        // Turbo checks - validate configuration values (not runtime turboChargePsi)
        if (engine.turboCharged && engine.maxTurboChargePsi <= 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "Turbo is enabled but maxTurboChargePsi is zero or below",
                "Set maxTurboChargePsi (valid range: 1-35 PSI)", engine));
        } else if (engine.turboCharged && engine.maxTurboChargePsi > 35f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"maxTurboChargePsi is very high ({engine.maxTurboChargePsi})",
                "Maximum recommended is 35 PSI", engine));
        }

        if (engine.turboCharged && engine.turboChargerCoEfficient <= 1f) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                $"turboChargerCoEfficient should be greater than 1 ({engine.turboChargerCoEfficient})",
                "Set turboChargerCoEfficient > 1 for turbo to provide boost (e.g., 1.25 = 25% boost)", engine));
        }

        // Engine inertia check (0.01-1.00)
        if (engine.engineInertia < 0.01f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Engine inertia is very low ({engine.engineInertia})",
                "Minimum recommended is 0.01 (lower = faster response)", engine));
        } else if (engine.engineInertia > 1.0f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Engine,
                $"Engine inertia is very high ({engine.engineInertia})",
                "Maximum recommended is 1.00 (higher = slower, smoother response)", engine));
        }

        // Temperature simulation (from editor)
        if (engine.simulateEngineTemperature && engine.ambientTemperature >= engine.optimalTemperature) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "Ambient temperature should be lower than optimal temperature",
                "Set ambient temperature below optimal temperature", engine));
        }

        // VVT validation (from editor)
        if (engine.enableVVT && (engine.vvtOptimalRange.x < engine.minEngineRPM || engine.vvtOptimalRange.y > engine.maxEngineRPM)) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "VVT optimal range is outside engine RPM limits",
                "VVT optimal range should be within minimum and maximum engine RPM", engine));
        }

        if (engine.enableVVT && engine.vvtOptimalRange.x >= engine.vvtOptimalRange.y) {
            results.Add(new ValidationResult(Severity.Error, Category.Engine,
                "VVT optimal range minimum value should be lower than maximum",
                "Set VVT optimal range with min < max", engine));
        }
    }

    #endregion

    #region Clutch Validation

    private static void ValidateClutch(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Clutch clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);
        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);

        if (clutch == null) return;

        // HARD RULE: engageRPM > minEngineRPM
        if (engine != null) {
            if (clutch.engageRPM <= engine.minEngineRPM) {
                results.Add(new ValidationResult(Severity.Error, Category.Clutch,
                    $"Clutch engage RPM ({clutch.engageRPM}) is at or below engine idle ({engine.minEngineRPM})",
                    $"HARD RULE: engageRPM must be > minEngineRPM (recommended: {engine.minEngineRPM + 200})", clutch,
                    () => {
                        Undo.RecordObject(clutch, "Fix Clutch Engage RPM");
                        clutch.engageRPM = engine.minEngineRPM + 500f;
                        EditorUtility.SetDirty(clutch);
                    }));
            }

            if (clutch.engageRPM >= engine.maxEngineRPM) {
                results.Add(new ValidationResult(Severity.Error, Category.Clutch,
                    $"Clutch engage RPM ({clutch.engageRPM}) is at or above max engine RPM ({engine.maxEngineRPM})",
                    "Clutch engage RPM must be below maximum engine RPM", clutch));
            }

            if (clutch.engageRPM > engine.maxEngineRPM * 0.5f && clutch.engageRPM < engine.maxEngineRPM) {
                results.Add(new ValidationResult(Severity.Warning, Category.Clutch,
                    $"Clutch engage RPM ({clutch.engageRPM}) is quite high",
                    "High engage RPM may cause slow starts", clutch));
            }
        }

        // Clutch engage RPM range check (900-2500)
        if (clutch.engageRPM < 900f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Clutch,
                $"Clutch engage RPM is low ({clutch.engageRPM})",
                "Recommended range: 900-2500 RPM", clutch));
        } else if (clutch.engageRPM > 2500f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Clutch,
                $"Clutch engage RPM is high ({clutch.engageRPM})",
                "Maximum recommended: 2500 RPM", clutch));
        }

        // Clutch inertia check (0.03-1.00)
        if (clutch.clutchInertia < 0.03f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Clutch,
                $"Clutch inertia is very low ({clutch.clutchInertia})",
                "Minimum recommended: 0.03", clutch));
        } else if (clutch.clutchInertia > 1.0f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Clutch,
                $"Clutch inertia is high ({clutch.clutchInertia})",
                "Maximum recommended: 1.00 (higher = smoother, slower response)", clutch));
        }
    }

    #endregion

    #region Gearbox Validation

    private static void ValidateGearbox(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Gearbox gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);

        if (gearbox == null) return;

        // Gear ratios check (4-10 gears depending on vehicle type)
        if (gearbox.gearRatios == null || gearbox.gearRatios.Length == 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Gearbox,
                "No gear ratios defined",
                "Add gear ratios to the gearbox (typically 5-8 gears)", gearbox));
        } else if (gearbox.gearRatios.Length < 2) {
            results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                $"Only {gearbox.gearRatios.Length} gear ratio defined",
                "Economy cars: 5-6 gears, Sports/Race: 6-8 gears, Trucks: 6-10 gears", gearbox));
        }

        // Check gear ratio ranges (1st: 3.20-5.00, last: 0.55-0.95)
        if (gearbox.gearRatios != null && gearbox.gearRatios.Length > 0) {
            float firstGear = gearbox.gearRatios[0];
            if (firstGear < 3.20f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                    $"First gear ratio is low ({firstGear:F2})",
                    "Recommended 1st gear ratio: 3.20-5.00", gearbox));
            } else if (firstGear > 5.00f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                    $"First gear ratio is high ({firstGear:F2})",
                    "Recommended 1st gear ratio: 3.20-5.00", gearbox));
            }
        }

        // HARD RULES for shift RPM
        if (engine != null) {
            // Rule: shiftUpRPM < maxEngineRPM - 200
            float maxShiftUp = engine.maxEngineRPM - 200f;
            if (gearbox.shiftUpRPM > engine.maxEngineRPM) {
                results.Add(new ValidationResult(Severity.Error, Category.Gearbox,
                    $"Shift up RPM ({gearbox.shiftUpRPM}) exceeds max engine RPM ({engine.maxEngineRPM})",
                    $"HARD RULE: shiftUpRPM must be < maxEngineRPM - 200 (max: {maxShiftUp})", gearbox,
                    () => {
                        Undo.RecordObject(gearbox, "Fix Gearbox ShiftUp RPM");
                        gearbox.shiftUpRPM = engine.maxEngineRPM - 500f;
                        EditorUtility.SetDirty(gearbox);
                    }));
            } else if (gearbox.shiftUpRPM > maxShiftUp) {
                results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                    $"Shift up RPM ({gearbox.shiftUpRPM}) is too close to redline ({engine.maxEngineRPM})",
                    $"HARD RULE: shiftUpRPM should be < {maxShiftUp} (200 RPM margin required)", gearbox));
            }

            // Rule: shiftDownRPM > minEngineRPM + 500
            float minShiftDown = engine.minEngineRPM + 500f;
            if (gearbox.shiftDownRPM < engine.minEngineRPM) {
                results.Add(new ValidationResult(Severity.Error, Category.Gearbox,
                    $"Shift down RPM ({gearbox.shiftDownRPM}) is below min engine RPM ({engine.minEngineRPM})",
                    $"HARD RULE: shiftDownRPM must be > minEngineRPM + 500 (min: {minShiftDown})", gearbox,
                    () => {
                        Undo.RecordObject(gearbox, "Fix Gearbox ShiftDown RPM");
                        gearbox.shiftDownRPM = engine.minEngineRPM + 1500f;
                        EditorUtility.SetDirty(gearbox);
                    }));
            } else if (gearbox.shiftDownRPM < minShiftDown) {
                results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                    $"Shift down RPM ({gearbox.shiftDownRPM}) is too close to idle ({engine.minEngineRPM})",
                    $"HARD RULE: shiftDownRPM should be > {minShiftDown} (500 RPM margin required)", gearbox));
            }
        }

        // Rule: shiftUpRPM - shiftDownRPM >= 1000
        if (gearbox.shiftDownRPM >= gearbox.shiftUpRPM) {
            results.Add(new ValidationResult(Severity.Error, Category.Gearbox,
                $"Shift down RPM ({gearbox.shiftDownRPM}) is not less than shift up RPM ({gearbox.shiftUpRPM})",
                "shiftDownRPM must be lower than shiftUpRPM", gearbox,
                () => {
                    Undo.RecordObject(gearbox, "Fix Gearbox Shift RPM Gap");
                    gearbox.shiftDownRPM = gearbox.shiftUpRPM - 2000f;
                    if (engine != null && gearbox.shiftDownRPM < engine.minEngineRPM + 500f) {
                        gearbox.shiftDownRPM = engine.minEngineRPM + 500f;
                    }
                    EditorUtility.SetDirty(gearbox);
                }));
        } else {
            float rpmGap = gearbox.shiftUpRPM - gearbox.shiftDownRPM;
            if (rpmGap < 1000f) {
                results.Add(new ValidationResult(Severity.Error, Category.Gearbox,
                    $"RPM gap between shift points is too small ({rpmGap} RPM)",
                    $"HARD RULE: shiftUpRPM - shiftDownRPM must be >= 1000 RPM (current: {rpmGap})", gearbox));
            }
        }

        // Shifting time check (0.15-0.80 seconds)
        if (gearbox.shiftingTime < 0.15f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                $"Gear shifting time is very fast ({gearbox.shiftingTime}s)",
                "Minimum recommended is 0.15s (valid range: 0.15-0.80s)", gearbox));
        } else if (gearbox.shiftingTime > 0.80f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                $"Gear shifting time is slow ({gearbox.shiftingTime}s)",
                "Maximum recommended is 0.80s (valid range: 0.15-0.80s)", gearbox));
        }

        // Shift threshold check (0.75-0.95)
        if (gearbox.shiftThreshold < 0.75f) {
            results.Add(new ValidationResult(Severity.Info, Category.Gearbox,
                $"Shift threshold is low ({gearbox.shiftThreshold})",
                "Recommended range: 0.75-0.95", gearbox));
        } else if (gearbox.shiftThreshold > 0.95f) {
            results.Add(new ValidationResult(Severity.Warning, Category.Gearbox,
                $"Shift threshold is very high ({gearbox.shiftThreshold})",
                "Maximum recommended is 0.95", gearbox));
        }
    }

    #endregion

    #region Differential Validation

    private static void ValidateDifferentials(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Differential[] differentials = carController.GetComponentsInChildren<RCCP_Differential>(true);
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);

        if (differentials == null || differentials.Length == 0) return;

        // Track which axles are connected
        HashSet<RCCP_Axle> connectedAxles = new HashSet<RCCP_Axle>();

        foreach (var diff in differentials) {
            if (diff.connectedAxle == null) {
                results.Add(new ValidationResult(Severity.Error, Category.Differential,
                    $"Differential '{diff.gameObject.name}' is not connected to any axle",
                    "Connect the differential to an axle in the inspector", diff));
            } else {
                if (connectedAxles.Contains(diff.connectedAxle)) {
                    results.Add(new ValidationResult(Severity.Error, Category.Differential,
                        $"Multiple differentials connected to same axle '{diff.connectedAxle.gameObject.name}'",
                        "Each axle should have only one differential connected", diff));
                }
                connectedAxles.Add(diff.connectedAxle);
            }

            // Final drive ratio check (2.50-5.50)
            if (diff.finalDriveRatio <= 0) {
                results.Add(new ValidationResult(Severity.Error, Category.Differential,
                    $"Final drive ratio is zero or negative on '{diff.gameObject.name}'",
                    "Set a positive final drive ratio (valid range: 2.50-5.50)", diff));
            } else if (diff.finalDriveRatio < 2.50f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Differential,
                    $"Final drive ratio is low ({diff.finalDriveRatio:F2}) on '{diff.gameObject.name}'",
                    "Minimum recommended: 2.50 (low ratio = high top speed, slow acceleration)", diff));
            } else if (diff.finalDriveRatio > 5.50f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Differential,
                    $"Final drive ratio is high ({diff.finalDriveRatio:F2}) on '{diff.gameObject.name}'",
                    "Maximum recommended: 5.50 (high ratio = fast acceleration, low top speed)", diff));
            }
        }

        // Check that at least one differential has a connected axle (needed for power delivery)
        if (connectedAxles.Count == 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Differential,
                "No differential is connected to an axle",
                "Connect at least one differential to an axle for power delivery", carController));
        }

        // AWD check - should have 2 differentials if both front and rear axles are connected
        bool hasFrontAxleConnected = connectedAxles.Any(a => a.gameObject.name.Contains("Front"));
        bool hasRearAxleConnected = connectedAxles.Any(a => a.gameObject.name.Contains("Rear"));

        if (hasFrontAxleConnected && hasRearAxleConnected && differentials.Length < 2) {
            results.Add(new ValidationResult(Severity.Info, Category.Differential,
                "Multiple axles connected - consider using two differentials for AWD",
                "AWD vehicles typically have separate differentials for front and rear axles", carController));
        }
    }

    #endregion

    #region Axles Validation

    private static void ValidateAxles(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);

        if (axles == null || axles.Length == 0) return;

        bool hasSteeringAxle = false;
        bool hasBrakeAxle = false;

        foreach (var axle in axles) {
            if (axle.isSteer) hasSteeringAxle = true;
            if (axle.isBrake) hasBrakeAxle = true;

            // Steering angle check
            if (axle.isSteer && axle.maxSteerAngle <= 0) {
                results.Add(new ValidationResult(Severity.Error, Category.Axles,
                    $"Steering axle '{axle.gameObject.name}' has no steering angle",
                    "Set maxSteerAngle (typically 30-45 degrees)", axle));
            }

            // Brake torque check
            if (axle.isBrake && axle.maxBrakeTorque <= 0) {
                results.Add(new ValidationResult(Severity.Warning, Category.Axles,
                    $"Brake axle '{axle.gameObject.name}' has no brake torque",
                    "Set maxBrakeTorque (typically 2000-5000 Nm)", axle));
            }

            // Wheel model assignment check
            if (axle.leftWheelModel == null) {
                results.Add(new ValidationResult(Severity.Warning, Category.Axles,
                    $"Axle '{axle.gameObject.name}' has no left wheel model assigned",
                    "Assign the left wheel mesh to leftWheelModel", axle));
            }

            if (axle.rightWheelModel == null) {
                results.Add(new ValidationResult(Severity.Warning, Category.Axles,
                    $"Axle '{axle.gameObject.name}' has no right wheel model assigned",
                    "Assign the right wheel mesh to rightWheelModel", axle));
            }

            // Wheel collider check
            if (axle.leftWheelCollider == null) {
                results.Add(new ValidationResult(Severity.Error, Category.Axles,
                    $"Axle '{axle.gameObject.name}' has no left wheel collider",
                    "Create wheel colliders for this axle", axle));
            } else if (axle.leftWheelCollider.transform.localPosition == Vector3.zero) {
                results.Add(new ValidationResult(Severity.Error, Category.Axles,
                    $"Axle '{axle.gameObject.name}' left wheel collider is at origin",
                    "Position the wheel collider at the correct location", axle.leftWheelCollider));
            }

            if (axle.rightWheelCollider == null) {
                results.Add(new ValidationResult(Severity.Error, Category.Axles,
                    $"Axle '{axle.gameObject.name}' has no right wheel collider",
                    "Create wheel colliders for this axle", axle));
            } else if (axle.rightWheelCollider.transform.localPosition == Vector3.zero) {
                results.Add(new ValidationResult(Severity.Error, Category.Axles,
                    $"Axle '{axle.gameObject.name}' right wheel collider is at origin",
                    "Position the wheel collider at the correct location", axle.rightWheelCollider));
            }
        }

        // Global axle checks
        if (!hasSteeringAxle) {
            results.Add(new ValidationResult(Severity.Error, Category.Axles,
                "No steering axle defined",
                "Enable isSteer on at least one axle (typically front)", carController));
        }

        if (!hasBrakeAxle) {
            results.Add(new ValidationResult(Severity.Error, Category.Axles,
                "No brake axle defined",
                "Enable isBrake on at least one axle (typically all axles)", carController));
        }

        // Note: We don't check isPower on axles here because isPower is dynamically set
        // by RCCP_Differential at runtime. The check for powered axles is handled in
        // ValidateDifferentials by verifying differentials have connected axles.
    }

    #endregion

    #region Wheels Validation

    private static void ValidateWheels(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_WheelCollider[] wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);

        if (wheelColliders == null || wheelColliders.Length == 0) {
            results.Add(new ValidationResult(Severity.Error, Category.Wheels,
                "No wheel colliders found",
                "Add RCCP_WheelCollider components to the vehicle", carController));
            return;
        }

        if (wheelColliders.Length < 4) {
            results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                $"Only {wheelColliders.Length} wheel colliders found",
                "Most vehicles have 4 wheels", carController));
        }

        foreach (var wheelCollider in wheelColliders) {
            WheelCollider wc = wheelCollider.GetComponent<WheelCollider>();
            if (wc == null) {
                results.Add(new ValidationResult(Severity.Error, Category.Wheels,
                    $"RCCP_WheelCollider '{wheelCollider.gameObject.name}' has no WheelCollider component",
                    "Add a WheelCollider component", wheelCollider));
                continue;
            }

            // Radius check
            if (wc.radius <= 0.1f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has very small radius ({wc.radius}m)",
                    "Typical wheel radius is 0.3-0.5m", wc));
            }

            // Suspension distance check (0.10-0.40m)
            if (wc.suspensionDistance <= 0) {
                results.Add(new ValidationResult(Severity.Error, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has no suspension distance",
                    "Set suspensionDistance (valid range: 0.10-0.40m)", wc));
            } else if (wc.suspensionDistance < 0.05f) {
                results.Add(new ValidationResult(Severity.Error, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has very short suspension ({wc.suspensionDistance}m)",
                    "Minimum: 0.05m, recommended: 0.10m+", wc));
            } else if (wc.suspensionDistance < 0.10f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has short suspension ({wc.suspensionDistance}m)",
                    "Minimum recommended: 0.10m (Track/Race preset)", wc));
            } else if (wc.suspensionDistance > 0.40f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has very long suspension ({wc.suspensionDistance}m)",
                    "Maximum recommended: 0.40m", wc));
            }

            // Spring check (20000-250000 N)
            float springForce = wc.suspensionSpring.spring;
            if (springForce <= 0) {
                results.Add(new ValidationResult(Severity.Error, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has no suspension spring force",
                    "Set spring force (valid range: 20000-250000 N)", wc));
            } else if (springForce < 20000f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has weak spring ({springForce} N)",
                    "Minimum recommended: 20000 N", wc));
            } else if (springForce > 250000f) {
                results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                    $"Wheel '{wheelCollider.gameObject.name}' has very stiff spring ({springForce} N)",
                    "Maximum recommended: 250000 N (typical for heavy trucks)", wc));
            }

            // Damper check (Rule: damper = spring x 0.08 to 0.18)
            float damperForce = wc.suspensionSpring.damper;
            if (damperForce > 0 && springForce > 0) {
                float damperRatio = damperForce / springForce;
                if (damperRatio < 0.08f) {
                    results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                        $"Wheel '{wheelCollider.gameObject.name}' damper is weak relative to spring (ratio: {damperRatio:F3})",
                        "Rule: damper should be spring x 0.08 to 0.18", wc));
                } else if (damperRatio > 0.18f) {
                    results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                        $"Wheel '{wheelCollider.gameObject.name}' damper is strong relative to spring (ratio: {damperRatio:F3})",
                        "Rule: damper should be spring x 0.08 to 0.18", wc));
                }
            }

            // Wheel model check
            if (wheelCollider.wheelModel == null) {
                results.Add(new ValidationResult(Severity.Warning, Category.Wheels,
                    $"Wheel collider '{wheelCollider.gameObject.name}' has no wheel model assigned",
                    "Assign the wheel mesh to wheelModel", wheelCollider));
            }

            // Axle connection check (from editor)
            if (wheelCollider.connectedAxle == null) {
                results.Add(new ValidationResult(Severity.Error, Category.Wheels,
                    $"Wheel collider '{wheelCollider.gameObject.name}' is not connected to any axle",
                    "Connect the wheel to an axle", wheelCollider));
            }
        }
    }

    #endregion

    #region Stability Validation

    private static void ValidateStability(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Stability stability = carController.GetComponentInChildren<RCCP_Stability>(true);

        if (stability == null) {
            results.Add(new ValidationResult(Severity.Info, Category.Stability,
                "No stability component found",
                "Add RCCP_Stability for driving assists (ABS, ESP, TCS)", carController));
            return;
        }

        // Check if all assists are disabled (might be intentional)
        if (!stability.ABS && !stability.ESP && !stability.TCS &&
            !stability.steeringHelper && !stability.tractionHelper && !stability.angularDragHelper) {
            results.Add(new ValidationResult(Severity.Info, Category.Stability,
                "All stability assists are disabled",
                "This may be intentional for arcade-style handling", stability));
        }

        // Check helper strengths
        if (stability.steeringHelper && stability.steerHelperStrength <= 0) {
            results.Add(new ValidationResult(Severity.Warning, Category.Stability,
                "Steering helper is enabled but strength is zero",
                "Set steerHelperStrength > 0 or disable the helper", stability));
        }

        if (stability.tractionHelper && stability.tractionHelperStrength <= 0) {
            results.Add(new ValidationResult(Severity.Warning, Category.Stability,
                "Traction helper is enabled but strength is zero",
                "Set tractionHelperStrength > 0 or disable the helper", stability));
        }
    }

    #endregion

    #region Audio Validation

    private static void ValidateAudio(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Audio audio = carController.GetComponentInChildren<RCCP_Audio>(true);

        if (audio == null) {
            results.Add(new ValidationResult(Severity.Info, Category.Audio,
                "No audio component found",
                "Add RCCP_Audio for engine and vehicle sounds", carController));
        }
    }

    #endregion

    #region Lights Validation

    private static void ValidateLights(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Lights lights = carController.GetComponentInChildren<RCCP_Lights>(true);

        if (lights == null) {
            results.Add(new ValidationResult(Severity.Info, Category.Lights,
                "No lights component found",
                "Add RCCP_Lights for headlights, brake lights, etc.", carController));
            return;
        }

        // Check all RCCP_Light components for proper LensFlare ignore layers
        RCCP_Light[] allLights = carController.GetComponentsInChildren<RCCP_Light>(true);

        if (allLights == null || allLights.Length == 0) {
            results.Add(new ValidationResult(Severity.Info, Category.Lights,
                "No individual lights found",
                "Add RCCP_Light components under RCCP_Lights", lights));
            return;
        }

#if !BCG_URP && !BCG_HDRP
        // Get the expected ignore layer mask
        int expectedIgnoreMask = LayerMask.GetMask(
            RCCP_Settings.Instance.RCCPLayer,
            RCCP_Settings.Instance.RCCPWheelColliderLayer,
            RCCP_Settings.Instance.RCCPDetachablePartLayer
        );

        foreach (var light in allLights) {
            LensFlare lensFlare = light.GetComponent<LensFlare>();

            if (lensFlare != null) {
                // Use SerializedObject to read the ignore layers (not exposed in public API)
                SerializedObject serializedFlare = new SerializedObject(lensFlare);
                SerializedProperty ignoreLayersProp = serializedFlare.FindProperty("m_IgnoreLayers");

                if (ignoreLayersProp != null) {
                    int currentMask = ignoreLayersProp.intValue;

                    // Check if the required layers are being ignored
                    if ((currentMask & expectedIgnoreMask) != expectedIgnoreMask) {
                        results.Add(new ValidationResult(Severity.Warning, Category.Lights,
                            $"LensFlare on '{light.gameObject.name}' doesn't ignore vehicle layers",
                            "LensFlare should ignore RCCP_Vehicle, RCCP_WheelCollider, RCCP_DetachablePart layers",
                            lensFlare,
                            () => {
                                // Auto-fix: Set the correct ignore layers
                                SerializedObject so = new SerializedObject(lensFlare);
                                SerializedProperty prop = so.FindProperty("m_IgnoreLayers");
                                if (prop != null) {
                                    prop.intValue = expectedIgnoreMask;
                                    so.ApplyModifiedProperties();
                                    EditorUtility.SetDirty(lensFlare);
                                }
                            }));
                    }
                }
            }
        }
#endif
    }

    #endregion

    #region Damage Validation

    private static void ValidateDamage(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Damage damage = carController.GetComponentInChildren<RCCP_Damage>(true);

        if (damage == null) {
            results.Add(new ValidationResult(Severity.Info, Category.Damage,
                "No damage component found",
                "Add RCCP_Damage for vehicle damage simulation", carController));
            return;  // No damage component = no mesh check needed
        }

        // Check mesh readability for damage system
        MeshFilter[] meshFilters = carController.GetComponentsInChildren<MeshFilter>(true);
        RCCP_WheelCollider[] wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);

        foreach (MeshFilter meshFilter in meshFilters) {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            // Skip if no MeshRenderer (not a visible mesh)
            if (meshFilter.GetComponent<MeshRenderer>() == null)
                continue;

            // Skip wheel meshes (they shouldn't be deformed)
            bool isWheelMesh = false;
            if (wheelColliders != null) {
                foreach (var wc in wheelColliders) {
                    if (wc == null || wc.wheelModel == null)
                        continue;

                    if (meshFilter.transform == wc.wheelModel ||
                        meshFilter.transform.IsChildOf(wc.wheelModel)) {
                        isWheelMesh = true;
                        break;
                    }
                }
            }
            if (isWheelMesh) continue;

            // Check if mesh is readable
            if (!meshFilter.sharedMesh.isReadable) {
                // Get the root model asset (FBX/OBJ file) for selection, not just the mesh
                string assetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                UnityEngine.Object modelAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

                results.Add(new ValidationResult(
                    Severity.Warning,
                    Category.Damage,
                    $"Mesh '{meshFilter.sharedMesh.name}' on '{meshFilter.gameObject.name}' is not readable",
                    "Enable 'Read/Write' in the mesh Import Settings to allow damage deformation",
                    modelAsset ?? meshFilter.sharedMesh  // Target the model asset for easy selection
                ));
            }
        }
    }

    #endregion

    #region Input Validation

    private static void ValidateInputs(RCCP_CarController carController, List<ValidationResult> results) {
        RCCP_Input input = carController.GetComponentInChildren<RCCP_Input>(true);

        if (input == null) return;

        // Input component exists, basic checks can be added here
    }

    #endregion

    #region Output Events Validation

    private static void ValidateOutputEvents(RCCP_CarController carController, List<ValidationResult> results) {
        // Engine output event
        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        if (engine != null) {
            if (engine.outputEvent == null) {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Engine output event not configured",
                    "Add output event listener (Engine -> Clutch)", engine));
            } else if (engine.outputEvent.GetPersistentEventCount() < 1) {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Engine output event has no listeners",
                    "Connect Engine output to Clutch ReceiveOutput method", engine));
            } else if (engine.outputEvent.GetPersistentMethodName(0) == "") {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Engine output event listener method not selected",
                    "Select ReceiveOutput method on Clutch component", engine));
            }
        }

        // Clutch output event
        RCCP_Clutch clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);
        if (clutch != null) {
            if (clutch.outputEvent == null) {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Clutch output event not configured",
                    "Add output event listener (Clutch -> Gearbox)", clutch));
            } else if (clutch.outputEvent.GetPersistentEventCount() < 1) {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Clutch output event has no listeners",
                    "Connect Clutch output to Gearbox ReceiveOutput method", clutch));
            } else if (clutch.outputEvent.GetPersistentMethodName(0) == "") {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Clutch output event listener method not selected",
                    "Select ReceiveOutput method on Gearbox component", clutch));
            }
        }

        // Gearbox output event
        RCCP_Gearbox gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        if (gearbox != null) {
            if (gearbox.outputEvent == null) {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Gearbox output event not configured",
                    "Add output event listener (Gearbox -> Differential)", gearbox));
            } else if (gearbox.outputEvent.GetPersistentEventCount() < 1) {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Gearbox output event has no listeners",
                    "Connect Gearbox output to Differential ReceiveOutput method", gearbox));
            } else if (gearbox.outputEvent.GetPersistentMethodName(0) == "") {
                results.Add(new ValidationResult(Severity.Error, Category.OutputEvents,
                    "Gearbox output event listener method not selected",
                    "Select ReceiveOutput method on Differential component", gearbox));
            }
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Updates completeSetup flags on all components based on validation results
    /// </summary>
    private static void UpdateComponentSetupFlags(RCCP_CarController carController, List<ValidationResult> results) {
        // Get all components
        RCCP_Engine engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        RCCP_Clutch clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);
        RCCP_Gearbox gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        RCCP_Differential[] differentials = carController.GetComponentsInChildren<RCCP_Differential>(true);
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        RCCP_Axles axlesManager = carController.GetComponentInChildren<RCCP_Axles>(true);
        RCCP_WheelCollider[] wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);

        // Check for errors per category
        bool hasEngineErrors = results.Any(r => r.severity == Severity.Error &&
            (r.category == Category.Engine || (r.category == Category.OutputEvents && r.targetObject == engine)));
        bool hasClutchErrors = results.Any(r => r.severity == Severity.Error &&
            (r.category == Category.Clutch || (r.category == Category.OutputEvents && r.targetObject == clutch)));
        bool hasGearboxErrors = results.Any(r => r.severity == Severity.Error &&
            (r.category == Category.Gearbox || (r.category == Category.OutputEvents && r.targetObject == gearbox)));
        bool hasDifferentialErrors = results.Any(r => r.severity == Severity.Error && r.category == Category.Differential);
        bool hasAxleErrors = results.Any(r => r.severity == Severity.Error && r.category == Category.Axles);
        bool hasWheelErrors = results.Any(r => r.severity == Severity.Error && r.category == Category.Wheels);

        // Update flags
        if (engine != null) engine.completeSetup = !hasEngineErrors;
        if (clutch != null) clutch.completeSetup = !hasClutchErrors;
        if (gearbox != null) gearbox.completeSetup = !hasGearboxErrors;

        if (differentials != null) {
            foreach (var diff in differentials) {
                bool thisDiffHasError = results.Any(r => r.severity == Severity.Error && r.targetObject == diff);
                diff.completeSetup = !thisDiffHasError && !hasDifferentialErrors;
            }
        }

        if (axles != null) {
            foreach (var axle in axles) {
                bool thisAxleHasError = results.Any(r => r.severity == Severity.Error && r.targetObject == axle);
                axle.completeSetup = !thisAxleHasError && !hasAxleErrors;
            }
        }

        if (axlesManager != null) {
            axlesManager.completeSetup = !hasAxleErrors;
        }

        if (wheelColliders != null) {
            foreach (var wc in wheelColliders) {
                bool thisWheelHasError = results.Any(r => r.severity == Severity.Error &&
                    (r.targetObject == wc || r.targetObject == wc.GetComponent<WheelCollider>()));
                wc.completeSetup = !thisWheelHasError && !hasWheelErrors;
            }
        }
    }

    #endregion
}
#endif
