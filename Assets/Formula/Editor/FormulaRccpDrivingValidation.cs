using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Runs the actual RCCP vehicle loop in isolated test-drive scenes, then restores
// the user's original scene setup. No saved scene or player save is rewritten.
[InitializeOnLoad]
public static class FormulaRccpDrivingValidation
{
    private const string ActiveKey = "FormulaRccpValidation.Active";
    private const string IndexKey = "FormulaRccpValidation.Index";
    private const string SetupKey = "FormulaRccpValidation.SceneSetup";
    private const string ResultsKey = "FormulaRccpValidation.Results";
    private const string Folder = "Docs/AI/FormulaRccp";
    private static readonly string[] Scenes = { "Formula_A1_TestDrive", "Formula_Generic_TestDrive" };
    private static RCCP_CarController car;
    private static RCCP_Inputs inputs;
    private static Vector3 startPosition;
    private static Quaternion startWheelRotation;
    private static float startTime;
    private static double realStartTime;
    private static bool captured;
    private static bool stoppedAfterBraking;
    private static bool reverseStarted;
    private static Vector3 reverseStartPosition;
    private static Vector3 reverseForward;
    private static Result current;

    [Serializable] public class Report
    {
        public string editorVersion;
        public List<Result> results = new List<Result>();
    }
    [Serializable] public class Result
    {
        public string model;
        public bool passed;
        public int frames;
        public int groundedWheels;
        public float maximumSpeedKmh;
        public float distanceMeters;
        public float speedBeforeBrakingKmh;
        public float speedAfterBrakingKmh;
        public float steeringDegrees;
        public float wheelRotationDegrees;
        public int playingAudioSources;
        public bool rearLightResponds;
        public bool physicsRemainsEnabledAtDistance;
        public bool reverseEngaged;
        public float reverseSpeedKmh;
        public float reverseDistanceMeters;
        public bool forwardRecovered;
        public List<string> errors = new List<string>();
        public List<string> baselineMessages = new List<string>();
    }

