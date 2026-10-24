using System;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    // Serialized presentation only. The presenter owns navigation; progression owns player data.
    [DisallowMultipleComponent]
    public sealed class FormulaMenuView : MonoBehaviour
    {
        [SerializeField] private Canvas menuCanvas;
        [SerializeField] private RectTransform screens;
        [SerializeField] private CanvasGroup screenTransition;
        [SerializeField] private TMP_Text wallet, title, hints;
        [SerializeField] private UnityEngine.UI.Button back;
        [SerializeField] private FormulaHomeView home;
        [SerializeField] private FormulaCareerView career;
        [SerializeField] private FormulaGarageView garage;
        [SerializeField] private FormulaQuickView quick;
        [SerializeField] private FormulaProfileView profile;
        [SerializeField] private FormulaSettingsView settings;
        [SerializeField] private FormulaMenuDialogs dialogs;
        [SerializeField] private TMP_Text driver;
        [SerializeField] private bool upgradeTheme;
        [SerializeField] private UnityEngine.UI.Image xpFill, screenShade;
        public Canvas Canvas => menuCanvas;
        public Transform Screens => screens;
        public CanvasGroup Transition => screenTransition;
        public UnityEngine.UI.Button Back => back;
        public bool UpgradeTheme => upgradeTheme;
        public FormulaHomeView Home => home;
        public FormulaCareerView Career => career;
        public FormulaGarageView Garage => garage;
        public FormulaQuickView Quick => quick;
        public FormulaProfileView Profile => profile;
        public FormulaSettingsView Settings => settings;
        public FormulaMenuDialogs Dialogs => dialogs;
        public void SetChrome(string pageTitle, string inputHints, bool canGoBack)
        {
            title.text = pageTitle; hints.text = inputHints; back.gameObject.SetActive(canGoBack);
            RefreshWallet();
        }
        public void RefreshWallet()
        {
            var d = FormulaProgression.Data;
            wallet.text = upgradeTheme ? d.money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : driver != null ? $"{d.money:N0} <size=16>CR</size>" : $"{d.PlayerName ?? "Pilot"}  ·  LEVEL {d.currentLevel}\n{d.money:N0} CR  /  {d.exp % 3000:N0}/3.000 XP";
            if (driver != null) driver.text = upgradeTheme ? FormulaMenuFormatting.DriverName : $"{d.PlayerName ?? "Pilot"}   /   LEVEL {d.currentLevel}";
            if (xpFill != null) xpFill.fillAmount = (d.exp % 3000) / 3000f;
        }
        private string activePage = "Acasă";
        public void SetStatus(string message) => hints.text = message;
        public void RefreshContrast()
        {
            bool contrast = FormulaProgression.State.highContrast;
            if (screenShade != null)
            {
                screenShade.gameObject.SetActive(activePage != "Carieră" && !(activePage == "Garaj" && garage.Upgrade.IsPresented));
                screenShade.color = new Color(0, 0, 0, contrast ? .8f : activePage == "Acasă" ? 0 : activePage == "Garaj" ? .2f : .65f);
            }
            var shade = garage.Upgrade.transform.Find("World Shade")?.GetComponent<UnityEngine.UI.Image>();
            if (shade != null) shade.color = new Color(0, 0, 0, contrast ? .7f : .2f);
        }
        public Transform Activate(string page)
        {
            if (garage.Upgrade != null) garage.Upgrade.SetPresentationActive(false);
            career.SetPresentationActive(page == "Carieră");
            activePage = page; RefreshContrast();
            home.gameObject.SetActive(page == "Acasă"); career.gameObject.SetActive(page == "Carieră");
            garage.gameObject.SetActive(page == "Garaj"); quick.gameObject.SetActive(page == "Curse rapide");
            profile.gameObject.SetActive(page == "Profil"); settings.gameObject.SetActive(page == "Setări");
            switch (page)
            {
                case "Acasă": return home.transform;
                case "Carieră": return career.transform;
                case "Garaj": return garage.transform;
                case "Curse rapide": return quick.transform;
                case "Profil": return profile.transform;
                default: return settings.transform;
            }
        }
        public void SetBaseInteraction(bool interactable)
        {
            screenTransition.interactable = interactable; screenTransition.blocksRaycasts = interactable;
            back.interactable = interactable;
            foreach (var button in screens.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                button.navigation = new UnityEngine.UI.Navigation { mode = interactable ? UnityEngine.UI.Navigation.Mode.Automatic : UnityEngine.UI.Navigation.Mode.None };
            if (interactable) { garage.WireNavigation(); home.WireNavigation(); }
        }
    }
}
