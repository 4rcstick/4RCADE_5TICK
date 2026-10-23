using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // Video Preview layer: fallback image, static preview image and the preview video surface, all fitting
    // inside the border ring. ApplyLayout sizes this control to the entry's rect (the video area) inflated by
    // BorderSize, so the ring draws outside the video area. MainWindow.xaml.cs reaches PreviewVideoSurface
    // through the public field.
    // The border (size, color, corner radius) and the background color come from the layer's entry, pushed in
    // by ApplyBorder and ApplyBackground.
    public partial class VideoPreviewLayerControl : UserControl
    {
        private double _borderSize;
        private double _borderRadius;

        public VideoPreviewLayerControl()
        {
            InitializeComponent();
            Loaded += VideoPreviewLayerControl_Loaded;
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

            ApplyClip();
        }

        // Called from MainWindow.ApplyLayout with the layer entry's background color (the letterbox bars)
        public void ApplyBackground(string colorHex)
        {
            MediaSlot.Background = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);
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