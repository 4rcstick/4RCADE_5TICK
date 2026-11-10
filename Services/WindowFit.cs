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

        // [SECTION: Workspace Window Placement]
        // Workspace windows (Options, Sorting, Theme Builder) have no owner, so CenterOwner no longer applies. Call once,
        // before the first Show: centers the window (its XAML Width / Height, capped to the work area) on the work area of
        // the monitor the anchor (the main window) is on, in the anchor's DIPs. Activating an open window never calls this,
        // and the window does not follow the anchor when it moves. KeepInWorkArea still corrects the window once shown
        // (mixed-DPI monitor setups are untested).
        public static void CenterOnMonitorOf(Window window, Window anchor)
        {
            if (double.IsNaN(window.Width) || double.IsNaN(window.Height) || !TryGetWorkAreaDips(anchor, out Rect workArea)) return;

            double width = Math.Min(window.Width, workArea.Width);
            double height = Math.Min(window.Height, workArea.Height);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = workArea.Left + ((workArea.Width - width) / 2);
            window.Top = workArea.Top + ((workArea.Height - height) / 2);
        }

        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        // Does the z-order move of a shown window to just below the anchor, without activating, moving or sizing it (used
        // when windows hidden for a game come back, so they never cover the main window)
        public static void PlaceBehind(Window window, Window anchor)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            IntPtr anchorHwnd = new WindowInteropHelper(anchor).Handle;
            if (hwnd == IntPtr.Zero || anchorHwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, anchorHwnd, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
        }
        // [END SECTION: Workspace Window Placement]
    }
    // [END SECTION: Window Fit]
}
