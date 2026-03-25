//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Centralized cache for RCCP scene data and vehicles.
/// Optimized for performance with lazy loading and smart caching.
/// </summary>
public static class RCCP_SceneDataCache {

    #region Cached Data

    // Vehicle references.
    private static List<RCCP_CarController> cachedVehicles = new List<RCCP_CarController>();

    // Component collections per vehicle.
    private static Dictionary<RCCP_CarController, VehicleComponentStatus> vehicleComponentStatus = new Dictionary<RCCP_CarController, VehicleComponentStatus>();

    // Statistics.
    private static SceneStatistics currentStatistics;

    // Cache validity.
    private static bool isCacheDirty = true;
    private static double lastCacheTime;
    private static Scene lastCachedScene;
    private const double CACHE_LIFETIME = 1.0; // 1 second cache lifetime.

    // Lifecycle tracking.
    private static int usageCount;
    private static bool isInitialized;

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the cache system.
    /// </summary>
    public static void Initialize() {

        usageCount++;

        if (isInitialized)
            return;

        EditorApplication.hierarchyChanged += MarkCacheDirty;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        isInitialized = true;
        MarkCacheDirty();

    }

    /// <summary>
    /// Updates the cache if needed.
    /// </summary>
    public static void Update() {

        Scene currentScene = EditorSceneManager.GetActiveScene();

        if (isCacheDirty || lastCachedScene != currentScene ||
            (EditorApplication.timeSinceStartup - lastCacheTime) > CACHE_LIFETIME) {

            RefreshCache();

        }

    }

    /// <summary>
    /// Forces cache refresh.
    /// </summary>
    public static void ForceRefresh() {

        RefreshCache();

    }

    /// <summary>
    /// Gets all cached vehicles.
    /// </summary>
    public static List<RCCP_CarController> GetVehicles(string filter = "") {

        Update();

        if (string.IsNullOrEmpty(filter))
            return new List<RCCP_CarController>(cachedVehicles);

        string lowerFilter = filter.ToLower();
        return cachedVehicles.Where(v => v != null &&
            v.gameObject.name.ToLower().Contains(lowerFilter)
        ).ToList();

    }

    /// <summary>
    /// Gets component status for a specific vehicle.
    /// </summary>
    public static VehicleComponentStatus GetVehicleComponentStatus(RCCP_CarController vehicle) {

        Update();

        if (vehicle != null && vehicleComponentStatus.TryGetValue(vehicle, out VehicleComponentStatus status)) {
            return status;
        }

        return new VehicleComponentStatus();

    }

    /// <summary>
    /// Gets scene statistics.
    /// </summary>
    public static SceneStatistics GetStatistics() {

        Update();
        return currentStatistics;

    }

    /// <summary>
    /// Cleanup the cache system.
    /// </summary>
    public static void Cleanup() {

        if (usageCount > 0)
            usageCount--;

        if (usageCount > 0)
            return;

        if (!isInitialized)
            return;

        EditorApplication.hierarchyChanged -= MarkCacheDirty;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        isInitialized = false;
        ClearCache();

    }

    #endregion

    #region Private Methods

