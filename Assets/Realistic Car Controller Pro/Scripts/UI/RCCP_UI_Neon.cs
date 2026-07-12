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
using HalvaStudio.Save;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// UI neon button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Neon Button")]
public class RCCP_UI_Neon : RCCP_UIComponent ,ISelectHandler
    {

    /// <summary>
    /// Target material.
    /// </summary>
    public Material material;
    [Min(0)] public int price = 50;
    [SerializeField] private YesNo _yesNo;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private MoneyManager _moneyManager;
    [SerializeField] private List<RCCP_UI_Neon> _neons = new List<RCCP_UI_Neon>();
    public void OnSelect(BaseEventData baseEventData)
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.NeonManager.UpgradeWithoutSave(material);
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

    public void Refresh()
    {
        foreach (var VARIABLE in _neons)
        {
            VARIABLE.Initialize();
        }
    }
    private void Initialize() 
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.NeonManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
        int materialIndex = playerVehicle.Customizer.NeonManager.FindMaterialIndex(material);

        if (materialIndex == playerVehicle.Customizer.NeonManager.index)
            RCCP_UI_PriceLabelUtility.SetEquipped(priceText, "In Use");
        else if (loadout.IsNeonPurchased(materialIndex))
            RCCP_UI_PriceLabelUtility.SetPurchased(priceText, "Owned");
        else
            RCCP_UI_PriceLabelUtility.SetPrice(priceText, price);
    }
    private void OnDisable()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.NeonManager.Initialize();
        RCCP_UI_CustomizationActionButton.Clear();
    }

    private void UpdateActionButton(RCCP_CarController playerVehicle) {

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.NeonManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
        int materialIndex = playerVehicle.Customizer.NeonManager.FindMaterialIndex(material);
        bool isEquipped = materialIndex == playerVehicle.Customizer.NeonManager.index;
        bool isPurchased = loadout.IsNeonPurchased(materialIndex);
        RCCP_UI_CustomizationActionButton.RefreshForSelection(GetComponent<Button>(), isPurchased, isEquipped);

    }

    public void Upgrade() {

        YesNo();

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

        if (!playerVehicle.Customizer.NeonManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
        int materialIndex = playerVehicle.Customizer.NeonManager.FindMaterialIndex(material);

        if (materialIndex == playerVehicle.Customizer.NeonManager.index)
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
            _yesNo.Notify(operation.Result);
            SoundManager.Instance.PlayButtonClick();
            UpdateActionButton(playerVehicle);
            return;
        }

        if (loadout.IsNeonPurchased(materialIndex))
        {
            SoundManager.Instance.PlayButtonClick();
            playerVehicle.Customizer.NeonManager.Upgrade(material);
            Refresh();
            UpdateActionButton(playerVehicle);
            GetComponent<Button>().Select();
            return;
        }

        if (playerVehicle.Customizer.NeonManager.FindMaterialIndex(material) != playerVehicle.Customizer.NeonManager.index)
        {
            var buystring = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "BuyYes/No");

            bool result = await _yesNo.ShowYesNoPanelAsync(buystring.Result + "?");

            if (result)
            {
                if (SaveManager.Instance.saveData.money >= price)
                {
                    _moneyManager.MoneyToTake(price);
                    SoundManager.Instance.PlayButtonClick();
                    loadout.MarkNeonPurchased(materialIndex);
                    playerVehicle.Customizer.NeonManager.Upgrade(material);
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
