using TMPro;
using UnityEngine;

namespace TheLightenRoad.Frontend
{
    /// <summary>One typographic voice for title, pause and settings. Body copy keeps its readable sans fallback.</summary>
    public static class ArchiveMenuStyle
    {
        private static TMP_FontAsset serif;
        public static TMP_FontAsset Serif => serif != null ? serif : (serif=Resources.Load<TMP_FontAsset>("UI/ArchiveSerif"));
        public static void Apply(TMP_Text label,string role,float size)
        {
            bool display=size>=18f || role=="Title" || role=="Caption" || role=="Heading" || role.Contains("Name");
            if(display && Serif!=null)label.font=Serif;
            label.fontSize=Mathf.Max(14f,size);
            if(display)label.characterSpacing=1.4f;
        }
    }
}
