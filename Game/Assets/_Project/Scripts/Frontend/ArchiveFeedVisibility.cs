using TheLightenRoad.Codex;
using UnityEngine;
namespace TheLightenRoad.Frontend
{
    public sealed class ArchiveFeedVisibility : MonoBehaviour, ISocialListener
    {
        private CanvasGroup group;
        private float remaining;
        private SocialChannel channel;
        void OnEnable(){group=GetComponent<CanvasGroup>();if(group==null)group=gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;channel=CodexSession.Channel;if(channel!=null)channel.Subscribe(this);}
        void OnDisable(){if(channel!=null)channel.Unsubscribe(this);}
        public void OnSocialEvent(SocialEvent e){if(e.kind==SocialEventKind.LogFound && !e.isReplay)remaining=4;}
        void Update(){remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);group.alpha=ArchiveCodexMenu.IsOpen?0:Mathf.Clamp01(remaining);}
    }
}
