using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Editor
{
    public static class ShadowSetupUtility
    {
        [MenuItem("Tools/The Lighten Road/Setup 2D Dynamic Shadows")]
        public static void Setup2DDynamicShadows()
        {
            int addedCount = 0;
            var colliders = Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);

            foreach (var col in colliders)
            {
                // Skip player and pickups
                if (col.CompareTag("Player") || col.isTrigger)
                    continue;

                var go = col.gameObject;
                if (go.GetComponent<ShadowCaster2D>() == null)
                {
                    Undo.AddComponent<ShadowCaster2D>(go);
                    addedCount++;
                    Debug.Log($"[ShadowSetupUtility] Added ShadowCaster2D to {go.name}");
                }
            }

            // Verify player light has shadows active
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.lightType == Light2D.LightType.Point)
                {
                    Undo.RecordObject(l, "Enable 2D Shadows");
                    l.shadowsEnabled = true;
                    l.shadowIntensity = 0.85f;
                    l.shadowSoftness = 0.25f;
                    EditorUtility.SetDirty(l);
                }
            }

            Debug.Log($"[ShadowSetupUtility] Configured 2D dynamic shadows. Added {addedCount} shadow casters.");
        }
    }
}
