using System;
using System.Collections.Generic;
using UnityEngine;

namespace FormulaGame
{
    // Geometric progress for the player. GamePlayManager remains the owner of
    // laps and results; the dense AI navigation points are no longer point gates.
    public sealed class FormulaRouteProgress
    {
        public readonly struct Sample
        {
            public readonly int nextWaypoint, passedFrom, passedCount, newLapPassedCount;
            public readonly bool completedLap, invalidateLap;
            public Sample(int next, int from, int count, int newCount, bool lap, bool invalid)
            { nextWaypoint = next; passedFrom = from; passedCount = count; newLapPassedCount = newCount; completedLap = lap; invalidateLap = invalid; }
        }
        private readonly Vector3[] points;
        private readonly float[] distances, checkpointPhases;
        private readonly float length, finishDistance, progressWidth;
        private Vector3 lastPosition;
        private float lastPhase, lapDistance, highWater, wrongSeconds, clearSeconds;
        private int nextWaypoint;
        private bool started, departed, lostProjection;
        public bool WrongWay { get; private set; }
        public int NextWaypoint => nextWaypoint;
        public float Length => length;
#if UNITY_EDITOR
        // Read-only evidence for Editor acceptance; excluded from player builds.
        public string EditorLastInvalidation { get; private set; }
#endif

