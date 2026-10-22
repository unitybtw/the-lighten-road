using System;
using System.Linq;
using TheLightenRoad.Audio;
using TheLightenRoad.Effects;
using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TheLightenRoad.Lantern.Glyphs;
using TheLightenRoad.Lantern.Relays;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.Sirac.FurkanPrototype;
using TheLightenRoad.Sirac.FurkanPrototype.Editor;
using TheLightenRoad.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.OfficeTutorial.Editor
{
    public static class OfficeTutorialBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/OfficeTutorial.unity";
        private const string OfficeSheetPath = "Assets/_Project/Art/Sprites/ofis.png";
        private const string FloorTilePath = "Assets/_Project/Art/TileMaps/TheOfficeTilemap/OfficeFloor.asset";
        private const string WallTilePath = "Assets/_Project/Art/TileMaps/TheOfficeTilemap/OfficeWall.asset";
        private const string FontPath = "Assets/_Project/Scenes/Sandbox/Sirac/TestChamber/ArchiveFont.asset";
        private const string LitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        private static Material litMaterial;
        private static TMP_FontAsset font;
        private static TileBase floorTile;
        private static TileBase wallTile;
        private static Sprite[] officeSprites;
        private static Sprite squareSprite;
        private static Transform root;
        private static FurkanLevelDirector director;

        [MenuItem("Tools/The Lighten Road/Create Office Tutorial Scene")]
        public static void CreateMenu()
        {
            Create(false);
        }

        public static void CreateBatchmode()
        {
            Create(true);
        }

        public static void Create(bool batchmode)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Save the current scene and leave Play Mode first.");

            LoadAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("OFFICE TUTORIAL / FURKAN LEVEL DESIGN").transform;
            director = root.gameObject.AddComponent<FurkanLevelDirector>();

            // 1. Player Setup
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerPrototype.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            director.FirstSpawn = new Vector3(1.0f, 0.85f, 0f);
            director.SecondSpawn = new Vector3(18.5f, 3.2f, 0f);
            player.transform.position = director.FirstSpawn;

            director.Player = player.GetComponent<PlayerRespawnController>();
            director.Player.SpawnPosition = director.FirstSpawn;
            director.Player.VoidKillY = -5.0f;

            // 2. Camera Setup
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(FollowCamera2D));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.backgroundColor = new Color(0.016f, 0.022f, 0.026f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraGo.transform.position = new Vector3(1.0f, 2.4f, -10f);

            var followCam = cameraGo.GetComponent<FollowCamera2D>();
            followCam.SetTarget(player.transform, true);
            var followSo = new SerializedObject(followCam);
            followSo.FindProperty("enableBounds").boolValue = true;
            followSo.FindProperty("boundsMin").vector2Value = new Vector2(3.5f, 2.8f);
            followSo.FindProperty("boundsMax").vector2Value = new Vector2(39.0f, 5.5f);
            followSo.ApplyModifiedPropertiesWithoutUndo();

            // 3. Ambient Lighting & Atmosphere
            var ambient = new GameObject("Archive Ambient Light", typeof(Light2D)).GetComponent<Light2D>();
            ambient.transform.SetParent(root, false);
            ambient.lightType = Light2D.LightType.Global;
            ambient.intensity = 0.04f;
            ambient.color = new Color(0.35f, 0.45f, 0.55f);

            var dustGo = new GameObject("Ambient Dust Motes");
            dustGo.transform.SetParent(root, false);
            dustGo.transform.position = new Vector3(20f, 4f, 0f);
            var dust = dustGo.AddComponent<DustMotesAtmosphere>();
            var dustSo = new SerializedObject(dust);
            dustSo.FindProperty("areaSize").vector2Value = new Vector2(48f, 12f);
            dustSo.FindProperty("maxParticles").intValue = 140;
            dustSo.FindProperty("emissionRate").floatValue = 12f;
            dustSo.FindProperty("moteColor").colorValue = new Color(1f, 0.95f, 0.82f, 0.45f);
            dustSo.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("Audio Ambience").AddComponent<BackgroundMusicManager>();

            // 4. HUD
            var hud = new GameObject("Lantern HUD").AddComponent<LanternHUD>();
            hud.SetTarget(player.GetComponent<LanternController>());
            new GameObject("Stamina HUD").AddComponent<StaminaHUD>().SetTarget(player.GetComponent<PlayerStamina>());
            new GameObject("Death Overlay").AddComponent<DeathScreenOverlay>().SetTarget(director.Player);
            new GameObject("Pause Menu").AddComponent<PauseMenuController>();

            // 5. Build Tilemaps & Level Design Architecture
            CreateTilemapsAndGeometry();
            CreatePlatformColliders();

            // 6. Office Props (Furniture, Obstacles, Lights)
            PlaceOfficeProps();

            // 7. Interactive Mechanics (Relay, Gate, Circuit, Platform, Memento)
            InstallMechanics();

            // 8. Diegetic Whispers & Triggers
            BuildPromptsAndTriggers();

            // 9. Completion Sequence
            InstallCompletion();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = root.gameObject;
            Debug.Log($"[OfficeTutorial] Scene successfully generated at {ScenePath}!");
        }

        private static void LoadAssets()
        {
            litMaterial = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(FloorTilePath);
            wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(WallTilePath);
            officeSprites = AssetDatabase.LoadAllAssetsAtPath(OfficeSheetPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            squareSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Prototype/WhiteSquare.png").OfType<Sprite>().First();

            if (litMaterial == null) throw new InvalidOperationException("Missing Lit Material!");
            if (font == null) throw new InvalidOperationException("Missing ArchiveFont!");
            if (floorTile == null) throw new InvalidOperationException("Missing OfficeFloor tile asset!");
            if (wallTile == null) throw new InvalidOperationException("Missing OfficeWall tile asset!");
            if (officeSprites == null || officeSprites.Length == 0) throw new InvalidOperationException("Missing Office sprite sheet!");
        }

        private static void CreateTilemapsAndGeometry()
        {
            var gridGo = new GameObject("Grid", typeof(Grid));
            gridGo.transform.SetParent(root, false);
            var grid = gridGo.GetComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            // Floor Tilemap (Walkable Collider)
            var floorGo = new GameObject("Tilemap_Floor", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D), typeof(CompositeCollider2D), typeof(Rigidbody2D));
            floorGo.transform.SetParent(gridGo.transform, false);
            var floorTm = floorGo.GetComponent<Tilemap>();
            var floorRend = floorGo.GetComponent<TilemapRenderer>();
            floorRend.material = litMaterial;
            floorRend.sortingOrder = 0;

            var floorRb = floorGo.GetComponent<Rigidbody2D>();
            floorRb.bodyType = RigidbodyType2D.Static;
            var floorTileCol = floorGo.GetComponent<TilemapCollider2D>();
            floorTileCol.compositeOperation = Collider2D.CompositeOperation.Merge;
            var floorCompCol = floorGo.GetComponent<CompositeCollider2D>();
            floorCompCol.geometryType = CompositeCollider2D.GeometryType.Polygons;

            // Wall Tilemap (Architecture & Ceilings)
            var wallGo = new GameObject("Tilemap_Wall", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D), typeof(CompositeCollider2D), typeof(Rigidbody2D));
            wallGo.transform.SetParent(gridGo.transform, false);
            var wallTm = wallGo.GetComponent<Tilemap>();
            var wallRend = wallGo.GetComponent<TilemapRenderer>();
            wallRend.material = litMaterial;
            wallRend.sortingOrder = 1;

            var wallRb = wallGo.GetComponent<Rigidbody2D>();
            wallRb.bodyType = RigidbodyType2D.Static;
            var wallTileCol = wallGo.GetComponent<TilemapCollider2D>();
            wallTileCol.compositeOperation = Collider2D.CompositeOperation.Merge;
            var wallCompCol = wallGo.GetComponent<CompositeCollider2D>();
            wallCompCol.geometryType = CompositeCollider2D.GeometryType.Polygons;

            // Background Tilemap (Depth & Visuals, No Colliders)
            var bgGo = new GameObject("Tilemap_Background", typeof(Tilemap), typeof(TilemapRenderer));
            bgGo.transform.SetParent(gridGo.transform, false);
            var bgTm = bgGo.GetComponent<Tilemap>();
            var bgRend = bgGo.GetComponent<TilemapRenderer>();
            bgRend.material = litMaterial;
            bgRend.sortingOrder = -20;
            bgTm.color = new Color(0.24f, 0.28f, 0.32f, 1f);

            // Paint Chamber 01 - Tutorial
            // Ground from x = -3 to 5 (top at y = 0)
            FillBox(floorTm, floorTile, -3, -2, 5, -1);

            // Jump 1 (Low Jump ~ 0.9m / 1 tile high)
            FillBox(floorTm, floorTile, 5, -2, 7, 0); // top at y = 1

            // Gap / transition
            FillBox(floorTm, floorTile, 7, -2, 8, -1);

            // Jump 2 (Medium Jump ~ 1.8m / 2 tiles high)
            FillBox(floorTm, floorTile, 8, -2, 10, 1); // top at y = 2

            // Raised Reception Landing (top at y = 3)
            FillBox(floorTm, floorTile, 11, -2, 17, 2);

            // Dividing Wall above Gate (from y = 5 to 9)
            FillBox(wallTm, wallTile, 17, 5, 18, 9);

            // Chamber 01 Outer Wall & Ceiling
            FillBox(wallTm, wallTile, -4, -2, -3, 9);
            FillBox(wallTm, wallTile, -3, 8, 17, 9);

            // Paint Chamber 02 - Beginnings
            // Intake landing just past gate (top at y = 2)
            FillBox(floorTm, floorTile, 17, -2, 19, 1);

            // Stepping Stone A (width 2, top at y = 2)
            FillBox(floorTm, floorTile, 20, 1, 21, 1);

            // Stepping Stone B (width 2, top at y = 2)
            FillBox(floorTm, floorTile, 23, 1, 24, 1);

            // Circuit Approach Landing (top at y = 1)
            FillBox(floorTm, floorTile, 25, -2, 28, 0);

            // Far Side Landing / Vein 2 (top at y = 1)
            FillBox(floorTm, floorTile, 37, -2, 43, 0);

            // Secret Memory Ledge (high upper detour for collectible, top at y = 6)
            FillBox(floorTm, floorTile, 32, 5, 35, 5);

            // Chamber 02 Ceiling & Outer East Wall
            FillBox(wallTm, wallTile, 18, 9, 43, 10);
            FillBox(wallTm, wallTile, 43, -2, 44, 9);

            // Background Fill (Backdrop depth for chambers)
            FillBox(bgTm, wallTile, -3, 0, 17, 7);
            FillBox(bgTm, wallTile, 17, 0, 43, 8);

            floorTm.RefreshAllTiles();
            wallTm.RefreshAllTiles();
            bgTm.RefreshAllTiles();

            floorCompCol.GenerateGeometry();
            wallCompCol.GenerateGeometry();
        }

        private static void CreatePlatformColliders()
        {
            var colRoot = new GameObject("Walkable Colliders");
            colRoot.transform.SetParent(root, false);

            AddCollider(colRoot.transform, "Safe Arrival Floor", 1.0f, -0.5f, 9.0f, 1.0f);
            AddCollider(colRoot.transform, "Jump 1 Step", 6.0f, 0.5f, 2.0f, 1.0f);
            AddCollider(colRoot.transform, "Jump 2 Step", 9.0f, 1.0f, 2.2f, 2.0f);
            AddCollider(colRoot.transform, "Reception Landing", 14.0f, 1.5f, 6.2f, 3.0f);
            AddCollider(colRoot.transform, "Chamber 2 Entrance", 18.0f, 1.0f, 2.2f, 2.0f);
            AddCollider(colRoot.transform, "Stepping Stone A", 20.75f, 1.5f, 1.6f, 1.0f);
            AddCollider(colRoot.transform, "Stepping Stone B", 23.75f, 1.5f, 1.6f, 1.0f);
            AddCollider(colRoot.transform, "Circuit Approach", 26.75f, 0.5f, 3.6f, 1.0f);
            AddCollider(colRoot.transform, "Far Side Landing", 40.25f, 0.5f, 6.6f, 1.0f);
            AddCollider(colRoot.transform, "Secret Memory Ledge", 33.75f, 5.5f, 3.6f, 1.0f);
        }

        private static void AddCollider(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(BoxCollider2D));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            var col = go.GetComponent<BoxCollider2D>();
            col.size = new Vector2(w, h);
        }

        private static void FillBox(Tilemap tm, TileBase tile, int minX, int minY, int maxX, int maxY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    tm.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        private static void PlaceOfficeProps()
        {
            var propsRoot = new GameObject("Office Props").transform;
            propsRoot.SetParent(root, false);

            var spriteDict = officeSprites.ToDictionary(s => s.name);

            // Chamber 01 - Scenic Tutorial Room
            if (spriteDict.TryGetValue("ofis_0", out var desk))
                SpawnProp(propsRoot, "Scenic_Desk_PC", desk, new Vector3(1.8f, 0.52f, 0f), 2);
            if (spriteDict.TryGetValue("ofis_4", out var chair))
                SpawnProp(propsRoot, "Scenic_Chair", chair, new Vector3(0.8f, 0.52f, 0f), 1);
            if (spriteDict.TryGetValue("ofis_1", out var tallBookcase))
                SpawnProp(propsRoot, "Scenic_Tall_Bookcase", tallBookcase, new Vector3(-1.5f, 1.0f, 0f), -5);
            if (spriteDict.TryGetValue("ofis_2", out var doubleCabinet))
                SpawnProp(propsRoot, "Scenic_Double_Cabinet", doubleCabinet, new Vector3(3.5f, 1.5f, 0f), -5);
            if (spriteDict.TryGetValue("ofis_8", out var cooler))
                SpawnProp(propsRoot, "Scenic_Water_Cooler", cooler, new Vector3(-2.2f, 0.8f, 0f), 2);

            // Chamber 01 Ceiling Fluorescent Lamps
            if (spriteDict.TryGetValue("ofis_7", out var lamp))
            {
                SpawnFluorescentLamp(propsRoot, "Lamp_Scenic_01", lamp, new Vector3(1.5f, 7.7f, 0f));
                SpawnFluorescentLamp(propsRoot, "Lamp_Jump_01", lamp, new Vector3(6.5f, 7.7f, 0f));
            }

            // Raised Reception Landing
            // 1. Knocked Over Reception Desk (Furkan's note: "KNOCKED OVER RECEPTION DESK CAN'T TOUCH, GET ON TOP. NO RIGIDBODY")
            if (spriteDict.TryGetValue("ofis_3", out var receptionDesk))
            {
                var deskGo = SpawnProp(propsRoot, "Knocked_Over_Reception_Desk", receptionDesk, new Vector3(14.2f, 3.8f, 0f), 3);
                // Vault surface collider on top of the desk
                var col = deskGo.AddComponent<BoxCollider2D>();
                col.size = new Vector2(2.8f, 0.4f);
                col.offset = new Vector2(0f, 0.65f);
            }
            if (spriteDict.TryGetValue("ofis_0", out var recDesk))
                SpawnProp(propsRoot, "Reception_Terminal", recDesk, new Vector3(12.0f, 3.52f, 0f), 2);
            if (spriteDict.TryGetValue("ofis_4", out var recChair))
                SpawnProp(propsRoot, "Reception_Chair", recChair, new Vector3(11.2f, 3.52f, 0f), 1);
            if (spriteDict.TryGetValue("ofis_6", out var clock))
                SpawnProp(propsRoot, "Wall_Clock", clock, new Vector3(13.5f, 5.5f, 0f), 2);
            if (spriteDict.TryGetValue("ofis_10", out var archiveCabinet))
                SpawnProp(propsRoot, "Archive_Filing_Cabinet", archiveCabinet, new Vector3(16.2f, 4.0f, 0f), -5);
            if (spriteDict.TryGetValue("ofis_7", out var recLamp))
                SpawnFluorescentLamp(propsRoot, "Lamp_Reception", recLamp, new Vector3(14.0f, 7.7f, 0f));

            // Chamber 02 - Beginnings Props
            // Note: NO ceiling lamps above chasm (x=20 to 25) so it remains pitch black!
            if (spriteDict.TryGetValue("ofis_10", out var c2Cabinet))
                SpawnProp(propsRoot, "Chamber2_Archive_Rack", c2Cabinet, new Vector3(26.2f, 2.0f, 0f), -5);
            if (spriteDict.TryGetValue("ofis_0", out var exitDesk))
                SpawnProp(propsRoot, "Exit_Archive_Desk", exitDesk, new Vector3(39.5f, 1.52f, 0f), 2);
            if (spriteDict.TryGetValue("ofis_1", out var exitBookcase))
                SpawnProp(propsRoot, "Exit_Tall_Bookcase", exitBookcase, new Vector3(42.0f, 2.0f, 0f), -5);
            if (spriteDict.TryGetValue("ofis_7", out var exitLamp))
            {
                SpawnFluorescentLamp(propsRoot, "Lamp_Circuit_Approach", exitLamp, new Vector3(27.0f, 8.7f, 0f));
                SpawnFluorescentLamp(propsRoot, "Lamp_Archive_Exit", exitLamp, new Vector3(40.0f, 8.7f, 0f));
            }
        }

        private static GameObject SpawnProp(Transform parent, string name, Sprite sprite, Vector3 pos, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = litMaterial;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static void SpawnFluorescentLamp(Transform parent, string name, Sprite lampSprite, Vector3 pos)
        {
            var go = SpawnProp(parent, name, lampSprite, pos, 5);

            var lightGo = new GameObject("Fluorescent_Light", typeof(Light2D));
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            var light = lightGo.GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.85f, 0.92f, 1.0f);
            light.intensity = 0.65f;
            light.pointLightOuterRadius = 4.8f;
            light.pointLightInnerRadius = 0.5f;
            light.falloffIntensity = 0.5f;
        }

        private static void InstallMechanics()
        {
            // 1. Photosensitive Relay & Archive Gate at Chamber Transition
            var relayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PhotosensitiveRelay.prefab");
            var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArchiveGate.prefab");

            var gateInstance = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, root);
            gateInstance.name = "ArchiveGate_Transition";
            gateInstance.transform.position = new Vector3(17.5f, 3.0f, 0f);
            var gateComp = gateInstance.GetComponent<ArchiveGate2D>();

            var relayInstance = (GameObject)PrefabUtility.InstantiatePrefab(relayPrefab, root);
            relayInstance.name = "PhotosensitiveRelay_Transition";
            relayInstance.transform.position = new Vector3(15.8f, 3.0f, 0f);
            var relayComp = relayInstance.GetComponent<PhotosensitiveRelay2D>();
            relayComp.Mode = RelayMode.Timed;
            relayComp.LingerDuration = 6.0f;

            if (gateComp != null) gateComp.Relay = relayComp;

            // Chamber Return Shutter (Closes behind player)
            var shutterGo = new GameObject("Chamber seal / closes behind you", typeof(SpriteRenderer), typeof(BoxCollider2D));
            shutterGo.transform.SetParent(root, false);
            shutterGo.transform.position = new Vector3(17.8f, 5.5f, 0f);
            shutterGo.transform.localScale = new Vector3(0.28f, 5.5f, 1f);
            var shutterSr = shutterGo.GetComponent<SpriteRenderer>();
            shutterSr.sprite = squareSprite;
            shutterSr.sharedMaterial = litMaterial;
            shutterSr.color = new Color32(39, 46, 44, 255);
            shutterGo.SetActive(false);
            director.ReturnShutter = shutterGo;

            // 2. Hazard Pit in Chasm
            var pit = new GameObject("Hazard_Abyss_Pit", typeof(BoxCollider2D), typeof(HazardTrigger2D));
            pit.transform.SetParent(root, false);
            pit.transform.position = new Vector3(22.5f, -4.5f, 0f);
            var pitCol = pit.GetComponent<BoxCollider2D>();
            pitCol.size = new Vector2(16f, 1.5f);
            pitCol.isTrigger = true;

            // 3. Shared Ember Circuit & Patrol Platform
            var circuitGo = new GameObject("Shared Ember Circuit");
            circuitGo.transform.SetParent(root, false);
            director.Circuit = circuitGo.AddComponent<ArchiveCircuit>();

            // Patrol Platform
            var platformGo = new GameObject("Four-Stop Patrol Platform", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D), typeof(ArchivePatrolPlatform));
            platformGo.transform.SetParent(root, false);
            platformGo.transform.position = new Vector3(29.8f, 1.4f, 0f);

            var platSr = platformGo.GetComponent<SpriteRenderer>();
            platSr.sprite = squareSprite;
            platSr.sharedMaterial = litMaterial;
            platSr.color = new Color32(66, 70, 64, 255);
            platformGo.transform.localScale = new Vector3(2.2f, 0.35f, 1f);

            var platformLip = new GameObject("Platform Lip", typeof(SpriteRenderer));
            platformLip.transform.SetParent(platformGo.transform, false);
            platformLip.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            platformLip.transform.localScale = new Vector3(1f, 0.15f, 1f);
            var lipSr = platformLip.GetComponent<SpriteRenderer>();
            lipSr.sprite = squareSprite;
            lipSr.sharedMaterial = litMaterial;
            lipSr.color = new Color32(126, 119, 96, 255);
            lipSr.sortingOrder = 4;

            var platform = platformGo.GetComponent<ArchivePatrolPlatform>();
            platform.Waypoints = new[]
            {
                new Vector2(29.8f, 1.4f), // Waypoint 1: Slot / Stop Spot
                new Vector2(36.5f, 1.4f), // Waypoint 2: Vein 2 Landing
                new Vector2(36.5f, 6.2f), // Waypoint 3: Secret Memory Ledge
                new Vector2(29.8f, 6.2f)  // Waypoint 4: Return loop
            };
            platform.Speed = 2.0f;
            platform.CornerPause = 1.0f; // Exactly 1 second delay between stops
            director.Circuit.Platform = platform;

            // Waypoint markers
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = platform.Waypoints[i], b = platform.Waypoints[(i + 1) % 4];
                for (int dot = 0; dot < 12; dot++)
                {
                    Vector2 p = Vector2.Lerp(a, b, dot / 12f);
                    var dotGo = new GameObject("Rail_Dot", typeof(SpriteRenderer));
                    dotGo.transform.SetParent(root, false);
                    dotGo.transform.position = new Vector3(p.x, p.y, 0f);
                    dotGo.transform.localScale = new Vector3(0.04f, 0.04f, 1f);
                    var dotSr = dotGo.GetComponent<SpriteRenderer>();
                    dotSr.sprite = squareSprite;
                    dotSr.sharedMaterial = litMaterial;
                    dotSr.color = new Color(0.35f, 0.42f, 0.38f);
                    dotSr.sortingOrder = -12;
                }
            }

            // Circuit Nodes
            director.Nodes = new[]
            {
                CreateCircuitNode("Vein 1 / DEPOSIT", 27.8f, 1.65f),
                CreateCircuitNode("Vein 2 / RECOVER", 37.8f, 1.65f)
            };

            // Ember Pickups
            var pickups = new GameObject("Chamber 02 / Ember Reserves");
            pickups.transform.SetParent(root, false);
            director.PickupRoot = pickups.transform;
            director.PickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EmberPickup.prefab");

            var reserveEmber = (GameObject)PrefabUtility.InstantiatePrefab(director.PickupPrefab, pickups.transform);
            reserveEmber.name = "Reserve Ember / Power the Platform";
            reserveEmber.transform.position = new Vector3(26.5f, 1.8f, 0f);

            // 4. Secret Detour: Collectible Memento (Green Star in visual)
            var mementoGo = new GameObject("Optional Archive Fragment", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(ArchiveMemento));
            mementoGo.transform.SetParent(root, false);
            mementoGo.transform.position = new Vector3(33.5f, 6.6f, 0f);
            mementoGo.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

            var memSr = mementoGo.GetComponent<SpriteRenderer>();
            memSr.sprite = squareSprite;
            memSr.sharedMaterial = litMaterial;
            memSr.color = new Color(0.35f, 0.95f, 0.65f);
            memSr.sortingOrder = 5;

            var memCol = mementoGo.GetComponent<BoxCollider2D>();
            memCol.isTrigger = true;

            var mementoLight = new GameObject("Memento_Glow", typeof(Light2D));
            mementoLight.transform.SetParent(mementoGo.transform, false);
            var mLight = mementoLight.GetComponent<Light2D>();
            mLight.lightType = Light2D.LightType.Point;
            mLight.color = new Color(0.25f, 0.95f, 0.55f);
            mLight.intensity = 0.8f;
            mLight.pointLightOuterRadius = 3.2f;

            director.Memento = mementoGo.GetComponent<ArchiveMemento>();
            director.Memento.Director = director;

            // Crossing Guide Rune (flare glyph)
            var glyphPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FlareGlyph.prefab");
            if (glyphPrefab != null)
            {
                var runeGo = (GameObject)PrefabUtility.InstantiatePrefab(glyphPrefab, root);
                runeGo.name = "Glyph / Crossing Guide";
                runeGo.transform.position = new Vector3(20.5f, 3.5f, 0f);
                var rune = runeGo.GetComponent<FlareGlyph>();
                var runeSo = new SerializedObject(rune);
                runeSo.FindProperty("glyphType").enumValueIndex = (int)GlyphType.EmberEye;
                runeSo.FindProperty("glowColor").colorValue = new Color(0.31f, 0.95f, 0.82f, 1f);
                runeSo.FindProperty("activationDistance").floatValue = 9.5f;
                runeSo.FindProperty("lingerDuration").floatValue = 4.0f;
                runeSo.FindProperty("inscriptionText").stringValue = "Focus the flame; ancient eyes guide your stride.";
                runeSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static ArchiveCircuitNode CreateCircuitNode(string title, float x, float y)
        {
            var go = new GameObject(title, typeof(SpriteRenderer), typeof(ArchiveCircuitNode));
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(0.25f, 0.65f, 1f);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = squareSprite;
            sr.sharedMaterial = litMaterial;
            sr.color = new Color(0.35f, 0.59f, 0.64f);
            sr.sortingOrder = 2;

            var node = go.GetComponent<ArchiveCircuitNode>();
            node.Circuit = director.Circuit;
            node.Core = sr;

            // Point Light for node core
            var lightGo = new GameObject("Core_Light", typeof(Light2D));
            lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.35f, 0.75f, 0.85f);
            light.intensity = 0.5f;
            light.pointLightOuterRadius = 2.2f;

            return node;
        }

        private static void BuildPromptsAndTriggers()
        {
            // UI Overlay Canvas for instructions
            var canvasGo = new GameObject("Tutorial Prompt Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(root, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var labelGo = new GameObject("Instruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 127f);
            rect.sizeDelta = new Vector2(1150f, 85f);

            var text = labelGo.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 23;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.90f, 0.87f, 0.75f);
            director.Prompt = text;

            // Diegetic control prompts in the world
            DiegeticWhisper.Spawn(new Vector3(1.5f, 2.2f, 0f), "WASD", "TRAVERSE THE ARCHIVE CORRIDORS", WhisperAction.Move, "tut_wasd", root);
            DiegeticWhisper.Spawn(new Vector3(4.5f, 2.2f, 0f), "SPACE", "LEAP ACROSS THE RISING PLATFORMS", WhisperAction.Jump, "tut_jump", root);
            DiegeticWhisper.Spawn(new Vector3(12.5f, 4.5f, 0f), "SHIFT", "SPRINT TO VAULT THE DESK", WhisperAction.Sprint, "tut_sprint", root);
            DiegeticWhisper.Spawn(new Vector3(18.5f, 3.8f, 0f), "F", "IGNITE FLARE TO PIERCE DARKNESS", WhisperAction.Flare, "tut_flare", root);
            DiegeticWhisper.Spawn(new Vector3(26.5f, 2.6f, 0f), "E", "TRANSFER EMBER TO POWER LIFT", WhisperAction.Interact, "tut_circuit", root);

            // Tutorial Area Boundary Triggers
            CreateTrigger("Jump prompt trigger", 4.0f, false, false, Vector3.zero, "SPACE / JUMP\nThe first blocks teach your jump height. SHIFT is optional.");
            CreateTrigger("Desk prompt trigger", 13.5f, false, false, Vector3.zero, "RECEPTION\nThe fallen desk is solid scenery. Jump onto it, then continue right.");
            CreateTrigger("Chamber boundary trigger", 17.8f, true, false, new Vector3(18.5f, 3.2f, 0f), "CHAMBER 02 / BEGINNINGS\nThree embers restored. F reveals the dark crossing ahead.");
            CreateTrigger("Circuit prompt trigger", 26.7f, false, false, Vector3.zero, "ONE CIRCUIT / TWO NODES\nE deposits one ember. Ride the shelf; recover it on the far side to freeze it.");
        }

        private static void CreateTrigger(string name, float x, bool beginsChamber, bool isExit, Vector3 spawn, string message)
        {
            var go = new GameObject(name, typeof(BoxCollider2D), typeof(ArchiveTutorialTrigger));
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, 4f, 0f);

            var col = go.GetComponent<BoxCollider2D>();
            col.size = new Vector2(0.55f, 9f);
            col.isTrigger = true;

            var trigger = go.GetComponent<ArchiveTutorialTrigger>();
            trigger.Director = director;
            trigger.BeginsChamber = beginsChamber;
            trigger.IsExit = isExit;
            trigger.Spawn = spawn;
            trigger.Message = message;
        }

        private static void InstallCompletion()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ChamberCompletionProfile>(CompletionSetup.ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ChamberCompletionProfile>();
                profile.Font = font;
                profile.CompletionSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Completion/ArchiveArrival.wav");
                AssetDatabase.CreateAsset(profile, CompletionSetup.ProfilePath);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompletionSetup.PrefabPath);
            if (prefab != null)
            {
                var exit = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                exit.name = "Chamber Exit";
                exit.transform.position = new Vector3(41.5f, 3.0f, 0f);

                var sequence = exit.GetComponent<ChamberCompletionSequence>();
                sequence.DestinationScene = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity";
                exit.GetComponent<ChamberExitTrigger>().Player = director.Player;
                UnityEventTools.AddPersistentListener(sequence.Started, director.Complete);
                director.Completion = sequence;
            }
        }

        private static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
