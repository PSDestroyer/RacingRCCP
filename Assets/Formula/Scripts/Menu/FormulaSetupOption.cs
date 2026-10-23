using UnityEngine;
using UnityEngine.EventSystems;
namespace FormulaGame
{
    public sealed class FormulaSetupOption : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [SerializeField] private FormulaGarageView owner;
        [SerializeField] private string setupId;
        public void OnSelect(BaseEventData data) => owner.PreviewSetup(setupId);
        public void OnPointerEnter(PointerEventData data) => owner.PreviewSetup(setupId);
    }
}
