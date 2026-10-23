using UnityEngine;
using UnityEngine.EventSystems;
namespace FormulaGame
{
    // Focus previews a component or level; only Button submission can request a transaction.
    public sealed class FormulaUpgradeOption : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [SerializeField] private FormulaUpgradeView owner;
        [SerializeField] private bool category;
        [SerializeField] private int index;
        public void OnSelect(BaseEventData data) { if (category) owner.SelectCategory(index); else owner.PreviewLevel(index); }
        public void OnPointerEnter(PointerEventData data) { if (!category) owner.PreviewLevel(index); }
    }
}
