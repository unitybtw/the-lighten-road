using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Effects;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SwayingPropTests
{
    private GameObject propGo;
    private GameObject playerGo;
    private GameObject lanternGo;

    [TearDown]
    public void TearDown()
    {
        if (propGo != null) Object.DestroyImmediate(propGo);
        if (playerGo != null) Object.DestroyImmediate(playerGo);
        if (lanternGo != null) Object.DestroyImmediate(lanternGo);
    }

    [Test]
    public void HarmonicOscillatorOscillatesAndDecaysToZero()
    {
        propGo = new GameObject("SwayingProp");
        var prop = propGo.AddComponent<SwayingProp2D>();
        prop.NaturalFrequency = 4.0f;
        prop.DampingCoefficient = 1.5f;

        // Impart angular impulse
        prop.ApplyAngularImpulse(35f);
        Assert.That(prop.AngularVelocity, Is.EqualTo(35f));

        // Step forward by 0.05s
        prop.TickPhysics(0.05f);
        Assert.That(prop.CurrentAngle, Is.GreaterThan(0.5f), "Prop should swing in the positive angle direction.");

        // Simulate 4 seconds of damped oscillation
        for (int i = 0; i < 200; i++)
        {
            prop.TickPhysics(0.02f);
        }

        Assert.That(Mathf.Abs(prop.CurrentAngle), Is.LessThan(2.0f), "Prop must decay towards resting angle of 0 degrees.");
        Assert.That(Mathf.Abs(prop.AngularVelocity), Is.LessThan(2.0f), "Angular velocity must decay near 0.");
    }

    [Test]
    public void ClampsAngleToMaxAngle()
    {
        propGo = new GameObject("SwayingProp");
        var prop = propGo.AddComponent<SwayingProp2D>();
        prop.MaxAngle = 45f;

        // Apply excessive impulse
        prop.ApplyAngularImpulse(500f);
        for (int i = 0; i < 20; i++)
        {
            prop.TickPhysics(0.02f);
            Assert.That(prop.CurrentAngle, Is.LessThanOrEqualTo(45.01f), "Angle must never exceed maxAngle.");
            Assert.That(prop.CurrentAngle, Is.GreaterThanOrEqualTo(-45.01f), "Angle must never invert beyond -maxAngle.");
        }
    }

    [Test]
    public void RadialShockwaveAppliesDirectionalImpulse()
    {
        propGo = new GameObject("SwayingProp_Right");
        propGo.transform.position = new Vector3(3f, 0f, 0f);
        var prop = propGo.AddComponent<SwayingProp2D>();

        // Shockwave from left (0,0) towards right (3,0) should push in + direction
        prop.ApplyRadialShockwave(Vector2.zero, 30f, 10f);
        Assert.That(prop.AngularVelocity, Is.GreaterThan(10f), "Shockwave from the left must push rightwards (+ direction).");

        // Shockwave from far beyond radius (15,0) should have 0 impulse
        float oldVel = prop.AngularVelocity;
        prop.ApplyRadialShockwave(new Vector2(25f, 0f), 30f, 10f);
        Assert.That(prop.AngularVelocity, Is.EqualTo(oldVel), "Shockwave outside maxRadius must have zero effect.");
    }

    [UnityTest]
    public IEnumerator NearbyLanternFlareAppliesShockwave()
    {
        propGo = new GameObject("SwayingProp");
        propGo.transform.position = new Vector3(2f, 0f, 0f);
        var prop = propGo.AddComponent<SwayingProp2D>();

        lanternGo = new GameObject("LanternSource");
        lanternGo.transform.position = Vector3.zero;
        var fuel = lanternGo.AddComponent<LanternFuel>();
        fuel.Initialize(6f, 6f);
        var lantern = lanternGo.AddComponent<LanternController>();

        yield return null;

        Assert.That(prop.AngularVelocity, Is.EqualTo(0f));

        // Flare the lantern
        lantern.SetFlared(true);

        yield return null;

        Assert.That(prop.AngularVelocity, Is.GreaterThan(5f), "Nearby lantern Flare must buffet swaying prop via shockwave.");
    }

    [UnityTest]
    public IEnumerator PlayerBrushWithVelocityImpartsImpulse()
    {
        propGo = new GameObject("SwayingProp");
        propGo.transform.position = Vector3.zero;
        var prop = propGo.AddComponent<SwayingProp2D>();

        playerGo = new GameObject("PlayerTrigger");
        playerGo.transform.position = new Vector3(0f, -0.5f, 0f);
        var rb = playerGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(3.5f, 0f);
        var col = playerGo.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That(prop.AngularVelocity, Is.GreaterThan(5f), "Player brushing prop with horizontal velocity must impart swing impulse.");
    }

    [Test]
    public void BuildsVisualHierarchyWithPivotAndLampLight()
    {
        propGo = new GameObject("SwayingProp_Visuals");
        var prop = propGo.AddComponent<SwayingProp2D>();

        Assert.That(prop.PivotTransform, Is.Not.Null, "PivotTransform must be created.");
        Assert.That(prop.PivotTransform.childCount, Is.GreaterThan(0), "Chain and lamp visual elements must be generated.");
        Assert.That(prop.LampLight, Is.Not.Null, "Industrial cage lamp must have an attached Light2D.");
        Assert.That(prop.TriggerCollider, Is.Not.Null, "Trigger collider must be configured for physical interactions.");
        Assert.That(prop.TriggerCollider.isTrigger, Is.True, "Collider must be configured as a 2D trigger.");
    }
}
