using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EmberPickupTests
{
    private GameObject player;
    private GameObject pickup;
    private GameObject obstacle;

    [UnityTest]
    public IEnumerator RestoresChargesAndDestroysPickupWhenCollected()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");
        Assert.That(playerPrefab, Is.Not.Null, "PlayerPrototype prefab must exist.");
        Assert.That(pickupPrefab, Is.Not.Null, "EmberPickup prefab must exist.");

        pickup = Object.Instantiate(pickupPrefab, Vector3.zero, Quaternion.identity);
        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

        var fuel = player.GetComponent<LanternFuel>();
        Assert.That(fuel, Is.Not.Null);
        // PlayerPrototype starts with 6 charges (3 full embers).
        Assert.That(fuel.CurrentFuel, Is.EqualTo(6f));

        // Allow 2D physics trigger to register
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(fuel.CurrentFuel, Is.EqualTo(8f), "Charges must be incremented by 2 (1 ember).");
        Assert.That(fuel.TotalEmbers, Is.EqualTo(4));
        Assert.That(pickup == null, Is.True, "Ember pickup must be destroyed after collection.");
#else
        Assert.Ignore("This prefab integration test runs in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator AlwaysCollectsPickupAndAddsToInventoryEvenWhenFull()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        pickup = Object.Instantiate(pickupPrefab, Vector3.zero, Quaternion.identity);
        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

        var fuel = player.GetComponent<LanternFuel>();
        fuel.Initialize(10f, 10f); // 5 Embers (fills the 5-slot HUD)
        Assert.That(fuel.IsFull, Is.True);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        // Furkan's rule: pickups are ALWAYS collected and added to inventory (overflow expands to +1)
        Assert.That(fuel.CurrentFuel, Is.EqualTo(12f));
        Assert.That(fuel.TotalEmbers, Is.EqualTo(6));
        Assert.That(fuel.OverflowEmbers, Is.EqualTo(1));
        Assert.That(pickup == null, Is.True, "Pickup must be collected into inventory even when slots are full.");
#else
        Assert.Ignore("This prefab integration test runs in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator IgnoresCollidersWithoutLanternFuel()
    {
#if UNITY_EDITOR
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        pickup = Object.Instantiate(pickupPrefab, Vector3.zero, Quaternion.identity);

        obstacle = new GameObject("Non-Player Obstacle");
        obstacle.transform.position = Vector3.zero;
        var body = obstacle.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        var collider = obstacle.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(pickup != null, Is.True, "Pickup must not be collected by objects without LanternFuel.");
        var ember = pickup.GetComponent<EmberPickup>();
        Assert.That(ember.IsCollected, Is.False);
#else
        Assert.Ignore("This prefab integration test runs in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (player != null) Object.Destroy(player);
        if (pickup != null) Object.Destroy(pickup);
        if (obstacle != null) Object.Destroy(obstacle);
        yield return null;
    }
}
