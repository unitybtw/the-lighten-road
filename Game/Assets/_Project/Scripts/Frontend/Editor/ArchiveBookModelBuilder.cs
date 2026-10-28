using System.IO;
using UnityEditor;
using UnityEngine;
namespace TheLightenRoad.Frontend.Editor
{
    public static class ArchiveBookModelBuilder
    {
        const string Folder="Assets/_Project/Resources/UI/ArchiveBook3D";
        static T Save<T>(T asset,string name) where T:Object
        {
            string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old!=null){EditorUtility.CopySerialized(asset,old);Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(asset,path);return asset;
        }
        [MenuItem("The Lighten Road/Archive/Rebuild 3D Book Model")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            Material Surface(string name,Color color,float paper=0,float metal=0)
            {var m=new Material(Shader.Find("TheLightenRoad/Archive Book Surface")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Paper",paper);m.SetFloat("_Metal",metal);return Save(m,name);}
            var leather=Surface("Worn walnut leather",new Color(.16f,.082f,.043f));
            var brass=Surface("Aged brass",new Color(.52f,.31f,.10f),0,1);
            var paper=Surface("Ivory rag paper",new Color(.84f,.75f,.57f),1);
            var edges=Surface("Folio edges",new Color(.63f,.53f,.37f),1);
            var red=Surface("Oxblood cloth",new Color(.28f,.052f,.038f));
            var ink=Surface("Recessed binding",new Color(.055f,.031f,.021f));
            var glass=Surface("Lantern amber",new Color(.87f,.45f,.08f),0,.3f);
            var root=new GameObject("Archive Book 3D");root.layer=31;var model=root.AddComponent<ArchiveBookModel>();
            GameObject Part(string name,Transform parent,Mesh geometry,Material material,Vector3 position)
            {
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=31;go.transform.SetParent(parent,false);go.transform.localPosition=position;
                go.GetComponent<MeshFilter>().sharedMesh=Save(geometry,name+" mesh");go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
            }
            GameObject Box(string name,Transform parent,Vector3 size,Vector3 position,Material material,float bevel=.025f)
                =>Part(name,parent,ArchiveBookGeometry.Binding(size.x,size.y,size.z,bevel),material,position);
            var left=new GameObject("Left cover and folios");left.layer=31;left.transform.SetParent(root.transform,false);model.LeftAssembly=left.transform;
            foreach(int side in new[]{-1,1})
            {
                var parent=side<0?left.transform:root.transform;string label=side<0?"Left":"Right";
                Box(label+" bevelled hardcover",parent,new Vector3(6.12f,7.5f,.19f),new Vector3(side*3.08f,0,.22f),leather,.045f);
                Box(label+" raised cover panel",parent,new Vector3(5.88f,7.23f,.06f),new Vector3(side*3.08f,0,.105f),ink,.028f);
                Box(label+" bound folio block",parent,new Vector3(5.84f,7.0f,.18f),new Vector3(side*2.94f,0,.035f),edges,.022f);
                for(int i=0;i<7;i++)Box(label+" signature "+i,parent,new Vector3(5.83f,6.98f,.008f),new Vector3(side*2.94f,0,-.045f+i*.022f),paper,.002f);
                var leaf=Part(label+" readable leaf",parent,ArchiveBookGeometry.Leaf(side),paper,Vector3.zero);
                var collider=leaf.AddComponent<MeshCollider>();collider.sharedMesh=leaf.GetComponent<MeshFilter>().sharedMesh;
                if(side<0){model.LeftPaper=leaf.GetComponent<MeshRenderer>();model.LeftCollider=collider;}else{model.RightPaper=leaf.GetComponent<MeshRenderer>();model.RightCollider=collider;}
                foreach(float y in new[]{-3.58f,3.58f})Box(label+" brass corner "+y,parent,new Vector3(.27f,.27f,.035f),new Vector3(side*5.96f,y,.08f),brass,.012f);
            }
            Box("Rounded leather spine",root.transform,new Vector3(.28f,7.44f,.35f),new Vector3(0,0,.14f),leather,.045f);
            for(int i=0;i<5;i++)Box("Raised spine band "+i,root.transform,new Vector3(.33f,.085f,.12f),new Vector3(0,-2.8f+i*1.4f,.025f),brass,.02f);
            var hinge=new GameObject("Front cover hinge");hinge.layer=31;hinge.transform.SetParent(root.transform,false);model.CoverHinge=hinge.transform;
            Box("Opening leather cover",hinge.transform,new Vector3(6.12f,7.5f,.18f),new Vector3(3.08f,0,-.32f),leather,.045f);
            Box("Embossed cover inset",hinge.transform,new Vector3(5.4f,6.7f,.025f),new Vector3(3.08f,0,-.425f),ink,.016f);
            foreach(float x in new[]{.34f,5.82f})foreach(float y in new[]{-3.38f,3.38f})Box("Cover brass "+x+" "+y,hinge.transform,new Vector3(.28f,.28f,.045f),new Vector3(x,y,-.445f),brass,.012f);
            Box("Lantern glass medallion",hinge.transform,new Vector3(.65f,.90f,.045f),new Vector3(3.08f,0,-.48f),glass,.03f);
            foreach(float x in new[]{2.70f,3.46f})Box("Lantern cage "+x,hinge.transform,new Vector3(.07f,1.18f,.065f),new Vector3(x,0,-.52f),brass,.015f);
            foreach(float y in new[]{-.55f,.55f})Box("Lantern crown "+y,hinge.transform,new Vector3(.90f,.075f,.065f),new Vector3(3.08f,y,-.52f),brass,.02f);
            Box("Lantern handle",hinge.transform,new Vector3(.46f,.26f,.065f),new Vector3(3.08f,.78f,-.50f),brass,.025f);
            Part("Woven red bookmark",left.transform,ArchiveBookGeometry.Ribbon(),red,new Vector3(-1.2f,0,-.06f));
            var turning=Part("Flexible turning leaf",root.transform,ArchiveBookGeometry.Leaf(1),paper,new Vector3(0,0,-.035f));
            model.TurningPaper=turning.GetComponent<MeshRenderer>();model.TurningFilter=turning.GetComponent<MeshFilter>();turning.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ArchiveBook.prefab");Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        }
    }
}
