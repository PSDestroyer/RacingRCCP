using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace FormulaGame
{
    public sealed class FormulaLiveryCard : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [SerializeField] private string liveryId;
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private TMP_Text ownership;
        [SerializeField] private TMP_Text liveryName;
        [SerializeField] private GameObject equippedMarker;
        private Action<string> select;
        private bool displayed, wasEquipped, wasOwned;
        public string LiveryId => liveryId;
        public void Bind(Action<string> callback) { select = callback; button.onClick.AddListener(() => select(liveryId)); }
        public void OnSelect(BaseEventData data) => select?.Invoke(liveryId);
        public void OnPointerEnter(PointerEventData data) => select?.Invoke(liveryId);
        public void Refresh()
        {
            var p = FormulaProgression.State; bool equipped = p.equippedLivery == liveryId;
            bool owned = p.ownedLiveries.Contains(liveryId);
            var livery = FormulaCatalog.Current.Skin(liveryId);
            if (liveryName != null) liveryName.text = FormulaLocalization.T(livery.displayName);
            ownership.text = equipped ? FormulaLocalization.T("EQUIPPED") : p.ownedLiveries.Contains(liveryId) ? FormulaLocalization.T("OWNED") : livery.acquisition == "credit_purchase" ? livery.priceCredits.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" CR") : FormulaLocalization.T("REWARD");
            ownership.color = Color.white;
            equippedMarker.SetActive(equipped);
            if (displayed && (equipped && !wasEquipped || owned && !wasOwned))
            {
                if (equipped) FormulaUIMotion.Enter(equippedMarker, true);
                FormulaUIMotion.Pulse(ownership.gameObject, .055f, .24f);
            }
            displayed = true; wasEquipped = equipped; wasOwned = owned;
        }
    }
}
