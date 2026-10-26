using UnityEngine;

namespace FormulaGame
{
    /// <summary>Formula automatic shift scheduling and clutch engagement using RCCP's drivetrain APIs.</summary>
    [DefaultExecutionOrder(-8), DisallowMultipleComponent, RequireComponent(typeof(RCCP_CarController))]
    public sealed class FormulaTransmission : MonoBehaviour
    {
        [SerializeField, Range(.12f, .35f)] private float shiftCooldown = .18f;
        [SerializeField, Range(.04f, .12f)] private float clutchEngagementSeconds = .06f;
        [SerializeField, Range(.12f, .4f)] private float launchEngagementSeconds = .22f;
        [SerializeField, Range(3f, 12f)] private float wheelCoupling = 8f;
        private RCCP_CarController car;
        private RCCP_Engine engine;
        private RCCP_Gearbox gearbox;
        private RCCP_Clutch clutch;
        private WheelCollider[] wheels;
        private RCCP_Axle[] axles;
        private float wheelRadius, nextShiftAt, previousCoupling, previousTimer, previousClutch;
        private bool ready, owns, wasShifting, previousClutchOverride;
        private int pendingGear = -1;
        private float shiftCompletesAt;
        public bool ControlActive => owns;
        public int AutomaticUpshifts { get; private set; }
        public int AutomaticDownshifts { get; private set; }
        public int GroundedWheels { get; private set; }
        public float LastRequestRpm { get; private set; }
        public float BodyCoupledRpm { get; private set; }
        public float OverspeedTorqueScale { get; private set; } = 1;
        public float CooldownRemaining => Mathf.Max(0, nextShiftAt - Time.fixedTime);

