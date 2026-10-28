using System;
using System.Collections;
using TheLightenRoad.Frontend;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace TheLightenRoad.FirstLight
{
    /// <summary>Owns only a white curtain across one scene swap, never the tutorial geometry.</summary>
    public sealed class FirstLightDeparture : MonoBehaviour
    {
        private Image curtain;
        private Func<bool> previousGate,ownGate;
        private bool running;
        public static void Begin(string destination,bool load,Action finished)
        {
            var go=new GameObject("First light / white passage",typeof(RectTransform),typeof(Canvas),typeof(FirstLightDeparture));
            DontDestroyOnLoad(go);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=5000;
            var transition=go.GetComponent<FirstLightDeparture>();transition.StartCoroutine(transition.Play(destination,load,finished));
        }
        private IEnumerator Play(string destination,bool load,Action finished)
        {
            running=true;GameControls.BlockGameplay(this,true);
            previousGate=PauseMenuController.IsCompletionSequenceRunning;
            ownGate=()=>running || (previousGate!=null && previousGate());PauseMenuController.IsCompletionSequenceRunning=ownGate;
            var child=new GameObject("White curtain",typeof(RectTransform),typeof(Image));child.transform.SetParent(transform,false);
            curtain=child.GetComponent<Image>();curtain.raycastTarget=true;
            var rect=curtain.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            yield return Fade(0,1,MenuPreferences.ReducedMotion?.35f:1.2f);
            if(load)
            {
                var operation=SceneManager.LoadSceneAsync(destination,LoadSceneMode.Single);
                if(operation!=null)while(!operation.isDone)yield return null;
                yield return null;
            }
            yield return Fade(1,0,MenuPreferences.ReducedMotion?.35f:1f);
            Release();finished?.Invoke();Destroy(gameObject);
        }
        private IEnumerator Fade(float from,float to,float duration)
        {
            float t=0;
            while(t<duration)
            {
                t+=Time.unscaledDeltaTime;float value=Mathf.SmoothStep(from,to,Mathf.Clamp01(t/duration));
                curtain.color=new Color(.98f,.985f,1,value);yield return null;
            }
            curtain.color=new Color(.98f,.985f,1,to);
        }
        private void Release(){running=false;GameControls.BlockGameplay(this,false);if(PauseMenuController.IsCompletionSequenceRunning==ownGate)PauseMenuController.IsCompletionSequenceRunning=previousGate;}
        private void OnDisable()=>Release();
    }
}
