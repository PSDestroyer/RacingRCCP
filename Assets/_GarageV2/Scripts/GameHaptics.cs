using UnityEngine;
using UnityEngine.InputSystem;

public static class GameHaptics
{
    private sealed class Driver : MonoBehaviour
    {
        private float pulseLow;
        private float pulseHigh;
        private float pulseUntil;
        private float continuousLow;
        private float continuousHigh;
        private float continuousUntil;

        private void Update()
        {
            if (Time.unscaledTime >= continuousUntil)
            {
                continuousLow = 0f;
                continuousHigh = 0f;
            }

            if (Time.unscaledTime >= pulseUntil)
            {
                pulseLow = 0f;
                pulseHigh = 0f;
            }

            Apply(Mathf.Max(continuousLow, pulseLow), Mathf.Max(continuousHigh, pulseHigh));
        }

        public void Pulse(float low, float high, float duration)
        {
            pulseLow = Mathf.Clamp01(low);
            pulseHigh = Mathf.Clamp01(high);
            pulseUntil = Time.unscaledTime + Mathf.Max(.02f, duration);
        }

        public void Continuous(float low, float high)
        {
            continuousLow = Mathf.Clamp01(low);
            continuousHigh = Mathf.Clamp01(high);
            continuousUntil = Time.unscaledTime + 0.15f;
        }

        private static void Apply(float low, float high)
        {
            if (Gamepad.current != null)
                Gamepad.current.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
        }

        private void OnDisable()
        {
            if (Gamepad.current != null)
                Gamepad.current.ResetHaptics();
        }
    }

    private static Driver driver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (driver != null)
            return;

        GameObject instance = new GameObject(nameof(GameHaptics));
        Object.DontDestroyOnLoad(instance);
        driver = instance.AddComponent<Driver>();
    }

    private static Driver Instance
    {
        get
        {
            if (driver == null)
                Initialize();
            return driver;
        }
    }

    public static void Pulse(float low, float high, float duration) => Instance.Pulse(low, high, duration);
    public static void Continuous(float low, float high) => Instance.Continuous(low, high);

    public static void GearShift() => Pulse(0.12f, 0.22f, 0.08f);
    public static void StartTick() => Pulse(0.18f, 0.32f, 0.11f);
    public static void StartGo() => Pulse(0.42f, 0.7f, 0.25f);
    public static void CheckpointMissed() => Pulse(0.7f, 0.9f, 0.3f);
    public static void RespawnTick() => Pulse(0.25f, 0.45f, 0.13f);
    public static void Respawn() => Pulse(0.6f, 0.85f, 0.32f);
    public static void Eliminated() => Pulse(0.85f, 1f, 0.55f);
    public static void Victory() => Pulse(0.45f, 0.8f, 0.5f);
    public static void Defeat() => Pulse(0.65f, 0.85f, 0.45f);
    public static void Success() => Pulse(0.2f, 0.45f, 0.18f);
    public static void Error() => Pulse(0.55f, 0.3f, 0.12f);
}
