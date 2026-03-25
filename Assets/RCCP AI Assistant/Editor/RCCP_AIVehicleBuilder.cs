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
/// Builds and configures RCCP vehicles based on AI-generated configurations.
/// This is the main orchestrator - component-specific logic is in partial class files.
/// </summary>
public static partial class RCCP_AIVehicleBuilder {

    #region Prefab Safety

    /// <summary>
    /// Result of attempting to destroy a component's GameObject.
    /// </summary>
    public enum DestroyResult {
        Success,            // GameObject was destroyed
        DisabledInstead,    // Prefab child - component was disabled instead
        NotFound,           // Component was null
        Failed              // Destruction failed for another reason
    }

    /// <summary>
    /// Safely destroys a component's GameObject, handling prefab instances.
    /// If the GameObject is part of a prefab instance, disables the component instead.
    /// </summary>
    /// <typeparam name="T">The MonoBehaviour type</typeparam>
    /// <param name="component">The component whose GameObject to destroy</param>
    /// <param name="componentName">Name for logging purposes</param>
    /// <returns>Result indicating what action was taken</returns>
    public static DestroyResult TryDestroyComponentGameObject<T>(T component, string componentName) where T : MonoBehaviour {
        if (component == null) {
            if (VerboseLogging) Debug.Log($"[RCCP AI] {componentName} component not found, nothing to remove");
            return DestroyResult.NotFound;
        }

        GameObject targetObject = component.gameObject;

        // Check if this GameObject is part of a prefab instance
        if (PrefabUtility.IsPartOfPrefabInstance(targetObject)) {
            // Warn user about prefab-linked objects before modifying hierarchy
            EditorUtility.DisplayDialog(
                "Prefab Instance Detected",
                $"'{targetObject.name}' is part of a prefab instance. Removing it may require unpacking the prefab. " +
                "This operation will either destroy added overrides or disable the component to preserve the prefab link.",
                "OK"
            );

            // Check if this specific object is an added object (not part of the original prefab)
            // Added objects CAN be destroyed
            if (PrefabUtility.IsAddedGameObjectOverride(targetObject)) {
                // This was added to the prefab instance, safe to destroy
                Undo.DestroyObjectImmediate(targetObject);
                if (VerboseLogging) Debug.Log($"[RCCP AI] {componentName} component removed (added prefab override destroyed)");
                return DestroyResult.Success;
            }

            // This is part of the original prefab structure - cannot destroy
            // Disable the component instead
            Undo.RecordObject(component, $"RCCP AI Disable {componentName}");
            component.enabled = false;
            EditorUtility.SetDirty(component);

            Debug.LogWarning($"[RCCP AI] Cannot remove {componentName} - it's part of a prefab instance. Component has been DISABLED instead. To fully remove, unpack the prefab first.");
            return DestroyResult.DisabledInstead;
        }

        // Not a prefab instance - safe to destroy
        Undo.DestroyObjectImmediate(targetObject);
        if (VerboseLogging) Debug.Log($"[RCCP AI] {componentName} component removed (GameObject destroyed)");
        return DestroyResult.Success;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Enable verbose logging to console. Reads from RCCP_AISettings.
    /// </summary>
    public static bool VerboseLogging => RCCP_AISettings.Instance != null && RCCP_AISettings.Instance.verboseLogging;

    /// <summary>
    /// Context for logging history entries
    /// </summary>
    public struct HistoryContext {
        public string panelType;
        public string userPrompt;
        public string explanation;
        public string appliedJson;
    }

    /// <summary>
    /// Current history context (set before calling Build/Customize)
    /// </summary>
    public static HistoryContext? CurrentContext { get; set; }

    /// <summary>
    /// When true, Partial apply methods will apply ALL values including false booleans and zeros.
    /// Used during restore operations to ensure complete state restoration.
    /// </summary>
    private static bool IsRestoreMode { get; set; } = false;

    #endregion

    #region Main Entry Points

    /// <summary>
    /// Main entry point - builds or updates a vehicle with the given configuration.
    /// </summary>
    public static void BuildVehicle(GameObject vehicle, RCCP_AIConfig.VehicleSetupConfig config) {
        if (vehicle == null || config == null) {
            Debug.LogError("[RCCP AI] Vehicle or config is null!");
            return;
        }

        // Begin undo group for all changes
        Undo.SetCurrentGroupName("RCCP AI Build Vehicle");
        int undoGroup = Undo.GetCurrentGroup();

        if (VerboseLogging) Debug.Log($"[RCCP AI] Building vehicle: {config.vehicleConfig?.name ?? vehicle.name}");

        // Check if vehicle already has RCCP using centralized utility
        RCCP_CarController carController = RCCP_AIUtility.GetRCCPController(vehicle);
        bool hasExistingRCCP = RCCP_AIUtility.HasRCCP(vehicle);

        // Capture before state (will be empty for new vehicles)
        string beforeState = hasExistingRCCP ? CaptureVehicleState(carController) : "(New vehicle)";
        string beforeStateJson = hasExistingRCCP ? CaptureVehicleStateAsJson(carController) : "";

        if (!hasExistingRCCP) {
            // Create new RCCP setup
            carController = CreateNewVehicle(vehicle, config);
        }

        if (carController == null) {
            Debug.LogError("[RCCP AI] Failed to create/find RCCP_CarController!");
            return;
        }

        // Apply configurations
        // Skip COM during vehicle creation - let RCCP_AeroDynamics.Reset() auto-calculate from mesh geometry
        ApplyRigidbodySettings(carController, config, skipCOM: true);
        ApplyEngineSettings(carController, config);
        ApplyClutchSettings(carController, config);
        ApplyGearboxSettings(carController, config);
        ApplyDifferentialSettings(carController, config);

        // Ensure drive type is respected even if differential config is missing
        if (!string.IsNullOrEmpty(config.driveType) && config.differential == null) {
            ApplyDriveTypeChange(carController, config.driveType);
        }

        ApplyAxleSettings(carController, config);
        ApplyStabilitySettings(carController, config);
        ApplyWheelSettings(carController, config);

        // Detect and assign wheels if paths provided
        if (config.detectedWheels != null) {
            AssignDetectedWheels(carController, vehicle.transform, config.detectedWheels);
        }

        EditorUtility.SetDirty(carController);
        if (VerboseLogging) Debug.Log($"[RCCP AI] Vehicle setup complete: {config.explanation}");

        // Capture after state and log history
        string afterState = CaptureVehicleState(carController);
        LogHistory(carController.gameObject, beforeState, beforeStateJson, afterState, config.explanation);

        // Collapse all changes into a single undo operation
        Undo.CollapseUndoOperations(undoGroup);
    }

    /// <summary>
    /// Customizes an existing RCCP vehicle with partial configuration updates.
    /// Only applies settings that are present in the config (non-null sections with meaningful values).
    /// </summary>
    /// <param name="forceApplyAll">When true, applies all non-null configs without checking for meaningful values. Used for restore.</param>
    /// <param name="skipHistory">When true, skips logging to history. Used for restore operations.</param>
    /// <param name="originalJson">Original JSON string, used for detecting explicit 'false' values in boolean fields.</param>
    /// <param name="skipRefreshSelection">When true, skips the RefreshSelection call at the end. Used for batch operations to avoid selection interference.</param>
    public static void CustomizeVehicle(RCCP_CarController carController, RCCP_AIConfig.VehicleSetupConfig config, bool forceApplyAll = false, bool skipHistory = false, string originalJson = null, bool skipRefreshSelection = false) {
        if (carController == null || config == null) {
            Debug.LogError("[RCCP AI] CarController or config is null!");
            return;
        }

        // Begin undo group for all changes
        string undoName = forceApplyAll ? "RCCP AI Restore Vehicle" : "RCCP AI Customize Vehicle";
        Undo.SetCurrentGroupName(undoName);
        int undoGroup = Undo.GetCurrentGroup();

        if (VerboseLogging) Debug.Log(forceApplyAll ? "[RCCP AI] Restoring vehicle state..." : "[RCCP AI] Customizing vehicle with partial updates...");

        // Capture before state for history (both display string and JSON for restore)
        string beforeState = "";
        string beforeStateJson = "";
        if (!skipHistory) {
            beforeState = CaptureVehicleState(carController);
            beforeStateJson = CaptureVehicleStateAsJson(carController);
        }

        // Prepare "All True" config for boolean detection if JSON is available
        // This allows us to distinguish between "missing" (default false) and "explicit false"
        RCCP_AIConfig.VehicleSetupConfig configAllTrue = null;
        if (!string.IsNullOrEmpty(originalJson) && !forceApplyAll) {
            configAllTrue = new RCCP_AIConfig.VehicleSetupConfig();
            configAllTrue.stability = CreateAllTrueStabilityConfig();
            configAllTrue.clutch = CreateAllTrueClutchConfig();
            configAllTrue.engine = CreateAllTrueEngineConfig();
            configAllTrue.nos = CreateAllTrueNosConfig();
            configAllTrue.fuelTank = CreateAllTrueFuelTankConfig();
            configAllTrue.limiter = CreateAllTrueLimiterConfig();
            configAllTrue.input = CreateAllTrueInputConfig();

            // Initialize all other nested configs to avoid null reference errors during FromJsonOverwrite
            configAllTrue.vehicleConfig = new RCCP_AIConfig.VehicleConfig {
                centerOfMassOffset = new RCCP_AIConfig.Vector3Config()
            };
            configAllTrue.gearbox = new RCCP_AIConfig.GearboxConfig();
            configAllTrue.differential = new RCCP_AIConfig.DifferentialConfig();
            configAllTrue.axles = CreateAllTrueAxlesConfig();
            configAllTrue.suspension = new RCCP_AIConfig.SuspensionConfig();
            configAllTrue.wheelFriction = CreateAllTrueWheelFrictionConfig();
            configAllTrue.wheels = CreateAllTrueWheelConfig();
            configAllTrue.aeroDynamics = new RCCP_AIConfig.AeroDynamicsConfig();

            // Overwrite with JSON - fields present in JSON will overwrite our 'true' values
            JsonUtility.FromJsonOverwrite(originalJson, configAllTrue);
        }

        int changesApplied = 0;

        // Apply sections - when forceApplyAll is true, apply all non-null configs
        // When false, only apply sections with meaningful values (for AI-generated partial updates)
        if (forceApplyAll ? config.vehicleConfig != null : HasMeaningfulValues(config.vehicleConfig)) {
            ApplyRigidbodySettings(carController, config);
            changesApplied++;
        }

        if (forceApplyAll ? config.engine != null : HasMeaningfulValues(config.engine, configAllTrue?.engine)) {
            ApplyEngineSettingsPartial(carController, config.engine, configAllTrue?.engine);
            changesApplied++;
        }

        if (forceApplyAll ? config.clutch != null : HasMeaningfulValues(config.clutch, configAllTrue?.clutch)) {
            ApplyClutchSettingsPartial(carController, config.clutch, configAllTrue?.clutch);
            changesApplied++;
        }

        if (forceApplyAll ? config.gearbox != null : HasMeaningfulValues(config.gearbox)) {
            ApplyGearboxSettingsPartial(carController, config.gearbox);
            changesApplied++;
        }

        if (forceApplyAll ? config.differential != null : HasMeaningfulValues(config.differential)) {
            ApplyDifferentialSettingsPartial(carController, config.differential);
            changesApplied++;
        }

        // Handle drive type change (FWD/RWD/AWD) - manages differentials and axle connections
        if (!string.IsNullOrEmpty(config.driveType)) {
            ApplyDriveTypeChange(carController, config.driveType);
            changesApplied++;
        }

        if (forceApplyAll ? config.axles != null : HasMeaningfulValues(config.axles)) {
            ApplyAxleSettingsPartial(carController, config.axles, configAllTrue?.axles);
            changesApplied++;
        }

        if (forceApplyAll ? config.suspension != null : HasMeaningfulValues(config.suspension)) {
            ApplySuspensionSettingsPartial(carController, config.suspension, config.frontSuspension, config.rearSuspension);
            changesApplied++;
        }

        if (forceApplyAll ? config.wheelFriction != null : HasMeaningfulValues(config.wheelFriction)) {
            ApplyWheelFrictionPartial(carController, config.wheelFriction, config.frontWheelFriction, config.rearWheelFriction);
            changesApplied++;
        }

        if (forceApplyAll ? config.wheels != null : HasMeaningfulValues(config.wheels)) {
            ApplyWheelGeometryPartial(carController, config.wheels, configAllTrue?.wheels);
            changesApplied++;
        }

        if (forceApplyAll ? config.stability != null : HasMeaningfulValues(config.stability, configAllTrue?.stability)) {
            ApplyStabilitySettingsPartial(carController, config.stability, configAllTrue?.stability);
            changesApplied++;
        }

        if (forceApplyAll ? config.aeroDynamics != null : HasMeaningfulValues(config.aeroDynamics)) {
            ApplyAeroDynamicsPartial(carController, config.aeroDynamics);
            changesApplied++;
        }

        if (forceApplyAll ? config.nos != null : HasMeaningfulValues(config.nos, configAllTrue?.nos)) {
            ApplyNosSettingsPartial(carController, config.nos, configAllTrue?.nos);
            changesApplied++;
        }

        if (forceApplyAll ? config.fuelTank != null : HasMeaningfulValues(config.fuelTank, configAllTrue?.fuelTank)) {
            ApplyFuelTankSettingsPartial(carController, config.fuelTank, configAllTrue?.fuelTank);
            changesApplied++;
        }

        if (forceApplyAll ? config.limiter != null : HasMeaningfulValues(config.limiter, configAllTrue?.limiter)) {
            ApplyLimiterSettingsPartial(carController, config.limiter, configAllTrue?.limiter);
            changesApplied++;
        }

        // Recorder (remove only - no configurable properties)
        if (config.recorder != null && config.recorder.remove) {
            ApplyRecorderSettingsPartial(carController, config.recorder);
            changesApplied++;
        }

        // TrailerAttacher (remove only - no configurable properties)
        if (config.trailerAttacher != null && config.trailerAttacher.remove) {
            ApplyTrailerAttacherSettingsPartial(carController, config.trailerAttacher);
            changesApplied++;
        }

        // Customizer (save/load system settings)
        if (config.customizer != null) {
            ApplyCustomizerSettingsPartial(carController, config.customizer, configAllTrue?.customizer);
            changesApplied++;
        }

        if (forceApplyAll ? config.input != null : HasMeaningfulValues(config.input, configAllTrue?.input)) {
            ApplyInputSettingsPartial(carController, config.input, configAllTrue?.input);
            changesApplied++;
        }

        // Audio settings (restore only - forceApplyAll mode)
        if (forceApplyAll && config.audio != null) {
            ApplyAudioSettingsForRestore(carController, config.audio);
            changesApplied++;
        }

        // Lights settings (restore only - forceApplyAll mode)
        if (forceApplyAll && config.lights != null) {
            ApplyLightsSettingsForRestore(carController, config.lights);
            changesApplied++;
        }

        // Damage settings (restore only - forceApplyAll mode)
        if (forceApplyAll && config.damage != null) {
            ApplyDamageSettingsForRestore(carController, config.damage);
            changesApplied++;
        }

        // Visual effects settings (restore only - forceApplyAll mode)
        if (forceApplyAll && config.visualEffects != null) {
            ApplyVisualEffectsForRestore(carController, config.visualEffects);
            changesApplied++;
        }

        // Handle per-vehicle behavior preset
        if (!string.IsNullOrEmpty(config.vehicleBehavior)) {
            ApplyVehicleBehavior(carController, config.vehicleBehavior);
            changesApplied++;
        }

        EditorUtility.SetDirty(carController);
        if (VerboseLogging) Debug.Log($"[RCCP AI] {(forceApplyAll ? "Restore" : "Customization")} complete: {changesApplied} sections updated. {config.explanation}");

        // Capture after state and log history (skip for restore operations)
        if (!skipHistory) {
            string afterState = CaptureVehicleState(carController);
            LogHistory(carController.gameObject, beforeState, beforeStateJson, afterState, config.explanation);
        }

        // Refresh selection to update editor scripts with new component data
        // Skip during batch operations to avoid selection interference with multiple vehicles
        if (!skipRefreshSelection) {
            RCCP_AIUtility.RefreshSelection(carController.gameObject);
        }

        // Collapse all changes into a single undo operation
        Undo.CollapseUndoOperations(undoGroup);
    }

    #region All-True Config Helpers for Boolean Detection

    private static RCCP_AIConfig.StabilityConfig CreateAllTrueStabilityConfig() {
        var c = new RCCP_AIConfig.StabilityConfig();
        c.remove = true;
        c.ABS = true;
        c.ESP = true;
        c.TCS = true;
        c.steeringHelper = true;
        c.tractionHelper = true;
        c.angularDragHelper = true;
        return c;
    }

    private static RCCP_AIConfig.ClutchConfig CreateAllTrueClutchConfig() {
        var c = new RCCP_AIConfig.ClutchConfig();
        c.automaticClutch = true;
        c.pressClutchWhileShiftingGears = true;
        c.pressClutchWhileHandbraking = true;
        return c;
    }

    private static RCCP_AIConfig.EngineConfig CreateAllTrueEngineConfig() {
        var c = new RCCP_AIConfig.EngineConfig();
        c.turboCharged = true;
        return c;
    }

    private static RCCP_AIConfig.NosConfig CreateAllTrueNosConfig() {
        var c = new RCCP_AIConfig.NosConfig();
        c.enabled = true;
        c.remove = true;
        // Float fields initialized to NaN to detect JSON presence
        c.torqueMultiplier = float.NaN;
        c.durationTime = float.NaN;
        c.regenerateTime = float.NaN;
        c.regenerateRate = float.NaN;
        return c;
    }

    private static RCCP_AIConfig.FuelTankConfig CreateAllTrueFuelTankConfig() {
        var c = new RCCP_AIConfig.FuelTankConfig();
        c.enabled = true;
        c.remove = true;
        c.stopEngineWhenEmpty = true;
        // Float fields initialized to NaN to detect JSON presence
        c.fuelTankCapacity = float.NaN;
        c.fuelTankFillAmount = float.NaN;
        c.baseLitersPerHour = float.NaN;
        c.maxLitersPerHour = float.NaN;
        return c;
    }

    private static RCCP_AIConfig.LimiterConfig CreateAllTrueLimiterConfig() {
        var c = new RCCP_AIConfig.LimiterConfig();
        c.enabled = true;
        c.remove = true;
        c.applyDownhillForce = true;
        // Float field initialized to NaN to detect JSON presence
        c.downhillForceStrength = float.NaN;
        return c;
    }

    private static RCCP_AIConfig.InputConfig CreateAllTrueInputConfig() {
        var c = new RCCP_AIConfig.InputConfig();
        c.counterSteering = true;
        c.steeringLimiter = true;
        c.autoReverse = true;
        // Float fields initialized to NaN to detect JSON presence
        c.counterSteerFactor = float.NaN;
        c.steeringDeadzone = float.NaN;
        return c;
    }

    private static RCCP_AIConfig.AxlesConfig CreateAllTrueAxlesConfig() {
        return new RCCP_AIConfig.AxlesConfig {
            front = new RCCP_AIConfig.AxleConfig {
                isSteer = true,
                isBrake = true,
                isHandbrake = true,
                // Float fields initialized to NaN to detect JSON presence
                maxSteerAngle = float.NaN,
                maxBrakeTorque = float.NaN,
                maxHandbrakeTorque = float.NaN,
                antirollForce = float.NaN,
                steerSpeed = float.NaN,
                powerMultiplier = float.NaN,
                steerMultiplier = float.NaN,
                brakeMultiplier = float.NaN,
                handbrakeMultiplier = float.NaN
            },
            rear = new RCCP_AIConfig.AxleConfig {
                isSteer = true,
                isBrake = true,
                isHandbrake = true,
                // Float fields initialized to NaN to detect JSON presence
                maxSteerAngle = float.NaN,
                maxBrakeTorque = float.NaN,
                maxHandbrakeTorque = float.NaN,
                antirollForce = float.NaN,
                steerSpeed = float.NaN,
                powerMultiplier = float.NaN,
                steerMultiplier = float.NaN,
                brakeMultiplier = float.NaN,
                handbrakeMultiplier = float.NaN
            }
        };
    }

    private static RCCP_AIConfig.WheelFrictionConfig CreateAllTrueWheelFrictionConfig() {
        return new RCCP_AIConfig.WheelFrictionConfig {
            forward = new RCCP_AIConfig.FrictionCurveConfig(),
            sideways = new RCCP_AIConfig.FrictionCurveConfig()
        };
    }

    private static RCCP_AIConfig.WheelConfig CreateAllTrueWheelConfig() {
        return new RCCP_AIConfig.WheelConfig {
            // Float fields initialized to NaN to detect JSON presence
            wheelWidth = float.NaN,
            camber = float.NaN,
            caster = float.NaN,
            grip = float.NaN,
            forwardFriction = new RCCP_AIConfig.FrictionCurveConfig(),
            sidewaysFriction = new RCCP_AIConfig.FrictionCurveConfig(),
            front = new RCCP_AIConfig.AxleWheelConfig {
                wheelWidth = float.NaN,
                camber = float.NaN,
                caster = float.NaN,
                grip = float.NaN
            },
            rear = new RCCP_AIConfig.AxleWheelConfig {
                wheelWidth = float.NaN,
                camber = float.NaN,
                caster = float.NaN,
                grip = float.NaN
            }
        };
    }

    #endregion

    #endregion

    #region Per-Vehicle Behavior

    /// <summary>
    /// Sets the per-vehicle behavior preset by name.
    /// </summary>
    /// <param name="carController">The vehicle to configure</param>
    /// <param name="behaviorName">Behavior preset name (e.g., "Drift", "Racing").
    /// Use "global" or empty string to clear custom behavior and use global.</param>
    public static void ApplyVehicleBehavior(RCCP_CarController carController, string behaviorName) {
        if (carController == null) {
            Debug.LogError("[RCCP AI] Cannot apply behavior: CarController is null");
            return;
        }

        Undo.RecordObject(carController, "RCCP AI Vehicle Behavior");

        if (string.IsNullOrEmpty(behaviorName) ||
            string.Equals(behaviorName, "global", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(behaviorName, "none", System.StringComparison.OrdinalIgnoreCase)) {
            // Clear custom behavior, use global
#if RCCP_V2_2_OR_NEWER
            carController.useCustomBehavior = false;
            carController.customBehaviorIndex = -1;
#endif
            if (VerboseLogging) Debug.Log("[RCCP AI] Vehicle behavior set to: Global (using RCCP_Settings preset)");
        } else {
            // Find behavior by name
#if RCCP_V2_2_OR_NEWER
            int index = global::RCCP.GetBehaviorIndexByName(behaviorName);
            if (index >= 0) {
                carController.useCustomBehavior = true;
                carController.customBehaviorIndex = index;
                if (VerboseLogging) {
                    string actualName = RCCP_Settings.Instance?.behaviorTypes?[index]?.behaviorName ?? "Unknown";
                    Debug.Log($"[RCCP AI] Vehicle behavior set to: {actualName} (index {index})");
                }
            } else {
                Debug.LogWarning($"[RCCP AI] Behavior preset '{behaviorName}' not found. Available presets: {GetAvailableBehaviorNames()}");
                return;
            }
#else
            int index = GetBehaviorIndexByNameFallback(behaviorName);
            if (index >= 0) {
                if (VerboseLogging) {
                    string actualName = RCCP_Settings.Instance?.behaviorTypes?[index]?.behaviorName ?? "Unknown";
                    Debug.Log($"[RCCP AI] Behavior preset found: {actualName} (index {index}) - Note: V2.0 does not support per-vehicle behavior");
                }
            } else {
                Debug.LogWarning($"[RCCP AI] Behavior preset '{behaviorName}' not found. Available presets: {GetAvailableBehaviorNames()}");
                return;
            }
#endif
        }

        EditorUtility.SetDirty(carController);
    }

    /// <summary>
    /// Gets a comma-separated list of available behavior preset names.
    /// </summary>
    private static string GetAvailableBehaviorNames() {
        var settings = RCCP_Settings.Instance;
        if (settings?.behaviorTypes == null || settings.behaviorTypes.Length == 0)
            return "None";

        var names = new System.Collections.Generic.List<string>();
        foreach (var bt in settings.behaviorTypes) {
            if (bt != null && !string.IsNullOrEmpty(bt.behaviorName))
                names.Add(bt.behaviorName);
        }
        return names.Count > 0 ? string.Join(", ", names) : "None";
    }

#if !RCCP_V2_2_OR_NEWER
    /// <summary>
    /// Fallback method for finding behavior index by name (V2.0 compatibility).
    /// RCCP V2.2+ has RCCP.GetBehaviorIndexByName() but V2.0 does not.
    /// </summary>
    /// <param name="behaviorName">Name of the behavior preset to find</param>
    /// <returns>Index of the behavior, or -1 if not found</returns>
    private static int GetBehaviorIndexByNameFallback(string behaviorName) {
        var settings = RCCP_Settings.Instance;
        if (settings?.behaviorTypes == null || settings.behaviorTypes.Length == 0)
            return -1;

        for (int i = 0; i < settings.behaviorTypes.Length; i++) {
            var bt = settings.behaviorTypes[i];
            if (bt != null && string.Equals(bt.behaviorName, behaviorName, System.StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }

        return -1;
    }
#endif

    #endregion
}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
