using System;
using System.IO;
using System.Windows;

namespace ArcadeStick.Views
{
    public partial class ThemeBuilderWindow : Window
    {
        private readonly ArcadeStick.Models.ConfigurationSettings _settings;
        private readonly ArcadeStick.ViewModels.MainViewModel _viewModel;

        private System.Collections.Generic.List<ArcadeStick.Models.ThemeLayout> _layoutsAtLastSave = new();
        private int _resolutionWidthAtLastSave;
        private int _resolutionHeightAtLastSave;
        // The flat Global tab values at the same restore point (theme-wide, so the size switch never moves it)
        private ArcadeStick.Models.ThemeSettings _globalAtLastSave = new();

        // [SECTION: Constructor & Tab Initialization]
        // Anchors DataContext to the live ConfigurationSettings (same as OptionsWindow, so the window
        // inherits the exact same theme bindings), then hands ThemesTab the persist/refresh callbacks
        // it needs to save settings.json and re-evaluate this window's bindings independently.
        public ThemeBuilderWindow(ArcadeStick.ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _settings = viewModel.Configuration;

            this.DataContext = _settings;

            ThemesTab.Initialize(_viewModel, _settings, PersistSettingsToDisk, RefreshAfterThemeLoad);
            ThemesTab.SetThemeSavedCallback(() =>
            {
                AdvancedTab.RefreshSavedSizes();
                // Save Changes wrote the theme zip (which includes the current size), so a pending size is now kept
                AdvancedTab.CommitPendingSize();
                // what was just saved becomes the close restore point
                _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
                _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
                _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
                _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
            });
            AdvancedTab.Initialize(_settings);
            GlobalTab.Initialize(_settings);

            // Alignment guides: forward the Advanced tab's guide info to the main window, but only while the
            // Advanced tab is actually the selected tab. Cleared when switching to Basic or closing.
            AdvancedTab.GuidesChanged += info =>
            {
                if (ThemeBuilderTabControl.SelectedIndex != 1) info = null;
                (Owner as MainWindow)?.ShowGuides(info);
            };

            ThemeBuilderTabControl.SelectionChanged += (s, e) =>
            {
                // SelectionChanged bubbles up from the ListBox and ComboBox inside the tabs - only react to the tab strip itself
                if (!ReferenceEquals(e.OriginalSource, ThemeBuilderTabControl)) return;

                if (ThemeBuilderTabControl.SelectedIndex == 1) AdvancedTab.RaiseGuides();
                else (Owner as MainWindow)?.ShowGuides(null);
            };

            // Live layout: every committed Advanced edit re-applies the layout on the main window.
            // Nothing is written to disk here - only Save Adjustments / Save Changes do that.
            AdvancedTab.LayoutChanged += () => (Owner as MainWindow)?.ApplyLayoutNow();

            // Restore point for closing without saving: the layouts and Global values as of window open / last Save Adjustments.
            _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
            _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);

            Closed += (s, e) =>
            {
                // Put back the last saved layouts so abandoned edits don't linger on the main window
                _settings.Layouts = _layoutsAtLastSave;
                _settings.ActiveResolutionWidth = _resolutionWidthAtLastSave;
                _settings.ActiveResolutionHeight = _resolutionHeightAtLastSave;

                // Same for the Global values, then republish their app resources
                ArcadeStick.Services.GlobalThemeResources.RestoreSnapshot(_globalAtLastSave, _settings, ArcadeStick.Services.GlobalArea.All);
                ArcadeStick.Services.GlobalThemeResources.Apply(_settings);

                var main = Owner as MainWindow;
                main?.ShowGuides(null);
                main?.ApplyLayoutNow();
            };
        }

        // Passed to ThemesTab as its refresh callback. A theme load replaces _settings.Layouts and the Global
        // values, so the Advanced and Global tabs re-read them in addition to the normal binding refresh.
        // Save Adjustments deliberately calls RefreshWindowBindings only, so the Advanced tab keeps its
        // current layer selection when you save.
        private void RefreshAfterThemeLoad()
        {
            RefreshWindowBindings();
            AdvancedTab.ReloadFromSettings();
            GlobalTab.ReloadFromSettings();

            // A theme load is a deliberate change: the loaded layouts and Global values become the restore point for closing
            _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
            _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
        }

        // Called by MainWindow after the theme is switched from the hamburger menu. Reloads both tabs and
        // moves the close-without-saving restore point to the newly loaded theme.
        public void RefreshAfterExternalThemeSwitch()
        {
            ThemesTab.ReloadFromSettings();
            RefreshAfterThemeLoad();
        }

        // Called by MainWindow after the layout size is switched from the hamburger menu. Only the Advanced
        // tab is reloaded, so unsaved Basic tab edits survive. Only the size restore point moves, so closing
        // won't snap the window back to the old size.
        public void RefreshAfterExternalSizeSwitch()
        {
            AdvancedTab.ReloadFromSettings();
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
        }
        // [END SECTION: Constructor & Tab Initialization]

        // [SECTION: Window Binding Refresh]
        // Forces every binding in this window to re-evaluate by briefly clearing and restoring
        // DataContext. Used after a theme loads/saves so controls reflect the new values immediately.
        private void RefreshWindowBindings()
        {
            this.DataContext = null;
            this.DataContext = _settings;
        }
        // [END SECTION: Window Binding Refresh]

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

        // [SECTION: Save / Close Handlers]
        // Save Adjustments: commits the Basic Themes tab into ConfigurationSettings, writes settings.json,
        // and live-refreshes the main window's theme bindings/folder/game colors. Does NOT write to a
        // theme file - that only happens via the Save Changes button inside the Basic Themes tab.
        private void BtnSaveAdjustments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ThemesTab.SyncUiToSettings();

                PersistSettingsToDisk();

                // Saved: a pending custom size is now permanent, and this is the new restore point for closing
                AdvancedTab.CommitPendingSize();
                _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
                _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
                _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
                _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);

                _viewModel.RefreshThemeBindings();
                _viewModel.RefreshFolderColorsLive();
                _viewModel.RefreshGameColorsLive();

                RefreshWindowBindings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save local theme settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClosePanel_Click(object sender, RoutedEventArgs e) => this.Close();
        // [END SECTION: Save / Close Handlers]
    }
}