using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public sealed partial class MainMenuController
    {
        private readonly GameObject[] settingsSections = new GameObject[3];
        private readonly Button[] settingsTabs = new Button[3];
        private readonly TMP_Text[] settingsTabLabels = new TMP_Text[3];
        private readonly Button[] frameRateOptions = new Button[3];
        private readonly TMP_Text[] frameRateLabels = new TMP_Text[3];
        private static readonly int[] FrameLimits = {60,120,-1};
        private GameObject settingsResetPanel;
        private Button resetSettingsButton, settingsDoneButton, cancelResetButton;
        private TMP_Text settingsSaveStatus, frameRateHint;
        private float settingsSaveDelay = -1f;
        private int settingsSection;
        public int CurrentSettingsSection => settingsSection;
        public bool IsSettingsResetOpen => settingsResetPanel != null && settingsResetPanel.activeSelf;

        private void BuildSettingsWorkspace()
        {
            var surface=CreateEtherealSubViewFrame(settings,"OPTIONS","TEND THE LIGHT. FIND YOUR BALANCE.");
            var backdrop=CreateAnchoredRect("Settings reading surface",surface,new Vector2(1580,1120),Vector2.one*.5f,Vector2.zero);
            backdrop.SetAsFirstSibling();
            var shade=backdrop.gameObject.AddComponent<Image>();
            shade.sprite=softRadialSprite;shade.color=new Color(.006f,.008f,.010f,.98f);shade.raycastTarget=false;
            BuildSettingsLanterns(surface);
            SettingsRule(surface,"Footer rule",new Vector2(0,-235),880);
            var names=new[]{"Audio","Display","Accessibility"};
            var descriptions=new[]{"Find your balance.","Keep the archive in focus.","Explore at your own pace."};
            for(int i=0;i<3;i++)
            {
                int index=i;
                settingsTabs[i]=SettingsButton(surface,names[i],new Vector2(-365,145-i*64),new Vector2(210,52),()=>SelectSettingsSection(index));
                settingsTabLabels[i]=settingsTabs[i].GetComponentInChildren<TMP_Text>();
                var section=CreateAnchoredRect(names[i]+" settings",surface,new Vector2(650,400),Vector2.one*.5f,new Vector2(120,0));
                settingsSections[i]=section.gameObject;
                SettingsText(section,names[i],new Vector2(0,210),24,radiantIvory,650);
                SettingsText(section,descriptions[i],new Vector2(0,174),14,moonlightSilver,650);
            }
            var audio=(RectTransform)settingsSections[0].transform;
            masterSlider=CreateEtherealSlider(audio,"Master Volume","Overall audio level",110,out masterValueText);
            musicSlider=CreateEtherealSlider(audio,"Ambient Music","Music and atmospheric score",10,out musicValueText);
            effectsSlider=CreateEtherealSlider(audio,"Sound Effects (SFX)","Footsteps, lantern and interactions",-90,out effectsValueText);
            masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            musicSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            effectsSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            SettingsText(audio,"Balance music and the sounds of the archive.",new Vector2(0,-166),13,moonlightSilver,650);

            var display=(RectTransform)settingsSections[1].transform;
            fullscreenToggle=CreateEtherealToggle(display,"Fullscreen","Use the whole display",110,out fullscreenToggleValueText,OnFullscreenToggled);
            vsyncToggle=CreateEtherealToggle(display,"V-Sync","Match the display refresh rate",10,out vsyncToggleValueText,OnVSyncToggled);
            BuildFrameLimit(display);
            var accessibility=(RectTransform)settingsSections[2].transform;
            motionToggle=CreateEtherealToggle(accessibility,"Reduced motion","Reduce parallax, sway and HUD motion",110,out motionToggleValueText,OnReducedMotionToggled);
            SettingsText(accessibility,"A steadier view",new Vector2(14,-1),18,radiantIvory,620);
            var note=SettingsText(accessibility,"Keeps the archive atmosphere while reducing decorative movement.\nGameplay, puzzle timing and controls remain the same.",new Vector2(14,-65),15,moonlightSilver,620);
            note.rectTransform.sizeDelta=new Vector2(620,100);

            settingsSaveStatus=SettingsText(surface,"Preferences are saved on this device",new Vector2(120,-205),13,moonlightSilver,650);
            resetSettingsButton=SettingsButton(surface,"Restore defaults",new Vector2(-350,-284),new Vector2(250,48),RequestSettingsReset);
            settingsDoneButton=SettingsButton(surface,"Done",new Vector2(350,-284),new Vector2(230,48),ShowHome);
            SettingsText(surface,"ESC / BACK",new Vector2(-360,-332),12,moonlightSilver,240);
            BuildSettingsReset(surface);
            SelectSettingsSection(0);
            SyncSettingsUI();
            ConfigureSettingsNavigation();
        }

        public void SelectSettingsSection(int section)
        {
            settingsSection=Mathf.Clamp(section,0,2);
            if(settingsResetPanel!=null)settingsResetPanel.SetActive(false);
            for(int i=0;i<settingsSections.Length;i++)
            {
                if(settingsSections[i]!=null)settingsSections[i].SetActive(i==settingsSection);
                if(settingsTabs[i]!=null)settingsTabs[i].interactable=true;
                if(settingsTabLabels[i]!=null)settingsTabLabels[i].color=i==settingsSection?amberGold:radiantIvory;
                var focus=settingsTabs[i]!=null?settingsTabs[i].GetComponent<SettingsLightFocus>():null;
                if(focus!=null)focus.IsCurrent=i==settingsSection;
                var marker=settingsTabs[i]!=null?settingsTabs[i].transform.Find("Selection rule"):null;
                if(marker!=null)marker.gameObject.SetActive(i==settingsSection);
            }
            if(resetSettingsButton!=null)resetSettingsButton.interactable=true;
            if(settingsDoneButton!=null)settingsDoneButton.interactable=true;
        }

        private void BuildFrameLimit(RectTransform parent)
        {
            var row=CreateAnchoredRect("Target Frame Rate",parent,new Vector2(650,90),Vector2.one*.5f,new Vector2(0,-100));
            SettingsText(row,"Frame limit",new Vector2(-175,20),18,radiantIvory,300);
            frameRateHint=SettingsText(row,"",new Vector2(14,-54),13,moonlightSilver,630);
            for(int i=0;i<3;i++)
            {
                int limit=FrameLimits[i];
                frameRateOptions[i]=SettingsButton(row,limit<0?"Unlimited":limit+" FPS",new Vector2(44+i*115,20),new Vector2(108,44),()=>SetSettingsFrameRate(limit));
                frameRateLabels[i]=frameRateOptions[i].GetComponentInChildren<TMP_Text>();
                frameRateLabels[i].fontSize=14;
            }
            RefreshSettingsFrameRate();
        }

        public void SetSettingsFrameRate(int limit)
        {
            if(MenuPreferences.VSync || (limit!=60 && limit!=120 && limit!=-1))return;
            MenuPreferences.SetTargetFPS(limit);
            MarkSettingsChanged();RefreshSettingsFrameRate();
        }
        private void RefreshSettingsFrameRate()
        {
            for(int i=0;i<frameRateOptions.Length;i++)
            {
                if(frameRateOptions[i]==null)continue;
                frameRateOptions[i].interactable=!MenuPreferences.VSync;
                frameRateLabels[i].color=MenuPreferences.TargetFPS==FrameLimits[i]?amberGold:radiantIvory;
                var marker=frameRateOptions[i].transform.Find("Selection rule");
                if(marker!=null)marker.gameObject.SetActive(MenuPreferences.TargetFPS==FrameLimits[i]);
            }
            if(frameRateHint!=null)frameRateHint.text=MenuPreferences.VSync
                ?"Controlled by V-Sync. Turn it off to choose a frame limit."
                :"Choose a maximum frame rate. Unlimited may use more power.";
        }

        private void BuildSettingsReset(RectTransform parent)
        {
            var panel=CreateAnchoredRect("Restore settings confirmation",parent,new Vector2(650,400),Vector2.one*.5f,new Vector2(120,0));
            settingsResetPanel=panel.gameObject;
            SettingsText(panel,"Restore default settings?",new Vector2(0,130),25,radiantIvory,650);
            var note=SettingsText(panel,"Audio, display and accessibility preferences will return to their defaults.\nYour chapter progress will be kept.",new Vector2(0,55),16,moonlightSilver,650);
            note.rectTransform.sizeDelta=new Vector2(650,100);
            cancelResetButton=SettingsButton(panel,"Keep my settings",new Vector2(-160,-55),new Vector2(280,52),CancelSettingsReset);
            SettingsButton(panel,"Restore defaults",new Vector2(165,-55),new Vector2(280,52),ResetDefaults);
            settingsResetPanel.SetActive(false);
        }
        public void RequestSettingsReset()
        {
            if(settingsResetPanel==null)return;
            foreach(var section in settingsSections)if(section!=null)section.SetActive(false);
            foreach(var tab in settingsTabs)if(tab!=null)tab.interactable=false;
            resetSettingsButton.interactable=false;settingsDoneButton.interactable=false;
            settingsResetPanel.SetActive(true);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(cancelResetButton.gameObject);
        }
        public void CancelSettingsReset()
        {
            if(settingsResetPanel==null)return;
            bool wasOpen=settingsResetPanel.activeSelf;
            SelectSettingsSection(settingsSection);
            if(wasOpen && EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(resetSettingsButton.gameObject);
        }

        private void MarkSettingsChanged()
        {
            settingsSaveDelay=.45f;
            if(settingsSaveStatus!=null)settingsSaveStatus.text="Applying changes...";
        }
        private void TickSettingsSave(float dt)
        {
            if(settingsSaveDelay<0f)return;
            settingsSaveDelay-=dt;
            if(settingsSaveDelay<=0f)FlushSettingsSave();
        }
        private void FlushSettingsSave()
        {
            MenuPreferences.Save();settingsSaveDelay=-1f;
            if(settingsSaveStatus!=null)settingsSaveStatus.text="All changes saved";
        }

        private void ConfigureSettingsNavigation()
        {
            Selectable[] first={masterSlider,fullscreenToggle,motionToggle};
            for(int i=0;i<3;i++)
            {
                var nav=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=i>0?settingsTabs[i-1]:settingsDoneButton,
                    selectOnDown=i<2?settingsTabs[i+1]:resetSettingsButton,selectOnRight=first[i]};
                settingsTabs[i].navigation=nav;
            }
        }
        private Button SettingsButton(RectTransform parent,string text,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var rect=CreateAnchoredRect(text,parent,size,Vector2.one*.5f,position);
            var image=rect.gameObject.AddComponent<Image>();image.color=Color.clear;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var label=CreateLabel("Caption",rect,text,17,radiantIvory,size,Vector2.one*.5f,Vector2.zero,Vector2.one*.5f);
            label.alignment=TextAlignmentOptions.Center;label.characterSpacing=2;
            AddSettingsFocus(rect,button,label);
            var rule=CreateAnchoredRect("Selection rule",rect,new Vector2(size.x-24,2),new Vector2(.5f,0),new Vector2(0,3),new Vector2(.5f,0));
            var line=rule.gameObject.AddComponent<Image>();line.sprite=filigreeSprite;line.color=amberGold;line.raycastTarget=false;rule.gameObject.SetActive(false);
            button.onClick.AddListener(()=>{action();if(Application.isPlaying)SoundEffectManager.Instance?.PlayUIClick();});
            return button;
        }
        private void AddSettingsFocus(RectTransform rect,Selectable control,TMP_Text label)
        {
            control.transition=Selectable.Transition.None;
            if(!(control is Slider) && control.targetGraphic!=null)control.targetGraphic.color=Color.clear;
            var halo=CreateAnchoredRect("Lantern focus",rect,new Vector2(rect.sizeDelta.x*1.25f,rect.sizeDelta.y*2.2f),Vector2.one*.5f,Vector2.zero);
            halo.SetAsFirstSibling();
            var glow=halo.gameObject.AddComponent<Image>();glow.sprite=softRadialSprite;glow.color=Color.clear;glow.raycastTarget=false;
            var focus=rect.gameObject.AddComponent<SettingsLightFocus>();focus.Glow=glow;focus.Label=label;
        }
        private void BuildSettingsLanterns(RectTransform parent)
        {
            var ambience=parent.gameObject.AddComponent<SettingsLanternAmbience>();
            ambience.Visibility=settingsGroup;
            ambience.Pivots=new RectTransform[2];ambience.Glows=new Image[2];
            // Reuse the game's actual cage lamp and chain sprites, including in player builds.
            var lampSprite=hangingLampSprite;
            var chainSprite=chainLinkSprite;
            if(lampSprite==null && stage!=null)
            {
                foreach(var source in stage.GetComponentsInChildren<Image>())
                {
                    if(source.name=="Ceiling Lamp Head")lampSprite=source.sprite;
                    if(source.name=="ChainLink_0")chainSprite=source.sprite;
                }
            }
            for(int i=0;i<2;i++)
            {
                var pivot=CreateAnchoredRect("Settings archive lantern "+i,parent,new Vector2(100,100),Vector2.one*.5f,i==0?new Vector2(-385,425):new Vector2(510,370));
                pivot.pivot=new Vector2(.5f,1);pivot.localScale=Vector3.one*(i==0?1f:.72f);
                ambience.Pivots[i]=pivot;
                for(int c=0;c<4;c++)
                {
                    var chain=CreateAnchoredRect("Chain",pivot,new Vector2(12,26),new Vector2(.5f,1),new Vector2(0,-10-c*20));
                    var image=chain.gameObject.AddComponent<Image>();image.sprite=chainSprite;image.color=new Color(.67f,.53f,.34f,.8f);image.raycastTarget=false;
                }
                var glowRect=CreateAnchoredRect("Archive lamplight",pivot,new Vector2(380,520),new Vector2(.5f,1),new Vector2(0,-215));
                var light=glowRect.gameObject.AddComponent<Image>();light.sprite=softRadialSprite;light.raycastTarget=false;light.color=new Color(1,.61f,.2f,.14f);
                ambience.Glows[i]=light;
                var cage=CreateAnchoredRect("Cage lamp",pivot,new Vector2(80,112),new Vector2(.5f,1),new Vector2(0,-135));
                var lamp=cage.gameObject.AddComponent<Image>();lamp.sprite=lampSprite;lamp.preserveAspect=true;lamp.color=new Color(1,.86f,.61f);lamp.raycastTarget=false;
            }
        }
        private TMP_Text SettingsText(RectTransform parent,string words,Vector2 position,float size,Color color,float width)
        {
            var text=CreateLabel(words,parent,words,size,color,new Vector2(width,36),Vector2.one*.5f,position,Vector2.one*.5f);
            text.alignment=TextAlignmentOptions.Left;text.characterSpacing=.4f;
            return text;
        }
        private void SettingsRule(RectTransform parent,string name,Vector2 pos,float width)
        {
            var rect=CreateAnchoredRect(name,parent,new Vector2(width,1),Vector2.one*.5f,pos);
            var image=rect.gameObject.AddComponent<Image>();image.sprite=filigreeSprite;image.color=new Color(.68f,.48f,.24f,.5f);image.raycastTarget=false;
        }
    }
}
