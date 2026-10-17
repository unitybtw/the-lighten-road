using System.IO;
using System.Linq;
using TheLightenRoad.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.Frontend.Editor
{
    /// <summary>
    /// Creates and configures the redesigned MainMenu scene (powered by ProceduralArchiveStage,
    /// with zero static backdrop photos) and registers all selectable gameplay realms in EditorBuildSettings.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";
        private const string FontPath = "Assets/_Project/Resources/UI/MenuType.asset";

        private static readonly string[] BuildScenePaths =
        {
            ScenePath,
            "Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity",
            "Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity",
            "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity",
            "Assets/Scenes/SampleScene.unity"
        };

        [MenuItem("Tools/The Lighten Road/Create Main Menu")]
        public static void CreateFromMenu()
        {
            Create(forceOverwrite: true);
        }

        public static void Create(bool forceOverwrite = true)
        {
            if (!forceOverwrite && File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                RegisterBuildScenes();
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/_Project/Scenes/Frontend");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Menu Camera (Deep Midnight Indigo Clear Color)
            var cameraGo = new GameObject("Menu Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.030f, 0.068f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = -10f;
            camera.farClipPlane = 100f;
            cameraGo.AddComponent<AudioListener>();

            // 2. Menu Ambience (Background Music)
            var musicGo = new GameObject("Menu Ambience");
            musicGo.AddComponent<BackgroundMusicManager>();

            // 3. Sound Effect Manager (UI + Lantern Flare Ignition SFX)
            var sfxGo = new GameObject("Sound Effect Manager");
            sfxGo.AddComponent<SoundEffectManager>();

            // 4. Main Menu Controller Root
            var root = new GameObject("The Lighten Road / Main Menu");
            var controller = root.AddComponent<MainMenuController>();

            var serialized = new SerializedObject(controller);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                serialized.FindProperty("font").objectReferenceValue = font;
            }

            serialized.FindProperty("gameplayScene").stringValue = MainMenuController.DefaultGameplayScene;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
        }

        private static void RegisterBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.ToList();

            foreach (string path in BuildScenePaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                int existingIdx = scenes.FindIndex(s => s.path == path);
                if (existingIdx >= 0)
                {
                    scenes[existingIdx].enabled = true;
                }
                else
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            int menuIdx = scenes.FindIndex(s => s.path == ScenePath);
            if (menuIdx > 0)
            {
                var menuScene = scenes[menuIdx];
                scenes.RemoveAt(menuIdx);
                scenes.Insert(0, menuScene);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
