// [SECTION: File Overrides] - Theme-connected overrides for the folder dialog context
using ArcadeStick.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    public partial class ManagePlayListsWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly string _playlistsDir;
        private string _selectedColorHex = "#FFFFFF";

        // [SECTION: Constructor & Playlist Context Setup]
        // Ensures the playlists config folder exists, applies theme colors/fonts, populates the folder
        // dropdown, wires event handlers, and - if opened with a game selected that already belongs to a
        // visible custom folder - pre-selects that folder so the dialog opens in edit mode for it.
        public ManagePlayListsWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel.Configuration;

            // Direct directory sync with your central portable Configuration engine path
            _playlistsDir = Path.Combine(_viewModel.Configuration.GetConfigPath(), "playlists");

            if (!Directory.Exists(_playlistsDir))
            {
                Directory.CreateDirectory(_playlistsDir);
            }

            ApplyThemeConfigurations();
            InitializeDropdown();
            HookEventHandlers();

            // Auto-select playlist context if opened from a virtual playlist node
            if (_viewModel.SelectedGame != null)
            {
                var activeNode = _viewModel.TreeNodesCollection.FirstOrDefault(n =>
                    n.ChildGames.Contains(_viewModel.SelectedGame) &&
                    FolderComboBox.Items.Cast<string>().Any(item => item.Equals(n.HeaderText, StringComparison.OrdinalIgnoreCase)));

                if (activeNode != null)
                {
                    var matchedItem = FolderComboBox.Items.Cast<string>()
                        .FirstOrDefault(item => item.Equals(activeNode.HeaderText, StringComparison.OrdinalIgnoreCase));

                    if (matchedItem != null)
                    {
                        FolderComboBox.SelectedItem = matchedItem;
                    }
                }
            }

            Loaded += (s, e) => FolderNameTextBox.Focus();
        }
        // [END SECTION: Constructor & Playlist Context Setup]

        // [SECTION: Theme Application]
        // Applies the current theme's background color (falling back to main window background if
        // OptionsBg is unset) and input font size/color to this dialog's text fields.
        private void ApplyThemeConfigurations()
        {
            // Fallback rule mapping: Inherit background color from main window context if blank
            string backgroundHex = !string.IsNullOrWhiteSpace(_viewModel.Configuration.OptionsBg)
                ? _viewModel.Configuration.OptionsBg
                : _viewModel.Configuration.BackgroundColor;

            try
            {
                this.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(backgroundHex)!;
            }
            catch
            {
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E24"));
            }

            // Apply global user typography size definitions
            FolderNameTextBox.FontSize = _viewModel.Configuration.InputFontSize;
            HexColorTextBox.FontSize = _viewModel.Configuration.InputFontSize;

            try
            {
                var fontBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(_viewModel.Configuration.InputColorHex)!;
                FolderNameTextBox.Foreground = fontBrush;
                HexColorTextBox.Foreground = fontBrush;
            }
            catch { }
        }
        // [END SECTION: Theme Application]

        // [SECTION: Dropdown Initialization]
        // Resets FolderComboBox to "-- Create New Custom Folder --" plus every *.cfg playlist on disk,
        // and resets the form fields/buttons back to their default "create new" state.
        private void InitializeDropdown()
        {
            FolderComboBox.Items.Clear();
            FolderComboBox.Items.Add("-- Create New Custom Folder --");

            if (Directory.Exists(_playlistsDir))
            {
                var files = Directory.GetFiles(_playlistsDir, "*.cfg");
                foreach (var file in files)
                {
                    FolderComboBox.Items.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            FolderComboBox.SelectedIndex = 0;
            FolderNameTextBox.Text = "";
            HexColorTextBox.Text = "#FFFFFF";

            RemoveButton.IsEnabled = false;
            DeleteFolderButton.IsEnabled = false;
        }
        // [END SECTION: Dropdown Initialization]

        // [SECTION: Event Wiring]
        private void HookEventHandlers()
        {
            FolderComboBox.SelectionChanged += FolderComboBox_SelectionChanged;
            ConfirmButton.Click += ConfirmButton_Click;
            CancelButton.Click += (s, e) => Close();
            DeleteFolderButton.Click += DeleteFolderButton_Click;
            RemoveButton.Click += RemoveButton_Click;

            HexColorTextBox.TextChanged += (s, e) => _selectedColorHex = HexColorTextBox.Text.Trim();
        }
        // [END SECTION: Event Wiring]

        // [SECTION: Selection Changed - Create vs Edit Mode]
        // Toggles the dialog between "create new folder" mode (index 0: name field visible/editable, no
        // remove/delete) and "edit existing folder" mode (name field locked to the selected playlist,
        // remove/delete enabled, existing color loaded from the playlist file's first line).
        private void FolderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderComboBox.SelectedIndex <= 0)
            {
                FolderNameTextBox.Visibility = Visibility.Visible;
                FolderNameTextBox.IsEnabled = true;
                FolderNameTextBox.Text = "";
                HexColorTextBox.Text = "#FFFFFF";
                _selectedColorHex = "#FFFFFF";

                RemoveButton.IsEnabled = false;
                DeleteFolderButton.IsEnabled = false;
            }
            else
            {
                string selectedPlaylist = FolderComboBox.SelectedItem.ToString()!;
                FolderNameTextBox.Text = selectedPlaylist;
                FolderNameTextBox.Visibility = Visibility.Collapsed;
                FolderNameTextBox.IsEnabled = false;

                RemoveButton.IsEnabled = true;
                DeleteFolderButton.IsEnabled = true;

                string filePath = Path.Combine(_playlistsDir, $"{selectedPlaylist}.cfg");
                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length > 0)
                    {
                        _selectedColorHex = lines[0];
                        HexColorTextBox.Text = _selectedColorHex;
                    }
                }
            }
        }
        // [END SECTION: Selection Changed - Create vs Edit Mode]

        // [SECTION: Confirm / Save]
        // Validates the folder name and hex color (falling back to #FFFFFF on invalid input), guards
        // against creating a duplicate folder name, then writes/updates the playlist .cfg file (line 0 is
        // always the hex color, remaining lines are ROM names) adding the currently selected game if it
        // isn't already present.
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            string playlistName = FolderNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(playlistName) || playlistName.StartsWith("--"))
            {
                MessageBox.Show("Please specify a valid playlist identification string.", "Validation Error");
                return;
            }

            string hexInput = _selectedColorHex.Trim();
            if (!hexInput.StartsWith("#"))
            {
                hexInput = "#" + hexInput;
            }

            if ((hexInput.Length != 7 && hexInput.Length != 9) || !hexInput.Skip(1).All(c => Uri.IsHexDigit(c)))
            {
                MessageBox.Show("Invalid HEX color format. Falling back to #FFFFFF.", "Color Specification Warning");
                hexInput = "#FFFFFF";
            }
            _selectedColorHex = hexInput;

            if (FolderComboBox.SelectedIndex <= 0)
            {
                string checkPath = Path.Combine(_playlistsDir, $"{playlistName}.cfg");
                if (File.Exists(checkPath))
                {
                    MessageBox.Show($"A playlist named '{playlistName}' already exists. Please choose it from the dropdown or pick a unique name.", "Duplicate Entry Error");
                    return;
                }
            }

            if (_viewModel.SelectedGame == null) return;

            string filePath = Path.Combine(_playlistsDir, $"{playlistName}.cfg");
            System.Collections.Generic.List<string> fileLines = new();

            if (File.Exists(filePath))
            {
                fileLines = File.ReadAllLines(filePath).ToList();
            }

            if (fileLines.Count == 0)
            {
                fileLines.Add(_selectedColorHex);
            }
            else
            {
                fileLines[0] = _selectedColorHex;
            }

            string targetRom = _viewModel.SelectedGame.RomName;
            if (!fileLines.Skip(1).Contains(targetRom))
            {
                fileLines.Add(targetRom);
            }

            File.WriteAllLines(filePath, fileLines);

            _viewModel.UpdateLiveTreeDisplay();
            Close();
        }
        // [END SECTION: Confirm / Save]

        // [SECTION: Remove From Playlist]
        // Removes the currently selected game's ROM name from the selected playlist's .cfg file, then
        // refreshes the dropdown and live tree.
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderComboBox.SelectedIndex <= 0 || _viewModel.SelectedGame == null) return;

            string selectedPlaylist = FolderComboBox.SelectedItem.ToString()!;
            string filePath = Path.Combine(_playlistsDir, $"{selectedPlaylist}.cfg");

            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath).ToList();
                string targetRom = _viewModel.SelectedGame.RomName;

                if (lines.Remove(targetRom))
                {
                    File.WriteAllLines(filePath, lines);
                    MessageBox.Show($"{targetRom} has been safely removed from playlist '{selectedPlaylist}'.");
                    InitializeDropdown();
                    _viewModel.UpdateLiveTreeDisplay();
                }
                else
                {
                    MessageBox.Show($"{targetRom} was not found inside playlist '{selectedPlaylist}'.");
                }
            }
        }
        // [END SECTION: Remove From Playlist]

        // [SECTION: Delete Playlist]
        // Prompts for confirmation, then deletes the selected playlist's .cfg file entirely and refreshes
        // the dropdown and live tree.
        private void DeleteFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderComboBox.SelectedIndex <= 0) return;

            string selectedPlaylist = FolderComboBox.SelectedItem.ToString()!;
            string filePath = Path.Combine(_playlistsDir, $"{selectedPlaylist}.cfg");

            var checkResult = MessageBox.Show($"Are you sure you want to completely erase the '{selectedPlaylist}' configuration file?", "Confirm Deletion", MessageBoxButton.YesNo);
            if (checkResult == MessageBoxResult.Yes)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                InitializeDropdown();
                _viewModel.UpdateLiveTreeDisplay();
            }
        }
        // [END SECTION: Delete Playlist]
    }
}
// [END SECTION: File Overrides]