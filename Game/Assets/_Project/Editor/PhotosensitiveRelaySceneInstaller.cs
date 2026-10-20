using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TheLightenRoad.Lantern.Relays;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern.Relays.Editor
{
    public static class PhotosensitiveRelaySceneInstaller
    {
        private const string RelayPrefabPath = "Assets/_Project/Prefabs/PhotosensitiveRelay.prefab";
        private const string GatePrefabPath = "Assets/_Project/Prefabs/ArchiveGate.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity";

        [MenuItem("Tools/Relays/Build Prefabs and Install to Chamber")]
        public static void BuildPrefabsAndInstall()
        {
            BuildPrefabs();
            InstallToChamber();
        }

        [MenuItem("Tools/Relays/Build Prefabs")]
        public static void BuildPrefabs()
        {
            // 1. Build PhotosensitiveRelay Prefab
            var relayGo = new GameObject("PhotosensitiveRelay");
            var relayComp = relayGo.AddComponent<PhotosensitiveRelay2D>();
            relayComp.EnsureHierarchy();

            PrefabUtility.SaveAsPrefabAsset(relayGo, RelayPrefabPath);
            Object.DestroyImmediate(relayGo);
            Debug.Log($"[Relays] Saved prefab to {RelayPrefabPath}");

            // 2. Build ArchiveGate Prefab
            var gateGo = new GameObject("ArchiveGate");
            var gateComp = gateGo.AddComponent<ArchiveGate2D>();
            gateComp.EnsureHierarchy();

            PrefabUtility.SaveAsPrefabAsset(gateGo, GatePrefabPath);
            Object.DestroyImmediate(gateGo);
            Debug.Log($"[Relays] Saved prefab to {GatePrefabPath}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Relays/Install to Archive Test Chamber")]
        public static void InstallToChamber()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            bool needReopen = activeScene.path != ScenePath;

            UnityEngine.SceneManagement.Scene scene;
            if (needReopen)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = activeScene;
            }

            var root = GameObject.Find("SIRAC / ARCHIVE TEST CHAMBER");
            if (root == null)
            {
                Debug.LogError("[Relays] Could not find root object in ArchiveTestChamber!");
                return;
            }

            Transform decor = null;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name.StartsWith("02"))
                {
                    decor = child;
                    break;
                }
            }

            if (decor == null)
            {
                Debug.LogError("[Relays] Could not find child starting with '02' under root!");
                return;
            }

            // Remove previous instances if any
            var existingRelay = decor.Find("PhotosensitiveRelay_Intake");
            if (existingRelay != null) Object.DestroyImmediate(existingRelay.gameObject);

            var existingGate = decor.Find("ArchiveGate_IntakeVault");
            if (existingGate != null) Object.DestroyImmediate(existingGate.gameObject);

            // Load prefabs
            var relayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RelayPrefabPath);
            var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath);

            if (relayPrefab == null || gatePrefab == null)
            {
                BuildPrefabs();
                relayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RelayPrefabPath);
                gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath);
            }

            // Instantiate Gate at intake doorway (x = -6.2, y = 0.0)
            var gateInstance = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, decor);
            gateInstance.name = "ArchiveGate_IntakeVault";
            gateInstance.transform.position = new Vector3(-6.2f, 0.0f, 0f);
            var gateComp = gateInstance.GetComponent<ArchiveGate2D>();

            // Instantiate Relay between spawn (-5.0) and shelf (-1.0) at x = -3.2, y = 0.0
            var relayInstance = (GameObject)PrefabUtility.InstantiatePrefab(relayPrefab, decor);
            relayInstance.name = "PhotosensitiveRelay_Intake";
            relayInstance.transform.position = new Vector3(-3.2f, 0.0f, 0f);
            var relayComp = relayInstance.GetComponent<PhotosensitiveRelay2D>();
            relayComp.Mode = RelayMode.Timed;
            relayComp.LingerDuration = 6.0f;

            // Connect Gate to Relay
            if (gateComp != null)
            {
                gateComp.Relay = relayComp;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Relays] Successfully installed Photosensitive Relay and Archive Gate into ArchiveTestChamber!");
        }
    }
}
