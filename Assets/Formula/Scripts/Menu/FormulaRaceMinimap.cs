using UnityEngine;

namespace FormulaGame
{
    // Static circuit geometry is built once from the same world-space route used by the race.
    // Moving cars use a separate graphic, so they never rebuild the circuit mesh.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaRaceMinimap : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private FormulaMinimapMarkers markers;
        [SerializeField] private GameObject rivalsLegend;
        private Vector2[] path;
        private Vector2 worldCenter, worldSize;
        private Vector3 startFinish;
        public int RoutePointCount => path == null ? 0 : path.Length;

        public void Bind(GamePlayManager game)
        {
            raycastTarget = false;
            var route = game.raceWaypoints;
            if (route == null || route.waypoints.Count < 3)
            {
                Debug.LogError("Formula minimap requires a circuit route.", this); return;
            }
            path = new Vector2[route.waypoints.Count];
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 position = route.waypoints[i].transform.position;
                path[i] = new Vector2(position.x, position.z);
                minimum = Vector2.Min(minimum, path[i]); maximum = Vector2.Max(maximum, path[i]);
            }
            worldCenter = (minimum + maximum) * .5f; worldSize = maximum - minimum;
            var circuit = game.GetComponent<FormulaCircuit>();
            startFinish = circuit != null && circuit.startFinish != null ? circuit.startFinish.position : route.waypoints[0].transform.position;
            markers.Bind(this, game);
            rivalsLegend.SetActive(false);
            SetVerticesDirty();
        }

        public Vector2 WorldToMap(Vector3 position)
        {
            Rect rect = rectTransform.rect;
            float scale = Mathf.Min(Mathf.Max(1, rect.width - 24) / Mathf.Max(1, worldSize.x), Mathf.Max(1, rect.height - 24) / Mathf.Max(1, worldSize.y));
            return rect.center + (new Vector2(position.x, position.z) - worldCenter) * scale;
        }
        public Vector2 ClampMarker(Vector2 position)
        {
            Rect rect = rectTransform.rect;
            return new Vector2(Mathf.Clamp(position.x, rect.xMin + 11, rect.xMax - 11), Mathf.Clamp(position.y, rect.yMin + 11, rect.yMax - 11));
        }
        private Vector2 Point(int index) => WorldToMap(new Vector3(path[index].x, 0, path[index].y));
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (path == null || path.Length < 3) return;
            for (int layer = 0; layer < 2; layer++)
            {
                float width = layer == 0 ? 7 : 3;
                Color tint = layer == 0 ? new Color(0, 0, 0, .9f) : Color.white;
                for (int i = 0; i < path.Length; i++) Stroke(mesh, Point(i), Point((i + 1) % path.Length), width, tint * color);
            }
            // Repeated chevrons communicate the authored racing direction while the map stays fixed.
            foreach (int index in new[] { path.Length / 6, path.Length * 2 / 3 })
            {
                Vector2 forward = (Point((index + 1) % path.Length) - Point(index)).normalized;
                Arrow(mesh, Point(index), forward, 7, Color.black * color);
                Arrow(mesh, Point(index), forward, 5, FormulaUI.Red * color);
            }
            Vector2 start = WorldToMap(startFinish), tangent = (Point(1) - Point(0)).normalized;
            if (tangent.sqrMagnitude < .1f) tangent = Vector2.up;
            Vector2 side = new Vector2(-tangent.y, tangent.x);
            Quad(mesh, start - side * 8 - tangent * 4, start - side * 8 + tangent * 4, start + side * 8 + tangent * 4, start + side * 8 - tangent * 4, Color.black * color);
            for (int row = 0; row < 2; row++) for (int column = 0; column < 6; column++)
            {
                Vector2 center = start + side * ((column - 2.5f) * 2) + tangent * ((row - .5f) * 2);
                Color tint = (row + column) % 2 == 0 ? Color.white : Color.black;
                Quad(mesh, center - side - tangent, center - side + tangent, center + side + tangent, center + side - tangent, tint * color);
            }
        }
        internal static void Stroke(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(mesh, a - normal, a + normal, b + normal, b - normal, tint);
        }
        internal static void Quad(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.up);
            mesh.AddVert(c, tint, Vector2.one); mesh.AddVert(d, tint, Vector2.right);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
        internal static void Arrow(UnityEngine.UI.VertexHelper mesh, Vector2 center, Vector2 forward, float radius, Color tint)
        {
            Vector2 side = new Vector2(-forward.y, forward.x); int n = mesh.currentVertCount;
            mesh.AddVert(center + forward * radius, tint, Vector2.up);
            mesh.AddVert(center - forward * radius * .7f - side * radius * .65f, tint, Vector2.zero);
            mesh.AddVert(center - forward * radius * .7f + side * radius * .65f, tint, Vector2.right);
            mesh.AddTriangle(n, n + 1, n + 2);
        }
        internal static void Disc(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            int n = mesh.currentVertCount; mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= 10; i++)
            {
                float angle = i * Mathf.PI * .2f;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(n, n + i, n + i + 1);
            }
        }
    }
}
