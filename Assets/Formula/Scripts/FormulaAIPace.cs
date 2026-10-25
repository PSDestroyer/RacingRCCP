using UnityEngine;

namespace FormulaGame
{
    // Feed the authored braking profile through RCCP's existing speed-limit API.
    // Vehicle movement, steering, traction and braking stay under RCCP physics.
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class FormulaAIPace : MonoBehaviour
    {
        private FormulaCircuit circuit;
        private RCCP_AI driver;
        private RCCP_AIBrakeZone limit;
        public void Initialize(FormulaCircuit track, RCCP_AI ai)
        {
            circuit = track; driver = ai;
            if (limit == null)
            {
                var data = new GameObject("AI Pace Limit"); data.transform.SetParent(transform, false);
                limit = data.AddComponent<RCCP_AIBrakeZone>();
                limit.enabled = false; // Data only: there is no physical trigger.
            }
        }
        private void FixedUpdate()
        {
            if (driver == null || limit == null || circuit.aiSpeedPlan == null || circuit.aiSpeedPlan.Length == 0) return;
            limit.targetSpeed = circuit.aiSpeedPlan[Mathf.Clamp(driver.currentWaypointIndex, 0, circuit.aiSpeedPlan.Length - 1)];
            driver.EnteredBrakeZone(limit);
        }
        private void OnDisable() { if (driver != null) driver.ExitedBrakeZone(); }
    }
}
