using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // Splash Image layer: owns the static, animated and video boot splash. MainWindow.xaml.cs reaches
    // BootSplashVideoPreview and MediaLayer0_Back through this control's public fields.
    // The border (size, color, corner radius) comes from the layer's entry, pushed in by ApplyBorder.
    public partial class SplashLayerControl : UserControl
    {
        private double _borderSize;
        private double _borderRadius;

        public SplashLayerControl()
        {
            InitializeComponent();
            SplashContent.SizeChanged += (s, e) => ApplyClip();
        }

        // Loop: MediaElement has no built-in repeat, so rewind the boot splash and keep playing.
        private void BootSplashVideoPreview_MediaEnded(object sender, RoutedEventArgs e)
        {
            BootSplashVideoPreview.Position = TimeSpan.Zero;
            BootSplashVideoPreview.Play();
        }

        // Called from MainWindow.ApplyLayout with the layer entry's border values.
        public void ApplyBorder(double size, string colorHex, double radius)
        {
            _borderSize = Math.Max(0, size);
            _borderRadius = Math.Max(0, radius);

            SplashRing.BorderThickness = new Thickness(_borderSize);
            SplashRing.CornerRadius = new CornerRadius(_borderRadius);
            SplashRing.BorderBrush = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);

            ApplyClip();
        }

        // Same rule as VideoPreviewLayerControl.ApplyClip: the ring stays at the control's outer edge (ApplyLayout sizes the
        // control to the entry rect inflated by BorderSize), and the splash content pulls in by the border minus 1px, so the
        // ring covers 1px of the picture (hides any sub-pixel sliver). Corner radius and clip shrink by the same inset.
        private void ApplyClip()
        {
            double inset = Math.Max(0, _borderSize - 1);
            double innerRadius = Math.Max(0, _borderRadius - inset);
            SplashContent.Margin = new Thickness(inset);

            double width = SplashContent.ActualWidth;
            double height = SplashContent.ActualHeight;
            if (width <= 0 || height <= 0) return;

            SplashContent.Clip = new RectangleGeometry(new Rect(0, 0, width, height), innerRadius, innerRadius);
        }
    }
}