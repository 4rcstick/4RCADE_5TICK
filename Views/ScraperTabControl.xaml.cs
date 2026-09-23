using System;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class ScraperTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;
        private ArcadeStick.Services.ArtworkScraperService? _scraperService;

        public ScraperTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Load / Save Sync]
        // Load side: populates each checkbox from ConfigurationSettings. Called by OptionsWindow
        // when this tab is shown.
        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;
            _scraperService = new ArcadeStick.Services.ArtworkScraperService(_settings);

            ChkScraperEnabled.IsChecked = _settings.ScraperEnabled;

            ChkFetchOnLaunch.IsChecked = _settings.ScraperFetchOnLaunch;
            ChkFetchOnContextMenu.IsChecked = _settings.ScraperFetchOnContextMenu;

            ChkFetchMarquees.IsChecked = _settings.ScraperFetchMarquees;
            ChkFetchFlyers.IsChecked = _settings.ScraperFetchFlyers;
            ChkFetchTitlescreens.IsChecked = _settings.ScraperFetchTitlescreens;
            ChkFetchSnaps.IsChecked = _settings.ScraperFetchSnaps;
            ChkFetchCabinets.IsChecked = _settings.ScraperFetchCabinets;
            ChkFetchVideos.IsChecked = _settings.ScraperFetchVideos;

            ChkOverwriteExisting.IsChecked = _settings.ScraperOverwriteExisting;
        }

        // Save side: writes each checkbox's current state back into ConfigurationSettings. Called by
        // OptionsWindow's save handler before persisting settings.json.
        public void SyncToSettings()
        {
            _settings.ScraperEnabled = ChkScraperEnabled.IsChecked ?? false;

            _settings.ScraperFetchOnLaunch = ChkFetchOnLaunch.IsChecked ?? false;
            _settings.ScraperFetchOnContextMenu = ChkFetchOnContextMenu.IsChecked ?? false;

            _settings.ScraperFetchMarquees = ChkFetchMarquees.IsChecked ?? false;
            _settings.ScraperFetchFlyers = ChkFetchFlyers.IsChecked ?? false;
            _settings.ScraperFetchTitlescreens = ChkFetchTitlescreens.IsChecked ?? false;
            _settings.ScraperFetchSnaps = ChkFetchSnaps.IsChecked ?? false;
            _settings.ScraperFetchCabinets = ChkFetchCabinets.IsChecked ?? false;
            _settings.ScraperFetchVideos = ChkFetchVideos.IsChecked ?? false;

            _settings.ScraperOverwriteExisting = ChkOverwriteExisting.IsChecked ?? false;
        }
        // [END SECTION: Load / Save Sync]

        // [SECTION: Test Connection]
        // Calls ADB's download_status endpoint and displays the remaining bandwidth headroom. Doesn't
        // require the scraper to be enabled/saved first - this is a pure connectivity/status check,
        // independent of whether the feature itself is currently turned on.
        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (_scraperService == null) return;

            BtnTestConnection.IsEnabled = false;
            TxtScraperStatus.Text = "Checking...";

            var status = await _scraperService.CheckDownloadStatusAsync();

            if (status == null)
            {
                TxtScraperStatus.Text = "Couldn't reach the artwork database. Check your connection and try again.";
            }
            else
            {
                double remainingGb = status.DownloadLimitBytes / 1024.0 / 1024.0 / 1024.0;
                TxtScraperStatus.Text = $"Connected. {status.DownloadLimitFiles:N0} files / {remainingGb:N1} GB remaining today.";
            }

            BtnTestConnection.IsEnabled = true;
        }
        // [END SECTION: Test Connection]
    }
}