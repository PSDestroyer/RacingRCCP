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
    
    [Min(0)] public int price = 150;
    [SerializeField] private int[] levelPrices = { 150, 300, 550, 900, 1400 };
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

        if (priceText)
            priceText.text = GetCurrentPrice(playerVehicle).ToString();
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

        if (priceText)
            priceText.text = GetCurrentPrice(playerVehicle).ToString();

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

        if (!playerVehicle.Customizer.UpgradeManager)
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
                int currentPrice = GetCurrentPrice(playerVehicle);

                if (SaveManager.Instance.saveData.money >= currentPrice)
                {
                    _moneyManager.MoneyToTake(currentPrice);
                    SoundManager.Instance.PlayButtonClick();
                    Buy();
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
                GetComponent<Button>().Select();
                SoundManager.Instance.PlayButtonClick();
            }
        }
    
    private int GetCurrentPrice(RCCP_CarController playerVehicle) {

        int currentLevel = GetCurrentLevel(playerVehicle);

        if (levelPrices != null && levelPrices.Length > 0) {
            int priceIndex = Mathf.Clamp(currentLevel, 0, levelPrices.Length - 1);
            return Mathf.Max(0, levelPrices[priceIndex]);
        }

        return Mathf.Max(0, price);

    }

    private int GetCurrentLevel(RCCP_CarController playerVehicle) {

        if (!playerVehicle || !playerVehicle.Customizer || !playerVehicle.Customizer.UpgradeManager)
            return 0;

        switch (upgradeClass) {

            case UpgradeClass.Engine:
                return playerVehicle.Customizer.UpgradeManager.EngineLevel;
            case UpgradeClass.Handling:
                return playerVehicle.Customizer.UpgradeManager.HandlingLevel;
            case UpgradeClass.Brake:
                return playerVehicle.Customizer.UpgradeManager.BrakeLevel;
            case UpgradeClass.Speed:
                return playerVehicle.Customizer.UpgradeManager.SpeedLevel;
            default:
                return 0;

        }

    }

    }


