using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Codex;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend.Tests
{
    public class ArchiveCodexTests
    {
        private readonly List<Object> created = new List<Object>();
        private readonly List<Scene> temporaryScenes = new List<Scene>();
        private Scene originalScene;

        private T New<T>() where T : ScriptableObject { var x = ScriptableObject.CreateInstance<T>(); created.Add(x); return x; }
        private GameObject Go(string n) { var x = new GameObject(n); created.Add(x); return x; }

        [SetUp]
        public void Setup()
        {
            originalScene = SceneManager.GetActiveScene();
            var a = New<ArchiveLog>();
            a.Id = "a";
            a.Title = "The Keeper's Last Shift";
            a.Location = "CORPORATE OFFICE / RECEPTION";
            a.Text = "A discovered record from the quiet lower office. We left our lamps burning beside the ledger.";
            a.RoomClue = "Drain the shelf vein to freeze it at its current position.";

            var b = New<ArchiveLog>();
            b.Id = "b";
            b.Title = "A Margin in the Dark";
            b.Location = "UPPER ARCHIVE / HALL";
            b.Text = string.Join(" ", Enumerable.Repeat("They called the light an endless gift. We counted every ember.", 30));
            b.RoomClue = "Powered water is dangerous; drained water is safe.";

            var catalog = New<ArchiveCatalog>();
            catalog.Logs = new[] { a, b };
            catalog.Channel = New<SocialChannel>();
            catalog.Channel.delivery = New<StoredDelivery>();
            catalog.Channel.membership = New<AmbientMembership>();
            CodexSession.Begin(catalog);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            CodexSession.End();
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator CleanupScenes()
        {
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            foreach (var scene in temporaryScenes) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            temporaryScenes.Clear();
        }

        [UnityTest]
        public IEnumerator AdditiveRoomKeepsItsOwnStandbyAfterThePreviousRoomUnloads()
        {
            var previous = SceneManager.CreateScene("Previous reader room"); temporaryScenes.Add(previous);
            var destination = SceneManager.CreateScene("Destination reader room"); temporaryScenes.Add(destination);
            SceneManager.SetActiveScene(previous);
            var oldReader = ArchiveCodexMenu.CreateStandby();
            var player = Go("Destination reader player"); SceneManager.MoveGameObjectToScene(player, destination); player.AddComponent<PlayerMotor2D>();
            typeof(ArchiveCodexMenu).GetMethod("Install", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { destination, LoadSceneMode.Additive });
            var reader = destination.GetRootGameObjects().Select(r => r.GetComponent<ArchiveCodexMenu>()).Single(r => r != null);
            Assert.That(reader, Is.Not.SameAs(oldReader)); Assert.That(reader.PresentationCreated, Is.False);
            CodexSession.Discover("a"); Assert.That(reader.EntryCount, Is.EqualTo(1));
            SceneManager.SetActiveScene(destination); yield return SceneManager.UnloadSceneAsync(previous);
            Assert.That(oldReader == null, Is.True); Assert.That(reader != null && reader.isActiveAndEnabled, Is.True);
            reader.Open("a"); Assert.That(ArchiveCodexMenu.IsOpen, Is.True); reader.Close();
        }

        [Test]
        public void StandbyReaderKeepsDiscoveryButCreatesNoBookResourcesUntilAValidOpen()
        {
            var menu = ArchiveCodexMenu.CreateStandby(); created.Add(menu.gameObject);
            Assert.That(menu.PresentationCreated, Is.False);
            Assert.That(menu.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(menu.GetComponent<ArchiveNarration>(), Is.Null);
            menu.Open("missing"); Assert.That(menu.PresentationCreated, Is.False);
            CodexSession.Discover("a"); Assert.That(menu.EntryCount, Is.EqualTo(1));
            Assert.That(menu.PresentationCreated, Is.False, "Discovery must not allocate the book UI or start its narration worker.");
            menu.enabled = false; menu.enabled = true; Assert.That(menu.EntryCount, Is.EqualTo(1));
            menu.Open("a"); Assert.That(menu.PresentationCreated, Is.True); Assert.That(ArchiveCodexMenu.IsOpen, Is.True);
            var view = menu.GetComponent<ArchiveBookPresentation>(); Assert.That(view.IsInitialized, Is.True);
            Assert.That(menu.GetComponentsInChildren<Camera>(true), Is.Empty, "The 2D pixel book must not spawn any 3D/secondary camera.");
            menu.Close(); menu.Open("a"); Assert.That(menu.GetComponent<ArchiveBookPresentation>(), Is.SameAs(view)); menu.Close();
        }

        [UnityTest]
        public IEnumerator StandbyStillShowsTheDiscoveryToastWithoutBuildingTheReader()
        {
            var menu = ArchiveCodexMenu.CreateStandby(); created.Add(menu.gameObject);
            CodexSession.Discover("a"); yield return null;
            var feed = menu.GetComponentInChildren<ArchiveFeedVisibility>(true);
            Assert.That(feed, Is.Not.Null); Assert.That(feed.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f));
            Assert.That(menu.PresentationCreated, Is.False); Assert.That(menu.GetComponentsInChildren<Camera>(true), Is.Empty);
        }

        [Test]
        public void UnopenedStandbyCannotUnpauseAnExistingReader()
        {
            CodexSession.Discover("a"); var owner = Go("Reader owner").AddComponent<ArchiveCodexMenu>(); owner.Open("a");
            var standby = ArchiveCodexMenu.CreateStandby(); created.Add(standby.gameObject);
            standby.Close(); standby.enabled = false;
            Assert.That(ArchiveCodexMenu.IsOpen, Is.True); Assert.That(Time.timeScale, Is.Zero); owner.Close();
            Assert.That(ArchiveCodexMenu.IsOpen, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ReaderOwnershipSuspendsLegacyInteractionAndPreservesOtherLeases()
        {
            var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TheLightenRoad.Interaction.EmberVein")).First(t => t != null);
            var actor = (Behaviour)Go("Book legacy interaction").AddComponent(type);
            CodexSession.Discover("a"); var menu = Go("Book ownership regression").AddComponent<ArchiveCodexMenu>(); menu.Open("a");
            Assert.That(actor.enabled, Is.False, "The old raw E polling must stop while the book owns the screen.");
            var other = Go("Other modal owner"); LegacyInteractionPollingGate.Acquire(other); GameControls.BlockGameplay(other, true);
            try
            {
                menu.Close(); yield return null; yield return null; yield return null;
                Assert.That(actor.enabled, Is.False); Assert.That(GameControls.GameplayAllowed, Is.False);
            }
            finally { LegacyInteractionPollingGate.Release(other); GameControls.BlockGameplay(other, false); }
            yield return null; yield return null; yield return null;
            Assert.That(actor.enabled, Is.True); Assert.That(GameControls.GameplayAllowed, Is.True);
        }

        [Test]
        public void ReaderOwnershipCannotOpenOverAnotherModalOrAllocateItsPresentation()
        {
            CodexSession.Discover("a"); var menu = ArchiveCodexMenu.CreateStandby(); created.Add(menu.gameObject);
            var other = Go("Existing modal"); GameControls.BlockGameplay(other, true);
            try
            {
                menu.Open("a");
                Assert.That(menu.PresentationCreated, Is.False);
                Assert.That(ArchiveCodexMenu.IsOpen, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally { menu.Close(); GameControls.BlockGameplay(other, false); }
        }

        [Test]
        public void LiveDiscoveryRescuesEmptyLanternAndDuplicateTerminalNeverRepays()
        {
            var fuel = Go("Fuel").AddComponent<LanternFuel>(); fuel.Initialize(0, 8);
            var terminal = Go("Terminal").AddComponent<DiegeticWhisper>(); terminal.ArchiveLog = CodexSession.Catalog.Find("a");
            Assert.That(terminal.TryReadArchive(), Is.True); Assert.That(fuel.TotalEmbers, Is.EqualTo(1));
            Assert.That(CodexSession.Decryption, Is.EqualTo(50)); Assert.That(CodexSession.FoundCount, Is.EqualTo(1));
            Assert.That(terminal.TryReadArchive(), Is.True); Assert.That(fuel.TotalEmbers, Is.EqualTo(1));
            fuel.ResetForChamber(); fuel.TrySpendEmber(); terminal.TryReadArchive(); Assert.That(fuel.TotalEmbers, Is.EqualTo(2));
        }

        [Test]
        public void LiveLogCancelsDarknessCountdown()
        {
            var go = Go("Survivor"); var fuel = go.AddComponent<LanternFuel>(); go.AddComponent<Rigidbody2D>(); go.AddComponent<BoxCollider2D>();
            var respawn = go.AddComponent<PlayerRespawnController>(); fuel.Initialize(0, 8); respawn.TickSurvival(1.8f);
            Assert.That(respawn.DarknessTimer, Is.GreaterThan(1f)); CodexSession.Discover("a");
            Assert.That(respawn.DarknessTimer, Is.Zero); respawn.TickSurvival(2f); Assert.That(respawn.State, Is.EqualTo(RespawnState.Alive));
        }

        [Test]
        public void ReconstructedSceneListenersReplayWithoutAnotherReward()
        {
            var fuel = Go("Old fuel").AddComponent<LanternFuel>(); CodexSession.Discover("a"); Object.DestroyImmediate(fuel.gameObject);
            var fresh = Go("Reloaded fuel").AddComponent<LanternFuel>(); Assert.That(fresh.TotalEmbers, Is.EqualTo(3));
            fresh.gameObject.SetActive(false); fresh.gameObject.SetActive(true); Assert.That(fresh.TotalEmbers, Is.EqualTo(3));
            Assert.That(CodexSession.Has("a"), Is.True); Assert.That(((StoredDelivery)CodexSession.Channel.delivery).Count, Is.EqualTo(1));
            Assert.That(CodexSession.Discover("a"), Is.False); Assert.That(CodexSession.Discover("missing"), Is.False);
        }

        [Test]
        public void RewardsClampAtFourAndSessionRestartClearsDiscovery()
        {
            var fuel = Go("Fuel").AddComponent<LanternFuel>(); CodexSession.Discover("a"); CodexSession.Discover("b"); Assert.That(fuel.TotalEmbers, Is.EqualTo(4));
            Assert.That(CodexSession.Decryption, Is.EqualTo(100)); var catalog = CodexSession.Catalog; CodexSession.Begin(catalog); Assert.That(CodexSession.FoundCount, Is.Zero);
        }

        [Test]
        public void CatalogIdentityRejectsUnassignedOrWhitespaceRecordIds()
        {
            var valid = CodexSession.Catalog.Find("a"); var unfinished = New<ArchiveLog>();
            CodexSession.Catalog.Logs = new[] { valid, unfinished };
            foreach (var id in new[] { null, "", "   " })
            {
                unfinished.Id = id;
                Assert.That(CodexSession.Discover(id), Is.False, "Unfinished records must not count as discoveries.");
                Assert.That(CodexSession.FoundCount, Is.Zero); Assert.That(CodexSession.Decryption, Is.Zero);
            }
        }

        [Test]
        public void CatalogIdentityHandlesAnUnassignedLogCollection()
        {
            var catalog = CodexSession.Catalog; catalog.Logs = null;
            Assert.That(catalog.Total, Is.Zero); Assert.That(catalog.Find("a"), Is.Null);
            Assert.That(CodexSession.Discover("a"), Is.False);
        }

        [Test]
        public void CatalogIdentityRejectsANullRestartBeforeClearingCurrentDiscoveries()
        {
            CodexSession.Discover("a"); var catalog = CodexSession.Catalog; var channel = CodexSession.Channel;
            Assert.Throws<System.ArgumentNullException>(() => CodexSession.Begin(null));
            Assert.That(CodexSession.Catalog, Is.SameAs(catalog)); Assert.That(CodexSession.Channel, Is.SameAs(channel));
            Assert.That(CodexSession.Has("a"), Is.True);
        }

        [Test]
        public void PixelBookOpeningStepsThroughSixFramesAndRevealsInkOnlyWhenOpen()
        {
            bool reduced = MenuPreferences.ReducedMotion; MenuPreferences.SetReducedMotion(false);
            try
            {
                CodexSession.Discover("a"); CodexSession.Discover("b");
                var menu = Go("2D Book motion regression").AddComponent<ArchiveCodexMenu>();
                menu.Open("a");

                var tick = typeof(ArchiveCodexMenu).GetMethod("AnimateBook", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var view = menu.GetComponent<ArchiveBookPresentation>();
                var ink = menu.GetComponentsInChildren<CanvasGroup>(true).Single(g => g.name == "Book ink and controls");

                Assert.That(view.CurrentOpeningFrame, Is.EqualTo(0));
                Assert.That(ink.alpha, Is.EqualTo(0f));
                view.CapturePreview(1280, 720, "Library/Readability/book-2d-closed.png");

                tick.Invoke(menu, new object[] { 0.40f });
                Assert.That(view.CurrentOpeningFrame, Is.InRange(2, 4));
                Assert.That(ink.alpha, Is.EqualTo(0f), "Ink must stay hidden while the 2D cover is mid-unfold.");
                view.CapturePreview(1280, 720, "Library/Readability/book-2d-opening.png");

                tick.Invoke(menu, new object[] { 0.60f });
                Assert.That(menu.IsAnimating, Is.False);
                Assert.That(view.CurrentOpeningFrame, Is.EqualTo(PixelArchiveBook.FrameCount - 1));
                Assert.That(ink.alpha, Is.EqualTo(1f));
                Assert.That(view.BookArtImage.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                menu.Close();
            }
            finally { MenuPreferences.SetReducedMotion(reduced); }
        }

        [UnityTest]
        public IEnumerator BookCanCloseDuringOpeningWithoutLeavingGameplayBlocked()
        {
            bool reduced = MenuPreferences.ReducedMotion; MenuPreferences.SetReducedMotion(false);
            try
            {
                CodexSession.Discover("a");
                var menu = Go("2D Cinematic book").AddComponent<ArchiveCodexMenu>();
                menu.Open("a");
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(menu.IsAnimating, Is.True);
                Assert.That(menu.GetComponentsInChildren<Camera>(true), Is.Empty, "Pure 2D pixel book uses no extra cameras.");

                menu.Close();
                Assert.That(menu.IsAnimating, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                yield return null; yield return null;
                Assert.That(GameControls.GameplayAllowed, Is.True);

                menu.Open("a");
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.That(menu.IsAnimating, Is.False);
                Assert.That(menu.GetComponentsInChildren<CanvasGroup>().Any(g => g.name == "Codex reading surface" && g.alpha == 1f && g.interactable), Is.True);
                menu.Close();
            }
            finally { MenuPreferences.SetReducedMotion(reduced); }
        }

        [UnityTest]
        public IEnumerator TerminalDeskStaysAnchoredWhilePromptAnimates()
        {
            var go = Go("Office desk"); go.transform.position = new Vector3(10, 2, 0);
            var terminal = go.AddComponent<DiegeticWhisper>(); terminal.ArchiveLog = CodexSession.Catalog.Find("a");
            go.AddComponent<ArchiveTerminalVisual>(); var start = go.transform.position;
            yield return null; terminal.TickWhisper(0.2f);
            Assert.That(go.transform.position, Is.EqualTo(start));
            Assert.That(go.GetComponentInChildren<Canvas>().transform.localPosition.y, Is.GreaterThan(1f));
        }

        [Test]
        public void ReadingPaginationPreservesWordsParagraphsAndAllText()
        {
            var text = string.Join("\n\n", Enumerable.Repeat("The old archive kept every letter, every promise, and every forgotten name.", 30));
            CodexSession.Catalog.Find("a").Text = text; CodexSession.Discover("a");
            var menu = Go("Pagination regression").AddComponent<ArchiveCodexMenu>(); menu.Open("a");
            var split = typeof(ArchiveCodexMenu).GetMethod("ReadingPages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var chunks = (List<string>)split.Invoke(menu, new object[] { text });
            Assert.That(chunks.Count, Is.GreaterThan(1)); Assert.That(string.Concat(chunks), Is.EqualTo(text));
            var body = menu.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Log text");
            foreach (var chunk in chunks)
            {
                Assert.That(body.GetPreferredValues(chunk, body.rectTransform.rect.width, 0).y, Is.LessThanOrEqualTo(body.rectTransform.rect.height));
                if (chunk != chunks.Last()) Assert.That(char.IsWhiteSpace(chunk.Last()), Is.True, "Page breaks must preserve whole words.");
            }
            menu.Close();
        }

        [UnityTest]
        public IEnumerator ReaderNavigationSkipsDisabledPageActions()
        {
            CodexSession.Discover("a");
            var menu = Go("Reader navigation").AddComponent<ArchiveCodexMenu>();
            menu.Open("a"); yield return new WaitForSecondsRealtime(1.0f);
            var buttons = menu.GetComponentsInChildren<Button>(true).Where(b => !b.name.StartsWith("Bookmark Tab")).ToArray();
            var close = buttons.Single(b => b.name == "Close");
            foreach (var button in buttons.Where(b => b.interactable))
            {
                foreach (var target in new[] { button.navigation.selectOnLeft, button.navigation.selectOnRight, button.navigation.selectOnUp, button.navigation.selectOnDown })
                {
                    if (target != null) Assert.That(target.IsInteractable(), Is.True, button.name + " points to a disabled action");
                }
            }
            Assert.That(close.navigation.selectOnUp, Is.Not.Null);
            menu.Close();
        }

        [UnityTest]
        public IEnumerator PixelBookFitsSupportedScreensAndSeparatesButtonsAndBookmarkTabs()
        {
            CodexSession.Catalog.Find("a").Text = string.Join(" ", Enumerable.Repeat("The office kept its records in silence.", 15));
            CodexSession.Discover("a"); CodexSession.Discover("b");
            var menu = Go("2D Office reader").AddComponent<ArchiveCodexMenu>();
            menu.Open("a"); yield return new WaitForSecondsRealtime(1.0f);

            var view = menu.GetComponent<ArchiveBookPresentation>();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
            {
                view.CapturePreview(size.x, size.y, "Library/Readability/office-codex-" + size.x + ".png");
                foreach (var label in menu.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Log text" || t.name == "Room clue" || t.name == "Log title"))
                {
                    Assert.That(label.GetPreferredValues(label.text, label.rectTransform.rect.width, 0).y,
                        Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2f), label.name);
                }
            }

            Assert.That(menu.BookmarkTabCount, Is.EqualTo(2));
            var pageButtons = menu.GetComponentsInChildren<Button>().Where(b => !b.name.StartsWith("Bookmark Tab")).ToArray();
            Assert.That(pageButtons.Length, Is.EqualTo(5));
            var voice = pageButtons.Single(b => b.name == "Pause voice");
            var replay = pageButtons.Single(b => b.name == "Replay voice");
            Assert.That(voice.navigation.selectOnRight, Is.SameAs(replay));
            Assert.That(replay.navigation.selectOnDown, Is.Not.Null);
            Assert.That(voice.GetComponent<SettingsLightFocus>().UseInkPalette, Is.True);

            // Check no overlapping buttons inside same parent coordinate space
            var allButtons = menu.GetComponentsInChildren<Button>();
            foreach (var first in allButtons)
            {
                foreach (var second in allButtons.Where(b => b != first && b.transform.parent == first.transform.parent))
                {
                    var a = (RectTransform)first.transform;
                    var b = (RectTransform)second.transform;
                    var boundsA = new Rect(a.anchoredPosition - a.sizeDelta * 0.5f, a.sizeDelta);
                    var boundsB = new Rect(b.anchoredPosition - b.sizeDelta * 0.5f, b.sizeDelta);
                    Assert.That(boundsA.Overlaps(boundsB), Is.False, first.name + " overlaps " + second.name);
                }
            }
            Assert.That(allButtons.All(b => b.GetComponent<SettingsLightFocus>() != null), Is.True);
            menu.Close();
        }

        [UnityTest]
        public IEnumerator CodexRebuildsEntriesOnEnableAndRestoresTimeAndInputOnClose()
        {
            CodexSession.Discover("a");
            var menu = Go("Reader").AddComponent<ArchiveCodexMenu>();
            yield return null;
            Assert.That(menu.EntryCount, Is.EqualTo(1));
            menu.Open("a");
            Assert.That(ArchiveCodexMenu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(GameControls.GameplayAllowed, Is.False);
            yield return new WaitForSecondsRealtime(1.0f);

            menu.GetComponent<ArchiveBookPresentation>().CapturePreview(1280, 720, "Library/Readability/codex-reading-720p.png");
            CodexSession.Discover("b");
            menu.Open("b");
            menu.GetComponentsInChildren<Button>().Single(b => b.name == "Next").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.55f);
            Assert.That(menu.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Page counter").text, Does.Contain("PAGE 2 /"));

            menu.Close();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(ArchiveCodexMenu.IsOpen, Is.False);
            menu.gameObject.SetActive(false);
            menu.gameObject.SetActive(true);
            Assert.That(menu.EntryCount, Is.EqualTo(2));
            menu.Open("a");
            Object.DestroyImmediate(menu.gameObject);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PixelBookSpritesSurviveUnloadUnusedAssetsWithoutTurningWhite()
        {
            CodexSession.Discover("a");
            var menu = Go("Unload survival reader").AddComponent<ArchiveCodexMenu>();
            menu.Open("a");
            yield return new WaitForSecondsRealtime(1.0f);

            var view = menu.GetComponent<ArchiveBookPresentation>();
            Assert.That(view.BookArtImage.sprite, Is.Not.Null);
            Assert.That(view.BookArtImage.sprite.texture, Is.Not.Null);

            // Simulate scene transition / memory cleanup while book is closed then reopened
            menu.Close();
            yield return Resources.UnloadUnusedAssets();
            menu.Open("a");
            yield return null;

            Assert.That(view.BookArtImage.sprite, Is.Not.Null, "Book art sprite must never become null (white box) after UnloadUnusedAssets.");
            Assert.That(view.BookArtImage.sprite.texture, Is.Not.Null);
            foreach (var frame in PixelArchiveBook.OpeningFrames)
                Assert.That(frame != null && frame.texture != null, Is.True);
            foreach (var frame in PixelArchiveBook.PageTurnFrames)
                Assert.That(frame != null && frame.texture != null, Is.True);

            menu.Close();
        }
    }
}
