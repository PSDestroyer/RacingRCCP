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
        [SerializeField] private bool homeNavigation;
        private UnityEngine.UI.Slider slider;
        private FormulaPanelGraphic sliderSurface;
        private UnityEngine.UI.Graphic sliderFill, sliderHandle;
        private Color sliderFillColor, sliderHandleColor;
        private Vector2 sliderHandleSize;
        public static void StyleSlider(UnityEngine.UI.Slider control)
        {
            if (control == null) return;
            var focus = control.GetComponent<FormulaMenuFocus>() ?? control.gameObject.AddComponent<FormulaMenuFocus>();
            if (focus.slider == control) return;
            focus.slider = control;
            var row = control.transform.parent;
            var surface = row.Find("Upgrade Surface") ?? row.Find("Surface");
            focus.sliderSurface = surface != null ? surface.GetComponent<FormulaPanelGraphic>() : row.GetComponent<FormulaPanelGraphic>();
            if (focus.sliderSurface != null && focus.sliderSurface.transform.parent == row)
            {
                // The shorter vibration row inherits a volume panel. Keep its
                // selection inside the actual row, including at larger UI scales.
                var rect = focus.sliderSurface.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            focus.sliderFill = control.fillRect != null ? control.fillRect.GetComponent<UnityEngine.UI.Graphic>() : null;
            focus.sliderHandle = control.handleRect != null ? control.handleRect.GetComponent<UnityEngine.UI.Graphic>() : null;
            // Slider owns these anchors. Authored sizeDelta on a stretched fill
            // doubles its width; a horizontal handle's height must stretch only once.
            if (control.fillRect != null) { control.fillRect.sizeDelta = Vector2.zero; control.fillRect.anchoredPosition = Vector2.zero; }
            if (control.handleRect != null && (control.direction == UnityEngine.UI.Slider.Direction.LeftToRight || control.direction == UnityEngine.UI.Slider.Direction.RightToLeft))
                control.handleRect.sizeDelta = new Vector2(control.handleRect.sizeDelta.x, 0);
            if (focus.sliderFill != null) focus.sliderFillColor = focus.sliderFill.color;
            if (focus.sliderHandle != null) focus.sliderHandleColor = focus.sliderHandle.color;
            if (control.handleRect != null) focus.sliderHandleSize = control.handleRect.sizeDelta;
            // The whole row owns focus feedback; avoid Selectable's subtle handle
            // tint competing with the explicit selected/unselected colours.
            control.transition = UnityEngine.UI.Selectable.Transition.None;
            if (control.targetGraphic != null) control.targetGraphic.canvasRenderer.SetColor(Color.white);
            focus.selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == control.gameObject;
            focus.SetColor(0);
        }
        public void UseHomeNavigation() { homeNavigation = true; }
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
            if (slider != null)
            {
                bool active = selected && slider.IsInteractable();
                if (sliderSurface != null) sliderSurface.Configure(active);
                if (sliderFill != null) FormulaUIMotion.Tint(sliderFill, active ? FormulaUI.Accent : sliderFillColor, .10f);
                if (sliderHandle != null) sliderHandle.color = active ? Color.white : sliderHandleColor;
                if (slider.handleRect != null) slider.handleRect.sizeDelta = active ? new Vector2(Mathf.Max(20, sliderHandleSize.x), sliderHandleSize.y) : sliderHandleSize;
                return;
            }
            if (figmaSelection != null)
            {
                float tint = homeNavigation && pressed ? .82f : 1;
                figmaSelection.color = new Color(tint, tint, tint, alpha);
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
        public void OnSelect(BaseEventData data) { selected = true; strength = 1; SetColor(1); if (slider == null) FormulaUIMotion.Focus(gameObject, 1); FormulaAudio.Play(FormulaAudio.Cue.Focus); }
        public void OnDeselect(BaseEventData data) { selected = false; if (slider != null) SetColor(0); else FormulaUIMotion.Focus(gameObject, hovered ? .35f : 0); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; SetColor(strength); if (slider == null) FormulaUIMotion.Focus(gameObject, selected ? 1 : .35f); }
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; SetColor(strength); if (slider == null) FormulaUIMotion.Focus(gameObject, selected ? 1 : 0); }
        private void OnDisable() { selected = hovered = pressed = false; strength = 0; SetColor(0); }
        public void OnPointerDown(PointerEventData data) { pressed = data.button == PointerEventData.InputButton.Left; SetColor(strength); if (pressed && slider == null) FormulaUIMotion.Pulse(gameObject, -.035f, .14f); }
        public void OnPointerUp(PointerEventData data) { pressed = false; SetColor(strength); }
        public void OnSubmit(BaseEventData data) { if (slider == null) FormulaUIMotion.Pulse(gameObject, -.035f, .14f); FormulaAudio.Play(FormulaAudio.Cue.Confirm); }
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) FormulaAudio.Play(FormulaAudio.Cue.Confirm); }
        private void Update()
        {
            if (slider != null) return; // Slider feedback is immediate and event-driven, including while paused.
            if (frame == null) return;
            float target = selected ? 1 : homePlate == null && hovered ? .18f : 0;
            if (Mathf.Approximately(strength, target)) return;
            strength = FormulaProgression.State.reducedMotion ? target : Mathf.MoveTowards(strength, target, Time.unscaledDeltaTime * 10);
            SetColor(strength);
        }
    }
}
