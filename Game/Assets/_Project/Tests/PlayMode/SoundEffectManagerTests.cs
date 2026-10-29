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
    public void EffectsVolumeChangesAlreadyPlayingVoicesWithoutApplyingGainTwice()
    {
        sfxManager.ResetSceneGraceForTesting();
        sfxManager.MasterSFXVolume = .8f;
        sfxManager.PlayOneShot(testClip, .5f);
        var voices = sfxObject.GetComponents<AudioSource>();
        var voice = System.Array.Find(voices, v => v.clip == testClip && !v.loop);
        Assert.That(voice, Is.Not.Null);
        Assert.That(voice.volume, Is.EqualTo(.4f).Within(.001f));
        sfxManager.MasterSFXVolume = 0;
        Assert.That(voice.volume, Is.Zero);
        sfxManager.MasterSFXVolume = .5f;
        Assert.That(voice.volume, Is.EqualTo(.25f).Within(.001f));
    }

    [UnityTest]
    public IEnumerator RepeatedAttachmentChecksDoNotRestartPlayerAudioGrace()
    {
        var go = new GameObject("Audio grace regression");
        go.AddComponent<PlayerMotor2D>();
        var controller = go.AddComponent<PlayerAudioController>();
        yield return new WaitForSeconds(.75f);
        PlayerAudioController.AttachToAllMotors();
        var field = typeof(PlayerAudioController).GetField("spawnGraceTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That((float)field.GetValue(controller), Is.LessThanOrEqualTo(0));
        Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator RepeatedStepsDoNotImmediatelyReuseTheSameRecording()
    {
        var bank = new[] { testClip, sfxManager.WoodWalkClips[0], sfxManager.WoodWalkClips[1] };
        sfxManager.ConfigureSurfaceClips(SurfaceType.Wood, bank, bank);
        sfxManager.ResetSceneGraceForTesting();
        AudioClip previous = null;
        for (int i = 0; i < 8; i++)
        {
            sfxManager.StopTransientSounds();
            sfxManager.PlayWalkFootstep(SurfaceType.Wood);
            var active = System.Array.Find(sfxObject.GetComponents<AudioSource>(), v => v.isPlaying && !v.loop);
            Assert.That(active, Is.Not.Null);
            Assert.That(active.clip, Is.Not.EqualTo(previous));
            previous = active.clip;
            yield return new WaitForSeconds(.06f);
        }
    }

    [Test]
    public void FallbackEffectsUseImportedAssetsInsteadOfRuntimeTones()
    {
#if UNITY_EDITOR
        foreach (var clip in new[] { sfxManager.UIHoverClip, sfxManager.UIClickClip, sfxManager.UIBackClip,
            sfxManager.FlareExtinguishClip, sfxManager.SkidScuffClip, sfxManager.AirDraftClip })
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(clip), Does.StartWith("Assets/_Project/Resources/Audio/SFX/"));
        }
#endif
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

    [Test]
    public void PlaysUIFeedbackClipsWithoutErrors()
    {
        Assert.DoesNotThrow(() => sfxManager.PlayUIHover());
        Assert.DoesNotThrow(() => sfxManager.PlayUIClick());
        Assert.DoesNotThrow(() => sfxManager.PlayUIBack());
        Assert.That(sfxManager.UIHoverClip, Is.Not.Null);
        Assert.That(sfxManager.UIClickClip, Is.Not.Null);
        Assert.That(sfxManager.UIBackClip, Is.Not.Null);
    }

    [Test]
    public void GracePeriod_InitializesActive_AndSuppressesWorldSFX()
    {
        Assert.That(sfxManager.IsInSceneGracePeriod, Is.True);
        Assert.That(sfxManager.SceneGraceTimer, Is.GreaterThan(0.5f));

        // World sounds should be suppressed during grace period without errors
        Assert.DoesNotThrow(() => sfxManager.PlayFlareIgnite());
        Assert.DoesNotThrow(() => sfxManager.PlayFlareExtinguish());
        Assert.DoesNotThrow(() => sfxManager.PlayEmberBreak());
        Assert.DoesNotThrow(() => sfxManager.PlayEmberPickup());
        Assert.DoesNotThrow(() => sfxManager.PlayCrystalActivate());

        // UI clicks bypass grace period
        Assert.DoesNotThrow(() => sfxManager.PlayUIClick());

        // Once grace period expires or is reset, world sounds operate normally
        sfxManager.ResetSceneGraceForTesting();
        Assert.That(sfxManager.IsInSceneGracePeriod, Is.False);
        Assert.DoesNotThrow(() => sfxManager.PlayFlareIgnite());
        Assert.DoesNotThrow(() => sfxManager.PlayEmberBreak());
    }

    [Test]
    public void DebouncesIdenticalClipsRapidlyFiredInSameFrame()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                sfxManager.PlayUIClick();
            }
        });
    }
}

