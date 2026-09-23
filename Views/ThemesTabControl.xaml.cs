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
        private System.Windows.Threading.DispatcherTimer _themeSavedFlashTimer;

        // TEMPLATE for Claude Code: this field plus the sync block below are the pattern to replicate
        // per Theme*ColorHex field (rename to match, e.g. _suppressGameColorSync). Prevents the picker's
        // ColorChanged and the hex textbox's LostFocus from re-triggering each other.
        private bool _suppressTabBgColorSync;
        private bool _suppressMainColorSync;
        private bool _suppressSearchBoxColorSync;
        private bool _suppressSearchBoxBgColorSync;
        private bool _suppressGamesColorSync;
        private bool _suppressMarqueeColorSync;
        private bool _suppressMediaColorSync;
        private bool _suppressOptionsColorSync;
        private bool _suppressGameHoverColorSync;
        private bool _suppressArrowColorSync;
        private bool _suppressFavoritesColorSync;
        private bool _suppressTabActiveColorSync;
        private bool _suppressTabActiveBgColorSync;
        private bool _suppressFrameworkBorderColorSync;
        private bool _suppressSeparatorColorSync;
        private bool _suppressMarqueeBorderColorSync;
        private bool _suppressBtnTextColorNormalSync;
        private bool _suppressBtnBgColorSync;
        private bool _suppressBtnBorderColorSync;
        private bool _suppressBtnBgColorHoverSync;
        private bool _suppressScrollThumbColorSync;
        private bool _suppressScrollThumbHoverColorSync;
        private bool _suppressScrollThumbDragColorSync;
        private bool _suppressScrollTrackColorSync;
        private bool _suppressScrollTrackHoverColorSync;
        private bool _suppressFolderColorSync;
        private bool _suppressGameColorSync;
        private bool _suppressHeaderColorSync;
        private bool _suppressSubHeaderColorSync;
        private bool _suppressStandardColorSync;
        private bool _suppressTabColorSync;
        private bool _suppressFolderSelectedColorSync;
        private bool _suppressFolderSelectedBgColorSync;
        private bool _suppressGameSelectedColorSync;
        private bool _suppressGameSelectedBgColorSync;
        private bool _suppressGameNameHeaderColorSync;
        private bool _suppressPubYearRatingColorSync;
        private bool _suppressInfoHeaderColorSync;
        private bool _suppressInfoBodyColorSync;
        private bool _suppressCreditsHeaderColorSync;
        private bool _suppressCreditsBodyColorSync;
        private bool _suppressVideoBgColorSync;
        private bool _suppressVideoBorderColorSync;
        private bool _suppressNavIconsColorSync;
        private bool _suppressPreviewBorderColorSync;
        private bool _suppressSearchLabelColorSync;
        private bool _suppressSearchLabelBgColorSync;
        private bool _suppressMainWinBtnColorSync;
        private bool _suppressMainWinBtnBgColorSync;
        private bool _suppressMainWinBtnColorHoverSync;
        private bool _suppressMainWinBtnBgColorHoverSync;
        private bool _suppressMainWinBtnBorderColorSync;
        private bool _suppressGamesBorderColorSync;
        private bool _suppressContextMenuFontColorSync;
        private bool _suppressContextMenuIconColorSync;
        private bool _suppressContextMenuBgColorSync;
        private bool _suppressContextMenuHoverColorSync;
        private bool _suppressContextMenuHoverBgColorSync;
        private bool _suppressSubTextColorSync;
        private bool _suppressOptionsMenuBgColorSync;
        private bool _suppressOptionsMenuBorderColorSync;
        private bool _suppressTabColorHoverSync;
        private bool _suppressTabBgColorHoverSync;
        private bool _suppressOptionsBtnColorHoverSync;

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

        // Commits every Theme Builder control's value into _settings, then immediately re-populates the
        // UI from that same _settings state (reusing LoadCurrentThemeValuesIntoUi's existing sync-point-3
        // logic). This closes the gap where a saved value could differ from what's displayed - e.g. a
        // bare hex typed without a trailing Tab/focus-loss would commit normalized via
        // SyncUiEntriesToSettingsLayer, but the textbox and color-picker swatch would keep showing the
        // stale, unnormalized input until the window was closed and reopened.
        public void SyncUiToSettings()
        {
            SyncUiEntriesToSettingsLayer();
            LoadCurrentThemeValuesIntoUi();
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

        // Mirrors the picker's color into the hex textbox. Alpha 0 writes "Transparent" to match this
        // field's existing convention rather than a #00RRGGBB hex value.
        private void TabBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressTabBgColorSync) return;

            var newColor = TabBgColorPicker.SelectedColor;
            _suppressTabBgColorSync = true;
            TxtTabBgColorHex.Text = newColor.A == 0
                ? "Transparent"
                : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressTabBgColorSync = false;
        }

        // Pushes a manually-typed hex value back into the picker on focus loss. Invalid text is left
        // alone rather than crashing.
        private void TxtTabBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressTabBgColorSync) return;

            var parsedColor = TryParseColor(TxtTabBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressTabBgColorSync = true;
                TabBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressTabBgColorSync = false;
            }
        }

        private void SearchBoxColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchBoxColorSync) return;
            var newColor = SearchBoxColorPicker.SelectedColor;
            _suppressSearchBoxColorSync = true;
            TxtSearchBoxColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressSearchBoxColorSync = false;
        }

        private void TxtSearchBoxColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchBoxColorSync) return;
            var parsedColor = TryParseColor(TxtSearchBoxColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressSearchBoxColorSync = true;
                SearchBoxColorPicker.SelectedColor = parsedColor.Value;
                _suppressSearchBoxColorSync = false;
            }
        }

        private void SearchBoxBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchBoxBgColorSync) return;
            var newColor = SearchBoxBgColorPicker.SelectedColor;
            _suppressSearchBoxBgColorSync = true;
            TxtSearchBoxBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressSearchBoxBgColorSync = false;
        }

        private void TxtSearchBoxBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchBoxBgColorSync) return;
            var parsedColor = TryParseColor(TxtSearchBoxBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressSearchBoxBgColorSync = true;
                SearchBoxBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressSearchBoxBgColorSync = false;
            }
        }

        private void GameNameHeaderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGameNameHeaderColorSync) return;
            var newColor = GameNameHeaderColorPicker.SelectedColor;
            _suppressGameNameHeaderColorSync = true;
            TxtGameNameHeaderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressGameNameHeaderColorSync = false;
        }

        private void TxtGameNameHeaderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGameNameHeaderColorSync) return;
            var parsedColor = TryParseColor(TxtGameNameHeaderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressGameNameHeaderColorSync = true;
                GameNameHeaderColorPicker.SelectedColor = parsedColor.Value;
                _suppressGameNameHeaderColorSync = false;
            }
        }

        private void PubYearRatingColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressPubYearRatingColorSync) return;
            var newColor = PubYearRatingColorPicker.SelectedColor;
            _suppressPubYearRatingColorSync = true;
            TxtPubYearRatingColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressPubYearRatingColorSync = false;
        }

        private void TxtPubYearRatingColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressPubYearRatingColorSync) return;
            var parsedColor = TryParseColor(TxtPubYearRatingColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressPubYearRatingColorSync = true;
                PubYearRatingColorPicker.SelectedColor = parsedColor.Value;
                _suppressPubYearRatingColorSync = false;
            }
        }

        private void InfoHeaderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressInfoHeaderColorSync) return;
            var newColor = InfoHeaderColorPicker.SelectedColor;
            _suppressInfoHeaderColorSync = true;
            TxtInfoHeaderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressInfoHeaderColorSync = false;
        }

        private void TxtInfoHeaderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressInfoHeaderColorSync) return;
            var parsedColor = TryParseColor(TxtInfoHeaderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressInfoHeaderColorSync = true;
                InfoHeaderColorPicker.SelectedColor = parsedColor.Value;
                _suppressInfoHeaderColorSync = false;
            }
        }

        private void InfoBodyColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressInfoBodyColorSync) return;
            var newColor = InfoBodyColorPicker.SelectedColor;
            _suppressInfoBodyColorSync = true;
            TxtInfoBodyColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressInfoBodyColorSync = false;
        }

        private void TxtInfoBodyColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressInfoBodyColorSync) return;
            var parsedColor = TryParseColor(TxtInfoBodyColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressInfoBodyColorSync = true;
                InfoBodyColorPicker.SelectedColor = parsedColor.Value;
                _suppressInfoBodyColorSync = false;
            }
        }

        private void CreditsHeaderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressCreditsHeaderColorSync) return;
            var newColor = CreditsHeaderColorPicker.SelectedColor;
            _suppressCreditsHeaderColorSync = true;
            TxtCreditsHeaderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressCreditsHeaderColorSync = false;
        }

        private void TxtCreditsHeaderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressCreditsHeaderColorSync) return;
            var parsedColor = TryParseColor(TxtCreditsHeaderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressCreditsHeaderColorSync = true;
                CreditsHeaderColorPicker.SelectedColor = parsedColor.Value;
                _suppressCreditsHeaderColorSync = false;
            }
        }

        private void CreditsBodyColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressCreditsBodyColorSync) return;
            var newColor = CreditsBodyColorPicker.SelectedColor;
            _suppressCreditsBodyColorSync = true;
            TxtCreditsBodyColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressCreditsBodyColorSync = false;
        }

        private void TxtCreditsBodyColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressCreditsBodyColorSync) return;
            var parsedColor = TryParseColor(TxtCreditsBodyColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressCreditsBodyColorSync = true;
                CreditsBodyColorPicker.SelectedColor = parsedColor.Value;
                _suppressCreditsBodyColorSync = false;
            }
        }

        private void VideoBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressVideoBgColorSync) return;
            var newColor = VideoBgColorPicker.SelectedColor;
            _suppressVideoBgColorSync = true;
            TxtVideoBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressVideoBgColorSync = false;
        }

        private void TxtVideoBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressVideoBgColorSync) return;
            var parsedColor = TryParseColor(TxtVideoBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressVideoBgColorSync = true;
                VideoBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressVideoBgColorSync = false;
            }
        }

        private void VideoBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressVideoBorderColorSync) return;
            var newColor = VideoBorderColorPicker.SelectedColor;
            _suppressVideoBorderColorSync = true;
            TxtVideoBorderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressVideoBorderColorSync = false;
        }

        private void TxtVideoBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressVideoBorderColorSync) return;
            var parsedColor = TryParseColor(TxtVideoBorderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressVideoBorderColorSync = true;
                VideoBorderColorPicker.SelectedColor = parsedColor.Value;
                _suppressVideoBorderColorSync = false;
            }
        }

        private void NavIconsColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressNavIconsColorSync) return;
            var newColor = NavIconsColorPicker.SelectedColor;
            _suppressNavIconsColorSync = true;
            TxtNavIconsColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressNavIconsColorSync = false;
        }

        private void TxtNavIconsColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressNavIconsColorSync) return;
            var parsedColor = TryParseColor(TxtNavIconsColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressNavIconsColorSync = true;
                NavIconsColorPicker.SelectedColor = parsedColor.Value;
                _suppressNavIconsColorSync = false;
            }
        }

        private void PreviewBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressPreviewBorderColorSync) return;
            var newColor = PreviewBorderColorPicker.SelectedColor;
            _suppressPreviewBorderColorSync = true;
            TxtPreviewBorderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressPreviewBorderColorSync = false;
        }

        private void TxtPreviewBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressPreviewBorderColorSync) return;
            var parsedColor = TryParseColor(TxtPreviewBorderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressPreviewBorderColorSync = true;
                PreviewBorderColorPicker.SelectedColor = parsedColor.Value;
                _suppressPreviewBorderColorSync = false;
            }
        }

        private void SearchLabelColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchLabelColorSync) return;
            var newColor = SearchLabelColorPicker.SelectedColor;
            _suppressSearchLabelColorSync = true;
            TxtSearchLabelColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressSearchLabelColorSync = false;
        }

        private void TxtSearchLabelColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchLabelColorSync) return;
            var parsedColor = TryParseColor(TxtSearchLabelColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressSearchLabelColorSync = true;
                SearchLabelColorPicker.SelectedColor = parsedColor.Value;
                _suppressSearchLabelColorSync = false;
            }
        }

        private void SearchLabelBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchLabelBgColorSync) return;
            var newColor = SearchLabelBgColorPicker.SelectedColor;
            _suppressSearchLabelBgColorSync = true;
            TxtSearchLabelBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressSearchLabelBgColorSync = false;
        }

        private void TxtSearchLabelBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchLabelBgColorSync) return;
            var parsedColor = TryParseColor(TxtSearchLabelBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressSearchLabelBgColorSync = true;
                SearchLabelBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressSearchLabelBgColorSync = false;
            }
        }

        private void MainWinBtnColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnColorSync) return;
            var newColor = MainWinBtnColorPicker.SelectedColor;
            _suppressMainWinBtnColorSync = true;
            TxtMainWinBtnColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMainWinBtnColorSync = false;
        }

        private void TxtMainWinBtnColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnColorSync) return;
            var parsedColor = TryParseColor(TxtMainWinBtnColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMainWinBtnColorSync = true;
                MainWinBtnColorPicker.SelectedColor = parsedColor.Value;
                _suppressMainWinBtnColorSync = false;
            }
        }

        private void MainWinBtnBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnBgColorSync) return;
            var newColor = MainWinBtnBgColorPicker.SelectedColor;
            _suppressMainWinBtnBgColorSync = true;
            TxtMainWinBtnBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMainWinBtnBgColorSync = false;
        }

        private void TxtMainWinBtnBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnBgColorSync) return;
            var parsedColor = TryParseColor(TxtMainWinBtnBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMainWinBtnBgColorSync = true;
                MainWinBtnBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressMainWinBtnBgColorSync = false;
            }
        }

        private void MainWinBtnColorHoverPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnColorHoverSync) return;
            var newColor = MainWinBtnColorHoverPicker.SelectedColor;
            _suppressMainWinBtnColorHoverSync = true;
            TxtMainWinBtnColorHoverHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMainWinBtnColorHoverSync = false;
        }

        private void TxtMainWinBtnColorHoverHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnColorHoverSync) return;
            var parsedColor = TryParseColor(TxtMainWinBtnColorHoverHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMainWinBtnColorHoverSync = true;
                MainWinBtnColorHoverPicker.SelectedColor = parsedColor.Value;
                _suppressMainWinBtnColorHoverSync = false;
            }
        }

        private void MainWinBtnBgColorHoverPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnBgColorHoverSync) return;
            var newColor = MainWinBtnBgColorHoverPicker.SelectedColor;
            _suppressMainWinBtnBgColorHoverSync = true;
            TxtMainWinBtnBgColorHoverHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMainWinBtnBgColorHoverSync = false;
        }

        private void TxtMainWinBtnBgColorHoverHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnBgColorHoverSync) return;
            var parsedColor = TryParseColor(TxtMainWinBtnBgColorHoverHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMainWinBtnBgColorHoverSync = true;
                MainWinBtnBgColorHoverPicker.SelectedColor = parsedColor.Value;
                _suppressMainWinBtnBgColorHoverSync = false;
            }
        }

        private void MainWinBtnBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnBorderColorSync) return;
            var newColor = MainWinBtnBorderColorPicker.SelectedColor;
            _suppressMainWinBtnBorderColorSync = true;
            TxtMainWinBtnBorderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMainWinBtnBorderColorSync = false;
        }

        private void TxtMainWinBtnBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMainWinBtnBorderColorSync) return;
            var parsedColor = TryParseColor(TxtMainWinBtnBorderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMainWinBtnBorderColorSync = true;
                MainWinBtnBorderColorPicker.SelectedColor = parsedColor.Value;
                _suppressMainWinBtnBorderColorSync = false;
            }
        }

        private void GamesBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGamesBorderColorSync) return;
            var newColor = GamesBorderColorPicker.SelectedColor;
            _suppressGamesBorderColorSync = true;
            TxtGamesBorderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressGamesBorderColorSync = false;
        }

        private void TxtGamesBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGamesBorderColorSync) return;
            var parsedColor = TryParseColor(TxtGamesBorderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressGamesBorderColorSync = true;
                GamesBorderColorPicker.SelectedColor = parsedColor.Value;
                _suppressGamesBorderColorSync = false;
            }
        }

        private void ContextMenuFontColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuFontColorSync) return;
            var newColor = ContextMenuFontColorPicker.SelectedColor;
            _suppressContextMenuFontColorSync = true;
            TxtContextMenuFontColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressContextMenuFontColorSync = false;
        }

        private void TxtContextMenuFontColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuFontColorSync) return;
            var parsedColor = TryParseColor(TxtContextMenuFontColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressContextMenuFontColorSync = true;
                ContextMenuFontColorPicker.SelectedColor = parsedColor.Value;
                _suppressContextMenuFontColorSync = false;
            }
        }

        private void ContextMenuIconColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuIconColorSync) return;
            var newColor = ContextMenuIconColorPicker.SelectedColor;
            _suppressContextMenuIconColorSync = true;
            TxtContextMenuIconColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressContextMenuIconColorSync = false;
        }

        private void TxtContextMenuIconColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuIconColorSync) return;
            var parsedColor = TryParseColor(TxtContextMenuIconColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressContextMenuIconColorSync = true;
                ContextMenuIconColorPicker.SelectedColor = parsedColor.Value;
                _suppressContextMenuIconColorSync = false;
            }
        }

        private void ContextMenuBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuBgColorSync) return;
            var newColor = ContextMenuBgColorPicker.SelectedColor;
            _suppressContextMenuBgColorSync = true;
            TxtContextMenuBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressContextMenuBgColorSync = false;
        }

        private void TxtContextMenuBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuBgColorSync) return;
            var parsedColor = TryParseColor(TxtContextMenuBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressContextMenuBgColorSync = true;
                ContextMenuBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressContextMenuBgColorSync = false;
            }
        }

        private void ContextMenuHoverColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuHoverColorSync) return;
            var newColor = ContextMenuHoverColorPicker.SelectedColor;
            _suppressContextMenuHoverColorSync = true;
            TxtContextMenuHoverColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressContextMenuHoverColorSync = false;
        }

        private void TxtContextMenuHoverColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuHoverColorSync) return;
            var parsedColor = TryParseColor(TxtContextMenuHoverColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressContextMenuHoverColorSync = true;
                ContextMenuHoverColorPicker.SelectedColor = parsedColor.Value;
                _suppressContextMenuHoverColorSync = false;
            }
        }

        private void ContextMenuHoverBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuHoverBgColorSync) return;
            var newColor = ContextMenuHoverBgColorPicker.SelectedColor;
            _suppressContextMenuHoverBgColorSync = true;
            TxtContextMenuHoverBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressContextMenuHoverBgColorSync = false;
        }

        private void TxtContextMenuHoverBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressContextMenuHoverBgColorSync) return;
            var parsedColor = TryParseColor(TxtContextMenuHoverBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressContextMenuHoverBgColorSync = true;
                ContextMenuHoverBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressContextMenuHoverBgColorSync = false;
            }
        }

        private void SubTextColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSubTextColorSync) return;
            var newColor = SubTextColorPicker.SelectedColor;
            _suppressSubTextColorSync = true;
            TxtSubTextColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressSubTextColorSync = false;
        }

        private void TxtSubTextColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressSubTextColorSync) return;
            var parsedColor = TryParseColor(TxtSubTextColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressSubTextColorSync = true;
                SubTextColorPicker.SelectedColor = parsedColor.Value;
                _suppressSubTextColorSync = false;
            }
        }

        private void OptionsMenuBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsMenuBgColorSync) return;
            var newColor = OptionsMenuBgColorPicker.SelectedColor;
            _suppressOptionsMenuBgColorSync = true;
            TxtOptionsMenuBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressOptionsMenuBgColorSync = false;
        }

        private void TxtOptionsMenuBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsMenuBgColorSync) return;
            var parsedColor = TryParseColor(TxtOptionsMenuBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressOptionsMenuBgColorSync = true;
                OptionsMenuBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressOptionsMenuBgColorSync = false;
            }
        }

        private void OptionsMenuBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsMenuBorderColorSync) return;
            var newColor = OptionsMenuBorderColorPicker.SelectedColor;
            _suppressOptionsMenuBorderColorSync = true;
            TxtOptionsMenuBorderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressOptionsMenuBorderColorSync = false;
        }

        private void TxtOptionsMenuBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsMenuBorderColorSync) return;
            var parsedColor = TryParseColor(TxtOptionsMenuBorderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressOptionsMenuBorderColorSync = true;
                OptionsMenuBorderColorPicker.SelectedColor = parsedColor.Value;
                _suppressOptionsMenuBorderColorSync = false;
            }
        }

        private void TabColorHoverPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressTabColorHoverSync) return;
            var newColor = TabColorHoverPicker.SelectedColor;
            _suppressTabColorHoverSync = true;
            TxtTabColorHoverHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressTabColorHoverSync = false;
        }

        private void TxtTabColorHoverHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressTabColorHoverSync) return;
            var parsedColor = TryParseColor(TxtTabColorHoverHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressTabColorHoverSync = true;
                TabColorHoverPicker.SelectedColor = parsedColor.Value;
                _suppressTabColorHoverSync = false;
            }
        }

        private void TabBgColorHoverPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressTabBgColorHoverSync) return;
            var newColor = TabBgColorHoverPicker.SelectedColor;
            _suppressTabBgColorHoverSync = true;
            TxtTabBgColorHoverHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressTabBgColorHoverSync = false;
        }

        private void TxtTabBgColorHoverHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressTabBgColorHoverSync) return;
            var parsedColor = TryParseColor(TxtTabBgColorHoverHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressTabBgColorHoverSync = true;
                TabBgColorHoverPicker.SelectedColor = parsedColor.Value;
                _suppressTabBgColorHoverSync = false;
            }
        }

        private void OptionsBtnColorHoverPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsBtnColorHoverSync) return;
            var newColor = OptionsBtnColorHoverPicker.SelectedColor;
            _suppressOptionsBtnColorHoverSync = true;
            TxtOptionsBtnColorHoverHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressOptionsBtnColorHoverSync = false;
        }

        private void TxtOptionsBtnColorHoverHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsBtnColorHoverSync) return;
            var parsedColor = TryParseColor(TxtOptionsBtnColorHoverHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressOptionsBtnColorHoverSync = true;
                OptionsBtnColorHoverPicker.SelectedColor = parsedColor.Value;
                _suppressOptionsBtnColorHoverSync = false;
            }
        }

        private void MainColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMainColorSync) return;
            var newColor = MainColorPicker.SelectedColor;
            _suppressMainColorSync = true;
            TxtMainColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMainColorSync = false;
        }

        private void TxtMainColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMainColorSync) return;
            var parsedColor = TryParseColor(TxtMainColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMainColorSync = true;
                MainColorPicker.SelectedColor = parsedColor.Value;
                _suppressMainColorSync = false;
            }
        }

        private void GamesColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGamesColorSync) return;
            var newColor = GamesColorPicker.SelectedColor;
            _suppressGamesColorSync = true;
            TxtGamesColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressGamesColorSync = false;
        }

        private void TxtGamesColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGamesColorSync) return;
            var parsedColor = TryParseColor(TxtGamesColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressGamesColorSync = true;
                GamesColorPicker.SelectedColor = parsedColor.Value;
                _suppressGamesColorSync = false;
            }
        }

        private void MarqueeColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMarqueeColorSync) return;
            var newColor = MarqueeColorPicker.SelectedColor;
            _suppressMarqueeColorSync = true;
            TxtMarqueeColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMarqueeColorSync = false;
        }

        private void TxtMarqueeColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMarqueeColorSync) return;
            var parsedColor = TryParseColor(TxtMarqueeColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMarqueeColorSync = true;
                MarqueeColorPicker.SelectedColor = parsedColor.Value;
                _suppressMarqueeColorSync = false;
            }
        }

        private void MediaColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMediaColorSync) return;
            var newColor = MediaColorPicker.SelectedColor;
            _suppressMediaColorSync = true;
            TxtMediaColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMediaColorSync = false;
        }

        private void TxtMediaColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMediaColorSync) return;
            var parsedColor = TryParseColor(TxtMediaColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMediaColorSync = true;
                MediaColorPicker.SelectedColor = parsedColor.Value;
                _suppressMediaColorSync = false;
            }
        }

        private void OptionsColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsColorSync) return;
            var newColor = OptionsColorPicker.SelectedColor;
            _suppressOptionsColorSync = true;
            TxtOptionsColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressOptionsColorSync = false;
        }

        private void TxtOptionsColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionsColorSync) return;
            var parsedColor = TryParseColor(TxtOptionsColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressOptionsColorSync = true;
                OptionsColorPicker.SelectedColor = parsedColor.Value;
                _suppressOptionsColorSync = false;
            }
        }

        private void GameHoverColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGameHoverColorSync) return;
            var newColor = GameHoverColorPicker.SelectedColor;
            _suppressGameHoverColorSync = true;
            TxtGameHoverColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressGameHoverColorSync = false;
        }

        private void TxtGameHoverColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGameHoverColorSync) return;
            var parsedColor = TryParseColor(TxtGameHoverColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressGameHoverColorSync = true;
                GameHoverColorPicker.SelectedColor = parsedColor.Value;
                _suppressGameHoverColorSync = false;
            }
        }

        private void ArrowColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressArrowColorSync) return;
            var newColor = ArrowColorPicker.SelectedColor;
            _suppressArrowColorSync = true;
            TxtArrowColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressArrowColorSync = false;
        }

        private void TxtArrowColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressArrowColorSync) return;
            var parsedColor = TryParseColor(TxtArrowColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressArrowColorSync = true;
                ArrowColorPicker.SelectedColor = parsedColor.Value;
                _suppressArrowColorSync = false;
            }
        }

        private void FavoritesColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressFavoritesColorSync) return;
            var newColor = FavoritesColorPicker.SelectedColor;
            _suppressFavoritesColorSync = true;
            TxtFavoritesColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressFavoritesColorSync = false;
        }

        private void TxtFavoritesColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressFavoritesColorSync) return;
            var parsedColor = TryParseColor(TxtFavoritesColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressFavoritesColorSync = true;
                FavoritesColorPicker.SelectedColor = parsedColor.Value;
                _suppressFavoritesColorSync = false;
            }
        }

        private void TabActiveColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressTabActiveColorSync) return;
            var newColor = TabActiveColorPicker.SelectedColor;
            _suppressTabActiveColorSync = true;
            TxtTabActiveColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressTabActiveColorSync = false;
        }

        private void TxtTabActiveColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressTabActiveColorSync) return;
            var parsedColor = TryParseColor(TxtTabActiveColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressTabActiveColorSync = true;
                TabActiveColorPicker.SelectedColor = parsedColor.Value;
                _suppressTabActiveColorSync = false;
            }
        }

        private void TabActiveBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressTabActiveBgColorSync) return;
            var newColor = TabActiveBgColorPicker.SelectedColor;
            _suppressTabActiveBgColorSync = true;
            TxtTabActiveBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressTabActiveBgColorSync = false;
        }

        private void TxtTabActiveBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressTabActiveBgColorSync) return;
            var parsedColor = TryParseColor(TxtTabActiveBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressTabActiveBgColorSync = true;
                TabActiveBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressTabActiveBgColorSync = false;
            }
        }

        // FrameworkBorderColorPicker_ColorChanged/TxtFrameworkBorderColorHex_LostFocus removed - no UI
        // field for BorderColorFramework currently exists (removed from Border and Line Options during
        // the Theme Builder rework). BorderColorFramework still works everywhere at runtime; it just
        // can't be edited from the Theme Builder until a new field is added. See _suppressFrameworkBorderColorSync
        // declaration too if it's otherwise unused after this.

        private void SeparatorColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSeparatorColorSync) return;
            var newColor = SeparatorColorPicker.SelectedColor;
            _suppressSeparatorColorSync = true;
            TxtSeparatorColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressSeparatorColorSync = false;
        }

        private void TxtSeparatorColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressSeparatorColorSync) return;
            var parsedColor = TryParseColor(TxtSeparatorColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressSeparatorColorSync = true;
                SeparatorColorPicker.SelectedColor = parsedColor.Value;
                _suppressSeparatorColorSync = false;
            }
        }

        private void MarqueeBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressMarqueeBorderColorSync) return;
            var newColor = MarqueeBorderColorPicker.SelectedColor;
            _suppressMarqueeBorderColorSync = true;
            TxtMarqueeBorderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressMarqueeBorderColorSync = false;
        }

        private void TxtMarqueeBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressMarqueeBorderColorSync) return;
            var parsedColor = TryParseColor(TxtMarqueeBorderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressMarqueeBorderColorSync = true;
                MarqueeBorderColorPicker.SelectedColor = parsedColor.Value;
                _suppressMarqueeBorderColorSync = false;
            }
        }

        private void BtnTextColorNormalPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnTextColorNormalSync) return;
            var newColor = BtnTextColorNormalPicker.SelectedColor;
            _suppressBtnTextColorNormalSync = true;
            TxtBtnTextColorNormalHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressBtnTextColorNormalSync = false;
        }

        private void TxtBtnTextColorNormalHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnTextColorNormalSync) return;
            var parsedColor = TryParseColor(TxtBtnTextColorNormalHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressBtnTextColorNormalSync = true;
                BtnTextColorNormalPicker.SelectedColor = parsedColor.Value;
                _suppressBtnTextColorNormalSync = false;
            }
        }

        private void BtnBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnBgColorSync) return;
            var newColor = BtnBgColorPicker.SelectedColor;
            _suppressBtnBgColorSync = true;
            TxtBtnBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressBtnBgColorSync = false;
        }

        private void TxtBtnBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnBgColorSync) return;
            var parsedColor = TryParseColor(TxtBtnBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressBtnBgColorSync = true;
                BtnBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressBtnBgColorSync = false;
            }
        }

        private void BtnBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnBorderColorSync) return;
            var newColor = BtnBorderColorPicker.SelectedColor;
            _suppressBtnBorderColorSync = true;
            TxtBtnBorderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressBtnBorderColorSync = false;
        }

        private void TxtBtnBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnBorderColorSync) return;
            var parsedColor = TryParseColor(TxtBtnBorderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressBtnBorderColorSync = true;
                BtnBorderColorPicker.SelectedColor = parsedColor.Value;
                _suppressBtnBorderColorSync = false;
            }
        }

        private void BtnBgColorHoverPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnBgColorHoverSync) return;
            var newColor = BtnBgColorHoverPicker.SelectedColor;
            _suppressBtnBgColorHoverSync = true;
            TxtBtnBgColorHoverHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressBtnBgColorHoverSync = false;
        }

        private void TxtBtnBgColorHoverHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressBtnBgColorHoverSync) return;
            var parsedColor = TryParseColor(TxtBtnBgColorHoverHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressBtnBgColorHoverSync = true;
                BtnBgColorHoverPicker.SelectedColor = parsedColor.Value;
                _suppressBtnBgColorHoverSync = false;
            }
        }

        private void ScrollThumbColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollThumbColorSync) return;
            var newColor = ScrollThumbColorPicker.SelectedColor;
            _suppressScrollThumbColorSync = true;
            TxtScrollThumbColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressScrollThumbColorSync = false;
        }

        private void TxtScrollThumbColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollThumbColorSync) return;
            var parsedColor = TryParseColor(TxtScrollThumbColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressScrollThumbColorSync = true;
                ScrollThumbColorPicker.SelectedColor = parsedColor.Value;
                _suppressScrollThumbColorSync = false;
            }
        }

        private void ScrollThumbHoverColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollThumbHoverColorSync) return;
            var newColor = ScrollThumbHoverColorPicker.SelectedColor;
            _suppressScrollThumbHoverColorSync = true;
            TxtScrollThumbHoverColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressScrollThumbHoverColorSync = false;
        }

        private void TxtScrollThumbHoverColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollThumbHoverColorSync) return;
            var parsedColor = TryParseColor(TxtScrollThumbHoverColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressScrollThumbHoverColorSync = true;
                ScrollThumbHoverColorPicker.SelectedColor = parsedColor.Value;
                _suppressScrollThumbHoverColorSync = false;
            }
        }

        private void ScrollThumbDragColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollThumbDragColorSync) return;
            var newColor = ScrollThumbDragColorPicker.SelectedColor;
            _suppressScrollThumbDragColorSync = true;
            TxtScrollThumbDragColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressScrollThumbDragColorSync = false;
        }

        private void TxtScrollThumbDragColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollThumbDragColorSync) return;
            var parsedColor = TryParseColor(TxtScrollThumbDragColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressScrollThumbDragColorSync = true;
                ScrollThumbDragColorPicker.SelectedColor = parsedColor.Value;
                _suppressScrollThumbDragColorSync = false;
            }
        }

        private void ScrollTrackColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollTrackColorSync) return;
            var newColor = ScrollTrackColorPicker.SelectedColor;
            _suppressScrollTrackColorSync = true;
            TxtScrollTrackColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressScrollTrackColorSync = false;
        }

        private void TxtScrollTrackColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollTrackColorSync) return;
            var parsedColor = TryParseColor(TxtScrollTrackColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressScrollTrackColorSync = true;
                ScrollTrackColorPicker.SelectedColor = parsedColor.Value;
                _suppressScrollTrackColorSync = false;
            }
        }

        private void ScrollTrackHoverColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollTrackHoverColorSync) return;
            var newColor = ScrollTrackHoverColorPicker.SelectedColor;
            _suppressScrollTrackHoverColorSync = true;
            TxtScrollTrackHoverColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressScrollTrackHoverColorSync = false;
        }

        private void TxtScrollTrackHoverColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressScrollTrackHoverColorSync) return;
            var parsedColor = TryParseColor(TxtScrollTrackHoverColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressScrollTrackHoverColorSync = true;
                ScrollTrackHoverColorPicker.SelectedColor = parsedColor.Value;
                _suppressScrollTrackHoverColorSync = false;
            }
        }

        private void FolderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressFolderColorSync) return;
            var newColor = FolderColorPicker.SelectedColor;
            _suppressFolderColorSync = true;
            TxtFolderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressFolderColorSync = false;
        }

        private void TxtFolderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressFolderColorSync) return;
            var parsedColor = TryParseColor(TxtFolderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressFolderColorSync = true;
                FolderColorPicker.SelectedColor = parsedColor.Value;
                _suppressFolderColorSync = false;
            }
        }

        private void GameColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGameColorSync) return;
            var newColor = GameColorPicker.SelectedColor;
            _suppressGameColorSync = true;
            TxtGameColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressGameColorSync = false;
        }

        private void TxtGameColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGameColorSync) return;
            var parsedColor = TryParseColor(TxtGameColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressGameColorSync = true;
                GameColorPicker.SelectedColor = parsedColor.Value;
                _suppressGameColorSync = false;
            }
        }

        private void HeaderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressHeaderColorSync) return;
            var newColor = HeaderColorPicker.SelectedColor;
            _suppressHeaderColorSync = true;
            TxtHeaderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressHeaderColorSync = false;
        }

        private void TxtHeaderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressHeaderColorSync) return;
            var parsedColor = TryParseColor(TxtHeaderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressHeaderColorSync = true;
                HeaderColorPicker.SelectedColor = parsedColor.Value;
                _suppressHeaderColorSync = false;
            }
        }

        private void SubHeaderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSubHeaderColorSync) return;
            var newColor = SubHeaderColorPicker.SelectedColor;
            _suppressSubHeaderColorSync = true;
            TxtSubHeaderColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressSubHeaderColorSync = false;
        }

        private void TxtSubHeaderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressSubHeaderColorSync) return;
            var parsedColor = TryParseColor(TxtSubHeaderColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressSubHeaderColorSync = true;
                SubHeaderColorPicker.SelectedColor = parsedColor.Value;
                _suppressSubHeaderColorSync = false;
            }
        }

        private void StandardColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressStandardColorSync) return;
            var newColor = StandardColorPicker.SelectedColor;
            _suppressStandardColorSync = true;
            TxtStandardColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressStandardColorSync = false;
        }

        private void TxtStandardColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressStandardColorSync) return;
            var parsedColor = TryParseColor(TxtStandardColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressStandardColorSync = true;
                StandardColorPicker.SelectedColor = parsedColor.Value;
                _suppressStandardColorSync = false;
            }
        }

        private void TabColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressTabColorSync) return;
            var newColor = TabColorPicker.SelectedColor;
            _suppressTabColorSync = true;
            TxtTabColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressTabColorSync = false;
        }

        private void TxtTabColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressTabColorSync) return;
            var parsedColor = TryParseColor(TxtTabColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressTabColorSync = true;
                TabColorPicker.SelectedColor = parsedColor.Value;
                _suppressTabColorSync = false;
            }
        }

        private void FolderSelectedColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressFolderSelectedColorSync) return;
            var newColor = FolderSelectedColorPicker.SelectedColor;
            _suppressFolderSelectedColorSync = true;
            TxtFolderSelectedColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressFolderSelectedColorSync = false;
        }

        private void TxtFolderSelectedColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressFolderSelectedColorSync) return;
            var parsedColor = TryParseColor(TxtFolderSelectedColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressFolderSelectedColorSync = true;
                FolderSelectedColorPicker.SelectedColor = parsedColor.Value;
                _suppressFolderSelectedColorSync = false;
            }
        }

        private void FolderSelectedBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressFolderSelectedBgColorSync) return;
            var newColor = FolderSelectedBgColorPicker.SelectedColor;
            _suppressFolderSelectedBgColorSync = true;
            TxtFolderSelectedBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressFolderSelectedBgColorSync = false;
        }

        private void TxtFolderSelectedBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressFolderSelectedBgColorSync) return;
            var parsedColor = TryParseColor(TxtFolderSelectedBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressFolderSelectedBgColorSync = true;
                FolderSelectedBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressFolderSelectedBgColorSync = false;
            }
        }

        private void GameSelectedColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGameSelectedColorSync) return;
            var newColor = GameSelectedColorPicker.SelectedColor;
            _suppressGameSelectedColorSync = true;
            TxtGameSelectedColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressGameSelectedColorSync = false;
        }

        private void TxtGameSelectedColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGameSelectedColorSync) return;
            var parsedColor = TryParseColor(TxtGameSelectedColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressGameSelectedColorSync = true;
                GameSelectedColorPicker.SelectedColor = parsedColor.Value;
                _suppressGameSelectedColorSync = false;
            }
        }

        private void GameSelectedBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGameSelectedBgColorSync) return;
            var newColor = GameSelectedBgColorPicker.SelectedColor;
            _suppressGameSelectedBgColorSync = true;
            TxtGameSelectedBgColorHex.Text = newColor.A == 0 ? "Transparent" : $"#{newColor.A:X2}{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressGameSelectedBgColorSync = false;
        }

        private void TxtGameSelectedBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGameSelectedBgColorSync) return;
            var parsedColor = TryParseColor(TxtGameSelectedBgColorHex.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressGameSelectedBgColorSync = true;
                GameSelectedBgColorPicker.SelectedColor = parsedColor.Value;
                _suppressGameSelectedBgColorSync = false;
            }
        }

        // Normalizes a hex color string to #RRGGBB (uppercase, # prefix, stripping alpha if present).
        // Any recognized WPF named color (Transparent, Red, DodgerBlue, etc.) passes through as its proper
        // canonical name instead of getting a "#" incorrectly prepended - mirrors SafeConvertToBrush's
        // IsKnownColorName check in MainViewModel.cs, so load-time display and live rendering agree.
        private string FormatHexCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string cleanInput = input.Trim();

            var namedColorProperty = typeof(System.Windows.Media.Colors).GetProperty(
                cleanInput, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
            if (namedColorProperty != null) return namedColorProperty.Name;

            cleanInput = cleanInput.ToUpper();
            if (!cleanInput.StartsWith("#")) cleanInput = "#" + cleanInput;
            return cleanInput;
        }

        // Prepends "#" to a bare hex string (e.g. "1C1C1E" or "FF1C1C1E") before it's persisted to
        // settings, so saving doesn't require the user to type the "#" themselves. Unlike FormatHexCode,
        // this does not strip the alpha channel or uppercase - it only fixes the missing "#" so ARGB
        // values from the alpha-enabled pickers round-trip intact. "Transparent" and WPF named colors
        // (e.g. "Red") are passed through untouched.
        private static string NormalizeHexInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            string trimmed = input.Trim();
            if (trimmed.StartsWith("#")) return trimmed;
            if (trimmed.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) return "Transparent";
            if (IsBareHexDigits(trimmed)) return "#" + trimmed;
            return trimmed;
        }

        // True if the string is exactly 6 or 8 hex digits (RGB or ARGB with no "#" prefix)
        private static bool IsBareHexDigits(string input)
        {
            if (input.Length != 6 && input.Length != 8) return false;
            foreach (char c in input)
            {
                if (!Uri.IsHexDigit(c)) return false;
            }
            return true;
        }

        // Parses a hex (or "Transparent") string into a Color, returning null instead of throwing on
        // invalid input. Shared by every picker field's LostFocus handler and load-sync block.
        private static System.Windows.Media.Color? TryParseColor(string hexText)
        {
            try
            {
                var brush = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hexText);
                return brush.Color;
            }
            catch
            {
                return null;
            }
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
            {
                var parsedColor = TryParseColor(TxtMainColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMainColorSync = true;
                    MainColorPicker.SelectedColor = parsedColor.Value;
                    _suppressMainColorSync = false;
                }
            }
            TxtGamesColorHex.Text = FormatHexCode(_settings.FileListBg);
            {
                var parsedColor = TryParseColor(TxtGamesColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressGamesColorSync = true;
                    GamesColorPicker.SelectedColor = parsedColor.Value;
                    _suppressGamesColorSync = false;
                }
            }
            TxtMarqueeColorHex.Text = FormatHexCode(_settings.MarqueeBoxBg);
            {
                var parsedColor = TryParseColor(TxtMarqueeColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMarqueeColorSync = true;
                    MarqueeColorPicker.SelectedColor = parsedColor.Value;
                    _suppressMarqueeColorSync = false;
                }
            }
            TxtMediaColorHex.Text = FormatHexCode(_settings.VideoBoxBg);
            {
                var parsedColor = TryParseColor(TxtMediaColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMediaColorSync = true;
                    MediaColorPicker.SelectedColor = parsedColor.Value;
                    _suppressMediaColorSync = false;
                }
            }
            TxtOptionsColorHex.Text = FormatHexCode(_settings.OptionsBg);
            {
                var parsedColor = TryParseColor(TxtOptionsColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressOptionsColorSync = true;
                    OptionsColorPicker.SelectedColor = parsedColor.Value;
                    _suppressOptionsColorSync = false;
                }
            }
            // Framework Border Color sync removed - no UI field currently exists (see note near
            // SeparatorColorPicker_ColorChanged). _settings.BorderColorFramework is untouched here,
            // so it still loads correctly for runtime use, it just isn't reflected in any control.
            TxtSearchBoxFontSize.Text = _settings.SearchBoxFontSize.ToString();
            TxtSearchBoxColorHex.Text = FormatHexCode(_settings.SearchBoxColorHex);
            {
                var parsedColor = TryParseColor(TxtSearchBoxColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressSearchBoxColorSync = true;
                    SearchBoxColorPicker.SelectedColor = parsedColor.Value;
                    _suppressSearchBoxColorSync = false;
                }
            }
            TxtSearchBoxBgColorHex.Text = FormatHexCode(_settings.SearchBoxBgColorHex);
            {
                var parsedColor = TryParseColor(TxtSearchBoxBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressSearchBoxBgColorSync = true;
                    SearchBoxBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressSearchBoxBgColorSync = false;
                }
            }
            TxtGameNameHeaderFontSize.Text = _settings.GameNameHeaderFontSize.ToString();
            TxtGameNameHeaderColorHex.Text = FormatHexCode(_settings.GameNameHeaderColorHex);
            {
                var parsedColor = TryParseColor(TxtGameNameHeaderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressGameNameHeaderColorSync = true;
                    GameNameHeaderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressGameNameHeaderColorSync = false;
                }
            }
            TxtPubYearRatingFontSize.Text = _settings.PubYearRatingFontSize.ToString();
            TxtPubYearRatingColorHex.Text = FormatHexCode(_settings.PubYearRatingColorHex);
            {
                var parsedColor = TryParseColor(TxtPubYearRatingColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressPubYearRatingColorSync = true;
                    PubYearRatingColorPicker.SelectedColor = parsedColor.Value;
                    _suppressPubYearRatingColorSync = false;
                }
            }
            TxtInfoHeaderFontSize.Text = _settings.InfoHeaderFontSize.ToString();
            TxtInfoHeaderColorHex.Text = FormatHexCode(_settings.InfoHeaderColorHex);
            {
                var parsedColor = TryParseColor(TxtInfoHeaderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressInfoHeaderColorSync = true;
                    InfoHeaderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressInfoHeaderColorSync = false;
                }
            }
            TxtInfoBodyFontSize.Text = _settings.InfoBodyFontSize.ToString();
            TxtInfoBodyColorHex.Text = FormatHexCode(_settings.InfoBodyColorHex);
            {
                var parsedColor = TryParseColor(TxtInfoBodyColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressInfoBodyColorSync = true;
                    InfoBodyColorPicker.SelectedColor = parsedColor.Value;
                    _suppressInfoBodyColorSync = false;
                }
            }
            TxtCreditsHeaderFontSize.Text = _settings.CreditsHeaderFontSize.ToString();
            TxtCreditsHeaderColorHex.Text = FormatHexCode(_settings.CreditsHeaderColorHex);
            {
                var parsedColor = TryParseColor(TxtCreditsHeaderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressCreditsHeaderColorSync = true;
                    CreditsHeaderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressCreditsHeaderColorSync = false;
                }
            }
            TxtCreditsBodyFontSize.Text = _settings.CreditsBodyFontSize.ToString();
            TxtCreditsBodyColorHex.Text = FormatHexCode(_settings.CreditsBodyColorHex);
            {
                var parsedColor = TryParseColor(TxtCreditsBodyColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressCreditsBodyColorSync = true;
                    CreditsBodyColorPicker.SelectedColor = parsedColor.Value;
                    _suppressCreditsBodyColorSync = false;
                }
            }
            TxtVideoBgColorHex.Text = FormatHexCode(_settings.VideoBgColorHex);
            {
                var parsedColor = TryParseColor(TxtVideoBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressVideoBgColorSync = true;
                    VideoBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressVideoBgColorSync = false;
                }
            }
            TxtVideoBorderSize.Text = _settings.VideoBorderSize.ToString();
            TxtVideoBorderColorHex.Text = FormatHexCode(_settings.VideoBorderColorHex);
            {
                var parsedColor = TryParseColor(TxtVideoBorderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressVideoBorderColorSync = true;
                    VideoBorderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressVideoBorderColorSync = false;
                }
            }
            TxtVideoBorderRadius.Text = _settings.VideoBorderRadius.ToString();
            TxtNavIconsColorHex.Text = FormatHexCode(_settings.NavIconsColorHex);
            {
                var parsedColor = TryParseColor(TxtNavIconsColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressNavIconsColorSync = true;
                    NavIconsColorPicker.SelectedColor = parsedColor.Value;
                    _suppressNavIconsColorSync = false;
                }
            }
            TxtNavIconsSize.Text = _settings.NavIconsSize.ToString();
            TxtPreviewBorderSize.Text = _settings.PreviewBorderSize.ToString();
            TxtPreviewBorderColorHex.Text = FormatHexCode(_settings.PreviewBorderColorHex);
            {
                var parsedColor = TryParseColor(TxtPreviewBorderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressPreviewBorderColorSync = true;
                    PreviewBorderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressPreviewBorderColorSync = false;
                }
            }
            TxtPreviewBorderRadius.Text = _settings.PreviewBorderRadius.ToString();
            TxtSearchLabelFontSize.Text = _settings.SearchLabelFontSize.ToString();
            TxtSearchLabelColorHex.Text = FormatHexCode(_settings.SearchLabelColorHex);
            {
                var parsedColor = TryParseColor(TxtSearchLabelColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressSearchLabelColorSync = true;
                    SearchLabelColorPicker.SelectedColor = parsedColor.Value;
                    _suppressSearchLabelColorSync = false;
                }
            }
            TxtSearchLabelBgColorHex.Text = FormatHexCode(_settings.SearchLabelBgColorHex);
            {
                var parsedColor = TryParseColor(TxtSearchLabelBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressSearchLabelBgColorSync = true;
                    SearchLabelBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressSearchLabelBgColorSync = false;
                }
            }
            TxtMainWinBtnFontSize.Text = _settings.MainWinBtnFontSize.ToString();
            TxtMainWinBtnColorHex.Text = FormatHexCode(_settings.MainWinBtnColorHex);
            {
                var parsedColor = TryParseColor(TxtMainWinBtnColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMainWinBtnColorSync = true;
                    MainWinBtnColorPicker.SelectedColor = parsedColor.Value;
                    _suppressMainWinBtnColorSync = false;
                }
            }
            TxtMainWinBtnBgColorHex.Text = FormatHexCode(_settings.MainWinBtnBgColorHex);
            {
                var parsedColor = TryParseColor(TxtMainWinBtnBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMainWinBtnBgColorSync = true;
                    MainWinBtnBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressMainWinBtnBgColorSync = false;
                }
            }
            TxtMainWinBtnColorHoverHex.Text = FormatHexCode(_settings.MainWinBtnColorHoverHex);
            {
                var parsedColor = TryParseColor(TxtMainWinBtnColorHoverHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMainWinBtnColorHoverSync = true;
                    MainWinBtnColorHoverPicker.SelectedColor = parsedColor.Value;
                    _suppressMainWinBtnColorHoverSync = false;
                }
            }
            TxtMainWinBtnBgColorHoverHex.Text = FormatHexCode(_settings.MainWinBtnBgColorHoverHex);
            {
                var parsedColor = TryParseColor(TxtMainWinBtnBgColorHoverHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMainWinBtnBgColorHoverSync = true;
                    MainWinBtnBgColorHoverPicker.SelectedColor = parsedColor.Value;
                    _suppressMainWinBtnBgColorHoverSync = false;
                }
            }
            TxtMainWinBtnBorderSize.Text = _settings.MainWinBtnBorderSize.ToString();
            TxtMainWinBtnBorderColorHex.Text = FormatHexCode(_settings.MainWinBtnBorderColorHex);
            {
                var parsedColor = TryParseColor(TxtMainWinBtnBorderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMainWinBtnBorderColorSync = true;
                    MainWinBtnBorderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressMainWinBtnBorderColorSync = false;
                }
            }
            TxtMainWinBtnCornerRadius.Text = _settings.MainWinBtnCornerRadius.ToString();
            TxtGamesBorderSize.Text = _settings.GamesBorderSize.ToString();
            TxtGamesBorderColorHex.Text = FormatHexCode(_settings.GamesBorderColorHex);
            {
                var parsedColor = TryParseColor(TxtGamesBorderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressGamesBorderColorSync = true;
                    GamesBorderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressGamesBorderColorSync = false;
                }
            }
            TxtGamesBorderCornerRadius.Text = _settings.GamesBorderCornerRadius.ToString();
            TxtContextMenuFontColorHex.Text = FormatHexCode(_settings.ContextMenuFontColorHex);
            {
                var parsedColor = TryParseColor(TxtContextMenuFontColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressContextMenuFontColorSync = true;
                    ContextMenuFontColorPicker.SelectedColor = parsedColor.Value;
                    _suppressContextMenuFontColorSync = false;
                }
            }
            TxtContextMenuIconColorHex.Text = FormatHexCode(_settings.ContextMenuIconColorHex);
            {
                var parsedColor = TryParseColor(TxtContextMenuIconColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressContextMenuIconColorSync = true;
                    ContextMenuIconColorPicker.SelectedColor = parsedColor.Value;
                    _suppressContextMenuIconColorSync = false;
                }
            }
            TxtContextMenuBgColorHex.Text = FormatHexCode(_settings.ContextMenuBgColorHex);
            {
                var parsedColor = TryParseColor(TxtContextMenuBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressContextMenuBgColorSync = true;
                    ContextMenuBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressContextMenuBgColorSync = false;
                }
            }
            TxtContextMenuHoverColorHex.Text = FormatHexCode(_settings.ContextMenuHoverColorHex);
            {
                var parsedColor = TryParseColor(TxtContextMenuHoverColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressContextMenuHoverColorSync = true;
                    ContextMenuHoverColorPicker.SelectedColor = parsedColor.Value;
                    _suppressContextMenuHoverColorSync = false;
                }
            }
            TxtContextMenuHoverBgColorHex.Text = FormatHexCode(_settings.ContextMenuHoverBgColorHex);
            {
                var parsedColor = TryParseColor(TxtContextMenuHoverBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressContextMenuHoverBgColorSync = true;
                    ContextMenuHoverBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressContextMenuHoverBgColorSync = false;
                }
            }
            TxtMarqueeBorderCornerRadius.Text = _settings.MarqueeBorderRadius.ToString();
            TxtSubTextFontSize.Text = _settings.SubTextFontSize.ToString();
            TxtSubTextColorHex.Text = FormatHexCode(_settings.SubTextColorHex);
            {
                var parsedColor = TryParseColor(TxtSubTextColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressSubTextColorSync = true;
                    SubTextColorPicker.SelectedColor = parsedColor.Value;
                    _suppressSubTextColorSync = false;
                }
            }
            TxtOptionsMenuBgColorHex.Text = FormatHexCode(_settings.OptionsMenuBgColorHex);
            {
                var parsedColor = TryParseColor(TxtOptionsMenuBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressOptionsMenuBgColorSync = true;
                    OptionsMenuBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressOptionsMenuBgColorSync = false;
                }
            }
            TxtOptionsMenuBorderSize.Text = _settings.OptionsMenuBorderSize.ToString();
            TxtOptionsMenuBorderColorHex.Text = FormatHexCode(_settings.OptionsMenuBorderColorHex);
            {
                var parsedColor = TryParseColor(TxtOptionsMenuBorderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressOptionsMenuBorderColorSync = true;
                    OptionsMenuBorderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressOptionsMenuBorderColorSync = false;
                }
            }
            TxtOptionsMenuBorderRadius.Text = _settings.OptionsMenuBorderRadius.ToString();
            TxtTabColorHoverHex.Text = FormatHexCode(_settings.TabColorHoverHex);
            {
                var parsedColor = TryParseColor(TxtTabColorHoverHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressTabColorHoverSync = true;
                    TabColorHoverPicker.SelectedColor = parsedColor.Value;
                    _suppressTabColorHoverSync = false;
                }
            }
            TxtTabBgColorHoverHex.Text = FormatHexCode(_settings.TabBgColorHoverHex);
            {
                var parsedColor = TryParseColor(TxtTabBgColorHoverHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressTabBgColorHoverSync = true;
                    TabBgColorHoverPicker.SelectedColor = parsedColor.Value;
                    _suppressTabBgColorHoverSync = false;
                }
            }
            TxtOptionsBtnFontSize.Text = _settings.OptionsBtnFontSize.ToString();
            TxtOptionsBtnColorHoverHex.Text = FormatHexCode(_settings.OptionsBtnColorHoverHex);
            {
                var parsedColor = TryParseColor(TxtOptionsBtnColorHoverHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressOptionsBtnColorHoverSync = true;
                    OptionsBtnColorHoverPicker.SelectedColor = parsedColor.Value;
                    _suppressOptionsBtnColorHoverSync = false;
                }
            }
            TxtOptionsBtnBorderSize.Text = _settings.OptionsBtnBorderSize.ToString();
            TxtOptionsBtnBorderRadius.Text = _settings.OptionsBtnBorderRadius.ToString();
            TxtScrollTrackColorHex.Text = FormatHexCode(_settings.ScrollTrackColor);
            {
                var parsedColor = TryParseColor(TxtScrollTrackColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressScrollTrackColorSync = true;
                    ScrollTrackColorPicker.SelectedColor = parsedColor.Value;
                    _suppressScrollTrackColorSync = false;
                }
            }
            TxtScrollTrackHoverColorHex.Text = FormatHexCode(_settings.ScrollTrackHoverColor);
            {
                var parsedColor = TryParseColor(TxtScrollTrackHoverColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressScrollTrackHoverColorSync = true;
                    ScrollTrackHoverColorPicker.SelectedColor = parsedColor.Value;
                    _suppressScrollTrackHoverColorSync = false;
                }
            }
            TxtScrollThumbColorHex.Text = FormatHexCode(_settings.ScrollThumbColor);
            {
                var parsedColor = TryParseColor(TxtScrollThumbColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressScrollThumbColorSync = true;
                    ScrollThumbColorPicker.SelectedColor = parsedColor.Value;
                    _suppressScrollThumbColorSync = false;
                }
            }
            TxtScrollThumbHoverColorHex.Text = FormatHexCode(_settings.ScrollThumbHoverColor);
            {
                var parsedColor = TryParseColor(TxtScrollThumbHoverColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressScrollThumbHoverColorSync = true;
                    ScrollThumbHoverColorPicker.SelectedColor = parsedColor.Value;
                    _suppressScrollThumbHoverColorSync = false;
                }
            }
            TxtScrollThumbDragColorHex.Text = FormatHexCode(_settings.ScrollThumbDragColor);
            {
                var parsedColor = TryParseColor(TxtScrollThumbDragColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressScrollThumbDragColorSync = true;
                    ScrollThumbDragColorPicker.SelectedColor = parsedColor.Value;
                    _suppressScrollThumbDragColorSync = false;
                }
            }

            // Framework Border Color / Border Width / Border Curve sync removed - no UI fields exist for
            // any of these anymore (BorderWidthValue/BorderCurveValue retired entirely; BorderColorFramework
            // has no field yet, see earlier note).
            TxtSeparatorColorHex.Text = FormatHexCode(_settings.SeparatorColorHex);
            {
                var parsedColor = TryParseColor(TxtSeparatorColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressSeparatorColorSync = true;
                    SeparatorColorPicker.SelectedColor = parsedColor.Value;
                    _suppressSeparatorColorSync = false;
                }
            }
            TxtMarqueeBorderColorHex.Text = FormatHexCode(_settings.MarqueeBorderColorHex);
            {
                var parsedColor = TryParseColor(TxtMarqueeBorderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressMarqueeBorderColorSync = true;
                    MarqueeBorderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressMarqueeBorderColorSync = false;
                }
            }
            TxtMarqueeBorderWidthValue.Text = _settings.MarqueeBorderWidthValue.ToString();

            // Typography & State Colors (Filtered through Gatekeeper)
            TxtFolderFontSize.Text = _settings.FolderFontSize.ToString();
            TxtFolderColorHex.Text = FormatHexCode(_settings.FolderColorHex);
            {
                var parsedColor = TryParseColor(TxtFolderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressFolderColorSync = true;
                    FolderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressFolderColorSync = false;
                }
            }
            TxtFolderSelectedColorHex.Text = FormatHexCode(_settings.FolderSelectedColorHex);
            {
                var parsedColor = TryParseColor(TxtFolderSelectedColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressFolderSelectedColorSync = true;
                    FolderSelectedColorPicker.SelectedColor = parsedColor.Value;
                    _suppressFolderSelectedColorSync = false;
                }
            }
            TxtFolderSelectedBgColorHex.Text = FormatHexCode(_settings.FolderSelectedBgColorHex);
            {
                var parsedColor = TryParseColor(TxtFolderSelectedBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressFolderSelectedBgColorSync = true;
                    FolderSelectedBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressFolderSelectedBgColorSync = false;
                }
            }
            TxtGameFontSize.Text = _settings.GameFontSize.ToString();
            TxtGameColorHex.Text = FormatHexCode(_settings.GameColorHex);
            {
                var parsedColor = TryParseColor(TxtGameColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressGameColorSync = true;
                    GameColorPicker.SelectedColor = parsedColor.Value;
                    _suppressGameColorSync = false;
                }
            }
            TxtGameHoverColorHex.Text = FormatHexCode(_settings.GameHoverColorHex);
            {
                var parsedColor = TryParseColor(TxtGameHoverColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressGameHoverColorSync = true;
                    GameHoverColorPicker.SelectedColor = parsedColor.Value;
                    _suppressGameHoverColorSync = false;
                }
            }
            TxtGameSelectedColorHex.Text = FormatHexCode(_settings.GameSelectedColorHex);
            {
                var parsedColor = TryParseColor(TxtGameSelectedColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressGameSelectedColorSync = true;
                    GameSelectedColorPicker.SelectedColor = parsedColor.Value;
                    _suppressGameSelectedColorSync = false;
                }
            }
            TxtGameSelectedBgColorHex.Text = FormatHexCode(_settings.GameSelectedBgColorHex);
            {
                var parsedColor = TryParseColor(TxtGameSelectedBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressGameSelectedBgColorSync = true;
                    GameSelectedBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressGameSelectedBgColorSync = false;
                }
            }
            TxtFavoritesColorHex.Text = FormatHexCode(_settings.FavoritesColorHex);
            {
                var parsedColor = TryParseColor(TxtFavoritesColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressFavoritesColorSync = true;
                    FavoritesColorPicker.SelectedColor = parsedColor.Value;
                    _suppressFavoritesColorSync = false;
                }
            }
            TxtArrowColorHex.Text = FormatHexCode(_settings.ArrowColorHex);
            {
                var parsedColor = TryParseColor(TxtArrowColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressArrowColorSync = true;
                    ArrowColorPicker.SelectedColor = parsedColor.Value;
                    _suppressArrowColorSync = false;
                }
            }
            TxtTabFontSize.Text = _settings.TabFontSize.ToString();
            TxtTabColorHex.Text = FormatHexCode(_settings.TabColorHex);
            {
                var parsedColor = TryParseColor(TxtTabColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressTabColorSync = true;
                    TabColorPicker.SelectedColor = parsedColor.Value;
                    _suppressTabColorSync = false;
                }
            }
            TxtTabBgColorHex.Text = _settings.TabBgColorHex;
            {
                var parsedColor = TryParseColor(_settings.TabBgColorHex);
                if (parsedColor.HasValue)
                {
                    _suppressTabBgColorSync = true;
                    TabBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressTabBgColorSync = false;
                }
            }
            TxtTabActiveColorHex.Text = FormatHexCode(_settings.TabActiveColorHex);
            {
                var parsedColor = TryParseColor(TxtTabActiveColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressTabActiveColorSync = true;
                    TabActiveColorPicker.SelectedColor = parsedColor.Value;
                    _suppressTabActiveColorSync = false;
                }
            }
            TxtTabActiveBgColorHex.Text = FormatHexCode(_settings.TabActiveBgColorHex);
            {
                var parsedColor = TryParseColor(TxtTabActiveBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressTabActiveBgColorSync = true;
                    TabActiveBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressTabActiveBgColorSync = false;
                }
            }
            TxtHeaderFontSize.Text = _settings.HeaderFontSize.ToString();
            TxtHeaderColorHex.Text = FormatHexCode(_settings.HeaderColorHex);
            {
                var parsedColor = TryParseColor(TxtHeaderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressHeaderColorSync = true;
                    HeaderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressHeaderColorSync = false;
                }
            }
            TxtSubHeaderFontSize.Text = _settings.SubHeaderFontSize.ToString();
            TxtSubHeaderColorHex.Text = FormatHexCode(_settings.SubHeaderColorHex);
            {
                var parsedColor = TryParseColor(TxtSubHeaderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressSubHeaderColorSync = true;
                    SubHeaderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressSubHeaderColorSync = false;
                }
            }
            TxtStandardFontSize.Text = _settings.StandardFontSize.ToString();
            TxtStandardColorHex.Text = FormatHexCode(_settings.StandardColorHex);
            {
                var parsedColor = TryParseColor(TxtStandardColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressStandardColorSync = true;
                    StandardColorPicker.SelectedColor = parsedColor.Value;
                    _suppressStandardColorSync = false;
                }
            }
            // TxtInputFontSize.Text = _settings.InputFontSize.ToString();
            // TxtInputColorHex.Text = FormatHexCode(_settings.InputColorHex);

            // Button Configs (Filtered through Gatekeeper)
            TxtBtnBgColorHex.Text = FormatHexCode(_settings.BtnBgColorHex);
            {
                var parsedColor = TryParseColor(TxtBtnBgColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressBtnBgColorSync = true;
                    BtnBgColorPicker.SelectedColor = parsedColor.Value;
                    _suppressBtnBgColorSync = false;
                }
            }
            TxtBtnBorderColorHex.Text = FormatHexCode(_settings.BtnBorderColorHex);
            {
                var parsedColor = TryParseColor(TxtBtnBorderColorHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressBtnBorderColorSync = true;
                    BtnBorderColorPicker.SelectedColor = parsedColor.Value;
                    _suppressBtnBorderColorSync = false;
                }
            }
            TxtBtnTextColorNormalHex.Text = FormatHexCode(_settings.BtnTextColorNormalHex);
            {
                var parsedColor = TryParseColor(TxtBtnTextColorNormalHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressBtnTextColorNormalSync = true;
                    BtnTextColorNormalPicker.SelectedColor = parsedColor.Value;
                    _suppressBtnTextColorNormalSync = false;
                }
            }
            TxtBtnBgColorHoverHex.Text = FormatHexCode(_settings.BtnBgColorHoverHex);
            {
                var parsedColor = TryParseColor(TxtBtnBgColorHoverHex.Text);
                if (parsedColor.HasValue)
                {
                    _suppressBtnBgColorHoverSync = true;
                    BtnBgColorHoverPicker.SelectedColor = parsedColor.Value;
                    _suppressBtnBgColorHoverSync = false;
                }
            }
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
                    if (_viewModel.LoadTheme(selectedTheme))
                    {
                        LoadCurrentThemeValuesIntoUi();
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
        // [END SUB-SECTION: Load Theme - SYNC POINT 1 of 3]

        // [SECTION: Theme Archive Asset Bundling]
        // Resolves a theme asset property's stored value to a real absolute path. Stored values follow
        // BtnBrowseFile_Click's portable-friendly convention: relative to the MAME root when the file was
        // on the same drive and inside that folder tree, absolute otherwise.
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

        // [SUB-SECTION: Save Theme - SYNC POINT 2 of 3]
        // Validates the theme name (prompting for one if "[New Theme...]" is selected), syncs the UI into
        // _settings, builds a ThemeSettings DTO, bundles its 7 referenced asset files into a staging
        // assets/ folder (rewriting the DTO's copies of those properties to archive-relative paths), then
        // zips the result to the theme's .zip file. DANGER: any new theme property must be added to this
        // DTO too - see the sync point warning above.
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
                    MarqueeBorderColorHex = _settings.MarqueeBorderColorHex,
                    MarqueeBorderWidthValue = _settings.MarqueeBorderWidthValue,
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
                    SearchBoxFontSize = _settings.SearchBoxFontSize,
                    SearchBoxColorHex = _settings.SearchBoxColorHex,
                    SearchBoxBgColorHex = _settings.SearchBoxBgColorHex,
                    GameNameHeaderFontSize = _settings.GameNameHeaderFontSize,
                    GameNameHeaderColorHex = _settings.GameNameHeaderColorHex,
                    PubYearRatingFontSize = _settings.PubYearRatingFontSize,
                    PubYearRatingColorHex = _settings.PubYearRatingColorHex,
                    InfoHeaderFontSize = _settings.InfoHeaderFontSize,
                    InfoHeaderColorHex = _settings.InfoHeaderColorHex,
                    InfoBodyFontSize = _settings.InfoBodyFontSize,
                    InfoBodyColorHex = _settings.InfoBodyColorHex,
                    CreditsHeaderFontSize = _settings.CreditsHeaderFontSize,
                    CreditsHeaderColorHex = _settings.CreditsHeaderColorHex,
                    CreditsBodyFontSize = _settings.CreditsBodyFontSize,
                    CreditsBodyColorHex = _settings.CreditsBodyColorHex,
                    VideoBgColorHex = _settings.VideoBgColorHex,
                    VideoBorderSize = _settings.VideoBorderSize,
                    VideoBorderColorHex = _settings.VideoBorderColorHex,
                    VideoBorderRadius = _settings.VideoBorderRadius,
                    NavIconsColorHex = _settings.NavIconsColorHex,
                    NavIconsSize = _settings.NavIconsSize,
                    PreviewBorderSize = _settings.PreviewBorderSize,
                    PreviewBorderColorHex = _settings.PreviewBorderColorHex,
                    PreviewBorderRadius = _settings.PreviewBorderRadius,
                    SearchLabelFontSize = _settings.SearchLabelFontSize,
                    SearchLabelColorHex = _settings.SearchLabelColorHex,
                    SearchLabelBgColorHex = _settings.SearchLabelBgColorHex,
                    MainWinBtnFontSize = _settings.MainWinBtnFontSize,
                    MainWinBtnColorHex = _settings.MainWinBtnColorHex,
                    MainWinBtnBgColorHex = _settings.MainWinBtnBgColorHex,
                    MainWinBtnColorHoverHex = _settings.MainWinBtnColorHoverHex,
                    MainWinBtnBgColorHoverHex = _settings.MainWinBtnBgColorHoverHex,
                    MainWinBtnBorderSize = _settings.MainWinBtnBorderSize,
                    MainWinBtnBorderColorHex = _settings.MainWinBtnBorderColorHex,
                    MainWinBtnCornerRadius = _settings.MainWinBtnCornerRadius,
                    GamesBorderSize = _settings.GamesBorderSize,
                    GamesBorderColorHex = _settings.GamesBorderColorHex,
                    GamesBorderCornerRadius = _settings.GamesBorderCornerRadius,
                    ContextMenuFontColorHex = _settings.ContextMenuFontColorHex,
                    ContextMenuIconColorHex = _settings.ContextMenuIconColorHex,
                    ContextMenuBgColorHex = _settings.ContextMenuBgColorHex,
                    ContextMenuHoverColorHex = _settings.ContextMenuHoverColorHex,
                    ContextMenuHoverBgColorHex = _settings.ContextMenuHoverBgColorHex,
                    MarqueeBorderRadius = _settings.MarqueeBorderRadius,
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

                // Bundle the 7 referenced asset files into a staging assets/ folder, rewriting the DTO's
                // copies of those properties to archive-relative paths - the original absolute/relative
                // paths on THIS machine are meaningless to whoever imports the theme elsewhere.
                string stagingDir = Path.Combine(Path.GetTempPath(), $"4rcstick_theme_{Guid.NewGuid():N}");
                string stagingAssetsDir = Path.Combine(stagingDir, "assets");
                Directory.CreateDirectory(stagingAssetsDir);

                var missingAssets = new List<string>();

                themeDto.MainWindowWallpaper = BundleThemeAsset(themeDto.MainWindowWallpaper, stagingAssetsDir, missingAssets);
                themeDto.GamesListWallpaper = BundleThemeAsset(themeDto.GamesListWallpaper, stagingAssetsDir, missingAssets);
                themeDto.MarqueeWindowWallpaper = BundleThemeAsset(themeDto.MarqueeWindowWallpaper, stagingAssetsDir, missingAssets);
                themeDto.MediaWindowWallpaper = BundleThemeAsset(themeDto.MediaWindowWallpaper, stagingAssetsDir, missingAssets);
                themeDto.ThemeLogo = BundleThemeAsset(themeDto.ThemeLogo, stagingAssetsDir, missingAssets);
                themeDto.ThemeBootSplash = BundleThemeAsset(themeDto.ThemeBootSplash, stagingAssetsDir, missingAssets);
                themeDto.ThemeMissingPreview = BundleThemeAsset(themeDto.ThemeMissingPreview, stagingAssetsDir, missingAssets);

                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string jsonString = System.Text.Json.JsonSerializer.Serialize(themeDto, jsonOptions);
                File.WriteAllText(Path.Combine(stagingDir, "theme.cfg"), jsonString);

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

            _settings.BackgroundColor = NormalizeHexInput(TxtMainColorHex.Text);
            _settings.FileListBg = NormalizeHexInput(TxtGamesColorHex.Text);
            _settings.MarqueeBoxBg = NormalizeHexInput(TxtMarqueeColorHex.Text);
            _settings.VideoBoxBg = NormalizeHexInput(TxtMediaColorHex.Text);
            _settings.OptionsBg = NormalizeHexInput(TxtOptionsColorHex.Text);
            // BorderColorFramework sync removed (both occurrences) - no UI field exists yet.
            // BorderWidthValue/BorderCurveValue sync removed - properties retired entirely.
            _settings.ScrollTrackColor = NormalizeHexInput(TxtScrollTrackColorHex.Text);
            _settings.ScrollTrackHoverColor = NormalizeHexInput(TxtScrollTrackHoverColorHex.Text);
            _settings.ScrollThumbColor = NormalizeHexInput(TxtScrollThumbColorHex.Text);
            _settings.ScrollThumbHoverColor = NormalizeHexInput(TxtScrollThumbHoverColorHex.Text);
            _settings.ScrollThumbDragColor = NormalizeHexInput(TxtScrollThumbDragColorHex.Text);

            _settings.SeparatorColorHex = NormalizeHexInput(TxtSeparatorColorHex.Text);
            _settings.MarqueeBorderColorHex = NormalizeHexInput(TxtMarqueeBorderColorHex.Text);
            if (double.TryParse(TxtMarqueeBorderWidthValue.Text, out double mbWidth)) _settings.MarqueeBorderWidthValue = mbWidth;

            if (int.TryParse(TxtFolderFontSize.Text, out int fSize)) _settings.FolderFontSize = fSize;
            _settings.FolderColorHex = NormalizeHexInput(TxtFolderColorHex.Text);
            _settings.FolderSelectedColorHex = NormalizeHexInput(TxtFolderSelectedColorHex.Text);
            _settings.FolderSelectedBgColorHex = NormalizeHexInput(TxtFolderSelectedBgColorHex.Text);
            if (int.TryParse(TxtGameFontSize.Text, out int gSize)) _settings.GameFontSize = gSize;
            _settings.GameColorHex = NormalizeHexInput(TxtGameColorHex.Text);
            _settings.GameHoverColorHex = NormalizeHexInput(TxtGameHoverColorHex.Text);
            _settings.GameSelectedColorHex = NormalizeHexInput(TxtGameSelectedColorHex.Text);
            _settings.GameSelectedBgColorHex = NormalizeHexInput(TxtGameSelectedBgColorHex.Text);
            _settings.FavoritesColorHex = NormalizeHexInput(TxtFavoritesColorHex.Text);
            _settings.ArrowColorHex = NormalizeHexInput(TxtArrowColorHex.Text);

            if (int.TryParse(TxtTabFontSize.Text, out int tSize)) _settings.TabFontSize = tSize;
            _settings.TabColorHex = NormalizeHexInput(TxtTabColorHex.Text);
            _settings.TabBgColorHex = NormalizeHexInput(TxtTabBgColorHex.Text);
            _settings.TabActiveColorHex = NormalizeHexInput(TxtTabActiveColorHex.Text);
            _settings.TabActiveBgColorHex = NormalizeHexInput(TxtTabActiveBgColorHex.Text);
            if (int.TryParse(TxtHeaderFontSize.Text, out int hSize)) _settings.HeaderFontSize = hSize;
            _settings.HeaderColorHex = NormalizeHexInput(TxtHeaderColorHex.Text);
            if (int.TryParse(TxtSubHeaderFontSize.Text, out int shSize)) _settings.SubHeaderFontSize = shSize;
            _settings.SubHeaderColorHex = NormalizeHexInput(TxtSubHeaderColorHex.Text);
            if (int.TryParse(TxtStandardFontSize.Text, out int sSize)) _settings.StandardFontSize = sSize;
            _settings.StandardColorHex = NormalizeHexInput(TxtStandardColorHex.Text);
            // if (int.TryParse(TxtInputFontSize.Text, out int iSize)) _settings.InputFontSize = iSize;
            // _settings.InputColorHex = TxtInputColorHex.Text;
            if (int.TryParse(TxtSearchBoxFontSize.Text, out int sbSize)) _settings.SearchBoxFontSize = sbSize;
            _settings.SearchBoxColorHex = NormalizeHexInput(TxtSearchBoxColorHex.Text);
            _settings.SearchBoxBgColorHex = NormalizeHexInput(TxtSearchBoxBgColorHex.Text);

            if (int.TryParse(TxtGameNameHeaderFontSize.Text, out int gameNameHeaderSize)) _settings.GameNameHeaderFontSize = gameNameHeaderSize;
            _settings.GameNameHeaderColorHex = NormalizeHexInput(TxtGameNameHeaderColorHex.Text);
            if (int.TryParse(TxtPubYearRatingFontSize.Text, out int pubYearRatingSize)) _settings.PubYearRatingFontSize = pubYearRatingSize;
            _settings.PubYearRatingColorHex = NormalizeHexInput(TxtPubYearRatingColorHex.Text);
            if (int.TryParse(TxtInfoHeaderFontSize.Text, out int infoHeaderSize)) _settings.InfoHeaderFontSize = infoHeaderSize;
            _settings.InfoHeaderColorHex = NormalizeHexInput(TxtInfoHeaderColorHex.Text);
            if (int.TryParse(TxtInfoBodyFontSize.Text, out int infoBodySize)) _settings.InfoBodyFontSize = infoBodySize;
            _settings.InfoBodyColorHex = NormalizeHexInput(TxtInfoBodyColorHex.Text);
            if (int.TryParse(TxtCreditsHeaderFontSize.Text, out int creditsHeaderSize)) _settings.CreditsHeaderFontSize = creditsHeaderSize;
            _settings.CreditsHeaderColorHex = NormalizeHexInput(TxtCreditsHeaderColorHex.Text);
            if (int.TryParse(TxtCreditsBodyFontSize.Text, out int creditsBodySize)) _settings.CreditsBodyFontSize = creditsBodySize;
            _settings.CreditsBodyColorHex = NormalizeHexInput(TxtCreditsBodyColorHex.Text);
            _settings.VideoBgColorHex = NormalizeHexInput(TxtVideoBgColorHex.Text);
            if (double.TryParse(TxtVideoBorderSize.Text, out double videoBorderSize)) _settings.VideoBorderSize = videoBorderSize;
            _settings.VideoBorderColorHex = NormalizeHexInput(TxtVideoBorderColorHex.Text);
            if (double.TryParse(TxtVideoBorderRadius.Text, out double videoBorderRadius)) _settings.VideoBorderRadius = videoBorderRadius;
            _settings.NavIconsColorHex = NormalizeHexInput(TxtNavIconsColorHex.Text);
            if (double.TryParse(TxtNavIconsSize.Text, out double navIconsSize)) _settings.NavIconsSize = navIconsSize;
            if (double.TryParse(TxtPreviewBorderSize.Text, out double previewBorderSize)) _settings.PreviewBorderSize = previewBorderSize;
            _settings.PreviewBorderColorHex = NormalizeHexInput(TxtPreviewBorderColorHex.Text);
            if (double.TryParse(TxtPreviewBorderRadius.Text, out double previewBorderRadius)) _settings.PreviewBorderRadius = previewBorderRadius;

            if (int.TryParse(TxtSearchLabelFontSize.Text, out int searchLabelSize)) _settings.SearchLabelFontSize = searchLabelSize;
            _settings.SearchLabelColorHex = NormalizeHexInput(TxtSearchLabelColorHex.Text);
            _settings.SearchLabelBgColorHex = NormalizeHexInput(TxtSearchLabelBgColorHex.Text);

            if (int.TryParse(TxtMainWinBtnFontSize.Text, out int mainWinBtnSize)) _settings.MainWinBtnFontSize = mainWinBtnSize;
            _settings.MainWinBtnColorHex = NormalizeHexInput(TxtMainWinBtnColorHex.Text);
            _settings.MainWinBtnBgColorHex = NormalizeHexInput(TxtMainWinBtnBgColorHex.Text);
            _settings.MainWinBtnColorHoverHex = NormalizeHexInput(TxtMainWinBtnColorHoverHex.Text);
            _settings.MainWinBtnBgColorHoverHex = NormalizeHexInput(TxtMainWinBtnBgColorHoverHex.Text);
            if (double.TryParse(TxtMainWinBtnBorderSize.Text, out double mainWinBtnBorderSize)) _settings.MainWinBtnBorderSize = mainWinBtnBorderSize;
            _settings.MainWinBtnBorderColorHex = NormalizeHexInput(TxtMainWinBtnBorderColorHex.Text);
            if (double.TryParse(TxtMainWinBtnCornerRadius.Text, out double mainWinBtnCornerRadius)) _settings.MainWinBtnCornerRadius = mainWinBtnCornerRadius;

            if (double.TryParse(TxtGamesBorderSize.Text, out double gamesBorderSize)) _settings.GamesBorderSize = gamesBorderSize;
            _settings.GamesBorderColorHex = NormalizeHexInput(TxtGamesBorderColorHex.Text);
            if (double.TryParse(TxtGamesBorderCornerRadius.Text, out double gamesBorderCornerRadius)) _settings.GamesBorderCornerRadius = gamesBorderCornerRadius;

            _settings.ContextMenuFontColorHex = NormalizeHexInput(TxtContextMenuFontColorHex.Text);
            _settings.ContextMenuIconColorHex = NormalizeHexInput(TxtContextMenuIconColorHex.Text);
            _settings.ContextMenuBgColorHex = NormalizeHexInput(TxtContextMenuBgColorHex.Text);
            _settings.ContextMenuHoverColorHex = NormalizeHexInput(TxtContextMenuHoverColorHex.Text);
            _settings.ContextMenuHoverBgColorHex = NormalizeHexInput(TxtContextMenuHoverBgColorHex.Text);

            if (double.TryParse(TxtMarqueeBorderCornerRadius.Text, out double marqueeBorderRadius)) _settings.MarqueeBorderRadius = marqueeBorderRadius;

            if (int.TryParse(TxtSubTextFontSize.Text, out int subTextSize)) _settings.SubTextFontSize = subTextSize;
            _settings.SubTextColorHex = NormalizeHexInput(TxtSubTextColorHex.Text);
            _settings.OptionsMenuBgColorHex = NormalizeHexInput(TxtOptionsMenuBgColorHex.Text);
            if (double.TryParse(TxtOptionsMenuBorderSize.Text, out double optionsMenuBorderSize)) _settings.OptionsMenuBorderSize = optionsMenuBorderSize;
            _settings.OptionsMenuBorderColorHex = NormalizeHexInput(TxtOptionsMenuBorderColorHex.Text);
            if (double.TryParse(TxtOptionsMenuBorderRadius.Text, out double optionsMenuBorderRadius)) _settings.OptionsMenuBorderRadius = optionsMenuBorderRadius;
            _settings.TabColorHoverHex = NormalizeHexInput(TxtTabColorHoverHex.Text);
            _settings.TabBgColorHoverHex = NormalizeHexInput(TxtTabBgColorHoverHex.Text);
            if (int.TryParse(TxtOptionsBtnFontSize.Text, out int optionsBtnSize)) _settings.OptionsBtnFontSize = optionsBtnSize;
            _settings.OptionsBtnColorHoverHex = NormalizeHexInput(TxtOptionsBtnColorHoverHex.Text);
            if (double.TryParse(TxtOptionsBtnBorderSize.Text, out double optionsBtnBorderSize)) _settings.OptionsBtnBorderSize = optionsBtnBorderSize;
            if (double.TryParse(TxtOptionsBtnBorderRadius.Text, out double optionsBtnBorderRadius)) _settings.OptionsBtnBorderRadius = optionsBtnBorderRadius;

            _settings.BtnBgColorHex = NormalizeHexInput(TxtBtnBgColorHex.Text);
            _settings.BtnBorderColorHex = NormalizeHexInput(TxtBtnBorderColorHex.Text);
            _settings.BtnTextColorNormalHex = NormalizeHexInput(TxtBtnTextColorNormalHex.Text);
            _settings.BtnBgColorHoverHex = NormalizeHexInput(TxtBtnBgColorHoverHex.Text);
        }
        // [END SECTION: Sync UI to Settings]

        // [SECTION: File Browse Handler]
        // Shared handler for all seven image-asset browse buttons. Opens an image file picker, converts
        // the selection to a path relative to the MAME root when on the same drive (portable-friendly),
        // then routes the result to the correct TextBox based on which button raised the event.
        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            // Boot splash is the only asset that accepts an mp4 alongside an image, since it can resolve
            // to either a static image or a looping video (see IsBootSplashVideo/ThemeBootSplashVideoPath) -
            // every other browse button stays image-only.
            string dialogFilter = sender == BtnBrowseThemeBootSplash
                ? "Image & Video Files (*.png;*.jpg;*.jpeg;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Video Files (*.mp4)|*.mp4|All files (*.*)|*.*"
                : "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*";

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = dialogFilter,
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