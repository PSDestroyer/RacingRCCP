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

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Partial class containing state capture, history logging, and restore methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region History Logging

    /// <summary>
    /// Capture current vehicle state as a JSON config for restore functionality
    /// </summary>
    public static string CaptureVehicleStateAsJson(RCCP_CarController carController) {
        if (carController == null) return "";

        var config = new RCCP_AIConfig.VehicleSetupConfig();

        var rb = carController.GetComponent<Rigidbody>();
        var engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        var clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);
        var gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        var stability = carController.GetComponentInChildren<RCCP_Stability>(true);
        var diffs = carController.GetComponentsInChildren<RCCP_Differential>(true);
        var axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        var wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);
        var aero = carController.GetComponentInChildren<RCCP_AeroDynamics>(true);
        var otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Vehicle config
        if (rb != null) {
            // Get COM from RCCP_AeroDynamics child object (preferred) or fallback to rigidbody
            Vector3 comOffset = rb.centerOfMass;
            if (aero != null && aero.COM != null) {
                comOffset = aero.COM.localPosition;
            }

            config.vehicleConfig = new RCCP_AIConfig.VehicleConfig {
                name = carController.gameObject.name,
                mass = rb.mass,
                centerOfMassOffset = new RCCP_AIConfig.Vector3Config {
                    x = comOffset.x,
                    y = comOffset.y,
                    z = comOffset.z
                }
            };
        }

        // Engine
        if (engine != null) {
            config.engine = new RCCP_AIConfig.EngineConfig {
                maximumTorqueAsNM = engine.maximumTorqueAsNM,
                minEngineRPM = engine.minEngineRPM,
                maxEngineRPM = engine.maxEngineRPM,
                maximumSpeed = engine.maximumSpeed,
                engineInertia = engine.engineInertia,
                engineFriction = engine.engineFriction,
                turboCharged = engine.turboCharged,
                maxTurboChargePsi = engine.maxTurboChargePsi,
                turboChargerCoEfficient = engine.turboChargerCoEfficient
            };
        }

        // Clutch
        if (clutch != null) {
            config.clutch = new RCCP_AIConfig.ClutchConfig {
                clutchInertia = clutch.clutchInertia,
                engageRPM = clutch.engageRPM,
                automaticClutch = clutch.automaticClutch,
                pressClutchWhileShiftingGears = clutch.pressClutchWhileShiftingGears,
                pressClutchWhileHandbraking = clutch.pressClutchWhileHandbraking
            };
        }

        // Gearbox
        if (gearbox != null) {
            config.gearbox = new RCCP_AIConfig.GearboxConfig {
                transmissionType = gearbox.transmissionType.ToString(),
                gearRatios = gearbox.gearRatios != null ? (float[])gearbox.gearRatios.Clone() : null,
                shiftingTime = gearbox.shiftingTime,
                shiftThreshold = gearbox.shiftThreshold,
                shiftUpRPM = gearbox.shiftUpRPM,
                shiftDownRPM = gearbox.shiftDownRPM
            };
        }

        // Differential (use first one)
        if (diffs != null && diffs.Length > 0) {
            var diff = diffs[0];
            config.differential = new RCCP_AIConfig.DifferentialConfig {
                type = diff.differentialType.ToString(),
                limitedSlipRatio = diff.limitedSlipRatio,
                finalDriveRatio = diff.finalDriveRatio
            };
        }

        // Axles - determine front/rear by comparing wheel Z positions
        config.axles = new RCCP_AIConfig.AxlesConfig();
        FindFrontRearAxles(carController, out RCCP_Axle frontAxleRef, out _);
        if (axles != null) {
            foreach (var axle in axles) {
                var axleConfig = new RCCP_AIConfig.AxleConfig {
                    isSteer = axle.isSteer,
                    isBrake = axle.isBrake,
                    isHandbrake = axle.isHandbrake,
                    maxSteerAngle = axle.maxSteerAngle,
                    maxBrakeTorque = axle.maxBrakeTorque,
                    maxHandbrakeTorque = axle.maxBrakeTorque * axle.handbrakeMultiplier,
                    antirollForce = axle.antirollForce,
                    steerSpeed = axle.steerSpeed,
                    powerMultiplier = axle.powerMultiplier,
                    steerMultiplier = axle.steerMultiplier,
                    brakeMultiplier = axle.brakeMultiplier,
                    handbrakeMultiplier = axle.handbrakeMultiplier
                };

                if (axle == frontAxleRef) {
                    config.axles.front = axleConfig;
                } else {
                    config.axles.rear = axleConfig;
                }
            }
        }

        // Suspension and wheel geometry (global from first wheel + per-axle for restore)
        if (wheelColliders != null && wheelColliders.Length > 0) {
            var firstWheelCollider = wheelColliders[0];
            var wc = firstWheelCollider.GetComponent<WheelCollider>();
            if (wc != null) {
                config.suspension = new RCCP_AIConfig.SuspensionConfig {
                    distance = wc.suspensionDistance,
                    spring = wc.suspensionSpring.spring,
                    damper = wc.suspensionSpring.damper
                };

                config.wheelFriction = new RCCP_AIConfig.WheelFrictionConfig {
                    forward = new RCCP_AIConfig.FrictionCurveConfig {
                        extremumSlip = wc.forwardFriction.extremumSlip,
                        extremumValue = wc.forwardFriction.extremumValue,
                        asymptoteSlip = wc.forwardFriction.asymptoteSlip,
                        asymptoteValue = wc.forwardFriction.asymptoteValue,
                        stiffness = wc.forwardFriction.stiffness
                    },
                    sideways = new RCCP_AIConfig.FrictionCurveConfig {
                        extremumSlip = wc.sidewaysFriction.extremumSlip,
                        extremumValue = wc.sidewaysFriction.extremumValue,
                        asymptoteSlip = wc.sidewaysFriction.asymptoteSlip,
                        asymptoteValue = wc.sidewaysFriction.asymptoteValue,
                        stiffness = wc.sidewaysFriction.stiffness
                    }
                };

                // Capture per-axle suspension and friction for accurate restore
                FindFrontRearAxles(carController, out RCCP_Axle frontAxleForSusp, out RCCP_Axle rearAxleForSusp);

                if (frontAxleForSusp != null && frontAxleForSusp.leftWheelCollider != null) {
                    var frontWc = frontAxleForSusp.leftWheelCollider.GetComponent<WheelCollider>();
                    if (frontWc != null) {
                        config.frontSuspension = new RCCP_AIConfig.SuspensionConfig {
                            distance = frontWc.suspensionDistance,
                            spring = frontWc.suspensionSpring.spring,
                            damper = frontWc.suspensionSpring.damper
                        };
                        config.frontWheelFriction = new RCCP_AIConfig.WheelFrictionConfig {
                            forward = new RCCP_AIConfig.FrictionCurveConfig {
                                extremumSlip = frontWc.forwardFriction.extremumSlip,
                                extremumValue = frontWc.forwardFriction.extremumValue,
                                asymptoteSlip = frontWc.forwardFriction.asymptoteSlip,
                                asymptoteValue = frontWc.forwardFriction.asymptoteValue,
                                stiffness = frontWc.forwardFriction.stiffness
                            },
                            sideways = new RCCP_AIConfig.FrictionCurveConfig {
                                extremumSlip = frontWc.sidewaysFriction.extremumSlip,
                                extremumValue = frontWc.sidewaysFriction.extremumValue,
                                asymptoteSlip = frontWc.sidewaysFriction.asymptoteSlip,
                                asymptoteValue = frontWc.sidewaysFriction.asymptoteValue,
                                stiffness = frontWc.sidewaysFriction.stiffness
                            }
                        };
                    }
                }

                if (rearAxleForSusp != null && rearAxleForSusp.leftWheelCollider != null) {
                    var rearWc = rearAxleForSusp.leftWheelCollider.GetComponent<WheelCollider>();
                    if (rearWc != null) {
                        config.rearSuspension = new RCCP_AIConfig.SuspensionConfig {
                            distance = rearWc.suspensionDistance,
                            spring = rearWc.suspensionSpring.spring,
                            damper = rearWc.suspensionSpring.damper
                        };
                        config.rearWheelFriction = new RCCP_AIConfig.WheelFrictionConfig {
                            forward = new RCCP_AIConfig.FrictionCurveConfig {
                                extremumSlip = rearWc.forwardFriction.extremumSlip,
                                extremumValue = rearWc.forwardFriction.extremumValue,
                                asymptoteSlip = rearWc.forwardFriction.asymptoteSlip,
                                asymptoteValue = rearWc.forwardFriction.asymptoteValue,
                                stiffness = rearWc.forwardFriction.stiffness
                            },
                            sideways = new RCCP_AIConfig.FrictionCurveConfig {
                                extremumSlip = rearWc.sidewaysFriction.extremumSlip,
                                extremumValue = rearWc.sidewaysFriction.extremumValue,
                                asymptoteSlip = rearWc.sidewaysFriction.asymptoteSlip,
                                asymptoteValue = rearWc.sidewaysFriction.asymptoteValue,
                                stiffness = rearWc.sidewaysFriction.stiffness
                            }
                        };
                    }
                }

                // Wheel geometry (radius, width, camber, caster)
                config.wheels = new RCCP_AIConfig.WheelConfig {
                    wheelRadius = wc.radius,
                    wheelWidth = firstWheelCollider.width,
                    camber = firstWheelCollider.camber,
                    caster = firstWheelCollider.caster
                };

                // Capture per-axle wheel geometry if axles are available
                if (axles != null && axles.Length > 0) {
                    FindFrontRearAxles(carController, out RCCP_Axle frontAxleForWheels, out RCCP_Axle rearAxleForWheels);

                    // Front axle wheels (suspension settings are in SuspensionConfig, not here)
                    if (frontAxleForWheels != null && frontAxleForWheels.leftWheelCollider != null) {
                        var frontWc = frontAxleForWheels.leftWheelCollider.GetComponent<WheelCollider>();
                        if (frontWc != null) {
                            config.wheels.front = new RCCP_AIConfig.AxleWheelConfig {
                                wheelRadius = frontWc.radius,
                                wheelWidth = frontAxleForWheels.leftWheelCollider.width,
                                camber = frontAxleForWheels.leftWheelCollider.camber,
                                caster = frontAxleForWheels.leftWheelCollider.caster
                            };
                        }
                    }

                    // Rear axle wheels (suspension settings are in SuspensionConfig, not here)
                    if (rearAxleForWheels != null && rearAxleForWheels.leftWheelCollider != null) {
                        var rearWc = rearAxleForWheels.leftWheelCollider.GetComponent<WheelCollider>();
                        if (rearWc != null) {
                            config.wheels.rear = new RCCP_AIConfig.AxleWheelConfig {
                                wheelRadius = rearWc.radius,
                                wheelWidth = rearAxleForWheels.leftWheelCollider.width,
                                camber = rearAxleForWheels.leftWheelCollider.camber,
                                caster = rearAxleForWheels.leftWheelCollider.caster
                            };
                        }
                    }
                }
            }
        }

        // Stability
        if (stability != null) {
            config.stability = new RCCP_AIConfig.StabilityConfig {
                ABS = stability.ABS,
                ESP = stability.ESP,
                TCS = stability.TCS,
                steeringHelper = stability.steeringHelper,
                tractionHelper = stability.tractionHelper,
                angularDragHelper = stability.angularDragHelper,
                steerHelperStrength = stability.steerHelperStrength,
                tractionHelperStrength = stability.tractionHelperStrength,
                angularDragHelperStrength = stability.angularDragHelperStrength
            };
        }

        // Aerodynamics
        if (aero != null) {
            config.aeroDynamics = new RCCP_AIConfig.AeroDynamicsConfig {
                downForce = aero.downForce
            };
        }

        // Other Addons - use GetComponentInChildren instead of runtime properties
        // Always capture addon state (even as disabled stubs) so restore can properly disable
        // components that were added after this snapshot was taken
        {
            var nos = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_Nos>(true) : null;
            if (nos != null) {
                config.nos = new RCCP_AIConfig.NosConfig {
                    enabled = nos.enabled,
                    torqueMultiplier = nos.torqueMultiplier,
                    durationTime = nos.durationTime,
                    regenerateTime = nos.regenerateTime,
                    regenerateRate = nos.regenerateRate
                };
            } else {
                // Component absent - create disabled stub so restore can disable it if AI adds it later
                config.nos = new RCCP_AIConfig.NosConfig { enabled = false };
            }

            var fuel = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_FuelTank>(true) : null;
            if (fuel != null) {
                config.fuelTank = new RCCP_AIConfig.FuelTankConfig {
                    enabled = fuel.enabled,
                    fuelTankCapacity = fuel.fuelTankCapacity,
                    fuelTankFillAmount = fuel.fuelTankFillAmount,
                    stopEngineWhenEmpty = fuel.stopEngine,
                    baseLitersPerHour = fuel.baseLitersPerHour,
                    maxLitersPerHour = fuel.maxLitersPerHour
                };
            } else {
                config.fuelTank = new RCCP_AIConfig.FuelTankConfig { enabled = false };
            }

            var limiter = otherAddons != null ? otherAddons.GetComponentInChildren<RCCP_Limiter>(true) : null;
            if (limiter != null) {
                config.limiter = new RCCP_AIConfig.LimiterConfig {
                    enabled = limiter.enabled,
                    limitSpeedAtGear = limiter.limitSpeedAtGear != null ? (float[])limiter.limitSpeedAtGear.Clone() : null,
                    applyDownhillForce = limiter.applyDownhillForce,
                    downhillForceStrength = limiter.downhillForceStrength
                };
            } else {
                config.limiter = new RCCP_AIConfig.LimiterConfig { enabled = false };
            }
        }

        // Input
        var input = carController.GetComponentInChildren<RCCP_Input>(true);
        if (input != null) {
            config.input = new RCCP_AIConfig.InputConfig {
                counterSteerFactor = input.counterSteerFactor,
                counterSteering = input.counterSteering,
                steeringLimiter = input.steeringLimiter,
                autoReverse = input.autoReverse,
                steeringDeadzone = input.steeringDeadzone
            };
        }

        // Audio
        var audio = carController.GetComponentInChildren<RCCP_Audio>(true);
        if (audio != null && audio.engineSounds != null && audio.engineSounds.Length > 0) {
            var engineSoundConfigs = new System.Collections.Generic.List<RCCP_AIConfig.EngineSoundConfig>();
            for (int i = 0; i < audio.engineSounds.Length; i++) {
                var layer = audio.engineSounds[i];
                if (layer != null) {
                    // EngineSound has audioSourceOn and audioSourceOff, check On for enabled state
                    bool isEnabled = layer.audioSourceOn != null && layer.audioSourceOn.enabled;
                    engineSoundConfigs.Add(new RCCP_AIConfig.EngineSoundConfig {
                        layerIndex = i,
                        enabled = isEnabled ? 1 : 0,
                        minRPM = layer.minRPM,
                        maxRPM = layer.maxRPM,
                        minPitch = layer.minPitch,
                        maxPitch = layer.maxPitch,
                        maxVolume = layer.maxVolume,
                        minDistance = layer.minDistance,
                        maxDistance = layer.maxDistance
                    });
                }
            }
            if (engineSoundConfigs.Count > 0) {
                config.audio = new RCCP_AIConfig.AudioConfig {
                    engineSounds = engineSoundConfigs.ToArray()
                };
            }
        }

        // Lights
        var lightsManager = carController.GetComponentInChildren<RCCP_Lights>(true);
        if (lightsManager != null && lightsManager.lights != null && lightsManager.lights.Count > 0) {
            var lightConfigs = new System.Collections.Generic.List<RCCP_AIConfig.LightConfig>();
            foreach (var light in lightsManager.lights) {
                if (light != null) {
                    var unityLight = light.GetComponent<Light>();
                    var lightConfig = new RCCP_AIConfig.LightConfig {
                        lightType = light.lightType.ToString(),
                        intensity = light.intensity,
                        smoothness = light.smoothness,
                        range = unityLight != null ? unityLight.range : 10f,
                        spotAngle = unityLight != null ? unityLight.spotAngle : 80f,
                        flareBrightness = light.flareBrightness,
                        useLensFlares = light.useLensFlares ? 2 : 1,
                        isBreakable = light.isBreakable ? 2 : 1,
                        strength = light.strength,
                        breakPoint = light.breakPoint
                    };
                    if (unityLight != null) {
                        lightConfig.lightColor = new RCCP_AIConfig.ColorConfig {
                            r = unityLight.color.r,
                            g = unityLight.color.g,
                            b = unityLight.color.b,
                            a = unityLight.color.a
                        };
                    }
                    lightConfigs.Add(lightConfig);
                }
            }
            if (lightConfigs.Count > 0) {
                config.lights = new RCCP_AIConfig.LightsConfig {
                    lights = lightConfigs.ToArray()
                };
            }
        }

        // Damage
        var damage = carController.GetComponentInChildren<RCCP_Damage>(true);
        if (damage != null) {
            config.damage = new RCCP_AIConfig.DamageConfig {
                meshDeformation = damage.meshDeformation,
                maximumDamage = damage.maximumDamage,
                deformationRadius = damage.deformationRadius,
                deformationMultiplier = damage.deformationMultiplier,
                automaticInstallation = damage.automaticInstallation,
                wheelDamage = damage.wheelDamage,
                wheelDamageRadius = damage.wheelDamageRadius,
                wheelDamageMultiplier = damage.wheelDamageMultiplier,
                wheelDetachment = damage.wheelDetachment,
                lightDamage = damage.lightDamage,
                lightDamageRadius = damage.lightDamageRadius,
                lightDamageMultiplier = damage.lightDamageMultiplier,
                partDamage = damage.partDamage,
                partDamageRadius = damage.partDamageRadius,
                partDamageMultiplier = damage.partDamageMultiplier
            };
        }

        // Visual Effects - LOD
        var lod = carController.GetComponentInChildren<RCCP_Lod>(true);
        var wheelBlur = carController.GetComponentInChildren<RCCP_WheelBlur>(true);
        var exhausts = carController.GetComponentsInChildren<RCCP_Exhaust>(true);
        var bodyTilt = carController.GetComponentInChildren<RCCP_BodyTilt>(true);
        var particles = carController.GetComponentInChildren<RCCP_Particles>(true);

        if (lod != null || wheelBlur != null || (exhausts != null && exhausts.Length > 0) || bodyTilt != null || particles != null) {
            config.visualEffects = new RCCP_AIConfig.VisualEffectsConfig();

            if (lod != null) {
                config.visualEffects.lod = new RCCP_AIConfig.LodConfig {
                    lodFactor = lod.lodFactor,
                    forceToFirstLevel = lod.forceToFirstLevel ? 1 : 0,
                    forceToLatestLevel = lod.forceToLatestLevel ? 1 : 0
                };
            }

            if (wheelBlur != null) {
                config.visualEffects.wheelBlur = new RCCP_AIConfig.WheelBlurConfig {
                    offset = new RCCP_AIConfig.Vector3Config {
                        x = wheelBlur.offset.x,
                        y = wheelBlur.offset.y,
                        z = wheelBlur.offset.z
                    },
                    scale = wheelBlur.scale,
                    rotationSpeed = wheelBlur.rotationSpeed,
                    smoothness = wheelBlur.smoothness
                };
            }

            if (exhausts != null && exhausts.Length > 0) {
                var exhaustConfigs = new System.Collections.Generic.List<RCCP_AIConfig.ExhaustConfig>();
                for (int i = 0; i < exhausts.Length; i++) {
                    var exhaust = exhausts[i];
                    if (exhaust != null) {
                        exhaustConfigs.Add(new RCCP_AIConfig.ExhaustConfig {
                            exhaustIndex = i,
                            flameOnCutOff = exhaust.flameOnCutOff ? 1 : 0,
                            flareBrightness = exhaust.flareBrightness,
                            flameColor = new RCCP_AIConfig.ColorConfig {
                                r = exhaust.flameColor.r,
                                g = exhaust.flameColor.g,
                                b = exhaust.flameColor.b,
                                a = exhaust.flameColor.a
                            },
                            boostFlameColor = new RCCP_AIConfig.ColorConfig {
                                r = exhaust.boostFlameColor.r,
                                g = exhaust.boostFlameColor.g,
                                b = exhaust.boostFlameColor.b,
                                a = exhaust.boostFlameColor.a
                            },
                            minEmission = exhaust.minEmission,
                            maxEmission = exhaust.maxEmission,
                            minSize = exhaust.minSize,
                            maxSize = exhaust.maxSize,
                            minSpeed = exhaust.minSpeed,
                            maxSpeed = exhaust.maxSpeed
                        });
                    }
                }
                if (exhaustConfigs.Count > 0) {
                    config.visualEffects.exhausts = new RCCP_AIConfig.ExhaustsConfig {
                        exhausts = exhaustConfigs.ToArray()
                    };
                }
            }

            if (bodyTilt != null) {
                config.visualEffects.bodyTilt = new RCCP_AIConfig.BodyTiltConfig {
                    enabled = bodyTilt.enabled ? 1 : 0,
                    maxTiltAngle = bodyTilt.maxTiltAngle,
                    forwardTiltMultiplier = bodyTilt.forwardTiltMultiplier,
                    sidewaysTiltMultiplier = bodyTilt.sidewaysTiltMultiplier,
                    tiltSmoothSpeed = bodyTilt.tiltSmoothSpeed
                };
            }

            if (particles != null) {
                config.visualEffects.particles = new RCCP_AIConfig.ParticlesConfig {
                    collisionFilterMask = particles.collisionFilter.value
                };
            }
        }

        return JsonUtility.ToJson(config, true);
    }

    /// <summary>
    /// Capture current vehicle state as a formatted string for history
    /// </summary>
    public static string CaptureVehicleState(RCCP_CarController carController) {
        if (carController == null) return "";

        var sb = new System.Text.StringBuilder();

        var rb = carController.GetComponent<Rigidbody>();
        var engine = carController.GetComponentInChildren<RCCP_Engine>(true);
        var clutch = carController.GetComponentInChildren<RCCP_Clutch>(true);
        var gearbox = carController.GetComponentInChildren<RCCP_Gearbox>(true);
        var stability = carController.GetComponentInChildren<RCCP_Stability>(true);
        var diffs = carController.GetComponentsInChildren<RCCP_Differential>(true);
        var axles = carController.GetComponentsInChildren<RCCP_Axle>(true);
        var wheelColliders = carController.GetComponentsInChildren<RCCP_WheelCollider>(true);
        var aero = carController.GetComponentInChildren<RCCP_AeroDynamics>(true);
        var otherAddons = carController.GetComponentInChildren<RCCP_OtherAddons>(true);

        // Vehicle basics
        if (rb != null) {
            sb.AppendLine($"Mass: {rb.mass}kg");
        }

        // Engine
        if (engine != null) {
            sb.AppendLine($"Engine: {engine.maximumTorqueAsNM}Nm, {engine.minEngineRPM}-{engine.maxEngineRPM}RPM, {engine.maximumSpeed}km/h");
            if (engine.turboCharged) {
                sb.AppendLine($"  Turbo: {engine.maxTurboChargePsi}psi");
            }
        }

        // Clutch
        if (clutch != null) {
            sb.AppendLine($"Clutch: engageRPM={clutch.engageRPM}, inertia={clutch.clutchInertia}, auto={clutch.automaticClutch}");
        }

        // Gearbox
        if (gearbox != null) {
            string ratios = gearbox.gearRatios != null ? string.Join(", ", gearbox.gearRatios) : "none";
            sb.AppendLine($"Gearbox: {gearbox.transmissionType}, {gearbox.gearRatios?.Length} gears, shift={gearbox.shiftingTime}s");
            sb.AppendLine($"  Ratios: [{ratios}]");
            sb.AppendLine($"  ShiftRPM: up={gearbox.shiftUpRPM}, down={gearbox.shiftDownRPM}");
        }

        // Differential
        if (diffs != null && diffs.Length > 0) {
            foreach (var diff in diffs) {
                string axleName = diff.connectedAxle != null ? diff.connectedAxle.gameObject.name : "unknown";
                sb.AppendLine($"Diff ({axleName}): {diff.differentialType}, slip={diff.limitedSlipRatio}, final={diff.finalDriveRatio}");
            }
        }

        // Axles
        if (axles != null && axles.Length > 0) {
            foreach (var axle in axles) {
                sb.AppendLine($"Axle ({axle.gameObject.name}): steer={axle.isSteer}, power={axle.isPower}, brake={axle.isBrake}");
                sb.AppendLine($"  steerAngle={axle.maxSteerAngle}, brakeTorque={axle.maxBrakeTorque}, antiroll={axle.antirollForce}");
            }
        }

        // Stability
        if (stability != null) {
            sb.AppendLine($"Stability: ABS={stability.ABS}, ESP={stability.ESP}, TCS={stability.TCS}");
            sb.AppendLine($"  steerHelper={stability.steeringHelper} ({stability.steerHelperStrength}), tractionHelper={stability.tractionHelper} ({stability.tractionHelperStrength})");
        }

        // Suspension (from first wheel)
        if (wheelColliders != null && wheelColliders.Length > 0) {
            var wc = wheelColliders[0].GetComponent<WheelCollider>();
            if (wc != null) {
                sb.AppendLine($"Suspension: dist={wc.suspensionDistance}m, spring={wc.suspensionSpring.spring}, damper={wc.suspensionSpring.damper}");
            }
        }

        // Wheel settings (camber, caster, grip, width) - group by axle
        if (axles != null && axles.Length > 0) {
            for (int i = 0; i < axles.Length; i++) {
                var axle = axles[i];
                string axleName = i == 0 ? "Front" : (i == axles.Length - 1 ? "Rear" : $"Axle{i}");
                var wheel = axle.leftWheelCollider ?? axle.rightWheelCollider;
                if (wheel != null) {
#if RCCP_V2_2_OR_NEWER
                    sb.AppendLine($"Wheels ({axleName}): camber={wheel.camber:F1}°, caster={wheel.caster:F1}°, grip={wheel.grip:F2}, width={wheel.width:F3}m");
#else
                    sb.AppendLine($"Wheels ({axleName}): camber={wheel.camber:F1}°, caster={wheel.caster:F1}°, width={wheel.width:F3}m");
#endif
                }
            }
        }

        // Aerodynamics
        if (aero != null) {
            sb.AppendLine($"Aero: downForce={aero.downForce}");
        }

        // NOS - use GetComponentInChildren instead of runtime property
        var nos = otherAddons?.GetComponentInChildren<RCCP_Nos>(true);
        if (nos != null) {
            sb.AppendLine($"NOS: enabled={nos.enabled}, multiplier={nos.torqueMultiplier}, duration={nos.durationTime}s");
        }

        // Fuel Tank - use GetComponentInChildren instead of runtime property
        var fuel = otherAddons?.GetComponentInChildren<RCCP_FuelTank>(true);
        if (fuel != null) {
            sb.AppendLine($"Fuel: enabled={fuel.enabled}, capacity={fuel.fuelTankCapacity}L");
        }

        // Limiter - use GetComponentInChildren instead of runtime property
        var limiter = otherAddons?.GetComponentInChildren<RCCP_Limiter>(true);
        if (limiter != null) {
            string limits = limiter.limitSpeedAtGear != null ? string.Join(", ", limiter.limitSpeedAtGear) : "none";
            sb.AppendLine($"Limiter: enabled={limiter.enabled}, speeds=[{limits}]");
        }

        // Input
        var input = carController.GetComponentInChildren<RCCP_Input>(true);
        if (input != null) {
            sb.AppendLine($"Input: counterSteerFactor={input.counterSteerFactor}, counterSteering={input.counterSteering}, steeringLimiter={input.steeringLimiter}");
        }

        // Audio settings (engine sound layers)
        var audio = carController.GetComponentInChildren<RCCP_Audio>(true);
        if (audio != null && audio.engineSounds != null && audio.engineSounds.Length > 0) {
            for (int i = 0; i < audio.engineSounds.Length; i++) {
                var layer = audio.engineSounds[i];
                if (layer != null) {
                    sb.AppendLine($"Audio Layer {i}: RPM={layer.minRPM:F0}-{layer.maxRPM:F0}, pitch={layer.minPitch:F2}-{layer.maxPitch:F2}, vol={layer.maxVolume:F2}, dist={layer.minDistance:F0}-{layer.maxDistance:F0}");
                }
            }
        }

        // Lights settings
        var lightsManager = carController.GetComponentInChildren<RCCP_Lights>(true);
        if (lightsManager != null && lightsManager.lights != null && lightsManager.lights.Count > 0) {
            foreach (var light in lightsManager.lights) {
                if (light != null) {
                    var unityLight = light.GetComponent<Light>();
                    string colorStr = unityLight != null ? $"color=({unityLight.color.r:F2},{unityLight.color.g:F2},{unityLight.color.b:F2})" : "";
                    string rangeStr = unityLight != null ? $"range={unityLight.range:F0}" : "";
                    sb.AppendLine($"Light ({light.lightType}): intensity={light.intensity:F1}, {rangeStr}, {colorStr}, flares={light.useLensFlares}, breakable={light.isBreakable}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Log a history entry to the vehicle
    /// </summary>
    internal static void LogHistory(GameObject vehicle, string beforeState, string beforeStateJson, string afterState, string explanation) {
        if (vehicle == null) return;

        // Get or add history component (with undo support)
        RCCP_AIHistory history = vehicle.GetComponent<RCCP_AIHistory>();
        if (history == null) {
            history = Undo.AddComponent<RCCP_AIHistory>(vehicle);
        }

        if (history == null) {
            Debug.LogWarning("[RCCP AI] Failed to create history component");
            return;
        }

        Undo.RecordObject(history, "RCCP AI Log History");

        // Create entry from current context or with defaults
        RCCP_AIHistory.HistoryEntry entry;

        if (CurrentContext.HasValue) {
            var ctx = CurrentContext.Value;
            entry = new RCCP_AIHistory.HistoryEntry(
                ctx.panelType,
                ctx.userPrompt,
                !string.IsNullOrEmpty(explanation) ? explanation : ctx.explanation,
                ctx.appliedJson,
                beforeState,
                beforeStateJson,
                afterState
            );
        } else {
            entry = new RCCP_AIHistory.HistoryEntry(
                "Unknown",
                "",
                explanation ?? "",
                "",
                beforeState,
                beforeStateJson,
                afterState
            );
        }

        history.AddEntry(entry);
        EditorUtility.SetDirty(history);

        if (VerboseLogging) Debug.Log($"[RCCP AI] History entry logged for {vehicle.name}");
    }

    /// <summary>
    /// Restore a vehicle to a previous state from history entry
    /// </summary>
    public static void RestoreFromHistory(RCCP_CarController carController, string beforeStateJson) {
        if (carController == null || string.IsNullOrEmpty(beforeStateJson)) {
            Debug.LogError("[RCCP AI] Cannot restore: CarController or JSON is null!");
            return;
        }

        if (VerboseLogging) Debug.Log("[RCCP AI] Restoring vehicle to previous state...");

        var config = JsonUtility.FromJson<RCCP_AIConfig.VehicleSetupConfig>(beforeStateJson);
        if (config == null) {
            Debug.LogError("[RCCP AI] Cannot restore: Failed to parse JSON config!");
            return;
        }

        try {
            // Set restore mode so Partial methods apply ALL values (including false booleans and zeros)
            IsRestoreMode = true;

            // Apply ALL settings from the saved state (forceApplyAll=true bypasses HasMeaningfulValues checks)
            // skipHistory=true because the restore is logged separately by the caller
            CustomizeVehicle(carController, config, forceApplyAll: true, skipHistory: true);

            if (VerboseLogging) Debug.Log("[RCCP AI] Vehicle restored to previous state.");
        } finally {
            // Always reset restore mode
            IsRestoreMode = false;
        }
    }

    #endregion

    #region Restore Apply Methods

    // NOTE: ApplyAudioSettingsForRestore is now in RCCP_AIVehicleBuilder.Audio.cs
    // NOTE: ApplyLightsSettingsForRestore is now in RCCP_AIVehicleBuilder.Lights.cs
    // NOTE: ApplyDamageSettingsForRestore is now in RCCP_AIVehicleBuilder.Damage.cs

    /// <summary>
    /// Apply visual effects settings during restore operation.
    /// Restores LOD, wheel blur, exhausts, body tilt, and particles settings.
    /// </summary>
    private static void ApplyVisualEffectsForRestore(RCCP_CarController carController, RCCP_AIConfig.VisualEffectsConfig config) {
        if (config == null) return;

        // LOD
        if (config.lod != null) {
            var lod = carController.GetComponentInChildren<RCCP_Lod>(true);
            if (lod != null) {
                Undo.RecordObject(lod, "RCCP AI Restore LOD");
                lod.lodFactor = config.lod.lodFactor;
                if (config.lod.ShouldModifyForceFirst) {
                    lod.forceToFirstLevel = config.lod.forceToFirstLevel == 1;
                }
                if (config.lod.ShouldModifyForceLast) {
                    lod.forceToLatestLevel = config.lod.forceToLatestLevel == 1;
                }
                EditorUtility.SetDirty(lod);
            }
        }

        // Wheel Blur
        if (config.wheelBlur != null) {
            var wheelBlur = carController.GetComponentInChildren<RCCP_WheelBlur>(true);
            if (wheelBlur != null) {
                Undo.RecordObject(wheelBlur, "RCCP AI Restore WheelBlur");
                if (config.wheelBlur.offset != null) {
                    wheelBlur.offset = config.wheelBlur.offset.ToVector3();
                }
                wheelBlur.scale = config.wheelBlur.scale;
                wheelBlur.rotationSpeed = config.wheelBlur.rotationSpeed;
                wheelBlur.smoothness = config.wheelBlur.smoothness;
                EditorUtility.SetDirty(wheelBlur);
            }
        }

        // Exhausts
        if (config.exhausts != null && config.exhausts.exhausts != null) {
            var exhausts = carController.GetComponentsInChildren<RCCP_Exhaust>(true);
            if (exhausts != null && exhausts.Length > 0) {
                foreach (var exhaustConfig in config.exhausts.exhausts) {
                    if (exhaustConfig.exhaustIndex >= 0 && exhaustConfig.exhaustIndex < exhausts.Length) {
                        var exhaust = exhausts[exhaustConfig.exhaustIndex];
                        if (exhaust != null) {
                            Undo.RecordObject(exhaust, "RCCP AI Restore Exhaust");

                            if (exhaustConfig.ShouldModifyFlameOnCutOff) {
                                exhaust.flameOnCutOff = exhaustConfig.flameOnCutOff == 1;
                            }
                            exhaust.flareBrightness = exhaustConfig.flareBrightness;

                            if (exhaustConfig.flameColor != null && exhaustConfig.flameColor.IsSpecified) {
                                exhaust.flameColor = exhaustConfig.flameColor.ToColor();
                            }
                            if (exhaustConfig.boostFlameColor != null && exhaustConfig.boostFlameColor.IsSpecified) {
                                exhaust.boostFlameColor = exhaustConfig.boostFlameColor.ToColor();
                            }

                            exhaust.minEmission = exhaustConfig.minEmission;
                            exhaust.maxEmission = exhaustConfig.maxEmission;
                            exhaust.minSize = exhaustConfig.minSize;
                            exhaust.maxSize = exhaustConfig.maxSize;
                            exhaust.minSpeed = exhaustConfig.minSpeed;
                            exhaust.maxSpeed = exhaustConfig.maxSpeed;

                            EditorUtility.SetDirty(exhaust);
                        }
                    }
                }
            }
        }

        // Body Tilt
        if (config.bodyTilt != null) {
            var bodyTilt = carController.GetComponentInChildren<RCCP_BodyTilt>(true);
            if (bodyTilt != null) {
                Undo.RecordObject(bodyTilt, "RCCP AI Restore BodyTilt");
                if (config.bodyTilt.ShouldModifyEnabled) {
                    bodyTilt.enabled = config.bodyTilt.enabled == 1;
                }
                bodyTilt.maxTiltAngle = config.bodyTilt.maxTiltAngle;
                bodyTilt.forwardTiltMultiplier = config.bodyTilt.forwardTiltMultiplier;
                bodyTilt.sidewaysTiltMultiplier = config.bodyTilt.sidewaysTiltMultiplier;
                bodyTilt.tiltSmoothSpeed = config.bodyTilt.tiltSmoothSpeed;
                EditorUtility.SetDirty(bodyTilt);
            }
        }

        // Particles
        if (config.particles != null) {
            var particles = carController.GetComponentInChildren<RCCP_Particles>(true);
            if (particles != null) {
                Undo.RecordObject(particles, "RCCP AI Restore Particles");
                particles.collisionFilter = config.particles.collisionFilterMask;
                EditorUtility.SetDirty(particles);
            }
        }

        if (VerboseLogging) Debug.Log("[RCCP AI Restore] Visual effects settings restored");
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
