using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace TheLightenRoad.Frontend
{
    /// <summary>Cursor-constrained paper with reversible, unscaled settling.</summary>
    public sealed class ArchiveBookPageTurn : MonoBehaviour,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerUpHandler
    {
        private ArchiveBookPresentation view;
        private Func<bool> canTurn;
        private Action<int> commit;
        private bool dragging,settling,committed;
        private int side,pointerId=int.MinValue;
        private float amount,grabY,grabU,grabV,pullY,releasePull,target,settleFrom,settleTime,settleDuration;
        public float VerticalPull=>pullY;
        private Vector2 pointer,screenOffset,pressPosition;
        public bool IsBusy=>dragging||settling;
        public bool IsDragging=>dragging;
        public float Progress=>amount;
        public void Initialize(ArchiveBookPresentation view,Func<bool> canTurn,Action<int> commit){this.view=view;this.canTurn=canTurn;this.commit=commit;}
        public bool Begin(Vector2 screen)
        {
            if(IsBusy || canTurn==null || commit==null || view==null || !canTurn() || !view.TryPagePoint(screen,out var point,out var uv))return false;
            if(!view.IsPageEdge(uv))return false;
            side=point.x>=0?1:-1;
            grabU=Mathf.Clamp01((Mathf.Abs(point.x)+.002f)/ArchiveBookGeometry.PageWidth);
            grabV=Mathf.Clamp01(point.y/ArchiveBookGeometry.PageHeight+.5f);
            grabY=Mathf.Clamp(grabV*2-1,-.95f,.95f);
            amount=0;pullY=0;dragging=true;committed=false;pointer=pressPosition=screen;
            view.PrepareSheet(side);view.Model.Curl(side,0,grabY);
            screenOffset=screen-SheetScreenPoint(0,0);
            return true;
        }
        private Vector2 SheetScreenPoint(float progress,float pull)=>view.ScreenPoint(view.Model.SheetPoint(side,progress,grabY,grabU,grabV,pull));
        public void Drag(Vector2 screen)
        {
            if(!dragging || screen==pointer)return;
            pointer=screen;FollowPointer();
        }
        private float SeekProgress(float desiredX,float pull)
        {
            float low=0,high=1;
            for(int i=0;i<16;i++)
            {
                float mid=(low+high)*.5f;
                if(SheetScreenPoint(mid,pull).x*side>desiredX*side)low=mid;else high=mid;
            }
            return (low+high)*.5f;
        }
        private void FollowPointer()
        {
            Vector2 desired=pointer-screenOffset;
            // Solve the actual picked material point in two screen axes. Vertical
            // pulling bends the free paper, using its width while keeping the spine pinned.
            float limit=Mathf.Max(.12f,grabU*ArchiveBookGeometry.PageWidth*.58f);
            for(int iteration=0;iteration<6;iteration++)
            {
                amount=SeekProgress(desired.x,pullY);
                float y=SheetScreenPoint(amount,pullY).y;
                float nextAmount=SeekProgress(desired.x,pullY+.01f);
                float derivative=(SheetScreenPoint(nextAmount,pullY+.01f).y-y)/.01f;
                if(Mathf.Abs(derivative)<.01f)break;
                pullY=Mathf.Clamp(pullY+Mathf.Clamp((desired.y-y)/derivative,-.6f,.6f),-limit,limit);
            }
            amount=SeekProgress(desired.x,pullY);
            if(Vector2.SqrMagnitude(pointer-pressPosition)<.01f){amount=0;pullY=0;}
            view.Model.Curl(side,amount,grabY,pullY,grabU);
        }
        public void Release()
        {
            if(!dragging)return;
            dragging=false;pointerId=int.MinValue;
            target=side*view.Model.SheetPoint(side,amount,grabY,grabU,grabV,pullY).x<0?1:0;
            if(MenuPreferences.ReducedMotion || (amount<.002f && Mathf.Abs(pullY)<.002f)){Finish();return;}
            settling=true;settleFrom=amount;releasePull=pullY;settleTime=0;
            settleDuration=.38f+.68f*Mathf.Abs(target-amount);
        }
        public void Automatic(int direction)
        {
            if(IsBusy || canTurn==null || commit==null || view==null || !canTurn())return;
            if(MenuPreferences.ReducedMotion){commit(direction);return;}
            side=direction>0?1:-1;amount=0;pullY=releasePull=0;grabU=1;grabY=-.72f;target=1;committed=false;
            settling=true;settleFrom=0;settleTime=0;settleDuration=1.2f;
            view.PrepareSheet(side);view.Model.Curl(side,0,grabY);
        }
        private void Update()=>Tick(Time.unscaledDeltaTime);
        public void Tick(float dt)
        {
            if(dragging)return;
            if(!settling)return;
            settleTime+=Mathf.Max(0,dt);
            float t=Mathf.Clamp01(settleTime/settleDuration);
            float ease=t*t*t*(t*(t*6f-15f)+10f);
            amount=Mathf.Lerp(settleFrom,target,ease);pullY=Mathf.Lerp(releasePull,0,ease);
            view.Model.Curl(side,amount,grabY,pullY,grabU);
            if(t>=1f)Finish();
        }
        private void Finish()
        {
            // Selection and narration change only when the sheet has actually landed.
            if(target==1 && !committed){committed=true;commit(side);}
            dragging=false;settling=false;amount=0;pullY=0;view.Model.ReleaseSheet();
        }
        public void Cancel(){if(view!=null && view.Model!=null)view.Model.ReleaseSheet();dragging=false;settling=false;committed=false;amount=0;pullY=0;pointerId=int.MinValue;}
        private void OnDisable()=>Cancel();
        private void OnApplicationFocus(bool focus){if(!focus)Cancel();}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left && Begin(e.position))pointerId=e.pointerId;}
        public void OnBeginDrag(PointerEventData e){/* Only a page pointer-down can capture paper. */}
        public void OnDrag(PointerEventData e){if(e.pointerId==pointerId)Drag(e.position);}
        public void OnEndDrag(PointerEventData e){if(e.pointerId==pointerId)Release();}
        public void OnPointerUp(PointerEventData e){if(e.button==PointerEventData.InputButton.Left && e.pointerId==pointerId)Release();}
    }
}
