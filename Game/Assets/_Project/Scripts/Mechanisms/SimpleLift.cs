using UnityEngine;

namespace TheLightenRoad.Mechanisms
{
    public class SimpleLift : MonoBehaviour
    {
        [Header("Lift Settings")]
        public Vector3 localMovement = new Vector3(0, 5, 0); // Asansör nereye gidecek?
        public float speed = 3f;

        private Vector3 startPosition;
        private Vector3 targetPosition;

        private void Start()
        {
            startPosition = transform.position;
            targetPosition = startPosition;
        }

        private void Update()
        {
            // Asansör hedef pozisyona doğru hareket eder
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        }

        // EmberVein OnPowered Event'ine bağlanacak
        public void ActivateLift()
        {
            targetPosition = startPosition + localMovement;
            Debug.Log("Lift Activated: Moving up.");
        }

        // EmberVein OnDrained Event'ine bağlanacak
        public void DeactivateLift()
        {
            targetPosition = startPosition;
            Debug.Log("Lift Deactivated: Moving down.");
        }
    }
}