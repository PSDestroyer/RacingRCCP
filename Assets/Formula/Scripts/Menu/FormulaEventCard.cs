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
        [SerializeField] private TMP_Text reward;
        public string EventId => eventId;
        public string Mode => mode;
        public void Bind(Action<string, string> inspect) => button.onClick.AddListener(() => inspect(eventId, mode));
        public void Refresh(int index)
        {
            var e = FormulaCatalog.Current.Event(eventId); var p = FormulaProgression.State;
            bool unlocked = mode != "career" || FormulaProgression.Unlocked(e.unlock, p);
            if (stage != null) { stage.text = "EVENT " + (index + 1).ToString("00") + (unlocked ? "" : " / LOCKED"); stage.color = unlocked ? FormulaUI.Accent : Color.white; }
            if (eventName != null && mode == "career") eventName.text = e.name;
            trackName.text = FormulaAssets.Current.FindTrack(e.trackId).displayName;
            if (format != null) format.text = e.laps + (e.laps == 1 ? " LAP" : " LAPS") + "  /  " + e.opponentCount + " OPPONENTS";
            if (state != null) state.text = mode == "career" ? (unlocked ? FormulaUI.MedalName(p.Best(e.id)) : FormulaProgression.LockReason(e.unlock, p)) : "NO CAREER REWARDS";
            if (reward != null)
            {
                var available = FormulaMenuFormatting.AvailableBronzeReward(e);
                reward.text = mode == "career" ? "BRONZE  /  " + available.credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR  ·  " + available.xp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " XP" : "CHOOSE YOUR CAR SETUP";
            }
        }
    }
}
