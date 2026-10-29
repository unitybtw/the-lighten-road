using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerRespawnTests
{
    private GameObject player;
    private GameObject floor;
    private PlayerMotor2D motor;
    private LanternFuel fuel;
    private PlayerRespawnController respawnController;
    private DeathScreenOverlay overlay;
    private GameObject overlayGo;
    private bool hadDeathCount;
    private int deathCount;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        hadDeathCount = PlayerPrefs.HasKey("TheLightenRoad.TotalDeaths");
        deathCount = PlayerPrefs.GetInt("TheLightenRoad.TotalDeaths");
        floor = new GameObject("Test Floor");
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(30f, 1f);

#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        Assert.That(prefab, Is.Not.Null, "The tests must exercise the actual player prefab.");
        player = Object.Instantiate(prefab, new Vector3(0f, 1.5f, 0f), Quaternion.identity);
        motor = player.GetComponent<PlayerMotor2D>();
        fuel = player.GetComponent<LanternFuel>();

        respawnController = player.GetComponent<PlayerRespawnController>();
        if (respawnController == null)
            respawnController = player.AddComponent<PlayerRespawnController>();

        respawnController.SpawnPosition = new Vector3(0f, 1.5f, 0f);
        respawnController.AutoRespawnDelay = 1.0f;
        respawnController.VoidKillY = -3f;

        overlayGo = new GameObject("Test Death Overlay");
        overlay = overlayGo.AddComponent<DeathScreenOverlay>();
        overlay.SetTarget(respawnController);
#else
        Assert.Ignore("This integration suite runs in Editor Play Mode.");
        yield break;
#endif
        yield return new WaitForSeconds(0.4f);
        Assert.That(motor.IsGrounded, Is.True, "Player must settle on the floor.");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (player != null)
            Object.Destroy(player);
        if (floor != null)
            Object.Destroy(floor);
        if (overlayGo != null)
            Object.Destroy(overlayGo);
        yield return null;
        if (hadDeathCount) PlayerPrefs.SetInt("TheLightenRoad.TotalDeaths", deathCount);
        else PlayerPrefs.DeleteKey("TheLightenRoad.TotalDeaths");
    }

    [UnityTest]
    public IEnumerator DepletedFuelTriggersDarknessDeath()
    {
        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));

        respawnController.KillOnFuelDepleted = true;
        // Drain fuel to 0
        fuel.TrySpendFuel(fuel.CurrentFuel);
        Assert.That(fuel.IsEmpty, Is.True);

        // Exactly three seconds are allowed; death begins only after that.
        respawnController.TickSurvival(3f);
        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));
        respawnController.TickSurvival(.01f);
        yield return null;

        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Dying).Or.EqualTo(RespawnState.Dead));
        Assert.That(respawnController.LastDeathReason, Is.EqualTo(DeathReason.Darkness));
        Assert.That(motor.enabled, Is.False, "Player motor should be disabled upon death.");
        Assert.That(overlay.IsVisible, Is.True, "Death screen overlay should activate.");
    }

    [UnityTest]
    public IEnumerator FallingBelowVoidLimitTriggersVoidDeath()
    {
        player.transform.position = new Vector3(0f, -4f, 0f);
        yield return null;

        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Dying).Or.EqualTo(RespawnState.Dead));
        Assert.That(respawnController.LastDeathReason, Is.EqualTo(DeathReason.Void));
    }

    [UnityTest]
    public IEnumerator HazardTriggerKillsPlayerOnContact()
    {
        var hazardGo = new GameObject("Hazard Zone");
        hazardGo.transform.position = player.transform.position;
        var box = hazardGo.AddComponent<BoxCollider2D>();
        box.size = new Vector2(2f, 2f);
        box.isTrigger = true;
        hazardGo.AddComponent<HazardTrigger2D>();

        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Dying).Or.EqualTo(RespawnState.Dead));
        Assert.That(respawnController.LastDeathReason, Is.EqualTo(DeathReason.Hazard));

        Object.Destroy(hazardGo);
    }

    [UnityTest]
    public IEnumerator RespawnRestoresPlayerFuelPositionAndMotor()
    {
        // Kill player
        respawnController.Kill(DeathReason.Darkness);
        Assert.That(motor.enabled, Is.False);

        // Move player away
        player.transform.position = new Vector3(10f, 10f, 0f);

        // Request respawn
        respawnController.Respawn();

        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));
        Assert.That(motor.enabled, Is.True, "Player motor should be re-enabled on respawn.");
        Assert.That(player.transform.position.x, Is.EqualTo(respawnController.SpawnPosition.x).Within(0.1f));
        Assert.That(player.transform.position.y, Is.EqualTo(respawnController.SpawnPosition.y).Within(0.1f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(respawnController.RespawnFuelAmount).Within(0.1f));

        yield return null;
    }
    [UnityTest]
    public IEnumerator RecoveryCancelsDarknessAndChamberEntryRestoresThreeEmbers()
    {
        fuel.TrySpendFuel(fuel.CurrentFuel);
        respawnController.TickSurvival(2.9f);
        fuel.AddEmbers(1);
        Assert.That(respawnController.DarknessTimer, Is.Zero);
        respawnController.TickSurvival(1f);
        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));
        fuel.TrySpendFuel(fuel.CurrentFuel);
        respawnController.TickSurvival(2f);
        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));
        var chamberStart = new Vector3(4f, 1.5f, 0);
        respawnController.BeginChamber(chamberStart);
        Assert.That(fuel.TotalEmbers, Is.EqualTo(3));
        Assert.That(respawnController.DarknessTimer, Is.Zero);
        respawnController.Kill(DeathReason.Hazard);
        respawnController.Respawn();
        Assert.That(player.transform.position, Is.EqualTo(chamberStart));
        Assert.That(fuel.TotalEmbers, Is.EqualTo(3));
        yield return null;
    }

    [UnityTest] public IEnumerator RespawnRecoveryNotificationSeesAReadyPlayerAtTheCheckpoint()
    {
        RespawnState observed = RespawnState.Dead;bool motorReady = false, simulated = false;Vector2 position = default;
        respawnController.SpawnPosition = new Vector3(5, 2, 0);
        respawnController.OnPlayerRespawned += () => { observed=respawnController.State;motorReady=motor.enabled;simulated=motor.Body.simulated;position=motor.Body.position; };
        respawnController.Kill(DeathReason.Hazard);respawnController.Respawn();
        Assert.That(observed, Is.EqualTo(RespawnState.Alive));
        Assert.That(motorReady && simulated, Is.True);
        Assert.That(Vector2.Distance(position, respawnController.SpawnPosition), Is.LessThan(.001f));
        yield return null;
    }

    [UnityTest] public IEnumerator RespawnRecoveryListenerFailureCannotStrandThePlayerInRespawning()
    {
        respawnController.OnPlayerRespawned += () => throw new System.InvalidOperationException("Recovery listener fixture");
        respawnController.Kill(DeathReason.Hazard);
        Assert.Throws<System.InvalidOperationException>(() => respawnController.Respawn());
        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));
        Assert.That(motor.enabled && motor.Body.simulated, Is.True);
        yield return null;
    }

    [UnityTest] public IEnumerator RespawnRecoveryListenerCanReportANewHazardWithoutItBeingOverwritten()
    {
        respawnController.OnPlayerRespawned += () => respawnController.Kill(DeathReason.Hazard);
        respawnController.Kill(DeathReason.Darkness);respawnController.Respawn();
        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Dying));
        Assert.That(respawnController.LastDeathReason, Is.EqualTo(DeathReason.Hazard));
        Assert.That(motor.enabled || motor.Body.simulated, Is.False);
        yield return null;
    }

    [UnityTest] public IEnumerator RespawnRecoveryManualSkipWaitsUntilTheWorldIsUnpaused()
    {
        var previousKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        try
        {
            overlay.enabled=false;respawnController.AutoRespawnEnabled=false;respawnController.AllowManualRespawnInput=true;
            respawnController.Kill(DeathReason.Hazard);yield return new WaitForSeconds(.5f);
            Assert.That(respawnController.State, Is.EqualTo(RespawnState.Dead));
            Time.timeScale=0;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;yield return null;
            Assert.That(respawnController.State, Is.EqualTo(RespawnState.Dead), "Pause-menu submit must not also respawn the player behind it.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Time.timeScale=1;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;yield return null;
            Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));
        }
        finally
        {
            Time.timeScale=1;InputSystem.RemoveDevice(keyboard);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
        }
    }
}
