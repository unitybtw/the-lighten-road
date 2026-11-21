using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Player
{
    /// <summary>Frame-timed artist sprites driven by actual movement and lantern events; no root motion.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(PlayerMotor2D))]
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerAnimationCatalog catalog;
        [SerializeField] private SpriteRenderer target;
        [Tooltip("Visual-only grace for momentary ground-contact loss at seams; never grants physical support.")]
        [SerializeField, Range(0f, .2f)] private float groundAnimationGrace = .12f;
        [Tooltip("Minimum real drop from the last supported position before a walk-off shows an air pose.")]
        [SerializeField, Range(.02f, .3f)] private float fallPoseDrop = .16f;
        [Tooltip("Calm breathing speed multiplier for Idle so standing still feels atmospheric rather than jittery.")]
        [SerializeField, Range(.25f, 1f)] private float idleCadence = .48f;
        private PlayerMotor2D motor;
        private PlayerRespawnController recovery;
        private LanternController lantern;
        private LanternFuel fuel;
        private PlayerSpriteSequence sequence;
        private float elapsed;
        private bool oneShot, explicitContext, holdContext;
        private bool originalSquash, originalTilt;
        private Sprite originalSprite;
        private Vector3 originalScale, originalLightPosition;
        private Quaternion originalRotation;
        private int transferFrame = -1;
        private float unsupportedTime;
        private float supportedHeight;
        private bool hadSupport, jumpLaunched, airPoseShown;
        private Object pushOwner;
        private Object ladderOwner;
        private float ladderVerticalInput;
        private Collider2D activeCrateCollider;
        private int crateSideSign = 1;
        private bool wasPushing;
        private bool pushDustSpawned;
        private SpriteRenderer placedLanternRenderer;
        private static Sprite sharedPlacedLanternSprite;
        private PlayerActionWalkLayer actionWalkLayer;
        private float walkElapsed;
        private bool walkingAction;
        private bool hasLightAnchor;
        private int lightFacing;
        private LightAnimation lightAnimation;

        private struct PushDustSpeck
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 WorldVelocity;
            public float Life;
            public float MaxLife;
        }
        private PushDustSpeck[] pushDustPool;
        private static Sprite sharedDustSprite;

        public bool HasWalkingActionLayer => walkingAction && actionWalkLayer != null && actionWalkLayer.Legs.gameObject.activeSelf;
        public SpriteRenderer WalkingLegsRenderer => actionWalkLayer != null ? actionWalkLayer.Legs : null;
        public int WalkingLegFrame { get; private set; } = -1;
        public bool AutomaticTicks { get; set; } = true;
        public PlayerAnimationCatalog Catalog => catalog;
        public LightAnimation CurrentAnimation => sequence != null ? sequence.Kind : LightAnimation.Idle;
        public int CurrentFrame { get; private set; }
        public SpriteRenderer Renderer => target;
        public bool IsLanternPlacedOnCrate => placedLanternRenderer != null && placedLanternRenderer.enabled;
        public float IdleCadence => idleCadence;
        /// <summary>Smooth initial weight ramp: keeps crate steady while placing lantern (0..52%), then ramps smoothly to 1.0.</summary>
        public float PushSpeedMultiplier
        {
            get
            {
                if (CurrentAnimation != LightAnimation.PushStart || sequence == null || sequence.Duration <= 0.001f)
                    return 1f;
                float t = Mathf.Clamp01(elapsed / sequence.Duration);
                if (t < 0.52f) return 0.14f;
                float ramp = (t - 0.52f) / 0.48f;
                return Mathf.Lerp(0.14f, 1f, ramp * ramp);
            }
        }
        /// <summary>Contact owners report effort; they retain responsibility for collision and movement.</summary>
        public void SetPushing(Object owner, bool pushing, Collider2D crateCollider = null)
        {
            if (owner == null) return;
            if (pushing)
            {
                pushOwner = owner;
                if (crateCollider != null)
                {
                    activeCrateCollider = crateCollider;
                    crateSideSign = crateCollider.bounds.center.x >= transform.position.x ? 1 : -1;
                }
            }
            else if (pushOwner == owner)
            {
                pushOwner = null;
            }
        }
        /// <summary>Ladder traversal owners report active vertical climbing; animation runs forward/reverse or pauses cleanly on rungs.</summary>
        public void SetLadderClimbing(Object owner, bool climbing, float verticalInput = 0f)
        {
            if (owner == null) return;
            if (climbing)
            {
                ladderOwner = owner;
                ladderVerticalInput = verticalInput;
                pushOwner = null;
                activeCrateCollider = null;
                wasPushing = false;
                HidePlacedLantern();
            }
            else if (ladderOwner == owner)
            {
                ladderOwner = null;
                ladderVerticalInput = 0f;
            }
        }

        private void Awake()
        {
            motor = GetComponent<PlayerMotor2D>();
            recovery = GetComponent<PlayerRespawnController>();
            lantern = GetComponent<LanternController>();
            fuel = GetComponent<LanternFuel>();
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            if (catalog == null) catalog = Resources.Load<PlayerAnimationCatalog>(PlayerAnimationCatalog.ResourcePath);
        }
        private void OnEnable()
        {
            if (catalog == null || target == null) return;
            originalSprite = target.sprite;
            originalScale = target.transform.localScale;
            originalRotation = target.transform.localRotation;
            originalSquash = motor.EnableSquashAndStretch;
            originalTilt = motor.EnableKineticTilt;
            motor.EnableSquashAndStretch = motor.EnableKineticTilt = false;
            if (motor.LanternTransform != null) originalLightPosition = motor.LanternTransform.localPosition;
            motor.OnJumped += Jumped;
            motor.OnLanded += Landed;
            if (lantern != null) lantern.FlareChanged += FlareChanged;
            if (fuel != null) fuel.FuelChanged += FuelChanged;
            if (recovery != null) recovery.OnPlayerRespawned += Respawned;
            walkElapsed = 0;
            hasLightAnchor = false;
            ResetAirState();
            Select(LightAnimation.Idle, false, true);
            RenderFrame();
        }
        private void OnDisable()
        {
            pushOwner = null;
            ladderOwner = null;
            ladderVerticalInput = 0f;
            activeCrateCollider = null;
            wasPushing = false;
            pushDustSpawned = false;
            HidePushDust();
            HidePlacedLantern();
            ClearWalkingAction();
            if (motor == null || catalog == null || target == null) return;
            motor.OnJumped -= Jumped;
            motor.OnLanded -= Landed;
            if (lantern != null) lantern.FlareChanged -= FlareChanged;
            if (fuel != null) fuel.FuelChanged -= FuelChanged;
            if (recovery != null) recovery.OnPlayerRespawned -= Respawned;
            motor.EnableSquashAndStretch = originalSquash;
            motor.EnableKineticTilt = originalTilt;
            target.sprite = originalSprite;
            target.transform.localScale = originalScale;
            target.transform.localRotation = originalRotation;
            if (motor.LanternTransform != null) motor.LanternTransform.localPosition = originalLightPosition;
        }
        private void Jumped()
        {
            pushOwner = null;
            ladderOwner = null;
            ladderVerticalInput = 0f;
            activeCrateCollider = null;
            wasPushing = false;
            pushDustSpawned = false;
            HidePlacedLantern();
            explicitContext = holdContext = false;
            jumpLaunched = airPoseShown = true;
            Select(LightAnimation.Jump, true, true);
        }
        private void Landed(float impact)
        {
            // A small seam can produce a real contact event without ever showing an air pose.
            if (!explicitContext && airPoseShown) Select(LightAnimation.Landing, true, true);
            ResetAirState();
        }
        private void FlareChanged(bool active)
        {
            // Airborne legs retain the jump/fall pose; the light/fuel event still happens normally.
            if (active && IsAlive && !explicitContext && motor.IsGrounded) Select(LightAnimation.Flare, true, true);
        }
        private bool IsAlive => recovery == null || recovery.State == RespawnState.Alive;
        private void FuelChanged(float current, float capacity)
        {
            if (!IsAlive || !GameControls.GameplayAllowed || transferFrame == Time.frameCount) return;
            var keyboard = Keyboard.current; var pad = GameControls.ActivePad;
            bool interact = (keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                (pad != null && (pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame));
            if (interact) PlayEmberTransfer();
        }
        public void PlayEmberTransfer()
        {
            if (!IsAlive || explicitContext || CurrentAnimation == LightAnimation.Flare && oneShot) return;
            transferFrame = Time.frameCount;
            Select(LightAnimation.EmberSwap, true, true);
        }
        private void Respawned()
        {
            pushOwner = null;
            ladderOwner = null;
            ladderVerticalInput = 0f;
            activeCrateCollider = null;
            wasPushing = false;
            pushDustSpawned = false;
            HidePushDust();
            HidePlacedLantern();
            explicitContext = holdContext = false; transferFrame = -1;
            walkElapsed = 0;
            hasLightAnchor = false;
            ResetAirState();
            Select(LightAnimation.Idle, false, true); RenderFrame();
        }
        /// <summary>Visual hook for future climbing/stowing owners. It does not move or lock the player.</summary>
        public bool PlayContextAnimation(LightAnimation kind, bool holdLastFrame = false)
        {
            if (!IsAlive || catalog == null || catalog.Find(kind) == null) return false;
            explicitContext = true; holdContext = holdLastFrame;
            ResetAirState();
            Select(kind, true, true); return true;
        }
        public void ClearContextAnimation()
        { explicitContext = holdContext = false; oneShot = false; ResetAirState(); Select(LightAnimation.Idle, false, true); RenderFrame(); }

        /// <summary>Sample an owned context at an exact source time without advancing a second clock.</summary>
        public bool SampleContextPose(float sourceTime)
        {
            if (!explicitContext || sequence == null || float.IsNaN(sourceTime) || float.IsInfinity(sourceTime)) return false;
            elapsed = Mathf.Clamp(sourceTime, 0, sequence.Duration);
            RenderFrame();
            return true;
        }

        private void ResetAirState()
        {
            unsupportedTime = 0; jumpLaunched = airPoseShown = false;
            hadSupport = motor != null && motor.IsGrounded;
            supportedHeight = motor != null && motor.Body != null ? motor.Body.position.y : transform.position.y;
        }
        private void StartFallPose()
        {
            Select(LightAnimation.Jump, true, true);
            // A walk-off has no take-off anticipation. Hold the delivered airborne pose directly.
            elapsed = sequence != null ? sequence.Duration : 0;
        }

        private void LateUpdate() { if (AutomaticTicks && Time.timeScale > 0f) Tick(Time.deltaTime); }
        public void Tick(float deltaTime)
        {
            if (sequence == null || target == null || !IsAlive || deltaTime <= 0) return;
            walkingAction = false;
            if (explicitContext)
            {
                elapsed += deltaTime;
                if (elapsed >= sequence.Duration && !holdContext)
                { explicitContext = false; oneShot = false; }
            }
            else if (ladderOwner != null && catalog != null && catalog.Find(LightAnimation.LadderClimb) != null)
            {
                oneShot = false;
                ResetAirState();
                Select(LightAnimation.LadderClimb, false, false);
                float climbDir = Mathf.Abs(ladderVerticalInput) > 0.1f ? Mathf.Sign(ladderVerticalInput) : 0f;
                if (climbDir != 0f && sequence != null && sequence.Duration > 0.001f)
                {
                    elapsed += deltaTime * 1.15f * climbDir;
                    while (elapsed < 0f) elapsed += sequence.Duration;
                    while (elapsed >= sequence.Duration) elapsed -= sequence.Duration;
                }
            }
            else
            {
                bool unsupported = motor.enabled && !motor.IsGrounded;
                unsupportedTime = unsupported ? unsupportedTime + deltaTime : 0;
                bool dropped = !hadSupport || (motor.Body.linearVelocity.y < -.5f && motor.Body.position.y < supportedHeight - fallPoseDrop);
                bool airborne = unsupported && (jumpLaunched || airPoseShown || (unsupportedTime >= groundAnimationGrace && dropped));
                bool visuallySupported = motor.IsGrounded || (unsupported && !airborne && hadSupport);
                bool moving = visuallySupported && Mathf.Abs(motor.HorizontalInput) > .05f && Mathf.Abs(motor.HorizontalSpeed) > .08f;
                bool pushing = pushOwner != null && visuallySupported && Mathf.Abs(motor.HorizontalInput) > .05f;
                bool walkingAway = visuallySupported && Mathf.Abs(motor.HorizontalInput) > .05f && Mathf.Sign(motor.HorizontalInput) != crateSideSign;
                if (airborne) { airPoseShown = true; hadSupport = false; wasPushing = false; pushDustSpawned = false; }
                else if (motor.IsGrounded) { hadSupport = true; supportedHeight = motor.Body.position.y; jumpLaunched = airPoseShown = false; }
                if (airborne && CurrentAnimation != LightAnimation.Jump)
                    StartFallPose();

                if (oneShot && CurrentAnimation == LightAnimation.PushStart)
                {
                    if (!pushing)
                    {
                        if (!walkingAway && elapsed >= 0.08f && catalog.Find(LightAnimation.PushEnd) != null)
                            Select(LightAnimation.PushEnd, true, true);
                        else
                        {
                            oneShot = false;
                            activeCrateCollider = null;
                        }
                    }
                    else if (!pushDustSpawned && CurrentFrame >= 4)
                    {
                        pushDustSpawned = true;
                        SpawnPushContactDust();
                    }
                }
                else if (oneShot && CurrentAnimation == LightAnimation.PushEnd)
                {
                    if (pushing || walkingAway)
                    {
                        oneShot = false;
                        if (!pushing) activeCrateCollider = null;
                    }
                }

                if (moving && oneShot && (CurrentAnimation == LightAnimation.Landing || CurrentAnimation == LightAnimation.EmberSwap)) oneShot = false;
                if (!oneShot)
                {
                    if (airborne) { activeCrateCollider = null; StartFallPose(); }
                    else if (pushing)
                    {
                        if (!wasPushing && CurrentAnimation != LightAnimation.Push && catalog.Find(LightAnimation.PushStart) != null)
                        {
                            pushDustSpawned = false;
                            Select(LightAnimation.PushStart, true, true);
                        }
                        else Select(LightAnimation.Push, false, false);
                    }
                    else if (wasPushing && CurrentAnimation == LightAnimation.Push && !walkingAway && catalog.Find(LightAnimation.PushEnd) != null)
                    {
                        Select(LightAnimation.PushEnd, true, true);
                    }
                    else
                    {
                        if (pushOwner == null) activeCrateCollider = null;
                        Select(moving ? LightAnimation.Walk : LightAnimation.Idle, false, false);
                    }
                }
                wasPushing = pushing;
                // At normal walk speed, retain the artist's 100ms frames. Slow
                // analog movement scales proportionally; Idle breathes at a calm cadence.
                float gaitRate = Mathf.Abs(motor.HorizontalSpeed) / Mathf.Max(.1f, motor.WalkSpeed);
                float speed = CurrentAnimation == LightAnimation.Push ? Mathf.Clamp(Mathf.Abs(motor.HorizontalSpeed) / 1.2f, .65f, 1.1f)
                    : CurrentAnimation == LightAnimation.Walk ? gaitRate
                    : CurrentAnimation == LightAnimation.Idle ? idleCadence
                    : 1f;
                elapsed += deltaTime * speed;
                if (moving)
                {
                    if (CurrentAnimation == LightAnimation.Walk) walkElapsed = elapsed;
                    else walkElapsed += deltaTime * gaitRate;
                }
                else if (CurrentAnimation == LightAnimation.Idle) walkElapsed = 0;
                walkingAction = moving && oneShot && CurrentAnimation == LightAnimation.Flare;
                if (oneShot && elapsed >= sequence.Duration)
                {
                    if (CurrentAnimation == LightAnimation.PushStart && pushing)
                    {
                        Select(LightAnimation.Push, false, true);
                    }
                    else if (!airborne || CurrentAnimation != LightAnimation.Jump)
                    {
                        oneShot = false;
                    }
                }
            }
            TickPushDust(deltaTime);
            RenderFrame(deltaTime);
        }
        private void Select(LightAnimation kind, bool once, bool restart)
        {
            if (catalog == null || (!restart && sequence != null && sequence.Kind == kind)) return;
            var next = catalog.Find(kind);
            if (next == null || next.Frames == null || next.Frames.Length == 0) return;
            ClearWalkingAction();
            sequence = next; elapsed = !restart && kind == LightAnimation.Walk ? walkElapsed : 0;
            oneShot = once; CurrentFrame = 0;
        }
        private void ClearWalkingAction()
        {
            walkingAction = false; WalkingLegFrame = -1;
            if (actionWalkLayer != null) actionWalkLayer.Clear();
        }
        private void OnDestroy()
        {
            if (actionWalkLayer != null) actionWalkLayer.Dispose();
            DestroyPushDust();
            if (placedLanternRenderer != null) Destroy(placedLanternRenderer.gameObject);
        }
        private static bool IsPushTransitionFamily(LightAnimation kind)
            => kind == LightAnimation.Idle || kind == LightAnimation.Walk || kind == LightAnimation.PushStart || kind == LightAnimation.Push || kind == LightAnimation.PushEnd;

        private bool ShouldShowWorldLantern()
        {
            if (activeCrateCollider == null || !activeCrateCollider.gameObject.activeInHierarchy || explicitContext) return false;
            return CurrentAnimation == LightAnimation.PushStart
                || CurrentAnimation == LightAnimation.Push
                || CurrentAnimation == LightAnimation.PushEnd;
        }

        private Vector3 GetRaisedHandLanternWorldPos()
        {
            // Anchor to Light's exact Idle[0] hand lantern position so handoff between world lantern and Idle/Walk is seamless.
            Vector2 offset = new Vector2(13.5f / 36f, 16.1f / 36f);
            var idleSeq = catalog != null ? catalog.Find(LightAnimation.Idle) : null;
            if (idleSeq != null && idleSeq.LanternOffsets != null && idleSeq.LanternOffsets.Length > 0)
            {
                offset = idleSeq.LanternOffsets[0];
            }
            float flip = target != null && target.flipX ? -1f : 1f;
            Vector3 local = new Vector3(offset.x * flip, offset.y - 0.11f, 0f);
            return target != null ? target.transform.TransformPoint(local) : transform.TransformPoint(local);
        }

        private Vector3 UpdatePlacedLanternOnCrate()
        {
            if (!ShouldShowWorldLantern() || target == null)
            {
                HidePlacedLantern();
                return Vector3.zero;
            }

            EnsurePlacedLantern();
            var bounds = activeCrateCollider.bounds;
            var crateSr = activeCrateCollider.GetComponent<SpriteRenderer>();
            float edgeX = crateSideSign > 0 ? bounds.min.x + 0.16f : bounds.max.x - 0.16f;
            float lanternX = Mathf.Lerp(bounds.center.x, edgeX, 0.45f);
            Vector3 crateRestPos = new Vector3(lanternX, bounds.max.y - 0.02f, target.transform.position.z);
            Vector3 handPos = GetRaisedHandLanternWorldPos();
            handPos.z = target.transform.position.z;

            Vector3 worldPos = crateRestPos;
            if (CurrentAnimation == LightAnimation.PushStart && sequence != null && sequence.Duration > 0.001f)
            {
                // First ~52% of PushStart: visibly arc the exact hand lantern over the lip and set it down onto the crate top.
                float placeT = Mathf.Clamp01((elapsed / sequence.Duration) / 0.52f);
                float smooth = placeT * placeT * (3f - 2f * placeT);
                float arcLift = Mathf.Sin(placeT * Mathf.PI) * 0.16f;
                worldPos = Vector3.Lerp(handPos, crateRestPos, smooth) + new Vector3(0f, arcLift, 0f);
            }
            else if (CurrentAnimation == LightAnimation.PushEnd && sequence != null && sequence.Duration > 0.001f)
            {
                // First ~35% of PushEnd: lantern rests on crate while hand reaches up; then smoothly lifts back into hand!
                float norm = elapsed / sequence.Duration;
                float pickT = Mathf.Clamp01((norm - 0.35f) / 0.62f);
                float smooth = pickT * pickT * (3f - 2f * pickT);
                float arcLift = Mathf.Sin(pickT * Mathf.PI) * 0.14f;
                worldPos = Vector3.Lerp(crateRestPos, handPos, smooth) + new Vector3(0f, arcLift, 0f);
            }

            placedLanternRenderer.transform.position = worldPos;
            placedLanternRenderer.transform.localScale = target.transform.lossyScale;
            placedLanternRenderer.sortingLayerID = target.sortingLayerID;
            placedLanternRenderer.sortingOrder = Mathf.Max(target.sortingOrder, crateSr != null ? crateSr.sortingOrder : 0) + 3;
            if (target.sharedMaterial != null) placedLanternRenderer.sharedMaterial = target.sharedMaterial;
            placedLanternRenderer.color = Color.white;
            placedLanternRenderer.enabled = true;
            return worldPos;
        }

        private void HidePlacedLantern()
        {
            if (placedLanternRenderer != null) placedLanternRenderer.enabled = false;
        }

        private void EnsurePlacedLantern()
        {
            if (sharedPlacedLanternSprite == null || sharedPlacedLanternSprite.texture == null)
            {
                sharedPlacedLanternSprite = BuildPlacedLanternSprite();
            }
            if (placedLanternRenderer != null)
            {
                if (placedLanternRenderer.sprite == null || placedLanternRenderer.sprite.texture == null)
                    placedLanternRenderer.sprite = sharedPlacedLanternSprite;
                return;
            }
            var go = new GameObject("PlacedCrateLantern");
            go.hideFlags = HideFlags.DontSave;
            placedLanternRenderer = go.AddComponent<SpriteRenderer>();
            placedLanternRenderer.sprite = sharedPlacedLanternSprite;
            placedLanternRenderer.enabled = false;
        }

        private static Sprite BuildPlacedLanternSprite()
        {
            // Verbatim 1:1 pixel match with Light's hand lantern from Idle.png (x=46..50, y=15..22, 5x8 @ 36 PPU).
            const int w = 5, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[w * h];
            Color32 blk = new Color32(0, 0, 0, 255);
            Color32 yel = new Color32(251, 242, 54, 255);
            Color32 clr = new Color32(0, 0, 0, 0);

            // Rows bottom (y=0 -> Idle y=22) to top (y=7 -> Idle y=15)
            string[] pattern =
            {
                "..#..", // y=0 (Idle y=22) pointed base tip
                ".###.", // y=1 (Idle y=21) lower frame
                "##*##", // y=2 (Idle y=20) lower flame
                "##*##", // y=3 (Idle y=19) mid flame
                "#*#*#", // y=4 (Idle y=18) iconic hollow-center flame diamond
                "##*##", // y=5 (Idle y=17) upper flame
                ".####", // y=6 (Idle y=16) lantern hood
                ".###."  // y=7 (Idle y=15) top ring
            };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    char c = pattern[y][x];
                    px[y * w + x] = c == '#' ? blk : c == '*' ? yel : clr;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 36f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void SpawnPushContactDust()
        {
            if (target == null) return;
            EnsurePushDustPool();
            float dir = motor != null && motor.FacingDirection < 0 ? -1f : 1f;
            Vector3 basePos = transform.position + new Vector3(dir * 0.34f, -0.78f, 0f);
            for (int i = 0; i < pushDustPool.Length; i++)
            {
                var speck = pushDustPool[i];
                if (speck.Transform == null || speck.Renderer == null) continue;
                float spread = (i - 1.5f) * 0.09f;
                speck.Transform.position = basePos + new Vector3(dir * (i * 0.06f), (i % 2) * 0.05f, 0f);
                speck.WorldVelocity = new Vector3(-dir * (0.45f + i * 0.14f) + spread, 0.55f + (i % 3) * 0.18f, 0f);
                speck.Life = 0f;
                speck.MaxLife = 0.22f + (i % 2) * 0.08f;
                speck.Renderer.sortingLayerID = target.sortingLayerID;
                speck.Renderer.sortingOrder = target.sortingOrder + 1;
                speck.Renderer.color = new Color(0.84f, 0.78f, 0.66f, 0.78f);
                speck.Renderer.enabled = true;
                pushDustPool[i] = speck;
            }
        }

        private void TickPushDust(float dt)
        {
            if (pushDustPool == null) return;
            for (int i = 0; i < pushDustPool.Length; i++)
            {
                var speck = pushDustPool[i];
                if (speck.Renderer == null || !speck.Renderer.enabled) continue;
                speck.Life += dt;
                if (speck.Life >= speck.MaxLife)
                {
                    speck.Renderer.enabled = false;
                }
                else
                {
                    float t = speck.Life / speck.MaxLife;
                    speck.Transform.position += speck.WorldVelocity * dt;
                    speck.WorldVelocity *= Mathf.Exp(-4.5f * dt);
                    speck.Renderer.color = new Color(0.84f, 0.78f, 0.66f, 0.78f * (1f - t));
                }
                pushDustPool[i] = speck;
            }
        }

        private void HidePushDust()
        {
            if (pushDustPool == null) return;
            for (int i = 0; i < pushDustPool.Length; i++)
                if (pushDustPool[i].Renderer != null) pushDustPool[i].Renderer.enabled = false;
        }

        private void DestroyPushDust()
        {
            if (pushDustPool == null) return;
            for (int i = 0; i < pushDustPool.Length; i++)
                if (pushDustPool[i].Transform != null) Destroy(pushDustPool[i].Transform.gameObject);
            pushDustPool = null;
        }

        private void EnsurePushDustPool()
        {
            if (sharedDustSprite == null || sharedDustSprite.texture == null)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                tex.Apply(false, true);
                sharedDustSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 36f);
                sharedDustSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            if (pushDustPool != null) return;
            pushDustPool = new PushDustSpeck[4];
            for (int i = 0; i < pushDustPool.Length; i++)
            {
                var go = new GameObject("PushDust_" + i);
                go.hideFlags = HideFlags.DontSave;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sharedDustSprite;
                sr.enabled = false;
                pushDustPool[i] = new PushDustSpeck { Transform = go.transform, Renderer = sr };
            }
        }
        private void RenderFrame(float deltaTime = 0f)
        {
            if (sequence == null || target == null) return;
            CurrentFrame = sequence.FrameAt(elapsed);
            if (CurrentFrame < 0) return;
            target.sprite = sequence.Frames[CurrentFrame];
            target.flipX = CurrentAnimation != LightAnimation.LadderClimb && motor.FacingDirection < 0;
            if (walkingAction && !explicitContext && CurrentAnimation == LightAnimation.Flare)
            {
                var walk = catalog.Find(LightAnimation.Walk);
                if (walk != null && walk.Frames != null && walk.Frames.Length > 0)
                {
                    if (actionWalkLayer == null) actionWalkLayer = new PlayerActionWalkLayer(target);
                    WalkingLegFrame = walk.FrameAt(walkElapsed);
                    float upperWaist = sequence.WaistOffsetsX != null && CurrentFrame < sequence.WaistOffsetsX.Length ? sequence.WaistOffsetsX[CurrentFrame] : 0;
                    float lowerWaist = walk.WaistOffsetsX != null && WalkingLegFrame < walk.WaistOffsetsX.Length ? walk.WaistOffsetsX[WalkingLegFrame] : 0;
                    actionWalkLayer.Show(sequence.Frames[CurrentFrame], walk.Frames[WalkingLegFrame], upperWaist - lowerWaist);
                }
            }
            else ClearWalkingAction();
            Vector3 placedWorldPos = UpdatePlacedLanternOnCrate();
            if (motor.LanternTransform != null && sequence.LanternOffsets != null && CurrentFrame < sequence.LanternOffsets.Length)
            {
                Vector3 position;
                if (IsLanternPlacedOnCrate)
                {
                    // Dynamic lantern light rides smoothly on top of the crate while pushing!
                    position = transform.InverseTransformPoint(placedWorldPos + new Vector3(0f, 0.11f, 0f));
                }
                else
                {
                    var offset = sequence.LanternOffsets[CurrentFrame];
                    // Convert through the sprite's transform so prefab visual offsets/scales remain valid.
                    var local = new Vector3(offset.x * (target.flipX ? -1 : 1), offset.y, 0);
                    position = transform.InverseTransformPoint(target.transform.TransformPoint(local));
                }
                position.z = originalLightPosition.z;
                var previous = motor.LanternTransform.localPosition;
                // The artwork keeps its authored cadence. Only the light between its
                // hand/crate markers settles smoothly across push transitions and calm Idle.
                bool smoothCrossAnim = IsPushTransitionFamily(lightAnimation) && IsPushTransitionFamily(CurrentAnimation);
                bool snap = !hasLightAnchor || deltaTime <= 0f || explicitContext ||
                    lightFacing != motor.FacingDirection ||
                    (!smoothCrossAnim && lightAnimation != CurrentAnimation) ||
                    (position - previous).sqrMagnitude > 2.25f;
                float settleTau = CurrentAnimation == LightAnimation.Idle ? 0.085f : (smoothCrossAnim ? 0.045f : 0.035f);
                motor.LanternTransform.localPosition = snap ? position
                    : Vector3.Lerp(previous, position, 1f - Mathf.Exp(-deltaTime / settleTau));
                hasLightAnchor = true;
                lightFacing = motor.FacingDirection;
                lightAnimation = CurrentAnimation;
            }
        }
    }
}
