//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;

/// <summary>
/// Provides runtime-cloned instances of RCCP ScriptableObject settings.
/// This allows modifications to settings at runtime without affecting the original assets.
/// Use Clear() to reset the cloned instances when changing scenes if needed.
/// </summary>
public static class RCCP_RuntimeSettings {

    private static RCCP_Settings _runtimeInstance;

    /// <summary>
    /// Gets a runtime clone of RCCP_Settings. Creates the clone on first access.
    /// </summary>
    public static RCCP_Settings RCCPSettingsInstance {

        get {

            if (_runtimeInstance == null)
                _runtimeInstance = ScriptableObject.Instantiate(RCCP_Settings.Instance);

            return _runtimeInstance;

        }

    }

    /// <summary>
    /// Gets a runtime clone of RCCP_GroundMaterials. Creates the clone on first access.
    /// </summary>
    public static RCCP_GroundMaterials RCCPGroundMaterialsInstance {

        get {

            if (_RCCPGroundMaterials == null)
                _RCCPGroundMaterials = ScriptableObject.Instantiate(RCCP_GroundMaterials.Instance);

            return _RCCPGroundMaterials;

        }

    }
    private static RCCP_GroundMaterials _RCCPGroundMaterials;

    /// <summary>
    /// Gets a runtime clone of RCCP_ChangableWheels. Creates the clone on first access.
    /// </summary>
    public static RCCP_ChangableWheels RCCPChangableWheelsInstance {

        get {

            if (_RCCPChangableWheels == null)
                _RCCPChangableWheels = ScriptableObject.Instantiate(RCCP_ChangableWheels.Instance);

            return _RCCPChangableWheels;

        }

    }
    private static RCCP_ChangableWheels _RCCPChangableWheels;

    /// <summary>
    /// Clears all runtime-cloned instances. Call this when changing scenes if you want fresh clones.
    /// </summary>
    public static void Clear() {

        _runtimeInstance = null;
        _RCCPGroundMaterials = null;
        _RCCPChangableWheels = null;

    }

}
