using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Pure-code procedural 2.5D animated background stage for The Lighten Road Main Menu.
    /// Renders a deep midnight-indigo ruined archive with parallax gothic arches,
    /// Ember-Vein-carved pillars, a physics-inspired swinging wrought-iron lantern
    /// with volumetric god-rays, a cloaked wanderer holding a lantern on a foreground ledge,
    /// multi-depth ember particles, and a live Bezier energy stream flowing into the active menu item.
    /// </summary>
    public sealed class ProceduralArchiveStage : MonoBehaviour
    {
        private RectTransform stageRoot;
        private RectTransform cameraRig;
        private RectTransform farVaultLayer;
        private RectTransform midPillarLayer;
        private RectTransform pendulumPivot;
        private RectTransform lanternHeadRect;
        private RectTransform foregroundLedgeLayer;
        private RectTransform wandererBodyRect;
        private RectTransform wandererLanternRect;

        private Image skyImage;
        private Image farVaultImage;
        private Image midPillarImage;
        private Image pillarVeinGlowImage;
        private Image lanternOuterHalo;
        private Image lanternMidHalo;
        private Image lanternFlameCore;
        private Image wandererHalo;
        private Image wandererFlame;
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
            public float Depth; // 0 = far dust, 0.5 = mid ember, 1 = foreground bright spark
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
        private Vector2 targetButtonCanvasPos = new Vector2(0f, -120f);
        private Vector2 currentButtonCanvasPos = new Vector2(0f, -120f);
        private bool hasActiveButtonTarget;

        public void BuildStage(RectTransform parentCanvasRect)
        {
            stageRoot = CreateStretchRect("Procedural Archive Stage", parentCanvasRect);
            stageRoot.SetAsFirstSibling();

            // Generate reusable procedural sprites
            radialGlowSprite = CreateRadialGlowSprite(256);
            lightRaySprite = CreateLightRaySprite(128, 384);
            lanternCageSprite = CreateLanternCageSprite(256, 320);
            chainLinkSprite = CreateChainLinkSprite(48, 84);
            diamondSparkSprite = CreateDiamondSparkSprite(64);

            // 1. Deep Midnight Indigo Sky & Vault Gradient (Static Fullscreen Base)
            var skyRect = CreateStretchRect("Midnight Sky Gradient", stageRoot);
            skyImage = skyRect.gameObject.AddComponent<Image>();
            skyImage.sprite = CreateSkyGradientSprite(256, 512);
            skyImage.raycastTarget = false;

            // Camera Rig (oversized slightly to allow smooth camera pan & zoom without exposing edges)
            cameraRig = CreateStretchRect("Stage Camera Rig", stageRoot);
            cameraRig.localScale = Vector3.one * 1.06f;

            // 2. Far Layer: Gothic Pointed Arches, Rose Tracery & Ruined Archive Colonnade
            farVaultLayer = CreateAnchoredRect("Far Gothic Colonnade", cameraRig, new Vector2(2240f, 1180f), new Vector2(0.5f, 0.5f), Vector2.zero);
            farVaultImage = farVaultLayer.gameObject.AddComponent<Image>();
            farVaultImage.sprite = CreateGothicColonnadeSprite(1024, 540);
            farVaultImage.raycastTarget = false;

            // Subtle moonlit mist veil between far and mid layers
            var mistRect = CreateAnchoredRect("Midnight Mist Veil", cameraRig, new Vector2(2400f, 680f), new Vector2(0.5f, 0.25f), Vector2.zero);
            var mistImg = mistRect.gameObject.AddComponent<Image>();
            mistImg.sprite = radialGlowSprite;
            mistImg.color = new Color(0.08f, 0.16f, 0.30f, 0.28f);
            mistImg.raycastTarget = false;

            // 3. Mid Layer: Ruined Archive Pillars & Carved Glowing Ember Veins
            midPillarLayer = CreateAnchoredRect("Mid Archive Pillars", cameraRig, new Vector2(2160f, 1160f), new Vector2(0.5f, 0.5f), Vector2.zero);
            CreatePillarAndVeinSprites(1024, 540, out Sprite pillarSprite, out Sprite veinGlowSprite);

            midPillarImage = midPillarLayer.gameObject.AddComponent<Image>();
            midPillarImage.sprite = pillarSprite;
            midPillarImage.raycastTarget = false;

            var veinRect = CreateStretchRect("Carved Ember Veins Glow", midPillarLayer);
            pillarVeinGlowImage = veinRect.gameObject.AddComponent<Image>();
            pillarVeinGlowImage.sprite = veinGlowSprite;
            pillarVeinGlowImage.color = new Color(1f, 0.68f, 0.22f, 0.65f);
            pillarVeinGlowImage.raycastTarget = false;

            // 4. Swinging Pendulum Great Lantern (Upper-Right Vault Focal Point)
            BuildSwingingLantern(cameraRig);

            // 5. Interactive Ember Vein Energy Stream (From Swinging Lantern -> Hovered Menu Button)
            BuildEnergyTether(cameraRig);

            // 6. Foreground Layer: Ruined Balustrade Ledge & Cloaked Wanderer with Handheld Lantern
            BuildForegroundWanderer(cameraRig);

            // 7. Multi-Depth Floating Embers, Rising Sparks & Midnight Ash
            BuildEmberField(stageRoot);

            // 8. Interactive Mouse-Following Lantern Glow
            cursorLanternRect = CreateAnchoredRect("Mouse Lantern Spotlight", stageRoot, new Vector2(540f, 540f), new Vector2(0.5f, 0.5f), Vector2.zero);
            cursorLanternGlow = cursorLanternRect.gameObject.AddComponent<Image>();
            cursorLanternGlow.sprite = radialGlowSprite;
            cursorLanternGlow.color = new Color(1.0f, 0.66f, 0.24f, 0.11f);
            cursorLanternGlow.raycastTarget = false;
        }

        private void BuildSwingingLantern(RectTransform parent)
        {
            // Pivot high in the upper-right gothic arch
            pendulumPivot = CreateAnchoredRect("Pendulum Lantern Pivot", parent, new Vector2(40f, 40f), new Vector2(0.75f, 1.03f), Vector2.zero);
            pendulumPivot.pivot = new Vector2(0.5f, 1f);

            // Chain links descending from the vault
            const int chainCount = 6;
            for (int i = 0; i < chainCount; i++)
            {
                var linkRect = CreateAnchoredRect($"ChainLink_{i}", pendulumPivot, new Vector2(22f, 44f), new Vector2(0.5f, 1f), new Vector2(0f, -12f - i * 34f));
                linkRect.pivot = new Vector2(0.5f, 1f);
                var linkImg = linkRect.gameObject.AddComponent<Image>();
                linkImg.sprite = chainLinkSprite;
                linkImg.color = new Color(0.85f, 0.72f, 0.52f, 0.9f);
                linkImg.raycastTarget = false;
            }

            // Lantern Head at the end of the chain
            lanternHeadRect = CreateAnchoredRect("Great Archive Lantern", pendulumPivot, new Vector2(180f, 225f), new Vector2(0.5f, 1f), new Vector2(0f, -212f));
            lanternHeadRect.pivot = new Vector2(0.5f, 0.85f);

            // Volumetric God-Rays radiating from behind the lantern
            float[] rayAngles = { -38f, -18f, 2f, 22f, 42f };
            float[] rayWidths = { 220f, 310f, 360f, 280f, 200f };
            for (int i = 0; i < rayAngles.Length; i++)
            {
                var rayRect = CreateAnchoredRect($"VolumetricGodRay_{i}", lanternHeadRect, new Vector2(rayWidths[i], 1150f), new Vector2(0.5f, 0.48f), Vector2.zero);
                rayRect.pivot = new Vector2(0.5f, 1f);
                rayRect.localEulerAngles = new Vector3(0f, 0f, rayAngles[i]);
                var rayImg = rayRect.gameObject.AddComponent<Image>();
                rayImg.sprite = lightRaySprite;
                rayImg.color = new Color(1.0f, 0.72f, 0.28f, 0.09f);
                rayImg.raycastTarget = false;
                godRays.Add(rayRect);
                godRayImages.Add(rayImg);
            }

            // Wide Outer Atmospheric Halo
            var outerHaloRect = CreateAnchoredRect("Pedestal Lantern Outer", lanternHeadRect, new Vector2(960f, 960f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternOuterHalo = outerHaloRect.gameObject.AddComponent<Image>();
            lanternOuterHalo.sprite = radialGlowSprite;
            lanternOuterHalo.color = new Color(0.98f, 0.56f, 0.16f, 0.26f);
            lanternOuterHalo.raycastTarget = false;

            // Warm Amber Mid Halo
            var midHaloRect = CreateAnchoredRect("Pedestal Lantern Core", lanternHeadRect, new Vector2(420f, 420f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternMidHalo = midHaloRect.gameObject.AddComponent<Image>();
            lanternMidHalo.sprite = radialGlowSprite;
            lanternMidHalo.color = new Color(1.0f, 0.78f, 0.34f, 0.48f);
            lanternMidHalo.raycastTarget = false;

            // Bright White-Gold Flame Heart
            var coreRect = CreateAnchoredRect("Lantern Flame Heart", lanternHeadRect, new Vector2(135f, 165f), new Vector2(0.5f, 0.45f), Vector2.zero);
            lanternFlameCore = coreRect.gameObject.AddComponent<Image>();
            lanternFlameCore.sprite = radialGlowSprite;
            lanternFlameCore.color = new Color(1.0f, 0.95f, 0.78f, 0.92f);
            lanternFlameCore.raycastTarget = false;

            // Ornate Wrought-Iron Lantern Cage Silhouette on top of the flame
            var cageRect = CreateStretchRect("Wrought Iron Cage", lanternHeadRect);
            var cageImg = cageRect.gameObject.AddComponent<Image>();
            cageImg.sprite = lanternCageSprite;
            cageImg.raycastTarget = false;
        }

        private void BuildEnergyTether(RectTransform parent)
        {
            var tetherContainer = CreateStretchRect("Ember Vein Energy Tether", parent);
            const int nodeCount = 22;
            for (int i = 0; i < nodeCount; i++)
            {
                float norm = i / (float)(nodeCount - 1);
                float size = Mathf.Lerp(18f, 9f, norm);
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

            // Ledge & Ruined Balustrade on bottom-left and bottom-right corners
            var ledgeImg = foregroundLedgeLayer.gameObject.AddComponent<Image>();
            ledgeImg.sprite = CreateForegroundLedgeSprite(1024, 540);
            ledgeImg.raycastTarget = false;

            // Cloaked Wanderer standing on the left stone overlook
            wandererBodyRect = CreateAnchoredRect("Cloaked Wanderer Silhouette", foregroundLedgeLayer, new Vector2(340f, 420f), new Vector2(0.20f, 0.15f), Vector2.zero);
            wandererBodyRect.pivot = new Vector2(0.5f, 0.1f);
            var wandererImg = wandererBodyRect.gameObject.AddComponent<Image>();
            wandererImg.sprite = CreateWandererSprite(256, 320);
            wandererImg.raycastTarget = false;

            // Wanderer's Handheld Small Lantern Glow (at outstretched right hand)
            wandererLanternRect = CreateAnchoredRect("Wanderer Handheld Lantern", wandererBodyRect, new Vector2(260f, 260f), new Vector2(0.78f, 0.54f), Vector2.zero);
            wandererHalo = wandererLanternRect.gameObject.AddComponent<Image>();
            wandererHalo.sprite = radialGlowSprite;
            wandererHalo.color = new Color(1.0f, 0.68f, 0.24f, 0.42f);
            wandererHalo.raycastTarget = false;

            var smallFlameRect = CreateAnchoredRect("Wanderer Lantern Core", wandererLanternRect, new Vector2(54f, 54f), new Vector2(0.5f, 0.5f), Vector2.zero);
            wandererFlame = smallFlameRect.gameObject.AddComponent<Image>();
            wandererFlame.sprite = diamondSparkSprite;
            wandererFlame.color = new Color(1.0f, 0.92f, 0.72f, 0.95f);
            wandererFlame.raycastTarget = false;
        }

        private void BuildEmberField(RectTransform parent)
        {
            var layer = CreateStretchRect("Multi-Depth Ember Field", parent);
            for (int i = 0; i < 64; i++)
            {
                float depth = (i % 5 == 0) ? 1f : ((i % 2 == 0) ? 0.55f : 0.2f);
                float size = depth > 0.8f ? 11f : (depth > 0.4f ? 6.5f : 3.2f);
                var rect = CreateAnchoredRect($"EmberMote_{i:00}", layer, new Vector2(size, size), Vector2.zero, Vector2.zero);
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = (depth > 0.8f && i % 2 == 0) ? diamondSparkSprite : radialGlowSprite;
                img.raycastTarget = false;

                embers.Add(new EmberMote
                {
                    Rect = rect,
                    Img = img,
                    SeedX = Mathf.Repeat(i * 0.6180339f, 1f),
                    SeedY = Mathf.Repeat(i * 0.381966f, 1f),
                    SpeedY = Mathf.Lerp(0.012f, 0.036f, depth) + (i % 4) * 0.003f,
                    DriftAmp = Mathf.Lerp(0.008f, 0.026f, depth),
                    DriftFreq = 0.4f + (i % 7) * 0.09f,
                    Depth = depth
                });
            }
        }

        /// <summary>
        /// Triggers a lantern flare surge, pendulum impulse, and focuses the energy stream on a target UI position.
        /// </summary>
        public void TriggerInteractionSurge(Vector2 buttonCanvasPosition, Vector2 cameraPanTarget, float boost = 1f)
        {
            surgeIntensity = Mathf.Clamp(surgeIntensity + 0.85f * boost, 0f, 1.6f);
            pendulumBoost = Mathf.Clamp(pendulumBoost + 3.2f * boost, -7f, 7f);
            targetButtonCanvasPos = buttonCanvasPosition;
            targetCameraOffset = cameraPanTarget;
            hasActiveButtonTarget = true;
        }

        public void SetCameraFocus(Vector2 cameraPanTarget)
        {
            targetCameraOffset = cameraPanTarget;
        }

        public void TickStage(float dt, float introIgnition, Vector2 normalizedMouse, Vector2 mouseCanvasPosition, bool reducedMotion)
        {
            time += dt;
            surgeIntensity = Mathf.MoveTowards(surgeIntensity, 0f, dt * 1.35f);
            pendulumBoost = Mathf.MoveTowards(pendulumBoost, 0f, dt * 2.4f);

            // Smooth camera pan toward hovered button + mouse parallax
            Vector2 desiredCam = reducedMotion
                ? Vector2.zero
                : targetCameraOffset + new Vector2(-normalizedMouse.x * 22f, -normalizedMouse.y * 14f);
            currentCameraOffset = Vector2.Lerp(currentCameraOffset, desiredCam, 1f - Mathf.Exp(-dt * 4.2f));
            currentButtonCanvasPos = Vector2.Lerp(currentButtonCanvasPos, targetButtonCanvasPos, 1f - Mathf.Exp(-dt * 10f));

            // 1. Multi-layer 2.5D Parallax Offsets
            if (farVaultLayer != null)
            {
                farVaultLayer.anchoredPosition = currentCameraOffset * 0.45f;
            }
            if (midPillarLayer != null)
            {
                midPillarLayer.anchoredPosition = currentCameraOffset * 0.85f;
            }
            if (foregroundLedgeLayer != null)
            {
                foregroundLedgeLayer.anchoredPosition = currentCameraOffset * 1.35f;
            }

            // 2. Swinging Pendulum Lantern Physics & Flicker
            float swingAngle = reducedMotion
                ? 0f
                : Mathf.Sin(time * 1.25f) * (4.2f + pendulumBoost) + Mathf.Cos(time * 0.62f) * 1.4f - normalizedMouse.x * 2.5f;

            if (pendulumPivot != null)
            {
                pendulumPivot.anchoredPosition = currentCameraOffset * 0.95f;
                pendulumPivot.localEulerAngles = new Vector3(0f, 0f, swingAngle);
            }

            float flicker = reducedMotion
                ? 0.92f
                : 0.88f + Mathf.Sin(time * 2.7f) * 0.07f + Mathf.Sin(time * 7.3f) * 0.04f + Mathf.Cos(time * 14.1f) * 0.02f;
            float totalGlow = Mathf.Clamp(flicker + surgeIntensity * 0.48f, 0f, 1.65f) * introIgnition;

            if (lanternOuterHalo != null)
            {
                lanternOuterHalo.color = new Color(0.98f, 0.56f, 0.16f, Mathf.Clamp01(0.26f * totalGlow));
                float s = 1f + surgeIntensity * 0.18f + (reducedMotion ? 0f : Mathf.Sin(time * 1.8f) * 0.04f);
                lanternOuterHalo.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (lanternMidHalo != null)
            {
                lanternMidHalo.color = new Color(1.0f, 0.78f, 0.34f, Mathf.Clamp01(0.50f * totalGlow));
            }
            if (lanternFlameCore != null)
            {
                lanternFlameCore.color = new Color(1.0f, 0.96f, 0.80f, Mathf.Clamp01(0.95f * totalGlow));
                float coreScale = 1f + surgeIntensity * 0.25f + (reducedMotion ? 0f : Mathf.Sin(time * 5.2f) * 0.07f);
                lanternFlameCore.rectTransform.localScale = new Vector3(coreScale, coreScale, 1f);
            }

            // God-rays sway & pulse with lantern surge
            for (int i = 0; i < godRayImages.Count; i++)
            {
                float rayWave = reducedMotion ? 1f : (0.75f + 0.25f * Mathf.Sin(time * 1.4f + i * 1.1f));
                float alpha = (0.075f + surgeIntensity * 0.065f) * rayWave * introIgnition;
                godRayImages[i].color = new Color(1.0f, 0.74f, 0.30f, Mathf.Clamp01(alpha));
            }

            // 3. Carved Pillar Ember Veins Pulse
            if (pillarVeinGlowImage != null)
            {
                float veinPulse = reducedMotion ? 0.55f : (0.48f + 0.20f * Mathf.Sin(time * 1.9f));
                float veinAlpha = Mathf.Clamp01((veinPulse + surgeIntensity * 0.45f) * introIgnition);
                pillarVeinGlowImage.color = new Color(1.0f, 0.68f, 0.22f, veinAlpha);
            }

            // 4. Foreground Cloaked Wanderer Breathing & Handheld Lantern
            if (wandererBodyRect != null)
            {
                float breathY = reducedMotion ? 1f : (1f + Mathf.Sin(time * 1.6f) * 0.012f);
                float cloakSway = reducedMotion ? 0f : Mathf.Sin(time * 2.1f) * 0.8f;
                wandererBodyRect.localScale = new Vector3(1f, breathY, 1f);
                wandererBodyRect.localEulerAngles = new Vector3(0f, 0f, cloakSway);
            }
            if (wandererHalo != null)
            {
                wandererHalo.color = new Color(1.0f, 0.68f, 0.24f, Mathf.Clamp01((0.40f + surgeIntensity * 0.22f) * flicker * introIgnition));
            }
            if (wandererFlame != null)
            {
                wandererFlame.rectTransform.localEulerAngles = new Vector3(0f, 0f, time * 24f);
            }

            // 5. Interactive Ember Vein Energy Stream (From Great Lantern to Active Button)
            UpdateEnergyTether(introIgnition, reducedMotion);

            // 6. Floating Ember & Ash Field
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
                    float twinkle = 0.7f + 0.3f * Mathf.Sin(time * 5.5f + i * 1.7f);
                    e.Img.color = new Color(1.0f, 0.72f, 0.26f, Mathf.Clamp01((0.55f + surgeIntensity * 0.3f) * life * twinkle * introIgnition));
                }
                else
                {
                    e.Img.color = new Color(0.55f, 0.68f, 0.88f, Mathf.Clamp01(0.18f * life * introIgnition));
                }
            }

            // 7. Mouse Lantern Spotlight
            if (cursorLanternRect != null && cursorLanternGlow != null)
            {
                cursorLanternRect.anchoredPosition = Vector2.Lerp(cursorLanternRect.anchoredPosition, mouseCanvasPosition, 1f - Mathf.Exp(-dt * 9f));
                cursorLanternGlow.color = new Color(1.0f, 0.68f, 0.25f, (0.12f + surgeIntensity * 0.06f) * introIgnition);
            }
        }

        private void UpdateEnergyTether(float introIgnition, bool reducedMotion)
        {
            if (!hasActiveButtonTarget || tetherNodes.Count == 0)
            {
                return;
            }

            // Lantern flame world-like position in canvas coordinates (~ upper-right (460, 260))
            float swingRad = pendulumPivot != null ? pendulumPivot.localEulerAngles.z * Mathf.Deg2Rad : 0f;
            Vector2 startPos = new Vector2(480f + Mathf.Sin(swingRad) * 280f, 265f);
            Vector2 endPos = currentButtonCanvasPos;
            Vector2 controlPos = (startPos + endPos) * 0.5f + new Vector2(-120f, 110f);

            float streamVisibility = Mathf.Clamp01((0.32f + surgeIntensity * 0.68f) * introIgnition);

            for (int i = 0; i < tetherNodes.Count; i++)
            {
                var node = tetherNodes[i];
                float flowT = reducedMotion
                    ? node.Phase
                    : Mathf.Repeat(node.Phase + time * (0.45f + surgeIntensity * 0.35f), 1f);

                // Quadratic Bezier evaluation
                float inv = 1f - flowT;
                Vector2 pos = inv * inv * startPos + 2f * inv * flowT * controlPos + flowT * flowT * endPos;
                // Subtle sine wave undulation along the vein
                float wave = reducedMotion ? 0f : Mathf.Sin(flowT * 10f - time * 5f) * 8f * Mathf.Sin(flowT * Mathf.PI);
                pos.y += wave;

                node.Rect.anchoredPosition = pos;
                float edgeFade = Mathf.Sin(flowT * Mathf.PI);
                float alpha = streamVisibility * edgeFade;
                node.Img.color = new Color(1.0f, 0.78f, 0.32f, alpha);
                float pulseScale = 0.75f + 0.5f * edgeFade + surgeIntensity * 0.25f;
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

        private Sprite CreateSkyGradientSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color topVault = new Color(0.018f, 0.030f, 0.068f, 1f);     // Deep Midnight Indigo
            Color midArchive = new Color(0.042f, 0.075f, 0.145f, 1f);   // Moonlit Archive Blue
            Color lowerAbyss = new Color(0.022f, 0.032f, 0.058f, 1f);   // Dark Slate Abyss
            Color warmBounce = new Color(0.16f, 0.09f, 0.04f, 1f);      // Warm Lantern Bounce

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                Color vertical = ny > 0.45f
                    ? Color.Lerp(midArchive, topVault, (ny - 0.45f) / 0.55f)
                    : Color.Lerp(lowerAbyss, midArchive, ny / 0.45f);

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float lanternDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.74f, 0.68f));
                    float bounce = Mathf.Pow(Mathf.Clamp01(1f - lanternDist * 1.45f), 2.2f) * 0.45f;
                    tex.SetPixel(x, y, Color.Lerp(vertical, warmBounce, bounce));
                }
            }
            return RegisterSprite(tex);
        }

        private Sprite CreateGothicColonnadeSprite(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color stoneDark = new Color(0.025f, 0.042f, 0.085f, 0.94f);
            Color shelfSilhouette = new Color(0.035f, 0.058f, 0.110f, 0.78f);
            Color warmRim = new Color(0.95f, 0.62f, 0.22f, 0.35f);

            const int archCount = 5;
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float cell = nx * archCount;
                    float localX = (cell - Mathf.Floor(cell)) * 2f - 1f; // -1..1 per arch bay
                    float absX = Mathf.Abs(localX);

                    // Pointed Gothic Arch curve
                    float archHeight = 0.76f - 0.38f * (absX * absX + 0.35f * absX);
                    bool isPillarOrVault = absX > 0.78f || ny > archHeight;

                    // Rose window cutout in upper vault
                    float roseDist = Vector2.Distance(new Vector2(localX * 0.6f, ny), new Vector2(0f, 0.87f));
                    if (roseDist < 0.065f && Mathf.Abs(roseDist - 0.042f) > 0.008f)
                    {
                        isPillarOrVault = false;
                    }

                    if (isPillarOrVault)
                    {
                        // Rim highlight near the arch edge
                        float edgeDist = Mathf.Min(Mathf.Abs(absX - 0.78f), Mathf.Abs(ny - archHeight));
                        float rim = Mathf.Clamp01(1f - edgeDist * 45f) * Mathf.Clamp01(1f - Mathf.Abs(nx - 0.72f) * 1.8f);
                        tex.SetPixel(x, y, Color.Lerp(stoneDark, warmRim, rim * 0.45f));
                    }
                    else
                    {
                        // Distant ruined archive bookshelves inside the arches
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

            Color pillarCol = new Color(0.016f, 0.025f, 0.052f, 0.98f);
            Color goldRim = new Color(0.98f, 0.66f, 0.24f, 0.85f);

            float[] pillarCenters = { 0.07f, 0.27f, 0.73f, 0.93f };
            float[] pillarWidths = { 0.055f, 0.032f, 0.032f, 0.055f };

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
                        // Capital flare near top
                        if (ny > 0.82f) pw += (ny - 0.82f) * 0.14f;
                        if (ny < 0.12f) pw += (0.12f - ny) * 0.10f;

                        if (Mathf.Abs(dx) <= pw)
                        {
                            // Rim light on side facing center/lantern
                            float innerSide = (pillarCenters[i] < 0.5f) ? (dx / pw) : (-dx / pw);
                            float rim = Mathf.Pow(Mathf.Clamp01((innerSide + 1f) * 0.5f), 5f) * 0.5f;
                            pOut = Color.Lerp(pillarCol, goldRim, rim);

                            // Carved glowing Ember Vein channels inside the stone column
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
                    float nx = (x / (float)(w - 1)) * 2f - 1f; // -1..1
                    float ax = Mathf.Abs(nx);
                    bool solid = false;
                    bool rim = false;

                    // Top suspension ring (ny: 0.86..0.96)
                    float ringDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0f, 0.90f));
                    if (ringDist > 0.05f && ringDist < 0.095f) solid = true;

                    // Spire roof dome (ny: 0.68..0.86)
                    if (ny >= 0.68f && ny <= 0.86f)
                    {
                        float roofWidth = Mathf.Lerp(0.56f, 0.05f, (ny - 0.68f) / 0.18f);
                        if (ax <= roofWidth)
                        {
                            solid = true;
                            if (Mathf.Abs(ax - roofWidth) < 0.03f || ny < 0.705f) rim = true;
                        }
                    }

                    // Lantern body cage bars (ny: 0.26..0.68)
                    if (ny >= 0.26f && ny < 0.68f)
                    {
                        float cageOuter = Mathf.Lerp(0.36f, 0.48f, (ny - 0.26f) / 0.42f);
                        // Outer struts, center strut, and diagonal gothic lattice
                        if (Mathf.Abs(ax - cageOuter) < 0.038f || ax < 0.028f)
                        {
                            solid = true;
                            rim = true;
                        }
                        // Horizontal rings
                        if (Mathf.Abs(ny - 0.47f) < 0.018f && ax <= cageOuter)
                        {
                            solid = true;
                            rim = true;
                        }
                    }

                    // Bottom base & finial spike (ny: 0.04..0.26)
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

                    // Left cliff overlook where the wanderer stands (nx: 0..0.34, ny: 0..0.19)
                    float leftLedgeH = 0.18f - Mathf.Max(0f, nx - 0.22f) * 1.15f + Mathf.Sin(nx * 28f) * 0.008f;
                    // Right broken steps (nx: 0.76..1.0)
                    float rightLedgeH = 0.14f - Mathf.Max(0f, 0.84f - nx) * 0.95f + Mathf.Cos(nx * 24f) * 0.007f;
                    // Bottom vignette floor
                    float floorH = 0.038f + Mathf.Sin(nx * 15f) * 0.006f;

                    float maxH = Mathf.Max(floorH, Mathf.Max(nx < 0.38f ? leftLedgeH : 0f, nx > 0.70f ? rightLedgeH : 0f));

                    if (ny <= maxH)
                    {
                        float edge = Mathf.Clamp01(1f - (maxH - ny) * 55f);
                        float lanternProximity = Mathf.Clamp01(1f - Mathf.Abs(nx - 0.22f) * 4.5f);
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

                    // Pointed Traveler Hood & Head (center ~ (0.46, 0.78))
                    float headDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.47f, 0.77f));
                    if (headDist < 0.095f || (ny >= 0.75f && ny <= 0.89f && Mathf.Abs(nx - 0.44f) < (0.89f - ny) * 0.7f))
                    {
                        inside = true;
                        if (nx > 0.49f) rimFactor = Mathf.Clamp01((nx - 0.49f) * 14f);
                    }

                    // Flowing Traveler Cloak & Torso (ny: 0.08..0.75)
                    if (ny >= 0.08f && ny <= 0.75f)
                    {
                        float t = (0.75f - ny) / 0.67f; // 0 at neck, 1 at hem
                        float leftEdge = 0.42f - t * 0.24f - Mathf.Sin(t * 5f) * 0.03f;
                        float rightEdge = 0.52f + t * 0.11f;
                        // Hem tatters at bottom
                        float hemCut = 0.08f + Mathf.Abs(Mathf.Sin(nx * 26f)) * 0.045f;
                        if (nx >= leftEdge && nx <= rightEdge && ny >= hemCut)
                        {
                            inside = true;
                            if (rightEdge - nx < 0.035f) rimFactor = Mathf.Clamp01(1f - (rightEdge - nx) / 0.035f);
                        }
                    }

                    // Outstretched right arm holding lantern staff (from (0.50, 0.64) to (0.78, 0.60))
                    if (nx >= 0.48f && nx <= 0.79f)
                    {
                        float armY = Mathf.Lerp(0.64f, 0.60f, (nx - 0.48f) / 0.31f);
                        if (Mathf.Abs(ny - armY) < 0.024f)
                        {
                            inside = true;
                            rimFactor = 0.75f;
                        }
                    }

                    // Small hanging lantern at (0.78, 0.50..0.60)
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
                float ny = y / (float)(h - 1); // 1 at top (lantern source), 0 at bottom
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
                    // 4-point star flare
                    float star = Mathf.Clamp01(1f - (nx * 0.85f + ny * 0.85f)) + Mathf.Clamp01(1f - Mathf.Min(nx, ny) * 6f) * Mathf.Clamp01(1f - Mathf.Max(nx, ny));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(star)));
                }
            }
            return RegisterSprite(tex);
        }

        /// <summary>
        /// Creates a symmetrical Hollow-Knight / Ori style ornate filigree divider sprite.
        /// </summary>
        public Sprite CreateOrnateFiligreeSprite(int w = 512, int h = 64)
        {
            var tex = NewTexture(w, h);
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f; // -1..1
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f; // -1..1
                    float ax = Mathf.Abs(nx);
                    float ay = Mathf.Abs(ny);

                    // Tapering horizontal hairline
                    float lineThickness = Mathf.Lerp(0.08f, 0.01f, ax);
                    float mainLine = Mathf.Clamp01(1f - ay / lineThickness) * Mathf.Clamp01(1f - ax);

                    // Center diamond crest
                    float centerDiamond = Mathf.Clamp01(1f - (ax * 14f + ay * 1.8f));

                    // Symmetrical side flourishes (curved wings near |x| = 0.08..0.28)
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
    }
}
