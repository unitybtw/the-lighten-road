using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TheLightenRoad.Audio;
using TheLightenRoad.Player;

namespace TheLightenRoad.Editor
{
    public static class AudioSetupUtility
    {
        [MenuItem("Tools/The Lighten Road/Setup SFX & Audio")]
        public static void SetupAudio()
        {
            int changes = 0;

            // 1. Setup SoundEffectManager in Scene
            var sfxManager = Object.FindAnyObjectByType<SoundEffectManager>();
            if (sfxManager == null)
            {
                var sfxGo = new GameObject("Sound Effect Manager");
                Undo.RegisterCreatedObjectUndo(sfxGo, "Create Sound Effect Manager");
                sfxManager = sfxGo.AddComponent<SoundEffectManager>();
                changes++;
                Debug.Log("[AudioSetupUtility] Created Sound Effect Manager in scene.");
            }

            // Load and assign clips
            var walks = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_walk_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_walk_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_walk_03.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_walk_04.wav")
            };

            var sprints = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_sprint_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_sprint_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_sprint_03.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/footstep_sprint_04.wav")
            };

            var jump = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/jump_takeoff.wav");
            var landSoft = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/landing_soft.wav");
            var landHard = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/landing_hard.wav");
            var flare = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Lantern/flare_ignite.wav");
            var emberBreak = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Lantern/ember_break.wav");
            var emberPick = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Environment/ember_pickup.wav");
            var breath = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Audio/SFX/Player/exhaustion_breath.wav");

            sfxManager.ConfigureClips(walks, sprints, jump, landSoft, landHard, flare, emberBreak, emberPick, breath);
            EditorUtility.SetDirty(sfxManager);
            changes++;

            // 2. Setup PlayerAudioController on Player
            var playerMotor = Object.FindAnyObjectByType<PlayerMotor2D>();
            if (playerMotor != null)
            {
                var playerGo = playerMotor.gameObject;
                if (playerGo.GetComponent<PlayerAudioController>() == null)
                {
                    Undo.AddComponent<PlayerAudioController>(playerGo);
                    changes++;
                    Debug.Log("[AudioSetupUtility] Added PlayerAudioController to player.");
                }
            }

            if (changes > 0)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AudioSetupUtility] Setup completed and scene '{scene.name}' saved with {changes} audio configurations.");
            }
        }
    }
}
