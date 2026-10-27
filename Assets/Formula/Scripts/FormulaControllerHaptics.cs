using UnityEngine;
using UnityEngine.InputSystem;

namespace FormulaGame
{
    // One scene owner drives only the player's active pad. Observes RCCP without
    // changing physics; 20 Hz telemetry/output, cached wheels and no render work.
    [DisallowMultipleComponent, RequireComponent(typeof(GamePlayManager))]
    public sealed class FormulaControllerHaptics : MonoBehaviour
    {
        private GamePlayManager game;
        private RCCP_CarController car;
        private RCCP_WheelCollider[] wheels;
        private FormulaContactAudio contacts;
        private float[] previousForce;
        private Gamepad pad;
        private Vector3 previousVelocity;
        private float nextSample, sampledAt, refreshAt, impact, shift, nextImpact;
        private int previousGear = -1;
        private bool sampled, finished, focused, suspended;
        public float LowFrequency { get; private set; }
        public float HighFrequency { get; private set; }
        public int OutputCommandCount { get; private set; }
        public int ImpactPulseCount { get; private set; }
        public RCCP_CarController Player => car;
        public float Intensity => Mathf.Clamp01(FormulaProgression.Data.vibrationIntensity);

        private void Awake() => game = GetComponent<GamePlayManager>();
        private void OnEnable()
        {
            focused = Application.isFocused; suspended = finished = false;
            FormulaSession.Finished += Finished;
            RCCP_Events.OnRCCPCollision += Collision;
            InputSystem.onDeviceChange += DeviceChanged;
        }

        private bool CanPlay => !finished && focused && !suspended && FormulaSession.InRace &&
            Time.timeScale > 0 && game != null && game.FormulaRaceStarted &&
            car != null && car.canControl && !car.externalControl && !car.Inputs.overrideExternalInputs &&
            FormulaProgression.Data.vibrationsState && Intensity > 0;

