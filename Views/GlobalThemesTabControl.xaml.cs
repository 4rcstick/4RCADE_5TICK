using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArcadeStick.Models;
using ArcadeStick.Services;

namespace ArcadeStick.Views
{
    // The Theme Builder's Global tab: theme-wide flat values on ConfigurationSettings (not per resolution).
    // Edits write straight into the settings and republish the app resources through GlobalThemeResources.Apply,
    // so every window follows live. Closing the Theme Builder without saving reverts them (ThemeBuilderWindow's
    // Global snapshot). Each field is one row in the tables built by BuildFieldTables.
    public partial class GlobalThemesTabControl : UserControl
    {
        private ConfigurationSettings? _settings;
        private bool _loading;
        private bool _suppressColorSync;

        // [SECTION: Field Tables]
        // A color field: picker, hex box, and how to read and write its settings property.
        private sealed record ColorField(ColorPicker.PortableColorPicker Picker, TextBox HexBox,
            Func<ConfigurationSettings, string> Get, Action<ConfigurationSettings, string> Set);

        // A number field: text box, read and write, the accepted range and whether only whole numbers are accepted.
        private sealed record NumberField(TextBox Box, Func<ConfigurationSettings, double> Get, Action<ConfigurationSettings, double> Set,
            double Min, double Max, bool WholeOnly);

        // Both color controls (picker and hex box) map to their field; number boxes map to theirs.
        private readonly Dictionary<object, ColorField> _colorFields = new();
        private readonly Dictionary<TextBox, NumberField> _numberFields = new();

        // Font sizes use the Games List range; border size and radius the Advanced tab's Border tab ranges.
        private const double MinFontSize = 6, MaxFontSize = 72;
        private const double MinBorderSize = 0, MaxBorderSize = 50;
        private const double MinBorderRadius = 0, MaxBorderRadius = 200;

        public GlobalThemesTabControl()
        {
            InitializeComponent();
            BuildFieldTables();
        }

        // Registers every Fonts and Backgrounds control with its ConfigurationSettings property
        private void BuildFieldTables()
        {
            AddColor(GlobalHeaderColorPicker, GlobalHeaderColorHex, s => s.HeaderColorHex, (s, v) => s.HeaderColorHex = v);
            AddColor(GlobalSubHeaderColorPicker, GlobalSubHeaderColorHex, s => s.SubHeaderColorHex, (s, v) => s.SubHeaderColorHex = v);
            AddColor(GlobalStandardColorPicker, GlobalStandardColorHex, s => s.StandardColorHex, (s, v) => s.StandardColorHex = v);
            AddColor(GlobalSubTextColorPicker, GlobalSubTextColorHex, s => s.SubTextColorHex, (s, v) => s.SubTextColorHex = v);
            AddNumber(GlobalHeaderSize, s => s.HeaderFontSize, (s, v) => s.HeaderFontSize = (int)v, MinFontSize, MaxFontSize, true);
            AddNumber(GlobalSubHeaderSize, s => s.SubHeaderFontSize, (s, v) => s.SubHeaderFontSize = (int)v, MinFontSize, MaxFontSize, true);
            AddNumber(GlobalStandardSize, s => s.StandardFontSize, (s, v) => s.StandardFontSize = (int)v, MinFontSize, MaxFontSize, true);
            AddNumber(GlobalSubTextSize, s => s.SubTextFontSize, (s, v) => s.SubTextFontSize = (int)v, MinFontSize, MaxFontSize, true);

            AddColor(GlobalBgColorPicker, GlobalBgColorHex, s => s.OptionsMenuBgColorHex, (s, v) => s.OptionsMenuBgColorHex = v);
            AddColor(GlobalContentBgColorPicker, GlobalContentBgColorHex, s => s.OptionsBg, (s, v) => s.OptionsBg = v);
            AddColor(GlobalSeparatorColorPicker, GlobalSeparatorColorHex, s => s.SeparatorColorHex, (s, v) => s.SeparatorColorHex = v);
            AddColor(GlobalBorderColorPicker, GlobalBorderColorHex, s => s.OptionsMenuBorderColorHex, (s, v) => s.OptionsMenuBorderColorHex = v);
            AddNumber(GlobalBorderSize, s => s.OptionsMenuBorderSize, (s, v) => s.OptionsMenuBorderSize = v, MinBorderSize, MaxBorderSize, false);
            AddNumber(GlobalBorderRadius, s => s.OptionsMenuBorderRadius, (s, v) => s.OptionsMenuBorderRadius = v, MinBorderRadius, MaxBorderRadius, false);
        }

