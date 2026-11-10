using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ArcadeStick.Services;

namespace ArcadeStick.Views
{
    // The Theme Browser's enlarged thumbnail preview over its dimmed host window (the Theme Builder or the standalone Theme
    // Browser window; each hosts its own instance as the last child of its root Grid). While open nothing underneath can
    // be used: every mouse button press is swallowed and captured, and the matching release (or a lost capture) closes it;
    // the wheel is swallowed; every key and typed character is swallowed at the window, and Escape closes it on its
    // release. Opening and closing load and save nothing, and keyboard focus goes back where it was.
    public partial class ThemePreviewOverlay : UserControl
    {
        // [SECTION: Preview Constants]
        public const double HoverGlyphSize = 32;        // the Browser tile's magnifier-plus glyph box
        public const double FrameBorderThickness = 4;   // the pop-out frame's border (fixed, not the Global border size)
        public const double FramePadding = 8;           // between the frame's border and the image
        // [END SECTION: Preview Constants]

        // The window whose keyboard input is blocked while open, and the element that had keyboard focus before opening
        private Window? _window;
        private IInputElement? _focusBeforeOpen;

        // True while the preview shows
        public bool IsOpen { get; private set; }

        public ThemePreviewOverlay()
        {
            InitializeComponent();
            Frame.BorderThickness = new Thickness(FrameBorderThickness);
            Frame.Padding = new Thickness(FramePadding);

            PreviewMouseDown += Overlay_PreviewMouseDown;
            PreviewMouseUp += Overlay_PreviewMouseUp;
            LostMouseCapture += Overlay_LostMouseCapture;
            PreviewMouseWheel += (_, e) => e.Handled = IsOpen;
        }

        // [SECTION: Open and Close]
        // Does a thumbnail click from either Browser host: reads the theme's saved thumbnail at full size from its zip
        // (ThemeCatalogReader.ReadFullThumbnail, no lock kept) and shows it. Returns false, opening nothing, when the zip is
        // gone or unreadable or has no usable thumbnail; the host then refreshes its catalog.
        public bool TryShow(string zipPath)
        {
            var image = ThemeCatalogReader.ReadFullThumbnail(zipPath);
            if (image == null) return false;

            Show(image);
            return true;
        }

        // Does the open: shows the image at its own pixel size fitted into the pop-out box (never upscaled, so its DPI
        // setting does not matter), dims the window, blocks its keyboard input and takes keyboard focus
        public void Show(BitmapSource image)
        {
            if (IsOpen) Hide();

            _window = Window.GetWindow(this);
            if (_window == null || image.PixelWidth <= 0 || image.PixelHeight <= 0) return;

            double scale = Math.Min(1.0, Math.Min(ThemeThumbnailStore.PopOutMaxWidth / image.PixelWidth, ThemeThumbnailStore.PopOutMaxHeight / image.PixelHeight));
            ImgPreview.Width = Math.Max(1, Math.Round(image.PixelWidth * scale));
            ImgPreview.Height = Math.Max(1, Math.Round(image.PixelHeight * scale));
            ImgPreview.Source = image;

            _focusBeforeOpen = Keyboard.FocusedElement;
            _window.PreviewKeyDown += Window_PreviewKeyDown;
            _window.PreviewKeyUp += Window_PreviewKeyUp;
            _window.PreviewTextInput += Window_PreviewTextInput;

            IsOpen = true;
            Visibility = Visibility.Visible;
            Focus();
        }

        // Does the close (also called on window deactivation and closing): stops blocking, releases the image and the mouse,
        // hides the overlay and gives keyboard focus back. Safe when already closed.
        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;

            if (_window != null)
            {
                _window.PreviewKeyDown -= Window_PreviewKeyDown;
                _window.PreviewKeyUp -= Window_PreviewKeyUp;
                _window.PreviewTextInput -= Window_PreviewTextInput;
            }
            if (IsMouseCaptured) ReleaseMouseCapture();

            Visibility = Visibility.Collapsed;
            ImgPreview.Source = null;

            RestoreFocus();
            _window = null;
        }

        // Does the focus restore: the element focused before opening, if it is still on screen, with scroll-into-view blocked
        // during the restore so the Browser list never moves; the temporary block is always removed. Otherwise focus stays
        // where WPF puts it.
        private void RestoreFocus()
        {
            var target = _focusBeforeOpen as UIElement;
            _focusBeforeOpen = null;
            if (target == null || !target.IsVisible || PresentationSource.FromDependencyObject(target) == null) return;

            RequestBringIntoViewEventHandler blockScroll = (_, e) => e.Handled = true;
            target.AddHandler(FrameworkElement.RequestBringIntoViewEvent, blockScroll);
            try
            {
                target.Focus();
            }
            finally
            {
                target.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent, blockScroll);
            }
        }
        // [END SECTION: Open and Close]

        // [SECTION: Input Blocking]
        // Does the mouse press: swallowed (nothing beneath gets it) and captured, so its release comes back here
        private void Overlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsOpen) return;
            e.Handled = true;
            CaptureMouse();
        }

        // Does the mouse release: swallowed and closes the preview, so it can never click, load or reopen anything beneath
        private void Overlay_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsOpen) return;
            e.Handled = true;
            Hide();
        }

        // Does the lost capture (for example Alt+Tab during a press): closes the preview
        private void Overlay_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (IsOpen) Hide();
        }

        // Does the key press: every key is swallowed (Tab, Space, Enter, arrows, Page keys, access keys, shortcuts), Escape
        // included, which closes on its release. Alt+F4 passes, so the window can still close (Closing hides the preview).
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.System && e.SystemKey == Key.F4) return;
            e.Handled = true;
        }

        // Does the key release: swallowed; Escape closes the preview
        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.Key == Key.Escape) Hide();
        }

        // Does the typed text: swallowed
        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
        }
        // [END SECTION: Input Blocking]
    }
}
