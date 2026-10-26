using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;

namespace TheLightenRoad.UI
{
    /// <summary>Dedicated macOS speaker stream using system IOKit, independent of rendering cadence.</summary>
    public static class MacDualSenseSpeakerTransport
    {
        public sealed class Playback
        {
            internal volatile bool Cancelled;
            internal byte[] Frames;
            internal byte Volume;
            internal Target Target;
            public volatile bool Finished, Completed;
            public volatile int SuccessfulReports, FailedReports, LastErrorReportId, Result;
            public void Cancel() { Cancelled = true; Wake.Set(); }
        }
        internal sealed class Target { public int Product, DeviceId; public string Serial; }
        private static readonly AutoResetEvent Wake = new AutoResetEvent(false);
        public static int LastNativeError { get; private set; }
        public const double DrainSeconds = .35;
        public const double MaxPlaybackSeconds = 3.5;
        // A synchronous macOS HID write can take longer than one codec interval. Do not
        // classify the transport's own blocking time as a rendering stall or discard the cue.
        public static double NextReportTime(double scheduled, double writeFinished) =>
            Math.Max(scheduled + DualSenseSpeakerProtocol.LegacyReportSeconds, writeFinished);
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string Hid = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const string Cf = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private static IntPtr manager, device;
        private static int deviceId = -1;
        private static readonly object Gate = new object();
        private static readonly System.Collections.Generic.Dictionary<int, byte> Counters = new System.Collections.Generic.Dictionary<int, byte>();
        private static Playback pending, active;
        private static Thread worker;
        public static bool TryPlay(Gamepad pad, byte[] frames, byte volume, out Playback playback)
        {
            playback = null;
            if (!DualSenseBluetoothRumble.IsBluetooth(pad)) return false;
            var target = new Target { Product = HID.HIDDeviceDescriptor.FromJson(pad.description.capabilities).productId,
                DeviceId = pad.deviceId, Serial = pad.description.serial };
            playback = new Playback { Target = target, Frames = frames, Volume = volume };
            lock (Gate)
            {
                if (pending != null) { pending.Cancelled = true; pending.Finished = true; }
                if (active != null) active.Cancelled = true;
                pending = playback;
                if (worker == null || !worker.IsAlive)
                {
                    worker = new Thread(Work) { IsBackground = true, Name = "DualSense speaker" };
                    worker.Start();
                }
            }
            Wake.Set();
            return true;
        }
        public static void StopAll()
        {
            lock (Gate)
            {
                if (active != null) active.Cancelled = true;
                if (pending != null) { pending.Cancelled = true; pending.Finished = true; pending = null; }
            }
            Wake.Set();
        }
        private static void Work()
        {
            // Only this worker owns IOKit handles. It never calls Unity, InputSystem or PlayerPrefs APIs.
            while (true)
            {
                Playback job;
                lock (Gate)
                {
                    job = pending; pending = null; active = job;
                    if (job == null) { worker = null; return; }
                }
                try { Stream(job); }
                catch (Exception) { job.Result = -1; job.FailedReports++; }
                finally
                {
                    try
                    {
                        if (device != IntPtr.Zero)
                        {
                            var mute = DualSenseSpeakerProtocol.Setup(DualSenseBluetoothRumble.NextSequence(job.Target.DeviceId), 0);
                            LastNativeError = IOHIDDeviceSetReport(device, 1, (IntPtr)mute[0], mute, (IntPtr)mute.Length);
                            if(LastNativeError!=0){job.Result=-1;job.FailedReports++;job.Completed=false;}
                        }
                    }
                    finally { Release(); job.Finished = true; lock (Gate) { if (active == job) active = null; } }
                }
            }
        }
        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        private static bool Wait(Playback job, double until)
        {
            while (!job.Cancelled)
            {
                double remaining = until - Now;
                if (remaining <= 0) return true;
                Wake.WaitOne(Math.Max(1, (int)Math.Ceiling(remaining * 1000)));
            }
            return false;
        }
        private static bool Write(Playback job, byte[] report)
        {
            for (int attempt = 0; attempt < 3 && !job.Cancelled; attempt++)
            {
                LastNativeError = IOHIDDeviceSetReport(device, 1, (IntPtr)report[0], report, (IntPtr)report.Length);
                job.Result = LastNativeError == 0 ? 1 : -1;
                if (LastNativeError == 0) { job.SuccessfulReports++; return true; }
                job.FailedReports++; job.LastErrorReportId = report[0];
                if (!Wait(job, Now + .02)) return false;
            }
            return false;
        }
        private static void Stream(Playback job)
        {
            if (job.Cancelled) return;
            if (!Acquire(job.Target)) { job.Result = -1; job.FailedReports++; return; }
            // Match the initialization sequence verified on the physical Bluetooth controller.
            for(int i=0;i<3;i++)
                if (!Write(job, DualSenseSpeakerProtocol.Setup(DualSenseBluetoothRumble.NextSequence(job.Target.DeviceId), job.Volume)) ||
                    !Wait(job, Now + .02)) return;
            Counters.TryGetValue(job.Target.DeviceId, out byte counter);
            double next = Now;
            double deadline = Now + MaxPlaybackSeconds;
            for (int offset = 0; offset < job.Frames.Length; offset += 200)
            {
                if (!Wait(job, next)) return;
                if (Now > deadline) { job.Result = -1; job.FailedReports++; return; }
                if (!Write(job, DualSenseSpeakerProtocol.LegacyAudio(DualSenseBluetoothRumble.NextSequence(job.Target.DeviceId), counter, job.Frames, offset))) return;
                counter = unchecked((byte)(counter + 1));
                Counters[job.Target.DeviceId] = counter;
                next = NextReportTime(next, Now);
            }
            // Native acceptance is not playback completion. Allow the decoder/output queue to drain.
            job.Completed = Wait(job, Math.Max(next, Now + DrainSeconds));
        }
        private static bool Acquire(Target target)
        {
            IntPtr data = IntPtr.Zero, matching = IntPtr.Zero, devices = IntPtr.Zero;
            IntPtr vendorKey = IntPtr.Zero, productKey = IntPtr.Zero, transportKey = IntPtr.Zero, serialKey = IntPtr.Zero;
            try
            {
                int product = target.Product;
                // Parsing a local property list gives a CFDictionary with Apple's own retain/equality callbacks.
                byte[] xml = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>VendorID</key><integer>1356</integer><key>ProductID</key><integer>" + product + "</integer></dict></plist>");
                data = CFDataCreate(IntPtr.Zero, xml, (IntPtr)xml.Length);
                matching = CFPropertyListCreateWithData(IntPtr.Zero, data, 0, out _, out IntPtr error);
                if (error != IntPtr.Zero) CFRelease(error);
                if (matching == IntPtr.Zero) return false;
                manager = IOHIDManagerCreate(IntPtr.Zero, 0);
                if (manager == IntPtr.Zero) return false;
                IOHIDManagerSetDeviceMatching(manager, matching);
                LastNativeError = IOHIDManagerOpen(manager, 0);
                if (LastNativeError != 0) return false;
                devices = IOHIDManagerCopyDevices(manager);
                if (devices == IntPtr.Zero) return false;
                var count = CFSetGetCount(devices).ToInt32();
                if (count < 1 || count > 64) return false;
                var list = new IntPtr[count];
                CFSetGetValues(devices, list);
                vendorKey = Key("VendorID"); productKey = Key("ProductID");
                transportKey = Key("Transport"); serialKey = Key("SerialNumber");
                int matches = 0;
                IntPtr selected = IntPtr.Zero;
                foreach (IntPtr candidate in list)
                {
                    if (Number(IOHIDDeviceGetProperty(candidate, vendorKey)) != 1356 ||
                        Number(IOHIDDeviceGetProperty(candidate, productKey)) != product ||
                        Text(IOHIDDeviceGetProperty(candidate, transportKey)).IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string serial = target.Serial;
                    if (!string.IsNullOrEmpty(serial) && !string.Equals(serial, Text(IOHIDDeviceGetProperty(candidate, serialKey)), StringComparison.OrdinalIgnoreCase)) continue;
                    selected = candidate;
                    matches++;
                }
                // Without an exact serial match, ambiguity must never send audio to somebody else's pad.
                if (matches != 1) return false;
                device = CFRetain(selected);
                LastNativeError = IOHIDDeviceOpen(device, 0);
                if (LastNativeError != 0) { CFRelease(device); device = IntPtr.Zero; return false; }
                deviceId = target.DeviceId;
                return true;
            }
            catch (Exception) { return false; }
            finally
            {
                foreach (IntPtr value in new[]{data, matching, devices, vendorKey, productKey, transportKey, serialKey})
                    if (value != IntPtr.Zero) CFRelease(value);
                if (deviceId != target.DeviceId) Release();
            }
        }
        private static IntPtr Key(string value) => CFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);
        private static int Number(IntPtr value)
        {
            if (value == IntPtr.Zero) return 0;
            CFNumberGetValue(value, 3, out int number);
            return number;
        }
        private static string Text(IntPtr value)
        {
            if (value == IntPtr.Zero) return "";
            var buffer = new StringBuilder(256);
            return CFStringGetCString(value, buffer, (IntPtr)256, 0x08000100) ? buffer.ToString() : "";
        }
        private static void Release()
        {
            if (device != IntPtr.Zero) { IOHIDDeviceClose(device, 0); CFRelease(device); device = IntPtr.Zero; }
            if (manager != IntPtr.Zero) { IOHIDManagerClose(manager, 0); CFRelease(manager); manager = IntPtr.Zero; }
            deviceId = -1;
        }
        [DllImport(Hid)] private static extern IntPtr IOHIDManagerCreate(IntPtr allocator, int options);
        [DllImport(Hid)] private static extern void IOHIDManagerSetDeviceMatching(IntPtr manager, IntPtr dictionary);
        [DllImport(Hid)] private static extern int IOHIDManagerOpen(IntPtr manager, int options);
        [DllImport(Hid)] private static extern int IOHIDManagerClose(IntPtr manager, int options);
        [DllImport(Hid)] private static extern IntPtr IOHIDManagerCopyDevices(IntPtr manager);
        [DllImport(Hid)] private static extern IntPtr IOHIDDeviceGetProperty(IntPtr device, IntPtr key);
        [DllImport(Hid)] private static extern int IOHIDDeviceOpen(IntPtr device, int options);
        [DllImport(Hid)] private static extern int IOHIDDeviceClose(IntPtr device, int options);
        [DllImport(Hid)] private static extern int IOHIDDeviceSetReport(IntPtr device, int type, IntPtr reportId, byte[] data, IntPtr length);
        [DllImport(Cf)] private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, IntPtr length);
        [DllImport(Cf)] private static extern IntPtr CFPropertyListCreateWithData(IntPtr allocator, IntPtr data, int options, out IntPtr format, out IntPtr error);
        [DllImport(Cf)] private static extern IntPtr CFSetGetCount(IntPtr set);
        [DllImport(Cf)] private static extern void CFSetGetValues(IntPtr set, [Out] IntPtr[] values);
        [DllImport(Cf)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string text, uint encoding);
        [DllImport(Cf)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CFStringGetCString(IntPtr text, StringBuilder buffer, IntPtr size, uint encoding);
        [DllImport(Cf)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CFNumberGetValue(IntPtr number, int type, out int value);
        [DllImport(Cf)] private static extern IntPtr CFRetain(IntPtr value);
        [DllImport(Cf)] private static extern void CFRelease(IntPtr value);
#else
        public static bool TryPlay(Gamepad pad, byte[] frames, byte volume, out Playback playback) { playback = null; return false; }
        public static void StopAll() { }
#endif
    }
}
