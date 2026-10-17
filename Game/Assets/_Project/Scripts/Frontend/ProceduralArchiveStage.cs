using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Clean, atmospheric 2.5D procedural background stage for The Lighten Road Main Menu.
    /// Renders a deep midnight-indigo ruined archive with seamless SDF gothic arches,
    /// a compound-pendulum swinging wrought-iron lantern casting soft volumetric light shafts,
    /// multi-depth rising embers, and a delicate golden spark stream toward the focused UI item.
    /// </summary>
    public sealed class ProceduralArchiveStage : MonoBehaviour
    {
        private RectTransform stageRoot;
        private RectTransform cameraRig;
        private RectTransform farVaultLayer;
        private RectTransform midArchLayer;
        private RectTransform pendulumPivot;
        private RectTransform lanternHeadRect;
        private RectTransform foregroundFrameLayer;
        private RectTransform wandererSilhouetteRect;

        private Image skyImage;
        private Image farVaultImage;
        private Image midArchImage;
        private Image pillarVeinGlowImage;
        private Image lanternOuterHalo;
        private Image lanternMidHalo;
        private Image lanternFlameCore;
        private Image cursorLanternGlow;
        private RectTransform cursorLanternRect;

        private readonly List<RectTransform> godRays = new List<RectTransform>();
        private readonly List<Image> godRayImages = new List<Image>();

        private struct TetherNode
        {
            public RectTransform Rect;
            public Image Img;
            public float Phase;
            public float BaseSize;
        }
        private readonly List<TetherNode> tetherNodes = new List<TetherNode>();

        private struct EmberMote
        {
            public RectTransform Rect;
            public Image Img;
            public float SeedX;
            public float SeedY;
            public float SpeedY;
            public float DriftAmp;
            public float DriftFreq;
            public float Depth;
            public bool IsLanternSpark;
        }
        private readonly List<EmberMote> embers = new List<EmberMote>();

        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly List<Sprite> generatedSprites = new List<Sprite>();

        private Sprite radialGlowSprite;
        private Sprite lightRaySprite;
        private Sprite lanternCageSprite;
        private Sprite chainLinkSprite;
        private Sprite diamondSparkSprite;

        // Dynamic animation state
        private float time;
        private float surgeIntensity;
        private float pendulumBoost;
        private Vector2 targetCameraOffset;
        private Vector2 currentCameraOffset;
        private Vector2 targetButtonCanvasPos = new Vector2(0f, -20f);
        private Vector2 currentButtonCanvasPos = new Vector2(0f, -20f);
        private bool hasActiveButtonTarget;

        public void BuildStage(RectTransform parentCanvasRect)
        {
            stageRoot = CreateStretchRect("Procedural Archive Stage", parentCanvasRect);
            stageRoot.SetAsFirstSibling();

            radialGlowSprite = CreateRadialGlowSprite(256);
            lightRaySprite = CreateLightRaySprite(128, 384);
            lanternCageSprite = CreateLanternCageSprite(256, 340);
            chainLinkSprite = CreateChainLinkSprite(48, 84);
            diamondSparkSprite = CreateDiamondSparkSprite(64);

            // 1. Deep Obsidian-Indigo Sky & Atmospheric Bounce Gradient
            var skyRect = CreateStretchRect("Midnight Sky Gradient", stageRoot);
            skyImage = skyRect.gameObject.AddComponent<Image>();
            skyImage.sprite = CreateSkyGradientSprite(256, 512);
            skyImage.raycastTarget = false;

            // Oversized Camera Rig for smooth 2.5D parallax & breathing zoom
            cameraRig = CreateStretchRect("Stage Camera Rig", stageRoot);
            cameraRig.localScale = Vector3.one * 1.06f;

            // 2. Far Layer: Seamless Distant Gothic Vaults & Bookshelf Silhouettes
            farVaultLayer = CreateAnchoredRect("Far Gothic Colonnade", cameraRig, new Vector2(2240f, 1200f), new Vector2(0.5f, 0.5f), Vector2.zero);
            farVaultImage = farVaultLayer.gameObject.AddComponent<Image>();
            farVaultImage.sprite = CreateSeamlessGothicVaultSprite(1024, 540);
            farVaultImage.raycastTarget = false;

            // Soft Atmospheric Horizon Mist
            var mistRect = CreateAnchoredRect("Midnight Mist Veil", cameraRig, new Vector2(2400f, 760f), new Vector2(0.5f, 0.28f), Vector2.zero);
            var mistImg = mistRect.gameObject.AddComponent<Image>();
            mistImg.sprite = radialGlowSprite;
            mistImg.color = new Color(0.06f, 0.13f, 0.26f, 0.26f);
            mistImg.raycastTarget = false;

            // 3. Mid Layer: Framing Cathedral Pillars & Subtle Carved Runes
            midArchLayer = CreateAnchoredRect("Mid Archive Pillars", cameraRig, new Vector2(2180f, 1180f), new Vector2(0.5f, 0.5f), Vector2.zero);
            CreateFramingPillarsAndRunes(1024, 540, out Sprite pillarSprite, out Sprite runeGlowSprite);

            midArchImage = midArchLayer.gameObject.AddComponent<Image>();
            midArchImage.sprite = pillarSprite;
            midArchImage.raycastTarget = false;

            var veinRect = CreateStretchRect("Carved Ember Veins Glow", midArchLayer);
            pillarVeinGlowImage = veinRect.gameObject.AddComponent<Image>();
            pillarVeinGlowImage.sprite = runeGlowSprite;
            pillarVeinGlowImage.color = new Color(1f, 0.68f, 0.24f, 0.45f);
            pillarVeinGlowImage.raycastTarget = false;

            // 4. Swinging Pendulum Great Lantern (Upper-Right Focal Point)
            BuildSwingingLantern(cameraRig);

            // 5. Delicate Ember Spark Tether
            BuildEnergyTether(cameraRig);

            // 6. Foreground Layer: Dark Stone Framing Arch & Distant Cloaked Silhouette
            BuildForegroundLayer(cameraRig);

            // 7. Multi-Depth Floating Embers & Rising Lantern Sparks
            BuildEmberField(stageRoot);

            // 8. Soft Center Vignette so UI Typography is 100% Crisp and Readable
            var centerShade = CreateAnchoredRect("Center Typography Contrast Shade", stageRoot, new Vector2(1150f, 920f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var shadeImg = centerShade.gameObject.AddComponent<Image>();
            shadeImg.sprite = radialGlowSprite;
            shadeImg.color = new Color(0.015f, 0.024f, 0.052f, 0.48f);
            shadeImg.raycastTarget = false;

            // 9. Mouse-Following Lantern Spotlight
            cursorLanternRect = CreateAnchoredRect("Mouse Lantern Spotlight", stageRoot, new Vector2(480f, 480f), new Vector2(0.5f, 0.5f), Vector2.zero);
            cursorLanternGlow = cursorLanternRect.gameObject.AddComponent<Image>();
            cursorLanternGlow.sprite = radialGlowSprite;
            cursorLanternGlow.color = new Color(1.0f, 0.68f, 0.24f, 0.09f);
            cursorLanternGlow.raycastTarget = false;
        }

        private void BuildSwingingLantern(RectTransform parent)
        {
            pendulumPivot = CreateAnchoredRect("Pendulum Lantern Pivot", parent, new Vector2(40f, 40f), new Vector2(0.77f, 1.03f), Vector2.zero);
            pendulumPivot.pivot = new Vector2(0.5f, 1f);

            const int chainCount = 6;
            for (int i = 0; i < chainCount; i++)
            {
                var linkRect = CreateAnchoredRect($"ChainLink_{i}", pendulumPivot, new Vector2(16f, 38f), new Vector2(0.5f, 1f), new Vector2(0f, -10f - i * 30f));
                linkRect.pivot = new Vector2(0.5f, 1f);
                var linkImg = linkRect.gameObject.AddComponent<Image>();
                linkImg.sprite = chainLinkSprite;
                linkImg.color = new Color(0.72f, 0.58f, 0.40f, 0.88f);
                linkImg.raycastTarget = false;
            }

            lanternHeadRect = CreateAnchoredRect("Great Archive Lantern", pendulumPivot, new Vector2(164f, 214f), new Vector2(0.5f, 1f), new Vector2(0f, -190f));
            lanternHeadRect.pivot = new Vector2(0.5f, 0.86f);

            // Soft, borderless volumetric light cones
            float[] rayAngles = { -34f, -16f, 0f, 16f, 34f };
            float[] rayWidths = { 260f, 340f, 420f, 340f, 260f };
            for (int i = 0; i < rayAngles.Length; i++)
            {
                var rayRect = CreateAnchoredRect($"VolumetricGodRay_{i}", lanternHeadRect, new Vector2(rayWidths[i], 1080f), new Vector2(0.5f, 0.48f), Vector2.zero);
                rayRect.pivot = new Vector2(0.5f, 1f);
                rayRect.localEulerAngles = new Vector3(0f, 0f, rayAngles[i]);
                var rayImg = rayRect.gameObject.AddComponent<Image>();
                rayImg.sprite = lightRaySprite;
                rayImg.color = new Color(1.0f, 0.72f, 0.28f, 0.055f);
                rayImg.raycastTarget = false;
                godRays.Add(rayRect);
                godRayImages.Add(rayImg);
            }

            var outerHaloRect = CreateAnchoredRect("Pedestal Lantern Outer", lanternHeadRect, new Vector2(920f, 920f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternOuterHalo = outerHaloRect.gameObject.AddComponent<Image>();
            lanternOuterHalo.sprite = radialGlowSprite;
            lanternOuterHalo.color = new Color(0.98f, 0.56f, 0.16f, 0.22f);
            lanternOuterHalo.raycastTarget = false;

            var midHaloRect = CreateAnchoredRect("Pedestal Lantern Core", lanternHeadRect, new Vector2(380f, 380f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternMidHalo = midHaloRect.gameObject.AddComponent<Image>();
            lanternMidHalo.sprite = radialGlowSprite;
            lanternMidHalo.color = new Color(1.0f, 0.78f, 0.34f, 0.44f);
            lanternMidHalo.raycastTarget = false;

            var coreRect = CreateAnchoredRect("Lantern Flame Heart", lanternHeadRect, new Vector2(115f, 145f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternFlameCore = coreRect.gameObject.AddComponent<Image>();
            lanternFlameCore.sprite = radialGlowSprite;
            lanternFlameCore.color = new Color(1.0f, 0.95f, 0.78f, 0.92f);
            lanternFlameCore.raycastTarget = false;

            var cageRect = CreateStretchRect("Wrought Iron Cage", lanternHeadRect);
            var cageImg = cageRect.gameObject.AddComponent<Image>();
            cageImg.sprite = lanternCageSprite;
            cageImg.raycastTarget = false;
        }

        private void BuildEnergyTether(RectTransform parent)
        {
            var tetherContainer = CreateStretchRect("Ember Vein Energy Tether", parent);
            const int nodeCount = 16;
            for (int i = 0; i < nodeCount; i++)
            {
                float norm = i / (float)(nodeCount - 1);
                float size = Mathf.Lerp(11f, 5.5f, norm);
                var nRect = CreateAnchoredRect($"VeinNode_{i:00}", tetherContainer, new Vector2(size, size), new Vector2(0.5f, 0.5f), Vector2.zero);
                var nImg = nRect.gameObject.AddComponent<Image>();
                nImg.sprite = (i % 3 == 0) ? diamondSparkSprite : radialGlowSprite;
                nImg.color = Color.clear;
                nImg.raycastTarget = false;

                tetherNodes.Add(new TetherNode
                {
                    Rect = nRect,
                    Img = nImg,
                    Phase = norm,
                    BaseSize = size
                });
            }
        }

        private void BuildForegroundLayer(RectTransform parent)
        {
            foregroundFrameLayer = CreateAnchoredRect("Foreground Ledge & Wanderer", parent, new Vector2(2280f, 1180f), new Vector2(0.5f, 0.5f), Vector2.zero);

            var ledgeImg = foregroundFrameLayer.gameObject.AddComponent<Image>();
            ledgeImg.sprite = CreateForegroundLedgeSprite(1024, 540);
            ledgeImg.raycastTarget = false;

            // Keep the named object for test compatibility & subtle left-ledge lantern brazier
            wandererSilhouetteRect = CreateAnchoredRect("Cloaked Wanderer Silhouette", foregroundFrameLayer, new Vector2(180f, 220f), new Vector2(0.16f, 0.14f), Vector2.zero);
            wandererSilhouetteRect.pivot = new Vector2(0.5f, 0.1f);
            var brazierGlow = wandererSilhouetteRect.gameObject.AddComponent<Image>();
            brazierGlow.sprite = radialGlowSprite;
            brazierGlow.color = new Color(1.0f, 0.68f, 0.24f, 0.22f);
            brazierGlow.raycastTarget = false;
        }

        private void BuildEmberField(RectTransform parent)
        {
            var layer = CreateStretchRect("Multi-Depth Ember Field", parent);
            for (int i = 0; i < 68; i++)
            {
                bool isLanternSpark = i < 14;
                float depth = isLanternSpark ? 0.95f : ((i % 4 == 0) ? 1f : ((i % 2 == 0) ? 0.55f : 0.22f));
                float size = depth > 0.8f ? 8.5f : (depth > 0.4f ? 5.2f : 2.8f);
                var rect = CreateAnchoredRect($"EmberMote_{i:00}", layer, new Vector2(size, size), Vector2.zero, Vector2.zero);
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = (depth > 0.75f && i % 2 == 0) ? diamondSparkSprite : radialGlowSprite;
                img.raycastTarget = false;

                embers.Add(new EmberMote
                {
                    Rect = rect,
                    Img = img,
                    SeedX = isLanternSpark ? (0.74f + (i % 5) * 0.015f) : Mathf.Repeat(i * 0.6180339f, 1f),
                    SeedY = Mathf.Repeat(i * 0.381966f, 1f),
                    SpeedY = isLanternSpark ? (0.042f + (i % 4) * 0.007f) : (Mathf.Lerp(0.012f, 0.034f, depth) + (i % 4) * 0.003f),
                    DriftAmp = isLanternSpark ? 0.025f : Mathf.Lerp(0.008f, 0.024f, depth),
                    DriftFreq = 0.42f + (i % 7) * 0.10f,
                    Depth = depth,
                    IsLanternSpark = isLanternSpark
                });
            }
        }

        public void TriggerInteractionSurge(Vector2 buttonCanvasPosition, Vector2 cameraPanTarget, float boost = 1f)
        {
            surgeIntensity = Mathf.Clamp(surgeIntensity + 0.85f * boost, 0f, 1.6f);
            pendulumBoost = Mathf.Clamp(pendulumBoost + 3.2f * boost, -7f, 7f);
            targetButtonCanvasPos = buttonCanvasPosition;
            targetCameraOffset = cameraPanTarget;
            hasActiveButtonTarget = true;
        }

        public void TickStage(float dt, float introIgnition, Vector2 normalizedMouse, Vector2 mouseCanvasPosition, bool reducedMotion)
        {
            time += dt;
            surgeIntensity = Mathf.MoveTowards(surgeIntensity, 0f, dt * 1.4f);
            pendulumBoost = Mathf.MoveTowards(pendulumBoost, 0f, dt * 2.2f);

            Vector2 desiredCam = reducedMotion
                ? Vector2.zero
                : targetCameraOffset + new Vector2(-normalizedMouse.x * 24f, -normalizedMouse.y * 14f);
            currentCameraOffset = Vector2.Lerp(currentCameraOffset, desiredCam, 1f - Mathf.Exp(-dt * 4.5f));
            currentButtonCanvasPos = Vector2.Lerp(currentButtonCanvasPos, targetButtonCanvasPos, 1f - Mathf.Exp(-dt * 11f));

            if (cameraRig != null && !reducedMotion)
            {
                float rigZoom = 1.06f + Mathf.Sin(time * 0.35f) * 0.01f + surgeIntensity * 0.012f;
                cameraRig.localScale = new Vector3(rigZoom, rigZoom, 1f);
            }

            if (farVaultLayer != null) farVaultLayer.anchoredPosition = currentCameraOffset * 0.40f;
            if (midArchLayer != null) midArchLayer.anchoredPosition = currentCameraOffset * 0.82f;
            if (foregroundFrameLayer != null) foregroundFrameLayer.anchoredPosition = currentCameraOffset * 1.32f;

            // Compound Pendulum Swinging Lantern
            float swingAngle = reducedMotion
                ? 0f
                : Mathf.Sin(time * 1.28f) * (4.2f + pendulumBoost) + Mathf.Cos(time * 0.64f) * 1.4f - normalizedMouse.x * 2.5f;

            if (pendulumPivot != null)
            {
                pendulumPivot.anchoredPosition = currentCameraOffset * 0.92f;
                pendulumPivot.localEulerAngles = new Vector3(0f, 0f, swingAngle);
            }

            if (lanternHeadRect != null && !reducedMotion)
            {
                float headLag = Mathf.Cos(time * 1.28f) * (1.8f + pendulumBoost * 0.35f);
                lanternHeadRect.localEulerAngles = new Vector3(0f, 0f, headLag);
            }

            float flicker = reducedMotion
                ? 0.92f
                : 0.88f + Mathf.Sin(time * 2.8f) * 0.07f + Mathf.Sin(time * 7.4f) * 0.04f + Mathf.Cos(time * 14.7f) * 0.02f;
            float totalGlow = Mathf.Clamp(flicker + surgeIntensity * 0.48f, 0f, 1.65f) * introIgnition;

            if (lanternOuterHalo != null)
            {
                lanternOuterHalo.color = new Color(0.98f, 0.56f, 0.16f, Mathf.Clamp01(0.24f * totalGlow));
                float s = 1f + surgeIntensity * 0.18f + (reducedMotion ? 0f : Mathf.Sin(time * 1.8f) * 0.04f);
                lanternOuterHalo.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (lanternMidHalo != null)
            {
                lanternMidHalo.color = new Color(1.0f, 0.78f, 0.34f, Mathf.Clamp01(0.46f * totalGlow));
            }
            if (lanternFlameCore != null)
            {
                lanternFlameCore.color = new Color(1.0f, 0.96f, 0.80f, Mathf.Clamp01(0.95f * totalGlow));
                float coreScale = 1f + surgeIntensity * 0.24f + (reducedMotion ? 0f : Mathf.Sin(time * 5.4f) * 0.07f);
                lanternFlameCore.rectTransform.localScale = new Vector3(coreScale, coreScale, 1f);
            }

            for (int i = 0; i < godRayImages.Count; i++)
            {
                float rayWave = reducedMotion ? 1f : (0.75f + 0.25f * Mathf.Sin(time * 1.4f + i * 1.1f));
                float alpha = (0.055f + surgeIntensity * 0.045f) * rayWave * introIgnition;
                godRayImages[i].color = new Color(1.0f, 0.74f, 0.30f, Mathf.Clamp01(alpha));
            }

            if (pillarVeinGlowImage != null)
            {
                float veinPulse = reducedMotion ? 0.45f : (0.38f + 0.18f * Mathf.Sin(time * 1.9f));
                float veinAlpha = Mathf.Clamp01((veinPulse + surgeIntensity * 0.38f) * introIgnition);
                pillarVeinGlowImage.color = new Color(1.0f, 0.70f, 0.24f, veinAlpha);
            }

            UpdateEnergyTether(introIgnition, reducedMotion);

            float speedMult = 1f + surgeIntensity * 0.85f;
            for (int i = 0; i < embers.Count; i++)
            {
                var e = embers[i];
                float y = Mathf.Repeat(e.SeedY + (reducedMotion ? 0f : time * e.SpeedY * speedMult), 1f);
                float x = e.IsLanternSpark
                    ? Mathf.Clamp01(e.SeedX + (reducedMotion ? 0f : Mathf.Sin(time * e.DriftFreq + i) * e.DriftAmp * y))
                    : Mathf.Repeat(e.SeedX + (reducedMotion ? 0f : Mathf.Sin(time * e.DriftFreq + i) * e.DriftAmp), 1f);

                e.Rect.anchorMin = e.Rect.anchorMax = new Vector2(x, y);
                e.Rect.anchoredPosition = currentCameraOffset * e.Depth;

                float life = Mathf.Sin(y * Mathf.PI);
                if (e.Depth > 0.5f)
                {
                    float twinkle = 0.68f + 0.32f * Mathf.Sin(time * 5.5f + i * 1.7f);
                    e.Img.color = new Color(1.0f, 0.74f, 0.28f, Mathf.Clamp01((0.52f + surgeIntensity * 0.28f) * life * twinkle * introIgnition));
                }
                else
                {
                    e.Img.color = new Color(0.52f, 0.66f, 0.86f, Mathf.Clamp01(0.16f * life * introIgnition));
                }
            }

            if (cursorLanternRect != null && cursorLanternGlow != null)
            {
                cursorLanternRect.anchoredPosition = Vector2.Lerp(cursorLanternRect.anchoredPosition, mouseCanvasPosition, 1f - Mathf.Exp(-dt * 10f));
                cursorLanternGlow.color = new Color(1.0f, 0.68f, 0.25f, (0.09f + surgeIntensity * 0.05f) * introIgnition);
            }
        }

        private void UpdateEnergyTether(float introIgnition, bool reducedMotion)
        {
            if (!hasActiveButtonTarget || tetherNodes.Count == 0)
            {
                return;
            }

            float swingRad = pendulumPivot != null ? pendulumPivot.localEulerAngles.z * Mathf.Deg2Rad : 0f;
            Vector2 startPos = new Vector2(510f + Mathf.Sin(swingRad) * 240f, 275f);
            Vector2 endPos = currentButtonCanvasPos + new Vector2(180f, 0f);
            Vector2 controlPos = (startPos + endPos) * 0.5f + new Vector2(-60f, 95f);

            float streamVisibility = Mathf.Clamp01((0.18f + surgeIntensity * 0.55f) * introIgnition);

            for (int i = 0; i < tetherNodes.Count; i++)
            {
                var node = tetherNodes[i];
                float flowT = reducedMotion
                    ? node.Phase
                    : Mathf.Repeat(node.Phase + time * (0.42f + surgeIntensity * 0.32f), 1f);

                float inv = 1f - flowT;
                Vector2 pos = inv * inv * startPos + 2f * inv * flowT * controlPos + flowT * flowT * endPos;
                float wave = reducedMotion ? 0f : Mathf.Sin(flowT * 9f - time * 4.5f) * 6f * Mathf.Sin(flowT * Mathf.PI);
                pos.y += wave;

                node.Rect.anchoredPosition = pos;
                float edgeFade = Mathf.Sin(flowT * Mathf.PI);
                float alpha = streamVisibility * edgeFade;
                node.Img.color = new Color(1.0f, 0.78f, 0.32f, alpha);
                float pulseScale = 0.7f + 0.45f * edgeFade + surgeIntensity * 0.22f;
                node.Rect.localScale = new Vector3(pulseScale, pulseScale, 1f);
            }
        }

        private void OnDestroy()
        {
            foreach (var s in generatedSprites)
            {
                if (s != null) Destroy(s);
            }
            foreach (var t in generatedTextures)
            {
                if (t != null) Destroy(t);
            }
        }

        // =====================================================================
        // SEAMLESS SDF ART & TEXTURE GENERATORS
        // =====================================================================

        private Sprite RegisterSprite(Texture2D tex)
        {
            tex.Apply();
            generatedTextures.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100f);
            generatedSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateSkyGradientSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color topVault = new Color(0.012f, 0.020f, 0.048f, 1f);
            Color midArchive = new Color(0.028f, 0.052f, 0.108f, 1f);
            Color lowerAbyss = new Color(0.010f, 0.016f, 0.036f, 1f);
            Color warmBounce = new Color(0.14f, 0.08f, 0.035f, 1f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                Color vertical = ny > 0.45f
                    ? Color.Lerp(midArchive, topVault, (ny - 0.45f) / 0.55f)
                    : Color.Lerp(lowerAbyss, midArchive, ny / 0.45f);

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float lanternDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.76f, 0.68f));
                    float bounce = Mathf.Pow(Mathf.Clamp01(1f - lanternDist * 1.5f), 2.4f) * 0.42f;
                    tex.SetPixel(x, y, Color.Lerp(vertical, warmBounce, bounce));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateSeamlessGothicVaultSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color vaultDark = new Color(0.016f, 0.026f, 0.058f, 0.92f);
            Color warmArchRim = new Color(0.92f, 0.58f, 0.20f, 0.28f);

            const int archCount = 3;
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float cell = nx * archCount;
                    float localX = (cell - Mathf.Floor(cell)) * 2f - 1f;
                    float absX = Mathf.Abs(localX);

                    // True SDF distance to the pointed Gothic Arch opening (zero internal seam lines!)
                    float archCurveY = 0.80f - 0.44f * (absX * absX + 0.25f * absX);
                    float distToSideWall = 0.78f - absX;
                    float distToRoof = archCurveY - ny;
                    float openingSignedDist = Mathf.Min(distToSideWall, distToRoof);

                    float edgeAlpha = Mathf.Clamp01((-openingSignedDist + 0.005f) / 0.010f);
                    edgeAlpha = edgeAlpha * edgeAlpha * (3f - 2f * edgeAlpha);

                    if (edgeAlpha > 0.001f)
                    {
                        float boundaryProximity = Mathf.Clamp01(1f - Mathf.Max(0f, -openingSignedDist) * 38f);
                        float lanternWeight = Mathf.Clamp01(1f - Mathf.Abs(nx - 0.75f) * 1.9f);
                        Color c = Color.Lerp(vaultDark, warmArchRim, boundaryProximity * lanternWeight * 0.5f);
                        c.a *= edgeAlpha;
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            return RegisterSprite(tex);
        }

        private void CreateFramingPillarsAndRunes(int w, int h, out Sprite pillarSprite, out Sprite runeSprite)
        {
            var pillarTex = NewTexture(w, h);
            var runeTex = NewTexture(w, h);

            Color pillarCol = new Color(0.010f, 0.016f, 0.036f, 0.98f);
            Color goldRim = new Color(0.96f, 0.64f, 0.22f, 0.65f);

            // Only outer framing columns (leaves the entire center stage open and uncluttered!)
            float[] pillarCenters = { 0.05f, 0.95f };
            float[] pillarWidths = { 0.055f, 0.055f };

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    Color pOut = Color.clear;
                    Color rOut = Color.clear;

                    for (int i = 0; i < pillarCenters.Length; i++)
                    {
                        float dx = nx - pillarCenters[i];
                        float pw = pillarWidths[i];
                        if (ny > 0.82f) pw += (ny - 0.82f) * 0.16f;
                        if (ny < 0.12f) pw += (0.12f - ny) * 0.12f;

                        float signedDist = Mathf.Abs(dx) - pw;
                        float edgeAlpha = Mathf.Clamp01((-signedDist + 0.0025f) / 0.005f);
                        if (edgeAlpha > 0.001f)
                        {
                            float innerSide = (pillarCenters[i] < 0.5f) ? (dx / pw) : (-dx / pw);
                            float rim = Mathf.Pow(Mathf.Clamp01((innerSide + 1f) * 0.5f), 6f) * 0.45f;
                            pOut = Color.Lerp(pillarCol, goldRim, rim);
                            pOut.a *= edgeAlpha;

                            // Subtle vertical architectural fluting & small diamond rune nodes
                            bool verticalFlute = Mathf.Abs(dx) < 0.0022f && ny > 0.16f && ny < 0.80f;
                            float nodeRepeat = Mathf.Abs(Mathf.Repeat(ny * 7f, 1f) - 0.5f);
                            bool runeDiamond = (Mathf.Abs(dx) * 90f + nodeRepeat * 12f) < 0.32f && ny > 0.18f && ny < 0.78f;

                            if (verticalFlute || runeDiamond)
                            {
                                rOut = new Color(1f, 1f, 1f, (runeDiamond ? 0.85f : 0.35f) * Mathf.Sin(ny * Mathf.PI) * edgeAlpha);
                            }
                            break;
                        }
                    }

                    pillarTex.SetPixel(x, y, pOut);
                    runeTex.SetPixel(x, y, rOut);
                }
            }

            pillarSprite = RegisterSprite(pillarTex);
            runeSprite = RegisterSprite(runeTex);
        }

        private Sprite CreateLanternCageSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color iron = new Color(0.05f, 0.045f, 0.065f, 0.98f);
            Color brassRim = new Color(0.95f, 0.68f, 0.28f, 0.95f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float ax = Mathf.Abs(nx);
                    bool solid = false;
                    bool rim = false;

                    float ringDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0f, 0.90f));
                    if (ringDist > 0.045f && ringDist < 0.088f) solid = true;

                    if (ny >= 0.68f && ny <= 0.86f)
                    {
                        float roofWidth = Mathf.Lerp(0.54f, 0.04f, (ny - 0.68f) / 0.18f);
                        if (ax <= roofWidth)
                        {
                            solid = true;
                            if (Mathf.Abs(ax - roofWidth) < 0.028f || ny < 0.705f) rim = true;
                        }
                    }

                    if (ny >= 0.26f && ny < 0.68f)
                    {
                        float cageOuter = Mathf.Lerp(0.34f, 0.46f, (ny - 0.26f) / 0.42f);
                        if (Mathf.Abs(ax - cageOuter) < 0.034f || ax < 0.024f)
                        {
                            solid = true;
                            rim = true;
                        }
                        if (Mathf.Abs(ny - 0.47f) < 0.016f && ax <= cageOuter)
                        {
                            solid = true;
                            rim = true;
                        }
                    }

                    if (ny >= 0.04f && ny < 0.26f)
                    {
                        float baseWidth = Mathf.Lerp(0.015f, 0.38f, Mathf.Pow((ny - 0.04f) / 0.22f, 0.65f));
                        if (ax <= baseWidth)
                        {
                            solid = true;
                            if (ny > 0.23f) rim = true;
                        }
                    }

                    tex.SetPixel(x, y, solid ? (rim ? Color.Lerp(iron, brassRim, 0.55f) : iron) : Color.clear);
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateChainLinkSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color iron = new Color(0.12f, 0.10f, 0.14f, 0.95f);
            Color highlight = new Color(0.85f, 0.62f, 0.28f, 0.85f);

            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx * 1.4f + ny * ny * 0.7f);
                    if (d >= 0.46f && d <= 0.80f)
                    {
                        tex.SetPixel(x, y, nx > 0.1f ? Color.Lerp(iron, highlight, 0.45f) : iron);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateForegroundLedgeSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color darkStone = new Color(0.008f, 0.012f, 0.026f, 1f);
            Color amberEdge = new Color(0.96f, 0.64f, 0.22f, 0.55f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float floorH = 0.035f + Mathf.Pow(Mathf.Abs(nx - 0.5f) * 2f, 2.6f) * 0.08f + Mathf.Sin(nx * 18f) * 0.004f;

                    if (ny <= floorH)
                    {
                        float edge = Mathf.Clamp01(1f - (floorH - ny) * 65f);
                        tex.SetPixel(x, y, Color.Lerp(darkStone, amberEdge, edge * 0.35f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateRadialGlowSprite(int size)
        {
            var tex = NewTexture(size, size);
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxR = (size - 4) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateLightRaySprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                // Strictly zero at top & bottom edges so no rectangle border can ever show
                float halfWidth = Mathf.Lerp(0.42f, 0.08f, ny);
                float verticalFade = Mathf.Pow(Mathf.Sin(ny * Mathf.PI), 1.6f) * Mathf.Pow(ny, 0.8f);

                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs((x / (float)(w - 1)) - 0.5f);
                    float horizFade = Mathf.Pow(Mathf.Clamp01(1f - nx / halfWidth), 2.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, verticalFade * horizFade));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateDiamondSparkSprite(int size)
        {
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            {
                float ny = Mathf.Abs((y / (float)(size - 1)) * 2f - 1f);
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs((x / (float)(size - 1)) * 2f - 1f);
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float edgeMask = Mathf.Clamp01(1f - d);
                    float star = (Mathf.Clamp01(1f - (nx * 0.9f + ny * 0.9f)) + Mathf.Clamp01(1f - Mathf.Min(nx, ny) * 6f) * edgeMask) * edgeMask;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(star)));
                }
            }
            return RegisterSprite(tex);
        }

        public Sprite CreateOrnateFiligreeSprite(int w = 512, int h = 64)
        {
            var tex = NewTexture(w, h);
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float ax = Mathf.Abs(nx);
                    float ay = Mathf.Abs(ny);

                    float lineThickness = Mathf.Lerp(0.07f, 0.008f, ax);
                    float mainLine = Mathf.Clamp01(1f - ay / lineThickness) * Mathf.Clamp01(1f - ax * 1.05f);
                    float centerDiamond = Mathf.Clamp01(1f - (ax * 16f + ay * 2.0f));

                    float alpha = Mathf.Clamp01(Mathf.Max(mainLine, centerDiamond));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            return RegisterSprite(tex);
        }

        private static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private static RectTransform CreateStretchRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateAnchoredRect(string name, RectTransform parent, Vector2 size, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }
    }
}
