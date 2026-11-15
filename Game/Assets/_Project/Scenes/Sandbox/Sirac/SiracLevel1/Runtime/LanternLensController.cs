using System;
using System.Collections.Generic;
using System.Reflection;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Controls Lantern Lenses switched via Q/E or Mouse Scroll:
    /// - Normal (Amber)
    /// - Ethereal (Cyan, disables collision on Ghost mechanisms & sets Veil targets to state 2)
    /// - Invert (Crimson, reverses Vein/mechanism motion & sets targets to state -1)
    /// </summary>
    public sealed class LanternLensController : MonoBehaviour
    {
        [SerializeField] private float lensEffectRadius = 100f;

        private LanternController lantern;
        private Light2D lanternLight;
        private float cachedBaseIntensity = -1f;

        private static readonly Color AmberColor = new Color(1f, 0.62f, 0.24f, 1f);
        private static readonly Color EtherealCyan = new Color(0.20f, 0.92f, 1.0f, 1f);
        private static readonly Color InvertCrimson = new Color(1.0f, 0.20f, 0.32f, 1f);
        private static readonly List<ILensReactive> RegisteredReactives = new List<ILensReactive>(32);

        public static void RegisterReactive(ILensReactive reactive)
        {
            if (reactive != null && !RegisteredReactives.Contains(reactive))
                RegisteredReactives.Add(reactive);
        }

        public static void UnregisterReactive(ILensReactive reactive)
        {
            if (reactive != null)
                RegisteredReactives.Remove(reactive);
        }

        public LanternLensType ActiveLens { get; private set; } = LanternLensType.Normal;
        public bool IsSuppressedByShadowRealm { get; set; }
        public float LensEffectRadius { get => lensEffectRadius; set => lensEffectRadius = Mathf.Max(0.5f, value); }

        public event Action<LanternLensType> OnLensChanged;

        private void Awake()
        {
            lantern = GetComponent<LanternController>();
            lanternLight = lantern != null ? lantern.LanternLight : GetComponentInChildren<Light2D>();
            if (lanternLight != null)
            {
                cachedBaseIntensity = lanternLight.intensity;
            }
        }

        private void Update()
        {
            if (GameControls.GameplayAllowed)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.qKey.wasPressedThisFrame) CycleLens(-1);
                    else if (kb.eKey.wasPressedThisFrame && !IsInteractingWithVeinNearby()) CycleLens(1);
                }

                var mouse = Mouse.current;
                if (mouse != null)
                {
                    float scroll = mouse.scroll.y.ReadValue();
                    if (scroll > 0.05f) CycleLens(1);
                    else if (scroll < -0.05f) CycleLens(-1);
                }
            }

            ApplyLensEffectsNow();
        }

        private void LateUpdate()
        {
            SyncLanternLight();
        }

        private bool IsInteractingWithVeinNearby()
        {
            var veins = AdvancedEmberVein.ActiveVeins;
            if (veins.Count == 0)
            {
                var found = FindObjectsByType<AdvancedEmberVein>(FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && !veins.Contains(found[i]))
                        veins.Add(found[i]);
                }
            }

            Vector2 origin = transform.position;
            const float maxDistSq = 2.4f * 2.4f;
            for (int i = veins.Count - 1; i >= 0; i--)
            {
                var v = veins[i];
                if (v == null)
                {
                    veins.RemoveAt(i);
                    continue;
                }
                if (v.isActiveAndEnabled && ((Vector2)v.transform.position - origin).sqrMagnitude <= maxDistSq)
                    return true;
            }
            return false;
        }

        public void CycleLens(int direction)
        {
            int count = 3;
            int next = ((int)ActiveLens + direction) % count;
            if (next < 0) next += count;
            SetLens((LanternLensType)next);
        }

        public void SetLens(LanternLensType lens)
        {
            if (ActiveLens == lens) return;
            ActiveLens = lens;
            OnLensChanged?.Invoke(ActiveLens);
            SyncLanternLight();
            ApplyLensEffectsNow();
        }

        public void ApplyLensEffectsNow()
        {
            if (RegisteredReactives.Count == 0)
            {
                var allMono = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < allMono.Length; i++)
                {
                    if (allMono[i] is ILensReactive discovered)
                        RegisterReactive(discovered);
                }
            }

            LanternLensType effectiveLens = IsSuppressedByShadowRealm ? LanternLensType.Normal : ActiveLens;
            Vector2 origin = transform.position;
            float radiusSq = lensEffectRadius * lensEffectRadius;
            for (int i = RegisteredReactives.Count - 1; i >= 0; i--)
            {
                var reactive = RegisteredReactives[i];
                if (!(reactive is MonoBehaviour mb) || mb == null)
                {
                    RegisteredReactives.RemoveAt(i);
                    continue;
                }
                if (!mb.isActiveAndEnabled) continue;
                float distSq = ((Vector2)mb.transform.position - origin).sqrMagnitude;
                bool inRange = !IsSuppressedByShadowRealm && distSq <= radiusSq;
                reactive.ApplyLensField(effectiveLens, inRange);
            }
        }

        public void SyncLanternLight()
        {
            if (lantern == null) lantern = GetComponent<LanternController>();
            if (lanternLight == null && lantern != null) lanternLight = lantern.LanternLight;

            if (lantern != null)
            {
                lantern.SetPresentationVisibility(IsSuppressedByShadowRealm ? 0f : 1f);
            }

            if (lanternLight == null) return;

            if (cachedBaseIntensity < 0f && lanternLight.intensity > 0f)
            {
                cachedBaseIntensity = lanternLight.intensity;
            }

            if (IsSuppressedByShadowRealm)
            {
                lanternLight.intensity = 0f;
            }
            else
            {
                lanternLight.color = GetLensColor(ActiveLens);
            }
        }

        public static Color GetLensColor(LanternLensType lens)
        {
            switch (lens)
            {
                case LanternLensType.Ethereal: return EtherealCyan;
                case LanternLensType.Invert: return InvertCrimson;
                default: return AmberColor;
            }
        }

        public static Color GetEmberColorTint(EmberColor color)
        {
            switch (color)
            {
                case EmberColor.Orange: return AmberColor;
                case EmberColor.Cyan: return EtherealCyan;
                case EmberColor.Crimson: return InvertCrimson;
                default: return new Color(0.28f, 0.32f, 0.38f, 1f);
            }
        }
    }
}
