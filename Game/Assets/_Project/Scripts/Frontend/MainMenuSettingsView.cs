using TheLightenRoad.Audio;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public sealed partial class MainMenuController
    {
        private readonly GameObject[] settingsSections = new GameObject[4];
        private readonly Button[] settingsTabs = new Button[4];
        private readonly TMP_Text[] settingsTabLabels = new TMP_Text[4];
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

        // Controller Settings elements
        private Button controllerStyleButton;
        private TMP_Text controllerStyleValueText;
        private Toggle vibrationToggle;
        private TMP_Text vibrationToggleValueText;
        private Slider deadzoneSlider;
        private TMP_Text deadzoneValueText;
        private TMP_Text controllerMappingSummaryText;
        private readonly Image[] controllerMappingIcons = new Image[5];

        private void BuildSettingsWorkspace()
        {
            var surface=CreateEtherealSubViewFrame(settings,"OPTIONS","AUDIO, DISPLAY, CONTROLS & ACCESSIBILITY");
            var backdrop=CreateAnchoredRect("Settings reading surface",surface,new Vector2(1580,1120),Vector2.one*.5f,Vector2.zero);
            backdrop.SetAsFirstSibling();
            var shade=backdrop.gameObject.AddComponent<Image>();
            shade.sprite=softRadialSprite;shade.color=new Color(.006f,.010f,.020f,.96f);shade.raycastTarget=false;
            BuildSettingsLanterns(surface);
            SettingsRule(surface,"Top tab rule",new Vector2(0,150),720);
            SettingsRule(surface,"Footer rule",new Vector2(0,-230),720);
            var names=new[]{"Audio","Display","Controls","Accessibility"};
            var descriptions=new[]{
                "Master, music and sound effects volumes.",
                "Window mode, vertical sync and frame rate.",
                "Controller type (Xbox / PlayStation), prompts and vibration.",
                "Motion and visual comfort settings."
            };
            for(int i=0;i<4;i++)
            {
                int index=i;
                float xOffset = (i - 1.5f) * 170f;
                settingsTabs[i]=SettingsButton(surface,names[i],new Vector2(xOffset,182f),new Vector2(162,48),()=>SelectSettingsSection(index));
                settingsTabLabels[i]=settingsTabs[i].GetComponentInChildren<TMP_Text>();
                settingsTabLabels[i].fontSize = 13.5f;
                var section=CreateAnchoredRect(names[i]+" settings",surface,new Vector2(720,350),Vector2.one*.5f,new Vector2(0,-22));
                settingsSections[i]=section.gameObject;
                var desc=SettingsText(section,descriptions[i],new Vector2(0,140),13f,moonlightSilver,720);
                desc.alignment=TextAlignmentOptions.Center;
            }
            var audio=(RectTransform)settingsSections[0].transform;
            masterSlider=CreateEtherealSlider(audio,"Master Volume","Overall audio level",84,out masterValueText);
            musicSlider=CreateEtherealSlider(audio,"Ambient Music","Music and atmospheric score",-6,out musicValueText);
            effectsSlider=CreateEtherealSlider(audio,"Sound Effects (SFX)","Footsteps, lantern and interactions",-96,out effectsValueText);
            masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            musicSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            effectsSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            var audioHint=SettingsText(audio,"Adjust audio levels for music and gameplay effects.",new Vector2(0,-156),12.5f,moonlightSilver,720);
            audioHint.alignment=TextAlignmentOptions.Center;

            var display=(RectTransform)settingsSections[1].transform;
            fullscreenToggle=CreateEtherealToggle(display,"Fullscreen","Use the whole display",84,out fullscreenToggleValueText,OnFullscreenToggled);
            vsyncToggle=CreateEtherealToggle(display,"V-Sync","Match the display refresh rate",-6,out vsyncToggleValueText,OnVSyncToggled);
            BuildFrameLimit(display);

            // Controls settings section (index 2)
            var controls=(RectTransform)settingsSections[2].transform;
            var styleRow=CreateAnchoredRect("Prompt Style Row",controls,new Vector2(680,54),Vector2.one*0.5f,new Vector2(0,92));
            var styleTitle=CreateLabel("Title",styleRow,"PROMPT GLYPHS",15f,radiantIvory,new Vector2(300,24),new Vector2(0,0.5f),new Vector2(8,10),new Vector2(0,0.5f));
            styleTitle.fontStyle=FontStyles.Bold; styleTitle.characterSpacing=2.5f;
            var styleSub=CreateLabel("Subtitle",styleRow,"Xbox (A/B/X/Y), PlayStation (Cross/Circle) or PC",11.5f,moonlightSilver,new Vector2(340,20),new Vector2(0,0.5f),new Vector2(8,-12),new Vector2(0,0.5f));
            controllerStyleButton=SettingsButton(styleRow,"< AUTO-DETECT >",new Vector2(175,0),new Vector2(250,38),CycleControllerStyle);
            controllerStyleValueText=controllerStyleButton.GetComponentInChildren<TMP_Text>();
            controllerStyleValueText.fontSize=12.5f;

            vibrationToggle=CreateEtherealToggle(controls,"Vibration (Rumble)","Haptic rumble on jump, flare and impacts",22,out vibrationToggleValueText,OnVibrationToggled);

            deadzoneSlider=CreateEtherealSlider(controls,"Stick Deadzone","Analog stick inner drift threshold",-48,out deadzoneValueText);
            deadzoneSlider.minValue=0.05f; deadzoneSlider.maxValue=0.40f;
            deadzoneSlider.onValueChanged.AddListener(OnDeadzoneSliderChanged);

            var guideCard=CreateAnchoredRect("Controller Mapping Preview",controls,new Vector2(700,80),Vector2.one*0.5f,new Vector2(0,-118));
            var guideBg=guideCard.gameObject.AddComponent<Image>();
            guideBg.sprite=softRadialSprite; guideBg.color=new Color(0.012f,0.024f,0.045f,0.85f); guideBg.raycastTarget=false;
            controllerMappingSummaryText=SettingsText(guideCard,"",new Vector2(0,22),12f,amberGold,680);
            controllerMappingSummaryText.alignment=TextAlignmentOptions.Center;
            controllerMappingSummaryText.characterSpacing=1.2f;

            string[] actionNames = { "JUMP", "FLARE", "SPRINT", "INTERACT", "PAUSE" };
            float[] xOffsets = { -252f, -126f, 0f, 126f, 252f };
            for (int i = 0; i < 5; i++)
            {
                var slot = CreateAnchoredRect("Slot_" + i, guideCard, new Vector2(124, 36), new Vector2(0.5f, 0.5f), new Vector2(xOffsets[i], -14));
                var iconRect = CreateAnchoredRect("Icon", slot, new Vector2(32, 32), new Vector2(0, 0.5f), new Vector2(18, 0));
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                controllerMappingIcons[i] = icon;

                var lbl = CreateLabel("Label", slot, actionNames[i], 12f, radiantIvory, new Vector2(82, 24), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(0, 0.5f));
                lbl.fontStyle = FontStyles.Bold;
                lbl.characterSpacing = 1f;
            }

            var accessibility=(RectTransform)settingsSections[3].transform;
            motionToggle=CreateEtherealToggle(accessibility,"Reduced motion","Reduce parallax, sway and HUD motion",84,out motionToggleValueText,OnReducedMotionToggled);
            var comfortCard=CreateAnchoredRect("Motion Comfort Card",accessibility,new Vector2(680,104),Vector2.one*.5f,new Vector2(0,-36));
            var titleComfort=SettingsText(comfortCard,"MOTION COMFORT",new Vector2(0,22),15f,radiantIvory,680);
            titleComfort.alignment=TextAlignmentOptions.Center;titleComfort.fontStyle=FontStyles.Bold;titleComfort.characterSpacing=2.5f;
            var note=SettingsText(comfortCard,"Reduces camera sway, parallax and UI animations. Gameplay, puzzle timing and controls remain unchanged.",new Vector2(0,-14),12.5f,moonlightSilver,660);
            note.alignment=TextAlignmentOptions.Center;
            note.rectTransform.sizeDelta=new Vector2(660,54);
            SettingsRule(comfortCard,"Comfort Bottom Rule",new Vector2(0,-48),540);

            settingsSaveStatus=SettingsText(surface,"Preferences are saved on this device",new Vector2(0,-206),12.5f,moonlightSilver,720);
            settingsSaveStatus.alignment=TextAlignmentOptions.Center;
            resetSettingsButton=SettingsButton(surface,"Restore defaults",new Vector2(-180,-268),new Vector2(280,46),RequestSettingsReset);
            settingsDoneButton=SettingsButton(surface,"Done",new Vector2(180,-268),new Vector2(280,46),ShowHome);
            BuildSettingsReset(surface);
            SelectSettingsSection(0);
            SyncSettingsUI();
            ConfigureSettingsNavigation();
        }

        public void SelectSettingsSection(int section)
        {
            settingsSection=Mathf.Clamp(section,0,3);
            if(settingsResetPanel!=null)settingsResetPanel.SetActive(false);
            for(int i=0;i<settingsSections.Length;i++)
            {
                bool active=(i==settingsSection);
                if(settingsSections[i]!=null)settingsSections[i].SetActive(active);
                if(settingsTabs[i]!=null)settingsTabs[i].interactable=true;
                if(settingsTabLabels[i]!=null)settingsTabLabels[i].color=active?amberGold:radiantIvory;
                var focus=settingsTabs[i]!=null?settingsTabs[i].GetComponent<SettingsLightFocus>():null;
                if(focus!=null)focus.IsCurrent=active;
            }
            if (settingsSection == 2) RefreshControllerSettingsUI();
            if(resetSettingsButton!=null)resetSettingsButton.interactable=true;
            if(settingsDoneButton!=null)settingsDoneButton.interactable=true;
        }

        private void BuildFrameLimit(RectTransform parent)
        {
            var row=CreateAnchoredRect("Target Frame Rate",parent,new Vector2(680,70),Vector2.one*.5f,new Vector2(0,-96));

            var titleTxt=CreateLabel("Title",row,"FRAME LIMIT",16.5f,radiantIvory,new Vector2(300,24),new Vector2(0,0.5f),new Vector2(8,11),new Vector2(0,0.5f));
            titleTxt.fontStyle=FontStyles.Bold;titleTxt.characterSpacing=2.5f;
            frameRateHint=CreateLabel("Subtitle",row,"",12f,moonlightSilver,new Vector2(340,22),new Vector2(0,0.5f),new Vector2(8,-12),new Vector2(0,0.5f));

            for(int i=0;i<3;i++)
            {
                int limit=FrameLimits[i];
                frameRateOptions[i]=SettingsButton(row,limit<0?"Unlimited":limit+" FPS",new Vector2(102+i*108,0),new Vector2(102,38),()=>SetSettingsFrameRate(limit));
                frameRateLabels[i]=frameRateOptions[i].GetComponentInChildren<TMP_Text>();
                frameRateLabels[i].fontSize=13f;
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
                bool active=(MenuPreferences.TargetFPS==FrameLimits[i]);
                frameRateOptions[i].interactable=!MenuPreferences.VSync;
                frameRateLabels[i].color=active?amberGold:radiantIvory;
                var marker=frameRateOptions[i].transform.Find("Selection rule");
                if(marker!=null)marker.gameObject.SetActive(active);
            }
            if(frameRateHint!=null)frameRateHint.text=MenuPreferences.VSync
                ?"Controlled by V-Sync. Turn it off to choose a limit."
                :"Maximum frame rate limit.";
        }

        private void BuildSettingsReset(RectTransform parent)
        {
            var panel=CreateAnchoredRect("Restore settings confirmation",parent,new Vector2(720,300),Vector2.one*.5f,new Vector2(0,-22));
            var panelBg=panel.gameObject.AddComponent<Image>();
            panelBg.sprite=softRadialSprite;
            panelBg.color=new Color(0.008f,0.014f,0.028f,0.96f);

            settingsResetPanel=panel.gameObject;
            SettingsRule(panel,"Reset Top Rule",new Vector2(0,122),520);
            var head=SettingsText(panel,"RESTORE DEFAULT SETTINGS?",new Vector2(0,82),24,radiantIvory,660);
            head.alignment=TextAlignmentOptions.Center;head.fontStyle=FontStyles.Bold;head.characterSpacing=4f;
            var note=SettingsText(panel,"Audio, display and accessibility preferences will return to their defaults.\nYour chapter progress will be kept.",new Vector2(0,18),14.5f,moonlightSilver,660);
            note.alignment=TextAlignmentOptions.Center;
            note.rectTransform.sizeDelta=new Vector2(660,72);
            SettingsRule(panel,"Reset Bottom Rule",new Vector2(0,-32),520);
            cancelResetButton=SettingsButton(panel,"Keep my settings",new Vector2(-155,-76),new Vector2(270,46),CancelSettingsReset);
            SettingsButton(panel,"Restore defaults",new Vector2(155,-76),new Vector2(270,46),ResetDefaults);
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
            Selectable[] first={masterSlider,fullscreenToggle,controllerStyleButton,motionToggle};
            for(int i=0;i<4;i++)
            {
                var nav=new Navigation{mode=Navigation.Mode.Explicit,
                    selectOnLeft=i>0?settingsTabs[i-1]:settingsDoneButton,
                    selectOnRight=i<3?settingsTabs[i+1]:resetSettingsButton,
                    selectOnDown=first[i]};
                settingsTabs[i].navigation=nav;
            }
        }

        private void CycleControllerStyle()
        {
            int current = MenuPreferences.ControllerStyle;
            int next = (current + 1) % 4;
            MenuPreferences.SetControllerStyle(next);
            MarkSettingsChanged();
            RefreshControllerSettingsUI();
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void OnVibrationToggled(bool value)
        {
            MenuPreferences.SetVibration(value);
            MarkSettingsChanged();
            UpdateSwitchVisual(vibrationToggle);
            if (value && Application.isPlaying)
            {
                ControllerGlyphService.Rumble(0.4f, 0.6f, 0.15f);
                SoundEffectManager.Instance?.PlayUIClick();
            }
        }

        private void OnDeadzoneSliderChanged(float val)
        {
            MenuPreferences.SetStickDeadzone(val);
            if (deadzoneValueText != null) deadzoneValueText.text = Mathf.RoundToInt(val * 100f) + "%";
            MarkSettingsChanged();
        }

        private void RefreshControllerSettingsUI()
        {
            if (controllerStyleValueText != null)
            {
                int style = MenuPreferences.ControllerStyle;
                string label = "< AUTO-DETECT >";
                if (style == 1) label = "< XBOX (A / B / X / Y) >";
                else if (style == 2) label = "< PLAYSTATION (CROSS / CIRCLE) >";
                else if (style == 3) label = "< KEYBOARD & MOUSE >";
                controllerStyleValueText.text = label;
            }

            if (controllerMappingSummaryText != null)
            {
                var active = ControllerGlyphService.ActiveStyle;
                if (active == ControllerPromptStyle.PlayStation)
                {
                    controllerMappingSummaryText.text = "PLAYSTATION DUALSENSE / DUALSHOCK 4 MAPPING PREVIEW";
                }
                else if (active == ControllerPromptStyle.Xbox)
                {
                    controllerMappingSummaryText.text = "XBOX WIRELESS CONTROLLER MAPPING PREVIEW";
                }
                else
                {
                    controllerMappingSummaryText.text = "KEYBOARD & MOUSE: [SPACE] JUMP  [F/LMB] FLARE  [SHIFT] SPRINT  [E] INTERACT  [ESC] PAUSE";
                }

                GlyphAction[] actions = { GlyphAction.Jump, GlyphAction.Flare, GlyphAction.Sprint, GlyphAction.Interact, GlyphAction.Pause };
                for (int i = 0; i < 5; i++)
                {
                    if (controllerMappingIcons[i] != null)
                    {
                        if (active == ControllerPromptStyle.PlayStation || active == ControllerPromptStyle.Xbox)
                        {
                            var sprite = ControllerGlyphService.GetActionSprite(actions[i], active);
                            controllerMappingIcons[i].sprite = sprite;
                            controllerMappingIcons[i].color = sprite != null ? Color.white : Color.clear;
                            controllerMappingIcons[i].transform.parent.gameObject.SetActive(true);
                        }
                        else
                        {
                            controllerMappingIcons[i].transform.parent.gameObject.SetActive(false);
                        }
                    }
                }
            }

            if (deadzoneSlider != null)
            {
                deadzoneSlider.SetValueWithoutNotify(MenuPreferences.StickDeadzone);
                if (deadzoneValueText != null) deadzoneValueText.text = Mathf.RoundToInt(MenuPreferences.StickDeadzone * 100f) + "%";
            }
        }
        private Button SettingsButton(RectTransform parent,string text,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var rect=CreateAnchoredRect(text,parent,size,Vector2.one*.5f,position);

            var plateRect=CreateStretchRect("Card Plate",rect);
            var plateImg=plateRect.gameObject.AddComponent<Image>();
            plateImg.sprite=thinBorderSprite;
            plateImg.type=Image.Type.Sliced;
            plateImg.color=new Color(0.012f,0.020f,0.038f,0.65f);
            plateImg.raycastTarget=true;

            var borderRect=CreateStretchRect("Card Border",rect);
            var borderImg=borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite=thinBorderSprite;
            borderImg.type=Image.Type.Sliced;
            borderImg.color=new Color(0.48f,0.62f,0.82f,0.25f);
            borderImg.raycastTarget=false;

            var button=rect.gameObject.AddComponent<Button>();
            button.targetGraphic=plateImg;
            button.transition=Selectable.Transition.None;

            var label=CreateLabel("Caption",rect,text.ToUpperInvariant(),14.5f,radiantIvory,size,Vector2.one*.5f,Vector2.zero,Vector2.one*.5f);
            label.alignment=TextAlignmentOptions.Center;label.fontStyle=FontStyles.Bold;label.characterSpacing=2.8f;

            AddSettingsFocus(rect,button,label,plateImg,borderImg);

            button.onClick.AddListener(()=>{action();if(Application.isPlaying)SoundEffectManager.Instance?.PlayUIClick();});
            return button;
        }

        private void AddSettingsFocus(RectTransform rect,Selectable control,TMP_Text label,Image plate=null,Image border=null)
        {
            control.transition=Selectable.Transition.None;
            var halo=CreateAnchoredRect("Lantern focus",rect,new Vector2(rect.sizeDelta.x+60f,rect.sizeDelta.y+36f),Vector2.one*.5f,Vector2.zero);
            halo.SetAsFirstSibling();
            var glow=halo.gameObject.AddComponent<Image>();glow.sprite=softRadialSprite;glow.color=Color.clear;glow.raycastTarget=false;
            var focus=rect.gameObject.AddComponent<SettingsLightFocus>();
            focus.Glow=glow;
            focus.Label=label;
            focus.CardPlate=plate;
            focus.BorderFrame=border;
        }
        private void BuildSettingsLanterns(RectTransform parent)
        {
            var ambience=parent.gameObject.AddComponent<SettingsLanternAmbience>();
            ambience.Visibility=settingsGroup;
            ambience.Pivots=new RectTransform[2];ambience.Glows=new Image[2];
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
                var pivot=CreateAnchoredRect("Settings archive lantern "+i,parent,new Vector2(100,100),Vector2.one*.5f,i==0?new Vector2(-430,425):new Vector2(430,425));
                pivot.pivot=new Vector2(.5f,1);pivot.localScale=Vector3.one*(i==0?.9f:.9f);
                ambience.Pivots[i]=pivot;
                for(int c=0;c<4;c++)
                {
                    var chain=CreateAnchoredRect("Chain",pivot,new Vector2(12,26),new Vector2(.5f,1),new Vector2(0,-10-c*20));
                    var image=chain.gameObject.AddComponent<Image>();image.sprite=chainSprite;image.color=new Color(.62f,.72f,.86f,.75f);image.raycastTarget=false;
                }
                var glowRect=CreateAnchoredRect("Archive lamplight",pivot,new Vector2(360,480),new Vector2(.5f,1),new Vector2(0,-215));
                var light=glowRect.gameObject.AddComponent<Image>();light.sprite=softRadialSprite;light.raycastTarget=false;light.color=new Color(1,.68f,.25f,.12f);
                ambience.Glows[i]=light;
                var cage=CreateAnchoredRect("Cage lamp",pivot,new Vector2(76,106),new Vector2(.5f,1),new Vector2(0,-135));
                var lamp=cage.gameObject.AddComponent<Image>();lamp.sprite=lampSprite;lamp.preserveAspect=true;lamp.color=new Color(.88f,.92f,.98f);lamp.raycastTarget=false;
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
            var rect=CreateAnchoredRect(name,parent,new Vector2(width,1.5f),Vector2.one*.5f,pos);
            var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.48f,.62f,.82f,.25f);image.raycastTarget=false;
        }
    }
}
