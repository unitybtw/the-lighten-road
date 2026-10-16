using System;
using System.Linq;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Sirac.LevelMap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Sirac.Editor
{
    /// <summary>One-time layout pass for MovementSandbox. Never rebuilds a worked-on level.</summary>
    public static class RuinedArchiveLevelBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity";
        private const string RootPath = "Assets/_Project/Scenes/Sandbox/Sirac/LevelMap";
        private const string SheetPath = RootPath + "/Art/SpikeSheet.png";
        private static Sprite square;
        private static Sprite[] spikes;
        private static Material lit;
        private static TMP_FontAsset font;
        private static Transform collisions, scenery, dangers, routeMarkers;
        private static RuinedArchiveRoute route;
        private static readonly Color Stone = new Color32(86, 82, 72, 255);
        private static readonly Color Edge = new Color32(145, 131, 105, 255);
        private static readonly Color Iron = new Color32(57, 58, 54, 255);
        private static readonly Color Brass = new Color32(156, 118, 67, 255);

        public static string ApplyToCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved MovementSandbox in Edit Mode first.");
            if (GameObject.Find("THE RUINED ARCHIVE / Level") != null)
                throw new InvalidOperationException("Layout already applied. Edit the level normally to preserve subsequent work.");

            PrepareSpikes();
            square = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Prototype/WhiteSquare.png").OfType<Sprite>().First();
            spikes = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Scenes/Sandbox/Sirac/TestChamber/ArchiveFont.asset");
            if (spikes.Length != 5 || square == null || lit == null || font == null)
                throw new InvalidOperationException("Required level assets are missing.");

            // Keep the existing player, lighting, effects, UI and audio objects and their tuning.
            var player = GameObject.Find("Player");
            var existingEmbers = UnityEngine.Object.FindObjectsByType<EmberPickup>(FindObjectsSortMode.None).OrderBy(e => e.transform.position.x).ToArray();
            if (player == null || existingEmbers.Length != 2) throw new InvalidOperationException("Expected the current player and two ember pickups.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Design Ruined Archive route");
            var oldGeometry = GameObject.Find("Greybox Geometry");
            if (oldGeometry != null) Undo.DestroyObjectImmediate(oldGeometry);
            var oldVoid = GameObject.Find("Void Pit");
            if (oldVoid != null) Undo.DestroyObjectImmediate(oldVoid);

            var root = new GameObject("THE RUINED ARCHIVE / Level");
            Undo.RegisterCreatedObjectUndo(root, "Create level");
            collisions = Group("01 / Walkable geometry", root.transform);
            scenery = Group("02 / Ruined architecture", root.transform);
            dangers = Group("03 / Spike hazards", root.transform);
            var emberRoot = Group("04 / Three ember reserves", root.transform);
            routeMarkers = Group("05 / Route and checkpoints", root.transform);
            route = root.AddComponent<RuinedArchiveRoute>();
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EmberPickup.prefab");
            route.Configure(player.GetComponent<PlayerRespawnController>(), emberRoot, pickupPrefab, font);

            Floor("Intake to bridge", -10f, 32f);
            Floor("Service landing", 34.2f, 46f);
            Floor("Exit landing", 48.2f, 62f);
            Solid("West wall", -10.4f, 3.5f, 0.8f, 9f, Stone);
            Solid("East wall", 62.4f, 3.5f, 0.8f, 9f, Stone);
            Room("01 / INTAKE", -10f, 1f, 4.0f);
            Room("02 / ARCHIVE AND OFFICE", 1f, 21.5f, 7.3f);
            Room("03 / SERVICE CROSSING", 21.5f, 37f, 4.8f);
            Room("04 / COLLAPSED GALLERY", 37f, 50f, 5.5f);
            Room("05 / ARCHIVE EXIT", 50f, 62f, 4.2f);

            Platform("Archive step 01", 2.7f, 0.9f, 2f);
            Platform("Archive step 02", 5.5f, 1.8f, 2f, 0.12f);
            Platform("Archive step 03", 8.3f, 2.7f, 2f);
            Platform("Upper office / optional route", 15.1f, 3.5f, 9.8f);
            Solid("Office desk", 15.5f, 3.82f, 1.6f, 0.64f, Iron);
            Quad("Desk top", 15.5f, 4.16f, 1.8f, 0.08f, Edge, 2);
            for (int i = 0; i < 3; i++)
            {
                float x = 11.4f + i * 3.8f;
                Quad("Office rear support", x, 1.65f, 0.17f, 3.3f, Iron, -18);
                Quad("Office balustrade", x + 0.55f, 3.9f, 0.045f, 0.75f, Iron, -8);
            }
            Quad("Office rail", 15.1f, 4.3f, 9.8f, 0.06f, Edge, -9);
            Shelf(-3.5f, 1.3f, 2.2f, 2.6f);
            Shelf(6.0f, 3.5f, 2.3f, 3.0f);
            Shelf(12.1f, 5.0f, 2.2f, 2.5f);
            Shelf(18.6f, 4.8f, 1.5f, 2.3f);
            Shelf(36f, 1.4f, 1.7f, 2.8f);
            Shelf(52f, 1.5f, 2.4f, 3f);

            Door(-8.4f, false);
            Door(58f, true);
            CollapsedArch(24.7f, 4.0f);
            CollapsedArch(39.3f, 4.7f);
            CollapsedArch(51f, 3.6f);
            SpikeField("Learning spikes / lower passage", 12.4f, 3, 0f);
            SpikeField("Service spikes", 27.2f, 3, 0f);
            Trench("Broken bridge", 32f, 34.2f);
            SpikeField("Gallery spikes", 41f, 4, 0f);
            Trench("Final crossing", 46f, 48.2f);

            var voidGo = new GameObject("Void safety plane", typeof(BoxCollider2D), typeof(HazardTrigger2D));
            voidGo.transform.SetParent(dangers);
            voidGo.transform.position = new Vector3(26f, -6f);
            voidGo.GetComponent<BoxCollider2D>().size = new Vector2(76f, 1f);
            voidGo.GetComponent<BoxCollider2D>().isTrigger = true;
            var voidData = new SerializedObject(voidGo.GetComponent<HazardTrigger2D>());
            voidData.FindProperty("deathReason").enumValueIndex = (int)DeathReason.Void;
            voidData.ApplyModifiedPropertiesWithoutUndo();

            Marker(-5f, ArchiveRouteVolume.VolumeKind.Hint, 0, "01 / INTAKE", "Walk safely. Hold SHIFT to sprint; release it to recover stamina.", 2.9f);
            Marker(1.0f, ArchiveRouteVolume.VolumeKind.Hint, 0, "02 / THE UPPER OFFICE", "The shelves lead to an optional ember. The lower passage has exposed spikes.", 3.0f);
            Marker(22.5f, ArchiveRouteVolume.VolumeKind.Checkpoint, 1, "REST POINT / 01", "Scout the floor. Clear the spikes, rest on the landing, then sprint-jump the broken bridge.", 2.7f);
            Marker(26.1f, ArchiveRouteVolume.VolumeKind.Hint, 0, "03 / SERVICE CROSSING", "SHIFT + SPACE crosses the hazards. Land before releasing SPACE for a full jump.", 3.0f);
            Marker(37.7f, ArchiveRouteVolume.VolumeKind.Checkpoint, 2, "REST POINT / 02", "Let stamina recover here. One final ember lies ahead of the gallery.", 3.0f);
            Marker(40f, ArchiveRouteVolume.VolumeKind.Hint, 0, "04 / THE COLLAPSED GALLERY", "Two separate jumps. A safe run-up and landing surround each hazard.", 3.0f);
            Marker(58f, ArchiveRouteVolume.VolumeKind.Exit, 0, "ARCHIVE REACHED", "Route complete. BACKSPACE restores all three embers and checkpoints.", 3.0f);

            Vector3[] positions = { new Vector3(-0.2f, 0.6f), new Vector3(15.5f, 4.65f), new Vector3(39.2f, 0.6f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var ember = i < 2 ? existingEmbers[i].gameObject : (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab);
                if (i < 2)
                {
                    Undo.SetTransformParent(ember.transform, emberRoot, "Place existing ember");
                    Undo.RecordObject(ember.transform, "Place existing ember");
                }
                else ember.transform.SetParent(emberRoot);
                ember.name = "Ember " + (i + 1).ToString("00") + (i == 1 ? " / upper office reward" : " / main route");
                ember.transform.position = positions[i];
            }
            Undo.RecordObject(player.transform, "Set route start");
            player.transform.position = new Vector3(-7.5f, 0.85f);
            var respawn = player.GetComponent<PlayerRespawnController>();
            Undo.RecordObject(respawn, "Set safe spawn");
            respawn.SpawnPosition = player.transform.position;
            respawn.VoidKillY = -5f;
            var camera = Camera.main;
            var follow = camera.GetComponent<FollowCamera2D>();
            Undo.RecordObject(camera, "Frame route");
            camera.orthographicSize = 3.7f;
            var followData = new SerializedObject(follow);
            followData.FindProperty("target").objectReferenceValue = player.transform;
            followData.FindProperty("enableBounds").boolValue = true;
            followData.FindProperty("boundsMin").vector2Value = new Vector2(-4.5f, 2.5f);
            followData.FindProperty("boundsMax").vector2Value = new Vector2(56.5f, 5.8f);
            followData.ApplyModifiedProperties();
            follow.SetTarget(player.transform, true);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(26f, 2f), new Vector3(75f, 13f, 1f)), false);
            return "MovementSandbox redesigned: 72m, optional upper office, three embers, two checkpoints, five spike fields, two 2.2m gaps. Existing player, UI, audio and lantern tuning retained.";
        }

        private static void PrepareSpikes()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = 640f;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects();
            int[] xs = { 156, 533, 917, 1303, 1685 };
            int[] widths = { 325, 323, 326, 325, 328 };
            var rects = new SpriteRect[5];
            for (int i = 0; i < 5; i++) rects[i] = new SpriteRect {
                name = "Spike " + (i + 1).ToString("00"), spriteID = old.Length == 5 ? old[i].spriteID : GUID.Generate(),
                rect = new Rect(xs[i], 186, widths[i], 274), pivot = new Vector2(0.5f, 0f), alignment = SpriteAlignment.BottomCenter
            };
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Quad(string name, float x, float y, float w, float h, Color color, int order = -20, Transform parent = null, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : scenery, false);
            go.transform.position = new Vector3(x, y);
            var child = new GameObject("Surface", typeof(SpriteRenderer));
            child.transform.SetParent(go.transform, false);
            var render = child.GetComponent<SpriteRenderer>();
            render.sprite = sprite != null ? sprite : square;
            render.sharedMaterial = lit;
            render.color = color;
            render.sortingOrder = order;
            var size = render.sprite.bounds.size;
            var scale = new Vector3(w / size.x, h / size.y, 1f);
            child.transform.localScale = scale;
            child.transform.localPosition = -Vector3.Scale(render.sprite.bounds.center, scale);
            return go;
        }

        private static GameObject Solid(string name, float x, float y, float w, float h, Color color)
        {
            var go = Quad(name, x, y, w, h, color, 0, collisions);
            go.AddComponent<BoxCollider2D>().size = new Vector2(w, h);
            // Use the unscaled collider as the shadow provider; scaled sprite providers can
            // over-occlude the 2D light when long floors / roofs are serialized and reloaded.
            go.AddComponent<ShadowCaster2D>().selfShadows = false;
            return go;
        }

        private static void Floor(string name, float left, float right)
        {
            Solid(name, (left + right) * 0.5f, -0.65f, right - left, 1.3f, Stone);
            Quad("Worn floor edge", (left + right) * 0.5f, -0.025f, right - left, 0.05f, Edge, 2);
            for (float x = left + 0.7f; x < right; x += 1.5f)
                Quad("Stone joint", x, -0.43f, 0.025f, 0.7f, Iron, 2);
        }

        private static void Platform(string name, float x, float top, float width, float thickness = 0.22f)
        {
            Solid(name, x, top - thickness / 2f, width, thickness, Iron);
            Quad("Worn shelf lip", x, top - 0.025f, width, 0.05f, Edge, 2);
            Quad("Shelf brace", x + width * 0.3f, top - 0.4f, 0.11f, 0.5f, Iron, -15).transform.rotation = Quaternion.Euler(0f, 0f, -32f);
        }

        private static void Room(string name, float left, float right, float height)
        {
            var parent = Group(name, scenery);
            Quad("Rear masonry", (left + right) / 2f, height / 2f, right - left, height, new Color32(43, 43, 39, 255), -60, parent);
            for (int row = 0; row < Mathf.CeilToInt(height); row++)
                for (float x = left + 0.5f + (row % 2) * 0.65f; x < right - 0.3f; x += 1.5f)
                    Quad("Weathered block", x, row + 0.45f, Mathf.Min(1.42f, 2f * (right - x)), 0.92f,
                        (row + Mathf.RoundToInt(x)) % 3 == 0 ? new Color32(55, 53, 46, 255) : new Color32(48, 48, 43, 255), -57, parent);
            Solid("Broken roof / " + name, (left + right) / 2f, height + 0.2f, right - left, 0.4f, Stone);
            for (float x = left + 1.8f; x < right; x += 4f)
            {
                Quad("Recessed pilaster", x, height / 2f, 0.28f, height, Iron, -40, parent);
                Quad("Crumbled cornice", x + 0.1f, height - 0.2f, 0.8f, 0.38f, Stone, -35, parent);
                Quad("Hairline wall fracture", x + 0.5f, height * 0.6f, 0.035f, 0.7f, new Color32(22, 25, 23, 255), -35, parent).transform.rotation = Quaternion.Euler(0f, 0f, 24f);
            }
            Quad("Low skirting", (left + right) / 2f, 0.16f, right - left, 0.22f, Iron, -34, parent);
        }

        private static void Shelf(float x, float y, float w, float h)
        {
            var parent = Group("Abandoned filing shelves", scenery);
            Quad("Cabinet recess", x, y, w, h, new Color32(26, 30, 29, 255), -32, parent);
            for (int row = 0; row < 4; row++)
            {
                float sy = y - h / 2f + row * h / 4f;
                Quad("Shelf", x, sy, w, 0.07f, Iron, -22, parent);
                for (int file = 0; file < 5; file++)
                {
                    if ((row + file) % 5 == 0) continue;
                    float fh = 0.23f + (row + file) % 3 * 0.06f;
                    Quad("Dusty record", x - w * 0.4f + file * w * 0.18f, sy + fh / 2f + 0.06f, w * 0.12f, fh,
                        file % 3 == 0 ? new Color32(103, 86, 62, 255) : new Color32(72, 75, 64, 255), -26, parent);
                }
            }
        }

        private static void Door(float x, bool exit)
        {
            Quad("Sealed doorway", x, 1.5f, 2.1f, 3f, new Color32(19, 25, 23, 255), -30);
            Quad("Door split", x, 1.5f, 0.04f, 2.9f, Iron, -15);
            for (int side = -1; side <= 1; side += 2) Quad("Chipped jamb", x + side * 1.1f, 1.5f, 0.22f, 3f, Stone, -10);
            Quad("Door lintel", x, 3f, 2.55f, 0.23f, Stone, -10);
            Quad(exit ? "Exit seal" : "Intake seal", x + 0.7f, 1.25f, 0.08f, 0.6f, Brass, -5);
        }

        private static void CollapsedArch(float x, float height)
        {
            Quad("Broken arch pier", x - 1.8f, 1.3f, 0.38f, 2.6f, Stone, -15);
            Quad("Broken arch crown", x, height, 3.8f, 0.35f, Stone, -15).transform.rotation = Quaternion.Euler(0f, 0f, -5f);
            Quad("Exposed tie beam", x + 0.3f, height - 0.4f, 2f, 0.12f, Iron, -14).transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            for (int i = 0; i < 3; i++) Quad("Rubble silhouette", x + 0.45f * i, 0.07f, 0.3f + 0.1f * i, 0.14f, Stone, -12);
        }

        private static void SpikeField(string name, float left, int count, float floor)
        {
            var parent = Group(name, dangers);
            float width = count * 0.44f;
            Quad("Recessed iron bed", left + width / 2f, floor + 0.015f, width, 0.05f, Iron, 1, parent);
            for (int i = 0; i < count; i++)
            {
                float x = left + i * 0.44f + 0.22f;
                var spike = Quad("Spike " + (i + 1), x, floor + 0.18f, 0.42f, 0.36f, new Color32(190, 181, 155, 255), 4, parent, spikes[i % 5]);
                var trigger = spike.AddComponent<PolygonCollider2D>();
                trigger.points = new[] { new Vector2(-0.18f, -0.16f), new Vector2(0.18f, -0.16f), new Vector2(0.18f, -0.08f), new Vector2(0f, 0.16f), new Vector2(-0.18f, -0.08f) };
                trigger.isTrigger = true;
                spike.AddComponent<HazardTrigger2D>();
            }
            if (floor >= 0f)
                for (int side = 0; side < 2; side++)
                    for (int i = 0; i < 3; i++) Quad("Worn warning paint", side == 0 ? left - 0.3f - i * 0.11f : left + width + 0.3f + i * 0.11f,
                        floor + 0.025f, 0.065f, 0.04f, Brass, 5, parent);
        }

        private static void Trench(string name, float left, float right)
        {
            Solid("Sunken bed / " + name, (left + right) / 2f, -1.55f, right - left, 0.45f, Iron);
            SpikeField(name + " / spikes", left, 5, -1.28f);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? left - 0.22f : right + 0.22f;
                Quad("Broken bridge nosing", x, -0.09f, 0.44f, 0.18f, Edge, 3);
                for (int i = 0; i < 3; i++) Quad("Gap warning paint", side == 0 ? x - 0.18f - i * 0.13f : x + 0.18f + i * 0.13f,
                    0.025f, 0.07f, 0.04f, Brass, 4);
            }
        }

        private static void Marker(float x, ArchiveRouteVolume.VolumeKind kind, int index, string title, string hint, float signY)
        {
            var go = new GameObject(title, typeof(BoxCollider2D), typeof(ArchiveRouteVolume));
            go.transform.SetParent(routeMarkers);
            go.transform.position = new Vector3(x, 2.7f);
            go.GetComponent<BoxCollider2D>().size = new Vector2(0.9f, 5.4f);
            go.GetComponent<BoxCollider2D>().isTrigger = true;
            var sign = new GameObject("Faded wall lettering", typeof(TextMeshPro));
            sign.transform.SetParent(go.transform, false);
            sign.transform.position = new Vector3(x, signY);
            var text = sign.GetComponent<TextMeshPro>();
            text.font = font;
            text.fontSize = 1.5f;
            text.text = title;
            text.color = new Color32(153, 145, 122, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(7f, 0.5f);
            sign.GetComponent<MeshRenderer>().sortingOrder = -8;
            var plaque = Quad("Route plaque", x, signY - 0.3f, 0.5f, 0.05f, Brass, -5).GetComponentInChildren<SpriteRenderer>();
            if (kind == ArchiveRouteVolume.VolumeKind.Checkpoint)
            {
                Quad("Rest pedestal", x + 0.6f, 0.55f, 0.3f, 1.1f, Iron, -10);
                Quad("Rest collar", x + 0.6f, 1.1f, 0.45f, 0.12f, Brass, -9);
            }
            go.GetComponent<ArchiveRouteVolume>().Configure(route, kind, title, hint, index, new Vector3(x, 0.85f), text, plaque);
        }
    }
}
