using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
namespace TheLightenRoad.Frontend
{
    /// <summary>Renders live handwritten ink onto an independent, mesh-based hardcover model.</summary>
    public sealed class ArchiveBookPresentation : MonoBehaviour
    {
        public Camera BookCamera{get;private set;}
        public Camera InkCamera{get;private set;}
        public ArchiveBookModel Model{get;private set;}
        public ArchiveBookPageTurn Turn{get;private set;}
        public RenderTexture Ink{get;private set;}
        public RenderTexture PreviewInk{get;private set;}
        private Action<int> preview;
        private Canvas sourceCanvas,previewCanvas;
        private TMP_Text[] previewLabels;
        private GameObject pixelDisplay;
        private RawImage pixelImage;
        public RenderTexture PixelTarget{get;private set;}
        private int pixelWidth,pixelHeight;
        public bool Visible{get;private set;}
        public bool IsInitialized=>Model!=null && Model.IsBound && Ink!=null && PreviewInk!=null && PixelTarget!=null && Turn!=null;
        public void Rebind(Func<bool> canTurn,Action<int> commit,Action<int> preview)
        {this.preview=preview;if(Turn!=null)Turn.Initialize(this,canTurn,commit);}
        private Transform stage;private float lastAspect,baseDistance,openingProgress;private Renderer[] modelRenderers;
        private readonly List<Canvas> hiddenOverlays=new List<Canvas>();
        public void Initialize(Canvas canvas,Button[] controls,Func<bool> canTurn,Action<int> commit,Action<int> preview)
        {
            this.preview=preview;sourceCanvas=canvas;
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)scaler.enabled=false;
            var raycaster=canvas.GetComponent<GraphicRaycaster>();if(raycaster!=null)raycaster.enabled=false;
            canvas.renderMode=RenderMode.WorldSpace;
            var rect=(RectTransform)canvas.transform;rect.position=new Vector3(10000,0,0);rect.rotation=Quaternion.identity;rect.localScale=Vector3.one;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,1260);rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,900);
            foreach(var child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
            Ink=new RenderTexture(2048,1464,24,RenderTextureFormat.ARGB32){name="Live archive handwriting",filterMode=FilterMode.Point};Ink.Create();
            PreviewInk=new RenderTexture(Ink.descriptor){name="Next archive spread preview",filterMode=FilterMode.Point};PreviewInk.Create();
            InkCamera=CameraChild("Archive ink camera",transform);InkCamera.transform.position=new Vector3(10000,0,-1200);
            InkCamera.orthographic=true;InkCamera.orthographicSize=450;InkCamera.aspect=1.4f;InkCamera.cullingMask=1<<30;
            InkCamera.clearFlags=CameraClearFlags.SolidColor;InkCamera.backgroundColor=Color.clear;InkCamera.targetTexture=Ink;InkCamera.depth=90;
            canvas.worldCamera=InkCamera;
            stage=new GameObject("Archive model stage").transform;stage.SetParent(transform,false);stage.position=new Vector3(20000,0,0);
            BookCamera=CameraChild("Archive perspective camera",stage);BookCamera.fieldOfView=34;BookCamera.cullingMask=1<<31;
            BookCamera.depth=100;BookCamera.clearFlags=CameraClearFlags.SolidColor;BookCamera.backgroundColor=new Color(.022f,.017f,.012f);
            var prefab=Resources.Load<GameObject>("UI/ArchiveBook3D/ArchiveBook");
            if(prefab==null)throw new InvalidOperationException("The archive model is missing. Rebuild it with ArchiveBookModelBuilder.Build.");
            Model=Instantiate(prefab,stage,false).GetComponent<ArchiveBookModel>();Model.transform.localRotation=Quaternion.Euler(15,0,-1);
            Model.Bind(Ink);modelRenderers=Model.GetComponentsInChildren<Renderer>(true);SetOpening(1);
            Turn=gameObject.AddComponent<ArchiveBookPageTurn>();Turn.Initialize(this,canTurn,commit);
            gameObject.AddComponent<ArchiveBookRaycaster>().Initialize(this,controls,rect,gameObject);
            pixelDisplay=new GameObject("Pixel art book display",typeof(RectTransform),typeof(Canvas),typeof(RawImage),typeof(GraphicRaycaster));pixelDisplay.transform.SetParent(transform,false);
            var displayCanvas=pixelDisplay.GetComponent<Canvas>();displayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;displayCanvas.sortingOrder=120;
            pixelImage=pixelDisplay.GetComponent<RawImage>();pixelImage.raycastTarget=true;
            EnsurePixelTarget();SetAspect((float)Screen.width/Mathf.Max(1,Screen.height));Show(false);
        }
        private static Camera CameraChild(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(Camera));go.transform.SetParent(parent,false);var camera=go.GetComponent<Camera>();
            camera.nearClipPlane=.1f;camera.farClipPlane=2500;camera.allowHDR=false;camera.allowMSAA=false;camera.useOcclusionCulling=false;camera.enabled=false;return camera;
        }
        private void LateUpdate()
        {
            if(BookCamera==null)return;EnsurePixelTarget();
            float aspect=(float)Screen.width/Mathf.Max(1,Screen.height);if(!Mathf.Approximately(aspect,lastAspect))SetAspect(aspect);if(Visible && (Turn==null || !Turn.IsBusy))FitModel(false);
        }
        private void EnsurePixelTarget()
        {
            int scale=Screen.height>=1500?3:2;
            int width=Mathf.Max(320,Screen.width/scale),height=Mathf.Max(180,Screen.height/scale);
            if(PixelTarget!=null && width==pixelWidth && height==pixelHeight)return;
            if(BookCamera!=null)BookCamera.targetTexture=null;DisposeTexture(PixelTarget);
            PixelTarget=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Point-filtered pixel book",filterMode=FilterMode.Point};PixelTarget.Create();
            pixelWidth=width;pixelHeight=height;BookCamera.targetTexture=PixelTarget;if(pixelImage!=null)pixelImage.texture=PixelTarget;
        }
        private void SetAspect(float aspect)
        {
            lastAspect=aspect;BookCamera.aspect=aspect;
            float height=Mathf.Max(9.1f,13.2f/Mathf.Max(.5f,aspect))*1.12f;
            baseDistance=height/(2*Mathf.Tan(BookCamera.fieldOfView*.5f*Mathf.Deg2Rad));
            BookCamera.transform.localPosition=new Vector3(0,.35f,-baseDistance);
        }
        public void FitModel(bool instant,bool allowWhileHeld=false)
        {
            if(Turn!=null && Turn.IsBusy && !allowWhileHeld)return;
            if(modelRenderers==null || Model==null)return;
            bool any=false;Bounds bounds=default;
            foreach(var renderer in modelRenderers)
            {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);
            }
            if(!any)return;
            if(openingProgress>=.999f)
            {
                // Reserve the entire paper sweep before a gesture; framing must not
                // change underneath a held pointer or cause a zoom/drag feedback loop.
                for(int side=-1;side<=1;side+=2)for(int step=0;step<=12;step++)
                    for(int corner=0;corner<2;corner++)
                    {
                        var edge=ArchiveBookGeometry.LeafPoint(side,step/12f,corner==0?-1:1,1,corner);
                        bounds.Encapsulate(Model.transform.TransformPoint(edge+new Vector3(-side*.017f,0,-.035f)));
                    }
            }
            Vector3 min=bounds.min-stage.position,max=bounds.max-stage.position;
            float desiredY=Mathf.Clamp((min.y+max.y)*.5f,.25f,1.15f);
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*7);
            float cameraY=instant?desiredY:Mathf.Lerp(BookCamera.transform.localPosition.y,desiredY,blend);
            float tangent=Mathf.Tan(BookCamera.fieldOfView*.5f*Mathf.Deg2Rad)*.88f;
            float required=baseDistance;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
            {
                var p=new Vector3(x==0?min.x:max.x,y==0?min.y:max.y,z==0?min.z:max.z);
                required=Mathf.Max(required,Mathf.Abs(p.x)/(tangent*BookCamera.aspect)-p.z,Mathf.Abs(p.y-cameraY)/tangent-p.z);
            }
            float distance=instant?required:Mathf.Max(required,Mathf.Lerp(-BookCamera.transform.localPosition.z,required,blend));
            BookCamera.transform.localPosition=new Vector3(0,cameraY,-distance);
        }
        public void PrepareSheet(int side){Model.HoldSheet(Ink,side);preview(side);Model.PreviewUnderlay(PreviewInk,side);}
        public void RenderNextSpread()
        {
            if(previewCanvas==null)
            {
                var clone=Instantiate(sourceCanvas.gameObject,transform,false);clone.name="Preview ink canvas";previewCanvas=clone.GetComponent<Canvas>();
                foreach(var node in clone.GetComponentsInChildren<Transform>(true))node.gameObject.layer=29;
                foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))if(!(behaviour is Graphic))behaviour.enabled=false;
                previewLabels=clone.GetComponentsInChildren<TMP_Text>(true);
                foreach(var label in previewLabels)label.name="Preview "+label.name;
                foreach(var button in clone.GetComponentsInChildren<Button>(true))button.name="Preview "+button.name;
            }
            var labels=sourceCanvas.GetComponentsInChildren<TMP_Text>(true);
            for(int i=0;i<labels.Length;i++)
            {
                var from=labels[i];var to=previewLabels[i];to.font=from.font;to.fontStyle=from.fontStyle;to.text=from.text;to.color=from.color;to.fontSize=from.fontSize;
                to.enableAutoSizing=from.enableAutoSizing;to.fontSizeMin=from.fontSizeMin;to.fontSizeMax=from.fontSizeMax;
                to.characterSpacing=from.characterSpacing;to.lineSpacing=from.lineSpacing;to.paragraphSpacing=from.paragraphSpacing;to.ForceMeshUpdate(true,true);
            }
            int mask=InkCamera.cullingMask;var previous=RenderTexture.active;
            try{Canvas.ForceUpdateCanvases();InkCamera.cullingMask=1<<29;RenderPipeline.SubmitRenderRequest(InkCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=PreviewInk});}
            finally{InkCamera.cullingMask=mask;RenderTexture.active=previous;}
        }
        public void SetOpening(float progress)
        {
            openingProgress=progress;
            if(Model==null)return;Model.SetOpening(progress);Model.transform.localPosition=new Vector3(-3.05f*(1-progress),.25f,0);
        }
        public void Show(bool value)
        {
            if(value && !Visible)
            {
                foreach(var overlay in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if(overlay.enabled && overlay.isRootCanvas && overlay.renderMode==RenderMode.ScreenSpaceOverlay && !overlay.transform.IsChildOf(transform))
                    {hiddenOverlays.Add(overlay);overlay.enabled=false;}
            }
            if(!value){foreach(var overlay in hiddenOverlays)if(overlay!=null)overlay.enabled=true;hiddenOverlays.Clear();}
            Visible=value;if(pixelDisplay!=null)pixelDisplay.SetActive(value);if(BookCamera!=null)BookCamera.enabled=value;if(InkCamera!=null)InkCamera.enabled=value;
            if(Model!=null)Model.gameObject.SetActive(value);if(!value && Turn!=null)Turn.Cancel();
        }
        public bool TryPagePoint(Vector2 screen,out Vector3 point,out Vector2 uv)
        {
            point=Vector3.zero;uv=Vector2.zero;if(!Visible || Model==null)return false;
            Physics.SyncTransforms();
            var ray=BookCamera.ViewportPointToRay(new Vector3(screen.x/Mathf.Max(1,Screen.width),screen.y/Mathf.Max(1,Screen.height),0));bool found=false;RaycastHit nearest=default;float distance=float.MaxValue;
            foreach(var collider in new[]{Model.LeftCollider,Model.RightCollider})
                if(collider!=null && collider.gameObject.activeInHierarchy && collider.Raycast(ray,out var hit,1000) && hit.distance<distance){nearest=hit;distance=hit.distance;found=true;}
            if(!found)return false;point=Model.transform.InverseTransformPoint(nearest.point);uv=nearest.textureCoord;return true;
        }
        public bool TryDragPoint(Vector2 screen,out Vector3 point)
        {
            point=Vector3.zero;if(!Visible)return false;
            var ray=BookCamera.ViewportPointToRay(new Vector3(screen.x/Mathf.Max(1,Screen.width),screen.y/Mathf.Max(1,Screen.height),0));var plane=new Plane(Model.transform.TransformDirection(Vector3.back),Model.transform.TransformPoint(new Vector3(0,0,-.15f)));
            if(!plane.Raycast(ray,out var distance))return false;point=Model.transform.InverseTransformPoint(ray.GetPoint(distance));return true;
        }
        // The collider already bounds the paper: top, bottom, outer edge and blank
        // paper are all grabbable, leaving only the narrow sewn binding pinned.
        public bool IsPageEdge(Vector2 uv)=>Mathf.Abs(uv.x-.5f)>.008f;
        public Vector2 ScreenPoint(Vector3 modelPoint)
        {
            var viewport=BookCamera.WorldToViewportPoint(Model.transform.TransformPoint(modelPoint));return new Vector2(viewport.x*Screen.width,viewport.y*Screen.height);
        }
        public void CapturePreview(int width,int height,string path)
        {
            var previous=RenderTexture.active;float oldBase=baseDistance;float aspect=BookCamera.aspect;var position=BookCamera.transform.localPosition;Texture2D image=null;
            var target=RenderTexture.GetTemporary(width,height,24);var pixels=RenderTexture.GetTemporary(Mathf.Max(1,width/2),Mathf.Max(1,height/2),24);pixels.filterMode=FilterMode.Point;
            try
            {
                Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(InkCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=Ink});
                SetAspect((float)width/height);FitModel(true,true);RenderPipeline.SubmitRenderRequest(BookCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=pixels});
                Graphics.Blit(pixels,target);RenderTexture.active=target;image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{BookCamera.aspect=aspect;BookCamera.transform.localPosition=position;baseDistance=oldBase;lastAspect=aspect;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(pixels);RenderTexture.ReleaseTemporary(target);if(image!=null)Destroy(image);}
        }
        private static void DisposeTexture(RenderTexture texture)
        {if(texture==null)return;if(RenderTexture.active==texture)RenderTexture.active=null;texture.Release();Destroy(texture);}
        private void OnDisable()=>Show(false);
        private void OnDestroy(){if(InkCamera!=null)InkCamera.targetTexture=null;if(BookCamera!=null)BookCamera.targetTexture=null;DisposeTexture(Ink);DisposeTexture(PreviewInk);DisposeTexture(PixelTarget);}
    }
}
