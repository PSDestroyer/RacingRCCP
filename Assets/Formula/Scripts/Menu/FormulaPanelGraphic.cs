using UnityEngine;
using UnityEngine.UI;

namespace FormulaGame
{
    // Native UI geometry keeps the Upgrade gradient and hairlines crisp at every UI scale.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaPanelGraphic : MaskableGraphic
    {
        [SerializeField] private bool selected;
        public void Configure(bool selection) { selected = selection; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Color left = selected ? new Color32(255, 30, 0, 255) : new Color32(14, 10, 11, 255);
            Color right = selected ? new Color32(94, 8, 2, 255) : new Color32(14, 10, 11, 255);
            Quad(vh, r.xMin, r.yMin, r.xMax, r.yMax, left * color, right * color);
            Color edge = new Color(1, 1, 1, selected ? .7f : .32f) * color;
            Quad(vh, r.xMin, r.yMax - 1, r.xMax, r.yMax, edge, edge);
            Quad(vh, r.xMin, r.yMin, r.xMax, r.yMin + 1, edge, edge);
            if (selected) Quad(vh, r.xMin, r.yMin, r.xMin + 3, r.yMax, Color.white * color, Color.white * color);
        }
        private static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color left, Color right)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(x0,y0), left, Vector2.zero); vh.AddVert(new Vector3(x0,y1), left, Vector2.up);
            vh.AddVert(new Vector3(x1,y1), right, Vector2.one); vh.AddVert(new Vector3(x1,y0), right, Vector2.right);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
