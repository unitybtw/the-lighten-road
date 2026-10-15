using UnityEngine;

namespace TheLightenRoad.Player
{
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;
        private Vector3 velocity;

        public void SetTarget(Transform newTarget, bool snap = false)
        {
            target = newTarget;
            velocity = Vector3.zero;
            if (snap && target != null)
                transform.position = target.position + offset;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            transform.position = Vector3.SmoothDamp(
                transform.position, target.position + offset, ref velocity, smoothTime);
        }
    }
}