    private static void RefreshCache() {

        // Cache vehicles.
        cachedVehicles = Object.FindObjectsByType<RCCP_CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();

        // Cache component status for each vehicle.
        vehicleComponentStatus.Clear();
        foreach (var vehicle in cachedVehicles) {

            if (vehicle == null) continue;

            var status = new VehicleComponentStatus();

            // Check required components.
            status.hasEngine = vehicle.GetComponentInChildren<RCCP_Engine>(true) != null;
            status.hasGearbox = vehicle.GetComponentInChildren<RCCP_Gearbox>(true) != null;
            status.hasClutch = vehicle.GetComponentInChildren<RCCP_Clutch>(true) != null;
            status.hasDifferential = vehicle.GetComponentInChildren<RCCP_Differential>(true) != null;
            status.hasAxles = vehicle.GetComponentInChildren<RCCP_Axles>(true) != null;
            status.hasInput = vehicle.GetComponentInChildren<RCCP_Input>(true) != null;

            // Check optional components.
            status.hasAudio = vehicle.GetComponentInChildren<RCCP_Audio>(true) != null;
            status.hasLights = vehicle.GetComponentInChildren<RCCP_Lights>(true) != null;
            status.hasStability = vehicle.GetComponentInChildren<RCCP_Stability>(true) != null;
            status.hasDamage = vehicle.GetComponentInChildren<RCCP_Damage>(true) != null;
            status.hasOtherAddons = vehicle.GetComponentInChildren<RCCP_OtherAddons>(true) != null;

            // Count components.
            status.totalRequiredComponents = 6;
            status.activeRequiredComponents = 0;
            if (status.hasEngine) status.activeRequiredComponents++;
            if (status.hasGearbox) status.activeRequiredComponents++;
            if (status.hasClutch) status.activeRequiredComponents++;
            if (status.hasDifferential) status.activeRequiredComponents++;
            if (status.hasAxles) status.activeRequiredComponents++;
            if (status.hasInput) status.activeRequiredComponents++;

            status.totalOptionalComponents = 5;
            status.activeOptionalComponents = 0;
            if (status.hasAudio) status.activeOptionalComponents++;
            if (status.hasLights) status.activeOptionalComponents++;
            if (status.hasStability) status.activeOptionalComponents++;
            if (status.hasDamage) status.activeOptionalComponents++;
            if (status.hasOtherAddons) status.activeOptionalComponents++;

            vehicleComponentStatus[vehicle] = status;

        }

        // Update statistics.
        UpdateStatistics();

        // Update cache state.
        isCacheDirty = false;
        lastCacheTime = EditorApplication.timeSinceStartup;
        lastCachedScene = EditorSceneManager.GetActiveScene();

    }

    private static void UpdateStatistics() {

        int fullyConfigured = 0;
        int partiallyConfigured = 0;
        int missingRequired = 0;

        foreach (var kvp in vehicleComponentStatus) {

            var status = kvp.Value;

            if (status.activeRequiredComponents == status.totalRequiredComponents) {
                fullyConfigured++;
            } else if (status.activeRequiredComponents > 0) {
                partiallyConfigured++;
            } else {
                missingRequired++;
            }

        }

        currentStatistics = new SceneStatistics {

            totalVehicles = cachedVehicles.Count,
            fullyConfiguredVehicles = fullyConfigured,
            partiallyConfiguredVehicles = partiallyConfigured,
            vehiclesWithMissingRequired = missingRequired,
            hasRCCPSettings = RCCP_Settings.Instance != null,
            hasRCCPSceneManager = Object.FindFirstObjectByType<RCCP_SceneManager>(FindObjectsInactive.Include) != null

        };

    }

    private static void ClearCache() {

        cachedVehicles.Clear();
        vehicleComponentStatus.Clear();
        isCacheDirty = true;

    }

    private static void MarkCacheDirty() {

        isCacheDirty = true;

    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) {

        MarkCacheDirty();

    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state) {

        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) {

            MarkCacheDirty();

        }

    }

    #endregion

    #region Data Structures

    /// <summary>
    /// Structure holding component status for a vehicle.
    /// </summary>
    public struct VehicleComponentStatus {

        // Required components.
        public bool hasEngine;
        public bool hasGearbox;
        public bool hasClutch;
        public bool hasDifferential;
        public bool hasAxles;
        public bool hasInput;

        // Optional components.
        public bool hasAudio;
        public bool hasLights;
        public bool hasStability;
        public bool hasDamage;
        public bool hasOtherAddons;

        // Counts.
        public int totalRequiredComponents;
        public int activeRequiredComponents;
        public int totalOptionalComponents;
        public int activeOptionalComponents;

        public int TotalComponents => totalRequiredComponents + totalOptionalComponents;
        public int ActiveComponents => activeRequiredComponents + activeOptionalComponents;

        public bool IsFullyConfigured => activeRequiredComponents == totalRequiredComponents;
        public bool HasCriticalIssues => activeRequiredComponents < totalRequiredComponents;

    }

    /// <summary>
    /// Structure holding scene statistics.
    /// </summary>
    public struct SceneStatistics {

        public int totalVehicles;
        public int fullyConfiguredVehicles;
        public int partiallyConfiguredVehicles;
        public int vehiclesWithMissingRequired;
        public bool hasRCCPSettings;
        public bool hasRCCPSceneManager;

    }

    #endregion

}

#endif
