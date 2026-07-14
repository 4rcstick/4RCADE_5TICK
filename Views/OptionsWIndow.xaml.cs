// 🏗️ START: THEME BUILDER MANAGEMENT AND INITIALIZATION ENGINE
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    public partial class OptionsWindow : Window
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;
        private ArcadeStick.ViewModels.MainViewModel _viewModel;

        public OptionsWindow(ArcadeStick.ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _settings = viewModel.Configuration;

            // Anchor the DataContext so container UI bindings hook directly into the active settings framework
            this.DataContext = _settings;

            // System Paths Folder Reference Mappings with Consistent Portable Formatting
            SystemPathsTab.Initialize(_settings);

            // Custom Media Asset Storage Path Fields
            AssetPathsTab.Initialize(_settings);

            ThemesTab.Initialize(_viewModel, _settings, PersistSettingsToDisk, RefreshOptionsWindowBindings);

            InputsTab.Initialize(_settings);

            MameIniTab.Initialize(_settings);
            FolderOrderTab.Initialize(_viewModel, _settings);
            PreviewsOrderTab.Initialize(_viewModel, _settings);
        }

        private void RefreshOptionsWindowBindings()
        {
            this.DataContext = null;
            this.DataContext = _settings;
        }

        private void PersistSettingsToDisk()
        {
            string configFilePath = Path.Combine(_settings.GetArcadeStickFilesPath(), "settings.json");
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string jsonString = System.Text.Json.JsonSerializer.Serialize(_settings, jsonOptions);
            File.WriteAllText(configFilePath, jsonString);
        }

        private void BtnSaveOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemPathsTab.SyncToSettings();
                AssetPathsTab.SyncToSettings();
                ThemesTab.SyncUiToSettings();
                InputsTab.SyncToSettings();

                PersistSettingsToDisk();

                _viewModel.RefreshThemeBindings();
                _viewModel.RefreshFolderColorsLive();

                this.DataContext = null;
                this.DataContext = _settings;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save local theme settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void WireLiveDiagnostics(ArcadeStick.Services.WGIService inputService)
        {
            InputsTab.WireLiveDiagnostics(inputService);
        }

        private void BtnCloseOptions_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
// 🏗️ END: THEME BUILDER MANAGEMENT AND INITIALIZATION ENGINE