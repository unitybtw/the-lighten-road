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

        [Header("Surface Multi-Channel SFX")]
        [SerializeField] private AudioClip[] woodWalkClips;
        [SerializeField] private AudioClip[] woodSprintClips;
        [SerializeField] private AudioClip[] metalWalkClips;
        [SerializeField] private AudioClip[] metalSprintClips;
        [SerializeField] private AudioClip[] etherealWalkClips;
        [SerializeField] private AudioClip[] etherealSprintClips;
        [SerializeField] private AudioClip[] waterWalkClips;
        [SerializeField] private AudioClip[] waterSprintClips;

        [Header("Lantern & Ember SFX")]
        [SerializeField] private AudioClip flareIgniteClip;
        [SerializeField] private AudioClip flareExtinguishClip;
        [SerializeField] private AudioClip emberBreakClip;
        [SerializeField] private AudioClip emberPickupClip;

        [Header("Foley & Movement Foley")]
        [SerializeField] private AudioClip skidScuffClip;
        [SerializeField] private AudioClip airDraftClip;

        [Header("Environment Props SFX")]
        [SerializeField] private AudioClip[] chainClinkClips;
        [SerializeField, Range(0f, 1f)] private float chainClinkVolume = 0.65f;
        [SerializeField] private AudioClip crystalChargeClip;
        [SerializeField] private AudioClip crystalActivateClip;
        [SerializeField] private AudioClip gateOpenClip;
        [SerializeField] private AudioClip gateCloseClip;
        [SerializeField, Range(0f, 1f)] private float crystalVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float gateVolume = 0.85f;

        [Header("UI Feedback SFX")]
        [SerializeField] private AudioClip uiHoverClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip uiBackClip;
        [SerializeField, Range(0f, 1f)] private float uiVolume = 0.65f;

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
        private float sceneGraceTimer = 0f;
        private readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayTimes = new System.Collections.Generic.Dictionary<AudioClip, float>();
        private int oneShotsThisFrame;
        private int lastFrameCount = -1;
        private const int MaxOneShotsPerFrame = 4;

        public bool IsInSceneGracePeriod => sceneGraceTimer > 0f;

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
        public AudioClip FlareExtinguishClip => flareExtinguishClip;
        public AudioClip EmberBreakClip => emberBreakClip;
        public AudioClip EmberPickupClip => emberPickupClip;
        public AudioClip ExhaustionBreathClip => exhaustionBreathClip;
        public AudioClip[] WoodWalkClips => woodWalkClips;
        public AudioClip[] WoodSprintClips => woodSprintClips;
        public AudioClip[] MetalWalkClips => metalWalkClips;
        public AudioClip[] MetalSprintClips => metalSprintClips;
        public AudioClip[] EtherealWalkClips => etherealWalkClips;
        public AudioClip[] EtherealSprintClips => etherealSprintClips;
        public AudioClip[] WaterWalkClips => waterWalkClips;
        public AudioClip[] WaterSprintClips => waterSprintClips;
        public AudioClip SkidScuffClip => skidScuffClip;
        public AudioClip AirDraftClip => airDraftClip;
        public AudioClip[] ChainClinkClips => chainClinkClips;
        public AudioClip UIHoverClip => uiHoverClip;
        public AudioClip UIClickClip => uiClickClip;
        public AudioClip UIBackClip => uiBackClip;
        public float UIVolume { get => uiVolume; set => uiVolume = Mathf.Clamp01(value); }

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
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            EmberPickup.EmberCollected -= HandleEmberPickupCollected;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            StopTransientSounds();
            sceneGraceTimer = 0.35f;
            lastPlayTimes.Clear();
        }

        public void StopTransientSounds()
        {
            if (sourcePool != null)
            {
                for (int i = 0; i < sourcePool.Length; i++)
                {
                    if (sourcePool[i] != null && sourcePool[i].isPlaying)
                    {
                        sourcePool[i].Stop();
                    }
                }
            }
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

            if (woodWalkClips == null || woodWalkClips.Length == 0 || woodWalkClips[0] == null)
            {
                woodWalkClips = LoadSurfaceBank("Wood", "footstep_wood_walk", SurfaceType.Wood, false);
            }
            if (woodSprintClips == null || woodSprintClips.Length == 0 || woodSprintClips[0] == null)
            {
                woodSprintClips = LoadSurfaceBank("Wood", "footstep_wood_sprint", SurfaceType.Wood, true);
            }
            if (metalWalkClips == null || metalWalkClips.Length == 0 || metalWalkClips[0] == null)
            {
                metalWalkClips = LoadSurfaceBank("Metal", "footstep_metal_walk", SurfaceType.Metal, false);
            }
            if (metalSprintClips == null || metalSprintClips.Length == 0 || metalSprintClips[0] == null)
            {
                metalSprintClips = LoadSurfaceBank("Metal", "footstep_metal_sprint", SurfaceType.Metal, true);
            }
            if (etherealWalkClips == null || etherealWalkClips.Length == 0 || etherealWalkClips[0] == null)
            {
                etherealWalkClips = LoadSurfaceBank("Ethereal", "footstep_ethereal_walk", SurfaceType.Ethereal, false);
            }
            if (etherealSprintClips == null || etherealSprintClips.Length == 0 || etherealSprintClips[0] == null)
            {
                etherealSprintClips = LoadSurfaceBank("Ethereal", "footstep_ethereal_sprint", SurfaceType.Ethereal, true);
            }
            if (waterWalkClips == null || waterWalkClips.Length == 0 || waterWalkClips[0] == null)
            {
                waterWalkClips = LoadSurfaceBank("Water", "footstep_water_walk", SurfaceType.Water, false);
            }
            if (waterSprintClips == null || waterSprintClips.Length == 0 || waterSprintClips[0] == null)
            {
                waterSprintClips = LoadSurfaceBank("Water", "footstep_water_sprint", SurfaceType.Water, true);
            }
            if (flareExtinguishClip == null)
            {
                flareExtinguishClip = CreateProceduralClip("Flare_Extinguish", 0.32f, t =>
                {
                    float decay = Mathf.Exp(-10f * t);
                    float whoosh = Mathf.Sin(2f * Mathf.PI * (180f - t * 240f) * t) * 0.6f 
                                 + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.4f;
                    return whoosh * decay;
                });
            }
            if (skidScuffClip == null)
            {
                skidScuffClip = CreateProceduralClip("Skid_Scuff", 0.09f, t =>
                {
                    float decay = Mathf.Exp(-42f * t);
                    float rasp = Mathf.Sin(2f * Mathf.PI * 320f * t) * 0.6f 
                               + Mathf.Sin(2f * Mathf.PI * 740f * t) * 0.4f;
                    return rasp * decay;
                });
            }
            if (airDraftClip == null)
            {
                airDraftClip = CreateProceduralClip("Air_Draft", 0.42f, t =>
                {
                    float env = Mathf.Sin(Mathf.PI * (t / 0.42f));
                    float breeze = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.4f 
                                 + Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.6f;
                    return breeze * env;
                });
            }
            if (chainClinkClips == null || chainClinkClips.Length == 0 || chainClinkClips[0] == null)
            {
                var c1 = Resources.Load<AudioClip>("Audio/SFX/Environment/chain_clink_01");
                var c2 = Resources.Load<AudioClip>("Audio/SFX/Environment/chain_clink_02");
                if (c1 != null && c2 != null)
                {
                    chainClinkClips = new[] { c1, c2 };
                }
                else
                {
                    chainClinkClips = new[]
                    {
                        CreateProceduralClip("Chain_Clink_1", 0.15f, t => Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-35f * t) * 0.4f),
                        CreateProceduralClip("Chain_Clink_2", 0.18f, t => Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-40f * t) * 0.4f)
                    };
                }
            }
            if (crystalChargeClip == null) crystalChargeClip = Resources.Load<AudioClip>("Audio/SFX/Environment/crystal_charge");
            if (crystalActivateClip == null) crystalActivateClip = Resources.Load<AudioClip>("Audio/SFX/Environment/crystal_activate");
            if (gateOpenClip == null) gateOpenClip = Resources.Load<AudioClip>("Audio/SFX/Environment/gate_open");
            if (gateCloseClip == null) gateCloseClip = Resources.Load<AudioClip>("Audio/SFX/Environment/gate_close");

            if (uiHoverClip == null)
            {
                uiHoverClip = CreateProceduralClip("UI_Hover", 0.05f, t =>
                {
                    float env = Mathf.Exp(-70f * t);
                    return Mathf.Sin(2f * Mathf.PI * 1450f * t) * 0.25f * env;
                });
            }
            if (uiClickClip == null)
            {
                uiClickClip = CreateProceduralClip("UI_Click", 0.07f, t =>
                {
                    float env = Mathf.Exp(-48f * t);
                    float click = Mathf.Sin(2f * Mathf.PI * 560f * t) * 0.5f 
                                + Mathf.Sin(2f * Mathf.PI * 1120f * t) * 0.3f;
                    return click * env;
                });
            }
            if (uiBackClip == null)
            {
                uiBackClip = CreateProceduralClip("UI_Back", 0.11f, t =>
                {
                    float env = Mathf.Exp(-32f * t);
                    float freq = Mathf.Lerp(420f, 260f, t / 0.11f);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f * env;
                });
            }
        }

        private float playerCheckTimer;
        private float savedPreMuteVolume = 1.0f;

        private void Update()
        {
            if (sceneGraceTimer > 0f)
            {
                sceneGraceTimer -= Time.unscaledDeltaTime;
            }

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
            if (clip == null || masterSFXVolume <= 0.001f) return;

            // Debounce identical clips playing in the same millisecond burst
            float now = Time.unscaledTime;
            if (lastPlayTimes.TryGetValue(clip, out float lastTime))
            {
                if (now - lastTime < 0.045f) return;
            }
            lastPlayTimes[clip] = now;

            // Burst limiter to prevent cacophony when multiple colliders trigger on same frame
            if (Time.frameCount != lastFrameCount)
            {
                lastFrameCount = Time.frameCount;
                oneShotsThisFrame = 1;
            }
            else
            {
                oneShotsThisFrame++;
                if (oneShotsThisFrame > MaxOneShotsPerFrame)
                {
                    return;
                }
            }

            var src = GetAvailableSource();
            if (src == null) return;

            src.pitch = pitch;
            src.volume = Mathf.Clamp01(volume * masterSFXVolume);
            src.PlayOneShot(clip);
        }

        private static AudioClip CreateProceduralClip(string name, float duration, System.Func<float, float> waveFunc)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.Max(1, (int)(duration * sampleRate));
            var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            float[] data = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Clamp(waveFunc(t), -1f, 1f);
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip[] LoadSurfaceBank(string folder, string filePrefix, SurfaceType surface, bool isSprint)
        {
            var loaded = new AudioClip[4];
            bool anyMissing = false;
            for (int i = 0; i < 4; i++)
            {
                string path = $"Audio/SFX/Surfaces/{folder}/{filePrefix}_{i + 1:D2}";
                loaded[i] = Resources.Load<AudioClip>(path);
                if (loaded[i] == null)
                {
                    anyMissing = true;
                }
            }

            if (anyMissing)
            {
                // Fallback to procedural generation if resources not loaded (e.g. headless unit tests without asset database)
                return GenerateStepBank(folder, surface, isSprint);
            }

            return loaded;
        }

        private static AudioClip[] GenerateStepBank(string prefix, SurfaceType surface, bool isSprint)
        {
            var bank = new AudioClip[4];
            float dur = isSprint ? 0.12f : 0.15f;
            for (int i = 0; i < 4; i++)
            {
                float pitchMod = 1f + (i - 1.5f) * 0.08f;
                string clipName = $"{prefix}_{surface}_{(isSprint ? "Sprint" : "Walk")}_{i + 1}";
                bank[i] = CreateProceduralClip(clipName, dur, t =>
                {
                    switch (surface)
                    {
                        case SurfaceType.Wood:
                        {
                            float decay = Mathf.Exp(-24f * t);
                            float freq = (210f + i * 15f) * pitchMod;
                            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.35f;
                            float knock = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.4f;
                            return (tone + knock) * decay;
                        }
                        case SurfaceType.Metal:
                        {
                            float decay = Mathf.Exp(-28f * t);
                            float freq = (1200f + i * 60f) * pitchMod;
                            float ring = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * freq * 2.1f * t) * 0.4f;
                            float click = Mathf.Sin(2f * Mathf.PI * 3400f * t) * 0.2f * Mathf.Exp(-90f * t);
                            return (ring + click) * decay;
                        }
                        case SurfaceType.Ethereal:
                        {
                            float decay = Mathf.Exp(-14f * t);
                            float f1 = (880f + i * 40f) * pitchMod;
                            float f2 = f1 * 1.5f;
                            float f3 = f1 * 2f;
                            float tone = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.5f 
                                       + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.35f 
                                       + Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.2f;
                            return tone * decay;
                        }
                        case SurfaceType.Water:
                        {
                            float decay = Mathf.Exp(-20f * t);
                            float splash = Mathf.Sin(2f * Mathf.PI * (500f + i * 50f) * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.3f;
                            float plop = Mathf.Sin(2f * Mathf.PI * (280f - t * 500f) * t) * 0.5f;
                            return (splash + plop) * decay;
                        }
                        default:
                        {
                            float decay = Mathf.Exp(-36f * t);
                            float freq = (520f + i * 30f) * pitchMod;
                            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * freq * 1.9f * t) * 0.3f;
                            return tone * decay;
                        }
                    }
                });
            }
            return bank;
        }

        private AudioClip[] GetWalkClipsForSurface(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Wood: return (woodWalkClips != null && woodWalkClips.Length > 0) ? woodWalkClips : walkClips;
                case SurfaceType.Metal: return (metalWalkClips != null && metalWalkClips.Length > 0) ? metalWalkClips : walkClips;
                case SurfaceType.Ethereal: return (etherealWalkClips != null && etherealWalkClips.Length > 0) ? etherealWalkClips : walkClips;
                case SurfaceType.Water: return (waterWalkClips != null && waterWalkClips.Length > 0) ? waterWalkClips : walkClips;
                default: return walkClips;
            }
        }

        private AudioClip[] GetSprintClipsForSurface(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Wood: return (woodSprintClips != null && woodSprintClips.Length > 0) ? woodSprintClips : sprintClips;
                case SurfaceType.Metal: return (metalSprintClips != null && metalSprintClips.Length > 0) ? metalSprintClips : sprintClips;
                case SurfaceType.Ethereal: return (etherealSprintClips != null && etherealSprintClips.Length > 0) ? etherealSprintClips : sprintClips;
                case SurfaceType.Water: return (waterSprintClips != null && waterSprintClips.Length > 0) ? waterSprintClips : sprintClips;
                default: return sprintClips;
            }
        }

        public void PlayWalkFootstep(SurfaceType surface = SurfaceType.Stone)
        {
            if (sceneGraceTimer > 0f) return;
            EnsureClipsLoaded();
            var bank = GetWalkClipsForSurface(surface);
            if (bank == null || bank.Length == 0 || bank[0] == null) bank = walkClips;
            if (bank == null || bank.Length == 0) return;

            AudioClip clip = bank[Random.Range(0, bank.Length)];
            float pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
            PlayOneShot(clip, footstepsVolume, pitch);
        }

        public void PlaySprintFootstep(SurfaceType surface = SurfaceType.Stone)
        {
            if (sceneGraceTimer > 0f) return;
            EnsureClipsLoaded();
            var bank = GetSprintClipsForSurface(surface);
            if (bank == null || bank.Length == 0 || bank[0] == null) bank = sprintClips;
            if (bank == null || bank.Length == 0) return;

            AudioClip clip = bank[Random.Range(0, bank.Length)];
            float pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
            PlayOneShot(clip, footstepsVolume * 1.25f, pitch);
        }

        public void PlayJump(SurfaceType surface = SurfaceType.Stone)
        {
            if (sceneGraceTimer > 0f) return;
            if (jumpClip == null) EnsureClipsLoaded();
            if (jumpClip == null) return;
            float pitchMod = surface == SurfaceType.Ethereal ? 1.15f : (surface == SurfaceType.Metal ? 1.08f : 1.0f);
            float pitch = pitchMod + Random.Range(-pitchRandomRange * 0.5f, pitchRandomRange * 0.5f);
            PlayOneShot(jumpClip, jumpVolume, pitch);
        }

        public void PlayLanding(float impactSpeed, SurfaceType surface = SurfaceType.Stone)
        {
            if (sceneGraceTimer > 0f || Mathf.Abs(impactSpeed) < 1.0f) return;

            EnsureClipsLoaded();
            bool isHard = Mathf.Abs(impactSpeed) > 10.5f;
            AudioClip clip = isHard ? landingHardClip : landingSoftClip;
            if (clip == null) clip = landingSoftClip ?? landingHardClip;
            if (clip == null) return;

            float speedFactor = Mathf.Clamp01((Mathf.Abs(impactSpeed) - 1.0f) / 12f);
            float vol = landingVolume * Mathf.Lerp(0.25f, 1.0f, speedFactor);
            float pitchMod = surface == SurfaceType.Ethereal ? 1.2f : (surface == SurfaceType.Wood ? 0.9f : 1.0f);
            float pitch = pitchMod + Random.Range(-pitchRandomRange, pitchRandomRange);
            PlayOneShot(clip, vol, pitch);

            if (surface == SurfaceType.Ethereal && etherealWalkClips != null && etherealWalkClips.Length > 0)
            {
                PlayOneShot(etherealWalkClips[0], vol * 0.7f, 1.3f);
            }
        }

        public void PlayFlareExtinguish()
        {
            if (flareExtinguishClip == null) EnsureClipsLoaded();
            if (flareExtinguishClip == null) return;
            PlayOneShot(flareExtinguishClip, flareVolume * 0.75f, 1.0f);
        }

        public void PlaySkidScuff(SurfaceType surface = SurfaceType.Stone)
        {
            if (sceneGraceTimer > 0f) return;
            if (skidScuffClip == null) EnsureClipsLoaded();
            if (skidScuffClip == null) return;
            float pitchMod = surface == SurfaceType.Metal ? 1.25f : (surface == SurfaceType.Wood ? 0.85f : 1.0f);
            PlayOneShot(skidScuffClip, footstepsVolume * 0.8f, pitchMod);
        }

        public void PlayAirDraft()
        {
            if (sceneGraceTimer > 0f) return;
            if (airDraftClip == null) EnsureClipsLoaded();
            if (airDraftClip == null) return;
            PlayOneShot(airDraftClip, 0.45f, 1.0f);
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

        public void PlayChainClink(float volumeScale = 1.0f)
        {
            if (sceneGraceTimer > 0f) return;
            if (chainClinkClips == null || chainClinkClips.Length == 0) EnsureClipsLoaded();
            if (chainClinkClips == null || chainClinkClips.Length == 0) return;

            int idx = Random.Range(0, chainClinkClips.Length);
            var clip = chainClinkClips[idx];
            if (clip != null)
            {
                float pitch = 1.0f + Random.Range(-pitchRandomRange, pitchRandomRange);
                PlayOneShot(clip, chainClinkVolume * Mathf.Clamp01(volumeScale), pitch);
            }
        }

        public void PlayCrystalCharge(float pitchScale = 1.0f, float volumeScale = 1.0f)
        {
            if (sceneGraceTimer > 0f) return;
            if (crystalChargeClip == null) EnsureClipsLoaded();
            if (crystalChargeClip != null)
            {
                PlayOneShot(crystalChargeClip, crystalVolume * Mathf.Clamp01(volumeScale), pitchScale);
            }
        }

        public void PlayCrystalActivate(float volumeScale = 1.0f)
        {
            if (crystalActivateClip == null) EnsureClipsLoaded();
            if (crystalActivateClip != null)
            {
                float pitch = 1.0f + Random.Range(-pitchRandomRange * 0.5f, pitchRandomRange * 0.5f);
                PlayOneShot(crystalActivateClip, crystalVolume * Mathf.Clamp01(volumeScale), pitch);
            }
        }

        public void PlayGateOpen(float volumeScale = 1.0f)
        {
            if (sceneGraceTimer > 0f) return;
            if (gateOpenClip == null) EnsureClipsLoaded();
            if (gateOpenClip != null)
            {
                float pitch = 1.0f + Random.Range(-pitchRandomRange, pitchRandomRange);
                PlayOneShot(gateOpenClip, gateVolume * Mathf.Clamp01(volumeScale), pitch);
            }
        }

        public void PlayGateClose(float volumeScale = 1.0f)
        {
            if (sceneGraceTimer > 0f) return;
            if (gateCloseClip == null) EnsureClipsLoaded();
            if (gateCloseClip != null)
            {
                float pitch = 1.0f + Random.Range(-pitchRandomRange, pitchRandomRange);
                PlayOneShot(gateCloseClip, gateVolume * Mathf.Clamp01(volumeScale), pitch);
            }
        }

        public void PlayUIHover(float volumeScale = 1.0f)
        {
            if (uiHoverClip == null) EnsureClipsLoaded();
            if (uiHoverClip != null)
            {
                PlayOneShot(uiHoverClip, uiVolume * 0.40f * Mathf.Clamp01(volumeScale), 1.0f);
            }
        }

        public void PlayUIClick(float volumeScale = 1.0f)
        {
            if (uiClickClip == null) EnsureClipsLoaded();
            if (uiClickClip != null)
            {
                PlayOneShot(uiClickClip, uiVolume * 0.85f * Mathf.Clamp01(volumeScale), 1.0f);
            }
        }

        public void PlayUIBack(float volumeScale = 1.0f)
        {
            if (uiBackClip == null) EnsureClipsLoaded();
            if (uiBackClip != null)
            {
                PlayOneShot(uiBackClip, uiVolume * 0.75f * Mathf.Clamp01(volumeScale), 1.0f);
            }
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

        public void ConfigureSurfaceClips(
            SurfaceType surface,
            AudioClip[] walks,
            AudioClip[] sprints)
        {
            switch (surface)
            {
                case SurfaceType.Wood:
                    woodWalkClips = walks;
                    woodSprintClips = sprints;
                    break;
                case SurfaceType.Metal:
                    metalWalkClips = walks;
                    metalSprintClips = sprints;
                    break;
                case SurfaceType.Ethereal:
                    etherealWalkClips = walks;
                    etherealSprintClips = sprints;
                    break;
                case SurfaceType.Water:
                    waterWalkClips = walks;
                    waterSprintClips = sprints;
                    break;
                default:
                    walkClips = walks;
                    sprintClips = sprints;
                    break;
            }
        }
    }
}
