//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using TMPro;
using UnityEngine.Localization.Settings;

/// <summary>
/// UI upgrade button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Upgrade Button")]
public class RCCP_UI_Upgrade : RCCP_UIComponent {

    /// <summary>
    /// Upgrader class for this upgrader.
    /// </summary>
    public UpgradeClass upgradeClass = UpgradeClass.Engine;
    public enum UpgradeClass { Engine, Handling, Brake, Speed }

    /// <summary>
    /// Level count will be displayed on this text, if choosen.
    /// </summary>
    public TMP_Text levelText;
    public TMP_Text UpgText;
    
    [Min(0)] public int price = 50;
    [SerializeField] private YesNo _yesNo;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private MoneyManager _moneyManager;

    private void OnEnable() {

        //  Finding the player vehicle.
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        //  If no player vehicle found, return.
        if (!playerVehicle)
            return;

        //  If player vehicle doesn't have the customizer component, return.
        if (!playerVehicle.Customizer)
            return;

        if (!playerVehicle.Customizer.UpgradeManager)
            return;

        if (!levelText)
            return;

        switch (upgradeClass) {

            case UpgradeClass.Engine:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.EngineLevel + 0).ToString();
                break;
            case UpgradeClass.Handling:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.HandlingLevel + 0).ToString();
                break;
            case UpgradeClass.Brake:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.BrakeLevel + 0).ToString();
                break;
            case UpgradeClass.Speed:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.SpeedLevel + 0).ToString();
                break;

        }

        UpgText.text = upgradeClass.ToString();
        priceText.text = price + "<sprite index=1>";
    }

    public void OnClick()
    {
        YesNo();
    }

    public void Buy() {

        //  Finding the player vehicle.
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        //  If no player vehicle found, return.
        if (!playerVehicle)
            return;

        //  If player vehicle doesn't have the customizer component, return.
        if (!playerVehicle.Customizer)
            return;

        if (!playerVehicle.Customizer.UpgradeManager)
            return;

        switch (upgradeClass) {

            case UpgradeClass.Engine:
                playerVehicle.Customizer.UpgradeManager.UpgradeEngine();
                break;
            case UpgradeClass.Handling:
                playerVehicle.Customizer.UpgradeManager.UpgradeHandling();
                break;
            case UpgradeClass.Brake:
                playerVehicle.Customizer.UpgradeManager.UpgradeBrake();
                break;
            case UpgradeClass.Speed:
                playerVehicle.Customizer.UpgradeManager.UpgradeSpeed();
                break;

        }

        if (!levelText)
            return;

        switch (upgradeClass) {

            case UpgradeClass.Engine:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.EngineLevel + 0).ToString();
                break;
            case UpgradeClass.Handling:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.HandlingLevel + 0).ToString();
                break;
            case UpgradeClass.Brake:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.BrakeLevel + 0).ToString();
                break;
            case UpgradeClass.Speed:
                levelText.text = (playerVehicle.Customizer.UpgradeManager.SpeedLevel + 0).ToString();
                break;

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

        switch (upgradeClass) {

            case UpgradeClass.Engine:
                if (playerVehicle.Customizer.UpgradeManager.EngineLevel >= 5)
                {
                    var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "MaxLevelUpgrade");
                    _yesNo.Notify(operation.Result);
                    SoundManager.Instance.PlayButtonClick();
                    return;
                }

                break;
            case UpgradeClass.Handling:
                if(playerVehicle.Customizer.UpgradeManager.HandlingLevel >= 5)
                {
                    var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "MaxLevelUpgrade");
                    _yesNo.Notify(operation.Result);
                    SoundManager.Instance.PlayButtonClick();
                    return;
                }
                break;
            case UpgradeClass.Brake:
                if(playerVehicle.Customizer.UpgradeManager.BrakeLevel >= 5)
                {
                    var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "MaxLevelUpgrade");
                    _yesNo.Notify(operation.Result);
                    SoundManager.Instance.PlayButtonClick();
                    return;
                }
                break;
            case UpgradeClass.Speed:
                if(playerVehicle.Customizer.UpgradeManager.SpeedLevel >= 5)
                {
                    var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "MaxLevelUpgrade");
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
                    Buy();
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
    
    }


