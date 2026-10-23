using System;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaCareerView : MonoBehaviour
    {
        [SerializeField] private FormulaEventCard[] events;
        [SerializeField] private GameObject[] standardChrome;
        [SerializeField] private TMP_Text driver, wallet;
        [SerializeField] private UnityEngine.UI.Button back;
        public void Bind(Action<string, string> inspect, Action onBack = null)
        {
            foreach (var card in events) card.Bind(inspect);
            if (back != null && onBack != null) back.onClick.AddListener(() => onBack());
        }
        public void SetPresentationActive(bool active)
        {
            if (standardChrome == null) return;
            foreach (var item in standardChrome) if (item != null) item.SetActive(!active);
        }
        public void Refresh()
        {
            for (int i = 0; i < events.Length; i++) events[i].Refresh(i);
            if (driver != null) driver.text = FormulaMenuFormatting.DriverName;
            if (wallet != null) wallet.text = FormulaProgression.Data.money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
