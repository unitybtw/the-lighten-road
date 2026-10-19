using System;
using TheLightenRoad.Player.Completion;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLightenRoad.Sirac.FurkanPrototype.Editor
{
    public static class CompletionSetup
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/ChamberExit.prefab";
        public const string ProfilePath = "Assets/_Project/Resources/Completion/FirstLightCompletion.asset";
        [MenuItem("Tools/The Lighten Road/Install tutorial completion")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode first.");
            var director=UnityEngine.Object.FindAnyObjectByType<FurkanLevelDirector>();
            if(director==null)throw new InvalidOperationException("Open FurkanTutorial first.");
            var profile=AssetDatabase.LoadAssetAtPath<ChamberCompletionProfile>(ProfilePath);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<ChamberCompletionProfile>();
                profile.Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Scenes/Sandbox/Sirac/TestChamber/ArchiveFont.asset");
                profile.CompletionSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Resources/Completion/ArchiveArrival.wav");
                AssetDatabase.CreateAsset(profile,ProfilePath);
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null)
            {
                var template=new GameObject("Chamber Exit",typeof(BoxCollider2D),typeof(ChamberCompletionSequence),typeof(ChamberExitTrigger));
                template.GetComponent<BoxCollider2D>().isTrigger=true;
                template.GetComponent<BoxCollider2D>().size=new Vector2(.55f,9f);
                template.GetComponent<ChamberCompletionSequence>().Profile=profile;
                prefab=PrefabUtility.SaveAsPrefabAsset(template,PrefabPath);
                UnityEngine.Object.DestroyImmediate(template);
            }
            if(director.Completion==null)
            {
                foreach(var trigger in director.GetComponentsInChildren<ArchiveTutorialTrigger>(true))
                    if(trigger.IsExit)Undo.DestroyObjectImmediate(trigger.gameObject);
                var exit=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(exit,"Install chamber completion");
                exit.transform.position=new Vector3(40.5f,4f);
                var sequence=exit.GetComponent<ChamberCompletionSequence>();
                sequence.DestinationScene="Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity";
                exit.GetComponent<ChamberExitTrigger>().Player=director.Player;
                UnityEventTools.AddPersistentListener(sequence.Started,director.Complete);
                director.Completion=sequence;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sequence);
                PrefabUtility.RecordPrefabInstancePropertyModifications(exit.GetComponent<ChamberExitTrigger>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(exit.transform);
                EditorUtility.SetDirty(director);
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
