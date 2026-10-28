using System;
using System.Collections.Generic;
using UnityEngine;
namespace TheLightenRoad.Codex
{
    /// <summary>Session-owned runtime copies keep lecture assets and editor state untouched.</summary>
    public static class CodexSession
    {
        private sealed class Index : ISocialListener
        {
            public readonly HashSet<string> Found=new HashSet<string>();
            public void OnSocialEvent(SocialEvent e){if(e.kind==SocialEventKind.LogFound && Catalog.Find(e.text)!=null)Found.Add(e.text);}
        }
        private static Index index;
        public static ArchiveCatalog Catalog {get;private set;}
        public static SocialChannel Channel {get;private set;}
        public static event Action<string> PageRequested;
        public static int FoundCount=>index?.Found.Count??0;
        public static float Decryption=>Catalog!=null && Catalog.Total>0?100f*FoundCount/Catalog.Total:0;
        public static bool Has(string id)=>index!=null && index.Found.Contains(id);
        public static void Ensure()
        {
            if(Channel!=null)return;
            var catalog=Resources.Load<ArchiveCatalog>("Codex/ArchiveCatalog");
            if(catalog!=null)Begin(catalog);
        }
        public static void Begin(ArchiveCatalog catalog)
        {
            End();Catalog=catalog;
            Channel=catalog.Channel!=null?UnityEngine.Object.Instantiate(catalog.Channel):ScriptableObject.CreateInstance<SocialChannel>();
            var stored=catalog.Channel!=null?catalog.Channel.delivery as StoredDelivery:null;
            var delivery=stored!=null?UnityEngine.Object.Instantiate(stored):ScriptableObject.CreateInstance<StoredDelivery>();
            Channel.hideFlags=HideFlags.DontUnloadUnusedAsset;delivery.hideFlags=HideFlags.DontUnloadUnusedAsset;
            delivery.capacity=Mathf.Max(1,catalog.Total);delivery.Clear();Channel.delivery=delivery;
            Channel.membership=catalog.Channel!=null?catalog.Channel.membership:null;
            index=new Index();Channel.Subscribe(index);
        }
        public static bool Discover(string id)
        {
            Ensure();if(Catalog==null || Catalog.Find(id)==null || Has(id))return false;
            // Record before dispatch so nested interactions cannot pay the same ID twice.
            index.Found.Add(id);Channel.Raise(new SocialEvent{kind=SocialEventKind.LogFound,text=id,value=1});return true;
        }
        public static void RequestPage(string id){if(Has(id))PageRequested?.Invoke(id);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void End()
        {
            if(Channel!=null){UnityEngine.Object.Destroy(Channel.delivery);UnityEngine.Object.Destroy(Channel);}
            Channel=null;Catalog=null;index=null;PageRequested=null;
        }
    }
}
