using TMPro;
using UnityEngine;
namespace FormulaGame
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FormulaDevicePrompt : MonoBehaviour
    {
        private TMP_Text label;
        private int revision = -1;
        private void Awake() => label = GetComponent<TMP_Text>();
        private void OnEnable() => revision = -1;
        private void Update()
        {
            FormulaInputHints.Poll();
            if (revision == FormulaInputHints.Revision) return;
            revision = FormulaInputHints.Revision; label.text = FormulaInputHints.Back;
        }
    }
}
