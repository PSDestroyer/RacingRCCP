//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class RCCP_UI_CustomizationActionButton {

    private static Button cachedButton;
    private static TMP_Text cachedLabel;

    public static void RefreshForSelection(Button sourceButton, bool isPurchased, bool isEquipped) {

        if (!EnsureReferences())
            return;

        cachedButton.onClick.RemoveAllListeners();

        if (isEquipped) {
            cachedButton.interactable = false;
            SetLabel(string.Empty);
            return;
        }

        if (sourceButton != null)
            cachedButton.onClick.AddListener(new UnityAction(sourceButton.onClick.Invoke));

        cachedButton.interactable = true;
        SetLabel(isPurchased ? "Select" : "Buy");

    }

    public static void Clear() {

        if (!EnsureReferences())
            return;

        cachedButton.onClick.RemoveAllListeners();
        cachedButton.interactable = false;
        SetLabel(string.Empty);

    }

    private static bool EnsureReferences() {

        if (!cachedButton) {
            GameObject buttonObject = GameObject.Find("Select BUt");

            if (buttonObject)
                cachedButton = buttonObject.GetComponent<Button>();
        }

        if (cachedButton && !cachedLabel)
            cachedLabel = cachedButton.GetComponentInChildren<TMP_Text>(true);

        return cachedButton && cachedLabel;

    }

    private static void SetLabel(string label) {

        if (!cachedLabel)
            return;

        cachedLabel.text = label;
        cachedLabel.havePropertiesChanged = true;
        cachedLabel.ForceMeshUpdate(true, true);

    }

}
