using System;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaEventCard : MonoBehaviour
    {
        [SerializeField] private string eventId, mode = "career";
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private TMP_Text stage, eventName, trackName, format, state;
        [SerializeField] private TMP_Text reward, objective;
        [SerializeField] private bool figmaLayout;
        [SerializeField] private FormulaMissionIcon missionIcon;
        [SerializeField] private TMP_Text availability, earnedMedal, cardAction;
        public string EventId => eventId;
        public string Mode => mode;
        public void Bind(Action<string, string> inspect) => button.onClick.AddListener(() => inspect(eventId, mode));
        public void ShowCareer(FormulaCatalog.CareerEvent entry, int index)
        {
            eventId = entry.id; mode = "career"; gameObject.name = entry.id;
            var map = GetComponentInChildren<FormulaTrackMap>(true);
            if (map != null) map.SetTrack(entry.trackId);
            Refresh(index);
        }
        public void ShowCircuit(FormulaAssets.Track track, int index, string session)
        {
            eventId = session + ":" + track.id; mode = session == "trial" ? "time_trial" : "quick";
            if (eventName != null) eventName.text = FormulaLocalization.T(track.displayName).ToUpperInvariant();
            if (trackName != null) trackName.text = FormulaLocalization.T("REVIEW SESSION  ›");
            if (stage != null) stage.text = FormulaLocalization.T("CIRCUIT ") + (index + 1).ToString("00");
            if (format != null) format.text = track.lengthMeters > 0 ? (track.lengthMeters / 1000).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" KM") : FormulaLocalization.T("CLOSED CIRCUIT");
            var map = GetComponentInChildren<FormulaTrackMap>(true); if (map != null) map.SetTrack(track.id);
            GetComponent<FormulaMenuFocus>()?.RefreshVisuals();
        }
        public void Refresh(int index)
        {
            var e = FormulaCatalog.Current.Event(eventId); var p = FormulaProgression.State;
            if (figmaLayout)
            {
                eventName.text = FormulaLocalization.T(e.name).ToUpperInvariant();
                stage.text = FormulaLocalization.T("Round ") + (index + 1);
                trackName.text = FormulaLocalization.T(FormulaAssets.Current.FindTrack(e.trackId).displayName);
                bool open = FormulaProgression.Unlocked(e.unlock, p);
                var best = p.Best(e.id);
                var upcoming = FormulaCatalog.Current.careerEvents.Find(entry => p.Best(entry.id) < Medal.Bronze);
                if (availability != null)
                {
                    availability.text = !open ? FormulaLocalization.T("LOCKED") : best >= Medal.Bronze ? FormulaLocalization.T("COMPLETED") : upcoming?.id == e.id ? FormulaLocalization.T("NEXT EVENT") : FormulaLocalization.T("AVAILABLE");
                    availability.color = !open || upcoming?.id == e.id ? FormulaUI.Accent : Color.white;
                }
                if (earnedMedal != null) earnedMedal.text = best == Medal.None ? "" : FormulaLocalization.F("{0} MEDAL", FormulaUI.MedalName(best).ToUpperInvariant());
                if (cardAction != null) cardAction.text = open ? FormulaLocalization.T("VIEW EVENT  ›") : FormulaLocalization.T("VIEW REQUIREMENT  ›");
                state.text = FormulaSeasonCalendar.Current.Caption(e);
                if (missionIcon != null) missionIcon.SetEvent(e);
                GetComponent<FormulaMenuFocus>()?.RefreshVisuals();
                return;
            }
            bool unlocked = mode != "career" || FormulaProgression.Unlocked(e.unlock, p);
            if (stage != null) { stage.text = FormulaLocalization.T("EVENT ") + (index + 1).ToString("00");  }
            if (eventName != null && mode == "career") eventName.text = FormulaLocalization.T(e.name);
            trackName.text = FormulaLocalization.T(FormulaAssets.Current.FindTrack(e.trackId).displayName) + (mode == "career" ? " · " + e.laps + (e.laps == 1 ? FormulaLocalization.T(" lap") : FormulaLocalization.T(" laps")) : "");
            if (format != null) format.text = e.laps + (e.laps == 1 ? FormulaLocalization.T(" LAP") : FormulaLocalization.T(" LAPS")) + "  /  " + e.opponentCount + FormulaLocalization.T(" OPPONENTS");
            if (state != null) state.text = mode == "career" ? (unlocked ? (p.Best(e.id) == Medal.None ? FormulaLocalization.T("AVAILABLE") : FormulaUI.MedalName(p.Best(e.id)).ToUpperInvariant()) : FormulaLocalization.T("LOCKED")) : FormulaLocalization.T("NO CAREER REWARDS");
            if (objective != null) objective.text = e.mode == "race" ? FormulaLocalization.T("Finish in the top 3") : e.mode == "familiarization" ? FormulaLocalization.T("Complete a valid lap") : FormulaLocalization.T("Lap time ≤ ") + FormulaRaceUI.TimeText(FormulaAssets.Current.FindTrack(e.trackId).referenceLapSeconds * 1.2);
            if (reward != null)
            {
                var available = FormulaMenuFormatting.AvailableBronzeReward(e);
                reward.text = mode == "career" ? available.credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" CR  ·  ") + available.xp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" XP") : FormulaLocalization.T("CHOOSE YOUR CAR SETUP");
            }
            GetComponent<FormulaMenuFocus>()?.RefreshVisuals();
        }
    }
}
