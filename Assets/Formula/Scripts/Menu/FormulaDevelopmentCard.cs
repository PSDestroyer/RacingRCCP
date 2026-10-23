using System;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaDevelopmentCard : MonoBehaviour
    {
        [SerializeField] private string branchId;
        [SerializeField] private UnityEngine.UI.Button equip, buy;
        [SerializeField] private TMP_Text equippedLevel, purchase;
        [SerializeField] private UnityEngine.UI.Image[] segments;
        [SerializeField] private TMP_Text effect;
        public void Bind(Action<string> selectLevel, Action<string> purchaseLevel)
        {
            equip.onClick.AddListener(() => selectLevel(branchId)); buy.onClick.AddListener(() => purchaseLevel(branchId));
        }
        public void Refresh()
        {
            var p = FormulaProgression.State; var branch = FormulaCatalog.Current.Upgrade(branchId);
            var level = branch.levels.Find(l => l.level == p.Owned(branchId) + 1);
            equippedLevel.text = "LEVEL " + p.Equipped(branchId) + " >"; equip.interactable = p.Owned(branchId) > 0;
            purchase.text = level == null ? "FULLY UPGRADED" : "UPGRADE / " + level.incrementalPriceCredits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR";
            buy.interactable = level != null; FormulaStatisticsView.SetSegments(segments, p.Equipped(branchId), p.Owned(branchId));
        }
        public void RefreshComparison(RCCP_CarController car, WheelCollider wheel)
        {
            if (effect == null) return;
            int equipped = FormulaProgression.State.Equipped(branchId), next = Mathf.Min(3, FormulaProgression.State.Owned(branchId) + 1);
            float current, step; string unit;
            switch (branchId)
            {
                case "engine": current = car.Engine.maximumTorqueAsNM; step = .03f; unit = "Nm"; break;
                case "brakes": current = car.FrontAxle.maxBrakeTorque; step = .04f; unit = "Nm"; break;
                case "suspension": current = wheel != null ? wheel.suspensionSpring.spring : 0; step = .015f; unit = "N/m"; break;
                default: current = car.AeroDynamics.downForce; step = .04f; unit = "coef."; break;
            }
            float target = current / (1 + step * equipped) * (1 + step * next);
            effect.text = FormulaProgression.State.Owned(branchId) == 3 ? "Fully upgraded · " + current.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " " + unit : current.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " → <color=#FFE13B>" + target.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " " + unit + "</color>";
        }
    }
}
