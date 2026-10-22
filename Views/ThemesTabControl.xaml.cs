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

        // TEMPLATE for Claude Code: this field plus the sync block below are the pattern to replicate
        // per Theme*ColorHex field (rename to match, e.g. _suppressGameColorSync). Prevents the picker's
        // ColorChanged and the hex textbox's LostFocus from re-triggering each other.
        private bool _suppressTabBgColorSync;
        private bool _suppressTabActiveColorSync;
        private bool _suppressTabActiveBgColorSync;
        private bool _suppressFrameworkBorderColorSync;
        private bool _suppressBtnTextColorNormalSync;
        private bool _suppressBtnBgColorSync;
        private bool _suppressBtnBorderColorSync;
        private bool _suppressBtnBgColorHoverSync;
        private bool _suppressScrollThumbColorSync;
        private bool _suppressScrollThumbHoverColorSync;
        private bool _suppressScrollThumbDragColorSync;
        private bool _suppressScrollTrackColorSync;
        private bool _suppressScrollTrackHoverColorSync;
        private bool _suppressTabColorSync;
        private bool _suppressContextMenuFontColorSync;
        private bool _suppressContextMenuIconColorSync;
        private bool _suppressContextMenuBgColorSync;
        private bool _suppressContextMenuHoverColorSync;
        private bool _suppressContextMenuHoverBgColorSync;
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

        // Re-reads every field from _settings WITHOUT writing anything back first (unlike SyncUiToSettings,
        // which would overwrite freshly loaded values with the tab's stale field contents). Used when the
        // theme is switched from outside this tab (the main window's hamburger menu).
        public void ReloadFromSettings()
        {
            RefreshThemeList();
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

        // BorderColorFramework has no field: the chrome borders it drove now use the fixed ChromeBorder palette
        // color. Only the main window's context menu border still reads it (through ThemeBorderColor) until the
        // Global tab's Context Menus pass gives the menus their own border color.

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

            // Color System Enforcements (Filtered through Gatekeeper)
            
            // BorderColorFramework has no field (see the note above BtnTextColorNormalPicker_ColorChanged).
            
                        
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
            // has no field, see the earlier note).

            // Typography & State Colors (Filtered through Gatekeeper)
            
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

        // [SUB-SECTION: Save Theme - SYNC POINT 2 of 3]
        // Validates the theme name (prompting for one if "[New Theme...]" is selected), syncs the UI into
        // _settings, builds a ThemeSettings DTO, bundles every layout entry's ImagePath into a staging assets/
        // folder (rewriting the DTO's copies of those paths to archive-relative paths), then zips the result to the theme's .zip file. DANGER: any new theme property must be added to this
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
            

            
            // BorderColorFramework sync removed (both occurrences) - no UI field exists yet.
            // BorderWidthValue/BorderCurveValue sync removed - properties retired entirely.
            _settings.ScrollTrackColor = NormalizeHexInput(TxtScrollTrackColorHex.Text);
            _settings.ScrollTrackHoverColor = NormalizeHexInput(TxtScrollTrackHoverColorHex.Text);
            _settings.ScrollThumbColor = NormalizeHexInput(TxtScrollThumbColorHex.Text);
            _settings.ScrollThumbHoverColor = NormalizeHexInput(TxtScrollThumbHoverColorHex.Text);
            _settings.ScrollThumbDragColor = NormalizeHexInput(TxtScrollThumbDragColorHex.Text);

            

            

            if (int.TryParse(TxtTabFontSize.Text, out int tSize)) _settings.TabFontSize = tSize;
            _settings.TabColorHex = NormalizeHexInput(TxtTabColorHex.Text);
            _settings.TabBgColorHex = NormalizeHexInput(TxtTabBgColorHex.Text);
            _settings.TabActiveColorHex = NormalizeHexInput(TxtTabActiveColorHex.Text);
            _settings.TabActiveBgColorHex = NormalizeHexInput(TxtTabActiveBgColorHex.Text);


            

            _settings.ContextMenuFontColorHex = NormalizeHexInput(TxtContextMenuFontColorHex.Text);
            _settings.ContextMenuIconColorHex = NormalizeHexInput(TxtContextMenuIconColorHex.Text);
            _settings.ContextMenuBgColorHex = NormalizeHexInput(TxtContextMenuBgColorHex.Text);
            _settings.ContextMenuHoverColorHex = NormalizeHexInput(TxtContextMenuHoverColorHex.Text);
            _settings.ContextMenuHoverBgColorHex = NormalizeHexInput(TxtContextMenuHoverBgColorHex.Text);

            

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
    }
}