using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // Game Info layer group: a transparent host holding seven independently positioned parts (see the XAML).
    // This file drives the 4-page Game Info cycle (0 = Game Info, 1 = Trivia, 2 = Tips & Tricks,
    // 3 = Specs & Credits) and picks the one image the previews slot shows. MainWindow's
    // ApplyLayout positions, sizes and shows/hides each part element (PartTitle, PartPublisher, PartInfoNav,
    // PartInfoBody, PartPreviewsBody, PartPlayButton, PartFavoritesStar) directly.
    public partial class GameInfoLayerControl : UserControl
    {
        private int _column2StateIndex = 0;
        private const int Column2StateCount = 4;

        // Raised when the Play Button is clicked; MainWindow does the launch
        public event EventHandler? PlayRequested;

        // Raised when the Favorites Star is clicked; MainWindow does the toggle
        public event EventHandler? FavoriteToggleRequested;

        public GameInfoLayerControl()
        {
            InitializeComponent();
            Loaded += GameInfoLayerControl_Loaded;
        }

        private void GameInfoLayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateColumn2Visibility();
            UpdatePreviewVisibility();

            if (DataContext is System.ComponentModel.INotifyPropertyChanged vm)
            {
                vm.PropertyChanged += DataContext_PropertyChanged;
            }
        }

        // Resets the Game Info pages to the first one on every game switch (the 4 pages are tabs on THIS
        // game's info, not a viewing preference), and scrolls every body back to the top, since ScrollViewers
        // keep their own offset independent of the bound text. Flyer/Title/Snap are set at selection time
        // (instantly from the view model's cache, or together in one callback once a background decode lands)
        // - pick the image to show again whenever any of them changes.
        // The Game Info Nav's color change repaints its dot row (the active dot's fill is set in code).
        private void DataContext_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ThemeGameInfoNavColor")
            {
                UpdateColumn2Visibility();
                return;
            }

            if (e.PropertyName == "FlyerImage" || e.PropertyName == "TitleImage" || e.PropertyName == "SnapImage")
            {
                UpdatePreviewVisibility();
                return;
            }

            if (e.PropertyName != "SelectedGame") return;

            _column2StateIndex = 0;
            UpdateColumn2Visibility();

            Column2BodyGameInfoScroll.ScrollToTop();
            Column2BodyTriviaScroll.ScrollToTop();
            Column2BodyTipsScroll.ScrollToTop();
            Column2BodySpecsCreditsScroll.ScrollToTop();
        }

        // Does the Play Button click: asks MainWindow to launch the selected game
        private void PartPlayButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => PlayRequested?.Invoke(this, EventArgs.Empty);

        // Does the Favorites Star click: asks MainWindow to add or remove the selected game
        private void PartFavoritesStar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => FavoriteToggleRequested?.Invoke(this, EventArgs.Empty);

        // [SECTION: Game Info pages]
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

        // Each dot jumps straight to its own page; Tag holds the target index as a string (set in XAML).
        private void Column2Dot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement dot && dot.Tag is string tag && int.TryParse(tag, out int index))
            {
                _column2StateIndex = index;
                UpdateColumn2Visibility();
            }
        }

        // Resolves the Game Info Nav color into a Brush for its active dot fill (white fallback)
        private Brush ResolveGameInfoNavBrush() =>
            DataContext is ArcadeStick.ViewModels.MainViewModel vm ? ResolveNavBrush(vm.ThemeGameInfoNavColor) : Brushes.White;

        // Converts a nav color hex into a Brush, falling back to white when it can't be parsed
        private static Brush ResolveNavBrush(string colorHex)
        {
            try
            {
                if (new BrushConverter().ConvertFromString(colorHex) is Brush brush) return brush;
            }
            catch { }
            return Brushes.White;
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

            var navColor = ResolveGameInfoNavBrush();
            Column2Dot0.Fill = _column2StateIndex == 0 ? navColor : Brushes.Transparent;
            Column2Dot1.Fill = _column2StateIndex == 1 ? navColor : Brushes.Transparent;
            Column2Dot2.Fill = _column2StateIndex == 2 ? navColor : Brushes.Transparent;
            Column2Dot3.Fill = _column2StateIndex == 3 ? navColor : Brushes.Transparent;
        }
        // [END SECTION: Game Info pages]

        // [SECTION: Image Previews (Flyer / Title Screen / Snap)]
        // Does the fixed image choice: the flyer, else the title screen, else the snap, else a blank slot
        private void UpdatePreviewVisibility()
        {
            var vm = DataContext as ArcadeStick.ViewModels.MainViewModel;
            bool showFlyer = vm?.FlyerImage != null;
            bool showTitle = !showFlyer && vm?.TitleImage != null;
            bool showSnap = !showFlyer && !showTitle && vm?.SnapImage != null;

            Row3FlyerImage.Visibility = showFlyer ? Visibility.Visible : Visibility.Collapsed;
            Row3TitleImage.Visibility = showTitle ? Visibility.Visible : Visibility.Collapsed;
            Row3SnapImage.Visibility = showSnap ? Visibility.Visible : Visibility.Collapsed;
        }
        // [END SECTION: Image Previews (Flyer / Title Screen / Snap)]
    }
}