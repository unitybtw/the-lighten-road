using System.Collections.Generic;
using UnityEngine;
namespace TheLightenRoad.Frontend
{
    /// <summary>Editable mesh geometry for the archive volume, in metres. No primitive plates or UI sprites.</summary>
    public static class ArchiveBookGeometry
    {
        public const float PageWidth=5.8f,PageHeight=6.96f;
        public const int LeafColumns=64,LeafRows=24;
        public static Mesh Binding(float width,float height,float depth,float bevel)
        {
            // Thin signatures and narrow brass bands need smaller radii than the covers.
            float radius=Mathf.Min(.10f,width*.45f,height*.45f);
            bevel=Mathf.Clamp(bevel,0,Mathf.Min(depth*.45f,radius*.8f));
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int count=20;
            for(int ring=0;ring<4;ring++)
            {
                float inset=ring==0 || ring==3?bevel:0;
                float z=ring==0?-depth*.5f:ring==1?-depth*.5f+bevel:ring==2?depth*.5f-bevel:depth*.5f;
                for(int corner=0;corner<4;corner++)for(int j=0;j<5;j++)
                {
                    float a=(corner*90+j*22.5f)*Mathf.Deg2Rad;
                    float x=Mathf.Cos(a)*(radius-inset)+(corner==0||corner==3?1:-1)*(width*.5f-radius);
                    float y=Mathf.Sin(a)*(radius-inset)+(corner<2?1:-1)*(height*.5f-radius);
                    vertices.Add(new Vector3(x,y,z));uv.Add(new Vector2(x/width+.5f,y/height+.5f));
                }
            }
            for(int ring=0;ring<3;ring++)for(int j=0;j<count;j++)
            {int a=ring*count+j,b=ring*count+(j+1)%count;triangles.AddRange(new[]{a,b,a+count,b,b+count,a+count});}
            for(int j=1;j<count-1;j++){triangles.AddRange(new[]{0,j+1,j,3*count,3*count+j,3*count+j+1});}
            var mesh=new Mesh{name="Bevelled book binding"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh Leaf(int side,float progress=0,float grabY=0)
        {var m=new Mesh{name="Flexible double-sided archive leaf"};UpdateLeaf(m,side,progress,grabY);return m;}
        // Midpoint samples are invariant. Cursor solving evaluates this curve thousands
        // of times per gesture; no exponential or trigonometric rest-shape work belongs there.
        private static readonly float[] MidU=new float[LeafColumns],RestAngle=new float[LeafColumns],BendSin=new float[LeafColumns];
        static ArchiveBookGeometry()
        {
            for(int i=0;i<LeafColumns;i++)
            {
                float u=(i+.5f)/LeafColumns;MidU[i]=u;
                RestAngle[i]=Mathf.Atan(.63f/PageWidth*Mathf.Exp(-u*18));BendSin[i]=Mathf.Sin(u*Mathf.PI);
            }
        }
        private readonly struct Curve
        {
            private readonly float centre,gutterSign,lift,corner,baseY,baseZ,pull,influence,radius;
            public Curve(float v,float progress,float grabY,float pullY,float grabU)
            {
                float wave=Mathf.Sin(progress*Mathf.PI);
                centre=Mathf.Lerp(1.32f,-.32f,progress)+.12f*(grabY-(v*2-1))*Mathf.Abs(grabY)*wave;
                gutterSign=Mathf.Cos(progress*Mathf.PI);lift=Mathf.Abs(pullY)/PageWidth*1.5f;
                corner=.045f*wave*grabY;baseY=(v-.5f)*PageHeight;baseZ=-.085f-.025f*Mathf.Sin(v*Mathf.PI);
                pull=pullY;float distance=v-(grabY+1)*.5f;influence=Mathf.Exp(-2*distance*distance);radius=Mathf.Max(.08f,grabU);
            }
            private float Angle(float u,float rest,float bend)
            {
                float t=Mathf.Clamp01((u-centre)/.56f+.5f);
                return Mathf.Clamp(Mathf.PI*(t*t*(3-2*t))+rest*gutterSign+lift*bend,0,Mathf.PI);
            }
            public float SampleAngle(int column)=>Angle(MidU[column],RestAngle[column],BendSin[column]);
            public float PartialAngle(float u)=>Angle(u,Mathf.Atan(.63f/PageWidth*Mathf.Exp(-u*18)),Mathf.Sin(u*Mathf.PI));
            public float Scale(float u)
            {
                if(pull==0)return 1;
                float t=Mathf.Clamp01(u/radius),slope=pull*6*t*(1-t)/radius*influence/PageWidth;
                return Mathf.Sqrt(Mathf.Max(.05f,1-slope*slope));
            }
            public Vector3 Position(int side,float u,float x,float z)
            {
                float t=Mathf.Clamp01(u/radius);
                return new Vector3(side*(x+.015f),baseY+corner*u*u+pull*t*t*(3-2*t)*influence,z+baseZ);
            }
        }
        /// <summary>The same material point used by the mesh and cursor constraint.</summary>
        public static Vector3 LeafPoint(int side,float progress,float grabY,float u,float v,float pullY=0,float grabU=1)
        {
            progress=Mathf.Clamp01(progress);u=Mathf.Clamp01(u);v=Mathf.Clamp01(v);
            var curve=new Curve(v,progress,grabY,pullY,grabU);float x=0,z=0;
            int full=Mathf.FloorToInt(u*LeafColumns);
            for(int column=0;column<full;column++)
            {
                float angle=curve.SampleAngle(column),length=PageWidth/LeafColumns*curve.Scale(MidU[column]);
                x+=Mathf.Cos(angle)*length;z-=Mathf.Sin(angle)*length;
            }
            float remainder=u-full/(float)LeafColumns;
            if(remainder>0)
            {
                float mid=full/(float)LeafColumns+remainder*.5f;
                float angle=curve.PartialAngle(mid),length=PageWidth*remainder*curve.Scale(mid);
                x+=Mathf.Cos(angle)*length;z-=Mathf.Sin(angle)*length;
            }
            return curve.Position(side,u,x,z);
        }
        /// <summary>One owned workspace per animated leaf; topology changes only when its side changes.</summary>
        public sealed class LeafBuffer
        {
            private readonly Vector3[] vertices=new Vector3[(LeafColumns+1)*(LeafRows+1)];
            private readonly Vector2[] uv=new Vector2[(LeafColumns+1)*(LeafRows+1)];
            private readonly int[] indices=new int[LeafColumns*LeafRows*6];
            private Mesh boundMesh;private int boundSide;
            public void Update(Mesh mesh,int side,float progress,float grabY,float pullY,float grabU)
            {
                progress=Mathf.Clamp01(progress);
                bool topology=boundMesh!=mesh || boundSide!=side || mesh.vertexCount!=vertices.Length;
                for(int row=0;row<=LeafRows;row++)
                {
                    float v=row/(float)LeafRows,x=0,z=0;var curve=new Curve(v,progress,grabY,pullY,grabU);
                    for(int column=0;column<=LeafColumns;column++)
                    {
                        float u=column/(float)LeafColumns;int i=row*(LeafColumns+1)+column;
                        if(column>0)
                        {
                            float angle=curve.SampleAngle(column-1),length=PageWidth/LeafColumns*curve.Scale(MidU[column-1]);
                            x+=Mathf.Cos(angle)*length;z-=Mathf.Sin(angle)*length;
                        }
                        vertices[i]=curve.Position(side,u,x,z);
                        if(topology)uv[i]=new Vector2((side*(u*580+1)+630)/1260,(v*696-323+450)/900);
                    }
                }
                if(topology)
                {
                    int k=0;
                    for(int row=0;row<LeafRows;row++)for(int col=0;col<LeafColumns;col++)
                    {int a=row*(LeafColumns+1)+col,b=a+LeafColumns+1;if(side>0){indices[k++]=a;indices[k++]=b;indices[k++]=a+1;indices[k++]=a+1;indices[k++]=b;indices[k++]=b+1;}else{indices[k++]=a;indices[k++]=a+1;indices[k++]=b;indices[k++]=a+1;indices[k++]=b+1;indices[k++]=b;}}
                    if(mesh.vertexCount!=vertices.Length)mesh.Clear();
                    mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
                    boundMesh=mesh;boundSide=side;
                }
                else mesh.SetVertices(vertices);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
            }
        }
        public static void UpdateLeaf(Mesh mesh,int side,float progress,float grabY,float pullY=0,float grabU=1,LeafBuffer buffer=null)
            =>(buffer??new LeafBuffer()).Update(mesh,side,progress,grabY,pullY,grabU);
        public static Mesh Ribbon()
        {
            // The hidden head stays beneath the paper. Only a short, curved forked tip
            // leaves the lower cover; it never crosses the printed voice controls.
            const int steps=24,columns=3;
            var vertices=new Vector3[(steps+1)*columns];var uv=new Vector2[vertices.Length];var triangles=new int[steps*12];
            for(int row=0;row<=steps;row++)
            {
                float t=row/(float)steps;
                for(int col=0;col<columns;col++)
                {
                    float across=col/2f;int i=row*columns+col;
                    float fork=col==1?.065f*Mathf.Pow(t,16):0;
                    vertices[i]=new Vector3((across-.5f)*.14f+.045f*Mathf.Sin(t*Mathf.PI),-3.32f-t*.67f+fork,.025f+.035f*Mathf.Sin(t*Mathf.PI));
                    uv[i]=new Vector2(across,t);
                }
            }
            int k=0;
            for(int row=0;row<steps;row++)for(int col=0;col<2;col++)
            {
                int a=row*columns+col,b=a+columns;
                triangles[k++]=a;triangles[k++]=b;triangles[k++]=a+1;
                triangles[k++]=a+1;triangles[k++]=b;triangles[k++]=b+1;
            }
            var mesh=new Mesh{name="Tucked cloth bookmark with forked tip"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
