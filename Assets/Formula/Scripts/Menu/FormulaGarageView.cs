using System;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaGarageView : MonoBehaviour
    {
        [Serializable] public sealed class SetupCard { public string id; public UnityEngine.UI.Button button; public TMP_Text label; public GameObject equippedMarker; }
        [SerializeField] private UnityEngine.UI.Button[] tabs;
        [SerializeField] private GameObject[] tabMarkers, sections;
        [SerializeField] private FormulaDevelopmentCard[] development;
        [SerializeField] private SetupCard[] setups;
        [SerializeField] private FormulaLiveryCard[] liveries;
        [SerializeField] private FormulaStatisticsView[] statistics;
        [SerializeField] private TMP_Text liveryName, liveryStatus, liveryCondition, liveryActionText;
        [SerializeField] private UnityEngine.UI.Button liveryAction;
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
            for (int i = 0; i < sections.Length; i++) { sections[i].SetActive(i == index); tabMarkers[i].SetActive(i == index); }
        }
        public void Refresh()
        {
            foreach (var card in development) card.Refresh();
            var p = FormulaProgression.State;
            foreach (var card in setups)
            {
                var setup = FormulaCatalog.Current.freeSetups.Find(s => s.id == card.id);
                string description = card.id == "balanced" ? "Balance between corners and straights." : card.id == "speed" ? "Downforce −8% · Drag −4%" : "Downforce +8% · Drag +4%";
                card.label.text = setup.name.ToUpperInvariant() + (p.setup == card.id ? " · EQUIPPED" : " · FREE") + "\n" + description;
                card.equippedMarker.SetActive(p.setup == card.id);
            }
            foreach (var card in liveries) card.Refresh();
        }
        public void RefreshStatistics(RCCP_CarController car, WheelCollider wheel)
        {
            foreach (var panel in statistics) panel.Refresh(car, wheel);
            foreach (var card in development) card.RefreshComparison(car, wheel);
        }
        public void InspectLivery(string id)
        {
            var livery = FormulaCatalog.Current.Skin(id); var p = FormulaProgression.State;
            bool owned = p.ownedLiveries.Contains(id), unlocked = FormulaProgression.Unlocked(livery.unlock, p), equipped = p.equippedLivery == id;
            liveryName.text = livery.displayName;
            liveryStatus.text = equipped ? "EQUIPPED" : owned ? "OWNED" : livery.acquisition == "credit_purchase" && unlocked ? livery.priceCredits.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR" : "CAREER REWARD";
            liveryCondition.text = owned ? "Cosmetic only. Your upgrades are retained." : FormulaProgression.LockReason(livery.unlock, p);
            bool buy = !owned && unlocked && livery.acquisition == "credit_purchase";
            liveryAction.interactable = !equipped && (owned || buy && FormulaProgression.Data.money >= livery.priceCredits.Value);
            liveryActionText.text = equipped ? "Equipped" : owned ? "Equip livery" : buy ? "Buy and equip" : "Continue career";
            if (buy && FormulaProgression.Data.money < livery.priceCredits.Value) liveryCondition.text = "Not enough credits / " + livery.priceCredits.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR";
        }
    }
}
