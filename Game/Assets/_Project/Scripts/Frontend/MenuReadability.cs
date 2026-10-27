using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>Enlarges menu content while fitting the available safe canvas, independently of entrance animations.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuReadability : MonoBehaviour
    {
        public Vector2 ContentSize = new Vector2(1260f, 820f);
        private RectTransform rect;
        private void Awake() => rect = (RectTransform)transform;
        private void LateUpdate()
        {
            if (rect == null) return;
            var canvas = GetComponentInParent<Canvas>();
            var bounds = canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.size : ContentSize;
            // Respect a smaller safe-area ancestor without inheriting animated local scales.
            for (var parent = rect.parent as RectTransform; parent != null; parent = parent.parent as RectTransform)
                if (parent.name == "Safe area") bounds = Vector2.Min(bounds, parent.rect.size);
            rect.localScale = Vector3.one * FitScale(bounds, ContentSize);
        }
        public static float FitScale(Vector2 available, Vector2 content)
        {
            if (available.x <= 0 || available.y <= 0 || content.x <= 0 || content.y <= 0) return 1f;
            return Mathf.Min(1.2f, available.x * .9f / content.x, available.y * .9f / content.y);
        }
        public static void EnlargeText(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontSize = Mathf.Max(20f, text.fontSize);
                text.enableAutoSizing = false;
                var r = text.rectTransform;
                if (r.anchorMin.y == r.anchorMax.y && r.sizeDelta.y > 0 && r.sizeDelta.y < 30)
                    r.sizeDelta = new Vector2(r.sizeDelta.x, 30);
            }
        }
        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchoredPosition = position; rect.sizeDelta = size; }
        public static void Row(RectTransform row)
        {
            row.sizeDelta = new Vector2(900,76);
            var title = row.Find("Title")?.GetComponent<TMP_Text>();
            var subtitle = row.Find("Subtitle")?.GetComponent<TMP_Text>();
            Label(title, new Vector2(16,14), new Vector2(500,32),24);
            Label(subtitle,new Vector2(16,-17),new Vector2(500,30),20);
            var plate = row.Find("Row Plate")?.GetComponent<Image>();
            if (plate != null) plate.raycastTarget = row.GetComponent<Toggle>() != null;
            var slider = row.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                var hit = slider.GetComponent<Image>();
                if (hit == null) hit = slider.gameObject.AddComponent<Image>();
                hit.color = Color.clear; hit.raycastTarget = true;
                var s = (RectTransform)slider.transform;
                s.anchorMin = s.anchorMax = new Vector2(1,.5f); s.pivot = new Vector2(1,.5f);
                Place(s,new Vector2(-16,0),new Vector2(350,56));
                var track = s.Find("Track") as RectTransform;
                if (track != null) track.sizeDelta = new Vector2(260,track.sizeDelta.y);
                var value = s.Find("Value")?.GetComponent<TMP_Text>();
                if (value != null) { value.fontSize=20; value.rectTransform.sizeDelta=new Vector2(76,32); }
            }
            var pill = row.Find("Pill") as RectTransform;
            if (pill != null)
            {
                pill.anchorMin = pill.anchorMax = new Vector2(1,.5f); pill.pivot = new Vector2(1,.5f);
                Place(pill,new Vector2(-16,0),new Vector2(160,52));
                var value=pill.GetComponentInChildren<TMP_Text>(); if(value!=null)value.fontSize=20;
            }
        }
        private static void Label(TMP_Text label, Vector2 pos, Vector2 size, float font)
        {
            if(label==null)return;
            var r=label.rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,.5f);r.pivot=new Vector2(0,.5f);
            Place(r,pos,size);label.fontSize=font;label.characterSpacing=.5f;
        }
        public static void RefreshFocus(Transform root)
        {
            foreach(var focus in root.GetComponentsInChildren<SettingsLightFocus>(true))
            {
                var rect=(RectTransform)focus.transform;
                focus.PointerOffset=rect.rect.width*.5f+24;
                if(focus.Glow!=null)focus.Glow.rectTransform.sizeDelta=rect.sizeDelta+new Vector2(60,36);
            }
        }
        // Dense device tabs retain their focus glow without decorative wings crossing adjacent labels.
        public static void HideFocusWings(Button button)
        {
            var focus=button.GetComponent<SettingsLightFocus>();
            if(focus!=null)
            {
                if(focus.LeftPointer!=null)focus.LeftPointer.gameObject.SetActive(false);
                if(focus.RightPointer!=null)focus.RightPointer.gameObject.SetActive(false);
            }
            var wings=button.transform.Find("Wings Container");if(wings!=null)wings.gameObject.SetActive(false);
        }
        public static void CompactDeviceTab(Button button)
        {
            HideFocusWings(button);
            if(button.transform.Find("Selection rule")!=null)return;
            var marker=new GameObject("Selection rule",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            marker.transform.SetParent(button.transform,false);
            var rect=(RectTransform)marker.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,.5f);
            Place(rect,new Vector2(0,5),new Vector2(90,2.5f));
            var image=marker.GetComponent<Image>();image.color=new Color(1f,.75f,.3f,.9f);image.raycastTarget=false;
            marker.SetActive(false);
        }
        public static void Footer(RectTransform footer)
        {
            footer.sizeDelta=new Vector2(1040,44);
            string[] names={"Slot_Select","Slot_Back","Slot_Reset","Slot_Tab"};
            for(int i=0;i<names.Length;i++)
            {
                var slot=footer.Find(names[i]) as RectTransform;if(slot==null)continue;
                Place(slot,new Vector2(-390+i*260,0),new Vector2(240,44));
                var label=slot.Find("Label") as RectTransform;
                if(label!=null)label.sizeDelta=new Vector2(210,32);
            }
        }
    }
}
