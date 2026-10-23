using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public sealed class SettingsLanternAmbience : MonoBehaviour
    {
        public RectTransform[] Pivots;
        public Image[] Glows;
        public CanvasGroup Visibility;
        private void Update()
        {
            if(Visibility!=null && Visibility.alpha<.001f)return;
            bool reduced=MenuPreferences.ReducedMotion;
            for(int i=0;i<Pivots.Length;i++)
            {
                if(Pivots[i]!=null)Pivots[i].localRotation=Quaternion.Euler(0,0,reduced?0:Mathf.Sin(Time.unscaledTime*.65f+i*1.7f)*.85f);
                if(Glows[i]!=null)Glows[i].color=new Color(1,.61f,.20f,reduced?.14f:.14f+Mathf.Sin(Time.unscaledTime*1.7f+i)*.015f);
            }
        }
    }
}
