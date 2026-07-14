using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class SystemPathsTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;

        public SystemPathsTabControl()
        {
            InitializeComponent();
        }

        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;

            TxtRomsPath.Text = _settings.RomsSubFolder;
            TxtBiosPath.Text = _settings.BiosPath;
            TxtChdPath.Text = _settings.ChdPath;
        }

        public void SyncToSettings()
        {
            _settings.RomsSubFolder = TxtRomsPath.Text;
            _settings.BiosPath = TxtBiosPath.Text;
            _settings.ChdPath = TxtChdPath.Text;
        }

        private void BtnBrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                TextBox? associatedTextBox = null;
                switch (clickedButton.Name)
                {
                    case "BtnBrowseRoms": associatedTextBox = TxtRomsPath; break;
                    case "BtnBrowseBios": associatedTextBox = TxtBiosPath; break;
                    case "BtnBrowseChd": associatedTextBox = TxtChdPath; break;
                }

                string mameRoot = _settings.GetMamePath();
                string initialDir = mameRoot;

                if (associatedTextBox != null && !string.IsNullOrWhiteSpace(associatedTextBox.Text))
                {
                    try
                    {
                        string boxText = associatedTextBox.Text.Trim();
                        string currentPathInBox = System.IO.Path.GetFullPath(boxText, mameRoot);
                        if (System.IO.Directory.Exists(currentPathInBox))
                        {
                            initialDir = currentPathInBox;
                        }
                    }
                    catch
                    {
                        initialDir = mameRoot;
                    }
                }

                Microsoft.Win32.OpenFolderDialog folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Target Arcade Resource Directory",
                    Multiselect = false,
                    InitialDirectory = initialDir
                };

                if (folderDialog.ShowDialog() == true)
                {
                    string selectedPath = folderDialog.FolderName;
                    string appDrive = System.IO.Path.GetPathRoot(mameRoot) ?? "";
                    string selectedDrive = System.IO.Path.GetPathRoot(selectedPath) ?? "";

                    if (!string.IsNullOrEmpty(appDrive) && appDrive.Equals(selectedDrive, System.StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = System.IO.Path.GetRelativePath(mameRoot, selectedPath);
                        selectedPath = relativePath == "." ? "." : (!relativePath.StartsWith("..") ? relativePath : selectedPath);
                    }

                    if (associatedTextBox != null) associatedTextBox.Text = selectedPath;
                }
            }
        }
    }
}