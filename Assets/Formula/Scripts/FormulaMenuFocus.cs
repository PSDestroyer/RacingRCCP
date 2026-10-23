using UnityEngine;
using UnityEngine.EventSystems;

namespace FormulaGame
{
    // Focus is separate from ownership/equipped state; pointer hover never buys anything.
    public sealed class FormulaMenuFocus : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] private UnityEngine.UI.Graphic[] frame;
        private bool selected, hovered;
        private float strength;
        public void Initialize(params UnityEngine.UI.Graphic[] target) { frame = target; SetColor(0); }
        private void SetColor(float alpha) { if (frame != null) foreach (var graphic in frame) if (graphic != null) graphic.color = new Color(1, .88f, .23f, alpha); }
        public void OnSelect(BaseEventData data) => selected = true;
        public void OnDeselect(BaseEventData data) => selected = false;
        public void OnPointerEnter(PointerEventData data) => hovered = true;
        public void OnPointerExit(PointerEventData data) => hovered = false;
        private void OnDisable() { selected = hovered = false; strength = 0; SetColor(0); }
        public void OnSubmit(BaseEventData data) => SoundManager.Instance?.PlayButtonClick();
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) SoundManager.Instance?.PlayButtonClick(); }
        private void Update()
        {
            if (frame == null) return;
            float target = selected ? 1 : hovered ? .5f : 0;
            if (Mathf.Approximately(strength, target)) return;
            strength = Mathf.MoveTowards(strength, target, Time.unscaledDeltaTime * 10);
            SetColor(strength);
        }
    }
}
