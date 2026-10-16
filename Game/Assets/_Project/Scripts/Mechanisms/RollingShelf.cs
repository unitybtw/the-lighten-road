using UnityEngine;

namespace TheLightenRoad.Mechanisms
{
    public class RollingShelf : MonoBehaviour
    {
        [Header("Shelf Settings")]
        public Vector3 localMovement = new Vector3(4f, 0f, 0f); // Varsayýlan olarak X ekseninde 4 birim kayar
        public float speed = 2.5f;

        private Vector3 startPosition;
        private Vector3 targetPosition;

        private void Start()
        {
            // Rafýn ilk durduðu yeri baþlangýç noktasý kabul ediyoruz
            startPosition = transform.position;
            targetPosition = startPosition;
        }

        private void Update()
        {
            // Rafý hedefine doðru pürüzsüz bir þekilde kaydýrýyoruz
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        }

        public void OpenShelf()
        {
            // Kor damarýna güç verildiðinde raf yana kayýp yolu açar
            targetPosition = startPosition + localMovement;
            Debug.Log("Shelf Opened: Path is clear.");
        }

        public void CloseShelf()
        {
            // Güç geri çekildiðinde raf eski yerine dönüp yolu kapatýr
            targetPosition = startPosition;
            Debug.Log("Shelf Closed: Path is blocked.");
        }
    }
}