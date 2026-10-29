using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TheLightenRoad.Codex;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.TestTools;
namespace TheLightenRoad.Frontend.Tests
{
    public class ArchiveCodexTests
    {
        private readonly List<Object> created=new List<Object>();
        T New<T>() where T:ScriptableObject{var x=ScriptableObject.CreateInstance<T>();created.Add(x);return x;}
        GameObject Go(string n){var x=new GameObject(n);created.Add(x);return x;}
        [SetUp] public void Setup()
        {
            var a=New<ArchiveLog>();a.Id="a";a.Title="Keeper";a.Text="A discovered record.";a.RoomClue="Drain the shelf.";
            var b=New<ArchiveLog>();b.Id="b";b.Title="Margin";b.Text=new string('x',1800);
            var catalog=New<ArchiveCatalog>();catalog.Logs=new[]{a,b};catalog.Channel=New<SocialChannel>();catalog.Channel.delivery=New<StoredDelivery>();catalog.Channel.membership=New<AmbientMembership>();CodexSession.Begin(catalog);
        }
        [TearDown] public void Cleanup(){foreach(var o in created)if(o!=null)Object.DestroyImmediate(o);created.Clear();CodexSession.End();Time.timeScale=1;}
        [Test] public void StandbyReaderKeepsDiscoveryButCreatesNoBookResourcesUntilAValidOpen()
        {
            var menu=ArchiveCodexMenu.CreateStandby();created.Add(menu.gameObject);
            Assert.That(menu.PresentationCreated,Is.False);
            Assert.That(menu.GetComponentsInChildren<Camera>(true),Is.Empty);
            Assert.That(menu.GetComponent<ArchiveNarration>(),Is.Null);
            menu.Open("missing");Assert.That(menu.PresentationCreated,Is.False);
            CodexSession.Discover("a");Assert.That(menu.EntryCount,Is.EqualTo(1));
            Assert.That(menu.PresentationCreated,Is.False,"Discovery must not allocate the 3D reader or start its narration worker.");
            menu.enabled=false;menu.enabled=true;Assert.That(menu.EntryCount,Is.EqualTo(1));
            menu.Open("a");Assert.That(menu.PresentationCreated,Is.True);Assert.That(ArchiveCodexMenu.IsOpen,Is.True);
            var view=menu.GetComponent<ArchiveBookPresentation>();Assert.That(view.IsInitialized,Is.True);
            menu.Close();menu.Open("a");Assert.That(menu.GetComponent<ArchiveBookPresentation>(),Is.SameAs(view));menu.Close();
        }
        [UnityTest] public IEnumerator StandbyStillShowsTheDiscoveryToastWithoutBuildingTheReader()
        {
            var menu=ArchiveCodexMenu.CreateStandby();created.Add(menu.gameObject);
            CodexSession.Discover("a");yield return null;
            var feed=menu.GetComponentInChildren<ArchiveFeedVisibility>(true);
            Assert.That(feed,Is.Not.Null);Assert.That(feed.GetComponent<CanvasGroup>().alpha,Is.GreaterThan(0));
            Assert.That(menu.PresentationCreated,Is.False);Assert.That(menu.GetComponentsInChildren<Camera>(true),Is.Empty);
        }
        [Test] public void UnopenedStandbyCannotUnpauseAnExistingReader()
        {
            CodexSession.Discover("a");var owner=Go("Reader owner").AddComponent<ArchiveCodexMenu>();owner.Open("a");
            var standby=ArchiveCodexMenu.CreateStandby();created.Add(standby.gameObject);
            standby.Close();standby.enabled=false;
            Assert.That(ArchiveCodexMenu.IsOpen,Is.True);Assert.That(Time.timeScale,Is.Zero);owner.Close();
            Assert.That(ArchiveCodexMenu.IsOpen,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ReaderOwnershipSuspendsLegacyInteractionAndPreservesOtherLeases()
        {
            var type=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("TheLightenRoad.Interaction.EmberVein")).First(t=>t!=null);
            var actor=(Behaviour)Go("Book legacy interaction").AddComponent(type);
            CodexSession.Discover("a");var menu=Go("Book ownership regression").AddComponent<ArchiveCodexMenu>();menu.Open("a");
            Assert.That(actor.enabled,Is.False,"The old raw E polling must stop while the book owns the screen.");
            var other=Go("Other modal owner");LegacyInteractionPollingGate.Acquire(other);GameControls.BlockGameplay(other,true);
            try
            {
                menu.Close();yield return null;yield return null;yield return null;
                Assert.That(actor.enabled,Is.False);Assert.That(GameControls.GameplayAllowed,Is.False);
            }
            finally {LegacyInteractionPollingGate.Release(other);GameControls.BlockGameplay(other,false);}
            yield return null;yield return null;yield return null;
            Assert.That(actor.enabled,Is.True);Assert.That(GameControls.GameplayAllowed,Is.True);
        }
        [Test] public void ReaderOwnershipCannotOpenOverAnotherModalOrAllocateItsPresentation()
        {
            CodexSession.Discover("a");var menu=ArchiveCodexMenu.CreateStandby();created.Add(menu.gameObject);
            var other=Go("Existing modal");GameControls.BlockGameplay(other,true);
            try
            {
                menu.Open("a");
                Assert.That(menu.PresentationCreated,Is.False);
                Assert.That(ArchiveCodexMenu.IsOpen,Is.False);
                Assert.That(Time.timeScale,Is.EqualTo(1));
            }
            finally {menu.Close();GameControls.BlockGameplay(other,false);}
        }
        [Test] public void LiveDiscoveryRescuesEmptyLanternAndDuplicateTerminalNeverRepays()
        {
            var fuel=Go("Fuel").AddComponent<LanternFuel>();fuel.Initialize(0,8);
            var terminal=Go("Terminal").AddComponent<DiegeticWhisper>();terminal.ArchiveLog=CodexSession.Catalog.Find("a");
            Assert.That(terminal.TryReadArchive(),Is.True);Assert.That(fuel.TotalEmbers,Is.EqualTo(1));
            Assert.That(CodexSession.Decryption,Is.EqualTo(50));Assert.That(CodexSession.FoundCount,Is.EqualTo(1));
            Assert.That(terminal.TryReadArchive(),Is.True);Assert.That(fuel.TotalEmbers,Is.EqualTo(1));
            fuel.ResetForChamber();fuel.TrySpendEmber();terminal.TryReadArchive();Assert.That(fuel.TotalEmbers,Is.EqualTo(2));
        }
        [Test] public void LiveLogCancelsDarknessCountdown()
        {
            var go=Go("Survivor");var fuel=go.AddComponent<LanternFuel>();go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>();
            var respawn=go.AddComponent<PlayerRespawnController>();fuel.Initialize(0,8);respawn.TickSurvival(1.8f);
            Assert.That(respawn.DarknessTimer,Is.GreaterThan(1));CodexSession.Discover("a");
            Assert.That(respawn.DarknessTimer,Is.Zero);respawn.TickSurvival(2);Assert.That(respawn.State,Is.EqualTo(RespawnState.Alive));
        }
        [Test] public void ReconstructedSceneListenersReplayWithoutAnotherReward()
        {
            var fuel=Go("Old fuel").AddComponent<LanternFuel>();CodexSession.Discover("a");Object.DestroyImmediate(fuel.gameObject);
            var fresh=Go("Reloaded fuel").AddComponent<LanternFuel>();Assert.That(fresh.TotalEmbers,Is.EqualTo(3));
            fresh.gameObject.SetActive(false);fresh.gameObject.SetActive(true);Assert.That(fresh.TotalEmbers,Is.EqualTo(3));
            Assert.That(CodexSession.Has("a"),Is.True);Assert.That(((StoredDelivery)CodexSession.Channel.delivery).Count,Is.EqualTo(1));
            Assert.That(CodexSession.Discover("a"),Is.False);Assert.That(CodexSession.Discover("missing"),Is.False);
        }
        [Test] public void RewardsClampAtFourAndSessionRestartClearsDiscovery()
        {
            var fuel=Go("Fuel").AddComponent<LanternFuel>();CodexSession.Discover("a");CodexSession.Discover("b");Assert.That(fuel.TotalEmbers,Is.EqualTo(4));
            Assert.That(CodexSession.Decryption,Is.EqualTo(100));var catalog=CodexSession.Catalog;CodexSession.Begin(catalog);Assert.That(CodexSession.FoundCount,Is.Zero);
        }
        [Test] public void OpeningKeepsOneHingeAndRevealsInkAfterCoverLands()
        {
            bool reduced=MenuPreferences.ReducedMotion;MenuPreferences.SetReducedMotion(false);
            try
            {
                CodexSession.Discover("a");var menu=Go("Book motion regression").AddComponent<ArchiveCodexMenu>();menu.Open("a");
                var tick=typeof(ArchiveCodexMenu).GetMethod("AnimateBook",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var view=menu.GetComponent<ArchiveBookPresentation>();var hinge=view.Model.CoverHinge;
                var ink=menu.GetComponentsInChildren<CanvasGroup>(true).Single(g=>g.name=="Book ink and controls");
                float previous=0;
                foreach(float time in new[]{0f,.35f,.8f,1.05f,1.4f,1.6f,2.1f})
                {
                    tick.Invoke(menu,new object[]{time-previous});previous=time;
                    Assert.That(hinge.localPosition,Is.EqualTo(Vector3.zero),"The physical hinge stays attached to the spine.");
                    Assert.That(view.Model.LeftAssembly.gameObject.activeSelf,Is.True,"The left folios rotate with the cover instead of appearing mid-animation.");
                    if(time<1.6f)Assert.That(hinge.gameObject.activeSelf,Is.True,"The physical cover must occlude the ink while opening.");
                    if(time>.7f && time<1.5f)
                        Assert.That(view.Model.transform.InverseTransformPoint(hinge.TransformPoint(new Vector3(3.08f,0,-.32f))).z,Is.LessThan(-.2f),"The cover must lift towards the reader, never rotate through the folio block.");
                    view.CapturePreview(1280,720,"Library/Readability/book-motion-"+Mathf.RoundToInt(time*100)+".png");
                }
                Assert.That(menu.IsAnimating,Is.False);Assert.That(ink.alpha,Is.EqualTo(1));menu.Close();
            }
            finally{MenuPreferences.SetReducedMotion(reduced);}
        }
        [UnityTest] public IEnumerator BookCanCloseDuringOpeningWithoutLeavingGameplayBlocked()
        {
            CodexSession.Discover("a");var menu=Go("Cinematic book").AddComponent<ArchiveCodexMenu>();
            menu.Open("a");yield return new WaitForSecondsRealtime(.45f);
            menu.GetComponent<ArchiveBookPresentation>().CapturePreview(1280,720,"Library/Readability/codex-closed-cover.png");
            yield return new WaitForSecondsRealtime(.55f);
            menu.GetComponent<ArchiveBookPresentation>().CapturePreview(1280,720,"Library/Readability/codex-opening.png");
            Assert.That(menu.GetComponent<ArchiveBookPresentation>().Model.GetComponentsInChildren<MeshFilter>().Length,Is.GreaterThan(20));
            menu.Close();
            Assert.That(menu.IsAnimating,Is.False);
            Assert.That(Time.timeScale,Is.EqualTo(1));yield return null;yield return null;Assert.That(GameControls.GameplayAllowed,Is.True);
            menu.Open("a");yield return new WaitForSecondsRealtime(2.2f);
            Assert.That(menu.IsAnimating,Is.False);
            Assert.That(menu.GetComponentsInChildren<CanvasGroup>().Any(g=>g.name=="Codex reading surface" && g.alpha==1 && g.interactable),Is.True);
            menu.Close();
        }
        [UnityTest] public IEnumerator TerminalDeskStaysAnchoredWhilePromptAnimates()
        {
            var go=Go("Office desk");go.transform.position=new Vector3(10,2,0);var terminal=go.AddComponent<DiegeticWhisper>();terminal.ArchiveLog=CodexSession.Catalog.Find("a");
            go.AddComponent<ArchiveTerminalVisual>();var start=go.transform.position;
            yield return null;terminal.TickWhisper(.2f);
            Assert.That(go.transform.position,Is.EqualTo(start));
            Assert.That(go.GetComponentInChildren<Canvas>().transform.localPosition.y,Is.GreaterThan(1));
        }
        [UnityTest] public IEnumerator PerspectiveReaderKeepsButtonsClickableAndDisablesCameraOnClose()
        {
            CodexSession.Discover("a");var menu=Go("Perspective reader").AddComponent<ArchiveCodexMenu>();
            var camera=menu.GetComponentsInChildren<Camera>(true).Single(c=>c.name=="Archive perspective camera");
            Assert.That(camera.enabled,Is.False);menu.Open("a");yield return new WaitForSecondsRealtime(2.3f);
            Assert.That(camera.enabled,Is.True);Assert.That(camera.orthographic,Is.False);
            var canvas=menu.GetComponentsInChildren<Canvas>().Single(c=>c.name=="Codex Canvas");
            Assert.That(canvas.worldCamera,Is.SameAs(menu.GetComponent<ArchiveBookPresentation>().InkCamera));
            var ink=menu.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Log text");Assert.That(ink.font.name,Is.EqualTo("ArchiveHand"));
            Assert.That(menu.GetComponent<ArchiveBookPresentation>().Model.LeftCollider.sharedMesh.vertexCount,Is.GreaterThan(100));
            foreach(var button in menu.GetComponentsInChildren<Button>().Where(b=>b.IsInteractable()))
            {
                var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                if(button.name=="Close")pointer.position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
                else
                {
                    var local=canvas.transform.InverseTransformPoint(button.transform.position);
                    pointer.position=menu.GetComponent<ArchiveBookPresentation>().ScreenPoint(new Vector3(local.x/100,(local.y-25)/100,-.16f));
                }
                var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                Assert.That(hits.Any(h=>h.gameObject==button.gameObject),Is.True,button.name+" must be clickable in perspective");
            }
            menu.Close();Assert.That(camera.enabled,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        [Test] public void ReadingPaginationPreservesWordsParagraphsAndAllText()
        {
            var text=string.Join("\n\n",Enumerable.Repeat("The old archive kept every letter, every promise, and every forgotten name.",30));
            CodexSession.Catalog.Find("a").Text=text;CodexSession.Discover("a");
            var menu=Go("Pagination regression").AddComponent<ArchiveCodexMenu>();menu.Open("a");
            var split=typeof(ArchiveCodexMenu).GetMethod("ReadingPages",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var chunks=(List<string>)split.Invoke(menu,new object[]{text});
            Assert.That(chunks.Count,Is.GreaterThan(1));Assert.That(string.Concat(chunks),Is.EqualTo(text));
            var body=menu.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Log text");
            foreach(var chunk in chunks)
            {
                Assert.That(body.GetPreferredValues(chunk,body.rectTransform.rect.width,0).y,Is.LessThanOrEqualTo(body.rectTransform.rect.height));
                if(chunk!=chunks.Last())Assert.That(char.IsWhiteSpace(chunk.Last()),Is.True,"Page breaks must preserve whole words.");
            }
            menu.Close();
        }
        [UnityTest] public IEnumerator ReaderNavigationSkipsDisabledPageActions()
        {
            CodexSession.Discover("a");var menu=Go("Reader navigation").AddComponent<ArchiveCodexMenu>();menu.Open("a");yield return new WaitForSecondsRealtime(2.3f);
            var buttons=menu.GetComponentsInChildren<Button>(true);var close=buttons.Single(b=>b.name=="Close");
            foreach(var button in buttons.Where(b=>b.interactable))
                foreach(var target in new[]{button.navigation.selectOnLeft,button.navigation.selectOnRight,button.navigation.selectOnUp,button.navigation.selectOnDown})
                    if(target!=null)Assert.That(target.IsInteractable(),Is.True,button.name+" points to a disabled action");
            Assert.That(close.navigation.selectOnUp,Is.Not.Null);menu.Close();
        }
        [UnityTest] public IEnumerator DossierReadingFitsSupportedScreensAndKeepsActionsSeparated()
        {
            CodexSession.Catalog.Find("a").Text=string.Join(" ",Enumerable.Repeat("The office kept its records in silence.",15));
            CodexSession.Discover("a");var menu=Go("Office reader").AddComponent<ArchiveCodexMenu>();menu.Open("a");yield return new WaitForSecondsRealtime(2.2f);
            var canvas=menu.GetComponentInChildren<Canvas>();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
            {
                menu.GetComponent<ArchiveBookPresentation>().CapturePreview(size.x,size.y,"Library/Readability/office-codex-"+size.x+".png");
                foreach(var label in menu.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Log text" || t.name=="Room clue" || t.name=="Log title"))
                    Assert.That(label.GetPreferredValues(label.text,label.rectTransform.rect.width,0).y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+2),label.name);
            }
            var buttons=menu.GetComponentsInChildren<Button>();Assert.That(buttons.Length,Is.EqualTo(5));
            var voice=buttons.Single(b=>b.name=="Pause voice");var replay=buttons.Single(b=>b.name=="Replay voice");
            Assert.That(voice.navigation.selectOnRight,Is.SameAs(replay));
            Assert.That(replay.navigation.selectOnDown,Is.Not.Null);
            Assert.That(voice.GetComponent<SettingsLightFocus>().UseInkPalette,Is.True);
            foreach(var first in buttons)
                foreach(var second in buttons.Where(b=>b!=first))
                {
                    var a=(RectTransform)first.transform;var b=(RectTransform)second.transform;
                    var boundsA=new Rect(a.anchoredPosition-a.sizeDelta*.5f,a.sizeDelta);
                    var boundsB=new Rect(b.anchoredPosition-b.sizeDelta*.5f,b.sizeDelta);
                    Assert.That(boundsA.Overlaps(boundsB),Is.False,first.name+" overlaps "+second.name);
                }
            Assert.That(buttons.All(b=>b.GetComponent<SettingsLightFocus>()!=null),Is.True);
            menu.Close();
        }
        [UnityTest] public IEnumerator CodexRebuildsEntriesOnEnableAndRestoresTimeAndInputOnClose()
        {
            CodexSession.Discover("a");var menu=Go("Reader").AddComponent<ArchiveCodexMenu>();yield return null;
            Assert.That(menu.EntryCount,Is.EqualTo(1));menu.Open("a");Assert.That(ArchiveCodexMenu.IsOpen,Is.True);Assert.That(Time.timeScale,Is.Zero);Assert.That(GameControls.GameplayAllowed,Is.False);
            yield return new WaitForSecondsRealtime(2.2f);
            menu.GetComponent<ArchiveBookPresentation>().CapturePreview(1280,720,"Library/Readability/codex-reading-720p.png");
            CodexSession.Discover("b");menu.Open("b");
            menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Next").onClick.Invoke();yield return new WaitForSecondsRealtime(1.35f);
            Assert.That(menu.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Page counter").text,Does.Contain("PAGE 2 /"));
            menu.Close();Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(ArchiveCodexMenu.IsOpen,Is.False);
            menu.gameObject.SetActive(false);menu.gameObject.SetActive(true);Assert.That(menu.EntryCount,Is.EqualTo(2));
            menu.Open("a");Object.DestroyImmediate(menu.gameObject);Assert.That(Time.timeScale,Is.EqualTo(1));yield return null;
        }
    }
}
