using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TheLightenRoad.Effects;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;

namespace TheLightenRoad.Editor
{
    public static class EffectsSetupUtility
    {
        [MenuItem("Tools/The Lighten Road/Setup Atmospheric Particle Effects")]
        public static void SetupAtmosphericEffects()
        {
            int addedCount = 0;

            // 1. Ambient Dust Motes in Scene
            var existingDust = Object.FindAnyObjectByType<DustMotesAtmosphere>();
            if (existingDust == null)
            {
                var dustGo = new GameObject("Ambient Dust Motes");
                dustGo.transform.position = new Vector3(0f, 2f, 0f);
                Undo.RegisterCreatedObjectUndo(dustGo, "Create Ambient Dust Motes");
                var dustComp = dustGo.AddComponent<DustMotesAtmosphere>();
                addedCount++;
                Debug.Log("[EffectsSetupUtility] Created Ambient Dust Motes in scene.");
            }

            // 2. Player Effects (Footstep Dust + Lantern Sparks)
            var motor = Object.FindAnyObjectByType<PlayerMotor2D>();
            if (motor != null)
            {
                var playerGo = motor.gameObject;
                if (playerGo.GetComponent<PlayerFootstepDustFX>() == null)
                {
                    Undo.AddComponent<PlayerFootstepDustFX>(playerGo);
                    addedCount++;
                    Debug.Log("[EffectsSetupUtility] Added PlayerFootstepDustFX to player.");
                }

                var lantern = playerGo.GetComponentInChildren<LanternController>();
                if (lantern != null)
                {
                    var lanternLight = lantern.transform.Find("Lantern Light");
                    var targetGo = lanternLight != null ? lanternLight.gameObject : lantern.gameObject;

                    if (targetGo.GetComponent<LanternSparksFX>() == null)
                    {
                        Undo.AddComponent<LanternSparksFX>(targetGo);
                        addedCount++;
                        Debug.Log($"[EffectsSetupUtility] Added LanternSparksFX to {targetGo.name}.");
                    }
                }
            }

            if (addedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"[EffectsSetupUtility] Setup completed. Added {addedCount} particle atmospheric elements.");
            }
            else
            {
                Debug.Log("[EffectsSetupUtility] Atmospheric particle effects are already set up.");
            }
        }
    }
}
