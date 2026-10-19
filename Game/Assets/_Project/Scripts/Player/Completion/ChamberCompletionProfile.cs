using TMPro;
using UnityEngine;

namespace TheLightenRoad.Player.Completion
{
    [CreateAssetMenu(menuName = "The Lighten Road/Chamber Completion Profile")]
    public sealed class ChamberCompletionProfile : ScriptableObject
    {
        [Header("Presentation")]
        public string Eyebrow = "CHAMBER COMPLETE";
        public string Title = "THE FIRST LIGHT";
        public string Subtitle = "A little light survives.";
        public TMP_FontAsset Font;
        public Color LightColor = new Color(1f, .70f, .34f);
        [Min(1f)] public float LightRadius = 13f;
        [Range(0f, 3f)] public float LightIntensity = 1.15f;
        [Header("Timing / real seconds")]
        [Min(.05f)] public float RevealSeconds = 1.15f;
        [Min(0f)] public float HoldSeconds = 1.45f;
        [Min(.05f)] public float FadeOutSeconds = .8f;
        [Min(.05f)] public float FadeInSeconds = .9f;
        [Header("Sound / optional")]
        public AudioClip CompletionSound;
        [Range(0f, 1f)] public float SoundVolume = .65f;
    }
}
