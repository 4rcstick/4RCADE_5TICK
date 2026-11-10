using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcadeStick.Services
{
    // Radius-aware scrollbar clearance (themes.md 3.27). An opted-in ScrollViewer (ScrollbarClearance.Enabled="True")
    // whose nearest framed host is a TabContentBorder (ThemedTabControlStyle: Global Border Radius and Border Size) gets a
    // Margin on its PART_VerticalScrollBar so the drawn track never crosses the frame's rounded bottom-right corner. Only
    // the bar moves: the ScrollViewer's own margin, size, viewport and scroll range never change, and the shared scrollbar
    // templates in App.xaml are not edited. Nothing in MainWindow opts in. Layout only, no clipping.
    public static class ScrollbarClearance
    {
        // [SECTION: Clearance Constants]
        // The minimum gap (DIPs) between the drawn track and the frame's inner top, right and bottom edges, and between the track's
        // bottom-right corner and the frame's inner curve (the top gap stays fixed at this value). Change here only; no user setting.
        public const double BaselineGutter = 2;

        // The bottom inset never shortens the track below this length (DIPs), so a short window with a large radius keeps a
        // usable scrollbar (the track corner may then cross the curve).
        public const double MinTrackLength = 40;

        // The frame element in ThemedTabControlStyle (App.xaml) and the ScrollViewer's scrollbar template part
        private const string FrameName = "TabContentBorder";
        private const string BarPartName = "PART_VerticalScrollBar";

        // Values closer than this (DIPs) to a whole device pixel count as on it, so floating point noise never adds a pixel
        private const double DevicePixelTolerance = 0.001;
        // [END SECTION: Clearance Constants]

        // [SECTION: Attached Properties]
        // Opt-in switch for one ScrollViewer. Not inherited, so ScrollViewers nested inside lists or combos are unaffected.
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(ScrollbarClearance), new PropertyMetadata(false, OnEnabledChanged));

        // The per-ScrollViewer tracker that owns the hooks and the bar margin
        private static readonly DependencyProperty TrackerProperty = DependencyProperty.RegisterAttached(
            "Tracker", typeof(Tracker), typeof(ScrollbarClearance), new PropertyMetadata(null));

        // Does the attached property read
        public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

        // Does the attached property write
        public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

        // Does the opt-in or opt-out: creates and attaches a tracker on true, detaches it and clears the bar margin on false
        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer scrollViewer) return;

            if (scrollViewer.GetValue(TrackerProperty) is Tracker existing)
            {
                existing.Detach();
                scrollViewer.ClearValue(TrackerProperty);
            }

            if (e.NewValue is true)
            {
                var tracker = new Tracker(scrollViewer);
                scrollViewer.SetValue(TrackerProperty, tracker);
                tracker.Attach();
            }
        }
        // [END SECTION: Attached Properties]

        // [SECTION: Clearance Calculation]
        // Does the clearance math (pure, DIPs): returns the bar margin for a frame with bottom-right radius `radius` and border
        // size `borderSize`, an existing gap of `gapTop` / `gapRight` / `gapBottom` between the ScrollViewer and the frame's inner
        // edges, the DPI scales, the baseline, the minimum track length and the bar's height.
        // Top: a fixed baseline gutter (the frame's top corners are square), whatever the radius; the left margin is never set.
        // The track is right-aligned and full height in the bar, so its bottom-right corner is the only point that can cross the
        // curve, and the bar width does not matter. WPF draws CornerRadius at the middle of the border line, so the inner curve
        // is R - B/2 (larger than R - B, the conservative side). The corner must stay `baseline` from that curve, with equal right
        // and bottom gutters g: 2 (Ri - g)^2 <= (Ri - baseline)^2, so g = Ri - (Ri - baseline) / sqrt(2), never below the baseline.
        // An existing gap that already meets g needs no inset; a smaller one is topped up to g rounded up to whole device pixels.
        // The inputs are the ScrollViewer's own geometry, which the bar margin never changes, so repeated runs never accumulate.
        public static Thickness ComputeBarMargin(double radius, double borderSize, double gapTop, double gapRight, double gapBottom,
            double scaleX, double scaleY, double baseline, double minTrackLength, double barHeight)
        {
            double top = gapTop >= baseline ? 0 : Math.Max(0, RoundUpToDevicePixels(baseline, scaleY) - gapTop);

            double innerRadius = Math.Max(0, radius - borderSize / 2);
            double gutter = innerRadius > baseline
                ? innerRadius - (innerRadius - baseline) / Math.Sqrt(2)
                : baseline;

            double right = gapRight >= gutter ? 0 : Math.Max(0, RoundUpToDevicePixels(gutter, scaleX) - gapRight);
            double bottom = gapBottom >= gutter ? 0 : Math.Max(0, RoundUpToDevicePixels(gutter, scaleY) - gapBottom);

            bottom = Math.Min(bottom, Math.Max(0, barHeight - top - minTrackLength));
            return new Thickness(0, top, right, bottom);
        }

        // Does the device pixel round-up: the smallest whole number of device pixels (as DIPs) that is at least `value`
        private static double RoundUpToDevicePixels(double value, double scale)
        {
            if (scale <= 0) scale = 1;
            return Math.Ceiling(value * scale - DevicePixelTolerance) / scale;
        }
        // [END SECTION: Clearance Calculation]

        // [SECTION: Tracker]
        // Owns one opted-in ScrollViewer's hooks. Hooks on Loaded and unhooks on Unloaded (a tab switch unloads its content),
        // recomputing on the ScrollViewer's and the frame's size changes, the frame's CornerRadius and BorderThickness changes
        // (live Global edits, Restore Defaults, close-revert) and the window's DPI change. Computes are deferred to Loaded
        // priority so no layout runs inside layout. Without a frame or a bar part it does nothing and leaves the margin alone.
        private sealed class Tracker
        {
            private static readonly DependencyPropertyDescriptor CornerRadiusDescriptor =
                DependencyPropertyDescriptor.FromProperty(Border.CornerRadiusProperty, typeof(Border));
            private static readonly DependencyPropertyDescriptor BorderThicknessDescriptor =
                DependencyPropertyDescriptor.FromProperty(Border.BorderThicknessProperty, typeof(Border));

            private readonly ScrollViewer _scrollViewer;
            private Border? _frame;
            private Window? _window;
            private ScrollBar? _bar;
            private bool _hooked;
            private bool _pending;

            // Does the tracker setup for one ScrollViewer
            public Tracker(ScrollViewer scrollViewer)
            {
                _scrollViewer = scrollViewer;
            }

            // Does the attach: listens for Loaded / Unloaded and hooks at once when already loaded
            public void Attach()
            {
                _scrollViewer.Loaded += OnLoaded;
                _scrollViewer.Unloaded += OnUnloaded;
                if (_scrollViewer.IsLoaded) Hook();
            }

            // Does the detach (opt-out): removes every handler and clears the margin this tracker set on the bar
            public void Detach()
            {
                _scrollViewer.Loaded -= OnLoaded;
                _scrollViewer.Unloaded -= OnUnloaded;
                Unhook();
                _bar?.ClearValue(FrameworkElement.MarginProperty);
                _bar = null;
            }

            // Does the hook on Loaded
            private void OnLoaded(object sender, RoutedEventArgs e) => Hook();

            // Does the unhook on Unloaded (the bar keeps its margin until the next Loaded recomputes it)
            private void OnUnloaded(object sender, RoutedEventArgs e) => Unhook();

            // Does the hooking: finds the nearest TabContentBorder frame and the window, subscribes, then schedules a compute
            private void Hook()
            {
                if (_hooked) return;
                _frame = FindFrame(_scrollViewer);
                if (_frame == null) return;

                _scrollViewer.SizeChanged += OnGeometryChanged;
                _frame.SizeChanged += OnGeometryChanged;
                CornerRadiusDescriptor.AddValueChanged(_frame, OnFrameValueChanged);
                BorderThicknessDescriptor.AddValueChanged(_frame, OnFrameValueChanged);
                _window = Window.GetWindow(_scrollViewer);
                if (_window != null) _window.DpiChanged += OnDpiChanged;
                _hooked = true;
                Schedule();
            }

            // Does the unhooking: removes every subscription Hook made, including the descriptor ones (they hold strong references)
            private void Unhook()
            {
                if (!_hooked) return;
                _scrollViewer.SizeChanged -= OnGeometryChanged;
                if (_frame != null)
                {
                    _frame.SizeChanged -= OnGeometryChanged;
                    CornerRadiusDescriptor.RemoveValueChanged(_frame, OnFrameValueChanged);
                    BorderThicknessDescriptor.RemoveValueChanged(_frame, OnFrameValueChanged);
                }
                if (_window != null) _window.DpiChanged -= OnDpiChanged;
                _frame = null;
                _window = null;
                _hooked = false;
            }

            // Does a recompute after a size change of the ScrollViewer or the frame
            private void OnGeometryChanged(object sender, SizeChangedEventArgs e) => Schedule();

            // Does a recompute after the frame's CornerRadius or BorderThickness changes
            private void OnFrameValueChanged(object? sender, EventArgs e) => Schedule();

            // Does a recompute after the window moves to a monitor with another DPI
            private void OnDpiChanged(object sender, DpiChangedEventArgs e) => Schedule();

            // Does one deferred compute at Loaded priority, collapsing repeated requests into one
            private void Schedule()
            {
                if (_pending) return;
                _pending = true;
                _scrollViewer.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    _pending = false;
                    Compute();
                }));
            }

            // Does the compute: measures the ScrollViewer's gaps to the frame's inner edges and sets the bar margin. The gaps
            // come from the ScrollViewer, never the bar, so the margin already on the bar never feeds back into the result.
            private void Compute()
            {
                if (!_hooked || _frame == null || !_scrollViewer.IsLoaded) return;
                if (_scrollViewer.ActualWidth <= 0 || _scrollViewer.ActualHeight <= 0) return;
                if (!_frame.IsAncestorOf(_scrollViewer)) return;

                // The template part, looked up each time so a replaced template is picked up
                _scrollViewer.ApplyTemplate();
                if (_scrollViewer.Template?.FindName(BarPartName, _scrollViewer) is not ScrollBar bar) return;
                _bar = bar;

                Point origin, corner;
                try
                {
                    GeneralTransform toFrame = _scrollViewer.TransformToAncestor(_frame);
                    origin = toFrame.Transform(new Point(0, 0));
                    corner = toFrame.Transform(new Point(_scrollViewer.ActualWidth, _scrollViewer.ActualHeight));
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                Thickness border = _frame.BorderThickness;
                double gapTop = origin.Y - border.Top;
                double gapRight = _frame.ActualWidth - border.Right - corner.X;
                double gapBottom = _frame.ActualHeight - border.Bottom - corner.Y;
                DpiScale dpi = VisualTreeHelper.GetDpi(_scrollViewer);

                Thickness margin = ComputeBarMargin(_frame.CornerRadius.BottomRight, Math.Max(border.Right, border.Bottom),
                    gapTop, gapRight, gapBottom, dpi.DpiScaleX, dpi.DpiScaleY, BaselineGutter, MinTrackLength, _scrollViewer.ActualHeight);

                if (!bar.Margin.Equals(margin)) bar.Margin = margin;
            }

            // Does the frame lookup: the nearest visual ancestor Border named TabContentBorder, or null
            private static Border? FindFrame(DependencyObject start)
            {
                DependencyObject? current = VisualTreeHelper.GetParent(start);
                while (current != null)
                {
                    if (current is Border border && border.Name == FrameName) return border;
                    current = VisualTreeHelper.GetParent(current);
                }
                return null;
            }
        }
        // [END SECTION: Tracker]
    }
}
