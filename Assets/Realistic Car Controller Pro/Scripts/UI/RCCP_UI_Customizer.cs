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
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;

/// <summary>
/// RCCP UI Canvas that manages the event systems, panels, gauges, images and texts related to the vehicle and player.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/RCCP UI Customizer")]
public class RCCP_UI_Customizer : RCCP_UIComponent
{


    public EnableCinemachine enableCinemachine;
    public CinemachineCamera Cinemachine;
    public CinemachineCamera LastCinemachine;
    
    [Header("Customization Panels")]
    public GameObject paints;        //  Painting panel.
    public GameObject wheels;        //  Wheels panel.
    public GameObject customization;      //  Customization panel.
    public GameObject upgrades;      //  Upgrades panel.
    public GameObject spoilers;       //  Spoilers panel.
    public GameObject sirens;     //  Sirens panel.
    public GameObject decals;     //  Decals panel.
    public GameObject neons;     //  Neons panel.

    [Header("Customization Buttons")]
    public Button paintsButton;        //  Painting button.
    public Button wheelsButton;        //  Wheels button.
    public Button customizationButton;      //  Customization button.
    public Button upgradesButton;      //  Upgrades button.
    public Button spoilersButton;       //  Spoilers button.
    public Button sirensButton;     //  Sirens button.
    public Button decalsButton;     //  Decals button.
    public Button neonsButton;     //  Neons button.

    public void OpenCustomizationPanel(GameObject activeMenu) {

        CloseCustomizationPanels();

        if (activeMenu && IsPanelAvailable(activeMenu))
            activeMenu.SetActive(true);

    }

    public void CloseCustomizationPanels() {

        if (paints)
            paints.SetActive(false);

        if (wheels)
            wheels.SetActive(false);

        if (customization)
            customization.SetActive(false);

        if (upgrades)
            upgrades.SetActive(false);

        if (spoilers)
            spoilers.SetActive(false);

        if (sirens)
            sirens.SetActive(false);

        if (decals)
            decals.SetActive(false);

        if (neons)
            neons.SetActive(false);

    }
    private void OnEnable()
    {
        RefreshButtons();
        RefreshPanelAvailability();
        if(LastCinemachine != null)
        enableCinemachine.CameraChange(LastCinemachine);

    }

    public void setUpLastCamera(CinemachineCamera camera)
    {
        LastCinemachine = camera;
    }
    private void OnDisable()
    {
        enableCinemachine.CameraChange(Cinemachine);
    }

    private void RefreshButtons()
    {
        if (!RCCPSceneManager)
        {
            Debug.LogWarning("RCCPSceneManager missing");
            return;
        }

        var vehicle = RCCPSceneManager.activePlayerVehicle;

        if (!vehicle)
        {
            Debug.LogWarning("No active vehicle for customization panel");
            return;
        }

        if (!vehicle.Customizer)
        {
            Debug.LogWarning("Active vehicle has no Customizer");
            return;
        }

        var customizer = vehicle.Customizer;

        if (paintsButton)
            paintsButton.interactable = customizer.PaintManager != null;

        if (wheelsButton)
            wheelsButton.interactable = customizer.WheelManager != null;

        if (customizationButton)
            customizationButton.interactable = customizer.CustomizationManager != null;

        if (upgradesButton)
            upgradesButton.interactable = customizer.UpgradeManager != null;

        if (spoilersButton)
            spoilersButton.interactable = customizer.SpoilerManager != null;

        if (sirensButton)
            sirensButton.interactable = customizer.SirenManager != null;

        if (decalsButton)
            decalsButton.interactable = customizer.DecalManager != null;

        if (neonsButton)
            neonsButton.interactable = customizer.NeonManager != null;
    }

    private void RefreshPanelAvailability()
    {
        if (!RCCPSceneManager || !RCCPSceneManager.activePlayerVehicle || !RCCPSceneManager.activePlayerVehicle.Customizer)
        {
            CloseCustomizationPanels();
            return;
        }

        if (paints && !IsPanelAvailable(paints))
            paints.SetActive(false);

        if (wheels && !IsPanelAvailable(wheels))
            wheels.SetActive(false);

        if (customization && !IsPanelAvailable(customization))
            customization.SetActive(false);

        if (upgrades && !IsPanelAvailable(upgrades))
            upgrades.SetActive(false);

        if (spoilers && !IsPanelAvailable(spoilers))
            spoilers.SetActive(false);

        if (sirens && !IsPanelAvailable(sirens))
            sirens.SetActive(false);

        if (decals && !IsPanelAvailable(decals))
            decals.SetActive(false);

        if (neons && !IsPanelAvailable(neons))
            neons.SetActive(false);
    }

    private bool IsPanelAvailable(GameObject panel)
    {
        if (!panel || !RCCPSceneManager || !RCCPSceneManager.activePlayerVehicle || !RCCPSceneManager.activePlayerVehicle.Customizer)
            return false;

        var customizer = RCCPSceneManager.activePlayerVehicle.Customizer;

        if (panel == paints)
            return customizer.PaintManager != null;

        if (panel == wheels)
            return customizer.WheelManager != null;

        if (panel == customization)
            return customizer.CustomizationManager != null;

        if (panel == upgrades)
            return customizer.UpgradeManager != null;

        if (panel == spoilers)
            return customizer.SpoilerManager != null;

        if (panel == sirens)
            return customizer.SirenManager != null;

        if (panel == decals)
            return customizer.DecalManager != null;

        if (panel == neons)
            return customizer.NeonManager != null;

        return false;
    }
/*
    private void Update() {

        if (paintsButton)
            paintsButton.interactable = false;

        if (wheelsButton)
            wheelsButton.interactable = false;

        if (customizationButton)
            customizationButton.interactable = false;

        if (upgradesButton)
            upgradesButton.interactable = false;

        if (spoilersButton)
            spoilersButton.interactable = false;

        if (sirensButton)
            sirensButton.interactable = false;

        if (decalsButton)
            decalsButton.interactable = false;

        if (neonsButton)
            neonsButton.interactable = false;

        if (!RCCPSceneManager)
            return;

        if (!RCCPSceneManager.activePlayerVehicle)
            return;

        if (!RCCPSceneManager.activePlayerVehicle.Customizer)
            return;

        if (paintsButton)
            paintsButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.PaintManager;
     
        if (wheelsButton)
            wheelsButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.WheelManager;

        if (customizationButton)
            customizationButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.CustomizationManager;

        if (upgradesButton)
            upgradesButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.UpgradeManager;

        if (spoilersButton)
            spoilersButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.SpoilerManager;

        if (sirensButton)
            sirensButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.SirenManager;

        if (decalsButton)
            decalsButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.DecalManager;

        if (neonsButton)
            neonsButton.interactable = RCCPSceneManager.activePlayerVehicle.Customizer.NeonManager;

    }
*/
}
