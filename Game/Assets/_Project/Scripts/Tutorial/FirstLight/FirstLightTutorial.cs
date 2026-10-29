using System;
using TheLightenRoad.Frontend;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using UnityEngine;

namespace TheLightenRoad.FirstLight
{
    public enum FirstLightStage { Awakening, FindEmber, Ignition, Move, Jump, Gather, Scout, DrawingDoor, DoorReady, Departing, Complete }

    /// <summary>A scene-local prologue. Reuses the real motor, inventory, flare and respawn contracts.</summary>
    public sealed class FirstLightTutorial : MonoBehaviour
    {
        public const string ScenePath="Assets/_Project/Scenes/Sandbox/Sirac/FirstLightTutorial.unity";
        public const string FirstLevel="Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity";
        public PlayerRespawnController Player;
        public FirstLightPresentation Presentation;
        public EmberPickup FirstEmber, ReserveA, ReserveB;
        public LanternHUD Hud;
        public string DestinationScene=FirstLevel;
        public bool LoadSceneOnExit=true;
        public bool AutomaticTicks=true;
        public FirstLightStage Stage { get; private set; }
        public bool HasIgnited { get; private set; }
        public bool HasJumped { get; private set; }
        public bool CanEnterDoor=>Stage==FirstLightStage.DoorReady;
        public int CollectedReserves=>(collectedA?1:0)+(collectedB?1:0);
        public readonly float[] Marks=new float[3];
        public float StageTime { get; private set; }
        public event Action<FirstLightStage> StageChanged;
        private PlayerMotor2D motor;
        private LanternController lantern;
        private LanternFuel fuel;
        private bool initialized,collectedA,collectedB,previousSurvival;
        private Vector3 ignitionPosition;
        private bool gated;
        public LanternController Lantern=>lantern;

