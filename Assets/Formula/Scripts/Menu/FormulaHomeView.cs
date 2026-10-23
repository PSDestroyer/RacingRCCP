using System;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaHomeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nextStage, nextEvent, currentLivery;
        [SerializeField] private UnityEngine.UI.Button continueCareer, garage, development, quick, profile, settings;
        [SerializeField] private UnityEngine.UI.Button careerOverview;
        [SerializeField] private TMP_Text goal, reward, progress;
        public void BindCareerOverview(Action showCareer) { if (careerOverview != null) careerOverview.onClick.AddListener(() => showCareer()); }
        public void Bind(Action continueAction, Action garageAction, Action developmentAction, Action quickAction, Action profileAction, Action settingsAction)
        {
            continueCareer.onClick.AddListener(() => continueAction()); garage.onClick.AddListener(() => garageAction());
            development.onClick.AddListener(() => developmentAction()); quick.onClick.AddListener(() => quickAction());
            profile.onClick.AddListener(() => profileAction()); settings.onClick.AddListener(() => settingsAction());
        }
        public void Refresh()
        {
            var next = FormulaCatalog.Current.careerEvents.Find(e => FormulaProgression.State.Best(e.id) < Medal.Bronze) ?? FormulaCatalog.Current.Event("g06");
            int completed = FormulaCatalog.Current.careerEvents.FindAll(e => FormulaProgression.State.Best(e.id) >= Medal.Bronze).Count;
            nextStage.text = completed == 6 ? "CAREER COMPLETE / 6 OF 6" : "YOUR CAREER / EVENT " + next.id.Substring(1);
            nextEvent.text = next.name.ToUpperInvariant();
            continueCareer.GetComponentInChildren<TMP_Text>(true).text = completed == 6 ? "REPLAY FINAL" : "CONTINUE CAREER";
            currentLivery.text = FormulaCatalog.Current.Skin(FormulaProgression.State.equippedLivery).displayName;
            if (goal != null) goal.text = FormulaMenuFormatting.EventGoal(next);
            var available = FormulaMenuFormatting.AvailableBronzeReward(next);
            if (reward != null) reward.text = "BRONZE TARGET   /   " + available.credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR  +  " + available.xp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " XP";
            if (progress != null) progress.text = FormulaCatalog.Current.careerEvents.FindAll(e => FormulaProgression.State.Best(e.id) >= Medal.Bronze).Count + " / 6 EVENTS  ·  " + FormulaMenuFormatting.NextReward();
        }
    }
}
