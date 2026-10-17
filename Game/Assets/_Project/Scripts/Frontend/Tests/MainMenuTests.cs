using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Audio;
using TheLightenRoad.Frontend;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MainMenuTests
{
    private GameObject menuObject;
    private MainMenuController menu;
    private float master, music, effects, listener;
    private bool reduced;
    private readonly string[] floatKeys = { "TLR.Menu.Master", "TLR.Menu.Music", "TLR.Menu.Effects" };
    private bool[] existed;
    private bool motionExisted;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        existed = new bool[floatKeys.Length];
        for (int i=0;i<floatKeys.Length;i++) existed[i]=PlayerPrefs.HasKey(floatKeys[i]);
        motionExisted=PlayerPrefs.HasKey("TLR.Menu.ReducedMotion");
        master=MenuPreferences.Master; music=MenuPreferences.Music; effects=MenuPreferences.Effects;
        reduced=MenuPreferences.ReducedMotion;listener=AudioListener.volume;
        MenuPreferences.SetReducedMotion(false);
        menuObject=new GameObject("Menu under test");
        menu=menuObject.AddComponent<MainMenuController>();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Teardown()
    {
        Object.Destroy(menuObject);
        var service=Object.FindAnyObjectByType<MenuPreferences>();
        if(service!=null) Object.Destroy(service.gameObject);
        yield return null;
        MenuPreferences.SetAudio(master,music,effects);MenuPreferences.SetReducedMotion(reduced);
        for(int i=0;i<floatKeys.Length;i++)if(!existed[i])PlayerPrefs.DeleteKey(floatKeys[i]);
        if(!motionExisted)PlayerPrefs.DeleteKey("TLR.Menu.ReducedMotion");
        MenuPreferences.Save();AudioListener.volume=listener;
    }

    [UnityTest]
    public IEnumerator SkippingIntroUnlocksNavigationAndBackReturnsHome()
    {
        Assert.That(menu.IntroComplete,Is.False);
        menu.SkipIntro();menu.ShowSettings();
        yield return new WaitForSecondsRealtime(0.35f);
        Assert.That(menu.IntroComplete,Is.True);
        Assert.That(menu.CurrentView,Is.EqualTo("settings"));
        Assert.That(menu.Canvas.transform.Find("Safe area/Settings").GetComponent<CanvasGroup>().interactable,Is.True);
        Assert.That(menu.Canvas.transform.Find("Safe area/Home").GetComponent<CanvasGroup>().interactable,Is.False);
        menu.ShowCredits();Assert.That(menu.CurrentView,Is.EqualTo("credits"));
        menu.ShowQuit();Assert.That(menu.CurrentView,Is.EqualTo("quit"));
        menu.ShowHome();Assert.That(menu.CurrentView,Is.EqualTo("home"));
    }

    [UnityTest]
    public IEnumerator SlidersApplyAudioAndSurviveClosingSettings()
    {
        menu.SkipIntro();menu.ShowSettings();
        var sliders=menu.Canvas.GetComponentsInChildren<Slider>();
        Assert.That(sliders.Length,Is.EqualTo(3));
        sliders[0].value=0.37f;sliders[1].value=0.42f;sliders[2].value=0.23f;
        menu.ShowHome();menu.ShowSettings();yield return null;
        Assert.That(AudioListener.volume,Is.EqualTo(0.37f).Within(0.001f));
        Assert.That(MenuPreferences.Music,Is.EqualTo(0.42f).Within(0.001f));
        Assert.That(MenuPreferences.Effects,Is.EqualTo(0.23f).Within(0.001f));
        Assert.That(sliders[0].value,Is.EqualTo(0.37f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator SavedReducedMotionSkipsOpeningOnNextMenu()
    {
        MenuPreferences.SetReducedMotion(true);
        Object.Destroy(menuObject);yield return null;
        menuObject=new GameObject("Second menu");menu=menuObject.AddComponent<MainMenuController>();
        yield return null;
        Assert.That(menu.IntroComplete,Is.True);
        Assert.That(menu.CurrentView,Is.EqualTo("home"));
    }
}
