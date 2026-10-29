using System.Collections.Generic;
using TheLightenRoad.Frontend;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TheLightenRoad.Player;

namespace TheLightenRoad.FirstLight
{
    public sealed class FirstLightPresentation : MonoBehaviour
    {
        public Camera SceneCamera;
        public FollowCamera2D Follow;
        public Light2D Ambient,SeedHalo,DoorLight;
        public SpriteRenderer SeedRing,IgnitionRing,DoorCore;
        public SpriteRenderer[] MarkFaces;
        public LineRenderer DoorOutline;
        public LineRenderer[] Conduits;
        public Vector3[] MarkPositions;
        public Vector3[] OutlinePoints;
        public Vector3[][] ConduitPoints;
        public Image BlackVeil,TopBar,BottomBar;
        public CanvasGroup HintGroup,TitleGroup;
        public TMP_Text Hint,Heading,Chapter;
        private FirstLightTutorial tutorial;
        private CanvasGroup overlayVisibility;
        private AudioSource sound;
        private ParticleSystem passageWisps;
        private readonly List<AudioClip> clips=new List<AudioClip>();
        private string previousHint;
        private float hintTime;
        private FirstLightStage hintStage=(FirstLightStage)(-1);
        private ControllerPromptStyle hintStyle=(ControllerPromptStyle)(-1);
        private bool hintAtSeal;
        private string hintText;
        private LightTrace[] conduitTraces;
        private LightTrace doorTrace;
        private static readonly Color Ember=new Color(1,.62f,.25f),Ivory=new Color(.96f,.97f,1);
        public void Initialize(FirstLightTutorial value)
        {
            // Keep tutorial captions and letterboxing below modal menus without losing their stage state.
            var overlay=BlackVeil.canvas.rootCanvas;
            overlayVisibility=overlay.GetComponent<CanvasGroup>();
            if(overlayVisibility==null)overlayVisibility=overlay.gameObject.AddComponent<CanvasGroup>();
            tutorial=value;sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;
            // Rehydrate line paths from editable scene renderers, not a serialized jagged array.
            ConduitPoints=new Vector3[Conduits.Length][];
            for(int i=0;i<Conduits.Length;i++){ConduitPoints[i]=new Vector3[Conduits[i].positionCount];Conduits[i].GetPositions(ConduitPoints[i]);}
            conduitTraces=new LightTrace[Conduits.Length];
            for(int i=0;i<Conduits.Length;i++)conduitTraces[i]=new LightTrace(Conduits[i],ConduitPoints[i]);
            var motes=new GameObject("White passage / slow light motes",typeof(ParticleSystem));motes.transform.SetParent(transform,false);motes.transform.position=new Vector3(30.2f,1.8f,-.1f);
            passageWisps=motes.GetComponent<ParticleSystem>();passageWisps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=passageWisps.main;main.playOnAwake=false;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(1.2f,2.4f);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.025f,.05f);main.startColor=new Color(1,1,1,.32f);main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=passageWisps.emission;emission.rateOverTime=9;
            var shape=passageWisps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(2,3.2f,.02f);
            var drift=passageWisps.velocityOverLifetime;drift.enabled=true;drift.y=.18f;
            var renderer=motes.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=DoorCore.sharedMaterial;renderer.sortingOrder=7;
            if(OutlinePoints==null || OutlinePoints.Length==0){OutlinePoints=new Vector3[DoorOutline.positionCount];DoorOutline.GetPositions(OutlinePoints);}
            doorTrace=new LightTrace(DoorOutline,OutlinePoints);
        }
        private void LateUpdate()
        {
            if(overlayVisibility!=null)overlayVisibility.alpha=(PauseMenuController.IsPaused || DeathScreenOverlay.IsAnyVisible)?0:1;
        }
        public void SetIgnition(float t)
        {
            var light=tutorial.Lantern.LanternLight;float eased=Mathf.SmoothStep(0,1,t);
            light.enabled=true;light.intensity=tutorial.Lantern.DimIntensity*eased;
            light.pointLightOuterRadius=Mathf.Lerp(.05f,tutorial.Lantern.DimRadius,eased);
        }
        public void Paint(FirstLightStage stage,float time,float dt)
        {
            bool reduced=MenuPreferences.ReducedMotion;
            float opening=stage==FirstLightStage.Awakening?Mathf.InverseLerp(.65f,1.8f,time):1;
            BlackVeil.color=new Color(0,0,0,1-Mathf.SmoothStep(0,1,opening));
            Ambient.intensity=tutorial.HasIgnited?.025f:0;
            bool finding=stage<=FirstLightStage.FindEmber;
            SeedHalo.intensity=finding?Mathf.SmoothStep(0,.55f,opening):Mathf.MoveTowards(SeedHalo.intensity,0,dt*.8f);
            SeedHalo.pointLightOuterRadius=finding?Mathf.Lerp(.15f,2.5f,opening):2.5f;
            SeedRing.enabled=finding && !reduced;
            if(SeedRing.enabled)
            {
                float pulse=.65f+.12f*Mathf.Sin(Time.time*1.8f);
                SeedRing.transform.localScale=Vector3.one*pulse;SeedRing.color=new Color(1,.54f,.17f,.18f*opening);
            }
            IgnitionRing.enabled=stage==FirstLightStage.Ignition && !reduced;
            if(IgnitionRing.enabled)
            {
                float t=Mathf.Clamp01(time/1.8f);IgnitionRing.transform.position=tutorial.Player.transform.position+Vector3.up*.25f;
                IgnitionRing.transform.localScale=Vector3.one*Mathf.Lerp(.2f,7,t);
                IgnitionRing.color=new Color(1,.67f,.31f,Mathf.Sin(t*Mathf.PI)*.4f);
            }
            bool drawing=stage==FirstLightStage.DrawingDoor;
            bool frameDoor=drawing || (stage==FirstLightStage.DoorReady && tutorial.Player.transform.position.x>=18.5f && tutorial.Player.transform.position.x<=27);
            Follow.enabled=stage>=FirstLightStage.Move && !frameDoor;
            if(stage<FirstLightStage.Move)
            {
                Vector3 position=new Vector3(2,2.1f,-10);
                SceneCamera.transform.position=position;
                SceneCamera.orthographicSize=reduced?4.6f:Mathf.Lerp(5.15f,4.6f,opening);
            }
            else if(frameDoor)
            {
                SceneCamera.transform.position=Vector3.Lerp(SceneCamera.transform.position,new Vector3(25,2.1f,-10),reduced?1:1-Mathf.Exp(-dt*2));
                SceneCamera.orthographicSize=Mathf.Lerp(SceneCamera.orthographicSize,5.1f,reduced?1:1-Mathf.Exp(-dt*2));
            }
            else SceneCamera.orthographicSize=Mathf.Lerp(SceneCamera.orthographicSize,4.6f,1-Mathf.Exp(-dt*2));
            float bars=stage<=FirstLightStage.Ignition?1:drawing?.45f:0;
            float barHeight=Mathf.Lerp(TopBar.rectTransform.sizeDelta.y,85*bars,reduced?1:1-Mathf.Exp(-dt*4));
            TopBar.rectTransform.sizeDelta=new Vector2(0,barHeight);BottomBar.rectTransform.sizeDelta=new Vector2(0,barHeight);
            TitleGroup.alpha=stage<=FirstLightStage.FindEmber?Mathf.InverseLerp(0,1.8f,time):0;
            if(stage==FirstLightStage.FindEmber)TitleGroup.alpha=.7f;
            for(int i=0;i<MarkFaces.Length;i++)
            {
                float charge=tutorial.Marks[i];MarkFaces[i].color=Color.Lerp(new Color(.065f,.075f,.08f),Ember,charge);
                float trace=stage==FirstLightStage.DrawingDoor?Mathf.Clamp01(time/2.2f):stage>=FirstLightStage.DoorReady?1:0;
                conduitTraces[i].Apply(trace);Conduits[i].startColor=Conduits[i].endColor=new Color(.95f,.81f,.5f,.75f);
            }
            float reveal=stage==FirstLightStage.DrawingDoor?Mathf.Clamp01(time/2.8f):stage>=FirstLightStage.DoorReady?1:0;
            doorTrace.Apply(reduced && reveal>0?1:reveal);
            DoorCore.color=new Color(1,1,1,Mathf.SmoothStep(0,.97f,Mathf.InverseLerp(.55f,1,reveal)));
            if(passageWisps!=null)
            {
                bool emit=reveal>.65f && !reduced;
                if(emit && !passageWisps.isPlaying)passageWisps.Play();
                else if(!emit && passageWisps.isPlaying)passageWisps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            DoorLight.intensity=1.25f*Mathf.SmoothStep(0,1,reveal);DoorLight.pointLightOuterRadius=Mathf.Lerp(.05f,5,reveal);
            var style=ControllerGlyphService.ActiveStyle;
            bool atSeal=tutorial.Player.transform.position.x>=18.5f;
            if(hintStage!=stage || hintStyle!=style || hintAtSeal!=atSeal)
            {
                hintStage=stage;hintStyle=style;hintAtSeal=atSeal;hintText=TextFor(stage);
            }
            SetHint(hintText,time,dt);
        }
        private string TextFor(FirstLightStage stage)
        {
            string move=ControllerGlyphService.GetMovePrompt(),jump=ControllerGlyphService.GetJumpPrompt(),flare=ControllerGlyphService.GetFlarePrompt();
            switch(stage)
            {
                case FirstLightStage.FindEmber:return move+"   —   Approach the ember.";
                case FirstLightStage.Move:return move+"   —   Carry the light forward.";
                case FirstLightStage.Jump:return jump+"   —   Jump the broken stones.\nKeep moving to cross the gap.";
                case FirstLightStage.Gather:return "Walk close to the embers to gather them.\nA little light is worth keeping.";
                case FirstLightStage.Scout:return tutorial.Player.transform.position.x<18.5f?"Follow the broken floor to the three sleeping marks.":flare+"   —   Use Flare beside the seal.\nOne ember spent. Three marks awakened.";
                case FirstLightStage.DrawingDoor:return "The light remembers a way through.";
                case FirstLightStage.DoorReady:return "Step into the white light.";
                default:return "";
            }
        }
        private void SetHint(string text,float time,float dt)
        {
            if(previousHint!=text){previousHint=text;Hint.text=text;hintTime=0;}
            hintTime+=dt;HintGroup.alpha=text.Length==0?0:Mathf.SmoothStep(0,1,hintTime/.5f);
            Chapter.text="PROLOGUE";
        }
        private sealed class LightTrace
        {
            private readonly LineRenderer renderer;
            private readonly Vector3[] path;
            private readonly float[] lengths;
            private readonly float total;
            private float previous=float.NaN;
            public LightTrace(LineRenderer renderer,Vector3[] path)
            {
                this.renderer=renderer;this.path=path;lengths=new float[path.Length];
                for(int i=1;i<path.Length;i++){lengths[i]=Vector3.Distance(path[i-1],path[i]);total+=lengths[i];}
            }
            public void Apply(float amount)
            {
                amount=Mathf.Clamp01(amount);
                if(amount==previous)return;previous=amount;
                renderer.enabled=amount>0;
                if(amount<=0 || path.Length<2)return;
                float remaining=total*amount;int last=1;
                while(last<path.Length-1 && remaining>=lengths[last]){remaining-=lengths[last];last++;}
                renderer.positionCount=last+1;
                for(int i=0;i<last;i++)renderer.SetPosition(i,path[i]);
                renderer.SetPosition(last,Vector3.Lerp(path[last-1],path[last],lengths[last]>0?remaining/lengths[last]:0));
            }
        }
        public void Chime(float frequency,float length)
        {
            if(sound==null)return;
            const int rate=24000;var samples=new float[Mathf.CeilToInt(rate*length)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;float envelope=Mathf.Min(1,t/.025f)*Mathf.Exp(-t*5/length)*(1-t/length);
                samples[i]=envelope*(Mathf.Sin(2*Mathf.PI*frequency*t)+.24f*Mathf.Sin(2*Mathf.PI*frequency*2.003f*t))*.18f;
            }
            var clip=AudioClip.Create("First light / harmonic chime",samples.Length,1,rate,false);clip.SetData(samples,0);clips.Add(clip);
            sound.PlayOneShot(clip,MenuPreferences.Master*MenuPreferences.Effects*.45f);
        }
        private void OnDestroy(){foreach(var clip in clips)if(clip!=null)Destroy(clip);}
    }
}
