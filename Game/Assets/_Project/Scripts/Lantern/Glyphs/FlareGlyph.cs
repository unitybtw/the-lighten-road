using TMPro;
using TheLightenRoad.Audio;
using TheLightenRoad.Frontend;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern.Glyphs
{
    /// <summary>
    /// Ancient rune etched into ruin walls or floors that remains invisible in darkness and Dim light,
    /// but charges and glows with radiant luminescent energy when struck by high-intensity Flare light.
    /// Lingers for a configurable duration after Flare ends, optionally revealing secret passages,
    /// triggering puzzle events, or displaying an ancient translated inscription.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class FlareGlyph : MonoBehaviour
    {
        [Header("Glyph Identity & Visuals")]
        [SerializeField] private GlyphType glyphType = GlyphType.EmberEye;
        [SerializeField] private Sprite customSprite;
        [SerializeField] private Color glowColor = new Color(0.31f, 0.95f, 0.82f, 1f); // Ancient cyan-teal
        [SerializeField] private Color dormantColor = new Color(0.18f, 0.22f, 0.24f, 0.06f); // Faint etched stone
        [SerializeField, Min(0.1f)] private float glyphScale = 1.0f;
        [SerializeField] private int sortingOrder = 15;

        [Header("Illumination & Charge Tuning")]
        [Tooltip("Maximum distance from the player's lantern where Flare light can charge this glyph.")]
        [SerializeField, Min(1f)] private float activationDistance = 9.5f;
        [Tooltip("Rate per second at which the glyph absorbs Flare light to reach full illumination.")]
        [SerializeField, Min(0.1f)] private float chargeSpeed = 1.35f;
        [Tooltip("Seconds the glyph remains fully illuminated after Flare light leaves it.")]
        [SerializeField, Min(0.2f)] private float lingerDuration = 3.5f;
        [Tooltip("Rate per second at which the glyph fades back to dormant after linger expires.")]
        [SerializeField, Min(0.1f)] private float dischargeSpeed = 0.55f;
        [Tooltip("Minimum FlareBlend required to charge the glyph (prevents low dim light from triggering).")]
        [SerializeField, Range(0.1f, 1f)] private float minFlareThreshold = 0.35f;

        [Header("Local 2D Light Glow")]
        [SerializeField] private bool enableLocalLight = true;
        [SerializeField, Min(0.5f)] private float localLightRadius = 3.2f;
        [SerializeField, Range(0f, 2f)] private float localLightIntensity = 0.95f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip activationSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.65f;

        [Header("Translated Inscription (Optional)")]
        [TextArea(1, 3)]
        [SerializeField] private string inscriptionText;
        [SerializeField, Min(1f)] private float inscriptionReadDistance = 4.2f;

        [Header("Events")]
        public UnityEvent<float> OnChargeChanged = new UnityEvent<float>();
        public UnityEvent OnFullyCharged = new UnityEvent();
        public UnityEvent OnExtinguished = new UnityEvent();

        private SpriteRenderer spriteRenderer;
        private Light2D localLight;
        private LanternController targetLantern;
        private GameObject inscriptionObject;
        private TextMeshPro inscriptionLabel;
        private CanvasGroup inscriptionCanvasGroup;

        private float chargeLevel;
        private float lingerTimer;
        private bool isFullyCharged;
        private bool isReceivingFlareLight;

        public GlyphType Type { get => glyphType; set { glyphType = value; ApplySprite(); } }
        public float ChargeLevel => chargeLevel;
        public bool IsFullyCharged => isFullyCharged;
        public bool IsReceivingFlareLight => isReceivingFlareLight;
        public float ActivationDistance => activationDistance;
        public string InscriptionText { get => inscriptionText; set { inscriptionText = value; UpdateInscription(); } }

        private void Awake()
        {
            EnsureComponents();
            ApplySprite();
            BuildInscription();
        }

        private void Start()
        {
            if (targetLantern == null)
                FindPlayerLantern();
        }

        private void OnEnable()
        {
            EnsureComponents();
            ApplyVisuals();
        }

        private void Update()
        {
            if (targetLantern == null || !targetLantern.gameObject.activeInHierarchy)
            {
                FindPlayerLantern();
                if (targetLantern == null) return;
            }

            CheckIllumination();
            UpdateCharge();
            ApplyVisuals();
        }

        private void EnsureComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingOrder = sortingOrder;

            if (enableLocalLight && localLight == null)
            {
                var lightChild = transform.Find("GlyphLight");
                if (lightChild != null)
                {
                    localLight = lightChild.GetComponent<Light2D>();
                }
                else
                {
                    var lightObj = new GameObject("GlyphLight");
                    lightObj.transform.SetParent(transform, false);
                    localLight = lightObj.AddComponent<Light2D>();
                    localLight.lightType = Light2D.LightType.Point;
                    localLight.color = glowColor;
                    localLight.pointLightOuterRadius = localLightRadius;
                    localLight.pointLightInnerRadius = localLightRadius * 0.2f;
                    localLight.intensity = 0f;
                }
            }
        }

        private void ApplySprite()
        {
            if (spriteRenderer == null) return;

            if (glyphType == GlyphType.Custom && customSprite != null)
            {
                spriteRenderer.sprite = customSprite;
            }
            else
            {
                spriteRenderer.sprite = GlyphSprites.GetSprite(glyphType);
            }

            transform.localScale = Vector3.one * glyphScale;
        }

        private void FindPlayerLantern()
        {
            targetLantern = LanternController.ActiveInstance ?? Object.FindAnyObjectByType<LanternController>();
        }

        public void BindLantern(LanternController lantern)
        {
            targetLantern = lantern;
        }

        private void CheckIllumination()
        {
            if (targetLantern == null || !targetLantern.gameObject.activeInHierarchy)
            {
                isReceivingFlareLight = false;
                return;
            }

            float dist = Vector2.Distance(transform.position, targetLantern.transform.position);
            bool inRange = dist <= activationDistance;
            bool isFlaring = targetLantern.IsFlared || targetLantern.FlareBlend >= minFlareThreshold || targetLantern.HasActiveScout;

            isReceivingFlareLight = inRange && isFlaring;
        }

        private void UpdateCharge()
        {
            float dt = Time.deltaTime;
            float prevCharge = chargeLevel;

            if (isReceivingFlareLight)
            {
                lingerTimer = lingerDuration;
                chargeLevel = Mathf.MoveTowards(chargeLevel, 1f, chargeSpeed * dt);

                if (chargeLevel >= 1f && !isFullyCharged)
                {
                    isFullyCharged = true;
                    PlayActivationFeedback();
                    OnFullyCharged.Invoke();
                }
            }
            else
            {
                if (lingerTimer > 0f)
                {
                    lingerTimer -= dt;
                }
                else
                {
                    chargeLevel = Mathf.MoveTowards(chargeLevel, 0f, dischargeSpeed * dt);

                    if (chargeLevel <= 0f && isFullyCharged)
                    {
                        isFullyCharged = false;
                        OnExtinguished.Invoke();
                    }
                }
            }

            if (!Mathf.Approximately(prevCharge, chargeLevel))
            {
                OnChargeChanged.Invoke(chargeLevel);
            }
        }

        private void ApplyVisuals()
        {
            if (spriteRenderer == null) return;

            bool reduced = MenuPreferences.ReducedMotion;
            float pulse = (!reduced && chargeLevel > 0.75f) ? (Mathf.Sin(Time.time * 3.5f) * 0.08f) : 0f;

            Color targetColor = Color.Lerp(dormantColor, glowColor, chargeLevel);
            targetColor.a = Mathf.Clamp01(targetColor.a + pulse);
            spriteRenderer.color = targetColor;

            if (localLight != null)
            {
                localLight.color = glowColor;
                localLight.intensity = localLightIntensity * chargeLevel * (1f + pulse * 0.5f);
                localLight.pointLightOuterRadius = Mathf.Lerp(0.3f, localLightRadius, chargeLevel);
                localLight.pointLightInnerRadius = localLight.pointLightOuterRadius * 0.2f;
            }

            UpdateInscriptionVisibility();
        }

        private void BuildInscription()
        {
            if (string.IsNullOrWhiteSpace(inscriptionText)) return;

            var existing = transform.Find("InscriptionDisplay");
            if (existing != null)
            {
                inscriptionObject = existing.gameObject;
                inscriptionLabel = inscriptionObject.GetComponentInChildren<TextMeshPro>();
                return;
            }

            inscriptionObject = new GameObject("InscriptionDisplay");
            inscriptionObject.transform.SetParent(transform, false);
            inscriptionObject.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            inscriptionLabel = inscriptionObject.AddComponent<TextMeshPro>();
            inscriptionLabel.text = inscriptionText;
            inscriptionLabel.fontSize = 2.4f;
            inscriptionLabel.color = new Color(0.92f, 0.94f, 0.92f, 0f);
            inscriptionLabel.alignment = TextAlignmentOptions.Center;
            inscriptionLabel.sortingOrder = sortingOrder + 5;
            inscriptionLabel.rectTransform.sizeDelta = new Vector2(8f, 2f);
        }

        private void UpdateInscription()
        {
            if (inscriptionLabel != null)
            {
                inscriptionLabel.text = inscriptionText;
            }
            else if (!string.IsNullOrWhiteSpace(inscriptionText))
            {
                BuildInscription();
            }
        }

        private void UpdateInscriptionVisibility()
        {
            if (inscriptionLabel == null || string.IsNullOrWhiteSpace(inscriptionText)) return;

            float dist = targetLantern != null ? Vector2.Distance(transform.position, targetLantern.transform.position) : 99f;
            bool canRead = chargeLevel > 0.45f && dist <= inscriptionReadDistance;

            float targetAlpha = canRead ? Mathf.InverseLerp(0.45f, 0.9f, chargeLevel) : 0f;
            Color c = inscriptionLabel.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, Time.deltaTime * 3f);
            inscriptionLabel.color = c;
        }

        private void PlayActivationFeedback()
        {
            AudioClip clipToPlay = activationSound != null ? activationSound : Resources.Load<AudioClip>("Audio/SFX/Environment/crystal_activate");
            if (clipToPlay != null && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayOneShot(clipToPlay, soundVolume);
            }
        }

        public void ForceCharge(float level = 1f)
        {
            chargeLevel = Mathf.Clamp01(level);
            lingerTimer = lingerDuration;
            if (chargeLevel >= 1f && !isFullyCharged)
            {
                isFullyCharged = true;
                OnFullyCharged.Invoke();
            }
            ApplyVisuals();
        }

        public void ForceDischarge()
        {
            chargeLevel = 0f;
            lingerTimer = 0f;
            if (isFullyCharged)
            {
                isFullyCharged = false;
                OnExtinguished.Invoke();
            }
            ApplyVisuals();
        }



        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.31f, 0.95f, 0.82f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, activationDistance);
            if (!string.IsNullOrWhiteSpace(inscriptionText))
            {
                Gizmos.color = new Color(1f, 0.92f, 0.45f, 0.25f);
                Gizmos.DrawWireSphere(transform.position, inscriptionReadDistance);
            }
        }
    }
}
