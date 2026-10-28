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
        [UnityTest] public IEnumerator TerminalDeskStaysAnchoredWhilePromptAnimates()
        {
            var go=Go("Office desk");go.transform.position=new Vector3(10,2,0);var terminal=go.AddComponent<DiegeticWhisper>();terminal.ArchiveLog=CodexSession.Catalog.Find("a");
            go.AddComponent<ArchiveTerminalVisual>();var start=go.transform.position;
            yield return null;terminal.TickWhisper(.2f);
            Assert.That(go.transform.position,Is.EqualTo(start));
            Assert.That(go.GetComponentInChildren<Canvas>().transform.localPosition.y,Is.GreaterThan(1));
        }
        [UnityTest] public IEnumerator DossierReadingFitsSupportedScreensAndKeepsActionsSeparated()
        {
            CodexSession.Catalog.Find("a").Text=string.Join(" ",Enumerable.Repeat("The office kept its records in silence.",15));
            CodexSession.Discover("a");var menu=Go("Office reader").AddComponent<ArchiveCodexMenu>();menu.Open("a");yield return new WaitForSecondsRealtime(.22f);
            var canvas=menu.GetComponentInChildren<Canvas>();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
            {
                MenuReadabilityTests.Capture(canvas,size.x,size.y,"office-codex-"+size.x);
                foreach(var label in menu.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Log text" || t.name=="Room clue" || t.name=="Log title"))
                    Assert.That(label.GetPreferredValues(label.text,label.rectTransform.rect.width,0).y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+2),label.name);
            }
            var buttons=menu.GetComponentsInChildren<Button>();Assert.That(buttons.Length,Is.EqualTo(3));
            Assert.That(buttons.All(b=>b.GetComponent<SettingsLightFocus>()!=null),Is.True);
            menu.Close();
        }
        [UnityTest] public IEnumerator CodexRebuildsEntriesOnEnableAndRestoresTimeAndInputOnClose()
        {
            CodexSession.Discover("a");var menu=Go("Reader").AddComponent<ArchiveCodexMenu>();yield return null;
            Assert.That(menu.EntryCount,Is.EqualTo(1));menu.Open("a");Assert.That(ArchiveCodexMenu.IsOpen,Is.True);Assert.That(Time.timeScale,Is.Zero);Assert.That(GameControls.GameplayAllowed,Is.False);
            yield return new WaitForSecondsRealtime(.22f);
            MenuReadabilityTests.Capture(menu.GetComponentInChildren<Canvas>(),1280,720,"codex-reading-720p");
            CodexSession.Discover("b");menu.Open("b");
            menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Next").onClick.Invoke();
            Assert.That(menu.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Page counter").text,Does.Contain("PAGE 2 / 3"));
            menu.Close();Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(ArchiveCodexMenu.IsOpen,Is.False);
            menu.gameObject.SetActive(false);menu.gameObject.SetActive(true);Assert.That(menu.EntryCount,Is.EqualTo(2));
            menu.Open("a");Object.DestroyImmediate(menu.gameObject);Assert.That(Time.timeScale,Is.EqualTo(1));yield return null;
        }
    }
}
