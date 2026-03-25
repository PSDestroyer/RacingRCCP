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
using System.IO;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

/// <summary>
/// Editor utility that extracts default values from RCCP components by instantiating them
/// and reading their initial field values. Creates/updates the RCCP_AIComponentDefaults asset.
/// </summary>
public static class RCCP_AIDefaultsExtractor {

    // Use dynamic paths from RCCP_AIUtility
    private static string ASSET_PATH => RCCP_AIUtility.ComponentDefaultsAssetPath;
    private static string RESOURCES_FOLDER => RCCP_AIUtility.ResourcesPath;

    // Internal method - no menu item, call from code or setup wizard
    public static void ExtractDefaults() {
        if (!EditorUtility.DisplayDialog(
            "Extract RCCP Defaults",
            "This will create/update the RCCP_AIComponentDefaults asset by extracting default values from RCCP components.\n\n" +
            "Run this after updating RCCP to sync default values.",
            "Extract",
            "Cancel")) {
            return;
        }

        try {
            ExtractDefaultsInternal();
            EditorUtility.DisplayDialog("Success", "RCCP component defaults extracted successfully!", "OK");
        } catch (Exception e) {
            Debug.LogError($"[RCCP AI] Failed to extract defaults: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("Error", $"Failed to extract defaults: {e.Message}", "OK");
        }
    }

    private static void ExtractDefaultsInternal() {
        // Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder(RESOURCES_FOLDER)) {
            string parent = Path.GetDirectoryName(RESOURCES_FOLDER).Replace("\\", "/");
            AssetDatabase.CreateFolder(parent, "Resources");
        }

        // Load or create the asset
        RCCP_AIComponentDefaults defaults = AssetDatabase.LoadAssetAtPath<RCCP_AIComponentDefaults>(ASSET_PATH);
        if (defaults == null) {
            defaults = ScriptableObject.CreateInstance<RCCP_AIComponentDefaults>();
            AssetDatabase.CreateAsset(defaults, ASSET_PATH);
            Debug.Log($"[RCCP AI] Created new RCCP_AIComponentDefaults asset at {ASSET_PATH}");
        }

        Undo.RecordObject(defaults, "Extract RCCP Defaults");

        // Record metadata
        defaults.extractedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        defaults.rccpVersion = GetRCCPVersion();

        // Create a temporary GameObject to instantiate components
        GameObject tempGO = new GameObject("_RCCP_AI_TempExtractor");
        tempGO.hideFlags = HideFlags.HideAndDontSave;

        try {
            // Add required components for RCCP (Rigidbody is needed by some components)
            Rigidbody rb = tempGO.AddComponent<Rigidbody>();
            ExtractRigidbodyDefaults(rb, defaults.rigidbody);

            // Extract from each RCCP component - Core drivetrain
            ExtractEngineDefaults(tempGO, defaults.engine);
            ExtractGearboxDefaults(tempGO, defaults.gearbox);
            ExtractClutchDefaults(tempGO, defaults.clutch);
            ExtractDifferentialDefaults(tempGO, defaults.differential);
            ExtractAxleDefaults(tempGO, defaults.axle);
            ExtractStabilityDefaults(tempGO, defaults.stability);
            ExtractWheelColliderDefaults(tempGO, defaults.wheelCollider);
            ExtractAeroDynamicsDefaults(tempGO, defaults.aeroDynamics);
            ExtractNosDefaults(tempGO, defaults.nos);
            ExtractFuelTankDefaults(tempGO, defaults.fuelTank);
            ExtractLimiterDefaults(tempGO, defaults.limiter);
            ExtractDamageDefaults(tempGO, defaults.damage);
            ExtractInputDefaults(tempGO, defaults.input);

            // Extract from additional core components
            ExtractAudioDefaults(tempGO, defaults.audio);
            ExtractLightsDefaults(tempGO, defaults.lights);
            ExtractParticlesDefaults(tempGO, defaults.particles);
            ExtractLodDefaults(tempGO, defaults.lod);

            // Extract from addon components
            ExtractAIDefaults(tempGO, defaults.ai);
            ExtractAIDynamicObstacleAvoidanceDefaults(tempGO, defaults.aiObstacleAvoidance);
            ExtractBodyTiltDefaults(tempGO, defaults.bodyTilt);
            ExtractExhaustsDefaults(tempGO, defaults.exhausts);
            ExtractTrailerAttacherDefaults(tempGO, defaults.trailerAttacher);
            ExtractWheelBlurDefaults(tempGO, defaults.wheelBlur);
            ExtractRecorderDefaults(tempGO, defaults.recorder);
            ExtractDetachablePartDefaults(tempGO, defaults.detachablePart);
            ExtractVisualDashboardDefaults(tempGO, defaults.visualDashboard);
            ExtractExteriorCamerasDefaults(tempGO, defaults.exteriorCameras);

            // Extract from upgrade components
            ExtractUpgradeEngineDefaults(defaults.upgradeEngine);
            ExtractUpgradeBrakeDefaults(defaults.upgradeBrake);
            ExtractUpgradeHandlingDefaults(defaults.upgradeHandling);
            ExtractUpgradeSpeedDefaults(defaults.upgradeSpeed);
            ExtractUpgradeSpoilerDefaults(defaults.upgradeSpoiler);
            ExtractUpgradePaintDefaults(defaults.upgradePaint);
            ExtractUpgradeNeonDefaults(defaults.upgradeNeon);
            ExtractUpgradeDecalDefaults(defaults.upgradeDecal);
            ExtractUpgradeSirenDefaults(defaults.upgradeSiren);

        } finally {
            // Clean up temp object
            GameObject.DestroyImmediate(tempGO);
        }

        // Save the asset
        EditorUtility.SetDirty(defaults);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Reset singleton to reload
        RCCP_AIComponentDefaults.ResetInstance();

        Debug.Log($"[RCCP AI] Extracted defaults from RCCP components. RCCP Version: {defaults.rccpVersion}");
    }

