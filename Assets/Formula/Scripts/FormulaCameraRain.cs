using UnityEngine;

namespace FormulaGame
{
    // Scene-owned cosmetic telemetry; never writes vehicle physics. All beads
    // share one small UI mesh below the HUD, without a scene-color copy or blur.
    [DisallowMultipleComponent]
    public sealed class FormulaCameraRain : MonoBehaviour
    {
        [SerializeField] private Material rainMaterial;
        [SerializeField, Range(0, 1)] private float intensity = .72f;
        private Canvas canvas;
        private FormulaLensDroplets droplets;
        private GamePlayManager game;
        private Rigidbody body;
        private Vector3 previousVelocity, acceleration;
        private float speed;
        private bool finished;
        public Canvas RainCanvas => canvas;
        public FormulaLensDroplets Droplets => droplets;
        public Rigidbody TelemetrySource => body;
        public Vector3 WorldAcceleration => acceleration;
        public Vector3 LensInertia { get; private set; }
        public float SpeedMetersPerSecond => speed;
        public float Intensity { get => intensity; set => intensity = Mathf.Clamp01(value); }

        private void OnEnable()
        {
            finished = false;
            game = GetComponent<GamePlayManager>();
            FormulaSession.Finished += Finished;
            if (rainMaterial == null || !rainMaterial.HasProperty("_MainTex")) return;
            var root = new GameObject("Camera Rain Lite", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            canvas.enabled = false;
            var drops = new GameObject("Lens Droplets", typeof(RectTransform), typeof(FormulaLensDroplets));
            drops.transform.SetParent(root.transform, false);
            var rect = drops.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            droplets = drops.GetComponent<FormulaLensDroplets>();
            droplets.material = rainMaterial;
            droplets.raycastTarget = false;
            droplets.color = new Color(1, 1, 1, intensity);
            droplets.Initialize();
        }

        private void FixedUpdate()
        {
            var player = game != null ? game.CarController : null;
            var current = player != null ? player.Rigid : null;
            if (body != current)
            {
                body = current; acceleration = Vector3.zero;
                previousVelocity = body != null ? body.linearVelocity : Vector3.zero;
            }
            if (body == null) { speed = 0; return; }
            Vector3 velocity = body.linearVelocity;
            Vector3 measured = (velocity - previousVelocity) / Time.fixedDeltaTime;
            previousVelocity = velocity;
            // Bound impact/reset spikes and filter suspension/physics jitter.
            acceleration = Vector3.Lerp(acceleration, Vector3.ClampMagnitude(measured, 30), 1 - Mathf.Exp(-Time.fixedDeltaTime / .18f));
            speed = velocity.magnitude;
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            bool visible = FormulaSession.InRace && !finished && intensity > 0;
            if (canvas.enabled != visible) canvas.enabled = visible;
            if (droplets.color.a != intensity) droplets.color = new Color(1, 1, 1, intensity);
            if (!visible || Time.deltaTime <= 0) return;
            var scene = RCCP_SceneManager.Instance;
            var camera = scene != null && scene.activePlayerCamera != null ? scene.activePlayerCamera.actualCamera : null;
            var view = camera != null ? camera.transform : body != null ? body.transform : transform;
            Vector3 gravity = view.InverseTransformDirection(Vector3.down);
            LensInertia = view.InverseTransformDirection(-acceleration) / 9.81f;
            droplets.Simulate(Mathf.Min(Time.deltaTime, .05f), new Vector2(gravity.x, gravity.y), LensInertia, Mathf.Clamp01(speed / 55f));
        }

        private void Finished(FormulaReceipt receipt) { finished = true; }

        private void OnDisable()
        {
            FormulaSession.Finished -= Finished;
            // Disable immediately; Destroy is deferred until the end of frame.
            if (canvas != null) { canvas.enabled = false; Destroy(canvas.gameObject); }
            canvas = null; droplets = null; body = null;
            speed = 0; acceleration = previousVelocity = LensInertia = Vector3.zero;
        }
    }
}
