using UnityEngine;

namespace TheLightenRoad.Audio
{
    /// <summary>
    /// Acoustic surface material classification for dynamic footstep, landing, and scuff foley.
    /// </summary>
    public enum SurfaceType
    {
        Stone = 0,
        Wood = 1,
        Metal = 2,
        Ethereal = 3,
        Water = 4
    }

    /// <summary>
    /// Explicitly declares the audio surface type of a collider or platform.
    /// Read by PlayerAudioController for realistic, dynamic surface acoustics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceAudioTypeProvider : MonoBehaviour
    {
        [SerializeField] private SurfaceType surfaceType = SurfaceType.Stone;

        public SurfaceType SurfaceType
        {
            get => surfaceType;
            set => surfaceType = value;
        }
    }
}
