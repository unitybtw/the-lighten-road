using UnityEngine;

namespace TheLightenRoad.Audio
{
    /// <summary>
    /// Manages atmospheric background music with gentle volume, smooth fade-in,
    /// seamless 2D looping, and non-intrusive ambient presence.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class BackgroundMusicManager : MonoBehaviour
    {
        public static BackgroundMusicManager Instance { get; private set; }

        [Header("Audio Asset")]
        [Tooltip("Atmospheric background music clip.")]
        [SerializeField] private AudioClip musicClip;

        [Header("Volume & Presence")]
        [Tooltip("Resting background volume (kept subtle and quiet to avoid overwhelming the player).")]
        [SerializeField, Range(0.01f, 1f)] private float targetVolume = 0.20f;
        [Tooltip("Duration in seconds for the initial smooth fade-in.")]
        [SerializeField, Min(0.1f)] private float fadeInDuration = 2.5f;
        [Tooltip("Duration in seconds when fading out.")]
        [SerializeField, Min(0.1f)] private float fadeOutDuration = 1.5f;

        [Header("Playback Behavior")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = true;

        private AudioSource audioSource;
        private float currentFadeVolume;
        private float desiredVolume;
        private float fadeRate;
        private bool isFading;

        public AudioSource Source => audioSource != null ? audioSource : audioSource = GetComponent<AudioSource>();
        public AudioClip CurrentClip => musicClip;
        public float TargetVolume
        {
            get => targetVolume;
            set
            {
                targetVolume = Mathf.Clamp01(value);
                desiredVolume = targetVolume;
            }
        }
        public float CurrentVolume => audioSource != null ? audioSource.volume : 0f;
        public bool IsPlaying => audioSource != null && audioSource.isPlaying;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSource();
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            TickFade(Time.unscaledDeltaTime);
        }

        public void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.playOnAwake = false;
            audioSource.loop = loop;
            audioSource.spatialBlend = 0f; // Pure 2D stereo across listener
            audioSource.bypassListenerEffects = false;

            if (musicClip == null)
            {
                musicClip = Resources.Load<AudioClip>("Audio/AmbientRuinsBGM");
            }

            if (musicClip != null && audioSource.clip == null)
            {
                audioSource.clip = musicClip;
            }
        }

        public void Play(AudioClip clip = null, float customFadeDuration = -1f)
        {
            EnsureAudioSource();

            if (clip != null && clip != musicClip)
            {
                musicClip = clip;
                audioSource.clip = musicClip;
            }

            if (audioSource.clip == null)
            {
                return;
            }

            float duration = customFadeDuration >= 0f ? customFadeDuration : fadeInDuration;

            if (!audioSource.isPlaying)
            {
                audioSource.volume = 0f;
                currentFadeVolume = 0f;
                audioSource.Play();
            }

            desiredVolume = targetVolume;
            fadeRate = duration > 0.01f ? (targetVolume / duration) : 100f;
            isFading = true;
        }

        public void Stop(float customFadeDuration = -1f)
        {
            if (audioSource == null || !audioSource.isPlaying) return;

            float duration = customFadeDuration >= 0f ? customFadeDuration : fadeOutDuration;
            desiredVolume = 0f;
            fadeRate = duration > 0.01f ? (Mathf.Max(0.01f, audioSource.volume) / duration) : 100f;
            isFading = true;
        }

        public void FadeTo(float target, float duration = 1.0f)
        {
            EnsureAudioSource();
            desiredVolume = Mathf.Clamp01(target);
            float diff = Mathf.Abs(desiredVolume - audioSource.volume);
            fadeRate = duration > 0.01f ? (diff / duration) : 100f;
            isFading = true;
        }

        public void TickFade(float dt)
        {
            if (audioSource == null) return;

            if (isFading)
            {
                currentFadeVolume = Mathf.MoveTowards(audioSource.volume, desiredVolume, fadeRate * dt);
                audioSource.volume = currentFadeVolume;

                if (Mathf.Approximately(audioSource.volume, desiredVolume))
                {
                    audioSource.volume = desiredVolume;
                    isFading = false;

                    if (desiredVolume <= 0.0001f && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                }
            }
        }

        public void SetClip(AudioClip clip)
        {
            musicClip = clip;
            if (audioSource != null)
            {
                audioSource.clip = clip;
            }
        }
    }
}
