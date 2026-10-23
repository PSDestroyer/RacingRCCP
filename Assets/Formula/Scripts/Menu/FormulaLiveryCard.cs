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
        [SerializeField] private GameObject equippedMarker;
        private Action<string> select;
        public string LiveryId => liveryId;
        public void Bind(Action<string> callback) { select = callback; button.onClick.AddListener(() => select(liveryId)); }
        public void OnSelect(BaseEventData data) => select?.Invoke(liveryId);
        public void OnPointerEnter(PointerEventData data) => select?.Invoke(liveryId);
        public void Refresh()
        {
            var p = FormulaProgression.State; bool equipped = p.equippedLivery == liveryId;
            ownership.text = equipped ? "EQUIPPED" : p.ownedLiveries.Contains(liveryId) ? "OWNED" : "LOCKED";
            equippedMarker.SetActive(equipped);
        }
    }
}
