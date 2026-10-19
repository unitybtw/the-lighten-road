using System;
using System.Linq;
using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.FurkanPrototype.Editor
{
    public static class FurkanPrototypeBuilder
    {
        public const string ScenePath="Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity";
        private static Sprite sprite;
        private static Material lit;
        private static TMP_FontAsset font;
        private static Transform root;
        private static FurkanLevelDirector director;
        private static readonly Color Stone=new Color32(66,70,64,255),Edge=new Color32(126,119,96,255),Iron=new Color32(39,46,44,255);
        [MenuItem("Tools/The Lighten Road/Create Furkan Tutorial")]
        public static void Create()
        {
            if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene and leave Play Mode first.");
            if(System.IO.File.Exists(ScenePath))throw new InvalidOperationException("Tutorial already exists. Edit its objects normally.");
            sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Prototype/WhiteSquare.png").OfType<Sprite>().First();
            lit=AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Scenes/Sandbox/Sirac/TestChamber/ArchiveFont.asset");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            root=new GameObject("FURKAN / TUTORIAL AND BEGINNINGS").transform;
            director=root.gameObject.AddComponent<FurkanLevelDirector>();
            var playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerPrototype.prefab");
            var player=(GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            director.SecondSpawn=new Vector3(17.8f,3.25f);director.FirstSpawn=new Vector3(.5f,.85f);player.transform.position=director.FirstSpawn;
            director.Player=player.GetComponent<PlayerRespawnController>();director.Player.SpawnPosition=director.FirstSpawn;director.Player.VoidKillY=-4;
            var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(FollowCamera2D));cameraGo.tag="MainCamera";
            var camera=cameraGo.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=4.5f;camera.backgroundColor=new Color(.025f,.037f,.039f);camera.clearFlags=CameraClearFlags.SolidColor;cameraGo.transform.position=new Vector3(.5f,2.4f,-10);
            cameraGo.GetComponent<FollowCamera2D>().SetTarget(player.transform,true);
            var followData=new SerializedObject(cameraGo.GetComponent<FollowCamera2D>());followData.FindProperty("enableBounds").boolValue=true;followData.FindProperty("boundsMin").vector2Value=new Vector2(3.5f,2.9f);followData.FindProperty("boundsMax").vector2Value=new Vector2(38f,5.1f);followData.ApplyModifiedPropertiesWithoutUndo();
            var sky=new GameObject("Main directional light",typeof(Light));sky.GetComponent<Light>().type=LightType.Directional;sky.GetComponent<Light>().intensity=.05f;
            var ambient=new GameObject("Archive ambient",typeof(Light2D)).GetComponent<Light2D>();ambient.lightType=Light2D.LightType.Global;ambient.intensity=.22f;ambient.color=new Color(.56f,.65f,.62f);
            new GameObject("Archive ambience").AddComponent<BackgroundMusicManager>();
            var hud=new GameObject("Lantern HUD").AddComponent<LanternHUD>();hud.SetTarget(player.GetComponent<LanternController>());
            new GameObject("Stamina HUD").AddComponent<StaminaHUD>().SetTarget(player.GetComponent<PlayerStamina>());
            new GameObject("Death overlay").AddComponent<DeathScreenOverlay>().SetTarget(director.Player);
            BuildArchitecture();BuildPuzzle();BuildPrompts();
            CompletionSetup.Install();
            EditorSceneManager.SaveScene(scene,ScenePath);
            var scenes=EditorBuildSettings.scenes.ToList();
            if(!scenes.Any(s=>s.path==ScenePath))scenes.Add(new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
            Selection.activeGameObject=root.gameObject;
        }
        private static void BuildArchitecture()
        {
            Solid("01 / Safe arrival floor",4,-.45f,12,.9f);
            Solid("First jump / 0.9m",5.7f,.45f,2,.9f);
            Solid("Second jump / 1.8m",8.8f,.9f,2,1.8f);
            Solid("Raised reception landing",13.8f,.65f,8,3.5f); // top 2.4
            Solid("Reception desk / static obstacle",15,2.77f,1.45f,.74f);
            Quad("Desk rim",15,3.17f,1.65f,.09f,Edge,2);
            Solid("02 / Chamber entry",18,1.35f,2.4f,2.1f);
            Solid("Compact stepping stone A",20.65f,1.35f,1.3f,.7f);
            Solid("Compact stepping stone B",22.9f,1.73f,1.3f,.74f);
            Solid("Circuit approach",26.65f,1.35f,4.3f,2.1f);
            Solid("Far side and exit",38.6f,1.35f,6.8f,2.1f);
            Solid("Optional memory ledge",32.45f,6.5f,2.2f,.4f);
            Solid("Archive west wall",-2.1f,3.5f,.3f,8);
            Solid("Archive exit wall",42.2f,4.5f,.35f,10);
            director.ReturnShutter=Solid("Chamber seal / closes behind you",16.65f,5.3f,.28f,5.8f);director.ReturnShutter.SetActive(false);
            var pit=new GameObject("Pit / darkness hazard",typeof(BoxCollider2D),typeof(HazardTrigger2D));pit.transform.SetParent(root);pit.transform.position=new Vector3(21,-4.5f);pit.GetComponent<BoxCollider2D>().size=new Vector2(49,1);pit.GetComponent<BoxCollider2D>().isTrigger=true;
            // The scenic side view follows the reference's left-to-right chamber silhouettes.
            for(int room=0;room<7;room++)
            {
                float x=room*6;
                Quad("Concrete rear panel",x,4,5.8f,10,new Color(.10f,.135f,.13f),-30);
                Quad("Rear pillar",x-2.7f,4,.25f,10,Iron,-25);
                Quad("Archive cornice",x,7.9f,5.9f,.12f,Iron,-24);
                for(int shelf=0;shelf<3;shelf++)
                {
                    float sy=1+shelf*1.3f;
                    Quad("Archive rack",x,sy,3.4f,.1f,Iron,-20);
                    for(int f=0;f<6;f++)Quad("Dusty file",x-1.4f+f*.52f,sy+.45f,.33f,.8f,new Color(.19f+f*.009f,.20f+f*.008f,.16f),-21);
                }
            }
            Label("01 / TUTORIAL",3,5.5f,3,.26f);
            Label("02 / BEGINNINGS",21,7.2f,4,.26f);
            Label("RECEPTION",13,4.4f,3,.17f);
            Label("ARCHIVE EXIT",39.7f,4.4f,3,.22f);
            // Small static lamps establish safe reading areas without exposing distant pickups.
            foreach(float x in new[]{1f,15f,18f,27.8f,36.2f,40f})
            {
                var glow=new GameObject("Wall sconce",typeof(Light2D));glow.transform.SetParent(root);glow.transform.position=new Vector3(x,3.4f);
                var light=glow.GetComponent<Light2D>();light.lightType=Light2D.LightType.Point;light.pointLightOuterRadius=2;light.intensity=.24f;light.color=new Color(1,.68f,.31f);
                Quad("Sconce iron",x,3.4f,.16f,.35f,Iron,2);Quad("Sconce flame",x,3.45f,.08f,.14f,new Color(1,.76f,.4f),3);
            }
        }
        private static void BuildPuzzle()
        {
            director.Circuit=new GameObject("Shared Ember Circuit / both nodes").AddComponent<ArchiveCircuit>();director.Circuit.transform.SetParent(root);
            var platform=Solid("Four-stop rolling shelf",30,2.25f,2,.3f).AddComponent<ArchivePatrolPlatform>();
            platform.Waypoints=new[]{new Vector2(30,2.25f),new Vector2(34,5.35f),new Vector2(34,2.25f),new Vector2(30,.85f)};
            platform.Speed=1.8f;platform.CornerPause=1;director.Circuit.Platform=platform;
            Quad("Platform brass lip",30,2.42f,2,.05f,Edge,4).transform.SetParent(platform.transform,true);
            // Dashed path is world decoration behind the platform, marking its four stops.
            for(int i=0;i<4;i++)
            {
                Vector2 a=platform.Waypoints[i],b=platform.Waypoints[(i+1)%4];
                for(int dot=0;dot<12;dot++){Vector2 p=Vector2.Lerp(a,b,dot/12f);Quad("Guide rail marker",p.x,p.y,.04f,.04f,new Color(.28f,.32f,.27f),-12);}
                Label((i+1).ToString()+" / 1s",a.x,a.y-.65f,1.5f,.14f);
            }
            director.Nodes=new[]{Node("A / INSERT",27.8f,2.95f),Node("B / RECOVER",36.2f,2.95f)};
            var pickups=new GameObject("Chamber 02 / ember reserves");pickups.transform.SetParent(root);director.PickupRoot=pickups.transform;
            director.PickupPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EmberPickup.prefab");
            foreach(var p in new[]{new Vector3(25.4f,2.95f),new Vector3(39.2f,2.95f)})
            {
                var ember=(GameObject)PrefabUtility.InstantiatePrefab(director.PickupPrefab);ember.name="Hidden Ember / scout to reveal";ember.transform.SetParent(pickups.transform);ember.transform.position=p;
            }
            var memory=Quad("Optional archive fragment",32.45f,7.15f,.32f,.45f,new Color(.86f,.75f,.45f),3);
            memory.AddComponent<BoxCollider2D>().isTrigger=true;
            director.Memento=memory.AddComponent<ArchiveMemento>();director.Memento.Director=director;
            Quad("Fragment seam",32.45f,7.15f,.22f,.035f,Iron,4).transform.SetParent(memory.transform,true);
            Label("OPTIONAL / ARCHIVE FRAGMENT",32.45f,7.95f,5,.16f);
        }
        private static ArchiveCircuitNode Node(string title,float x,float y)
        {
            var go=Quad("Vein node "+title,x,y,.22f,.65f,new Color(.35f,.59f,.64f),2);
            var node=go.AddComponent<ArchiveCircuitNode>();node.Circuit=director.Circuit;node.Core=go.GetComponent<SpriteRenderer>();
            Label(title,x,y+1.05f,3,.18f);return node;
        }
        private static void BuildPrompts()
        {
            var go=new GameObject("Tutorial prompt",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.transform.SetParent(root);go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=70;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var label=new GameObject("Instruction",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);
            var rect=label.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,127);rect.sizeDelta=new Vector2(1150,85);
            var text=label.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=23;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.90f,.87f,.75f);director.Prompt=text;
            Trigger("Jump prompt",3.3f,false,false,Vector3.zero,"SPACE / JUMP\nThe first blocks teach your jump height. SHIFT is optional.");
            Trigger("Desk prompt",13.5f,false,false,Vector3.zero,"RECEPTION\nThe fallen desk is solid scenery. Jump onto it, then continue right.");
            Trigger("Chamber boundary",17.8f,true,false,new Vector3(17.8f,3.25f),"CHAMBER 02 / BEGINNINGS\nThree embers restored. F reveals the dark crossing ahead.");
            Trigger("Circuit prompt",26.7f,false,false,Vector3.zero,"ONE CIRCUIT / TWO NODES\nE deposits one ember. Ride the shelf; recover it on the far side to freeze it.");
            Trigger("Exit",40.5f,false,true,Vector3.zero,"");
        }
        private static void Trigger(string name,float x,bool chamber,bool exit,Vector3 spawn,string message)
        {
            var go=new GameObject(name,typeof(BoxCollider2D),typeof(ArchiveTutorialTrigger));go.transform.SetParent(root);go.transform.position=new Vector3(x,4);
            go.GetComponent<BoxCollider2D>().size=new Vector2(.55f,9);go.GetComponent<BoxCollider2D>().isTrigger=true;
            var t=go.GetComponent<ArchiveTutorialTrigger>();t.Director=director;t.BeginsChamber=chamber;t.IsExit=exit;t.Spawn=spawn;t.Message=message;
        }
        private static GameObject Solid(string name,float x,float y,float width,float height)
        {
            var go=Quad(name,x,y,width,height,Stone,0);go.AddComponent<BoxCollider2D>();
            Quad("Worn upper edge",x,y+height*.5f,width,.06f,Edge,1).transform.SetParent(go.transform,true);
            return go;
        }
        private static GameObject Quad(string name,float x,float y,float width,float height,Color color,int order)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(root);go.transform.position=new Vector3(x,y);go.transform.localScale=new Vector3(width,height,1);
            var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=lit;r.color=color;r.sortingOrder=order;return go;
        }
        private static void Label(string words,float x,float y,float width,float size)
        {
            var go=new GameObject(words,typeof(TextMeshPro));go.transform.SetParent(root);go.transform.position=new Vector3(x,y);
            var t=go.GetComponent<TextMeshPro>();t.font=font;t.text=words;t.fontSize=size*10;t.alignment=TextAlignmentOptions.Center;t.color=Edge;t.rectTransform.sizeDelta=new Vector2(width,.8f);t.GetComponent<MeshRenderer>().sortingOrder=5;
        }
    }
}
