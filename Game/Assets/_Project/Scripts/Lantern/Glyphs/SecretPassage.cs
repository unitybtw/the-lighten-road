using System.Collections;
using TheLightenRoad.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace TheLightenRoad.Lantern.Glyphs
{
    /// <summary>
    /// A hidden stone wall, archway, or partition that conceals a secret path, shortcut,
    /// or ember cache. Automatically fades out and opens when triggered by a FlareGlyph.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class SecretPassage : MonoBehaviour
    {
        [Header("Passage Components")]
        [SerializeField] private Collider2D wallCollider;
        [SerializeField] private SpriteRenderer wallRenderer;
        [SerializeField] private GameObject secretReward;

        [Header("Opening Behavior")]
        [Tooltip("If true, the passage remains permanently open once discovered.")]
        [SerializeField] private bool stayOpenOnceDiscovered = true;
        [SerializeField, Min(0.1f)] private float fadeDuration = 0.85f;
        [SerializeField] private AudioClip openSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.75f;

        [Header("Events")]
        public UnityEvent OnOpened = new UnityEvent();
        public UnityEvent OnClosed = new UnityEvent();

        public bool IsOpen { get; private set; }
        private Coroutine fadeRoutine;
        private float baseAlpha = 1.0f;

        private void Awake()
        {
            if (wallCollider == null)
                wallCollider = GetComponent<Collider2D>();
            if (wallRenderer == null)
                wallRenderer = GetComponent<SpriteRenderer>();

            if (wallRenderer != null)
                baseAlpha = wallRenderer.color.a;

            if (secretReward != null)
                secretReward.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadePassage(0f, false));

            if (secretReward != null)
                secretReward.SetActive(true);

            PlaySound();
            OnOpened.Invoke();
        }

        public void Close()
        {
            if (!IsOpen || stayOpenOnceDiscovered) return;
            IsOpen = false;

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadePassage(baseAlpha, true));

            OnClosed.Invoke();
        }

        private IEnumerator FadePassage(float targetAlpha, bool enableCollisionOnFinish)
        {
            if (wallRenderer == null)
            {
                if (wallCollider != null) wallCollider.enabled = enableCollisionOnFinish;
                yield break;
            }

            if (!enableCollisionOnFinish && wallCollider != null)
            {
                wallCollider.enabled = false;
            }

            Color startColor = wallRenderer.color;
            float startAlpha = startColor.a;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                Color c = startColor;
                c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
                wallRenderer.color = c;
                yield return null;
            }

            Color finalColor = startColor;
            finalColor.a = targetAlpha;
            wallRenderer.color = finalColor;

            if (enableCollisionOnFinish && wallCollider != null)
            {
                wallCollider.enabled = true;
            }

            fadeRoutine = null;
        }

        private void PlaySound()
        {
            if (openSound != null && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayOneShot(openSound, soundVolume * SoundEffectManager.Instance.MasterSFXVolume);
            }
        }
    }
}
