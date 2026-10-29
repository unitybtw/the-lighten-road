using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TheLightenRoad.Codex;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace TheLightenRoad.Frontend.Tests
{
 public sealed class ArchiveNarrationTests
 {
  private readonly List<Object> owned=new List<Object>();
  T Asset<T>() where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();owned.Add(a);return a;}
  GameObject Host(){var go=new GameObject("Narration regression");owned.Add(go);return go;}
  [TearDown] public void Cleanup(){foreach(var o in owned)if(o!=null)Object.DestroyImmediate(o);owned.Clear();CodexSession.End();Time.timeScale=1;}
  [Test] public void DisablingNarrationAcceptsAnEmptyReloadedRequestPath()
  {
   var narration=Host().AddComponent<ArchiveNarration>();
   typeof(ArchiveNarration).GetField("requestFile",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(narration,"");
   Assert.DoesNotThrow(()=>narration.enabled=false);
  }
  [Test] public void CacheKeyMatchesOfflineWorkerAndInvalidatesEditedText()
  {
   var catalog=Resources.Load<ArchiveCatalog>("Codex/ArchiveCatalog");var log=catalog.Find("office-ledger");
   string text=ArchiveNarration.SpokenText(log);
   Assert.That(ArchiveNarration.CacheKey(text),Is.EqualTo("d2d80a97424247b8230decbbea94018f0d3db35c1d0c71abc641c98b82bcf4b8"));
   Assert.That(ArchiveNarration.CacheKey(text+" New words."),Is.Not.EqualTo(ArchiveNarration.CacheKey(text)));
  }
  [UnityTest] public IEnumerator BakedCatalogClipsLoadOfflineAndReuseOneClip()
  {
   var narration=Host().AddComponent<ArchiveNarration>();var catalog=Resources.Load<ArchiveCatalog>("Codex/ArchiveCatalog");
   foreach(var log in catalog.Logs)
   {
    narration.Prepare(log);float until=Time.realtimeSinceStartup+10;
    while(narration.IsPending(log)&&Time.realtimeSinceStartup<until)yield return null;
    var clip=narration.GetReadyClip(log);Assert.That(clip,Is.Not.Null,log.Id);Assert.That(clip.length,Is.GreaterThan(3));
    narration.Prepare(log);Assert.That(narration.GetReadyClip(log),Is.SameAs(clip));Assert.That(narration.IsPending(log),Is.False);
   }
  }
  [UnityTest] public IEnumerator NarrationWaitsForCoverAndStopsWhenReaderCloses()
  {
   var log=Asset<ArchiveLog>();log.Id="reading";log.Title="A quiet room";log.Text="The keeper left a light burning.";
   var clip=AudioClip.Create("Narration fixture",24000*8,1,24000,false);owned.Add(clip);log.Audio=clip;
   var catalog=Asset<ArchiveCatalog>();catalog.Logs=new[]{log};catalog.Channel=Asset<SocialChannel>();catalog.Channel.delivery=Asset<StoredDelivery>();catalog.Channel.membership=Asset<AmbientMembership>();CodexSession.Begin(catalog);CodexSession.Discover(log.Id);
   var menu=Host().AddComponent<ArchiveCodexMenu>();menu.Open(log.Id);
   Assert.That(menu.IsNarrationPlaying,Is.False);
   yield return new WaitForSecondsRealtime(2.3f);Assert.That(menu.IsNarrationPlaying,Is.True);
   menu.Close();Assert.That(menu.IsNarrationPlaying,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));
   menu.Open(log.Id);menu.Close();yield return new WaitForSecondsRealtime(2.3f);Assert.That(menu.IsNarrationPlaying,Is.False);
  }
  [UnityTest] public IEnumerator ChangingRecordStopsOldAudioAndLateLoadCannotReopenClosedBook()
  {
   var a=Asset<ArchiveLog>();a.Id="first";a.Title="First";a.Text="The first recording.";
   var b=Asset<ArchiveLog>();b.Id="second";b.Title="Second";b.Text="The second recording.";
   a.Audio=AudioClip.Create("First voice",24000*8,1,24000,false);owned.Add(a.Audio);
   b.Audio=AudioClip.Create("Second voice",24000*8,1,24000,false);owned.Add(b.Audio);
   var catalog=Asset<ArchiveCatalog>();catalog.Logs=new[]{a,b};catalog.Channel=Asset<SocialChannel>();catalog.Channel.delivery=Asset<StoredDelivery>();catalog.Channel.membership=Asset<AmbientMembership>();CodexSession.Begin(catalog);CodexSession.Discover(a.Id);CodexSession.Discover(b.Id);
   var menu=Host().AddComponent<ArchiveCodexMenu>();menu.Open(a.Id);yield return new WaitForSecondsRealtime(2.3f);
   Assert.That(menu.GetComponent<AudioSource>().clip,Is.SameAs(a.Audio));
   typeof(ArchiveCodexMenu).GetMethod("Turn",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(menu,new object[]{1});
   Assert.That(menu.IsNarrationPlaying,Is.False);yield return new WaitForSecondsRealtime(1.35f);
   Assert.That(menu.GetComponent<AudioSource>().clip,Is.SameAs(b.Audio));Assert.That(menu.IsNarrationPlaying,Is.True);menu.Close();
   var real=Resources.Load<ArchiveCatalog>("Codex/ArchiveCatalog");CodexSession.Begin(real);CodexSession.Discover("office-ledger");
   menu.gameObject.SetActive(false);menu.gameObject.SetActive(true);menu.Open("office-ledger");menu.Close();
   yield return new WaitForSecondsRealtime(2.5f);Assert.That(menu.IsNarrationPlaying,Is.False);
  }
  ArchiveCodexMenu ReaderWithVoice()
  {
   var log=Asset<ArchiveLog>();log.Id="voice-controls";log.Title="The quiet archive";log.Text="A record for the return journey.";
   log.Audio=AudioClip.Create("Voice controls fixture",24000*8,1,24000,false);owned.Add(log.Audio);
   var catalog=Asset<ArchiveCatalog>();catalog.Logs=new[]{log};catalog.Channel=Asset<SocialChannel>();
   catalog.Channel.delivery=Asset<StoredDelivery>();catalog.Channel.membership=Asset<AmbientMembership>();
   CodexSession.Begin(catalog);CodexSession.Discover(log.Id);
   var menu=Host().AddComponent<ArchiveCodexMenu>();menu.Open(log.Id);return menu;
  }
  [UnityTest] public IEnumerator PauseBeforeOpeningCompletesPreventsLateAutoplayAndCloseClearsPause()
  {
   var menu=ReaderWithVoice();menu.ToggleNarration();Assert.That(menu.IsNarrationPaused,Is.True);
   yield return new WaitForSecondsRealtime(2.35f);
   Assert.That(menu.IsNarrationPlaying,Is.False,"A ready clip must respect a pending pause.");
   menu.ToggleNarration();yield return null;
   Assert.That(menu.IsNarrationPlaying,Is.True);Assert.That(menu.IsNarrationPaused,Is.False);
   menu.ToggleNarration();menu.Close();Assert.That(menu.IsNarrationPaused,Is.False);
   menu.ToggleNarration();menu.ReplayNarration();yield return null;
   Assert.That(menu.IsNarrationPlaying,Is.False,"Closed readers must ignore voice controls.");
  }
  [UnityTest] public IEnumerator NarrationUsesEffectsGainOnceWhileTheListenerOwnsMasterGain()
  {
   float master=MenuPreferences.Master,music=MenuPreferences.Music,effects=MenuPreferences.Effects;
   var menu=ReaderWithVoice();
   try
   {
    MenuPreferences.SetAudio(.4f,music,.6f);yield return null;yield return null;
    Assert.That(AudioListener.volume,Is.EqualTo(.4f).Within(.001f));
    Assert.That(menu.GetComponent<AudioSource>().volume,Is.EqualTo(.6f).Within(.001f),"The master setting must not attenuate narration twice.");
    MenuPreferences.SetAudio(.7f,music,.25f);yield return null;yield return null;
    Assert.That(menu.GetComponent<AudioSource>().volume,Is.EqualTo(.25f).Within(.001f));
   }
   finally {menu.Close();MenuPreferences.SetAudio(master,music,effects);}
  }
  [UnityTest] public IEnumerator PausePreservesPlaybackPositionAndReplayStartsFromBeginning()
  {
   var menu=ReaderWithVoice();yield return new WaitForSecondsRealtime(2.8f);
   var source=menu.GetComponent<AudioSource>();Assert.That(menu.IsNarrationPlaying,Is.True);
   menu.ToggleNarration();int pausedAt=source.timeSamples;Assert.That(pausedAt,Is.GreaterThan(0));
   yield return new WaitForSecondsRealtime(.25f);
   Assert.That(source.timeSamples,Is.EqualTo(pausedAt));Assert.That(menu.IsNarrationPlaying,Is.False);
   menu.ToggleNarration();yield return new WaitForSecondsRealtime(.2f);
   Assert.That(source.timeSamples,Is.GreaterThan(pausedAt));
   menu.ToggleNarration();menu.ReplayNarration();yield return null;
   Assert.That(menu.IsNarrationPaused,Is.False);Assert.That(menu.IsNarrationPlaying,Is.True);
   Assert.That(source.timeSamples,Is.LessThan(pausedAt));menu.Close();
  }
  [UnityTest] public IEnumerator NativeWorkerGeneratesWithoutBlockingFramesAndCachesResult()
  {
#if UNITY_EDITOR_OSX
   string exe=System.IO.Path.Combine(Application.streamingAssetsPath,"Kokoro/runtime-macos-arm64/archive-voice-worker/archive-voice-worker");
   if(!System.IO.File.Exists(exe))Assert.Ignore("Provision the native worker to run the offline synthesis integration test.");
   var log=Asset<ArchiveLog>();log.Id="worker-test";log.Title="An archive test";log.Text="Keep a light for the return journey. The room is quiet, and the lamps are still burning. "+Guid.NewGuid().ToString("N").Substring(0,4)+".";
   var narration=Host().AddComponent<ArchiveNarration>();narration.Prepare(log);float until=Time.realtimeSinceStartup+45;int frames=0;
   while(narration.IsPending(log)&&Time.realtimeSinceStartup<until){frames++;yield return null;}
   Assert.That(narration.IsPending(log),Is.False);Assert.That(narration.GetReadyClip(log),Is.Not.Null);Assert.That(frames,Is.GreaterThan(2));
   string path=System.IO.Path.Combine(Application.persistentDataPath,"Narration",ArchiveNarration.CacheKey(ArchiveNarration.SpokenText(log))+".wav");
   Assert.That(System.IO.File.Exists(path),Is.True);System.IO.File.Delete(path);
#else
   Assert.Ignore("Native worker provisioning is currently verified on macOS ARM64 only.");yield break;
#endif
  }
 }
}
