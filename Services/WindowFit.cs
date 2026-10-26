using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcadeStick.Services
{
    // [SECTION: Window Fit]
    // Keeps a sub-window inside the work area (the screen minus the taskbar) of the monitor it is on. Window sizes are in
    // DIPs, so at 125% and above on a 1080p screen the taller windows (the Theme Builder's 885) ran past the bottom and cut
    // off their buttons. MaxWidth / MaxHeight cap the window to the work area (each window's content row is star-sized and
    // its tab pages scroll, so the buttons row stays visible), and once shown the window is moved back inside the work area
    // if it starts partly off it. Re-applied on DpiChanged, when the window moves to a monitor with another scale.
    public static class WindowFit
    {
        // Does the hookup: call once from a window's constructor, after InitializeComponent
        public static void KeepInWorkArea(Window window)
        {
            window.SourceInitialized += (s, e) => ApplyMaxSize(window);
            window.Loaded += (s, e) => window.Dispatcher.BeginInvoke(new Action(() => MoveIntoWorkArea(window)), DispatcherPriority.Loaded);
            window.DpiChanged += (s, e) => window.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyMaxSize(window);
                MoveIntoWorkArea(window);
            }));
        }

        // Does the work-area lookup in the window's own DIPs (physical pixels divided by its current DPI scale)
        private static bool TryGetWorkAreaDips(Window window, out Rect workArea)
        {
            workArea = Rect.Empty;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (!MonitorBounds.TryGetPhysicalWorkArea(hwnd, out int left, out int top, out int width, out int height)) return false;

            var dpi = VisualTreeHelper.GetDpi(window);
            workArea = new Rect(left / dpi.DpiScaleX, top / dpi.DpiScaleY, width / dpi.DpiScaleX, height / dpi.DpiScaleY);
            return true;
        }

        // Does the size cap: the window can be no wider or taller than its monitor's work area
        private static void ApplyMaxSize(Window window)
        {
            if (!TryGetWorkAreaDips(window, out Rect workArea)) return;
            window.MaxWidth = workArea.Width;
            window.MaxHeight = workArea.Height;
        }

        // Does the position fix: moves a normal (not maximized or minimized) window so it lies fully inside the work area
        private static void MoveIntoWorkArea(Window window)
        {
            if (window.WindowState != WindowState.Normal || !TryGetWorkAreaDips(window, out Rect workArea)) return;

            double left = Math.Max(workArea.Left, Math.Min(window.Left, workArea.Right - window.ActualWidth));
            double top = Math.Max(workArea.Top, Math.Min(window.Top, workArea.Bottom - window.ActualHeight));
            if (left != window.Left) window.Left = left;
            if (top != window.Top) window.Top = top;
        }
    }
    // [END SECTION: Window Fit]
}
