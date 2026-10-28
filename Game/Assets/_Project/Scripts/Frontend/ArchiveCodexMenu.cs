using System.Collections.Generic;
using System.Linq;
using TheLightenRoad.Codex;
using TheLightenRoad.Input;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace TheLightenRoad.Frontend
{
    /// <summary>Independent observer; ArchiveMenuStyle remains the shared static font/style utility.</summary>
    public sealed class ArchiveCodexMenu : MonoBehaviour, ISocialListener
    {
        public static bool IsOpen {get;private set;}
        private static int closedFrame=-10;
        public static bool ClosedThisFrame=>Time.frameCount<=closedFrame+1;
        private readonly List<string> entries=new List<string>();
        private SocialChannel channel;
        private GameObject panel;
        private TMP_Text progress,title,body,clue,pages,location,shortcuts;
        private CanvasGroup readingGroup;
        private RectTransform decryptionFill;
        private float revealTime;
        private Button previous,next,close;
        private AudioSource audioSource;
        private int selected,page;
        private float previousTime;
        private GameObject previousSelection;
        public int EntryCount=>entries.Count;
        private const int PageLength=600;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){IsOpen=false;closedFrame=-10;SceneManager.sceneLoaded-=Install;SceneManager.sceneLoaded+=Install;}
        static void Install(Scene scene,LoadSceneMode mode)
        {
            if(Object.FindAnyObjectByType<PlayerMotor2D>()==null || Object.FindAnyObjectByType<ArchiveCodexMenu>()!=null)return;
            new GameObject("Archive Codex").AddComponent<ArchiveCodexMenu>();
        }
        void Awake(){Build();}
        void OnEnable()
        {
            CodexSession.Ensure();channel=CodexSession.Channel;entries.Clear();
            if(channel!=null)channel.Subscribe(this);
            CodexSession.PageRequested+=Open;ControllerGlyphService.OnPromptStyleChanged+=RefreshShortcuts;Redraw();RefreshShortcuts();
        }
        void OnDisable(){Close();if(channel!=null)channel.Unsubscribe(this);CodexSession.PageRequested-=Open;ControllerGlyphService.OnPromptStyleChanged-=RefreshShortcuts;}
        public void OnSocialEvent(SocialEvent e)
        {
            if(e.kind!=SocialEventKind.LogFound || CodexSession.Catalog?.Find(e.text)==null)return;
            if(!entries.Contains(e.text))entries.Add(e.text);
            Redraw();
        }
        public void Open(string id)
        {
            int index=entries.IndexOf(id);if(index<0)return;
            selected=index;page=0;
            if(!IsOpen)
            {
                previousTime=Time.timeScale;previousSelection=EventSystem.current?.currentSelectedGameObject;
                Time.timeScale=0;GameControls.BlockGameplay(this,true);IsOpen=true;
            }
            panel.SetActive(true);revealTime=0;readingGroup.alpha=MenuPreferences.ReducedMotion?1:.15f;Redraw();RefreshShortcuts();
            var log=CodexSession.Catalog.Find(id);audioSource.Stop();
            if(log.Audio!=null){audioSource.volume=MenuPreferences.Master*MenuPreferences.Effects;audioSource.clip=log.Audio;audioSource.Play();}
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
        public void Close()
        {
            if(panel!=null)panel.SetActive(false);
            if(!IsOpen)return;
            audioSource.Stop();Time.timeScale=previousTime;GameControls.BlockGameplay(this,false);IsOpen=false;closedFrame=Time.frameCount;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        void Update()
        {
            if(IsOpen){revealTime+=Time.unscaledDeltaTime;readingGroup.alpha=MenuPreferences.ReducedMotion?1:Mathf.SmoothStep(.15f,1,Mathf.Clamp01(revealTime/.18f));}
            var kb=Keyboard.current;var pad=GameControls.ActivePad;
            if(IsOpen)
            {
                if((kb!=null && (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) || (pad!=null && pad.buttonEast.wasPressedThisFrame)){Close();return;}
                if((kb!=null && kb.leftArrowKey.wasPressedThisFrame)||(pad!=null && pad.leftShoulder.wasPressedThisFrame))Turn(-1);
                if((kb!=null && kb.rightArrowKey.wasPressedThisFrame)||(pad!=null && pad.rightShoulder.wasPressedThisFrame))Turn(1);
            }
            else if(GameControls.GameplayAllowed && entries.Count>0 && ((kb!=null && kb.tabKey.wasPressedThisFrame)||(pad!=null && pad.selectButton.wasPressedThisFrame)))Open(entries[selected]);
        }
        private int PageCount(string text)=>Mathf.Max(1,Mathf.CeilToInt((text??"").Length/(float)PageLength));
        private void Turn(int direction)
        {
            if(entries.Count==0)return;
            var log=CodexSession.Catalog.Find(entries[selected]);
            page+=direction;
            if(page<0){selected=(selected-1+entries.Count)%entries.Count;page=PageCount(CodexSession.Catalog.Find(entries[selected]).Text)-1;}
            else if(page>=PageCount(log.Text)){selected=(selected+1)%entries.Count;page=0;}
            audioSource.Stop();Redraw();
        }
        private void Redraw()
        {
            if(progress==null)return;
            progress.text=$"ARCHIVE DECRYPTION\n\n{CodexSession.Decryption:0}%\n{CodexSession.FoundCount} OF {CodexSession.Catalog?.Total??0} RECORDS";
            decryptionFill.sizeDelta=new Vector2(450*CodexSession.Decryption/100f,2);
            if(entries.Count==0){title.text="The archive is silent";body.text="Recover logs from Archive Terminals to reveal the Old World.";clue.text="";pages.text="";return;}
            selected=Mathf.Clamp(selected,0,entries.Count-1);var log=CodexSession.Catalog.Find(entries[selected]);
            location.text=string.IsNullOrWhiteSpace(log.Location)?"THE ARCHIVE":log.Location;
            previous.interactable=next.interactable=entries.Count>1 || PageCount(log.Text)>1;
            title.text=log.Title;var text=log.Text??"";page=Mathf.Clamp(page,0,PageCount(text)-1);
            int start=page*PageLength;body.text=text.Substring(start,Mathf.Min(PageLength,text.Length-start));
            clue.text="ROOM CLUE\n"+log.RoomClue;pages.text=$"LOG {selected+1} / {entries.Count}    ·    PAGE {page+1} / {PageCount(text)}";
        }
        private RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;
            r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.sizeDelta=size;r.anchoredPosition=pos;return r;
        }
        private TMP_Text Label(string name,Transform parent,Vector2 size,Vector2 pos,float font,Color color)
        {
            var t=Rect(name,parent,size,pos).gameObject.AddComponent<TextMeshProUGUI>();t.fontSize=font;t.color=color;t.raycastTarget=false;
            t.textWrappingMode=TextWrappingModes.Normal;t.richText=false;return t;
        }
        private void RefreshShortcuts()
        {
            if(shortcuts==null)return;
            var style=ControllerGlyphService.ActiveStyle;
            shortcuts.text=style==ControllerPromptStyle.PlayStation?"L1 / R1  Browse     Circle  Close     Create  Reopen":
                style==ControllerPromptStyle.Xbox?"LB / RB  Browse     B  Close     View  Reopen":"Left / Right  Browse     Esc  Close     Tab  Reopen";
        }
        private Image Rule(Transform parent,string name,Vector2 size,Vector2 pos,Color color)
        {
            var image=Rect(name,parent,size,pos).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        private static Sprite coverSprite,pageSprite;
        private void BookShape(Transform parent,string name,Vector2 size,Vector2 pos,Color color,bool page)
        {
            var image=Rect(name,parent,size,pos).gameObject.AddComponent<Image>();
            image.sprite=BookSprite(page);image.color=color;image.raycastTarget=false;
        }
        private static Sprite BookSprite(bool page)
        {
            var cached=page?pageSprite:coverSprite;if(cached!=null)return cached;
            const int width=256,height=160;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            texture.name=page?"Codex page shape":"Codex cover shape";texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float nx=Mathf.Abs((x+.5f)/width*2-1), ny=Mathf.Abs((y+.5f)/height*2-1);
                // Clipped outer corners and a shallow dip where the book is bound.
                float edge=page?.95f:.94f;
                bool inside=nx<.99f && ny<edge && nx+ny<1.84f;
                if(!page && nx<.12f && ny>.89f)inside=false;
                pixels[y*width+x]=new Color32(255,255,255,inside?(byte)255:(byte)0);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            var sprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f));
            if(page)pageSprite=sprite;else coverSprite=sprite;return sprite;
        }
        private Button Action(Transform parent,string text,float x,UnityEngine.Events.UnityAction callback)
        {
            var r=Rect(text,parent,new Vector2(220,60),new Vector2(x,-340));var bg=r.gameObject.AddComponent<Image>();bg.color=Color.clear;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=bg;b.transition=Selectable.Transition.None;b.onClick.AddListener(callback);
            var t=Label(text+" caption",r,new Vector2(190,50),Vector2.zero,22,new Color(1,.82f,.5f));t.text=text;t.alignment=TextAlignmentOptions.Center;ArchiveMenuStyle.Apply(t,"Caption",22);
            var focus=r.gameObject.AddComponent<SettingsLightFocus>();focus.Label=t;focus.CardPlate=bg;focus.PointerOffset=101;
            var diamond=Rule(r,"Focus diamond",new Vector2(6,6),new Vector2(-101,0),Color.clear);diamond.rectTransform.localRotation=Quaternion.Euler(0,0,45);focus.LeftPointer=diamond.rectTransform;
            return b;
        }
        private void Build()
        {
            if(EventSystem.current==null){var events=new GameObject("Codex EventSystem",typeof(EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));events.transform.SetParent(transform,false);}
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.ignoreListenerPause=true;audioSource.playOnAwake=false;
            var canvasGo=new GameObject("Codex Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=120;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var overlay=Rect("Codex overlay",canvasGo.transform,Vector2.zero,Vector2.zero);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.sizeDelta=Vector2.zero;panel=overlay.gameObject;
            var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(.008f,.009f,.008f,.94f);
            var r=Rect("Codex reading surface",overlay,new Vector2(1260,820),Vector2.zero);r.gameObject.AddComponent<MenuReadability>();readingGroup=r.gameObject.AddComponent<CanvasGroup>();
            var gold=new Color(.8f,.65f,.4f);var ivory=new Color(.9f,.88f,.81f);
            // An illustrated spread is drawn as geometry, so no external texture is required.
            BookShape(r, "Leather cover", new Vector2(1220,750), new Vector2(0,4), new Color(.075f,.045f,.025f), false);
            BookShape(r, "Page block", new Vector2(1194,724), new Vector2(0,12), new Color(.36f,.28f,.18f), false);
            BookShape(r, "Left parchment", new Vector2(580,696), new Vector2(-291,25), new Color(.22f,.185f,.135f), true);
            BookShape(r, "Right parchment", new Vector2(580,696), new Vector2(291,25), new Color(.24f,.205f,.15f), true);
            Rule(r,"Spine shadow",new Vector2(28,650),new Vector2(0,24),new Color(.035f,.025f,.016f,.7f));
            Rule(r,"Spine glint",new Vector2(2,630),new Vector2(13,24),new Color(.65f,.5f,.3f,.35f));
            Rule(r,"Left heading rule",new Vector2(450,1),new Vector2(-300,259),gold*.55f);
            Rule(r,"Right heading rule",new Vector2(450,1),new Vector2(300,190),gold*.55f);
            var stamp=Label("Archive stamp",r,new Vector2(450,40),new Vector2(-300,300),26,gold);stamp.text="THE FIELD ARCHIVE";ArchiveMenuStyle.Apply(stamp,"Heading",26);
            progress=Label("Decryption",r,new Vector2(450,115),new Vector2(-300,175),23,gold);
            Rule(r,"Decryption track",new Vector2(450,2),new Vector2(-300,91),new Color(.42f,.34f,.23f));
            var fill=Rule(r,"Decryption fill",new Vector2(0,2),Vector2.zero,gold);fill.rectTransform.SetParent(r.Find("Decryption track"),false);decryptionFill=fill.rectTransform;decryptionFill.anchorMin=decryptionFill.anchorMax=decryptionFill.pivot=new Vector2(0,.5f);
            var note=Label("Archive note",r,new Vector2(450,95),new Vector2(-300,8),21,new Color(.76f,.72f,.63f));note.text="Fragments of the Old World.\nRecovered records remain with you through this journey.";
            Rule(r,"Clue rule",new Vector2(450,1),new Vector2(-300,-75),gold*.45f);
            clue=Label("Room clue",r,new Vector2(450,135),new Vector2(-300,-164),21,gold);
            location=Label("Record location",r,new Vector2(450,32),new Vector2(300,315),20,gold);
            title=Label("Log title",r,new Vector2(450,75),new Vector2(300,252),32,ivory);ArchiveMenuStyle.Apply(title,"Heading",32);
            body=Label("Log text",r,new Vector2(450,395),new Vector2(300,-25),22,ivory);
            pages=Label("Page counter",r,new Vector2(450,28),new Vector2(300,-267),20,new Color(.72f,.69f,.60f));
            previous=Action(r,"Previous",-310,()=>Turn(-1));close=Action(r,"Close",0,Close);next=Action(r,"Next",310,()=>Turn(1));
            var buttons=new[]{previous,close,next};for(int i=0;i<3;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=buttons[(i+2)%3],selectOnRight=buttons[(i+1)%3]};
            shortcuts=Label("Codex shortcuts",r,new Vector2(1100,30),new Vector2(0,-390),20,new Color(.62f,.61f,.55f));shortcuts.alignment=TextAlignmentOptions.Center;
            var feedRect=Rect("Archive recovery feed",canvasGo.transform,new Vector2(500,130),Vector2.zero);
            var feedText=feedRect.gameObject.AddComponent<Text>();feedText.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");feedText.fontSize=18;feedText.color=gold;feedText.raycastTarget=false;
            var feed=feedRect.gameObject.AddComponent<PartyFeedUI>();feed.enabled=false;CodexSession.Ensure();feed.channel=CodexSession.Channel;feed.text=feedText;feed.maxLines=2;feed.enabled=true;
            feedRect.anchorMin=feedRect.anchorMax=new Vector2(.5f,0);feedRect.anchoredPosition=new Vector2(0,120);feedRect.gameObject.AddComponent<ArchiveFeedVisibility>();
            panel.SetActive(false);
        }
    }

}
