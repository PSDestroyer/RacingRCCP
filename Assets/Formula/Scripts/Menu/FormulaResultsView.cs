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
        [SerializeField] private TMP_Text resultCaption;
        [SerializeField] private RectTransform progressionRoot;
        [SerializeField] private TMP_Text[] metricCaptions;
        [SerializeField] private CanvasGroup rewardPresentation;
        [SerializeField] private CanvasGroup resultPresentation;
        private float revealStarted;
        public void OnCancel(UnityEngine.EventSystems.BaseEventData data) => FormulaSession.ReturnToMenu();
        public void Present(FormulaReceipt receipt)
        {
            eventName.text = FormulaCatalog.Current.Event(receipt.eventId).name.ToUpperInvariant();
            bool career = FormulaProgression.State.activeMode == "career";
            var session = FormulaCatalog.Current.Event(receipt.eventId);
            bool noValidLap = receipt.bestLapSeconds <= 0;
            bool failed = noValidLap || career && receipt.medal == Medal.None;
            if (resultCaption != null) resultCaption.text = failed ? "SESSION ENDED" : career ? "EVENT PASSED" : "SESSION COMPLETE";
            medal.text = noValidLap ? "NO VALID LAP" : career && failed ? session.mode == "race" ? "OUTSIDE TOP 3" : "TARGET MISSED"
                : career ? FormulaUI.MedalName(receipt.medal).ToUpperInvariant()
                : receipt.bestLapSeconds <= 0 ? "NO VALID LAP"
                : session.mode == "race" ? "P" + receipt.finishPosition
                : receipt.personalBest ? "PERSONAL BEST" : "SESSION COMPLETE";
            medal.color = failed ? FormulaUI.Red : FormulaUI.Accent;
            elapsed.text = FormulaRaceUI.TimeText(receipt.elapsedSeconds); bestLap.text = FormulaRaceUI.TimeText(receipt.bestLapSeconds);
            credits.text = "+" + receipt.credits.ToString("N0") + " CR"; xp.text = "+" + receipt.xp.ToString("N0") + " XP";
            bonus.gameObject.SetActive(receipt.levelCredits > 0);
            bonus.text = "LEVEL UP BONUS  +" + receipt.levelCredits.ToString("N0") + " CR";
            var data = FormulaProgression.Data;
            string xpText = FormulaMenuFormatting.Experience(data.exp, data.currentLevel, out float fraction);
            if (progress != null) progress.text = "LEVEL " + data.currentLevel + "  /  " + xpText;
            if (xpFill != null) xpFill.fillAmount = fraction;
            if (target != null) target.text = noValidLap ? "Complete a lap within track limits, passing every checkpoint."
                : failed && session.mode == "race" ? "Finish in the top 3. You finished P" + receipt.finishPosition + "."
                : failed ? "Record a valid lap in " + FormulaRaceUI.TimeText(FormulaAssets.Current.FindTrack(session.trackId).referenceLapSeconds * 1.2) + " or faster.\nImprove by " + (receipt.bestLapSeconds - FormulaAssets.Current.FindTrack(session.trackId).referenceLapSeconds * 1.2).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " s."
                : NextTarget(receipt);
            wallet.text = FormulaProgression.Data.money.ToString("N0"); driver.text = FormulaMenuFormatting.DriverName;
            unlocks.text = receipt.newLiveries.Count == 0 ? receipt.personalBest && career ? "PERSONAL BEST\nResult saved" : "RESULT SAVED" : "LIVERY UNLOCKED\n" + string.Join(" · ", receipt.newLiveries.Select(id => FormulaCatalog.Current.Skin(id).displayName));
            unlocks.color = receipt.newLiveries.Count > 0 ? FormulaUI.Accent : Color.white;
            if (rewardLivery != null)
            {
                rewardLivery.gameObject.SetActive(receipt.newLiveries.Count > 0);
                if (receipt.newLiveries.Count > 0) rewardLivery.texture = FormulaMenuAssets.Current.liveries[FormulaCatalog.Current.liveries.FindIndex(l => l.id == receipt.newLiveries[0])];
            }
            LayoutOutcome(receipt, failed, career);
            revealStarted = Time.unscaledTime;
            if (resultPresentation != null) resultPresentation.alpha = FormulaProgression.State.reducedMotion ? 1 : 0;
            if (rewardPresentation != null) rewardPresentation.alpha = FormulaProgression.State.reducedMotion ? 1 : 0;
            FormulaAudio.Play(receipt.newLiveries.Count > 0 ? FormulaAudio.Cue.Unlock : failed ? FormulaAudio.Cue.End : FormulaAudio.Cue.Result);
            retry.onClick.RemoveAllListeners(); next.onClick.RemoveAllListeners(); menu.onClick.RemoveAllListeners();
            retry.GetComponentInChildren<TMP_Text>().text = "RETRY";
            menu.GetComponentInChildren<TMP_Text>().text = "MENU";
            retry.onClick.AddListener(() => FormulaSession.Launch(receipt.eventId, FormulaProgression.State.activeMode));
            var nextEvent = career && !failed ? FormulaCatalog.Current.careerEvents.Find(e => FormulaProgression.State.Best(e.id) < Medal.Bronze) : null;
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
            else
            {
                ResizeAction(retry, 30, 390); ResizeAction(next, 440, 390); ResizeAction(menu, 850, 400);
            }
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(failed ? retry.gameObject : nextEvent != null ? next.gameObject : menu.gameObject);
        }
        private void LayoutOutcome(FormulaReceipt receipt, bool failed, bool career)
        {
            // Outcomes lead; zero rewards never compete with the explanation or retry action.
            FormulaMenuTheme.Place(eventName.rectTransform, 30, 144, 1220, 60);
            FormulaMenuTheme.Place(medal.rectTransform, 30, 208, failed ? 1220 : 580, 78);
            bool earned = receipt.credits > 0 || receipt.xp > 0 || receipt.levelCredits > 0;
            bool singleLap = FormulaCatalog.Current.Event(receipt.eventId).laps == 1;
            bool livery = receipt.newLiveries.Count > 0;
            metricCaptions[0].text = singleLap && !failed ? "LAP TIME" : "SESSION TIME";
            if (singleLap && !failed) elapsed.text = FormulaRaceUI.TimeText(receipt.bestLapSeconds);
            metricCaptions[1].text = "BEST LAP";
            Metric(0, elapsed, failed ? 650 : 30, failed ? 300 : 320, 84);
            Metric(1, bestLap, failed ? 960 : 340, failed ? 300 : 320, 84);
            Metric(2, credits, 650, failed ? 402 : 208, failed ? 84 : 116);
            Metric(3, xp, 960, failed ? 402 : 208, failed ? 84 : 116);
            elapsed.transform.parent.gameObject.SetActive(true);
            bestLap.transform.parent.gameObject.SetActive(!singleLap && receipt.bestLapSeconds > 0);
            credits.transform.parent.gameObject.SetActive(receipt.credits > 0);
            xp.transform.parent.gameObject.SetActive(receipt.xp > 0);
            credits.color = FormulaUI.Accent; xp.color = FormulaUI.Accent;
            if (!career && !failed && !earned)
            {
                Metric(0, elapsed, 650, 208, 116); Metric(1, bestLap, 960, 208, 116);
            }
            progressionRoot.gameObject.SetActive(career && (receipt.xp > 0 || receipt.levelCredits > 0));
            float progressWidth = livery ? 260 : 600;
            FormulaMenuTheme.Place(progressionRoot, 650, failed ? 492 : livery ? 414 : 348, progressWidth, livery ? 50 : failed ? 36 : 52);
            FormulaMenuTheme.Place(progress.rectTransform, 0, 0, progressWidth, livery ? 48 : 28);
            progress.textWrappingMode = livery ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            var bar = (RectTransform)xpFill.transform.parent;
            FormulaMenuTheme.Place(bar, 0, livery ? 48 : failed ? 30 : 44, progressWidth, 4);
            FormulaMenuTheme.Place(xpFill.rectTransform, 0, 0, progressWidth, 4);
            FormulaMenuTheme.Place(target.rectTransform, 30, failed ? 292 : livery ? 414 : 482, failed || livery ? 600 : 1000, failed || livery ? 86 : 40);
            FormulaMenuTheme.Place(unlocks.rectTransform, livery ? 650 : 30, failed ? 394 : livery ? 342 : 414, livery ? 260 : 580, livery ? 66 : 62);
            FormulaMenuTheme.Place(bonus.rectTransform, failed ? 30 : 650, livery ? 474 : failed ? 464 : 402, failed ? 580 : livery ? 280 : 390, livery ? 38 : 28);
            bonus.textWrappingMode = livery ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            FormulaMenuTheme.Place(nextContext.rectTransform, 650, 432, 390, 44);
            nextContext.gameObject.SetActive(!livery);
            if (rewardLivery != null) FormulaMenuTheme.Place(rewardLivery.rectTransform, 930, 342, 320, 176);
        }
        private void Update()
        {
            // A short reveal never delays focus, navigation or the next session.
            if (resultPresentation != null && resultPresentation.alpha < 1)
                resultPresentation.alpha = FormulaProgression.State.reducedMotion ? 1 : Mathf.Clamp01((Time.unscaledTime - revealStarted) / .18f);
            if (rewardPresentation == null || !rewardLivery.gameObject.activeSelf || rewardPresentation.alpha >= 1) return;
            float t = Mathf.Clamp01((Time.unscaledTime - revealStarted) / .28f);
            rewardPresentation.alpha = FormulaProgression.State.reducedMotion ? 1 : t * t * (3 - 2 * t);
        }
        private void Metric(int index, TMP_Text value, float x, float y, float height)
        {
            var panel = (RectTransform)value.transform.parent;
            FormulaMenuTheme.Place(panel, x, y, 290, height);
            foreach (var surface in panel.GetComponentsInChildren<FormulaPanelGraphic>(true)) FormulaMenuTheme.Place(surface.rectTransform, 0, 0, 290, height);
            FormulaMenuTheme.Place(metricCaptions[index].rectTransform, 16, 4, 258, 26);
            FormulaMenuTheme.Place(value.rectTransform, 16, 30, 258, height - 30);
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
