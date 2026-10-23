using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>Shared lantern-lit focus for settings controls; never changes layout or control values.</summary>
    public sealed class SettingsLightFocus : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public Image Glow;
        public TMP_Text Label;
        public bool IsCurrent;
        private bool hovered;
        private float blend;
        private Selectable control;
        private void Awake()=>control=GetComponent<Selectable>();
        public void OnPointerEnter(PointerEventData data)=>hovered=true;
        public void OnPointerExit(PointerEventData data)=>hovered=false;
        private void OnDisable(){hovered=false;blend=0;}
        private void Update()
        {
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            bool focus=hovered || IsCurrent || (selected!=null && selected.transform.IsChildOf(transform));
            bool enabled=control==null || control.IsInteractable();
            blend=Mathf.MoveTowards(blend,focus && enabled?1:0,Time.unscaledDeltaTime*7);
            float pulse=MenuPreferences.ReducedMotion?1f:.94f+.06f*Mathf.Sin(Time.unscaledTime*2.2f);
            if(Glow!=null)Glow.color=new Color(1,.65f,.25f,blend*.14f*pulse);
            if(Label!=null)Label.color=!enabled?new Color(.58f,.56f,.50f,.8f):Color.Lerp(new Color(.80f,.80f,.75f),new Color(1,.80f,.45f),blend);
        }
    }
}
