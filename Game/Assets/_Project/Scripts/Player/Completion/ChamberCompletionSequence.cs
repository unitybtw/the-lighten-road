using System.Collections;
using TheLightenRoad.Audio;
using TheLightenRoad.Input;
using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.Player.Completion
{
    /// <summary>Reusable, interruptible exit presentation. Only the local player is gated; timeScale is untouched.</summary>
    [DisallowMultipleComponent]
    public sealed class ChamberCompletionSequence : MonoBehaviour
    {
        public ChamberCompletionProfile Profile;
        [Tooltip("Full enabled Build Settings scene path. Empty celebrates in the current scene.")]
        public string DestinationScene;
        [Tooltip("Optional checkpoint in the same scene. Ignored when Destination Scene is set.")]
        public Transform DestinationSpawn;
        public UnityEvent Started = new UnityEvent();
        public UnityEvent ScreenCovered = new UnityEvent();
        public UnityEvent Finished = new UnityEvent();
        public UnityEvent Cancelled = new UnityEvent();
        public bool IsRunning { get; private set; }
        public bool HasCompleted { get; private set; }
        public string Phase { get; private set; } = "Idle";
        public float CurtainAlpha => curtain != null ? curtain.color.a : 0f;
        public float TitleAlpha => caption != null ? caption.alpha : 0f;
        public bool CanCancel => IsRunning && !loading;
        private static ChamberCompletionSequence active;
        private Coroutine routine;
        private GameObject presentation;
        private Image curtain;
        private CanvasGroup caption;
        private RectTransform captionRect;
        private TMP_Text title;
        private AudioSource sound;
        private float soundGain = 1f;
        private LanternController animatedLantern;
        private float previousLanternVisibility;
        private PlayerRespawnController lockedPlayer;
        private Behaviour[] gated;
        private bool[] previousEnabled;
        private Rigidbody2D body;
        private bool previousSimulation;
        private Vector2 previousVelocity;
        private bool loading, persistent;
        private int generation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
            PauseMenuController.IsCompletionSequenceRunning = () => active != null && active.IsRunning;
        }

        private void OnEnable()
        {
            PauseMenuController.IsCompletionSequenceRunning = () => active != null && active.IsRunning;
        }

        public bool TryBegin(PlayerRespawnController player)
        {
            var motor = player != null ? player.GetComponent<PlayerMotor2D>() : null;
            if (!isActiveAndEnabled || Profile == null || player == null || player.State != RespawnState.Alive ||
                motor == null || motor.TraversalLocked || GameControls.HasGameplayBlockers ||
                IsRunning || HasCompleted || (active != null && active != this)) return false;
            // Validate before locking or showing a curtain: a mistyped scene must never strand the player.
            if (!string.IsNullOrWhiteSpace(DestinationScene) && !Application.CanStreamedLevelBeLoaded(DestinationScene))
            {
                Debug.LogWarning("[ChamberCompletion] Destination is not enabled in Build Settings: " + DestinationScene, this);
                return false;
            }
            active = this;
            IsRunning = true;
            int run = ++generation;
            Lock(player);
            BuildPresentation();
            var startedRoutine = StartCoroutine(Play(run));
            if (IsRunning && generation == run) routine = startedRoutine;
            return true;
        }

        private IEnumerator Play(int run)
        {
            string completedScene = SceneManager.GetActiveScene().name;
            Phase = "Extinguish";
            ControllerGlyphService.PlayHaptic(HapticCue.ChamberComplete);
            Notify(Started);
            if (!IsRunning || generation != run || !isActiveAndEnabled) yield break;
            PlaySound(SoundEffectManager.Instance.FlareExtinguishClip, .45f);
            yield return Animate(Profile.ExtinguishSeconds, t =>
            {
                if (animatedLantern != null) animatedLantern.SetPresentationVisibility(previousLanternVisibility * (1f - t));
                SetCurtain(Mathf.InverseLerp(.18f, 1f, t));
            });
            // Typography is above the opaque curtain: the scene never competes with the title.
            Phase = "Reveal";
            PlaySound(Profile.CompletionSound);
            yield return Animate(Profile.RevealSeconds, t => ApplyReveal(t));
            Phase = "Hold";
            yield return Animate(Profile.HoldSeconds, t => { });
            Phase = "FadeOut";
            yield return Animate(Profile.FadeOutSeconds, t => caption.alpha = 1f - t);
            Notify(ScreenCovered);
            if (!IsRunning || generation != run || !isActiveAndEnabled) yield break;
            bool changesScene = !string.IsNullOrWhiteSpace(DestinationScene);
            if (changesScene)
            {
                // This root owns its effect and survives exactly one scene swap, then destroys itself.
                loading = true;
                Phase = "Loading";
                transform.SetParent(null, true);
                DontDestroyOnLoad(gameObject);
                persistent = true;
                Unlock();
                var operation = SceneManager.LoadSceneAsync(DestinationScene, LoadSceneMode.Single);
                if (operation != null) while (!operation.isDone) yield return null;
                // Allow the destination's Awake/Start to establish its own chamber state first.
                yield return null;
                if (!IsRunning || generation != run || !isActiveAndEnabled) yield break;
                var players = FindObjectsByType<PlayerRespawnController>(FindObjectsSortMode.None);
                foreach (var player in players)
                    if (player.gameObject.scene == SceneManager.GetActiveScene())
                    {
                        Lock(player);
                        if (animatedLantern != null) animatedLantern.SetPresentationVisibility(0f);
                        break;
                    }
            }
            else if (DestinationSpawn != null && lockedPlayer != null)
            {
                lockedPlayer.transform.position = DestinationSpawn.position;
                if (body != null) body.position = DestinationSpawn.position;
                lockedPlayer.BeginChamber(DestinationSpawn.position);
                var follow = FindAnyObjectByType<FollowCamera2D>();
                if (follow != null) follow.SetTarget(lockedPlayer.transform, true);
            }
            if (caption != null) caption.alpha = 0f;
            Phase = "FadeIn";
            PlaySound(SoundEffectManager.Instance.FlareIgniteClip, .45f);
            yield return Animate(Profile.FadeInSeconds, t =>
            {
                if (animatedLantern != null) animatedLantern.SetPresentationVisibility(previousLanternVisibility * t);
                // Establish a little light before uncovering the destination.
                SetCurtain(1f - Mathf.InverseLerp(.12f, 1f, t));
            });
            Unlock();
            ReleaseOwnership();
            IsRunning = false;
            HasCompleted = true;
            Phase = "Complete";

            if (completedScene.Contains("Furkan")) MenuPreferences.SetRealmCompleted(0, true);
            else if (completedScene.Contains("Kemal")) MenuPreferences.SetRealmCompleted(1, true);
            else if (completedScene.Contains("ArchiveTest")) MenuPreferences.SetRealmCompleted(2, true);

            loading = false;
            routine = null;
            if (active == this) active = null;
            Cleanup();
            Notify(Finished);
            if (persistent) Destroy(gameObject);
        }

        private void Notify(UnityEvent listeners)
        {
            try { listeners?.Invoke(); }
            catch (System.Exception error) { Debug.LogException(error, this); }
        }

        private IEnumerator Animate(float seconds, System.Action<float> apply)
        {
            float elapsed = 0f;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            apply(0f);
            while (elapsed < Mathf.Max(.001f, seconds))
            {
                // The triggering frame happened before this animation began. Start timing
                // on the next frame, rather than consuming a long loading/editor frame at once.
                yield return null;
                elapsed = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, seconds));
                apply(t * t * (3f - 2f * t));
                if (sound != null) sound.volume = Profile.SoundVolume * soundGain * SoundEffectManager.Instance.MasterSFXVolume;
            }
            apply(1f);
        }

        public bool Cancel()
        {
            if (!CanCancel) return false;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Unlock();
            ReleaseOwnership();
            Cleanup();
            IsRunning = false;
            Phase = "Idle";
            if (active == this) active = null;
            Notify(Cancelled);
            return true;
        }

        public void ResetCompletion()
        {
            if (IsRunning) return;
            HasCompleted = false;
            Phase = "Idle";
        }

        private void Lock(PlayerRespawnController player)
        {
            GameControls.BlockGameplay(this, true);
            LegacyInteractionPollingGate.Acquire(this);
            lockedPlayer = player;
            var motor = player.GetComponent<PlayerMotor2D>();
            animatedLantern = player.GetComponentInChildren<LanternController>(true);
            previousLanternVisibility = animatedLantern != null ? animatedLantern.PresentationVisibility : 1f;
            gated = new Behaviour[] { motor, animatedLantern, player };
            body = motor != null ? motor.Body : player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                previousSimulation = body.simulated;
                previousVelocity = body.linearVelocity;
            }
            previousEnabled = new bool[gated.Length];
            for (int i = 0; i < gated.Length; i++)
                if (gated[i] != null) { previousEnabled[i] = gated[i].enabled; gated[i].enabled = false; }
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
        }

        private void Unlock()
        {
            if (animatedLantern != null) animatedLantern.SetPresentationVisibility(previousLanternVisibility);
            if (body != null) { body.simulated = previousSimulation; body.linearVelocity = previousVelocity; }
            if (gated != null) for (int i = 0; i < gated.Length; i++)
                if (gated[i] != null) gated[i].enabled = previousEnabled[i];
            gated = null; body = null; lockedPlayer = null; animatedLantern = null;
        }
        private void ReleaseOwnership()
        {
            GameControls.BlockGameplay(this, false);
            LegacyInteractionPollingGate.Release(this);
        }

        private void BuildPresentation()
        {
            presentation = new GameObject("Completion / presentation");
            presentation.transform.SetParent(transform, false);
            var canvasObject = new GameObject("Completion / overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(presentation.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            curtain = Image("Curtain", canvasObject.transform, Color.clear);
            Stretch(curtain.rectTransform);
            var card = new GameObject("Completion / typography", typeof(RectTransform), typeof(CanvasGroup));
            card.transform.SetParent(canvasObject.transform, false);
            caption = card.GetComponent<CanvasGroup>();
            caption.blocksRaycasts = false; caption.interactable = false; caption.alpha = 0f;
            captionRect = card.GetComponent<RectTransform>();
            Stretch(captionRect);
            Text(Profile.Eyebrow, card.transform, 48f, 24f, new Color(.75f, .66f, .51f), 2f);
            title = Text(Profile.Title, card.transform, -3f, 48f, new Color(.94f, .90f, .81f), 2f);
            if (!string.IsNullOrWhiteSpace(Profile.Subtitle))
                Text(Profile.Subtitle, card.transform, -61f, 20f, new Color(.73f, .71f, .64f), .5f);
            var seal = Image("Ember mark", card.transform, Profile.LightColor);
            seal.rectTransform.sizeDelta = new Vector2(4, 4);
            seal.rectTransform.anchoredPosition = new Vector2(0, -43);
            seal.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            bool? memento = CheckSceneMemento();
            if (memento.HasValue)
                Text(memento.Value ? "MEMENTO RECOVERED" : "MEMENTO NOT FOUND", card.transform, -82f, 20f,
                    memento.Value ? new Color(.92f, .78f, .53f) : new Color(.67f, .65f, .58f), 1f);

            sound = presentation.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.spatialBlend = 0f;
            sound.volume = Profile.SoundVolume * SoundEffectManager.Instance.MasterSFXVolume;
        }

        private void PlaySound(AudioClip clip, float gain = 1f)
        {
            if (clip == null || sound == null) return;
            soundGain = gain;
            sound.clip = clip;
            sound.volume = Profile.SoundVolume * soundGain * SoundEffectManager.Instance.MasterSFXVolume;
            sound.Play();
        }

        private static bool? CheckSceneMemento()
        {
            try
            {
                var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var mb in behaviours)
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    var field = type.GetField("HasMemento", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        return (bool)field.GetValue(mb);
                    }
                    var prop = type.GetProperty("HasMemento", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (prop != null && prop.PropertyType == typeof(bool))
                    {
                        return (bool)prop.GetValue(mb, null);
                    }
                }
            }
            catch (System.Exception)
            {
                // Fallback gracefully
            }
            return null;
        }

        private void ApplyReveal(float t)
        {
            bool reduced = MenuPreferences.ReducedMotion;
            caption.alpha = t;
            captionRect.anchoredPosition = new Vector2(0, reduced ? 0 : Mathf.Lerp(-6, 0, t));
            title.characterSpacing = reduced ? 2f : Mathf.Lerp(7f, 2f, t);
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private TMP_Text Text(string words, Transform parent, float y, float size, Color color, float spacing)
        {
            var go = new GameObject(words, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = Profile.Font != null ? Profile.Font : (ArchiveInterfaceStyle.DisplayFont != null ? ArchiveInterfaceStyle.DisplayFont : TMP_Settings.defaultFontAsset);
            label.text = words; label.fontSize = size; label.color = color;
            label.characterSpacing = spacing; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(1000, 64);
            label.rectTransform.anchoredPosition = new Vector2(0, y);
            return label;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void SetCurtain(float alpha) => curtain.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        private void Cleanup()
        {
            if (sound != null) sound.Stop();
            if (presentation != null) { presentation.SetActive(false); Destroy(presentation); }
            curtain = null; caption = null; title = null;
        }
        private void OnDisable()
        {
            // Disable/destroy is also a safe interruption (e.g. leaving Play Mode or unloading a local scene).
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Unlock(); ReleaseOwnership(); Cleanup();
            IsRunning = false;
            if (active == this) active = null;
        }
    }
}
