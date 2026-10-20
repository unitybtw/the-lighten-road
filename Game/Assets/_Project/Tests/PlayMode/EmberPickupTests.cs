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
        // Make room for one whole ember before the overlap is processed.
        fuel.Initialize(4f, 6f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(4f));

        // Allow 2D physics trigger to register
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(fuel.CurrentFuel, Is.EqualTo(6f), "Charges must be incremented by 2 (1 ember).");
        Assert.That(fuel.TotalEmbers, Is.EqualTo(3));
        Assert.That(pickup == null, Is.True, "Ember pickup must be destroyed after collection.");
#else
        Assert.Ignore("This prefab integration test runs in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator FullLanternLeavesPickupInWorldUntilThereIsRoom()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        pickup = Object.Instantiate(pickupPrefab, Vector3.zero, Quaternion.identity);
        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

        var fuel = player.GetComponent<LanternFuel>();
        fuel.Initialize(8f,8f); // Fill the four-ember carrying capacity.
        Assert.That(fuel.IsFull, Is.True);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(fuel.CurrentFuel, Is.EqualTo(8f));
        Assert.That(fuel.TotalEmbers, Is.EqualTo(4));
        Assert.That(pickup != null, Is.True, "Full inventory must leave the pickup in the world.");
        var ember = pickup.GetComponent<EmberPickup>();
        Assert.That(ember.IsCollected, Is.False);
        fuel.TrySpendEmber();
        Assert.That(ember.TryCollect(fuel), Is.True);
        Assert.That(fuel.TotalEmbers, Is.EqualTo(4));
        yield return null;
        Assert.That(pickup == null, Is.True);
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

    [UnityTest]
    public IEnumerator DormantAndInvisibleInPureDarknessWhenOutOfLight()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        player = Object.Instantiate(playerPrefab, new Vector3(-20f, 0f, 0f), Quaternion.identity);
        pickup = Object.Instantiate(pickupPrefab, Vector3.zero, Quaternion.identity);

        var ember = pickup.GetComponent<EmberPickup>();
        Assert.That(ember, Is.Not.Null);

        yield return null;
        yield return null;

        Assert.That(ember.IsIlluminated, Is.False, "Ember must not be illuminated in darkness.");
        Assert.That(ember.ReactionProgress, Is.EqualTo(0f));
        Assert.That(ember.IsFlaredUp, Is.False);
        Assert.That(ember.VisualRenderer.color.a, Is.EqualTo(0f), "Ember must be completely invisible in pure darkness.");
        Assert.That(ember.ReactiveLight.intensity, Is.EqualTo(0f), "Reactive light must be 0 in pure darkness.");
#else
        Assert.Ignore("This prefab integration test runs in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator FlaresUpWhenScoutLightVisionExpandsOverIt()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        var motor = player.GetComponent<TheLightenRoad.Player.PlayerMotor2D>();
        if (motor != null) motor.enabled = false;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
        pickup = Object.Instantiate(pickupPrefab, new Vector3(6f, 0f, 0f), Quaternion.identity);

        var ember = pickup.GetComponent<EmberPickup>();
        var lantern = player.GetComponent<LanternController>();
        Assert.That(ember, Is.Not.Null);
        Assert.That(lantern, Is.Not.Null);

        yield return null;
        yield return null;

        Assert.That(ember.IsIlluminated, Is.False, "In Dim mode, 6m distance is outside light radius.");
        Assert.That(ember.ReactionProgress, Is.EqualTo(0f));
        Assert.That(ember.VisualRenderer.color.a, Is.EqualTo(0f));

        lantern.SetFlared(true);
        float elapsed = 0f;
        while (elapsed < 0.6f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.That(ember.IsIlluminated, Is.True, "Scouting light reached the ember.");
        Assert.That(ember.ReactionProgress, Is.GreaterThan(0.7f), "Ember must chemically flare up.");
        Assert.That(ember.IsFlaredUp, Is.True);
        Assert.That(ember.VisualRenderer.color.a, Is.GreaterThan(0.7f), "Ember must become visible.");
        Assert.That(ember.ReactiveLight.intensity, Is.GreaterThan(0.4f), "Reactive light must shine.");
#else
        Assert.Ignore("This prefab integration test runs in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator LightsUpWhenPlayerInDimStateComesClose()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        var motor = player.GetComponent<TheLightenRoad.Player.PlayerMotor2D>();
        if (motor != null) motor.enabled = false;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
        pickup = Object.Instantiate(pickupPrefab, new Vector3(1.5f, 0f, 0f), Quaternion.identity);

        var ember = pickup.GetComponent<EmberPickup>();
        Assert.That(ember, Is.Not.Null);

        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.That(ember.IsIlluminated, Is.True, "Player's small light radius must illuminate nearby ember.");
        Assert.That(ember.IsFlaredUp, Is.True);
        Assert.That(ember.VisualRenderer.color.a, Is.GreaterThan(0.7f));
        Assert.That(ember.ReactiveLight.intensity, Is.GreaterThan(0.4f));
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
