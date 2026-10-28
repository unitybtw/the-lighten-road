using System.Collections.Generic;
using UnityEngine;
namespace TheLightenRoad.Frontend
{
    /// <summary>Editable mesh geometry for the archive volume, in metres. No primitive plates or UI sprites.</summary>
    public static class ArchiveBookGeometry
    {
        public const float PageWidth=5.8f,PageHeight=6.96f;
        public static Mesh Binding(float width,float height,float depth,float bevel)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int count=20;
            for(int ring=0;ring<4;ring++)
            {
                float inset=ring==0 || ring==3?bevel:0;
                float z=ring==0?-depth*.5f:ring==1?-depth*.5f+bevel:ring==2?depth*.5f-bevel:depth*.5f;
                for(int corner=0;corner<4;corner++)for(int j=0;j<5;j++)
                {
                    float a=(corner*90+j*22.5f)*Mathf.Deg2Rad;
                    float radius=.10f;
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
        public static void UpdateLeaf(Mesh mesh,int side,float progress,float grabY)
        {
            const int columns=64,rows=24;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];
            var indices=new int[columns*rows*6];var rowX=new float[rows+1];var rowZ=new float[rows+1];
            for(int column=0;column<=columns;column++)
            {
                float u=column/(float)columns;
                for(int row=0;row<=rows;row++)
                {
                    float v=row/(float)rows;int i=row*(columns+1)+column;
                    if(column>0)
                    {
                        float mid=(column-.5f)/columns;
                        float twist=.85f*grabY*(v*2-1)*mid;
                        float angle=progress*Mathf.PI+Mathf.Sin(progress*Mathf.PI)*(.64f*Mathf.Sin(mid*Mathf.PI)+twist);
                        rowX[row]+=Mathf.Cos(angle)*PageWidth/columns;rowZ[row]-=Mathf.Sin(angle)*PageWidth/columns;
                    }
                    float curl=Mathf.Sin(Mathf.PI*progress)*u*u;
                    float y=(v-.5f)*PageHeight+curl*.34f*(grabY-v*2+1);
                    vertices[i]=new Vector3(side*(rowX[row]+.015f),y,rowZ[row]-.12f-.10f*Mathf.Exp(-u*25)-.025f*Mathf.Sin(v*Mathf.PI));
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
            const int steps=24;var v=new Vector3[(steps+1)*2];var uv=new Vector2[v.Length];var t=new int[steps*6];
            for(int i=0;i<=steps;i++){float p=i/(float)steps;for(int j=0;j<2;j++){int n=i*2+j;v[n]=new Vector3((j-.5f)*.19f+.08f*Mathf.Sin(p*2),-3.1f-p*1.15f,-.05f-.09f*Mathf.Sin(p*Mathf.PI));uv[n]=new Vector2(j,p);}}
            for(int i=0;i<steps;i++){int n=i*2;int k=i*6;t[k]=n;t[k+1]=n+2;t[k+2]=n+1;t[k+3]=n+1;t[k+4]=n+2;t[k+5]=n+3;}
            var m=new Mesh{name="Cloth bookmark"};m.vertices=v;m.uv=uv;m.triangles=t;m.RecalculateNormals();return m;
        }
    }
}
