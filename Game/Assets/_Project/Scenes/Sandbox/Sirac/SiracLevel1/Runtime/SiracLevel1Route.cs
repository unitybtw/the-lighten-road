using TheLightenRoad.Frontend;
using TheLightenRoad.Interaction;
using TheLightenRoad.Lantern.Glyphs;
using TheLightenRoad.Lantern.Relays;
using TheLightenRoad.Mechanisms;
using TheLightenRoad.Player;
using TheLightenRoad.Sirac.OfficeAnnex;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Coordinates retry cleanup and pause-restart hooks for Sirac's Level 1 office route.</summary>
    public sealed class SiracLevel1Route : MonoBehaviour
    {
        public PlayerRespawnController Player;
        public AnnexPushBlock Crate;
        public PhotosensitiveRelay2D BridgeRelay;
        public EtherealPlatform2D BridgePlatform;
        public EmberVein WaterVein;
        public ElectrifiedWater WaterHazard;
        public PhotosensitiveRelay2D ExitRelay;
        public ArchiveGate2D ExitGate;
        public FlareGlyph SecretGlyph;
        public SecretPassage SecretCache;

        [Header("Core Traversal, Lenses & Colored Embers")]
        public Level1TraversalRig TraversalRig;
        public LanternLensController LensController;
        public EmberInventoryController EmberInventory;
        public ClimbableLadder2D MezzanineLadder;

        [Header("4-State Ember Veins & Mechanisms")]
        public AdvancedEmberVein MultiStateVein;
        public AdvancedEmberVein CorruptedVein;
        public VeinDrivenMechanism2D BlastDoorMech;
        public VeinDrivenMechanism2D PistonMech;
        public VeinDrivenMechanism2D ElevatorMech;
        public VeinDrivenMechanism2D MovingPlatformMech;
        public VeinDrivenMechanism2D MovingBlockMech;
        public VeinDrivenMechanism2D PusherMech;
        public VeinDrivenMechanism2D GhostDoorMech;
        public VeinDrivenMechanism2D GhostCrateMech;
        public VeinDrivenMechanism2D GhostElevatorMech;

        [Header("Conduit Puzzle")]
        public ConduitGridController ConduitPuzzle;

        [Header("Light Mirrors & Rails")]
        public ProjectorVein2D MainProjector;
        public LightMirror2D RotatableMirror;
        public LightMirror2D CorruptedMirror;
        public MirrorRail2D MirrorRail;
        public DarknessBarrier2D DarknessBarrier;

        [Header("Shadow Realm, Level Flow, Mementos & Finale")]
        public ShadowRealmZone2D ShadowRealm;
        public ScriptedFallTrigger2D ScriptedFall;
        public MementoCollectible2D[] Mementos;
        public CentralCoreFinale2D CentralCoreFinale;
        public MechanicsHUD HudOverlay;

        private void OnEnable()
        {
            if (Player != null)
            {
                Player.OnPlayerRespawned -= HandleRespawn;
                Player.OnPlayerRespawned += HandleRespawn;
            }
            PauseMenuController.OnRestartLevelRequested = RestartScene;
        }

        private void OnDisable()
        {
            if (Player != null)
            {
                Player.OnPlayerRespawned -= HandleRespawn;
            }
            if (PauseMenuController.OnRestartLevelRequested == RestartScene)
            {
                PauseMenuController.OnRestartLevelRequested = null;
            }
        }

        private void HandleRespawn()
        {
            if (Crate != null)
            {
                Crate.ResetForRetry();
            }
        }

        private void RestartScene()
        {
            var active = SceneManager.GetActiveScene();
            if (active.IsValid())
            {
                SceneManager.LoadScene(active.path);
            }
        }
    }
}
