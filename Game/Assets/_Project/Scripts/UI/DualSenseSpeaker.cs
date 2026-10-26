using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.UI
{
    public enum SpeakerCue { Ignite, Extinguish, Pickup, Break, Preview }

    /// <summary>Short, quiet controller-speaker details. Normal game audio remains the fallback.</summary>
    public sealed class DualSenseSpeaker : MonoBehaviour
    {
        private static DualSenseSpeaker instance;
        private Gamepad recipient;
        private MacDualSenseSpeakerTransport.Playback nativePlayback;
        private byte[] frames;
        private int offset, setupCount;
        private byte counter;
        private double nextReport, failedUntil;
        private bool suspended, allowPaused;
        public static string LastStatus { get; private set; } = "Ready";
        public static int RequestCount { get; private set; }
        private int failedDevice = -1, consecutiveFailures;
        public static int SuccessfulReports { get; private set; }
        public static int FailedReports { get; private set; }
        public static byte LastErrorReportId { get; private set; }
        public static bool LastPlaybackComplete { get; private set; }
        public static long LastCommandResult { get; private set; }
        public static bool IsPlaying => instance != null && instance.recipient != null;
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("TLR.Menu.ControllerSpeaker", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("TLR.Menu.ControllerSpeaker", value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) Stop();
                else if (instance != null) instance.failedDevice = -1;
            }
        }
        public static float OutputGain
        {
            get
            {
                float value = PlayerPrefs.GetFloat("TLR.Menu.Master", .85f) * PlayerPrefs.GetFloat("TLR.Menu.Effects", 1f);
                return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            }
        }
        // A perceptual curve keeps normal mixer levels out of the controller's near-silent
        // register range. Hard mute remains exact; source peaks are limited offline.
        public static byte NativeVolume => OutputGain <= 0f ? (byte)0 : (byte)Mathf.Clamp(
            Mathf.RoundToInt(DualSenseSpeakerProtocol.MaxSpeakerVolume + 20f * Mathf.Log10(OutputGain)),
            0, DualSenseSpeakerProtocol.MaxSpeakerVolume);
        public static void Play(SpeakerCue cue) => TryPlay(cue);
        public static bool TryPlay(SpeakerCue cue, bool previewWhilePaused = false)
        {
            RequestCount++;
            if (!Application.isPlaying) return Reject("Start the game to test the speaker.");
            if (!Enabled) return Reject("Turn on DualSense Speaker first.");
            if (Time.timeScale <= 0f && !(previewWhilePaused && cue == SpeakerCue.Preview)) return Reject("Game is paused.");
            if (OutputGain <= 0f) return Reject("Master Volume or Sound Effects is muted.");
            var pad = ControllerGlyphService.GetActiveGamepad();
            if (pad == null || !pad.added) return Reject("Connect your DualSense using Bluetooth.");
            if (!DualSenseBluetoothRumble.IsBluetooth(pad)) return Reject("The active controller is not a Bluetooth DualSense.");
            if (instance == null)
            {
                var go = new GameObject("DualSense Speaker");
                instance = go.AddComponent<DualSenseSpeaker>();
                DontDestroyOnLoad(go);
            }
            if (!Application.isFocused || instance.suspended) return Reject("Focus the game window and try again.");
            if (instance.failedDevice == pad.deviceId && Time.realtimeSinceStartupAsDouble < instance.failedUntil)
                return Reject("Controller is busy. Try again in a moment.");
            // Do not accumulate a delayed queue. Pickup never interrupts a more important lantern cue.
            if (instance.recipient != null && cue == SpeakerCue.Pickup) return Reject("Another controller sound is playing.");
            #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            const string resourcePath = "Audio/ControllerSpeaker/Mac/";
#else
            const string resourcePath = "Audio/ControllerSpeaker/";
#endif
            var asset = Resources.Load<TextAsset>(resourcePath + cue);
            if (asset == null || asset.bytes.Length == 0 || asset.bytes.Length % 400 != 0) return Reject("Controller sound asset is unavailable.");
            instance.Halt();
            instance.recipient = pad;
            instance.allowPaused = previewWhilePaused && cue == SpeakerCue.Preview;
            instance.frames = asset.bytes;
            instance.offset = instance.setupCount = instance.consecutiveFailures = 0;
            SuccessfulReports = FailedReports = 0;
            LastErrorReportId = 0;
            LastPlaybackComplete = false;
            instance.counter = 0;
            instance.nextReport = Time.realtimeSinceStartupAsDouble;
            MacDualSenseSpeakerTransport.TryPlay(pad, instance.frames, NativeVolume, out instance.nativePlayback);
            LastStatus = "Playing controller sound...";
            return true;
        }
        private static bool Reject(string reason) { LastStatus = reason; LastPlaybackComplete=false; return false; }
        public static void Stop() { if (instance != null) instance.Halt(); }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            SceneManager.activeSceneChanged += SceneChanged;
            InputSystem.onDeviceChange += DeviceChanged;
        }
        private void Update()
        {
            if (recipient == null) return;
            if (!Enabled || !recipient.added || !Application.isFocused || suspended || (Time.timeScale <= 0f && !allowPaused) ||
                OutputGain <= 0f || ControllerGlyphService.GetActiveGamepad() != recipient) { Halt(); return; }
            if (nativePlayback != null)
            {
                LastCommandResult = nativePlayback.Result;
                SuccessfulReports = nativePlayback.SuccessfulReports;
                FailedReports = nativePlayback.FailedReports;
                LastErrorReportId = (byte)nativePlayback.LastErrorReportId;
                if (nativePlayback.Finished)
                {
                    LastPlaybackComplete = nativePlayback.Completed;
                    LastStatus = nativePlayback.Completed ? "Sound sent. Listen to your controller." :
                        "Controller audio was interrupted. Reconnect and test again.";
                    nativePlayback = null; recipient = null; frames = null;
                }
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextReport) return;
            // A long stall invalidates the small hardware buffer. Stop instead of flooding stale audio.
            if (now - nextReport > .15) { Halt(); return; }
            if (setupCount < 3)
            {
                if (!Send(DualSenseSpeakerProtocol.Setup(Sequence(), NativeVolume))) return;
                setupCount++;
                nextReport = now + .02;
                return;
            }
            int sent = 0;
            while (now >= nextReport && offset < frames.Length && sent++ < 3)
            {
                byte nextCounter = unchecked((byte)(counter + 2));
                if (!Send(DualSenseSpeakerProtocol.Audio(Sequence(), nextCounter, frames, offset))) return;
                counter = nextCounter;
                offset += 400;
                nextReport += DualSenseSpeakerProtocol.ReportSeconds;
            }
            // Wait until the final packet has played before muting the hardware speaker.
            if (offset >= frames.Length && now >= nextReport) { LastPlaybackComplete = true; Halt(); LastStatus = "Sound sent. Listen to your controller."; }
        }
        private byte Sequence() => DualSenseBluetoothRumble.NextSequence(recipient.deviceId);
        private bool Send(byte[] report)
        {
            try { LastCommandResult = DualSenseSpeakerProtocol.Send(recipient, report); }
            catch (Exception) { LastCommandResult = -1; }
            if (LastCommandResult >= 0) { SuccessfulReports++; consecutiveFailures = 0; return true; }
            FailedReports++;
            LastErrorReportId = report[0];
            // macOS Bluetooth can temporarily reject a write while another output is in flight.
            // Retry this packet at most three times; never accumulate stale sounds.
            if (++consecutiveFailures < 3) nextReport = Time.realtimeSinceStartupAsDouble + .02;
            else { failedDevice = recipient.deviceId; failedUntil = Time.realtimeSinceStartupAsDouble + .5; Halt(); LastStatus = "Controller audio could not be delivered. Reconnect and test again."; }
            return false;
        }
        private void Halt()
        {
            var pad = recipient;
            if (recipient != null) LastStatus = "Controller sound stopped.";
            recipient = null;
            if (nativePlayback != null)
            {
                SuccessfulReports = nativePlayback.SuccessfulReports;
                FailedReports = nativePlayback.FailedReports;
                LastCommandResult = nativePlayback.Result;
                nativePlayback.Cancel();
                nativePlayback = null;
                frames = null;
                return;
            }
            frames = null;
            if (pad == null || !pad.added) return;
            try { DualSenseSpeakerProtocol.Send(pad, DualSenseSpeakerProtocol.Setup(DualSenseBluetoothRumble.NextSequence(pad.deviceId), 0)); }
            catch (Exception) { /* Disconnects and unsupported drivers leave normal game audio available. */ }
        }
        private void SceneChanged(Scene previous, Scene next) => Halt();
        private void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (device.deviceId == failedDevice && (change == InputDeviceChange.Reconnected || change == InputDeviceChange.Added)) failedDevice = -1;
            if (device == recipient && (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed ||
                change == InputDeviceChange.Disabled || change == InputDeviceChange.SoftReset || change == InputDeviceChange.HardReset)) Halt();
        }
        private void OnApplicationFocus(bool value) { if (!value) Halt(); }
        private void OnApplicationPause(bool value) { suspended = value; if (value) Halt(); }
        private void OnDisable() => Halt();
        private void OnApplicationQuit() { Halt(); MacDualSenseSpeakerTransport.StopAll(); }
        private void OnDestroy()
        {
            Halt();
            SceneManager.activeSceneChanged -= SceneChanged;
            InputSystem.onDeviceChange -= DeviceChanged;
            if (instance == this) instance = null;
        }
    }
}
