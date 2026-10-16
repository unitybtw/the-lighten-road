using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Audio;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BackgroundMusicManagerTests
{
    private GameObject musicObject;
    private BackgroundMusicManager musicManager;
    private AudioClip testClip;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Generate a 1-second sine wave clip for testing
        testClip = AudioClip.Create("Test BGM", 44100, 1, 44100, false);
        float[] samples = new float[44100];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / 44100f);
        }
        testClip.SetData(samples, 0);

        musicObject = new GameObject("Background Music Manager");
        musicManager = musicObject.AddComponent<BackgroundMusicManager>();
        musicManager.SetClip(testClip);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (musicObject != null)
        {
            Object.Destroy(musicObject);
        }
        if (testClip != null)
        {
            Object.Destroy(testClip);
        }
        yield return null;
    }

    [Test]
    public void ConfiguresAudioSourceAs2DStereoLooping()
    {
        musicManager.EnsureAudioSource();
        var source = musicManager.Source;

        Assert.That(source, Is.Not.Null);
        Assert.That(source.spatialBlend, Is.EqualTo(0f), "Background music must be 2D stereo (spatialBlend 0).");
        Assert.That(source.loop, Is.True, "Background music must loop continuously.");
        Assert.That(source.playOnAwake, Is.False, "AudioSource must not abruptly blast on awake without fade control.");
    }

    [Test]
    public void MaintainsSubtleRestingVolumeAndFadesInSmoothly()
    {
        // Target volume should be quiet and non-intrusive (<= 0.25)
        Assert.That(musicManager.TargetVolume, Is.LessThanOrEqualTo(0.25f),
            "Background music volume must be subtle and non-intrusive.");

        musicManager.Play(testClip, customFadeDuration: 0.5f);
        Assert.That(musicManager.IsPlaying, Is.True);
        Assert.That(musicManager.CurrentVolume, Is.EqualTo(0f).Within(0.01f),
            "Volume should start at 0 before fading in.");

        // Step halfway through fade
        musicManager.TickFade(0.25f);
        Assert.That(musicManager.CurrentVolume, Is.GreaterThan(0.05f));
        Assert.That(musicManager.CurrentVolume, Is.LessThan(musicManager.TargetVolume));

        // Complete fade
        musicManager.TickFade(0.3f);
        Assert.That(musicManager.CurrentVolume, Is.EqualTo(musicManager.TargetVolume).Within(0.01f),
            "Volume must settle exactly at target volume.");
    }

    [Test]
    public void FadesOutGracefullyAndStopsWhenStopped()
    {
        musicManager.Play(testClip, customFadeDuration: 0.1f);
        musicManager.TickFade(0.15f);
        Assert.That(musicManager.CurrentVolume, Is.EqualTo(musicManager.TargetVolume).Within(0.01f));

        musicManager.Stop(customFadeDuration: 0.2f);
        musicManager.TickFade(0.1f);
        Assert.That(musicManager.CurrentVolume, Is.LessThan(musicManager.TargetVolume));

        musicManager.TickFade(0.15f);
        Assert.That(musicManager.CurrentVolume, Is.EqualTo(0f).Within(0.001f));
        Assert.That(musicManager.IsPlaying, Is.False, "AudioSource should stop when fade-out reaches zero.");
    }

    [Test]
    public void AllowsDynamicFadeToCustomVolumeLevel()
    {
        musicManager.Play(testClip, customFadeDuration: 0.1f);
        musicManager.TickFade(0.15f);

        // Duck volume (e.g. for pause menu or intense moment)
        musicManager.FadeTo(0.08f, duration: 0.2f);
        musicManager.TickFade(0.25f);

        Assert.That(musicManager.CurrentVolume, Is.EqualTo(0.08f).Within(0.01f));
    }
}
