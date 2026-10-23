using System;
using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaStatisticsView : MonoBehaviour
    {
        [Serializable] public sealed class DevelopmentRow { public string branchId; public UnityEngine.UI.Image[] segments; public TMP_Text label; }
        [SerializeField] private TMP_Text torque, brakes, spring, aero, drag;
        [SerializeField] private DevelopmentRow[] development;
        [SerializeField] private bool figmaLevels, setupComparison;
        private string inspectedSetup;
        private RCCP_CarController currentCar;
        private WheelCollider currentWheel;
        private string previewBranch;
        private int previewLevel;
        public void PreviewUpgrade(string branch, int level) { previewBranch = branch; previewLevel = level; RefreshDevelopment(); }
        public void PreviewSetup(string id) { if (!setupComparison) return; inspectedSetup = id; if (currentCar != null) Refresh(currentCar, currentWheel); }
        public void Refresh(RCCP_CarController car, WheelCollider wheel)
        {
            currentCar = car; currentWheel = wheel;
            torque.text = $"{car.Engine.maximumTorqueAsNM:0.#} Nm";
            brakes.text = $"{car.FrontAxle.maxBrakeTorque:0} Nm";
            spring.text = wheel != null ? $"{wheel.suspensionSpring.spring:0} N/m" : "—";
            aero.text = $"{car.AeroDynamics.downForce:0.##}";
            drag.text = $"Aero drag {car.AeroDynamics.airResistance:0.##} · Mass {car.Rigid.mass:0} kg";
            if (setupComparison && !string.IsNullOrEmpty(inspectedSetup) && inspectedSetup != FormulaProgression.State.setup)
            {
                string installed = FormulaProgression.State.setup;
                float down = car.AeroDynamics.downForce / SetupFactor(installed, false) * SetupFactor(inspectedSetup, false);
                float resistance = car.AeroDynamics.airResistance / SetupFactor(installed, true) * SetupFactor(inspectedSetup, true);
                aero.text += " → " + down.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                drag.text = "Aero drag " + car.AeroDynamics.airResistance.ToString("0.##") + " → " + resistance.ToString("0.##") + " · Free setup preview";
            }
            RefreshDevelopment();
        }
        private static float SetupFactor(string id, bool drag) => id == "speed" ? drag ? .96f : .92f : id == "corners" ? drag ? 1.04f : 1.08f : 1f;
        private void RefreshDevelopment()
        {
            foreach (var row in development)
            {
                int equipped = FormulaProgression.State.Equipped(row.branchId), owned = FormulaProgression.State.Owned(row.branchId);
                if (!figmaLevels) SetSegments(row.segments, equipped, owned);
                else for (int i = 0; i < row.segments.Length; i++) row.segments[i].color = i / 3 < equipped ? Color.white : row.branchId == previewBranch && i / 3 < previewLevel ? new Color32(255, 30, 0, 255) : new Color32(116, 18, 5, 255);
                if (row.label != null) row.label.text = (figmaLevels && row.branchId == "aero" ? "AERO" : FormulaCatalog.Current.Upgrade(row.branchId).name.ToUpperInvariant()) + "  " + equipped + "/3";
            }
        }
        public static void SetSegments(UnityEngine.UI.Image[] segments, int equipped, int owned)
        {
            for (int i = 0; i < segments.Length; i++) segments[i].color = i < equipped ? FormulaUI.Accent : i < owned ? Color.white : FormulaUI.Red;
        }
    }
}
