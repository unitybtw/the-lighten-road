using System.IO;
using System.Linq;
using TheLightenRoad.Codex;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace TheLightenRoad.Frontend.Editor
{
    [InitializeOnLoad]
    public static class ArchiveCodexSetup
    {
        static ArchiveCodexSetup(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Library/Codex-setup.txt"))return;
            File.Delete("Library/Codex-setup.txt");Create();File.WriteAllText("Library/Codex-setup-status.txt","Created catalog, channel, policies, terminal prefab and two FurkanTutorial terminals.");
        }
        [MenuItem("The Lighten Road/Codex/Install archive terminals")]
        public static void Create()
        {
            const string root="Assets/_Project/Resources/Codex";
            Directory.CreateDirectory(root);Directory.CreateDirectory("Assets/_Project/Prefabs/Codex");AssetDatabase.Refresh();
            T Asset<T>(string name) where T:ScriptableObject
            {
                string path=root+"/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(path);
                if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;
            }
            var delivery=Asset<StoredDelivery>("Delivery_Stored");delivery.capacity=100;
            var membership=Asset<AmbientMembership>("Membership_Ambient");
            var channel=Asset<SocialChannel>("ArchiveChannel");channel.delivery=delivery;channel.membership=membership;
            var first=Asset<ArchiveLog>("01_KeepersNote");first.Id="keepers-note";first.Title="The Keeper's Last Shift";
            first.Text="The lower office has gone quiet. We left our lamps burning, certain someone would return to collect them.\n\nI recorded the shelves' circuit before the last bell. Power gives the archive motion. Without it, even the heaviest shelf becomes a place to stand.";
            first.RoomClue="Drain a moving shelf's vein to freeze it at its current position. Recover your ember before leaving.";
            var second=Asset<ArchiveLog>("02_UpperArchive");second.Id="upper-archive";second.Title="A Margin in the Dark";
            second.Text="They called the light an endless gift. We counted every ember.\n\nBehind the upper shelves I kept a record of the flooded rooms. A circuit that opens a passage can wake the current beneath your feet. Watch both consequences before committing your light.";
            second.RoomClue="Powered water is dangerous; drained water is safe. One circuit may control more than one mechanism.";
            var catalog=Asset<ArchiveCatalog>("ArchiveCatalog");catalog.Channel=channel;catalog.Logs=catalog.Logs.Concat(new[]{first,second}).Where(l=>l!=null).GroupBy(l=>l.Id).Select(g=>g.First()).ToArray();
            foreach(var a in new Object[]{delivery,membership,channel,first,second,catalog})EditorUtility.SetDirty(a);
            var template=Terminal(first,Vector3.zero);
            PrefabUtility.SaveAsPrefabAsset(template,"Assets/_Project/Prefabs/Codex/ArchiveTerminal.prefab");Object.DestroyImmediate(template);
            const string path="Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity";
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            if(!scene.GetRootGameObjects().AnyNamed("Archive Terminals"))
            {
                var rootGo=new GameObject("Archive Terminals");SceneManager.MoveGameObjectToScene(rootGo,scene);
                Terminal(first,new Vector3(2.2f,.9f,0)).transform.SetParent(rootGo.transform,true);
                Terminal(second,new Vector3(37.4f,7.25f,0)).transform.SetParent(rootGo.transform,true);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            if(opened)EditorSceneManager.CloseScene(scene,true);AssetDatabase.SaveAssets();
        }
        static bool AnyNamed(this GameObject[] roots,string name){foreach(var r in roots)if(r.name==name)return true;return false;}
        static GameObject Terminal(ArchiveLog log,Vector3 position)
        {
            var go=new GameObject("Archive Terminal - "+log.Title);go.transform.position=position;
            var whisper=go.AddComponent<DiegeticWhisper>();whisper.ArchiveLog=log;whisper.KeyHint="E";whisper.ActionPrompt="READ ARCHIVE LOG";whisper.RequiredAction=WhisperAction.Interact;whisper.TriggerRadius=1.7f;
            go.AddComponent<ArchiveTerminalVisual>();return go;
        }
    }
}
