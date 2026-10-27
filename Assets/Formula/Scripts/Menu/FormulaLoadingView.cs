using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaLoadingView : MonoBehaviour
    {
        [SerializeField] private TMP_Text circuit, progress, caption;
        [SerializeField] private UnityEngine.UI.Image fill;
        private bool menu;
        public void SetDestination(string sceneName)
        {
            menu = System.IO.Path.GetFileNameWithoutExtension(sceneName) == "Menu";
            RefreshCaption();
        }
        private void OnEnable() => FormulaLocalization.Changed += RefreshCaption;
        private void OnDisable() => FormulaLocalization.Changed -= RefreshCaption;
        private void RefreshCaption()
        {
            if (caption != null) caption.text = menu ? FormulaLocalization.T("RETURNING TO MAIN MENU") : FormulaLocalization.T("PREPARING YOUR SESSION");
        }
        public void Present(float value)
        {
            var e = FormulaSession.CurrentEvent;
            circuit.text = menu ? FormulaLocalization.T("MAIN MENU") : e == null ? FormulaLocalization.T("FORMULA G27") : FormulaLocalization.T(FormulaAssets.Current.FindTrack(e.trackId).displayName).ToUpperInvariant();
            progress.text = FormulaLocalization.T("LOADING  /  ") + Mathf.RoundToInt(value * 100) + "%"; fill.fillAmount = value;
        }
    }
}
