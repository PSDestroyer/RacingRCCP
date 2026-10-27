using System;
using System.Linq;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaProfileView : MonoBehaviour
    {
        [Serializable] public sealed class MedalRow { public string eventId; public TMP_Text medal, record, eventName; }
        [SerializeField] private TMP_Text pilotName, summary;
        [SerializeField] private MedalRow[] medals;
        [SerializeField] private TMP_Text nextReward, experience;
        [SerializeField] private UnityEngine.UI.Image xpFill;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private UnityEngine.UI.Button previousPage, nextPage;
        private int page;
        public int Page => page;
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)FormulaCatalog.Current.careerEvents.Count / medals.Length));
        public void Bind()
        {
            if (previousPage != null) previousPage.onClick.AddListener(() => SetPage(page - 1));
            if (nextPage != null) nextPage.onClick.AddListener(() => SetPage(page + 1));
        }
        public void SetPage(int index) { page = (index % PageCount + PageCount) % PageCount; Refresh(); }
        public void Refresh()
        {
            var p = FormulaProgression.State; var d = FormulaProgression.Data;
            pilotName.text = FormulaMenuFormatting.DriverName;
            var catalog = FormulaCatalog.Current.careerEvents;
            summary.text = FormulaLocalization.F("LEVEL {0}\n{1}/{2} LIVERIES\n{3}/{4} EVENTS COMPLETE", d.currentLevel, p.ownedLiveries.Count, FormulaCatalog.Current.liveries.Count, catalog.Count(e => p.Best(e.id) >= Medal.Bronze), catalog.Count);
            string xpText = FormulaMenuFormatting.Experience(d.exp, d.currentLevel, out float fraction);
            if (experience != null) experience.text = xpText;
            if (xpFill != null) xpFill.fillAmount = fraction;
            var reward = FormulaCatalog.Current.liveries.Where(l => l.acquisition == "achievement_grant" && !p.ownedLiveries.Contains(l.id)).OrderBy(l => (string)l.unlock["eventId"] ?? "zz").FirstOrDefault();
            if (nextReward != null) nextReward.text = reward == null ? FormulaLocalization.T("ALL CAREER LIVERIES UNLOCKED") : FormulaLocalization.T("NEXT LIVERY  /  ") + FormulaLocalization.T(reward.displayName) + "\n" + FormulaProgression.LockReason(reward.unlock, p);
            page = Mathf.Clamp(page, 0, PageCount - 1);
            for (int i = 0; i < medals.Length; i++)
            {
                var row = medals[i]; int index = page * medals.Length + i;
                row.medal.transform.parent.gameObject.SetActive(index < catalog.Count);
                if (index >= catalog.Count) continue;
                row.eventId = catalog[index].id;
                if (row.eventName != null) row.eventName.text = (index + 1).ToString("00") + "  /  " + FormulaLocalization.T(catalog[index].name);
                row.medal.text = p.Best(row.eventId) == Medal.None ? FormulaLocalization.T("NO MEDAL") : FormulaUI.MedalName(p.Best(row.eventId)).ToUpperInvariant();
                row.medal.color = p.Best(row.eventId) == Medal.Gold ? FormulaUI.Accent : Color.white;
                if (row.record != null) row.record.text = FormulaLocalization.T("BEST LAP  ") + (p.bestLapSeconds.TryGetValue(row.eventId, out double seconds) ? FormulaRaceUI.TimeText(seconds) : "—");
            }
            if (pageLabel != null) pageLabel.text = FormulaLocalization.T("EVENTS ") + (page * medals.Length + 1).ToString("00") + "–" + Mathf.Min((page + 1) * medals.Length, catalog.Count).ToString("00") + "  /  " + catalog.Count.ToString("00");
            if (previousPage != null) previousPage.interactable = PageCount > 1;
            if (nextPage != null) nextPage.interactable = PageCount > 1;
        }
    }
}
