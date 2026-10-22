using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Mechanisms
{
    [RequireComponent(typeof(Light2D))]
    public class FluorescentFlicker : MonoBehaviour
    {
        private Light2D fluorescentLight;

        [Header("Intensity Settings (Þiddet ve Yumuþaklýk)")]
        [SerializeField] private float normalIntensity = 1.0f;
        [SerializeField] private float minIntensity = 0.6f;
        [Tooltip("Geçiþin ne kadar yumuþak olacaðý. Deðer düþtükçe daha soft geçer.")]
        [SerializeField] private float transitionSpeed = 8f;

        [Header("Timing Settings (Zamanlama - Nadirlik)")]
        [Tooltip("Iþýk arýza yapmadan önce NE KADAR SÜRE normal yanacak?")]
        [SerializeField] private float minNormalTime = 4f;
        [SerializeField] private float maxNormalTime = 12f;

        [Tooltip("Arýza baþladýðýnda (titreme krizi) ne kadar sürecek?")]
        [SerializeField] private float minFlickerDuration = 0.5f;
        [SerializeField] private float maxFlickerDuration = 1.5f;

        private float stateTimer;
        private bool isFlickering;
        private float targetIntensity;
        private float flickerTargetTimer;

        private void Start()
        {
            if (!TryGetComponent(out fluorescentLight)) return;

            targetIntensity = normalIntensity;

            
            stateTimer = UnityEngine.Random.Range(minNormalTime, maxNormalTime);
            isFlickering = false;
        }

        private void Update()
        {
            if (fluorescentLight == null) return;

            stateTimer -= Time.deltaTime;

            
            if (stateTimer <= 0f)
            {
                isFlickering = !isFlickering;

                if (isFlickering)
                {
                    
                    stateTimer = UnityEngine.Random.Range(minFlickerDuration, maxFlickerDuration);
                }
                else
                {
                    
                    stateTimer = UnityEngine.Random.Range(minNormalTime, maxNormalTime);
                    targetIntensity = normalIntensity; 
                }
            }

            
            if (isFlickering)
            {
                flickerTargetTimer -= Time.deltaTime;
                if (flickerTargetTimer <= 0f)
                {
                    targetIntensity = UnityEngine.Random.Range(minIntensity, normalIntensity);
                    flickerTargetTimer = UnityEngine.Random.Range(0.05f, 0.15f); 
                }
            }

            
            fluorescentLight.intensity = Mathf.Lerp(fluorescentLight.intensity, targetIntensity, Time.deltaTime * transitionSpeed);
        }
    }
}