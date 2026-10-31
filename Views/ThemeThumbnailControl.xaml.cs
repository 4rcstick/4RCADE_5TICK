using ArcadeStick.Models;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ArcadeStick.Views
{
    // Shows a theme's thumbnail at a fixed display height, or the "No Image" box. Content only: headings and glyphs belong to the host.
    public partial class ThemeThumbnailControl : UserControl
    {
        private const double NoImageAspect = 320.0 / 180.0;
        private double _displayHeight = 180;

        // Raised when a new image is set through SetNewImage (not when the host shows a stored value)
        public event Action<BitmapSource>? ThumbnailChanged;

        public ThemeThumbnailControl()
        {
            InitializeComponent();
            TxtNoImage.Text = ThemeInfoFallbacks.NoImage;
            ApplyDisplayHeight();
        }

        // The height the image is shown at; the width follows its aspect ratio
        public double DisplayHeight
        {
            get => _displayHeight;
            set
            {
                _displayHeight = value;
                ApplyDisplayHeight();
            }
        }

        // Does the sizing of the image limit and the "No Image" box from the display height
        private void ApplyDisplayHeight()
        {
            ImgThumbnail.MaxHeight = _displayHeight;
            BdNoImage.Height = _displayHeight;
            BdNoImage.Width = _displayHeight * NoImageAspect;
        }

        // Shows a stored image file. A blank, missing or corrupt file shows the "No Image" box.
        public void ShowFile(string absolutePath) => ShowImage(LoadImage(absolutePath));

        // Shows an image held in memory (null = the "No Image" box)
        public void ShowImage(BitmapSource? image)
        {
            ImgThumbnail.Source = image;
            ImgThumbnail.Visibility = image != null ? Visibility.Visible : Visibility.Collapsed;
            BoxNoImage.Visibility = image != null ? Visibility.Collapsed : Visibility.Visible;
        }

        // Shows a newly captured image and tells the host
        public void SetNewImage(BitmapSource image)
        {
            ShowImage(image);
            ThumbnailChanged?.Invoke(image);
        }

        // Loads into memory so the file is never locked (LoadTheme deletes the extract folder), bypassing the cache so a retake never shows the old image
        private static BitmapSource? LoadImage(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath)) return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.UriSource = new Uri(absolutePath);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
