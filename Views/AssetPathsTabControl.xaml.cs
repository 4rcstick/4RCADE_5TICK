using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class AssetPathsTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;

        public AssetPathsTabControl()
        {
            InitializeComponent();
        }

        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;

            TxtMarqueesPath.Text = _settings.MarqueesPath;
            TxtVideosPath.Text = _settings.VideosPath;
            TxtFlyersPath.Text = _settings.FlyersPath;
            TxtGameplayPath.Text = _settings.ScreenshotsPath;
            TxtTitlescreensPath.Text = _settings.TitlescreensPath;
            TxtCabinetsPath.Text = _settings.CabinetsPath;
        }

        public void SyncToSettings()
        {
            _settings.MarqueesPath = TxtMarqueesPath.Text;
            _settings.VideosPath = TxtVideosPath.Text;
            _settings.FlyersPath = TxtFlyersPath.Text;
            _settings.ScreenshotsPath = TxtGameplayPath.Text;
            _settings.TitlescreensPath = TxtTitlescreensPath.Text;
            _settings.CabinetsPath = TxtCabinetsPath.Text;
        }

        private void BtnBrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                TextBox? associatedTextBox = null;
                switch (clickedButton.Name)
                {
                    case "BtnBrowseMarquees": associatedTextBox = TxtMarqueesPath; break;
                    case "BtnBrowseVideos": associatedTextBox = TxtVideosPath; break;
                    case "BtnBrowseFlyers": associatedTextBox = TxtFlyersPath; break;
                    case "BtnBrowseGameplay": associatedTextBox = TxtGameplayPath; break;
                    case "BtnBrowseTitlescreens": associatedTextBox = TxtTitlescreensPath; break;
                    case "BtnBrowseCabinets": associatedTextBox = TxtCabinetsPath; break;
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