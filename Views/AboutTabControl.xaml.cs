using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace ArcadeStick.Views
{
    public partial class AboutTabControl : UserControl
    {
        public AboutTabControl()
        {
            InitializeComponent();
        }

        private void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/4rcstick/4RCADE_5TICK") { UseShellExecute = true });
        }

        private void BmacImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://buymeacoffee.com/4rchimede5") { UseShellExecute = true });
        }
    }
}