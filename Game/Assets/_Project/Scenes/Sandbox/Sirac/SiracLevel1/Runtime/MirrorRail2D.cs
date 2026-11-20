using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Ember-Vein-driven track that slides an attached LightMirror2D along RailStart..RailEnd.</summary>
    public sealed class MirrorRail2D : MonoBehaviour, ILensReactive
    {
        public AdvancedEmberVein RailVein;
        public LightMirror2D AttachedMirror;
        public Vector3 RailStart;
        public Vector3 RailEnd;
        [Min(0.1f)] public float SlideSpeed = 2.2f;

        private bool hasLensOverride;
        private LanternLensType lensOverride = LanternLensType.Normal;

        public int EffectiveState { get; private set; }

        private void Awake()
        {
            LanternLensController.RegisterReactive(this);
            if (RailStart == Vector3.zero && RailEnd == Vector3.zero)
            {
                RailStart = transform.position;
                RailEnd = transform.position + Vector3.right * 3f;
            }
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
            StepRail(Time.deltaTime);
        }

        public void ApplyLensField(LanternLensType lens, bool inRange)
        {
            hasLensOverride = inRange && lens == LanternLensType.Invert;
            lensOverride = inRange ? lens : LanternLensType.Normal;
        }

        public void StepRail(float dt)
        {
            int state = RailVein != null ? RailVein.VeinStateValue : 0;
            if (hasLensOverride && lensOverride == LanternLensType.Invert) state = -1;
            EffectiveState = state;

            if (AttachedMirror == null || dt <= 0f || state == 0 || state == 2) return;
            Vector3 target = state == 1 ? RailEnd : RailStart;
            AttachedMirror.transform.position = Vector3.MoveTowards(AttachedMirror.transform.position, target, SlideSpeed * dt);
        }
    }
}
