#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FormulaGame;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class FormulaTransmissionProbe : MonoBehaviour
{
    [Serializable] public sealed class Sample
    {
        public float time, speed, rpm, wheelSpeed, clutch, cooldown, coupledRpm, overspeedTorqueScale;
        public int gear, grounded;
        public bool shifting;
        public string phase;
    }
    [Serializable] public sealed class Shift
    {
        public int from, to;
        public float time, requestRpm, speed, duration, clutchEngagement;
    }
    [Serializable] public sealed class Metrics
    {
        public string profile;
        public float peakSpeed, maxShiftSeconds, maxClutchEngagementSeconds;
        public int[] accelerationGears, brakingGears;
        public float[] longestLimiterSeconds = new float[8];
        public float[] longestNearRedlineEpisode = new float[8];
        public List<Shift> shifts = new List<Shift>();
        public List<Sample> samples = new List<Sample>();
    }
    public string Phase { get; set; } = "settle";
    public Metrics Result { get; private set; }
    private RCCP_CarController car;
    private FormulaTransmission transmission;
    private WheelCollider[] wheels;
    private readonly HashSet<int> accelerationGears = new HashSet<int>(), brakingGears = new HashSet<int>();
    private bool recording, wasShifting;
    private float began, shiftBegan, requestRpm, limiterTime, clutchBegan = -1;
    private int lastGear;
    private Shift currentShift;
    private float episodeStart = -1, lastNearRedline = -100;
    public void Begin(string profile)
    {
        car = GetComponent<RCCP_CarController>(); transmission = GetComponent<FormulaTransmission>();
        wheels = GetComponentsInChildren<WheelCollider>();
        Result = new Metrics { profile = profile }; began = Time.fixedTime;
        lastGear = car.Gearbox.currentGear; wasShifting = car.Gearbox.shiftingNow; recording = true;
    }
    private void FixedUpdate()
    {
        if (!recording || car == null) return;
        var box = car.Gearbox; var engine = car.Engine;
        int grounded = 0;
        foreach (var wheel in wheels) if (wheel.enabled && wheel.GetGroundHit(out _)) grounded++;
        float speed = Vector3.Dot(car.Rigid.linearVelocity, transform.forward) * 3.6f;
        int gear = box.currentGear;
        if (Phase == "accelerate") accelerationGears.Add(gear + 1);
        if (Phase == "brake") brakingGears.Add(gear + 1);
        Result.peakSpeed = Mathf.Max(Result.peakSpeed, speed);
        if (box.shiftingNow && !wasShifting) { shiftBegan = Time.fixedTime; requestRpm = engine.engineRPM; }
        if (!box.shiftingNow && wasShifting)
            Result.maxShiftSeconds = Mathf.Max(Result.maxShiftSeconds, Time.fixedTime - shiftBegan);
        if (gear != lastGear)
        {
            currentShift = new Shift { from = lastGear + 1, to = gear + 1, time = Time.fixedTime - began,
                requestRpm = requestRpm, speed = speed, duration = Time.fixedTime - shiftBegan };
            Result.shifts.Add(currentShift); clutchBegan = Time.fixedTime; limiterTime = 0;
            episodeStart = -1; lastNearRedline = -100;
        }
        if (clutchBegan >= 0 && car.Clutch.clutchInput <= .02f)
        {
            float duration = Time.fixedTime - clutchBegan;
            Result.maxClutchEngagementSeconds = Mathf.Max(Result.maxClutchEngagementSeconds, duration);
            if (currentShift != null) currentShift.clutchEngagement = duration;
            clutchBegan = -1;
        }
        bool limited = Phase == "accelerate" && !box.shiftingNow && grounded >= 2 && speed > 10 && engine.engineRPM >= engine.maxEngineRPM - 100;
        limiterTime = limited ? limiterTime + Time.fixedDeltaTime : 0;
        if (gear >= 0 && gear < 8) Result.longestLimiterSeconds[gear] = Mathf.Max(Result.longestLimiterSeconds[gear], limiterTime);
        bool nearRedline = Phase == "accelerate" && !box.shiftingNow && grounded >= 2 && speed > 10 && engine.engineRPM >= engine.maxEngineRPM - 200;
        if (nearRedline)
        {
            if (episodeStart < 0) episodeStart = Time.fixedTime;
            lastNearRedline = Time.fixedTime;
            if (gear >= 0 && gear < 8) Result.longestNearRedlineEpisode[gear] = Mathf.Max(Result.longestNearRedlineEpisode[gear], Time.fixedTime - episodeStart + Time.fixedDeltaTime);
        }
        else if (Time.fixedTime - lastNearRedline > .1f || box.shiftingNow) episodeStart = -1;
        Result.samples.Add(new Sample { time = Time.fixedTime - began, speed = speed, rpm = engine.engineRPM,
            wheelSpeed = car.wheelRPM2Speed, clutch = car.Clutch.clutchInput,
            cooldown = transmission != null && transmission.ControlActive ? transmission.CooldownRemaining : box.lastTimeShifted,
            coupledRpm = transmission != null ? transmission.BodyCoupledRpm : 0,
            overspeedTorqueScale = transmission != null ? transmission.OverspeedTorqueScale : 1,
            gear = gear + 1, grounded = grounded, shifting = box.shiftingNow, phase = Phase });
        wasShifting = box.shiftingNow; lastGear = gear;
    }
    public Metrics Finish()
    {
        recording = false;
        Result.accelerationGears = new List<int>(accelerationGears).ToArray(); Array.Sort(Result.accelerationGears);
        Result.brakingGears = new List<int>(brakingGears).ToArray(); Array.Sort(Result.brakingGears);
        return Result;
    }
}
#endif
