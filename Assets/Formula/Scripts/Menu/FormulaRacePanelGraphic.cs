using UnityEngine;

namespace FormulaGame
{
    // A clipped corner and a restrained red rail keep the HUD distinct from menu cards.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaRacePanelGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private int proximitySide;
        public void ConfigureProximity(bool right)
        {
            proximitySide = right ? 1 : -1; raycastTarget = false; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (proximitySide != 0)
            {
                // Two outlined chevrons stay recognizable over both bright and dark track surfaces.
                for (int layer = 0; layer < 2; layer++) for (int i = 0; i < 2; i++)
                {
                    float x = 5 + i * 11;
                    Vector2 tip = new Vector2(proximitySide < 0 ? r.xMin + x : r.xMax - x, r.center.y);
                    Vector2 arm = Vector2.right * (proximitySide < 0 ? 13 : -13);
                    Color tint = (layer == 0 ? Color.black : FormulaUI.Accent) * color;
                    float width = layer == 0 ? 11 : 5;
                    FormulaRaceMinimap.Stroke(vh, tip + arm + Vector2.up * 24, tip, width, tint);
                    FormulaRaceMinimap.Stroke(vh, tip, tip + arm - Vector2.up * 24, width, tint);
                }
                return;
            }
            float cut = Mathf.Min(14, r.height * .3f);
            Color left = new Color(0.025f, 0.018f, 0.022f, 1) * color;
            Color right = new Color(0.018f, 0.012f, 0.016f, 1) * color;
            vh.AddVert(new Vector3(r.xMin, r.yMin), left, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), left, Vector2.up);
            vh.AddVert(new Vector3(r.xMax, r.yMax), right, Vector2.one);
            vh.AddVert(new Vector3(r.xMax, r.yMin + cut), right, Vector2.right);
            vh.AddVert(new Vector3(r.xMax - cut, r.yMin), right, Vector2.right);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3); vh.AddTriangle(0, 3, 4);
            Quad(vh, r.xMin, r.yMax - 1, r.xMax, r.yMax, new Color(1, 1, 1, .24f) * color);
            Quad(vh, r.xMin, r.yMax - 2, r.xMin + Mathf.Min(64, r.width), r.yMax, FormulaUI.Red * color);
            Quad(vh, r.xMin, r.yMin, r.xMin + 2, r.yMax - 2, new Color(1, 1, 1, .18f) * color);
        }
        private static void Quad(UnityEngine.UI.VertexHelper vh, float x0, float y0, float x1, float y1, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), tint, Vector2.zero); vh.AddVert(new Vector3(x0, y1), tint, Vector2.up);
            vh.AddVert(new Vector3(x1, y1), tint, Vector2.one); vh.AddVert(new Vector3(x1, y0), tint, Vector2.right);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
