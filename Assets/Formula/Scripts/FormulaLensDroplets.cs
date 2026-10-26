using UnityEngine;

namespace FormulaGame
{
    // A bounded cosmetic simulation: 22 beads, 88 vertices, one shared material.
    // Only bead rectangles rasterize, instead of a transparent full-screen quad.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaLensDroplets : UnityEngine.UI.MaskableGraphic
    {
        public FormulaLensDroplets() { useLegacyMeshGeneration = false; }
        public const int DropCount = 22;
        private struct Drop
        {
            public Vector2 position, velocity;
            public float radius, age, lifetime, adhesion, mobility;
        }
        private readonly Drop[] drops = new Drop[DropCount];
        private uint random = 87231;
        private bool initialized;
        public int SimulatedDropCount => initialized ? DropCount : 0;
        public Vector2 MeanVelocity { get; private set; }
        public float CoveredScreenFraction { get; private set; }
        public override Texture mainTexture => material != null ? material.GetTexture("_MainTex") : Texture2D.whiteTexture;

        public void Initialize()
        {
            random = 87231;
            for (int i = 0; i < DropCount; i++) Spawn(ref drops[i], true);
            initialized = true; SetVerticesDirty();
        }

        private float Next()
        {
            // Private generator: cosmetic respawns do not alter gameplay RNG.
            random = unchecked(random * 1664525u + 1013904223u);
            return (random >> 8) * (1f / 16777216f);
        }

        private void Spawn(ref Drop drop, bool initial)
        {
            float x = Next();
            // Most beads land toward the sides; a few cross the driving view.
            if (Next() < .72f) x = Next() < .5f ? x * .25f : .75f + x * .25f;
            drop.position = new Vector2(x, Next()); drop.velocity = Vector2.zero;
            drop.radius = Mathf.Lerp(.0045f, .009f, Next());
            drop.lifetime = Mathf.Lerp(9, 22, Next());
            drop.age = initial ? Mathf.Lerp(.5f, drop.lifetime - 2, Next()) : 0;
            drop.adhesion = Mathf.Lerp(.3f, 1.15f, Next());
            drop.mobility = Mathf.Lerp(.45f, 1.35f, Mathf.InverseLerp(.0045f, .009f, drop.radius));
        }

        public void Simulate(float dt, Vector2 gravity, Vector3 inertia, float airspeed)
        {
            if (!initialized || dt <= 0) return;
            dt = Mathf.Min(dt, .05f);
            Vector2 mean = Vector2.zero;
            float aspect = Mathf.Max(.1f, rectTransform.rect.width / Mathf.Max(1, rectTransform.rect.height));
            float response = 1 - Mathf.Exp(-dt / .28f);
            for (int i = 0; i < DropCount; i++)
            {
                ref Drop drop = ref drops[i];
                drop.age += dt;
                // Tangential gravity and inertia move water on the lens.
                // Airflow sweeps outward/upward as speed rises. Braking's
                // normal force also releases pinned beads, without input fakes.
                Vector2 force = gravity + new Vector2(inertia.x, inertia.y) * 1.6f;
                force += new Vector2((drop.position.x - .5f) * 1.3f, .65f) * airspeed;
                force.y -= Mathf.Max(0, inertia.z) * .45f;
                float release = Mathf.Clamp01((force.magnitude + Mathf.Abs(inertia.z) * .5f - drop.adhesion) * 1.5f);
                Vector2 target = force * (.038f * drop.mobility * release);
                drop.velocity = Vector2.Lerp(drop.velocity, target, response);
                drop.position += new Vector2(drop.velocity.x / aspect, drop.velocity.y) * dt;
                if (drop.age >= drop.lifetime || drop.position.x < -.04f || drop.position.x > 1.04f || drop.position.y < -.05f || drop.position.y > 1.05f) Spawn(ref drop, false);
                mean += drop.velocity;
            }
            MeanVelocity = mean / DropCount;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); CoveredScreenFraction = 0;
            if (!initialized) return;
            Rect rect = rectTransform.rect;
            float area = Mathf.Max(1, rect.width * rect.height);
            for (int i = 0; i < DropCount; i++)
            {
                var drop = drops[i];
                float radius = drop.radius * rect.height;
                float stretch = 1 + Mathf.Min(1.3f, drop.velocity.magnitude * 12);
                Vector2 along = drop.velocity.sqrMagnitude > .000001f ? drop.velocity.normalized : Vector2.down;
                Vector2 side = new Vector2(along.y, -along.x) * radius;
                along *= radius * stretch;
                Vector2 center = new Vector2(rect.xMin + drop.position.x * rect.width, rect.yMin + drop.position.y * rect.height);
                float fade = Mathf.Clamp01(drop.age / .7f) * Mathf.Clamp01((drop.lifetime - drop.age) / 1.5f);
                float border = Mathf.SmoothStep(.35f, 1, Mathf.Clamp01(Mathf.Abs(drop.position.x - .5f) * 2));
                Color tint = color; tint.a *= fade * border;
                int n = mesh.currentVertCount;
                mesh.AddVert(center - side - along, tint, Vector2.zero);
                mesh.AddVert(center - side + along, tint, Vector2.up);
                mesh.AddVert(center + side + along, tint, Vector2.one);
                mesh.AddVert(center + side - along, tint, Vector2.right);
                mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n + 2, n + 3, n);
                CoveredScreenFraction += 4 * radius * radius * stretch / area;
            }
        }
    }
}
