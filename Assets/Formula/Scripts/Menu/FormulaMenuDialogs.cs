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
        private Action confirm, close;
        private Action<int> selectLevel;
        public bool IsOpen => gameObject.activeSelf;
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
            gameObject.SetActive(true); confirmation.SetActive(true); levelSelection.SetActive(false); if (eventBriefing != null) eventBriefing.SetActive(false);
            title.text = heading; details.text = description; acceptText.text = actionName;
            accept.interactable = canConfirm; confirm = action; Wire(confirmation.transform); FormulaUI.Focus(confirmation.transform);
        }
        public void SelectLevel(string branchId, Action<int> action)
        {
            gameObject.SetActive(true); confirmation.SetActive(false); levelSelection.SetActive(true); if (eventBriefing != null) eventBriefing.SetActive(false); selectLevel = action;
            levelTitle.text = FormulaCatalog.Current.Upgrade(branchId).name.ToUpperInvariant() + " / EQUIP";
            for (int i = 0; i < levels.Length; i++)
            {
                bool owned = i <= FormulaProgression.State.Owned(branchId);
                string state = FormulaProgression.State.Equipped(branchId) == i ? "EQUIPPED" : owned ? "OWNED" : "NOT PURCHASED";
                levels[i].name = "level-" + branchId + "-" + i;
                levels[i].interactable = owned; levelLabels[i].text = (i == 0 ? "STOCK CONFIGURATION" : "LEVEL " + i) + " / " + state;
            }
            Wire(levelSelection.transform); FormulaUI.Focus(levelSelection.transform);
        }
        public void Close() { confirm = null; selectLevel = null; gameObject.SetActive(false); }
        public void EventBriefing(FormulaCatalog.CareerEvent e, string mode, Action action, bool unlocked)
        {
            // Older authored prefabs remain playable until the explicit editor migration runs.
            if (eventBriefing == null)
            {
                Confirm(e.name, FormulaMenuFormatting.EventGoal(e) + "\n" + FormulaAssets.Current.FindTrack(e.trackId).displayName + "\n" + (unlocked ? "" : FormulaProgression.LockReason(e.unlock, FormulaProgression.State)), unlocked ? "Start race" : "Event locked", action, unlocked);
                return;
            }
            gameObject.SetActive(true); confirmation.SetActive(false); levelSelection.SetActive(false); eventBriefing.SetActive(true);
            bool career = mode == "career";
            string freeTitle = mode == "time_trial" ? "TIME ATTACK" : e.mode == "race" ? "QUICK RACE" : "PRACTICE";
            confirm = action; eventTitle.text = career ? e.name.ToUpperInvariant() : freeTitle; eventMap.SetTrack(e.trackId);
            var track = FormulaAssets.Current.FindTrack(e.trackId); float t = track.referenceLapSeconds;
            eventState.text = mode == "career" ? "CAREER / EVENT " + e.id.Substring(1) + " · " + FormulaSeasonCalendar.Current.Caption(e) + " " + FormulaSeasonCalendar.Current.year : mode == "time_trial" ? "TIME ATTACK" : "FREE RACE";
            eventTrack.text = track.displayName + "\n" + e.laps + (e.laps == 1 ? " lap" : " laps") + "  ·  " + (e.opponentCount == 0 ? "Solo" : e.opponentCount + " opponents");
            eventGoal.text = career ? FormulaMenuFormatting.EventGoal(e) : e.mode == "race" ? "Race for your best result on this circuit." : "Learn the circuit and complete valid laps.";
            eventVehicle.text = e.vehicleRule == "stock_balanced" || mode == "time_trial" ? "STOCK CAR\nBalanced setup" : "YOUR CAR\nEquipped upgrades and setup";
            medalGoals[0].text = e.mode == "race" ? "Finish in the top 3" : e.mode == "familiarization" ? "Complete a valid lap" : "≤ " + FormulaRaceUI.TimeText(t * 1.2);
            medalGoals[1].text = e.mode == "race" ? "Finish in the top 2" : "≤ " + FormulaRaceUI.TimeText(t * (e.mode == "familiarization" ? 1.25 : 1.1));
            medalGoals[2].text = e.mode == "race" ? "Win the race" : "≤ " + FormulaRaceUI.TimeText(t * (e.mode == "familiarization" ? 1.12 : 1.03));
            if (medalTiers != null && medalTiers.Length == 3)
            {
                string[] tiers = career ? new[] { "BRONZE", "SILVER", "GOLD" } : new[] { "FORMAT", "OPPONENTS", "SESSION" };
                for (int i = 0; i < 3; i++) medalTiers[i].text = tiers[i];
            }
            if (!career)
            {
                medalGoals[0].text = e.laps + (e.laps == 1 ? " lap" : " laps");
                medalGoals[1].text = e.opponentCount == 0 ? "No opponents" : e.opponentCount + " AI drivers";
                medalGoals[2].text = "Free practice";
            }
            eventReward.text = career ? "TOTAL REWARD" : "NO CAREER REWARDS";
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
                    medalRewards[i].text = career ? credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR · " + xp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " XP" : "—";
                }
            }
            eventNote.text = !unlocked ? "LOCKED · " + FormulaProgression.LockReason(e.unlock, FormulaProgression.State) : !career ? "No career credits or XP. Your career progress is unchanged." : "Totals include finish reward and unclaimed medal bonuses."
                + (track.referenceCalibrated || e.mode == "race" ? "" : "\nProvisional lap targets.");
            eventAccept.interactable = unlocked; eventActionText.text = unlocked ? "START EVENT" : "EVENT LOCKED";
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
