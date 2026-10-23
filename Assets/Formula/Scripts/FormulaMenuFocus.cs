using UnityEngine;
using UnityEngine.EventSystems;

namespace FormulaGame
{
    // Focus is separate from ownership/equipped state; pointer hover never buys anything.
    public sealed class FormulaMenuFocus : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private UnityEngine.UI.Graphic[] frame;
        [SerializeField] private UnityEngine.UI.Graphic homePlate, homeLabel, homeIcon, homeRail, homeArrow;
        [SerializeField] private Vector2 homeLabelPosition;
        [SerializeField] private UnityEngine.UI.Graphic[] supportingGraphics;
        [SerializeField] private Color[] supportingColors;
        [SerializeField] private UnityEngine.UI.Graphic figmaSelection, figmaAction;
        [SerializeField] private UnityEngine.UI.Graphic figmaNormal;
        public void StyleFigma(UnityEngine.UI.Graphic selection, UnityEngine.UI.Graphic action, UnityEngine.UI.Graphic normal = null)
        {
            homePlate = homeLabel = homeIcon = homeRail = homeArrow = null;
            supportingGraphics = null; supportingColors = null;
            frame = System.Array.Empty<UnityEngine.UI.Graphic>();
            figmaSelection = selection; figmaAction = action; figmaNormal = normal; SetColor(0);
        }
        public void StyleSupporting(params UnityEngine.UI.Graphic[] graphics)
        {
            supportingGraphics = graphics; supportingColors = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) supportingColors[i] = graphics[i].color;
            RefreshVisuals();
        }
        public void RefreshVisuals() => SetColor(strength);
        private bool selected, hovered, pressed;
        public void StyleHome(UnityEngine.UI.Graphic plate, UnityEngine.UI.Graphic label, UnityEngine.UI.Graphic icon, UnityEngine.UI.Graphic rail, UnityEngine.UI.Graphic arrow)
        {
            figmaSelection = figmaAction = figmaNormal = null;
            homePlate = plate; homeLabel = label; homeIcon = icon; homeRail = rail; homeArrow = arrow;
            homeLabelPosition = label.rectTransform.anchoredPosition; SetColor(0);
        }
        private float strength;
        public void Initialize(params UnityEngine.UI.Graphic[] target) { frame = target; SetColor(0); }
        private void SetColor(float alpha)
        {
            if (figmaSelection != null)
            {
                figmaSelection.color = new Color(1, 1, 1, alpha);
                if (figmaNormal != null) figmaNormal.color = new Color(1, 1, 1, 1 - alpha);
                if (figmaAction != null) figmaAction.color = new Color(1, 1, 1, alpha);
                return;
            }
            if (frame != null) foreach (var graphic in frame) if (graphic != null) graphic.color = new Color(1, .88f, .23f, alpha);
            if (homePlate == null) return;
            Color ink = new Color32(32, 3, 12, 248);
            homePlate.color = Color.Lerp(hovered ? new Color32(112, 9, 30, 255) : ink, pressed ? FormulaUI.Accent : Color.white, alpha);
            homeLabel.color = Color.Lerp(Color.white, ink, alpha);
            homeLabel.rectTransform.anchoredPosition = homeLabelPosition + Vector2.right * (4 * alpha);
            if (homeIcon != null) homeIcon.color = Color.Lerp(Color.white, FormulaUI.Red, alpha);
            if (supportingGraphics != null && supportingColors != null)
                for (int i = 0; i < supportingGraphics.Length && i < supportingColors.Length; i++)
                    if (supportingGraphics[i] != null) supportingGraphics[i].color = Color.Lerp(supportingColors[i], ink, alpha);
            homeRail.color = FormulaUI.Red;
            homeArrow.color = new Color(ink.r, ink.g, ink.b, alpha);
        }
        public void OnSelect(BaseEventData data) => selected = true;
        public void OnDeselect(BaseEventData data) => selected = false;
        public void OnPointerEnter(PointerEventData data) { hovered = true; SetColor(strength); }
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; SetColor(strength); }
        private void OnDisable() { selected = hovered = pressed = false; strength = 0; SetColor(0); }
        public void OnPointerDown(PointerEventData data) { pressed = data.button == PointerEventData.InputButton.Left; SetColor(strength); }
        public void OnPointerUp(PointerEventData data) { pressed = false; SetColor(strength); }
        public void OnSubmit(BaseEventData data) => SoundManager.Instance?.PlayButtonClick();
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) SoundManager.Instance?.PlayButtonClick(); }
        private void Update()
        {
            if (frame == null) return;
            float target = selected ? 1 : homePlate == null && hovered ? .5f : 0;
            if (Mathf.Approximately(strength, target)) return;
            strength = Mathf.MoveTowards(strength, target, Time.unscaledDeltaTime * 10);
            SetColor(strength);
        }
    }
}
