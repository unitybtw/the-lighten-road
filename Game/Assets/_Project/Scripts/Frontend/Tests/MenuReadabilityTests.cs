using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class MenuReadabilityTests
    {
        private GameObject host, pauseHost;
        private MainMenuController menu;
        private bool reduced;
        [UnitySetUp] public IEnumerator Setup()
        {
            reduced=MenuPreferences.ReducedMotion;MenuPreferences.SetReducedMotion(true);
            host=new GameObject("Readability test menu",typeof(AudioListener));
            menu=host.AddComponent<MainMenuController>();menu.SkipIntro();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pauseHost!=null)Object.DestroyImmediate(pauseHost);
            if(host!=null)Object.DestroyImmediate(host);
            MenuPreferences.SetReducedMotion(reduced);Time.timeScale=1;
            yield return null;
        }
        [TestCase(1920,1080)] [TestCase(1280,720)] [TestCase(1280,800)] [TestCase(1024,768)] [TestCase(2560,1080)]
        public void ComfortableScaleStaysWithinSafeBounds(float width,float height)
        {
            float pixelScale=Mathf.Sqrt(width/1920f*height/1080f);
            var available=new Vector2(width,height)/pixelScale;
            float factor=MenuReadability.FitScale(available,new Vector2(1260,820));
            Assert.That(1260*factor,Is.LessThanOrEqualTo(available.x*.9f+.01f));
            Assert.That(820*factor,Is.LessThanOrEqualTo(available.y*.9f+.01f));
            Assert.That(20*factor*pixelScale,Is.GreaterThanOrEqualTo(14f),"Body copy must remain readable in the supported preview matrix.");
        }
        [UnityTest] public IEnumerator SettingsPagesHaveSeparateFooterAndComfortableHitTargets()
        {
            menu.ShowSettings();
            for(int page=0;page<4;page++)
            {
                menu.SelectSettingsSection(page);yield return new WaitForSecondsRealtime(.25f);
                string name=new[]{"Audio settings","Display settings","Controls settings","Accessibility settings"}[page];
                var section=menu.GetComponentsInChildren<RectTransform>(true).Single(x=>x.name==name);
                foreach(var subtitle in section.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Subtitle"))
                    Assert.That(subtitle.fontSize,Is.GreaterThanOrEqualTo(20));
                foreach(var toggle in section.GetComponentsInChildren<Toggle>())
                {
                    Assert.That(((RectTransform)toggle.transform).rect.height,Is.GreaterThanOrEqualTo(72));
                    Assert.That(toggle.transform.Find("Row Plate").GetComponent<Image>().raycastTarget,Is.True);
                }
                foreach(var slider in section.GetComponentsInChildren<Slider>())
                {
                    Assert.That(((RectTransform)slider.transform).rect.height,Is.GreaterThanOrEqualTo(52));
                    Assert.That(slider.GetComponent<Image>().raycastTarget,Is.True);
                }
                if(page==2)
                {
                    var bottom=section.Find("Stick Deadzone");
                    var status=menu.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Preferences are saved on this device");
                    var canvas=section.GetComponentInParent<Canvas>().transform;
                    Assert.That(RectOf((RectTransform)bottom,canvas).Overlaps(RectOf(status.rectTransform,canvas)),Is.False);
                    Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.Null);
                }
                Capture(menu.GetComponentInChildren<Canvas>(),1280,720,"settings-"+page+"-720p");
            }
        }
        [UnityTest] public IEnumerator SettingsAndControllerGuideFitWideTallAndNarrowCanvasProjections()
        {
            var canvas=menu.GetComponentInChildren<Canvas>();
            foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,800),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
            {
                menu.ShowSettings();menu.SelectSettingsSection(2);yield return new WaitForSecondsRealtime(.25f);
                Capture(canvas,resolution.x,resolution.y,"controller-settings-"+resolution.x+"x"+resolution.y);
                AssertVisibleControlsFit(canvas,menu.transform);
                menu.ShowControls();menu.SetControlsDeviceTab(2);yield return new WaitForSecondsRealtime(.25f);
                Capture(canvas,resolution.x,resolution.y,"playstation-guide-"+resolution.x+"x"+resolution.y);
                AssertVisibleControlsFit(canvas,menu.transform);
            }
        }
        [UnityTest] public IEnumerator DeviceTabsAndControllerGuideStaySeparated()
        {
            menu.ShowControls();menu.SetControlsDeviceTab(0);yield return new WaitForSecondsRealtime(.25f);
            var canvas=menu.GetComponentInChildren<Canvas>();
            var tabs=menu.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Controls Device Tabs");
            var buttons=tabs.GetComponentsInChildren<Button>();
            Assert.That(buttons.Length,Is.EqualTo(3));
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
            ExecuteEvents.Execute<IPointerEnterHandler>(buttons[1].gameObject,new PointerEventData(EventSystem.current),(target,data)=>target.OnPointerEnter((PointerEventData)data));
            yield return null;
            var labels=buttons.Select(b=>b.GetComponentInChildren<TMP_Text>()).ToArray();
            for(int i=0;i<buttons.Length;i++)
            {
                var focus=buttons[i].GetComponent<SettingsLightFocus>();
                Assert.That(focus.LeftPointer.gameObject.activeSelf,Is.False);
                Assert.That(focus.RightPointer.gameObject.activeSelf,Is.False);
                Assert.That(buttons[i].transform.Find("Selection rule").gameObject.activeSelf,Is.EqualTo(i==0));
                if(i>0)Assert.That(RectOf(labels[i-1].rectTransform,canvas.transform).Overlaps(RectOf(labels[i].rectTransform,canvas.transform)),Is.False);
            }
            var firstRow=menu.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Action" && t.text=="MOVE / CLIMB").transform.parent as RectTransform;
            Assert.That(firstRow.Find("Description").GetComponent<TMP_Text>().text,Does.Contain("W or Up"));
            Assert.That(RectOf(tabs,canvas.transform).Overlaps(RectOf(firstRow,canvas.transform)),Is.False);
            Capture(canvas,1280,720,"keyboard-tabs-720p");
            for(int tab=1;tab<=2;tab++)
            {
                menu.SetControlsDeviceTab(tab);yield return null;
                for(int i=0;i<buttons.Length;i++)Assert.That(buttons[i].transform.Find("Selection rule").gameObject.activeSelf,Is.EqualTo(i==tab));
                var artwork=menu.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Device artwork");
                Assert.That(artwork.rect.width,Is.GreaterThan(500));
                foreach(var callout in menu.GetComponentsInChildren<TMP_Text>().Where(t=>t.name.StartsWith("Controller callout")))
                {
                    Assert.That(callout.GetPreferredValues(callout.text,callout.rectTransform.rect.width,0).y,Is.LessThanOrEqualTo(callout.rectTransform.rect.height+2),callout.text);
                    Assert.That(RectOf(callout.rectTransform,canvas.transform).Overlaps(RectOf(artwork,canvas.transform)),Is.False,callout.text);
                }
                Capture(canvas,1280,720,tab==1?"xbox-guide-720p":"playstation-guide-720p");
                AssertVisibleControlsFit(canvas,menu.transform);
            }
        }
        [UnityTest] public IEnumerator PauseSettingsAndTabsHaveReadableRowsAt720p()
        {
            menu.ShowHome();yield return null;
            pauseHost=new GameObject("Readability pause");var pause=pauseHost.AddComponent<PauseMenuController>();
            pause.Pause();pause.ShowSettingsView();yield return new WaitForSecondsRealtime(.25f);
            var canvas=pause.GetComponentInChildren<Canvas>();
            Capture(canvas,1280,720,"pause-settings-720p");AssertVisibleControlsFit(canvas,pause.transform);
            pause.ShowControlsView();pause.SetPauseControlsDeviceTab(2);yield return new WaitForSecondsRealtime(.25f);
            var deviceTabs=pause.ControlsPanel.transform.Find("Pause Controls Tabs").GetComponentsInChildren<Button>();
            Assert.That(deviceTabs.Count(b=>b.transform.Find("Selection rule")!=null && b.transform.Find("Selection rule").gameObject.activeSelf),Is.EqualTo(1));
            Assert.That(deviceTabs.Where(b=>b.transform.Find("Wings Container")!=null).All(b=>!b.transform.Find("Wings Container").gameObject.activeSelf),Is.True);
            Capture(canvas,1280,720,"pause-controls-720p");AssertVisibleControlsFit(canvas,pause.transform);
            foreach(var badge in pause.ControlsPanel.GetComponentsInChildren<RectTransform>().Where(r=>r.name.EndsWith("Bumper Badge")))
                Assert.That(badge.GetComponent<Image>().raycastTarget,Is.True);
            var labels=pause.ControlsPanel.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Description");
            Assert.That(labels.Count(),Is.EqualTo(6));Assert.That(labels.All(t=>t.fontSize>=20),Is.True);
            foreach(var badge in pause.ControlsPanel.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="Keycap Badge"))
            {
                var icon=(RectTransform)badge.Find("Glyph Icon");var key=badge.Find("Key Text").GetComponent<TMP_Text>();
                Assert.That(RectOf(icon,canvas.transform).Overlaps(RectOf(key.rectTransform,canvas.transform)),Is.False,"Device icon and key label must not overlap.");
                Assert.That(key.GetPreferredValues().x,Is.LessThanOrEqualTo(key.rectTransform.rect.width+2),key.text);
            }
            pause.Resume();
        }
        [UnityTest] public IEnumerator CreditsIncludeTheWholeTeamAndFitSmallScreens()
        {
            menu.ShowCredits();yield return new WaitForSecondsRealtime(.25f);
            var root=menu.CreditsGroup.transform;
            var names=root.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Name").ToArray();
            foreach(var handle in new[]{"unitybtw","kemal-berke","benesas","Ichnolith","aeshmacodex"})
                Assert.That(names.Any(t=>t.text.Contains(handle)),Is.True,handle);
            Assert.That(root.GetComponentsInChildren<TMP_Text>().Count(t=>t.name=="Role" && t.text=="Artist"),Is.EqualTo(2));
            var canvas=menu.GetComponentInChildren<Canvas>();
            foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768)})
            {
                Capture(canvas,resolution.x,resolution.y,"credits-"+resolution.x);
                AssertVisibleControlsFit(canvas,root);
                foreach(var name in names)
                    Assert.That(name.GetPreferredValues().x,Is.LessThanOrEqualTo(name.rectTransform.rect.width+2),name.text);
            }
        }
        [UnityTest] public IEnumerator ConfirmationsKeepFocusInsideAndCancelWithoutChangingPreferences()
        {
            menu.ShowSettings();menu.SelectSettingsSection(2);menu.RequestSettingsReset();
            yield return new WaitForSecondsRealtime(.25f);
            var panel=menu.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Restore settings confirmation");
            bool vibration=MenuPreferences.Vibration;
            AssertConfirmationFocus(panel);
            var footer=menu.GetComponentsInChildren<RectTransform>(true).Single(r=>r.Find("Slot_Reset")!=null && r.Find("Slot_Tab")!=null);
            Assert.That(footer.Find("Slot_Reset").gameObject.activeSelf,Is.False);
            Assert.That(footer.Find("Slot_Tab").gameObject.activeSelf,Is.False);
            var canvas=menu.GetComponentInChildren<Canvas>();
            Capture(canvas,1280,720,"restore-confirmation-720p");
            menu.CancelSettingsReset();Assert.That(MenuPreferences.Vibration,Is.EqualTo(vibration));
            menu.ShowQuit();yield return new WaitForSecondsRealtime(.25f);
            var quit=menu.QuitGroup.transform;AssertConfirmationFocus(quit);
            Capture(canvas,1280,720,"quit-confirmation-720p");AssertVisibleControlsFit(canvas,quit);
            var cancel=quit.GetComponentsInChildren<Button>().Single(b=>b.name=="Cancel");
            cancel.onClick.Invoke();Assert.That(menu.CurrentView,Is.EqualTo("home"));
        }
        private static void AssertConfirmationFocus(Transform root)
        {
            var buttons=root.GetComponentsInChildren<Button>();Assert.That(buttons.Length,Is.EqualTo(2));
            foreach(var button in buttons)
            {
                Assert.That(button.navigation.mode,Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(buttons.Contains(button.navigation.selectOnLeft),Is.True);
                Assert.That(buttons.Contains(button.navigation.selectOnRight),Is.True);
            }
            Assert.That(RectOf((RectTransform)buttons[0].transform,root).Overlaps(RectOf((RectTransform)buttons[1].transform,root)),Is.False);
        }
        private static Rect RectOf(RectTransform rect,Transform relative)
        {
            var points=new Vector3[4];rect.GetWorldCorners(points);
            var low=relative.InverseTransformPoint(points[0]);var high=relative.InverseTransformPoint(points[2]);
            return Rect.MinMaxRect(low.x,low.y,high.x,high.y);
        }
        private static void AssertVisibleControlsFit(Canvas canvas,Transform root)
        {
            var rect=((RectTransform)canvas.transform).rect;
            foreach(var item in root.GetComponentsInChildren<Selectable>())
            {
                if(!item.IsInteractable())continue;
                var bounds=RectOf((RectTransform)item.transform,canvas.transform);
                Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(rect.xMin+1),item.name);
                Assert.That(bounds.xMax,Is.LessThanOrEqualTo(rect.xMax-1),item.name);
                Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(rect.yMin+1),item.name);
                Assert.That(bounds.yMax,Is.LessThanOrEqualTo(rect.yMax-1),item.name);
            }
        }
        internal static void Capture(Canvas canvas,int width,int height,string name)
        {
            // Off-screen UI projection: match the shipped CanvasScaler's logarithmic width/height blend.
            // This checks layout and rendering, not physical-display or device-specific input behavior.
            float pixels=Mathf.Sqrt(width/1920f*height/1080f);
            var units=new Vector2(width,height)/pixels;
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)scaler.enabled=false;
            canvas.renderMode=RenderMode.WorldSpace;
            var root=(RectTransform)canvas.transform;root.position=Vector3.zero;root.rotation=Quaternion.identity;root.localScale=Vector3.one;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,units.x);root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,units.y);
            Canvas.ForceUpdateCanvases();
            foreach(var layout in canvas.GetComponentsInChildren<MenuReadability>(true))layout.SendMessage("LateUpdate");
            Canvas.ForceUpdateCanvases();
            var go=new GameObject("Offscreen UI camera",typeof(Camera));var camera=go.GetComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=units.y*.5f;camera.aspect=(float)width/height;
            camera.transform.position=new Vector3(0,0,-100);camera.nearClipPlane=.1f;camera.farClipPlane=1000;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.01f,.015f,.025f);
            var target=RenderTexture.GetTemporary(width,height,24);var previous=RenderTexture.active;
            Texture2D image=null;
            try
            {
                camera.targetTexture=target;canvas.worldCamera=camera;camera.Render();RenderTexture.active=target;
                image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                Directory.CreateDirectory("Library/Readability");File.WriteAllBytes("Library/Readability/"+name+".png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(go);if(image!=null)Object.DestroyImmediate(image);}
        }
    }
}
