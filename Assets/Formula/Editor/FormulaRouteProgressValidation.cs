using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FormulaRouteProgressValidation
{
    public static readonly List<string> Passed = new List<string>(), Failed = new List<string>();
    [MenuItem("Tools/Formula Career/Validate Player Route Flow")]
    public static void Run()
    {
        Passed.Clear(); Failed.Clear();
        var loop = Enumerable.Range(0, 100).Select(i => new Vector3(Mathf.Cos(i * Mathf.PI * 2 / 100) * 80, 0, Mathf.Sin(i * Mathf.PI * 2 / 100) * 80)).ToArray();
        Check("Leaving the grid cannot award a lap", () => { var s = new Probe(loop, 8); s.Drive(30); Require(s.laps == 0 && s.tracker.NextWaypoint > 0, "Grid departure awarded a lap or did not advance"); });
        Check("A clean closed circuit counts exactly one valid lap", () => { var s = new Probe(loop, 8); s.Drive(s.length + 4); Require(s.laps == 1 && s.validLaps == 1, "Clean lap was not counted"); });
        Check("Driving seven metres off the centreline does not require touching waypoint points", () => { var s = new Probe(loop, 8, 7); s.Drive(s.length + 4, 7); Require(s.laps == 1 && s.validLaps == 1, "Legal lane-wide lap was rejected"); });
        Check("Fast samples can pass several dense navigation points", () => { var s = new Probe(loop, 8); s.Drive(s.length + 4, 0, 15, 90); Require(s.laps == 1 && s.validLaps == 1 && s.maxPassed > 1, "Multi-point frame did not advance correctly"); });
        Check("A wide excursion can rejoin and close an invalid attempt", () =>
        {
            var s = new Probe(loop, 8); s.Drive(100); s.Drive(160, 30); s.Drive(s.length + 4);
            Require(s.laps == 1 && s.validLaps == 0 && s.tracker.NextWaypoint < 5, "Rejoin stranded the old checkpoint or rewarded a shortcut");
            s.Drive(s.length * 2 + 4); Require(s.validLaps == 1, "The next clean lap did not recover");
        });
        Check("A skipped route section resynchronises without granting a valid lap", () =>
        {
            var s = new Probe(loop, 8); s.Drive(100); s.Move(s.length * .65f, 0, .02f); Require(s.invalid, "Large discontinuity was not invalidated");
            Require(s.tracker.NextWaypoint > 40, "Progress is still waiting at an obsolete checkpoint"); s.Drive(s.length + 4); Require(s.laps == 1 && s.validLaps == 0, "Shortcut earned a valid lap");
        });
        Check("Back-and-forth movement across the start line cannot farm laps", () =>
        {
            var s = new Probe(loop, 8); for (int i = 0; i < 10; i++) { s.Move(4, 0, 1); s.Move(-4, 0, 1); }
            Require(s.laps == 0, "Start-line oscillation awarded laps");
        });
        Check("A full backward circuit cannot advance a lap", () => { var s = new Probe(loop, 8); for (float p = -11; p > -s.length - 20; p -= 2) s.Move(p, 0, 2f / 20); Require(s.laps == 0 && s.tracker.WrongWay, "Backward circuit counted or omitted the warning"); });
        Check("Backtracking to the start cannot replace the missing last quarter of a valid lap", () =>
        {
            var s = new Probe(loop, 8); s.Drive(s.length * .8f);
            while (s.phase > -4) { float next = Mathf.Max(-4, s.phase - 2); s.Move(next, 0, .1f); }
            s.Drive(4); Require(s.validLaps == 0, "Backtracking awarded a valid lap without completing the course");
        });
        Check("Wrong-way warning has a grace period and clears after correction", () =>
        {
            var s = new Probe(loop, 8); s.Drive(100);
            float p = s.phase; for (int i = 0; i < 4; i++) s.Move(p -= 2, 0, .1f); Require(!s.tracker.WrongWay, "A brief correction triggered WRONG WAY");
            for (int i = 0; i < 7; i++) s.Move(p -= 2, 0, .1f); Require(s.tracker.WrongWay, "Sustained opposite movement was not detected");
            for (int i = 0; i < 4; i++) s.Move(p += 2, 0, .1f); Require(!s.tracker.WrongWay, "Warning did not clear after forward movement");
        });
        Check("Stopping clears the warning and standing still does not trigger it", () =>
        {
            var s = new Probe(loop, 8); float p = -9; for (int i = 0; i < 12; i++) s.Move(p -= 2, 0, .1f);
            Require(s.tracker.WrongWay, "Reverse movement was not detected");
            for (int i = 0; i < 5; i++) s.Move(p, 0, .1f); Require(!s.tracker.WrongWay, "Stopped car retained WRONG WAY");
        });
        Check("Pause does not accumulate warning time or route progress", () =>
        {
            var s = new Probe(loop, 8); for (int i = 0; i < 100; i++) s.tracker.Step(s.position, Vector3.back * 20, 0);
            Require(!s.tracker.WrongWay && s.tracker.NextWaypoint == 0, "Paused sample advanced state");
        });
        Check("A different bridge deck cannot advance checkpoints", () =>
        {
            var s = new Probe(loop, 8); s.Drive(100); int before = s.tracker.NextWaypoint;
            var step = s.tracker.Step(s.position + Vector3.up * 15, Vector3.forward * 20, .02f);
            Require(step.invalidateLap && step.nextWaypoint == before && !step.completedLap, "Wrong bridge deck advanced the lap");
        });
        foreach (var track in FormulaAssets.Current.tracks)
        {
            Check("Saved circuit geometry: " + track.id, () =>
            {
                var scene = EditorSceneManager.OpenPreviewScene(track.scenePath);
                try
                {
                    var circuit = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FormulaCircuit>(true)).Single();
                    var points = circuit.route.waypoints.Select(w => w.transform.position).ToArray();
                    ValidateRoute(points, circuit.halfWidth);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            });
        }
        Directory.CreateDirectory("Docs/AI/FormulaRouteFlow");
        File.WriteAllText("Docs/AI/FormulaRouteFlow/LogicValidation.json", JsonConvert.SerializeObject(new { status = Failed.Count == 0 ? "passed" : "failed", unity = Application.unityVersion, method = "Deterministic route positions; all 18 saved circuits opened in isolated preview scenes, without saving. Separate real-physics acceptance is required.", passed = Passed, failed = Failed }, Formatting.Indented));
        Debug.Log("FORMULA_ROUTE_LOGIC: " + Passed.Count + " passed, " + Failed.Count + " failed");
        if (Failed.Count > 0) throw new InvalidOperationException(string.Join("\n", Failed));
    }
    public static void ValidateRoute(Vector3[] points, float halfWidth)
    {
        var centre = new Probe(points, halfWidth); centre.Drive(centre.length + 4);
        Require(centre.laps == 1 && centre.validLaps == 1, "Centreline failed: " + centre.laps + " laps / " + centre.validLaps + " valid");
        var lane = new Probe(points, halfWidth, Mathf.Min(3, halfWidth * .5f)); lane.Drive(lane.length + 4, Mathf.Min(3, halfWidth * .5f));
        Require(lane.laps == 1 && lane.validLaps == 1, "Offset lane failed: " + lane.laps + " laps / " + lane.validLaps + " valid");
    }
    private static void Check(string name, Action check) { try { check(); Passed.Add(name); } catch (Exception e) { Failed.Add(name + ": " + e.Message); } }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Probe
    {
        public readonly FormulaRouteProgress tracker;
        private readonly Vector3[] points;
        private readonly float[] distances;
        public readonly float length;
        public float phase = -9;
        public Vector3 position;
        public bool invalid;
        public int laps, validLaps, maxPassed;
        public Probe(Vector3[] points, float halfWidth, float offset = 0)
        {
            this.points = points; distances = new float[points.Length];
            for (int i = 1; i < points.Length; i++) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            length = distances[points.Length - 1] + Vector3.Distance(points[points.Length - 1], points[0]);
            position = At(phase, offset); tracker = new FormulaRouteProgress(points, halfWidth, position);
        }
        public void Drive(float end, float offset = 0, float step = 2, float speed = 35)
        {
            while (phase < end - .001f) { float next = Mathf.Min(end, phase + step); Move(next, offset, (next - phase) / speed); }
        }
        public void Move(float next, float offset, float delta)
        {
            Vector3 point = At(next, offset); var sample = tracker.Step(point, (point - position) / Mathf.Max(.001f, delta), delta);
            invalid |= sample.invalidateLap; maxPassed = Mathf.Max(maxPassed, sample.passedCount);
            if (sample.completedLap) { laps++; if (!invalid) validLaps++; invalid = false; }
            phase = next; position = point;
        }
        private Vector3 At(float phase, float offset)
        {
            float raw = Mathf.Repeat(distances[points.Length - 1] + phase, length);
            for (int i = 0; i < points.Length; i++)
            {
                var next = points[(i + 1) % points.Length]; Vector3 segment = next - points[i]; float span = segment.magnitude;
                if (raw <= span || i == points.Length - 1)
                    return points[i] + segment * Mathf.Clamp01(raw / Mathf.Max(.001f, span)) + Vector3.Cross(Vector3.up, segment).normalized * offset;
                raw -= span;
            }
            return points[0];
        }
    }
}
