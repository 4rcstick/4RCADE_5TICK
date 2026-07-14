using System;
using System.IO;
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
        private System.Windows.Threading.DispatcherTimer _themeSavedFlashTimer;

        public ThemesTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Constructor & Initialization]
        // Wires up theme dropdown/button events, populates the theme list, and loads current settings
        // into the UI. persistToDisk/refreshOptionsBindings are callbacks from OptionsWindow so this tab
        // can save settings.json and force-refresh the parent window's bindings independently of the
        // main Save Adjustments button.
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
            LoadCurrentThemeValuesIntoUi();
        }

        public void SyncUiToSettings() => SyncUiEntriesToSettingsLayer();
        // [END SECTION: Constructor & Initialization]

        // [SECTION: Theme List Management]
        // Rebuilds the theme dropdown from *.cfg files in the Themes folder, plus a "[New Theme...]"
        // entry, and re-selects the currently active theme if it still exists.
        private void RefreshThemeList()
        {
            CboThemePresets.Items.Clear();
            string themeDirectory = Path.Combine(_settings.GetConfigPath(), "Themes");

            if (Directory.Exists(themeDirectory))
            {
                var files = Directory.GetFiles(themeDirectory, "*.cfg");
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

        // Normalizes a hex color string to #RRGGBB (uppercase, # prefix, stripping alpha if present)
        private string FormatHexCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string cleanInput = input.Trim().ToUpper();
            if (!cleanInput.StartsWith("#")) cleanInput = "#" + cleanInput;
            if (cleanInput.Length == 9) return "#" + cleanInput.Substring(3);
            return cleanInput;
        }

        // [SECTION: Load Current Values Into UI - SYNC POINT 3 of 3]
        // Populates every Theme Builder control from the current ConfigurationSettings. DANGER: any new
        // theme property must be added here too, or the Theme Builder UI will show stale/default values
        // even though the underlying setting is correct - see project notes on the three Theme Builder
        // sync points (this method, the themeDto builder in BtnSaveTheme_Click, and BtnLoadTheme_Click).
        private void LoadCurrentThemeValuesIntoUi()
        {
            // Background Theme Wallpapers & Branding Assets
            TxtMainBgPath.Text = _settings.MainWindowWallpaper;
            ChkDisableMainBgImage.IsChecked = _settings.DisableMainBgImage;
            TxtGamesBgPath.Text = _settings.GamesListWallpaper;
            TxtMarqueeBgPath.Text = _settings.MarqueeWindowWallpaper;
            TxtMediaBgPath.Text = _settings.MediaWindowWallpaper;
            TxtThemeLogoPath.Text = _settings.ThemeLogo;
            TxtThemeBootSplashPath.Text = _settings.ThemeBootSplash;
            TxtThemeMissingPreviewPath.Text = _settings.ThemeMissingPreview;

            // Color System Enforcements (Filtered through Gatekeeper)
            TxtMainColorHex.Text = FormatHexCode(_settings.BackgroundColor);
            TxtGamesColorHex.Text = FormatHexCode(_settings.FileListBg);
            TxtMarqueeColorHex.Text = FormatHexCode(_settings.MarqueeBoxBg);
            TxtMediaColorHex.Text = FormatHexCode(_settings.VideoBoxBg);
            TxtOptionsColorHex.Text = FormatHexCode(_settings.OptionsBg);
            TxtFrameworkBorderColorHex.Text = FormatHexCode(_settings.BorderColorFramework);
            TxtScrollTrackColorHex.Text = FormatHexCode(_settings.ScrollTrackColor);
            TxtScrollTrackHoverColorHex.Text = FormatHexCode(_settings.ScrollTrackHoverColor);
            TxtScrollThumbColorHex.Text = FormatHexCode(_settings.ScrollThumbColor);
            TxtScrollThumbHoverColorHex.Text = FormatHexCode(_settings.ScrollThumbHoverColor);
            TxtScrollThumbDragColorHex.Text = FormatHexCode(_settings.ScrollThumbDragColor);

            TxtFrameworkBorderColorHex.Text = FormatHexCode(_settings.BorderColorFramework);
            TxtBorderWidthValue.Text = _settings.BorderWidthValue.ToString();
            TxtBorderCurveValue.Text = _settings.BorderCurveValue.ToString();
            TxtSeparatorColorHex.Text = FormatHexCode(_settings.SeparatorColorHex);

            // Typography & State Colors (Filtered through Gatekeeper)
            TxtFolderFontSize.Text = _settings.FolderFontSize.ToString();
            TxtFolderColorHex.Text = FormatHexCode(_settings.FolderColorHex);
            TxtFolderSelectedColorHex.Text = FormatHexCode(_settings.FolderSelectedColorHex);
            TxtFolderSelectedBgColorHex.Text = FormatHexCode(_settings.FolderSelectedBgColorHex);
            TxtGameFontSize.Text = _settings.GameFontSize.ToString();
            TxtGameColorHex.Text = FormatHexCode(_settings.GameColorHex);
            TxtGameHoverColorHex.Text = FormatHexCode(_settings.GameHoverColorHex);
            TxtGameSelectedColorHex.Text = FormatHexCode(_settings.GameSelectedColorHex);
            TxtGameSelectedBgColorHex.Text = FormatHexCode(_settings.GameSelectedBgColorHex);
            TxtFavoritesColorHex.Text = FormatHexCode(_settings.FavoritesColorHex);
            TxtArrowColorHex.Text = FormatHexCode(_settings.ArrowColorHex);
            TxtTabFontSize.Text = _settings.TabFontSize.ToString();
            TxtTabColorHex.Text = FormatHexCode(_settings.TabColorHex);
            TxtTabBgColorHex.Text = _settings.TabBgColorHex;
            TxtTabActiveColorHex.Text = FormatHexCode(_settings.TabActiveColorHex);
            TxtTabActiveBgColorHex.Text = FormatHexCode(_settings.TabActiveBgColorHex);
            TxtHeaderFontSize.Text = _settings.HeaderFontSize.ToString();
            TxtHeaderColorHex.Text = FormatHexCode(_settings.HeaderColorHex);
            TxtSubHeaderFontSize.Text = _settings.SubHeaderFontSize.ToString();
            TxtSubHeaderColorHex.Text = FormatHexCode(_settings.SubHeaderColorHex);
            TxtStandardFontSize.Text = _settings.StandardFontSize.ToString();
            TxtStandardColorHex.Text = FormatHexCode(_settings.StandardColorHex);
            // TxtInputFontSize.Text = _settings.InputFontSize.ToString();
            // TxtInputColorHex.Text = FormatHexCode(_settings.InputColorHex);

            // Button Configs (Filtered through Gatekeeper)
            TxtBtnBgColorHex.Text = FormatHexCode(_settings.BtnBgColorHex);
            TxtBtnBorderColorHex.Text = FormatHexCode(_settings.BtnBorderColorHex);
            TxtBtnTextColorNormalHex.Text = FormatHexCode(_settings.BtnTextColorNormalHex);
            TxtBtnBgColorHoverHex.Text = FormatHexCode(_settings.BtnBgColorHoverHex);
        }
        // [END SECTION: Load Current Values Into UI - SYNC POINT 3 of 3]

        // Shows/hides the "New Theme Name" input based on whether "[New Theme...]" is selected
        private void CboThemePresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is string selectedTheme)
            {
                PnlNewThemeName.Visibility = (selectedTheme == "[New Theme...]") ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // [SECTION: Theme Serialization Logic]

        // [SUB-SECTION: Load Theme - SYNC POINT 1 of 3]
        // Deserializes the selected theme's .cfg (JSON) file into a ThemeSettings DTO and copies every
        // field onto _settings. DANGER: any new theme property must be added here too - see the sync
        // point warning on LoadCurrentThemeValuesIntoUi above.
        private void BtnLoadTheme_Click(object sender, RoutedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is string selectedTheme && selectedTheme != "[New Theme...]")
            {
                try
                {
                    string themeFile = _settings.GetThemePath(selectedTheme);
                    if (File.Exists(themeFile))
                    {
                        string jsonString = File.ReadAllText(themeFile);
                        var themeData = System.Text.Json.JsonSerializer.Deserialize<ArcadeStick.Models.ThemeSettings>(jsonString);
                        if (themeData != null)
                        {
                            // Map ThemeSettings DTO properties back to the main ConfigurationSettings object
                            _settings.MainWindowWallpaper = themeData.MainWindowWallpaper;
                            _settings.DisableMainBgImage = themeData.DisableMainBgImage;
                            _settings.GamesListWallpaper = themeData.GamesListWallpaper;
                            _settings.MarqueeWindowWallpaper = themeData.MarqueeWindowWallpaper;
                            _settings.MediaWindowWallpaper = themeData.MediaWindowWallpaper;
                            _settings.ThemeLogo = themeData.ThemeLogo;
                            _settings.ThemeBootSplash = themeData.ThemeBootSplash;
                            _settings.ThemeMissingPreview = themeData.ThemeMissingPreview;
                            _settings.BackgroundColor = themeData.BackgroundColor;
                            _settings.FileListBg = themeData.FileListBg;
                            _settings.MarqueeBoxBg = themeData.MarqueeBoxBg;
                            _settings.VideoBoxBg = themeData.VideoBoxBg;
                            _settings.OptionsBg = themeData.OptionsBg;
                            _settings.BorderColorFramework = themeData.BorderColorFramework;
                            _settings.ScrollTrackColor = themeData.ScrollTrackColor;
                            _settings.ScrollTrackHoverColor = themeData.ScrollTrackHoverColor;
                            _settings.ScrollThumbColor = themeData.ScrollThumbColor;
                            _settings.ScrollThumbHoverColor = themeData.ScrollThumbHoverColor;
                            _settings.ScrollThumbDragColor = themeData.ScrollThumbDragColor;
                            _settings.BorderColorFramework = themeData.BorderColorFramework;
                            _settings.BorderWidthValue = themeData.BorderWidthValue;
                            _settings.BorderCurveValue = themeData.BorderCurveValue;
                            _settings.SeparatorColorHex = themeData.SeparatorColorHex;
                            _settings.FolderFontSize = themeData.FolderFontSize;
                            _settings.FolderColorHex = themeData.FolderColorHex;
                            _settings.FolderSelectedColorHex = themeData.FolderSelectedColorHex;
                            _settings.FolderSelectedBgColorHex = themeData.FolderSelectedBgColorHex;
                            _settings.GameFontSize = themeData.GameFontSize;
                            _settings.GameColorHex = themeData.GameColorHex;
                            _settings.GameHoverColorHex = themeData.GameHoverColorHex;
                            _settings.GameSelectedColorHex = themeData.GameSelectedColorHex;
                            _settings.GameSelectedBgColorHex = themeData.GameSelectedBgColorHex;
                            _settings.FavoritesColorHex = themeData.FavoritesColorHex;
                            _settings.ArrowColorHex = themeData.ArrowColorHex;
                            _settings.TabFontSize = themeData.TabFontSize;
                            _settings.TabColorHex = themeData.TabColorHex;
                            _settings.TabBgColorHex = themeData.TabBgColorHex;
                            _settings.TabActiveColorHex = themeData.TabActiveColorHex;
                            _settings.TabActiveBgColorHex = themeData.TabActiveBgColorHex;
                            _settings.HeaderFontSize = themeData.HeaderFontSize;
                            _settings.HeaderColorHex = themeData.HeaderColorHex;
                            _settings.SubHeaderFontSize = themeData.SubHeaderFontSize;
                            _settings.SubHeaderColorHex = themeData.SubHeaderColorHex;
                            _settings.StandardFontSize = themeData.StandardFontSize;
                            _settings.StandardColorHex = themeData.StandardColorHex;
                            _settings.InputFontSize = themeData.InputFontSize;
                            _settings.InputColorHex = themeData.InputColorHex;
                            _settings.BtnBgColorHex = themeData.BtnBgColorHex;
                            _settings.BtnBorderColorHex = themeData.BtnBorderColorHex;
                            _settings.BtnTextColorNormalHex = themeData.BtnTextColorNormalHex;
                            _settings.BtnBgColorHoverHex = themeData.BtnBgColorHoverHex;

                            LoadCurrentThemeValuesIntoUi();
                            _viewModel.RefreshThemeBindings();
                            _viewModel.RefreshFolderColorsLive();
                            _refreshOptionsBindings?.Invoke();
                            _settings.ActiveThemeName = selectedTheme;
                            _persistToDisk?.Invoke();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading theme properties: {ex.Message}", "Processing Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // [END SUB-SECTION: Load Theme - SYNC POINT 1 of 3]

        // [SUB-SECTION: Save Theme - SYNC POINT 2 of 3]
        // Validates the theme name (prompting for one if "[New Theme...]" is selected), syncs the UI into
        // _settings, builds a ThemeSettings DTO, and writes it as JSON to the theme's .cfg file. DANGER:
        // any new theme property must be added to this DTO too - see the sync point warning above.
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

                SyncUiEntriesToSettingsLayer();

                // Create and populate the DTO
                var themeDto = new ArcadeStick.Models.ThemeSettings
                {
                    MainWindowWallpaper = _settings.MainWindowWallpaper,
                    DisableMainBgImage = _settings.DisableMainBgImage,
                    GamesListWallpaper = _settings.GamesListWallpaper,
                    MarqueeWindowWallpaper = _settings.MarqueeWindowWallpaper,
                    MediaWindowWallpaper = _settings.MediaWindowWallpaper,
                    ThemeLogo = _settings.ThemeLogo,
                    ThemeBootSplash = _settings.ThemeBootSplash,
                    ThemeMissingPreview = _settings.ThemeMissingPreview,
                    BackgroundColor = _settings.BackgroundColor,
                    FileListBg = _settings.FileListBg,
                    MarqueeBoxBg = _settings.MarqueeBoxBg,
                    VideoBoxBg = _settings.VideoBoxBg,
                    OptionsBg = _settings.OptionsBg,
                    BorderColorFramework = _settings.BorderColorFramework,
                    ScrollTrackColor = _settings.ScrollTrackColor,
                    ScrollTrackHoverColor = _settings.ScrollTrackHoverColor,
                    ScrollThumbColor = _settings.ScrollThumbColor,
                    ScrollThumbHoverColor = _settings.ScrollThumbHoverColor,
                    ScrollThumbDragColor = _settings.ScrollThumbDragColor,
                    BorderWidthValue = _settings.BorderWidthValue,
                    BorderCurveValue = _settings.BorderCurveValue,
                    SeparatorColorHex = _settings.SeparatorColorHex,
                    FolderFontSize = _settings.FolderFontSize,
                    FolderColorHex = _settings.FolderColorHex,
                    FolderSelectedColorHex = _settings.FolderSelectedColorHex,
                    FolderSelectedBgColorHex = _settings.FolderSelectedBgColorHex,
                    GameFontSize = _settings.GameFontSize,
                    GameColorHex = _settings.GameColorHex,
                    GameHoverColorHex = _settings.GameHoverColorHex,
                    GameSelectedColorHex = _settings.GameSelectedColorHex,
                    GameSelectedBgColorHex = _settings.GameSelectedBgColorHex,
                    FavoritesColorHex = _settings.FavoritesColorHex,
                    ArrowColorHex = _settings.ArrowColorHex,
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
                    InputFontSize = _settings.InputFontSize,
                    InputColorHex = _settings.InputColorHex,
                    BtnBgColorHex = _settings.BtnBgColorHex,
                    BtnBorderColorHex = _settings.BtnBorderColorHex,
                    BtnTextColorNormalHex = _settings.BtnTextColorNormalHex,
                    BtnBgColorHoverHex = _settings.BtnBgColorHoverHex
                };

                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string jsonString = System.Text.Json.JsonSerializer.Serialize(themeDto, jsonOptions);
                File.WriteAllText(themeFile, jsonString);

                _persistToDisk?.Invoke();

                _viewModel.RefreshThemeBindings();
                _viewModel.RefreshFolderColorsLive();
                RefreshThemeList();
                CboThemePresets.SelectedItem = themeName;
                _settings.ActiveThemeName = themeName;
                _persistToDisk?.Invoke();
                ShowThemeSavedFlash();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save theme: {ex.Message}", "Write Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // [END SUB-SECTION: Save Theme - SYNC POINT 2 of 3]

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
                        if (File.Exists(themeFile))
                        {
                            File.Delete(themeFile);
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

        // [SECTION: Sync UI to Settings]
        // Writes every Theme Builder control's current value back into _settings. Called before saving
        // a theme file (BtnSaveTheme_Click) and from OptionsWindow's main Save Adjustments button
        // (via SyncUiToSettings). Numeric fields are parsed defensively - invalid input leaves the
        // existing setting unchanged rather than throwing.
        private void SyncUiEntriesToSettingsLayer()
        {
            _settings.MainWindowWallpaper = TxtMainBgPath.Text;
            _settings.DisableMainBgImage = ChkDisableMainBgImage.IsChecked ?? false;
            _settings.GamesListWallpaper = TxtGamesBgPath.Text;
            _settings.MarqueeWindowWallpaper = TxtMarqueeBgPath.Text;
            _settings.MediaWindowWallpaper = TxtMediaBgPath.Text;
            _settings.ThemeLogo = TxtThemeLogoPath.Text;
            _settings.ThemeBootSplash = TxtThemeBootSplashPath.Text;
            _settings.ThemeMissingPreview = TxtThemeMissingPreviewPath.Text;

            _settings.BackgroundColor = TxtMainColorHex.Text;
            _settings.FileListBg = TxtGamesColorHex.Text;
            _settings.MarqueeBoxBg = TxtMarqueeColorHex.Text;
            _settings.VideoBoxBg = TxtMediaColorHex.Text;
            _settings.OptionsBg = TxtOptionsColorHex.Text;
            _settings.BorderColorFramework = TxtFrameworkBorderColorHex.Text;
            _settings.ScrollTrackColor = TxtScrollTrackColorHex.Text;
            _settings.ScrollTrackHoverColor = TxtScrollTrackHoverColorHex.Text;
            _settings.ScrollThumbColor = TxtScrollThumbColorHex.Text;
            _settings.ScrollThumbHoverColor = TxtScrollThumbHoverColorHex.Text;
            _settings.ScrollThumbDragColor = TxtScrollThumbDragColorHex.Text;

            _settings.BorderColorFramework = TxtFrameworkBorderColorHex.Text;
            if (double.TryParse(TxtBorderWidthValue.Text, out double bWidth)) _settings.BorderWidthValue = bWidth;
            if (double.TryParse(TxtBorderCurveValue.Text, out double bCurve)) _settings.BorderCurveValue = bCurve;
            _settings.SeparatorColorHex = TxtSeparatorColorHex.Text;

            if (int.TryParse(TxtFolderFontSize.Text, out int fSize)) _settings.FolderFontSize = fSize;
            _settings.FolderColorHex = TxtFolderColorHex.Text;
            _settings.FolderSelectedColorHex = TxtFolderSelectedColorHex.Text;
            _settings.FolderSelectedBgColorHex = TxtFolderSelectedBgColorHex.Text;
            if (int.TryParse(TxtGameFontSize.Text, out int gSize)) _settings.GameFontSize = gSize;
            _settings.GameColorHex = TxtGameColorHex.Text;
            _settings.GameHoverColorHex = TxtGameHoverColorHex.Text;
            _settings.GameSelectedColorHex = TxtGameSelectedColorHex.Text;
            _settings.GameSelectedBgColorHex = TxtGameSelectedBgColorHex.Text;
            _settings.FavoritesColorHex = TxtFavoritesColorHex.Text;
            _settings.ArrowColorHex = TxtArrowColorHex.Text;

            if (int.TryParse(TxtTabFontSize.Text, out int tSize)) _settings.TabFontSize = tSize;
            _settings.TabColorHex = TxtTabColorHex.Text;
            _settings.TabBgColorHex = TxtTabBgColorHex.Text;
            _settings.TabActiveColorHex = TxtTabActiveColorHex.Text;
            _settings.TabActiveBgColorHex = TxtTabActiveBgColorHex.Text;
            if (int.TryParse(TxtHeaderFontSize.Text, out int hSize)) _settings.HeaderFontSize = hSize;
            _settings.HeaderColorHex = TxtHeaderColorHex.Text;
            if (int.TryParse(TxtSubHeaderFontSize.Text, out int shSize)) _settings.SubHeaderFontSize = shSize;
            _settings.SubHeaderColorHex = TxtSubHeaderColorHex.Text;
            if (int.TryParse(TxtStandardFontSize.Text, out int sSize)) _settings.StandardFontSize = sSize;
            _settings.StandardColorHex = TxtStandardColorHex.Text;
            // if (int.TryParse(TxtInputFontSize.Text, out int iSize)) _settings.InputFontSize = iSize;
            // _settings.InputColorHex = TxtInputColorHex.Text;

            _settings.BtnBgColorHex = TxtBtnBgColorHex.Text;
            _settings.BtnBorderColorHex = TxtBtnBorderColorHex.Text;
            _settings.BtnTextColorNormalHex = TxtBtnTextColorNormalHex.Text;
            _settings.BtnBgColorHoverHex = TxtBtnBgColorHoverHex.Text;
        }
        // [END SECTION: Sync UI to Settings]

        // [SECTION: File Browse Handler]
        // Shared handler for all seven image-asset browse buttons. Opens an image file picker, converts
        // the selection to a path relative to the MAME root when on the same drive (portable-friendly),
        // then routes the result to the correct TextBox based on which button raised the event.
        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
                Title = "Select Theme Background Asset",
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                string mameRoot = _settings.GetMamePath();

                string appDrive = System.IO.Path.GetPathRoot(mameRoot) ?? "";
                string selectedDrive = System.IO.Path.GetPathRoot(selectedPath) ?? "";

                if (!string.IsNullOrEmpty(appDrive) && appDrive.Equals(selectedDrive, System.StringComparison.OrdinalIgnoreCase))
                {
                    string relativePath = System.IO.Path.GetRelativePath(mameRoot, selectedPath);
                    selectedPath = relativePath == "." ? "." : (!relativePath.StartsWith("..") ? relativePath : selectedPath);
                }

                if (sender == BtnBrowseMainBg) TxtMainBgPath.Text = selectedPath;
                else if (sender == BtnBrowseGamesBg) TxtGamesBgPath.Text = selectedPath;
                else if (sender == BtnBrowseMarqueeBg) TxtMarqueeBgPath.Text = selectedPath;
                else if (sender == BtnBrowseMediaBg) TxtMediaBgPath.Text = selectedPath;
                else if (sender == BtnBrowseThemeLogo) TxtThemeLogoPath.Text = selectedPath;
                else if (sender == BtnBrowseThemeBootSplash) TxtThemeBootSplashPath.Text = selectedPath;
                else if (sender == BtnBrowseThemeMissingPreview) TxtThemeMissingPreviewPath.Text = selectedPath;
            }
        }
        // [END SECTION: File Browse Handler]
    }
}