using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // Video Preview layer: fallback image, static preview image and the preview video surface, all fitting
    // inside the border ring. ApplyLayout places this control at the entry's X / Y (the ring's outer corner), sized to the
    // entry's Width / Height (the video area) plus 2 x BorderSize, so the ring draws outside the video area. MainWindow.xaml.cs reaches PreviewVideoSurface
    // through the public field.
    // The border (size, color, corner radius) and the background color come from the layer's entry, pushed in
    // by ApplyBorder and ApplyBackground. The preview sound glyph sits at the video area's bottom right.
    public partial class VideoPreviewLayerControl : UserControl
    {
        private double _borderSize;
        private double _borderRadius;

        // [SECTION: Preview Sound Glyph Values]
        private const string SoundOnGlyph = "\uE767";    // Volume
        private const string SoundOffGlyph = "\uE74F";   // Mute
        private const double SoundGlyphHeightRatio = 0.08;
        private const double SoundGlyphMinSize = 16;
        private const double SoundGlyphMaxSize = 40;
        private const double SoundGlyphMargin = 8;
        // [END SECTION: Preview Sound Glyph Values]

        private bool _previewPlaying;

        // Raised when the sound glyph is clicked; MainWindow owns the setting and the restart
        public event EventHandler? SoundToggleRequested;

        public VideoPreviewLayerControl()
        {
            InitializeComponent();
            Loaded += VideoPreviewLayerControl_Loaded;
            MouseEnter += (s, e) => UpdateSoundGlyphVisibility();
            MouseLeave += (s, e) => UpdateSoundGlyphVisibility();
        }

        private void VideoPreviewLayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyClip();
        }

        private void MediaSlotContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyClip();
        }

        // Called from MainWindow.ApplyLayout with the layer entry's border values.
        public void ApplyBorder(double size, string colorHex, double radius)
        {
            _borderSize = Math.Max(0, size);
            _borderRadius = Math.Max(0, radius);

            MediaSlotRing.BorderThickness = new Thickness(_borderSize);
            MediaSlotRing.CornerRadius = new CornerRadius(_borderRadius);
            MediaSlotRing.BorderBrush = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);
            SoundGlyphButton.Margin = new Thickness(0, 0, _borderSize + SoundGlyphMargin, _borderSize + SoundGlyphMargin);

            ApplyClip();
        }

        // Called from MainWindow.ApplyLayout with the layer entry's background color (the letterbox bars)
        public void ApplyBackground(string colorHex)
        {
            MediaSlot.Background = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);
        }

        // Called from MainWindow.ApplyLayout with the entry's Height (the video area): sizes the glyph from it
        public void ApplySoundGlyphSize(double contentHeight)
        {
            SoundGlyphIcon.FontSize = Math.Clamp(contentHeight * SoundGlyphHeightRatio, SoundGlyphMinSize, SoundGlyphMaxSize);
        }

        // Shows the volume glyph when sound is on, the mute glyph when it is off
        public void SetSoundEnabled(bool enabled)
        {
            SoundGlyphIcon.Text = enabled ? SoundOnGlyph : SoundOffGlyph;
        }

        // MainWindow reports whether a preview video is actually playing in the slot
        public void SetPreviewPlaying(bool playing)
        {
            _previewPlaying = playing;
            UpdateSoundGlyphVisibility();
        }

        // Does the glyph's visibility: only while the mouse is over the layer and a video is playing
        private void UpdateSoundGlyphVisibility()
        {
            SoundGlyphButton.Visibility = _previewPlaying && IsMouseOver ? Visibility.Visible : Visibility.Collapsed;
        }

        // Passes a glyph click to MainWindow
        private void SoundGlyphButton_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            SoundToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        // The border ring stays at the slot's outer edge; the video layer pulls in by the border thickness
        // minus 1px, so the border only covers 1px of the picture (enough to hide any sub-pixel sliver).
        // Corner radius and clip shrink by the same inset to stay concentric.
        private void ApplyClip()
        {
            double availableWidth = MediaSlotContent.ActualWidth;
            double availableHeight = MediaSlotContent.ActualHeight;
            if (availableWidth <= 0 || availableHeight <= 0) return;

            double inset = Math.Max(0, _borderSize - 1);
            double videoRadius = Math.Max(0, _borderRadius - inset);

            MediaSlot.Margin = new Thickness(inset);
            MediaSlot.CornerRadius = new CornerRadius(videoRadius);
            MediaSlotContent.Clip = new RectangleGeometry(
                new Rect(0, 0, availableWidth, availableHeight), videoRadius, videoRadius);
        }
    }
}