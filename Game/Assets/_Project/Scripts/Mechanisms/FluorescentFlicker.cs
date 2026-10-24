using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Mechanisms
{
    public enum FluorescentBehavior
    {
        RealisticFaulty,  // Classic dying fluorescent tube with electrical staccato drops to 0 and rapid ignition strikes
        SubtleHum,        // Stable tube with gentle, occasional micro-dips
        DeadBlink,        // Mostly dark, rare erratic ignition attempts
        Steady            // Rock-solid stable light
    }

    [RequireComponent(typeof(Light2D))]
    public class FluorescentFlicker : MonoBehaviour
    {
        private Light2D fluorescentLight;

        [Header("Behavior Preset")]
        [SerializeField] private FluorescentBehavior behavior = FluorescentBehavior.RealisticFaulty;

        [Header("Intensity Settings (Şiddet ve Yumuşaklık)")]
        [SerializeField] private float normalIntensity = 0.55f;
        [SerializeField] private float minIntensity = 0.0f;
        [Tooltip("Geçişin ne kadar yumuşak olacağı. Değer düştükçe daha soft geçer.")]
        [SerializeField] private float transitionSpeed = 16f;

        [Header("Timing Settings (Zamanlama - Nadirlik)")]
        [Tooltip("Işık arıza yapmadan önce NE KADAR SÜRE normal yanacak?")]
        [SerializeField] private float minNormalTime = 3f;
        [SerializeField] private float maxNormalTime = 9f;

        [Tooltip("Arıza başladığında (titreme krizi) ne kadar sürecek?")]
        [SerializeField] private float minFlickerDuration = 0.4f;
        [SerializeField] private float maxFlickerDuration = 1.4f;

        private float stateTimer;
        private bool isFlickering;
        private float targetIntensity;
        private float flickerTargetTimer;
        private bool isSnapCut;

        public FluorescentBehavior Behavior { get => behavior; set => behavior = value; }
        public float NormalIntensity { get => normalIntensity; set => normalIntensity = value; }
        public float MinIntensity { get => minIntensity; set => minIntensity = value; }
        public float TransitionSpeed { get => transitionSpeed; set => transitionSpeed = value; }
        public float MinNormalTime { get => minNormalTime; set => minNormalTime = value; }
        public float MaxNormalTime { get => maxNormalTime; set => maxNormalTime = value; }
        public float MinFlickerDuration { get => minFlickerDuration; set => minFlickerDuration = value; }
        public float MaxFlickerDuration { get => maxFlickerDuration; set => maxFlickerDuration = value; }

        private void Awake()
        {
            TryGetComponent(out fluorescentLight);
        }

        private void Start()
        {
            if (fluorescentLight == null)
                TryGetComponent(out fluorescentLight);

            if (behavior == FluorescentBehavior.DeadBlink)
            {
                targetIntensity = minIntensity;
                if (fluorescentLight != null)
                    fluorescentLight.intensity = minIntensity;
            }
            else
            {
                targetIntensity = normalIntensity;
            }

            ResetTimerForState(false);
            isFlickering = false;
        }

        private void ResetTimerForState(bool flickering)
        {
            switch (behavior)
            {
                case FluorescentBehavior.Steady:
                    stateTimer = 9999f;
                    break;
                case FluorescentBehavior.SubtleHum:
                    stateTimer = flickering ? UnityEngine.Random.Range(0.2f, 0.5f) : UnityEngine.Random.Range(10f, 22f);
                    break;
                case FluorescentBehavior.DeadBlink:
                    stateTimer = flickering ? UnityEngine.Random.Range(0.25f, 0.7f) : UnityEngine.Random.Range(7f, 16f);
                    break;
                case FluorescentBehavior.RealisticFaulty:
                default:
                    stateTimer = flickering ? UnityEngine.Random.Range(minFlickerDuration, maxFlickerDuration) : UnityEngine.Random.Range(minNormalTime, maxNormalTime);
                    break;
            }
        }

        private void Update()
        {
            if (fluorescentLight == null) return;

            if (behavior == FluorescentBehavior.Steady)
            {
                fluorescentLight.intensity = normalIntensity;
                return;
            }

            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0f)
            {
                isFlickering = !isFlickering;
                ResetTimerForState(isFlickering);

                if (!isFlickering)
                {
                    targetIntensity = behavior == FluorescentBehavior.DeadBlink ? minIntensity : normalIntensity;
                    isSnapCut = false;
                }
            }

            if (isFlickering)
            {
                flickerTargetTimer -= Time.deltaTime;
                if (flickerTargetTimer <= 0f)
                {
                    ApplyFlickerTick();
                }
            }

            // Apply intensity
            if (isSnapCut)
            {
                fluorescentLight.intensity = targetIntensity;
            }
            else
            {
                fluorescentLight.intensity = Mathf.Lerp(fluorescentLight.intensity, targetIntensity, Time.deltaTime * transitionSpeed);
            }
        }

        private void ApplyFlickerTick()
        {
            switch (behavior)
            {
                case FluorescentBehavior.SubtleHum:
                    // Subtle organic dip
                    targetIntensity = normalIntensity * UnityEngine.Random.Range(0.70f, 0.95f);
                    flickerTargetTimer = UnityEngine.Random.Range(0.06f, 0.14f);
                    isSnapCut = false;
                    break;

                case FluorescentBehavior.DeadBlink:
                    // Rapid erratic ignition attempts before falling dark again
                    float rollDead = UnityEngine.Random.value;
                    if (rollDead < 0.65f)
                    {
                        targetIntensity = minIntensity; // total black
                        flickerTargetTimer = UnityEngine.Random.Range(0.03f, 0.08f);
                        isSnapCut = true;
                    }
                    else
                    {
                        targetIntensity = normalIntensity * UnityEngine.Random.Range(0.6f, 1.1f);
                        flickerTargetTimer = UnityEngine.Random.Range(0.02f, 0.06f);
                        isSnapCut = true;
                    }
                    break;

                case FluorescentBehavior.RealisticFaulty:
                default:
                    // Classic dying fluorescent starter tube:
                    // Sharp micro-staccato dropouts to black + bright restrike flashes
                    float roll = UnityEngine.Random.value;
                    if (roll < 0.38f)
                    {
                        // 1. Arc cutout: sudden pitch black
                        targetIntensity = minIntensity;
                        flickerTargetTimer = UnityEngine.Random.Range(0.02f, 0.07f);
                        isSnapCut = true;
                    }
                    else if (roll < 0.72f)
                    {
                        // 2. High-voltage ignition restrike: momentary bright surge
                        targetIntensity = normalIntensity * UnityEngine.Random.Range(0.95f, 1.25f);
                        flickerTargetTimer = UnityEngine.Random.Range(0.025f, 0.06f);
                        isSnapCut = true;
                    }
                    else
                    {
                        // 3. Partial struggling discharge
                        targetIntensity = normalIntensity * UnityEngine.Random.Range(0.25f, 0.55f);
                        flickerTargetTimer = UnityEngine.Random.Range(0.05f, 0.14f);
                        isSnapCut = false;
                    }
                    break;
            }
        }
    }
}
