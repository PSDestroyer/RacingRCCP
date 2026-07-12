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
using Unity.VisualScripting;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// UI spoiler button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Spoiler Button")]
public class RCCP_UI_Spoiler : RCCP_UIComponent , ISelectHandler{

    /// <summary>
    /// Index of the target spoiler.
    /// </summary>
    [Min(0)] public int index = 0;
    [Min(0)] public int price = 50;
    [SerializeField] private YesNo _yesNo;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private MoneyManager _moneyManager;
    [SerializeField] private List<RCCP_UI_Spoiler> Comp = new List<RCCP_UI_Spoiler>();

    public void OnSelect(BaseEventData baseEventData)
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.SpoilerManager.UpgradeWithoutSave(index);
        UpdateActionButton(playerVehicle);

    }

    private void OnDisable()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.SpoilerManager.Initialize();
        RCCP_UI_CustomizationActionButton.Clear();
    }

    public void OnClick() {

        YesNo();

    }
    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    public void Refresh()
    {
        foreach (var VARIABLE in Comp)
        {
            VARIABLE.Initialize();
        }
    }

    private void Initialize()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.SpoilerManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();

        if (playerVehicle.Customizer.SpoilerManager.spoilerIndex == index)
            RCCP_UI_PriceLabelUtility.SetEquipped(priceText, "In Use");
        else if (loadout.IsSpoilerPurchased(index))
            RCCP_UI_PriceLabelUtility.SetPurchased(priceText, "Owned");
        else
            RCCP_UI_PriceLabelUtility.SetPrice(priceText, price);


    }

    private void UpdateActionButton(RCCP_CarController playerVehicle) {

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.SpoilerManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
        bool isEquipped = playerVehicle.Customizer.SpoilerManager.spoilerIndex == index;
        bool isPurchased = loadout.IsSpoilerPurchased(index);
        RCCP_UI_CustomizationActionButton.RefreshForSelection(GetComponent<Button>(), isPurchased, isEquipped);

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

        if (!playerVehicle.Customizer.SpoilerManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();

        if (playerVehicle.Customizer.SpoilerManager.spoilerIndex == index)
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
            _yesNo.Notify(operation.Result);
            SoundManager.Instance.PlayButtonClick();
            UpdateActionButton(playerVehicle);
            return;
        }

        if (loadout.IsSpoilerPurchased(index))
        {
            SoundManager.Instance.PlayButtonClick();
            playerVehicle.Customizer.SpoilerManager.Upgrade(index);
            Refresh();
            UpdateActionButton(playerVehicle);
            GetComponent<Button>().Select();
            return;
        }

        if (playerVehicle.Customizer.SpoilerManager.spoilerIndex != index)
        {
            var buystring = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "BuyYes/No");

            bool result = await _yesNo.ShowYesNoPanelAsync(buystring.Result + "?");

            if (result)
            {
                if (SaveManager.Instance.saveData.money >= price)
                {
                    _moneyManager.MoneyToTake(price);
                    SoundManager.Instance.PlayButtonClick();
                    loadout.MarkSpoilerPurchased(index);
                    playerVehicle.Customizer.SpoilerManager.Upgrade(index);
                    Refresh();
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
