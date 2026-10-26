using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>Miniature player-lantern artwork inside a stable, accessible slider hit target.</summary>
    public sealed class LanternSliderHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        private Slider slider;
        private RectTransform visual, flame;
        private Image frameImage, flameImage, glow;
        private bool hovered, pressed;
        private float focus;
        private float phase;
        public RectTransform Visual => visual;
        public static LanternSliderHandle Attach(Slider control,RectTransform handle)
        {
            var effect=control.gameObject.AddComponent<LanternSliderHandle>();
            effect.slider=control;control.transition=Selectable.Transition.None;
            effect.phase=(control.GetInstanceID()%23)*.37f;
            effect.glow=Picture("Lantern light",handle,PixelSprites.Glow,new Vector2(54,54),Vector2.zero);
            effect.visual=Rect("Miniature lantern",handle,new Vector2(22,30),Vector2.zero);
            effect.flameImage=Picture("Living flame",effect.visual,PixelSprites.Flame,new Vector2(8,11),new Vector2(0,-1));
            effect.flame=effect.flameImage.rectTransform;
            effect.frameImage=Picture("Player lantern frame",effect.visual,PixelSprites.LanternFrame,new Vector2(22,30),Vector2.zero);
            effect.Refresh(0f);return effect;
        }
        private void Update(){if(slider!=null && visual!=null)Refresh(Time.unscaledDeltaTime);}
        private void Refresh(float dt)
        {
            bool usable=slider.IsInteractable();
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            bool active=usable && (hovered || pressed || selected==gameObject);
            focus=Mathf.MoveTowards(focus,active?1f:0f,dt*10f);
            bool still=MenuPreferences.ReducedMotion;
            float t=Time.unscaledTime+phase;
            float flicker=still?1f:1f+.07f*Mathf.Sin(t*9.3f)+.035f*Mathf.Sin(t*17.7f);
            visual.anchoredPosition=still?Vector2.zero:new Vector2(0,Mathf.Sin(t*2.2f)*.45f);
            visual.localRotation=Quaternion.Euler(0,0,still?0:Mathf.Sin(t*2.7f)*Mathf.Lerp(.7f,2.2f,focus));
            flame.localScale=new Vector3(still?1:1f+.04f*Mathf.Sin(t*11f),flicker,1);
            frameImage.color=usable?Color.Lerp(new Color(.85f,.88f,.91f),Color.white,focus):new Color(.5f,.5f,.5f,.65f);
            flameImage.color=new Color(1f,1f,1f,usable?Mathf.Clamp01(.78f*flicker+focus*.2f):.22f);
            glow.color=new Color(1f,.61f,.16f,usable?Mathf.Lerp(.12f,pressed?.36f:.26f,focus)*flicker:.025f);
            glow.rectTransform.localScale=Vector3.one*(1f+focus*.12f);
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=Vector2.one*.5f;
            rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static Image Picture(string name,Transform parent,Sprite sprite,Vector2 size,Vector2 position)
        {
            var image=Rect(name,parent,size,position).gameObject.AddComponent<Image>();
            image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        public void OnPointerEnter(PointerEventData data){hovered=true;}
        public void OnPointerExit(PointerEventData data){hovered=false;}
        public void OnPointerDown(PointerEventData data){if(data.button==PointerEventData.InputButton.Left)pressed=true;}
        public void OnPointerUp(PointerEventData data){pressed=false;}
        public void OnEndDrag(PointerEventData data){pressed=false;}
        private void OnDisable(){hovered=false;pressed=false;focus=0;}
    }
}
