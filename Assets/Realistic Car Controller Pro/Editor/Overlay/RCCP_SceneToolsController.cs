//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Shared controller logic for RCCP Scene Tools (Overlay and Window).
/// Handles state management, data caching, and content provider lifecycle.
/// </summary>
public class RCCP_SceneToolsController {

    #region Variables

    // Tab system.
    private int currentTabIndex = 0;
    private string[] tabNames;

    // Content providers.
    private Dictionary<string, IRCCP_OverlayContent> contentProviders;

    // Search and filter.
    private string currentSearchQuery = "";
    private List<string> searchHistory = new List<string>();

    // Update timer.
    private double lastUpdateTime;
    private const double UPDATE_INTERVAL = 0.5;

    // Session Key Prefix (to separate Overlay vs Window state if needed).
    private readonly string sessionKeyPrefix;

    // AI Assistant detection cache.
    private static bool? aiAssistantInstalledCache;

    #endregion

    #region Properties

    /// <summary>
    /// Checks if RCCP AI Assistant package is installed.
    /// </summary>
    public static bool IsAIAssistantInstalled {
        get {
            if (!aiAssistantInstalledCache.HasValue) {
                aiAssistantInstalledCache = System.Type.GetType("BoneCrackerGames.RCCP.AIAssistant.RCCP_AIAssistantWindow, Assembly-CSharp-Editor") != null;
            }
            return aiAssistantInstalledCache.Value;
        }
    }

    public string[] TabNames => tabNames;

    public int CurrentTabIndex {
        get => currentTabIndex;
        set {
            if (currentTabIndex != value) {
                currentTabIndex = value;
                SessionState.SetInt($"{sessionKeyPrefix}_CurrentTab", currentTabIndex);
            }
        }
    }

    public string CurrentSearchQuery {
        get => currentSearchQuery;
        set => currentSearchQuery = value;
    }

    public string CurrentTabName => (currentTabIndex >= 0 && currentTabIndex < tabNames.Length) ? tabNames[currentTabIndex] : "";

    #endregion

    #region Initialization

    public RCCP_SceneToolsController(string sessionKeyPrefix) {

        this.sessionKeyPrefix = sessionKeyPrefix;
        InitializeContentProviders();

        // Load saved state.
        currentTabIndex = SessionState.GetInt($"{sessionKeyPrefix}_CurrentTab", 0);

        // Validate tab index.
        if (currentTabIndex >= tabNames.Length) {
            currentTabIndex = 0;
        }

        // Initialize data cache.
        RCCP_SceneDataCache.Initialize();

    }

    private void InitializeContentProviders() {

        contentProviders = new Dictionary<string, IRCCP_OverlayContent>();
        var tabNamesList = new List<string>();

        // Always add core tabs.
        contentProviders.Add("Vehicles", new RCCP_VehiclesTab());
        tabNamesList.Add("Vehicles");

        contentProviders.Add("Diagnostics", new RCCP_DiagnosticsTab());
        tabNamesList.Add("Diagnostics");

        // Conditionally add AI Assistant tab.
        if (IsAIAssistantInstalled) {
            contentProviders.Add("AI Assistant", new RCCP_AIAssistantTab());
            tabNamesList.Add("AI Assistant");
        }

        // Always add Settings tab.
        contentProviders.Add("Settings", new RCCP_SettingsTab());
        tabNamesList.Add("Settings");

        tabNames = tabNamesList.ToArray();

    }

    #endregion

    #region Public Methods

    public IRCCP_OverlayContent GetCurrentContentProvider() {

        if (contentProviders.TryGetValue(CurrentTabName, out IRCCP_OverlayContent provider)) {
            return provider;
        }
        return null;

    }

    public void ClearSearch() {
        currentSearchQuery = "";
    }

    public void AddToSearchHistory(string query) {

        if (!string.IsNullOrEmpty(query) && !searchHistory.Contains(query)) {
            searchHistory.Insert(0, query);
            if (searchHistory.Count > 10)
                searchHistory.RemoveAt(searchHistory.Count - 1);
        }

    }

    public void Update() {

        // Throttle updates.
        if (EditorApplication.timeSinceStartup - lastUpdateTime < UPDATE_INTERVAL)
            return;

        lastUpdateTime = EditorApplication.timeSinceStartup;

        // Update data cache.
        RCCP_SceneDataCache.Update();

        // Refresh current tab if needed.
        if (contentProviders != null && contentProviders.TryGetValue(CurrentTabName, out IRCCP_OverlayContent provider)) {
            provider.OnUpdate();
        }

    }

    public void Cleanup() {

        if (contentProviders != null) {
            foreach (var provider in contentProviders.Values) {
                provider.OnDestroy();
            }
            contentProviders.Clear();
        }

        RCCP_SceneDataCache.Cleanup();

    }

    /// <summary>
    /// Invalidates the AI Assistant detection cache.
    /// Call this if the AI Assistant package is installed/uninstalled at runtime.
    /// </summary>
    public static void InvalidateAIAssistantCache() {
        aiAssistantInstalledCache = null;
    }

    #endregion

}

#endif
