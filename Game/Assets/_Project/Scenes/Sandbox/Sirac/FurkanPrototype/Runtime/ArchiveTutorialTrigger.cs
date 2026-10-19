using TheLightenRoad.Player;
using UnityEngine;
namespace TheLightenRoad.Sirac.FurkanPrototype
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ArchiveTutorialTrigger : MonoBehaviour
    {
        public FurkanLevelDirector Director;
        public bool BeginsChamber;
        public bool IsExit;
        public Vector3 Spawn;
        [TextArea] public string Message;
        private bool entered;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var player=other.GetComponentInParent<PlayerRespawnController>();
            if(entered || Director==null || player!=Director.Player)return;
            entered=true;
            if(BeginsChamber)Director.EnterSecondChamber(Spawn);
            if(IsExit)Director.Complete();
            else Director.Show(Message,8f);
        }
        public void ResetTrigger() => entered=false;
    }
}
