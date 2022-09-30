using System;
using System.CodeDom;
using System.Runtime.InteropServices;
using System.Text;

namespace PIDViewer
{
    using static Win32API;

    public class Win32API
    {
        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        static StringBuilder stringBuilder = new StringBuilder(1024);
        public static string GetWindowTitle(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd) + 1;
            stringBuilder.Clear();
            GetWindowText(hWnd, stringBuilder, length);
            return stringBuilder.ToString();
        }

        // OS >= Windows 10 1903
        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
        public static extern bool ShouldSystemUseDarkMode();
    }

    public class EventHook
    {
        WinEventDelegate winEvent;

        public EventHook(EventHookID eventMin, WinEventDelegate callback) : this(eventMin, eventMin, callback) { }

        public EventHook(EventHookID eventMin, EventHookID eventMax, WinEventDelegate callback)
        {
            winEvent = callback;
            _ = SetWinEventHook((uint)eventMin, (uint)eventMax, IntPtr.Zero, winEvent, 0, 0, (uint)EventHookID.WINEVENT_OUTOFCONTEXT);
        }
    }

    public enum EventHookID : uint
    {
        WINEVENT_OUTOFCONTEXT = 0,
        EVENT_SYSTEM_FOREGROUND = 0x0003,
        EVENT_OBJECT_NAMECHANGE = 0x800C,
        EVENT_SYSTEM_CAPTURESTART = 0x0008,
    }
}
