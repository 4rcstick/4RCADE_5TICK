using System.Windows;

namespace ArcadeStick.Views
{
    // The standalone Theme Browser (window management Pass 6, themes.md 3.29): a modeless workspace window with no owner,
    // opened and tracked by MainWindow.OpenThemeBrowserWindow like Options and Sorting. It hosts the same
    // ThemeBrowserControl as the Theme Builder's THEME BROWSER tab and its own ThemePreviewOverlay; it holds no load,
    // catalog or preview logic of its own. A resolution button goes to MainWindow.LoadThemeFromBrowser, the one load path
    // both Browsers share, and MainWindow's notices keep its marks and catalog current.
    public partial class ThemeBrowserWindow : Window
    {
        private readonly MainWindow _main;

        // [SECTION: Constructor]
        // Does the setup: work-area fit, the catalog settings, the load and preview requests, the preview's dismissal on
        // deactivation or close, and the first catalog read (the window shows the catalog as soon as it opens)
        public ThemeBrowserWindow(ArcadeStick.ViewModels.MainViewModel viewModel, MainWindow main)
        {
            InitializeComponent();
            ArcadeStick.Services.WindowFit.KeepInWorkArea(this); // never taller than the monitor's work area
            _main = main;

            ThemeBrowser.Initialize(viewModel.Configuration);
            ThemeBrowser.LoadRequested += (name, width, height) => _main.LoadThemeFromBrowser(name, width, height);
            ThemeBrowser.PreviewRequested += entry =>
            {
                if (!PreviewOverlay.TryShow(entry.ZipPath)) ThemeBrowser.RefreshCatalog();
            };

            // The preview never outlives the window's focus: losing activation or closing dismisses it first
            Deactivated += (s, e) => PreviewOverlay.Hide();
            Closing += (s, e) => PreviewOverlay.Hide();

            ThemeBrowser.RefreshCatalog();
        }
        // [END SECTION: Constructor]

        // [SECTION: Notices]
        // Called by MainWindow.NotifyActiveThemeOrSizeChanged: the highlight and active marker follow the active theme and
        // size in place, with no zip read
        public void RefreshActiveMarks()
        {
            ThemeBrowser.RefreshActiveMarks();
        }

        // Called by MainWindow.NotifySavedThemesChanged and after a failed Browser load: re-reads the catalog (only zips whose
        // write time or length changed are read again) and the marks, keeping the scroll position. Runs while minimized too,
        // so the window is current when it comes back.
        public void RefreshCatalog()
        {
            ThemeBrowser.RefreshCatalog();
        }
        // [END SECTION: Notices]

        private void BtnCloseBrowser_Click(object sender, RoutedEventArgs e) => Close();
    }
}
