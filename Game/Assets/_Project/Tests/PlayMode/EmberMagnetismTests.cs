using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EmberMagnetismTests
{
    private GameObject player;
    private GameObject pickup;

    [Test]
    public void MagnetismStopsIfTheLanternFillsDuringFlight()
    {
        player = new GameObject("Lantern");
        var fuel = player.AddComponent<LanternFuel>();
        var lantern = player.AddComponent<LanternController>();
        fuel.Initialize(4f, 8f);
        pickup = new GameObject("Ember");
        pickup.AddComponent<CircleCollider2D>();
        var ember = pickup.AddComponent<EmberPickup>();
        ember.SetTargetLantern(lantern);
        ember.StartMagnetism(fuel);
        Assert.That(ember.SparkTrail.emitting, Is.True);
        fuel.Initialize(8f, 8f);
        ember.Tick(.02f);
        Assert.That(ember.IsMagnetizing, Is.False);
        Assert.That(ember.SparkTrail.emitting, Is.False);
        Assert.That(ember.IsCollected, Is.False);
        fuel.Initialize(4f, 8f);
        Assert.That(ember.TryCollect(fuel), Is.True);
    }

    [Test]
    public void DisabledLanternLightDoesNotIlluminateAnEmber()
    {
        player = new GameObject("Lantern");
        player.AddComponent<LanternFuel>();
        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(player.transform);
        var light = lightObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        var lantern = player.AddComponent<LanternController>();
        light.enabled = false;
        pickup = new GameObject("Ember");
        pickup.AddComponent<CircleCollider2D>();
        var ember = pickup.AddComponent<EmberPickup>();
        ember.EnableMagnetism = false;
        ember.SetTargetLantern(lantern);
        ember.Tick(.5f);
        Assert.That(ember.IsIlluminated, Is.False);
        Assert.That(ember.ReactionProgress, Is.Zero);
        light.enabled = true;
        ember.Tick(.5f);
        Assert.That(ember.IsIlluminated, Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (player != null) Object.Destroy(player);
        if (pickup != null) Object.Destroy(pickup);
        yield return null;
    }

    [UnityTest]
    public IEnumerator EmberHasSparkTrailAndConfiguredProperly()
    {
#if UNITY_EDITOR
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");
        Assert.That(pickupPrefab, Is.Not.Null);

        pickup = Object.Instantiate(pickupPrefab, Vector3.zero, Quaternion.identity);
        var ember = pickup.GetComponent<EmberPickup>();
        Assert.That(ember, Is.Not.Null);

        yield return null;

        var trail = ember.SparkTrail;
        Assert.That(trail, Is.Not.Null, "Ember must have a SparkTrail TrailRenderer attached.");
        Assert.That(trail.startWidth, Is.GreaterThan(trail.endWidth), "Trail should taper off from start to end.");
        Assert.That(trail.time, Is.GreaterThan(0.1f), "Trail duration must be positive.");
#else
        Assert.Ignore("Prefab integration test in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator MagnetizesAndCollectsWhenPlayerInRangeWithFuelDeficit()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        var motor = player.GetComponent<PlayerMotor2D>();
        if (motor != null) motor.enabled = false;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }

        var fuel = player.GetComponent<LanternFuel>();
        fuel.Initialize(4f, 6f); // 2 units room

        // Place pickup within attraction radius (0.65m in Dim mode)
        pickup = Object.Instantiate(pickupPrefab, new Vector3(0.5f, 0f, 0f), Quaternion.identity);
        var ember = pickup.GetComponent<EmberPickup>();
        Assert.That(ember, Is.Not.Null);

        float elapsed = 0f;
        while (elapsed < 1.0f && pickup != null)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.That(pickup == null, Is.True, "Ember must magnetize and be collected within 1 second.");
        Assert.That(fuel.CurrentFuel, Is.EqualTo(6f), "Player fuel should be fully restored by the collected ember.");
#else
        Assert.Ignore("Prefab integration test in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator FlaredLanternExpandsMagnetismRadius()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        var motor = player.GetComponent<PlayerMotor2D>();
        if (motor != null) motor.enabled = false;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }

        var fuel = player.GetComponent<LanternFuel>();
        fuel.Initialize(4f, 6f);

        // Place pickup at 1.1m (outside 0.65m Dim radius, but inside 0.65 * 2.2 = 1.43m Flared radius)
        pickup = Object.Instantiate(pickupPrefab, new Vector3(1.1f, 0f, 0f), Quaternion.identity);
        var ember = pickup.GetComponent<EmberPickup>();
        var lantern = player.GetComponent<LanternController>();

        // In Dim mode, wait a few frames and verify it does NOT magnetize
        yield return null;
        yield return null;
        Assert.That(ember.IsMagnetizing, Is.False, "Ember should not magnetize when outside Dim attraction radius.");

        // Activate flare scout mode (spends 1 ember = 2 fuel to activate)
        lantern.SetFlared(true);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(2f), "Activating Flare should consume 1 ember (2 charges).");

        float elapsed = 0f;
        while (elapsed < 1.2f && pickup != null)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.That(pickup == null, Is.True, "Flared lantern expanded radius must pull in and collect the ember.");
        Assert.That(fuel.CurrentFuel, Is.EqualTo(4f), "Collected ember must restore the spent ember charge.");
#else
        Assert.Ignore("Prefab integration test in Editor Play Mode.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator DoesNotMagnetizeWhenLanternFuelIsFull()
    {
#if UNITY_EDITOR
        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/EmberPickup.prefab");

        player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        var motor = player.GetComponent<PlayerMotor2D>();
        if (motor != null) motor.enabled = false;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }

        var fuel = player.GetComponent<LanternFuel>();
        fuel.Initialize(8f, 8f); // Maximum 4-ember capacity (8 charges)
        Assert.That(fuel.IsFull, Is.True);

        pickup = Object.Instantiate(pickupPrefab, new Vector3(0.5f, 0f, 0f), Quaternion.identity);
        var ember = pickup.GetComponent<EmberPickup>();

        yield return null;
        yield return null;

        Assert.That(ember.IsMagnetizing, Is.False, "Full fuel must not trigger ember magnetism.");
        Assert.That(pickup != null, Is.True, "Pickup remains uncollected in the world when full.");
#else
        Assert.Ignore("Prefab integration test in Editor Play Mode.");
        yield break;
#endif
    }
}
