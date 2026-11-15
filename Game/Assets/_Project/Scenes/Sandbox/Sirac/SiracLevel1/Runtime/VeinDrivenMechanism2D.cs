using System;
using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Universal Vein- and Lens-driven mechanism supporting all 9 requested mechanism types
    /// across the 4 Vein states (1 Normal, 0 Stop, -1 Invert, 2 Ethereal).
    /// </summary>
    public sealed class VeinDrivenMechanism2D : MonoBehaviour, ILensReactive
    {
        public VeinMechanismType MechanismType = VeinMechanismType.MovingPlatform;
        public AdvancedEmberVein SourceVein;
        public VeinDrivenMechanism2D LinkedTarget;

        [Header("Motion Endpoints")]
        public Vector3 PointA;
        public Vector3 PointB;
        [Min(0.1f)] public float MoveSpeed = 2.5f;
        [Min(0.1f)] public float ElevatorDwellTime = 3.0f;

        [Header("Components")]
        public Collider2D SolidCollider;
        public SpriteRenderer VisualRenderer;

        private bool hasLensOverride;
        private LanternLensType lensInRange = LanternLensType.Normal;
        private bool externalForceOpen;
        private int lastResolvedState;
        private int lastVisualTintState = int.MinValue;
        private bool lastVisualPhasedOut;

        public int EffectiveState { get; private set; }
        public bool IsPhasedOut { get; private set; }
        public bool IsOpen { get; private set; }
        public bool MovingToB { get; private set; } = true;
        public float ElevatorWaitTimer { get; private set; }

        private void Awake()
        {
            LanternLensController.RegisterReactive(this);
            if (SolidCollider == null) SolidCollider = GetComponent<Collider2D>();
            if (VisualRenderer == null) VisualRenderer = GetComponent<SpriteRenderer>();
            if (PointA == Vector3.zero && PointB == Vector3.zero)
            {
                PointA = transform.position;
                PointB = transform.position + Vector3.up * 2f;
            }
            ResolveEffectiveState();
        }

        private void OnEnable()
        {
            LanternLensController.RegisterReactive(this);
        }

        private void OnDestroy()
        {
            LanternLensController.UnregisterReactive(this);
        }

        private void Update()
        {
            StepMechanism(Time.deltaTime);
        }

        public void ApplyLensField(LanternLensType lens, bool inRange)
        {
            bool nextHasOverride = inRange && lens != LanternLensType.Normal;
            LanternLensType nextLens = inRange ? lens : LanternLensType.Normal;
            if (hasLensOverride == nextHasOverride && lensInRange == nextLens)
                return;

            hasLensOverride = nextHasOverride;
            lensInRange = nextLens;
            ResolveEffectiveState();
        }

        public void SetExternalForceOpen(bool open)
        {
            externalForceOpen = open;
        }

        private void ResolveEffectiveState()
        {
            int state = SourceVein != null ? SourceVein.VeinStateValue : 0;
            if (hasLensOverride)
            {
                if (lensInRange == LanternLensType.Ethereal) state = 2;
                else if (lensInRange == LanternLensType.Invert) state = -1;
            }

            if (state == -1 && lastResolvedState != -1 && (MechanismType == VeinMechanismType.Elevator || MechanismType == VeinMechanismType.Piston))
            {
                MovingToB = !MovingToB;
                ElevatorWaitTimer = 0f;
            }

            lastResolvedState = state;
            EffectiveState = state;
        }

        public void StepMechanism(float dt)
        {
            ResolveEffectiveState();

            bool isGhostType = MechanismType == VeinMechanismType.GhostDoor
                               || MechanismType == VeinMechanismType.GhostCrate
                               || MechanismType == VeinMechanismType.GhostElevator;

            // State 2 (Ethereal) or Cyan Lens removes collisions (ghost phase-out)
            IsPhasedOut = EffectiveState == 2;
            if (SolidCollider != null && SolidCollider.enabled == IsPhasedOut)
            {
                SolidCollider.enabled = !IsPhasedOut;
            }

            if (lastVisualTintState != EffectiveState || lastVisualPhasedOut != IsPhasedOut)
            {
                lastVisualTintState = EffectiveState;
                lastVisualPhasedOut = IsPhasedOut;
                UpdateVisualTint(isGhostType);
            }

            if (dt <= 0f) return;

            switch (MechanismType)
            {
                case VeinMechanismType.BlastDoor:
                case VeinMechanismType.GhostDoor:
                {
                    if (externalForceOpen || EffectiveState == 1)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, PointB, MoveSpeed * dt);
                    }
                    else if (EffectiveState == -1)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, PointA, MoveSpeed * dt);
                    }
                    IsOpen = IsPhasedOut || Vector3.Distance(transform.position, PointB) <= 0.08f;
                    break;
                }

                case VeinMechanismType.Piston:
                {
                    // State 0 halts Piston solid in-place so the player can stand on it as a platform
                    if (EffectiveState == 0 || EffectiveState == 2) break;
                    Vector3 target = MovingToB ? PointB : PointA;
                    transform.position = Vector3.MoveTowards(transform.position, target, MoveSpeed * dt);
                    if (Vector3.Distance(transform.position, target) <= 0.01f)
                    {
                        MovingToB = !MovingToB;
                    }
                    break;
                }

                case VeinMechanismType.Elevator:
                case VeinMechanismType.GhostElevator:
                {
                    if (EffectiveState == 0 || EffectiveState == 2) break;
                    if (ElevatorWaitTimer > 0f)
                    {
                        ElevatorWaitTimer = Mathf.Max(0f, ElevatorWaitTimer - dt);
                        break;
                    }
                    Vector3 target = MovingToB ? PointB : PointA;
                    transform.position = Vector3.MoveTowards(transform.position, target, MoveSpeed * dt);
                    if (Vector3.Distance(transform.position, target) <= 0.01f)
                    {
                        transform.position = target;
                        MovingToB = !MovingToB;
                        ElevatorWaitTimer = ElevatorDwellTime;
                    }
                    break;
                }

                case VeinMechanismType.MovingPlatform:
                case VeinMechanismType.MovingBlock1x1:
                {
                    if (EffectiveState == 1)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, PointB, MoveSpeed * dt);
                    }
                    else if (EffectiveState == -1)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, PointA, MoveSpeed * dt);
                    }
                    break;
                }

                case VeinMechanismType.PusherMechanism:
                {
                    if (EffectiveState == 1)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, PointB, MoveSpeed * dt);
                        bool pressed = Vector3.Distance(transform.position, PointB) <= 0.1f;
                        IsOpen = pressed;
                        if (LinkedTarget != null) LinkedTarget.SetExternalForceOpen(pressed);
                    }
                    else if (EffectiveState == -1)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, PointA, MoveSpeed * dt);
                        IsOpen = false;
                        if (LinkedTarget != null) LinkedTarget.SetExternalForceOpen(false);
                    }
                    break;
                }

                case VeinMechanismType.GhostCrate:
                    break;
            }
        }

        private void UpdateVisualTint(bool isGhostType)
        {
            if (VisualRenderer == null) return;

            Color c = VisualRenderer.color;
            if (IsPhasedOut)
            {
                VisualRenderer.color = new Color(0.25f, 0.90f, 1f, 0.28f);
            }
            else if (EffectiveState == -1)
            {
                VisualRenderer.color = new Color(0.95f, 0.35f, 0.42f, 1f);
            }
            else if (EffectiveState == 1)
            {
                VisualRenderer.color = isGhostType ? new Color(0.55f, 0.85f, 0.95f, 0.92f) : new Color(0.95f, 0.72f, 0.36f, 1f);
            }
            else
            {
                VisualRenderer.color = isGhostType ? new Color(0.62f, 0.76f, 0.88f, 0.92f) : new Color(c.r, c.g, c.b, 1f);
            }
        }
    }
}
