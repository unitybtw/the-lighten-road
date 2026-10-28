using UnityEngine;
using TheLightenRoad.Player;
namespace TheLightenRoad.FirstLight
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FirstLightDoor : MonoBehaviour
    {
        public FirstLightTutorial Tutorial;
        private void OnTriggerEnter2D(Collider2D other)=>Try(other);
        private void OnTriggerStay2D(Collider2D other)=>Try(other);
        private void Try(Collider2D other)
        {if(Tutorial!=null)Tutorial.TryEnterDoor(other.GetComponentInParent<PlayerRespawnController>());}
    }
}
