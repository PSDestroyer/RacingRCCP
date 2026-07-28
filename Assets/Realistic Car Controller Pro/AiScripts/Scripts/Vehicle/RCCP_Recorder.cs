//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Record / Replay system for saving and playing back vehicle movement and input data.
/// Allows capturing a sequence of frames (inputs, transforms, velocities) and then replaying them.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Other Addons/RCCP Recorder")]
public class RCCP_Recorder : RCCP_Component {

    /// <summary>
    /// A recorded clip containing inputs, transforms, and velocities across multiple frames.
    /// </summary>
    [System.Serializable]
    public class RecordedClip {

        public string recordName = "New Record";

        /// <summary>
        /// All recorded inputs (throttle, brake, steer, etc.) for each frame.
        /// </summary>
        [HideInInspector] public VehicleInput[] inputs;

        /// <summary>
        /// All recorded positions/rotations for each frame.
        /// </summary>
        [HideInInspector] public VehicleTransform[] transforms;

        /// <summary>
        /// All recorded velocities/rotations for each frame.
        /// </summary>
        [HideInInspector] public VehicleVelocity[] rigids;

        public RecordedClip(VehicleInput[] _inputs, VehicleTransform[] _transforms, VehicleVelocity[] _rigids, string _recordName) {

            inputs = _inputs;
            transforms = _transforms;
            rigids = _rigids;
            recordName = _recordName;

        }

        public RecordedClip() { }

    }

    /// <summary>
    /// Holds the most recently saved or loaded recorded clip.
    /// </summary>
    public RecordedClip recorded;

    [Header("Editor Export")]
    public bool saveAsAnimationClip = false;
    public AnimationClip animationClip;

    private List<VehicleInput> Inputs;
    private List<VehicleTransform> Transforms;
    private List<VehicleVelocity> Rigidbodies;

    /// <summary>
    /// Stores a single frame of vehicle input data.
    /// </summary>
    [System.Serializable]
    public class VehicleInput {

        public float throttleInput;
        public float brakeInput;
        public float steerInput;
        public float handbrakeInput;
        public float clutchInput;
        public float nosInput;
        public int direction;
        public int currentGear;
        public float gearInput = 1f;
        public RCCP_Gearbox.CurrentGearState.GearState gearState;
        public bool NGear;

        public bool lowBeamHeadLightsOn;
        public bool highBeamHeadLightsOn;
        public bool indicatorsLeft;
        public bool indicatorsRight;
        public bool indicatorsAll;

        public VehicleInput(
            float _gasInput,
            float _brakeInput,
            float _steerInput,
            float _handbrakeInput,
            float _clutchInput,
            float _boostInput,
            int _direction,
            int _currentGear,
            RCCP_Gearbox.CurrentGearState.GearState _gearState,
            bool _NGear,
            bool _lowBeamHeadLightsOn,
            bool _highBeamHeadLightsOn,
            bool _indicatorsLeft,
            bool _indicatorsRight,
            bool _indicatorsAll
        ) {

            throttleInput = _gasInput;
            brakeInput = _brakeInput;
            steerInput = _steerInput;
            handbrakeInput = _handbrakeInput;
            clutchInput = _clutchInput;
            nosInput = _boostInput;
            direction = _direction;
            currentGear = _currentGear;
            gearState = _gearState;
            NGear = _NGear;

            lowBeamHeadLightsOn = _lowBeamHeadLightsOn;
            highBeamHeadLightsOn = _highBeamHeadLightsOn;
            indicatorsLeft = _indicatorsLeft;
            indicatorsRight = _indicatorsRight;
            indicatorsAll = _indicatorsAll;

        }

    }

    /// <summary>
    /// Records the vehicle’s position and rotation for a single frame.
    /// </summary>
    [System.Serializable]
    public class VehicleTransform {

        public Vector3 position;
        public Quaternion rotation;

        public VehicleTransform(Vector3 _pos, Quaternion _rot) {

            position = _pos;
            rotation = _rot;

        }

    }

    /// <summary>
    /// Records the vehicle’s velocity and angular velocity for a single frame.
    /// </summary>
    [System.Serializable]
    public class VehicleVelocity {

        public Vector3 velocity;
        public Vector3 angularVelocity;

        public VehicleVelocity(Vector3 _vel, Vector3 _angVel) {

            velocity = _vel;
            angularVelocity = _angVel;

        }

    }

    /// <summary>
    /// Operational modes for the recorder: neutral, recording, or playing back.
    /// </summary>
    public enum RecorderMode { Neutral, Play, Record }
    public RecorderMode mode = RecorderMode.Neutral;

    public override void Start() {

        base.Start();

        Inputs = new List<VehicleInput>();
        Transforms = new List<VehicleTransform>();
        Rigidbodies = new List<VehicleVelocity>();

    }

