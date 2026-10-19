using TheLightenRoad.Player;
using UnityEngine;
namespace TheLightenRoad.Sirac.FurkanPrototype
{
    public sealed class ArchiveMemento : MonoBehaviour
    {
        public FurkanLevelDirector Director;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var p=other.GetComponentInParent<PlayerRespawnController>();
            if(Director==null || p!=Director.Player)return;
            Director.HasMemento=true;
            Director.Show("ARCHIVE FRAGMENT FOUND\nThe upper detour holds a memory of the world before.",8f);
            gameObject.SetActive(false);
        }
    }
}
