using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Codex;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class ArchiveBookInteractionTests
    {
        private readonly List<Object> owned = new List<Object>();
        private ArchiveCodexMenu menu;
        private ArchiveBookPresentation view;
        private bool reduced;

        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); owned.Add(a); return a; }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            reduced = MenuPreferences.ReducedMotion;
            MenuPreferences.SetReducedMotion(false);

            var first = Asset<ArchiveLog>();
            first.Id = "long-record";
            first.Title = "A Keeper's Journal";
            first.Location = "LOWER ARCHIVE";
            first.RoomClue = "Keep a spare ember before entering the dark corridor.";
            first.Text = string.Join("\n\n", Enumerable.Repeat("The archive kept every forgotten name. We left a lamp burning beside the ledger.", 24));

            var second = Asset<ArchiveLog>();
            second.Id = "second-record";
            second.Title = "The Flooded Gallery";
            second.Location = "UPPER HALL";
            second.RoomClue = "Drained water is safe to cross.";
            second.Text = "A short second field entry discovered in the upper hallway.";

            var catalog = Asset<ArchiveCatalog>();
            catalog.Logs = new[] { first, second };
            catalog.Channel = Asset<SocialChannel>();
            catalog.Channel.delivery = Asset<StoredDelivery>();
            catalog.Channel.membership = Asset<AmbientMembership>();

            CodexSession.Begin(catalog);
            CodexSession.Discover(first.Id);
            CodexSession.Discover(second.Id);

            var host = new GameObject("2D Pixel Archive Test Host");
            owned.Add(host);
            menu = host.AddComponent<ArchiveCodexMenu>();
            view = host.GetComponent<ArchiveBookPresentation>();
            menu.Open(first.Id);

            float readyBy = Time.realtimeSinceStartup + 5f;
            do { yield return null; } while (menu.IsAnimating && Time.realtimeSinceStartup < readyBy);

            Assert.That(ArchiveCodexMenu.IsOpen, Is.True);
            Assert.That(menu.IsAnimating, Is.False);
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
            CodexSession.End();
            MenuPreferences.SetReducedMotion(reduced);
            Time.timeScale = 1f;
        }

        private string Counter() => menu.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Page counter").text;
        private string Title() => menu.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Log title").text;

        [Test]
        public void FrameByFrameTurnCommitsAtMidpointAndFlipsDirectionForPrevious()
        {
            string initial = Counter();
            Assert.That(initial, Does.Contain("PAGE 1 /"));

            view.Turn.Automatic(1);
            Assert.That(view.Turn.IsBusy, Is.True);
            Assert.That(view.PageTurnOverlayImage.gameObject.activeSelf, Is.True);
            Assert.That(view.PageTurnOverlayImage.rectTransform.localScale.x, Is.EqualTo(1f));

            // Before midpoint (t < 0.5), page counter stays on page 1 while the 2D sheet sweeps across
            view.Turn.Tick(ArchiveBookPageTurn.TurnDuration * 0.35f);
            Assert.That(Counter(), Is.EqualTo(initial));
            view.CapturePreview(1280, 720, "Library/Readability/book-2d-turning.png");

            // Crossing midpoint (t >= 0.5) commits page 2 underneath the sweeping sheet
            view.Turn.Tick(ArchiveBookPageTurn.TurnDuration * 0.25f);
            Assert.That(Counter(), Does.Contain("PAGE 2 /"));
            Assert.That(view.Turn.IsBusy, Is.True);

            // Finishing hides the 2D page-turn overlay cleanly
            view.Turn.Tick(ArchiveBookPageTurn.TurnDuration * 0.5f);
            Assert.That(view.Turn.IsBusy, Is.False);
            Assert.That(view.PageTurnOverlayImage.gameObject.activeSelf, Is.False);

            // Turning backwards mirrors the 2D turn overlay and returns to page 1
            view.Turn.Automatic(-1);
            Assert.That(view.PageTurnOverlayImage.rectTransform.localScale.x, Is.EqualTo(-1f));
            view.Turn.Tick(ArchiveBookPageTurn.TurnDuration + 0.05f);
            Assert.That(view.Turn.IsBusy, Is.False);
            Assert.That(Counter(), Does.Contain("PAGE 1 /"));
        }

        [Test]
        public void BookmarkTabsSwitchRecordsDirectlyAndHighlightActiveTab()
        {
            Assert.That(menu.BookmarkTabCount, Is.EqualTo(2));
            Assert.That(menu.SelectedEntryIndex, Is.EqualTo(0));
            Assert.That(Title(), Is.EqualTo("A Keeper's Journal"));

            var tab1 = menu.GetComponentsInChildren<Button>(true).Single(b => b.name == "Bookmark Tab 1");
            tab1.onClick.Invoke();
            Assert.That(view.Turn.IsBusy, Is.True);
            Assert.That(view.Turn.Direction, Is.EqualTo(1));

            view.Turn.Tick(ArchiveBookPageTurn.TurnDuration + 0.05f);
            Assert.That(view.Turn.IsBusy, Is.False);
            Assert.That(menu.SelectedEntryIndex, Is.EqualTo(1));
            Assert.That(Title(), Is.EqualTo("The Flooded Gallery"));
            Assert.That(((RectTransform)tab1.transform).anchoredPosition.x, Is.GreaterThan(8f));

            // Clicking tab 0 flips backwards cleanly to the first record
            var tab0 = menu.GetComponentsInChildren<Button>(true).Single(b => b.name == "Bookmark Tab 0");
            tab0.onClick.Invoke();
            Assert.That(view.Turn.Direction, Is.EqualTo(-1));
            view.Turn.Tick(ArchiveBookPageTurn.TurnDuration + 0.05f);
            Assert.That(menu.SelectedEntryIndex, Is.EqualTo(0));
            Assert.That(Title(), Is.EqualTo("A Keeper's Journal"));
        }

        [Test]
        public void ClosingMidTurnCancelsWithoutAdvancingPage()
        {
            string before = Counter();
            view.Turn.Automatic(1);
            view.Turn.Tick(ArchiveBookPageTurn.TurnDuration * 0.25f);
            Assert.That(view.Turn.IsBusy, Is.True);

            menu.Close();
            Assert.That(view.Turn.IsBusy, Is.False);
            Assert.That(view.PageTurnOverlayImage.gameObject.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Counter(), Is.EqualTo(before));
        }

        [Test]
        public void ReducedMotionSkipsOpeningAndPageTurnImmediately()
        {
            MenuPreferences.SetReducedMotion(true);
            menu.Close();
            menu.Open("long-record");
            Assert.That(menu.IsAnimating, Is.False);
            Assert.That(view.CurrentOpeningFrame, Is.EqualTo(PixelArchiveBook.FrameCount - 1));

            view.Turn.Automatic(1);
            Assert.That(view.Turn.IsBusy, Is.False);
            Assert.That(Counter(), Does.Contain("PAGE 2 /"));
        }

        [Test]
        public void ReenablingReaderRestoresTurnCallbacks()
        {
            menu.enabled = false;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ArchiveBookPageTurn).GetField("canTurn", flags).SetValue(view.Turn, null);
            typeof(ArchiveBookPageTurn).GetField("commitDirection", flags).SetValue(view.Turn, null);

            menu.enabled = true;
            menu.Open("long-record");
            typeof(ArchiveCodexMenu).GetMethod("AnimateBook", flags).Invoke(menu, new object[] { ArchiveCodexMenu.OpeningDuration + 0.1f });

            view.Turn.Automatic(1);
            Assert.That(view.Turn.IsBusy, Is.True);
            view.Turn.Cancel();
            menu.Close();
        }

        [Test]
        public void AllPixelBookSpritesUseCrispPointFiltering()
        {
            foreach (var sprite in PixelArchiveBook.OpeningFrames)
            {
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), sprite.name);
            }
            foreach (var sprite in PixelArchiveBook.PageTurnFrames)
            {
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), sprite.name);
            }
            Assert.That(PixelArchiveBook.BookmarkTab.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(PixelArchiveBook.PixelButton.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(PixelArchiveBook.LanternEmblem.texture.filterMode, Is.EqualTo(FilterMode.Point));
        }
    }
}