    /// <summary>
    /// Begin or stop recording the current vehicle’s movements and inputs.
    /// </summary>
    public void Record() {

        // Toggle between entering record mode or stopping and saving the record.
        if (mode != RecorderMode.Record) {

            mode = RecorderMode.Record;

        } else {

            mode = RecorderMode.Neutral;
            SaveRecord();

        }

        // If we’re entering record mode, clear existing data from prior recordings.
        if (mode == RecorderMode.Record) {

            Inputs.Clear();
            Transforms.Clear();
            Rigidbodies.Clear();

        }

    }

    /// <summary>
    /// Saves the current recorded data to the 'recorded' struct and appends it to RCCP_Records.
    /// </summary>
    public void SaveRecord() {

        Debug.Log("Record saved!");

        recorded = new RecordedClip(
            Inputs.ToArray(),
            Transforms.ToArray(),
            Rigidbodies.ToArray(),
            RCCP_Records.Instance.records.Count + "_" + CarController.transform.name
        );

        RCCP_Records.Instance.records.Add(recorded);

#if UNITY_EDITOR
        if (RCCP_Records.Instance)
            EditorUtility.SetDirty(RCCP_Records.Instance);

        SaveAsAnimationClipIfNeeded();
        AssetDatabase.SaveAssets();
#endif

    }

#if UNITY_EDITOR
    private void SaveAsAnimationClipIfNeeded() {

        if (!saveAsAnimationClip || animationClip == null || recorded == null || recorded.transforms == null || recorded.transforms.Length == 0)
            return;

        RCCP_TimelinePlayback timelinePlayback = CarController.GetComponent<RCCP_TimelinePlayback>();

        if (!timelinePlayback)
            timelinePlayback = CarController.gameObject.AddComponent<RCCP_TimelinePlayback>();

        animationClip.ClearCurves();
        animationClip.frameRate = Mathf.Approximately(Time.fixedDeltaTime, 0f) ? 60f : (1f / Time.fixedDeltaTime);

        AnimationCurve posX = new AnimationCurve();
        AnimationCurve posY = new AnimationCurve();
        AnimationCurve posZ = new AnimationCurve();
        AnimationCurve rotX = new AnimationCurve();
        AnimationCurve rotY = new AnimationCurve();
        AnimationCurve rotZ = new AnimationCurve();
        AnimationCurve rotW = new AnimationCurve();
        AnimationCurve throttle = new AnimationCurve();
        AnimationCurve brake = new AnimationCurve();
        AnimationCurve steer = new AnimationCurve();
        AnimationCurve handbrake = new AnimationCurve();
        AnimationCurve clutch = new AnimationCurve();
        AnimationCurve nos = new AnimationCurve();
        AnimationCurve direction = new AnimationCurve();
        AnimationCurve currentGear = new AnimationCurve();
        AnimationCurve gearInput = new AnimationCurve();
        AnimationCurve gearState = new AnimationCurve();
        AnimationCurve neutralGear = new AnimationCurve();
        AnimationCurve lowBeam = new AnimationCurve();
        AnimationCurve highBeam = new AnimationCurve();
        AnimationCurve indicatorsLeft = new AnimationCurve();
        AnimationCurve indicatorsRight = new AnimationCurve();
        AnimationCurve indicatorsAll = new AnimationCurve();
        AnimationCurve linearVelocityX = new AnimationCurve();
        AnimationCurve linearVelocityY = new AnimationCurve();
        AnimationCurve linearVelocityZ = new AnimationCurve();
        AnimationCurve angularVelocityX = new AnimationCurve();
        AnimationCurve angularVelocityY = new AnimationCurve();
        AnimationCurve angularVelocityZ = new AnimationCurve();

        Transform targetTransform = CarController.transform;
        Transform parent = targetTransform.parent;

        for (int i = 0; i < recorded.transforms.Length; i++) {

            float time = i * Time.fixedDeltaTime;
            Vector3 position = recorded.transforms[i].position;
            Quaternion rotation = recorded.transforms[i].rotation;

            if (parent) {
                position = parent.InverseTransformPoint(position);
                rotation = Quaternion.Inverse(parent.rotation) * rotation;
            }

            posX.AddKey(time, position.x);
            posY.AddKey(time, position.y);
            posZ.AddKey(time, position.z);
            rotX.AddKey(time, rotation.x);
            rotY.AddKey(time, rotation.y);
            rotZ.AddKey(time, rotation.z);
            rotW.AddKey(time, rotation.w);

            if (recorded.inputs != null && i < recorded.inputs.Length) {

                VehicleInput input = recorded.inputs[i];

                throttle.AddKey(time, input.throttleInput);
                brake.AddKey(time, input.brakeInput);
                steer.AddKey(time, input.steerInput);
                handbrake.AddKey(time, input.handbrakeInput);
                clutch.AddKey(time, input.clutchInput);
                nos.AddKey(time, input.nosInput);
                direction.AddKey(time, input.direction);
                currentGear.AddKey(time, input.currentGear);
                gearInput.AddKey(time, input.gearInput);
                gearState.AddKey(time, (float)input.gearState);
                neutralGear.AddKey(time, input.NGear ? 1f : 0f);
                lowBeam.AddKey(time, input.lowBeamHeadLightsOn ? 1f : 0f);
                highBeam.AddKey(time, input.highBeamHeadLightsOn ? 1f : 0f);
                indicatorsLeft.AddKey(time, input.indicatorsLeft ? 1f : 0f);
                indicatorsRight.AddKey(time, input.indicatorsRight ? 1f : 0f);
                indicatorsAll.AddKey(time, input.indicatorsAll ? 1f : 0f);

            }

            if (recorded.rigids != null && i < recorded.rigids.Length) {

                VehicleVelocity rigid = recorded.rigids[i];

                linearVelocityX.AddKey(time, rigid.velocity.x);
                linearVelocityY.AddKey(time, rigid.velocity.y);
                linearVelocityZ.AddKey(time, rigid.velocity.z);
                angularVelocityX.AddKey(time, rigid.angularVelocity.x);
                angularVelocityY.AddKey(time, rigid.angularVelocity.y);
                angularVelocityZ.AddKey(time, rigid.angularVelocity.z);

            }

        }

        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.x"), posX);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.y"), posY);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.z"), posZ);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalRotation.x"), rotX);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalRotation.y"), rotY);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalRotation.z"), rotZ);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalRotation.w"), rotW);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "throttleInput"), throttle);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "brakeInput"), brake);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "steerInput"), steer);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "handbrakeInput"), handbrake);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "clutchInput"), clutch);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "nosInput"), nos);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "direction"), direction);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "currentGear"), currentGear);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "gearInput"), gearInput);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "gearState"), gearState);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "neutralGear"), neutralGear);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "lowBeamHeadLightsOn"), lowBeam);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "highBeamHeadLightsOn"), highBeam);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "indicatorsLeft"), indicatorsLeft);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "indicatorsRight"), indicatorsRight);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "indicatorsAll"), indicatorsAll);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "linearVelocity.x"), linearVelocityX);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "linearVelocity.y"), linearVelocityY);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "linearVelocity.z"), linearVelocityZ);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "angularVelocity.x"), angularVelocityX);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "angularVelocity.y"), angularVelocityY);
        AnimationUtility.SetEditorCurve(animationClip, EditorCurveBinding.FloatCurve(string.Empty, typeof(RCCP_TimelinePlayback), "angularVelocity.z"), angularVelocityZ);

        animationClip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(animationClip);
        EditorUtility.SetDirty(timelinePlayback);

    }
