using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SoundEffectManagerTests
{
    private GameObject sfxObject;
    private SoundEffectManager sfxManager;
    private AudioClip testClip;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        var existing = Object.FindObjectsByType<SoundEffectManager>(FindObjectsSortMode.None);
        foreach (var e in existing)
        {
            Object.Destroy(e.gameObject);
        }
        SoundEffectManager.ResetInstanceForTesting(null);
        yield return null;

        testClip = AudioClip.Create("Test SFX", 44100, 1, 44100, false);
        float[] samples = new float[44100];
        for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / 44100f);
        testClip.SetData(samples, 0);

        sfxObject = new GameObject("Sound Effect Manager");
        sfxManager = sfxObject.AddComponent<SoundEffectManager>();

        sfxManager.ConfigureClips(
            walks: new[] { testClip },
            sprints: new[] { testClip },
            jump: testClip,
            landSoft: testClip,
            landHard: testClip,
            flare: testClip,
            emberBreak: testClip,
            emberPick: testClip,
            breath: testClip
        );

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (sfxObject != null) Object.Destroy(sfxObject);
        if (testClip != null) Object.Destroy(testClip);
        SoundEffectManager.ResetInstanceForTesting(null);
        yield return null;
    }

    [Test]
    public void BuildsAudioSourcePoolAndPlaysOneShots()
    {
        var src1 = sfxManager.GetAvailableSource();
        var src2 = sfxManager.GetAvailableSource();

        Assert.That(src1, Is.Not.Null);
        Assert.That(src2, Is.Not.Null);
        Assert.That(src1.spatialBlend, Is.EqualTo(0f));
    }

    [Test]
    public void PlaysFootstepsJumpsAndLandingsWithoutErrors()
    {
        Assert.DoesNotThrow(() => sfxManager.PlayWalkFootstep());
        Assert.DoesNotThrow(() => sfxManager.PlaySprintFootstep());
        Assert.DoesNotThrow(() => sfxManager.PlayJump());
        Assert.DoesNotThrow(() => sfxManager.PlayLanding(5f));
        Assert.DoesNotThrow(() => sfxManager.PlayLanding(14f));
    }

    [Test]
    public void PlaysFlareIgnitionAndEmberPickup()
    {
        Assert.DoesNotThrow(() => sfxManager.PlayFlareIgnite());
        Assert.DoesNotThrow(() => sfxManager.PlayEmberBreak());
        Assert.DoesNotThrow(() => sfxManager.PlayEmberPickup());
    }

    [Test]
    public void ControlsExhaustionLoopWithSmoothFade()
    {
        sfxManager.SetExhaustionActive(true);
        Assert.That(sfxManager.IsExhaustionPlaying, Is.True);

        sfxManager.TickExhaustion(0.5f);
        Assert.That(sfxManager.ExhaustionVolume, Is.GreaterThan(0.1f));

        sfxManager.SetExhaustionActive(false);
        sfxManager.TickExhaustion(0.6f);
        Assert.That(sfxManager.ExhaustionVolume, Is.EqualTo(0f).Within(0.01f));
        Assert.That(sfxManager.IsExhaustionPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator PlayerAudioControllerRespondsToEvents()
    {
        var playerGo = new GameObject("Test Player Audio");
        var motor = playerGo.AddComponent<PlayerMotor2D>();
        var stamina = playerGo.AddComponent<PlayerStamina>();
        var lantern = playerGo.AddComponent<LanternController>();
        var audioCtrl = playerGo.AddComponent<PlayerAudioController>();

        yield return null;

        // Verify footstep ticking
        Assert.DoesNotThrow(() => audioCtrl.TickFootsteps(0.5f));

        Object.Destroy(playerGo);
    }

    [Test]
    public void LoadsAllInternetClipsFromResources()
    {
        var go = new GameObject("Resource Loaded SFX Manager");
        var mgr = go.AddComponent<SoundEffectManager>();
        mgr.EnsureClipsLoaded();

        Assert.That(mgr.WalkClips, Is.Not.Null);
        Assert.That(mgr.WalkClips.Length, Is.EqualTo(4));
        Assert.That(mgr.SprintClips, Is.Not.Null);
        Assert.That(mgr.SprintClips.Length, Is.EqualTo(4));
        Assert.That(mgr.JumpClip, Is.Not.Null);
        Assert.That(mgr.LandingSoftClip, Is.Not.Null);
        Assert.That(mgr.LandingHardClip, Is.Not.Null);
        Assert.That(mgr.FlareIgniteClip, Is.Not.Null);
        Assert.That(mgr.EmberBreakClip, Is.Not.Null);
        Assert.That(mgr.EmberPickupClip, Is.Not.Null);
        Assert.That(mgr.ExhaustionBreathClip, Is.Not.Null);

        Object.Destroy(go);
    }

    [Test]
    public void AutoInstantiatesWhenInstanceAccessedWithoutSceneObject()
    {
        // Destroy existing test manager
        if (sfxObject != null) Object.Destroy(sfxObject);
        SoundEffectManager.ResetInstanceForTesting(null);

        var autoInstance = SoundEffectManager.Instance;
        Assert.That(autoInstance, Is.Not.Null);
        Assert.That(autoInstance.gameObject.name, Is.EqualTo("Sound Effect Manager"));
        Assert.That(autoInstance.WalkClips, Is.Not.Null);

        Object.Destroy(autoInstance.gameObject);
        SoundEffectManager.ResetInstanceForTesting(null);
    }

    [Test]
    public void TogglesMuteAndAdjustsVolume()
    {
        sfxManager.MasterSFXVolume = 1.0f;
        sfxManager.ToggleMute();
        Assert.That(sfxManager.MasterSFXVolume, Is.EqualTo(0f));

        sfxManager.ToggleMute();
        Assert.That(sfxManager.MasterSFXVolume, Is.EqualTo(1.0f));

        sfxManager.AdjustVolume(-0.2f);
        Assert.That(sfxManager.MasterSFXVolume, Is.EqualTo(0.8f).Within(0.01f));
    }

    [Test]
    public void AttachesToAllActiveMotors()
    {
        var motorGo = new GameObject("Test Motor For Attach");
        var motor = motorGo.AddComponent<PlayerMotor2D>();

        Assert.That(motorGo.GetComponent<PlayerAudioController>(), Is.Null);
        PlayerAudioController.AttachToAllMotors();
        Assert.That(motorGo.GetComponent<PlayerAudioController>(), Is.Not.Null);

        Object.Destroy(motorGo);
    }

    [UnityTest]
    public IEnumerator RecoversGracefullyIfAudioSourcesAreDestroyed()
    {
        // Obtain a source first
        var src = sfxManager.GetAvailableSource();
        Assert.That(src, Is.Not.Null);

        // Intentionally destroy all AudioSource components on the manager
        var allSources = sfxManager.GetComponents<AudioSource>();
        foreach (var s in allSources)
        {
            Object.Destroy(s);
        }
        yield return null; // wait 1 frame for Unity Object destruction to complete

        // Verify that calling playback does NOT throw MissingReferenceException and self-heals
        Assert.DoesNotThrow(() => sfxManager.PlayEmberPickup());
        Assert.DoesNotThrow(() => sfxManager.PlayWalkFootstep());
        Assert.DoesNotThrow(() => sfxManager.PlayJump());

        var recoveredSrc = sfxManager.GetAvailableSource();
        Assert.That(recoveredSrc, Is.Not.Null);
    }
}

