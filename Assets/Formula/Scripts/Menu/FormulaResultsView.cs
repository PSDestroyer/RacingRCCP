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
        public void OnCancel(UnityEngine.EventSystems.BaseEventData data) => FormulaSession.ReturnToMenu();
        public void Present(FormulaReceipt receipt)
        {
            eventName.text = FormulaCatalog.Current.Event(receipt.eventId).name.ToUpperInvariant();
            medal.text = FormulaUI.MedalName(receipt.medal).ToUpperInvariant();
            elapsed.text = FormulaRaceUI.TimeText(receipt.elapsedSeconds); bestLap.text = FormulaRaceUI.TimeText(receipt.bestLapSeconds);
            credits.text = "+" + receipt.credits.ToString("N0") + " CR"; xp.text = "+" + receipt.xp.ToString("N0") + " XP";
            bonus.text = "Level bonus: +" + receipt.levelCredits.ToString("N0") + " CR";
            wallet.text = FormulaProgression.Data.money.ToString("N0"); driver.text = FormulaMenuFormatting.DriverName;
            unlocks.text = receipt.newLiveries.Count == 0 ? "Progress saved" : "LIVERY UNLOCKED\n" + string.Join(" · ", receipt.newLiveries.Select(id => FormulaCatalog.Current.Skin(id).displayName));
            retry.onClick.AddListener(() => FormulaSession.Launch(receipt.eventId, FormulaProgression.State.activeMode));
            bool career = FormulaProgression.State.activeMode == "career";
            var nextEvent = career ? FormulaCatalog.Current.careerEvents.Find(e => FormulaProgression.State.Best(e.id) < Medal.Bronze) : null;
            next.gameObject.SetActive(nextEvent != null); if (nextEvent != null) next.onClick.AddListener(() => FormulaSession.Launch(nextEvent.id, "career"));
            menu.onClick.AddListener(FormulaSession.ReturnToMenu);
            var buttons = new[] { retry, next, menu }.Where(b => b.gameObject.activeSelf).ToArray();
            for (int i = 0; i < buttons.Length; i++) buttons[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length], selectOnRight = buttons[(i + 1) % buttons.Length] };
            if (nextEvent == null)
            {
                ResizeAction(retry, 30, 600); ResizeAction(menu, 650, 600);
            }
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(nextEvent != null ? next.gameObject : menu.gameObject);
        }
        private static void ResizeAction(UnityEngine.UI.Button button, float x, float width)
        {
            FormulaMenuTheme.Place((RectTransform)button.transform, x, 532, width, 64);
            foreach (var surface in button.GetComponentsInChildren<FormulaPanelGraphic>()) FormulaMenuTheme.Place(surface.rectTransform, 0, 0, width, 64);
            var label = button.GetComponentInChildren<TMP_Text>(); FormulaMenuTheme.Place(label.rectTransform, 18, 0, width - 36, 64);
        }
    }
}
