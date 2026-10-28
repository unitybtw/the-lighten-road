using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace TheLightenRoad.Frontend
{
    public sealed class ArchiveBookRaycaster : BaseRaycaster
    {
        private ArchiveBookPresentation view;private Button[] buttons;private RectTransform sourceCanvas;private GameObject dragTarget;
        public void Initialize(ArchiveBookPresentation view,Button[] buttons,RectTransform source,GameObject drag){this.view=view;this.buttons=buttons;sourceCanvas=source;dragTarget=drag;}
        public override Camera eventCamera=>view!=null?view.BookCamera:null;
        public override int sortOrderPriority=>120;
        public override int renderOrderPriority=>120;
        public override void Raycast(PointerEventData e,List<RaycastResult> hits)
        {
            if(view==null || !view.Visible || !view.TryPagePoint(e.position,out var point,out var uv))return;
            var source=sourceCanvas.TransformPoint(new Vector3(uv.x*1260-630,uv.y*900-450,0));
            GameObject target=null;
            foreach(var button in buttons)
            {
                if(button==null || !button.IsInteractable())continue;
                var rect=(RectTransform)button.transform;
                if(rect.rect.Contains(rect.InverseTransformPoint(source))){target=button.gameObject;break;}
            }
            if(target==null)target=dragTarget; // Consume paper clicks even away from a draggable edge.
            if(target==null)return;
            hits.Add(new RaycastResult{gameObject=target,module=this,index=hits.Count,distance=Vector3.Distance(eventCamera.transform.position,view.Model.transform.TransformPoint(point)),
                screenPosition=e.position,worldPosition=view.Model.transform.TransformPoint(point),sortingOrder=120,depth=10});
        }
    }
}
