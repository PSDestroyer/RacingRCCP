//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Upgrades engine of the car controller.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Engine")]
public class RCCP_VehicleUpgrade_Engine : RCCP_Component {

    private int _engineLevel = 0;

    /// <summary>
    /// Current engine level. Maximum is 5.
    /// </summary>
    public int EngineLevel {
        get {
            return _engineLevel;
        }
        set {
            if (value <= 5)
                _engineLevel = value;
        }
    }

    /// <summary>
    /// Default engine torque.
    /// </summary>
    [HideInInspector] public float defEngine = -1f;

    /// <summary>
    /// Efficiency of the upgrade.
    /// </summary>
    [Range(1f, 2f)] public float efficiency = 1.15f;

    /// <summary>
    /// Engine level that unlocks turbo for non-turbo vehicles. Set to 0 to disable.
    /// </summary>
    [Range(0, 5)] public int turboUnlockLevel = 4;

    /// <summary>
    /// Default turbo state of the vehicle.
    /// </summary>
    [HideInInspector] public bool defTurboCharged;

    private bool turboStateCached = false;

    private readonly float[] levelMultipliers = { 1f, 1.04f, 1.08f, 1.13f, 1.18f, 1.25f };

    /// <summary>
    /// Updates engine torque and initializes it.
    /// </summary>
    public void Initialize() {

        if (!CarController.Engine) {

            Debug.LogError("Engine couldn't found in the vehicle. RCCP_VehicleUpgrade_Engine needs it to upgrade the engine level");
            enabled = false;
            return;

        }

        if (defEngine <= 0)
            defEngine = CarController.Engine.maximumTorqueAsNM;

        CarController.Engine.maximumTorqueAsNM = defEngine * GetLevelMultiplier();
        ApplyTurboState();

    }

    /// <summary>
    /// Updates engine torque and save it.
    /// </summary>
    public void UpdateStats() {

        if (!CarController.Engine) {

            Debug.LogError("Engine couldn't found in the vehicle. RCCP_VehicleUpgrade_Engine needs it to upgrade the engine level");
            enabled = false;
            return;

        }

        if (defEngine <= 0)
            defEngine = CarController.Engine.maximumTorqueAsNM;

        CarController.Engine.maximumTorqueAsNM = defEngine * GetLevelMultiplier();
        ApplyTurboState();

    }

    private void Update() {

        if (!CarController.Engine) {

            Debug.LogError("Engine couldn't found in the vehicle. RCCP_VehicleUpgrade_Engine needs it to upgrade the engine level");
            enabled = false;
            return;

        }

    }

    public void Restore() {

        EngineLevel = 0;

        if (defEngine <= 0)
            defEngine = CarController.Engine.maximumTorqueAsNM;

        CarController.Engine.maximumTorqueAsNM = defEngine;
        ApplyTurboState();

    }

    private float GetLevelMultiplier() {

        return levelMultipliers[Mathf.Clamp(EngineLevel, 0, levelMultipliers.Length - 1)];

    }

    private void ApplyTurboState() {

        if (!CarController.Engine)
            return;

        CacheDefaultTurboState();

        if (turboUnlockLevel <= 0)
            CarController.Engine.turboCharged = defTurboCharged;
        else
            CarController.Engine.turboCharged = defTurboCharged || EngineLevel >= turboUnlockLevel;

    }

    private void CacheDefaultTurboState() {

        if (turboStateCached)
            return;

        defTurboCharged = CarController.Engine.turboCharged;
        turboStateCached = true;

    }

}
