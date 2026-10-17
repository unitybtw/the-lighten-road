using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace TheLightenRoad.Frontend
{
    public sealed class MenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public RectTransform Caption;
        public Image Rule;
        public Image Marker;
        public TMP_Text Text;
        public Color Rest = new Color32(155, 151, 135, 255);
        public Color Active = new Color32(232, 211, 167, 255);
        private bool hovered, selected;
        private float amount;
        public void OnPointerEnter(PointerEventData data) => hovered = true;
        public void OnPointerExit(PointerEventData data) => hovered = false;
        public void OnSelect(BaseEventData data) => selected = true;
        public void OnDeselect(BaseEventData data) => selected = false;
        private void OnDisable() { hovered = selected = false; amount = 0f; }
        private void Update()
        {
            float wanted = hovered || selected ? 1f : 0f;
            amount = MenuPreferences.ReducedMotion ? wanted : Mathf.MoveTowards(amount, wanted, Time.unscaledDeltaTime * 5f);
            if (Caption != null) Caption.anchoredPosition = new Vector2(32f + 8f * amount, 0f);
            if (Text != null) Text.color = Color.Lerp(Rest, Active, amount);
            if (Rule != null) { Rule.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 190f, amount), 1f); Rule.color = new Color(0.66f, 0.49f, 0.27f, amount * 0.75f); }
            if (Marker != null) { Marker.color = new Color(0.85f, 0.62f, 0.32f, 0.35f + 0.65f * amount); Marker.rectTransform.localEulerAngles = new Vector3(0, 0, 45); }
        }
    }
}