        private void Start()=>Initialize();
        private void OnEnable()
        {
            if(!initialized || Player==null)return;
            Subscribe();
            bool lit=HasIgnited && Stage>=FirstLightStage.Move;
            Player.KillOnFuelDepleted=lit;
            if(lantern!=null)lantern.enabled=lit;
            Gate(Stage==FirstLightStage.Awakening || Stage==FirstLightStage.Ignition ||
                 Stage==FirstLightStage.DrawingDoor || Stage==FirstLightStage.Departing);
        }
        private void Subscribe()
        {
            motor.OnJumped-=OnJump;motor.OnJumped+=OnJump;
            Player.OnPlayerRespawned-=OnRespawn;Player.OnPlayerRespawned+=OnRespawn;
        }
        public void Initialize()
        {
            if(initialized || Player==null || Presentation==null)return;
            initialized=true;motor=Player.GetComponent<PlayerMotor2D>();lantern=Player.GetComponent<LanternController>();fuel=lantern.Fuel;
            previousSurvival=Player.KillOnFuelDepleted;Player.KillOnFuelDepleted=false;
            fuel.Initialize(0,LanternFuel.MaximumFuel);lantern.ResetToDim();lantern.enabled=false;
            Subscribe();
            if(Hud!=null)Hud.gameObject.SetActive(false);
            FirstEmber.gameObject.SetActive(false);ReserveA.gameObject.SetActive(false);ReserveB.gameObject.SetActive(false);
            Stage=FirstLightStage.Awakening;StageTime=0;Gate(true);
            Presentation.Initialize(this);Presentation.Paint(Stage,0,0);
        }
        private void Update()
        {
            if(AutomaticTicks && Time.timeScale>0 && !PauseMenuController.IsPaused)Tick(Time.deltaTime);
        }
        public void Tick(float deltaTime)
        {
            if(!initialized || Stage==FirstLightStage.Departing || Stage==FirstLightStage.Complete)return;
            float dt=Mathf.Max(0,deltaTime);StageTime+=dt;
            switch(Stage)
            {
                case FirstLightStage.Awakening:
                    if(StageTime>=.85f && FirstEmber!=null)FirstEmber.gameObject.SetActive(true);
                    if(StageTime>=2.8f){Gate(false);Enter(FirstLightStage.FindEmber);}break;
                case FirstLightStage.FindEmber:
                    if(fuel.TotalEmbers>0)
                    {
                        HasIgnited=true;ignitionPosition=Player.transform.position;Gate(true);
                        Player.SpawnPosition=new Vector3(2.8f,.95f,0);Enter(FirstLightStage.Ignition);Presentation.Chime(196,.45f);
                    }break;
                case FirstLightStage.Ignition:
                    Presentation.SetIgnition(Mathf.Clamp01(StageTime/1.8f));
                    if(StageTime>=1.8f)
                    {
                        lantern.enabled=true;Player.KillOnFuelDepleted=true;
                        if(Hud!=null)Hud.gameObject.SetActive(true);
                        Gate(false);Enter(FirstLightStage.Move);
                    }break;
                case FirstLightStage.Move:
                    if(Player.transform.position.x>=4.6f && Player.transform.position.x-ignitionPosition.x>=1.3f)Enter(FirstLightStage.Jump);break;
                case FirstLightStage.Jump:
                    if(HasJumped && Player.transform.position.x>=11.9f)
                    {ReserveA.gameObject.SetActive(true);ReserveB.gameObject.SetActive(true);Enter(FirstLightStage.Gather);}break;
                case FirstLightStage.Gather:
                    collectedA|=ReserveA==null || ReserveA.IsCollected;
                    collectedB|=ReserveB==null || ReserveB.IsCollected;
                    if(collectedA && collectedB)
                    {
                        Player.SpawnPosition=new Vector3(16.4f,1.2f,0);
                        Enter(FirstLightStage.Scout);
                    }break;
                case FirstLightStage.Scout:
                    for(int i=0;i<Marks.Length;i++)
                    {
                        if(Marks[i]>=1 || !CanChargeMark(i))continue;
                        float before=Marks[i];Marks[i]=Mathf.Min(1,Marks[i]+dt/1.2f);
                        if(before<1 && Marks[i]>=1)Presentation.Chime(261.63f*Mathf.Pow(1.25f,i),.25f);
                    }
                    if(Marks[0]>=1 && Marks[1]>=1 && Marks[2]>=1)
                    {Gate(true);Enter(FirstLightStage.DrawingDoor);Presentation.Chime(523.25f,.65f);}break;
                case FirstLightStage.DrawingDoor:
                    if(StageTime>=2.8f){Gate(false);Enter(FirstLightStage.DoorReady);}break;
            }
            Presentation.Paint(Stage,StageTime,dt);
        }
        public bool CanChargeMark(int index)
        {
            if(index<0 || index>=Marks.Length || Stage!=FirstLightStage.Scout || !lantern.IsFlared || lantern.FlareBlend<.5f)return false;
            if(Player.transform.position.x<18.5f || Player.transform.position.x>27.5f)return false;
            return Vector2.Distance(lantern.LanternLight.transform.position,Presentation.MarkPositions[index])<=lantern.LanternLight.pointLightOuterRadius;
        }
        private void OnJump(){if(Stage>=FirstLightStage.Move && Stage<=FirstLightStage.Jump)HasJumped=true;}
        private void OnRespawn()
        {
            Gate(false);
            fuel.Initialize(Stage>=FirstLightStage.Scout?LanternFuel.ChamberFuel:(1+CollectedReserves)*LanternFuel.ChargesPerEmber,LanternFuel.MaximumFuel);
            lantern.enabled=HasIgnited;Player.KillOnFuelDepleted=HasIgnited;
            if(HasIgnited && Stage<FirstLightStage.Move)Enter(FirstLightStage.Move);
        }
        private void Gate(bool value)
        {
            gated=value;GameControls.BlockGameplay(this,value);
            if(value && motor!=null && motor.Body!=null)motor.Body.linearVelocity=Vector2.zero;
        }
        private void Enter(FirstLightStage next){Stage=next;StageTime=0;StageChanged?.Invoke(next);}
        public bool TryEnterDoor(PlayerRespawnController player)
        {
            if(!CanEnterDoor || player!=Player || player.State!=RespawnState.Alive)return false;
            if(LoadSceneOnExit && !UnityEngine.Application.CanStreamedLevelBeLoaded(DestinationScene))return false;
            Enter(FirstLightStage.Departing);Gate(true);
            FirstLightDeparture.Begin(DestinationScene,LoadSceneOnExit,()=>{if(this!=null){Gate(false);Enter(FirstLightStage.Complete);}});
            return true;
        }
        private void OnDisable()
        {
            if(!initialized)return;
            if(motor!=null)motor.OnJumped-=OnJump;
            if(Player!=null){Player.OnPlayerRespawned-=OnRespawn;Player.KillOnFuelDepleted=previousSurvival;}
            if(initialized && lantern!=null)lantern.enabled=true;
            if(gated)Gate(false);
        }
    }
}
