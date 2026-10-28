using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace TheLightenRoad.Frontend
{
    /// <summary>Direct manipulation with a reversible half-turn threshold and unscaled settling.</summary>
    public sealed class ArchiveBookPageTurn : MonoBehaviour,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerUpHandler
    {
        private ArchiveBookPresentation view;
        private Func<bool> canTurn;
        private Action<int> commit;
        private bool dragging,settling,committed;
        private int side;
        private float amount,originX,grabY,target,settleFrom,settleTime,settleDuration;
        public bool IsBusy=>dragging||settling;
        public bool IsDragging=>dragging;
        public float Progress=>amount;
        public void Initialize(ArchiveBookPresentation view,Func<bool> canTurn,Action<int> commit){this.view=view;this.canTurn=canTurn;this.commit=commit;}
        public bool Begin(Vector2 screen)
        {
            if(IsBusy || canTurn==null || commit==null || view==null || !canTurn() || !view.TryPagePoint(screen,out var point,out var uv))return false;
            if(!view.IsPageEdge(uv))return false;
            side=point.x>=0?1:-1;originX=point.x;grabY=Mathf.Clamp(point.y/3.48f,-1,1);amount=0;dragging=true;committed=false;
            view.PrepareSheet(side);view.Model.Curl(side,0,grabY);return true;
        }
        public void Drag(Vector2 screen)
        {
            if(!dragging || !view.TryDragPoint(screen,out var point))return;
            amount=Mathf.Clamp01(side*(originX-point.x)/(ArchiveBookGeometry.PageWidth*2));view.Model.Curl(side,amount,grabY);
        }
        public void Release()
        {
            if(!dragging)return;dragging=false;target=amount>=.5f?1:0;
            if(MenuPreferences.ReducedMotion){if(target==1)commit(side);Finish();return;}
            settling=true;settleFrom=amount;settleTime=0;settleDuration=Mathf.Lerp(.20f,.34f,Mathf.Abs(target-amount));
        }
        public void Automatic(int direction)
        {
            if(IsBusy || canTurn==null || commit==null || view==null || !canTurn())return;
            if(MenuPreferences.ReducedMotion){commit(direction);return;}
            side=direction>0?1:-1;amount=0;grabY=-.65f;target=1;committed=false;settling=true;settleFrom=0;settleTime=0;settleDuration=.42f;
            view.PrepareSheet(side);view.Model.Curl(side,0,grabY);
        }
        private void Update()=>Tick(Time.unscaledDeltaTime);
        public void Tick(float dt)
        {
            if(!settling)return;
            settleTime+=Mathf.Max(0,dt);
            float t=Mathf.Clamp01(settleTime/settleDuration);
            float ease=settleFrom==0 && target==1?Mathf.SmoothStep(0,1,t):1-Mathf.Pow(1-t,3);
            amount=Mathf.Lerp(settleFrom,target,ease);
            if(target==1 && amount>=.5f && !committed){committed=true;commit(side);}
            view.Model.Curl(side,amount,grabY);
            if(Mathf.Approximately(amount,target))Finish();
        }
        private void Finish(){dragging=false;settling=false;amount=0;view.Model.ReleaseSheet();}
        public void Cancel(){if(view!=null && view.Model!=null)view.Model.ReleaseSheet();dragging=false;settling=false;committed=false;amount=0;}
        private void OnDisable()=>Cancel();
        private void OnApplicationFocus(bool focus){if(!focus)Cancel();}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Begin(e.position);}
        public void OnBeginDrag(PointerEventData e){/* Capture happens on the page pointer-down; bubbled button drags never capture paper. */}
        public void OnDrag(PointerEventData e)=>Drag(e.position);
        public void OnEndDrag(PointerEventData e)=>Release();
        public void OnPointerUp(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Release();}
    }
}
