using UnityEngine;

namespace TheLightenRoad.Mechanisms
{
    public class ElectrifiedWater : MonoBehaviour
    {
        [Header("Water Components")]
        [Tooltip("Siraç'ýn HazardTrigger2D bileþenini içeren alt obje.")]
        public GameObject hazardZone;
        public SpriteRenderer waterRenderer;

        [Header("Visual Feedback")]
        public Color safeColor = new Color(0.2f, 0.4f, 0.8f, 0.8f); // Koyu sakin mavi
        public Color electrifiedColor = new Color(0.9f, 0.8f, 0.2f, 0.8f); // Tehlikeli parlak sarý/yeþil

        private void Start()
        {
            // Oyun baþladýðýnda su tehlikeli durumdadýr
            Electrify();
        }

        public void Electrify()
        {
            // Suyu ölümcül yap ve rengini deðiþtir (Güç çekildiðinde çalýþýr)
            if (hazardZone != null) hazardZone.SetActive(true);
            if (waterRenderer != null) waterRenderer.color = electrifiedColor;
            Debug.Log("Water is now ELECTRIFIED!");
        }

        public void Purify()
        {
            // Elektriði kes, suyun içinden geçilebilir (Güç verildiðinde çalýþýr)
            if (hazardZone != null) hazardZone.SetActive(false);
            if (waterRenderer != null) waterRenderer.color = safeColor;
            Debug.Log("Water is safe to cross.");
        }
    }
}