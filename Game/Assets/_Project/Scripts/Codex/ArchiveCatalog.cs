using UnityEngine;
using System.Linq;
namespace TheLightenRoad.Codex
{
    [CreateAssetMenu(menuName="The Lighten Road/Archive Catalog")]
    public sealed class ArchiveCatalog : ScriptableObject
    {
        public SocialChannel Channel;
        public ArchiveLog[] Logs = new ArchiveLog[0];
        public ArchiveLog Find(string id) => System.Array.Find(Logs,l=>l!=null && l.Id==id);
        public int Total => Logs.Where(l=>l!=null && !string.IsNullOrWhiteSpace(l.Id)).Select(l=>l.Id).Distinct().Count();
    }
}
