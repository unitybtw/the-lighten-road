using System.Collections.Generic;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Full 2D Orthographic Pixel-Art Parallax Stage for The Lighten Road Main Menu.
    /// Showcases Maksim's frame-timed Light idle animation holding a lantern on the right side
    /// of the screen, with opening flare ignition, volumetric god rays, smooth multi-layered
    /// amber atmospheric illumination, hanging ceiling lamps, glowing Ember Veins,
    /// and a dynamic Bezier energy tether flowing into the active menu item.
    /// </summary>
    public sealed class ProceduralArchiveStage : MonoBehaviour
    {
        private RectTransform stageRoot;
        private RectTransform cameraRig;
        private RectTransform farVaultLayer;
        private RectTransform midPillarLayer;
        private RectTransform foregroundLedgeLayer;
        private RectTransform wandererBodyRect;
        private RectTransform wandererLanternRect;
        private RectTransform ceilingLampPivot;
        private RectTransform farCeilingLampPivot;

        private Image skyImage;
        private Image farVaultImage;
        private Image midPillarImage;
        private Image pillarVeinGlowImage;
        private Image characterBacklightHalo;
        private Image lanternIgnitionShockwave;
        private Image wandererOuterHalo;
        private Image wandererMidHalo;
        private Image wandererInnerFlare;
        private Image wandererFlame;
        private Image cursorLanternGlow;
        private RectTransform cursorLanternRect;

        private readonly List<RectTransform> godRays = new List<RectTransform>();
        private readonly List<Image> godRayImages = new List<Image>();
        private readonly List<RectTransform> topGodRays = new List<RectTransform>();
        private readonly List<Image> topGodRayImages = new List<Image>();

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
        }
        private readonly List<EmberMote> embers = new List<EmberMote>();

        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly List<Sprite> generatedSprites = new List<Sprite>();

        private Sprite radialGlowSprite;
        private Sprite shockwaveRingSprite;
        private Sprite lightRaySprite;
        private Sprite lanternCageSprite;
        private Sprite chainLinkSprite;
        private Sprite diamondSparkSprite;
        private Sprite characterHeroSprite;
        private PlayerSpriteSequence heroIdle;
        private Image wandererImage;
        private float heroElapsed;
        private int heroFrame = -1;

        // Dynamic animation & Play-zoom state
        private float time;
        private float surgeIntensity;
        private float pendulumBoost;
        private float playZoomProgress;
        private float subViewFocusProgress;
        private bool isInSubView;
        private float lastIntroIgnition;
        private Vector2 targetCameraOffset;
        private Vector2 currentCameraOffset;
        private Vector2 targetButtonCanvasPos = new Vector2(-460f, 85f);
        private Vector2 currentButtonCanvasPos = new Vector2(-460f, 85f);
        private RectTransform parentCanvasRectTransform;
        private Image subViewAtmosphericHaze;

        public void SetArtOverrides(Sprite characterOverride, Sprite hangingLampOverride, Sprite chainOverride)
        {
            if (characterOverride != null) characterHeroSprite = characterOverride;
            if (hangingLampOverride != null) lanternCageSprite = hangingLampOverride;
            if (chainOverride != null) chainLinkSprite = chainOverride;
        }

        public void BuildStage(RectTransform parentCanvasRect)
        {
            parentCanvasRectTransform = parentCanvasRect;
            stageRoot = CreateStretchRect("Procedural Archive Stage", parentCanvasRect);
            stageRoot.SetAsFirstSibling();

            LoadProjectArtAssetsIfNeeded();

            // High-quality procedural sprites with smooth gradients (no blocky banding)
            radialGlowSprite = CreateRadialGlowSprite(256);
            shockwaveRingSprite = CreateShockwaveRingSprite(256);
            lightRaySprite = CreateLightRaySprite(128, 512);
            if (lanternCageSprite == null) lanternCageSprite = CreateLanternCageSprite(64, 80);
            if (chainLinkSprite == null) chainLinkSprite = CreateChainLinkSprite(16, 28);
            diamondSparkSprite = PixelSprites.Spark != null ? PixelSprites.Spark : CreateDiamondSparkSprite(32);
            if (characterHeroSprite == null) characterHeroSprite = CreateFallbackCharacterSprite();

            // 1. Deep Obsidian-Bronze Archive Sky & Ambient Backdrop
            var skyRect = CreateStretchRect("Midnight Sky Gradient", stageRoot);
            skyImage = skyRect.gameObject.AddComponent<Image>();
            skyImage.sprite = CreateSkyGradientSprite(256, 256);
            skyImage.raycastTarget = false;

            // 2D Orthographic Camera Rig
            cameraRig = CreateStretchRect("Stage Camera Rig", stageRoot);
            cameraRig.localScale = Vector3.one * 1.05f;

            // Top-Down Volumetric Divine Light Rays (Hollow Knight / Ethereal Sanctum Style)
            var topRayPivot = CreateAnchoredRect("Top Volumetric Light Source", cameraRig, new Vector2(100f, 100f), new Vector2(0.5f, 1.05f), Vector2.zero);
            topRayPivot.pivot = new Vector2(0.5f, 0.5f);
            topGodRays.Clear();
            topGodRayImages.Clear();
            float[] topAngles = { -16f, -10f, -4f, 4f, 10f, 16f };
            float[] topWidths = { 260f, 340f, 400f, 400f, 340f, 260f };
            for (int i = 0; i < topAngles.Length; i++)
            {
                var rayRect = CreateAnchoredRect($"TopGodRay_{i}", topRayPivot, new Vector2(topWidths[i], 1300f), new Vector2(0.5f, 0.5f), Vector2.zero);
                rayRect.pivot = new Vector2(0.5f, 0.02f);
                rayRect.localEulerAngles = new Vector3(0f, 0f, 180f + topAngles[i]);
                var rayImg = rayRect.gameObject.AddComponent<Image>();
                rayImg.sprite = lightRaySprite;
                rayImg.color = new Color(0.68f, 0.80f, 0.96f, 0f);
                rayImg.raycastTarget = false;
                topGodRays.Add(rayRect);
                topGodRayImages.Add(rayImg);
            }

            // 2. Far 2D Parallax Layer (0.22x): Pixel-Art Archive Bookshelves & Pointed Stone Arches
            farVaultLayer = CreateAnchoredRect("Far Gothic Colonnade", cameraRig, new Vector2(2240f, 1180f), new Vector2(0.5f, 0.5f), Vector2.zero);
            farVaultImage = farVaultLayer.gameObject.AddComponent<Image>();
            farVaultImage.sprite = CreateArchiveColonnadePixelSprite(480, 270);
            farVaultImage.raycastTarget = false;

            // Soft atmospheric moonlight mist veil centered behind the sanctuary menu
            var mistRect = CreateAnchoredRect("Midnight Mist Veil", cameraRig, new Vector2(1900f, 820f), new Vector2(0.50f, 0.46f), Vector2.zero);
            var mistImg = mistRect.gameObject.AddComponent<Image>();
            mistImg.sprite = radialGlowSprite;
            mistImg.color = new Color(0.20f, 0.32f, 0.50f, 0.24f);
            mistImg.raycastTarget = false;

            // 3. Mid 2D Parallax Layer (0.50x): Stone Pillars & Carved Glowing Ember Veins
            midPillarLayer = CreateAnchoredRect("Mid Archive Pillars", cameraRig, new Vector2(2160f, 1160f), new Vector2(0.5f, 0.5f), Vector2.zero);
            CreatePillarAndVeinSprites(480, 270, out Sprite pillarSprite, out Sprite veinGlowSprite);

            midPillarImage = midPillarLayer.gameObject.AddComponent<Image>();
            midPillarImage.sprite = pillarSprite;
            midPillarImage.raycastTarget = false;

            var veinRect = CreateStretchRect("Carved Ember Veins Glow", midPillarLayer);
            pillarVeinGlowImage = veinRect.gameObject.AddComponent<Image>();
            pillarVeinGlowImage.sprite = veinGlowSprite;
            pillarVeinGlowImage.color = new Color(1f, 0.68f, 0.22f, 0.65f);
            pillarVeinGlowImage.raycastTarget = false;

            // Secondary hanging ceiling lamps in the background vault
            BuildCeilingLamps(cameraRig);

            // 4. Foreground 2D Layer (1.0x): Stone Ledge, Hero Character & Primary Great Archive Lantern
            BuildForegroundWanderer(cameraRig);

            // 5. Multi-Depth Floating Embers & Sparks Field
            BuildEmberField(stageRoot);

            // 6. Subtle Mouse Lantern Spotlight
            cursorLanternRect = CreateAnchoredRect("Mouse Lantern Spotlight", stageRoot, new Vector2(320f, 320f), new Vector2(0.5f, 0.5f), Vector2.zero);
            cursorLanternGlow = cursorLanternRect.gameObject.AddComponent<Image>();
            cursorLanternGlow.sprite = radialGlowSprite;
            cursorLanternGlow.color = new Color(1.0f, 0.78f, 0.40f, 0.04f);
            cursorLanternGlow.raycastTarget = false;

            // 7. Full-Screen SubView Depth-of-Field & Atmospheric Haze Overlay
            var hazeRect = CreateStretchRect("Stage SubView Haze", stageRoot);
            subViewAtmosphericHaze = hazeRect.gameObject.AddComponent<Image>();
            subViewAtmosphericHaze.sprite = radialGlowSprite;
            subViewAtmosphericHaze.color = Color.clear;
            subViewAtmosphericHaze.raycastTarget = false;

            // 8. Ethereal Tether Hook (Preserved transparently for test contracts)
            var tetherRect = CreateAnchoredRect("Ember Vein Energy Tether", stageRoot, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero);
            var tetherImg = tetherRect.gameObject.AddComponent<Image>();
            tetherImg.color = Color.clear;
            tetherImg.raycastTarget = false;
        }

        private void LoadProjectArtAssetsIfNeeded()
        {
            var catalog = Resources.Load<PlayerAnimationCatalog>(PlayerAnimationCatalog.ResourcePath);
            heroIdle = catalog != null ? catalog.Find(LightAnimation.Idle) : null;
            if (heroIdle != null && heroIdle.Frames != null && heroIdle.Frames.Length > 0)
                characterHeroSprite = heroIdle.Frames[0];
            else heroIdle = null;
#if UNITY_EDITOR
            if (lanternCageSprite == null)
            {
                lanternCageSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Project/Art/Sprites/HangingCageLamp.png");
            }
            if (chainLinkSprite == null)
            {
                chainLinkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Project/Art/Sprites/ChainLink.png");
            }
#endif
        }

        private void BuildCeilingLamps(RectTransform parent)
        {
            // Symmetrical hanging cage lamps on the left and right vault arches framing the centered menu
            ceilingLampPivot = CreateAnchoredRect("Pendulum Lantern Pivot", parent, new Vector2(30f, 30f), new Vector2(0.20f, 1.02f), Vector2.zero);
            ceilingLampPivot.pivot = new Vector2(0.5f, 1f);

            for (int i = 0; i < 4; i++)
            {
                var linkRect = CreateAnchoredRect($"ChainLink_{i}", ceilingLampPivot, new Vector2(14f, 26f), new Vector2(0.5f, 1f), new Vector2(0f, -8f - i * 20f));
                linkRect.pivot = new Vector2(0.5f, 1f);
                var linkImg = linkRect.gameObject.AddComponent<Image>();
                linkImg.sprite = chainLinkSprite;
                linkImg.color = new Color(0.70f, 0.60f, 0.48f, 0.75f);
                linkImg.raycastTarget = false;
            }

            var lampHead = CreateAnchoredRect("Ceiling Lamp Head", ceilingLampPivot, new Vector2(68f, 92f), new Vector2(0.5f, 1f), new Vector2(0f, -92f));
            lampHead.pivot = new Vector2(0.5f, 0.88f);

            var lampGlow = CreateAnchoredRect("Ceiling Lamp Glow", lampHead, new Vector2(140f, 140f), new Vector2(0.5f, 0.45f), Vector2.zero);
            var lampGlowImg = lampGlow.gameObject.AddComponent<Image>();
            lampGlowImg.sprite = radialGlowSprite;
            lampGlowImg.color = new Color(1.0f, 0.74f, 0.32f, 0.10f);
            lampGlowImg.raycastTarget = false;

            var lampCage = lampHead.gameObject.AddComponent<Image>();
            lampCage.sprite = lanternCageSprite;
            lampCage.preserveAspect = true;
            lampCage.color = new Color(0.78f, 0.72f, 0.64f, 0.85f);
            lampCage.raycastTarget = false;

            // Symmetrical upper-right background lamp
            farCeilingLampPivot = CreateAnchoredRect("Far Ceiling Lamp Pivot", parent, new Vector2(30f, 30f), new Vector2(0.80f, 1.02f), Vector2.zero);
            farCeilingLampPivot.pivot = new Vector2(0.5f, 1f);
            for (int i = 0; i < 4; i++)
            {
                var lRect = CreateAnchoredRect($"FarChain_{i}", farCeilingLampPivot, new Vector2(14f, 26f), new Vector2(0.5f, 1f), new Vector2(0f, -8f - i * 20f));
                lRect.pivot = new Vector2(0.5f, 1f);
                var lImg = lRect.gameObject.AddComponent<Image>();
                lImg.sprite = chainLinkSprite;
                lImg.color = new Color(0.70f, 0.60f, 0.48f, 0.75f);
                lImg.raycastTarget = false;
            }
            var farLampHead = CreateAnchoredRect("Far Lamp Head", farCeilingLampPivot, new Vector2(68f, 92f), new Vector2(0.5f, 1f), new Vector2(0f, -92f));
            farLampHead.pivot = new Vector2(0.5f, 0.88f);
            var farGlow = CreateAnchoredRect("Far Lamp Glow", farLampHead, new Vector2(140f, 140f), new Vector2(0.5f, 0.45f), Vector2.zero);
            var farGlowImg = farGlow.gameObject.AddComponent<Image>();
            farGlowImg.sprite = radialGlowSprite;
            farGlowImg.color = new Color(1.0f, 0.74f, 0.32f, 0.10f);
            farGlowImg.raycastTarget = false;
            var farCageImg = farLampHead.gameObject.AddComponent<Image>();
            farCageImg.sprite = lanternCageSprite;
            farCageImg.preserveAspect = true;
            farCageImg.color = new Color(0.78f, 0.72f, 0.64f, 0.85f);
            farCageImg.raycastTarget = false;
        }

        private void BuildForegroundWanderer(RectTransform parent)
        {
            foregroundLedgeLayer = CreateAnchoredRect("Foreground Ledge & Wanderer", parent, new Vector2(2280f, 1180f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // Crisp 2D Pixel-Art Ruined Archive Stone Floor & Right-Side Pedestal
            var ledgeImg = foregroundLedgeLayer.gameObject.AddComponent<Image>();
            ledgeImg.sprite = CreateForegroundLedgePixelSprite(480, 270);
            ledgeImg.raycastTarget = false;

            // Hero Character Centerpiece on Right Overlook (0.78f) leaving the Center Menu Column (0.50f) completely clear
            var heroSize = heroIdle != null ? new Vector2(420, 420) : new Vector2(360, 390);
            wandererBodyRect = CreateAnchoredRect("Cloaked Wanderer Silhouette", foregroundLedgeLayer, heroSize, new Vector2(0.78f, 0.155f), Vector2.zero);
            // Delivered cells have their shared foot baseline 6.5px above the bottom of 70px.
            wandererBodyRect.pivot = new Vector2(0.5f, heroIdle != null ? 6.5f / 70 : .04f);
            wandererImage = wandererBodyRect.gameObject.AddComponent<Image>();
            wandererImage.sprite = characterHeroSprite;
            wandererImage.preserveAspect = true;
            wandererImage.raycastTarget = false;

            // =================================================================
            // PRIMARY HERO LIGHT: GREAT ARCHIVE LANTERN AT CHARACTER'S HAND
            // =================================================================
            // The fallback anchor is replaced by each delivered frame's flame registration below.
            wandererLanternRect = CreateAnchoredRect("Great Archive Lantern", wandererBodyRect, new Vector2(50f, 50f), new Vector2(0.81f, 0.77f), Vector2.zero);
            wandererLanternRect.pivot = new Vector2(0.5f, 0.5f);
            heroElapsed = 0; heroFrame = -1;
            TickHero(0, false);

            // 1. Soft Warm Golden Backlight behind character so silhouette is crisp
            var backlightRect = CreateAnchoredRect("Character Backlight Aura", wandererLanternRect, new Vector2(320f, 320f), new Vector2(0.5f, 0.5f), new Vector2(-35f, -20f));
            characterBacklightHalo = backlightRect.gameObject.AddComponent<Image>();
            characterBacklightHalo.sprite = radialGlowSprite;
            characterBacklightHalo.color = new Color(1.0f, 0.74f, 0.35f, 0f);
            characterBacklightHalo.raycastTarget = false;

            // 2. Volumetric God Rays radiating from the lantern into the dark archive (localized around wanderer)
            godRays.Clear();
            godRayImages.Clear();
            float[] rayAngles = { -30f, -12f, 8f, 28f, 46f };
            float[] rayWidths = { 150f, 190f, 210f, 170f, 130f };
            for (int i = 0; i < rayAngles.Length; i++)
            {
                var rayRect = CreateAnchoredRect($"VolumetricGodRay_{i}", wandererLanternRect, new Vector2(rayWidths[i], 560f), new Vector2(0.5f, 0.5f), Vector2.zero);
                rayRect.pivot = new Vector2(0.5f, 0.02f);
                rayRect.localEulerAngles = new Vector3(0f, 0f, rayAngles[i]);
                var rayImg = rayRect.gameObject.AddComponent<Image>();
                rayImg.sprite = lightRaySprite;
                rayImg.color = new Color(1.0f, 0.84f, 0.54f, 0f);
                rayImg.raycastTarget = false;
                godRays.Add(rayRect);
                godRayImages.Add(rayImg);
            }

            // 3. Opening Ignition Shockwave Ring (expands dramatically when lamp ignites)
            var shockwaveRect = CreateAnchoredRect("Ignition Shockwave Ring", wandererLanternRect, new Vector2(380f, 380f), new Vector2(0.5f, 0.5f), Vector2.zero);
            lanternIgnitionShockwave = shockwaveRect.gameObject.AddComponent<Image>();
            lanternIgnitionShockwave.sprite = shockwaveRingSprite;
            lanternIgnitionShockwave.color = new Color(1.0f, 0.82f, 0.40f, 0f);
            lanternIgnitionShockwave.raycastTarget = false;

            // 4. Wide Atmospheric Lantern Ambient Illumination Bloom
            var outerHaloRect = CreateAnchoredRect("Hero Lantern Ambient Bloom", wandererLanternRect, new Vector2(440f, 440f), new Vector2(0.5f, 0.5f), Vector2.zero);
            wandererOuterHalo = outerHaloRect.gameObject.AddComponent<Image>();
            wandererOuterHalo.sprite = radialGlowSprite;
            wandererOuterHalo.color = new Color(1.0f, 0.76f, 0.38f, 0f);
            wandererOuterHalo.raycastTarget = false;

            // 5. Warm Amber Mid Radiance
            var midHaloRect = CreateAnchoredRect("Hero Lantern Mid Radiance", wandererLanternRect, new Vector2(200f, 200f), new Vector2(0.5f, 0.5f), Vector2.zero);
            wandererMidHalo = midHaloRect.gameObject.AddComponent<Image>();
            wandererMidHalo.sprite = radialGlowSprite;
            wandererMidHalo.color = new Color(1.0f, 0.82f, 0.45f, 0f);
            wandererMidHalo.raycastTarget = false;

            // 6. Bright Golden-White Inner Flare
            var innerFlareRect = CreateAnchoredRect("Hero Lantern Inner Flare", wandererLanternRect, new Vector2(80f, 80f), new Vector2(0.5f, 0.5f), Vector2.zero);
            wandererInnerFlare = innerFlareRect.gameObject.AddComponent<Image>();
            wandererInnerFlare.sprite = radialGlowSprite;
            wandererInnerFlare.color = new Color(1.0f, 0.96f, 0.82f, 0f);
            wandererInnerFlare.raycastTarget = false;

            // 7. Crisp Pixel-Art Flame Core in Lantern Glass
            var smallFlameRect = CreateAnchoredRect("Wanderer Lantern Core", wandererLanternRect, heroIdle != null ? new Vector2(14, 18) : new Vector2(26, 32), new Vector2(0.5f, 0.5f), Vector2.zero);
            wandererFlame = smallFlameRect.gameObject.AddComponent<Image>();
            wandererFlame.sprite = PixelSprites.Flame != null ? PixelSprites.Flame : diamondSparkSprite;
            wandererFlame.color = new Color(1.0f, 0.95f, 0.76f, 0f);
            wandererFlame.raycastTarget = false;
        }

        private void BuildEmberField(RectTransform parent)
        {
            var layer = CreateStretchRect("Multi-Depth Ember Field", parent);
            for (int i = 0; i < 60; i++)
            {
                float depth = (i % 5 == 0) ? 1f : ((i % 2 == 0) ? 0.55f : 0.22f);
                float size = depth > 0.8f ? 10f : (depth > 0.4f ? 6f : 4f);
                var rect = CreateAnchoredRect($"EmberMote_{i:00}", layer, new Vector2(size, size), Vector2.zero, Vector2.zero);
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = (depth > 0.4f && PixelSprites.Spark != null) ? PixelSprites.Spark : PixelSprites.White;
                img.raycastTarget = false;

                embers.Add(new EmberMote
                {
                    Rect = rect,
                    Img = img,
                    SeedX = Mathf.Repeat(i * 0.6180339f, 1f),
                    SeedY = Mathf.Repeat(i * 0.381966f, 1f),
                    SpeedY = Mathf.Lerp(0.012f, 0.038f, depth) + (i % 4) * 0.003f,
                    DriftAmp = Mathf.Lerp(0.008f, 0.025f, depth),
                    DriftFreq = 0.4f + (i % 7) * 0.09f,
                    Depth = depth
                });
            }
        }

        public void TriggerInteractionSurge(Vector2 buttonCanvasPosition, Vector2 cameraPanTarget, float boost = 1f)
        {
            surgeIntensity = Mathf.Clamp(surgeIntensity + 0.85f * boost, 0f, 1.8f);
            pendulumBoost = Mathf.Clamp(pendulumBoost + 2.4f * boost, -6f, 6f);
            targetButtonCanvasPos = buttonCanvasPosition;
            targetCameraOffset = cameraPanTarget;
        }

        public void SetCameraFocus(Vector2 cameraPanTarget)
        {
            targetCameraOffset = cameraPanTarget;
        }

        public void SetPlayZoom(float zoom01)
        {
            playZoomProgress = Mathf.Clamp01(zoom01);
        }

        public void SetSubViewFocus(bool inSubView)
        {
            isInSubView = inSubView;
        }

        public void TickStage(float dt, float introIgnition, Vector2 normalizedMouse, Vector2 mouseCanvasPosition, bool reducedMotion)
        {
            TickHero(dt, reducedMotion);
            time += dt;
            surgeIntensity = Mathf.MoveTowards(surgeIntensity, 0f, dt * 1.35f);
            pendulumBoost = Mathf.MoveTowards(pendulumBoost, 0f, dt * 2.4f);
            subViewFocusProgress = Mathf.MoveTowards(subViewFocusProgress, isInSubView ? 1f : 0f, dt * 2.8f);

            // Smooth 2D orthographic parallax pan
            Vector2 desiredCam = reducedMotion
                ? Vector2.zero
                : targetCameraOffset + new Vector2(-normalizedMouse.x * 24f, -normalizedMouse.y * 14f);
            currentCameraOffset = Vector2.Lerp(currentCameraOffset, desiredCam, 1f - Mathf.Exp(-dt * 4.5f));
            currentButtonCanvasPos = Vector2.Lerp(currentButtonCanvasPos, targetButtonCanvasPos, 1f - Mathf.Exp(-dt * 10f));

            // Dynamic lantern canvas position (always exact to held lantern)
            Vector2 lanternCanvasPos = GetHeroLanternCanvasPosition();

            // Play Transition Zoom & Sub-View Focal Zoom
            if (cameraRig != null)
            {
                float smoothZoom = playZoomProgress * playZoomProgress * (3f - 2f * playZoomProgress);
                float subViewZoom = Mathf.SmoothStep(0f, 1f, subViewFocusProgress);
                float baseScale = Mathf.Lerp(1.05f, 1.16f, subViewZoom);
                float rigScale = Mathf.Lerp(baseScale, 1.28f, smoothZoom);
                cameraRig.localScale = new Vector3(rigScale, rigScale, 1f);
                Vector2 zoomPan = Vector2.Lerp(Vector2.zero, new Vector2(-lanternCanvasPos.x * 0.40f, -lanternCanvasPos.y * 0.40f), smoothZoom);
                cameraRig.anchoredPosition = zoomPan;
            }

            if (subViewAtmosphericHaze != null)
            {
                float hazeAlpha = Mathf.Lerp(0f, 0.65f, Mathf.SmoothStep(0f, 1f, subViewFocusProgress));
                subViewAtmosphericHaze.color = new Color(0.015f, 0.022f, 0.045f, hazeAlpha);
            }

            // 1. Multi-layer 2D Orthographic Parallax
            if (farVaultLayer != null) farVaultLayer.anchoredPosition = currentCameraOffset * 0.22f;
            if (midPillarLayer != null) midPillarLayer.anchoredPosition = currentCameraOffset * 0.50f;
            if (foregroundLedgeLayer != null) foregroundLedgeLayer.anchoredPosition = currentCameraOffset * 1.0f;

            // 2. Ceiling lamps gentle ambient sway
            float swingAngle = reducedMotion ? 0f : Mathf.Sin(time * 1.25f) * (2.8f + pendulumBoost);
            if (ceilingLampPivot != null)
            {
                ceilingLampPivot.anchoredPosition = currentCameraOffset * 0.55f;
                ceilingLampPivot.localEulerAngles = new Vector3(0f, 0f, swingAngle);
            }
            if (farCeilingLampPivot != null)
            {
                farCeilingLampPivot.anchoredPosition = currentCameraOffset * 0.35f;
                farCeilingLampPivot.localEulerAngles = new Vector3(0f, 0f, -swingAngle * 0.65f);
            }

            // 3. Flame flicker & Organic Breathing
            float flicker = reducedMotion
                ? 0.94f
                : 0.90f + Mathf.Sin(time * 2.9f) * 0.06f + Mathf.Sin(time * 7.7f) * 0.035f + Mathf.Cos(time * 13.5f) * 0.02f;

            // Extinguish factor during transition to game ("fenerin ışığı gidip karanlık çöksün")
            float extinguish = Mathf.Clamp01(1f - playZoomProgress);
            float dyingFlicker = 1f;
            if (playZoomProgress > 0.01f)
            {
                // Dying fluttering tremor as the flame is snuffed out
                float flutter = Mathf.Sin(time * 36f) * 0.35f * Mathf.Sin(playZoomProgress * Mathf.PI);
                dyingFlicker = Mathf.Clamp01((1f - playZoomProgress) + flutter);
            }

            // =================================================================
            // OPENING IGNITION & FLAME EXPANSION ("O LAMBA IŞIĞI AÇILSIN")
            // =================================================================
            float sparkBurst = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.12f, 0.55f, introIgnition)) * Mathf.PI));
            float bloomExpansion = reducedMotion ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.90f, introIgnition));
            float totalMasterLight = (bloomExpansion + sparkBurst * 0.65f + surgeIntensity * 0.40f) * flicker * extinguish * dyingFlicker;

            // Expanding Ignition Shockwave Ring
            if (lanternIgnitionShockwave != null)
            {
                float shockwaveT = Mathf.InverseLerp(0.18f, 0.85f, introIgnition);
                if (shockwaveT > 0f && shockwaveT < 1f && !reducedMotion)
                {
                    float waveScale = Mathf.Lerp(0.10f, 1.6f, shockwaveT);
                    float waveAlpha = Mathf.Sin(shockwaveT * Mathf.PI) * 0.40f;
                    lanternIgnitionShockwave.rectTransform.localScale = new Vector3(waveScale, waveScale, 1f);
                    lanternIgnitionShockwave.color = new Color(1.0f, 0.84f, 0.42f, waveAlpha);
                }
                else
                {
                    lanternIgnitionShockwave.color = Color.clear;
                }
            }

            // Character Backlight Halo (Subtle rim light around wanderer)
            if (characterBacklightHalo != null)
            {
                float backAlpha = Mathf.Clamp01((0.07f + surgeIntensity * 0.04f) * totalMasterLight);
                characterBacklightHalo.color = new Color(1.0f, 0.74f, 0.35f, backAlpha);
            }

            // Ambient Wide Bloom (Soft, localized warm aura)
            if (wandererOuterHalo != null)
            {
                float outerAlpha = Mathf.Clamp01((0.08f + surgeIntensity * 0.04f) * totalMasterLight);
                wandererOuterHalo.color = new Color(1.0f, 0.76f, 0.38f, outerAlpha);
                float bloomScale = (Mathf.Lerp(0.35f, 1.0f, bloomExpansion) + (reducedMotion ? 0f : Mathf.Sin(time * 2.0f) * 0.02f) + surgeIntensity * 0.06f) * extinguish;
                wandererOuterHalo.rectTransform.localScale = new Vector3(bloomScale, bloomScale, 1f);
            }

            // Mid Warm Amber Radiance
            if (wandererMidHalo != null)
            {
                float midAlpha = Mathf.Clamp01((0.20f + surgeIntensity * 0.08f) * totalMasterLight);
                wandererMidHalo.color = new Color(1.0f, 0.82f, 0.45f, midAlpha);
                float midScale = (Mathf.Lerp(0.40f, 1.0f, bloomExpansion) + surgeIntensity * 0.10f) * extinguish;
                wandererMidHalo.rectTransform.localScale = new Vector3(midScale, midScale, 1f);
            }

            // Inner Bright Flare
            if (wandererInnerFlare != null)
            {
                float innerAlpha = Mathf.Clamp01((0.45f + sparkBurst * 0.18f + surgeIntensity * 0.12f) * introIgnition * extinguish * dyingFlicker);
                wandererInnerFlare.color = new Color(1.0f, 0.96f, 0.82f, innerAlpha);
                float innerScale = (1f + sparkBurst * 0.20f + surgeIntensity * 0.12f) * extinguish;
                wandererInnerFlare.rectTransform.localScale = new Vector3(innerScale, innerScale, 1f);
            }

            // Pixel Flame Core
            if (wandererFlame != null)
            {
                float flameScaleY = reducedMotion ? 1f : (0.92f + 0.15f * Mathf.Sin(time * 8.5f));
                float flameScaleX = reducedMotion ? 1f : (0.95f + 0.10f * Mathf.Cos(time * 6.2f));
                float flameScale = extinguish * dyingFlicker;
                wandererFlame.rectTransform.localScale = new Vector3(flameScaleX * flameScale, flameScaleY * flameScale, 1f);
                float flameAlpha = introIgnition > 0.05f ? Mathf.Clamp01(0.98f * introIgnition * extinguish * dyingFlicker) : 0f;
                // Transition flame from radiant gold to dying red ember as it extinguishes
                Color dyingColor = new Color(0.92f, 0.32f, 0.10f, flameAlpha);
                Color normalColor = new Color(1.0f, 0.95f, 0.76f, flameAlpha);
                wandererFlame.color = Color.Lerp(dyingColor, normalColor, extinguish);
            }

            // Volumetric God Rays (localized delicate cone of dust rays around wanderer pedestal)
            for (int i = 0; i < godRays.Count; i++)
            {
                float rayWave = reducedMotion ? 1f : (0.80f + 0.20f * Mathf.Sin(time * 1.5f + i * 1.2f));
                float rayAlpha = Mathf.Clamp01((0.022f + surgeIntensity * 0.012f) * rayWave * bloomExpansion * extinguish);
                godRayImages[i].color = new Color(1.0f, 0.84f, 0.54f, rayAlpha);
            }

            // Top-Down Volumetric Moonlight Rays
            for (int i = 0; i < topGodRays.Count; i++)
            {
                float rayWave = reducedMotion ? 1f : (0.75f + 0.25f * Mathf.Sin(time * 1.3f + i * 1.1f));
                float rayAlpha = Mathf.Clamp01((0.036f + surgeIntensity * 0.015f) * rayWave * bloomExpansion * extinguish);
                topGodRayImages[i].color = new Color(0.68f, 0.80f, 0.96f, rayAlpha);
            }

            // 4. Carved Pillar Ember Veins Steady Amber Glow
            if (pillarVeinGlowImage != null)
            {
                float veinAlpha = Mathf.Clamp01((0.36f + surgeIntensity * 0.35f) * bloomExpansion * extinguish);
                pillarVeinGlowImage.color = new Color(1.0f, 0.68f, 0.22f, veinAlpha);
            }

            // 5. Hero Character Breathing Motion
            if (wandererBodyRect != null)
            {
                float breathY = reducedMotion || heroIdle != null ? 1f : (1f + Mathf.Sin(time * 1.8f) * 0.010f);
                wandererBodyRect.localScale = new Vector3(1f, breathY, 1f);
            }

            // 6. Multi-Depth 2D Pixel Ember & Ash Field
            float speedMult = 1f + surgeIntensity * 0.9f;
            for (int i = 0; i < embers.Count; i++)
            {
                var e = embers[i];
                float y = Mathf.Repeat(e.SeedY + (reducedMotion ? 0f : time * e.SpeedY * speedMult), 1f);
                float x = Mathf.Repeat(e.SeedX + (reducedMotion ? 0f : Mathf.Sin(time * e.DriftFreq + i) * e.DriftAmp), 1f);
                e.Rect.anchorMin = e.Rect.anchorMax = new Vector2(x, y);
                e.Rect.anchoredPosition = currentCameraOffset * e.Depth;

                float life = Mathf.Sin(y * Mathf.PI);
                if (e.Depth > 0.5f)
                {
                    float twinkle = 0.72f + 0.28f * Mathf.Sin(time * 5.5f + i * 1.7f);
                    e.Img.color = new Color(1.0f, 0.72f, 0.24f, Mathf.Clamp01((0.60f + surgeIntensity * 0.3f) * life * twinkle * introIgnition * extinguish));
                }
                else
                {
                    e.Img.color = new Color(0.75f, 0.60f, 0.44f, Mathf.Clamp01(0.20f * life * introIgnition * extinguish));
                }
            }

            // 8. Mouse Lantern Spotlight
            if (cursorLanternRect != null && cursorLanternGlow != null)
            {
                cursorLanternRect.anchoredPosition = Vector2.Lerp(cursorLanternRect.anchoredPosition, mouseCanvasPosition, 1f - Mathf.Exp(-dt * 9f));
                cursorLanternGlow.color = new Color(1.0f, 0.68f, 0.25f, (0.09f + surgeIntensity * 0.05f) * introIgnition * extinguish);
            }

            lastIntroIgnition = introIgnition;
        }

        private void TickHero(float dt, bool reducedMotion)
        {
            if (heroIdle == null || wandererImage == null) return;
            if (!reducedMotion) heroElapsed += Mathf.Max(0, dt);
            if (heroIdle.Duration > 0) heroElapsed %= heroIdle.Duration;
            int frame = heroIdle.FrameAt(heroElapsed);
            if (frame < 0 || frame == heroFrame) return;
            heroFrame = frame;
            var sprite = heroIdle.Frames[frame];
            wandererImage.sprite = sprite;
            if (wandererLanternRect != null && sprite != null && heroIdle.LanternOffsets != null && frame < heroIdle.LanternOffsets.Length)
            {
                var pixel = heroIdle.LanternOffsets[frame] * sprite.pixelsPerUnit + sprite.pivot;
                var anchor = new Vector2(pixel.x / sprite.rect.width, pixel.y / sprite.rect.height);
                wandererLanternRect.anchorMin = wandererLanternRect.anchorMax = anchor;
                wandererLanternRect.anchoredPosition = Vector2.zero;
            }
        }

        private Vector2 GetHeroLanternCanvasPosition()
        {
            if (wandererLanternRect != null && parentCanvasRectTransform != null)
            {
                Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, wandererLanternRect.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvasRectTransform, screenPt, null, out Vector2 localPt))
                {
                    return localPt;
                }
            }
            return new Vector2(465f, -60f);
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
        // 2D PIXEL-ART & PROCEDURAL SPRITE GENERATORS
        // =====================================================================

        private Sprite RegisterSprite(Texture2D tex, float ppu = 100f)
        {
            tex.Apply();
            generatedTextures.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, ppu);
            generatedSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateSkyGradientSprite(int w, int h)
        {
            var tex = NewTexture(w, h, FilterMode.Bilinear);
            Color topObsidian = new Color(0.024f, 0.020f, 0.030f, 1f);
            Color midStone = new Color(0.065f, 0.050f, 0.045f, 1f);
            Color lowerAbyss = new Color(0.020f, 0.016f, 0.022f, 1f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                Color vertical = ny > 0.42f
                    ? Color.Lerp(midStone, topObsidian, (ny - 0.42f) / 0.58f)
                    : Color.Lerp(lowerAbyss, midStone, ny / 0.42f);

                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, vertical);
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateArchiveColonnadePixelSprite(int w, int h)
        {
            var tex = NewTexture(w, h, FilterMode.Point);
            Color stoneDark = new Color(0.055f, 0.042f, 0.038f, 0.96f);
            Color stoneBrick = new Color(0.082f, 0.064f, 0.052f, 0.96f);
            Color shelfWood = new Color(0.095f, 0.068f, 0.048f, 0.88f);
            Color bookToneA = new Color(0.16f, 0.11f, 0.07f, 0.88f);
            Color bookToneB = new Color(0.12f, 0.09f, 0.06f, 0.88f);

            const int archCount = 4;
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                int brickRow = y / 10;
                bool isMortarH = (y % 10 == 0);

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float cell = nx * archCount;
                    float localX = (cell - Mathf.Floor(cell)) * 2f - 1f;
                    float absX = Mathf.Abs(localX);

                    float qAbsX = Mathf.Floor(absX * 24f) / 24f;
                    float archHeight = 0.78f - 0.36f * (qAbsX * qAbsX + 0.32f * qAbsX);
                    bool isPillarOrVault = absX > 0.76f || ny > archHeight;

                    if (isPillarOrVault)
                    {
                        int brickOffset = (brickRow % 2) * 9;
                        bool isMortarV = ((x + brickOffset) % 18 == 0);
                        tex.SetPixel(x, y, (isMortarH || isMortarV) ? stoneDark : stoneBrick);
                    }
                    else
                    {
                        if (ny < 0.64f && absX > 0.18f)
                        {
                            int shelfY = y % 18;
                            if (shelfY < 3 || (x % 28 < 3))
                            {
                                tex.SetPixel(x, y, shelfWood);
                            }
                            else if (shelfY < 14 && (x % 5 != 0))
                            {
                                int bookIdx = (x / 5 + y / 18) % 3;
                                int bookHeight = 10 + (bookIdx * 2);
                                tex.SetPixel(x, y, shelfY < bookHeight ? (bookIdx == 0 ? bookToneA : bookToneB) : Color.clear);
                            }
                            else
                            {
                                tex.SetPixel(x, y, Color.clear);
                            }
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
            var pillarTex = NewTexture(w, h, FilterMode.Point);
            var veinTex = NewTexture(w, h, FilterMode.Point);

            Color pillarDark = new Color(0.035f, 0.026f, 0.024f, 0.98f);
            Color pillarMid = new Color(0.068f, 0.050f, 0.040f, 0.98f);

            float[] pillarCenters = { 0.05f, 0.16f, 0.84f, 0.95f };
            float[] pillarWidths = { 0.042f, 0.030f, 0.030f, 0.045f };

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                bool mortarRow = (y % 12 == 0);

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    Color pOut = Color.clear;
                    Color vOut = Color.clear;

                    for (int i = 0; i < pillarCenters.Length; i++)
                    {
                        float dx = nx - pillarCenters[i];
                        float pw = pillarWidths[i];
                        if (ny > 0.84f) pw += Mathf.Floor((ny - 0.84f) * 20f) * 0.006f;
                        if (ny < 0.14f) pw += Mathf.Floor((0.14f - ny) * 20f) * 0.005f;

                        if (Mathf.Abs(dx) <= pw)
                        {
                            pOut = mortarRow ? pillarDark : pillarMid;
                            float qNy = Mathf.Floor(ny * 64f) / 64f;
                            float veinPath = Mathf.Sin(qNy * 16f + i * 2.1f) * (pw * 0.35f);
                            float d1 = Mathf.Abs(dx - veinPath);
                            if (d1 < 0.0045f && ny > 0.14f && ny < 0.84f)
                            {
                                vOut = new Color(1f, 1f, 1f, Mathf.Sin(ny * Mathf.PI));
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

        private Sprite CreateForegroundLedgePixelSprite(int w, int h)
        {
            var tex = NewTexture(w, h, FilterMode.Point);
            Color deepShadow = new Color(0.018f, 0.014f, 0.018f, 1f);
            Color stoneBody = new Color(0.11f, 0.082f, 0.062f, 1f);
            Color stoneTopCap = new Color(0.24f, 0.18f, 0.12f, 1f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                bool brickLineH = (y % 9 == 0);

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    bool brickLineV = ((x + (y / 9) * 7) % 16 == 0);

                    float floorH = 0.095f;
                    float pedestalH = (nx >= 0.62f && nx <= 0.94f) ? ((nx >= 0.65f && nx <= 0.90f) ? 0.155f : 0.125f) : 0f;
                    float bookStackH = (nx >= 0.675f && nx <= 0.715f) ? 0.195f : 0f;
                    float maxH = Mathf.Max(floorH, pedestalH);

                    if (ny <= maxH)
                    {
                        float distFromTop = maxH - ny;
                        if (distFromTop < 0.014f)
                        {
                            tex.SetPixel(x, y, stoneTopCap);
                        }
                        else if (distFromTop < 0.055f && !brickLineH && !brickLineV)
                        {
                            tex.SetPixel(x, y, stoneBody);
                        }
                        else
                        {
                            tex.SetPixel(x, y, deepShadow);
                        }
                    }
                    else if (bookStackH > 0f && ny <= bookStackH)
                    {
                        bool pageStripe = (y % 4 == 2);
                        tex.SetPixel(x, y, pageStripe ? new Color(0.65f, 0.52f, 0.34f, 1f) : new Color(0.22f, 0.14f, 0.09f, 1f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateFallbackCharacterSprite()
        {
            int w = 50;
            int h = 54;
            var tex = NewTexture(w, h, FilterMode.Point);
            Color outline = new Color(0.04f, 0.04f, 0.05f, 1f);
            Color coat = new Color(0.28f, 0.28f, 0.30f, 1f);
            Color pants = new Color(0.14f, 0.14f, 0.16f, 1f);
            Color skin = new Color(0.95f, 0.95f, 0.95f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }

            for (int y = 38; y <= 50; y++)
            {
                for (int x = 22; x <= 31; x++)
                {
                    bool face = (x >= 26 && x <= 30 && y >= 40 && y <= 46);
                    tex.SetPixel(x, y, face ? skin : outline);
                }
            }
            for (int y = 24; y < 38; y++)
            {
                for (int x = 20; x <= 32; x++)
                {
                    bool edge = (x == 20 || x == 32 || y == 24 || y == 37);
                    tex.SetPixel(x, y, edge ? outline : coat);
                }
            }
            for (int y = 37; y <= 43; y++)
            {
                for (int x = 32; x <= 37; x++)
                {
                    tex.SetPixel(x, y, (y == 37 || y == 43) ? outline : skin);
                }
            }
            for (int y = 36; y <= 48; y++)
            {
                for (int x = 37; x <= 43; x++)
                {
                    bool glass = (x >= 39 && x <= 41 && y >= 38 && y <= 44);
                    tex.SetPixel(x, y, glass ? skin : outline);
                }
            }
            for (int y = 2; y < 24; y++)
            {
                int leftLegX = 16 + (y - 2) / 3;
                for (int x = leftLegX; x <= leftLegX + 5; x++)
                {
                    tex.SetPixel(x, y, (x == leftLegX || x == leftLegX + 5 || y < 6) ? outline : pants);
                }
                for (int x = 27; x <= 32; x++)
                {
                    tex.SetPixel(x, y, (x == 27 || x == 32 || y < 6) ? outline : pants);
                }
            }

            return RegisterSprite(tex, 32f);
        }

        private Sprite CreateLanternCageSprite(int w, int h)
        {
            var tex = NewTexture(w, h, FilterMode.Point);
            Color iron = new Color(0.14f, 0.16f, 0.20f, 0.98f);
            Color warmGlass = new Color(1.0f, 0.82f, 0.42f, 0.95f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float ax = Mathf.Abs(nx);
                    Color c = Color.clear;

                    if (ny >= 0.68f && ny <= 0.88f && ax <= Mathf.Lerp(0.55f, 0.12f, (ny - 0.68f) / 0.20f))
                    {
                        c = iron;
                    }
                    else if (ny >= 0.22f && ny < 0.68f && ax <= 0.42f)
                    {
                        bool bar = ax > 0.32f || (ax > 0.08f && ax < 0.16f);
                        c = bar ? iron : warmGlass;
                    }
                    else if (ny >= 0.08f && ny < 0.22f && ax <= 0.26f)
                    {
                        c = iron;
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateChainLinkSprite(int w, int h)
        {
            var tex = NewTexture(w, h, FilterMode.Point);
            Color iron = new Color(0.22f, 0.24f, 0.28f, 0.95f);
            for (int y = 0; y < h; y++)
            {
                float ny = Mathf.Abs((y / (float)(h - 1)) * 2f - 1f);
                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs((x / (float)(w - 1)) * 2f - 1f);
                    bool ring = (nx < 0.65f && ny < 0.88f) && (nx > 0.32f || ny > 0.62f);
                    tex.SetPixel(x, y, ring ? iron : Color.clear);
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateShockwaveRingSprite(int size)
        {
            var tex = NewTexture(size, size, FilterMode.Bilinear);
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxR = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.68f) / 0.14f, 2f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateRadialGlowSprite(int size)
        {
            var tex = NewTexture(size, size, FilterMode.Bilinear);
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxR = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateLightRaySprite(int w, int h)
        {
            var tex = NewTexture(w, h, FilterMode.Bilinear);
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                float halfWidth = Mathf.Lerp(0.08f, 0.48f, ny);
                float verticalFade = Mathf.Pow(1f - ny, 0.7f) * Mathf.Sin((1f - ny) * Mathf.PI);

                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs((x / (float)(w - 1)) - 0.5f);
                    float horizFade = Mathf.Pow(Mathf.Clamp01(1f - nx / halfWidth), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, verticalFade * horizFade));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateDiamondSparkSprite(int size)
        {
            var tex = NewTexture(size, size, FilterMode.Point);
            for (int y = 0; y < size; y++)
            {
                float ny = Mathf.Abs((y / (float)(size - 1)) * 2f - 1f);
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs((x / (float)(size - 1)) * 2f - 1f);
                    float star = Mathf.Clamp01(1f - (nx * 0.85f + ny * 0.85f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(star)));
                }
            }
            return RegisterSprite(tex);
        }

        public Sprite CreateOrnateFiligreeSprite(int w = 512, int h = 32)
        {
            var tex = NewTexture(w, h, FilterMode.Bilinear);
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f;
                float ay = Mathf.Abs(ny);
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float ax = Mathf.Abs(nx);

                    // Ethereal tapered light blade: vertically robust core with smooth horizontal falloff
                    float core = Mathf.Clamp01(1f - ay * 1.5f) * Mathf.Clamp01(1f - ax * ax);
                    float softGlow = Mathf.Pow(Mathf.Clamp01(1f - ay), 2.0f) * Mathf.Pow(Mathf.Clamp01(1f - ax), 1.2f) * 0.35f;

                    float alpha = Mathf.Clamp01(core + softGlow);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            return RegisterSprite(tex);
        }

        private static Texture2D NewTexture(int w, int h, FilterMode filterMode = FilterMode.Bilinear)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = filterMode
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
