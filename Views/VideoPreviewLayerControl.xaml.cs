using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // Video Preview layer: fallback image, static preview image and the MediaElement player, all fitting
    // inside this control's rect. MainWindow.xaml.cs reaches MediaElementPreview through the public field.
    public partial class VideoPreviewLayerControl : UserControl
    {
        public VideoPreviewLayerControl()
        {
            InitializeComponent();
            Loaded += VideoPreviewLayerControl_Loaded;
        }

        private void VideoPreviewLayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyClip();

            if (DataContext is System.ComponentModel.INotifyPropertyChanged vm)
            {
                vm.PropertyChanged += DataContext_PropertyChanged;
            }
        }

        private void MediaSlotContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyClip();
        }

        // Border radius/size changes from the Theme Builder don't resize the slot, so SizeChanged alone
        // wouldn't rebuild the rounded clip - redo it here.
        private void DataContext_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ThemeVideoBorderRadius" || e.PropertyName == "ThemeVideoBorderSize")
            {
                ApplyClip();
            }
        }

        // The border ring stays at the slot's outer edge; the video layer pulls in by the border thickness
        // minus 1px, so the border only covers 1px of the picture (enough to hide any sub-pixel sliver).
        // Corner radius and clip shrink by the same inset to stay concentric.
        private void ApplyClip()
        {
            double availableWidth = MediaSlotContent.ActualWidth;
            double availableHeight = MediaSlotContent.ActualHeight;
            if (availableWidth <= 0 || availableHeight <= 0) return;

            double outerRadius = 0;
            double thickness = 0;
            if (DataContext is ArcadeStick.ViewModels.MainViewModel vm)
            {
                outerRadius = vm.ThemeVideoBorderRadius;
                thickness = vm.ThemeVideoBorderSize;
            }

            double inset = Math.Max(0, thickness - 1);
            double videoRadius = Math.Max(0, outerRadius - inset);

            MediaSlot.Margin = new Thickness(inset);
            MediaSlot.CornerRadius = new CornerRadius(videoRadius);
            MediaSlotContent.Clip = new RectangleGeometry(
                new Rect(0, 0, availableWidth, availableHeight), videoRadius, videoRadius);
        }
    }
}