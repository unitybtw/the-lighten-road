using TheLightenRoad.Frontend;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.UI;
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
            PauseMenuController.OnRestartLevelRequested = Restart;
            SpawnTutorialWhispers();
        }

        private void SpawnTutorialWhispers()
        {
            DiegeticWhisper.Spawn(new Vector3(1f, 2.1f, 0f), "A / D", "WALK", WhisperAction.Move, "furkan_move", transform).TriggerRadius = 2.2f;
            DiegeticWhisper.Spawn(new Vector3(3.3f, 2.5f, 0f), "SPACE", "JUMP", WhisperAction.Jump, "furkan_jump", transform).TriggerRadius = 2.5f;
            DiegeticWhisper.Spawn(new Vector3(18.2f, 2.8f, 0f), "F", "SCOUT THE DARK AHEAD", WhisperAction.Flare, "furkan_flare", transform).TriggerRadius = 3f;
        }
        private void OnDestroy()
        {
            if (PauseMenuController.OnRestartLevelRequested == Restart) PauseMenuController.OnRestartLevelRequested = null;
            if(Player!=null)Player.OnPlayerRespawned-=ResetAttempt;
        }
        private void Update()
        {
            if(Completion!=null && Completion.IsRunning)return;
            if(!GameControls.GameplayAllowed)return;
            var k=Keyboard.current;
            if(k!=null && k.backspaceKey.wasPressedThisFrame){Restart();return;}
            if(Player.State!=RespawnState.Alive){if(Prompt!=null)Prompt.text="";return;}
            promptTime=Mathf.Max(0,promptTime-Time.deltaTime);
            ArchiveCircuitNode closest=null;float distance=2.1f;
            foreach(var n in Nodes)
            {
                float d=Vector2.Distance(Player.transform.position,n.transform.position);
                if(d<distance){distance=d;closest=n;}
            }
            if(closest!=null)
            {
                var pad=GameControls.ActivePad;
                bool interactPressed = (k!=null && k.eKey.wasPressedThisFrame) || (pad!=null && pad.buttonNorth.wasPressedThisFrame);
                if(interactPressed)
                {
                    bool changed=Circuit.TryTransfer(Player.GetComponent<LanternFuel>());
                    Show(changed?(Circuit.Powered?"EMBER DEPOSITED / SHELF MOVING":"EMBER RECOVERED / SHELF STOPPED"):(Circuit.Powered?"LANTERN FULL / MAKE ROOM TO RECOVER":"AN EMBER IS NEEDED TO POWER THE SHELF"),3f);
                }
                string interactKey = ControllerGlyphService.GetInteractPrompt();
                if(promptTime<=0 && Prompt!=null)Prompt.text=Circuit.Powered?$"{interactKey} / RECOVER EMBER · STOP SHELF":$"{interactKey} / DEPOSIT EMBER · MOVE SHELF";
            }
            else if(promptTime<=0 && Prompt!=null)Prompt.text="";
        }
        public void Show(string text,float seconds){promptTime=seconds;if(Prompt!=null)Prompt.text=text;}
        public void EnterSecondChamber(Vector3 start)
        {
            if(Chamber==2)return;
            Chamber=2;SecondSpawn=start;Player.BeginChamber(start);
            if(ReturnShutter!=null)ReturnShutter.SetActive(true);
            Show("THREE EMBERS RESTORED",3f);
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
            Show("THREE EMBERS RESTORED",3f);
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
