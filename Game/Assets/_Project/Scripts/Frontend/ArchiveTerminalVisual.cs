using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace TheLightenRoad.Frontend
{
    /// <summary>A code-drawn, point-filtered filing desk and reading lamp for the ruined office.</summary>
    public sealed class ArchiveTerminalVisual : MonoBehaviour
    {
        private Light2D lamp;
        private static Sprite desk;
        private static Sprite BuildSprite()
        {
            var tex=new Texture2D(48,56,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,name="Archive desk pixels"};
            var pixels=new Color[48*56];tex.SetPixels(pixels);
            Color iron=new Color32(49,51,48,255),edge=new Color32(94,90,72,255),dark=new Color32(23,26,25,255),paper=new Color32(177,164,123,255),brass=new Color32(140,102,48,255);
            void Box(int x,int y,int w,int h,Color c){for(int j=y;j<y+h;j++)for(int i=x;i<x+w;i++)tex.SetPixel(i,j,c);}
            Box(8,0,4,4,dark);Box(35,0,4,4,dark);Box(6,3,35,26,dark);Box(8,5,31,23,iron);
            for(int y=7;y<26;y+=9){Box(10,y,27,7,edge);Box(11,y+1,25,5,iron);Box(21,y+3,7,1,brass);Box(10,y,27,1,dark);}
            Box(3,28,42,3,brass);Box(4,31,40,2,edge);Box(7,32,26,1,dark);
            Box(9,33,22,2,dark);Box(9,35,10,5,paper);Box(20,35,10,5,paper);Box(19,34,1,7,brass);
            for(int y=36;y<40;y+=2){Box(11,y,6,1,edge);Box(22,y,6,1,edge);}
            Box(35,33,5,2,brass);Box(37,35,1,13,edge);Box(29,47,9,1,brass);
            Box(27,44,8,3,dark);Box(26,43,10,1,brass);Box(28,42,6,1,new Color32(246,188,83,255));
            Box(7,22,2,3,brass);Box(37,9,1,4,brass);Box(13,5,3,1,dark);Box(33,29,2,1,paper);
            tex.Apply();return Sprite.Create(tex,new Rect(0,0,48,56),new Vector2(.5f,.15f),32);
        }
        void Awake()
        {
            if(desk==null)desk=BuildSprite();
            var go=new GameObject("Filing desk and ledger",typeof(SpriteRenderer));go.transform.SetParent(transform,false);
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=desk;sr.sortingOrder=0;
            var light=new GameObject("Desk lamp pool",typeof(Light2D));light.transform.SetParent(transform,false);light.transform.localPosition=new Vector3(.2f,1.05f,0);
            lamp=light.GetComponent<Light2D>();lamp.lightType=Light2D.LightType.Point;lamp.color=new Color(1,.71f,.34f);lamp.pointLightOuterRadius=1.65f;lamp.pointLightInnerRadius=.25f;lamp.intensity=.38f;
        }
        void Update(){if(lamp!=null)lamp.intensity=MenuPreferences.ReducedMotion ? .38f : .38f+.025f*Mathf.Sin(Time.unscaledTime*1.7f);}
    }
}
