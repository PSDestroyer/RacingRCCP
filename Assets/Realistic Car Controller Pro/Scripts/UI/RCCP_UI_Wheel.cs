//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// UI change wheel button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Wheel Button")]
public class RCCP_UI_Wheel : RCCP_UIComponent, ISelectHandler {

    /// <summary>
    /// Index of the target wheel. 
    /// </summary>
    [Min(0)] public int wheelIndex = 0;
    [Min(0)] public int price = 50;
    [SerializeField] private YesNo _yesNo;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private MoneyManager _moneyManager;

    public void OnSelect(BaseEventData baseEventData)
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.WheelManager.UpdateWheelWithoutSave(wheelIndex);
        UpdateActionButton(playerVehicle);

    }
    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }
    private void OnDisable()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.WheelManager.Initialize();
        RCCP_UI_CustomizationActionButton.Clear();
    }

    public void OnClick() {

        YesNo();

    }

    private void Initialize()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.WheelManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();

        if (playerVehicle.Customizer.WheelManager.wheelIndex == wheelIndex)
            RCCP_UI_PriceLabelUtility.SetEquipped(priceText, "In Use");
        else if (loadout.IsWheelPurchased(wheelIndex))
            RCCP_UI_PriceLabelUtility.SetPurchased(priceText, "Owned");
        else
            RCCP_UI_PriceLabelUtility.SetPrice(priceText, price);
    }

    private void UpdateActionButton(RCCP_CarController playerVehicle) {

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.WheelManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
        bool isEquipped = playerVehicle.Customizer.WheelManager.wheelIndex == wheelIndex;
        bool isPurchased = loadout.IsWheelPurchased(wheelIndex);
        RCCP_UI_CustomizationActionButton.RefreshForSelection(GetComponent<Button>(), isPurchased, isEquipped);

    }

    private void RefreshWheelButtons()
    {
        Transform root = transform.parent ? transform.parent : transform;
        RCCP_UI_Wheel[] wheels = root.GetComponentsInChildren<RCCP_UI_Wheel>(true);

        foreach (RCCP_UI_Wheel wheel in wheels)
        {
            if (wheel)
                wheel.Initialize();
        }
    }

    public async void YesNo()
    {
        //  Finding the player vehicle.
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        //  If no player vehicle found, return.
        if (!playerVehicle)
            return;

        //  If player vehicle doesn't have the customizer component, return.
        if (!playerVehicle.Customizer)
            return;

        if (!playerVehicle.Customizer.WheelManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();

        if (playerVehicle.Customizer.WheelManager.wheelIndex == wheelIndex)
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
            _yesNo.Notify(operation.Result);
            SoundManager.Instance.PlayButtonClick();
            UpdateActionButton(playerVehicle);
            return;
        }

        if (loadout.IsWheelPurchased(wheelIndex))
        {
            SoundManager.Instance.PlayButtonClick();
            playerVehicle.Customizer.WheelManager.UpdateWheel(wheelIndex);
            RefreshWheelButtons();
            UpdateActionButton(playerVehicle);
            GetComponent<Button>().Select();
            return;
        }

        if (playerVehicle.Customizer.WheelManager.wheelIndex != wheelIndex)
        {
            var buystring = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "BuyYes/No");

            bool result = await _yesNo.ShowYesNoPanelAsync(buystring.Result + "?");

            if (result)
            {
                if (SaveManager.Instance.saveData.money >= price)
                {
                    _moneyManager.MoneyToTake(price);
                    SoundManager.Instance.PlayButtonClick();
                    loadout.MarkWheelPurchased(wheelIndex);
                    playerVehicle.Customizer.WheelManager.UpdateWheel(wheelIndex);
                    RefreshWheelButtons();
                    UpdateActionButton(playerVehicle);
                }
                else
                {
                    var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "No money");
                    _yesNo.Notify(operation.Result);
                    SoundManager.Instance.PlayButtonError();
                    Debug.Log("dont have enought Money");
                }

                GetComponent<Button>().Select();

            }
            else
            {
                UpdateActionButton(playerVehicle);
                GetComponent<Button>().Select();
                SoundManager.Instance.PlayButtonClick();
            }
        }
    }
}
