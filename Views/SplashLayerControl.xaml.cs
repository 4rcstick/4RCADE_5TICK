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
            SizeChanged += (s, e) => ApplyClip();
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

        // The splash content sits inside the border ring, with a rounded clip matching the ring's inner edge.
        private void ApplyClip()
        {
            double width = ActualWidth - (_borderSize * 2);
            double height = ActualHeight - (_borderSize * 2);
            if (width <= 0 || height <= 0) return;

            double innerRadius = Math.Max(0, _borderRadius - _borderSize);

            SplashContent.Margin = new Thickness(_borderSize);
            SplashContent.Clip = new RectangleGeometry(new Rect(0, 0, width, height), innerRadius, innerRadius);
        }
    }
}