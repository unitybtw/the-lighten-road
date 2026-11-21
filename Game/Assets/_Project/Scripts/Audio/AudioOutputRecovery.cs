using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TheLightenRoad.Audio
{
    /// <summary>
    /// Detects macOS CoreAudio default-device switches or detached Bluetooth output streams
    /// (where Unity's FMOD driver stops feeding the active hardware output) and triggers a
    /// guarded AudioSettings.Reset so BackgroundMusicManager, SoundEffectManager, and narration
    /// immediately re-attach and resume on the active device.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioOutputRecovery : MonoBehaviour
    {
        private static AudioOutputRecovery instance;
        private static bool isApplicationQuitting;

        public static event Action OnAudioOutputReset;

        private const float PollIntervalSeconds = 1.25f;
        private const float ResetCooldownSeconds = 4.0f;
        private const int MaxConsecutiveStaleResets = 2;

        private uint lastDefaultDeviceId;
        private float nextPollTime;
        private float nextAllowedResetTime;
        private int consecutiveStaleResets;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            isApplicationQuitting = false;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureActive()
        {
            if (isApplicationQuitting || Application.isBatchMode) return;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (instance != null) return;
            instance = FindAnyObjectByType<AudioOutputRecovery>();
            if (instance == null)
            {
                var go = new GameObject("Audio Output Recovery");
                instance = go.AddComponent<AudioOutputRecovery>();
                DontDestroyOnLoad(go);
            }
#endif
        }

        private void Awake()
        {
            isApplicationQuitting = false;
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            TryReadDefaultOutputState(out lastDefaultDeviceId, out _);
#endif
            nextPollTime = Time.unscaledTime + 0.35f;
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                nextPollTime = Mathf.Min(nextPollTime, Time.unscaledTime + 0.15f);
            }
        }

        private void Update()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (Application.isBatchMode || Time.unscaledTime < nextPollTime) return;
            nextPollTime = Time.unscaledTime + PollIntervalSeconds;
            CheckAndRecoverOutput();
#endif
        }

        public bool CheckAndRecoverOutput()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (Application.isBatchMode || isApplicationQuitting) return false;
            if (AudioListener.pause || AudioListener.volume <= 0.001f) return false;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.audioMasterMute) return false;
#endif
            if (!TryReadDefaultOutputState(out uint defaultDeviceId, out bool isRunning) || defaultDeviceId == 0)
            {
                return false;
            }

            if (lastDefaultDeviceId != 0 && defaultDeviceId != lastDefaultDeviceId)
            {
                lastDefaultDeviceId = defaultDeviceId;
                consecutiveStaleResets = 0;
                return TriggerReset("Default macOS audio output device changed.");
            }

            lastDefaultDeviceId = defaultDeviceId;

            if (isRunning)
            {
                consecutiveStaleResets = 0;
                return false;
            }

            if (consecutiveStaleResets < MaxConsecutiveStaleResets && HasActivePlaybackWithListener())
            {
                if (TriggerReset("macOS default audio output stream was idle while game audio was playing."))
                {
                    consecutiveStaleResets++;
                    return true;
                }
            }
#endif
            return false;
        }

        private bool TriggerReset(string reason)
        {
            if (Time.unscaledTime < nextAllowedResetTime) return false;
            nextAllowedResetTime = Time.unscaledTime + ResetCooldownSeconds;

            var config = AudioSettings.GetConfiguration();
            bool ok = AudioSettings.Reset(config);
            if (ok)
            {
                Debug.Log($"[AudioOutputRecovery] Re-initialized Unity audio output ({reason})");
                OnAudioOutputReset?.Invoke();
            }
            return ok;
        }

        private static bool HasActivePlaybackWithListener()
        {
            bool hasActiveVoice = false;
            var bgm = BackgroundMusicManager.Instance;
            if (bgm != null && bgm.IsPlaying && bgm.CurrentVolume > 0.001f)
            {
                hasActiveVoice = true;
            }
            else if (SoundEffectManager.HasInstance && SoundEffectManager.Instance.HasAnyPlayingVoice())
            {
                hasActiveVoice = true;
            }

            if (!hasActiveVoice) return false;

            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string CoreAudioLib = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
        private const uint AudioObjectSystemObject = 1;
        private const uint SelectorDefaultOutputDevice = 0x644F7574; // 'dOut'
        private const uint SelectorDeviceIsRunningSomewhere = 0x676F6E65; // 'gone'
        private const uint ScopeGlobal = 0x676C6F62; // 'glob'

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioObjectPropertyAddress
        {
            public uint mSelector;
            public uint mScope;
            public uint mElement;
        }

        [DllImport(CoreAudioLib)]
        private static extern int AudioObjectGetPropertyData(
            uint inObjectID,
            ref AudioObjectPropertyAddress inAddress,
            uint inQualifierDataSize,
            IntPtr inQualifierData,
            ref uint ioDataSize,
            out uint outData);

        private static bool TryReadDefaultOutputState(out uint deviceId, out bool isRunning)
        {
            deviceId = 0;
            isRunning = false;
            try
            {
                var devAddr = new AudioObjectPropertyAddress
                {
                    mSelector = SelectorDefaultOutputDevice,
                    mScope = ScopeGlobal,
                    mElement = 0
                };
                uint size = sizeof(uint);
                int status = AudioObjectGetPropertyData(AudioObjectSystemObject, ref devAddr, 0, IntPtr.Zero, ref size, out deviceId);
                if (status != 0 || deviceId == 0)
                {
                    deviceId = 0;
                    return false;
                }

                var runAddr = new AudioObjectPropertyAddress
                {
                    mSelector = SelectorDeviceIsRunningSomewhere,
                    mScope = ScopeGlobal,
                    mElement = 0
                };
                uint runSize = sizeof(uint);
                int runStatus = AudioObjectGetPropertyData(deviceId, ref runAddr, 0, IntPtr.Zero, ref runSize, out uint runningVal);
                isRunning = runStatus == 0 && runningVal != 0;
                return true;
            }
            catch
            {
                deviceId = 0;
                isRunning = false;
                return false;
            }
        }
#endif
    }
}
