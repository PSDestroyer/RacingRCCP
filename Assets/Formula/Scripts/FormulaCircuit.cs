using UnityEngine;
using System.Collections.Generic;

namespace FormulaGame
{
    // Authored circuit wiring. The existing GamePlayManager owns race and lap state.
    [DisallowMultipleComponent]
    public sealed class FormulaCircuit : MonoBehaviour
    {
        public string trackId, sourcePack;
        public float lengthMeters, halfWidth;
        [Range(.4f, 1f)] public float aiCornerSpeedScale = 1;
        public RCCP_AIWaypointsContainer route;
        public Transform startFinish;
        public Transform[] startingGrid;
        public Collider[] roadColliders;
        [HideInInspector] public float[] aiSpeedPlan;
        public static bool SampleRoad(IReadOnlyList<Collider> roads, Vector3 point, out Vector3 ground)
        {
            ground = point; float closest = float.PositiveInfinity;
            for (int i = 0; i < roads.Count; i++)
            {
                // Begin close to the intended deck as well as above the whole
                // structure; this also works with imported closed mesh shells.
                if (roads[i].Raycast(new Ray(point + Vector3.up * 2, Vector3.down), out var nearby, 40))
                {
                    float difference = Mathf.Abs(nearby.point.y - point.y);
                    if (difference < closest) { closest = difference; ground = nearby.point; }
                }
                var ray = new Ray(point + Vector3.up * 15, Vector3.down); float remaining = 40;
                // A single imported MeshCollider can contain both bridge decks.
                // Continue below each surface instead of accepting its top hit.
                for (int surface = 0; surface < 8 && remaining > 0; surface++)
                {
                    if (!roads[i].Raycast(ray, out var hit, remaining)) break;
                    float difference = Mathf.Abs(hit.point.y - point.y);
                    if (difference < closest) { closest = difference; ground = hit.point; }
                    remaining -= hit.distance + .02f; ray.origin = hit.point + Vector3.down * .02f;
                }
            }
            return !float.IsPositiveInfinity(closest);
        }
        public void BuildSpeedPlan()
        {
            int count = route.waypoints.Count; aiSpeedPlan = new float[count];
            for (int i = 0; i < count; i++)
            {
                float radius = 10000;
                for (int span = 1; span <= 2; span++)
                {
                    Vector3 a = route.waypoints[(i + count - span) % count].transform.position;
                    Vector3 b = route.waypoints[i].transform.position;
                    Vector3 c = route.waypoints[(i + span) % count].transform.position;
                    a.y = b.y = c.y = 0;
                    float cross = Vector3.Cross(b - a, c - a).magnitude;
                    if (cross > .01f) radius = Mathf.Min(radius, Vector3.Distance(a, b) * Vector3.Distance(b, c) * Vector3.Distance(a, c) / (2 * cross));
                }
                aiSpeedPlan[i] = Mathf.Clamp(Mathf.Sqrt(.95f * 9.81f * radius) * 3.6f * aiCornerSpeedScale, 20, 170);
            }
            // Closed-loop backward propagation: each preceding target leaves
            // enough distance to reach the next one at 4.5 m/s² deceleration.
            for (int pass = 0; pass < 3; pass++)
                for (int i = count - 1; i >= 0; i--)
                {
                    int next = (i + 1) % count;
                    float distance = Vector3.Distance(route.waypoints[i].transform.position, route.waypoints[next].transform.position);
                    float nextSpeed = aiSpeedPlan[next] / 3.6f;
                    aiSpeedPlan[i] = Mathf.Min(aiSpeedPlan[i], Mathf.Sqrt(nextSpeed * nextSpeed + 9 * distance) * 3.6f);
                }
        }
        public void ConfigureDriver(RCCP_AI driver)
        {
            driver.waypointReachThreshold = Mathf.Clamp(halfWidth * 1.5f, 5, 12);
            driver.raceLookAhead = Mathf.Clamp(halfWidth * 2.5f, 10, 20);
            driver.agressiveness = 1;
            if (aiCornerSpeedScale >= .99f) return;
            // Earlier, firmer braking for compact converted/RTG layouts. Integral
            // wind-up on a straight otherwise defeats the next corner's speed cap.
            driver.ki = 0; driver.kp = .8f;
            driver.cornerDetectionDistance = 70;
            driver.mediumCornerAngleThreshold = 12; driver.sharpCornerAngleThreshold = 28;
            driver.mediumCornerTargetSpeed = 85 * aiCornerSpeedScale;
            driver.sharpCornerTargetSpeed = 55 * aiCornerSpeedScale;
            driver.lookAheadPerKph = .25f;
            driver.mediumCornerLookAhead = 10; driver.sharpCornerLookAhead = 6;
            var pace = driver.GetComponent<FormulaAIPace>();
            if (pace == null) pace = driver.gameObject.AddComponent<FormulaAIPace>();
            pace.Initialize(this, driver);
            driver.useBrakeZoneSpeedProfile = trackId == "rtg_09" || trackId == "rtg_10";
        }
    }
}
