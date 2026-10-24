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
            if (caption != null) caption.text = menu ? "RETURNING TO MAIN MENU" : "PREPARING YOUR SESSION";
        }
        public void Present(float value)
        {
            var e = FormulaSession.CurrentEvent;
            circuit.text = menu ? "MAIN MENU" : e == null ? "FORMULA G27" : FormulaAssets.Current.FindTrack(e.trackId).displayName.ToUpperInvariant();
            progress.text = "LOADING  /  " + Mathf.RoundToInt(value * 100) + "%"; fill.fillAmount = value;
        }
    }
}