    private static string GetRCCPVersion() {
        // Try to get version from RCCP_Version class or fallback
        try {
            var versionType = Type.GetType("RCCP_Version, Assembly-CSharp");
            if (versionType != null) {
                var versionField = versionType.GetField("version", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (versionField != null) {
                    return versionField.GetValue(null)?.ToString() ?? "Unknown";
                }
            }
        } catch (Exception) {
            // Reflection may fail if RCCP_Version class doesn't exist or has different structure
            // Fall through to return "Unknown"
        }
        return "Unknown";
    }

    #region Component Extractors

    private static void ExtractRigidbodyDefaults(Rigidbody rb, RCCP_AIComponentDefaults.RigidbodyDefaults target) {
        // These are set by RCCP wizard, not Unity defaults
        target.mass = 1350f; // RCCP wizard default
        target.linearDamping = 0.0025f;
        target.angularDamping = 0.35f;
        target.maxAngularVelocity = RCCP_Settings.Instance != null ? RCCP_Settings.Instance.maxAngularVelocity : 6f;
    }

    private static void ExtractEngineDefaults(GameObject go, RCCP_AIComponentDefaults.EngineDefaults target) {
        RCCP_Engine engine = go.AddComponent<RCCP_Engine>();
        SerializedObject so = new SerializedObject(engine);

        target.minEngineRPM = GetFloat(so, "minEngineRPM", 750f);
        target.maxEngineRPM = GetFloat(so, "maxEngineRPM", 7000f);
        target.maximumTorqueAsNM = GetFloat(so, "maximumTorqueAsNM", 200f);
        target.maximumSpeed = GetFloat(so, "maximumSpeed", 200f);
        target.engineAccelerationRate = GetFloat(so, "engineAccelerationRate", 0.75f);
        target.engineCouplingToWheelsRate = GetFloat(so, "engineCouplingToWheelsRate", 1.5f);
        target.engineDecelerationRate = GetFloat(so, "engineDecelerationRate", 0.35f);
        target.engineInertia = GetFloat(so, "engineInertia", 0.15f);
        target.engineFriction = GetFloat(so, "engineFriction", 0.2f);
        target.turboCharged = GetBool(so, "turboCharged", false);
        target.maxTurboChargePsi = GetFloat(so, "maxTurboChargePsi", 6f);
        target.turboChargerCoEfficient = GetFloat(so, "turboChargerCoEfficient", 1.5f);
        target.engineRevLimiter = GetBool(so, "engineRevLimiter", true);

        GameObject.DestroyImmediate(engine);
    }

    private static void ExtractGearboxDefaults(GameObject go, RCCP_AIComponentDefaults.GearboxDefaults target) {
        RCCP_Gearbox gearbox = go.AddComponent<RCCP_Gearbox>();
        SerializedObject so = new SerializedObject(gearbox);

        var transmissionProp = so.FindProperty("transmissionType");
        if (transmissionProp != null) {
            target.transmissionType = transmissionProp.enumNames[transmissionProp.enumValueIndex];
        }

        var ratiosProp = so.FindProperty("gearRatios");
        if (ratiosProp != null && ratiosProp.isArray) {
            target.gearRatios = new float[ratiosProp.arraySize];
            for (int i = 0; i < ratiosProp.arraySize; i++) {
                target.gearRatios[i] = ratiosProp.GetArrayElementAtIndex(i).floatValue;
            }
        }

        target.shiftingTime = GetFloat(so, "shiftingTime", 0.2f);
        target.shiftThreshold = GetFloat(so, "shiftThreshold", 0.85f);
        target.shiftUpRPM = GetFloat(so, "shiftUpRPM", 6500f);
        target.shiftDownRPM = GetFloat(so, "shiftDownRPM", 3500f);

        GameObject.DestroyImmediate(gearbox);
    }

    private static void ExtractClutchDefaults(GameObject go, RCCP_AIComponentDefaults.ClutchDefaults target) {
        RCCP_Clutch clutch = go.AddComponent<RCCP_Clutch>();
        SerializedObject so = new SerializedObject(clutch);

        target.clutchInertia = GetFloat(so, "clutchInertia", 0.5f);
        target.engageRPM = GetFloat(so, "engageRPM", 1600f);
        target.automaticClutch = GetBool(so, "automaticClutch", true);
        target.pressClutchWhileShiftingGears = GetBool(so, "pressClutchWhileShiftingGears", true);
        target.pressClutchWhileHandbraking = GetBool(so, "pressClutchWhileHandbraking", true);

        GameObject.DestroyImmediate(clutch);
    }

    private static void ExtractDifferentialDefaults(GameObject go, RCCP_AIComponentDefaults.DifferentialDefaults target) {
        RCCP_Differential diff = go.AddComponent<RCCP_Differential>();
        SerializedObject so = new SerializedObject(diff);

        var typeProp = so.FindProperty("differentialType");
        if (typeProp != null) {
            target.differentialType = typeProp.enumNames[typeProp.enumValueIndex];
        }

        target.limitedSlipRatio = GetFloat(so, "limitedSlipRatio", 80f);
        target.finalDriveRatio = GetFloat(so, "finalDriveRatio", 3.73f);

        GameObject.DestroyImmediate(diff);
    }

    private static void ExtractAxleDefaults(GameObject go, RCCP_AIComponentDefaults.AxleDefaults target) {
        RCCP_Axle axle = go.AddComponent<RCCP_Axle>();
        SerializedObject so = new SerializedObject(axle);

        target.antirollForce = GetFloat(so, "antirollForce", 500f);
        target.maxSteerAngle = GetFloat(so, "maxSteerAngle", 40f);
        target.maxBrakeTorque = GetFloat(so, "maxBrakeTorque", 3000f);
        target.maxHandbrakeTorque = GetFloat(so, "maxHandbrakeTorque", 5000f);
        target.steerSpeed = GetFloat(so, "steerSpeed", 5f);
        target.powerMultiplier = GetFloat(so, "powerMultiplier", 1f);
        target.brakeMultiplier = GetFloat(so, "brakeMultiplier", 1f);
        target.steerMultiplier = GetFloat(so, "steerMultiplier", 1f);
        target.handbrakeMultiplier = GetFloat(so, "handbrakeMultiplier", 1f);

        // These are typically set by RCCP wizard based on front/rear
        target.frontIsSteer = true;
        target.frontIsBrake = true;
        target.frontIsHandbrake = false;
        target.rearIsSteer = false;
        target.rearIsBrake = true;
        target.rearIsHandbrake = true;

        GameObject.DestroyImmediate(axle);
    }

    private static void ExtractStabilityDefaults(GameObject go, RCCP_AIComponentDefaults.StabilityDefaults target) {
        RCCP_Stability stability = go.AddComponent<RCCP_Stability>();
        SerializedObject so = new SerializedObject(stability);

        target.ABS = GetBool(so, "ABS", true);
        target.ESP = GetBool(so, "ESP", true);
        target.TCS = GetBool(so, "TCS", true);
        target.steeringHelper = GetBool(so, "steeringHelper", true);
        target.tractionHelper = GetBool(so, "tractionHelper", true);
        target.angularDragHelper = GetBool(so, "angularDragHelper", false);
        target.engageABSThreshold = GetFloat(so, "engageABSThreshold", 0.35f);
        target.engageESPThreshold = GetFloat(so, "engageESPThreshold", 0.35f);
        target.engageTCSThreshold = GetFloat(so, "engageTCSThreshold", 0.35f);
        target.steerHelperStrength = GetFloat(so, "steerHelperStrength", 0.5f);
        target.tractionHelperStrength = GetFloat(so, "tractionHelperStrength", 0.5f);
        target.angularDragHelperStrength = GetFloat(so, "angularDragHelperStrength", 0.5f);

        GameObject.DestroyImmediate(stability);
    }

    private static void ExtractWheelColliderDefaults(GameObject go, RCCP_AIComponentDefaults.WheelColliderDefaults target) {
        // RCCP_WheelCollider requires a WheelCollider component
        WheelCollider wc = go.AddComponent<WheelCollider>();
        RCCP_WheelCollider rcwc = go.AddComponent<RCCP_WheelCollider>();
        SerializedObject so = new SerializedObject(rcwc);
        SerializedObject wcSo = new SerializedObject(wc);

        // From WheelCollider
        target.wheelRadius = wc.radius;
        target.suspensionDistance = wc.suspensionDistance;
        target.suspensionSpring = wc.suspensionSpring.spring;
        target.suspensionDamper = wc.suspensionSpring.damper;

        // Friction curves from WheelCollider
        target.forwardExtremumSlip = wc.forwardFriction.extremumSlip;
        target.forwardExtremumValue = wc.forwardFriction.extremumValue;
        target.forwardAsymptoteSlip = wc.forwardFriction.asymptoteSlip;
        target.forwardAsymptoteValue = wc.forwardFriction.asymptoteValue;
        target.forwardStiffness = wc.forwardFriction.stiffness;

        target.sidewaysExtremumSlip = wc.sidewaysFriction.extremumSlip;
        target.sidewaysExtremumValue = wc.sidewaysFriction.extremumValue;
        target.sidewaysAsymptoteSlip = wc.sidewaysFriction.asymptoteSlip;
        target.sidewaysAsymptoteValue = wc.sidewaysFriction.asymptoteValue;
        target.sidewaysStiffness = wc.sidewaysFriction.stiffness;

        // From RCCP_WheelCollider
        target.wheelWidth = GetFloat(so, "width", 0.25f);
        target.camber = GetFloat(so, "camber", 0f);
        target.caster = GetFloat(so, "caster", 0f);

        GameObject.DestroyImmediate(rcwc);
        GameObject.DestroyImmediate(wc);
    }

    private static void ExtractAeroDynamicsDefaults(GameObject go, RCCP_AIComponentDefaults.AeroDynamicsDefaults target) {
        RCCP_AeroDynamics aero = go.AddComponent<RCCP_AeroDynamics>();
        SerializedObject so = new SerializedObject(aero);

        target.downForce = GetFloat(so, "downForce", 10f);
        target.airResistance = GetFloat(so, "airResistance", 10f);
        target.wheelResistance = GetFloat(so, "wheelResistance", 10f);

        GameObject.DestroyImmediate(aero);
    }

    private static void ExtractNosDefaults(GameObject go, RCCP_AIComponentDefaults.NosDefaults target) {
        RCCP_Nos nos = go.AddComponent<RCCP_Nos>();
        SerializedObject so = new SerializedObject(nos);

        target.enabled = nos.enabled;
        target.torqueMultiplier = GetFloat(so, "torqueMultiplier", 2.5f);
        target.durationTime = GetFloat(so, "durationTime", 3f);
        target.regenerateTime = GetFloat(so, "regenerateTime", 2f);
        target.regenerateRate = GetFloat(so, "regenerateRate", 0.1f);

        GameObject.DestroyImmediate(nos);
    }

    private static void ExtractFuelTankDefaults(GameObject go, RCCP_AIComponentDefaults.FuelTankDefaults target) {
        RCCP_FuelTank fuel = go.AddComponent<RCCP_FuelTank>();
        SerializedObject so = new SerializedObject(fuel);

        target.enabled = fuel.enabled;
        target.fuelTankCapacity = GetFloat(so, "fuelTankCapacity", 60f);
        target.fuelTankFillAmount = GetFloat(so, "fuelTankFillAmount", 1f);
        target.stopEngineWhenEmpty = GetBool(so, "stopEngine", true);
        target.baseLitersPerHour = GetFloat(so, "baseLitersPerHour", 2f);
        target.maxLitersPerHour = GetFloat(so, "maxLitersPerHour", 20f);

        GameObject.DestroyImmediate(fuel);
    }

    private static void ExtractLimiterDefaults(GameObject go, RCCP_AIComponentDefaults.LimiterDefaults target) {
        RCCP_Limiter limiter = go.AddComponent<RCCP_Limiter>();
        SerializedObject so = new SerializedObject(limiter);

        target.enabled = limiter.enabled;
        target.applyDownhillForce = GetBool(so, "applyDownhillForce", true);
        target.downhillForceStrength = GetFloat(so, "downhillForceStrength", 100f);

        GameObject.DestroyImmediate(limiter);
    }

    private static void ExtractDamageDefaults(GameObject go, RCCP_AIComponentDefaults.DamageDefaults target) {
        RCCP_Damage damage = go.AddComponent<RCCP_Damage>();
        SerializedObject so = new SerializedObject(damage);

        target.meshDeformation = GetBool(so, "meshDeformation", true);
        target.maximumDamage = GetFloat(so, "maximumDamage", 0.75f);
        target.deformationRadius = GetFloat(so, "deformationRadius", 0.75f);
        target.deformationMultiplier = GetFloat(so, "deformationMultiplier", 1f);

        GameObject.DestroyImmediate(damage);
    }

    private static void ExtractInputDefaults(GameObject go, RCCP_AIComponentDefaults.InputDefaults target) {
        RCCP_Input input = go.AddComponent<RCCP_Input>();
        SerializedObject so = new SerializedObject(input);

        target.counterSteerFactor = GetFloat(so, "counterSteerFactor", 0.5f);
        target.counterSteering = GetBool(so, "counterSteering", true);
        target.steeringLimiter = GetBool(so, "steeringLimiter", true);
        target.autoReverse = GetBool(so, "autoReverse", true);
        target.steeringDeadzone = GetFloat(so, "steeringDeadzone", 0.05f);

        GameObject.DestroyImmediate(input);
    }

    #endregion

    #region Additional Core Component Extractors

    private static void ExtractAudioDefaults(GameObject go, RCCP_AIComponentDefaults.AudioDefaults target) {
        // RCCP_Audio uses nested classes (EngineSound[], etc.) - no top-level extractable fields
        // We extract defaults from the first engine sound layer if available
        RCCP_Audio audio = go.AddComponent<RCCP_Audio>();
        SerializedObject so = new SerializedObject(audio);

        // Try to get defaults from engineSounds[0] if it exists
        var engineSoundsProp = so.FindProperty("engineSounds");
        if (engineSoundsProp != null && engineSoundsProp.isArray && engineSoundsProp.arraySize > 0) {
            var firstSound = engineSoundsProp.GetArrayElementAtIndex(0);
            target.engineMinPitch = GetNestedFloat(firstSound, "minPitch", 0.1f);
            target.engineMaxPitch = GetNestedFloat(firstSound, "maxPitch", 1f);
            target.engineMinRPM = GetNestedFloat(firstSound, "minRPM", 600f);
            target.engineMaxRPM = GetNestedFloat(firstSound, "maxRPM", 8000f);
            target.engineMinDistance = GetNestedFloat(firstSound, "minDistance", 10f);
            target.engineMaxDistance = GetNestedFloat(firstSound, "maxDistance", 200f);
            target.engineMaxVolume = GetNestedFloat(firstSound, "maxVolume", 1f);
        } else {
            // Use hardcoded defaults if no engine sounds configured
            target.engineMinPitch = 0.1f;
            target.engineMaxPitch = 1f;
            target.engineMinRPM = 600f;
            target.engineMaxRPM = 8000f;
            target.engineMinDistance = 10f;
            target.engineMaxDistance = 200f;
            target.engineMaxVolume = 1f;
        }

        GameObject.DestroyImmediate(audio);
    }

    private static float GetNestedFloat(SerializedProperty parent, string propertyName, float fallback) {
        var prop = parent.FindPropertyRelative(propertyName);
        return prop != null ? prop.floatValue : fallback;
    }

    private static void ExtractLightsDefaults(GameObject go, RCCP_AIComponentDefaults.LightsDefaults target) {
        // RCCP_Lights is just a manager - extract from RCCP_Light (individual light component)
        RCCP_Light light = go.AddComponent<RCCP_Light>();
        SerializedObject so = new SerializedObject(light);

        target.intensity = GetFloat(so, "intensity", 1f);
        target.smoothness = GetFloat(so, "smoothness", 0.5f);
        target.useLensFlares = GetBool(so, "useLensFlares", true);
        target.flareBrightness = GetFloat(so, "flareBrightness", 1.5f);
        target.isBreakable = GetBool(so, "isBreakable", true);
        target.breakStrength = GetFloat(so, "strength", 100f);

        GameObject.DestroyImmediate(light);
    }

    private static void ExtractParticlesDefaults(GameObject go, RCCP_AIComponentDefaults.ParticlesDefaults target) {
        // RCCP_Particles only has prefab references and LayerMask - no numeric config
        RCCP_Particles particles = go.AddComponent<RCCP_Particles>();
        SerializedObject so = new SerializedObject(particles);

        // Extract collision filter LayerMask value
        var collisionFilterProp = so.FindProperty("collisionFilter");
        target.collisionFilterValue = collisionFilterProp != null ? collisionFilterProp.intValue : -1;

        GameObject.DestroyImmediate(particles);
    }

    private static void ExtractLodDefaults(GameObject go, RCCP_AIComponentDefaults.LodDefaults target) {
        RCCP_Lod lod = go.AddComponent<RCCP_Lod>();
        SerializedObject so = new SerializedObject(lod);

        target.forceToFirstLevel = GetBool(so, "forceToFirstLevel", false);
        target.forceToLatestLevel = GetBool(so, "forceToLatestLevel", false);
        target.lodFactor = GetFloat(so, "lodFactor", 0.8f);

        GameObject.DestroyImmediate(lod);
    }

    #endregion

    #region Addon Component Extractors

    private static void ExtractAIDefaults(GameObject go, RCCP_AIComponentDefaults.AIDefaults target) {
        RCCP_AI ai = go.AddComponent<RCCP_AI>();
        SerializedObject so = new SerializedObject(ai);

        // Navigation
        target.waypointRadius = GetFloat(so, "waypointRadius", 5f);
        target.nextWaypointDistance = GetFloat(so, "nextWaypointDistance", 20f);

        // Speed control
        target.maxSpeed = GetFloat(so, "limitSpeed", 100f);
        target.smoothedSteer = GetFloat(so, "smoothedSteer", 5f);

        // Obstacle detection
        target.raycastDistance = GetFloat(so, "rayDistance", 30f);
        target.raycastAngle = GetFloat(so, "raycastAngle", 30f);
        target.raycastCount = GetInt(so, "totalRays", 5);

        // Behavior
        target.brakeDistance = GetFloat(so, "brakeDistance", 10f);
        target.steeringSensitivity = GetFloat(so, "steeringSensitivity", 1f);

        GameObject.DestroyImmediate(ai);
    }

    private static void ExtractBodyTiltDefaults(GameObject go, RCCP_AIComponentDefaults.BodyTiltDefaults target) {
        RCCP_BodyTilt bodyTilt = go.AddComponent<RCCP_BodyTilt>();
        SerializedObject so = new SerializedObject(bodyTilt);

        target.enabled = bodyTilt.enabled;
        target.tiltAngle = GetFloat(so, "tiltAngle", 5f);
        target.tiltSpeed = GetFloat(so, "tiltSpeed", 2f);
        target.verticalTiltAngle = GetFloat(so, "verticalTiltAngle", 2f);

        GameObject.DestroyImmediate(bodyTilt);
    }

    private static void ExtractExhaustsDefaults(GameObject go, RCCP_AIComponentDefaults.ExhaustsDefaults target) {
        RCCP_Exhausts exhausts = go.AddComponent<RCCP_Exhausts>();
        SerializedObject so = new SerializedObject(exhausts);

        target.emissionMultiplier = GetFloat(so, "emissionMultiplier", 1f);
        target.smokeIntensity = GetFloat(so, "smokeIntensity", 1f);
        target.flameEnabled = GetBool(so, "flameEnabled", true);
        target.flameThreshold = GetFloat(so, "flameThreshold", 0.8f);

        GameObject.DestroyImmediate(exhausts);
    }

    private static void ExtractTrailerAttacherDefaults(GameObject go, RCCP_AIComponentDefaults.TrailerAttacherDefaults target) {
        RCCP_TrailerAttacher trailer = go.AddComponent<RCCP_TrailerAttacher>();
        SerializedObject so = new SerializedObject(trailer);

        target.connectionDistance = GetFloat(so, "connectionDistance", 2f);
        target.breakForce = GetFloat(so, "breakForce", 10000f);
        target.breakTorque = GetFloat(so, "breakTorque", 10000f);
        target.autoConnect = GetBool(so, "autoConnect", false);

        GameObject.DestroyImmediate(trailer);
    }

    private static void ExtractWheelBlurDefaults(GameObject go, RCCP_AIComponentDefaults.WheelBlurDefaults target) {
        RCCP_WheelBlur wheelBlur = go.AddComponent<RCCP_WheelBlur>();
        SerializedObject so = new SerializedObject(wheelBlur);

        target.enabled = wheelBlur.enabled;
        target.blurStartSpeed = GetFloat(so, "blurStartSpeed", 50f);
        target.blurFullSpeed = GetFloat(so, "blurFullSpeed", 100f);
        target.blurIntensity = GetFloat(so, "blurIntensity", 1f);

        GameObject.DestroyImmediate(wheelBlur);
    }

    private static void ExtractRecorderDefaults(GameObject go, RCCP_AIComponentDefaults.RecorderDefaults target) {
        RCCP_Recorder recorder = go.AddComponent<RCCP_Recorder>();
        SerializedObject so = new SerializedObject(recorder);

        target.enabled = recorder.enabled;
        target.recordInterval = GetFloat(so, "recordInterval", 0.1f);
        target.maxRecordedFrames = GetInt(so, "maxRecordedFrames", 1000);
        target.recordPosition = GetBool(so, "recordPosition", true);
        target.recordRotation = GetBool(so, "recordRotation", true);
        target.recordInputs = GetBool(so, "recordInputs", true);

        GameObject.DestroyImmediate(recorder);
    }

    private static void ExtractAIDynamicObstacleAvoidanceDefaults(GameObject go, RCCP_AIComponentDefaults.AIDynamicObstacleAvoidanceDefaults target) {
        RCCP_AIDynamicObstacleAvoidance avoidance = go.AddComponent<RCCP_AIDynamicObstacleAvoidance>();
        SerializedObject so = new SerializedObject(avoidance);

        target.enabled = avoidance.enabled;
        target.detectionDistance = GetFloat(so, "detectionDistance", 20f);
        target.detectionAngle = GetFloat(so, "detectionAngle", 45f);
        target.avoidanceStrength = GetFloat(so, "avoidanceStrength", 1f);
        target.reactionTime = GetFloat(so, "reactionTime", 0.5f);
        target.rayCount = GetInt(so, "rayCount", 5);

        GameObject.DestroyImmediate(avoidance);
    }

    private static void ExtractDetachablePartDefaults(GameObject go, RCCP_AIComponentDefaults.DetachablePartDefaults target) {
        RCCP_DetachablePart detachable = go.AddComponent<RCCP_DetachablePart>();
        SerializedObject so = new SerializedObject(detachable);

        target.detachmentForce = GetFloat(so, "detachmentForce", 5000f);
        target.detachmentTorque = GetFloat(so, "detachmentTorque", 5000f);
        target.canDetach = GetBool(so, "canDetach", true);
        target.randomRotationForce = GetFloat(so, "randomRotationForce", 10f);

        GameObject.DestroyImmediate(detachable);
    }

    private static void ExtractVisualDashboardDefaults(GameObject go, RCCP_AIComponentDefaults.VisualDashboardDefaults target) {
        RCCP_Visual_Dashboard dashboard = go.AddComponent<RCCP_Visual_Dashboard>();
        SerializedObject so = new SerializedObject(dashboard);

        target.needleSmoothness = GetFloat(so, "needleSmoothness", 5f);
        target.rpmMultiplier = GetFloat(so, "rpmMultiplier", 1f);
        target.speedMultiplier = GetFloat(so, "speedMultiplier", 1f);
        target.digitalDisplay = GetBool(so, "digitalDisplay", true);

        GameObject.DestroyImmediate(dashboard);
    }

    private static void ExtractExteriorCamerasDefaults(GameObject go, RCCP_AIComponentDefaults.ExteriorCamerasDefaults target) {
        RCCP_Exterior_Cameras cameras = go.AddComponent<RCCP_Exterior_Cameras>();
        SerializedObject so = new SerializedObject(cameras);

        target.hoodCameraFOV = GetFloat(so, "hoodCameraFOV", 60f);
        target.wheelCameraFOV = GetFloat(so, "wheelCameraFOV", 70f);
        target.cameraSmoothing = GetFloat(so, "cameraSmoothing", 5f);
        target.autoSwitch = GetBool(so, "autoSwitch", false);

        GameObject.DestroyImmediate(cameras);
    }

    #endregion

    #region Upgrade Component Extractors

    // Upgrade components don't need GameObject instantiation - they use static defaults
    // These are applied via RCCP_Customizer system

    private static void ExtractUpgradeEngineDefaults(RCCP_AIComponentDefaults.UpgradeEngineDefaults target) {
        // These are configuration values, not extracted from components
        // Values are set based on RCCP upgrade system defaults
        target.maxLevel = 5;
        target.torqueIncreasePerLevel = 25f;
        target.rpmIncreasePerLevel = 500f;
        target.efficiencyPerLevel = 0.05f;
    }

    private static void ExtractUpgradeBrakeDefaults(RCCP_AIComponentDefaults.UpgradeBrakeDefaults target) {
        target.maxLevel = 5;
        target.brakeForceIncreasePerLevel = 500f;
        target.absEfficiencyPerLevel = 0.05f;
    }

    private static void ExtractUpgradeHandlingDefaults(RCCP_AIComponentDefaults.UpgradeHandlingDefaults target) {
        target.maxLevel = 5;
        target.steerAngleIncreasePerLevel = 2f;
        target.stabilityIncreasePerLevel = 0.1f;
        target.tractionIncreasePerLevel = 0.05f;
    }

    private static void ExtractUpgradeSpeedDefaults(RCCP_AIComponentDefaults.UpgradeSpeedDefaults target) {
        target.maxLevel = 5;
        target.topSpeedIncreasePerLevel = 10f;
        target.accelerationIncreasePerLevel = 0.05f;
    }

    private static void ExtractUpgradeSpoilerDefaults(RCCP_AIComponentDefaults.UpgradeSpoilerDefaults target) {
        target.maxLevel = 3;
        target.downforceIncreasePerLevel = 50f;
        target.dragIncreasePerLevel = 5f;
    }

    private static void ExtractUpgradePaintDefaults(RCCP_AIComponentDefaults.UpgradePaintDefaults target) {
        target.useMetallic = true;
        target.defaultSmoothness = 0.8f;
        target.defaultMetallic = 0.5f;
    }

    private static void ExtractUpgradeNeonDefaults(RCCP_AIComponentDefaults.UpgradeNeonDefaults target) {
        target.enabled = false;
        target.intensity = 1f;
        target.range = 5f;
        target.underglow = true;
    }

    private static void ExtractUpgradeDecalDefaults(RCCP_AIComponentDefaults.UpgradeDecalDefaults target) {
        target.defaultScale = 1f;
        target.defaultOpacity = 1f;
        target.allowMultiple = true;
    }

    private static void ExtractUpgradeSirenDefaults(RCCP_AIComponentDefaults.UpgradeSirenDefaults target) {
        target.enabled = false;
        target.lightIntensity = 2f;
        target.flashInterval = 0.25f;
        target.soundEnabled = true;
        target.soundVolume = 1f;
    }

    #endregion

    #region Helpers

    private static float GetFloat(SerializedObject so, string propertyName, float fallback) {
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.floatValue : fallback;
    }

    private static bool GetBool(SerializedObject so, string propertyName, bool fallback) {
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.boolValue : fallback;
    }

    private static int GetInt(SerializedObject so, string propertyName, int fallback) {
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.intValue : fallback;
    }

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
