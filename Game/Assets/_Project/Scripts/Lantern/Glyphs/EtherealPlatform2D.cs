using TheLightenRoad.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern.Glyphs
{
    /// <summary>
    /// Ancient ethereal archive platform that remains translucent and non-solid in darkness,
    /// but energizes and solidifies into physical stone when touched by the player's lantern light.
    /// Dim light solidifies nearby platforms; Flare light reaches platforms across wide chasms.
    /// When light departs, the platform retains solidity for a grace decay period before dissolving.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class EtherealPlatform2D : MonoBehaviour
    {
        [Header("Platform Visuals")]
        [SerializeField] private Sprite customSprite;
        [SerializeField] private Vector2 platformSize = new Vector2(3.5f, 0.6f);
        [SerializeField] private Color solidColor = new Color(0.96f, 0.93f, 0.85f, 1.0f); // Solid radiant ivory stone
        [SerializeField] private Color etherealColor = new Color(0.40f, 0.58f, 0.85f, 0.22f); // Translucent midnight ghost
        [SerializeField] private Color decayWarningColor = new Color(1.0f, 0.68f, 0.25f, 0.85f); // Flickering amber warning
        [SerializeField] private int sortingOrder = 10;

        [Header("Illumination & Solidification Tuning")]
        [Tooltip("Max distance from player lantern in Dim mode to solidify this platform.")]
        [SerializeField, Min(1f)] private float dimActivationDistance = 3.4f;
        [Tooltip("Max distance from player lantern in Flare mode to solidify this platform.")]
        [SerializeField, Min(1f)] private float flareActivationDistance = 8.5f;
        [Tooltip("Seconds the platform remains solid after lantern light leaves.")]
        [SerializeField, Min(0.2f)] private float lingerDuration = 2.0f;
        [Tooltip("Additional seconds granted when energized by high-intensity Flare.")]
        [SerializeField, Min(0f)] private float flareLingerBonus = 1.5f;
        [Tooltip("Speed of transition between ethereal and solid states.")]
        [SerializeField, Min(1f)] private float transitionSpeed = 5.0f;

        [Header("Collision & Physics")]
        [Tooltip("Enable one-way collision so the player can jump through from beneath and land on top.")]
        [SerializeField] private bool useOneWayPlatform = true;

        [Header("Local 2D Glow")]
        [SerializeField] private bool enableLocalLight = true;
        [SerializeField, Min(0.5f)] private float localLightRadius = 2.8f;
        [SerializeField, Range(0f, 2f)] private float localLightIntensity = 0.85f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip solidifySound;
        [SerializeField] private AudioClip dissolveSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.65f;

        [Header("Events")]
        public UnityEvent OnSolidified = new UnityEvent();
        public UnityEvent OnDecaying = new UnityEvent();
        public UnityEvent OnDissolved = new UnityEvent();
        public UnityEvent<bool> OnSolidStateChanged = new UnityEvent<bool>();

        private SpriteRenderer spriteRenderer;
        private BoxCollider2D boxCollider;
        private PlatformEffector2D effector;
        private Light2D localLight;
        private LanternController targetLantern;

        private float solidBlend;
        private float lingerTimer;
        private bool isSolid;
        private bool isDecaying;
        private bool isIlluminated;
        private bool wasIlluminatedByFlare;
        private float forceSolidTimer;

        private static Sprite cachedDefaultPlatformSprite;
        private static AudioClip proceduralChimeClip;

        public bool IsSolid => isSolid;
        public bool IsDecaying => isDecaying;
        public bool IsIlluminated => isIlluminated;
        public float SolidBlend => solidBlend;
        public float LingerTimer => lingerTimer;
        public Vector2 PlatformSize => platformSize;

        private void Awake()
        {
            EnsureComponents();
            ApplySprite();
        }

        private void Start()
        {
            if (targetLantern == null)
            {
                FindPlayerLantern();
            }
        }

        private void OnEnable()
        {
            EnsureComponents();
            UpdateVisuals(instant: true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (targetLantern == null)
            {
                FindPlayerLantern();
            }

            CheckIllumination();

            // Handle solid state & linger decay logic
            if (forceSolidTimer > 0f)
            {
                forceSolidTimer = Mathf.Max(0f, forceSolidTimer - dt);
                isIlluminated = true;
            }

            if (isIlluminated)
            {
                lingerTimer = lingerDuration + (wasIlluminatedByFlare ? flareLingerBonus : 0f);
                if (isDecaying)
                {
                    isDecaying = false;
                }
                if (!isSolid)
                {
                    SetSolidState(true);
                }
            }
            else if (isSolid)
            {
                if (lingerTimer > 0f)
                {
                    lingerTimer -= dt;
                    if (!isDecaying && lingerTimer <= 1.2f)
                    {
                        isDecaying = true;
                        OnDecaying.Invoke();
                    }
                }
                else
                {
                    SetSolidState(false);
                }
            }

            // Smooth visual blend interpolation
            float targetBlend = isSolid ? 1f : 0f;
            solidBlend = Mathf.MoveTowards(solidBlend, targetBlend, dt * transitionSpeed);

            UpdateVisuals(instant: false);
        }

        private void CheckIllumination()
        {
            if (targetLantern == null || !targetLantern.isActiveAndEnabled)
            {
                isIlluminated = false;
                return;
            }

            float dist = Vector2.Distance(transform.position, targetLantern.transform.position);
            bool isFlare = targetLantern.IsFlared;
            float maxDist = isFlare ? flareActivationDistance : dimActivationDistance;

            if (dist <= maxDist)
            {
                isIlluminated = true;
                if (isFlare)
                {
                    wasIlluminatedByFlare = true;
                }
            }
            else
            {
                isIlluminated = false;
            }
        }

        private void SetSolidState(bool solid)
        {
            if (isSolid == solid) return;
            isSolid = solid;

            if (boxCollider != null)
            {
                boxCollider.enabled = isSolid;
            }

            if (isSolid)
            {
                PlaySound(solidifySound, 587.33f, 880f); // D5 -> A5 ascending crystalline chime
                OnSolidified.Invoke();
            }
            else
            {
                isDecaying = false;
                wasIlluminatedByFlare = false;
                PlaySound(dissolveSound, 440f, 220f); // Descending whisper
                OnDissolved.Invoke();
            }

            OnSolidStateChanged.Invoke(isSolid);
        }

        public void ForceSolidify(float duration)
        {
            forceSolidTimer = Mathf.Max(forceSolidTimer, duration);
            SetSolidState(true);
        }

        public void DissolveImmediate()
        {
            forceSolidTimer = 0f;
            lingerTimer = 0f;
            isIlluminated = false;
            SetSolidState(false);
            solidBlend = 0f;
            UpdateVisuals(instant: true);
        }

        private void UpdateVisuals(bool instant)
        {
            if (spriteRenderer == null) return;

            Color targetCol;
            if (isSolid)
            {
                if (isDecaying)
                {
                    // Fast warning pulse when decaying
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                    targetCol = Color.Lerp(solidColor, decayWarningColor, pulse);
                }
                else
                {
                    targetCol = solidColor;
                }
            }
            else
            {
                // Soft ghost breathing pulse when ethereal
                float breath = 0.05f * Mathf.Sin(Time.time * 2.5f);
                targetCol = etherealColor;
                targetCol.a = Mathf.Clamp01(etherealColor.a + breath);
            }

            spriteRenderer.color = Color.Lerp(etherealColor, targetCol, instant ? (isSolid ? 1f : 0f) : solidBlend);

            if (localLight != null)
            {
                localLight.enabled = enableLocalLight && (solidBlend > 0.05f);
                localLight.intensity = solidBlend * localLightIntensity;
            }
        }

        private void EnsureComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingOrder = sortingOrder;

            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider2D>();
                if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            boxCollider.size = platformSize;
            boxCollider.enabled = isSolid;

            var surfaceProvider = GetComponent<SurfaceAudioTypeProvider>();
            if (surfaceProvider == null)
            {
                surfaceProvider = gameObject.AddComponent<SurfaceAudioTypeProvider>();
                surfaceProvider.SurfaceType = SurfaceType.Ethereal;
            }

            if (useOneWayPlatform)
            {
                boxCollider.usedByEffector = true;
                if (effector == null)
                {
                    effector = GetComponent<PlatformEffector2D>();
                    if (effector == null) effector = gameObject.AddComponent<PlatformEffector2D>();
                }
                effector.useOneWay = true;
                effector.surfaceArc = 160f;
            }
            else
            {
                boxCollider.usedByEffector = false;
            }

            if (enableLocalLight && localLight == null)
            {
                var lightChild = transform.Find("EtherealLight");
                GameObject lightGo = lightChild != null ? lightChild.gameObject : new GameObject("EtherealLight");
                lightGo.transform.SetParent(transform, false);

                localLight = lightGo.GetComponent<Light2D>();
                if (localLight == null) localLight = lightGo.AddComponent<Light2D>();

                localLight.lightType = Light2D.LightType.Point;
                localLight.pointLightOuterRadius = localLightRadius;
                localLight.color = new Color(1.0f, 0.74f, 0.35f, 1f);
                localLight.intensity = 0f;
                localLight.enabled = false;
            }
        }

        private void ApplySprite()
        {
            if (spriteRenderer == null) return;

            if (customSprite != null)
            {
                spriteRenderer.sprite = customSprite;
            }
            else
            {
                if (cachedDefaultPlatformSprite == null)
                {
                    cachedDefaultPlatformSprite = GeneratePlatformSprite(128, 24);
                }
                spriteRenderer.sprite = cachedDefaultPlatformSprite;
            }

            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = platformSize;
        }

        private static Sprite GeneratePlatformSprite(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var colors = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x == 0 || x == w - 1 || y == 0 || y == h - 1);
                    bool isTopRim = (y >= h - 3);
                    bool isRuneMark = ((x % 16 == 0 || (x + y) % 18 == 0) && y > 3 && y < h - 3);

                    if (isBorder)
                    {
                        colors[y * w + x] = new Color(1f, 1f, 1f, 0.95f);
                    }
                    else if (isTopRim)
                    {
                        colors[y * w + x] = new Color(1f, 1f, 1f, 0.85f);
                    }
                    else if (isRuneMark)
                    {
                        colors[y * w + x] = new Color(1f, 1f, 1f, 0.70f);
                    }
                    else
                    {
                        colors[y * w + x] = new Color(1f, 1f, 1f, 0.40f);
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f, 1, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        }

        private void FindPlayerLantern()
        {
            targetLantern = FindFirstObjectByType<LanternController>();
        }

        private void PlaySound(AudioClip clip, float freq1, float freq2)
        {
            AudioClip sound = clip != null ? clip : GetProceduralCrystalClip(freq1, freq2);
            if (sound != null && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayOneShot(sound, soundVolume * SoundEffectManager.Instance.MasterSFXVolume);
            }
        }

        private static AudioClip GetProceduralCrystalClip(float freq1, float freq2)
        {
            if (proceduralChimeClip != null) return proceduralChimeClip;

            const int sampleRate = 44100;
            const float duration = 0.35f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 11f);
                float f = Mathf.Lerp(freq1, freq2, t / duration);
                // Dual harmonic chime
                float wave = Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.3f;
                data[i] = wave * env * 0.3f;
            }

            proceduralChimeClip = AudioClip.Create("EtherealChime", samples, 1, sampleRate, false);
            proceduralChimeClip.SetData(data, 0);
            return proceduralChimeClip;
        }
    }
}
