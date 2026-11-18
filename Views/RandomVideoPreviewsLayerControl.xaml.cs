using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // [SECTION: Random Video Previews Layer]
    // Random Video Previews layer: the splash-only video surface inside the outer video frame. ApplyLayout places this control at
    // the entry's X / Y (the ring's outer corner), sized to the entry's Width / Height (the video area) plus 2 x BorderSize, the
    // same border-ring rule as the Video Preview. The frame (size, color, corner radius) and the letterbox color come from the
    // layer's entry, pushed in by ApplyBorder and ApplyBackground. MainWindow.xaml.cs reaches PreviewVideoSurface through the
    // public field.
    public partial class RandomVideoPreviewsLayerControl : UserControl
    {
        private double _borderSize;
        private double _borderRadius;

        // Does the control setup: the rounded clip is applied once the control is loaded
        public RandomVideoPreviewsLayerControl()
        {
            InitializeComponent();
            Loaded += (s, e) => ApplyClip();
        }

        // Does the clip refresh when the media area changes size
        private void MediaSlotContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyClip();
        }

        // Does the outer video frame from the layer entry's border values (called from MainWindow.ApplyLayout)
        public void ApplyBorder(double size, string colorHex, double radius)
        {
            _borderSize = Math.Max(0, size);
            _borderRadius = Math.Max(0, radius);

            MediaSlotRing.BorderThickness = new Thickness(_borderSize);
            MediaSlotRing.CornerRadius = new CornerRadius(_borderRadius);
            MediaSlotRing.BorderBrush = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);

            ApplyClip();
        }

        // Does the letterbox color behind the video from the layer entry (called from MainWindow.ApplyLayout)
        public void ApplyBackground(string colorHex)
        {
            MediaSlot.Background = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);
        }

        // Does the inset and rounded clip, same as VideoPreviewLayerControl.ApplyClip: the media area pulls in by the border minus
        // 1px, so the frame covers only 1px of the picture, and its corner radius and clip shrink by the same inset
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
    // [END SECTION: Random Video Previews Layer]
}