        private void Update()
        {
            var player = game != null ? game.CarController : null;
            if (car != player)
            {
                Stop(); car = player;
                wheels = car != null ? car.AllWheelColliders : null;
                previousForce = wheels != null ? new float[wheels.Length] : null;
                contacts = car != null ? car.GetComponent<FormulaContactAudio>() : null;
            }
            FormulaInputHints.Poll();
            var active = FormulaInputHints.Gamepad ? Gamepad.current : null;
            if (pad != active) { Stop(); pad = active; }
            if (pad == null || !pad.added || !pad.enabled || !CanPlay) { Stop(); return; }
            float now = Time.unscaledTime;
            if (now < nextSample) return;
            nextSample = now + .05f;
            float dt = sampled ? Mathf.Clamp(now - sampledAt, .001f, .15f) : .05f;
            var body = car.Rigid;
            Vector3 velocity = body.linearVelocity;
            Vector3 acceleration = sampled ? car.transform.InverseTransformDirection((velocity - previousVelocity) / dt) : Vector3.zero;
            previousVelocity = velocity; sampledAt = now;
            float speed = velocity.magnitude;
            float slip = 0, bump = 0;
            int grounded = 0;
            float expectedLoad = body.mass * 9.81f / Mathf.Max(1, wheels == null ? 0 : wheels.Length);
            if (wheels != null)
                for (int i = 0; i < wheels.Length; i++)
                {
                    var wheel = wheels[i];
                    if (wheel == null || !wheel.enabled || !wheel.WheelCollider.enabled || !wheel.WheelCollider.GetGroundHit(out WheelHit hit)) { previousForce[i] = 0; continue; }
                    grounded++;
                    slip = Mathf.Max(slip, Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip)));
                    if (sampled) bump = Mathf.Max(bump, Mathf.InverseLerp(.7f, 3, (hit.force - previousForce[i]) / expectedLoad));
                    previousForce[i] = hit.force;
                }
            int gear = car.Gearbox != null ? car.Gearbox.currentGear : -1;
            if (sampled && previousGear >= 0 && gear != previousGear) shift = .25f;
            previousGear = gear; sampled = true;
            float moving = Mathf.InverseLerp(.5f, 8, speed);
            float intensity = Intensity;
            float drive = Mathf.Clamp01(acceleration.z / 8) * .1f + Mathf.Clamp01(car.throttleInput_V) * .04f;
            float braking = Mathf.Clamp01(car.brakeInput_V) * Mathf.InverseLerp(0, 12, -acceleration.z) * .18f;
            float corner = Mathf.InverseLerp(2, 12, Mathf.Abs(acceleration.x)) * .07f;
            float tyres = Mathf.InverseLerp(.08f, .6f, slip) * .35f;
            float surface = contacts != null && (contacts.LastSurface == FormulaAudioSurfaceKind.Kerb || contacts.LastSurface == FormulaAudioSurfaceKind.Gravel || contacts.LastSurface == FormulaAudioSurfaceKind.Grass) ? .14f : 0;
            float low = grounded > 0 ? moving * (drive + braking + corner + bump * .22f) : 0;
            float high = grounded > 0 ? moving * (tyres + surface + bump * .18f) : 0;
            low = Mathf.Clamp01((low + impact * .8f + shift * .45f) * intensity);
            high = Mathf.Clamp01((high + impact + shift) * intensity);
            // Smoothing applies only during play. Safety stops below are immediate.
            low = Mathf.MoveTowards(LowFrequency, Mathf.Min(.65f, low), dt * 5);
            high = Mathf.MoveTowards(HighFrequency, Mathf.Min(.75f, high), dt * 7);
            impact = Mathf.MoveTowards(impact, 0, dt * 4);
            shift = Mathf.MoveTowards(shift, 0, dt * 3.5f);
            Send(low < .005f ? 0 : low, high < .005f ? 0 : high, now);
        }

        private void Collision(RCCP_CarController source, UnityEngine.Collision collision)
        {
            if (source != car || !CanPlay || pad == null || collision == null || Time.unscaledTime < nextImpact) return;
            if (collision.contactCount > 0 && Mathf.Abs(Vector3.Dot(collision.GetContact(0).normal, car.transform.up)) > .7f) return;
            float severity = Mathf.InverseLerp(.7f, 7, collision.impulse.magnitude / Mathf.Max(1, car.Rigid.mass));
            if (severity <= 0) return;
            impact = Mathf.Max(impact, Mathf.Lerp(.15f, 1, severity));
            nextImpact = Time.unscaledTime + .12f; ImpactPulseCount++;
        }

        private void Send(float low, float high, float now)
        {
            if (Mathf.Abs(low - LowFrequency) < .005f && Mathf.Abs(high - HighFrequency) < .005f && now < refreshAt) return;
            pad.SetMotorSpeeds(low, high);
            LowFrequency = low; HighFrequency = high; refreshAt = now + .5f; OutputCommandCount++;
        }

        private void Stop()
        {
            if (pad != null && pad.added && (LowFrequency > 0 || HighFrequency > 0))
            {
                pad.SetMotorSpeeds(0, 0); OutputCommandCount++;
            }
            LowFrequency = HighFrequency = impact = shift = 0;
            sampled = false; previousGear = -1; nextSample = refreshAt = 0;
        }
        private void Finished(FormulaReceipt receipt) { finished = true; Stop(); }
        private void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (device == pad && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled)) { Stop(); pad = null; }
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) Stop(); }
        private void OnApplicationPause(bool value) { suspended = value; if (value) Stop(); }
        private void OnApplicationQuit() => Stop();
        private void OnDisable()
        {
            FormulaSession.Finished -= Finished;
            RCCP_Events.OnRCCPCollision -= Collision;
            InputSystem.onDeviceChange -= DeviceChanged;
            Stop(); pad = null;
        }
    }
}
