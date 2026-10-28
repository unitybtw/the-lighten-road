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
    public static class OfficeCodexSetup
    {
        static OfficeCodexSetup(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
            if(File.Exists("Library/Office-install.txt")){File.Delete("Library/Office-install.txt");Install();File.WriteAllText("Library/Office-install-status.txt","Installed");}
        }
        [MenuItem("The Lighten Road/Codex/Install Office archive points")]
        public static void Install()
        {
            const string path="Assets/_Project/Scenes/Sandbox/Sirac/Office.unity";
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            if(scene.GetRootGameObjects().Any(r=>r.name=="Office Archive Points")){if(opened)EditorSceneManager.CloseScene(scene,true);return;}
            var catalog=AssetDatabase.LoadAssetAtPath<ArchiveCatalog>("Assets/_Project/Resources/Codex/ArchiveCatalog.asset");
            var a=Log("03_OfficeLedger","office-ledger","The Unfinished Ledger","CORPORATE OFFICE / RECEPTION",
                "The last entry is unfinished. A ring of soot marks the place where the clerk kept a small lamp.\n\nNames were entered, drawers were sealed, and the office carried on as though the world outside had not already ended. Someone left a final note between the pages: leave enough light for the return journey.",
                "A Flare spends one whole ember. Keep a reserve before exploring the darker end of the office.");
            var b=Log("04_OfficeMargin","office-margin","A Note Above the Dust","CORPORATE OFFICE / UPPER FILES",
                "Most of the cabinets are empty. Above the dust line, one folder has survived.\n\nIts margins are filled with small observations: the sound of a door settling, the shape of a gap, the route back to a familiar lamp. Whoever wrote this learned the office by the pieces the darkness could not hide.",
                "Scout the landing before jumping down. Recovered records remain available after a chamber reset.");
            catalog.Logs=catalog.Logs.Concat(new[]{a,b}).GroupBy(l=>l.Id).Select(g=>g.First()).ToArray();EditorUtility.SetDirty(catalog);
            var group=new GameObject("Office Archive Points");SceneManager.MoveGameObjectToScene(group,scene);
            Point(group,a,new Vector3(-1.7f,-2.74f,0));Point(group,b,new Vector3(26f,-2.74f,0));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
        static ArchiveLog Log(string file,string id,string title,string location,string text,string clue)
        {
            string path="Assets/_Project/Resources/Codex/"+file+".asset";var log=AssetDatabase.LoadAssetAtPath<ArchiveLog>(path);if(log!=null)return log;
            log=ScriptableObject.CreateInstance<ArchiveLog>();log.Id=id;log.Title=title;log.Location=location;log.Text=text;log.RoomClue=clue;AssetDatabase.CreateAsset(log,path);return log;
        }
        static void Point(GameObject parent,ArchiveLog log,Vector3 pos)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Codex/ArchiveTerminal.prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.scene);go.name="Office record - "+log.Title;go.transform.SetParent(parent.transform,false);go.transform.position=pos;
            var whisper=go.GetComponent<DiegeticWhisper>();whisper.ArchiveLog=log;whisper.TriggerRadius=1.55f;whisper.ActionPrompt="READ ARCHIVE RECORD";
            PrefabUtility.RecordPrefabInstancePropertyModifications(whisper);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
    }
}
