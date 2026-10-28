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
        // The bend travels from the free edge into the binding. Integrating its tangent
        // preserves each horizontal paper strip's length instead of stretching a flat plate.
        private static float LeafAngle(float u,float v,float progress,float grabY,float pullY=0)
        {
            float wave=Mathf.Sin(progress*Mathf.PI);
            float diagonal=.12f*(grabY-(v*2-1))*Mathf.Abs(grabY)*wave;
            float centre=Mathf.Lerp(1.32f,-.32f,progress)+diagonal;
            float t=Mathf.Clamp01((u-centre)/.56f+.5f);
            float gutter=Mathf.Atan(.63f/PageWidth*Mathf.Exp(-u*18))*Mathf.Cos(progress*Mathf.PI);
            float lift=Mathf.Abs(pullY)/PageWidth*1.5f*Mathf.Sin(u*Mathf.PI);
            return Mathf.Clamp(Mathf.PI*(t*t*(3-2*t))+gutter+lift,0,Mathf.PI);
        }
        private static Vector3 LeafPosition(int side,float u,float v,float progress,float grabY,float x,float z,float pullY=0,float grabU=1)
        {
            float wave=Mathf.Sin(progress*Mathf.PI);
            float y=(v-.5f)*PageHeight+.045f*wave*u*u*grabY;
            y+=pullY*PullWeight(u,v,grabY,grabU);
            float restingZ=-.085f-.025f*Mathf.Sin(v*Mathf.PI);
            return new Vector3(side*(x+.015f),y,z+restingZ);
        }
        private static float PullWeight(float u,float v,float grabY,float grabU)
        {
            float t=Mathf.Clamp01(u/Mathf.Max(.08f,grabU));
            float distance=v-(grabY+1)*.5f;
            return t*t*(3-2*t)*Mathf.Exp(-2*distance*distance);
        }
        private static float StripScale(float u,float v,float grabY,float pullY,float grabU)
        {
            float radius=Mathf.Max(.08f,grabU),t=Mathf.Clamp01(u/radius);
            float distance=v-(grabY+1)*.5f;
            float slope=pullY*6*t*(1-t)/radius*Mathf.Exp(-2*distance*distance)/PageWidth;
            // Vertical motion uses paper length instead of stretching its horizontal strips.
            return Mathf.Sqrt(Mathf.Max(.05f,1-slope*slope));
        }
        /// <summary>Same material point used by both the mesh and the cursor constraint.</summary>
        public static Vector3 LeafPoint(int side,float progress,float grabY,float u,float v,float pullY=0,float grabU=1)
        {
            progress=Mathf.Clamp01(progress);u=Mathf.Clamp01(u);v=Mathf.Clamp01(v);
            float x=0,z=0;
            int full=Mathf.FloorToInt(u*LeafColumns);
            for(int column=0;column<full;column++)
            {
                float angle=LeafAngle((column+.5f)/LeafColumns,v,progress,grabY,pullY);
                float length=PageWidth/LeafColumns*StripScale((column+.5f)/LeafColumns,v,grabY,pullY,grabU);
                x+=Mathf.Cos(angle)*length;z-=Mathf.Sin(angle)*length;
            }
            float remainder=u-full/(float)LeafColumns;
            if(remainder>0)
            {
                float angle=LeafAngle(full/(float)LeafColumns+remainder*.5f,v,progress,grabY,pullY);
                float length=PageWidth*remainder*StripScale(full/(float)LeafColumns+remainder*.5f,v,grabY,pullY,grabU);
                x+=Mathf.Cos(angle)*length;z-=Mathf.Sin(angle)*length;
            }
            return LeafPosition(side,u,v,progress,grabY,x,z,pullY,grabU);
        }
        public static void UpdateLeaf(Mesh mesh,int side,float progress,float grabY,float pullY=0,float grabU=1)
        {
            progress=Mathf.Clamp01(progress);
            const int columns=LeafColumns,rows=LeafRows;
            var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];
            var indices=new int[columns*rows*6];
            for(int row=0;row<=rows;row++)
            {
                float v=row/(float)rows,x=0,z=0;
                for(int column=0;column<=columns;column++)
                {
                    float u=column/(float)columns;int i=row*(columns+1)+column;
                    if(column>0)
                    {
                        float angle=LeafAngle((column-.5f)/columns,v,progress,grabY,pullY);
                        float length=PageWidth/columns*StripScale((column-.5f)/columns,v,grabY,pullY,grabU);
                        x+=Mathf.Cos(angle)*length;z-=Mathf.Sin(angle)*length;
                    }
                    vertices[i]=LeafPosition(side,u,v,progress,grabY,x,z,pullY,grabU);
                    uv[i]=new Vector2((side*(u*580+1)+630)/1260,(v*696-323+450)/900);
                }
            }
            int k=0;
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {int a=row*(columns+1)+col;int b=a+columns+1;if(side>0){indices[k++]=a;indices[k++]=b;indices[k++]=a+1;indices[k++]=a+1;indices[k++]=b;indices[k++]=b+1;}else{indices[k++]=a;indices[k++]=a+1;indices[k++]=b;indices[k++]=a+1;indices[k++]=b+1;indices[k++]=b;}}
            mesh.Clear();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
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
