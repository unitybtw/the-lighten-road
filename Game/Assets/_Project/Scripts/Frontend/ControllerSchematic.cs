using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>Original UI vector schematic, independent of controller-brand artwork or raster screenshots.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ControllerSchematic : MaskableGraphic
    {
        public bool SymmetricSticks;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2[] outline={new Vector2(-145,65),new Vector2(-185,36),new Vector2(-211,-43),new Vector2(-208,-92),new Vector2(-184,-104),new Vector2(-149,-76),new Vector2(-108,-39),new Vector2(108,-39),new Vector2(149,-76),new Vector2(184,-104),new Vector2(208,-92),new Vector2(211,-43),new Vector2(185,36),new Vector2(145,65)};
            for(int i=0;i<outline.Length;i++)
            {
                Vector2 p0=outline[(i+outline.Length-1)%outline.Length],p1=outline[i],p2=outline[(i+1)%outline.Length],p3=outline[(i+2)%outline.Length];
                Vector2 last=p1;
                for(int s=1;s<=10;s++)
                {
                    float t=s/10f;
                    Vector2 p=.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t);
                    Line(vh,last,p,1.4f);last=p;
                }
            }
            Circle(vh,new Vector2(SymmetricSticks?-64:-115,SymmetricSticks?-27:18),26);
            Circle(vh,new Vector2(64,-27),26);
            Vector2 dpad=new Vector2(SymmetricSticks?-122:-64,SymmetricSticks?18:-27);
            Vector2[] cross={new Vector2(-7,19),new Vector2(7,19),new Vector2(7,7),new Vector2(19,7),new Vector2(19,-7),new Vector2(7,-7),new Vector2(7,-19),new Vector2(-7,-19),new Vector2(-7,-7),new Vector2(-19,-7),new Vector2(-19,7),new Vector2(-7,7)};
            for(int i=0;i<cross.Length;i++)Line(vh,dpad+cross[i],dpad+cross[(i+1)%cross.Length],1.2f);
            foreach(var p in new[]{new Vector2(119,20),new Vector2(147,-7),new Vector2(119,-34),new Vector2(91,-7)})Circle(vh,p,10);
            Line(vh,new Vector2(-17,20),new Vector2(17,20),1.4f);
            Line(vh,new Vector2(-160,72),new Vector2(-93,72),2);
            Line(vh,new Vector2(93,72),new Vector2(160,72),2);
            // Leader lines terminate outside the controls, keeping text out of the silhouette.
            Leader(vh,dpad,new Vector2(-225,65),new Vector2(-300,65));
            Leader(vh,new Vector2(SymmetricSticks?-64:-115,SymmetricSticks?-27:18),new Vector2(-225,-15),new Vector2(-300,-15));
            Leader(vh,new Vector2(0,20),new Vector2(225,100),new Vector2(300,100));
            Leader(vh,new Vector2(119,20),new Vector2(235,48),new Vector2(300,48));
            Leader(vh,new Vector2(91,-7),new Vector2(235,-8),new Vector2(300,-8));
            Leader(vh,new Vector2(119,-34),new Vector2(235,-65),new Vector2(300,-65));
        }
        void Leader(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c){Line(vh,a,b,1.2f);Line(vh,b,c,1.2f);}
        void Circle(VertexHelper vh,Vector2 center,float radius)
        {
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                Line(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,1.15f);
            }
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width)
        {
            Vector2 scale=new Vector2(rectTransform.rect.width/800f,rectTransform.rect.height/360f);
            a=Vector2.Scale(a,scale);b=Vector2.Scale(b,scale);
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            int start=vh.currentVertCount;
            vh.AddVert(a-normal,color,Vector2.zero);vh.AddVert(a+normal,color,Vector2.zero);
            vh.AddVert(b+normal,color,Vector2.zero);vh.AddVert(b-normal,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
        public void SetSymmetric(bool symmetric){SymmetricSticks=symmetric;SetVerticesDirty();}
    }
}
