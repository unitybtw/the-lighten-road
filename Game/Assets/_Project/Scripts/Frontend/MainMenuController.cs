using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>The animated front door. Menu animation uses unscaled time.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string DefaultGameplayScene = "Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity";
        [SerializeField] private Texture2D backdrop;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string gameplayScene = DefaultGameplayScene;
        private readonly Color ivory = new Color32(222, 212, 184, 255);
        private readonly Color quiet = new Color32(152, 151, 135, 255);
        private readonly Color gold = new Color32(173, 131, 73, 255);
        private Canvas canvas;
        private RectTransform background, home, settings, credits, quit, lightHalo;
        private CanvasGroup homeGroup, settingsGroup, creditsGroup, quitGroup;
        private Image curtain, halo;
        private TMP_Text hint, loadingText;
        private Button playButton, settingsFirst, creditsFirst, quitFirst;
        private Slider masterSlider, musicSlider, effectsSlider;
        private Toggle motionToggle, fullscreenToggle;
        private readonly List<RectTransform> ashes = new List<RectTransform>();
        private readonly List<Image> ashImages = new List<Image>();
        private float age, viewAge;
        private string view = "home";
        private bool introDone, loading, quitting;
        private Vector2 pointer;
        private Texture2D glowTexture;
        private RectTransform safeFrame;
        private Rect lastSafeArea;
        private int lastWidth, lastHeight;
        private float leftMargin = 160f;
        private Sprite glowSprite;

        public Canvas Canvas => canvas;
        public string CurrentView => view;
        public bool IntroComplete => introDone;
        public bool IsLoading => loading;
        public string GameplayScene => gameplayScene;

        private void Awake()
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>("UI/MenuType");
            Build();
        }

        private void Start()
        {
            MenuPreferences.EnsureActive();
            MenuPreferences.ApplySavedDisplay();
            MenuPreferences.Apply();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;
            if (MenuPreferences.ReducedMotion) SkipIntro();
        }

        private void OnDestroy()
        {
            MenuPreferences.Save();
            if (glowSprite != null) Destroy(glowSprite);
            if (glowTexture != null) Destroy(glowTexture);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            viewAge += dt;
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                || Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            bool confirm = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame
                || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                || Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (!introDone && (confirm || back)) SkipIntro();
            else if (back && !loading)
            {
                if (view == "home") ShowQuit();
                else ShowHome();
            }
            if (!introDone && age >= 3.7f) SkipIntro();
            float reveal = MenuPreferences.ReducedMotion ? 1f : Ease(Mathf.InverseLerp(1.25f, 3.25f, age));
            if (!loading && !quitting) curtain.color = new Color(0.015f, 0.018f, 0.016f, 1f - Ease(Mathf.InverseLerp(0.15f, 2.5f, age)));
            float switchAmount = MenuPreferences.ReducedMotion ? 1f : Ease(Mathf.Clamp01(viewAge / 0.28f));
            UpdateView(homeGroup, view == "home", reveal, switchAmount);
            UpdateView(settingsGroup, view == "settings", reveal, switchAmount);
            UpdateView(creditsGroup, view == "credits", reveal, switchAmount);
            UpdateView(quitGroup, view == "quit", reveal, switchAmount);
            hint.gameObject.SetActive(!introDone);
            AnimateBackdrop(dt);
            UpdateSafeFrame();
        }

        private void UpdateView(CanvasGroup group, bool active, float reveal, float progress)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, active ? reveal : 0f, Time.unscaledDeltaTime * 5f);
            group.interactable = active && introDone && !loading && !quitting;
            group.blocksRaycasts = active && !loading && !quitting;
            var rect = (RectTransform)group.transform;
            rect.anchoredPosition = new Vector2(leftMargin + (MenuPreferences.ReducedMotion ? 0f : -14f * (1f - progress)), -7f * (1f - reveal));
        }

        public void SkipIntro()
        {
            age = Mathf.Max(age, 3.7f);
            introDone = true;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        private void SetView(string next, Selectable first)
        {
            if (loading || quitting) return;
            MenuPreferences.Save();
            view = next;
            viewAge = 0f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void ShowHome() => SetView("home", playButton);
        public void ShowSettings() { SyncSettings(); SetView("settings", masterSlider); }
        public void ShowCredits() => SetView("credits", creditsFirst);
        public void ShowQuit() => SetView("quit", quitFirst);

        public void Play()
        {
            if (loading || quitting) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                loadingText.text = "The archive is unavailable. Please check the scene configuration.";
                loadingText.gameObject.SetActive(true);
                return;
            }
            StartCoroutine(EnterGame());
        }

        private IEnumerator EnterGame()
        {
            loading = true;
            MenuPreferences.Save();
            loadingText.text = "ENTERING THE ARCHIVE";
            loadingText.gameObject.SetActive(true);
            var operation = SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
            if (operation == null) { loading = false; loadingText.text = "Unable to open the archive."; yield break; }
            operation.allowSceneActivation = false;
            float t = 0f;
            while (t < 0.85f || operation.progress < 0.9f)
            {
                t += Time.unscaledDeltaTime;
                curtain.color = new Color(0.015f, 0.018f, 0.016f, Mathf.Clamp01(t / 0.75f));
                yield return null;
            }
            operation.allowSceneActivation = true;
        }

        public void ConfirmQuit()
        {
            if (loading || quitting) return;
            StartCoroutine(ExitGame());
        }

        private IEnumerator ExitGame()
        {
            quitting = true;
            MenuPreferences.Save();
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0.015f, 0.018f, 0.016f, t / 0.45f);
                yield return null;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SyncSettings()
        {
            masterSlider.SetValueWithoutNotify(MenuPreferences.Master);
            musicSlider.SetValueWithoutNotify(MenuPreferences.Music);
            effectsSlider.SetValueWithoutNotify(MenuPreferences.Effects);
            motionToggle.SetIsOnWithoutNotify(MenuPreferences.ReducedMotion);
            fullscreenToggle.SetIsOnWithoutNotify(Application.isEditor
                ? PlayerPrefs.GetInt("TLR.Menu.Fullscreen", Screen.fullScreen ? 1 : 0) == 1 : Screen.fullScreen);
            foreach (var slider in new[] { masterSlider, musicSlider, effectsSlider }) UpdatePercent(slider);
            UpdateToggleCaption(motionToggle); UpdateToggleCaption(fullscreenToggle);
        }

        private void AudioChanged(float value)
        {
            MenuPreferences.SetAudio(masterSlider.value, musicSlider.value, effectsSlider.value);
            foreach (var slider in new[] { masterSlider, musicSlider, effectsSlider }) UpdatePercent(slider);
        }

        private void UpdatePercent(Slider slider) => slider.transform.Find("Value").GetComponent<TMP_Text>().text = Mathf.RoundToInt(slider.value * 100f) + "%";
        private void UpdateToggleCaption(Toggle toggle) => toggle.transform.Find("Value").GetComponent<TMP_Text>().text = toggle.isOn ? "ON" : "OFF";

        private void AnimateBackdrop(float dt)
        {
            bool still = MenuPreferences.ReducedMotion;
            Vector2 target = Mouse.current != null && Screen.width > 0 && Screen.height > 0
                ? new Vector2(Mouse.current.position.ReadValue().x / Screen.width - 0.5f, Mouse.current.position.ReadValue().y / Screen.height - 0.5f) : Vector2.zero;
            target = Vector2.ClampMagnitude(target, 0.7f);
            pointer = Vector2.Lerp(pointer, target, 1f - Mathf.Exp(-dt * 2f));
            float intro = still ? 1f : Ease(Mathf.Clamp01(age / 4.2f));
            background.localScale = Vector3.one * Mathf.Lerp(1.14f, 1.055f, intro);
            background.anchoredPosition = still ? Vector2.zero : new Vector2(-pointer.x * 14f + Mathf.Sin(age * 0.09f) * 5f, -pointer.y * 8f);
            float pulse = still ? 0.8f : 0.78f + Mathf.Sin(age * 1.9f) * 0.06f + Mathf.Sin(age * 4.1f) * 0.025f;
            halo.color = new Color(0.83f, 0.43f, 0.12f, pulse * 0.13f * Ease(Mathf.InverseLerp(0.3f, 2f, age)));
            lightHalo.localScale = Vector3.one * (still ? 1f : 1f + Mathf.Sin(age * 0.6f) * 0.025f);
            for (int i = 0; i < ashes.Count; i++)
            {
                float seed = i * 0.618034f;
                float x = Mathf.Repeat(seed, 1f);
                float y = Mathf.Repeat(i * 0.337f + (still ? 0f : age * (0.012f + i % 4 * 0.002f)), 1f);
                ashes[i].anchorMin = ashes[i].anchorMax = new Vector2(x, y);
                ashes[i].anchoredPosition = still ? Vector2.zero : new Vector2(Mathf.Sin(age * 0.3f + i) * 15f, 0f);
                ashImages[i].color = new Color(0.72f, 0.58f, 0.36f, still ? 0.08f : Mathf.Sin(y * Mathf.PI) * (0.08f + i % 3 * 0.04f));
            }
        }

        private void UpdateSafeFrame()
        {
            if (lastWidth == Screen.width && lastHeight == Screen.height && lastSafeArea == Screen.safeArea) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafeArea = Screen.safeArea;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            safeFrame.anchorMin = new Vector2(lastSafeArea.xMin / lastWidth, lastSafeArea.yMin / lastHeight);
            safeFrame.anchorMax = new Vector2(lastSafeArea.xMax / lastWidth, lastSafeArea.yMax / lastHeight);
            float width = ((RectTransform)canvas.transform).rect.width;
            float left = Mathf.Max(90f, width * 0.083f);
            leftMargin = left;
            foreach (var panel in new[] { home, settings, credits, quit })
            {
                panel.offsetMin = new Vector2(left, 0f);
                panel.offsetMax = new Vector2(left + 720f, 0f);
            }
        }

        private void Build()
        {
            var root = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var rootRect = (RectTransform)root.transform;
            Image("Night", rootRect, new Color32(9, 12, 11, 255));
            background = Stretch("Archive painting", rootRect);
            background.pivot = Vector2.one * 0.5f;
            var painting = background.gameObject.AddComponent<RawImage>();
            painting.texture = backdrop; painting.color = Color.white; painting.raycastTarget = false;
            if (backdrop != null)
            {
                var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = (float)backdrop.width / backdrop.height;
            }
            glowTexture = CreateGlow();
            lightHalo = Rect("Lantern glow", background, new Vector2(600, 500), new Vector2(0.76f, 0.45f), Vector2.zero);
            lightHalo.pivot = Vector2.one * 0.5f;
            var glow = lightHalo.gameObject.AddComponent<RawImage>(); glow.texture = glowTexture; glow.raycastTarget = false;
            // Image uses the same procedural radial alpha as the lantern's soft spill.
            halo = Image("Warm spill", lightHalo, Color.clear);
            glowSprite = Sprite.Create(glowTexture, new Rect(0,0,glowTexture.width,glowTexture.height), Vector2.one * 0.5f, 100);
            halo.sprite = glowSprite;
            glow.enabled = false;
            for (int i = 0; i < 34; i++)
            {
                var ash = Rect("Ash " + i.ToString("00"), rootRect, new Vector2(i % 3 == 0 ? 3 : 2, i % 4 == 0 ? 5 : 2), Vector2.zero, Vector2.zero);
                var image = ash.gameObject.AddComponent<Image>(); image.raycastTarget = false;
                ashes.Add(ash); ashImages.Add(image);
            }
            var shade = Image("Reading veil", rootRect, new Color(0.015f,0.02f,0.018f,0.24f));
            shade.raycastTarget = false;
            safeFrame = Stretch("Safe area", rootRect);
            home = Panel("Home", safeFrame, out homeGroup);
            settings = Panel("Settings", safeFrame, out settingsGroup);
            credits = Panel("Credits", safeFrame, out creditsGroup);
            quit = Panel("Quit confirmation", safeFrame, out quitGroup);
            BuildHome(); BuildSettings(); BuildCredits(); BuildQuit();
            settingsGroup.alpha = creditsGroup.alpha = quitGroup.alpha = homeGroup.alpha = 0f;
            var footer = Label("Footer", safeFrame, "THE LIGHTEN ROAD  /  2026", 12, quiet, new Vector2(700,30), new Vector2(0,0), new Vector2(160,44));
            footer.characterSpacing = 4;
            hint = Label("Skip hint", safeFrame, "PRESS ENTER OR CLICK TO SKIP", 11, quiet, new Vector2(600,30), new Vector2(1,0), new Vector2(-650,44));
            hint.alignment = TextAlignmentOptions.Right; hint.characterSpacing = 3;
            curtain = Image("Cinematic fade", rootRect, new Color32(4,5,4,255)); curtain.raycastTarget = false;
            loadingText = Label("Loading", rootRect, "ENTERING THE ARCHIVE", 16, ivory, new Vector2(1000,50), new Vector2(0.5f,0.12f), new Vector2(-500,0));
            loadingText.alignment = TextAlignmentOptions.Center; loadingText.characterSpacing = 6; loadingText.gameObject.SetActive(false);
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Menu input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private void BuildHome()
        {
            var eyebrow = Label("Eyebrow", home, "SOME LIGHTS REFUSE TO DIE", 12, gold, new Vector2(620,30), new Vector2(0,0.5f), new Vector2(0,299)); eyebrow.characterSpacing=6;
            var the = Label("The",home,"THE",21,ivory,new Vector2(600,40),new Vector2(0,0.5f),new Vector2(0,228));the.characterSpacing=14;
            var title = Label("Lighten",home,"LIGHTEN",86,ivory,new Vector2(680,110),new Vector2(0,0.5f),new Vector2(-5,114));title.characterSpacing=9;
            var road = Label("Road",home,"ROAD",86,ivory,new Vector2(680,110),new Vector2(0,0.5f),new Vector2(-5,23));road.characterSpacing=18;
            var rule=Rect("Title rule",home,new Vector2(64,1),new Vector2(0,0.5f),new Vector2(0,-10));rule.gameObject.AddComponent<Image>().color=gold;
            playButton = MenuButton(home,"Play","01",-89,Play);
            MenuButton(home,"Settings","02",-153,ShowSettings);
            MenuButton(home,"Credits","03",-217,ShowCredits);
            MenuButton(home,"Quit","04",-281,ShowQuit);
        }

        private void BuildSettings()
        {
            Heading(settings,"Settings","MAKE YOURSELF AT HOME IN THE DARK");
            masterSlider = AudioSlider(settings,"Master volume",150);
            musicSlider = AudioSlider(settings,"Music",50);
            effectsSlider = AudioSlider(settings,"Sound effects",-50);
            masterSlider.onValueChanged.AddListener(AudioChanged); musicSlider.onValueChanged.AddListener(AudioChanged); effectsSlider.onValueChanged.AddListener(AudioChanged);
            fullscreenToggle=Switch(settings,"Fullscreen",-155,value=>MenuPreferences.SetFullscreen(value));
            motionToggle=Switch(settings,"Reduce motion",-219,value=>MenuPreferences.SetReducedMotion(value));
            Label("Motion note",settings,"Less camera drift, ash movement and decorative animation.",13,quiet,new Vector2(650,30),new Vector2(0,0.5f),new Vector2(0,-253));
            settingsFirst=MenuButton(settings,"Back","←",-330,ShowHome);
            Label("Saved note",settings,Application.isEditor ? "Saved automatically. Fullscreen applies in the standalone game." : "Your preferences are saved automatically.",12,quiet,new Vector2(700,30),new Vector2(0,0.5f),new Vector2(0,-380));
        }

        private void BuildCredits()
        {
            Heading(credits,"The people behind the light","THE LIGHTEN ROAD");
            Credit("Siraç", "GAME DEVELOPER", "@unitybtw", 145);
            Credit("Kemal", "GAME DEVELOPER", "@kemal-berke", 62);
            Credit("Can", "ARTIST", "@benesas", -21);
            Credit("Maksim", "ARTIST", "@Ichnolith", -104);
            Credit("Furkan", "GAME DESIGNER", "@aeshmacodex", -187);
            creditsFirst=MenuButton(credits,"Back","←",-310,ShowHome);
        }

        private void Credit(string name,string role,string handle,float y)
        {
            Label(name,credits,name,26,ivory,new Vector2(240,44),new Vector2(0,0.5f),new Vector2(0,y));
            var r=Label(role+name,credits,role,11,gold,new Vector2(340,25),new Vector2(0,0.5f),new Vector2(270,y+16));r.characterSpacing=3;
            Label(handle,credits,handle,14,quiet,new Vector2(340,25),new Vector2(0,0.5f),new Vector2(270,y-8));
        }

        private void BuildQuit()
        {
            Heading(quit,"Leave the light behind?","UNTIL NEXT TIME");
            Label("Quit note",quit,"The archive will be here when you return.",22,quiet,new Vector2(720,80),new Vector2(0,0.5f),new Vector2(0,100));
            quitFirst=MenuButton(quit,"Stay","←",-20,ShowHome);
            MenuButton(quit,"Quit game","×",-100,ConfirmQuit);
        }

        private void Heading(RectTransform parent,string title,string subtitle)
        {
            var sub=Label("Section label",parent,subtitle,11,gold,new Vector2(720,30),new Vector2(0,0.5f),new Vector2(0,300));sub.characterSpacing=4;
            Label("Heading",parent,title,43,ivory,new Vector2(760,100),new Vector2(0,0.5f),new Vector2(0,205));
        }

        private Button MenuButton(RectTransform parent,string caption,string number,float y,UnityEngine.Events.UnityAction action)
        {
            var row=Rect(caption,parent,new Vector2(440,56),new Vector2(0,0.5f),new Vector2(0,y));
            var hit=row.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var button=row.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;button.onClick.AddListener(action);
            var marker=Rect("Ember marker",row,new Vector2(4,4),new Vector2(0,0.5f),new Vector2(5,0)).gameObject.AddComponent<Image>();marker.raycastTarget=false;
            var text=Label("Caption",row,caption.ToUpperInvariant(),22,quiet,new Vector2(330,56),new Vector2(0,0.5f),new Vector2(32,0));text.rectTransform.pivot=new Vector2(0,0.5f);text.characterSpacing=4;
            var index=Label("Index",row,number,10,gold,new Vector2(40,30),new Vector2(0,0.5f),new Vector2(370,0));index.rectTransform.pivot=new Vector2(0,0.5f);
            var line=Rect("Focus rule",row,new Vector2(0,1),new Vector2(0,0),new Vector2(32,0)).gameObject.AddComponent<Image>();line.raycastTarget=false;
            var motion=row.gameObject.AddComponent<MenuButtonMotion>();motion.Caption=text.rectTransform;motion.Text=text;motion.Rule=line;motion.Marker=marker;
            return button;
        }

        private Slider AudioSlider(RectTransform parent,string title,float y)
        {
            var row=Rect(title,parent,new Vector2(610,75),new Vector2(0,0.5f),new Vector2(0,y));
            Label("Label",row,title.ToUpperInvariant(),14,ivory,new Vector2(450,30),new Vector2(0,1),new Vector2(0,-30)).characterSpacing=2;
            var value=Label("Value",row,"100%",14,gold,new Vector2(100,30),new Vector2(1,1),new Vector2(-100,-30));value.alignment=TextAlignmentOptions.Right;
            var hit=Stretch("Hit area",row);var hitImage=hit.gameObject.AddComponent<Image>();hitImage.color=Color.clear;
            var slider=row.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.value=1;
            var rail=Rect("Rail",hit,new Vector2(610,3),new Vector2(0,0),new Vector2(0,12));rail.gameObject.AddComponent<Image>().color=new Color32(60,62,54,255);
            var fillArea=Stretch("Fill area",rail);var fill=Image("Fill",fillArea,gold);slider.fillRect=fill.rectTransform;
            var handleArea=Stretch("Handle area",rail);var handle=Rect("Handle",handleArea,new Vector2(9,17),new Vector2(1,0.5f),Vector2.zero);handle.pivot=Vector2.one*0.5f;
            var handleImage=handle.gameObject.AddComponent<Image>();handleImage.color=ivory;slider.handleRect=handle;slider.targetGraphic=handleImage;
            var colors=slider.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,0.88f,0.65f);colors.selectedColor=colors.highlightedColor;slider.colors=colors;
            return slider;
        }

        private Toggle Switch(RectTransform parent,string title,float y,UnityEngine.Events.UnityAction<bool> change)
        {
            var row=Rect(title,parent,new Vector2(610,52),new Vector2(0,0.5f),new Vector2(0,y));
            var hit=row.gameObject.AddComponent<Image>();hit.color=Color.clear;
            Label("Label",row,title.ToUpperInvariant(),14,ivory,new Vector2(450,52),new Vector2(0,0.5f),Vector2.zero).rectTransform.pivot=new Vector2(0,0.5f);
            var value=Label("Value",row,"OFF",13,gold,new Vector2(60,52),new Vector2(1,0.5f),new Vector2(-60,0));value.rectTransform.pivot=new Vector2(0,0.5f);value.alignment=TextAlignmentOptions.Right;
            var toggle=row.gameObject.AddComponent<Toggle>();toggle.targetGraphic=hit;toggle.onValueChanged.AddListener(v=>{change(v);UpdateToggleCaption(toggle);});
            var selected=toggle.colors;selected.highlightedColor=new Color(0.7f,0.6f,0.4f,0.1f);selected.selectedColor=selected.highlightedColor;toggle.colors=selected;
            return toggle;
        }

        private RectTransform Panel(string name,RectTransform parent,out CanvasGroup group)
        {
            var rect=Stretch(name,parent);rect.pivot=Vector2.zero;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(0,1);rect.offsetMin=new Vector2(160,0);rect.offsetMax=new Vector2(880,0);
            group=rect.gameObject.AddComponent<CanvasGroup>();return rect;
        }

        private RectTransform Stretch(string name,Transform parent)
        {
            var rect=Rect(name,parent,Vector2.zero,Vector2.one*0.5f,Vector2.zero);rect.pivot=Vector2.one*0.5f;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }

        private RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 anchor,Vector2 pos)
        {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(0,0);rect.sizeDelta=size;rect.anchoredPosition=pos;return rect;
        }

        private Image Image(string name,RectTransform parent,Color color)
        {
            var rect=Stretch(name,parent);var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }

        private TMP_Text Label(string name,Transform parent,string value,float size,Color color,Vector2 dimensions,Vector2 anchor,Vector2 pos)
        {
            var rect=Rect(name,parent,dimensions,anchor,pos);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Left;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;return text;
        }

        private static float Ease(float t) => t*t*(3f-2f*t);

        private static Texture2D CreateGlow()
        {
            const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.name="Menu lantern falloff";
            var colors=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new Vector2(x,y),Vector2.one*(size-1)*0.5f)/(size*0.5f);float a=Mathf.Pow(Mathf.Clamp01(1f-d),2f);colors[y*size+x]=new Color(1,1,1,a);}texture.SetPixels(colors);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;return texture;
        }
    }
}
