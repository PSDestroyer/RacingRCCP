using System;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.AI;

namespace FormulaGame
{
    public static class FormulaSession
    {
        public static bool InRace { get; private set; }
        public static FormulaCatalog.CareerEvent CurrentEvent => FormulaCatalog.Current.Event(FormulaProgression.State.activeEventId);
        public static bool StockVehicle => InRace && (FormulaProgression.State.activeMode == "time_trial" || (FormulaProgression.State.activeMode == "career" && CurrentEvent?.vehicleRule == "stock_balanced"));
        public static int OpponentLevel => CurrentEvent?.id == "g06" ? 2 : CurrentEvent?.id == "g04" ? 1 : 0;
        public static double BestLap { get; private set; }
        public static int ValidLaps { get; private set; }
        public static double LastLap { get; private set; }
        public static bool LastLapValid { get; private set; }
        public static bool LapInvalid => lapInvalid;
        public static double LapElapsed(double elapsed) => System.Math.Max(0, elapsed - lastLapEnd);
        public static string PendingReviewEvent { get; set; }
        public static void ReviewEvent(string id) { PendingReviewEvent = id; ReturnToMenu(); }
        private static double lastLapEnd;
        private static bool lapInvalid, reachedBrakingSpeed;
        public static bool BrakeStopCompleted { get; private set; }
        public static string LastError { get; private set; }
        private static bool pendingFinished;
        private static int pendingPosition;
        private static double pendingElapsed;
        private static bool launching;
        public static event Action<FormulaReceipt> Finished;
        public static event Action<int, double> CheckpointPassed;
        public static event Action LapCompleted;
        public static void EnterMenu() { launching = false; InRace = false; LastError = null; Time.timeScale = 1f; AudioListener.pause = false; }
        public static string Launch(string id, string mode)
        {
            if (launching) return "An event is already loading.";
            var e = FormulaCatalog.Current.Event(id);
            var track = e == null ? null : FormulaAssets.Current?.FindTrack(e.trackId);
            if (track == null || string.IsNullOrEmpty(track.scenePath) || !Application.CanStreamedLevelBeLoaded(track.scenePath)) return "This circuit is not available in this build.";
            if (mode == "career" && id.Contains(":")) return "Select a career event to earn career rewards.";
            if (!FormulaProgression.Commit(d => FormulaProgression.BeginRun(d, id, mode), out string error)) return error;
            launching = true;
            Time.timeScale = 1f; AudioListener.pause = false;
            try
            {
                var loader = LoadingManager.Instance;
                if (loader == null) loader = new GameObject("Formula Loading Manager").AddComponent<LoadingManager>();
                loader.LoadScene(track.scenePath);
            }
            catch (Exception failure) { launching = false; return "Failed to load the circuit: " + failure.Message; }
            return null;
        }
        public static void PrepareAgent(GameObject vehicle)
        {
            if (vehicle.GetComponentInChildren<NavMeshAgent>(true) != null) return;
            if (!NavMesh.SamplePosition(vehicle.transform.position, out NavMeshHit hit, 30f, NavMesh.AllAreas))
                throw new InvalidOperationException("No road navigation mesh near vehicle: " + vehicle.transform.position);
            var child = new GameObject("FormulaAgent"); child.SetActive(false); child.transform.SetParent(vehicle.transform, false); child.transform.position = hit.position;
            var agent = child.AddComponent<NavMeshAgent>(); agent.updatePosition = false; agent.updateRotation = false;
            child.SetActive(true); agent.Warp(hit.position);
        }
        public static void Configure(GamePlayManager game)
        {
            launching = false;
            if (game.GetComponent<FormulaRaceUI>() == null) { InRace = false; return; }
            var circuit = game.GetComponent<FormulaCircuit>();
            var eventTrack = CurrentEvent == null ? null : FormulaAssets.Current.FindTrack(CurrentEvent.trackId);
            if (CurrentEvent == null || string.IsNullOrEmpty(FormulaProgression.State.activeRunId) || eventTrack?.scenePath != game.gameObject.scene.path)
            {
                // Direct scene play starts the authored race and cannot award career rewards.
                string id = circuit != null ? "race:" + circuit.trackId : "g03";
                FormulaProgression.BeginRun(FormulaProgression.Data, id, "quick");
            }
            InRace = true; LastError = null; reachedBrakingSpeed = false; BrakeStopCompleted = false; BestLap = 0; ValidLaps = 0; LastLap = 0; LastLapValid = false; lastLapEnd = 0; lapInvalid = false;
            var e = CurrentEvent;
            CorridorHalfWidth = game.GetComponent<FormulaRaceUI>().trackHalfWidth;
            game.useCurrentMapModeSettings = false;
            game.RaceType = e.mode == "race" ? RaceType.Racing : RaceType.TimeAttack;
            game.totalRaceLaps = e.laps; game.opponentCount = e.opponentCount;
            game.copyPlayerPerformanceOnSpawn = false;
            game.enableDynamicRacePacing = false;
            game.autoSpawnOpponents = e.opponentCount > 0;
            game.showLiveLeaderboard = e.opponentCount > 0;
            // Menu mission indices must not feed the legacy mission economy.
            GlobalCarData.thismap = null;
        }
        public static float CorridorHalfWidth { get; private set; } = 8f;
        public static bool WaypointReached(Vector3 position, Vector3 previous, Vector3 target, float radius)
        {
            if (Mathf.Abs(position.y - target.y) > 6f) return false;
            Vector3 segment = target - previous; segment.y = 0;
            Vector3 offset = position - target; offset.y = 0;
            if (offset.magnitude <= radius) return true;
            float along = Vector3.Dot(offset, segment.normalized);
            float lateral = (offset - segment.normalized * along).magnitude;
            return along >= 0 && lateral <= CorridorHalfWidth && offset.magnitude <= Mathf.Max(20f, segment.magnitude * 2f);
        }
        public static void ObserveBraking(float speed, float brake)
        {
            reachedBrakingSpeed |= speed >= 30f;
            BrakeStopCompleted |= reachedBrakingSpeed && speed <= 3f && brake >= .5f;
        }
        public static FormulaReceipt RetryResult() => Complete(pendingFinished, pendingPosition, pendingElapsed);
        public static void InvalidateLap() { if (InRace) lapInvalid = true; }
        public static void Checkpoint(int index, double elapsed) { if (InRace) CheckpointPassed?.Invoke(index, LapElapsed(elapsed)); }
        public static void Lap(double elapsed)
        {
            if (!InRace) return;
            double time = elapsed - lastLapEnd; lastLapEnd = elapsed;
            LastLap = time; LastLapValid = !lapInvalid && time > 1;
            if (LastLapValid)
            {
                ValidLaps++; if (BestLap <= 0 || time < BestLap) BestLap = time;
            }
            lapInvalid = false;
            LapCompleted?.Invoke();
        }
        public static FormulaReceipt Complete(bool finished, int position, double elapsed)
        {
            pendingFinished = finished; pendingPosition = position; pendingElapsed = elapsed;
            var e = CurrentEvent;
            var p = FormulaProgression.State;
            bool valid = finished && ValidLaps > 0;
            Medal medal = Medal.None;
            if (valid && e.mode == "race") medal = position == 1 ? Medal.Gold : position == 2 ? Medal.Silver : position == 3 ? Medal.Bronze : Medal.None;
            else if (valid)
            {
                float reference = FormulaAssets.Current.FindTrack(e.trackId).referenceLapSeconds;
                double metric = e.mode == "familiarization" ? elapsed : BestLap;
                double gold = e.mode == "familiarization" ? 1.12 : 1.03;
                double silver = e.mode == "familiarization" ? 1.25 : 1.10;
                medal = reference > 0 && metric <= reference * gold ? Medal.Gold : reference > 0 && metric <= reference * silver ? Medal.Silver
                    : e.mode == "familiarization" || (reference > 0 && metric <= reference * 1.2) ? Medal.Bronze : Medal.None;
            }
            FormulaReceipt receipt = null;
            if (!FormulaProgression.Commit(d => { receipt = FormulaProgression.Complete(d, p.activeRunId, p.activeEventId, valid, medal, elapsed, BestLap); if (receipt != null) receipt.finishPosition = position; return receipt == null ? "Duplicate or invalid result." : null; }, out string error))
            {
                LastError = error; receipt = null;
                Debug.LogError("Formula result not saved: " + error);
            }
            else LastError = null;
            if (receipt != null) Finished?.Invoke(receipt);
            return receipt;
        }
        public static void ReturnToMenu()
        {
            EnterMenu();
            var loader = LoadingManager.Instance;
            if (loader == null) loader = new GameObject("Formula Loading Manager").AddComponent<LoadingManager>();
            loader.LoadScene("Menu");
        }
    }
}
