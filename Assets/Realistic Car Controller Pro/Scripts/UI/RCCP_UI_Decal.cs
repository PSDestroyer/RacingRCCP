//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using TMPro;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// UI decal button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Decal Button")]
public class RCCP_UI_Decal : RCCP_UIComponent ,ISelectHandler{

    /// <summary>
    /// Target location of the decal. 0 is front, 1 is back, 2 is left, and 3 is right.
    /// </summary>
    [Min(0)] public int location = 0;

    /// <summary>
    /// Target material.
    /// </summary>
    public Material material;
    [Min(0)] public int price = 50;
    [SerializeField] private YesNo _yesNo;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private MoneyManager _moneyManager;
    [SerializeField] private List<RCCP_UI_Decal> Comp = new List<RCCP_UI_Decal>();

    
    public void OnSelect(BaseEventData baseEventData)
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.DecalManager.UpgradeWithoutSave(location, material);
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
        foreach (var VARIABLE in Comp)
        {
            VARIABLE.Initialize();
        }
    }

    private int GetMaterialIndex(RCCP_CarController playerVehicle)
    {
        return playerVehicle.Customizer.DecalManager.FindMaterialIndex(material);
    }

    private int GetCurrentDecalIndex(RCCP_CarController playerVehicle)
    {
        return location switch
        {
            0 => playerVehicle.Customizer.DecalManager.index_decalFront,
            1 => playerVehicle.Customizer.DecalManager.index_decalBack,
            2 => playerVehicle.Customizer.DecalManager.index_decalLeft,
            3 => playerVehicle.Customizer.DecalManager.index_decalRight,
            _ => -1
        };
    }

    private void Initialize() 
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.DecalManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
        int materialIndex = GetMaterialIndex(playerVehicle);

        if (materialIndex == GetCurrentDecalIndex(playerVehicle))
            RCCP_UI_PriceLabelUtility.SetEquipped(priceText, "In Use");
        else if (loadout.IsDecalPurchased(location, materialIndex))
            RCCP_UI_PriceLabelUtility.SetPurchased(priceText, "Owned");
        else
            RCCP_UI_PriceLabelUtility.SetPrice(priceText, price);

    }
    private void OnDisable()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.DecalManager.Initialize();
        RCCP_UI_CustomizationActionButton.Clear();
    }

    private void UpdateActionButton(RCCP_CarController playerVehicle) {

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.DecalManager)
            return;

        RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
        int materialIndex = GetMaterialIndex(playerVehicle);
        bool isEquipped = materialIndex == GetCurrentDecalIndex(playerVehicle);
        bool isPurchased = loadout.IsDecalPurchased(location, materialIndex);
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
    
            if (!playerVehicle.Customizer.DecalManager)
                return;

            RCCP_CustomizationLoadout loadout = playerVehicle.Customizer.GetLoadout();
            int materialIndex = GetMaterialIndex(playerVehicle);
            int currentIndex = GetCurrentDecalIndex(playerVehicle);

            if (materialIndex == currentIndex)
            {
                var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
                _yesNo.Notify(operation.Result);
                SoundManager.Instance.PlayButtonClick();
                UpdateActionButton(playerVehicle);
                return;
            }

            if (loadout.IsDecalPurchased(location, materialIndex))
            {
                SoundManager.Instance.PlayButtonClick();
                playerVehicle.Customizer.DecalManager.Upgrade(location, material);
                Refresh();
                UpdateActionButton(playerVehicle);
                GetComponent<Button>().Select();
                return;
            }

            var buystring = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "BuyYes/No");
    
                bool result = await _yesNo.ShowYesNoPanelAsync(buystring.Result + "?");
    
                if (result)
                {
                    if (SaveManager.Instance.saveData.money >= price)
                    {
                        _moneyManager.MoneyToTake(price);
                        SoundManager.Instance.PlayButtonClick();
                        loadout.MarkDecalPurchased(location, materialIndex);
                        playerVehicle.Customizer.DecalManager.Upgrade(location, material);
                        Refresh();
                        UpdateActionButton(playerVehicle);
                    }
                    else
                    {
                        _yesNo.NotifyNotEnoughMoney();
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

    /// <summary>
    /// Sets the location of the decal. 0 is front, 1 is back, 2 is left, and 3 is right.
    /// </summary>
    /// <param name="_location"></param>
    public void SetLocation(int _location) {

        location = _location;

    }

}
