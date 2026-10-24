using UnityEngine;

namespace FormulaGame
{
    // A native fade reserves quiet space for navigation without dimming the car.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaHomeBackdrop : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            float[] stops = { 0, .32f, .48f, .67f, 1 };
            float[] opacity = { .98f, .94f, .64f, .08f, 0 };
            for (int i=1;i<stops.Length;i++)
            {
                int n=mesh.currentVertCount;
                float left=r.xMin+r.width*stops[i-1], right=r.xMin+r.width*stops[i];
                var a=new Color(.025f,.002f,.004f,opacity[i-1])*color;
                var b=new Color(.025f,.002f,.004f,opacity[i])*color;
                mesh.AddVert(new Vector3(left,r.yMin),a,Vector2.zero); mesh.AddVert(new Vector3(left,r.yMax),a,Vector2.up);
                mesh.AddVert(new Vector3(right,r.yMax),b,Vector2.one); mesh.AddVert(new Vector3(right,r.yMin),b,Vector2.right);
                mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
