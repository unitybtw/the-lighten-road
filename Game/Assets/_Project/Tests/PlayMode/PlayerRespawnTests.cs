using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
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

    [UnitySetUp]
    public IEnumerator SetUp()
    {
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
    }

    [UnityTest]
    public IEnumerator DepletedFuelTriggersDarknessDeath()
    {
        Assert.That(respawnController.State, Is.EqualTo(RespawnState.Alive));

        // Drain fuel to 0
        fuel.TrySpendFuel(fuel.CurrentFuel);
        Assert.That(fuel.IsEmpty, Is.True);

        yield return null;

        Assert.That(respawnController.State, Is.AnyOf(RespawnState.Dying, RespawnState.Dead));
        Assert.That(respawnController.LastDeathReason, Is.EqualTo(DeathReason.Darkness));
        Assert.That(motor.enabled, Is.False, "Player motor should be disabled upon death.");
        Assert.That(overlay.IsVisible, Is.True, "Death screen overlay should activate.");
    }

    [UnityTest]
    public IEnumerator FallingBelowVoidLimitTriggersVoidDeath()
    {
        player.transform.position = new Vector3(0f, -4f, 0f);
        yield return null;

        Assert.That(respawnController.State, Is.AnyOf(RespawnState.Dying, RespawnState.Dead));
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

        Assert.That(respawnController.State, Is.AnyOf(RespawnState.Dying, RespawnState.Dead));
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
    }
}
