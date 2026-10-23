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
        public void Refresh(RCCP_CarController car, WheelCollider wheel)
        {
            torque.text = $"{car.Engine.maximumTorqueAsNM:0.#} Nm";
            brakes.text = $"{car.FrontAxle.maxBrakeTorque:0} Nm";
            spring.text = wheel != null ? $"{wheel.suspensionSpring.spring:0} N/m" : "—";
            aero.text = $"{car.AeroDynamics.downForce:0.##}";
            drag.text = $"Aero drag {car.AeroDynamics.airResistance:0.##} · Mass {car.Rigid.mass:0} kg";
            foreach (var row in development)
            {
                SetSegments(row.segments, FormulaProgression.State.Equipped(row.branchId), FormulaProgression.State.Owned(row.branchId));
                if (row.label != null) row.label.text = FormulaCatalog.Current.Upgrade(row.branchId).name.ToUpperInvariant() + "  " + FormulaProgression.State.Equipped(row.branchId) + "/3";
            }
        }
        public static void SetSegments(UnityEngine.UI.Image[] segments, int equipped, int owned)
        {
            for (int i = 0; i < segments.Length; i++) segments[i].color = i < equipped ? FormulaUI.Accent : i < owned ? Color.white : FormulaUI.Red;
        }
    }
}
