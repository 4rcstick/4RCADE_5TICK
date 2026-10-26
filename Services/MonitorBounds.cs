using System;
using System.Runtime.InteropServices;

namespace ArcadeStick.Services
{
    // [SECTION: Monitor Bounds]
    // Finds the monitor a window is on and its full bounds or its work area. With the Per-Monitor V2 manifest both are in
    // physical pixels on every monitor. Used by Smart fullscreen (bounds) and WindowFit (work area); WinForms' Screen is
    // avoided so the self-contained build does not pull in WinForms.
    public static class MonitorBounds
    {
        private const uint MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        // Does the monitor lookup: the window's nearest monitor handle and its full size in physical pixels. False on failure.
        public static bool TryGetPhysicalSize(IntPtr hwnd, out IntPtr monitor, out int width, out int height)
        {
            width = 0;
            height = 0;
            monitor = hwnd == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return false;

            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return false;

            width = info.Monitor.Right - info.Monitor.Left;
            height = info.Monitor.Bottom - info.Monitor.Top;
            return width > 0 && height > 0;
        }

        // Does the work-area lookup: the window's nearest monitor work area (the screen minus the taskbar) in physical
        // pixels, as a top-left corner and size in screen coordinates. False on failure.
        public static bool TryGetPhysicalWorkArea(IntPtr hwnd, out int left, out int top, out int width, out int height)
        {
            left = 0;
            top = 0;
            width = 0;
            height = 0;
            IntPtr monitor = hwnd == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return false;

            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return false;

            left = info.Work.Left;
            top = info.Work.Top;
            width = info.Work.Right - info.Work.Left;
            height = info.Work.Bottom - info.Work.Top;
            return width > 0 && height > 0;
        }
    }
    // [END SECTION: Monitor Bounds]
}
