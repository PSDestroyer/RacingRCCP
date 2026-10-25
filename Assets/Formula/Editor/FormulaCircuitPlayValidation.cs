using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Real physics/AI circuit traversal, with a separate save and no simulated race completion.
[InitializeOnLoad]
public static class FormulaCircuitPlayValidation
{
    private const string Active = "Formula.Circuits.Validation";
    private const string Previous = Active + ".Previous";
    private static readonly List<object> results = new List<object>();
    private static readonly List<string> errors = new List<string>();
    private static readonly HashSet<string> passedTracks = new HashSet<string>();
    private static int errorsAtLaunch;
    private static int trackIndex, phase;
    private static double phaseStart;
    private static float progressAt, maximumSpeed;
    private static RCCP_AI playerDriver;
    private static string lastScene;
    private static int[] lastCheckpoints;
    private static double nextTelemetry;
    private static float[] individualProgressAt;
    private static object telemetry;
    private static string validationSave;
    private static bool failing;
    static FormulaCircuitPlayValidation() { EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    [MenuItem("Tools/Formula Career/Drive Validate All Formula Circuits")]
    public static void Run() => Start(0);
    [MenuItem("Tools/Formula Career/Resume Circuit Drive Validation")]
    public static void Resume()
    {
        var previous = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Docs/AI/FormulaCircuits/PlayValidation.json"));
        File.Copy("Docs/AI/FormulaCircuits/PlayValidation.json", "Docs/AI/FormulaCircuits/DriveAttempt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json", true);
        var completed = new Newtonsoft.Json.Linq.JArray(previous["results"].Where(r => r["passed"]?.Value<bool>() != false));
        Start(0);
        SessionState.SetString(Active + ".Results", completed.ToString());
    }
    private static void Start(int first)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetInt(Active + ".First", first);
        SessionState.SetString(Active + ".Results", "[]");
        validationSave = Path.Combine(Path.GetTempPath(), "FormulaCircuits-" + Guid.NewGuid().ToString("N"), "save.json");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", validationSave);
        SessionState.SetString(Previous, EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active, true); results.Clear(); errors.Clear(); failing = false; trackIndex = first; phase = 0;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            trackIndex = SessionState.GetInt(Active + ".First", 0); phase = 0;
            results.Clear(); errors.Clear(); failing = false; passedTracks.Clear();
            results.AddRange(Newtonsoft.Json.Linq.JArray.Parse(SessionState.GetString(Active + ".Results", "[]")).Select(r => (object)r));
            foreach (var result in results)
            {
                var item = Newtonsoft.Json.Linq.JObject.FromObject(result);
                if (item["track"] != null) passedTracks.Add((string)item["track"]);
            }
            phaseStart = EditorApplication.timeSinceStartup; Application.runInBackground = true;
            var type = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (type != null) EditorWindow.GetWindow(type).Focus();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Active, false); Time.timeScale = 1;
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", null);
            string path = SessionState.GetString(Previous, ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Next(int value) { phase = value; phaseStart = EditorApplication.timeSinceStartup; }
    private static void Save(string status) => File.WriteAllText("Docs/AI/FormulaCircuits/PlayValidation.json", JsonConvert.SerializeObject(new { status, trackIndex, phase, lastScene, method = "Editor Play Mode, isolated save, real RCCP physics. Test player has no transport or scripted finish; opponents use production recovery, counted per circuit.", telemetry, results, errors }, Formatting.Indented));
    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - phaseStart;
            if (age < 1) return;
            Require(age < (phase == 3 ? 480 : 90), "Circuit test timed out in phase " + phase);
            if (phase == 0)
            {
                var menu = Object.FindFirstObjectByType<FormulaMainMenu>(); if (menu == null) return;
                Require(FormulaAssets.Current.tracks.Length == 18, "Expected exactly 18 circuits.");
                menu.Show("Curse rapide");
                var quick = menu.View.Quick; Require(quick.PageCount == 3, "Circuit selection must contain three pages.");
                foreach (int mode in Enumerable.Range(0, 3))
                {
                    quick.SelectMode(mode);
                    foreach (int page in Enumerable.Range(0, 3))
                    {
                        quick.SetPage(page); var cards = quick.GetComponentsInChildren<FormulaEventCard>(); Require(cards.Length == 6, "Expected six visible circuits per page.");
                        foreach (var card in cards)
                        {
                            var e = FormulaCatalog.Current.Event(card.EventId); Require(e != null, "Missing quick event " + card.EventId);
                            var track = FormulaAssets.Current.FindTrack(e.trackId); Require(Application.CanStreamedLevelBeLoaded(track.scenePath), "Scene excluded from build " + track.scenePath);
                            Require(FormulaMenuAssets.Current.trackMaps.Any(t => t.id == track.id && t.points.Length > 10), "Circuit outline missing: " + track.id);
                            var data = JsonConvert.DeserializeObject<HalvaStudio.Save.SaveManager.SaveData>(JsonConvert.SerializeObject(FormulaProgression.Data));
                            int money = data.money, xp = data.exp;
                            string runMode = mode == 1 ? "time_trial" : "quick";
                            Require(FormulaProgression.BeginRun(data, e.id, "career") != null, "Free session can enter career reward path.");
                            Require(FormulaProgression.BeginRun(data, e.id, runMode) == null, "Free session was rejected.");
                            var receipt = FormulaProgression.Complete(data, data.formula.activeRunId, e.id, true, Medal.Gold, 120, 110);
                            Require(receipt != null && receipt.credits == 0 && receipt.xp == 0 && money == data.money && xp == data.exp, "Free session changed career economy.");
                        }
                    }
                }
                quick.SelectMode(0); quick.SetPage(0); ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCircuits/Selection.png");
                if (results.Count == 0) results.Add(new { check = "54 mode/circuit combinations, 18 build scenes, three selection pages, route outlines" });
                Next(1); Save("running");
            }
            else if (phase == 1)
            {
                while (trackIndex < FormulaAssets.Current.tracks.Length && passedTracks.Contains(FormulaAssets.Current.tracks[trackIndex].id)) trackIndex++;
                if (trackIndex >= FormulaAssets.Current.tracks.Length) { Save(errors.Count == 0 ? "passed" : "failed"); EditorApplication.ExitPlaymode(); return; }
                var track = FormulaAssets.Current.tracks[trackIndex]; lastScene = track.scenePath; errorsAtLaunch = errors.Count;
                string error = FormulaSession.Launch("race:" + track.id, "quick"); Require(error == null, error); Next(2); Save("running");
            }
            else if (phase == 2)
            {
                var game = Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || game.CarController == null || !game.FormulaRaceStarted || !game.CarController.GetComponent<FormulaVehicle>().IsReady) return;
                Require(game.gameObject.scene.path == lastScene, "Wrong scene loaded.");
                Require(game.aiRacers.Length == 3 && game.aiRacers.All(r => r.aiDriver != null), "Three AI opponents were not spawned.");
                Require(game.FormulaCompletedLaps == 0, "A lap was awarded while leaving the grid.");
                Require(game.aiRacers.All(r => r.aiDriver.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>().isOnNavMesh), "AI not attached to road navigation.");
                FormulaSession.PrepareAgent(game.CarController.gameObject); playerDriver = game.CarController.gameObject.AddComponent<RCCP_AI>();
                playerDriver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; playerDriver.waypointsContainer = game.raceWaypoints;
                game.GetComponent<FormulaCircuit>().ConfigureDriver(playerDriver);
                // Keep the driving probe running until all four cars cross the line;
                // the normal three-lap finish flow is covered separately.
                game.totalRaceLaps = 999;
                individualProgressAt = Enumerable.Repeat(Time.time, 4).ToArray();
                maximumSpeed = 0; progressAt = Time.time; lastCheckpoints = new int[4];
                Time.timeScale = 3; Next(3); Save("driving");
            }
            else if (phase == 3)
            {
                var game = Object.FindFirstObjectByType<GamePlayManager>(); var car = game.CarController;
                string trackId = FormulaAssets.Current.tracks[trackIndex].id;
                if (trackId == "rtg_09" || trackId == "rtg_10")
                    Require(game.FormulaElapsed < 360, "Sloped circuit pace regressed: excessive braking before the first valid lap.");
                maximumSpeed = Mathf.Max(maximumSpeed, car.absoluteSpeed);
                if (EditorApplication.timeSinceStartup >= nextTelemetry)
                {
                    telemetry = new { elapsed = game.FormulaElapsed, timeScale = Time.timeScale, playerValidLaps = FormulaSession.ValidLaps, playerCheckpoints = playerDriver.currentWaypointIndex, playerRaceState = PlayerRaceState(game),
                        opponents = game.aiRacers.Select(r => DriverState(r)).ToArray() };
                    nextTelemetry = EditorApplication.timeSinceStartup + 5; Save("driving");
                }
                int[] checkpoints = new[] { playerDriver.currentWaypointIndex }.Concat(game.aiRacers.Select(r => r.aiDriver.currentWaypointIndex)).ToArray();
                for (int i = 0; i < 4; i++)
                {
                    if (checkpoints[i] != lastCheckpoints[i]) individualProgressAt[i] = Time.time;
                    Require(Time.time - individualProgressAt[i] < 90, "Racer " + i + " made no checkpoint progress for 90 simulated seconds.");
                }
                if (!checkpoints.SequenceEqual(lastCheckpoints)) { progressAt = Time.time; lastCheckpoints = checkpoints; }
                Require(Time.time - progressAt < 55, "Grid stopped progressing for 55 simulated seconds: " + string.Join(",", checkpoints));
                float routeHeight = game.raceWaypoints.waypoints[playerDriver.currentWaypointIndex].transform.position.y;
                Require(car.transform.position.y > routeHeight - 10, "Player fell below the local road profile.");
                if (FormulaSession.ValidLaps < 1 || !game.aiRacers.All(r => r.completedLaps >= 1)) return;
                Require(errors.Count == errorsAtLaunch, "Runtime error during this circuit; see error stack trace.");
                Require(maximumSpeed > 35, "Car never reached racing speed.");
                passedTracks.Add(FormulaAssets.Current.tracks[trackIndex].id);
                results.Add(new { track = FormulaAssets.Current.tracks[trackIndex].id, passed = true, playerValidLaps = FormulaSession.ValidLaps, aiLaps = game.aiRacers.Select(r => r.completedLaps).ToArray(), aiRecoveries = game.aiRacers.Select(r => r.aiDriver.GetComponent<FormulaAIRecovery>().RecoveryCount).ToArray(), maximumSpeed, elapsed = game.FormulaElapsed, errors = 0 });
                Time.timeScale = 1; ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCircuits/" + FormulaAssets.Current.tracks[trackIndex].id + ".png");
                trackIndex++; Next(4); Save("running");
            }
            else if (phase == 4) { FormulaSession.ReturnToMenu(); Next(5); }
            else if (phase == 5 && Object.FindFirstObjectByType<FormulaMainMenu>() != null) { Next(1); }
        }
        catch (Exception failure)
        {
            failing = true;
            errors.Add(lastScene + ": " + failure.Message);
            Time.timeScale = 1;
            if (phase >= 1 && phase <= 3)
            {
                string id = FormulaAssets.Current.tracks[trackIndex].id;
                ScreenCapture.CaptureScreenshot("Docs/AI/FormulaCircuits/" + id + "-Failed.png");
                results.Add(new { track = id, passed = false, reason = failure.Message });
                trackIndex++; Next(4); Save("continuing_after_failure");
                Debug.LogError("Circuit validation failed: " + id + ": " + failure.Message); failing = false;
            }
            else { Save("failed"); EditorApplication.ExitPlaymode(); Debug.LogException(failure); }
        }
    }
    private static object PlayerRaceState(GamePlayManager game)
    {
        var racer = (GamePlayManager.RaceRacer)typeof(GamePlayManager).GetField("playerRacer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
        Vector3 position = game.CarController.transform.position;
        Vector3 target = game.raceWaypoints.waypoints[racer.currentWaypointIndex].transform.position;
        return new { racer.currentWaypointIndex, racer.completedLaps, racer.lapCountingArmed,
            position = new[] { position.x, position.y, position.z }, target = new[] { target.x, target.y, target.z }, verticalError = position.y - target.y };
    }
    private static object DriverState(GamePlayManager.RaceRacer racer)
    {
        var ai = racer.aiDriver; var car = ai.CarController;
        var agent = car.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        return new { checkpoint = ai.currentWaypointIndex, laps = racer.completedLaps, speed = car.absoluteSpeed, controlDiagnostics = ControlDiagnostics(ai),
            recoveries = ai.GetComponent<FormulaAIRecovery>()?.RecoveryCount ?? 0, canControl = car.canControl, reverse = ai.reverseNow, stop = ai.stopNow,
            throttle = ai.inputs.throttleInput, brake = ai.inputs.brakeInput, steering = ai.inputs.steerInput,
            groundedWheels = car.GetComponentsInChildren<WheelCollider>().Count(w => w.isGrounded),
            nearbyGeometry = Physics.OverlapBox(car.transform.position + Vector3.up * .4f, new Vector3(1.2f, .25f, 2.5f), car.transform.rotation, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(car.transform)).Select(c => c.name).Distinct().Take(8).ToArray(),
            onNavMesh = agent.isOnNavMesh, hasPath = agent.hasPath, pending = agent.pathPending, pathStatus = agent.pathStatus.ToString(),
            position = new[] { racer.racerTransform.position.x, racer.racerTransform.position.y, racer.racerTransform.position.z } };
    }
    private static object ControlDiagnostics(RCCP_AI ai)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Invoke(string name, params object[] args) => typeof(RCCP_AI).GetMethod(name, flags).Invoke(ai, args);
        var car = ai.CarController;
        var zone = (RCCP_AIBrakeZone)typeof(RCCP_AI).GetField("currentBrakeZone", flags).GetValue(ai);
        float lookAhead = (float)typeof(RCCP_AI).GetField("smoothedSteeringLookAhead", flags).GetValue(ai);
        float pathError = (float)Invoke("GetCurrentPathError");
        float recovery = Mathf.InverseLerp(ai.pathRecoveryDistance, ai.pathRecoveryDistance * 2.5f, pathError);
        Vector3 look = (Vector3)Invoke("GetSteeringLookAheadPoint", lookAhead);
        look = (Vector3)Invoke("GetRecoveryAwareLookPoint", look, recovery);
        Vector3 predictedPosition = car.transform.position + car.Rigid.linearVelocity * .5f;
        Quaternion predictedRotation = car.transform.rotation * Quaternion.Euler(car.Rigid.angularVelocity * Mathf.Rad2Deg * .5f);
        Vector3 direction = look - predictedPosition;
        return new { ai.useBrakeZoneSpeedProfile, zoneSpeed = zone == null ? -1 : zone.targetSpeed, lookAhead, pathError, recovery,
            navRadius = (float)Invoke("GetTightestRadiusAhead", ai.raceLookAhead), cornerAngle = (float)Invoke("GetUpcomingCornerAngle", ai.cornerDetectionDistance),
            predictedAngle = Vector3.Angle(predictedRotation * Vector3.forward, direction), planarAngle = Vector3.Angle(Vector3.ProjectOnPlane(predictedRotation * Vector3.forward, Vector3.up), Vector3.ProjectOnPlane(direction, Vector3.up)),
            yawRate = car.Rigid.angularVelocity.y * Mathf.Rad2Deg, pitch = car.transform.eulerAngles.x, roll = car.transform.eulerAngles.z,
            wheels = car.GetComponentsInChildren<WheelCollider>().Select(w => new { w.rpm, w.motorTorque, w.brakeTorque, w.steerAngle, forwardStiffness = w.forwardFriction.stiffness }).ToArray() };
    }
    private static void Log(string message, string trace, LogType type)
    {
        if (failing || !SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors.Add(message + "\n" + trace); Save("runtime-error"); }
    }
}
