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
        private GameObject panel,footerPanel;
        private TMP_Text progress,title,body,clue,pages,location,shortcuts;
        private CanvasGroup readingGroup;
        private RectTransform decryptionFill;
        private float revealTime;
        private CanvasGroup copyGroup;
        private ArchiveBookPresentation presentation;
        private TMP_FontAsset handwriting;
        private bool opening;
        public bool IsAnimating => opening || (presentation!=null && presentation.Turn!=null && presentation.Turn.IsBusy);
        private const float OpeningDuration = 2.1f;
        private const int ReaderLayoutVersion=2;
        [SerializeField,HideInInspector] private int builtLayoutVersion;
        private Button previous,next,close,voiceToggle,voiceReplay;
        private TMP_Text voiceStatus,voiceToggleCaption,voiceTime;
        private RectTransform voiceFill;
        private Button[] readingButtons;
        private int navigationMask=-1;
        private readonly Dictionary<string,List<string>> paginatedText=new Dictionary<string,List<string>>();
        private bool narrationPaused;
        public bool IsNarrationPaused=>narrationPaused;
        private AudioSource audioSource;
        private ArchiveNarration narration;
        private ArchiveLog narrationLog;
        private bool narrationRequested;
        public bool IsNarrationPlaying=>audioSource!=null && audioSource.isPlaying;
        private int selected,page;
        private float previousTime;
        private GameObject previousSelection;
        public int EntryCount=>entries.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){IsOpen=false;closedFrame=-10;SceneManager.sceneLoaded-=Install;SceneManager.sceneLoaded+=Install;}
        static void Install(Scene scene,LoadSceneMode mode)
        {
            if(Object.FindAnyObjectByType<PlayerMotor2D>()==null || Object.FindAnyObjectByType<ArchiveCodexMenu>()!=null)return;
            new GameObject("Archive Codex").AddComponent<ArchiveCodexMenu>();
        }
        void Awake(){Build();}
        void Start()
        {
            var catalog=CodexSession.Catalog;
            if(catalog!=null && catalog==Resources.Load<ArchiveCatalog>("Codex/ArchiveCatalog"))
                foreach(var log in catalog.Logs)narration.Prepare(log);
        }
        void OnEnable()
        {
            if(builtLayoutVersion!=ReaderLayoutVersion || panel==null || presentation==null || !presentation.IsInitialized)RebuildAfterReload();
            presentation.Rebind(CanTurn,ApplyTurn,PreviewTurn);
            CodexSession.Ensure();channel=CodexSession.Channel;entries.Clear();
            if(channel!=null)channel.Subscribe(this);
            CodexSession.PageRequested+=Open;ControllerGlyphService.OnPromptStyleChanged+=RefreshShortcuts;Redraw();RefreshShortcuts();
        }
        private void RebuildAfterReload()
        {
            // Unity's script reload does not serialize delegates or runtime rendering state.
            // Rebuild only this reader's owned hierarchy if native resources were lost.
            Close();
            foreach(var child in transform.Cast<Transform>().ToArray())DestroyImmediate(child.gameObject);
            foreach(var component in GetComponents<MonoBehaviour>().Where(c=>c!=this).ToArray())DestroyImmediate(component);
            foreach(var source in GetComponents<AudioSource>())DestroyImmediate(source);
            paginatedText.Clear();navigationMask=-1;Build();
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
            bool wasOpen=IsOpen;selected=index;page=0;
            if(!IsOpen)
            {
                previousTime=Time.timeScale;previousSelection=EventSystem.current?.currentSelectedGameObject;
                Time.timeScale=0;GameControls.BlockGameplay(this,true);IsOpen=true;
            }
            presentation.Turn.Cancel();panel.SetActive(true);footerPanel.SetActive(true);presentation.Show(true);revealTime=0;opening=!wasOpen && !MenuPreferences.ReducedMotion;
            Redraw();RefreshShortcuts();AnimateBook(0);
            QueueNarration(CodexSession.Catalog.Find(id));
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
        public void Close()
        {
            if(panel!=null)panel.SetActive(false);if(footerPanel!=null)footerPanel.SetActive(false);if(presentation!=null)presentation.Show(false);
            opening=false;narrationRequested=false;narrationPaused=false; narrationLog=null;
            if(!IsOpen)return;
            audioSource.Stop();Time.timeScale=previousTime;GameControls.BlockGameplay(this,false);IsOpen=false;closedFrame=Time.frameCount;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        void Update()
        {
            if(IsOpen)
            {
                AnimateBook(Time.unscaledDeltaTime);
                audioSource.volume=MenuPreferences.Master*MenuPreferences.Effects;
                if(narrationRequested)
                {
                    var clip=audioSource.clip!=null?audioSource.clip:narration.GetReadyClip(narrationLog);
                    if(clip!=null)
                    {
                        audioSource.clip=clip;
                        if(!narrationPaused && !IsAnimating){audioSource.Play();narrationRequested=false;}
                    }
                    else if(!narration.IsPending(narrationLog))narrationRequested=false;
                }
                RefreshNarrationControls();
            }
            var kb=Keyboard.current;var pad=GameControls.ActivePad;
            if(IsOpen)
            {
                if((kb!=null && (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) || (pad!=null && pad.buttonEast.wasPressedThisFrame)){Close();return;}
                if(opening && ((kb!=null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad!=null && pad.buttonSouth.wasPressedThisFrame)))
                {revealTime=OpeningDuration;AnimateBook(0);return;}
                if(!IsAnimating)
                {
                    if((kb!=null && kb.vKey.wasPressedThisFrame)||(pad!=null && pad.buttonNorth.wasPressedThisFrame))ToggleNarration();
                    if((kb!=null && kb.rKey.wasPressedThisFrame)||(pad!=null && pad.buttonWest.wasPressedThisFrame))ReplayNarration();
                }
                if((kb!=null && kb.leftArrowKey.wasPressedThisFrame)||(pad!=null && pad.leftShoulder.wasPressedThisFrame))Turn(-1);
                if((kb!=null && kb.rightArrowKey.wasPressedThisFrame)||(pad!=null && pad.rightShoulder.wasPressedThisFrame))Turn(1);
            }
            else if(GameControls.GameplayAllowed && entries.Count>0 && ((kb!=null && kb.tabKey.wasPressedThisFrame)||(pad!=null && pad.selectButton.wasPressedThisFrame)))Open(entries[selected]);
        }
        // The model and drag controller use unscaled time while reading pauses gameplay.
        private void AnimateBook(float dt)
        {
            revealTime+=dt;
            if(MenuPreferences.ReducedMotion)opening=false;
            float unfold=opening?Mathf.SmoothStep(0,1,Mathf.Clamp01((revealTime-.25f)/1.55f)):1;
            presentation.SetOpening(unfold);copyGroup.alpha=1;
            if(revealTime>=OpeningDuration)opening=false;
            readingGroup.alpha=1;readingGroup.interactable=readingGroup.blocksRaycasts=!IsAnimating;
        }
        private void QueueNarration(ArchiveLog log)
        {
            audioSource.Stop();audioSource.clip=null;narrationPaused=false;narrationLog=log;narrationRequested=log!=null;narration.Prepare(log);RefreshNarrationControls();
        }
        /// <summary>Pause also applies while a recording is loading, preventing late autoplay.</summary>
        public void ToggleNarration()
        {
            if(!IsOpen || narrationLog==null)return;
            if(narrationPaused)
            {
                narrationPaused=false;
                if(!narrationRequested)audioSource.UnPause();
            }
            else if(narrationRequested || audioSource.isPlaying)
            {
                narrationPaused=true;audioSource.Pause();
            }
            else ReplayNarration();
            RefreshNarrationControls();
        }
        public void ReplayNarration()
        {
            if(!IsOpen || narrationLog==null)return;
            QueueNarration(narrationLog);
        }
        private void RefreshNarrationControls()
        {
            if(voiceStatus==null)return;
            bool available=narrationLog!=null && (audioSource.clip!=null || narrationRequested);
            voiceToggle.interactable=voiceReplay.interactable=available;
            ConfigureReadingNavigation();
            voiceStatus.text=!available?"Voice unavailable":narrationPaused?"Reading paused":
                narrationRequested?"Preparing the archivist…":audioSource.isPlaying?"The archivist is reading":"Reading complete";
            var clip=audioSource.clip;
            float elapsed=clip==null?0:audioSource.time;
            if(clip!=null && !narrationRequested && !narrationPaused && !audioSource.isPlaying)elapsed=clip.length;
            voiceFill.sizeDelta=new Vector2(430*(clip==null?0:Mathf.Clamp01(elapsed/Mathf.Max(.01f,clip.length))),2);
            voiceTime.text=clip==null?"TITLE & STORY":"TITLE & STORY     "+VoiceClock(elapsed)+" / "+VoiceClock(clip.length);
            voiceToggleCaption.text=narrationPaused?"Resume voice":narrationRequested || audioSource.isPlaying?"Pause voice":"Listen again";
        }
        private void ConfigureReadingNavigation()
        {
            int mask=0;for(int i=0;i<readingButtons.Length;i++)if(readingButtons[i].interactable)mask|=1<<i;
            if(mask==navigationMask)return;navigationMask=mask;
            var enabled=readingButtons.Where(b=>b.interactable).ToArray();
            for(int i=0;i<enabled.Length;i++)enabled[i].navigation=new Navigation{
                mode=Navigation.Mode.Explicit,selectOnLeft=enabled[(i+enabled.Length-1)%enabled.Length],
                selectOnRight=enabled[(i+1)%enabled.Length],selectOnDown=close};
            close.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=enabled.LastOrDefault()};
            var selectedObject=EventSystem.current?.currentSelectedGameObject;
            if(selectedObject!=null && readingButtons.Any(b=>b.gameObject==selectedObject && !b.interactable))
                EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
        private static string VoiceClock(float value)=>$"{Mathf.FloorToInt(value)/60:00}:{Mathf.FloorToInt(value)%60:00}";
        // Measure the actual typeface and line spacing; keep every character and break at word boundaries.
        private List<string> ReadingPages(string text)
        {
            text=text??"";
            if(paginatedText.TryGetValue(text,out var cached))return cached;
            var result=new List<string>();int offset=0;
            while(offset<text.Length)
            {
                int low=1,high=text.Length-offset,fit=1;
                while(low<=high)
                {
                    int length=(low+high)/2;
                    float height=body.GetPreferredValues(text.Substring(offset,length),body.rectTransform.rect.width,0).y;
                    if(height<=body.rectTransform.rect.height){fit=length;low=length+1;}else high=length-1;
                }
                if(offset+fit<text.Length)
                {
                    int boundary=fit-1;
                    while(boundary>0 && !char.IsWhiteSpace(text[offset+boundary]))boundary--;
                    if(boundary>0)fit=boundary+1;
                    else if(fit>1 && char.IsHighSurrogate(text[offset+fit-1]))fit--;
                }
                result.Add(text.Substring(offset,fit));offset+=fit;
            }
            if(result.Count==0)result.Add("");paginatedText[text]=result;return result;
        }
        private int PageCount(string text)=>ReadingPages(text).Count;
        private bool CanTurn()=>IsOpen && !opening && entries.Count>0 && (entries.Count>1 || PageCount(CodexSession.Catalog.Find(entries[selected]).Text)>1);
        private void Turn(int direction)
        {
            if(!CanTurn() || presentation.Turn.IsBusy)return;
            var log=CodexSession.Catalog.Find(entries[selected]);
            if(page+direction<0 || page+direction>=PageCount(log.Text)){audioSource.Stop();narrationRequested=false;narrationPaused=false;}
            presentation.Turn.Automatic(direction);
        }
        private void MovePage(int direction)
        {
            var log=CodexSession.Catalog.Find(entries[selected]);page+=direction;
            if(page<0){selected=(selected-1+entries.Count)%entries.Count;page=PageCount(CodexSession.Catalog.Find(entries[selected]).Text)-1;}
            else if(page>=PageCount(log.Text)){selected=(selected+1)%entries.Count;page=0;}
        }
        private void PreviewTurn(int direction)
        {
            int originalRecord=selected,originalPage=page;
            try{MovePage(direction);Redraw();presentation.RenderNextSpread();}
            finally{selected=originalRecord;page=originalPage;Redraw();}
        }
        private void ApplyTurn(int direction)
        {
            if(entries.Count==0)return;
            var log=CodexSession.Catalog.Find(entries[selected]);MovePage(direction);
            var current=CodexSession.Catalog.Find(entries[selected]);
            if(current!=log)QueueNarration(current);Redraw();
        }
        private void Redraw()
        {
            if(progress==null)return;
            progress.text=$"{CodexSession.FoundCount:00} / {CodexSession.Catalog?.Total??0:00} RECOVERED    ·    {CodexSession.Decryption:0}%";
            decryptionFill.sizeDelta=new Vector2(430*CodexSession.Decryption/100f,2);
            if(entries.Count==0){title.text="The archive is silent";body.text="Recover logs from Archive Terminals to reveal the Old World.";clue.text="";pages.text="";return;}
            selected=Mathf.Clamp(selected,0,entries.Count-1);var log=CodexSession.Catalog.Find(entries[selected]);
            location.text=(string.IsNullOrWhiteSpace(log.Location)?"THE ARCHIVE":log.Location).ToUpperInvariant();
            previous.interactable=next.interactable=entries.Count>1 || PageCount(log.Text)>1;
            title.text=log.Title;var text=log.Text??"";page=Mathf.Clamp(page,0,PageCount(text)-1);
            body.text=ReadingPages(text)[page];
            clue.text=string.IsNullOrWhiteSpace(log.RoomClue)?"No field observations recorded.":log.RoomClue;pages.text=$"RECORD {selected+1:00} / {entries.Count:00}     ·     PAGE {page+1} / {PageCount(text)}";
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
            shortcuts.text=style==ControllerPromptStyle.PlayStation?"L1 / R1  Browse    Circle  Close    Create  Reopen\nTriangle  Pause / Resume voice    Square  Replay":
                style==ControllerPromptStyle.Xbox?"LB / RB  Browse    B  Close    View  Reopen\nY  Pause / Resume voice    X  Replay":"Left / Right  Browse    Esc  Close    Tab  Reopen\nV  Pause / Resume voice    R  Replay";
        }
        private Image Rule(Transform parent,string name,Vector2 size,Vector2 pos,Color color)
        {
            var image=Rect(name,parent,size,pos).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }

        private void Hand(TMP_Text label,float size)
        {
            if(handwriting!=null)label.font=handwriting;
            label.fontSize=size;label.characterSpacing=0;label.fontStyle=FontStyles.Normal;
        }
        private Button Action(Transform parent,string text,float x,UnityEngine.Events.UnityAction callback,float y=-340,float width=220,float height=60,Color? color=null)
        {
            var r=Rect(text,parent,new Vector2(width,height),new Vector2(x,y));var bg=r.gameObject.AddComponent<Image>();bg.color=Color.clear;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=bg;b.transition=Selectable.Transition.None;b.onClick.AddListener(callback);
            var t=Label(text+" caption",r,new Vector2(width-30,height-4),Vector2.zero,22,color??new Color(1,.82f,.5f));t.text=text;t.alignment=TextAlignmentOptions.Center;ArchiveMenuStyle.Apply(t,"Caption",22);
            var focus=r.gameObject.AddComponent<SettingsLightFocus>();focus.Label=t;focus.CardPlate=bg;focus.UseInkPalette=color.HasValue;focus.PointerOffset=width*.5f-9;
            var diamond=Rule(r,"Focus diamond",new Vector2(6,6),new Vector2(-focus.PointerOffset,0),Color.clear);diamond.rectTransform.localRotation=Quaternion.Euler(0,0,45);focus.LeftPointer=diamond.rectTransform;
            return b;
        }
        private void Build()
        {
            if(EventSystem.current==null){var events=new GameObject("Codex EventSystem",typeof(EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));events.transform.SetParent(transform,false);}
            handwriting=Resources.Load<TMP_FontAsset>("UI/ArchiveHand");
            narration=gameObject.AddComponent<ArchiveNarration>();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.ignoreListenerPause=true;audioSource.playOnAwake=false;
            var canvasGo=new GameObject("Codex Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=120;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var overlay=Rect("Codex overlay",canvasGo.transform,Vector2.zero,Vector2.zero);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.sizeDelta=Vector2.zero;panel=overlay.gameObject;

            var r=Rect("Codex reading surface",overlay,new Vector2(1260,900),Vector2.zero);
            readingGroup=r.gameObject.AddComponent<CanvasGroup>();
            r=Rect("Book ink and controls",r,new Vector2(1260,900),Vector2.zero);copyGroup=r.gameObject.AddComponent<CanvasGroup>();
            var gold=new Color(.29f,.18f,.095f);var ivory=new Color(.16f,.12f,.09f);
            var stamp=Label("Archive stamp",r,new Vector2(430,30),new Vector2(-300,309),20,gold);
            stamp.text="THE FIELD ARCHIVE";ArchiveMenuStyle.Apply(stamp,"Caption",20);stamp.characterSpacing=3;
            Rule(r,"Left heading rule",new Vector2(430,1),new Vector2(-300,275),gold*.4f);
            var heading=Label("Archive heading",r,new Vector2(430,110),new Vector2(-300,211),42,ivory);
            heading.text="Recovered\nmemories";ArchiveMenuStyle.Apply(heading,"Heading",42);heading.characterSpacing=0;
            progress=Label("Decryption",r,new Vector2(430,32),new Vector2(-300,126),20,gold);
            var track=Rule(r,"Decryption track",new Vector2(430,2),new Vector2(-300,95),new Color(.42f,.34f,.23f,.25f));
            var fill=Rule(track.transform,"Decryption fill",Vector2.zero,Vector2.zero,gold);
            decryptionFill=fill.rectTransform;decryptionFill.anchorMin=decryptionFill.anchorMax=decryptionFill.pivot=new Vector2(0,.5f);
            var note=Label("Archive note",r,new Vector2(430,60),new Vector2(-300,44),21,new Color(.32f,.25f,.17f));
            note.text="A record of what the world left behind.";note.fontStyle=FontStyles.Italic;note.characterSpacing=0;
            Rule(r,"Clue margin",new Vector2(2,126),new Vector2(-520,-72),gold*.5f);
            var clueHeading=Label("Clue heading",r,new Vector2(400,28),new Vector2(-292,-32),18,gold);clueHeading.text="FIELD OBSERVATION";clueHeading.characterSpacing=2;
            clue=Label("Room clue",r,new Vector2(400,100),new Vector2(-292,-103),23,ivory);clue.lineSpacing=6;
            Rule(r,"Narration separator",new Vector2(430,1),new Vector2(-300,-157),gold*.3f);
            voiceStatus=Label("Voice status",r,new Vector2(430,30),new Vector2(-300,-187),21,ivory);
            voiceStatus.alignment=TextAlignmentOptions.Center;voiceStatus.characterSpacing=0;
            var voiceTrack=Rule(r,"Voice track",new Vector2(430,2),new Vector2(-300,-218),new Color(.42f,.34f,.23f,.25f));
            voiceFill=Rule(voiceTrack.transform,"Voice progress",Vector2.zero,Vector2.zero,gold).rectTransform;
            voiceFill.anchorMin=voiceFill.anchorMax=voiceFill.pivot=new Vector2(0,.5f);
            voiceTime=Label("Voice time",r,new Vector2(430,24),new Vector2(-300,-239),17,gold);voiceTime.alignment=TextAlignmentOptions.Center;
            voiceToggle=Action(r,"Pause voice",-415,ToggleNarration,-284,210,48,gold);
            voiceReplay=Action(r,"Replay voice",-185,ReplayNarration,-284,210,48,gold);
            voiceToggleCaption=voiceToggle.GetComponentInChildren<TMP_Text>();
            location=Label("Record location",r,new Vector2(430,28),new Vector2(300,311),18,gold);location.characterSpacing=2;
            title=Label("Log title",r,new Vector2(430,100),new Vector2(300,239),36,ivory);ArchiveMenuStyle.Apply(title,"Heading",36);
            title.characterSpacing=0;title.enableAutoSizing=true;title.fontSizeMin=26;title.fontSizeMax=36;
            Rule(r,"Right heading rule",new Vector2(430,1),new Vector2(300,175),gold*.4f);
            body=Label("Log text",r,new Vector2(430,365),new Vector2(300,-20),24,ivory);
            body.characterSpacing=0;body.lineSpacing=8;body.paragraphSpacing=10;
            body.alignment=TextAlignmentOptions.TopLeft;
            Rule(r,"Reading footer rule",new Vector2(430,1),new Vector2(300,-225),gold*.3f);
            pages=Label("Page counter",r,new Vector2(430,28),new Vector2(300,-249),18,gold);pages.alignment=TextAlignmentOptions.Center;
            previous=Action(r,"Previous",185,()=>Turn(-1),-290,200,48,gold);
            next=Action(r,"Next",415,()=>Turn(1),-290,200,48,gold);
            close=Action(r,"Close",0,Close,-385,180,52);
            readingButtons=new[]{voiceToggle,voiceReplay,previous,next};
            shortcuts=Label("Codex shortcuts",r,new Vector2(1100,48),new Vector2(0,-433),18,new Color(.72f,.69f,.59f));shortcuts.alignment=TextAlignmentOptions.Center;
            var feedRect=Rect("Archive recovery feed",canvasGo.transform,new Vector2(500,130),Vector2.zero);
            var feedText=feedRect.gameObject.AddComponent<Text>();feedText.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");feedText.fontSize=18;feedText.color=gold;feedText.raycastTarget=false;
            var feed=feedRect.gameObject.AddComponent<PartyFeedUI>();feed.enabled=false;CodexSession.Ensure();feed.channel=CodexSession.Channel;feed.text=feedText;feed.maxLines=2;feed.enabled=true;
            feedRect.anchorMin=feedRect.anchorMax=new Vector2(.5f,0);feedRect.anchoredPosition=new Vector2(0,120);feedRect.gameObject.AddComponent<ArchiveFeedVisibility>();
            Hand(heading,52);Hand(title,44);title.fontSizeMin=32;title.fontSizeMax=44;
            Hand(body,36);body.fontStyle=FontStyles.Bold;body.lineSpacing=3;body.paragraphSpacing=7;
            Hand(clue,28);clue.fontStyle=FontStyles.Bold;clue.enableAutoSizing=true;clue.fontSizeMin=24;clue.fontSizeMax=28;clue.lineSpacing=3;Hand(note,28);
            var feedCanvas=new GameObject("Archive feed canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));feedCanvas.transform.SetParent(transform,false);
            feedCanvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;feedCanvas.GetComponent<Canvas>().sortingOrder=119;
            var feedScaler=feedCanvas.GetComponent<CanvasScaler>();feedScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;feedScaler.referenceResolution=new Vector2(1920,1080);feedScaler.matchWidthOrHeight=.5f;
            feedRect.SetParent(feedCanvas.transform,false);feedRect.anchoredPosition=new Vector2(0,120);
            var footer=new GameObject("Archive reading footer",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));footer.transform.SetParent(transform,false);footerPanel=footer;
            footer.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;footer.GetComponent<Canvas>().sortingOrder=121;
            var footerScaler=footer.GetComponent<CanvasScaler>();footerScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;footerScaler.referenceResolution=new Vector2(1920,1080);footerScaler.matchWidthOrHeight=.5f;
            close.transform.SetParent(footer.transform,false);shortcuts.transform.SetParent(footer.transform,false);
            var closeRect=(RectTransform)close.transform;closeRect.anchorMin=closeRect.anchorMax=new Vector2(.5f,0);closeRect.anchoredPosition=new Vector2(0,99);
            shortcuts.rectTransform.anchorMin=shortcuts.rectTransform.anchorMax=new Vector2(.5f,0);shortcuts.rectTransform.anchoredPosition=new Vector2(0,44);
            var hint=Label("Page drag hint",footer.transform,new Vector2(740,30),Vector2.zero,18,new Color(.7f,.63f,.50f));
            hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=new Vector2(.5f,1);hint.rectTransform.anchoredPosition=new Vector2(0,-30);hint.text="Hold any page edge and pull to turn";hint.alignment=TextAlignmentOptions.Center;
            presentation=gameObject.AddComponent<ArchiveBookPresentation>();presentation.Initialize(canvas,readingButtons,CanTurn,ApplyTurn,PreviewTurn);
            panel.SetActive(false);footerPanel.SetActive(false);builtLayoutVersion=ReaderLayoutVersion;
        }
    }

}
