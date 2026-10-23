using UnityEngine;

namespace FormulaGame
{
    // Native vector graphic drawn from this game's validated waypoint route.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaTrackMap : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private string trackId = "track_01";
        [SerializeField] private float lineWidth = 2f;
        public void SetTrack(string id) { trackId = id; SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var art = FormulaMenuAssets.Current;
            var route = art != null && art.trackMaps != null ? System.Array.Find(art.trackMaps, r => r.id == trackId) : null;
            if (route == null || route.points == null || route.points.Length < 3) return;
            var rect = rectTransform.rect;
            Vector2 Point(int i) => new Vector2(rect.xMin + 8 + route.points[i].x * (rect.width - 16), rect.yMin + 8 + route.points[i].y * (rect.height - 16));
            for (int i = 0; i < route.points.Length; i++)
            {
                Vector2 a = Point(i), b = Point((i + 1) % route.points.Length), normal = new Vector2(-(b - a).y, (b - a).x).normalized * lineWidth * .5f;
                Quad(mesh, a - normal, a + normal, b + normal, b - normal, color);
            }
            var start = Point(0); const float half = 3f;
            Quad(mesh, start + new Vector2(-half, -half), start + new Vector2(-half, half), start + new Vector2(half, half), start + new Vector2(half, -half), FormulaUI.Accent);
        }
        private static void Quad(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            int offset = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero); mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2); mesh.AddTriangle(offset, offset + 2, offset + 3);
        }
    }
}
