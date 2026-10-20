using System.Collections;
using TheLightenRoad.Audio;
using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
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
        public float LightRadius => glow != null ? glow.pointLightOuterRadius : 0f;
        public bool CanCancel => IsRunning && !loading;
        private static ChamberCompletionSequence active;
        private Coroutine routine;
        private GameObject presentation;
        private Image curtain;
        private CanvasGroup caption;
        private RectTransform captionRect;
        private Light2D glow;
        private SpriteRenderer ring;
        private Texture2D ringTexture;
        private Sprite ringSprite;
        private AudioSource sound;
        private PlayerRespawnController lockedPlayer;
        private Behaviour[] gated;
        private bool[] previousEnabled;
        private Rigidbody2D body;
        private bool previousSimulation;
        private Vector2 previousVelocity;
        private bool loading, persistent;
        private float ringBaseAlpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;

        public bool TryBegin(PlayerRespawnController player)
        {
            if (!isActiveAndEnabled || Profile == null || player == null || player.State != RespawnState.Alive ||
                IsRunning || HasCompleted || (active != null && active != this)) return false;
            // Validate before locking or showing a curtain: a mistyped scene must never strand the player.
            if (!string.IsNullOrWhiteSpace(DestinationScene) && !Application.CanStreamedLevelBeLoaded(DestinationScene))
            {
                Debug.LogWarning("[ChamberCompletion] Destination is not enabled in Build Settings: " + DestinationScene, this);
                return false;
            }
            active = this;
            IsRunning = true;
            Lock(player);
            BuildPresentation();
            routine = StartCoroutine(Play());
            return true;
        }

        private IEnumerator Play()
        {
            string completedScene = SceneManager.GetActiveScene().name;
            Phase = "Reveal";
            Started.Invoke();
            if (sound.clip != null) sound.Play();
            yield return Animate(Profile.RevealSeconds, t => ApplyReveal(t));
            Phase = "Hold";
            yield return Animate(Profile.HoldSeconds, t => { });
            Phase = "FadeOut";
            yield return Animate(Profile.FadeOutSeconds, t => SetCurtain(t));
            ScreenCovered.Invoke();
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
                var players = FindObjectsByType<PlayerRespawnController>(FindObjectsSortMode.None);
                foreach (var player in players)
                    if (player.gameObject.scene == SceneManager.GetActiveScene()) { Lock(player); break; }
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
            if (glow != null) glow.intensity = 0f;
            if (ring != null) ring.enabled = false;
            Phase = "FadeIn";
            yield return Animate(Profile.FadeInSeconds, t => SetCurtain(1f - t));
            Unlock();
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
            Finished.Invoke();
            if (persistent) Destroy(gameObject);
        }

        private IEnumerator Animate(float seconds, System.Action<float> apply)
        {
            float elapsed = 0f;
            apply(0f);
            while (elapsed < Mathf.Max(.001f, seconds))
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, seconds));
                apply(t * t * (3f - 2f * t));
                if (sound != null) sound.volume = Profile.SoundVolume * SoundEffectManager.Instance.MasterSFXVolume;
                yield return null;
            }
            apply(1f);
        }

        public bool Cancel()
        {
            if (!CanCancel) return false;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Unlock();
            Cleanup();
            IsRunning = false;
            Phase = "Idle";
            if (active == this) active = null;
            Cancelled.Invoke();
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
            lockedPlayer = player;
            var motor = player.GetComponent<PlayerMotor2D>();
            gated = new Behaviour[] { motor, player.GetComponentInChildren<LanternController>(), player };
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
            if (body != null) { body.simulated = previousSimulation; body.linearVelocity = previousVelocity; }
            if (gated != null) for (int i = 0; i < gated.Length; i++)
                if (gated[i] != null) gated[i].enabled = previousEnabled[i];
            gated = null; body = null; lockedPlayer = null;
        }

        private void BuildPresentation()
        {
            presentation = new GameObject("Completion / presentation");
            presentation.transform.SetParent(transform, false);
            var lightObject = new GameObject("Warm light / arrival");
            lightObject.transform.SetParent(presentation.transform, false);
            lightObject.transform.position = transform.position;
            glow = lightObject.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = Profile.LightColor;
            glow.pointLightOuterRadius = .2f;
            glow.pointLightInnerRadius = 0f;
            glow.intensity = 0f;
            BuildRing(lightObject.transform);

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
            var veil = Image("Reading veil", card.transform, new Color(.025f, .03f, .025f, .68f));
            veil.rectTransform.anchorMin = new Vector2(0, .31f); veil.rectTransform.anchorMax = new Vector2(1, .69f);
            veil.rectTransform.offsetMin = veil.rectTransform.offsetMax = Vector2.zero;
            Text(Profile.Eyebrow, card.transform, 71f, 16f, new Color(.79f, .68f, .48f), 5f);
            Text(Profile.Title, card.transform, 12f, 50f, new Color(.96f, .91f, .79f), 6f);
            Text(Profile.Subtitle, card.transform, -66f, 20f, new Color(.73f, .73f, .65f), 1f);
            var line = Image("Brass rule", card.transform, new Color(.7f, .51f, .25f, .7f));
            line.rectTransform.sizeDelta = new Vector2(160, 1); line.rectTransform.anchoredPosition = new Vector2(0, -32);
            var diamond = Image("Ember seal", card.transform, Profile.LightColor);
            diamond.rectTransform.sizeDelta = new Vector2(5, 5); diamond.rectTransform.anchoredPosition = new Vector2(0, -32);
            diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            bool? memento = CheckSceneMemento();
            if (memento.HasValue)
            {
                if (memento.Value)
                    Text("✦   ARCHIVE MEMENTO RECOVERED   ✦", card.transform, -102f, 13f, new Color(.98f, .86f, .52f, .95f), 3.5f);
                else
                    Text("◇   ARCHIVE MEMENTO UNFOUND   ◇", card.transform, -102f, 13f, new Color(.62f, .68f, .75f, .62f), 3.5f);
            }

            // The curtain must draw above the caption to conceal the scene swap completely.
            curtain.transform.SetAsLastSibling();
            sound = presentation.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.spatialBlend = 0f;
            sound.clip = Profile.CompletionSound;
            sound.volume = Profile.SoundVolume * SoundEffectManager.Instance.MasterSFXVolume;
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
            glow.pointLightOuterRadius = Mathf.Lerp(.3f, Profile.LightRadius, reduced ? 1f : t);
            glow.pointLightInnerRadius = glow.pointLightOuterRadius * .18f;
            glow.intensity = Profile.LightIntensity * t;
            if (ring != null)
            {
                ring.enabled = !reduced;
                ring.transform.localScale = Vector3.one * Mathf.Lerp(.5f, Profile.LightRadius * 2f, t);
                ring.color = new Color(Profile.LightColor.r, Profile.LightColor.g, Profile.LightColor.b, Mathf.Sin(t * Mathf.PI) * ringBaseAlpha);
            }
            caption.alpha = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, 1f, t));
            captionRect.anchoredPosition = new Vector2(0, reduced ? 0 : Mathf.Lerp(-12, 0, t));
        }

        private void BuildRing(Transform parent)
        {
            const int size = 256;
            ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            ringTexture.name = "Completion / generated soft ring";
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f).magnitude;
                float alpha = Mathf.Exp(-Mathf.Pow((radius - .88f) / .025f, 2));
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            ringTexture.SetPixels(pixels); ringTexture.Apply();
            ringSprite = Sprite.Create(ringTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            var go = new GameObject("Light wave", typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
            ring = go.GetComponent<SpriteRenderer>(); ring.sprite = ringSprite; ring.sortingOrder = 50;
            ring.color = Color.clear; ringBaseAlpha = .32f;
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private void Text(string words, Transform parent, float y, float size, Color color, float spacing)
        {
            var go = new GameObject(words, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = Profile.Font != null ? Profile.Font : TMP_Settings.defaultFontAsset;
            label.text = words; label.fontSize = size; label.color = color;
            label.characterSpacing = spacing; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(1500, 90);
            label.rectTransform.anchoredPosition = new Vector2(0, y);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void SetCurtain(float alpha) => curtain.color = new Color(.012f, .019f, .017f, Mathf.Clamp01(alpha));
        private void Cleanup()
        {
            if (sound != null) sound.Stop();
            if (presentation != null) { presentation.SetActive(false); Destroy(presentation); }
            if (ringSprite != null) Destroy(ringSprite);
            if (ringTexture != null) Destroy(ringTexture);
            curtain = null; caption = null; glow = null; ring = null;
        }
        private void OnDisable()
        {
            // Disable/destroy is also a safe interruption (e.g. leaving Play Mode or unloading a local scene).
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Unlock(); Cleanup();
            IsRunning = false;
            if (active == this) active = null;
        }
    }
}
