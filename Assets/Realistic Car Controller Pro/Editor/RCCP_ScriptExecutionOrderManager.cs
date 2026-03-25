//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright (c) 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Manages script execution order for RCCP scripts. Automatically sets up correct execution order on import.
/// </summary>
[InitializeOnLoad]
public class RCCP_ScriptExecutionOrderManager {

    /// <summary>
    /// Script execution order definitions. Negative values execute earlier, positive values execute later.
    ///
    /// TIMING HIERARCHY:
    /// -50: Singletons (SceneManager, InputManager) - must exist before anything accesses them
    /// -10: RCCP_CarController - main controller, initializes all components
    /// -5:  Parent containers (Axles, Lights, OtherAddons) - must register before children
    /// 0:   Default - all other components
    /// 5:   Camera - needs vehicle state ready
    /// 10:  Late components (Customizer, Lod, BodyTilt) - run after everything
    /// </summary>
    private static readonly Dictionary<string, int> ExecutionOrders = new Dictionary<string, int>() {

        // === SINGLETONS (-50) ===
        // These must be ready before ANY component tries to access them
        { "RCCP_SceneManager", -50 },
        { "RCCP_InputManager", -50 },
        { "RCCP_SkidmarksManager", -50 },

        // === CORE CONTROLLER (-10) ===
        // Main vehicle controller - initializes all child components via GetAllComponents()
        { "RCCP_CarController", -10 },

        // === PARENT CONTAINERS (-5) ===
        // These must be registered with CarController BEFORE their child components
        // Otherwise child components get disabled in Register() method
        { "RCCP_OtherAddons", -5 },      // Parent of: AI, Nos, Recorder, FuelTank, Exhausts, etc.
        { "RCCP_Axles", -5 },            // Parent of: RCCP_Axle components
        { "RCCP_Lights", -5 },           // Parent of: RCCP_Light components
        { "RCCP_Exhausts", -5 },         // Parent of: RCCP_Exhaust components

        // === CAMERA (5) ===
        // Slightly late to ensure vehicle state is fully ready
        { "RCCP_Camera", 5 },

        // === LATE EXECUTION (10) ===
        // These run after all core systems are ready
        { "RCCP_Customizer", 10 },       // Needs all systems ready for customization
        { "RCCP_Lod", 10 },              // Level of detail - runs after rendering setup
        { "RCCP_BodyTilt", 10 },         // Visual effect - runs late
    };

    /// <summary>
    /// Static constructor - called when Unity loads assemblies.
    /// </summary>
    static RCCP_ScriptExecutionOrderManager() {

        // Delay execution to ensure all scripts are loaded
        EditorApplication.delayCall += OnDelayedInit;

    }

    /// <summary>
    /// Delayed initialization to ensure Unity is fully ready.
    /// </summary>
    private static void OnDelayedInit() {

        EditorApplication.delayCall -= OnDelayedInit;
        ValidateExecutionOrders();

    }

    /// <summary>
    /// Validates and sets execution orders for all RCCP scripts.
    /// </summary>
    [MenuItem("Tools/BCG/RCCP/Validate Script Execution Order", false, 1000)]
    public static void ValidateExecutionOrders() {

        bool anyChanged = false;

        foreach (var kvp in ExecutionOrders) {

            string scriptName = kvp.Key;
            int targetOrder = kvp.Value;

            MonoScript script = FindScript(scriptName);

            if (script == null) {
                // Script not found - might not be installed yet
                continue;
            }

            int currentOrder = MonoImporter.GetExecutionOrder(script);

            if (currentOrder != targetOrder) {

                MonoImporter.SetExecutionOrder(script, targetOrder);
                anyChanged = true;
                Debug.Log($"[RCCP] Set execution order for {scriptName}: {targetOrder}");

            }

        }

        if (anyChanged)
            Debug.Log("[RCCP] Script execution order validated successfully.");

    }

    /// <summary>
    /// Resets all RCCP script execution orders to default (0).
    /// </summary>
    [MenuItem("Tools/BCG/RCCP/Reset Script Execution Order", false, 1001)]
    public static void ResetExecutionOrders() {

        foreach (var kvp in ExecutionOrders) {

            string scriptName = kvp.Key;
            MonoScript script = FindScript(scriptName);

            if (script == null)
                continue;

            int currentOrder = MonoImporter.GetExecutionOrder(script);

            if (currentOrder != 0) {

                MonoImporter.SetExecutionOrder(script, 0);
                Debug.Log($"[RCCP] Reset execution order for {scriptName} to 0");

            }

        }

        Debug.Log("[RCCP] All script execution orders have been reset to default.");

    }

    /// <summary>
    /// Shows current execution orders in the console.
    /// </summary>
    [MenuItem("Tools/BCG/RCCP/Show Script Execution Order", false, 1002)]
    public static void ShowExecutionOrders() {

        Debug.Log("[RCCP] Current Script Execution Orders:");

        foreach (var kvp in ExecutionOrders) {

            string scriptName = kvp.Key;
            int targetOrder = kvp.Value;

            MonoScript script = FindScript(scriptName);

            if (script == null) {
                Debug.Log($"  {scriptName}: NOT FOUND");
                continue;
            }

            int currentOrder = MonoImporter.GetExecutionOrder(script);
            string status = currentOrder == targetOrder ? "OK" : $"MISMATCH (expected {targetOrder})";
            Debug.Log($"  {scriptName}: {currentOrder} [{status}]");

        }

    }

    /// <summary>
    /// Finds a MonoScript by class name.
    /// </summary>
    /// <param name="className">Name of the class to find.</param>
    /// <returns>MonoScript asset if found, null otherwise.</returns>
    private static MonoScript FindScript(string className) {

        // Search for script assets matching the class name
        string[] guids = AssetDatabase.FindAssets($"t:MonoScript {className}");

        foreach (string guid in guids) {

            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

            if (script != null && script.name == className)
                return script;

        }

        return null;

    }

}
#endif
