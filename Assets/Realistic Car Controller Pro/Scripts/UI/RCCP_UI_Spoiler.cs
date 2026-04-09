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

    }

    private void OnDisable()
    {
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;
        playerVehicle.Customizer.SpoilerManager.Initialize();
    }

    public void OnClick() {

        YesNo();

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
        if (playerVehicle.Customizer.SpoilerManager.spoilerIndex != index)
        {
            priceText.text = price + "<sprite index=1>";
        }
        else
        {
            var Purch = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","Purchased");
            priceText.text = Purch.ToString();
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

        if (!playerVehicle.Customizer.SpoilerManager)
            return;

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
                    playerVehicle.Customizer.SpoilerManager.Upgrade(index);
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
        else
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "This item bought");
            _yesNo.Notify(operation.Result);
            SoundManager.Instance.PlayButtonClick();

        }
    }
}
