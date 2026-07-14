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

        // [SECTION: Load / Save Sync]
        // Load side: populates ROMs/BIOS/CHD path TextBoxes from ConfigurationSettings. Called by
        // OptionsWindow when this tab is shown.
        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;

            TxtRomsPath.Text = _settings.RomsSubFolder;
            TxtBiosPath.Text = _settings.BiosPath;
            TxtChdPath.Text = _settings.ChdPath;
        }

        // Save side: writes each TextBox's current text back into ConfigurationSettings. NOTE: these
        // paths feed the boot-time ROM scan (see MainViewModel.InitializeDatabaseAsync /
        // SyncMameRomPathsAsync) - changes here require an app restart to actually take effect, they're
        // not picked up live.
        public void SyncToSettings()
        {
            _settings.RomsSubFolder = TxtRomsPath.Text;
            _settings.BiosPath = TxtBiosPath.Text;
            _settings.ChdPath = TxtChdPath.Text;
        }
        // [END SECTION: Load / Save Sync]

        // [SECTION: Folder Browse Handler]
        // Shared handler for all three "..." browse buttons. Resolves which TextBox to update from the
        // clicked button's name, opens a folder picker starting from the current path (or the MAME root
        // if unset/invalid), then converts the selected path back to a relative path when it's on the same
        // drive as the MAME root - keeps paths portable for USB/relocatable deployments.
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

                // Start the picker at the currently configured path if it's valid, otherwise fall back to MAME root
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

                    // Same-drive selections get converted to a relative path for portability;
                    // cross-drive selections are kept absolute since a relative path wouldn't make sense
                    if (!string.IsNullOrEmpty(appDrive) && appDrive.Equals(selectedDrive, System.StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = System.IO.Path.GetRelativePath(mameRoot, selectedPath);
                        selectedPath = relativePath == "." ? "." : (!relativePath.StartsWith("..") ? relativePath : selectedPath);
                    }

                    if (associatedTextBox != null) associatedTextBox.Text = selectedPath;
                }
            }
        }
        // [END SECTION: Folder Browse Handler]
    }
}