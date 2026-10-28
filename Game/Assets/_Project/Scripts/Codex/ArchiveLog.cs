using UnityEngine;
namespace TheLightenRoad.Codex
{
    [CreateAssetMenu(menuName="The Lighten Road/Archive Log")]
    public sealed class ArchiveLog : ScriptableObject
    {
        public string Id;
        public string Title;
        public string Location = "THE ARCHIVE";
        [TextArea(4,12)] public string Text;
        [TextArea(2,4)] public string RoomClue;
        public AudioClip Audio;
    }
}
