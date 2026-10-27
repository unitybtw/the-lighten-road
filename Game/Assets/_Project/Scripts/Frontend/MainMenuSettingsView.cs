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
        private static readonly string[] SectionTabNames = { "Audio", "Display", "Controls", "Accessibility" };
        private Image leftBumperImg, rightBumperImg;
        private TMP_Text leftBumperText, rightBumperText;
        private RectTransform settingsFooterBar;
        private Image footerSelectIcon, footerBackIcon, footerResetIcon, footerTabIcon;
        private TMP_Text footerSelectText, footerBackText, footerResetText, footerTabText;
        private Button controllerStyleButton;
        private TMP_Text controllerStyleValueText;
        private Toggle vibrationToggle, adaptiveTriggersToggle, controllerSpeakerToggle, controllerLightbarToggle;
        private TMP_Text controllerSpeakerValueText, speakerTestStatus;
        private Button speakerTestButton;
        private bool speakerTestPending;
        private TMP_Text adaptiveTriggersValueText, controllerLightbarValueText;
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
            var crest = CreateAnchoredRect("Settings Bottom Filigree", surface, new Vector2(340f, 22f), Vector2.one * 0.5f, new Vector2(0f, -215f));
            if (bottomCrestSprite != null)
            {
                var cImg = crest.gameObject.AddComponent<Image>();
                cImg.sprite = bottomCrestSprite;
                cImg.color = new Color(0.86f, 0.92f, 1.0f, 0.60f);
                cImg.raycastTarget = false;
            }
            SettingsRule(surface,"Footer rule",new Vector2(0,-218),720);

            // Hollow Knight Bumper Indicators for switching tabs
            var leftBumperBadge = CreateAnchoredRect("Left Bumper Badge", surface, new Vector2(50f, 32f), Vector2.one * 0.5f, new Vector2(-365f, 182f));
            leftBumperImg = leftBumperBadge.gameObject.AddComponent<Image>();
            leftBumperImg.preserveAspect = true;
            leftBumperImg.raycastTarget = false;
            leftBumperText = CreateLabel("Keycap", leftBumperBadge, "[Q]", 14f, radiantIvory, new Vector2(50f, 32f), Vector2.one * 0.5f, Vector2.zero, Vector2.one * 0.5f);
            leftBumperText.alignment = TextAlignmentOptions.Center;
            leftBumperText.fontStyle = FontStyles.Bold;
            var lBtn = leftBumperBadge.gameObject.AddComponent<Button>();
            lBtn.targetGraphic = leftBumperImg;
            lBtn.onClick.AddListener(() => { SelectSettingsSection((settingsSection - 1 + 4) % 4); if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick(); });

            var rightBumperBadge = CreateAnchoredRect("Right Bumper Badge", surface, new Vector2(50f, 32f), Vector2.one * 0.5f, new Vector2(365f, 182f));
            rightBumperImg = rightBumperBadge.gameObject.AddComponent<Image>();
            rightBumperImg.preserveAspect = true;
            rightBumperImg.raycastTarget = false;
            rightBumperText = CreateLabel("Keycap", rightBumperBadge, "[E]", 14f, radiantIvory, new Vector2(50f, 32f), Vector2.one * 0.5f, Vector2.zero, Vector2.one * 0.5f);
            rightBumperText.alignment = TextAlignmentOptions.Center;
            rightBumperText.fontStyle = FontStyles.Bold;
            var rBtn = rightBumperBadge.gameObject.AddComponent<Button>();
            rBtn.targetGraphic = rightBumperImg;
            rBtn.onClick.AddListener(() => { SelectSettingsSection((settingsSection + 1) % 4); if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick(); });

            var names=SectionTabNames;
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
                settingsTabLabels[i].fontSize = 17f;
                var section=CreateAnchoredRect(names[i]+" settings",surface,new Vector2(720,350),Vector2.one*.5f,new Vector2(0,-22));
                settingsSections[i]=section.gameObject;
                var desc=SettingsText(section,descriptions[i],new Vector2(0,140),13f,moonlightSilver,720);
                desc.alignment=TextAlignmentOptions.Center;
            }
            var audio=(RectTransform)settingsSections[0].transform;
            masterSlider=CreateEtherealSlider(audio,"Master Volume","Overall audio level",100,out masterValueText);
            musicSlider=CreateEtherealSlider(audio,"Ambient Music","Music and atmospheric score",34,out musicValueText);
            effectsSlider=CreateEtherealSlider(audio,"Sound Effects (SFX)","Footsteps, lantern and interactions",-32,out effectsValueText);
            controllerSpeakerToggle=CreateEtherealToggle(audio,"DualSense Speaker","Quiet lantern details over Bluetooth",-98,out controllerSpeakerValueText,value =>
            {
                MenuPreferences.SetControllerSpeaker(value);MarkSettingsChanged();UpdateSwitchVisual(controllerSpeakerToggle);
            });
            var speakerPill=controllerSpeakerToggle.transform.Find("Pill") as RectTransform;
            if(speakerPill!=null)speakerPill.anchoredPosition=new Vector2(-146,0);
            speakerTestButton=SettingsButton(audio,"TEST",new Vector2(278,-98),new Vector2(112,44),TestControllerSpeaker);
            speakerTestButton.gameObject.name="Test DualSense Speaker";
            speakerTestButton.GetComponentInChildren<TMP_Text>().fontSize=14;
            speakerTestStatus=SettingsText(audio,"",new Vector2(0,-153),12.5f,moonlightSilver,700);
            speakerTestStatus.alignment=TextAlignmentOptions.Center;
            masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            musicSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            effectsSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            var audioHint=SettingsText(audio,"Adjust audio levels for music and gameplay effects.",new Vector2(0,-156),14f,moonlightSilver,720);
            audioHint.alignment=TextAlignmentOptions.Center;audioHint.gameObject.SetActive(false);

            var display=(RectTransform)settingsSections[1].transform;
            fullscreenToggle=CreateEtherealToggle(display,"Fullscreen","Use the whole display",84,out fullscreenToggleValueText,OnFullscreenToggled);
            vsyncToggle=CreateEtherealToggle(display,"V-Sync","Match the display refresh rate",-6,out vsyncToggleValueText,OnVSyncToggled);
            BuildFrameLimit(display);

            // Controls settings section (index 2)
            var controls=(RectTransform)settingsSections[2].transform;
            var styleRow=CreateAnchoredRect("Prompt Style Row",controls,new Vector2(680,54),Vector2.one*0.5f,new Vector2(0,100));
            var styleTitle=CreateLabel("Title",styleRow,"PROMPT GLYPHS",20f,radiantIvory,new Vector2(300,24),new Vector2(0,0.5f),new Vector2(8,10),new Vector2(0,0.5f));
            styleTitle.fontStyle=FontStyles.Bold; styleTitle.characterSpacing=2.5f;
            var styleSub=CreateLabel("Subtitle",styleRow,"Button symbols shown throughout the game",14f,moonlightSilver,new Vector2(340,20),new Vector2(0,0.5f),new Vector2(8,-12),new Vector2(0,0.5f));
            controllerStyleButton=SettingsButton(styleRow,"< AUTO-DETECT >",new Vector2(175,0),new Vector2(250,38),CycleControllerStyle);
            controllerStyleValueText=controllerStyleButton.GetComponentInChildren<TMP_Text>();
            controllerStyleValueText.fontSize=14f;

            vibrationToggle=CreateEtherealToggle(controls,"Vibration (Rumble)","Contextual vibration and DualSense textures",34,out vibrationToggleValueText,OnVibrationToggled);

            adaptiveTriggersToggle=CreateEtherealToggle(controls,"DualSense R2","Two-stage flint ignition lock & release (Bluetooth)",-32,out adaptiveTriggersValueText,value=>
            {
                MenuPreferences.SetAdaptiveTriggers(value);MarkSettingsChanged();UpdateSwitchVisual(adaptiveTriggersToggle);
                ControllerGlyphService.UpdateAdaptiveTriggers(true);
                if(value && Application.isPlaying)ControllerGlyphService.PlayHaptic(HapticCue.Preview);
            });

            controllerLightbarToggle=CreateEtherealToggle(controls,"DualSense Lightbar","Dynamic lantern and flare glow",-98,out controllerLightbarValueText,value=>
            {
                MenuPreferences.SetControllerLightbar(value);MarkSettingsChanged();UpdateSwitchVisual(controllerLightbarToggle);
                if(value && Application.isPlaying)TheLightenRoad.UI.ControllerLightbar.Pulse(TheLightenRoad.UI.ControllerLightbar.AmberLantern, 0.8f, 2f);
            });

            deadzoneSlider=CreateEtherealSlider(controls,"Stick Deadzone","Analog stick inner drift threshold",-164,out deadzoneValueText);
            ((MenuSlider)deadzoneSlider).NavigationStep=.01f;
            deadzoneSlider.minValue=0.05f; deadzoneSlider.maxValue=0.40f;
            deadzoneSlider.onValueChanged.AddListener(OnDeadzoneSliderChanged);

            var guideCard=CreateAnchoredRect("Controller Mapping Preview",controls,new Vector2(700,80),Vector2.one*0.5f,new Vector2(0,-118));
            var guideBg=guideCard.gameObject.AddComponent<Image>();
            guideBg.sprite=softRadialSprite; guideBg.color=Color.clear; guideBg.raycastTarget=false;
            guideCard.gameObject.SetActive(false);
            controllerMappingSummaryText=SettingsText(guideCard,"",new Vector2(0,22),14f,amberGold,680);
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

                var lbl = CreateLabel("Label", slot, actionNames[i], 14f, radiantIvory, new Vector2(82, 24), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(0, 0.5f));
                lbl.fontStyle = FontStyles.Bold;
                lbl.characterSpacing = 1f;
            }

            var accessibility=(RectTransform)settingsSections[3].transform;
            motionToggle=CreateEtherealToggle(accessibility,"Reduced motion","Reduce parallax, sway and HUD motion",84,out motionToggleValueText,OnReducedMotionToggled);
            var comfortCard=CreateAnchoredRect("Motion Comfort Card",accessibility,new Vector2(680,104),Vector2.one*.5f,new Vector2(0,-36));
            var titleComfort=SettingsText(comfortCard,"MOTION COMFORT",new Vector2(0,22),15f,radiantIvory,680);
            titleComfort.alignment=TextAlignmentOptions.Center;titleComfort.fontStyle=FontStyles.Bold;titleComfort.characterSpacing=2.5f;
            var note=SettingsText(comfortCard,"Reduces camera sway, parallax and UI animations. Gameplay, puzzle timing and controls remain unchanged.",new Vector2(0,-14),14f,moonlightSilver,660);
            note.alignment=TextAlignmentOptions.Center;
            note.rectTransform.sizeDelta=new Vector2(660,54);
            SettingsRule(comfortCard,"Comfort Bottom Rule",new Vector2(0,-48),540);

            settingsSaveStatus=SettingsText(surface,"Preferences are saved on this device",new Vector2(0,-198),14f,moonlightSilver,720);
            settingsSaveStatus.alignment=TextAlignmentOptions.Center;
            resetSettingsButton=SettingsButton(surface,"Restore defaults",new Vector2(-155,-236),new Vector2(250,40),RequestSettingsReset);
            settingsDoneButton=SettingsButton(surface,"Back",new Vector2(155,-236),new Vector2(250,40),ShowHome);
            BuildSettingsReset(surface);
            BuildSettingsFooter(surface);
            BuildOptionsIndex(surface);
            ApplySettingsReadability(surface);
            SyncSettingsUI();
            OpenOptionsIndex();
        }

        private void BuildSettingsFooter(RectTransform parent)
        {
            settingsFooterBar = CreateAnchoredRect("Settings HollowKnight Footer", parent, new Vector2(880f, 36f), Vector2.one * 0.5f, new Vector2(0f, -276f));
            var fBg = settingsFooterBar.gameObject.AddComponent<Image>();
            fBg.sprite = softRadialSprite;
            fBg.color = new Color(0.008f, 0.014f, 0.028f, 0.70f);
            fBg.raycastTarget = false;

            float[] xPos = { -320f, -110f, 110f, 320f };

            // Slot 0: SELECT
            var s0 = CreateAnchoredRect("Slot_Select", settingsFooterBar, new Vector2(180f, 32f), Vector2.one * 0.5f, new Vector2(xPos[0], 0f));
            var icon0 = CreateAnchoredRect("Icon", s0, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            footerSelectIcon = icon0.gameObject.AddComponent<Image>();
            footerSelectIcon.preserveAspect = true;
            footerSelectIcon.raycastTarget = false;
            footerSelectText = CreateLabel("Label", s0, "SELECT", 14f, radiantIvory, new Vector2(130f, 24f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(0f, 0.5f));
            footerSelectText.fontStyle = FontStyles.Bold;
            footerSelectText.characterSpacing = 1.2f;

            // Slot 1: BACK
            var s1 = CreateAnchoredRect("Slot_Back", settingsFooterBar, new Vector2(160f, 32f), Vector2.one * 0.5f, new Vector2(xPos[1], 0f));
            var icon1 = CreateAnchoredRect("Icon", s1, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            footerBackIcon = icon1.gameObject.AddComponent<Image>();
            footerBackIcon.preserveAspect = true;
            footerBackIcon.raycastTarget = false;
            footerBackText = CreateLabel("Label", s1, "BACK", 14f, radiantIvory, new Vector2(110f, 24f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(0f, 0.5f));
            footerBackText.fontStyle = FontStyles.Bold;
            footerBackText.characterSpacing = 1.2f;
            var bBtn = s1.gameObject.AddComponent<Button>();
            bBtn.onClick.AddListener(BackFromSettings);

            // Slot 2: RESTORE DEFAULTS
            var s2 = CreateAnchoredRect("Slot_Reset", settingsFooterBar, new Vector2(200f, 32f), Vector2.one * 0.5f, new Vector2(xPos[2], 0f));
            var icon2 = CreateAnchoredRect("Icon", s2, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            footerResetIcon = icon2.gameObject.AddComponent<Image>();
            footerResetIcon.preserveAspect = true;
            footerResetIcon.raycastTarget = false;
            footerResetText = CreateLabel("Label", s2, "RESTORE DEFAULTS", 14f, radiantIvory, new Vector2(150f, 24f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(0f, 0.5f));
            footerResetText.fontStyle = FontStyles.Bold;
            footerResetText.characterSpacing = 1.2f;
            var rBtn = s2.gameObject.AddComponent<Button>();
            rBtn.onClick.AddListener(RequestSettingsReset);

            // Slot 3: SWITCH TAB
            var s3 = CreateAnchoredRect("Slot_Tab", settingsFooterBar, new Vector2(180f, 32f), Vector2.one * 0.5f, new Vector2(xPos[3], 0f));
            var icon3 = CreateAnchoredRect("Icon", s3, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            footerTabIcon = icon3.gameObject.AddComponent<Image>();
            footerTabIcon.preserveAspect = true;
            footerTabIcon.raycastTarget = false;
            footerTabText = CreateLabel("Label", s3, "SWITCH TAB", 14f, radiantIvory, new Vector2(130f, 24f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(0f, 0.5f));
            footerTabText.fontStyle = FontStyles.Bold;
            footerTabText.characterSpacing = 1.2f;

            RefreshSettingsFooterPrompts();
        }

        public void RefreshSettingsFooterPrompts()
        {
            var style = ControllerGlyphService.ActiveStyle;
            bool isPad = ControllerGlyphService.IsGamepadActive;

            if (leftBumperImg != null)
            {
                if (isPad)
                {
                    leftBumperImg.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.PrevTab, style);
                    leftBumperImg.color = Color.white;
                    if (leftBumperText != null) leftBumperText.text = "";
                }
                else
                {
                    leftBumperImg.sprite = null;
                    leftBumperImg.color = Color.clear;
                    if (leftBumperText != null) leftBumperText.text = "[ Q ]";
                }
            }

            if (rightBumperImg != null)
            {
                if (isPad)
                {
                    rightBumperImg.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.NextTab, style);
                    rightBumperImg.color = Color.white;
                    if (rightBumperText != null) rightBumperText.text = "";
                }
                else
                {
                    rightBumperImg.sprite = null;
                    rightBumperImg.color = Color.clear;
                    if (rightBumperText != null) rightBumperText.text = "[ E ]";
                }
            }

            if (footerSelectIcon != null)
            {
                if (isPad)
                {
                    footerSelectIcon.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.Confirm, style);
                    footerSelectIcon.color = Color.white;
                    if (footerSelectText != null) footerSelectText.text = "SELECT";
                }
                else
                {
                    footerSelectIcon.sprite = null;
                    footerSelectIcon.color = Color.clear;
                    if (footerSelectText != null) footerSelectText.text = "[ENTER] SELECT";
                }
            }

            if (footerBackIcon != null)
            {
                if (isPad)
                {
                    footerBackIcon.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.Cancel, style);
                    footerBackIcon.color = Color.white;
                    if (footerBackText != null) footerBackText.text = "BACK";
                }
                else
                {
                    footerBackIcon.sprite = null;
                    footerBackIcon.color = Color.clear;
                    if (footerBackText != null) footerBackText.text = "[ESC] BACK";
                }
            }

            if (footerResetIcon != null)
            {
                if (isPad)
                {
                    footerResetIcon.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.Interact, style);
                    footerResetIcon.color = Color.white;
                    if (footerResetText != null) footerResetText.text = "RESTORE DEFAULTS";
                }
                else
                {
                    footerResetIcon.sprite = null;
                    footerResetIcon.color = Color.clear;
                    if (footerResetText != null) footerResetText.text = "[R] DEFAULTS";
                }
            }

            if (footerTabIcon != null)
            {
                if (isPad)
                {
                    footerTabIcon.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.NextTab, style);
                    footerTabIcon.color = Color.white;
                    if (footerTabText != null) footerTabText.text = "SWITCH TAB";
                }
                else
                {
                    footerTabIcon.sprite = null;
                    footerTabIcon.color = Color.clear;
                    if (footerTabText != null) footerTabText.text = "[Q / E] TABS";
                }
            }
            UpdateOptionsFooterLayout();
        }

        public void SelectSettingsSection(int section)
        {
            settingsSection = Mathf.Clamp(section, 0, 3);
            ShowOptionsPage(settingsSection);
            if (settingsResetPanel != null) settingsResetPanel.SetActive(false);
            for (int i = 0; i < settingsSections.Length; i++)
            {
                bool active = (i == settingsSection);
                if (settingsSections[i] != null) settingsSections[i].SetActive(active);
                if (settingsTabs[i] != null) settingsTabs[i].interactable = true;
                if (settingsTabLabels[i] != null)
                {
                    settingsTabLabels[i].text = new[]{"Audio","Video","Controller","Accessibility"}[i];
                    settingsTabLabels[i].color = active ? amberGold : radiantIvory;
                }
                var focus = settingsTabs[i] != null ? settingsTabs[i].GetComponent<SettingsLightFocus>() : null;
                if (focus != null) focus.IsCurrent = active;
            }
            if (settingsSection == 2) RefreshControllerSettingsUI();
            if (resetSettingsButton != null) resetSettingsButton.interactable = true;
            if (settingsDoneButton != null) settingsDoneButton.interactable = true;
            ConfigureSettingsNavigation();
            RefreshSettingsFooterPrompts();

            Selectable[] firstControls = { masterSlider, fullscreenToggle, controllerStyleButton, motionToggle };
            if (EventSystem.current != null && firstControls[settingsSection] != null)
            {
                EventSystem.current.SetSelectedGameObject(firstControls[settingsSection].gameObject);
            }
        }

        private void BuildFrameLimit(RectTransform parent)
        {
            var row=CreateAnchoredRect("Target Frame Rate",parent,new Vector2(680,70),Vector2.one*.5f,new Vector2(0,-96));

            var titleTxt=CreateLabel("Title",row,"FRAME LIMIT",16.5f,radiantIvory,new Vector2(300,24),new Vector2(0,0.5f),new Vector2(8,11),new Vector2(0,0.5f));
            titleTxt.fontStyle=FontStyles.Bold;titleTxt.characterSpacing=2.5f;
            frameRateHint=CreateLabel("Subtitle",row,"",14f,moonlightSilver,new Vector2(340,22),new Vector2(0,0.5f),new Vector2(8,-12),new Vector2(0,0.5f));

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
                ?"Turn off V-Sync to set a limit."
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
            var note=SettingsText(panel,"Restore audio, display and accessibility options.\nYour chapter progress will be kept.",new Vector2(0,18),14.5f,moonlightSilver,660);
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
        public void TestControllerSpeaker()
        {
            speakerTestPending=DualSenseSpeaker.TryPlay(SpeakerCue.Preview,true);
            if(speakerTestStatus!=null)speakerTestStatus.text=DualSenseSpeaker.LastStatus;
        }
        private void TickSettingsSave(float dt)
        {
            if(speakerTestPending)
            {
                if(speakerTestStatus!=null)speakerTestStatus.text=DualSenseSpeaker.LastStatus;
                if(!DualSenseSpeaker.IsPlaying)speakerTestPending=false;
            }
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
            if(IsOptionsIndex){ConfigureOptionsIndexNavigation();return;}
            Selectable[] first = { masterSlider, fullscreenToggle, controllerStyleButton, motionToggle };
            Selectable lastInActiveSection = null;

            for (int i = 0; i < 4; i++)
            {
                if (settingsTabs[i] == null) continue;
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = settingsTabs[(i - 1 + 4) % 4],
                    selectOnRight = settingsTabs[(i + 1) % 4],
                    selectOnDown = first[i],
                    selectOnUp = settingsDoneButton
                };
                settingsTabs[i].navigation = nav;
            }

            // Section 0: Audio
            if (masterSlider != null)
            {
                var nav = masterSlider.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = settingsTabs[0];
                nav.selectOnDown = musicSlider;
                masterSlider.navigation = nav;
            }
            if (musicSlider != null)
            {
                var nav = musicSlider.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = masterSlider;
                nav.selectOnDown = effectsSlider;
                musicSlider.navigation = nav;
            }
            if (effectsSlider != null)
            {
                var nav = effectsSlider.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = musicSlider;
                nav.selectOnDown = controllerSpeakerToggle;
                effectsSlider.navigation = nav;
                if (settingsSection == 0) lastInActiveSection = speakerTestButton;
            }

            if (controllerSpeakerToggle != null)
            {
                var nav = controllerSpeakerToggle.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = effectsSlider;
                nav.selectOnDown = speakerTestButton;
                nav.selectOnRight = speakerTestButton;
                controllerSpeakerToggle.navigation = nav;
            }

            if(speakerTestButton!=null)
            {
                var nav=speakerTestButton.navigation;nav.mode=Navigation.Mode.Explicit;
                nav.selectOnUp=controllerSpeakerToggle;nav.selectOnLeft=controllerSpeakerToggle;
                nav.selectOnDown=resetSettingsButton;speakerTestButton.navigation=nav;
            }

            // Section 1: Display
            if (fullscreenToggle != null)
            {
                var nav = fullscreenToggle.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = settingsTabs[1];
                nav.selectOnDown = vsyncToggle;
                fullscreenToggle.navigation = nav;
            }
            if (vsyncToggle != null)
            {
                var nav = vsyncToggle.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = fullscreenToggle;
                nav.selectOnDown = frameRateOptions[0] != null ? frameRateOptions[0] : resetSettingsButton;
                vsyncToggle.navigation = nav;
            }
            for (int i = 0; i < 3; i++)
            {
                if (frameRateOptions[i] != null)
                {
                    var nav = frameRateOptions[i].navigation;
                    nav.mode = Navigation.Mode.Explicit;
                    nav.selectOnUp = vsyncToggle;
                    nav.selectOnDown = resetSettingsButton;
                    nav.selectOnLeft = i > 0 ? frameRateOptions[i - 1] : null;
                    nav.selectOnRight = i < 2 ? frameRateOptions[i + 1] : null;
                    frameRateOptions[i].navigation = nav;
                }
            }
            if (settingsSection == 1) lastInActiveSection = frameRateOptions[0] != null ? frameRateOptions[0] : vsyncToggle;

            // Section 2: Controls
            if (controllerStyleButton != null)
            {
                var nav = controllerStyleButton.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = settingsTabs[2];
                nav.selectOnDown = vibrationToggle;
                controllerStyleButton.navigation = nav;
            }
            if (vibrationToggle != null)
            {
                var nav = vibrationToggle.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = controllerStyleButton;
                nav.selectOnDown = adaptiveTriggersToggle;
                vibrationToggle.navigation = nav;
            }
            if(adaptiveTriggersToggle!=null)adaptiveTriggersToggle.navigation=new Navigation
            {
                mode=Navigation.Mode.Explicit,selectOnUp=vibrationToggle,selectOnDown=controllerLightbarToggle
            };
            if(controllerLightbarToggle!=null)controllerLightbarToggle.navigation=new Navigation
            {
                mode=Navigation.Mode.Explicit,selectOnUp=adaptiveTriggersToggle,selectOnDown=deadzoneSlider
            };
            if (deadzoneSlider != null)
            {
                var nav = deadzoneSlider.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = controllerLightbarToggle;
                nav.selectOnDown = resetSettingsButton;
                deadzoneSlider.navigation = nav;
                if (settingsSection == 2) lastInActiveSection = deadzoneSlider;
            }

            // Section 3: Accessibility
            if (motionToggle != null)
            {
                var nav = motionToggle.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = settingsTabs[3];
                nav.selectOnDown = resetSettingsButton;
                motionToggle.navigation = nav;
                if (settingsSection == 3) lastInActiveSection = motionToggle;
            }

            // Bottom Buttons
            if (resetSettingsButton != null)
            {
                var nav = resetSettingsButton.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = lastInActiveSection != null ? lastInActiveSection : settingsTabs[settingsSection];
                nav.selectOnRight = settingsDoneButton;
                nav.selectOnDown = settingsTabs[settingsSection];
                resetSettingsButton.navigation = nav;
            }
            if (settingsDoneButton != null)
            {
                var nav = settingsDoneButton.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = lastInActiveSection != null ? lastInActiveSection : settingsTabs[settingsSection];
                nav.selectOnLeft = resetSettingsButton;
                nav.selectOnDown = settingsTabs[settingsSection];
                settingsDoneButton.navigation = nav;
            }
            int page=Mathf.Clamp(settingsSection,0,3);
            if(first[page]!=null){var n=first[page].navigation;n.selectOnUp=settingsDoneButton;first[page].navigation=n;}
            if(vsyncToggle!=null){var n=vsyncToggle.navigation;n.selectOnDown=MenuPreferences.VSync?resetSettingsButton:frameRateOptions[0];vsyncToggle.navigation=n;}
            var rn=resetSettingsButton.navigation;rn.selectOnDown=settingsDoneButton;rn.selectOnLeft=null;rn.selectOnRight=settingsDoneButton;resetSettingsButton.navigation=rn;
            var bn=settingsDoneButton.navigation;bn.selectOnDown=first[page];bn.selectOnLeft=resetSettingsButton;bn.selectOnRight=null;settingsDoneButton.navigation=bn;
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
                ControllerGlyphService.PlayHaptic(HapticCue.Preview);
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
                string label = "Automatic";
                if (style == 1) label = "Xbox";
                else if (style == 2) label = "PlayStation";
                else if (style == 3) label = "Keyboard & mouse";
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
            plateImg.color=Color.clear;
            plateImg.raycastTarget=true;

            var borderRect=CreateStretchRect("Card Border",rect);
            var borderImg=borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite=thinBorderSprite;
            borderImg.type=Image.Type.Sliced;
            borderImg.color=Color.clear;
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

            if (buttonPointerSprite != null)
            {
                float halfWidth = rect.sizeDelta.x * 0.5f;
                float offset = halfWidth + 24f;
                var leftPointer = CreateAnchoredRect("Left Pointer", rect, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(-offset, 0f));
                var lImg = leftPointer.gameObject.AddComponent<Image>();
                lImg.sprite = buttonPointerSprite;
                lImg.color = Color.clear;
                lImg.raycastTarget = false;

                var rightPointer = CreateAnchoredRect("Right Pointer", rect, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(offset, 0f));
                rightPointer.localScale = new Vector3(-1f, 1f, 1f);
                var rImg = rightPointer.gameObject.AddComponent<Image>();
                rImg.sprite = buttonPointerSprite;
                rImg.color = Color.clear;
                rImg.raycastTarget = false;

                focus.LeftPointer = leftPointer;
                focus.RightPointer = rightPointer;
                focus.PointerOffset = offset;
            }
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
