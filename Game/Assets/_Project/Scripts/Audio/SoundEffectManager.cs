using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Audio
{
    /// <summary>
    /// Central sound effect manager handling audio source pooling, organic pitch variation,
    /// seamless multi-channel playback, and exhaustion breath looping.
    /// Auto-instantiates with full CC0 SFX library if missing from any scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundEffectManager : MonoBehaviour
    {
        private static SoundEffectManager instance;
        private static bool isApplicationQuitting;

        public static bool HasInstance => instance != null && !isApplicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            isApplicationQuitting = false;
            instance = null;
        }

        public static SoundEffectManager Instance
        {
            get
            {
                if (isApplicationQuitting)
                {
                    return instance;
                }

                if (instance == null)
                {
                    instance = Object.FindAnyObjectByType<SoundEffectManager>();
                    if (instance == null && !isApplicationQuitting)
                    {
                        var go = new GameObject("Sound Effect Manager");
                        instance = go.AddComponent<SoundEffectManager>();
                        instance.EnsureClipsLoaded();
                        instance.BuildPool();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureSoundEffectManager()
        {
            if (isApplicationQuitting) return;

            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<SoundEffectManager>();
                if (instance == null && !isApplicationQuitting)
                {
                    var go = new GameObject("Sound Effect Manager");
                    instance = go.AddComponent<SoundEffectManager>();
                    instance.EnsureClipsLoaded();
                    instance.BuildPool();
                    DontDestroyOnLoad(go);
                }
            }
        }

        public static void ResetInstanceForTesting(SoundEffectManager testInstance = null)
        {
            isApplicationQuitting = false;
            instance = testInstance;
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        [Header("Player Movement SFX")]
        [SerializeField] private AudioClip[] walkClips;
        [SerializeField] private AudioClip[] sprintClips;
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip landingSoftClip;
        [SerializeField] private AudioClip landingHardClip;
        [SerializeField] private AudioClip exhaustionBreathClip;

        [Header("Lantern & Ember SFX")]
        [SerializeField] private AudioClip flareIgniteClip;
        [SerializeField] private AudioClip emberBreakClip;
        [SerializeField] private AudioClip emberPickupClip;

        [Header("Volume Balances")]
        [SerializeField, Range(0f, 1f)] private float masterSFXVolume = 1.0f;
        [SerializeField, Range(0f, 1f)] private float footstepsVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float landingVolume = 0.90f;
        [SerializeField, Range(0f, 1f)] private float flareVolume = 0.90f;
        [SerializeField, Range(0f, 1f)] private float emberBreakVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float emberPickupVolume = 0.90f;
        [SerializeField, Range(0f, 1f)] private float exhaustionVolume = 0.65f;

        [Header("Pitch Natural Randomization")]
        [SerializeField, Range(0f, 0.2f)] private float pitchRandomRange = 0.06f;

        private const int PoolSize = 8;
        private AudioSource[] sourcePool;
        private int poolIndex;
        private AudioSource exhaustionSource;
        private float targetExhaustionVol;
        private float exhaustionFadeSpeed = 2.5f;

        public float MasterSFXVolume { get => masterSFXVolume; set => masterSFXVolume = Mathf.Clamp01(value); }
        public float FootstepsVolume { get => footstepsVolume; set => footstepsVolume = Mathf.Clamp01(value); }
        public float ExhaustionVolume => exhaustionSource != null ? exhaustionSource.volume : 0f;
        public bool IsExhaustionPlaying => exhaustionSource != null && exhaustionSource.isPlaying;
        public AudioClip[] WalkClips => walkClips;
        public AudioClip[] SprintClips => sprintClips;
        public AudioClip JumpClip => jumpClip;
        public AudioClip LandingSoftClip => landingSoftClip;
        public AudioClip LandingHardClip => landingHardClip;
        public AudioClip FlareIgniteClip => flareIgniteClip;
        public AudioClip EmberBreakClip => emberBreakClip;
        public AudioClip EmberPickupClip => emberPickupClip;
        public AudioClip ExhaustionBreathClip => exhaustionBreathClip;

        private void Awake()
        {
            isApplicationQuitting = false;
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

            EnsureClipsLoaded();
            BuildPool();
        }

        private void OnEnable()
        {
            EmberPickup.EmberCollected += HandleEmberPickupCollected;
        }

        private void OnDisable()
        {
            EmberPickup.EmberCollected -= HandleEmberPickupCollected;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void EnsureClipsLoaded()
        {
            if (walkClips == null || walkClips.Length == 0 || walkClips[0] == null)
            {
                walkClips = new[]
                {
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_walk_01"),
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_walk_02"),
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_walk_03"),
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_walk_04")
                };
            }
            if (sprintClips == null || sprintClips.Length == 0 || sprintClips[0] == null)
            {
                sprintClips = new[]
                {
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_sprint_01"),
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_sprint_02"),
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_sprint_03"),
                    Resources.Load<AudioClip>("Audio/SFX/Player/footstep_sprint_04")
                };
            }
            if (jumpClip == null) jumpClip = Resources.Load<AudioClip>("Audio/SFX/Player/jump_takeoff");
            if (landingSoftClip == null) landingSoftClip = Resources.Load<AudioClip>("Audio/SFX/Player/landing_soft");
            if (landingHardClip == null) landingHardClip = Resources.Load<AudioClip>("Audio/SFX/Player/landing_hard");
            if (exhaustionBreathClip == null) exhaustionBreathClip = Resources.Load<AudioClip>("Audio/SFX/Player/exhaustion_breath");
            if (flareIgniteClip == null) flareIgniteClip = Resources.Load<AudioClip>("Audio/SFX/Lantern/flare_ignite");
            if (emberBreakClip == null) emberBreakClip = Resources.Load<AudioClip>("Audio/SFX/Lantern/ember_break");
            if (emberPickupClip == null) emberPickupClip = Resources.Load<AudioClip>("Audio/SFX/Environment/ember_pickup");

            if (exhaustionSource != null && exhaustionSource.clip == null && exhaustionBreathClip != null)
            {
                exhaustionSource.clip = exhaustionBreathClip;
            }
        }

        private float playerCheckTimer;
        private float savedPreMuteVolume = 1.0f;

        private void Update()
        {
            TickExhaustion(Time.unscaledDeltaTime);
            HandleSFXShortcuts();

            playerCheckTimer -= Time.unscaledDeltaTime;
            if (playerCheckTimer <= 0f)
            {
                playerCheckTimer = 2.0f;
                PlayerAudioController.AttachToAllMotors();
            }
        }

        private void HandleSFXShortcuts()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.nKey.wasPressedThisFrame)
            {
                ToggleMute();
            }
            else if (kb.commaKey.wasPressedThisFrame)
            {
                AdjustVolume(-0.05f);
            }
            else if (kb.periodKey.wasPressedThisFrame)
            {
                AdjustVolume(0.05f);
            }
        }

        public void ToggleMute()
        {
            if (masterSFXVolume > 0.001f)
            {
                savedPreMuteVolume = masterSFXVolume;
                masterSFXVolume = 0f;
                Debug.Log("[SoundEffectManager] SFX Muted.");
            }
            else
            {
                masterSFXVolume = savedPreMuteVolume > 0.001f ? savedPreMuteVolume : 1.0f;
                Debug.Log($"[SoundEffectManager] SFX Unmuted ({masterSFXVolume * 100:F0}%).");
            }
        }

        public void AdjustVolume(float delta)
        {
            masterSFXVolume = Mathf.Clamp01(masterSFXVolume + delta);
            Debug.Log($"[SoundEffectManager] SFX Volume: {masterSFXVolume * 100:F0}%");
        }

        private AudioSource CreatePoolSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = false;
            return src;
        }

        public AudioSource EnsureExhaustionSource()
        {
            if (exhaustionSource == null)
            {
                exhaustionSource = gameObject.AddComponent<AudioSource>();
                exhaustionSource.playOnAwake = false;
                exhaustionSource.spatialBlend = 0f;
                exhaustionSource.loop = true;
                exhaustionSource.volume = 0f;
            }

            if (exhaustionBreathClip != null && exhaustionSource.clip == null)
            {
                exhaustionSource.clip = exhaustionBreathClip;
            }

            return exhaustionSource;
        }

        private void BuildPool()
        {
            // Clean up legacy child sources if any exist
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("SFX_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            if (sourcePool == null || sourcePool.Length != PoolSize)
            {
                sourcePool = new AudioSource[PoolSize];
            }

            for (int i = 0; i < PoolSize; i++)
            {
                if (sourcePool[i] == null)
                {
                    sourcePool[i] = CreatePoolSource();
                }
            }

            EnsureExhaustionSource();
        }

        public AudioSource GetAvailableSource()
        {
            if (sourcePool == null || sourcePool.Length != PoolSize)
            {
                BuildPool();
            }

            // Self-heal: ensure every pool slot has a valid, living AudioSource
            for (int i = 0; i < PoolSize; i++)
            {
                if (sourcePool[i] == null)
                {
                    sourcePool[i] = CreatePoolSource();
                }
            }

            for (int i = 0; i < sourcePool.Length; i++)
            {
                int idx = (poolIndex + i) % sourcePool.Length;
                var candidate = sourcePool[idx];
                if (candidate == null)
                {
                    candidate = sourcePool[idx] = CreatePoolSource();
                }

                if (!candidate.isPlaying)
                {
                    poolIndex = (idx + 1) % sourcePool.Length;
                    return candidate;
                }
            }

            // If all busy, steal the oldest one
            var fallback = sourcePool[poolIndex];
            if (fallback == null)
            {
                fallback = sourcePool[poolIndex] = CreatePoolSource();
            }
            poolIndex = (poolIndex + 1) % sourcePool.Length;
            return fallback;
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            var src = GetAvailableSource();
            if (src == null) return;

            src.pitch = pitch;
            src.volume = Mathf.Clamp01(volume * masterSFXVolume);
            src.PlayOneShot(clip);
        }

        public void PlayWalkFootstep()
        {
            if (walkClips == null || walkClips.Length == 0 || walkClips[0] == null) EnsureClipsLoaded();
            if (walkClips == null || walkClips.Length == 0) return;
            AudioClip clip = walkClips[Random.Range(0, walkClips.Length)];
            float pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
            PlayOneShot(clip, footstepsVolume, pitch);
        }

        public void PlaySprintFootstep()
        {
            if (sprintClips == null || sprintClips.Length == 0 || sprintClips[0] == null) EnsureClipsLoaded();
            if (sprintClips == null || sprintClips.Length == 0) return;
            AudioClip clip = sprintClips[Random.Range(0, sprintClips.Length)];
            float pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
            PlayOneShot(clip, footstepsVolume * 1.25f, pitch);
        }

        public void PlayJump()
        {
            if (jumpClip == null) EnsureClipsLoaded();
            if (jumpClip == null) return;
            float pitch = 1f + Random.Range(-pitchRandomRange * 0.5f, pitchRandomRange * 0.5f);
            PlayOneShot(jumpClip, jumpVolume, pitch);
        }

        public void PlayLanding(float impactSpeed)
        {
            if (landingSoftClip == null || landingHardClip == null) EnsureClipsLoaded();
            bool isHard = Mathf.Abs(impactSpeed) > 10.5f;
            AudioClip clip = isHard ? landingHardClip : landingSoftClip;
            if (clip == null) clip = landingSoftClip ?? landingHardClip;
            if (clip == null) return;

            float speedFactor = Mathf.Clamp01(Mathf.Abs(impactSpeed) / 14f);
            float vol = landingVolume * Mathf.Lerp(0.5f, 1.2f, speedFactor);
            float pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
            PlayOneShot(clip, vol, pitch);
        }

        public void PlayFlareIgnite()
        {
            if (flareIgniteClip == null) EnsureClipsLoaded();
            if (flareIgniteClip == null) return;
            PlayOneShot(flareIgniteClip, flareVolume, 1.0f);
        }

        public void PlayEmberBreak()
        {
            if (emberBreakClip == null) EnsureClipsLoaded();
            if (emberBreakClip == null) return;
            float pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
            PlayOneShot(emberBreakClip, emberBreakVolume, pitch);
        }

        public void PlayEmberPickup()
        {
            if (emberPickupClip == null) EnsureClipsLoaded();
            if (emberPickupClip == null) return;
            PlayOneShot(emberPickupClip, emberPickupVolume, 1.0f);
        }

        private void HandleEmberPickupCollected()
        {
            PlayEmberPickup();
        }

        public void SetExhaustionActive(bool active)
        {
            targetExhaustionVol = active ? (exhaustionVolume * masterSFXVolume) : 0f;

            var src = EnsureExhaustionSource();
            if (active && src != null && !src.isPlaying)
            {
                if (src.clip == null && exhaustionBreathClip != null)
                {
                    src.clip = exhaustionBreathClip;
                }
                if (src.clip != null)
                {
                    src.volume = 0f;
                    src.Play();
                }
            }
        }

        public void TickExhaustion(float dt)
        {
            if (exhaustionSource == null) return;

            if (!Mathf.Approximately(exhaustionSource.volume, targetExhaustionVol))
            {
                exhaustionSource.volume = Mathf.MoveTowards(exhaustionSource.volume, targetExhaustionVol, exhaustionFadeSpeed * dt);

                if (targetExhaustionVol <= 0.001f && exhaustionSource.volume <= 0.001f && exhaustionSource.isPlaying)
                {
                    exhaustionSource.Stop();
                }
            }
        }

        public void ConfigureClips(
            AudioClip[] walks,
            AudioClip[] sprints,
            AudioClip jump,
            AudioClip landSoft,
            AudioClip landHard,
            AudioClip flare,
            AudioClip emberBreak,
            AudioClip emberPick,
            AudioClip breath)
        {
            walkClips = walks;
            sprintClips = sprints;
            jumpClip = jump;
            landingSoftClip = landSoft;
            landingHardClip = landHard;
            flareIgniteClip = flare;
            emberBreakClip = emberBreak;
            emberPickupClip = emberPick;
            exhaustionBreathClip = breath;

            if (exhaustionSource != null && breath != null)
            {
                exhaustionSource.clip = breath;
            }
        }
    }
}
