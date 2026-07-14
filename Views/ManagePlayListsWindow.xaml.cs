// 🏗️ START: THEME-CONNECTED OVERRIDES FOR FOLDER DIALOG CONTEXT
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

        private void ApplyThemeConfigurations()
        {
            // Fallback rule mapping: Inherit background color from main window context if blank[cite: 3]
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

            // Apply global user typography size definitions[cite: 3]
            FolderNameTextBox.FontSize = _viewModel.Configuration.InputFontSize;
            HexColorTextBox.FontSize = _viewModel.Configuration.InputFontSize;

            try
            {
                var fontBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(_viewModel.Configuration.InputColorHex)!; //[cite: 3]
                FolderNameTextBox.Foreground = fontBrush;
                HexColorTextBox.Foreground = fontBrush;
            }
            catch { }
        }

        private void InitializeDropdown()
        {
            FolderComboBox.Items.Clear(); //[cite: 8]
            FolderComboBox.Items.Add("-- Create New Custom Folder --"); //[cite: 8]

            if (Directory.Exists(_playlistsDir)) //[cite: 8]
            {
                var files = Directory.GetFiles(_playlistsDir, "*.cfg"); //[cite: 8]
                foreach (var file in files) //[cite: 8]
                {
                    FolderComboBox.Items.Add(Path.GetFileNameWithoutExtension(file)); //[cite: 8]
                }
            }

            FolderComboBox.SelectedIndex = 0; //[cite: 8]
            FolderNameTextBox.Text = ""; //[cite: 8]
            HexColorTextBox.Text = "#FFFFFF"; //[cite: 8]

            RemoveButton.IsEnabled = false; //[cite: 8]
            DeleteFolderButton.IsEnabled = false; //[cite: 8]
        }

        private void HookEventHandlers()
        {
            FolderComboBox.SelectionChanged += FolderComboBox_SelectionChanged; //[cite: 8]
            ConfirmButton.Click += ConfirmButton_Click; //[cite: 8]
            CancelButton.Click += (s, e) => Close(); //[cite: 8]
            DeleteFolderButton.Click += DeleteFolderButton_Click; //[cite: 8]
            RemoveButton.Click += RemoveButton_Click; //[cite: 8]

            HexColorTextBox.TextChanged += (s, e) => _selectedColorHex = HexColorTextBox.Text.Trim(); //[cite: 8]
        }

        private void FolderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderComboBox.SelectedIndex <= 0) //[cite: 8]
            {
                FolderNameTextBox.Visibility = Visibility.Visible; //[cite: 8]
                FolderNameTextBox.IsEnabled = true; //[cite: 8]
                FolderNameTextBox.Text = ""; //[cite: 8]
                HexColorTextBox.Text = "#FFFFFF"; //[cite: 8]
                _selectedColorHex = "#FFFFFF"; //[cite: 8]

                RemoveButton.IsEnabled = false; //[cite: 8]
                DeleteFolderButton.IsEnabled = false; //[cite: 8]
            }
            else
            {
                string selectedPlaylist = FolderComboBox.SelectedItem.ToString()!; //[cite: 8]
                FolderNameTextBox.Text = selectedPlaylist; //[cite: 8]
                FolderNameTextBox.Visibility = Visibility.Collapsed; //[cite: 8]
                FolderNameTextBox.IsEnabled = false; //[cite: 8]

                RemoveButton.IsEnabled = true; //[cite: 8]
                DeleteFolderButton.IsEnabled = true; //[cite: 8]

                string filePath = Path.Combine(_playlistsDir, $"{selectedPlaylist}.cfg"); //[cite: 8]
                if (File.Exists(filePath)) //[cite: 8]
                {
                    var lines = File.ReadAllLines(filePath); //[cite: 8]
                    if (lines.Length > 0) //[cite: 8]
                    {
                        _selectedColorHex = lines[0]; //[cite: 8]
                        HexColorTextBox.Text = _selectedColorHex; //[cite: 8]
                    }
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            string playlistName = FolderNameTextBox.Text.Trim(); //[cite: 8]
            if (string.IsNullOrEmpty(playlistName) || playlistName.StartsWith("--")) //[cite: 8]
            {
                MessageBox.Show("Please specify a valid playlist identification string.", "Validation Error"); //[cite: 8]
                return; //[cite: 8]
            }

            string hexInput = _selectedColorHex.Trim(); //[cite: 8]
            if (!hexInput.StartsWith("#")) //[cite: 8]
            {
                hexInput = "#" + hexInput; //[cite: 8]
            }

            if ((hexInput.Length != 7 && hexInput.Length != 9) || !hexInput.Skip(1).All(c => Uri.IsHexDigit(c))) //[cite: 8]
            {
                MessageBox.Show("Invalid HEX color format. Falling back to #FFFFFF.", "Color Specification Warning"); //[cite: 8]
                hexInput = "#FFFFFF"; //[cite: 8]
            }
            _selectedColorHex = hexInput; //[cite: 8]

            if (FolderComboBox.SelectedIndex <= 0) //[cite: 8]
            {
                string checkPath = Path.Combine(_playlistsDir, $"{playlistName}.cfg"); //[cite: 8]
                if (File.Exists(checkPath)) //[cite: 8]
                {
                    MessageBox.Show($"A playlist named '{playlistName}' already exists. Please choose it from the dropdown or pick a unique name.", "Duplicate Entry Error"); //[cite: 8]
                    return; //[cite: 8]
                }
            }

            if (_viewModel.SelectedGame == null) return; //[cite: 8]

            string filePath = Path.Combine(_playlistsDir, $"{playlistName}.cfg"); //[cite: 8]
            System.Collections.Generic.List<string> fileLines = new(); //[cite: 8]

            if (File.Exists(filePath)) //[cite: 8]
            {
                fileLines = File.ReadAllLines(filePath).ToList(); //[cite: 8]
            }

            if (fileLines.Count == 0) //[cite: 8]
            {
                fileLines.Add(_selectedColorHex); //[cite: 8]
            }
            else
            {
                fileLines[0] = _selectedColorHex; //[cite: 8]
            }

            string targetRom = _viewModel.SelectedGame.RomName; //[cite: 8]
            if (!fileLines.Skip(1).Contains(targetRom)) //[cite: 8]
            {
                fileLines.Add(targetRom); //[cite: 8]
            }

            File.WriteAllLines(filePath, fileLines); //[cite: 8]

            _viewModel.UpdateLiveTreeDisplay(); //[cite: 8]
            Close(); //[cite: 8]
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderComboBox.SelectedIndex <= 0 || _viewModel.SelectedGame == null) return; //[cite: 8]

            string selectedPlaylist = FolderComboBox.SelectedItem.ToString()!; //[cite: 8]
            string filePath = Path.Combine(_playlistsDir, $"{selectedPlaylist}.cfg"); //[cite: 8]

            if (File.Exists(filePath)) //[cite: 8]
            {
                var lines = File.ReadAllLines(filePath).ToList(); //[cite: 8]
                string targetRom = _viewModel.SelectedGame.RomName; //[cite: 8]

                if (lines.Remove(targetRom)) //[cite: 8]
                {
                    File.WriteAllLines(filePath, lines); //[cite: 8]
                    MessageBox.Show($"{targetRom} has been safely removed from playlist '{selectedPlaylist}'."); //[cite: 8]
                    InitializeDropdown(); //[cite: 8]
                    _viewModel.UpdateLiveTreeDisplay(); //[cite: 8]
                }
                else
                {
                    MessageBox.Show($"{targetRom} was not found inside playlist '{selectedPlaylist}'."); //[cite: 8]
                }
            }
        }

        private void DeleteFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderComboBox.SelectedIndex <= 0) return; //[cite: 8]

            string selectedPlaylist = FolderComboBox.SelectedItem.ToString()!; //[cite: 8]
            string filePath = Path.Combine(_playlistsDir, $"{selectedPlaylist}.cfg"); //[cite: 8]

            var checkResult = MessageBox.Show($"Are you sure you want to completely erase the '{selectedPlaylist}' configuration file?", "Confirm Deletion", MessageBoxButton.YesNo); //[cite: 8]
            if (checkResult == MessageBoxResult.Yes) //[cite: 8]
            {
                if (File.Exists(filePath)) //[cite: 8]
                {
                    File.Delete(filePath); //[cite: 8]
                }
                InitializeDropdown(); //[cite: 8]
                _viewModel.UpdateLiveTreeDisplay(); //[cite: 8]
            }
        }
    }
}
// 🏗️ END: THEME-CONNECTED OVERRIDES FOR FOLDER DIALOG CONTEXT