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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// RCCP UI Canvas that manages the event systems, panels, gauges, images and texts related to the vehicle and player.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/RCCP UI Customizer")]
public class RCCP_UI_Customizer : RCCP_UIComponent {

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

    private Dictionary<Button, Sprite> normalButtonSprites;
    private bool openedCustomizationOnce;
    private GameObject lastOpenedPanel;

    private void Awake() {

        CacheNormalButtonSprites();

    }

    public void OpenCustomizationPanel(GameObject activeMenu) {

        CloseCustomizationPanels();

        if (activeMenu)
            activeMenu.SetActive(true);

        if (activeMenu)
            lastOpenedPanel = activeMenu;

        SetSelectedCustomizationButton(activeMenu);
        SelectFirstPanelItem(activeMenu);

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
        CacheNormalButtonSprites();
        RefreshButtons();

        GameObject targetPanel = GetPanelToOpenOnEnable();

        if (targetPanel)
            OpenCustomizationPanel(targetPanel);
        else
        {
            SetSelectedCustomizationButton(GetActiveCustomizationPanel());
            SelectFirstPanelItem(GetActiveCustomizationPanel());
        }
    }

    private void Update()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || Gamepad.current == null)
            return;

        if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            SwitchCustomizationPanel(1);

        if (Gamepad.current.rightShoulder.wasPressedThisFrame)
            SwitchCustomizationPanel(-1);
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

        DisableTabNavigation();
    }

    private void DisableTabNavigation() {

        DisableButtonNavigation(paintsButton);
        DisableButtonNavigation(wheelsButton);
        DisableButtonNavigation(customizationButton);
        DisableButtonNavigation(upgradesButton);
        DisableButtonNavigation(spoilersButton);
        DisableButtonNavigation(sirensButton);
        DisableButtonNavigation(decalsButton);
        DisableButtonNavigation(neonsButton);

    }

    private void DisableButtonNavigation(Button button) {

        if (!button)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

    }

    private void CacheNormalButtonSprites() {

        if (normalButtonSprites == null)
            normalButtonSprites = new Dictionary<Button, Sprite>();

        CacheNormalButtonSprite(paintsButton);
        CacheNormalButtonSprite(wheelsButton);
        CacheNormalButtonSprite(customizationButton);
        CacheNormalButtonSprite(upgradesButton);
        CacheNormalButtonSprite(spoilersButton);
        CacheNormalButtonSprite(sirensButton);
        CacheNormalButtonSprite(decalsButton);
        CacheNormalButtonSprite(neonsButton);

    }

    private void CacheNormalButtonSprite(Button button) {

        if (!button || !button.targetGraphic)
            return;

        Image image = button.targetGraphic as Image;

        if (!image || normalButtonSprites.ContainsKey(button))
            return;

        normalButtonSprites.Add(button, image.sprite);

    }

    private GameObject GetActiveCustomizationPanel() {

        if (paints && paints.activeSelf)
            return paints;

        if (wheels && wheels.activeSelf)
            return wheels;

        if (customization && customization.activeSelf)
            return customization;

        if (upgrades && upgrades.activeSelf)
            return upgrades;

        if (spoilers && spoilers.activeSelf)
            return spoilers;

        if (sirens && sirens.activeSelf)
            return sirens;

        if (decals && decals.activeSelf)
            return decals;

        if (neons && neons.activeSelf)
            return neons;

        return null;

    }

    private GameObject GetPanelToOpenOnEnable() {

        if (!openedCustomizationOnce) {

            openedCustomizationOnce = true;
            GameObject wheelsPanel = GetAvailablePanel(wheels, wheelsButton);
            if (wheelsPanel)
                return wheelsPanel;

        }

        if (lastOpenedPanel && IsPanelAvailable(lastOpenedPanel))
            return lastOpenedPanel;

        GameObject activePanel = GetActiveCustomizationPanel();
        if (activePanel && IsPanelAvailable(activePanel))
            return activePanel;

        List<CustomizationTab> tabs = GetAvailableTabs();
        return tabs.Count > 0 ? tabs[0].panel : null;

    }

    private GameObject GetAvailablePanel(GameObject panel, Button button) {

        return IsPanelAvailable(panel, button) ? panel : null;

    }

    private bool IsPanelAvailable(GameObject panel) {

        if (panel == paints)
            return IsPanelAvailable(panel, paintsButton);

        if (panel == wheels)
            return IsPanelAvailable(panel, wheelsButton);

        if (panel == customization)
            return IsPanelAvailable(panel, customizationButton);

        if (panel == upgrades)
            return IsPanelAvailable(panel, upgradesButton);

        if (panel == spoilers)
            return IsPanelAvailable(panel, spoilersButton);

        if (panel == sirens)
            return IsPanelAvailable(panel, sirensButton);

        if (panel == decals)
            return IsPanelAvailable(panel, decalsButton);

        if (panel == neons)
            return IsPanelAvailable(panel, neonsButton);

        return false;

    }

    private bool IsPanelAvailable(GameObject panel, Button button) {

        return panel && button && button.gameObject.activeInHierarchy && button.interactable;

    }

    private void SwitchCustomizationPanel(int direction) {

        List<CustomizationTab> tabs = GetAvailableTabs();

        if (tabs.Count == 0)
            return;

        GameObject activePanel = GetActiveCustomizationPanel();
        int currentIndex = tabs.FindIndex(tab => tab.panel == activePanel);

        if (currentIndex < 0)
            currentIndex = 0;
        else
            currentIndex = (currentIndex + direction + tabs.Count) % tabs.Count;

        OpenCustomizationPanel(tabs[currentIndex].panel);

    }

    private void SelectFirstPanelItem(GameObject panel) {

        if (!panel || !panel.activeInHierarchy || EventSystem.current == null)
            return;

        StartCoroutine(SelectFirstPanelItemNextFrame(panel));

    }

    private IEnumerator SelectFirstPanelItemNextFrame(GameObject panel) {

        yield return null;

        if (!panel || !panel.activeInHierarchy || EventSystem.current == null)
            yield break;

        Selectable[] selectables = panel.GetComponentsInChildren<Selectable>(true);

        foreach (Selectable selectable in selectables) {

            if (!selectable || !selectable.gameObject.activeInHierarchy || !selectable.IsInteractable())
                continue;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            yield break;

        }

        EventSystem.current.SetSelectedGameObject(null);

    }

    private List<CustomizationTab> GetAvailableTabs() {

        List<CustomizationTab> tabs = new List<CustomizationTab>();

        AddTabIfAvailable(tabs, paints, paintsButton);
        AddTabIfAvailable(tabs, wheels, wheelsButton);
        AddTabIfAvailable(tabs, customization, customizationButton);
        AddTabIfAvailable(tabs, upgrades, upgradesButton);
        AddTabIfAvailable(tabs, spoilers, spoilersButton);
        AddTabIfAvailable(tabs, sirens, sirensButton);
        AddTabIfAvailable(tabs, decals, decalsButton);
        AddTabIfAvailable(tabs, neons, neonsButton);

        return tabs;

    }

    private void AddTabIfAvailable(List<CustomizationTab> tabs, GameObject panel, Button button) {

        if (!panel || !button || !button.gameObject.activeInHierarchy || !button.interactable)
            return;

        tabs.Add(new CustomizationTab(panel, button));

    }

    private struct CustomizationTab {

        public GameObject panel;
        public Button button;

        public CustomizationTab(GameObject panel, Button button) {

            this.panel = panel;
            this.button = button;

        }

    }

    private void SetSelectedCustomizationButton(GameObject activeMenu) {

        SetButtonSelectedSprite(paintsButton, activeMenu == paints);
        SetButtonSelectedSprite(wheelsButton, activeMenu == wheels);
        SetButtonSelectedSprite(customizationButton, activeMenu == customization);
        SetButtonSelectedSprite(upgradesButton, activeMenu == upgrades);
        SetButtonSelectedSprite(spoilersButton, activeMenu == spoilers);
        SetButtonSelectedSprite(sirensButton, activeMenu == sirens);
        SetButtonSelectedSprite(decalsButton, activeMenu == decals);
        SetButtonSelectedSprite(neonsButton, activeMenu == neons);

    }

    private void SetButtonSelectedSprite(Button button, bool selected) {

        if (!button || !button.targetGraphic)
            return;

        Image image = button.targetGraphic as Image;

        if (!image)
            return;

        if (selected && button.spriteState.selectedSprite)
            image.sprite = button.spriteState.selectedSprite;
        else if (normalButtonSprites != null && normalButtonSprites.TryGetValue(button, out Sprite normalSprite))
            image.sprite = normalSprite;

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
