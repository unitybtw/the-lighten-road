using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TheLightenRoad.Effects;

namespace TheLightenRoad.Editor
{
    public static class SwayingPropSceneInstaller
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/HangingCageLamp.prefab";
        [MenuItem("Tools/The Lighten Road/Install Swaying Props to Scenes")]
        public static void InstallAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var prefab = CreateOrUpdatePrefab();
                InstallToArchiveTestChamber(prefab);
                InstallToFurkanTutorial(prefab);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
            AssetDatabase.Refresh();
            Debug.Log("[SwayingPropSceneInstaller] Successfully installed Hanging Cage Lamps and positioned test ember in scenes.");
        }

        public static GameObject CreateOrUpdatePrefab()
        {
            var go = new GameObject("HangingCageLamp");
            var prop = go.AddComponent<SwayingProp2D>();
            prop.InitializeHierarchy();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        public static void InstallToArchiveTestChamber(GameObject prefab = null)
        {
            if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreateOrUpdatePrefab();

            string scenePath = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var root = GameObject.Find("SIRAC / ARCHIVE TEST CHAMBER");
            if (root == null)
            {
                Debug.LogError("[SwayingPropSceneInstaller] Root 'SIRAC / ARCHIVE TEST CHAMBER' not found.");
                return;
            }

            var propsParent = root.transform.Find("02 / Architecture and props");
            if (propsParent == null) propsParent = root.transform;

            var lampsGroup = propsParent.Find("Hanging Lamps");
            if (lampsGroup == null)
            {
                var lampsGo = new GameObject("Hanging Lamps");
                lampsGo.transform.SetParent(propsParent, false);
                lampsGroup = lampsGo.transform;
            }

            // Clear old if re-running
            while (lampsGroup.childCount > 0)
            {
                Object.DestroyImmediate(lampsGroup.GetChild(0).gameObject);
            }

            // Positions tuned specifically for the ceiling and player path in ArchiveTestChamber
            var positions = new[]
            {
                new Vector3(-2.5f, 3.6f, 0f), // Right in front of spawn corridor (-5 to 0)
                new Vector3(4.5f, 3.6f, 0f),  // Main intake corridor
                new Vector3(12.0f, 3.6f, 0f), // Near Checkpoint 01
                new Vector3(20.5f, 3.6f, 0f), // Service island
                new Vector3(35.0f, 4.2f, 0f)  // High ceiling vault
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lampsGroup);
                instance.transform.position = positions[i];
                instance.name = $"HangingCageLamp_{i + 1:00}";
            }

            // Position an ember pickup near player spawn (-2.0, 1.0) so magnetism and spark trail are immediately testable
            var pickupsRoot = root.transform.Find("03 / Ember pickups");
            if (pickupsRoot != null && pickupsRoot.childCount > 0)
            {
                var firstEmber = pickupsRoot.GetChild(0);
                firstEmber.position = new Vector3(-2.0f, 1.0f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SwayingPropSceneInstaller] Installed {positions.Length} lamps and positioned Ember in ArchiveTestChamber.");
        }

        public static void InstallToFurkanTutorial(GameObject prefab = null)
        {
            if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreateOrUpdatePrefab();

            string scenePath = "Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var root = GameObject.Find("FURKAN / TUTORIAL AND BEGINNINGS");
            if (root == null)
            {
                Debug.LogError("[SwayingPropSceneInstaller] Root 'FURKAN / TUTORIAL AND BEGINNINGS' not found.");
                return;
            }

            var lampsGroup = root.transform.Find("Hanging Lamps");
            if (lampsGroup == null)
            {
                var lampsGo = new GameObject("Hanging Lamps");
                lampsGo.transform.SetParent(root.transform, false);
                lampsGroup = lampsGo.transform;
            }

            while (lampsGroup.childCount > 0)
            {
                Object.DestroyImmediate(lampsGroup.GetChild(0).gameObject);
            }

            var positions = new[]
            {
                new Vector3(3.0f, 3.8f, 0f),  // Tutorial arrival corridor
                new Vector3(14.5f, 4.8f, 0f), // Reception landing
                new Vector3(22.0f, 4.8f, 0f), // Stepping stones
                new Vector3(32.0f, 5.8f, 0f)  // Puzzle ledge
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lampsGroup);
                instance.transform.position = positions[i];
                instance.name = $"HangingCageLamp_{i + 1:00}";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SwayingPropSceneInstaller] Installed {positions.Length} lamps in FurkanTutorial.");
        }
    }
}
