using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // [SECTION: Media Panel Control]
    // Media panel rework in progress. LibVLC/_libVLC, VlcMediaPlayer, and BootSplashMediaPlayer all
    // remain created and owned by MainWindow (the VideoView.MediaPlayer bindings inside this control's
    // XAML resolve them via RelativeSource AncestorType=Window, which works correctly through this
    // control's boundary). VideoPreview and MediaLayer0_Back are exposed as public fields (via
    // x:FieldModifier="public" in the XAML) so MainWindow.xaml.cs's existing call sites can keep
    // reaching them directly through this control instance.
    public partial class MediaPanelControl : UserControl
    {
        public MediaPanelControl()
        {
            InitializeComponent();
            Loaded += MediaPanelControl_Loaded;
        }

        private void MediaPanelControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateRow3Visibility();
            ApplyRow3TextCounterScale();
            UpdateColumn2Visibility();
            ResizeVideoPreviewToFit();

            if (DataContext is System.ComponentModel.INotifyPropertyChanged vm)
            {
                vm.PropertyChanged += DataContext_PropertyChanged;
            }
        }

        // [SECTION: Video Preview Real-Pixel Sizing]
        // VideoView is HwndHost-backed (LibVLC), which ignores WPF render/layout transforms - a Viewbox
        // wrapping it renders full-size regardless of the transform, which is why that approach silently
        // failed (the video always covered the full slot, swallowing the border). This instead computes
        // the exact 4:3 pixel box that fits inside ColumnOneMediaSlotContent (the inner Grid Border arranges
        // AFTER subtracting BorderThickness - Border.ActualWidth/Height report the OUTER box and don't
        // change with BorderThickness, which is why the first version of this fix silently failed too), and
        // sets VideoPreview's real Width/Height to that box. That's a true Arrange-time size, which HwndHost
        // has no choice but to honor, and Center alignment lets it float in the middle of the slot with the
        // border-thickness gap as genuinely empty space around it.
        private void ColumnOneMediaSlotContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ResizeVideoPreviewToFit();
        }

        // Back to exact-4:3 sizing (not "fill the leftover area exactly") - LibVLC internally preserves the
        // source video's real aspect ratio no matter what Width/Height we hand VideoView, so handing it a
        // non-4:3 box (the previous approach) just meant LibVLC pillarboxed ITSELF inside that box, painting
        // its own default near-black padding that our WPF bindings can't reach or theme. Handing it a true
        // 4:3 box eliminates that internal pillarboxing entirely. The few px of leftover space this leaves
        // on two sides (since a symmetric BorderThickness subtracted from an exact 4:3 box can't produce
        // another exact 4:3 box) is now background-matched to ThemeVideoBorderColor in XAML, so it merges
        // into the border instead of reading as a mismatched leak.
        private void ResizeVideoPreviewToFit()
        {
            double availableWidth = ColumnOneMediaSlotContent.ActualWidth;
            double availableHeight = ColumnOneMediaSlotContent.ActualHeight;
            if (availableWidth <= 0 || availableHeight <= 0) return;

            const double targetAspect = 4.0 / 3.0;
            double candidateWidth = availableHeight * targetAspect;

            double finalWidth;
            double finalHeight;
            if (candidateWidth <= availableWidth)
            {
                finalWidth = candidateWidth;
                finalHeight = availableHeight;
            }
            else
            {
                finalWidth = availableWidth;
                finalHeight = availableWidth / targetAspect;
            }

            // Rounded to whole pixels before assignment - WPF centers VideoPreview at whatever fractional
            // offset the math produces, but the underlying HWND can only be placed at integer pixel
            // boundaries. That mismatch was leaving a literal 1px sliver where neither the WPF border nor
            // the video itself painted, exposing whatever's behind in the visual tree as a thin gray line.
            // Whole-pixel dimensions mean WPF's centering offset also lands on a whole pixel, closing the gap.
            VideoPreview.Width = Math.Round(finalWidth);
            VideoPreview.Height = Math.Round(finalHeight);
        }
        // [END SECTION: Video Preview Real-Pixel Sizing]

        // ScrollViewers keep their own scroll position independent of whatever Text is bound inside
        // them - switching games doesn't reset it on its own, so a game scrolled to the bottom of its
        // Info/Credits/Specs text would stay scrolled to the bottom after picking a different game.
        // Resets all five scrollable text areas to the top whenever the selected game changes.
        private void DataContext_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Flyer/Title/Snap resolve after the preview debounce, not at selection time - rebuild Row 3's
            // available states and dot row whenever any of them lands.
            if (e.PropertyName == "FlyerImage" || e.PropertyName == "TitleImage" || e.PropertyName == "SnapImage")
            {
                UpdateRow3Visibility();
                return;
            }

            if (e.PropertyName != "SelectedGame") return;

            // Column Two resets to Game Info on every game switch - its 4 states are tabs on THIS game's
            // info, not a viewing preference that should carry over. Row 3 deliberately stays on whatever
            // state the user picked (e.g. always viewing Snaps) since that IS a viewing preference.
            _column2StateIndex = 0;
            UpdateColumn2Visibility();

            Column2BodyGameInfoScroll.ScrollToTop();
            Column2BodyTriviaScroll.ScrollToTop();
            Column2BodyTipsScroll.ScrollToTop();
            Column2BodySpecsCreditsScroll.ScrollToTop();
        }

        // [SECTION: Row 3 - Dynamic Image Cycling]
        // 0 = Flyer, 1 = Title Screen, 2 = Snap - matches Row 2's dot order left-to-right. Only states whose
        // image actually resolved are part of the cycle. _row3StateIndex is the user's PREFERRED state and
        // persists across game switches (viewing preference); if the new game lacks that image, the first
        // available one shows instead without overwriting the preference.
        private int _row3StateIndex = 0;

        private int[] GetRow3AvailableStates()
        {
            var available = new System.Collections.Generic.List<int>(3);
            if (DataContext is ArcadeStick.ViewModels.MainViewModel vm)
            {
                if (vm.FlyerImage != null) available.Add(0);
                if (vm.TitleImage != null) available.Add(1);
                if (vm.SnapImage != null) available.Add(2);
            }
            return available.ToArray();
        }

        // Returns -1 when nothing is available (slot stays blank).
        private int ResolveRow3ActiveState(int[] available)
        {
            if (available.Length == 0) return -1;
            return Array.IndexOf(available, _row3StateIndex) >= 0 ? _row3StateIndex : available[0];
        }

        private void StepRow3State(int direction)
        {
            var available = GetRow3AvailableStates();
            if (available.Length < 2) return;

            int position = Array.IndexOf(available, ResolveRow3ActiveState(available));
            position = (position + direction + available.Length) % available.Length;
            _row3StateIndex = available[position];
            UpdateRow3Visibility();
        }

        private void Row3ArrowLeft_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StepRow3State(-1);
        }

        private void Row3ArrowRight_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StepRow3State(1);
        }

        // Lets each dot jump straight to its own state, same as clicking a pagination dot anywhere else -
        // Tag holds the target index as a string (set in XAML). Clicking the already-active dot just
        // re-applies the same index, a harmless no-op.
        private void Row3Dot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement dot && dot.Tag is string tag && int.TryParse(tag, out int index))
            {
                _row3StateIndex = index;
                UpdateRow3Visibility();
            }
        }

        // Code-behind can't reuse the XAML {Binding ThemeNavIconsColor} directly (it needs an actual
        // Brush, not a binding expression) - this resolves the same hex string DataContext exposes and
        // converts it, falling back to white if DataContext isn't a MainViewModel yet or parsing fails.
        private Brush ResolveThemeNavIconsBrush()
        {
            if (DataContext is ArcadeStick.ViewModels.MainViewModel vm)
            {
                try
                {
                    return (Brush)new BrushConverter().ConvertFromString(vm.ThemeNavIconsColor);
                }
                catch { }
            }
            return Brushes.White;
        }

        // Nav row only shows with 2+ images - with 0 or 1 there's nothing to cycle. ColumnOneArrowRow sits
        // in a fixed-height grid row, so collapsing it never shifts the image slot below.
        private void UpdateRow3Visibility()
        {
            var available = GetRow3AvailableStates();
            int active = ResolveRow3ActiveState(available);

            Row3FlyerImage.Visibility = active == 0 ? Visibility.Visible : Visibility.Collapsed;
            Row3TitleImage.Visibility = active == 1 ? Visibility.Visible : Visibility.Collapsed;
            Row3SnapImage.Visibility = active == 2 ? Visibility.Visible : Visibility.Collapsed;

            ColumnOneArrowRow.Visibility = available.Length >= 2 ? Visibility.Visible : Visibility.Collapsed;

            Row3Dot0.Visibility = Array.IndexOf(available, 0) >= 0 ? Visibility.Visible : Visibility.Collapsed;
            Row3Dot1.Visibility = Array.IndexOf(available, 1) >= 0 ? Visibility.Visible : Visibility.Collapsed;
            Row3Dot2.Visibility = Array.IndexOf(available, 2) >= 0 ? Visibility.Visible : Visibility.Collapsed;

            var navColor = ResolveThemeNavIconsBrush();
            Row3Dot0.Fill = active == 0 ? navColor : Brushes.Transparent;
            Row3Dot1.Fill = active == 1 ? navColor : Brushes.Transparent;
            Row3Dot2.Fill = active == 2 ? navColor : Brushes.Transparent;
        }
        // [END SECTION: Row 3 - Dynamic Image Cycling]

        // [SECTION: Column Two - 3-State Cycling]
        // 0 = Game Info, 1 = Trivia, 2 = Tips & Tricks - independent of Row 3's 4-state index, own arrow
        // control, own dot row. No counter-scale needed here (unlike Row 3) since Column Two was never
        // inside a scoped Viewbox to begin with - it already renders at true fixed size.
        private int _column2StateIndex = 0;
        private const int Column2StateCount = 4;

        private void Column2ArrowLeft_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _column2StateIndex = (_column2StateIndex - 1 + Column2StateCount) % Column2StateCount;
            UpdateColumn2Visibility();
        }

        private void Column2ArrowRight_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _column2StateIndex = (_column2StateIndex + 1) % Column2StateCount;
            UpdateColumn2Visibility();
        }

        // Lets each dot jump straight to its own state, same as clicking a pagination dot anywhere else -
        // Tag holds the target index as a string (set in XAML). Clicking the already-active dot just
        // re-applies the same index, a harmless no-op.
        private void Column2Dot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement dot && dot.Tag is string tag && int.TryParse(tag, out int index))
            {
                _column2StateIndex = index;
                UpdateColumn2Visibility();
            }
        }

        private void UpdateColumn2Visibility()
        {
            Column2HeaderGameInfo.Visibility = _column2StateIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            Column2HeaderTrivia.Visibility = _column2StateIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            Column2HeaderTips.Visibility = _column2StateIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            Column2HeaderSpecsCredits.Visibility = _column2StateIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

            Column2BodyGameInfoScroll.Visibility = _column2StateIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            Column2BodyTriviaScroll.Visibility = _column2StateIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            Column2BodyTipsScroll.Visibility = _column2StateIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            Column2BodySpecsCredits.Visibility = _column2StateIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

            var navColor = ResolveThemeNavIconsBrush();
            Column2Dot0.Fill = _column2StateIndex == 0 ? navColor : Brushes.Transparent;
            Column2Dot1.Fill = _column2StateIndex == 1 ? navColor : Brushes.Transparent;
            Column2Dot2.Fill = _column2StateIndex == 2 ? navColor : Brushes.Transparent;
            Column2Dot3.Fill = _column2StateIndex == 3 ? navColor : Brushes.Transparent;
        }
        // [END SECTION: Column Two - 3-State Cycling]

        // [SECTION: Row 3 Text Counter-Scale]
        // Column One's Flyer/Cabinet images live inside the scoped Viewbox and are meant to scale with
        // the column - that's correct and desired. Staff/Technical TEXT is not - text scaling down with
        // a narrow column would become illegible, so this pushes an inverse LayoutTransform onto just
        // the two text elements, canceling out however much the Viewbox has scaled the 640-wide design
        // canvas, so the text always renders at a fixed real on-screen size regardless of window size.
        private const double ColumnOneDesignWidth = 800.0;

        private void ColumnOneViewbox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyRow3TextCounterScale();
        }

        private void ApplyRow3TextCounterScale()
        {
            if (ColumnOneViewbox.ActualWidth <= 0) return;

            double currentScale = ColumnOneViewbox.ActualWidth / ColumnOneDesignWidth;
            if (currentScale <= 0) return;

            double counterScale = 1.0 / currentScale;
            var transform = new ScaleTransform(counterScale, counterScale);

            Row3ArrowStack.LayoutTransform = transform;
        }
        // [END SECTION: Row 3 Text Counter-Scale]
    }
    // [END SECTION: Media Panel Control]
}