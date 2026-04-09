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

    }
    private void Start()
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
        priceText.text = price + "<sprite index=1>";
        
        switch (location) {

                case 0:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalFront)
                    {
                        var Purch = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","Purchased");
                        priceText.text = Purch.ToString();
                    }
                    break;

                case 1:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalBack)
                    {
                        var Purch = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","Purchased");
                        priceText.text = Purch.ToString();
                    }
                    break;

                case 2:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalLeft)
                    {
                        var Purch = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","Purchased");
                        priceText.text = Purch.ToString();
                    }
                    break;

                case 3:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalRight)
                    {
                        var Purch = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","Purchased");
                        priceText.text = Purch.ToString();
                    }
                    break;

            }
    }
    private void OnDisable()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.DecalManager.Initialize();
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
            switch (location) {

                case 0:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalFront)
                    {
                        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
                        _yesNo.Notify(operation.Result);
                        SoundManager.Instance.PlayButtonClick();
                        return;
                    }
                    break;

                case 1:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalBack)
                    {
                        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
                        _yesNo.Notify(operation.Result);
                        SoundManager.Instance.PlayButtonClick();
                        return;
                    }
                    break;

                case 2:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalLeft)
                    {
                        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
                        _yesNo.Notify(operation.Result);
                        SoundManager.Instance.PlayButtonClick();
                        return;
                    }
                    break;

                case 3:
                    if (playerVehicle.Customizer.DecalManager.FindMaterialIndex(material) ==
                        playerVehicle.Customizer.DecalManager.index_decalRight)
                    {
                        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
                        _yesNo.Notify(operation.Result);
                        SoundManager.Instance.PlayButtonClick();
                        return;
                    }
                    break;

            }
            var buystring = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "BuyYes/No");
    
                bool result = await _yesNo.ShowYesNoPanelAsync(buystring.Result + "?");
    
                if (result)
                {
                    if (SaveManager.Instance.saveData.money >= price)
                    {
                        _moneyManager.MoneyToTake(price);
                        SoundManager.Instance.PlayButtonClick();
                        playerVehicle.Customizer.DecalManager.Upgrade(location, material);
                        Refresh();
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
