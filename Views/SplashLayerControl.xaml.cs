using System;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    // Splash Image layer: owns the static, animated and video boot splash. MainWindow.xaml.cs reaches
    // BootSplashVideoPreview and MediaLayer0_Back through this control's public fields.
    public partial class SplashLayerControl : UserControl
    {
        public SplashLayerControl()
        {
            InitializeComponent();
        }

        // Loop: MediaElement has no built-in repeat, so rewind the boot splash and keep playing.
        private void BootSplashVideoPreview_MediaEnded(object sender, RoutedEventArgs e)
        {
            BootSplashVideoPreview.Position = TimeSpan.Zero;
            BootSplashVideoPreview.Play();
        }
    }
}