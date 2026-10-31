using ArcadeStick.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    // Shows a theme's description as wrapped, scrolling text, with an edit mode the host opens through BeginEdit. Content only: headings and glyphs belong to the host.
    public partial class ThemeDescriptionControl : UserControl
    {
        // The longest description the edit box accepts
        public const int MaxDescriptionLength = 500;

        private const double FallbackOpacity = 0.55;
        private string _value = "";

        // Raised with the trimmed text when Apply commits an edit
        public event Action<string>? ValueApplied;

        public ThemeDescriptionControl()
        {
            InitializeComponent();
            TxtEdit.MaxLength = MaxDescriptionLength;
        }

        // Shows a stored value (or the dimmer fallback when blank) and closes any open edit, discarding its text
        public void ShowValue(string? value)
        {
            _value = (value ?? "").Trim();
            bool blank = _value.Length == 0;
            TxtValue.Text = blank ? ThemeInfoFallbacks.NoDescription : _value;
            TxtValue.Opacity = blank ? FallbackOpacity : 1;
            CancelEdit();
        }

        // Opens the edit box pre-filled with the current value
        public void BeginEdit()
        {
            TxtEdit.Text = _value;
            ScrollValue.Visibility = Visibility.Collapsed;
            PnlEdit.Visibility = Visibility.Visible;
            TxtEdit.Focus();
            TxtEdit.CaretIndex = TxtEdit.Text.Length;
        }

        // Closes the edit box without committing
        public void CancelEdit()
        {
            PnlEdit.Visibility = Visibility.Collapsed;
            ScrollValue.Visibility = Visibility.Visible;
        }

        // Only Apply commits; Enter in the box is a normal line break
        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ShowValue(TxtEdit.Text);
            ValueApplied?.Invoke(_value);
        }

        // The shared text box template hides its scrollbars for one-line use, so this box turns its own on
        private void TxtEdit_Loaded(object sender, RoutedEventArgs e)
        {
            if (TxtEdit.Template.FindName("PART_ContentHost", TxtEdit) is ScrollViewer host)
            {
                host.Style = (Style)FindResource("ArcadeDarkScrollViewerThemed");
                host.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
        }
    }
}
