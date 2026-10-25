using System.Linq;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    // The result receipt is immutable; this view only presents it and routes the next action.
    public sealed class FormulaResultsView : MonoBehaviour, UnityEngine.EventSystems.ICancelHandler
    {
        [SerializeField] private TMP_Text eventName, medal, elapsed, bestLap, credits, xp, bonus, wallet, driver, unlocks;
        [SerializeField] private UnityEngine.UI.Button retry, next, menu;
        [SerializeField] private TMP_Text progress, target, nextContext;
        [SerializeField] private UnityEngine.UI.Image xpFill;
        [SerializeField] private UnityEngine.UI.RawImage rewardLivery;
        public void OnCancel(UnityEngine.EventSystems.BaseEventData data) => FormulaSession.ReturnToMenu();
        public void Present(FormulaReceipt receipt)
        {
            eventName.text = FormulaCatalog.Current.Event(receipt.eventId).name.ToUpperInvariant();
            bool career = FormulaProgression.State.activeMode == "career";
            var session = FormulaCatalog.Current.Event(receipt.eventId);
            medal.text = career ? FormulaUI.MedalName(receipt.medal).ToUpperInvariant()
                : receipt.bestLapSeconds <= 0 ? "NO VALID LAP"
                : session.mode == "race" ? "P" + receipt.finishPosition
                : receipt.personalBest ? "PERSONAL BEST" : "SESSION COMPLETE";
            elapsed.text = FormulaRaceUI.TimeText(receipt.elapsedSeconds); bestLap.text = FormulaRaceUI.TimeText(receipt.bestLapSeconds);
            credits.text = "+" + receipt.credits.ToString("N0") + " CR"; xp.text = "+" + receipt.xp.ToString("N0") + " XP";
            bonus.gameObject.SetActive(receipt.levelCredits > 0);
            bonus.text = "LEVEL UP BONUS  +" + receipt.levelCredits.ToString("N0") + " CR";
            var data = FormulaProgression.Data;
            string xpText = FormulaMenuFormatting.Experience(data.exp, data.currentLevel, out float fraction);
            if (progress != null) progress.text = "LEVEL " + data.currentLevel + "  /  " + xpText;
            if (xpFill != null) xpFill.fillAmount = fraction;
            if (target != null) target.text = NextTarget(receipt);
            wallet.text = FormulaProgression.Data.money.ToString("N0"); driver.text = FormulaMenuFormatting.DriverName;
            unlocks.text = receipt.newLiveries.Count == 0 ? receipt.personalBest ? "PERSONAL BEST\nProgress saved" : "PROGRESS SAVED" : "LIVERY UNLOCKED\n" + string.Join(" · ", receipt.newLiveries.Select(id => FormulaCatalog.Current.Skin(id).displayName));
            unlocks.color = receipt.newLiveries.Count > 0 ? FormulaUI.Accent : Color.white;
            if (rewardLivery != null)
            {
                rewardLivery.gameObject.SetActive(receipt.newLiveries.Count > 0);
                if (receipt.newLiveries.Count > 0) rewardLivery.texture = FormulaMenuAssets.Current.liveries[FormulaCatalog.Current.liveries.FindIndex(l => l.id == receipt.newLiveries[0])];
            }
            retry.onClick.AddListener(() => FormulaSession.Launch(receipt.eventId, FormulaProgression.State.activeMode));
            var nextEvent = career ? FormulaCatalog.Current.careerEvents.Find(e => FormulaProgression.State.Best(e.id) < Medal.Bronze) : null;
            next.gameObject.SetActive(nextEvent != null); if (nextEvent != null) next.onClick.AddListener(() => FormulaSession.ReviewEvent(nextEvent.id));
            if (nextContext != null) nextContext.text = nextEvent == null ? "" : "UP NEXT  /  " + nextEvent.name.ToUpperInvariant() + " · " + FormulaAssets.Current.FindTrack(nextEvent.trackId).displayName;
            if (nextEvent != null) next.GetComponentInChildren<TMP_Text>().text = nextEvent.id == receipt.eventId ? "REVIEW EVENT" : "NEXT EVENT";
            menu.onClick.AddListener(FormulaSession.ReturnToMenu);
            var buttons = new[] { retry, next, menu }.Where(b => b.gameObject.activeSelf).ToArray();
            for (int i = 0; i < buttons.Length; i++) buttons[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length], selectOnRight = buttons[(i + 1) % buttons.Length] };
            if (nextEvent == null)
            {
                ResizeAction(retry, 30, 600); ResizeAction(menu, 650, 600);
            }
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(nextEvent != null ? next.gameObject : menu.gameObject);
        }
        private static string NextTarget(FormulaReceipt r)
        {
            if (FormulaProgression.State.activeMode != "career")
            {
                string mode = FormulaProgression.State.activeMode == "time_trial" ? "Time Attack"
                    : FormulaCatalog.Current.Event(r.eventId).mode == "race" ? "Quick Race" : "Practice";
                return mode + " · No career credits or XP";
            }
            if (r.medal == Medal.Gold) return "GOLD TARGET ACHIEVED";
            var e = FormulaCatalog.Current.Event(r.eventId);
            var tier = r.medal == Medal.None ? Medal.Bronze : r.medal + 1;
            if (e.mode == "race") return "NEXT MEDAL  /  " + tier.ToString().ToUpperInvariant() + " · Finish " + (tier == Medal.Gold ? "1st" : tier == Medal.Silver ? "in the top 2" : "in the top 3") + (r.finishPosition > 0 ? " · You finished P" + r.finishPosition : "");
            double reference = FormulaAssets.Current.FindTrack(e.trackId).referenceLapSeconds;
            double factor = tier == Medal.Gold ? e.mode == "familiarization" ? 1.12 : 1.03 : tier == Medal.Silver ? e.mode == "familiarization" ? 1.25 : 1.1 : 1.2;
            double goal = reference * factor, actual = e.mode == "familiarization" ? r.elapsedSeconds : r.bestLapSeconds;
            if (e.mode == "familiarization" && tier == Medal.Bronze) return "NEXT MEDAL  /  BRONZE · Complete a valid lap";
            return "NEXT MEDAL  /  " + tier.ToString().ToUpperInvariant() + " · " + FormulaRaceUI.TimeText(goal) + (actual > goal ? " · Improve by " + (actual - goal).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " s" : " · Complete a valid lap");
        }
        private static void ResizeAction(UnityEngine.UI.Button button, float x, float width)
        {
            FormulaMenuTheme.Place((RectTransform)button.transform, x, 532, width, 64);
            foreach (var surface in button.GetComponentsInChildren<FormulaPanelGraphic>()) FormulaMenuTheme.Place(surface.rectTransform, 0, 0, width, 64);
            var label = button.GetComponentInChildren<TMP_Text>(); FormulaMenuTheme.Place(label.rectTransform, 18, 0, width - 36, 64);
        }
    }
}
