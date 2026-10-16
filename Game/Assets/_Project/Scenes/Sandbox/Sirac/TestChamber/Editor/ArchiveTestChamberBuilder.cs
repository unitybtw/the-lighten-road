using System.IO;
using System.Linq;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Sirac.TestChamber;
using TheLightenRoad.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Sirac.Editor
{
    /// <summary>Creates only Siraç's isolated archive test scene and its local materials.</summary>
    public static class ArchiveTestChamberBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity";
        private const string AssetRoot = "Assets/_Project/Scenes/Sandbox/Sirac/TestChamber";
        private static Sprite square;
        private static Sprite crate;
        private static Material lit;
        private static Transform geometry;
        private static Transform decor;
        private static Transform pickups;
        private static ChamberDirector director;
        private static TMP_FontAsset font;

        public static string Build()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play Mode before building the test chamber.");
            if (File.Exists(ScenePath))
                throw new System.InvalidOperationException("The test chamber already exists; edit it normally to preserve scene work.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the active scene before creating the test chamber.");

            square = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Prototype/WhiteSquare.png").OfType<Sprite>().First();
            crate = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Box.png");
            if (!AssetDatabase.IsValidFolder(AssetRoot + "/Materials"))
                AssetDatabase.CreateFolder(AssetRoot, "Materials");
            lit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
            lit.name = "Archive test surfaces";
            AssetDatabase.CreateAsset(lit, AssetRoot + "/Materials/ArchiveLit.mat");
            AssetDatabase.CopyAsset("Assets/_Project/Resources/UI/EmberHUD.asset", AssetRoot + "/ArchiveFont.asset");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetRoot + "/ArchiveFont.asset");
            font.TryAddCharacters(new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SIRAC / ARCHIVE TEST CHAMBER");
            geometry = Group("01 / Collisions", root.transform);
            decor = Group("02 / Architecture and props", root.transform);
            pickups = Group("03 / Ember pickups", root.transform);
            var markers = Group("04 / Route markers", root.transform);
            var systems = Group("05 / Player and systems", root.transform);
            director = root.AddComponent<ChamberDirector>();

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerPrototype.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, systems);
            player.name = "Player";
            player.transform.position = new Vector3(-5f, 0.9f, 0f);
            var respawn = player.GetComponent<PlayerRespawnController>();
            respawn.SpawnPosition = player.transform.position;
            respawn.VoidKillY = -4f;
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EmberPickup.prefab");
            director.Configure(respawn, pickups, pickupPrefab, player.transform.position, font);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.transform.SetParent(systems);
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            var follow = cameraGo.AddComponent<FollowCamera2D>();
            follow.Offset = new Vector3(0f, 1.65f, -10f);
            follow.SetTarget(player.transform, true);
            var cameraData = new SerializedObject(follow);
            cameraData.FindProperty("enableBounds").boolValue = true;
            cameraData.FindProperty("boundsMin").vector2Value = new Vector2(-3.5f, 2.1f);
            cameraData.FindProperty("boundsMax").vector2Value = new Vector2(54f, 3.2f);
            cameraData.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("Lantern HUD", typeof(LanternHUD)).transform.SetParent(systems);
            new GameObject("Death overlay", typeof(DeathScreenOverlay)).transform.SetParent(systems);
            // No ambient light: every surface is lit by the existing lantern, including the backdrop.
            var global = new GameObject("Ambient light / zero", typeof(Light2D));
            global.transform.SetParent(systems);
            var ambient = global.GetComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.intensity = 0f;
            ambient.color = Color.black;

            Backdrop();
            // Floor top is y=0. Main-route pits are 2m wide; jump rise is at most 1m.
            Floor("Intake and archive", -8f, 17f);
            Floor("Service island", 19f, 22f);
            Floor("Service landing", 24f, 39f);
            Floor("Recovery and exit", 41f, 62f);
            Solid("West boundary", -8.35f, 2f, 0.7f, 9f, Stone);
            Solid("East boundary", 62.35f, 2f, 0.7f, 9f, Stone);
            Ceiling("Intake ceiling", -8f, 1f, 4.2f);
            Ceiling("Archive ceiling", 1f, 16f, 6.2f);
            Ceiling("Service ceiling", 16f, 28f, 4.4f);
            Ceiling("Blackout ceiling", 28f, 44f, 4.2f);
            Ceiling("Recovery ceiling", 44f, 62f, 5.2f);

            Crate("Low jump / 0.65m", 2.5f, 0.65f, 1.3f);
            Crate("High jump / 1.0m", 5.7f, 1f, 1.5f);
            Solid("Optional archive mezzanine", 9.5f, 1.92f, 5.4f, 0.16f, Metal);
            Edge("Mezzanine lip", 9.5f, 2.02f, 5.4f, Gold);
            Quad("Mezzanine rear support", 11.6f, 1.0f, 0.14f, 2f, Metal, -12);
            Crate("Blackout low jump", 34.2f, 0.65f, 1.3f);
            Crate("Blackout high jump", 36f, 1f, 1.3f);
            Crate("Exit low jump", 46.5f, 0.65f, 1.3f);
            Crate("Exit optional step", 50.5f, 1f, 1.5f);
            Hazard("Service trench A", 17f, 19f);
            Hazard("Service trench B", 22f, 24f);
            Hazard("Blackout trench", 39f, 41f);

            // Visible shelves establish scale and provide meaningful silhouettes during scouting.
            Shelf(-1f, 2.1f, 2.3f, 2.5f);
            Shelf(10f, 4f, 3.8f, 2.4f);
            Shelf(13.5f, 1.3f, 1.9f, 2.3f);
            Shelf(29f, 1.4f, 2.2f, 2.5f);
            Shelf(48f, 3.5f, 3f, 2.2f);
            Doorway(-6.2f, 0f, 2.4f, 3.2f, false);
            Doorway(57.5f, 0f, 2.4f, 3.2f, true);
            CheckpointStand(14f);
            CheckpointStand(32.5f);

            Marker(markers, "01 / INTAKE", -4f, ChamberMarker.MarkerKind.Zone, 0,
                "01 / INTAKE", "Start in Dim. Use F to compare the visibility of the archive ahead.", 3.3f);
            Marker(markers, "02 / ARCHIVE STACKS", 1f, ChamberMarker.MarkerKind.Zone, 0,
                "02 / ARCHIVE STACKS", "Short hops, full jumps, and an optional upper shelf. Hold SPACE for a higher jump.", 4.8f);
            Marker(markers, "Checkpoint 01", 14f, ChamberMarker.MarkerKind.Checkpoint, 1,
                "CHECKPOINT 01", "Service crossing ahead.", 2.5f);
            Marker(markers, "03 / SERVICE CROSSING", 15.8f, ChamberMarker.MarkerKind.Zone, 0,
                "03 / SERVICE CROSSING", "Two 2m gaps. Scout the landing before jumping; falls return to Checkpoint 01.", 3.6f);
            Marker(markers, "Checkpoint 02", 32.5f, ChamberMarker.MarkerKind.Checkpoint, 2,
                "CHECKPOINT 02", "Blackout corridor ahead.", 2.5f);
            Marker(markers, "04 / BLACKOUT", 33.8f, ChamberMarker.MarkerKind.Zone, 0,
                "04 / BLACKOUT CORRIDOR", "Use Flare to read silhouettes, overhead shadows, and the final trench.", 3.6f);
            Marker(markers, "05 / RECOVERY", 43.5f, ChamberMarker.MarkerKind.Zone, 0,
                "05 / RECOVERY", "Collect the remaining embers and reach the sealed archive door.", 3.8f);
            Marker(markers, "Exit", 57.5f, ChamberMarker.MarkerKind.Exit, 0,
                "ARCHIVE ACCESS", "End of test route", 3.5f);

            foreach (var position in new[] {
                new Vector3(-2.5f, 0.6f), new Vector3(5.7f, 1.6f),
                new Vector3(9.5f, 2.6f), new Vector3(20.5f, 0.6f),
                new Vector3(26.7f, 0.6f), new Vector3(36f, 1.6f),
                new Vector3(46.5f, 1.25f), new Vector3(52.8f, 0.6f) })
            {
                var pickup = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, pickups);
                pickup.transform.position = position;
                pickup.name = "Ember / " + (pickups.childCount).ToString("00");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssetIfDirty(font);
            AssetDatabase.SaveAssetIfDirty(lit);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(27f, 1.5f), new Vector3(74f, 12f, 1f)), false);
            return "Created ArchiveTestChamber: five sections, three 2m trenches, two checkpoints, eight pickups, and a resettable exit. No shared scene, prefab, or project setting was edited.";
        }

        private static readonly Color Stone = new Color32(73, 82, 85, 255);
        private static readonly Color Metal = new Color32(57, 71, 77, 255);
        private static readonly Color Gold = new Color32(166, 139, 87, 255);

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Quad(string name, float x, float y, float w, float h, Color color,
            int order = -20, Transform parent = null, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : decor, false);
            go.transform.position = new Vector3(x, y, 0f);
            var visual = new GameObject("Surface", typeof(SpriteRenderer));
            visual.transform.SetParent(go.transform, false);
            var render = visual.GetComponent<SpriteRenderer>();
            render.sprite = sprite != null ? sprite : square;
            render.sharedMaterial = lit;
            render.color = color;
            render.sortingOrder = order;
            Vector3 size = render.sprite.bounds.size;
            Vector3 scale = new Vector3(w / size.x, h / size.y, 1f);
            visual.transform.localScale = scale;
            visual.transform.localPosition = -Vector3.Scale(render.sprite.bounds.center, scale);
            return go;
        }

        private static GameObject Solid(string name, float x, float y, float w, float h, Color color, Sprite sprite = null)
        {
            var go = Quad(name, x, y, w, h, color, 0, geometry, sprite);
            go.AddComponent<BoxCollider2D>().size = new Vector2(w, h);
            var shadow = go.GetComponentInChildren<SpriteRenderer>().gameObject.AddComponent<ShadowCaster2D>();
            shadow.selfShadows = false;
            return go;
        }

        private static void Edge(string name, float x, float y, float width, Color color)
            => Quad(name, x, y, width, 0.06f, color, 1);

        private static void Floor(string name, float left, float right)
        {
            Solid("Floor / " + name, (left + right) / 2f, -1f, right - left, 2f, Stone);
            Edge("Walkable edge", (left + right) / 2f, -0.04f, right - left, new Color32(134, 142, 136, 255));
            for (float x = left + 0.5f; x < right; x += 1f)
                Quad("Floor joint", x, -0.38f, 0.025f, 0.65f, new Color32(46, 54, 58, 255), 1);
        }

        private static void Ceiling(string name, float left, float right, float y)
        {
            Solid(name, (left + right) / 2f, y + 0.25f, right - left, 0.5f, Stone);
            Edge("Ceiling rim", (left + right) / 2f, y, right - left, Metal);
            Quad("Conduit", (left + right) / 2f, y - 0.28f, right - left - 0.4f, 0.06f, Gold, -14);
        }

        private static void Crate(string name, float x, float top, float width)
            => Solid(name, x, top / 2f, width, top, Color.white, crate);

        private static void Backdrop()
        {
            Quad("Back wall", 27f, 2.2f, 72f, 8f, new Color32(42, 52, 58, 255), -50);
            for (float x = -8f; x < 62f; x += 2f)
            {
                Quad("Wall panel", x + 0.95f, 2.3f, 1.84f, 4.5f, new Color32(48, 59, 65, 255), -48);
                Quad("Wall seam", x, 2.3f, 0.06f, 4.5f, new Color32(26, 35, 39, 255), -46);
                Quad("Wall skirting", x + 0.95f, 0.22f, 1.84f, 0.28f, Metal, -42);
                if (Mathf.RoundToInt(x) % 8 == 0)
                {
                    Quad("Rear pillar", x + 0.1f, 2.7f, 0.28f, 5.4f, new Color32(38, 48, 54, 255), -38);
                    Quad("Pipe riser", x + 0.5f, 2.3f, 0.08f, 4f, Gold, -35);
                }
            }
        }

        private static void Shelf(float x, float y, float w, float h)
        {
            Quad("Archive shelving", x, y, w, h, new Color32(30, 39, 42, 255), -30);
            for (int row = 0; row < 4; row++)
            {
                float shelfY = y - h * 0.5f + row * h / 4f;
                Quad("Archive shelf", x, shelfY, w, 0.06f, Metal, -24);
                for (int book = 0; book < 6; book++)
                {
                    float height = 0.22f + ((book + row) % 3) * 0.06f;
                    Quad("Archive folder", x - w * 0.42f + book * w * 0.155f,
                        shelfY + 0.06f + height * 0.5f, w * 0.1f, height,
                        (book + row) % 3 == 0 ? new Color32(108, 92, 67, 255) : new Color32(70, 82, 84, 255), -25);
                }
            }
        }

        private static void Doorway(float x, float floor, float w, float h, bool exit)
        {
            Quad("Door recess", x, floor + h / 2f, w, h, new Color32(15, 24, 28, 255), -35);
            Quad("Door split", x, floor + h / 2f, 0.04f, h, Metal, -20);
            Quad("Door lintel", x, floor + h, w + 0.25f, 0.18f, Metal, -10);
            Quad("Door frame west", x - w / 2f, floor + h / 2f, 0.13f, h, Metal, -10);
            Quad("Door frame east", x + w / 2f, floor + h / 2f, 0.13f, h, Metal, -10);
            Quad(exit ? "Exit access strip" : "Entry access strip", x + w / 2f - 0.25f, floor + 1.1f,
                0.09f, 0.65f, Gold, -5);
        }

        private static void CheckpointStand(float x)
        {
            Quad("Checkpoint pedestal", x + 0.55f, 0.65f, 0.28f, 1.3f, Metal, -10);
            Quad("Checkpoint collar", x + 0.55f, 1.3f, 0.48f, 0.16f, Gold, -9);
        }

        private static void Hazard(string name, float left, float right)
        {
            var trigger = new GameObject(name, typeof(BoxCollider2D));
            trigger.transform.SetParent(geometry);
            trigger.transform.position = new Vector3((left + right) / 2f, -0.65f, 0f);
            var col = trigger.GetComponent<BoxCollider2D>();
            col.size = new Vector2(right - left, 1.1f);
            col.isTrigger = true;
            trigger.AddComponent<HazardTrigger2D>();
            Quad("Trench bed", (left + right) / 2f, -1.45f, right - left, 0.45f,
                new Color32(78, 39, 28, 255), -15);
            for (float x = left + 0.15f; x < right; x += 0.3f)
                Quad("Hazard rib", x, -1.1f, 0.07f, 0.5f, new Color32(159, 96, 49, 255), -10);
            for (int side = 0; side < 2; side++)
            {
                float edge = side == 0 ? left - 0.15f : right + 0.15f;
                for (int i = 0; i < 4; i++)
                    Quad("Warning stripe", edge + (i - 1.5f) * 0.07f, 0.025f, 0.04f, 0.04f, Gold, 2);
            }
        }

        private static void Marker(Transform parent, string name, float x, ChamberMarker.MarkerKind kind,
            int index, string title, string hint, float signY)
        {
            var go = new GameObject(name, typeof(BoxCollider2D), typeof(ChamberMarker));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, 1.7f, 0f);
            var col = go.GetComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 4.2f);
            col.isTrigger = true;
            var sign = new GameObject("Wall sign", typeof(TextMeshPro));
            sign.transform.SetParent(go.transform, false);
            sign.transform.position = new Vector3(x, signY, 0f);
            var label = sign.GetComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = kind == ChamberMarker.MarkerKind.Checkpoint ? 1.8f : 2.6f;
            label.characterSpacing = 1.5f;
            label.text = title;
            label.color = new Color32(191, 178, 143, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(8f, 0.7f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            sign.GetComponent<MeshRenderer>().sortingOrder = -5;
            var light = Quad("Status plaque", x, signY - 0.35f, kind == ChamberMarker.MarkerKind.Checkpoint ? 0.65f : 0.35f,
                0.045f, Gold, -5).GetComponentInChildren<SpriteRenderer>();
            go.GetComponent<ChamberMarker>().Configure(director, kind, title, hint, index,
                new Vector3(x, 0.9f, 0f), label, light);
        }
    }
}
