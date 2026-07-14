using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace ArcadeStick.Views
{
    // [SECTION: About Tab Control]
    // Code-behind for the About tab: opens external links (GitHub, Buy Me a Coffee) in the system browser.
    public partial class AboutTabControl : UserControl
    {
        public AboutTabControl()
        {
            InitializeComponent();
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
    }
    // [END SECTION: About Tab Control]
}