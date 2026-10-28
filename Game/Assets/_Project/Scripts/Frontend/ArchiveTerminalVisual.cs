using UnityEngine;
namespace TheLightenRoad.Frontend
{
    /// <summary>Code-built terminal silhouette, independent of puzzle machinery.</summary>
    public sealed class ArchiveTerminalVisual : MonoBehaviour
    {
        private SpriteRenderer ember;
        static Sprite square;
        void Awake()
        {
            if(square==null)square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);
            Piece("Archive plinth",new Vector2(0,-.35f),new Vector2(.65f,.95f),new Color(.12f,.14f,.14f));
            Piece("Brass reading desk",new Vector2(0,.2f),new Vector2(.85f,.12f),new Color(.62f,.43f,.2f));
            ember=Piece("Terminal ember",new Vector2(0,.4f),new Vector2(.18f,.24f),new Color(1,.72f,.25f));
        }
        SpriteRenderer Piece(string name,Vector2 pos,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(transform,false);go.transform.localPosition=pos;go.transform.localScale=size;
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=square;sr.color=color;sr.sortingOrder=12;return sr;
        }
        void Update(){if(ember!=null)ember.color=new Color(1,.7f,.25f,.75f+.2f*Mathf.Sin(Time.unscaledTime*2));}
    }
}
