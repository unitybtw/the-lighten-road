using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLightenRoad.Audio;
using TheLightenRoad.Codex;
using TheLightenRoad.Effects;
using TheLightenRoad.Frontend;
using TheLightenRoad.Interaction;
using TheLightenRoad.Lantern;
using TheLightenRoad.Lantern.Glyphs;
using TheLightenRoad.Lantern.Relays;
using TheLightenRoad.Mechanisms;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.Sirac.OfficeAnnex;
using TheLightenRoad.Sirac.OfficeAnnex.Editor;
using TheLightenRoad.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TheLightenRoad.Sirac.Level1.Editor
{
    /// <summary>Builds Sirac's playable Level 1 Office scene from Maksim's tilesets, furniture sheet, and core gameplay systems.</summary>
    public static class SiracLevel1Builder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/SiracLevel1.unity";
        private const string FolderRoot = "Assets/_Project/Scenes/Sandbox/Sirac/SiracLevel1";
        private const string PropsSheetPath = FolderRoot + "/Art/MaksimOfficeProps.png";
        private const string TileRoot = "Assets/_Project/Art/TileMaps/OfficeAnnex/Tiles";

        private static readonly (string Name, Rect Rect)[] MaksimPropRects =
        {
            ("Sofa", new Rect(3, 83, 66, 33)),
            ("Armchair_Right", new Rect(121, 83, 32, 33)),
            ("Armchair_Front", new Rect(162, 83, 30, 33)),
            ("Armchair_Left", new Rect(201, 83, 32, 33)),
            ("WallShelf", new Rect(244, 111, 34, 5)),
            ("FlowerPot", new Rect(94, 67, 25, 16)),
            ("BooksSmall", new Rect(134, 68, 8, 10)),
            ("BooksLarge", new Rect(144, 68, 11, 10)),
            ("CoffeeTable", new Rect(159, 51, 37, 14)),
            ("WaterCooler_Left", new Rect(242, 35, 16, 43)),
            ("WaterCooler_Front", new Rect(264, 35, 15, 43)),
            ("WaterCooler_Right", new Rect(285, 35, 16, 43)),
            ("BrokenDebris", new Rect(94, 51, 31, 9)),
            ("DeskLamp", new Rect(137, 51, 9, 12)),
            ("LeakingBin", new Rect(204, 46, 11, 18)),
            ("FilingCabinet_Right", new Rect(139, 3, 21, 29)),
            ("FilingCabinet_Front", new Rect(169, 3, 16, 29)),
            ("FilingCabinet_Left", new Rect(194, 3, 21, 29))
        };

        private sealed class BuildContext
        {
            public Transform LevelRoot;
            public Transform ArchitectureGroup;
            public Transform PropsGroup;
            public Transform LightsGroup;
            public Transform GameplayGroup;
            public Material LitMaterial;
            public Material BeamMaterial;
            public Sprite WhiteSprite;
            public Dictionary<string, Sprite> MaksimProps;
            public Dictionary<string, Sprite> OfisProps;
            public Dictionary<string, Sprite> WorldArt;
            public Tile RearWallTile;
            public Sprite GateSprite;
            public Sprite PedestalSprite;
            public Sprite CrystalSprite;
            public Tile[][] Tiles;
            public PlayerRespawnController Player;
            public Transform CameraTransform;
            public Rect[] CarvedAirRects;
            public SiracLevel1Route Route;
        }

        [MenuItem("Tools/The Lighten Road/Level 1 (Sirac)/Create or Rebuild Level 1")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding Level 1.");
            BuildInternal();
        }

        [MenuItem("Tools/The Lighten Road/Level 1 (Sirac)/Open Level 1")]
        public static void Open() => EditorSceneManager.OpenScene(ScenePath);

        /// <summary>Headless entry point invoked via Unity -executeMethod.</summary>
        public static void BuildForBatch()
        {
            BuildInternal();
        }

        private static void BuildInternal()
        {
            Directory.CreateDirectory(FolderRoot + "/Art");
            EnsureOfficeTilesExist();
            SliceMaksimProps();
            EnsureMechanismSprites();
            var worldArt = BakeWorldArtSprites();
            var beamMat = EnsureBeamMaterial();
            var rearWallTile = EnsureArchiveWallTile(worldArt);

            // Create the target scene before loading ScriptableObject/Sprite assets so NewScene does not unload them.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ctx = new BuildContext
            {
                LitMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat"),
                BeamMaterial = beamMat,
                WhiteSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Prototype/WhiteSquare.png").OfType<Sprite>().First(),
                MaksimProps = AssetDatabase.LoadAllAssetsAtPath(PropsSheetPath).OfType<Sprite>().ToDictionary(s => s.name),
                OfisProps = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites/ofis.png").OfType<Sprite>().ToDictionary(s => s.name),
                WorldArt = worldArt,
                RearWallTile = rearWallTile,
                GateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FolderRoot + "/Art/ArchiveGate.png"),
                PedestalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FolderRoot + "/Art/RelayPedestal.png"),
                CrystalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FolderRoot + "/Art/RelayCrystal.png"),
                Tiles = LoadOfficeTiles()
            };
            ctx.LevelRoot = new GameObject("LEVEL 1 / CORPORATE ARCHIVE").transform;
            ctx.Route = ctx.LevelRoot.gameObject.AddComponent<SiracLevel1Route>();
            ctx.ArchitectureGroup = Group("Architecture", ctx.LevelRoot);
            ctx.PropsGroup = Group("Furniture & Props", ctx.LevelRoot);
            ctx.LightsGroup = Group("Lighting", ctx.LevelRoot);
            ctx.GameplayGroup = Group("Puzzles & Pickups", ctx.LevelRoot);

            BuildTileGeometry(ctx);
            BuildBackdropDetails(ctx);
            BuildPlayerAndCamera(ctx);
            BuildFurnitureAndWindows(ctx);
            BuildLightingAndAtmosphere(ctx);
            BuildGameplayAndPuzzles(ctx);
            BuildHudAndSystems(ctx);

            Physics2D.SyncTransforms();
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings();
            AssetDatabase.SaveAssets();
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void EnsureOfficeTilesExist()
        {
            if (AssetDatabase.LoadAssetAtPath<Tile>(TileRoot + "/TileMapOffice_Gray_1_0.asset") == null)
            {
                OfficeAnnexBuilder.BuildTiles();
            }
        }

        private static Tile[][] LoadOfficeTiles()
        {
            string[] sheets = { "TileMapOffice_Gray_1", "TileMapOffice_Gray_2", "TileMapOffice_Blue_1", "TileMapOffice_Blue_2" };
            return sheets.Select(s => Enumerable.Range(0, 9)
                .Select(i => AssetDatabase.LoadAssetAtPath<Tile>($"{TileRoot}/{s}_{i}.asset")).ToArray()).ToArray();
        }

        private static void EnsureMechanismSprites()
        {
            CopySpriteAsset("Assets/_Project/Art/Sprites/ArchiveGate.png", FolderRoot + "/Art/ArchiveGate.png", 16f, new Vector2(0.5f, 0f));
            CopySpriteAsset("Assets/_Project/Art/Sprites/RelayPedestal.png", FolderRoot + "/Art/RelayPedestal.png", 32f, new Vector2(0.5f, 0f));
            CopySpriteAsset("Assets/_Project/Art/Sprites/RelayCrystal.png", FolderRoot + "/Art/RelayCrystal.png", 24f, new Vector2(0.5f, 0.5f));
        }

        private static Material EnsureBeamMaterial()
        {
            string matPath = FolderRoot + "/Art/ProjectorBeam.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Sprites/Default");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static Tile EnsureArchiveWallTile(Dictionary<string, Sprite> worldArt)
        {
            string tilePath = FolderRoot + "/Art/ArchiveWallTile.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            if (worldArt != null && worldArt.TryGetValue("ArchiveWallTile", out var sprite) && sprite != null)
            {
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
            }
            return tile;
        }

        private static void CopySpriteAsset(string sourcePath, string destPath, float ppu, Vector2 pivot)
        {
            bool changed = false;
            if (!File.Exists(destPath))
            {
                File.Copy(sourcePath, destPath, true);
                AssetDatabase.ImportAsset(destPath);
                changed = true;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(destPath);
            if (importer.textureType != TextureImporterType.Sprite || Math.Abs(importer.spritePixelsPerUnit - ppu) > 0.01f)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        private static Dictionary<string, Sprite> SliceMaksimProps()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(PropsSheetPath);
            if (importer == null)
                throw new InvalidOperationException("Missing MaksimOfficeProps.png at " + PropsSheetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[MaksimPropRects.Length];
            var names = new SpriteNameFileIdPair[MaksimPropRects.Length];
            for (int i = 0; i < MaksimPropRects.Length; i++)
            {
                var (name, rect) = MaksimPropRects[i];
                var id = new GUID(Hash128.Compute(PropsSheetPath + "/" + name).ToString());
                rects[i] = new SpriteRect
                {
                    name = name,
                    spriteID = id,
                    rect = rect,
                    alignment = SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f)
                };
                names[i] = new SpriteNameFileIdPair(name, id);
            }

            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);
            provider.Apply();
            importer.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(PropsSheetPath).OfType<Sprite>().ToDictionary(s => s.name);
        }

        private static void BuildTileGeometry(BuildContext ctx)
        {
            var grid = new GameObject("Office Grid", typeof(Grid)).transform;
            grid.SetParent(ctx.ArchitectureGroup, false);

            var carvedRects = new[]
            {
                // Tier 2 (Upper Archive): Main Corridor, Blast Arch, Shaft Top Landing, Attic & Secret Vault, Sunken Pockets
                new RectInt(0, 0, 48, 5),      // Upper Main Corridor (x: 0..48, y: 0..5)
                new RectInt(48, 0, 1, 3),      // Blast Door Archway (x: 48..49, y: 0..3)
                new RectInt(49, 0, 4, 5),      // Shaft Top Landing (x: 49..53, y: 0..5)
                new RectInt(19, 5, 4, 3),      // Attic Chimney (x: 19..23, y: 5..8)
                new RectInt(13, 8, 13, 5),     // Attic Conduit Chamber (x: 13..26, y: 8..13)
                new RectInt(26, 8, 1, 3),      // Attic Secret Archway (x: 26..27, y: 8..11)
                new RectInt(27, 8, 5, 5),      // Attic Secret Vault (x: 27..32, y: 8..13)
                new RectInt(22, -5, 5, 5),     // Pocket 1 / Sunken Vein Chamber (x: 22..27, y: -5..0)
                new RectInt(30, -3, 12, 3),    // Pocket 2 / Flooded Hall (x: 30..42, y: -3..0)
                new RectInt(32, -4, 7, 1),     // Pocket 2 Water Trough (x: 32..39, y: -4..-3)

                // East Vertical Shaft & Mid-Shaft Ember Alcove
                new RectInt(53, -15, 5, 20),   // East Vertical Shaft (x: 53..58, y: -15..5)
                new RectInt(50, -8, 3, 3),     // Mid-Shaft Crimson Alcove (x: 50..53, y: -8..-5)

                // Tier 1 (Lower Wing): Ghost Bay, Mirror Vault, Approach & Core Sanctuary
                new RectInt(46, -15, 7, 5),    // Lower East Ghost Bay (x: 46..53, y: -15..-10)
                new RectInt(45, -15, 1, 3),    // Ghost Door Archway (x: 45..46, y: -15..-12)
                new RectInt(40, -15, 5, 5),    // Lower Ghost Annex (x: 40..45, y: -15..-10)
                new RectInt(24, -15, 16, 7),   // Mirror Vault (x: 24..40, y: -15..-8)
                new RectInt(14, -15, 10, 5),   // Lower West Approach (x: 14..24, y: -15..-10)
                new RectInt(13, -15, 1, 3),    // Security Gate Archway (x: 13..14, y: -15..-12)
                new RectInt(0, -15, 13, 6),    // Central Core Sanctuary (x: 0..13, y: -15..-9)

                // Sub-Levels: Lower Well & Deep Shadow Realm Vault
                new RectInt(18, -21, 3, 6),    // Lower Well (x: 18..21, y: -21..-15)
                new RectInt(25, -19, 3, 4),    // Shadow Vault Neck (x: 25..28, y: -19..-15)
                new RectInt(26, -25, 22, 6)    // Deep Shadow Vault (x: 26..48, y: -25..-19)
            };

            var carved = new HashSet<Vector3Int>();
            foreach (var r in carvedRects)
                foreach (var p in r.allPositionsWithin)
                    carved.Add(new Vector3Int(p.x, p.y, 0));

            // 4-tile solid stepped masonry envelope enclosing all carved chambers
            var solidCells = new HashSet<Vector3Int>();
            foreach (var r in carvedRects)
            {
                for (int x = r.xMin - 4; x < r.xMax + 4; x++)
                    for (int y = r.yMin - 4; y < r.yMax + 4; y++)
                        solidCells.Add(new Vector3Int(x, y, 0));
            }
            // Seal the 1-tile seam between Tier 2 West floor (-4) and Sanctuary ceiling (-5)
            for (int x = -4; x < 18; x++)
                for (int y = -6; y < -3; y++)
                    solidCells.Add(new Vector3Int(x, y, 0));
            solidCells.ExceptWith(carved);

            var solidMap = CreateTilemap(ctx, grid, "Terrain - Carved Masonry", true, 0, Color.white);
            foreach (var cell in solidCells)
            {
                solidMap.SetTile(cell, ResolveCarvedTile(ctx.Tiles, carved, cell, 0));
            }
            solidMap.RefreshAllTiles();

            var rb = solidMap.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = solidMap.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            var collider = solidMap.gameObject.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            collider.ProcessTilemapChanges();
            composite.GenerateGeometry();

            ctx.CarvedAirRects = carvedRects.Select(r => new Rect(r.x, r.y, r.width, r.height)).ToArray();

            // Clean matte dark slate rear wall only behind carved rooms (+1 border overlap behind bevel edges)
            var rearCells = new HashSet<Vector3Int>();
            foreach (var p in carved)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        rearCells.Add(p + new Vector3Int(dx, dy, 0));
            }

            var rearWall = CreateTilemap(ctx, grid, "Rear Office Wall", false, -45, new Color(0.28f, 0.31f, 0.36f, 1f));
            foreach (var cell in rearCells)
            {
                rearWall.SetTile(cell, ctx.RearWallTile != null ? ctx.RearWallTile : ctx.Tiles[2][4]);
            }
            rearWall.RefreshAllTiles();
        }

        private static Tilemap CreateTilemap(BuildContext ctx, Transform grid, string name, bool solid, int sortingOrder, Color tint)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var map = go.GetComponent<Tilemap>();
            map.color = tint;
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = ctx.LitMaterial;
            renderer.sortingOrder = sortingOrder;
            return map;
        }

        private static Tile ResolveCarvedTile(Tile[][] tiles, HashSet<Vector3Int> carved, Vector3Int p, int baseSheet)
        {
            // Only interior faces bordering carved voids receive crisp gray bevels; outer casing stays pure black.
            bool SolidAt(int dx, int dy) => !carved.Contains(p + new Vector3Int(dx, dy, 0));
            bool n = SolidAt(0, 1), e = SolidAt(1, 0), s = SolidAt(0, -1), w = SolidAt(-1, 0);
            int col = !w ? 0 : !e ? 2 : 1;
            int row = !n ? 0 : !s ? 2 : 1;
            int selected = row * 3 + col;
            int source = baseSheet;

            if (n && e && s && w)
            {
                if (!SolidAt(1, 1)) { source++; selected = 6; }
                else if (!SolidAt(-1, 1)) { source++; selected = 8; }
                else if (!SolidAt(1, -1)) { source++; selected = 0; }
                else if (!SolidAt(-1, -1)) { source++; selected = 2; }
            }

            return tiles[source][selected];
        }

        private static void BuildBackdropDetails(BuildContext ctx)
        {
            // Carved stone rooms read cleanly on their own without fake trim or pilaster quads.
        }

        private static void BuildPlayerAndCamera(BuildContext ctx)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerPrototype.prefab");
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(2.2f, 0.85f, 0f);
            ctx.Player = playerGo.GetComponent<PlayerRespawnController>();
            ctx.Player.SpawnPosition = playerGo.transform.position;
            ctx.Player.VoidKillY = -32f;
            ctx.Route.Player = ctx.Player;

            var ledge = playerGo.GetComponent<PlayerLedgeClimb>();
            if (ledge != null)
            {
                var ledgeSo = new SerializedObject(ledge);
                var maxLip = ledgeSo.FindProperty("maximumLipHeight");
                if (maxLip != null) maxLip.floatValue = 2.05f;
                ledgeSo.ApplyModifiedPropertiesWithoutUndo();
            }

            ctx.Route.TraversalRig = playerGo.GetComponent<Level1TraversalRig>() ?? playerGo.AddComponent<Level1TraversalRig>();
            ctx.Route.LensController = playerGo.GetComponent<LanternLensController>() ?? playerGo.AddComponent<LanternLensController>();
            var inv = playerGo.GetComponent<EmberInventoryController>() ?? playerGo.AddComponent<EmberInventoryController>();
            var invSo = new SerializedObject(inv);
            var startProp = invSo.FindProperty("startingEmbers");
            startProp.arraySize = 3;
            startProp.GetArrayElementAtIndex(0).enumValueIndex = (int)EmberColor.Orange;
            startProp.GetArrayElementAtIndex(1).enumValueIndex = (int)EmberColor.Cyan;
            startProp.GetArrayElementAtIndex(2).enumValueIndex = (int)EmberColor.Crimson;
            invSo.ApplyModifiedPropertiesWithoutUndo();
            ctx.Route.EmberInventory = inv;

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(FollowCamera2D));
            camGo.tag = "MainCamera";
            ctx.CameraTransform = camGo.transform;
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.85f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.016f);
            camGo.transform.position = new Vector3(4.5f, 2.0f, -10f);

            var follow = camGo.GetComponent<FollowCamera2D>();
            follow.Offset = new Vector3(0f, 1.15f, -10f);
            follow.SmoothTime = 0.16f;
            follow.LookAheadDistance = 0.7f;
            follow.IdleLookAheadDistance = 0.25f;
            follow.FallLeadDistance = 0.65f;
            follow.SetWorldBounds(new Rect(-4f, -29f, 66f, 46f), 3.85f);
            follow.SetTarget(playerGo.transform, true);
        }

        private static void BuildFurnitureAndWindows(BuildContext ctx)
        {
            // Tier 2 West Lounge & Corridor (solid floor y = 0, x = 0..22)
            AddMaksimProp(ctx, "Reception Sofa", "Sofa", new Vector2(4.2f, 0f), -2);
            AddMaksimProp(ctx, "Lounge Coffee Table", "CoffeeTable", new Vector2(6.2f, 0f), -2);
            AddMaksimProp(ctx, "Lounge Desk Lamp", "DeskLamp", new Vector2(6.2f, 0.4375f), -1);
            AddMaksimProp(ctx, "Lounge Armchair Left", "Armchair_Left", new Vector2(7.7f, 0f), -2);
            AddMaksimProp(ctx, "Lounge Potted Plant", "FlowerPot", new Vector2(9.0f, 0f), -2);
            AddOfisProp(ctx, "Reception Desk", "ofis_0", new Vector2(11.0f, 0.516f), -2);
            AddOfisProp(ctx, "Reception Cracked PC", "ofis_4", new Vector2(11.05f, 1.42f), -1);
            AddMaksimProp(ctx, "Reception Water Cooler", "WaterCooler_Right", new Vector2(12.9f, 0f), -2);
            AddMaksimProp(ctx, "Lounge Wall Shelf", "WallShelf", new Vector2(4.2f, 2.75f), -4);
            AddMaksimProp(ctx, "Shelf Books Small", "BooksSmall", new Vector2(3.85f, 2.906f), -3);
            AddMaksimProp(ctx, "Shelf Books Large", "BooksLarge", new Vector2(4.45f, 2.906f), -3);
            AddMaksimProp(ctx, "Landing Filing Cabinet", "FilingCabinet_Right", new Vector2(15.6f, 0f), -2);
            AddMaksimProp(ctx, "Landing Floor Debris", "BrokenDebris", new Vector2(17.4f, 0f), -2);
            AddOfisProp(ctx, "Archive Window West", "ofis_3", new Vector2(8.2f, 2.85f), -10, 1.0f);
            AddOfisProp(ctx, "Archive Air Vent West", "ofis_5", new Vector2(16.5f, 3.65f), -10);

            // Tier 2 East Landing (solid floor y = 0, x = 42..48)
            AddMaksimProp(ctx, "Upper East Armchair", "Armchair_Front", new Vector2(44.0f, 0f), -2);
            AddMaksimProp(ctx, "Upper East Water Cooler", "WaterCooler_Front", new Vector2(45.3f, 0f), -2);
            AddMaksimProp(ctx, "Upper East Filing Cabinet", "FilingCabinet_Right", new Vector2(46.4f, 0f), -2);
            AddOfisProp(ctx, "Archive Window East", "ofis_3", new Vector2(44.8f, 2.85f), -10, 1.0f);

            // Attic Chamber (floor y = 8)
            AddMaksimProp(ctx, "Attic Filing Cabinet", "FilingCabinet_Front", new Vector2(18.1f, 8f), -2);
            AddMaksimProp(ctx, "Attic Armchair", "Armchair_Right", new Vector2(24.4f, 8f), -2);
            AddMaksimProp(ctx, "Attic Vault Books", "BooksLarge", new Vector2(31.0f, 8f), -2);
            AddOfisProp(ctx, "Attic Air Vent", "ofis_5", new Vector2(28.8f, 11.4f), -10);

            // Sunken Pocket 1 & Flooded Pocket 2
            AddMaksimProp(ctx, "Pocket 1 Broken Debris", "BrokenDebris", new Vector2(22.9f, -5f), -2);
            AddOfisProp(ctx, "Pocket 2 Desk", "ofis_0", new Vector2(39.8f, -2.485f), -3);
            AddMaksimProp(ctx, "Pocket 2 Water Cooler", "WaterCooler_Left", new Vector2(30.8f, -3f), -3);

            // Tier 1 Lower Wing & Central Core Sanctuary (floor y = -15)
            AddMaksimProp(ctx, "Sanctuary Sofa", "Sofa", new Vector2(9.6f, -15f), -2);
            AddMaksimProp(ctx, "Sanctuary Coffee Table", "CoffeeTable", new Vector2(7.6f, -15f), -2);
            AddMaksimProp(ctx, "Sanctuary Table Lamp", "DeskLamp", new Vector2(7.6f, -14.5625f), -1);
            AddMaksimProp(ctx, "Sanctuary Filing Cabinet", "FilingCabinet_Right", new Vector2(4.2f, -15f), -2);
            AddOfisProp(ctx, "Sanctuary Window", "ofis_3", new Vector2(10.0f, -11.4f), -10, 1.0f);
            AddOfisProp(ctx, "Archive Exit Door", "ofis_2", new Vector2(2.0f, -13.5f), -2);

            AddMaksimProp(ctx, "Approach Potted Plant", "FlowerPot", new Vector2(16.5f, -15f), -2);
            AddMaksimProp(ctx, "Ghost Annex Cabinet", "FilingCabinet_Left", new Vector2(42.5f, -15f), -2);
            AddMaksimProp(ctx, "Ghost Bay Leaking Bin", "LeakingBin", new Vector2(47.2f, -15f), -2);

            // Deep Shadow Vault (floor y = -25)
            AddMaksimProp(ctx, "Shadow Debris", "BrokenDebris", new Vector2(33.5f, -25f), -2);
            AddMaksimProp(ctx, "Shadow Leaking Bin", "LeakingBin", new Vector2(40.8f, -25f), -2);
            AddMaksimProp(ctx, "Shadow Filing Cabinet", "FilingCabinet_Front", new Vector2(46.0f, -25f), -2);
        }

        private static void BuildLightingAndAtmosphere(BuildContext ctx)
        {
            var ambientGo = new GameObject("Archive Ambient Light", typeof(Light2D));
            ambientGo.transform.SetParent(ctx.LightsGroup, false);
            var ambient = ambientGo.GetComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.intensity = 0.045f;
            ambient.color = new Color(0.55f, 0.65f, 0.82f);

            // Subtle nocturnal window & alcove accents
            AddPointLight(ctx, "Moonlight West Window", new Vector2(8.2f, 2.85f), new Color(0.45f, 0.65f, 0.95f), 0.20f, 4.0f);
            AddPointLight(ctx, "Moonlight East Window", new Vector2(44.8f, 2.85f), new Color(0.45f, 0.65f, 0.95f), 0.18f, 4.0f);
            AddPointLight(ctx, "Moonlight Sanctuary Window", new Vector2(10.0f, -11.4f), new Color(0.45f, 0.65f, 0.95f), 0.22f, 4.2f);
            AddPointLight(ctx, "Shaft Mid Alcove Glow", new Vector2(51.5f, -6.2f), new Color(0.88f, 0.42f, 0.38f), 0.35f, 3.2f);

            // Desk lamp warm pools
            AddPointLight(ctx, "Lounge Lamp Glow", new Vector2(6.2f, 0.70f), new Color(1.0f, 0.72f, 0.38f), 0.52f, 3.0f);
            AddPointLight(ctx, "Sanctuary Lamp Glow", new Vector2(7.6f, -14.30f), new Color(1.0f, 0.72f, 0.38f), 0.52f, 3.0f);

            // Swaying hanging cage lamps hung cleanly below carved ceilings
            var cagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/HangingCageLamp.prefab");
            foreach (var pos in new[]
            {
                new Vector2(4.2f, 4.4f),
                new Vector2(55.5f, 4.4f),
                new Vector2(42.5f, -10.5f),
                new Vector2(16.5f, -10.5f)
            })
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(cagePrefab, ctx.LightsGroup);
                lamp.transform.position = pos;
            }

            // Overhead Office Fluorescent tubes flush below chamber ceilings
            var officeLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/OfficeLight.prefab");
            void AddFluorescent(string name, Vector2 pos, FluorescentBehavior behavior)
            {
                var tube = (GameObject)PrefabUtility.InstantiatePrefab(officeLightPrefab, ctx.LightsGroup);
                tube.name = name;
                tube.transform.position = pos;
                var flicker = tube.GetComponentInChildren<FluorescentFlicker>(true);
                if (flicker != null) flicker.Behavior = behavior;
            }

            AddFluorescent("Fluorescent Entrance", new Vector2(10.0f, 4.75f), FluorescentBehavior.SubtleHum);
            AddFluorescent("Fluorescent Upper East", new Vector2(35.0f, 4.75f), FluorescentBehavior.RealisticFaulty);
            AddFluorescent("Fluorescent Attic", new Vector2(20.5f, 12.75f), FluorescentBehavior.DeadBlink);
            AddFluorescent("Fluorescent Mirror Vault", new Vector2(32.0f, -8.25f), FluorescentBehavior.SubtleHum);
            AddFluorescent("Fluorescent Sanctuary", new Vector2(6.5f, -9.25f), FluorescentBehavior.SubtleHum);

            // Camera-following atmospheric dust motes confined to carved air chambers
            var dustGo = new GameObject("Ambient Dust Motes");
            dustGo.transform.SetParent(ctx.CameraTransform != null ? ctx.CameraTransform : ctx.LightsGroup, false);
            dustGo.transform.localPosition = new Vector3(0f, 0f, 10f);
            var dust = dustGo.AddComponent<DustMotesAtmosphere>();
            var dustSo = new SerializedObject(dust);
            dustSo.FindProperty("areaSize").vector2Value = new Vector2(14f, 8.5f);
            dustSo.FindProperty("maxParticles").intValue = 65;
            dustSo.FindProperty("emissionRate").floatValue = 12f;
            dustSo.FindProperty("sizeRange").vector2Value = new Vector2(0.045f, 0.095f);
            dustSo.FindProperty("speedRange").vector2Value = new Vector2(0.05f, 0.18f);
            dustSo.FindProperty("lifetimeRange").vector2Value = new Vector2(6f, 12f);
            dustSo.FindProperty("moteColor").colorValue = new Color(0.95f, 0.86f, 0.66f, 0.55f);
            dustSo.FindProperty("orderInLayer").intValue = 12;
            dustSo.ApplyModifiedPropertiesWithoutUndo();

            var psRenderer = dustGo.GetComponent<ParticleSystemRenderer>();
            var dustMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Scenes/Sandbox/Sirac/OfficeLevel/OfficeDust.mat");
            if (psRenderer != null && dustMat != null)
            {
                psRenderer.sharedMaterial = dustMat;
            }

            var containment = dustGo.AddComponent<Level1DustContainment>();
            containment.Configure(ctx.Player != null ? ctx.Player.GetComponent<Collider2D>() : null, ctx.CarvedAirRects);
        }

        private static void BuildGameplayAndPuzzles(BuildContext ctx)
        {
            // Tier 2 West: Reception Ledger Desk, Ember Pickup & Pushable Crate
            AddCodexDesk(ctx, "Reception Mezzanine Ledger", new Vector2(16.6f, 0.0f),
                "Assets/_Project/Resources/Codex/03_OfficeLedger.asset", "READ THE LEDGER");
            AddPickup(ctx, "Ember Pickup / Corridor West", new Vector2(9.8f, 0.65f));
            ctx.Route.Crate = AddPushCrate(ctx, new Vector2(18.6f, 0.635f));

            // Tier 2 Overhead Flare Shelf (1-way platform) + Secret Flare Alcove & Checkpoint 1
            AddOneWayPlatform(ctx, ctx.GameplayGroup, "Flare Alcove Shelf", new Vector2(37.5f, 2.05f), new Vector2(7.6f, 0.30f));
            var secretWall = AddSecretPassageWall(ctx, new Vector2(39.2f, 3.6f), new Vector2(0.75f, 2.8f),
                new Vector2(40.5f, 2.85f), out var secretReward);
            var secretGlyph = AddFlareGlyph(ctx, new Vector2(35.5f, 2.95f), "LIGHT REVEALS THE ARCHIVE SEAL");
            UnityEventTools.AddPersistentListener(secretGlyph.OnFullyCharged, secretWall.Open);
            ctx.Route.SecretGlyph = secretGlyph;
            ctx.Route.SecretCache = secretWall;
            if (secretReward != null) secretReward.name = "Ember Pickup / Secret Cache";

            AddCheckpoint(ctx, "Checkpoint 1 / East Gallery", new Vector2(42.8f, 1.25f), new Vector3(42.8f, 0.85f, 0f));

            // Pocket 2: Electrified Water Trough & EmberVein Drain Switch
            var water = AddElectrifiedWaterPool(ctx, new Vector2(35.5f, -3.56f), new Vector2(6.95f, 0.88f));
            var vein = AddEmberVein(ctx, new Vector2(28.5f, 0.0f), 2);
            UnityEventTools.AddPersistentListener(vein.OnPowered, water.Purify);
            UnityEventTools.AddPersistentListener(vein.OnDrained, water.Electrify);
            ctx.Route.WaterVein = vein;
            ctx.Route.WaterHazard = water;
            AddPickup(ctx, "Ember Pickup / Trough West", new Vector2(31.2f, -2.35f));
            AddPickup(ctx, "Ember Pickup / Trough East", new Vector2(39.8f, -2.35f));

            // Tier 1 Lower Wing: Ghost Annex Relay & Upper Mirror Vault Ethereal Catwalk
            var bridgeA = AddEtherealPlatform(ctx, "Light Bridge Span A", new Vector2(34.5f, -10.2f), new Vector2(4.0f, 0.35f));
            var bridgeB = AddEtherealPlatform(ctx, "Light Bridge Span B", new Vector2(28.0f, -10.2f), new Vector2(4.0f, 0.35f));
            var bridgeRelay = AddPhotosensitiveRelay(ctx, "Bridge Relay Crystal", new Vector2(42.8f, -15.0f),
                RelayMode.Timed, 1.0f, 9.0f, 3.5f, 9.5f);
            UnityEventTools.AddFloatPersistentListener(bridgeRelay.OnActivated, bridgeA.ForceSolidify, 9.0f);
            UnityEventTools.AddFloatPersistentListener(bridgeRelay.OnActivated, bridgeB.ForceSolidify, 9.0f);
            ctx.Route.BridgeRelay = bridgeRelay;
            ctx.Route.BridgePlatform = bridgeA;
            AddPickup(ctx, "Ember Pickup / Ghost Bay", new Vector2(49.2f, -14.35f));

            AddCheckpoint(ctx, "Checkpoint 2 / Shaft Base", new Vector2(51.8f, -13.75f), new Vector3(51.8f, -14.15f, 0f));

            // Tier 1 Approach & Security Gate -> Core Sanctuary & Exit
            AddPickup(ctx, "Ember Pickup / Approach", new Vector2(18.0f, -14.35f));
            AddCodexDesk(ctx, "Server Wing Archive Note", new Vector2(37.5f, -25.0f),
                "Assets/_Project/Resources/Codex/04_OfficeMargin.asset", "INSPECT SHADOW LOG");

            var exitRelay = AddPhotosensitiveRelay(ctx, "Security Gate Relay", new Vector2(15.2f, -15.0f),
                RelayMode.Latched, 1.1f, 6.0f, 3.2f, 9.5f);
            var exitGate = AddArchiveGate(ctx, "Executive Security Gate", new Vector2(13.5f, -15.0f), exitRelay);
            ctx.Route.ExitRelay = exitRelay;
            ctx.Route.ExitGate = exitGate;

            // Exit Completion Trigger inside Core Sanctuary
            var exitGo = new GameObject("Level 1 Exit Trigger", typeof(BoxCollider2D), typeof(ChamberCompletionSequence), typeof(ChamberExitTrigger));
            exitGo.transform.SetParent(ctx.GameplayGroup, false);
            exitGo.transform.position = new Vector3(2.0f, -13.75f, 0f);
            var exitCol = exitGo.GetComponent<BoxCollider2D>();
            exitCol.isTrigger = true;
            exitCol.size = new Vector2(0.95f, 2.5f);

            string profilePath = FolderRoot + "/Level1Completion.asset";
            var profile = AssetDatabase.LoadAssetAtPath<ChamberCompletionProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ChamberCompletionProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.Title = "CORPORATE ARCHIVE";
            profile.Eyebrow = "LEVEL 1 COMPLETE";
            profile.Subtitle = "Sector Cleared";
            EditorUtility.SetDirty(profile);

            var completionSeq = exitGo.GetComponent<ChamberCompletionSequence>();
            completionSeq.Profile = profile;
            exitGo.GetComponent<ChamberExitTrigger>().Player = ctx.Player;

            BuildExtendedMechanics(ctx, completionSeq);
        }

        private static void BuildHudAndSystems(BuildContext ctx)
        {
            new GameObject("Background Music").AddComponent<BackgroundMusicManager>();
            var lantern = ctx.Player.GetComponent<LanternController>();
            new GameObject("LanternHUD").AddComponent<LanternHUD>().SetTarget(lantern);
            new GameObject("StaminaHUD").AddComponent<StaminaHUD>().SetTarget(ctx.Player.GetComponent<PlayerStamina>());
            new GameObject("DeathScreenOverlay").AddComponent<DeathScreenOverlay>().SetTarget(ctx.Player);
            ctx.Route.HudOverlay = new GameObject("MechanicsHUD").AddComponent<MechanicsHUD>();
            new GameObject("Pause Menu").AddComponent<PauseMenuController>();
        }

        private static SpriteRenderer AddMaksimProp(BuildContext ctx, string name, string spriteKey, Vector2 pos, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(ctx.PropsGroup, false);
            go.transform.position = pos;
            var r = go.GetComponent<SpriteRenderer>();
            r.sprite = ctx.MaksimProps[spriteKey];
            r.sharedMaterial = ctx.LitMaterial;
            r.sortingOrder = order;
            return r;
        }

        private static SpriteRenderer AddOfisProp(BuildContext ctx, string name, string spriteKey, Vector2 pos, int order, float scale = 1f)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(ctx.PropsGroup, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var r = go.GetComponent<SpriteRenderer>();
            r.sprite = ctx.OfisProps[spriteKey];
            r.sharedMaterial = ctx.LitMaterial;
            r.sortingOrder = order;
            return r;
        }

        private static void AddQuad(BuildContext ctx, string name, Vector2 pos, Vector2 size, Color color, int order, Transform parent)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.GetComponent<SpriteRenderer>();
            r.sprite = ctx.WhiteSprite;
            r.sharedMaterial = ctx.LitMaterial;
            r.color = color;
            r.sortingOrder = order;
        }

        private static void AddPointLight(BuildContext ctx, string name, Vector2 pos, Color color, float intensity, float radius)
        {
            var go = new GameObject(name, typeof(Light2D));
            go.transform.SetParent(ctx.LightsGroup, false);
            go.transform.position = pos;
            var l = go.GetComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
        }

        private static void AddPickup(BuildContext ctx, string name, Vector2 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EmberPickup.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ctx.GameplayGroup);
            go.name = name;
            go.transform.position = pos;
        }

        private static void AddCodexDesk(BuildContext ctx, string name, Vector2 floorPos, string logAssetPath, string prompt)
        {
            string deskSpritePath = FolderRoot + "/Art/ArchiveDeskSprite.png";
            if (!File.Exists(deskSpritePath))
            {
                File.WriteAllBytes(deskSpritePath, ArchiveTerminalVisual.DeskSprite.texture.EncodeToPNG());
                AssetDatabase.ImportAsset(deskSpritePath);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(deskSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            importer.spritePivot = new Vector2(0.5f, 0f);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var desk = new GameObject(name, typeof(SpriteRenderer), typeof(DiegeticWhisper));
            desk.transform.SetParent(ctx.GameplayGroup, false);
            desk.transform.position = floorPos;
            var renderer = desk.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(deskSpritePath);
            renderer.sharedMaterial = ctx.LitMaterial;
            renderer.sortingOrder = -1;

            var whisper = desk.GetComponent<DiegeticWhisper>();
            whisper.ArchiveLog = AssetDatabase.LoadAssetAtPath<ArchiveLog>(logAssetPath);
            whisper.ActionPrompt = prompt;
            whisper.KeyHint = "E";
            var so = new SerializedObject(whisper);
            so.FindProperty("requiredAction").enumValueIndex = (int)WhisperAction.Interact;
            so.FindProperty("triggerRadius").floatValue = 1.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnnexPushBlock AddPushCrate(BuildContext ctx, Vector2 pos)
        {
            var go = new GameObject("Pushable Archive Crate", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D), typeof(AnnexPushBlock));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1.25f, 1.25f, 1f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites/Box.png").OfType<Sprite>().First();
            renderer.sharedMaterial = ctx.LitMaterial;
            renderer.sortingOrder = 2;

            var box = go.GetComponent<BoxCollider2D>();
            box.size = Vector2.one;
            string frictionPath = FolderRoot + "/Art/CrateFriction.physicsMaterial2D";
            var friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(frictionPath);
            if (friction == null)
            {
                friction = new PhysicsMaterial2D("Level 1 Crate Friction") { friction = 0.12f, bounciness = 0f };
                AssetDatabase.CreateAsset(friction, frictionPath);
            }
            box.sharedMaterial = friction;

            var rb = go.GetComponent<Rigidbody2D>();
            rb.mass = 6f;
            rb.gravityScale = 3f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            return go.GetComponent<AnnexPushBlock>();
        }

        private static void AddCheckpoint(BuildContext ctx, string name, Vector2 pos, Vector3 spawn)
        {
            var go = new GameObject(name, typeof(BoxCollider2D), typeof(AnnexCheckpoint));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = pos;
            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 2.5f);
            go.GetComponent<AnnexCheckpoint>().Spawn = spawn;
        }

        private static EtherealPlatform2D AddEtherealPlatform(BuildContext ctx, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(EtherealPlatform2D));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = pos;
            var plat = go.GetComponent<EtherealPlatform2D>();
            var so = new SerializedObject(plat);
            so.FindProperty("platformSize").vector2Value = size;
            so.FindProperty("lingerDuration").floatValue = 3.5f;
            so.FindProperty("flareActivationDistance").floatValue = 8.5f;
            so.FindProperty("useOneWayPlatform").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return plat;
        }

        private static PhotosensitiveRelay2D AddPhotosensitiveRelay(BuildContext ctx, string name, Vector2 pos,
            RelayMode mode, float chargeTime, float lingerDuration, float dimDist, float flareDist)
        {
            var go = new GameObject(name, typeof(PhotosensitiveRelay2D));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = pos;
            var relay = go.GetComponent<PhotosensitiveRelay2D>();
            relay.EnsureHierarchy();

            var ped = relay.transform.Find("Pedestal")?.GetComponent<SpriteRenderer>();
            if (ped != null)
            {
                ped.sprite = ctx.PedestalSprite;
                ped.sharedMaterial = ctx.LitMaterial;
            }

            if (relay.CrystalTransform != null)
            {
                relay.CrystalTransform.localPosition = new Vector3(0f, 1.05f, 0f);
            }

            if (relay.CrystalRenderer != null)
            {
                relay.CrystalRenderer.sprite = ctx.CrystalSprite;
                relay.CrystalRenderer.sharedMaterial = ctx.LitMaterial;
            }

            var so = new SerializedObject(relay);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.FindProperty("chargeTime").floatValue = chargeTime;
            so.FindProperty("lingerDuration").floatValue = lingerDuration;
            so.FindProperty("dimActivationRadius").floatValue = dimDist;
            so.FindProperty("flareActivationRadius").floatValue = flareDist;
            so.ApplyModifiedPropertiesWithoutUndo();
            return relay;
        }

        private static ArchiveGate2D AddArchiveGate(BuildContext ctx, string name, Vector2 pos, PhotosensitiveRelay2D relay)
        {
            var go = new GameObject(name, typeof(ArchiveGate2D));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = pos;
            var gate = go.GetComponent<ArchiveGate2D>();
            gate.EnsureHierarchy();

            var gateRenderer = gate.GetComponent<SpriteRenderer>();
            if (gateRenderer != null)
            {
                gateRenderer.sprite = ctx.GateSprite;
                gateRenderer.sharedMaterial = ctx.LitMaterial;
            }

            var so = new SerializedObject(gate);
            so.FindProperty("relay").objectReferenceValue = relay;
            so.FindProperty("openOffset").vector3Value = new Vector3(0f, 3.0f, 0f);
            so.FindProperty("startOpen").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        private static ElectrifiedWater AddElectrifiedWaterPool(BuildContext ctx, Vector2 center, Vector2 size)
        {
            var root = new GameObject("Flooded Server Electrified Water", typeof(SpriteRenderer), typeof(ElectrifiedWater));
            root.transform.SetParent(ctx.GameplayGroup, false);
            root.transform.position = center;
            root.transform.localScale = Vector3.one;

            var sr = root.GetComponent<SpriteRenderer>();
            sr.sprite = GetArt(ctx.WorldArt, "HazardWater", ctx.WhiteSprite);
            sr.sharedMaterial = ctx.LitMaterial;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = 4;

            var hazardGo = new GameObject("Water Hazard Trigger", typeof(BoxCollider2D), typeof(HazardTrigger2D));
            hazardGo.transform.SetParent(root.transform, false);
            hazardGo.transform.localScale = Vector3.one;
            var col = hazardGo.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = size;

            var water = root.GetComponent<ElectrifiedWater>();
            water.waterRenderer = sr;
            water.hazardZone = hazardGo;

            // Subtle electric glow accent
            AddPointLight(ctx, "Electrified Pool Glow", center, new Color(0.85f, 0.88f, 0.25f), 0.35f, 4.5f);
            return water;
        }

        private static EmberVein AddEmberVein(BuildContext ctx, Vector2 floorPos, int chargesRequired)
        {
            var go = new GameObject("Server Power Ember Vein", typeof(SpriteRenderer), typeof(EmberVein), typeof(DiegeticWhisper));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = floorPos;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = ctx.PedestalSprite;
            sr.sharedMaterial = ctx.LitMaterial;
            sr.color = new Color(1.0f, 0.65f, 0.25f);
            sr.sortingOrder = 5;

            var vein = go.GetComponent<EmberVein>();
            vein.chargesRequired = chargesRequired;
            vein.OnPowered ??= new UnityEngine.Events.UnityEvent();
            vein.OnDrained ??= new UnityEngine.Events.UnityEvent();

            var whisper = go.GetComponent<DiegeticWhisper>();
            whisper.KeyHint = "E";
            whisper.ActionPrompt = "POWER CONDUIT (1 EMBER) TO DRAIN FLOOR";
            var so = new SerializedObject(whisper);
            so.FindProperty("requiredAction").enumValueIndex = (int)WhisperAction.ProximityOnly;
            so.FindProperty("triggerRadius").floatValue = 2.6f;
            so.FindProperty("autoDissipateOnAction").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            AddPointLight(ctx, "Ember Vein Glow", floorPos + new Vector2(0f, 0.85f), new Color(1.0f, 0.58f, 0.2f), 0.55f, 2.8f);
            return vein;
        }

        private static SecretPassage AddSecretPassageWall(BuildContext ctx, Vector2 wallPos, Vector2 wallSize, Vector2 rewardPos, out GameObject rewardGo)
        {
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EmberPickup.prefab");
            rewardGo = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, ctx.GameplayGroup);
            rewardGo.transform.position = rewardPos;

            var go = new GameObject("Secret Archive Partition", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(SecretPassage));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = wallPos;
            go.transform.localScale = Vector3.one;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetArt(ctx.WorldArt, "SecretDoor", ctx.WhiteSprite);
            sr.sharedMaterial = ctx.LitMaterial;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = wallSize;
            sr.color = Color.white;
            sr.sortingOrder = 3;

            var col = go.GetComponent<BoxCollider2D>();
            col.size = wallSize;
            col.offset = Vector2.zero;

            var secret = go.GetComponent<SecretPassage>();
            var so = new SerializedObject(secret);
            so.FindProperty("wallCollider").objectReferenceValue = col;
            so.FindProperty("wallRenderer").objectReferenceValue = sr;
            so.FindProperty("secretReward").objectReferenceValue = rewardGo;
            so.FindProperty("stayOpenOnceDiscovered").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return secret;
        }

        private static FlareGlyph AddFlareGlyph(BuildContext ctx, Vector2 pos, string inscription)
        {
            var go = new GameObject("Archive Flare Glyph", typeof(FlareGlyph));
            go.transform.SetParent(ctx.GameplayGroup, false);
            go.transform.position = pos;
            var glyph = go.GetComponent<FlareGlyph>();
            var so = new SerializedObject(glyph);
            so.FindProperty("glyphType").enumValueIndex = (int)GlyphType.EmberEye;
            so.FindProperty("inscriptionText").stringValue = inscription;
            so.FindProperty("lingerDuration").floatValue = 4.0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return glyph;
        }

        private static GameObject AddOneWayPlatform(BuildContext ctx, Transform parent, string name, Vector2 centerPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(PlatformEffector2D));
            go.transform.SetParent(parent, false);
            go.transform.position = centerPos;
            go.transform.localScale = Vector3.one;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetArt(ctx.WorldArt, "BeamGirder", ctx.WhiteSprite);
            sr.sharedMaterial = ctx.LitMaterial;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = new Color(0.82f, 0.85f, 0.90f, 1f);
            sr.sortingOrder = 3;

            var col = go.GetComponent<BoxCollider2D>();
            col.size = size;
            col.usedByEffector = true;

            var effector = go.GetComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            return go;
        }

        private static void BuildExtendedMechanics(BuildContext ctx, ChamberCompletionSequence completionSeq)
        {
            var group = Group("Extended Mechanics", ctx.GameplayGroup);
            var art = ctx.WorldArt ?? BakeWorldArtSprites();

            // 1. Climbable Ladders & One-Way Grates connecting all carved tiers safely
            ctx.Route.MezzanineLadder = AddLadder(ctx, group, "Mezzanine Ladder", new Vector2(20.5f, 0.0f), 8.9f, GetArt(art, "LadderRungTile"));
            AddOneWayPlatform(ctx, group, "Attic Chimney Grate", new Vector2(21.0f, 7.85f), new Vector2(3.8f, 0.30f));
            AddOneWayPlatform(ctx, group, "Pocket 1 Escape Grate", new Vector2(22.7f, -2.5f), new Vector2(1.2f, 0.30f));
            AddLadder(ctx, group, "Pocket 2 Exit Ladder", new Vector2(41.2f, -3.0f), 3.9f, GetArt(art, "LadderRungTile"));
            AddLadder(ctx, group, "East Shaft Service Ladder", new Vector2(57.3f, -15.0f), 15.9f, GetArt(art, "LadderRungTile"));
            AddLadder(ctx, group, "Lower Well Ladder", new Vector2(19.0f, -21.0f), 6.9f, GetArt(art, "LadderRungTile"));
            AddLadder(ctx, group, "Shadow Vault Ladder", new Vector2(26.2f, -25.0f), 10.9f, GetArt(art, "LadderRungTile"));

            // 2. Non-Consuming Colored Ember Pickups (Orange, Cyan, Crimson)
            AddColoredPickup(ctx, group, "Ember Pickup / Orange", new Vector2(14.8f, 0.65f), EmberColor.Orange);
            AddColoredPickup(ctx, group, "Ember Pickup / Cyan", new Vector2(30.8f, 0.65f), EmberColor.Cyan);
            AddColoredPickup(ctx, group, "Ember Pickup / Crimson", new Vector2(51.4f, -7.25f), EmberColor.Crimson);

            // 3. Tutorial Prompts
            SpawnWhisperHint(group, "Hint Move & Jump", new Vector2(3.4f, 1.85f), "A / D · SPACE", "TRAVERSE THE FLOOR", WhisperAction.Move);
            SpawnWhisperHint(group, "Hint Ledge & Ladder", new Vector2(20.5f, 1.2f), "W / S", "CLIMB SHAFT & ATTIC", WhisperAction.ProximityOnly);
            SpawnWhisperHint(group, "Hint Flare Pan", new Vector2(34.5f, 3.0f), "F", "FLARE & SURVEY WITH A / D", WhisperAction.Flare);
            SpawnWhisperHint(group, "Hint Lenses", new Vector2(23.5f, -3.6f), "Q / E", "SHIFT LANTERN LENS", WhisperAction.ProximityOnly);

            // 4. 4-State Vein & All 9 Vein-Driven Mechanism Types integrated along carved chambers
            ctx.Route.MultiStateVein = AddAdvancedVein(ctx, group, "Multi-State Mechanism Vein", new Vector2(23.5f, -5.0f), EmberColor.None, false, art);
            ctx.Route.CorruptedVein = AddAdvancedVein(ctx, group, "Corrupted Archive Vein", new Vector2(38.5f, -15.0f), EmberColor.None, true, art);

            var solidNeutral = new Color(0.92f, 0.92f, 0.94f, 1f);
            var ghostTint = new Color(0.45f, 0.88f, 1.0f, 0.82f);

            ctx.Route.BlastDoorMech = AddVeinMechanism(ctx, group, "Vein Blast Door", VeinMechanismType.BlastDoor,
                new Vector3(48.5f, 0.0f, 0f), new Vector3(48.5f, 2.75f, 0f), new Vector2(0.85f, 3.0f), ctx.Route.MultiStateVein, solidNeutral, art);
            ctx.Route.PusherMech = AddVeinMechanism(ctx, group, "Vein Pusher Mechanism", VeinMechanismType.PusherMechanism,
                new Vector3(47.3f, 0.65f, 0f), new Vector3(48.1f, 0.65f, 0f), new Vector2(0.85f, 0.55f), ctx.Route.MultiStateVein, Color.white, art);
            ctx.Route.PusherMech.LinkedTarget = ctx.Route.BlastDoorMech;

            ctx.Route.PistonMech = AddVeinMechanism(ctx, group, "Vein Piston Platform", VeinMechanismType.Piston,
                new Vector3(25.6f, -4.6f, 0f), new Vector3(25.6f, -0.5f, 0f), new Vector2(1.6f, 0.45f), ctx.Route.MultiStateVein, solidNeutral, art);
            ctx.Route.ElevatorMech = AddVeinMechanism(ctx, group, "Shaft Freight Elevator", VeinMechanismType.Elevator,
                new Vector3(56.1f, -14.7f, 0f), new Vector3(56.1f, -0.18f, 0f), new Vector2(2.1f, 0.36f), ctx.Route.MultiStateVein, solidNeutral, art);
            ctx.Route.MovingPlatformMech = AddVeinMechanism(ctx, group, "Vein Moving Platform", VeinMechanismType.MovingPlatform,
                new Vector3(32.0f, -0.18f, 0f), new Vector3(39.5f, -0.18f, 0f), new Vector2(2.4f, 0.35f), ctx.Route.MultiStateVein, solidNeutral, art);
            ctx.Route.MovingBlockMech = AddVeinMechanism(ctx, group, "Vein Moving 1x1 Block", VeinMechanismType.MovingBlock1x1,
                new Vector3(31.8f, 2.70f, 0f), new Vector3(34.4f, 2.70f, 0f), Vector2.one, ctx.Route.MultiStateVein, Color.white, art);

            ctx.Route.GhostDoorMech = AddVeinMechanism(ctx, group, "Ghost Archive Door", VeinMechanismType.GhostDoor,
                new Vector3(45.5f, -15.0f, 0f), new Vector3(45.5f, -12.2f, 0f), new Vector2(0.85f, 3.0f), ctx.Route.MultiStateVein, ghostTint, art);
            ctx.Route.GhostCrateMech = AddVeinMechanism(ctx, group, "Ghost Archive Crate", VeinMechanismType.GhostCrate,
                new Vector3(48.5f, -14.5f, 0f), new Vector3(48.5f, -14.5f, 0f), new Vector2(1.0f, 1.0f), ctx.Route.MultiStateVein, ghostTint, art);
            ctx.Route.GhostElevatorMech = AddVeinMechanism(ctx, group, "Ghost Archive Elevator", VeinMechanismType.GhostElevator,
                new Vector3(50.2f, -14.7f, 0f), new Vector3(50.2f, -11.5f, 0f), new Vector2(1.6f, 0.35f), ctx.Route.MultiStateVein, ghostTint, art);

            // 5. Conduit Pipe & Junction Puzzle (Attic Chamber) -> Secret Sliding Panel into Attic Secret Vault
            ctx.Route.ConduitPuzzle = BuildConduitPuzzle(ctx, group, new Vector3(14.8f, 9.6f, 0f), art);

            // 6. Light Mirrors, Projector Vein, Rail, Darkness Barrier & Corrupted Mirror (Mirror Vault)
            BuildLightMirrorStation(ctx, group, art);

            // 7. Shadow Realm Zone, Scripted Fall, Mementos & 4-Beam Central Core Finale
            BuildShadowRealmAndFlow(ctx, group, completionSeq, art);
        }

        private static Sprite GetArt(Dictionary<string, Sprite> art, string key, Sprite fallback = null)
        {
            return art != null && art.TryGetValue(key, out var s) && s != null ? s : fallback;
        }

        private static Dictionary<string, Sprite> BakeWorldArtSprites()
        {
            const string outDir = FolderRoot + "/GeneratedArt";
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            var sprites = new Dictionary<string, Sprite>();
            foreach (var (key, tex, border) in Level1PixelArt.EnumerateWorldTextures())
            {
                string assetPath = $"{outDir}/{key}.png";
                File.WriteAllBytes(assetPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 16f;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.spriteBorder = border;

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null) sprites[key] = sprite;
            }
            return sprites;
        }

        private static ClimbableLadder2D AddLadder(BuildContext ctx, Transform parent, string name, Vector2 basePos, float height, Sprite ladderSprite)
        {
            var go = new GameObject(name, typeof(BoxCollider2D), typeof(ClimbableLadder2D));
            go.transform.SetParent(parent, false);
            go.transform.position = basePos;
            go.transform.localScale = Vector3.one;

            var rungsGo = new GameObject("Rungs", typeof(SpriteRenderer));
            rungsGo.transform.SetParent(go.transform, false);
            rungsGo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            var sr = rungsGo.GetComponent<SpriteRenderer>();
            sr.sprite = ladderSprite != null ? ladderSprite : ctx.WhiteSprite;
            sr.sharedMaterial = ctx.LitMaterial;
            sr.color = Color.white;
            sr.sortingOrder = -1;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(0.85f, height);

            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.85f, height);
            col.offset = new Vector2(0f, height * 0.5f);

            var ladder = go.GetComponent<ClimbableLadder2D>();
            ladder.Height = height;
            return ladder;
        }

        private static ColoredEmberPickup2D AddColoredPickup(BuildContext ctx, Transform parent, string name, Vector2 pos, EmberColor color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(ColoredEmberPickup2D));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = ctx.CrystalSprite != null ? ctx.CrystalSprite : ctx.WhiteSprite;
            sr.sharedMaterial = ctx.LitMaterial;
            sr.sortingOrder = 5;

            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.4f, 1.4f);

            var pickup = go.GetComponent<ColoredEmberPickup2D>();
            pickup.ColorType = color;
            pickup.RefreshVisual();
            return pickup;
        }

        private static void SpawnWhisperHint(Transform parent, string name, Vector2 pos, string keyHint, string prompt, WhisperAction action)
        {
            var go = new GameObject(name, typeof(DiegeticWhisper));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var w = go.GetComponent<DiegeticWhisper>();
            w.KeyHint = keyHint;
            w.ActionPrompt = prompt;
            var so = new SerializedObject(w);
            so.FindProperty("requiredAction").enumValueIndex = (int)action;
            so.FindProperty("triggerRadius").floatValue = 2.4f;
            so.FindProperty("autoDissipateOnAction").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AdvancedEmberVein AddAdvancedVein(BuildContext ctx, Transform parent, string name, Vector2 pos, EmberColor initial, bool corrupted, Dictionary<string, Sprite> art)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(AdvancedEmberVein));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = ctx.PedestalSprite != null ? ctx.PedestalSprite : ctx.WhiteSprite;
            sr.sharedMaterial = ctx.LitMaterial;
            sr.sortingOrder = 5;

            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.1f, 1.4f);

            var glowGo = new GameObject("Glow", typeof(SpriteRenderer));
            glowGo.transform.SetParent(go.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            glowGo.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            var glowSr = glowGo.GetComponent<SpriteRenderer>();
            glowSr.sprite = ctx.CrystalSprite != null ? ctx.CrystalSprite : ctx.WhiteSprite;
            glowSr.sharedMaterial = ctx.LitMaterial;
            glowSr.sortingOrder = 6;

            var gearGo = new GameObject("Gear", typeof(SpriteRenderer));
            gearGo.transform.SetParent(go.transform, false);
            gearGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            gearGo.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
            var gearSr = gearGo.GetComponent<SpriteRenderer>();
            gearSr.sprite = art.TryGetValue("GearCog", out var gearSprite) ? gearSprite : ctx.WhiteSprite;
            gearSr.sharedMaterial = ctx.LitMaterial;
            gearSr.color = Color.white;
            gearSr.sortingOrder = 6;

            var barGo = new GameObject("Barricade", typeof(SpriteRenderer));
            barGo.transform.SetParent(go.transform, false);
            barGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            barGo.transform.localScale = new Vector3(0.95f, 0.55f, 1f);
            var barSr = barGo.GetComponent<SpriteRenderer>();
            barSr.sprite = art.TryGetValue("BarricadeClasp", out var claspSprite) ? claspSprite : ctx.WhiteSprite;
            barSr.sharedMaterial = ctx.LitMaterial;
            barSr.color = Color.white;
            barSr.sortingOrder = 7;

            var vein = go.GetComponent<AdvancedEmberVein>();
            vein.GlowRenderer = glowSr;
            vein.GearTransform = gearGo.transform;
            vein.BarricadeRenderer = barSr;

            var so = new SerializedObject(vein);
            so.FindProperty("initialEmber").enumValueIndex = (int)initial;
            so.FindProperty("isCorrupted").boolValue = corrupted;
            so.ApplyModifiedPropertiesWithoutUndo();
            return vein;
        }

        private static VeinDrivenMechanism2D AddVeinMechanism(BuildContext ctx, Transform parent, string name,
            VeinMechanismType type, Vector3 pointA, Vector3 pointB, Vector2 size, AdvancedEmberVein vein, Color tint, Dictionary<string, Sprite> art)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(VeinDrivenMechanism2D));
            go.transform.SetParent(parent, false);
            go.transform.position = pointA;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = ctx.LitMaterial;
            sr.color = tint;
            sr.sortingOrder = 4;

            var col = go.GetComponent<BoxCollider2D>();

            Sprite chosen = null;
            bool useSliced = false;

            switch (type)
            {
                case VeinMechanismType.BlastDoor:
                case VeinMechanismType.GhostDoor:
                    chosen = ctx.GateSprite != null ? ctx.GateSprite : GetArt(art, "BarricadeClasp");
                    break;
                case VeinMechanismType.GhostCrate:
                    chosen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Box.png");
                    if (chosen == null) chosen = GetArt(art, "EtchedBlock");
                    break;
                case VeinMechanismType.MovingBlock1x1:
                    chosen = GetArt(art, "EtchedBlock");
                    break;
                case VeinMechanismType.PusherMechanism:
                    chosen = GetArt(art, "PusherRam");
                    useSliced = chosen != null;
                    break;
                default:
                    chosen = GetArt(art, "BeamGirder");
                    useSliced = chosen != null;
                    break;
            }

            sr.sprite = chosen != null ? chosen : ctx.WhiteSprite;

            if (useSliced)
            {
                go.transform.localScale = Vector3.one;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
                col.size = size;
                col.offset = Vector2.zero;
            }
            else if (sr.sprite != null)
            {
                Vector2 sb = sr.sprite.bounds.size;
                go.transform.localScale = new Vector3(
                    size.x / Mathf.Max(0.01f, sb.x),
                    size.y / Mathf.Max(0.01f, sb.y),
                    1f);
                col.size = sb;
                col.offset = sr.sprite.bounds.center;
            }
            else
            {
                go.transform.localScale = new Vector3(size.x, size.y, 1f);
                col.size = Vector2.one;
                col.offset = Vector2.zero;
            }

            var mech = go.GetComponent<VeinDrivenMechanism2D>();
            mech.MechanismType = type;
            mech.SourceVein = vein;
            mech.PointA = pointA;
            mech.PointB = pointB;
            mech.SolidCollider = col;
            mech.VisualRenderer = sr;
            return mech;
        }

        private static ConduitGridController BuildConduitPuzzle(BuildContext ctx, Transform parent, Vector3 origin, Dictionary<string, Sprite> art)
        {
            var root = new GameObject("Conduit Puzzle Station", typeof(ConduitGridController));
            root.transform.SetParent(parent, false);
            root.transform.position = origin;
            var grid = root.GetComponent<ConduitGridController>();

            // Sliding secret panel sealing AtticSecretArch (x: 26..27, y: 8..11) -> opens into Attic Secret Vault
            var panelGo = new GameObject("Conduit Secret Sliding Panel", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(SecretSlidingPanel2D));
            panelGo.transform.SetParent(root.transform, false);
            panelGo.transform.position = new Vector3(26.5f, 8.0f, 0f);
            var panelSr = panelGo.GetComponent<SpriteRenderer>();
            panelSr.sprite = ctx.GateSprite != null ? ctx.GateSprite : GetArt(art, "BeamGirder", ctx.WhiteSprite);
            panelSr.sharedMaterial = ctx.LitMaterial;
            panelSr.color = new Color(0.84f, 0.86f, 0.90f, 1f);
            panelSr.sortingOrder = 4;
            var panelCol = panelGo.GetComponent<BoxCollider2D>();
            if (panelSr.sprite != null)
            {
                Vector2 sb = panelSr.sprite.bounds.size;
                panelGo.transform.localScale = new Vector3(0.85f / Mathf.Max(0.01f, sb.x), 3.0f / Mathf.Max(0.01f, sb.y), 1f);
                panelCol.size = sb;
                panelCol.offset = panelSr.sprite.bounds.center;
            }
            var panel = panelGo.GetComponent<SecretSlidingPanel2D>();
            panel.ClosedPos = panelGo.transform.position;
            panel.OpenPos = panelGo.transform.position + Vector3.up * 2.6f;
            panel.PanelCollider = panelCol;

            grid.SourceCell = new Vector2Int(0, 0);
            grid.OutputCell = new Vector2Int(2, 1);
            grid.TargetSecretPanel = panel;

            // 4-node L-path from (0,0) -> (1,0) -> (1,1) -> (2,1) with Tee junction at (1,0)
            var spec = new (Vector2Int pos, ConduitPipeShape shape, int solvedRot, bool isJunction)[]
            {
                (new Vector2Int(0, 0), ConduitPipeShape.Straight, 1, false), // E-W
                (new Vector2Int(1, 0), ConduitPipeShape.Tee, 0, true),       // N-E-W
                (new Vector2Int(1, 1), ConduitPipeShape.Corner, 1, false),   // E-S
                (new Vector2Int(2, 1), ConduitPipeShape.Straight, 1, false)  // E-W
            };

            foreach (var (pos, shape, solvedRot, isJunc) in spec)
            {
                var nGo = new GameObject($"ConduitNode_{pos.x}_{pos.y}", typeof(SpriteRenderer), typeof(ConduitNode2D));
                nGo.transform.SetParent(root.transform, false);
                nGo.transform.position = origin + new Vector3(pos.x * 0.9f, pos.y * 0.9f, 0f);
                nGo.transform.localScale = new Vector3(0.90f, 0.90f, 1f);
                var nSr = nGo.GetComponent<SpriteRenderer>();
                string pipeKey = shape == ConduitPipeShape.Corner ? "ConduitCorner"
                    : shape == ConduitPipeShape.Tee ? "ConduitTee"
                    : shape == ConduitPipeShape.Cross ? "ConduitCross"
                    : "ConduitStraight";
                nSr.sprite = GetArt(art, pipeKey, ctx.WhiteSprite);
                nSr.sharedMaterial = ctx.LitMaterial;
                nSr.sortingOrder = 5;

                var node = nGo.GetComponent<ConduitNode2D>();
                node.GridPos = pos;
                node.Shape = shape;
                node.SolvedRotationSteps = solvedRot;
                node.IsJunction = isJunc;
                node.NodeRenderer = nSr;
                node.Grid = grid;
                grid.Nodes.Add(node);
            }

            grid.RandomizeLayout(42);
            return grid;
        }

        private static void BuildLightMirrorStation(BuildContext ctx, Transform parent, Dictionary<string, Sprite> art)
        {
            var group = new GameObject("Light Mirror Station");
            group.transform.SetParent(parent, false);

            var railVein = AddAdvancedVein(ctx, group.transform, "Mirror Rail Vein", new Vector2(36.5f, -15.0f), EmberColor.Orange, false, art);

            var railGo = new GameObject("Mirror Rail", typeof(SpriteRenderer), typeof(MirrorRail2D));
            railGo.transform.SetParent(group.transform, false);
            railGo.transform.position = new Vector3(33.15f, -12.6f, 0f);
            railGo.transform.localScale = Vector3.one;
            var railSr = railGo.GetComponent<SpriteRenderer>();
            railSr.sprite = GetArt(art, "MirrorTrack", ctx.WhiteSprite);
            railSr.sharedMaterial = ctx.LitMaterial;
            railSr.drawMode = SpriteDrawMode.Sliced;
            railSr.size = new Vector2(3.6f, 0.32f);
            railSr.color = Color.white;
            var rail = railGo.GetComponent<MirrorRail2D>();
            rail.RailVein = railVein;
            rail.RailStart = new Vector3(31.5f, -12.6f, 0f);
            rail.RailEnd = new Vector3(34.8f, -12.6f, 0f);

            var mirrorSprite = GetArt(art, "MirrorBlade", ctx.WhiteSprite);

            var mirrorGo = new GameObject("Rotatable Light Mirror", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(LightMirror2D));
            mirrorGo.transform.SetParent(group.transform, false);
            mirrorGo.transform.position = new Vector3(31.5f, -12.6f, 0f);
            mirrorGo.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
            var mSr = mirrorGo.GetComponent<SpriteRenderer>();
            mSr.sprite = mirrorSprite;
            mSr.sharedMaterial = ctx.LitMaterial;
            mSr.sortingOrder = 7;
            var mCol = mirrorGo.GetComponent<BoxCollider2D>();
            mCol.isTrigger = true;
            mCol.size = new Vector2(0.32f, 1.0f);
            var mirror = mirrorGo.GetComponent<LightMirror2D>();
            mirror.MirrorAngleDegrees = 135f; // Reflects rightward ray (1,0) downward (0,-1)
            mirror.AttachedRail = rail;
            mirror.MirrorRenderer = mSr;
            rail.AttachedMirror = mirror;

            var corruptGo = new GameObject("Corrupted Light Mirror", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(LightMirror2D));
            corruptGo.transform.SetParent(group.transform, false);
            corruptGo.transform.position = new Vector3(34.8f, -10.8f, 0f);
            corruptGo.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
            var cSr = corruptGo.GetComponent<SpriteRenderer>();
            cSr.sprite = mirrorSprite;
            cSr.sharedMaterial = ctx.LitMaterial;
            var cCol = corruptGo.GetComponent<BoxCollider2D>();
            cCol.isTrigger = true;
            cCol.size = new Vector2(0.32f, 1.0f);
            var corruptMirror = corruptGo.GetComponent<LightMirror2D>();
            corruptMirror.MirrorAngleDegrees = 135f;
            corruptMirror.IsCorrupted = true;
            corruptMirror.MirrorRenderer = cSr;
            corruptMirror.SyncVisual();

            var barrierGo = new GameObject("Darkness Melt Barrier", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(DarknessBarrier2D));
            barrierGo.transform.SetParent(group.transform, false);
            barrierGo.transform.position = new Vector3(31.5f, -14.2f, 0f);
            barrierGo.transform.localScale = Vector3.one;
            var bSr = barrierGo.GetComponent<SpriteRenderer>();
            bSr.sprite = GetArt(art, "DarknessVeil", ctx.WhiteSprite);
            bSr.sharedMaterial = ctx.LitMaterial;
            bSr.drawMode = SpriteDrawMode.Tiled;
            bSr.size = new Vector2(1.2f, 1.4f);
            bSr.color = new Color(0.28f, 0.18f, 0.42f, 0.95f);
            bSr.sortingOrder = 6;
            var bCol = barrierGo.GetComponent<BoxCollider2D>();
            bCol.isTrigger = true;
            bCol.size = new Vector2(1.2f, 1.4f);
            var barrier = barrierGo.GetComponent<DarknessBarrier2D>();
            barrier.BarrierCollider = bCol;
            barrier.BarrierRenderer = bSr;

            var projGo = new GameObject("Projector Vein", typeof(SpriteRenderer), typeof(LineRenderer), typeof(ProjectorVein2D));
            projGo.transform.SetParent(group.transform, false);
            projGo.transform.position = new Vector3(25.5f, -12.6f, 0f);
            projGo.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            var pSr = projGo.GetComponent<SpriteRenderer>();
            pSr.sprite = ctx.CrystalSprite != null ? ctx.CrystalSprite : ctx.WhiteSprite;
            pSr.sharedMaterial = ctx.LitMaterial;
            pSr.color = new Color(1f, 0.78f, 0.32f, 1f);
            var proj = projGo.GetComponent<ProjectorVein2D>();
            proj.EmitDirection = Vector2.right;
            proj.BeamLine = projGo.GetComponent<LineRenderer>();
            if (ctx.BeamMaterial != null) proj.BeamLine.sharedMaterial = ctx.BeamMaterial;

            ctx.Route.MainProjector = proj;
            ctx.Route.RotatableMirror = mirror;
            ctx.Route.CorruptedMirror = corruptMirror;
            ctx.Route.MirrorRail = rail;
            ctx.Route.DarknessBarrier = barrier;
        }

        private static void BuildShadowRealmAndFlow(BuildContext ctx, Transform parent, ChamberCompletionSequence completionSeq, Dictionary<string, Sprite> art)
        {
            // Scripted Fall Hatch in left lane of East Vertical Shaft (x: 53.1..55.5, y: 0.0)
            var hatchGo = new GameObject("Scripted Breakaway Floor", typeof(SpriteRenderer), typeof(BoxCollider2D));
            hatchGo.transform.SetParent(parent, false);
            hatchGo.transform.position = new Vector3(54.3f, 0.0f, 0f);
            hatchGo.transform.localScale = Vector3.one;
            var hSr = hatchGo.GetComponent<SpriteRenderer>();
            hSr.sprite = GetArt(art, "BreakawayPlank", ctx.WhiteSprite);
            hSr.sharedMaterial = ctx.LitMaterial;
            hSr.drawMode = SpriteDrawMode.Sliced;
            hSr.size = new Vector2(2.4f, 0.36f);
            hSr.color = Color.white;
            var hCol = hatchGo.GetComponent<BoxCollider2D>();
            hCol.size = new Vector2(2.4f, 0.32f);

            var fallTrigGo = new GameObject("Scripted Fall Trigger", typeof(BoxCollider2D), typeof(ScriptedFallTrigger2D));
            fallTrigGo.transform.SetParent(parent, false);
            fallTrigGo.transform.position = new Vector3(54.3f, 0.9f, 0f);
            var fCol = fallTrigGo.GetComponent<BoxCollider2D>();
            fCol.isTrigger = true;
            fCol.size = new Vector2(1.8f, 1.2f);
            var scriptedFall = fallTrigGo.GetComponent<ScriptedFallTrigger2D>();
            scriptedFall.BreakawayFloorCollider = hCol;
            scriptedFall.BreakawayFloorRenderer = hSr;
            ctx.Route.ScriptedFall = scriptedFall;

            // Deep Shadow Vault Zone (y: -25..-19, x: 29.75..47.25) with warm red guide beacons
            var shadowGo = new GameObject("Shadow Realm Zone", typeof(BoxCollider2D), typeof(ShadowRealmZone2D));
            shadowGo.transform.SetParent(parent, false);
            shadowGo.transform.position = new Vector3(38.5f, -22.0f, 0f);
            var sCol = shadowGo.GetComponent<BoxCollider2D>();
            sCol.isTrigger = true;
            sCol.size = new Vector2(17.5f, 5.8f);

            Light2D CreateGuideLight(string lightName, Vector3 localPos)
            {
                var lGo = new GameObject(lightName, typeof(Light2D));
                lGo.transform.SetParent(shadowGo.transform, false);
                lGo.transform.localPosition = localPos;
                var l = lGo.GetComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = new Color(0.85f, 0.28f, 0.28f);
                l.intensity = 0.85f;
                l.pointLightOuterRadius = 5.5f;
                return l;
            }

            var guideA = CreateGuideLight("Shadow Realm Guide Light A", new Vector3(-5.5f, 1.8f, 0f));
            var guideB = CreateGuideLight("Shadow Realm Guide Light B", new Vector3(4.5f, 1.8f, 0f));
            var shadow = shadowGo.GetComponent<ShadowRealmZone2D>();
            shadow.AmbientGuideLights = new[] { guideA, guideB };
            ctx.Route.ShadowRealm = shadow;

            // Bonus secret & sub-well pickups
            AddPickup(ctx, "Ember Pickup / Attic Secret", new Vector2(28.2f, 8.65f));
            AddPickup(ctx, "Ember Pickup / Deep Well", new Vector2(19.6f, -20.35f));

            // Mementos (2 standard + 1 secret inside Attic Secret Vault behind Conduit Sliding Panel)
            var m1 = AddMemento(ctx, parent, "Memento / Corridor", new Vector2(14.4f, 0.85f), "MEMENTO_01", "Ashen Clockwork", false, art);
            var m2 = AddMemento(ctx, parent, "Memento / Sanctuary", new Vector2(12.0f, -14.15f), "MEMENTO_02", "Final Decree", false, art);
            var mSecret = AddMemento(ctx, parent, "Memento / Secret Conduit", new Vector2(29.8f, 8.85f), "MEMENTO_SECRET_01", "Hidden Blueprint", true, art);
            ctx.Route.Mementos = new[] { m1, m2, mSecret };

            // Central Core Finale inside Core Sanctuary (requires 4 beams)
            var coreGo = new GameObject("Central Core Finale", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(CentralCoreFinale2D));
            coreGo.transform.SetParent(parent, false);
            coreGo.transform.position = new Vector3(5.8f, -12.2f, 0f);
            coreGo.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            var cSr = coreGo.GetComponent<SpriteRenderer>();
            cSr.sprite = ctx.CrystalSprite != null ? ctx.CrystalSprite : ctx.WhiteSprite;
            cSr.sharedMaterial = ctx.LitMaterial;
            cSr.sortingOrder = 7;
            var cCol = coreGo.GetComponent<BoxCollider2D>();
            cCol.isTrigger = true;
            var coreLightGo = new GameObject("Core Light", typeof(Light2D));
            coreLightGo.transform.SetParent(coreGo.transform, false);
            var cLight = coreLightGo.GetComponent<Light2D>();
            cLight.lightType = Light2D.LightType.Point;

            var core = coreGo.GetComponent<CentralCoreFinale2D>();
            core.RequiredBeams = 4;
            core.CompletionSequence = completionSeq;
            core.CoreRenderer = cSr;
            core.CoreLight = cLight;
            ctx.Route.CentralCoreFinale = core;
        }

        private static MementoCollectible2D AddMemento(BuildContext ctx, Transform parent, string name, Vector2 pos, string id, string title, bool secret, Dictionary<string, Sprite> art)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(MementoCollectible2D));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = art.TryGetValue("MementoRelic", out var relic) ? relic : (ctx.CrystalSprite != null ? ctx.CrystalSprite : ctx.WhiteSprite);
            sr.sharedMaterial = ctx.LitMaterial;
            sr.color = secret ? new Color(0.92f, 0.64f, 1f, 1f) : new Color(0.95f, 0.88f, 0.62f, 1f);
            sr.sortingOrder = 6;
            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 0.9f);
            var mem = go.GetComponent<MementoCollectible2D>();
            mem.MementoId = id;
            mem.Title = title;
            mem.IsSecretMemento = secret;
            return mem;
        }

        private static void RegisterSceneInBuildSettings()
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
            {
                list.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }
    }
}