        // Adds one color field to the table, keyed by both its picker and its hex box
        private void AddColor(ColorPicker.PortableColorPicker picker, TextBox hexBox, Func<ConfigurationSettings, string> get, Action<ConfigurationSettings, string> set)
        {
            var field = new ColorField(picker, hexBox, get, set);
            _colorFields[picker] = field;
            _colorFields[hexBox] = field;
        }

        // Adds one number field to the table
        private void AddNumber(TextBox box, Func<ConfigurationSettings, double> get, Action<ConfigurationSettings, double> set, double min, double max, bool wholeOnly)
        {
            _numberFields[box] = new NumberField(box, get, set, min, max, wholeOnly);
        }
        // [END SECTION: Field Tables]

        // [SECTION: Initialization and Loading]
        // Takes the live settings from the Theme Builder window and fills every field
        public void Initialize(ConfigurationSettings settings)
        {
            _settings = settings;
            ReloadFromSettings();
        }

        // Re-reads every field from the settings (after a theme load, Restore Defaults or a reverted edit)
        public void ReloadFromSettings()
        {
            if (_settings == null) return;

            _loading = true;
            foreach (var field in new HashSet<ColorField>(_colorFields.Values)) ShowColor(field);
            foreach (var field in _numberFields.Values) ShowNumber(field);
            _loading = false;
        }

        // Shows a stored color in its picker and hex box without firing the picker's change handler. A blank or unreadable
        // stored value shows the property's default, which is also what GlobalThemeResources.Apply publishes for it.
        private void ShowColor(ColorField field)
        {
            string hex = field.Get(_settings!);
            Color? color = GlobalThemeResources.ParseColor(hex);
            if (color == null)
            {
                hex = field.Get(GlobalThemeResources.DefaultSettings);
                color = GlobalThemeResources.ParseColor(hex);
            }

            field.HexBox.Text = hex;
            if (color == null) return;
            _suppressColorSync = true;
            field.Picker.SelectedColor = color.Value;
            _suppressColorSync = false;
        }

        // Shows a stored number in its box
        private void ShowNumber(NumberField field)
        {
            field.Box.Text = field.Get(_settings!).ToString(CultureInfo.InvariantCulture);
        }
        // [END SECTION: Initialization and Loading]

        // [SECTION: Live Edit Handlers]
        // Stores a picked color and republishes the Global resources
        private void GlobalColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressColorSync || _loading || _settings == null) return;
            if (!_colorFields.TryGetValue(sender, out var field)) return;

            var c = field.Picker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            field.HexBox.Text = hex;
            field.Set(_settings, hex);
            GlobalThemeResources.Apply(_settings);
        }

        // Stores a typed hex color and syncs the picker; invalid input reverts to the stored value
        private void GlobalColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (!_colorFields.TryGetValue(sender, out var field)) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(field.HexBox.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressColorSync = true;
                field.Picker.SelectedColor = c;
                _suppressColorSync = false;
                field.HexBox.Text = hex;
                field.Set(_settings, hex);
                GlobalThemeResources.Apply(_settings);
            }
            catch
            {
                _suppressColorSync = false;
                ShowColor(field);
            }
        }

        // Enter applies a number field
        private void GlobalField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyNumberField(sender);
        }

        // Focus loss applies a number field
        private void GlobalField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyNumberField(sender);
        }

        // Stores a number within its range and republishes the Global resources; anything else reverts to the stored value
        private void ApplyNumberField(object sender)
        {
            if (_loading || _settings == null) return;
            if (sender is not TextBox box || !_numberFields.TryGetValue(box, out var field)) return;

            string text = box.Text.Trim();
            double value;
            bool parsed;
            if (field.WholeOnly)
            {
                parsed = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int whole);
                value = whole;
            }
            else
            {
                parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }

            if (parsed && value >= field.Min && value <= field.Max)
            {
                field.Set(_settings, value);
                GlobalThemeResources.Apply(_settings);
            }
            ShowNumber(field);
        }
        // [END SECTION: Live Edit Handlers]

        // [SECTION: Restore Defaults]
        // Resets the selected subtab's values (its Tag names the GlobalArea) to the code defaults and applies them live
        private void BtnRestoreGlobalDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            if (TabsGlobal.SelectedItem is not TabItem tab || tab.Tag is not string tag) return;
            if (!Enum.TryParse(tag, out GlobalArea area)) return;

            GlobalThemeResources.RestoreDefaults(_settings, area);
            ReloadFromSettings();
        }
        // [END SECTION: Restore Defaults]
    }
}
