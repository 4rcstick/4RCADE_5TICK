using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcadeStick.Views
{
    // Shows a theme's author on one line, with an edit mode the host opens through BeginEdit. Content only: headings and glyphs belong to the host.
    public partial class ThemeAuthorControl : UserControl
    {
        private string _value = "";

        // Raised with the trimmed text when Apply commits an edit
        public event Action<string>? ValueApplied;

        public ThemeAuthorControl()
        {
            InitializeComponent();
        }

        // Shows a stored value and closes any open edit, discarding its text
        public void ShowValue(string? value)
        {
            _value = (value ?? "").Trim();
            TxtValue.Text = _value;
            CancelEdit();
        }

        // Opens the edit box pre-filled with the current value
        public void BeginEdit()
        {
            TxtEdit.Text = _value;
            TxtValue.Visibility = Visibility.Collapsed;
            PnlEdit.Visibility = Visibility.Visible;
            TxtEdit.Focus();
            TxtEdit.SelectAll();
        }

        // Closes the edit box without committing
        public void CancelEdit()
        {
            PnlEdit.Visibility = Visibility.Collapsed;
            TxtValue.Visibility = Visibility.Visible;
        }

        // Commits the edit box text and tells the host
        private void ApplyEdit()
        {
            ShowValue(TxtEdit.Text);
            ValueApplied?.Invoke(_value);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e) => ApplyEdit();

        // Enter commits, the same as Apply
        private void TxtEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyEdit();
        }
    }
}
