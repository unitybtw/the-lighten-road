using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public enum WhisperAction
    {
        Jump = 0,
        Flare = 1,
        Sprint = 2,
        Interact = 3,
        Move = 4,
        ProximityOnly = 5
    }

    /// <summary>
    /// In-world diegetic ancient guidance whisper that hovers over altars, gaps, and mechanisms.
    /// Features ethereal unscaled bobbing, soft radial lantern glow, ornate runic keycaps,
    /// dynamic player proximity detection, and a shimmering dissolution burst upon action fulfillment.
    /// Inspired by the visual and mechanical language of Ori and the Blind Forest and Hollow Knight.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class DiegeticWhisper : MonoBehaviour
    {
        [Header("Whisper Message")]
        [SerializeField] private string keyHint = "SPACE";
        [SerializeField] private string actionPrompt = "LEAP ACROSS THE ABYSS";
        [SerializeField] private WhisperAction requiredAction = WhisperAction.Jump;

        [Header("Proximity & Transition")]
        [SerializeField, Min(1f)] private float triggerRadius = 4.2f;
        [SerializeField, Min(0.5f)] private float fadeSpeed = 3.8f;
        [SerializeField] private bool autoDissipateOnAction = true;
        [SerializeField] private float proximityDurationRequired = 2.5f;

        [Header("Persistence")]
        [Tooltip("Optional unique identifier to remember completion across attempts.")]
        [SerializeField] private string whisperId = "";
        [SerializeField] private bool persistCompletion = false;
        [SerializeField] private bool destroyOnDissolve = false;

        [Header("Visual Palette")]
        [SerializeField] private Color radiantIvory = new Color(0.97f, 0.95f, 0.89f, 1.0f);
        [SerializeField] private Color amberGold = new Color(1.0f, 0.72f, 0.28f, 1.0f);
        [SerializeField] private Color glowColor = new Color(1.0f, 0.65f, 0.20f, 0.40f);

        private Canvas worldCanvas;
        private CanvasGroup canvasGroup;
        private Image glowImage;
        private Image keycapBg;
        private TextMeshProUGUI keycapText;
        private TextMeshProUGUI promptText;

        private PlayerMotor2D playerMotor;
        private LanternController playerLantern;
        private Transform playerTransform;

        private Vector3 initialLocalPos;
        private float proximityTimer;
        private float dissolveTimer;
        private bool isCompleted;
        private bool isDissolving;
        private float currentGlowScale = 1.0f;

        public string KeyHint
        {
            get => keyHint;
            set
            {
                keyHint = value;
                RefreshText();
            }
        }

        public string ActionPrompt
        {
            get => actionPrompt;
            set
            {
                actionPrompt = value;
                RefreshText();
            }
        }

        public WhisperAction RequiredAction
        {
            get => requiredAction;
            set => requiredAction = value;
        }

        public float TriggerRadius
        {
            get => triggerRadius;
            set => triggerRadius = value;
        }

        public float FadeSpeed
        {
            get => fadeSpeed;
            set => fadeSpeed = value;
        }

        public bool IsCompleted => isCompleted;
        public bool IsDissolving => isDissolving;
        public float CurrentAlpha => canvasGroup != null ? canvasGroup.alpha : 0f;
        public bool IsRevealed => CurrentAlpha > 0.5f;

        public void SetAlpha(float a)
        {
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(a);
        }

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            CheckPersistence();
            BuildWorldCanvas();
            RefreshText();
        }

        private void OnEnable()
        {
            FindPlayerReferences();
            if (playerMotor != null)
            {
                playerMotor.OnJumped -= HandlePlayerJumped;
                playerMotor.OnJumped += HandlePlayerJumped;
            }
            if (playerLantern != null)
            {
                playerLantern.FlareChanged -= HandlePlayerFlareChanged;
                playerLantern.FlareChanged += HandlePlayerFlareChanged;
            }
        }

        private void OnDisable()
        {
            if (playerMotor != null)
            {
                playerMotor.OnJumped -= HandlePlayerJumped;
            }
            if (playerLantern != null)
            {
                playerLantern.FlareChanged -= HandlePlayerFlareChanged;
            }
        }

        public void EnsureReferences()
        {
            if (canvasGroup == null) BuildWorldCanvas();
            FindPlayerReferences();
        }

        private void CheckPersistence()
        {
            if (persistCompletion && !string.IsNullOrEmpty(whisperId))
            {
                if (PlayerPrefs.GetInt("TLR_Whisper_" + whisperId, 0) == 1)
                {
                    isCompleted = true;
                    if (destroyOnDissolve)
                    {
                        Destroy(gameObject);
                    }
                    else if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 0f;
                        gameObject.SetActive(false);
                    }
                }
            }
        }

        private void FindPlayerReferences()
        {
            if (playerMotor == null)
            {
                playerMotor = Object.FindAnyObjectByType<PlayerMotor2D>();
            }
            if (playerMotor != null)
            {
                playerTransform = playerMotor.transform;
                if (playerLantern == null)
                {
                    playerLantern = playerMotor.GetComponent<LanternController>()
                                    ?? playerMotor.GetComponentInChildren<LanternController>();
                }
            }
        }

        private void BuildWorldCanvas()
        {
            if (worldCanvas != null) return;

            var canvasGo = new GameObject("WhisperWorldCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = Vector3.zero;

            worldCanvas = canvasGo.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 50;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 20f;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 100f);
            rect.localScale = Vector3.one * 0.012f;

            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Background soft radial amber glow
            var glowGo = new GameObject("GlowAura");
            glowGo.transform.SetParent(canvasGo.transform, false);
            var glowRect = glowGo.AddComponent<RectTransform>();
            glowRect.sizeDelta = new Vector2(260f, 90f);
            glowImage = glowGo.AddComponent<Image>();
            glowImage.sprite = CreateSoftRadialSprite(128);
            glowImage.color = glowColor;

            // Keycap container
            var badgeGo = new GameObject("KeycapBadge");
            badgeGo.transform.SetParent(canvasGo.transform, false);
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchoredPosition = new Vector2(0f, 16f);
            badgeRect.sizeDelta = new Vector2(90f, 32f);

            keycapBg = badgeGo.AddComponent<Image>();
            keycapBg.sprite = CreateOrnateFiligreeSprite(256, 64);
            keycapBg.type = Image.Type.Sliced;
            keycapBg.color = new Color(0.04f, 0.06f, 0.11f, 0.88f);

            var keyTextGo = new GameObject("KeyText");
            keyTextGo.transform.SetParent(badgeGo.transform, false);
            var keyTextRect = keyTextGo.AddComponent<RectTransform>();
            keyTextRect.anchorMin = Vector2.zero;
            keyTextRect.anchorMax = Vector2.one;
            keyTextRect.sizeDelta = Vector2.zero;

            keycapText = keyTextGo.AddComponent<TextMeshProUGUI>();
            keycapText.alignment = TextAlignmentOptions.Center;
            keycapText.fontSize = 17f;
            keycapText.fontStyle = FontStyles.Bold;
            keycapText.color = amberGold;
            keycapText.characterSpacing = 15f;
            keycapText.raycastTarget = false;

            // Action prompt text (sub-whisper)
            var promptGo = new GameObject("PromptText");
            promptGo.transform.SetParent(canvasGo.transform, false);
            var promptRect = promptGo.AddComponent<RectTransform>();
            promptRect.anchoredPosition = new Vector2(0f, -20f);
            promptRect.sizeDelta = new Vector2(280f, 30f);

            promptText = promptGo.AddComponent<TextMeshProUGUI>();
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 12.5f;
            promptText.fontStyle = FontStyles.Normal;
            promptText.color = radiantIvory;
            promptText.characterSpacing = 12f;
            promptText.raycastTarget = false;
        }

        private void RefreshText()
        {
            if (keycapText != null)
            {
                keycapText.text = keyHint;
                if (keycapBg != null)
                {
                    float width = Mathf.Max(70f, keyHint.Length * 16f + 30f);
                    keycapBg.rectTransform.sizeDelta = new Vector2(width, 32f);
                }
            }
            if (promptText != null)
            {
                promptText.text = actionPrompt;
            }
        }

        private void Update()
        {
            TickWhisper(Time.deltaTime);
        }

        public void TickWhisper(float dt)
        {
            if (isCompleted && !isDissolving)
            {
                if (canvasGroup != null && canvasGroup.alpha > 0f)
                {
                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, fadeSpeed * dt);
                }
                return;
            }

            FindPlayerReferences();

            // Sinuous organic floating bob
            transform.localPosition = initialLocalPos + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.08f);

            // Shimmering dissolution sequence
            if (isDissolving)
            {
                dissolveTimer += dt;
                float progress = Mathf.Clamp01(dissolveTimer / 0.35f);

                transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.35f, progress);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(1.0f, 0f, progress * progress);
                }

                if (progress >= 1.0f)
                {
                    isDissolving = false;
                    isCompleted = true;
                    if (destroyOnDissolve)
                    {
                        Destroy(gameObject);
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }
                }
                return;
            }

            if (playerTransform == null) return;

            float distance = Vector2.Distance(transform.position, playerTransform.position);
            bool inRange = distance <= triggerRadius;

            if (inRange)
            {
                CheckContinuousActions(dt);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1.0f, fadeSpeed * dt);
                }

                // Breathing glow aura
                currentGlowScale = 1.0f + 0.12f * Mathf.Sin(Time.time * 2.5f);
                if (glowImage != null)
                {
                    glowImage.rectTransform.localScale = Vector3.one * currentGlowScale;
                    glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, (0.35f + 0.15f * Mathf.Sin(Time.time * 2.8f)) * CurrentAlpha);
                }
            }
            else
            {
                proximityTimer = 0f;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, fadeSpeed * dt);
                }
            }
        }

        private void CheckContinuousActions(float dt)
        {
            var kb = Keyboard.current;

            switch (requiredAction)
            {
                case WhisperAction.Sprint:
                    if ((playerMotor != null && playerMotor.IsSprinting) || (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)))
                    {
                        TriggerActionFulfillment();
                    }
                    break;

                case WhisperAction.Interact:
                    if (kb != null && kb.eKey.wasPressedThisFrame)
                    {
                        TriggerActionFulfillment();
                    }
                    break;

                case WhisperAction.Move:
                    if (playerMotor != null && Mathf.Abs(playerMotor.HorizontalInput) > 0.35f)
                    {
                        TriggerActionFulfillment();
                    }
                    else if (kb != null && (kb.aKey.isPressed || kb.dKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed))
                    {
                        TriggerActionFulfillment();
                    }
                    break;

                case WhisperAction.ProximityOnly:
                    proximityTimer += dt;
                    if (proximityTimer >= proximityDurationRequired)
                    {
                        TriggerActionFulfillment();
                    }
                    break;
            }
        }

        private void HandlePlayerJumped()
        {
            if (isCompleted || isDissolving) return;
            if (requiredAction == WhisperAction.Jump)
            {
                if (playerTransform != null && Vector2.Distance(transform.position, playerTransform.position) <= triggerRadius)
                {
                    TriggerActionFulfillment();
                }
            }
        }

        private void HandlePlayerFlareChanged(bool isFlared)
        {
            if (isCompleted || isDissolving) return;
            if (requiredAction == WhisperAction.Flare && isFlared)
            {
                if (playerTransform != null && Vector2.Distance(transform.position, playerTransform.position) <= triggerRadius)
                {
                    TriggerActionFulfillment();
                }
            }
        }

        public void TriggerActionFulfillment()
        {
            if (isCompleted || isDissolving) return;

            if (autoDissipateOnAction)
            {
                Dissipate();
            }
        }

        public void Dissipate()
        {
            if (isCompleted || isDissolving) return;

            isDissolving = true;
            dissolveTimer = 0f;

            // Audio celebration
            SoundEffectManager.Instance?.PlayEmberPickup();

            // Record persistence
            if (persistCompletion && !string.IsNullOrEmpty(whisperId))
            {
                PlayerPrefs.SetInt("TLR_Whisper_" + whisperId, 1);
                PlayerPrefs.Save();
            }
        }

        public void ResetWhisper()
        {
            isCompleted = false;
            isDissolving = false;
            dissolveTimer = 0f;
            proximityTimer = 0f;
            transform.localScale = Vector3.one;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(whisperId))
            {
                PlayerPrefs.DeleteKey("TLR_Whisper_" + whisperId);
            }
        }

        public static DiegeticWhisper Spawn(
            Vector3 worldPos,
            string hint,
            string prompt,
            WhisperAction action,
            string id = "",
            Transform parent = null)
        {
            var go = new GameObject($"Whisper_{hint.Replace(" ", "_")}");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            var whisper = go.AddComponent<DiegeticWhisper>();
            whisper.keyHint = hint;
            whisper.actionPrompt = prompt;
            whisper.requiredAction = action;
            whisper.whisperId = id;
            whisper.persistCompletion = !string.IsNullOrEmpty(id);

            whisper.EnsureReferences();
            whisper.RefreshText();
            return whisper;
        }

        private static Sprite CreateSoftRadialSprite(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        private static Sprite CreateOrnateFiligreeSprite(int width = 256, int height = 64)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color clear = new Color(0, 0, 0, 0);
            Color border = new Color(1.0f, 0.72f, 0.28f, 0.95f);
            Color fill = new Color(0.04f, 0.06f, 0.11f, 0.88f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = (x < 3 || x >= width - 3 || y < 3 || y >= height - 3);
                    tex.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            Vector4 borderInsets = new Vector4(8, 8, 8, 8);
            return Sprite.Create(tex, new Rect(0, 0, width, height), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, borderInsets);
        }
    }
}
