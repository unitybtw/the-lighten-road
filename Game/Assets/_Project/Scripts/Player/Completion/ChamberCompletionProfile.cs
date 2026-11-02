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
        public string Subtitle = "";
        public TMP_FontAsset Font;
        public Color LightColor = new Color(1f, .70f, .34f);
        [Header("Timing / real seconds")]
        [Min(.05f)] public float ExtinguishSeconds = .85f;
        [Min(.05f)] public float RevealSeconds = .75f;
        [Min(0f)] public float HoldSeconds = .85f;
        [Tooltip("Title fades away while the scene remains fully covered.")]
        [Min(.05f)] public float FadeOutSeconds = .4f;
        [Tooltip("The destination lantern ignites as the new chamber becomes visible.")]
        [Min(.05f)] public float FadeInSeconds = 1.25f;
        [Header("Sound / optional")]
        public AudioClip CompletionSound;
        [Range(0f, 1f)] public float SoundVolume = .65f;
    }
}
