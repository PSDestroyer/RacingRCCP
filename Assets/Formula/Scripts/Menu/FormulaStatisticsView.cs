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
        [SerializeField] private TMP_Text comparison;
        public void PreviewUpgrade(string branch, int level) { previewBranch = branch; previewLevel = level; if (currentCar != null) Refresh(currentCar, currentWheel); else RefreshDevelopment(); }
        public void PreviewSetup(string id) { if (!setupComparison) return; inspectedSetup = id; if (currentCar != null) Refresh(currentCar, currentWheel); }
        public void Refresh(RCCP_CarController car, WheelCollider wheel)
        {
            currentCar = car; currentWheel = wheel;
            torque.text = FormulaLocalization.F("{0:0.#} Nm", car.Engine.maximumTorqueAsNM);
            brakes.text = FormulaLocalization.F("{0:0} Nm", car.FrontAxle.maxBrakeTorque);
            spring.text = wheel != null ? FormulaLocalization.F("{0:0} N/m", wheel.suspensionSpring.spring) : "—";
            aero.text = $"{car.AeroDynamics.downForce:0.##}";
            drag.text = FormulaLocalization.F("Aero drag {0:0.##} · Mass {1:0} kg", car.AeroDynamics.airResistance, car.Rigid.mass);
            if (setupComparison && !string.IsNullOrEmpty(inspectedSetup) && inspectedSetup != FormulaProgression.State.setup)
            {
                string installed = FormulaProgression.State.setup;
                float down = car.AeroDynamics.downForce / SetupFactor(installed, false) * SetupFactor(inspectedSetup, false);
                float resistance = car.AeroDynamics.airResistance / SetupFactor(installed, true) * SetupFactor(inspectedSetup, true);
                aero.text += " → " + down.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                drag.text = FormulaLocalization.T("Aero drag ") + car.AeroDynamics.airResistance.ToString("0.##") + " → " + resistance.ToString("0.##");
            }
            if (!setupComparison && !string.IsNullOrEmpty(previewBranch))
            {
                var value = previewBranch == "engine" ? torque : previewBranch == "brakes" ? brakes : previewBranch == "suspension" ? spring : aero;
                float proposed = FormulaUpgradeView.ValueAtLevel(previewBranch, previewLevel, car, wheel);
                if (previewLevel != FormulaProgression.State.Equipped(previewBranch))
                    value.text += " → " + proposed.ToString(previewBranch == "aero" ? "0.##" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + (previewBranch == "aero" ? "" : " " + FormulaUpgradeView.Unit(previewBranch));
                if (previewBranch == "aero" && previewLevel != FormulaProgression.State.Equipped("aero"))
                {
                    float proposedDrag = car.AeroDynamics.airResistance / (1 + .01f * FormulaProgression.State.Equipped("aero")) * (1 + .01f * previewLevel);
                    drag.text = FormulaLocalization.F("Drag {0:0.##} → {1:0.##}", car.AeroDynamics.airResistance, proposedDrag);
                }
            }
            if (comparison != null) comparison.text = setupComparison ? FormulaLocalization.T("CURRENT → SETUP PREVIEW") : previewBranch != null && previewLevel != FormulaProgression.State.Equipped(previewBranch) ? FormulaLocalization.T("CURRENT → LEVEL ") + previewLevel + FormulaLocalization.T(" PREVIEW") : FormulaLocalization.T("CURRENT CONFIGURATION");
            RefreshDevelopment();
            if (setupComparison)
            {
                foreach (var row in development) if (row.branchId == "aero" && row.label != null) row.label.text = FormulaLocalization.T("DOWNFORCE");
                if (string.IsNullOrEmpty(inspectedSetup) || inspectedSetup == FormulaProgression.State.setup) drag.text = FormulaLocalization.T("Aero drag ") + car.AeroDynamics.airResistance.ToString("0.##");
            }
        }
        private static float SetupFactor(string id, bool drag) => id == "speed" ? drag ? .96f : .92f : id == "corners" ? drag ? 1.04f : 1.08f : 1f;
        private void RefreshDevelopment()
        {
            foreach (var row in development)
            {
                int equipped = FormulaProgression.State.Equipped(row.branchId), owned = FormulaProgression.State.Owned(row.branchId);
                if (!figmaLevels) SetSegments(row.segments, equipped, owned);
                else for (int i = 0; i < row.segments.Length; i++) row.segments[i].color = i / 3 < equipped ? Color.white : row.branchId == previewBranch && i / 3 < previewLevel ? new Color32(255, 30, 0, 255) : new Color32(116, 18, 5, 255);
                if (row.label != null) row.label.text = (figmaLevels && row.branchId == "aero" ? FormulaLocalization.T("AERO") : FormulaLocalization.T(FormulaCatalog.Current.Upgrade(row.branchId).name).ToUpperInvariant()) + "  " + equipped + "/3";
            }
        }
        public static void SetSegments(UnityEngine.UI.Image[] segments, int equipped, int owned)
        {
            for (int i = 0; i < segments.Length; i++) segments[i].color = i < equipped ? FormulaUI.Accent : i < owned ? Color.white : FormulaUI.Red;
        }
    }
}