        public FormulaRouteProgress(IReadOnlyList<Vector3> route, float trackHalfWidth, Vector3 initialPosition)
        {
            if (route == null || route.Count < 3) throw new ArgumentException("A closed route needs at least three points.", nameof(route));
            points = new Vector3[route.Count]; distances = new float[route.Count]; checkpointPhases = new float[route.Count];
            for (int i = 0; i < route.Count; i++)
            {
                points[i] = route[i];
                if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            finishDistance = distances[points.Length - 1]; length = finishDistance + Vector3.Distance(points[points.Length - 1], points[0]);
            if (length < 10) throw new ArgumentException("The circuit route is too short.", nameof(route));
            for (int i = 0; i < points.Length - 1; i++) checkpointPhases[i] = length - finishDistance + distances[i];
            checkpointPhases[points.Length - 1] = length;
            progressWidth = Mathf.Max(12, trackHalfWidth * 1.8f + 2);
            lastPosition = initialPosition;
            if (Project(initialPosition, float.PositiveInfinity, out float phase, out _, out _))
            { lastPhase = phase; lapDistance = phase > length * .5f ? phase - length : phase; started = phase < length * .25f; }
            else lostProjection = true;
        }

        public Sample Step(Vector3 position, Vector3 velocity, float deltaTime)
        {
            if (deltaTime <= 0) return new Sample(nextWaypoint, nextWaypoint, 0, 0, false, false);
            Vector3 movement = position - lastPosition;
            float physicalStep = movement.magnitude;
            float plausibleStep = Mathf.Max(20, Mathf.Min(150, velocity.magnitude) * deltaTime * 2 + 5);
            float routeStepLimit = Mathf.Max(25, physicalStep * 2.5f + 5);
            if (!Project(position, routeStepLimit, out float phase, out float distance, out Vector3 tangent))
            {
#if UNITY_EDITOR
                EditorLastInvalidation = "No matching route elevation";
#endif
                lostProjection = true; lastPosition = position; UpdateWrongWay(Vector3.zero, Vector3.zero, deltaTime);
                return new Sample(nextWaypoint, nextWaypoint, 0, 0, false, true);
            }
            UpdateWrongWay(velocity, tangent, deltaTime);
            Vector3 planarVelocity = velocity; planarVelocity.y = 0;
            bool forward = Vector3.Dot(planarVelocity, tangent) > .1f;
            float delta = SignedDistance(phase - lastPhase);
            bool recovery = lostProjection || physicalStep > plausibleStep || Mathf.Abs(delta) > routeStepLimit;
            bool wrapped = lastPhase > length * .75f && phase < length * .25f && delta > 0 && forward;
            lapDistance += delta;
            // Reacquire a missed section within the current attempt. Ordinary
            // reversing keeps its signed distance, so recrossing the start line
            // cannot stand in for completing the remaining course.
            if (recovery && !wrapped) lapDistance = phase;
            bool invalid = recovery || distance > progressWidth;
#if UNITY_EDITOR
            if (invalid) EditorLastInvalidation = lostProjection ? "Rejoined route elevation" : physicalStep > plausibleStep ? "Discontinuous world position" : Mathf.Abs(delta) > routeStepLimit ? "Discontinuous route projection" : "Outside progress corridor";
#endif
            int from = nextWaypoint, count = 0, newCount = 0;
            bool lap = false;

            if (!started)
            {
                // Crossing the line while leaving the grid starts the first lap.
                // It must never award a lap, even when the grid is near the end.
                if (forward && (wrapped || phase < length * .5f)) started = true;
            }
            if (started && forward && delta >= 0)
            {
                if (wrapped && departed && highWater >= length * .75f && lapDistance >= length - .05f)
                {
                    // Rejoins can close an invalid attempt, but never fabricate
                    // checkpoint split times or turn a shortcut into a valid lap.
                    lap = true;
                    if (!recovery) count = points.Length - nextWaypoint;
                    lapDistance -= length;
                    nextWaypoint = 0; highWater = 0; departed = false;
                    while (nextWaypoint < points.Length - 1 && checkpointPhases[nextWaypoint] <= lapDistance)
                    { nextWaypoint++; newCount++; }
                    highWater = Mathf.Max(0, lapDistance);
                }
                else if (lapDistance < length * .5f || departed)
                {
                    highWater = Mathf.Max(highWater, Mathf.Clamp(lapDistance, 0, length));
                    departed |= highWater >= Mathf.Min(40, length * .12f);
                    while (nextWaypoint < points.Length - 1 && checkpointPhases[nextWaypoint] <= highWater)
                    { nextWaypoint++; if (!recovery) count++; }
                }
            }
            lastPhase = phase; lastPosition = position; lostProjection = false;
            return new Sample(nextWaypoint, from, count, newCount, lap, invalid);
        }

        private float SignedDistance(float distance) => Mathf.Repeat(distance + length * .5f, length) - length * .5f;
        private bool Project(Vector3 position, float stepLimit, out float phase, out float distance, out Vector3 tangent)
        {
            phase = distance = 0; tangent = Vector3.zero; float best = float.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                Vector3 segment = points[next] - points[i], flat = segment; flat.y = 0;
                if (flat.sqrMagnitude < .001f) continue;
                Vector3 offset = position - points[i]; offset.y = 0;
                float t = Mathf.Clamp01(Vector3.Dot(offset, flat) / flat.sqrMagnitude);
                Vector3 projected = points[i] + segment * t;
                float height = Mathf.Abs(position.y - projected.y);
                if (height > 6) continue; // Separate bridge decks and tunnel levels.
                Vector3 lateral = position - projected; lateral.y = 0;
                float candidatePhase = Mathf.Repeat(distances[i] + segment.magnitude * t - finishDistance, length);
                float score = lateral.sqrMagnitude + height * height * 4;
                // Nearby parallel legs must not steal progress at a hairpin.
                if (Mathf.Abs(SignedDistance(candidatePhase - lastPhase)) > stepLimit) score += progressWidth * progressWidth * 4;
                if (score >= best) continue;
                best = score; phase = candidatePhase; distance = lateral.magnitude; tangent = flat.normalized;
            }
            return !float.IsPositiveInfinity(best);
        }
        private void UpdateWrongWay(Vector3 velocity, Vector3 tangent, float deltaTime)
        {
            velocity.y = 0;
            bool opposite = velocity.magnitude >= 2.8f && tangent.sqrMagnitude > .5f && Vector3.Dot(velocity.normalized, tangent) < -.35f;
            if (opposite)
            { clearSeconds = 0; wrongSeconds += deltaTime; if (wrongSeconds >= .9f) WrongWay = true; }
            else
            { wrongSeconds = 0; clearSeconds += deltaTime; if (clearSeconds >= .25f) WrongWay = false; }
        }
    }
}
