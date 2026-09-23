using System;
using System.Windows;
using Microsoft.Win32;

namespace ArcadeStick.Views
{
    public partial class RomCopyPathWindow : Window
    {
        private readonly ViewModels.MainViewModel _viewModel;

        // [SECTION: Lifecycle & Dependency Injection]
        // Pre-fills the text field with whatever destination is currently saved (empty on first use).
        public RomCopyPathWindow(ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DestinationPathTextBox.Text = _viewModel.Configuration.RomCopyDestinationPath;
        }
        // [END SECTION: Lifecycle & Dependency Injection]

        // [SECTION: Folder Browse]
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select ROM Copy Destination Folder"
            };

            if (!string.IsNullOrWhiteSpace(DestinationPathTextBox.Text) &&
                System.IO.Directory.Exists(DestinationPathTextBox.Text))
            {
                dialog.InitialDirectory = DestinationPathTextBox.Text;
            }

            if (dialog.ShowDialog() == true)
            {
                DestinationPathTextBox.Text = dialog.FolderName;
            }
        }
        // [END SECTION: Folder Browse]

        // [SECTION: Save / Cancel]
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Configuration.RomCopyDestinationPath = DestinationPathTextBox.Text.Trim();
            _viewModel.PersistConfigurationToDisk();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        // [END SECTION: Save / Cancel]
    }
}