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
    public IEnumerator RestoresFuelAndDestroysPickupWhenLanternNeedsFuel()
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
        // PlayerPrototype starts with 80 fuel and 100 capacity.
        Assert.That(fuel.CurrentFuel, Is.EqualTo(80f));

        // Allow 2D physics trigger to register
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(fuel.CurrentFuel, Is.EqualTo(100f), "Fuel must be restored to 100.");
        Assert.That(pickup == null, Is.True, "Ember pickup must be destroyed after collection.");
#else
        Assert.Ignore("This prefab integration test runs in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator PreservesPickupWhenLanternIsFull()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        pickup = Object.Instantiate(pickupPrefab, Vector3.zero, Quaternion.identity);
        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

        var fuel = player.GetComponent<LanternFuel>();
        fuel.Initialize(100f, 100f);
        Assert.That(fuel.IsFull, Is.True);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(fuel.CurrentFuel, Is.EqualTo(100f));
        Assert.That(pickup != null, Is.True, "Pickup must not be destroyed when lantern is already full.");
        var ember = pickup.GetComponent<EmberPickup>();
        Assert.That(ember.IsCollected, Is.False);
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
