using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TheLightenRoad.Codex;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace TheLightenRoad.Frontend.Tests
{
    public sealed class ArchiveBookInteractionTests
    {
        private readonly List<Object> owned=new List<Object>();private ArchiveCodexMenu menu;private ArchiveBookPresentation view;private bool reduced;private Mouse scriptedMouse,originalMouse;private InputActionAsset mouseActions;private InputDevice[] originalUiDevices;private bool changedFocusSettings;private InputSettings.BackgroundBehavior oldBackground;private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        T Asset<T>() where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();owned.Add(a);return a;}
        [UnitySetUp] public IEnumerator Setup()
        {
            reduced=MenuPreferences.ReducedMotion;MenuPreferences.SetReducedMotion(false);
            var log=Asset<ArchiveLog>();log.Id="long-record";log.Title="A keeper's journal";
            log.Text=string.Join("\n\n",Enumerable.Repeat("The archive kept every forgotten name. We left a lamp burning beside the ledger.",30));
            var catalog=Asset<ArchiveCatalog>();catalog.Logs=new[]{log};catalog.Channel=Asset<SocialChannel>();catalog.Channel.delivery=Asset<StoredDelivery>();catalog.Channel.membership=Asset<AmbientMembership>();
            CodexSession.Begin(catalog);CodexSession.Discover(log.Id);var host=new GameObject("3D archive regression");owned.Add(host);menu=host.AddComponent<ArchiveCodexMenu>();view=host.GetComponent<ArchiveBookPresentation>();
            menu.Open(log.Id);
            float readyBy=Time.realtimeSinceStartup+10;
            do{yield return null;}while(menu.IsAnimating && Time.realtimeSinceStartup<readyBy);
            Assert.That(ArchiveCodexMenu.IsOpen,Is.True,"The fixture must retain its open reader.");
            Assert.That(menu.IsAnimating,Is.False,"Wait for the actual opening state, not a fixed wall-clock delay.");
            yield return null;
        }
        [TearDown] public void Cleanup(){if(changedFocusSettings){InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;changedFocusSettings=false;}if(mouseActions!=null)mouseActions.devices=originalUiDevices;if(scriptedMouse!=null){InputSystem.RemoveDevice(scriptedMouse);scriptedMouse=null;}if(originalMouse!=null && originalMouse.added)originalMouse.MakeCurrent();foreach(var item in owned)if(item!=null)Object.DestroyImmediate(item);owned.Clear();CodexSession.End();MenuPreferences.SetReducedMotion(reduced);Time.timeScale=1;}
        string Counter()=>menu.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Page counter").text;
        Vector2 Point(float x,float y=2.2f)=>view.ScreenPoint(new Vector3(x,y,-.16f));
        [UnityTest] public IEnumerator MouseStateEventsDragAndReleaseThroughTheInputModule()
        {
            oldBackground=InputSystem.settings.backgroundBehavior;oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;changedFocusSettings=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            originalMouse=Mouse.current;scriptedMouse=InputSystem.AddDevice<Mouse>("Archive drag integration");
            mouseActions=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().actionsAsset;
            originalUiDevices=mouseActions.devices.HasValue?mouseActions.devices.Value.ToArray():null;
            mouseActions.devices=new InputDevice[]{scriptedMouse};
            var start=Point(5.4f);var finish=Point(-4);
            InputSystem.QueueStateEvent(scriptedMouse,new MouseState{position=start});yield return null;yield return null;
            InputSystem.QueueStateEvent(scriptedMouse,new MouseState{position=start,buttons=1});yield return null;yield return null;
            Assert.That(view.Turn.IsDragging,Is.True,"A real UI pointer-down must capture the sheet.");
            InputSystem.QueueStateEvent(scriptedMouse,new MouseState{position=finish,buttons=1});yield return null;yield return null;
            Assert.That(view.Turn.Progress,Is.GreaterThan(.5f));
            InputSystem.QueueStateEvent(scriptedMouse,new MouseState{position=finish});yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.Turn.IsBusy,Is.False);Assert.That(Counter(),Does.Contain("PAGE 2 /"));
        }
        [Test] public void UninitializedPointerHandlersIgnoreInputSafely()
        {
            var host=new GameObject("Uninitialized pointer receiver");owned.Add(host);var turn=host.AddComponent<ArchiveBookPageTurn>();
            var pointer=new PointerEventData(EventSystem.current){position=Point(5.4f),button=PointerEventData.InputButton.Left};
            Assert.DoesNotThrow(()=>turn.OnPointerDown(pointer));Assert.DoesNotThrow(()=>turn.OnBeginDrag(pointer));
            Assert.That(turn.Begin(pointer.position),Is.False);Assert.DoesNotThrow(()=>turn.Automatic(1));
        }
        [Test] public void ReenablingReaderRestoresItsNonSerializedGestureCallbacks()
        {
            menu.enabled=false;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ArchiveBookPageTurn).GetField("canTurn",flags).SetValue(view.Turn,null);
            typeof(ArchiveBookPageTurn).GetField("commit",flags).SetValue(view.Turn,null);
            menu.enabled=true;menu.Open("long-record");typeof(ArchiveCodexMenu).GetMethod("AnimateBook",flags).Invoke(menu,new object[]{2.2f});
            Assert.That(view.Turn.Begin(Point(5.4f)),Is.True);view.Turn.Cancel();menu.Close();
        }
        [Test] public void ShortDragReturnsWithoutChangingTheRecord()
        {
            string before=Counter();Assert.That(view.Turn.Begin(Point(5.4f)),Is.True);view.Turn.Drag(Point(3));
            Assert.That(view.Model.RightPaper.sharedMaterial.GetTexture("_InkTex"),Is.SameAs(view.PreviewInk));
            Assert.That(view.Turn.Progress,Is.InRange(.1f,.35f));Assert.That(Counter(),Is.EqualTo(before));
            view.Turn.Release();view.Turn.Tick(1);Assert.That(view.Turn.IsBusy,Is.False);Assert.That(Counter(),Is.EqualTo(before));
            Assert.That(view.Model.TurningPaper.gameObject.activeSelf,Is.False);
            Assert.That(view.Model.RightPaper.sharedMaterial.GetTexture("_InkTex"),Is.SameAs(view.Ink));
        }
        [Test] public void CommittedDragAdvancesOnceAndLeftDragReturnsToPreviousPage()
        {
            Assert.That(view.Turn.Begin(Point(5.4f)),Is.True);view.Turn.Drag(Point(-4));Assert.That(Counter(),Does.Contain("PAGE 1 /"));
            view.Turn.Release();view.Turn.Tick(1);Assert.That(Counter(),Does.Contain("PAGE 2 /"));view.Turn.Release();view.Turn.Tick(1);Assert.That(Counter(),Does.Contain("PAGE 2 /"));
            Assert.That(view.Turn.Begin(Point(-5.4f)),Is.True);view.Turn.Drag(Point(4));view.Turn.Release();view.Turn.Tick(1);Assert.That(Counter(),Does.Contain("PAGE 1 /"));
        }
        [Test] public void ClosingWhileDraggingDiscardsTheGestureAndReleasesTheModal()
        {
            var screen=Point(5.4f);bool hit=view.TryPagePoint(screen,out var point,out var uv);
            Assert.That(view.Turn.Begin(screen),Is.True,$"open={ArchiveCodexMenu.IsOpen}, animating={menu.IsAnimating}, hit={hit}, uv={uv}");view.Turn.Drag(Point(-4));menu.Close();view.Turn.Release();view.Turn.Tick(1);
            Assert.That(view.Turn.IsBusy,Is.False);Assert.That(view.BookCamera.enabled,Is.False);Assert.That(view.InkCamera.enabled,Is.False);
            Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(Counter(),Does.Contain("PAGE 1 /"));
        }
        [Test] public void EventSystemRoutesPageGesturesAndButtonsToDifferentTargets()
        {
            var pointer=new PointerEventData(EventSystem.current){position=Point(5.4f),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits.First().gameObject,Is.SameAs(menu.gameObject));
            ExecuteEvents.Execute<IPointerDownHandler>(menu.gameObject,pointer,(target,data)=>target.OnPointerDown((PointerEventData)data));
            pointer.position=Point(3);ExecuteEvents.Execute<IDragHandler>(menu.gameObject,pointer,(target,data)=>target.OnDrag((PointerEventData)data));
            Assert.That(view.Turn.IsDragging,Is.True);
            ExecuteEvents.Execute<IPointerUpHandler>(menu.gameObject,pointer,(target,data)=>target.OnPointerUp((PointerEventData)data));view.Turn.Tick(1);
            var button=menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Next");var canvas=menu.GetComponentsInChildren<Canvas>().Single(c=>c.name=="Codex Canvas");
            var local=canvas.transform.InverseTransformPoint(button.transform.position);pointer.position=Point(local.x/100,(local.y-25)/100);hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.First().gameObject,Is.SameAs(button.gameObject));Assert.That(view.Turn.IsBusy,Is.False);
            pointer.pointerPress=button.gameObject;
            ExecuteEvents.Execute<IBeginDragHandler>(menu.gameObject,pointer,(target,data)=>target.OnBeginDrag((PointerEventData)data));
            pointer.position=Point(5.4f);ExecuteEvents.Execute<IDragHandler>(menu.gameObject,pointer,(target,data)=>target.OnDrag((PointerEventData)data));
            Assert.That(view.Turn.IsBusy,Is.False,"Dragging from a voice or page button must not take a paper sheet.");
        }
        [Test] public void PixelPreviewPreservesNearestNeighborBlocks()
        {
            Assert.That(view.PixelTarget.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(view.BookCamera.targetTexture,Is.SameAs(view.PixelTarget));
            string path=System.IO.Path.Combine(Application.temporaryCachePath,"archive-pixel-preview.png");view.CapturePreview(1280,720,path);
            var image=new Texture2D(2,2);owned.Add(image);image.LoadImage(System.IO.File.ReadAllBytes(path));var pixels=image.GetPixels32();
            for(int y=120;y<600;y+=22)for(int x=200;x<1000;x+=34)
            {
                int i=y*1280+x;Assert.That(pixels[i],Is.EqualTo(pixels[i+1]));Assert.That(pixels[i],Is.EqualTo(pixels[i+1280]));
            }
            System.IO.File.Delete(path);
        }
        [Test] public void UnderlyingSpreadHasItsOwnPrintedCounterWithoutCommittingSelection()
        {
            view.CapturePreview(640,360,System.IO.Path.Combine(Application.temporaryCachePath,"archive-before-preview.png"));
            string before=Counter();Assert.That(view.Turn.Begin(Point(5.4f)),Is.True);
            Texture2D ReadFooter(RenderTexture source)
            {
                var previous=RenderTexture.active;var image=new Texture2D(700,64,TextureFormat.RGBA32,false);owned.Add(image);
                try{RenderTexture.active=source;image.ReadPixels(new Rect(1150,295,700,64),0,0);image.Apply();}finally{RenderTexture.active=previous;}
                return image;
            }
            var original=ReadFooter(view.Ink).GetPixels32();var next=ReadFooter(view.PreviewInk).GetPixels32();
            Assert.That(original.SequenceEqual(next),Is.False,"The page underneath must show its own counter, not a reused copy of the held sheet.");
            Assert.That(Counter(),Is.EqualTo(before));view.Turn.Cancel();
        }
        [Test] public void RaisedPageStaysInsideTheCameraFrustum()
        {
            Assert.That(view.Turn.Begin(Point(5.4f)),Is.True);view.Turn.Drag(Point(1));view.FitModel(true);
            foreach(var renderer in view.Model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled))
            {
                var bounds=renderer.bounds;
                for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                {
                    var p=new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z);
                    var projected=view.BookCamera.WorldToViewportPoint(p);
                    Assert.That(projected.x,Is.InRange(.035f,.965f),renderer.name);Assert.That(projected.y,Is.InRange(.035f,.965f),renderer.name);
                    Assert.That(projected.z,Is.GreaterThan(view.BookCamera.nearClipPlane));
                }
            }
            view.Turn.Cancel();
        }
        [Test] public void CurledSheetKeepsFiniteGeometryAndTheSpineAnchored()
        {
            Assert.That(view.Turn.Begin(Point(5.4f)),Is.True);view.Turn.Drag(Point(0));
            var mesh=view.Model.TurningFilter.sharedMesh;Assert.That(mesh.vertexCount,Is.GreaterThan(1000));
            Assert.That(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)),Is.True);
            Assert.That(mesh.bounds.size.z,Is.GreaterThan(2));
            var first=mesh.vertices[0];Assert.That(first.x,Is.EqualTo(.015f).Within(.001));
            view.Turn.Cancel();
        }
        [Test] public void ExistingOverlayIsRestoredButAlreadyDisabledOverlayRemainsDisabled()
        {
            menu.Close();var a=new GameObject("Existing HUD",typeof(Canvas));owned.Add(a);var b=new GameObject("Disabled HUD",typeof(Canvas));owned.Add(b);
            var enabled=a.GetComponent<Canvas>();enabled.renderMode=RenderMode.ScreenSpaceOverlay;
            var disabled=b.GetComponent<Canvas>();disabled.renderMode=RenderMode.ScreenSpaceOverlay;disabled.enabled=false;
            menu.Open("long-record");Assert.That(enabled.enabled,Is.False);menu.Close();Assert.That(enabled.enabled,Is.True);Assert.That(disabled.enabled,Is.False);
        }
    }
}
