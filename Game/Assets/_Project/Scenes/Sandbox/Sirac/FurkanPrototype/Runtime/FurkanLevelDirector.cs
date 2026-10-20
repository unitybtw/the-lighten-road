using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.FurkanPrototype
{
    public sealed class FurkanLevelDirector : MonoBehaviour
    {
        public PlayerRespawnController Player;
        public ArchiveCircuit Circuit;
        public ArchiveCircuitNode[] Nodes;
        public Transform PickupRoot;
        public GameObject PickupPrefab;
        public TMP_Text Prompt;
        public GameObject ReturnShutter;
        public ArchiveMemento Memento;
        public Vector3 FirstSpawn;
        public Vector3 SecondSpawn;
        public int Chamber {get;private set;} = 1;
        public bool IsComplete {get;private set;}
        public bool HasMemento;
        public ChamberCompletionSequence Completion;
        private Vector3[] pickupPositions;
        private float promptTime;
        private void Awake()
        {
            Time.timeScale = 1f;
        }
        private void Start()
        {
            Time.timeScale = 1f;
            pickupPositions=new Vector3[PickupRoot.childCount];
            for(int i=0;i<pickupPositions.Length;i++)pickupPositions[i]=PickupRoot.GetChild(i).position;
            Player.OnPlayerRespawned+=ResetAttempt;
            SpawnTutorialWhispers();
            Show("CHAMBER 01 / QUIET ARRIVAL\nA / D to walk. SPACE to jump. Follow the rising archive blocks.",9f);
        }

        private void SpawnTutorialWhispers()
        {
            DiegeticWhisper.Spawn(new Vector3(3.3f, 2.5f, 0f), "SPACE", "LEAP ACROSS THE ABYSS", WhisperAction.Jump, "furkan_jump", transform);
            DiegeticWhisper.Spawn(new Vector3(13.5f, 2.5f, 0f), "SHIFT", "SPRINT TO VAULT THE DESK", WhisperAction.Sprint, "furkan_sprint", transform);
            DiegeticWhisper.Spawn(new Vector3(18.2f, 2.8f, 0f), "F", "IGNITE FLARE TO PIERCE DARKNESS", WhisperAction.Flare, "furkan_flare", transform);
            DiegeticWhisper.Spawn(new Vector3(26.7f, 2.8f, 0f), "E", "TRANSFER EMBER TO POWER LIFT", WhisperAction.Interact, "furkan_circuit", transform);
        }
        private void OnDestroy(){if(Player!=null)Player.OnPlayerRespawned-=ResetAttempt;}
        private void Update()
        {
            if(Completion!=null && Completion.IsRunning)return;
            var k=Keyboard.current;
            if(k!=null && k.backspaceKey.wasPressedThisFrame)Restart();
            if(Player.State!=RespawnState.Alive)return;
            ArchiveCircuitNode closest=null;float distance=2.1f;
            foreach(var n in Nodes)
            {
                float d=Vector2.Distance(Player.transform.position,n.transform.position);
                if(d<distance){distance=d;closest=n;}
            }
            if(closest!=null)
            {
                if(k!=null && k.eKey.wasPressedThisFrame)
                {
                    bool changed=Circuit.TryTransfer(Player.GetComponent<LanternFuel>());
                    Show(changed?(Circuit.Powered?"CIRCUIT POWERED\nOne ember is stored in the circuit. The platform is moving.":"EMBER RECOVERED\nThe platform freezes exactly where it is."):(Circuit.Powered?"LANTERN FULL\nMake room before recovering the circuit's ember.":"NO EMBERS\nAn ember is needed to power this circuit."),5f);
                }
                if(promptTime<=0 && Prompt!=null)Prompt.text=Circuit.Powered?"E / RECOVER EMBER — FREEZE PLATFORM":"E / DEPOSIT ONE EMBER — POWER PLATFORM";
            }
            else if(promptTime<=0 && Prompt!=null)Prompt.text=IsComplete?"CHAMBERS COMPLETE / BACKSPACE TO RESTART":"F / SCOUT WITH ONE EMBER     BACKSPACE / RESTART";
            promptTime=Mathf.Max(0,promptTime-Time.unscaledDeltaTime);
        }
        public void Show(string text,float seconds){promptTime=seconds;if(Prompt!=null)Prompt.text=text;}
        public void EnterSecondChamber(Vector3 start)
        {
            if(Chamber==2)return;
            Chamber=2;SecondSpawn=start;Player.BeginChamber(start);
            if(ReturnShutter!=null)ReturnShutter.SetActive(true);
            Show("CHAMBER 02 / BEGINNINGS\nScout the compact crossing. F spends one ember to reveal the dark ahead.",9f);
        }
        public void Complete()
        {
            if(IsComplete)return;
            if(Completion!=null && !Completion.IsRunning && !Completion.TryBegin(Player))return;
            IsComplete=true;
            if(Completion!=null)
            {
                promptTime=0;
                if(Prompt!=null)Prompt.text="";
            }
            else Show("THE ARCHIVE OPENS\n"+(HasMemento?"Memory recovered. ":"")+"Both chambers complete.",15f);
        }
        public void ResetAttempt()
        {
            Circuit.ResetCircuit();HasMemento=false;IsComplete=false;
            if(Completion!=null)Completion.ResetCompletion();
            if(Memento!=null)Memento.gameObject.SetActive(true);
            if(pickupPositions==null)return;
            foreach(Transform item in PickupRoot){item.gameObject.SetActive(false);Destroy(item.gameObject);}
            foreach(var position in pickupPositions)Instantiate(PickupPrefab,position,Quaternion.identity,PickupRoot);
            Show("THREE EMBERS RESTORED\nChamber reset. Deposits and pickups return to their authored state.",5f);
        }
        public void Restart()
        {
            Time.timeScale = 1f;
            Chamber=1;if(ReturnShutter!=null)ReturnShutter.SetActive(false);
            foreach(var trigger in GetComponentsInChildren<ArchiveTutorialTrigger>(true))trigger.ResetTrigger();
            Player.BeginChamber(FirstSpawn);
            if(Player.State==RespawnState.Alive)Player.Kill(DeathReason.Hazard);
            Player.Respawn();
        }
    }
}
