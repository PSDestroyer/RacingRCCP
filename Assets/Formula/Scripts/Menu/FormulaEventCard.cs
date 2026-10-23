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
        public string EventId => eventId;
        public string Mode => mode;
        public void Bind(Action<string, string> inspect) => button.onClick.AddListener(() => inspect(eventId, mode));
        public void Refresh(int index)
        {
            var e = FormulaCatalog.Current.Event(eventId); var p = FormulaProgression.State;
            if (figmaLayout)
            {
                eventName.text = FormulaAssets.Current.FindTrack(e.trackId).displayName.Replace(" Circuit", "").ToUpperInvariant();
                stage.text = "Round " + (index + 1);
                trackName.text = e.name;
                state.text = FormulaSeasonCalendar.Current.Caption(e);
                if (missionIcon != null) missionIcon.SetEvent(e);
                GetComponent<FormulaMenuFocus>()?.RefreshVisuals();
                return;
            }
            bool unlocked = mode != "career" || FormulaProgression.Unlocked(e.unlock, p);
            if (stage != null) { stage.text = "EVENT " + (index + 1).ToString("00");  }
            if (eventName != null && mode == "career") eventName.text = e.name;
            trackName.text = FormulaAssets.Current.FindTrack(e.trackId).displayName + (mode == "career" ? " · " + e.laps + (e.laps == 1 ? " lap" : " laps") : "");
            if (format != null) format.text = e.laps + (e.laps == 1 ? " LAP" : " LAPS") + "  /  " + e.opponentCount + " OPPONENTS";
            if (state != null) state.text = mode == "career" ? (unlocked ? (p.Best(e.id) == Medal.None ? "AVAILABLE" : FormulaUI.MedalName(p.Best(e.id)).ToUpperInvariant()) : "LOCKED") : "NO CAREER REWARDS";
            if (objective != null) objective.text = e.mode == "race" ? "Finish in the top 3" : e.mode == "familiarization" ? "Complete a valid lap" : "Lap time ≤ " + FormulaRaceUI.TimeText(FormulaAssets.Current.FindTrack(e.trackId).referenceLapSeconds * 1.2);
            if (reward != null)
            {
                var available = FormulaMenuFormatting.AvailableBronzeReward(e);
                reward.text = mode == "career" ? available.credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR  ·  " + available.xp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " XP" : "CHOOSE YOUR CAR SETUP";
            }
            GetComponent<FormulaMenuFocus>()?.RefreshVisuals();
        }
    }
}
