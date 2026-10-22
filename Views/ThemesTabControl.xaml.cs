using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class ThemesTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;
        private ArcadeStick.ViewModels.MainViewModel _viewModel;
        private Action _persistToDisk;
        private Action _refreshOptionsBindings;
        private Action? _themeSavedCallback;

        // Lets the hosting window react when Save Changes writes a theme (Theme Builder uses it to move
        // its close-without-saving restore point). Optional - nothing else needs to set it.
        public void SetThemeSavedCallback(Action callback) => _themeSavedCallback = callback;
        private System.Windows.Threading.DispatcherTimer _themeSavedFlashTimer;

        public ThemesTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Constructor & Initialization]
        // Wires up theme dropdown/button events and populates the theme list. persistToDisk/refreshOptionsBindings
        // are callbacks from the hosting window so this tab can save settings.json and force-refresh the parent
        // window's bindings independently of the main Save Adjustments button.
        public void Initialize(ArcadeStick.ViewModels.MainViewModel viewModel, ArcadeStick.Models.ConfigurationSettings settings, Action persistToDisk, Action refreshOptionsBindings)
        {
            _viewModel = viewModel;
            _settings = settings;
            _persistToDisk = persistToDisk;
            _refreshOptionsBindings = refreshOptionsBindings;

            CboThemePresets.SelectionChanged += CboThemePresets_SelectionChanged;
            BtnLoadTheme.Click += BtnLoadTheme_Click;
            BtnSaveTheme.Click += BtnSaveTheme_Click;
            BtnDeleteTheme.Click += BtnDeleteTheme_Click;

            RefreshThemeList();
        }

        // Rebuilds the theme list and re-selects the active theme. Used when the theme is switched from
        // outside this tab (the main window's hamburger menu).
        public void ReloadFromSettings()
        {
            RefreshThemeList();
        }
        // [END SECTION: Constructor & Initialization]

        // [SECTION: Theme List Management]
        // Rebuilds the theme dropdown from *.cfg files in the Themes folder, plus a "[New Theme...]"
        // entry, and re-selects the currently active theme if it still exists.
        private void RefreshThemeList()
        {
            CboThemePresets.Items.Clear();
            string themeDirectory = Path.Combine(_settings.GetArcadeStickFilesPath(), "Themes");

            if (Directory.Exists(themeDirectory))
            {
                var files = Directory.GetFiles(themeDirectory, "*.zip");
                foreach (var file in files)
                {
                    CboThemePresets.Items.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            CboThemePresets.Items.Add("[New Theme...]");

            if (!string.IsNullOrWhiteSpace(_settings.ActiveThemeName) && CboThemePresets.Items.Contains(_settings.ActiveThemeName))
            {
                CboThemePresets.SelectedItem = _settings.ActiveThemeName;
            }
            else
            {
                CboThemePresets.SelectedItem = "[New Theme...]";
            }
        }
        // [END SECTION: Theme List Management]

        // Shows/hides the "New Theme Name" input based on whether "[New Theme...]" is selected
        private void CboThemePresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is string selectedTheme)
            {
                PnlNewThemeName.Visibility = (selectedTheme == "[New Theme...]") ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // [SECTION: Theme Serialization Logic]

        // [SUB-SECTION: Load Theme]
        // Loads the selected theme zip through MainViewModel.LoadTheme (which maps theme.json onto _settings),
        // then refreshes the hosting window's bindings and persists settings.json.
        private void BtnLoadTheme_Click(object sender, RoutedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is string selectedTheme && selectedTheme != "[New Theme...]")
            {
                try
                {
                    if (_viewModel.LoadTheme(selectedTheme))
                    {
                        _refreshOptionsBindings?.Invoke();
                        _persistToDisk?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading theme properties: {ex.Message}", "Processing Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // [END SUB-SECTION: Load Theme]

        // [SECTION: Theme Archive Asset Bundling]
        // Resolves a theme asset's stored value to a real absolute path: a relative value is resolved against
        // the MAME root (the portable-friendly convention the old Basic tab browse buttons used), an absolute
        // value is returned as is.
        private string ResolveAssetAbsolutePath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return string.Empty;
            if (Path.IsPathRooted(storedPath)) return storedPath;
            return Path.Combine(_settings.GetMamePath(), storedPath);
        }

        // Copies one asset file into the staging assets/ folder (numbering the filename if it collides
        // with an asset already bundled from a different property) and returns the archive-relative path
        // the DTO should be rewritten to point at. A missing/empty source is left untouched and reported
        // back via missingAssets, rather than failing the whole save.
        private string BundleThemeAsset(string storedPath, string stagingAssetsDir, List<string> missingAssets)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return storedPath;

            string absolutePath = ResolveAssetAbsolutePath(storedPath);
            if (!File.Exists(absolutePath))
            {
                missingAssets.Add(storedPath);
                return storedPath;
            }

            string fileName = Path.GetFileName(absolutePath);
            string destPath = Path.Combine(stagingAssetsDir, fileName);

            int counter = 1;
            while (File.Exists(destPath))
            {
                string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                destPath = Path.Combine(stagingAssetsDir, $"{nameOnly}_{counter}{ext}");
                counter++;
            }

            File.Copy(absolutePath, destPath);
            return $"assets/{Path.GetFileName(destPath)}";
        }
        // [END SECTION: Theme Archive Asset Bundling]

        // [SUB-SECTION: Save Theme]
        // Validates the theme name (prompting for one if "[New Theme...]" is selected), builds a ThemeSettings DTO
        // from _settings, bundles every layout entry's ImagePath into a staging assets/ folder (rewriting the DTO's
        // copies of those paths to archive-relative paths), then zips the result to the theme's .zip file. DANGER:
        // any new flat theme property must be added to this DTO too (and to ThemeSettings and LoadTheme).
        private void BtnSaveTheme_Click(object sender, RoutedEventArgs e)
        {
            string themeName = CboThemePresets.SelectedItem as string;
            if (themeName == "[New Theme...]")
            {
                themeName = TxtNewThemeName.Text.Trim();
                if (string.IsNullOrEmpty(themeName))
                {
                    MessageBox.Show("Please enter a valid theme name.", "Validation Halt", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (string.IsNullOrEmpty(themeName)) return;

            try
            {
                string themeFile = _settings.GetThemePath(themeName);
                Directory.CreateDirectory(Path.GetDirectoryName(themeFile));

                // Create and populate the DTO
                var themeDto = new ArcadeStick.Models.ThemeSettings
                {
                    OptionsBg = _settings.OptionsBg,
                    ScrollTrackColor = _settings.ScrollTrackColor,
                    ScrollTrackHoverColor = _settings.ScrollTrackHoverColor,
                    ScrollThumbColor = _settings.ScrollThumbColor,
                    ScrollThumbHoverColor = _settings.ScrollThumbHoverColor,
                    ScrollThumbDragColor = _settings.ScrollThumbDragColor,
                    ScrollbarWidth = _settings.ScrollbarWidth,
                    SeparatorColorHex = _settings.SeparatorColorHex,
                    TabFontSize = _settings.TabFontSize,
                    TabColorHex = _settings.TabColorHex,
                    TabBgColorHex = _settings.TabBgColorHex,
                    TabActiveColorHex = _settings.TabActiveColorHex,
                    TabActiveBgColorHex = _settings.TabActiveBgColorHex,
                    HeaderFontSize = _settings.HeaderFontSize,
                    HeaderColorHex = _settings.HeaderColorHex,
                    SubHeaderFontSize = _settings.SubHeaderFontSize,
                    SubHeaderColorHex = _settings.SubHeaderColorHex,
                    StandardFontSize = _settings.StandardFontSize,
                    StandardColorHex = _settings.StandardColorHex,
                    ContextMenuFontColorHex = _settings.ContextMenuFontColorHex,
                    ContextMenuIconColorHex = _settings.ContextMenuIconColorHex,
                    ContextMenuBgColorHex = _settings.ContextMenuBgColorHex,
                    ContextMenuHoverColorHex = _settings.ContextMenuHoverColorHex,
                    ContextMenuHoverBgColorHex = _settings.ContextMenuHoverBgColorHex,
                    ContextMenuBorderColorHex = _settings.ContextMenuBorderColorHex,
                    SubTextFontSize = _settings.SubTextFontSize,
                    SubTextColorHex = _settings.SubTextColorHex,
                    OptionsMenuBgColorHex = _settings.OptionsMenuBgColorHex,
                    OptionsMenuBorderSize = _settings.OptionsMenuBorderSize,
                    OptionsMenuBorderColorHex = _settings.OptionsMenuBorderColorHex,
                    OptionsMenuBorderRadius = _settings.OptionsMenuBorderRadius,
                    TabColorHoverHex = _settings.TabColorHoverHex,
                    TabBgColorHoverHex = _settings.TabBgColorHoverHex,
                    OptionsBtnFontSize = _settings.OptionsBtnFontSize,
                    OptionsBtnColorHoverHex = _settings.OptionsBtnColorHoverHex,
                    OptionsBtnBorderSize = _settings.OptionsBtnBorderSize,
                    OptionsBtnBorderRadius = _settings.OptionsBtnBorderRadius,
                    BtnBgColorHex = _settings.BtnBgColorHex,
                    BtnBorderColorHex = _settings.BtnBorderColorHex,
                    BtnTextColorNormalHex = _settings.BtnTextColorNormalHex,
                    BtnBgColorHoverHex = _settings.BtnBgColorHoverHex
                };

                // Bundle every layout entry's ImagePath (below - wallpaper, logo, splash, fallback, shape and custom
                // layer files) into a staging assets/ folder, rewriting the DTO's copies of those paths to
                // archive-relative paths - the original absolute/relative
                // paths on THIS machine are meaningless to whoever imports the theme elsewhere.
                string stagingDir = Path.Combine(Path.GetTempPath(), $"4rcstick_theme_{Guid.NewGuid():N}");
                string stagingAssetsDir = Path.Combine(stagingDir, "assets");
                Directory.CreateDirectory(stagingAssetsDir);

                var missingAssets = new List<string>();


                // Layouts: keep every saved resolution, and make sure the active resolution has an entry
                // (registry defaults fill in anything not customized yet) so a save always records it.
                int activeW = _settings.ActiveResolutionWidth;
                int activeH = _settings.ActiveResolutionHeight;
                var layoutsToSave = new List<ArcadeStick.Models.ThemeLayout>();
                ArcadeStick.Models.ThemeLayout? activeSaved = null;
                foreach (var layout in _settings.Layouts)
                {
                    if (layout.Width == activeW && layout.Height == activeH) activeSaved = layout;
                    else layoutsToSave.Add(layout);
                }
                layoutsToSave.Add(ArcadeStick.Models.LayerRegistry.ResolveLayout(activeSaved, activeW, activeH));
                _settings.Layouts = layoutsToSave;

                // The theme file gets its own copy of the layouts, with every custom layer's image bundled
                // into the zip and its path rewritten to archive-relative. The live layouts keep their real paths.
                var layoutsForTheme = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(layoutsToSave);
                var bundledCustomImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var customEntry in layoutsForTheme.SelectMany(l => l.Layers).Where(e => !string.IsNullOrWhiteSpace(e.ImagePath)))
                {
                    if (string.IsNullOrWhiteSpace(customEntry.ImagePath)) continue;

                    string imageKey = ResolveAssetAbsolutePath(customEntry.ImagePath);
                    if (!bundledCustomImages.TryGetValue(imageKey, out string? archivePath))
                    {
                        archivePath = BundleThemeAsset(customEntry.ImagePath, stagingAssetsDir, missingAssets);
                        bundledCustomImages[imageKey] = archivePath;
                    }
                    customEntry.ImagePath = archivePath;
                }
                themeDto.Layouts = layoutsForTheme;
                themeDto.ActiveWidth = activeW;
                themeDto.ActiveHeight = activeH;

                // Saving the theme moves the restore point to what was just written to the zip.
                _settings.SavedThemeLayouts = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(layoutsToSave);
                _settings.SavedThemeWidth = activeW;
                _settings.SavedThemeHeight = activeH;

                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string jsonString = System.Text.Json.JsonSerializer.Serialize(themeDto, jsonOptions);
                File.WriteAllText(Path.Combine(stagingDir, "theme.json"), jsonString);

                if (File.Exists(themeFile))
                {
                    File.Delete(themeFile); // ZipFile.CreateFromDirectory throws if the destination already exists
                }
                ZipFile.CreateFromDirectory(stagingDir, themeFile);
                Directory.Delete(stagingDir, recursive: true);

                if (missingAssets.Count > 0)
                {
                    MessageBox.Show($"Theme saved, but {missingAssets.Count} asset file(s) could not be found and were not included:\n\n{string.Join("\n", missingAssets)}", "Some Assets Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _persistToDisk?.Invoke();

                _viewModel.RefreshThemeBindings();
                _viewModel.RefreshFolderColorsLive();
                _viewModel.RefreshGameColorsLive();
                RefreshThemeList();
                CboThemePresets.SelectedItem = themeName;
                _settings.ActiveThemeName = themeName;
                _persistToDisk?.Invoke();
                _themeSavedCallback?.Invoke();
                ShowThemeSavedFlash();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save theme: {ex.Message}", "Write Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // [END SUB-SECTION: Save Theme]

        // Briefly flashes the "Theme Saved" confirmation text for 2 seconds after a successful save
        private void ShowThemeSavedFlash()
        {
            TxtThemeSavedFlash.Visibility = Visibility.Visible;

            _themeSavedFlashTimer?.Stop();
            _themeSavedFlashTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _themeSavedFlashTimer.Tick += (s, e) =>
            {
                TxtThemeSavedFlash.Visibility = Visibility.Collapsed;
                _themeSavedFlashTimer.Stop();
            };
            _themeSavedFlashTimer.Start();
        }
        // [END SECTION: Theme Serialization Logic]

        // [SECTION: Delete Theme]
        // Prompts for confirmation, then deletes the selected theme's .cfg file and refreshes the dropdown.
        private void BtnDeleteTheme_Click(object sender, RoutedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is string selectedTheme && selectedTheme != "[New Theme...]")
            {
                if (MessageBox.Show($"Are you completely certain you want to permanently delete the theme layout configuration file for '{selectedTheme}'?", "Verify Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        string themeFile = _settings.GetThemePath(selectedTheme);
                        bool deletedAnything = false;

                        if (File.Exists(themeFile))
                        {
                            File.Delete(themeFile);
                            deletedAnything = true;
                        }

                        // Also clean up this theme's extracted temp assets (Themes\Extracted\<name>\), if
                        // it was ever loaded - otherwise those files would just sit there orphaned forever
                        // with no theme left to reference them.
                        string extractDir = Path.Combine(_settings.GetArcadeStickFilesPath(), "Themes", "Extracted", selectedTheme);
                        if (Directory.Exists(extractDir))
                        {
                            Directory.Delete(extractDir, recursive: true);
                            deletedAnything = true;
                        }

                        if (deletedAnything)
                        {
                            RefreshThemeList();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Deletion protocol faulted: {ex.Message}", "IO Access Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        // [END SECTION: Delete Theme]
    }
}