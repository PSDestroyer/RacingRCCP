using UnityEngine;

namespace FormulaGame
{
    // Resolution-independent symbols identify the event type in the former flag slot.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaMissionIcon : UnityEngine.UI.MaskableGraphic
    {
        public enum Symbol { Helmet, Stopwatch, Race, Sprint, Precision, Trophy }
        [SerializeField] private Symbol symbol;
        [SerializeField] private float stroke = 1.8f;
        public Symbol DisplaySymbol => symbol;
        public void SetEvent(FormulaCatalog.CareerEvent e)
        {
            Symbol next;
            switch (e.missionIcon)
            {
                case "stopwatch": next = Symbol.Stopwatch; break;
                case "race": next = Symbol.Race; break;
                case "sprint": next = Symbol.Sprint; break;
                case "precision": next = Symbol.Precision; break;
                case "trophy": next = Symbol.Trophy; break;
                default: next = Symbol.Helmet; break;
            }
            if (next == symbol) return;
            symbol = next; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            switch (symbol)
            {
                case Symbol.Helmet:
                    Path(mesh, new[] { new Vector2(.16f,.28f),new Vector2(.13f,.44f),new Vector2(.18f,.65f),new Vector2(.31f,.81f),new Vector2(.50f,.87f),new Vector2(.69f,.82f),new Vector2(.82f,.65f),new Vector2(.88f,.29f),new Vector2(.73f,.20f),new Vector2(.29f,.20f),new Vector2(.16f,.28f) });
                    Path(mesh, new[] { new Vector2(.22f,.62f),new Vector2(.71f,.62f),new Vector2(.76f,.46f),new Vector2(.28f,.46f),new Vector2(.22f,.62f) });
                    Line(mesh, new Vector2(.30f,.32f), new Vector2(.75f,.32f)); break;
                case Symbol.Stopwatch:
                    Ring(mesh, new Vector2(.5f,.45f), .30f);
                    Line(mesh,new Vector2(.5f,.75f),new Vector2(.5f,.87f)); Line(mesh,new Vector2(.39f,.89f),new Vector2(.61f,.89f));
                    Line(mesh,new Vector2(.73f,.69f),new Vector2(.81f,.78f));
                    Line(mesh,new Vector2(.5f,.45f),new Vector2(.5f,.64f)); Line(mesh,new Vector2(.5f,.45f),new Vector2(.65f,.36f)); break;
                case Symbol.Race:
                    Line(mesh,new Vector2(.18f,.12f),new Vector2(.18f,.87f));
                    Path(mesh,new[] {new Vector2(.19f,.83f),new Vector2(.85f,.83f),new Vector2(.85f,.43f),new Vector2(.19f,.43f)});
                    for (int y=0;y<3;y++) for (int x=0;x<4;x++) if ((x+y)%2==0)
                    { float left=.21f+x*.15f,bottom=.45f+y*.12f; Fill(mesh,new Vector2(left,bottom),new Vector2(left+.15f,bottom),new Vector2(left+.15f,bottom+.12f),new Vector2(left,bottom+.12f)); }
                    break;
                case Symbol.Sprint:
                    Triangle(mesh,new Vector2(.62f,.94f),new Vector2(.19f,.44f),new Vector2(.57f,.44f));
                    Triangle(mesh,new Vector2(.38f,.06f),new Vector2(.81f,.62f),new Vector2(.44f,.62f)); break;
                case Symbol.Precision:
                    Ring(mesh,new Vector2(.5f,.5f),.28f); Ring(mesh,new Vector2(.5f,.5f),.10f);
                    Line(mesh,new Vector2(.5f,.05f),new Vector2(.5f,.32f)); Line(mesh,new Vector2(.5f,.68f),new Vector2(.5f,.95f));
                    Line(mesh,new Vector2(.05f,.5f),new Vector2(.32f,.5f)); Line(mesh,new Vector2(.68f,.5f),new Vector2(.95f,.5f)); break;
                case Symbol.Trophy:
                    Path(mesh,new[] {new Vector2(.27f,.84f),new Vector2(.73f,.84f),new Vector2(.68f,.53f),new Vector2(.58f,.40f),new Vector2(.42f,.40f),new Vector2(.32f,.53f),new Vector2(.27f,.84f)});
                    Path(mesh,new[] {new Vector2(.28f,.77f),new Vector2(.12f,.77f),new Vector2(.14f,.59f),new Vector2(.32f,.50f)});
                    Path(mesh,new[] {new Vector2(.72f,.77f),new Vector2(.88f,.77f),new Vector2(.86f,.59f),new Vector2(.68f,.50f)});
                    Line(mesh,new Vector2(.5f,.4f),new Vector2(.5f,.17f)); Line(mesh,new Vector2(.33f,.15f),new Vector2(.67f,.15f)); break;
            }
        }
        private Vector2 Point(Vector2 p) => new Vector2(rectTransform.rect.xMin+p.x*rectTransform.rect.width,rectTransform.rect.yMin+p.y*rectTransform.rect.height);
        private void Path(UnityEngine.UI.VertexHelper mesh, Vector2[] points) { for(int i=1;i<points.Length;i++) Line(mesh,points[i-1],points[i]); }
        private void Ring(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius)
        {
            const int segments=36;
            for(int i=0;i<segments;i++) { float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments; Line(mesh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius); }
        }
        private void Line(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b)
        {
            a=Point(a);b=Point(b);var d=b-a; if(d.sqrMagnitude<.0001f)return;
            var n=new Vector2(-d.y,d.x).normalized*(stroke*.5f);Quad(mesh,a-n,a+n,b+n,b-n);
        }
        private void Fill(UnityEngine.UI.VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d) => Quad(mesh,Point(a),Point(b),Point(c),Point(d));
        private void Quad(UnityEngine.UI.VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            int i=mesh.currentVertCount;mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        private void Triangle(UnityEngine.UI.VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c)
        {
            int i=mesh.currentVertCount;mesh.AddVert(Point(a),color,Vector2.zero);mesh.AddVert(Point(b),color,Vector2.zero);mesh.AddVert(Point(c),color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);
        }
    }
}
