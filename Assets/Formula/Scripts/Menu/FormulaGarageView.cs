using System;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaGarageView : MonoBehaviour
    {
        [Serializable] public sealed class SetupCard { public string id; public UnityEngine.UI.Button button; public TMP_Text label, description, status; public GameObject equippedMarker; }
        [SerializeField] private UnityEngine.UI.Button[] tabs;
        [SerializeField] private GameObject[] tabMarkers, sections;
        [SerializeField] private FormulaDevelopmentCard[] development;
        [SerializeField] private SetupCard[] setups;
        [SerializeField] private FormulaLiveryCard[] liveries;
        [SerializeField] private FormulaStatisticsView[] statistics;
        [SerializeField] private TMP_Text liveryName, liveryStatus, liveryCondition, liveryActionText;
        [SerializeField] private TMP_Text liveryFunds, liveryCost;
        [SerializeField] private UnityEngine.UI.Button liveryAction;
        [SerializeField] private FormulaUpgradeView upgrade;
        [SerializeField] private UnityEngine.UI.Button back;
        [SerializeField] private TMP_Text tabHints, contextLabel;
        private int currentSection;
        private string inspectedLivery;
        private int motionSection = -1;
        private string shownSetup;
        public FormulaUpgradeView Upgrade => upgrade;
        public void Bind(Action<int> changeTab, Action<string> levels, Action<string> buyUpgrade, Action<string> equipSetup, Action<string> selectLivery, Action applyLivery)
        {
            for (int i = 0; i < tabs.Length; i++) { int index = i; tabs[i].onClick.AddListener(() => changeTab(index)); }
            foreach (var card in development) card.Bind(levels, buyUpgrade);
            foreach (var card in setups) card.button.onClick.AddListener(() => equipSetup(card.id));
            foreach (var card in liveries) card.Bind(selectLivery);
            liveryAction.onClick.AddListener(() => applyLivery());
        }
        public void ShowSection(int index)
        {
            Vector3 from = motionSection >= 0 ? tabMarkers[motionSection].transform.position : Vector3.zero;
            currentSection = index;
            for (int i = 0; i < sections.Length; i++) { sections[i].SetActive(i == index); tabMarkers[i].SetActive(i == index); }
            if (gameObject.activeInHierarchy)
            {
                if (motionSection >= 0 && motionSection != index) FormulaUIMotion.Glide(tabMarkers[index], from);
                motionSection = index;
            }
            if (upgrade != null) upgrade.SetPresentationActive(gameObject.activeInHierarchy && index == 0);
            WireNavigation();
        }
        public void Refresh()
        {
            foreach (var card in development) card.Refresh();
            if (upgrade != null) upgrade.Refresh();
            var p = FormulaProgression.State;
            foreach (var card in setups)
            {
                var setup = FormulaCatalog.Current.freeSetups.Find(s => s.id == card.id);
                string description = card.id == "balanced" ? FormulaLocalization.T("Balanced cornering and speed.") : card.id == "speed" ? FormulaLocalization.T("Downforce −8% · Drag −4%") : FormulaLocalization.T("Downforce +8% · Drag +4%");
                card.label.text = card.description == null ? FormulaLocalization.T(setup.name).ToUpperInvariant() + (p.setup == card.id ? FormulaLocalization.T(" · EQUIPPED") : FormulaLocalization.T(" · FREE")) + "\n" + description : FormulaLocalization.T(setup.name).ToUpperInvariant();
                if (card.description != null) card.description.text = description;
                if (card.status != null) card.status.text = p.setup == card.id ? FormulaLocalization.T("APPLIED") : FormulaLocalization.T("FREE · APPLY");
                card.equippedMarker.SetActive(p.setup == card.id);
                if (shownSetup != null && shownSetup != p.setup && p.setup == card.id)
                { FormulaUIMotion.Enter(card.equippedMarker, true); if (card.status != null) FormulaUIMotion.Pulse(card.status.gameObject); }
            }
            shownSetup = p.setup;
            foreach (var card in liveries) card.Refresh();
        }
        public void RefreshStatistics(RCCP_CarController car, WheelCollider wheel)
        {
            foreach (var panel in statistics) panel.Refresh(car, wheel);
            foreach (var card in development) card.RefreshComparison(car, wheel);
            if (upgrade != null) upgrade.RefreshPhysics(car, wheel);
        }
        public void SetEventContext(FormulaCatalog.CareerEvent entry, string mode)
        {
            if (contextLabel == null) return;
            contextLabel.text = entry == null ? "" : FormulaLocalization.T(entry.name).ToUpperInvariant() + "\n" + ((mode == "career" && entry.vehicleRule == "stock_balanced") || mode == "time_trial" ? FormulaLocalization.T("Stock event · Your car changes will not apply") : FormulaLocalization.T("Your equipped upgrades and setup will be used"));
        }
        public void PreviewSetup(string id)
        {
            foreach (var panel in statistics) panel.PreviewSetup(id);
        }
        public void WireNavigation()
        {
            if (!gameObject.activeInHierarchy || back == null) return;
            if (tabHints != null) tabHints.text = FormulaInputHints.Sections;
            var navigationBack = upgrade != null && upgrade.IsPresented ? upgrade.BackControl : back;
            if (currentSection == 0 && upgrade != null) upgrade.WireNavigation(tabs[0]);
            var first = currentSection == 0 && upgrade != null ? upgrade.FirstControl : Array.Find(sections[currentSection].GetComponentsInChildren<UnityEngine.UI.Selectable>(), b => b.IsActive() && b.IsInteractable());
            for (int i = 0; i < tabs.Length; i++) tabs[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = tabs[(i + tabs.Length - 1) % tabs.Length], selectOnRight = tabs[(i + 1) % tabs.Length], selectOnDown = first, selectOnUp = navigationBack };
            if (currentSection == 1)
                for (int i = 0; i < setups.Length; i++) setups[i].button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = i == 0 ? tabs[1] : setups[i-1].button, selectOnDown = i == setups.Length-1 ? back : setups[i+1].button, selectOnRight = tabs[1] };
            if (currentSection == 2)
            {
                var selected = Array.Find(liveries, c => c.LiveryId == inspectedLivery) ?? liveries[0];
                var selectedButton = selected.GetComponent<UnityEngine.UI.Button>();
                var upper = liveryAction.interactable ? liveryAction : tabs[2];
                for (int i = 0; i < liveries.Length; i++)
                    liveries[i].GetComponent<UnityEngine.UI.Button>().navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = liveries[(i + liveries.Length - 1) % liveries.Length].GetComponent<UnityEngine.UI.Button>(), selectOnRight = liveries[(i + 1) % liveries.Length].GetComponent<UnityEngine.UI.Button>(), selectOnUp = upper, selectOnDown = back };
                liveryAction.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = tabs[2], selectOnDown = selectedButton, selectOnRight = selectedButton };
            }
        }
        public void InspectLivery(string id)
        {
            inspectedLivery = id; var livery = FormulaCatalog.Current.Skin(id);
            var offer = new FormulaLiveryOffer(livery, FormulaProgression.State, FormulaProgression.Data.money);
            liveryName.text = FormulaLocalization.T(livery.displayName);
            liveryStatus.text = offer.Status; liveryStatus.color = offer.Owned ? Color.white : FormulaUI.Accent;
            liveryCondition.text = offer.Condition; liveryCondition.color = offer.Unlocked || offer.Owned ? Color.white : FormulaUI.Accent;
            if (liveryFunds != null) liveryFunds.text = offer.Funds ?? (offer.Owned ? FormulaLocalization.T("Cosmetic only · No performance changes") : FormulaLocalization.T("Not sold for credits"));
            if (liveryCost != null) { liveryCost.text = offer.Cost; liveryCost.color = offer.Shortfall > 0 ? FormulaUI.Red : Color.white; }
            liveryAction.interactable = !offer.Equipped;
            liveryActionText.text = offer.Action; WireNavigation();
        }
    }
}
