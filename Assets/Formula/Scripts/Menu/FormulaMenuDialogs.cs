using System;
using System.Linq;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaMenuDialogs : MonoBehaviour
    {
        [SerializeField] private GameObject confirmation, levelSelection;
        [SerializeField] private TMP_Text title, details, acceptText, levelTitle;
        [SerializeField] private UnityEngine.UI.Button cancel, accept, levelCancel;
        [SerializeField] private UnityEngine.UI.Button[] levels;
        [SerializeField] private TMP_Text[] levelLabels;
        [SerializeField] private GameObject eventBriefing;
        [SerializeField] private TMP_Text eventTitle, eventState, eventTrack, eventGoal, eventVehicle, eventReward, eventActionText, eventNote;
        [SerializeField] private TMP_Text[] medalGoals;
        [SerializeField] private TMP_Text[] medalTiers;
        [SerializeField] private TMP_Text[] medalRewards;
        [SerializeField] private FormulaTrackMap eventMap;
        [SerializeField] private UnityEngine.UI.Button eventCancel, eventAccept, eventGarage;
        [SerializeField] private GameObject upgradeContent;
        [SerializeField] private TMP_Text upgradeEffect, upgradeTradeoff, upgradeBenefit, upgradeRequirement, upgradeCost, upgradeFunds;
        [SerializeField] private GameObject liveryContent;
        [SerializeField] private TMP_Text liveryAcquisition, liveryRequirements, liveryFunds, liveryCost;
        private Action confirm, close;
        private Action<int> selectLevel;
        private bool presented;
        public bool IsOpen => presented && gameObject.activeSelf && !FormulaUIMotion.IsClosing(gameObject);
        public void Bind(Action onClose, Action onGarage = null)
        {
            close = onClose; cancel.onClick.AddListener(() => close()); levelCancel.onClick.AddListener(() => close());
            accept.onClick.AddListener(() => confirm?.Invoke());
            if (eventGarage != null && onGarage != null) eventGarage.onClick.AddListener(() => onGarage());
            if (eventCancel != null) eventCancel.onClick.AddListener(() => close());
            if (eventAccept != null) eventAccept.onClick.AddListener(() => confirm?.Invoke());
            for (int i = 0; i < levels.Length; i++) { int level = i; levels[i].onClick.AddListener(() => selectLevel?.Invoke(level)); }
        }
        public void Confirm(string heading, string description, string actionName, Action action, bool canConfirm)
        {
            presented = true;
            gameObject.SetActive(true); confirmation.SetActive(true); levelSelection.SetActive(false); if (eventBriefing != null) eventBriefing.SetActive(false);
            title.text = heading; details.text = description; acceptText.text = actionName;
            details.gameObject.SetActive(true); if (upgradeContent != null) upgradeContent.SetActive(false);
            if (liveryContent != null) liveryContent.SetActive(false);
            accept.interactable = canConfirm; confirm = action;
            // Reopening a closing dialog must restore its CanvasGroup before
            // IsInteractable is used to build navigation and select a control.
            FormulaUIMotion.Enter(gameObject, true);
            Wire(confirmation.transform); FormulaUI.Focus(confirmation.transform);
        }
        public void Upgrade(string heading, string effect, string tradeoff, string benefit, string requirement, string cost, string actionName, Action action, bool canConfirm, bool focusAction, string funds = null, bool creditsMissing = false)
        {
            Confirm(heading,effect + "\n" + tradeoff + "\n" + benefit + "\n\n" + requirement + "\n" + funds + "\n" + cost,actionName,action,canConfirm);
            if (upgradeContent != null)
            {
                details.gameObject.SetActive(false); upgradeContent.SetActive(true);
                upgradeEffect.text = effect; upgradeTradeoff.text = tradeoff; upgradeBenefit.text = benefit;
                upgradeRequirement.text = requirement; upgradeCost.text = cost;
                upgradeCost.color = creditsMissing ? FormulaUI.Red : Color.white;
                if (upgradeFunds != null) { upgradeFunds.text = funds; upgradeFunds.gameObject.SetActive(!string.IsNullOrEmpty(funds)); }
            }
            if (focusAction && canConfirm) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(accept.gameObject);
        }
        public void Livery(FormulaCatalog.Livery livery, FormulaLiveryOffer offer, string actionName, Action action, bool focusAction)
        {
            Confirm(FormulaLocalization.T(livery.displayName) + FormulaLocalization.T(" / Livery"),offer.Requirements + "\n" + offer.Funds + "\n" + offer.Cost,actionName,action,true);
            if (liveryContent != null)
            {
                details.gameObject.SetActive(false); liveryContent.SetActive(true);
                liveryAcquisition.text = offer.Purchase ? FormulaLocalization.T("CREDIT PURCHASE") : FormulaLocalization.T("CAREER REWARD · FREE");
                liveryRequirements.text = offer.Requirements;
                liveryFunds.text = offer.Funds; liveryFunds.gameObject.SetActive(!string.IsNullOrEmpty(offer.Funds));
                liveryCost.text = offer.Cost; liveryCost.color = offer.Shortfall > 0 ? FormulaUI.Red : Color.white;
            }
            if (focusAction) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(accept.gameObject);
        }
        public void SelectLevel(string branchId, Action<int> action)
        {
            presented = true;
            gameObject.SetActive(true); confirmation.SetActive(false); levelSelection.SetActive(true); if (eventBriefing != null) eventBriefing.SetActive(false); selectLevel = action;
            levelTitle.text = FormulaLocalization.T(FormulaCatalog.Current.Upgrade(branchId).name).ToUpperInvariant() + FormulaLocalization.T(" / EQUIP");
            for (int i = 0; i < levels.Length; i++)
            {
                bool owned = i <= FormulaProgression.State.Owned(branchId);
                string state = FormulaProgression.State.Equipped(branchId) == i ? FormulaLocalization.T("EQUIPPED") : owned ? FormulaLocalization.T("OWNED") : FormulaLocalization.T("NOT PURCHASED");
                levels[i].name = "level-" + branchId + "-" + i;
                levels[i].interactable = owned; levelLabels[i].text = (i == 0 ? FormulaLocalization.T("STOCK CONFIGURATION") : FormulaLocalization.T("LEVEL ") + i) + " / " + state;
            }
            FormulaUIMotion.Enter(gameObject, true);
            Wire(levelSelection.transform); FormulaUI.Focus(levelSelection.transform);
        }
        public void Close() { bool animate = presented; presented = false; confirm = null; selectLevel = null; if (animate) FormulaUIMotion.Hide(gameObject); else gameObject.SetActive(false); }
        public void EventBriefing(FormulaCatalog.CareerEvent e, string mode, Action action, bool unlocked)
        {
            presented = true;
            // Older authored prefabs remain playable until the explicit editor migration runs.
            if (eventBriefing == null)
            {
                Confirm(FormulaLocalization.T(e.name),FormulaMenuFormatting.EventGoal(e) + "\n" + FormulaLocalization.T(FormulaAssets.Current.FindTrack(e.trackId).displayName) + "\n" + (unlocked ? "" : FormulaProgression.LockReason(e.unlock, FormulaProgression.State)),unlocked ? FormulaLocalization.T("Start race") : FormulaLocalization.T("Event locked"),action,unlocked);
                return;
            }
            gameObject.SetActive(true); confirmation.SetActive(false); levelSelection.SetActive(false); eventBriefing.SetActive(true);
            bool career = mode == "career";
            string freeTitle = mode == "time_trial" ? FormulaLocalization.T("TIME ATTACK") : e.mode == "race" ? FormulaLocalization.T("QUICK RACE") : FormulaLocalization.T("PRACTICE");
            confirm = action; eventTitle.text = career ? FormulaLocalization.T(e.name).ToUpperInvariant() : freeTitle; eventMap.SetTrack(e.trackId);
            var track = FormulaAssets.Current.FindTrack(e.trackId); float t = track.referenceLapSeconds;
            eventState.text = mode == "career" ? FormulaLocalization.T("CAREER / EVENT ") + e.id.Substring(1) + " · " + FormulaSeasonCalendar.Current.Caption(e) + " " + FormulaSeasonCalendar.Current.year : mode == "time_trial" ? FormulaLocalization.T("TIME ATTACK") : FormulaLocalization.T("FREE RACE");
            eventTrack.text = FormulaLocalization.T(track.displayName) + "\n" + e.laps + (e.laps == 1 ? FormulaLocalization.T(" lap") : FormulaLocalization.T(" laps")) + "  ·  " + (e.opponentCount == 0 ? FormulaLocalization.T("Solo") : e.opponentCount + FormulaLocalization.T(" opponents"));
            eventGoal.text = career ? FormulaMenuFormatting.EventGoal(e) : e.mode == "race" ? FormulaLocalization.T("Race for your best result on this circuit.") : FormulaLocalization.T("Learn the circuit and complete valid laps.");
            eventVehicle.text = (mode == "career" && e.vehicleRule == "stock_balanced") || mode == "time_trial" ? FormulaLocalization.T("STOCK CAR\nBalanced setup") : FormulaLocalization.T("YOUR CAR\nEquipped upgrades and setup");
            medalGoals[0].text = e.mode == "race" ? FormulaLocalization.T("Finish in the top 3") : e.mode == "familiarization" ? FormulaLocalization.T("Complete a valid lap") : "≤ " + FormulaRaceUI.TimeText(t * 1.2);
            medalGoals[1].text = e.mode == "race" ? FormulaLocalization.T("Finish in the top 2") : "≤ " + FormulaRaceUI.TimeText(t * (e.mode == "familiarization" ? 1.25 : 1.1));
            medalGoals[2].text = e.mode == "race" ? FormulaLocalization.T("Win the race") : "≤ " + FormulaRaceUI.TimeText(t * (e.mode == "familiarization" ? 1.12 : 1.03));
            if (medalTiers != null && medalTiers.Length == 3)
            {
                string[] tiers = career ? new[] { "BRONZE", "SILVER", "GOLD" } : new[] { "FORMAT", "RIVALS", "SESSION" };
                for (int i = 0; i < 3; i++) medalTiers[i].text = FormulaLocalization.T(tiers[i]);
            }
            if (!career)
            {
                medalGoals[0].text = e.laps + (e.laps == 1 ? FormulaLocalization.T(" lap") : FormulaLocalization.T(" laps"));
                medalGoals[1].text = e.opponentCount == 0 ? FormulaLocalization.T("No opponents") : e.opponentCount + FormulaLocalization.T(" AI drivers");
                medalGoals[2].text = mode == "time_trial" ? FormulaLocalization.T("Stock car") : e.mode == "race" ? FormulaLocalization.T("Race") : FormulaLocalization.T("Practice");
            }
            eventReward.text = career ? FormulaLocalization.T("TOTAL REWARD") : FormulaLocalization.T("NO CAREER REWARDS");
            if (medalRewards != null && medalRewards.Length == 3)
            {
                FormulaProgression.State.claimedMedalMasks.TryGetValue(e.id, out int claimed);
                int credits = e.baseValidFinishReward.credits, xp = e.baseValidFinishReward.xp;
                for (int i = 0; i < 3; i++)
                {
                    if ((claimed & (1 << (i + 1))) == 0)
                    {
                        var bonus = e.oneTimeMedalRewards[((Medal)(i + 1)).ToString().ToLowerInvariant()]; credits += bonus.credits; xp += bonus.xp;
                    }
                    medalRewards[i].text = career ? credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" CR · ") + xp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" XP") : "—";
                }
            }
            eventNote.text = !unlocked ? FormulaLocalization.T("LOCKED · ") + FormulaProgression.LockReason(e.unlock, FormulaProgression.State) : !career ? FormulaLocalization.T("No career credits or XP. Your career progress is unchanged.") : FormulaLocalization.T("Rewards include your finish reward and unclaimed medal bonuses.\nValid laps require staying within track limits and passing every checkpoint.");
            eventAccept.interactable = unlocked; eventActionText.text = unlocked ? FormulaLocalization.T("START EVENT") : FormulaLocalization.T("EVENT LOCKED");
            FormulaUIMotion.Enter(gameObject, true);
            Wire(eventBriefing.transform); UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(unlocked ? eventAccept.gameObject : eventCancel.gameObject);
        }
        private static void Wire(Transform panel)
        {
            var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsInteractable()).ToArray();
            for (int i = 0; i < buttons.Length; i++)
            {
                var previous = buttons[(i + buttons.Length - 1) % buttons.Length]; var next = buttons[(i + 1) % buttons.Length];
                buttons[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = previous, selectOnDown = next, selectOnLeft = previous, selectOnRight = next };
            }
        }
    }
}
