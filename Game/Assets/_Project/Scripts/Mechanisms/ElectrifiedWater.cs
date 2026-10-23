using UnityEngine;

namespace TheLightenRoad.Mechanisms
{
    public class ElectrifiedWater : MonoBehaviour
    {
        [Header("Water Components")]
        [Tooltip("Siraç'ın HazardTrigger2D bileşenini içeren alt obje.")]
        public GameObject hazardZone;
        public SpriteRenderer waterRenderer;

        [Header("Visual Feedback")]
        public Color safeColor = new Color(0.2f, 0.4f, 0.8f, 0.8f); // Koyu sakin mavi
        public Color electrifiedColor = new Color(0.9f, 0.8f, 0.2f, 0.8f); // Tehlikeli parlak sarı/yeşil

        private void Start()
        {
            // Oyun başladığında su tehlikeli durumdadır
            Electrify();
        }

        public void Electrify()
        {
            // Suyu ölümcül yap ve rengini değiştir (Güç çekildiğinde çalışır)
            if (hazardZone != null) hazardZone.SetActive(true);
            if (waterRenderer != null) waterRenderer.color = electrifiedColor;
            Debug.Log("Water is now ELECTRIFIED!");
        }

        public void Purify()
        {
            // Elektriği kes, suyun içinden geçilebilir (Güç verildiğinde çalışır)
            if (hazardZone != null) hazardZone.SetActive(false);
            if (waterRenderer != null) waterRenderer.color = safeColor;
            Debug.Log("Water is safe to cross.");
        }
    }
}