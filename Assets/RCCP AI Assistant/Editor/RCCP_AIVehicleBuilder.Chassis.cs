//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Partial class containing axle, wheel, suspension, and rigidbody methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Rigidbody Settings

    /// <summary>
    /// Applies rigidbody settings (mass and optionally COM) to the vehicle.
    /// </summary>
    /// <param name="carController">The vehicle to configure</param>
    /// <param name="config">Configuration containing mass and COM settings</param>
    /// <param name="skipCOM">When true, skips COM setting (used during vehicle creation to let RCCP_AeroDynamics.Reset() auto-calculate)</param>
    private static void ApplyRigidbodySettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config, bool skipCOM = false) {
        Rigidbody rb = carController.GetComponent<Rigidbody>();
        if (rb == null) return;

        if (config.vehicleConfig != null) {
            Undo.RecordObject(rb, "RCCP AI Rigidbody");

            // Only set mass if it's a positive value (guard against zeroing out mass)
            // In restore mode (IsRestoreMode), we apply all values including zero
            if (config.vehicleConfig.mass > 0 || IsRestoreMode) {
                rb.mass = config.vehicleConfig.mass;
            }

            // Only apply COM if not skipped (skip during creation to let RCCP auto-calculate from mesh)
            if (!skipCOM && config.vehicleConfig.centerOfMassOffset != null) {
                // Use RCCP_AeroDynamics COM child object instead of direct rigidbody assignment
                RCCP_AeroDynamics aero = carController.GetComponentInChildren<RCCP_AeroDynamics>(true);
                if (aero != null && aero.COM != null) {
                    Undo.RecordObject(aero.COM, "RCCP AI COM");
#if RCCP_V2_2_OR_NEWER
                    aero.SetCOMOffset(config.vehicleConfig.centerOfMassOffset.ToVector3());
#else
                    // V2.0: Directly set COM transform local position
                    aero.COM.localPosition = config.vehicleConfig.centerOfMassOffset.ToVector3();
#endif
                }
            }
        }

        // NOTE: We intentionally do NOT rename the vehicle or its children.
        // Users prefer to keep their original model names intact.
    }

    #endregion

    #region Axle Settings

    private static void ApplyAxleSettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config) {
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.axle;

        // Get front/rear axles by comparing wheel Z positions
        FindFrontRearAxles(carController, out RCCP_Axle frontAxleRef, out RCCP_Axle rearAxleRef);

        foreach (var axle in axles) {
            Undo.RecordObject(axle, "RCCP AI Axle");

            bool isFront = (axle == frontAxleRef);
            RCCP_AIConfig.AxleConfig axleConfig = isFront ? config.axles?.front : config.axles?.rear;

            if (axleConfig != null) {
                axle.isSteer = axleConfig.isSteer;
                axle.isBrake = axleConfig.isBrake;
                axle.isHandbrake = axleConfig.isHandbrake;
                axle.maxSteerAngle = axleConfig.maxSteerAngle > 0 ? axleConfig.maxSteerAngle : defaults?.maxSteerAngle ?? 40f;
                axle.maxBrakeTorque = axleConfig.maxBrakeTorque > 0 ? axleConfig.maxBrakeTorque : defaults?.maxBrakeTorque ?? 3000f;
                axle.antirollForce = axleConfig.antirollForce > 0 ? axleConfig.antirollForce : defaults?.antirollForce ?? 500f;
                axle.steerSpeed = axleConfig.steerSpeed > 0 ? axleConfig.steerSpeed : defaults?.steerSpeed ?? 5f;
                axle.powerMultiplier = axleConfig.powerMultiplier != 0f ? Mathf.Clamp(axleConfig.powerMultiplier, -1f, 1f) : defaults?.powerMultiplier ?? 1f;
                axle.steerMultiplier = axleConfig.steerMultiplier != 0f ? Mathf.Clamp(axleConfig.steerMultiplier, -1f, 1f) : defaults?.steerMultiplier ?? 1f;
                axle.brakeMultiplier = axleConfig.brakeMultiplier > 0 ? Mathf.Clamp01(axleConfig.brakeMultiplier) : defaults?.brakeMultiplier ?? 1f;

                float handbrakeMultiplier = axleConfig.handbrakeMultiplier > 0
                    ? Mathf.Clamp01(axleConfig.handbrakeMultiplier)
                    : defaults?.handbrakeMultiplier ?? 1f;

                if (axleConfig.maxHandbrakeTorque > 0 && axleConfig.handbrakeMultiplier <= 0f) {
                    float baseTorque = axle.maxBrakeTorque > 0f ? axle.maxBrakeTorque : (defaults?.maxBrakeTorque ?? 3000f);
                    if (baseTorque > 0f)
                        handbrakeMultiplier = Mathf.Clamp01(axleConfig.maxHandbrakeTorque / baseTorque);
                }

                axle.handbrakeMultiplier = handbrakeMultiplier;
                // Note: isPower is set automatically by RCCP_Differential at runtime
            } else {
                // No config - apply all defaults
                if (isFront) {
                    axle.isSteer = defaults?.frontIsSteer ?? true;
                    axle.isBrake = defaults?.frontIsBrake ?? true;
                    axle.isHandbrake = defaults?.frontIsHandbrake ?? false;
                } else {
                    axle.isSteer = defaults?.rearIsSteer ?? false;
                    axle.isBrake = defaults?.rearIsBrake ?? true;
                    axle.isHandbrake = defaults?.rearIsHandbrake ?? true;
                }
                axle.maxSteerAngle = defaults?.maxSteerAngle ?? 40f;
                axle.maxBrakeTorque = defaults?.maxBrakeTorque ?? 3000f;
                axle.antirollForce = defaults?.antirollForce ?? 500f;
                axle.steerSpeed = defaults?.steerSpeed ?? 5f;
                axle.powerMultiplier = defaults?.powerMultiplier ?? 1f;
                axle.steerMultiplier = defaults?.steerMultiplier ?? 1f;
                axle.brakeMultiplier = defaults?.brakeMultiplier ?? 1f;
                axle.handbrakeMultiplier = defaults?.handbrakeMultiplier ?? 1f;
                // Note: isPower is set automatically by RCCP_Differential at runtime
            }

            EditorUtility.SetDirty(axle);
        }
    }

    private static void ApplyAxleSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.AxlesConfig config, RCCP_AIConfig.AxlesConfig configAllTrue = null) {
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        if (axles.Length == 0) return;

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.axle;

        // Get front/rear axles by comparing wheel Z positions
        FindFrontRearAxles(carController, out RCCP_Axle frontAxleRef, out _);

        foreach (var axle in axles) {
            bool isFront = (axle == frontAxleRef);
            RCCP_AIConfig.AxleConfig axleConfig = isFront ? config.front : config.rear;
            RCCP_AIConfig.AxleConfig axleAllTrue = isFront ? configAllTrue?.front : configAllTrue?.rear;

            if (axleConfig == null) continue;

            Undo.RecordObject(axle, "RCCP AI Axle");

            if (IsRestoreMode) {
                // In restore mode, apply ALL values unconditionally
                axle.isSteer = axleConfig.isSteer;
                axle.isBrake = axleConfig.isBrake;
                axle.isHandbrake = axleConfig.isHandbrake;
                axle.maxSteerAngle = axleConfig.maxSteerAngle;
                axle.maxBrakeTorque = axleConfig.maxBrakeTorque;
                axle.antirollForce = axleConfig.antirollForce;
                axle.steerSpeed = axleConfig.steerSpeed;
                axle.powerMultiplier = axleConfig.powerMultiplier;
                axle.steerMultiplier = axleConfig.steerMultiplier;
                axle.brakeMultiplier = axleConfig.brakeMultiplier;
                axle.handbrakeMultiplier = axleConfig.handbrakeMultiplier;
            } else {
                // Partial update mode - ONLY change values that are explicitly specified
                void ApplyBool(ref bool field, bool value, bool allTrueValue) {
                    if (value) field = true;
                    else if (configAllTrue != null && !allTrueValue) field = false;
                }

                // Helper to detect if a float field was actually present in the JSON
                // Uses the allTrue config (initialized to NaN, then JSON-overwritten) to detect presence
                bool HasExplicitFloat(float value, float? allTrueValue) {
                    if (configAllTrue != null)
                        return allTrueValue.HasValue && !float.IsNaN(allTrueValue.Value);
                    return !Mathf.Approximately(value, 0f);
                }

                ApplyBool(ref axle.isSteer, axleConfig.isSteer, axleAllTrue?.isSteer ?? true);
                ApplyBool(ref axle.isBrake, axleConfig.isBrake, axleAllTrue?.isBrake ?? true);
                ApplyBool(ref axle.isHandbrake, axleConfig.isHandbrake, axleAllTrue?.isHandbrake ?? true);

                // Use HasExplicitFloat to detect actual JSON presence (prevents 0 defaults from being applied)
                if (HasExplicitFloat(axleConfig.maxSteerAngle, axleAllTrue?.maxSteerAngle))
                    axle.maxSteerAngle = axleConfig.maxSteerAngle;
                if (HasExplicitFloat(axleConfig.maxBrakeTorque, axleAllTrue?.maxBrakeTorque))
                    axle.maxBrakeTorque = axleConfig.maxBrakeTorque;
                if (HasExplicitFloat(axleConfig.antirollForce, axleAllTrue?.antirollForce))
                    axle.antirollForce = axleConfig.antirollForce;
                if (HasExplicitFloat(axleConfig.steerSpeed, axleAllTrue?.steerSpeed))
                    axle.steerSpeed = axleConfig.steerSpeed;

                if (HasExplicitFloat(axleConfig.powerMultiplier, axleAllTrue?.powerMultiplier))
                    axle.powerMultiplier = Mathf.Clamp(axleConfig.powerMultiplier, -1f, 1f);
                if (HasExplicitFloat(axleConfig.steerMultiplier, axleAllTrue?.steerMultiplier))
                    axle.steerMultiplier = Mathf.Clamp(axleConfig.steerMultiplier, -1f, 1f);
                if (HasExplicitFloat(axleConfig.brakeMultiplier, axleAllTrue?.brakeMultiplier))
                    axle.brakeMultiplier = Mathf.Clamp01(axleConfig.brakeMultiplier);

                if (HasExplicitFloat(axleConfig.handbrakeMultiplier, axleAllTrue?.handbrakeMultiplier)) {
                    axle.handbrakeMultiplier = Mathf.Clamp01(axleConfig.handbrakeMultiplier);
                } else if (axleConfig.maxHandbrakeTorque > 0f) {
                    float baseTorque = axle.maxBrakeTorque > 0f ? axle.maxBrakeTorque : (defaults?.maxBrakeTorque ?? 3000f);
                    if (baseTorque > 0f)
                        axle.handbrakeMultiplier = Mathf.Clamp01(axleConfig.maxHandbrakeTorque / baseTorque);
                }
            }

            EditorUtility.SetDirty(axle);
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Axle settings updated");
    }

    #endregion

    #region Wheel Settings

    private static void ApplyWheelSettings(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config) {
        RCCP_WheelCollider[] wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);

        if (wheelColliders.Length == 0) return;

        // Get defaults for fallback values
        var wcDefaults = RCCP_AIComponentDefaults.Instance?.wheelCollider;

        // Get friction presets
        var frictionPreset = RCCP_AIConfig.FrictionPresets.GetPreset(config.wheelFriction?.type ?? "Balanced");

        // Use custom friction if provided, otherwise use preset, otherwise use defaults
        WheelFrictionCurve forwardFriction = config.wheelFriction?.forward?.ToWheelFrictionCurve()
            ?? frictionPreset.forward.ToWheelFrictionCurve();

        WheelFrictionCurve sidewaysFriction = config.wheelFriction?.sideways?.ToWheelFrictionCurve()
            ?? frictionPreset.sideways.ToWheelFrictionCurve();

        // Get axles for per-axle wheel config
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        HashSet<RCCP_WheelCollider> frontWheels = new HashSet<RCCP_WheelCollider>();
        HashSet<RCCP_WheelCollider> rearWheels = new HashSet<RCCP_WheelCollider>();

        foreach (var axle in axles) {
            bool isFront = axle.gameObject.name.ToLower().Contains("front");
            if (axle.leftWheelCollider != null) {
                if (isFront) frontWheels.Add(axle.leftWheelCollider);
                else rearWheels.Add(axle.leftWheelCollider);
            }
            if (axle.rightWheelCollider != null) {
                if (isFront) frontWheels.Add(axle.rightWheelCollider);
                else rearWheels.Add(axle.rightWheelCollider);
            }
        }

        foreach (var wheelCollider in wheelColliders) {
            WheelCollider wc = wheelCollider.GetComponent<WheelCollider>();
            if (wc == null) continue;

            Undo.RecordObject(wc, "RCCP AI WheelCollider");
            Undo.RecordObject(wheelCollider, "RCCP AI WheelCollider");

            // Determine if front or rear for per-axle config
            bool isFront = frontWheels.Contains(wheelCollider);
            RCCP_AIConfig.AxleWheelConfig axleConfig = isFront
                ? config.wheels?.front
                : config.wheels?.rear;

            // Note: Wheel radius is NOT set here - RCCP calculates it automatically from wheel mesh

            // Wheel dimensions - width (RCCP_WheelCollider has width property)
            float width = axleConfig?.wheelWidth ?? config.wheels?.wheelWidth ?? 0;
            if (width > 0) {
                wheelCollider.width = width;
            } else {
                wheelCollider.width = wcDefaults?.wheelWidth ?? 0.25f;
            }

            // Wheel alignment - camber
            // For vehicle creation, use non-zero check (0 means use defaults)
            float camber = axleConfig?.camber ?? config.wheels?.camber ?? 0;
            if (!Mathf.Approximately(camber, 0f) || IsRestoreMode) {
                wheelCollider.camber = camber;
            } else {
                wheelCollider.camber = wcDefaults?.camber ?? 0f;
            }

            // Wheel alignment - caster
            // For vehicle creation, use non-zero check (0 means use defaults)
            float caster = axleConfig?.caster ?? config.wheels?.caster ?? 0;
            if (!Mathf.Approximately(caster, 0f) || IsRestoreMode) {
                wheelCollider.caster = caster;
            } else {
                wheelCollider.caster = wcDefaults?.caster ?? 0f;
            }

            // Grip multiplier (0-2, default 1.0) - V2.2+ only
            float grip = axleConfig?.grip ?? config.wheels?.grip ?? 0;
#if RCCP_V2_2_OR_NEWER
            if (grip > 0 || IsRestoreMode) {
                wheelCollider.grip = Mathf.Clamp(grip > 0 ? grip : 1f, 0f, 2f);
            }
#endif

            // Suspension - use config values or defaults
            if (config.suspension != null) {
                wc.suspensionDistance = config.suspension.distance > 0 ? config.suspension.distance : wcDefaults?.suspensionDistance ?? 0.2f;

                JointSpring spring = wc.suspensionSpring;
                spring.spring = config.suspension.spring > 0 ? config.suspension.spring : wcDefaults?.suspensionSpring ?? 35000f;
                spring.damper = config.suspension.damper > 0 ? config.suspension.damper : wcDefaults?.suspensionDamper ?? 3500f;
                wc.suspensionSpring = spring;
            } else {
                // Apply defaults
                wc.suspensionDistance = wcDefaults?.suspensionDistance ?? 0.2f;

                JointSpring spring = wc.suspensionSpring;
                spring.spring = wcDefaults?.suspensionSpring ?? 35000f;
                spring.damper = wcDefaults?.suspensionDamper ?? 3500f;
                wc.suspensionSpring = spring;
            }

            // Friction
            wc.forwardFriction = forwardFriction;
            wc.sidewaysFriction = sidewaysFriction;

            EditorUtility.SetDirty(wc);
            EditorUtility.SetDirty(wheelCollider);
        }
    }

    private static void ApplyWheelGeometryPartial(RCCP_CarController carController, RCCP_AIConfig.WheelConfig config, RCCP_AIConfig.WheelConfig configAllTrue = null) {
        RCCP_WheelCollider[] wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);
        if (wheelColliders.Length == 0) return;

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.wheelCollider;

        // Get axles for per-axle config
        RCCP_Axle[] axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        FindFrontRearAxles(carController, out RCCP_Axle frontAxleRef, out RCCP_Axle rearAxleRef);

        HashSet<RCCP_WheelCollider> frontWheels = new HashSet<RCCP_WheelCollider>();
        HashSet<RCCP_WheelCollider> rearWheels = new HashSet<RCCP_WheelCollider>();

        foreach (var axle in axles) {
            bool isFront = (axle == frontAxleRef);
            if (axle.leftWheelCollider != null) {
                if (isFront) frontWheels.Add(axle.leftWheelCollider);
                else rearWheels.Add(axle.leftWheelCollider);
            }
            if (axle.rightWheelCollider != null) {
                if (isFront) frontWheels.Add(axle.rightWheelCollider);
                else rearWheels.Add(axle.rightWheelCollider);
            }
        }

        // Helper to detect if a float field was actually present in the JSON
        bool HasExplicitFloat(float value, float? allTrueValue) {
            if (configAllTrue != null)
                return allTrueValue.HasValue && !float.IsNaN(allTrueValue.Value);
            return !Mathf.Approximately(value, 0f);
        }

        foreach (var wheelCollider in wheelColliders) {
            WheelCollider wc = wheelCollider.GetComponent<WheelCollider>();
            if (wc == null) continue;

            Undo.RecordObject(wc, "RCCP AI WheelGeometry");
            Undo.RecordObject(wheelCollider, "RCCP AI WheelGeometry");

            // Determine if front or rear for per-axle config
            bool isFront = frontWheels.Contains(wheelCollider);
            RCCP_AIConfig.AxleWheelConfig axleConfig = isFront ? config.front : config.rear;
            RCCP_AIConfig.AxleWheelConfig axleAllTrue = isFront ? configAllTrue?.front : configAllTrue?.rear;

            // Note: Wheel radius is NOT set here - RCCP calculates it automatically from wheel mesh

            // Wheel width - check axle-specific first, then base config
            float width = axleConfig?.wheelWidth ?? config.wheelWidth;
            float? widthAllTrue = axleAllTrue?.wheelWidth ?? configAllTrue?.wheelWidth;
            if (IsRestoreMode || HasExplicitFloat(width, widthAllTrue)) {
                if (width > 0) wheelCollider.width = width;
            }

            // Camber - check axle-specific first, then base config
            float camber = axleConfig?.camber ?? config.camber;
            float? camberAllTrue = axleAllTrue?.camber ?? configAllTrue?.camber;
            if (IsRestoreMode || HasExplicitFloat(camber, camberAllTrue)) {
                wheelCollider.camber = camber;
            }

            // Caster - check axle-specific first, then base config
            float caster = axleConfig?.caster ?? config.caster;
            float? casterAllTrue = axleAllTrue?.caster ?? configAllTrue?.caster;
            if (IsRestoreMode || HasExplicitFloat(caster, casterAllTrue)) {
                wheelCollider.caster = caster;
            }

            // Grip multiplier - check axle-specific first, then base config (V2.2+ only)
            float grip = axleConfig?.grip ?? config.grip;
            float? gripAllTrue = axleAllTrue?.grip ?? configAllTrue?.grip;
#if RCCP_V2_2_OR_NEWER
            if (IsRestoreMode || HasExplicitFloat(grip, gripAllTrue)) {
                wheelCollider.grip = Mathf.Clamp(grip > 0 ? grip : 1f, 0f, 2f);
            }
#endif

            EditorUtility.SetDirty(wc);
            EditorUtility.SetDirty(wheelCollider);
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Wheel geometry updated: width={config.wheelWidth}, camber={config.camber}, caster={config.caster}, grip={config.grip}");
    }

    #endregion

    #region Suspension Settings

    private static void ApplySuspensionSettingsPartial(RCCP_CarController carController, RCCP_AIConfig.SuspensionConfig config,
        RCCP_AIConfig.SuspensionConfig frontConfig = null, RCCP_AIConfig.SuspensionConfig rearConfig = null) {
        RCCP_WheelCollider[] wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);
        if (wheelColliders.Length == 0) return;

        // Get defaults for fallback values
        var defaults = RCCP_AIComponentDefaults.Instance?.wheelCollider;

        // For per-axle restore, find front/rear axles
        RCCP_Axle frontAxle = null, rearAxle = null;
        bool hasPerAxle = IsRestoreMode && (frontConfig != null || rearConfig != null);
        if (hasPerAxle) {
            FindFrontRearAxles(carController, out frontAxle, out rearAxle);
        }

        foreach (var wheelCollider in wheelColliders) {
            WheelCollider wc = wheelCollider.GetComponent<WheelCollider>();
            if (wc == null) continue;

            Undo.RecordObject(wc, "RCCP AI Suspension");

            JointSpring spring = wc.suspensionSpring;

            if (IsRestoreMode) {
                // Determine which config to use based on which axle this wheel belongs to
                RCCP_AIConfig.SuspensionConfig effectiveConfig = config;
                if (hasPerAxle) {
                    RCCP_Axle ownerAxle = wheelCollider.GetComponentInParent<RCCP_Axle>();
                    if (ownerAxle == frontAxle && frontConfig != null) {
                        effectiveConfig = frontConfig;
                    } else if (ownerAxle == rearAxle && rearConfig != null) {
                        effectiveConfig = rearConfig;
                    }
                }

                // In restore mode, apply ALL values unconditionally
                wc.suspensionDistance = effectiveConfig.distance;
                spring.spring = effectiveConfig.spring;
                spring.damper = effectiveConfig.damper;
            } else {
                // Partial update mode - ONLY change values that are explicitly specified
                if (config.distance > 0) wc.suspensionDistance = config.distance;
                if (config.spring > 0) spring.spring = config.spring;
                if (config.damper > 0) spring.damper = config.damper;
            }

            wc.suspensionSpring = spring;
            EditorUtility.SetDirty(wc);
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Suspension updated: distance={config.distance}, spring={config.spring}, damper={config.damper}");
    }

    #endregion

    #region Wheel Friction Settings

    private static void ApplyWheelFrictionPartial(RCCP_CarController carController, RCCP_AIConfig.WheelFrictionConfig config,
        RCCP_AIConfig.WheelFrictionConfig frontConfig = null, RCCP_AIConfig.WheelFrictionConfig rearConfig = null) {
        RCCP_WheelCollider[] wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);
        if (wheelColliders.Length == 0) return;

        // For per-axle restore, find front/rear axles
        RCCP_Axle frontAxle = null, rearAxle = null;
        bool hasPerAxle = IsRestoreMode && (frontConfig != null || rearConfig != null);
        if (hasPerAxle) {
            FindFrontRearAxles(carController, out frontAxle, out rearAxle);
        }

        // Get friction from type preset or custom values
        WheelFrictionCurve? forwardFriction = null;
        WheelFrictionCurve? sidewaysFriction = null;

        if (!string.IsNullOrEmpty(config.type)) {
            var preset = RCCP_AIConfig.FrictionPresets.GetPreset(config.type);
            forwardFriction = preset.forward.ToWheelFrictionCurve();
            sidewaysFriction = preset.sideways.ToWheelFrictionCurve();
        }

        // Custom friction curves override presets
        if (config.forward != null) forwardFriction = config.forward.ToWheelFrictionCurve();
        if (config.sideways != null) sidewaysFriction = config.sideways.ToWheelFrictionCurve();

        foreach (var wheelCollider in wheelColliders) {
            WheelCollider wc = wheelCollider.GetComponent<WheelCollider>();
            if (wc == null) continue;

            Undo.RecordObject(wc, "RCCP AI Friction");

            // In per-axle restore mode, use axle-specific friction if available
            if (hasPerAxle) {
                RCCP_Axle ownerAxle = wheelCollider.GetComponentInParent<RCCP_Axle>();
                RCCP_AIConfig.WheelFrictionConfig axleFriction = null;

                if (ownerAxle == frontAxle && frontConfig != null) {
                    axleFriction = frontConfig;
                } else if (ownerAxle == rearAxle && rearConfig != null) {
                    axleFriction = rearConfig;
                }

                if (axleFriction != null) {
                    if (axleFriction.forward != null) wc.forwardFriction = axleFriction.forward.ToWheelFrictionCurve();
                    if (axleFriction.sideways != null) wc.sidewaysFriction = axleFriction.sideways.ToWheelFrictionCurve();
                    EditorUtility.SetDirty(wc);
                    continue;
                }
            }

            // Global friction (non-restore or fallback)
            if (forwardFriction.HasValue) wc.forwardFriction = forwardFriction.Value;
            if (sidewaysFriction.HasValue) wc.sidewaysFriction = sidewaysFriction.Value;

            EditorUtility.SetDirty(wc);
        }

        if (VerboseLogging) Debug.Log($"[RCCP AI] Wheel friction updated: type={config.type}");
    }

    #endregion

    #region Wheel Alignment and Friction (Wheels Panel)

    /// <summary>
    /// Applies wheel alignment and friction settings from the Wheels panel.
    /// This handles per-wheel settings including friction curves, camber, caster, and grip.
    /// </summary>
    /// <param name="carController">The vehicle to configure</param>
    /// <param name="config">Wheel configuration from AI response</param>
    /// <returns>Number of wheels modified</returns>
    public static int ApplyWheelAlignmentAndFriction(
        RCCP_CarController carController,
        RCCP_AIConfig.WheelConfig config) {

        if (carController == null) {
            Debug.LogError("[RCCP AI] Cannot apply wheel settings: CarController is null");
            return 0;
        }

        if (config == null) {
            if (VerboseLogging) Debug.Log("[RCCP AI] No wheel configuration to apply");
            return 0;
        }

        // Get axles directly (Editor-safe, doesn't rely on runtime property)
        var axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        if (axles == null || axles.Length == 0) {
            Debug.LogError("[RCCP AI] Vehicle has no axles configured");
            return 0;
        }

        int wheelsModified = 0;

        // Determine front/rear axles by comparing wheel collider Z positions
        FindFrontRearAxles(carController, out RCCP_Axle frontAxle, out RCCP_Axle rearAxle);

        // Process each axle
        foreach (var axle in axles) {
            if (axle == null) continue;

            // Determine if this is front or rear axle by Z position comparison
            bool isFrontAxle = (axle == frontAxle);
            bool isRearAxle = (axle == rearAxle);

            // Get the axle-specific config if available
            RCCP_AIConfig.AxleWheelConfig axleConfig = null;
            if (isFrontAxle && config.front != null)
                axleConfig = config.front;
            else if (isRearAxle && config.rear != null)
                axleConfig = config.rear;

            // Process left wheel
            if (axle.leftWheelCollider != null) {
                ApplyWheelAlignmentSettings(axle.leftWheelCollider, config, axleConfig);
                wheelsModified++;
            }

            // Process right wheel
            if (axle.rightWheelCollider != null) {
                ApplyWheelAlignmentSettings(axle.rightWheelCollider, config, axleConfig);
                wheelsModified++;
            }
        }

        if (VerboseLogging && wheelsModified > 0)
            Debug.Log($"[RCCP AI] Wheel alignment/friction applied to {wheelsModified} wheels");

        return wheelsModified;
    }

    /// <summary>
    /// Applies wheel alignment settings from config to a single RCCP_WheelCollider.
    /// </summary>
    private static void ApplyWheelAlignmentSettings(RCCP_WheelCollider wheel, RCCP_AIConfig.WheelConfig config, RCCP_AIConfig.AxleWheelConfig axleOverride) {
        if (wheel == null) return;

        Undo.RecordObject(wheel, "RCCP AI Wheel Settings");

        // Get the Unity WheelCollider
        WheelCollider wc = wheel.WheelCollider;
        if (wc != null) {
            Undo.RecordObject(wc, "RCCP AI WheelCollider Settings");

            // Note: Suspension settings (spring, damper, distance) are handled by the Customization panel
            // Note: Wheel radius is NOT set here - RCCP calculates it automatically from wheel mesh

            // Apply forward friction
            RCCP_AIConfig.FrictionCurveConfig forwardFriction = axleOverride?.forwardFriction ?? config.forwardFriction;
            if (forwardFriction != null && forwardFriction.HasValues) {
                wc.forwardFriction = forwardFriction.ToWheelFrictionCurve();
            }

            // Apply sideways friction
            RCCP_AIConfig.FrictionCurveConfig sidewaysFriction = axleOverride?.sidewaysFriction ?? config.sidewaysFriction;
            if (sidewaysFriction != null && sidewaysFriction.HasValues) {
                wc.sidewaysFriction = sidewaysFriction.ToWheelFrictionCurve();
            }

            EditorUtility.SetDirty(wc);
        }

        // Apply wheel width (RCCP_WheelCollider property)
        float wheelWidth = axleOverride?.wheelWidth > 0
            ? axleOverride.wheelWidth
            : config.wheelWidth;
        if (wheelWidth > 0)
            wheel.width = Mathf.Clamp(wheelWidth, 0.1f, 0.5f);

        // Apply alignment settings to RCCP_WheelCollider (camber and caster)
        // Axle override takes priority over global config
        // Use Mathf.Approximately to detect if value was set (non-zero)
        float camberValue = (axleOverride != null && !Mathf.Approximately(axleOverride.camber, 0f))
            ? axleOverride.camber
            : config.camber;
        float casterValue = (axleOverride != null && !Mathf.Approximately(axleOverride.caster, 0f))
            ? axleOverride.caster
            : config.caster;

        // Apply if value is non-zero (0 means "don't change" since JsonUtility defaults to 0)
        // For truly neutral alignment, use 0.001
        if (!Mathf.Approximately(camberValue, 0f))
            wheel.camber = Mathf.Clamp(camberValue, -8f, 8f);

        if (!Mathf.Approximately(casterValue, 0f))
            wheel.caster = Mathf.Clamp(casterValue, -8f, 8f);

        // Grip multiplier (0-2, default 1.0 means no change) - V2.2+ only
        float gripValue = axleOverride?.grip > 0 ? axleOverride.grip : config.grip;
#if RCCP_V2_2_OR_NEWER
        if (gripValue > 0)
            wheel.grip = Mathf.Clamp(gripValue, 0f, 2f);
#endif

        EditorUtility.SetDirty(wheel);
    }

    #endregion

    #region Behavior Conflict Detection

    /// <summary>
    /// Result of checking if a behavior preset will override wheel friction at runtime.
    /// </summary>
    public class BehaviorConflictInfo {
        public bool hasConflict;
        public bool isGlobalOverride;
        public bool isCustomVehicleBehavior;
        public string activeBehaviorName;
        public int activeBehaviorIndex;
        public RCCP_Settings.BehaviorType activeBehavior;

        public string GetDescription() {
            if (!hasConflict) return "No behavior conflict";
            if (isCustomVehicleBehavior)
                return $"Vehicle uses custom behavior preset: {activeBehaviorName}";
            if (isGlobalOverride)
                return $"Global behavior override active: {activeBehaviorName}";
            return "Unknown conflict";
        }
    }

    /// <summary>
    /// Checks if wheel friction will be overridden by a behavior preset at runtime.
    /// Call this before applying friction curves to warn the user about potential conflicts.
    /// </summary>
    /// <param name="carController">The vehicle to check</param>
    /// <returns>Conflict information with details about the active behavior</returns>
    public static BehaviorConflictInfo CheckBehaviorFrictionConflict(RCCP_CarController carController) {
        var result = new BehaviorConflictInfo { hasConflict = false };

        if (carController == null) return result;

        // Check 1: If ineffectiveBehavior is true, vehicle ignores all behaviors - no conflict
        if (carController.ineffectiveBehavior) {
            return result;
        }

        var rccp_settings = RCCP_Settings.Instance;
        if (rccp_settings == null || rccp_settings.behaviorTypes == null || rccp_settings.behaviorTypes.Length == 0) {
            return result;
        }

        // Check 2: Per-vehicle custom behavior takes priority (V2.2+ only)
#if RCCP_V2_2_OR_NEWER
        if (carController.useCustomBehavior && carController.customBehaviorIndex >= 0) {
            if (carController.customBehaviorIndex < rccp_settings.behaviorTypes.Length) {
                result.hasConflict = true;
                result.isCustomVehicleBehavior = true;
                result.activeBehaviorIndex = carController.customBehaviorIndex;
                result.activeBehavior = rccp_settings.behaviorTypes[carController.customBehaviorIndex];
                result.activeBehaviorName = result.activeBehavior?.behaviorName ?? $"Behavior {carController.customBehaviorIndex}";
                return result;
            }
        }
#endif

        // Check 3: Global behavior override
        if (rccp_settings.overrideBehavior) {
            if (rccp_settings.behaviorSelectedIndex >= 0 &&
                rccp_settings.behaviorSelectedIndex < rccp_settings.behaviorTypes.Length) {
                result.hasConflict = true;
                result.isGlobalOverride = true;
                result.activeBehaviorIndex = rccp_settings.behaviorSelectedIndex;
                result.activeBehavior = rccp_settings.behaviorTypes[rccp_settings.behaviorSelectedIndex];
                result.activeBehaviorName = result.activeBehavior?.behaviorName ?? $"Behavior {rccp_settings.behaviorSelectedIndex}";
                return result;
            }
        }

        return result; // No conflict
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
