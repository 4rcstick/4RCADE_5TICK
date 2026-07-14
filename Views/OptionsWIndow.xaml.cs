// [SECTION: File Overrides] - Theme Builder management and initialization engine
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

        // [SECTION: Constructor & Tab Initialization]
        // Anchors DataContext to the live ConfigurationSettings, then initializes each tab UserControl
        // (loading its portion of settings). ThemesTab additionally gets callbacks for persisting to disk
        // and refreshing this window's bindings, since Theme Builder can save/load independently of the
        // main Save Adjustments button.
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
        // [END SECTION: Constructor & Tab Initialization]

        // [SECTION: Options Window Binding Refresh]
        // Forces every binding in this window to re-evaluate by briefly clearing and restoring
        // DataContext. Used after Theme Builder saves/loads a theme so controls reflect the new values
        // immediately without closing/reopening the Options window.
        private void RefreshOptionsWindowBindings()
        {
            this.DataContext = null;
            this.DataContext = _settings;
        }
        // [END SECTION: Options Window Binding Refresh]

        // [SECTION: Settings Persistence]
        // Serializes the full ConfigurationSettings object to settings.json in 4rcade5tick_files.
        private void PersistSettingsToDisk()
        {
            string configFilePath = Path.Combine(_settings.GetArcadeStickFilesPath(), "settings.json");
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string jsonString = System.Text.Json.JsonSerializer.Serialize(_settings, jsonOptions);
            File.WriteAllText(configFilePath, jsonString);
        }
        // [END SECTION: Settings Persistence]

        // [SECTION: Save Button Handler]
        // Syncs SystemPaths/AssetPaths/Themes/Inputs tabs into ConfigurationSettings, persists to disk,
        // writes MameIniTab's own settings (video/frameskip/checkboxes) directly to mame.ini, refreshes
        // MainViewModel's live theme/folder-color bindings, then refreshes this window's own bindings.
        // NOTE: MameIniTab.UpdateMameIniSettings() intentionally does NOT touch rompath - that's only
        // written by MainViewModel's boot-time sync. Don't swap this for a method that rebuilds rompath
        // without re-checking that history.
        private void BtnSaveOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemPathsTab.SyncToSettings();
                AssetPathsTab.SyncToSettings();
                ThemesTab.SyncUiToSettings();
                InputsTab.SyncToSettings();

                PersistSettingsToDisk();

                MameIniTab.UpdateMameIniSettings();

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
        // [END SECTION: Save Button Handler]

        // [SECTION: Live Diagnostics Passthrough]
        // Forwards the active WGIService to the Inputs tab so it can wire up port status/input readout events.
        public void WireLiveDiagnostics(ArcadeStick.Services.WGIService inputService)
        {
            InputsTab.WireLiveDiagnostics(inputService);
        }
        // [END SECTION: Live Diagnostics Passthrough]

        private void BtnCloseOptions_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
// [END SECTION: File Overrides]