        private void Awake()
        {
            car = GetComponent<RCCP_CarController>();
            engine = GetComponentInChildren<RCCP_Engine>(true);
            gearbox = GetComponentInChildren<RCCP_Gearbox>(true);
            clutch = GetComponentInChildren<RCCP_Clutch>(true);
            wheels = GetComponentsInChildren<WheelCollider>(true);
            axles = GetComponentsInChildren<RCCP_Axle>(true);
        }
        private void Start()
        {
            if (engine == null || gearbox == null || clutch == null || wheels.Length == 0)
            { Debug.LogError("Formula transmission needs an RCCP engine, gearbox, clutch and wheels.", this); enabled = false; return; }
            wheelRadius = 0;
            foreach (var wheel in wheels) wheelRadius += wheel.radius;
            wheelRadius /= wheels.Length;
            ready = true;
        }
        private void FixedUpdate()
        {
            if (!ready) return;
            bool automatic = gearbox.transmissionType != RCCP_Gearbox.TransmissionType.Manual;
            // Public RCCP overrides belong to replay/test/external owners. Leave them intact.
            if (!automatic || gearbox.overrideGear || engine.overrideEngineRPM || !clutch.automaticClutch ||
                !gearbox.isActiveAndEnabled || !engine.isActiveAndEnabled || !clutch.isActiveAndEnabled ||
                !owns && clutch.overrideClutch)
            { Release(); return; }
            if (!owns) Acquire();
            float dt = Time.fixedDeltaTime;
            // Finish our automatic forward shifts on the physics clock, so a render
            // hitch cannot stretch RCCP's WaitForSeconds coroutine across extra steps.
            if (pendingGear >= 0 && Time.fixedTime >= shiftCompletesAt)
            {
                gearbox.currentGear = pendingGear;
                gearbox.currentGearState.gearState = RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
                gearbox.shiftingNow = false;
                pendingGear = -1;
            }
            if (wasShifting && !gearbox.shiftingNow) nextShiftAt = Time.fixedTime + shiftCooldown;
            wasShifting = gearbox.shiftingNow;

            // RCCP has private automatic decisions and a public timer gate. Hold only
            // that gate closed before its FixedUpdate; its neutral/reverse state handling,
            // shift coroutine, gearInput and torque output continue normally.
            gearbox.lastTimeShifted = Mathf.Max(nextShiftAt - Time.fixedTime, dt + .021f);
            GroundedWheels = 0;
            foreach (var wheel in wheels)
                if (wheel.enabled && wheel.gameObject.activeInHierarchy && wheel.GetGroundHit(out _)) GroundedWheels++;
            float forwardSpeed = Vector3.Dot(car.Rigid.linearVelocity, transform.forward);
            int gear = Mathf.Clamp(gearbox.currentGear, 0, gearbox.gearRatios.Length - 1);
            BodyCoupledRpm = Mathf.Max(0, forwardSpeed) * 60f / (2f * Mathf.PI * Mathf.Max(.05f, wheelRadius))
                * gearbox.gearRatios[gear] * car.GetFinalDriveRatio();
            bool forward = gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.InForwardGear;
            bool driveSelected = gearbox.transmissionType != RCCP_Gearbox.TransmissionType.Automatic_DNRP ||
                gearbox.automaticGearSelector == RCCP_Gearbox.SemiAutomaticDNRPGear.D;
            ProtectFromWheelOverspeed(forwardSpeed, forward && driveSelected, dt);
            if (!gearbox.shiftingNow && Time.fixedTime >= nextShiftAt && car.canControl && !car.Rigid.isKinematic &&
                engine.engineRunning && forward && driveSelected && !gearbox.forceToNGear && !gearbox.forceToRGear && forwardSpeed >= 0)
            {
                int target = SelectGear(gear, gearbox.gearRatios, engine.engineRPM, BodyCoupledRpm,
                    gearbox.shiftUpRPM, gearbox.shiftDownRPM, car.throttleInput_V, car.brakeInput_V, GroundedWheels);
                if (target != gear)
                {
                    LastRequestRpm = engine.engineRPM;
                    if (target > gear) AutomaticUpshifts++; else AutomaticDownshifts++;
                    pendingGear = target;
                    shiftCompletesAt = Time.fixedTime + gearbox.shiftingTime;
                    gearbox.shiftingNow = true;
                    wasShifting = true;
                    nextShiftAt = Time.fixedTime + gearbox.shiftingTime + shiftCooldown;
                }
            }
            bool disengaged = gearbox.shiftingNow || gearbox.forceToNGear || clutch.forceToPressClutch ||
                gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.Neutral ||
                gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.Park ||
                !car.canControl || car.Rigid.isKinematic ||
                clutch.pressClutchWhileHandbraking && car.handbrakeInput_V >= .75f;
            float speed = Mathf.Abs(forwardSpeed);
            float targetClutch = disengaged ? 1 : LaunchClutch(speed, car.throttleInput_V, engine.engineRPM, engine.minEngineRPM);
            float engagement = speed * 3.6f < 18 ? launchEngagementSeconds : clutchEngagementSeconds;
            float value = disengaged ? 1 : Mathf.MoveTowards(clutch.clutchInput, targetClutch, dt / engagement);
            clutch.OverrideInput(Mathf.Clamp01(value));
        }
        private void ProtectFromWheelOverspeed(float speed, bool forward, float dt)
        {
            // With TCS enabled, prevent a spinning driven axle from pinning the
            // engine at redline while road speed is still too low for the next gear.
            // This changes torque, never reported RPM, wheel velocity or tyre grip.
            if (!forward || GroundedWheels < 2 || car.Stability == null || !car.Stability.TCS ||
                gearbox.shiftingNow || car.throttleInput_V < .1f || car.brakeInput_V > .1f)
            { OverspeedTorqueScale = 1; return; }
            float wheelSpeed = 0; int count = 0;
            foreach (var axle in axles)
            {
                if (!axle.isPower || !axle.isActiveAndEnabled) continue;
                var left = axle.leftWheelCollider.WheelCollider; var right = axle.rightWheelCollider.WheelCollider;
                if (left.enabled && left.GetGroundHit(out _)) { wheelSpeed += Mathf.Abs(left.rpm) * (2f * Mathf.PI * left.radius) / 60; count++; }
                if (right.enabled && right.GetGroundHit(out _)) { wheelSpeed += Mathf.Abs(right.rpm) * (2f * Mathf.PI * right.radius) / 60; count++; }
            }
            if (count == 0) { OverspeedTorqueScale = 1; return; }
            wheelSpeed /= count;
            float target = WheelOverspeedTorqueScale(wheelSpeed, Mathf.Max(0, speed), engine.engineRPM, gearbox.shiftUpRPM);
            OverspeedTorqueScale = Mathf.MoveTowards(OverspeedTorqueScale, target, dt * (target < OverspeedTorqueScale ? 15 : 1.5f));
            engine.Multiply(OverspeedTorqueScale);
        }
        public static float WheelOverspeedTorqueScale(float wheelSpeed, float roadSpeed, float rpm, float shiftRpm)
        {
            float ratio = Mathf.Clamp01((Mathf.Max(0, roadSpeed) * 1.12f + 2) / Mathf.Max(2, wheelSpeed));
            float activation = Mathf.InverseLerp(shiftRpm - 3500, shiftRpm - 2200, rpm);
            return Mathf.Lerp(1, Mathf.Max(.04f, ratio * ratio * ratio * ratio * ratio * ratio), activation);
        }
        public static float LaunchClutch(float speedKinematicMetresPerSecond, float throttle, float rpm, float idle)
        {
            float speedKmh = Mathf.Abs(speedKinematicMetresPerSecond) * 3.6f;
            if (speedKmh < 2 && throttle < .05f) return 1;
            float slip = Mathf.Lerp(.55f, 0, Mathf.InverseLerp(0, 18, speedKmh));
            if (speedKmh < 10 && rpm < idle * .9f) slip = Mathf.Max(slip, .7f);
            return slip;
        }
        public static int SelectGear(int gear, float[] ratios, float rpm, float coupledRpm,
            float shiftUpRpm, float shiftDownRpm, float throttle, float brake, int groundedWheels)
        {
            if (groundedWheels < 2 || ratios == null || ratios.Length == 0 || gear < 0 || gear >= ratios.Length) return gear;
            float ratio = Mathf.Max(.01f, ratios[gear]);
            if (gear < ratios.Length - 1 && throttle > .12f && brake < .1f && rpm >= shiftUpRpm)
            {
                // A faster spinning airborne/slipping wheel is not a reason to run
                // through the entire box: retain enough actual road speed for the next gear.
                float predictedRoadRpm = coupledRpm * ratios[gear + 1] / ratio;
                if (predictedRoadRpm >= shiftDownRpm + 400) return gear + 1;
            }
            if (gear > 0 && (rpm <= shiftDownRpm || brake > .15f && rpm <= shiftDownRpm + 2000))
            {
                float predictedRpm = Mathf.Max(rpm, coupledRpm) * ratios[gear - 1] / ratio;
                if (predictedRpm < shiftUpRpm - 500) return gear - 1;
            }
            return gear;
        }
        private void Acquire()
        {
            previousCoupling = engine.engineCouplingToWheelsRate; previousTimer = gearbox.lastTimeShifted;
            previousClutch = clutch.clutchInput; previousClutchOverride = clutch.overrideClutch;
            engine.engineCouplingToWheelsRate = Mathf.Max(previousCoupling, wheelCoupling);
            wasShifting = gearbox.shiftingNow; nextShiftAt = Time.fixedTime; owns = true;
        }
        private void Release()
        {
            if (!owns) return;
            if (pendingGear >= 0) { pendingGear = -1; gearbox.shiftingNow = false; }
            engine.engineCouplingToWheelsRate = previousCoupling;
            gearbox.lastTimeShifted = previousTimer;
            clutch.OverrideInput(previousClutch); clutch.overrideClutch = previousClutchOverride;
            owns = false; wasShifting = false; OverspeedTorqueScale = 1;
        }
        private void OnDisable() => Release();
    }
}
