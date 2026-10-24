using System;
using System.Linq;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaProfileView : MonoBehaviour
    {
        [Serializable] public sealed class MedalRow { public string eventId; public TMP_Text medal, record; }
        [SerializeField] private TMP_Text pilotName, summary;
        [SerializeField] private MedalRow[] medals;
        [SerializeField] private TMP_Text nextReward, experience;
        [SerializeField] private UnityEngine.UI.Image xpFill;
        public void Refresh()
        {
            var p = FormulaProgression.State; var d = FormulaProgression.Data;
            pilotName.text = FormulaMenuFormatting.DriverName;
            summary.text = $"LEVEL {d.currentLevel}\n{p.ownedLiveries.Count}/8 LIVERIES\n{FormulaCatalog.Current.careerEvents.Count(e => p.Best(e.id) >= Medal.Bronze)}/6 EVENTS COMPLETE";
            string xpText = FormulaMenuFormatting.Experience(d.exp, d.currentLevel, out float fraction);
            if (experience != null) experience.text = xpText;
            if (xpFill != null) xpFill.fillAmount = fraction;
            var reward = FormulaCatalog.Current.liveries.Where(l => l.acquisition == "achievement_grant" && !p.ownedLiveries.Contains(l.id)).OrderBy(l => (string)l.unlock["eventId"] ?? "zz").FirstOrDefault();
            if (nextReward != null) nextReward.text = reward == null ? "ALL CAREER LIVERIES UNLOCKED" : "NEXT LIVERY  /  " + reward.displayName + "\n" + FormulaProgression.LockReason(reward.unlock, p).Replace(" · ", " in ");
            foreach (var row in medals)
            {
                row.medal.text = p.Best(row.eventId) == Medal.None ? "NO MEDAL" : FormulaUI.MedalName(p.Best(row.eventId)).ToUpperInvariant();
                row.medal.color = p.Best(row.eventId) == Medal.Gold ? FormulaUI.Accent : Color.white;
                if (row.record != null) row.record.text = "BEST LAP  " + (p.bestLapSeconds.TryGetValue(row.eventId, out double seconds) ? FormulaRaceUI.TimeText(seconds) : "—");
            }
        }
    }
}
