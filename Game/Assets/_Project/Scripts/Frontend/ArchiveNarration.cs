using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TheLightenRoad.Codex;
using UnityEngine;
using UnityEngine.Networking;

namespace TheLightenRoad.Frontend
{
    /// <summary>Offline, serial background synthesis with content-addressed WAV caching.</summary>
    public sealed class ArchiveNarration : MonoBehaviour
    {
        public const string Profile="kokoro-fp16-george-v2";
        private readonly Dictionary<string,AudioClip> ready=new Dictionary<string,AudioClip>();
        private readonly HashSet<string> pending=new HashSet<string>();
        private readonly HashSet<string> failed=new HashSet<string>();
        private readonly Queue<string> queue=new Queue<string>();
        private readonly Dictionary<string,string> textByKey=new Dictionary<string,string>();
        private bool running;
        private Process worker;
        private string requestFile;
        public static string SpokenText(ArchiveLog log)=>Regex.Replace((log.Title??"").Trim().TrimEnd('.','!','?')+". "+(log.Text??"").Trim(),@"\s+"," ").Trim();
        public static string CacheKey(string text)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Profile+"\n"+text))).Replace("-","").ToLowerInvariant();
        }
        public AudioClip GetReadyClip(ArchiveLog log)
        {
            if(log==null)return null;if(log.Audio!=null)return log.Audio;
            return ready.TryGetValue(CacheKey(SpokenText(log)),out var clip)?clip:null;
        }
        public bool IsPending(ArchiveLog log)=>log!=null && pending.Contains(CacheKey(SpokenText(log)));
        public void Prepare(ArchiveLog log)
        {
            if(log==null || log.Audio!=null)return;
            string text=SpokenText(log),key=CacheKey(text);
            if(ready.ContainsKey(key)||pending.Contains(key)||failed.Contains(key))return;
            pending.Add(key);textByKey[key]=text;queue.Enqueue(key);
            if(!running){running=true;StartCoroutine(Drain());}
        }
        private IEnumerator Drain()
        {
            while(queue.Count>0)
            {
                string key=queue.Dequeue();yield return Load(key,textByKey[key]);
                pending.Remove(key);textByKey.Remove(key);
                if(!ready.ContainsKey(key))failed.Add(key);
            }
            running=false;
        }
        private IEnumerator Load(string key,string text)
        {
            string path=Path.Combine(Application.persistentDataPath,"Narration",key+".wav");
            string bundled=Path.Combine(Application.streamingAssetsPath,"SpeechCache",key+".wav");
            if(!File.Exists(path) && File.Exists(bundled))path=bundled;
            if(!File.Exists(path))
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                string platform=Application.platform==RuntimePlatform.WindowsEditor || Application.platform==RuntimePlatform.WindowsPlayer?"runtime-windows-x64":"runtime-macos-arm64";
                string name=platform.StartsWith("runtime-windows")?"archive-voice-worker.exe":"archive-voice-worker";
                string models=Path.Combine(Application.streamingAssetsPath,"Kokoro");
                string exe=Path.Combine(models,platform,"archive-voice-worker",name);
                if(!File.Exists(exe)||!File.Exists(Path.Combine(models,"model.onnx"))||text.Length>10000)yield break;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                requestFile=Path.Combine(Path.GetDirectoryName(path),Guid.NewGuid().ToString("N")+".json");
                File.WriteAllText(requestFile,JsonUtility.ToJson(new Request{text=text,output=path}),new UTF8Encoding(false));
                string error=null;
                try
                {
                    worker=Process.Start(new ProcessStartInfo{FileName=exe,Arguments="--models "+Quote(models)+" --request "+Quote(requestFile),UseShellExecute=false,CreateNoWindow=true});
                }
                catch(Exception e){error=e.Message;}
                if(error!=null){UnityEngine.Debug.LogWarning("Narration worker could not start: "+error);CleanWorker();yield break;}
                float deadline=Time.realtimeSinceStartup+90;
                while(worker!=null && !worker.HasExited && Time.realtimeSinceStartup<deadline)yield return null;
                bool success=worker!=null && worker.HasExited && worker.ExitCode==0;
                CleanWorker();if(!success || !File.Exists(path))yield break;
#else
                yield break;
#endif
            }
            using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.WAV))
            {
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){UnityEngine.Debug.LogWarning("Narration audio could not load: "+request.error);yield break;}
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(clip!=null){clip.name="Archive narration "+key;ready[key]=clip;}
            }
        }
        private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
        [Serializable] private sealed class Request{public string text;public string output;}
        private void CleanWorker()
        {
            if(worker!=null){try{if(!worker.HasExited)worker.Kill();}catch(InvalidOperationException){}worker.Dispose();worker=null;}
            if(requestFile!=null){try{File.Delete(requestFile);}catch(IOException){}requestFile=null;}
        }
        private void OnDisable(){StopAllCoroutines();CleanWorker();pending.Clear();queue.Clear();textByKey.Clear();running=false;}
        private void OnDestroy(){StopAllCoroutines();CleanWorker();foreach(var clip in ready.Values)if(clip!=null)Destroy(clip);ready.Clear();}
    }
}
