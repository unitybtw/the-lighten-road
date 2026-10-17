using System.Linq;
using TheLightenRoad.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.Frontend.Editor
{
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";
        [MenuItem("Tools/The Lighten Road/Create Main Menu")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new System.InvalidOperationException("Save the current scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new System.InvalidOperationException("The menu exists. Edit its scene directly.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            var cam = camera.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(9,12,11,255);
            cam.orthographic = true;
            camera.transform.position = new Vector3(0,0,-10);
            // The painted environment and live UI glow are intentionally independent of gameplay lights.
            var light = new GameObject("Menu key light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 0.1f;
            var music = new GameObject("Menu ambience", typeof(BackgroundMusicManager));
            var root = new GameObject("The Lighten Road / Main Menu", typeof(MainMenuController));
            var data = new SerializedObject(root.GetComponent<MainMenuController>());
            data.FindProperty("backdrop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Frontend/ArchiveMenuBackdrop.png");
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var existing = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != MainMenuController.DefaultGameplayScene).ToList();
            existing.Insert(0, new EditorBuildSettingsScene(MainMenuController.DefaultGameplayScene, true));
            existing.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = existing.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
