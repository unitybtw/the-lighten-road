using System;
using System.Linq;
using TheLightenRoad.Codex;
using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.Sirac.OfficeAnnex;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TheLightenRoad.Sirac.OfficeLevel.Editor
{
    public static class OfficeLevelBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/Office.unity";
        private const string Root = "Assets/_Project/Scenes/Sandbox/Sirac/OfficeLevel";
        public static readonly Rect ViewBounds = new Rect(-11, -4, 46, 10);
        private static Material material;
        private static TileBase floorTile;

        [MenuItem("Tools/The Lighten Road/Office/Complete playable route")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved Sirac/Office scene outside Play Mode first.");
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == "Office playable route"))
                throw new InvalidOperationException("The route already exists. Edit its scene objects; this command never overwrites it.");
            var grid = roots.Single(r => r.name == "Grid").transform;
            var ground = grid.GetComponentsInChildren<Tilemap>().Single(m => m.name == "Ground");
            var wall = grid.GetComponentsInChildren<Tilemap>().Single(m => m.name == "Wall");
            floorTile = ground.GetTile(new Vector3Int(-10, -4));
            var wallTile = wall.GetTile(new Vector3Int(-10, -3));
            if (floorTile == null || wallTile == null) throw new InvalidOperationException("Office source tiles are missing.");
            material = ground.GetComponent<TilemapRenderer>().sharedMaterial;
            var routeRoot = new GameObject("Office playable route");
            var route = routeRoot.AddComponent<OfficeLevelRoute>();
            Fill(ground, new RectInt(-12, -6, 23, 3), floorTile);
            PrepareCollider(ground);
            Terrain("Records landing", grid, new RectInt(11, -6, 9, 6));
            Terrain("Upper files floor", grid, new RectInt(22, -6, 7, 5));
            Terrain("Exit landing", grid, new RectInt(29, -6, 7, 7));
            Terrain("Office ceiling", grid, new RectInt(-12, 5, 48, 2));
            Terrain("West outer wall", grid, new RectInt(-12, -3, 2, 8));
            Terrain("East outer wall", grid, new RectInt(34, 1, 2, 4));
            Fill(wall, new RectInt(-10, -3, 44, 8), wallTile);
            wall.GetComponent<TilemapRenderer>().sortingOrder = -20;

            var player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerRespawnController>()).Single();
            player.transform.position = new Vector3(-6, -2.15f, 0); player.SpawnPosition = player.transform.position; player.VoidKillY = -8;
            Record(player.transform); Record(player); route.Player = player;
            var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
            var follow = camera.GetComponent<FollowCamera2D>();
            follow.Offset = new Vector3(0, 1.2f, -10); follow.SmoothTime = .18f;
            follow.LookAheadDistance = .65f; follow.IdleLookAheadDistance = .25f; follow.FallLeadDistance = .6f;
            follow.SetWorldBounds(ViewBounds, 3.7f); follow.SetTarget(player.transform, true);
            var objects = roots.Single(r => r.name == "_Objects").transform;
            foreach (var collider in objects.GetComponentsInChildren<Collider2D>()) { collider.enabled = false; Record(collider); }
            // Move complete prop groups, including their existing computers and child artwork.
            foreach (Transform prop in objects)
            {
                float x = prop.position.x;
                float rise = x >= 29 ? 4 : x >= 22 ? 2 : x >= 11 ? 3 : 0;
                prop.position += Vector3.up * rise; Record(prop);
            }
            var props = objects.GetComponentsInChildren<SpriteRenderer>();
            void Move(string name, Vector2 position)
            {
                var sprite = props.First(r => r.name == name); sprite.transform.position = position; Record(sprite.transform);
            }
            Move("ofis_0", new Vector2(1.4f, -2.484375f));
            Move("ofis_0 (1)", new Vector2(15.2f, .515625f));
            Move("ofis_0 (2)", new Vector2(-4.1f, -2.484375f));
            // Standalone computer sprites follow their corresponding desks in this authored scene.
            var computers = props.Where(r => r.name == "ofis_4").OrderBy(r => r.transform.position.x).ToArray();
            Vector2[] computerPoints = { new Vector2(-4.05f,-1.64f), new Vector2(1.45f,-1.64f), new Vector2(15.25f,1.36f) };
            for (int i=0;i<computers.Length && i<computerPoints.Length;i++) { computers[i].transform.position=computerPoints[i];Record(computers[i].transform); }
            Move("ofis_1 (1)", new Vector2(18.5f, 1.02f));
            Move("ofis_8", new Vector2(23.1f, .16f)); Move("ofis_8 (1)", new Vector2(30.7f, 2.16f));
            Move("ofis_3", new Vector2(-1.16f, 2.3f)); Move("ofis_3 (1)", new Vector2(16.6f, 2.6f));
            Move("ofis_9", new Vector2(4.25f, -2.6f));
            Move("ofis_5", new Vector2(7.6f, 1.9f)); Move("ofis_5 (1)", new Vector2(27, 2.6f));
            foreach (var sprite in props) { sprite.sortingOrder = -3; Record(sprite); }
            AddProp("Upper files window", "ofis_3", new Vector2(25, 2.9f), routeRoot.transform);
            AddProp("Office exit door", "ofis_2", new Vector2(32.7f, 2.5f), routeRoot.transform);
            foreach (var fixture in roots.Single(r => r.name == "_Lights").transform.Cast<Transform>())
            { fixture.position = new Vector3(fixture.position.x, 4.735f, fixture.position.z); Record(fixture); }
            foreach (float x in new[] {26f,32.7f})
            {
                var fixture=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/OfficeLight.prefab"),scene);
                fixture.name=x<30?"Upper files ceiling fixture":"Exit ceiling fixture";
                fixture.transform.SetParent(roots.Single(r=>r.name=="_Lights").transform,false);fixture.transform.position=new Vector3(x,4.735f,0);Record(fixture.transform);
            }
            var ambient = roots.SelectMany(r => r.GetComponentsInChildren<Light2D>()).FirstOrDefault(l=>l.lightType==Light2D.LightType.Global);
            if (ambient != null) { ambient.intensity = .16f; EditorUtility.SetDirty(ambient); }

            var books = roots.SelectMany(r=>r.GetComponentsInChildren<DiegeticWhisper>(true)).Where(w=>w.ArchiveLog!=null).ToArray();
            foreach (var book in books)
            {
                if (book.ArchiveLog.Id == "office-ledger") book.transform.position = new Vector3(-1.7f, -2.7375f);
                if (book.ArchiveLog.Id == "office-margin") book.transform.position = new Vector3(25.8f, -.7375f);
                Record(book.transform);
            }
            var pickups = roots.SelectMany(r=>r.GetComponentsInChildren<EmberPickup>()).OrderBy(p=>p.transform.position.x).ToArray();
            Vector2[] pickupPoints = { new Vector2(4,-2.45f),new Vector2(24.3f,-.45f) };
            for (int i=0;i<pickups.Length && i<pickupPoints.Length;i++){pickups[i].transform.position=pickupPoints[i];Record(pickups[i].transform);}
            route.Crate = Crate(routeRoot.transform, new Vector2(6, -2.365f));
            var checkpoint = new GameObject("Records checkpoint", typeof(BoxCollider2D), typeof(AnnexCheckpoint));
            checkpoint.transform.SetParent(routeRoot.transform,false);checkpoint.transform.position=new Vector3(14.4f,1);
            checkpoint.GetComponent<BoxCollider2D>().isTrigger=true;checkpoint.GetComponent<BoxCollider2D>().size=new Vector2(.8f,2.4f);
            checkpoint.GetComponent<AnnexCheckpoint>().Spawn=new Vector3(14.8f,.85f);
            var pit = new GameObject("Service shaft fall trigger",typeof(BoxCollider2D),typeof(HazardTrigger2D));
            pit.transform.SetParent(routeRoot.transform,false);pit.transform.position=new Vector3(21,-4.3f);
            pit.GetComponent<BoxCollider2D>().isTrigger=true;pit.GetComponent<BoxCollider2D>().size=new Vector2(2.05f,1);
            var exit = new GameObject("Office exit",typeof(BoxCollider2D),typeof(ChamberCompletionSequence),typeof(ChamberExitTrigger));
            exit.transform.SetParent(routeRoot.transform,false);exit.transform.position=new Vector3(33,2.1f);
            exit.GetComponent<BoxCollider2D>().isTrigger=true;exit.GetComponent<BoxCollider2D>().size=new Vector2(.4f,2.2f);
            string profilePath=Root+"/OfficeCompletion.asset";
            var profile=AssetDatabase.LoadAssetAtPath<ChamberCompletionProfile>(profilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<ChamberCompletionProfile>();profile.Title="THE OFFICE";profile.Eyebrow="CHAMBER COMPLETE";AssetDatabase.CreateAsset(profile,profilePath);}
            exit.GetComponent<ChamberCompletionSequence>().Profile=profile;exit.GetComponent<ChamberExitTrigger>().Player=player;
            if (!roots.SelectMany(r=>r.GetComponentsInChildren<PauseMenuController>()).Any()) new GameObject("Pause menu").AddComponent<PauseMenuController>();
            Physics2D.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=routeRoot;
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(12,1),new Vector3(50,15,1)),false);
        }
        private static void Record(UnityEngine.Object item)
        { if(PrefabUtility.IsPartOfPrefabInstance(item))PrefabUtility.RecordPrefabInstancePropertyModifications(item);EditorUtility.SetDirty(item); }
        private static void Fill(Tilemap map, RectInt rect, TileBase tile)
        { map.ClearAllTiles();foreach(var p in rect.allPositionsWithin)map.SetTile(new Vector3Int(p.x,p.y),tile);map.RefreshAllTiles(); }
        private static void Terrain(string name, Transform parent, RectInt rect)
        {
            var go=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,0,-1);
            var map=go.GetComponent<Tilemap>();go.GetComponent<TilemapRenderer>().sharedMaterial=material;
            Fill(map,rect,floorTile);PrepareCollider(map);
        }
        private static void PrepareCollider(Tilemap map)
        {
            var go=map.gameObject;var body=go.GetComponent<Rigidbody2D>();if(body==null)body=go.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Static;
            var composite=go.GetComponent<CompositeCollider2D>();if(composite==null)composite=go.AddComponent<CompositeCollider2D>();composite.geometryType=CompositeCollider2D.GeometryType.Polygons;
            var collider=go.GetComponent<TilemapCollider2D>();if(collider==null)collider=go.AddComponent<TilemapCollider2D>();collider.compositeOperation=Collider2D.CompositeOperation.Merge;
            collider.ProcessTilemapChanges();composite.GenerateGeometry();
        }
        private static AnnexPushBlock Crate(Transform parent, Vector2 position)
        {
            var go=new GameObject("Pushable office crate",typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(Rigidbody2D),typeof(AnnexPushBlock));
            go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=new Vector3(1.25f,1.25f,1);
            var sprite=go.GetComponent<SpriteRenderer>();sprite.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites/Box.png").OfType<Sprite>().First();sprite.sharedMaterial=material;sprite.sortingOrder=2;
            var body=go.GetComponent<Rigidbody2D>();body.mass=6;body.gravityScale=3;body.constraints=RigidbodyConstraints2D.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;body.interpolation=RigidbodyInterpolation2D.Interpolate;
            go.GetComponent<BoxCollider2D>().size=Vector2.one;
            go.GetComponent<BoxCollider2D>().sharedMaterial=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/_Project/Scenes/Sandbox/Sirac/OfficeAnnex/Art/CrateFriction.physicsMaterial2D");
            return go.GetComponent<AnnexPushBlock>();
        }
        private static void AddProp(string name,string spriteName,Vector2 position,Transform parent)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);go.transform.position=position;
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites/ofis.png").OfType<Sprite>().First(s=>s.name==spriteName);renderer.sharedMaterial=material;renderer.sortingOrder=-3;
        }
    }
}
