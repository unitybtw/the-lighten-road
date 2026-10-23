using UnityEngine;

namespace TheLightenRoad.Mechanisms
{
    public class RollingShelf : MonoBehaviour
    {
        [Header("Shelf Settings")]
        public Vector3 localMovement = new Vector3(4f, 0f, 0f); // Varsayılan olarak X ekseninde 4 birim kayar
        public float speed = 2.5f;

        private Vector3 startPosition;
        private Vector3 targetPosition;

        private void Start()
        {
            // Rafın ilk durduğu yeri başlangıç noktası kabul ediyoruz
            startPosition = transform.position;
            targetPosition = startPosition;
        }

        private void Update()
        {
            // Rafı hedefine doğru pürüzsüz bir şekilde kaydırıyoruz
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        }

        public void OpenShelf()
        {
            // Kor damarına güç verildiğinde raf yana kayıp yolu açar
            targetPosition = startPosition + localMovement;
            Debug.Log("Shelf Opened: Path is clear.");
        }

        public void CloseShelf()
        {
            // Güç geri çekildiğinde raf eski yerine dönüp yolu kapatır
            targetPosition = startPosition;
            Debug.Log("Shelf Closed: Path is blocked.");
        }
    }
}