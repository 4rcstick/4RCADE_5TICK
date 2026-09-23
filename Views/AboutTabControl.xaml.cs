using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcadeStick.Views
{
    // [SECTION: About Tab Control]
    // Code-behind for the About tab: opens external links (GitHub, Buy Me a Coffee) in the system browser.
    public partial class AboutTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;

        public AboutTabControl()
        {
            InitializeComponent();
        }

        // Load side: needed here only so ReadmeLink_Click can resolve the readme's portable path.
        // Called by OptionsWindow when this tab is shown, same pattern as every other tab.
        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;
        }

        // Opens the project's GitHub repo in the default browser
        private void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/4rcstick/4RCADE_5TICK") { UseShellExecute = true });
        }

        // Opens the Buy Me a Coffee donation page in the default browser
        private void BmacImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://buymeacoffee.com/4rchimede5") { UseShellExecute = true });
        }

        // Opens Wikipedia in the default browser (game info text source attribution)
        private void WikipediaLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.wikipedia.org") { UseShellExecute = true });
        }

        // Opens the CC BY-SA 4.0 license deed in the default browser (required by Wikipedia's license)
        private void CcBySaLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://creativecommons.org/licenses/by-sa/4.0/") { UseShellExecute = true });
        }

        // Opens Arcade-History in the default browser (history_arcade.xml source attribution)
        private void ArcadeHistoryLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.arcade-history.com") { UseShellExecute = true });
        }

        // Opens the Arcade Database (ADB) in the default browser (scraper artwork source attribution)
        private void ArcadeDatabaseLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://adb.arcadeitalia.net") { UseShellExecute = true });
        }

        // Opens the PNGtree.com in the default browser
        private void PNGtreeLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://pngtree.com") { UseShellExecute = true });
        }

        // Link to readme.txt
        private void ReadmeLink_Click(object sender, RoutedEventArgs e)
        {
            string readmePath = Path.Combine(_settings.GetArcadeStickFilesPath(), "readme.txt");
            Process.Start(new ProcessStartInfo(readmePath) { UseShellExecute = true });
        }
    }
    // [END SECTION: About Tab Control]
}