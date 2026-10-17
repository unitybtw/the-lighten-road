using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Audio
{
    /// <summary>
    /// Manages atmospheric background music with gentle volume, smooth fade-in,
    /// seamless 2D looping, resilient loading recovery, and live volume adjustment.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class BackgroundMusicManager : MonoBehaviour
    {
        private static BackgroundMusicManager instance;
        public static BackgroundMusicManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Object.FindAnyObjectByType<BackgroundMusicManager>();
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static void ResetInstanceForTesting(BackgroundMusicManager testInstance = null)
        {
            instance = testInstance;
        }

        [Header("Audio Asset")]
        [Tooltip("Atmospheric background music clip.")]
        [SerializeField] private AudioClip musicClip;

        [Header("Volume & Presence")]
        [Tooltip("Resting background volume (subtle, non-intrusive ambient presence).")]
        [SerializeField, Range(0.01f, 1f)] private float targetVolume = 0.25f;
        [Tooltip("Duration in seconds for the initial smooth fade-in.")]
        [SerializeField, Min(0.1f)] private float fadeInDuration = 1.5f;
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
        private float savedPreMuteVolume = 0.25f;
        private bool isPlayRequested;

        public AudioSource Source => audioSource != null ? audioSource : audioSource = GetComponent<AudioSource>();
        public AudioClip CurrentClip => musicClip;
        public float TargetVolume
        {
            get => targetVolume;
            set
            {
                targetVolume = Mathf.Clamp01(value);
                desiredVolume = targetVolume;
                if (!isFading && audioSource != null)
                {
                    audioSource.volume = targetVolume;
                }
            }
        }
        public float CurrentVolume => audioSource != null ? audioSource.volume : 0f;
        public bool IsPlaying => audioSource != null && audioSource.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureMusicActive()
        {
            var mgr = Object.FindAnyObjectByType<BackgroundMusicManager>();
            if (mgr != null && mgr.playOnStart && !mgr.IsPlaying)
            {
                mgr.Play();
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying && transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureAudioSource();

            if (musicClip != null && musicClip.loadState == AudioDataLoadState.Unloaded)
            {
                musicClip.LoadAudioData();
            }
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
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            // Resilient retry: if playOnStart was requested but playback hasn't started yet,
            // start it as soon as the clip is loaded or streaming.
            if ((playOnStart || isPlayRequested) && musicClip != null && audioSource != null && !audioSource.isPlaying)
            {
                if (musicClip.loadState == AudioDataLoadState.Loaded || musicClip.loadType == AudioClipLoadType.Streaming)
                {
                    isPlayRequested = false;
                    StartPlayback();
                }
            }

            TickFade(Time.unscaledDeltaTime);
            HandleDebugInputs();
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
            audioSource.bypassEffects = false;
            audioSource.bypassReverbZones = true;

            if (musicClip == null)
            {
                musicClip = Resources.Load<AudioClip>("Audio/Music/AmbientRuinsBGM");
                if (musicClip == null)
                {
                    musicClip = Resources.Load<AudioClip>("Audio/AmbientRuinsBGM");
                }
#if UNITY_EDITOR
                if (musicClip == null)
                {
                    musicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/AmbientRuinsBGM.mp3");
                }
#endif
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
                if (musicClip != null)
                {
                    audioSource.clip = musicClip;
                }
                else
                {
                    isPlayRequested = true;
                    return;
                }
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

        private void StartPlayback()
        {
            if (audioSource == null || musicClip == null) return;
            if (audioSource.clip != musicClip) audioSource.clip = musicClip;
            audioSource.loop = loop;

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
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

        public void ToggleMute()
        {
            if (audioSource == null) return;
            if (desiredVolume > 0.001f)
            {
                savedPreMuteVolume = desiredVolume;
                FadeTo(0f, 0.4f);
            }
            else
            {
                float restoreVol = savedPreMuteVolume > 0.01f ? savedPreMuteVolume : targetVolume;
                if (!audioSource.isPlaying) Play();
                FadeTo(restoreVol, 0.4f);
            }
        }

        public void AdjustVolume(float delta)
        {
            TargetVolume = Mathf.Clamp(targetVolume + delta, 0.05f, 1.0f);
            FadeTo(targetVolume, 0.2f);
            Debug.Log($"[BackgroundMusicManager] Music Volume set to {targetVolume:P0}");
        }

        private void HandleDebugInputs()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null)
            {
                if (Keyboard.current.mKey.wasPressedThisFrame)
                {
                    ToggleMute();
                }
                else if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
                {
                    AdjustVolume(-0.05f);
                }
                else if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
                {
                    AdjustVolume(0.05f);
                }
            }
#endif
        }
    }
}
