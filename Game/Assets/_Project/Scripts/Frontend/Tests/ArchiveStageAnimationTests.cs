using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class ArchiveStageAnimationTests
    {
        private GameObject host;
        private ProceduralArchiveStage stage;
        private Image hero;
        private RectTransform lamp;
        private PlayerSpriteSequence idle;

        [SetUp] public void Setup()
        {
            host = new GameObject("Archive art test", typeof(RectTransform), typeof(Canvas));
            var rect = host.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1920, 1080);
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            stage = host.AddComponent<ProceduralArchiveStage>();
#if UNITY_EDITOR
            // The existing scene used this legacy override. The runtime catalog must supersede it.
            var old = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Character_Light_Stand.png");
            stage.SetArtOverrides(old, null, null);
#endif
            stage.BuildStage(rect);
            hero = host.GetComponentsInChildren<Image>().Single(i => i.name == "Cloaked Wanderer Silhouette");
            lamp = host.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Great Archive Lantern");
            idle = Resources.Load<PlayerAnimationCatalog>(PlayerAnimationCatalog.ResourcePath).Find(LightAnimation.Idle);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Object.Destroy(host); yield return null; yield return null;
        }
        [Test] public void MenuUsesTheDeliveredIdleAndFlameRegistrationInsteadOfLegacyArt()
        {
            Assert.That(hero.sprite, Is.EqualTo(idle.Frames[0]));
            Assert.That(hero.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(hero.raycastTarget, Is.False);
            var size = hero.rectTransform.rect.size;
            Assert.That(size.x / hero.sprite.rect.width, Is.EqualTo(6).Within(.001f));
            Assert.That(size.x, Is.EqualTo(size.y));
            CheckLamp(0);
            stage.TickStage(.23f, 1, Vector2.zero, Vector2.zero, false);
            Assert.That(hero.sprite, Is.EqualTo(idle.Frames[2]));
            CheckLamp(2);
            Assert.That(hero.rectTransform.localScale, Is.EqualTo(Vector3.one), "Do not stretch the authored pixel animation.");
        }
        [Test] public void ReducedMotionFreezesTheCurrentFrameAndNormalPlaybackResumesWithoutDrift()
        {
            stage.TickStage(.23f, 1, Vector2.zero, Vector2.zero, false);
            var position = hero.rectTransform.anchoredPosition;
            var anchor = lamp.anchorMin;
            for (int i = 0; i < 5; i++) stage.TickStage(1, 1, Vector2.one, Vector2.one, true);
            Assert.That(hero.sprite, Is.EqualTo(idle.Frames[2]));
            Assert.That(lamp.anchorMin, Is.EqualTo(anchor));
            Assert.That(hero.rectTransform.anchoredPosition, Is.EqualTo(position));
            stage.TickStage(.1f, 1, Vector2.zero, Vector2.zero, false);
            Assert.That(hero.sprite, Is.EqualTo(idle.Frames[3]));
            stage.TickStage(10, 1, Vector2.zero, Vector2.zero, false);
            Assert.That(hero.sprite, Is.EqualTo(idle.Frames[3]));
            CheckLamp(3);
        }
        private void CheckLamp(int frame)
        {
            var sprite = idle.Frames[frame];
            var pixel = idle.LanternOffsets[frame] * sprite.pixelsPerUnit + sprite.pivot;
            Assert.That(lamp.anchorMin.x, Is.EqualTo(pixel.x / sprite.rect.width).Within(.0001f));
            Assert.That(lamp.anchorMin.y, Is.EqualTo(pixel.y / sprite.rect.height).Within(.0001f));
            Assert.That(lamp.anchorMax, Is.EqualTo(lamp.anchorMin));
            Assert.That(lamp.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
    }
}
