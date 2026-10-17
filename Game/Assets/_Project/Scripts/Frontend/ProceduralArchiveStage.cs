using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Pure-code procedural 2.5D animated background stage for The Lighten Road Main Menu.
    /// Features counter-rotating sacred astrolabe rings, parallax gothic colonnades,
    /// glowing Ember-Vein pillars, a compound-pendulum swinging wrought-iron lantern
    /// with volumetric god-rays, a breathing cloaked wanderer on a foreground cliff,
    /// interactive Bezier energy streams, and expanding golden light ripples.
    /// </summary>
    public sealed class ProceduralArchiveStage : MonoBehaviour
    {
        private RectTransform stageRoot;
        private RectTransform cameraRig;
        private RectTransform farVaultLayer;
        private RectTransform midPillarLayer;
        private RectTransform outerAstrolabeRect;
        private RectTransform innerAstrolabeRect;
        private RectTransform pendulumPivot;
        private RectTransform lanternHeadRect;
        private RectTransform foregroundLedgeLayer;
        private RectTransform wandererBodyRect;
        private RectTransform wandererLanternRect;
        private RectTransform rippleRect;

        private Image skyImage;
        private Image farVaultImage;
        private Image midPillarImage;
        private Image pillarVeinGlowImage;
        private Image outerAstrolabeImage;
        private Image innerAstrolabeImage;
        private Image lanternOuterHalo;
        private Image lanternMidHalo;
        private Image lanternFlameCore;
        private Image wandererHalo;
        private Image wandererFlame;
        private Image rippleImage;
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
        private Sprite astrolabeRingSprite;

        // Dynamic animation state
        private float time;
        private float astrolabeAngle;
        private float surgeIntensity;
        private float pendulumBoost;
        private float rippleProgress = 1f;
        private Vector2 rippleCenter;
        private Vector2 targetCameraOffset;
        private Vector2 currentCameraOffset;
        private Vector2 targetButtonCanvasPos = new Vector2(0f, -40f);
        private Vector2 currentButtonCanvasPos = new Vector2(0f, -40f);
        private bool hasActiveButtonTarget;

        public void BuildStage(RectTransform parentCanvasRect)
        {
            stageRoot = CreateStretchRect("Procedural Archive Stage", parentCanvasRect);
            stageRoot.SetAsFirstSibling();

            radialGlowSprite = CreateRadialGlowSprite(256);
            lightRaySprite = CreateLightRaySprite(128, 384);
            lanternCageSprite = CreateLanternCageSprite(256, 320);
            chainLinkSprite = CreateChainLinkSprite(48, 84);
            diamondSparkSprite = CreateDiamondSparkSprite(64);
            astrolabeRingSprite = CreateAstrolabeRingSprite(384);

            // 1. Deep Midnight Indigo Sky Gradient
            var skyRect = CreateStretchRect("Midnight Sky Gradient", stageRoot);
            skyImage = skyRect.gameObject.AddComponent<Image>();
            skyImage.sprite = CreateSkyGradientSprite(256, 512);
            skyImage.raycastTarget = false;

            // Oversized Camera Rig for smooth 2.5D pan & breathing zoom
            cameraRig = CreateStretchRect("Stage Camera Rig", stageRoot);
            cameraRig.localScale = Vector3.one * 1.07f;

            // 2. Far Layer: Gothic Pointed Arches & Distant Ruined Archive
            farVaultLayer = CreateAnchoredRect("Far Gothic Colonnade", cameraRig, new Vector2(2260f, 1200f), new Vector2(0.5f, 0.5f), Vector2.zero);
            farVaultImage = farVaultLayer.gameObject.AddComponent<Image>();
            farVaultImage.sprite = CreateGothicColonnadeSprite(1024, 540);
            farVaultImage.raycastTarget = false;

            // 3. Sacred Astrolabe Rune Rings (Counter-rotating geometric light rings in the vault)
            outerAstrolabeRect = CreateAnchoredRect("Outer Sacred Astrolabe", cameraRig, new Vector2(760f, 760f), new Vector2(0.5f, 0.56f), Vector2.zero);
            outerAstrolabeImage = outerAstrolabeRect.gameObject.AddComponent<Image>();
            outerAstrolabeImage.sprite = astrolabeRingSprite;
            outerAstrolabeImage.color = new Color(1.0f, 0.74f, 0.32f, 0.14f);
            outerAstrolabeImage.raycastTarget = false;

            innerAstrolabeRect = CreateAnchoredRect("Inner Sacred Astrolabe", cameraRig, new Vector2(490f, 490f), new Vector2(0.5f, 0.56f), Vector2.zero);
            innerAstrolabeImage = innerAstrolabeRect.gameObject.AddComponent<Image>();
            innerAstrolabeImage.sprite = astrolabeRingSprite;
            innerAstrolabeImage.color = new Color(1.0f, 0.82f, 0.44f, 0.18f);
            innerAstrolabeImage.raycastTarget = false;

            // Moonlit Mist Veil
            var mistRect = CreateAnchoredRect("Midnight Mist Veil", cameraRig, new Vector2(2400f, 700f), new Vector2(0.5f, 0.24f), Vector2.zero);
            var mistImg = mistRect.gameObject.AddComponent<Image>();
            mistImg.sprite = radialGlowSprite;
            mistImg.color = new Color(0.08f, 0.16f, 0.32f, 0.28f);
            mistImg.raycastTarget = false;

            // 4. Mid Layer: Ruined Stone Pillars & Carved Glowing Ember Veins
            midPillarLayer = CreateAnchoredRect("Mid Archive Pillars", cameraRig, new Vector2(2180f, 1180f), new Vector2(0.5f, 0.5f), Vector2.zero);
            CreatePillarAndVeinSprites(1024, 540, out Sprite pillarSprite, out Sprite veinGlowSprite);

            midPillarImage = midPillarLayer.gameObject.AddComponent<Image>();
            midPillarImage.sprite = pillarSprite;
            midPillarImage.raycastTarget = false;

            var veinRect = CreateStretchRect("Carved Ember Veins Glow", midPillarLayer);
            pillarVeinGlowImage = veinRect.gameObject.AddComponent<Image>();
            pillarVeinGlowImage.sprite = veinGlowSprite;
            pillarVeinGlowImage.color = new Color(1f, 0.68f, 0.22f, 0.65f);
            pillarVeinGlowImage.raycastTarget = false;

            // 5. Swinging Pendulum Great Lantern
            BuildSwingingLantern(cameraRig);

            // 6. Interactive Bezier Ember Vein Energy Tether
            BuildEnergyTether(cameraRig);

            // 7. Expanding Golden Interaction Shockwave Ripple
            rippleRect = CreateAnchoredRect("Interaction Light Ripple", cameraRig, new Vector2(160f, 160f), new Vector2(0.5f, 0.5f), Vector2.zero);
            rippleImage = rippleRect.gameObject.AddComponent<Image>();
            rippleImage.sprite = astrolabeRingSprite;
            rippleImage.color = Color.clear;
            rippleImage.raycastTarget = false;

            // 8. Foreground Layer: Cliff Overlook & Cloaked Wanderer with Handheld Lantern
            BuildForegroundWanderer(cameraRig);

            // 9. Multi-Depth Floating Embers & Rising Lantern Sparks
            BuildEmberField(stageRoot);

            // 10. Mouse-Following Lantern Spotlight
            cursorLanternRect = CreateAnchoredRect("Mouse Lantern Spotlight", stageRoot, new Vector2(520f, 520f), new Vector2(0.5f, 0.5f), Vector2.zero);
            cursorLanternGlow = cursorLanternRect.gameObject.AddComponent<Image>();
            cursorLanternGlow.sprite = radialGlowSprite;
            cursorLanternGlow.color = new Color(1.0f, 0.68f, 0.24f, 0.11f);
            cursorLanternGlow.raycastTarget = false;
        }

        private void BuildSwingingLantern(RectTransform parent)
        {
            pendulumPivot = CreateAnchoredRect("Pendulum Lantern Pivot", parent, new Vector2(40f, 40f), new Vector2(0.76f, 1.03f), Vector2.zero);
            pendulumPivot.pivot = new Vector2(0.5f, 1f);

            const int chainCount = 6;
            for (int i = 0; i < chainCount; i++)
            {
                var linkRect = CreateAnchoredRect($"ChainLink_{i}", pendulumPivot, new Vector2(20f, 42f), new Vector2(0.5f, 1f), new Vector2(0f, -12f - i * 33f));
                linkRect.pivot = new Vector2(0.5f, 1f);
                var linkImg = linkRect.gameObject.AddComponent<Image>();
                linkImg.sprite = chainLinkSprite;
                linkImg.color = new Color(0.86f, 0.72f, 0.52f, 0.92f);
                linkImg.raycastTarget = false;
            }

            lanternHeadRect = CreateAnchoredRect("Great Archive Lantern", pendulumPivot, new Vector2(176f, 220f), new Vector2(0.5f, 1f), new Vector2(0f, -208f));
            lanternHeadRect.pivot = new Vector2(0.5f, 0.85f);

            float[] rayAngles = { -40f, -20f, 0f, 20f, 40f };
            float[] rayWidths = { 210f, 300f, 360f, 290f, 200f };
            for (int i = 0; i < rayAngles.Length; i++)
            {
                var rayRect = CreateAnchoredRect($"VolumetricGodRay_{i}", lanternHeadRect, new Vector2(rayWidths[i], 1180f), new Vector2(0.5f, 0.48f), Vector2.zero);
                rayRect.pivot = new Vector2(0.5f, 1f);
                rayRect.localEulerAngles = new Vector3(0f, 0f, rayAngles[i]);
                var rayImg = rayRect.gameObject.AddComponent<Image>();
                rayImg.sprite = lightRaySprite;
                rayImg.color = new Color(1.0f, 0.72f, 0.28f, 0.085f);
                rayImg.raycastTarget = false;
                godRays.Add(rayRect);
                godRayImages.Add(rayImg);
            }

            var outerHaloRect = CreateAnchoredRect("Pedestal Lantern Outer", lanternHeadRect, new Vector2(960f, 960f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternOuterHalo = outerHaloRect.gameObject.AddComponent<Image>();
            lanternOuterHalo.sprite = radialGlowSprite;
            lanternOuterHalo.color = new Color(0.98f, 0.56f, 0.16f, 0.26f);
            lanternOuterHalo.raycastTarget = false;

            var midHaloRect = CreateAnchoredRect("Pedestal Lantern Core", lanternHeadRect, new Vector2(420f, 420f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternMidHalo = midHaloRect.gameObject.AddComponent<Image>();
            lanternMidHalo.sprite = radialGlowSprite;
            lanternMidHalo.color = new Color(1.0f, 0.78f, 0.34f, 0.48f);
            lanternMidHalo.raycastTarget = false;

            var coreRect = CreateAnchoredRect("Lantern Flame Heart", lanternHeadRect, new Vector2(130f, 160f), new Vector2(0.5f, 0.45f), Vector2.zero);
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
            const int nodeCount = 26;
            for (int i = 0; i < nodeCount; i++)
            {
                float norm = i / (float)(nodeCount - 1);
                float size = Mathf.Lerp(17f, 8f, norm);
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

        private void BuildForegroundWanderer(RectTransform parent)
        {
            foregroundLedgeLayer = CreateAnchoredRect("Foreground Ledge & Wanderer", parent, new Vector2(2280f, 1180f), new Vector2(0.5f, 0.5f), Vector2.zero);

            var ledgeImg = foregroundLedgeLayer.gameObject.AddComponent<Image>();
            ledgeImg.sprite = CreateForegroundLedgeSprite(1024, 540);
            ledgeImg.raycastTarget = false;

            wandererBodyRect = CreateAnchoredRect("Cloaked Wanderer Silhouette", foregroundLedgeLayer, new Vector2(330f, 410f), new Vector2(0.19f, 0.15f), Vector2.zero);
            wandererBodyRect.pivot = new Vector2(0.5f, 0.1f);
            var wandererImg = wandererBodyRect.gameObject.AddComponent<Image>();
            wandererImg.sprite = CreateWandererSprite(256, 320);
            wandererImg.raycastTarget = false;

            wandererLanternRect = CreateAnchoredRect("Wanderer Handheld Lantern", wandererBodyRect, new Vector2(260f, 260f), new Vector2(0.78f, 0.54f), Vector2.zero);
            wandererHalo = wandererLanternRect.gameObject.AddComponent<Image>();
            wandererHalo.sprite = radialGlowSprite;
            wandererHalo.color = new Color(1.0f, 0.68f, 0.24f, 0.42f);
            wandererHalo.raycastTarget = false;

            var smallFlameRect = CreateAnchoredRect("Wanderer Lantern Core", wandererLanternRect, new Vector2(52f, 52f), new Vector2(0.5f, 0.5f), Vector2.zero);
            wandererFlame = smallFlameRect.gameObject.AddComponent<Image>();
            wandererFlame.sprite = diamondSparkSprite;
            wandererFlame.color = new Color(1.0f, 0.92f, 0.72f, 0.95f);
            wandererFlame.raycastTarget = false;
        }

        private void BuildEmberField(RectTransform parent)
        {
            var layer = CreateStretchRect("Multi-Depth Ember Field", parent);
            for (int i = 0; i < 76; i++)
            {
                bool isLanternSpark = i < 16;
                float depth = isLanternSpark ? 0.95f : ((i % 4 == 0) ? 1f : ((i % 2 == 0) ? 0.55f : 0.22f));
                float size = depth > 0.8f ? 10.5f : (depth > 0.4f ? 6.2f : 3.2f);
                var rect = CreateAnchoredRect($"EmberMote_{i:00}", layer, new Vector2(size, size), Vector2.zero, Vector2.zero);
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = (depth > 0.75f && i % 2 == 0) ? diamondSparkSprite : radialGlowSprite;
                img.raycastTarget = false;

                embers.Add(new EmberMote
                {
                    Rect = rect,
                    Img = img,
                    SeedX = isLanternSpark ? (0.72f + (i % 5) * 0.018f) : Mathf.Repeat(i * 0.6180339f, 1f),
                    SeedY = Mathf.Repeat(i * 0.381966f, 1f),
                    SpeedY = isLanternSpark ? (0.045f + (i % 4) * 0.008f) : (Mathf.Lerp(0.014f, 0.038f, depth) + (i % 4) * 0.003f),
                    DriftAmp = isLanternSpark ? 0.032f : Mathf.Lerp(0.009f, 0.028f, depth),
                    DriftFreq = 0.45f + (i % 7) * 0.11f,
                    Depth = depth,
                    IsLanternSpark = isLanternSpark
                });
            }
        }

        public void TriggerInteractionSurge(Vector2 buttonCanvasPosition, Vector2 cameraPanTarget, float boost = 1f)
        {
            surgeIntensity = Mathf.Clamp(surgeIntensity + 0.90f * boost, 0f, 1.75f);
            pendulumBoost = Mathf.Clamp(pendulumBoost + 3.6f * boost, -8f, 8f);
            targetButtonCanvasPos = buttonCanvasPosition;
            targetCameraOffset = cameraPanTarget;
            hasActiveButtonTarget = true;

            // Trigger expanding golden light ripple at the focused position
            rippleCenter = buttonCanvasPosition;
            rippleProgress = 0f;
        }

        public void TickStage(float dt, float introIgnition, Vector2 normalizedMouse, Vector2 mouseCanvasPosition, bool reducedMotion)
        {
            time += dt;
            surgeIntensity = Mathf.MoveTowards(surgeIntensity, 0f, dt * 1.35f);
            pendulumBoost = Mathf.MoveTowards(pendulumBoost, 0f, dt * 2.3f);

            // Astrolabe sacred geometry rotation (accelerates on button hover/click surge!)
            if (!reducedMotion)
            {
                astrolabeAngle += dt * (4.5f + surgeIntensity * 22f);
            }

            if (outerAstrolabeRect != null && outerAstrolabeImage != null)
            {
                outerAstrolabeRect.localEulerAngles = new Vector3(0f, 0f, astrolabeAngle);
                float astroScale = 1f + (reducedMotion ? 0f : Mathf.Sin(time * 0.8f) * 0.025f) + surgeIntensity * 0.04f;
                outerAstrolabeRect.localScale = new Vector3(astroScale, astroScale, 1f);
                outerAstrolabeImage.color = new Color(1.0f, 0.74f, 0.32f, (0.12f + surgeIntensity * 0.14f) * introIgnition);
            }

            if (innerAstrolabeRect != null && innerAstrolabeImage != null)
            {
                innerAstrolabeRect.localEulerAngles = new Vector3(0f, 0f, -astrolabeAngle * 1.45f);
                innerAstrolabeImage.color = new Color(1.0f, 0.84f, 0.46f, (0.16f + surgeIntensity * 0.18f) * introIgnition);
            }

            // Expanding Golden Interaction Ripple
            if (rippleProgress < 1f && rippleRect != null && rippleImage != null)
            {
                rippleProgress = Mathf.MoveTowards(rippleProgress, 1f, dt * 1.85f);
                rippleRect.anchoredPosition = rippleCenter;
                float rScale = Mathf.Lerp(0.35f, 2.85f, SmoothStep(rippleProgress));
                rippleRect.localScale = new Vector3(rScale, rScale * 0.42f, 1f);
                rippleImage.color = new Color(1.0f, 0.78f, 0.32f, (1f - rippleProgress) * 0.42f * introIgnition);
            }

            // Smooth camera pan + subtle breathing zoom
            Vector2 desiredCam = reducedMotion
                ? Vector2.zero
                : targetCameraOffset + new Vector2(-normalizedMouse.x * 26f, -normalizedMouse.y * 16f);
            currentCameraOffset = Vector2.Lerp(currentCameraOffset, desiredCam, 1f - Mathf.Exp(-dt * 4.5f));
            currentButtonCanvasPos = Vector2.Lerp(currentButtonCanvasPos, targetButtonCanvasPos, 1f - Mathf.Exp(-dt * 11f));

            if (cameraRig != null && !reducedMotion)
            {
                float rigZoom = 1.07f + Mathf.Sin(time * 0.35f) * 0.012f + surgeIntensity * 0.015f;
                cameraRig.localScale = new Vector3(rigZoom, rigZoom, 1f);
            }

            // 1. Multi-layer 2.5D Parallax Offsets
            if (farVaultLayer != null) farVaultLayer.anchoredPosition = currentCameraOffset * 0.42f;
            if (midPillarLayer != null) midPillarLayer.anchoredPosition = currentCameraOffset * 0.85f;
            if (foregroundLedgeLayer != null) foregroundLedgeLayer.anchoredPosition = currentCameraOffset * 1.38f;

            // 2. Compound Pendulum Swinging Lantern
            float swingAngle = reducedMotion
                ? 0f
                : Mathf.Sin(time * 1.32f) * (4.6f + pendulumBoost) + Mathf.Cos(time * 0.68f) * 1.6f - normalizedMouse.x * 2.8f;

            if (pendulumPivot != null)
            {
                pendulumPivot.anchoredPosition = currentCameraOffset * 0.95f;
                pendulumPivot.localEulerAngles = new Vector3(0f, 0f, swingAngle);
            }

            if (lanternHeadRect != null && !reducedMotion)
            {
                // Secondary lag whip on the lantern cage itself
                float headLag = Mathf.Cos(time * 1.32f) * (2.2f + pendulumBoost * 0.4f);
                lanternHeadRect.localEulerAngles = new Vector3(0f, 0f, headLag);
            }

            float flicker = reducedMotion
                ? 0.92f
                : 0.88f + Mathf.Sin(time * 2.9f) * 0.07f + Mathf.Sin(time * 7.7f) * 0.04f + Mathf.Cos(time * 15.3f) * 0.02f;
            float totalGlow = Mathf.Clamp(flicker + surgeIntensity * 0.52f, 0f, 1.70f) * introIgnition;

            if (lanternOuterHalo != null)
            {
                lanternOuterHalo.color = new Color(0.98f, 0.56f, 0.16f, Mathf.Clamp01(0.27f * totalGlow));
                float s = 1f + surgeIntensity * 0.20f + (reducedMotion ? 0f : Mathf.Sin(time * 1.9f) * 0.045f);
                lanternOuterHalo.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (lanternMidHalo != null)
            {
                lanternMidHalo.color = new Color(1.0f, 0.78f, 0.34f, Mathf.Clamp01(0.52f * totalGlow));
            }
            if (lanternFlameCore != null)
            {
                lanternFlameCore.color = new Color(1.0f, 0.96f, 0.80f, Mathf.Clamp01(0.96f * totalGlow));
                float coreScale = 1f + surgeIntensity * 0.28f + (reducedMotion ? 0f : Mathf.Sin(time * 5.6f) * 0.08f);
                lanternFlameCore.rectTransform.localScale = new Vector3(coreScale, coreScale, 1f);
            }

            for (int i = 0; i < godRayImages.Count; i++)
            {
                float rayWave = reducedMotion ? 1f : (0.72f + 0.28f * Mathf.Sin(time * 1.5f + i * 1.15f));
                float alpha = (0.08f + surgeIntensity * 0.07f) * rayWave * introIgnition;
                godRayImages[i].color = new Color(1.0f, 0.74f, 0.30f, Mathf.Clamp01(alpha));
            }

            // 3. Carved Pillar Ember Veins
            if (pillarVeinGlowImage != null)
            {
                float veinPulse = reducedMotion ? 0.55f : (0.48f + 0.22f * Mathf.Sin(time * 2.1f));
                float veinAlpha = Mathf.Clamp01((veinPulse + surgeIntensity * 0.48f) * introIgnition);
                pillarVeinGlowImage.color = new Color(1.0f, 0.68f, 0.22f, veinAlpha);
            }

            // 4. Foreground Cloaked Wanderer Breathing & Cloak Sway
            if (wandererBodyRect != null)
            {
                float breathY = reducedMotion ? 1f : (1f + Mathf.Sin(time * 1.7f) * 0.015f);
                float cloakSway = reducedMotion ? 0f : Mathf.Sin(time * 2.3f) * 1.1f;
                wandererBodyRect.localScale = new Vector3(1f, breathY, 1f);
                wandererBodyRect.localEulerAngles = new Vector3(0f, 0f, cloakSway);
            }
            if (wandererHalo != null)
            {
                wandererHalo.color = new Color(1.0f, 0.68f, 0.24f, Mathf.Clamp01((0.42f + surgeIntensity * 0.25f) * flicker * introIgnition));
            }
            if (wandererFlame != null)
            {
                wandererFlame.rectTransform.localEulerAngles = new Vector3(0f, 0f, time * 28f);
            }

            // 5. Interactive Bezier Ember Vein Energy Stream
            UpdateEnergyTether(introIgnition, reducedMotion);

            // 6. Floating Ember & Rising Lantern Spark Field
            float speedMult = 1f + surgeIntensity * 0.95f;
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
                    float twinkle = 0.68f + 0.32f * Mathf.Sin(time * 5.8f + i * 1.7f);
                    e.Img.color = new Color(1.0f, 0.74f, 0.28f, Mathf.Clamp01((0.58f + surgeIntensity * 0.32f) * life * twinkle * introIgnition));
                }
                else
                {
                    e.Img.color = new Color(0.55f, 0.68f, 0.88f, Mathf.Clamp01(0.18f * life * introIgnition));
                }
            }

            // 7. Mouse Lantern Spotlight
            if (cursorLanternRect != null && cursorLanternGlow != null)
            {
                cursorLanternRect.anchoredPosition = Vector2.Lerp(cursorLanternRect.anchoredPosition, mouseCanvasPosition, 1f - Mathf.Exp(-dt * 10f));
                cursorLanternGlow.color = new Color(1.0f, 0.68f, 0.25f, (0.12f + surgeIntensity * 0.07f) * introIgnition);
            }
        }

        private void UpdateEnergyTether(float introIgnition, bool reducedMotion)
        {
            if (!hasActiveButtonTarget || tetherNodes.Count == 0)
            {
                return;
            }

            float swingRad = pendulumPivot != null ? pendulumPivot.localEulerAngles.z * Mathf.Deg2Rad : 0f;
            Vector2 startPos = new Vector2(495f + Mathf.Sin(swingRad) * 265f, 265f);
            Vector2 endPos = currentButtonCanvasPos;
            Vector2 controlPos = (startPos + endPos) * 0.5f + new Vector2(-110f, 115f);

            float streamVisibility = Mathf.Clamp01((0.34f + surgeIntensity * 0.66f) * introIgnition);

            for (int i = 0; i < tetherNodes.Count; i++)
            {
                var node = tetherNodes[i];
                float flowT = reducedMotion
                    ? node.Phase
                    : Mathf.Repeat(node.Phase + time * (0.48f + surgeIntensity * 0.38f), 1f);

                float inv = 1f - flowT;
                Vector2 pos = inv * inv * startPos + 2f * inv * flowT * controlPos + flowT * flowT * endPos;
                float wave = reducedMotion ? 0f : Mathf.Sin(flowT * 11f - time * 5.5f) * 7f * Mathf.Sin(flowT * Mathf.PI);
                pos.y += wave;

                node.Rect.anchoredPosition = pos;
                float edgeFade = Mathf.Sin(flowT * Mathf.PI);
                float alpha = streamVisibility * edgeFade;
                node.Img.color = new Color(1.0f, 0.78f, 0.32f, alpha);
                float pulseScale = 0.75f + 0.5f * edgeFade + surgeIntensity * 0.28f;
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
        // PROCEDURAL ART & SDF TEXTURE GENERATORS
        // =====================================================================

        private Sprite RegisterSprite(Texture2D tex)
        {
            tex.Apply();
            generatedTextures.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100f);
            generatedSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateAstrolabeRingSprite(int size)
        {
            var tex = NewTexture(size, size);
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxR = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 dVec = new Vector2(x, y) - center;
                    float r = dVec.magnitude / maxR;
                    float angle = Mathf.Atan2(dVec.y, dVec.x);

                    // Outer double ring + inner dashed ring + 12 radial rune ticks
                    float ring1 = Mathf.Clamp01(1f - Mathf.Abs(r - 0.90f) / 0.008f);
                    float ring2 = Mathf.Clamp01(1f - Mathf.Abs(r - 0.84f) / 0.005f) * (Mathf.Sin(angle * 24f) > 0f ? 1f : 0.2f);
                    float ring3 = Mathf.Clamp01(1f - Mathf.Abs(r - 0.64f) / 0.006f);

                    float tick = 0f;
                    if (r >= 0.82f && r <= 0.92f)
                    {
                        float angularMod = Mathf.Abs(Mathf.Sin(angle * 12f));
                        if (angularMod > 0.992f) tick = 0.85f;
                    }

                    float alpha = Mathf.Clamp01(Mathf.Max(Mathf.Max(ring1, ring2), Mathf.Max(ring3 * 0.6f, tick)));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateSkyGradientSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color topVault = new Color(0.016f, 0.028f, 0.064f, 1f);
            Color midArchive = new Color(0.040f, 0.072f, 0.142f, 1f);
            Color lowerAbyss = new Color(0.020f, 0.030f, 0.056f, 1f);
            Color warmBounce = new Color(0.16f, 0.09f, 0.04f, 1f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                Color vertical = ny > 0.45f
                    ? Color.Lerp(midArchive, topVault, (ny - 0.45f) / 0.55f)
                    : Color.Lerp(lowerAbyss, midArchive, ny / 0.45f);

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float lanternDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.75f, 0.68f));
                    float bounce = Mathf.Pow(Mathf.Clamp01(1f - lanternDist * 1.45f), 2.2f) * 0.45f;
                    tex.SetPixel(x, y, Color.Lerp(vertical, warmBounce, bounce));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateGothicColonnadeSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color stoneDark = new Color(0.024f, 0.040f, 0.082f, 0.94f);
            Color shelfSilhouette = new Color(0.034f, 0.056f, 0.108f, 0.78f);
            Color warmRim = new Color(0.95f, 0.62f, 0.22f, 0.35f);

            const int archCount = 5;
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float cell = nx * archCount;
                    float localX = (cell - Mathf.Floor(cell)) * 2f - 1f;
                    float absX = Mathf.Abs(localX);

                    float archHeight = 0.76f - 0.38f * (absX * absX + 0.35f * absX);
                    bool isPillarOrVault = absX > 0.78f || ny > archHeight;

                    float roseDist = Vector2.Distance(new Vector2(localX * 0.6f, ny), new Vector2(0f, 0.87f));
                    if (roseDist < 0.065f && Mathf.Abs(roseDist - 0.042f) > 0.008f)
                    {
                        isPillarOrVault = false;
                    }

                    if (isPillarOrVault)
                    {
                        float edgeDist = Mathf.Min(Mathf.Abs(absX - 0.78f), Mathf.Abs(ny - archHeight));
                        float rim = Mathf.Clamp01(1f - edgeDist * 45f) * Mathf.Clamp01(1f - Mathf.Abs(nx - 0.74f) * 1.8f);
                        tex.SetPixel(x, y, Color.Lerp(stoneDark, warmRim, rim * 0.45f));
                    }
                    else
                    {
                        bool shelfRow = (Mathf.FloorToInt(ny * 26f) % 3 == 0) && ny < 0.62f;
                        bool bookSpine = (Mathf.FloorToInt(nx * 140f) % 4 != 0) && ny < 0.60f;
                        if ((shelfRow || (bookSpine && (Mathf.FloorToInt(ny * 26f) % 3 == 1))) && absX > 0.25f)
                        {
                            tex.SetPixel(x, y, shelfSilhouette);
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                }
            }
            return RegisterSprite(tex);
        }

        private void CreatePillarAndVeinSprites(int w, int h, out Sprite pillarSprite, out Sprite veinSprite)
        {
            var pillarTex = NewTexture(w, h);
            var veinTex = NewTexture(w, h);

            Color pillarCol = new Color(0.015f, 0.024f, 0.050f, 0.98f);
            Color goldRim = new Color(0.98f, 0.66f, 0.24f, 0.85f);

            float[] pillarCenters = { 0.06f, 0.24f, 0.76f, 0.94f };
            float[] pillarWidths = { 0.052f, 0.028f, 0.028f, 0.052f };

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    Color pOut = Color.clear;
                    Color vOut = Color.clear;

                    for (int i = 0; i < pillarCenters.Length; i++)
                    {
                        float dx = nx - pillarCenters[i];
                        float pw = pillarWidths[i];
                        if (ny > 0.82f) pw += (ny - 0.82f) * 0.14f;
                        if (ny < 0.12f) pw += (0.12f - ny) * 0.10f;

                        if (Mathf.Abs(dx) <= pw)
                        {
                            float innerSide = (pillarCenters[i] < 0.5f) ? (dx / pw) : (-dx / pw);
                            float rim = Mathf.Pow(Mathf.Clamp01((innerSide + 1f) * 0.5f), 5f) * 0.5f;
                            pOut = Color.Lerp(pillarCol, goldRim, rim);

                            float veinPath1 = Mathf.Sin(ny * 18f + i * 2.1f) * (pw * 0.38f);
                            float veinPath2 = Mathf.Cos(ny * 29f - i * 1.4f) * (pw * 0.26f);
                            float d1 = Mathf.Abs(dx - veinPath1);
                            float d2 = Mathf.Abs(dx - veinPath2);
                            float veinMask = Mathf.Clamp01(1f - Mathf.Min(d1, d2) / 0.0045f);
                            if (veinMask > 0.01f && ny > 0.10f && ny < 0.88f)
                            {
                                vOut = new Color(1f, 1f, 1f, veinMask * Mathf.Sin(ny * Mathf.PI));
                            }
                            break;
                        }
                    }

                    pillarTex.SetPixel(x, y, pOut);
                    veinTex.SetPixel(x, y, vOut);
                }
            }

            pillarSprite = RegisterSprite(pillarTex);
            veinSprite = RegisterSprite(veinTex);
        }

        private Sprite CreateLanternCageSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color iron = new Color(0.06f, 0.05f, 0.07f, 0.98f);
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
                    if (ringDist > 0.05f && ringDist < 0.095f) solid = true;

                    if (ny >= 0.68f && ny <= 0.86f)
                    {
                        float roofWidth = Mathf.Lerp(0.56f, 0.05f, (ny - 0.68f) / 0.18f);
                        if (ax <= roofWidth)
                        {
                            solid = true;
                            if (Mathf.Abs(ax - roofWidth) < 0.03f || ny < 0.705f) rim = true;
                        }
                    }

                    if (ny >= 0.26f && ny < 0.68f)
                    {
                        float cageOuter = Mathf.Lerp(0.36f, 0.48f, (ny - 0.26f) / 0.42f);
                        if (Mathf.Abs(ax - cageOuter) < 0.038f || ax < 0.028f)
                        {
                            solid = true;
                            rim = true;
                        }
                        if (Mathf.Abs(ny - 0.47f) < 0.018f && ax <= cageOuter)
                        {
                            solid = true;
                            rim = true;
                        }
                    }

                    if (ny >= 0.04f && ny < 0.26f)
                    {
                        float baseWidth = Mathf.Lerp(0.015f, 0.40f, Mathf.Pow((ny - 0.04f) / 0.22f, 0.65f));
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
            Color iron = new Color(0.14f, 0.12f, 0.15f, 0.95f);
            Color highlight = new Color(0.85f, 0.62f, 0.28f, 0.90f);

            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx * 1.4f + ny * ny * 0.7f);
                    if (d >= 0.46f && d <= 0.82f)
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
            Color darkStone = new Color(0.010f, 0.015f, 0.032f, 1f);
            Color amberEdge = new Color(0.96f, 0.64f, 0.22f, 0.75f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float leftLedgeH = 0.18f - Mathf.Max(0f, nx - 0.21f) * 1.15f + Mathf.Sin(nx * 28f) * 0.008f;
                    float rightLedgeH = 0.14f - Mathf.Max(0f, 0.84f - nx) * 0.95f + Mathf.Cos(nx * 24f) * 0.007f;
                    float floorH = 0.038f + Mathf.Sin(nx * 15f) * 0.006f;

                    float maxH = Mathf.Max(floorH, Mathf.Max(nx < 0.36f ? leftLedgeH : 0f, nx > 0.72f ? rightLedgeH : 0f));

                    if (ny <= maxH)
                    {
                        float edge = Mathf.Clamp01(1f - (maxH - ny) * 55f);
                        float lanternProximity = Mathf.Clamp01(1f - Mathf.Abs(nx - 0.21f) * 4.5f);
                        tex.SetPixel(x, y, Color.Lerp(darkStone, amberEdge, edge * lanternProximity * 0.65f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateWandererSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color cloakDark = new Color(0.018f, 0.026f, 0.050f, 0.99f);
            Color goldRim = new Color(1.0f, 0.74f, 0.30f, 0.92f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    bool inside = false;
                    float rimFactor = 0f;

                    float headDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.47f, 0.77f));
                    if (headDist < 0.095f || (ny >= 0.75f && ny <= 0.89f && Mathf.Abs(nx - 0.44f) < (0.89f - ny) * 0.7f))
                    {
                        inside = true;
                        if (nx > 0.49f) rimFactor = Mathf.Clamp01((nx - 0.49f) * 14f);
                    }

                    if (ny >= 0.08f && ny <= 0.75f)
                    {
                        float t = (0.75f - ny) / 0.67f;
                        float leftEdge = 0.42f - t * 0.24f - Mathf.Sin(t * 5f) * 0.03f;
                        float rightEdge = 0.52f + t * 0.11f;
                        float hemCut = 0.08f + Mathf.Abs(Mathf.Sin(nx * 26f)) * 0.045f;
                        if (nx >= leftEdge && nx <= rightEdge && ny >= hemCut)
                        {
                            inside = true;
                            if (rightEdge - nx < 0.035f) rimFactor = Mathf.Clamp01(1f - (rightEdge - nx) / 0.035f);
                        }
                    }

                    if (nx >= 0.48f && nx <= 0.79f)
                    {
                        float armY = Mathf.Lerp(0.64f, 0.60f, (nx - 0.48f) / 0.31f);
                        if (Mathf.Abs(ny - armY) < 0.024f)
                        {
                            inside = true;
                            rimFactor = 0.75f;
                        }
                    }

                    if (Mathf.Abs(nx - 0.78f) < 0.035f && ny >= 0.49f && ny <= 0.60f)
                    {
                        inside = true;
                        rimFactor = 0.9f;
                    }

                    tex.SetPixel(x, y, inside ? Color.Lerp(cloakDark, goldRim, rimFactor * 0.7f) : Color.clear);
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateRadialGlowSprite(int size)
        {
            var tex = NewTexture(size, size);
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxR = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.3f);
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
                float halfWidth = Mathf.Lerp(0.48f, 0.10f, ny);
                float verticalFade = Mathf.Pow(ny, 1.35f) * Mathf.Sin(ny * Mathf.PI);

                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs((x / (float)(w - 1)) - 0.5f);
                    float horizFade = Mathf.Pow(Mathf.Clamp01(1f - nx / halfWidth), 2f);
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
                    float star = Mathf.Clamp01(1f - (nx * 0.85f + ny * 0.85f)) + Mathf.Clamp01(1f - Mathf.Min(nx, ny) * 6f) * Mathf.Clamp01(1f - Mathf.Max(nx, ny));
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

                    float lineThickness = Mathf.Lerp(0.08f, 0.01f, ax);
                    float mainLine = Mathf.Clamp01(1f - ay / lineThickness) * Mathf.Clamp01(1f - ax);
                    float centerDiamond = Mathf.Clamp01(1f - (ax * 14f + ay * 1.8f));

                    float wingCurveY = Mathf.Sin((ax - 0.06f) * 18f) * 0.42f * Mathf.Clamp01(1f - Mathf.Abs(ax - 0.16f) * 7f);
                    float wing = (ax > 0.05f && ax < 0.28f)
                        ? Mathf.Clamp01(1f - Mathf.Abs(ay - Mathf.Abs(wingCurveY)) / 0.07f) * Mathf.Clamp01(1f - (ax - 0.05f) * 4f)
                        : 0f;

                    float alpha = Mathf.Clamp01(Mathf.Max(mainLine, Mathf.Max(centerDiamond, wing)));
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

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
