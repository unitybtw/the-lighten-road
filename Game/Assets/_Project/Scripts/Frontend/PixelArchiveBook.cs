using UnityEngine;
namespace TheLightenRoad.Frontend
{
    /// <summary>Small, authored pixel surfaces. No imported or generated image assets.</summary>
    public static class PixelArchiveBook
    {
        private static Sprite cover, parchment, backing, edges;
        public static Sprite Cover => cover != null ? cover : cover=Draw(0);
        public static Sprite Parchment => parchment != null ? parchment : parchment=Draw(1);
        public static Sprite Backing => backing != null ? backing : backing=Draw(2);
        public static Sprite Edges => edges != null ? edges : edges=Draw(3);
        private static Sprite Draw(int kind)
        {
            const int w=96,h=112;
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Pixel archive book "+kind,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var px=new Color32[w*h];
            Color32 ink=new Color32(28,23,23,255),leather=new Color32(61,42,34,255),lightLeather=new Color32(76,52,39,255),gold=new Color32(161,116,62,255),shine=new Color32(210,166,89,255);
            void Box(int x,int y,int width,int height,Color32 c){for(int j=y;j<y+height;j++)for(int i=x;i<x+width;i++)if(i>=0&&i<w&&j>=0&&j<h)px[j*w+i]=c;}
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int inset=y<3||y>=h-3?6:y<6||y>=h-6?3:1;
                if(x<inset||x>=w-inset)continue;
                int edge=Mathf.Min(x,w-1-x,y,h-1-y);
                int noise=(x*17+y*31+x*y)%29;
                if(kind==0||kind==2)px[y*w+x]=edge<3?ink:edge<5?gold:noise<3?lightLeather:leather;
                else
                {
                    Color32 page=edge<3?new Color32(101,76,47,255):edge<5?new Color32(153,120,76,255):noise<2 && (x<15 || x>82 || y<12 || y>100)?new Color32(190,164,117,255):new Color32(209,184,139,255);
                    if(kind==3 && y%3==0)page=new Color32(123,94,59,255);
                    if(x<9 && kind==1)page=new Color32((byte)(100+x*11),(byte)(77+x*10),(byte)(49+x*8),255);
                    px[y*w+x]=page;
                }
            }
            if(kind==0)
            {
                // Raised spine, worn brass corners, stitched leather panels.
                Box(5,6,6,100,ink);Box(7,7,2,98,lightLeather);
                for(int y=14;y<104;y+=18){Box(4,y,8,3,gold);Box(5,y+2,6,1,shine);}
                foreach(int x in new[]{14,79})for(int y=12;y<101;y+=4)Box(x,y,1,2,gold);
                foreach(int y in new[]{10,99})for(int x=17;x<80;x+=4)Box(x,y,2,1,gold);
                foreach(int x in new[]{12,76})foreach(int y in new[]{8,95})
                {Box(x,y,9,3,gold);Box(x,y,3,9,gold);Box(x+1,y+1,6,1,shine);Box(x+1,y+1,1,6,shine);}
                // Lantern medallion: handle, cage, glass, wick and stepped ember.
                Box(37,39,24,31,ink);Box(39,41,20,27,gold);Box(42,44,14,20,leather);
                Box(42,68,14,3,gold);Box(44,71,10,2,shine);Box(44,72,2,5,gold);Box(52,72,2,5,gold);Box(46,76,6,2,gold);
                Box(45,47,8,3,gold);Box(46,50,6,9,new Color32(224,154,58,255));Box(48,56,3,7,shine);Box(49,58,2,3,new Color32(250,218,139,255));
                Box(40,39,18,2,shine);Box(45,35,8,3,gold);
                // A weathered ribbon peeks beyond the lower cover.
                Box(66,0,7,12,new Color32(94,42,37,255));Box(67,2,2,9,new Color32(130,57,42,255));
                // Tiny embossed symbols keep the cover in the game's own visual language.
                for(int i=0;i<5;i++){Box(34+i*6,87,3,1,gold);Box(34+i*6,84,1,3,gold);}
            }
            if(kind==1)
            {
                // Sparse flecks and tears live in the margins, away from readable copy.
                for(int i=0;i<18;i++){int y=8+i*5;Box(88+(i%3),y,2,1,new Color32(156,122,76,255));}
                Box(90,18,5,2,new Color32(101,76,47,255));Box(87,19,4,1,new Color32(153,120,76,255));
            }
            t.SetPixels32(px);t.Apply(false,true);
            return Sprite.Create(t,new Rect(0,0,w,h),new Vector2(.5f,.5f),16,0,SpriteMeshType.FullRect);
        }
    }
}
