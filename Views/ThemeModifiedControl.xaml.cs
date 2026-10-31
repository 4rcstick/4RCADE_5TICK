using ArcadeStick.Models;
using System;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    // Shows when a theme was last saved with Save Changes. Content only: headings belong to the host.
    public partial class ThemeModifiedControl : UserControl
    {
        private const double FallbackOpacity = 0.6;

        public ThemeModifiedControl()
        {
            InitializeComponent();
            ShowValue(null);
        }

        // Shows the stored date in local time, or the fallback for null (or an empty date, so 1/1/0001 never shows)
        public void ShowValue(DateTime? modifiedUtc)
        {
            bool saved = modifiedUtc is DateTime date && date != default;
            TxtValue.Text = saved ? $"Last saved: {modifiedUtc!.Value.ToLocalTime():g}" : ThemeInfoFallbacks.NotSavedYet;
            TxtValue.Opacity = saved ? 1 : FallbackOpacity;
        }
    }
}
