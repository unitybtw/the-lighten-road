using UnityEngine;

namespace TheLightenRoad.UI
{
    /// <summary>Distinct dual-motor textures for DualSense. Not audio-streamed HD haptics.</summary>
    public static class DualSenseHapticPatterns
    {
        public static float Duration(HapticCue cue)
        {
            switch(cue)
            {
                case HapticCue.Jump:return .09f;
                case HapticCue.Landing:return .26f;
                case HapticCue.Flare:return .65f;
                case HapticCue.EmberPickup:return .25f;
                case HapticCue.EmberBreak:return .32f;
                case HapticCue.Death:return .55f;
                case HapticCue.ChamberComplete:return .85f;
                default:return .8f;
            }
        }
        public static Vector2 Sample(HapticCue cue,float time)
        {
            if(float.IsNaN(time) || time<0f || time>=1f)return Vector2.zero;
            float a,b;
            switch(cue)
            {
                case HapticCue.Jump:
                    a=.25f*Pulse(time,0f,.75f);b=Pulse(time,0f,.95f);break;
                case HapticCue.Landing:
                    a=Pulse(time,0f,.55f)+.18f*Pulse(time,.68f,.98f);
                    b=.45f*Pulse(time,0f,.3f);break;
                case HapticCue.Flare:
                    a=Mathf.Sin(time*Mathf.PI)*Mathf.Clamp01(time*5f);
                    b=a*(.65f+.35f*Mathf.Sin(time*Mathf.PI*8f));break;
                case HapticCue.EmberPickup:
                    a=.2f*Pulse(time,.05f,.25f);b=Pulse(time,0f,.3f)+.7f*Pulse(time,.54f,.9f);break;
                case HapticCue.EmberBreak:
                    a=.4f*Pulse(time,.3f,.55f);b=Pulse(time,0f,.2f)+.8f*Pulse(time,.32f,.52f)+.5f*Pulse(time,.68f,.92f);break;
                case HapticCue.Death:
                    a=Pulse(time,0f,.65f);b=.8f*Pulse(time,0f,.23f)+.45f*Pulse(time,.4f,.58f)+.2f*Pulse(time,.75f,.95f);break;
                case HapticCue.ChamberComplete:
                    a=.6f*Pulse(time,0f,.36f)+Pulse(time,.48f,.98f);
                    b=.35f*Pulse(time,.12f,.4f)+.8f*Pulse(time,.57f,.98f);break;
                default:
                    a=Pulse(time,0f,.25f)+.7f*Pulse(time,.7f,.98f);
                    b=Pulse(time,.35f,.6f)+.7f*Pulse(time,.7f,.98f);break;
            }
            return new Vector2(Mathf.Clamp01(a),Mathf.Clamp01(b));
        }
        public static float RightTrigger(HapticCue cue,float time)
        {
            if(time<0f || time>=1f || float.IsNaN(time))return 0f;
            return cue==HapticCue.Flare || cue==HapticCue.Preview ? .18f*Mathf.Sin(Mathf.PI*time) : 0f;
        }
        private static float Pulse(float time,float start,float end)
        {
            if(time<start || time>=end)return 0f;
            float t=(time-start)/(end-start);
            return Mathf.Clamp01((t+.06f)*10f)*Mathf.Pow(1f-t,1.5f);
        }
    }
}
