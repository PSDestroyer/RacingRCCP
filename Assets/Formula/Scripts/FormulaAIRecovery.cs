using UnityEngine;
using UnityEngine.AI;

namespace FormulaGame
{
    // Give RCCP's reverse recovery time first. A stranded opponent then rejoins
    // behind its outstanding checkpoint, without changing its lap or checkpoint.
    [DisallowMultipleComponent]
    public sealed class FormulaAIRecovery : MonoBehaviour
    {
        private GamePlayManager game;
        private FormulaCircuit circuit;
        private RCCP_AI driver;
        private int checkpoint;
        private float progressAt, nextCheck;
        public int RecoveryCount { get; private set; }
        public void Initialize(GamePlayManager manager, FormulaCircuit track, RCCP_AI ai)
        {
            game = manager; circuit = track; driver = ai;
            checkpoint = ai.currentWaypointIndex; progressAt = Time.time;
        }
        private void Update()
        {
            if (driver == null || Time.time < nextCheck) return;
            nextCheck = Time.time + .5f;
            if (!game.FormulaRaceStarted || !driver.CarController.canControl || checkpoint != driver.currentWaypointIndex)
            { checkpoint = driver.currentWaypointIndex; progressAt = Time.time; return; }
            if (Time.time - progressAt < 12) return;
            if (TryRejoin()) { RecoveryCount++; progressAt = Time.time; }
        }
        private bool TryRejoin()
        {
            var points = circuit.route.waypoints; int index = driver.currentWaypointIndex;
            float remaining = driver.waypointReachThreshold + 5;
            Vector3 position = points[index].transform.position, direction = Vector3.forward;
            for (int scanned = 0; scanned < points.Count; scanned++)
            {
                var previous = points[(index + points.Count - 1) % points.Count].transform.position;
                float distance = Vector3.Distance(previous, position);
                direction = (position - previous).normalized;
                if (remaining <= distance) { position -= direction * remaining; break; }
                remaining -= distance; position = previous; index = (index + points.Count - 1) % points.Count;
            }
            if (!FormulaCircuit.SampleRoad(circuit.roadColliders, position, out var ground) || !NavMesh.SamplePosition(ground, out var nav, 2, NavMesh.AllAreas)) return false;
            var rotation = Quaternion.LookRotation(direction);
            foreach (var other in Physics.OverlapBox(ground + Vector3.up, new Vector3(1.4f, .6f, 3.2f), rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!other.transform.IsChildOf(transform) && !(other is TerrainCollider) && other.GetComponentInParent<RCCP_CarController>() != null) return false;
            driver.StopAllCoroutines(); driver.Reload();
            var car = driver.CarController;
            car.Inputs.autoReverse = false; car.Gearbox.ShiftToGear(0);
            RCCP.Transport(car, ground + Vector3.up * .35f, rotation);
            var agent = car.GetComponentInChildren<NavMeshAgent>();
            if (agent != null) agent.Warp(nav.position);
            return true;
        }
    }
}
