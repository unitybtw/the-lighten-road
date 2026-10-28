using UnityEngine;
using System.Linq;
namespace TheLightenRoad.Frontend
{
    /// <summary>The independently editable hardcover, spine, folios, and deformable sheet.</summary>
    public sealed class ArchiveBookModel : MonoBehaviour
    {
        public Transform CoverHinge,LeftAssembly;
        public MeshRenderer LeftPaper,RightPaper,TurningPaper;
        public MeshFilter TurningFilter;
        public MeshCollider LeftCollider,RightCollider;
        private Mesh dynamicLeaf;
        private Material leftMaterial,rightMaterial,turnMaterial;
        private RenderTexture frozenInk;private RenderTexture liveInk;
        private MeshRenderer[] fixedLeftCover;
        public bool IsBound=>dynamicLeaf!=null && leftMaterial!=null && rightMaterial!=null && turnMaterial!=null && frozenInk!=null && liveInk!=null;
        public void Bind(RenderTexture ink)
        {
            liveInk=ink;
            leftMaterial=new Material(LeftPaper.sharedMaterial);rightMaterial=new Material(RightPaper.sharedMaterial);turnMaterial=new Material(TurningPaper.sharedMaterial);
            LeftPaper.sharedMaterial=leftMaterial;RightPaper.sharedMaterial=rightMaterial;TurningPaper.sharedMaterial=turnMaterial;
            foreach(var material in new[]{leftMaterial,rightMaterial,turnMaterial}){material.SetTexture("_InkTex",ink);material.SetFloat("_HasInk",1);}
            dynamicLeaf=Instantiate(TurningFilter.sharedMesh);dynamicLeaf.MarkDynamic();TurningFilter.sharedMesh=dynamicLeaf;
            frozenInk=new RenderTexture(ink.descriptor){name="Held sheet ink snapshot",filterMode=FilterMode.Point};frozenInk.Create();
            TurningPaper.gameObject.SetActive(false);
        }
        public void SetOpening(float progress)
        {
            if(CoverHinge==null)return;
            CoverHinge.gameObject.SetActive(progress<.999f);
            CoverHinge.localRotation=Quaternion.Euler(0,180*progress,0);
            LeftAssembly.gameObject.SetActive(true);
            LeftAssembly.localRotation=Quaternion.Euler(0,-180*(1-progress),0);
            if(fixedLeftCover==null)fixedLeftCover=LeftAssembly.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.name.Contains("hardcover") || r.name.Contains("cover panel") || r.name.Contains("brass corner")).ToArray();
            foreach(var renderer in fixedLeftCover)renderer.enabled=progress>=.999f;
        }
        public void HoldSheet(RenderTexture ink){var previous=RenderTexture.active;try{Graphics.Blit(ink,frozenInk);}finally{RenderTexture.active=previous;}turnMaterial.SetTexture("_InkTex",frozenInk);TurningPaper.gameObject.SetActive(true);}
        public void Curl(int side,float amount,float grabY)
        {
            ArchiveBookGeometry.UpdateLeaf(dynamicLeaf,side,Mathf.Clamp01(amount),grabY);
            var shade=new Vector4(amount,side,0,0);leftMaterial.SetVector("_TurnShade",shade);rightMaterial.SetVector("_TurnShade",shade);
        }
        public void PreviewUnderlay(RenderTexture next){leftMaterial.SetTexture("_InkTex",next);rightMaterial.SetTexture("_InkTex",next);}
        public void ReleaseSheet()
        {
            TurningPaper.gameObject.SetActive(false);
            leftMaterial.SetTexture("_InkTex",liveInk);rightMaterial.SetTexture("_InkTex",liveInk);
            leftMaterial.SetVector("_TurnShade",Vector4.zero);rightMaterial.SetVector("_TurnShade",Vector4.zero);
        }
        private void OnDestroy()
        {
            if(dynamicLeaf!=null)Destroy(dynamicLeaf);
            foreach(var m in new[]{leftMaterial,rightMaterial,turnMaterial})if(m!=null)Destroy(m);
            if(frozenInk!=null){if(RenderTexture.active==frozenInk)RenderTexture.active=null;frozenInk.Release();Destroy(frozenInk);}
        }
    }
}
