using System;
using System.Linq;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaProfileView : MonoBehaviour
    {
        [Serializable] public sealed class MedalRow { public string eventId; public TMP_Text medal; }
        [SerializeField] private TMP_Text pilotName, summary;
        [SerializeField] private MedalRow[] medals;
        public void Refresh()
        {
            var p = FormulaProgression.State; var d = FormulaProgression.Data;
            pilotName.text = FormulaMenuFormatting.DriverName;
            summary.text = $"LEVEL {d.currentLevel}\n{d.exp:N0} XP\n{p.ownedLiveries.Count}/8 LIVERIES\n{FormulaCatalog.Current.careerEvents.Count(e => p.Best(e.id) >= Medal.Bronze)}/6 EVENTS COMPLETED";
            foreach (var row in medals) row.medal.text = FormulaUI.MedalName(p.Best(row.eventId));
        }
    }
}
