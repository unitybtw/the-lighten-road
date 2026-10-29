using System;
using UnityEngine;

namespace TheLightenRoad.Player
{
    public enum LightAnimation { Idle, Walk, Jump, Landing, Flare, EmberSwap, Climb, Sheathing }

    [Serializable]
    public sealed class PlayerSpriteSequence
    {
        public LightAnimation Kind;
        public Sprite[] Frames;
        public float[] Durations;
        public Vector2[] LanternOffsets;
        public AnimationClip PreviewClip;
        public bool Loop;
        public float Duration
        {
            get { float total = 0; if (Durations != null) foreach (float t in Durations) total += t; return total; }
        }
        public int FrameAt(float time)
        {
            if (Frames == null || Frames.Length == 0) return -1;
            float length = Duration;
            if (Loop && length > 0) time = Mathf.Max(0, time) % length;
            for (int i = 0; i < Frames.Length; i++)
            {
                float duration = Durations != null && i < Durations.Length ? Mathf.Max(.001f, Durations[i]) : .1f;
                if (time < duration) return i;
                time -= duration;
            }
            return Frames.Length - 1;
        }
    }

    [CreateAssetMenu(menuName = "The Lighten Road/Player animation catalog")]
    public sealed class PlayerAnimationCatalog : ScriptableObject
    {
        public const string ResourcePath = "Player/LightAnimations";
        public PlayerSpriteSequence[] Sequences;
        public PlayerSpriteSequence Find(LightAnimation kind)
        {
            if (Sequences != null) foreach (var item in Sequences) if (item != null && item.Kind == kind) return item;
            return null;
        }
    }
}
