using System;
using System.IO;
using System.Linq;
using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TheLightenRoad.FirstLight.Editor
{
    public static class FirstLightSceneBuilder
    {
        private const string Art="Assets/_Project/Art/FirstLightTutorial";
        private static Transform world;
        private static Sprite square;
        private static Material lit,unlit;
        private static FirstLightTutorial tutorial;
        private static FirstLightPresentation view;
        [MenuItem("The Lighten Road/Tutorial/Create First Light Scene")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before creating the tutorial.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before creating the tutorial.");
            if(File.Exists(FirstLightTutorial.ScenePath))throw new InvalidOperationException("First Light already exists. Edit its scene objects rather than overwriting them.");
            Directory.CreateDirectory(Art);AssetDatabase.Refresh();
            square=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Prototype/WhiteSquare.png").OfType<Sprite>().First();
            lit=AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            unlit=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(unlit,Art+"/FirstLightUnlit.mat");
            Sprite ring=TextureSprite("EmberRing",64,64,(x,y)=>{float r=new Vector2(x-31.5f,y-31.5f).magnitude;return new Color(1,1,1,Mathf.Clamp01(1-Mathf.Abs(r-27)/1.7f));});
            Sprite arch=TextureSprite("WhitePassage",32,64,(x,y)=>
            {
                float dx=Mathf.Abs(x-15.5f);float top=48+Mathf.Sqrt(Mathf.Max(0,15.5f*15.5f-dx*dx));
                float alpha=y<=top?Mathf.Clamp01(Mathf.Min(15.5f-dx,top-y)/3):0;return new Color(1,1,1,alpha);
            });
            Sprite mark=TextureSprite("SleepingMark",24,24,(x,y)=>
            {
                int dx=Mathf.Abs(x-12),dy=Mathf.Abs(y-12);bool ink=(dx+dy>=8 && dx+dy<=10) || (dx<=1 && dy<=5) || (dy<=1 && dx<=5);
                return new Color(1,1,1,ink?1:0);
            });
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            world=new GameObject("FIRST LIGHT / isolated tutorial").transform;
            tutorial=world.gameObject.AddComponent<FirstLightTutorial>();view=world.gameObject.AddComponent<FirstLightPresentation>();tutorial.Presentation=view;
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerPrototype.prefab"));
            player.name="Light / tutorial player";player.transform.position=new Vector3(0,.95f,0);
            tutorial.Player=player.GetComponent<PlayerRespawnController>();tutorial.Player.SpawnPosition=player.transform.position;tutorial.Player.VoidKillY=-4;
            var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(FollowCamera2D));cameraGo.tag="MainCamera";
            view.SceneCamera=cameraGo.GetComponent<Camera>();view.SceneCamera.orthographic=true;view.SceneCamera.orthographicSize=4.6f;
            view.SceneCamera.clearFlags=CameraClearFlags.SolidColor;view.SceneCamera.backgroundColor=Color.black;cameraGo.transform.position=new Vector3(2,2.1f,-10);
            view.Follow=cameraGo.GetComponent<FollowCamera2D>();view.Follow.SetTarget(player.transform,true);view.Follow.Offset=new Vector3(.6f,1.2f,-10);
            var follow=new SerializedObject(view.Follow);follow.FindProperty("enableBounds").boolValue=true;follow.FindProperty("boundsMin").vector2Value=new Vector2(2,2.1f);follow.FindProperty("boundsMax").vector2Value=new Vector2(27,2.8f);follow.ApplyModifiedPropertiesWithoutUndo();
            view.Ambient=Light("Darkness / global",Vector3.zero,0,0);view.Ambient.lightType=Light2D.LightType.Global;
            view.SeedHalo=Light("First ember / breath",new Vector3(2.3f,.9f),.5f,2.5f);
            view.SeedRing=SpritePart("First ember / orbit",world,ring,new Vector3(2.3f,.9f),Vector2.one,Color.clear,8,false);
            view.IgnitionRing=SpritePart("First ignition / expanding light",world,ring,Vector3.zero,Vector2.one,Color.clear,9,false);
            BuildRuins();
            tutorial.FirstEmber=Pickup("01 / the first ember",new Vector3(2.3f,.85f),false);
            tutorial.ReserveA=Pickup("02 / reserve ember",new Vector3(12.8f,1.0f),true);
            tutorial.ReserveB=Pickup("03 / reserve ember",new Vector3(15.8f,1.0f),true);
            view.MarkPositions=new[]{new Vector3(20.6f,2.1f),new Vector3(23.1f,3.25f),new Vector3(25.6f,2.1f)};
            view.MarkFaces=new SpriteRenderer[3];view.Conduits=new LineRenderer[3];
            for(int i=0;i<3;i++)
            {
                Quad("Seal / stone "+i,view.MarkPositions[i],new Vector2(.9f,1.05f),new Color(.19f,.21f,.21f),-2);
                view.MarkFaces[i]=SpritePart("Sleeping mark "+(i+1),world,mark,view.MarkPositions[i],new Vector2(.65f,.65f),new Color(.08f,.08f,.08f),4,false);
                view.Conduits[i]=Line("Light trace "+(i+1),new[]{view.MarkPositions[i],new Vector3(view.MarkPositions[i].x,.32f),new Vector3(29.1f,.32f),new Vector3(30.2f,.5f)},.035f,new Color(1,.74f,.3f));
            }
            var door=new GameObject("White passage / locked until the marks wake",typeof(BoxCollider2D),typeof(FirstLightDoor));door.transform.SetParent(world,false);door.transform.position=new Vector3(30.2f,1.9f);
            door.GetComponent<BoxCollider2D>().isTrigger=true;door.GetComponent<BoxCollider2D>().size=new Vector2(1.25f,3.2f);door.GetComponent<FirstLightDoor>().Tutorial=tutorial;
            view.DoorCore=SpritePart("White passage / living light",world,arch,new Vector3(30.2f,2.0f),new Vector2(2.2f,3.7f),Color.clear,5,false);
            var outline=new System.Collections.Generic.List<Vector3>{new Vector3(29.1f,.2f),new Vector3(29.1f,2.65f)};
            for(int i=0;i<=20;i++){float angle=Mathf.PI-i/20f*Mathf.PI;outline.Add(new Vector3(30.2f+Mathf.Cos(angle)*1.1f,2.65f+Mathf.Sin(angle)*1.1f));}
            outline.Add(new Vector3(31.3f,.2f));view.OutlinePoints=outline.ToArray();view.DoorOutline=Line("White passage / drawn outline",view.OutlinePoints,.055f,new Color(.94f,.98f,1));
            view.DoorLight=Light("White passage / radiance",new Vector3(30.2f,1.8f),0,5);view.DoorLight.color=new Color(.82f,.91f,1);
            BuildInterface();
            tutorial.Hud=new GameObject("Lantern HUD").AddComponent<LanternHUD>();tutorial.Hud.SetTarget(player.GetComponent<LanternController>());
            new GameObject("Death overlay").AddComponent<DeathScreenOverlay>().SetTarget(tutorial.Player);
            new GameObject("Pause menu").AddComponent<PauseMenuController>();
            var volume=new GameObject("First light / restrained bloom",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();var bloom=profile.Add<Bloom>();bloom.intensity.Override(.3f);bloom.threshold.Override(.85f);bloom.scatter.Override(.55f);
            AssetDatabase.CreateAsset(profile,Art+"/FirstLightBloom.asset");volume.sharedProfile=profile;view.SceneCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            EditorSceneManager.SaveScene(scene,FirstLightTutorial.ScenePath);
            var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==FirstLightTutorial.ScenePath))scenes.Add(new EditorBuildSettingsScene(FirstLightTutorial.ScenePath,true));
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();Selection.activeGameObject=world.gameObject;
        }
        private static void BuildRuins()
        {
            Solid("Arrival / uninterrupted learning floor",new Vector2(2.2f,-.45f),new Vector2(15.6f,.9f));
            Solid("Jump / low broken stone",new Vector2(7,.31f),new Vector2(1.8f,.62f));
            Solid("Archive / safe landing and seal chamber",new Vector2(22,-.25f),new Vector2(20.8f,.9f));
            Solid("West boundary",new Vector2(-5.6f,2),new Vector2(.4f,5));Solid("East boundary",new Vector2(32.5f,2),new Vector2(.4f,5));
            var random=new System.Random(5416);
            for(int i=0;i<13;i++)
            {
                float x=-4+i*3;
                Quad("Archive / sunken wall "+i,new Vector2(x,3),new Vector2(2.85f,7),new Color(.12f,.14f,.15f),-30);
                Quad("Archive / vertical fracture "+i,new Vector2(x+1.36f,3),new Vector2(.07f,7),new Color(.045f,.06f,.065f),-28);
                for(int shelf=0;shelf<3;shelf++)
                {
                    float y=1.4f+shelf*1.5f;
                    Quad("Archive / shelf beam",new Vector2(x,y),new Vector2(2.4f,.13f),new Color(.14f,.13f,.115f),-22);
                    for(int book=0;book<8;book++)
                    {
                        float h=.36f+(float)random.NextDouble()*.54f;
                        var spine=Quad("Archive / forgotten volume",new Vector2(x-1+book*.28f,y+h*.5f+.08f),new Vector2(.17f,h),Color.Lerp(new Color(.11f,.13f,.13f),new Color(.23f,.19f,.13f),(float)random.NextDouble()),-24);
                        spine.transform.localRotation=Quaternion.Euler(0,0,random.Next(-5,6));
                    }
                }
            }
            foreach(float x in new[]{-3.8f,9.2f,17.5f,27.4f,32.1f})
            {
                Quad("Ruins / fluted pier",new Vector2(x,2.2f),new Vector2(.52f,4.4f),new Color(.22f,.23f,.22f),-7);
                Quad("Ruins / pier inner shadow",new Vector2(x+.13f,2.2f),new Vector2(.16f,4.3f),new Color(.085f,.10f,.10f),-6);
                Quad("Ruins / weathered capital",new Vector2(x,4.35f),new Vector2(.88f,.22f),new Color(.27f,.26f,.22f),-5);
            }
            for(int i=0;i<55;i++)
            {
                float x=-4+(float)random.NextDouble()*36;if(x>10 && x<11.6f)continue;
                float ground=x<10?0:.2f;float w=.08f+(float)random.NextDouble()*.25f;
                Quad("Ruins / fallen chip",new Vector2(x,ground+.025f),new Vector2(w,.035f+(float)random.NextDouble()*.08f),new Color(.25f,.24f,.21f),-1);
            }
            // A quiet circular seal on the floor tells the player where to scout.
            for(int i=0;i<9;i++)Quad("Seal / worn floor inlay",new Vector2(19.5f+i*.36f,.235f),new Vector2(.2f,.035f),new Color(.38f,.31f,.18f),1);
        }
        private static void BuildInterface()
        {
            var canvasGo=new GameObject("First light / cinematic overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvasGo.transform.SetParent(world,false);
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=450;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            view.BlackVeil=Image("Darkness",canvasGo.transform,Color.black);Stretch(view.BlackVeil.rectTransform);
            view.TopBar=Image("Upper cinematic matte",canvasGo.transform,Color.black);view.TopBar.rectTransform.anchorMin=new Vector2(0,1);view.TopBar.rectTransform.anchorMax=Vector2.one;view.TopBar.rectTransform.pivot=new Vector2(.5f,1);view.TopBar.rectTransform.sizeDelta=new Vector2(0,85);
            view.BottomBar=Image("Lower cinematic matte",canvasGo.transform,Color.black);view.BottomBar.rectTransform.anchorMin=Vector2.zero;view.BottomBar.rectTransform.anchorMax=new Vector2(1,0);view.BottomBar.rectTransform.pivot=new Vector2(.5f,0);view.BottomBar.rectTransform.sizeDelta=new Vector2(0,85);
            var titles=new GameObject("First ember / title",typeof(RectTransform),typeof(CanvasGroup));titles.transform.SetParent(canvasGo.transform,false);Stretch(titles.GetComponent<RectTransform>());view.TitleGroup=titles.GetComponent<CanvasGroup>();view.TitleGroup.blocksRaycasts=false;
            view.Chapter=Label(titles.transform,"PROLOGUE",new Vector2(0,310),15,new Color(.72f,.63f,.46f));view.Chapter.characterSpacing=7;
            view.Heading=Label(titles.transform,"The First Ember",new Vector2(0,265),36,new Color(.91f,.84f,.68f));view.Heading.characterSpacing=3;
            var hints=new GameObject("One thought at a time",typeof(RectTransform),typeof(CanvasGroup));hints.transform.SetParent(canvasGo.transform,false);Stretch(hints.GetComponent<RectTransform>());view.HintGroup=hints.GetComponent<CanvasGroup>();view.HintGroup.blocksRaycasts=false;
            view.Hint=Label(hints.transform,"",new Vector2(0,-414),22,new Color(.85f,.82f,.73f));view.Hint.rectTransform.sizeDelta=new Vector2(1400,90);
            view.BlackVeil.transform.SetAsLastSibling();
        }
        private static TMP_Text Label(Transform parent,string text,Vector2 pos,float size,Color color)
        {
            var go=new GameObject(text.Length==0?"Tutorial hint":text,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var label=go.GetComponent<TextMeshProUGUI>();label.font=Resources.Load<TMP_FontAsset>("UI/ArchiveSerif");label.fontSize=size;label.text=text;label.color=color;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.rectTransform.sizeDelta=new Vector2(1200,80);label.rectTransform.anchoredPosition=pos;return label;
        }
        private static Image Image(string name,Transform parent,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static EmberPickup Pickup(string name,Vector3 position,bool magnet)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EmberPickup.prefab"));go.name=name;go.transform.SetParent(world,false);go.transform.position=position;
            var pickup=go.GetComponent<EmberPickup>();pickup.SetFuelAmount(2);pickup.EnableDarknessDormancy=false;pickup.EnableMagnetism=magnet;return pickup;
        }
        private static GameObject Solid(string name,Vector2 position,Vector2 size)
        {
            var renderer=Quad(name,position,size,new Color(.27f,.28f,.255f),0);var collider=renderer.gameObject.AddComponent<BoxCollider2D>();collider.size=renderer.sprite.bounds.size;
            Quad(name+" / worn upper lip",position+Vector2.up*(size.y*.5f-.035f),new Vector2(size.x,.07f),new Color(.46f,.43f,.34f),1);return renderer.gameObject;
        }
        private static SpriteRenderer Quad(string name,Vector3 position,Vector2 size,Color color,int order)=>SpritePart(name,world,square,position,size,color,order,true);
        private static SpriteRenderer SpritePart(string name,Transform parent,Sprite sprite,Vector3 pos,Vector2 size,Color color,int order,bool lighting)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);go.transform.position=pos;go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=color;renderer.sharedMaterial=lighting?lit:unlit;renderer.sortingOrder=order;return renderer;
        }
        private static Light2D Light(string name,Vector3 position,float intensity,float radius)
        {
            var go=new GameObject(name,typeof(Light2D));go.transform.SetParent(world,false);go.transform.position=position;var light=go.GetComponent<Light2D>();light.lightType=Light2D.LightType.Point;light.intensity=intensity;light.pointLightInnerRadius=0;light.pointLightOuterRadius=radius;light.color=new Color(1,.57f,.23f);light.shadowsEnabled=false;return light;
        }
        private static LineRenderer Line(string name,Vector3[] points,float width,Color color)
        {
            var go=new GameObject(name,typeof(LineRenderer));go.transform.SetParent(world,false);var line=go.GetComponent<LineRenderer>();line.sharedMaterial=unlit;line.useWorldSpace=true;line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.positionCount=points.Length;line.SetPositions(points);line.sortingOrder=4;line.numCornerVertices=0;line.numCapVertices=0;return line;
        }
        private static Sprite TextureSprite(string name,int width,int height,Func<int,int,Color> sample)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);var pixels=new Color[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[y*width+x]=sample(x,y);
            texture.SetPixels(pixels);texture.Apply();string path=Art+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=width;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
