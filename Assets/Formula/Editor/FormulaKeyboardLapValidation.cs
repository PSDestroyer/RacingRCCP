using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only route follower uses virtual keyboard/gamepad devices through real input actions.
// It never transports the car, writes its velocity, adds an RCCP_AI, or grants a lap/result.
[InitializeOnLoad]
public static class FormulaKeyboardLapValidation
{
    private const string Key = "Formula.KeyboardLap", Folder = "Docs/AI/FormulaHandling";
    private static Keyboard keyboard;
    private static Gamepad gamepad;
    private static bool gamepadMode, measurementFocusLost;
    private static Vector3[] points;
    private static float began, maxSpeed, maxSlip, maxDistance, lastControlFixedTime, previousRackAngle, nextTraceTime;
    private static int phase, frames, progressIndex, analogSteeringFrames, analogTriggerFrames, heldSteering;
    private static double started, nextWrite;
    private static readonly List<string> errors = new List<string>();
    private static readonly List<DriverTrace> trace = new List<DriverTrace>();
    private static DriverTrace latestTrace;
    private static string status, interruptionReason;
    private static Vector3[] rivalStarts;
    [Serializable]
    private sealed class DriverTrace
    {
        public double time;
        public int routeIndex, keyboardSteer, frontGrounded, rearGrounded;
        public float speed, targetSpeed, desiredAngle, actualAngle, rawSteer, processedSteer, slip, routeError;
        public float gamepadStickX, gamepadStickY;
        public float rawThrottle, tractionTorqueScale;
        public bool applicationFocused, gamepadEnabled, canControl, externalControl, overridePlayerInputs, overrideExternalInputs, managerOverrideInputs;
        public bool steeringActionEnabled, throttleActionEnabled;
    }
    static FormulaKeyboardLapValidation() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode; Application.logMessageReceived += Log; Application.focusChanged += FocusChanged; }

    [MenuItem("Tools/Formula Career/Validate Keyboard Formula Lap")]
    public static void Run() => Start(false);
    [MenuItem("Tools/Formula Career/Validate Gamepad Formula Lap")]
    public static void GamepadLap() => Start(false, true);
    [MenuItem("Tools/Formula Career/Validate Formula Steering Profiles")]
    public static void Profiles() => Start(true);
    private static void Start(bool profiles, bool useGamepad = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        string previousSave = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSaveOverride", previousSave != null);
        SessionState.SetString(Key + ".SaveOverride", previousSave ?? "");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaKeyboardLap-" + Guid.NewGuid().ToString("N"), "save.json"));
        SessionState.SetBool(Key + ".Profiles", profiles);
        SessionState.SetBool(Key + ".Gamepad", useGamepad);
        SessionState.SetBool(Key, true); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            phase = 0; frames = 0; maxSpeed = maxSlip = maxDistance = previousRackAngle = 0; heldSteering = 0;
            lastControlFixedTime = -1; nextTraceTime = 0; trace.Clear(); latestTrace = null;
            measurementFocusLost = false; interruptionReason = null;
            errors.Clear(); points = null; rivalStarts = null; status = "running"; started = EditorApplication.timeSinceStartup;
            gamepadMode = SessionState.GetBool(Key + ".Gamepad", false); analogSteeringFrames = analogTriggerFrames = 0;
            Application.runInBackground = true; Time.timeScale = 1;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        if (value == PlayModeStateChange.EnteredEditMode)
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); keyboard = null;
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad); gamepad = null;
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSaveOverride", false) ? SessionState.GetString(Key + ".SaveOverride", "") : null); SessionState.SetBool(Key, false);
            string previous = SessionState.GetString(Key + ".Previous", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
        try
        {
            if (EditorApplication.timeSinceStartup - started > 420) throw new Exception((gamepadMode ? "Gamepad" : "Keyboard") + " lap timed out.");
            if (phase == 0)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null) return;
                bool profiles = SessionState.GetBool(Key + ".Profiles", false);
                string error = FormulaSession.Launch(profiles ? "race:track_01" : "g01", profiles ? "quick" : "career"); if (error != null) throw new Exception(error); phase = 1;
            }
            else if (phase == 1)
            {
                // A new local input test needs an active Editor before devices
                // are added. A later focus loss still interrupts the measurement.
                if (!Application.isFocused) return;
                if (game == null || game.CarController == null || !game.FormulaRaceStarted) return;
                if (!Mathf.Approximately(game.CarController.Inputs.steeringCurve.Evaluate(200), FormulaHandlingAuthoring.SteeringCurve().Evaluate(200)) || game.CarController.GetComponent<FormulaSteering>() == null) throw new Exception("Player did not load the progressive steering profile.");
                if (SessionState.GetBool(Key + ".Profiles", false))
                {
                    if (game.aiRacers.Length != 3) throw new Exception("Expected three actual AI opponents.");
                    rivalStarts = new Vector3[3];
                    for (int i = 0; i < 3; i++)
                    {
                        var rival = game.aiRacers[i].racerTransform.GetComponent<RCCP_CarController>();
                        if (!Mathf.Approximately(rival.Inputs.steeringCurve.Evaluate(180), .27f)) throw new Exception("AI inherited the player's digital steering curve.");
                        rivalStarts[i] = rival.transform.position;
                    }
                }
                points = new Vector3[game.raceWaypoints.waypoints.Count]; for (int i = 0; i < points.Length; i++) points[i] = game.raceWaypoints.waypoints[i].transform.position;
                if (gamepadMode) gamepad = InputSystem.AddDevice<Gamepad>("Formula analog gamepad lap acceptance");
                else keyboard = InputSystem.AddDevice<Keyboard>("Formula keyboard lap acceptance");
                began = Time.time; phase = 2;
            }
            else if (phase == 2)
            {
                if (game == null || game.CarController == null) return;
                if (measurementFocusLost || !Application.isFocused)
                {
                    measurementFocusLost = true;
                    latestTrace = CaptureTrace(game, latestTrace?.targetSpeed ?? 0, latestTrace?.desiredAngle ?? 0, heldSteering, latestTrace?.routeError ?? 0);
                    trace.Add(latestTrace);
                    interruptionReason = "Application focus was lost during input-device measurement. RCCP disables its input actions on focus loss; this run cannot assess vehicle handling.";
                    status = "interrupted"; NeutralInputs(); Write(game); EditorApplication.ExitPlaymode(); return;
                }
                if (SessionState.GetBool(Key + ".Profiles", false) && Time.time - began > 8)
                {
                    NeutralInputs();
                    for (int i = 0; i < 3; i++)
                    {
                        var rival = game.aiRacers[i].racerTransform;
                        var agent = rival.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
                        if (Vector3.Distance(rivalStarts[i], rival.position) < 10 || agent == null || !agent.isOnNavMesh) throw new Exception("AI did not drive normally with its preserved steering profile; see opponent telemetry.");
                        if (!Mathf.Approximately(rival.GetComponent<RCCP_CarController>().FrontAxle.steerSpeed, 10f)) throw new Exception("Player steering smoothing overwrote an AI opponent's rack speed.");
                    }
                    status = errors.Count == 0 ? "passed" : "failed"; Write(game); EditorApplication.ExitPlaymode(); return;
                }
                if (UnityEngine.Object.FindFirstObjectByType<FormulaResultsView>() != null)
                {
                    NeutralInputs();
                    if (FormulaSession.ValidLaps < 1 || FormulaSession.BestLap <= 0 || FormulaProgression.State.Best("g01") < Medal.Bronze) throw new Exception("Physical input-device run did not earn a valid career lap.");
                    if (gamepadMode && (analogSteeringFrames < 30 || analogTriggerFrames < 30)) throw new Exception("Gamepad lap did not exercise fractional steering and trigger input through RCCP.");
                    phase = 3; began = Time.time; return;
                }
                var car = game.CarController;
                if (FormulaSession.LapInvalid) throw new Exception("Input-device route follower exceeded track limits or missed a checkpoint.");
                // Keep each binary button state held between physics steps. Render-frame
                // pulse density does not represent mean rack angle with asymmetric rack rates.
                if (Time.fixedTime <= lastControlFixedTime) return;
                float controlDelta = lastControlFixedTime < 0 ? Time.fixedDeltaTime : Time.fixedTime - lastControlFixedTime;
                lastControlFixedTime = Time.fixedTime;
                Vector3 here = car.transform.position;
                Vector3 projected = Vector3.zero; float nearest = float.PositiveInfinity;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 a = points[i], d = points[(i + 1) % points.Length] - a; d.y = 0; Vector3 offset = here - a; offset.y = 0;
                    float t = Mathf.Clamp01(Vector3.Dot(offset, d) / Mathf.Max(.001f, d.sqrMagnitude)); Vector3 p = a + d * t;
                    float distance = (new Vector2(p.x - here.x, p.z - here.z)).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; progressIndex = i; projected = p; }
                }
                maxDistance = Mathf.Max(maxDistance, Mathf.Sqrt(nearest));
                float ahead = Mathf.Clamp(10 + car.absoluteSpeed * .12f, 14, 34), remaining = ahead;
                int index = progressIndex; Vector3 target = projected;
                for (int j = 0; j < points.Length && remaining > 0; j++)
                {
                    Vector3 end = points[(index + 1) % points.Length]; float length = Vector3.Distance(target, end);
                    if (length >= remaining) { target = Vector3.Lerp(target, end, remaining / Mathf.Max(.001f, length)); break; }
                    remaining -= length; target = end; index = (index + 1) % points.Length;
                }
                Vector3 local = car.transform.InverseTransformPoint(target); local.y = 0;
                float desiredAngle = Mathf.Atan(2 * 3.25f * local.x / Mathf.Max(1, local.sqrMagnitude)) * Mathf.Rad2Deg;
                float maximum = car.FrontAxle.maxSteerAngle * car.Inputs.steeringCurve.Evaluate(car.absoluteSpeed);
                float requested = Mathf.Clamp(desiredAngle / Mathf.Max(.1f, maximum), -1, 1);
                int steering = KeyboardSteering(requested * maximum, car.FrontAxle.steerAngle, maximum, controlDelta);
                var plan = game.GetComponent<FormulaCircuit>().aiSpeedPlan;
                float targetSpeed = Mathf.Clamp(Mathf.Min(plan[progressIndex], plan[index]), 65, 170);
                bool brake = car.absoluteSpeed > targetSpeed + 5;
                bool throttle = !brake && car.absoluteSpeed < targetSpeed + 1;
                if (gamepadMode)
                {
                    // The same geometric route target becomes a continuous stick request.
                    // Compensate only RCCP's known deadzone; input bindings/processors remain active.
                    float stick = Mathf.Abs(requested) < .001f ? 0 : Mathf.Sign(requested) * Mathf.Lerp(car.Inputs.steeringDeadzone, 1f, Mathf.Abs(requested));
                    float brakeAmount = Mathf.Clamp01((car.absoluteSpeed - targetSpeed - 2f) / 12f);
                    float throttleAmount = brakeAmount > 0 ? 0 : Mathf.Clamp01((targetSpeed + 1f - car.absoluteSpeed) / 12f);
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(stick, 0), rightTrigger = throttleAmount, leftTrigger = brakeAmount });
                    float observedSteer = Mathf.Abs(car.Inputs.inputs.steerInput);
                    if (observedSteer > .05f && observedSteer < .95f) analogSteeringFrames++;
                    if (car.Inputs.inputs.throttleInput > .05f && car.Inputs.inputs.throttleInput < .95f || car.Inputs.inputs.brakeInput > .05f && car.Inputs.inputs.brakeInput < .95f) analogTriggerFrames++;
                }
                else
                {
                    var keys = new List<UnityEngine.InputSystem.Key>(2);
                    if (throttle) keys.Add(UnityEngine.InputSystem.Key.W);
                    if (brake) keys.Add(UnityEngine.InputSystem.Key.S);
                    if (steering != 0) keys.Add(steering > 0 ? UnityEngine.InputSystem.Key.D : UnityEngine.InputSystem.Key.A);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                }
                Vector3 velocity = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
                float slip = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                if (car.absoluteSpeed > 40) maxSlip = Mathf.Max(maxSlip, Mathf.Abs(slip));
                maxSpeed = Mathf.Max(maxSpeed, car.absoluteSpeed); frames++;
                latestTrace = CaptureTrace(game, targetSpeed, desiredAngle, steering, Mathf.Sqrt(nearest));
                if (Time.time >= nextTraceTime) { nextTraceTime = Time.time + .2f; trace.Add(latestTrace); }
                if (EditorApplication.timeSinceStartup > nextWrite) { nextWrite = EditorApplication.timeSinceStartup + 5; Write(game); }
            }
            else if (phase == 3 && Time.time - began > 2)
            {
                var agent = game.CarController.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
                if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) throw new Exception("Post-finish AI has no valid navigation binding.");
                if (!Mathf.Approximately(game.CarController.Inputs.steeringCurve.Evaluate(180), .27f)) throw new Exception("Post-finish AI did not receive its autonomous steering calibration.");
                if (!Mathf.Approximately(game.CarController.FrontAxle.steerSpeed, 10f)) throw new Exception("Post-finish AI rack speed was overwritten by player steering smoothing.");
                if (!Mathf.Approximately(game.CarController.GetComponent<FormulaTractionControl>().TorqueScale, 1f)) throw new Exception("Player traction protection remained active after AI takeover.");
                status = errors.Count == 0 ? "passed" : "failed"; Write(game);
                ScreenCapture.CaptureScreenshot(Folder + (gamepadMode ? "/Gamepad-Lap-Results.png" : "/Keyboard-Lap-Results.png")); phase = 4; began = Time.time;
            }
            else if (phase == 4 && Time.time - began > .6f)
            {
                if (!Mathf.Approximately(game.CarController.FrontAxle.steerSpeed, 10f)) throw new Exception("Post-finish AI rack speed did not remain at its autonomous calibration.");
                Write(game); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception e)
        {
            if (latestTrace != null && (trace.Count == 0 || trace[trace.Count - 1] != latestTrace)) trace.Add(latestTrace);
            errors.Add(e.Message); status = "failed"; Write(game); EditorApplication.ExitPlaymode();
        }
    }
    private static void FocusChanged(bool focused)
    {
        if (!focused && phase == 2 && SessionState.GetBool(Key, false)) measurementFocusLost = true;
    }
    private static DriverTrace CaptureTrace(GamePlayManager game, float targetSpeed, float desiredAngle, int steering, float routeError)
    {
        var car = game.CarController;
        var manager = car.Inputs.RCCPInputManager;
        var actions = manager != null ? manager.inputActionsInstance : null;
        Vector2 stick = gamepad != null && gamepad.added ? gamepad.leftStick.ReadValue() : Vector2.zero;
        Vector3 velocity = car.transform.InverseTransformDirection(car.Rigid.linearVelocity);
        return new DriverTrace
        {
            time = game.FormulaElapsed, routeIndex = progressIndex, speed = car.absoluteSpeed,
            targetSpeed = targetSpeed, desiredAngle = desiredAngle, actualAngle = car.FrontAxle.steerAngle,
            rawSteer = car.Inputs.inputs.steerInput, processedSteer = car.steerInput_P,
            rawThrottle = car.Inputs.inputs.throttleInput, tractionTorqueScale = car.GetComponent<FormulaTractionControl>().TorqueScale,
            keyboardSteer = gamepadMode ? 0 : steering, slip = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg, routeError = routeError,
            frontGrounded = (car.FrontAxle.leftWheelCollider.WheelCollider.isGrounded ? 1 : 0) + (car.FrontAxle.rightWheelCollider.WheelCollider.isGrounded ? 1 : 0),
            rearGrounded = (car.RearAxle.leftWheelCollider.WheelCollider.isGrounded ? 1 : 0) + (car.RearAxle.rightWheelCollider.WheelCollider.isGrounded ? 1 : 0),
            applicationFocused = Application.isFocused, gamepadEnabled = gamepad != null && gamepad.added && gamepad.enabled,
            gamepadStickX = stick.x, gamepadStickY = stick.y,
            canControl = car.canControl, externalControl = car.externalControl,
            overridePlayerInputs = car.Inputs.overridePlayerInputs, overrideExternalInputs = car.Inputs.overrideExternalInputs,
            managerOverrideInputs = manager != null && manager.overrideInputs,
            steeringActionEnabled = actions != null && actions.FindAction("Steering", false)?.enabled == true,
            throttleActionEnabled = actions != null && actions.FindAction("Throttle", false)?.enabled == true
        };
    }
    private static int KeyboardSteering(float targetAngle, float actualAngle, float maximum, float controlDelta)
    {
        // Follow the real steering rack instead of assuming an input duty cycle is
        // proportional to wheel angle. One-step prediction allows for the existing
        // keyboard -> RCCP Update -> axle propagation delay without bypassing it.
        float observedRate = (actualAngle - previousRackAngle) / Mathf.Max(.001f, controlDelta);
        previousRackAngle = actualAngle;
        float prediction = actualAngle + Mathf.Clamp(observedRate * Time.fixedDeltaTime, -maximum * .2f, maximum * .2f);
        float deadband = Mathf.Max(.05f, maximum * .025f);
        int direction = targetAngle > deadband ? 1 : targetAngle < -deadband ? -1 : 0;
        if (direction == 0) heldSteering = 0;
        else
        {
            float directedError = direction * (targetAngle - prediction);
            if (directedError > deadband) heldSteering = direction;
            else if (directedError < -deadband || heldSteering != direction) heldSteering = 0;
        }
        return heldSteering;
    }
    private static void NeutralInputs()
    {
        if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        if (gamepad != null) InputSystem.QueueStateEvent(gamepad, new GamepadState());
    }
    private static void Write(GamePlayManager game)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + (SessionState.GetBool(Key + ".Profiles", false) ? "/AIProfiles.json" : gamepadMode ? "/GamepadLap.json" : "/KeyboardLap.json"), JsonConvert.SerializeObject(new
        {
            status, phase, interruptionReason, measurementFocusLost, unityVersion = Application.unityVersion,
            method = SessionState.GetBool(Key + ".Profiles", false) ? "Four-car Quick Race: actual player/AI steering profiles and eight seconds of opponent physics/navigation" : gamepadMode ? "Automated continuous analog left-stick/trigger route follower using a virtual Gamepad through real Input System bindings, updated once per observed physics step; no AI during the timed lap, no transport, velocity writes, raw input override or scripted finish; normal AI takeover after crossing the line" : "Automated binary W/A/S/D route follower through Input System, updated once per observed physics step with held/released steering selected from actual rack-angle feedback; no AI during the timed lap, no transport, velocity writes or scripted finish; normal AI takeover after crossing the line",
            frames, maxSpeed, maxSlipAngle = maxSlip, maxDistanceFromRoute = maxDistance, routeIndex = progressIndex,
            fixedDeltaTime = Time.fixedDeltaTime, trace,
            analogSteeringFrames, analogTriggerFrames,
            finishAiSteerSpeed = phase >= 3 && game != null && game.CarController != null ? game.CarController.FrontAxle.steerSpeed : 0,
            opponents = game != null && rivalStarts != null ? game.aiRacers.Select((r, i) => new { index = i, distance = Vector3.Distance(rivalStarts[i], r.racerTransform.position), speed = r.racerTransform.GetComponent<RCCP_CarController>().absoluteSpeed, navBound = r.racerTransform.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>().isOnNavMesh }).ToArray() : null,
            elapsed = game != null ? game.FormulaElapsed : 0, validLaps = FormulaSession.ValidLaps, bestLap = FormulaSession.BestLap, isolatedSave = true, errors
        }, Formatting.Indented));
    }
    private static void Log(string message, string trace, LogType type) { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) errors.Add(message); }
}
