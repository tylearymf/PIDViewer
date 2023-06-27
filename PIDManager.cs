using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace PIDViewer
{
    using static Win32API;

    internal class PIDManager
    {
        public static readonly PIDManager Instance = new PIDManager();

        public event Action<MyProcessInfo> OnForegroundWindowChanged;

        public MyProcessInfo ForegroundProcessInfo { get; private set; }

        Process currenProcess;
        List<EventHook> eventHooks;
        SynchronizationContext synchronizationContext;

        public PIDManager()
        {
            currenProcess = Process.GetCurrentProcess();
            synchronizationContext = SynchronizationContext.Current;

            eventHooks = new List<EventHook>();
            eventHooks.Add(new EventHook(EventHookID.EVENT_SYSTEM_FOREGROUND, new WinEventDelegate(OnForegroundChanged)));
            eventHooks.Add(new EventHook(EventHookID.EVENT_SYSTEM_CAPTURESTART, new WinEventDelegate(OnCaptureStart)));
            eventHooks.Add(new EventHook(EventHookID.EVENT_OBJECT_NAMECHANGE, new WinEventDelegate(OnWinNameChanged)));
        }

        public void KillForegroundWindow()
        {
            if (ForegroundProcessInfo.Kill())
            {
                UpdateInfo();
            }
            else
            {
                ForegroundProcessInfo.Reset();
                OnForegroundWindowChanged?.Invoke(ForegroundProcessInfo);
            }
        }

        void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            UpdateInfo();
        }

        void OnCaptureStart(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            UpdateInfo();
        }

        void OnWinNameChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            UpdateTitle();
        }

        MyProcessInfo GetForegroundProcessInfo(bool ignoreCurrent)
        {
            var hwnd = GetForegroundWindow();

            GetWindowThreadProcessId(hwnd, out var pid);
            if (ignoreCurrent && pid == currenProcess?.Id)
                return MyProcessInfo.Empty;

            var currentInfo = ForegroundProcessInfo;
            if (currentInfo != null && currentInfo.PID == pid)
                return currentInfo;

            return GetProcessInfo((int)pid);
        }

        public MyProcessInfo GetProcessInfo(int pid)
        {
            try
            {
                var process = pid == 0 ? null : Process.GetProcessById(pid);
                return new MyProcessInfo(process);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return MyProcessInfo.Empty;
            }
        }

        public void UpdateInfo()
        {
            var info = GetForegroundProcessInfo(true);
            if (info.PID == 0)
                return;

            ForegroundProcessInfo = info;
            SendChangedEvent();
        }

        public void UpdateTitle()
        {
            var info = ForegroundProcessInfo;
            if (info == null)
                return;

            if (info.UpdateTitle())
                SendChangedEvent();
        }

        public void SendChangedEvent(MyProcessInfo processInfo = null)
        {
            OnForegroundWindowChanged?.Invoke(processInfo ?? ForegroundProcessInfo);
        }

        public void PostUpdateInfo(MyProcessInfo processInfo)
        {
            synchronizationContext.Post(state => { SendChangedEvent(processInfo); }, null);
        }
    }

    public class MyProcessInfo
    {
        public static readonly MyProcessInfo Empty = new MyProcessInfo();

        public Process Process { set; get; }

        public int PID { set; get; }
        public string Title { set; get; }
        public string Name { set; get; }
        public string Version { set; get; }
        public string StartTime { set; get; }
        public string Argument { set; get; }

        public MyProcessInfo() { }

        public MyProcessInfo(Process process)
        {
            Process = process;

            try
            {
                PID = process?.Id ?? 0;
                Name = process?.ProcessName;
                Version = process?.MainModule?.FileVersionInfo?.ProductVersion;
                StartTime = process?.StartTime.ToString();

                UpdateTitle();

                if (string.IsNullOrEmpty(Argument) && PID != 0)
                {
                    Task.Factory.StartNew(() =>
                    {
                        Argument = process?.GetCommandLine();
                        PIDManager.Instance.PostUpdateInfo(this);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public bool UpdateTitle()
        {
            try
            {
                var process = Process;
                var title = process == null ? string.Empty : GetWindowTitle(process.MainWindowHandle);
                var hasChanged = Title != title;

                Title = title;

                return hasChanged;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        public void Reset()
        {
            PID = 0;
            Title = string.Empty;
            Name = string.Empty;
            Version = string.Empty;
            StartTime = string.Empty;
            Argument = string.Empty;
        }

        public bool Kill()
        {
            try
            {
                var process = Process;
                Process = null;

                if (process != null && !process.HasExited)
                {
                    process.Kill();

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return false;
        }

        public override string ToString()
        {
            return $"{PID} - {Name} - {Title} - {Argument}";
        }
    }

    static class Extensions
    {
        public static string GetCommandLine(this Process process)
        {
            try
            {
                if (process == null)
                    return string.Empty;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
                using (ManagementObjectCollection objects = searcher.Get())
                {
                    return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return string.Empty;
            }
        }
    }
}