    static FormulaRccpDrivingValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }

    [MenuItem("Tools/Formula RCCP/Run Driving Validation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running validation.");
        FormulaRccpBuilder.ValidatePrefabs();
        Directory.CreateDirectory(Folder);
        SessionState.SetString(SetupKey, EditorSceneManager.playModeStartScene == null ? ""
            : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(ResultsKey, JsonUtility.ToJson(new Report { editorVersion = Application.unityVersion }));
        SessionState.SetInt(IndexKey, 0);
        SessionState.SetBool(ActiveKey, true);
        OpenNextScene();
    }

    private static void OpenNextScene()
    {
        string path = FormulaRccpBuilder.Output + "/Scenes/" + Scenes[SessionState.GetInt(IndexKey, 0)] + ".unity";
        // Unity restores the entire edit-time scene setup, including unsaved
        // edits, when Play Mode exits. No scene is closed or saved by this tool.
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            current = new Result { model = Scenes[SessionState.GetInt(IndexKey, 0)] };
            realStartTime = EditorApplication.timeSinceStartup;
            startTime = Time.time;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            car = UnityEngine.Object.FindFirstObjectByType<RCCP_CarController>();
            inputs = new RCCP_Inputs();
            captured = false;
            stoppedAfterBraking = false;
            reverseStarted = false;
            if (car == null)
            {
                current.errors.Add("No RCCP car spawned.");
                Complete();
                return;
            }
            RCCP.RegisterPlayerVehicle(car);
            car.Inputs.OverrideInputs(inputs);
            car.canControl = true;
            car.Engine.engineRunning = true;
            startPosition = car.transform.position;
            startWheelRotation = Quaternion.Inverse(car.transform.rotation) * car.FrontAxle.leftWheelCollider.wheelModel.rotation;
            Type view = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (view != null) EditorWindow.GetWindow(view).Focus();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            int next = SessionState.GetInt(IndexKey, 0) + 1;
            if (next < Scenes.Length)
            {
                SessionState.SetInt(IndexKey, next);
                EditorApplication.delayCall += OpenNextScene;
            }
            else
            {
                SessionState.SetBool(ActiveKey, false);
                string previous = SessionState.GetString(SetupKey, "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null
                    : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                Debug.Log("FORMULA_RCCP_DRIVING_VALIDATION_COMPLETE: " + Folder + "/DrivingValidation.json");
            }
        }
    }

    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || current == null || car == null)
            return;
        if (EditorApplication.timeSinceStartup - realStartTime > 45d)
        {
            current.errors.Add("Timed out: the Editor did not advance the required driving scenario.");
            Complete();
            return;
        }
        EditorApplication.QueuePlayerLoopUpdate();
        float elapsed = Time.time - startTime;
        current.frames = Time.frameCount;
        current.maximumSpeedKmh = Mathf.Max(current.maximumSpeedKmh, car.absoluteSpeed);
        current.distanceMeters = Mathf.Max(current.distanceMeters, Vector3.Distance(startPosition, car.transform.position));
        inputs.throttleInput = elapsed < 1.5f ? 0f : elapsed < 6.5f ? 0.7f : elapsed < 7.5f ? 0.3f
            : elapsed >= 13.5f ? 0.7f : 0f;
        // Release the brake as soon as the car stops, then separately test a
        // sustained S/left-trigger input and W/right-trigger recovery.
        if (elapsed >= 7.5f && elapsed < 10.5f)
        {
            if (!stoppedAfterBraking)
            {
                current.speedAfterBrakingKmh = car.absoluteSpeed;
                stoppedAfterBraking = car.absoluteSpeed < 1f;
            }
            current.rearLightResponds |= car.GetComponentsInChildren<RCCP_Light>(true)
                .Where(l => l.lightType == RCCP_Light.LightType.Brakelight).Any(l => l.GetComponent<Light>().intensity > 0.05f);
        }
        inputs.brakeInput = elapsed < 1.5f ? 0.5f
            : elapsed >= 7.5f && elapsed < 10.5f && !stoppedAfterBraking ? 1f
            : elapsed >= 10.5f && elapsed < 13.5f ? 1f : 0f;
        inputs.steerInput = elapsed >= 6.5f && elapsed < 7.5f ? 0.35f : 0f;
        car.Inputs.OverrideInputs(inputs);
        if (elapsed >= 10.5f && elapsed < 13.5f)
        {
            if (!reverseStarted)
            {
                reverseStarted = true;
                reverseStartPosition = car.transform.position;
                reverseForward = car.transform.forward;
            }
            current.reverseEngaged |= car.reversingNow;
            current.reverseSpeedKmh = Mathf.Min(current.reverseSpeedKmh, car.speed);
            current.reverseDistanceMeters = Mathf.Max(current.reverseDistanceMeters,
                -Vector3.Dot(car.transform.position - reverseStartPosition, reverseForward));
        }
        if (elapsed >= 13.5f)
            current.forwardRecovered |= !car.reversingNow && !car.shiftingNow && car.speed > 5f;
        if (elapsed > 1f && elapsed < 1.5f)
            current.groundedWheels = Mathf.Max(current.groundedWheels, car.AllWheelColliders.Count(w => w.WheelCollider.isGrounded));
        if (!captured && elapsed > 1.2f)
        {
            CaptureVehicle(car, Folder + "/" + (SessionState.GetInt(IndexKey, 0) == 0 ? "A1.png" : "Generic.png"));
            captured = true;
        }
        if (elapsed > 6.5f && elapsed < 7.5f)
        {
            current.steeringDegrees = Mathf.Max(current.steeringDegrees, Mathf.Abs(car.FrontAxle.leftWheelCollider.WheelCollider.steerAngle));
            // RCCP inserts and rotates a new visual pivot in Start. Inspect the
            // collider's live wheelModel, rather than the original child pivot.
            Quaternion rotation = Quaternion.Inverse(car.transform.rotation) * car.FrontAxle.leftWheelCollider.wheelModel.rotation;
            current.wheelRotationDegrees = Mathf.Max(current.wheelRotationDegrees, Quaternion.Angle(startWheelRotation, rotation));
            current.speedBeforeBrakingKmh = car.absoluteSpeed;
        }
        if (elapsed > 19.5f)
        {
            current.playingAudioSources = car.GetComponentsInChildren<AudioSource>(true).Count(a => a.isPlaying && a.clip != null);
            if (Camera.main != null) Camera.main.transform.position = car.transform.position + Vector3.one * 200f;
            current.physicsRemainsEnabledAtDistance = car.AeroDynamics.enabled && car.Stability.enabled;
            if (current.groundedWheels < 3) current.errors.Add("Fewer than three wheels contacted the ground after settling.");
            if (current.distanceMeters < 5f || current.maximumSpeedKmh < 10f) current.errors.Add("Car failed to accelerate through the RCCP drivetrain.");
            if (current.steeringDegrees < 1f || current.wheelRotationDegrees < 10f) current.errors.Add("Wheel steering/rotation did not respond.");
            if (current.speedAfterBrakingKmh > Mathf.Max(5f, current.speedBeforeBrakingKmh * 0.45f)) current.errors.Add("Braking did not sufficiently reduce speed.");
            if (!current.rearLightResponds) current.errors.Add("Rear brake light did not illuminate.");
            if (current.playingAudioSources == 0) current.errors.Add("No engine audio sources were playing.");
            if (!current.physicsRemainsEnabledAtDistance) current.errors.Add("Aero/stability was disabled by camera distance.");
            if (!current.reverseEngaged || current.reverseSpeedKmh > -2f || current.reverseDistanceMeters < 0.5f)
                current.errors.Add("Holding brake at standstill failed to engage and drive in reverse.");
            if (!current.forwardRecovered) current.errors.Add("Throttle failed to stop reverse motion and recover forward drive.");
            Complete();
        }
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false) || current == null || !EditorApplication.isPlaying) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        // This pre-existing SaveManager logs a missing profile as an Error even
        // when it successfully falls back to defaults. Preserve it separately.
        if (message.StartsWith("Save file not found. Using default data."))
        {
            if (!current.baselineMessages.Contains(message)) current.baselineMessages.Add(message);
        }
        else if (!current.errors.Contains(message)) current.errors.Add(message);
    }

    private static void Complete()
    {
        current.passed = current.errors.Count == 0;
        Report report = JsonUtility.FromJson<Report>(SessionState.GetString(ResultsKey, ""));
        report.results.Add(current);
        string json = JsonUtility.ToJson(report, true);
        SessionState.SetString(ResultsKey, json);
        File.WriteAllText(Folder + "/DrivingValidation.json", json);
        current = null;
        EditorApplication.ExitPlaymode();
    }

    private static void CaptureVehicle(RCCP_CarController vehicle, string path)
    {
        var cameraObject = new GameObject("Formula Validation Preview Camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        Vector3 target = vehicle.transform.position + Vector3.up * 0.45f;
        camera.transform.position = target + new Vector3(5.5f, 3.4f, 6f);
        camera.transform.LookAt(target);
        camera.orthographic = true;
        camera.orthographicSize = 3.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
        camera.nearClipPlane = 0.1f; camera.farClipPlane = 1000f;
        var targetTexture = new RenderTexture(1200, 750, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            targetTexture.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = targetTexture });
            RenderTexture.active = targetTexture;
            pixels = new Texture2D(1200, 750, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(targetTexture);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
