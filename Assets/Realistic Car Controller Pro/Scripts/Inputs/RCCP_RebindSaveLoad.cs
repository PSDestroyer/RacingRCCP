//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.InputSystem;
using HalvaStudio.Save;

[System.Serializable]
public class RCCP_RebindSaveLoad {

    public static void Save() {

        InputActionAsset actions = RCCP_InputActions.Instance.inputActions;
        if (actions == null || SaveManager.Instance == null)
            return;

        var rebinds = actions.SaveBindingOverridesAsJson();
        SaveManager.Instance.saveData.inputRebindsJson = rebinds;
        SaveManager.Instance.Save();

    }

    public static void Load() {

        InputActionAsset actions = RCCP_InputActions.Instance.inputActions;
        if (actions == null || SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        var rebinds = SaveManager.Instance.saveData.inputRebindsJson;

        if (!string.IsNullOrEmpty(rebinds))
            actions.LoadBindingOverridesFromJson(rebinds);

    }

}
