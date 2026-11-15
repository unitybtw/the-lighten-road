using System;
using System.Collections.Generic;
using System.Reflection;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Coordinates SiracLevel1 traversal rules:
    /// - Calibrates 1x2 box jump and 2-box ledge grab reach
    /// - Ladder climbing via W / S
    /// - Flare freezes character in place, keeps Embers non-consumed, and pans camera horizontally via A / D
    /// </summary>
    public sealed class Level1TraversalRig : MonoBehaviour
    {
        [Header("Jump & Ledge Calibration")]
        [SerializeField, Min(0.1f)] private float calibratedWalkSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float calibratedJumpSpeed = 8.4f;
        [SerializeField, Min(0.5f)] private float calibratedLedgeMaxHeight = 2.05f;

        [Header("Flare Camera Pan")]
        [SerializeField, Min(0.5f)] private float panSpeed = 6.5f;
        [SerializeField, Min(0.5f)] private float maxPanOffset = 8.0f;
        [SerializeField] private bool preventEmberConsumption = true;

        private PlayerMotor2D motor;
        private PlayerLedgeClimb ledgeClimb;
        private Rigidbody2D body;
        private LanternController lantern;
        private LanternFuel fuel;
        private FollowCamera2D followCamera;
        private LanternLensController lensController;

        private float baseGravityScale = 3f;
        private Vector3 baseCameraOffset = new Vector3(0f, 1.25f, -10f);
        private bool hasBaseCameraOffset;
        private bool wasFlareFreezeApplied;
        private float cachedFuelBeforeFlare = -1f;

        private static readonly FieldInfo MaxLipHeightField =
            typeof(PlayerLedgeClimb).GetField("maximumLipHeight", BindingFlags.Instance | BindingFlags.NonPublic);

        public bool PreventEmberConsumption
        {
            get => preventEmberConsumption;
            set => preventEmberConsumption = value;
        }

        public bool IsClimbingLadder { get; private set; }
        public ClimbableLadder2D ActiveLadder { get; private set; }
        public bool IsCharacterFrozenByFlare { get; private set; }
        public bool IsFlarePanning => IsCharacterFrozenByFlare;
        public float CurrentPanOffsetX { get; private set; }
        public float CalibratedLedgeMaxHeight => calibratedLedgeMaxHeight;
        public float CalibratedJumpSpeed => calibratedJumpSpeed;

        private void Awake()
        {
            CacheReferences();
            ApplyCalibration();
        }

        private void OnEnable()
        {
            CacheReferences();
            if (lantern != null)
            {
                lantern.FlareChanged -= HandleFlareChanged;
                lantern.FlareChanged += HandleFlareChanged;
            }
        }

        private void OnDisable()
        {
            if (lantern != null)
            {
                lantern.FlareChanged -= HandleFlareChanged;
            }
            if (IsCharacterFrozenByFlare)
            {
                ReleaseFlareFreeze();
            }
            if (IsClimbingLadder)
            {
                StopClimbing();
            }
        }

        private void CacheReferences()
        {
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (ledgeClimb == null) ledgeClimb = GetComponent<PlayerLedgeClimb>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (lantern == null) lantern = GetComponent<LanternController>();
            if (fuel == null) fuel = GetComponent<LanternFuel>();
            if (lensController == null) lensController = GetComponent<LanternLensController>();
            if (followCamera == null) followCamera = FollowCamera2D.ActiveInstance ?? FindFirstObjectByType<FollowCamera2D>();

            if (body != null && body.gravityScale > 0.01f)
            {
                baseGravityScale = body.gravityScale;
            }
            if (followCamera != null && !hasBaseCameraOffset)
            {
                baseCameraOffset = followCamera.Offset;
                hasBaseCameraOffset = true;
            }
        }

        public void ApplyCalibration()
        {
            if (motor != null)
            {
                motor.WalkSpeed = calibratedWalkSpeed;
                motor.JumpSpeed = calibratedJumpSpeed;
            }

            if (ledgeClimb != null && MaxLipHeightField != null)
            {
                MaxLipHeightField.SetValue(ledgeClimb, calibratedLedgeMaxHeight);
            }
        }

        private void Update()
        {
            CacheReferences();

            if (!GameControls.GameplayAllowed) return;

            var kb = Keyboard.current;
            var pad = GameControls.ActivePad;

            // Shadow Realm suppresses Flare completely
            if (lensController != null && lensController.IsSuppressedByShadowRealm)
            {
                if (lantern != null && lantern.IsFlared)
                {
                    lantern.ResetToDim();
                }
                if (IsCharacterFrozenByFlare)
                {
                    ReleaseFlareFreeze();
                }
            }
            else if (lantern != null)
            {
                if (lantern.IsFlared && !IsCharacterFrozenByFlare)
                {
                    EnterFlareFreeze();
                }
                else if (!lantern.IsFlared && IsCharacterFrozenByFlare)
                {
                    ReleaseFlareFreeze();
                }
            }

            // Track baseline fuel right before Flare spends a stone
            if (fuel != null && (lantern == null || !lantern.IsFlared))
            {
                cachedFuelBeforeFlare = fuel.CurrentFuel;
            }

            if (IsCharacterFrozenByFlare)
            {
                if (body != null)
                {
                    body.linearVelocity = new Vector2(0f, Mathf.Min(0f, body.linearVelocity.y));
                }

                float panDir = ReadHorizontalInput(kb, pad);
                PanCamera(panDir, Time.deltaTime);
                return;
            }

            // Smoothly recenter camera after Flare ends
            if (Mathf.Abs(CurrentPanOffsetX) > 0.001f)
            {
                CurrentPanOffsetX = Mathf.MoveTowards(CurrentPanOffsetX, 0f, panSpeed * 2.5f * Time.deltaTime);
                ApplyCameraOffset();
            }

            HandleLadderInput(kb, pad, Time.deltaTime);
        }

        private void HandleFlareChanged(bool flared)
        {
            CacheReferences();
            if (flared)
            {
                if (lensController != null && lensController.IsSuppressedByShadowRealm)
                {
                    lantern?.ResetToDim();
                    return;
                }

                if (preventEmberConsumption && fuel != null)
                {
                    float targetFuel = cachedFuelBeforeFlare >= LanternFuel.ChargesPerEmber
                        ? cachedFuelBeforeFlare
                        : Mathf.Min(fuel.Capacity, fuel.CurrentFuel + LanternFuel.ChargesPerEmber);
                    if (fuel.CurrentFuel < targetFuel)
                    {
                        fuel.AddFuel(targetFuel - fuel.CurrentFuel);
                    }
                }

                EnterFlareFreeze();
            }
            else
            {
                ReleaseFlareFreeze();
            }
        }

        public void SetFlareScouting(bool active)
        {
            CacheReferences();
            if (active)
            {
                if (lensController != null && lensController.IsSuppressedByShadowRealm) return;
                if (lantern != null)
                {
                    lantern.SetFlared(true);
                }
                else
                {
                    EnterFlareFreeze();
                }
            }
            else
            {
                if (lantern != null)
                {
                    lantern.SetFlared(false);
                }
                else
                {
                    ReleaseFlareFreeze();
                }
            }
        }

        private void EnterFlareFreeze()
        {
            if (IsClimbingLadder) StopClimbing();
            IsCharacterFrozenByFlare = true;
            if (motor != null && motor.enabled)
            {
                motor.enabled = false;
                wasFlareFreezeApplied = true;
            }
            if (body != null)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }
        }

        private void ReleaseFlareFreeze()
        {
            IsCharacterFrozenByFlare = false;
            if (wasFlareFreezeApplied && motor != null)
            {
                motor.enabled = true;
                wasFlareFreezeApplied = false;
            }
        }

        public void PanCamera(float horizontalInput, float dt)
        {
            if (dt <= 0f) return;
            CurrentPanOffsetX = Mathf.Clamp(CurrentPanOffsetX + horizontalInput * panSpeed * dt, -maxPanOffset, maxPanOffset);
            ApplyCameraOffset();
        }

        private void ApplyCameraOffset()
        {
            if (followCamera == null) return;
            followCamera.Offset = new Vector3(baseCameraOffset.x + CurrentPanOffsetX, baseCameraOffset.y, baseCameraOffset.z);
        }

        private void HandleLadderInput(Keyboard kb, Gamepad pad, float dt)
        {
            float verticalInput = ReadVerticalInput(kb, pad);
            bool jumpPressed = (kb != null && kb.spaceKey.wasPressedThisFrame)
                               || (pad != null && pad.buttonSouth.wasPressedThisFrame);

            if (!IsClimbingLadder)
            {
                if (Mathf.Abs(verticalInput) > 0.1f)
                {
                    var ladder = FindOverlappingLadder();
                    if (ladder != null)
                    {
                        StartClimbing(ladder);
                    }
                }
                return;
            }

            if (jumpPressed)
            {
                StopClimbing();
                if (body != null)
                {
                    body.linearVelocity = new Vector2(body.linearVelocity.x, calibratedJumpSpeed * 0.85f);
                }
                return;
            }

            StepClimb(verticalInput, dt);
        }

        public ClimbableLadder2D FindOverlappingLadder()
        {
            var ladders = FindObjectsByType<ClimbableLadder2D>(FindObjectsSortMode.None);
            foreach (var ladder in ladders)
            {
                if (ladder != null && ladder.isActiveAndEnabled && ladder.ContainsPosition(transform.position))
                {
                    return ladder;
                }
            }
            return null;
        }

        public void StartClimbing(ClimbableLadder2D ladder)
        {
            if (ladder == null) return;
            CacheReferences();
            ActiveLadder = ladder;
            IsClimbingLadder = true;
            if (body != null)
            {
                baseGravityScale = body.gravityScale > 0.01f ? body.gravityScale : baseGravityScale;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
            }
            transform.position = new Vector3(ladder.CenterX, Mathf.Clamp(transform.position.y, ladder.BottomY, ladder.TopY), transform.position.z);
        }

        public void StepClimb(float verticalInput, float dt)
        {
            if (!IsClimbingLadder || ActiveLadder == null) return;

            float speed = ActiveLadder.ClimbSpeed;
            float nextY = transform.position.y + verticalInput * speed * dt;
            float clampedY = Mathf.Clamp(nextY, ActiveLadder.BottomY, ActiveLadder.TopY);
            var nextPos = new Vector3(ActiveLadder.CenterX, clampedY, transform.position.z);
            transform.position = nextPos;

            if (body != null)
            {
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
                body.position = nextPos;
            }

            if ((verticalInput > 0.1f && nextY >= ActiveLadder.TopY)
                || (verticalInput < -0.1f && nextY <= ActiveLadder.BottomY))
            {
                StopClimbing();
            }
        }

        public void StopClimbing()
        {
            IsClimbingLadder = false;
            ActiveLadder = null;
            if (body != null)
            {
                body.gravityScale = baseGravityScale;
            }
        }

        private static float ReadHorizontalInput(Keyboard kb, Gamepad pad)
        {
            float val = 0f;
            if (kb != null)
            {
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) val += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) val -= 1f;
            }
            if (Mathf.Abs(val) < 0.01f && pad != null)
            {
                val = pad.leftStick.x.ReadValue();
            }
            return Mathf.Clamp(val, -1f, 1f);
        }

        private static float ReadVerticalInput(Keyboard kb, Gamepad pad)
        {
            float val = 0f;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) val += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) val -= 1f;
            }
            if (Mathf.Abs(val) < 0.01f && pad != null)
            {
                val = pad.leftStick.y.ReadValue();
            }
            return Mathf.Clamp(val, -1f, 1f);
        }
    }
}
