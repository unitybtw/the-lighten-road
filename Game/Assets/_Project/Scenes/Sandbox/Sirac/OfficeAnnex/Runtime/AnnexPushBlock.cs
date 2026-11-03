using TheLightenRoad.Input;
using TheLightenRoad.Player;
using UnityEngine;

namespace TheLightenRoad.Sirac.OfficeAnnex
{
    /// <summary>Local physical crate. Only grounded side contact with deliberate input requests Push.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class AnnexPushBlock : MonoBehaviour
    {
        public float MaximumSpeed = 1.2f;
        private Rigidbody2D body;
        private PlayerMotor2D actor;
        private PlayerSpriteAnimator pose;
        private Vector2 initialPosition;
        private float lastContact;
        public bool IsBeingPushed => pose != null;
        public float DistanceMoved => body != null ? Mathf.Abs(body.position.x - initialPosition.x) : 0f;

        private void Awake() { body = GetComponent<Rigidbody2D>(); initialPosition = body.position; }
        private void FixedUpdate()
        {
            var velocity = body.linearVelocity;
            velocity.x = Mathf.Clamp(velocity.x, -MaximumSpeed, MaximumSpeed);
            body.linearVelocity = velocity;
            if (actor != null && (!actor.enabled || !actor.IsGrounded || actor.TraversalLocked ||
                !GameControls.GameplayAllowed || Mathf.Abs(actor.HorizontalInput) < .05f || Time.time - lastContact > .08f)) Release();
        }
        private void OnCollisionEnter2D(Collision2D collision) => Contact(collision);
        private void OnCollisionStay2D(Collision2D collision) => Contact(collision);
        private void Contact(Collision2D collision)
        {
            var candidate = collision.collider.GetComponentInParent<PlayerMotor2D>();
            if (candidate == null) return;
            bool side = false;
            for (int i = 0; i < collision.contactCount; i++) side |= Mathf.Abs(collision.GetContact(i).normal.x) > .7f;
            float toward = Mathf.Sign(body.position.x - candidate.Body.position.x);
            if (!side || !candidate.enabled || !candidate.IsGrounded || candidate.TraversalLocked ||
                !GameControls.GameplayAllowed || candidate.HorizontalInput * toward <= .05f)
            { if (actor == candidate) Release(); return; }
            if (actor != candidate) Release();
            actor = candidate; pose = actor.GetComponent<PlayerSpriteAnimator>();
            lastContact = Time.time;
            if (pose != null) pose.SetPushing(this, true);
            var velocity = body.linearVelocity;
            velocity.x = Mathf.Clamp(velocity.x, -MaximumSpeed, MaximumSpeed);
            body.linearVelocity = velocity;
        }
        private void OnCollisionExit2D(Collision2D collision)
        { if (collision.collider.GetComponentInParent<PlayerMotor2D>() == actor) Release(); }
        private void Release()
        { if (pose != null) pose.SetPushing(this, false); pose = null; actor = null; }
        private void OnDisable() => Release();
        public void ResetForRetry()
        {
            Release(); body.position = initialPosition; body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0; body.rotation = 0;
            Physics2D.SyncTransforms();
        }
    }
}
