using System.Collections.Generic;
using System.Linq;
using TheLightenRoad.Codex;
using TheLightenRoad.Input;
using TheLightenRoad.Player;
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
        private TMP_Text progress,title,body,clue,pages;
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
            CodexSession.PageRequested+=Open;Redraw();
        }
        void OnDisable(){Close();if(channel!=null)channel.Unsubscribe(this);CodexSession.PageRequested-=Open;}
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
            panel.SetActive(true);Redraw();
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
            progress.text=$"ARCHIVE DECRYPTION   {CodexSession.Decryption:0}%   /   {CodexSession.FoundCount} OF {CodexSession.Catalog?.Total??0} LOGS";
            if(entries.Count==0){title.text="The archive is silent";body.text="Recover logs from Archive Terminals to reveal the Old World.";clue.text="";pages.text="";return;}
            selected=Mathf.Clamp(selected,0,entries.Count-1);var log=CodexSession.Catalog.Find(entries[selected]);
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
        private Button Action(Transform parent,string text,float x,UnityEngine.Events.UnityAction callback)
        {
            var r=Rect(text,parent,new Vector2(260,60),new Vector2(x,-310));var bg=r.gameObject.AddComponent<Image>();bg.color=new Color(.16f,.12f,.07f,.95f);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=bg;b.onClick.AddListener(callback);
            var t=Label(text+" caption",r,new Vector2(250,50),Vector2.zero,22,new Color(1,.82f,.5f));t.text=text;t.alignment=TextAlignmentOptions.Center;ArchiveMenuStyle.Apply(t,"Caption",22);return b;
        }
        private void Build()
        {
            if(EventSystem.current==null){var events=new GameObject("Codex EventSystem",typeof(EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));events.transform.SetParent(transform,false);}
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.ignoreListenerPause=true;audioSource.playOnAwake=false;
            var canvasGo=new GameObject("Codex Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=120;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var r=Rect("Codex reading surface",canvasGo.transform,new Vector2(1180,820),Vector2.zero);panel=r.gameObject;
            r.gameObject.AddComponent<MenuReadability>();var bg=r.gameObject.AddComponent<Image>();bg.color=new Color(.018f,.025f,.032f,.985f);
            var gold=new Color(1,.78f,.4f);var ivory=new Color(.94f,.92f,.85f);
            progress=Label("Decryption",r,new Vector2(1040,40),new Vector2(0,340),22,gold);
            title=Label("Log title",r,new Vector2(1040,64),new Vector2(0,268),34,ivory);ArchiveMenuStyle.Apply(title,"Heading",34);
            body=Label("Log text",r,new Vector2(1040,320),new Vector2(0,65),24,ivory);
            clue=Label("Room clue",r,new Vector2(1040,110),new Vector2(0,-170),22,gold);
            pages=Label("Page counter",r,new Vector2(1040,32),new Vector2(0,-248),20,ivory);
            previous=Action(r,"Previous",-320,()=>Turn(-1));close=Action(r,"Close",0,Close);next=Action(r,"Next",320,()=>Turn(1));
            var buttons=new[]{previous,close,next};for(int i=0;i<3;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=buttons[(i+2)%3],selectOnRight=buttons[(i+1)%3]};
            var hint=Label("Codex shortcuts",r,new Vector2(1040,32),new Vector2(0,-370),18,ivory);hint.text="Arrows / LB–RB: pages    ·    Esc / B / Circle: close    ·    Tab / View / Create: reopen";
            // Reuse the lecture feed unchanged as a separate observer.
            var feedRect=Rect("Archive recovery feed",canvasGo.transform,new Vector2(500,130),new Vector2(0,440));
            var feedText=feedRect.gameObject.AddComponent<Text>();feedText.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");feedText.fontSize=18;feedText.color=gold;feedText.raycastTarget=false;
            var feed=feedRect.gameObject.AddComponent<PartyFeedUI>();feed.enabled=false;CodexSession.Ensure();feed.channel=CodexSession.Channel;feed.text=feedText;feed.maxLines=2;feed.enabled=true;
            feedRect.anchorMin=feedRect.anchorMax=new Vector2(.5f,0);feedRect.anchoredPosition=new Vector2(0,120);
            feedRect.gameObject.AddComponent<ArchiveFeedVisibility>();
            panel.SetActive(false);
        }
    }
}
