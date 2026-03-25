//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Partial class containing config validation helper methods.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Config Validation Helpers

    // Check if config has meaningful (non-default) values
    // JsonUtility creates default objects for all class fields, so we need to check for actual content

    public static bool HasMeaningfulValues(RCCP_AIConfig.VehicleConfig config) {
        if (config == null) return false;
        return !string.IsNullOrEmpty(config.name) || config.mass > 0 || config.centerOfMassOffset != null;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.EngineConfig config) {
        if (config == null) return false;
        return config.maximumTorqueAsNM > 0 || config.maxEngineRPM > 0 || config.maximumSpeed > 0 ||
               config.turboCharged;  // true means enable turbo
    }

    /// <summary>
    /// Overload that can detect explicit 'false' for turboCharged.
    /// </summary>
    public static bool HasMeaningfulValues(RCCP_AIConfig.EngineConfig config, RCCP_AIConfig.EngineConfig configAllTrue) {
        if (config == null) return false;
        // Numeric values or turbo enabled
        if (config.maximumTorqueAsNM > 0 || config.maxEngineRPM > 0 || config.maximumSpeed > 0 ||
            config.engineInertia > 0 || config.engineFriction > 0 || config.maxTurboChargePsi > 0 ||
            config.turboCharged)
            return true;
        // Explicit false for turboCharged
        if (configAllTrue != null && !configAllTrue.turboCharged)
            return true;
        return false;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.ClutchConfig config) {
        if (config == null) return false;
        // Clutch is meaningful if any numeric value is set OR any boolean is true
        // (JsonUtility can only reliably detect true booleans, not false)
        return config.clutchInertia > 0 || config.engageRPM > 0 ||
               config.automaticClutch || config.pressClutchWhileShiftingGears || config.pressClutchWhileHandbraking;
    }

    /// <summary>
    /// Overload that can detect explicit 'false' for clutch booleans.
    /// </summary>
    public static bool HasMeaningfulValues(RCCP_AIConfig.ClutchConfig config, RCCP_AIConfig.ClutchConfig configAllTrue) {
        if (config == null) return false;
        // Numeric values or true booleans
        if (config.clutchInertia > 0 || config.engageRPM > 0 ||
            config.automaticClutch || config.pressClutchWhileShiftingGears || config.pressClutchWhileHandbraking)
            return true;
        // Explicit false values
        if (configAllTrue != null) {
            if (!configAllTrue.automaticClutch || !configAllTrue.pressClutchWhileShiftingGears || !configAllTrue.pressClutchWhileHandbraking)
                return true;
        }
        return false;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.GearboxConfig config) {
        if (config == null) return false;
        return !string.IsNullOrEmpty(config.transmissionType) ||
               (config.gearRatios != null && config.gearRatios.Length > 0) ||
               config.shiftingTime > 0 ||
               config.shiftUpRPM > 0 ||
               config.shiftDownRPM > 0;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.DifferentialConfig config) {
        if (config == null) return false;
        return !string.IsNullOrEmpty(config.type) || config.limitedSlipRatio > 0 || config.finalDriveRatio > 0;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.AxlesConfig config) {
        if (config == null) return false;
        return HasMeaningfulValues(config.front) || HasMeaningfulValues(config.rear);
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.AxleConfig config) {
        if (config == null) return false;
        return config.isSteer || config.isBrake || config.isHandbrake ||
               config.maxSteerAngle > 0 || config.maxBrakeTorque > 0 || config.maxHandbrakeTorque > 0 ||
               config.antirollForce > 0 || config.steerSpeed > 0 ||
               config.powerMultiplier != 0 || config.steerMultiplier != 0 ||
               config.brakeMultiplier != 0 || config.handbrakeMultiplier != 0;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.SuspensionConfig config) {
        if (config == null) return false;
        return config.distance > 0 || config.spring > 0 || config.damper > 0;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.WheelFrictionConfig config) {
        if (config == null) return false;
        return !string.IsNullOrEmpty(config.type) || config.forward != null || config.sideways != null;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.StabilityConfig config) {
        if (config == null) return false;
        // Check if remove requested, any boolean is explicitly TRUE (means user wants to enable it)
        // Also check if any helper strength is set
        // Note: We can't detect "user set false" vs "default false" with JsonUtility,
        // so we only trigger on TRUE booleans or non-zero strengths
        return config.remove || config.ABS || config.ESP || config.TCS ||
               config.steeringHelper || config.tractionHelper || config.angularDragHelper ||
               config.steerHelperStrength > 0 || config.tractionHelperStrength > 0 || config.angularDragHelperStrength > 0;
    }

    /// <summary>
    /// Overload that can detect explicit 'false' boolean values using the all-true config.
    /// When JSON contains an explicit false, it overwrites the true in configAllTrue.
    /// </summary>
    public static bool HasMeaningfulValues(RCCP_AIConfig.StabilityConfig config, RCCP_AIConfig.StabilityConfig configAllTrue) {
        if (config == null) return false;
        // Check remove, strengths, or any true boolean
        if (config.remove ||
            config.steerHelperStrength > 0 || config.tractionHelperStrength > 0 || config.angularDragHelperStrength > 0 ||
            config.ABS || config.ESP || config.TCS ||
            config.steeringHelper || config.tractionHelper || config.angularDragHelper)
            return true;
        // Explicit false values (detected via all-true config)
        // If configAllTrue was set to true but is now false, the JSON contained an explicit false
        if (configAllTrue != null) {
            if (!configAllTrue.ABS || !configAllTrue.ESP || !configAllTrue.TCS ||
                !configAllTrue.steeringHelper || !configAllTrue.tractionHelper || !configAllTrue.angularDragHelper)
                return true;
        }
        return false;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.AeroDynamicsConfig config) {
        if (config == null) return false;
        return config.remove || config.downForce > 0 || config.airResistance > 0 || config.wheelResistance > 0;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.NosConfig config) {
        if (config == null) return false;
        // NOS is meaningful if enabled, remove requested, OR if any values are set
        return config.enabled || config.remove || config.torqueMultiplier > 0 || config.durationTime > 0;
    }

    /// <summary>
    /// Overload that can detect explicit 'false' for NOS enabled.
    /// </summary>
    public static bool HasMeaningfulValues(RCCP_AIConfig.NosConfig config, RCCP_AIConfig.NosConfig configAllTrue) {
        if (config == null) return false;
        // Check enabled, remove, or numeric values
        if (config.enabled || config.remove || config.torqueMultiplier > 0 || config.durationTime > 0 ||
            config.regenerateTime > 0 || config.regenerateRate > 0)
            return true;
        // Explicit false for enabled
        if (configAllTrue != null && !configAllTrue.enabled)
            return true;
        return false;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.FuelTankConfig config) {
        if (config == null) return false;
        // FuelTank is meaningful if enabled, remove requested, OR if capacity is set
        return config.enabled || config.remove || config.fuelTankCapacity > 0 || config.fuelTankFillAmount > 0 ||
               config.stopEngineWhenEmpty;
    }

    /// <summary>
    /// Overload that can detect explicit 'false' for FuelTank booleans.
    /// </summary>
    public static bool HasMeaningfulValues(RCCP_AIConfig.FuelTankConfig config, RCCP_AIConfig.FuelTankConfig configAllTrue) {
        if (config == null) return false;
        // Check enabled, remove, numeric values, or true booleans
        if (config.enabled || config.remove || config.fuelTankCapacity > 0 || config.fuelTankFillAmount > 0 ||
            config.stopEngineWhenEmpty)
            return true;
        // Explicit false values
        if (configAllTrue != null) {
            if (!configAllTrue.enabled || !configAllTrue.stopEngineWhenEmpty)
                return true;
        }
        return false;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.LimiterConfig config) {
        if (config == null) return false;
        // Limiter is meaningful if enabled, remove requested, OR if speed limits are set
        return config.enabled || config.remove || (config.limitSpeedAtGear != null && config.limitSpeedAtGear.Length > 0) ||
               config.applyDownhillForce;
    }

    /// <summary>
    /// Overload that can detect explicit 'false' for Limiter booleans.
    /// </summary>
    public static bool HasMeaningfulValues(RCCP_AIConfig.LimiterConfig config, RCCP_AIConfig.LimiterConfig configAllTrue) {
        if (config == null) return false;
        // Check enabled, remove, arrays, or true booleans
        if (config.enabled || config.remove || (config.limitSpeedAtGear != null && config.limitSpeedAtGear.Length > 0) ||
            config.applyDownhillForce)
            return true;
        // Explicit false values
        if (configAllTrue != null) {
            if (!configAllTrue.enabled || !configAllTrue.applyDownhillForce)
                return true;
        }
        return false;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.InputConfig config) {
        if (config == null) return false;
        // Input is meaningful if any float value is set OR any boolean is true
        return config.counterSteerFactor > 0 || config.steeringDeadzone > 0 ||
               config.counterSteering || config.steeringLimiter || config.autoReverse;
    }

    /// <summary>
    /// Overload that can detect explicit 'false' boolean values using the all-true config.
    /// When JSON contains an explicit false, it overwrites the true in configAllTrue.
    /// </summary>
    public static bool HasMeaningfulValues(RCCP_AIConfig.InputConfig config, RCCP_AIConfig.InputConfig configAllTrue) {
        if (config == null) return false;
        // Float values
        if (config.counterSteerFactor > 0 || config.steeringDeadzone > 0) return true;
        // Boolean true values
        if (config.counterSteering || config.steeringLimiter || config.autoReverse) return true;
        // Explicit false values (detected via all-true config)
        // If configAllTrue was set to true but is now false, the JSON contained an explicit false
        if (configAllTrue != null) {
            if (!configAllTrue.counterSteering || !configAllTrue.steeringLimiter || !configAllTrue.autoReverse)
                return true;
        }
        return false;
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.WheelConfig config) {
        if (config == null) return false;
        // WheelConfig is meaningful if any dimension, alignment, grip, friction, or per-axle config is set
        return config.wheelRadius > 0 || config.wheelWidth > 0 ||
               config.camber != 0 || config.caster != 0 ||
               (config.grip > 0 && config.grip != 1f) ||
               HasMeaningfulValues(config.forwardFriction) ||
               HasMeaningfulValues(config.sidewaysFriction) ||
               HasMeaningfulValues(config.front) ||
               HasMeaningfulValues(config.rear);
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.AxleWheelConfig config) {
        if (config == null) return false;
        return config.wheelWidth > 0 ||
               config.camber != 0 || config.caster != 0 ||
               (config.grip > 0 && config.grip != 1f) ||
               HasMeaningfulValues(config.forwardFriction) ||
               HasMeaningfulValues(config.sidewaysFriction);
    }

    public static bool HasMeaningfulValues(RCCP_AIConfig.FrictionCurveConfig config) {
        if (config == null) return false;
        // Friction is meaningful if any curve value or stiffness is explicitly set
        return config.extremumSlip > 0 || config.extremumValue > 0 ||
               config.asymptoteSlip > 0 || config.asymptoteValue > 0 ||
               config.stiffness > 0;
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