#endif

    /// <summary>
    /// Toggle playback of the last recorded clip or stop if already playing.
    /// </summary>
    public void Play() {

        if (recorded == null)
            return;

        Play(recorded, 0f);

    }

    /// <summary>
    /// Plays back a specified clip instead of the last recorded one.
    /// </summary>
    /// <param name="_recorded">The recorded clip to play.</param>
    public void Play(RecordedClip _recorded) {

        Play(_recorded, 0f);

    }

    /// <summary>
    /// Plays back a specified clip starting from the given time offset in seconds.
    /// </summary>
    /// <param name="_recorded">The recorded clip to play.</param>
    /// <param name="startTime">Playback start offset in seconds.</param>
    public void Play(RecordedClip _recorded, float startTime) {

        recorded = _recorded;

        Debug.Log("Replaying record " + recorded.recordName);

        if (recorded == null)
            return;

        int startFrame = GetPlaybackStartFrame(startTime);

        if (mode != RecorderMode.Play)
            mode = RecorderMode.Play;
        else
            mode = RecorderMode.Neutral;

        if (mode == RecorderMode.Play) {

            OverrideVehicle(true);
            StartCoroutine(Replay(startFrame));

            if (recorded.transforms.Length > startFrame)
                CarController.transform.SetPositionAndRotation(recorded.transforms[startFrame].position, recorded.transforms[startFrame].rotation);

            StartCoroutine(Revel(startFrame));

        } else {

            OverrideVehicle(false);

        }

    }

    private int GetPlaybackStartFrame(float startTime) {

        if (recorded == null || recorded.transforms == null || recorded.transforms.Length == 0)
            return 0;

        float safeStartTime = Mathf.Max(0f, startTime);
        int maxFrame = Mathf.Max(recorded.transforms.Length - 1, 0);
        int frame = Mathf.RoundToInt(safeStartTime / Mathf.Max(Time.fixedDeltaTime, .0001f));

        return Mathf.Clamp(frame, 0, maxFrame);

    }

    /// <summary>
    /// Stops playback or recording, returning to neutral mode.
    /// </summary>
    public void Stop() {

        mode = RecorderMode.Neutral;
        OverrideVehicle(false);

    }

    private IEnumerator Replay(int startFrame) {

        for (int i = startFrame; i < recorded.inputs.Length && mode == RecorderMode.Play; i++) {

            OverrideVehicle(true);

            RCCP_Inputs inputs = new RCCP_Inputs {
                throttleInput = recorded.inputs[i].throttleInput,
                brakeInput = recorded.inputs[i].brakeInput,
                steerInput = recorded.inputs[i].steerInput,
                handbrakeInput = recorded.inputs[i].handbrakeInput,
                clutchInput = recorded.inputs[i].clutchInput,
                nosInput = recorded.inputs[i].nosInput
            };

            if (CarController.Inputs)
                CarController.Inputs.OverrideInputs(inputs);

            if (CarController.Gearbox)
                CarController.Gearbox.OverrideGear(recorded.inputs[i].currentGear, recorded.inputs[i].gearInput, recorded.inputs[i].gearState);

            if (CarController.Lights) {

                CarController.Lights.lowBeamHeadlights = recorded.inputs[i].lowBeamHeadLightsOn;
                CarController.Lights.highBeamHeadlights = recorded.inputs[i].highBeamHeadLightsOn;
                CarController.Lights.indicatorsLeft = recorded.inputs[i].indicatorsLeft;
                CarController.Lights.indicatorsRight = recorded.inputs[i].indicatorsRight;
                CarController.Lights.indicatorsAll = recorded.inputs[i].indicatorsAll;

            }

            yield return new WaitForFixedUpdate();

        }

        mode = RecorderMode.Neutral;
        OverrideVehicle(false);

    }

    /// <summary>
    /// Applies the recorded velocities to the vehicle’s Rigidbody each physics frame.
    /// </summary>
    /// <returns></returns>
    private IEnumerator Revel(int startFrame) {

        for (int i = startFrame; i < recorded.rigids.Length && mode == RecorderMode.Play; i++) {

            CarController.Rigid.linearVelocity = recorded.rigids[i].velocity;
            CarController.Rigid.angularVelocity = recorded.rigids[i].angularVelocity;

            yield return new WaitForFixedUpdate();

        }

        mode = RecorderMode.Neutral;
        OverrideVehicle(false);

    }

    private void FixedUpdate() {

        switch (mode) {

            case RecorderMode.Neutral:
                break;

            case RecorderMode.Play:
                // Continuously override vehicle while playing.
                OverrideVehicle(true);
                break;

            case RecorderMode.Record:

                Inputs.Add(new VehicleInput(
                    CarController.throttleInput_V,
                    CarController.brakeInput_V,
                    CarController.steerInput_V,
                    CarController.handbrakeInput_V,
                    CarController.clutchInput_V,
                    CarController.nosInput_V,
                    CarController.direction,
                    CarController.currentGear,
                    CarController.Gearbox.currentGearState.gearState,
                    CarController.NGearNow,
                    CarController.lowBeamLights,
                    CarController.highBeamLights,
                    CarController.indicatorsLeftLights,
                    CarController.indicatorsRightLights,
                    CarController.indicatorsAllLights
                ));

                Transforms.Add(new VehicleTransform(
                    CarController.transform.position,
                    CarController.transform.rotation
                ));

                Rigidbodies.Add(new VehicleVelocity(
                    CarController.Rigid.linearVelocity,
                    CarController.Rigid.angularVelocity
                ));

                break;

        }

    }

    /// <summary>
    /// Overrides the vehicle’s input and gear logic with the record/playback system, disabling player input.
    /// </summary>
    /// <param name="overrideState">True to override with recorded data, false to return to normal control.</param>
    private void OverrideVehicle(bool overrideState) {

        if (CarController.Inputs) {
            CarController.Inputs.overridePlayerInputs = overrideState;
            CarController.Inputs.overrideExternalInputs = overrideState;
        }

        if (CarController.Gearbox)
            CarController.Gearbox.overrideGear = overrideState;

    }

    /// <summary>
    /// Resets the recorder to a neutral state, stopping any record or playback in progress.
    /// </summary>
    public void Reload() {

        mode = RecorderMode.Neutral;

    }

    private void Reset() {

        if (recorded == null)
            recorded = new RecordedClip();

        if (recorded != null && recorded.recordName == "New Record") {

            RCCP_CarController carController = transform.GetComponentInParent<RCCP_CarController>(true);

            if (carController != null)
                recorded.recordName = carController.transform.name;

        }

    }

}
